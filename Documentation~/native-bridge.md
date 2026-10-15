# Native Bridge

The SDK includes a prebuilt `libvosk-bridge.so` for Android arm64. This guide covers building the native bridge from source, which is only necessary if you need to modify the C++ layer.

The bridge has two operating modes: **capture mode** (`vosk_bridge_start`), where a platform capture backend feeds the microphone into the recognition pipeline, and **push mode** (`vosk_bridge_start_push`), where the caller supplies pre-DSP 48 kHz mono float audio via `vosk_bridge_push_audio` — used by the automated-verification tiers to replay recorded fixtures through the real pipeline (ring buffer → 48→16 kHz downsampler → AGC → int16 → VOSK) without a microphone. Pushed writes are clamped to the free ring space and `vosk_bridge_push_audio` returns the count it actually wrote, so a short return means the ring is full — let the recognition thread drain it and retry the remainder; pushed audio is never overwritten (a capture backend, by contrast, overwrites the oldest samples and raises an overflow). The two modes are mutually exclusive per session. A read-only input-level getter (`vosk_bridge_get_input_level`, ~300 ms rolling RMS of pre-DSP audio) supports mic-liveness checks — it is the device-side source of the public `VoxrSpeechRecogniser.InputLevel`, with the Editor backend computing the same quantity in C# so both platforms report one contract. Push mode is not game-facing API: the C# layer binds `vosk_bridge_start_push` and `vosk_bridge_push_audio` (`BridgeNative`) but nothing in the runtime calls them; its only consumer is the native desktop harness, which calls the C entry points directly.

---

## Prerequisites

