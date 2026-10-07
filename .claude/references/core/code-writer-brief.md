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
- `<for each name in .claude/harness.json's packs, in the manifest's order, .claude/references/<pack>/writer-rules.md where that file exists, one line each by that repo-relative path; with no manifest or no such file, this bullet is left out>`

## Verification command

`<the verification binding's Targeted command with this phase's targets, verbatim — or: not run — the gate runs the suite — or: none bound — or: not run — not tree-safe>` — expected green: `<pattern, or n/a>`

Or, where the binding carries a Fallback — a line whose label begins `Fallback` and whose answer is not `none` — and the targeted run covers a Command that line names as replaced, the routed form below, judged at BRIEF from the binding's words; on the split path, `Tree-safe` `no` still gives `not run — not tree-safe` first:

- Selector: `<the selector the binding's Fallback line names, verbatim>`
- On `<the result that line names for the Commands>`: `<the Targeted command on this phase's targets>` — expected green: `<…>`
- On any other result, unreadable output included: `<the Fallback narrowed as the Targeted slot says>` — expected green: `<…>` — or `not run — the gate runs the suite` where it cannot narrow

## Declared out of scope

- …

## Project rules

- <the lines of the project's `CLAUDE.md` or other standing instruction the task must obey, quoted, or `none`>

## Constraints

- One phase per dispatch. Exactly the files above. No redesign, no scope expansion, no secrets.
- Write only by the path the *Working-dir read* quoted under Conventions prints for the working directory, in the *Path form* quoted there, joined with a repo-relative path from this brief — never by a shell form that *Path form* says is never written by.
- Bash runs the quoted *Working-dir read* once, before the first write; the verification command — a routed one as the Verification command states; and the commands granted under `## Granted commands` by each writer-rules reference named under Conventions, within the limits each bullet states — and nothing else.

## Stop-and-report rule

On a plan defect, a missing input, or a criterion that cannot be met as planned: stop and report `BLOCKED` naming it. Do not adapt.
