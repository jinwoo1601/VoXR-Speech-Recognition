---
type: requirements
feature: bench-conditions
topic: audio-bench
status: accepted
updated: 2026-11-10
sources:
  - Planning~/design-docs/audio-bench.md
  - Planning~/design-docs/audio-bench/decisions/adr-0004-seeded-on-the-fly-conditions.md
  - Planning~/design-docs/audio-bench/decisions/adr-0007-stand-in-negatives.md
  - Planning~/features/bench-fast-replay/requirements.md
---

# feat-bench-conditions — requirements

Track **Full**. Design `audio-bench` (`Planning~/design-docs/audio-bench.md`), locked at G1 2026-11-08, merged as PR #197. Depends on `bench-fast-replay` (merged as PR #198, `63434cb`). Path `development`.

## 1. User story

- **S1** — As the maintainer, I want the bench to replay the fixtures under seeded noise, echo, mic, speed and level conditions, plus a stand-in negative set, and report accuracy and false fires as one curve per condition, so that I can see where a recognition change breaks and whether it adds phantom commands, before a Quest session.

## 2. Feature & scope

Backlog row (`audio-bench.md` §6):

> `bench-conditions` | The seeded on-the-fly condition pipeline (adr-0004); the stand-in negative set (adr-0007); per-condition curves. Path: `development`. **Full.** | `bench-fast-replay`

**Agreed at G0 on 2026-11-09** by the maintainer, in session, as drafted; concurrent with `lab-next-level-probes` (independent; the lab writes only under `.scratch/lab-next-level-probes/` and merges nothing).

**In**

1. The seeded on-the-fly condition pipeline: per case and from a fixed seed — speed change, synthetic room echo, generated noise at SNR steps, mic-like filtering, and level into the Quest mic range; each clip's speech boundaries kept (adr-0006); only clean renders committed (adr-0004). The steps — speed {0.9, 1.0, 1.1}, RT60 0.2–0.5 s, SNR {20, 15, 10, 5} dB, level [0.04, 0.4] — are provisional → the tuning pass.
2. MUSAN (CC BY 4.0) noise and OpenSLR SLR28 room impulse responses (Apache-2.0) used as an optional add-on where the maintainer has downloaded them; until then noise and echo are the generated and synthetic kinds only (adr-0004).
3. Every report names the condition and the seed it ran with (adr-0004; design §3, hidden config differences).
4. The stand-in negative set: generated noise-only clips at each condition, silence, and TTS phrases that are not commands (out-of-set words, near-misses); its false-fire rate marked provisional and stand-in until R4 replaces it; zero false fires reads as "set too easy", not "zero" (adr-0007).
5. Per-condition curves — e.g. accuracy against SNR — never pooled; significance counted per phrase, not per phrase × condition (adr-0004; `R6-evaluation-methodology.md` :198, :336).
6. The open item this feature owns (design :208): whether the stand-in negative set needs game audio — settled in requirements; if it does, the maintainer supplies the title audio.

**Out**

1. Kokoro-82M voices — `bench-voices`; paired A/B runs, p50/p90/p99 and McNemar — `bench-ab-report`.
2. The R4 negative corpus and the R5 human corpus (human recording); committing conditioned audio (adr-0004).
3. The MUSAN and SLR28 download itself — a human step, the maintainer's (adr-0004).
4. Ranking two decoders or decoder configurations on TTS-only audio (C12), and gating verification (adr-0008); developer usability and Quest's absolute timing — the design's non-goals.
5. Left untouched: the bridge interface (a change found needed is a scope stop, design §4, :159).

## 3. Why — the value it delivers

The Phase D numbers today come from clean single-voice TTS only, and the phantom-command axis has no rate — the only negative audio is 3 s of digital silence (`03-critique-and-ranking.md` :244; design §0 Prerequisites). This feature shows where a change breaks as noise, echo, speed and level worsen, as curves per condition, and gives a first, stand-in false-fire rate (adr-0004, adr-0007), serving the maintainer's accuracy axis: "real out-of-grammar/noise rejection … robustness to the Quest mic, exertion, and game audio; fewer phantom or wrong commands" (`00-brief.md` :40-41). The thing protected when a tradeoff forces a cut: the clean reference still ties exactly to the pins, and conditioned numbers are never pooled into one pass rate (`R6-evaluation-methodology.md` :198, :336).

