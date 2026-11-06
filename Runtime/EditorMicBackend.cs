// ============================================================================
// Purpose:  Editor-only Windows microphone capture via Unity.Microphone, the voxr-frontend native front end (48→16 kHz filter, AGC2, int16) and libvosk P/Invoke
// Layer:    Runtime (UNITY_EDITOR_WIN only)
// Owns:     EditorMicBackend (internal sealed class)
// Depends:  FrontEndNative, VoxrNative, BridgeNative, VoxrBridgeErrorCode
// ============================================================================
#if UNITY_EDITOR_WIN
using System;
using System.Threading.Tasks;
using UnityEngine;
using VoXR.Native;

namespace VoXR
{
    // Custom delegate (not Action<>) because generic delegates cannot accept
    // ref-struct parameters such as ReadOnlySpan<byte>.
    internal delegate void EditorJsonDispatcher(ReadOnlySpan<byte> json, bool isFinal);

    internal sealed class EditorMicBackend
    {
        const int CaptureLengthSeconds = 5;
        const int SourceSampleRate = 48000;

        IntPtr _model;
        IntPtr _recognizer;
        float _sampleRate;

        // Set by Release() so a still-in-flight model-load continuation knows
        // to free its result instead of assigning it to a dead instance.
        // volatile because the Task.Run continuation may resume on the thread
        // pool before the UnitySynchronizationContext posts it back to main.
        volatile bool _released;

        IntPtr _frontEnd;
        voxr_fe_stats _stats;

        AudioClip _clip;
        int _lastSamplePos;
        bool _firstTick;

        float[] _readBuffer;
        float[] _workBuffer;
        short[] _int16Buffer;

        byte[] _lastPartialBuffer;
        int _lastPartialLength;

        // WAV playback state (test seam; design §6.1). Allocated only by
        // StartPlayback so the microphone path never pays for the mode.
        float[] _playbackSamples;
        float[] _playbackChunk;
        int _playbackChunkSamples;
        int _playbackPos;

        internal bool IsInitialised { get; private set; }
        internal bool IsRunning { get; private set; }

        internal float PreAgcRms => _stats.pre_rms;
        internal float PostAgcRms => _stats.post_rms;
        internal float AgcGain =>
            _frontEnd == IntPtr.Zero ? 1f : (float)Math.Pow(10.0, _stats.gain_db / 20.0);
        internal voxr_fe_stats FrontEndStats => _stats;

        // Rolling ~300 ms RMS of pre-DSP audio, linear 0..1. Mirrors
        // vosk_bridge_get_input_level so the managed and native branches of
        // VoxrSpeechRecogniser.InputLevel report the same thing.
        internal float InputLevel { get; private set; }

        internal async Task<bool> InitialiseAsync(
            string modelPath,
            float sampleRate,
            float micGainTargetDb,
            Action<VoxrBridgeErrorCode, string> fireError)
        {
            if (IsInitialised) return true;

            _sampleRate = sampleRate;

            if (!ProbeFrontEnd(FrontEndNative.voxr_fe_abi_version, fireError))
                return false;

            try
            {
                VoxrNative.vosk_set_log_level(-1);

                IntPtr model = await Task.Run(() => VoxrNative.vosk_model_new(modelPath));

                if (_released)
                {
                    if (model != IntPtr.Zero)
                        VoxrNative.vosk_model_free(model);
                    return false;
                }

                if (model == IntPtr.Zero)
                {
                    fireError?.Invoke(VoxrBridgeErrorCode.ModelLoadFailed,
                        $"vosk_model_new returned NULL for path: {modelPath}");
                    return false;
                }
                _model = model;

                _recognizer = VoxrNative.vosk_recognizer_new(_model, _sampleRate);
                if (_recognizer == IntPtr.Zero)
                {
                    fireError?.Invoke(VoxrBridgeErrorCode.ModelLoadFailed,
                        "vosk_recognizer_new returned NULL");
                    VoxrNative.vosk_model_free(_model);
                    _model = IntPtr.Zero;
                    return false;
                }

                VoxrNative.vosk_recognizer_set_words(_recognizer, 1);

                _frontEnd = FrontEndNative.voxr_fe_create(micGainTargetDb);
                if (_frontEnd == IntPtr.Zero)
                {
                    fireError?.Invoke(VoxrBridgeErrorCode.ModelLoadFailed,
                        $"voxr_fe_create returned NULL for target {micGainTargetDb} dBFS");
                    VoxrNative.vosk_recognizer_free(_recognizer);
                    _recognizer = IntPtr.Zero;
                    VoxrNative.vosk_model_free(_model);
                    _model = IntPtr.Zero;
                    return false;
                }
                FrontEndNative.voxr_fe_get_stats(_frontEnd, out _stats);

                IsInitialised = true;
                return true;
            }
            catch (DllNotFoundException ex)
            {
                fireError?.Invoke(VoxrBridgeErrorCode.ModelLoadFailed,
                    "libvosk.dll (or a MinGW runtime dependency) not found in " +
                    "Runtime/Plugins/x86_64/. Download vosk-win64 from " +
                    $"https://github.com/alphacep/vosk-api/releases and drop the " +
                    $"DLLs into that folder. Details: {ex.Message}");
                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                fireError?.Invoke(VoxrBridgeErrorCode.ModelLoadFailed,
                    $"libvosk.dll is missing an expected export — version mismatch? " +
                    $"Details: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                fireError?.Invoke(VoxrBridgeErrorCode.ModelLoadFailed,
                    $"EditorMicBackend.InitialiseAsync failed: {ex.Message}");
                return false;
            }
        }

