You're picking up: implement Phase 1 ("the staged world") of `feat-joined-rig-recogniser` from its persisted, validated plan. Read the files below, then start at Next steps.

## Objective

Run `implement` for Phase 1 from `Planning~/features/joined-rig-recogniser/plan-1.md`: its dispatches A (baseline), B (code-writer, Steps 1-6 as one atomic dispatch), C (the gate), through to the phase's check-in. Done when the plan's acceptance criteria are met with evidence and the phase is committed with STATUS advanced. Nothing past Phase 1.

## Lane

feature

## Context

- Branch `feat-joined-rig-recogniser` in the one checkout `D:\Workspace\VoXR-Speech-Recognition` (`trees` none), head `a9d2bb0`, pushed. Design `joined-rig` G1-locked; track Full.
- Today (2026-10-15): G0, requirements, architecture and the 4-phase build plan done; Phase 1 plan persisted, validated (full), fixed and committed (`a9d2bb0`).
- STATUS `memory/STATUS.md` `## Current` line 8: next `implement` Phase 1. Session note `memory/2026-10-15.md`, entry "feat-joined-rig-recogniser — Phase 1 pickup".

## Current state

- `plan-1.md` validated: report `.scratch/plan-validator-joined-rig-recogniser-phase-1-report.md` (no canon conflict; findings 1-5, 7 fixed by `.scratch/architect-joined-rig-recogniser-phase-1-fix-report.md`; 8 refuted). It is linked from the architecture doc's `## Build plan`.
- No rig or `Runtime/` code changed yet.

## Key files & locations

- Plan: `Planning~/features/joined-rig-recogniser/plan-1.md` (Steps 0-6, dispatches A/B/C, acceptance criteria 1-8 — gate = 2-6 in order, 7-8 after; `## Runtime claims` a-j).
- Architecture: `Planning~/features/joined-rig-recogniser/architecture.md` (register *Staging*, *Stage layering*, *The player loop*, *Delivery*, *The clock* — now with the K + 3 horizon ruling, *Guards at this layer*, *Trace*).
- Requirements: `Planning~/features/joined-rig-recogniser/requirements.md` (F3, F4, §2 Out 1, §8, §10 item 1).
- Rig: `Planning~/features/coverage-in-selection/ab-rig/` (`UnityStub.cs`, `stage.sh`, `Joined.cs`, `Program.cs`, `Rig.csproj`; new `BridgeNative.cs`, `ModelExtractor.cs`, `JoinedRecogniser.cs`).
- Recon record: `.scratch/joined-rig-recogniser-phase-1-recon.md` (trust it).
- Dump for criterion 6 (parser-layer `main`-vs-`main`): `.scratch/joined/p4.jsonl` (exists, gitignored).
- DocCheck: `Planning~/verification-recipe.md` `## Parser A/B rig — DocCheck`; memory `doccheck-run-procedure` (`stage.sh` under `env -u MSYS_NO_PATHCONV`).

## Constraints & decisions

- Horizon ruled by the maintainer 2026-10-15: K + 3 frames (recorded in the register's *The clock*).
- No edit to any shipped `Runtime/` source (§2 Out 1, §8); a canary edits only a `.scratch/` staged copy, never a worktree.
- Commit set: the eight ab-rig files, the plan/architecture if touched, memory notes; nothing under `.scratch/` or `Runtime/`; no CHANGELOG line. Steps 1-6 are one check-in.
- Implement's first gate checks the runtime claims a-j (e.g. stand-in `ModelExtractor` returns a completed task so the drive stays on thread 1; shipped sources compile unedited against the stub; `dotnet build` defines `DEBUG`; `Main` on `ManagedThreadId == 1`). A failed claim that contradicts the plan is a stop, not a patch.
- No binding command (Commands 1-3) runs in Phase 1.
- Agents never run git writes; commits are the PM's per the `vc` binding. Subagents return reports as text; the PM files them under `.scratch/`. Every brief names the project root and says to write by it if `pwd -W` differs. Compiler/test output goes to `compile-check`, never the main thread.
- Windows-native only (Git Bash, .NET SDK 10); never WSL.

## Next steps

1. Invoke `implement` for feature `joined-rig-recogniser`, Phase 1 (plan `Planning~/features/joined-rig-recogniser/plan-1.md`, validated).

## Open questions

- None open. Phase 2 (later) needs the host Editor closed for Commands 1-2.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-joined-rig-recogniser; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 828d2703-fd21-4445-9e19-fda7e5fc4db2". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
