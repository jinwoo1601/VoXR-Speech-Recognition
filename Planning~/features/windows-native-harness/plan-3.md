# Phase 3 — persisted plan (2026-10-14)

**Phase 3 - the bound text and the README** (`architecture.md` `## Build plan`). Unsplit. Discharges F4 (closing), F5, F7; F8 checked. Phase 2 proved `<CMD>` as written (no form correction). Decisions: the human's approved plan, 2026-10-14.

## Files to change

| Path | Change | Why |
|---|---|---|
| `.claude/bindings/unity.md` | modify (:8 only) | F7, F4 (Command 3) |
| `Planning~/verification-recipe.md` | modify (:21 only) | F7, F4 (step 2b) |
| `NativeBridge~/harness/README.md` | modify (rewritten whole) | F5, F4; Components *Harness README*; register *The vendor layout* |

## Contracts and interfaces

**Conventions.** Git Bash, from the project root `D:/Workspace/VoXR-Speech-Recognition`. `<CMD>` is `architecture.md` :102, verbatim, cited by name. It begins `cd NativeBridge~`, so every run of it is `( <CMD> )`. Paths in `C:/...` or Git Bash form, never `/mnt/`. No new WSL text.

**Step 1 - binding :8.** The Command 3 line is replaced whole by `architecture.md` :97's text, without the blockquote `> `. The command string goes inside the backticks holding the `<CMD>` token. One line in, one out. :9, :12, :15, :17 (row 2) and every other line stay byte-unchanged.

**Step 2 - recipe :21.** The step 2b item is replaced whole by `architecture.md` :107's text, without the `> `, `<CMD>` filled the same way. The 3-space indent stays. One line in, one out. Every other line stays unchanged.

**Step 3 - README rewritten.** The required content below; the code-writer writes the prose.
1. Title and summary, Windows-native. The harness replays the corpus through the real bridge against "a real Windows libvosk 0.3.45, the same DLL the Editor ships", with no Unity and no device. It is blind to the arm64 binary, JNI capture and Quest routing. The word `WSL` never appears (the F4 grep is case-insensitive).
2. `## One-time provisioning` into the gitignored `NativeBridge~/vendor/`:
   1. Toolchain: VS 2022 Community with the x64 C++ tools at its default path; Unity 6000.3.7f1 with Android Build Support, whose SDK ships the cmake 3.22.1 the command uses by absolute path; the shell is Git Bash.
   2. libvosk, from `NativeBridge~/vendor`. These three lines, verbatim and in order:
      - `curl -LO https://github.com/alphacep/vosk-api/releases/download/v0.3.45/vosk-win64-0.3.45.zip` (URL: `.scratch/probe-windows-native-harness-zip-report.md:5`)
      - `echo "f1dcc9cca460630f81ea8f71794f69c80bed6556d2a4e6237b5785e1d2dff34b  vosk-win64-0.3.45.zip" | sha256sum -c -`
      - `unzip vosk-win64-0.3.45.zip`

      Result: `vendor/vosk-win64-0.3.45/`, its six files as unzipped, nothing copied up. A mismatch stops: do not extract, raise it (adr-0004). The pin must match `expectations.json`'s `libvosk`.
   3. Model `vosk-model-small-en-us-0.15`: unzip the host project's copy (`VoXR TestGround/Assets/StreamingAssets/vosk-model-small-en-us-0.15.zip`) into `vendor/`, or `curl -LO https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip` and unzip it.
   4. `vendor/libvosk.so`, `vendor/vosk-linux.zip` and `vendor/vosk-linux-x86_64-0.3.45/` are unused: delete them.
3. Build and run: `<CMD>` verbatim, on exactly one line in one fenced block, run from the project root in Git Bash. The README names the binding's Command 3 as the source it copies. One line per part: `ls -d` checks the toolchain paths; the configure uses preset `windows-x64`, pinned to the VS 2022 instance; the build is Release; then the harness runs. The command behaves the same with or without `MSYS_NO_PATHCONV`. The preset selects stub capture and builds the harness. No other line carries the whole `<CMD>`.
4. Kept, edited only where stale: the exit codes 0/1/2, the three guards, the 48 kHz WAV rule, the baseline section, `## Notes`.
5. Re-baselining runs `./build-windows/bin/vosk-bridge-harness.exe ... --write-baseline`. It keeps "conscious, reviewed step" and adds that a Windows divergence stops for the human and is never re-pinned.
6. Dropped: the raw-configure fallback (CMake < 3.21), and every `wget`, `apt`, `build-desktop`, `desktop-linux`, `/mnt/` and `wsl`.

**Stops for the human:** harness exit 1 (never re-pin) or exit 2; a hash mismatch; a missing README tool (a README wording fix, per Risks); a non-empty symbol diff; any edit a row-2 line would need.

