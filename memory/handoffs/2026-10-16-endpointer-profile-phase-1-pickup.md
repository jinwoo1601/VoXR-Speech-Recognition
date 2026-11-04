You're picking up: plan Phase 1 (pause-ladder corpus) of `feat-endpointer-profile` through `phase-pickup`, from its RECON step. Read the files below, then start at Next steps.

## Objective

Run `phase-pickup` for Phase 1 of feature `endpointer-profile`: recon, plan, persist the plan as `Planning~/features/endpointer-profile/plan-1.md` (with its link line in the architecture doc's `## Build plan`), validate, reconcile, commit. Done for this brief when the plan is persisted, validated and checked in; `implement` Phase 1 is the step after.

## Lane

feature

## Context

- Branch `feat-endpointer-profile`, checked out in the one checkout (`trees` is none), head `e02f784`, pushed. Stage: architecture done; PLAN ruled 2026-10-16 ("Approve as returned", 8 phases).
- Requirements: `Planning~/features/endpointer-profile/requirements.md` (F1–F12, all Must; §4.5 the ladder; §9 G2 checklist).
- Architecture: `Planning~/features/endpointer-profile/architecture.md` — `## Decision register` "Pause ladder — 21 split entries, pinned by the harness"; `### The pause ladder` (the phrases); `## Build plan` Phase 1 row; `## Risks` "Piper's edge silence".
- Design (locked G1): `Planning~/design-docs/endpointer-profile.md`, ADRs `Planning~/design-docs/endpointer-profile/decisions/adr-0001..0004`.
- STATUS `## Current` names the branch at stage architecture, next `phase-pickup` Phase 1; the session note is `memory/2026-10-16.md`.

## Current state

- ORIENT done this session: branch correct; tree clean but for untracked `.claude/csharpier/`, `.claude/references/vc-git/__pycache__/` (pre-existing, not ours) and Unity-made `memory/**/*.meta` (committed with this handoff). No plan file exists yet.
- Phase 1 row (as ruled): 21 manifest entries; WAVs; the band check; 21 `{file}` entries in `expectations.json`; `--write-baseline` on the vendor model; the Tier B skip of the `ladder` category; `doc-writer` edits the `unity` binding's Command 3 "16/16". Green: Command 3 at the new count with a harness-written pin diff; Commands 1–2. Discharges F1, §4.5, §10 item 2.
- Ladder phrases (ruled): P (0.2/0.3/0.4/0.6 s) "set heading two · seven zero", "orient heading two seven zero · mark one five", "orient heading one · eight zero"; M (0.3/0.6/0.9 s) "launch missiles target · hotel one", "fall back from · target bravo two", "approach · target alpha one".

## Key files & locations

- Code recon already done for the architecture (file:line, scope flags): `.scratch/endpointer-profile-arch-recon-collation.md` — Q5 harness, Q7 fixtures are Phase 1's.
- Fixtures: `Tests~/Fixtures/audio/manifest.json` (split entry form `:166-182`), `Tests~/Fixtures/generate.py` (`build_case` `:64-81`, `--only`), `Tests~/Fixtures/generate.sh` (venv, piper-tts 1.6.0).
- Harness: `NativeBridge~/harness/main.cpp` (drift guard `:224-243`, `--write-baseline` `:355-367`), `NativeBridge~/harness/expectations.json`, `NativeBridge~/harness/README.md` (re-baseline `:61-68`).
- Bindings: `.claude/bindings/core.md` (`process-docs`, `layout`, `session-launch`), `.claude/bindings/unity.md` (`verification` Commands 1–3), `.claude/bindings/vc-git.md`.
- Precedent briefs: `.scratch/architect-joined-rig-recogniser-phase-1-brief.md`.

## Constraints & decisions

- The design is locked; a contradiction reopens it on a `design-` branch, never a patch.
- F1: pinned values are written only by the harness's `--write-baseline`; the drift guard's bare `{file}` entries are not pins.
- Phase 1 stops for the human's ruling if piper's edge silence pushes a rung's effective pause across a sweep point (0.25/0.35/0.5).
- The piper venv and voice download is a human step (subagent downloads are refused): the plan names it.
- Windows-native only; never WSL. Agents never run git writes; compile and test output goes to `compile-check`.
- File every background agent's report by hand beside its brief in `.scratch/` (the filer misses them); briefs tell agents to return reports as text.
- Every brief names the project root `D:\Workspace\VoXR-Speech-Recognition` and says to write by it if `pwd -W` disagrees.
- STATUS lines stay at or under 200 characters; the architecture doc is at 33.8 KB of its 40 KB budget.
- A subagent's Unity run leaves `memory/**/*.meta`: commit them, never delete.

## Next steps

1. Invoke `phase-pickup` for `feat-endpointer-profile` Phase 1; ORIENT is done, so start at RECON.

## Open questions

- None for the human before PLAN; the band-check stop above may raise one during implementation.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-endpointer-profile; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor b4165bea-d56c-4bbd-98ce-bf4c55707320". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
