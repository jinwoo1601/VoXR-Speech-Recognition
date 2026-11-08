---
id: adr-0001
title: Stamp each stage on both clocks and split latency into an audio-time part and a compute part, over audio time only or wall time only
status: accepted
date: 2026-11-08
topic: audio-bench
---

# adr-0001 — Both clocks, with the split

## The fork

The scope agreed at G0 (`Planning~/design-docs/audio-bench.md` §0, In 1 and 2) asks the bench to timestamp each stage — end of speech → endpoint → final result → command event, plus time to first partial and the real-time factor — and asks its desktop numbers to compare settings fairly rather than give Quest's absolute timing, the human-only on-device latency stamp being the calibration (`05-rulings.md` :134). No wall-clock timing exists today: the verdict harness pushes 16384-sample chunks as fast as the ring takes them, and its only `<chrono>` uses are sleeps (`NativeBridge~/harness/main.cpp:156`, `:160`, `:177`); the bridge queues each result with `consumed` and `origin` in sample time and with no wall-clock stamp (`NativeBridge~/src/vosk_bridge.cpp:130-140`; `result_queue.h:9-13`); the joined rig measures its delay in sample time on a 90 Hz fake clock (`JoinedRecogniser.cs:38`, `:204`, `:301-303`). No doc states how the on-device stamp calibrates a desktop number; the nearest is "the desktop number is a lower bound on Quest cost" (`I3-latency-ux-redesign.md` :98). Which clock each stage is stamped on decides what part of a desktop number holds on Quest, what the device stamp has to calibrate, and which code path and run modes the bench needs, so the code path (adr-0002), the run modes (adr-0003) and the numbers' role (adr-0008) sit downstream of it. This is the keystone fork of the design.

## Options considered

1. **(A) Both clocks, with the split.** Real-speed replay in 20 ms chunks stamps each stage on audio time — the samples the decoder had consumed when the result was emitted (`consumed` ÷ sample rate) — and on wall time — a monotonic clock read at the bench's push and at its pop of each result. Latency is an audio-time part plus a compute part. It separates what transfers to Quest from what does not, and it measures compute lag and the real-time factor. It costs a real-speed replay path, which the toolkit does not have today: there is no 20 ms chunk path anywhere (recon of 2026-11-08).
2. **(B) Audio time only**, by extending the joined rig. It is fast and fully repeatable. It is blind to compute lag: it has no real-time factor and only a sample-time first partial, and it leaves Quest's largest unmeasured budget row unmeasured, the decoder catch-up that "adds ~0.5–1.0 s" (`I3-latency-ux-redesign.md`, budget row 7).
3. **(C) Wall time only.** Each stage gets one number, as the machine delivers it. That number mixes the parts that transfer to Quest with the parts that do not, and machine noise rides on every comparison.

## The decision

**Option (A): both clocks, with the split.** Real-speed 20 ms replay stamps each stage on audio time and on wall time, and each stage's latency is an audio-time part plus a compute part. The audio-time part — the endpointer wait, the buffer window, eager commit and similar — is deterministic, and it transfers to Quest unchanged, because Quest runs the same decoder on the same samples. The compute part is wall time − audio time: decode and queue lag, small on a desktop and labelled desktop-only. The human-only on-device latency stamp (`05-rulings.md` :134; T0-T5 per `03-critique-and-ranking.md` :177 and `I3-latency-ux-redesign.md` :336) calibrates the compute part only; it does not calibrate the audio-time part. This is the design's answer to the gap the recon found, that no doc states how the stamp calibrates desktop numbers. The 20 ms chunk is provisional → the tuning pass (it matches Quest capture, 960 frames at 48 kHz, `I3-latency-ux-redesign.md` :23).

## Why the rejected branches lost

- **(B) Audio time only** — it is blind to compute lag: no real-time factor, only a sample-time first partial, and Quest's largest unmeasured budget row ("adds ~0.5–1.0 s", `I3-latency-ux-redesign.md`, row 7) stays unmeasured. What would have to change for it to win: the compute part could be had from elsewhere.
- **(C) Wall time only** — a desktop number would mix transferable and non-transferable parts, and settings could not be compared clear of machine noise. What would have to change for it to win: the bench ran on the Quest itself, where nothing has to transfer.

## Consequences

- The bench needs a real-speed path into the decoder as the device runs it (adr-0002) and, because real speed costs real time, an unpaced mode beside it for the sweeps (adr-0003).
- The stamps are taken at the bench's push and pop, on the bridge's existing interface. The design intent is no bridge interface change; if the architecture finds one is needed, that is a scope stop.
- The split carries its own kill criterion: a self-check runs paced timing at fast mode's chunking, 16384-sample pushes, so the read boundaries match, and its audio-time stamps must match fast mode's to within one decoder read (4096 samples, `NativeBridge~/src/vosk_bridge.cpp:62`; the joined rig's "may differ between runs by at most one decoder read", `Planning~/features/joined-rig-recogniser/architecture.md` :69), or the split into audio and compute parts is wrong and the bench stops. Differences between 20 ms pacing and fast mode are not a self-check failure; they stay the reported finding of adr-0003. The cost is one extra paced pass per self-check run.
- The command event is stamped on both clocks in paced mode: the joined rig delivers each result at its recorded paced wall-arrival time, on its frame clock, and the command stage's compute part is its paced time − its fast time. The C# side's own compute, the recogniser's frame cost, is modelled by the rig's frame clock, not measured live (adr-0002).
- Wall numbers are never compared across machines: A and B run interleaved, on one machine, in one session, repeated, and the compute part is reported with its run-to-run spread.
- The compute part measured on a desktop is not Quest's; it stays labelled desktop-only until the on-device stamp calibrates it, and the bench does not decide how that stamp is taken.
- Left open, for the architecture doc: how the stamps are taken and recorded.

## Status

Accepted on 2026-11-08 by the human's (the maintainer's) ruling in session; the ruling took the recommended option. The design doc is `Planning~/design-docs/audio-bench.md`; it is not yet locked at G1. Amended on 2026-11-08 with the maintainer's BAR gap rulings; still not locked.
