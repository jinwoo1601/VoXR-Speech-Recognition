You're picking up: `phase-pickup` for `feat-windows-native-harness` Phase 3. Read the files below, then start at Next steps.

## Objective

Plan Phase 3 of `windows-native-harness` (the bound text and the README): orient, recon, plan, persist `Planning~/features/windows-native-harness/plan-3.md` (with its link line in the architecture doc's `## Build plan`), validate, reconcile, commit. Done = plan-3 persisted, validated and committed; then `implement` Phase 3, or a handoff at the context bound.

## Lane

feature

## Context

- The human, 2026-10-13: taking WSL out of the VoXR pipeline is top priority; `feat-joined-rig-parser`, `release` and `lab-next-level-probes` stay held.
- Design `windows-native-toolchain` G1 LOCKED 2026-10-13 (PR #174). This feature is its backlog row 1, **Full** track; the 3-phase plan was ruled 2026-10-14. Phase 3 is the last phase; after it comes `review-cycle`, then G2.
- Branch `feat-windows-native-harness`, one checkout (`trees` none), clean at `5c10602`, pushed. No board; `memory/STATUS.md` is the state.
- This session ran Phase 2's `implement`, so Phase 3's pickup starts a fresh session at ORIENT.

## Current state

- Phase 1 DONE (`0e74bc3` plan, `bd8443e` build). Phase 2 DONE: plan `8bf279d` (`plan-2.md`, validated, one fix pass), build `5c10602` (`CMakePresets.json` `windows-x64` replaces `desktop-linux`; `expectations.json:236` label `vosk-win64`).
- Phase 2 gate (`.scratch/compile-check-windows-native-harness-phase-2-gate-report.md`): EditMode 203/203, PlayMode 704/704; arm64 symbol diff empty; first VS 2022 configure (MSVC 19.44, MSBuild 17.14.23) and build: 0 warnings, 0 errors, **no source fix needed**; harness exit 0, 16/16, byte-identical with and without `MSYS_NO_PATHCONV`; misspelt-instance stops at `ls -d`; VS 2026 (`C:/Program Files/Microsoft Visual Studio/18/Community`) rejected by the generator; empty `VOSK_LIB_DIR` fails naming `libvosk.lib`.
- **`<CMD>` proved exactly as written** in the architecture doc's `## Data contract & runtime flow`: no form correction, so no register amendment is due. Phase 3 binds it verbatim.
- `NativeBridge~/vendor/vosk-win64-0.3.45/` is provisioned (six files, hashes checked: `.scratch/compile-check-windows-native-harness-phase-2-provision-report.md`). `vendor/` still holds the Linux leftovers (`libvosk.so`, `vosk-linux.zip`, `vosk-linux-x86_64-0.3.45/`) and the model. `NativeBridge~/build-windows/` holds the Windows build.
- Phase 2 reports, for form: `.scratch/{code-mapper,doc-extractor,plan-validator}-windows-native-harness-phase-2-report.md`, `.scratch/architect-windows-native-harness-phase-2-{brief,fix-brief}.md`, `.scratch/compile-check-windows-native-harness-phase-2-gate-brief.md`.

## Key files & locations

- Docs: `Planning~/features/windows-native-harness/{requirements.md,architecture.md,plan-1.md,plan-2.md}`. Design: `Planning~/design-docs/windows-native-toolchain.md`, ADRs in `.../windows-native-toolchain/decisions/`.
- Phase 3 scope is the architecture doc's `## Build plan` Phase 3 bullet (discharges F4 closing, F5, F7). Command 3's new text and step 2b's new text are in `## Data contract & runtime flow`.
- Sites: `.claude/bindings/unity.md:8` (Command 3), `Planning~/verification-recipe.md:21` (step 2b), `NativeBridge~/harness/README.md` (WSL hits at :30, :31, :39, :48, :71 and others). The six F4 sites for the closing grep: `CMakePresets.json`, `CMakeLists.txt`, `harness/CMakeLists.txt`, `harness/README.md`, the binding's Command 3, the recipe's desktop-harness section.

## Constraints & decisions

- Locked: MSVC from VS 2022 through the "Visual Studio 17 2022" generator; Unity's cmake 3.22.1 by absolute path; pinned `vosk-win64-0.3.45` (zip SHA-256 `f1dcc9cc...f34b`); WSL removed outright; no Windows bridge in the Editor (F8: `git diff --stat main -- Runtime/` empty).
- Row 2 `windows-native-dotnet-fixtures` owns Command 4, DocCheck, `generate.sh`, the binding's Prerequisites/Shell/Path-form lines, recipe :3, :47 and every other WSL section. Keep out of them. `Documentation~/native-bridge.md` and `CHANGELOG.md:74` are product docs (post-G2).
- Phase 3's F5 check runs the README from a `vendor/` emptied of all but the model: move contents aside (not delete), restore only if the run fails. The README downloads the zip by `curl`; **subagent network downloads were refused by permission policy (2026-10-14)** - don't retry in another form; the human can run the download, or the pinned copy at `.scratch/vosk-win64-probe/vosk-win64-0.3.45.zip` may serve if the plan rules so.
- The architecture doc is at its body budget (whole file 41,415 bytes): plan detail goes in `plan-3.md` (10 KB budget), never in the architecture doc.
- Gate pattern: `compile-check` runs Commands 1 and 2 in full (host Editor closed: `D:\Workspace\VoXR TestGround\Temp\UnityLockfile` absent), the revert and deletion of untracked `.meta` orphans (also under `memory/`), then the phase's checks. Build steps go to `compile-check`, never the main thread.
- Subagents can't write under `.scratch/`: their reports are in their transcripts at `~/.claude/projects/D--Workspace-VoXR-Speech-Recognition/<session-id>/subagents/agent-<agentId>.jsonl`, in the `SubagentHandback` tool call's `message`; the PM extracts and files it.
- Claude Code's Git Bash sets `MSYS_NO_PATHCONV=1`; use absolute paths, `cd` persists. Leave `.claude/references/vc-git/__pycache__/` untracked. No CHANGELOG line so far (dev tooling only).

## Next steps

1. Invoke `phase-pickup` for feature `windows-native-harness`, Phase 3.
2. Then `implement` Phase 3.

## Open questions

- None pending for the human.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-windows-native-harness; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 695956be-9a5d-472f-b21b-4327ef5aaff3". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; send to the one on this machine. If the bare name is refused as ambiguous, use the ref `ListAgents` shows for the local one.)
