You're picking up: implement Phase 3 (rig sweep machinery) of `feat-endpointer-profile` through `implement`. Read the files below, then start at Next steps.

## Objective

Run `implement` for Phase 3 of `endpointer-profile`, exactly as `Planning~/features/endpointer-profile/plan-3.md` says, through its green gate. The task is done when Phase 3 is built, its gate is green, and the build is checked in.

## Lane

feature

## Context

- Branch `feat-endpointer-profile`, the one checkout (`trees` is none), head `7e933ac` (pushed). STATUS `## Current` says Phase 3 planned and next `implement` Phase 3. The session note is `memory/2026-10-16.md`; its last entries are "Phase 3 pickup (planned)" and this handoff.
- Requirements: `Planning~/features/endpointer-profile/requirements.md` (F4). Architecture: `Planning~/features/endpointer-profile/architecture.md` (`## Decision register` *Buffer setups — a staged-copy edit*, *Kill criteria in the report*; `## Build plan` Phase 3). The design is locked: `Planning~/design-docs/endpointer-profile.md`.
- The plan is validated: narrow, one fix pass, approved by the human 2026-10-16. Its readings R1-R4 are approved with it.

## Current state

- Phases 1-2 are built (`2aed6fa`, `9617e63`). Phase 3 is planned and checked in (`7e933ac`). None of Phase 3's code exists yet.
- Pickup trail in `.scratch/`: `code-mapper-`, `doc-extractor-`, `architect-`, `plan-validator-endpointer-profile-phase-3-*` (briefs and reports), `architect-endpointer-profile-phase-3-fix-*`, and `endpointer-profile-phase-3-stated-plan.md`.
- There is no `plan-validator` fix-pass report. The fix pass was not re-validated, and `implement`'s first gate takes the plan's runtime claims a-l.

## Key files & locations

- The plan: `Planning~/features/endpointer-profile/plan-3.md` (10,162 B). It changes `stage.sh`, `joined.sh` and `JoinedReport.cs` under `Planning~/features/coverage-in-selection/ab-rig/`. All three are CRLF and must stay CRLF.
- Green dumps (untracked, must exist): `.scratch/endpointer-profile-phase-2/a.jsonl` (vendor, pinned) and `b.jsonl` (rule2 0.35). Probe area: `.scratch/endpointer-profile-phase-3/`.
- DocCheck: `Planning~/verification-recipe.md` :222-232, command at :227. It was green at 66/66 after Phase 2.
- Precedent for the build trail: `.scratch/implement-endpointer-profile-phase-2-*`, `.scratch/compile-check-endpointer-profile-phase-2-gate-*`.

## Constraints & decisions

- Probe-writing lessons (also in plan-3 Step 4):
  - The agents' Bash tool collapses `\\` to `\`, so probe commands must be backslash-free, with Python in `.py` files run by `python3 -I`.
  - In Git Bash, `grep -c $'\r$'` prints 0 on CRLF files.
  - A writer runs only the plan's checks inside a single-invocation probe.
- Subagents may not write under `.scratch/` in some runs (note of 2026-10-03), though Phase 2's `code-writer` did. Plan-3 has `compile-check` write its `.py` probes there, so say so in that brief, or have `code-writer` write them.
- `git init` in a temp dir outside the project (check 5) is not in a protected tree, so vc-guard allows it. Never edit `Runtime/`, `Joined.cs`, `JoinedInput.cs`, or the Phase 2 dumps.
- Compile and test output goes to `compile-check`. File every agent's report by hand beside its brief in `.scratch/`. Windows-native in Git Bash only, never WSL.
- A contradiction with the locked design reopens it on a `design-` branch; it is never patched.
- The as-built reconcile happens at the build plan's end. It carries plan-2's Blast-radius items (the `joined-rig.md` rule 2 wording, the stale `:143-195` cite), the Bash-tool facts, and stale rig cites found in Phase 3 recon: `Joined.cs` kill check :214-277, not :214-266; dump-header copy :318-322, not :307-311; `JoinedInput.cs` :153-161 and :606-608, not :151 and :587-592.

## Next steps

1. Invoke `implement` for `feat-endpointer-profile` Phase 3.

## Open questions

- None known.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-endpointer-profile; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor bd397bce-8ea4-4a17-ba63-88a14a3a7ef6". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
