You're picking up: plan Phase 3 of `feat-resolver-slot-exemption` through `phase-pickup`, then build it. Read the files below, then start at Next steps.

## Objective

1. Run `phase-pickup` for Phase 3 of `feat-resolver-slot-exemption`. That persists and validates `Planning~/features/resolver-slot-exemption/plan-3.md`, linked from the architecture doc's `## Build plan`.
2. Then run `implement` for Phase 3.

Done means Phase 3 is built and committed with its gate green (Commands 1, 2 and 4), discharging F7.

## Lane

feature

## Context

- Repo `D:\Workspace\VoXR-Speech-Recognition`, the Unity package `com.jinwoo1601.voxr`. There is one checkout (`trees` is none), and `memory/` lives here.
- Branch `feat-resolver-slot-exemption`, head `ceedf43`, pushed to `origin`. The memory commit recording this handoff follows it.
- Issue #161. The design `resolver-slot-scoring` was locked at G1 on 2026-10-02 (`Planning~/design-docs/scoring-model.md` Amendment A5: DR-8, DR-9). Track **Full**.
- Requirements F1-F11 carry the SETTLE rulings R1-R4. The architecture doc carries the PLAN ruling: Phases 0-4 approved as proposed.
- Phase 3, per the architecture doc's `## Build plan`: the batch runner given names.
  - Both constructors take `registeredSlotNames`.
  - The completeness verdict becomes three-way.
  - The known-gap comment in `VoxrBatchTestRunner.cs` (~:200-217) is rewritten to the new contract.
  - Runner tests cover:
    - score parity with the runtime;
    - the verdict, with `required slot unfilled` kept when an unfilled slot is unregistered;
    - no-names behaviour unchanged.
  - Green: Commands 1, 2 and 4, since Phase 3 changes public API. *Discharges:* F7.

## Current state

- Phase 0 (`18a7d66`): the seam — the mask, the snapshot, `MatchResult.ExemptedSlots`, the DR-9 key.
- Phase 1 (`4a0bae9`, plan `cceb1f6`): DR-8 in `TryMatchScored` and `ScoreFollowUp`, plus tests. Gate: EditMode 190/190, PlayMode 701/701, DocCheck 66/66.
- Phase 2 (plan `ceedf43`; outputs gitignored, so nothing else to commit): the A4L reproduction against the built parser.
  - All 28 F10 figures match the lab's A4L column, including key rounds 14 by all three methods.
  - The report is `.scratch/lab-resolver-slot-exemption/report.md`, and F10 is discharged.
  - Gate: DocCheck 66/66, probe inertness byte-exact, `compare.py` exit 0.
  - Records: `memory/2026-10-03.md`, and `.scratch/*resolver-slot-exemption*phase-2*` and `.scratch/*resolver-slot-exemption-2*`.

## Key files & locations

- `Planning~/features/resolver-slot-exemption/requirements.md` (F7, §9).
- `Planning~/features/resolver-slot-exemption/architecture.md`: `## Decision register`, `## Build plan` (Phase 3), and `## Risks` (the "Stale after this build" bullet names `VoxrBatchTestRunner.cs` :200-217 for Phase 3).
- The plans `plan-0.md`, `plan-1.md` and `plan-2.md`, for their form.
- `Runtime/Commands/VoxrCommandParser.cs`: the `registeredSlots:` constructor parameter, and the `internal` `IRegisteredSlotNames` / `RegisteredSlotNames` near :142-165.
- `VoxrBatchTestRunner.cs`. Locate it by recon; it is the runner Phase 3 changes.
- Bindings: `.claude/bindings/unity.md` (`verification` — Commands 1, 2 and 4 and the platform slots) and `.claude/bindings/core.md` (`process-docs`, `layout`, `session-launch`).

## Constraints & decisions

- The locked design, A5 and the requirements doc are immutable. A contradiction found while building stops the work and reopens them on a design branch.
- Phase 3 changes public API, so Command 4 (the Samples~ compile in WSL, per `Planning~/verification-recipe.md`) runs at its gate along with Commands 1-2.
- **Staging any rig here.** Claude Code's Git Bash sets `MSYS_NO_PATHCONV=1`, so run `stage.sh` as `env -u MSYS_NO_PATHCONV bash "<path>/stage.sh" WORKTREE .scratch/<out>`. Under WSL it fails on its CRLF working copy. Build and run with WSL `dotnet`.
- **Subagent report files are refused.** A harness policy refuses a subagent's write of a report file. Briefs should have the agent return report content as text, and the PM files it beside the brief (or at the named path) from the agent's `SubagentHandback` call in `~/.claude/projects/<project>/<session>/subagents/agent-<id>.jsonl`. No report-filer hook runs here.
- **Byte checks.** A `grep -v | cmp` byte check in Git Bash strips CR from CRLF files and fails falsely. Gate briefs should use a Python byte comparison.
- No CHANGELOG line per phase; the feature's line is written at close-out. The suggested lines so far:
  - Phase 1: "Parser: a missed required slot with a registered resolver no longer costs score (DR-8), the follow-up re-score agrees, and DocCheck's two resolver claims now read 0.400 and 1.000."
  - Phase 2: none, since it is non-shipping.
- No product docs until G2. Never stage the untracked `.claude/csharpier/` or `memory/handoffs/2026-10-02-feat-resolver-slot-exemption-g0.md`.

## Next steps

1. Invoke `phase-pickup` for Phase 3 of `feat-resolver-slot-exemption`.
2. Then invoke `implement` for Phase 3.

## Open questions

These are carried to the feature's as-built reconcile pass (`architect-reconcile`), not for Phase 3:
- `plan-1.md` criterion 12's DocCheck command does not work here.
- Phase 2's record-only probe and its other departures from the row (`memory/2026-10-03.md`).
- `probe` mode needs `BuildCoverageTables`.
- The A5 re-pin list for the PR body is in `.scratch/implement-resolver-slot-exemption-1-report.md` ("Pre-existing tests edited"). Carry it into the PR at G2.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-resolver-slot-exemption; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps. BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer.
