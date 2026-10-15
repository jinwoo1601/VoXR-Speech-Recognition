You're picking up: the Phase 4 as-built pass, then `g2-accept`, for `feat-joined-rig-recogniser`. Read the files below, then start at Next steps.

## Objective

1. Reconcile the architecture doc to as-built for Phase 4 and the review fix (`architect-reconcile`). First get the human's body-budget ruling, below.
2. Then take the feature through `g2-accept` up to and including its hard stop. Done for this brief when the G2 ruling is asked with the preflight audit in view. After the ruling, the skill's own steps (record, product docs, merge, close-out) follow.

## Lane

feature

## Context

- Branch `feat-joined-rig-recogniser`, the one checkout `D:\Workspace\VoXR-Speech-Recognition` (`trees` none). Head `6c9f1a0`, pushed.
- Design `joined-rig` is G1-locked; Full track.
- All four build-plan phases are built and committed: `ef1ed67`, `594f783`, `ca8c6ea`, `722acd9`.
- `review-cycle` is done (full, 11 angles); its one fix is committed in `6c9f1a0`.
- STATUS `memory/STATUS.md` `## Current`: next `g2-accept`. Session note `memory/2026-10-15.md`, entries "Phase 4 implement", "review-cycle" and this handoff's.

## Current state

- **Phase 4 gate A: PASS.** Criteria 1-14, claims a-q true.
  - Command 3: 16/16, both shells.
  - DocCheck: 66/66.
  - Every acceptance run, re-made set and unset, is byte-equal to Phase 3's.
  - The 80 and 120 Hz probes move no class.
  - Report: `.scratch/compile-check-joined-rig-recogniser-phase-4-gate-a-report.md`.
- **Criterion 15 done.** The *Tuning record* Evidence cell is written. `body_bytes` reads 40,819 of 40,960.
- **Review.** Collation: `.scratch/review-joined-rig-recogniser-collation.md`.
  - WRAP-N1 was CONFIRMED and fixed: in `ab-rig/JoinedRecogniser.cs` `RunWorld`, the observers now subscribe before the `OnEnable` sends.
  - Gate: `.scratch/compile-check-joined-rig-recogniser-review-fix-report.md`, PASS. Shipped-default outputs are byte-equal to Phase 3's, and the `bufferWindow` 0 canary went from 14/14 misordered fires to 0/14.
  - GAP-1 and SIMP-1 were REFUTED.
- **Not yet done:**
  - The `## As-built deltas` entry for Phase 4 and the review fix.
  - The architecture doc's `status:`/`updated:` move (that is `g2-accept` RECORD).
  - The G2 preflight.

## Key files & locations

- Architecture: `Planning~/features/joined-rig-recogniser/architecture.md` (`## As-built deltas` :155, Phases 1-3 entries :173-193; *Tuning record (F7)* :115). Requirements: `Planning~/features/joined-rig-recogniser/requirements.md` (§9 G2 criteria). Plans `plan-1.md` … `plan-4.md` beside them.
- Phase 3 as-built brief and report, for form: `.scratch/architect-reconcile-joined-rig-recogniser-phase-3-{brief,report}.md`.
- Body budget: measure with `python3 -I -c "import sys; sys.path.insert(0, r'D:\Workspace\harness\scripts'); import harness; print(harness.body_bytes(r'D:\Workspace\VoXR-Speech-Recognition\Planning~\features\joined-rig-recogniser\architecture.md'))"` (it leaves out `## Build plan`). The budget is 40,960.
- Verification binding: `.claude/bindings/unity.md` `## verification`. Recipe: `Planning~/verification-recipe.md`.

## Constraints & decisions

- **Rulings 2026-10-15 (maintainer):**
  - 90 Hz stays; the tuning pass records, it does not re-pick.
  - The `pending` labels stay unexercised on real data. That is a G2 human item, not a defect.
  - The §6 negative stops were not re-made in Phase 4; Phase 3's stand.
  - Commands 1-2 are not re-run: `Runtime/`, `NativeBridge~/` and `Tests~/` are unchanged since `ca8c6ea`. The review fix touched only `ab-rig/`.
- **Carry into `g2-accept`'s product-docs brief** (review XFILE-1/GAP-2, ruled post-G2):
  - `Documentation~/native-bridge.md` `## ABI Exports` lacks rows for `vosk_bridge_get_result_position` and `vosk_bridge_get_consumed`. Both are harness-bound; C# binds neither.
  - The feature's CHANGELOG line has to cover the additive ABI and the rebuilt `.so`.
- **Notes for the as-built entry** (from the session note and the collation):
  - The trace-order fix.
  - `NativeBridge~/build` pre-existed, ignored, and the arm64 configure succeeded on it (claim l).
  - Staged rig C# copies are LF.
  - plan-2 :37 vs :33 wording: `timing` red on more than 4096, against the closed FAIL list.
- **Process:**
  - Compiler and test output goes to `compile-check`, never the main thread.
  - Agents never run git writes; commits are the PM's, `git add -- <paths>` then `git commit -F <file> -- <paths>`.
  - No CHANGELOG line per phase. Windows-native only.
- The report filer misses background agents' reports. After each dispatch, check `.scratch/`, and file by hand from the agent's `SubagentHandback` tool input in `~/.claude/projects/D--Workspace-VoXR-Speech-Recognition/<session>/subagents/agent-<id>.jsonl`. Open it by its `cygpath -w` path in Python.

## Next steps

1. Ask the human the body-budget ruling (Open questions 1).
2. Invoke `architecture-doc` to reconcile to as-built for Phase 4 and the review fix, per that ruling. Measure `body_bytes` after.
3. Commit; then invoke `g2-accept` for `joined-rig-recogniser`.

## Open questions

1. **Body budget.** The architecture body is 40,819 of 40,960 bytes, and the Phase 4 as-built entry will cross it. The human rules one of:
   - (a) route the Phases 1-3 as-built entries out to a dated record, read on demand; or
   - (b) take a `memory/doctor-rulings.md` exemption.
2. **Human-only at G2:** the Quest on-device check after the bridge change, and reading the 16-fixture recogniser-layer report (the split case and the flips legible).

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-joined-rig-recogniser; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 8805ce6f-3252-4b31-af59-48fa44892892". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
