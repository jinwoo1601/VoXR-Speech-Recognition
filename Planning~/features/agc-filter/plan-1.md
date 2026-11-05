# Phase 1 — persisted plan (2026-11-06)

Scope: the row's AGC2 subset probe — README provisioning for the two archives, then a committed CMake probe at `Planning~/features/agc-filter/probe/` (P) that checks, extracts, builds and measures the subset on arm64-v8a and Windows x64 into P`/report.md`; Command 3 once (Step 9), the row's Green. Unsplit, branch tree. V = `NativeBridge~/vendor/`.

## Files to change

| Path | Change | Owner | Why |
|---|---|---|---|
| `NativeBridge~/harness/README.md` | modify, two provisioning sections only | `doc-writer` | Assets bullet; F3 |
| P`/CMakeLists.txt` | create | `code-writer` | F3; §10 item 3 |
| P`/cmake/agc2_subset.cmake` | create; Phase 3 lifts it | `code-writer` | F3; *Pinned source* |
| P`/src/agc2_chain.h`, `.cpp` | create | `code-writer` | *AGC2 driver*; R16 |
| P`/src/keep_alive.cpp`, `probe_dll.cpp`, `driver_main.cpp` | create | `code-writer` | §6; *Symbols and runtime* |
| P`/report.md` | create | `code-writer` | §10 items 3–4; §6 |

Every build output, extraction and scratch copy goes under P`/build/` (`.gitignore:5`), no other name.

## Contracts and interfaces

**Step 0 — precondition, the human's.** Hub V holds the 2.1 release tarball and the abseil archive its `subprojects/abseil-cpp.wrap` names. Per archive the human gives the PM file name, SHA-256, source URL, and whether the release publishes a checksum (if so, that it matched); the PM puts them in both writers' briefs. Check: `sha256sum` in hub V prints them.

**Step 1 — README (`doc-writer`).** `## One-time provisioning`: new item 4 (Linux leftovers → 5, once `git grep` finds nothing citing item 4 by number, else stop for the PM): per archive `curl -LO <url>`, then `echo "<sha>  <file>" | sha256sum -c -`, must print `<file>: OK`; not extracted; a mismatch stops: delete, raise it; the published checksum noted where one exists. `## Provisioning a branch tree`: the existing block byte for byte; after it a second block, from the tree root: guards `test -d 'NativeBridge~/harness'`, both archives `test -f` in the hub, neither they nor `$V/.copying` in the tree; copy both into `$V/.copying/`, `sha256sum -c` there on both pins, `mv` into `$V/`, `rmdir`; prints `provisioned` / `NOT provisioned`. :32's not-copied sentence, quoted from the README itself, names the vosk zips; a paragraph in :38's form follows the block; :40 names item 4 and the block. Check: `git diff` within those two sections.

**Step 2 — provision this tree (`code-writer`, first).** The existing block, then the new: each prints `provisioned`; `git status --short` unchanged.

**Step 3 — check, extract.** The module: cache path `AGC2_VENDOR_DIR` (default this tree's V); `AGC2_WEBRTC_ARCHIVE`, `AGC2_WEBRTC_SHA256`, `AGC2_ABSL_ARCHIVE`, `AGC2_ABSL_SHA256` (Step 0); both hashed by `file(SHA256)` before either extracts; a mismatch: `message(FATAL_ERROR "<file>: SHA-256 <actual>, pinned <expected>")`; `file(ARCHIVE_EXTRACT)` into `${CMAKE_BINARY_DIR}/_src/` only. Check: per archive, a configure into P`/build/refusal-<webrtc|absl>` with `AGC2_VENDOR_DIR` = P`/build/corrupt-<webrtc|absl>` (absolute, drive-letter) holding both, that one with a byte appended: non-zero exit, file and both hashes named, no `_src/`. Then, configure only with Step 7's generator, `-B` P`/build/src-read`: exit 0 (pins verified), `_src/` holds both trees.

**Step 4 — read the pinned tree, into the report.** From P`/build/src-read/_src/`. (a) In `gain_controller2.cc`'s order — voice detector, speech-level and noise-level estimators, saturation protector, adaptive digital controller, limiter: header, constructor, per-frame call, frame type, sample scale. (b) Source list: the link closure from the `meson.build` files, by group. (c) Constants at `file:line`: detector threshold and confident-frame count, estimator time constants, saturation protector, limiter. (d) Gain readout: a getter, else the frame-energy ratio across the adaptive stage. (e) The speech-probability call. (f) SIMD: how 2.1 picks x64 and arm64 paths; whether one x64 path can be pinned. (g) The wrap's abseil URL, hash (against Step 0) and any source patch. **Stop for the human** — a design reopen — if composing needs `AudioBuffer`, band-split code, `audio_processing_impl`, another APM module, `GainController2` (past adr-0003's subset; the register rejects it), or an extracted-source patch.

**Step 5 — subset, chain.** Module: 4(b) as an explicit `webrtc_agc2` STATIC, defines per platform as `meson.build` sets them; abseil `add_subdirectory(… EXCLUDE_FROM_ALL)`, `ABSL_PROPAGATE_CXX_STD ON`, `BUILD_TESTING OFF`, `absl::` targets only; hidden visibility and inlines set first. `ProbeChain(float target_db)`, the register's *Configuration* values; `FrameReading Process(float* frame)`, 160 samples at 16 kHz, in place, ±1.0 (any rescale to 4(a)'s scale inside the chain); `FrameReading {speech_probability, gain_db, pre_rms, post_rms}`: RMS linear on ±1.0; x64 SIMD pinned if 4(f) allows.

**Step 6 — arm64**: recipe :51-63 flags, `-S` P, `-B` P`/build/arm64`. Three SHARED targets with the bridge's sources, capture `src/audio_capture_audiorecord.cpp` (`VOSK_BRIDGE_CAPTURE`'s Android default), defines, include dirs `src`, `include` (`CMakeLists.txt` :21-53), in place, libvosk from `Runtime/Plugins/Android/arm64-v8a/libvosk.so`, `log`, :96-110's flags and thin LTO: `bridge-today` (as shipped); `bridge-bare` (+ `-Wl,--exclude-libs,ALL`); `bridge-with` (bare + `agc2_chain` + `keep_alive.cpp`, a static object running the chain on one frame). Check: 0 errors; bytes of each and of the committed `.so`; growth = with − bare; each `.so`'s sorted `llvm-nm -D --defined-only` names (NDK) equal the committed 15 `vosk_bridge_*`.

