You're picking up: `phase-pickup` Phase 2 of `feat-joined-rig-parser`. Read the files below, then start at Next steps.

## Objective

Plan Phase 2 (inputs and guards) of `feat-joined-rig-parser`, then persist and validate the plan as `Planning~/features/joined-rig-parser/plan-2.md`. Done means `plan-2.md` is persisted, validated, linked from the architecture doc's `## Build plan`, committed and pushed, and STATUS names `implement` Phase 2 as next.

## Lane

feature

## Context

- Feature `joined-rig-parser`, Full track; design `joined-rig` G1-locked 2026-10-13. Branch `feat-joined-rig-parser` in the one checkout (`trees` none), pushed to `origin` at `d5e500d`.
- Phase 1 (the harness `--dump` writer) is built and committed at `d5e500d`: plan criteria 1-8 green. `memory/STATUS.md` says: stage implement, Phase 1 built; next `phase-pickup` Phase 2.
- Phase 2's row in the architecture doc's `## Build plan`: "First the PM's `Rig.csproj` check (*Risks*)", then `JoinedInput.cs`, guard and kill check in `Joined.cs`, `score`, the `Program.cs` dispatch, `stage.sh` list, `joined.sh`, the manifest's `provenance` and split tag, the recipe clause; its Green list and *Discharges* are in that row.

## Current state

- Phase 1 runtime facts now in the architecture doc (human ruling 2026-10-14, "Record as-built"): Vosk word times on finals run on across `vosk_recognizer_reset` with a run-dependent per-case origin, so they are exact within a case only. Partials are `{"partial":…}` only. Two dumps of one build hold equal final texts and equal partial sequences. Nothing in `Runtime/` or the rig reads `VoxrWord.StartTime`/`EndTime`. Exact per-case origin joins deferral JRP-1 (`joined-rig-recogniser`'s).
- A real dump for Phase 2's readers to bind to: `.scratch/joined/p1.jsonl` (and `p1b.jsonl`), untracked; the checker `.scratch/joined/check_p1.py` shows the record shapes.
- Nothing of Phase 2 is started.

## Key files & locations

- Requirements: `Planning~/features/joined-rig-parser/requirements.md` (F2, F8, F11, the NFRs, §10 items 2, 3, 5 are Phase 2's).
- Architecture: `Planning~/features/joined-rig-parser/architecture.md` — `## Decision register` (*Schema*, *Kill check*, *Audio time*), *Data contract*, `## Non-functional realization` (*Input boundary*), `## Build plan` Phase 2 row, `## Risks` (the `Rig.csproj` bullet). 31,824 bytes against a 40 KB budget.
- Phase 1 plan as precedent: `Planning~/features/joined-rig-parser/plan-1.md` (9,936 bytes; plan-file budget 10 KB).
- The rig: `Planning~/features/coverage-in-selection/ab-rig/` (`stage.sh`, `Program.cs`, `Rig.csproj`); DocCheck procedure in `Planning~/verification-recipe.md` (and memory `doccheck-run-procedure`).
- Dump writer: `NativeBridge~/harness/main.cpp` (as of `d5e500d`).

## Constraints & decisions

- `Runtime/**`, `NativeBridge~/src/**`, `expectations.json` and the binding's Commands stay untouched; the grammar is never re-pinned — a grammar mismatch stops for the human.
- `Rig.csproj` check first (architecture `## Risks`): if `git ls-files` prints nothing for `Planning~/features/coverage-in-selection/ab-rig/Rig.csproj`, Phase 2's commit force-adds it (`git add -f`).
- The PM writes only `memory/`, CHANGELOG lines and `.scratch/` briefs; plans by `architect`, validation by `plan-validator`, code by `code-writer`, builds and tests by `compile-check`.
- Unity runs generate `memory/**/*.meta`: commit them, never delete. Commands 1-2 need the host Editor closed (`D:\Workspace\VoXR TestGround\Temp\UnityLockfile` absent).

## Next steps

1. Invoke `phase-pickup` for Phase 2 of `feat-joined-rig-parser`.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-joined-rig-parser; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 4106f588-b134-43d5-a1ea-e1443f3623a8". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; send to the one on this machine. If the bare name is refused as ambiguous, use the ref `ListAgents` shows for the local one.)
