---
name: upgrade
description: "Bring a project's composed files up to its base's current pack versions, behind a dry run and the human's stop. Use on 'upgrade this project', 'the base has moved', or after doctor reports a project behind its packs."
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

The key is looked up as `.claude/references/core/delegation-contract.md` `## Bindings` says: `.claude/bindings/<pack>.md` first, the project `CLAUDE.md` *Bindings* section as the pre-scaffold fallback — read from **the project being upgraded**, not from the base, because the snapshot is taken of that project's working tree.

- `vc` (required, the project's) — the snapshot procedure SNAPSHOT follows: the record, the read that confirms it, and how the human restores it. The binding absent, or present without its snapshot slot → stop in the first line, name the binding interview, and run nothing. The slot still `TODO` → stop the same way: an unanswered slot is no snapshot.
- The slot answering `none` → state that no snapshot is possible and wait for the human's explicit word that the upgrade applies without one.
- `process-docs` (optional, the project's) — the bindings file answering it, whose *Brief budget* line step 7 reads. The binding absent, or its *Brief budget* slot not filled → step 7's second trigger does not fire, and step 8's `doctor` is the backstop.
- `doc-writer` not composed in this project while the report names keys to append, or while the *Brief budget* line needs step 7's update → say so at the stop, before the apply, so the human rules on an upgrade whose binding interview has no writer.

## Prerequisites

Confirm before proceeding:
- The project holds `.claude/harness.json`, and its `base` points at a base that exists.
- The project's own work is committed or known: this skill does not check in, and the apply lands on whatever branch the project is on.

## Workflow

1. **BINDINGS** — read the project's `vc` binding **and its snapshot slot** before anything else. Either absent → stop in the first line, name the binding interview, and run nothing. The slot `TODO` → stop. The slot `none` → state that no snapshot is possible and wait for the human's explicit word.
2. **LOCATE** — the project root: the walk-up the workspace pack's `add-pack` skill describes, an explicit root the human named, or a workspace root for `--all`. A session in this repository upgrades project zero by its root and runs `--all` over the workspace root.
3. **DRY RUN** — `harness.py upgrade --dry-run --root <project>`. The report is shown **whole**, never summarized.
4. **HARD STOP — the human's word** on the report, on the snapshot the skill will take or the statement that none is possible, on any single `--take-theirs`, and, for `--all`, on which projects to apply, put to the human by `.claude/references/core/ruling-form.md`. Nothing is written before it. Where `doc-writer` is not composed and the report names keys to append, it is said **here**, before the apply.
5. **SNAPSHOT** — run the slot's record, then its confirming read; show the snapshot's id. A failed confirm stops the skill before the apply.
6. **APPLY** — `harness.py upgrade --root <project>`, with at most one `--take-theirs` the human named at the stop. Then compare the applied report's body with the dry run's and **stop on a difference**: the run the human ruled on is the run that must have happened.
7. **BINDING INTERVIEW** — for each bindings file with appended keys, and for the bindings file answering `process-docs` where its filled *Brief budget* line names a pattern under `.claude/scratch`, file `.scratch/doc-writer-<slug>-brief.md` from `.claude/references/core/decision-brief.md` and dispatch `doc-writer` through the Agent tool without a `name`, as the workspace pack's `add-pack` skill does. That line's brief dictates it: its KB figure kept, the pattern's folder replaced by `.scratch`. One brief per bindings file — a file with appended keys and that line takes one brief carrying both; the dispatches run serially.
8. **VERIFY** — `harness.py validate --root <project>`, **then** `harness.py doctor --root <project>`, in that order: `doctor` checks neither V3, V4 nor V10, which an upgraded composition can break.
9. **REPORT** — every outcome class the run produced, of which replaced, skipped, appended and adopted are only the common ones and a `not written: …` is never dropped, it being the class where a file was not written at all; the snapshot's id; the verification result; and, where the project is the session's own, that the session restarts to load what was written. Then what is left to the human, read by row path from the `SCRATCH` rows of step 8's `doctor`: `.scratch` not ignored → the ignore line for `.scratch/`, which this skill never writes; `.scratch` reading NOT CHECKED → that, with its reason, the human checking the ignore by their own system; the row whose message opens `an old scratch folder is present` → that folder, the human's to remove; `.claude/bindings` → the *Brief budget* line still under the old scratch folder, which step 7 missed. No `SCRATCH` row → nothing added.

For `--all`, steps 5 to 8 run once per project the human picked, serially, parents before their delta children.

**This skill does not check in.** An upgrade runs on whatever branch the project's work is on, and a check-in here would commit whatever else is uncommitted there; that work's own ceremony commits the upgrade with it. (`promote`, whose branch is its own, does check in.)

## Output

Report, in the PM's own voice: the project or projects upgraded, the snapshot id for each, one line per outcome class the run actually produced (commonly replaced, skipped, appended and adopted; any `not written: …` never dropped, it being the class where a file was not written at all), the `validate` and `doctor` results, and what is left to the human — the check-in on the project's own branch, the restart where the project is the session's own, and each act step 9 read from the `SCRATCH` rows: the ignore line for `.scratch/` or the NOT CHECKED read with its reason, the old scratch folder to remove, and the *Brief budget* line still under it.
