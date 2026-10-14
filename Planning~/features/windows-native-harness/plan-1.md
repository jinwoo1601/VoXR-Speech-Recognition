# Phase 1 — persisted plan (2026-10-14)

**Phase 1 — cross-compiler source, Android unchanged** (`architecture.md` `## Build plan`). Unsplit. Discharges F3; F4 at `NativeBridge~/CMakeLists.txt` and `NativeBridge~/harness/CMakeLists.txt`; F7 at the recipe's `### Building NativeBridge`; F8 checked. Every Windows change sits behind `_WIN32`, `WIN32` or `MSVC`, so only the arm64 build observes this phase, and it is proven unchanged. The desktop half is not run.

## Files to change

| Path | Change | Why |
|---|---|---|
| `NativeBridge~/src/vosk_bridge.h` | modify (:6) | F1, F3 — register *export macro* |
| `NativeBridge~/CMakeLists.txt` | modify (before :39; :50-51; :57-60; :71-75; :84) | F1, F3, F4, degraded-vendor NFR — register *source encoding*, *run-time DLL discovery*, *vendor checks*, *libvosk per platform*, *GNU link flags* |
| `NativeBridge~/harness/CMakeLists.txt` | modify (:2, comment) | F4 |
| `NativeBridge~/harness/main.cpp` | modify (:2, comment) | F4 sweep (Components: `main.cpp`) |
| `Planning~/verification-recipe.md` | modify (:53) | F7 — ruling B |

## Contracts and interfaces

Steps run in this order. Step 0 precedes any edit.

**Step 0 — baseline before any edit.**
1. Move `NativeBridge~/build/` aside, whole, to `.scratch/windows-native-harness-phase-1-stale-build/` (moved, not deleted; the destination must not exist first, else `mv` nests it). CMake 3.22 has no `--fresh`, so the recipe needs a fresh build dir. The moved dir stays in `.scratch/` (ignored); this phase neither restores nor deletes it.
2. Build the arm64 half from the unchanged tree with the recipe's `### Building NativeBridge` commands (configure, then build, into `NativeBridge~/build/`), with `CMAKE` set to step 4's Git Bash form instead of the recipe's current `/mnt/c/` line.
3. With `LLVM_NM="/c/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/NDK/toolchains/llvm/prebuilt/windows-x86_64/bin/llvm-nm.exe"` (confirmed by the PM, 2026-10-14: it listed 15 defined dynamic symbols on the committed `.so`), write:
   - `"$LLVM_NM" -D --defined-only NativeBridge~/build/libvosk-bridge.so | awk '{print $2, $3}' | sort > .scratch/windows-native-harness-phase-1-symbols-baseline.txt`
   - `"$LLVM_NM" -D --defined-only Runtime/Plugins/Android/arm64-v8a/libvosk-bridge.so | awk '{print $2, $3}' | sort > .scratch/windows-native-harness-phase-1-symbols-committed.txt`
4. `diff` the two files. Non-empty: stop for the human before any edit.

**Step 1 — `NativeBridge~/src/vosk_bridge.h:6`.** Replace the single `#define` with a three-arm block, lines in this order: `#if defined(_WIN32)`; `#if defined(vosk_bridge_EXPORTS)`; `#define VOSK_BRIDGE_EXPORT __declspec(dllexport)`; `#else`; `#define VOSK_BRIDGE_EXPORT __declspec(dllimport)`; `#endif`; `#else`; the current line `#define VOSK_BRIDGE_EXPORT __attribute__((visibility("default")))`, character for character; `#endif`. The 14 use sites (:25-62) unchanged.

