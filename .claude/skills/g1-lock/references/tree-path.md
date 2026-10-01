# g1-lock — the tree path

The order `g1-lock`'s `## Workflow` steps run in where `trees` is filled, and the binding sentences only that order uses; "the contract" is `.claude/references/core/delegation-contract.md`.

## Bindings

- `verification` (required only where CATCH-UP brings the target in) — the commands CATCH-UP's `compile-check` runs in the branch's tree; `none` is a filled value, and CATCH-UP records `nothing to run`. It is consulted nowhere else. Absent there → stop before the merge and say so.
- `trees` (optional) — the Hub path ROOT compares with this session's root and PREFLIGHT's `budget` run takes as `--hub`; the List read that confirms the branch's tree; the Catch-up procedure; and the Clean read and the Hub slot's idiom by which HUB READ, MERGE and each `memory/` write run in the hub.
- `session-launch` (optional) — the Hub session name, to which the tree path's REMOVE sends `FINISHED`.

## The tree path

Where `trees` is filled, as the contract reads it, the steps run in this order; each item names the `## Workflow` steps it runs.

1. **ROOT** — `.claude/references/core/gate-close.md` `### ROOT`.
2. **PREFLIGHT through HARD STOP** — `## Workflow`'s PREFLIGHT, PRESENT and HARD STOP, as written, in this tree.
3. **CATCH-UP** — `.claude/references/core/gate-close.md` `### CATCH-UP`, with `<skill>` `g1-lock` and `<slug>` `<topic>`.
4. **LOCK and BACKLOG** — `## Workflow`'s LOCK and BACKLOG, as written, the PM checking their edits in on the branch, in this tree, per the `vc` binding's check-in procedure.
5. **SHOW** — the relay of `## Workflow`'s MERGE: what was authored after the ruling — LOCK's and BACKLOG's edits and anything CATCH-UP brought in — shown as a diff the human can read. Show, do not ask again. It comes before HUB READ, so that the read is the last act before the merge.
6. **HUB READ** — `.claude/references/core/gate-close.md` `### HUB READ`.
7. **MERGE** — `.claude/references/core/gate-close.md` `### MERGE`. The G1 ruling is itself the word to merge.
8. **STATUS** — `## Workflow`'s STATUS, as written, in the hub's `memory/`: the `memory/STATUS.md` line and the session's dated note, each write one hub operation, as the trees contract (`.claude/references/core/trees-contract.md`) defines it.
9. **REMOVE** — `.claude/references/core/gate-close.md` `### REMOVE`, with `<lane>` `design`.

## The re-lock path

Where PREFLIGHT read a re-lock and `trees` is filled, the steps run as `## The tree path` orders them, save as these items say.

1. **ROOT** — as `## The tree path` has it.
2. **PREFLIGHT through HARD STOP** — as `## The tree path` has it.
3. **CATCH-UP** — `.claude/references/core/gate-close.md` `### CATCH-UP`, the feature branch in place of the main branch, in its read and in the Catch-up procedure, with `<skill>` `g1-lock` and `<slug>` `<topic>`.
4. **LOCK and BACKLOG** — as `## The tree path` has it.
5. **SHOW** — as `## The tree path` has it.
6. **HUB READ** — the hub's on-main and Clean reads skipped: the hub is not merged into. Then CATCH-UP's changed-file read again, against the feature branch: anything new outside `memory/` → CATCH-UP again, then this read again.
7. **MERGE** — the design branch into the feature branch by the re-lock merge-back the `vc` binding's *Branch procedure* names, in the tree that holds the feature branch: where the List read names no tree on the feature branch, this tree, the feature's own, switched back to it; otherwise the merge alone, run in the tree the List read names on the feature branch by the Hub slot's idiom. The G1 ruling is itself the word to merge.
8. **STATUS** — as `## The tree path` has it, in the hub.
9. **REMOVE** — only where MERGE ran in another tree, this tree being the design branch's own: `.claude/references/core/gate-close.md` `### REMOVE`, with `<lane>` `design` and `<commit>` the commit MERGE left the feature branch at. Otherwise nothing is removed, and the session resumes the feature in its own tree.