**Step 7 — Windows x64**: `-B` P`/build/win-x64`, `-G "Visual Studio 17 2022" -A x64`, Command 3's `CMAKE_GENERATOR_INSTANCE`, `--config Release`, `CMAKE_MSVC_RUNTIME_LIBRARY` `MultiThreaded` project-wide. `probe-frontend.dll` exports only `probe_fe_create(float)`, `probe_fe_process(h, float*, FrameReading*)` (Step 5's frame), `probe_fe_destroy(h)`, cdecl. `agc2-probe-driver.exe --wav <path>`: links the DLL, compiles `NativeBridge~/harness/wav_reader.cpp` and includes `NativeBridge~/src/downsampler.h` in place; `LoadWav`'s ±1.0 floats in 160-sample frames; a JSON line per frame (`frame`, the four readings), then a summary; exit 0 all frames processed, probabilities in [0,1], gains finite; 1 otherwise; 2 unreadable WAV. Check: 0 errors; DLL bytes; `dumpbin /exports` (instance `VC/Tools/MSVC/*/bin/Hostx64/x64/`) the three only; `dumpbin /dependents` no `MSVCP*`, `VCRUNTIME*`, `api-ms-win-crt-*`; driver exit 0 on `Tests~/Fixtures/audio/tts/cease_fire.wav` and `launch_one_missiles_target_hotel_one.wav`.

**Step 8 — report.** Sections in order: Pins; Source list; Classes and APIs; Constants; Gain readout; Speech probability; SIMD; Builds (command, exit); Sizes (bytes); Export lists; Refusal; Driver (per fixture: frames, max probability, frames over threshold, gain first/min/max/last); Repairs; Deviations. Figures read off tool output.

**Step 9 — Command 3** verbatim, verification agent, this tree, after Step 2.

**Beyond repair.** A repair changes only P's flags, defines or source list, logged under Repairs. Still failing after three repairs, or fixable only by an extracted-source patch, another abseil, or a source past Step 4's stop: stop for the human with the log (adr-0001 (c), adr-0003 (b)).

**Gate.** The verification agent reruns Steps 3, 6, 7 from empty build dirs, matches the report's figures and finds Step 4's `file:line` in `src-read`.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| Second README block, `sha256sum -c` | Widen the vosk block | Its `test ! -e` refuses provisioned trees; checks the pins |
| Three bridge variants | A stub `.so` | Honest growth; today vs bare isolates `--exclude-libs` |
| Static-object keep-alive | An extra export | Export list stays the bridge's |

## Acceptance criteria

1. [ ] Step 0's values in both briefs; hub `sha256sum` matches. (F3)
2. [ ] README diff in two sections; the new block prints `provisioned` here. (Assets)
3. [ ] Both refusals: non-zero, file and hashes named, no `_src/`. (F3)
4. [ ] arm64 subset, abseil, three `.so`; Windows subset, DLL, driver: 0 errors. (F3; §10 item 3)
5. [ ] Each `.so` exports the committed 15; DLL only `probe_fe_*`; no CRT dependents. (F3)
6. [ ] Driver exit 0 on both fixtures, speech probability per frame. (F3, R16)
7. [ ] `report.md` complete, `file:line` cited; module pins equal README's. (§10 items 3–4; §6)
8. [ ] Command 3: exit 0, 37/37. (row)
9. [ ] `git status --short`: the table's paths only, nothing under P`/build/`. (row)

## Open questions that block implementation

- Step 0's assets — resolved by the human's download and the four facts.

## Blast radius

- unity.md :8, :12 cite both README sections, still vosk-only; a later phase's edit.
- The probe compiles `NativeBridge~/src/vosk_bridge.cpp`, the capture source, `include/` and `src/downsampler.h` in place; Phase 4 rewrites or deletes them, so this one-time probe will not build after it and `bridge-today` is pre-Phase-4. Nothing later builds it: Phase 3 lifts only the module and list; Phase 4's pins read 4(f).
- The register (*AGC2 driver*, *Pinned source*, *Symbols and runtime*), amended from the report after the build by `architect-reconcile`, never `code-writer`.

## Runtime claims

- 3: CMake 3.22.1 extracts both formats; `file(SHA256)` equals `sha256sum`.
- 5: abseil's CMake configures under 3.22.1 (NDK, VS); the runtime setting reaches its targets.
- 6: NDK lld takes `--exclude-libs,ALL`; the keep-alive survives LTO (with > bare); `bridge-today` (android-27) exports equal the committed.
- 7: dumpbin under that glob; no CRT dependents; 160-sample frames accepted; the detector passes threshold on some TTS frame; `_src/` paths fit MAX_PATH, else a shorter `-B` under P`/build/` (Deviations).
- 2: the new block's guards hold once the existing block has left no `$V/.copying`.

Hazards: Step 0 (human) precedes all; Step 1 precedes 2; Step 2 precedes 3 and 9; Step 3's extract precedes 4, 4 precedes 5.
