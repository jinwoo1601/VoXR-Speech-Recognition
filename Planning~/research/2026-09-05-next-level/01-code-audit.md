# VoXR code audit — structural constraints and redesign seams (2026-09-05)

Scope: the shipped package as of v2.0.0 (`package.json:3`). Every claim below carries a `file:line` verified by reading. No design proposals.

Conventions: paths are relative to the repo root `/mnt/d/Game Development/VoXR-Speech-Recognition`. `~`-suffixed folders (`NativeBridge~`, `Tests~`, `Documentation~`, `Samples~`, `Planning~`) are stripped by Unity's package importer and never compile in a consumer project.

---

## 1. Component map

### 1.1 C# runtime

| Component | Responsibility | Lines | Thread | Depends on |
|---|---|---|---|---|
| `VoxrSpeechRecogniser` (`Runtime/VoxrSpeechRecogniser.cs:20`) | MonoBehaviour owning decoder lifecycle, result polling, event dispatch | 660 | Unity main thread only (`Update()` at :508, `AssertMainThread` at :500) | `BridgeNative`, `EditorMicBackend`, `ModelExtractor`, `VoxrJsonParser` |
| `EditorMicBackend` (`Runtime/EditorMicBackend.cs:20`) | `UNITY_EDITOR_WIN`-only mic capture + DSP + libvosk P/Invoke, plus a WAV-playback seam | 563 | Main thread (driven from `VoxrSpeechRecogniser.Update`); one `Task.Run` for model load (:80) | `Downsampler`, `Agc`, `VoxrNative`, `BridgeNative.SpanFrom*` |
| `BridgeNative` (`Runtime/Native/BridgeNative.cs:14`) | P/Invoke surface for `libvosk-bridge` (Android) | 96 | caller's thread (main) | none |
| `VoxrNative` (`Runtime/Native/VoxrNative.cs:15`, whole file wrapped in `#if UNITY_EDITOR_WIN`) | P/Invoke surface for `libvosk.dll` (Windows Editor) | 77 | main | none |
| `VoxrJsonParser` (`Runtime/VoxrJsonParser.cs:13`) | Hand-rolled zero-alloc UTF-8 JSON reader for VOSK result/error payloads | 195 | main | `VoxrWord`, `VoxrBridgeErrorCode` |
| `ModelExtractor` (`Runtime/ModelExtractor.cs:16`) | Unzips the model from StreamingAssets into `Application.persistentDataPath/VoxrModels` | 156 | async; unzip on `Task.Run` (`:53`) | none |
| `Downsampler` (`Runtime/Dsp/Downsampler.cs:11`) | 15-tap FIR + decimate-by-3, C# mirror of `downsampler.h` | 70 | main | none |
| `Agc` (`Runtime/Dsp/Agc.cs:11`) | AGC + tanh soft limiter, C# mirror of `agc.h` | 114 | main | none |
| `VoxrCommandRecogniser` (`Runtime/Commands/VoxrCommandRecogniser.cs:15`) | The whole speech→command policy layer: buffering, eager flush, thresholds, debounce, pending, grammar coordination, event dispatch | 1378 | main (`Update()` :534; `InjectText` asserts main at :301) | everything in `Runtime/Commands` + `VoxrSpeechRecogniser` |
| `VoxrCommandParser` (`Runtime/Commands/VoxrCommandParser.cs:137`) | Pure-C# token matcher/scorer/selector + grammar-JSON generator + construction-time grammar validation | **4918** | caller's thread (always main in practice) | slot/command definitions, `VoxrNumberParser`, `VoxrFollowUpVocabulary` |
| `UtteranceBuffer` (`Runtime/Commands/UtteranceBuffer.cs:12`) | Concatenates consecutive VOSK finals and their word arrays into one utterance | 90 | main | `VoxrWord` |
| `GrammarManager` (`Runtime/Commands/GrammarManager.cs:11`) | Holds current grammar JSON; stop→`SetGrammar`→start ceremony | 62 | main | `VoxrCommandParser.GenerateGrammarJson`, `VoxrSpeechRecogniser` |
| `CommandSetManager` (`Runtime/Commands/CommandSetManager.cs:12`) | Named sets → active `VoxrCommandDefinition[]`, plus intent→definition lookup | 110 | main | `VoxrCommandSet` |
| `DynamicSlotManager` (`Runtime/Commands/DynamicSlotManager.cs:12`) | Runtime `Func<string[]>` slot value providers; rebuilds effective slot defs | 96 | main (provider is invoked synchronously at `:54`) | `VoxrSlotDefinition` |
| `PendingCommandHandler` (`Runtime/Commands/PendingCommandHandler.cs:48`) | Pending-state machine: confirm/cancel, disambiguation choice, slot-fill advance, timeout | 460 | main | `VoxrCommandParser`, `VoxrFollowUpVocabulary` |
| `CommandDebouncer` (`Runtime/Commands/CommandDebouncer.cs:12`) | Per-intent cooldown map | 33 | main | none |
| `VoxrPushToTalkController` (`Runtime/VoxrPushToTalkController.cs:14`) | PTT / continuous / mode-switch gating; `OnApplicationPause` handling | 218 | main | `VoxrSpeechRecogniser`, `VoxrCommandRecogniser` |
| `VoxrBatchTestRunner` (`Runtime/Testing/VoxrBatchTestRunner.cs`) | Feeds text cases through a parser instance and applies thresholds offline | 324 | main | `VoxrCommandParser`, `VoxrSpeechRecogniser.CreateSimulatedWords` (:105) |
| `VoxrWavReader` (`Runtime/Testing/VoxrWavReader.cs`) | RIFF/PCM reader, 48 kHz mono 16-bit only | 132 | main | none |

Data types (public, all `readonly struct`): `VoxrWord`/`VoxrResult` (`Runtime/VoxrResult.cs:9`,`:30`), `VoxrSlotMatch`/`VoxrCommand`/`VoxrCommandResult` (`Runtime/Commands/VoxrCommand.cs:11`,`:33`,`:137`), `VoxrCommandDefinition` (`VoxrCommandDefinition.cs:11`), `VoxrSlotDefinition` (`VoxrSlotDefinition.cs:12`), `VoxrPendingAmbiguity` (`VoxrPendingAmbiguity.cs:26`).

### 1.2 Native bridge (C++)

Files: `NativeBridge~/src/vosk_bridge.cpp` (412), `vosk_bridge.h` (68), `ring_buffer.h` (105), `result_queue.h` (48), `downsampler.h` (75), `agc.h` (110), `audio_capture.h` (22, include-forwarder), `audio_capture_audiorecord.cpp/.h` (212/38), `audio_capture_aaudio.cpp/.h` (101/40), `audio_capture_stub.cpp/.h` (18/35), `logging.h` (24). Build: `NativeBridge~/CMakeLists.txt` (89), backend chosen by the `VOSK_BRIDGE_CAPTURE` cache variable (`CMakeLists.txt:12`).

Threads inside the bridge:
- **Capture thread** — `AudioCapture::ReadLoop` (`audio_capture_audiorecord.cpp:157`), JNI-attached, blocking `AudioRecord.read` of 960 frames (`:13`, `:180`), writes into `g_ring_buffer`.
- **Recognition thread** — `recognition_loop` (`vosk_bridge.cpp:70`), started at `:193`, reads 4096 samples/iteration, runs DSP + VOSK, pushes JSON onto `g_result_queue`.
- **Unity main thread** — calls every `extern "C"` entry point; drains via `vosk_bridge_get_result`.

All bridge state is **file-scope static** (`vosk_bridge.cpp:20-42`): `g_model`, `g_recognizer`, `g_sample_rate`, `g_ring_buffer`, `g_result_queue`, `g_audio_capture`, `g_downsampler`, `g_agc`, `g_recognition_thread`, `g_running`, `g_initialised`, `g_push_mode`, `g_input_level`, `g_last_error`, `g_last_partial`, `g_current_result`.

#### C ABI

| Function | Purpose | State it touches |
|---|---|---|
| `vosk_bridge_init` (`vosk_bridge.cpp:203`) | Load model, create plain (non-grammar) recognizer, `set_words(1)`, configure AGC | `g_model`, `g_recognizer`, `g_sample_rate`, `g_agc`, `g_initialised`, `g_last_error`; calls `reset_pipeline()` |
| `vosk_bridge_destroy` (`:247`) | Stop, join recognition thread, free recognizer + model | all of the above + `g_running`, `g_push_mode`, `g_input_level` |
| `vosk_bridge_start` (`:275`) | `start_internal(true)` — capture + recognition thread | `g_ring_buffer`, `g_result_queue`, `g_downsampler`, `g_agc`, `g_audio_capture`, `g_running`, `g_push_mode`, `g_input_level` |
| `vosk_bridge_start_push` (`:279`) | `start_internal(false)` — recognition thread, no capture | same minus `g_audio_capture` |
| `vosk_bridge_stop` (`:283`) | Stop capture, join thread (thread flushes `final_result` at `:158`) | `g_running`, `g_audio_capture`, `g_push_mode`, `g_input_level` |
| `vosk_bridge_reset` (`:296`) | Stop if running, `vosk_recognizer_reset`, restart in the same mode | `g_recognizer` decoder state + restart path |
| `vosk_bridge_push_audio` (`:320`) | Write 48 kHz float samples; clamps to free space, returns count written, **negative** error on misuse | `g_ring_buffer` |
| `vosk_bridge_get_input_level` (`:342`) | Rolling pre-DSP RMS (~300 ms EMA, updated at `:102-113`) | `g_input_level` |
| `vosk_bridge_set_grammar` (`:346`) | **Frees and recreates the recognizer** with `vosk_recognizer_new_grm` (or plain), re-applies `set_words(1)`; refuses while running | `g_recognizer` |
| `vosk_bridge_has_result` (`:379`) | Queue non-empty | `g_result_queue` |
| `vosk_bridge_get_result` (`:383`) | Pop one result; returns pointer into `g_current_result.json` valid until the next call | `g_result_queue`, `g_current_result` |
| `vosk_bridge_is_running` (`:393`) | Process-wide running flag | `g_running` |
| `vosk_bridge_is_initialised` (`:397`) | Process-wide init flag | `g_initialised` |
| `vosk_bridge_get_error` (`:401`) | Copy last error string | `g_last_error` |

**No handle appears anywhere in this ABI** — this is the mechanical cause of the one-recogniser-per-process rule (see §8).

---

## 2. Data flow with timing

Path from a capture chunk to `OnCommandRecognised`, Android (device) path. Editor differences are noted inline.

1. **Mic → ring buffer.** `AudioRecord.read(float[], 0, 960, READ_BLOCKING)` — `audio_capture_audiorecord.cpp:180`, chunk constant `kReadFrames = 960` at `:13`, rate `kSampleRate = 48000` at `:10`. **Floor: ~20 ms** per chunk (960/48000). Written into the ring at `:201`.
   *Editor:* `Microphone.Start(null, true, 5, 48000)` — `EditorMicBackend.cs:168`; the buffer is drained once per `Update()` (`EditorMicBackend.cs:342-366`), so the floor is one frame (~11–16 ms at 60–90 fps), not 20 ms.
