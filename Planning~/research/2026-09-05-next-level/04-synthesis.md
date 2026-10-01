# VoXR next-level research: synthesis (2026-09-05)

Start here. This folder is the output of a two-stage agent fan-out: six literature surveys and one code audit, then three ideation reports, then one adversarial critique that verified the load-bearing claims against the shipped binaries and source and merged everything into a single ranked list. This file is the reader's map plus the conclusions; the evidence lives in the files it points to.

## Folder map

| File | What it is | Words |
|---|---|---|
| `00-brief.md` | The shared system profile and ground rules every agent read | 1.4k |
| `01-code-audit.md` | Component map, data flow with timing, the inventory of ~60 hard-won parser rules, seams, constraints, smells | 11k |
| `02-verified-facts.md` | Facts checked on the main thread that override the reports where they conflict (exported symbols, header/binary mismatch, model file list, `model.conf` verbatim) | 0.5k |
| `research/R1-decoders-and-models.md` | On-device streaming decoders and acoustic models; grammar under a neural decoder | 9.6k, 55 sources |
| `research/R2-biasing-rejection-confidence.md` | Contextual biasing, out-of-grammar and noise rejection, confidence, N-best, OOV; what VOSK offers unused | 11k, 42 sources |
| `research/R3-kws-slu-endpointing-latency.md` | Keyword spotting, end-to-end SLU, VAD, endpointing, emission latency; a latency budget | 9.8k, 43 sources |
| `research/R4-frontend-robustness-personalization.md` | Enhancement, AEC, AGC, resampling, Quest mic, speaker adaptation, TTS augmentation; an audit of the shipped DSP | 12k, 45 sources |
| `research/R5-xr-voice-ux-multimodal.md` | Voice in VR/XR: deixis with gaze/pointing, feedback, activation modes, latency perception, repair, LLM parsing, developer tooling | 9.5k, 31 sources |
| `research/R6-evaluation-methodology.md` | Metrics, datasets, TTS validity, statistics on small sets, telemetry; a proposed evaluation harness | 11k, 32 sources |
| `ideas/I1-recognition-core-redesign.md` | 12 proposals for the front end, decoder, grammar, rejection, confidence; a decoder decision table | 11k |
| `ideas/I2-matching-redesign.md` | 9 proposals for the matching layer, with the rules each subsumes or must keep | 9.8k |
| `ideas/I3-latency-ux-redesign.md` | 12 proposals for latency and player/developer UX; a before/after latency budget | 14k |
| `03-critique-and-ranking.md` | Verification of 8 load-bearing claims, 12 resolved contradictions, a 42-item merged register with verdicts, the ranked top 12, redesign verdicts, 10 open questions | 12k |
| `05-rulings.md` | The maintainer's rulings on all ten open questions (2026-09-05/06), each with what it changes, plus the consolidated effect on the shortlist | 2k |

Citations in the research reports are tagged `VERIFIED` (the agent fetched the paper or repository page) or `UNVERIFIED` (it could not). Roughly one in four citations is `UNVERIFIED`; treat those as leads, not evidence. Everything in `02-verified-facts.md` and the critique's verification section was checked against files in this repository.

## The answer in one paragraph

The package is not primarily limited by its acoustic model. It is limited by defects and unused capability stacked between the microphone and the command event, most of which are cheaper to fix than the decoder is to replace, and by the absence of any instrument that can measure a decoder-side change. Two front-end defects are now measured twice by independent methods: the 48 to 16 kHz decimation filter attenuates the band the model's features use by 6 to 26 dB, and the AGC has no voice gate so it amplifies room tone by up to 26 dB inside every pause. The two dominant latency terms are silence timers set independently to defend against the same failure, and one of them lives in a text file inside the model zip. The shipped VOSK binary exports N-best, partial words, a live grammar reconfigure, a vocabulary lookup and speaker-model entry points that nothing calls. The recommended order is: fix the instrument and the two measured defects, exploit the unused decoder surface, then decide the endpointing redesign on a number nobody has yet, and only then revisit the decoder question. Replacing VOSK, rewriting the parser, and a handle-based native ABI are all explicitly not recommended this cycle.

