---
name: phase-pickup
description: "Plan one implementation phase from recon, then persist and validate its plan as its own file beside the architecture doc. Use on 'pick up phase N', 'start/resume the next phase', or on a branch at plan/implement stage."
kind: orchestration
agents: [architect, code-mapper, doc-extractor, plan-validator]
stops: [plan-mode approval where the plan changes scope or splits units or departs from the build plan, a canon-conflict or scope-changing validator defect]
status: active
bindings: [process-docs, vc, board, layout, session-launch]
removal-date: null
---

# phase-pickup

## What this is

**A phase pickup is orientation + reconnaissance + a plan that survives the session.** It turns "pick up phase N" into: verified current state, agent-gathered ground truth, a plan persisted into canon, and a validated go signal.

> Plan ONE phase. Recon by agents, judgment on the main thread, and no plan exists until it is persisted in its own file beside the architecture doc.

## Bindings

Each key is looked up as `.claude/references/core/delegation-contract.md` `## Bindings` says: `.claude/bindings/<pack>.md` first, the project `CLAUDE.md` *Bindings* section as the pre-scaffold fallback.

- `process-docs` (required) — where the feature's requirements and architecture docs live, which the prerequisites confirm, and the path a phase's plan file is written at, which PERSIST fills. Absent → stop and say so before any recon is dispatched; the plan has no home, and an unpersisted plan does not exist. For a lite feature it also places the backlog whose row is read (`.claude/references/core/feature-lite.md` `## The acceptance source`).
- `vc` (required) — the working-tree read and the current branch at ORIENT and at the branch prerequisite (*Base-revision reads*), and the check-in at RECORD & COMMIT (*Check-in procedure*). Absent → stop and report; never improvise a version-control idiom.
- `board` (optional) — where cross-session state lives, the row ORIENT cross-checks against local state and RECORD & COMMIT advances. Absent → `memory/STATUS.md` is the state; orient from what the project binds instead, and skip the row advance.
- `layout` (optional) — the *Source*, *Tests* and *Generated* lines RECON quotes into the `code-mapper` dispatch, by which it classifies each site. Absent, empty, `TODO` or `none` → the dispatch says so, and `code-mapper` classifies each site by its own reading; never a stop.
- `session-launch` (optional) — the *Context bound* and the *Context read*, which the rhythm reads at each step's end, as `handoff` `## When to hand off` says. Absent, either slot missing or `none`, or the read failed → the phase-boundary handoff only, the slot named once; never a stop.
- Agents — `architect` missing → stop before PERSIST and say so. `code-mapper` or `doc-extractor` missing → run RECON with what the remaining agent returns and record the gap in the persisted plan; both missing → stop, because the plan would rest on no verified ground truth. `plan-validator` missing → plan mode always, then persist and record in the plan that it went unvalidated.

## When it applies — and prerequisites

**Trigger:** starting or resuming an implementation phase on a feature branch.

The steps below branch on the five keys `## Bindings` above names — the process-doc layout, version control, whether a workflow board exists, the project's source layout, and the session's context read — and each carries its degradation path there.

Confirm before proceeding — if any item fails, stop and say which skill or step is actually needed:
- The feature branch exists and the workspace is on it (never plan on main).
- The feature's requirements doc and architecture doc exist (else: `feature-requirement-doc` / `architecture-doc` first). The skill applies the test of `.claude/references/core/feature-lite.md` `## The mark` here; a lite feature needs its architecture doc alone.
- The phase is identifiable from the architecture doc's Build plan (or the board row, where a board is bound).

A phase whose plan file exists at the path the `process-docs` binding names is planned: a resumed pickup goes to VALIDATE, or to `implement` once the plan was validated — known by the `plan-validator` report `.scratch/plan-validator-<feature>-phase-<id>-report.md`, filed beside VALIDATE's brief; without one, VALIDATE.

## Workflow

