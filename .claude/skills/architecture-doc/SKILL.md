---
name: architecture-doc
description: "Author, revise or reconcile to as-built a feature's architecture doc: the seams between its components. Use after the requirements doc and before implementation, or to reconcile it before G2."
kind: orchestration
agents: [architect, architect-reconcile]
stops: [the one ask after BAR on the contradictions and the phase breakdown, a contradiction with the requirements or the locked design]
status: active
bindings: [process-docs, vc, board, product-docs]
removal-date: null
---

# architecture-doc

## What this is

An architecture doc specifies **how one feature is built** — the seams between its components and the runtime flow across them, the non-functional realization — and **holds the build plan's breakdown and the links to its plan files**. It answers the one question the design and requirements docs refuse to; both of them say *names a class → that is the architecture doc*. It sits on the feature branch, written *after* the requirements doc, and feeds **G2**. A lite feature has no requirements doc; its doc is written beneath its two G0 sections (`.claude/references/core/feature-lite.md` `## The three placements`).

It carries two mandates a generic architecture doc does not, and they shape everything:

> The architecture doc is the feature's working record — the living document that holds the how, the build plan, and what the build taught. It is not a draft of the product page: the product docs are authored after G2 from live source. One obligation crosses over — `## Decision register` is the design rationale a reader cannot recover from the code, and it is what the product docs carry forward. **And the build plan is anchored here:** plan-mode output evaporates at session end, so each phase's implementation plan is persisted as a plan file this doc links.

The test of every line: *would a maintainer who did not sit in this feature need this to build, verify or reopen what was built?* So document the **load-bearing decisions** — the ones costly to reverse — with their rejected alternatives, cite real paths and symbols so drift is detectable, and reconcile the doc to **as-built** before G2. Trivia the code already states, exact signatures that churn, throwaway scaffolding: leave them out. The code is the source of truth; this doc records how it is organized and *why*.

## Bindings

- `process-docs` (required) — where feature docs live and what they are called, and therefore where this feature's architecture doc sits beside its requirements doc, and the path a phase's plan file is written at. Absent → stop and say so before any brief is filed. For a lite feature it also places the backlog whose row is read (`.claude/references/core/feature-lite.md` `## The acceptance source`).
- `vc` (required) — the branch procedure (the feature branch this work runs on, never the main branch) and the check-in procedure for the file drafted here.
- `board` (optional) — where cross-session state lives beyond `memory/STATUS.md`, and therefore where this feature's row is updated. Absent → `memory/STATUS.md` is the state; orient from what the project binds.
- `product-docs` (optional) — the product surface that takes this feature's `## Decision register` at G2, when the agent that reconciles the product docs writes them from the built tree; this doc is not drafted toward that surface. Absent → the register is written the same way regardless, since it records why each costly-to-reverse choice was made and which alternative it beat, and where it lands is settled with the human at G2.
- Agents — `architect` missing → stop before DRAFT and say so. `architect-reconcile` missing → stop before RECONCILE, or before a record PLAN needs, and say so.

<!-- harness:binding:start process-docs .claude/bindings/core.md -->
<!-- Interview: Where do this project's design docs and feature docs live, what are the files called, and at which path is a phase's plan file written, and at most how many KB may an architecture doc's body, a plan file and a brief each hold, at which path pattern? -->
- Design docs: `Planning~/design-docs/<topic>.md`, one file per design topic, its decision records beside it per design-track's ADR convention; the root-level `Planning~/*.md` vN docs are pre-harness history, read-only
- Feature docs: `Planning~/features/<feature>/requirements.md` and `Planning~/features/<feature>/architecture.md`, one directory per feature
- Persisted plan: `Planning~/features/<feature>/plan-<id>.md`
- Architecture-doc body budget: 40 KB over `Planning~/features/<feature>/architecture.md`; the closed features' oversized docs, all written before the harness, are ruled in `memory/doctor-rulings.md`
- Plan-file budget: 10 KB over `Planning~/features/<feature>/plan-*.md`
- Brief budget: 15 KB over `.scratch/**/*brief*.md`
<!-- harness:binding:end process-docs -->

