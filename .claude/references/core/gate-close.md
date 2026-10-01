# Gate close

The close steps the three gates share word for word — `g1-lock`, `g2-accept` and `g2-lite` — each loaded where the calling skill names it: the session's dated note, and the tree-path steps. In these blocks, `## Workflow` and "the items below" are the calling skill's, and "the contract" is `.claude/references/core/delegation-contract.md`.

## The dated note

`memory/<YYYY-MM-DD>.md` for today's date, created when absent: re-read it, since concurrent sessions share it, then append one entry at its end and edit nothing above — an H1 naming the date, the branch and this step, then `## Summary`, `## Changes`, `## Decisions` and `## Open issues` in that order, none dropped, an empty one saying so in one line. The closed branch's block moves out of `memory/STATUS.md`'s `## Current` into the entry's `## Changes`, whole and verbatim, opened by one line naming where it came from and this step; gate evidence — commit shas, test counts, exit codes, live-run transcripts — and authorship or provenance confessions go under `## Changes`, and the gate ruling and review-cycle records — angle counts, findings, verdicts, rulings — under `## Decisions`; deferred items and named limitations go to STATUS `## Deferred`, one line each in the entry form, `**<ID>** · <VERDICT> · <route> · v<date> — <title>`, their full text under the entry's `## Open issues`; a deferral the work closes leaves `## Deferred` for the entry's `## Changes`, marked CLOSED and naming this step; backlog candidates go to `memory/backlog-candidates.md`, lessons for future features to `memory/notes-for-future-features.md`, each file appended to and created where absent. Where no branch stays active, `## Current` keeps one headline for the main branch naming the next action. A project root with no `memory/` at all: say so and write no note, inventing no other location.

## Tree-path steps

### ROOT

compare this session's project root with the Hub path, as the trees contract (`.claude/references/core/trees-contract.md`) compares them. Equal → the session is in the hub, on a branch with no tree of its own: the `vc` binding's current-branch read must show the hub on the branch under close, or stop and say so, since a branch with a tree of its own closes in that tree; then `## Workflow` runs as written, merge and all, with nothing to remove and none of the items below, and its `memory/` writes follow the trees contract — as today while the hub is on this branch, under its exception for a hub session on its own branch, and as hub operations once MERGE has returned the hub to the main branch. Not equal → the List read must name this root as a tree on the branch under close, or stop and say which tree the session is in; the items below then run in this tree.

### CATCH-UP

first after the ruling: the `vc` binding's changed-file read from the branch's merge base with the main branch to the main branch's tip, with every `memory/` path set aside. Nothing left → no catch-up. Otherwise the Catch-up procedure runs in this tree; file `.scratch/<skill>-<slug>-catch-up-brief.md` from the common fields of `.claude/references/core/delegation-contract.md`, the `verification` binding quoted, and dispatch `compile-check` through the Agent tool without a `name`, so that its commands run in this tree; then show the human the changed-file list from the ruled head to the branch's new head. A conflict or a red gate stops for the human, and nothing merges. `<skill>` is the calling skill's name and `<slug>` its topic, feature or changeset.

### HUB READ

the hub on the main branch, by the `vc` binding's current-branch read, and clean, by the Clean read, both run in the hub by the Hub slot's idiom; either failing stops and says which, and nothing merges. Then CATCH-UP's changed-file read again: anything new outside `memory/` → CATCH-UP again, then this read again.

### MERGE

the `vc` binding's merge procedure, the branch into the main branch, run in the hub by the Hub slot's idiom, with nothing else run in the hub since HUB READ.

### REMOVE

where the `session-launch` binding reads filled with its Hub session name, as the contract reads it: tell the human this tree's path, that its branch is merged, and that the hub removes the tree on this session's report once its checks pass, or keeps it and says which check failed; say the report line, `FINISHED - <branch>; lane: <lane>; merged <commit>`, `<commit>` the commit MERGE left the main branch at; then send it by `SendMessage` to the Hub session name, as this session's last act — nothing runs after it, since the hub may stop this session within seconds. Otherwise, as today: tell the human this tree's path, that its branch is merged, and that the tree is removed once this session is closed; the removal runs later from the hub, on `orient`'s report. Either way, this session removes nothing. `<lane>` is the lane the calling skill names.