**Step 2 — `NativeBridge~/CMakeLists.txt`.** Comment lines: :50-51 reworded, no others added.
- Immediately before `add_library(vosk-bridge SHARED ...)` (:39): `if(MSVC)` / `add_compile_options(/utf-8)` / `endif()`; then `if(WIN32)` / `set(CMAKE_RUNTIME_OUTPUT_DIRECTORY "$<1:${CMAKE_BINARY_DIR}/bin>")` / `endif()`.
- :50 → `# libvosk — prebuilt VOSK library (libvosk.so on Android; libvosk.lib + libvosk.dll on Windows).`; :51 → `# Set VOSK_LIB_DIR to the directory containing it for the target platform.`; :52-55 unchanged.
- :57-60 becomes `if(WIN32)` … `else()` … `endif()`. The `WIN32` arm, in this order:
  1. `foreach(_vosk_file IN ITEMS libvosk.lib libvosk.dll libstdc++-6.dll libgcc_s_seh-1.dll libwinpthread-1.dll)`; inside, `if(NOT EXISTS "${VOSK_LIB_DIR}/${_vosk_file}")` → `message(FATAL_ERROR "Missing ${VOSK_LIB_DIR}/${_vosk_file} - provision NativeBridge~/vendor per harness/README.md")` (ASCII `-`, not an em dash); `endif()`; `endforeach()`.
  2. `file(COPY` the four DLLs — `"${VOSK_LIB_DIR}/libvosk.dll"`, `"${VOSK_LIB_DIR}/libstdc++-6.dll"`, `"${VOSK_LIB_DIR}/libgcc_s_seh-1.dll"`, `"${VOSK_LIB_DIR}/libwinpthread-1.dll"` — `DESTINATION "${CMAKE_BINARY_DIR}/bin")`.
  3. `add_library(vosk SHARED IMPORTED)` and `set_target_properties(vosk PROPERTIES` with `IMPORTED_IMPLIB "${VOSK_LIB_DIR}/libvosk.lib"` and `IMPORTED_LOCATION "${VOSK_LIB_DIR}/libvosk.dll"` `)`.

  The `else()` arm is :57-60's text verbatim, indented one level.
