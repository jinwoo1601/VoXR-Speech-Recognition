---
id: adr-0001
title: Every branch runs in its own tree, over Unity-tested branches in the hub checkout or Unity-tested work run serially
status: accepted
date: 2026-11-05
topic: parallel-trees
---

# adr-0001 — Every branch runs in its own tree, over Unity-tested branches in the hub checkout or Unity-tested work run serially

## The fork

§0 of the design (`Planning~/design-docs/parallel-trees.md`, Forks for the design) left open what runs in a separate tree and what runs in the checkout TestGround tests (In 1): docs, bindings and planning, which run no Unity; the native harness, whose Command 3 runs outside Unity; and the Unity tests, Commands 1, 2 and 4. It had to be decided here because the `trees` binding cannot be filled while VoXR TestGround consumes this one checkout by `file:` path, and its constraint is the trees contract's: under a filled `trees`, the hub checkout stays on the main branch and holds `memory/`, and a session in a branch tree that finds the hub off the main branch stops before any `memory/` write (`.claude/references/core/trees-contract.md` :23; `.claude/rules/CONSTITUTION.md` *One writer per tree*: "The main checkout stays on the main branch and holds the project's memory"). How the tested checkout is chosen (adr-0002) is downstream of it.

## Options considered

1. **(a) Every branch in its own tree.** Design, light, feat and lab branches alike each run in a tree of their own, and the hub stays on `main`, holding `memory/`.
2. **(b) Only no-Unity work in trees.** Work that runs no Unity goes in trees; Unity-tested branches run in the hub checkout, one at a time.
3. **(c) Non-Unity work in trees, Unity-tested work serial.** Work that runs no Unity goes in trees; Unity-tested work runs one branch at a time, blocking new trees.

## The decision

**Option (a): every branch — design, light, feat and lab alike — runs in its own tree, and the hub stays on `main`, holding `memory/`.** Unity-tested branches run in trees too, so how TestGround tests a tree is adr-0002's.

## Why the rejected branches lost

- **(b) Only no-Unity work in trees** — the hub would be off main while a Unity-tested branch runs in it, and every tree's `memory/` writes would stop (`.claude/references/core/trees-contract.md` :23). What would have to change for it to win: the hub being able to leave main without stopping the trees' `memory/` writes.
- **(c) Unity-tested work serial, blocking new trees** — less parallel than (a), and it still needs the hub off main. What would have to change for it to win: the same change as for (b), and the parallelism it gives up not being wanted.

## Consequences

- Commands 1-2 test a tree, never the hub checkout; adr-0002 rules how (design intent, not yet built).
- The hub stays on `main` and holds `memory/`: `memory/` writes become hub operations (`scripts/hub.py write`), and the hub session `voxr-hub` takes on the hub roles of the trees contract (acts 4, 7, 10, 11, 13). Each is changed by the follow-up feature after G1, not on this branch.
- `.claude/bindings/vc-git.md` :8, the Branch procedure's tail "runs in the one checkout, since `trees` is none", and `.claude/bindings/unity.md` :5, whose Run from becomes the root of the tree the session runs in, change in the follow-up feature after G1.
- Two auto-memories that assume `trees` none go stale: `no-successor-after-merge` and `handoff-launch-hub-py-relative`.

## Status

Accepted on 2026-11-05 by the human's ruling (AskUserQuestion), option (a).
