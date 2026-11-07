# AGC2 subset probe — report (agc-filter Phase 1, 2026-11-06)

All paths under `_src/` are relative to `Planning~/features/agc-filter/probe/build/src-read/_src/`. `W/` = `webrtc-audio-processing-2.1/webrtc/`; `A/` = `abseil-cpp-20240722.0/`. Every figure below was read off tool output in this tree. The build outputs under `build/` are gitignored (`.gitignore:5`).

## Pins

| Archive | SHA-256 | Module | README |
|---|---|---|---|
| `webrtc-audio-processing-2.1.tar.xz` (604,192 bytes) | `ae9302824b2038d394f10213cab05312c564a038434269f11dbf68f511f9f9fe` | `cmake/agc2_subset.cmake:10-11` | `NativeBridge~/harness/README.md:32`, `:51` |
| `abseil-cpp-20240722.0.tar.gz` (2,242,861 bytes) | `f50e5ac311a81382da7fa75b97310e4b9006474f9560ac46f54a9967f07d4ae3` | `cmake/agc2_subset.cmake:12-13` | `NativeBridge~/harness/README.md:34`, `:51` |

The module's pins equal the README's. Provisioning this tree (Step 2): the vosk block printed `provisioned`; the archives block printed `webrtc-audio-processing-2.1.tar.xz: OK`, `abseil-cpp-20240722.0.tar.gz: OK`, `provisioned`; `git status --short` stayed ` M NativeBridge~/harness/README.md` before and after.

The wrap (`webrtc-audio-processing-2.1/subprojects/abseil-cpp.wrap`): `source_url` `https://github.com/abseil/abseil-cpp/releases/download/20240722.0/abseil-cpp-20240722.0.tar.gz` (:3), `source_hash` `f50e5ac3…4ae3` (:5), which equals the Step 0 pin. The wrap's patch, `abseil-cpp_20240722.0-3_patch.zip` (:6-8), adds Meson build files only. It is not used, because abseil builds with its own CMake. The 2.1 tarball's `patches/` directory holds the upstream patches already applied to its WebRTC import. The probe applies no patch to any extracted source.

## Source list

This is the `webrtc_agc2` STATIC link closure, `cmake/agc2_subset.cmake` `AGC2_SOURCES_*`, grouped by the `meson.build` that lists each file. It was composed from the composed classes' includes, and the remaining entries came from unresolved externals at the first MSVC links (see Repairs). The full link succeeds with this list, but nothing shows that the list is minimal.

- **agc2** (`W/modules/audio_processing/meson.build`): `agc2/adaptive_digital_gain_controller.cc` :70, `agc2/biquad_filter.cc` :72, `agc2/cpu_features.cc` :76, `agc2/fixed_digital_level_estimator.cc` :77, `agc2/gain_applier.cc` :78, `agc2/interpolated_gain_curve.cc` :81, `agc2/limiter.cc` :82, `agc2/noise_level_estimator.cc` :84, `agc2/rnn_vad/{auto_correlation, features_extraction, lp_residual, pitch_search, pitch_search_internal, rnn, rnn_fc, rnn_gru, spectral_features, spectral_features_internal}.cc` :85-94, `agc2/saturation_protector.cc` :95, `agc2/saturation_protector_buffer.cc` :96, `agc2/speech_level_estimator.cc` :97, `agc2/vad_wrapper.cc` :99.
- **APM support** (same file): `logging/apm_data_dumper.cc` :116 (the AGC2 constructors take an `ApmDataDumper*`), `utility/pffft_wrapper.cc` :137 (used by `rnn_vad/auto_correlation.h` and `spectral_features.h`).
- **common_audio** (`W/common_audio/meson.build`): `audio_util.cc` :3, `resampler/push_resampler.cc` :7, `resampler/push_sinc_resampler.cc` :8, `resampler/sinc_resampler.cc` :10. x64 only: `resampler/sinc_resampler_sse.cc` :64 and `resampler/sinc_resampler_avx2.cc` :77 (`/arch:AVX2`). arm64 only: `resampler/sinc_resampler_neon.cc` :118.
- **x64 only, APM** (`W/modules/audio_processing/meson.build`): `agc2/rnn_vad/vector_math_avx2.cc` :163 (`/arch:AVX2`).
- **rtc_base** (`W/rtc_base/meson.build`): `checks.cc` :2, `logging.cc` :7, `memory/aligned_malloc.cc` :8, `platform_thread_types.cc` :10, `string_encode.cc` :13, `string_utils.cc` :15, `strings/string_builder.cc` :16, `system_time.cc` :20, `time_utils.cc` :21.
- **system_wrappers** (`W/system_wrappers/meson.build`): `source/cpu_features.cc` :2, `source/metrics.cc` :5.
- **third_party**: `pffft/src/pffft.c` (`W/third_party/pffft/meson.build:2`, with `_GNU_SOURCE` from :5), `rnnoise/src/rnn_vad_weights.cc` (`W/third_party/rnnoise/meson.build:2`).
- **abseil**: `add_subdirectory(… EXCLUDE_FROM_ALL)`, linked through `absl::strings` only. Libraries built: `absl_base`, `absl_log_severity`, `absl_raw_logging_internal`, `absl_spinlock_wait`, `absl_throw_delegate`, `absl_int128`, `absl_string_view`, `absl_strings`, `absl_strings_internal` (Windows build log :6-52; the same set under `build/arm64/_absl/`).
- **System libraries**: `log` on Android (`meson.build:84`) and `winmm` on Windows (`meson.build:105`).

