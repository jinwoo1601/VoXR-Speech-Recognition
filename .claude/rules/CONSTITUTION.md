# The Constitution

This is the workflow every project on this harness runs; each project holds a copy it does not edit. The constitution says *what*; the project's `CLAUDE.md` says *how*; packs say *who* and by which procedure. Each project's `CLAUDE.md` binds this workflow to that project's tooling.

> **Process docs come before implementation. Product docs come after.**

- **Tier 1 — process docs that lock: design docs and requirements docs.** They record *how the work got here*: the exploration, the locked decisions, the intended shape. Written before building; **immutable once locked**, and changed only by unlocking — amended on a design branch and re-locked by the human at G1 — and that branch need not be the topic that owns the doc: a design may amend another topic's locked doc where it names the exact site and the human's G1 covers it. The human's ruling, quoted in session, also unlocks the locked text it names: the change is made on the branch whose work needs it, as a dated note appended to the doc that quotes the ruling and keeps the original text, with no design branch and no re-lock.
- **Tier 2 — the process doc that lives: a path's working record, where the path keeps one.** A piece of work's record — the how, the build plan, and what the build taught. **No lock event:** it is reconciled and corrected as the build teaches, including after G2.
- **Tier 3 — product docs** — the project's knowledge base — represent *what was actually built*. Written or updated only after a piece of work is implemented and accepted, where its path writes product docs, never during design or implementation.

## Paths and the three gates

Every piece of work runs on one path: an ordered list of stages, each a skill, and the human gates it carries. A pack declares each path it supplies; the project composes the packs. The design path may run far ahead of the paths that build what it locks; they are not forced to move together.

**The design path.** Its pack holds its steps: the design doc where the process-docs binding says, its closing G1, the break into a feature backlog — feature name, one-line scope and the path each row runs on — and the merge to the main branch.

**Paths.** Each piece of work takes one path its project composes and runs on its own branch. The path is named at the work's G0 — the hub proposes it from the work's shape, the human confirms — and written on the work's STATUS line; work broken from a locked design takes the path its backlog row names, under that design's G1. The path's pack holds its stages, its gates, its review depth, the heavier paths it may move up to and its requirements template, where it keeps one. A move to a heavier path the current path declares takes no ruling — the PM states it and writes the STATUS line; a move to a lighter path takes the human's word.

- **G0** — branch exists, scope and path agreed, before any doc or code. **Human confirmation**; where the human names the work at the hub, that word is its G0, recorded with the path.
- **G1** — design or findings locked, before breaking into features or merging; also a G1 a path holds inside it. **Human sign-off.**
- **G2** — the work accepted, before writing its product docs, where its path writes them, and merging. **Human sign-off.**

**The floor.** A path carries the gates its pack declares, never fewer than these: G0, on every path; a closing human G1 or G2 on a path whose work reaches the main branch, or a close by class where the classes binding delegates the changeset's class — an unfilled or `none` slot delegates nothing, and a close by class is not a G2; a path whose work never reaches the main branch may carry no closing gate. A path declares review depth `none` only where it merges no code. The validator checks every composed path against the floor.

I do not cross G1 or G2 on my own judgment. "Locked" and "accepted" are the human's calls.

**Branching.** One branch per piece of work, named by the branch binding with the prefix its path declares (`design-`, `feat-`, `proto-`, `light-` and `lab-` by default); never design or build on the main branch. Default to sequential-through-main — build a feature, merge it, branch the next off the updated main; dependency-ordered features branch off their prerequisite. Branches neither of which depends on the other may run concurrently as *One writer per tree* allows.

**When implementation contradicts a locked process doc.** If building reveals a locked decision was wrong, **stop**. Reopen the tier-1 doc that carries it — the design doc, or the requirements doc — on a design branch — which may be cut from the feature branch and merged back into it after G1, reaching the main branch with the feature's G2 — amend it, re-lock (**G1** again), then resume; or, where the human rules the change, quoted in session, append it to that doc on this branch as a dated note that quotes the ruling and keeps the original text, then resume. Never silently patch a locked decision mid-implementation.

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

Agents never dispatch agents and never invoke skills. Skills never fork into a subagent when they orchestrate; execution-type skills may fork. Fan-out always ends in an explicit fan-in step with a merge rule. Every irreversible act (a merge into the main branch, delete, publish, external post) sits behind a human stop, save the merge of a changeset closed by class, in a class the classes binding delegates.

Reviews always fan out through a skill, never ad hoc.

## Standing principles

