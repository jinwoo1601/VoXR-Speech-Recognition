---
type: architecture
feature: <kebab-name>
topic: <the design topic this feature belongs to>
status: draft
updated: <YYYY-MM-DD>
sources:
  - <the feature's requirements doc — for a lite feature, the design's backlog>
  - <the locked design doc(s) and decision record(s) this realizes>
  - "<the code this builds or touches>"
---

# <Feature Name> — Architecture

<!-- This is the HOW for the feature, and the feature's working record. The architecture doc is the feature's working record — the living document that holds the how, the build plan, and what the build taught. It is not a draft of the product page: the product docs are authored after G2 from live source. One obligation crosses over — `## Decision register` is the design rationale a reader cannot recover from the code, and it is what the product docs carry forward. Document load-bearing decisions and WHY (with the rejected alternative); cite real paths and symbols so drift is detectable; reconcile to AS-BUILT before G2, under the charter the As-built deltas guidance below carries. Keep each paragraph and list item on one logical line — no hard-wrapped prose; match the link and date form the project's existing process docs use. Delete these guidance comments as you fill each section, except the charter, which the As-built deltas section keeps. -->

<!-- OPENING (one paragraph): what this feature builds, the requirements it realizes, and the one governing architectural driver — often a non-functional row the platform makes load-bearing — that shapes the design below. -->

<!-- LITE FEATURE: `## User story` and `## Context & scope` stay first, as G0 wrote them, and `## Context & scope` is not repeated below (`.claude/references/core/feature-lite.md` `## The three placements`); the design sections cite the locked design and hold only what it does not state; where a comment below names requirement rows, the backlog row's criteria stand (`.claude/references/core/feature-lite.md` `## The acceptance source`). -->

## What this implements

<!-- Optional but powerful: a table mapping each requirement / locked design decision → the work → the section here that covers it. Makes coverage legible and flags verify-only versus new-code items. This is the doc-wide traceability table; the per-phase traceability lives in the Build plan rows below. -->

| Requirement / decision | Work | Section |
|---|---|---|
| <F1 / design decision> | <what gets built> | <§ below> |

## Context & scope

<!-- Where this sits in the existing system; what it owns and what it deliberately does not; the existing modules it touches. Align with the patterns the codebase already uses — cite what you reuse. -->

## Decision register

<!-- The heart, and the one section that crosses over: the rationale written here is what a later product pass lifts out of the doc. The load-bearing, costly-to-reverse choices — each: the decision, WHY, and the rejected alternative kept on record. (Inline here — a feature has no separate decision-record files.) When the build extends one of these decisions, or teaches a trap a later maintainer must not undo, amend the bullet in place with a dated line in the form **Amended YYYY-MM-DD:** rather than filing it under As-built deltas. -->

- **<Decision A — short name>.** <Chosen approach.> *Why:* <rationale — the requirement or constraint it serves>. *Rejected:* <the alternative and why it loses>.

## Components & responsibilities

<!-- Each type, module or object with ONE clear responsibility, and how they collaborate. Cite intended and real paths plus symbols. Treat as drift-prone. -->

- `<TypeOrFile>` — <role>. Cite: `<path>` (`Symbol`).

## Data contract & runtime flow

<!-- The data crossing the seams (events, schemas, per-step structures, serialized and persisted state) AND the runtime flow: what happens per request, per step, lifecycle placement, state machine. A reader should trace an operation end to end from here. -->

## Non-functional realization

<!-- How the requirements doc's non-functional rows are MET structurally — not restated, realized. The budget and its allocation pattern, the behaviour under degraded dependencies, and any structural law the platform imposes. On some platforms this drives the whole design. -->

## Build plan

