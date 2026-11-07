# Native Bridge

The SDK includes a prebuilt `libvosk-bridge.so` for Android arm64 and a prebuilt `voxr-frontend.dll` for the Windows Editor, both built from `NativeBridge~/`. This guide covers building them from source, which is only necessary if you need to modify the C++ layer, and the [audio front end](#audio-front-end) the two share.

The bridge has two operating modes: **capture mode** (`vosk_bridge_start`), where a platform capture backend feeds the microphone into the recognition pipeline, and **push mode** (`vosk_bridge_start_push`), where the caller supplies pre-DSP 48 kHz mono float audio via `vosk_bridge_push_audio` — used by the automated-verification tiers to replay recorded fixtures through the real pipeline (ring buffer → [front end](#audio-front-end): 48→16 kHz decimator → AGC2 → int16 → VOSK) without a microphone. Pushed writes are clamped to the free ring space and `vosk_bridge_push_audio` returns the count it actually wrote, so a short return means the ring is full — let the recognition thread drain it and retry the remainder; pushed audio is never overwritten (a capture backend, by contrast, overwrites the oldest samples and raises an overflow). The two modes are mutually exclusive per session. A read-only input-level getter (`vosk_bridge_get_input_level`, ~300 ms rolling RMS of pre-DSP audio) supports mic-liveness checks — it is the device-side source of the public `VoxrSpeechRecogniser.InputLevel`, with the Editor backend computing the same quantity in C# so both platforms report one contract. Push mode is not game-facing API: the C# layer binds `vosk_bridge_start_push` and `vosk_bridge_push_audio` (`BridgeNative`) but nothing in the runtime calls them; its only consumer is the native desktop harness, which calls the C entry points directly.

---

## Prerequisites

- **Android NDK** -- bundled with Unity 6 or standalone r26+
- **CMake 3.18+** -- bundled with Unity's Android toolchain or standalone
- **Ninja build system** -- bundled with Unity's Android toolchain or standalone
- **`libvosk.so` for Android arm64** -- download from [VOSK releases](https://github.com/alphacep/vosk-api/releases)
- **The pinned AGC2 source archives** -- `webrtc-audio-processing-2.1.tar.xz` and `abseil-cpp-20240722.0.tar.gz` in `NativeBridge~/vendor/` (git-ignored). Every configure checks both against their SHA-256 and stops if either is missing or differs; the download and check steps are in `NativeBridge~/harness/README.md` (*One-time provisioning*). See [Audio Front End](#audio-front-end).

---

## Build Steps

1. Place `libvosk.so` in `Runtime/Plugins/Android/arm64-v8a/` (the location the package's CMake build expects by default — `NativeBridge~/CMakeLists.txt` sets `VOSK_LIB_DIR` here when nothing else is provided).

2. Configure and build with CMake:

```bash
CMAKE="/path/to/cmake"
NDK_WIN="C:/path/to/NDK"

"$CMAKE" -B NativeBridge~/build \
         -S NativeBridge~ \
         -DCMAKE_TOOLCHAIN_FILE="$NDK_WIN/build/cmake/android.toolchain.cmake" \
         -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-27 -DANDROID_STL=c++_shared \
         -DCMAKE_BUILD_TYPE=Release \
         -DCMAKE_MAKE_PROGRAM="/path/to/ninja" \
         -G Ninja

"$CMAKE" --build NativeBridge~/build --config Release -j 4
```

Replace the paths with your actual NDK, CMake, and Ninja locations. If you are using Unity's bundled Android toolchain, the tools are located under your Unity Editor installation at `Editor/Data/PlaybackEngines/AndroidPlayer/`.

3. Install the result. Unity loads the plugin from `Runtime/Plugins/Android/arm64-v8a/libvosk-bridge.so`, not from the CMake build directory, so copy it into place:

```bash
cp NativeBridge~/build/libvosk-bridge.so Runtime/Plugins/Android/arm64-v8a/libvosk-bridge.so
```

Without this step the build succeeds and the shipped plugin stays at its previous revision, so the change never reaches the device.

`NativeBridge~/build.sh` runs all three steps in one command — configure, build, and copy into the plugin folder. It expects `ANDROID_NDK_HOME` to be set, `libvosk.so` to be in the plugin folder already, and `cmake` on your `PATH`; use the explicit invocation above when you need Unity's bundled toolchain instead.

---

## Capture Backend Selection

The capture backend is selected at configure time by the CMake option `VOSK_BRIDGE_CAPTURE` (`audiorecord` | `aaudio` | `stub`, default `audiorecord`). It picks both the compiled `audio_capture_<backend>.cpp` and the header the backend-neutral `audio_capture.h` forwards to — `vosk_bridge.cpp` includes only the neutral header. The default Android build is source-identical to the pre-option build. The `stub` backend (no capture hardware; a "silent microphone") exists for desktop builds where audio enters via push mode only; `aaudio` is accepted by the option but is unmaintained legacy (see below) and is not verified to compile.

---

## Desktop Build & Windows Harness

`NativeBridge~/CMakePresets.json` defines one preset, `windows-x64` (CMake's "Visual Studio 17 2022" generator, x64, stub backend, Release, output in `NativeBridge~/build-windows/`). It builds the bridge with MSVC from Visual Studio 2022 Community — no NDK, no JNI — against Alphacephei's pinned Windows prebuilt `libvosk` 0.3.45 (`libvosk.dll` and its import library `libvosk.lib`, provisioned under `NativeBridge~/vendor/vosk-win64-0.3.45/`), the same `libvosk.dll` the Editor ships in `Runtime/Plugins/x86_64/`. With it comes a CLI harness (`NativeBridge~/harness/`) that replays the committed fixture corpus (`Tests~/Fixtures/audio/tts/`) through the real bridge in push mode and compares final transcripts against a committed baseline (`harness/expectations.json`, which pins the grammar, AGC gain, libvosk version, and model conf it was decoded under, and per fixture the [front end's](#audio-front-end) output sample count and digest). The model it decodes is the Continuous default's: `confgen` (`harness/confgen/`, a .NET SDK 10 console tool that compiles the package's own `Runtime/EndpointerOverlay.cs` unedited) writes the [endpointer profile](endpointer-profile.md) into a copy of the vendored model, so the pinned conf is the one the package itself produces. Configure, build, `confgen`, and run are one command in Git Bash on the Windows host, driven by the cmake 3.22.1 that Unity 6000.3.7f1's Android SDK ships, so bridge-logic changes are verifiable in seconds; it deliberately cannot validate the arm64 binary, JNI capture, or Quest audio routing (the on-device tier owns those). Provisioning, the build-and-run command, and re-baselining are documented in `NativeBridge~/harness/README.md`. The same preset builds the Windows Editor's front end, `voxr-frontend.dll`, into `build-windows/bin/`; the shipped copy is `Runtime/Plugins/x86_64/voxr-frontend.dll`, and no build step writes it, so a rebuilt DLL is copied there by hand. Otherwise the Windows build exists for the harness: `Runtime/Plugins/x86_64/` ships no bridge DLL, and the Windows Editor never loads `libvosk-bridge` -- its capture and VOSK calls stay managed over `libvosk.dll`, with only the front end native.

How the Windows build holds together, and why:

- **The Visual Studio generator, not Ninja.** The generator locates the Visual Studio 2022 installation itself and drives MSBuild, so no compiler-environment script (`vcvars64.bat`) runs and the whole command stays plain Git Bash. The command pins the installation with `CMAKE_GENERATOR_INSTANCE`, and the "Visual Studio 17 2022" generator selects only a Visual Studio 2022 instance, so a missing installation fails instead of falling back to another. The MSVC toolset is the generator's default, not pinned: a Visual Studio update can move it, and the baseline judges any effect.
- **Release lives in the build preset**, because `cmake --build` on a Visual Studio tree builds Debug when no configuration is given.
- **No argument Git Bash would rewrite.** Nothing passed to a Windows program starts with `/` (paths are `C:/…` or relative, and `/utf-8` reaches the compiler through `CMakeLists.txt`), so the command behaves the same with or without `MSYS_NO_PATHCONV`.
- **The vendor is checked at configure time.** On Windows, configure fails naming the first missing file among `libvosk.lib`, `libvosk.dll`, and the three MinGW runtime DLLs (`libstdc++-6.dll`, `libgcc_s_seh-1.dll`, `libwinpthread-1.dll`) in `VOSK_LIB_DIR`; nothing else is searched, so no other `libvosk` can be linked in its place. On every platform the two AGC2 archives are hash-checked as well ([Audio Front End](#audio-front-end)).
- **One run directory.** `vosk-bridge.dll`, `vosk-bridge-harness.exe`, `voxr-frontend.dll`, and `fe-probe.exe` land together in `build-windows/bin/`, and configure copies the four vendor DLLs beside them, so the loader takes them from the executable's directory before any `PATH` entry — Git for Windows puts its own `libgcc_s_seh-1.dll` on Git Bash's `PATH`.
- **`/utf-8` under MSVC**, so the sources' UTF-8 punctuation compiles the same whatever the host's code page.
- **The GNU link flags** (`-Wl,--no-undefined -Wl,--strip-all -Wl,--exclude-libs,ALL`) apply only off MSVC, which rejects them.
- **The pinned zip is hash-checked and extracted as-is.** The README checks `vosk-win64-0.3.45.zip`'s SHA-256 before extracting and stops on a mismatch; the version in the directory name makes a re-pin a visible preset edit.
- **The baseline is not re-pinned for a Windows divergence.** Re-baselining follows a corpus regeneration, a libvosk bump, or a reviewed front-end change; a transcript or front-end pin mismatch on the Windows build (exit 1) stops for a ruling instead.

---

## Audio Front End

The device and the Windows Editor run one native front end, `voxr::FrontEnd` (`NativeBridge~/src/frontend/`), between 48 kHz mono float input and VOSK. It is one static library, `voxr-frontend-core`, linked into `libvosk-bridge` on Android and in the harness. On Windows the same library plus a small C ABI shim builds `voxr-frontend.dll`, which depends on neither `libvosk` nor the bridge: the Editor needs the signal processing, not the bridge's process-wide session, so `EditorMicBackend` keeps capture and its VOSK calls managed and calls the front end through `Runtime/Native/FrontEndNative.cs`. The device ships no second `.so`. Because both callers run the same code, what you test in the Editor is the front end the Quest runs.

### Signal path

1. **Decimation, 48 → 16 kHz.** Each input sample enters a direct-form FIR low-pass, and the dot product is evaluated only at every third input, in scalar C++. The taps are a committed table, `src/frontend/decimator_taps.h`, generated by `NativeBridge~/tools/design_decimator.py --design f1` (the generator's default design): a Kaiser-windowed sinc, 219 taps, cutoff 8000 Hz, β 5.65326, normalised in double precision to sum to 1 so the DC gain is unity, then written as float literals. A committed table is the same bits under MSVC and clang, where taps designed at run time would depend on each C runtime's `sin` and Bessel functions, and the generator records where the numbers came from -- regenerate the header rather than editing it.
2. **Framing.** Decimated samples collect in a 160-sample (10 ms) frame. Only whole frames are processed and returned; a remainder waits for the next call. The output therefore depends only on the input since the last reset or stream start, never on how the caller chunked it -- the bridge passes whatever the ring buffer holds, the Editor whatever a frame's worth of microphone time delivered. A caller sizes its int16 buffer for `n / 3` samples plus one frame; a smaller buffer is refused with `-1`.
3. **AGC2.** WebRTC AGC2 classes, compiled from pinned `webrtc-audio-processing` 2.1 source, run once per frame in the order upstream's `gain_controller2.cc` uses: RNN voice detector, noise-level estimator, speech-level estimator, saturation protector, adaptive digital gain controller, a fixed-gain stage at 0 dB, limiter. The classes are composed directly rather than through `GainController2`, which keeps the applied gain and the speech probability private and takes APM's `AudioBuffer`, pulling in buffer and band-split code outside the compiled subset. No input-volume controller, noise suppressor or echo canceller is compiled.
4. **int16.** The limited frame is rounded and clamped to the int16 range (NaN becomes 0), so full-scale or over-range input saturates instead of wrapping.

### Configuration

Adaptive digital gain on; headroom −`micGainTargetDb` dB; maximum gain 50 dB; initial gain 15 dB; gain change at most 6 dB/s; output noise ceiling −50 dBFS; fixed gain 0 dB (`src/frontend/front_end.cpp`). `micGainTargetDb` is the only value that crosses into the unit -- as `vosk_bridge_init`'s `mic_gain_target_db` on device and `voxr_fe_create`'s `target_db` in the Editor -- and it is read once, when the unit is created. A non-finite target falls back to −18; a target above 0 is treated as 0. The adaptive controller is also handed the saturation protector's headroom every frame, so the target sets AGC2's configured headroom rather than an exact output speech level. Keeping the field's name and meaning is what lets an existing scene upgrade with no serialised-field migration.

### Lifecycle

| Call | Clears | Keeps |
|------|--------|-------|
| `BeginStream` | Decimator history, the unfinished frame, stats and digest | All AGC2 state -- the learned speech and noise levels, the gain, the voice detector, the limiter envelope |
| `Reset` | All of the above and all AGC2 state; the gain reading returns to the 15 dB initial gain | Nothing |

On device, `vosk_bridge_init` creates the unit cold, `vosk_bridge_start` and `vosk_bridge_start_push` call `BeginStream`, and `vosk_bridge_reset` and `vosk_bridge_destroy` call `Reset`. In the Editor, `EditorMicBackend.Start` calls `BeginStream`, `Reset` and `StartPlayback` call `Reset`, and `Release` destroys the unit. So the learned level survives `StopRecognition()` / `StartRecognition()`, and `ResetRecogniser()` or a re-initialisation returns the front end to its cold start. The learned level is AGC2's state and is worth keeping; decimator history and an unfinished frame belong to one stream, and carrying them across a stop would splice pre-stop audio onto the next session -- so an unfinished frame (under 10 ms) at a stop is dropped. A WAV replay starts cold, as the harness does per case, so replays do not depend on their order. The pinned source gives the voice detector, the noise-level estimator and the adaptive controller no reset, so `Reset` rebuilds those three and allocates; it is a control call, never on the per-chunk path.

### Stats and the digest

The unit counts input and output samples since the last reset or stream start, folds every int16 sample it emits into a 64-bit FNV-1a digest, and reports the applied gain in dB with the last frame's pre- and post-gain RMS. AGC2 exposes no getter for the applied gain, so the unit reads it as the frame-energy ratio across the adaptive stage; a zero-energy frame holds the previous reading. The Editor debug window's level meters read these stats (`AgcGain` as a linear factor). On device the bridge publishes a snapshot after every chunk for [`vosk_bridge_get_frontend_stats`](#abi-exports); the harness pins each fixture's `frontEndSamples` and `frontEndDigest` from it. Pinning and re-baselining the digests, and their dependence on the pinning host's CPU features, are documented in `NativeBridge~/harness/README.md`.

### C ABI (Windows)

`src/frontend/voxr_frontend.h`: handle-based, `cdecl`, blittable, with caller-owned buffers -- no strings, no callbacks. Arrays marshal by pinning, so a chunk allocates nothing on the managed side, and a handle lets each backend own its unit and a test run two.

| Symbol | Purpose |
|--------|---------|
| `int32_t voxr_fe_abi_version(void)` | ABI version, `1`. |
| `void* voxr_fe_create(float target_db)` | Creates a cold unit for the target; `NULL` on failure. |
| `int32_t voxr_fe_process(void* handle, const float* in48k, int32_t n, int16_t* out16k, int32_t cap)` | Feeds `n` samples at 48 kHz; returns the int16 samples written (whole 10 ms frames only), or `-1` for a null handle or buffer, a negative `n`, or a `cap` below the frames due. |
| `void voxr_fe_begin_stream(void* handle)` | `BeginStream` (see [Lifecycle](#lifecycle)). |
| `void voxr_fe_reset(void* handle)` | `Reset`. |
| `int32_t voxr_fe_get_stats(void* handle, voxr_fe_stats* out_stats)` | Copies the stats into a 40-byte `voxr_fe_stats`; `0`, or `-1` for a null argument. |
| `void voxr_fe_destroy(void* handle)` | Frees the unit. |

`EditorMicBackend.InitialiseAsync` calls `voxr_fe_abi_version` before the model loads, in its own `try`. A missing DLL, a missing export or another version raises `ModelLoadFailed` naming `voxr-frontend.dll` and `Runtime/Plugins/x86_64/`, and initialisation fails -- there is no managed fallback. The DLL's import settings enable it for the Windows Editor on x86_64 only and exclude it from every player build. `FrontEnd::Process` takes an optional fifth argument, the post-limiter floats, for `fe-probe` alone; it is C++ only and never on the C ABI.

### Build

The build never downloads the AGC2 source. `webrtc-audio-processing-2.1.tar.xz` and `abseil-cpp-20240722.0.tar.gz` sit in `NativeBridge~/vendor/`, provisioned by hand. `cmake/webrtc_agc2.cmake` holds both SHA-256 pins, hashes each archive at configure, stops naming the file and both hashes on a mismatch (or the file on its absence), and extracts the verified archives under the build directory, so only verified bytes compile. The WebRTC subset is an explicit source list; abseil builds by `add_subdirectory(… EXCLUDE_FROM_ALL)`, and only `absl::strings` is linked. Two settings must stay as they are:

- **The static MSVC runtime.** The project sets `CMAKE_MSVC_RUNTIME_LIBRARY` to the static runtime, so `voxr-frontend.dll` needs no runtime DLL and cannot fail on a runtime version the Editor process chose. `ABSL_MSVC_STATIC_RUNTIME` must stay `ON` beside it: abseil otherwise sets its own runtime and builds `/MD`, reintroducing the dependency.
- **No `WEBRTC_ENABLE_SYMBOL_EXPORT`.** It is left undefined on purpose: it turns WebRTC's `RTC_EXPORT` into exports.

The front-end core, WebRTC and abseil compile with hidden visibility and inlines, and the `.so` link adds `--exclude-libs,ALL`, so the abseil inside `libvosk-bridge.so` cannot collide with another plugin's abseil in the same APK. Under clang the core and the subset compile with `-ffp-contract=off`.

`fe-probe` (`harness/fe_probe.cpp`, built with the harness) links only `voxr-frontend-core` and the WAV reader. Given `--fixtures <dir> --out <file>`, it measures the decimator's response, reads back the configuration, checks the int16 output's bounding on full-scale and over-range input, and measures the gain behaviour over pauses, noise and bursts, the `micGainTargetDb` mapping, the added latency, per-frame host cost and any allocation inside `Process`, prints one JSON report, and exits `1` when a bar fails (`2` on a usage or I/O error).

---

## ABI Exports

The complete C ABI is declared in `NativeBridge~/src/vosk_bridge.h`. All symbols are `extern "C"` and marked `VOSK_BRIDGE_EXPORT`: default visibility on every non-Windows target (the Android build), `__declspec(dllexport)` when MSVC compiles the bridge DLL itself (CMake defines `vosk_bridge_EXPORTS` for the `vosk-bridge` target), and `__declspec(dllimport)` for a consumer such as the harness — MSVC emits no import library for a DLL with no exports, so the harness could not link without it. The target's visibility presets are `hidden`, the front-end core, the WebRTC subset and abseil compile hidden as well, and off MSVC the link adds `-Wl,--exclude-libs,ALL`, so nothing but the C ABI is exported -- plus, on Android, `JNI_OnLoad`, which the AudioRecord backend (`audio_capture_audiorecord.cpp`) defines so the runtime can hand the bridge its `JavaVM`. The lifecycle and grammar calls return a `VoskBridgeError` (`0` = OK); the push, status, result, and position calls return values of their own, as each row notes.

| Symbol | Purpose |
|--------|---------|
| `int vosk_bridge_init(const char* model_path, float sample_rate, float mic_gain_target_db)` | Heavyweight: loads the VOSK model and builds the pipeline. |
| `void vosk_bridge_destroy()` | Tears the model and pipeline down. |
| `int vosk_bridge_start()` | Starts a capture-mode session — the compiled capture backend feeds the ring buffer. |
| `int vosk_bridge_start_push()` | Starts a push-mode session — no capture backend; the caller supplies audio. Same return codes as `vosk_bridge_start`. |
| `void vosk_bridge_stop()` | Stops the session and joins the recognition thread. The model stays loaded. |
| `int vosk_bridge_reset()` | Clears recogniser state, restarting a running session in the mode it was already in. |
| `int vosk_bridge_set_grammar(const char* grammar_json)` | Rebuilds the recogniser with a VOSK grammar (empty or `NULL` = free dictation). Rejected while running. |
| `int vosk_bridge_push_audio(const float* samples, uint32_t count)` | Push mode only: writes pre-DSP 48 kHz mono float samples. Returns the sample count written (`0..count`), or a **negative** `VoskBridgeError` on misuse. |
| `float vosk_bridge_get_input_level()` | Rolling ~300 ms RMS of pre-DSP audio, linear `0..1`; `0` when not running. |
| `int vosk_bridge_has_result()` | Non-zero when a transcript is queued. **Exported but unused** — the managed layer polls `vosk_bridge_get_result` directly and never binds this symbol. |
| `const char* vosk_bridge_get_result(int* out_is_final, int* out_length)` | Pops the next queued transcript JSON, or returns `NULL` when the queue is empty. The pointer is valid until the next call. |
| `void vosk_bridge_get_result_position(int64_t* out_consumed, int64_t* out_origin)` | Position of the result `vosk_bridge_get_result` last returned: the session's input samples (pre-DSP 48 kHz) consumed when it was queued, and its word-time origin — the decoder samples fed since the recogniser was created (by `vosk_bridge_init` or `vosk_bridge_set_grammar`), counted at session start. Fills each non-null out-param; both are `0` before the first result. Out-params, so the transcript JSON stays VOSK's verbatim. **Exported but unused** — the managed layer never binds this symbol; the desktop harness reads it for its `--dump` output. |
| `int64_t vosk_bridge_get_consumed(void)` | Input samples (pre-DSP 48 kHz) the recognition thread has consumed this session; zeroed at each start, held after `vosk_bridge_stop` until the next start. **Exported but unused** — the managed layer never binds this symbol; the desktop harness polls it under `--dump` to drain each case before stopping. |
| `int vosk_bridge_get_frontend_stats(uint64_t* out_input_samples, uint64_t* out_output_samples, uint64_t* out_digest, float* out_gain_db, float* out_pre_rms, float* out_post_rms)` | The [front end's](#audio-front-end) stats as the recognition thread last published them (after every chunk, and at init, start, reset, and destroy): input samples consumed and int16 samples emitted since the last reset or stream start, the 64-bit FNV-1a digest of those samples, the applied gain in dB, and the last 10 ms frame's pre- and post-gain RMS. Fills each non-null out-param; returns `VOSK_BRIDGE_ERR_NOT_INITIALISED` before init. **Exported but unused** — the managed layer never binds this symbol; the desktop harness reads it for its per-fixture `frontEndSamples` and `frontEndDigest` pins and its front-end lifecycle check. |
| `int vosk_bridge_is_running()` | Session state. Process-wide, not per-instance. |
| `int vosk_bridge_is_initialised()` | Model/pipeline state. Process-wide, not per-instance. |
| `int vosk_bridge_get_error(char* buf, int buf_size)` | Copies the last error message into `buf` and returns the number of bytes written. |

---

## Source Files

The native source is in `NativeBridge~/` (excluded from Unity import by the `~` suffix). Paths below are relative to `NativeBridge~/src/`, except `cmake/`, `tools/` and `harness/`, which sit at the top of `NativeBridge~/`. `vosk_bridge.cpp` owns one [front-end](#audio-front-end) unit and feeds its int16 output to VOSK:

| File | Description |
|------|-------------|
| `vosk_bridge.h` | The C ABI — exported `vosk_bridge_*` entry points and the `VoskBridgeError` codes (see above). |
| `vosk_bridge.cpp` | Main bridge: capture/push session lifecycle, the ring-buffer reads into the front end and its `BeginStream`/`Reset` calls, the published front-end stats snapshot, the input-level RMS, the sample counters behind result positions, and the VOSK recognition thread. Feeds the front end's int16 samples to VOSK on a dedicated native thread. |
| `frontend/front_end.h`, `frontend/front_end.cpp` | `voxr::FrontEnd`: frame buffering, the AGC2 chain per 10 ms frame, the clamped int16 conversion, stats and digest, `BeginStream` and `Reset`. |
| `frontend/decimator.h`, `frontend/decimator.cpp` | `voxr::Decimator`: the 48 kHz -> 16 kHz FIR low pass evaluated at every third input over a mirrored history. |
| `frontend/decimator_taps.h` | The generated 219-tap table and its design parameters. Written by `tools/design_decimator.py`; regenerate, do not edit. |
| `frontend/voxr_frontend.h`, `frontend/voxr_frontend.cpp` | The front end's C ABI (`voxr_fe_*`), built only into the Windows `voxr-frontend.dll`. |
| `cmake/webrtc_agc2.cmake` | Checks and extracts the pinned `webrtc-audio-processing` and abseil archives and builds the AGC2 subset (`webrtc_agc2`). |
| `tools/design_decimator.py` | Designs the decimator's Kaiser low pass and writes `decimator_taps.h` (`--design f1` is the default). |
| `ring_buffer.h` | Lock-free single-producer/single-consumer ring buffer between the audio producer (capture callback or pushed audio) and the recognition thread. |
| `result_queue.h` | Mutex-guarded transcript queue: the recognition thread pushes, C# pops once per frame. Each entry carries its session position — input samples consumed when it was queued, and the word-time origin — read back through `vosk_bridge_get_result_position`. |
| `logging.h` | `LOGI`/`LOGE` macros — `android/log` on Android, `stderr` on desktop. |
| `audio_capture.h` | Backend-neutral include — forwards to the backend selected by `VOSK_BRIDGE_CAPTURE`. |
| `audio_capture_audiorecord.cpp` | Java `AudioRecord` JNI backend. Active capture implementation on Android. Routes to the headset microphone on Quest devices via the `VOICE_RECOGNITION` audio source. |
| `audio_capture_stub.cpp` | No-op backend for desktop builds: `Start` succeeds without producing samples, so capture-mode calls are safe on platforms without a capture path. |
| `audio_capture_aaudio.cpp` | AAudio backend. Retained for reference but **not compiled** in the current build. |
| `harness/` | Desktop WAV→transcript regression harness (see above), and `fe-probe`, the front end's measurement tool ([Build](#build)). Never ships — `NativeBridge~/` is stripped from the published package. |

---

## AAudio on Quest 3

The AAudio backend (`audio_capture_aaudio.cpp`) is retained in the source tree but is not used. AAudio input delivers silence on Quest 3 -- the `AAudioStream` opens and starts without error and callbacks fire on schedule, but the audio buffer contains near-zero samples regardless of input preset (`GENERIC`, `VOICE_RECOGNITION`, `UNPROCESSED`).

The shipped build uses Java `AudioRecord` via JNI instead, which routes correctly to the headset microphone. If porting to another Android device, test AAudio first -- it may work outside Quest 3. Do not remove the AAudio files; they serve as a reference and fallback seed for future device ports.

---

## See Also

- [Getting Started](getting-started.md) -- Installation and model setup
- [Troubleshooting](troubleshooting.md) -- Common issues on Quest and in the Editor
- [Known Limitations](../KNOWN_LIMITATIONS.md) -- Hardware audio constraints including AAudio silence and the `vosk_recognizer_accept_waveform_f` bug
