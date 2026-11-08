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
  - Planning~/research/2026-09-05-next-level/00-brief.md (goals)
  - Planning~/research/2026-09-05-next-level/research/R6-evaluation-methodology.md (metrics, augmentation)
  - Planning~/research/2026-09-05-next-level/ideas/I3-latency-ux-redesign.md (budget, stamps)
  - Planning~/research/2026-09-05-next-level/03-critique-and-ranking.md (C12, R-items)
  - Planning~/design-docs/audio-bench/decisions/adr-0001-two-clocks-split.md
  - Planning~/design-docs/audio-bench/decisions/adr-0002-bridge-and-rig.md
  - Planning~/design-docs/audio-bench/decisions/adr-0003-fast-and-paced-modes.md
  - Planning~/design-docs/audio-bench/decisions/adr-0004-seeded-on-the-fly-conditions.md
  - Planning~/design-docs/audio-bench/decisions/adr-0005-kokoro-voices.md
  - Planning~/design-docs/audio-bench/decisions/adr-0006-generator-speech-truth.md
  - Planning~/design-docs/audio-bench/decisions/adr-0007-stand-in-negatives.md
  - Planning~/design-docs/audio-bench/decisions/adr-0008-report-and-stop.md
  - Planning~/design-docs/joined-rig/decisions/adr-0003-outcome-classes-never-netted.md
  - "Session direction 2026-11-08 (G0)"
  - "Session direction 2026-11-08 (settle)"
  - "Session direction 2026-11-08 (BAR gap rulings)"
---

# Design: The Desktop Audio Bench

**Branch:** `design-audio-bench`
**Origin:** the maintainer's direction of 2026-11-08: measure speech-recognition response time and accuracy without a Quest, so every Phase D change can be measured on the desktop.
**Status:** draft. G0 agreed 2026-11-08; model settled 2026-11-08 (adr-0001 through adr-0008); not locked.

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

This system is the desktop audio bench: a separate tool that replays the TTS fixtures, under seeded conditions and in several voices, through the real decoder and the real C# command recogniser, and stamps each stage on two clocks, so that every Phase D change (R17, R12, R22, R41, R30; `memory/STATUS.md`) can be measured on the desktop before a Quest session (§0). It carries the maintainer's two axes (`00-brief.md` :40-41): latency, "time from end of speech (or from the point the command is determinable) to the command event", and accuracy, "real out-of-grammar/noise rejection; calibrated confidence; robustness to the Quest mic, exertion, and game audio; fewer phantom or wrong commands." It is needed now because "There is no path in the current toolkit by which a decoder-side, grammar-side, or DSP-side improvement could be demonstrated at all" (`R6-evaluation-methodology.md` :300), and "None of this should ship ahead of the instrument" (`I3-latency-ux-redesign.md` :13). It resolves eight forks, recorded as adr-0001 through adr-0008; the keystone is the clock (adr-0001). Two facts govern everything below: no TTS-only comparison ranks two decoders (C12, `03-critique-and-ranking.md` :164-165; `05-rulings.md` :112), and no wall-clock timing exists anywhere today — the harness pushes faster than real time and its verdicts are timing-independent (`NativeBridge~/harness/README.md` :108), and no result the bridge queues carries a wall-clock stamp. No locked design doc states a latency target (`Planning~/design-docs/endpointer-profile.md` :65); the bench sets none.

> **What's decided.** The bench stamps each stage of each case on two clocks, audio time and wall time, and splits every stage's latency into an audio-time part, which is deterministic and transfers to Quest unchanged, and a compute part, which is desktop-only and is what the human-only on-device stamp calibrates (adr-0001). Both modes replay into the real native bridge, fast mode with the harness's 16384-sample pushes, and the command stage runs through the real C# recogniser sources in the joined rig, fed that run's timeline; the Unity Editor is not the path (adr-0002). It runs in two modes: fast, unpaced and in audio time only, for accuracy and the audio-time part across all conditions; and paced, in 20 ms chunks at real speed, for the wall-clock part (adr-0003). Conditions — speed, room echo, noise, mic-like filtering, level — are generated on the fly from a fixed seed, and only clean renders are committed (adr-0004). Kokoro-82M adds voices beside the baseline Piper voice, whose pins never move (adr-0005). End of speech is the generator's truth (adr-0006). A stand-in negative set gives a provisional false-fire rate until R4 replaces it (adr-0007). The numbers report and can stop a change as a regression; they never rank decoders, never prove a gain before R5 human audio, and never gate verification (adr-0008). All numbers on this page are **provisional → the tuning pass** (the design intent is the load-bearing part).

