# R3 — Keyword spotting, end-to-end SLU, VAD, endpointing, and emission latency

Research report, 2026-09-05. Scope: what modern (2021–2026) work on KWS / E2E SLU / VAD / endpointing / emission-latency offers VoXR, a Unity offline voice-command package on Meta Quest built on VOSK (Kaldi nnet3 chain, `vosk-model-small-en-us-0.15`) in grammar mode.

Pipeline stage numbers refer to §2 of `Planning~/research/2026-09-05-next-level/00-brief.md`.

## Executive summary

- **The dominant latency term is not VoXR's, it is Kaldi's endpointer, and it is currently un-tuned.** `NativeBridge~/vendor/vosk-model-small-en-us-0.15/conf/model.conf` overrides only `--endpoint.rule2/3/4.min-trailing-silence` to `0.5 / 0.75 / 1.0` s; everything else falls back to Kaldi's `OnlineEndpointConfig` defaults, so a *complete-per-the-grammar* utterance still costs **≥ 500 ms of trailing silence** before VOSK emits a final. VoXR never calls `vosk_recognizer_set_endpointer_delays` or `vosk_recognizer_set_endpointer_mode`, both of which exist in `vosk_api.h` and take effect at runtime.
- **The eager flush cannot beat the endpointer, because it only ever sees finals.** `VoxrCommandRecogniser.HandleResult` (buffer append + `ProbeEagerCommit`) is wired to `OnResult`; `HandlePartialResult` only stores `LastPartialResult`, and only under `#if UNITY_EDITOR`. So eager flush removes the 2.0 s buffer window but leaves the 0.5–1.0 s silence wait plus Quest decoder catch-up intact. Running the same `TryEagerCommit` scan on *partial* results is the single largest available cut and is pure C#/bridge plumbing — `vosk_recognizer_set_partial_words` even restores per-word `conf` so eager condition 7 need not be bypassed.
- **VoXR's eager flush is a re-invention of grammar-completeness endpointing, and the prior art is inside Kaldi itself.** `max_relative_cost` in `OnlineEndpointRule` is documented as "requires relative-cost of final-states to be <= this value" — Kaldi rule2 fires at 0.5 s *only when the decoder has reached a final state with good probability*, i.e. only when the grammar says the utterance is complete. VOSK does not expose that cost through its C ABI, which is exactly why VoXR had to rebuild the test on the transcript.
- **Text-enrolled ("open-vocabulary") KWS is now real and small enough for Quest**, but it buys short fixed phrases, not slots. sherpa-onnx ships Apache-2.0 streaming Zipformer KWS models at ~3.3 M parameters where keywords are supplied as text at runtime (`keywords.txt`, per-keyword boost `:` and threshold `#`), with Android arm64 and C# bindings. Research models are smaller still: CED 155 K params (Interspeech 2024), MALEFA 650 K / 93 MFLOPs (2026). None of them fills `{heading} two seven zero`.
- **End-to-end SLU is blocked on VoXR's actual constraint: no per-game audio.** Every SLURP/FSC-class model needs supervised audio→intent data. The best evidence that TTS closes the gap is Lugosch et al. 2019: on Fluent Speech Commands a model trained on **22 synthetic voices only** reaches **94.9 % ± 0.2** vs **99.1 % ± 0.1** for real speech (77 speakers) — a 4-point gap on a 248-sentence closed set. Synth4Kws (Google, 2024) shows the same shape for KWS: TTS-only improves monotonically with phrase diversity, and TTS+50 k real utterances beats real-only by 30.1 % EER. Useful, not free: it still means a per-game training job.
- **Picovoice Rhino is the productised version of what VoXR wants** (text/YAML context → on-device intent+slots, no transcript, `isUnderstood: false` instead of a hallucinated guess) — but its contexts are compiled by a cloud console, its Unity SDK is **deprecated as of v4.0.0 (Dec 2025)**, and the licence is commercial. It is a design reference, not a dependency.
- **VAD is cheap enough to add and would close the buffer window on evidence rather than on a timer.** TEN VAD ships a **532 KB Android arm64** library at **RTF 0.057 on a Galaxy J6+**, 16 ms hops, Apache-2.0-with-conditions; Silero VAD is ~2 MB, MIT, <1 ms per 30 ms chunk on one CPU thread. Neither distinguishes a cough from a word — they distinguish *voiced sound* from silence — so a VAD fixes "the buffer waits 2 s after you stop" and only partially fixes "a cough decodes as `on`".
- **Learned end-of-utterance models exist and are all the wrong size or the wrong task.** Smart Turn v2 is 360 MB of waveform model; LiveKit Turn Detector v1 is a distilled Qwen2.5-0.5B (0.1 B INT8) over *transcript text*, under a bespoke non-OSS licence; TEN Turn Detection wraps Qwen2.5-7B. All are tuned for conversational turn-taking, not for "is this tactical order syntactically finished". VoXR's grammar already answers that question for free.
- **Emission-latency training (FastEmit, delay-penalty, minimum-latency) is a real 100–200 ms lever but only for whoever trains the acoustic model.** It does not apply to a pretrained Kaldi chain model. It becomes relevant the moment VoXR considers a k2/icefall Zipformer (sherpa-onnx), whose recipes support delay penalty natively.
- **Target to design against: ~200–300 ms.** Human conversational response offsets peak within 200 ms of turn end across 10 languages (Stivers et al., PNAS 2009); voice-agent practice converges on 300 ms. VoXR's measured-by-construction budget is **~550–1100 ms (eager) to ~2550–3100 ms (time-driven)** after end of speech, plus an unmeasured Quest decoder catch-up the docs put at 0.5–1.0 s. Even the eager path is 2–4× over target.

## Findings table

