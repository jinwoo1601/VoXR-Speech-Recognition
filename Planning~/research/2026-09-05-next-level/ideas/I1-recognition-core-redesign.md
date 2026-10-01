# I1 — Recognition-core redesign: proposals, decoder decision, rejection architecture

Ideation pass, 2026-09-05. Scope: brief §2 stages 1–4 (capture, DSP, decoder, grammar) plus the confidence signal stage 6 consumes. Sources: `00-brief.md`, `02-verified-facts.md` (authoritative where R1–R6 conflict), `01-code-audit.md`, `research/R1`–`R6`, and the source files named below.

Citation convention. `file:line` for code. Line numbers in `NativeBridge~/src/*`, `Runtime/Dsp/*`, `Runtime/VoxrJsonParser.cs`, `Runtime/EditorMicBackend.cs`, `Runtime/Commands/GrammarManager.cs`, `Runtime/Commands/VoxrCommandParser.cs:3060-3210`, `Runtime/Commands/VoxrCommandRecogniser.cs:498-525,1328-1334`, `Runtime/ModelExtractor.cs:20-40` and `NativeBridge~/include/vosk_api.h` were read directly for this report. Line numbers elsewhere (`VoxrSpeechRecogniser.cs`, the rest of `VoxrCommandParser.cs`, `BridgeNative.cs`) are taken from `01-code-audit.md`, which states every claim there carries a verified `file:line`; they are marked `[audit]` on first use in each proposal.

Every benefit claim is tagged **measured** (a number exists in a cited report, with its dataset) or **unmeasured**.

---

## Thesis

VoXR's recognition core is not primarily limited by its acoustic model; it is limited by four self-inflicted layers stacked between the microphone and the decoder's own capabilities, each of which is cheaper to remove than the model is to replace. The audio the decoder receives has been passed through a 15-tap "anti-alias" filter that is really a 3 kHz shelf (−6.1 dB at 4 kHz, −25.9 dB at 7 kHz, +2.51 dB of unnormalised DC gain — R4 D1, measured) against a model whose features run to 7.6 kHz, and then through an AGC with no voice-activity gate that amplifies room tone by up to +26 dB inside every pause (R4 A2, measured) before handing the result to a grammar decoder that is structurally obliged to emit an in-vocabulary word for it. The grammar itself is emitted as a `HashSet` (`VoxrCommandParser.cs:3079`) into an API that reads it as bigram *counts*, throwing away the only free biasing lever VOSK offers (R2 finding 1). The decoder's 1-best output is the only thing consumed, so the alternative that usually contains the right words (`set_max_alternatives`, exported by the shipped `libvosk.so` per `02-verified-facts.md`) is never asked for; and the number the parser calls confidence is a *grammar-conditioned* MBR posterior that by construction cannot say "this was not in the grammar" (R2 finding 2) — so `minConfidence` was never a noise gate and no amount of tuning will make it one. The correct order of work is therefore: fix the plumbing, buy a real speech/non-speech gate, turn the grammar into weights, get N-best and a confidence signal that means something, build the FA/h × FRR instrument that can prove any of it, and only then decide the decoder question — for which the honest answer today is *stay on VOSK and run the migration as an offline measurement*, because every published RTF in R1 is on non-Quest silicon and no small English streaming CTC model with an HLG path currently exists.

---

## Proposals

### P1 — Replace the decimation filter (95-tap Kaiser, DC-normalised)

**Problem.** `cease fire → safe five` (/s/↔/f/) and `all → fall` (inserted /f/) — the two best-documented in-grammar substitutions in `KNOWN_LIMITATIONS.md`. Both are high-band fricative confusions.

