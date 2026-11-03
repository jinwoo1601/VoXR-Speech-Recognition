You're picking up: set VoXR's vc binding to `Auto-mode entries: excluded` as a light-lane changeset. Read this brief, then start at Next steps.

## Objective

Change `.claude/bindings/vc-git.md` line 7 from `- Auto-mode entries: included` to `- Auto-mode entries: excluded` on a new branch `light-vc-automode-excluded`, re-splice the skills that compose the `vc` binding, review, PR, and close at G2-lite on the human's ruling. Done = PR merged by the human, merge recorded on `main`, one line to harness-hub.

## Lane

light, class trivial

## Context

- Asked by harness-hub, 2026-10-27, on the human's ruling there: "Exclude VoXR (Recommended)" (rejected: "Add a merge-route slot", "Hold the batch"). `harness.py automode` composes the autoMode block in the user's `~/.claude/settings.json` from per-pack fragments for each project whose vc binding reads `included` (harness design automode-rules, adr-0099). vc-git's fragment allows a local `git -C "<hub>" merge --no-edit <branch>` into the default branch; VoXR merges only by GitHub PR, merged by the human, and Claude never merges (`.claude/bindings/vc-git.md` Merge procedure), so `included` grants a permission VoXR's rules forbid. The vc-git template names a PR-only repository as the case for `excluded`.
- The slot was filled `included` on 2026-10-23 (round 2 upgrade, the human's "included (Recommended)"); this reverses it.
- The human, 2026-10-28, in voxr-hub: "Now (Recommended)" — take it now rather than queue it.
- One checkout `D:\Workspace\VoXR-Speech-Recognition` (`trees` is none); `main` is the dev branch, at `10e6dca` or later. Base `../harness`; run `py ../harness/scripts/harness.py ...`.

## Current state

- The checkout is on `feat-endpointer-profile`, clean; its session `65d226e6` is blocked waiting on the human's Phase 8 Quest 3 run and writes nothing until then. Leave that branch untouched; switch back to it when this changeset is done (the feature session expects its branch checked out).
- Nothing of this changeset exists yet.

## Key files & locations

- Target: `.claude/bindings/vc-git.md` line 7 (`## vc`, *Auto-mode entries*).
- Light-lane bounds: `.claude/bindings/light-lane.md` `## classes` (trivial: 20 lines, 1 file; Delegated classes: none, so G2-lite is the human's).
- This brief: `.scratch/light-vc-automode-excluded-launch-brief.md`; commit a copy as `memory/handoffs/2026-10-28-light-vc-automode-excluded.md` on the branch.

## Constraints & decisions

- Cut the branch by the `vc` Branch procedure from an up-to-date `main`.
- The binding edit is one value; the `vc` block is composed into skills, so the `upgrade` skill's re-splice (dry run, the human's stop, snapshot, apply) follows; if the re-splice takes the changeset past the trivial bound, say so and re-declare the class at the human's stop (slim, as the harness-upgrade precedent).
- Commit named paths only, never `git add -A`; no CHANGELOG line (no package surface).
- Not yours: `harness.py automode --apply` and deleting the old untagged Set B lines from `~/.claude/settings.json` are the human's, after the merge.
- Claude never merges; the human merges on GitHub.

## Next steps

1. Cut `light-vc-automode-excluded`; add its `memory/STATUS.md` `## Current` line.
2. Make the one-line edit; then `upgrade` for the re-splice; `validate` and `doctor`.
3. Check in, push; `review-cycle` at the declared class; PR by the vc Merge procedure.
4. `g2-lite` on the human's ruling.
5. After the merge: one line to harness-hub that it landed (merge sha); switch the checkout back to `feat-endpointer-profile`.

## Open questions

- None.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - light-vc-automode-excluded; lane: light, class trivial; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps. BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer.
