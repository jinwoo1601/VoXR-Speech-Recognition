You're picking up: plan Phase 2 of `feat-resolver-slot-exemption` through `phase-pickup`, then build it. Read the files below, then start at Next steps.

## Objective

Run `phase-pickup` for Phase 2 of `feat-resolver-slot-exemption` (persist and validate `Planning~/features/resolver-slot-exemption/plan-2.md`, linked from the architecture doc's `## Build plan`), then `implement` Phase 2. Done = the A4L reproduction report (or a recorded reason it was not run) filed, discharging F10, and the plan committed on the branch.

## Lane

feature

## Context

- Repo `D:\Workspace\VoXR-Speech-Recognition` (Unity package `com.jinwoo1601.voxr`), one checkout (`trees` is none); `memory/` lives here. Branch `feat-resolver-slot-exemption`, head `4a0bae9`, pushed to `origin`.
- Issue #161; design `resolver-slot-scoring` locked at G1 2026-10-02 (`Planning~/design-docs/scoring-model.md` Amendment A5: DR-8, DR-9). Track **Full**. Requirements F1-F11 with SETTLE rulings R1-R4 (R4: the A4L reproduction is a Should); architecture with the PLAN ruling (Phases 0-4 approved as proposed).
- Phase 2, per the architecture doc `## Build plan`: non-shipping - restage the lab rig from the worktree, pass the set through the constructor, run the lab's `occ`, `thin`, full-utterance, corpus, prefix-verdict and start-probe passes; the key's round count by the diff described in the Decision register; report against `lab-2026-10-02.md`'s A4L column. Writes only under `.scratch/`. No source change, so Commands 1-2 are not re-run. Discharges F10.

## Current state

- Phase 0 `18a7d66`: the seam (mask, snapshot, `MatchResult.ExemptedSlots`, DR-9 key).
- Phase 1 `4a0bae9` (plan `cceb1f6`, `plan-1.md`, validated go-with-fixes, fixes applied): DR-8 in `TryMatchScored` and `ScoreFollowUp`, comments, F1-F6 tests, allocation twin, A5 re-pins, DocCheck's two pins (0.400, 1.000). Gate: EditMode 190/190, PlayMode 701/701, DocCheck 66/66. Mutation checks ran red as intended (the `bestMissedRequiredSlot` guard; an allocation in the snapshot).
- The lab rig `.scratch/lab-resolver-slot-scoring/` is present (2026-10-02: `Lab.cs`, `a4l.patch`, corpus files, `analyze.py`, `brief.md`).

## Key files & locations

- `Planning~/features/resolver-slot-exemption/requirements.md` (F10), `architecture.md` (`## Decision register` - the rig entry and the key's round-count diff; `## Build plan` Phase 2; `## Risks`), `plan-0.md`, `plan-1.md` (form).
- `Planning~/design-docs/resolver-slot-scoring/lab-2026-10-02.md` (the A4L column to report against).
- Lab rig `.scratch/lab-resolver-slot-scoring/`; DocCheck/ab-rig `Planning~/features/coverage-in-selection/ab-rig/` (`stage.sh`).
- Phase 1 records: `.scratch/*resolver-slot-exemption-phase-1*`, `.scratch/implement-resolver-slot-exemption-1-*`, `.scratch/compile-check-resolver-slot-exemption-1*`.

## Constraints & decisions

- The locked design, A5 and the requirements doc are immutable; a contradiction found while building stops and reopens them on a design branch.
- **Staging the rig in this environment:** Claude Code's Git Bash sets `MSYS_NO_PATHCONV=1`, so `stage.sh` must run as `env -u MSYS_NO_PATHCONV bash "<path>/stage.sh" WORKTREE .scratch/<out>`; run it under WSL and it fails on its CRLF working copy. Build and run with WSL `dotnet` (`wsl.exe -e bash -lc 'cd "/mnt/d/..." && dotnet build -v q --nologo && dotnet run --no-build -- <flag>'`). Put this in every brief that stages a rig.
- Phase 2 writes only under `.scratch/` (gitignored); its report is the record. If the lab rig turns out unusable, F10 records "not run" with the reason - that is an allowed outcome per the Build plan.
- No CHANGELOG line per phase; the feature's line is written at close-out. Phase 1's suggested line: "Parser: a missed required slot with a registered resolver no longer costs score (DR-8), the follow-up re-score agrees, and DocCheck's two resolver claims now read 0.400 and 1.000."
- No product docs until G2. Never stage untracked `.claude/csharpier/` or `memory/handoffs/2026-10-02-feat-resolver-slot-exemption-g0.md`.
- No report-filer hook runs here: file each agent's report beside its brief by hand (the report arrives as a SubagentHandback tool call in `~/.claude/projects/<project>/<session>/subagents/agent-<id>.jsonl`).

## Next steps

1. Invoke `phase-pickup` for Phase 2 of `feat-resolver-slot-exemption`.
2. Then invoke `implement` for Phase 2.

## Open questions

- `plan-1.md` criterion 12 states a DocCheck command that does not work here (see Constraints); record the working procedure at the feature's as-built reconcile pass (`architect-reconcile`), not by editing the plan now.
- PR list for the A5 re-pins: `.scratch/implement-resolver-slot-exemption-1-report.md` ("Pre-existing tests edited") - carry into the PR body at G2.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-resolver-slot-exemption; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps. BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer.
