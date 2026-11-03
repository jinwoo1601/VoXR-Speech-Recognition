---
type: architecture
feature: <kebab-name>
topic: <the design topic this feature belongs to>
status: draft   # draft while building; accepted at G2 by g2-accept RECORD, and still living after
updated: <YYYY-MM-DD>
sources:
  - <the feature's requirements doc — for a lite feature, the design's backlog>
  - <the locked design doc(s) and decision record(s) this realizes>
  - "<the code this builds or touches>"
---

# <Feature Name> — Architecture

<!-- This is the HOW for the feature, and the feature's working record. The architecture doc is the feature's working record — the living document that holds the how, the build plan, and what the build taught. It is not a draft of the product page: the product docs are authored after G2 from live source. One obligation crosses over — `## Decision register` is the design rationale a reader cannot recover from the code, and it is what the product docs carry forward. Document load-bearing decisions and WHY (with the rejected alternative); cite real paths and symbols so drift is detectable; reconcile to AS-BUILT before G2, under the charter that the `**Charter:**` line in `## As-built deltas` points to. Keep each paragraph and list item on one logical line — no hard-wrapped prose; match the link and date form the project's existing process docs use. Delete these guidance comments as you fill each section; the `**Charter:**` line is not a guidance comment and stays. -->

<!-- OPENING (one paragraph): what this feature builds, the requirements it realizes, and the one governing architectural driver — often a non-functional row the platform makes load-bearing — that shapes the design below. -->

<!-- LITE FEATURE: A lite doc is `## User story`, `## Context & scope`, `## What this implements`, `## Decision register`, `## Build plan` (its rows and plan links), `## As-built deltas` and `## Related`, in that order, and no other section. `## User story` and `## Context & scope` stay first, as G0 wrote them, and `## Context & scope` is not repeated below (`.claude/references/core/feature-lite.md` `## The three placements`); `## What this implements` and `## Decision register` cite the locked design and hold only what it does not state; a lite doc's non-functionals and risks stay in the locked design it cites, since lite means the design fixed every choice, and `## As-built deltas` records drift; where a comment below names requirement rows, the backlog row's criteria stand (`.claude/references/core/feature-lite.md` `## The acceptance source`). -->

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

## Seams

<!-- Only the seams between components — what crosses each and the runtime flow across them, each end cited by path and symbol — never per function, that detail being the plans'. Treat as drift-prone. -->

- **<Component> → <component>.** <what crosses, and when>. Cite: `<path>` (`Symbol`) at each end.

**Flow:** <one operation traced across the seams, end to end>.

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

<!-- Fill AFTER implementing: where the build diverged from the plan and the decisions above. Empty while still planning. The line below stays. -->

**Charter:** this section is kept under `.claude/skills/architecture-doc/references/as-built-charter.md`, which says what it admits, what goes to `## Decision register` or is corrected in place, and what is routed out.

## Related

<!-- Dense list of links: the requirements doc, the design docs and decision records, the code and product pages this couples to — in the link form the project's existing process docs use. -->