## Headline findings (all verified against this repository)

1. **The decimation filter is a 3 kHz shelf.** The 15-tap filter in `NativeBridge~/src/downsampler.h` and its C# twin measures −3.3 dB at 3 kHz, −6.1 dB at 4 kHz, −16.0 dB at 6 kHz, −25.9 dB at 7 kHz, with +2.51 dB of un-normalised DC gain and only ~29 dB alias rejection. The model's own `mfcc.conf` uses features to 7.6 kHz. The /s/ vs /f/ distinction that separates "cease fire" from "safe five" lives in the starved band. A 95-tap Kaiser design costs under 0.2 % of one core. The defect is measured; the benefit of fixing it is not.
2. **The AGC has no voice gate and the two copies of it disagree.** `kNoiseFloor = 1e-5` is below any real mic floor, so gain saturates at 20× within ~0.9 s of every pause, presenting a post-pause cough ~26 dB hotter than speech. The C++ gate tests the smoothed envelope; the C# gate tests the raw sample. On digital silence they behave oppositely, and the fixture corpus is built from digital silence, so today the WSL harness and the Editor replay tests feed the decoder different signals.
3. **The endpointer is the latency, and it is a text file.** `conf/model.conf` sets trailing-silence rules at 0.5 / 0.75 / 1.0 s; the utterance buffer then waits another 0.5 s by default and 2.0 s as recommended on Quest. Both timers defend against the same mid-command split. The runtime endpointer setters are not exported by the shipped `libvosk.so`, so the conf file is the only route. The same file ships `--lattice-beam=2.0`, a third of VOSK's own default, which starves N-best and confidence downstream.
4. **A changed model or conf is silently ignored on upgrade.** `ModelExtractor` keys its cache on the zip file name and validates only three files, none of them `model.conf`. Any tuning shipped through the model is a no-op on every device that already ran the app.
5. **Nothing partial reaches the command layer in a player build.** The bridge and the speech recogniser deliver partials everywhere, but the command recogniser subscribes only under `UNITY_EDITOR` and only stores the text. The eager-flush path therefore never sees a partial, and partials carry no word array, so `set_partial_words` is a precondition for running the eager scan on them, not an optimisation.
6. **Half the shipped VOSK C API is unused.** Exported and uncalled: `set_max_alternatives`, `set_partial_words`, `set_grm`, `model_find_word`, `new_spk`, `set_spk_model`. Also: `vosk_set_log_level(-1)` hides the Kaldi warning that names every grammar word the model does not know, which is why "cqb" vanishes without a trace.
7. **Grammar entries are counts, not a set** (upstream read, corroborated in the binary but not reproducible from vendored source). Repeating an entry raises its prior; repeating `[unk]` raises the rejection prior. `GenerateGrammarJson` deduplicates through a `HashSet`, so the only free biasing lever VOSK offers is thrown away. One harness run with a 10× repeated entry would prove or kill this.
8. **The per-word confidence cannot reject noise by construction.** It is an MBR posterior over an already grammar-constrained lattice; it answers "which in-grammar word" and never "was this in-grammar". "two" at 0.50 is a correct report of a to/two lattice split. `minConfidence` was never a noise gate.
9. **The evaluation instrument cannot see the decoder.** The 699-row A/B rig stages nine parser files and zero VOSK; it replays perturbed transcripts. A separate WSL push-audio harness does run the real decoder, on 16 fixtures from one TTS voice, with a 95 % interval of ±0.10 at a perfect score. Nobody has joined the two. The project has zero hours of negative audio, so the phantom-command class has never had a rate.
10. **Two parser hazards are real and cheap.** Per-word confidence is keyed by word text with first-occurrence-wins, so a repeated word reports the wrong number; and the utterance buffer concatenates word arrays without rebasing timings, so a merged utterance carries restarting timelines. Both block any timing-based feature (deixis) and the confidence display.

## What the six research areas concluded