2. **Ring buffer.** `RingBuffer<float, 65536>` — `ring_buffer.h:15`, capacity 65536 samples = **1.365 s at 48 kHz**. Lock-free SPSC; producer overwrites the oldest on lap and sets the overflow flag (`:41`), consumer snaps forward (`:56`).
3. **Recognition-thread read.** `g_ring_buffer.Read(read_buf, kReadChunkSize)` — `vosk_bridge.cpp:91`; `kReadChunkSize = 4096` at `:45`, commented "~85 ms". **Floor: up to 85 ms of audio is accumulated per decode iteration** (4096/48000 = 85.3 ms).
4. **Empty-queue sleep.** When the ring is empty the thread sleeps **10 ms** — `vosk_bridge.cpp:94`. This is the polling granularity at which new audio is picked up, so it adds **0–10 ms** on top of hop 3.
5. **DSP.** RMS accumulation (`:102-113`), `Downsampler::Process` 48→16 kHz (`vosk_bridge.cpp:116`, `downsampler.h:25`, 15 taps → ~7 samples of group delay at 48 kHz ≈ 0.15 ms), `Agc::Process` in place (`vosk_bridge.cpp:119`, `agc.h:41`), `float_to_int16` (`vosk_bridge.cpp:122`, defined `:51`).
6. **Decode.** `vosk_recognizer_accept_waveform_s(g_recognizer, int16_buf, ds_count)` — `vosk_bridge.cpp:125`. Returns 1 at an endpoint. Cost is VOSK's own RTF, not readable from constants.
7. **Result enqueue.** Final: `vosk_recognizer_result` → `g_result_queue.Push(json, true)` (`vosk_bridge.cpp:130-132`). Partial: `vosk_recognizer_partial_result` → pushed only when the string changed (`:136-140`). Queue is a mutex-guarded `std::deque` — `result_queue.h:19-31`; the header comment at `:15-16` states push frequency ~4/sec and pop once per Unity frame.
8. **Main-thread drain.** `VoxrSpeechRecogniser.Update()` — `Runtime/VoxrSpeechRecogniser.cs:508`; drain loop at `:526-536`. **Floor: one frame** (11.1 ms at 90 fps, 13.9 ms at 72 fps). The whole queue is drained each frame, so this is latency, not throughput loss.
9. **JSON parse.** `DispatchJsonResult` — `Runtime/VoxrSpeechRecogniser.cs:563`; error probe at `:571`, text at `:578`, word array at `:589` (`VoxrJsonParser.ParseWordsFromJson`, `Runtime/VoxrJsonParser.cs:52`). Words are parsed only when `OnResult != null` or in the Editor (`:584-587`).
10. **Event to the command layer.** `DispatchFinalResult` → `OnResult` (`Runtime/VoxrSpeechRecogniser.cs:497`) → `VoxrCommandRecogniser.HandleResult` (subscribed at `Runtime/Commands/VoxrCommandRecogniser.cs:501`).
11. **Utterance buffer.** `HandleResult` — `VoxrCommandRecogniser.cs:566`. If `bufferWindow <= 0` it parses immediately (`:574-578`). Otherwise `_buffer.Append(...)` (`:581`) resets the timer (`UtteranceBuffer.cs:34`). **Floor: `bufferWindow`, default 0.5 s** (`VoxrCommandRecogniser.cs:72`); the tooltip at `:68-70` recommends **2.0 s on Quest 3**, which is the dominant latency term in the whole pipeline.
12. **Eager-flush probe (opt-in, default off).** `eagerFlushOnCompleteMatch` — `VoxrCommandRecogniser.cs:79`. When on and no pending is live, `ProbeEagerCommit()` (`:628`) runs a full speculative parse over the peeked buffer and `TryEagerCommit` (`VoxrCommandParser.cs:4274`) returns `Commit` / `HoldExtendable` / `None`. `Commit` flushes in the same frame (`:596`) — **removes the entire `bufferWindow` term**. `HoldExtendable` arms `prefixHoldSeconds` (`:89`, default 0), which can only *shorten* the wait (`EffectiveBufferWindow`, `:605-608`).
13. **Timed flush.** `Update()` → `_buffer.ShouldFlush(Time.time, EffectiveBufferWindow)` (`VoxrCommandRecogniser.cs:536`; predicate `UtteranceBuffer.cs:38-41`). **Floor: one frame of polling granularity on top of `bufferWindow`.**
14. **Parse.** `FlushBuffer` (`:610`) → `ProcessParsedResultsCore` (`:650`) → `_parser.ParseInternal(tokens, text, wordConfidence)` (`:719`). The selection loop is `VoxrCommandParser.cs:2475-2830`; it is O(commands × patterns × tokens) per extraction round, and `IsAdmissibleStart` (`:4019`) adds one further full pattern sweep per *un-startable* token, once per utterance via `BuildCoverageTables` (`:3856`).
15. **Policy gates.** Confirm/cancel (`VoxrCommandRecogniser.cs:667`), follow-up slot fill (`:715`), completeness + score (`:931`), confidence (`:980`), debounce (`:991`, `commandCooldown` default **0.3 s**, `:94`), sibling-tie disambiguation (`:1007`), `requiresConfirmation` (`:1057`).
16. **`OnCommandRecognised`.** Fired per accepted command at `VoxrCommandRecogniser.cs:1100`; batch `OnCommandsRecognised` at `:1107`; pending resolutions fire through `InterpretResolution` (`:1245-1271`).

**Additive latency floor, device, default settings:** ~20 ms (capture chunk) + up to 85 ms (decode read chunk) + 0–10 ms (thread sleep) + VOSK decode + ≤1 frame (main-thread drain) + **500 ms** (buffer window) + ≤1 frame (flush poll). The buffer window dominates by an order of magnitude; at the recommended Quest setting of 2.0 s it dominates by two.

**Additional latency sources on state change:** `GrammarManager.ForceApply` (`GrammarManager.cs:38`) does `StopRecognition()` → `SetGrammar()` → `StartRecognition()`. `vosk_bridge_stop` joins the recognition thread (`vosk_bridge.cpp:287`) and `vosk_bridge_set_grammar` frees and recreates the recognizer (`:356-365`) — this is the documented "audio gap".

---

## 3. What the decoder gives and what the package consumes

### 3.1 VOSK calls actually made

Android bridge (`NativeBridge~/src/vosk_bridge.cpp`):
- `vosk_set_log_level(-1)` — `:215`
- `vosk_model_new` — `:217`; `vosk_model_free` — `:229`, `:265`
- `vosk_recognizer_new` — `:225`, `:365`
- `vosk_recognizer_new_grm` — `:363`
- `vosk_recognizer_set_words(rec, 1)` — `:235`, `:373`
- `vosk_recognizer_accept_waveform_s` — `:125`
- `vosk_recognizer_result` — `:130`
- `vosk_recognizer_partial_result` — `:136`
- `vosk_recognizer_final_result` — `:158`
- `vosk_recognizer_reset` — `:307`
- `vosk_recognizer_free` — `:260`, `:357`

Windows Editor (`Runtime/Native/VoxrNative.cs`): the *same eleven*, and only those — declarations at `:22` (`vosk_model_new`), `:26`, `:31`, `:34` (`new_grm`), `:40`, `:43` (`set_words`), `:50` (`accept_waveform_s`), `:60`, `:63`, `:66`, `:69` (`reset`), `:74` (`set_log_level`). Call sites: `EditorMicBackend.cs:75, 80, 88, 100, 105, 110, 198, 217, 235, 271, 276, 279, 288, 300, 305, 384, 393, 400, 480, 509`.

### 3.2 JSON fields parsed

