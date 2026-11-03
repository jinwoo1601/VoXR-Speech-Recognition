# The development path

The constitution's **Paths** paragraph has a path's pack hold its steps. These are the development path's, binding on every piece of work on it, each held by the enforcer `## The enforcers` names.

## The steps

2. Write the requirements doc, then the architecture doc, where the process-docs binding says. The architecture doc is the feature's working record — the living document that holds the how, the build plan, and what the build taught. It is not a draft of the product page: the product docs are authored after G2 from live source. One obligation crosses over — its decision register is the design rationale the product docs carry forward.
3. Plan the implementation. **Persist the plan** beside the architecture doc, where the process-docs binding puts phase plans — plan-mode output is otherwise lost at session end. The architecture doc links each plan and keeps the design, the decision register and what the build taught.
4. Implement.
5. Review — a code-review pass, agent or human — and test: the checks the verification binding names plus any human-only verification.
6. **G2 — feature accepted / merge-ready (human sign-off; hard stop).**
7. Only now write or update the product docs, where the product-docs binding says. Then merge the feature branch.

## Feature-lite

**Feature-lite.** A backlog feature whose locked design already fixes every file-level choice may be marked lite in the design's backlog, under the human's G1. A lite feature has no requirements doc — its acceptance criteria live in its backlog row — and its architecture doc holds the links to its plan files and what the build taught. G2 is unchanged. A build that meets a choice the design did not fix returns the feature to the full track, which writes its requirements doc.

`.claude/references/core/feature-lite.md` holds the procedure.

## The enforcers

| Rule | Enforcer |
|---|---|
| The requirements doc is written first, then the architecture doc, where the process-docs binding says. | `g0-open` RECORD opens the requirements doc; where composed, the `architecture-doc` skill's prerequisites require the requirements doc, and the `phase-pickup` skill's both docs; the `architect` body answers `BLOCKED` to a brief missing an input its mode names. Each, a lite feature excepted (`.claude/references/core/feature-lite.md` `## The acceptance source`). |
| Product docs are authored after G2 from live source, carrying the architecture doc's decision register forward as design rationale. | `g2-accept` PRODUCT DOCS, every claim verified against live source; carrying the register forward is the product-docs reconciler's, the agent the `product-docs` binding names; partial guard: a `doctor` warning row, where a pack's product-docs page's stated version differs from its `PACK.md`, pinned by the `test_cdx_` tests in `scripts/tests/test_harness.py`; it leaves every other claim on the page and a page with no such sentence. |
| Each phase's plan is persisted beside the architecture doc, where the process-docs binding puts phase plans. | Where composed, the `phase-pickup` skill's PERSIST step, where `architect` writes the plan file at the path the `process-docs` binding names; `implement` Prerequisites: the phase's plan file exists at that path and `plan-validator` has validated it, or it was recorded unvalidated under the human's ruling at the plan stop. |
| The architecture doc links each plan and keeps the design, the decision register and what the build taught. | `gate-preflight` checklist item 3 audits `## As-built deltas` and the decision register at G2. |
| Implement. | `implement` Prerequisites: a persisted phase plan, validated or recorded unvalidated under the human's ruling at the plan stop, before any dispatch. |
| Review is a code-review pass, agent or human; test is the verification binding's checks plus any human-only verification. | `review-cycle`, on the full profile at the feature track's G2; `gate-preflight` checklist item 2, *Tests: claimed vs run*, fails a check claimed green without run evidence, a human-only check included. |
| G2 — feature accepted / merge-ready (human sign-off; hard stop). | The G2 gate: `g2-accept` HARD STOP, the G2 ruling, before RECORD, PRODUCT DOCS and MERGE; partial guard: V7, which sees only that `stops:` is non-empty. |
| Product docs are written only after G2; then the feature branch merges. | `g2-accept` PRODUCT DOCS, then MERGE, the G2 ruling being the word to merge (`.claude/references/core/prose-invariants.md` `## Entries` entry 1). |
| A feature-lite, marked in the design's backlog under G1, has no requirements doc; its acceptance lives in its backlog row, its architecture doc holds its plan links and what the build taught, G2 is unchanged, and a choice the design did not fix returns it to the full track. | `g1-lock` PRESENT shows the marks under the G1 ruling and BACKLOG writes them; `g0-open` RECORD puts the G0 record in the architecture doc; each skill on the path after G1 makes the lite test of `.claude/references/core/feature-lite.md` `## The mark` and names the track in its briefs, `implement` and `review-cycle` through the brief template each fills; `gate-preflight` item 3 audits the row's criteria and item 6 the room for the G2 record; `g2-accept` RECORD writes that record through `architect-reconcile`; the return to full: `architect` `## Discipline`, `plan-validator` check 4 and `implement` RECEIPT; no check reads the lite mark. |
