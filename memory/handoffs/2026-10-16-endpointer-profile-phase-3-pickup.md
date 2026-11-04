You're picking up: plan Phase 3 (rig sweep machinery) of `feat-endpointer-profile` through `phase-pickup`. Read the files below, then start at Next steps.

## Objective

Run `phase-pickup` for Phase 3 of `endpointer-profile`: recon, plan, persist `Planning~/features/endpointer-profile/plan-3.md`, validate it, and have the human approve it before check-in. The task is done when plan-3 is persisted, validated, approved and checked in.

## Lane

feature

## Context

- Branch `feat-endpointer-profile`, the one checkout (`trees` is none), head `9617e63`, pushed. STATUS `## Current` says Phase 2 built and next `phase-pickup` Phase 3. The session note is `memory/2026-10-16.md`; its last entry is "Phase 2 implement (built)", followed by this handoff.
- Requirements: `Planning~/features/endpointer-profile/requirements.md` (F4). Architecture: `Planning~/features/endpointer-profile/architecture.md`, `## Build plan` Phase 3 (:144) and `## Decision register` (*Sweep measures*, *The rig*). Design (locked): `Planning~/design-docs/endpointer-profile.md`.
- Phase 3 scope, from the build plan: `--buffer`, `--window`, the staged edit, *Sweep measures*. Green: DocCheck; one dump against itself in all four setups (the edit in config, delta-f 0, KC1 pass, KC2 empty); a `.scratch/` B dump with one word changed gives a KC2 STOP; an unmatched initializer stops the stage. No binding command. Discharges F4 (machinery).

## Current state

- Phases 1-2 are built and checked in: `2aed6fa` (pause-ladder corpus) and `9617e63` (conf record).
- Phase 2's rig changes are in `Planning~/features/coverage-in-selection/ab-rig/` (`JoinedInput.cs`, `Joined.cs`, `JoinedReport.cs`). The pin `NativeBridge~/harness/expectations.json` now carries `modelConf`; every pin the rig reads must have it.
- Phase 2's plan and build trail: `Planning~/features/endpointer-profile/plan-2.md`; the `.scratch/*endpointer-profile-phase-2*` briefs and reports.
- Nothing of Phase 3 exists yet.

## Key files & locations

- Rig: `Planning~/features/coverage-in-selection/ab-rig/` (`joined.sh`, `stage.sh`, `Joined.cs`, `JoinedInput.cs`, `JoinedReport.cs`, `DocCheck`).
- DocCheck: `Planning~/verification-recipe.md` :222-232, command at :227. It was green at 66/66 after Phase 2.
- Precedent for plan form and size: `plan-2.md` (10,170 B; the plan-file budget is 10,240 B).

## Constraints & decisions

- **Probe-writing lessons from Phase 2:**
  - The agents' Bash tool collapses `\\` to `\` before bash runs a command, so plan probes must be backslash-free wherever a doubled backslash would be needed.
  - In Git Bash, `grep -c $'\r$'` prints 0 on CRLF files. Use `grep -U -c`.
  - Writers must run only the plan's checks inside a single-invocation probe: an extra line nested in `"$(...)"` broke attempt 2.
- `expectations.json` and `manifest.json` are CRLF throughout and must stay CRLF. Windows-native in Git Bash only, never WSL.
- Compile and test output goes to `compile-check`. File every agent's report by hand beside its brief in `.scratch/`.
- The design is locked. A contradiction reopens it on a `design-` branch; it is never patched.
- The architecture doc's as-built reconcile happens at the build plan's end. It carries plan-2's Blast radius items (the `joined-rig.md` rule 2 wording, the stale `:143-195` cite) and the Bash-tool facts above.

## Next steps

1. Invoke `phase-pickup` for `feat-endpointer-profile` Phase 3.

## Open questions

- None known.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-endpointer-profile; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 52d0ac5b-3f07-45e2-839f-ec0f08498690". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