| Name | Key citation | What it does | Reported effect (numbers, dataset) | Model size / CPU cost | Needs per-game training data? | Pipeline stage (§2) | Expected gain for VoXR | Main risk |
|---|---|---|---|---|---|---|---|---|
| Kaldi online endpointer rules | Kaldi `src/online2/online-endpoint.h`, kaldi-asr.org (canonical) | Silence + final-state-cost rules that close an utterance | Defaults rule1 5.0 s/∞, rule2 0.5 s/cost≤2.0, rule3 1.0 s/cost≤8.0, rule4 2.0 s/∞, rule5 20 s; VoXR's model.conf overrides rule2/3/4 silences to 0.5/0.75/1.0 s | Free (already running) | No | 3 (decoder) | Tuning `t_end` down is the cheapest latency cut available; **0.5 s** is the current floor | Shorter silence = more mid-command splits, the exact failure the 2 s buffer exists to hide |
| VOSK runtime endpointer API | `vosk_api.h`, alphacep/vosk-api (canonical) | `set_endpointer_mode` (DEFAULT/SHORT/LONG/VERY_LONG), `set_endpointer_delays(t_start_max, t_end, t_max)` | No published numbers; scales the rules above | Free | No | 3 | Direct control over the 500–1000 ms wait, per command set | Header's units comment is self-contradictory ("milliseconds … usually 0.5–1.0"); must be measured, not assumed |
| VOSK `set_grm` on a live recognizer | `vosk_api.h`, alphacep/vosk-api | "Reconfigures recognizer to use grammar" without recreating it | — | Free | No | 4 (grammar mode) | Candidate fix for the ~50 ms+ audio gap on set/slot changes | Unknown whether it resets decoder state mid-utterance; undocumented beyond the one-line comment |
| Eager scan on partials | This report (VoXR code) + Kaldi rule2 as prior art | Run `TryEagerCommit` on VOSK partial results, not only finals | — (unmeasured) | Free; one speculative parse per partial | No | 5–6 (buffer/parser) | Removes the whole endpointer wait for complete unambiguous commands: −500 to −1000 ms | Partial hypotheses are unstable; a command could fire on a transcript the final revises |
| TEN VAD | TEN-framework/ten-vad, 2025 (repo) | Frame-level speech/non-speech, 10/16 ms hops | RTF 0.0570 on Galaxy J6+ (Android); claims better precision-recall than WebRTC VAD and Silero on LibriSpeech/GigaSpeech/DNS test sets | 532 KB `.so` (arm64-v8a); 306 KB Linux x64 | No | 1–2 (capture/DSP) | Close the buffer window on measured offset instead of a 2.0 s timer | Speech-vs-silence only; will not tell a cough from "on". Apache-2.0 **with additional conditions** + LPCNet-derived BSD file |
| Silero VAD | snakers4/silero-vad (repo) | Same, ONNX/JIT | "<1 ms per 30+ ms chunk on a single CPU thread"; 8/16 kHz | ~2 MB JIT | No | 1–2 | Same as above, MIT-clean | Larger + slower than TEN VAD; repo reports no in-page accuracy table |
| WebRTC VAD | Google WebRTC GMM VAD (`py-webrtcvad`) `UNVERIFIED` | 6-subband GMM, 10/20/30 ms frames, 4 aggressiveness modes | No modern numbers found on a fetched page | Tens of KB, negligible CPU | No | 1–2 | Zero-dependency fallback | Weak in noise; aggressiveness trades misses for false alarms |
| MarbleNet | Jia, Majumdar, Ginsburg, ICASSP 2021 `UNVERIFIED` | 1D time-channel-separable CNN VAD | "similar performance at ~1/10 the parameter cost" of prior SOTA; configs cited at ~90 K params | ~90–140 K params | No | 1–2 | Alternative VAD if a trainable one is wanted | Not fetched; NeMo-centric tooling, no shipped arm64 build |
| sherpa-onnx KWS (Zipformer transducer) | k2-fsa/sherpa-onnx + `k2-fsa.github.io/sherpa/onnx/kws/` (docs) | "A tiny ASR that can only decode the given keywords"; keywords supplied as text at runtime with per-keyword boost `:` and threshold `#` | No FA/FR numbers published in the docs | `sherpa-onnx-kws-zipformer-gigaspeech-3.3M` (EN), `-zh-en-3M`; Apache-2.0; Android arm64 + C# bindings | **No** — text enrollment | 3–4 (could replace or gate the decoder for short commands) | A parallel fast path for slot-free commands ("cease fire", "brace for impact") with a per-phrase threshold VoXR does not have today | No slots, no number sequences; unpublished FA rate; a second model in RAM/APK |
| CED (CTC-aligned audio-text embedding) | Jin, Jung, Lee, Roh, Han, Cho, Interspeech 2024, arXiv:2406.07923 | Streaming open-vocab KWS; CTC aligns audio to keyword text on the fly | "competitive with non-streaming methods" on LibriPhrase; O(U) decode | **155 K params** | No | 3–4 | Proof that text-enrolled streaming KWS fits in a rounding error of the current 40 MB model | Research code/weights, LibriPhrase-only evidence, no FA/hour |
| PhonMatchNet | Lee & Cho (NCSOFT), Interspeech 2023, arXiv:2308.16511 | Phoneme-guided zero-shot user-defined KWS, two-stream encoder + phoneme-level loss | Avg **67 % relative EER** and **80 % relative AUC** improvement over baseline across familiar words, proper nouns, confusable pronunciations | Not stated on the abstract page | No | 3–4 | Directly addresses VoXR's OOV problem ("cqb", "pdc") — phoneme-level text enrollment instead of hand-written aliases | Isolated-phrase evaluation; no continuous-speech or FA/hour numbers |
| CLAD (contrastive + audio discrimination) | Xi et al., arXiv:2401.06485 (2024) | Adds audio-audio discrimination to audio-text matching for KWS in **continuous** speech | 8.65 % EER on LibriPhrase-hard; **16.95× speedup** vs two-stage baselines on LibriSpeech-derived continuous test | 2.2 M params | No | 3–4 | The continuous-speech variant of the above — closest to VoXR's always-on stream | Trades accuracy on frequent words for generalisation |
| MALEFA | Li, Lo, Hung, Huang, Chen, arXiv:2604.03689 (2026) | Utterance+phoneme contrastive learning with an explicit false-alarm-aware loss | AUC 93.58 %, EER 13.91 % (LibriPhrase-hard); **FAR 0.007 % on AMI**; acc 90–99.98 % | **650 K params, 93 MFLOPs** | No | 3–4 | The false-alarm-aware loss is the direct analogue of VoXR's coverage/bar rules, learned instead of authored | 2026 preprint, no released weights found |
| Prefix bias in OV-KWS | Liu, Huang, Quan, ICASSP 2026, arXiv:2602.08930 | Shows OV-KWS over-weights initial phonemes; adds Equal-weighting Position Scoring | POB-Spark EER **64.4 % → 29.3 %**; POB-LibriPhrase acc 87.6 % → 96.8 % | Lightweight (scoring change) | No | 3–4 | **Disqualifying evidence for naive KWS on VoXR's grammar**: "switch to weapons"/"switch to navigation" is exactly a shared-prefix negative pair | Confirms an off-the-shelf spotter will confuse VoXR's sibling commands |
| U2-KWS | Zhang, Zhou, Huang, Zou, Liu, Xie, ASRU 2023, arXiv:2312.09760 | Two-pass: CTC branch proposes keyword candidates, decoder branch validates with keyword bias | **+41 % relative wake-up rate at 0.5 false alarms/hour** vs customised KWS baseline (internal set, Aishell-1) | Not stated on abstract page | Model yes; keywords no | 3–4 | Architecture template for "cheap spotter proposes, expensive/grammar path confirms" | Chinese-centric evidence; needs training |
| TDT-KWS / MFA-KWS | Xi et al., ICASSP 2024 (arXiv:2403.13332); Xi et al., TASLP 2025 (arXiv:2505.19577) | Frame-**asynchronous** keyword search with token-and-duration transducer; multi-head CTC+TDT fusion | MFA-KWS: **47–63 % decoding speed-up** over frame-synchronous baselines; SOTA on Snips, MobvoiHotwords, LibriKWS-20 | Not stated on abstract pages | Model yes | 3 | Where the CPU budget would come from if a spotter runs alongside VOSK | Numbers behind paywall/PDF; no released arm64 build |
| Small-footprint KWS survey | Garai & Samui, Eng. Appl. AI, 2025, arXiv:2506.11169 | Taxonomy + Pareto view of SF-KWS | BC-ResNet-1 96.9 % @ 9.2 K params/1.5 M MACs; MatchboxNet 97.3–97.6 % @ 77–140 K; INT8 cuts size up to 69 % | — | — | 3 | Sets the realistic budget: a whole KWS model is 10 K–3 M params, i.e. <1 % of the 40 MB VOSK model | Survey states unseen-keyword degradation is still unsolved |
| Synth4Kws / LLM-Synth4KWS | Zhu, Agarwal, Bartel, Partridge, Park, Wang, SynData4GenAI @ Interspeech 2024, arXiv:2407.16840; Zhu, Wang, Agarwal, Partridge, Interspeech 2025, arXiv:2505.22995 | Train custom KWS from TTS; then LLM-generate *confusable* phrases and synthesise them | TTS-only improves monotonically with phrase diversity (11 k Speech Commands utts); +TTS on a 50 k-real baseline: **EER −30.1 %, AUC +46.7 %**. LLM-Synth4KWS: **AUC +3.7 %, c-AUC +11.3 %** | — | Yes, but data is **generated from text** | 3–4 | The only credible route from "developer typed a command" to "model that recognises it" without recordings | Requires an offline training pipeline VoXR does not have; single-word English evidence |
| TTS-trained E2E SLU | Lugosch, Meyer, Nowrouzezahrai, Ravanelli, arXiv:1910.09463 (2019) | Train E2E SLU on synthesised speech only / as augmentation | FSC: **94.9 ± 0.2 % (22 synthetic voices only)** vs **99.1 ± 0.1 % (real)**; Snips: 65.5 ± 2.9 → **71.4 ± 1.4 %** with synthetic added | Small RNN-based SLU | Yes (generated) | 3–6 (replaces decoder+parser) | Quantifies the TTS gap for the exact task VoXR would need: ~4 points on a closed 248-sentence set | 2019 TTS; closed intent set; no slot-filling of arbitrary numbers |
| Two-pass low-latency E2E SLU | Arora, Dalmia, Chang, Yan, Black, Watanabe, Interspeech 2022, arXiv:2207.06670 | Fast acoustic-only first pass, deliberation second pass with a pretrained LM | Improves FSC Challenge + SLURP while reducing inference latency (no ms figures on the abstract page) | ESPnet-SLU scale (100 M+) | Yes | 3–6 | Structural template only | Far too large for Quest CPU alongside a 90 fps game |
| Spoken function calling | Peng, Liu, Gao, Gao, Li, Chen, arXiv:2608.05126 (2026) | Audio → structured API call, in-context definitions | Text models +5–18 % over classic SLU; audio pipeline degrades 80 % → ~30 % accuracy as complexity rises | 7 B (SpokenFC-7B) | Zero-shot via in-context defs | 6 | Conceptually the right abstraction (commands as typed function signatures) | Not on-device at any scale VoXR can host; ASR error propagation is the reported failure mode |
| Picovoice Rhino | Picovoice/rhino (repo), picovoice.ai docs | On-device speech→intent from a YAML context (expressions, slots, macros); returns `isUnderstood: false` rather than guessing | Vendor-claimed 97.3 % at 6–24 dB SNR, >99 % clean | `.rhn` context + `rhino_params.pv`; Cortex-M to Android | **No** — context authored as text | 3–6 (replaces the whole decode+parse) | The existence proof that text-authored grammar → on-device intent works commercially | Cloud console compiles contexts; **Unity SDK deprecated v4.0.0 (Dec 2025)**; commercial licence |
| FastEmit | Yu, Chiu, Li, Chang, Sainath, He, Narayanan, Han, Gulati, Wu, Pang, ICASSP 2021, arXiv:2010.11148 | Sequence-level emission regularisation for transducers, no alignments needed | LibriSpeech WER 4.4/8.9 → 3.1/7.5 % **and P90 emission latency 210 ms → 30 ms**; 150–300 ms reduction on Voice Search | Training-time only | Yes (retrain acoustic model) | 3 | Only if VoXR ever trains/adopts a transducer | Inapplicable to a pretrained Kaldi chain model |
| Delay-penalized transducer | Kang, Yao, Kuang, Guo, Yang, Lin, Żelasko, Povey, ICASSP 2023, arXiv:2211.00490 | Adds `λ(T/2 − t)` to non-blank log-probs during the transducer recursion | "similar delay-accuracy trade-offs to FastEmit"; numbers in PDF only | Training-time only | Yes | 3 | Native in k2/icefall → free if sherpa-onnx models are ever retrained | Same |
| Minimum-latency training | Shinohara & Watanabe, Interspeech 2022, arXiv:2211.02333 | Expected latency along lattice diagonals inside the forward-backward | WSJ causal Conformer-T: **220 ms → 27 ms** emission latency at **0.7 % WER** cost; beats Ar-RNNT (110 ms) and FastEmit (67 ms) | Training-time only | Yes | 3 | Best published emission-latency/accuracy trade-off | Same |
| U2++ / dynamic chunk (WeNet) | Wu et al., arXiv:2106.05642; Yao et al., Interspeech 2021 `UNVERIFIED` | One model, chunk size chosen at inference | chunk=16 ⇒ 0–640 ms, avg 320 ms latency; Fast-U2++ 5.06 % CER at 80 ms vs 5.05 % at 320 ms | Conformer-scale | Yes | 3 | Shows chunk size is a tunable, not a constant — VoXR's analogue is the 4096-sample read cap | Not fetched; Conformer inference cost on Quest CPU unknown |
| Emformer | Shi, Wang, Wu, Yeh, Chan, Zhang, Le, Seltzer, ICASSP 2021 `UNVERIFIED` | Memory-bank streaming transformer AM | 960 ms avg latency: 2.50/5.62 % WER; **80 ms** avg latency: 3.01/7.09 % WER (LibriSpeech) | Large | Yes | 3 | Quantifies what an 80 ms look-ahead costs in WER (~+0.5 abs) | Not fetched; far above Quest budget |
| Unified ASR + endpointing | Bijwadia, Chang, Li, Sainath, Zhang, He, SLT 2022, arXiv:2211.00786 | One E2E model does ASR and EOQ, with a switch between raw audio and encoder latents | **Median EP latency −120 ms (−30.8 %), P90 −170 ms (−23.0 %)**, no WER regression on voice search; −10.6 % rel WER on continuous | Adds a head to an existing E2E model | Yes | 3 | The canonical "endpoint from what the recogniser already knows" result — VoXR's eager flush is the hand-written version | Requires an E2E model VoXR does not have |
| Two-pass endpoint detection ("EP Arbitrator") | Raju, Khare, He, Sklyar, Chen, Alptekin, Trinh, Zhang, Vaz, Ravichandran, Maas, Rastrow, ASRU 2023, arXiv:2401.08916 | A second model verifies the first-pass endpoint before committing | Gains on voice-assistant, conversational, and public SLURP; no ms figures in the abstract | Second small model | Yes | 5 | Architecture match for "commit the eager flush only if a verifier agrees" | Numbers not in fetched abstract |
| Streaming endpointer on codec features + label delay | Udupa, Watanabe, Schwarz, Černocký, arXiv:2506.07081 (2025) | Neural-audio-codec features + deliberately delayed labels for a streaming endpointer | At **160 ms median latency**: −42.7 % (single-stream) / −37.5 % (two-stream) relative cutoff errors; with a codec speech-LLM, −1200 ms median response and −35 % cutoffs | Not stated | Yes | 5 | Shows 160 ms endpointing is achievable — a 3× improvement over VoXR's 500 ms floor | Needs a codec front end and training data |
| Predictive ASR + EOU | Zink, Higuchi, Mullov, Waibel, Kobayashi, arXiv:2409.19990 (2024) | Mask up to 500 ms of the utterance end in training; predict remaining words and time-to-end | **~100 ms average error predicting 500 ms ahead**; usable up to 300 ms before completion; WER degradation 14.3 % → 13.2 % at 500 ms mask | Encoder-decoder ASR | Yes | 3+5 | The strongest form of what VoXR wants: fire *before* the speaker finishes | Only meaningful with a trainable E2E model |
| Endpoint anticipation | Udupa, Watanabe, Schwarz, Černocký, arXiv:2606.13450 (2026) | Anticipatory endpointing rather than reactive VAD | PDF did not parse; numbers not extracted | — | Yes | 5 | Same family as above, newest | Numbers unverified |
| Smart Turn v2 | pipecat-ai / Daily, 2025 `UNVERIFIED` | Semantic VAD over raw waveform: has the speaker finished? | 14 languages; "6× smaller" at **360 MB**; 12 ms inference on an L40S GPU | 360 MB | No | 5 | None — size and GPU assumption rule it out | Conversational-turn task ≠ command-complete task |
| LiveKit Turn Detector v1 | livekit/turn-detector (HF model card) | EOU probability from the **transcript**, distilled from a 7 B teacher | TPR 99.3–99.4 %, TNR 85.1–96.3 % across 14 languages; v0.4.1 +39.23 % relative on structured inputs | Qwen2.5-0.5B base, **0.1 B INT8 ONNX** | No | 5 | None directly; but confirms a *text*-side EOU signal is the effective one — which VoXR's grammar already provides for free | Bespoke "LiveKit Model License", not OSI; 0.1 B params is still ~100 MB-class on a 40 MB APK budget |
| TEN Turn Detection | TEN framework, 2025 `UNVERIFIED` | finished / unfinished / wait from transcript | — | Qwen2.5-**7B** | No | 5 | None | Far out of budget |
| Conversational timing target | Stivers et al., PNAS 106(26):10587, 2009 `UNVERIFIED` | Cross-linguistic turn-transition offsets | Unimodal peak **within 200 ms** of question end across 10 languages; cross-language means within a 250 ms band | — | — | — | Sets the design target VoXR should be judged against | Conversation, not command-and-control; transfer to VR command latency is an assumption |
| User-perceived latency accounting | Shangguan, Prabhavalkar, Su, Mahadeokar, Shi, Zhou, Wu, Le, Kalinli, Fuegen, Seltzer, Interspeech 2021, arXiv:2104.02207 | Defines UPL = end of speech → mic close, decomposed | Worked example: **1000 ms UPL = 600 ms endpointer lag + 400 ms decoder catch-up**; first-token delay 200 ms measured separately | — | — | All | The measurement framework VoXR's budget below is built on | Their stack is an E2E transducer, not Kaldi |

