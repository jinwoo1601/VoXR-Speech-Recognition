# Prose-held invariants

The standing rules of this harness that live only in prose, where no check can read them. Every review brief carries the entries below: the BRIEF steps of `review-cycle` and `review-pr` copy them verbatim and numbered, as the first items of the brief's `## Load-bearing invariants`, under one lead-in line, whatever narrowing the human rules. Only the entries travel; this paragraph and the admission rule stay here. A finding against an entry is a breach of it in the change under review, never the observation that no check enforces it.

## Admission

An entry is a rule that lives only in prose and that no check can read. It leaves this list when a mechanical check takes it over. Adding or retiring an entry is a change to this file, reviewed and versioned like any core change. An entry states the rule; where it lives, by durable address — a file and a step or section name, never a line number, so this file stays true as those files move; what partial guard exists and why no check reads the rest; and what would retire it.

## Entries

1. **The collapsed merge rule.** At G1, at G2 and at G2-lite the ruling is itself the word to merge, and there is no second merge stop; what was authored after the ruling is shown to the human first. *Lives in:* `g1-lock`'s MERGE step; `g2-accept`'s MERGE step; `g2-lite`'s MERGE step, the light lane's close. *Partial guard:* `check_v7` sees only that a skill's `stops:` is non-empty; no check reads which ruling authorizes a merge, or whether a second merge stop was added. *Retired by:* a check that reads each gate skill's merge authority.
2. **Core names no project particular, and takes what it needs through bindings.** Both clauses of `core/PACK.md`'s first paragraph under its title: nothing in core names a version-control system, an engine, a doc layout or a project; and what core needs from a project arrives through bindings. *Partial guard:* `test_ku`'s word list guards the first clause, for the words it lists; nothing checks the second, since no check can tell a project fact a core file assumes from one it reads through a binding. *Retired by:* a check that reads the second clause; the first clause's word list stays where it is.
