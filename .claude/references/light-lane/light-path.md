# The light path

The constitution's **Paths** paragraph has a path's pack hold its stages. These are the light path's, binding on every piece of work on it, each held by the enforcer `## The enforcers` names.

## The rules

Issue and maintenance work runs in a light lane: still reviewed, and closed at **G2**, by the human's ruling, its class named beside it, or closed by class by the PM where the classes binding delegates the changeset's class; a close by class is not a G2.

Which classes the PM closes and merges without the human's G2 ruling is a slot the light-lane binding names, and every close by class is listed for the human at the next human stop; an unfilled or `none` slot delegates nothing.

Several deferred items that each qualify for the lane may run as one deferral batch: one branch, one review over their union, one close.

## The enforcers

| Rule | Enforcer |
|---|---|
| Issue and maintenance work runs in the light lane: still reviewed, and closed at G2, by the human's ruling, its class named beside it, or closed by class by the PM where the classes binding delegates the changeset's class; a close by class is not a G2. | The light lane's G2: the light lane's close skill runs REVIEW, then its HARD STOP on the G2 ruling; `g2-lite` RESOLVE, after review and preflight, running `harness.py close-class`, which reads the classes binding's delegation slot and the effective class `harness.py bounds` computes, any other result a human class at that HARD STOP; the close's audit line, before its merge: `g2-lite`'s tree-path LIST, or its CLOSE OUT record where `trees` is absent or the hub is on the branch; the listing and clearing at the human's next stop: `.claude/references/core/gate-packet.md` `## The packet`. |
| Deferred items that each qualify for the lane may run as one deferral batch: one branch, one review of their union, one close. | `tackle-issue` takes a batch of open items routed `light-lane`, read by `hub.py items --route light-lane`, within the `classes` binding's cap, its batch prerequisite run by `harness.py bounds`, then one RECORD writing the batch's items, one BRANCH, and OPEN and REVIEW once over the union, REVIEW at the class `harness.py bounds` computes from the union's diff; `g2-lite` CLASS reads the batch's items and its effective class, and CLOSE OUT closes every item `hub.py items --status taken` shows taken by the branch, `done`, in the one close. |
| Which light-lane classes the PM closes and merges without the human's G2 ruling is a slot the light-lane binding names, and every close by class is listed for the human at the next human stop; an unfilled or `none` slot delegates nothing. | The light lane's close skill, reading the slot and the effective class `harness.py bounds` computes, with review and preflight before any close; the audit list: the hub STATUS `## Auto-closed` section, cleared at the human's next stop. |
