You're picking up: plan Phase 4 of `feat-resolver-slot-exemption` through `phase-pickup`, then build it. Read the files below, then start at Next steps.

## Objective

1. Run `phase-pickup` for Phase 4 of `feat-resolver-slot-exemption`. That persists and validates `Planning~/features/resolver-slot-exemption/plan-4.md`, linked from the architecture doc's `## Build plan`.
2. Then run `implement` for Phase 4.

Done means Phase 4 is built and committed with its gate green (Commands 1 and 2), discharging F11. The manual Editor check is human-only and is reported as deferred, never claimed.

## Lane

feature

## Context

- Repo `D:\Workspace\VoXR-Speech-Recognition`, the Unity package `com.jinwoo1601.voxr`. There is one checkout (`trees` is none), and `memory/` lives here.
- Branch `feat-resolver-slot-exemption`, head `492398a`, pushed to `origin`. The memory commit recording this handoff follows it.
- Issue #161. The design `resolver-slot-scoring` was locked at G1 on 2026-10-02 (`Planning~/design-docs/scoring-model.md` Amendment A5: DR-8, DR-9). Track **Full**.
- Requirements F1-F11 carry the SETTLE rulings R1-R4. R2 makes F11, the Batch Test window field, a **Could**. The architecture doc carries the PLAN ruling: Phases 0-4 approved as proposed.
- Phase 4, per the architecture doc's `## Build plan`: the Batch Test window field.
  - A serialized `string[] registeredSlotNames`, drawn beside `activeSetNames` and passed by `CreateRunner`.
  - Green: Commands 1-2. The manual Editor check is human-only and reported as deferred. *Discharges:* F11.
- Phase 4 is the last build phase. After it, the feature goes to review (`review-cycle`), then G2 (`g2-accept`), which is not part of this brief's objective.

## Current state

- Phase 0 (`18a7d66`): the seam. Phase 1 (`4a0bae9`): DR-8 in the parser. Phase 2 (outputs gitignored): the A4L reproduction, F10.
- Phase 3 is built at `492398a` (plan `1ad5835`, validated narrow with no findings). F7 is discharged.
  - Both public `VoxrBatchTestRunner` constructors take a trailing `string[] registeredSlotNames = null`.
  - The verdict is three-way: `would ask resolver for '...'` / `required slot unfilled`.
  - Gate: EditMode 197/197, PlayMode 702/702, the Samples~ compile green (compile set 7), its mutation red then green.
  - Records: `memory/2026-10-03.md` and `.scratch/*resolver-slot-exemption*phase-3*` / `.scratch/*resolver-slot-exemption-3*`.

## Key files & locations

- `Planning~/features/resolver-slot-exemption/requirements.md` (F11, R2).
- `Planning~/features/resolver-slot-exemption/architecture.md`: `## Components & responsibilities` (the `VoxrBatchTestWindow` bullet) and `## Build plan` (Phase 4).
- `plan-3.md` and earlier plans, for their form.
- `Editor/VoxrBatchTestWindow.cs`: `activeSetNames` (~:21) and `CreateRunner` (:326-360), which calls constructor B at :344 and :352, positionally.
- `Tests~/Editor/VoxrBatchTestWindowCoverageWeightTests.cs` :91-103 reaches `CreateRunner` by reflection.
- `Runtime/Testing/VoxrBatchTestRunner.cs`: the Phase 3 constructors.
- Bindings: `.claude/bindings/unity.md` (`verification`) and `.claude/bindings/core.md` (`process-docs`, `layout`, `session-launch`).

## Constraints & decisions

- The locked design, A5 and the requirements doc are immutable. A contradiction found while building stops the work and reopens them on a design branch.
- Phase 4 touches `Editor/` only, with no `Runtime/` public API and no `Samples~/`, so Command 4 should not apply. Confirm this against the plan.
- **Subagent report files are refused.** Briefs have the agent return its report as text. The PM files it beside the brief from the `SubagentHandback` call in `~/.claude/projects/D--Workspace-VoXR-Speech-Recognition/<session>/subagents/agent-<id>.jsonl`.
  - In Git Bash, pass Python that path in its `C:/Users/...` form, since `MSYS_NO_PATHCONV=1` is set.
  - A report filer also wrote `.scratch/reports/compile-check-<id>-report.md` this session.
- **Subagents may not change version-control state.** The vc-guard hook refused `git checkout --` from `compile-check`. Any mutation-check revert in a gate brief is a byte restore from a backup, with a Python byte compare and read-only git.
- **Byte checks.** Use a Python byte comparison, never `grep -v | cmp` in Git Bash.
- No CHANGELOG line per phase; the feature's line is written at close-out. The suggested lines so far:
  - Phase 1: "Parser: a missed required slot with a registered resolver no longer costs score (DR-8), the follow-up re-score agrees, and DocCheck's two resolver claims now read 0.400 and 1.000."
  - Phase 3: "VoxrBatchTestRunner accepts the game's registered slot names (registeredSlotNames on both constructors): it scores a registered slot's miss as the runtime does and reports "would ask resolver for '...'" instead of "required slot unfilled" (#161)."
- No product docs until G2. Never stage the untracked `.claude/csharpier/` or `memory/handoffs/2026-10-02-feat-resolver-slot-exemption-g0.md`.

## Next steps

1. Invoke `phase-pickup` for Phase 4 of `feat-resolver-slot-exemption`.
2. Then invoke `implement` for Phase 4.

## Open questions

These are carried to the feature's as-built reconcile pass (`architect-reconcile`), not for Phase 4:
- `plan-1.md` criterion 12's DocCheck command does not work here.
- Phase 2's record-only probe and its other departures from the row (`memory/2026-10-03.md`).
- `probe` mode needs `BuildCoverageTables`.
- The A5 re-pin list for the PR body is in `.scratch/implement-resolver-slot-exemption-1-report.md` ("Pre-existing tests edited"). Carry it into the PR at G2.
- `Planning~/verification-recipe.md` `## Compiling Samples~` reverts its mutation with `git checkout --`, which a subagent may not run. Phase 3 used a byte restore. The recipe also points to a nonexistent `.claude/verification-bindings.md`. Both are for a light-lane fix, not this feature.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-resolver-slot-exemption; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps. BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer.
