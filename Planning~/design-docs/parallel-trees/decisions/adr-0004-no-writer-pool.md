---
id: adr-0004
title: Writer trees are cut fresh and removed after the join, with no pool, over a pooled, reset set
status: accepted
date: 2026-11-05
topic: parallel-trees
---

# adr-0004 — Writer trees are cut fresh and removed after the join, with no pool, over a pooled, reset set

## The fork

§0 of the design (`Planning~/design-docs/parallel-trees.md`, Forks for the design) left open whether writer trees are pooled (In 3). The `trees` binding's Pool slot needs the answer, and with it how a writer tree comes to exist and how it goes. The precedent is the harness repo's filled `trees` binding (`D:\Workspace\harness\.claude\bindings\vc-git.md` `## trees`), which reads `Pool: none`.

## Options considered

1. **(a) No pool.** `Pool: none`: a writer tree is cut fresh from the branch tip and removed after the join.
2. **(b) A pooled, reset set.** Writer trees are kept as a set and reset between uses.

## The decision

**Option (a): `Pool: none` — a writer tree, `<branch>--<unit>`, is cut fresh from the branch's tip and removed after the join.**

## Why the rejected branches lost

- **(b) A pooled, reset set** — it needs a reset procedure, and its stale state can hide a missing-file bug. What would have to change for it to win: a reset procedure that leaves no stale state behind, and a reason to keep writer trees between joins.

## Consequences

- The `trees` binding's Writer-branch naming reads `<branch>--<unit>`, cut from the branch's tip, and its Pool reads `none`; Remove is by `.claude/references/vc-git/tree-remove.py` (design intent, not yet built).
- Changed by the follow-up feature after G1, not on this branch: `.claude/bindings/vc-git.md` :19-20, the `trees` value and comment.

## Status

Accepted on 2026-11-05 by the human's ruling (AskUserQuestion), option (a).