The list compiles nothing past adr-0003's subset. It does not include `AudioBuffer` (`audio_buffer.cc`), band-split code (`splitting_filter.cc`, `three_band_filter_bank.cc`), `audio_processing_impl.cc`, `gain_controller2.cc`, any other APM module (AEC3, AECM, AGC1, NS, HPF, the standalone VAD), `input_volume_controller.cc`, `clipping_predictor*.cc`, `limiter_db_gain_curve.cc`, `speech_probability_buffer.cc`, `vector_float_frame.cc`, `agc2_testing_common.cc`, or anything from `W/api/`. From `W/api/`, only headers are used: the config struct in `api/audio/audio_processing.h` and `api/audio/audio_view.h`.

Defines come from `meson.build`. Common: `WEBRTC_LIBRARY_IMPL` :178, `_WINSOCKAPI_` :181, `NDEBUG` :182, plus `WEBRTC_APM_DEBUG_DUMP=0` (`W/modules/audio_processing/meson.build:1`). Android: `WEBRTC_ANDROID WEBRTC_LINUX` :83, `WEBRTC_POSIX` :110. arm64: `WEBRTC_ARCH_ARM64` :143, `WEBRTC_HAS_NEON` :171. Windows: `WEBRTC_WIN _WIN32` :98, plus `__STDC_FORMAT_MACROS=1 NOMINMAX _USE_MATH_DEFINES`. x64: `WEBRTC_ENABLE_AVX2` :159. `WEBRTC_ENABLE_SYMBOL_EXPORT` (:179) is omitted; see Deviations.

## Classes and APIs

`W/modules/audio_processing/gain_controller2.cc` runs the stages in this order. The frame first goes through the VAD (:188-194) and the audio levels are computed (:205). Then the noise-level estimator runs (:207-211), then the speech-level estimator (:213-220), then the saturation protector (:240-242), then the adaptive digital controller (:247-254), then the fixed gain (:259), and last the limiter (:261). The noise-level estimator runs before the speech-level estimator, and a fixed-gain stage sits before the limiter. `ProbeChain` (`src/agc2_chain.cpp`) follows this order.