<!-- The breakdown, and one link line per phase to its plan file (a plan is otherwise lost at session end). Ordered phases, LOWEST-RISK-FIRST, each green by the verification binding's commands and ideally independently shippable. Prototype the riskiest or most visual part first, outside the shipped tree. Flag the assets the build needs that code cannot author. Each row carries three fields — the phase id, its scope, and the requirements it discharges: the table above maps requirement → work → section, this row maps phase → scope → requirements discharged, and the two are not redundant. When each phase is picked up, `architect` writes its plan as its own file, at the path the `process-docs` binding names, and adds one link line here in the form `- <label> — persisted plan (<date>): [plans/phase-<id>.md](plans/phase-<id>.md)`, directly after the last existing link line, else after the whole breakdown — after its table, or after its list of phase bullets and any assets bullet that closes it — never after or inside a persisted-plan `###` section; this section starts as the breakdown only. A phase's plan file may split the phase into units with disjoint write sets: files that change in pairs or are generated, and a file another unit needs in order to stay green, count with the unit that causes them; a file no unit can own alone goes to a serial step after the join; and an unsplit phase is planned and persisted with no units. -->

- **Phase 0 — <lowest-risk first>.** <scope — what this phase builds> *Discharges:* <the requirement rows and locked decisions this phase closes>.
- **Phase 1 — …**
- **Assets the build needs that code cannot author (flag them):** <what must be created by hand or by a tool outside this build>.

## Risks, tradeoffs & open questions

<!-- The fragile parts, load-bearing assumptions, deferred work, drift watch. Honest beats tidy. -->

## As-built deltas

<!-- Fill AFTER implementing: where the build diverged from the plan and the decisions above. Empty while still planning. The charter below is canon; keep it in the doc when you delete the rest of this comment.

**The `## As-built deltas` charter.** This section is about the plan, and the plan lives in the plan files this document links: four kinds of entry are **admitted** here, two are **amended into `## Decision register`** in place with a dated line instead, a fact the landed tree has made false in an always-current section is **corrected in place** where it stands, and every other kind is **routed** to a home that already receives it — the session's dated note, `memory/<YYYY-MM-DD>.md`; the cross-session state the `board` binding names, `memory/STATUS.md` where none is bound; or the lessons file, `memory/notes-for-future-features.md`. No new home is built for what is routed out.

**Admitted**, each a statement about the plan: (1) divergence between the persisted plan and the tree as built, with the ruling that settled it; (2) "built as planned" confirmations, which are a real result; (3) citation re-pointing, plan-time line numbers read against the landed tree; (4) post-G2 addenda from unrelated later work, carrying their date.

**Amended into `## Decision register`**, in place and carrying the amendment's date, in the form `**Amended YYYY-MM-DD:**` appended to the decision it extends: a ruling that extends a decision this document already carries, and a trap or do-not-undo notice for a later maintainer. Both are design rationale, and the register is the one section carried forward into the product surface the `product-docs` binding names; filed here instead, a decision would reach that surface without the category later added to it.

**Corrected in place**, by the reconcile pass: a fact the landed tree has made false in an always-current section — `What this implements`, `Context & scope`, `Components & responsibilities`, `Data contract & runtime flow`, `Non-functional realization`, `Risks, tradeoffs & open questions` — is corrected where it stands, and each correction is logged in that phase's entry here, with what the passage said and what it says now, as citation re-pointing is. A change of decision is never a correction: it goes to a dated `## Decision register` amendment. Dated passages — plan files, the entries here, `**Amended YYYY-MM-DD:**` clauses — stay records and are not corrected.

**Routed out**, each to a home that already receives it: gate evidence (commit shas, test counts, exit codes, live-run transcripts), review-cycle records, and authorship or provenance confessions about the document itself go to the session's dated note, `memory/<YYYY-MM-DD>.md`; deferrals and named limitations go to the cross-session state the `board` binding names — `memory/STATUS.md` under *Deferred*, one line each, where none is bound; process and methodology lessons aimed at future features go to `memory/notes-for-future-features.md`.

**Frontmatter `updated:`** — the reconcile pass also moves the doc's frontmatter `updated:` to its own date: bookkeeping, never logged as a correction, never a change of decision.

**A persisted phase plan is never edited** — it is the record of what its planner knew on its own date, which is why re-pointing its citations is an admitted kind here rather than a correction there.
-->

## Related

<!-- Dense list of links: the requirements doc, the design docs and decision records, the code and product pages this couples to — in the link form the project's existing process docs use. -->
