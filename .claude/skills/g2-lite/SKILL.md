---
name: g2-lite
description: "Close light-lane work on the human's G2-lite ruling, then the close-out and the merge that ruling authorizes. Use on 'close this issue', 'rule on the changeset', 'G2-lite', or when light-lane work is finished and ready to merge."
kind: orchestration
agents: [gate-preflight, compile-check]
stops: [the G2-lite ruling before any close-out or merge, a conflict or red re-verification at catch-up]
status: active
bindings: [vc, verification, product-docs, trees, session-launch, classes]
removal-date: null
---

# g2-lite

## What this is

**The light lane's one gate.** Issue and maintenance work is exempt from G0 and G1, is still reviewed, and is closed by a G2-lite ruling on the pull request or changeset — this skill prepares the evidence that ruling needs and executes what the ruling unlocks.

> The lane skips two gates, never the third. The bounds are checked, preflight audits a slim changeset and the STATUS budget is read on prose and trivial, the review runs or is confirmed, the human rules — and only then do close-out and merge exist.

## Bindings

Each key is looked up as `.claude/references/core/delegation-contract.md` `## Bindings` says: `.claude/bindings/<pack>.md` first, the project `CLAUDE.md` *Bindings* section as the pre-scaffold fallback.

- `vc` (required) — the *Branch procedure*, whose light-lane form LANE matches the branch name against; the working-tree and revision reads the preflight runs on an effective slim (*Base-revision reads*), the *Merge procedure* the review step branches on, and MERGE's own procedure. Absent → stop and say so before any brief is filed: the review step has no route to choose and the merge has no idiom.
- `verification` (required) — on an effective slim, the commands quoted into the preflight brief, whose run evidence the audit looks for; `none` is a filled value and the preflight reports `nothing to run`. Absent → stop and say so before the brief is filed, or on prose and trivial before PRESENT.
- `product-docs` (optional) — the *Reconciling agent* slot named at CLOSE OUT where the changeset falsifies a product doc. `none bound`, or a slot naming no agent → report the falsified doc to the human and go no further. A named agent → report it as the dispatch the PM makes; this skill never dispatches it, because that agent ships in a docs pack this pack neither provides nor requires.
- `trees` (optional) — the slots `references/tree-path.md` `## Bindings` names. Absent → today's path. Filled → the steps run as `references/tree-path.md` orders them.
- `session-launch` (optional) — the Hub session name, read by `references/tree-path.md`. Absent, or the Hub session name missing or `none` → REMOVE as today and no report sent.
- `classes` (optional) — the prose and trivial bounds and the batch cap, read by the `bounds` verb, not by this skill. Absent or unfilled → prose and trivial fail closed to slim and a batch fails the cap; never a stop.
- Agents — `gate-preflight` missing → stop before PREFLIGHT dispatches, on an effective slim, and say so. `compile-check` missing → where CATCH-UP brings the main branch in, stop before the merge and say so.

## When it applies — and prerequisites

**Trigger:** a light-lane changeset — one issue, one batch of deferrals, or one maintenance task — is finished and ready to close.

Confirm before proceeding — if any item fails, stop and say which step or lane is actually needed:
- The changeset is complete: nothing half-written, nothing left staged and forgotten.
- Everything the `verification` binding names has been run at the head being presented.
- The work is one changeset. Work that grew into a feature belongs on the implementation track, at G2 rather than here.
- The lane is declared light: by this branch's launch brief, committed under `memory/handoffs/`, whose `## Lane` value begins `light`, or by the branch name's light-lane form. Neither → this item fails: the work belongs to the tracks and gates it came from.
- The branch is not in the lab form: the lab form the `vc` binding's *Branch procedure* names, or `lab-<topic>` where it names none (a `<…>` placeholder matches any text). A lab branch merges nothing → stop and say so before any brief is filed.

## Workflow

Where `trees` is filled, as the contract reads it, these steps run as `references/tree-path.md` orders them; otherwise they run as written.

1. **LANE** — in this order:
   1. **Read the declaration.** This branch's launch brief, committed under `memory/handoffs/`, whose `## Lane` value begins `light`; or else the branch name, matching the light-lane form of the `vc` binding's *Branch procedure* (a `<…>` placeholder matches any text).
   2. **Record which one declares it** — the lane prerequisite holds, so one does: it was declared at the start. Go on without asking.
