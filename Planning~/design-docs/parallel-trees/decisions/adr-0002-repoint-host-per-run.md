---
id: adr-0002
title: TestGround's manifest is repointed at the tree under test per run, over one host project per tree or a fixed test checkout that branches take turns on
status: accepted
date: 2026-11-05
topic: parallel-trees
---

# adr-0002 — TestGround's manifest is repointed at the tree under test per run, over one host project per tree or a fixed test checkout that branches take turns on

## The fork

§0 of the design (`Planning~/design-docs/parallel-trees.md`, Forks for the design) left open how the tested checkout is chosen (In 2). It is the keystone: the `trees` binding reads `none` only because TestGround tests this one checkout by path and a batchmode run takes its Editor lock (`.claude/bindings/vc-git.md` :19-20), and with every branch in its own tree (adr-0001) this is the fork that removes that reason. `D:\Workspace\VoXR TestGround\Packages\manifest.json` :3 reads `"com.jinwoo1601.voxr": "file:D:/Workspace/VoXR-Speech-Recognition"`; TestGround is its own git repository on `main`, and its working tree already has uncommitted changes to tracked files. Commands 1-2 (`.claude/bindings/unity.md` :6-7) already edit that manifest, adding `"testables"` with the Editor closed (`Temp/UnityLockfile` absent), rename `Tests~` to `Tests` in the tested checkout, and revert both afterwards; both write their results XML and log to the absolute path `D:\Workspace\VoXR-Speech-Recognition\.scratch\test-results\`. Where each command runs, how a run takes the host, and the knock-on sites in the verification binding and the recipe are downstream of it.

## Options considered

1. **(a) Repoint TestGround's manifest per run.** For Commands 1-2, TestGround's `file:` path is pointed at the tree under test, inside the edit-and-restore step that already exists. A host lock makes runs take turns, and the restore is checked. Uncommitted work in the tree is what gets tested.
2. **(b) One host project per tree.** Each tree is tested by a host project of its own, so runs need not take turns on one host.
3. **(c) A fixed test checkout that branches take turns on.** TestGround keeps pointing at one checkout, and each branch takes its turn on it.

## The decision

**Option (a): for Commands 1-2, TestGround's `file:` path is pointed at the tree under test per run, inside the edit-and-restore step that already exists, under a host lock that makes runs take turns, with the restore checked; uncommitted work in the tree is what gets tested.** It commits the design to the bracket and its rules H1-H4 and H7 (the design doc, §1): the at-rest read, the wait and the stop for the human, green only with the run's own evidence that the package resolved from this tree's path, results per tree, and the host resting on the hub between runs. The lock's wait bound is provisional (→ the tuning pass), and the package re-import cost per repoint is not measured. The lock's form and location and the per-run manifest edit are the architecture's.

## Why the rejected branches lost

- **(b) One host project per tree** — it costs a 13G Library per tree (TestGround's `Library/`, `du -sh` 2026-11-05) and a full import per tree, and its copies drift from the human's TestGround. What would have to change for it to win: a host project's Library and import becoming cheap per tree, and its copies kept from drifting from the human's TestGround.
- **(c) A fixed test checkout** — it tests only committed work, and it still needs the same lock. What would have to change for it to win: testing only committed work being enough, and the per-run repoint's cost — a package re-import whenever the path changes, not yet measured — outweighing that loss.

## Consequences

- Commands 1-2 take turns on the one host; a run that finds the lock held waits up to a bound (provisional → the tuning pass) and then reports BLOCKED, and a run that finds the host not at rest stops for the human and never repairs it (design intent, not yet built).
- Each run costs a turn on the host lock and a package re-import when the path changes (cost not measured).
- Each tree writes its results and logs under its own `.scratch/test-results/`, never the hub's.
- Between runs the host rests on the hub, which is `main`, so when the human opens TestGround's Editor for device testing it shows main; testing a branch on device is an open item.
- Changed by the follow-up feature after G1, not on this branch: `.claude/bindings/unity.md` :5 (Run from: the root of the tree the session runs in), :6-7 (the results paths and the host repoint bracket) and :14 (Tree-safe: yes, under H1-H6); `Planning~/verification-recipe.md`, its `PkgRoot`, results paths and host bracket.
- Handed to the follow-up's architecture: the lock's form and location, and the per-run manifest edit.

## Status

Accepted on 2026-11-05 by the human's ruling (AskUserQuestion), option (a). This is the keystone of the design.
