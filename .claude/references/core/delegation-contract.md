# Delegation contract

The exchange between the PM — or a skill acting for it — and one agent: a brief down, a report up. Distinct from the session handoff (the `handoff` skill: a brief from one PM session to the next); both share the cold-reader discipline — the reader has no context, every path is absolute, save the repository paths inside a code-writer brief, which are repo-relative.

## Parties and direction

The PM, or a skill acting for it, delegates to one agent. Agents never delegate to each other; an agent's report returns to its delegator only. The skill is the collector: it collates reports, and the human sees the collation, never raw reports.

One mechanism: dispatch is the Agent tool without a `name`; a named call spawns a teammate and is outside this contract.

## The brief (down)

The common fields, and where a brief is filed: `.claude/references/core/brief-fields.md` `## The brief (down)`, this contract's field list.

Agent-specific templates in this directory extend it: `code-writer-brief.md` (`code-writer`, filed by `implement`), `review-brief.md` (`review-angle` and `finding-verifier`, filed by `review-cycle`), `decision-brief.md` (`doc-writer`), `debug-brief.md` (`debugger`). The governing sentence: **anything not written in the brief does not exist for the agent.**

**Size.** A brief carries what the agent cannot read for itself — decisions, values, criteria. A plan or a doc the agent can read goes by path, never pasted. A `doc-writer` brief may name several targets and stays at most about 8,000 characters; past that, split it.

**Hygiene.** Before dispatch the delegator: verifies every date against the environment, and every count, line figure and claim about the tree or about a tool's behaviour against the tree or a run — a claim inherited from a handoff, a note or a recon report included, and one not measured is not written; cites by symbol, section or quoted text, a line figure only beside such an anchor; states each acceptance criterion over the write scope, or widens the scope to the criterion, since a whole-file criterion over a one-section scope contradicts itself.

## Bindings

A binding is a project fact a core agent or skill needs and may not assume. It is looked up in `.claude/bindings/<pack>.md`, in the `## <key>` section — the shape `validate` checks (V5): present, non-empty, and free of `TODO`. Before a project is scaffolded, the project `CLAUDE.md`'s *Bindings* section is the fallback for the same key. `none` is a filled value — a project with no product docs writes `none bound`, a project that can run nothing writes `none` under `verification` — and is read as such, not as absent. A brief may quote the binding's lines instead of pointing at them; the quoted lines are then the binding for that dispatch. The core keys are `process-docs`, `product-docs`, `vc`, `verification`, `board`, `trees`, `session-launch`, `review`, `layout`; their templates ship with the core pack as `bindings/<key>.template.md`, and every project `CLAUDE.md`'s managed block names the file; an agent body states the lookup itself. Whether `trees` or `session-launch` reads filled, and what a filled `trees` means, are `.claude/references/core/trees-contract.md`'s: the trees contract.

A skill's `Agents` bullet declares, per agent, where the skill stops or how it degrades when that agent is missing from the composed set. Either way the skill says so, and the PM never does that agent's work or improvises a substitute.

## The report (up)

The first line, the trailer and a recon agent's index: `.claude/references/core/brief-fields.md` `## The report (up)`, this contract's report form.

A report returns as text, never as a file its agent writes: a brief never names a report path for its agent to write, and a deliverable that is a report is returned as a message — the agent's final message, and for a recon agent also the `# Full report —` message ahead of its index (`.claude/references/core/brief-fields.md` `## The report (up)`). A writing agent (`code-writer`, `doc-writer`) writes its target files, never its report. Who files which report, with core's report filer running or off, is `## Filing`'s.

## Status words

- `DONE` — the deliverable was produced; a negative verdict is still `DONE`.
- `PARTIAL` — the gap named.
- `BLOCKED` — the reason named.
- `NO-BINDING` — the binding the agent must consult is absent (the first-line rule of design §6.2).

The status word is the outcome of the task. A binding that is present is never reported on the first line: the binding sentence at the top of an agent body governs only the case where the binding is absent.

There is no `FAILED`.

## Receipt validation

A brief missing a required field is answered `BLOCKED` naming the field — never guessed.

## Filing

The brief, always, save that a read-only roster agent may take its brief inline, its report filed under the scratch area's reports folder (`.claude/references/core/constitution-annex.md` `## Agent and skill contracts`). A writing agent's report is filed beside its brief by the delegator — it is the record of what changed. Where core's report filer runs (`.claude/references/core/hooks.md`), it files each agent's final message beside the brief its first line names, as `<brief file name without -brief.md>-report.md`, numbering `-2`, `-3` for further answers to the same brief, with a recon agent's `# Full report —` message ahead of it; a filing step of the delegator's is then met by that file, and the delegator files by hand only where the hook is off. Read-only agents' reports are not filed raw by the delegator — where the hook files them, they are records read on demand — and the skill still files its one collation (the deduplicated findings, the verdicts). Where the hook is off, a recon agent is the exception: the delegator files its full report beside its brief as `<brief file name without -brief.md>-report.md`, extracted from the agent's transcript — Claude Code keeps it as `subagents/agent-<agent id>.jsonl` in the session's directory under Claude Code's projects directory in the user's home — by a command that writes the `# Full report —` message's text to the file without printing it. The delegator works from the index and reads the filed report on demand, by section.

## Rules

- Read-only agents may run in parallel; writing agents run in parallel only each in its own tree, per the constitution's *One writer per tree*, and otherwise serially, one per tree.
- Agents never commit — the version-control ceremony agent excepted, it *is* the ceremony.
- The baseline model is in the agent file; a brief may downgrade, never upgrade. Downgrades follow `## Modes`.
- Any looped agent: two fix iterations, then the escalation its skill names.
- Continue an agent by `SendMessage` only while it is small or the fix needs what it has loaded; otherwise dispatch a fresh short brief naming the delta — a continued agent carries its whole context forward.
- An agent's own success claim is never the gate.

## Modes

Which dispatches a brief may downgrade to `sonnet`; an agent whose baseline is already `sonnet` is not downgraded.

| Agent | Baseline | `sonnet` for | Baseline for |
|---|---|---|---|
| `code-writer` | `opus` | mechanical phases: version bumps, text the plan dictates verbatim, a fix pass over a listed error set | a phase whose plan leaves wording or structure to the writer, or adds logic |
| `doc-writer` | `opus` | dictated edits: the brief carries the text, or a transcription | prose composed from a decision record |
| `debugger` | `opus` | never — root cause is judgment | every dispatch |
| `architect` | `inherit` | never — its as-built and record passes go to `architect-reconcile` | every pass it owns |

## Where it lives

One clause in the constitution; the field lists in `brief-fields.md`; each agent file's `## Output` section conforms; each orchestration skill's brief template extends the common brief; `validate` checks the Output convention (V12) and the tool allowlist (V6).
