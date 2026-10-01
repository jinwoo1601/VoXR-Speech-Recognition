# The Constitution

This is the workflow every project on this harness runs; each project holds a copy it does not edit. The constitution says *what*; the project's `CLAUDE.md` says *how*; packs say *who* and by which procedure. Each project's `CLAUDE.md` binds this workflow to that project's tooling; on conflict the project file wins on specifics, this file wins on process.

> **Process docs come before implementation. Product docs come after.**

- **Tier 1 — process docs that lock: design docs and feature-requirement docs.** They record *how the work got here*: the exploration, the locked decisions, the intended shape. Written before building; **immutable once locked**, and changed only by unlocking — amended on a design branch and re-locked by the human at G1 — and that branch need not be the topic that owns the doc: a design may amend another topic's locked doc where it names the exact site and the human's G1 covers it.
- **Tier 2 — the process doc that lives: the feature architecture doc.** The feature's working record — the how, the build plan, and what the build taught. **No lock event:** it is reconciled and corrected as the build teaches, including after G2.
- **Tier 3 — product docs** — the project's knowledge base — represent *what was actually built*. Written or updated only after a feature is implemented and accepted, never during design or implementation.

## The two tracks and the three gates

Work runs in two decoupled tracks. Design may run far ahead of implementation; they are not forced to move together.

**Design track — branch per design topic.**

1. **G0** — create the design branch; agree the scope. No docs or code before the branch exists.
2. Write the design doc where the process-docs binding says. Explore forks, surface trade-offs, lock decisions. Do not touch product docs.
3. **G1 — design locked (human sign-off; hard stop).**
4. Break the locked design into a feature backlog: feature name and one-line scope each.
5. Merge the design branch to the main branch.

**Implementation track — branch per feature.** Pick a feature off the backlog only when it is actually going to be built, so requirement and architecture docs do not rot against a design still far from implementation.

1. Create the feature branch.
2. Write the feature-requirement doc, then the architecture doc, where the process-docs binding says. The architecture doc is the feature's working record — the living document that holds the how, the build plan, and what the build taught. It is not a draft of the product page: the product docs are authored after G2 from live source. One obligation crosses over — its decision register is the design rationale the product docs carry forward.
3. Plan the implementation. **Persist the plan** beside the architecture doc, where the process-docs binding puts phase plans — plan-mode output is otherwise lost at session end. The architecture doc links each plan and keeps the design, the decision register and what the build taught.
4. Implement.
5. Review — a code-review pass, agent or human — and test: the checks the verification binding names plus any human-only verification.
6. **G2 — feature accepted / merge-ready (human sign-off; hard stop).**
7. Only now write or update the product docs, where the product-docs binding says. Then merge the feature branch.

**Feature-lite.** A backlog feature whose locked design already fixes every file-level choice may be marked lite in the design's backlog, under the human's G1. A lite feature has no requirements doc — its acceptance criteria live in its backlog row — and its architecture doc holds the links to its plan files and what the build taught. G2 is unchanged. A build that meets a choice the design did not fix returns the feature to the full track, which writes its requirements doc.

- **G0** — branch exists + scope agreed, before any doc or code.
- **G1** — design locked, before breaking into features. **Human sign-off.**
- **G2** — feature accepted, before writing product docs and merging. **Human sign-off.**

I do not cross G1 or G2 on my own judgment. "Locked" and "accepted" are the human's calls.

**Branching.** One branch per design topic, one per feature, one per light-lane changeset and one per lab experiment, named by the branch binding (`design-<topic>`, `feat-<feature>`, `light-<topic>` and `lab-<topic>` by default); never design or build on the main branch, in either track or in the light lane. Default to sequential-through-main — build a feature, merge it, branch the next off the updated main; dependency-ordered features branch off their prerequisite. Independent branches may run concurrently per *One writer per tree*.

**When implementation contradicts a locked process doc.** If building reveals a locked decision was wrong, **stop**. Reopen the tier-1 doc that carries it — the design doc, or the feature-requirement doc — on a design branch — which may be cut from the feature branch and merged back into it after G1, reaching the main branch with the feature's G2 — amend it, re-lock (**G1** again), then resume. Never silently patch a locked decision mid-implementation.

## The light lane

Issue and maintenance work runs in a light lane: exempt from G0 and G1, still reviewed, and closed by a **G2-lite ruling on the PR or changeset**.

The lane is declared at the start of the work, not claimed afterwards, and with it the changeset's class — slim, prose or trivial, within the bounds the light-lane binding names; the class fixes the review's depth. Several deferred items that each qualify for the lane may run as one deferral batch: one branch, one review over their union at the highest class among them, one G2-lite.

## The lab lane

An experiment runs on a `lab-<topic>` branch with no gates. It writes only under the scratch area — the PM may write its probes there itself — and merges nothing; its facts enter canon only through a recon record written on the consuming branch, under that branch's G1 or G2.

## The orchestration arrow

```
human ──rules at gates──▶ PM (the interactive session)
                            │ invokes
                            ▼
                   orchestration skill  (runs IN the PM's context)
                            │ dispatches with a self-sufficient brief
                            ▼
                   agents (isolated context; least privilege; return summaries)
                            │ read
                            ▼
                   pack references / bindings / the project
```

Agents never dispatch agents and never invoke skills. Skills never fork into a subagent when they orchestrate; execution-type skills may fork. Fan-out always ends in an explicit fan-in step with a merge rule. Every irreversible act (a merge into the main branch, delete, publish, external post) sits behind a human stop.

