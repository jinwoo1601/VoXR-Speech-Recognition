---
name: orient
description: "Say where things stand in three lines, from the status file, the latest session note and the branch, and name the next skill. Use at the start of a session, on 'where are we', 'what's the state', or after a context reset."
kind: execution
status: active
bindings: [vc, board, trees, session-launch]
---

# orient

## What this is

Session start: read the project's state, say where things stand in three lines, name the next skill. Nothing here decides anything.

> Orientation reports state; it never changes it.

## Bindings

Each key is looked up as `.claude/references/core/delegation-contract.md` `## Bindings` says: `.claude/bindings/<pack>.md` first, the project `CLAUDE.md` *Bindings* section as the pre-scaffold fallback.

- `vc` (required) — the base-revision reads that give the current branch, and the *Branch procedure*, whose forms STATE reads the lane from. Absent → after MEMORY, stop and say so, proposing the binding interview or, before the project is scaffolded, the project `CLAUDE.md` *Bindings* section.
- `board` (optional) — where cross-session state lives beyond `memory/STATUS.md`. Absent → `memory/STATUS.md` is the state.
- `trees` (optional) — the Hub path, whose `memory/` MEMORY reads and whose branch BRANCH states; the List read and the Location slot, which TREES reads; the Hub slot's idiom by which those reads run in the hub; and the Remove procedure PROPOSE names. Absent → today's path; TREES is then skipped. Filled → MEMORY, BRANCH and PROPOSE also read the hub, and TREES runs.
- `session-launch` (optional) — the List command, read by `references/trees.md`, and the *Context bound* and the *Context read*, which STATE reads. Absent, or the List command missing or `none` → TREES and PROPOSE as today; the *Context bound* or the *Context read* missing or `none` → STATE without the read.
- No agent: this skill dispatches nothing.

## Workflow

1. **MEMORY** — read `memory/STATUS.md`'s `## Current` section only — from its heading to the next `## ` heading, at the line numbers the base's `scripts/harness.py outline` prints for that file, the base being the `base` path in `.claude/harness.json`, relative to the project root, which MEMORY reads itself; or, where no base is found (no manifest), by its heading in `memory/STATUS.md`; where STATUS has no `## Current` heading, say so and read it whole — and the latest `memory/<YYYY-MM-DD>.md`. No `memory/` → say so and propose `g0-open`. Where `trees` is filled, both are the hub's, read by absolute path under the Hub path, whichever tree the session opened in.
2. **MANIFEST** — read the project's manifest and report the doctor run the SessionStart hook put into this session's context at its start, resume or clear — and only where the context holds none, run the base's `scripts/harness.py doctor --root <project>` — where they exist, reporting its failures first; where they do not, say "no manifest — skipping" and continue.
3. **BRANCH** — the current branch per the `vc` binding's base-revision reads, cross-checked against its STATUS line. A mismatch is stated, not resolved. Where `trees` is filled, the same read, run in the hub by the Hub slot's idiom, also gives the hub's branch, and a hub off the main branch is a mismatch too.
4. **TREES** — where `trees` is filled, as the contract reads it: as `references/trees.md` says; otherwise skipped.
5. **STATE** — three lines: the branch and its lane, the stage, the next action. The lane, `design`, `feature`, `light` or `lab`, is the one the brief this session started from names, or else the one the branch gives, matched against the forms the `vc` binding's *Branch procedure* names (a `<…>` placeholder matches any text): its design, feature, light-lane or lab form, the lab form `lab-<topic>` where the binding names none; a branch with none of them, the main branch among them, is stated without a lane. Where the `session-launch` binding's *Context bound* and *Context read* both read filled, STATE runs the read, and past the bound the next action is `handoff`, as its `## When to hand off` says; a failed read is named in one line and changes nothing.
6. **PROPOSE** — the skill that fits the next action (`g0-open`, `g1-lock`, `implement`, `review-cycle`, `g2-accept`, `handoff`), then stop. The human decides. Where TREES ran, tree removals are proposed as `references/trees.md` `## PROPOSE` says.