**Decoders (R1).** Accuracy headroom is real: VOSK small reports 9.85 % WER on LibriSpeech test-clean; a ~20 M streaming Zipformer reports 3.94 % at comparable int8 size under Apache-2.0, and VOSK's own maintainers are migrating to that stack. But no English streaming VOSK-Zipformer model exists, no small English streaming CTC model exists for the grammar (HLG) path, every published real-time factor is on non-Quest silicon, and Meta does not expose the NPU. Whisper is closed out (not streaming, ~273 MB RAM for tiny, grammar constraint reported ineffective). The sherpa-onnx keyword-spotting model (3.3 M params, ~5 MB) is the cheapest closed-set option but returns hits, not transcripts. Verdict: stay on VOSK this cycle; keep two options alive as offline screens.

**Biasing, rejection, confidence (R2).** Neural biasing (TCPGen, CLAS, sherpa-onnx hotwords) reports large rare-word gains but requires a decoder migration. What works today: N-best parsing (with the trap that N-best mode deletes per-word `conf`), grammar weighting by repetition, the OOV vocabulary gate, a wider lattice beam, and a parser-side logistic calibrator producing one reported reliability number. Rejecting coughs is not solvable inside a grammar decoder; the answer is a competitor path or a VAD in front.

**Keyword spotting, SLU, endpointing (R3).** End-to-end SLU is blocked on per-game audio; TTS-only training leaves a 4-point gap on a closed 248-sentence set and nothing measures numeric slots. Text-enrolled KWS is Quest-sized but reports EER up to 64 % on shared-prefix negatives, which are exactly sibling commands, so it survives as a gate and not a fast path. VADs are cheap (TEN VAD 532 KB, Silero ~2 MB) but detect voicing, not words. Learned turn detectors are the wrong size or task. Kaldi's own endpointer already has grammar-completeness logic (`max_relative_cost`) that VOSK does not expose. Target latency: 200 to 300 ms after end of speech; today's eager path is ~550 to 1100 ms and the time-driven path ~2.5 to 3.1 s, plus an unmeasured Quest catch-up.

**Front end and personalisation (R4).** The filter and AGC findings above. Enhancement in front of an ASR not trained on enhanced audio degraded WER in 40 of 40 tested configurations, so no neural denoiser. The model runs online i-vector speaker adaptation, and every recogniser recreation discards it. What Horizon OS applies to the `VOICE_RECOGNITION` source, whether `UNPROCESSED` exists, and whether platform AEC is available are unknown and answerable in one adb session. Game-audio bleed is unmeasured.

**XR voice UX (R5).** The largest measured player-facing win is deixis: +26.5 % coreference accuracy from adding gaze and pointing to a VR transcript, and 30 to 86 % faster multi-target selection; it shrinks the grammar. Fuse on VOSK's per-word timings within about ±1 s of the deictic word. Acknowledge at the first partial: in VR, quality of experience degrades at every step from 1.5 to 6.5 s and natural fillers help while spinners do not. Marking low-confidence words raised error detection 12 % without any threshold change. Keep push-to-talk as the default, add gaze-gated listening, bound repair at two attempts. No on-device LLM parser on Quest. Developer experience gap is a confusability linter and an annotation-set evaluator, both with prior art.

**Evaluation (R6).** Score intents and slots, not words. Report TTS and human numbers as a pair and rank only on human (CHiME-4 protocol), because the synthetic-to-real gap is 6 to 19 points and architecture-dependent. Paired McNemar: six discordant utterances all one way is the smallest significant result; a 90 to 95 % improvement needs ~290 paired utterances. Adopt FA/h and FRR at a fixed operating point for the phantom-command class. Licensing: Speech Commands v2, MUSAN, SLR28 RIRs and Kokoro TTS are clean; SLURP audio, Fluent Speech Commands, ATCO2, XTTS and F5-TTS weights are non-commercial. The undelegatable item: record 10 speakers × 30 phrases × 3 conditions on Quest and seal half.

## Redesign verdicts (from the critique)

