You're picking up: `implement` Phase 3 of `feat-joined-rig-parser`. Read the files below, then start at Next steps.

## Objective

Build Phase 3 (scorer and report) of `feat-joined-rig-parser` through `implement`, exactly as `Planning~/features/joined-rig-parser/plan-3.md` says, through its gate (steps 1-9). Done means the gate is green, the phase is committed and pushed, and STATUS names the next step (`phase-pickup` Phase 4, after the architecture's as-built reconcile).

## Lane

feature

## Context

- Feature `joined-rig-parser`, Full track; design `joined-rig` G1-locked 2026-10-13. Branch `feat-joined-rig-parser` in the one checkout (`trees` none), project root `D:/Workspace/VoXR-Speech-Recognition`, pushed to `origin` at `b47df07` plus this handoff's commit.
- Phases 1 (`d5e500d`) and 2 (`371596c`) are built with green gates. Phase 3's plan was persisted, validated (full depth, twelve findings fixed) and committed at `b47df07`.
- STATUS: `memory/STATUS.md` line 8. Session note: `memory/2026-10-14.md`, last two entries.

## Current state

- `plan-3.md` (10,207 bytes) is validated, with no open questions. No Phase 3 code exists.
- Recon and validation, for reference only: `.scratch/code-mapper-joined-rig-parser-phase-3-report.md`, `.scratch/doc-extractor-joined-rig-parser-phase-3-report.md`, `.scratch/plan-validator-joined-rig-parser-phase-3-report.md`, `.scratch/architect-joined-rig-parser-phase-3-fix-report.md`.

## Key files & locations

- Plan: `Planning~/features/joined-rig-parser/plan-3.md`.
- Architecture: `Planning~/features/joined-rig-parser/architecture.md` (40 KB budget, 32 KB now).
- Requirements: `Planning~/features/joined-rig-parser/requirements.md`.
- Rig: `Planning~/features/coverage-in-selection/ab-rig/` (`Joined.cs`, `JoinedInput.cs`, `joined.sh`, `Program.cs`, `stage.sh`, plus the new `JoinedScorer.cs` and `JoinedReport.cs`).
- Real dump: `.scratch/joined/p2.jsonl` (untracked).
- Recipe: `Planning~/verification-recipe.md` `## Parser A/B rig - DocCheck` (:222-232). Binding: `.claude/bindings/unity.md` `## verification`.

## Constraints & decisions

- **Maintainer ruling at plan approval, 2026-10-14:** `Parse` returns its results in utterance order, not best first, and *The fire* stands. The architecture's *The fire* *Why*/*Rejected* and *Risks* "best first" wording, and its *Report* paragraph (it lacks the per-case listing), are corrected at the as-built pass, not during the build.
- **Approved departures from the row** (they are in the plan): `JoinedInput.cs`, `Program.cs` (fourth `Joined.Run` argument) and the `stage.sh` copy list.
- **Who runs the gate:**
  - Steps 1-5: `compile-check` on the clean tree.
  - Step 6: the canary, one `code-writer` dispatch after steps 1-5 are green.
  - Step 7: the PM runs `git checkout -- Runtime/Commands/VoxrCommandParser.cs`. The vc-guard refuses agents git writes.
  - Steps 8-9: the next `compile-check` dispatch.
- Keep the F5 canary's `report.md` in `.scratch/joined/` for the maintainer's F6 read. F6 is human-only verification at G2.
- `Runtime/**` (except the temporary canary), `NativeBridge~/**`, `expectations.json`, `DocCheck.cs`, the recipe and the binding all stay untouched. Grammar unequal or harness exit 1 is a stop for the human, never a re-pin.
- Unity runs generate `memory/**/*.meta`: commit them, never delete them. Commands 1-2 need the host Editor closed (`D:\Workspace\VoXR TestGround\Temp\UnityLockfile` absent).
- Name the project root `D:/Workspace/VoXR-Speech-Recognition` explicitly in every brief.
- Subagents cannot write `.scratch/` reports. Each report comes back as hand-back text. The PM files it from the subagent transcript under `C:/Users/excal/.claude/projects/D--Workspace-VoXR-Speech-Recognition/<session id>/subagents/agent-<id>.jsonl`; the tasks `.output` file is empty.
- `core.autocrlf=true`: new LF files check out CRLF (deferral WND-2).
- No CHANGELOG line (dev tooling). The untracked `.claude/references/vc-git/__pycache__/` predates this work: leave it.

## Next steps

1. Invoke `implement` for Phase 3 of `feat-joined-rig-parser` (plan `Planning~/features/joined-rig-parser/plan-3.md`).

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-joined-rig-parser; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 3c64faf3-e0c7-4d2f-bffc-7d5dfe8e31f3". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; send to the one on this machine. If the bare name is refused as ambiguous, use the ref `ListAgents` shows for the local one.)