**Mechanism.** The shipped filter is a 15-tap symmetric windowed sinc applied at 48 kHz with every third output taken (`NativeBridge~/src/downsampler.h:25-49`, coefficients `:64-68`; C# mirror `Runtime/Dsp/Downsampler.cs:22-27`, `:33-61`). R4 evaluated `H(f)` on those exact coefficients: −0.35 dB @1 kHz, −3.28 @3 kHz, −6.08 @4 kHz, −15.97 @6 kHz, **−25.90 @7 kHz**, `Σh = 1.3358` (**+2.51 dB un-normalised broadband gain**), worst alias image rejection **−29.2 dB**. The model's `conf/mfcc.conf` sets `--high-freq=7600` over 40 mel bins, so the top ~13 bins arrive 6–37 dB below where the acoustic model was trained to see them, on every frame of every utterance. Replace the coefficient table with a 95-tap Kaiser design (fc 7800, β 8.5) and normalise DC to unity.

**Grounding.** R4 D1/A1 (`MEASURED (this report)` in R4, method given so it is re-derivable). The header comment at `downsampler.h:58-63` ("cutoff at 1/6 of sample rate … ~7.5 kHz passband") does not describe the coefficients that are there. Fricative place/sibilance cues live in this band (Kong et al., PLOS ONE 2014, cited R4 D1). Note the brief's own §2.2 description ("decimation-by-3 downsampler") understates it — there *is* a filter; it is just badly specified.

**Code impact.** Stage 2 only. `NativeBridge~/src/downsampler.h:64-68` (table) and `:14` `kFilterTaps`, plus history-buffer length; `Runtime/Dsp/Downsampler.cs:14,22-27`. Two files, identical change; the `Process` loop shape is unchanged. No native ABI change. **Editor/device parity: both copies must change in the same commit** — the WSL harness exercises the C++ path (`NativeBridge~/harness/main.cpp` [audit]), the Editor WAV-replay tests exercise the C# path via `EditorMicBackend.ProcessChunk` (`Runtime/EditorMicBackend.cs:375`). The `.so` must be rebuilt and committed (`Runtime/Plugins/Android/arm64-v8a/`).

**Expected effect.** Filter response: measured (≤0.36 dB droop 0–7 kHz, ≥53.9 dB alias rejection, R4 D1 table). Effect on the substitution pairs: **unmeasured** — R4 states the mechanism is a hypothesis, not a proven cause.

**Cost.** ~1 day engineering. CPU 1.52 MMAC/s vs 0.24 today — R4 sizes this at under 0.2 % of one XR2 core before NEON, halved again by symmetry. RAM +~320 B. APK 0.

**Risk and kill criterion.** Every score in every corpus shifts; `minScore` 0.6, `minConfidence` 0.4 and the leading-miss bar were tuned against the current tilt, and `Documentation~/scoring.md` publishes five specific 699-row measurements as justification (`scoring.md:178,238,242,454,511` [audit]). Removing the +2.51 dB DC gain also changes the effective AGC operating point, so P1 and P2 must be evaluated jointly, not separately. **Kill if** command-level accuracy on the acoustic corpus does not improve or regresses at equal thresholds after a threshold re-sweep.

**Cheapest validating experiment.** Offline, WSL, no device: replay the 16-file WAV fixture corpus (`Tests~/Fixtures/audio/tts/` [audit]) through the desktop push-mode harness with old vs new coefficients, same everything else. Caveat that decides the schedule: R6 computes the 95 % Wilson half-width of a 16-case run at **±0.097 (16/16)** and **±0.163 (14/16)** — that corpus cannot rank a filter change. This experiment is *directional only* until P12 lands. Then re-pin the published numbers with `DocCheck.cs` before shipping.

**Migration.** No public API change, no asset change. Documentation: `scoring.md`'s corpus figures must be re-derived, not edited.

---

### P2 — Make the AGC speech-gated and re-target it; unify the two implementations

**Problem.** "Cough, hum, and noise can trigger false matches in grammar mode", whose shipped workaround is "use push-to-talk" — a recognition problem answered with a UX concession. Secondary: leading-word loss, the failure the 2.0.0 leading-required-miss bar exists to contain (`scoring.md:242` [audit]: 9 genuine rows lost per 699 against 39 invented suppressed).

**Mechanism.** `kNoiseFloor = 1e-5f` (≈−100 dBFS, `NativeBridge~/src/agc.h:77`) is the only "is there signal" test, and no real mic noise floor sits below it. So in a pause `desired_gain = target_level_ / smoothed_level_` (`agc.h:56`) saturates at `kMaxGain = 20` (`agc.h:79`) and the 300 ms release (`agc.h:32`) walks `current_gain_` to ~10× in ~190 ms and ~19× in ~0.9 s (R4 A2, measured derivation). A cough after a one-second pause is handed to VOSK **~26 dB hotter than speech**. Separately, `target_level_` is `db_to_linear(−18)` compared against a *mean-absolute* EMA (`agc.h:49,56`), and speech peak−mean-abs is +14…+20 dB, so post-gain peaks land near −4…+2 dBFS and `fast_tanh` (`agc.h:67,93-98`) compresses on essentially every syllable; coming out of silence at gain ≈20 the 10 ms attack (`agc.h:31`) takes 20–30 ms to settle — 2–3 chain frames at `--frame-subsampling-factor=3`, i.e. one phoneme, at the start of every utterance. Fix: gate gain adaptation on a VAD decision (freeze `current_gain_` while non-speech), raise `kNoiseFloor` to a realistic ~−60 dBFS, drop the target to ~−24 dBFS on the same estimator. Principled variant: replace `agc.h` with WebRTC AGC2, which is VAD-driven by design.

**Grounding.** R4 A2/A3/A4/D5/D7. R4 D7 also settles *why* an AGC is needed at all here and the code does not say it: Kaldi's nnet3 online pipeline feeds the network un-adapted, non-mean-normalised MFCCs, so gain is not normalised away — it lands entirely on c0 (`--use-energy=false`), which argues for a slow speech-gated utterance-level normaliser, not a per-sample compressor. The int16 argument in `vosk_bridge.cpp:48-50` is weaker than it reads: at the documented 0.04 peak the residual quantisation SNR is ≈68 dB (R4 A5, measured), at or below MEMS self-noise.

**Code impact.** Stage 2. `NativeBridge~/src/agc.h:53,56,77,79,31-32,17` and `Runtime/Dsp/Agc.cs:26,28,66,72-74,23-24`. Depends on P3 for the gate signal (or ships with an energy-hysteresis gate as a stopgap). **Parity hazard, already live:** the two implementations have silently diverged — C++ gates on `smoothed_level_` (`agc.h:53`), C# gates on the instantaneous sample (`Agc.cs:66`), and the comment at `Agc.cs:63-64` points to "the divergence note in the class summary" which does not exist (the summary is `Agc.cs:1-6`). Any AGC experiment run in the Editor is therefore measuring a different AGC from the one that ships. Unify as part of this change.

**Expected effect.** The +26 dB silence boost is measured (R4 A2); its share of the false-trigger rate is **unmeasured**. The onset-transient → leading-word-loss link is explicitly labelled an untested hypothesis with at least three rival causes in R4 A4.

**Cost.** 2–3 days for the minimal variant; 1–2 weeks plus a ~1–2 MB BSD-3 dependency for AGC2. CPU: state machine ≈ free; whole WebRTC APM measured at 4.2 µs per 10 ms frame @16 kHz on M4 Max NEON (R4 D5), ~1.3 % of one core even at 30× derating.

**Risk and kill criterion.** A gate that misfires clips quiet onsets and makes leading-word loss *worse* — which the bar then converts into silence. **Kill if** the silence-then-command fixture set shows first-word retention dropping at any false-trigger reduction.

**Cheapest validating experiment.** R4's recommendation 2 verbatim: build silence-then-command and silence-then-cough WAVs (2 s of real Quest room tone, then the event), replay through the push-audio bridge with (i) current AGC, (ii) gain-frozen AGC, (iii) AGC off, and count false triggers **and** leading-word losses. Both, or the experiment is worthless. Recording the room tone and the coughs is the only human-only step and needs only a recorder, not a build.

**Migration.** `micGainTargetDb` is a serialized Inspector field (`VoxrSpeechRecogniser.cs:34` [audit], default −18) whose meaning changes if the target statistic changes; either keep the units and change the default, or rename with `[FormerlySerializedAs]` — noting `KNOWN_LIMITATIONS.md:365` [audit], that attribute migrates the field but *not* a prefab-instance override.

---

### P3 — A VAD gate with pre-roll, in front of the decoder — the missing garbage class

**Problem.** "No silence/garbage output in grammar mode": a grammar-constrained WFST must emit an in-vocabulary path, so coughs, hums and breathing become "on"/"from"/"four". `[unk]` only catches out-of-grammar *speech*.

**Mechanism.** Insert a neural VAD between the AGC and `vosk_recognizer_accept_waveform_s` (`NativeBridge~/src/vosk_bridge.cpp:119` → `:125`). Frames the VAD calls non-speech are never offered to the decoder, so the decoder is never asked to explain them. **Mandatory design constraint: 200–300 ms pre-roll on gate-open**, implemented by running the VAD on a delayed copy — the ring buffer already holds 1.365 s (`ring_buffer.h` capacity 65536 [audit]), so the delay line is free. The same gate signal feeds P2, and a per-utterance speechiness statistic becomes a feature for P11.

**Grounding.** R2 finding 8 / rejection option B; R4 D4; R1 item 11; R3's VAD row. Silero VAD: MIT, ~2 MB, "<1 ms per 30+ ms chunk on a single CPU thread"; measured on Pixel 7 arm64 at **0.8 ms/32 ms frame on 2 threads, 0.3 ms with NNAPI**, in a published Android write-up that pairs it with *Vosk grammar mode specifically* and reports **11 % FP / 4 % FN** at threshold 0.65 / 5 min-speech-frames in a TV+AC+conversation condition. TEN VAD is the smaller alternative: **532 KB arm64-v8a**, RTF 0.057 on a Galaxy J6+, 16 ms hops, but Apache-2.0 **with additional conditions** plus an LPCNet-derived BSD file — a licence review, not a licence clearance (R3).

**Code impact.** Stage 2, native only. New source in `NativeBridge~/src/`, a hook in `recognition_loop` between `vosk_bridge.cpp:119` and `:125`, and either an ONNX Runtime arm64 dependency (~15 MB, R1) or a hand-written inference kernel for one small net. The C# `EditorMicBackend.ProcessChunk` path (`Runtime/EditorMicBackend.cs:373-407`) would have **no equivalent** unless the gate is also mirrored — a new parity divergence of exactly the kind §P2 is cleaning up. Prefer a native-only gate plus an explicit documented Editor/device difference, or accept the ORT dependency on both sides.

**Expected effect.** Latency/CPU: measured (above). VoXR's own false-trigger reduction: **unmeasured**. And R2 names the load-bearing caveat: **a cough is voiced**; Silero is trained to detect speech, and there is no published number for cough rejection. It will certainly kill taps, breathing and room tone.

**Cost.** 1–2 weeks with ORT; ~2 MB (Silero) or 532 KB (TEN) plus up to 15 MB of runtime if ORT is taken. CPU ~2.5 % of one core.

**Risk and kill criterion.** Onset clipping (see P2). Gating audio also changes what Kaldi's endpointer sees, so `--endpoint.rule2.min-trailing-silence` behaviour and `UtteranceBuffer` reassembly move underneath you (R4 D4 open question). **Kill criterion, and run it before writing any code:** record ~30 coughs, hums, throat-clears and mic taps, run them through Silero offline at thresholds {0.5, 0.65, 0.8}, and count frames called speech. If coughs pass at 0.65, the VAD is the wrong instrument for the *named* failure and the case collapses to "kills taps and breathing" — worth much less than the ORT dependency. Cheaper still and strictly first: R1's zero-code probe — classify the existing 73-utterance field debug logs (`Library/VoxrDebugLogs/`) into false triggers that were non-speech versus speech-like. That upper-bounds the whole win before anything is built.

**Migration.** No public API change. `VoxrPushToTalkController` users gain nothing; continuous-mode users gain the fix. `KNOWN_LIMITATIONS.md`'s "use push-to-talk" workaround gets replaced by a real answer.

---

### P4 — Emit the grammar as weights, not as a set

**Problem.** In-grammar substitutions where the *phrase* was correct and a constituent word won instead: `switch to weapons → switch two weapons`, `all modes → fall modes`. Plus: no in-decoder lever at all on noise-driven short words.

**Mechanism.** `vosk_recognizer_new_grm` does not build a hard grammar. `Recognizer::UpdateGrammarFst` feeds each JSON entry to Kaldi's `LanguageModelEstimator` at `ngram_order=2, discount=0.5`, whose `AddCounts` is documented as "for each n-gram in the sentence, `count[n-gram] += 1`". Multiplicity is therefore **weight**. `GenerateGrammarJson` accumulates into `new HashSet<string>(StringComparer.Ordinal)` (`Runtime/Commands/VoxrCommandParser.cs:3079`), sorts (`:3160-3161`) and emits each entry exactly once (`:3163-3174`) — discarding the lever entirely. Change the accumulator to `Dictionary<string,int>` and emit each entry `count` times. Two independent uses: (a) weight the multi-word phrase runs above their constituent single words, sharpening the bias the code already intends — the comment at `:3177-3181` concedes that keeping single words alongside phrases makes "the sequence constraint a bias rather than a hard rule"; (b) weight `[unk]` (added once at `:3157`) up, raising the decoder's willingness to leave the grammar.

**Grounding.** R2 finding 1 (`VERIFIED` from vosk-api and Kaldi source; magnitude `UNMEASURED`). Code: `VoxrCommandParser.cs:3079`, `:3089-3121` (run collection), `:3157` (`[unk]`), `:3182-3192` (`AddPhrase` — phrase plus every constituent word), `:3196-3207` (`AddSurfaceForm`).

**Code impact.** Stage 4, ~20 lines, one method, pure C#. No native change, no ABI change, no asset-format change, no API change. Grammar JSON grows linearly with total weight; `UpdateGrammarFst` cost grows with total token count, which is paid only at rebuild. Editor and device are identical here (both call the same static, `GrammarManager.cs:22`).

**Expected effect.** **Unmeasured.** The estimator has a hard backoff with a 0.5 discount, so the effect is monotone but not proportional, and R2 explicitly cannot say how many repetitions move the decision boundary at all. The two uses pull against each other: `[unk]` mass costs in-grammar recall.

**Cost.** ~1 day engineering plus the A/B work. Zero runtime cost.

**Risk and kill criterion.** Over-weighting one phrase can starve a sibling, which is precisely the sibling-tie hazard `disambiguateSiblingTies` exists for. The project memory `grammar_ab_rig` records the trap directly: a clean A/B on the 699-corpus is **not** on its own evidence that a scoring-adjacent change is safe. **Kill if** the `[unk]` weight that suppresses noise triggers also raises `OnUnrecognisedSpeech` on genuine commands, or if phrase weighting flips any sibling pair.

**Cheapest validating experiment.** A one-dimensional sweep of the two weights against the acoustic corpus, offline. **Precondition and warning:** per R6's audit, the 699-row "A/B corpus" is *text-only* — the rig stages parser `.cs` files and zero VOSK code — so it cannot see a grammar change at all. This experiment needs audio, which today means the 16 WAV fixtures, which R6 shows cannot rank anything. So P4 is a cheap change gated on P12, not a cheap change gated on nothing. (Flagged as an open question below: project memory `grammar_ab_rig` describes measuring a `GenerateGrammarJson` change "against the real decoder in WSL", which conflicts with R6's reading of `stage.sh`. Resolve before planning.)

