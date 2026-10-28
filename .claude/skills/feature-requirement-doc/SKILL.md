---
name: feature-requirement-doc
description: "Author or revise a feature's requirements doc, its testable contract, after G1 and before the architecture doc. Use when a feature is picked off the locked design, or when asked what belongs in requirements or acceptance criteria."
kind: orchestration
agents: [doc-writer, architect-reconcile]
stops: [the agreement at SETTLE before the drafting brief is filed, the call on each open fork and deferred non-goal]
status: active
bindings: [process-docs, vc, board]
removal-date: null
---

# feature-requirement-doc

## What this is

A feature-requirement doc turns one slice of **G1-locked design** into a precise, scoped, testable contract for a single buildable feature — **without deciding how to build it**. It sits in one specific gap in the workflow: *after* the design is locked, *before* the architecture doc, which is the *how* and the working record the build keeps as it goes.

The anchor idea, and the test of every line:

> **The requirements doc is the G2 acceptance checklist, written in advance.**

G2 is *feature accepted* — the human's sign-off. If at G2 the human cannot walk the doc ticking each line against the built thing, including the checks only a human can run, the doc failed its one job. Everything below follows from that.

## Bindings

- `process-docs` (required) — where feature docs live and what they are called, and therefore where this feature's directory and its requirements doc sit. Absent → stop and say so before any brief is filed.
- `vc` (required) — the branch procedure (the feature branch this work runs on, never the main branch) and the check-in procedure for the file drafted here.
- `board` (optional) — where cross-session state lives beyond `memory/STATUS.md`, and therefore where this feature's row is updated. Absent → `memory/STATUS.md` is the state; orient from what the project binds.
- Agents — `doc-writer` missing → stop after SETTLE and say so. `architect-reconcile` missing → on a return the record pass is reported as not done and the two sites stand, which misroutes nothing (`.claude/references/core/feature-lite.md` `## The mark`).

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

## When it applies — and prerequisites

Trigger: the implementation track for a feature begins. A lite feature gets this skill only on its return to the full track (`.claude/references/core/feature-lite.md` `## The return to full`). Before drafting, confirm:

- **The design is locked (G1).** Requirements derive from locked design; they never invent it. If the parent design is not locked, **stop** — that is a design-track problem, not a requirements one.
- **You are on a feature branch**, created per the `vc` binding's branch procedure. Not on the main branch.
- **This is docs, not code.** Requirements — and the architecture doc after them — are written before any implementation. Do **not** scaffold, move, or edit code in this phase, even to "set things up"; treat that as a hard line.
- **The human owns the scope and priority calls.** The PM gathers, settles, surfaces tradeoffs and holds the bar; it never invents a requirement the locked design does not support, and it does not cross G2 — that ruling is core's `g2-accept`, and nothing here stops at G2.
- **A locked decision is immutable.** If building later reveals a requirement contradicts locked design, **stop**: reopen that design on a design branch, amend it, re-lock at G1. Never silently patch a locked decision from a requirements doc.

## Where the feature comes from

A feature comes off the backlog the design track produces when a locked design is broken into features — one feature name and one line of scope each — and where a project keeps no such list, off the locked design docs themselves. Step one is to pin down, **with the human**:

1. **Which feature** — its name (kebab-case) and its one-line scope.
2. **Its parent design** — the specific design doc(s) and decision record(s) it derives from. These become the doc's `sources:` and the spine of every requirement.
3. **Its user story** — the story or stories the feature serves, as `g0-open` recorded them in the doc's §1: read them back and confirm them verbatim with the human. Only where §1 is empty — a feature opened before G0 recorded stories — is the story pinned down here, each *As a \<user\>, I want \<capability\>, so that \<value\>.*, captured verbatim. On a return the stories are those of the architecture doc's `## User story` (`.claude/references/core/feature-lite.md` `## The return to full`).

Read those parents **fully** before drafting. A requirement with no parent in locked design is either scope creep or a sign the design is not actually locked — surface it, do not paper over it.

## Workflow

