---
id: adr-0005
title: Thin firing is accepted and documented, over a guard on how little must be spoken
status: accepted
date: 2026-10-02
topic: resolver-slot-scoring
---

# adr-0005 — Thin firing is accepted and documented, with no guard

## The fork

Under the exemption (adr-0001, adr-0003) a pattern whose only unspoken elements are fillable slots scores 1.000 whatever its length, so a bare verb or a two-word fragment can fire and leave the slots to the game. The lab measured it (`Planning~/design-docs/resolver-slot-scoring/lab-2026-10-02.md`, thin firing): the question is whether the design adds a rule against it.

## Options considered

1. **Accept** — thin firing is documented, with no new rule.
2. **Guard** — refuse an exempt-scored candidate unless at least 2 words were spoken, or at least 1 spoken slot or content word.

## The decision

**Accept thin firing and document it; add no guard.** A bare verb `launch` → 1.000 against `launch {weapon}` is ellipsis as defined (ruling 9); the bar guarantees the anchor was spoken; the confidence gate still applies; registering a resolver is the author's opt-in; and the exposure is the same as a hand-written slot-less sibling.

## Why the rejected branches lost

- **Guard** — it adds a rule and a number, needs another lab run, and `launch` alone would fail again.

## Consequences

Residues accepted with the ruling:

- Under arm A4L, 16 of the lab's 124 thin utterances fire with a missing slot: 2 one-word (`drive`, `thrusters`) and 14 two-word, among them `launch track` and `set to`. In the demo grammar `fire at` fires at 1.000 as well.
- 2 corpus rows (`disengage close distance safe range` and its reverse) fire two commands: A0 already fired the debris word `disengage` → `cease_fire`, and A4L adds the speaker's real phrase.
- The resolver's per-intent decline is the author's lever where a thin utterance must not fire (design doc §2).
- The product docs carry thin firing and the two-command rows in `KNOWN_LIMITATIONS.md` after G2.

## Status

Accepted on 2026-10-02 by the human's ruling in conversation; reopenable on R5 human-corpus evidence. Recorded in `scoring-model.md` Amendment A5 (§0E), which re-locks only at G1. Re-locked with Amendment A5 at G1 on 2026-10-02.
