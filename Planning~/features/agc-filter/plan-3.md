# Phase 3 — persisted plan (2026-11-06)

Scope: the row — the unit built (both builds; `voxr-frontend.dll` on MSVC) and measured by `fe-probe`, with `doc-writer`'s two clauses; not wired: `vosk_bridge.*`, `downsampler.h`, `agc.h`, `main.cpp`, `expectations.json`, C# and the committed `.so` untouched. Serial, this tree. NB = `NativeBridge~/`, FE = NB`src/frontend/`, MOD = NB`cmake/webrtc_agc2.cmake`, P = `Planning~/features/agc-filter/probe/`, S = `.scratch/agc-filter/` (ignored), R = register bullet.

## Files to change

| Path | Change | Why |
|---|---|---|
| NB`tools/design_decimator.py`; FE`decimator_taps.h` | create; generated | F1, F17 |
| FE`decimator.h/.cpp`, `front_end.h/.cpp` | create | F1, F4, F5, F11, F17 |
| FE`voxr_frontend.h/.cpp` | create | R *C ABI* |
| MOD; NB`CMakeLists.txt` | create from P`cmake/agc2_subset.cmake`; modify | F3 |
| NB`harness/CMakeLists.txt`; NB`harness/fe_probe.cpp` | modify; create | F1, F4-F6, F11; §6 |
| S`fe-probe.json`, S`refusal/` | untracked | evidence |
| `.claude/bindings/unity.md` :8, :12 | modify (`doc-writer`; the rest `code-writer`) | row |

## Contracts and interfaces

New files: header block only; CRLF in NB (as NB, read by bytes; the generator writes `\r\n`); edited files keep theirs. No agent commits.

**Step 1: baseline, before any edit.** Command 3 verbatim (arm64 half: recipe :55-63): exit 0, 52/52; record `sha256sum NativeBridge~/build/libvosk-bridge.so`. Not green: BLOCKED.

**Step 2: generator.** numpy, the fixtures venv (`generate.sh` :22-24); writes FE`decimator_taps.h`. `--design r4` (default): `numpy.kaiser(95, 8.5)` × ideal sinc at 7800 Hz, fs 48000. `--design f1`: Kaiser's formulas, edges 7600/8400 Hz, A = 60 dB: cutoff their midpoint, β = 0.1102(A - 8.7), N the least odd >= (A - 7.95)/(2.285·Δω) + 1, Δω = 2π·800/48000. Sum 1 in double; 9-digit float literals; `namespace voxr` `constexpr` `kDecimatorTapCount`, `kDecimatorTaps[]`, design name, cutoff, β, rate. Check: rerun, `cmp` identical.

**Step 3: MOD.** P's :8-175, except: vendor default `${CMAKE_CURRENT_LIST_DIR}/../vendor`; `enable_language(C)` first (`pffft.c`); :47-49's visibility variables set only around abseil's `add_subdirectory`, unset after; `webrtc_agc2` hidden visibility and inlines, Clang `-ffp-contract=off`. All else as P (R *Pinned source*, *Symbols* amendments; `--exclude-libs` is Phase 4's).

**Step 4: root CMake.** After `project()`: `CMAKE_MSVC_RUNTIME_LIBRARY` `MultiThreaded$<$<CONFIG:Debug>:Debug>` (R *Symbols and runtime*). Between :44 and :46: the include; `voxr-frontend-core` STATIC, in ALL, PUBLIC include FE, PRIVATE `webrtc_agc2`, PIC, hidden, Clang `-ffp-contract=off`; `if(MSVC)` `voxr-frontend` SHARED, the shim + core. `vosk-bridge` untouched.

