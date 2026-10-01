# R2 — Contextual biasing, out-of-grammar / noise rejection, confidence & calibration, N-best / lattices, OOV

Research agent report, 2026-09-05. Scope: what modern (2020–2026) work on biasing, rejection, confidence and OOV offers a small on-device command recogniser, and what VoXR (VOSK/Kaldi small English model, grammar mode, Quest CPU) could actually adopt.

Pipeline-stage references are to §2 of `00-brief.md`. Failure references are to `KNOWN_LIMITATIONS.md`.

Every claim tagged `VERIFIED` was read from the cited source (paper abstract page, repository source file, or the local repo). `UNVERIFIED` means the item is real but a specific number in it was not read at source.

---

## Executive summary

- **The largest single finding is not academic: VoXR's own decoder already exposes five capabilities the bridge never calls, and one of them is silently swallowing the OOV failure.** `vosk_set_log_level(-1)` (`NativeBridge~/src/vosk_bridge.cpp:215`) suppresses the `KALDI_WARN "Ignoring word missing in vocabulary"` that `Recognizer::UpdateGrammarFst` emits for every grammar entry not in `graph/words.txt`. "cqb" is dropped from the grammar with no signal to anyone. `vosk_model_find_word()` is exported by the shipped `libvosk.so` and would turn that into an authoring-time Editor error. *(VERIFIED against vosk-api source and the shipped arm64 binary's export table.)*
- **Grammar entries in VOSK are *counts*, not a set.** `UpdateGrammarFst` feeds each JSON entry to Kaldi's `LanguageModelEstimator` (`ngram_order=2, discount=0.5`), whose `AddCounts` documents itself as "for each n-gram in the sentence, `count[n-gram] += 1`". Repeating an entry therefore raises its prior — and repeating `[unk]` raises the rejection prior. `GenerateGrammarJson` uses a `HashSet<string>`, so VoXR currently emits every entry exactly once and throws this lever away. This is the only *free* contextual-biasing mechanism available without touching the decoder. *(VERIFIED; magnitude of effect UNMEASURED.)*
- **VOSK's per-word `conf` cannot answer the question VoXR asks of it.** It is Kaldi MBR (`MinimumBayesRisk::GetOneBestConfidences`) computed over a lattice that is *already grammar-constrained*. It estimates "which in-grammar word was this", never "was this in-grammar at all". vosk-api issue #741 reports exactly VoXR's symptom from an unrelated user: out-of-grammar speech returns in-grammar words at confidence ≈1.0. `minConfidence` is therefore structurally incapable of rejecting noise, and "two" at 0.50 is a *within-grammar* ambiguity report, not an unreliability report.
- **The shipped model's decoder config throttles the lattice to near-uselessness.** `conf/model.conf` sets `--lattice-beam=2.0`, `--max-active=3000`, `--beam=10.0` against Kaldi/vosk V1 defaults of `6.0 / 7000 / 13.0`. Everything downstream of the lattice — MBR confidence quality, N-best diversity, any rescoring — is limited by that. `Model::ConfigureV2` registers `nnet3_decoding_config_`, `endpoint_config_` and `decodable_opts_` with the `ParseOptions` that reads `model.conf`, so these are shippable config changes with **zero code**. The same file also holds the endpointer rules (`--endpoint.rule2/3/4.min-trailing-silence = 0.5/0.75/1.0`) that cause the VAD-splitting limitation.
- **N-best is available today and is the single highest-value decoder change**: `vosk_recognizer_set_max_alternatives` is exported by the shipped arm64 `libvosk.so`. Feeding all N alternatives to `VoxrCommandParser` and keeping the best-scoring parse directly attacks the "switch **two** weapons" / "**fall** modes" / "**safe five**" substitutions, because the correct string is frequently alternative 2 or 3. **Cost:** in N-best mode VOSK emits `alternatives[].confidence` as an *unnormalised likelihood* (`-(Value1+Value2)`) and drops per-word `conf` entirely — `minConfidence` would have to be replaced by an N-best posterior margin.
- **Rejecting coughs and hums is not solvable inside the grammar decoder.** A grammar-constrained WFST must emit an in-vocabulary path; that is the mechanism, not a bug. The literature's answer since the 1990s is a *competing* model — filler/garbage, phone-loop, or background — scored in parallel and compared by likelihood ratio, and Kaldi ships the modern form of exactly that (`utils/lang/make_unk_lm.sh` builds a phone-level LM for `<unk>` instead of a single garbage phone). The cheap 2026 answer is a neural VAD in front: Silero VAD is ~2 MB, MIT, <1 ms per 30 ms chunk, and a published Android Vosk-grammar + Silero-v5 wake-word system measured 11% FP / 4% FN in TV + air-conditioning + conversation noise using it as the gate.
- **Neural contextual biasing (CLAS, TCPGen, trie/deep biasing, sherpa-onnx hotwords) reports large, consistent rare-word gains** — TCPGen cuts LibriSpeech rare-word error 15.6% → 8.3% (46.8% rel, test-clean, utterance-level lists); phoneme-aware TCPGen adds a further 15.0% → 12.1% R-WER on 100 h for +0.03 M parameters; CLAS reports up to 68% rel WER. **None of it works with a Kaldi WFST decoder.** All of it presupposes a transducer/AED and re-training, i.e. a decoder migration (sherpa-onnx + Zipformer, whose hotword path is an Aho-Corasick context graph over `modified_beam_search`).
- **The 2026 on-device baseline has moved.** A compact streaming English model at int4 k-quant reaches 8.20% average WER over eight benchmarks at 0.67 GB and 0.56 s algorithmic latency on CPU; int8 at 8.01% with a 48% size cut and RTFx > 6. VoXR's model is 40 MB at 9.85% LibriSpeech test-clean / 0.11×RT — still an excellent size/accuracy point, but it is 2020 technology and none of the biasing literature can reach it.
- **Confidence can be made informative without a new model, but not from `conf` alone.** The literature's shape is: a small learned confidence head over decoder-internal features (Qiu et al. ICASSP 2021; Li et al. ICASSP 2022) plus post-hoc calibration (Yu, Li & Deng, TASLP 2011). VoXR's tractable version is a *parser-side* calibrator: features it can already compute (N-best posterior margin, per-word MBR `conf`, VAD speechiness, match score, coverage) fitted against the 699-utterance corpus labels to produce one calibrated P(command correct). That is a logistic regression, not a neural net.
- **Nothing in this area fixes the phantom-command / leading-required-miss trade, the sibling-tie coin flip, or the grammar-rebuild audio gap** — those are parser and lifecycle problems. But a calibrated rejection score would let the leading-miss bar become *evidence-conditional* rather than positional, which is the only route back to the 9 genuine commands the bar costs per 699 utterances.

---

## Findings table