| Stage | Header | Constructor (as `GainController2` builds it) | Per-frame call |
|---|---|---|---|
| Voice detector | `agc2/vad_wrapper.h:27` `VoiceActivityDetectorWrapper` | `(int vad_reset_period_ms, const AvailableCpuFeatures&, int sample_rate_hz)` :49-51; gc2 :111-112 | `float Analyze(DeinterleavedView<const float>)` :65 |
| Noise level | `agc2/noise_level_estimator.h:22` `NoiseLevelEstimator` | `CreateNoiseFloorEstimator(ApmDataDumper*)` :31-32; gc2 :125 | `float Analyze(DeinterleavedView<const float>)` :27, returns dBFS |
| Speech level | `agc2/speech_level_estimator.h:27` `SpeechLevelEstimator` | `(ApmDataDumper*, const AdaptiveDigital&, int adjacent_speech_frames_threshold)` :29-32; gc2 :108-109 | `Update(rms_dbfs, peak_dbfs, speech_probability)` :37; `level_dbfs()` :39; `is_confident()` :41 |
| Saturation protector | `agc2/saturation_protector.h:21` `SaturationProtector` | `CreateSaturationProtector(initial_headroom_db, adjacent_speech_frames_threshold, ApmDataDumper*)` :39-42; gc2 :126-128 | `Analyze(speech_probability, peak_dbfs, speech_level_dbfs)` :30-32; `HeadroomDb()` :26 |
| Adaptive digital | `agc2/adaptive_digital_gain_controller.h:26` `AdaptiveDigitalGainController` | `(ApmDataDumper*, const AdaptiveDigital&, int adjacent_speech_frames_threshold)` :39-42; gc2 :130-133 | `Process(const FrameInfo&, DeinterleavedView<float>)` :49; `FrameInfo` :29-37 |
| Fixed gain | `agc2/gain_applier.h:20` `GainApplier` | `(bool hard_clip_samples, float initial_gain_factor)` :22; gc2 :95-97 | `ApplyGain(DeinterleavedView<float>)` :24 |
| Limiter | `agc2/limiter.h:25` `Limiter` | `(ApmDataDumper*, size_t samples_per_channel, absl::string_view histogram_name_prefix)` :28-30; gc2 :98-100 | `Process(DeinterleavedView<float>)` :37; `LastAudioLevel()` :50 |

- **Frame type.** Every stage takes `webrtc::DeinterleavedView<float>` (`W/api/audio/audio_view.h:123`, constructor :130), one channel, 10 ms (`agc2/agc2_common.h:23`). At 16 kHz that is 160 samples, which is one of the sizes the adaptive controller accepts (`agc2/adaptive_digital_gain_controller.cc:131`). The limiter takes `SampleRateToDefaultChannelSize(16000)` (`W/api/audio/audio_frame.h:38`).
- **Sample scale.** All stages work in FloatS16, ±32768 (`agc2/agc2_common.h:16-18`), and levels are `FloatS16ToDbfs` (gc2 :76-77; `W/common_audio/include/audio_util.h:94`). The chain converts ±1.0 to FloatS16 with `FloatToFloatS16` (audio_util.h :65-69) and back with `FloatS16ToFloat` (:71-76).
- **Config.** `AudioProcessing::Config::GainController2::AdaptiveDigital` (`W/api/audio/audio_processing.h:360-371`) and `FixedDigital` (:376-380). The chain sets the register's *Configuration* values: enabled, `headroom_db` = −`target_db` (18), max gain 50, initial gain 15, 6 dB/s, noise ceiling −50 dBFS, fixed gain 0 dB.
- **Composition.** The chain needs none of `AudioBuffer`, band-split code, `audio_processing_impl`, another APM module, `GainController2`, or an extracted-source patch. Step 4's stop does not apply.

## Constants