**Step 5: the unit.** `voxr::Decimator`: `int Process(const float*, int n, float* out)`, `Reset()`; mirrored 2N window; float dot product in tap order at every third input, the first after the third. `voxr::FrontEnd` (WebRTC types forward-declared): `FrontEnd(float target_db)`; `int Process(const float* in48k, int n, int16_t* out16k, int cap)` -> samples written, whole frames (R *Frame buffering*); -1, state unchanged, on a null, n < 0 or `cap` short of this call's frames; `BeginStream()`, `Reset()`; `Stats GetStats()` {`uint64_t input_samples, output_samples, digest; float gain_db, pre_rms, post_rms`}; `float SpeechProbability()` (R16); `const Configuration& Config()` (R *Configuration*; P`src/agc2_chain.cpp` :40-49). Per frame: P :91-142's order and readout, x64 pin :51-57; then `FloatS16ToInt16` (inline, public): clamp to int16, round half away from zero, NaN -> 0 (F5); bytes low-first into 64-bit FNV-1a (R *Parity*). RMS on ±1.0: pre before AGC2, post after the limiter. `BeginStream`, `Reset`: R *Lifecycle* (stats: counts, digest, RMS; `gain_db` is AGC2's). `Reset` (amended R *Lifecycle*, *C ABI*): `gain_db` 15; rebuild as P :73-82 `VoiceActivityDetectorWrapper`, `NoiseLevelEstimator`, `AdaptiveDigitalGainController`; `Reset()` `SpeechLevelEstimator`, `SaturationProtector`, `Limiter` (2.1 headers :43, :35, :48); keep `GainApplier` (no reset; factor 1, never set), the dumper (dump off, P cmake :137). `Process` allocates nothing.

**C ABI.** `voxr_frontend.h`, `extern "C"`, `__cdecl`, dllexport on `voxr_frontend_EXPORTS` as `vosk_bridge.h` :6-14: `int32_t voxr_fe_abi_version(void)` -> 1; `void* voxr_fe_create(float)`, null on failure, no exception crosses; `int32_t voxr_fe_process(void*, const float*, int32_t, int16_t*, int32_t)`; `void voxr_fe_begin_stream|reset|destroy(void*)`; `int32_t voxr_fe_get_stats(void*, voxr_fe_stats*)`, -1 on null; `voxr_fe_stats` = `Stats` in order, `static_assert` 40 bytes.

**Step 6: `fe-probe`.** `add_executable(fe-probe fe_probe.cpp wav_reader.cpp)`, include `third_party`, links only the core; NB`harness/CMakeLists.txt`'s header Owns/Depends add it and the core. `--fixtures <dir> --out <file>`; prints and writes the `ordered_json` report; exit 0 all bars pass, 1 one fails, 2 usage or WAV error. Sections `config`, `f1`, `f4`, `f5`, `f6`, `f11`, `alloc`, `cost`, `latency`, `pass`. A replaced global `operator new` counts. F6 feeds 480 a call, stats per frame. Bars:
- F1 (REQ :68): table response in double, 10 Hz grid: DC ±0.1 dB, >= -1 dB to 7600 Hz, <= -50 dB from 8400 Hz; sweep through a fresh `Decimator`: 0.5-amplitude 1 s sines every 100 Hz, 100-7600 and 8400-23900 Hz bar 16000 (aliases to DC), first N/3 outputs dropped, same bars.
- F4: `Config()` at -18 equals R *Configuration*. F11: headroom 18 at -18, 12 at -12; lead A's output RMS at both reported (Phase 5's bar).
- F5: `FloatS16ToInt16` over ±4·32768 step 0.25 non-decreasing, saturated; 1 kHz sines at 1.0 and 4.0, 2 s: no adjacent output pair beyond ±16384 with opposite signs.
- F6 (REQ :73): every bar compares gain and RMS in dB, each rounded to 0.01 dB (measurement resolution, not slack in the relation). Lead A = `approach_target_alpha_one`, `switch_to_navigation`, `enable_all`, `resume_fire`; lead B = `cease_fire` ×4. Per lead, cold: lead, 3 s of digital silence or of -60 dBFS noise, burst. W = the lead's last 50 frames with speech probability >= 0.95 (all, where fewer); valid only with >= 12 consecutive such frames; an invalid lead fails its bars (exit 1), never skipped or passed vacuously. Burst = 200 ms seeded white noise at W's input RMS. Bars: max gain over pause and burst <= max gain over W; burst output RMS <= W's. Noise only: 10 s -60 dBFS, cold, every gain reading <= the first.
- alloc: 0 in `Process`. cost: µs per frame, mean, max, no bar. latency: (N - 1)/2 inputs + the longest emit-free run of one-sample calls plus one, at 48 kHz, <= 15 ms.