- **Context is the budget.** What a session or agent loads at start carries current state only; closed history moves to dated records read on demand, never deleted. `memory/STATUS.md`, read at every session's start, carries a size budget in its binding; `doctor` warns over it, and a gate does not pass over it while it can still be trimmed at that gate.
- **Proportionate ceremony.** Review depth is as deep as the path declares — fixed, or computed from the changeset where the path declares `computed`. Ceremony a change does not need is waste, not safety.
- **Display, don't ask.** Between a skill's declared stops the PM states the next step and takes it; it stops only at a gate, before an irreversible act, at a scope change, or at a fork with no recommended option, or one the fork default's exceptions stop. A review finding verified CONFIRMED is fixed by default and listed at the next gate; a disputed finding, a deferral or a scope change stops for the human. At a fork between declared stops the PM takes the recommended option and lists it with its option text at the next gate, except that it fixes rather than defers a CONFIRMED finding whose fix lies inside the changeset's own files and needs no new decision — deferral is proposed only when the fix needs another branch's files, a locked doc, or a design choice; runs the probe when a runnable probe is offered beside a docs-only option; never splits or edits a built plan; stops for any option that amends a locked doc, merges, publishes, deletes outside the tree, changes the human's settings, or changes scope; and stops when the options differ on the human's own time.
- **Every invariant names its enforcer.** A hook where a breach is silent or irreversible, the validator where it is structural, prose only where it takes judgment. A rule with no enforcer is advice and says so. The register of each rule and its enforcer is `.claude/references/core/constitution-annex.md`, `## Invariants and their enforcers`.
- **Evidence and computed numbers.** A check counts only by its evidence — output shown, exit code read. A count or figure written into a doc, report or memory comes from a command run for it.
- **Refusals are reported.** A tool call refused by permission or policy is reported, never retried in another form.
- **Session rhythm.** A branch session hands off at a phase boundary, or, inside a phase, at the next skill-step boundary once its context passes the bound the project names — never below the bound merely because a step ended. A hub session does not hand off at the bound: it files what only its context holds and carries on, and hands off on the human's word. Where the project binds a session launch, the PM launches the successor without waiting to be asked.

## The PM — role and write scope

The PM is the human's session, by constitution. It orients, composes, plans with the human, invokes skills, dispatches agents through them, dedups and rules on findings with the human, and holds every gate.

**Write scope.** The PM writes `memory/` (which includes `STATUS.md`), CHANGELOG lines, and the brief and collation files under the scratch area — on a `lab` path's branch, anything under the scratch area — and makes edits it does not compose anywhere but a locked doc or code logic. Composition is delegated by brief; a PM edit shows its diff at the next gate.

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

Parallel agents that write need physical isolation — a working tree each, made by the procedure the version-control binding names — or serialization; within one working tree, **one writing agent at a time** holds. Read-only agents may run in parallel.

Parallel writers on one branch take disjoint write sets, fixed in the persisted plan, and are joined back onto the branch serially before its one independent gate; the join is not a gated merge, and a conflict at the join stops for the human.

Branches may run concurrently, each in its own tree, when neither depends on the other. Their write sets may overlap only in the release files the `trees` binding names, each with its resolution rule — these overlap freely, and their conflicts are resolved by that rule with no stop; with none named there is no release class — or where the human rules the overlap at the branch's cut. A conflict is resolved by the later-merging branch in its own tree, after its gate's word and before its merge, on that word's authority: a resolution that keeps both sides merges on a green gate and is listed for the human at the next human stop; one that chooses between them returns to the human before it merges. An overlap found after the cut takes no stop: its gate packet names it, and the later-merging branch resolves it at its catch-up as any conflict, its resolution listed for the human at the next human stop; a conflict in locked text still stops. The main checkout stays on the main branch and holds the project's memory; merges into the main branch stay serial.

## The subagent model rule

Every agent carries an explicit model in its file, `inherit` only for `architect`; a brief may downgrade an agent below that model, never upgrade, and the agent guard denies an upgrade.

## The delegation contract

Every delegation carries a brief; every report opens with its status; no agent delegates to another. Every dispatch carries a brief the agent can act on with zero conversation history, and anything not written in the brief does not exist for the agent.

The status words are `DONE`, `PARTIAL`, `BLOCKED`, `NO-BINDING`; there is no `FAILED`.

Agents never commit — the version-control ceremony agent excepted, it *is* the ceremony. An agent's own success claim is never the gate.

The brief's field list and the report trailer live in the core `brief-fields` reference, and receipt validation and the filing rule in the core `delegation-contract` reference, not here.

## The amendment rule

The constitution changes only on a `design-` branch of the base repo, through this same design workflow, locked by the human at G1.

The rest of this section is in force from `.claude/references/core/constitution-annex.md`, where its text moved verbatim.
