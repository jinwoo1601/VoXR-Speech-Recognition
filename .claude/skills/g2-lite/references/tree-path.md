# g2-lite — the tree path

The order `g2-lite`'s `## Workflow` steps run in where `trees` is filled; "the contract" is `.claude/references/core/trees-contract.md`.

## The tree path

Where `trees` is filled, as the contract reads it, the steps run in this order; each item names the `## Workflow` steps it runs.

1. **ROOT** — `.claude/references/core/gate-close.md` `### ROOT`.
2. **LANE through HARD STOP** — `## Workflow`'s LANE, CLASS, PREFLIGHT, PRESENT, REVIEW, RESOLVE and HARD STOP, as written, in this tree. CLASS reads the hub's `memory/STATUS.md` line, by absolute path under the Hub path. PREFLIGHT's `budget` run, on prose and trivial, adds `--hub "<Hub path>"`. RESOLVE's `--status` and `--brief` are the hub's, by absolute path under the Hub path, and its word is one `scripts/hub.py write --root <hub> --message <file> --entry Current <branch> <file>` command. HARD STOP runs on a human class only; else CATCH-UP follows RESOLVE.
3. **CATCH-UP** — `.claude/references/core/gate-close.md` `### CATCH-UP`, with `<skill>` `g2-lite` and `<slug>` `<changeset>`. Where it resolved a conflict, changing the branch's own content, RESOLVE's read and its `close-class` run again at the head CATCH-UP leaves, before LIST, and that run's class is the one LIST and MERGE read: a human class is HARD STOP, as RESOLVE says.
4. **CHANGELOG** — the CHANGELOG line of `## Workflow`'s CLOSE OUT, the PM checking it in on the branch, in this tree, per the `vc` binding's check-in procedure, with CLOSE OUT's word on a falsified product doc where there is one. CLOSE OUT's `memory/` record waits for RECORD, after the merge.
5. **SHOW** — the relay of `## Workflow`'s MERGE: the CHANGELOG edit shown first, as a diff the human can read. Show, do not ask again. It comes before HUB READ, so that the read is the last act before the merge.
6. **LIST** — on a delegated or audited class only: a line under `<branch>` in the hub's `memory/STATUS.md` `## Auto-closed`, read by absolute path under the Hub path → HARD STOP, as a human class. Else one `scripts/hub.py write --root <hub> --message <file> --entry Auto-closed <branch> <file>` command, with `--main` as the contract says, run from this tree, its inputs drafted in `.scratch/g2-lite-<changeset>-list/`, the entry file holding `## Workflow`'s CLOSE OUT line in its `closing` form. A refusal printing a `correct:` line: correct it and re-run, as the contract says; any other refusal, the re-run cap spent, or a lock the command holds on its own failure: reported as the contract says, then HARD STOP, as a human class — nothing merged, and HUB READ runs on after the ruling. Where LIST landed, each later stop's report says that LIST's line stays and nothing merged: HUB READ's refused take, the hub off the main branch or not clean, a CATCH-UP conflict or red gate on HUB READ's re-run, a refused release before the merge, a failed merge.
7. **HUB READ** — `.claude/references/core/gate-close.md` `### HUB READ`.
8. **MERGE** — `.claude/references/core/gate-close.md` `### MERGE`, with `## Workflow`'s MERGE's word: the G2-lite ruling on a human class, the slot and RESOLVE's class otherwise.
9. **RECORD** — the `memory/` record of `## Workflow`'s CLOSE OUT, after the merge, in the hub's `memory/`: the record `.claude/references/core/gate-close.md` `## The record` says, one call, a batch's deferrals among the deferrals it closes, and LIST's line, where it landed, rewritten `merged <commit>`, the note naming the merge commit.
10. **REMOVE** — `.claude/references/core/gate-close.md` `### REMOVE`, with `<lane>` `light`.
