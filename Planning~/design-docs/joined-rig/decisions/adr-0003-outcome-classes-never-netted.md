---
id: adr-0003
title: Score by outcome classes plus slot credit, never netted, over one weighted score or exact pass/fail
status: accepted
date: 2026-10-13
topic: joined-rig
---

# adr-0003 — Outcome classes plus slot credit, never netted

## The fork

The scope agreed at G0 (`Planning~/design-docs/joined-rig.md` §0, In 2) asks for a scorer that gives slot partial credit. The parser rig has no scorer, and the only intent/slot check, `Runtime/Testing/VoxrBatchTestRunner.cs`, requires intent and slots to match exactly, with the first failure ending the check (`:208`, `:303-329`). Ruling 3 governs what the score must never hide: "I don't want commands firing that were not of user's intention" (`Planning~/research/2026-09-05-next-level/05-rulings.md`). The scorer's shape is what every gated item reads off the joined run, so it is decided here.

## Options considered

1. **(a) Outcome classes plus slot credit, never netted.** Each utterance lands in one class — correct fire; right intent with wrong slot(s); wrong intent; missed (no fire); or false fire on a negative — and slot partial credit is the fraction of expected slots that match exactly.
2. **(b) One weighted score.**
3. **(c) Keep exact pass/fail.**

## The decision

**Option (a): each utterance lands in one outcome class, slot partial credit is the fraction of expected slots that match exactly, and wrong-intent fires and false fires are always reported separately and never netted against hits (ruling 3).** Any weighting of classes into a single number is provisional and belongs to the tuning pass, as does the partial-credit formula beyond "fraction of expected slots matched exactly".

## Why the rejected branches lost

- **(b) One weighted score** — it hides which failure moved, and G1 would be locking its weight. What would have to change for it to win: the decision record gives the reason it lost and nothing more.
- **(c) Exact pass/fail** — it has no partial credit, which the locked scope requires (§0, In 2). What would have to change for it to win: the decision record gives the reason it lost and nothing more.

## Consequences

- A change that adds hits and a false fire cannot look like a net gain: phantoms stay visible as their own number.
- The false-fire class needs negatives to fire on; the R4 negative corpus is the only source of a false-fire rate, and the rig accepts its manifest without building it.
- No class weight or threshold is decided by this design; any weighting, and the partial-credit formula beyond "fraction of expected slots matched exactly", wait for the tuning pass.
- The scorer and its outcome classes are owned by this topic.

## Status

Accepted on 2026-10-13 by the human's ruling in conversation. The design doc (`Planning~/design-docs/joined-rig.md`) is in design and not locked.