| # | Name | Key citation | What it does | Reported effect (numbers, dataset) | Needs which decoder | Stage (§2) | Expected gain for VoXR | Main risk |
|---|---|---|---|---|---|---|---|---|
| 1 | Grammar-entry repetition as a weight | Vosk `Recognizer::UpdateGrammarFst`; Kaldi `LanguageModelEstimator::AddCounts` | Duplicating a phrase in the grammar JSON raises its bigram counts → its LM prior | No published numbers; mechanism read from source | **VOSK as-is** | 4 | Bias toward whole command phrases and away from single short words; raise `[unk]` prior to increase rejection | Hard-backoff estimator: effect is non-linear and could over-bias one command; must be A/B'd on the 699-corpus |
| 2 | `[unk]` weighting as a rejection prior | Same as #1 + [Vosk grammar docs](https://alphacephei.com/vosk/adaptation) | `[unk]` is an ordinary grammar token; its count sets how readily the decoder escapes the grammar | Unmeasured | **VOSK as-is** | 4 | The only in-decoder lever on cough/hum false triggers | Too much `[unk]` destroys in-grammar recall; interacts with coverage (`[unk]` is coverage-exempt) |
| 3 | N-best decoding (`max_alternatives`) | Vosk `Recognizer::NbestResult`; exported by shipped `libvosk.so` | Emits N shortest lattice paths with an unnormalised likelihood each | Vosk issue #604: alternatives' `confidence` ranges ~50–>500, unnormalised | **VOSK as-is** (bridge change) | 3→6 | Directly attacks "switch two weapons", "fall modes", "safe five"; gives a hypothesis-level posterior margin | **Loses per-word `conf`** → `minConfidence` must be redesigned; N× parser cost |
| 4 | Widen `--lattice-beam` / `--max-active` in `conf/model.conf` | Vosk `Model::ConfigureV2` (`po.ReadConfigFile(.../conf/model.conf)`) | Richer lattice → better MBR posteriors and more diverse N-best | Model ships 2.0/3000/10.0 vs V1 defaults 6.0/7000/13.0 | **VOSK as-is (config only)** | 3 | Prerequisite for #3 and for any confidence work | CPU/latency cost on Quest; must be measured against the 72–90 fps budget |
| 5 | Endpointer rules in `conf/model.conf` | Same registration path (`endpoint_config_.Register(&po)`) | `--endpoint.rule2/3/4.min-trailing-silence` control the VAD split | Ships 0.5/0.75/1.0 s | **VOSK as-is (config only)** | 3→5 | Fewer mid-command splits → smaller `bufferWindow`, lower latency | Longer endpoints raise time-to-final; interacts with eager flush |
| 6 | In-place grammar swap (`vosk_recognizer_set_grm`) | Vosk `Recognizer::SetGrm`; exported by shipped `libvosk.so` | Replaces the grammar FST without freeing the model | Rebuilds `LookaheadComposeFst` + decoder + feature pipeline; model kept | **VOSK as-is** (undeclared in local header) | 4 | Removes the model-reload half of the ~50 ms audio gap | Refuses while `RECOGNIZER_RUNNING`; still resets `samples_processed_`; net saving unmeasured |
| 7 | Runtime custom lexicon (`vosk_recognizer_set_grm_with_lexicon`) | [vosk-api PR #1362](https://github.com/alphacep/vosk-api/pull/1362) (open) | Rebuilds HCLr from a caller-supplied pronunciation lexicon → genuinely new words | ~15 ms/10 words, 70 ms/100, 430 ms/500, 1500 ms/1000 | Needs a **patched libvosk** + model files | 3/4 | The real fix for `cqb`, `pdc` → `[unk]` | Unmerged; needs `phones.txt` + CD files the small model does not ship; would fork the prebuilt `.so` |
| 8 | Kaldi phone-level unknown-word LM | [`utils/lang/make_unk_lm.sh`](https://github.com/kaldi-asr/kaldi/blob/master/egs/wsj/s5/utils/lang/make_unk_lm.sh) | Replaces the single garbage phone for `<unk>` with a phone n-gram LM (pocolm, order ≤4) | No numbers in the script | **Kaldi graph rebuild** | 3 | A true garbage model → coughs decode as `[unk]`, not "on" | Requires rebuilding the model graph offline; ships a new 40 MB model |
| 9 | Silero VAD as a non-speech gate | [snakers4/silero-vad](https://github.com/snakers4/silero-vad) (MIT) | Neural speech/non-speech per 32 ms frame | ~2 MB; <1 ms per 30+ ms chunk, 1 CPU thread | Decoder-independent | 1/2 | Cheapest first line against coughs, hums, taps, game audio | Coughs *are* voiced; VAD alone will pass some. Needs measurement |
| 10 | Vosk grammar + Silero v5, deployed | [Zenn case study, 2025](https://zenn.dev/diced/articles/vosk-silero-vad-wakeword-android?locale=en) | Same architecture VoXR would build | Pixel 7 arm64: 0.8 ms/frame (2 threads CPU), 0.3 ms NNAPI; threshold 0.65 + 5 min speech frames → **11% FP / 4% FN** in TV+AC+conversation | **VOSK as-is** | 1/2 | Existence proof + tuned starting parameters | Japanese, single deployment, not a benchmark |
| 11 | Word-confusion-network / N-best SLU | [Villatoro-Tello et al. 2023](https://arxiv.org/html/2212.08489) | Feed the NLU a WCN instead of the 1-best | SLURP intent F1 **0.73 (1-best) → 0.77 (WCN)**; "5.5% relative improvement"; multimodal 0.82 | Any decoder producing lattices | 6 | Quantifies the ceiling of #3 for command parsing | Their NLU is neural; VoXR's parser is symbolic — gain may not transfer |
| 12 | N-best hypotheses for SLU | [Li et al. 2020](https://arxiv.org/abs/2001.05284) | Hypothesis-embedding models over ASR N-best for intent/slot | Abstract confirms method; numbers not on the abstract page (UNVERIFIED) | Any | 6 | Corroborates #11 | — |
| 13 | TCPGen (tree-constrained pointer generator) | [Sun, Zhang & Woodland, ASRU 2021](https://arxiv.org/abs/2109.00627) | Prefix tree of bias words + neural shortcut into the output distribution | LS-960 AED test-clean, utterance lists: 4.4%/15.6% → **3.0%/8.3%** (46.8% rel R-WER); book-level 4.4%/33.8% → 3.5%/19.4%; handles 5000 bias words | **Neural (AED/RNN-T)** | 3 | None without migration | Book-level lists "increased error rates in unbiased words" — over-biasing is real |
| 14 | Phoneme-aware prefix-tree biasing | [Futami et al. 2023](https://arxiv.org/html/2312.09582) | Adds G2P/EM phoneme alignment to TCPGen keys and queries | LS-100h 5.4%/15.0% → **4.9%/12.1%**; LS-960 2.3%/4.9% → 2.2%/4.6%; CSJ 13.5%/38.8% → **12.2%/30.6%**; +0.03 M params (~0.1%) | **Neural** | 3 | None without migration; but the *idea* (match on phones, not letters) transfers to #18 | — |
| 15 | CLAS (deep context) | [Pundak et al., SLT 2018](https://arxiv.org/abs/1808.02480) | Jointly trained bias-phrase embeddings + attention | "up to 68% relative WER" vs baseline (numbers UNVERIFIED beyond abstract) | **Neural (LAS)** | 3 | Canonical reference only | — |
| 16 | Trie-based deep biasing + shallow fusion | [Le et al., Interspeech 2021](https://arxiv.org/abs/2104.02194) | Streaming RNN-T with a bias trie and WFST shallow fusion | 19.5% rel WER over prior contextual biasing; 5.4–9.3% vs a strong hybrid baseline | **Neural (RNN-T)** | 3 | The closest neural analogue of VoXR's grammar | Over-biasing cost not stated in abstract |
| 17 | sherpa-onnx hotwords (context graph) | [sherpa-onnx hotwords docs](https://k2-fsa.github.io/sherpa/onnx/hotwords/index.html) | Aho-Corasick automaton over tokenised hotwords; per-token boost, cancelled on partial-match failure; `--hotwords-file`, `--hotwords-score`, `--modeling-unit`, `--bpe-vocab` | Qualitative examples only ("QUARTER"→"QUARTERS") | **Neural transducer, `modified_beam_search`** | 3/4 | The migration target if VoXR leaves VOSK | Whole-decoder replacement; ONNX Runtime on Quest; new model size/licence questions |
| 18 | Retraining-free CTC keyword biasing (WCTC-Biasing) | [Nakamura et al., Interspeech 2025](https://arxiv.org/abs/2506.01263) | Wildcard-CTC keyword spotting on intermediate layers, biasing later layers at inference | **+29% F1 on unknown words** (Japanese); no retraining | **Neural CTC** | 3 | Shows biasing without retraining is possible — but still needs a CTC model | Single language, F1 only |
| 19 | Streaming CTC word-spotting biasing | [Tsai et al. 2026](https://arxiv.org/abs/2605.18222) | Stateful token passing + incremental commitment for streaming biasing | "reduces overall WER and improves keyword F-score"; no numbers in abstract | **Neural CTC** | 3 | Streaming-shaped variant of #18 | Numbers UNVERIFIED |
| 20 | Multi-token-prediction biasing | [Selvakumar et al. 2025](https://arxiv.org/html/2512.17657) | Predict K future tokens, score bias entities against them; confidence threshold γ gates biasing | LS-960 test-clean **B-WER 8.70%, U-WER 2.07%** at λ=4.4, N=100 (50.34% rel B-WER improvement) | **Neural AED** | 3 | Its *gating* idea (disable biasing below a confidence threshold) is portable to any biasing scheme | — |
| 21 | Learned word-level confidence head | [Qiu et al., ICASSP 2021](https://arxiv.org/abs/2103.06716) | Self-attention confidence model over multiple hypotheses, avoids word-piece label noise | NCE / AUC / RMSE improvements on Voice Search + long-tail (values not on abstract page) | **Neural** for the paper; the *pattern* is decoder-agnostic | 6 | Blueprint for a VoXR confidence head over parser + N-best features | Needs labelled data; VoXR has 699 utterances + 73 field ones |
| 22 | Confidence estimation under domain mismatch | [Li et al., ICASSP 2022](https://arxiv.org/abs/2110.03327) | Pseudo-transcriptions + an out-of-domain LM to keep confidence calibrated off-domain | Improved confidence metrics on TED-LIUM & Switchboard from a LibriSpeech model | Neural | 6 | Names VoXR's exact hazard: a confidence fitted on TTS fixtures will not hold on Quest-mic speech | — |
| 23 | Post-hoc confidence calibration | [Yu, Li & Deng, IEEE TASLP 19(8):2461–2473, 2011](https://www.microsoft.com/en-us/research/publication/calibration-of-confidence-measures-in-speech-recognition/) | MaxEnt / ANN / DBN calibration applied *after* the recogniser, without touching it | "significant NCE increase and EER reduction" (exact table UNVERIFIED) | **Any** | 6 | Canonical justification for a VoXR-side calibrator | 2011; features are hand-designed |
| 24 | Overconfidence under noise | [Huo, Zhang & Tang 2025](https://arxiv.org/pdf/2509.07195) | Measures and temperature-scales ASR overconfidence in noise | Numbers not extractable from the PDF (UNVERIFIED) | Any | 6 | Supports the "confidence degrades in noise" premise | Preprint |
| 25 | Phoneme-guided zero-shot KWS | [Lee & Cho, Interspeech 2023](https://arxiv.org/abs/2308.16511) | Audio–phoneme matching for user-defined keywords, no per-keyword training | **67% rel EER / 80% rel AUC** improvement over baseline across familiar words, proper nouns, indistinguishable pronunciations | Own small model | 3 (parallel) | A phoneme-matching side-path could catch "cqb" where the grammar cannot | Extra model + G2P; EER still not zero |
| 26 | Universal phone recogniser (Allosaurus) | [Li et al., ICASSP 2020](https://github.com/xinjli/allosaurus) | Language-independent phone recognition, 2000+ languages | No on-device numbers | Own model | 3 (parallel) | Phone substrate for phonetic matching / a garbage path | Python/PyTorch; not an on-device artefact |
| 27 | G2P for authored aliases (`g2p_en`) | [Kyubyong/g2p](https://github.com/Kyubyong/g2p) — **Apache-2.0** | CMUdict lookup + numpy seq2seq fallback for OOV | No accuracy claims in README | Editor-time tool | 6 (authoring) | Auto-generate phonetic aliases ("see queue bee" → cqb) instead of hand-spelling them | Licence is fine; quality of generated *spelled* aliases unmeasured |
| 28 | Phonetisaurus WFST G2P | [AdolfVonKleist/Phonetisaurus](https://github.com/AdolfVonKleist/Phonetisaurus) | Joint n-gram WFST G2P; the tool Vosk's own docs name | Vosk docs: "fast training and almost accurate prediction" | Offline graph rebuild | 3 | Pairs with #7/#8 for a rebuilt lexicon | Licence UNVERIFIED |
| 29 | Graph-based phonetic ASR error correction (G-SPIN) | [Singh et al. 2026](https://arxiv.org/html/2606.24889v1) | GNN over phoneme-similarity graph proposes candidates; MLM + LLM rerank | English WER **0.60 → 0.32**; named-entity errors 0.51 → 0.19 (Loquacious-Set, synthetic noise) | Post-processing, any decoder | 6 | The *phonetic-neighbourhood constraint* is portable; the LLM reranker is not | LLM stage is impossible offline on Quest |
| 30 | Kaldi GrammarFst (dynamic sub-grammars) | [kaldi-asr.org/doc/grammar.html](https://kaldi-asr.org/doc/grammar.html) | Pre-compile sub-HCLGs, stitch at decode time via `#nonterm:` labels | Decoding overhead "about 15% with -O0 and 5% with -O2"; left-biphone models only | **Kaldi decoder change** (VOSK does not use it) | 3/4 | Would remove the grammar-rebuild audio gap outright | Not exposed by vosk-api; would mean forking libvosk |
| 31 | kaldi-active-grammar | [daanzu/kaldi-active-grammar](https://github.com/daanzu/kaldi-active-grammar) | Working GrammarFst system: per-utterance rule activation, dictation mixing, runtime word addition via `g2p_en` | ~1 GB model; no published latency figures | Kaldi fork | 3/4 | Proof the architecture works; **AGPL-3.0** | **Licence is incompatible with VoXR's Apache-2.0 brief** — read it for design, do not vendor it |
| 32 | Compact on-device streaming ASR, 2026 state of the art | [Banfic et al. 2026](https://arxiv.org/abs/2604.14493) | Quantisation study over 50+ streaming configs | int4 k-quant: 0.67 GB (from 2.47 GB), **8.20% avg WER** over 8 benchmarks, 0.56 s algorithmic latency, CPU-only; int8 8.01% vs fp32 8.03% at −48% size, RTFx > 6 | Neural | 3 | Calibrates what "migrate off VOSK" would cost in APK size | 0.67 GB is ~17× VoXR's current model |
| 33 | YAMNet audio-event classifier | [tensorflow/models AudioSet](https://github.com/tensorflow/models/tree/master/research/audioset/yamnet) | 521-class AudioSet event classifier (MobileNet-v1) | 3.7 M weights, 69.2 M multiplies per 960 ms frame | Independent | 1/2 | Could classify cough/throat-clearing explicitly rather than just "not speech" | 960 ms frames are too coarse for a 200 ms cough gate; heavier than Silero |
| 34 | Speech Commands "unknown"/"silence" classes | [Warden 2018](https://arxiv.org/pdf/1804.03209) | The canonical benchmark that makes rejection a first-class class | 12-class setup incl. `unknown` + `silence`; SOTA ~97.8% on v1 (UNVERIFIED) | Independent | — | The evaluation shape VoXR's fixture corpus lacks: it has no cough/hum/OOG negatives | — |
| 35 | Open-set / few-shot KWS with rejection | [Kim et al. 2022, dummy prototypical networks](https://arxiv.org/pdf/2206.13691) | Explicit "none of the above" prototypes | Numbers UNVERIFIED | Own model | 3 | Design pattern for "in-set vs out-of-set" as a learned decision | — |

---

## Detailed findings

### 1. VOSK grammar entries are n-gram *counts* — repetition is a weight, and `[unk]` is weightable — `VERIFIED`

**Source.** `Recognizer::UpdateGrammarFst`, https://github.com/alphacep/vosk-api/blob/master/src/recognizer.cc ; `LanguageModelEstimator`, https://github.com/kaldi-asr/kaldi/blob/master/src/chain/language-model.h

**Mechanism.** `vosk_recognizer_new_grm(model, rate, json)` does not build a hard grammar. It parses the JSON array, maps each space-separated token through `model_->word_syms_->Find(token)`, and calls `estimator.AddCounts(sentence)` on a Kaldi `LanguageModelEstimator` configured `opts.ngram_order = 2; opts.discount = 0.5;`. The estimated bigram FST is then lookahead-composed with `HCLr.fst`. Kaldi documents `AddCounts` as "Adds counts for this sentence. Basically does: for each n-gram in the sentence, `count[n-gram] += 1`." So the grammar is a *bag of observed sentences*, and multiplicity is meaningful: an entry listed three times contributes three times the counts. `[unk]` is an ordinary vocabulary token in this scheme, so its unigram prior — the decoder's willingness to leave the grammar — is set the same way.

**Evidence.** Read from source. No published measurement of the resulting cost deltas exists; the effect passes through a hard-backoff estimator with a 0.5 discount, so it is monotone but not proportional.

**VoXR mapping.** `VoxrCommandParser.GenerateGrammarJson` (`Runtime/Commands/VoxrCommandParser.cs` ~3076) accumulates into `HashSet<string> uniqueWords`, sorts, and emits each entry once. Changing that to a `Dictionary<string,int>` and emitting each entry `count` times is a ~20-line change with no native, API or asset-format impact. Two immediate uses: (a) weight the multi-word phrase runs above their constituent single words, sharpening the existing phrase-chunking bias that #45 introduced — targets "switch **two** weapons" and "**fall** modes"; (b) weight `[unk]` up — targets "cough, hum, and noise can trigger false matches".

**Feasibility on Quest.** Free. Grammar JSON grows linearly with the weights, and `UpdateGrammarFst` cost grows with total token count, which affects only the (already-paid) rebuild.

**Risk.** The two uses pull against each other: more `[unk]` mass costs in-grammar recall, and over-weighting one phrase can starve a sibling. The 699-utterance A/B rig is the correct instrument, and per the `grammar_ab_rig` memory a clean A/B is *not* on its own evidence that a scoring-adjacent change is safe.

**Open questions.** How many repetitions move the decision boundary at all? Does weighting `[unk]` produce `[unk]` for coughs, or merely for out-of-grammar *speech*?

---

### 2. Per-word `conf` is a grammar-conditioned MBR posterior — it cannot express "out of grammar" — `VERIFIED`

**Source.** `Recognizer::MbrResult`, https://github.com/alphacep/vosk-api/blob/master/src/recognizer.cc ; vosk-api issue #741 "Unusable 'Confidence'", https://github.com/alphacep/vosk-api/issues/741

**Mechanism.** With `max_alternatives == 0`, VOSK word-aligns the lattice (`WordAlignLattice` with `graph/phones/word_boundary.int`), constructs `kaldi::MinimumBayesRisk`, and writes `word["conf"] = conf[i]` from `mbr.GetOneBestConfidences()`. MBR confidences are word posteriors *normalised over the lattice*. In grammar mode the lattice contains only paths the grammar admits. The posterior therefore answers "given that the speaker said something this grammar can represent, which word was it" — a question whose answer is confidently wrong for a cough.

**Evidence.** Code above, plus an independent user report (#741): "When I say words that are not in that list the result returns one or more words from my list, but the confidence is almost always 1 (or very close to it)." That is `KNOWN_LIMITATIONS`' "cough, hum, and noise can trigger false matches" seen from outside VoXR.

The "two" ≈ 0.50 case is the same mechanism from the other side. In a grammar containing both "to" and "two" (VoXR's grammar contains both: "to" as a required literal, "two" from `VoxrNumberParser.DigitVocabulary`), the lattice keeps both arcs, the MBR sausage splits mass ~evenly, and 0.50 is the *correct* posterior. It is a well-calibrated statement about a genuine two-way ambiguity, not a defect — which is why `minConfidence` cannot be raised past it and why no tuning fixes it.

**VoXR mapping.** Two consequences worth writing into `Documentation~/scoring.md`: (i) `minConfidence` filters *within-grammar ambiguity*, never out-of-grammar speech, so it can never be the noise gate the KNOWN_LIMITATIONS workaround suggests; (ii) 0.50 on "two" is informative — it says "this token is a coin flip between two grammar words". The parser could exploit that: a matched literal whose confidence is ≈0.5 and whose homophone is also in the grammar is exactly the sibling-tie situation, detectable *from the confidence* rather than from pattern structure.

**Feasibility.** Documentation + parser logic only. Free.

**Risk.** Reading semantics into 0.50 is an inference from the MBR mechanism, not a measurement. It needs a red-then-green pin: force a grammar containing "two" but not "to" and check whether the confidence rises.

**Open questions.** Does removing "to" from the grammar raise "two"'s confidence? (Directly testable with the A/B rig and `DocCheck`.)

---

### 3. N-best decoding is available in the shipped binary and is the highest-value unused capability — `VERIFIED`

**Source.** `Recognizer::NbestResult` and `Recognizer::GetResult`, https://github.com/alphacep/vosk-api/blob/master/src/recognizer.cc ; export table of `Runtime/Plugins/Android/arm64-v8a/libvosk.so` (local, `strings | grep ^vosk_`).

**Mechanism.** `vosk_recognizer_set_max_alternatives(r, N)` switches `GetResult()` from `MbrResult` to `NbestResult`, which does `fst::ShortestPath(lat, &nbest_lat, N)`, determinises each path, word-aligns it, and emits `{"alternatives":[{"text":…, "confidence":<likelihood>, "result":[{word,start,end}…]}…]}`. `confidence` is `-(weight.Weight().Value1() + weight.Weight().Value2())` — a raw graph+acoustic log-likelihood, unnormalised (vosk-api issue #604 reports observed values ~50 to >500).

**Evidence.** Source read. The clean, *published* evidence that alternatives help a downstream semantic parser is Villatoro-Tello et al. 2023: SLURP intent F1 0.73 (1-best) → 0.77 (word confusion network), "5.5% relative improvement". Le et al. and the whole biasing literature rest on the same premise — the right answer is usually *in* the lattice.

**VoXR mapping.** This is the direct attack on three `KNOWN_LIMITATIONS` entries at once: "to"→"two", "all"→"fall", "cease fire"→"safe five". Change shape: `vosk_bridge.cpp` calls `set_max_alternatives(N)` after each recognizer construction; the JSON parser learns the `alternatives` array; `UtteranceBuffer`/`VoxrCommandRecogniser` run `VoxrCommandParser.Parse` over each alternative and keep the best-scoring result, with the alternative index and its likelihood recorded in the session log. Because the parser is deterministic and already scores candidates, this composes with everything downstream (the bar, coverage, sibling ties) rather than replacing it.

Two derived quantities become available and are *better* confidence signals than `conf`:
- **posterior margin** — `softmax` over the alternatives' likelihoods gives a proper hypothesis-level posterior; the gap between the best-parsing alternative and the next is a calibrated-shaped "how sure am I this is the command".
- **parse agreement** — if all N alternatives parse to the same intent, that intent is robust; if they parse to different intents, that is precisely a sibling tie discovered acoustically.

**Feasibility on Quest.** Lattice already exists; `ShortestPath` over N=3–5 is cheap relative to decoding. Parsing N transcripts multiplies parser cost by N — the parser is string-level and runs once per final result, so this is bounded and off the render thread. The real cost is #4 (a wider lattice beam) which N-best needs to be worth anything at `--lattice-beam=2.0`.

**Risk — this one is load-bearing.** In N-best mode **VOSK does not emit `conf` at all** (`NbestResult` writes only `word`, `start`, `end`). `VoxrCommand.Confidence` and the entire `minConfidence` gate would go to the `-1` "no data" path. Any adoption must replace `minConfidence` with an N-best-derived score *in the same change*, or every command silently bypasses the confidence gate.

**Open questions.** Is the correct string actually present in the top-3 for VoXR's measured substitutions? This is answerable offline today with the A/B rig on the 699-utterance corpus at zero device cost — see recommendation 1.

---

### 4. `conf/model.conf` is a shipped, editable decoder config — and it currently throttles the lattice and sets the endpointer — `VERIFIED`

**Source.** `Model::ConfigureV2`, https://github.com/alphacep/vosk-api/blob/master/src/model.cc ; local `NativeBridge~/vendor/vosk-model-small-en-us-0.15/conf/model.conf`.

**Mechanism.** `ConfigureV2` registers `nnet3_decoding_config_`, `endpoint_config_` and `decodable_opts_` with a `ParseOptions` and then calls `po.ReadConfigFile(model_path + "/conf/model.conf")`. Every `--flag` in that file therefore reaches the decoder. The shipped file reads:

```
--min-active=200          --max-active=3000        --beam=10.0
--lattice-beam=2.0        --acoustic-scale=1.0     --frame-subsampling-factor=3
--endpoint.silence-phones=1:2:3:4:5:6:7:8:9:10
--endpoint.rule2.min-trailing-silence=0.5
--endpoint.rule3.min-trailing-silence=0.75
--endpoint.rule4.min-trailing-silence=1.0
```

against the values vosk's own `ConfigureV1` hardcodes for older models: `--beam=13.0 --max-active=7000 --lattice-beam=6.0`.

**Evidence.** Source + local file. The `--lattice-beam=2.0` figure is a third of vosk's own default and is the binding constraint on lattice density, hence on MBR posterior quality, N-best diversity, and any rescoring.

**VoXR mapping.** Two independent, code-free levers:
- **Lattice width** (`--lattice-beam`, `--max-active`): prerequisite for findings 2 and 3. Raising it should make `conf` more informative *and* make the alternatives list contain real competitors rather than near-duplicates.
- **Endpointing** (`--endpoint.rule*.min-trailing-silence`): this is the mechanism behind "VOSK's VAD splits mid-command pauses" and behind the `bufferWindow` ≈ 2.0 s recommendation on Quest 3. Raising `rule2`/`rule3` trades time-to-final against split commands, and is *tunable per shipped model* without a bridge change. (`vosk_recognizer_set_endpointer_delays` exists in newer vosk-api but is **not** exported by VoXR's arm64 `libvosk.so` — verified against the export table — so `model.conf` is the only route.)

**Feasibility.** Edit a text file in the shipped model directory. Rebuild nothing. Measurable entirely on the WAV-replay corpus.

**Risk.** Both levers cost CPU (wider beams) or latency (longer endpoints), against a Quest budget of a fraction of one core at 72–90 fps. Neither has been measured on device. Widening the lattice also widens the set of in-grammar words a cough can be mapped onto, so #3 and #1(b) should be evaluated together, not independently.

**Open questions.** What is the RTF cost of `--lattice-beam=6.0 --max-active=7000` on Quest 3? (Human-only measurement; the WSL harness gives a desktop lower bound.)

---

### 5. `vosk_recognizer_set_grm` — an in-place grammar swap the bridge does not use — `VERIFIED`

**Source.** `Recognizer::SetGrm`, https://github.com/alphacep/vosk-api/blob/master/src/recognizer.cc ; symbol present in `Runtime/Plugins/Android/arm64-v8a/libvosk.so`; **absent from `NativeBridge~/include/vosk_api.h`** (present in `NativeBridge~/vendor/vosk-linux-x86_64-0.3.45/vosk_api.h:155`).

**Mechanism.** `SetGrm` refuses while `state_ == RECOGNIZER_RUNNING`, then deletes `decode_fst_`, re-runs `UpdateGrammarFst` (or restores the model's own `Gr.fst` for `"[]"`), and rebuilds the decoder, feature pipeline and silence weighting — **keeping the loaded model**. `vosk_bridge_set_grammar` in `NativeBridge~/src/vosk_bridge.cpp:346` instead does `vosk_recognizer_free` + `vosk_recognizer_new_grm`, which is the same work plus a recognizer teardown.

**Evidence.** Source read; symbol confirmed exported by the shipped binary. The saving is the recognizer alloc/free, not the FST composition — `UpdateGrammarFst` and the `LookaheadComposeFst` dominate and happen either way.

**VoXR mapping.** The ~50 ms audio gap (`SetActiveSets` → `RebuildParserAndGrammar`) is a *composite*: stop AudioCapture + grammar rebuild + restart AudioCapture. `set_grm` shortens only the middle term. The larger win is architectural: `SetGrm` does not require the capture thread to stop *for model reasons*, so the bridge could keep `AudioRecord` running and buffer into the ring while the recognizer is swapped, losing nothing at the audio layer. That is where the dropped "fall back from" words actually go.

**Feasibility.** Declare it in `include/vosk_api.h`, call it, keep capture running. Bridge-only change → **on-device human verification required** per the project's verification bindings, and the Editor path (`EditorMicBackend`) does not exercise it.

**Risk.** `SetGrm` resets `samples_processed_`/`frame_offset_`; any partially decoded utterance is discarded, which is the current behaviour too. The `RECOGNIZER_RUNNING` refusal means the swap must be sequenced against `AcceptWaveform` on the recognition thread — a real concurrency hazard in `vosk_bridge.cpp`'s file-scope state.

**Open questions.** What fraction of the measured ~50 ms is recognizer construction vs FST composition vs capture stop/start? Measurable on the desktop harness.

---

### 6. Runtime custom lexicon (`vosk_recognizer_set_grm_with_lexicon`) — the only real fix for `cqb` → `[unk]` — `VERIFIED` (as an open PR)

**Source.** https://github.com/alphacep/vosk-api/pull/1362 (open, unmerged as of the July 2025 review request on the thread).

**Mechanism.** Adds an API that takes a grammar *and* a pronunciation lexicon, and **recreates the HCLr transducer at runtime** from that lexicon, so words absent from `graph/words.txt` become decodable. The contributor reports rebuild costs of ~15 ms for 10 words, 70 ms for 100, 430 ms for 500, and ~1500 ms for 1000. Requires lookahead models that ship context-dependency files and a phone symbol table (`phones.txt`), and requires the lexicon to include `<eps>`.

**Evidence.** PR page read. Costs are the contributor's own measurements on unstated hardware — treat as order-of-magnitude.

**VoXR mapping.** This is the mechanism `KNOWN_LIMITATIONS`' "Abbreviations and letter sequences map to `[unk]`" entry currently answers with "spell it phonetically by hand". A developer would author `cqb` with a pronunciation (`K IY UW B IY` or similar, generated by `g2p_en` in the Editor) and it would decode as `cqb`.

**Feasibility on Quest — poor today, and the blocker is the model, not the CPU.** VoXR's vendored `vosk-model-small-en-us-0.15` ships only `graph/{Gr.fst, HCLr.fst, disambig_tid.int, phones/word_boundary.int}` — **no `phones.txt`, no tree/context-dependency files**, so the PR's requirements are not met by the shipped model. (Also worth checking in the repo: the vendored copy appears to be missing `graph/words.txt` entirely, which `Model::ReadDataFiles` loads into `word_syms_` and which grammar mode requires; if that is a genuine gap rather than an artefact, it is a packaging bug.) Adopting this would mean shipping a different/repackaged model **and** forking the prebuilt `libvosk.so` for arm64 — which the project already knows is risky (`accept_waveform_f` is broken in the prebuilt).

**Risk.** Unmerged upstream; a fork of `libvosk.so` is a permanent maintenance liability for a package that currently consumes a vendor binary. The 430 ms cost at 500 words also makes it unusable for per-frame dynamic slots.

**Open questions.** Does Alphacephei ship (or will they release) a small English model with the CD files? Vosk's own adaptation docs say some models "don't have required files, you need to contact Alphacephei to get access to them."

---

### 7. The `[unk]` token is only as good as the model's unknown-word model — and Kaldi's better one is `make_unk_lm.sh` — `VERIFIED`

**Source.** https://github.com/kaldi-asr/kaldi/blob/master/egs/wsj/s5/utils/lang/make_unk_lm.sh ; `prepare_lang.sh --unk-fst`.

**Mechanism.** In a default Kaldi lexicon, `<unk>` has a pronunciation consisting of one designated garbage phone (SPN). `make_unk_lm.sh` replaces that with a *phone-level language model* trained on the phone sequences of the dictionary's own entries (pocolm, order configurable 2–7, default 4; bigram-constrained transitions; prunable), and `apply_unk_lm.sh` splices it into `L.fst`. The Kaldi docs state the `--unk-fst` option is "more useful for test-time than train-time" — which is exactly VoXR's use.

**Evidence.** Script header read. This is the modern, in-toolkit form of the classic keyword/filler and phone-loop rejection architecture that the utterance-verification literature has used since the 1990s: a competing model that is *not* constrained by the lexicon, so anything unlexical scores better under it than under any word.

**VoXR mapping.** A single garbage phone is a very cheap path — it can be traversed in one frame — which is a plausible mechanical reason why short in-grammar words ("on", "from", "four") beat `[unk]` on a 200 ms cough rather than losing to it. A phone-loop `<unk>` gives noise somewhere realistic to go. It addresses both "cough, hum, and noise can trigger false matches" and "Set restriction cannot produce meaningful rejection transcripts" — a phone-level `<unk>` can in principle expose the phone sequence, which is the raw material for telling the player "you said something like /əˈproʊtʃ/, which isn't a weapons command".

**Feasibility on Quest.** The decode cost is small. The *build* cost is the problem: this requires rebuilding the model's `L.fst` and `HCLr.fst` offline with Kaldi + pocolm, then shipping a new ~40 MB model. There is no runtime route.

**Risk.** Rebuilding the graph of a model VoXR did not train, without the training recipe, is a serious undertaking and would fork the model asset. A too-permissive `<unk>` LM will start winning against genuine commands.

**Open questions.** What does `[unk]` map to in `vosk-model-small-en-us-0.15` specifically — single SPN or a phone LM? Not determinable from the shipped files without `words.txt`/`phones.txt`; would need `HCLr.fst` inspection with OpenFst.

---

### 8. Neural VAD as the first line against coughs — cheap, deployed, and measured on Android — `VERIFIED`

**Sources.** https://github.com/snakers4/silero-vad (MIT) ; https://zenn.dev/diced/articles/vosk-silero-vad-wakeword-android?locale=en

**Mechanism.** A ~2 MB neural speech/non-speech classifier run per 32 ms frame (512 samples @ 16 kHz) ahead of the recogniser. Frames the VAD calls non-speech are never fed to VOSK, so the grammar decoder is never asked to explain them and cannot produce an in-grammar word for them. The Zenn deployment is architecturally identical to what VoXR would build: 16 kHz PCM fanned out to Silero on one thread and grammar-mode Vosk on another, with `[unk]` in the grammar so the WFST always has an escape path.

**Evidence.** Silero README: ~2 MB JIT; "one audio chunk (30+ ms) takes less than 1ms to be processed on a single CPU thread"; MIT licence; ONNX build available. Zenn, on a Pixel 7 (arm64-v8a): **0.8 ms/frame on 2 CPU threads, 0.3 ms with NNAPI**; tuned to `threshold=0.65, minSpeechFrames=5`, giving **11% false positive / 4% false negative** in a TV + air-conditioning + conversation condition. Model sizes there: Vosk small ~50 MB + Silero ~2 MB.

**VoXR mapping.** Insert between §2.2 (DSP) and §2.3 (decoder), after the 16 kHz downsample and before `accept_waveform_s`. It changes nothing in the parser and nothing in the API. It targets exactly one `KNOWN_LIMITATIONS` entry — "Cough, hum, and noise can trigger false matches in grammar mode" — whose current workaround is "use push-to-talk", i.e. a UX concession.

**Feasibility on Quest.** 0.8 ms per 32 ms frame is ~2.5% of one core at Pixel-7-class performance; XR2 Gen 2 is in the same family. ~2 MB APK. Requires an ONNX Runtime (or a hand-written inference for this specific small net) in the native bridge — a real but bounded piece of work, and an added third-party dependency with its own Android arm64 binary.

**Risk.** **A cough is voiced.** VAD is trained to detect *speech*, and there is no published number for Silero's cough rejection specifically. It will certainly kill breathing, taps and room tone; it may pass coughs and throat-clearing straight through to the decoder. Also: gating audio changes what the endpointer sees, so `bufferWindow` behaviour will move.

**Open questions.** What is Silero's actual accept rate on coughs? Answerable cheaply and offline: record 30 coughs, run them through the existing WAV-replay harness with a Silero stage, count. That is a better first experiment than any device test.

---

### 9. TCPGen and phoneme-aware prefix-tree biasing — the accuracy ceiling this area offers, behind a decoder migration — `VERIFIED`

**Sources.** Sun, Zhang & Woodland, "Tree-constrained Pointer Generator for End-to-end Contextual Speech Recognition", ASRU 2021, https://arxiv.org/abs/2109.00627 (numbers from https://ar5iv.labs.arxiv.org/html/2109.00627). Futami, Tsunoo, Kashiwagi, Ogawa, Arora, Watanabe, "Phoneme-Aware Encoding for Prefix-Tree-Based Contextual ASR", arXiv:2312.09582, Dec 2023.

**Mechanism.** TCPGen structures the bias list (command words, slot values, target callsigns) as a prefix tree and adds a neural shortcut from that tree into the model's output distribution, so at each step the model can copy from the tree instead of generating from the vocabulary. It is neural-symbolic: the tree is symbolic and swappable at inference; the shortcut is trained. The phoneme-aware extension aligns phonemes to subwords (via G2P attention or EM) and adds phoneme-informed keys and queries, so words whose *spelling* is unusual but whose *pronunciation* is predictable are still reachable.

**Evidence (numbers).** TCPGen, AED, LibriSpeech-960, test-clean: baseline 4.4% WER / 15.6% R-WER → TCPGen+shallow-fusion **3.0% / 8.3%** (46.8% relative R-WER reduction) with utterance-level lists; chapter-level 4.4/15.1 → 3.1/8.7; book-level 4.4/33.8 → 3.5/19.4. RNN-T, test-clean, utterance-level: 5.5/18.7 → 3.8/11.3. Handles 5000 bias words with small overhead. Phoneme-aware: LS-100h 5.4/15.0 → **4.9/12.1**; LS-960 2.3/4.9 → 2.2/4.6; CSJ 13.5/38.8 → **12.2/30.6**, for **+0.03 M parameters (~0.1% of model size)**.

**VoXR mapping.** Conceptually this is what VoXR's grammar *is* — a swappable symbolic list of what the speaker might say — done in a model that can also decode freely. It would address the substitution failures and the OOV failure simultaneously, and it would remove the audio gap entirely (swapping a bias list is a data change, not a graph rebuild). **But it requires an AED or transducer decoder and a trained biasing component.** With VOSK/Kaldi there is no adoption path at all; the honest mapping is "this is the argument for migrating to sherpa-onnx + a Zipformer transducer, whose `modified_beam_search` hotword path is the deployed, no-training version of the same idea".

**Feasibility on Quest.** Not with the current stack. With sherpa-onnx: streaming Zipformer models in the 60–100 MB class exist; the 2026 compact-streaming study puts a *high-accuracy* CPU-only streaming model at 0.67 GB int4, which is 17× VoXR's current asset. There is a real size/accuracy frontier to survey before committing.

**Risk.** TCPGen's own paper reports that book-level (i.e. large) bias lists "increased error rates in unbiased words" — over-biasing is a measured cost, not a theoretical one. That maps onto VoXR's "all"→"fall" failure exactly: making the grammar prefer frequent grammar words *is* biasing, and it is already hurting.

**Open questions.** Does a Zipformer streaming transducer at ~60–100 MB with hotwords beat `vosk-model-small-en-us-0.15` + grammar on VoXR's own 699-utterance corpus? That is a bounded, offline, WSL-runnable experiment and it would settle the whole "migrate or not" question.

---

### 10. Retraining-free biasing — WCTC-Biasing and streaming CTC word spotting — `VERIFIED`

**Sources.** Nakamura et al., "WCTC-Biasing: Retraining-free Contextual Biasing ASR with Wildcard CTC-based Keyword Spotting and Inter-layer Biasing", Interspeech 2025, https://arxiv.org/abs/2506.01263. Tsai, Lo, Sun & Chen, "Contextual Biasing for Streaming ASR via CTC-based Word Spotting", arXiv:2605.18222, May 2026.

**Mechanism.** WCTC-Biasing spots keywords from intermediate-layer acoustic features using *wildcard* CTC (tolerant of unmatched segments), then biases the later layers of the same forward pass toward those keywords. No retraining, no TTS, no bias encoder. The 2026 streaming variant adds a stateful token-passing algorithm so keyword state survives across audio segments, plus an incremental commitment mechanism for low-latency output, again without touching the acoustic model.

**Evidence.** WCTC-Biasing: **29% improvement in F1 score for unknown words** on Japanese speech recognition. The streaming paper states WER reduction and keyword F-score improvement without publishing numbers in its abstract (`UNVERIFIED` for magnitudes).

**VoXR mapping.** This is the strongest evidence that biasing does **not** require retraining — which matters because VoXR cannot train anything. It still requires a CTC model with accessible intermediate layers, so it is not applicable to VOSK; it applies to a hypothetical sherpa-onnx CTC/Zipformer path.

**Feasibility on Quest.** Only after migration. The method's cost is an extra CTC decode over intermediate features — cheap relative to the encoder.

**Risk.** Japanese-only evaluation for WCTC-Biasing; F1 on unknown words says nothing about false triggers on noise.

**Open questions.** Does the intermediate-layer spotting survive int8 quantisation?

---

### 11. Learned confidence and post-hoc calibration — the tractable version for VoXR is a logistic regression over features it already has — `VERIFIED` (papers), `UNVERIFIED` (some magnitudes)

**Sources.** Qiu, Li, He, Zhang, Li, Cao, Prabhavalkar, Bhatia, Li, Hu, Sainath, McGraw, "Learning Word-Level Confidence for Subword End-to-End ASR", ICASSP 2021, https://arxiv.org/abs/2103.06716. Li, Zhang, Qiu, He, Cao, Woodland, "Improving Confidence Estimation on Out-of-Domain Data for End-to-End Speech Recognition", ICASSP 2022, https://arxiv.org/abs/2110.03327. Yu, Li & Deng, "Calibration of Confidence Measures in Speech Recognition", IEEE TASLP 19(8):2461–2473, 2011.

**Mechanism.** The consistent finding across this line is that a recogniser's *native* score is a poor confidence, and that a **separate, small, post-hoc estimator** trained on correctness labels beats it — evaluated by NCE, AUC and EER rather than accuracy. Qiu et al. use self-attention over multiple hypotheses to produce word-level confidence directly (avoiding word-piece label noise), and note it enables hybrid on-device/server model selection. Li et al. show these estimators degrade under acoustic/linguistic mismatch and repair that with pseudo-transcriptions and an out-of-domain LM. Yu, Li & Deng establish the older, decoder-agnostic form: calibration is "a special adaptation technique applied to confidence measures" applied *without modifying the recognition engine*, using MaxEnt / ANN / DBN over generic confidence plus application-dependent features.

**Evidence.** Qiu et al. report NCE/AUC/RMSE improvements on Voice Search and long-tail sets (exact table not read → `UNVERIFIED`). Li et al. report improved confidence metrics on TED-LIUM and Switchboard from a LibriSpeech-trained model while preserving in-domain performance. Yu et al. report "significant NCE increase and EER reduction" (table `UNVERIFIED` — the MSR PDF did not parse).

**VoXR mapping.** VoXR should not build a neural confidence model. It should build the 2011-shaped thing: one scalar `P(command is correct)` from a small model over features it already computes or can cheaply obtain —

| feature | source | already available? |
|---|---|---|
| min / mean per-word MBR `conf` over the span | VOSK `result[].conf` | yes (MBR mode) |
| N-best posterior margin, parse agreement across alternatives | finding 3 | no — needs `max_alternatives` |
| match score, coverage charge, admission margin | `VoxrCommandParser` | yes |
| leading-required-miss flag, sibling-tie flag | parser | yes |
| span duration, words/second, silence before/after | `start`/`end` | yes |
| VAD speechiness over the span | finding 8 | no — needs Silero |
| utterance length vs pattern length | parser | yes |

Fitted on the 699-utterance corpus plus the 73 field utterances (with an honest train/test split), this yields a *calibrated* number that `minScore` and `minConfidence` currently approximate with two uncalibrated thresholds. It is directly what "flat 0.50 confidence on two" needs: not a better `conf`, but a decision function that knows 0.50-on-"two" is normal and 0.50-on-"heading" is not.

**Feasibility on Quest.** A logistic regression is ~30 floats. Free at runtime. The work is in the labelling and the fitting harness, both of which are Editor/desktop.

**Risk — and it is the one Li et al. 2022 exists to name.** A calibrator fitted on TTS fixtures will be **mis**calibrated on Quest-mic human speech. The 699 corpus is synthesised; the field set is 73 utterances from one speaker. That is enough to fit ~6 features, not 20, and it will not transfer to other speakers. This must ship as a *reported* score alongside the existing gates, not as a replacement for them, until there is field evidence.

**Open questions.** Is there enough label diversity in 699 TTS + 73 field utterances to fit anything that generalises? Probably only if the feature count is kept very small (3–5).

---

### 12. Phonetic matching as a substrate — where the OOV and homophone failures actually live — `VERIFIED` (components), `UNVERIFIED` (that it helps VoXR)

**Sources.** Lee & Cho, "PhonMatchNet: Phoneme-Guided Zero-Shot Keyword Spotting for User-Defined Keywords", Interspeech 2023, https://arxiv.org/abs/2308.16511. Li et al., Allosaurus, https://github.com/xinjli/allosaurus. `g2p_en` (Apache-2.0), https://github.com/Kyubyong/g2p. Phonetisaurus, https://github.com/AdolfVonKleist/Phonetisaurus. Singh et al., "Graph-Based Phonetic Error Correction of Noisy ASR" (G-SPIN), arXiv:2606.24889, 2026.

**Mechanism.** Three distinct uses of phones, only two of which are feasible here:
1. **Authoring-time G2P.** `g2p_en` (CMUdict lookup + a numpy seq2seq fallback for OOV, Apache-2.0) can generate a pronunciation for "cqb" in the Unity Editor, from which VoXR can *auto-generate the spelled alias* ("see queue bee") that `KNOWN_LIMITATIONS` currently tells developers to write by hand, and can *validate* every authored literal against the model vocabulary.
2. **Phonetic distance in the parser.** Every one of VoXR's substitution failures is a near-homophone pair: to/two, all/fall, cease-fire/safe-five, a/on. A phone-string edit distance between the decoded token and the expected literal would recognise "two" as a plausible realisation of "to" at near-zero cost, entirely in C#, with a pronunciation table baked at build time from the authored vocabulary. G-SPIN's contribution is the *discipline*: constrain candidates to the phonetic neighbourhood first, then decide semantically — its LLM reranker is unusable offline, but the constraint half is exactly this.
3. **A parallel phone recogniser** (PhonMatchNet-style matching, Allosaurus-style phone output) as a competitor path to the grammar. PhonMatchNet reports **67% relative EER and 80% relative AUC improvement** over its baseline for user-defined keywords including proper nouns and "indistinguishable pronunciations". This is the most principled answer to both OOV and rejection, and the least feasible: it is a second model, a second inference budget, and no on-device artefact exists.

**Evidence.** Cited above. Note what is *not* evidenced: nobody has measured phonetic-distance matching against VoXR's corpus, and it interacts dangerously with the parser's existing scoring — a phonetic match is a *softer* match and would need its own miss cost, or it will resurrect the phantom-command problem the leading-required-miss bar was built to kill.

**VoXR mapping.** Use 1 is a pure Editor feature with no runtime risk and addresses the OOV entry's workaround directly. Use 2 is a parser change (`Runtime/Commands/VoxrCommandParser.cs`) and must be gated behind the existing bar and admission rules. Use 3 is out of reach.

**Feasibility on Quest.** Use 1: zero runtime cost. Use 2: a CMU-phone table for the authored vocabulary is a few KB; edit distance over ≤10 phones per token is trivial. Use 3: not feasible.

**Risk.** Use 2 makes the grammar *more* permissive at exactly the point where VoXR's history says permissiveness produced phantom commands (#124). Any phonetic match must be scored strictly below an exact match, and it must not be able to satisfy a pattern's first required element on its own.

**Open questions.** On the 699-utterance corpus, how many currently-failing rows would a phone-edit-distance ≤1 substitution recover, and how many *invented commands* would it create? That ratio — recoveries vs inventions — is the whole decision, and it is measurable offline with the existing rig.

---

## What VOSK already offers and VoXR leaves unused

Verified against vosk-api source (`master`), the vendored 0.3.45 header, the shipped `Runtime/Plugins/Android/arm64-v8a/libvosk.so` export table, and the vendored model directory.

**C API exported by the shipped arm64 `libvosk.so`, never called by `NativeBridge~/src/vosk_bridge.cpp`:**

| Symbol | What it gives | Notes |
|---|---|---|
| `vosk_recognizer_set_max_alternatives` | N-best list with per-hypothesis likelihood (`Recognizer::NbestResult`) | **Loses per-word `conf`** — `MbrResult` runs only when `max_alternatives == 0` |
| `vosk_recognizer_set_grm` | Replace the grammar without freeing the model (`Recognizer::SetGrm`) | **Not declared in `NativeBridge~/include/vosk_api.h`** although present in `vendor/vosk-linux-x86_64-0.3.45/vosk_api.h:155`. Refuses while `RECOGNIZER_RUNNING` |
| `vosk_model_find_word` | `word_syms_->Find(word)`, −1 if absent | The authoring-time OOV check VoXR needs for "cqb" |
| `vosk_recognizer_set_partial_words` | Word/timing detail on *partials*, not just finals | Would let the eager-flush path see per-word timing before the final |
| `vosk_recognizer_set_nlsml` | NLSML XML output | Same likelihoods as N-best, XML-wrapped; no added information |
| `vosk_recognizer_set_spk_model` / `vosk_recognizer_new_spk` | x-vector speaker embedding per utterance (`GetSpkVector`) | Would allow "only the wearer's voice commands the ship" — needs a separate `spk` model asset |

**Present in `NativeBridge~/include/vosk_api.h` but NOT exported by the shipped binary** — the local header is ahead of the binary, which is a latent trap:

- `vosk_recognizer_set_endpointer_mode`
- `vosk_recognizer_set_endpointer_delays`

Calling either would fail to resolve at load/link time on device. Endpointing must be tuned through `conf/model.conf` instead.

**Model-directory files and what they control** (`NativeBridge~/vendor/vosk-model-small-en-us-0.15/`):

| File | Loaded as (`src/model.cc`) | VoXR's use |
|---|---|---|
| `conf/model.conf` | read by `po.ReadConfigFile` into `nnet3_decoding_config_`, `endpoint_config_`, `decodable_opts_` | **Unused as a lever.** Ships `--lattice-beam=2.0`, `--max-active=3000`, `--beam=10.0` (vs vosk V1 defaults 6.0/7000/13.0) and `--endpoint.rule2/3/4.min-trailing-silence=0.5/0.75/1.0` |
| `graph/HCLr.fst` + `graph/Gr.fst` | `hcl_fst_`, `g_fst_` | Presence of these (rather than `graph/HCLG.fst`) is exactly what makes grammar mode possible — `SetGrm` warns "Runtime graphs are not supported by this model" without `hcl_fst_` |
| `graph/words.txt` | `word_syms_` | **Appears to be absent from the vendored copy** — worth verifying, since `UpdateGrammarFst` needs it for every token lookup |
| `graph/phones/word_boundary.int` | `winfo_` | Enables `WordAlignLattice`, hence per-word `start`/`end` and MBR alignment |
| `am/final.mdl`, `ivector/*` | acoustic model + i-vector extractor | — |
| `rescore/G.carpa`, `rescore/G.fst`, `rnnlm/*` | second-pass rescoring, RNNLM | **Not shipped in the small model.** A const-ARPA or RNNLM rescoring pass is therefore unavailable without a different model |

**Behaviours VoXR should know it is suppressing:**

- `vosk_set_log_level(-1)` at `NativeBridge~/src/vosk_bridge.cpp:215` hides `KALDI_WARN "Ignoring word missing in vocabulary: '<token>'"` from `UpdateGrammarFst`. Every OOV grammar entry — every "cqb" — is dropped silently. This is the single cheapest high-value fix in this whole report: iterate the generated grammar entries through `vosk_model_find_word` at Configure time and raise an Editor error naming the word.
- Grammar entries pass through `HashSet<string>` in `GenerateGrammarJson`, discarding the multiplicity that `LanguageModelEstimator::AddCounts` would interpret as weight.

---

## Rejection design options

Four realistic architectures for rejecting coughs, hums, and out-of-set speech, cheapest first. Costs are per-utterance unless stated; false-accept (FA) / false-reject (FR) directions are reasoned from mechanism, and only option B has a published measurement in a comparable deployment.

### A. Grammar-side only: weight `[unk]` up, phrase entries up

**What:** emit `[unk]` k times and multi-word phrase runs m times in `GenerateGrammarJson` (finding 1). No native change, no new asset.

**Cost:** zero runtime. Grammar rebuild grows with total token count. Days of A/B work.

**Behaviour:** raising `[unk]`'s prior makes the escape path cheaper, so short noise-driven in-grammar words lose to `[unk]` sooner. FA on noise **falls**; FR on genuine but poorly-articulated commands **rises**, monotonically in k. Because `[unk]` is coverage-exempt in the parser, an utterance that turns into `[unk]` runs is silent rather than wrong — which is the correct failure direction.

**Limit:** it cannot distinguish a cough from a quiet in-grammar word, because both are short low-energy events with some voicing. Expect it to shift the operating point, not to change the shape of the curve.

### B. VAD gate in front of the decoder (Silero)

**What:** ~2 MB MIT neural VAD between the downsampler and `accept_waveform_s`; non-speech frames never reach VOSK (finding 8).

**Cost:** ~2 MB APK, ~0.8 ms per 32 ms frame at Pixel-7-class CPU (~2.5% of one core), plus an ONNX Runtime dependency in the native bridge. On-device verification required.

**Behaviour:** the one comparable deployment tuned to `threshold=0.65, minSpeechFrames=5` measured **11% FP / 4% FN** against TV + air-conditioning + conversation. Non-vocal noise (taps, breathing, fans, room tone) is essentially eliminated. **Coughs are voiced and may pass.** FR rises on very quiet speech and on the first phoneme of a command (the `minSpeechFrames` hangover), which interacts with VoXR's already-painful leading-word problem — a VAD that eats the first 160 ms eats the verb, and the leading-required-miss bar then silences the command.

**Limit:** it is a speech/non-speech decision, not an in-grammar/out-of-grammar one. It does nothing for "cease fire" spoken in navigation mode.

### C. Parallel competitor path (likelihood-ratio rejection)

**What:** run a second recognizer off the *same* `VoskModel` (models are refcounted — `Recognizer` ctor calls `model_->Ref()`) in **free-vocabulary** mode, and accept the grammar result only when the grammar path's likelihood is competitive with the free path's. This is the classic keyword/filler, utterance-verification, likelihood-ratio architecture in its cheapest available form. The purer form — a phone-loop garbage model — needs option D.

**Cost:** roughly **doubles decode CPU**. The small model runs at 0.11×RT on desktop (model README); on Quest 3 with a full-vocabulary graph it will be substantially worse than the grammar path, and two paths together is the single most expensive option here. It also needs the "one recogniser per process" ABI limitation lifted (`g_recognizer` is file-scope in `vosk_bridge.cpp`).

**Behaviour:** this is the only option that produces a **rejection transcript**. "In weapons mode, say 'approach target alpha one'" would come back from the free path as approximately `approach target alpha one` while the grammar path returns `[unk] target alpha one` — which is precisely what `KNOWN_LIMITATIONS`' "Set restriction cannot produce meaningful rejection transcripts" says is impossible today. FA on both noise and out-of-set speech falls sharply; FR rises where the free path mis-transcribes a genuine command (which it does — "orient" → "korean", per the free-speech-mode entry), so the ratio must be thresholded generously.

**Limit:** cost, and the free path's own unreliability. A middle version — free path only over a short window, triggered when the grammar path's result is short and low-scoring — would bound the cost.

### D. Rebuilt model with a phone-level `<unk>` LM

**What:** rebuild the model's lexicon FST with `utils/lang/make_unk_lm.sh` so `[unk]` is a phone loop rather than a single garbage phone (finding 7). Optionally combine with option A's `[unk]` weighting.

**Cost:** an offline Kaldi + pocolm build, a forked ~40 MB model asset, and a re-verification of the whole fixture corpus. Near-zero runtime cost thereafter.

**Behaviour:** the theoretically correct answer. Noise and out-of-vocabulary speech get a path that genuinely fits them, so they stop being forced onto "on"/"from"/"four". Both FA-on-noise and FA-on-out-of-set fall without the CPU cost of option C. It may also expose the phone sequence, giving a weak rejection transcript.

**Limit:** VoXR does not own the model's training recipe, and `vosk-model-small-en-us-0.15` does not ship the files a rebuild needs. This is a multi-week piece of work with a real chance of ending in "cannot be done without Alphacephei".

### Recommended composition

**B + A** as the shipping pair: the VAD kills the non-vocal majority, and `[unk]` weighting moves the residual operating point, with the parser's existing bar as the last line. **C** only behind a developer-facing flag, for the "explain why the command was rejected" UX, on the understanding that it doubles decode cost. **D** only if a model rebuild becomes necessary for other reasons.

**What none of them changes:** all four alter the *rate* of noise-driven in-grammar words. None makes the decision principled at the token level, because none gives the parser a per-token "this token was probably not spoken" signal. That signal only exists in a lattice/N-best world (finding 3) or a calibrated-score world (finding 11).

---

## What this area cannot fix

- **The dropped-word invisibility.** "An in-grammar word VOSK drops leaves no token and no timing hole." Neither biasing, nor rejection, nor confidence produces evidence of a word that was never hypothesised. N-best helps only if some alternative *does* contain the word. A lattice with a wider beam raises that chance; it does not create a guarantee. The `KNOWN_LIMITATIONS` statement — "nothing in the transcript distinguishes 'the speaker never said it' from 'the speaker said it and the decoder dropped it'" — remains true after everything in this report, with one qualification: **N-best weakens it**, because a word appearing in alternative 2 but not alternative 1 is exactly that distinction, partially observed.
- **The phantom-command / leading-required-miss trade itself.** The bar is a decision about what to do with ambiguous evidence. Better evidence changes where the trade sits; it does not remove the trade. The 9-genuine-commands-lost vs 39-invented-suppressed ratio would need re-measuring under any change here, not assuming.
- **Sibling ties where the discriminating word was never uttered.** If the speaker did not say "weapons" or "navigation", no acoustic method recovers which they meant. This is an information-theoretic wall, and `disambiguateSiblingTies` (ask) is the correct answer.
- **The grammar-rebuild audio gap.** `set_grm` shortens one term of it; Kaldi's `GrammarFst` would remove it, and vosk-api does not expose `GrammarFst`. This is a decoder-architecture problem, not a biasing/rejection/confidence one.
- **The single-recogniser-per-process ABI limit.** Pure C++ state (`g_model`, `g_recognizer`, `g_initialised`). Option C above *requires* fixing it first.
- **Latency floors.** The endpointer, the `bufferWindow`, and the ~0.5–1.0 s post-speech final latency on Quest 3 are not confidence problems. `model.conf` endpoint rules move them; nothing in this report removes them.
- **Calibration on unseen speakers.** Every confidence method here is fitted to data. VoXR's evidence base is 699 TTS utterances and 73 field utterances from **one speaker**. Li et al. 2022 exists precisely because confidence estimators break under mismatch. Any calibrator VoXR ships will be, at best, calibrated for its author.

---

## Ranked recommendations for VoXR

### 1. Turn on N-best and parse all alternatives — but only after widening the lattice beam

**Rationale.** It is the largest accuracy lever available *without leaving VOSK*, it needs no new asset and no new dependency, and it attacks three separately-documented substitution failures with one change. The published analogue (WCN over 1-best for SLU: F1 0.73 → 0.77) says the gain is real but modest for a symbolic parser; the gain here should be larger because VoXR's failures are specifically homophone substitutions, which are exactly what sits at rank 2 in a lattice.

**Cheapest validating experiment.** No code change on the ship path at all. Using the existing offline A/B rig against the real decoder in WSL: set `--lattice-beam=6.0` in a copy of `conf/model.conf`, call `vosk_recognizer_set_max_alternatives(3)` in the desktop harness, and run the 699-utterance corpus. Count the rows where the *correct* command is produced by alternative 2 or 3 but not by alternative 1, and — equally important — the rows where an alternative produces a **wrong** command that alternative 1 did not. Ship only if recoveries clearly exceed inventions. Cost: one desktop harness change, hours. **Before writing any C# for this, decide what replaces `minConfidence`, because N-best mode deletes per-word `conf`.**

### 2. Validate every grammar word against the model vocabulary at Configure time

**Rationale.** `vosk_model_find_word` is exported by the shipped binary; `UpdateGrammarFst` silently drops unknown tokens; `vosk_set_log_level(-1)` hides the warning. A developer who authors "cqb" today gets no signal from any layer of the system — not the parser, not the bridge, not the log — and the failure surfaces only as `[unk]` in the field. This is a 30-line change that converts a documented mystery into an Editor error, and it makes the whole OOV limitation *legible* even though it does not fix it.

**Cheapest validating experiment.** Add `vosk_bridge_find_word(const char*)` to the ABI, call it over `GenerateGrammarJson`'s entries in `Configure`, and assert in an EditMode/PlayMode test that a grammar containing "cqb" raises a warning naming the word while one containing "cease" does not. Runs entirely in the host project. Pair with an `g2p_en`-driven Editor utility that proposes a spelled alias for any rejected word.

### 3. Ship a tuned `conf/model.conf` (lattice beam, then endpoint rules)

**Rationale.** Zero code, zero new dependency, and it is a *precondition* for recommendation 1 and for any confidence work: at `--lattice-beam=2.0` the lattice is too thin for the alternatives or the MBR posteriors to carry much information. The endpoint rules are separately the mechanism behind the mid-command-split limitation and the 2.0 s `bufferWindow` recommendation, and they have never been touched.

**Cheapest validating experiment.** Two independent sweeps on the WAV-replay corpus in WSL, one at a time: (a) `--lattice-beam` ∈ {2.0, 4.0, 6.0} against per-utterance decode wall-clock and command-match outcome; (b) `--endpoint.rule2.min-trailing-silence` ∈ {0.5, 0.8, 1.2} against the count of multi-final utterances and time-to-final. The desktop number is a lower bound on Quest cost — the RTF headroom question stays open until a human runs it in-headset, and must be reported as deferred.

### 4. Add a Silero VAD gate, measured against coughs before it is built

**Rationale.** It is the only cheap, deployed, licence-compatible answer to the cough/hum entry whose current workaround is "use push-to-talk" — i.e. VoXR currently solves a recognition problem with a UX concession. A comparable Android Vosk-grammar deployment measured 11% FP / 4% FN with it and published its tuning.

**Cheapest validating experiment.** **Do not integrate first.** Record ~30 coughs, hums, throat-clears and mic taps on the Quest (the one thing that needs the headset, and it needs only the recorder), run them offline through Silero at `threshold` ∈ {0.5, 0.65, 0.8}, and count how many frames are called speech. If coughs pass at 0.65, the VAD is the wrong instrument for VoXR's *named* failure and the case collapses to "kills taps and breathing", which is worth much less. Only then decide whether to take the ONNX Runtime dependency into `NativeBridge~`.

### 5. Replace the two uncalibrated thresholds with one reported, calibrated score — as an addition, not a replacement

**Rationale.** `minScore` and `minConfidence` are two hand-tuned gates over quantities with incomparable units, and `KNOWN_LIMITATIONS` documents that neither can be moved: `minConfidence` is pinned below 0.5 by "two", and `minScore` above 0.7 breaks three-element patterns. The literature's consistent answer since Yu, Li & Deng 2011 is a post-hoc calibrator over hand-designed features, applied without touching the recogniser. VoXR has most of the features already and would need only a logistic regression.

**Cheapest validating experiment.** Offline, no shipping change: dump the existing per-attempt features from the 699-corpus session logs (score, coverage charge, min/mean `conf`, span length, leading-miss flag, tied-rival flag), label each row correct/incorrect from the existing pins, fit a 4-feature logistic regression, and report AUC and ECE against the current two-threshold rule on a held-out split. If it does not beat two thresholds on 699 rows, it will not beat them in the field either. **Ship it first as a reported number on `VoxrCommand` and in the session log, gating nothing** — the calibration will be wrong for anyone who is not the author, and Li et al. 2022 is the citation for why.

**Explicitly not recommended now:** migrating off VOSK to sherpa-onnx for hotword biasing (finding 9). The published gains are large and the mechanism is the right one, but it is a whole-decoder replacement with unresolved model-size, licence and Quest-RTF questions. The bounded way to keep the option alive is a single offline experiment — run a streaming Zipformer transducer with hotwords over the same 699-utterance corpus in WSL and compare against VOSK+grammar. That measurement, not this report, should decide it.

---

## Sources

Fetched and read for this report unless marked otherwise.

**VoXR's own decoder (source read at the cited files)**
1. vosk-api, `src/recognizer.cc` — `UpdateGrammarFst`, `SetGrm`, `MbrResult`, `NbestResult`, `NlsmlResult`, `GetResult`, `CleanUp` — https://github.com/alphacep/vosk-api/blob/master/src/recognizer.cc
2. vosk-api, `src/model.cc` — model file paths, `ConfigureV1`/`ConfigureV2`, `FindWord` — https://github.com/alphacep/vosk-api/blob/master/src/model.cc
3. vosk-api, `src/vosk_api.cc` — C-ABI wrappers — https://github.com/alphacep/vosk-api/blob/master/src/vosk_api.cc
4. vosk-api, `src/recognizer.h` — https://github.com/alphacep/vosk-api/blob/master/src/recognizer.h
5. Kaldi, `src/chain/language-model.h` — `LanguageModelEstimator`, `AddCounts` — https://github.com/kaldi-asr/kaldi/blob/master/src/chain/language-model.h
6. Kaldi, `egs/wsj/s5/utils/lang/make_unk_lm.sh` — phone-level unknown-word LM — https://github.com/kaldi-asr/kaldi/blob/master/egs/wsj/s5/utils/lang/make_unk_lm.sh
7. Kaldi, "Support for grammars and graphs with on-the-fly parts" (`GrammarFst`) — https://kaldi-asr.org/doc/grammar.html
8. Alpha Cephei, "Model adaptation for VOSK" — https://alphacephei.com/vosk/adaptation
9. Alpha Cephei, "VOSK language model adaptation" — https://alphacephei.com/vosk/lm
10. vosk-api PR #1362, "Added option to set grammar with custom lexicon" (open) — https://github.com/alphacep/vosk-api/pull/1362
11. vosk-api issue #741, "Unusable 'Confidence'" — https://github.com/alphacep/vosk-api/issues/741
12. vosk-api issue #604, "Confidence value in nbest list is not normalized to 1.0?" — https://github.com/alphacep/vosk-api/issues/604
13. vosk-api issue #1017, grammar mode rarely returning `[unk]` (German/Spanish) — https://github.com/alphacep/vosk-api/issues/1017
14. daanzu, kaldi-active-grammar (AGPL-3.0) — https://github.com/daanzu/kaldi-active-grammar

**Contextual biasing**
15. G. Sun, C. Zhang, P. C. Woodland, "Tree-constrained Pointer Generator for End-to-end Contextual Speech Recognition", ASRU 2021 — https://arxiv.org/abs/2109.00627 (tables via https://ar5iv.labs.arxiv.org/html/2109.00627)
16. H. Futami, E. Tsunoo, Y. Kashiwagi, H. Ogawa, S. Arora, S. Watanabe, "Phoneme-Aware Encoding for Prefix-Tree-Based Contextual ASR", arXiv:2312.09582, 2023 — https://arxiv.org/html/2312.09582
17. G. Pundak, T. N. Sainath, R. Prabhavalkar, A. Kannan, D. Zhao, "Deep context: end-to-end contextual speech recognition", IEEE SLT 2018 — https://arxiv.org/abs/1808.02480 *(abstract fetched; the "68% relative" figure is from the abstract, tables UNVERIFIED)*
18. D. Zhao, T. N. Sainath, D. Rybach, P. Rondon, D. Bhatia, B. Li, R. Pang, "Shallow-Fusion End-to-End Contextual Biasing", Interspeech 2019 — https://www.isca-archive.org/interspeech_2019/zhao19d_interspeech.html *(numbers UNVERIFIED — none on the abstract page)*
19. D. Le et al., "Contextualized Streaming End-to-End Speech Recognition with Trie-Based Deep Biasing and Shallow Fusion", Interspeech 2021 — https://arxiv.org/abs/2104.02194
20. Y. Nakamura et al., "WCTC-Biasing: Retraining-free Contextual Biasing ASR with Wildcard CTC-based Keyword Spotting and Inter-layer Biasing", Interspeech 2025 — https://arxiv.org/abs/2506.01263
21. K.-C. Tsai, T.-H. Lo, Y.-T. Sun, B. Chen, "Contextual Biasing for Streaming ASR via CTC-based Word Spotting", arXiv:2605.18222, 2026 — https://arxiv.org/abs/2605.18222 *(magnitudes UNVERIFIED)*
22. Selvakumar et al., "Peeking Into The Future For Contextual Biasing", arXiv:2512.17657, 2025 — https://arxiv.org/html/2512.17657
23. sherpa-onnx, "Hotwords (Contextual biasing)" — https://k2-fsa.github.io/sherpa/onnx/hotwords/index.html

**Rejection, VAD, keyword spotting**
24. snakers4, silero-vad (MIT) — https://github.com/snakers4/silero-vad
25. "On-Device Wake Word Detection with Vosk Grammar Mode and Silero VAD v5" (Android deployment, measured FP/FN and per-frame latency), Zenn, 2025 — https://zenn.dev/diced/articles/vosk-silero-vad-wakeword-android?locale=en
26. Y.-H. Lee, N. Cho, "PhonMatchNet: Phoneme-Guided Zero-Shot Keyword Spotting for User-Defined Keywords", Interspeech 2023 — https://arxiv.org/abs/2308.16511
27. Y. Xi, H. Li, X. Gu, Y. Jiang, K. Yu, "MFA-KWS: Effective Keyword Spotting with Multi-head Frame-asynchronous Decoding", IEEE/ACM TASLP (accepted 2025) — https://arxiv.org/abs/2505.19577
28. P. Warden, "Speech Commands: A Dataset for Limited-Vocabulary Speech Recognition", arXiv:1804.03209, 2018 — https://arxiv.org/pdf/1804.03209 *(SOTA figure UNVERIFIED)*
29. B. Kim et al., "Dummy Prototypical Networks for Few-Shot Open-Set Keyword Spotting", 2022 — https://arxiv.org/pdf/2206.13691 *(numbers UNVERIFIED)*
30. tensorflow/models, YAMNet (AudioSet, 3.7 M weights) — https://github.com/tensorflow/models/tree/master/research/audioset/yamnet

**Confidence and calibration**
31. D. Qiu, Q. Li, Y. He, Y. Zhang, B. Li, L. Cao, R. Prabhavalkar, D. Bhatia, W. Li, K. Hu, T. N. Sainath, I. McGraw, "Learning Word-Level Confidence for Subword End-to-End ASR", ICASSP 2021 — https://arxiv.org/abs/2103.06716
32. Q. Li, Y. Zhang, D. Qiu, Y. He, L. Cao, P. C. Woodland, "Improving Confidence Estimation on Out-of-Domain Data for End-to-End Speech Recognition", ICASSP 2022 — https://arxiv.org/abs/2110.03327
33. D. Yu, J. Li, L. Deng, "Calibration of Confidence Measures in Speech Recognition", IEEE TASLP 19(8):2461–2473, 2011 — https://www.microsoft.com/en-us/research/publication/calibration-of-confidence-measures-in-speech-recognition/ *(PDF did not parse; tables UNVERIFIED)*
34. M. Huo, Y. Zhang, Y. Tang, "Identifying and Calibrating Overconfidence in Noisy Speech Recognition", arXiv:2509.07195, 2025 — https://arxiv.org/pdf/2509.07195 *(numbers UNVERIFIED — PDF did not parse)*
35. N. Kalika et al., "TeLeS: Temporal Lexeme Similarity Score to Estimate Confidence in End-to-End ASR", submitted IEEE/ACM TASLP 2024 — https://arxiv.org/abs/2401.03251 *(numbers UNVERIFIED)*

**N-best / lattices for SLU, and phonetic post-processing**
36. E. Villatoro-Tello et al., "Effectiveness of Text, Acoustic, and Lattice-based representations in Spoken Language Understanding tasks", arXiv:2212.08489, 2023 — https://arxiv.org/html/2212.08489
37. M. Li, W. Ruan, X. Liu, L. Soldaini, W. Hamza, C. Su, "Improving Spoken Language Understanding By Exploiting ASR N-best Hypotheses", 2020 — https://arxiv.org/abs/2001.05284 *(numbers UNVERIFIED)*
38. P. R. Singh, M. Zaki, A. Mukkamala, P. Wasnik, "Graph-Based Phonetic Error Correction of Noisy ASR" (G-SPIN), arXiv:2606.24889, 2026 — https://arxiv.org/html/2606.24889v1

**OOV, G2P, on-device baselines**
39. Kyubyong Park, `g2p_en` (Apache-2.0) — https://github.com/Kyubyong/g2p
40. AdolfVonKleist, Phonetisaurus (WFST G2P) — https://github.com/AdolfVonKleist/Phonetisaurus *(licence UNVERIFIED)*
41. X. Li et al., Allosaurus — universal phone recogniser (ICASSP 2020) — https://github.com/xinjli/allosaurus
42. N. Banfic, D. Fan, K. Vaishnavi, S. Kemp, S. Choi, R. Ren, S. Shaw, M. Tang, "Pushing the Limits of On-Device Streaming ASR: A Compact, High-Accuracy English Model for Low-Latency Inference", arXiv:2604.14493, 2026 — https://arxiv.org/abs/2604.14493

**Local repository files inspected**
43. `NativeBridge~/src/vosk_bridge.cpp`, `NativeBridge~/include/vosk_api.h`, `NativeBridge~/vendor/vosk-linux-x86_64-0.3.45/vosk_api.h`
44. `NativeBridge~/vendor/vosk-model-small-en-us-0.15/{README, conf/model.conf, graph/, am/, ivector/}`
45. `Runtime/Plugins/Android/arm64-v8a/libvosk.so` (export table)
46. `Runtime/Commands/VoxrCommandParser.cs` (`GenerateGrammarJson`, ~line 3076)
47. `Documentation~/scoring.md` §5, `KNOWN_LIMITATIONS.md`
