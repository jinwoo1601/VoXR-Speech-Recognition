---
name: architecture-doc
description: "Author, revise or reconcile to as-built a feature's architecture doc: components, data contracts, runtime flow. Use after the requirements doc and before implementation, or to reconcile it before G2."
kind: orchestration
agents: [architect, architect-reconcile]
stops: [the ruling at PLAN on the phase breakdown, a contradiction with the requirements or the locked design]
status: active
bindings: [process-docs, vc, board, product-docs]
removal-date: null
---

# architecture-doc

## What this is

An architecture doc specifies **how one feature is built** — the components, the data contracts, the runtime flow, the non-functional realization — and **holds the build plan's breakdown and the links to its plan files**. It answers the one question the design and requirements docs refuse to; both of them say *names a class → that is the architecture doc*. It sits on the feature branch, written *after* the requirements doc, and feeds **G2**. A lite feature has no requirements doc; its doc is written beneath its two G0 sections (`.claude/references/core/feature-lite.md` `## The three placements`).

It carries two mandates a generic architecture doc does not, and they shape everything:

> The architecture doc is the feature's working record — the living document that holds the how, the build plan, and what the build taught. It is not a draft of the product page: the product docs are authored after G2 from live source. One obligation crosses over — `## Decision register` is the design rationale a reader cannot recover from the code, and it is what the product docs carry forward. **And the build plan is anchored here:** plan-mode output evaporates at session end, so each phase's implementation plan is persisted as a plan file this doc links.

The test of every line: *would a maintainer who did not sit in this feature need this to build, verify or reopen what was built?* So document the **load-bearing decisions** — the ones costly to reverse — with their rejected alternatives, cite real paths and symbols so drift is detectable, and reconcile the doc to **as-built** before G2. Trivia the code already states, exact signatures that churn, throwaway scaffolding: leave them out. The code is the source of truth; this doc records how it is organized and *why*.

## Bindings

Each key is looked up as `.claude/references/core/delegation-contract.md` `## Bindings` says: `.claude/bindings/<pack>.md` first, the project `CLAUDE.md` *Bindings* section as the pre-scaffold fallback.

- `process-docs` (required) — where feature docs live and what they are called, and therefore where this feature's architecture doc sits beside its requirements doc, and the path a phase's plan file is written at. Absent → stop and say so before any brief is filed. For a lite feature it also places the backlog whose row is read (`.claude/references/core/feature-lite.md` `## The acceptance source`).
- `vc` (required) — the branch procedure (the feature branch this work runs on, never the main branch) and the check-in procedure for the file drafted here. Absent → stop and say so.
- `board` (optional) — where cross-session state lives beyond `memory/STATUS.md`, and therefore where this feature's row is updated. Absent → `memory/STATUS.md` is the state; orient from what the project binds.
- `product-docs` (optional) — the product surface that takes this feature's `## Decision register` at G2, when the agent that reconciles the product docs writes them from the built tree; this doc is not drafted toward that surface. Absent → the register is written the same way regardless, since it records why each costly-to-reverse choice was made and which alternative it beat, and where it lands is settled with the human at G2.
- Agents — `architect` missing → stop before DRAFT and say so. `architect-reconcile` missing → stop before RECONCILE, or before a record PLAN needs, and say so.

## When it applies — and prerequisites

Trigger: a feature's architecture and implementation design begins, after its requirements. For a lite feature the requirements and the requirements doc named in this section and the next are its backlog row and the locked design, an error in either being a design error (`.claude/references/core/feature-lite.md` `## The acceptance source`). Before drafting, confirm:

- **You are on the feature branch** — the same branch as the requirements doc, created per the `vc` binding's branch procedure. Not on the main branch.
- **The requirements doc exists.** Architecture *realizes* the requirements: its functional rows and its non-functional rows are the inputs here. If they are missing, write them first — that is the `feature-requirement-doc` step. A lite feature's inputs are its backlog row and its doc's two G0 sections (`.claude/references/core/feature-lite.md` `## The acceptance source`).
- **This is still the docs phase, but implementation is the next step.** Author the architecture and the plan *before* coding; do not jump ahead and scaffold mid-authoring. Unlike design and requirements, where code is far off, here you build immediately after — so the doc is written as *intended* architecture, then reconciled to **as-built** before G2.
- **The human owns G2 and the hard build calls.** The PM gathers, briefs, holds the bar and rules the plan with the human; it does not cross G2 — that ruling is core's `g2-accept`, and nothing here stops at G2.
- **A locked decision is immutable.** If the architecture needs something the requirements or the locked design behind them contradict, **stop**: a requirements error is a requirements-doc fix on this branch; a design error means reopening that design on a design branch, amending it and re-locking at G1. Never silently paper over the contradiction in the architecture doc.

## What the architecture derives from

1. **The requirements doc** — the buildable contract this realizes. Every component traces to a requirement it serves, and the non-functional rows this project's platform makes load-bearing are architectural drivers, not footnotes. For a lite feature it is the backlog row's criteria, A1…An (`.claude/references/core/feature-lite.md` `## The acceptance source`).
2. **The locked design docs and decision records** — the model and its rationale. The architecture is how that model meets the platform's reality.
3. **The existing codebase** — the *primary source of truth*. Read the code you will touch and the patterns it already uses; match those patterns and cite them, rather than reinventing beside them.