1. **ORIENT** — confirm the feature, the branch and that the design is locked: the branch per the `vc` binding's branch procedure, the cross-session state per the `board` binding. Nothing is drafted before the feature and its one-line scope are agreed.
2. **GATHER** — read the feature's requirements doc as `g0-open` began it — its §1 and §2 — and the locked design doc(s) and decision record(s) the feature derives from, in full, plus any already-merged feature this one depends on. Cite each; an uncited parent is an assumption. On a return §1 and §2 come from the architecture doc's two G0 sections, and §9 opens as `.claude/references/core/feature-lite.md` `## The return to full` says.
3. **SETTLE** — the first hard stop: the PM and the human work the feature out in conversation until its content is settled — opening with the user story as G0 recorded it — confirmed, not re-settled; §1 is settled here only where it is empty; on a return the stop opens with the unfixed choice, before the story, as `.claude/references/core/feature-lite.md` `## The return to full` says — then the scope and the non-goals, the value it delivers, the observable behaviour that shows it working, each functional requirement with its priority and the check that verifies it, the non-functional rows this project's platform makes load-bearing, the dependencies and the assumptions, and the G2 acceptance criteria. That is §§1–9 of `assets/requirements-template.md` in full, and it is the decision record the next step's brief carries: `doc-writer` records decisions and never makes one, so nothing left unsettled here is available for it to invent. A genuine unknown rather than a settled answer leaves this step open and surfaces at FORKS for the human's ruling; §10 is the one section this step does not settle.
4. **DRAFT** — file `.scratch/doc-writer-<feature>-requirements-brief.md` from `.claude/references/core/decision-brief.md`: the settled content and its open items as the decision record, the parents and their citations, `assets/requirements-template.md` as the skeleton, and the target directory and file name per the `process-docs` binding. Dispatch `doc-writer` through the Agent tool without a `name`.
5. **BAR** — judge the returned draft against `## The quality bar` below. For the hard parts — cashing a "feel" target out into an observable proxy, keeping *how* out of *what* — `references/quality-bar.md` carries the worked examples and the *Operationalizing "feel"* table. A miss is a second brief to `doc-writer`, never a PM edit; where the miss is a gap in the settled content rather than in the writing, the fix is upstream — settle it with the human first, then re-brief.
6. **FORKS** — the second hard stop: where scope, priority, a deferred non-goal or a requirement's value is genuinely open, lay the tradeoff out in prose and let the human rule. Do not force a picker, do not silently pick. What stays unresolved is parked in `## 10. Open questions`, and the architecture doc inherits it.
7. **FILE** — the doc and its bookkeeping per `## Filing and bookkeeping`. On a return, once the doc is filed, file `.scratch/architect-reconcile-<feature>-return-brief.md` — the architecture doc and the new requirements doc by path, and the two corrections `.claude/references/core/feature-lite.md` `## The return to full` names — and dispatch `architect-reconcile` in its record mode through the Agent tool without a `name`.

## The quality bar

Every requirement must clear these. (Expanded, with good/bad examples: `references/quality-bar.md`.)

1. **Testable — done is observable.** Each requirement has an acceptance check someone could run against the built thing. A subjective "feel" target is allowed *only* once cashed out into an observable proxy, with the feel target kept alongside as the rationale.
2. **What and why, never how.** No class, data layout, component or algorithm — a good requirement survives a total re-implementation. Genuine *constraints* (a latency ceiling, reuse of an existing pipeline) are allowed: they bound the solution, they do not choose it.
3. **Traced upward.** Each requirement cites the locked design doc or decision record it derives from, and names the stated goal of that design it serves, and each traces to a user story in §1. A requirement that traces to no stated goal, or has no story behind it, is scope creep.
4. **Scoped — non-goals as sharp as goals.** State what the feature does *not* do, what is deferred to a later feature, and which adjacent systems it touches but does not own.
5. **Prioritized — Must / Should / Could.** The Musts define the minimal G2-acceptable version; that is what lets a feature ship instead of sprawling.
6. **Non-functional requirements are first-class — some are feature-killers.** Take the rows this project's platform makes load-bearing — the performance budget, the behaviour at the input boundary when input is lossy or unrecognized, what the feature does when a dependency is degraded, and accessibility — and cut the rows that do not apply rather than leaving them blank.
7. **Dependencies and assumptions stated.** What must already exist or be merged first — features are dependency-ordered, so declare the order — and what is assumed about the people using it and the rest of the system.
8. **Right-sized and honest about unknowns.** A page or three, not a specification suite. End with the open questions you did *not* resolve; the architecture doc would rather inherit a known unknown than hit it mid-build.

**Smell tests:** cannot test it → rewrite it as something observable. Names a class or component → it is architecture; move it to the architecture doc. A "feel" word with no proxy → unfinished. No non-goals → unscoped. No upward citation → unparented, or the design is not actually locked.

## Filing and bookkeeping

- **Location:** where the `process-docs` binding says feature docs live, kebab-case, one directory per feature; a project's first feature creates that directory. New files are added to version control per the `vc` binding's check-in procedure.
- **Frontmatter:**
  ```yaml
  ---
  type: requirements
  feature: <kebab-name>
  topic: <the design topic this feature belongs to>
  status: draft         # draft until G2; accepted at G2 by g2-accept RECORD
  updated: YYYY-MM-DD   # absolute date, never "today"
  sources:
    - <the locked design doc(s) this feature derives from>
    - <the decision record(s) behind them>
  ---
  ```
- **Prose and linking convention:** keep each paragraph and list item on one logical line — no hard-wrapped prose. For link and date form, match what the project's existing process docs already do; where there are none, link by relative path and write dates absolute (`YYYY-MM-DD`).
- **Update the cross-session state:** add or update the feature's row where the `board` binding says — track `feature`, stage `requirements`, the post-G1 pre-architecture stage. No board bound → `memory/STATUS.md` is the state.
- **Do not touch the product surface.** A project's product docs are written only after G2 acceptance, by the agent that reconciles them; writing there now is a layer violation. Requirements are process docs, and their only bookkeeping is the row above. Cross-*linking* to a product page is fine; creating or editing one from here is not.
- **Check in only when the human asks**, per the `vc` binding's check-in procedure. Concurrent sessions edit the same cross-session state — re-read it before appending.