## Detailed findings

### 1. Kaldi's endpointer already does grammar-aware endpointing; VOSK just hides it — `VERIFIED`

- Source: Kaldi `src/online2/online-endpoint.h`, https://kaldi-asr.org/doc/online-endpoint_8h_source.html (canonical, fetched); VoXR's `NativeBridge~/vendor/vosk-model-small-en-us-0.15/conf/model.conf` (read).
- **Mechanism.** Each `OnlineEndpointRule` is `(must_contain_nonsilence, min_trailing_silence, max_relative_cost, min_utterance_length)`. `max_relative_cost` is documented as "requires relative-cost of final-states to be <= this value (describes how good the probability of final-states is)". Defaults: rule1 `(false, 5.0, ∞, 0.0)`, rule2 `(true, 0.5, 2.0, 0.0)`, rule3 `(true, 1.0, 8.0, 0.0)`, rule4 `(true, 2.0, ∞, 0.0)`, rule5 `(false, 0.0, ∞, 20.0)`.
- **Evidence.** VoXR's shipped `model.conf` sets `--endpoint.rule2.min-trailing-silence=0.5`, `rule3=0.75`, `rule4=1.0` and `--endpoint.silence-phones=1:2:...:10`, leaving all `max_relative_cost` values at Kaldi defaults. So on a **grammar** decoding graph, rule2 means: "0.5 s of silence *and* the decoder is in a final state of the phrase grammar with good probability". That is semantic/grammar-complete endpointing, already in the loop.
- **VoXR mapping.** Stage 3. It explains why the eager flush had to be built at the transcript level: `vosk_api.h` exposes no relative cost, no lattice, no final-state signal, so C# cannot see the decoder's own completeness verdict.
- **Feasibility on Quest.** Free — it is running now.
- **Risk.** None to observe; the risk is in *changing* it (below).
- **Open questions.** Does the phrase-list grammar built by `vosk_recognizer_new_grm` even have meaningful final states for multi-word phrases, given the brief describes it as a bag-of-phrases LM where entries combine in any order? If every phrase boundary is a final state, rule2 fires at 0.5 s after *any* complete phrase — which would explain the observed mid-command splits precisely.

