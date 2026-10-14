---
type: requirements
feature: windows-native-dotnet-fixtures
topic: windows-native-toolchain
status: accepted   # G2 ruled in-conversation 2026-10-14: accepted at fe2e984
updated: 2026-10-14
sources:
  - Planning~/design-docs/windows-native-toolchain.md
  - Planning~/design-docs/windows-native-toolchain/decisions/adr-0001-windows-native-wsl-removed.md
  - Planning~/design-docs/windows-native-toolchain/decisions/adr-0005-host-neutral-fixture-generation.md
---

# feat-windows-native-dotnet-fixtures — requirements

Track **Full**. Design `windows-native-toolchain` (`Planning~/design-docs/windows-native-toolchain.md`), locked at G1 2026-10-13. Depends on `windows-native-harness`, merged.

## 1. User story

- **S1** — As the VoXR maintainer, I want the `Samples~` compile, the DocCheck rig and fixture generation to run from Claude Code's Windows-native shell, so that no step of verification needs WSL installed or started.

## 2. Feature & scope

Backlog row (`windows-native-toolchain.md` §6):

> `windows-native-dotnet-fixtures` | Command 4 (the `Samples~` compile) and the parser A/B rig's DocCheck run procedure on native dotnet with Windows-form paths; the host-neutral `Tests~/Fixtures/generate.sh` (adr-0005); the verification binding's prerequisites, Shell and Path-form lines; the recipe's remaining WSL sections. Closes with the sweep for `wsl` and `/mnt/` over the live areas that closes deferral WSL-1 (adr-0001). **Full.** | `windows-native-harness`

**Agreed at G0 on 2026-10-14** by the maintainer, in session.

**In**

1. Command 4, the `Samples~` compile, runs on native dotnet with Windows-form paths: its line in the verification binding (`.claude/bindings/unity.md` Command 4) and the `Samples~` sections of `Planning~/verification-recipe.md`, including the generated csproj's `/mnt/...` paths.
2. The parser A/B rig's DocCheck run procedure (`Planning~/features/coverage-in-selection/ab-rig/`) runs on native dotnet. The procedure is written down in the repo; today it exists only outside the repo. Where it lives is settled in this doc's later sections. `stage.sh` is unchanged (design §1).
3. `Tests~/Fixtures/generate.sh` is made host-neutral (adr-0005): only the venv layout and the interpreter name change; piper-tts 1.6.0, voice `en_US-lessac-medium` and soxr stay. **Amended 2026-10-14** (ruled "All recommended"): the path form of the paths the script passes to Python also changes, so it behaves the same with `MSYS_NO_PATHCONV` set or unset.
4. The verification binding's Prerequisites, Shell and Path-form lines (`.claude/bindings/unity.md`) drop WSL.
5. The recipe's remaining WSL text in `Planning~/verification-recipe.md`: the `/mnt/c/...` Unity.exe path of Commands 1–2, the "cannot be verified from WSL" wording, the "WSL Usage" section, and the `Samples~` note.
6. A closing sweep for `wsl` and `/mnt/` over the live areas, which closes deferral WSL-1 (adr-0001). The exact list of live areas is settled in this doc's later sections; history docs are excluded per design §4.
7. **Folded in at G0** (ruled "Fold it in"): deferral WNH-1 — the comment at `NativeBridge~/include/vosk_api.h:3` names only `libvosk.so`, while the desktop build now links `libvosk.dll`; it is reworded.

**Out**

