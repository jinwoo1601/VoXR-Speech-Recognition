You're picking up: `phase-pickup` for `feat-windows-native-harness` Phase 2. Read the files below, then start at Next steps.

## Objective

Plan Phase 2 of `windows-native-harness` (the Windows build and run): orient, recon, plan, persist `Planning~/features/windows-native-harness/plan-2.md` (with its link line in the architecture doc's `## Build plan`), validate, reconcile, commit. Done = plan-2 persisted, validated and committed; then `implement` Phase 2, or a handoff at the context bound.

## Lane

feature

## Context

- The human, 2026-10-13: taking WSL out of the VoXR pipeline is top priority; `feat-joined-rig-parser`, `release` and `lab-next-level-probes` stay held.
- Design `windows-native-toolchain` G1 LOCKED 2026-10-13 (PR #174). This feature is its backlog row 1, **Full** track; the 3-phase plan was ruled 2026-10-14 ("All recommended. Go").
- Branch `feat-windows-native-harness`, one checkout (`trees` none), clean at `bd8443e`, pushed. No board; `memory/STATUS.md` line 8 is the state.
- Phase 1 is DONE: planned (`0e74bc3`), built and gated (`bd8443e`). This session ran Phase 1's `implement`, so Phase 2's pickup starts a fresh session at ORIENT.

## Current state

- Phase 1 landed (in `bd8443e`): the three-arm `VOSK_BRIDGE_EXPORT` (`NativeBridge~/src/vosk_bridge.h`); in `NativeBridge~/CMakeLists.txt`, `/utf-8` under MSVC, the `WIN32` runtime dir `$<1:${CMAKE_BINARY_DIR}/bin>`, five vendor `EXISTS` checks (`FATAL_ERROR` names `harness/README.md`), a configure-time `file(COPY)` of four DLLs, the `.lib`/`.dll` imported `vosk`, and `LINK_FLAGS` under `if(NOT MSVC)`; the `Purpose:` lines; and the recipe's `CMAKE=` line in `/c/...` form. **None of the `WIN32`/`MSVC` branches has ever run**: Phase 2's first configure exercises them.
- Phase 1 gate (filed `.scratch/compile-check-windows-native-harness-phase-1-gate-report.md`): EditMode 203/203, PlayMode 704/704, arm64 build exit 0, symbol diff empty.
- Symbol lists: `.scratch/windows-native-harness-phase-1-symbols-committed.txt` (15 lines, the committed `.so`) is the reference for Phase 2's "Command 3's arm64 half as in Phase 1" check.
- `NativeBridge~/build/` holds the post-Phase-1 arm64 build. The old stale cache was moved to `.scratch/windows-native-harness-phase-1-stale-build/`; leave it there.
- Phase 1 plan and reports, for form: `Planning~/features/windows-native-harness/plan-1.md`; `.scratch/{code-mapper,doc-extractor}-windows-native-harness-phase-1-report.md`, `.scratch/plan-validator-windows-native-harness-phase-1-report.md`, `.scratch/compile-check-windows-native-harness-phase-1-{baseline,gate}-brief.md`.
- `NativeBridge~/vendor/` is not yet provisioned for Windows. The pinned zip sits at `.scratch/vosk-win64-probe/vosk-win64-0.3.45.zip`, extracted beside it. Zip facts: `.scratch/probe-windows-native-harness-zip-report.md`.

## Key files & locations

- Docs: `Planning~/features/windows-native-harness/{requirements.md,architecture.md,plan-1.md}`. Design: `Planning~/design-docs/windows-native-toolchain.md` and `.../windows-native-toolchain/decisions/adr-0001`...`adr-0004`.
- Phase 2 scope is the architecture doc's `## Build plan` Phase 2 bullet. Its `<CMD>` (the desktop configure/build/run) is in `## Data contract & runtime flow`; *Provisioned inputs* (the SHA-256) is in the same doc.
- Code: `NativeBridge~/CMakePresets.json` (the single `desktop-linux` preset, to be replaced by `windows-x64`), `NativeBridge~/harness/expectations.json` (the `libvosk` label line only), `NativeBridge~/CMakeLists.txt`.
- NDK `llvm-nm` (confirmed working): `/c/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/NDK/toolchains/llvm/prebuilt/windows-x86_64/bin/llvm-nm.exe`; compare `llvm-nm -D --defined-only <so> | awk '{print $2, $3}' | sort`.
- Verification: `.claude/bindings/unity.md` `## verification` (Command 3's desktop half is still the WSL text, so don't run it; Phase 2 runs the architecture doc's `<CMD>` instead); `Planning~/verification-recipe.md` `### Building NativeBridge`.

## Constraints & decisions

- Locked: MSVC from VS 2022 through the "Visual Studio 17 2022" generator (no ninja for the desktop, no vcvars, no `cmd.exe` hop); Unity's cmake 3.22.1 by absolute path; the pinned `vosk-win64-0.3.45`; WSL removed outright; no Windows bridge in the Editor (F8: `git diff --stat main -- Runtime/` empty, and no `.so` recommit).
- Phase 2 stops for the human if: the generator cannot find or drive VS 2022 (adr-0003's reopening condition); the zip SHA-256 mismatches or `.lib` is missing (F6); an MSVC fix touches DSP arithmetic; or any transcript diverges (never re-pin).
- Row 2 `windows-native-dotnet-fixtures` owns Command 4, DocCheck, `generate.sh`, the binding's Prerequisites/Shell/Path-form lines, and every other WSL section of the recipe, including the tool-list path at recipe :47. Keep out of them.
- Don't write WSL into anything new. Don't touch `.claude/settings.local.json`, history docs, or product docs (`Documentation~/native-bridge.md` is post-G2).
- The architecture doc is near its 40 KB body budget (whole file 41,349 bytes by `wc -c`; the body was 40,197 bytes before Phase 1's one link line). Plan detail goes in `plan-2.md` (10 KB budget), never in the architecture doc.
- Gate pattern used in Phase 1: `compile-check` runs Commands 1 and 2 in full (the host Editor must be closed: `D:\Workspace\VoXR TestGround\Temp\UnityLockfile` absent), then the revert and deletion of untracked `.meta` orphans (Unity also writes them under `memory/`), then the phase's native checks. Build steps go to `compile-check`, never the main thread.
- Subagents can't write report files under `.scratch/`: briefs have them return text, and the PM files it. Network downloads by subagents were refused by permission policy (2026-10-14); don't retry in another form.
- Claude Code's Git Bash sets `MSYS_NO_PATHCONV=1`. Use absolute paths in Bash, because `cd` persists.
- Leave `.claude/references/vc-git/__pycache__/` untracked.
- CHANGELOG: Phase 1 added none, since the change is dev tooling only and nothing is consumer-visible.

## Next steps

1. Invoke `phase-pickup` for feature `windows-native-harness`, Phase 2.
2. Then `implement` Phase 2.

## Open questions

- None pending for the human.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-windows-native-harness; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 66603a24-b06d-441f-b260-ad6d908613e9". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; send to the one on this machine. If the bare name is refused as ambiguous, use the ref `ListAgents` shows for the local one.)
