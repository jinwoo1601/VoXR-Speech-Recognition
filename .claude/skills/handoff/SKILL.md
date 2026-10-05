---
name: handoff
description: "Write a focused handoff brief a cold session can start from, with one task, minimal state and exact paths. Use on 'write a handoff', 'prep the next session', 'continue this in a new session', or at a session's end."
kind: execution
status: active
bindings: [process-docs, vc, trees, session-launch, verification]
---

# handoff

## What this is

Write a **handoff brief** that another session can start from cold, with no access to this conversation. The goal is to hand off ONE focused task — not to dump this whole session. Include only what the next session needs to do that task well, and deliberately leave everything else out.

Focus of the handoff: whatever focus the human gave when invoking this skill (arguments or the free-form ask). If none was given, infer the single most likely next task from where this session left off, and state that inference in the Objective so it can be corrected.

> One task, the minimum state, every path exact. The next session reads the file, not this conversation.

## When to hand off

A session hands off at a phase boundary, or, inside a phase, at the next skill-step boundary once its context passes the bound — never below the bound merely because a step ended.

- **A phase boundary** — the start of a build-plan phase's pickup by a session that ran the previous phase's `implement`: there the session hands off whatever its context, the objective that phase's pickup.
- **A skill-step boundary** — inside a phase, at the end of each step of the skill in hand, the session runs the `session-launch` binding's *Context read* and hands off there once the figure passes its *Context bound*, the objective the next step, or, after the skill's last step, the skill that follows it; at or below the bound it takes the next step.
- **No read** — the *Context bound* or the *Context read* missing or `none`, or the read failed → phase boundaries only, the slot named once; never a stop, and never a guessed figure.

A session that reaches a handoff point invokes this skill itself, without waiting to be asked.

## Bindings

Each key is looked up as `.claude/references/core/delegation-contract.md` `## Bindings` says: `.claude/bindings/<pack>.md` first, the project `CLAUDE.md` *Bindings* section as the pre-scaffold fallback.

- `process-docs` (required) — where the design and feature docs live, so Context and Key files point at them. Absent → stop and say so before writing; a handoff that cannot point at the process docs is written from `memory/` only on the human's word, not by default.
- `vc` (required where `trees` is filled) — the current-branch read and the check-in procedure of the hub operation that writes the brief and the note; and, wherever it is bound, the *Branch procedure*, whose forms `## Sections` item 2 reads the lane from. Where `trees` is not filled it is not otherwise consulted, and this skill runs as written. Absent where `trees` is filled → stop before writing and say so.
- `trees` (optional) — the Hub path, under which the brief and the note are written; the Hub slot's idiom and the Clean read of the hub operation that writes them; and the List read, which gives the path of the tree the next session opens in. Absent → today's path. Filled → the brief and the note go to the hub, as `## Where it goes` and `## The dated note` say.
- `session-launch` (optional) — the Launch command, the List command and the Confirm window, which `references/launch.md` runs, and the *Context bound* and the *Context read*, which `## When to hand off` reads, all this session's own; what the CLI behind those commands does is `.claude/references/core/claude-code-cli.md`'s. The Hub session name, which `references/launch.md` condition 2 reads and the brief's Reporting section writes out, is the target's: read from the target tree's own bindings by the contract's lookup rooted at that tree — the `.claude/bindings/<pack>.md` there whose `## session-launch` section answers it, that tree's `CLAUDE.md` *Bindings* section as the fallback — as the contract reads `session-launch`. This session's binding absent → no launch: `## What you show me` gives the paste line. The target's binding absent, or its Hub session name missing or `none` → no Reporting section and no launch, and the paste line naming the slot; the Launch command, the List command or the Confirm window missing or `none` → the paste line naming the slot. The *Context bound* or the *Context read* missing or `none` → handoffs at phase boundaries only, as `## When to hand off` says.
- `verification` (optional) — the *Path form*, which RUN writes the target's path in. Absent, `none` or missing the slot → no launch: the paste line naming the slot, as `references/launch.md` condition 2 says, never a stop.
- No agent: this skill dispatches nothing.

## Where it goes

Write the brief to a file — **do not** print it as a copy-pasteable block.

- Path: `memory/handoffs/<YYYY-MM-DD>-<short-kebab-slug>.md` under the project root (the directory the session opened), where the slug names the objective. Where `trees` is filled, the project root here is the hub: the brief goes under the Hub path's `memory/handoffs/` by absolute path, whichever tree the session opened in, and the pointer line of `## What you show me` carries that absolute path.
- Create `memory/handoffs/` if it does not exist. Only where the project root has no `memory/` at all, fall back to `~/.claude/handoffs/`.
- If that exact filename already exists, append `-2`, `-3`, … rather than overwriting.
- Start the file with a one-line instruction to the new session (e.g. "You're picking up: <objective>. Read the files below, then start at Next steps."), then the sections.

## The dated note

Besides the brief, write the session's dated note, `memory/<YYYY-MM-DD>.md` under the project root for today's date, created when absent. Re-read it first, since concurrent sessions share it, then append one entry at its end and edit nothing above: an H1 naming the date, the branch or task and `handoff`, then `## Summary`, `## Changes`, `## Decisions` and `## Open issues` in that order, none dropped, an empty one saying so in one line. `## Summary` names the handoff's objective and the brief's path; the other three carry what this session changed, ruled and left open that no earlier entry of the note records. The entry moves nothing out of `memory/STATUS.md`'s `## Current`, since the branch handed off is still active, and this skill writes no STATUS line. Where `trees` is filled, the note is the hub's, `memory/<YYYY-MM-DD>.md` under the Hub path, and the brief and the note are written together as one hub operation, as the trees contract (`.claude/references/core/trees-contract.md`) defines it, or as today under its exception for a hub session on a branch with no tree of its own.

