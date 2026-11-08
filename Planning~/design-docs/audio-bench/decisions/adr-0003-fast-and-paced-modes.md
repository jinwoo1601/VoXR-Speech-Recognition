---
id: adr-0003
title: Run a fast mode and a paced mode, over pacing every run
status: accepted
date: 2026-11-08
topic: audio-bench
---

# adr-0003 — Fast and paced modes

## The fork

Real-speed replay (adr-0001) takes as long as the audio it plays, and the conditions and voices (adr-0004, adr-0005) multiply every clip. The pinned baseline was written by fast pushes: the harness writes 16384-sample chunks as fast as the ring takes them (`NativeBridge~/harness/main.cpp:156`), and "The harness pushes faster than real time; verdicts are timing-independent" (`NativeBridge~/harness/README.md` :108). Device-like 20 ms chunking can shift vosk's endpoints compared with those large fast pushes. Whether every run is paced decides how long a condition sweep takes and whether a bench run ties exactly to the pinned transcripts (`Planning~/design-docs/audio-bench.md` §0, In 1).

## Options considered

1. **(A) Fast + paced.** Fast mode is unpaced and stamps audio time only; it gives accuracy and the audio-time latency part across all conditions. Paced mode pushes 20 ms chunks at real speed and adds the wall-clock part. Sweeps run at fast-mode speed, a fast run can tie exactly to the pinned baseline, and the compute part is measured where it is wanted. It costs two modes that must agree with each other.
2. **(B) Paced always.** One mode, and every number comes from device-like chunking. Every noise sweep would take hours, and no run ties exactly to the pinned baseline.

## The decision

**Option (A): a fast mode and a paced mode.** Fast mode is unpaced, audio time only, and gives accuracy and the audio-time latency part across all conditions; its path is the real native bridge, pushed in 16384-sample pushes as the verdict harness does (`NativeBridge~/harness/main.cpp:160`), since the pins come from that path, so fast and paced modes share one code path (adr-0002). The cost is that fast mode is tied to the harness's push size; changing it later means re-pinning. Paced mode is 20 ms chunks at real speed and adds the wall-clock part, from which the real-time factor comes. Paced-vs-fast transcript differences at 20 ms pacing are reported as a finding, not a self-check failure: device-like chunking can shift vosk's endpoints compared with the harness's large fast pushes. The 20 ms chunk is provisional → the tuning pass; the self-check's tolerance below is ruled, one decoder read.

## Why the rejected branches lost

- **(B) Paced always** — one mode, but every noise sweep would take hours, and there is no exact tie to the pinned baseline. What would have to change for it to win: a full paced sweep took minutes (e.g. cases run in parallel) and paced runs reproduced the pins.

## Consequences

- Fast mode carries a kill criterion for the bench itself: it must reproduce the harness's pinned transcripts exactly, or none of its numbers may be quoted (mirroring `Planning~/design-docs/joined-rig.md` :28).
- Paced mode carries the other: a self-check runs paced timing at fast mode's chunking, 16384-sample pushes, so the read boundaries match, and its audio-time stamps must match fast mode's to within one decoder read (the bridge reads 4096 samples at a time, `vosk_bridge.cpp:62`; 16384 = 4 × 4096; the joined rig records that per-record positions "may differ between runs by at most one decoder read", `Planning~/features/joined-rig-recogniser/architecture.md` :69); if not, the split into audio and compute parts is wrong and the bench stops (adr-0001). Differences between 20 ms pacing and fast mode are not a self-check failure; they stay the reported finding above. The cost is one extra paced pass per self-check run.
- The real-time factor and the compute part exist only for paced runs; a question about them needs a paced run, with its real-time cost.
- Whoever drives the bench chooses the mode per question (the design doc's §2).

## Status

Accepted on 2026-11-08 by the human's (the maintainer's) ruling in session; the ruling took the recommended option. The design doc is `Planning~/design-docs/audio-bench.md`; it was locked at G1 on 2026-11-08. Amended on 2026-11-08 with the maintainer's BAR gap rulings.