1. **ORIENT** — read the working tree and the current branch with the reads the `vc` binding's *Base-revision reads* slot names. If the project binds a workflow board, read it and cross-check the row against local state — if the row's stage, dates, or commit refs don't match (e.g. stage already `implement` with no matching workspace evidence, or notes newer than this session's handoff), state the mismatch and the reading this pickup follows — the local branch and files — go on, and list the mismatch at the next gate. No board bound → orient from the sources the project binds instead (e.g. open PRs/issues). Where this session ran the previous phase's `implement`, this pickup is a phase boundary: invoke `handoff`, its objective this phase's pickup, and take no further step, as `handoff` `## When to hand off` says.
2. **RECON** — spawn `code-mapper` (the code sites this phase touches, with any line refs from the docs to re-verify, and the `layout` binding's lines quoted verbatim, or a line saying it is absent) and `doc-extractor` (the requirements, ADR constraints, and architecture decisions bearing on this phase, as a numbered question list) — both in a single message, all paths absolute. Trust their reports; do not re-read what they've mapped. For a lite feature the backlog row and the locked design stand for the requirements (`.claude/references/core/feature-lite.md` `## The acceptance source`).
3. **PLAN** — draft the plan for the ONE phase from the two recon inventories. Scope is one phase: if the human asks for two, plan the first and note the second — never a combined plan. Then compare the draft with the phase's row in the architecture doc's `## Build plan`: files or requirements beyond the row's scope; a split into units; any other departure — order, a decision, work the row does not carry. Any one, or `plan-validator` missing from the composed set → enter plan mode, whose approval is the stop; none → state the plan in one message and go to PERSIST. The plan may propose splitting the phase into units, weighing the plan's file sets and the cost of cutting a tree per unit against wall-clock time — a small or tightly coupled phase is best serial, and broad disjoint work favours the split; the human rules the split with the plan, in plan mode. Under a split, each unit owns its write set; files that change in pairs or are generated, and a file another unit needs in order to stay green, count with the unit that causes them; and a file no unit can own alone goes to a serial step after the join. An unsplit phase is planned as above, with no units.
4. **PERSIST** — after the plan is approved or stated, file `.scratch/architect-<feature>-phase-<id>-brief.md` on the field list of `.claude/references/core/delegation-contract.md` — the phase and its scope, the requirements and acceptance criteria it discharges, the architecture doc and the requirements doc by path, the two recon reports attached by path, the plan file's path — the pattern the `process-docs` binding names, `<feature>` and `<id>` filled — and what is out of scope; where PLAN's split was ruled, also the units in join order, each unit's files, and the `serial` files, none of which an unsplit brief carries; and it asks the plan to close with a `Runtime claims` list — each step whose correctness rests on what something does when run — or `none` — and dispatch `architect` in its plan mode through the Agent tool without a `name` to write the plan file at that path and its link line in the architecture doc's `## Build plan`. The plan does not exist until it is persisted — plan-mode output evaporates at session end. For a lite feature the brief says `lite`, gives the backlog's path, quotes the row and cites the criteria the phase discharges as A1…An (`.claude/references/core/feature-lite.md` `## The acceptance source`).
5. **VALIDATE** — pick the depth: `narrow` where the plan's files table has at most 3 rows, or the plan only transcribes text it dictates; otherwise `full`. File `.scratch/plan-validator-<feature>-phase-<id>-brief.md` on the field list of `.claude/references/core/delegation-contract.md` — the plan file by its path, the depth and the canon set (requirements, architecture doc, ADRs), and nothing of this session's reasoning — and spawn `plan-validator` on that brief. The plan's runtime claims are not the validator's to confirm: they go to implement's first gate. For a lite feature the canon set is the backlog row, the locked design, the architecture doc and the ADRs, and the brief names the feature lite (`.claude/references/core/feature-lite.md` `## The acceptance source`).
6. **RECONCILE** — rule on the main thread which findings are real defects — a runtime claim the validator lists that the plan's list omits among them — then apply them by a second `architect` dispatch: `.scratch/architect-<feature>-phase-<id>-fix-brief.md` in PERSIST's own form, carrying the findings ruled real and the plan file by its path and nothing of the main thread's reasoning, dispatched through the Agent tool without a `name`. If a defect changes the plan's scope, re-gate with the human. A canon-conflict finding stops the line: per the workflow, reopen the design question with the human — do not patch around it. For a lite feature an `unfixed-choice` finding ruled real by the test of `.claude/references/core/feature-lite.md` `## The escape` is the return `.claude/references/core/feature-lite.md` `## The return to full` describes, not a fix pass.
7. **RECORD & COMMIT** — if a board is bound, advance its row (implement stage, date). Then check in per the `vc` binding's *Check-in procedure* slot. If the binding marks the process-doc area untracked/local-only, the persisted file is the record — nothing to commit.

**The rhythm.** At the end of each step above the session applies `handoff` `## When to hand off`: it runs the *Context read*, and past the *Context bound* it invokes `handoff`, its objective the next step, instead of taking that step. A handoff taken at the end of PLAN carries the plan, as stated or as approved, whole in the brief, so the next session takes PERSIST from it. After step 7 there is no next step, and the objective is `implement` for the phase, as `handoff` `## When to hand off` says of a skill's last step.

Recon, the plan's authorship and its validation are the agents' work; the planning judgment stays on the main thread, and the human's plan-mode approval only where PLAN's comparison calls for it.
