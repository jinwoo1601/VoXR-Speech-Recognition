---
type: requirements
feature: bench-fast-replay
topic: audio-bench
status: accepted
updated: 2026-11-09
sources:
  - Planning~/design-docs/audio-bench.md
  - Planning~/design-docs/audio-bench/decisions/adr-0001-two-clocks-split.md
  - Planning~/design-docs/audio-bench/decisions/adr-0002-bridge-and-rig.md
  - Planning~/design-docs/audio-bench/decisions/adr-0003-fast-and-paced-modes.md
  - Planning~/design-docs/audio-bench/decisions/adr-0006-generator-speech-truth.md
  - Planning~/design-docs/audio-bench/decisions/adr-0008-report-and-stop.md
---

# feat-bench-fast-replay — requirements

Track **Full**. Design `audio-bench` (`Planning~/design-docs/audio-bench.md`), locked at G1 2026-11-08, merged as PR #197. Depends on no feature. Path `development`.

## 1. User story

- **S1** — As the maintainer measuring a Phase D change, I want a fast desktop bench that replays the TTS fixtures through the real native bridge and C# command recogniser, stamps each stage in audio time from end of speech, and scores command accuracy, WER and confidence calibration against the pinned transcripts, so that I can see a change's effect on end-of-speech → command latency (the part that carries over to Quest) and on accuracy before any Quest session, and stop a regression early.

## 2. Feature & scope

Backlog row (`audio-bench.md` §6):

> `bench-fast-replay` | Fast mode through the native bridge in 16384-sample pushes (adr-0002, adr-0003); audio-time stamps (adr-0001); the generator's speech-boundary truth in `generate.py` (adr-0006); the command stage through the joined rig (adr-0002); accuracy scoring: command classes, WER, ECE/NCE; reports naming their full config; the pinned-transcript self-check (adr-0003). Path: `development`. **Full.** | None.

**Agreed at G0 on 2026-11-08** by the maintainer, in session, as drafted; concurrent with `lab-next-level-probes` (independent; shares the confidence-calibration subject through its TTS NCE/ECE dry-run probe).

**In**

1. Fast mode: the TTS fixtures replayed unpaced into the real native bridge in the harness's 16384-sample pushes (adr-0002, adr-0003).
2. Audio-time stamps per stage — first partial, final = endpoint, command event — from the bridge's `consumed` sample counts (adr-0001).
3. The generator's speech-boundary truth: `Tests~/Fixtures/generate.py` writes each clip's speech start and end; every latency counts from end of speech (adr-0006).
4. The command stage: the run's timeline fed to the real C# recogniser through the joined rig, the command event stamped (adr-0002).
5. Accuracy scoring: the command classes (correct-fire, correct-no-fire, wrong-slots, wrong-intent, missed, false-fire) and from them intent accuracy, semantic accuracy and phantom rate; WER against the expected transcript; confidence calibration, per-word ECE and NCE.
6. Reports naming their full config — recogniser settings, endpointer profile, gain, voice, seed — and the corpus provenance.
7. The pinned-transcript self-check: fast mode must reproduce the harness's pinned transcripts exactly, or none of its numbers may be quoted (adr-0003).

**Out**

1. Paced 20 ms replay, wall-clock stamps and the real-time factor — `bench-paced-mode`.
2. The seeded condition pipeline (noise, echo, mic filtering, level, speed), the stand-in negative set and per-condition curves — `bench-conditions`.
3. Kokoro-82M voices — `bench-voices`.
4. Paired A/B runs, p50/p90/p99 and McNemar — `bench-ab-report`.
5. Developer usability; Quest's absolute timing; ranking two decoders or decoder configurations on TTS-only audio (C12) — the design's non-goals.
6. Left untouched: the bridge interface (a change found needed is a scope stop, design §4); Command 3 and its pinned transcripts ("re-pin, never edit"); the committed fixture WAVs.

## 3. Why — the value it delivers

Every Phase D change (R17, R12, R22, R41, R30) can have its end-of-speech → command latency and its accuracy measured on the desktop before a Quest session; nothing does that today — the native harness is pass/fail and records no timing (design §0 Prerequisites; `R6-evaluation-methodology.md` :300; `I3-latency-ux-redesign.md` :13). This feature gives the audio-time half, the deterministic part that transfers to Quest unchanged (adr-0001), and the accuracy axis. The thing protected when a tradeoff forces a cut: the numbers tie exactly to the pinned path (adr-0003).