## 1. The model

The bench's model, per run and per case (design intent, not yet built).

**The facts it rests on** (recon of 2026-11-08).

- The verdict harness pushes 16384-sample 48 kHz chunks as fast as the ring takes them, with a 5 ms back-off; its only `<chrono>` uses are sleeps, and its `--dump` records each result's `consumed`/`origin` in sample time (`NativeBridge~/harness/main.cpp:156`, `:160`, `:177`, `:115-117`).
- The bridge's recognition thread reads up to 4096 samples (~85 ms) at a time, sleeps 10 ms when the ring is empty, and advances its consumed counter by what it read (`NativeBridge~/src/vosk_bridge.cpp:62`, `:87`, `:106-111`). The bridge has no endpoint logic of its own — vosk's model.conf rules decide (`Runtime/EndpointerOverlay.cs:139-141`) — and when vosk declares an endpoint the final is pushed with `consumed`/`origin` (`vosk_bridge.cpp:130-140`). Words carry start, end and confidence (`:243`). No queued result carries a wall-clock stamp (`result_queue.h:9-13`).
- The C# recogniser's only time source is `Time.time` (`VoxrCommandRecogniser.cs:694`); a command fires through `OnCommandRecognised` (`:1516`). The Editor runs a different native path, with no bridge thread or queue, playing back a 4800-sample (100 ms) chunk per frame, not real time (`EditorMicBackend.cs:456`, `:531`).
- The joined rig drives staged copies of the real Runtime recogniser sources on a 90 Hz fake clock (`JoinedRecogniser.cs:38`, `:113`, `:204`); its delay is first fire − end of audio, which includes the fixture's 1.0 s tail (`:301-303`). Its scorer's classes are correct-fire, correct-no-fire, wrong-slots, wrong-intent, missed and false-fire.
- The fixtures are 52 TTS WAVs in one Piper voice, `en_US-lessac-medium`, rendered by `Tests~/Fixtures/generate.py` with a 0.3 s lead and a 1.0 s tail, and not byte-deterministic across runs (`generate.py:8-14`).
- No WER, edit-distance, ECE or calibration code exists, and no 20 ms chunk path exists anywhere; live Quest capture is 960 frames ≈ 20 ms (`I3-latency-ux-redesign.md` :23).
- Quest's decoder catch-up is the latency budget's unmeasured row, "Largest hole in the budget" (`I3-latency-ux-redesign.md` :23-39), and no XR2 real-time factor is published (`I1-recognition-core-redesign.md` :284).

**State, per run.** A set of cases, each a clip × condition × voice; one configuration, A or B; one mode, fast or paced (adr-0003). Per case, the bench records a timeline of events, each stamped on two clocks (adr-0001):

- **Audio time:** the samples the decoder had consumed when the result was emitted (`consumed` ÷ sample rate).
- **Wall time:** a monotonic clock read at the bench's push and at its pop of each result.

**The events.**

| Event | What it is | Audio time | Wall time |
|---|---|---|---|
| T_speech_end (truth) | Where speech ends in the clip, written by the generator, which placed the words (adr-0006). | the truth itself | — |
| First partial | The decoder's first partial result. | both modes | paced mode |
| Final = endpoint | vosk declares the endpoint internally; its only observable is the final result, so the two are one event. | both modes | paced mode |
| Command event | The real C# recogniser fires `OnCommandRecognised`, fed the run's timeline (adr-0002). Its wall stamp: in paced mode the joined rig delivers each result at its recorded paced wall-arrival time, on its frame clock, and the command stage's compute part is its paced time − its fast time. Cost: the C# side's own compute, the recogniser's frame cost, is modelled by the rig's frame clock, not measured live. | both modes | paced mode |
| Real-time factor | Decode time ÷ audio duration. | — | paced mode |

**The latency split.** The latency of each stage, counted from T_speech_end, is an audio-time part plus a compute part (adr-0001).