| Constant | Value | Site |
|---|---|---|
| Detector confidence threshold | 0.95 | `agc2/agc2_common.h:36` `kVadConfidenceThreshold` |
| Confident-frame count | 12 | `agc2/agc2_common.h:40` `kAdjacentSpeechFramesThreshold` |
| Detector reset period | 1500 ms | `agc2/agc2_common.h:33` `kVadResetPeriodMs` |
| Detector analysis rate | 24 kHz (resamples the 16 kHz frame) | `agc2/vad_wrapper.cc:36` |
| Speech-level time to confidence | 400 ms | `agc2/agc2_common.h:44` |
| Speech-level leak factor | 1 − 1/400 | `agc2/agc2_common.h:45-46` |
| Speech-level clamp | [−90, 30] dBFS | `agc2/speech_level_estimator.cc:23` |
| Speech-level initial estimate | −20 − initial gain − headroom | `agc2/speech_level_estimator.cc:29-30` |
| Noise floor update period | 500 frames (5 s) | `agc2/noise_level_estimator.cc:68` |
| Noise floor attack | 0.5, instant decay | `agc2/noise_level_estimator.cc:56`, :61-62 |
| Noise floor minimum | −84 dBFS (energy `rate·2·2/100`) | `agc2/noise_level_estimator.cc:149-150` |
| Saturation initial headroom | 20 dB | `agc2/agc2_common.h:49` |
| Saturation peak buffer | 4 | `agc2/agc2_common.h:50` |
| Saturation super-frame | 400 ms | `agc2/saturation_protector.cc:24` |
| Saturation margin clamp | [12, 25] dB | `agc2/saturation_protector.cc:25-26` |
| Saturation attack / decay | 0.9988493699365052 / 0.9997697679981565 | `agc2/saturation_protector.cc:27-28` |
| Adaptive limiter-level threshold | −1 dBFS | `agc2/agc2_common.h:30` |
| Adaptive gain step | `max_gain_change_db_per_second · 10 / 1000` per frame; × 12 on the first confident frame | `agc2/adaptive_digital_gain_controller.cc:115-116`, :161-167 |
| Limiter max input level | 1 dBFS | `agc2/agc2_testing_common.h:31` |
| Limiter knee smoothness | 1 dB | `agc2/agc2_testing_common.h:32` |
| Limiter compression ratio | 5 | `agc2/agc2_testing_common.h:33` |
| Limiter decay | 20 ms → 0.9971259 per sub-frame | `agc2/agc2_testing_common.h:28`; `agc2/fixed_digital_level_estimator.cc:33` |
| Limiter attack | 0 (instant) | `agc2/fixed_digital_level_estimator.cc:27` |
| Limiter first-sub-frame power | 8 | `agc2/limiter.cc:34` |
| Limiter sub-frames | 20 | `agc2/agc2_common.h:24` |

## Gain readout

Upstream has no getter. The applied gain is `AdaptiveDigitalGainController::last_gain_db_`, which is private (`agc2/adaptive_digital_gain_controller.h:61`). Its public API is the constructor and `Process` (:39-49). `GainApplier::GetGainFactor()` (`agc2/gain_applier.h:26`) sits on the controller's private `gain_applier_` (:53). The readout is therefore the frame-energy ratio across the adaptive stage: `gain_db = 10·log10(E_after / E_before)` around `Process` (`src/agc2_chain.cpp`).

- When the gain changes, `GainApplier` ramps it across the frame (`agc2/gain_applier.h:41-43`), so the ratio is the frame's average gain.
- A frame with zero energy before the stage has no ratio. The chain keeps the previous reading for it, starting from the 15 dB initial gain. Both fixtures open with digital silence, so their first reading is 15.

## Speech probability

`VoiceActivityDetectorWrapper::Analyze(DeinterleavedView<const float>)` (`agc2/vad_wrapper.h:65`) returns the probability in [0, 1]. The class is public, and the subset compiles it with `rnn_vad/` and `third_party/rnnoise`, so the probability is reachable for R16. `GainController2` calls it at gc2 :193. The chain returns it per frame as `FrameReading::speech_probability`.

## SIMD

- **RNN detector.** The path follows the `AvailableCpuFeatures` passed to the wrapper (`agc2/vad_wrapper.h:49-51`, `rnn_vad/vector_math.h:46-104`). `GetAvailableCpuFeatures()` (`agc2/cpu_features.cc:42-56`) reads the CPU on x64 (SSE2/AVX2) and is fixed at compile time on NEON. `GainController2` adds field-trial kill switches (gc2 :40-52). This path can be pinned: on x64 the chain passes `AvailableCpuFeatures(sse2=true, avx2=false, neon=false)`, and on arm64 it passes `GetAvailableCpuFeatures()` (NEON, a compile-time constant).
- **16 → 24 kHz resampler inside the wrapper.** `W/common_audio/resampler/sinc_resampler.cc:125-139` picks `Convolve_AVX2` if AVX2+FMA3, else `Convolve_SSE` if SSE2, else `Convolve_C`. It reads `GetCPUInfo` at runtime on x64 and is fixed to NEON at compile time on arm64. It has no injection point, and pinning it would need a source patch. **The x64 path therefore cannot be fully pinned: the resampler is host-scoped.**
- **pffft.** SIMD is chosen at compile time (`W/third_party/pffft/meson.build:7-8`): SSE on x64, NEON on arm64.
- **meson.** `WEBRTC_ENABLE_AVX2` is set unconditionally on x86 (`meson.build:159`), and the AVX2 files compile with `/arch:AVX2` (:190). `WEBRTC_HAS_NEON` is set on aarch64 (:171).

