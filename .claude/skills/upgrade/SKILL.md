---
name: upgrade
description: "Bring a project's composed files up to its base's current pack versions, behind a dry run and the human's stop. Use on 'upgrade this project' or after doctor reports a project behind its packs."
kind: orchestration
agents: [doc-writer]
stops: [the word on the dry run before anything is written, the word to apply with no snapshot where the slot is none]
status: active
bindings: [vc, process-docs]
---

# upgrade

## What this is

A project composed from the base drifts behind it: packs move, files change, keys appear. `upgrade` is how a project catches up without losing what was written locally — the dry run says what would happen, the human rules on it, and a snapshot of the working tree sits under the apply.

> The verb plans, reports, and writes. The snapshot, the human's word, and the verification after are this skill's.

## Bindings

The key is read from **the project being upgraded**, not from the base, because the snapshot is taken of that project's working tree.

- `vc` (required, the project's) — the snapshot procedure SNAPSHOT follows: the record, the read that confirms it, and how the human restores it. The binding absent, or present without its snapshot slot → stop in the first line, name the binding interview, and run nothing. The slot still `TODO` → stop the same way: an unanswered slot is no snapshot.
- The slot answering `none` → state that no snapshot is possible and wait for the human's explicit word that the upgrade applies without one.
- `process-docs` (optional, the project's) — the bindings file answering it, whose *Brief budget* line step 7 reads, and whose *Design docs* slot the report's `design docs:` line flags for step 7's third trigger. The binding absent, or its *Brief budget* slot not filled → step 7's second trigger does not fire, and step 8's `doctor` is the backstop.
- `doc-writer` not composed in this project while the report names keys to append or carries a `design docs:` or `path form:` line, or while the *Brief budget* line needs step 7's update → say so at the stop, before the apply, so the human rules on an upgrade whose binding interview has no writer.

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

<!-- harness:binding:start process-docs .claude/bindings/core.md -->
<!-- Interview: Where do this project's design docs and feature docs live, what are the files called, and at which path is a phase's plan file written, and at most how many KB may an architecture doc's body, a plan file and a brief each hold, at which path pattern? -->
- Design docs: `Planning~/design-docs/<topic>.md`, one file per design topic, its decision records beside it per design-track's ADR convention; the root-level `Planning~/*.md` vN docs are pre-harness history, read-only
- Feature docs: `Planning~/features/<feature>/requirements.md` and `Planning~/features/<feature>/architecture.md`, one directory per feature
- Persisted plan: `Planning~/features/<feature>/plan-<id>.md`
- Architecture-doc body budget: 40 KB over `Planning~/features/<feature>/architecture.md`; the closed features' oversized docs, all written before the harness, are ruled in `memory/doctor-rulings.md`
- Plan-file budget: 10 KB over `Planning~/features/<feature>/plan-*.md`
- Brief budget: 15 KB over `.scratch/**/*brief*.md`
<!-- harness:binding:end process-docs -->

## Prerequisites

Confirm before proceeding:
- The project holds `.claude/harness.json`, and its `base` points at a base that exists.
- The project's own work is committed or known: this skill does not check in, and the apply lands on whatever branch the project is on.

## Workflow

1. **BINDINGS** — read the project's `vc` binding **and its snapshot slot** before anything else. Either absent → stop in the first line, name the binding interview, and run nothing. The slot `TODO` → stop. The slot `none` → state that no snapshot is possible and wait for the human's explicit word.
2. **LOCATE** — the project root: the walk-up the workspace pack's `add-pack` skill describes, an explicit root the human named, or a workspace root for `--all`. A session in this repository upgrades project zero by its root and runs `--all` over the workspace root.
3. **DRY RUN** — `harness.py upgrade --dry-run --root <project>`. The report is shown **whole**, never summarized.
4. **HARD STOP — the human's word** on the report, on the snapshot the skill will take or the statement that none is possible, on any single `--take-theirs`, on step 7's same-version recompose, where a fill there changes a section a composed skill splices, and, for `--all`, on which projects to apply, put to the human by `.claude/references/core/ruling-form.md`. Nothing is written before it. Where `doc-writer` is not composed and the report names keys to append or carries a `design docs:` or `path form:` line, it is said **here**, before the apply.
5. **SNAPSHOT** — run the slot's record, then its confirming read; show the snapshot's id. A failed confirm stops the skill before the apply.
6. **APPLY** — `harness.py upgrade --root <project>`, with at most one `--take-theirs` the human named at the stop. Then compare the applied report's body with the dry run's and **stop on a difference**: the run the human ruled on is the run that must have happened.
7. **BINDING INTERVIEW** — for each bindings file with appended keys, and for the bindings file answering `process-docs` where its filled *Brief budget* line names a pattern under `.claude/scratch`, file `.scratch/doc-writer-<slug>-brief.md` from `.claude/references/core/decision-brief.md` and dispatch `doc-writer` through the Agent tool without a `name`, as the workspace pack's `add-pack` skill does. That line's brief dictates it: its KB figure kept, the pattern's folder replaced by `.scratch`. One brief per bindings file — a file with appended keys and that line takes one brief carrying both; the dispatches run serially. The report's `design docs:` line is the third trigger: for the bindings file the `design docs:` line names, ask the human where this project's design docs live — the `process-docs` interview's design-docs question, `none` refused — then file that file's one brief from `.claude/references/core/decision-brief.md` and dispatch `doc-writer` as for the other triggers, also where the `design docs:` line is that file's only trigger; the brief carries the question asked and the human's answer, for `doc-writer` to write as the section's `- Design docs:` line, in place of the value that read `none` or added where a section answered slot by slot — one carrying the template's other slot lines — lacks the line, nothing else in the section rewritten. Any other `process-docs` section the `design docs:` line flags as lacking the line — a whole key answered `none` in any spelling (`- none`, `- None`, `- none — …`), a `none` line beside other bullets, prose with no slot lines — takes the whole `process-docs` interview in place of the design-docs question alone, `none` refused for *Design docs* only; the brief carries every question asked and every answer, and names as the form to match the template the filling pack declares for the key in the base, for `doc-writer` to rewrite the section from it — the heading, the comment line, one filled line per slot — nothing of the old section kept. A file the `design docs:` line names that also has appended keys or the old *Brief budget* line takes one brief carrying all of it. The report's `path form:` line is the fourth trigger: for the bindings file the `path form:` line names, ask the human for the `verification` binding's *Path form* slot, as that pack's template for the key asks it, then file that file's one brief from `.claude/references/core/decision-brief.md` and dispatch `doc-writer` as for the other triggers, also where the `path form:` line is that file's only trigger; the brief carries the question asked and the human's answer, for `doc-writer` to write as the section's `- Path form:` line — replacing the line read, or added to the section where it has none — nothing else in the section rewritten. A file the `path form:` line names that has other triggers takes one brief carrying all of it. Where a dispatch here filled or rewrote a section a composed file under `.claude/skills/` splices, those files read `V23` until recomposed: run `harness.py upgrade --dry-run --root <project>` again and show its report whole; where every file it would write is a `replaced:` file under `.claude/skills/`, beside the `manifest:` line, the step 4 word covers it, and `harness.py upgrade --root <project>` applies it, its body compared with this dry run's as in step 6; any other write → stop, and put it to the human by `.claude/references/core/ruling-form.md`.
8. **VERIFY** — `harness.py validate --root <project>`, **then** `harness.py doctor --root <project>`, in that order: `doctor` checks neither V3, V4 nor V10, which an upgraded composition can break.
9. **REPORT** — every outcome class the run produced, of which replaced, skipped, appended and adopted are only the common ones and a `not written: …` is never dropped, it being the class where a file was not written at all, and each orphan line by its label — `removed` (deleted, its ledger row dropped), `orphaned: locally modified` (edited, kept with its row), `dropped from the ledger` (its row dropped, the disk untouched) or `orphaned: unreadable` (not readable, kept with its row); the snapshot's id; the verification result; and, where the project is the session's own, that the session restarts to load what was written. Then what is left to the human, read by row path from the `SCRATCH` rows of step 8's `doctor`: `.scratch` not ignored → the ignore line for `.scratch/`, which this skill never writes; `.scratch` reading NOT CHECKED → that, with its reason, the human checking the ignore by their own system; the row whose message opens `an old scratch folder is present` → that folder, the human's to remove; `.claude/bindings` → the *Brief budget* line still under the old scratch folder, which step 7 missed. No `SCRATCH` row → nothing added. Each `orphaned: locally modified` path is left to the human as well: to remove it, or to compose the pack that now ships it.

For `--all`, steps 5 to 8 run once per project the human picked, serially, parents before their delta children.

**This skill does not check in.** An upgrade runs on whatever branch the project's work is on, and a check-in here would commit whatever else is uncommitted there; that work's own ceremony commits the upgrade with it. (`promote`, whose branch is its own, does check in.)

## Output

Report, in the PM's own voice: the project or projects upgraded, the snapshot id for each, one line per outcome class the run actually produced (commonly replaced, skipped, appended and adopted; any `not written: …` never dropped, it being the class where a file was not written at all; each orphan line by its label, `removed`, `orphaned: locally modified`, `dropped from the ledger` or `orphaned: unreadable`), the `validate` and `doctor` results, and what is left to the human — the check-in on the project's own branch, the restart where the project is the session's own, and each act step 9 read from the `SCRATCH` rows: the ignore line for `.scratch/` or the NOT CHECKED read with its reason, the old scratch folder to remove, and the *Brief budget* line still under it. Each `orphaned: locally modified` path joins what is left to the human: to remove it, or to compose the pack that now ships it.