Where the project root has no `memory/` at all, say so and write no note: the user-level fallback under `## Where it goes` is the brief's, never the note's.

A launch brief takes no dated note: the brief `g0-open` or `tackle-issue` has this skill write for a new branch (`## Sections`) is written alone — where `trees` is filled, as one hub operation of its own — the committed brief is its record, and the day's note is neither re-read nor appended to for it.

## Sections

Use these; drop any that are genuinely empty rather than padding them — except Lane, which every brief carries, and Reporting, which is present exactly where the target's `session-launch` binding reads filled with its Hub session name and the brief's target is not the hub:

1. **Objective** — the one task, in a sentence or two. Concrete and verifiable.
2. **Lane** — `design`, `feature`, `light` or `lab`: the lane of the task handed off — the work's own, or else the one its branch gives, matched against the forms the `vc` binding's *Branch procedure* names (a `<…>` placeholder matches any text): its design, feature, light-lane or lab form, the lab form `lab-<topic>` where the binding names none. A light lane's value is one ASCII line, `light, class <c>`, or for a batch `light, class <c>, batch <id>, <id>...`: the class the work declared, slim where none was.
3. **Context** — the minimum background needed to understand *why*, plus any project or workflow state that matters (active branch, gate or stage, design decisions in play). Pull from `memory/STATUS.md`, the latest session note, and the project's process docs per the `process-docs` binding. Where `trees` is filled, name the tree the next session opens in — the branch's own tree, by its path per the List read, or the hub — and say that `memory/` lives in the hub, at the Hub path, where the STATUS and the session note this section pulls from are read.
4. **Current state** — what's already done and what's in progress, so the next session doesn't redo it.
5. **Key files & locations** — exact paths (with line refs where useful) the task touches.
6. **Constraints & decisions** — locked choices, conventions, and gotchas that must be respected; things NOT to change.
7. **Next steps** — an ordered, concrete starting sequence. **State, not process:** if the next task is a workflow step, this section names the skill to invoke plus the state it needs — it never re-transcribes that skill's own steps.
8. **Open questions** — anything unresolved that needs the human's input, so the new session asks instead of guessing.
9. **Reporting** — only where the target's `session-launch` binding reads filled with its Hub session name, as `## Bindings` reads it, and the brief's target is not the hub — the target is the hub where `trees` reads filled and the next session opens at the Hub path: the text `references/launch.md` `## Reporting` gives, written into the brief as it says. Where the target is this session's own project root, this session reads `CLAUDE_CODE_SESSION_ID` in the shell (`echo "$CLAUDE_CODE_SESSION_ID"`) as it writes the brief and writes the value, whole, into that text's STARTED clause as `## Reporting` says; unset or empty → no predecessor, and `## What you show me` says so.

Keep it tight and skimmable. Prefer precise paths and names over prose. Don't invent facts to fill a section — if something is unknown, say so.

**A launch brief for a new branch** — written when `g0-open` or `tackle-issue` invokes this skill for a branch it has just cut, naming the new tree as the target, the lane, and the skill the branch's session resumes — carries four sections, or five where Reporting applies (item 9): Objective — agree the G0 scope of the item, its line quoted, or work issue `<n>`; Lane — `design` or `feature` from `g0-open`, `light, class <c>`, or for a batch `light, class <c>, batch <id>, <id>...` from `tackle-issue`; Context — the backlog line or the issue, the new tree's path by the List read, and that `memory/` lives in the hub, at the Hub path; Next steps — invoke `g0-open`, whose branch run starts at SCOPE, or `tackle-issue` for issue `<n>`, whose resumed run goes on from DIAGNOSE; and Reporting, as item 9 says. It is written under the hub's `memory/handoffs/` as one hub operation, with no dated note, and its prompt is the paste line of `## What you show me`.

After the brief is written, where the `session-launch` binding reads filled, as the contract reads it, this session launches the next one as `references/launch.md` says; otherwise nothing is launched, and `## What you show me` gives the paste line.

## What you show me

After writing the file, where `references/launch.md` ran and CONFIRM saw the new session, output one sentence naming the objective and the file path, then CONFIRM's one line, and nothing else of substance but the `CLAUDE_CODE_SESSION_ID` line below, where it applies.

Otherwise — `references/launch.md`'s conditions not all met, or its BEFORE or CONFIRM ending in its one line, which comes first — output exactly two things, and the `CLAUDE_CODE_SESSION_ID` line below where it applies, and nothing else of substance:

1. One sentence naming the objective and the file path, so the human knows what was written. Where the next session opens in a tree other than this session's project root, the sentence also names that tree's path and tells the human to open a plain `claude` session there, never `claude -w`.
2. A single fenced line to copy-paste as the first message of a fresh session, built at run time from the file's **absolute** path:

```
Read the handoff brief at <the file's absolute path> and pick up from there.
```

Where item 9's read found `CLAUDE_CODE_SESSION_ID` unset or empty, either output above, the launched one or the paste-line one, ends with one line more, its one addition: `CLAUDE_CODE_SESSION_ID` was unset or empty, so the brief names no predecessor and the hub will not stop this session; close it yourself once the next session starts.

Do not reproduce the brief's contents in the conversation.
