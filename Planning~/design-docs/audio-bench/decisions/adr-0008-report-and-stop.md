---
id: adr-0008
title: The bench's numbers report and can stop a change, over also making them a verification gate
status: accepted
date: 2026-11-08
topic: audio-bench
---

# adr-0008 — Report and stop only

## The fork

The bench's numbers come from TTS audio on a desktop. C12 holds that the synthetic/real gap is architecture-dependent and that R41 screens "must never be used to rank two decoders" (`03-critique-and-ranking.md` :164-165), and ruling 6 reaffirms "No TTS-only comparison ranks two decoders" (`05-rulings.md` :112). TTS is allowed as a no-regression gate or screen (agc-filter adr-0004 :17, :30; `automated-verification.md` :252; `Planning~/design-docs/joined-rig.md` :118). Command 3, the pinned pass/fail harness, is timing-independent by design (`NativeBridge~/harness/README.md` :108), its pins are re-pinned and never edited (`05-rulings.md` :98), and no timing or accuracy baseline exists. What the bench's numbers may do decides whether the bench is a separate tool or part of verification, and what a Phase D item may conclude from a bench result.

## Options considered

1. **(A) Report + stop only.** A separate tool; Command 3 stays untouched. It reports paired A-vs-B comparisons: p50/p90/p99 per stage, never means (`I3-latency-ux-redesign.md` :337); accuracy flips with a paired significance test (McNemar, per `R6-evaluation-methodology.md` :369); curves per condition. A TTS result can stop a change as a regression, but never ranks decoders and never proves a gain until R5 human audio exists. It is never a verification gate.
2. **(B) Also a gate.** The bench's numbers are pinned and checked as a verification gate, as the harness's transcripts are.

## The decision

**Option (A): the bench reports and can stop a change, and nothing more.** It is a separate tool, and Command 3 stays untouched. It reports paired A-vs-B comparisons — p50/p90/p99 per stage, never means; accuracy flips with McNemar; curves per condition. A TTS result can stop a change as a regression, but never ranks decoders (C12, `03-critique-and-ranking.md` :164-165; `05-rulings.md` :112) and never proves a gain until R5 human audio exists. It is never a verification gate. The percentiles and McNemar's minimum discordant pairs (6, per `R6-evaluation-methodology.md` :369) are provisional → the tuning pass.

## Why the rejected branches lost

- **(B) Also a gate** — it would pin numbers on synthetic audio that cannot be trusted yet, and a desktop timing gate is flaky across machines. It would win once R5 human audio exists and the gate pins only the machine-independent audio-time part.

## Consequences

- Command 3 and its pins are left untouched ("re-pin, never edit", `05-rulings.md` :98); the bench adds no pin of its own.
- Each Phase D item's kill threshold — e.g. R17's ~1 % contradiction rate (`03-critique-and-ranking.md` :238) — belongs to that item's own design; the bench supplies the measurement.
- A setting chosen on the bench stays provisional until R5.
- A bench result can stop a change early; gains and decoder rankings wait for R5 human audio.
- The latency and accuracy axes are always reported side by side, never combined into one score (the design doc's §3).

## Status

Accepted on 2026-11-08 by the human's (the maintainer's) ruling in session; the ruling took the recommended option. The design doc is `Planning~/design-docs/audio-bench.md`; it was locked at G1 on 2026-11-08. Amended on 2026-11-08 with the maintainer's BAR gap rulings.
