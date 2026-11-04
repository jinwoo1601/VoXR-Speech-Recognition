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
  - parallel-trees/decisions/adr-0001-every-branch-in-a-tree.md
  - parallel-trees/decisions/adr-0002-repoint-host-per-run.md
  - parallel-trees/decisions/adr-0003-sibling-trees-directory.md
  - parallel-trees/decisions/adr-0004-no-writer-pool.md
  - parallel-trees/decisions/adr-0005-release-files.md
  - .claude/references/core/trees-contract.md
  - "Session direction 2026-11-05 (forks a-e ruled)"
---

# Design: Parallel Trees

**Branch:** `design-parallel-trees`
**Origin:** the human's ask 2026-11-05 to turn parallelism on, after light-lane batch A merged (PR #186).
**Status:** **G1 LOCKED 2026-11-05.** Rulings in adr-0001 through adr-0005. This document is immutable from here; a contradiction found during implementation reopens it on a new design branch and re-locks.
**Amended 2026-11-05** (re-lock of `feat-host-repoint`, branch `design-parallel-trees-at-rest`): at rest keeps `testables` as TestGround's committed manifest has them (`Packages/manifest.json` :52-54), so the bracket no longer adds or removes it — §1's host row and H1. Unlocked for this amendment; re-locked at G1 2026-11-05 by the human's ruling "Lock as amended (Recommended)".

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

- What runs in a separate tree vs the tested checkout. Ruled 2026-11-05 — adr-0001.
- How the tested checkout is chosen: one host project per tree; pointing TestGround's manifest at a tree per run; or one test checkout that branches take turns on (In 2). Ruled 2026-11-05 — adr-0002.
- Where trees live and how they are named. Ruled 2026-11-05 — adr-0003.
- Whether writer trees are pooled. Ruled 2026-11-05 — adr-0004.
- Which files are release files. Ruled 2026-11-05 — adr-0005.

**Facts at G0** (verified 2026-11-05)

- `.claude/bindings/vc-git.md` :19 `trees` reads `none`; its :20 comment gives the reason.
- `.claude/bindings/vc-git.md` :8 Branch procedure ends "runs in the one checkout, since `trees` is none".
- `.claude/bindings/unity.md` :5 Run from names "the one checkout".
- `.claude/bindings/unity.md` :14 `Tree-safe: no`.
- `D:\Workspace\VoXR TestGround\Packages\manifest.json` :3 `"com.jinwoo1601.voxr": "file:D:/Workspace/VoXR-Speech-Recognition"`.

This design is the work-placement and host-sharing model for every branch VoXR runs: which tree a branch works in, and how the one Unity host, VoXR TestGround, is shared between trees. It carries the human's ask of 2026-11-05, "You have parallelism turned off, correct? Let's turn it on when this is over", and the constitution's provision that branches neither of which depends on the other may run concurrently, each in its own tree (`.claude/rules/CONSTITUTION.md`, *Branching* and *One writer per tree*). Its keystone is adr-0002, TestGround repointed per run. The governing constraint is that there is one host with one Editor lock, which consumes a checkout by `file:` path, so Unity runs from any number of trees must take turns on it.

> **What's decided.** Every branch — design, light, feat and lab alike — runs in its own tree under `D:\Workspace\VoXR-Speech-Recognition.trees\`, writer trees cut fresh as `<branch>--<unit>` with no pool, while the hub stays on `main` and holds `memory/` (adr-0001, adr-0003, adr-0004); the release files are `CHANGELOG.md` and `.claude/` (adr-0005). For Commands 1-2, TestGround's manifest is repointed at the tree under test per run, inside the edit-and-restore step that already exists, under a host lock that makes runs take turns and with the restore checked (adr-0002, the keystone). All numbers on this page are **provisional → the tuning pass** (the design intent is the load-bearing part).

## 1. The model

One host, TestGround, serves every tree in turn. A Commands 1-2 run is one bracket that takes the host lock, points the host at the tree under test, runs, restores the host to rest and releases the lock.

