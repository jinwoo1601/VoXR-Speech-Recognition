---
type: system
topic: audio-bench
tags: [latency, accuracy, bench, harness]
status: draft
updated: 2026-11-08
sources:
  - Planning~/research/2026-09-05-next-level/05-rulings.md (C12; §5; R41 screen; R5, R4 corpora; human-only items)
  - NativeBridge~/harness/ (verification Command 3)
  - Planning~/design-docs/joined-rig.md (R1)
  - "Session direction 2026-11-08 (G0)"
---

# Design: The Desktop Audio Bench

**Branch:** `design-audio-bench`
**Origin:** the maintainer's direction of 2026-11-08: measure speech-recognition response time and accuracy without a Quest, so every Phase D change can be measured on the desktop.
**Status:** draft, G0 agreed 2026-11-08; not locked.

**Scope, agreed at G0 (2026-11-08):** a response-time harness that replays audio at real speed in 20 ms chunks, as a mic would deliver it, and timestamps each stage — end of speech → endpoint → final result → command event — plus time to first partial and the decoder's speed against real time (real-time factor); desktop numbers that compare settings fairly rather than give Quest's absolute timing, the human-only on-device latency stamp being the calibration (`05-rulings.md` :134); accuracy measured as command accuracy, false triggers on non-command audio, word error rate, and how well confidence scores match real accuracy (calibration); harder test audio, the TTS fixtures with noise, room echo and mic-like filtering added, plus several voices and levels; and accuracy numbers that stay provisional until the R5 human recordings exist.

> **What's decided going in.** The human (the maintainer) ruled at G0 on 2026-11-08: the work runs on the **design track**, branch `design-audio-bench`, with the scope above agreed as proposed, and it runs concurrently with `lab-next-level-probes`, the only other active branch, which is independent (it writes only under `.scratch/lab-next-level-probes/` and merges nothing) though it shares a subject, since its Phase B "TTS NCE/ECE dry run" probe also touches confidence calibration. The topic exists because of the human's direction on testing without a Quest: "i don't care about the developer usability. that i can test myself. what i'm talking about is on audio, speech recognition, testing response time and accuracy." The bench is needed so that every Phase D change is measured without a Quest: the desktop native harness that exists today is pass/fail and records no timing.
>
> **Non-goals:** developer usability; Quest's absolute timing; ranking two decoders, or two decoder configurations, on TTS-only audio (standing rule, critique C12) (see §0).

---

## 0. Scope (agreed at G0)

**In**

1. **Response time:** a harness that replays audio at real speed in 20 ms chunks, as a mic would deliver it. It timestamps each stage: end of speech → endpoint → final result → command event, plus time to first partial and the decoder's speed against real time (real-time factor).
2. **What the desktop numbers are for:** comparing settings fairly, not giving Quest's absolute timing. The human-only on-device latency stamp is the calibration, per `Planning~/research/2026-09-05-next-level/05-rulings.md` ("Human-only items now scheduled", :134: "the recording pilot and campaign; the one adb session on Quest audio effects/sources (R4 rec. 4); any on-device latency stamp").
3. **Accuracy:** command accuracy, false triggers on non-command audio, word error rate, and how well confidence scores match real accuracy (calibration).
4. **Harder test audio:** the TTS fixtures with noise, room echo and mic-like filtering added, plus several voices and levels.
5. **Provisional status:** accuracy numbers stay provisional until the R5 human recordings exist. `Tests~/Fixtures/audio/human` holds only `.gitkeep` today (read 2026-11-08), and R5 (human corpus) and R4 (negative corpus) have not started.

**Out**

- Developer usability: the human tests that themselves (quote above).
- Quest's absolute timing. The desktop numbers compare settings only; the on-device stamp is human-only.
- Ranking two decoders, or two decoder configurations, on TTS-only audio (standing rule, critique C12, reaffirmed by ruling 6: `05-rulings.md` :112, "No TTS-only comparison ranks two decoders").

**Prerequisites**

- The desktop native harness, `NativeBridge~/harness/` (the `verification` binding's Command 3): it runs the cases in `NativeBridge~/harness/expectations.json` through the real decoder and front end, pinned to exact transcripts. It is pass/fail and records no timing. It holds **52** cases (read from `expectations.json` `cases` on 2026-11-08): 16 single-command TTS cases, the silence negative among them, 15 pause-lead cases and 21 pause-ladder cases.
- R1, the joined rig (bridge, recogniser and parser together): merged, PRs #177 and #178.
- The constraining rulings, all in `Planning~/research/2026-09-05-next-level/05-rulings.md`: C12 (above); the latency analysis of the VAD pre-roll and hangover in §5 (:47: onset costs nothing with 200–300 ms of pre-roll; offset is a net cut when the VAD owns the end-of-utterance decision); the R41 `0.22-lgraph` screen, "as a measurement only" (:59); and the R5 human corpus and R4 negative corpus, both ruled GO (:119, :126).

---