**Migration.** None visible. `Documentation~/command-sets.md` should state that grammar entries are weighted, since it currently implies a set.

---

### P5 — Turn the silent OOV drop into an authoring-time error, and add a confusability lint

**Problem.** OOV abbreviations: `cqb`, `pdc` → `[unk]`, with the shipped workaround "spell phonetic aliases by hand". The developer gets **no signal from any layer** that the word was dropped.

**Mechanism.** Two parts. (a) `Recognizer::UpdateGrammarFst` emits `KALDI_WARN "Ignoring word missing in vocabulary"` for every grammar entry not in the word symbol table — and `vosk_set_log_level(-1)` at `NativeBridge~/src/vosk_bridge.cpp:215` (mirrored at `Runtime/EditorMicBackend.cs:75`) suppresses it. `vosk_model_find_word` is declared at `NativeBridge~/include/vosk_api.h:21` and **is exported by the shipped arm64 binary** per `02-verified-facts.md`; it is called nowhere in the repo. Add `vosk_bridge_find_word(const char*)` to the ABI, iterate `GenerateGrammarJson`'s entries at `Configure` time, and raise an Editor error naming the word. (b) The same pass is the natural home for a *confusability* lint: run G2P over the authored vocabulary at Editor time (`g2p_en` is Apache-2.0) and flag phone-edit-distance-1 pairs across intents — which would have caught `cease fire`/`safe five` and `switch to`/`switch two` before shipping.

**Grounding.** R2 finding 6 / recommendation 2 (the OOV half); R2 finding 12 use 1 and R5 item 13 (Microsoft's Grammar Confusability Metric, US7844456B2) for the lint half. Note `02-verified-facts.md` corrects R2 here: the missing `graph/words.txt` is **not** a defect — VOSK reads the symbol table from the FST for this model family, and grammar mode is in production.

**Code impact.** Stage 4 + authoring. One C export in `vosk_bridge.cpp`, one P/Invoke in `BridgeNative.cs`, one Editor-side validation pass alongside the five that already run at parser construction (`VoxrCommandParser.cs:732,868,1026,1080,1644` [audit]). The lint half is pure Editor C# plus a baked pronunciation table (a few KB). No device behaviour change.

**Expected effect.** Converts a documented mystery into a named error: **certain by construction**. Accuracy effect: none directly — this does not fix OOV, it makes it legible. Lint recall on real grammars: **unmeasured**.

**Cost.** ~30 lines for (a) plus a test; ~1 week for (b) including the G2P table. Zero runtime cost, zero APK cost on device (the lint is `[Conditional("UNITY_EDITOR")]`-shaped).

**Risk and kill criterion.** The lint must not become noise: `VoxrCommandParser` already emits five warning classes and `KNOWN_LIMITATIONS.md:308` [audit] records that they re-emit on every active-set switch. **Kill (b)** if it fires on more than a handful of pairs in the demo grammar without the developer agreeing they are hazards.

**Cheapest validating experiment.** For (a): an EditMode/PlayMode test asserting a grammar containing `cqb` warns by name and one containing `cease` does not. Runs entirely in the host project, no device. For (b): run the lint over the maintainer's own field grammar and check whether it rediscovers the aliases already hand-written (R4 P3's validation shape).

**Migration.** New warning text in a shipped player only if `RunValidationWarnings`' non-conditional pattern is copied — do not; make this Editor-only. `KNOWN_LIMITATIONS.md`'s OOV entry gains a detection story.

---

### P6 — N-best parsing with an agreement-and-margin confidence replacing the flat MBR `conf`

**Problem.** All three documented substitution classes at once (`switch two weapons`, `fall modes`, `safe five`) plus the flat-0.50 confidence limitation.