## Builds (command, exit)

`CM` = `C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe` (3.22.1). `P` = `Planning~/features/agc-filter/probe`. All commands ran from the tree root. `VS` = `-G "Visual Studio 17 2022" -A x64 '-DCMAKE_GENERATOR_INSTANCE=C:/Program Files/Microsoft Visual Studio/2022/Community'`.

| Build | Command | Exit |
|---|---|---|
| src-read | `CM -S P -B P/build/src-read VS` | 0; `_src/` holds `webrtc-audio-processing-2.1/` and `abseil-cpp-20240722.0/` |
| refusal-webrtc | `CM -S P -B P/build/refusal-webrtc VS -DAGC2_VENDOR_DIR=D:/…/P/build/corrupt-webrtc` | 1 |
| refusal-absl | `CM -S P -B P/build/refusal-absl VS -DAGC2_VENDOR_DIR=D:/…/P/build/corrupt-absl` | 1 |
| arm64 configure | `CM -B P/build/arm64 -S P -DCMAKE_TOOLCHAIN_FILE=…/NDK/build/cmake/android.toolchain.cmake -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-27 -DANDROID_STL=c++_shared -DCMAKE_BUILD_TYPE=Release -DCMAKE_MAKE_PROGRAM=…/SDK/cmake/3.22.1/bin/ninja.exe -G Ninja` (recipe :51-63) | 0 |
| arm64 build | `CM --build P/build/arm64 --config Release -j 4` | 0, no warnings or errors in the log |
| win-x64 configure | `CM -S P -B P/build/win-x64 VS` | 0 (MSVC 19.44.35220.0, SDK 10.0.26100.0) |
| win-x64 build | `CM --build P/build/win-x64 --config Release` | 0, 0 errors; 13 warnings, all in upstream sources (C4005 ×7, C4244 ×2, C4715 ×1, C4068 ×3) |

The arm64 and win-x64 builds were rerun from empty build directories after the last source change. The figures below come from those reruns.

## Sizes (bytes)

| Artifact | Bytes |
|---|---|
| Committed `Runtime/Plugins/Android/arm64-v8a/libvosk-bridge.so` | 56,752 |
| `libbridge-today.so` | 56,752 |
| `libbridge-bare.so` | 56,752 |
| `libbridge-with.so` | 157,384 |
| **arm64 growth (with − bare)** | **100,632** |
| `probe-frontend.dll` (Windows x64, `/MT`) | 316,416 |
| `agc2-probe-driver.exe` | 179,200 |
| `libwebrtc_agc2.a` (arm64, static, not shipped) | 5,103,272 |
| `webrtc_agc2.lib` (x64, static, not shipped) | 1,024,532 |

`libbridge-today.so` matches the committed `.so` in size, but the files differ (SHA-256 `9a018459…2e27` against `33372e89…c487`). The probe builds at android-27, the recipe's level, while `build.sh:53` builds at android-29.

## Export lists

