# Phase 2 — persisted plan (2026-10-14)

**Phase 2 - the Windows build and run** (`architecture.md` `## Build plan`). Unsplit. Discharges F1, F2, F6, both §6 NFRs; F4 at `CMakePresets.json`; F8 checked. Phase 1's `vosk_bridge.h` is consumed, not edited unless a portability fix needs it.

## Files to change

| Path | Change | Why |
|---|---|---|
| `NativeBridge~/vendor/vosk-win64-0.3.45/` | create (step 1; gitignored) | F6, degraded-vendor NFR; register *The vendor layout* |
| `NativeBridge~/CMakePresets.json` | modify (whole file) | F1, F4; register *The presets* |
| `NativeBridge~/harness/expectations.json` | modify (:236) | F2; register *The `libvosk` label* |
| Conditional: repo-authored `.h`/`.cpp` under `NativeBridge~/src/`, `include/`, `harness/` (not `harness/third_party/**`); both `CMakeLists.txt` | modify, only as Step 5 allows | F1; requirements §7 |

## Contracts and interfaces

**Conventions.** Git Bash, from the project root `D:/Workspace/VoXR-Speech-Recognition`. `CMAKE_EXE='C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe'` and `T='C:/Users/excal/AppData/Local/Temp/voxr-wnh-phase-2'`, set in each Bash call using them. `<CMD>`: the command in the architecture doc's `## Data contract & runtime flow`, verbatim, cited by name; it begins `cd NativeBridge~`, so it and any copy run in a subshell `( ... )`. Step 1 precedes any edit.

