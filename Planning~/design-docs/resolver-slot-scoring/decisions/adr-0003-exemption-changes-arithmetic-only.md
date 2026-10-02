---
id: adr-0003
title: The exemption changes score arithmetic only (lab arm A4L), over also removing the miss from the DR-7 ledger and start probe (arm A4)
status: accepted
date: 2026-10-02
topic: resolver-slot-scoring
---

# adr-0003 — The exemption changes score arithmetic only

## The fork

An exempt miss leaves raw and den (adr-0001), but the parser also counts missed required elements for DR-7 admission (`Planning~/design-docs/scoring-model.md` §0B) and for the admissibility start probe (§0D). Whether the exempt miss still counts there decides whether the exemption is pure arithmetic or also loosens which candidates may compete. The lab measured both shapes (`Planning~/design-docs/resolver-slot-scoring/lab-2026-10-02.md`).

## Options considered

1. **A4** — the exempt miss also leaves the DR-7 matched/missed ledger and the start probe.
2. **A4L** — the exempt miss still counts as missed there; only its own score term changes.

## The decision

**A4L: the exemption changes score arithmetic only.** The exempt miss still counts as missed for DR-7 admission and the admissibility/start probe; still sets the leading-miss latch, so the bar refuses an exempt anchor first (ruling 3); and still counts as a missed required slot for the eager gate (DR-3), so eager never commits it and elided commands wait for the flush. The coverage term and A3's charged-token rule are unchanged — the exemption touches only the slot's own term.

## Why the rejected branches lost

- **A4** — stutter fragments were admitted and won on earliest start, evicting the real command: `launch launch all missiles target hotel one` → a 0.333 fragment; DocCheck 62/66. What would have to change for it to win (PM's analysis): a redesign of admission or of the earliest-start key such that an admitted fragment can no longer take a round from the real command.

## Consequences

- Under A4L the DR-7 counts are A0's by construction, and the start probe differs from A0 at 0 of 4959 positions; the two eviction/absorption cases A4 broke return to their A0 results; DocCheck is 64/66, the two failures being the direct score pins (§7 A start 2 0.167 → 0.400; `fire at` 0.333 → 1.000).
- Arithmetic only still moves selection: the adr-0004 key decides 14 rounds, all between same-intent siblings.
- Arithmetic only still moves the eager hold, never to Commit: 3 Set_Combat prefixes go HoldExtendable → None; Commit stays 40/40.
- Leaving the orphan-table selector incremented keeps coverage as it is: `drive cut drive` scores 1/3.
- Bare-verb and two-word firing survive, because they are pure arithmetic; adr-0005 accepts them.

## Status

Accepted on 2026-10-02 by the human's ruling in conversation, after the lab. Recorded in `scoring-model.md` Amendment A5 (§0E), which re-locks only at G1. Re-locked with Amendment A5 at G1 on 2026-10-02.