## 4. Observable behaviour

1. The maintainer runs fast mode naming a configuration (as `bench-fast-replay` takes it) and a seed; with no condition named the bench behaves exactly as `bench-fast-replay` does.
2. Naming the condition grid, the bench first runs the clean reference over all 52 cases and checks it against the pins; on any mismatch it names the differing cases, gives no numbers and exits non-zero.
3. It then replays each of the 16 non-pause cases and each negative-set clip under each of the grid's 12 conditions, generated on the fly from the seed; the 36 pause cases run in the clean reference only.
4. The report: a header naming the full config, the grid, the seed, the noise and room-response source, the mic filter, the level and the game-audio status; per case and condition its command class, WER and signed audio-time latency offsets, with no aggregate; per condition step the curves; the negative set's false fires per condition step, apart from the positive set. It is labelled TTS-only and provisional, and its augmented numbers rank conditions, not field rates.
5. A second run with the same seed gives an identical report; a different seed gives different conditioned audio.
6. Where the maintainer has placed title audio in the stated folder, the report adds the game-audio negatives and the game-audio condition; otherwise it says "game audio: not supplied".

## 5. Functional requirements

**Ruled on 2026-11-09** by the maintainer, in session, at SETTLE: S1 confirmed as G0 recorded it; R1, the condition grid is R6's — speed {0.9, 1.0, 1.1} × SNR {20, 15, 10, 5} dB, 12 conditions, every conditioned case also carrying synthetic room echo (RT60 drawn from the seed in 0.2–0.5 s), the mic-like filter and a level drawn from the seed in [0.04, 0.4], curves as metric against SNR, one per speed (`R6-evaluation-methodology.md` :188-196) (F1, F8); R2, the 16 non-pause cases and the negative set are conditioned, the 36 pause-lead and ladder cases run in the clean reference only (F5); R3, the design's open item (:208) — game audio is an optional input the maintainer supplies, Should (F11); R4, the rest as drafted; each took the recommended option.

