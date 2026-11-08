---
id: adr-0007
title: Measure false fires on a stand-in negative set before R4, over waiting for R4
status: accepted
date: 2026-11-08
topic: audio-bench
---

# adr-0007 — Stand-in negatives before R4

## The fork

The maintainer's accuracy axis includes "real out-of-grammar/noise rejection" and "fewer phantom or wrong commands" (`00-brief.md` :40-41), and the scope agreed at G0 asks for false triggers on non-command audio (`Planning~/design-docs/audio-bench.md` §0, In 3). R4, the negative corpus, is ruled GO (`05-rulings.md` :126) but has not started, and the fixtures hold one silence negative (§0, Prerequisites). Whether the bench measures false fires before R4 exists decides whether the phantom-command axis is measured at all for the Phase D changes that come first.

## Options considered

1. **(A) A stand-in set.** Generated noise-only clips at each condition, silence, and TTS phrases that are not commands (out-of-set words, near-misses). It gives an early false-fire rate, marked provisional and stand-in, and R4 replaces it.
2. **(B) Wait for R4.** Less work, and the false-fire rate comes from the real negative corpus when it does come.

## The decision

**Option (A): a stand-in negative set of generated noise-only clips at each condition, silence, and non-command TTS phrases, whose false-fire rate is marked provisional and stand-in until R4 replaces it.**

## Why the rejected branches lost

- **(B) Wait for R4** — less work, but the phantom-command axis, one of the maintainer's accuracy goals, stays unmeasured. What would have to change for it to win: R4 landed before the bench's first Phase D measurement.

## Consequences

- Zero false fires on the stand-in set reads as "set too easy", not "zero" (R4's cheapest-experiment logic, `03-critique-and-ranking.md` :244).
- R4 replaces the stand-in set; the bench does not decide the R4 corpus, which is human recording and human-only.
- Left open: whether the stand-in set needs game audio, for which the maintainer's title audio is a human-supplied input.

## Status

Accepted on 2026-11-08 by the human's (the maintainer's) ruling in session; the ruling took the recommended option. The design doc is `Planning~/design-docs/audio-bench.md`; it was locked at G1 on 2026-11-08. Amended on 2026-11-08 with the maintainer's BAR gap rulings.