2. **CLASS** — read the declared class and a batch's items: the branch's `memory/STATUS.md` line (`class <c>`, `batch <id>, <id>...`), else both from its launch or handoff brief's `## Lane` (`light, class <c>`, or for a batch `light, class <c>, batch <id>, <id>...`); none found → slim. Then, at the head being presented, the PM saves the `vc` binding's per-file line-count read (its *Base-revision reads*) from the changeset's merge base with the main branch to its head, redirected to `.scratch/g2-lite-<changeset>-numstat.txt`, and runs the base's `scripts/harness.py bounds --root <project> --class <declared> --numstat .scratch/g2-lite-<changeset>-numstat.txt`, with one `--item <id>` per batch item, the base being the `base` path in `.claude/harness.json`, relative to the project root. Its verdict is read by exit code and words, never the dash: exit 0 and a line beginning `bounds: PASS` keep the declared class as the effective one; anything else — exit 1, `bounds: FAIL`, `bounds: refused` on stderr — is slim. With no manifest the check could not run: say so and read slim. Keep the output and the effective class.
3. **PREFLIGHT** — no agent on an effective prose or trivial: instead the PM runs the base's `scripts/harness.py budget --root <project>` itself, the base being the `base` path in `.claude/harness.json`, relative to the project root, which checks `memory/STATUS.md` against its budget. It is read as `gate-preflight` item 6 reads it: a final line `budget: PASS` passes; `budget: FAIL` is a FAIL, its failing lines quoted; a refusal (`budget: refused — <reason>` on stderr) or no final line is UNVERIFIABLE, quoted; `NOT CHECKED` lines are quoted and never a FAIL. On an effective slim, file `.scratch/g2-lite-<changeset>-preflight-brief.md` from the common fields of `.claude/references/core/delegation-contract.md` — the changeset and what it claims to do, the composition's declared preflight checks, the `vc`, `verification` and `trees` bindings quoted, and the project rules, as those common fields say — then dispatch `gate-preflight` through the Agent tool without a `name`, scoped to the changeset rather than to a feature.
4. **PRESENT** — relay CLASS's output on every class — banner, bound lines, verdict, or that the check could not run; where the declared class is not slim and the verdict is not PASS, state the escalation: declared `<c>`, run at slim, with its FAIL lines. Where PREFLIGHT dispatched, relay the audit verbatim: per-item PASS / FAIL / UNVERIFIABLE with evidence. An audit FAIL → remediate and re-run the preflight; never present a failing audit as rulable. Where it dispatched none, say so and relay its `budget` output verbatim; a `budget: FAIL` → remediate and re-run it; never present a failing budget as rulable.
5. **REVIEW** — review per the `vc` binding, at the effective class, handed to the skill below as the class it reviews at. Where its *Merge procedure* names a pull-request route, run the pull-request review skill the project's version-control pack provides — it is in `.claude/skills/`. Where it names none, run core's `review-cycle` over the changeset. No `vc` binding: stop and say so. Where a review has already run on this changeset at the effective class or deeper — trivial below prose below slim below full, read from the profile its brief recorded under `## Angles` — it is confirmed rather than repeated — its CONFIRMED findings and the fixes landed for each, fixed by default and listed at the G2-lite stop, and the human's rulings on the only findings that took one: a disputed finding, a deferral, a scope change or a canon fork.
6. **HARD STOP — the G2-lite ruling.** Ask for the human's explicit ruling on the pull request or changeset, with the `bounds` lines, the audit where one ran, and the review in view, putting the ruling by `.claude/references/core/ruling-form.md`. A clean preflight is not a ruling; neither is approval of anything but the close itself. The ruling's form — where and how it is given: in conversation, a pull-request approval, … — is the one the `vc` binding names; `ruling-form.md` governs what the PM's message puts to the human. Only that ruling unlocks steps 7 and 8.
7. **CLOSE OUT** — the CHANGELOG line and the `memory/` record, both inside the PM's own write scope and both the whole record this lane keeps: the lane writes no design, requirement or architecture doc, so no writer is dispatched to author a ruling into one. The `memory/` record is the `memory/STATUS.md` line and the session's dated note; the same act writes the note as `.claude/references/core/gate-close.md` `## The dated note` says. For a batch, every item closes in the `memory/` record: each deferral's entry leaves `## Deferred` for the session's dated note. Where the changeset falsifies a product doc, say which doc and name the `product-docs` binding's *Reconciling agent* slot per `## Bindings` above, and leave the dispatch to the PM.
8. **MERGE** — per the `vc` binding's *Merge procedure*. **The G2-lite ruling is itself the word to merge**; there is no second stop. Relay CLOSE OUT's own edits first, as a diff the human can read, then merge. Show, do not ask again.

The audit is the agent's work and the review is the lane's; the ruling and the merge are the human's, and the close-out record is the PM's own.
