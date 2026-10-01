# R6 — Evaluation methodology: how VoXR would prove an improvement is real

Research report, 2026-09-05. Scope: metrics, corpora, synthetic speech validity, noise/room simulation, statistical rigour on small sets, latency instrumentation, in-product telemetry. Every citation below was fetched; anything not fetched is tagged `UNVERIFIED`.

---

## Executive summary

- **VoXR currently has no instrument that can measure the thing it optimises.** The 699-row "A/B corpus" is text-only — it replays perturbed *transcripts* through the parser with no decoder in the loop (`Planning~/features/coverage-in-selection/ab-rig/stage.sh` stages nine parser `.cs` files and zero VOSK code). So every parser-scoring change measured to date has been measured against a decoder that was held constant *by construction*, not by experiment. Accuracy changes that route through the grammar or the acoustics are invisible to it.
- **The acoustic corpus is 16 fixtures from one Piper voice** (`en_US-lessac-medium`, `Tests~/Fixtures/generate.sh`). A 16-case pass/fail run has a 95% Wilson CI half-width of **±0.097 at 16/16** and **±0.163 at 14/16** — i.e. it cannot distinguish 87% from 96% accuracy. `Documentation~/editor-testing.md` already says the right thing ("detects changes in behaviour, not absolute recognition quality"); nothing enforces it.
- **Synthetic-clean does not predict human.** Timers-and-Such (Lugosch et al., NeurIPS D&B 2021) reports a direct SLU model at **81.6% ± 5.4%** trained on real speech vs **68.0% ± 5.5%** trained on 132 h of TTS, both scored on the same real test set — a 13.6-point gap from the synthetic/real distribution shift alone. Hilmes/Rossenbach/Schlüter (2024) measure the same shift as **+6.3 pp (GMM-HMM) to +18.7 pp (AED)** WER, and note the gap is *architecture-dependent* — so a TTS-measured delta does not even transfer between decoders.
- **The fix is not "abandon TTS" but CHiME-4's protocol**: report simulated and real numbers side by side, and rank only on real. CHiME-4's own instructions require four WERs (real/simu × dev/test) per system and state that "only the results of the best system on the real test will be taken into account in the final WER ranking."
- **WER is the wrong headline metric for VoXR.** ATCO2 (Zuluaga-Gomez et al., 2023), whose domain — terse radio-style tactical commands with callsigns — is the closest public analogue to VoXR's, reports **22.3–22.6% WER but NER F1 of 0.97 (callsign) / 0.82 (command) / 0.87 (value)**. The words that matter are recoverable far above the transcription floor. VoXR should score intents and slots, not words.
- **VoXR's dangerous failure — the phantom command — has an established metric family it is not using**: false-accepts-per-hour against a negative corpus, with false-reject-rate reported *at a fixed FA/h operating point* (Picovoice's public Apache-2.0 wake-word benchmark fixes it at 1 FA per 10 h). The leading-required-miss bar shipped in 2.0.0 on the strength of "zero observed legitimate recoveries in 73 utterances" — a claim with no negative-hours denominator at all.
- **Latency has an established decomposition VoXR does not instrument.** FastEmit (Yu et al., ICASSP 2021) defines *partial-recognition latency* as the gap between end-of-speech (by forced alignment) and last-token emission, and reports **90th-percentile latency cut from 210 ms to 30 ms**; Shangguan et al. (Interspeech 2021) show model size and FLOPS "are not always strongly correlated with observed UPL." VoXR's pipeline has at least six timestampable points and logs none of them.
- **Statistics: a 73-utterance paired A/B cannot show much, but it can show something.** Because significance in a paired design depends on *discordant* utterances only, **six utterances that all flip the same way is the smallest possible significant result** (2 × 0.5⁶ = 0.031). Five is not significant no matter how it looks. Detecting a 90%→95% improvement at 80% power needs ≈435 utterances per arm unpaired, or ≈290 paired at a 10% discordance rate.
- **Repeated tuning against a fixed 16-case corpus is a textbook adaptive-data-analysis failure** (Dwork et al., *Science* 2015). Every threshold sweep, alias, and scoring rule that was accepted because the fixture suite went green has spent some of that suite's validity. VoXR needs a sealed holdout it does not look at.
- **Licensing constrains the corpus more than availability does.** Google Speech Commands v2 (CC BY 4.0), MUSAN (CC BY 4.0), OpenSLR SLR28 RIRs (Apache-2.0) and Kokoro-82M (Apache-2.0) are all clean for a commercial package. Fluent Speech Commands (CC BY-NC-ND, "not authorized… for any commercial purpose, including… bench-marking"), SLURP audio (CC BY-NC), XTTS-v2 (CPML, non-commercial) and F5-TTS weights (CC BY-NC-4.0) are **not**.

---

## Findings table

