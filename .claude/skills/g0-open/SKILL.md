---
name: g0-open
description: "Open a design topic or feature at G0: agree its scope, cut its branch, record the scope. Use on 'start a design topic', 'open a new feature', 'pick this feature off the backlog', 'start X and Y', or before any doc or code exists."
kind: orchestration
agents: [doc-writer]
stops: [the agreement on the topic and its scope, the ruling to run concurrently or wait, the post-scope re-check on overlapping write sets]
status: active
bindings: [vc, process-docs, trees, session-launch, board]
---

# g0-open

## What this is

G0 opens a design topic or a feature: the human and the PM agree the scope, the branch is created, and the scope, with a feature's user story beside it, is recorded as the doc's opening sections — no doc or code exists before the branch does.

> The branch first, then the scope in writing. Nothing is built on the main branch.

## Bindings

Each key is looked up as `.claude/references/core/delegation-contract.md` `## Bindings` says: `.claude/bindings/<pack>.md` first, the project `CLAUDE.md` *Bindings* section as the pre-scaffold fallback.

- `vc` (required) — the branch procedure (the design or feature form the branch procedure names, off the main branch) and the check-in procedure. Absent → stop and say so before anything is created.
- `process-docs` (required) — the layout and the file names the opening sections are written into. Absent → stop and say so.
- `trees` (optional) — the slots `references/tree-path.md` `## Bindings` names. Absent → today's path. Filled → the steps run as `references/tree-path.md` orders them, and SCOPE reads the hub's `memory/backlog-candidates.md` and `memory/notes-for-future-features.md` by absolute path under the Hub path.
- `session-launch` (optional) — the Hub session name and the List command, read by `references/tree-path.md`. Absent → no report is sent, the active branches read as `references/tree-path.md` has them where the key reads absent, and `handoff` gives the paste line. The Hub session name missing or `none` → no report is sent, and `handoff` gives the paste line; the List command missing or `none` → the active branches read as where the key reads absent, and `handoff` gives the paste line.
- `board` (optional) — the *STATUS line cap*, which STATUS keeps the branch's line within. Absent, `none` or missing the slot → no cap stated, never a stop.
- Agents — `doc-writer` missing → stop after BRANCH.

## Workflow

Where `trees` is filled, as the contract reads it, these steps run as `references/tree-path.md` orders them; otherwise they run as written.

1. **SCOPE** — the hard stop: the human and the PM agree the topic, or the backlog feature, and its scope — for a feature, its user story as well, in the form *As a \<user\>, I want \<capability\>, so that \<value\>.*, as the human states it — in conversation, the PM putting it to the human by `.claude/references/core/ruling-form.md`. Before putting the scope, the PM reads `memory/backlog-candidates.md` and `memory/notes-for-future-features.md`, where they exist. Nothing is created before that agreement. For a backlog feature the PM reads the row's mark and names the track, lite or full, with the scope (`.claude/references/core/feature-lite.md` `## The mark`).
2. **BRANCH** — per the `vc` binding's branch procedure; the ceremony is the PM's.
3. **RECORD** — file `.scratch/doc-writer-<slug>-g0-brief.md` from `.claude/references/core/decision-brief.md`: the agreed scope as the decision record, the target and form per `process-docs` — the design doc's scope section for a design topic, the requirements doc's §1, the user story, and §2, the scope, for a feature. For a lite feature the target is the architecture doc's `## User story` and `## Context & scope`, no requirements doc is made (`.claude/references/core/feature-lite.md` `## The three placements`), and the brief says `lite`, gives the backlog's path and quotes the row (`.claude/references/core/feature-lite.md` `## The acceptance source`). Dispatch `doc-writer` through the Agent tool without a `name`.
4. **STATUS** — the PM adds the branch's line to `memory/STATUS.md` (name, lane, stage, next action; within the `board` binding's *STATUS line cap* — absent or `none` → no cap stated) and checks in per the `vc` binding's check-in procedure.

Where `trees` is filled, as the contract reads it, several items the human orders at once start under one ruling, as `references/tree-path.md` `## Several items` orders them; where it reads absent, one item at a time through `## Workflow`, as today.
