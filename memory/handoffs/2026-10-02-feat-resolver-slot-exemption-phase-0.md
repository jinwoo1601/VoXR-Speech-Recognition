You're picking up: plan Phase 0 of `feat-resolver-slot-exemption` through `phase-pickup`, then build it. Read the files below, then start at Next steps.

## Objective

Run `phase-pickup` for Phase 0 of `feat-resolver-slot-exemption` (persist and validate `Planning~/features/resolver-slot-exemption/plan-0.md`, linked from the architecture doc's `## Build plan`), then `implement` Phase 0. Done = Phase 0 built, green by the `verification` binding's Commands 1-2, and committed on the branch.

## Lane

feature

## Context

- Repo `D:\Workspace\VoXR-Speech-Recognition` (Unity package `com.jinwoo1601.voxr`), one checkout (`trees` is none); `memory/` lives here. Branch `feat-resolver-slot-exemption`, head `a4ce85c`, pushed to `origin`.
- Issue #161; design `resolver-slot-scoring` locked at G1 2026-10-02 (`scoring-model.md` Amendment A5: DR-8, DR-9). Track **Full**.
- Done on this branch, all 2026-10-02: G0 (`ed8d1f2`); requirements F1-F11 with the SETTLE rulings R1-R4 (`31dd42e`); the architecture doc with the PLAN ruling - Phases 0-4 approved as proposed, the runner verdict confirmed as a rejection reason (`a4ce85c`).
- Phase 0, per the architecture doc `## Build plan`: the seam with zero behaviour change - `IRegisteredSlotNames` and `RegisteredSlotNames`; `DynamicSlotManager` implements the interface; the parser's `registeredSlots` constructor parameter, mask and per-pass snapshot (in `BuildCoverageTables` and `ScoreFollowUp`); the recogniser passes `_slotManager` in `Configure` and `RebuildParser`; `MatchResult.ExemptedSlots` (byte, padding hole) plus the 32-byte size pin; `CompareCandidate`'s required `bestExemptedSlots` and the DR-9 key through flush, eager and the Editor runner-up; the direct comparator tests edited mechanically (pass 0); new comparator tests per key. Discharges F3's comparator half and the plumbing of F4 and F6.

## Key files & locations

- `Planning~/features/resolver-slot-exemption/requirements.md` - the contract (F1-F11, §6 NFRs, R1-R4).
- `Planning~/features/resolver-slot-exemption/architecture.md` - `## Decision register` (every Phase 0 choice and its rejected alternative), `## Data contract & runtime flow`, `## Non-functional realization`, `## Build plan`.
- `.scratch/recon-resolver-slot-exemption-code-2026-10-02.md` - code-mapper recon of every scoring site with line anchors (untracked scratch; verify against live code).
- Code: `Runtime/Commands/VoxrCommandParser.cs` (ctor ~466, `MatchResult` ~3325, `CompareCandidate` ~3457, `TryMatchScored` ~3617, `BuildCoverageTables` ~4083, `ScoreFollowUp` ~4494, `TryEagerCommit` ~4593, runner-up ~2747-2777); `Runtime/Commands/VoxrCommandRecogniser.cs` (`Configure` ~277, `RebuildParser` ~463); `Runtime/Commands/DynamicSlotManager.cs`; tests `Tests~/Runtime/VoxrCommandParserTests.cs` (`CompareCandidate_*` ~6341-6459).
- Verification: `.claude/bindings/unity.md` `## verification`; `Planning~/verification-recipe.md`.

## Constraints & decisions

- The locked design, A5 and both requirements docs are immutable; a contradiction found while building stops and reopens them on a design branch.
- Phase 0 must change no behaviour: nothing writes a non-zero exempt count yet, so every existing test passes save the mechanical comparator-test edits.
- `CompareCandidate`'s new parameter has no default (architecture decision register); `TryMatchScored`'s and `TryEagerCommit`'s signatures stay as they are (the DocCheck rig reflects on them).
- No product docs until G2. Never stage untracked `.claude/csharpier/`.

## Next steps

1. Invoke `phase-pickup` for Phase 0 of `feat-resolver-slot-exemption`.
2. Then invoke `implement` for Phase 0.

## Open questions

None open; Phases 1-4 follow in the ruled order.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-resolver-slot-exemption; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps. BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer.