- :71-75 split: `set_target_properties(vosk-bridge PROPERTIES C_VISIBILITY_PRESET hidden CXX_VISIBILITY_PRESET hidden)` unconditional (kept as a multi-line call in today's layout); then `if(NOT MSVC)` / `set_target_properties(vosk-bridge PROPERTIES LINK_FLAGS "-Wl,--no-undefined -Wl,--strip-all")` / `endif()`. The :70 comment unchanged.
- :84 `# --- WSL harness (Tier C) ---` → `# --- Desktop harness (Tier C) ---`.

**Step 3 — `Purpose:` comments; no code change.**
- `NativeBridge~/harness/CMakeLists.txt:2` → `# Purpose:  Build the desktop WAV->transcript harness CLI (Tier C)`.
- `NativeBridge~/harness/main.cpp:2` → `// Purpose:  Desktop WAV->transcript harness — replays the fixture corpus through` (the em dash kept as today; lines 3-4 unchanged).

**Step 4 — `Planning~/verification-recipe.md:53`.** → `CMAKE="/c/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe"` (the same binary, Git Bash form). Nothing else in the recipe changes.

**Step 5 — green, by the verification agent.** Command 3's arm64 half per the recipe (step 4's text) into `NativeBridge~/build/` (the baseline's dir; the `CMakeLists.txt` change re-runs configure); then the criteria below. The rebuilt `.so` is not committed.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| Baseline the unchanged tree's symbols before editing | Diff only post-edit against the committed `.so` | Separates an existing difference (e.g. the recipe's `android-27` vs `build.sh`'s `android-29`) from this phase's |
| Move `build/` aside to `.scratch/` | Delete it | Fresh dir needed (no `--fresh`); moving loses nothing (requirements §7) |
| Symbol name + type, sorted | Raw `llvm-nm` output | Addresses are build-dependent; F3 is about the list |
| ASCII `-` in the `FATAL_ERROR` text | Em dash | Console output of the configure; no encoding dependence |
| `/c/…` Git Bash form for `CMAKE=` | `C:/…` | PM decision; a command word bash resolves, no `/mnt/` (design §1 rule 3) |
| No recommit of the `.so` | Recommit the rebuilt `.so` | No ABI change; F8 needs `Runtime/` untouched; a non-empty diff stops for the human |

## Acceptance criteria

1. [ ] Step 0's two symbol files exist and their `diff` is empty, before any edit. (F3)
2. [ ] Step 5's post-edit arm64 build (not step 0's baseline), per the recipe with step 4's `CMAKE=` line, builds `NativeBridge~/build/libvosk-bridge.so` with no errors. (F3, F7)
3. [ ] The post-edit sorted name+type list of `NativeBridge~/build/libvosk-bridge.so` (step 0's command) equals `.scratch/windows-native-harness-phase-1-symbols-committed.txt` — empty `diff`. (F3)
4. [ ] `grep -inE 'wsl|/mnt/|desktop-linux' NativeBridge~/CMakeLists.txt NativeBridge~/harness/CMakeLists.txt NativeBridge~/harness/main.cpp` finds nothing. (F4)
5. [ ] The recipe's `### Building NativeBridge` `CMAKE=` line reads exactly step 4's text. (F7)
6. [ ] `git diff --stat main -- Runtime/` is empty. (F8)
7. [ ] The phase's code change touches only the five files in the table; `NativeBridge~/src/vosk_bridge.cpp` and `NativeBridge~/build.sh` unchanged.

## Open questions that block implementation

None. A non-empty diff at step 0 or criterion 3 is a stop for the human, not an open question.

## Blast radius

- `vosk_bridge.h` is included by `src/vosk_bridge.cpp`, `src/audio_capture_stub.cpp`, `src/audio_capture_audiorecord.cpp` (arm64 default, `CMakeLists.txt:12`), `src/audio_capture_aaudio.cpp`, `harness/main.cpp`.
- `CMakeLists.txt` is shared by `build.sh` and the recipe's arm64 build; `/utf-8` and the runtime dir reach `harness/` via `add_subdirectory` (Phase 2 consumes them).
- `.claude/bindings/unity.md` Command 3 runs the arm64 half "per the recipe", so it now runs step 4's line.
- `.scratch/` gains the stale build dir and two symbol files (ignored).
- Out of scope: `CMakePresets.json`, `vendor/`, `expectations.json` (Phase 2); the binding text, `harness/README.md` (Phase 3); recipe :47 and all other recipe WSL text, Command 4, DocCheck, `generate.sh` (row 2); `build.sh`; `vosk_bridge.cpp`; `Runtime/**`; product docs.

## Runtime claims

- Step 0: Git Bash runs the `/c/…` `cmake.exe` command word, and the unchanged tree configures and builds into a fresh `NativeBridge~/build/`.
- Step 0 / criterion 3: `llvm-nm -D --defined-only` prints `address type name`, so `awk '{print $2, $3}'` yields type and name.
- Step 0: the fresh build's symbol list equals the committed `.so`'s (unknown until run; a difference stops).
- Steps 1-2: `_WIN32`, `WIN32` and `MSVC` are false under the NDK toolchain, so the Android preprocessing, link flags and libvosk import are unchanged — criterion 3 is the proof.
- Step 0: `mv` of `NativeBridge~/build/` works on Windows; Git Bash has `awk`, `sort`, `diff`; the recipe's other cmake args (`-G Ninja`, `-DCMAKE_MAKE_PROGRAM=C:/…`, `-DCMAKE_TOOLCHAIN_FILE=…`, `--build … -j 4`) reach `cmake.exe` from Git Bash unchanged.
- Step 5: the re-run configure in the existing `NativeBridge~/build/` picks up the edited `CMakeLists.txt`.
- Step 5: in the reused baseline `NativeBridge~/build/`, ninja recompiles the arm64 objects including `vosk_bridge.h` after its edit — the step 5 log shows `vosk_bridge.cpp` and `audio_capture_audiorecord.cpp` rebuilt; else criterion 3 has not observed the edit.
- Not exercised in Phase 1 (Phase 2's): `vosk_bridge_EXPORTS` reaching the bridge only, `/utf-8`, the `$<1:…>` runtime dir, the vendor checks and `file(COPY)`.
