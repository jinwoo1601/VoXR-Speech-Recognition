---
name: close-tree
description: "Close a merged branch's tree from the hub, keeping the branch, or stop a handed-off session. Use when a 'FINISHED - <branch>' or 'HANDOFF - <branch>' line arrives in the hub, or on the human's word for a merged tree; never on 'STARTED' or 'BLOCKED'."
kind: execution
status: active
bindings: [vc, trees, session-launch]
---

# close-tree

## What this is

The hub's half of a branch's close: the tree a finished branch was built in is removed from the hub once its checks pass, or kept with one line naming the check that failed. A report line is a trigger, never proof — a tree's close reads the `vc` binding's log read, the session list and the tree, never the report's detail, and `## A handed-off session`, which stops a handed-off session and removes nothing, takes from its line only the branch and the successor to look for. The removal rides on the gate ruling that authorised the merge — the G1, G2 or G2-lite ruling — and takes no ruling of its own (`adr-0024`).

> At a tree's close, nothing is stopped before the branch is merged, nothing is read clean while a session is at the tree, nothing is removed before the release wait, and a half-removal is told, never retried.

## When it runs

- A `FINISHED - <branch>; lane: <lane>; <detail>` line arriving in this session → `## Workflow` for `<branch>`; the detail is never read.
- A `HANDOFF - <branch>; lane: <lane>; successor <id>` line arriving in this session → `## A handed-off session` for `<branch>` and `<id>`; `## Workflow` does not run.
- The human's word on a tree `orient`'s TREES states merged and awaiting removal, proposed by its PROPOSE, or the human's word that the branch finished on a tree TREES states as "in progress, or finished with its report lost", for which PROPOSE offers this skill → `## Workflow` for that tree's branch.
- A `STARTED` or `BLOCKED` line → nothing runs, and the line is the human's to read.

This skill never answers a report, never gives a session work, and writes nothing to `memory/`.

## Bindings

Each key is looked up as `.claude/references/core/delegation-contract.md` `## Bindings` says: `.claude/bindings/<pack>.md` first, the project `CLAUDE.md` *Bindings* section as the pre-scaffold fallback.

- `vc` (required) — the log read MERGED runs. Absent → stop and say so before anything runs.
- `trees` (optional) — the Hub path and the Hub slot's idiom; the List read of TREE, MERGED and READ BACK; the Clean read of CLEAN; and the Remove procedure of REMOVE. Absent → there is no tree to remove: one line saying so, and nothing runs.
- `session-launch` (optional) — the List command, the Stop command and the Confirm window, which STOPPED and `## A handed-off session` run, and the Release wait, which RELEASE waits; the Hub session name is not read, since this skill sends nothing; what the CLI behind those commands does is `.claude/references/core/claude-code-cli.md`'s. Absent, or the List command missing or `none` → STOPPED cannot run: R3's line naming the binding or the slot, and the tree kept. The Stop command or the Confirm window missing or `none` → R3's line naming the slot where a session is at the tree, and STOPPED holds where none is. The Release wait missing → RELEASE cannot run: R3's line naming the slot, and the tree kept.
- No agent: this skill dispatches nothing.

## Workflow

`<branch>` is the branch the trigger names.

1. **TREE** — the List read, run in the hub by the trees contract's hub clause (`.claude/references/core/trees-contract.md` `## The hub clause`): the tree on `<branch>`, and its path. No tree on `<branch>`, or `<branch>` is the main branch → one line saying which; stop, and nothing else runs.
2. **MERGED** — the `vc` binding's log read from the main branch to `<branch>` lists no commit, or, failing that, the same read from the branch of any other tree the List read names, run where the `vc` binding runs a log read whose destination is that branch, lists none; a tree on no branch is passed over. Otherwise R3's line, naming the branches read; nothing is stopped.
3. **STOPPED** — first the shell returns to this session's project root. Then the List command: a session is at the tree when its working directory, as the list shows it, is the tree's path or lies under it, compared as the trees contract compares paths. None → on to CLEAN. Any session at the tree that shows no `<id>` for the Stop command → R3's line, and nothing is stopped. Otherwise each is ended by the Stop command with its `<id>`, its conversation kept; then the List command is re-read until no session is at the tree, within the `session-launch` binding's *Confirm window*, or R3's line naming what is still listed.
4. **CLEAN** — the Clean read, run in the tree by the Hub slot's idiom: empty, or R3's line with what it showed.
5. **RELEASE** — where the Remove procedure waits for the directory's release itself, or the `session-launch` binding's *Release wait* reads `none`, nothing; otherwise wait the *Release wait*: a stopped session can hold its tree's directory for a few seconds after it leaves the list. The slot missing → R3's line naming it, and the tree kept.
6. **REMOVE** — the Remove procedure for the tree's path, run in the hub by the trees contract's hub clause.
7. **READ BACK** — the List read, run in the hub, and the tree's path: still listed, unlisted with its directory gone, or unlisted with its directory present, as `## Outcomes` tells them apart.

## Outcomes

| Outcome | The tree | Its sessions | The human hears |
|---|---|---|---|
| not merged | kept | untouched | R3's line |
| a session at the tree without an `<id>` | kept | untouched | R3's line |
| a session still listed after the Confirm window | kept | the stop sent | R3's line |
| not clean | kept | stopped, conversations kept | R3's line |
| the removal refused — still listed | kept | stopped | R3's line, with the procedure's message |
| half-removed — unlisted, its directory present | unregistered; its directory left, empty or still holding files, as the Remove procedure leaves it | stopped | R3's line naming the directory; `orient` names it as a leftover until the human removes it |
| removed — unlisted, its directory gone | gone; the branch kept | stopped | nothing |

R3's line is `<branch>: tree kept at <path> - <the check> failed: <what the read showed>`, one plain-ASCII line to the human: `<path>` the tree's path; `<the check>` the step that failed — MERGED, STOPPED, CLEAN, RELEASE or REMOVE; and `<what the read showed>` the read's own words — for MERGED, the branches read and the commits each listed, the sessions without an `<id>` or still listed, the Clean read's lines, the procedure's message, or the directory left in place; where STOPPED or RELEASE could not run, the binding or slot missing.

## A handed-off session

`<branch>` and `<id>` are the ones the `HANDOFF` line names; the line is a trigger and never proof. The tree is kept: nothing is removed, read clean or written, and MERGED is not read.

1. **TREE** — as `## Workflow`'s TREE.
2. **SUCCESSOR** — first the shell returns to this session's project root. Then the sessions at the tree, as STOPPED reads them. No session there shows `<id>` → one line to the human, `<branch>: handed-off session not stopped - successor <id> is not listed at <path>`, and nothing is stopped.
3. **PREDECESSORS** — every other session at the tree that shows an id of its own is ended by the Stop command with that id, its conversation kept; one that shows none is named to the human in one line and left. Then the List command is re-read until no stopped session is at the tree, within the `session-launch` binding's *Confirm window*, or one line to the human naming what is still listed.

The `session-launch` binding absent, or a slot these steps need missing or `none` → one line to the human naming it, and nothing is stopped.

At a tree's close and for a handed-off session alike, this skill writes nothing to `memory/`, keeps the branch, sends nothing to any session, and says nothing on success.