- **arm64.** For the committed `.so` and all three variants, `llvm-nm -D --defined-only -j` (NDK `toolchains/llvm/prebuilt/windows-x86_64/bin/llvm-nm.exe`) sorted, then `diff` against the committed list, showed no difference for `today`, `bare` or `with`. Each lists these 17 names: `JNI_OnLoad`, `vosk_bridge_destroy`, `vosk_bridge_get_consumed`, `vosk_bridge_get_error`, `vosk_bridge_get_input_level`, `vosk_bridge_get_result`, `vosk_bridge_get_result_position`, `vosk_bridge_has_result`, `vosk_bridge_init`, `vosk_bridge_is_initialised`, `vosk_bridge_is_running`, `vosk_bridge_push_audio`, `vosk_bridge_reset`, `vosk_bridge_set_grammar`, `vosk_bridge_start`, `vosk_bridge_start_push`, `vosk_bridge_stop`.
- **Windows exports.** `dumpbin /exports probe-frontend.dll` (`VC/Tools/MSVC/14.44.35207/bin/Hostx64/x64/dumpbin.exe`) reports 3 functions, 3 names: `probe_fe_create`, `probe_fe_destroy`, `probe_fe_process`.
- **Windows dependents.** `dumpbin /dependents` shows `WINMM.dll` and `KERNEL32.dll` for the DLL, and `probe-frontend.dll` and `KERNEL32.dll` for the driver. There is no `MSVCP*`, `VCRUNTIME*` or `api-ms-win-crt-*` dependent.

## Refusal

The corrupted copies were made under `P/build/corrupt-<id>/`: both archives copied from `NativeBridge~/vendor/`, then one byte (`x`) appended to that directory's own archive.

| Configure | Exit | Message | `_src/` |
|---|---|---|---|
| refusal-webrtc | 1 | `D:/…/P/build/corrupt-webrtc/webrtc-audio-processing-2.1.tar.xz: SHA-256 ae0d385c6197b1eda05dce031b5659c1f93a79e89dc270e2f09700986ce6fdbb, pinned ae9302824b2038d394f10213cab05312c564a038434269f11dbf68f511f9f9fe` | absent |
| refusal-absl | 1 | `D:/…/P/build/corrupt-absl/abseil-cpp-20240722.0.tar.gz: SHA-256 013b68cf51ac4afd31f91521cf08b35795a88ceede90062b8d3e1d977e94527e, pinned f50e5ac311a81382da7fa75b97310e4b9006474f9560ac46f54a9967f07d4ae3` | absent |

The module hashes both archives before it extracts either (`cmake/agc2_subset.cmake:15-25`, then :27-37). `sha256sum` on the corrupted copies printed the same `ae0d385c…` and `013b68cf…` values that `file(SHA256)` reported.

## Driver

`agc2-probe-driver.exe --wav <fixture>`, target −18 dB. The driver loads the 48 kHz WAV with `LoadWav`, downsamples it with `Downsampler` (factor 3), and splits it into 160-sample frames.

| Fixture | Exit | Frames | Remainder | Max probability | Frames ≥ 0.95 | Gain first / min / max / last (dB) |
|---|---|---|---|---|---|---|
| `Tests~/Fixtures/audio/tts/cease_fire.wav` | 0 | 207 | 126 | 0.999789 | 40 | 15 / 12.0497 / 15 / 12.0497 |
| `Tests~/Fixtures/audio/tts/launch_one_missiles_target_hotel_one.wav` | 0 | 354 | 11 | 0.999907 | 141 | 15 / 2.51714 / 15 / 2.51714 |

Each frame prints a JSON line with `frame`, `speech_probability`, `gain_db`, `pre_rms` and `post_rms`. For example, `cease_fire.wav` frame 36 printed `speech_probability` 0.977715, `pre_rms` 0.0101916 and `post_rms` 0.0573113.

## Repairs

1. **Abseil runtime.** Abseil's `CMakeLists.txt:64-71` sets `CMAKE_MSVC_RUNTIME_LIBRARY` itself: `MultiThreaded…DLL` unless `ABSL_MSVC_STATIC_RUNTIME` is on. With only the project-wide `CMAKE_MSVC_RUNTIME_LIBRARY MultiThreaded`, `absl_strings.vcxproj:126` read `MultiThreadedDLL` while `webrtc_agc2.vcxproj:123` read `MultiThreaded`. The repair is a define: `set(ABSL_MSVC_STATIC_RUNTIME ON)` in the module. After it, `absl_strings.vcxproj:126` reads `MultiThreaded`.
2. **Link-closure completion.** The first three MSVC links failed on unresolved externals, which named the missing sources. First link: `rtc::webrtc_logging_impl::Log`, `rtc::LogMessage::IsNoop`, `rtc::SimpleStringBuilder`, `webrtc::AlignedMalloc/AlignedFree`, resolved by adding `logging.cc`, `strings/string_builder.cc`, `memory/aligned_malloc.cc`. Second link: `rtc::CurrentThreadId`, `rtc::tokenize`, `rtc::ToString<long double>`, `rtc::ToHex`, `rtc::SystemTimeMillis`, `rtc::TimeDiff`, resolved by adding `platform_thread_types.cc`, `string_encode.cc`, `system_time.cc`, `time_utils.cc`. Third link: `rtc::ToHex` and `__imp_timeGetTime`, resolved by adding `string_utils.cc` and meson's `winmm` (`meson.build:105`). These are source-list additions, read from `W/rtc_base/meson.build`, while deriving 4(b)'s closure. The fourth link succeeded. The arm64 build succeeded on its first attempt with the final list.

