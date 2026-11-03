# g1-lock — the tree path

The order `g1-lock`'s `## Workflow` steps run in where `trees` is filled; "the contract" is `.claude/references/core/trees-contract.md`.

## The tree path

Where `trees` is filled, as the contract reads it, the steps run in this order; each item names the `## Workflow` steps it runs.

1. **ROOT** — `.claude/references/core/gate-close.md` `### ROOT`.
2. **PREFLIGHT through HARD STOP** — `.claude/references/core/gate-close.md` `### CATCH-UP`, with `<skill>` `g1-lock`, `<slug>` `<topic>` and the main branch the target; then `## Workflow`'s PREFLIGHT, PRESENT and HARD STOP, as written, in this tree.
3. **DELTA** — `.claude/references/core/gate-close.md` `### DELTA`, with `<skill>` `g1-lock`, `<slug>` `<topic>` and the main branch the target. A send-back runs item 2 again.
4. **LOCK and BACKLOG** — `## Workflow`'s LOCK and BACKLOG, as written, the PM checking their edits in on the branch, in this tree, per the `vc` binding's check-in procedure.
5. **SHOW** — the relay of `## Workflow`'s MERGE: what was authored after the ruling — LOCK's and BACKLOG's edits and anything DELTA brought in — shown as a diff the human can read. Show, do not ask again. It comes before HUB READ, so that the read is the last act before the merge.
6. **HUB READ** — `.claude/references/core/gate-close.md` `### HUB READ`.
7. **MERGE** — `.claude/references/core/gate-close.md` `### MERGE`. The G1 ruling is itself the word to merge.
8. **STATUS** — `## Workflow`'s STATUS, in the hub's `memory/`: the record `.claude/references/core/gate-close.md` `## The record` says, one call.
9. **KEPT BRIEF** — `.claude/references/core/gate-close.md` `## The kept brief`, in this tree, STATUS's call having landed.
10. **REMOVE** — `.claude/references/core/gate-close.md` `### REMOVE`, with `<lane>` `design`.

## The re-lock path

Where the branch's line in the hub's `memory/STATUS.md` reads `re-lock of <feature-branch>` and `trees` is filled, the steps run as `## The tree path` orders them, save as these items say.

1. **ROOT** — as `## The tree path` has it.
2. **PREFLIGHT through HARD STOP** — `.claude/references/core/gate-close.md` `### CATCH-UP`, the feature branch in place of the main branch, in its read and in the Catch-up procedure, with `<skill>` `g1-lock` and `<slug>` `<topic>`; then `## Workflow`'s PREFLIGHT, PRESENT and HARD STOP, as `## The tree path` has them.
3. **DELTA** — `.claude/references/core/gate-close.md` `### DELTA`, the feature branch in place of the main branch, with `<skill>` `g1-lock` and `<slug>` `<topic>`. A send-back runs item 2 again.
4. **LOCK and BACKLOG** — as `## The tree path` has it.
5. **SHOW** — as `## The tree path` has it.
6. **HUB READ** — the hub's on-main and Clean reads skipped, and no lock taken: the hub is not merged into. Then `### DELTA`'s read again, against the feature branch: anything new outside `memory/` → `### DELTA`, then, nothing sent back, this read again.
7. **MERGE** — the design branch into the feature branch by the re-lock merge-back the `vc` binding's *Branch procedure* names, in the tree that holds the feature branch: where the List read names no tree on the feature branch, this tree, the feature's own, switched back to it; otherwise the merge alone, run in the tree the List read names on the feature branch by the Hub slot's idiom. The G1 ruling is itself the word to merge.
8. **STATUS** — as `## The tree path` has it, in the hub.
9. **KEPT BRIEF** — as `## The tree path` has it: these items run only where ROOT found this session outside the hub, whose main branch stands checked out whichever branch MERGE left here.
10. **REMOVE** — only where MERGE ran in another tree, this tree being the design branch's own: `.claude/references/core/gate-close.md` `### REMOVE`, with `<lane>` `design` and `<commit>` the commit MERGE left the feature branch at. Otherwise nothing is removed, and the session resumes the feature in its own tree.
