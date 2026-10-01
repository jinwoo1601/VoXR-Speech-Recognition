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

The key is looked up as `.claude/references/core/delegation-contract.md` `## Bindings` says: `.claude/bindings/<pack>.md` first, the project `CLAUDE.md` *Bindings* section as the pre-scaffold fallback — read from **the base**, not from the project, because every version-control act of this skill happens in the base.

- `vc` (required, the base's) — the branch procedure BRANCH follows, the check-in procedure CHECK-IN follows, and the base-revision reads that show the base's branch and working state at the stop. Absent → stop in the first line, name the binding interview, and create no branch.
- No agent: this skill dispatches nothing.

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
