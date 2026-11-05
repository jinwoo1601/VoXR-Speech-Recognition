# g2-accept — the tree path

The order `g2-accept`'s `## Workflow` steps run in where `trees` is filled; "the contract" is `.claude/references/core/trees-contract.md`.

## The tree path

Where `trees` is filled, as the contract reads it, the steps run in this order; each item names the `## Workflow` steps it runs.

1. **ROOT** — `.claude/references/core/gate-close.md` `### ROOT`.
2. **PREFLIGHT through HARD STOP** — `.claude/references/core/gate-close.md` `### CATCH-UP`, with `<skill>` `g2-accept`, `<slug>` `<feature>` and the target this gate merges into; then `## Workflow`'s PREFLIGHT, PRESENT, STAGE and HARD STOP, as written, in this tree.
3. **DELTA** — `.claude/references/core/gate-close.md` `### DELTA`, with `<skill>` `g2-accept`, `<slug>` `<feature>` and that target. A send-back runs item 2 again.
4. **RECORD and PRODUCT DOCS** — `## Workflow`'s RECORD and PRODUCT DOCS, as written, the PM checking their edits in on the branch, in this tree, per the `vc` binding's check-in procedure.
5. **SHOW** — the relay of `## Workflow`'s MERGE: what was authored after the ruling — RECORD's edit and any product-doc changes, the fix's diff after "Accept, but fix X" or the human-only re-run, and each option taken after the ruling with its option text — shown as a diff the human can read. Show, do not ask again. It comes before HUB READ, so that the read is the last act before the merge.
6. **HUB READ** — `.claude/references/core/gate-close.md` `### HUB READ`.
7. **MERGE** — `.claude/references/core/gate-close.md` `### MERGE`. The G2 ruling is itself the word to merge.
8. **CLOSE OUT** — `## Workflow`'s CLOSE OUT, in the hub's `memory/`: the record `.claude/references/core/gate-close.md` `## The record` says, drafted and not written, since HANDOFF's call carries it.
9. **HANDOFF** — `## Workflow`'s HANDOFF, as written, CLOSE OUT's drafts named to `handoff`, whose one call carries them before its own note, as its `## The dated note` says; `handoff` names the hub as the tree the next session opens in, since this tree is removed after REMOVE, and writes no slot, as its `## Where it goes` says.
10. **KEPT BRIEF** — `.claude/references/core/gate-close.md` `## The kept brief`, in this tree, HANDOFF's call having landed the record.
11. **REMOVE** — `.claude/references/core/gate-close.md` `### REMOVE`, with `<lane>` `feature`.

## Bindings

- `vc` [Check-in procedure] (required) — the slot of that name, by which RECORD's and PRODUCT DOCS' edits are checked in on the branch.

<!-- harness:binding:start vc .claude/bindings/vc-git.md -->
- Check-in procedure: inline on the main thread — `git add -- <paths>`, then `git commit -F <message file> -- <paths>`, each one command, never a heredoc or a chain, never `git add -A` — a hub operation's `memory/` check-in by the base's `scripts/hub.py write --root D:\Workspace\VoXR-Speech-Recognition --main main`, which writes and checks in exactly the paths it edits under `memory/`, as one commit; then `git push -u origin <branch>` on a branch's first push and `git push` after; the commit sha is the receipt; every hub write — a `hub.py write` in the hub on `main` — is preceded by `git pull --no-rebase origin main` in the hub and followed by `git push` there, `hub.py write` neither pulling nor pushing; a failed pull or push is reported, never retried in another form, and the local write stands; a session's `memory/` commits in the hub while the hub is on that session's own branch are plain check-ins on that branch, outside the sync, `main` never pulled into it; pull requests still merge on GitHub, by the human
<!-- harness:binding:end vc -->
