# g2-accept — the tree path

The order `g2-accept`'s `## Workflow` steps run in where `trees` is filled; "the contract" is `.claude/references/core/trees-contract.md`.

## The tree path

Where `trees` is filled, as the contract reads it, the steps run in this order; each item names the `## Workflow` steps it runs.

1. **ROOT** — `.claude/references/core/gate-close.md` `### ROOT`.
2. **PREFLIGHT through HARD STOP** — `## Workflow`'s PREFLIGHT, PRESENT, STAGE and HARD STOP, as written, in this tree; on the word, its `gate word at <head>` line, as `.claude/references/core/gate-close.md` `### CATCH-UP` **The word** says.
3. **RECORD and PRODUCT DOCS** — `## Workflow`'s RECORD and PRODUCT DOCS, as written, the PM checking their edits in on the branch, in this tree, per the `vc` binding's check-in procedure.
4. **CATCH-UP and TIER** — `.claude/references/core/gate-close.md` `### CATCH-UP`, with `<skill>` `g2-accept`, `<slug>` `<feature>` and the target this gate merges into, whether G2's review ran in this session or in an earlier one, since `review-cycle` runs no catch-up; then `.claude/references/core/gate-close.md` `### TIER`.
5. **SHOW** — `.claude/references/core/gate-close.md` `### SHOW`, its edits what was authored after the ruling — RECORD's edit and any product-doc changes, the fix's diff after "Accept, but fix X", and each option taken after the ruling with its option text.
6. **HUB READ** — `.claude/references/core/gate-close.md` `### HUB READ`.
7. **MERGE** — `.claude/references/core/gate-close.md` `### MERGE`. The G2 ruling is itself the word to merge.
8. **CLOSE OUT** — `## Workflow`'s CLOSE OUT, in the hub's `memory/`: the record `.claude/references/core/gate-close.md` `## The record` says, drafted and not written, since HANDOFF's call carries it.
9. **HANDOFF** — `## Workflow`'s HANDOFF, as written, CLOSE OUT's drafts named to `handoff`, whose one call carries them before its own note, as its `## The dated note` says; `handoff` names the hub as the tree the next session opens in, since this tree is removed after REMOVE, and writes no slot, as its `## Where it goes` says.
10. **KEPT BRIEF** — `.claude/references/core/gate-close.md` `## The kept brief`, in this tree, HANDOFF's call having landed the record.
11. **REMOVE** — `.claude/references/core/gate-close.md` `### REMOVE`, with `<lane>` `feature`.

## Bindings

- `vc` [Check-in procedure] (required) — the slot of that name, by which RECORD's and PRODUCT DOCS' edits are checked in on the branch.

<!-- harness:binding:start vc .claude/bindings/vc-git.md -->
- Auto-mode entries: excluded
- Check-in procedure: inline on the main thread, in the session's own tree, from its root — `git add -- <paths>`, then `git commit -F <message file> -- <paths>`, each one command, bare, the forms the pack's `Bash(git add:*)` and `Bash(git commit:*)` rules match whole: never a heredoc, a chain, a leading `cd` or the Hub slot's `git -C "<path>"`, save `implement`'s JOIN check-in of a writer tree, which runs in that tree by that idiom; never `git add -A` — a hub operation's `memory/` check-in by the base's `scripts/hub.py write --root D:\Workspace\VoXR-Speech-Recognition --main main`, which writes and checks in exactly the paths it edits under `memory/`, as one commit; then `git push -u origin <branch>` on a branch's first push and `git push` after; the commit sha is the receipt; every hub write — a `hub.py write` in the hub on `main` — is preceded by `git pull --no-rebase origin main` in the hub and followed by `git push` there, `hub.py write` neither pulling nor pushing; a failed pull or push is reported, never retried in another form, and the local write stands; a session's `memory/` commits in the hub while the hub is on that session's own branch are plain check-ins on that branch, outside the sync, `main` never pulled into it; pull requests still merge on GitHub, by the human
<!-- harness:binding:end vc -->
