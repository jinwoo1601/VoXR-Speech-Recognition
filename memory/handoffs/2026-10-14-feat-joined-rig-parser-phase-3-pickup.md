You're picking up: `phase-pickup` Phase 3 of `feat-joined-rig-parser`. Read the files below, then start at Next steps.

## Objective

Plan Phase 3 (scorer and report) of `feat-joined-rig-parser` through `phase-pickup`: recon, plan, persist as `Planning~/features/joined-rig-parser/plan-3.md`, validate, reconcile, commit and push. Done means the plan is persisted and validated, committed, and STATUS names `implement` Phase 3 next.

## Lane

feature

## Context

- Feature `joined-rig-parser`, Full track; design `joined-rig` G1-locked 2026-10-13. Branch `feat-joined-rig-parser` in the one checkout (`trees` none), pushed to `origin` at `371596c` plus this handoff's commit.
- Phase 1 (harness `--dump`) built at `d5e500d`; Phase 2 (rig inputs and guards) built at `371596c`, gate green (criterion 0 grammar 133/133, EditMode 203/203, PlayMode 704/704, DocCheck 66/66, harness 16/16, joined run exit 0, canary exit 3, seven negatives exit 2; runtime claims 12/12).
- Phase 3's row is in `Planning~/features/joined-rig-parser/architecture.md` `## Build plan` (line ~140): `JoinedScorer.cs`, `JoinedReport.cs`, `report`, sealed and `--evaluate`; discharges F3-F8, requirements section 10 item 6. Its unrecorded behaviour to prove (line ~148): `Parse` returns its selection order, best first; the rig reaches the internal `HasUnfilledRequiredSlot`.

## Current state

- No Phase 3 recon, plan or code exists. `memory/STATUS.md` says next `phase-pickup` Phase 3.
- Phase 2 left `Joined.cs` step 6 printing counts and `--vary parser` scoring waiting for Phase 3's `JoinedScorer`; `joined.sh` rejects `--evaluate` (exit 2) and has no `report` shape yet.
- Phase 2 reports, for reference only: `.scratch/implement-joined-rig-parser-phase-2-report.md`, `.scratch/compile-check-joined-rig-parser-phase-2-gate-report.md`.

## Key files & locations

- Architecture: `Planning~/features/joined-rig-parser/architecture.md` (40 KB budget). Requirements: `Planning~/features/joined-rig-parser/requirements.md`. Plans: `plan-1.md`, `plan-2.md` beside them (Phase 2's plan is the nearest template; plan budget 10 KB).
- Rig: `Planning~/features/coverage-in-selection/ab-rig/` (`Joined.cs`, `JoinedInput.cs`, `joined.sh`, `Program.cs`, `stage.sh`, `Rig.csproj`, `DocCheck.cs`). Manifest `Tests~/Fixtures/audio/manifest.json`; pin `NativeBridge~/harness/expectations.json`; recipe `Planning~/verification-recipe.md` (`## Parser A/B rig - DocCheck`).
- Real artefacts: `.scratch/joined/p2.jsonl` (dump), `.scratch/joined/p2-run/scored-{a,b}.jsonl` (untracked).

## Constraints & decisions

- Maintainer ruling 2026-10-14, "Exit 2, reader stop": pin/dump case sets unequal stop in the readers (exit 2); the kill check's exit 3 is configuration, finals and failed decodes.
- Grammar unequal (rig vs pin) and harness exit 1 stop for the human: never a re-pin. `expectations.json` is never edited.
- `Runtime/**`, `NativeBridge~/**`, `DocCheck.cs` and the binding's Commands stay untouched unless the plan's row says otherwise; Phase 3's F5 canary seeds a regression in a worktree parser source, restored by the PM with `git checkout --` (a PM step).
- The PM writes only `memory/`, CHANGELOG lines and `.scratch/` briefs; plans by `architect`, code by `code-writer`, builds and tests by `compile-check`. Git writes are PM steps between dispatches.
- Unity runs generate `memory/**/*.meta`: commit them, never delete. Commands 1-2 need the host Editor closed (`D:\Workspace\VoXR TestGround\Temp\UnityLockfile` absent).
- Name the project root `D:/Workspace/VoXR-Speech-Recognition` explicitly in every brief: this session's cwd drifted into `.scratch/` and an agent's `pwd -W` printed the subdirectory.
- New rig files are LF in the working copy; `core.autocrlf=true` will check them out CRLF (deferral WND-2) - `stage.sh` is proven on CRLF, `joined.sh` is not yet.
- No CHANGELOG line (dev tooling).

## Next steps

1. Invoke `phase-pickup` for Phase 3 of `feat-joined-rig-parser`.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-joined-rig-parser; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 03b45033-0256-4bf4-9676-3af5451303dc". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; send to the one on this machine. If the bare name is refused as ambiguous, use the ref `ListAgents` shows for the local one.)
