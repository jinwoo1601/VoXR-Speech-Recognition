You're picking up: the post-merge close of `light-harness-upgrade-2026-10-23` (PR #180). Read the files below, then start at Next steps.

## Objective

Once the human has merged PR #180, do three things: record the merge commit in `memory/2026-10-23.md` on `main`, send the merge sha to harness-hub, and send FINISHED to `voxr-hub`. Done means the merge entry is committed and pushed on `main`, harness-hub has the sha, and FINISHED is sent as the last act. If the PR is not merged yet, send BLOCKED and stop.

## Lane

light, class slim

## Context

- This is the harness upgrade round run 2. Packs are now codex 0.9.0, core 0.90.0, design-track 0.7.0, feature-track 0.27.0, light-lane 0.24.0, unity 0.15.0 and vc-git 0.30.0. The upgrade commit is `0945aa9`.
- The human ruled G2-lite on PR #180 on 2026-10-23: "Accept (Recommended)". The close-out is already recorded and pushed on the branch at head `22210f5`. The human merges on GitHub; Claude never merges.
- There is one checkout (`trees` none). `main` is the dev branch, so a memory commit after the merge goes straight onto `main`.

## Current state

- Done:
  - binding slots filled;
  - verify (validate and doctor PASS);
  - check-in `0945aa9`, plus memory commits `52b4fc5` and `22210f5`;
  - slim review clean;
  - EditMode 203/203 and PlayMode 704/704;
  - preflight;
  - `close-class: human`;
  - G2-lite accepted;
  - close-out (STATUS line moved into the dated note).
- Not done: the merge, which is the human's. After it, this brief and its handoff entry in `memory/2026-10-23.md` are uncommitted in the working tree and get committed with the merge record.
- Upstream findings were sent to harness-hub, cc workspace-hub. harness-hub replied:
  - Finding 1 is B194.
  - Finding 2 will be filed at the round's end.
  - Do NOT delete the orphaned `.claude/references/core/handoff-guard-hook.py` and `hub-memory-guard-hook.py` by hand, because that trips a permanent DRIFT warning (B195).
  - It asked for the merge sha once G2-lite is ruled.

## Key files & locations

- `memory/2026-10-23.md`: its last entries are "G2-lite close-out" and then "handoff". Append the merge entry at the end.
- `memory/STATUS.md`: the branch's line is already gone from `## Current`. Nothing to change unless the main headline needs the merge noted.
- `.claude/references/core/gate-close.md` `### REMOVE` and `## The dated note`: the form for the FINISHED line and the dated-note entry.

## Constraints & decisions

- Check-in per the vc binding: `git add -- <paths>`, then `git commit -F <message file> -- <paths>`, each its own command. No heredoc commit, no `-A`. Then `git push`.
- To reach `main` after the merge, run `git fetch origin`, then `git switch main`, then `git merge --ff-only origin/main`. The untracked brief and note edits carry over.
- Leave `.claude/references/vc-git/__pycache__/` untracked. Don't touch `.claude/settings.local.json`.
- Don't delete the local or remote branch without the human's word.
- Commit trailer: `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`, plus the `Claude-Session:` line from your own session's attribution reminder.

## Next steps

1. Read the PR state: `gh pr view 180 --json number,state,headRefOid,baseRefName,url`. Then `git fetch origin` and `git log -1 --format=%H origin/main`. If it is not `MERGED`, send BLOCKED to `voxr-hub` with the detail `awaiting the human's merge of PR #180`, and stop.
2. Once it is merged, move to an up-to-date `main` as above. Append a dated-note entry to `memory/2026-10-23.md` naming the merge commit, in the four-section form `gate-close.md` `## The dated note` gives. Commit it with this brief and push.
3. Send the merge sha to `harness-hub [d6d4cb]` in one plain-ASCII message.
4. Send FINISHED to `voxr-hub [784b11]` as the very last act: `FINISHED - light-harness-upgrade-2026-10-23; lane: light, class slim; merged <commit>`, where `<commit>` is the commit the merge left `main` at.

## Open questions

- None pending for the human, apart from the merge itself.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - light-harness-upgrade-2026-10-23; lane: light, class slim; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 2ddb1b42-30a9-4567-883f-b3f57064cd88". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. Two sessions are named `voxr-hub`; address the local one as `voxr-hub [784b11]`.