1. Command 3 (both halves) and the recipe's desktop-harness section and `### Building NativeBridge` — done by `windows-native-harness` (merged, PR #175).
2. Amendment A2 to `Planning~/design-docs/automated-verification.md` — written and re-locked at the design's G1.
3. The Android arm64 build, and any change to recognition behaviour.
4. Product docs (e.g. `Documentation~/native-bridge.md`) — after G2, by the codex reconciler.
5. `.claude/settings.local.json` (the human's file) and history docs: closed features' docs, `Planning~/research/`, `Planning~/handoff-issue-*.md`, `Planning~/features/coverage-in-selection/ab-rig/issue124/*.py`, the `Planning~/features/resolver-slot-exemption/` plans.
6. `feat-joined-rig-parser`, held by the human.

## 3. Why — the value it delivers

This row finishes taking WSL out of verification (`windows-native-toolchain.md` §0 Goal, §5): after it, no check, rig or fixture tool needs a second OS installed or started, the design's rule 1 ("No `wsl.exe` anywhere in the live pipeline") holds over the whole pipeline, and deferral WSL-1 closes (adr-0001).

Goal 2 of `automated-verification.md` (Claude-runnable, no Unity, no device), as amended by A2, holds on the host Claude runs on.

## 4. Observable behaviour

1. Command 4 runs from Git Bash exactly as the binding writes it, on native dotnet (SDK 10.0.300), and reports `Build succeeded`; its mutation check goes red at the mutated sample's file:line and green again on revert.
2. Whoever runs DocCheck follows one procedure written in `Planning~/verification-recipe.md`: stage with `stage.sh` in Git Bash, then build and run with native dotnet; it exits 0.
3. The maintainer runs `Tests~/Fixtures/generate.sh` from Git Bash on a stock Windows Python; it bootstraps its venv, fetches the voice and synthesizes fixtures in the corpus format.
4. The binding and recipe name no WSL route; `wsl.exe` is never invoked.

## 5. Functional requirements

**Ruled on 2026-10-14** by the maintainer, in session ("Agreed"): the DocCheck run procedure lives in a new section of `Planning~/verification-recipe.md` (fork 1); `generate.sh` is checked by regenerating one fixture, then restoring the committed WAV (fork 2); the closing sweep covers the live areas F7 lists (fork 3).

**Ruled on 2026-10-14** by the maintainer, in session ("All recommended"): `generate.sh` may also convert the path form of the paths it passes to Python (§2 In 3), read as within adr-0005 ("changes only where the hosts differ"), no design reopen.

| #  | Requirement | Story | Priority | Acceptance check | Source |
|----|-------------|-------|----------|------------------|--------|
| F1 | Command 4 (the `Samples~` compile) runs on native dotnet with Windows-form paths: the binding's Command 4 line and the recipe's `## Compiling Samples~` section, including the generated csproj's paths and the sanity-count pattern. | S1 | Must | The verification agent runs Command 4 verbatim from the binding in Git Bash: `Build succeeded`, `0 Error(s)`, only the expected CS0618 warning; the recipe's mutation goes red at that file:line and green after revert; the recipe's sanity count prints 7. | `windows-native-toolchain.md` §0 In, §1, §6 row 2 |
| F2 | The parser A/B rig's DocCheck run procedure is written in `Planning~/verification-recipe.md` (ruled fork 1) and runs on native dotnet; `stage.sh` is unchanged (design §1). | S1 | Must | The procedure followed verbatim exits 0 with every check passing. | `windows-native-toolchain.md` §0 In, §1, §6 row 2; ruling 2026-10-14 (fork 1) |
| F3 | `Tests~/Fixtures/generate.sh` runs in Git Bash on a stock Windows Python 3.12 install, not relying on a `python3` name the installer does not provide, with piper-tts 1.6.0, voice `en_US-lessac-medium` and soxr 1.1.0 unchanged (adr-0005), and it behaves the same run from Git Bash with `MSYS_NO_PATHCONV` set or unset. | S1 | Must | (Ruled fork 2.) From an empty fixture-generation cache, `generate.sh --only <one fixture>` bootstraps the venv, fetches the voice and writes that fixture as a 48 kHz, 16-bit mono WAV with peak within 0.04–0.4; afterwards the committed WAVs under `Tests~/Fixtures/audio/` are unchanged (the regenerated one restored). Its downloads are run by the human as a `!` step. | `windows-native-toolchain.md` §0 In, §1, §6 row 2; adr-0005; ruling 2026-10-14 (fork 2); ruling 2026-10-14 (path form) |
| F4 | `generate.sh` keeps working on a Linux host layout as well ("either host", adr-0005). | S1 | Should | Review only; no Linux host is in the pipeline to run it on. | `windows-native-toolchain.md` §0 In, §1, §6 row 2; adr-0005 |
| F5 | The verification binding's Prerequisites, Shell and Path-form lines (`.claude/bindings/unity.md`) drop WSL and name what Commands 3–4 now need. | S1 | Must | The three lines read so; F7 passes. | `windows-native-toolchain.md` §0 In, §1, §6 row 2 |
| F6 | The recipe's remaining WSL text goes: the `/mnt/c/...` Unity.exe path of Commands 1–2, the "cannot be verified from WSL" wording, the `### WSL Usage` section (rewritten as Git Bash usage of adb and logcat), and the `Samples~` note's WSL dotnet line. | S1 | Must | F7 passes; the adb path the recipe names resolves with `ls` (no device needed). | `windows-native-toolchain.md` §0 In, §1, §6 row 2 |
| F7 | The closing sweep (adr-0001): a case-insensitive search for `wsl` and `/mnt/` over the live areas finds nothing. Live areas (ruled fork 3): `.claude/bindings/`, `Planning~/verification-recipe.md`, `NativeBridge~/`, `Tests~/`, `Runtime/`, `Samples~/`, `Editor/`, the root `CLAUDE.md` and `KNOWN_LIMITATIONS.md`, and `Planning~/features/coverage-in-selection/ab-rig/` except `issue124/`. Excluded as history or other owners: root-level `Planning~/*.md`, design docs, feature docs, `Planning~/research/`, `CHANGELOG.md`, `Documentation~/` (its `native-bridge.md` WSL line is the codex reconciler's after G2), `.claude/settings.local.json`. | S1 | Must | `git grep -n -I -i -E 'wsl\|/mnt/'` over the live areas prints nothing. (`-I` skips binary files: the shipped libvosk binaries match by chance and F9 keeps them unchanged.) | `windows-native-toolchain.md` §0 In, §1, §6 row 2; adr-0001; ruling 2026-10-14 (fork 3) |
| F8 | WNH-1: the comment at `NativeBridge~/include/vosk_api.h:3` names the libvosk library per platform (`libvosk.so` on Android, `libvosk.dll` on the Windows desktop build). | S1 | Must | The line reads so. | G0 fold-in |
| F9 | Nothing shipped changes. | S1 | Must | `git diff main -- Runtime/ Samples~/ Tests~/Fixtures/audio/` is empty; Commands 1–2 (EditMode, PlayMode) green. | `windows-native-toolchain.md` §0 In, §1, §6 row 2 |

## 6. Non-functional requirements

- **Path-conversion neutrality (Must):** every bound command and the DocCheck procedure behave the same with `MSYS_NO_PATHCONV` set (Claude Code's Git Bash) and unset (a maintainer's plain Git Bash). Check: run under both, same result.
- **Degraded dependency (Must):** a missing prerequisite (the .NET SDK, the Unity data folder, the host's `Library/ScriptAssemblies`, Python) fails loudly naming what is missing; nothing falls back to WSL. Check: by review of the bound text and the script; one negative run per Command where cheap (architecture names which).

Rows cut as not applicable: performance budget, input boundary, accessibility (tooling only).

## 7. Non-goals / deferred

The G0 **Out** list in §2, plus:

- Regenerating or re-pinning the committed fixture corpus (piper output is not byte-identical across runs, `generate.py` header).
- Making DocCheck a bound Command (fork 1 option rejected).
- Updating Claude's out-of-repo memory entry for DocCheck (the PM's, after G2).

## 8. Dependencies & assumptions

**Depends on:** `windows-native-harness` (merged, PR #175).

**Assumption:** .NET SDK 10.0.300 is installed native at `C:\Program Files\dotnet`.

**Assumption:** Python 3.12 is installed on Windows.

**Assumption:** Unity 6000.4.7f1 is installed.

**Assumption:** the host `VoXR TestGround` has been imported at least once (its `Library/ScriptAssemblies` for Command 4).

**Assumption:** network access for the human's `!` download step.

**Assumption:** agents cannot download (permission policy), so any download is the human's step.

## 9. Acceptance criteria (G2)

- [x] F1: Command 4 runs verbatim from the binding in Git Bash on native dotnet — `Build succeeded`, `0 Error(s)`, only the CS0618 warning; the mutation goes red and back to green; the sanity count prints 7. (Phase 1 gate: canary red `CS0103` at `TacticalSceneController.cs:56`, green after revert; review-fix CHECK: subshell form green twice, sanity count 7)
- [x] F2: the DocCheck procedure in `Planning~/verification-recipe.md`, followed verbatim, exits 0 with every check passing. (66/66, exit 0, at the Phase 1 gate and the review-fix CHECK)
- [x] F3: `generate.sh --only <one fixture>` from an empty cache bootstraps the venv, fetches the voice and writes a 48 kHz, 16-bit mono WAV with peak within 0.04–0.4; the committed WAVs under `Tests~/Fixtures/audio/` unchanged afterwards. (the maintainer's `--only cease_fire` run: `py -3` 3.12.10, peak 0.050; header `48000 2 1`; WAV restored)
- [x] F4: `generate.sh` keeps a working Linux host layout, by review. (Phase 2 read-through: paths and command words equal the old script's)
- [x] F5: the binding's Prerequisites, Shell and Path-form lines drop WSL and name what Commands 3–4 need; F7 passes. (Phase 1; F7 empty)
- [x] F6: the recipe's remaining WSL text is gone and its adb path resolves with `ls`; F7 passes. (Phase 1 gate: adb `ls` exit 0; F7 empty)
- [x] F7: `git grep -n -I -i -E 'wsl|/mnt/'` over the live areas prints nothing. (empty, exit 1, at Phase 2 gate C, the review-fix CHECK and both preflights)
- [x] F8: `NativeBridge~/include/vosk_api.h:3` names `libvosk.so` on Android and `libvosk.dll` on the Windows desktop build. (Phase 1; closes WNH-1)
- [x] F9: `git diff main -- Runtime/ Samples~/ Tests~/Fixtures/audio/` is empty; Commands 1–2 green. (diff empty every gate; EditMode 203/203, PlayMode 704/704 both ways)
- [x] Path-conversion neutrality: every bound command and the DocCheck procedure give the same result with `MSYS_NO_PATHCONV` set and unset. (Commands 1–4 and DocCheck each run set and unset, same results)
- [x] Degraded dependency: a missing prerequisite fails loudly naming it, with no WSL fallback — by review, plus the negative runs the architecture doc names. (negative runs: Command 4 `HostAsm` exit 1 naming `UnityEngine.UI.dll`; `generate.sh` `VOXR_PYTHON=no-such-python` exit 1 naming it, no venv)
- [x] Human-only verification: none — the human runs the `generate.sh` download step; agents judge its result. (the maintainer's `generate.sh` run, Phase 2)

**Ruled at G2 on 2026-10-14** by the maintainer, in conversation: accepted at head `fe2e984`, with preflight 2 in view — STATUS, five `memory/handoffs` `.meta` and the architecture doc's budget trim remediated preflight 1 before the ruling; WNH-1 and WSL-1 closed.

## 10. Open questions

None open for the human. Left to the architecture doc:

1. **How `generate.sh` resolves the venv layout and interpreter on each host** — the architecture doc's.
2. **The exact binding and recipe text** — the architecture doc's.
3. **Where Command 4's csproj is generated** — the binding says `.scratch/samples-compile/`, the recipe says the session scratchpad — the architecture doc's.