## Deviations

- **`WEBRTC_ENABLE_SYMBOL_EXPORT` omitted** (`meson.build:179`). It turns `RTC_EXPORT` into `__declspec(dllexport)` on Windows and `visibility("default")` elsewhere (`W/rtc_base/system/rtc_export.h:19-33`). Meson sets it for its shared library. The subset is static, and the DLL and `.so` must export only their own functions. This define change was made before the first build, not in response to a failure.
- **MSVC `-msse2`** (`W/common_audio/meson.build:69-70`) is not passed. SSE2 is MSVC's x64 baseline, and cl has no `-msse2`.
- **Export count.** The plan and brief say "the committed 15 `vosk_bridge_*`". The committed `.so` exports 17 names, 16 `vosk_bridge_*` plus `JNI_OnLoad`, and all three variants equal those 17.
- **Order.** In 2.1, the noise-level estimator runs before the speech-level estimator, and a fixed-gain stage runs before the limiter (gc2 :207-220, :259). The chain follows the source.
- **Gain readout for silent frames.** The previous reading is held (see Gain readout).
- **x64 SIMD only partly pinned.** The resampler is host-scoped (see SIMD).
- **Runtime claims, each checked:**
  - Step 2: the new block's guards hold after the vosk block — **true** (`provisioned`, no `$V/.copying`). `sha256sum -c -` prints `<file>: OK` — **true**.
  - Step 3: CMake 3.22.1 `file(ARCHIVE_EXTRACT)` extracts `.tar.xz` and `.tar.gz` — **true** (both trees under `src-read/_src/`). `file(SHA256)` equals `sha256sum` — **true** (the refusal hashes match `sha256sum`, and the pins verify). `FATAL_ERROR` before extraction leaves no `_src/` — **true** (both refusal directories lack `_src/`).
  - Step 5: abseil's CMake configures under 3.22.1 for the NDK and VS — **true** (its minimum is 3.16, `A/CMakeLists.txt:19`; both configures exit 0). `CMAKE_MSVC_RUNTIME_LIBRARY` reaches abseil's targets — **false as planned**, because abseil overrides it (`A/CMakeLists.txt:64-71`). It is **true** after repair 1.
  - Step 6: NDK lld accepts `-Wl,--exclude-libs,ALL` — **true** (`bare` and `with` link, exit 0). The keep-alive survives thin LTO — **true** (`with` 157,384 > `bare` 56,752). `bridge-today` (android-27) exports equal the committed `.so`'s (android-29, `build.sh:53`) — **true** (17 names identical, same size, different bytes).
  - Step 7: `dumpbin` found under the glob — **true** (`14.44.35207`). No CRT dependents — **true**. 160-sample frames accepted — **true** (driver exit 0 on 207 and 354 frames). The detector passes the threshold on a TTS frame — **true**, including `cease_fire.wav` (peak 0.05): 40 frames ≥ 0.95, max 0.999789. `_src/` paths fit MAX_PATH — **true** as far as checked: extraction and every MSVC compile from `_src/` succeeded with no path error. The longest compiled path, `…/probe/build/win-x64/_src/webrtc-audio-processing-2.1/webrtc/modules/audio_processing/agc2/rnn_vad/spectral_features_internal.cc`, is 216 characters by count. No tool measured the longest extracted path, and no shorter `-B` was needed.
