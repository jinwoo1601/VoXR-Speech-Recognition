# Decision brief — <slug>

<!-- Written by the PM (or by `g0-open` / `g1-lock` / `g2-accept`) into
     .scratch/doc-writer-<slug>-brief.md before dispatch.
     The doc-writer reads this file by ABSOLUTE path; it is its entire context.
     Anything not written here does not exist for the agent.
     One brief may name several targets and stays at most about 8,000
     characters (the delegation contract's ## The brief (down)).
     Common fields per .claude/references/core/brief-fields.md, then the doc-writer extensions. -->

## Task

- Agent: `doc-writer`
- Baseline model: `<the agent file's model:>`; downgrade for this dispatch: `<none | sonnet — a dictated edit, per the delegation contract's ## Modes>`
- Binding to consult: `<the process-docs binding, when the target is a process doc | the lines this brief quotes under Decision record, when the target is a bindings file or project CLAUDE.md prose>`
- Task: `<one line — what document or section is being written, and why now>`

## Target

<!-- Repeat the four lines per target when the brief names several, in the order they are to be written. -->
- File: `<absolute path>`
- Action: `<create | modify>`
- Section: `<the exact heading, when the target is a section of an existing file — otherwise: the whole file>`
- Form to match: `<absolute path of the doc or template the target must look like>`

## Decision record

- The question: `<what was decided>`
- Options considered: `<each option, one line>`
- Ruling: `<what was ruled, by whom, when>`
- Consequences: `<what the ruling changes elsewhere>`

<!-- A binding interview's brief only, every other brief leaving out the two lines below: after the four lines, one group per key, its Interview key line, then one line per slot with its label and the human's answer. A default the PM proposed is written with the human's words that ruled it; a slot answered under a question other than its key's adds the asked-under field naming that question, left out otherwise; a slot the human did not answer reads `no answer`. A brief dictating a bindings value under another ruling keeps the four lines and says under Ruling which ruling it is; the PM's own ruling of a slot is no such ruling. -->
- Interview key `<key>` — asked: `<the key's question, as put to the human>`
  - `<slot label>`: `<the human's answer, in the human's words | the default the PM proposed, then the human's words that ruled it | no answer>` — asked under: `<the question it was answered under>`

## Inputs

<!-- Absolute paths only: the sources to transcribe or cite, and the form-to-match docs. -->
- `<absolute path>` — `<what it supplies>`

## Deliverable

`<what each target file or section contains, by heading, in the targets' order>`

## Acceptance criteria for this dispatch

<!-- Each criterion is stated over the write scope. -->
1. ...
2. ...

## What may not be invented

Anything not written in this brief does not exist for the agent.

- No decision beyond the record above.
- No heading the target's form does not have.
- No value for a slot the record leaves empty — report `PARTIAL` with the open question instead.
- No value for a binding slot but from a slot line carrying the human's answer, or as dictated under a ruling the record names that is not the PM's own ruling of the slot — report `PARTIAL` naming any other slot.

## Declared out of scope

- ...

## Project rules

- <the lines of the project's `CLAUDE.md` or other standing instruction the task must obey, quoted, or `none`>

## Constraints

- The target paths above are the write scope; inside each, only the section named for it.
- Match the target's existing form: frontmatter keys, heading levels, table shapes, voice.
- No absolute path inside a file that ships in a pack.

## Verification command

`<none bound — a document has no verification command>` — or the check the PM names, e.g. the five `## <key>` headings present.

## Stop-and-report rule

A missing field is answered `BLOCKED` naming it. An ambiguity the record does not settle is an open question in the report and the status is `PARTIAL` — never resolve it yourself.