### 2. The runtime endpointer knobs VoXR never calls — `VERIFIED`

- Source: `vosk_api.h`, https://raw.githubusercontent.com/alphacep/vosk-api/master/src/vosk_api.h (fetched).
- **Mechanism.** `void vosk_recognizer_set_endpointer_mode(VoskRecognizer*, VoskEndpointerMode)` with `VOSK_EP_ANSWER_DEFAULT=0, SHORT=1, LONG=2, VERY_LONG=3` ("endpointer scaling factor"), and `void vosk_recognizer_set_endpointer_delays(VoskRecognizer*, float t_start_max, float t_end, float t_max)` where `t_end` is "timeout for stopping recognition … after we recognized something (usually 0.5 - 1.0)".
- **Evidence.** Grep of `Runtime/` and `NativeBridge~/src/vosk_bridge.cpp` finds no call to either; the only endpoint-related strings in the repo are documentation prose.
- **VoXR mapping.** Stage 3. Adding a bridge export `vosk_bridge_set_endpointer(t_start_max, t_end, t_max)` plus an Inspector field would make the 0.5–1.0 s wait a tunable on the same footing as `bufferWindow` — and would let a *push-to-talk* session run a much shorter `t_end` than a continuous one.
- **Feasibility.** Trivial: one C export, one P/Invoke, no model change, no APK growth.
- **Risk.** Shorter `t_end` increases splits. But VoXR already has the machinery to reassemble splits (utterance buffer) — the point is that it is currently paying the split-avoidance cost **twice**, once in the endpointer and once in the 2 s buffer.
- **Open questions.** The header comment says "milliseconds" while the suggested values are 0.5–1.0, i.e. seconds. Units must be established empirically before any claim is made.

### 3. Eager flush is blind to partial results — `VERIFIED` (code)

- Source: `Runtime/Commands/VoxrCommandRecogniser.cs:534` (`Update`), `:566` (`HandleResult`), `:1331` (`HandlePartialResult`, inside `#if UNITY_EDITOR`); `NativeBridge~/src/vosk_bridge.cpp:125-142`.
- **Mechanism.** The bridge pushes both finals (`vosk_recognizer_result`) and de-duplicated partials (`vosk_recognizer_partial_result`) to C#. `HandleResult` — the only path that appends to `UtteranceBuffer` and calls `ProbeEagerCommit` — is subscribed to `OnResult`. Partials only set `LastPartialResult`, and only in the Editor.
- **Evidence.** Direct reading of both files; `Documentation~/scoring.md` §6 says "each VOSK result triggers one speculative parse of the buffer", which is true of finals only.
- **VoXR mapping.** Stages 5–6. Running the same `TryEagerCommit` scan against `buffer + latest partial` would let a `Commit` verdict fire **before the endpointer**, cutting 500–1000 ms from every unambiguous complete command. The seven `Commit` conditions in `scoring.md` §6 were designed for exactly this hazard class (buffer-spanning, terminal, no sibling tie, first required element present) and would transfer unchanged; only condition 7 (confidence) needs `vosk_recognizer_set_partial_words(recognizer, 1)` to have data, otherwise it is bypassed as `-1`.
- **Feasibility.** Pure C#/bridge; no model, no APK cost. One extra parse per distinct partial (partials are already de-duplicated in `vosk_bridge.cpp`).
- **Risk.** **This is the real one.** A partial is a live hypothesis; Kaldi can and does revise it. Firing on a partial means firing on text that may not survive to the final — and VoXR's whole safety architecture (the leading-required-miss bar, coverage, sibling ties) was built against *final* transcripts. A stability requirement (same `Commit` verdict on N consecutive partials, or a minimum elapsed time since the last text change) is mandatory, not optional.
- **Open questions.** How stable are grammar-mode partials on Quest? Unmeasured. Does firing early and clearing the buffer strand the tail (the same hazard eager condition 6 exists to prevent)? Almost certainly, and worse, because more speech is still coming.

### 4. sherpa-onnx keyword spotting as a text-enrolled fast path — `VERIFIED`

- Source: https://k2-fsa.github.io/sherpa/onnx/kws/index.html and https://github.com/k2-fsa/sherpa-onnx (both fetched).
- **Mechanism.** "An open vocabulary keyword spotting system is just like a tiny ASR system, but it can only decode words/phrases in the given keywords." Keywords are a runtime text file of token sequences with optional per-keyword boosting score (`:1.5`) and trigger threshold (`#0.35`); `sherpa-onnx-cli text2token` converts plain English to that form using BPE or phonemes.
- **Evidence.** Pretrained models: `sherpa-onnx-kws-zipformer-gigaspeech-3.3M` (English, 2024), `sherpa-onnx-kws-zipformer-zh-en-3M` (2025). Repo confirms Apache-2.0, Android arm64, C# among 12 language bindings, and KWS as a first-class task. No FA/FR numbers are published in the docs.
- **VoXR mapping.** Stage 3/4. Two viable roles: (a) **gate** — a spotter running in parallel decides whether the utterance contains *any* registered command phrase, and suppresses the parser otherwise, which directly attacks "coughs and hums decode as in-grammar short words"; (b) **fast path** — for the subset of commands with no slots, a spotter fires on its own, and the VOSK+parser path handles the rest.
- **Feasibility on Quest.** 3.3 M params INT8 ≈ single-digit MB, well inside the APK budget next to the existing 40 MB model; a second ONNX Runtime dependency is the real cost, not the model. The survey (finding 12) puts comparable KWS models at 1.5–8 M MACs per inference — a rounding error against a Kaldi chain decode.
- **Risk.** Two published failure modes bite VoXR specifically: **prefix bias** (finding 6) on "switch to weapons"/"switch to navigation", and unpublished FA/hour for these particular checkpoints. Per-keyword thresholds are a tuning surface a game developer would have to face.
- **Open questions.** Does a spotter's confidence correlate better with correctness than VOSK's flat ~0.50 per-word `conf`? That is the calibration problem VoXR actually has, and nobody has measured it here.

### 5. CED — 155 K-parameter streaming text-enrolled KWS — `VERIFIED`

- Source: Jin, Jung, Lee, Roh, Han, Cho, "CTC-aligned Audio-Text Embedding for Streaming Open-vocabulary Keyword Spotting", Interspeech 2024, https://arxiv.org/abs/2406.07923.
- **Mechanism.** "the first attempt to dynamically align the audio and the keyword text on-the-fly"; CTC picks the best alignment at each input frame, frame-level acoustic embeddings are aggregated and matched against the keyword's text embedding. Decode cost O(U) in keyword length.
- **Evidence.** 155 K parameters; competitive with non-streaming methods on LibriPhrase. No FA/hour or absolute EER on the abstract page.
- **VoXR mapping.** Stage 3/4 — the same gate/fast-path role as finding 4, at ~1/20 the size.
- **Feasibility.** Trivially affordable if weights existed; none are published.
- **Risk.** Research artefact. LibriPhrase is isolated-phrase; VoXR's stream is continuous and noisy with game audio.
- **Open questions.** Streaming latency (frames of look-ahead) is not stated on the abstract page.

### 6. Prefix bias kills the naive multi-phrase spotter for VoXR's grammar — `VERIFIED`

- Source: Liu, Huang, Quan, "No Word Left Behind: Mitigating Prefix Bias in Open-Vocabulary Keyword Spotting", ICASSP 2026, https://arxiv.org/abs/2602.08930.
- **Mechanism.** OV-KWS models over-weight initial phonemes at enrollment, so negative pairs that share a prefix ("turn the volume up" vs "turn the volume down") are mis-accepted. Fix: Equal-weighting Position Scoring (EPS).
- **Evidence.** Their Partial Overlap Benchmark: POB-Spark EER **64.4 % → 29.3 %**; POB-LibriPhrase accuracy 87.6 % → 96.8 %, without regressing LibriPhrase or Google Speech Commands.
- **VoXR mapping.** This is a **negative** finding for stage 3/4 replacement. VoXR's authored command sets are dense in shared-prefix siblings — `switch to weapons` / `switch to navigation`, `decelerate {burn_level}` / `decelerate`, and the whole sibling-tie machinery in `scoring.md` exists because the *parser* already faces this. A spotter with 64 % EER on shared-prefix negatives would re-introduce, at the acoustic layer, the exact class of error VoXR spent three merged features fixing at the text layer.
- **Feasibility.** N/A — this constrains the design rather than adding one.
- **Risk.** Ignoring it. Any KWS evaluation for VoXR must be run on *sibling command pairs from a real command set*, not on Google Speech Commands.
- **Open questions.** Does EPS exist in any shipping implementation (sherpa-onnx does not mention it)?

### 7. TEN VAD and Silero VAD — the cheapest way to stop timing the buffer — `VERIFIED`