<!-- harness:binding:start vc .claude/bindings/vc-git.md -->
<!-- Interview: Which version-control system does this project use, and how are branches created, changes checked in, base revisions read, branches merged, the working tree recorded before an upgrade, and pull requests read, diffed and commented on? -->
- System: git; GitHub, remote `origin` `https://github.com/jinwoo1601/VoXR-Speech-Recognition.git`, public; `gh` on the Windows PATH (`C:\Program Files\GitHub CLI\gh.exe`), authenticated as `jinwoo1601`
- Main branch: `main`
- Auto-mode entries: excluded
- Branch procedure: a re-lock — a `design-<topic>` branch cut from the feature branch's head in the feature's own tree, declared on its STATUS line `re-lock of <feature-branch>`, merged back into that feature branch after G1 — is cut by `git switch -c design-<topic>` in the feature's tree and merged back by `git switch <feature-branch>`, then `git merge design-<topic>`, in that tree; branches are `design-<topic>`, `feat-<feature>`, `light-<topic>` (an issue's `light-<n>-<slug>`), `lab-<topic>`, which merges nothing, and the release prep `chore/release-X.Y.Z`; each is cut from an up-to-date `main` — `git fetch origin`, then on `main` `git merge --ff-only origin/main`, then `git switch -c <branch>` — and runs in the one checkout, since `trees` is none
- Check-in procedure: inline on the main thread — `git add -- <paths>`, then `git commit -F <message file> -- <paths>`, each one command, never a heredoc or a chain, never `git add -A` — a hub operation's `memory/` check-in by the base's `scripts/hub.py commit --message <file> --root <hub> <paths>`, which commits the named paths under `memory/` alone; then `git push -u origin <branch>` on a branch's first push and `git push` after; the commit sha is the receipt
- Base-revision reads: `git show <revision>:<repo-relative-path>`; `git diff --name-status <base>..<head>` for the changed-file list; `git diff <base>..<head> -- <path>`; `git log --oneline <base>..<head>`; `git status --short` for the working-tree read; `git branch --show-current` for the current branch; `git diff --numstat --no-renames <base>..<head>` for the per-file line-count read
- Merge procedure: a pull request against `main`, opened by `gh pr create --base main --title "<title>" --body-file <file>` after the push, merged by the human on GitHub behind the gate's stop; Claude never merges
- Snapshot procedure: `git stash create`, then, where it prints a sha, `git stash store -m "harness pre-upgrade <date>" <sha>` — the working tree and the index are left as they are; where it prints nothing, the snapshot is `git rev-parse HEAD`; confirmed by `git rev-parse --verify <sha>^{commit}`; restored by the human from that sha; `.claude/` and `CLAUDE.md` are tracked from `light-harness-adopt` on, so the record covers them — `.claude/settings.local.json` and `.scratch/` stay ignored and are not covered
- PR read: `gh pr view <n> --json number,title,body,url,state,isDraft,headRefName,headRefOid,baseRefName,files,labels` and `gh pr list --head "<branch>" --json number`; `gh` has no `baseRefOid` and no `closingIssuesReferences` field, and `--json` never goes without an explicit field list
- PR diff: `gh pr diff <n> > <file>`
- PR comment: `gh pr review <n> --comment --body-file <file>`, falling back to `gh pr comment <n> --body-file <file>`; never `--approve` or `--request-changes`, which GitHub refuses on one's own pull request; `gh pr edit` fails on the Projects-classic `projectCards` deprecation, so a PR body is patched by `gh api repos/jinwoo1601/VoXR-Speech-Recognition/pulls/<n> -X PATCH -F body=@<file>`
<!-- harness:binding:end vc -->

<!-- harness:binding:start board .claude/bindings/core.md -->
<!-- Interview: Does this project track cross-session state on a board — which, and how is it read — or is memory/STATUS.md the only state? And at most how many characters may one memory/STATUS.md line carry, or is there no cap? And at most how many KB may memory/STATUS.md hold, or is there no budget? -->
- Mechanism: none — memory/STATUS.md
- STATUS line cap: 200
- STATUS budget: 10 KB
<!-- harness:binding:end board -->

