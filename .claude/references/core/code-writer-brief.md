# Code-writer brief — <feature> / <phase>

<!-- Written by the implement skill (or the PM) into .scratch/ before dispatch.
     The code-writer reads this file by ABSOLUTE path; it is its entire context.
     Anything not written here does not exist for the agent.
     Common fields per .claude/references/core/delegation-contract.md, then the code-writer extensions. -->

## Task

- Agent: `code-writer`
- Baseline model: `<the agent file's model:>`; downgrade for this dispatch: `<none | sonnet — mechanical phase>`
- Binding to consult: the project's `verification` binding, looked up as `.claude/references/core/delegation-contract.md` `## Bindings` says (`.claude/bindings/<pack>.md` first, the project CLAUDE.md as the pre-scaffold fallback), quoted under "Verification command"
- Task: implement phase `<id>` of `<feature>` exactly as persisted

## Plan reference

- Plan file: `<repo-relative path>` (required)
- Architecture doc: `<repo-relative path>` — for the decisions the plan cites, not read whole
- Requirements doc: `<repo-relative path>` (rows `<F…>`) — for a lite feature, by the test of `.claude/references/core/feature-lite.md` `## The mark`, the backlog's path and criteria `<A…>`, with the word `lite`, the feature's name and the row quoted (`.claude/references/core/feature-lite.md` `## The acceptance source`)

## Files in scope (from the plan's files-to-change table)

| File (repo-relative path) | create / modify / delete | Intent (one line) |
|---|---|---|
| … | … | … |

## Acceptance criteria for this dispatch

1. …
2. …

## Conventions

- `.claude/references/core/implementation-rules.md`
- Project bindings: `<the lines of .claude/bindings/<pack>.md — or, before scaffold, of the project CLAUDE.md Bindings — that bind style, layout, verification>`
- The `verification` binding's *Shell*, *Working-dir read* and *Path form* lines, verbatim: `<the three lines>`; a `verification` binding that is a single `none` has none of them — the platform slots are then not read, and the brief says the working directory is read by `pwd` and written by the path it prints
- `.claude/references/core/coding-conventions.md`

## Verification command

`<the verification binding's Targeted command with this phase's targets, verbatim — or: not run — the gate runs the suite — or: none bound — or: not run — not tree-safe>` — expected green: `<pattern, or n/a>`

## Declared out of scope

- …

## Constraints

- One phase per dispatch. Exactly the files above. No redesign, no scope expansion, no secrets.
- Write only by the path the *Working-dir read* quoted under Conventions prints for the working directory, in the *Path form* quoted there, joined with a repo-relative path from this brief — never by a shell form that *Path form* says is never written by.
- Bash runs the quoted *Working-dir read* once, before the first write, and the verification command only.

## Stop-and-report rule

On a plan defect, a missing input, or a criterion that cannot be met as planned: stop and report `BLOCKED` naming it. Do not adapt.