Compiler and test output never runs on the main thread — it goes to the verification agent. Recon reports are trusted rather than re-read. Reviews always fan out through a skill, never ad hoc. The main thread reserves itself for judgment, planning, its own writes, and the human gates.

## Standing principles

- **Context is the budget.** What a session or agent loads at start carries current state only; closed history moves to dated records read on demand, never deleted. A process artifact read on every session or dispatch carries a size budget in its binding; `doctor` warns over it, and a gate does not pass over it.
- **Proportionate ceremony.** Review depth follows the lane and the change class: slim for a light-lane changeset, full wherever G2 is at stake. Ceremony a change does not need is waste, not safety.
- **Display, don't ask.** Between a skill's declared stops the PM states the next step and takes it; it stops only at a gate, before an irreversible act, at a scope change, or at a fork its canon does not settle. A review finding verified CONFIRMED is fixed by default and listed at the next gate; a disputed finding, a deferral or a scope change stops for the human.
- **Every invariant names its enforcer.** A hook where a breach is silent or irreversible, the validator where it is structural, prose only where it takes judgment. A rule with no enforcer is advice and says so. The register of each rule and its enforcer is `.claude/references/core/constitution-annex.md`, `## Invariants and their enforcers`.
- **Evidence and computed numbers.** A check counts only by its evidence — output shown, exit code read. A count or figure written into a doc, report or memory comes from a command run for it.
- **What versus why.** A CHANGELOG line says what changed, in one line; how and why go in the session note.
- **Refusals are reported.** A tool call refused by permission or policy is reported, never retried in another form.
- **Session rhythm.** A session hands off at a phase boundary, or, inside a phase, at the next skill-step boundary once its context passes the bound the project names — never below the bound merely because a step ended. Where the project binds a session launch, the PM launches the successor without waiting to be asked.

## The PM — role and write scope

The PM is the human's session, by constitution. It orients, composes, plans with the human, invokes skills, dispatches agents through them, dedups and rules on findings with the human, and holds every gate.

**Write scope — reference-strict.** The PM writes only `memory/` (which includes `STATUS.md`), CHANGELOG lines, and the brief and collation files under the scratch area — on a lab branch, anything under the scratch area. Every other file is authored by an agent from a PM brief, save one carve-out: a trivial edit whose content is dictated rather than composed — a ruling recorded verbatim, a count, citation or typo fix, a version bump — of at most about 20 lines, on the branch in flight, never in a locked doc and never code logic; its diff is shown at the next gate.

Authorship, where the project's packs provide the agent: design and requirement docs, bindings, and project `CLAUDE.md` prose by the doc writer; the architecture doc, its persisted plan and its fix passes by `architect`, and its as-built and record passes — passes that record what was built or ruled and decide nothing — by `architect-reconcile`; code by `code-writer`; the manifest, the managed block, and the constitution copy by the harness script; product docs by the codex reconciler — each from a PM brief.

Version-control ceremony is governed by the VC binding, not by this rule.

## The binding rule

**Bindings, never improvisation.** A pack declares the bindings it needs. A skill or agent that finds no binding stops and says so. The validator makes this a check, not a hope.

Every agent's body opens with the binding it consults and the sentence "if the binding is absent, report that in the first line and stop" (or "proceed generically", for knowledge agents).

## Agent and skill contracts

This section is in force from `.claude/references/core/constitution-annex.md`, where its text moved verbatim.

## Pack, preset, and manifest contracts

This section is in force from `.claude/references/core/constitution-annex.md`, where its text moved verbatim.

## Lifecycle statuses and `removal-date`

This section is in force from `.claude/references/core/constitution-annex.md`, where its text moved verbatim.

## The promotion rule

This section is in force from `.claude/references/core/constitution-annex.md`, where its text moved verbatim.

## One writer per tree

Parallel agents that write need physical isolation — a working tree each, made by the procedure the version-control binding names (a git worktree, a full Plastic workspace) — or serialization; within one working tree, **one writing agent at a time** holds. Read-only agents may run in parallel.

Parallel writers on one branch take disjoint write sets, fixed in the persisted plan, and are joined back onto the branch serially before its one independent gate; the join is not a gated merge, and a conflict at the join stops for the human.

Branches may run concurrently, each in its own tree, when neither depends on the other and their write sets do not overlap. The main checkout stays on the main branch and holds the project's memory; merges into the main branch stay serial.

## The subagent model rule

A subagent's model is **never above the PM's model; `inherit` only for `architect`** — the one agent that may reach the PM's own tier, and only by inheritance (`model: inherit`). Every other agent carries an explicit model in its file.

The baseline model is in the agent file; the brief may downgrade an agent below its baseline for a simple task, never upgrade; a failed downgraded run retries at baseline.

## The delegation contract

Every delegation carries a brief; every report opens with its status; no agent delegates to another. Every dispatch carries a brief the agent can act on with zero conversation history, and anything not written in the brief does not exist for the agent.

The status words are `DONE`, `PARTIAL`, `BLOCKED`, `NO-BINDING`; there is no `FAILED`.

Agents never commit — the version-control ceremony agent excepted, it *is* the ceremony. An agent's own success claim is never the gate.

The brief's field list, the report trailer, receipt validation, and the filing rule live in the core `delegation-contract` reference, not here.

## The amendment rule

The constitution changes only on a `design-` branch of the base repo, through this same design workflow, locked by the human at G1.

The rest of this section is in force from `.claude/references/core/constitution-annex.md`, where its text moved verbatim.
