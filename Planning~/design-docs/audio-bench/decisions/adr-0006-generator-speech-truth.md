---
id: adr-0006
title: Take end of speech from the generator's truth, over an energy detector
status: accepted
date: 2026-11-08
topic: audio-bench
---

# adr-0006 — End of speech from the generator's truth

## The fork

The latency chain agreed at G0 starts at end of speech (`Planning~/design-docs/audio-bench.md` §0, In 1), and the maintainer's latency axis is "time from end of speech (or from the point the command is determinable) to the command event" (`00-brief.md` :40-41). The joined rig's current delay counts from end of audio — first fire − `Samples/SampleRate` — which includes the fixture's 1.0 s tail (`JoinedRecogniser.cs:301-303`). The bench needs each clip's end of speech as the zero of every latency it reports, and the conditions (adr-0004) add echo tails and noise around it.

## Options considered

1. **(A) Generator truth.** The generator writes each clip's speech start and end, since it placed the words. Augmentation keeps them: an echo tail does not move end of speech. It is exact for TTS.
2. **(B) An energy detector.** End of speech is found in the audio itself, so it works the same on human audio later. It adds a threshold that is itself a tuning knob, and it can be wrong by tens of ms.

## The decision

**Option (A): end of speech is the generator's truth, written per clip and kept through augmentation.**

## Why the rejected branches lost

- **(B) An energy detector** — it adds a threshold that is itself a tuning knob, and it can be wrong by tens of ms. It would win on R5 human recordings, where no generator truth exists.

## Consequences

- `Tests~/Fixtures/generate.py` gains the speech-boundary truth (the design doc's §4).
- The bench's latency counts from end of speech, where the joined rig's delay counts from end of audio; the two are not the same number.
- The truth exists only for generated audio. How to mark speech end on R5 human recordings is an open item.

## Status

Accepted on 2026-11-08 by the human's (the maintainer's) ruling in session; the ruling took the recommended option. The design doc is `Planning~/design-docs/audio-bench.md`; it was locked at G1 on 2026-11-08. Amended on 2026-11-08 with the maintainer's BAR gap rulings.
