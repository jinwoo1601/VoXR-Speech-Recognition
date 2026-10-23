---
name: phase-pickup
description: "Plan one implementation phase from recon, then persist and validate its plan as its own file beside the architecture doc. Use on 'pick up phase N' or 'start/resume the next phase'."
kind: orchestration
agents: [architect, code-mapper, doc-extractor, plan-validator]
stops: [the plan stop where the persisted plan's scope or files or split differ from its build-plan row or plan-validator is missing, a canon-conflict or scope-changing validator defect]
status: active
bindings: [process-docs, vc, board, layout]
removal-date: null
---

# phase-pickup

## What this is

**A phase pickup is orientation + reconnaissance + a plan that survives the session.** It turns "pick up phase N" into: verified current state, agent-gathered ground truth, a plan persisted into canon, and a validated go signal.

> Plan ONE phase. Recon by agents, and no plan exists until it is persisted in its own file beside the architecture doc.

## Bindings

- `process-docs` (required) — where the feature's requirements and architecture docs live, which the prerequisites confirm, and the path a phase's plan file is written at, which PERSIST fills. Absent → stop and say so before any recon is dispatched; the plan has no home, and an unpersisted plan does not exist. For a lite feature it also places the backlog whose row is read (`.claude/references/core/feature-lite.md` `## The acceptance source`).
- `vc` (required) — the working-tree read and the current branch at ORIENT and at the branch prerequisite (*Base-revision reads*), and the check-in at RECORD & COMMIT (*Check-in procedure*). Absent → stop and report; never improvise a version-control idiom.
- `board` (optional) — where cross-session state lives, the row ORIENT cross-checks against local state and RECORD & COMMIT advances. Absent → `memory/STATUS.md` is the state; orient from what the project binds instead, and skip the row advance.
- `layout` (optional) — the *Source*, *Tests* and *Generated* lines RECON quotes into the `code-mapper` dispatch, by which it classifies each site. Absent, empty, `TODO` or `none` → the dispatch says so, and `code-mapper` classifies each site by its own reading; never a stop.
- Agents — `architect` missing → stop before PERSIST and say so. `code-mapper` or `doc-extractor` missing → run RECON with what the remaining agent returns and record the gap in the persisted plan; both missing → stop, because the plan would rest on no verified ground truth. `plan-validator` missing → the plan stop at PLAN always, PERSIST's brief asking the plan to record that it goes unvalidated, and VALIDATE skipped.

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
- Auto-mode entries: included
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

<!-- harness:binding:start layout .claude/bindings/core.md -->
<!-- Interview: Which directories hold this project's source, which its tests, and which are generated or composed output never edited by hand — or a single `none`? -->
- Source: `Runtime/**`, `Editor/**`, `NativeBridge~/src/**`, `NativeBridge~/include/**`
- Tests: `Tests~/**`, `NativeBridge~/harness/**`
- Generated: `.claude/**` except `.claude/bindings/**` (composed by `harness.py`); `Runtime/Plugins/Android/arm64-v8a/*.so` (built from `NativeBridge~/` by its `build.sh`); `NativeBridge~/build*/` and `NativeBridge~/vendor/` (ignored)
<!-- harness:binding:end layout -->

## When it applies — and prerequisites

**Trigger:** starting or resuming an implementation phase on a feature branch.

The steps below branch on the four keys `## Bindings` above names — the process-doc layout, version control, whether a workflow board exists, and the project's source layout — and each carries its degradation path there.

Confirm before proceeding — if any item fails, stop and say which skill or step is actually needed:
- The feature branch exists and the workspace is on it (never plan on main).
- The feature's requirements doc and architecture doc exist (else: `feature-requirement-doc` / `architecture-doc` first). The skill applies the test of `.claude/references/core/feature-lite.md` `## The mark` here; a lite feature needs its architecture doc alone.
- The phase is identifiable from the architecture doc's Build plan (or the board row, where a board is bound).

A phase whose plan file exists at the path the `process-docs` binding names is planned: a resumed pickup goes to `implement` once the plan was validated — known by the `plan-validator` report `.scratch/plan-validator-<feature>-phase-<id>-report.md`, filed beside VALIDATE's brief; without one, to PLAN where the plan carries a `Scope:` line, a ruling already given riding in the handoff brief, and to VALIDATE where it carries none.

## Workflow

1. **ORIENT** — read the working tree and the current branch with the reads the `vc` binding's *Base-revision reads* slot names. If the project binds a workflow board, read it and cross-check the row against local state — if the row's stage, dates, or commit refs don't match (e.g. stage already `implement` with no matching workspace evidence, or notes newer than this session's handoff), state the mismatch and the reading this pickup follows — the local branch and files — go on, and list the mismatch at the next gate. No board bound → orient from the sources the project binds instead (e.g. open PRs/issues). Where this session ran the previous phase's `implement`, this pickup is a phase boundary: invoke `handoff`, its objective this phase's pickup, and take no further step, as `handoff` `## When to hand off` says.
2. **RECON** — spawn `code-mapper` (the code sites this phase touches, with any line refs from the docs to re-verify, and the `layout` binding's lines quoted verbatim, or a line saying it is absent) and `doc-extractor` (the requirements, ADR constraints, and architecture decisions bearing on this phase, as a numbered question list) — both in a single message, all paths absolute. For a lite feature the backlog row and the locked design stand for the requirements (`.claude/references/core/feature-lite.md` `## The acceptance source`).
3. **PERSIST** — scope is one phase: if the human asks for two, plan the first and note the second — never a combined plan. File `.scratch/architect-<feature>-phase-<id>-brief.md` on the field list of `.claude/references/core/brief-fields.md` — the phase and its scope, the phase's row in the architecture doc's `## Build plan`, quoted, the requirements and acceptance criteria it discharges, the architecture doc and the requirements doc by path, the two recon reports attached by path, the plan file's path — the pattern the `process-docs` binding names, `<feature>` and `<id>` filled — what is out of scope, and the project rules, as that field list says; it asks the plan to close with a `Runtime claims` list — each step whose correctness rests on what something does when run — or `none` — and, where `plan-validator` is not in the composed set, to record that it goes unvalidated; and dispatch `architect` in its plan mode through the Agent tool without a `name` to write the plan file at that path and its link line in the architecture doc's `## Build plan`. For a lite feature the brief says `lite`, gives the backlog's path, quotes the row and cites the criteria the phase discharges as A1…An (`.claude/references/core/feature-lite.md` `## The acceptance source`).
4. **PLAN** — read the persisted plan and compare it with its row by three triggers only: the files, the plan's files table against the files the row names, a file added or dropped — silent where the row names none; the scope, the plan's `Scope:` line against the row's scope, work taken on or dropped; the split, a Unit column in the files table where the row has no split. Never test counts, constants, step order or wording. A statement or ruling already given for this persisted plan, carried in the handoff brief, ends the comparison, and PLAN goes on as that statement or ruling says. A trigger fired, or `plan-validator` missing from the composed set → the plan stop; none → state the plan in one message and go to VALIDATE. The plan stop is a ruling in session per `.claude/references/core/ruling-form.md`, its information the plan, the row and the PM's message naming what fired, its options accept, send back to the row, or treat as a scope change. A send-back is `architect`'s ruled-fix pass: `.scratch/architect-<feature>-phase-<id>-fix-brief.md` in PERSIST's form, carrying the ruling and the plan file by its path, dispatched through the Agent tool without a `name`; PLAN then compares the rewritten plan. No step drafts a plan or enters Claude Code plan mode.
5. **VALIDATE** — pick the depth: `narrow` where the plan's files table has at most 3 rows and the plan's last line reads `Hazards: none`; otherwise `full`. File `.scratch/plan-validator-<feature>-phase-<id>-brief.md` on the field list of `.claude/references/core/brief-fields.md` — the plan file by its path, the depth and the canon set (requirements, architecture doc, ADRs), the project rules, as that field list says, and nothing of this session's reasoning — and spawn `plan-validator` on that brief. The plan's runtime claims are not the validator's to confirm: they go to implement's first gate. For a lite feature the canon set is the backlog row, the locked design, the architecture doc and the ADRs, and the brief names the feature lite (`.claude/references/core/feature-lite.md` `## The acceptance source`).
6. **RECONCILE** — the PM rules which findings are real defects. A canon-conflict finding stops the line: per the workflow, reopen the design question with the human — do not patch around it. Findings ruled real in the other fix-pass classes `plan-validator`'s defect-class line names are applied by a second `architect` dispatch: `.scratch/architect-<feature>-phase-<id>-fix-brief.md` in PERSIST's own form, carrying those findings and the plan file by its path and nothing of the PM's reasoning, dispatched through the Agent tool without a `name`. Every other finding stays in the validator's report, for `implement`. If a defect changes the plan's scope, re-gate with the human. For a lite feature an `unfixed-choice` finding ruled real by the test of `.claude/references/core/feature-lite.md` `## The escape` is the return `.claude/references/core/feature-lite.md` `## The return to full` describes, not a fix pass.
7. **RECORD & COMMIT** — if a board is bound, advance its row (implement stage, date). Then check in per the `vc` binding's *Check-in procedure* slot. If the binding marks the process-doc area untracked/local-only, the persisted file is the record — nothing to commit.

A handoff taken at a step boundary, as `handoff` `## When to hand off` says, at the end of PLAN carries the plan file's path and the statement or the ruling, not the plan whole, so the next session's PLAN goes on from it. After step 7 there is no next step, and the objective is `implement` for the phase, as `handoff` `## When to hand off` says of a skill's last step.

Recon, the plan's authorship and its validation are the agents' work.