Read those fully before briefing. A component that realizes no requirement is either scope creep or a requirement nobody wrote down — surface it, do not paper over it.

## Workflow

1. **ORIENT** — confirm the feature, the branch and that the requirements doc exists: the branch per the `vc` binding's branch procedure, the cross-session state per the `board` binding. Nothing is drafted before the feature and its requirements are in hand. The skill applies the test of `.claude/references/core/feature-lite.md` `## The mark` here; for a lite feature this step and GATHER take the backlog row and the doc's two G0 sections for the requirements doc.
2. **GATHER** — read the requirements doc in full, the locked design docs and decision records it realizes, and the live code the build will touch, including the patterns that code already uses. Attach each by path in the next step's brief; an unattached input does not exist for the agent.
3. **DRAFT** — file `.scratch/architect-<feature>-brief.md` on the common field list of `.claude/references/core/delegation-contract.md`; core ships no architect-brief template, so the brief is written to that field list. It carries the requirements doc and its parents by path, the live code GATHER read, attached by path, `assets/architecture-template.md` as the skeleton, the target directory and file name per the `process-docs` binding, and the product surface the `product-docs` binding names — carried as the destination this feature's `## Decision register` is owed to after G2, not as a shape to draft toward. Dispatch `architect` in its author mode through the Agent tool without a `name`. For a lite feature the brief names the track, gives the backlog's path, quotes the row, numbers its criteria A1…An (`.claude/references/core/feature-lite.md` `## The acceptance source`), and attaches the existing doc, whose two G0 sections are kept (`.claude/references/core/feature-lite.md` `## The three placements`).
4. **BAR** — judge the returned draft against `## The quality bar` below. For the hard parts — holding the altitude, realizing the non-functionals, writing so a maintainer who did not sit in the feature can work from it — `references/quality-bar.md` carries the worked examples, the altitude stack and what crosses over at G2. A miss is a second brief to `architect`, never a PM edit. Where the report raises a contradiction with the requirements or the locked design instead of a defect in the draft, the second `stops:` entry attaches here and the human rules it before anything is re-briefed. For a lite feature a choice in the report that the locked design does not fix is put to the test of `.claude/references/core/feature-lite.md` `## The escape` and is not a contradiction put to the human.
5. **PLAN** — the hard stop the first `stops:` entry attaches to: `architect` returns the phase breakdown, and the human rules its order, its risk ordering and its count before the doc is filed and before any phase starts. The breakdown is written into `## Build plan` as one row per phase carrying three fields — the phase id, its scope, and the requirements it discharges — ordered lowest-risk-first, each phase green by the `verification` binding's commands and ideally independently shippable. A ruling that changes the returned breakdown is written into `## Build plan` by `architect-reconcile` in its record mode, from a brief quoting the ruling, dispatched through the Agent tool without a `name`. The per-phase plans themselves are written by `architect`, through the phase pickup that runs when each phase begins, each as its own plan file at the path the `process-docs` binding names, with one link line to it in `## Build plan`; **this skill writes none of them.** The same `stops:` entry attaches again wherever the returned plan raises a contradiction with locked canon. For a lite feature the third field is the criteria it discharges, A1…An (`.claude/references/core/feature-lite.md` `## The acceptance source`).
6. **RECONCILE** — after building, dispatch `architect-reconcile` through the Agent tool without a `name`, in its reconcile mode, to write the `## As-built deltas` pass: where the build diverged from the plan and the decisions above, under the charter this step states and the doc's own guidance carries. **The `## As-built deltas` charter.** This section is about the plan, and the plan lives in the plan files this document links: four kinds of entry are **admitted** here, two are **amended into `## Decision register`** in place with a dated line instead, a fact the landed tree has made false in an always-current section is **corrected in place** where it stands, and every other kind is **routed** to a home that already receives it — the session's dated note, `memory/<YYYY-MM-DD>.md`; the cross-session state the `board` binding names, `memory/STATUS.md` where none is bound; or the lessons file, `memory/notes-for-future-features.md`. No new home is built for what is routed out. A correction in place is made only in the six always-current sections — `What this implements`, `Context & scope`, `Components & responsibilities`, `Data contract & runtime flow`, `Non-functional realization`, `Risks, tradeoffs & open questions` — and each is logged in that phase's as-built entry with what the passage said and what it says now; a change of decision is never a correction and goes to a dated `## Decision register` amendment, and dated passages — plan files, as-built entries, `**Amended YYYY-MM-DD:**` clauses — stay records and are never corrected. Gate evidence, review-cycle records, and authorship or provenance confessions about the document itself are routed to the dated note; deferrals and named limitations to the `board` binding's state; process and methodology lessons aimed at future features to `memory/notes-for-future-features.md`. Separately from the charter's kinds, the reconcile also moves the doc's frontmatter `updated:` to its own date — bookkeeping, never logged as a correction, never a change of decision. The doc that reaches G2 must describe what was *actually built*.
7. **FILE** — the doc and its bookkeeping per `## Filing and bookkeeping`.

