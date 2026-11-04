---
type: system
topic: parallel-trees
tags: [trees, worktrees, parallelism, verification]
status: draft
updated: 2026-11-05
sources:
  - .claude/bindings/vc-git.md (Branch procedure :8; trees :19-20)
  - .claude/bindings/unity.md (verification Run from :5; Tree-safe :14)
  - memory/STATUS.md
  - "Session direction 2026-11-05 (G0)"
---

# Design: Parallel Trees

**Branch:** `design-parallel-trees`
**Origin:** the human's ask 2026-11-05 to turn parallelism on, after light-lane batch A merged (PR #186).
**Status:** In design — opened at G0 2026-11-05; not locked.

**Scope, agreed at G0 (2026-11-05):** decide how VoXR can run branches in their own git worktrees — so the `trees` binding can be filled — given VoXR TestGround consumes this one checkout by `file:` path and a batchmode test run takes its Editor lock. In scope are: which work may run in a separate tree and which must run in the checkout TestGround tests (docs, bindings and planning; the native harness; Unity tests); how the tested checkout is chosen; every value the `trees` binding needs (hub path, tree location and naming, how a tree is created, listed, checked clean, caught up with `main` and removed, writer pooling, release files); and the knock-on effects on the `verification` binding (`.claude/bindings/unity.md`), the vc binding (`.claude/bindings/vc-git.md`), the hub session (`voxr-hub`) and `memory/`.

> **What's decided going in.** The human (the maintainer) ruled on 2026-11-05. Before G0, asked how to start turning parallelism on, the human chose "Design topic first (Recommended)" over filling the binding in the light lane. The work runs on the **design track**; at G0 on 2026-11-05 the branch is `design-parallel-trees`, cut from `main` `3e4d84b`, and the scope above was agreed as proposed ("Agree (Recommended)"), over "Agree, narrower" (only work that never runs Unity in separate trees, Unity-tested work in the one checkout by design) and "Not now". No locked doc is amended at G0; the sites in In 4 are where the design will decide changes.
>
> **Non-goals:** filling the binding itself, a follow-up after G1 from this design's feature backlog; changes to the shared harness's core packs, whose gaps go to harness-hub; any change to how the package is published or consumed outside TestGround.

---

## 0. Scope (agreed at G0)

**Goal**

- Decide how VoXR can run branches in their own git worktrees — so the `trees` binding can be filled — given VoXR TestGround consumes this one checkout by `file:` path and a batchmode test run takes its Editor lock.

**In** (the design decides each)

1. Which work may run in a separate tree and which must run in the checkout TestGround tests — docs, bindings and planning (no Unity run); the native harness (Command 3 runs outside Unity); Unity tests (Commands 1, 2, 4).
2. How the tested checkout is chosen — one host project per tree; pointing TestGround's manifest at a tree per run; or one test checkout that branches take turns on.
3. Every value the `trees` binding needs: hub path, tree location and naming, and how a tree is created, listed, checked clean, caught up with `main` and removed; writer pooling; release files.
4. Knock-on effects: the `verification` binding's `Tree-safe: no` (`.claude/bindings/unity.md` :14) and its "the one checkout" Run-from line (:5); the vc binding's Branch procedure, which ends "runs in the one checkout, since `trees` is none" (`.claude/bindings/vc-git.md` :8), and its `trees` value and comment (:19-20); the hub session (`voxr-hub`) and `memory/`.

**Out**

- Filling the binding itself — a follow-up after G1, from this design's feature backlog.
- Changes to the shared harness's core packs — gaps go to harness-hub.
- Any change to how the package is published or consumed outside TestGround.

**Forks for the design** (named, not ruled at G0; each is put to the human)

- What runs in a separate tree vs the tested checkout. Not ruled.
- How the tested checkout is chosen: one host project per tree; pointing TestGround's manifest at a tree per run; or one test checkout that branches take turns on (In 2). Not ruled.
- Where trees live and how they are named. Not ruled.
- Whether writer trees are pooled. Not ruled.
- Which files are release files. Not ruled.

**Facts at G0** (verified 2026-11-05)

- `.claude/bindings/vc-git.md` :19 `trees` reads `none`; its :20 comment gives the reason.
- `.claude/bindings/vc-git.md` :8 Branch procedure ends "runs in the one checkout, since `trees` is none".
- `.claude/bindings/unity.md` :5 Run from names "the one checkout".
- `.claude/bindings/unity.md` :14 `Tree-safe: no`.
- `D:\Workspace\VoXR TestGround\Packages\manifest.json` :3 `"com.jinwoo1601.voxr": "file:D:/Workspace/VoXR-Speech-Recognition"`.
