You're picking up: implement Phase 2 ("bridge timing and dump schema 2") of `feat-joined-rig-recogniser`. Read the files below, then start at Next steps.

## Objective

Run `implement` for Phase 2 of `joined-rig-recogniser` from its persisted, validated plan `Planning~/features/joined-rig-recogniser/plan-2.md`. Done when the plan's gate (criteria 2-11) is green with evidence, the as-built pass is recorded, and the commit set is committed with STATUS advanced. The next step after that is `phase-pickup` Phase 3, not part of this task.

## Lane

feature

## Context

- Branch `feat-joined-rig-recogniser` in the one checkout `D:\Workspace\VoXR-Speech-Recognition` (`trees` none), head `bc5858e` (plan-2 persisted) plus this handoff's commit, pushed. Design `joined-rig` G1-locked; track Full.
- Phase 1 (the staged world, rig only) built and gated, commit `ef1ed67`. Phase 2 is the feature's only shipped-tree change: `NativeBridge~/src/` and the committed arm64 `.so`.
- STATUS `memory/STATUS.md` `## Current`: next `implement` Phase 2. Session note `memory/2026-10-15.md`, entry "Phase 2 pickup".

## Current state

- Plan validated at full depth (`plan-validator`: go after fixes, 12 findings, 0 canon conflicts); every finding ruled real and fixed by an `architect` fix pass; 10,212 bytes. No code written; no Step 0 run.
- Pickup records: `.scratch/code-mapper-joined-rig-recogniser-phase-2-report.md`, `.scratch/doc-extractor-joined-rig-recogniser-phase-2-report.md`, `.scratch/plan-validator-joined-rig-recogniser-phase-2-report.md`, `.scratch/architect-joined-rig-recogniser-phase-2{,-fix}-{brief,report}.md`.

## Key files & locations

- Plan: `Planning~/features/joined-rig-recogniser/plan-2.md` — Dispatch A / B / C (criterion 2) / B2 (Step 7, the `.so` copy) / C (criteria 3-11); Stops (PM); Commit set; `## Runtime claims` a-l.
- Architecture: `Planning~/features/joined-rig-recogniser/architecture.md` — register *Bridge timing*, *ABI route*, *Deterministic dump stream*, *Schema 2*, *Checks as commands*, *Staging* (2026-10-15 amendment); `## As-built deltas` (Phase 1 entry, the form for Phase 2's).
- Code: `NativeBridge~/src/{result_queue.h,vosk_bridge.cpp,vosk_bridge.h}`, `NativeBridge~/harness/main.cpp`, `Planning~/features/coverage-in-selection/ab-rig/{JoinedInput.cs,Joined.cs,Rig.csproj}`, `Runtime/Plugins/Android/arm64-v8a/libvosk-bridge.so`.
- Verification: `.claude/bindings/unity.md` `## verification` (Commands 1-3); `Planning~/verification-recipe.md` (Building NativeBridge; DocCheck).

## Constraints & decisions

- `NativeBridge~/build` is not deleted: its `CMakeCache.txt` already names this tree (read 2026-10-15), so the arm64 build configures in place.
- A Command 3 transcript mismatch, or a `--dump` stdout differing from Command 3's, stops for the human — never a re-pin, never a relaxed compare. `expectations.json` is never edited.
- A plan runtime claim found false at the first gate is a stop for the human's ruling (Phase 1's precedent: claims a and j).
- *Staging* trap: no world drives through `InjectResult`, `InjectPartialResult`, `InjectText`, `FlushPendingBuffer`.
- Commands 1-2 need the host Editor closed: `D:\Workspace\VoXR TestGround\Temp\UnityLockfile` absent; present → stop and ask the human.
- Quest on-device check: human-only, at G2, never claimed.
- Agents never run git writes; the PM commits per the `vc` binding. Compiler and test output goes to `compile-check`, never the main thread. Every brief names the project root.
- The report filer did not file background recon/validator reports this session; check `.scratch/` after each dispatch and file by hand from the transcript where missing.
- Windows-native only (Git Bash, MSVC 2022, the Unity 6000.3.7f1 SDK's `cmake.exe`/`ninja.exe`, .NET SDK 10); never WSL. `stage.sh` under `env -u MSYS_NO_PATHCONV`.

## Next steps

1. Invoke `implement` for feature `joined-rig-recogniser`, Phase 2 (plan `Planning~/features/joined-rig-recogniser/plan-2.md`).

## Open questions

- Whether the host Editor is closed is the human's call; it matters at criterion 9 (Commands 1-2).

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-joined-rig-recogniser; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor ee0bbcd6-0831-471b-b5f1-493d93256272". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