**Commit set:** the three files. No CHANGELOG line (dev tooling). `vendor/` and `build-windows/` are gitignored. Nothing in `.scratch/` is committed.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| `build-windows/` moved aside too (an addition to the row's procedure, approved with this plan) | Leave it | F5's build is a fresh configure, not a relink |
| The human runs the README's `curl` (ruled 2026-10-14) | An agent download; the `.scratch/vosk-win64-probe/` copy | Permission policy refuses agent downloads. F5 follows the README's own line. Requirements §4 item 1 admits the maintainer |
| F4 grep scoped to binding :8 and recipe :21 | File-wide | The other hits are row 2's (requirements §2 Out item 1) |
| The three-copies pattern read from :102 into a file | An inline pattern | `<CMD>` holds both kinds of quote |

## Acceptance criteria

The gate, run by `compile-check` in this order: two dispatches, with one human step between them.

**Part 1**
1. [ ] Commands 1 and 2 run in full and green, with the host Editor closed (the binding's `UnityLockfile` absent). Then the `Tests~` revert, and deletion of the untracked `.meta` orphans, `memory/` included.
2. [ ] Move aside, never delete: everything in `NativeBridge~/vendor/` but `vosk-model-small-en-us-0.15/` goes to `.scratch/windows-native-harness-phase-3-vendor-aside/`; `NativeBridge~/build-windows/` goes to `.scratch/windows-native-harness-phase-3-build-aside/`. Then `ls NativeBridge~/vendor/` shows the model alone. (F5)

**Human step (ruled 2026-10-14):** the human runs `! ( cd NativeBridge~/vendor && curl -LO https://github.com/alphacep/vosk-api/releases/download/v0.3.45/vosk-win64-0.3.45.zip )`, a subshell, so the session's cwd stays at the project root. Part 2 starts at the project root `D:/Workspace/VoXR-Speech-Recognition`.

**Part 2**
3. [ ] The README's next two lines, verbatim, each in `( cd NativeBridge~/vendor && ... )`. `sha256sum -c` prints `vosk-win64-0.3.45.zip: OK`, then `unzip` runs. `ls NativeBridge~/vendor/vosk-win64-0.3.45/` shows exactly the six files of `architecture.md` *Provisioned inputs* (`## Context & scope`, :41). (F5)
4. [ ] Command 3 from binding :8, verbatim, both halves (F5's build-and-run and F7, in one run):
   - The arm64 half follows the recipe's `### Building NativeBridge`. The symbol list is taken as `plan-2.md` criterion 1 does it, into `.scratch/windows-native-harness-phase-3-symbols-post.txt`. Its `diff` against `.scratch/windows-native-harness-phase-1-symbols-committed.txt` is empty. No recommit.
   - The desktop half `( <CMD> )` exits 0 with 16/16.
5. [ ] If 3 or 4 fails, restore and stop. First, an entry Part 2 made under an aside name (`vendor/vosk-win64-0.3.45/`, `build-windows/`) and the human's `vendor/vosk-win64-0.3.45.zip` go to `.scratch/windows-native-harness-phase-3-failed-run/`, never deleted. Then the vendor-aside's contents move back into `NativeBridge~/vendor/`, the build-aside's back to `NativeBridge~/build-windows/`. On green, the aside directories stay.
6. [ ] F4, each count 0:
   - `sed -n 8p .claude/bindings/unity.md | grep -icE 'wsl|/mnt/|desktop-linux'`
   - the same on `sed -n 21p Planning~/verification-recipe.md`
   - `grep -icE 'wsl|/mnt/|desktop-linux'` over `NativeBridge~/CMakePresets.json`, `NativeBridge~/CMakeLists.txt`, `NativeBridge~/harness/CMakeLists.txt`, `NativeBridge~/harness/README.md`
7. [ ] Three copies (F7; Risks):
   - `sed -n 102p Planning~/features/windows-native-harness/architecture.md | tr -d '\r' > .scratch/windows-native-harness-phase-3-cmd.txt`
   - `grep -cFf` of that file gives 1 in each of the binding, the recipe and the README.
8. [ ] `git diff --stat main -- Runtime/` is empty (F8). `git diff --name-only main -- NativeBridge~/ .claude/ Planning~/verification-recipe.md Runtime/` lists exactly these nine paths, no other: `NativeBridge~/CMakeLists.txt`, `NativeBridge~/CMakePresets.json`, `NativeBridge~/harness/CMakeLists.txt`, `NativeBridge~/harness/expectations.json`, `NativeBridge~/harness/main.cpp`, `NativeBridge~/src/vosk_bridge.h` (Phases 1-2, PM-verified by that command 2026-10-14); `NativeBridge~/harness/README.md`, `.claude/bindings/unity.md`, `Planning~/verification-recipe.md` (Phase 3; the recipe also carries Phase 1's `CMAKE=` line).

## Open questions that block implementation

None. The stops above are the human's at run time.

## Blast radius

- Binding :9, :12, :15, :17 and the recipe's other WSL lines (:3, :17, :24, :35-47, ...) keep their WSL text (row 2). A file-wide grep stays non-empty.
- `.gitignore:44`, `CMakePresets.json:7`, `CMakeLists.txt:67` and `expectations.json:2` name the README only; they are unaffected.
- `Documentation~/native-bridge.md` :59-61 and `CHANGELOG.md:74` stay Linux until after G2. `memory/` handoffs' old README line refs go stale (history).
- Host: the zip in `vendor/`, the aside directories in `.scratch/`, a fresh `build-windows/`; stale caches stay.
- Every later `NativeBridge~/` change is verified by this Command 3 text.

## Runtime claims

- Criterion 1: Commands 1 and 2 are green in Unity batchmode with the host Editor closed.
- Criterion 2: `mv` of the `vendor/` and `build-windows/` entries succeeds, no file locks held.
- Human step: the `!` subshell leaves the session cwd at the project root.
- Human step, criterion 3: Git Bash resolves `curl` (from `mingw64/bin` or `System32`; none in `usr/bin`), `sha256sum -c` and `unzip`. `curl -LO` follows GitHub's redirect and saves the file under the zip's name. `sha256sum -c -` reads stdin and prints `OK`.
- Criterion 3: `unzip` yields the six-file `vosk-win64-0.3.45/`.
- Criterion 4: Command 3 verbatim is green from a fresh `build-windows/` (first configure, the DLL copy, 16/16). The arm64 symbol list is unchanged.
- Criterion 7: `sed -n 102p | tr -d '\r'` yields `<CMD>` exactly; `grep -cFf` counts the lines carrying it.
