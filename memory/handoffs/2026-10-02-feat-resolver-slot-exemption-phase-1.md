You're picking up: plan Phase 1 of `feat-resolver-slot-exemption` through `phase-pickup`, then build it. Read the files below, then start at Next steps.

## Objective

Run `phase-pickup` for Phase 1 of `feat-resolver-slot-exemption` (persist and validate `Planning~/features/resolver-slot-exemption/plan-1.md`, linked from the architecture doc's `## Build plan`), then `implement` Phase 1. Done = Phase 1 built, green by the `verification` binding's Commands 1-2 and DocCheck at 66/66, and committed on the branch.

## Lane

feature

## Context

- Repo `D:\Workspace\VoXR-Speech-Recognition` (Unity package `com.jinwoo1601.voxr`), one checkout (`trees` is none); `memory/` lives here. Branch `feat-resolver-slot-exemption`, head `18a7d66`, pushed to `origin`.
- Issue #161; design `resolver-slot-scoring` locked at G1 2026-10-02 (`Planning~/design-docs/scoring-model.md` Amendment A5: DR-8, DR-9). Track **Full**. Requirements F1-F11 with SETTLE rulings R1-R4; architecture with the PLAN ruling (Phases 0-4 approved as proposed).
- Phase 1, per the architecture doc `## Build plan`: the DR-8 guarded arithmetic in `TryMatchScored` and `ScoreFollowUp` (reading the Phase 0 mask), the code comments the rule makes false, the F1-F6/F8/F9 tests, the allocation twin, the F5 re-pins (each traced to A5, listed in the PR), and DocCheck's two blocks and two expectations run to 66/66. Discharges F1, F2, F3, F4, F5, F6, F8, F9.

## Current state

- Phase 0 done 2026-10-02: plan `6c81f4d` (`plan-0.md`, validated go-with-fixes, fixes applied); build `18a7d66` - `IRegisteredSlotNames`/`RegisteredSlotNames` in `VoxrCommandParser.cs`, `DynamicSlotManager` implements it explicitly, parser ctor `registeredSlots` (last, default null), `_exemptSlot`/`_anyExempt` mask, `SnapshotRegisteredSlots()` first in `BuildCoverageTables` and `ScoreFollowUp`, `MatchResult.ExemptedSlots` (byte, padding hole, size pin `MatchResult_Size_IsThirtyTwoBytes`), `CompareCandidate`'s required `bestExemptedSlots` threaded through flush, eager and the Editor runner-up, five new comparator tests. Gate: EditMode 190/190, PlayMode 691/691.
- Nothing writes a non-zero `ExemptedSlots` yet and nothing reads the mask - that is Phase 1.

## Key files & locations

- `Planning~/features/resolver-slot-exemption/requirements.md`; `architecture.md` (`## Decision register`, `## Data contract & runtime flow`, `## Non-functional realization`, `## Build plan` Phase 1); `plan-0.md` (`## Blast radius` hand-forward bullets).
- Code: `Runtime/Commands/VoxrCommandParser.cs` (`TryMatchScored`, `ScoreFollowUp`, `SnapshotRegisteredSlots`, `CompareCandidate`); `Runtime/Commands/VoxrCommandRecogniser.cs`; tests `Tests~/Runtime/VoxrCommandParserTests.cs`, `Tests~/Runtime/VoxrEagerCommitTests.cs`, `Tests~/Runtime/VoxrCommandRecogniserInjectionTests.cs`. Line anchors moved in Phase 0 - re-map, do not reuse old ones.
- DocCheck rig: `Planning~/features/coverage-in-selection/ab-rig/` (`DocCheck.cs`, `stage.sh`); the run procedure is in `Planning~/verification-recipe.md` and the architecture doc.
- Phase 0 records: `.scratch/*resolver-slot-exemption-phase-0*`, `.scratch/implement-resolver-slot-exemption-0-*`, `.scratch/compile-check-resolver-slot-exemption-0-*`.
- Verification: `.claude/bindings/unity.md` `## verification`.

## Constraints & decisions

- The locked design, A5 and the requirements doc are immutable; a contradiction found while building stops and reopens them on a design branch.
- `TryMatchScored`'s and `TryEagerCommit`'s signatures stay as they are (DocCheck and Sweep reflect on them).
- Unity batchmode runs leave untracked `.meta` orphans, including under `memory/` (`memory/handoffs.meta`, `memory/handoffs/*.md.meta`, `memory/<date>.md.meta`); every gate brief must have them removed after the revert, never a tracked `.meta`.
- No report-filer hook runs here: file each agent's report beside its brief by hand (recon full reports extracted from `~/.claude/projects/<project>/<session>/subagents/agent-<id>.jsonl`).
- No CHANGELOG line per phase; the feature's line is written at close-out. Phase 0's suggested line: "Add the registered-slot seam (IRegisteredSlotNames, MatchResult.ExemptedSlots, fewer-exempted comparator key) to the command parser, with no behaviour change yet (#161)".
- No product docs until G2. Never stage untracked `.claude/csharpier/` or `memory/handoffs/2026-10-02-feat-resolver-slot-exemption-g0.md` (left untracked by an earlier session; not this branch's to commit without the human's word).

## Next steps

1. Invoke `phase-pickup` for Phase 1 of `feat-resolver-slot-exemption`.
2. Then invoke `implement` for Phase 1.

## Open questions

- Phase 0 left `DynamicSlotManager.cs`'s header `Depends:` line not naming `IRegisteredSlotNames` (the plan said "nothing else"); fold it into Phase 1 or the review - PM's call.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-resolver-slot-exemption; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps. BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer.
