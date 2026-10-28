---
name: promote
description: "Promote a local project file into a pack of the base; the merge and the version bump stay the human's. Use on 'promote this skill', 'this agent belongs in the base', or after doctor lists a local item worth keeping."
disable-model-invocation: true
kind: orchestration
agents: []
stops: [the word on the item and target before the promote branch, the merge and the version bump this skill never takes]
status: active
bindings: [vc]
---

# promote

## What this is

A file written locally inside a project has no way back into the base. `promote` is that way back: one run per file into one pack, on a branch of the base, reviewed by the human there with the harness's own workflow (the constitution's promotion rule).

> The verb copies bytes and edits one `provides` line. The branch, the check-in, and the human's word are this skill's; the merge and the version bump are nobody's but the human's.

This skill dispatches no agent. Every step runs in the PM's session.

## Bindings

The key is read from **the base**, not from the project, because every version-control act of this skill happens in the base.

- `vc` (required, the base's) — the branch procedure BRANCH follows, the check-in procedure CHECK-IN follows, and the base-revision reads that show the base's branch and working state at the stop. Absent → stop in the first line, name the binding interview, and create no branch.
- No agent: this skill dispatches nothing.

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

## Prerequisites

Confirm before proceeding:
- The project holds `.claude/harness.json`, and its `base` points at the base the item goes to.
- The base's working state is clean enough to branch, per its `vc` binding's reads; anything uncommitted there is shown at the stop.

## Workflow

1. **BINDINGS** — read the base's `vc` binding first, before anything else. Absent → stop.
2. **LOCATE** — the project root: the session's own directory, the walk-up, or the root the human named; then the base, from the manifest's `base`.
3. **CANDIDATE** — `harness.py doctor --root <project>` names the local items in its `LOCAL` rows. Pick one item with the human, list **its files** (a skill directory is several files, promoted one run per file, onto one branch), and settle the target: `--to <pack>` for an existing pack, or `--new-pack <name> --kind <kind>` for a new one, plus the branch slug.
4. **HARD STOP — the human's word** on the item, its files, the target pack and the slug, with the base's branch and working state shown, put to the human by `.claude/references/core/ruling-form.md`. Nothing is created before it. A file's bytes travel as they are: show the file, so a machine path or a project-specific line written into it is caught here.
5. **BRANCH** — `promote-<slug>` in the base, per the base's `vc` branch procedure.
6. **PROMOTE** — `harness.py promote <path> --root <project>` with the target flags, once per file. Every refusal is printed before the first byte; a refusal stops the run and is reported as it stands — never worked around.
7. **VALIDATE** — `harness.py validate --root <base>`. Green is the last line `validate: PASS` and exit 0. A failure stops the skill before the check-in and is reported with the rows, the branch left in place for the human.
8. **CHECK-IN** — the base's `vc` check-in procedure, on the promote branch, only when VALIDATE passed: the promoted files are recorded on the promote branch before the base does anything else, because what becomes of a pending change when the base leaves the branch is the version-control system's behaviour, never this skill's to assume.
9. **STOP — the merge and the version bump are the human's.** Report the branch, the files promoted, the `provides` lines edited, and the pack stub where `--new-pack` wrote one; say that the pack's version is unbumped and the branch unmerged, and that the originating project adopts its copy on the next `add` or `upgrade`.

## Output

Report, in the PM's own voice: the branch name, one line per promoted file (`<source> -> <destination>`), the `provides` members edited, the `validate` result, the check-in, and the two things left to the human — the merge and the version bump.
