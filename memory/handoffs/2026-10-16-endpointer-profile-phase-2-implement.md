You're picking up: build Phase 2 (conf record) of `feat-endpointer-profile` through `implement`. Read the files below, then start at Next steps.

## Objective

Run `implement` for Phase 2 of `endpointer-profile` from its persisted, validated plan `Planning~/features/endpointer-profile/plan-2.md`, through `code-writer`, `compile-check` and `doc-writer` per the plan's Dispatch. The task is done when the phase is built, its gate is green, and it is checked in.

## Lane

feature

## Context

- Branch `feat-endpointer-profile`, the one checkout (`trees` is none), head `f5aedc7`, pushed. STATUS `## Current` says Phase 2 planned and next `implement` Phase 2. The session note is `memory/2026-10-16.md`; its last entries are "Phase 2 pickup (planned)" and this handoff.
- Requirements: `Planning~/features/endpointer-profile/requirements.md` (F1, F2, F3). Architecture: `Planning~/features/endpointer-profile/architecture.md` (`## Build plan` Phase 2; `## Decision register` "Conf record" and "The rig"). Design (locked): `Planning~/design-docs/endpointer-profile.md`.
- The human approved plan-2 on 2026-10-16, before check-in.

## Current state

- `plan-2.md` (10,195 B) was validated at full depth, then re-validated narrow after fix pass 1; fix pass 2 was spot-checked. No canon conflicts.
- Nothing of Phase 2 is built yet. `NativeBridge~/harness/expectations.json` has no `modelConf` key, and neither does the rig.
- The plan's trail is in `.scratch/`:
  - `code-mapper-…-phase-2-report.md` and `doc-extractor-…-phase-2-report.md` (recon);
  - `endpointer-profile-phase-2-stated-plan.md`;
  - `architect-…-phase-2-{,fix-,fix2-}report.md`;
  - `plan-validator-…-phase-2-{,fix-}report.md`, which carry the PM rulings.
- The Phase 1 implement brief, `.scratch/implement-endpointer-profile-phase-1-brief.md`, is the form precedent, including its `## Granted commands`.

## Key files & locations

- Plan: `Planning~/features/endpointer-profile/plan-2.md`. Its Steps 1-6, Dispatch, the One change, the Harness messages M1-M3, Step 4's probes a-f, and `## Runtime claims` a-u.
- Harness: `NativeBridge~/harness/main.cpp`, `NativeBridge~/harness/expectations.json`, `NativeBridge~/harness/README.md`.
- Rig: `Planning~/features/coverage-in-selection/ab-rig/` (`JoinedInput.cs`, `Joined.cs`, `JoinedReport.cs`, `joined.sh`).
- Verification: `.claude/bindings/unity.md` `## verification` Command 3, whose green is 37/37. DocCheck is in `Planning~/verification-recipe.md` :222-232, with its command at :227.

## Constraints & decisions

- F1: `modelConf` is written into the pin only by `--write-baseline`. A diff beyond `modelConf` plus the `libvosk` comma, or a moved `expectedFinals`, stops for the human. It is never a re-pin.
- On a Step 3 stop, the writer leaves the pin as the harness wrote it. The restore (`git checkout -- NativeBridge~/harness/expectations.json`) is the PM's, and only after the human's ruling. No agent runs a git write; agents may run git reads only where the brief grants them.
- The implement brief owes three things that the validators left to it:
  - a Bash timeout for Step 4's one invocation (two 37-case `--dump` runs plus two cold `stage.sh`/`dotnet build` passes);
  - the DocCheck command quoted for `compile-check`, since it is not a bound Command;
  - per-dispatch `## Granted commands` for Steps 3-4, git reads only, as Phase 1's brief had.
- Step 3's pin edit and Step 2's `PinKeys` edit must land in one change, because the rig today rejects an unknown pin key.
- `expectations.json` and `manifest.json` are CRLF throughout and stay so. Windows-native in Git Bash only, never WSL.
- Compile and test output goes to `compile-check`. File every agent's report by hand beside its brief in `.scratch/`.
- No Unity run is planned (Commands 1-2 are out of scope). If one happens anyway, commit the `memory/**/*.meta` files it leaves; never delete them.
- The design is locked. A contradiction reopens it on a `design-` branch and is never patched.
- `.scratch/endpointer-profile-phase-2/` must not exist when Step 4 starts.
- The architecture doc's as-built reconcile happens at the build plan's end, not per phase. `plan-2` Blast radius lists the `joined-rig.md` rule 2 wording and the stale `:143-195` cite for it.

## Next steps

1. Invoke `implement` for `feat-endpointer-profile` Phase 2 (`plan-2.md`).

## Open questions

- None known.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-endpointer-profile; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 349167e7-66d3-4328-9231-bd948bfb5c76". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