        internal static bool ProbeFrontEnd(
            Func<int> abiVersion,
            Action<VoxrBridgeErrorCode, string> fireError)
        {
            string detail;
            try
            {
                int version = abiVersion();
                if (version == FrontEndNative.AbiVersion)
                    return true;
                detail = $"ABI version {version}, expected {FrontEndNative.AbiVersion}";
            }
            catch (DllNotFoundException ex)
            {
                detail = $"not found: {ex.Message}";
            }
            catch (EntryPointNotFoundException ex)
            {
                detail = $"missing an expected export: {ex.Message}";
            }
            catch (Exception ex)
            {
                detail = ex.Message;
            }
            fireError?.Invoke(VoxrBridgeErrorCode.ModelLoadFailed,
                "voxr-frontend.dll failed to load from Runtime/Plugins/x86_64/; the Editor's " +
                $"front end is unavailable. Details: {detail}");
            return false;
        }

        internal bool Start(Action<VoxrBridgeErrorCode, string> fireError)
        {
            if (!IsInitialised)
            {
                fireError?.Invoke(VoxrBridgeErrorCode.NotInitialised,
                    "EditorMicBackend.Start called before InitialiseAsync.");
                return false;
            }
            if (IsRunning) return true;

            if (IsInPlayback)
            {
                fireError?.Invoke(
                    VoxrBridgeErrorCode.AlreadyRunning,
                    "EditorMicBackend.Start called while WAV playback is active. "
                        + "Call StopPlayback first."
                );
                return false;
            }

            if (Microphone.devices.Length == 0)
            {
                fireError?.Invoke(VoxrBridgeErrorCode.AudioDeviceUnavailable,
                    "No microphone devices detected on this system.");
                return false;
            }

            _clip = Microphone.Start(
                deviceName: null,
                loop: true,
                lengthSec: CaptureLengthSeconds,
                frequency: SourceSampleRate);

            if (_clip == null)
            {
                fireError?.Invoke(VoxrBridgeErrorCode.AudioDeviceUnavailable,
                    "Microphone.Start returned null — audio device unavailable or " +
                    "Windows microphone permission denied.");
                return false;
            }

            _lastSamplePos = 0;
            _firstTick = true;
            _lastPartialLength = 0;

            int clipSamples = _clip.samples;
            int workSize = Math.Max(clipSamples / 10, SourceSampleRate / 10);
            _readBuffer = new float[clipSamples];
            _workBuffer = new float[workSize];
            _int16Buffer = new short[workSize / FrontEndNative.DecimationFactor + FrontEndNative.FrameSamples];

            FrontEndNative.voxr_fe_begin_stream(_frontEnd);
            FrontEndNative.voxr_fe_get_stats(_frontEnd, out _stats);
            InputLevel = 0f;
            VoxrNative.vosk_recognizer_reset(_recognizer);

            IsRunning = true;
            return true;
        }

