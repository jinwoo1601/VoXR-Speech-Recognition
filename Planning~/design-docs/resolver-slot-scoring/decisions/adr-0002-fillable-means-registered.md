---
id: adr-0002
title: A slot is fillable when a resolver is registered for its name, over an author mark on the pattern
status: accepted
date: 2026-10-02
topic: resolver-slot-scoring
---

# adr-0002 — A slot is fillable when a resolver is registered for its name

## The fork

adr-0001 exempts a *resolver-fillable* missed slot from the score, so the design must say what makes a slot fillable. Ruling 9's original wording has a required slot "marked resolvable"; #148 shipped registration as the marking (`Planning~/features/slot-resolver/requirements.md` §6 E-1). The choice decides whether the score depends on runtime configuration, and whether a new authoring surface is needed. Raised by the G0 scope's first open question for option 4 (`Planning~/design-docs/resolver-slot-scoring.md` §0).

## Options considered

1. **Registration.** A slot is fillable when a resolver is registered for its name (`RegisterSlotResolver(slotName, …)`), read at parse time.
2. **An author mark on the pattern.** Ruling 9's original wording: the author marks a required slot resolvable in the grammar.

## The decision

**A slot is fillable exactly when a resolver is registered for its name.** Registration is read at parse time and the score is fixed then (DR-8 in `scoring-model.md` §0E).

## Why the rejected branches lost

- **Author mark** — a new authoring surface #148 did not ship, and a marked slot with no resolver scores high and then falls to pending. What would have to change for it to win (PM's analysis): a requirement that tools without the registry (batch test runner, Editor tools) score identically to the runtime, or evidence that global slot names are too coarse — an author needing a slot fillable in some patterns and not in others.

## Consequences

Costs accepted with the ruling:

- The score becomes configuration-dependent, which no score has been before.
- Slot names are global, so registering `{track}` exempts it in every pattern that carries it.
- Any tool scoring without the registry — the batch test runner, Editor tools — disagrees with the runtime unless given the same registered set (the uniformity rule, M5, is the architecture doc's to implement).
- The author's lever against an unwanted exemption is the resolver itself: it may decline per intent, since the request carries the intent (design doc §2).

## Status

Accepted on 2026-10-02 by the human's ruling in conversation. Recorded in `scoring-model.md` Amendment A5 (§0E), which re-locks only at G1.
