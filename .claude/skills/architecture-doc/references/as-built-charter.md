# The architecture doc's `## As-built deltas` charter

**The `## As-built deltas` charter.** The section is about the plan, and the plan lives in the plan files the architecture doc links: four kinds of entry are **admitted** to it, two are **amended into `## Decision register`** in place with a dated line instead, a fact the landed tree has made false in an always-current section is **corrected in place** where it stands, and every other kind is **routed** to a home that already receives it — the session's dated note, `memory/<YYYY-MM-DD>.md`; the cross-session state the `board` binding names, `memory/STATUS.md` where none is bound; or the lessons file, `memory/notes-for-future-features.md`. No new home is built for what is routed out.

**Admitted**, each a statement about the plan, a few lines each, the detail in the session's dated note: (1) divergence between the persisted plan and the tree as built, with the ruling that settled it; (2) "built as planned" confirmations, which are a real result; (3) citation re-pointing, plan-time line numbers read against the landed tree; (4) post-G2 addenda from unrelated later work, carrying their date.

**Amended into `## Decision register`**, in place and carrying the amendment's date, in the form `**Amended YYYY-MM-DD:**` appended to the decision it extends: a ruling that extends a decision the architecture doc already carries, and a trap or do-not-undo notice for a later maintainer. Both are design rationale, and the register is the one section carried forward into the product surface the `product-docs` binding names; filed under `## As-built deltas` instead, a decision would reach that surface without the category later added to it.

**Corrected in place**, by the reconcile pass: a fact the landed tree has made false in an always-current section — `What this implements`, `Context & scope`, `Seams`, `Non-functional realization`, `Risks, tradeoffs & open questions` — is corrected where it stands, and each correction is logged in that phase's entry under `## As-built deltas`, with what the passage said and what it says now, as citation re-pointing is. A change of decision is never a correction: it goes to a dated `## Decision register` amendment. Dated passages — plan files, the entries under `## As-built deltas`, `**Amended YYYY-MM-DD:**` clauses — stay records and are not corrected.

**Routed out**, each to a home that already receives it: gate evidence (commit shas, test counts, exit codes, live-run transcripts), review-cycle records, and authorship or provenance confessions about the architecture doc itself go to the session's dated note, `memory/<YYYY-MM-DD>.md`; deferrals and named limitations go to the cross-session state the `board` binding names — `memory/STATUS.md` under *Deferred*, one line each, where none is bound; process and methodology lessons aimed at future features go to `memory/notes-for-future-features.md`.

**Frontmatter `updated:`** — the reconcile pass also moves the doc's frontmatter `updated:` to its own date: bookkeeping, never logged as a correction, never a change of decision.

**A persisted phase plan is never edited** — it is the record of what its planner knew on its own date, which is why re-pointing its citations is an admitted kind under `## As-built deltas` rather than a correction in the plan.

## Bindings

- `process-docs` [Persisted plan] (required) — the slot of that name, the path a phase's plan file is written at, the plan the admitted kinds speak of.

<!-- harness:binding:start process-docs .claude/bindings/core.md -->
- Persisted plan: `Planning~/features/<feature>/plan-<id>.md`
<!-- harness:binding:end process-docs -->