- Sources: https://github.com/TEN-framework/ten-vad; https://github.com/snakers4/silero-vad (both fetched).
- **Mechanism.** Frame-level speech/non-speech probability. TEN VAD runs 16 kHz with 160/256-sample hops (10/16 ms).
- **Evidence.** TEN VAD: **532 KB** Android arm64-v8a library, RTF **0.0570** on a Galaxy J6+ (0.0086 on a Xeon), Apache-2.0 *with additional conditions* plus an LPCNet-derived BSD file; repo claims better precision-recall than WebRTC VAD and Silero on manually annotated LibriSpeech/GigaSpeech/DNS test sets, and specifically that "Silero VAD suffers from a delay of several hundred milliseconds" on speech→non-speech transitions. Silero: ~2 MB JIT, MIT, "<1 ms per 30+ ms chunk on a single CPU thread", 8/16 kHz.
- **VoXR mapping.** Stages 1–2 (it can run on the already-downsampled 16 kHz int16 buffer in `recognition_loop`, before `accept_waveform`). Two uses: (a) **close the utterance buffer on measured speech offset** instead of on `bufferWindow` — cutting the 2.0 s tail to a VAD hangover of ~200–300 ms; (b) **suppress decoder output during non-speech**, so a cough that VOSK renders as "on" is discarded because no speech frame preceded it.
- **Feasibility on Quest.** A Galaxy J6+ (Snapdragon 425-class) at RTF 0.057 is a far weaker CPU than an XR2 Gen 1/2 core; this is affordable.
- **Risk.** A VAD detects *voicing*, not *words*. A cough is voiced. Use (b) will help against breath and room noise, and will **not** reliably help against coughs or hums — the brief's stated failure. Claiming otherwise would be unfounded. Also, TEN VAD's "Apache-2.0 with additional conditions" needs a licence read before it can go in an Apache-2.0 package.
- **Open questions.** No fetched source gives VAD accuracy on coughs/hums specifically; the searches surfaced only dataset papers (VocalSound) and vendor blogs. Treat speech-vs-cough discrimination as **unmeasured**.

### 8. TTS-only training closes most, not all, of the SLU data gap — `VERIFIED`

- Source: Lugosch, Meyer, Nowrouzezahrai, Ravanelli, "Using Speech Synthesis to Train End-to-End Spoken Language Understanding Models", https://arxiv.org/abs/1910.09463 (ar5iv full text fetched).
- **Mechanism.** Generate the SLU training corpus from text with many TTS voices; train the E2E SLU model on it; optionally mix with real speech.
- **Evidence.** Fluent Speech Commands: **94.9 % ± 0.2** trained on 22 synthetic voices only vs **99.1 % ± 0.1** on real speech from 77 speakers — the paper states "the model trained using only real speech is about 4 % more accurate". Snips: real-only best 65.5 % ± 2.9 → **71.4 % ± 1.4** with synthetic added. Corroborating, in KWS: Synth4Kws (arXiv:2407.16840) finds TTS-only performance improves *monotonically* with phrase diversity and utterance sampling over 11 k Speech Commands utterances, and TTS on top of a 50 k-real baseline gives **EER −30.1 %, AUC +46.7 %**; LLM-Synth4KWS (arXiv:2505.22995, Interspeech 2025) adds LLM-generated *confusable* phrases for **+3.7 % AUC / +11.3 % c-AUC**.
- **VoXR mapping.** This is the crux the brief names. VoXR's commands are authored as text by each developer with zero audio, which is precisely the Synth4Kws setting. The evidence says a text→TTS→train pipeline is viable in principle. It does **not** say it is viable *in a Unity package*: it implies shipping or invoking a training job per command set.
- **Feasibility on Quest.** Inference-side only; training happens offline on a workstation. VoXR already has the pieces — a TTS fixture corpus, a 699-utterance A/B rig, WAV replay — which is more infrastructure than most teams start with.
- **Risk.** (a) FSC is a **248-sentence closed set**; VoXR's grammar is combinatorial (slots, number sequences), so a 4-point gap there is not a 4-point gap here. (b) 2019-era TTS; modern TTS is better but no fetched source measures the current gap for slot-filling. (c) Per-game training changes the product from "type your commands and press play" to "type your commands and wait for a build step".
- **Open questions.** Nothing found measures TTS-only training for **numeric slot sequences** ("two seven zero"), which is where VoXR's flat confidence already hurts most.

### 9. Picovoice Rhino — the shipped form of "text context → on-device intent+slots" — `VERIFIED`

- Source: https://github.com/Picovoice/rhino (fetched) plus picovoice.ai product/doc pages.
- **Mechanism.** A YAML *context* of expressions, slots and macros is compiled to a `.rhn` file; the engine infers intent and slot values directly from audio, "no intermediate transcript, no hallucinations", and returns `isUnderstood: false` when the utterance is out of context.
- **Evidence.** Vendor-reported 97.3 % accuracy at 6–24 dB SNR and >99 % in clean conditions; supports Android, iOS, Cortex-M, Raspberry Pi, desktop, web; repo code Apache-2.0.
- **VoXR mapping.** Stages 3–6 combined — it is a complete alternative to VOSK+grammar+parser, with the same authoring model VoXR already has (developer writes text).
- **Feasibility.** Not adoptable: **Unity support is listed as deprecated as of v4.0.0 (December 2025)**, context compilation runs through Picovoice's cloud console (breaking "fully offline" at authoring time), and the models/licence are commercial — incompatible with the brief's Apache-2.0-compatible constraint.
- **Risk.** N/A (not adoptable). Its value is as a **specification**: the `isUnderstood: false` contract is exactly what VoXR's `OnUnrecognisedSpeech` and leading-miss bar are groping toward, and its YAML expression/slot/macro grammar is a mature version of VoXR's pattern arrays.
- **Open questions.** None material.

### 10. Unified ASR+endpointing and two-pass endpoint verification — `VERIFIED`

- Sources: Bijwadia, Chang, Li, Sainath, Zhang, He, "Unified End-to-End Speech Recognition and Endpointing", SLT 2022, https://arxiv.org/abs/2211.00786; Raju et al., "Two-pass Endpoint Detection for Speech Recognition", ASRU 2023, https://arxiv.org/abs/2401.08916.
- **Mechanism.** (a) Train ASR and endpointing as one multitask model with a switch between raw audio and encoder latents, so EOQ decisions use what the recogniser already computed. (b) Let a first-pass endpointer propose, and a second-pass "EP Arbitrator" verify before the endpoint is committed.
- **Evidence.** (a) median endpoint latency **−120 ms (−30.8 %)**, P90 **−170 ms (−23.0 %)** on voice search with no WER degradation, and −10.6 % relative WER on continuous recognition. (b) improvements across voice-assistant, conversational and public SLURP data, compatible with several first-pass endpointers; the fetched abstract gives no ms figures.
- **VoXR mapping.** Stage 5. Both are the published form of what VoXR should do at the transcript level: (a) says "endpoint from the recogniser's own state, not from silence" — VoXR's version is the eager-flush scan; (b) says "propose then verify" — VoXR's version would be *fire the eager commit only if the verdict is stable across N partials, or if a second cheap check agrees*.
- **Feasibility.** The architectures themselves need trained E2E models and are out of budget; the **patterns** are free.
- **Risk.** Cargo-culting the architecture instead of the pattern.
- **Open questions.** Raju's absolute numbers are behind a PDF that did not parse; the 30 % median-latency figure from Bijwadia is the one to quote.

### 11. Emission-latency training: FastEmit, delay penalty, minimum-latency — `VERIFIED`

- Sources: Yu et al., ICASSP 2021, https://arxiv.org/abs/2010.11148; Kang, Yao, Kuang, Guo, Yang, Lin, Żelasko, Povey, ICASSP 2023, https://arxiv.org/abs/2211.00490; Shinohara & Watanabe, Interspeech 2022, https://arxiv.org/abs/2211.02333.
- **Mechanism.** Transducers are free to delay a token to gather right context; all three add a training-time pressure to emit sooner — FastEmit as a sequence-level regulariser needing no alignments, delay-penalty as `λ(T/2 − t)` added to non-blank log-probs inside the recursion, minimum-latency training by differentiating an expected latency defined along lattice diagonals.
- **Evidence.** FastEmit: LibriSpeech WER 4.4/8.9 → 3.1/7.5 % with **P90 emission latency 210 ms → 30 ms**; 150–300 ms reduction on Voice Search. Minimum-latency: WSJ causal Conformer-T **220 ms → 27 ms** at 0.7 % WER cost, beating alignment-restricted training (110 ms) and FastEmit (67 ms) at comparable settings. Delay-penalty: reports trade-offs similar to FastEmit and is implemented in k2/icefall.
- **VoXR mapping.** Stage 3, and **only** under a decoder replacement. A Kaldi chain model's emission timing is fixed at training time by someone else.
- **Feasibility.** Zero today. Non-zero if sherpa-onnx/k2 Zipformer streaming ASR or KWS is ever adopted and retrained, since delay penalty is a training flag there.
- **Risk.** Being read as an option for the current stack. It is not.
- **Open questions.** How much of the observed Quest lag is emission delay vs decoder catch-up vs endpointer? Unmeasured — see the budget below.

