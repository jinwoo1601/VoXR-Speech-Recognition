---
name: g1-lock
description: "Lock a design at G1 on the human's ruling and break it into a feature backlog. Use on 'lock the design', 'ready for G1', or when a design topic's forks are all ruled."
kind: orchestration
agents: [doc-writer, doc-extractor, compile-check]
stops: [the G1 ruling before the design is marked locked, a conflict or red re-verification at catch-up]
status: active
bindings: [process-docs, vc, verification, trees, session-launch]
---

# g1-lock

## What this is

G1 locks a design: a preflight establishes that every fork is ruled and nothing outside the process docs changed, the human rules, the lock and the backlog are recorded, the branch merges — "locked" is the human's word.

> The preflight prepares, the human locks. A design is not locked because it reads finished.

## Bindings

Each key is looked up as `.claude/references/core/delegation-contract.md` `## Bindings` says: `.claude/bindings/<pack>.md` first, the project `CLAUDE.md` *Bindings* section as the pre-scaffold fallback.

- `process-docs` (required) — where the design doc and the feature backlog live, and the area every changed file on this branch must lie in. Absent → stop and say so before any brief is filed.
- `vc` (required) — the base-revision reads that give PREFLIGHT's changed-file list, the *Branch procedure*, whose lab form Prerequisites refuses, and the merge procedure of MERGE. Absent → stop and say so.
- `verification` (required only where CATCH-UP brings the target in) — read only on the tree path, as `references/tree-path.md` `## Bindings` says. Absent there → stop before the merge and say so.
- `trees` (optional) — the slots `references/tree-path.md` `## Bindings` names. Absent → today's path. Filled → the steps run as `references/tree-path.md` orders them, and PREFLIGHT's `budget` run adds `--hub "<Hub path>"`.
- `session-launch` (optional) — the Hub session name, read by `references/tree-path.md`. Absent, or the Hub session name missing or `none` → REMOVE as today and no report sent.
- Agents — `doc-extractor` missing → PREFLIGHT's question list is answered by the PM's own read, said so. `doc-writer` missing → stop after the ruling and say so. `compile-check` missing → where CATCH-UP brings the target in, stop before the merge and say so.

## Prerequisites

Confirm before proceeding:
- The checkout is on the design branch per the `vc` binding's base-revision reads.
- The design doc exists where `process-docs` says.
- The branch is not in the lab form: the lab form the `vc` binding's *Branch procedure* names, or `lab-<topic>` where it names none (a `<…>` placeholder matches any text). A lab branch merges nothing → stop and say so before any brief is filed.

## Workflow

Where `trees` is filled, as the contract reads it, these steps run as `references/tree-path.md` orders them — on a re-lock, as its `## The re-lock path` orders them; otherwise they run as written.

1. **PREFLIGHT** — file `.scratch/g1-lock-<topic>-preflight-brief.md` from the common fields of `.claude/references/core/delegation-contract.md` (the design doc by absolute path) and dispatch `doc-extractor` through the Agent tool without a `name`, with the fixed question list: is every fork ruled? is every decision marked locked? are the open questions listed, each with an owner? are the feature candidates named? Then the PM reads the branch's line in `memory/STATUS.md` — the hub's where `trees` is filled: where it reads `re-lock of <feature-branch>`, the branch is a re-lock and the target is that feature branch; otherwise the target is the main branch. The target is reported at PRESENT. Then the PM reads, per the `vc` binding's base-revision reads, the changed-file list from the branch's merge base with the target: every changed file lies in the process-docs area the binding names — a file outside it is reported (the constitution: no product-doc edit on a design branch). Then the PM runs the base's `scripts/harness.py budget --root <project>`, the base being the `base` path in `.claude/harness.json`, relative to the project root, with `--hub "<Hub path>"` where `trees` is filled; G1 has no feature, so the check is STATUS only. With no manifest, the PM says the check could not run.
2. **PRESENT** — the answers verbatim, read from the full report `.scratch/g1-lock-<topic>-preflight-report.md` (the index carries one sentence per answer), filed as `.claude/references/core/delegation-contract.md` `## Filing` says of a recon agent's report — by core's report filer where it runs, otherwise by the PM from the agent's transcript, with the changed-file list and the `budget` lines, or, with no manifest, that the check could not run. A `budget: FAIL` is remediated and `budget` re-run before the HARD STOP is put; a refusal (`budget: refused — <reason>`) or no final line is shown and resolved before the HARD STOP in the same way, never read as a pass. Anything unruled goes back to the design conversation, not to the gate. PRESENT also shows each feature candidate with its proposed mark, in the form `.claude/references/core/feature-lite.md` `## The mark` gives — lite only where the locked design fixes every file-level choice, with the design passages that fix them and the numbered acceptance criteria — and says the G1 ruling covers the marks.
3. **HARD STOP — the G1 ruling.** Ask for the human's explicit lock with the preflight in view, putting the ruling by `.claude/references/core/ruling-form.md` and saying, as it is put, that the lock is also the word to merge. Silence is not a ruling.
4. **LOCK** — file `.scratch/doc-writer-<topic>-g1-brief.md` from `.claude/references/core/decision-brief.md`; `doc-writer` sets the design doc's status to locked and dates the lock, as the doc's form provides.
5. **BACKLOG** — a second decision brief, `.scratch/doc-writer-<topic>-g1-backlog-brief.md`; `doc-writer` writes the feature backlog — feature name and one-line scope per row — where `process-docs` says. Each row's Scope cell closes with the mark as ruled, `**Full.**` or `**Lite** (<adr>): no requirements doc; acceptance — (1) … (2) …`, `<adr>` the decision record the mark rests on, or the design section where the topic has none, as `.claude/references/core/feature-lite.md` `## The mark` says.
6. **MERGE** — the design branch into the target PREFLIGHT read: into the main branch per the `vc` binding's merge procedure; on a re-lock, into the feature branch by the re-lock merge-back the binding's *Branch procedure* names. **The G1 ruling is itself the word to merge**; there is no second stop. Relay first what was authored after the ruling — LOCK's and BACKLOG's edits — as a diff the human can read, then merge. Show, do not ask again.
7. **STATUS** — the PM's `memory/STATUS.md` line; the check-ins per the `vc` binding's check-in procedure are the PM's. The same act writes the session's dated note as `.claude/references/core/gate-close.md` `## The dated note` says.