| State | At rest | During a Commands 1-2 run (design intent, not yet built) |
|---|---|---|
| The hub | On `main`, holding `memory/` | Unchanged |
| Branch trees and writer trees | Zero or more | The tree under test has `Tests~` renamed for the run, reverted after |
| The host, TestGround | Its manifest `file:` points at the hub, `testables` as its committed manifest has them | Its manifest `file:` points at the tree's root, `testables` unchanged |
| The host lock | Free | Held by that one tree's run |

| Work | Where it runs (design intent, not yet built) |
|---|---|
| Docs, design, bindings and planning | Its branch's tree; it needs no host |
| Commands 1-2 | The tree under test, by the H1 bracket, under the host lock |
| Command 4 | Any tree, `PkgRoot` that tree's root, without the lock (H5, PM default) |
| Command 3 | A branch tree whose `NativeBridge~/vendor/` is provisioned, after the join; never a writer tree (H6, PM default) |

**Rules:**

1. **H1 — the bracket.** Commands 1-2 run as one bracket: take the host lock; read that the host is at rest (`file:` is the hub, `testables` as the host's committed manifest has them, no `Temp/UnityLockfile`); point `file:` at the tree's root; rename `Tests~` in that tree; run; revert the rename and restore the manifest to rest; release the lock (design intent, not yet built).
2. **H2 — wait, then stop.** A run that finds the lock held waits up to a bound (provisional → the tuning pass) and then reports BLOCKED. A run that finds the lock free but the host not at rest stops for the human, because a crashed run may have left it there; it never repairs the host on its own (design intent, not yet built).
3. **H3 — green needs evidence.** Green for Commands 1-2 also requires the run's own evidence that the package resolved from this tree's path, and the host back at rest after the restore; a results XML from a host pointed elsewhere is never green (design intent, not yet built).
4. **H4 — results per tree.** Each tree writes its results and logs under its own `.scratch/test-results/`, never the hub's, so concurrent trees never read one another's XML (design intent, not yet built).
5. **H5 — Command 4 in any tree (PM default).** Command 4 runs in any tree, with `PkgRoot` set to that tree's root; it reads the host's `Library/ScriptAssemblies` without the lock, because it writes nothing to the host. This rule is the PM's default, not the human's ruling (design intent, not yet built).
6. **H6 — Command 3 in a provisioned branch tree (PM default).** Command 3 runs only in a branch tree whose `NativeBridge~/vendor/` is provisioned so that the run's writes, under `vendor/overlaid/`, stay in that tree; it never runs in a writer tree, and runs in the branch tree after the join. Whether the pinned inputs are copied or shared is the architecture's call. This rule is the PM's default, not the human's ruling (design intent, not yet built).
7. **H7 — the host rests on the hub.** Between runs the host rests on the hub, which is `main`, so when the human opens TestGround's Editor for device testing it shows main; testing a branch on device is an open item (design intent, not yet built).

**`trees` values** (to fill in the follow-up; design intent, not yet built):

- Hub: `D:\Workspace\VoXR-Speech-Recognition`, on `main`, holding `memory/`; a command runs in another tree by `git -C "<path>" <command>`.
- Location: `D:\Workspace\VoXR-Speech-Recognition.trees\<branch>`, and writer trees as `<branch>--<unit>` (adr-0003).
- Create, List, Clean, Catch-up and Remove: the vc-git pack's procedures, as in the harness repo's filled binding (`D:\Workspace\harness\.claude\bindings\vc-git.md` `## trees`); Remove is by `.claude/references/vc-git/tree-remove.py`, and Catch-up merges `main` with `merge-attributes.txt`.
- Writer-branch naming: `<branch>--<unit>`, cut from the branch's tip. Pool: none (adr-0004).
- Release files: `CHANGELOG.md`, keeping both sides' `## [Unreleased]` entries, this branch's first; and `.claude/`, including `.claude/harness.json` but not `.claude/bindings/*.md`, regenerated by `upgrade` and never merged by hand (adr-0005).

## 2. The decisions it puts to its operator

- **Run Commands 1-2 now, or batch them.** Each run costs a turn on the host lock and a package re-import when the path changes (cost not measured → the tuning pass). The choice trades feedback speed against the wait it puts on other trees. Targeted runs (`-testFilter`) shorten a turn.
- **Which branches to open concurrently.** A Unity-heavy branch competes for the host lock, while docs, design and light prose work never touches it. This is act 1 in the trees contract (`.claude/references/core/trees-contract.md` `## Who does what`), the human's call: the human rules, and the hub session asks.

## 3. Dynamics & failure modes

Most runs never contend: design, docs and binding work need no host, and Command 4 needs no lock. Contention comes only from feature branches that are in their verification step at the same moment. Each degenerate case below names the rule that kills it.

- **1. A crashed run leaves the host pointed at a tree.** H2's at-rest read stops the next run for the human, so it never tests the wrong code silently.
- **2. Wrong-tree green.** The XML comes from another tree's run or a stale file; H3 and H4 kill it.
- **3. Starvation behind long PlayMode runs.** H2's wait bound turns it into a visible BLOCKED; targeted runs shorten turns.
- **4. The human opens the Editor mid-bracket.** `Temp/UnityLockfile` is part of the at-rest read, and a run never starts over an open Editor; this is unchanged from today's Commands 1-2.
- **5. Concurrent Command 3 runs overwrite `vendor/overlaid/`.** H6 kills it.

## 4. Boundaries & couplings

- **Owns:** what runs where, how the host is chosen and taken in turn, and the `trees` values ruled above.
- **Couples to:** the core trees contract (`.claude/references/core/trees-contract.md`: hub operations, roles, catch-up), the vc-git pack's procedures, and TestGround as host.
- **Hands off to the follow-up feature:** filling the bindings.
- **Hands off to its architecture:** the lock's form and location, how `NativeBridge~/vendor/` is provisioned, and the per-run manifest edit.
- **Does not decide:**
  - TestGround's own `trees`, which stays `none`.
  - Publishing or consumption outside TestGround.
  - Core-pack changes, which go to harness-hub.
  - Device testing of a branch.
- **Knock-on sites** (each changed by the follow-up feature after G1, not on this branch):
  - `.claude/bindings/unity.md` :5, Run from: the root of the tree the session runs in.
  - `.claude/bindings/unity.md` :6-7, the results paths and the host repoint bracket.
  - `.claude/bindings/unity.md` :14, Tree-safe: yes, under H1-H6.
  - `.claude/bindings/vc-git.md` :8, the Branch procedure's tail "runs in the one checkout, since `trees` is none".
  - `.claude/bindings/vc-git.md` :19-20, the `trees` value and comment.
  - `.claude/bindings/core.md` `## session-launch`, the Launch command's `<tree>` note "(`trees` is none)".
  - `Planning~/verification-recipe.md` :177-178, where Command 4's generated csproj names `PkgRoot`; and the recipe's results paths and host bracket.
  - The hub session `voxr-hub`, which takes on the hub roles of the trees contract (acts 4, 7, 10, 11, 13).
  - `memory/` writes, which become hub operations (`scripts/hub.py write`).
  - Two auto-memories that assume `trees` none and go stale: `no-successor-after-merge` and `handoff-launch-hub-py-relative`.

## 5. What it delivers

The human's ask on 2026-11-05 was: "You have parallelism turned off, correct? Let's turn it on when this is over". The `trees` binding reads `none` only because TestGround tests this one checkout by path and a batchmode run takes its Editor lock (`.claude/bindings/vc-git.md` :19-20). This design removes that reason: TestGround tests whichever tree a run points it at, one run at a time under the host lock, so the binding can be filled.

When it goes right, every branch runs in its own tree while the hub stays on `main` holding `memory/`; work that needs no host never waits on one, Commands 1-2 take turns on TestGround and are green only on evidence from their own tree, and between runs TestGround rests on `main` (design intent, not yet built).

## 6. Feature backlog — locked at G1 (2026-11-05)

Two features, in dependency order. `host-repoint` is **Full**: the design leaves the host lock's form and location, how `NativeBridge~/vendor/` is provisioned and the per-run manifest edit to its architecture doc. `trees-fill` is **Lite**: the design fixes every value it writes (§1 `trees` values, §4 knock-on sites).

| Feature | Scope | Depends on |
|---|---|---|
| `host-repoint` | The per-run host bracket of §1 (H1-H6) in the verification binding's Commands 1-4 (`.claude/bindings/unity.md` :6-9) and `Planning~/verification-recipe.md`: the host lock, the at-rest read, the repoint of TestGround's `file:` path to the tree under test and its restore, green on the run's own evidence, results and logs under the tree's own `.scratch/test-results/`, Command 4's `PkgRoot` the tree's root, and `NativeBridge~/vendor/` provisioned per branch tree (adr-0002). Path: `development`. **Full.** | None. |
| `trees-fill` | Fill the `trees` binding (`.claude/bindings/vc-git.md` :19-20) with the §1 values, and change the knock-on sites of §4: the vc binding's Branch procedure tail (:8), the `verification` binding's Run from (:5) and Tree-safe (:14), and the `session-launch` Launch command's `<tree>` note (`.claude/bindings/core.md`). Path: `development`. **Lite** (adr-0003): no requirements doc; acceptance — (1) `.claude/bindings/vc-git.md` `## trees` carries every §1 value — Hub, Location, Create, List, Clean, Catch-up and Remove procedures, Writer-branch naming, Pool `none`, Release files — and `doctor` reads `trees` filled; (2) the Branch procedure cuts each branch in its own tree by the Create procedure, no longer "in the one checkout"; (3) the `verification` binding's Run from names the root of the tree the session runs in, and Tree-safe reads `yes` under the host bracket; (4) the `session-launch` Launch command's `<tree>` note no longer says `trees` is none. | `host-repoint`. |

## Status / openness

- **Status: G1 LOCKED 2026-11-05.** Forks (a)-(e) were ruled by the human on 2026-11-05 by AskUserQuestion, each to its recommended option, and are recorded in adr-0001…adr-0005; locked at G1 by the human's ruling "Lock as shown (Recommended)", which also took the PM defaults H5 and H6 and the open-item owners below.
- **All numbers provisional → the tuning pass:** the host-lock wait bound (H2); the package re-import cost per repoint, not yet measured — owner: the tuning pass of `host-repoint`.
- **PM defaults:** H5 and H6, the PM's defaults, were taken by the human's G1 ruling of 2026-11-05.
- **Hard dependencies / open:**
  - RESOLVED: (a) every branch in its own tree (adr-0001); (b) TestGround's manifest repointed per run, the keystone (adr-0002); (c) the sibling trees directory (adr-0003); (d) no writer pool (adr-0004); (e) the release files `CHANGELOG.md` and `.claude/` (adr-0005).
  - Open: device testing a branch on Quest through TestGround (H7) — parked; owner: the human, to raise.
  - Open: the host-lock wait bound, and the measured re-import cost per repoint — owner: the tuning pass of `host-repoint`.
  - RESOLVED: the follow-up splits into `host-repoint` and `trees-fill` — the feature backlog, §6, written at G1.
  - Open: the lock's form and location, how `NativeBridge~/vendor/` is provisioned, and the per-run manifest edit — owner: the architecture doc of `feat-host-repoint`.

## Related

- [adr-0001](../design-docs/parallel-trees/decisions/adr-0001-every-branch-in-a-tree.md) — every branch in its own tree.
- [adr-0002](../design-docs/parallel-trees/decisions/adr-0002-repoint-host-per-run.md) — TestGround's manifest repointed per run (keystone).
- [adr-0003](../design-docs/parallel-trees/decisions/adr-0003-sibling-trees-directory.md) — the sibling trees directory.
- [adr-0004](../design-docs/parallel-trees/decisions/adr-0004-no-writer-pool.md) — no writer pool.
- [adr-0005](../design-docs/parallel-trees/decisions/adr-0005-release-files.md) — release files `CHANGELOG.md` and `.claude/`.
- [trees-contract.md](../../.claude/references/core/trees-contract.md) — the core trees contract: the hub, the hub operation, the release files and the roles.
- [vc-git.md](../../.claude/bindings/vc-git.md) — the vc binding whose `trees` this fills.
- [unity.md](../../.claude/bindings/unity.md) — the verification binding.
- [core.md](../../.claude/bindings/core.md) — the `session-launch` binding.
- [verification-recipe.md](../verification-recipe.md) — the recipe whose `PkgRoot`, results paths and host bracket change.
- [tree-remove.py](../../.claude/references/vc-git/tree-remove.py) — the Remove procedure.