### 12. What a KWS model actually costs, and where the field is — `VERIFIED`

- Source: Garai & Samui, "Advances in Small-Footprint Keyword Spotting: A Comprehensive Review of Efficient Models and Algorithms", Engineering Applications of AI, 2025, https://arxiv.org/html/2506.11169. Corroborating on-device numbers: MALEFA (https://arxiv.org/html/2604.03689, 650 K params / 93 MFLOPs, **FAR 0.007 % on AMI**), CLAD (https://arxiv.org/html/2401.06485, 2.2 M params, 8.65 % EER LibriPhrase-hard, 16.95× faster than two-stage), MFA-KWS (https://arxiv.org/abs/2505.19577, **47–63 % decoding speed-up**), U2-KWS (https://arxiv.org/abs/2312.09760, **+41 % relative wake-up at 0.5 FA/hour**), PhonMatchNet (https://arxiv.org/abs/2308.16511, 67 %/80 % relative EER/AUC gains), Apple EMKWS (Nishu, Cho, Naik, Interspeech 2023, https://machinelearning.apple.com/research/matching-latent-encoding, +14.4 % AUC / +28.9 % EER via Dynamic Sequence Partitioning), MM-KWS (https://arxiv.org/abs/2406.07310, multi-modal text+audio enrollment).
- **Evidence.** Representative fixed-keyword models on Google Speech Commands v2: BC-ResNet-1 96.9 % at **9.2 K params / ~1.5 M MACs**; MatchboxNet 97.3–97.6 % at 77–140 K params; Res15 95.8 % at 238 K. INT8 quantisation cuts size by up to 69 %. The survey names **FAR per hour**, miss rate and EER as the streaming metrics, and explicitly flags that "the DL model's performance in KWS can suffer significant degradation when applied to unseen keywords in the target domain at runtime".
- **VoXR mapping.** Stage 3/4 sizing. A spotter is ~0.01–3 M params against a 40 MB acoustic model + 8.9 MB `libvosk.so`. CPU is not the obstacle; *coverage of VoXR's slot grammar* is.
- **Feasibility.** High for the model; medium for the runtime (a second inference engine).
- **Risk.** The survey's own caveat — unseen-keyword degradation — plus finding 6's prefix bias.
- **Open questions.** No fetched source reports FA/hour for a **multi-phrase** (20–60 phrase) spotter of the size VoXR would need; published FA/hour figures (0.5/hour in U2-KWS, <0.5/hour target in openWakeWord) are for **one** wake phrase. FA rate for N phrases is not simply N× but is certainly worse, and nobody has published it.

## A latency budget for VoXR

End of speech → `OnCommandRecognised`. Values marked **(code)** are read from this repo; **(doc)** from `Documentation~/`; **(inferred)** from Kaldi defaults; **(unmeasured)** means nobody has measured it and it must not be quoted as fact.

| # | Stage | Cost | Source | Where research cuts it |
|---|---|---|---|---|
| 1 | Mic capture chunking (`AudioRecord`, 48 kHz mono, ~20 ms chunks → ring buffer) | ~20 ms | brief §2.1 (doc) | Nothing meaningful; already near the floor |
| 2 | Ring-buffer read by the recognition thread | 0–10 ms typical; the read is capped at **4096 samples = 85.3 ms** and the loop sleeps **10 ms** when the buffer is empty | `vosk_bridge.cpp:45,91-95` (code) | The 85 ms cap binds only while catching up; not the bottleneck |
| 3 | DSP (48→16 kHz decimate-by-3 FIR, AGC, float→int16) | <10 ms **(unmeasured)** — FIR group delay only | `vosk_bridge.cpp:117-123` (code) | Irrelevant at this scale |
| 4 | Decoder frame granularity | 30 ms per decoder step (10 ms frames × `--frame-subsampling-factor=3`) | `model.conf` (code) + Kaldi 10 ms default **(inferred)** | Chunked/dynamic-chunk attention (U2++, Emformer) is about *look-ahead*, not this |
| 5 | **Endpointer trailing-silence wait** | **500 ms** (rule2: silence ≥0.5 s **and** final-state relative cost ≤2.0) / **750 ms** (rule3, cost ≤8.0) / **1000 ms** (rule4, any cost) | `model.conf` + Kaldi `online-endpoint.h` (code + canonical) | `set_endpointer_delays` / `set_endpointer_mode` (finding 2); VAD-assisted offset detection (finding 7); firing on partials removes it entirely (finding 3). Published alternatives reach **160 ms** median (Udupa 2025) and **−120 ms median** by unifying with ASR (Bijwadia 2022) |
| 6 | Decoder catch-up on Quest | **(unmeasured)**; `Documentation~/command-recognition.md` states "Quest 3 VOSK latency adds ~0.5–1.0 s to inter-result gaps" (doc) | doc | This is the **largest unknown in the budget**. Shangguan et al. 2021 split a 1000 ms UPL into 600 ms endpointer + 400 ms decoder catch-up; VoXR has never made that split |
| 7 | Native result queue → C# poll | 11–14 ms (one `Update` at 90–72 fps) | `VoxrCommandRecogniser.cs:534` (code) | Nothing; sub-frame delivery would need a different threading contract |
| 8 | **Utterance buffer window** | **2000 ms** recommended on Quest (0.5 s default); **600 ms** if `prefixHoldSeconds` is armed on a `HoldExtendable` verdict; **0 ms** on an eager `Commit` | `Documentation~/command-recognition.md` "Utterance Buffer" (doc), `VoxrCommandRecogniser.cs:72,89,606` (code) | VAD-driven close (finding 7); eager-on-partials makes it moot for the commit case |
| 9 | Parse + selection + extraction | **(unmeasured)**; bounded by one frame in practice since it runs inside `Update`/`HandleResult` | code | Not a target until measured |
| 10 | Debounce / pending | `commandCooldown`; pending path always waits the full window and skips eager entirely | doc | Out of scope for this report |

**Totals (end of speech → event), excluding the unmeasured Quest decoder catch-up:**

- Eager `Commit` path: **≈ 545–1055 ms** (1+2+4+5+7).
- `HoldExtendable` + `prefixHoldSeconds=0.6`: **≈ 1145–1655 ms**.
- Default time-driven path with `bufferWindow=2.0`: **≈ 2545–3055 ms**.
- Add the doc's 0.5–1.0 s Quest lag and the time-driven path is a **3–4 second** command.

**Target for comparison:** conversational response offsets peak within **200 ms** of turn end (Stivers et al. 2009, `UNVERIFIED`); voice-agent practice targets ≤300 ms. Even VoXR's best path is 2–4× over, and stage 5 alone (500–1000 ms) exceeds the entire target.

**The one-line conclusion of the budget:** two stages (5 and 8) account for 90 %+ of controllable latency, both are silence timers, and both are currently set conservatively to defend against the *same* failure — mid-command splits. Paying for it twice is the design defect.

## Endpointing design options

### A. Silence-based, tuned (status quo, tightened)

Keep Kaldi's endpointer; call `vosk_recognizer_set_endpointer_delays` with a shorter `t_end` (target 0.25–0.35 s), and shorten `bufferWindow` correspondingly since the buffer's job is to reassemble what a short endpointer splits.

- **Expected latency:** stage 5 drops 500→250–350 ms; total eager path ≈ 300–400 ms + Quest lag.
- **Split risk:** *up*, materially. The 2 s buffer already exists to catch splits, so the split *rate* rising is survivable; the split *cost* (an extra 2 s wait when reassembly is needed) is what gets worse.
- **Effort:** one bridge export + one Inspector field. No model, no APK cost.
- **Prior art:** the knob is documented in `vosk_api.h`; the trade-off is exactly Kaldi's rule2/3/4 ladder.

### B. Semantic / grammar-complete (eager flush on partials)

Run `TryEagerCommit` against `buffer + latest partial`, with the existing seven conditions plus a **stability gate** (identical `Commit` verdict on ≥2 consecutive distinct partials, or ≥T ms since the transcript last changed), and `vosk_recognizer_set_partial_words(1)` so confidence is real rather than `-1`.

- **Expected latency:** removes stages 5 and 8 for the commit case → **≈ 60–120 ms** after the last word of a complete unambiguous command, plus decoder catch-up. This is the only option in this list that can reach the 200–300 ms target.
- **Split risk:** *lower*, not higher — a command that fires before the pause never gets split by the pause. The new risk is a different one: firing on a hypothesis the final revises, and stranding the tail (the hazard eager condition 6 was written for).
- **Effort:** medium. C# + bridge plumbing, plus new test coverage for partial-revision cases the existing suites do not have.
- **Prior art:** Kaldi's own `max_relative_cost` final-state test (finding 1); unified ASR+endpointing (Bijwadia 2022, −120 ms median); two-pass EP verification (Raju 2023) for the stability gate; predictive EOU (Zink 2024) for the more ambitious version that fires *before* the last word.

