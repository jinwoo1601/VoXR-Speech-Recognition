---
type: requirements
feature: trees-fill
topic: parallel-trees
status: draft
updated: 2026-11-05
sources:
  - Planning~/design-docs/parallel-trees.md
  - Planning~/design-docs/parallel-trees/decisions/adr-0001-every-branch-in-a-tree.md
  - Planning~/design-docs/parallel-trees/decisions/adr-0002-repoint-host-per-run.md
  - Planning~/design-docs/parallel-trees/decisions/adr-0003-sibling-trees-directory.md
  - Planning~/design-docs/parallel-trees/decisions/adr-0004-no-writer-pool.md
  - Planning~/design-docs/parallel-trees/decisions/adr-0005-release-files.md
---

# feat-trees-fill — requirements

Track **Full**, returned from lite on 2026-11-05. Path `development`. Design `parallel-trees` (`Planning~/design-docs/parallel-trees.md`), locked at G1 2026-11-05. Depends on `host-repoint` (merged, PR #188).

## 1. User story

- **S1** — As the VoXR maintainer, I want the `trees` binding filled so each branch runs in its own git worktree, so that branches can run side by side while every Unity test run still goes through the one host by the `host-repoint` bracket.

## 2. Feature & scope

Backlog row (`parallel-trees.md` §6 :162):

> `trees-fill` | Fill the `trees` binding (`.claude/bindings/vc-git.md` :19-20) with the §1 values, and change the knock-on sites of §4: the vc binding's Branch procedure tail (:8), the `verification` binding's Run from (:5) and Tree-safe (:14), and the `session-launch` Launch command's `<tree>` note (`.claude/bindings/core.md`). Path: `development`. **Lite** (adr-0003): no requirements doc; acceptance — (1) `.claude/bindings/vc-git.md` `## trees` carries every §1 value — Hub, Location, Create, List, Clean, Catch-up and Remove procedures, Writer-branch naming, Pool `none`, Release files — and `doctor` reads `trees` filled; (2) the Branch procedure cuts each branch in its own tree by the Create procedure, no longer "in the one checkout"; (3) the `verification` binding's Run from names the root of the tree the session runs in, and Tree-safe reads `yes` under the host bracket; (4) the `session-launch` Launch command's `<tree>` note no longer says `trees` is none. | `host-repoint`.

**Agreed at G0 on 2026-11-05** by the maintainer, in conversation (AskUserQuestion, "Agree as shown (Recommended)"): opened lite, path `development`, HRP-1 out of scope.

**Returned to full on 2026-11-05.** At PLAN on the lite draft the maintainer ruled "Return to full track (Recommended)" (AskUserQuestion), because how the hub's local `main` stays level with `origin/main` is a choice the locked design does not fix (`.claude/references/core/feature-lite.md` `## The escape`); at the same stop the maintainer ruled "Rule all three in (Recommended)" for `.claude/bindings/core.md` :27, `.claude/bindings/vc-git.md` :9 and the `upgrade` recompose, and "Accept as shown (Recommended)" for the four-phase breakdown, which is put again after this doc. At SETTLE the maintainer ruled the sync choice "A: pull before, push after (Recommended)"; rejected were B, merging locally in the hub as the harness repo does, because Claude would make the merge commit, and C, routing memory writes through PRs, as too heavy. The content of §§1-9 was ruled "Settle as written (Recommended)".

**In**

1. Fill `.claude/bindings/vc-git.md` `## trees` with the §1 values.
2. Change the knock-on sites:
   - `vc-git.md` :8, the Branch procedure tail;
   - `vc-git.md` :9, the hub check-in clause;
   - `unity.md` :5, Run from;
   - `unity.md` :14, Tree-safe;
   - `core.md` :22, the Launch note;
   - `core.md` :27, the Release wait reason.
3. State the hub sync rule in the bindings.
4. Bring the composed skill copies current by `upgrade`.

## 3. Why — the value it delivers

- Branches run in parallel trees.
- Unity runs stay serialized through the one host.
- The hub's `main` and GitHub's `main` never stay split.

## 4. Observable behaviour

1. A branch is cut into `D:\Workspace\VoXR-Speech-Recognition.trees\<branch>` by the Create procedure.
2. `doctor` reads `trees` filled.
3. The skills' copies show the filled values.
4. A hub write made after a PR merged on GitHub lands, its push succeeds, and the hub's `main` equals `origin/main` afterwards.

## 5. Functional requirements

| #  | Requirement | Story | Priority | Acceptance check | Source |
|----|-------------|-------|----------|------------------|--------|
| F1 | `.claude/bindings/vc-git.md` `## trees` carries every §1 value. | S1 | Must | `doctor` shows no `SLOTS` or `UNFILLED` row for `trees`. | A1; `parallel-trees.md` §6 :162 |
| F2 | The Branch procedure cuts each branch in its own tree by Create, and no longer says "in the one checkout". | S1 | Must | Read `vc-git.md` :8. | A2; `parallel-trees.md` §6 :162 |
| F3 | Run from names the session's tree root, and Tree-safe reads `yes` under the host bracket. | S1 | Must | Read `unity.md` :5 and :14. | A3; `parallel-trees.md` §6 :162 |
| F4 | The Launch command's `<tree>` note no longer says `trees` is none. | S1 | Must | Read `core.md` :22. | A4; `parallel-trees.md` §6 :162 |
| F5 | The Release wait reason is true under filled trees. The value stays `none`, because `tree-remove.py` waits for the tree itself. | S1 | Must | Read `core.md` :27. | `parallel-trees.md` §4 :137-147; PLAN ruling 2026-11-05, "Rule all three in (Recommended)" |
| F6 | The vc hub check-in clause (`vc-git.md` :9) names the contract's hub write. | S1 | Must | Read `vc-git.md` :9 against `trees-contract.md`. | `parallel-trees.md` §4 :137-147; PLAN ruling 2026-11-05, "Rule all three in (Recommended)" |
| F7 | The hub sync: every hub write is preceded by `git pull --no-rebase origin main` in the hub and followed by `git push`. PRs still merge on GitHub, by the human. | S1 | Must | A PM exercise after a PR merge on origin: the hub write lands; the push succeeds; `git rev-parse main origin/main` match. | adr-0001 (the hub holds `memory/` on `main`); SETTLE sync ruling 2026-11-05, "A: pull before, push after (Recommended)" |
| F8 | The composed copies are recomposed by `upgrade`. | S1 | Must | No V23 row, and the spliced `trees`, `session-launch`, vc and verification blocks equal the live bindings. | adr-0005 (`.claude/` is regenerated by `upgrade`) |

## 6. Non-functional requirements

- No Unity Command fires, because the change is binding prose only.
- `validate` and `doctor` show no new failure compared with `bd6f1b3`.
- The `host-repoint` bracket is unchanged.
- A failed push or pull is reported, never retried in another form. The local write stands.

## 7. Non-goals / deferred

- HRP-1.
- TestGround's own `trees`.
- Core-pack changes. A built-in push in `hub.py` would go to harness-hub.
- Device testing of a branch.
- A writer pool.
- The two stale auto-memories, which the PM updates after G2.

## 8. Dependencies & assumptions

- **Depends on:**
  - `host-repoint` (merged, PR #188);
  - the base `../harness` with `tree-remove.py` and `merge-attributes.txt`;
  - `upgrade`.
- **Assumes:**
  - only the hub writes `memory/` under trees;
  - PR merges stay the human's, on GitHub.

## 9. Acceptance criteria (G2)

The backlog row's criteria (`parallel-trees.md` §6 :162), verbatim:

- **A1.** `.claude/bindings/vc-git.md` `## trees` carries every §1 value — Hub, Location, Create, List, Clean, Catch-up and Remove procedures, Writer-branch naming, Pool `none`, Release files — and `doctor` reads `trees` filled
- **A2.** the Branch procedure cuts each branch in its own tree by the Create procedure, no longer "in the one checkout"
- **A3.** the `verification` binding's Run from names the root of the tree the session runs in, and Tree-safe reads `yes` under the host bracket
- **A4.** the `session-launch` Launch command's `<tree>` note no longer says `trees` is none.

- [ ] Every Must, F1-F8, is checked.
- [ ] The PM cuts a throwaway branch tree by Create, lists it, and removes it by Remove, from the hub.
- [ ] `validate` and `doctor` show no new failure.
- [ ] The verification binding's Commands: none fire. This is said, not run.

## 10. Open questions

None at SETTLE.

The four-phase breakdown is put again at the architecture doc's PLAN, with F5-F8 placed.
