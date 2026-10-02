---
id: adr-0004
title: Fewer exempted slots ranks better, immediately after score, over adding no selection key
status: accepted
date: 2026-10-02
topic: resolver-slot-scoring
---

# adr-0004 — Fewer exempted slots ranks better, immediately after score

## The fork

Under the exemption (adr-0001) a pattern that omits a fillable slot can tie at the same start and score with a slot-less sibling the author wrote for the elided form — `intercept track {track}` beside `intercept track {track} {burn_level}`, both 1.000. Selection today orders earliest start → score → consumed span → literal count → registration order (`Planning~/design-docs/scoring-model.md` §5.3), and nothing in it prefers the candidate that needs less from the game.

## Options considered

1. **The key** — a new selection key immediately after score: the candidate with fewer exempted slots ranks better.
2. **No key** — selection keys unchanged.

## The decision

**Add the key: selection becomes earliest start → score → fewer exempted slots → consumed span → literal count → registration order** (DR-9 in `scoring-model.md` §0E).

## Why the rejected branches lost

- **No key** — 5 fully spoken Set_Combat commands asked the resolver for a `burn_level` the speaker deliberately left out: the sibling `intercept track {track}` tied with `… {burn_level}`, and the tie fell to the later selection keys. What would have to change for it to win (PM's analysis): a grammar where the with-slot pattern should beat its slot-less sibling on an exact tie; none is known.

## Consequences

- The slot-less sibling idiom keeps working where authored: under arm A4L, 0 of 81 fully spoken Set_Combat utterances change against A0, and the 5 that change without the key are exactly those sibling cases (`Planning~/design-docs/resolver-slot-scoring/lab-2026-10-02.md`).
- The key decides 14 rounds under A4L, all between same-intent siblings, with 0 cross-intent rivals.
- Ties it does not separate still fall to registration order: `orient to` → `orient_to_heading` over `orient_to_track`, equal at 0.667 with equal exemptions.
- The key is the reason a slot-less sibling still wins a tie, which the operator relies on when keeping or dropping siblings (design doc §2).

## Status

Accepted on 2026-10-02 by the human's ruling in conversation. Recorded in `scoring-model.md` Amendment A5 (§0E), which re-locks only at G1. Re-locked with Amendment A5 at G1 on 2026-10-02.
