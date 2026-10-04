You're picking up: the review of `feat-resolver-slot-exemption` through `review-cycle`, then G2 through `g2-accept`. Read the files below, then start at Next steps.

## Objective

1. Run `review-cycle` over the whole feature branch `feat-resolver-slot-exemption` against `main` (merge base `4c7ebb6`), at full depth since G2 is at stake. Fix CONFIRMED findings by default and list them at the gate.
2. Then run `g2-accept`. G2 is the human's ruling, a hard stop: never cross it on your own judgment.

Done means the review is closed with its findings ruled, and the feature is presented for G2. Product docs, the CHANGELOG line, the PR and the merge follow only after the human accepts.

## Lane

feature

## Context

- Repo `D:\Workspace\VoXR-Speech-Recognition`, the Unity package `com.jinwoo1601.voxr`. There is one checkout (`trees` is none), and `memory/` lives here.
- Branch `feat-resolver-slot-exemption`, pushed to `origin`. Code head is `9bbc7af`, and the memory commits recording this handoff follow it. There are 18 commits over `origin/main`.
- Issue #161. The design `resolver-slot-scoring` was locked at G1 on 2026-10-02 (`Planning~/design-docs/scoring-model.md` Amendment A5: DR-8, DR-9). Track **Full**.
- Requirements F1-F11 carry SETTLE rulings R1-R4. F1-F9 are Must. F10 (the A4L reproduction) is Should, per R4. F11 (the Batch Test window field) is Could, per R2. The architecture doc carries the PLAN ruling: Phases 0-4 approved as proposed.
- Hot paths per the `review` binding (`.claude/bindings/core.md`): `Runtime/Commands/**` is one, so the parser change gets the efficiency angle.

## Current state

All build phases are done and gated:

| Phase | Code commit | What it built | Discharges |
|---|---|---|---|
| 0 | `18a7d66` | the seam | |
| 1 | `4a0bae9` | DR-8 in the parser | |
| 2 | outputs gitignored | the A4L reproduction | F10 |
| 3 | `492398a` | `VoxrBatchTestRunner` takes `string[] registeredSlotNames = null` on both ctors and gives the three-way verdict | F7 |
| 4 | `9bbc7af` | `VoxrBatchTestWindow` "Registered Slots" field, passed by `CreateRunner` | F11 |

- **Last gate (Phase 4):** EditMode 201/201 and PlayMode 702/702. The mutation went red, then green after a byte restore.
- **Session records:** `memory/2026-10-03.md` (Phase 3 and Phase 4 entries). Briefs and reports are under `.scratch/*resolver-slot-exemption*` (gitignored).
- **Not done:** the review; the as-built reconcile of `architecture.md` (`architect-reconcile`); the manual Editor check for F11; everything after G2.

## Key files & locations

- Process docs:
  - `Planning~/features/resolver-slot-exemption/requirements.md` (§5 F1-F11, §9 G2 acceptance criteria)
  - `architecture.md` (`## Decision register`, `## Build plan` with links to `plan-0.md`..`plan-4.md`, `## Risks` "Stale after this build")
- Changed source, from `git diff --name-status origin/main..HEAD`:
  - `Runtime/Commands/VoxrCommandParser.cs`, `DynamicSlotManager.cs`, `PendingCommandHandler.cs`, `VoxrCommandRecogniser.cs`
  - `Runtime/Testing/VoxrBatchTestRunner.cs`
  - `Editor/VoxrBatchTestWindow.cs`
  - `Planning~/features/coverage-in-selection/ab-rig/DocCheck.cs`
- Changed tests:
  - `Tests~/Editor/VoxrBatchTestRunnerTests.cs`, `VoxrBatchTestWindowRegisteredSlotsTests.cs` (new)
  - `Tests~/Runtime/VoxrCommandParserTests.cs`, `VoxrCommandRecogniserInjectionTests.cs`, `VoxrDynamicSlotTests.cs`, `VoxrPendingCommandTests.cs`
