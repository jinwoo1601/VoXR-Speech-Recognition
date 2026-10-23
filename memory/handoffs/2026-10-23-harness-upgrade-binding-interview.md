You're picking up: the harness upgrade on `light-harness-upgrade-2026-10-23`, from the `upgrade` skill's step 7 BINDING INTERVIEW. Read the files below, then start at Next steps.

## Objective

Finish the upgrade (round run 2) on branch `light-harness-upgrade-2026-10-23`: fill the three missing binding slots, verify, check in, review, open the PR against `main` and stop for the human's G2-lite. Done = PR open with `validate` and `doctor` results shown, the result relayed to harness-hub (cc workspace-hub), or a BLOCKED report saying where it stopped.

## Lane

light, class slim

## Context

- Upgrade request from workspace-hub (relayed from harness-hub), base `D:\Workspace\harness` (requested at master `4d44cdb6`; base was at `ce09baad`, a memory-only commit on top). Scope ruled by the human 2026-10-23 ("All but KAIST (Recommended)"); the human said "Upgrade now" in voxr-hub the same day, ahead of `feat-endpointer-profile`'s merge.
- One checkout (`trees` none). The branch was cut from up-to-date `main` at `b255105`. `feat-endpointer-profile` (pushed, clean at `9894b50`) is parked: it waits on the human's Phase 8 Quest 3 run (`Planning~/features/endpointer-profile/plan-8.md`); its last session `f319bd27` is not running.
- `voxr-hub` (the hub session) lives in this same project root but only reads report lines; you are the one writer.

## Current state

- `upgrade` steps 1-6 done: dry run shown whole and ruled ("Apply (Recommended)"); snapshot `b2551051bde06f896d28e708a0dd313b1d41f982` (= HEAD; `git stash create` printed nothing), confirmed by `git rev-parse --verify`; apply wrote 78 files, its report body identical to the dry run's.
- Packs now: codex 0.9.0, core 0.90.0, design-track 0.7.0, feature-track 0.27.0, light-lane 0.24.0, unity 0.15.0, vc-git 0.30.0. Nothing committed yet (79 entries in `git status --short`, all from the apply).
- The report: `orphaned` `.claude/references/core/handoff-guard-hook.py` and `.claude/references/core/hub-memory-guard-hook.py` (left on disk by the verb); `replaced (LOCKED)` CONSTITUTION and annex; `merged: CLAUDE.md`; bindings `preserved`; `bindings to fill: none`.
- Slots missing, with the human's rulings of 2026-10-23:
  - `.claude/bindings/vc-git.md` `## vc` - `Main branch`: `` `main` `` (a fact); `Auto-mode entries`: `included` ("included (Recommended)").
  - `.claude/bindings/light-lane.md` `## classes` - `Delegated classes`: `none` ("none (Recommended)").

## Key files & locations

- Reports: `.scratch/upgrade-2026-10-23/upgrade-dry-run.txt`, `.scratch/upgrade-2026-10-23/upgrade-apply.txt`.
- Slot templates: `D:\Workspace\harness\packs\vc-git\bindings\vc.template.md` lines 4-5; `D:\Workspace\harness\packs\light-lane\bindings\classes.template.md` line 6.
- Bindings: `.claude/bindings/vc-git.md`, `.claude/bindings/light-lane.md`; manifest `.claude/harness.json`.

## Constraints & decisions

- Bindings are written by `doc-writer` from a decision brief (`.claude/references/core/decision-brief.md`), one brief per bindings file; add only the missing slot lines, rewrite nothing else.
- Don't touch `.claude/settings.local.json` (the human's), history docs or product docs. Never write WSL or `/mnt/`. Leave `.claude/references/vc-git/__pycache__/` untracked.
- Check-in per the vc binding: `git add -- <paths>` then `git commit -F <msgfile> -- <paths>`, each its own command; no heredoc commit, no `-A`. The `.claude/settings.json` hooks changed in this upgrade; if a hook refuses a step, report it and never retry it in another form.
- Claude never merges; the human merges the PR on GitHub after G2-lite. Delegated classes is `none`, so G2-lite is the human's.
- Commit trailer: `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` and `Claude-Session: https://claude.ai/code/session_01SJgTYRfWLifRBhnhCMqtqV`.

## Next steps

1. Invoke `upgrade`, resuming at step 7 BINDING INTERVIEW with the state above, then steps 8 VERIFY and 9 REPORT.
2. Check in and push the branch; run the slim review by `review-cycle`; open the PR against `main`; then `g2-lite` for the human's ruling.
3. Relay the result (PR number and head sha, or where it stopped) and any upstream findings to harness-hub, cc workspace-hub, by SendMessage.
4. After the merge: `feat-endpointer-profile` takes the new `main` in when its session is resumed; not this session's task.

## Open questions

- None pending for the human.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - light-harness-upgrade-2026-10-23; lane: light, class slim; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps. BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer.
