---
type: requirements
feature: host-repoint
topic: parallel-trees
status: draft
updated: 2026-11-05
sources:
  - Planning~/design-docs/parallel-trees.md
  - Planning~/design-docs/parallel-trees/decisions/adr-0002-repoint-host-per-run.md
---

# feat-host-repoint — requirements

Track **Full**. Path `development`. Design `parallel-trees` (`Planning~/design-docs/parallel-trees.md`), locked at G1 2026-11-05, merged as PR #187. Depends on no feature; `trees-fill` (§6 row 2) depends on it.

## 1. User story

- **S1** — As the VoXR maintainer, I want each Unity test run to take TestGround in turn — locked, pointed at the tree under test, restored to main after — with green only on that tree's own evidence, so that verification stays correct once branches run in separate trees and trees can be filled.

## 2. Feature & scope

Backlog row (`parallel-trees.md` §6):

> `host-repoint` | The per-run host bracket of §1 (H1-H6) in the verification binding's Commands 1-4 (`.claude/bindings/unity.md` :6-9) and `Planning~/verification-recipe.md`: the host lock, the at-rest read, the repoint of TestGround's `file:` path to the tree under test and its restore, green on the run's own evidence, results and logs under the tree's own `.scratch/test-results/`, Command 4's `PkgRoot` the tree's root, and `NativeBridge~/vendor/` provisioned per branch tree (adr-0002). Path: `development`. **Full.** | None.

**Agreed at G0 on 2026-11-05** by the maintainer, in session, as proposed ("Agree (Recommended)"); every default taken.

**In**

1. The host lock, so Commands 1-2 take turns on TestGround (H1), with a wait bound then BLOCKED (H2).
2. The at-rest read before a run — `file:` is the hub, no `testables`, no `Temp/UnityLockfile` — and a stop for the human, never a repair, when the lock is free but the host not at rest (H1, H2).
3. The per-run repoint of TestGround's `file:` path to the tree under test with `testables` added and the tree's `Tests~` renamed, and its restore to rest, checked (H1, adr-0002).
4. Green for Commands 1-2 only on the run's own evidence that the package resolved from this tree's path and that the host is back at rest (H3).
5. Results and logs under the tree's own `.scratch/test-results/` (H4).
6. Command 4's `PkgRoot` set to the tree's root, run without the lock (H5).
7. `NativeBridge~/vendor/` provisioned per branch tree so Command 3's writes under `vendor/overlaid/` stay in that tree, never in a writer tree (H6).

The sites: the `verification` binding's Commands 1-4 (`.claude/bindings/unity.md` :6-9) and `Planning~/verification-recipe.md`. Left to this feature's architecture doc: the lock's form and location, how `vendor/` is provisioned (copied or shared), and the per-run manifest edit. Tuning pass of this feature: the host-lock wait bound and the measured re-import cost per repoint.

**Out**

1. Filling the `trees` binding and the knock-on sites — the vc binding's Branch procedure tail, the `verification` binding's Run from (:5) and Tree-safe (:14), the `session-launch` `<tree>` note — all `trees-fill` (§6 row 2).
2. Device testing a branch through TestGround (H7, parked, owner the human).
3. TestGround's own `trees`, publishing or consumption outside TestGround, and core-pack changes (to harness-hub).
4. Product docs — after G2, by the codex reconciler.
5. `release` and `lab-next-level-probes`, held by the human.
