---
id: adr-0004
title: Generate the test conditions on the fly from a fixed seed, over pre-rendering and committing them
status: accepted
date: 2026-11-08
topic: audio-bench
---

# adr-0004 — Seeded on-the-fly conditions

## The fork

The scope agreed at G0 asks for harder test audio: the TTS fixtures with noise, room echo and mic-like filtering added, plus several voices and levels (`Planning~/design-docs/audio-bench.md` §0, In 4). Today the fixtures are 52 TTS WAVs rendered by `Tests~/Fixtures/generate.py` — piper-tts 1.6.0 → soxr 48 kHz → peak 0.04-0.4 → lead 0.3 s / tail 1.0 s → 16-bit, with optional seeded white noise at `noiseFloorDbfs` (`:107-110`; the manifest uses -60) — and the render is not byte-deterministic across runs (`generate.py:8-14`). Conditions multiply the clips, so where the conditioned audio comes from decides the repo's size, whether two runs are identical, and whether each conditioned file can be listened to.

## Options considered

1. **(A) Generated on the fly, seeded.** Only clean renders are committed. The bench applies, per condition and from a fixed seed: speed change, room echo, noise, mic-like filtering, and level into the Quest mic range. Runs are identical and the repo does not grow. It gives up having every conditioned file on disk to listen to.
2. **(B) Pre-rendered and committed.** Every file can be listened to. Clips × conditions bloats the repo, and any regeneration churns the files, because Piper is not byte-deterministic (`generate.py:8-14`).

## The decision

**Option (A): conditions generated on the fly from a fixed seed, with only clean renders committed.** Generated noise and synthetic echoes come first; MUSAN (CC BY 4.0) and OpenSLR SLR28 room impulse responses (Apache-2.0) are an optional add-on the human downloads, since subagents are refused downloads. The recipe follows `R6-evaluation-methodology.md` :191-196, and results are reported as curves per condition, e.g. accuracy against SNR, never pooled (`R6-evaluation-methodology.md` :336). The condition steps are provisional → the tuning pass: speed {0.9, 1.0, 1.1}, RT60 0.2–0.5 s, SNR {20, 15, 10, 5} dB, level range [0.04, 0.4], and game-audio SIR steps (`I1-recognition-core-redesign.md` :264).

## Why the rejected branches lost

- **(B) Pre-rendered and committed** — clips × conditions bloats the repo, and any regeneration churns the files, since Piper is not byte-deterministic. What would have to change for it to win: renders were byte-deterministic and the set small enough to commit, e.g. a fixed listening set.

## Consequences

- Every report names the condition and the seed it ran with (the design doc's §3, hidden config differences).
- The MUSAN and SLR28 add-on is a human step; until it is taken, noise and echo are the generated and synthetic kinds only.
- Augmented numbers rank conditions, not field rates (`R6-evaluation-methodology.md` :451), and digital mixing is "a lower bound on difficulty, not a model of it" (`I1-recognition-core-redesign.md` :274).
- The 16 phrases under 12 conditions are not 192 samples: significance is counted per phrase (`R6-evaluation-methodology.md` :198).
- Augmentation keeps each clip's speech boundaries (adr-0006).
- Left open: whether the stand-in negative set needs game audio, for which the maintainer's title audio is a human-supplied input (adr-0007).

## Status

Accepted on 2026-11-08 by the human's (the maintainer's) ruling in session; the ruling took the recommended option. The design doc is `Planning~/design-docs/audio-bench.md`; it was locked at G1 on 2026-11-08. Amended on 2026-11-08 with the maintainer's BAR gap rulings.