- **Replace VOSK now: no.** The case for staying is stronger after verification than before it. Revisit when a human corpus exists and after two bounded offline screens (`0.22-lgraph` swap; unconstrained 20 M Zipformer diff), neither of which may rank two decoders on TTS audio alone.
- **Rewrite the parser now: no.** All three ideation reports converge on this. The wholesale probabilistic reformulation would still have to keep the bar, completeness, selection determinism and the tie machinery as hard constraints, and would invalidate five published measurements to be justified on a corpus that cannot certify a five-point change. Fix the local defects instead (token-aligned confidence, timing rebase, anchored NumberSequence).
- **Introduce decoder/audio interfaces now: only the ones a shortlist item needs.** Unify the duplicated DSP rather than abstracting over two divergent copies. A full `IDecoder` seam waits for a committed consumer.
- **Handle-based native ABI now: no.** User-invisible, rewrites the one subsystem with no automated coverage, and both would-be consumers are deferred.

## Ranked shortlist

Rank is by value × confidence over effort, with prerequisites first only where they truly gate the rest. Effort: S ≤ 2 days, M ≤ 2 weeks, L > 2 weeks. Full rationale, kill criteria and experiments are in `03-critique-and-ranking.md` §4.

| # | Item | Axis | Effort | Verdict | First experiment |
|---|---|---|---|---|---|
| 1 | OOV vocabulary gate: `vosk_model_find_word` over the generated grammar, Editor error naming the word | DX | S | CONFIRMED | EditMode test: "cqb" warns, "cease" does not |
| 2 | Session log fields `barred`, `runnerUpIntent/Score`; invariant number formatting | Instrument | S | CONFIRMED | Replay a known-barred fixture, assert `barred: true` with no command |
| 3 | `ModelExtractor` cache version key (also validate `model.conf`) | DX / correctness | S | CONFIRMED | Mutate `model.conf` in the zip, re-run, assert the extraction changed |
| 4 | Join the parser A/B rig to the WSL push-audio harness; slot partial credit in the scorer | Instrument | M | CONFIRMED | Reproduce the harness's `expectations.json` baseline byte for byte |
| 5 | Record `human-core` (10 speakers × 30 phrases × 3 conditions), seal half; adopt dual TTS/human reporting | Instrument | L, human-only | CONFIRMED | Two speakers × the existing 16 phrases first; see if the pass rate moves |
| 6 | Endpointer profile via `conf/model.conf` (rule2/3/4 trailing silence) | Latency | S | PLAUSIBLE | Sweep rule2 ∈ {0.5, 0.35, 0.25} s on the joined rig, in sample time |
| 7 | Unify the C++/C# AGC, then speech-gate and re-target it | Accuracy | S then M | CONFIRMED defect | Diff post-AGC samples between harness and Editor on one WAV |
| 8 | Replace the decimation filter (95-tap Kaiser, DC-normalised), both copies | Accuracy | S then M | CONFIRMED defect | Old vs new coefficients through the joined rig; re-pin docs with `DocCheck` |
| 9 | Eager-commit scan on partials: instrument first (`set_partial_words`, stability gate, commit watermark) | Latency | L | PLAUSIBLE | Log every partial's would-be verdict vs the final's; kill if contradiction rate > ~1 % |
| 10 | N-best screen: `set_max_alternatives(3)` at `--lattice-beam=6.0`, recoveries vs inventions | Accuracy | L | PLAUSIBLE | Desktop harness only; decide what replaces `minConfidence` before any C# |
| 11 | `OnListeningStateChanged` + acknowledgement at the first partial + diegetic-filler docs | UX | S | PLAUSIBLE | Telemetry: re-utterances within 2 s, before and after |
| 12 | Negative corpus (≥ 5 h) with FA/h and FRR at a fixed operating point | Instrument | M | CONFIRMED | Assemble one hour, replay against a live set, count fires |

Just below the cut and first to add if there is slack: token-aligned per-word confidence (S, fixes a verified hazard, prerequisite for the confidence display and deixis).

## Refuted or blocked

