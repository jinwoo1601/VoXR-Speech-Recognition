You're picking up: implement Phase 7 (default flip and re-pin) of `feat-endpointer-profile` through `implement`. Read the files below, then start at Next steps.

## Objective

Run `implement` for Phase 7 of `endpointer-profile` from the persisted, validated plan `Planning~/features/endpointer-profile/plan-7.md`. Done when Gate A is green, the human has approved the re-pin at the plan's review stop, the standing pin has been re-pinned by the harness, Gate B is green, and the CHANGELOG line is written and checked, all on evidence. Then the build is committed and the as-built pass is through, as `implement` says.

## Lane

feature

## Context

- Branch `feat-endpointer-profile`, the one checkout (`trees` is none). Head `68015d0` plus this handoff's commit, pushed.
- STATUS `## Current`: Phase 7 planned; next `implement` Phase 7. Session note `memory/2026-10-17.md`; its last entries are "Phase 7 pickup (planned)" and this handoff.
- Requirements: `Planning~/features/endpointer-profile/requirements.md` (F6, F10, F11, §9).
- Architecture doc: `Planning~/features/endpointer-profile/architecture.md`, register entry "Flip and re-pin", the *Tuning record* (`Responsive`: rule2 `0.45`, rule3 and rule4 the archive's own), and Risks "Tier B under the tuned default".
- The human approved the plan in plan mode on 2026-10-17. It was validated at full depth, with one fix pass. Its departures from the Build-plan row are D1-D6, and two rulings apply:
  - R1: the CHANGELOG line lands in Phase 7, written by the PM.
  - R2: Command 3's trigger widens to the sources `confgen` links, and recipe :19 follows.
- Phase 8 (Quest 3, human-only) follows. If it fails, Phase 7 is reverted whole.

## Current state

- Phases 1-6 are built and committed. Nothing of Phase 7's code or docs is written.
- Phase 6 gate baseline: EditMode 230/230, PlayMode 715/715, Command 3 37/37 on the old pin.
- Records sit in `.scratch/` (ignored; read only): `code-mapper-`, `doc-extractor-`, `plan-validator-` and `architect-endpointer-profile-phase-7-{,fix-}*` files, plus `endpointer-profile-phase-7-approved-plan.md`.

## Key files & locations

- Plan: `Planning~/features/endpointer-profile/plan-7.md` (10,216 B, under the 10 KB budget). Its Steps are:
  1. `code-writer`: code, tests and the probes `review.py` / `runline.py`.
  2. Gate A (`compile-check`).
  3. The review stop: the PM runs `review.py`, then the human approves.
  4. The standing re-pin (`compile-check`).
  5. `doc-writer` at sonnet: the binding, the README and the recipe.
  6. Gate B (`compile-check`).
  7. The PM's CHANGELOG line, then Gate B check 3 again.
- Code and tests:
  - `Runtime/EndpointerOverlay.cs` `Resolve`: the Continuous return and its flip-site comment.
  - `Tests~/Editor/EndpointerOverlayTests.cs` `Resolve_AutoContinuous_ModelDefault`.
  - `Tests~/Runtime/VoxrEndpointerProfileLoadTests.cs` `Load_Default_LogsLine`.
- Pin and harness:
  - `NativeBridge~/harness/expectations.json`, written only by a harness `--write-baseline` run.
  - `NativeBridge~/harness/main.cpp`.
  - `NativeBridge~/harness/confgen/` (dll at `NativeBridge~/build-confgen/bin/confgen/debug/confgen.dll`; pre-flip, so it must be rebuilt).
- Doc sites, all CRLF:
  - `.claude/bindings/unity.md` Command 3 and Prerequisites.
  - `NativeBridge~/harness/README.md` (Toolchain, the run line, "What each part does", re-baselining).
  - `Planning~/verification-recipe.md` (:19, :21).
  - `CHANGELOG.md` `[Unreleased]` `### Changed`.
- Precedent implement run: `.scratch/implement-endpointer-profile-phase-6-brief.md` and its `-report.md`, and `.scratch/compile-check-endpointer-profile-phase-6-gate-brief.md`.

## Constraints & decisions

- **The re-pin is the human's reviewed act.** Nothing writes the standing `harness/expectations.json` before the human approves at Step 3.
  - A joined-words change, a Command 3 exit 1, or any red under the flip other than D1's two tests stops for the human.
  - That stop is never a re-pin and never a test edit.
- **Runtime claims a-j** go to implement's first gate, as plan-7's `## Runtime claims` says.
- **Build facts:**
  - The agents' Bash tool collapses `\\` to `\`, so probes are backslash-free, with Python in `.py` files under `python3 -I`.
  - `compile-check` has no Write tool, so `code-writer` writes the probes.
  - The vc-guard hook refuses VC-state changes to agents.
  - Everything runs Windows-native in Git Bash, never WSL.
  - Briefs forbid adding a pipe, redirect or capture file to plan commands.
  - Unity-made `memory/handoffs/*.meta` are committed with the phase.
- **Writer choices held for G2** are not reopened: `memory/2026-10-16.md` (plan-3) and `memory/2026-10-17.md` (plan-4, plan-6).
- **Owed to the end-of-build reconcile, not Phase 7's:** B1-B4; the stale cites, including the architecture's README `:61-68`/`:68`; `joined-rig.md` rule 2; plan-6's line anchors.
- **Process:**
  - Compile, test and harness output go to agents.
  - Reports are filed beside their briefs. Where the hook does not file them, extract the hand-back from `subagents/agent-<id>.jsonl`.
  - A contradiction with locked canon reopens it on a `design-` branch.
- **Background session:** one named `feat-joined-rig-parser` may be listed at this root. It is not this branch's. If it writes in this tree, stop and tell the human.

## Next steps

1. Invoke `implement` for `feat-endpointer-profile` Phase 7 (plan `Planning~/features/endpointer-profile/plan-7.md`).

## Open questions

- None known. The review stop at Step 3 is the human's by plan.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-endpointer-profile; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 9be56d48-108d-40e3-933e-a5fd969121f8". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
