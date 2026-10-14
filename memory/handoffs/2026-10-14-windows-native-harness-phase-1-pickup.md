You're picking up: `phase-pickup` for `feat-windows-native-harness` Phase 1, starting at RECON. Read the files below, then start at Next steps.

## Objective

Plan Phase 1 of `windows-native-harness`: recon, plan, persist `Planning~/features/windows-native-harness/plan-1.md` (with its link line in the architecture doc's `## Build plan`), validate, reconcile, commit. Done = the plan persisted and validated, and committed; then `implement` Phase 1, or a handoff at the context bound.

## Lane

feature

## Context

- The human, 2026-10-13: taking WSL out of the VoXR pipeline is top priority; everything else (`feat-joined-rig-parser`, `release`, `lab-next-level-probes`) stays held.
- Design `windows-native-toolchain` G1 LOCKED 2026-10-13 (merged, PR #174). This feature is its backlog row 1, **Full** track.
- Branch `feat-windows-native-harness`, one checkout (`trees` none), clean at `02fc2f9`, pushed. No board; `memory/STATUS.md` is the state.
- G0 2026-10-14; requirements and architecture written and the 3-phase plan ruled 2026-10-14 ("All recommended. Go").
- `phase-pickup` ORIENT is done (branch clean, no board). Start at RECON.

## Current state

- `Planning~/features/windows-native-harness/requirements.md`: §§1-10 complete. F1-F8 all Must; human-only G2 verification: none.
- `Planning~/features/windows-native-harness/architecture.md`: complete (body 40,197 of 40,960 bytes, which is tight: plan detail goes in the plan file, not here). `## Build plan` holds Phases 1-3 and no plan links yet.
- Phase 1 (from `## Build plan`): cross-compiler source, Android unchanged.
  - The export macro (`_WIN32` arm keyed on `vosk_bridge_EXPORTS`; the GCC/Clang arm unchanged).
  - In `NativeBridge~/CMakeLists.txt`: `/utf-8` under MSVC; the `WIN32` runtime output dir `$<1:${CMAKE_BINARY_DIR}/bin>`; vendor existence checks; DLL copy; the `.lib`/`.dll` imported `vosk`; `LINK_FLAGS` inside `if(NOT MSVC)`; the `# --- WSL harness` comment.
  - The `Purpose:` comments in `NativeBridge~/harness/CMakeLists.txt` and `NativeBridge~/harness/main.cpp`.
  - Ruling B: the `CMAKE=` line of `Planning~/verification-recipe.md` `### Building NativeBridge`, from `/mnt/c/...` to Git Bash form.
  - Green: Command 3's arm64 half builds with no errors, and the arm64 `libvosk-bridge.so` defined dynamic-symbol list equals the committed `Runtime/Plugins/Android/arm64-v8a/libvosk-bridge.so`'s (NDK `llvm-nm`, its path to be confirmed in the plan). Capture the list from the unchanged tree first. The desktop half is not run in Phase 1. `git diff --stat main -- Runtime/` must be empty (F8).
  - Discharges F3; F4 at both CMakeLists; F7 at the recipe's `### Building NativeBridge`.
- Recon already filed (trust it; don't redo it): `.scratch/probe-windows-native-harness-zip-report.md` (zip facts: F6 holds), `.scratch/code-mapper-windows-native-toolchain-recon-report.md`, `.scratch/probe-windows-native-toolchain-host-report.md`, `.scratch/researcher-windows-native-toolchain-report.md`. Phase 1 recon still needs a fresh `code-mapper` + `doc-extractor` pass per the skill.
- The pinned zip is already downloaded at `.scratch/vosk-win64-probe/vosk-win64-0.3.45.zip` (extracted beside it). `NativeBridge~/vendor/` is not yet provisioned for Windows (Phase 2).

## Key files & locations

- Docs: `Planning~/features/windows-native-harness/{requirements.md,architecture.md}`; design `Planning~/design-docs/windows-native-toolchain.md` and `.../windows-native-toolchain/decisions/adr-0001`...`adr-0004`.
- Code: `NativeBridge~/src/vosk_bridge.h` (macro `VOSK_BRIDGE_EXPORT`, 15 occurrences), `NativeBridge~/CMakeLists.txt` (`IMPORTED_LOCATION .../libvosk.so`; the `LINK_FLAGS` set_target_properties; `# --- WSL harness (Tier C) ---`), `NativeBridge~/harness/CMakeLists.txt`, `NativeBridge~/harness/main.cpp`, `NativeBridge~/build.sh` (arm64).
- Verification: `.claude/bindings/unity.md` `## verification` Command 3; `Planning~/verification-recipe.md` `### Building NativeBridge`.

## Constraints & decisions

- Locked: MSVC from VS 2022 via the "Visual Studio 17 2022" generator (ruled 2026-10-14; no ninja for the desktop build, no vcvars, no `cmd.exe` hop); Unity's cmake 3.22.1 by absolute path; the pinned `vosk-win64-0.3.45`; WSL removed outright; no Windows bridge in the Editor; a divergent transcript stops for the human, never a re-pin. The generator route is ruled within the locked design (no reopen).
- Row 2 `windows-native-dotnet-fixtures` owns: Command 4, DocCheck, `generate.sh`, the binding's Prerequisites/Shell/Path-form lines, and the recipe's other WSL sections (all except the one `CMAKE=` line). Keep out.
- Don't write WSL into anything new. Don't touch `.claude/settings.local.json`, history docs, or product docs (`Documentation~/native-bridge.md` is post-G2).
- Claude Code's Git Bash sets `MSYS_NO_PATHCONV=1`.
- Subagents can't write report files under `.scratch/`; briefs have them return text, and the PM files it. Network downloads by subagents were refused by permission policy (2026-10-14); don't retry in another form.
- Use absolute paths in Bash: `cd` persists across calls.
- Leave `.claude/references/vc-git/__pycache__/` untracked.

## Next steps

1. Invoke `phase-pickup` for feature `windows-native-harness`, Phase 1; ORIENT is done, so go on from RECON.
2. Then `implement` Phase 1.

## Open questions

- None pending for the human.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - <branch>; lane: <lane>; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps. BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer.
