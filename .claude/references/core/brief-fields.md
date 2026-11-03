# Brief fields

The delegation contract's field list and report form.

## The brief (down)

Filed under the scratch area — `.scratch/` at the project root, one untracked folder per working tree — before dispatch and passed by absolute path; a read-only roster agent may take its brief inline (`.claude/references/core/constitution-annex.md` `## Agent and skill contracts`). Common fields:
- task and agent;
- baseline model and any downgrade;
- the binding to consult;
- scope;
- inputs, by absolute path — save a code-writer brief's repository paths, which are repo-relative;
- the deliverable;
- acceptance criteria;
- declared out-of-scope;
- constraints;
- project rules — the lines of the project's `CLAUDE.md` or other standing instruction the task must obey, quoted, or `none`;
- the verification command, or `none bound`;
- the stop-and-report rule.

## The report (up)

- **First line:** a status word and the brief it answers — `<STATUS> — <brief path or task>`.
- **Body:** what the agent file defines for its role.
- **Refused:** a tool call refused by permission or policy is reported, never retried in another form: whatever the status, one `refused` line per refused call before the trailer, ``refused: <tool>; `<call>`; <the refusal's words>``, the call on one line, whitespace folded, cut to 140 characters, `...` where cut, non-ASCII as `?`, the refusal's words verbatim.
- **Trailer**, fixed: on `DONE`, one line — `Trailer: criteria <all met | the unmet or unchecked, named>; files <absolute paths | none>; verification <command and result | not run — reason>`, plus any item the agent file adds; on `PARTIAL`, `BLOCKED` or `NO-BINDING`, the full trailer — the brief's acceptance criteria each marked met / not met / not checked; files written, by absolute path, or `none`; verification run and result, or `not run`; for looped agents, attempts used and whether escalation is needed; blockers; open questions; suggested next step.
- **Recon index:** a recon agent (`code-mapper`, `doc-extractor`) returns an index, not its report. It writes the full report as one message opening `# Full report — <brief path>`, then reads its brief once more to mark the criteria, then ends with the index as its final message — the first line, one line per answer or site pointing into the full report, the not-found list and the flags in full, any `refused` lines, and the trailer; about 2,000 tokens at most. Only the final message reaches the delegator.

No restating the brief, no narrative, nothing else.
