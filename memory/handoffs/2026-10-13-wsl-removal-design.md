You're picking up: take WSL out of the VoXR pipeline. You run as the hub (`voxr-hub`), and the work is a design topic. Read the files below, then start at Next steps.

## Objective

The human's word, 2026-10-13: "Taking WSL out of Voxr pipeline is top priority. I don't know why it wasnt done before. Forget everything else."

Open the design topic `windows-native-toolchain` at G0, design it, lock it at G1, then build its backlog. Done = every verification command, rig and fixture tool this project runs works from Claude Code's Windows-native shell (Git Bash / PowerShell), with no `wsl.exe` anywhere in the live pipeline.

## Lane

design

## Context

- **Repo and checkout.**
  - Repo `D:\Workspace\VoXR-Speech-Recognition`, Unity package `com.jinwoo1601.voxr`.
  - One checkout; the `trees` binding is `none`. The host `D:\Workspace\VoXR TestGround` consumes it by `file:` path.
- **Claude Code runs natively on Windows.** The project's tooling still routes work through WSL. The human ruled the route on 2026-10-13: a design topic, then the build, not the light lane.
- **Why a design topic.** The locked `Planning~/design-docs/automated-verification.md` defines Tier C, the native harness, as WSL-hosted:
  - Goal 2, `:76`: "Claude can run directly from WSL, with no Unity and no device".
  - Tier C purpose, `:151`; what it verifies, `:183`.
  - Changing the harness host contradicts a locked decision. So this design amends that doc (design-track: "a design may amend another topic's locked doc where it names the exact site and the human's G1 covers it") and is re-locked at G1.
- **Precedent, the same day.** `Planning~/design-docs/joined-rig.md` (locked, PR #173) had its rule 7 made host-neutral by Amendment A1, `joined-rig/decisions/adr-0006-host-neutral-runner.md`. adr-0006 leaves "which host runs each half (Windows-native or WSL)" to `feat-joined-rig-parser`'s architecture doc. A project-wide toolchain ruling here settles that choice for it; name the coupling.
- **On hold until this is done:**
  - PR #173: the human merges it.
  - Lab `lab-next-level-probes`: probe 2 is waiting on the human's marks, probe 3 on Quest recordings.
  - `release` and `feat-joined-rig-parser`.

## Current state

- **Branches and STATUS.**
  - The checkout is on `design-joined-rig`; its PR #173 is open and unmerged.
  - The design-joined-rig commits (`b5de8cf`, `0d550cd`, `38f4a51`, `2110d0b`) carry `memory/STATUS.md` changes that `main` does not have yet. This brief is among them, along with the WSL-1 deferral and the joined-rig lines.
- **WSL-1 is open in STATUS `## Deferred`.** This design subsumes it and closes it at its G2.
- **Windows facts, verified 2026-10-13:**
  - `dotnet` is native at `C:\Program Files\dotnet`.
  - `cmake` and `ninja` are not on PATH. The Unity 6000.3.7f1 Android SDK ships `cmake.exe` and `ninja.exe`; Command 3 already uses them for the arm64 build.
  - WSL is still installed and works. A probe build of the harness ran there on 2026-10-13: 16/16, exit 0.

## Key files & locations (the live WSL sites)

- **`.claude/bindings/unity.md`**:
  - line 8, Command 3: the desktop harness via `wsl.exe -e bash -lc ...`;
  - line 9, Command 4: the `Samples~` compile in WSL;
  - line 12: the prerequisites, WSL with cmake and gcc;
  - line 15: Shell, WSL commands via `wsl.exe`;
  - line 17: Path form, `/mnt/d/...`.
- **`Planning~/verification-recipe.md`**:
  - `:21`: the desktop harness, "WSL, native Linux cmake/gcc - NOT the Windows .exe tools";
  - `:35`: "WSL Usage";
  - `:71`: `Samples~` verified under WSL dotnet.
- **`NativeBridge~/CMakePresets.json`**: the only desktop preset is `desktop-linux`, built against a Linux `libvosk.so` in the gitignored `NativeBridge~/vendor/`.
- **`NativeBridge~/CMakeLists.txt`**: the harness is added at about `:84-88`.
- **`NativeBridge~/harness/CMakeLists.txt`** and **`NativeBridge~/harness/README.md`**: provisioning at `:7-24`, build at `:28-31`.
- **`NativeBridge~/harness/main.cpp`**, the harness. **`NativeBridge~/src/vosk_bridge.cpp`**, the bridge, including `:91-126`, the recognition loop. Check both for POSIX-only code.
- **`Tests~/Fixtures/generate.sh`**, the fixture generator.
- **The parser A/B rig**, `Planning~/features/coverage-in-selection/ab-rig/` (`stage.sh`, `Rig.csproj`, DocCheck). Its run procedure, from the user's auto-memory, stages under Git Bash and builds and runs in WSL dotnet.
- **`Documentation~/native-bridge.md:61`**, product docs. Update only after G2, by the codex reconciler.
- **Other WSL mentions** in closed features' docs and in `Planning~/research/` are history; leave them.

## Constraints & decisions

- Design track: no code before G1, and no product-doc edits on the design branch.
- Use the `system-design-doc` skill for the design and `g1-lock` for the lock.
- Do not write WSL into anything new. The human's correction, 2026-10-13: "Why are we talking WSL? We moved out of wsl to windows native."
- The vc binding: branches are cut from an up-to-date `main`. Claude never merges; the human merges PRs on GitHub. Memory-only commits on `main` are fine; the human's ruling 2026-10-13 is that `main` is the dev branch.
- Leave `.claude/references/vc-git/__pycache__/` untracked; the human's ruling.
- Subagents may not write report files under `.scratch/`. Briefs have them return the report as text, and the PM files it.
- Forks to put to the human; don't pick them:
  - the C++ toolchain for the desktop harness (MSVC, or clang with the cmake and ninja Unity ships);
  - the Windows libvosk source (Alphacephei's win64 release, or another);
  - how fixtures are generated on Windows;
  - whether the bridge or harness C++ needs portability work;
  - where cmake and ninja come from on PATH;
  - whether WSL stays as an optional fallback or is removed outright. The human's words lean to "removed".

## Next steps

1. `gh pr view 173 --json state`. If it is MERGED: `git fetch origin`, `git switch main`, `git merge --ff-only origin/main`. If it is not merged, ask the human to merge PR #173 first, because `main`'s STATUS lacks the WSL-1 deferral and this brief.
2. Invoke `orient`.
3. Invoke `g0-open` for the design topic `windows-native-toolchain`, branch `design-windows-native-toolchain`, with scope from the Objective and the sites above.
4. Then `system-design-doc` (recon first, through `code-mapper`, over the files above), and `g1-lock`.

## Open questions

- Does WSL stay as an optional fallback, or is it removed outright? This is the human's call at SETTLE.
- Should the auto-memory `doccheck-run-procedure` (WSL-based) be rewritten once the rig runs natively? Update or delete it after the build.

