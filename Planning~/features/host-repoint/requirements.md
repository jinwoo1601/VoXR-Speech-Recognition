---
type: requirements
feature: host-repoint
topic: parallel-trees
status: accepted
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
2. The at-rest read before a run — `file:` is the hub, `testables` as the host's committed manifest has them, no `Temp/UnityLockfile` — and a stop for the human, never a repair, when the lock is free but the host not at rest (H1, H2; re-locked 2026-11-05, `parallel-trees.md` :26).
3. The per-run repoint of TestGround's `file:` path to the tree under test, `testables` left as committed, and the tree's `Tests~` renamed, and its restore to rest, checked (H1, adr-0002; re-locked 2026-11-05, `parallel-trees.md` :26).
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

## 3. Why — the value it delivers

The design's goal (`parallel-trees.md` §5) is to remove the reason `trees` is none: TestGround tests whichever tree a run points it at, one run at a time, green only on that tree's own evidence. Branches can then run concurrently without verification silently testing the wrong checkout; this feature is the prerequisite of `trees-fill`.

## 4. Observable behaviour

The operator is whoever runs the `verification` binding's Commands — the PM or compile-check.

1. Commands 1-2 are run from a tree: the run takes the host lock; if the lock is held, it waits up to the bound, then reports BLOCKED naming the holder, the host untouched.
2. With the lock taken, the run reads that the host is at rest — `file:` is the hub, `testables` as the host's committed manifest has them, no `Temp/UnityLockfile`; if the host is not at rest, the run stops for the human, reports what it found and changes nothing.
3. The run points `file:` at the tree's root, leaving `testables` as committed, renames the tree's `Tests~`, runs EditMode then PlayMode, and writes results and logs to the tree's own `.scratch/test-results/`.
4. The run reverts the rename, restores the manifest, reads the host back at rest and releases the lock — also on a failed run or a crashed test run.
5. The run reports green only with an XML written by this run, failed=0 inconclusive=0, evidence that the package resolved from this tree's path, and the host at rest after.
6. Command 4 from a tree compiles that tree's `Runtime/` and `Samples~` (`PkgRoot` the tree's root), without the lock.
7. Command 3 runs in a provisioned branch tree; its writes under `vendor/overlaid/` stay in that tree; it never runs in a writer tree.
8. In the one checkout (`trees` none), outcomes match today's: the hub is the tree under test.

## 5. Functional requirements

| #  | Requirement | Story | Priority | Acceptance check | Source |
|----|-------------|-------|----------|------------------|--------|
| F1 | The host lock: one Commands 1-2 run at a time; a run that finds the lock held waits up to the bound, then reports BLOCKED. | S1 | Must | A run with the lock held by hand → BLOCKED, host bytes unchanged. | H1, H2; `parallel-trees.md` §1 |
| F2 | The at-rest read before a run — `file:` is the hub, `testables` as the host's committed manifest has them, no `Temp/UnityLockfile` — and a stop for the human, never a repair, when the host is not at rest. | S1 | Must | The manifest's `file:` hand-pointed elsewhere; separately, a `Temp/UnityLockfile` present; separately, the manifest's `testables` hand-removed → in each case the run stops, host bytes unchanged. | H1, H2; `parallel-trees.md` §1, :26 |
| F3 | The per-run repoint of TestGround's `file:` path to the tree under test, `testables` left as committed, the tree's `Tests~` renamed, and its restore to rest, checked. | S1 | Must | A run from a tree: the host manifest byte-identical before and after; `Tests~` back; no leftovers in `git status --short`. Run from the one checkout and from a throwaway detached worktree of `feat-host-repoint`, created and removed by the PM. | H1; adr-0002; `parallel-trees.md` §1, :26 |
| F4 | Green for Commands 1-2 only on the run's own evidence that the package resolved from this tree's path and that the host is back at rest. | S1 | Must | The report names the resolved-path evidence; a stale XML or a run pointed elsewhere is reported not green (mutation). Run from the one checkout and from a throwaway detached worktree of `feat-host-repoint`, created and removed by the PM. | H3; `parallel-trees.md` §1 |
| F5 | Results and logs per tree, under the tree's own `.scratch/test-results/`. | S1 | Must | The XML and logs are under the tree's own `.scratch/test-results/`; the hub's are untouched when the tree is not the hub. Run from the one checkout and from a throwaway detached worktree of `feat-host-repoint`, created and removed by the PM. | H4; `parallel-trees.md` §1 |
| F6 | Command 4's `PkgRoot` is the tree's root, run without the lock. | S1 | Must | Green from the tree; the csproj's `PkgRoot` reads the tree's root; a sample mutation in that tree turns it red. | H5; `parallel-trees.md` §1 |
| F7 | `NativeBridge~/vendor/` provisioned per branch tree; Command 3's writes stay in that tree; never a writer tree. | S1 | Must | The provisioning procedure is in the binding or the recipe; Command 3 green 37/37 in a provisioned tree, `vendor/overlaid/` written only there. Run from the one checkout and from a throwaway detached worktree of `feat-host-repoint`, created and removed by the PM. | H6; `parallel-trees.md` §1 |
| F8 | In the one checkout, outcomes unchanged; the host rests on the hub. | S1 | Must | Commands 1, 2 and 4 green in the one checkout under the new procedure. | H7; `parallel-trees.md` §1 |
| F9 | Tuning: the host-lock wait bound set, and the re-import cost per repoint measured and recorded. | S1 | Should | The host-lock wait bound's value and the measured re-import cost per repoint are both recorded. | H2; `parallel-trees.md` Status / openness |

