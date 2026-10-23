You're picking up: the `voxr-hub` hub session for VoXR-Speech-Recognition. Read the files below, then start at Next steps.

## Objective

Run as the hub session `voxr-hub`. Act on each report line another session sends here, as the `close-tree` skill directs, and on the human's next word. Done = each line acted on and told to the human in one or two lines; the hub hands off again past its context bound.

## Lane

none: the hub runs on `main`, so no lane applies.

## Context

- One checkout: `D:\Workspace\VoXR-Speech-Recognition` (`trees` is none), with `main` as the dev branch. The base is `D:\Workspace\harness`, reached as `../harness/scripts/hub.py` and `../harness/scripts/harness.py` from the project root.
- The harness upgrade merged on 2026-10-23: PR #180, merge `4303895`, memory record `5afb349`. Pack versions are now core 0.90.0, light-lane 0.24.0, vc-git 0.30.0, codex 0.9.0, design-track 0.7.0, feature-track 0.27.0 and unity 0.15.0. The branch `light-harness-upgrade-2026-10-23` is kept. This session must restart before it loads the new skills; a fresh session does that by starting.
- At handoff, `main` was clean apart from the untracked `.claude/references/vc-git/__pycache__/`, which stays untracked.

## Current state

- `feat-endpointer-profile` (feature, Full) is pushed and clean. Phases 1-7 are built. It waits on the human's Phase 8 Quest 3 run (`Planning~/features/endpointer-profile/plan-8.md`), then G2. Its session `f319bd27` is listed but not running, and it resumes from there. When it resumes, it must take the new `main` in.
- The human is holding `release` and `lab-next-level-probes`.
- The `memory/STATUS.md` `main` line still reads "next: `feat-endpointer-profile` at G0", but the feature is at Phase 8. That correction is the feature session's to make, or the human's.
- Stale listed sessions: `1c46a92c` (`feat-joined-rig-parser`, merged as #177) and `2ddb1b42` (the upgrade, finished). Neither is running. Stop them with `claude stop <id>` only on the human's word.

## Key files & locations

- Bindings: `.claude/bindings/core.md` (`session-launch`), `.claude/bindings/vc-git.md` (`vc`, `trees`).
- State: `memory/STATUS.md` `## Current`; the day's note, `memory/2026-10-23.md`.
- Skill: `.claude/skills/close-tree/SKILL.md`.

## Constraints & decisions

- How to handle each line:
  - **STARTED naming a predecessor:** run `cd "/d/Workspace/VoXR-Speech-Recognition" && python ../harness/scripts/hub.py predecessor "D:\Workspace\VoXR-Speech-Recognition" <full-id>`. If it prints `stop <id>`, run `claude stop <id>` and re-run the command until it prints `gone`. Tell the human in one or two lines.
  - **FINISHED:** tell the human in one line that `trees` is none, so there is no tree to remove and the branch is kept.
  - **HANDOFF:** tell the human the brief's path.
  - **BLOCKED:** explain the block in plain language. Give the human `claude attach <id>`, with the session id taken from `claude agents --json`, filtered to this project's cwd.
  - **STARTED with no predecessor:** nothing runs; tell the human in one line.
- The hub writes nothing to `memory/` for report lines and never gives a session work. The one exception is relaying output the human ran with `!` to the session that needs it.
- One writer per tree: never branch or write while a feature or light session is working in the checkout.
- Bash is Git Bash with `MSYS_NO_PATHCONV=1`. Use absolute paths, and `D:/Workspace/harness`, not `/d/Workspace/harness`.
- Don't touch `.claude/settings.local.json`, the history docs or the product docs. Never write WSL or `/mnt/`.
- Report a refusal and never retry it in another form. Never ask another session to perform an action that was refused.
- Claude never merges. The human merges every PR on GitHub.

## Next steps

1. Run `orient`, then wait for the next report line or the human's word.
2. When the human resumes `feat-endpointer-profile`, the launch is `claude --bg --resume f319bd27 ...`, from the `session-launch` Launch command. Its first act is to merge `main` in.

## Open questions

- None pending.