`Runtime/VoxrJsonParser.cs` — the only consumer of decoder output:
- `"result"` array — key `:18`, located `:54`
- `"text"` — key `:19`, read by `ParseTextFromJson` `:148`
- `"partial"` — key `:20`, same call site `:150`
- `"conf"` — key `:21`, read `:85`
- `"start"` — key `:22`, read `:86`
- `"end"` — key `:23`, read `:87`
- `"word"` — key `:24`, read `:88`
- `"code"` — key `:25`, read by `ParseErrorCode` `:30-47` (bridge-injected error JSON, not VOSK's)
- `"error"` — key `:26`, probed at `Runtime/VoxrSpeechRecogniser.cs:571`

Nothing else in any VOSK payload is read. `VoxrWord` (`Runtime/VoxrResult.cs:9`) carries exactly `Text`, `Confidence`, `StartTime`, `EndTime` — and of those, **only `Text` and `Confidence` reach the parser**: `InstanceBuildWordConfidence` builds a `word → conf` dictionary (`VoxrCommandParser.cs:3802-3838`) and timing is never consulted by any scoring rule. `ComputeConfidence` (`:4090`) takes the **minimum** confidence over matched non-`[unk]` tokens.

### 3.3 Declared-but-unused VOSK capability

`NativeBridge~/include/vosk_api.h` declares, and nothing in this repo calls:

| Declared | Header line | Used? |
|---|---|---|
| `vosk_model_find_word` | `:21` | no |
| `vosk_recognizer_new_spk` / `VoskSpkModel` | `:26`, `:14` | no |
| `vosk_recognizer_set_max_alternatives` | `:29` | **no** — only the 1-best hypothesis is ever read |
| `vosk_recognizer_set_partial_words` | `:31` | no — partials arrive as bare text with no per-word data |
| `vosk_recognizer_set_nlsml` | `:32` | no |
| `vosk_recognizer_set_endpointer_mode` | `:33` | **no** — VOSK's default endpointer is what splits mid-command pauses |
| `vosk_recognizer_set_endpointer_delays(t_start_max, t_end, t_max)` | `:34-35` | **no** — the utterance buffer exists to compensate for this in C# instead |
| `vosk_recognizer_accept_waveform` (char) | `:37` | no |
| `vosk_recognizer_accept_waveform_f` (float) | `:39` | no — deliberately, see `vosk_bridge.cpp:49-50` and `Runtime/Native/VoxrNative.cs:45-48` (broken on prebuilt arm64) |
| `vosk_gpu_init`, `vosk_gpu_thread_init` | `:51-52` | no (CPU-only by design) |

Also unavailable at this API version: no lattice, no acoustic/LM score split, no phone-level output, no per-phrase weighting (`vosk_recognizer_new_grm` takes a flat JSON string array only — `:27`). The vendored newer header `NativeBridge~/vendor/vosk-linux-x86_64-0.3.45/vosk_api.h` (361 lines) is gitignored (`.gitignore:41`) and is not what the shipped Android `libvosk.so` exports.

The grammar handed to `new_grm` is a **flat, sorted, deduplicated list of strings** — `VoxrCommandParser.GenerateGrammarJson` (`:3076-3175`): contiguous required-literal runs as one multi-word entry plus each word individually (`AddPhrase`, `:3182-3192`), every slot value and alias key as a surface form plus its words (`:3124-3134`), the whole digit vocabulary if any `NumberSequence` slot exists (`:3137-3145`), caller-supplied follow-up words (`:3148-3155`), and `[unk]` (`:3157`). The comment at `:3177-3181` states the reason single words are kept alongside phrases: a VAD-split utterance must still decode as fragments — i.e. word order is a **bias, not a constraint**.

---

## 4. Coupling and seams

### 4.1 Interfaces that exist

There is **no C# interface or abstract class anywhere in `Runtime/`**. Every seam is one of: a compile-time `#if`, a `virtual` method on a concrete MonoBehaviour, a delegate parameter, or a CMake source swap.

- **Audio backend, native side — a real seam.** `audio_capture.h:12-20` forwards to one of three headers chosen by `CMakeLists.txt:12`; all three expose the identical shape (`Start(RingBuffer<float>*)`, `Stop()`, `IsRunning()`, `HasError()` — `audio_capture_audiorecord.h:11-33`, `audio_capture_aaudio.h:11-38`, `audio_capture_stub.h:18-33`). This is source-level polymorphism, not runtime.
- **Push-audio seam — a real seam, unused from C#.** `vosk_bridge_start_push` (`vosk_bridge.cpp:279`) + `vosk_bridge_push_audio` (`:320`) let a caller supply audio without a capture device. The C# bindings exist (`Runtime/Native/BridgeNative.cs:53`, `:60`) with the comment at `:49-50` "No runtime caller yet"; verified — no call site in `Runtime/`, `Editor/`, `Tests~/`, or `Samples~/`.
- **Editor playback seam — a real seam.** `EditorMicBackend.StartPlayback` / `TickPlayback` / `StopPlayback` (`EditorMicBackend.cs:421`, `:489`, `:520`) push a `float[]` through the *same* `ProcessChunk` the mic uses (`:373`), at a fixed 4800-sample chunk (`:413`) for determinism. Exposed to tests via `VoxrSpeechRecogniser.EditorBackend` / `EditorDispatcher` (`Runtime/VoxrSpeechRecogniser.cs:555-556`).
- **Text-injection seam — a real seam, and the widest one.** `VoxrSpeechRecogniser.InjectResult` (`:458`) and `VoxrCommandRecogniser.InjectText` (`:299`) enter the pipeline below the decoder, so the whole command layer is testable and replaceable-from-above without any audio.
- **Test doubles via `internal virtual`.** `VoxrSpeechRecogniser.IsRecognisingCore` (`:127`), `StartRecognitionCore` (`:286`), `StopRecognitionCore` (`:372`) — the comment at `:120-126` states this is deliberately `internal` "so the public surface [stays] free of an extension point". Reachable only through the `InternalsVisibleTo` grants in `Runtime/AssemblyInfo.cs:9-11`.
- **Dynamic slot values.** `RegisterSlotValueProvider(string, Func<string[]>)` (`VoxrCommandRecogniser.cs:330`) — the only runtime extension point that takes a delegate from game code.

### 4.2 Interfaces that are missing

- **No decoder interface.** The choice of decoder is a `#if UNITY_EDITOR_WIN` fork inside `VoxrSpeechRecogniser`, repeated at **eight** independent sites: `IsInitialised` (`:100`), `IsRecognisingCore` (`:135`), `InitialiseAsync` (`:196`), `ReleaseNativeResources` (`:255`), `StartRecognitionInternal` (`:351`), `StopRecognitionCore` (`:381`), `ResetRecogniser` (`:420`), `SetGrammar` (`:441`), plus the `Update()` fork at `:510`. Swapping in a different decoder means editing all nine.
- **No audio-backend interface on the C# side.** `EditorMicBackend` is `internal sealed` (`EditorMicBackend.cs:20`) with no base type.
- **No matcher interface.** `VoxrCommandRecogniser` holds a concrete `VoxrCommandParser _parser` (`:159`) and constructs it directly at three sites (`:247`, `:354`, and via `RebuildParserAndGrammar` `:435`).
- **No grammar-generator interface.** `GrammarManager.Rebuild` calls the static `VoxrCommandParser.GenerateGrammarJson` (`GrammarManager.cs:22`).

### 4.3 Is the parser a pure function of (tokens, words) → results?

**Almost, but no.**

Pure parts: `TryMatchScored` (`VoxrCommandParser.cs:3467`) reads only tokens, the pattern, `searchStart`, and the per-utterance coverage tables. `CompareCandidate` (`:3344`) is `static`. `ComputeConfidence` (`:4090`) is `static`. The parser takes **no Unity types** in any scoring path — the only `UnityEngine` references in the file are `Debug.LogWarning` calls in the construction-time validation passes (`:740, 749, 758, 773, 782, 791, 945, 1047, 1130, 1742, 1851`), all of which sit behind `[Conditional("UNITY_EDITOR")]` except `RunValidationWarnings` (`:732`, called unconditionally from the constructor at `:701`).

Impurities:
- **Hidden mutable per-utterance state.** `BuildCoverageTables` (`:3856`) writes `_recognisedPrefix`, `_orphanRun`, `_forcedOrphanRun`, `_coverageTokens` (`:3864-3875`) and every candidate score reads them (`:3647-3651`). The coupling is guarded only by an Editor-only assertion (`AssertCoverageTablesMatch`, `:3929-3939`).
- **Pooled output buffers.** `ResultBuffer` (`:2989`), `TiedSiblingBuffer` (`:2994`), `_matchSlotBuf`/`_bestSlotBuf` (`:290-291`), `_wordConfidencePool` (`:300`), `_tiedSiblingRivalBuf`/`_siblingRivalSlotBuf` (`:389-390`). `ParseInternal` returns a count and the caller reads the parser's arrays — `VoxrCommandRecogniser.cs:752` snapshots them precisely because a subscriber can invalidate them mid-loop (`:745-757`). The comment at `:753-757` states re-entrancy is "pre-existing and unsupported".
- **Editor-only behaviour divergence.** `_recordSiblingTies` (`:384`) is `disambiguateSiblingTies` in a player but **always true in the Editor** — `VoxrCommandRecogniser.cs:1301-1302` calls this out explicitly: "the parser records ties whenever the flag is set OR UNITY_EDITOR is defined, and every Unity test runs in the Editor". Same shape at `VoxrCommandParser.cs:378-381`.
- **Lazy caches.** `_canCommitEarly` (`:314`, built at `:4250`) and `_siblingMemberships` (`:330`, built at `:2070`) are built on first use — and in the Editor at construction (`:698-708`), so first-parse cost differs between Editor and player.

### 4.4 What leaks across layers

- **VOSK's `[unk]` reaches deep into the parser as a magic string.** `internal const string UnkToken = "[unk]"` — `VoxrCommandParser.cs:139`. Read at `:2547` (start-index skip), `:3514` (pre-element skip), `:3675` (slot match skip), `:3728` (number-sequence skip), `:3879` (`_recognisedPrefix`), `:3914` (`_orphanRun` transparency), `:4101` (confidence skip), `:4504` (eager first-token skip), and outside the parser at `PendingCommandHandler.cs:202` and `VoxrCommandParser.GenerateGrammarJson:3157` (injected into the grammar itself). A different decoder with a different OOV token, or none, would need every one of these sites.
- **VOSK's JSON shape reaches `VoxrSpeechRecogniser`.** The error probe at `Runtime/VoxrSpeechRecogniser.cs:571` matches the raw byte string `"error"` anywhere in the payload; the TODO at `:568-570` admits this false-positives on a recognised word "error".
- **Native memory lifetime leaks into C# call sequencing.** `BridgeNative.SpanFromPtr` (`:72`) and `SpanFromNullTerminated` (`:77`) wrap decoder-owned memory; the contract at `:66-71` — "MUST NOT be stored, captured by a closure, or held across an await/coroutine yield" — is enforced by comment only, at `Runtime/VoxrSpeechRecogniser.cs:532-534`, `:404-405`, and `EditorMicBackend.cs:387-389`.
- **`float sampleRate` is a serialized Inspector field on the recogniser** (`Runtime/VoxrSpeechRecogniser.cs:26`, default 16000) and is passed straight to `vosk_bridge_init` (`:222`) — while the capture rate is hard-coded 48000 in two unrelated places (`audio_capture_audiorecord.cpp:10`, `EditorMicBackend.cs:23`) and the decimation factor is a compile-time 3 (`downsampler.h:14`, `Runtime/Dsp/Downsampler.cs:13`). The three are coupled with nothing enforcing the relation.
- **Process-global static in C#.** `static VoxrSpeechRecogniser s_bridgeOwner` — `Runtime/VoxrSpeechRecogniser.cs:53`, with the full rationale at `:44-52`. Ownership is checked at `:98`, `:133`, `:166`, `:193`, `:205`, `:248`, `:266`, `:280`, `:379`, `:418`, `:439`, `:628`.
- **Editor-only static event.** `internal static event Action<VoxrCommandRecogniser, VoxrMatchDiagnostics> DiagnosticsPublished` — `VoxrCommandRecogniser.cs:142`.
- **`#if UNITY_EDITOR` forks *inside* the runtime hot path.** `VoxrCommandRecogniser.ProcessParsedResultsCore` interleaves 12 Editor-only diagnostic blocks with the decision logic (`:659-662`, `:676-707`, `:721-725`, `:826-837`, `:856-866`, `:868-880`, `:891-897`, `:906-908`, `:955-959`, `:964-975`, `:983-986`, `:995-998`, `:1040-1052`, `:1068-1071`, `:1077-1079`, `:1082-1085`). `needFullParse = true` under `UNITY_EDITOR` (`Runtime/VoxrSpeechRecogniser.cs:585-587`) means the Editor always parses the word array and a player may not — an Editor/device behavioural divergence in what `OnResult` carries.
- **Duplicated DSP.** `Runtime/Dsp/Downsampler.cs` is a line-for-line C# copy of `NativeBridge~/src/downsampler.h` (identical 15 coefficients: `Downsampler.cs:22-27` vs `downsampler.h:64-68`). `Runtime/Dsp/Agc.cs` mirrors `agc.h` — same constants (`Agc.cs:13,17,18,23,24,26,27,28,29` vs `agc.h:17,25,26,31,32,77,78,79,80`) — but **they are not identical**: the C++ gates on the smoothed level (`agc.h:53`, `smoothed_level_ < kNoiseFloor`) while the C# gates on the instantaneous sample (`Agc.cs:66`, `absX < NoiseFloor`). This is a deliberate Editor/device divergence in the audio actually fed to the decoder.

---

## 5. Hard-won parser rules inventory

Every rule below is implemented in `Runtime/Commands/VoxrCommandParser.cs` or `Runtime/Commands/VoxrCommandRecogniser.cs`. A redesign must preserve or consciously subsume each. Issue numbers are as cited in the code comments. `Documentation~/scoring.md` (570 lines) is the normative prose statement of the same rules; where it adds a measurement or a stated guarantee the code does not, it is cited as `scoring.md:N`.

Two things that make this list a hard constraint rather than a preference:
- **`scoring.md` publishes specific 699-utterance corpus measurements as justification** for individual rules — `scoring.md:178` (28 blocked / 27 recovered / 1 not), `:238` (refuse-to-compete destroys 11 clean-1.00 commands, refuse-to-fire destroys 0), `:242` (the bar costs 9 genuine rows, suppresses 39 invented), `:454` (17 commands stopped clearing `minScore` under coverage, 0 started firing wrongly), `:511` (the start test accounts for 11 intent changes + 1 count change). Any change to selection, the bar, the orphan start test, or coverage invalidates named published numbers.
- **Two closed enumerations are published as exhaustive**: the `OnUnrecognisedSpeech` fires/silent table (`scoring.md:309-321` — "those three are the only filters that suppress it") and the seven-condition `TryEagerCommit` conjunction plus two `Commit`-only extras (`scoring.md:338-361` — "None of the seven is implied by its neighbours").

### 5.1 Scoring (per candidate, `TryMatchScored` `:3467-3669`)

| # | Rule | Site | Failure it prevents |
|---|---|---|---|
| S1 | Score = `rawScore / (denominator + coverage)`, clamped to [0,1] by construction; `0` when the denominator is `0` | `:3654`; `scoring.md:29-31` | — (the model) |
| S2 | Matched required literal: `+1.0` numerator, `+1.0` denominator | `:3590`, `:3594`; `scoring.md:35` | — |
| S3 | Matched slot (required or optional): `+1.0` / `+1.0` | `:3546-3547`; `scoring.md:37,41` | — |
| S4 | Matched **optional literal**: `+0.5` / `+0.5` (`OptionalLiteralScore`, `:142`) | `:3579-3580`; `scoring.md:39` | Doc notes this changes the arithmetic of every *imperfect* match of that pattern (`scoring.md:48`) |
| S5 | **Dynamic denominator**: an *omitted* optional contributes to neither side | `:3477-3481`, `:3586`; `scoring.md:40,42,46` | "taking advantage of optionality is never penalized and a perfect match always normalizes to 1.0" |
| S6 | Missed required **literal** costs **zero** beyond its denominator credit (`RequiredLiteralMissPenalty = 0f`) | `:187`, `:3607`; `scoring.md:36,47` | Issue #65 §5.1. The old −0.5 charged a dropped word twice (1.5/N), so cost scaled inversely with pattern length: "time to target" heard as "time target" scored 0.50 against a 0.60 gate and did not fire, while a 7-element pattern shrugged the same drop off at 0.79 (`:153-170`) |
| S7 | Missed required **slot** costs `−1.0` (`RequiredSlotMissPenalty`) — charged **twice**, deliberately | `:143`, `:3554`; `scoring.md:38,47` | "an absent argument is not a dropped function word" |
| S8 | **Coverage charge**: `(SkippedBefore + OrphanedAfter) × coverageWeight` added to the denominator, computed *before* selection | `:3642-3651`; `scoring.md:88,100-104` | Issue #31 + #65 §5.2. Before it, "a short pattern found anywhere inside a longer sentence scores a full 1.0 and fires, discarding the rest" (`VoxrCommandRecogniser.cs:34-46`). Applying it after selection could only filter, never reorder (`:3619-3624`) — "nothing normalised to 1.0 can be beaten at any weight" |
| S9 | `[unk]` is never charged and is **transparent** rather than a run terminator | `:3881-3885`, `:3914-3919`; `scoring.md:143` | "A stopper would let one noise token shield every real orphan behind it" |
| S9b | The exemption is **literal-`[unk]` only** — `freeSpeechMode`, `InjectText` and the Batch Test Runner deliver real text and *are* charged, so "cease fire please" scores 0.67 offline vs 1.00 live | `:3914`; `scoring.md:145` | A batch score is a **lower bound** on the runtime score |
| S10 | An orphan run **stops** at any token where `IsAdmissibleStart` is true | `:3921`; `scoring.md:108` | Sequential extraction: "cease fire launch missiles target hotel one" must charge `cease_fire` nothing (`:246-250`) |
| S11 | `IsAdmissibleStart` = `CanStartPattern` OR some pattern started there matches **strictly more** required elements than it misses (`missed < matched`) | `:4019-4050`; `scoring.md:116-120` | Issue #82: `CanStartPattern` alone asks only about a pattern's *first* element, so a pattern whose lead VOSK dropped was invisible and `cease_fire` paid 0.4 for the next command's tokens (`:3966-3977`). The strictly-stronger threshold vs. admission is the #42 fix (`:3989-3997`) |
| S11b | The start test reads **registered patterns only**, never which candidates survived a real round, and is deliberately conservative — where unsure it answers yes and charges nothing | `:4010-4013`, `:4052-4058`; `scoring.md:122` | "Over-charging destroys sequential extraction; under-charging merely leaves a score where it already was" |
| S11c | Documented consequences of S11b: follow-up vocabulary counts as a legal start (so "disengage, yes" is uncharged); a pattern beginning with an open-ended `NumberSequence` makes almost every token a start, so nearly nothing is charged **grammar-wide**; **leading optionals join the start set**, so `["?please","fire"]` puts *both* words there and a stray "please" terminates the orphan run for **every** candidate | `_startLiterals`/`_startSlots` built `:578-614`; `scoring.md:126-128` | Authoring rule: put filler at the **end** of a pattern |
| S12 | **Amendment A3** — a candidate whose own next required element failed at position *i* is charged for *i* outright (`_forcedOrphanRun`), it may not claim another pattern could have started there; counting resumes normally from the next token | `:3922`, `:3650`, `:3892-3909`; `scoring.md:130-139` | Otherwise "the rule rewards matching LESS": on "switch to weapons target hotel" the *navigation* pattern won 0.67 vs weapons' 0.60 by missing its final element; with the charge, 0.33 vs 0.60. "Measured: safe at one leftover token, flips at two" |
| S12b | The **leading** term restarts per extraction round; the **trailing** term has no notion of a round and is a property of (utterance, grammar) | `:3945-3948` (searchStart subtraction), `:3961-3964`; `scoring.md:149,151` | "chained commands do not penalise each other"; errs toward "a score left higher than ideal, never a command charged for words that were someone else's" |
| S13 | At `coverageWeight <= 0` the expensive `IsAdmissibleStart` sweep is skipped and results stay bit-identical; negative/NaN/infinity weights are treated as `0` | `:4024-4033`; `scoring.md:157-160` | Cost, not correctness — but weight `0` also **switches off the #42 protection** |
| S14 | Confidence = **minimum** per-word confidence over matched non-`[unk]` tokens; `-1` when no word data, and the `minConfidence` check is then **bypassed entirely** | `:4090-4113`; `scoring.md:252-256,276` | "One weak word vetoes an otherwise-perfect command." `-1.00` means "no data", not "zero confidence" (`KNOWN_LIMITATIONS.md:837`) — "never [display it] as a low value" |
| S15 | The per-word confidence table is built **once per utterance, keyed by word text, first occurrence wins** — so every repeat scores at the first occurrence's confidence **even when that first occurrence lies outside the matched span** | `:3821-3831` (`!ContainsKey` guard at `:3828`), also `:3802-3812`; `scoring.md:269-274` | Two consequences the doc states: a weak repeat inside the span can be masked by a strong earlier one, and a weak word *before* the match can drag the reported confidence down. Bites hardest on `NumberSequence` slots |

### 5.2 Admission and selection (`CompareCandidate` `:3344-3413`)

| # | Rule | Site | Failure it prevents |
|---|---|---|---|
| A1 | `Score <= 0` → not a candidate ("discarded and never competes") | `:3353-3354`; `scoring.md:50,56` | Coverage only ever *adds* to the denominator, so both floors behave identically with coverage on or off (`scoring.md:50`) |
| A2 | **Admission rule (DR-7)**: `MissedRequired > MatchedRequired` → not a candidate. A count, not a threshold; no knob; unrelated to `minScore` | `:3385-3386`; `scoring.md:57-59` | Issue #65. Zeroing S6 removed the accidental enforcement the old −0.5 gave: "'alpha one weapons mode' fired `mode_weapons` at a full 1.00 where the skipped-word charge had correctly held it to 0.50, and a genuine command later in a multi-command utterance could be evicted entirely" (`:3356-3380`) |
| A2b | Visible effect: a very sparse partial match produces **no result at all**, not a low-scoring one | `scoring.md:61` | Debug rule: check matched-vs-missed before assuming a score problem |
| A3 | Optional elements count toward **neither** `MatchedRequired` nor `MissedRequired` — a second ledger, deliberately different from the denominator | `:3287-3299`; `scoring.md:57` | "an optional the author marked skippable says nothing about whether the speaker said the command the REQUIRED elements identify" |
| A4 | Selection key order: **earliest start → highest score → longest consumed span → highest literal count → registration order** | `:3396-3412`; `scoring.md:168-172` | Registration order is "a deterministic fallback, not a design surface; do not build behaviour on it" (`scoring.md:172`) |
| A5 | Span term compares `ConsumedEndIdx`, not `EndIdx`, and sits **above** literal count | `:3339-3343`, `:3400`; `scoring.md:170,176` | Issue #41: `intercept track {track} {burn_level}` vs `intercept track {track}` both score 1.0; without span the winner was whichever the asset listed first, and when the bare one won, sequential extraction fired the orphaned tail as a *second command* — "splitting one order in two with no warning". `ConsumedEndIdx` specifically stops a pattern winning by absorbing trailing `[unk]` (`:3502-3507`) |
| A5b | Since #65 that pair is usually settled on **key 2**, not key 3 (0.60 vs 1.00); key 3 still decides at `coverageWeight = 0` and where trailing tokens are charged to neither | `scoring.md:174-176` | — |
| A6 | Exhausted keys return `Tied` (three-state), not "not better" | `:3409-3412`, enum `:3314` | Issue #74 DR-3: the old bool "conflated a clear loss with an exact tie … the incumbent silently kept the win on registration order and the tie was never recorded anywhere" |
| A7 | **Key 1 bounds what coverage can do**: coverage can only reorder candidates starting at the *same* token, so a better-scoring later candidate is never promoted over a demoted earlier one | `:3396-3397`; `scoring.md:178` | Sequential extraction usually recovers it; **measured over 699 utterances: 28 blocked, 27 recovered by a later round, 1 not**. Known limitation |
| A8 | The eager path uses the **same** ordering, so an eager verdict never names a command the flush would not fire | `CompareCandidate` shared, `:4335`; `scoring.md:180`, code `:3321-3330` | It may name nothing; a refusal defers to the flush |

### 5.3 Extraction round (`ParseInternal` `:2455-2838`)

| # | Rule | Site | Failure it prevents |
|---|---|---|---|
| E1 | Sliding start: every pattern of every active command is tried at every non-`[unk]` index from `searchStart` | `:2545-2548`; `scoring.md:17` | Preamble/filler tolerance |
| E2 | Sequential extraction: on a win, `searchStart = bestEndIdx` and the loop repeats until nothing scores | `:2829`, `:2475`; `scoring.md:186` | Multiple commands per utterance |
| E3 | Round terminates on `bestScore <= 0` or no admitted candidate | `:2710-2711`; `scoring.md:196` | — |
| E4 | Zero-progress guard: `bestEndIdx <= searchStart` breaks | `:2713-2715`; `scoring.md:196` | Infinite loop |
| E5 | Result-buffer bound (one slot per active command) is checked **before** the scan, not after | `:2477-2487`, buffer sized `:2486`; `scoring.md:196` | Issue #74 item 3: the rival buffers are indexed by `_resultCount` and sized `_resultBuf.Length × MaxDisambiguationRivals`, so one extra round wrote a slab past their end |
| E6 | **Leading-required-miss bar**: a round whose winner matched nothing of its pattern's FIRST required element consumes its span and produces **no result** — "whatever it scored", positional, nothing to configure | `:2742-2746`; flag latched `:3495`, `:3560-3561`, `:3611-3612`; `scoring.md:213-215` | Issue #124, design DR-1/DR-3. A phantom command executed a maneuver order in the field from a stranded tail. The first required element is the verb; "losing an argument leaves the action identified; losing the verb leaves no evidence that any action was requested at all" — "`minScore` sees 2/3 and cannot ask *which* third went missing" |
| E6b | Refuse-to-**fire**, not refuse-to-compete: the barred winner still consumes its span, so `searchStart` re-bases past leading debris and `SkippedBefore` stays off the next command | `:2717-2723`, `:2744`; `scoring.md:230-238` | **Measured over 699 utterances: refusing to compete destroys 11 commands scoring a clean 1.00; refusing to fire destroys none.** Worked case: "target hotel one cease fire" — excluding the barred `approach_target` from selection would charge `cease_fire` 0.40 |
| E6c | The bar costs recall where the leading word *was* spoken and dropped: the command is silent and nothing distinguishes "never spoken" from "spoken and lost" | `scoring.md:242`; `KNOWN_LIMITATIONS.md:449` | **Measured: 9 rows lose a genuine command against 39 invented ones suppressed** |
| E7 | The bar is **uniform over element type** (literal or slot) — "the first required element is the pattern's ANCHOR" | `:3556-3559` (DR-2); `scoring.md:226` | — |
| E8 | Latch is derived from `matchedRequired == 0 && missedRequired == 0` rather than a separate index; optionals are therefore skipped when locating the first required element | `:3488-3494`; `scoring.md:226` | Makes "the latch must skip optionals" structurally impossible to get wrong; an unspoken `?please` or `{?quantity}` never triggers it |
| E9 | The winner's score is reported **as selected** — no post-selection adjustment; the bar moves no score and no scoring constant | `:2748-2749`; `scoring.md:228` | — |
| E10 | Sibling-tie recording is **clear-on-adopt** and per-round (locals declared inside the round, `:2507-2510`) | `:2578-2588`, `:2499-2503` | A rival recorded in round 1 surviving into round 2 |
| E11 | Downstream of the bar: a barred winner opens **no pending of any kind** (not `allowPartialMatch`, not `requiresConfirmation`, not disambiguation) and **records no session-log attempt** | `:2742-2746` (returns before the `VoxrCommand` is even constructed, `:2763`); `scoring.md:240` | Issue #124. Consequence for diagnosis: "the number of commands an utterance yields is not the number of rounds it took" (`scoring.md:198`), and *fewer logged attempts than rounds* is itself the symptom (`scoring.md:517,547-559`) |
| E12 | A barred winner reports through `OnUnrecognisedSpeech` **only** where nothing else in the utterance fired and no other candidate was diverted/confidence-filtered/debounced — those suppressions are **utterance-wide** | `VoxrCommandRecogniser.cs:1087-1096`; `scoring.md:240` | Issues #124 + #133 |
| E13 | Construction-time sibling warning is **withheld** where the discriminating word is *every* member pattern's first required element (the bar makes the tie unaskable); where only *some* members are so anchored the warning still fires | `DiscriminatorIsEveryMembersAnchor` `:1929-1948`, gate `:1737`; `scoring.md:240` | Issue #124 |

### 5.4 Sibling ties (`:2175-2274`, `:2847-2987`, and `VoxrCommandRecogniser.cs:1129-1224`)

| # | Rule | Site | Failure it prevents |
|---|---|---|---|
| T1 | A sibling set = patterns element-wise equal but for one position, each holding a required literal | struct `:123-135`, built by `FindSiblingSets` `:1380` | Issue #74 DR-1 |
| T2 | Same-intent pairs are **not** rivals | `:2204-2212` | A question with one answer |
| T3 | When a pattern's optional expansion was truncated, the pair falls back to `RequiredElementsAreSiblings` — it "refuses on a cross-intent tie rather than claiming there is no hazard" | `:2243-2247`, `:333-335` | False "no hazard" on a pathological grammar |
| T4 | Only **sibling** rivals are recorded, never any tie | `:2659`, `:87-93` | "DR-4 makes the discriminating values the choice vocabulary, and a non-sibling tie has no discriminating values … Recording one would produce a question the speaker cannot answer" |
| T5 | One question per round: rivals from a *different* set set `Truncated` instead of joining | `:2674-2690` | Mixing two questions |
| T6 | Max 4 offered choices (`MaxDisambiguationRivals`), overflow sets `Truncated` | `:376`, `:2908-2915` | Sized against measured set sizes (`:353-359`) |
| T7 | A duplicate discriminating value is dropped silently; an *unanswerable* one sets `Truncated` | `:2885-2905`, `IsAnswerableRival` `:1969` | — |
| T8 | Fewer than two survivors is not a question — fall through and fire the winner | `VoxrCommandRecogniser.cs:1217-1218` | — |
| T9 | Choice index 0 is always the candidate that would have fired with the flag off | `:1153-1164`, `VoxrPendingCommand.cs:43-45` | Stable ordering for prompts and tests |
| T10 | Chosen alternatives are **not** re-tested against debounce or confidence | `VoxrCommandRecogniser.cs:1166-1199` | Re-gating "drops the only rival, the choice list falls below two, and the winner fires — silently degrading to the coin flip this feature exists to remove" |
| T11 | A rival whose intent is unresolvable is not offered but sets `Truncated` | `:1200-1208` | — |
| T12 | **Known gap**: rivals are recorded **before** the bar runs (`:2624` sits inside the candidate loop; the bar is at `:2742`), so a leading-missed *rival* can be offered as a choice and fires if picked — issue #126, "ruled recorded-not-gated" | `PendingCommandHandler.cs:148-155`; `scoring.md:240`; `KNOWN_LIMITATIONS.md:488` | Documented, not fixed. "The bar governs which candidate may **win a round**, not which may be offered as an alternative to one that did" |
| T13 | A tie is reported **on the winner's attempt**, never as an attempt of its own — only winners are logged | `VoxrCommandRecogniser.cs:1349-1350`, `VoxrMatchDiagnostics.cs:58,66`; `scoring.md:18,100` | — |
| T14 | A **non-sibling** tie is a grammar defect, reported to the Editor diagnostic only (`tiedRivalIsSibling: false`) and never actionable at runtime — the design half deliberately not built (issue #95) | `:2522-2525`, `:87-93`; `scoring.md:557` | `false` also covers the winner's own second phrasing, so compare `tiedRival`'s intent against `intent` (`scoring.md:557`) |
| T15 | **Sibling-analysis cap = 6 optional elements** (`MaxWarningExpansion`, `:964`): past it a pattern's sibling relations are read from required elements only — enough to refuse a `Commit`, **not** enough to name the rival, so with `disambiguateSiblingTies` on the winner fires without asking and only `PendingAmbiguity.IsTruncated` reports it | `:964`, `ExpansionTruncated` `:971-972`, fallback `:2243-2247`; `scoring.md:375`; `KNOWN_LIMITATIONS.md:756` | Known limitation |

### 5.5 Eager commit (`TryEagerCommit` `:4274-4566`; opt-in)

`scoring.md:338` states the seven conditions below are a **conjunction** — "None of the seven is implied by its neighbours" — and `scoring.md:360-361` adds the two extras that gate `Commit` alone. Verdicts: `Commit` (fire now), `HoldExtendable` (wait `prefixHoldSeconds` if set and shorter than `bufferWindow`, else the full window), `None` (wait the full window) — `scoring.md:334-336`, enum `VoxrCommandParser.cs:14-27`.

| # | Rule | Site | Failure it prevents |
|---|---|---|---|
| G1 | Condition 1 — `bestScore < minScore` → `None`; the score is "the same number the flush path would compute, coverage included" | `:4372-4373`, coverage tables rebuilt `:4291`; `scoring.md:340,350` | The scan mirrors an extraction round starting at token 0 so "the two can never disagree about a buffer they both see" |
| G5 | Conditions 2 + 3 — the match must start at the **first recognised** token and reach the **end of the buffer** (nothing left over, recognised or `[unk]`) | `:4503-4508`; `scoring.md:341-342,351-352` | Leading `[unk]` is free; a leading *resolved* word blocks the commit. At `coverageWeight = 0` a bare pattern wins selection, fails condition 3, and the verdict drops to `None` (`scoring.md:352`) |
| G2 | Condition 4 — any missed required **slot** → `None` | `:4428-4429`; `scoring.md:343,353` | Issue #66. Not implied by condition 3: "a missed slot consumes no recognised token, so a pattern can appear to span the buffer while missing an argument" |
| G3 | Condition 5 — any unmatched required **tail** → `None` | `:4455-4456`; flag `:3249`, `:3662`; `scoring.md:344,354` | Issue #70. A **medial** miss still commits; a **terminal** miss is refused because "committing there fires the *wrong* command, not merely an early one" (`switch to weapons` / `switch to navigation` on the buffer "switch to" both score 0.67, settled by registration order) |
| G4 | Condition 6 — leading required miss → `None` | `:4486-4487`; flag `:3277`; `scoring.md:345,355` | Issue #124 DR-5. Not inherited from the shared ordering — an explicit refusal, and it exists to **protect the buffer**: committing clears the accumulated transcript, so without it a half-spoken command would be flushed and discarded and its continuation parsed as a separate utterance |
| G6 | Condition 7 — confidence below `minConfidence` → `None`; `-1` bypasses rather than fails | `:4510-4512`; `scoring.md:346,356` | "Acoustic confidence is orthogonal to everything above it" |
| G8 | `Commit` extra 1 — the pattern must be **terminal** (last element cannot grow: no trailing optional, no variable-width `NumberSequence`, no enumerated value that is a word-prefix of another) **and** not a prefix of any concrete form of another pattern → else `HoldExtendable` | `:4528-4529`, `:4606-4607`; `scoring.md:360` | Issue #32: committing a prefix fires the wrong (shorter) command |
| G9 | `Commit` extra 2 — a tied **sibling** rival → `None`. "This is the only one of these rules that gates `Commit` alone" — a match already going to be held is left held | `:4563-4564`, `:4359-4367`; `scoring.md:361` | Issue #74 DR-5: committing "would fire a coin flip before the utterance is over" |
| G7 | `MaxOptionalExpansion = 12`: a pattern past it makes the terminality analysis unsound, and the refusal covers the **whole command set** — nothing commits early, every complete match degrades to `HoldExtendable`; construction warns with pattern, intent and optional count | `:4523-4524`, `:220`, `:4583-4584`, warning `:1026-1050`; `scoring.md:373` | Issue #44/#25: "a partial analysis could commit the *wrong* command" |
| G10 | `HasUnmatchedRequiredTail` deliberately has **no flush-side counterpart** | `:3243-3248` | On the flush path the transcript is final; refusing would fire nothing — the class S6 exists to rescue |
| G11 | The eager probe is one speculative parse **per VOSK result**, skipped while a pending is live | `VoxrCommandRecogniser.cs:592-599`; `scoring.md:330` | Confirm/follow-up speech stays on the timer path |

### 5.6 Recogniser-level gates (`VoxrCommandRecogniser.cs`)

| # | Rule | Site | Failure it prevents |
|---|---|---|---|
| R1 | `minScore` default 0.6, compared against the **full** score (fidelity + coverage together). Re-read **fresh every parse**, so an Inspector edit applies to the next utterance — but a **second frozen copy** is handed to the parser at build time for the Editor authoring warnings (issue #140) | `:31`, `:761`, `:931`; parser `_minScore` `:414`, `:407-413`; `scoring.md:246-248`; `api/command-recogniser.md:13` | Two lifetimes for one number; the frozen copy only updates on `RebuildParser`/`Configure`/`SetActiveSets`/`NotifySlotChanged` |
| R2 | `minConfidence` default 0.4; skipped when `Confidence < 0` (no word data). Doc: do **not** push it above ~0.5 — "two" scores ≈0.50 essentially always on the small English model, so higher rejects every `NumberSequence` command containing it | `:27`, `:980`; `scoring.md:252-256,286-289`; `KNOWN_LIMITATIONS.md:121` | — |
| R3 | `coverageWeight` default 1.0, `[FormerlySerializedAs("skippedWordPenalty")]`. **Consumed at parser build**, not per parse — so an Inspector edit takes effect only at the next rebuild, unlike `minScore` | `:61-63`, passed at `:247`/`:354`; parser `:203`; `scoring.md:96`; `api/command-recogniser.md:14` | DR-4 rename; the old name "described only the leading half" and hid that setting it to 0 also disables the #42 fix (`:199-202`). `[FormerlySerializedAs]` migrates the field but **not** a prefab-instance override (`KNOWN_LIMITATIONS.md:365`) |
| R4 | **Completeness gate**: a command missing a required slot does not fire *at any score* | `:930-931`, `IsIncomplete` `:1237-1241`, `HasUnfilledRequiredSlot` parser `:4152` | Issue #73: "a five-element pattern with one missed required slot lands on exactly 0.60 and cleared the default gate, firing a command whose argument the handler never receives" |
| R5 | An incomplete command on a definition with `AllowPartialMatch` is **routed** to pending, not rejected | `:933-961` | Same issue; "Routing rather than refusing outright is what makes the two halves one branch" |
| R6 | Per-intent debounce, `commandCooldown` default 0.3 s | `:94`, `:991-1000`, `CommandDebouncer.cs:17` | "duplicate commands from rapid VOSK results" |
| R7 | Gate order in Step 7: score/completeness → confidence → **debounce** → sibling tie → `requiresConfirmation` | `:931`, `:980`, `:991`, `:1007`, `:1057` | Ordering is argued at `:1002-1006`: debounce before the question so an answer isn't spoken into a cooldown; "which?" before "are you sure?" |
| R8 | Threshold-filtered results (confidence, debounce, pending-entering) are silently dropped — `OnUnrecognisedSpeech` fires **only** when nothing matched | `:1087-1096`, flag set at `:948`, `:982`, `:994`, `:1024`, `:1061` | Issue #133: telling the integrator the speech was not understood in the same frame it was asked to prompt about it |
| R9 | A *complete* new command preempts (cancels) a live pending; an incomplete one does not | `:736-743`, `:885-886` | Issue #73: "losing the half-finished command to an utterance that produces nothing" |
| R10 | Follow-up fill that is still incomplete re-arms the pending instead of firing | `:788`, `:846-847`, `AdvanceSlotFill` `PendingCommandHandler.cs:269` | Issue #77: a multi-slot exchange must be able to advance one utterance at a time |
| R11 | `Score <= 0` floor restated on the follow-up merge path | `:824-844` | Issue #113: a merged command reached a subscriber without passing either of the two existing floors |
| R12 | A pending must never come to carry a score its own fire paths would refuse — `AdvanceSlotFill` retains the prior score | `PendingCommandHandler.cs:288-289` | Issue #113 |
| R13 | Cancel vocabulary is matched **before** confirm and before choices, under every reason | `PendingCommandHandler.cs:113-117` | Design §5.5: "a discriminating value that IS a cancel word cancels rather than choosing, safety wins" |
| R14 | Confirm is **inert** (not a cancel) under `AwaitingDisambiguation` | `PendingCommandHandler.cs:164-168` | Leaves the pending live so the real answer can follow |
| R15 | Choice/confirm/cancel matching is **whole-utterance** | `MatchPhraseAgainstTokens` `:425-452`, esp. `:451` | "'set alpha mode on' is NOT read as the bare choice 'mode'" |
| R16 | `FireAsIs` timeout degrades to `Cancel` under ambiguity | `PendingCommandHandler.cs:374-380` | DR-6: "under ambiguity the INTENT itself is unknown" |
| R17 | Completing a choice whose definition `RequiresConfirmation` re-enters pending, using the **resolved** definition not the winner's | `PendingCommandHandler.cs:323-357`, esp. `:316-322` | "a destructive rival marked requiresConfirmation would fire without asking" |
| R18 | A grammar rebuild requested while a pending is live is **deferred** and drained on resolution | `:369-375`, `:386-393`, `:1258`, `:1263` | Rebuilding mid-exchange would drop the pending's context |
| R19 | `EffectiveBufferWindow` may only *shorten* the wait | `:605-608` | Issue #32 |
| R20 | Eager probing is skipped while a pending is live | `:592`; `scoring.md:330` | Confirm/follow-up stays on the timer path |
| R21 | `OnUnrecognisedSpeech`'s fire/silent set is published as **exhaustive**. Fires: no pattern matched; every candidate under `minScore`; winner barred (#124); winner missing a required slot without `allowPartialMatch` (#73); a follow-up fill completed a pending but re-scored ≤ 0 (#113). Silent for exactly three: any pending diversion (#133), `minConfidence`, debounce | `:1087-1096`, `:841-842`, `:898`; `scoring.md:309-321` | "being told the speech was not understood, in the same frame you were asked to prompt the speaker about it, is a contradiction" (`scoring.md:322`). It is **not** an "I heard nothing" signal — score rejections raise it too (`scoring.md:324`) |
| R22 | `pendingTimeout` (default 5 s) **restarts** on each slot fill and again on re-entering pending for confirmation — it bounds **silence**, not the exchange | `:105`, `:540`; `PendingCommandHandler.cs:296` (`CreatedTime = currentTime`), `:351`, `:256-268`; `api/command-recogniser.md:23` | "each fill buys one more window, and the first one nobody answers ends it" |
| R23 | `Configure(slots, sets)` registers sets but **activates none**; `SetActiveSets` rebuilds parser *and* grammar, while `NotifySlotChanged`/`RebuildParser` rebuild the parser only and never touch VOSK | `:263-277`, `:279-292`, `:340-346`, `:348-361`; `api/command-recogniser.md:54-62` | The grammar always reflects the **full universe** of slot values — a dynamic provider narrows the parser, never the decoder (`api/command-recogniser.md:59`) |
| R24 | `RebuildParser()` and `RebuildGrammar()` **throw** `InvalidOperationException` with no active commands, while `NotifySlotChanged()` silently no-ops in the same state | `:349-352`, `:365-367`, `:342-343`; `api/command-recogniser.md:61-63` | A documented inconsistency |

### 5.7 Construction-time grammar validation (all `[Conditional("UNITY_EDITOR")]` except the first)

| Pass | Site | Reports |
|---|---|---|
| `RunValidationWarnings` (**not** conditional — runs in players) | `:732-800`, called `:701` | Non-lowercase slot values/alias keys, punctuation, single-character values |
| `WarnOnDroppableRequiredLiteral` | `:868-957` | A pattern that is a prefix of a longer one where the extension's leading literal can be dropped |
| `WarnOnExcessiveOptionalExpansion` | `:1026-1050` | `> MaxOptionalExpansion` optionals (analysis abandoned) |
| `WarnOnSiblingDiscriminator` | `:1644-1859` | Sibling sets that could tie at runtime, gated on reachability (`ScoreAfterDroppingDiscriminator(set.Frame) < _minScore`, `:1827-1828`) and on the discriminator being every member's anchor (`DiscriminatorIsEveryMembersAnchor`, `:1929`); also cancel-vocabulary collisions (`:1769-1855`) |
| `WarnOnDuplicateIntent` | `:1080-1134` | Two definitions under one intent — divergent or interchangeable |

`_minScore` (`:414`) exists **only** to make these predictions against the threshold that will actually run (issue #140); the comment at `:407-413` states explicitly that no parse path reads it. `WarnOnSiblingDiscriminator`'s three gates are all at `:1737-1741`: not a single-intent set (`IsSingleIntent`, `:1889`), the tie is score-reachable (`ScoreAfterDroppingDiscriminator(frame) >= _minScore`, `:1346`), and the discriminator is not every member's anchor (`DiscriminatorIsEveryMembersAnchor`, `:1929`). The comment at `:1730-1736` records that the last two agree on today's grammar "by coincidence of arithmetic, not by construction".

### 5.8 Residues the current model does not close (documented, not fixed)

These are stated as *known* in `KNOWN_LIMITATIONS.md` and `scoring.md`. A redesign gets no credit for preserving them, but must know they exist before claiming a regression.

| Residue | Where documented | What it is |
|---|---|---|
| **#42 residue** | `scoring.md:431-438`; `KNOWN_LIMITATIONS.md:614` | If a stranded slot value's own first word begins some other pattern, the bare candidate is charged nothing and discards the argument exactly as before #65 — at the default `coverageWeight`. This is why the construction-time warning was deliberately *not* narrowed |
| **Coverage cannot promote a later candidate** | `scoring.md:178`; `KNOWN_LIMITATIONS.md:422` | Key 1 (earliest start) outranks score, so a demoted early winner blocks a better later one. 28/699 blocked, 27 recovered by a later round, 1 not |
| **Bar suppresses genuine recoveries** | `scoring.md:242`; `KNOWN_LIMITATIONS.md:449` | 9 rows of 699 lose a real command; nothing distinguishes "never spoken" from "spoken and dropped" |
| **Bar does not gate disambiguation choices** | `scoring.md:240`; `KNOWN_LIMITATIONS.md:488`; issue #126 | A leading-missed rival is recorded before the bar runs and can fire if the speaker picks it |
| **Unnameable sibling ties fire without asking** | `scoring.md:375`; `KNOWN_LIMITATIONS.md:756` | Past 6 optionals only `IsTruncated` reports it |
| **Duplicate/overlapping patterns across intents** | `KNOWN_LIMITATIONS.md:810` | The second intent can never fire; only the Editor tie diagnostic shows it |
| **Batch/injected/free-speech scores read low** | `scoring.md:145`; `KNOWN_LIMITATIONS.md:385` | `[unk]` exemption does not apply offline; treat a batch score as a lower bound |
| **Slot-initial patterns weaken the discarded-argument protection** | `scoring.md:127`; `KNOWN_LIMITATIONS.md:402` | An open-ended `NumberSequence` first element makes almost every token a legal start, so almost nothing is charged grammar-wide |
| **Validation warnings re-emit on every set switch** | `KNOWN_LIMITATIONS.md:308` | The parser is rebuilt, so the constructor's warning passes run again |
| **`rejectReason` numbers are culture-formatted** | `scoring.md:305` | `score 0,50 < minScore 0,60` on comma-decimal locales; the numeric log *fields* are invariant |

---

## 6. Extension points exposed to games

Documented contracts below come from `Documentation~/api/speech-recogniser.md` (102 lines) and `Documentation~/api/command-recogniser.md` (75 lines) and are cited as `api/…:N`.

### 6.1 `VoxrSpeechRecogniser` (`Runtime/VoxrSpeechRecogniser.cs`)

| Member | Site | Contract |
|---|---|---|
| `event Action<string> OnPartialResult` | `:36`; `api/speech-recogniser.md:56` | **Documented "fired on the main thread"**; text only, no words |
| `event Action<string> OnFinalResult` | `:37`, fired `:496`; `api/speech-recogniser.md:57` | **Documented "fired on the main thread"**, at utterance boundaries |
| `event Action<VoxrResult> OnResult` | `:39`, fired `:497`; `api/speech-recogniser.md:58` | Carries per-word confidence and timing. **No thread claim in the docs** — the only result event without one. **Subscribing changes behaviour**: word parsing is skipped when there is no subscriber outside the Editor (`:584-587`) |
| `event Action<VoxrBridgeErrorCode,string> OnError` | `:41`, fired `:604-607`; `api/speech-recogniser.md:59` | **Documented "fired on the main thread"** |
| `event Action OnModelReady` | `:42`, fired `:209`, `:229`; `api/speech-recogniser.md:60` | No thread claim in the docs |
| `bool IsModelReady` / `IsInitialised` / `IsRecognising` | `:85`, `:87`, `:118` | `IsInitialised`/`IsRecognising` return **false for a non-owner** (`:98`, `:133`) |
| `Initialise()` / `InitialiseAsync()` | `:153`, `:158` | Async; claims the process-global bridge synchronously before the first await (`:174`) |
| `ReleaseNativeResources()` | `:253` | Frees the claim **synchronously**, unlike `Object.Destroy` (`:639-641`) |
| `StartRecognition()` / `StartRecognitionAsync()` / `StopRecognition()` | `:284`, `:291`, `:370` | Stop drains queued results inline (`:398-407`) |
| `ResetRecogniser()` / `SetGrammar(string)` | `:416`, `:437` | Both refuse loudly for a non-owner |
| `InjectResult(string, VoxrWord[])` / `InjectPartialResult(string)` | `:458`, `:464` | Main-thread-asserted |
| `static VoxrWord[] CreateSimulatedWords(string, float)` | `:473` | 0.3 s per word (`:471`) |
| Inspector: `modelRelativePath` (`:23`, default `vosk-model-small-en-us-0.15`), `sampleRate` (`:26`, 16000), `micGainTargetDb` (`:34`, −18) | | |

### 6.2 `VoxrCommandRecogniser` (`Runtime/Commands/VoxrCommandRecogniser.cs`)

| Member | Site | Contract |
|---|---|---|
| `event Action<VoxrCommand> OnCommandRecognised` | `:128`, fired `:1099-1100`; `api/command-recogniser.md:42` | Once per command passing threshold **and** debounce, in extraction order; also fired for confirmed pendings (`:1255`) |
| `event Action<VoxrCommand[]> OnCommandsRecognised` | `:129`, fired `:1105-1107`; `api/command-recogniser.md:43` | The **full batch** from one utterance, after sequential extraction; fresh array each time |
| `event Action<string> OnUnrecognisedSpeech` | `:130`; `api/command-recogniser.md:44`; `scoring.md:309-321` | See R21 — the fire/silent set is documented as exhaustive. Parameter is the **full buffered transcript** |
| `event Action<VoxrCommand> OnCommandPending` | `:132`, fired `:1268`; `api/command-recogniser.md:45` | Covers three reasons and is documented as firing **more than once per pending** — again on each follow-up fill of some-but-not-all remaining slots. Carries **no reason**; read `PendingAmbiguity.HasValue`. Never opens for a barred winner, at any score |
| `event Action<VoxrCommand> OnCommandConfirmed` | `:134`, fired `:1254`; `api/command-recogniser.md:46` | **Also fires `OnCommandRecognised` and `OnCommandsRecognised`** in that order (`:1254-1257`). Answering a disambiguation raises it too — *unless* the chosen command sets `requiresConfirmation`, in which case `OnCommandPending` raises a second time instead |
| `event Action<VoxrCommand> OnCommandCancelled` | `:136`, fired `:1262`; `api/command-recogniser.md:47` | Timeout, cancel vocabulary, or **preemption** (one cancel then a fresh `OnCommandPending`); also from `CancelPendingCommand`, **either** `Configure` overload, `SetActiveSets`/`SetActiveSet`, and `OnDisable` |
| `Configure(slots, commands)` / `Configure(slots, sets)` | `:230`, `:263` | Both cancel any live pending (`:235`, `:268`) and rebuild the parser + grammar |
| `SetActiveSets(params string[])` / `SetActiveSet(string)` | `:279`, `:294` | Throws if `Configure(slots, sets)` was not called; triggers the grammar rebuild ceremony |
| `InjectText(string, VoxrWord[])` | `:299` | Main-thread-asserted; warns and no-ops before `Configure` |
| `FlushPendingBuffer()` | `:317` | Main-thread-asserted |
| `CancelPendingCommand()` | `:326` | |
| `RegisterSlotValueProvider` / `UnregisterSlotValueProvider` / `NotifySlotChanged` | `:330`, `:335`, `:340` | Provider invoked synchronously during parser rebuild (`DynamicSlotManager.cs:54`) |
| `RebuildParser()` / `RebuildGrammar()` | `:348`, `:363` | `RebuildGrammar` defers while a pending is live (R18) |
| `string[] ActiveSetNames` | `:186`, `CommandSetManager.cs:18` | Documented as **the recogniser's own array, not a snapshot copy — treat as read-only** (`api/command-recogniser.md:33`) |
| `bool HasPendingCommand` / `VoxrCommand? PendingCommand` | `:188`, `:190` | Under a disambiguation, `PendingCommand` is documented as **index 0 of `PendingAmbiguity.Choices`** (`api/command-recogniser.md:35`) |
| `VoxrPendingAmbiguity? PendingAmbiguity` | `:207` | Hands out the **live pending's own arrays, not copies** — `:203-206`: writing to them changes what fires |
| Inspector: `speechRecogniser` `:17`, `freeSpeechMode` `:23`, `minConfidence` `:27`, `minScore` `:31`, `coverageWeight` `:63`, `bufferWindow` `:72`, `eagerFlushOnCompleteMatch` `:79`, `prefixHoldSeconds` `:89`, `commandCooldown` `:94`, `slotAssets` `:97`, `commandSetAssets` `:98`, `initialActiveSetNames` `:101`, `pendingTimeout` `:105`, `pendingTimeoutBehavior` `:108`, `confirmVocabulary` `:112`, `cancelVocabulary` `:116`, `disambiguateSiblingTies` `:126` | | |

### 6.3 `VoxrPushToTalkController` (`Runtime/VoxrPushToTalkController.cs`)

`PressTalk()` `:105`, `ReleaseTalk()` `:116`, `ListeningMode` property `:52`, `UnityEvent OnTalkStarted` / `OnTalkEnded` `:101`, `:103`. Inspector: `_speechRecogniser` `:18`, `_commandRecogniser` `:22`, `_listeningMode` `:27`, `_initialiseOnStart` `:31`, `_cancelPendingOnRelease` `:44`. Lifecycle contracts: `OnEnable` announces only for a *new* intent (`:141-165`), `OnApplicationPause` stops/restarts (`:173-187`), `Update` reconciles the Android mic-permission race (`:189-195`).

### 6.4 ScriptableObject authoring assets

`VoxrSlotAsset` (`Runtime/Commands/VoxrSlotAsset.cs:14`, menu `VoXR/Slot Definition`) with public fields `slotName`, `slotType`, `values`, `aliases`, `minWords`, `maxWords`; `VoxrCommandAsset` (`VoxrCommandAsset.cs:13`, menu `VoXR/Command Definition`) with `intent`, `patterns` (space-separated tokens, `{slot}`/`{?slot}`/`?word` syntax documented in the tooltip at `:17-18`), `allowPartialMatch`, `requiresConfirmation`; `VoxrCommandSetAsset` (`VoxrCommandSetAsset.cs`); plus `VoxrTestSuiteAsset` / `VoxrAudioTestSuiteAsset` in `Runtime/Testing/`.

### 6.5 What a redesign would break

- The **pattern DSL is the serialized asset format.** `patterns` is `string[]` of space-separated tokens parsed at `VoxrCommandAsset.cs:40`; `ExtractSlotName` (`VoxrCommandParser.cs:4115`), `IsOptionalSlot` (`:4127`) and `IsOptionalLiteral` (`:4133`) define the syntax. Any change to element semantics silently reinterprets existing assets.
- `VoxrCommandDefinition` and `VoxrSlotDefinition` are **public readonly structs with public readonly fields** (`VoxrCommandDefinition.cs:13-19`, `VoxrSlotDefinition.cs:14-24`) — adding a field is source-compatible, changing one is not.
- `VoxrSlotMatch.Value` for a `NumberSequence` slot is documented as **words-as-spoken, never numeric** (`VoxrCommand.cs:15-21`, `:72-87`, `VoxrSlotType.cs:22-27`). Games call `VoxrNumberParser` themselves.
- `OnCommandPending` carries no reason (`VoxrPendingAmbiguity.cs:16-21` states why this matters) and is documented as firing several times per pending (`api/command-recogniser.md:45`).
- **`InjectText` buffers rather than parses** when `bufferWindow > 0` (`:574-581`; `api/command-recogniser.md:57`) — a caller wanting a synchronous parse must set `bufferWindow = 0` or follow with `FlushPendingBuffer()`. Any change to buffering changes the meaning of the primary test seam.
- **`ReleaseNativeResources()` frees the claim synchronously while `Destroy()` defers to end of frame** (`Runtime/VoxrSpeechRecogniser.cs:280-281`; `api/speech-recogniser.md:34-42`), so `Destroy(outgoing); incoming.Initialise();` is documented as *rejected*, and `Continuous` listening mode has no retry (unlike push-to-talk, which retries on the next press).
- **Non-owner behaviour is specified, not incidental**: `IsInitialised`/`IsRecognising` report `false`; `InitialiseAsync`/`SetGrammar`/`ResetRecogniser` reject, `Debug.LogError`, and fire `OnError` with `AlreadyInitialised` **naming the owner's GameObject**; `StopRecognition` is a quiet no-op (`:626-645`; `api/speech-recogniser.md:17-24`).
- The `disambiguateSiblingTies`, `eagerFlushOnCompleteMatch` and `freeSpeechMode` defaults are all **off** (`:126`, `:79`, `:23`) — the shipped default path is the timed-buffer, no-disambiguation, grammar-constrained one.
- `PendingAmbiguity` exposes live internal arrays (`:203-206`).

---

## 7. Test surface

Assemblies: `Jinwoo1601.VoXR.Tests.Runtime` (PlayMode; `Tests~/Runtime/Jinwoo1601.VoXR.Tests.Runtime.asmdef`, `excludePlatforms: ["Android"]`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]`) and `Jinwoo1601.VoXR.Tests.Editor` (`includePlatforms: ["Editor"]`). Both reach internals via `Runtime/AssemblyInfo.cs:9-10`. **`Tests~` is a stripped folder** — the suites do not exist in a consumer project and are run by renaming `Tests~`→`Tests` inside a host project.

### PlayMode (`Tests~/Runtime/`, ~14.4k lines)

| File | Lines | Covers |
|---|---|---|
| `VoxrCommandParserTests.cs` | 6630 | Scoring, admission, coverage, selection, sibling sets, grammar JSON, validation warnings |
| `VoxrCommandRecogniserInjectionTests.cs` | 2380 | The whole policy layer through `InjectText` |
| `VoxrEagerCommitTests.cs` | 1913 | Every `TryEagerCommit` verdict |
| `VoxrPendingCommandTests.cs` | 1884 | Pending state machine |
| `VoxrPushToTalkControllerTests.cs` / `VoxrPushToTalkPauseTests.cs` | 585 / 368 | PTT modes, `OnApplicationPause` |
| `VoxrDynamicSlotTests.cs` | 338 | Slot value providers |
| `VoxrWavReplayTests.cs` / `VoxrPlaybackSeamTests.cs` | 237 / 204 | WAV replay through the real DSP + libvosk (Editor-Windows only) |
| `VoxrSpeechRecogniserOwnershipTests.cs` | 230 | The one-recogniser-per-process rule |
| `VoxrAssetConversionTests.cs` | 444 | ScriptableObject → definition conversion |
| `ZeroAllocPollPathTests.cs` | 158 | Allocation on the poll path — via `Is.Not.AllocatingGCMemory()` (`:80`) and `Recorder.Get("GC.Alloc")` (`:131`), with a **mutation control** at `:102-113` proving the instrument is live |
| `DemoGrammar.cs` | 203 | Shared fixture grammar |
| `VoxrNumberParserTests.cs`, `VoxrCommandSetTests.cs`, `VoxrCoverageWeightRenameTests.cs`, `ParseWordsFromJsonTests.cs`, `Utf8ParserAvailabilityTests.cs`, `VoxrSpeechRecogniserInjectionTests.cs`, `VoxrSpeechRecogniserLifecycleTests.cs` | 109/67/109/108/21/159/76 | |

### EditMode (`Tests~/Editor/`, ~2.8k lines)

`VoxrCommandParserDiagnosticTests.cs` (570), `VoxrBatchTestRunnerTests.cs` (526), `VoxrCommandRecogniserDiagnosticTests.cs` (351), `VoxrDebugSessionLogTests.cs` (280), `VoxrWavReaderTests.cs` (228), `VoxrFixtureCorpusTests.cs` (168), `VoxrBatchTestWindowCoverageWeightTests.cs` (151), `DownsamplerTests.cs` (112), `AgcTests.cs` (110), `VoxrMatchDiagnosticsTests.cs` (109), `AudioMetricTests.cs` (81), `ModelExtractorValidationTests.cs` (74), `VoxrBridgeErrorCodeTests.cs` (42).

### Audio fixtures and offline rigs

- **WAV corpus**: `Tests~/Fixtures/audio/tts/` — 16 TTS files plus `manifest.json` (192 lines) and an empty `human/` (`.gitkeep`). Generated by `Tests~/Fixtures/generate.py` / `generate.sh`.
- **WSL harness**: `NativeBridge~/harness/main.cpp` (275) replays the corpus through the **desktop bridge in push mode** and diffs final transcripts against a committed baseline (`main.cpp:1-8`, `DrainResults` `:41`). Built only with `-DVOSK_BRIDGE_BUILD_HARNESS=ON` (`CMakeLists.txt:86-89`) against the stub capture backend.
- **A/B rig**: `Planning~/features/coverage-in-selection/ab-rig/` (`Program.cs`, `Check.cs`, `DocCheck.cs`, `PlayerCheck.cs`, `Sweep.cs`, `UnityStub.cs`, `Rig.csproj`, plus `issue124/Verify*.cs`) and a second copy under `features/fidelity-miss-cost/`. These compile the parser **outside Unity** against a `UnityStub`, so `UNITY_EDITOR` is undefined there — which is why the Editor-only diagnostics and `[Conditional]` warnings vanish in the rig (noted at `VoxrCommandParser.cs:1676-1679`). The rigs live under the gitignored `Planning~/` (`.gitignore:44`).

### Behaviours with no automated coverage

- **Everything on device.** The Android capture path (`audio_capture_audiorecord.cpp`), JNI attach/detach, mic permission (`Runtime/VoxrSpeechRecogniser.cs:313-347`), `OnApplicationPause` on a real doff — the Runtime asmdef **excludes Android** outright.
- **The Android bridge C++ itself**: no unit tests for `ring_buffer.h`, `result_queue.h`, `vosk_bridge.cpp` state machine; the harness exercises the desktop build only.
- **The AAudio capture backend** (`audio_capture_aaudio.cpp`, 101 lines) — never compiled by the default CMake config (`CMakeLists.txt:12`).
- **The push-audio C# bindings** (`BridgeNative.cs:53, 60, 64`) — no caller anywhere, so the negative-error-code convention documented at `:56-58` is unexercised from C#.
- **The audio gap / grammar-rebuild timing** — `GrammarManager.ForceApply` is testable but the actual gap duration is a device measurement.
- **Real-mic accuracy** — only TTS fixtures exist (`Tests~/Fixtures/audio/human/` is empty).
- **AGC Editor/device parity** — `AgcTests.cs` (110 lines) tests the C# `Agc` only; nothing compares it against `agc.h`, and they diverge (see §4.4).

---

## 8. Constraints a redesign must respect

1. **One recogniser per process.** Mechanical cause: every bridge entry point takes no handle and mutates file-scope statics (`vosk_bridge.cpp:20-42`). The C# side enforces it with a static owner (`Runtime/VoxrSpeechRecogniser.cs:53`) and a loud rejection (`:626-645`) that deliberately holds the *per-instance* Editor backend to the same rule "so a scene that works here cannot fail on device" (`:637-639`). Documented at `KNOWN_LIMITATIONS.md:244`.
2. **Main-thread-only public API.** `Update()`-driven polling (`Runtime/VoxrSpeechRecogniser.cs:508`), asserted entry points (`:500`, `VoxrCommandRecogniser.cs:301`, `:319`), and `UnityEngine.Debug`/`Time.time`/`Time.frameCount` used throughout the command layer (`VoxrCommandRecogniser.cs:536`, `:556`, `:903`). Crossing threads would also break the native span-lifetime contract (`BridgeNative.cs:66-71`).
3. **Zero-allocation steady state.** Pinned by `ZeroAllocPollPathTests.cs` (`Is.Not.AllocatingGCMemory()` at `:80`, allocation *count* via `Recorder.Get("GC.Alloc")` at `:131`, with a mutation control at `:102-113`). The mechanisms: byte-span JSON parsing with a `[ThreadStatic]` unescape buffer (`VoxrJsonParser.cs:28`), pooled parser buffers (`VoxrCommandParser.cs:290-291`, `:300`, `:304`, `:388-390`, coverage tables `:280-282` "grown on demand and never shrunk"), a pooled accepted-command buffer (`VoxrCommandRecogniser.cs:184`, `:429-433`), a growable word buffer in the utterance buffer (`UtteranceBuffer.cs:15`, `:29`), and a cached delegate to avoid per-frame method-group allocation (`Runtime/VoxrSpeechRecogniser.cs:72`, `:550`). Deliberate exceptions are documented where they occur: slot arrays crossing into events (`VoxrCommandParser.cs:2755`, `PendingCommandHandler.cs:231-237`) and the disambiguation choice arrays (`VoxrCommandRecogniser.cs:1123-1128`).
4. **Editor/device parity is a stated design rule, and is currently imperfect.** Same DSP constants (`Downsampler.cs:22-27` = `downsampler.h:64-68`), same int16 conversion (`EditorMicBackend.cs:550` explicitly "Matches vosk_bridge.cpp:44-51"), same `set_words(1)`, same final-result flush on stop (`EditorMicBackend.cs:209-211` cites the Android line numbers). Known divergences: the AGC noise gate (§4.4), `needFullParse` always true in the Editor (`Runtime/VoxrSpeechRecogniser.cs:585-587`), sibling ties always recorded in the Editor (`VoxrCommandParser.cs:378-381`), and the eager/sibling caches built at construction in the Editor but lazily in a player (`:698-708`).
5. **Package layout: `~` folders are stripped.** `NativeBridge~`, `Tests~`, `Documentation~`, `Samples~`, `Planning~` never reach a consumer project. Consequences: the C++ sources ship as *documentation*, not as a build; the prebuilt `.so` must be committed (`Runtime/Plugins/Android/arm64-v8a/libvosk-bridge.so`, 56 KB); the test suites must be relocated into a host project to run.
6. **Plugin and model size.** Committed binaries: `libvosk.so` **8.86 MB** (arm64), `libvosk-bridge.so` 56 KB, and on the Editor side `libvosk.dll` **26.4 MB** + `libstdc++-6.dll` 26.6 MB + `libgcc_s_seh-1.dll` 606 KB + `libwinpthread-1.dll` 367 KB (Editor-only, but in the repo). The acoustic model is *not* in the repo — it is shipped as a StreamingAssets ZIP and unpacked at first run into `persistentDataPath/VoxrModels` (`ModelExtractor.cs:18`, `:25-27`, `:45`), so first-launch cost is a disk unzip on a background thread (`:53`).
7. **Unsafe code is enabled in the Runtime assembly** (`Runtime/Jinwoo1601.VoXR.Runtime.asmdef:7`, `"allowUnsafeCode": true`) — required by `SpanFromPtr`/`SpanFromNullTerminated` (`BridgeNative.cs:72`, `:77`). The Runtime asmdef has **zero references** (`:4`) and is `autoReferenced`.
8. **C# language level is pinned low.** `VoxrJsonParser.cs:15-17`: Unity 6000.3's default `LangVersion` is C# 9, so UTF-8 string literals (`"..."u8`) are unavailable and `Encoding.UTF8.GetBytes` statics are used instead.
9. **The workflow layer is gitignored and local-only**: `.claude/`, `CLAUDE.md`, `Planning~/`, `NativeBridge~/build*/`, `NativeBridge~/vendor/` (`.gitignore:32-44`). The A/B rigs and every design doc live inside `Planning~/` and therefore ship with nothing.
10. **Apache-2.0 and fully offline.** `package.json:21`; the vendored `vosk_api.h` header carries the upstream Apache-2.0 notice (`NativeBridge~/include/vosk_api.h:2-4`) with "Do not modify — it must match the prebuilt libvosk binary".
11. **The published rule set is itself a constraint.** `Documentation~/scoring.md` (570 lines) states the score formula, admission, coverage, selection keys, the bar, both gates and the eager verdicts as normative behaviour, with two enumerations declared exhaustive (`:309-321`, `:338-361`) and five specific 699-utterance measurements as justification (`:178`, `:238`, `:242`, `:454`, `:511`). `Documentation~/api/*.md` publishes main-thread delivery for `OnPartialResult`/`OnFinalResult`/`OnError` (`api/speech-recogniser.md:56,57,59`), the `OnCommandConfirmed` → `OnCommandRecognised` → `OnCommandsRecognised` chain (`api/command-recogniser.md:46`), and the "one recogniser per process" rule with its exact non-owner behaviour (`api/speech-recogniser.md:7-42`). Any of these changing is a documentation rewrite, not just a code change.
12. **48 kHz input is hard-required.** `EditorMicBackend.cs:330-339` and `:452-461` both refuse any other rate with the message "The 48 kHz → 16 kHz downsampler cannot handle other input rates"; the device side hard-codes 48000 (`audio_capture_audiorecord.cpp:10`).

---

## 9. Observed code smells and friction

1. **`VoxrCommandParser.cs` is 4918 lines in one file** holding at least five distinct responsibilities: the matcher/scorer (`:3467-3669`), the selector (`:2455-2838`, `:3344-3413`), the grammar-JSON generator (`:3076-3207`), the sibling-set analyser (`:1380-2274`), and five construction-time validation passes (`:732`, `:868`, `:1026`, `:1080`, `:1644`). Comment-to-code ratio is extreme in the rule regions — e.g. `:145-220` is 76 lines of prose for 5 constants, and `:773-823` inside `ProcessParsedResultsCore` in the recogniser is a 50-line comment for a 20-line branch.
2. **`VoxrCommandRecogniser.ProcessParsedResultsCore` is a 460-line method** (`:650-1112`) with seven numbered steps, 16 `#if UNITY_EDITOR` islands interleaved with the decisions, and two flags (`hasCompleteNewCommand` `:736`, `anyThresholdFiltered` `:905`) whose correctness depends on being set at five scattered sites each.
3. **Nine independent `#if UNITY_EDITOR_WIN` forks in `VoxrSpeechRecogniser`** (§4.2) — every lifecycle operation is written twice.
4. **Duplicated DSP between C# and C++**, with the AGC pair having already silently diverged (`Runtime/Dsp/Agc.cs:66` gates on `absX`, `NativeBridge~/src/agc.h:53` gates on `smoothed_level_`). The comment at `Agc.cs:63-64` says "See divergence note in the class summary" — **there is no such note**; the class summary is `Agc.cs:1-13`. Nothing tests the two implementations against each other.
5. **Dead / unreachable native code.**
   - `audio_capture_aaudio.cpp` + `.h` (141 lines) — never selected by the default CMake config, and `CMakeLists.txt:11` calls it "unmaintained legacy (broken on Quest)". Kept as a compile option.
   - `vosk_bridge_has_result` (`vosk_bridge.h:56`, `vosk_bridge.cpp:379`) — **has no C# binding at all**; the only consumer would be the harness, which uses `get_result` directly.
   - `vosk_bridge_start_push`, `vosk_bridge_push_audio`, `vosk_bridge_get_input_level` — bound in `BridgeNative.cs:53, 60, 64` with **zero call sites** in `Runtime/`, `Editor/`, `Tests~/` or `Samples~/`. The push pair is used only by the C++ harness. `get_input_level` has no consumer anywhere (its intended Tier-D consumer was dropped).
   - `DefaultSkippedWordPenalty` (`VoxrCommandParser.cs:212`) is `[Obsolete]` and its own comment (`:205-207`) admits it is "Inert in practice … there is no external call site for the warning to reach".
6. **Four stale CMake build trees are committed-adjacent but gitignored**: `NativeBridge~/build/`, `build-1.1.0/`, `build-1.2.0/`, `build-desktop/` (each with a `CMakeCache.txt` and a generated `CMakeCXXCompilerId.cpp`). `.gitignore:38` covers `build*/`, so these are untracked local residue that still shows up in any file sweep.
7. **Two divergent copies of the A/B rig** — `Planning~/features/fidelity-miss-cost/ab-rig/` (4 files) and `Planning~/features/coverage-in-selection/ab-rig/` (7 files + `issue124/` with four `Verify*.cs`). Both carry their own `UnityStub.cs`, so the parser has two shadow build configurations neither of which is exercised by CI.
8. **Known false positive in error detection.** `Runtime/VoxrSpeechRecogniser.cs:568-571`: the error probe is a substring search for `"error"` across the whole payload; the TODO admits a recognised word "error" would trip it.
9. **`RunValidationWarnings` is the one validation pass that is not `[Conditional("UNITY_EDITOR")]`** (`VoxrCommandParser.cs:732`, called at `:701`) — so slot-value casing/punctuation warnings log in shipped players, on every parser rebuild. Related: `KNOWN_LIMITATIONS.md:308`, "Validation warnings re-emit on every active-set switch".
10. **The result-buffer contract is a parallel-array protocol across a public event boundary.** `ResultBuffer` (`:2989`) and `TiedSiblingBuffer` (`:2994`) are index-parallel pooled arrays the recogniser walks while raising events a subscriber may answer by calling `Configure` or `InjectText`. `VoxrCommandRecogniser.cs:745-757` snapshots the parser to survive the first case and explicitly declares the second "pre-existing and unsupported".
11. **`vosk_bridge_push_audio` returns a negated error code** while every other binding returns a positive one — `BridgeNative.cs:55-58` warns "do not cast this result to `VoxrBridgeErrorCode` without negating it first (unlike every other binding)". An ABI inconsistency preserved by comment.
12. **Grammar change requires a full recognizer teardown.** `vosk_bridge_set_grammar` frees and recreates (`vosk_bridge.cpp:356-365`) and refuses while running (`:352-353`), so `GrammarManager.ForceApply` must stop→set→start (`GrammarManager.cs:43-52`); `EditorMicBackend.SetGrammar` carries the same comment, "VOSK has no grammar-swap API" (`:268`). Any dynamic-slot change (`NotifySlotChanged` → `RebuildParser`, `:340-346`) rebuilds the *parser* but not the grammar, while `SetActiveSets` rebuilds both (`:291`, `:435-439`) — two different costs behind two similarly-named methods.
13. **`Application.persistentDataPath` model cache has no version key** — `ModelExtractor.cs:25-27` keys only on the model folder name, and validity is a directory-content check (`:35`). Shipping a different model under the same name would reuse the stale extraction.