        internal void Stop(EditorJsonDispatcher dispatch)
        {
            if (!IsRunning) return;
            IsRunning = false;
            InputLevel = 0f;

            // Flush any in-progress utterance so the last command spoken before
            // StopRecognition() is not silently discarded. Matches what the
            // Android recognition thread does at exit (vosk_bridge.cpp:130-134).
            // The span wraps libvosk's internal result buffer, valid only until
            // the next vosk_recognizer_*_result call. Consume inline; do not store.
            if (_recognizer != IntPtr.Zero && dispatch != null)
            {
                ReadOnlySpan<byte> finalJson = BridgeNative.SpanFromNullTerminated(
                    VoxrNative.vosk_recognizer_final_result(_recognizer));
                if (finalJson.Length > 0)
                    dispatch(finalJson, true);
            }

            if (_clip != null)
            {
                Microphone.End(null);
                _clip = null;
            }
        }

        internal void Reset()
        {
            if (!IsInitialised || IsInPlayback)
                return;
            VoxrNative.vosk_recognizer_reset(_recognizer);
            FrontEndNative.voxr_fe_reset(_frontEnd);
            FrontEndNative.voxr_fe_get_stats(_frontEnd, out _stats);
            _lastPartialLength = 0;
        }

        internal void SetGrammar(
            string grammarJson,
            Action<VoxrBridgeErrorCode, string> fireError)
        {
            if (!IsInitialised)
            {
                fireError?.Invoke(VoxrBridgeErrorCode.NotInitialised,
                    "EditorMicBackend.SetGrammar called before InitialiseAsync.");
                return;
            }
            if (IsRunning)
            {
                fireError?.Invoke(VoxrBridgeErrorCode.AlreadyRunning,
                    "EditorMicBackend.SetGrammar called while recognition is running. " +
                    "Stop recognition first.");
                return;
            }
            if (IsInPlayback)
            {
                fireError?.Invoke(
                    VoxrBridgeErrorCode.AlreadyRunning,
                    "EditorMicBackend.SetGrammar called while WAV playback is active. "
                        + "The recognizer cannot be recreated mid-stream; stop playback first."
                );
                return;
            }

            // Authoring aid: a grammar word the model does not know is dropped silently by
            // the decoder (the Kaldi warning is suppressed by vosk_set_log_level(-1) in the
            // bridge). This is the only point where a grammar and a live model handle meet —
            // grammar generation can run before the model finishes loading.
            VoxrGrammarVocabulary.WarnOnUnknownWords(grammarJson, _model);

            // VOSK has no grammar-swap API — we must free and recreate the recognizer.
            if (_recognizer != IntPtr.Zero)
            {
                VoxrNative.vosk_recognizer_free(_recognizer);
                _recognizer = IntPtr.Zero;
            }

            if (!string.IsNullOrEmpty(grammarJson))
                _recognizer = VoxrNative.vosk_recognizer_new_grm(
                    _model, _sampleRate, grammarJson);
            else
                _recognizer = VoxrNative.vosk_recognizer_new(_model, _sampleRate);

            if (_recognizer == IntPtr.Zero)
            {
                fireError?.Invoke(VoxrBridgeErrorCode.ModelLoadFailed,
                    "vosk_recognizer_new failed during SetGrammar.");
                return;
            }

            VoxrNative.vosk_recognizer_set_words(_recognizer, 1);
        }

        internal void Release()
        {
            _released = true;
            Stop(dispatch: null);

            if (_frontEnd != IntPtr.Zero)
            {
                FrontEndNative.voxr_fe_destroy(_frontEnd);
                _frontEnd = IntPtr.Zero;
            }
            _stats = default;

            if (_recognizer != IntPtr.Zero)
            {
                VoxrNative.vosk_recognizer_free(_recognizer);
                _recognizer = IntPtr.Zero;
            }
            if (_model != IntPtr.Zero)
            {
                VoxrNative.vosk_model_free(_model);
                _model = IntPtr.Zero;
            }

            _readBuffer = null;
            _workBuffer = null;
            _int16Buffer = null;
            _lastPartialBuffer = null;
            _lastPartialLength = 0;
            _playbackSamples = null;
            _playbackChunk = null;
            _playbackPos = 0;

            IsInitialised = false;
        }