## 4. Observable behaviour

1. Every manifest clip carries its speech start and end per spoken segment, written by the generator's path without re-rendering; the committed WAVs are byte-unchanged.
2. The maintainer runs fast mode naming a configuration: model, recogniser settings, endpointer profile, gain.
3. The bench replays all 52 cases into the real native bridge in 16384-sample pushes and checks each case's finals against the pins (`NativeBridge~/harness/expectations.json` `expectedFinals`); on any mismatch it names the differing cases, gives no numbers, and exits non-zero.
4. On a full match, each case's timeline is fed to the real C# recogniser through the joined rig and its command events are collected.
5. The report: a header naming the full config, the corpus provenance and the seed; per case its category, command class, finals count, WER, and the audio-time offset from end of speech (signed) of the first partial, each final and the command event; totals — class counts never netted, intent accuracy, semantic accuracy, phantom rate, WER, the silence case's stray-word count, ECE, NCE. Latency and accuracy stand side by side, never one score. The report is labelled TTS-only and provisional: it can stop a change, never ranks decoders, and proves no gain before R5.
6. A second run on the same inputs gives an identical report.

## 5. Functional requirements

**Ruled on 2026-11-08** by the maintainer, in session, at SETTLE: S1 confirmed as G0 recorded it; R1, the F1 truth check holds within one 20 ms frame; R2, the silence case is excluded from WER and its stray words are counted (F7); R3, latency per case only, no aggregate (F4); R4, all 52 cases (F2); R5, the WER reference is the spoken text — `phrase`, or `segments` joined in order — read as the design's "expected transcript", since `expectedTranscript` is empty for 39 of the 52 cases (`Tests~/Fixtures/audio/manifest.json`, read 2026-11-08) (F7); each took the recommended option; the rest settled as drafted.

| #  | Requirement | Story | Priority | Acceptance check | Source |
|----|-------------|-------|----------|------------------|--------|
| F1 | Speech-boundary truth per spoken segment for every clip. | S1 | Must | Every clip's recorded end lies within one 20 ms frame of its last sample above a stated silence floor before the padded tail; the silence case has none. | adr-0006; R1 |
| F2 | Fast replay of all 52 cases into the real native bridge, unpaced, in 16384-sample pushes. | S1 | Must | A green fast run over all 52 cases. | adr-0002; adr-0003; R4 |
| F3 | Pinned-transcript self-check: each case's finals equal its pins (`NativeBridge~/harness/expectations.json` `expectedFinals`) exactly, or no number is given, the differing cases are named and the run exits non-zero. | S1 | Must | The green run's self-check passes; with one pin altered in a scratch copy, the bench refuses numbers and names the case. | adr-0003; design §3 |
| F4 | Audio-time stamps of the first partial, each final (= endpoint) and the command event, from consumed samples, as signed offsets from end of speech, per case only, with no aggregate. | S1 | Must | The green run's report gives each case's signed offsets for the first partial, each final and the command event, and no aggregate. | adr-0001; R3 |
| F5 | Command stage: the real C# recogniser sources, through the joined rig, fed the run's timeline. | S1 | Must | The green run's report gives each case's command class from the command stage. | adr-0002 |
| F6 | Command classes correct-fire, correct-no-fire, wrong-slots, wrong-intent, missed and false-fire; from them intent accuracy, semantic accuracy and phantom rate. | S1 | Must | The green run's report gives the class counts, never netted, and intent accuracy, semantic accuracy and phantom rate. | design §1; `R6-evaluation-methodology.md` :351-357 |
| F7 | WER against the spoken text over the cases with speech; the silence case excluded, its output words counted apart. | S1 | Must | WER matches hand-computed toy cases; the report gives the silence case's stray-word count outside WER. | R5; R2 |
| F8 | Per-word ECE over 10 equal-width bins (the bin count provisional) and NCE, per-word correctness from F7's alignment. | S1 | Must | ECE and NCE match hand-computed toy cases. | design §1; `R6-evaluation-methodology.md` :206, :359 |
| F9 | Finals per utterance reported beside each case. | S1 | Must | The green run's report gives each case's finals count. | design §3 (split commands) |
| F10 | Every report names its full config — recogniser settings, endpointer profile, gain, voice, seed — and the corpus provenance. | S1 | Must | The report header names every F10 field. | design §3 |
| F11 | The two axes, latency and accuracy, side by side, never combined; the TTS-only provisional label. | S1 | Must | The green run's report shows both axes with no combined score and carries the label. | adr-0008; design §0 In 5 |
| F12 | Deterministic: identical inputs give an identical report. | S1 | Must | Two runs give identical reports. | design §3 Forward |
| F13 | Untouched: Command 3, its pins, the committed WAVs and the bridge interface; a bridge interface change found needed is a scope stop. | S1 | Must | Command 3 green; `git diff main` empty for the committed WAVs, `expectations.json` and the bridge's public interface. | design §4; adr-0001; adr-0002; adr-0008 |