### C. VAD-assisted buffer close

Run TEN VAD (532 KB, RTF 0.057 on a Galaxy J6+) or Silero VAD (2 MB, MIT, <1 ms/30 ms chunk) on the 16 kHz stream inside `recognition_loop`; publish a speech/non-speech edge to C#; close the utterance buffer on a measured offset + short hangover instead of on `bufferWindow`.

- **Expected latency:** stage 8 drops 2000 → ~200–300 ms; stage 5 unchanged unless combined with A. Total ≈ 750–1300 ms.
- **Split risk:** *lower than a timer at equal latency* — silence is measured rather than assumed, so an intra-command pause shorter than the hangover no longer costs the full window, and a genuine end-of-speech no longer costs 2 s.
- **Effort:** medium — a new native dependency, licence review (TEN VAD is "Apache-2.0 with additional conditions"), and an ABI addition.
- **Secondary benefit:** a speech/non-speech gate on decoder output attacks the "cough decodes as `on`" false trigger — **partially**; a cough is voiced and no fetched source measures VAD discrimination of coughs. Do not claim this benefit without measuring it.

### D. Learned end-of-utterance model

Ship a trained EOU/turn-detector.

- **Expected latency:** published systems reach 160 ms median (Udupa 2025) and ~100 ms mean error predicting 500 ms ahead (Zink 2024).
- **Split risk:** lowest in principle — the model learns that "orient heading two seven…" is unfinished.
- **Verdict: not viable for VoXR now.** Every available implementation is the wrong size or the wrong task: Smart Turn v2 is **360 MB**; LiveKit Turn Detector v1 is a distilled Qwen2.5-0.5B (0.1 B INT8) over *transcript text* under a bespoke non-OSI licence; TEN Turn Detection wraps Qwen2.5-**7B**. All target conversational turn-taking, where "has the human finished a thought" is genuinely uncertain. VoXR's grammar makes that question *decidable* — which is why option B, not D, is the right shape here.
- **The one transferable fact from D:** LiveKit's finding that the effective EOU signal is on the **text** side, not the waveform, is direct support for option B over option C as the primary lever.

**Recommended combination:** B as the primary, A as the enabling change (a shorter `t_end` also produces partials/finals sooner), C as the fallback for the non-commit cases (incomplete, ambiguous, pending) that B leaves on the timer.

## What this area cannot fix

