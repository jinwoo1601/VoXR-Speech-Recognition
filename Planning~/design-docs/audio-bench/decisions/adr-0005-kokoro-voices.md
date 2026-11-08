---
id: adr-0005
title: Add voices with Kokoro-82M beside the baseline Piper voice, over more Piper voices or one voice for now
status: accepted
date: 2026-11-08
topic: audio-bench
---

# adr-0005 — Kokoro voices

## The fork

The scope agreed at G0 asks for several voices (`Planning~/design-docs/audio-bench.md` §0, In 4). The fixtures are one Piper voice, `en_US-lessac-medium` (`Tests~/Fixtures/manifest.json`), and the harness pins exact transcripts for them under "re-pin, never edit" (`05-rulings.md` :98). Piper's GPL-3.0 relicence is on record (`R6-evaluation-methodology.md` :321). Which voice source the bench uses decides the range of speakers its results cover, the licence it carries, and whether the existing pins move.

## Options considered

1. **(A) Kokoro-82M** (Apache-2.0, `R6-evaluation-methodology.md` :319). The voices come from a second engine, and the current Piper voice stays the baseline voice. The Kokoro model download is a human step.
2. **(B) More Piper voices.** The engine the fixtures already use. Its voice range is narrower, and Piper's GPL-3.0 relicence is on record.
3. **(C) One voice for now.** Nothing new is added. Every result is one speaker's.

## The decision

**Option (A): Kokoro-82M voices, with the current Piper voice `en_US-lessac-medium` kept as the baseline voice, so the existing pins never move.** The voice count is provisional → the tuning pass (~12 per R6).

## Why the rejected branches lost

- **(B) More Piper voices** — a narrower voice range, and Piper's GPL-3.0 relicence is on record (`R6-evaluation-methodology.md` :321). What would have to change for it to win: Piper stayed permissively licensed and covered Kokoro's speaker range.
- **(C) One voice for now** — every result would be one speaker's. What would have to change for it to win: the bench only had to stop regressions against the existing pins.

## Consequences

- The Kokoro model download is a human step; until it is taken, the bench runs the baseline voice only.
- Every report names the voice it ran with (the design doc's §3, hidden config differences).
- More voices are still TTS: no voice set lets a TTS result rank two decoders (C12, `03-critique-and-ranking.md` :164-165) or prove a gain before R5 human audio exists (adr-0008).

## Status

Accepted on 2026-11-08 by the human's (the maintainer's) ruling in session; the ruling took the recommended option. The design doc is `Planning~/design-docs/audio-bench.md`; it was locked at G1 on 2026-11-08. Amended on 2026-11-08 with the maintainer's BAR gap rulings.