        internal void Tick(
            Action<VoxrBridgeErrorCode, string> fireError,
            EditorJsonDispatcher dispatch)
        {
            if (!IsRunning || _clip == null) return;

            if (_firstTick)
            {
                _firstTick = false;
                int actualRate = _clip.frequency;
                if (actualRate != SourceSampleRate)
                {
                    fireError?.Invoke(VoxrBridgeErrorCode.AudioDeviceUnavailable,
                        $"Microphone returned {actualRate} Hz; {SourceSampleRate} Hz is required. " +
                        "The 48 kHz → 16 kHz downsampler cannot handle other input rates in v3.1.");
                    Stop(dispatch);
                    return;
                }
            }

            int clipSamples = _clip.samples;
            int currentPos = Microphone.GetPosition(null);
            int available = (currentPos - _lastSamplePos + clipSamples) % clipSamples;
            if (available == 0) return;

            // If this frame produced more samples than the work buffer can hold
            // (dropped frame / GC spike), cap the pull — the ring buffer has
            // already advanced past the oldest data anyway.
            if (available > _workBuffer.Length)
                available = _workBuffer.Length;

            // Read the entire clip once, then copy the new window into the work
            // buffer with modular-wrap indexing. Reading the full clip is a single
            // native memcpy and sidesteps AudioClip.GetData's wrap semantics.
            _clip.GetData(_readBuffer, 0);

            int firstChunk = Math.Min(available, clipSamples - _lastSamplePos);
            Array.Copy(_readBuffer, _lastSamplePos, _workBuffer, 0, firstChunk);
            int remaining = available - firstChunk;
            if (remaining > 0)
                Array.Copy(_readBuffer, 0, _workBuffer, firstChunk, remaining);

            _lastSamplePos = (_lastSamplePos + available) % clipSamples;

            ProcessChunk(_workBuffer, available, dispatch);
        }

        void ProcessChunk(float[] samples, int count, EditorJsonDispatcher dispatch)
        {
            if (count > 0)
            {
                float chunkRms = ComputeRms(samples, count);
                float chunkMs = count * 1000f / SourceSampleRate;
                float alpha = 1f - (float)Math.Exp(-chunkMs / 300f);
                InputLevel += alpha * (chunkRms - InputLevel);
            }

            int written = FrontEndNative.voxr_fe_process(
                _frontEnd, samples, count, _int16Buffer, _int16Buffer.Length);
            if (written < 0)
                throw new InvalidOperationException(
                    $"voxr_fe_process refused {count} samples with capacity {_int16Buffer.Length}.");
            FrontEndNative.voxr_fe_get_stats(_frontEnd, out _stats);
            if (written == 0) return;

            int result = VoxrNative.vosk_recognizer_accept_waveform_s(
                _recognizer, _int16Buffer, written);

            if (result == 1)
            {
                ReadOnlySpan<byte> json = BridgeNative.SpanFromNullTerminated(
                    VoxrNative.vosk_recognizer_result(_recognizer));
                if (json.Length > 0)
                    dispatch?.Invoke(json, true);
            }
            else
            {
                ReadOnlySpan<byte> json = BridgeNative.SpanFromNullTerminated(
                    VoxrNative.vosk_recognizer_partial_result(_recognizer));
                if (json.Length > 0 && !PartialMatchesLast(json))
                {
                    StoreLastPartial(json);
                    dispatch?.Invoke(json, false);
                }
            }
        }

        // Fixed chunk so replay results are machine- and frame-rate-independent:
        // 4800 samples = 100 ms at 48 kHz, divisible by the decimation factor.
        internal const int PlaybackChunkSamples = 4800;

        internal bool IsInPlayback => _playbackSamples != null;