- Bindings: `.claude/bindings/unity.md` (`verification`), `.claude/bindings/core.md` (`review`, `process-docs`, `session-launch`), `.claude/bindings/vc-git.md` (`vc`: PR by `gh pr create`; Claude never merges), `.claude/bindings/codex.md` (product docs).

## Constraints & decisions

- The locked design, A5, the ADRs and the requirements doc are immutable. A finding that contradicts them stops the work and reopens them on a design branch, never a silent patch.
- **Subagent report files are refused.** Briefs have the agent return its report as text. The PM files it beside the brief from the hand-back in `C:/Users/excal/.claude/projects/D--Workspace-VoXR-Speech-Recognition/<session-id>/subagents/agent-<id>.jsonl`, as the tool_use input of the `*Handback*` call.
- **Subagents may not change version-control state** (the vc-guard hook refuses it). Any mutation check in a gate brief reverts by a byte restore from a backup, confirmed by a Python byte compare. Never use `git checkout --` or `grep -v | cmp`.
- Compiler and test output runs in `compile-check`, never on the main thread.
- A CSharpier formatter hook runs on edits. In Phase 4 it reformatted the touched lines of `VoxrBatchTestWindow.cs` (the field over two lines, one ctor argument per line) unlike their one-line neighbours. This is cosmetic; the review rules on it.
- No product docs before the human's G2. Never stage the untracked `.claude/csharpier/` or `memory/handoffs/2026-10-02-feat-resolver-slot-exemption-g0.md`.
- CHANGELOG is one line for the feature, written at close-out. Suggested phase lines:
  - Phase 1: "Parser: a missed required slot with a registered resolver no longer costs score (DR-8), the follow-up re-score agrees, and DocCheck's two resolver claims now read 0.400 and 1.000."
  - Phase 3: "VoxrBatchTestRunner accepts the game's registered slot names (registeredSlotNames on both constructors): it scores a registered slot's miss as the runtime does and reports "would ask resolver for '...'" instead of "required slot unfilled" (#161)."
  - Phase 4: "Batch Test window: a "Registered Slots" list passes the game's registered slot names to the runner, so a run scores and reports them as the runtime does (F11, #161)."

## Next steps

1. Invoke `review-cycle` on `feat-resolver-slot-exemption`, range `origin/main..HEAD`, full depth.
2. Then invoke `g2-accept` for `feat-resolver-slot-exemption`.

## Open questions

- **The manual Editor check for F11** is the human's and is still undone: open Window > VoXR > Batch Test Runner and confirm three things.
  - The "Registered Slots" list and its tooltip appear under "Active Sets".
  - A case that leaves a registered required slot unfilled shows `would ask resolver for '...'`.
  - The entry survives a script recompile.

  Ask for its result at G2. Requirements §9 accepts "F11 built, or recorded as not built".
- For the as-built reconcile pass (`architect-reconcile`), carried from earlier phases:
  - `plan-1.md` criterion 12's DocCheck command does not work here.
  - Phase 2's record-only probe and its other departures from the row (`memory/2026-10-03.md`).
  - `probe` mode needs `BuildCoverageTables`.
  - Phase 4's added test file, a departure approved in plan mode 2026-10-03.
- The A5 re-pin list for the PR body is in `.scratch/implement-resolver-slot-exemption-1-report.md` ("Pre-existing tests edited"). Carry it into the PR at G2.
- The product-doc scope is not reconciled. Architecture `## Risks` names `Documentation~/api/batch-test-runner.md` and `editor-testing.md`. Requirements §2 In item 6 names only `scoring.md`, `command-recognition.md`, `KNOWN_LIMITATIONS.md` and `CHANGELOG.md`. Settle this at G2.
- `Planning~/verification-recipe.md` `## Compiling Samples~` reverts its mutation with `git checkout --`, which a subagent may not run, and it points to a nonexistent `.claude/verification-bindings.md`. Both are for a light-lane fix, not this feature.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-resolver-slot-exemption; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps. BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer.