| #  | Requirement | Story | Priority | Acceptance check | Source |
|----|-------------|-------|----------|------------------|--------|
| F1 | The seeded on-the-fly condition pipeline: per case, from the run's seed, a speed change, synthetic room echo, generated noise at the case's SNR step, mic-like filtering and a level in the Quest mic range, applied before replay in fast mode; only clean renders committed. | S1 | Must | Two runs with one seed give identical reports; a run with another seed gives different conditioned audio. | adr-0004; R1 |
| F2 | Each condition is what it names: mixed SNR within ±0.5 dB of the step over the speech region; duration scaled by the speed factor; the synthetic room response's RT60 within 0.2–0.5 s; peak within [0.04, 0.4]. | S1 | Must | Known-value toy checks for each of the four. | adr-0004; `R6-evaluation-methodology.md` :188-196 |
| F3 | Speech boundaries kept: each conditioned case's speech start and end are the clean truth scaled by the speed factor, within one 20 ms frame; echo, noise, filter and level do not move them. | S1 | Must | A known-value toy check at each speed; the truth rule passes on each conditioned clip's speed stage, the one stage that moves samples (ruled at PLAN 2026-11-09). | adr-0004; adr-0006 |
| F4 | The clean reference: every conditioned run first runs all 52 cases clean, and it must pass the pinned-transcript self-check, or no number is given, the differing cases are named and the run exits non-zero; with no condition named, the report is byte-identical to `bench-fast-replay`'s. | S1 | Must | A report from a run naming no condition is byte-identical (`cmp`) to the report `main` gives on the same inputs; with one pin altered in a scratch copy, a conditioned run refuses numbers and names the case. | adr-0003; design §3 |
| F5 | The conditioned set: the 16 non-pause cases (clean, slot-variant, homophone, filler, split, silence) and the negative set, each under each of the 12 conditions; the 36 pause cases in the clean reference only. | S1 | Must | The green conditioned run's report lists 12 conditions for each of those cases and none for the pause cases. | R2; design §2 item 3 |
| F6 | The stand-in negative set: generated noise-only clips at each condition, silence, and 16 non-command phrases — 8 out-of-set, 8 near-miss (sharing words with a command but not one) — as new clean renders in the baseline Piper voice, run clean and under each condition; the committed fixture WAVs and `expectations.json` unchanged. | S1 | Must | The set is present in the green run; `git diff main` is empty for the committed WAVs and `expectations.json`; the maintainer approves the phrase list (human-only). | adr-0007 |
| F7 | False fires on the negative set per condition step, as a count per negative case and per hour of negative audio, reported apart from the positive set's classes and never netted; labelled provisional and stand-in; zero labelled "set too easy", not "zero". | S1 | Must | The green run's report gives both rates per step with both labels; a toy run with zero false fires shows the "set too easy" label. | adr-0007; design §3 |
| F8 | Curves per condition: intent accuracy, semantic accuracy, phantom rate, WER, ECE, NCE and the false-fire rate per SNR step, one curve per speed, each point stating its n in phrases and cases; no total pooled across conditions. | S1 | Must | The green run's report gives each curve with its n per point and no pooled figure. | adr-0004; `R6-evaluation-methodology.md` :198, :336 |
| F9 | Latency under conditions: each case's signed audio-time offsets from end of speech (first partial, each final, the command event) per condition, with no aggregate. | S1 | Must | The green run's report gives each conditioned case's offsets and no aggregate. | adr-0001; `bench-fast-replay` F4 |
| F10 | Every report names its full config, adding the grid, the seed, the noise and room-response source (generated and synthetic, or MUSAN and SLR28), the mic filter, the level and the game-audio status; labelled TTS-only and provisional, ranking conditions, not field rates. | S1 | Must | The report header names every F10 field and carries the labels. | design §3; adr-0008; `R6-evaluation-methodology.md` :451 |
| F11 | Game audio, an optional input: where the maintainer places title audio in a stated folder, game-audio-only clips join the negative set and a game-audio condition mixes it under the conditioned cases at SIR {+20, +10, +5, 0} dB (provisional → the tuning pass); absent, the report says "game audio: not supplied". | S1 | Should | A run without the folder says "not supplied"; with any audio file placed there, the report gives the game-audio negatives' false fires and the SIR curve. | R3; design :208; `I1-recognition-core-redesign.md` :264 |
| F12 | MUSAN noise and SLR28 room responses used where the maintainer has placed them at a stated location; otherwise the generated and synthetic kinds, named as such. | S1 | Could | A run without them names the generated and synthetic kinds; with them present (a human download), the header names MUSAN and SLR28. | adr-0004 |
| F13 | Paced mode accepts one named condition. | S1 | Could | One paced run under one condition gives a report naming it. | adr-0003 |
| F14 | Untouched: Command 3, its pins, the committed fixture WAVs, `expectations.json` and the bridge interface; a bridge interface change found needed is a scope stop. | S1 | Must | Command 3 green; `git diff main` empty for the committed WAVs, `expectations.json` and the bridge's public interface. | design §4; adr-0008 |

## 6. Non-functional requirements

- **Performance:** the design rules no budget; none is set.
- **Robustness at the input boundary:** an unknown condition name is refused with the valid list; an add-on or game-audio path that exists but cannot be read, a clip without truth, or a metric undefined at a curve point (e.g. NCE where every word is correct) each give an explicit message naming it, never a silent or made-up number.
- **Degraded dependencies:** MUSAN, SLR28 or title audio absent is not an error — the run uses the generated kinds and the report says so; the clean reference failing its self-check stops the run with no numbers.

## 7. Non-goals / deferred

- The G0 Out list (§2).
- Significance tests, McNemar, percentiles and summaries across runs — `bench-ab-report`.
- Tuning the condition steps, the SIR steps or the phrase count — the tuning pass.
- Conditioning the 36 pause cases (R2).
- Being a verification gate: the bench never is one (adr-0008).