- **Shrinking the 4096-sample read chunk as a latency win: refuted.** The ring buffer read returns whatever is available; 85 ms is a catch-up cap, not a per-iteration wait. Two ideation budgets over-counted by up to 65 ms.
- **`vosk_recognizer_set_grm` as the audio-gap fix: refuted.** The gap's first terms are VoXR's own capture stop and recognition-thread join in `vosk_bridge_stop`, which no decoder API touches. Keeping capture running across a rebuild is the real lever, once the gap is measured in sample time.
- **Runtime endpointer setters: not implementable.** Declared in the local header, absent from the shipped binary. A latent link-time trap.
- **Grammar (HLG) decoding over a neural CTC model: blocked.** No small English streaming zipformer-CTC model exists.
- **A parallel free-vocabulary competitor path for wrong-mode explanation: blocked** on a handle-based ABI and roughly doubles decode CPU.
- **End-to-end SLU, learned turn detectors, emission-latency training, neural denoising, on-device LLM parsing: not recommended**, each for a measured reason in the reports.

## Longer-horizon proposals kept alive (deferred, not rejected)

Weighted grammar emission with an `[unk]` prior and a sibling-flat constraint; union grammar scope with an out-of-context event; i-vector re-priming after a rebuild; a neural VAD gate with 200 to 300 ms pre-roll (after classifying the 73 field false triggers into non-speech vs speech-like, and after a licence and APK decision); phonetic-neighbourhood matching on the failure path with an exact-only anchor (after the filter fix, or it tunes a compensator); one reported, calibrated reliability score; word-class miss costs; deictic slots bound to word timings; speculative execution with retraction for reversible commands; gaze-gated listening; the confusability linter; the annotation-set regression asset; the most-confused-pairs dashboard; the player voice-check sample over the already-bound input-level getter; deleting the dead AAudio backend.

## Decisions only the maintainer can make

**All ten were ruled on 2026-09-05/06; see `05-rulings.md`.** In brief: 1 yes and yes; 2 description; 3 absolute; 4 N-best first with a per-word display input designed in; 5 WebRTC APM first, Silero held, TEN VAD rejected on licence; 6 70 MB ceiling, models downloaded on demand at a language-selection screen, `0.22-lgraph` screen agreed; 7 yes as a measured experiment; 8 no for this cycle; 9 registry needs building, ellipsis only, defined as game-state resolution via a resolver hook; 10 yes, re-baseline as one unit.

1. Will you record `human-core` and seal a holdout you do not look at? Without it nothing from rank 6 down can be ranked, only built.
2. Is `scoring.md` a contract or a description? Five published corpus numbers and two enumerations declared exhaustive move under items 7, 8, 9 and 10.
3. Is the bar's uniformity negotiable given positive evidence that the anchor was spoken (N-best rank 2, one phone away), or is refuse-to-fire absolute?
4. Which goes first, N-best or the confidence display? N-best drives `Confidence` to −1 and silently disables two gates.
5. Will you take a second native dependency for a VAD, and does TEN VAD's licence clear your bar? It decides which one to prototype.
6. How much APK is actually available? The answers range from −35 MB to +88 MB across the options.
7. Should wrong-mode explainability be a product goal?
8. Which real-game commands are reversible, and is an occasional retraction acceptable for ~200 ms?
9. Does the game already publish targetable entities with categories and salience?
10. Are the front-end fixes (7, 8) allowed to shift every published score, with re-pinning rather than editing?

## Evidence caveats

- Three mechanisms rest on readings of upstream vosk-api or Kaldi source that is not vendored here: N-best deletes per-word `conf`; `SetGrm` rebuilds the feature pipeline; grammar entries are n-gram counts. Each is corroborated by strings or symbols in the shipped binary and each has a one-run local disproof.
- Every accuracy benefit in this folder is unmeasured for this system. The defects are measured; the fixes are hypotheses with mechanisms.
- The field evidence base is one speaker, 73 utterances. TTS results do not predict human results and cannot rank two decoders.
- On-device latency (the Quest decoder catch-up) has never been measured and is human-only to measure.