- **In-grammar substitutions.** "switch to weapons" → `switch two weapons` is an acoustic-model/posterior problem. No endpointer, VAD, or latency technique touches it. A spotter can *re-score* competing phrases and might help, but the prefix-bias result (finding 6) says an off-the-shelf one will be worse on exactly VoXR's sibling pairs.
- **Dropped short words leaving no timing hole.** Field-verified: neighbouring alignments absorb the missing word. Nothing in KWS/VAD/endpointing recovers information the decoder never emitted. Only an N-best/lattice or a re-scoring pass over the audio could, and VOSK's C ABI exposes neither (only `set_max_alternatives`, untried here).
- **Flat, uncalibrated confidence.** "two" at ~0.50 regardless of clarity is a property of the chain model's posteriors. Emission-latency work does not calibrate; VAD does not calibrate. Calibration would need either a different acoustic model or a learned per-command verifier trained on this decoder's outputs.
- **Zero-training slot filling.** Everything in the text-enrolled KWS literature spots *fixed phrases*. No fetched paper offers text-enrolled **slot extraction** with variable-length numeric sequences. That is what VoXR's parser exists for, and this literature offers no replacement.
- **The grammar-rebuild audio gap.** A latency-of-endpointing result cannot help; the candidate fix is `vosk_recognizer_set_grm` on a live recognizer (finding, table row 3), which is a VOSK API question, not a research question.
- **The single-recogniser-per-process constraint.** File-scope C++ state in `vosk_bridge.cpp`; purely an engineering matter.
- **"That's a navigation command, you're in weapons mode."** Requires decoding *outside* the active set, i.e. two grammars or a full-vocabulary second pass. Two-pass architectures (U2-KWS, Raju's arbitrator) are the right shape, but every one of them costs a second model.
- **What the evidence base cannot support.** 73 field utterances from one speaker cannot distinguish a 5 % from a 15 % false-accept rate, and cannot validate any FA/hour claim at all. Every number in this report about false alarms comes from a different corpus, mostly isolated-phrase, none from VR with game audio and exertion.

## Ranked recommendations for VoXR

**1. Make the Kaldi endpointer a tunable, then measure what it actually costs.** `vosk_recognizer_set_endpointer_delays` and `set_endpointer_mode` are in `vosk_api.h`, take effect at runtime, and are not called anywhere in this repo. Stage 5 is 500–1000 ms of the budget and is currently a constant nobody chose.
*Cheapest validating experiment:* add one bridge export, then sweep `t_end` ∈ {0.5, 0.35, 0.25} across the existing 699-utterance A/B corpus via the WSL rig, recording (a) split rate — finals per utterance — and (b) end-of-audio→final delay **in sample time, not wall clock** (the harness pushes faster than real time; `NativeBridge~/harness/README.md` says so explicitly). One rig run, no Unity, no device. First establish the units of `t_end` empirically — the header comment says "milliseconds" but suggests values of 0.5–1.0.

**2. Run the eager-flush scan on partial results, behind a stability gate.** This is the only change that can reach a 200–300 ms command, because it removes stages 5 *and* 8 together. `TryEagerCommit`'s seven conditions already encode the safety rules; what is missing is (a) subscribing the buffer path to partials, (b) `vosk_recognizer_set_partial_words(1)` so confidence is real, and (c) a stability requirement so a revised hypothesis cannot fire.
*Cheapest validating experiment:* do not implement it first — **instrument** it. Log, per fixture in the A/B corpus, the sequence of partials and the `TryEagerCommit` verdict each would have produced, and compare to the verdict on the final. The measurable question is: *on what fraction of utterances would a partial have produced a `Commit` verdict that the final contradicts?* If that fraction is non-trivial, option B is dead and the answer is option A+C. This is a read-only rig change.

**3. Add a VAD and close the utterance buffer on measured speech offset.** TEN VAD at 532 KB / RTF 0.057 on a much weaker Android CPU than the XR2 is affordable; Silero at 2 MB / MIT is the licence-clean fallback. This converts the 2.0 s `bufferWindow` from a guess into an observation, and gives the pending/ambiguous paths — which recommendation 2 deliberately does not accelerate — a real endpoint.
*Cheapest validating experiment:* run Silero VAD offline (Python, no Unity, no native integration) over the existing fixture WAVs and the field session recordings if any exist; measure the distribution of (VAD speech offset → VOSK final). If that gap is consistently ≥400 ms, the VAD is strictly better information than the timer and integration is justified; if it is noisy, it is not. Zero code in the package.

**4. Establish where the Quest time actually goes before optimising further.** The budget above has a hole: "Quest decoder catch-up, unmeasured, docs say 0.5–1.0 s". Shangguan et al. (Interspeech 2021) split user-perceived latency into endpointer lag and decoder catch-up (600/400 ms in their worked example) precisely because the two need different fixes. VoXR has never made that split, and recommendations 1–3 target different halves of it.
*Cheapest validating experiment:* timestamp, in `vosk_bridge.cpp`, (a) the sample index at which the last non-silence audio was pushed, (b) the wall-clock at which `accept_waveform` returned 1, and (c) the wall-clock at which C# `Update` delivered it. Three counters, one debug-log line, one human on-device session. This is human-only per the project bindings and should be scheduled as such.

**5. Prototype a text-enrolled spotter as a *gate*, not a replacement — and evaluate it on sibling command pairs.** sherpa-onnx KWS (Apache-2.0, Android arm64, C# bindings, `sherpa-onnx-kws-zipformer-gigaspeech-3.3M`, keywords as runtime text with per-phrase boost and threshold) is the only shipping, licence-compatible, zero-training option in this whole survey. Its plausible first job is not to recognise commands but to **suppress non-commands** — the cough/hum/breath false triggers that grammar mode turns into "on", "from", "four".
*Cheapest validating experiment:* offline, outside Unity — run the spotter over the fixture corpus with the real command phrases as keywords, and over any recorded non-speech, and report two numbers: recall on true commands, and false accepts per hour on non-speech. Then, critically, run it on **shared-prefix sibling pairs from a real command set** ("switch to weapons" vs "switch to navigation"), because ICASSP 2026's prefix-bias result reports EER as bad as 64.4 % on exactly that structure. If sibling EER is poor, the gate role survives (it only needs "was this any command?") and the fast-path role does not.

**Explicitly not recommended:** an end-to-end SLU replacement (needs per-game training data; the TTS evidence is a 4-point gap on a *closed 248-sentence set*, and nothing measures TTS-only training for numeric slots), any learned turn-detector (360 MB / 0.1 B / 7 B — all wrong size or wrong task), and any emission-latency training technique (inapplicable to a pretrained Kaldi chain model).

## Sources

Fetched and verified:

1. Lee & Cho, *PhonMatchNet: Phoneme-Guided Zero-Shot Keyword Spotting for User-Defined Keywords*, Interspeech 2023 — https://arxiv.org/abs/2308.16511
2. Liu, Huang & Quan, *No Word Left Behind: Mitigating Prefix Bias in Open-Vocabulary Keyword Spotting*, ICASSP 2026 — https://arxiv.org/abs/2602.08930
3. Jin, Jung, Lee, Roh, Han & Cho, *CTC-aligned Audio-Text Embedding for Streaming Open-vocabulary Keyword Spotting*, Interspeech 2024 — https://arxiv.org/abs/2406.07923
4. Xi et al., *Contrastive Learning with Audio Discrimination for Customizable Keyword Spotting in Continuous Speech* (CLAD), 2024 — https://arxiv.org/html/2401.06485
5. Li, Lo, Hung, Huang & Chen, *MALEFA: Multi-grAnularity Learning and Effective False Alarm Suppression for Zero-Shot Keyword Spotting*, 2026 — https://arxiv.org/html/2604.03689
6. Xi, Li, Gu, Jiang & Yu, *MFA-KWS: Effective Keyword Spotting with Multi-head Frame-asynchronous Decoding*, TASLP 2025 — https://arxiv.org/abs/2505.19577
7. Xi, Li, Yang, Li, Xu & Yu, *TDT-KWS: Fast And Accurate Keyword Spotting Using Token-and-duration Transducer*, ICASSP 2024 — https://arxiv.org/abs/2403.13332
8. Zhang, Zhou, Huang, Zou, Liu & Xie, *U2-KWS: Unified Two-pass Open-vocabulary Keyword Spotting with Keyword Bias*, ASRU 2023 — https://arxiv.org/abs/2312.09760
9. Ai, Chen & Xu, *MM-KWS: Multi-modal Prompts for Multilingual User-defined Keyword Spotting*, Interspeech 2024 — https://arxiv.org/abs/2406.07310
10. Nishu, Cho & Naik (Apple), *Matching Latent Encoding for Audio-Text based Keyword Spotting*, Interspeech 2023 — https://machinelearning.apple.com/research/matching-latent-encoding
11. Garai & Samui, *Advances in Small-Footprint Keyword Spotting: A Comprehensive Review*, Eng. Appl. AI 2025 — https://arxiv.org/html/2506.11169
12. Zhu, Agarwal, Bartel, Partridge, Park & Wang, *Synth4Kws: Synthesized Speech for User Defined Keyword Spotting in Low Resource Environments*, SynData4GenAI @ Interspeech 2024 — https://arxiv.org/abs/2407.16840
13. Zhu, Wang, Agarwal & Partridge, *LLM-Synth4KWS: Scalable Automatic Generation and Synthesis of Confusable Data for Custom Keyword Spotting*, Interspeech 2025 — https://arxiv.org/abs/2505.22995
14. Lugosch, Meyer, Nowrouzezahrai & Ravanelli, *Using Speech Synthesis to Train End-to-End Spoken Language Understanding Models*, 2019 — https://arxiv.org/abs/1910.09463
15. Arora, Dalmia, Chang, Yan, Black & Watanabe, *Two-pass Low Latency End-to-End Spoken Language Understanding*, Interspeech 2022 — https://arxiv.org/abs/2207.06670
16. Peng, Liu, Gao, Gao, Li & Chen, *Spoken Function Calling: A New Perspective on Spoken Language Understanding for Large Audio Language Models*, 2026 — https://arxiv.org/html/2608.05126
17. Yu, Chiu, Li, Chang, Sainath, He, Narayanan, Han, Gulati, Wu & Pang, *FastEmit: Low-latency Streaming ASR with Sequence-level Emission Regularization*, ICASSP 2021 — https://arxiv.org/abs/2010.11148
18. Kang, Yao, Kuang, Guo, Yang, Lin, Żelasko & Povey, *Delay-penalized transducer for low-latency streaming ASR*, ICASSP 2023 — https://arxiv.org/abs/2211.00490
19. Shinohara & Watanabe, *Minimum Latency Training of Sequence Transducers for Streaming End-to-End Speech Recognition*, Interspeech 2022 — https://arxiv.org/abs/2211.02333
20. Bijwadia, Chang, Li, Sainath, Zhang & He, *Unified End-to-End Speech Recognition and Endpointing for Fast and Efficient Speech Systems*, SLT 2022 — https://arxiv.org/abs/2211.00786
21. Raju, Khare, He, Sklyar, Chen, Alptekin, Trinh, Zhang, Vaz, Ravichandran, Maas & Rastrow, *Two-pass Endpoint Detection for Speech Recognition*, ASRU 2023 — https://arxiv.org/abs/2401.08916
22. Udupa, Watanabe, Schwarz & Černocký, *Streaming Endpointer for Spoken Dialogue using Neural Audio Codecs and Label-Delayed Training*, 2025 — https://arxiv.org/abs/2506.07081
23. Udupa, Watanabe, Schwarz & Černocký, *Endpoint Anticipation for Low-Latency Spoken Dialogue*, 2026 — https://arxiv.org/pdf/2606.13450 (title/authors verified; numeric results not extractable from the PDF)
24. Zink, Higuchi, Mullov, Waibel & Kobayashi, *Predictive Speech Recognition and End-of-Utterance Detection Towards Spoken Dialog Systems*, 2024 — https://arxiv.org/html/2409.19990
25. Shangguan, Prabhavalkar, Su, Mahadeokar, Shi, Zhou, Wu, Le, Kalinli, Fuegen & Seltzer, *Dissecting User-Perceived Latency of On-Device E2E Speech Recognition*, Interspeech 2021 — https://arxiv.org/html/2104.02207
26. Kaldi, `src/online2/online-endpoint.h` (OnlineEndpointRule / OnlineEndpointConfig) — https://kaldi-asr.org/doc/online-endpoint_8h_source.html
27. alphacep/vosk-api, `src/vosk_api.h` — https://raw.githubusercontent.com/alphacep/vosk-api/master/src/vosk_api.h
28. k2-fsa/sherpa-onnx — https://github.com/k2-fsa/sherpa-onnx and Keyword-spotting docs — https://k2-fsa.github.io/sherpa/onnx/kws/index.html
29. TEN-framework/ten-vad — https://github.com/TEN-framework/ten-vad
30. snakers4/silero-vad — https://github.com/snakers4/silero-vad
31. dscripka/openWakeWord — https://github.com/dscripka/openWakeWord
32. livekit/turn-detector (model card) — https://huggingface.co/livekit/turn-detector
33. Picovoice/rhino — https://github.com/Picovoice/rhino

`UNVERIFIED` (search-result evidence only; abstract or repo page not fetched):

34. Stivers et al., *Universals and cultural variation in turn-taking in conversation*, PNAS 106(26):10587, 2009 — https://www.pnas.org/doi/10.1073/pnas.0903616106
35. Shi, Wang, Wu, Yeh, Chan, Zhang, Le & Seltzer, *Emformer*, ICASSP 2021 — https://arxiv.org/abs/2010.10759
36. Wu et al., *U2++: Unified Two-pass Bidirectional End-to-end Model for Speech Recognition*, 2021 — https://arxiv.org/pdf/2106.05642
37. Jia, Majumdar & Ginsburg, *MarbleNet*, ICASSP 2021 — https://arxiv.org/pdf/2010.13886
38. Google WebRTC GMM VAD (via `wiseman/py-webrtcvad`) — https://github.com/wiseman/py-webrtcvad
39. pipecat-ai / Daily, *Smart Turn v2* — https://huggingface.co/pipecat-ai/smart-turn-v2
40. TEN Turn Detection — https://www.agora.io/en/blog/making-voice-ai-agents-more-human-with-ten-vad-and-turn-detection/
41. Bastianelli, Vanzo, Swietojanski & Rieser, *SLURP: A Spoken Language Understanding Resource Package*, EMNLP 2020 — https://aclanthology.org/2020.emnlp-main.588/
42. Lugosch, Ravanelli, Ignoto, Tomar & Bengio, *Speech Model Pre-training for End-to-End Spoken Language Understanding* (Fluent Speech Commands), Interspeech 2019 — https://arxiv.org/abs/1904.03670
43. Arora et al., *ESPnet-SLU: Advancing Spoken Language Understanding through ESPnet*, ICASSP 2022 — https://arxiv.org/pdf/2111.14706