        internal bool StartPlayback(
            float[] samples,
            int sampleRate,
            Action<VoxrBridgeErrorCode, string> fireError,
            int chunkSamples = PlaybackChunkSamples
        )
        {
            if (!IsInitialised)
            {
                fireError?.Invoke(
                    VoxrBridgeErrorCode.NotInitialised,
                    "EditorMicBackend.StartPlayback called before InitialiseAsync."
                );
                return false;
            }
            if (IsRunning)
            {
                fireError?.Invoke(
                    VoxrBridgeErrorCode.AlreadyRunning,
                    "EditorMicBackend.StartPlayback called while the microphone is "
                        + "running. Stop recognition first."
                );
                return false;
            }
            if (samples == null || samples.Length == 0)
            {
                fireError?.Invoke(
                    VoxrBridgeErrorCode.AudioDeviceUnavailable,
                    "EditorMicBackend.StartPlayback called with no samples."
                );
                return false;
            }
            if (sampleRate != SourceSampleRate)
            {
                fireError?.Invoke(
                    VoxrBridgeErrorCode.AudioDeviceUnavailable,
                    $"StartPlayback received {sampleRate} Hz audio; {SourceSampleRate} Hz "
                        + "is required. The 48 kHz → 16 kHz downsampler cannot handle other "
                        + "input rates; resample the fixture instead."
                );
                return false;
            }
            if (chunkSamples < 1)
            {
                fireError?.Invoke(
                    VoxrBridgeErrorCode.AudioDeviceUnavailable,
                    $"EditorMicBackend.StartPlayback called with a {chunkSamples}-sample chunk; "
                        + "at least 1 is required."
                );
                return false;
            }

            _playbackSamples = samples;
            _playbackPos = 0;
            _playbackChunkSamples = chunkSamples;
            if (_playbackChunk == null || _playbackChunk.Length < chunkSamples)
                _playbackChunk = new float[chunkSamples];

            int int16Size = chunkSamples / FrontEndNative.DecimationFactor + FrontEndNative.FrameSamples;
            if (_int16Buffer == null || _int16Buffer.Length < int16Size)
                _int16Buffer = new short[int16Size];

            FrontEndNative.voxr_fe_reset(_frontEnd);
            FrontEndNative.voxr_fe_get_stats(_frontEnd, out _stats);
            InputLevel = 0f;
            _lastPartialLength = 0;
            VoxrNative.vosk_recognizer_reset(_recognizer);

            return true;
        }

        internal bool TickPlayback(EditorJsonDispatcher dispatch)
        {
            if (!IsInPlayback)
                return false;

            int remaining = _playbackSamples.Length - _playbackPos;
            if (remaining > 0)
            {
                int count = Math.Min(_playbackChunkSamples, remaining);
                Array.Copy(_playbackSamples, _playbackPos, _playbackChunk, 0, count);
                _playbackPos += count;
                ProcessChunk(_playbackChunk, count, dispatch);
                return true;
            }

            if (_recognizer != IntPtr.Zero && dispatch != null)
            {
                ReadOnlySpan<byte> finalJson = BridgeNative.SpanFromNullTerminated(
                    VoxrNative.vosk_recognizer_final_result(_recognizer)
                );
                if (finalJson.Length > 0)
                    dispatch(finalJson, true);
            }
            StopPlayback();
            return false;
        }

        // Disarms playback without flushing — safe to call from test teardown.
        // The chunk buffer is kept for reuse across replays; Release() frees it.
        internal void StopPlayback()
        {
            _playbackSamples = null;
            _playbackPos = 0;
            InputLevel = 0f;
        }

        bool PartialMatchesLast(ReadOnlySpan<byte> json)
        {
            if (_lastPartialBuffer == null || _lastPartialLength != json.Length)
                return false;
            return new ReadOnlySpan<byte>(_lastPartialBuffer, 0, _lastPartialLength)
                .SequenceEqual(json);
        }

        void StoreLastPartial(ReadOnlySpan<byte> json)
        {
            if (_lastPartialBuffer == null || _lastPartialBuffer.Length < json.Length)
                Array.Resize(ref _lastPartialBuffer, json.Length);
            json.CopyTo(_lastPartialBuffer);
            _lastPartialLength = json.Length;
        }

        internal static float ComputeRms(float[] samples, int count)
        {
            float sum = 0f;
            for (int i = 0; i < count; i++)
                sum += samples[i] * samples[i];
            return count > 0 ? (float)Math.Sqrt(sum / count) : 0f;
        }
    }
}
#endif