## 8. Dependencies & assumptions

- Depends on `bench-fast-replay` (merged as PR #198, `63434cb`); F13 on `bench-paced-mode` (merged as PR #199, `8de2f69`).
- The Piper venv and the baseline voice `en_US-lessac-medium` are in the user cache (`~/.cache/voxr-fixture-gen/`, read 2026-11-09), so the new renders need no download.
- Human-supplied, optional: the MUSAN and SLR28 download (adr-0004) and the title audio (R3).
- Assumed: the maintainer runs it on the Windows desktop.

## 9. Acceptance criteria (G2)

- [x] A green conditioned fast run over the grid, the clean reference's self-check passing. (phase 5 gate V5: two seed-7 grid runs exit 0, the self-check line twice, `condition: wrote 580 clips`; the review-fix CHECK C4)
- [x] A run naming no condition gives a report byte-identical to `main`'s on the same inputs. (phase 5 gate V3 `cmp=0`; the review-fix CHECK C3 `cmp=0` against the baseline report)
- [x] Mutation: one pin altered in a scratch copy makes a conditioned run refuse numbers and name the case. (phase 5 gate V7: exit 3, the case named, no `cond/`)
- [x] Two runs with one seed give identical reports; another seed gives different conditioned audio. (phase 5 gate V5 `cmp=0`; V6 `differ=564 of 564` at seed 8)
- [x] The F2 and F3 toy checks pass. (the review-fix CHECK C2: metrics-check 67/67, condition-check 38/38, with negatives 56/56)
- [x] The report gives the curves with n per point (F8), the negative set's false fires with both labels (F7), and every F10 header field. (phase 3 and phase 5 gates V8; the G2 packet showed the curves and negative set of the seed-7 grid report)
- [x] A run without title audio says "game audio: not supplied" (F11). (phase 4 and phase 5 gates: no default add-on root, game audio reported not supplied)
- [x] Command 3 is green; `git diff main` is empty for the committed WAVs, `expectations.json` and the bridge's public interface. (Phase 5 V15 after the BCN-1 merge: arm64 exit 0, harness 52/52, exit 0; phase 5 gate V12 both diffs empty; the G2 preflight at `fc0d42a`, item 2)
- [x] Human-only verification: the maintainer listens to one conditioned case per SNR step and approves the negative phrase list. (the maintainer at G2 on 2026-11-10, given `cease_fire__s1.0-snr20/15/10/5.wav` and the 16 phrases of `Tests~/Fixtures/audio/negatives/manifest.json`: "Accept: listened, approved")
- [x] The commands the project's `verification` binding names are green. (Commands 1-2 EditMode 239/239, PlayMode 792/792 at every phase gate and the review-fix CHECK C1; Command 4 not triggered; the G2 preflight at `fc0d42a`, item 2)

**Ruled at G2 on 2026-11-10** by the maintainer, in conversation (AskUserQuestion, "Accept: listened, approved"): accepted at head `fc0d42a`, with the third preflight in view — the first preflight's item 3 remediated by the as-built reconcile `8c6e086`, the second's items 3 and 4 by `fc0d42a` and the STATUS line. In the same ask the maintainer deferred to STATUS the three items the reconcile routed out (the resampling cache's memory, the D5 report lines not naming the condition, the RT60 stop text's en dash): "Defer all three (Recommended)". The fork-default takes since Phase 1 were listed at G2.

## 10. Open questions

1. Where the negative clips, their truth and their manifest live, apart from `expectations.json` — the architecture.
2. The level of a noise-only clip at each SNR step, having no speech to reference — the architecture.
3. The mic-like filter's shape — the architecture; its values → the tuning pass.
4. The stated folders for the title audio and for MUSAN and SLR28, and the formats they accept — the architecture.
5. Where the condition pipeline code lives (the rig sits under `Planning~/features/coverage-in-selection/ab-rig/`) — the architecture.
6. All step values are provisional → the tuning pass (design Status).
