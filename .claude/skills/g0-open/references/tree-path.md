# g0-open — the tree path

The order `g0-open`'s `## Workflow` steps run in where `trees` is filled, how several items start under one ruling, and the binding sentences only those use; "the contract" is `.claude/references/core/delegation-contract.md`.

## Bindings

- `trees` (optional) — the Hub, Location, Create procedure and List read slots `## The tree path` uses; the Hub path, under which SCOPE reads `memory/backlog-candidates.md` and `memory/notes-for-future-features.md` by absolute path; and the Hub slot's idiom by which a command runs in another tree.
- `session-launch` (optional) — the Hub session name, to which the branch run's SCOPE sends `FINISHED` for a declined item and its RE-CHECK sends `BLOCKED`; the List command, by which the active branches of `## The tree path` see the sessions at each tree; the launch and the brief are `handoff`'s, which LAUNCH invokes and which reads this key by its own `## Bindings`.

## The tree path

Where `trees` is filled, as the contract reads it, the work runs in two parts, in two sessions: the ordering session — the one holding the human's order, the hub as a rule — cuts the branch and its tree from the item's line and launches the branch's own session, and that session agrees the scope and records it. Each step names the `## Workflow` steps it runs, and CUT stands in for BRANCH wherever this skill names BRANCH. A run is the branch's when this session's project root is not the Hub path, as the trees contract (`.claude/references/core/trees-contract.md`) compares them, the List read names that root as a tree, and no block of the hub's `memory/STATUS.md` `## Current` names that tree's branch; any other run is the ordering session's.

The active branches, which INDEPENDENCE, RE-CHECK and `## Several items` read, are the blocks of the hub's `memory/STATUS.md` `## Current`, read by absolute path under the Hub path, and the trees the List read names, run in the hub by the trees contract's hub clause, the hub excepted, whose branch no block names and which are in progress, each judged on its backlog line: a tree whose branch no `## Current` block names is in progress — counted among the active branches, and never "merged and awaiting removal" — when a session is at it (the List command, a session being at a tree when its working directory, as the list shows it, is the tree's path or lies under it, compared as the trees contract compares paths, where the `session-launch` key reads filled) or its branch has a commit the main branch lacks (the `vc` binding's log read); where the key reads absent, so sessions cannot be seen, every such tree counts as active. Where the key reads filled, such a tree with no session at it and no commit of its own is not among them.

**In the ordering session:**

1. **INDEPENDENCE** — on the item's line, its backlog line or the design topic as the human names it: state for each active branch whether this item is independent of it — neither depends on the other, and their expected write sets do not overlap, `memory/` exempt. The human rules run concurrently or wait, put to the human by `.claude/references/core/ruling-form.md`. Wait → stop; nothing is made. No active branch → nothing to rule.
2. **CHECKS** — the section carries the Hub, Location, Create procedure and List read slots; the tree's path, as Location names it for this branch, does not exist; and the Create procedure's branch check finds no branch of this name. Any failing → stop and say which; nothing is made.
3. **CUT** — the Create procedure, run in the hub by the trees contract's hub clause, cuts the branch the `vc` binding's branch procedure names for this work and its tree at that path; then the List read must name the tree at its path, on its branch. It does not → stop and say what the List read shows.
4. **LAUNCH** — invoke `handoff` for the branch's launch brief: the target the new tree, by its path per the List read; the lane `design` for a design topic or `feature` for a feature; the next step `g0-open` in that tree, whose branch run starts at SCOPE. `handoff` writes the brief and launches the session by its `references/launch.md`, or gives the paste line. This session's part in the branch ends here.

**In the branch's own session**, launched or opened from the paste line:

5. **SCOPE** — `## Workflow`'s SCOPE, as written, on the item the brief's Objective names. Declined → nothing is written; where the `session-launch` binding reads filled with its Hub session name, as the contract reads it, say the report line, `FINISHED - <branch>; lane: <lane>; declined at G0, nothing written`, `<lane>` the brief's Lane, then send it by `SendMessage` to the Hub session name as this session's last act, since the hub may stop this session within seconds and removes the tree once its checks pass; otherwise nothing is sent, and the tree stays until `orient` proposes its removal. Either way this session does no further work.
6. **RE-CHECK** — once the scope is agreed: the active branches, this one excepted, each stated overlapping or not — their expected write sets against this branch's, judged on its agreed scope, `memory/` exempt. None overlaps → RECORD. One overlaps → where the Hub session name reads filled, say the report line, `BLOCKED - <branch>; lane: <lane>; write sets overlap <other branch>`, then send it by `SendMessage` to the Hub session name; either way, stop for the human, naming the other branch.
7. **RECORD** — `## Workflow`'s RECORD, with the brief naming `doc-writer`'s target in this tree; then the PM checks the record in on the branch, in this tree, per the `vc` binding's check-in procedure, run here as the binding writes it.
8. **STATUS** — `## Workflow`'s STATUS, with the branch's line added to the hub's `memory/STATUS.md` `## Current` and checked in as one hub operation, as the trees contract defines it. The branch's work goes on in this session.

## Several items

1. **SET** — the human names the items, or approves a set the PM proposes: the PM proposes at most three (provisional → the tuning pass), and the human may name more. Nothing is made before the set is named or approved, and nothing launches on the PM's judgement.
2. **INDEPENDENCE** — once for the set: each item, on its line, against the active branches as `## The tree path` reads them, and pair by pair within the set, each stated independent or not; then one ruling from the human on which items run and which wait, put to the human by `.claude/references/core/ruling-form.md`. None runs → stop; nothing is made.
3. **CHECKS, CUT and LAUNCH, per item** — for each item that runs, in turn, `## The tree path`'s CHECKS, CUT and LAUNCH, the next item's CHECKS only once this item's LAUNCH has returned. A CHECKS or CUT failure stops the loop: say the item, what failed, and what the loop has made so far — each earlier item's branch, tree and brief — and nothing more is made. A failed launch, `handoff` giving that item's paste line, is that item's outcome, its tree kept, and the loop goes on.

Each item's branch session then runs `## The tree path` from SCOPE.