<!-- harness:binding:start product-docs .claude/bindings/codex.md -->
<!-- Interview: Where do this project's product docs live, is the layout a wiki vault or a Documentation~/CHANGELOG surface, is there a schema file for them, and which agent reconciles them after G2 — or none bound? -->
- Location: `Documentation~/` (the hub `Documentation~/index.md`, the per-type API reference under `Documentation~/api/`), plus `CHANGELOG.md` and `KNOWN_LIMITATIONS.md` at the root
- Layout: docs+changelog
- Schema file: none
- Reconciling agent: codex-reconcile
<!-- harness:binding:end product-docs -->

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
3. **DRAFT** — file `.scratch/architect-<feature>-brief.md` on the common field list of `.claude/references/core/brief-fields.md`; core ships no architect-brief template, so the brief is written to that field list. It carries the requirements doc and its parents by path, the live code GATHER read, attached by path, `assets/architecture-template.md` as the skeleton, the target directory and file name per the `process-docs` binding, and the product surface the `product-docs` binding names — carried as the destination this feature's `## Decision register` is owed to after G2, not as a shape to draft toward. Dispatch `architect` in its author mode through the Agent tool without a `name`, naming that brief by its absolute path. For a lite feature the brief names the track, gives the backlog's path, quotes the row, numbers its criteria A1…An (`.claude/references/core/feature-lite.md` `## The acceptance source`), and attaches the existing doc, whose two G0 sections are kept (`.claude/references/core/feature-lite.md` `## The three placements`).
4. **BAR** — judge the returned draft against `## The quality bar` below. For the hard parts — holding the altitude, realizing the non-functionals, writing so a maintainer who did not sit in the feature can work from it — `references/quality-bar.md` carries the worked examples, the altitude stack and what crosses over at G2. A miss is a second brief to `architect`, never a PM edit: file `.scratch/architect-<feature>-bar-brief.md` from the common fields of `.claude/references/core/brief-fields.md`, the draft by path and each miss against the bar quoted, and dispatch `architect` in its author mode through the Agent tool without a `name`, naming that brief by its absolute path; a further miss re-files it. Where the report raises a contradiction with the requirements or the locked design instead of a defect in the draft, no stop is made here: the contradiction is held for the one ask at step 5 PLAN, where the second `stops:` entry attaches to it. For a lite feature a choice in the report that the locked design does not fix is put to the test of `.claude/references/core/feature-lite.md` `## The escape` and is not a contradiction put to the human.
5. **PLAN** — the one ask after BAR, the hard stop the first `stops:` entry attaches to: `architect` returns the phase breakdown, and the human rules, in one ask put per `.claude/references/core/ruling-form.md`, first each contradiction BAR held, each a ruling under the second `stops:` entry, then the breakdown's order, its risk ordering and its count, before the doc is filed and before any phase starts. The ask says plainly that it is the human's last stop before G2: after it the run goes on unattended to the G2 packet, stopping only where a composed skill declares a stop or the constitution's *Display, don't ask* names one. The breakdown is written into `## Build plan` as one row per phase carrying three fields — the phase id, its scope, and the requirements it discharges — ordered lowest-risk-first, each phase green by the `verification` binding's commands and ideally independently shippable. A ruling that changes the returned breakdown is written into `## Build plan` by `architect-reconcile` in its record mode: file `.scratch/architect-reconcile-<feature>-plan-brief.md` from the common fields of `.claude/references/core/brief-fields.md`, quoting the ruling, and dispatch `architect-reconcile` through the Agent tool without a `name`, naming that brief by its absolute path. The per-phase plans themselves are written by `architect`, through the phase pickup that runs when each phase begins, each as its own plan file at the path the `process-docs` binding names, with one link line to it in `## Build plan`; **this skill writes none of them.** Each contradiction's answer is a further brief to `architect`: file `.scratch/architect-<feature>-contradiction-<n>-brief.md`, `<n>` the contradiction's number in the ask, from the common fields of `.claude/references/core/brief-fields.md`, quoting the ruling, and dispatch `architect` in its author mode through the Agent tool without a `name`, naming that brief by its absolute path; it corrects the draft to the ruling and returns the breakdown again. PLAN is asked again only where a contradiction's answer changes the breakdown: where the breakdown `architect` returns after that answer differs from the ruled one in its order, its risk ordering or its count, the first `stops:` entry attaches again for that re-ask alone. A contradiction with locked canon that a returned plan raises is ruled under the second `stops:` entry. For a lite feature the third field is the criteria it discharges, A1…An (`.claude/references/core/feature-lite.md` `## The acceptance source`).
6. **RECONCILE** — after building, file `.scratch/architect-reconcile-<feature>-as-built-brief.md` from the common fields of `.claude/references/core/brief-fields.md` and dispatch `architect-reconcile` through the Agent tool without a `name`, in its reconcile mode, naming that brief by its absolute path, to write the `## As-built deltas` pass: where the build diverged from the plan and the decisions above, under the `## As-built deltas` charter, which the template's `## As-built deltas` guidance (`assets/architecture-template.md`) carries and the brief quotes verbatim from there. The doc that reaches G2 must describe what was *actually built*.
7. **FILE** — the doc and its bookkeeping per `## Filing and bookkeeping`.

