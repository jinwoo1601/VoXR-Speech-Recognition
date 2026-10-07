---
name: implement
description: "Build one implementation phase whose plan is already persisted and validated, through code-writer and a verification gate. Use on 'implement phase N' once the phase's plan is persisted and validated."
kind: orchestration
agents: [code-writer, compile-check, debugger]
stops: [escalation after two failed fix loops, the branch tree not clean before the fan-out, a failed or misplaced dispatch or a unit not DONE, a stray file or a changed branch tree at the join, a conflict at the merge of the join]
status: active
bindings: [verification, vc, trees, process-docs, session-launch]
---

# implement

## What this is

**One phase, one writer, one independent gate — or, where the persisted plan splits the phase into units and `trees` is filled, one writer per unit, each in its own tree, joined onto the branch before that one gate.** A brief goes down to `code-writer`; the writer runs its targeted checks, and `compile-check` runs the suite as the gate; when the gate fails for a reason its error list does not explain, `debugger` finds the cause read-only — at most two fix loops, then the human.

> The agent's own report is never the evidence. Agents write, the gate verifies, the PM commits and rules.

## Bindings

Each key is looked up as `.claude/references/core/delegation-contract.md` `## Bindings` says: `.claude/bindings/<pack>.md` first, the project `CLAUDE.md` *Bindings* section as the pre-scaffold fallback.

- `verification` (required) — the commands `compile-check` runs at GATE: the suite runs at GATE, once per gate, and never in the writer. The writer runs only the `Targeted` slot's command, with the targets the plan names for the phase's own files, quoted in the brief under Verification command. Where the binding carries a Fallback whose replaced Commands the targeted run covers, the brief takes the routed form of `.claude/references/core/code-writer-brief.md` `## Verification command` instead, a LOOP's targeted command too; every other targeted run is quoted as above. A `Targeted` slot that is missing, carries `TODO` or answers `none` → the brief says `not run — the gate runs the suite`, and the writer runs nothing. `none` is a filled value: the brief says `none bound`, GATE is recorded as `nothing to run`, and nothing loops. A fix brief takes it on the gate's failing checks. Every code-writer brief also quotes the binding's *Shell*, *Working-dir read* and *Path form* lines under Conventions, verbatim — the shell the writer's commands run in, the read that prints its working directory, and the form it writes paths in; a filled binding lacking one of them → stop and say so, naming the slot, before any brief is filed — except a binding that is a single `none`, whose platform slots are not read: the brief then says the working directory is read by `pwd` and written by the path it prints, never a stop for that reason. Absent → stop and say so before any brief is filed.
- `vc` (required) — the base-revision reads behind the branch prerequisite, and the check-in procedure of the PM's closing commit. Absent → stop and say so.
- `trees` (optional) — the slots `references/split-path.md` `## Bindings` names. Absent → today's path. Filled → where the persisted plan names units, the steps run as `references/split-path.md` orders them; otherwise `## Workflow` runs as written.
- `process-docs` (required) — the path a phase's plan file is written at, by which the prerequisites find the phase's plan file and BRIEF names it. Absent → stop and say so before any brief is filed.
- `session-launch` (optional) — the *Context bound* and the *Context read*, which the rhythm reads at each step's end, as `handoff` `## When to hand off` says. Absent, either slot missing or `none`, or the read failed → the phase-boundary handoff only, the slot named once; never a stop.
- Agents — `code-writer` or `compile-check` missing → stop before any dispatch and say so. `debugger` missing → DIAGNOSE is unavailable, say so, and the loop runs on the parsed error list alone.

## Prerequisites

Confirm before proceeding:
- The phase's plan file exists at the path the process-docs binding names and was validated (`plan-validator`).
- The checkout is on the feature branch per the `vc` binding's base-revision reads (the current branch), never the main branch.
- The `verification` binding has been read: its commands, or `none`.

## Workflow

Where the persisted plan names units and `trees` is filled, as the contract reads it, these steps run as `references/split-path.md` orders them; otherwise they run as written — a plan that names units while `trees` reads absent runs here as one writer on its whole files table, every unit's rows and the `serial` rows alike.

1. **BRIEF** — fill `.claude/references/core/code-writer-brief.md` into `.scratch/implement-<feature>-<phase>-brief.md` (create the folder if absent): the plan reference, naming the phase's plan file by that path, the files in scope from the plan's table, this dispatch's acceptance criteria — which also carry each entry of the plan's `Runtime claims` list, to be checked by running it and reported true or false with its evidence — conventions, with each composed pack's writer-rules reference as the template's Conventions says, the Verification command as `## Bindings` says, out of scope, the project rules, as the template's Project rules says, constraints, the stop-and-report rule. Anything not in the brief does not exist for the agent.
2. **DISPATCH** — one `code-writer`, serially (one writer per tree), through the Agent tool without a `name`, model per the brief, with the brief's absolute path as its entire context.
3. **RECEIPT** — the report's first line must be a status word. `BLOCKED` or `PARTIAL` → stop, file the report, report to the PM; no loop. A report without a status line is a contract violation: report it, do not act on it. Where the feature is lite and a `BLOCKED` report names a design choice the plan does not carry, the feature returns to the full track, as `.claude/references/core/feature-lite.md` `## The escape` says.
4. **GATE** — dispatch `compile-check` on the `verification` binding. The suite runs here, once, on the bound commands in full. Where the plan dictates text verbatim, the gate also searches the landed files for that text, fixed-string; the writer's report that it is verbatim is not the check. Where the phase edits files that a compose step re-derives, such as a base repository's pack sources, the gate runs after that step, not after the dispatch: the source and its composed copy change as one unit. This first gate also reads the report's runtime-claim results: a claim reported false or unchecked is a plan defect — stop and report to the PM, with no LOOP. `none` → record `nothing to run` and go to FILE.
5. **DIAGNOSE (optional)** — when the gate fails for a reason the parsed error list does not explain, the PM files a debug brief from `.claude/references/core/debug-brief.md` into `.scratch/debugger-<slug>-brief.md` and dispatches `debugger` (read-only); its fix proposal joins the re-dispatch brief as an addendum. It does not count as a fix iteration.
6. **LOOP** — on FAIL, re-dispatch `code-writer` with the same brief plus the gate's parsed `file:line` error list, its Verification command the targeted command on the gate's failing checks, then gate again. At most two fix iterations; after the second failure, stop and escalate to the human with both reports.
7. **FILE** — save the final report beside the brief as `implement-<feature>-<phase>-report.md` — met by core's report filer where it runs (the delegation contract's `## Filing`); by hand otherwise.
8. **REPORT** — to the PM: status, files written, verification result, attempts used, suggested next step, and the agent's suggested CHANGELOG line. The commit is the PM's, per the `vc` binding's check-in procedure; this skill makes no commit of its own.

**The rhythm.** At the end of each step above — on the split path, each step of `references/split-path.md` — the session applies `handoff` `## When to hand off`: it runs the *Context read*, and past the *Context bound* it invokes `handoff`, its objective the next step, instead of taking that step. A handoff taken before FILE first files each report received so far beside its brief, and its brief names those reports by path and the fix iterations used. After REPORT there is no next step, and the objective is the skill that follows — the next phase's `phase-pickup`, or `review-cycle` after the build plan's last phase — as `handoff` `## When to hand off` says of a skill's last step; at or below the bound, a next phase's pickup is itself a phase boundary, where `phase-pickup` ORIENT hands off whatever the context.

Agents write, the gate verifies, the PM commits and rules.
