You're picking up: plan Phase 7 (default flip and re-pin) of `feat-endpointer-profile` through `phase-pickup`. Read the files below, then start at Next steps.

## Objective

Run `phase-pickup` for Phase 7 of `endpointer-profile`. Done when `Planning~/features/endpointer-profile/plan-7.md` is persisted, validated by `plan-validator`, linked from the architecture doc's `## Build plan`, and committed. Stop there; `implement` is the next session's.

## Lane

feature

## Context

- Branch `feat-endpointer-profile`, the one checkout (`trees` is none). Head `d91ef92` (Phase 6 build) plus this handoff's commit, pushed.
- STATUS `## Current`: Phase 6 built; next `phase-pickup` Phase 7. Session note `memory/2026-10-17.md`; its last entries are "Phase 6 implement (built)" and this handoff.
- Architecture doc: `Planning~/features/endpointer-profile/architecture.md`.
  - `## Build plan` Phase 7 row: `Automatic` in Continuous → the tuned preset; the reviewed re-pin; `doc-writer`: Command 3, the README, the CHANGELOG line. Green: Commands 1-2 (Tier B under the tuned default); Command 3 in its new form. Discharges F6 (Continuous default), F10.
  - Register entries *Presets*, *Mode at load*, *Load-time log*, *`confgen`*, *The rig*; the *Tuning record* (one preset, `Responsive`, rule2 `0.45`, rule3/rule4 the archive's own; it is the Continuous default at the flip).
  - Phase 8 (Quest 3, human-only) follows; a fail there reverts Phase 7 (adr-0002 (A)).
- Requirements: `Planning~/features/endpointer-profile/requirements.md` (F6, F10, §9).
- Design: `Planning~/design-docs/endpointer-profile.md` (locked), adr-0001 to adr-0004 under `Planning~/design-docs/endpointer-profile/decisions/`.

## Current state

- Phases 1-6 are built and committed. Phase 6 gate: EditMode 230/230, PlayMode 715/715, Command 4 green with mutation, Command 3 37/37 on the unchanged pin, DocCheck 66/66, rig staged at worktree and `b255105`.
- Nothing of Phase 7 is planned or written.

## Key files & locations

- Flip site: `Runtime/EndpointerOverlay.cs` `Resolve` — the Continuous return carries the flip-site comment (Phase 6 writer choice).
- Tests that pin today's default and will move with the flip: `Tests~/Editor/EndpointerOverlayTests.cs` `Resolve_AutoContinuous_ModelDefault`; `Tests~/Runtime/VoxrEndpointerProfileLoadTests.cs` `Load_Default_LogsLine` (expects rule3 `0.75` / rule4 `1.0`, the vendor conf).
- Pin: `NativeBridge~/harness/expectations.json`; harness `NativeBridge~/harness/main.cpp`; `confgen --profile/--mode` in `NativeBridge~/harness/confgen/Program.cs` (built at `NativeBridge~/build-confgen/bin/confgen/debug/confgen.dll`).
- Plans for shape: `plan-4.md` (pin and `confgen` checks), `plan-6.md` (latest code phase, Step 8 gate shape).
- Phase 6 trail (ignored, read only): `.scratch/implement-endpointer-profile-phase-6-{brief,report}.md`, `.scratch/compile-check-endpointer-profile-phase-6-gate-{brief,report}.md`. The writer's suggested CHANGELOG line is at the end of the implement report.
- Bindings: `.claude/bindings/unity.md` `## verification` (Commands 1-4; Command 3's text is what Phase 7's `doc-writer` changes); `Planning~/verification-recipe.md` (DocCheck :222-232); `.claude/bindings/core.md` `## process-docs` (plan 10 KB, 11 KB for plan-6 only by ruling in `memory/doctor-rulings.md`; brief 15 KB).

## Constraints & decisions

- A re-pin is the human's reviewed act: never `--write-baseline` on the standing pin without the plan's review step; a Command 3 mismatch stops for the human.
- Writer choices held for G2 are not reopened: `memory/2026-10-16.md` (plan-3), `memory/2026-10-17.md` (plan-4, plan-6).
- Owed to the end-of-build reconcile, not Phase 7's: B1-B4; stale cites (stage.sh closure, STAGED_FROM, "Overlay seam" register cites); `joined-rig.md` rule 2 wording; plan-6's line anchors against the landed tree.
- Build facts:
  - The agents' Bash tool collapses `\\` to `\`: probes backslash-free, Python in `.py` files under `python3 -I`.
  - `compile-check` has no Write tool; the vc-guard hook refuses VC-state changes to agents (restore files by probe, not `git checkout`).
  - Windows-native in Git Bash, never WSL; `stage.sh` under `env -u MSYS_NO_PATHCONV`; `stage.sh` is CRLF in the working tree.
  - Briefs forbid adding pipes, redirects or capture files to plan commands; Phase 6's gate still piped one rebuild through `| tail -5` — state the rule in the plan's Rules and in each brief.
  - The host manifest already carries `"testables"`; Unity makes `memory/handoffs/*.meta` on each test run, which are committed with the phase (precedent).
- Process: compile, test and rig output go to agents; reports are filed beside their briefs; a contradiction with locked canon reopens it on a `design-` branch, never patched.
- Background session `feat-joined-rig-parser` may be listed at this root; it is not this branch's. If it writes in this tree, stop and tell the human.

## Next steps

1. Invoke `phase-pickup` for `feat-endpointer-profile` Phase 7.

## Open questions

- Whether Phase 7's CHANGELOG line lands before G2: the Phase 7 row names it; the 2026-10-17 tuning-ruling-2 entry says no CHANGELOG line on this branch before G2. Ask the human at the plan.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-endpointer-profile; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 8dbfe84a-0d76-48c0-b912-96516ed3f3cd". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