| Part | What it holds | How it behaves | On Quest | Calibrated by |
|---|---|---|---|---|
| Audio-time part | The endpointer wait, the buffer window, eager commit and similar. | Deterministic. | Transfers unchanged: Quest runs the same decoder on the same samples. | Not by the on-device stamp. |
| Compute part | Wall time − audio time: decode and queue lag. | Small on a desktop; carries machine noise. | Labelled desktop-only. | The human-only on-device latency stamp (`05-rulings.md` :134; T0-T5 per `03-critique-and-ranking.md` :177 and `I3-latency-ux-redesign.md` :336). |

This is the design's answer to the gap the recon found: no doc states how the on-device stamp calibrates a desktop number, the nearest being "the desktop number is a lower bound on Quest cost" (`I3-latency-ux-redesign.md` :98). The stamp calibrates the compute part only.

**Accuracy, per case.** The metric definitions are R6's; thresholds are not the bench's (§4).

- The command classes the joined rig already uses: correct-fire, correct-no-fire, wrong-slots, wrong-intent, missed, false-fire.
- From them, intent accuracy, semantic accuracy (slots too) and phantom rate (`R6-evaluation-methodology.md` :351-357).
- WER against the expected transcript.
- Confidence calibration: per-word ECE over equal-width bins, and NCE (`R6-evaluation-methodology.md` :206, :359).
- False fires on the negative set (adr-0007).

**One case, resolved.**

1. The clean render is read, with the speech start and end its generator wrote (adr-0006).
2. The case's condition is applied from the run's fixed seed — speed change, room echo, noise, mic-like filtering, level into the Quest mic range — and the speech boundaries are kept (adr-0004).
3. The audio is replayed into the real native bridge: in fast mode, unpaced, in the harness's 16384-sample pushes (`NativeBridge~/harness/main.cpp:160`); in paced mode, in 20 ms chunks at real speed (adr-0002, adr-0003). Each result is stamped in audio time and, in paced mode, in wall time.
4. The run's timeline is fed to the real C# recogniser through the joined rig, and the command event is stamped; in paced mode the rig delivers each result at its recorded paced wall-arrival time, on its frame clock, so the command event is stamped on both clocks (adr-0002).
5. The case is scored: its command class, its WER, its word confidences for calibration, and each stage's latency from end of speech, split into its two parts; finals per utterance are recorded beside it (§3).

## 2. The decisions it puts to whoever drives it

The driver is whoever measures a Phase D change on the bench; the bench does not decide which settings get compared (§4). It puts four choices to the driver (design intent, not yet built).

1. **Which A/B configuration.** The information available is the Phase D item's own design, which owns its kill threshold — e.g. R17's ~1 % contradiction rate (`03-critique-and-ranking.md` :238) — and the full config every report names (§3). The opportunity cost is runs: every pair is a run per mode, and a paced run takes real time. No pair dominates because the comparison answers the item's question, not the bench's, and on TTS it can stop a change but never rank two decoders (C12; adr-0008). The record says nothing more on how a pair is chosen; that stays open (Status / openness).
2. **Fast or paced.** Fast mode gives accuracy and the audio-time part across all conditions, and ties exactly to the pinned transcripts; paced mode adds the wall-clock part and the real-time factor, and shows whether device-like chunking shifts vosk's endpoints (adr-0003). The opportunity cost is time against coverage: pacing every condition would take hours, and fast mode is blind to the compute part. Neither dominates: a change to the endpointer wait or the buffer window lives in the audio-time part, which fast mode gives; a question about decode cost, as R41's measurement-only screen is (`05-rulings.md` :59; `03-critique-and-ranking.md` :215), needs the paced run.
3. **Which conditions and voices.** The information available is the condition steps (provisional; recipe per `R6-evaluation-methodology.md` :191-196), the baseline voice whose pins never move, and whether the human has downloaded the optional MUSAN and SLR28 add-on and the Kokoro model (adr-0004, adr-0005). The opportunity cost is that cases grow as clip × condition × voice while the evidence does not: 16 phrases × 12 conditions is not 192 samples, and significance stays counted per phrase (`R6-evaluation-methodology.md` :198). None dominates: the clean baseline voice ties to the pins and shows no condition effect; harder conditions and other voices show where a change breaks, but augmented numbers rank conditions, not field rates (`R6-evaluation-methodology.md` :451), and digital mixing is "a lower bound on difficulty, not a model of it" (`I1-recognition-core-redesign.md` :274).
4. **How many repeats.** The information available is the compute part's run-to-run spread, which every report carries (§3), and McNemar's minimum of discordant pairs (6, per `R6-evaluation-methodology.md` :369). The opportunity cost is time: each paced repeat runs at real speed. The record leaves the repeat count provisional → the tuning pass and gives no rule for choosing it; that stays open (Status / openness).