**No SETTLE step, deliberately** — unlike its sibling: `architect` is contracted to produce the plan's parts itself, deriving each decision from a requirement, a locked design decision or a real constraint, so settling that content with the human in advance would pre-empt the work the agent is dispatched to do.

## The quality bar

Every architecture doc must clear these. (Expanded, with good/bad examples and what crosses over at G2: `references/quality-bar.md`.)

1. **Written as the feature's working record.** The architecture doc is the feature's working record — the living document that holds the how, the build plan, and what the build taught. It is not a draft of the product page: the product docs are authored after G2 from live source. One obligation crosses over — `## Decision register` is the design rationale a reader cannot recover from the code, and it is what the product docs carry forward. So shape it for the maintainer who will build, verify or reopen this feature — context, the seams, the plan, and what the build taught — rather than for a reader of the product page.
2. **Load-bearing decisions, with the rejected alternative — recorded inline.** Spend words on the choices costly to reverse (state ownership, the seams, event versus polled state, execution-order placement, module boundaries) and on *why this over the obvious alternative*. Record them inline in a decision register — **not** as separate decision-record files; a feature has just its requirements doc and this one. A lite feature has this one alone (`.claude/references/core/feature-lite.md` `## The acceptance source`).
3. **Right altitude — context → seams → only load-bearing code detail.** Do not transcribe the code method by method; it rots and duplicates the source of truth. Do not stay so vague that it constrains nothing. Pick the level and hold it. The per-function detail is each phase's plan's.
4. **Seams are the spine.** What crosses each seam between components (events, schemas, the per-step structures, serialized and persisted state) and the runtime flow across them (per request, per step, lifecycle, state machine), each end cited by path and symbol — never per function. A reader should trace an operation end to end without opening the code.
5. **Non-functional realization is first-class — on some platforms it is the architecture.** Show how the budget, the allocation pattern, the behaviour under degraded dependencies and the structural laws this project's platform imposes are *met* structurally, not restated. A clean design that blows the budget every step is *wrong* here.
6. **Cite real paths, symbols and changesets — make drift detectable.** Code is primary and it moves; cite the file and the symbol, with a line number where it helps. Document slow-drifting rationale heavily and fast-drifting signatures lightly.
7. **The build plan is broken down and linked here — sequenced, risk-first, verifiable.** Persist the plan (it is otherwise lost): the breakdown rows here, one link line per phase, each phase's plan in its own file; ordered phases lowest-risk-first, each one green by the `verification` binding's commands, run by `compile-check`; the riskiest or most visual part prototyped first, outside the shipped tree; and the assets the build needs that code cannot author flagged, because nobody finds them mid-phase.
8. **Honest about risk, tradeoff and as-built drift.** Name the fragile parts, the load-bearing assumptions and the deferred work — and after building, record where the build diverged, in `## As-built deltas`, under the charter that section's guidance carries. Honest beats tidy: this doc must describe what was actually built. A lite doc meets this bar and bar 5 through the locked design it cites, since lite means the design fixed every choice: its non-functionals and risks stay there, and its `## As-built deltas` records drift (`.claude/references/core/feature-lite.md` `## The mark`).

**Smell tests:** transcribes the code method by method → it is a duplicate; document the *why* and the shape. Lists seams with no rationale → nothing to evaluate or reopen. No non-functional realization → the load-bearing half of the architecture is missing. A line no maintainer would need in order to build, verify or reopen what was built → it fails the anchor; cut it. Uncited or vague → drift-blind. A plan with no risk ordering → sequence it lowest-risk-first.

## Filing and bookkeeping

- **Location:** where the `process-docs` binding says feature docs live, beside this feature's requirements doc; a project's first feature creates that directory. New files are added to version control per the `vc` binding's check-in procedure. A lite feature's doc is there from G0, with no requirements doc beside it (`.claude/references/core/feature-lite.md` `## The three placements`).
- **Frontmatter:**
  ```yaml
  ---
  type: architecture
  feature: <kebab-name>
  topic: <the design topic this feature belongs to>
  status: draft         # draft while building; accepted at G2 by g2-accept RECORD, and still living after
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
