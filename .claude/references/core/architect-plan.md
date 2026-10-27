# architect — plan mode

Read by `architect` when its brief dispatches it to persist one phase's plan, to apply a persisted plan's ruled fixes — the findings in `plan-validator`'s fix-pass classes ruled real, a canon-conflict excepted since it stops the line, or the human's ruling at the no-row stop that drops work in no build-plan row and no requirement — by rewriting its plan file in that same form, or to return a built phase's plan correction as text (`## The fix pass`). The agent's own body carries what every mode shares: the binding, the inputs every brief gives, the altitude, the discipline and the output.

## Inputs

A brief that dispatches you to persist a phase plan gives you, besides the inputs every brief gives:
- the phase to plan, its scope, its acceptance criteria, and what is declared out of scope;
- the plan file's path — the pattern the `process-docs` binding names, `<feature>` and `<id>` filled;
- the phase's row in the architecture doc's `## Build plan`, quoted, which the plan's `Scope:` line restates.

## What you produce

**One phase's plan.** One file, at the plan file's path the brief names, created whole: it opens `# Phase <id> — persisted plan (<date>)`; then the line `Scope:` and one sentence restating the row's scope as the plan carries it, from the row the brief quotes, naming any work the plan takes on or drops; then, where the brief asks the plan to record that it goes unvalidated, the line `Unvalidated:` and one sentence saying `plan-validator` is not composed; then it carries these six items as `##` sections, in this order:
1. **Files to change** — a table: path, create / modify / delete, why (the requirement or decision it serves). The split into units is yours: weigh the plan's file sets and the cost of cutting a tree per unit against wall-clock time — a small or tightly coupled phase is best serial, and broad disjoint work favours the split; a split the row lacks is taken at the pickup's PLAN without a stop, recorded as a take the G2 packet's side rulings list. Where you split the phase, the table gains a fourth column, **Unit** — one unit id or `serial` per row, each path in exactly one row — and a **Units** table continues this item after it: one row per unit, in join order, giving the unit id (kebab-case, never `serial`), its scope in one line, and the numbers of item 4's criteria that unit's writer discharges, then a `serial` row where the files table has `serial` rows. A unit's write set is the rows naming it; files that change in pairs or are generated, and a file another unit needs in order to stay green, sit with the unit that causes them; criteria no unit discharges are the join's and the gate's; and a file no unit can own alone is a `serial` row, written after the join. Where the phase is unsplit, the table keeps its three columns and no Units table follows.
2. **Contracts and interfaces** — every seam the phase introduces or changes: data shapes, file formats, command lines, report formats.
3. **Trade-offs** — a table with one decision per row: the choice, the alternative, why this one.
4. **Acceptance criteria** — checkboxes, each observable, traced to a requirement row. On a lite feature each is traced to a criterion, A1…An (`.claude/references/core/feature-lite.md` `## The acceptance source`).
5. **Open questions that block implementation** — each with what would resolve it. An ambiguous requirement goes here; you do not pick silently. On a lite feature a choice the locked design does not fix goes here (`.claude/references/core/feature-lite.md` `## The escape`).
6. **Blast radius** — every file, binding, or session the change can affect beyond the files-to-change table.

`## Runtime claims` is its last section — each step whose correctness rests on what something does when run, or `none` — and after it the plan's last line is `Hazards: none`, or `Hazards:` followed by the ordering or runtime hazard the plan declares.

**The link line.** The same dispatch writes one line into the architecture doc's `## Build plan`: `- <label> — persisted plan (<date>): [<path>](<path>)`, where `<label>` is `Phase <id>` and `<path>` is the plan file's path relative to the architecture doc — the path the brief names, from the binding's pattern, made relative to the doc's directory. It goes directly after the last existing link line there, else after the whole breakdown — after its table, or after its list of phase bullets and any assets bullet that closes it — never after or inside a persisted-plan `###` section.

## Discipline

- When the brief dispatches you to persist a phase plan, plan ONE phase. If it names two, plan the first and say so.
- Quote only a heading, a key, a command line or a one-sentence ruling — never a multi-line block of any file's content. Say what a text must say and where, identifiers exact.
- The plan file holds at most the plan-file budget the `process-docs` binding names, where it names one.

## The fix pass

A fix pass's brief gives you, besides the inputs every brief gives save the recon reports, which it does not carry, by path: the built phase's persisted plan, its `code-writer` brief, both failed gates' reports and the `debugger` report. The return is the plan correction as text, in your report: each item of the plan at fault, what it should say, and the files and changes it brings, inside the phase's row and its requirements. You write no file, and the built plan is never edited. The report, filed beside the brief, is the return: the delegator adds it verbatim to `code-writer`'s re-dispatch brief as its `## Plan correction`, which governs over the plan for each item it names. A correction outside the phase's row and its requirements, or one that needs a change to the architecture doc's seams or decision register, is not made: name it in open questions and report `PARTIAL` or `BLOCKED`.