## 3. Dynamics & failure modes

**Forward** (design intent, not yet built). In fast mode a case is fixed by its clip, condition, voice, seed and configuration, so two runs are identical and the audio-time part of each stage is read straight off the run. In paced mode the same samples arrive 20 ms at a time, and what wall time adds on top is the compute part; the self-check that guards the split runs paced timing at fast mode's chunking, 16384-sample pushes, so the read boundaries match, and its audio-time stamps must agree with fast mode's to within one decoder read (4096 samples, `NativeBridge~/src/vosk_bridge.cpp:62`), while differences at 20 ms pacing are not a self-check failure but the finding below. Device-like chunking can shift vosk's endpoints compared with the harness's large fast pushes; such transcript differences are reported as a finding (adr-0003). Because the audio-time part transfers to Quest unchanged, a change that moves only that part shows its Quest effect on the desktop; a change that moves the compute part shows only a desktop effect until the on-device stamp calibrates it (adr-0001).

**Attacks on the rules, and what kills each.**

| Failure mode | What it looks like | Killed by |
|---|---|---|
| Pseudo-replication | 16 phrases × 12 conditions read as 192 samples. | Significance counted per phrase (`R6-evaluation-methodology.md` :198); results as curves per condition, never pooled (`R6-evaluation-methodology.md` :336; adr-0004). |
| Tuning to the TTS voice | A setting is chosen because it suits the TTS voice. | A TTS result can stop a change, never prove a gain; a setting chosen on the bench stays provisional until R5 (adr-0008). |
| Machine noise in wall time | A and B differ in wall time because the machine did. | A and B run interleaved, on one machine, in one session, repeated; the compute part is reported with its run-to-run spread; wall numbers are never compared across machines (adr-0001). |
| Hiding a latency-vs-accuracy trade | A change buys latency with accuracy, and one score hides it. | The two axes are always reported side by side, never combined into one score, consistent with the joined-rig recogniser's rejection of a combined latency score (`Planning~/design-docs/joined-rig/decisions/adr-0003-outcome-classes-never-netted.md`). |
| A stand-in negative set that's too easy | The stand-in set shows zero false fires. | Zero reads as "set too easy", not "zero" (R4's cheapest-experiment logic, `03-critique-and-ranking.md` :244; adr-0007). |
| Hidden config differences | A and B differ in something the report does not name. | Every report names its full config — recogniser settings, endpointer profile, gain, voice, condition, seed — and the corpus provenance (`Planning~/design-docs/joined-rig.md` :130; the manifest's provenance string). |
| Fast mode drifts from the pins | Fast mode's transcripts differ from the harness's pinned transcripts. | Fast mode runs the path the pins come from, the real native bridge in the harness's 16384-sample pushes (`NativeBridge~/harness/main.cpp:160`; adr-0002), so the tie can hold. Self-check: fast mode must reproduce the pinned transcripts exactly, or none of its numbers may be quoted (mirroring `Planning~/design-docs/joined-rig.md` :28; adr-0003). |
| A wrong split | Paced mode's audio-time stamps disagree with fast mode's. | Self-check: paced timing runs at fast mode's chunking, 16384-sample pushes, so the read boundaries match, and its audio-time stamps must match fast mode's to within one decoder read (4096 samples, `NativeBridge~/src/vosk_bridge.cpp:62`; the joined rig's "may differ between runs by at most one decoder read", `Planning~/features/joined-rig-recogniser/architecture.md` :69); if not, the split into audio and compute parts is wrong and the bench stops. Differences between 20 ms pacing and fast mode are not a self-check failure; they are the reported finding of adr-0003 (adr-0001, adr-0003). |
| Split commands | One utterance produces several finals, so "the final" names no single moment. | Latency is measured to the command event, and finals per utterance are reported beside it, as the joined rig does. |

## 4. Boundaries & couplings

The bench's fence (design intent, not yet built).

- **Owns:** replay in both modes, the stamps, the condition pipeline, scoring and reports.
- **Consumes:**
  - The bridge's existing interface: push, results with `consumed`/`origin`, the consumed counter. The design intent is no bridge interface change; stamps are taken at the bench's push and pop. If the architecture finds a change is needed, that is a scope stop (adr-0001, adr-0002).
  - The real C# recogniser sources, through the joined rig (adr-0002).
  - The clean TTS renders and `Tests~/Fixtures/generate.py`, which gains the speech-boundary truth (adr-0006).
- **Leaves untouched:** Command 3 and its pins ("re-pin, never edit", `05-rulings.md` :98; adr-0008).
- **Hands off:** each Phase D item's kill threshold, e.g. R17's ~1 % contradiction rate, to that item's own design; the bench supplies the measurement (adr-0008).
- **Does not decide:** the R5 and R4 corpora (human recording, human-only); the on-device stamp method; which settings get compared; any latency target.

## 5. What it delivers

What the bench gives a Phase D change (design intent, not yet built). Every Phase D change can be measured on the desktop, before a Quest session:

- its effect on end-of-speech → command latency, split into a part that holds on Quest and a part the device stamp calibrates (adr-0001);
- its effect on accuracy, false fires and confidence calibration, across noise, echo, mic, level and voice conditions (adr-0004, adr-0005, adr-0007);
- whether it regresses anything (adr-0008).

It can stop a bad change early. Gains and decoder rankings wait for R5 human audio. This is the instrument that `R6-evaluation-methodology.md` :300 found missing and that `I3-latency-ux-redesign.md` :13 asks for ahead of the changes, carrying both of the maintainer's axes (`00-brief.md` :40-41).

## Status / openness

- **Status: draft.** G0 agreed 2026-11-08. The model was settled with the human (the maintainer) on 2026-11-08, every fork taking the recommended option, and its forks are recorded as eight ADRs, adr-0001 through adr-0008; the safeguards and self-checks of §3 were agreed as drafted. Not locked; G1 locks it.
- **The goal:** the maintainer's two axes (`00-brief.md` :40-41) and the G0 scope (§0). No locked latency target exists (`Planning~/design-docs/endpointer-profile.md` :65); the bench sets none.
- **All numbers provisional → the tuning pass:**
  - the 20 ms chunk (it matches Quest capture, 960 frames at 48 kHz, `I3-latency-ux-redesign.md` :23);
  - speed steps {0.9, 1.0, 1.1}; RT60 0.2–0.5 s; SNR steps {20, 15, 10, 5} dB; level range [0.04, 0.4]; game-audio SIR steps (`I1-recognition-core-redesign.md` :264);
  - the voice count (~12 per R6);
  - the repeat count per A/B; the percentiles p50/p90/p99; the ECE bin count (10 per R6); McNemar's minimum discordant pairs (6 per `R6-evaluation-methodology.md` :369).
- **Settled, not provisional (2026-11-08):** the paced-vs-fast self-check's tolerance is one decoder read (4096 samples, `NativeBridge~/src/vosk_bridge.cpp:62`), the self-check running at fast mode's 16384-sample chunking.
- **Hard dependencies / open:**
  - RESOLVED — the clock: both clocks, with the split (adr-0001).
  - RESOLVED — the code path: the native bridge + the joined rig (adr-0002).
  - RESOLVED — the run modes: fast + paced (adr-0003).
  - RESOLVED — the test audio: generated on the fly, seeded (adr-0004).
  - RESOLVED — the voices: Kokoro-82M beside the baseline Piper voice (adr-0005).
  - RESOLVED — end of speech: the generator's truth (adr-0006).
  - RESOLVED — negatives before R4: a stand-in set (adr-0007).
  - RESOLVED — the numbers' role: report + stop only (adr-0008).
  - How to mark speech end on R5 human recordings — owner: the R5 corpus work (human recording).
  - Whether the stand-in negative set needs game audio, for which the maintainer's title audio is a human-supplied input — owner: `bench-conditions`, which builds the negative set; the maintainer supplies the title audio.
  - Where the bench's code lives — the joined rig sits under `Planning~/features/coverage-in-selection/ab-rig/`, outside the layout's Source and Tests slots — owner: the architecture.
  - How the stamps are taken and recorded (adr-0001) — owner: the architecture.
  - How the paced push reaches the bridge, and how the run's timeline is fed to the recogniser layer (adr-0002) — owner: the architecture.
  - The lab branch's Phase B "TTS NCE/ECE dry run" probe overlaps the calibration metric: if it runs first, its findings feed this metric's definition — owner: `bench-fast-replay`, which builds the calibration metric and reads the lab's findings if they exist by then.
  - How the driver chooses the A/B pair beyond the Phase D item's own design, and how the repeat count is chosen: the record is thin on both (§2) — owner: for the A/B pair, each Phase D item's own design, as the bench already hands off kill thresholds; for the repeat count, the tuning pass.
  - Human steps: the MUSAN and SLR28 download (adr-0004) and the Kokoro model download (adr-0005) — owner: the maintainer.
- **Feature candidates (→ the backlog at G1):** five, in dependency order, each on the `development` path and each **Full**: the design leaves code location, stamp recording and the timeline feed to the architecture, so each build writes its own requirements and architecture docs.
  - `bench-fast-replay`: fast mode through the native bridge in 16384-sample pushes (adr-0002, adr-0003); audio-time stamps (adr-0001); the generator's speech-boundary truth in `generate.py` (adr-0006); the command stage through the joined rig (adr-0002); accuracy scoring: command classes, WER, ECE/NCE; reports naming their full config; the pinned-transcript self-check (adr-0003). Depends on: none.
  - `bench-conditions`: the seeded on-the-fly condition pipeline (adr-0004); the stand-in negative set (adr-0007); per-condition curves. Depends on: `bench-fast-replay`.
  - `bench-voices`: Kokoro-82M voices beside the baseline Piper voice (adr-0005). Depends on: `bench-fast-replay`.
  - `bench-paced-mode`: paced 20 ms replay with wall stamps and the real-time factor (adr-0001, adr-0003); the matched-chunk split self-check; the command event's paced-arrival wall stamp (adr-0002). Depends on: `bench-fast-replay`.
  - `bench-ab-report`: paired A/B runs, interleaved and repeated; p50/p90/p99 per stage; McNemar flips; report and stop (adr-0008). Depends on: `bench-paced-mode`, `bench-conditions`.

## Related

[adr-0001](audio-bench/decisions/adr-0001-two-clocks-split.md) · [adr-0002](audio-bench/decisions/adr-0002-bridge-and-rig.md) · [adr-0003](audio-bench/decisions/adr-0003-fast-and-paced-modes.md) · [adr-0004](audio-bench/decisions/adr-0004-seeded-on-the-fly-conditions.md) · [adr-0005](audio-bench/decisions/adr-0005-kokoro-voices.md) · [adr-0006](audio-bench/decisions/adr-0006-generator-speech-truth.md) · [adr-0007](audio-bench/decisions/adr-0007-stand-in-negatives.md) · [adr-0008](audio-bench/decisions/adr-0008-report-and-stop.md) · [joined-rig.md](joined-rig.md) (`:28`, `:130`) · [joined-rig adr-0003](joined-rig/decisions/adr-0003-outcome-classes-never-netted.md) · [endpointer-profile.md](endpointer-profile.md) (`:65`) · [00-brief.md](../research/2026-09-05-next-level/00-brief.md) (`:40-41`) · [03-critique-and-ranking.md](../research/2026-09-05-next-level/03-critique-and-ranking.md) (C12, `:164-165`; `:177`; `:215`; `:238`; `:244`) · [05-rulings.md](../research/2026-09-05-next-level/05-rulings.md) (`:59`, `:98`, `:112`, `:134`) · [R6-evaluation-methodology.md](../research/2026-09-05-next-level/research/R6-evaluation-methodology.md) (`:191-196`, `:198`, `:206`, `:300`, `:336`, `:351-359`, `:369`, `:451`, `:456`) · [I3-latency-ux-redesign.md](../research/2026-09-05-next-level/ideas/I3-latency-ux-redesign.md) (`:13`, `:23-39`, `:98`, `:336-337`) · [I1-recognition-core-redesign.md](../research/2026-09-05-next-level/ideas/I1-recognition-core-redesign.md) (`:264`, `:274`, `:284`) · [joined-rig-recogniser requirements](../features/joined-rig-recogniser/requirements.md) (`:89`) · [joined-rig-recogniser architecture](../features/joined-rig-recogniser/architecture.md) (`:69`, `:150`) · [harness](../../NativeBridge~/harness/) (`main.cpp`, `README.md` :108) · [vosk_bridge.cpp](../../NativeBridge~/src/vosk_bridge.cpp) · [JoinedRecogniser.cs](../features/coverage-in-selection/ab-rig/JoinedRecogniser.cs) (`:301-303`) · [generate.py](../../Tests~/Fixtures/generate.py) · [STATUS.md](../../memory/STATUS.md)