## 6. Non-functional requirements

- **Performance:** the design rules no budget; none is set.
- **Robustness at the input boundary:** a clip without truth, a missing model, an absent bridge build, or an undefined metric (e.g. NCE where every word is correct, ECE with no words) each give an explicit message naming it, never a silent or made-up number.
- **Degraded dependencies:** a joined-rig input it cannot read stops the run with the reason.

## 7. Non-goals / deferred

- The G0 Out list (§2).
- Percentiles and summaries across runs or cases — `bench-ab-report`.
- Being a verification gate: the bench never is one (adr-0008).
- A latency target: none is set (design Status).

## 8. Dependencies & assumptions

- No feature dependency; uses the merged joined rig and scorer (PRs #177, #178) and the Windows native harness build (`verification` Command 3).
- Assumed: the maintainer runs it on the Windows desktop.
- The lab's Phase B TTS NCE/ECE probe has not run (2026-11-08); if it reports before F8 is built, its findings feed F8's definition (design Status).

## 9. Acceptance criteria (G2)

- [x] A green fast run over all 52 cases, with the self-check passing. (phase 3 gate V6; the review-fix CHECK R4 `52/52`, exit 0; record `memory/2026-11-09.md`, "review-cycle VERIFY to COMMIT")
- [x] Mutation: one pin altered in a scratch copy makes the bench refuse numbers and name the case. (phase 3 gate V10 and the review-fix CHECK R7: exit 3, the case named, no report)
- [x] Two runs give identical reports. (phase 3 gate V7 and the review-fix CHECK R5: `cmp=0`)
- [x] Command 3 is green. (phase 4 gate V13: exit 0, 52/52; the binding's `37/37` is stale, `expectations.json` holding 52 cases)
- [x] `git diff main` is empty for the committed WAVs, `expectations.json` and the bridge's public interface. (phase 4 gate V14; the review-fix CHECK R10; the G2 preflight at `7f10edc`, item 2)
- [x] WER, ECE and NCE match hand-computed toy cases. (the review-fix CHECK R6: `metrics-check` 26/26)
- [x] The F1 check passes on every clip. (phase 2 gate: `--bench truth-check` 52/52 against the committed WAVs)
- [x] Human-only verification: the maintainer inspects three clips' waveforms against their recorded speech end. (the maintainer at G2 on 2026-11-09, shown plots of `launch_one_missiles_target_hotel_one.wav`, `ladder_p_set_heading_two_seven_zero_gap200ms.wav` and `pause_lead_cease_fire.wav` with each `speech` start and end marked: "Looks ok.")
- [x] The report header names every F10 field. (phase 3 and phase 4 gates; the G2 preflight at `7f10edc`, item 2)

**Ruled at G2 on 2026-11-09** by the maintainer, in conversation (AskUserQuestion, "Accepted. Looks ok."): accepted at head `7f10edc`, with the third preflight in view — the first preflight's two gate-evidence bullets in the architecture doc's as-built deltas routed out in `7f10edc`, and the STATUS line brought current. The run from Phase 2 to the G2 packet went unattended under the maintainer's 2026-11-08 word; its fork-default takes were listed at G2.

## 10. Open questions

1. How the truth is derived for committed clips without re-rendering, and the silence floor value (the pause-lead clips carry a -60 dBFS noise floor) — the architecture.
2. Where the bench code lives (the rig sits under `Planning~/features/coverage-in-selection/ab-rig/`) — the architecture.
3. How the stamps are taken and recorded — the architecture (adr-0001).
4. How the timeline feeds the recogniser layer — the architecture (adr-0002).
5. Speech end on R5 human audio — owner R5.