**No SETTLE step, deliberately** — unlike its sibling: `architect` is contracted to produce the plan's parts itself, deriving each decision from a requirement, a locked design decision or a real constraint, so settling that content with the human in advance would pre-empt the work the agent is dispatched to do.

## The quality bar

Every architecture doc must clear these. (Expanded, with good/bad examples and what crosses over at G2: `references/quality-bar.md`.)

1. **Written as the feature's working record.** The architecture doc is the feature's working record — the living document that holds the how, the build plan, and what the build taught. It is not a draft of the product page: the product docs are authored after G2 from live source. One obligation crosses over — `## Decision register` is the design rationale a reader cannot recover from the code, and it is what the product docs carry forward. So shape it for the maintainer who will build, verify or reopen this feature — context, components, contracts, flow, the plan, and what the build taught — rather than for a reader of the product page.
2. **Load-bearing decisions, with the rejected alternative — recorded inline.** Spend words on the choices costly to reverse (state ownership, the seams, event versus polled state, execution-order placement, module boundaries) and on *why this over the obvious alternative*. Record them inline in a decision register — **not** as separate decision-record files; a feature has just its requirements doc and this one. A lite feature has this one alone (`.claude/references/core/feature-lite.md` `## The acceptance source`).
3. **Right altitude — context → components → only load-bearing code detail.** Do not transcribe the code method by method; it rots and duplicates the source of truth. Do not stay so vague that it constrains nothing. Pick the level and hold it.
4. **Contracts and flow are the spine.** The data crossing the seams (events, schemas, the per-step structures, serialized and persisted state) and the runtime flow (per request, per step, lifecycle, state machine). A reader should trace an operation end to end without opening the code.
5. **Non-functional realization is first-class — on some platforms it is the architecture.** Show how the budget, the allocation pattern, the behaviour under degraded dependencies and the structural laws this project's platform imposes are *met* structurally, not restated. A clean design that blows the budget every step is *wrong* here.
6. **Cite real paths, symbols and changesets — make drift detectable.** Code is primary and it moves; cite the file and the symbol, with a line number where it helps. Document slow-drifting rationale heavily and fast-drifting signatures lightly.
7. **The build plan is broken down and linked here — sequenced, risk-first, verifiable.** Persist the plan (it is otherwise lost): the breakdown rows here, one link line per phase, each phase's plan in its own file; ordered phases lowest-risk-first, each one green by the `verification` binding's commands, run by `compile-check`; the riskiest or most visual part prototyped first, outside the shipped tree; and the assets the build needs that code cannot author flagged, because nobody finds them mid-phase.
8. **Honest about risk, tradeoff and as-built drift.** Name the fragile parts, the load-bearing assumptions and the deferred work — and after building, record where the build diverged, in `## As-built deltas`, under the charter that section's guidance carries. Honest beats tidy: this doc must describe what was actually built.

**Smell tests:** transcribes the code method by method → it is a duplicate; document the *why* and the shape. Lists components with no rationale → nothing to evaluate or reopen. No non-functional realization → the load-bearing half of the architecture is missing. A line no maintainer would need in order to build, verify or reopen what was built → it fails the anchor; cut it. Uncited or vague → drift-blind. A plan with no risk ordering → sequence it lowest-risk-first.

## Filing and bookkeeping

- **Location:** where the `process-docs` binding says feature docs live, beside this feature's requirements doc; a project's first feature creates that directory. New files are added to version control per the `vc` binding's check-in procedure. A lite feature's doc is there from G0, with no requirements doc beside it (`.claude/references/core/feature-lite.md` `## The three placements`).
- **Frontmatter:**
  ```yaml
  ---
  type: architecture
  feature: <kebab-name>
  topic: <the design topic this feature belongs to>
  status: draft         # draft while building; reconciled to as-built before G2
  updated: YYYY-MM-DD   # absolute date, never "today"
  sources:
    - <the feature's requirements doc — for a lite feature, the design's backlog>
    - <the locked design doc(s) and decision record(s) it realizes>
    - <the code this builds or touches>
  ---
  ```
- **Prose and linking convention:** keep each paragraph and list item on one logical line — no hard-wrapped prose. For link and date form, match what the project's existing process docs already do; where there are none, link by relative path and write dates absolute (`YYYY-MM-DD`).
- **Update the cross-session state:** add or update the feature's row where the `board` binding says — track `feature`, stage `architecture`, advancing as the plan is persisted and the phases run. No board bound → `memory/STATUS.md` is the state.
- **Do not touch the product surface.** A project's product docs are written only after G2 acceptance, by the agent that reconciles them; writing there now is a layer violation. The architecture doc is a process doc until G2, and its only bookkeeping is the row above — the one thing this doc owes the surface the `product-docs` binding names is its `## Decision register`, carried forward at that separate post-G2 step rather than distilled from here. Cross-*linking* to a product page is fine; creating or editing one from here is not.
- **Check in only when the human asks**, per the `vc` binding's check-in procedure. Concurrent sessions edit the same cross-session state — re-read it before appending.