**Mechanism.** `vosk_recognizer_set_max_alternatives(N)` is declared at `NativeBridge~/include/vosk_api.h:29` and **exported by the shipped `libvosk.so`** (`02-verified-facts.md`), and is called nowhere. Setting it switches `GetResult()` from `MbrResult` to `NbestResult`, emitting `{"alternatives":[{"text":…,"confidence":<likelihood>,"result":[…]}…]}`. Run the existing parser over each alternative and keep the best-scoring **command** rather than parsing the best-scoring **transcript**. Then derive two signals the current pipeline has no equivalent of: **parse agreement** (all N alternatives yield the same intent ⇒ robust; different intents ⇒ a sibling tie discovered acoustically rather than structurally) and **posterior margin** (softmax over the alternatives' likelihoods; gap between the best-parsing alternative and the next).

**Grounding.** R1 recommendation 1; R2 finding 3 / recommendation 1. Published analogue: word-confusion-network input to an SLU parser lifts SLURP intent F1 **0.73 → 0.77** (Villatoro-Tello et al. 2023, R2 finding 11) — real but modest for a symbolic parser; R2 argues the gain here should be larger because VoXR's failures are specifically homophone substitutions, which is exactly what sits at rank 2.

**Code impact.** Stages 3→6, and this is the widest-blast-radius proposal in the list. `vosk_bridge.cpp` after each recognizer construction (`:235`, `:373`) and `EditorMicBackend.cs:110,288`. **Three concrete traps:**
1. **`minConfidence` deletes itself.** In N-best mode VOSK writes only `word`/`start`/`end` — no per-word `conf` (R2 finding 3, load-bearing). `ComputeConfidence` (`Runtime/Commands/VoxrCommandParser.cs:4090-4113`) returns `-1f` when the confidence dictionary is empty (`:4093-4094`), and `-1` **bypasses the `minConfidence` gate entirely** (`scoring.md:252-256,276` [audit], gate at `VoxrCommandRecogniser.cs:980` [audit]). So switching on N-best without replacing the confidence signal in the *same change* silently removes a shipped gate from every command.
2. **The JSON parser will pretend nothing changed.** `VoxrJsonParser.ParseWordsFromJson` takes the *first* `"result"` array in the payload (`Runtime/VoxrJsonParser.cs:52-64`) and `ParseTextFromJson` the *first* `"text"` (`:148-151`) — both of which, in an `alternatives` payload, belong to alternative 1. The pipeline would keep working, look correct, and be 1-best-minus-confidence.
3. `ParseWordsFromJson` allocates `new VoxrWord[count]` per final (`VoxrJsonParser.cs:73`); N alternatives multiply that. The zero-allocation steady-state pin (`ZeroAllocPollPathTests.cs` [audit]) covers the poll path and must be re-checked.

**Expected effect.** WCN→SLU: measured (0.73→0.77 F1, SLURP, different parser). Recovery rate on VoXR's own substitutions: **unmeasured** and directly measurable — see the experiment.

**Cost.** 2–3 weeks including the confidence replacement. Decoder CPU: `ShortestPath` over N=3–5 on a grammar-restricted lattice is cheap relative to decoding (R2), but see P7 — at `--lattice-beam=2.0` the alternatives will be near-duplicates and the whole thing is worthless. Parser CPU ×N, off the render thread, once per final. APK 0.

**Risk and kill criterion.** Beyond the three traps: the parser's confidence table is built once per utterance keyed by word text, first occurrence wins (`VoxrCommandParser.cs:3821-3831` [audit], rule S15) — a per-alternative rebuild changes that contract. **Kill if** the experiment below shows recoveries do not clearly exceed inventions.

**Cheapest validating experiment.** R2's, exactly: in a copy of `conf/model.conf` set `--lattice-beam=6.0`, call `set_max_alternatives(3)` in the **desktop WSL harness only**, replay the acoustic corpus, and count (i) rows where the correct command is produced by alternative 2–3 but not by 1, and (ii) rows where an alternative produces a **wrong** command alternative 1 did not. Ship only if (i) ≫ (ii). No C# and no ship-path code is touched. Decide what replaces `minConfidence` *before* writing any C#.

**Migration.** `VoxrCommand.Confidence` changes meaning; `api/command-recogniser.md` and `scoring.md:252-256,269-274` are a documentation rewrite. `minConfidence` should be superseded, not silently repurposed — the `-1` "no data" sentinel is publicly documented as "never display as a low value" (`KNOWN_LIMITATIONS.md:837` [audit]).

---

### P7 — Treat `conf/model.conf` as a shipped, versioned tuning surface

**Problem.** (a) The lattice is throttled below the point where N-best (P6) or any confidence work can carry information. (b) The endpointer splits mid-command pauses, which is the root cause of the whole `UtteranceBuffer` design and of the 2.0 s `bufferWindow` recommendation on Quest 3 — the dominant latency term in the pipeline.

**Mechanism.** `Model::ConfigureV2` registers the nnet3 decoding, endpoint and decodable option sets with a `ParseOptions` and reads `<model>/conf/model.conf`, so every flag in that text file reaches the decoder. `02-verified-facts.md` quotes the shipped file verbatim: `--max-active=3000 --beam=10.0 --lattice-beam=2.0` against vosk's own V1 defaults of `7000 / 13.0 / 6.0`, and `--endpoint.rule2/3/4.min-trailing-silence = 0.5 / 0.75 / 1.0`. Two independent, **code-free** levers. Note what `02-verified-facts.md` also settles: `vosk_recognizer_set_endpointer_mode` and `_delays` are declared at `vosk_api.h:33-35` but are **not exported by the shipped binary** — R3's claim that they are callable at runtime is wrong for this `.so`. `model.conf` is the only route without rebuilding libvosk.

**Grounding.** R2 finding 4 / recommendation 3; R3 findings 1–2; `02-verified-facts.md`.

**Code impact.** Zero code. But it exposes a live packaging bug: `ModelExtractor` keys the cache only on the model folder name (`Runtime/ModelExtractor.cs:24-27`) and validates by directory-content check (`:33-36`), so shipping a re-tuned `model.conf` under the same model name **reuses the stale extraction on every device that already ran the app**. P7 cannot ship without a version key in the cache path. That is the change: `ModelExtractor.cs:24-27`, plus a version string in the StreamingAssets zip name or a stamp file.

**Expected effect.** Lattice width → MBR posterior quality and N-best diversity: mechanism established, magnitude **unmeasured**. Endpoint rules → mid-command splits and time-to-final: mechanism established, magnitude **unmeasured**. R3 notes that on a grammar graph, rule2 also requires a final state with good probability (`max_relative_cost`, left at Kaldi's default), so this is already grammar-aware endpointing — VoXR just cannot see the verdict, which is why the eager flush had to be rebuilt at the transcript level.

**Cost.** Hours to sweep; days to fix the cache key. CPU cost of a wider beam is real and **unmeasured on Quest** — the WSL harness gives a desktop lower bound only.

**Risk and kill criterion.** Both levers cost CPU or latency against a 72–90 fps budget. Widening the lattice also widens the set of in-grammar words a cough can be mapped onto, so P7 and P4(b) must be evaluated together. Shorter endpoints increase splits, which VoXR currently pays for **twice** (once in the endpointer, once in the 2 s buffer). **Kill a setting** if desktop RTF headroom shrinks below the margin the human's on-device check requires — and note that the Quest RTF number is **human-only, deferred**.

**Cheapest validating experiment.** Two independent sweeps on the WAV corpus in WSL, one at a time (R2 rec. 3): (a) `--lattice-beam ∈ {2.0, 4.0, 6.0}` against per-utterance decode wall-clock and command outcome; (b) `--endpoint.rule2.min-trailing-silence ∈ {0.5, 0.8, 1.2}` against the count of multi-final utterances and time-to-final. **Human-only, deferred:** the on-device RTF at the chosen beam.

**Migration.** The model zip becomes a versioned artefact. `Documentation~/troubleshooting.md` and the install docs must say the cache key changed, or the first upgrade after this silently keeps the old decoder config.

---

### P8 — Gap-free grammar swap: `set_grm` **plus** keep capture running **plus** re-prime the i-vector

**Problem.** The ~50 ms+ "audio gap" on set switching and dynamic-slot change — and a second, undocumented cost hidden inside it.

**Mechanism, and a correction to the seed.** Today `vosk_bridge_set_grammar` refuses while running (`NativeBridge~/src/vosk_bridge.cpp:352-353`), frees the recognizer (`:356-359`), rebuilds with `vosk_recognizer_new_grm` (`:363`) and re-applies `set_words` (`:373`); `GrammarManager.ForceApply` therefore does stop → set → start (`Runtime/Commands/GrammarManager.cs:43-52`), and `EditorMicBackend.SetGrammar` carries the same comment, "VOSK has no grammar-swap API" (`Runtime/EditorMicBackend.cs:268`) — which is **false for the shipped binary**: `vosk_recognizer_set_grm` is exported (`02-verified-facts.md`) though **not declared** in `NativeBridge~/include/vosk_api.h` (compare `:19-45`), so any use must add the declaration or vendor the 0.3.45 header. But the seed's premise that `set_grm` is "gap-free AND i-vector-preserving" is **half wrong, and R2 and R4 agree**: `Recognizer::SetGrm` deletes `decode_fst_`, re-runs `UpdateGrammarFst`, and **rebuilds the decoder, the feature pipeline and silence weighting**, resetting `samples_processed_`/`frame_offset_` (R2 finding 5); R4 D8 states the consequence in full — the shipped model carries a full i-vector extractor (`ivector/final.ie`, 8.29 MB of the 40 MB; `02-verified-facts.md` confirms the model uses online i-vectors), Kaldi estimates it left-to-right accumulating speaker history, and **every grammar rebuild returns the decoder to the speaker-independent prior**. So the documented cost of a set switch is understated: it is a gap *and* an adaptation reset. Three-part fix: (1) call `set_grm` instead of free-and-recreate — saves the recognizer alloc/free but **not** the FST composition, which dominates and happens either way; (2) **keep `AudioRecord` running and keep writing into the ring** across the swap, so no samples are lost at the audio layer — this is where the dropped "fall back from" words actually go, and it is the larger win; (3) after the swap, replay 1–2 s of the retained post-AGC 16 kHz history through the new recognizer with results discarded, so the i-vector and online-CMVN restart warm. (3) also fills the gap with useful work.

**Grounding.** R2 finding 5; R4 D8/P1; `02-verified-facts.md` (exports, header mismatch, i-vector directory); code as cited.

**Code impact.** Stage 3/4, native, and it is a **concurrency change**, not a config change: `SetGrm` refuses while `RECOGNIZER_RUNNING`, so the swap must be sequenced against `accept_waveform_s` on the recognition thread (`vosk_bridge.cpp:125`) while all bridge state is file-scope static (`:20-42`). Adds a 1–2 s rolling history buffer (~32 KB). `vosk_api.h:19-45` gains a declaration. The Editor path (`EditorMicBackend.cs:269-288`) does not exercise any of it, so this is **device-behaviour-only** — and per the verification bindings, any native-bridge change is human-only on-device verification, reported as deferred.

**Expected effect.** The recognizer-alloc saving: small, **unmeasured** — R2 says the FST composition dominates. Capture-continuity: eliminates the audio-layer loss **by construction**. Adaptation recovery: **unmeasured**; the i-vector SAT literature's ~10 % relative WERR is `UNVERIFIED` in R4 and is a different model on a different corpus.

**Cost.** 2–3 weeks including the concurrency work. Runtime: a ≤2 s decode at switch time (well under 1 s of CPU at the model's 0.11×RT desktop figure, more on Quest); steady-state zero.

**Risk and kill criterion.** A swap racing `accept_waveform_s` on file-scope state is the highest-severity correctness risk in this list; it argues for doing P9 (handles, explicit ownership) first or together. Re-priming can adapt to the *wrong* speaker in a shared-headset or spectator-audio scenario. **Kill (3)** if the experiment below shows the adaptation delta is ~0.

**Cheapest validating experiment.** R4 D8's, offline and decisive: replay the acoustic corpus twice through the real decoder — once as **one continuous recognizer session**, once **recreating the recognizer between every utterance**. The delta *is* the adaptation being thrown away. If it is ~0, drop part (3) and keep (1)+(2). Separately, measure the gap as *lost samples* between two fixture replays across a `set_grm` call, entirely in WSL (R1 rec. 1a). **Human-only, deferred:** the on-device gap duration and continuity.

**Migration.** `KNOWN_LIMITATIONS.md`'s "Active set switching has a brief audio gap" entry, `Documentation~/command-sets.md`'s "The Audio Gap" section, and `EditorMicBackend.cs:268`'s comment all become wrong. `R23` (grammar always reflects the full slot universe, `api/command-recogniser.md:59` [audit]) is unaffected.

---

### P9 — Handle-based bridge ABI and a real decoder seam

**Problem.** "Single recogniser per process" is a shipped, documented public contract (`api/speech-recogniser.md:7-42` [audit]) whose cause is purely mechanical — and it blocks three separate things: the parallel free-vocabulary competitor path that is the only route to a *rejection transcript* (P11), an on-device A/B between two decoders, and a clean home for the swap sequencing in P8.

**Mechanism.** Every bridge entry point takes no handle and mutates file-scope statics (`NativeBridge~/src/vosk_bridge.cpp:20-42`: `g_model`, `g_recognizer`, `g_ring_buffer`, `g_result_queue`, `g_audio_capture`, `g_downsampler`, `g_agc`, `g_recognition_thread`, `g_running`, `g_push_mode`, `g_input_level`, `g_last_error`, `g_last_partial`, `g_current_result`). The C# side enforces the same rule with a static owner (`VoxrSpeechRecogniser.cs:53` [audit]) checked at twelve sites. Introduce an opaque `VoxrBridgeHandle` returned by `init`, with each session owning its ring, queue, DSP, recognizer and thread; keep exactly one *capture* device (that constraint is real) and let extra handles be push-fed or share the ring by fan-out. In parallel, extract the `IDecoder`-shaped seam the audit says does not exist: the decoder choice is a `#if UNITY_EDITOR_WIN` fork repeated at **nine** independent sites in `VoxrSpeechRecogniser` (`:100,135,196,255,351,381,420,441,510` [audit]), so swapping a decoder today means editing all nine.

**Grounding.** `01-code-audit.md` §4.2 ("no decoder interface"), §8.1 (mechanical cause); R2 rejection option C ("requires fixing it first"); R1 §2 (sherpa-onnx's C API explicitly supports multiple streams off one recognizer, so a migration would fix this for free — which is an argument for doing the seam *before* deciding the migration, not after).

**Code impact.** Stages 1–4 and the whole lifecycle. `vosk_bridge.h/.cpp` entirely; `Runtime/Native/BridgeNative.cs`; nine forks in `VoxrSpeechRecogniser`. The push-audio pair already exists and is *unused from C#* (`BridgeNative.cs:53,60` [audit], comment "No runtime caller yet"), and `vosk_bridge_has_result` (`vosk_bridge.cpp:379`) has no binding at all — a handle-based rewrite is the moment to either wire or delete them. `vosk_bridge_push_audio` returns a **negated** error code unlike every other binding (`vosk_bridge.cpp:320-326`, warned about at `BridgeNative.cs:55-58` [audit]) — fix the inconsistency here or never.

**Expected effect.** Enables P11's competitor path and on-device A/B; no accuracy or latency effect on its own. **Unmeasured** by construction.

**Cost.** 3–4 weeks. Runtime cost zero. This is the largest pure-refactor item and buys no user-visible improvement by itself — sequence it only when something downstream needs it.

**Risk and kill criterion.** It rewrites the one part of the system with no automated coverage at all (`01-code-audit.md` §7: no unit tests for `ring_buffer.h`, `result_queue.h`, or the `vosk_bridge.cpp` state machine; the harness exercises the desktop build only) and changes a published API contract including the exact non-owner behaviour. **Do not start it speculatively** — kill unless a committed downstream proposal (P11-C or a decoder migration) needs it.

**Cheapest validating experiment.** Extend `NativeBridge~/harness/main.cpp` to open two handles, replay two fixtures concurrently in push mode, and assert independent transcripts. Pure WSL. **Human-only, deferred:** on-device capture with two live sessions.

**Migration.** `api/speech-recogniser.md`'s one-recogniser rule and its non-owner semantics (`:7-42`) are published behaviour; relaxing them is a documentation rewrite and arguably a minor-version API addition. Keep the single-owner *capture* rule.

---

### P10 — Run the eager scan on partials, and restore per-word data on partials

**Problem.** Latency. The additive floor at default settings is ~20 ms capture + up to 85 ms decode chunk + 0–10 ms thread sleep + VOSK decode + ≤1 frame + **500 ms `bufferWindow`** + ≤1 frame; at the tooltip-recommended Quest 3 setting of 2.0 s the buffer dominates by two orders of magnitude (`01-code-audit.md` §2, constants at `vosk_bridge.cpp:45,94`, `audio_capture_audiorecord.cpp:13`, `VoxrCommandRecogniser.cs:68-72` [audit]). Even the eager path cannot beat the endpointer's ≥500 ms trailing-silence wait.

**Mechanism.** The bridge already pushes de-duplicated partials to C# (`vosk_bridge.cpp:136-140`). But `HandleResult` — the only path that appends to `UtteranceBuffer` and calls `ProbeEagerCommit` (`VoxrCommandRecogniser.cs:566`, `:592-599` [audit]) — is subscribed to `OnResult`; the partial handler is **compiled out of players entirely** (`Runtime/Commands/VoxrCommandRecogniser.cs:1328-1334`, subscription guarded at `:502-504` and `:520-522`). Run the same `TryEagerCommit` scan against `buffer + latest partial`, so a `Commit` verdict can fire *before* the endpointer. Pair it with `vosk_recognizer_set_partial_words(1)` (declared `vosk_api.h:31`, exported per `02-verified-facts.md`, called nowhere) so eager condition 7 (confidence) has data instead of being bypassed as `-1`. Add a LocalAgreement stability rule — commit a prefix only once two successive partials agree on it (R1 finding 8: the one reusable idea from the Whisper line).

**Grounding.** R3 executive summary and finding 3; R1 finding 8; the seven-condition `TryEagerCommit` conjunction and its two `Commit`-only extras are published as exhaustive (`scoring.md:338-361` [audit]) and transfer unchanged — they were designed for exactly this hazard class.

**Code impact.** Stages 5–6 plus one bridge call. Pure C#/bridge, no model, no APK. **Editor/device parity is the point here:** un-guarding `:502-504` changes player behaviour from "partials never reach the command layer" to "partials drive commits", which is a behaviour change no existing test covers (the PlayMode suites all run in the Editor, where the handler already exists).

**Expected effect.** Removing the endpointer wait for unambiguous complete commands: R3 estimates **−500 to −1000 ms**, explicitly **unmeasured**. Reference points that are measured, on other systems: FastEmit p90 emission latency 210 → 30 ms; a unified ASR+endpointing head, median EP latency −120 ms (−30.8 %). Target: R3's design target of ~200–300 ms, from Stivers et al.'s cross-linguistic 200 ms turn-transition peak.

**Cost.** ~1 week. CPU: one extra speculative parse per distinct partial; partials are already de-duplicated in the bridge and R2's own header note puts push frequency at ~4/s.

**Risk and kill criterion.** Partial hypotheses are unstable and constantly revised — a command could fire on a transcript the final revises. G4 (`scoring.md:345,355` [audit]) exists precisely to protect the buffer: committing clears the accumulated transcript, so a wrong early commit discards a half-spoken command and parses its continuation as a separate utterance. **Kill if** the LocalAgreement rule cannot hold the wrong-commit rate at zero on the corpus; a `HoldExtendable`-only variant (never `Commit` from a partial, only shorten the wait) is the safe fallback.

**Migration.** `scoring.md:330` states "each VOSK result triggers one speculative parse of the buffer", true of finals only — a documentation correction. `OnPartialResult`'s documented main-thread delivery is unchanged.

---

### P11 — One calibrated accept/reject score, reported before it gates

**Problem.** `minScore` (0.6) and `minConfidence` (0.4) are two hand-tuned gates over quantities with incomparable units, and **neither can move**: `minConfidence` is pinned below 0.5 because "two" scores ≈0.50 essentially always (`KNOWN_LIMITATIONS.md:121` [audit], `scoring.md:286-289`), and `minScore` above ~0.7 breaks three-element patterns.

**Reframing — this is the most important single fact in the research pass.** R2 finding 2 establishes from vosk-api source that per-word `conf` is a Kaldi MBR posterior computed over a lattice that is **already grammar-constrained**. It answers "given that the speaker said something this grammar can represent, which word was it" — never "was this in the grammar at all". An independent user report (vosk-api #741) shows out-of-grammar speech returning in-grammar words at confidence ≈1.0. So `minConfidence` is **structurally incapable** of rejecting noise, and 0.50-on-"two" is not a defect: with both "to" (a required literal) and "two" (from `VoxrNumberParser.DigitVocabulary`, added at `VoxrCommandParser.cs:3137-3145`) in the grammar, the MBR sausage splits mass evenly and 0.50 is the *correct, well-calibrated* posterior for a genuine two-way ambiguity. Two consequences: the docs are currently wrong in spirit about what `minConfidence` does, and 0.50-with-an-in-grammar-homophone is a **usable signal** — it is a sibling tie detected acoustically rather than structurally, which is the same signal P6's parse-agreement produces more cleanly.

**Mechanism.** Build the 2011-shaped thing, not a neural model: one scalar `P(command correct)` from a small logistic regression over features the system already computes or cheaply gains — min/mean per-word MBR `conf` over the span; N-best posterior margin and parse agreement (P6); match score, coverage charge, admission margin; leading-required-miss and sibling-tie flags; span duration and words/second from `start`/`end` (parsed today at `VoxrJsonParser.cs:86-87` and **never consulted by any scoring rule**, `01-code-audit.md` §3.2); VAD speechiness over the span (P3). **Ship it as a reported number on `VoxrCommand` and in the session log, gating nothing**, until there is field evidence.

**Grounding.** R2 finding 11 and recommendation 5 (Yu/Li/Deng TASLP 2011 for the post-hoc form; Qiu et al. ICASSP 2021; Li et al. ICASSP 2022 for why it breaks under mismatch). R1 finding 10 for the future: entropy-based word confidence is **2× (CTC) / 4× (RNN-T)** better than max-per-frame probability at detecting incorrect words on LibriSpeech (Laptev & Ginsburg, SLT 2022) — available essentially free the moment the decoder is CTC or transducer, and *not* available from VOSK. R4 P2 for the per-user variant: a 30-second enrolment of 5–8 phrases turns raw `conf` into a per-user z-score, which is comparable across words in a way the raw value is not.

**Code impact.** Stage 6 plus a new field on a public readonly struct (`VoxrCommand.cs` [audit] — adding a field is source-compatible, changing one is not). ~30 floats at runtime; the work is labelling and fitting, both Editor/desktop. No native change.

**Expected effect.** **Unmeasured.** Reference points: post-hoc calibration cuts ECE 0.086 → 0.036 (−58 %) and overconfident mass 11.1 % → 6.6 % at −18…−5 dB SNR (Huo et al. 2025, Whisper-medium — must be re-derived per model).

**Risk and kill criterion.** R2 names it and it is decisive: a calibrator fitted on TTS fixtures will be **mis**calibrated on Quest-mic human speech. The corpus is synthetic and the field set is 73 utterances from **one speaker** — enough to fit 3–5 features honestly, not 20. **Kill if** a 4-feature model does not beat the two-threshold rule on AUC and ECE on a held-out split of 699 rows; it will not beat them in the field either.

**Cheapest validating experiment.** Offline, no shipping change: dump per-attempt features from the existing session logs, label each row from existing pins, fit a 4-feature logistic regression, report AUC and ECE against the current rule on a held-out split. Separately and cheaper: R2's red-then-green pin for the 0.50 claim — force a grammar containing "two" but not "to" and check whether the confidence rises. If it does not, the whole homophone-signal reframing is wrong.

**Migration.** Additive at first. If it ever gates, `scoring.md`'s `OnUnrecognisedSpeech` fire/silent table is published as **exhaustive** (`:309-321` [audit], "those three are the only filters that suppress it") — a new suppressor invalidates a published closed enumeration.

---

### P12 — Build the instrument: adverse-condition corpus, negative hours, sealed holdout

**Problem.** Not a recognition failure — a measurement failure that blocks P1, P2, P3, P4, P6 and P7 from being *decidable*. R6: the 699-row A/B corpus is text-only (the rig stages parser `.cs` files and zero VOSK code), so every scoring change to date was measured against a decoder held constant *by construction*; the acoustic corpus is **16 fixtures from one Piper voice** with a 95 % Wilson half-width of **±0.097 at 16/16** and **±0.163 at 14/16**; and there is **no negative-hours denominator anywhere**, which means the leading-miss bar shipped in 2.0.0 on "zero observed legitimate recoveries in 73 utterances" with no false-accept rate attached at all.

**Mechanism.** Four parts, in order of cost. (1) **Negative corpus and an operating point**: hours of game audio, breathing, exertion, room tone, out-of-set chatter and non-speech events, scored as **false accepts per hour**, with false-reject rate reported *at a fixed FA/h*. Reference operating points: Picovoice's public benchmark fixes 1 FA per 10 h; openWakeWord targets <5 % FRR at <0.5 FA/h (R5 item 9). (2) **Adverse conditions**: mix real game audio at +20/+10/+5/0 dB SIR and MUSAN noise (CC BY 4.0) into the existing fixtures and replay through the real DSP + decoder via the push-audio bridge; Raju et al. measured **30–45 % relative false-reject reduction** from playback-interference augmentation, so this both sizes the hazard and tells you whether AEC is worth building. (3) **Dual reporting, CHiME-4 style**: every metric reported twice (TTS, human) and ranked only on human — because Timers-and-Such measures the synthetic/real gap at **81.6 % vs 68.0 %** and Hilmes et al. at **+6.3 to +18.7 pp**, *architecture-dependent*, so a TTS-measured delta does not even transfer between decoders. (4) **A sealed holdout**, per Dwork et al. (*Science* 2015): the 16 fixtures and the 699 rows are the *training* set for the scoring rules, and every threshold sweep accepted because the suite went green has spent some of that suite's validity.

**Grounding.** R6 throughout; R4 D12 for the augmentation half; R5 item 14 (Amazon's annotation sets + build-time utterance-conflict detection) for the DX shape.

**Code impact.** Stage 8 tooling only. Extends `Tests~/Fixtures/generate.py`, the WSL harness, and the Editor batch runner. One substantive product change: `VoxrBatchTestRunner.CheckSlots` is exact string equality [audit], so a `{target}` extracted as "hotel one" vs "hotel 1" scores zero, indistinguishable from extracting nothing — adopt SLU-F1's partial-credit stance so the gradient a change moves along is visible.

**Expected effect.** No accuracy effect. It is the precondition for every other number in this document being trustworthy.

**Cost.** 2–3 weeks for (1)+(2)+(4). (3) is a reporting rule, ~free, but only works once a human corpus exists that is big enough to rank on. **The expensive part is human recordings** — R6's statistics: a paired A/B needs **six utterances all flipping the same way** to reach significance at all (2 × 0.5⁶ = 0.031; five is never significant), and detecting 90 %→95 % at 80 % power needs ≈435 utterances per arm unpaired, ≈290 paired at 10 % discordance. Multi-speaker, in-headset, Lombard/exerted recordings are **human-only** and are the single largest cost in this whole document.

**Risk and kill criterion.** Digitally mixed game audio is not Quest's real speaker→mic transfer function, which Meta does not publish (R4 D6/A7) — it is a lower bound on difficulty, not a model of it. Statistics on 10 sessions from one speaker must use a **blockwise** bootstrap with session as the block (R6), and even then sits at the method's small-sample limit. **Kill nothing** — but do not let this become a second test framework nobody adopts (R5's own risk note): every part must be wired into the existing WAV-replay/push-audio path, not beside it.

**Cheapest validating experiment.** Recursive but real: re-run one already-shipped decision (the leading-miss bar's 9-lost / 39-suppressed) against the new instrument and see whether the conclusion survives. If it does not, the instrument has already paid for itself.

**Migration.** `Documentation~/editor-testing.md` already says the right thing ("detects changes in behaviour, not absolute recognition quality"); nothing enforces it. Make it enforced.

---

## Decoder decision

Common facts that apply to every row. Meta does **not** expose the Hexagon NPU to third-party Quest apps, so the ONNX Runtime QNN EP is moot and every option is CPU-only (R1 §"What this area cannot fix"). **No published RTF in the entire research pass was measured on XR2 silicon**; the nearest ARM datapoint (int8 120 M streaming RNN-T, 217 ms post-speech, ORT CPU EP/XNNPACK) is a Galaxy S23 Ultra, materially stronger than XR2 Gen 2's 2 performance + 4 efficiency cores. Any plan that budgets CPU from a published RTF is building on sand. And the field evidence base — 73 utterances, one speaker — cannot validate a model choice; model selection will out-run the evidence unless P12 lands first.

| Option | Accuracy evidence | Grammar mechanism | Size / runtime | Licence | What the bridge and grammar generator become | Unknowns | Settling experiment |
|---|---|---|---|---|---|---|---|
| **Stay on VOSK small (recommended now)** | 9.85 % LS test-clean / 10.38 % TED (measured, model card). ~half the shipped C API is unused: `set_grm`, `set_max_alternatives`, `set_partial_words`, `model_find_word`, `set_spk_model` all exported (`02-verified-facts.md`), none called | `new_grm` / `set_grm` bag-of-phrases with bigram counts — already weighted, just not used (P4) | 40 MB model + 8.9 MB `libvosk.so`; ships today | Apache-2.0 | Unchanged except declarations (`vosk_api.h:19-45`) and P8's sequencing | Quest RTF at wider lattice beam; whether N-best actually contains the right words | P6's and P7's sweeps. Both are hours of WSL work and settle most of the accuracy question without a migration |
| **VOSK `en-us-0.22-lgraph`** | 7.82 % / 8.20 % — **~21 % relative WERR, measured** on read speech; dynamic graph retained so grammar mode still works | Identical — drop-in | **128 MB** vs 40 MB: +88 MB APK; RAM and grammar-rebuild time on Quest **unmeasured** | Apache-2.0 | **Zero code change** | Whether a bigger LM helps at all on short commands over a low-gain mic under exertion — R4/R1 both warn it may *hurt* (more vocabulary to confuse) | Swap the model directory, replay the corpus, diff the four documented confusion pairs. One afternoon in WSL. Do this before anything harder |
| **sherpa-onnx streaming Zipformer-CTC + HLG** | The structural analogue of `new_grm`: a real closed vocabulary with per-arc weights. WeNet's `T∘min(det(L∘G))` measured LS test-other 6.53 → 5.98 with an n-gram G | HLG compiled offline (k2/OpenFST) in the Unity Editor → **audio gap gone by construction** (swap a pre-compiled graph) | ~15 MB `libonnxruntime.so` + 3.7 MB jni; small **zh** int8 CTC is 25 MB | Apache-2.0 (models + runtime) | `GenerateGrammarJson` (`VoxrCommandParser.cs:3076-3175`) becomes a `G.fst` emitter: required runs → mandatory arcs, `?optional` → epsilon-skippable, choice slots → weighted alternation, NumberSequence → digit loop, `[unk]` → an explicit garbage arc with a tunable cost. The bridge becomes an `IDecoder` implementation (P9) | **Blocker: no small English streaming zipformer-CTC model is published.** Dynamic runtime slot values have no clean story — Kaldi-style `GrammarFst` nonterminals exist but the reference implementation (`kaldi-active-grammar`) is **AGPL-3.0**, unusable here | R1 rec. 4: write the `GenerateGrammarJson` → OpenFST `G.txt` translator in C#, compile one command set's HLG with k2 in WSL, decode the corpus through the Python HLG example. A day's work, zero production code — answers "is our pattern language expressible as an FST" |
| **sherpa-onnx streaming Zipformer-transducer + hotword biasing** | **3.94 / 9.79** (20 M, greedy, 320 ms chunk) and **3.06 / 7.81** (66 M) vs VOSK's 9.85 — ~2.5× relative error reduction at comparable int8 size, measured (icefall RESULTS.md). Biasing gains measured elsewhere: AISHELL-2 `test_p` 14.94 → 6.17; CTC-WS F 0.87, WER 14.02 → 10.48 at 12× the speed of beam-search biasing | **Soft** bias (Aho-Corasick context graph), not a closed vocabulary. Restores wrong-mode explainability — out-of-set speech transcribes as itself instead of collapsing to `[unk]` | ~42 MB int8 model + ~19 MB runtime ≈ 61 MB vs today's ~49 MB — **+12 MB, not a doubling**. Multiple streams per recognizer, so P9 comes free | Apache-2.0 | Same as above, plus: the closed-set *rejection* property moves from the decoder to stage 6, and `[unk]` — a magic string read at nine sites in the parser (`VoxrCommandParser.cs:139` and eight more [audit]) — has no equivalent | No published WER for the exact ONNX en-20M package. `modified_beam_search` is required for hotwords, costs more than greedy, and has open reports of **empty/hallucinated text on silence** (sherpa-onnx #845, #3267) — the exact hazard VoXR already suffers. Alpha Cephei is migrating VOSK to this stack, but **no English Zipformer2 VOSK model exists yet** | R1 rec. 3 / R2's closing note: run `sherpa-onnx-streaming-zipformer-en-20M` int8 offline in WSL over the same corpus, through the same 48→16 kHz decimation and AGC, and diff transcripts against VOSK's on the documented confusion pairs. **Unconstrained first** — the grammar question is separable and must not gate the measurement. Then a separate arm64 build and one on-device encoder-chunk timing (**human-only, deferred**) |
| **sherpa-onnx KWS-only (Zipformer 3.3 M)** | Deletes the audio gap *and* the OOV problem in one move: keywords are supplied **per stream** as token sequences, so `cqb`/`pdc` need no hand-written aliases, and each keyword has a real **trigger threshold** — the per-phrase rejection knob grammar-mode VOSK simply does not have | Hardest constraint: the model can only emit the supplied phrases | **~5 MB int8** — 8× smaller than today's model | Apache-2.0 | Catastrophic for stage 6: output is *hits with timestamps*, not a transcript. NumberSequence ("two seven zero" → ten digit keywords whose ordering the parser reassembles), sequential multi-command extraction, coverage charging and sibling ties would all be rebuilt on a hit stream | **No published recall / false-alarm numbers** for these models — a fatal evidence gap for a false-trigger-sensitive application. Worse, R3 surfaces disqualifying evidence: open-vocabulary KWS **over-weights initial phonemes** (POB-Spark EER 64.4 % → 29.3 % once corrected), and `switch to weapons` / `switch to navigation` is exactly a shared-prefix negative pair | Only worth running as a **parallel high-precision anchor** alongside a transcript decoder, not as a replacement. Measure FA/h at usable recall on P12's negative corpus before considering anything else |
| **Moonshine v2 Tiny** | 4.49 / 12.09 LS, 12.01 % 8-set average at **33.57 M**; 50 ms response latency at 8.03 % compute load — on a **MacBook M3**, not ARM | **None usable.** Encoder-decoder: no CTC posterior, no transducer lattice, so neither HLG nor Aho-Corasick applies. The only mechanism is constrained beam search over decoder tokens | Under the 40 MB budget; no arm64 int8 export identified | MIT | Would need decoder-side token masking, which has no proven C++ implementation | Whether masking actually binds — whisper.cpp's GBNF grammar has a filed, reproduced report that constrained output is **byte-identical to unconstrained** (#2159) | Closed unless someone demonstrates masking binding in a C++ ASR runtime |
| **Whisper family** | — | whisper.cpp `stream` is a 5 s sliding window stepped 500 ms, not streaming; grammar reported non-functional | tiny needs **~273 MB RAM** per the repo's own table | MIT | — | — | **Close this lead.** RAM alone breaks the brief's constraint. The only reusable idea is the LocalAgreement commit policy (folded into P10) |

**Recommendation.** Stay on VOSK for this cycle and spend the migration budget on P6/P7 and on P12. Keep exactly two options alive with bounded offline experiments that do not touch production code: the `0.22-lgraph` swap (one afternoon) and the unconstrained 20 M streaming Zipformer diff (a few days in WSL). Do **not** commit to sherpa-onnx before those two numbers exist and P12 can rank them, and do not commit to the HLG path at all until a small English streaming CTC model exists.

---

## Rejection architecture

The recommended pipeline, with the signal each stage contributes. Stages 1–3 reduce the *rate* at which the decoder is asked to explain non-speech; stages 4–6 decide, with evidence, whether what came out is a command.

```
[1] Front end            → clean, correctly-banded, correctly-levelled audio
    P1 fixed decimator + P2 VAD-gated AGC
    signal: none (removes a train/test mismatch and a +26 dB noise boost)

[2] VAD gate (+200-300 ms pre-roll)              P3
    signal: per-frame speech probability; per-utterance speechiness;
            an endpoint signal INDEPENDENT of the decoder's blank counting
    rejects: taps, breathing, room tone, fans, most game-audio bleed
    does NOT reject: coughs (voiced), out-of-set speech

[3] Grammar decoder with weighted entries        P4 + P7
    signal: [unk] runs (out-of-grammar speech, coverage-exempt at
            VoxrCommandParser.cs:3881-3919 [audit]); endpoint verdict
    rejects: nothing by itself — a grammar WFST MUST emit an in-vocabulary
            path; this is the mechanism, not a bug (R2 finding 2)

[4] N-best evidence                              P6
    signal: parse agreement across alternatives (same intent = robust;
            different intents = an acoustically-discovered sibling tie);
            posterior margin between the best-parsing alternative and the next
    rejects: substitutions where the right words exist at rank 2-3

[5] Parser policy (unchanged)                    existing
    signal: score, coverage charge, admission margin, leading-miss flag,
            sibling-tie flag, completeness
    rejects: phantom commands (the bar), sparse matches (admission),
            argument-discarding matches (coverage)

[6] Calibrated accept/reject                     P11
    inputs: [2] speechiness · [4] agreement + margin · [5] score/coverage/flags
          · span duration and words/second from the per-word start/end that
            VoxrJsonParser.cs:86-87 already parses and nothing reads
    output: ONE number, P(command correct), reported before it ever gates
```

**Optional stage 3b — the parallel competitor path (behind a developer flag, requires P9).** Run a second recognizer off the *same* refcounted `VoskModel` in **free-vocabulary** mode and accept the grammar result only when its likelihood is competitive with the free path's. This is the classic likelihood-ratio / utterance-verification architecture in its cheapest available form, and it is the **only** option that produces a *rejection transcript*: "in weapons mode, say 'approach target alpha one'" comes back from the free path as approximately `approach target alpha one` while the grammar path returns `[unk] target alpha one` — which is exactly what `KNOWN_LIMITATIONS.md`'s "set restriction cannot produce meaningful rejection transcripts" says is impossible today. Cost: roughly **doubles decode CPU** (R2 option C), which is why it is a flag and not a default; a bounded variant runs the free path only over a short window, triggered when the grammar path returns a short, low-scoring result.

**Operating point, as R6 demands.** Define acceptance as: **≤ 1 false command per hour** of continuous listening against the negative corpus (game audio at realistic SIR, breathing, exertion, out-of-set chatter, non-speech events), at **≤ 5 % false-reject rate** on in-set commands — reported CHiME-4 style, twice (TTS and human), and **ranked only on human**. Reference points for the numbers themselves: Picovoice's public wake-word benchmark fixes 1 FA per 10 h; openWakeWord targets <5 % FRR at <0.5 FA/h; the one comparable deployed Vosk-grammar + Silero system measured 11 % FP / 4 % FN in its own environment and tuning, which must **not** be quoted as VoXR's expected rate. Today VoXR has **no negative-hours denominator at all**, so this operating point cannot be evaluated until P12 exists — that is the point of stating it now.

**What no stage here fixes, and it must be said.** The dropped-word invisibility survives everything above: an in-grammar word VOSK drops leaves no token and no timing hole, so nothing distinguishes "never spoken" from "spoken and lost". N-best *weakens* it — a word present in alternative 2 but not alternative 1 is that distinction, partially observed — but does not remove it. And a sibling tie where the discriminating word was never uttered is an information-theoretic wall; asking is the correct answer.

---

## Roadmap

**Phase 0 — free facts, days, no code (do these first; several can kill later phases).**
- Classify the 73-utterance field debug logs into non-speech vs speech-like false triggers. Upper-bounds P3's entire value before the ORT dependency is taken. *(R1 rec. 2)*
- Swap in `vosk-model-en-us-0.22-lgraph`, replay the WAV corpus, diff the four documented confusion pairs. One afternoon, settles a whole decoder-decision row.
- The red-then-green pin on the 0.50 claim: a grammar with "two" but not "to". If confidence does not rise, P11's homophone reframing is wrong.
- Resolve the corpus contradiction below (R6 vs project memory) — it determines whether P4/P6 are cheap or blocked.
- **Human-only, deferred:** one Quest session recording room tone, ~30 coughs/hums/taps, game audio with no speech, and the same 10 phrases through audio sources 1/6/7/9; plus `adb shell cat /vendor/etc/audio_effects.xml` and an availability log for AEC/NS/AGC. Every front-end recommendation is currently sized against assumptions this session replaces with facts *(R4 rec. 4)*.

**Phase 1 — ≤1-week wins, independent of each other and of the harness.**
- **P5(a)** OOV validation at Configure time. ~30 lines, converts a mystery into an error.
- **P1** the decimation filter. One coefficient table, two files — but its *acceptance* waits on Phase 2.
- **P10** eager scan on partials + `set_partial_words`. The single largest latency cut available and pure C#/bridge.
- **P7** `model.conf` sweeps — but shipping a tuned conf **blocks on the `ModelExtractor` cache version key** (`Runtime/ModelExtractor.cs:24-27`), which is itself a ≤1-week fix.

**Phase 2 — the instrument (blocks the acceptance of Phase 1, and everything after).**
- **P12**. Nothing in Phase 1 can be *ranked* on a 16-fixture corpus with a ±0.097 half-width, and nothing that changes rejection can be evaluated at all without negative hours. Phase 1 work may proceed in parallel; Phase 1 **acceptance** may not.
- The human recording campaign inside P12 is the long pole of the entire document and should start as early as the maintainer will tolerate.

**Phase 3 — multi-week, ordered by dependency.**
- **P2** VAD-gated AGC → depends on P3 for the gate signal (or ships with an energy stopgap); must be evaluated *jointly* with P1 because P1 removes the +2.51 dB DC gain the AGC currently absorbs.
- **P3** VAD gate → gated by Phase 0's cough measurement.
- **P4** grammar weights → gated by Phase 2 (needs audio, and a real sample size).
- **P6** N-best → gated by P7's lattice-beam sweep (worthless at `--lattice-beam=2.0`) and must land its confidence replacement in the same change.
- **P11** calibrated score → depends on P6 (margin/agreement) and P3 (speechiness) for its best features, but a 4-feature version can be fitted on existing logs today.
- **P8** gap-free swap → depends on P9 for safe sequencing, or accepts a hand-rolled handshake against file-scope state.

**Phase 4 — only if something needs it.**
- **P9** handle-based ABI + `IDecoder` seam. Buys nothing user-visible on its own; justified by P11's competitor path, by P8's concurrency, or by a committed decoder migration — not speculatively.
- Decoder migration. Gated on: P12 existing, the two Phase-0/offline decoder measurements coming back favourable, an on-device RTF number (human-only), and — for the HLG path — a small English streaming CTC model existing at all.

**Explicitly waiting on the harness:** every claim about accuracy, every threshold re-tune, the rejection operating point, and both decoder measurements. **Not waiting:** P5(a), P10, the `ModelExtractor` version key, the Editor/device AGC unification, and every Phase-0 fact.

---

## What to stop doing

1. **Stop calling `vosk_set_log_level(-1)` unconditionally.** `NativeBridge~/src/vosk_bridge.cpp:215` and `Runtime/EditorMicBackend.cs:75` suppress Kaldi's `"Ignoring word missing in vocabulary"`, so every OOV grammar entry — every `cqb` — is dropped silently by `UpdateGrammarFst` and the developer learns about it only as `[unk]` in the field (R2 finding 1 / rec. 2). Keep it quiet in players; surface it in the Editor.

2. **Stop treating `minConfidence` as a noise gate, in code, docs and workarounds.** It is an MBR posterior over a lattice that is already grammar-constrained; it can only report *within-grammar* ambiguity (R2 finding 2, verified from vosk-api source; vosk-api #741 shows out-of-grammar speech at confidence ≈1.0). The `KNOWN_LIMITATIONS.md` workaround "use push-to-talk" for coughs is a UX concession standing in for a missing garbage model — say so, and point at P3.

3. **Stop emitting the grammar through a `HashSet`.** `VoxrCommandParser.cs:3079` throws away the multiplicity that `LanguageModelEstimator::AddCounts` reads as weight — the only free biasing lever in the current stack (R2 finding 1).

4. **Stop ranking anything on the 16-fixture corpus, and stop citing the 699-row corpus as acoustic evidence.** R6: Wilson half-width ±0.097 at 16/16 cannot distinguish 87 % from 96 %; the 699-row rig stages parser `.cs` files and **zero VOSK code**, so it holds the decoder constant by construction. Repeated tuning against a fixed suite is textbook adaptive data analysis (Dwork et al., *Science* 2015) — each green run spends some of the suite's validity.

5. **Stop maintaining two divergent DSP implementations.** `Runtime/Dsp/Agc.cs:66` gates on the instantaneous sample; `NativeBridge~/src/agc.h:53` gates on the smoothed level; the comment at `Agc.cs:63-64` refers to a "divergence note in the class summary" that does not exist (`Agc.cs:1-6`). Nothing tests them against each other (`01-code-audit.md` §7). Any AGC change validated in the Editor is validated on the wrong AGC.

6. **Stop free-and-recreating the recognizer to change grammar.** `vosk_recognizer_set_grm` is exported by the shipped `libvosk.so` (`02-verified-facts.md`); `EditorMicBackend.cs:268`'s "VOSK has no grammar-swap API" is false. **But do not overclaim the fix:** `SetGrm` rebuilds the decoder *and the feature pipeline* (R2 finding 5, R4 D8), so it is not i-vector-preserving — the gap-free property comes from keeping capture running and re-priming, not from the API call.

7. **Stop planning around any published RTF.** Not one number in R1 was measured on XR2 Gen 1 or Gen 2; the nearest ARM datapoint is a Galaxy S23 Ultra, a substantially stronger CPU than XR2 Gen 2's 2P+4E configuration with "just 33 %" CPU improvement over Gen 1.

8. **Stop considering a blind neural denoiser in front of VOSK.** Chondhekar et al. (2025) found noisy audio beat enhanced audio in **40 of 40** configurations, with degradations of **+1.1 % to +46.6 % absolute** semantic WER, against back-ends not trained on enhanced audio — and VoXR cannot retrain its back-end. The asymmetry is structural: VoXR's dominant condition is close-talk speech into a grammar decoder whose failure is "picks the wrong in-grammar word", and suppression can only subtract evidence. Revisit only if P12 reveals a genuinely low-SNR regime that occurs in the field.

9. **Stop shipping the model cache without a version key.** `Runtime/ModelExtractor.cs:24-27` keys only on the folder name; the moment `conf/model.conf` becomes a tuning surface (P7), a re-tuned model shipped under the same name is silently ignored on every device that already ran the app.

---

## Open questions for the maintainer

1. **Can the offline A/B rig reach the real decoder, or not?** R6 states the 699-row rig stages nine parser `.cs` files and zero VOSK code (transcript-level only); project memory `grammar_ab_rig` describes measuring a `GenerateGrammarJson` change "against the real decoder in WSL" and pinning documentation numbers against "the real parser". These cannot both describe the same tool. Which corpora have audio, and which have only transcripts? This single answer decides whether P4 and P6 are one-week experiments or blocked behind P12.

2. **How much of the 73-utterance false-trigger set is non-speech?** Answerable today from `Library/VoxrDebugLogs/` with no code. It upper-bounds P3 and therefore decides whether the ONNX Runtime dependency is worth taking.

3. **Is the leading-word loss the AGC onset transient, the endpointer, PTT button latency, or the buffer?** R4 A4 names four candidates and says the AGC one is the cheapest to falsify. Which does the field log actually support?

4. **What is the appetite for a human recording campaign?** R6's arithmetic is unforgiving: six discordant utterances is the *smallest possible* significant paired result, and 90 %→95 % needs ≈290–435 utterances per arm. Multi-speaker, in-headset, Lombard/exerted audio is human-only and is the long pole for everything downstream. If the answer is "none", the roadmap collapses to Phase 0, Phase 1 and the mechanically-certain items (P5a, P8's capture continuity, the cache key), and no accuracy claim in this document can be honoured.

5. **Is `Documentation~/scoring.md`'s published rule set re-derivable, or is it frozen?** Five specific 699-row measurements (`:178, :238, :242, :454, :511`) and two enumerations declared exhaustive (`:309-321`, `:338-361`) are published as normative. P1, P2, P4 and P6 each shift the numbers those measurements report; P11, if it ever gates, adds a suppressor to a closed enumeration. Is re-deriving and re-publishing them acceptable, or is the published rule set a hard constraint that caps front-end work?

6. **How much APK is actually available?** +88 MB (`0.22-lgraph`) vs +12 MB net (sherpa-onnx 20 M Zipformer, per R1's arithmetic) vs +2 MB (Silero) vs +532 KB (TEN VAD) vs −35 MB (KWS-only). Several decoder rows turn on this number and the brief only says "APK size matters".

7. **Should wrong-mode explainability be a product goal?** It is the one thing grammar mode structurally cannot give and every migration option restores — out-of-set speech transcribes as itself instead of collapsing to `[unk]`, so the game can say "that's a navigation command; you're in weapons mode". If yes, it strengthens the transducer-plus-biasing row considerably and justifies the parallel competitor path (3b). If no, the closed-vocabulary rejection property of the current design is worth more than the migration.

8. **Is TEN VAD's "Apache-2.0 with additional conditions" plus its LPCNet-derived BSD file acceptable, or is Silero's clean MIT worth 1.5 MB and ~4× the per-frame cost?** A licence call only the maintainer can make, and it gates P3's implementation choice.
