You're picking up: `feat-endpointer-profile` Phase 8, the human-only Quest 3 check, from the human's run through to the G2 ruling. Read the files below, then start at Next steps.

## Objective

1. Receive the human's Phase 8 report (`Planning~/features/endpointer-profile/plan-8.md`, Quest 3) and rule pass or fail with the human against the plan's Pass and Fail rules (:66-72).
2. On a pass, take the feature to G2 (`g2-accept`), with the end-of-build reconcile and a `review-cycle` done first. On a fail, stop: Phase 7 is reverted only on the human's ruling.

Done when the human has ruled at G2, or has ruled on a Phase 8 fail.

## Lane

feature

## Context

- One checkout (`trees` is none), `D:\Workspace\VoXR-Speech-Recognition`, branch `feat-endpointer-profile`, head `285589c`, pushed. Full track.
- Phases 1-7 built; Phase 7 (`100a987`) flipped the Continuous default to `Responsive` (rule2 0.45). Phase 8 gates the flip (adr-0002, option A): F11, plus §8's arm64 conf assumption.
- Requirements §9 re-locked at G1 2026-10-17 (`022912a`): "fires are not perceptibly later, sooner where perceptible", eager commit on for both runs. `plan-8.md` matches.
- 2026-10-23 and 2026-10-27: `main` merged in twice (harness upgrades PR #180, #181; last merge `d557a72`); the 15 V23-stale composed skills re-spliced by `upgrade` (`285589c`; snapshot `d557a72`; validate and doctor PASS).
- STATUS `## Current` line: "next: human runs Phase 8 (Quest 3, `plan-8.md`)". Session notes: `memory/2026-10-17.md` (re-lock, Phase 8 plan), `memory/2026-10-27.md` (merge, re-splice).

## Current state

- Done: `plan-8.md` persisted, validated, fixed; the architecture doc's Phase 8 row and `## Build plan` link line.
- Not started: the human's Quest run; any Phase 8 result record; the end-of-build as-built reconcile; `review-cycle`; G2.
- **No feature-wide `review-cycle` has run** (confirmed 2026-10-23: no endpointer review collation in `.scratch/`, none in the session notes). It runs after a pass, before `g2-accept`.
- `.scratch/endpointer-profile-phase-8/` does not exist yet; plan-8's Prerequisites line `mkdir -p`s it.

## Key files & locations

- `Planning~/features/endpointer-profile/plan-8.md`: Prerequisites (eager commit ticked in both builds; check the Inspector before each build); Steps 1-6; Pass and Fail (:66-72); `Report to the PM` (:74); `## Runtime claims`.
- Evidence: `.scratch/endpointer-profile-phase-8/` (ignored) - `logcat-tuned.txt`, `logcat-model-default.txt`, `tuned-model.conf`, `archive.conf`, `model-default-model.conf`.
- `Planning~/features/endpointer-profile/requirements.md`: §9 (:105), F11 (:78).
- `Planning~/features/endpointer-profile/architecture.md`: Phase 8 row (about :164), sweep record (about :106-124), Risks (about :180).

## Constraints & decisions

- **Human-only.** No agent runs adb, builds or deploys (`.claude/bindings/unity.md` Failure rule); the Quest result is never claimed by an agent.
- **D1-D4 stand** (plan-8 `Departures and rulings`): panels for the session log; Model default a second build by `adb install -r` with P3 by `cmp`; D3's pass/fail reading; host setup the human's in `D:\Workspace\VoXR TestGround`.
- A re-extraction at Step 5 makes P3 indeterminate, not passed: the human rules a rerun from Step 1.
- A fail reverts Phase 7 (`100a987`) whole, on the human's ruling only; Phase 6's opt-in state then stands.
- **Held for the end-of-build reconcile:** the writer choices of plans 4, 6, 7 (the "Writer choices ... held for G2" bullets in `memory/2026-10-17.md`); B1-B4 (boundary reading, `memory/2026-10-16.md:421`); stale cites; `joined-rig.md` rule 2; the line anchors of plan-6 and plan-7.
- Build facts: Windows-native Git Bash only, never WSL; vc-guard refuses VC-state changes to agents; Unity-made `memory/handoffs/*.meta` are committed with the work; STATUS lines capped at 200 characters; `git commit -F` takes a Windows-form path (`C:/...`), not `/c/...`.

## Next steps

1. Ask the human for the Phase 8 report in the shape of plan-8's `Report to the PM`. Do not start before it arrives.
2. Rule pass or fail with the human; record the result and the evidence paths in the architecture doc by `architect-reconcile` (record pass). Where it goes (Phase 8 row or a new record section) is the PM's call, tier 2.
3. Pass: `architecture-doc` reconcile mode carrying the held items; then `review-cycle` over the feature branch; then `g2-accept`.
4. Fail: stop for the human's ruling on reverting Phase 7.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-endpointer-profile; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor b16d6fbb-5c75-4cd9-b6d8-16ee4917c615". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