- **Android NDK** -- bundled with Unity 6 or standalone r26+
- **CMake 3.18+** -- bundled with Unity's Android toolchain or standalone
- **Ninja build system** -- bundled with Unity's Android toolchain or standalone
- **`libvosk.so` for Android arm64** -- download from [VOSK releases](https://github.com/alphacep/vosk-api/releases)

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

`NativeBridge~/CMakePresets.json` defines one preset, `windows-x64` (CMake's "Visual Studio 17 2022" generator, x64, stub backend, Release, output in `NativeBridge~/build-windows/`). It builds the bridge with MSVC from Visual Studio 2022 Community — no NDK, no JNI — against Alphacephei's pinned Windows prebuilt `libvosk` 0.3.45 (`libvosk.dll` and its import library `libvosk.lib`, provisioned under `NativeBridge~/vendor/vosk-win64-0.3.45/`), the same `libvosk.dll` the Editor ships in `Runtime/Plugins/x86_64/`. With it comes a CLI harness (`NativeBridge~/harness/`) that replays the committed fixture corpus (`Tests~/Fixtures/audio/tts/`) through the real bridge in push mode and compares final transcripts against a committed baseline (`harness/expectations.json`, which pins the grammar, AGC gain, and libvosk version it was decoded under). Configure, build, and run are one command in Git Bash on the Windows host, driven by the cmake 3.22.1 that Unity 6000.3.7f1's Android SDK ships, so bridge-logic changes are verifiable in seconds; it deliberately cannot validate the arm64 binary, JNI capture, or Quest audio routing (the on-device tier owns those). Provisioning, the build-and-run command, and re-baselining are documented in `NativeBridge~/harness/README.md`. Desktop-only: the Windows build exists for the harness, `Runtime/Plugins/x86_64/` ships no bridge DLL, and the Windows Editor keeps its own managed pipeline over `libvosk.dll` and never loads `libvosk-bridge`.

How the Windows build holds together, and why:

- **The Visual Studio generator, not Ninja.** The generator locates the Visual Studio 2022 installation itself and drives MSBuild, so no compiler-environment script (`vcvars64.bat`) runs and the whole command stays plain Git Bash. The command pins the installation with `CMAKE_GENERATOR_INSTANCE`, and the "Visual Studio 17 2022" generator selects only a Visual Studio 2022 instance, so a missing installation fails instead of falling back to another. The MSVC toolset is the generator's default, not pinned: a Visual Studio update can move it, and the baseline judges any effect.
- **Release lives in the build preset**, because `cmake --build` on a Visual Studio tree builds Debug when no configuration is given.
- **No argument Git Bash would rewrite.** Nothing passed to a Windows program starts with `/` (paths are `C:/…` or relative, and `/utf-8` reaches the compiler through `CMakeLists.txt`), so the command behaves the same with or without `MSYS_NO_PATHCONV`.
- **The vendor is checked at configure time.** On Windows, configure fails naming the first missing file among `libvosk.lib`, `libvosk.dll`, and the three MinGW runtime DLLs (`libstdc++-6.dll`, `libgcc_s_seh-1.dll`, `libwinpthread-1.dll`) in `VOSK_LIB_DIR`; nothing else is searched, so no other `libvosk` can be linked in its place.
- **One run directory.** `vosk-bridge.dll` and `vosk-bridge-harness.exe` land together in `build-windows/bin/`, and configure copies the four vendor DLLs beside them, so the loader takes them from the executable's directory before any `PATH` entry — Git for Windows puts its own `libgcc_s_seh-1.dll` on Git Bash's `PATH`.
- **`/utf-8` under MSVC**, so the sources' UTF-8 punctuation compiles the same whatever the host's code page.
- **The GNU link flags** (`-Wl,--no-undefined -Wl,--strip-all`) apply only off MSVC, which rejects them.
- **The pinned zip is hash-checked and extracted as-is.** The README checks `vosk-win64-0.3.45.zip`'s SHA-256 before extracting and stops on a mismatch; the version in the directory name makes a re-pin a visible preset edit.
- **The baseline is not re-pinned for a Windows divergence.** Re-baselining follows a corpus regeneration or a libvosk bump; a transcript mismatch on the Windows build (exit 1) stops for a ruling instead.

---

## ABI Exports

The complete C ABI is declared in `NativeBridge~/src/vosk_bridge.h`. All symbols are `extern "C"` and marked `VOSK_BRIDGE_EXPORT`: default visibility on every non-Windows target (the Android build), `__declspec(dllexport)` when MSVC compiles the bridge DLL itself (CMake defines `vosk_bridge_EXPORTS` for the `vosk-bridge` target), and `__declspec(dllimport)` for a consumer such as the harness — MSVC emits no import library for a DLL with no exports, so the harness could not link without it. The target's visibility presets are `hidden`, so nothing but the C ABI is exported. The lifecycle and grammar calls return a `VoskBridgeError` (`0` = OK); the push, status, result, and position calls return values of their own, as each row notes.

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
| `int vosk_bridge_is_running()` | Session state. Process-wide, not per-instance. |
| `int vosk_bridge_is_initialised()` | Model/pipeline state. Process-wide, not per-instance. |
| `int vosk_bridge_get_error(char* buf, int buf_size)` | Copies the last error message into `buf` and returns the number of bytes written. |

---

## Source Files

The native bridge source is in `NativeBridge~/` (excluded from Unity import by the `~` suffix). The DSP stages are header-only classes; `vosk_bridge.cpp` owns instances of them and orchestrates the chain:

| File | Description |
|------|-------------|
| `vosk_bridge.h` | The C ABI — exported `vosk_bridge_*` entry points and the `VoskBridgeError` codes (see above). |
| `vosk_bridge.cpp` | Main bridge: capture/push session lifecycle, the DSP chain (ring buffer -> downsampler -> AGC), float-to-int16 conversion, the input-level RMS, the sample counters behind result positions, and the VOSK recognition thread. Feeds int16 samples to VOSK on a dedicated native thread. |
| `agc.h` | Adaptive AGC with soft saturation: asymmetric attack/release level tracking, gain interpolated per sample toward the configured target dBFS, and a `tanh` limiter in place of hard clipping. |
| `downsampler.h` | FIR decimator, 48 kHz -> 16 kHz: a 15-tap windowed-sinc low pass (~7.5 kHz cutoff) to anti-alias before decimating by 3. |
| `ring_buffer.h` | Lock-free single-producer/single-consumer ring buffer between the audio producer (capture callback or pushed audio) and the recognition thread. |
| `result_queue.h` | Mutex-guarded transcript queue: the recognition thread pushes, C# pops once per frame. Each entry carries its session position — input samples consumed when it was queued, and the word-time origin — read back through `vosk_bridge_get_result_position`. |
| `logging.h` | `LOGI`/`LOGE` macros — `android/log` on Android, `stderr` on desktop. |
| `audio_capture.h` | Backend-neutral include — forwards to the backend selected by `VOSK_BRIDGE_CAPTURE`. |
| `audio_capture_audiorecord.cpp` | Java `AudioRecord` JNI backend. Active capture implementation on Android. Routes to the headset microphone on Quest devices via the `VOICE_RECOGNITION` audio source. |
| `audio_capture_stub.cpp` | No-op backend for desktop builds: `Start` succeeds without producing samples, so capture-mode calls are safe on platforms without a capture path. |
| `audio_capture_aaudio.cpp` | AAudio backend. Retained for reference but **not compiled** in the current build. |
| `harness/` | Desktop WAV→transcript regression harness (see above). Never ships — `NativeBridge~/` is stripped from the published package. |

---

## AAudio on Quest 3

The AAudio backend (`audio_capture_aaudio.cpp`) is retained in the source tree but is not used. AAudio input delivers silence on Quest 3 -- the `AAudioStream` opens and starts without error and callbacks fire on schedule, but the audio buffer contains near-zero samples regardless of input preset (`GENERIC`, `VOICE_RECOGNITION`, `UNPROCESSED`).

The shipped build uses Java `AudioRecord` via JNI instead, which routes correctly to the headset microphone. If porting to another Android device, test AAudio first -- it may work outside Quest 3. Do not remove the AAudio files; they serve as a reference and fallback seed for future device ports.

---

## See Also

- [Getting Started](getting-started.md) -- Installation and model setup
- [Troubleshooting](troubleshooting.md) -- Common issues on Quest and in the Editor
- [Known Limitations](../KNOWN_LIMITATIONS.md) -- Hardware audio constraints including AAudio silence and the `vosk_recognizer_accept_waveform_f` bug