**Step 1 - provision `vendor/` (F6).**
1. `sha256sum .scratch/vosk-win64-probe/vosk-win64-0.3.45.zip` = `f1dcc9cca460630f81ea8f71794f69c80bed6556d2a4e6237b5785e1d2dff34b`; `python3 -I -m zipfile -l` on it lists `vosk-win64-0.3.45/libvosk.lib`.
2. `vendor/vosk-win64-0.3.45/` must not exist; then `python3 -I -m zipfile -e .scratch/vosk-win64-probe/vosk-win64-0.3.45.zip NativeBridge~/vendor/`. Result: exactly `libvosk.dll`, `libvosk.lib`, `vosk_api.h`, `libstdc++-6.dll`, `libgcc_s_seh-1.dll`, `libwinpthread-1.dll` under it; nothing copied up a level.
3. `sha256sum` of the extracted `libvosk.dll` = `9331c2f6a32cf77141af27c9750e79532718f51bbc2e3cea3c60f14ca4251e4e` = that of `Runtime/Plugins/x86_64/libvosk.dll` (adr-0004).
4. The rest of `vendor/` untouched (the model; the Linux leftovers are the README's, Phase 3).

**Step 2 - `CMakePresets.json`, whole file rewritten**, two-space indent as today. `"version": 3`. One configure preset `windows-x64`: `displayName`/`description` naming Windows x64, MSVC (VS 2022), stub capture and the harness, pointing to `harness/README.md` for provisioning; `"generator": "Visual Studio 17 2022"`; `"architecture": { "value": "x64", "strategy": "set" }`; `"binaryDir": "${sourceDir}/build-windows"`; `cacheVariables` `VOSK_BRIDGE_CAPTURE` `stub`, `VOSK_BRIDGE_BUILD_HARNESS` `ON`, `VOSK_LIB_DIR` `${sourceDir}/vendor/vosk-win64-0.3.45`; no `CMAKE_BUILD_TYPE` or `CMAKE_CXX_COMPILER`. One build preset `{ "name": "windows-x64", "configurePreset": "windows-x64", "configuration": "Release" }`. `desktop-linux` deleted; no `wsl`, `/mnt/` or `desktop-linux` text.

**Step 3 - `expectations.json:236`**, in place, nothing reformatted: `"libvosk": "0.3.45 (Alphacephei vosk-linux-x86_64 prebuilt)"` -> `"libvosk": "0.3.45 (Alphacephei vosk-win64 prebuilt)"` (still the last key, no trailing comma).

**Step 4 - first configure, alone.** `( <CMD> )` cut after its `--preset windows-x64` configure segment, the kept text verbatim. A failure fixed purely by correcting `<CMD>`'s form (quoting, argument order) is applied and the proven form reported; the PM, never the code-writer, has the register amended, dated.

**Step 5 - build and run: `( <CMD> )`.** MSVC compile or link errors are fixed only as far as portability needs, in the conditional row's files only, each source fix behind `_WIN32`/`_MSC_VER` or a standard-C++ form both compilers accept. `NativeBridge~/CMakeLists.txt` and `NativeBridge~/harness/CMakeLists.txt` take only a portability addition inside an `if(MSVC)` or `if(WIN32)` block (e.g. a `NOMINMAX` definition), reported. Warnings are not fixed.

**Stops for the human, no workaround.** Step 1: a zip or `libvosk.dll` hash mismatch, no `libvosk.lib`, or the target dir present. Step 4: the generator cannot find or drive the VS 2022 Community instance (adr-0003's reopening condition); no Ninja, vcvars or other generator. Steps 4-5: a configure or build failure from Phase 1's `CMakeLists.txt` text (`$<1:...>` runtime dir, vendor-check loop, `file(COPY)`, imported `vosk` target) - a register decision, never patched in place. Step 5: a fix would touch DSP arithmetic (downsampler, AGC, gain, any float computation in `vosk_bridge.cpp` or its DSP helpers), change existing text in either `CMakeLists.txt`, or edit `harness/third_party/**` (a PM ruling for this plan); the harness exits 1 (never re-pin) or 2 (operational, a DLL loader failure included; never cleared by an edit). Criterion 1: a non-empty symbol diff.

**Code-writer report.** Each step's result; any proven `<CMD>` form, exact; each fix by file and reason; on a Phase 1 `CMakeLists.txt` failure, the error and a proposed minimal fix; on exit 1, the diverging fixtures and build inputs (cmake, MSVC toolset, zip hash); on exit 2, its stderr.

**Commit set:** `CMakePresets.json`, `expectations.json`, any portability-fix file. `vendor/` and `build-windows/` are gitignored (`.gitignore:42,45`). No CHANGELOG line (dev tooling only).

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| Configure alone first | `( <CMD> )` at once | Separates a route failure (stop) from a compile error (fix) |
| Negatives under `$T` via `-B` | `build-windows/` | The green build's cache stays clean |
| Empty `VOSK_LIB_DIR` by `-D` | Moving `vendor/` aside | The provisioned vendor is never disturbed |
| Fixes behind `_WIN32`/`_MSC_VER` or standard C++ | Unconditional rewrites | Android output stays as Phase 1 proved it (F3) |

## Acceptance criteria

The verification agent, at implement's gate, in this order.

1. [ ] Command 3's arm64 half: the recipe's `### Building NativeBridge` configure and build into `NativeBridge~/build/` with no errors; then, with `LLVM_NM="/c/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/NDK/toolchains/llvm/prebuilt/windows-x86_64/bin/llvm-nm.exe"`, `"$LLVM_NM" -D --defined-only NativeBridge~/build/libvosk-bridge.so | awk '{print $2, $3}' | sort > .scratch/windows-native-harness-phase-2-symbols-post.txt`, and its `diff` against `.scratch/windows-native-harness-phase-1-symbols-committed.txt` is empty. No recommit of the `.so`. (F3)
2. [ ] `( <CMD> )` in Claude Code's Git Bash, then `( unset MSYS_NO_PATHCONV; <CMD> )`, each exits 0 with 16/16. (F1, F2; F7's conversion clause)
3. [ ] `touch .scratch/windows-native-harness-phase-2-marker`; then a copy of `<CMD>` with `Community` misspelt `Comunity` in both its `ls -d` and `-DCMAKE_GENERATOR_INSTANCE` arguments, in a subshell, exits non-zero, printing `ls: cannot access` with the misspelt path; after it, `find NativeBridge~/build-windows -newer .scratch/windows-native-harness-phase-2-marker` prints nothing. (drift NFR)
4. [ ] `( cd NativeBridge~ && "$CMAKE_EXE" --preset windows-x64 -B "$T/vs2026" '-DCMAKE_GENERATOR_INSTANCE=C:/Program Files/Microsoft Visual Studio/18/Community' )` exits non-zero, printing a `CMake Error` that names that instance (the generator rejecting it; any other failure does not pass); no `.vcxproj` there (VS 2026, MSVC 14.51). (drift NFR; requirements §7)
5. [ ] With `$T/empty-vendor` created empty, `( cd NativeBridge~ && "$CMAKE_EXE" --preset windows-x64 -B "$T/empty-vendor-build" '-DCMAKE_GENERATOR_INSTANCE=C:/Program Files/Microsoft Visual Studio/2022/Community' "-DVOSK_LIB_DIR=$T/empty-vendor" )` exits non-zero, its output naming `libvosk.lib`. (vendor NFR)
6. [ ] After 4 and 5, `$T` is deleted whole.
7. [ ] `git diff main -- NativeBridge~/harness/expectations.json` shows the label line only (F2); `git diff --stat main -- Runtime/` empty (F8); `grep -inE 'wsl|/mnt/|desktop-linux' NativeBridge~/CMakePresets.json` empty (F4).

## Open questions that block implementation

None; the stops above are the human's at run time.

## Blast radius

- `desktop-linux` is gone, yet the binding's Command 3, step 2b and `harness/README.md` name it (Phase 3; unrun), as do `Documentation~/native-bridge.md:61`, `CHANGELOG.md:74`.
- `build-windows/`, the vendor dir (ignored) and the post symbol list appear; the arm64 configure takes no `--preset`.
- Out of scope: the Phase 3 sites above; recipe :3, :47, other WSL sections, Command 4, DocCheck, `generate.sh` (row 2); `build.sh`; `Runtime/**`; product docs; stale caches (`build-desktop/`, `build-1.1.0/`, `build-1.2.0/`, Phase 1's in `.scratch/`).

## Runtime claims

- Step 1: Git Bash has `sha256sum`, `python3`; `zipfile -e` recreates the zip's top-level dir.
- Step 4: cmake 3.22.1 reads the presets; its VS 17 2022 generator drives the 17.14 instance given as `C:/...`.
- Steps 4-5, criteria 2-3: Git Bash runs the quoted `C:/...` command word and passes `-D...=C:/...` intact, conversion on or off; relative arguments (`vendor/...`, `../Tests~/...`, `harness/...`) reach the exe unchanged with `MSYS_NO_PATHCONV` unset.
- Step 5: MSBuild builds Release from Git Bash's environment; `vosk_bridge_EXPORTS` reaches the bridge only (import library made); `/utf-8` applies; `$<1:...>` puts DLL and exe in `build-windows/bin/` with the four vendor DLLs, loaded first, so the exe starts (the loader finds them and `vosk-bridge.dll`), no exit 2 or loader error; MSVC compiles the sources (fixes possible); transcripts match the GCC baseline.
- Criterion 1: `llvm-nm -D --defined-only` prints `address type name`; Git Bash has `awk`, `sort`, `diff`; `NativeBridge~/build/` rebuilds incrementally with any portability fix.
- Criterion 3: `ls -d` exits non-zero when one of its two arguments is missing, before `cmake` runs; `&&` stops the chain.
- Criterion 4: `-B` overrides `binaryDir`; a version-18 instance fails the version-17 generator before any `.vcxproj`.
- Criterion 5: command-line `-DVOSK_LIB_DIR` overrides the preset's; `libvosk.lib` fails first.