## 6. Non-functional requirements

- **Performance:** the re-import cost per repoint is measured and recorded, and the wait bound set from it (F9); no budget is fixed now.
- **Robustness at the input boundary:** a crashed run never leads a later run to test the wrong code silently; the restore runs on a failed test run too.
- **Degraded dependencies:** the TestGround host repo — the bracket edits only the manifest's `file:`, leaving `testables` as committed; never TestGround's other uncommitted changes; never a git checkout or reset in TestGround.

## 7. Non-goals / deferred

- The G0 Out list (§2).
- Any change to `Runtime/` or `Samples~` code.
- A wait-bound value fixed in these requirements.

## 8. Dependencies & assumptions

- No feature merged first; `trees-fill` depends on this one.
- One machine; Unity 6000.4.7f1.
- TestGround is at rest at the start.
- TestGround's committed manifest carries `testables` (`Packages/manifest.json` :52-54).
- Agents cannot run git writes, so the restore is by file edit, and the throwaway worktree is created and removed by the PM.
- The throwaway worktree needs `NativeBridge~/vendor/` provisioned (527M, `du -sh` 2026-11-05) for F7.

## 9. Acceptance criteria (G2)

- [x] Every Must in §5 (F1-F8) checked; F3, F4, F5 and F7 from the one checkout and from a throwaway detached worktree of `feat-host-repoint`, created and removed by the PM. (Phase 5 from a throwaway worktree at `f390330`, green and its two mutations not green; the one checkout green at the Phase 5 gate and the review CHECK; record `memory/2026-11-05.md`, "implement Phase 5")
- [x] Human-only verification: open TestGround's Editor after an acceptance run and see VoXR resolved from the hub (H7). (read by the PM from a batchmode Editor log, `com.jinwoo1601.voxr@file:D:\Workspace\VoXR-Speech-Recognition`, under the human's ruling in Phase 5, "i'm saying you can check this yourself without me"; ruled to stand at G2)
- [x] The `verification` binding's Commands are green. (review-fix CHECK: Commands 1-4 green on the fixes committed as `55fe4f4`; record `memory/2026-11-05.md`, "review-cycle and as-built reconcile")

**Ruled at G2 on 2026-11-05** by the maintainer, in conversation (AskUserQuestion, "Accept"): accepted at head `9e8d8a6`, with the second preflight in view — the first preflight's as-built entries trimmed to the charter and the dated note and STATUS brought current in `9e8d8a6`. H7's PM read ruled to stand ("Stands as ruled"); the binding's "started in the background" deferred to the light lane ("Defer to light lane").

## 10. Open questions

1. The lock's form and location → the architecture doc.
2. How `NativeBridge~/vendor/` is provisioned, copied or shared → the architecture doc.
3. The per-run manifest edit → the architecture doc.
4. The source of H3's evidence → the architecture doc.
5. The host-lock wait bound's value → the tuning pass of `host-repoint`.
6. The re-import cost per repoint → the tuning pass of `host-repoint`.