| Name | Key citation | What it is | How VoXR would use it | Cost to adopt | Main caveat |
|---|---|---|---|---|---|
| SLU-F1 / SLURP | Bastianelli, Vanzo, Swietojanski, Rieser, EMNLP 2020 | 72k-utterance SLU corpus (18 scenarios, 46 actions, 55 entity types, 177 speakers, ~58 h, close + far mic) and a metric combining span-F1 with word/char distance on entity fillers | Adopt SLU-F1's *shape* as VoXR's slot metric: an alias-fuzzy slot match should score partial credit, not zero. Use SLURP itself only as an off-domain sanity corpus | Metric: 1–2 days. Corpus: 6 GB, plus grammar mapping (~1 wk) | Audio is CC BY-NC 4.0 — not usable to benchmark a commercial package without contacting the rights holder |
| Synthetic→real gap (Timers-and-Such) | Lugosch, Papreja, Ravanelli, Heba, Parcollet, NeurIPS D&B 2021 | Numbers corpus with parallel `train-synth` (192k utts, 16 TTS speakers, 132.2 h) and `train-real` (1,640 utts, 74 spk, 1.9 h); test-real is only 240 utts / 10 spk | The empirical warrant for "TTS-clean does not predict human": 81.6% (real) vs 68.0% (synth) on identical test-real | Free — a citation, not a build | Measures *training* transfer, not test-set validity; the direction of the bias for *testing* (TTS is easier) is inferred, not measured here |
| Purely-synthetic ASR gap | Hilmes, Rossenbach, Schlüter, arXiv:2407.17997, 2024 | WER of ASR trained only on TTS vs real, three architectures | Justifies never ranking two decoders/grammars on a TTS-only set: the gap is +6.3 pp (GMM-HMM) to +18.7 pp (AED) — architecture-dependent | Free | Again training-side; VOSK's nnet3 chain is not one of the three architectures tested |
| CHiME-4 dual reporting protocol | CHiME-4 challenge instructions, chimechallenge.org | Mandates reporting WER on real *and* simulated dev/test; ranks on real test only | Directly transplantable: VoXR reports every metric twice (TTS corpus, human corpus) and gates releases on the human number | ~0 — a reporting rule | Only works once a human corpus exists that is big enough to rank on |
| Blockwise bootstrap | Liu & Peng, Interspeech 2020 (arXiv:1912.09508) | Resamples non-overlapping *blocks* of utterances because same-speaker utterances are correlated; naive bootstrap underestimates variance | VoXR's field data is 10 sessions from **one** speaker — maximally dependent. Block = session, not utterance | ~100 lines of Python | With 10 blocks the CI is wide and the method is near its small-sample limit |
| MAPSSWE / SCTK | NIST SCTK (`sclite` v2.10, `sc_stats` v1.3), usnistgov/SCTK, NIST open licence | Matched-Pair Sentence-Segment Word Error test for paired ASR comparison, plus alignment/scoring | Use `sclite` for the transcript-level A/B (system A vs B on identical audio) and `sc_stats -t mapsswe` for the significance call | Half a day to wire in | Word-level; VoXR's decision is intent-level, so McNemar on intents is the primary test and MAPSSWE the secondary |
| FA/h × FRR operating point | Picovoice wake-word benchmark (public, Apache-2.0); Warden, arXiv:1804.03209, 2018 | Fix false alarms per hour (Picovoice: 1 per 10 h; Speech Commands streaming eval: 750 ms tolerance window), then compare miss rate | The missing metric for phantom commands: a negative corpus of game audio, breathing, chatter, out-of-set speech, scored in FA/h | 2–3 days to build the negative corpus + scorer | Requires *hours* of negative audio; VoXR has ~0. The 73-utterance field set contains no negative-hours denominator |
| PR latency / UPL decomposition | Yu et al., FastEmit, ICASSP 2021 (arXiv:2010.11148); Shangguan et al., Interspeech 2021, DOI 10.21437/Interspeech.2021-1887 | Latency measured end-of-speech (forced alignment) → last-token emission, reported at percentiles (FastEmit: p90 210 ms → 30 ms) | Timestamp six pipeline points; report p50/p90/p99 per stage. Shangguan: model size/FLOPS are not reliable proxies for user-perceived latency | 3–5 days (native + C# timestamps, plumbed to the session log) | Forced alignment needs a reference; for TTS fixtures the alignment is known, for field audio it is not |
| Entity-level scoring for radio commands | Zuluaga-Gomez et al., ATCO2, arXiv:2211.04054, 2023 | 4 h test set (3,521 sentences, 40,444 words) + 5,281 h pseudo-labelled; NER over `callsign`/`command`/`value` | The closest public domain analogue. Adopt its scoring stance: score entities, not words. 22.3% WER coexists with 0.97 callsign F1 | 1-hour subset is free; full set is licensed | CC BY-NC-ND 4.0. Accents/radio channel differ from a Quest mic |
| Augmentation: speed, RIR, noise | Ko et al., Interspeech 2015 (speed perturbation, +4.3% rel. avg); Ko et al., ICASSP 2017 + OpenSLR SLR28 (Apache-2.0); MUSAN, Snyder/Chen/Povey 2015 (CC BY 4.0); DNS Challenge (181 h, ~62k clips, 150 classes) | Turn one clean TTS render into many acoustic conditions | Convolve fixtures with simulated small-room RIRs, add MUSAN/DNS noise at controlled SNRs, ±10% speed perturbation → a 16-fixture corpus becomes thousands of conditioned trials | 1 wk of Python in `Tests~/Fixtures/` | Simulation ≠ reality (CHiME-4: reliability of simulated data "currently appears to be approximately true"). Augmented copies are not independent samples — the effective n is still 16 |
| Confidence calibration (NCE/ECE) | Wang, Soltau, El Shafey, Shafran, ASRU 2021 (arXiv:2110.15222) | Word-level confidence model; reports NCE 0.4, ECE 0.05 (ECE = 10 equal-width bins, mean \|confidence − accuracy\|) | Gives VoXR a way to *quantify* the documented "flat confidence" failure and to prove any replacement is better, before changing `minConfidence` | 1–2 days for the scorer; the corpus is the cost | Needs per-word ground truth. VOSK's `conf` in grammar mode may be too degenerate to bin usefully — measuring that is itself the finding |
| Reusable holdout / adaptive analysis | Dwork, Feldman, Hardt, Pitassi, Reingold, Roth, *Science* 349:636, 2015 | Repeatedly testing hypotheses against the same holdout invalidates it; a noise-added mechanism lets a holdout be reused a bounded number of times | Names the disease in VoXR's process: the 16 fixtures and the 699 rows are the *training* set for the scoring rules, not a test set. Requires a sealed corpus | Cheap to state, expensive to honour (a second corpus) | A sealed holdout only stays sealed if the human enforces it across sessions |

---

## Detailed findings

### 1. SLURP and the SLU-F1 metric — score entities with partial credit `VERIFIED`

Emanuele Bastianelli, Andrea Vanzo, Pawel Swietojanski, Verena Rieser. *SLURP: A Spoken Language Understanding Resource Package.* EMNLP 2020, pp. 7252–7262. https://aclanthology.org/2020.emnlp-main.588/ · https://arxiv.org/abs/2011.13205 · repo https://github.com/pswietojanski/slurp

**What it is.** ~72k audio recordings of single-turn interactions, ~58 h, 177 speakers (37.3% female, 32.2% male; 25.5% native, 44% non-native), 34,603 close-range and 37,674 far-range recordings, 18 scenarios, 46 actions, 55 entity types.

**Evidence.** SLU-F1 relaxes exact-span entity matching: entities match when *labels* are identical even if the *fillers* differ, and the distance between mismatched fillers (WER or normalised Levenshtein) increments FP and FN proportionally. Word-F1 captures ASR influence on NLU; Char-F1 measures NLU robustness to transcription noise; SLU-F1 combines them. Published baselines: Multi-ASR + HerMiT — 16.2% WER, 85.69% scenario accuracy, 81.42% action accuracy, 70.84% SLU-F1; Google-ASR + HerMiT — 81.68% / 76.58% / 66.00%.

**VoXR mapping.** Two uses, one good and one bad. Good: adopt SLU-F1's *stance* for VoXR's slot metric. Today `VoxrBatchTestRunner.CheckSlots` is exact string equality — a `{target}` extracted as "hotel one" vs expected "hotel 1" is a total failure, indistinguishable from extracting nothing. That flattens the very gradient a scoring change moves along. A char-distance-weighted slot F1 makes near-misses visible. Bad: SLURP audio is CC BY-NC 4.0 (per the repo), so it cannot be used to benchmark a shipped commercial package without a separate grant from the rights holder. Annotations are CC BY 4.0.

**Licence.** Annotations CC BY 4.0; audio CC BY-NC 4.0 (repo states less restrictive terms may be negotiated).

---

### 2. Timers-and-Such — the hardest number on TTS-vs-real validity `VERIFIED`

Loren Lugosch, Piyush Papreja, Mirco Ravanelli, Abdelwahab Heba, Titouan Parcollet. *Timers and Such: A Practical Benchmark for Spoken Language Understanding with Numbers.* NeurIPS 2021 Datasets & Benchmarks. https://arxiv.org/abs/2104.01604

**Evidence (Table 1 and Table 5, via ar5iv).**

| Split | Utterances | Speakers | Hours |
|---|---|---|---|
| train-real | 1,640 | 74 | 1.9 |
| dev-real | 271 | 11 | 0.3 |
| test-real | 240 | 10 | 0.3 |
| all-real | 2,151 | 95 | 2.5 |
| train-synth | 192,000 | 16 (synthetic) | 132.2 |
| dev-synth | 24,000 | 2 | 15.8 |
| test-synth | 36,000 | 3 | 23.5 |

Synthetic speakers were generated with VoiceLoop trained on VCTK. Accuracy on **test-real**: direct model 81.6% ± 5.4% (train-real) vs 68.0% ± 5.5% (train-synth); multistage (TAS LM) 64.0% ± 3.3% (train-real) vs 72.2% ± 1.4% (train-synth).

**VoXR mapping.** Three things. (a) 132 h of synthetic speech from 16 voices did not substitute for 1.9 h of real speech from 74 — 80× the data, 13.6 points worse. VoXR's corpus is 16 utterances from **one** voice. (b) The *direction reverses* between the direct and multistage models: synthetic-vs-real ranking is not architecture-invariant, so a delta measured on TTS may not survive the move to human audio at all. (c) The paper's own design is the template: ship parallel real and synthetic splits, and report both. `Tests~/Fixtures/audio/human/` exists and contains only a `.gitkeep` — the slot is already carved out and empty.

**Caveat.** These are *training*-side numbers. The evidence that TTS *test* sets are optimistically biased is indirect here; §4 supplies the test-side analogue.

---

### 3. Purely synthetic training data across architectures `VERIFIED`

Benedikt Hilmes, Nick Rossenbach, Ralf Schlüter. *On the Effect of Purely Synthetic Training Data for Different Automatic Speech Recognition Architectures.* arXiv:2407.17997, 2024. https://arxiv.org/html/2407.17997v1

**Evidence.** LibriSpeech dev-other WER, real vs purely synthetic training: GMM-HMM 25.9% → 32.2% (+6.3 pp); Hybrid 15.0% → 26.2% (+11.2 pp); AED 18.9% → 37.6% (+18.7 pp). The authors attribute part of the gap to TTS overfitting on speaker embeddings and to TTS modelling errors on unseen text.

**VoXR mapping.** VOSK's small model is a **Kaldi nnet3 chain hybrid** — architecturally closest to the "Hybrid" row, the middle of the three. More usefully: the fact that the gap ranges over 12 points *across architectures* means a synthetic-only measurement cannot be transferred between decoders. If VoXR ever evaluates a candidate replacement decoder (a different VOSK model, a Whisper-tiny variant, a CTC keyword model), doing so on the TTS corpus would compare architectures on an axis that is itself architecture-sensitive. This is the strongest argument in the report against the current rig being used for a decoder decision.

---

### 4. CHiME-4's dual real/simulated reporting protocol `VERIFIED`

CHiME-4 challenge instructions. https://www.chimechallenge.org/challenges/chime4/instructions (accompanying CHiME-3 dataset paper: Barker et al., ASRU 2015 — paper text `UNVERIFIED`, PDF would not decode)

**Evidence.** Every system must report four WERs — real dev, simulated dev, real test, simulated test — and the best system must report 16 (four environments × dev/test × real/simu). The instructions state that whether "simulated data are a reliable way of predicting ASR performance on real data… currently appears to be approximately true," and explicitly encourage improving simulation to make it more reliable. Ranking rule: "Only the results of the best system on the real test will be taken into account in the final WER ranking of all systems."

**VoXR mapping.** This is the single cheapest change in this report and the one that fixes the most. VoXR's release process should require, for any accuracy claim: (TTS corpus number, human corpus number) as a pair, with the ruling made on the human number. It costs a table row and a policy sentence. It converts the TTS corpus from a *substitute* for evidence into a *regression tripwire* — which is what `Documentation~/editor-testing.md` already claims it is, without any mechanism enforcing that claim. Note the honesty of CHiME-4's own hedge: even with real recorded noise and a real 6-channel array, simulation is only "approximately" predictive.

---

### 5. Blockwise bootstrap for dependent utterances `VERIFIED`

Zhe Liu, Fuchun Peng. *Statistical Testing on ASR Performance via Blockwise Bootstrap.* Interspeech 2020 (arXiv:1912.09508, Dec 2019 / rev. May 2020). https://arxiv.org/abs/1912.09508 · https://www.isca-archive.org/interspeech_2020/liu20c_interspeech.html

Companion: Liu et al., *Modeling Dependent Structure for Utterances in ASR Evaluation*, Interspeech 2023, arXiv:2209.05281 (graphical-lasso inference of the blocks) — `UNVERIFIED` (abstract seen in search results only).

**Evidence.** The naive bootstrap assumes independent utterances and therefore *underestimates* variance when utterances are correlated — e.g. utterances from the same speaker. The blockwise method partitions evaluation utterances into non-overlapping blocks and resamples blocks, yielding a consistent variance estimator for the absolute WER difference between two systems.

Foundational reference: Bisani & Ney, *Bootstrap estimates for confidence intervals in ASR performance evaluation*, ICASSP 2004 — `UNVERIFIED` (PDF would not decode; existence and topic confirmed via IEEE Xplore and RWTH listings).

**VoXR mapping.** VoXR's field data is the pathological case the paper is about: 73 utterances, **10 sessions, one speaker**. Utterance-level bootstrap on that set would produce a CI perhaps 2–3× too narrow. The correct resampling unit is the **session** (10 blocks), and the honest conclusion is that a 10-block bootstrap has very low resolution. That is a finding, not a failure: it tells the maintainer that no amount of clever statistics rescues 73 single-speaker utterances, and the only real fix is more speakers.

---

### 6. NIST SCTK: `sclite` + `sc_stats` MAPSSWE `VERIFIED`

NIST Speech Recognition Scoring Toolkit. https://github.com/usnistgov/SCTK · docs https://github.com/usnistgov/SCTK/blob/master/doc/sctk.htm

**Evidence.** SCTK ships `sclite` v2.10 (DP alignment + scoring reports), `sc_stats` v1.3 (paired significance comparison between systems run on identical test data), `rover` v0.1, `asclite` v1.11. `sc_stats -t mapsswe` runs the Matched-Pair Sentence-Segment (Word Error) test. Licence: NIST open licence (LICENSE.md).

**VoXR mapping.** Two concrete uses. (a) Where VoXR compares two *decoder/grammar* configurations on identical audio, `sclite` gives the alignment and `sc_stats -t mapsswe` gives the significance call, without VoXR writing statistics code. (b) `sclite`'s per-word alignment output is the natural source for **confusion-pair mining** — the substitution table (`switch→two`, `all→fall`, `cease→safe`) is exactly the artefact `KNOWN_LIMITATIONS.md` currently documents by hand from field logs. A nightly `sclite` run over the corpus would produce that table automatically and make it diffable across releases.

**Caveat.** MAPSSWE is word-level. VoXR's user-visible unit is the intent, so the primary test should be McNemar on per-utterance intent correctness, with MAPSSWE as a secondary transcript-level check.

---

### 7. False-accepts-per-hour: the metric the phantom-command bar was never measured against `VERIFIED (methodology)`

Picovoice wake-word benchmark. https://picovoice.ai/docs/benchmark/wake-word/ — methodology: DEMAND noise (18 environments) mixed at 10 dB SNR, background speech from LibriSpeech `test-clean`; engines compared **at a fixed false-alarm rate of 1 per 10 hours**; data and code public on GitHub under Apache-2.0.

Pete Warden. *Speech Commands: A Dataset for Limited-Vocabulary Speech Recognition.* arXiv:1804.03209, 2018. https://arxiv.org/abs/1804.03209 — v2: 105,829 utterances, 35 words, 2,618 speakers, 16 kHz, 1 s clips (v1: 64,727 / 1,881). Recommended protocol: 10 target words + an **"unknown word"** class + a **"silence/background noise"** class; hash-based train/val/test split stable across versions. Baselines: 88.2% top-one (v2 model on v2 test). Streaming evaluation uses a **750 ms tolerance window** and reports matched / correct / wrong / false-positive separately. Licence: CC BY 4.0 (per TFDS catalog: 85,511 train / 10,102 val / 4,890 test, 12 classes).

Related and `UNVERIFIED`: *A Real-World Dataset for Benchmarking False Alarm Rate in Keyword Spotting*, IOS Press FAIA 2023, doi:10.3233/FAIA230695 — reports that two KWS models with SOTA Speech Commands accuracy suffer high FAR in noisy real environments (abstract seen via search; full text not fetched).

**VoXR mapping.** This is the largest methodological gap in VoXR. `KNOWN_LIMITATIONS.md` documents that in grammar mode coughs, hums and breathing decode as short in-grammar words — a false-accept mechanism with no rate attached to it. The 2.0.0 leading-required-miss bar was justified by "zero legitimate leading-miss recoveries in 73 field utterances," which is a *false-reject* argument evaluated on a positive-only corpus; the bar's actual purpose is reducing false accepts, and no false-accept rate was ever measured because there are no negative hours to measure it against.

The build: assemble **≥5 hours of negative audio** — game music/SFX bed from the maintainer's own title, breathing/exertion, out-of-set conversational speech (LibriSpeech `test-clean`, CC BY 4.0), room tone, and the Speech Commands "unknown"/"silence" classes — replay it through the full pipeline with a live command set, and count every fired command. Report **FA/h**, and report false-reject rate on the positive corpus **at a stated FA/h**, exactly as Picovoice does. Speech Commands' 12-class protocol (target + unknown + silence) is the template for how a fixed-vocabulary system is supposed to be scored: the "unknown" and "silence" classes are first-class, not an afterthought.

---

### 8. Latency: partial-recognition latency and user-perceived latency `VERIFIED`

Jiahui Yu et al. *FastEmit: Low-latency Streaming ASR with Sequence-level Emission Regularization.* ICASSP 2021 (arXiv:2010.11148). https://arxiv.org/abs/2010.11148

Yuan Shangguan, Rohit Prabhavalkar, Hang Su, Jay Mahadeokar, Yangyang Shi, Jiatong Zhou, Chunyang Wu, Duc Le, Ozlem Kalinli, Christian Fuegen, Michael L. Seltzer. *Dissecting User-Perceived Latency of On-Device E2E Speech Recognition.* Interspeech 2021, pp. 4553–4557, DOI 10.21437/Interspeech.2021-1887. https://www.isca-archive.org/interspeech_2021/shangguan21_interspeech.html

**Evidence.** FastEmit defines **partial-recognition (PR) latency** as the timestamp difference between the emission of the last token in the finalised result and the end of speech, where end of speech is estimated by **forced alignment** — chosen deliberately because, unlike wall-clock latency, it "is inherent to streaming ASR models" and does not depend on hardware or system optimisation. Reported: 90th-percentile latency reduced from 210 ms to 30 ms on LibriSpeech; 150–300 ms latency reductions on the production task. Shangguan et al. decompose user-perceived latency into token-emission latency and endpointing behaviour, and find that conventional proxies — model size, FLOPS — "are not always strongly correlated with observed UPL."

**VoXR mapping.** VoXR's §2 pipeline has six timestampable boundaries and instruments none of them:

| # | Point | Where |
|---|---|---|
| T0 | Audio frame captured | `audio_capture_audiorecord.cpp`, ring-buffer write |
| T1 | Frame handed to decoder | `vosk_bridge.cpp`, before `vosk_recognizer_accept_waveform_s` |
| T2 | VOSK final JSON queued | `vosk_bridge.cpp`, result queue push |
| T3 | Result polled on main thread | `VoxrSpeechRecogniser` poll |
| T4 | Utterance buffer flushes | `UtteranceBuffer.cs` (window close **or** eager flush) |
| T5 | Command event dispatched | `VoxrCommandRecogniser`, `OnCommandRecognised` |

Two derived metrics matter and they are different: **decision latency** = T5 − (end of speech), which is what the player feels; and **buffer cost** = T4 − T2, which is the price the `bufferWindow` (0.5 s default, up to ~2 s in the field) charges for reassembling VOSK's split finals. Today the eager-flush feature's benefit is *asserted*, not measured. Following FastEmit, end-of-speech for the TTS fixture corpus is known exactly — `generate.py` pads a fixed `TAIL_SILENCE_S = 1.0` after synthesis, so the true end of speech is `len(file) − 1.0 s` by construction, with no forced aligner needed. That makes an offline latency A/B available essentially for free on the existing corpus. Report p50/p90/p99 per stage, never means: Shangguan's point is that aggregate proxies mislead.

---

### 9. ATCO2 — the closest public domain, and its entity-first scoring `VERIFIED`

Juan Zuluaga-Gomez, Karel Veselý et al. *ATCO2 corpus: A Large-Scale Dataset for Research on Automatic Speech Recognition and Natural Language Understanding of Air Traffic Control Communications.* arXiv:2211.04054 (v2, 2023). https://arxiv.org/html/2211.04054 · repo https://github.com/idiap/atco2-corpus

Companion: *Lessons Learned in ATCO2: 5000 hours…*, arXiv:2305.01155 — `UNVERIFIED`.

**Evidence.** ATCO2-test-set: 4 h, 3,521 sentences, 40,444 words, seven airports, manual transcripts, with gold NER annotation over three entity classes — **callsign, command, value**. ATCO2-PL-set: ~5,281 h pseudo-labelled. Reported: ASR WER 22.3–22.6% on ATCO2-test-set (9.0–10.6% on MALORCA Vienna); NER F1 0.97 callsign / 0.82 command / 0.87 value; speaker-role detection F1 0.84–0.86; and a 53.7% absolute (60.4% relative) improvement in callsign recognition from combining ASR with NLP-side contextual biasing. Licence: CC BY-NC-ND 4.0; a free 1-hour subset exists.

**VoXR mapping.** The domain match is unusually close: terse imperative commands, alphanumeric identifiers ("hotel one" / "alpha three" ↔ callsigns), a value slot, a closed command vocabulary, and a noisy channel. Two transferable lessons. (a) **The headline metric is entity F1, not WER.** A 22% WER system is production-useful because the entities survive. VoXR reporting "transcription accuracy" would understate the system badly and would misdirect optimisation onto function words the parser already tolerates. (b) **Contextual biasing on the identifier set was worth 53.7 points absolute** on the identifier — the single largest reported gain in this report. VoXR's grammar already does something analogous (phrase-chunked entries), and this is the evidence that pushing further on identifier-side biasing has more headroom than parser scoring does.

**Caveat.** Licence forbids commercial use, and the acoustic channel (VHF radio, international accents) is not a Quest mic. Use it to calibrate *methodology*, not as a VoXR benchmark.

---

### 10. Augmentation and simulation: turning 16 renders into a real corpus `VERIFIED`

Tom Ko, Vijayaditya Peddinti, Daniel Povey, Sanjeev Khudanpur. *Audio Augmentation for Speech Recognition.* Interspeech 2015, pp. 3586–3589. https://www.isca-archive.org/interspeech_2015/ko15_interspeech.html — speed perturbation at factors 0.9 / 1.0 / 1.1; **average relative WER improvement of 4.3% across 4 tasks** (100–960 h training sets).

Tom Ko, Vijayaditya Peddinti, Daniel Povey, Michael L. Seltzer, Sanjeev Khudanpur. *A Study on Data Augmentation of Reverberant Speech for Robust Speech Recognition.* ICASSP 2017, pp. 5220–5224, doi:10.1109/ICASSP.2017.7953152. Released dataset: **OpenSLR SLR28** — simulated + real RIRs, isotropic and point-source noises, 16 kHz/16-bit, **Apache-2.0**; real RIRs from RWCP, REVERB-2014 and AIR; point-source noises extracted from MUSAN. https://www.openslr.org/28/ (paper text `UNVERIFIED` — Semantic Scholar page returned empty).

David Snyder, Guoguo Chen, Daniel Povey. *MUSAN: A Music, Speech, and Noise Corpus.* arXiv:1510.08484, 2015. https://arxiv.org/abs/1510.08484v1 · https://www.openslr.org/17/ — music (several genres), speech (twelve languages), technical and non-technical noises; **CC BY 4.0**.

Microsoft DNS Challenge noise set: **181 hours, ~62,000 clips, ~150 noise classes** (AudioSet-derived plus Freesound and DEMAND). https://github.com/microsoft/DNS-Challenge — `VERIFIED` via ICASSP-2022 challenge paper summaries; per-clip licences vary (`UNVERIFIED` at the individual-clip level).

**VoXR mapping.** The augmentation pipeline slots directly into `Tests~/Fixtures/generate.py` after the `synth()` step and before `scale_to_peak()`:

```
piper/kokoro render → speed perturb {0.9, 1.0, 1.1} → convolve small-room RIR (SLR28 simulated subset,
  room dims ≲ 5 m, RT60 0.2–0.5 s) → mix MUSAN/DNS noise at SNR ∈ {20, 15, 10, 5} dB
  → peak-scale into the measured Quest range [0.04, 0.4] → 48 kHz int16
```

Every stage is already licence-clean for a commercial package (Apache-2.0 / CC BY 4.0). One clean render becomes 3 × 1 × 4 = 12 conditioned trials, and the existing 16-fixture corpus becomes 192 acoustic trials — enough to see an SNR *curve* rather than a single pass/fail bit.

**The caveat that must not be lost.** Twelve augmentations of one utterance are not twelve independent samples. The effective sample size for a *phrase-level* claim is still 16. Augmentation buys **condition coverage** (does the AGC hold at 5 dB SNR?), not **statistical power** (is this scoring change better?). Conflating the two is the most likely way this harness would be misused.

---

### 11. Confidence calibration: NCE and ECE `VERIFIED`

Mingqiu Wang, Hagen Soltau, Laurent El Shafey, Izhak Shafran. *Word-level confidence estimation for RNN transducers.* ASRU 2021 (arXiv:2110.15222). https://arxiv.org/abs/2110.15222

**Evidence.** A lightweight neural confidence model over RNN-T output, using word timing to reduce cost and a sub-word→word mapping to handle tokenisation ambiguity and deletions. Reported: **NCE 0.4, ECE 0.05**, plus AUC over negative-predictive-value and true-negative-rate. ECE is computed over M = 10 equal-width confidence bins as the mean absolute gap between average confidence and empirical accuracy per bin; NCE measures information gain over a non-informative baseline.

**VoXR mapping.** `KNOWN_LIMITATIONS.md` records that VOSK's per-word `conf` is effectively flat — "two" scores ~0.50 regardless of clarity — so `minConfidence` (0.4) cannot be raised without rejecting number sequences. That is a **calibration** claim stated qualitatively. Computing ECE and NCE over the existing session logs (`words[].confidence` against per-word correctness from the expected transcript) would turn it into a number, and — critically — would give any proposed replacement (lattice posteriors, N-best agreement, a small confidence head) a scoreboard to beat. This is cheap: the session log already records `words[].confidence` with start/end times, and the fixture manifest already records `expectedTranscript`. The alignment is the only new code.

**Risk.** If VOSK's grammar-mode confidence turns out to have near-zero NCE (no information at all), that is itself a publishable-quality finding for the project — it would mean `minConfidence` is a dead knob and every historical tuning decision that leaned on it was noise.

---

### 12. Adaptive data analysis: why the 16-fixture corpus is no longer a test set `VERIFIED`

Cynthia Dwork, Vitaly Feldman, Moritz Hardt, Toniann Pitassi, Omer Reingold, Aaron Roth. *The reusable holdout: Preserving validity in adaptive data analysis.* *Science* 349(6248):636–638, 2015. https://www.science.org/doi/10.1126/science.aaa9375 · https://www.cis.upenn.edu/~aaroth/reusable.html

**Evidence.** Standard statistical guarantees assume the analysis procedure is fixed before the data is seen. Real analysis is adaptive — each new hypothesis is chosen after looking at previous results on the same data — and this invalidates the guarantees, producing spurious findings. The paper's mechanism ("Thresholdout") answers holdout queries through a differentially-private noise layer, permitting an exponentially larger number of adaptive queries before the holdout is exhausted.

**VoXR mapping.** This is the diagnosis for a pattern visible throughout `Planning~/`: the scoring model, coverage weight, admission rule, sibling-tie rule and leading-required-miss bar were each proposed, then accepted or rejected on the basis of the **same 16 fixtures / 699 derived rows / 73 field utterances**. Under Dwork et al., those corpora are now part of the *training* procedure for the parser's rules. Their remaining power to certify a *new* rule is materially less than a naive reading of "all 16 pass" suggests, and there is no accounting of how much has been spent.

Two practical remedies, in order of cost:
1. **Seal a holdout.** Generate the new corpus (§ Proposed harness) in two halves at the same time, from the same script; commit only one; keep the other unlooked-at, opened once per release. Record how many times it has been opened.
2. **Freeze the tuning corpus explicitly.** Rename the current 16 fixtures and 699 rows to what they are — a *development* set — in the docs and in the runner's own vocabulary, so a green run stops reading as acceptance evidence.

Full Thresholdout is overkill here; the discipline is what matters, and the discipline is free.

---

### 13. WER is not the target: transcription noise and downstream understanding `VERIFIED`

Ori Shapira, Shlomo E. Chazan, Amir DN Cohen. *Measuring the Effect of Transcription Noise on Downstream Language Understanding Tasks.* arXiv:2502.13645, 2025. https://arxiv.org/html/2502.13645

Piotr Szymański, Piotr Żelasko, Mikolaj Morzy et al. *WER we are and WER we think we are.* Findings of EMNLP 2020. https://aclanthology.org/2020.findings-emnlp.295/

**Evidence.** Shapira et al. build ENDow, a configurable noisy-transcript framework over summarisation, QA and dialog-act classification, and conclude that WER "does not capture discrepancies in types of noise, and cannot forecast results on downstream tasks." Models tolerate ~0.2 WER before significant degradation (noise-tolerance points 0.07–0.3 for summarisation); **named entities are the most impactful error class**, while verb errors are nearly harmless (effectiveness 0.073–0.229). Notably, for *utterance-level* dialog-act classification the ordering inverts — non-content words mattered more than named entities. Szymański et al. show commercial ASR WERs on real spontaneous conversation are far above published benchmark figures, and give guidelines for constructing realistic test sets.

**VoXR mapping.** Direct confirmation of a design instinct VoXR already has and should now formalise: the parser's per-element miss costs, coverage charge and admission rule are all implicit statements that *not all words are equal*. Shapira et al. supply the general evidence, and — importantly — the inversion at utterance level matches VoXR's own field finding that dropped short function words ("a", "to", "on") are its most damaging error class, not entity errors. So VoXR sits on the *dialog-act* side of that split, and should weight its metrics accordingly: the primary metric is intent correctness, and the secondary is slot F1, with word accuracy reported only as a diagnostic.

---

## Audit of VoXR's current evaluation

### Text injection (`InjectText`, `VoxrBatchTestRunner`)

**Strengths.** Fast, deterministic, hardware-free, main-thread-safe, fires the same events as real recognition, exercises the real parser instance (`VoxrBatchTestRunner` constructs a real `VoxrCommandParser`), applies the same threshold and completeness gates the recogniser applies (`PassesThresholds`, the `required slot unfilled` check), and now passes `minScore` into the parser so the construction-time sibling scan predicts the harness's own gate (#140). CSV/JSON import-export make suites diffable and version-controllable. This is a genuinely good regression instrument for parser logic.

**Gaps.**
- **It cannot see the decoder.** Everything upstream of the token string — substitution, drop, `[unk]`, endpointing, confidence — is out of scope. Since the brief's §3 lists *seven* failure modes and six of them are decoder-side, text injection covers roughly one seventh of the observed problem space.
- **The `[unk]` overclaim** is documented (`editor-testing.md`: "the batch runner receives real text rather than the decoder's `[unk]` for out-of-grammar words, so trailing filler is charged here where the grammar-constrained decoder would have made it free"). This means coverage-related results measured on the batch runner are *systematically different* from runtime, in a known direction, and no correction is applied.
- **Slot comparison is exact string equality**, so near-miss slots score identically to missing slots (see §1: SLU-F1 exists precisely to fix this).
- **`wordConfidence` is a single uniform value per case.** Real per-word confidence varies; a uniform simulated value cannot exercise the minimum-over-span rule in any realistic way.

### The TTS fixture corpus (`Tests~/Fixtures`)

**Strengths.** Genuinely reproducible (`generate.sh` bootstraps a pinned venv: `piper-tts==1.6.0`, `soxr==1.1.0`, numpy; downloads the voice; regenerates from `manifest.json`). Amplitude is peak-scaled into the *measured* Quest 3 mic range `[0.04, 0.4]` with a hard range check — an unusually careful touch. Replays through the identical downsampler → AGC → VOSK path as live audio. Fixed lead/tail silence (0.3 s / 1.0 s) and a 0.7 s inter-segment gap make the split-utterance case reproducible. Fixtures carry `expectedTranscript` *and* `expectedIntent` *and* `expectedSlots`, so both decoder and parser can be scored. Categories (`clean`, `homophone`) already anticipate error-class analysis. The docs are honest about its limits.

**Gaps, in descending severity.**
1. **One voice.** `en_US-lessac-medium`, a single US female-read voice. No sex, age, accent, rate, or pitch variation. Any speaker-dependent regression is invisible by construction — and VoXR's field data is also one speaker, so *the entire evidence base is two speakers, one of them synthetic*.
2. **Sixteen cases.** 95% Wilson CI at 16/16 is [0.806, 1.000]; at 14/16, [0.640, 0.965]. The corpus cannot resolve any accuracy difference smaller than about 15 points.
3. **No acoustic variation at all.** No noise, no reverberation, no speed/pitch perturbation, no Lombard-style exertion. The only varied parameter is peak amplitude, and only for one fixture (`cease_fire.wav` at 0.05). Meanwhile the target device is a headset worn while a game plays audio and the player moves.
4. **No negative corpus.** Not one fixture is "audio that must produce nothing." The documented breath/cough/hum false-trigger mode has zero test coverage, and the `silenceSeconds` mechanism exists in `generate.py` but produces *digital* silence — which is not what a microphone produces.
5. **`audio/human/` is empty** (`.gitkeep` only). The slot for real speech was carved out on 2026-07-30 and never filled.
6. **TTS-clean bias is unquantified.** Per §2 and §3 the bias is real and large; VoXR has never measured its own version of it, because it has no paired human recording of the same 16 phrases.

### The 699-utterance A/B rig

**Strengths.** Methodologically the most sophisticated instrument in the project. `stage.sh` pins the exact revision under test (`STAGED_FROM` records the short SHA plus a `+dirty` marker plus the grep'd `RequiredLiteralMissPenalty`), so a report can never be attributed to the wrong build. `perturb.py`'s probe-word selection is adversarial by design — it deliberately includes both words that can begin a pattern and words that cannot, "chosen to span the predicate rather than to flatter it." `ablate.py`'s docstring explicitly names its own 4A pass as "vacuous as evidence ABOUT the change." `issue124/README.md` records a measurement *caveat* (the refuse-to-FIRE variant's numbers must be re-measured for refuse-to-COMPETE) and then discharges it. This is better epistemic hygiene than most research code.

**Gaps.**
1. **No decoder.** `stage.sh` stages exactly nine files, all `Runtime/Commands/*.cs` plus `VoxrResult.cs`; `grep -rn 'vosk'` over both rig directories returns nothing. The "699-utterance corpus" is a text corpus. Every number it has produced is a statement about the parser conditioned on a transcript distribution — not about the system.
2. **The corpus is derived, not sampled.** All 699 rows descend from the same **16** committed transcripts via four mechanical families (`delete`, `append`, `prepend`, `concat`). Effective independent sample size is far below 699; the 95% CI computed as if n = 699 (±0.019) is a fiction. `perturb.py` is transparent about *why* the families exist, but nothing downstream discounts the n.
3. **Perturbations are not the error distribution.** Uniform single-word deletion is not how VOSK drops words (it drops *short function words* preferentially, and — per the brief's field finding — leaves no timing hole). Appending an in-grammar probe word is not how a stranded tail arises acoustically. The corpus models a *plausible* error process, not the *measured* one. The measured one is available: the 73 field utterances contain real substitutions and drops, and they are not used to parameterise the perturber.
4. **No significance testing anywhere.** Reports are of the form "48 of 699 rows change: concat 29, delete 9, prepend 5, append 4, baseline 1." A count of changed rows is not a test, and because the rows are dependent, no off-the-shelf test applies to them as written.
5. **The rig lives in `Planning~/`, which is gitignored and local-only.** Two consequences: it is invisible to `grep` in this repo (a documented trap in project memory — a rename sweep misses the rig's C# and breaks it), and it is not part of CI, so it runs only when a human remembers.

### The session debug log

**Strengths.** Best-in-class for its purpose. Versioned schema (`schemaVersion: 2`, additive changes), self-describing (`readme` field, package and Unity versions, session timestamps, `entryCount`), automatic and headless, ten-session retention, correctly suppressed under batch mode and Test Runner runs so real playtests are not evicted. Per-utterance it records the transcript, per-word confidences with start/end times, active sets, and per-round diagnostics with score vs `minScore`, aggregate confidence vs `minConfidence`, slots as half-open token ranges, reject reason, and the `tiedRival` / `tiedRivalIsSibling` pair. `scoring.md`'s "Reading a session log" maps every field to the rule that produced it and tabulates the six synthetic-attempt paths. This is a well-designed telemetry schema that happens to be Editor-only.

**Gaps.**
1. **Losing candidates are never recorded.** Only the round winner is logged. So the log can answer "why did this fire?" but not "what nearly fired?" — and the near-miss distribution is exactly what a confusion-pair miner and a threshold sweep both need.
2. **Barred rounds leave no trace**, and `rejectReason: no match` conflates "nothing matched" with "everything matched and was barred." The docs flag this explicitly, which is honest, but it means the log cannot measure the bar's own cost — the very quantity the 2.0.0 decision turned on.
3. **No timestamps that support latency analysis.** Entries carry `timestamp` and `frame` for *when the utterance was matched*, but nothing about when audio arrived, when VOSK emitted, or when the buffer flushed. None of the six T0–T5 points in §8 is recorded.
4. **Editor-only.** It is compiled out of player builds, so the Quest — the actual target — produces no log at all. All 73 field utterances therefore came from Editor sessions or manual transcription, not from the device under test. (The brief says the field logs are from Quest; if so they came via a different route, and that route is not the shipped mechanism.)
5. **No aggregation layer.** The log is per-session JSON with a 10-session cap. There is no tool that unions sessions, computes rates, or diffs a release against a baseline. `scoring.md` suggests scripts or "LLM tooling" could do this — i.e. it is currently done by hand.
6. **`rejectReason` numbers are culture-formatted** (decimal separator may be `,`), a real hazard for any parser built on it. Documented, but it is a schema bug: machine-readable fields should not be locale-formatted.

### The 73-utterance field set

**Strengths.** It is the only real evidence in the project, and it earned its keep — the phantom-command finding that produced the 2.0.0 bar came from it, and per project memory the field logs *killed* two proposed discriminators (acoustic gap, `[unk]`) that looked good on paper. Real speech from the actual device, the actual game, and the actual grammar is worth more per utterance than anything synthetic.

**Gaps.**
1. **One speaker.** No generalisation to any other voice is licensed by it. Combined with the single-voice TTS corpus, VoXR has never observed a second human speaker.
2. **73 utterances / 361 words.** A single-proportion 95% Wilson CI at 66/73 (90.4%) is [0.815, 0.953] — half-width ±0.069. It cannot certify anything finer than a ~7-point change, and that is the *optimistic* independent-sample calculation; per §5 the correct unit is the **session**, of which there are 10.
3. **Positive-only.** 73 utterances is not a duration, and the corpus carries no negative hours, so no false-accept rate can be computed from it — see §7.
4. **No audio retained** (as far as the repo shows). Without the waveforms the set cannot be replayed against a new decoder, a new grammar, or a new DSP chain; it is frozen at the transcript level, which is precisely the level the A/B rig is already stuck at.
5. **It has been analysed repeatedly** across issues #124, #126, #128 and the 2.0.0 release. Under §12 it is now a development set.

**The compound problem.** Text injection cannot see the decoder; the A/B rig cannot see the decoder; the field set has no audio, so it cannot be replayed through a decoder; and the only corpus that *does* exercise the decoder has 16 items from one synthetic voice. **There is no path in the current toolkit by which a decoder-side, grammar-side, or DSP-side improvement could be demonstrated at all.** Given that the brief's "next level" axes are dominated by exactly those changes, this is the finding that matters most.

---

## Proposed evaluation harness

Designed to be built incrementally; each tier is independently useful and the ordering is by value-per-day.

### Tier 1 — Corpora

**1a. `human-core` — the corpus that unblocks everything (highest value, human-gated).**

- **30 phrases × 10 speakers = 300 utterances**, recorded *on a Quest through the real capture path*, in three conditions each (quiet / game-audio-playing / post-exertion), giving **900 recordings**. Phrases: the 16 existing fixture phrases plus 14 chosen to cover the documented failure classes (short-function-word commands, homophone traps, alphanumeric identifiers, out-of-set-mode commands).
- Speakers: friends/testers under a written consent form. Ten is the practical floor for any per-speaker variance estimate; it is also what Timers-and-Such uses for `test-real` (10 speakers, 240 utterances).
- **Split it in half at generation time**: `human-dev` (5 speakers) committed and freely used; `human-holdout` (5 speakers) sealed per §12, opened once per release with a logged open-count.
- Cost: 1 day of tooling, ~4–6 hours of recording sessions, ~2 days of transcription/annotation. **This is the single highest-leverage item in the report and the only one Claude cannot do.**

**1b. `tts-broad` — replace one voice with many.**

- **Kokoro-82M** (Apache-2.0, 82M params, 54 voices across 8 languages, 24 kHz, StyleTTS2+ISTFTNet) as the primary generator, alongside the existing Piper renders for continuity. Pick **12 en_US/en_GB voices** spanning sex and timbre.
- Explicitly **not**: XTTS-v2 (Coqui Public Model Licence, non-commercial) or F5-TTS weights (CC BY-NC-4.0). Both would contaminate a commercial package's test suite.
- Piper's engine repo (`OHF-Voice/piper1-gpl`) is now **GPL-3.0**, and individual Piper voices carry per-voice licences in their `MODEL_CARD` — a generation-time tool, not a shipped dependency, so this is a hygiene note rather than a blocker, but the current `generate.sh` should record each voice's licence.
- Phrase list: the 30 `human-core` phrases (so TTS and human are *paired* on identical text — this is what makes the TTS-bias measurable) plus a long tail of ~200 grammar-derived phrasings.
- Target: **30 phrases × 12 voices = 360** paired utterances, plus **200 × 3 voices = 600** tail utterances.

**1c. `augmented` — conditions, not samples.**

Apply to both `tts-broad` and (read-only copies of) `human-core`:

| Stage | Source | Settings |
|---|---|---|
| Speed perturbation | Ko et al. 2015 | 0.9 / 1.0 / 1.1 |
| Room simulation | OpenSLR **SLR28** (Apache-2.0) | simulated small-room subset, RT60 0.2–0.5 s |
| Additive noise | **MUSAN** (CC BY 4.0) + DNS noise set | SNR 20 / 15 / 10 / 5 dB |
| Level | existing | peak ∈ [0.04, 0.4], the measured Quest range |

12 conditions per source utterance. Report as an **SNR curve** (accuracy vs SNR), never as a pooled pass rate — the augmented copies are not independent (§10).

**1d. `negative` — the missing half of the problem.**

- **≥5 hours** with no correct command in it: game music/SFX from the maintainer's title, breathing and exertion recorded during VR play, room tone, out-of-set conversational speech (LibriSpeech `test-clean`, CC BY 4.0), and the Speech Commands v2 `_background_noise_` and non-target words (CC BY 4.0).
- 5 h is the minimum at which a rate of "1 false accept per hour" is measurable at all with any precision; 20 h would be better and is cheap to assemble.

**1e. Off-domain sanity (optional).** Google **Speech Commands v2** (CC BY 4.0, 105,829 utterances / 35 words / 2,618 speakers) as a licence-clean multi-speaker source for single-word robustness and for the unknown/silence protocol. SLURP, FSC and ATCO2 are **methodology references only** — their licences (CC BY-NC / CC BY-NC-ND) exclude benchmarking a commercial package.

### Tier 2 — Metrics, defined

Primary, reported for every corpus and every release:

| Metric | Definition |
|---|---|
| **Intent accuracy (IA)** | fraction of positive utterances whose *set* of fired intents equals the expected set, in order. Exact — this is the user-visible bit. |
| **Slot F1** | micro-F1 over (slot name, value) pairs, with SLU-F1's relaxation (§1): a filler mismatch contributes a fractional FP+FN by normalised character distance rather than a hard miss. |
| **Semantic accuracy (SemAcc)** | IA **and** all required slots exactly right. The "command did what I meant" rate. |
| **False accepts per hour (FA/h)** | commands fired while replaying `negative`, ÷ negative hours. |
| **False reject rate at 1 FA/h (FRR@1)** | 1 − IA on positives, with thresholds set so FA/h = 1.0 on `negative`. Following Picovoice, thresholds are *tuned to the operating point*, then FRR compared. This is the only fair way to compare two systems that trade the two errors differently. |
| **Phantom rate** | commands fired *in excess of* the expected count on positive utterances, per utterance. VoXR-specific; the metric the 2.0.0 bar should have moved. |
| **Barred rate** | rounds suppressed by the leading-required-miss bar, per utterance, split by whether a legitimate command was thereby lost. Requires logging barred rounds (see Tier 4). |

Secondary/diagnostic: WER and per-class substitution table via `sclite`; per-word **ECE** (10 equal-width bins) and **NCE** (§11); pending/disambiguation entry rate; tie rate and sibling-tie rate (already in the log).

Latency (Tier 3) and telemetry (Tier 5) below.

### Tier 3 — Statistical procedure for an A/B

For any proposed change (scoring rule, threshold, grammar emission, decoder, DSP):

1. **Pre-register.** Before running: the corpus (which of `human-holdout` / `tts-broad` / `negative`), the primary metric (normally SemAcc), the direction, and the decision rule. Write it into the feature's requirements doc. This is what makes the holdout a holdout (§12).
2. **Run paired.** Same audio, same fixtures, arm A = current `main`, arm B = candidate. `stage.sh` already does exactly this for the parser; extend it to stage the decoder config and grammar too.
3. **Primary test — McNemar on per-utterance SemAcc.** Only *discordant* utterances count. Report `b` (A right, B wrong) and `c` (A wrong, B right), the exact binomial two-sided p, and the odds ratio. **Practical floor: with 5 discordant utterances all in one direction, p = 0.0625 — not significant. With 6, p = 0.031 — significant.** Fewer than 6 flips is never a result, however good it looks.
4. **Interval — blockwise bootstrap (§5), 10,000 replicates, block = speaker for `human-*`, block = voice for `tts-broad`, block = source clip for `augmented`.** Report the 95% CI of the *difference*, not of each arm. Never bootstrap at the utterance level on augmented data.
5. **Secondary — MAPSSWE via `sc_stats -t mapsswe`** on transcripts, when the change could move the decoder.
6. **Guardrails, checked every time regardless of the primary metric:** FA/h must not increase; p90 decision latency must not increase; the `tts-broad` number is reported but **never used for the ruling** (CHiME-4 rule, §4).
7. **Power, stated up front.** Detecting 90%→95% at 80% power needs **≈435 utterances per arm unpaired**, or **≈290** paired at a 10% discordance rate (≈322 at 6% discordance / 4:1 odds). Detecting 90%→93% needs ≈1,356 per arm unpaired. `human-core` at 900 recordings is sized for the first, not the second — so VoXR should expect to prove ~5-point changes and should stop claiming ~1-point ones.
8. **Effect sizes, not counts.** Retire the "N of 699 rows changed" report format; it has no inferential content.

### Tier 4 — Latency instrumentation

Add a monotonic-clock timestamp at each of T0–T5 (§8), carried alongside the audio/result through the existing queues:

- **Native side** (`ring_buffer.h`, `vosk_bridge.cpp`): stamp T0 on ring-buffer write (frame index → time is exact at 48 kHz, so a sample counter suffices and costs nothing); stamp T1 and T2 around `vosk_recognizer_accept_waveform_s`. Widen the result struct crossing the C ABI by two `int64` fields — an ABI change, so per project bindings the rebuilt arm64 `.so` must ship in `Runtime/Plugins/Android/arm64-v8a/`.
- **Managed side**: stamp T3 in the poll, T4 in `UtteranceBuffer` (recording *which* path closed it — window vs eager flush), T5 at event dispatch.
- **Derived and reported at p50/p90/p99**: decode latency (T2−T1), queue latency (T3−T2), buffer cost (T4−T3), parse+dispatch (T5−T4), and **decision latency** (T5 − end-of-speech).
- **End-of-speech reference**: for `tts-broad` and `augmented` it is exact by construction — `generate.py` pads `TAIL_SILENCE_S = 1.0`, so EOS = duration − 1.0 s. For `human-core`, annotate EOS once at transcription time. This is the FastEmit trick (forced-alignment EOS) obtained for free.
- **Report percentiles, never means** (Shangguan et al.: aggregate proxies mislead).
- Also measure and report the **grammar-rebuild audio gap** (~50 ms+, `command-sets.md`) as a first-class latency number rather than prose: time from `SetActiveCommandSets` to first accepted sample.

### Tier 5 — In-game opt-in telemetry

Default **off**; a single explicit consent prompt; per-session toggle visible in-game; a "what is collected" screen quoting the schema. **No audio ever leaves the device by default** — audio capture is a separate, second opt-in used only for an explicit "report this misrecognition" action, and then only the ~4 s window around the reported utterance.

Schema (a superset of today's session log, minus PII, plus latency and negatives):

```jsonc
{
  "schemaVersion": 3,
  "installId": "<random uuid, regenerated on opt-out>",   // not a device id
  "package": "2.0.0", "unity": "6000.4.7f1",
  "device": { "model": "Quest3", "os": "<build>" },       // coarse; no serial
  "sessionSeconds": 1840.5,                                // the FA/h denominator
  "activeMinutes": 22.1,
  "entries": [{
    "t": 1234.56,                                          // seconds since session start, not wall clock
    "activeSets": ["weapons"],
    "words": [{ "w": "launch", "c": 0.82, "s": 0.10, "e": 0.41 }],
    "attempts": [{
      "intent": "launch_weapon", "patternId": 3,
      "score": 0.83, "minScore": 0.6,
      "conf": 0.50, "minConfidence": 0.4,
      "accepted": true, "rejectReason": "",
      "barred": false,                                      // NEW — closes the audit gap
      "runnerUpIntent": "cease_fire", "runnerUpScore": 0.61, // NEW — near-miss distribution
      "tiedRival": "", "tiedRivalIsSibling": false
    }],
    "latencyMs": { "decode": 61, "queue": 8, "buffer": 480, "parse": 2, "decision": 551 },
    "userSignal": "none"        // none | undo | repeat | cancel | explicit_report
  }]
}
```

Three design points carry most of the value:

- **`sessionSeconds` is the FA/h denominator.** Without a duration, no in-product false-accept rate can ever be computed. This one field is why telemetry beats bug reports.
- **`userSignal` is implicit feedback** in the sense of Verma & Murari (NeurIPS 2021 HCAI workshop, *Interpreting voice assistant interaction quality from unprompted user feedback*): a command immediately undone, a phrase immediately repeated, or a cancel right after a fire are the cheap, unprompted signals of a defect. A rate of `undo`-within-3 s is a proxy for user-perceived quality that needs no labelling. (Amazon's broader work on implicit dissatisfaction signals — termination, rephrase, barge-in, frustration language — is `UNVERIFIED` here; only the Verma & Murari abstract was fetched.)
- **`runnerUpIntent`/`runnerUpScore` and `barred`** are the two fields the current log is missing (see audit). Together they turn the log from "what fired" into "what nearly fired," which is what confusion-pair mining and threshold sweeps both need.

**Aggregation.** A small offline tool (`Tools~/voxr-report`) that unions session files and emits: FA/h, phantom rate, intent-level confusion pairs sorted by frequency, the near-miss score histogram around `minScore`, latency percentiles, `userSignal` rates, and a per-release diff against a pinned baseline. This is the "dashboard" and it should be a static HTML file, not a service.

### Cost to build

| Item | Effort | Who |
|---|---|---|
| CHiME-4 dual-reporting rule + rename fixtures to "development set" | 0.5 day | Claude |
| Metric library (IA / SemAcc / slot-F1 / FA-h / phantom rate) + scorer over existing outputs | 2 days | Claude |
| Statistical module (McNemar exact, blockwise bootstrap, power calc) + report template | 2 days | Claude |
| Augmentation pipeline in `generate.py` (speed / RIR / noise / SNR sweep) + SLR28 & MUSAN fetch | 4 days | Claude |
| Kokoro multi-voice generation path alongside Piper | 1 day | Claude |
| Negative corpus assembly + FA/h replay harness | 3 days | Claude (audio sourcing needs the maintainer) |
| Latency instrumentation T0–T5 (native ABI change + managed + log schema v3) | 4 days | Claude; **on-device verification human-only** |
| Wire the A/B rig to the real decoder (push-audio path already exists) | 3 days | Claude |
| Telemetry schema v3, consent UI, aggregation tool | 5 days | Claude (consent copy needs the maintainer) |
| **`human-core` recording: 10 speakers × 30 phrases × 3 conditions** | **~1 day tooling + 6 h sessions + 2 days annotation** | **Maintainer — cannot be delegated** |

Roughly **25 engineering days plus one irreplaceable human week**. The ordering in "Ranked recommendations" front-loads the items that are cheap and unblock the rest.

---

## What this area cannot fix

- **Evaluation does not improve accuracy.** Every day spent here is a day not spent on the decoder, grammar, or parser. The harness's only justification is that without it the project cannot tell which of those days paid off — and the audit shows it currently cannot.
- **No harness manufactures speakers.** The dominant limitation is that VoXR has observed one human voice. Simulation, augmentation, TTS voices and statistics all leave that untouched (§2, §3). A maintainer-run recording session is the only fix and no amount of tooling substitutes.
- **Simulation is only "approximately" predictive** even when done well with real recorded noise and real arrays (CHiME-4). Convolving SLR28 RIRs onto Kokoro renders will not reproduce a Quest 3 microphone's frequency response, its beamforming, its AGC interaction, or the acoustic coupling of a headset to a face. Numbers from `augmented` are for *ranking conditions*, not for predicting field rates.
- **Statistics cannot rescue small n.** With 73 single-speaker utterances in 10 sessions, the honest 95% interval is ±7 points at best and wider once session correlation is respected. A better test does not narrow it; more data does.
- **Metrics do not define good UX.** Intent accuracy, FA/h and p90 latency say nothing about whether the disambiguation prompt is annoying, whether the confidence display is legible in VR, or whether push-to-talk beats continuous for a two-handed game. Those need playtesting, and the harness will happily optimise a system into a worse experience if allowed to be the only voice.
- **Telemetry has a floor of trust it cannot buy back.** An opt-in rate low enough to bias the sample (enthusiasts only) produces confidently wrong rates. And no schema removes the reality that voice telemetry is politically sensitive in VR; some fraction of users will never enable it regardless of design.
- **The holdout discipline is a human commitment, not a mechanism.** Thresholdout-style noise addition is disproportionate for a 300-utterance corpus. The seal holds only because the maintainer decides it does — and the history in `Planning~/` shows how strong the pull is to look.
- **Nothing here validates the on-device path.** Per the project's own bindings, Quest verification of mic capture, lifecycle delivery and native-bridge changes is human-only and cannot be claimed from WSL. A latency harness that runs in the Editor measures the Editor.

---

## Ranked recommendations for VoXR

**1. Record `human-core`: 10 speakers × 30 phrases × 3 conditions, split into a used half and a sealed half.**
Everything else in this report is a multiplier on evidence VoXR does not have. The entire evidence base today is two speakers, one of them a single Piper voice, and the acoustic corpus has 16 items — a ±0.10 to ±0.16 measurement instrument. Timers-and-Such's `test-real` is 240 utterances from 10 speakers and is considered small; VoXR's equivalent is 16 from one synthetic voice. This is the only recommendation that cannot be delegated, and every subsequent one is worth less without it. Sealing half at generation time (§12) costs nothing extra and is the only chance to get a clean holdout before the next tuning cycle spends it.

**2. Adopt CHiME-4's dual-reporting rule today, before building anything.**
Every accuracy claim reports a pair — (TTS number, human number) — and the ruling is made on the human number only. It costs a policy sentence and a table row, it can be adopted this week, and it immediately stops the failure mode the audit found: a synthetic-clean green run reading as acceptance evidence. Rename the 16 fixtures and 699 rows in the docs to *development* sets while doing it. Highest value per unit of effort in the report by a wide margin.

**3. Build the negative corpus and start reporting FA/h and FRR@1 FA/h.**
VoXR's most severe documented failure — a phantom command executing a maneuver order in the field — belongs to a metric family (FA/h × FRR at a fixed operating point) that the project has never computed, because it has zero negative hours. The 2.0.0 bar was justified with a false-*reject* argument on a positive-only corpus. Five hours of game audio, breathing, room tone and out-of-set speech, plus a replay scorer, converts the project's scariest failure from an anecdote into a rate that can be regressed against. It also supplies the guardrail that stops a future recall-improving change from silently costing safety.

**4. Put a decoder in the A/B loop, and put it in CI.**
`stage.sh` is excellent and stages nine parser files and no VOSK. As a result, no decoder-side, grammar-side or DSP-side change — which is most of what the brief's "next level" asks for — is currently measurable at all. The push-audio bridge mode already exists (built for the WAV-replay suite), so the missing piece is plumbing, not architecture: stage the grammar and decoder config alongside the parser sources, replay real audio, and score with the Tier 2 metrics. Move the rig out of gitignored `Planning~/` so it survives a rename sweep and can run unattended.

**5. Replace count-reporting with McNemar + blockwise bootstrap, and pre-register the primary metric.**
"48 of 699 rows changed" is not evidence and, because the rows are dependent, cannot be made into evidence by any standard test. Adopting exact McNemar on per-utterance SemAcc with a session/speaker-blocked bootstrap CI is roughly 200 lines of Python and immediately imposes honest resolution — including the bracing rule that **fewer than 6 discordant utterances in one direction is never significant**, and that proving a 1-point change would need on the order of a thousand utterances per arm. Pre-registering the metric and corpus in the feature's requirements doc, before the run, is what keeps the sealed holdout from being spent by the same adaptive process (§12) that has already consumed the fixture suite.

*Just below the cut, and cheap: log `barred` and `runnerUpIntent`/`runnerUpScore` in the session log (schema v3). Two fields close the largest blind spot in an otherwise excellent telemetry schema — the log currently cannot measure the cost of the rule 2.0.0 shipped on.*

---

## Sources

**Fetched and verified**

1. Bastianelli, Vanzo, Swietojanski, Rieser. *SLURP: A Spoken Language Understanding Resource Package.* EMNLP 2020, 7252–7262. https://aclanthology.org/2020.emnlp-main.588/ · https://arxiv.org/abs/2011.13205 · https://ar5iv.labs.arxiv.org/html/2011.13205 · repo/licence https://github.com/pswietojanski/slurp
2. Lugosch, Papreja, Ravanelli, Heba, Parcollet. *Timers and Such: A Practical Benchmark for Spoken Language Understanding with Numbers.* NeurIPS 2021 Datasets & Benchmarks. https://arxiv.org/abs/2104.01604 · https://ar5iv.labs.arxiv.org/html/2104.01604
3. Hilmes, Rossenbach, Schlüter. *On the Effect of Purely Synthetic Training Data for Different Automatic Speech Recognition Architectures.* arXiv:2407.17997, 2024. https://arxiv.org/html/2407.17997v1
4. CHiME-4 Challenge instructions. https://www.chimechallenge.org/challenges/chime4/instructions
5. Liu, Peng. *Statistical Testing on ASR Performance via Blockwise Bootstrap.* Interspeech 2020 / arXiv:1912.09508. https://arxiv.org/abs/1912.09508
6. NIST Speech Recognition Scoring Toolkit (SCTK) — `sclite` 2.10, `sc_stats` 1.3, `rover`, `asclite`. https://github.com/usnistgov/SCTK
7. Warden. *Speech Commands: A Dataset for Limited-Vocabulary Speech Recognition.* arXiv:1804.03209, 2018. https://arxiv.org/abs/1804.03209 · https://ar5iv.labs.arxiv.org/html/1804.03209 · licence/splits https://www.tensorflow.org/datasets/catalog/speech_commands
8. Picovoice wake-word detection benchmark (methodology; code Apache-2.0). https://picovoice.ai/docs/benchmark/wake-word/
9. Yu et al. *FastEmit: Low-latency Streaming ASR with Sequence-level Emission Regularization.* ICASSP 2021 / arXiv:2010.11148. https://arxiv.org/abs/2010.11148
10. Shangguan, Prabhavalkar, Su, Mahadeokar, Shi, Zhou, Wu, Le, Kalinli, Fuegen, Seltzer. *Dissecting User-Perceived Latency of On-Device E2E Speech Recognition.* Interspeech 2021, 4553–4557, DOI 10.21437/Interspeech.2021-1887. https://www.isca-archive.org/interspeech_2021/shangguan21_interspeech.html
11. Zuluaga-Gomez, Veselý et al. *ATCO2 corpus.* arXiv:2211.04054 v2, 2023. https://arxiv.org/html/2211.04054
12. Ko, Peddinti, Povey, Khudanpur. *Audio Augmentation for Speech Recognition.* Interspeech 2015, 3586–3589. https://www.isca-archive.org/interspeech_2015/ko15_interspeech.html
13. OpenSLR SLR28 — RIR and Noise Database (Apache-2.0), accompanying Ko et al., ICASSP 2017. https://www.openslr.org/28/
14. Snyder, Chen, Povey. *MUSAN: A Music, Speech, and Noise Corpus.* arXiv:1510.08484, 2015. https://arxiv.org/abs/1510.08484v1 · licence https://www.openslr.org/17/
15. Wang, Soltau, El Shafey, Shafran. *Word-level confidence estimation for RNN transducers.* ASRU 2021 / arXiv:2110.15222. https://arxiv.org/abs/2110.15222
16. Shapira, Chazan, Cohen. *Measuring the Effect of Transcription Noise on Downstream Language Understanding Tasks.* arXiv:2502.13645, 2025. https://arxiv.org/html/2502.13645
17. Szymański, Żelasko, Morzy et al. *WER we are and WER we think we are.* Findings of EMNLP 2020. https://aclanthology.org/2020.findings-emnlp.295/
18. Lugosch, Ravanelli, Ignoto, Tomar, Bengio. *Speech Model Pre-training for End-to-End Spoken Language Understanding.* Interspeech 2019 / arXiv:1904.03670. https://arxiv.org/abs/1904.03670
19. Coucke, Saade, Ball et al. *Snips Voice Platform: an embedded Spoken Language Understanding system for private-by-design voice interfaces.* arXiv:1805.10190, 2018. https://arxiv.org/abs/1805.10190
20. Alharthi, Sharma, Dhamyal, Maiti, Raj, Singh. *Evaluating Speech Synthesis by Training Recognizers on Synthetic Speech.* arXiv:2310.00706, 2023. https://arxiv.org/abs/2310.00706
21. Verma, Murari. *Interpreting voice assistant interaction quality from unprompted user feedback.* NeurIPS 2021 Workshop on Human Centered AI. https://www.amazon.science/publications/interpreting-voice-assistant-interaction-quality-from-unprompted-user-feedback
22. Kokoro-82M model card (Apache-2.0, 82M params, 54 voices / 8 languages, 24 kHz, StyleTTS2 + ISTFTNet). https://huggingface.co/hexgrad/Kokoro-82M
23. Piper TTS (`piper1-gpl`, GPL-3.0; per-voice licences in each `MODEL_CARD`). https://github.com/OHF-Voice/piper1-gpl · https://github.com/OHF-Voice/piper1-gpl/blob/main/docs/VOICES.md
24. Dwork, Feldman, Hardt, Pitassi, Reingold, Roth. *The reusable holdout: Preserving validity in adaptive data analysis.* *Science* 349(6248):636–638, 2015. https://www.science.org/doi/10.1126/science.aaa9375 · https://www.cis.upenn.edu/~aaroth/reusable.html

**`UNVERIFIED` — existence confirmed via search results or third-party listings, full text not fetched**

25. Bisani, Ney. *Bootstrap estimates for confidence intervals in ASR performance evaluation.* ICASSP 2004. https://ieeexplore.ieee.org/document/1326009/ — PDF would not decode in this environment.
26. Liu et al. *Modeling Dependent Structure for Utterances in ASR Evaluation.* Interspeech 2023 / arXiv:2209.05281. https://arxiv.org/abs/2209.05281
27. *A Real-World Dataset for Benchmarking False Alarm Rate in Keyword Spotting.* IOS Press FAIA, 2023, doi:10.3233/FAIA230695. https://ebooks.iospress.nl/doi/10.3233/FAIA230695
28. Barker, Marxer, Vincent, Watanabe. *The third 'CHiME' Speech Separation and Recognition Challenge: Dataset, Task and Baselines.* ASRU 2015. https://www.merl.com/publications/docs/TR2015-136.pdf — PDF would not decode.
29. Ko, Peddinti, Povey, Seltzer, Khudanpur. *A study on data augmentation of reverberant speech for robust speech recognition.* ICASSP 2017, 5220–5224, doi:10.1109/ICASSP.2017.7953152 — dataset page fetched (source 13); paper text not fetched.
30. Marxer, Barker, Alghamdi, Maddock. *The impact of the Lombard effect on audio and visual speech recognition systems.* *Speech Communication*, 2018. https://www.sciencedirect.com/science/article/pii/S0167639317302674 — publisher returned HTTP 403. Search summaries report a 20–30% drop in correctness, equivalent to a ~9 dB SNR loss; **treat that figure as unconfirmed.**
31. Microsoft DNS Challenge noise set (181 h, ~62k clips, ~150 classes). https://github.com/microsoft/DNS-Challenge — figures from challenge-paper summaries; per-clip licences not individually verified.
32. Fluent Speech Commands Public Licence (CC BY-NC-ND 4.0; "not authorized to be used for any commercial purpose, including training, testing, bench-marking, or developing a product"). https://fluent.ai/wp-content/uploads/2021/04/Fluent_Speech_Commands_Public_License.pdf — licence text summarised from search results, not fetched directly.
33. Coqui XTTS-v2 (Coqui Public Model Licence, non-commercial) and F5-TTS weights (CC BY-NC-4.0). https://huggingface.co/coqui/XTTS-v2 — licence status from search summaries.

**Repository sources examined (this codebase)**

`Planning~/research/2026-09-05-next-level/00-brief.md` · `Documentation~/editor-testing.md` · `Documentation~/scoring.md` ("Reading a session log") · `Runtime/Testing/VoxrBatchTestRunner.cs` · `Tests~/Fixtures/generate.py`, `generate.sh`, `audio/manifest.json` (16 cases, `en_US-lessac-medium`) · `Tests~/Fixtures/audio/human/` (empty) · `Planning~/features/coverage-in-selection/ab-rig/{stage.sh, perturb.py, ablate.py, issue124/README.md}` · `Planning~/features/coverage-in-selection/phase7-corpus.tsv` (699 rows) · `Planning~/voxr-system-context.md`.