**Step 7: Command 3** verbatim: 52/52; NB`build/libvoxr-frontend-core.a` exists; bridge hash = Step 1's; `git status --short -- Runtime/` empty. dumpbin (`VC/Tools/MSVC/*/bin/Hostx64/x64/`): `/exports voxr-frontend.dll` exactly the seven; `/dependents` of it, `vosk-bridge.dll`, the harness, `fe-probe.exe`: no `MSVCP*`, `VCRUNTIME*`, `api-ms-win-crt-*`.

**Step 8: evidence.** From NB, `./build-windows/bin/fe-probe.exe --fixtures "../Tests~/Fixtures/audio/tts" --out ../.scratch/agc-filter/fe-probe.json`: exit 0. Only F1 failing on `r4`: Step 2 `--design f1`, Steps 7-8 again; still failing, or any other bar: stop with the report, nothing retuned.

**Step 9: refusal.** Per archive, S`refusal/vendor-<id>/` holds both, that one with a byte appended; configure `-S NativeBridge~ -B` S`refusal/build-<id>`, Command 3's generator and instance, `-DAGC2_VENDOR_DIR=<drive-letter path>`: non-zero, file and both hashes named, no `_src/`. NB`vendor/` never written.

**Step 10: `doc-writer`.** `unity.md` :8's provisioning clause and :12's Prerequisites name `webrtc-audio-processing-2.1.tar.xz` and `abseil-cpp-20240722.0.tar.gz` in `NativeBridge~/vendor/`; nothing else.

**Gate.** Steps 7-9's checks; MOD lists no input-volume, noise-suppression or echo source (F4). As-built for `architect-reconcile`: S`fe-probe.json`, its sha256, the design, Step 7's figures; no writer edits the architecture doc.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| Visibility scoped to abseil | Directory-wide (P) | bridge flags hold to Phase 4 |
| `f1` rule fixed here | Stop at a miss | R, F17 admit a clearing design |
| F6 bars over W, gain its max | 100 frames; last frame | like windows; a ramp averages |
| Burst at the lead's level | Fixed dBFS | like with like |
| -1 on short `cap` | Truncate | no sample dropped (§6) |
| Harness option | Own option | preset builds it; Command 3 never runs it |

## Acceptance criteria

1. [ ] Step 2 reproducible; parameters in the header. (F17)
2. [ ] Step 7: 52/52; arm64 core built; bridge hash unchanged; `Runtime/` clean. (F3; row)
3. [ ] Seven `voxr_fe_*` exports; no CRT dependent. (F3)
4. [ ] Step 8 exit 0, every bar passed, report in S. (F1, F4-F6, F11; §6; §10 items 5-6)
5. [ ] The table is `r4` or `f1`, named in the report. (F17)
6. [ ] Step 9: both refusals. (F3)
7. [ ] `unity.md`: the two clauses only. (row)
8. [ ] Header block only; new NB files CRLF. (conventions)
9. [ ] `git status --short -uall`: the table's tracked paths only. (row)

## Open questions that block implementation

None.

## Blast radius

- Windows `vosk-bridge.dll`, harness: `/MT` (build output).
- An unprovisioned NB configure stops at MOD.
- Command 3, `build.sh` compile WebRTC, abseil (`_src/`, `_absl/`).
- Phase 4 links the core; Phase 5 binds the ABI.

## Runtime claims

- 2: numpy repeats bit-for-bit in one venv.
- 3-4, 7: abseil and WebRTC build under `/utf-8` and `/MT`; CMake 3.22.1 takes the genex; the bridge's arm64 bytes hold; the `/MT` bridge and harness (C-only boundary) give 52/52; the DLL exports no static-library symbol.
- 5, 8: AGC2 calls no `operator new` per frame (`malloc` uncounted); both leads reach 12 confident frames; noise does not raise the gain; white noise's lower crest factor may be limited less than speech at equal input RMS (an RMS-bar failure from it stops under Step 8, nothing retuned); `r4` is expected to miss F1 (validator: ~2.77 kHz transition vs 0.8 kHz): `f1` is the expected path, `r4` run first as R *Decimator*'s F17 start.
- 9: both hashed before either extracts, as P.

Hazards: Step 1 precedes every edit; 2 precedes 5; 3-6 one block, first configured at 7; 7 precedes 8; the F1 fallback reruns 2, 7, 8; a 52/52 miss or a moved bridge hash stops; a `/MT` failure is a canon conflict with R *Symbols and runtime*, reported, no other reading taken; 10 after 8; the gate last.
