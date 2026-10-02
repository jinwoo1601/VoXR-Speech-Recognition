---
id: adr-0001
title: Exempt a resolver-fillable missed slot from the score, in the parser, over shipping as built, crediting it, waiving only its penalty, or a recogniser re-score
status: accepted
date: 2026-10-02
topic: resolver-slot-scoring
---

# adr-0001 — Exempt a resolver-fillable missed slot from the score, in the parser

## The fork

The score is raw / (den + coverage), and today a missed required slot is `-1` raw and `+1` den (`Planning~/design-docs/scoring-model.md` §5.1) while a missed required literal is `0/+1`. Slot resolvers (#148) are registered by slot name only, globally (`RegisterSlotResolver(slotName, …)`), at runtime with no parser rebuild, and are consulted only for candidates at or above `minScore` (0.6). So the short orders a resolver exists for never reach it: `"launch missiles"` against `launch missiles target {track}` scores 0.250 today (`Planning~/features/slot-resolver/requirements.md` §8 E-2). That defeats #148's player value ("A captain says 'launch missiles' … today the second form is the only one that fires", requirements §2) and ruling 9 (ellipsis is game-state resolution of an omitted required slot, `Planning~/research/2026-09-05-next-level/05-rulings.md`), under the constraint of ruling 3 (the bar is absolute; "I don't want commands firing that were not of user's intention") and `scoring-model.md` §3 goals 2 and 4. Every option except shipping as built amends the locked `scoring-model.md`, so it is decided here, on a design branch, and re-locked at G1.

## Options considered

1. **Ship as built.** The option taken for #148 on 2026-09-16; no score change, the arithmetic documented.
2. **Credit a resolver-filled slot as matched.** `"launch missiles"` → 0.75.
3. **Waive the miss penalty only, without crediting a match.** `"launch missiles"` → 0.50, still under the gate.
4. **Exempt a resolver-fillable missed slot from the score** — it contributes nothing to raw or den:
   - **4a**, in the parser, everywhere a pattern is scored;
   - **4b**, only in a recogniser re-score for the `minScore` check, leaving the parser's own scores and selection as they are.

## The decision

**Option 4a: a missed required slot whose name has a registered resolver contributes 0 to raw and 0 to den, in the parser, everywhere a pattern is scored.** It commits the design to the model in `Planning~/design-docs/resolver-slot-scoring.md` §1 and to Amendment A5 of `scoring-model.md` (§0E, DR-8): a spoken fillable slot scores as today, every other missed required slot keeps `-1/+1`, and the rule is uniform over every place a pattern is scored — how is the architecture doc's. Numbers stay provisional (→ the tuning pass).

## Why the rejected branches lost

- **(1) Ship as built** — the natural spoken form is reachable for 5/40 occurrences (recon record, A0 shape (b)); the hook stays close to inert for the short orders that motivated it. What would have to change for it to win (PM's analysis): R5 human-corpus evidence that speakers rarely elide required slots, or that the slot-less sibling idiom covers the elisions they do make.
- **(2) Credit as matched** — it manufactures evidence that the slot was spoken, against ruling 3's reason. What would have to change for it to win (PM's analysis): only the maintainer withdrawing ruling 3's stated reason.
- **(3) Waive the penalty only** — it buys nothing: `"launch missiles"` reaches 0.50, still under the gate. What would have to change for it to win (PM's analysis): nothing within this model — at 0.50 it never clears the gate.
- **(4b) Recogniser re-score only** — the parser already drops `"launch"` against `launch {weapon}` at 0.0 and selects winners on unexempted scores, so the short orders stay unreachable whatever the recogniser re-scores. What would have to change for it to win (PM's analysis): a ruling that scores must stay independent of runtime configuration, accepting that patterns the parser drops at ≤ 0 stay unreachable.

## Consequences

- The score becomes configuration-dependent, a first; what "fillable" means is adr-0002.
- How far the exemption reaches beyond arithmetic is adr-0003 (score arithmetic only); the selection key it needs is adr-0004; the thin firing it opens is accepted in adr-0005.
- Measured under the chosen shape (lab arm A4L, `resolver-slot-scoring/lab-2026-10-02.md`): reachable shape (a) 35/40 vs 18/40, shape (b) 28/40 vs 5/40, `{track}` (b) 12/15 vs 2/15, weapon release (b) 4/4 vs 0/4; 0/81 fully spoken Set_Combat utterances change.
- Two direct DocCheck score pins move: §7 A start 2 0.167 → 0.400; `fire at` 0.333 → 1.000.
- Every scoring site must apply the same rule — flush, eager, pending follow-up re-score, Editor runner-up comparison, the batch test runner given the registered set; the architecture doc owns how.
- Product docs (`scoring.md`, `command-recognition.md` authoring guidance, `KNOWN_LIMITATIONS.md`) change after G2, not here.

## Status

Accepted on 2026-10-02 by the human's ruling in conversation. Recorded in `scoring-model.md` Amendment A5 (§0E), which re-locks only at G1.
