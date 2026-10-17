You're picking up: `feat-endpointer-profile` Phase 8, the human-only Quest 3 check, from the human's run through to the G2 ruling. Read the files below, then start at Next steps.

## Objective

1. The human runs Phase 8 on a Quest 3 per `Planning~/features/endpointer-profile/plan-8.md` and reports to you. You rule pass or fail with the human against the plan's Pass and Fail rules.
2. On a pass, take the feature to G2 (`g2-accept`), with the end-of-build reconcile and review that G2 needs done first. On a fail, stop: Phase 7 is reverted only on the human's ruling.

Done when the human has ruled at G2, or has ruled on a Phase 8 fail.

## Lane

feature

## Context

- One checkout (`trees` is none), at `D:\Workspace\VoXR-Speech-Recognition`, branch `feat-endpointer-profile`, head `f85eb85`, pushed. The feature is on the Full track.
- Phases 1-7 are built; Phase 7 (`100a987`) flipped the Continuous default to `Responsive` (rule2 0.45). Phase 8 is the human-only check: F11, plus §8's arm64 conf assumption. It gates the flip (adr-0002, option A).
- On 2026-10-17, requirements §9 was re-locked at G1 (merged back at `022912a`). The bar now reads "fires are not perceptibly later, sooner where perceptible", with eager commit on for both runs. `plan-8.md` was fixed to match (10,047 B; validator findings 2-8 applied).
- STATUS `## Current` reads "next: human runs Phase 8 (Quest 3, `plan-8.md`)". The session note is `memory/2026-10-17.md`; its last entries cover the re-lock and the Phase 8 RECONCILE.

## Current state

- **Done:**
  - `plan-8.md` persisted, validated and fixed.
  - The architecture doc's Phase 8 row names eager commit.
  - The Phase 8 link line is in `## Build plan`.
- **Not started:**
  - The human's Quest run.
  - Any Phase 8 result record.
  - The end-of-build as-built reconcile.
  - G2.
- **Review status unknown.** No feature-wide `review-cycle` record was found by grepping `memory/2026-10-16.md` and `memory/2026-10-17.md`. Confirm from the session notes before G2.

## Key files & locations

- `Planning~/features/endpointer-profile/plan-8.md`:
  - Prerequisites: eager commit ticked in both builds; check the Inspector before each build.
  - Steps 1-6; Step 3's ten commands, marked E or W (items 5 and 10 are W).
  - Step 5's re-extraction tell, `.voxr-archive-model.conf` `ls -l` time.
  - The Pass and Fail rules, about :66-72.
  - `Report to the PM` (:74).
  - `## Runtime claims`.
- The human's evidence lands in `.scratch/endpointer-profile-phase-8/` (ignored): logcat files, the pulled confs, the `diff`/`cmp` outputs.
- `Planning~/features/endpointer-profile/requirements.md`: §9 (:105), F11 (:78).
- `Planning~/features/endpointer-profile/architecture.md`: Phase 8 row (about :164), sweep record (about :106-124), Risks (about :180).

## Constraints & decisions

- **Human-only.** No agent runs adb, builds or deploys (`.claude/bindings/unity.md` Failure rule). The Quest result is never claimed by an agent.
- **D1-D4 stand** (plan-mode approval, 2026-10-17):
  - D1: the CommandLog/LastCommand panels stand for the session log.
  - D2: "Model default" is a second build installed by `adb install -r`, with P3 checked by `cmp`.
  - D3: the pass/fail reading.
  - D4: host setup is the human's, in `D:\Workspace\VoXR TestGround`.
- **Indeterminate P3.** A re-extraction at Step 5 makes P3 indeterminate, not passed. The human rules on a rerun from Step 1.
- **A fail** reverts Phase 7 (`100a987`) whole, on the human's ruling only. Phase 6's opt-in state then stands.
- **Held for G2, not yet reconciled:**
  - The writer choices of plans 4, 6 and 7 (the "Writer choices … held for G2" bullets in `memory/2026-10-17.md`).
  - The end-of-build reconcile items: B1-B4 (boundary reading, `memory/2026-10-16.md:421`); stale cites; `joined-rig.md` rule 2; the line anchors of plan-6 and plan-7.
- **Build facts:**
  - Windows-native Git Bash only, never WSL.
  - Agents' Bash collapses `\\` to `\`.
  - vc-guard refuses VC-state changes to agents.
  - Unity-made `memory/handoffs/*.meta` are committed with the work.
  - STATUS lines are capped at 200 characters.
- **Background session.** A session `feat-joined-rig-parser` may be listed at this root. It is not this branch's. If it writes in this tree, stop and tell the human.

## Next steps

1. Ask the human for the Phase 8 report, in the shape of plan-8's `Report to the PM`. Do not start before it arrives.
2. Rule pass or fail with the human against plan-8's Pass and Fail rules. Record the result and the evidence paths in the architecture doc by `architect-reconcile` (record pass).
3. **Pass:**
   - Run the end-of-build as-built reconcile, `architecture-doc` reconcile mode, carrying the items held for G2.
   - Run `review-cycle` over the feature branch, unless the notes show one was already run.
   - Then `g2-accept`.
4. **Fail:** stop for the human's ruling on reverting Phase 7.

## Open questions

- Whether a feature-wide `review-cycle` has already run. Confirm from the notes; it was not found by grep.
- Whether the Phase 8 result goes into the existing Phase 8 row or a new record section in the architecture doc. That doc is tier 2, so the PM decides.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-endpointer-profile; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 822caacc-e514-49be-8be8-8aa915269682". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
