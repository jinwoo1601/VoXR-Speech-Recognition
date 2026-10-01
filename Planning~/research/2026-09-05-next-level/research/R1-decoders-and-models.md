# R1 — Decoders and acoustic models: can anything replace or augment Kaldi/VOSK in VoXR?

Research pass, 2026-09-05. Scope: on-device streaming ASR decoders and acoustic models, 2022–2026, evaluated against the VoXR pipeline described in `00-brief.md` §2 and the constraints in §4 (offline, Quest 2/3 arm64 CPU, ~40 MB model budget, Apache-2.0-compatible).

Stage numbers below refer to brief §2: **1** capture, **2** DSP, **3** decoder, **4** grammar mode, **5** utterance buffer, **6** parser, **7** UX, **8** verification.

---

## Executive summary

- **The single largest measured gap is not a new architecture — it is that VoXR uses roughly half of the VOSK C API it already ships.** I verified locally (`nm -D` on `Runtime/Plugins/Android/arm64-v8a/libvosk.so`, 8,862,928 bytes) that the shipped arm64 library **already exports `vosk_recognizer_set_grm` and `vosk_recognizer_set_max_alternatives`**. `set_grm` is documented in the upstream header as "Reconfigures recognizer to use grammar" on an "Already running VoskRecognizer" — i.e. the ~50 ms **audio gap is removable today without changing a model, a runtime, or a byte of the APK**. `set_max_alternatives` gives N-best with per-hypothesis confidence, which the parser (stage 6) could score over instead of 1-best. Neither is used by `vosk_bridge.cpp`. This is the cheapest, lowest-risk item in this entire report.
- The endpointer knobs (`vosk_recognizer_set_endpointer_mode` / `_delays`) are declared in the vendored header but **are not exported by the shipped `.so`** — they need a newer libvosk build, not just a code change. That matters because VOSK's ~0.8 s VAD split is the root cause of the whole `UtteranceBuffer` (stage 5) design.
- **On accuracy per megabyte, Kaldi-small is now clearly behind.** `vosk-model-small-en-us-0.15` reports **9.85 % WER on LibriSpeech test-clean** (alphacephei model page). A ~20 M-param *streaming* Zipformer transducer from the same lineage of authors reports **3.94 % / 9.79 %** (test-clean/test-other, greedy, 320 ms chunk) and a 66 M streaming Zipformer **3.06 % / 7.81 %** (icefall `RESULTS.md`). That is a ~2.5× relative error reduction at comparable on-disk size in int8. The headroom is real and large.
- **Grammar constraint survives the move to neural, and there are three working mechanisms**, in descending order of hardness: (a) **HLG/FST composed decoding over streaming CTC** — sherpa-onnx already ships this (`ctc_fst_decoder_config.graph`, "Path to H.fst, HL.fst, or HLG.fst"), which is the closest structural analogue to today's `vosk_recognizer_new_grm`; (b) **context-graph biasing** (sherpa-onnx hotwords, Aho-Corasick, `modified_beam_search` only; NVIDIA CTC-WS / TurboBias 2.0), which is a *soft* bias, not a closed vocabulary; (c) **KWS-mode transducers**, where the whole model only ever emits your phrases.
- **The one option that both closes the vocabulary and deletes the audio gap is KWS mode.** sherpa-onnx's `sherpa-onnx-kws-zipformer-gigaspeech-3.3M-2024-01-01` is ~**5 MB int8 total** (encoder 4.6 M + decoder 272 K + joiner 160 K) and keywords are supplied per-stream via `SherpaOnnxCreateKeywordStreamWithKeywords()` — **no model reload to change the command set**. Cost: it returns keyword hits, not a transcript, so stage 6's slot-filling and NumberSequence handling would have to be re-expressed as keyword phrases or run as a second stream.
- **Whisper is the wrong family for this problem** and should be closed out. It is not natively streaming (whisper.cpp's `stream` example is a 5 s sliding window stepped every 500 ms); `tiny` alone needs **~273 MB RAM** per the repo's own table; and whisper.cpp's GBNF grammar support has a filed, reproduced report that grammar constraints **produce identical output to unconstrained transcription** (issue #2159, May 2024). Moonshine v2 is the interesting Whisper-shaped exception (below), but its streaming variants start at 123 M params.
- **Licensing eliminates the strongest 2026 models.** NVIDIA `nemotron-speech-streaming-en-0.6b` posts 2.32 %/4.84 % LibriSpeech and a `NeMo-Speech.cpp` C++ runtime, but ships under the **NVIDIA Open Model License**, not Apache-2.0. Kyutai DSM STT is 1 B–2.6 B params. SeamlessStreaming is billion-scale. k2/icefall/sherpa-onnx (Apache-2.0) and Moonshine (MIT) are the two families that clear VoXR's §4 licensing bar cleanly.
- **The runtime cost of leaving VOSK is mostly the runtime, not the model.** An Android arm64 sherpa-onnx build is `libonnxruntime.so` **15 MB** + `libsherpa-onnx-jni.so` **3.7 MB** versus today's `libvosk.so` **8.9 MB**. A 20 M int8 streaming Zipformer is ~42 MB of ONNX. Net APK delta versus today (40 MB model + 8.9 MB lib) is roughly **+12 MB**, not a doubling. There are two existing Apache-2.0 Unity packages wrapping sherpa-onnx, so the C# ↔ native seam is not novel work.
- **No published RTF number in this survey was measured on Quest-class silicon.** The nearest real datapoint is a developer report of an int8 streaming RNN-T (Parakeet-EOU 120 M) on ONNX Runtime CPU EP/XNNPACK on a Galaxy S23 Ultra producing a final transcript **217 ms** after speech end. XR2 Gen 2 has **2 performance + 4 efficiency cores** and only "33 % improved" CPU over XR2 Gen 1 — so it is materially weaker than an S23 Ultra. **Every RTF claim below must be re-measured on device before it is planned around.**
- **Two things this area cannot fix**: the parser's disambiguation/pending/cooldown logic (stage 6) is a dialogue-state problem no acoustic model addresses; and Meta does not expose the Hexagon NPU to third-party Quest apps, so QNN/HTP acceleration is not available regardless of what the ONNX Runtime QNN EP supports.

---

## Findings table

| Name | Key citation | Params / on-disk | Streaming? | Reported accuracy | On-device evidence | License | Stage changed (§2) | Expected gain for VoXR | Main risk |
|---|---|---|---|---|---|---|---|---|---|
| **VOSK unused C API (`set_grm`, `set_max_alternatives`)** | alphacep/vosk-api `src/vosk_api.h` (master); locally verified `nm -D` on shipped `.so` | 0 (already shipped) | n/a | n/a | Symbols present in the exact `libvosk.so` in `Runtime/Plugins/Android/arm64-v8a/` | Apache-2.0 | **3, 4, 5, 6** | Audio gap → ~0; N-best hypotheses for the parser | `set_grm` reset semantics mid-stream unverified on device |
| VOSK `vosk-model-small-en-us-0.15` (baseline) | alphacephei.com/vosk/models | 40 MB | yes | 9.85 % LS test-clean; 10.38 % TED-LIUM | ships today on Quest | Apache-2.0 | — | — | — |
| VOSK `vosk-model-en-us-0.22-lgraph` | alphacephei.com/vosk/models | 128 MB | yes | 7.82 % LS; 8.20 % TED | none published | Apache-2.0 | **3** | ~21 % rel. WER cut, dynamic graph retained | 3.2× model size; APK budget |
| alphacep Zipformer2 "vosk-model-small-streaming-*" | huggingface.co/alphacep/vosk-model-small-streaming-bn | ~90 MB (bn) | yes | bn: 17.9 % CV, 16.6 % Respin-2025 | none published | Apache-2.0 | **3** | VOSK's own successor path | **No English model published as of this survey** |
| **Streaming Zipformer transducer (icefall/k2)** | Yao et al., *Zipformer*, ICLR 2024, arXiv:2310.11230; icefall `egs/librispeech/ASR/RESULTS.md` | 20 M small → ~42 MB int8; 66.11 M → ~70 MB int8 | yes (320/640 ms chunk) | 20 M: **3.94/9.79** greedy @320 ms; 66.11 M: **3.06/7.81** greedy, 2.81/7.15 @640 ms | sherpa-onnx docs RTF 0.06–0.08 (English, CPU unnamed) | Apache-2.0 | **3, 4, 5** | ~2.5× rel. WER cut at similar size | Grammar must be rebuilt as HLG or biasing; no Quest RTF |
| **Streaming Zipformer-CTC + HLG graph** | sherpa-onnx `python-api-examples/online-zipformer-ctc-hlg-decode-file.py`; c-api.h `ctc_fst_decoder_config.graph` | small zh int8 **25 MB** (no small English CTC model published) | yes | not published on model page | RTF 0.038 (small int8), 0.14 (large), 0.4 (xlarge); CPU unnamed | Apache-2.0 | **3, 4** | Direct structural replacement for `new_grm` | Needs offline HLG compilation toolchain (k2/OpenFST) in the authoring pipeline |
| **sherpa-onnx KWS (Zipformer 3.3 M)** | k2-fsa.github.io/sherpa/onnx/kws/ + pretrained_models | GigaSpeech-3.3M: **~5 MB int8** | yes (chunk-8 = 160 ms) | not published | none published | Apache-2.0 | **3, 4, 5** | **Zero-gap command-set switching**; 8× smaller than today's model | Emits keyword hits, not transcripts — stage 6 slots need redesign |
| sherpa-onnx hotwords / context graph | k2-fsa.github.io/sherpa/onnx/hotwords/ | n/a (decode-time) | yes | not published | none published | Apache-2.0 | **3, 4** | Bias toward command phrases without closing vocabulary | Requires `modified_beam_search`; open issues report empty/hallucinated text on silence (#845, #3267) |
| NVIDIA CTC-WS context biasing | Andrusenko et al., arXiv:2406.07096, Interspeech 2024 | n/a | offline in paper | F-score **0.87**; WER 14.02 → **10.48** (CTC); 12× faster than pyctcdecode beam biasing (15 s vs 179 s / 7 h audio) | none on ARM | Apache-2.0 (NeMo) | **3, 4** | Cheap phrase boosting with measured precision/recall | Evaluated at 100–1000 words, offline, not on ARM |
| TurboBias 2.0 | Bataev et al., arXiv:2608.21343 (2026) | n/a | yes | F-score 62.2 → **81.7** with boosting at 1.12 s latency; WER 13.1 → 12.6 | GPU-oriented (RTF at 128 streams) | NeMo (Apache-2.0) | **3, 4** | Per-stream context switching, no reload | GPU-batched design; CPU-single-stream value unproven |
| Nemotron Speech Streaming 0.6B | huggingface.co/nvidia/nemotron-speech-streaming-en-0.6b; Banfic et al., arXiv:2604.14493 | 600 M; **2.47 GB → 0.67 GB int4** | yes (80 ms–1.12 s) | **2.32/4.84** LS; 6.93 % avg of 8 | 7.28 % WER @2.46× RTFx on 32 EPYC cores; 0.56 s delay | **NVIDIA Open Model License** | 3 | best-in-class accuracy | **License + 670 MB**: disqualified for VoXR |
| Parakeet TDT / FastConformer | Rekesh et al., ASRU 2023, arXiv:2305.05084; nvidia/parakeet-tdt-0.6b-v2 | 600 M | yes (cache-aware) | 12.83 % streaming WER @1.38× RTFx (2604.14493 Table 5) | none on ARM | NVIDIA / CC-BY-4.0 varies | 3 | — | size + license |
| Emformer | Shi et al., ICASSP 2021, arXiv:2010.10759 | not stated | yes | **3.01/7.09** @80 ms avg latency; 2.50/5.62 @960 ms | 18 % rel. RTF reduction vs AM-TRF | (paper) | 3 | canonical low-latency streaming reference | superseded by Zipformer on the same accuracy/compute frontier |
| Moonshine v1 tiny/base | Jeffries et al., arXiv:2410.15608 (2024) | 27 M / 61 M | **no** (segment) | "no increase in WER vs Whisper tiny.en" at 5× less compute | claim only | MIT | 3 | small + command-oriented | not streaming; no grammar mechanism |
| **Moonshine v2** | Kudlur et al., arXiv:2602.12241 (2026) | Tiny **33.57 M**, Small 123.36 M, Medium 244.93 M | **yes** (sliding-window "ergodic" encoder) | Tiny: 4.49/12.09 LS, 12.01 % 8-set avg; Small: 2.49/6.78, 7.84 % avg | Tiny **50 ms** response latency, 8.03 % compute load — **MacBook M3**, not ARM mobile | MIT | 3, 5 | ~34 M streaming model at MIT with real latency numbers | encoder-decoder → no FST/HLG path; grammar would be constrained beam search only |
| Whisper + whisper.cpp | ggml-org/whisper.cpp | tiny 75 MiB / **~273 MB RAM**; base 142 MiB / ~388 MB | **no** (5 s sliding window, 500 ms step) | — | no ARM benchmarks in repo | MIT (code) | 3 | — | RAM alone breaks §4; grammar reported non-functional (#2159) |
| Distil-Whisper | Gandhi et al., arXiv:2311.00430 | 51 % fewer params than large-v2 (still ~750 M) | no | within 1 % WER, 5.8× faster | none on ARM | MIT | 3 | — | far over budget |
| whisper-streaming (LocalAgreement) | Macháček et al., IJCNLP-AACL 2023, arXiv:2307.14743 | wrapper | simulated | **3.3 s latency** | — | CC-BY-4.0 | 5 | the *policy* (commit on agreement across updates) is reusable on partials | 3.3 s is 6× VoXR's current buffer window |
| Kyutai DSM STT | Zeghidour et al., arXiv:2509.08753 (2025) | 1 B (en/fr, 0.5 s delay), 2.6 B (en, 2.5 s) | yes | not on abs page | MLX on Apple silicon | (models CC-BY-4.0 per repo) | 3 | — | 1–2.6 B params: infeasible |
| WeNet 2.0 (U2++ / TLG + biasing) | Zhang et al., arXiv:2203.15455 (2022) | — | yes | LM: LS-other 6.53 → **5.98**; biasing: AISHELL-2 test_p 14.94 % → **6.17 %** | "quantized model can double inference speed" on ARM (no numbers) | Apache-2.0 | 3, 4 | proves TLG (`T∘min(det(L∘G))`) + biasing on a streaming E2E model | Chinese-first ecosystem; fewer English models |
| Tiny Transducer | Zhang, Sun & Ma, ICASSP 2021, arXiv:2101.06856 | **0.9 M** post-SVD | yes | 9.1–20.5 % rel. improvement over a *larger* hybrid system on edge | "edge devices" (unspecified) | (paper) | 3 | proof a sub-1 M transducer + tiny decoding graph beats hybrid | 2021, Chinese, no released model |
| Silero VAD | github.com/snakers4/silero-vad | ~2 MB JIT | yes | — | **<1 ms per 30 ms chunk, single CPU thread** | MIT | **2, 5** | Real garbage/silence rejection ahead of the decoder | VAD ≠ speech/noise semantics; won't reject in-grammar-sounding coughs alone |
| Entropy-based word confidence | Laptev & Ginsburg, SLT 2022, arXiv:2212.08703 | n/a | n/a | up to **2× (CTC) / 4× (RNN-T)** better at detecting incorrect words vs max-per-frame prob, LibriSpeech | non-trainable, ~same cost as max-prob | (NeMo, Apache-2.0) | **3, 6** | Directly attacks VoXR's flat-confidence limitation | requires per-frame posteriors the current bridge never surfaces |
| Overconfidence calibration under noise | Huo, Zhang & Tang, arXiv:2509.07195 (2025) | 3.4 M post-hoc head | n/a | ECE 0.086 → **0.036** (−58 %), overconfident mass 11.1 % → 6.6 %, at −18…−5 dB SNR | post-hoc, no ARM numbers | (arXiv) | **6** | Calibrated `minConfidence` under game audio | Whisper-medium only; must be re-derived per model |
| ONNX Runtime (CPU EP / XNNPACK) | onnxruntime.ai docs; ORT discussion #29780 | libonnxruntime.so **15 MB** arm64 | — | — | int8 streaming RNN-T 120 M, **217 ms** after speech end; Silero VAD <1 ms/32 ms chunk; **Galaxy S23 Ultra** | MIT | **3** | the realistic Quest runtime | S23U ≫ XR2 Gen 2; unverified on Quest |
| ONNX Runtime QNN EP (Hexagon) | onnxruntime.ai/docs/execution-providers/QNN-ExecutionProvider.html | — | — | — | "QNN HTP backend only supports quantized models"; "does not support models with dynamic shapes"; no LSTM/RNN/Loop | MIT | 3 | — | **Meta does not expose the Quest NPU to third-party apps** — moot |
| ExecuTorch | Nachin et al., arXiv:2605.08195; pytorch/executorch | — | — | — | states it powers Meta on-device AI "across … Meta Quest" | BSD-3 | 3 | Meta-aligned Android/arm64 runtime | no ASR-on-Quest numbers published |
| Zipformer fine-tuning recipe | k2-fsa.github.io/icefall Finetune/from_supervised | — | — | GigaSpeech 20.06 → **13.47 %** dev WER after fine-tuning on the *small* subset | GPU training | Apache-2.0 | **3** | domain-adapt a model to VR-mic + command speech | needs a labelled in-domain corpus VoXR does not have |

---

## Detailed findings

### 1. VOSK's unused C API — `set_grm`, `set_max_alternatives` — `VERIFIED` (locally)

Citation: `alphacep/vosk-api`, `src/vosk_api.h`, master — https://raw.githubusercontent.com/alphacep/vosk-api/master/src/vosk_api.h. Verified locally against the exact binaries in this repo.

The upstream header declares `void vosk_recognizer_set_grm(VoskRecognizer *recognizer, char const *grammar)` with the doc comment *"Reconfigures recognizer to use grammar … @param recognizer Already running VoskRecognizer … @param grammar Set of phrases in JSON array of strings or `\"[]\"` to use default model graph."* It also declares `vosk_recognizer_set_max_alternatives`, documented to emit `{"alternatives":[{"text":…,"confidence":…}, …]}`. I ran `nm -D --defined-only` against both `Runtime/Plugins/Android/arm64-v8a/libvosk.so` and `NativeBridge~/vendor/vosk-linux-x86_64-0.3.45/libvosk.so`: both export **`vosk_recognizer_set_grm`, `vosk_recognizer_set_max_alternatives`, `set_partial_words`, `set_nlsml`** (36 `vosk_*` symbols each). Neither exports `vosk_recognizer_set_endpointer_mode` or `_delays`, although `NativeBridge~/include/vosk_api.h` declares them at lines 33–34.

**Map onto VoXR.** `vosk_bridge_set_grammar` (`vosk_bridge.cpp` L~344) currently does `vosk_recognizer_free(g_recognizer)` then `vosk_recognizer_new_grm(...)`, and additionally refuses to run while `g_running` is true — which is precisely why the command-set switch costs the documented ~50 ms **audio gap** (stage 4). Replacing that body with a single `vosk_recognizer_set_grm(g_recognizer, grammar_json)` call removes both the free/rebuild and the not-while-running restriction, and leaves `vosk_recognizer_set_words(1)` intact (no re-application needed, since the recognizer object survives). Separately, `vosk_recognizer_set_max_alternatives(g_recognizer, N)` in `vosk_bridge_init` would change the final-result JSON shape to an `alternatives` array; stage 6's `Parse` could then score every hypothesis and take the best-scoring *command*, rather than the best-scoring *transcript* — which is exactly the failure shape in `KNOWN_LIMITATIONS.md` ("switch two weapons", "fall modes", "safe five"), where the correct words are plausibly rank-2.

**Feasibility on Quest CPU.** Zero model change, zero APK change, zero new dependency. N-best raises decoder cost somewhat (lattice N-best extraction) but on a grammar-restricted graph the lattice is tiny.

**Risk.** Two unknowns. (a) Does `set_grm` mid-utterance discard buffered audio or corrupt the current partial? The header says "already running" but does not say whether it resets. (b) `max_alternatives > 0` historically changes the result JSON so that per-word `conf`/`start`/`end` are no longer emitted the same way — that would break stage 6's `minConfidence` and the `VoxrWord` timing path. Both are answerable with the existing WAV-replay/push-audio harness (stage 8) without a headset.

**Open questions.** Is the shipped `.so` 0.3.45 or newer? Does `set_grm` invalidate the `[unk]` token handling? Does N-best confidence correlate better with correctness than the current flat per-word `conf`?

### 2. Streaming Zipformer transducer (k2/icefall + sherpa-onnx) — `VERIFIED`

Citations: Zengwei Yao, Liyong Guo, Xiaoyu Yang, Wei Kang, Fangjun Kuang, Yifan Yang, Zengrui Jin, Long Lin, Daniel Povey, *Zipformer: A faster and better encoder for automatic speech recognition*, **ICLR 2024** — https://arxiv.org/abs/2310.11230. Results: https://github.com/k2-fsa/icefall/blob/master/egs/librispeech/ASR/RESULTS.md. Models: https://k2-fsa.github.io/sherpa/onnx/pretrained_models/online-transducer/zipformer-transducer-models.html. Runtime: https://github.com/k2-fsa/sherpa-onnx (Apache-2.0).

Zipformer is a U-Net-shaped encoder whose middle stacks run at lower frame rates, with attention weights re-used across a reorganised block, BiasNorm in place of LayerNorm, SwooshR/SwooshL activations, and the ScaledAdam optimiser. Non-streaming it posts **Zipformer-S 23.3 M → 2.42/5.73**, **M 65.6 M → 2.21/4.79**, **L 148.4 M → 2.06/4.63** on LibriSpeech test-clean/test-other, at 40.8/62.9/107.7 GFLOPs per 30 s of audio, "saving over 50 % FLOPs" versus their reproduced Conformer-L (2.46/5.55 at 122.5 M). Streaming, icefall's `RESULTS.md` reports the 66,110,931-param model at **3.06/7.81** (greedy, 320 ms chunk / 128 left context), **3.01/7.69** (modified beam search), **2.81/7.15** and **2.79/7.05** at 640 ms; and the ~20 M `pruned_transducer_stateless7_streaming` small model at **3.94/9.79** greedy and **3.88/9.53** modified-beam at 320 ms. A 6.1 M variant reaches 5.95/15.03.

**Map onto VoXR.** This replaces stage 3 wholesale: `libvosk.so` → `libonnxruntime.so` + sherpa-onnx; `vosk_recognizer_accept_waveform_s` → `SherpaOnnxOnlineStreamAcceptWaveform`; the JSON result queue stays (sherpa-onnx returns text + optional timestamps). The bridge's file-scope singleton can go away: the C API explicitly supports *"Reuse the same recognizer to create multiple streams"*, which fixes the "single recogniser per process" limitation for free. Stage 5's `UtteranceBuffer` gets a genuinely tunable endpointer underneath it: `enable_endpoint`, `rule1_min_trailing_silence` (2.4 s default, fires with nothing decoded), `rule2_min_trailing_silence` (1.2 s, fires only after something was decoded), `rule3_min_utterance_length` (20 s). Stage 4 is the hard part and is treated separately below.

**Feasibility on Quest CPU.** The `sherpa-onnx-streaming-zipformer-en-20M-2023-02-17` package is encoder 41 M + decoder 527 K + joiner 253 K in int8 (85 M/2.0 M/1.0 M fp32). With `libonnxruntime.so` 15 MB + `libsherpa-onnx-jni.so` 3.7 MB, the arm64 footprint is ~61 MB against today's ~49 MB. sherpa-onnx's own docs quote English streaming RTF **0.06–0.08** and small Chinese CTC int8 **0.038**, but **do not name the CPU** — treat these as order-of-magnitude only.

**Risk.** No published WER for the exact ONNX-exported en-20M package (the 3.94/9.79 figure is from the matching icefall recipe and date, an inference on my part, not a published pairing). No RTF on XR2. The `modified_beam_search` path — required for hotwords — has open issues reporting hallucinated or empty text on silence (k2-fsa/sherpa-onnx #845, #3267), which maps *exactly* onto VoXR's "coughs decode as short in-grammar words" failure.

**Open questions.** What does a 20 M streaming Zipformer do on VoXR's existing 699-utterance TTS corpus and 73-utterance field set, unconstrained? Does its `[unk]`-equivalent (blank-dominated output) actually reject noise better than grammar-mode VOSK?

### 3. HLG / FST-composed decoding over streaming CTC — the real grammar replacement — `VERIFIED`

Citations: sherpa-onnx `python-api-examples/online-zipformer-ctc-hlg-decode-file.py`; `sherpa-onnx/c-api/c-api.h` (`ctc_fst_decoder_config` with a `graph` field documented as *"Path to H.fst, HL.fst, or HLG.fst"*); WeNet 2.0 (Zhang et al., arXiv:2203.15455) for the TLG construction; k2-fsa.github.io/sherpa/onnx/pretrained_models/online-ctc/zipformer-ctc-models.html.

Streaming CTC + HLG is the direct structural analogue of `vosk_recognizer_new_grm`: instead of a phrase list, you compile a grammar into a weighted FST and compose it with the CTC topology and lexicon. sherpa-onnx supports this today for streaming zipformer-CTC — the example downloads `sherpa-onnx-streaming-zipformer-ctc-small-2024-03-18` and passes `ctc_graph=HLG.fst` to `OnlineRecognizer.from_zipformer2_ctc(...)`. WeNet 2.0 builds the equivalent as `T ∘ min(det(L ∘ G))` and decodes with CTC WFST beam search, reporting LibriSpeech test-other 6.53 → **5.98** with an n-gram G, and contextual biasing on AISHELL-2 `test_p` from 14.94 % to **6.17 %** at boost score 7 with no degradation on negative sets.

**Map onto VoXR.** `GenerateGrammarJson` (`VoxrCommandParser.cs` L3076–3170) is already a grammar compiler in miniature — it walks patterns, emits contiguous required-literal runs as multi-word phrases plus every individual word, adds slot values, alias keys, the digit vocabulary, follow-up words and `[unk]`. That structure maps cleanly onto a **G.fst**: required-literal runs become mandatory arcs, optional literals become epsilon-skippable arcs, choice slots become alternation over their values and aliases, NumberSequence slots become a digit loop, and the `[unk]` bucket becomes an explicit garbage/filler arc with a tunable cost. The comment in `AddPhrase` — that single words are kept deliberately so a VAD-split fragment can still decode, "making the sequence constraint a bias rather than a hard rule" — is exactly the compromise an FST removes: with a real G you can encode *both* the full pattern path and the fragment paths, each with its own weight, instead of flattening them into one bag.

**Feasibility on Quest CPU.** Decoding cost is fine; the problem is **build time and toolchain**. HLG compilation needs k2/OpenFST at *authoring* time, not runtime. For VoXR that means a Unity Editor-side compile step producing a `.fst` per command set — which is arguably better than today's runtime rebuild, because it removes the audio gap by construction (swap a pre-compiled graph). Dynamic slot values (runtime-provided target names) are the hard case: they would need either a runtime-composable sub-grammar (Kaldi's `GrammarFst` nonterminal mechanism, which `daanzu/kaldi-active-grammar` exploits — but that project is **AGPL-3.0**, unusable here) or a fall back to biasing for the dynamic part.

**Risk.** No small **English** streaming zipformer-CTC model is published on the sherpa-onnx page (the 25 MB int8 small CTC model is Chinese). VoXR would need to either use the CTC head of a hybrid model or train one. That is a real gap, not a detail.

**Open questions.** Can a `HL.fst` (lexicon only, no G) plus per-decode biasing carry the dynamic-slot case? What does a compiled-G decoder do with the coverage/admission rules in stage 6 — does it subsume the leading-required-miss bar, or make it unnecessary?

### 4. sherpa-onnx keyword spotting mode (Zipformer 3.3 M) — `VERIFIED`

Citations: https://k2-fsa.github.io/sherpa/onnx/kws/index.html and .../kws/pretrained_models/index.html; `SherpaOnnxCreateKeywordStreamWithKeywords()` in `c-api.h`.

sherpa-onnx's KWS is *"like a tiny ASR system, but it can only decode words/phrases in the given keywords."* Keywords are token sequences with two per-keyword knobs: a **boosting score** (`:1.5`) that helps a path survive beam search, and a **trigger threshold** (`#0.35`), a floor on acoustic probability. `sherpa-onnx-cli text2token` converts plain text into the model's units (BPE, phonemes, pinyin, CJK). Available English model: `sherpa-onnx-kws-zipformer-gigaspeech-3.3M-2024-01-01`, trained on GigaSpeech XL (10 000 h) — encoder 4.6 M int8, decoder 272 K, joiner 160 K, i.e. **~5 MB**. A newer bilingual `zh-en-3M-2025-12-20` offers chunk-8 (160 ms) and chunk-16 (320 ms) latency variants.

**Map onto VoXR.** This is the option that deletes the audio gap *and* the OOV problem in one move. Keywords are supplied per-stream at stream creation, so switching command sets is a new stream, not a model reload — no gap. "cqb"/"pdc" stop being OOV because keywords are specified as *token* sequences, so a developer can supply the phoneme/BPE spelling directly rather than hand-authoring "see queue bee" aliases. And the trigger threshold is a real, per-keyword rejection knob — the thing VoXR currently lacks (grammar mode has no garbage model, so coughs become "on"/"from"/"four").

**Feasibility.** 5 MB replaces a 40 MB model. Encoder cost at 3.3 M params is a small fraction of a 20 M Zipformer. This is by far the cheapest model in this report to run on Quest.

**Risk — and it is the decisive one.** KWS returns *keyword hits with timestamps*, not a word lattice. VoXR's stage 6 does sliding-start matching, per-element scoring, coverage charging, NumberSequence parsing, sequential multi-command extraction, and sibling-tie detection over a token stream. A NumberSequence like "two seven zero" would have to be ten digit keywords whose hit ordering the parser reassembles — losing the run-level phrase bias that `AddPhrase` currently buys. Reported accuracy (recall/false-alarm) is **not published** for these models, which is a significant evidence gap for a false-trigger-sensitive application.

**Open questions.** What is the false-alarm rate per hour at a usable recall for a ~60-phrase command set? Can KWS run *alongside* a full recogniser (two streams, one model each) so the parser keeps a transcript while KWS supplies high-precision anchors?

### 5. Context-graph biasing: sherpa-onnx hotwords, NVIDIA CTC-WS, TurboBias 2.0 — `VERIFIED`

Citations: https://k2-fsa.github.io/sherpa/onnx/hotwords/index.html; Andrei Andrusenko, Aleksandr Laptev, Vladimir Bataev, Vitaly Lavrukhin, Boris Ginsburg, *Fast Context-Biasing for CTC and Transducer ASR models with CTC-based Word Spotter*, arXiv:2406.07096 (Interspeech 2024); Vladimir Bataev et al., *TurboBias 2.0*, arXiv:2608.21343 (2026).

sherpa-onnx implements hotwords as an **Aho-Corasick automaton** over tokenised phrases, with goto/failure/output arcs; the boost "distributes on the arcs evenly along the path" and is *cancelled* if a partial match does not complete — so biasing toward "cease fire" does not leak a bonus onto "cease" alone. It requires `modified_beam_search`; `greedy_search` ignores hotwords entirely. NVIDIA's CTC-WS takes a different route: a word spotter matches CTC log-probabilities against a compact context graph **independently of the main decode**, then merges detections into greedy output — F-score **0.87** (89 % precision / 85 % recall), WER 14.02 → **10.48** on CTC, and **15 s versus 179 s** decode time over a 7-hour set against pyctcdecode's beam-search biasing (≈12×). It was tested at 100 words and degraded "relatively stably" to 1000. TurboBias 2.0 extends this to streaming and per-stream contexts: at 1.12 s latency, F-score goes 62.2 → **81.7** with boosting on; per-stream config means *"each stream in a batch [uses] an independent context-biasing configuration."*

**Map onto VoXR.** This is the *soft* version of stage 4. `GenerateGrammarJson`'s output becomes a hotwords list rather than a closed vocabulary: the multi-word required-literal runs become boosted phrases, slot values and alias keys become boosted phrases, and the digit vocabulary gets a modest boost. The decoder stays open-vocabulary, which fixes two documented VoXR problems at once: **wrong-mode rejection gains an explanation** (out-of-set speech transcribes as itself instead of collapsing to `[unk]`, so the game *can* say "that's a navigation command"), and out-of-grammar noise no longer has to be forced onto the nearest in-grammar word. CTC-WS's independence from the main decode is architecturally attractive here — it is a second, cheap pass over posteriors the encoder already produced, and its precision/recall are directly measurable on VoXR's existing corpora.

**Feasibility.** Aho-Corasick over ~200 phrases is negligible CPU. CTC-WS in NeMo is Apache-2.0 but is a Python/NeMo implementation — porting it to the C++ bridge is real work, though the algorithm is small.

**Risk.** Biasing does not close the vocabulary, so the "cease fire" → "safe five" *correct rejection* behaviour disappears — VoXR would have to reject at stage 6 instead, on a transcript that now says "cease fire" while the weapons set is inactive. Arguably an improvement, but a behaviour change with a migration cost. And `modified_beam_search` costs more than greedy and has the open silence-hallucination issues noted above.

**Open questions.** Boost score calibration: what boost makes command phrases win without inventing them out of breath noise? Does CTC-WS's 0.87 F-score hold at ~60 short, phonetically-confusable command phrases rather than 100 rare proper nouns?

### 6. VOSK's own successors: `0.22-lgraph` and the Zipformer2 migration — `VERIFIED`

Citations: https://alphacephei.com/vosk/models; https://huggingface.co/alphacep/vosk-model-small-streaming-bn; https://huggingface.co/alphacep (org listing).

`vosk-model-en-us-0.22-lgraph` is **128 MB**, reports **7.82 % LibriSpeech / 8.20 % TED-LIUM**, is Apache-2.0, and is described as a *"Big US English model with dynamic graph"* — the dynamic graph is what makes runtime vocabulary reconfiguration possible at all. Against today's `small-en-us-0.15` (40 MB, **9.85 %/10.38 %**) that is roughly a 21 % relative WER cut for 3.2× the size. The full `0.22` (1.8 GB) and `0.42-gigaspeech` (2.3 GB) are out of scope.

Separately and more importantly: **Alpha Cephei is migrating VOSK to Zipformer2 trained with k2-fsa/icefall.** `alphacep/vosk-model-small-streaming-bn` is described as a *"Small Zipformer2 model"* trained with icefall, Apache-2.0, and the corresponding sherpa-onnx package is `sherpa-onnx-streaming-zipformer-bn-vosk-2026-02-09`. The org also lists `vosk-model-small-streaming-ru` (May 2025), `vosk-model-streaming-ru` (Sept 2025), `vosk-model-small-streaming-uz` (Jul), `vosk-model-tg` (Jul). **As of this survey there is no English `vosk-model-*-streaming-en`** in the org listing.

**Map onto VoXR.** Two readings. Conservative: swap to `0.22-lgraph` (stage 3 only, zero API change) and pay 88 MB of APK for ~21 % relative WER. Strategic: the VOSK project's own direction is Zipformer2 + sherpa-onnx, so moving VoXR to sherpa-onnx is moving *with* the upstream, not away from it — and an English VOSK Zipformer2 model landing later would then be a drop-in.

**Feasibility.** `0.22-lgraph` at 128 MB against a stated ~40 MB budget is the problem, not the compute.

**Risk.** `0.22-lgraph`'s WER is measured on read speech; VoXR's failures are on short commands over a low-gain VR mic under exertion, where a bigger LM may not help and may even hurt (more vocabulary to be confused by). This is exactly the "cease fire → safe five" dynamic in reverse.

**Open questions.** Does `0.22-lgraph` fix any of the four documented substitution pairs on the existing WAV corpus? Is an English Zipformer2 VOSK model on the roadmap?

### 7. Moonshine v2 — `VERIFIED`

Citations: Nat Jeffries, Evan King, Manjunath Kudlur, Guy Nicholson, James Wang, Pete Warden, *Moonshine: Speech Recognition for Live Transcription and Voice Commands*, arXiv:2410.15608 (2024); Evan King, Adam Sabra, Manjunath Kudlur, James Wang, Pete Warden, *Flavors of Moonshine*, arXiv:2509.02523 (2025); Manjunath Kudlur, Evan King, James Wang, Pete Warden, *Moonshine v2: Ergodic Streaming Encoder ASR for Latency-Critical Speech Applications*, arXiv:2602.12241 (2026); https://github.com/moonshine-ai/moonshine (MIT).

v1 is an encoder-decoder transformer with RoPE trained without zero-padding, so encoder cost scales with actual audio length — 5× less compute than Whisper tiny.en on a 10 s segment at no WER cost. v2 replaces full attention with **sliding-window self-attention** ("ergodic streaming encoder") for bounded latency. Table 1 gives Tiny **33.57 M** (320-dim, 6/6 layers), Small **123.36 M**, Medium **244.93 M**. Table 3 (8-benchmark average): Tiny **12.01 %**, Small **7.84 %**, Medium **6.65 %**; LibriSpeech clean/other Tiny **4.49/12.09**, Small 2.49/6.78. Table 2, on an **Apple MacBook M3**: Tiny **50 ms** response latency at 8.03 % compute load, Small 148 ms / 17.97 %. Code and models are MIT (legacy non-English non-streaming v1 models are under a non-commercial Community License — check per-model).

**Map onto VoXR.** Stage 3 replacement with an MIT licence and a genuinely small streaming variant. Tiny at 33.57 M is under the current 40 MB model budget in fp32 and comfortably under in int8. Its 4.49 % LibriSpeech clean beats VOSK-small's 9.85 % by 2.2×.

**Feasibility.** Unknown on ARM. The only published latency is on an M3 laptop; the paper reports no ARM/Raspberry Pi/Android numbers. `transcribe.cpp`'s model card for **moonshine-streaming-small** gives wall-clock: 349 ms for 11 s of audio (32× realtime) on an AMD Ryzen 7 4750U Pro with the Vulkan backend, Q8_0, 189 MB file — that is the small model, on a laptop, with a GPU backend, so it does not transfer to Quest CPU.

**Risk — structural.** Moonshine is encoder-**decoder**. There is no CTC posterior and no transducer lattice, so **neither HLG composition nor Aho-Corasick context graphs apply**. The only grammar mechanism available is constrained beam search over the decoder's token vocabulary — the same mechanism whisper.cpp's GBNF grammar implements, and which whisper.cpp issue #2159 reports as **not actually constraining output**. That is a serious warning for anyone assuming decoder-side grammar masking is a solved problem in a C++ ASR runtime.

**Open questions.** Is there an ONNX/int8 export of v2 Tiny for arm64? Does sherpa-onnx's Moonshine support (listed among its model families) cover v2 streaming, and does it expose any biasing?

### 8. Whisper on device: whisper.cpp, Distil-Whisper, whisper-streaming — `VERIFIED` (closing the lead out)

Citations: https://github.com/ggml-org/whisper.cpp (MIT); https://github.com/ggml-org/whisper.cpp/issues/2159; whisper.cpp `examples/command`; Sanchit Gandhi, Patrick von Platen, Alexander M. Rush, *Distil-Whisper*, arXiv:2311.00430; Dominik Macháček, Raj Dabre, Ondřej Bojar, *Turning Whisper into Real-Time Transcription System*, IJCNLP-AACL 2023, arXiv:2307.14743.

whisper.cpp's own table: tiny 75 MiB disk / **~273 MB RAM**, base 142 MiB / ~388 MB, small 466 MiB / ~852 MB. The `stream` example is explicitly a sliding window (`--step 500 --length 5000`) — it re-runs a 5 s encoder-decoder every half second, not a streaming decode. There *is* a command-oriented path: `examples/command` has a "guided mode" that takes a `commands.txt` and *"the transcription will be guided to classify your command into one from the list"*, claimed to be *"extremely efficient in terms of performance, since it integrates very well with the 'partial Encoder' idea"* — but the README gives **no accuracy or latency numbers**, and the separate GBNF grammar path has a reproduced bug report (issue #2159, opened 2024-05-16) that grammar-constrained output is byte-identical to unconstrained output regardless of `--grammar-penalty`. Distil-Whisper is 51 % fewer parameters than Whisper large-v2 (still ~750 M) at 5.8× faster and within 1 % WER — an order of magnitude over budget. whisper-streaming's LocalAgreement policy achieves **3.3 s latency**, six times VoXR's 0.5 s buffer window.

**Map onto VoXR.** Only one thing here is worth keeping: **the LocalAgreement policy itself** (commit a prefix once two successive updates agree on it) is a general recipe and could be applied to stage 5 over VOSK's partial results, independently of Whisper. That is a stage-5/7 idea, not a stage-3 one, and it competes with the existing eager-flush mechanism.

**Feasibility.** tiny's ~273 MB RAM alongside a 72–90 fps VR game is not compatible with §4's "tens of MB RAM".

**Risk.** None to take — this lead should be closed.

**Open questions.** None material.

### 9. NVIDIA's 2026 streaming stack (Nemotron / FastConformer / Parakeet) — `VERIFIED`, and why it is out

Citations: Nenad Banfic, David Fan, Kunal Vaishnavi, Sam Kemp, Sunghoon Choi, Rui Ren, Sayan Shaw, Meng Tang, *Pushing the Limits of On-Device Streaming ASR*, arXiv:2604.14493 (2026); https://huggingface.co/nvidia/nemotron-speech-streaming-en-0.6b; Dima Rekesh et al., *Fast Conformer with Linearly Scalable Attention*, ASRU 2023, arXiv:2305.05084.

arXiv:2604.14493 is the most directly relevant systems paper in this survey: an empirical comparison of **over 50 configurations** across Whisper, Nemotron, Parakeet TDT, Canary, Conformer Transducer and Qwen3-ASR, explicitly for **CPU-based edge deployment**. Streaming results (their Table 5): Nemotron-0.6B **7.28 % WER at 2.46× RTFx, 0.56 s delay**; Conformer Transducer XL 11.06 % @1.27×; Parakeet TDT-0.6B-v3 12.83 % @1.38×; Canary-1B-v2 12.45 % @2.93×; Qwen3-ASR-1.7B 10.45 % @**0.49×** (slower than real time). Quantisation (Table 6): FP32 ONNX 2.47 GB @8.03 % → int4 k-quant **0.67 GB @8.20 %**, a 73 % size cut for 0.17 points absolute. FastConformer's core trick is 8× downsampling via three depthwise-separable conv layers plus a kernel reduction 31→9 and subsampling channels 512→256, giving 2.8× over Conformer.

**Map onto VoXR.** It does not. Three disqualifiers: (a) the measurement CPU is an **AMD EPYC 7V12 with inference pinned to 32 cores** — nothing about that transfers to two XR2 performance cores; (b) the paper explicitly *"does not discuss ARM processors or evaluate models smaller than the 0.6B variants tested"*; (c) `nemotron-speech-streaming-en-0.6b` is under the **NVIDIA Open Model License**, which is not Apache-2.0 and fails §4's licensing constraint for a redistributed Unity package.

**Value anyway.** Two transferable facts. First, **int4/int8 k-quantisation of a streaming ASR encoder costs ~0.2 WER points** — that is a strong prior for quantising a Zipformer to int8 without accuracy panic. Second, the *architecture ranking* for CPU streaming is cache-aware transducer > AED > LLM-based, which agrees with choosing a transducer or CTC model over anything Whisper-shaped.

**Risk / open questions.** Whether the ranking holds at 20–35 M params, where it was never measured.

### 10. Confidence: entropy-based estimation and noise calibration — `VERIFIED`

Citations: Aleksandr Laptev, Boris Ginsburg, *Fast Entropy-Based Methods of Word-Level Confidence Estimation for End-To-End ASR*, **SLT 2022** (Doha, Jan 2023), arXiv:2212.08703; Mingyue Huo, Yuheng Zhang, Yan Tang, *Identifying and Calibrating Overconfidence in Noisy Speech Recognition*, arXiv:2509.07195 (Sept 2025).

Laptev & Ginsburg give **non-trainable** confidence estimators that normalise and aggregate per-frame entropy into per-unit and per-word confidences for both CTC and RNN-T, at *"similar computational complexity to the traditional method based on the maximum per-frame probability"* but *"more adjustable, [with] a wider effective threshold range"*. On LibriSpeech they are up to **2× (Conformer-CTC) and 4× (Conformer-RNN-T)** better at detecting incorrect words than max-per-frame-probability. Huo et al. show that Whisper-medium stays calibrated on clean audio but that at **−18 to −5 dB SNR, 10–20 % of tokens are wrong with confidence above 0.7**; their post-hoc overconfidence classifier plus selective temperature scaling (3.4 M params, base model untouched) cuts ECE from **0.086 to 0.036 (−58 %)** and overconfident mass from 11.1 % to 6.6 %.

**Map onto VoXR.** This is the *only* line of work in the survey that addresses the documented **flat-confidence** limitation head-on ("two" is ~0.50 regardless of clarity; `minConfidence` cannot be raised above 0.5 without rejecting NumberSequence commands). VOSK's per-word `conf` is a lattice posterior with no calibration story. Any move to a CTC or transducer model makes entropy-based confidence available essentially for free — it is a function of posteriors the decoder computes anyway — and it would give stage 6 a *usable* threshold with a wider operating range, which in turn makes the coverage/admission/leading-miss machinery cheaper to justify. Huo et al. matters because VoXR's environment (game audio, exertion breathing, low-gain Quest mic) is precisely the low-SNR regime where overconfidence appears.

**Feasibility.** Entropy aggregation is a handful of floats per frame — negligible on Quest. The 3.4 M calibration head is not free but is small.

**Risk.** Requires the bridge to surface per-frame posteriors, which neither the current VOSK bridge nor sherpa-onnx's default result struct exposes. That is a C ABI change, not a config change.

**Open questions.** On VoXR's field corpus, does entropy-based confidence separate the known substitutions ("two"/"to", "all"/"fall") better than the current flat `conf`? Can a single calibration temperature be fit on the 699-utterance TTS corpus and validated on the 73 field utterances?

### 11. Silero VAD and real garbage rejection — `VERIFIED`

Citations: https://github.com/snakers4/silero-vad (MIT); sherpa-onnx `SherpaOnnxVadModelConfig` (supports Silero and Ten VAD).

~2 MB JIT model, 8 kHz and 16 kHz, *"one audio chunk (30+ ms) takes less than 1 ms to be processed on a single CPU thread"*, ONNX export available, MIT, trained on corpora spanning 6000+ languages.

**Map onto VoXR.** Stage 2/5. VoXR currently has **no** speech/non-speech gate: audio flows from AGC straight into VOSK, and in grammar mode there is no garbage model, so coughs, hums and breathing decode as short in-grammar words and fire commands. A VAD in front of the decoder is the cheapest structural fix for that specific failure — it is 1 ms per 30 ms chunk, i.e. ~3 % of one core, and it costs 2 MB. It also gives stage 5 an *independent* endpoint signal that does not depend on the decoder's own blank-counting, which is what currently splits commands at ~0.8 s pauses.

**Feasibility.** Trivially affordable. It can be added *without* replacing VOSK — Silero-VAD in the C++ bridge upstream of `vosk_recognizer_accept_waveform_s`.

**Risk.** A VAD distinguishes speech from non-speech, not commands from chatter. A cough that sounds like speech still passes. It reduces, not eliminates, the false-trigger class. It also adds an ONNX Runtime dependency (or a hand-rolled inference for one small model) to an otherwise ORT-free build.

**Open questions.** How much of the false-trigger set in the 73-utterance field log is non-speech (VAD-catchable) versus speech-like? That is answerable from the existing debug logs without any new code.

### 12. Domain fine-tuning a small Zipformer — `VERIFIED`

Citation: https://k2-fsa.github.io/icefall/recipes/Finetune/from_supervised/finetune_zipformer.html.

icefall ships a supported fine-tuning recipe: a LibriSpeech-pretrained Zipformer scores **20.06 % / 19.27 %** WER on GigaSpeech dev/test; after fine-tuning on the *small* GigaSpeech subset at lr 0.0045 (≈1/10 of original) for 20 epochs, it reaches **13.47 % / 13.66 %** — roughly a **33 % relative** reduction on the target domain.

**Map onto VoXR.** This is the answer to "fewer substitutions on *the words that matter*". Neither a bigger general model nor a tighter grammar fixes "switch to weapons" → "switch two weapons"; adapting the acoustic model to (a) the Quest mic's response after the existing 48→16 kHz decimation and AGC, and (b) the actual command vocabulary and delivery style, is the mechanism that does. VoXR already has the corpus infrastructure for this: TTS fixtures, the 699-utterance A/B corpus, WAV replay through the real DSP, and 73 labelled field utterances.

**Feasibility.** Training needs GPUs (the recipe recommends 2+); that is off-device, one-time, and does not affect the runtime budget at all.

**Risk.** 73 field utterances / 361 words from **one speaker** is far too little to fine-tune on without catastrophic overfitting to that speaker. The GigaSpeech result used a real corpus. Producing a usable in-domain corpus (multi-speaker, in-headset, with the game's audio bed) is a data-collection project, not a code project — and it is the single most expensive item in this report.

**Open questions.** How many hours, from how many speakers, are needed to move the documented confusion pairs? Would augmentation (room simulation + game-audio mixing + the real DSP chain) over TTS voices substitute for real recordings?

---

## How to keep a command grammar under a neural decoder

There are four realistic mechanisms. They are not mutually exclusive; the strongest design likely uses two.

**(a) WFST/HLG composition over CTC — hard constraint, closest to today.**
Compile the command patterns into a grammar FST `G`, compose with lexicon and CTC topology (`HLG`, or WeNet's `T ∘ min(det(L ∘ G))`), decode with CTC WFST beam search. *Pros:* it is a genuine closed vocabulary with per-arc weights, so required-literal runs, optional literals, slot alternations and digit loops each get their own cost — strictly more expressive than today's flat phrase bag, which the code's own comment concedes is "a bias rather than a hard rule". Supported in shipping code: sherpa-onnx `ctc_fst_decoder_config.graph` accepts `H.fst`, `HL.fst` or `HLG.fst`; WeNet reports LibriSpeech test-other 6.53 → 5.98 with an n-gram `G`. Pre-compiled graphs swap with no rebuild → **audio gap gone**. *Cons:* needs an offline compile step (k2/OpenFST) in the Unity Editor; dynamic runtime slot values have no clean story without Kaldi-style `GrammarFst` nonterminals (and the reference implementation of those, `daanzu/kaldi-active-grammar`, is **AGPL-3.0** — unusable in an Apache-2.0 package); **no small English streaming zipformer-CTC model is currently published**.
Citations: sherpa-onnx `online-zipformer-ctc-hlg-decode-file.py`; `c-api.h`; Zhang et al., arXiv:2203.15455.

**(b) Context-graph biasing — soft constraint, cheapest to adopt.**
Boost command phrases in beam search via an Aho-Corasick context graph (sherpa-onnx hotwords) or spot them in CTC posteriors independently of the decode (NVIDIA CTC-WS). *Pros:* no graph compilation, changes per-utterance, per-stream; measured effect is large (WeNet AISHELL-2 `test_p` 14.94 % → 6.17 %; CTC-WS F-score 0.87, WER 14.02 → 10.48, 12× faster than beam-search biasing); sherpa-onnx cancels the boost on incomplete matches, so partial-phrase leakage is handled; TurboBias 2.0 shows per-stream contexts and streaming beam search (F 62.2 → 81.7 at 1.12 s latency). Crucially it **restores wrong-mode explainability**: out-of-set speech transcribes as itself rather than collapsing to `[unk]`. *Cons:* the vocabulary stays open, so the "correct rejection" property of grammar mode moves from the decoder to stage 6; requires `modified_beam_search`, which is slower and has open reports of empty/hallucinated output on silence (sherpa-onnx #845, #3267) — the exact hazard VoXR already suffers.
Citations: k2-fsa hotwords docs; Andrusenko et al., arXiv:2406.07096; Bataev et al., arXiv:2608.21343.

**(c) KWS-style transducer — hardest constraint, smallest model.**
A dedicated keyword-spotting Zipformer that can *only* emit the supplied phrases, each with a boosting score (`:1.5`) and a trigger threshold (`#0.35`). *Pros:* ~5 MB int8 for the English GigaSpeech-3.3M model; keywords set per-stream via `SherpaOnnxCreateKeywordStreamWithKeywords()` → **no reload, no gap**; keywords are specified as token sequences, so "cqb"/"pdc" stop being OOV without hand-authored phonetic aliases; a real per-keyword rejection threshold, which grammar-mode VOSK simply does not have. *Cons:* output is hits, not a transcript — NumberSequence slots, sequential multi-command extraction, coverage scoring and sibling-tie detection would all have to be rebuilt on a hit stream; **no published recall/false-alarm numbers** for these models.
Citations: k2-fsa KWS docs + pretrained models; Zhang et al., *U2-KWS*, ASRU 2023, arXiv:2312.09760 (41 % relative wake-up-rate improvement at a fixed 0.5 false alarms/hour).

**(d) Decoder-side constrained beam search (token masking) — avoid for now.**
Mask the decoder's token distribution to grammar-legal continuations each step. This is the only option for encoder-decoder models (Whisper, Moonshine). *Pros:* no FST toolchain; expresses arbitrary CFGs. *Cons:* the reference C++ implementation of this idea, whisper.cpp's GBNF grammar with `--grammar-penalty`, has a filed report that it **does not change the output at all** (issue #2159, 2024) — and its own README's alternative is a separate "guided mode" with no published accuracy. Constrained decoding over an autoregressive decoder also interacts badly with streaming, since a committed prefix cannot be revised. Treat as unproven for ASR in production C++.

**Recommendation shape.** (a) or (c) for the closed-set property VoXR's parser depends on today; (b) layered on top for dynamic slot values and for wrong-mode explainability. (d) only if a Moonshine-class encoder-decoder is chosen, and only after the masking is proven to actually bind.

---

## What this area cannot fix

- **Everything downstream of the transcript.** The parser's sliding-start matching, coverage charging, admission rule, leading-required-miss bar, sibling-tie disambiguation, pending-slot resolution, `requiresConfirmation` and `commandCooldown` are dialogue and intent-selection logic. A better acoustic model changes the *input quality* to that logic; it does not subsume any of it. Any claim that a new decoder "makes the bar unnecessary" needs to say which measured failure the bar was earned by and show that failure gone.
- **Single-speaker evidence.** The field base is 10 sessions / 73 utterances / 361 words from **one speaker**. No model choice in this report can be validated to a confidence worth acting on against that corpus. Model selection will out-run the evidence unless the corpus grows.
- **NPU acceleration on Quest.** The ONNX Runtime QNN EP exists and targets Hexagon HTP, but requires quantised models with **no dynamic shapes** and does not support LSTM/RNN or Loop operators — and, more decisively, Meta does not expose the Hexagon NPU to third-party Quest applications (developer requests for it exist; no public API does). Plan for CPU only.
- **Published RTF numbers.** Not one number in this survey was measured on XR2 Gen 1 or Gen 2. The XR2 Gen 2 has 2 performance + 4 efficiency cores and "just 33 %" CPU improvement over XR2 Gen 1; the nearest useful ARM datapoint (217 ms post-speech transcript for an int8 120 M streaming RNN-T on ORT CPU EP/XNNPACK) is on a Galaxy S23 Ultra, a substantially stronger CPU. **Any plan that budgets CPU from a published RTF is building on sand.**
- **Short function words.** "to"/"two", "a"/"on", "all"/"fall" carry almost no acoustic information at conversational rate. A 2.5× better model reduces but does not remove this; the durable fixes are lexical (design commands without confusable monosyllables) and structural (phrase-level constraints that make the *sequence* cheap), both of which VoXR already partly does.
- **Multi-language.** Every English-competitive small streaming model surveyed is English-only or Chinese-first. The multilingual options at this size (Flavors of Moonshine: Arabic, Chinese, Japanese, Korean, Ukrainian, Vietnamese; Nemotron multilingual 0.6B) either exclude the likely target languages or fail the licence/size bar.
- **Model size versus APK.** Moving to ONNX Runtime costs ~19 MB of native library before a single model byte. There is no configuration in this report that is *both* significantly more accurate and significantly smaller than today's 40 MB + 8.9 MB, except KWS mode — which pays for it by giving up the transcript.

---

## Ranked recommendations for VoXR

**1 — Use the VOSK API you already ship: `set_grm` and `set_max_alternatives`.**
This is the highest ratio of value to risk in the entire report and it is verified against the exact binaries in this repository, not against a paper. `vosk_recognizer_set_grm` reconfigures a *running* recognizer's grammar, which makes the documented ~50 ms audio gap (stage 4) an implementation artefact of `vosk_bridge_set_grammar`'s free-and-recreate, not a property of VOSK. `vosk_recognizer_set_max_alternatives` turns stage 3's single hypothesis into an N-best list with per-hypothesis confidence, which lets stage 6 select the best-scoring *command* rather than parse the best-scoring *transcript* — attacking the in-grammar substitution class ("switch two weapons", "fall modes") at the only place where the correct words still exist. Neither costs an APK byte, a new dependency, or a licence review.
*Cheapest validating experiment:* two `InjectText`-free harness runs on the existing push-audio + WAV-fixture path. (a) Call `set_grm` between two fixture replays with no recognizer recreation and assert both decode correctly and no samples are dropped — the audio gap is measurable as lost samples, entirely in WSL. (b) Set `max_alternatives=5`, replay the 699-utterance A/B corpus, and count how often the correct command appears in ranks 2–5 when rank 1 fails. If that number is near zero, drop the idea; if it is large, it is the cheapest accuracy win available. Watch for the known hazard that `max_alternatives>0` may suppress per-word `conf`/`start`/`end`, which stage 6 depends on.

**2 — Put a VAD in front of the decoder.**
VoXR has no speech/non-speech gate, and grammar mode has no garbage model, so non-speech is *forced* onto the nearest in-grammar word — this is the documented cause of coughs and breathing firing commands. Silero VAD is MIT, ~2 MB, and processes a 30 ms chunk in under 1 ms on a single CPU thread; it sits in the C++ bridge between the AGC and `accept_waveform_s` and requires no model change. It also gives stage 5 an endpoint signal independent of VOSK's blank-counting VAD, which is what splits commands at ~0.8 s pauses today. This is orthogonal to every other recommendation and survives whichever decoder is chosen.
*Cheapest validating experiment:* no code at all first — classify the existing 73-utterance field debug logs (`Library/VoxrDebugLogs/`) into false triggers that were non-speech versus speech-like. That upper-bounds the win before anything is built. Only if the non-speech share is material, wire Silero into the push-audio harness and re-run the fixture corpus with a deliberate cough/hum track appended.

**3 — Prototype a streaming Zipformer through sherpa-onnx, unconstrained, and measure it against the existing corpora.**
The accuracy headroom is the clearest number in this survey: 9.85 % (VOSK small, LibriSpeech test-clean) versus 3.94 % (≈20 M streaming Zipformer, greedy, 320 ms chunk) at comparable int8 size, in an Apache-2.0 stack whose C ABI supports multiple recognizers and streams (fixing the single-recognizer limitation for free) and whose endpointing is a tunable three-rule policy rather than a fixed 0.8 s VAD. VOSK's own maintainers are migrating to this stack (`sherpa-onnx-streaming-zipformer-bn-vosk-2026-02-09`, `alphacep/vosk-model-small-streaming-*` "Small Zipformer2 model trained with k2-fsa/icefall"), so this is moving with upstream. Two existing Apache-2.0 Unity wrappers mean the C# seam is not novel. Do this *unconstrained* first: the grammar question (rec. 4) is separable and should not gate the measurement.
*Cheapest validating experiment:* run `sherpa-onnx-streaming-zipformer-en-20M-2023-02-17` (int8) offline in WSL over the 699-utterance A/B corpus and the 73 field WAVs, using the same 48→16 kHz decimation and AGC the real pipeline applies, and diff the transcripts against VOSK's on exactly the documented confusion pairs. No Unity, no device, no bridge changes. Then, separately, build the arm64 `.so` and time one encoder chunk on device to get a real RTF — every published RTF in this report is on non-Quest silicon.

**4 — Decide the grammar mechanism explicitly, and prefer HLG for the static part plus biasing for the dynamic part.**
`GenerateGrammarJson` is already a grammar compiler that flattens structure it knows about — the code comments say so: single words are kept "deliberately" so VAD-split fragments still decode, "making the sequence constraint a bias rather than a hard rule". An FST `G` recovers that structure with per-arc weights: required runs as mandatory paths, optional literals as epsilon-skippable arcs, choice slots as weighted alternations, NumberSequence as a digit loop, and an explicit garbage arc where `[unk]` is today. sherpa-onnx already accepts a compiled `HLG.fst` for streaming CTC (`ctc_fst_decoder_config.graph`). Move graph compilation to the Unity Editor, and the audio gap disappears by construction. Cover runtime-supplied slot values with hotword biasing (Aho-Corasick, boost cancelled on incomplete match), which also restores wrong-mode explainability that grammar mode destroys today.
*Cheapest validating experiment:* before touching C++, write the `GenerateGrammarJson` → OpenFST `G.txt` translator in C# and compile one command set's `HLG.fst` offline with k2 in WSL, then decode the same 699-utterance corpus through the Python `online-zipformer-ctc-hlg-decode-file.py` path. That answers "is our pattern language expressible as an FST, and does it beat the phrase bag" for a day's work and no production code. Note the blocker up front: no small **English** streaming zipformer-CTC model is currently published, so this experiment may need the CTC head of a hybrid model.

**5 — Fix confidence before tightening any threshold, and only then consider fine-tuning.**
`minConfidence` is currently pinned at 0.4 because "two" scores ~0.50 regardless of clarity — an uncalibrated posterior is why a whole class of rejection is unavailable to stage 6. Entropy-based word confidence (Laptev & Ginsburg, SLT 2022) is non-trainable, costs about what max-probability costs, and is up to 2×/4× better at flagging wrong words for CTC/RNN-T; it becomes available the moment the decoder is CTC or transducer. Huo et al. (2025) show that the low-SNR regime VoXR lives in is exactly where confidence goes overconfident (10–20 % of tokens wrong above 0.7 confidence at −18…−5 dB) and that a small post-hoc head halves ECE. Fine-tuning (icefall's recipe: GigaSpeech 20.06 → 13.47 % on the *small* subset) is the only thing that fixes the specific confusable words, but it is last on this list for a reason: 73 utterances from one speaker cannot support it, so it is a data-collection project first.
*Cheapest validating experiment:* on whatever CTC/transducer prototype rec. 3 produces, emit per-frame entropy alongside the transcript for the 699-utterance corpus, then plot the confidence separation between the known-correct and known-substituted decodes of the documented confusion pairs. If entropy separates them where VOSK's flat `conf` does not, a raisable `minConfidence` follows directly — and that is a change to stage 6 thresholds, not to the model. For fine-tuning, the cheap probe is augmentation-only: mix game audio and simulated headset-mic response into the existing TTS corpus, fine-tune, and see whether the confusion pairs move at all before committing to recording real speakers.

---

## Sources

Papers
- https://arxiv.org/abs/2310.11230 — Yao et al., *Zipformer: A faster and better encoder for ASR*, ICLR 2024 (and https://ar5iv.labs.arxiv.org/html/2310.11230 for the results tables)
- https://arxiv.org/abs/2010.10759 — Shi et al., *Emformer*, ICASSP 2021
- https://arxiv.org/abs/2305.05084 — Rekesh et al., *Fast Conformer with Linearly Scalable Attention*, ASRU 2023
- https://arxiv.org/abs/2203.15455 — Zhang et al., *WeNet 2.0*, 2022 (and https://ar5iv.labs.arxiv.org/html/2203.15455)
- https://arxiv.org/abs/2101.06856 — Zhang, Sun & Ma, *Tiny Transducer*, ICASSP 2021
- https://arxiv.org/abs/2410.15608 — Jeffries et al., *Moonshine*, 2024
- https://arxiv.org/html/2509.02523v1 — King et al., *Flavors of Moonshine*, 2025
- https://arxiv.org/abs/2602.12241v1 and https://arxiv.org/html/2602.12241v1 — Kudlur et al., *Moonshine v2*, 2026
- https://arxiv.org/abs/2311.00430 — Gandhi, von Platen & Rush, *Distil-Whisper*, 2023
- https://arxiv.org/abs/2307.14743 — Macháček, Dabre & Bojar, *Turning Whisper into Real-Time Transcription System*, IJCNLP-AACL 2023
- https://arxiv.org/abs/2509.08753 — Zeghidour et al., *Streaming Sequence-to-Sequence Learning with Delayed Streams Modeling*, 2025
- https://arxiv.org/abs/2604.14493 and https://arxiv.org/html/2604.14493v2 — Banfic et al., *Pushing the Limits of On-Device Streaming ASR*, 2026
- https://arxiv.org/html/2406.07096 — Andrusenko et al., *Fast Context-Biasing for CTC and Transducer ASR models with CTC-based Word Spotter*, Interspeech 2024
- https://arxiv.org/html/2608.21343v1 — Bataev et al., *TurboBias 2.0*, 2026
- https://arxiv.org/abs/2312.09760 — Zhang et al., *U2-KWS*, ASRU 2023
- https://arxiv.org/html/2606.11279v1 — Barreiros et al., *Massive Open-Vocabulary Keyword Spotting*, 2026
- https://arxiv.org/abs/2212.08703 — Laptev & Ginsburg, *Fast Entropy-Based Methods of Word-Level Confidence Estimation*, SLT 2022
- https://arxiv.org/html/2509.07195v1 — Huo, Zhang & Tang, *Identifying and Calibrating Overconfidence in Noisy Speech Recognition*, 2025
- https://arxiv.org/abs/2311.18188 — Benazir, Xu & Lin, *Speech Understanding on Tiny Devices with A Learning Cache*, MobiSys 2024
- https://arxiv.org/abs/2601.13044 — Sirichotedumrong et al., *Typhoon ASR Real-time*, 2026
- https://www.arxiv.org/abs/2506.14434 — Sharma et al., *Unifying Streaming and Non-streaming Zipformer-based ASR*, ACL 2025 Industry
- https://arxiv.org/abs/2605.08195 — Nachin et al., *ExecuTorch — A Unified PyTorch Solution to Run AI Models On-Device*, 2026

Models, toolkits and documentation
- https://alphacephei.com/vosk/models — VOSK model list and WERs
- https://raw.githubusercontent.com/alphacep/vosk-api/master/src/vosk_api.h — VOSK C API header
- https://huggingface.co/alphacep — Alpha Cephei model org listing
- https://huggingface.co/alphacep/vosk-model-small-streaming-bn — VOSK Zipformer2 streaming model card
- https://github.com/k2-fsa/sherpa-onnx — sherpa-onnx (Apache-2.0)
- https://raw.githubusercontent.com/k2-fsa/sherpa-onnx/master/sherpa-onnx/c-api/c-api.h — sherpa-onnx C API
- https://raw.githubusercontent.com/k2-fsa/sherpa-onnx/master/python-api-examples/online-zipformer-ctc-hlg-decode-file.py — streaming CTC + HLG example
- https://k2-fsa.github.io/sherpa/onnx/index.html — sherpa-onnx feature overview
- https://k2-fsa.github.io/sherpa/onnx/pretrained_models/online-transducer/zipformer-transducer-models.html and the raw .rst — streaming transducer model list/sizes
- https://k2-fsa.github.io/sherpa/onnx/pretrained_models/online-ctc/zipformer-ctc-models.html — streaming CTC models, RTF figures
- https://k2-fsa.github.io/sherpa/onnx/kws/index.html and https://k2-fsa.github.io/sherpa/onnx/kws/pretrained_models/index.html — keyword spotting
- https://k2-fsa.github.io/sherpa/onnx/hotwords/index.html — hotwords / contextual biasing
- https://raw.githubusercontent.com/k2-fsa/icefall/master/egs/librispeech/ASR/RESULTS.md — streaming Zipformer WERs and parameter counts
- https://k2-fsa.github.io/icefall/recipes/Finetune/from_supervised/finetune_zipformer.html — Zipformer fine-tuning recipe
- https://huggingface.co/csukuangfj/sherpa-onnx-streaming-zipformer-en-20M-2023-02-17 — 20 M English streaming model
- https://huggingface.co/nvidia/nemotron-speech-streaming-en-0.6b — Nemotron Speech Streaming model card and licence
- https://huggingface.co/nvidia/parakeet-tdt-0.6b-v2 — Parakeet TDT
- https://github.com/ggml-org/whisper.cpp — whisper.cpp memory table, licence, stream example
- https://github.com/ggml-org/whisper.cpp/tree/master/examples/command — guided command mode
- https://github.com/ggml-org/whisper.cpp/issues/2159 — grammar constraints reported non-functional
- https://github.com/moonshine-ai/moonshine — Moonshine licence and platform support
- https://huggingface.co/moonshine-ai/moonshine — Moonshine v1 parameter counts
- https://github.com/handy-computer/transcribe.cpp/blob/main/docs/models/moonshine-streaming-small.md — Moonshine streaming small sizes and wall-clock
- https://github.com/snakers4/silero-vad — Silero VAD
- https://github.com/daanzu/kaldi-active-grammar — dynamic Kaldi grammars (AGPL-3.0)
- https://github.com/EitanWong/com.eitan.sherpa-onnx-unity — Apache-2.0 Unity package for sherpa-onnx
- https://github.com/Ponyu-dev/Unity-Sherpa-ONNX — Unity plugin, Android arm64 library sizes
- https://onnxruntime.ai/docs/execution-providers/Xnnpack-ExecutionProvider.html — XNNPACK EP
- https://onnxruntime.ai/docs/execution-providers/QNN-ExecutionProvider.html — QNN EP constraints
- https://github.com/microsoft/onnxruntime/discussions/29780 — measured Android arm64 int8 streaming ASR latencies (Galaxy S23 Ultra)
- https://github.com/pytorch/executorch — ExecuTorch
- https://www.uploadvr.com/snapdragon-xr2-gen-2/ — XR2 Gen 2 CPU/NPU specifications
- https://communityforums.atmeta.com/discussions/dev-quest/direct-access-to-quest-3s-neural-processing-unit-qualcomm-hexagon-processor-npu/1311021 — developer request for Quest NPU access (`UNVERIFIED` — not fetched; cited only as evidence that the request exists)
