# I3 — Latency and UX redesign

Ideation report, 2026-09-05. Author: ideation agent (interaction/systems angle). Inputs: `00-brief.md`, `02-verified-facts.md` (authoritative on VOSK ABI), `01-code-audit.md`, research reports R1, R2, R3, R5, R6, plus the shipped docs and source cited inline.

**R4 (`research/R4-frontend-robustness-personalization.md`) did not exist on disk at the time of writing and was not read.** Nothing below depends on it; anything it says about AGC, mic front-end, Lombard/exertion robustness, or speaker adaptation should be checked against P2, P4 and P11 before those are built.

Every code claim carries `file:line`. Every quantitative claim is labelled **measured**, **inferred** (derived from a constant or a config value in this repo), **reported** (a number from a cited paper, on a different system), or **unmeasured**.

---

## Thesis

VoXR's controllable latency is two silence timers — Kaldi's endpointer (`conf/model.conf`, 500/750/1000 ms) and the utterance buffer (`VoxrCommandRecogniser.cs:72`, recommended 2.0 s on Quest by `command-recognition.md:282`) — that were set independently to defend against the *same* failure, a mid-command split, so the project pays for split-avoidance twice and the player waits 2.5–3.0 s for a command that was decidable a second earlier. The redesign is to stop timing and start deciding: the grammar already knows when an utterance is syntactically finished, and `TryEagerCommit` already encodes seven safety conditions for saying so (`VoxrCommandParser.cs:4274`) — it is simply never shown a partial hypothesis, because `HandlePartialResult` is `#if UNITY_EDITOR` and stores a string (`VoxrCommandRecogniser.cs:1331`). Wire the same scan to partials behind a stability gate and both timers vanish for the commit case; tune `model.conf` and add a VAD for the cases the grammar cannot decide. What latency remains is then spent, not lost: today the entire 0.5–3.0 s window is *silent*, and the cheapest measured UX win in the whole corpus of research is to acknowledge at the first partial (Maslych et al., CUI '25, n=54 — reported) instead of at the command. Beyond that, three structural moves each remove a class of problem rather than a constant: decoupling the decoder grammar's *scope* from the parser's active set — affordable because `new_grm` is a counted bigram estimate, not a hard grammar, so inactive sets can be carried at a lower weight rather than removed (R2 finding 1), which kills the audio gap *and* enables "that's a navigation command, you're in weapons mode"; binding slot values to gaze via VOSK's already-parsed per-word timings (shrinks the grammar instead of growing it); and replacing confirmation with speculative-execution-plus-retraction for reversible commands (an undo costs only the wrong utterances; a confirmation costs every correct one). None of this should ship ahead of the instrument: there is currently no path in the toolkit by which a decoder-side or timing-side change could be demonstrated at all (R6 §"the compound problem"), and every proposal below that changes *what fires* is gated on that instrument existing.

---

## Latency budget, before and after

End of speech → `OnCommandRecognised`, Android/Quest path, default settings unless noted.

| # | Stage | Today | Label / source | Target | Proposal |
|---|---|---|---|---|---|
| 1 | Mic capture chunk | ~20 ms | **inferred** — `audio_capture_audiorecord.cpp:13` (`kReadFrames = 960`), `:10` (48 kHz) | ~20 ms | — |
| 2 | Recognition-thread read chunk | up to **85.3 ms** | **inferred** — `vosk_bridge.cpp:45` (`kReadChunkSize = 4096`), comment says "~85 ms" | ≤ **21.3 ms** | P3 |
| 3 | Idle sleep when ring empty | 0–10 ms | **code** — `vosk_bridge.cpp:94` | 0–5 ms | P3 |
| 4 | DSP (FIR decimate + AGC + int16) | ~0.15 ms group delay | **inferred** — 15-tap FIR, `downsampler.h:64-68` | unchanged | — |
| 5 | Decoder frame granularity | 30 ms | **inferred** — `--frame-subsampling-factor=3` × Kaldi 10 ms frames (`conf/model.conf`, `02-verified-facts.md`) | unchanged | — |
| 6 | **Endpointer trailing silence** | **500 / 750 / 1000 ms** (rule2/3/4) | **config-measured** — `conf/model.conf` `--endpoint.rule2/3/4.min-trailing-silence`, quoted verbatim in `02-verified-facts.md` | **250–350 ms** tuned; **0 ms** on the commit path | P2, P1 |
| 7 | Quest decoder catch-up | "adds ~0.5–1.0 s to inter-result gaps" | **UNMEASURED** — prose only, `command-recognition.md:286`. Largest hole in the budget | must be measured before it can be targeted | P11 |
| 8 | Result queue → main-thread poll | 11.1 ms @ 90 fps / 13.9 ms @ 72 fps | **inferred** — one `Update()`, `VoxrSpeechRecogniser.cs:508` | unchanged | — |
| 9 | **Utterance buffer window** | **2000 ms** recommended on Quest (**500 ms** default) | **code + doc** — `VoxrCommandRecogniser.cs:72`; `command-recognition.md:282,286` | **0 ms** on commit; **~200–300 ms** VAD hangover otherwise | P1, P4 |
| 10 | Flush poll granularity | ≤ 1 frame | **code** — `VoxrCommandRecogniser.cs:534-536`, `UtteranceBuffer.cs:38-41` | unchanged | — |
| 11 | Parse + selection + extraction + dispatch | **unmeasured** (bounded by one frame in practice) | — | unmeasured | P11 |
| 12 | Per-intent debounce (2nd+ command only) | 300 ms | **code** — `VoxrCommandRecogniser.cs:94` | unchanged | — |
| — | **Total, eager `Commit` path** | **≈ 545–1055 ms** (excludes #7) | **derived** — R3 budget, rows 1+2+3+5+6+8 | **≈ 150–260 ms** | P1+P2+P3 |
| — | **Total, `HoldExtendable` + `prefixHoldSeconds = 0.6`** | **≈ 1145–1655 ms** | **derived** — R3 budget | **≈ 400–500 ms** | P1+P2+P3 |
| — | **Total, default time-driven, `bufferWindow = 2.0`** | **≈ 2545–3055 ms** | **derived** — R3 budget | **≈ 450–650 ms** (VAD close) | P4+P2+P3 |
| — | Add the unmeasured Quest catch-up (#7) and the time-driven path is | **3–4 s** | **reported by R3 from doc prose** | unknown until #7 is measured | P11 |
| — | Set-switch **audio gap** (not on the above path) | "~50 ms minimum on Quest 3" | **UNMEASURED** — prose only, `command-sets.md:156`; mechanism is `GrammarManager.ForceApply` stop→set→start (`GrammarManager.cs:38-53`) + free/recreate (`vosk_bridge.cpp:356-365`) | **0 ms** (no rebuild at all) | P10, P5 |

**Design target: ~200–300 ms.** Conversational turn-transition offsets peak within 200 ms of turn end across 10 languages (Stivers et al., PNAS 2009 — *reported, and R3 marks the citation `UNVERIFIED`*); voice-agent practice converges on ≤300 ms (R3). The one-line reading of the table: **rows 6 and 9 are 90 %+ of controllable latency, both are silence timers, and both defend against the same failure.**

---

## Proposals

### P1 — Semantic endpointing: run the eager-commit scan on partial results, with a commit watermark

**Problem.** The eager path can only ever fire *after* the endpointer has already closed the utterance, because it is driven from finals. `HandleResult` (`VoxrCommandRecogniser.cs:566`) — the only path that appends to the buffer (`:581`) and calls `ProbeEagerCommit` (`:592-599`, `:628`) — is subscribed to `OnResult` (`:501`). Partials reach `OnPartialResult` as bare text (`VoxrSpeechRecogniser.cs`, `DispatchJsonResult` else-branch), and the command layer's `HandlePartialResult` (`VoxrCommandRecogniser.cs:1331`) is inside `#if UNITY_EDITOR` and does nothing but assign `LastPartialResult`. So even with `eagerFlushOnCompleteMatch` on, a complete unambiguous command pays row 6 (500–1000 ms) in full and only saves row 9.

**Mechanism.** Subscribe the command layer to partials. On each distinct partial, run the existing `TryEagerCommit` scan against `bufferedText + partialText` and act on a `Commit` verdict immediately. Three additions the current design does not have:

1. **A stability gate.** Fire only when the same `Commit` verdict, naming the same intent over the same span, survives *N* consecutive distinct partials or *T* ms without a transcript change. Kaldi revises partials; VoXR's whole safety architecture (bar, coverage, sibling ties) was written against final transcripts.
2. **A commit watermark.** This is the piece the reports do not supply and it is the load-bearing detail. Today `FlushBuffer` (`:610`) clears the buffer, and that is sufficient *because finals are the only source*. Fire on a partial and the utterance is still open: the eventual final will re-deliver the same words and re-fire the same command (debounce at `:991` would mask it for 300 ms and then stop masking it). So a partial commit must record the committed token count and the emitting VOSK segment, and `HandleResult` must strip that prefix from the arriving final before appending. Getting this wrong produces double-fires, not misses — the loudest possible failure.
3. **Real confidence on partials.** `vosk_recognizer_set_partial_words` **is exported by the shipped Android `libvosk.so`** (`02-verified-facts.md`) and is declared in `NativeBridge~/include/vosk_api.h:31`. Without it, eager condition 7 (`scoring.md:346,356`, `VoxrCommandParser.cs:4510-4512`) sees `-1` and is bypassed on every partial — i.e. the fastest path would be the only one with no confidence gate.

**Grounding.** R3 finding 3 and option B: "the single largest available cut", removing budget rows 6 *and* 9 together, and the only option that can reach 200–300 ms. Kaldi's own `OnlineEndpointRule.max_relative_cost` is grammar-completeness endpointing that VOSK's C ABI hides (R3 finding 1) — VoXR's eager scan is the hand-written equivalent. Bijwadia et al., SLT 2022: unifying endpointing with the recogniser's own state cut median endpoint latency **−120 ms (−30.8 %)**, P90 **−170 ms (−23.0 %)**, no WER regression (*reported*). Raju et al., ASRU 2023 supplies the propose-then-verify shape for the stability gate. LiveKit's turn-detector result — the effective EOU signal is on the *text* side — is R3's argument for B over a waveform VAD as the primary lever. Baumann et al., NAACL 2009 names the lag↔stability tradeoff as a curve to be measured (numbers `UNVERIFIED` per R5 3.10). **Direct prior art for the stability gate:** whisper-streaming's *LocalAgreement* policy — commit a prefix once two successive updates agree — which R1 explicitly identifies as "a general recipe" applicable to stage 5 over VOSK partials and as a competitor to the existing eager flush. R2 independently notes that `set_partial_words` "would let the eager-flush path see per-word timing before the final", i.e. the same call also unblocks P8.

**A second, cheaper stability signal worth measuring beside the N-partial gate.** `set_max_alternatives(N)` is exported (`02-verified-facts.md`) and gives **parse agreement**: if all N alternatives parse to the same intent, the hypothesis is robust; if they diverge, it is "a sibling tie discovered acoustically" (R2 finding 3). That is a *within-frame* stability test rather than a temporal one, so it costs no latency at all. **But it carries a hard coupling that must be planned across tracks: in N-best mode VOSK does not emit per-word `conf` at all** — `NbestResult` writes only `word`/`start`/`end`, so `VoxrCommand.Confidence` and the whole `minConfidence` gate fall to the `-1` "no data" path (R2 finding 3, source-read of `recognizer.cc`; R1 flags the same thing as a hedged risk). Adopting N-best anywhere means replacing `minConfidence` **in the same change**, and it would silently delete P6's confidence marking.

**Code impact.** Stages 3, 5, 6.
- Bridge: call `vosk_recognizer_set_partial_words(rec, 1)` beside the existing `set_words(1)` at `vosk_bridge.cpp:235` and `:373`; **add the missing declaration** — the vendored header declares `set_partial_words` at `:31` so this one is free, unlike `set_grm` (P5). Rebuilt arm64 `.so` must be committed per the project's verification bindings.
- C#: `VoxrSpeechRecogniser` must parse words on partials, not only finals (`DispatchJsonResult` currently parses words only in the `isFinal` branch) → a new `OnPartialResult` overload carrying `VoxrResult`, or promote `OnPartialResult` to `Action<VoxrResult>` with the `Action<string>` retained (see Migration).
- `VoxrCommandRecogniser`: move `HandlePartialResult` out of `#if UNITY_EDITOR`, give it a probe path, add `_committedTokenCount` / segment id, and strip on the next final in `HandleResult`.
- Parser: unchanged. `TryEagerCommit`'s seven conditions transfer as-is; G4 (leading-required-miss → `None`, `:4486-4487`) exists precisely to protect the buffer from a half-spoken command and becomes *more* important, not less.
- New: `partialCommitStability` (int, distinct partials) and/or `partialCommitQuietMs`, both Inspector fields.

**Expected effect.** Removes budget rows 6 and 9 on the commit path: **≈ 545–1055 ms → ≈ 60–120 ms** plus rows 1–5 and 8, i.e. **≈ 150–260 ms** end-to-end (R3 option B, *derived from this repo's constants; the residual Quest catch-up of row 7 is unmeasured and rides on top*). What fraction of commands take the commit path at all is **unmeasured**.

**Cost.** 6–9 engineer-days, plus the instrument in P11 (which must come first). Bridge ABI change → rebuilt `.so` → human-only on-device verification.

**Risk and kill criterion.** The risk is firing on a hypothesis the final revises, and stranding the tail. **Kill criterion, measurable offline before writing any runtime code:** instrument the WSL harness (`NativeBridge~/harness/main.cpp`) to log, per fixture, every partial and the `TryEagerCommit` verdict it *would* have produced, then report the fraction of utterances where a partial yields a `Commit` the final contradicts (different intent, different slot values, or a command the final would not have fired). **If that fraction exceeds ~1 % at the chosen stability setting, P1 is dead and the answer is P2 + P4.** Second kill criterion: any double-fire in the watermark tests.

**Cheapest validating experiment.** The read-only harness instrumentation above — no Unity, no device, no package change (R3 recommendation 2). Sweep the stability gate (N ∈ {1,2,3}, T ∈ {0,60,120,200} ms) and plot contradiction rate against saved milliseconds; that is Baumann's curve, measured on VoXR's own corpus instead of copied from a 2009 German system. **Human-only, on device:** confirming that partial cadence on Quest resembles the harness's (the harness pushes faster than real time), and that the rebuilt `.so` behaves.

**Migration.** `OnPartialResult` gains a word-carrying sibling; the `Action<string>` form stays and stays main-thread (`api/speech-recogniser.md:56`). New Inspector fields default to the *old* behaviour (stability = ∞ ⇒ never commit on a partial), so an existing project is bit-identical until opted in. `scoring.md` §6 must be amended: "each VOSK result triggers one speculative parse" becomes true of partials too, and the seven-condition enumeration published as exhaustive (`scoring.md:338-361`) needs an explicit statement that it is unchanged and now evaluated on two input classes.

---

### P2 — Make the endpointer a shipped, tunable profile by patching `conf/model.conf` at extraction

**Problem.** Row 6 is 500–1000 ms of the budget and it is a constant nobody chose. R3 proposed calling `vosk_recognizer_set_endpointer_delays` / `set_endpointer_mode` — **and `02-verified-facts.md` establishes that neither symbol is exported by the shipped `libvosk.so`.** R3's recommendation 1 is therefore not implementable as written without rebuilding libvosk from a newer vosk-api, which is a much larger change than it advertises.

**Mechanism.** The same knobs are *text* in the model: `--endpoint.rule2.min-trailing-silence=0.5`, `rule3=0.75`, `rule4=1.0`, plus `--lattice-beam=2.0`, `--beam=10.0`, `--max-active=3000` (`conf/model.conf`, quoted verbatim in `02-verified-facts.md`). The model ships as a StreamingAssets zip and `ModelExtractor` unpacks it to `persistentDataPath/VoxrModels` (`ModelExtractor.cs:18,25-27,53-84`) — so the package already owns a write point on that file, once per install. Add a serialized `VoxrEndpointerProfile` (a small ScriptableObject or three Inspector floats) that is applied as a rewrite of `conf/model.conf` immediately after extraction and before `vosk_bridge_init`. Ship named profiles: `Conversational` (as-is), `PushToTalk` (rule2 0.25 / rule3 0.35 / rule4 0.5 — the button already supplies the endpoint, so the endpointer only has to split, not decide), `Continuous` (as-is).

**Grounding.** `02-verified-facts.md` (exports, and the verbatim `model.conf`). **R1 and R2 independently confirm the same conclusion from opposite directions:** R1 verified the export table with `nm -D --defined-only` on both the shipped arm64 `libvosk.so` (8 862 928 bytes) and the vendored desktop one — 36 `vosk_*` symbols each, neither including the endpointer setters, though the local header declares them at lines 33–34; R2 states plainly that "calling either would fail to resolve at load/link time on device" and that `model.conf` is the only endpointer route. R3 finding 1: rule2 is `(min_trailing_silence, max_relative_cost)` — 0.5 s of silence **and** a good final-state probability, i.e. Kaldi is already doing grammar-completeness endpointing and VoXR is only allowed to move the silence half. R3 option A: stage 5 drops 500 → 250–350 ms. Splits rise, but the buffer already exists to reassemble splits — R3's point is that the project currently pays for split-avoidance twice.

**Co-tenancy warning.** This proposal makes `conf/model.conf` a package-managed file, and the accuracy track wants the *same file* for a different reason: `--lattice-beam=2.0` is "a third of vosk's own default and is the binding constraint on lattice density, hence on MBR posterior quality, N-best diversity, and any rescoring" (R2 finding 4), and R2's recommendation 3 is a lattice-beam sweep. **Sweep the two knobs one at a time and never in the same run** (R2 rec 3 is explicit about this) — lattice beam changes what is decoded, endpoint rules change where it is cut, and a combined sweep cannot attribute a regression. Whoever builds the rewrite mechanism should build it once and hand it to both tracks.

**Code impact.** Stage 3, no native change at all — this is the reason to prefer it over a libvosk rebuild. `ModelExtractor.cs`: a post-extract rewrite step, and a fix to the cache key. **`ModelExtractor.cs:25-27` keys the cache on the model folder name only** and validity is a directory-content check (`:35`), so a changed profile against an already-extracted model would silently reuse the old conf (audit §9.13 flags the same hazard for a changed model). The profile must join the cache key or be re-applied on every launch.

**Expected effect.** Row 6: 500 ms → 250–350 ms on the tuned profile (**inferred** from the config semantics, not measured on this system). Split rate: **unmeasured, expected to rise**.

**Cost.** 2–3 days including the cache-key fix and docs.

**Risk and kill criterion.** More mid-command splits, each of which then costs a full buffer window to reassemble — so a shorter endpointer without P1 or P4 can make *mean* latency worse while improving the best case. **Kill criterion:** on the fixture corpus replayed through the real decoder, if finals-per-utterance rises such that (split rate × buffer window) exceeds the saved endpointer time, the profile is rejected. Second: any change in transcript content, not just segmentation, is an immediate stop — this knob is supposed to move boundaries, not words.

**Cheapest validating experiment.** WSL harness sweep over `rule2 ∈ {0.5, 0.35, 0.25}` on the existing fixture corpus, reporting (a) finals per utterance and (b) end-of-audio → final delay **in sample time, not wall clock** (R3 recommendation 1 — the harness pushes faster than real time). No Unity, no device. **Human-only, on device:** the profile actually reaching the decoder from `persistentDataPath` on Quest, and a felt-latency check. Note R2's standing caveat on every such sweep: **the desktop number is a lower bound on Quest cost**, so RTF headroom stays open until a human runs it in-headset and must be reported as deferred.

**Migration.** New optional Inspector field; absent ⇒ ship the model's conf untouched, so existing projects are unchanged. `troubleshooting.md` and a new `Documentation~/latency.md` need to explain that this file is now package-managed and that hand-edits to the extracted copy will be overwritten.

---

### P3 — Shrink the read chunk and the idle sleep

**Problem.** The recognition thread accumulates up to 4096 samples = **85.3 ms** of audio before calling the decoder (`vosk_bridge.cpp:45,91`), and sleeps **10 ms** whenever the ring is empty (`:94`). At the tail of an utterance that is up to 95 ms of pure latency, and — more importantly for P1 — it caps partial cadence at ~11–12 partials/second minus decode time, when partials are the signal P1 fires on.

**Mechanism.** `kReadChunkSize` 4096 → 1024 (21.3 ms), sleep 10 ms → 5 ms. Both are single constants; `kDownsampledSize` derives (`vosk_bridge.cpp:46`) and the stack buffers at `:71-73` shrink with them. The ring is 65 536 samples = 1.365 s (`ring_buffer.h:15`), so overflow headroom is untouched.

**Grounding.** R3 budget rows 2 and 4 ("the 85 ms cap binds only while catching up"). R3's U2++/dynamic-chunk row is the general statement that chunk size is a tunable, not a constant. Note honestly: R3 rates this "not the bottleneck" *for the current time-driven design* — it becomes material only because P1 makes partial cadence a first-class quantity.

**Code impact.** Stage 1–3 (`vosk_bridge.cpp:45`, `:94`). Native ABI unchanged, but the `.so` is rebuilt, so the arm64 binary in `Runtime/Plugins/Android/arm64-v8a/` must be committed and verified on device. The Editor path is unaffected — `EditorMicBackend` drains once per `Update()` at a fixed 4800-sample playback chunk (`EditorMicBackend.cs:413`), an existing Editor/device divergence this proposal widens slightly.

**Expected effect.** Rows 2+3: up to 95 ms → up to 26 ms (**inferred** from the constants). CPU cost of 4× more `accept_waveform_s` calls: **unmeasured** — Kaldi's per-call overhead is small relative to per-frame work, but that is an assumption, not a measurement, and this runs beside a 72–90 fps VR game.

**Cost.** 0.5 day of code, gated on a device CPU measurement.

**Risk and kill criterion.** CPU. **Kill criterion:** if the recognition thread's CPU share rises measurably at 1024 samples, or the game drops frames, revert to 2048 and re-measure. A secondary risk: more frequent partials means more `g_result_queue` pushes (`result_queue.h:15-16` documents ~4/s as the design assumption) and more per-frame drain work on the main thread — the zero-allocation poll path (`ZeroAllocPollPathTests.cs`) must stay green.

**Cheapest validating experiment.** WSL harness with a wall-clock CPU counter at three chunk sizes; then **human-only, on device:** `logcat` plus the Unity profiler on a real scene, comparing frame time with recognition running at 4096 vs 1024.

**Migration.** None — internal constants. Worth exposing as a CMake cache variable so the Editor `libvosk.dll` path and the harness can be built at matching settings.

---

### P4 — VAD-driven buffer close, noise reporting, and a real endpoint for the pending path

**Problem.** Row 9 is a 2.0 s timer standing in for an observation nobody makes. And P1 deliberately does not accelerate the cases that matter most for dialogue: eager probing is skipped entirely while a pending is live (`VoxrCommandRecogniser.cs:592`; `scoring.md:330`), so every confirmation, slot-fill and disambiguation answer waits the full window — the exact moments the player is most obviously waiting on the system.

**Mechanism.** Run a frame-level VAD on the already-downsampled 16 kHz int16 buffer inside `recognition_loop`, between the AGC (`vosk_bridge.cpp:119`) and `accept_waveform_s` (`:125`). Publish two things across the ABI: a speech/non-speech edge with a sample-accurate offset, and a rolling noise-floor estimate. Then (a) close the utterance buffer on `speech offset + hangover` instead of on `bufferWindow`, including on the pending path; (b) surface the noise floor so a game can show Meta's "move somewhere quieter" cue; (c) *optionally* gate decoder output when no speech frame preceded it.

**Grounding.** R3 finding 7 / option C; R2 rejection option B; R1 recommendation 2 — **all three reports independently rank a VAD in the top three, and R1 notes it "is orthogonal to every other recommendation and survives whichever decoder is chosen"**, which is the strongest argument for doing it early. TEN VAD: **532 KB** self-contained arm64 `.so`, **RTF 0.057** on a Galaxy J6+ (*reported*); Apache-2.0 **with additional conditions** plus an LPCNet-derived BSD file. Silero VAD: ~2 MB, **MIT**, ~0.8 ms per 32 ms frame on Pixel-7-class arm64 ≈ **2.5 % of one core** (R2, *reported*). R3 option C: row 9 drops 2000 → ~200–300 ms. Meta's *Voice Best Practices* requires warning the user when background noise is high (R5 3.8), and VoXR already computes a rolling pre-DSP RMS that **nothing consumes** — `vosk_bridge_get_input_level` (`vosk_bridge.cpp:342`) is bound at `BridgeNative.cs:64` with zero call sites (audit §9.5).

**The APK arithmetic inverts the obvious choice.** Silero is the licence-clean option but it is an ONNX graph, and R2 is explicit that it brings "an ONNX Runtime dependency in the native bridge" — R1 prices `libonnxruntime.so` on Android arm64 at **15 MB**. So Silero is ~17 MB, not 2 MB, against a package whose entire native payload is `libvosk.so` at 8.9 MB. TEN VAD's 532 KB is self-contained. **The licence read on TEN VAD's "additional conditions" is therefore not a formality — it decides a 30× APK difference**, and should be done before any integration work, not after.

**Code impact.** Stages 1–2 and 5. New native dependency + ABI additions (`vosk_bridge_get_speech_state`, or a field on the result struct). C#: `EffectiveBufferWindow` (`VoxrCommandRecogniser.cs:605-608`) gains a third source; today it may only ever *shorten* the wait (rule R19, issue #32) and a VAD close must respect that invariant or restate it. The Editor path needs the same VAD or it diverges — and the AGC pair has **already silently diverged** between `Agc.cs:66` and `agc.h:53` (audit §4.4), which is the precedent for taking parity seriously here.

**Expected effect.** Row 9: 2000 → ~200–300 ms (*R3, reported for the technique; unmeasured on VoXR*). Total time-driven path **≈ 2545–3055 → ≈ 750–1300 ms**, or **≈ 450–650 ms** combined with P2 and P3 (*derived*). The false-trigger benefit of (c) is **explicitly unmeasured and must not be claimed**: a cough is voiced, and R3 finding 7 says no fetched source measures VAD discrimination of coughs.

**Cost.** 5–8 days including licence review and Editor parity.

**Risk and kill criterion.** Three, and the third is the one nobody would guess.
1. **Licence/size.** TEN VAD's added conditions vs Silero's +15 MB of ONNX Runtime. **Kill criterion: if the added conditions are not clean and 17 MB is unacceptable, the VAD does not ship and P1 has to carry the whole design.**
2. **Hangover too short re-introduces splits** at the same rate a short endpointer does. **Kill criterion:** if the measured distribution of (VAD speech offset → VOSK final) on the fixture corpus is noisy rather than consistently ≥400 ms, the VAD carries no more information than the timer (R3 recommendation 3).
3. **A VAD gate in front of the decoder can eat the verb.** R2 names this precisely: "a VAD that eats the first 160 ms eats the verb, and the leading-required-miss bar then silences the command" — the `minSpeechFrames` warm-up interacts with the issue-#124 bar (`VoxrCommandParser.cs:2742-2746`) to convert a front-clipped utterance into *silence*, not into a degraded match. **Kill criterion: if use (c) — gating decoder input on speechiness — raises the barred-round rate at all, use (c) is dropped and only uses (a) and (b) ship.** This also means P4 use (c) must not be built before P11's `barred` log field exists, because today that rate is unobservable.

Note also that the false-trigger benefit of (c) is **explicitly unmeasured**: R3 finding 7 says no fetched source measures VAD discrimination of coughs, and R2 makes the same point from the other side — none of its four rejection options "makes the decision principled at the token level", they only shift the operating point.

**Cheapest validating experiment.** **R1's version first, because it costs nothing at all:** classify the existing 73-utterance field debug logs in `Library/VoxrDebugLogs/` into false triggers that were non-speech versus speech-like, which upper-bounds the win before anything is built. Then R3's: run Silero offline in Python over the existing fixture WAVs and plot the (VAD offset → VOSK final) distribution — zero package code, zero device. R2's version is the third step and the only one needing hardware: record ~30 coughs, hums, throat-clears and mic taps on the Quest and run them offline at `threshold ∈ {0.5, 0.65, 0.8}` — "**if coughs pass at 0.65, the VAD is the wrong instrument for VoXR's named failure**" and the case collapses to "kills taps and breathing". **Human-only, on device:** that recording session (it needs only the recorder, not the package), plus capture-path behaviour and the noise cue in a real room with game audio.

**Migration.** Additive events (`OnNoiseLevelChanged`, or a `NoiseLevel` property). `bufferWindow` remains and remains the ceiling; the VAD may only close early. APK grows by ~0.5–2 MB.

---

### P5 — Close the audio gap by never stopping capture, not by swapping the grammar faster

**Problem.** Every active-set switch and every grammar rebuild stops capture, frees the recogniser, builds a new one, and restarts: `GrammarManager.ForceApply` (`GrammarManager.cs:38-53`) → `StopRecognition` → `vosk_bridge_stop`, which stops the capture device *and joins the recognition thread* (`vosk_bridge.cpp:283-287`) → `vosk_bridge_set_grammar` (`:346`), which **refuses while running** (`:352-353`) and **frees and recreates** (`:356-365`) → `StartRecognition`. `command-sets.md:156` claims "~50 ms minimum on Quest 3" and that "the first one or two words spoken immediately after a mode switch may be lost" — a claim that has **never been measured** (audit §7). R5 3.3 raises the stakes: the literature assigns voice *specifically* to mode-switching, which is exactly the operation that costs the gap.

**R1 and R2 flatly contradict each other here, and the contradiction is load-bearing.** R1's recommendation 1 says `set_grm` "reconfigures a *running* recognizer's grammar, which makes the documented ~50 ms audio gap **an implementation artefact of `vosk_bridge_set_grammar`'s free-and-recreate, not a property of VOSK**". R2, reading `recognizer.cc` rather than the header comment, reports that **`Recognizer::SetGrm` refuses while `state_ == RECOGNIZER_RUNNING`**, then deletes `decode_fst_`, re-runs `UpdateGrammarFst` and rebuilds the decoder, feature pipeline and silence weighting — so "the saving is the recognizer alloc/free, not the FST composition — `UpdateGrammarFst` and the `LookaheadComposeFst` **dominate and happen either way**", and R2 lists the gap under what its area *cannot* fix: "`set_grm` shortens **one term** of it". R1 read a doc-comment; R2 read the implementation. **Treat R2's as the stronger evidence and R1's "gap → ~0" as unsupported.** That refutation is what makes this proposal worth restating, because the real fix is elsewhere and is cheaper.

**Mechanism.** The gap is not caused by the recogniser rebuild — it is caused by **stopping the microphone for the duration of a rebuild that has nothing to do with the microphone**. The bridge already owns a 65 536-sample ring = **1.365 s at 48 kHz** (`ring_buffer.h:15`), an order of magnitude more than the claimed 50 ms. So: split `vosk_bridge_stop` into *stop capture* and *pause recognition*. Let `AudioRecord` keep filling the ring across the swap; pause only the recognition thread; on resume it drains the backlog it missed, bounded by the 85 ms read chunk (or 21 ms after P3). **Nothing is lost at the audio layer at all** — which is precisely where `command-sets.md:156` says today's loss happens. R2 identifies this same residual win independently: "capture need not stop *for model reasons*".

Rank the three candidates honestly, now that the first is deflated:
- (a) `set_grm` live — **mostly refuted**; saves an alloc/free, not the composition, and cannot be called while running anyway.
- (b) **Keep capture running across the swap** — this proposal. No new dependency, no model change, and it works regardless of what `set_grm` does.
- (c) **Do not rebuild the grammar at all on a set switch** — P10. Strictly better than both, and it also buys out-of-context rejection. (c) subsumes (b) for the *set-switch* case; (b) still earns its keep for the dynamic-slot and `RebuildGrammar` cases.
- (d) Two pre-built recognisers swapped atomically — blocked by the file-scope singleton (`vosk_bridge.cpp:20-42`, audit §8.1) and a much larger change than it looks.

**Grounding.** R2's source read of `Recognizer::SetGrm` (the refutation). R1's export verification (`set_grm` is present in both binaries; 36 `vosk_*` symbols each) and `02-verified-facts.md`'s header/binary mismatch — the vendored `vosk_api.h` does **not** declare `set_grm`, so even the deflated version costs a declaration or a header vendoring, not "one line of C". R5 3.3: mode-switching is the job voice is best at, so a gap on that path costs more than 50 ms of wall clock suggests. `ring_buffer.h:15` for the headroom.

**Code impact.** Stage 1 and 4, all inside the bridge. Split the `g_running` flag into capture-running and recognition-running (they are already separate concerns — `start_internal(bool start_capture)` at `vosk_bridge.cpp` already parameterises exactly this distinction for push mode); add `vosk_bridge_pause_recognition`/`resume`; have `GrammarManager.ForceApply` (`:38-53`) use them instead of `StopRecognition`/`StartRecognition`. Rebuilt `.so`, human-verified on device. **Watch the overflow flag** — the ring sets a sticky overflow bit and the consumer snaps forward (`ring_buffer.h:41,56`), which currently surfaces as a `RingBufferOverflow` error to the game (`vosk_bridge.cpp`, overflow push); a deliberate backlog must not be reported as an error.

**Expected effect.** Audio-layer loss on a grammar rebuild → **0 samples** by construction. The *recognition* pause remains (~the rebuild duration, unmeasured), but it is now a latency, not a loss — the words are still in the ring. Whether that changes what the player experiences is **unmeasured** and depends entirely on the real gap duration, which nobody has measured.

**Cost.** 2–3 days, plus the measurement in P11.

**Risk and kill criterion.** If the real gap turns out to be much longer than 50 ms — say several hundred ms — the ring absorbs it but the *decoder* then processes a backlog while the player is already speaking their next command, which is a different and possibly worse failure. **Kill criterion: measure the gap first (below). If it exceeds ~400 ms, neither (a) nor (b) is the answer and only P10 is.** Secondary: any regression in the `RingBufferOverflow` error semantics.

**Cheapest validating experiment.** One harness run measuring the *current* gap **in sample time** — how many samples are lost across a stop→set→start. R1 proposes the same shape ("the audio gap is measurable as lost samples, entirely in WSL"). This is the decisive number for P5, P10 and the `command-sets.md` workaround all at once, and it costs a single WSL run. **Human-only, on device:** the real gap on Quest, since the 50 ms figure is a Quest claim and the harness has no capture device at all (it builds against the stub backend).

**Migration.** Internal; `GrammarManager`'s stop→set→start stays as the fallback. `command-sets.md:146-162` ("The Audio Gap") and its "wait ~500 ms before speaking" workaround (`:160`) get rewritten or deleted depending on the measurement. R18 (deferred rebuild during a pending, `VoxrCommandRecogniser.cs:369-375`) is a *published* behaviour and should stay regardless.

---

### P6 — A player-feedback architecture: listening states, early acknowledgement, confidence marking, discoverability

**Problem.** VoXR's entire 0.5–3.0 s window is silent, and the package gives a game no vocabulary for what is happening inside it. `OnPartialResult` exists but is documented as a transcript feed (`api/speech-recogniser.md:56`), not as the acknowledgement hook. `OnTalkStarted`/`OnTalkEnded` (`VoxrPushToTalkController.cs:101,103`) say when the *button* moved, not when the *system* heard anything. There is no event for "I have speech", no signal for "I am deciding", and — for `disambiguateSiblingTies` — the package ships a data structure whose own docs say the flag is worse than the coin flip it replaces without a subscriber (`Documentation~/command-recognition.md` §Ambiguous Commands; audit §6.5 notes the flag is **off** by default at `:126`).

**Mechanism.** Four additive pieces.
1. **`OnListeningStateChanged(VoxrListeningState)`** with a closed enum: `Idle → Armed → Hearing → Deciding → Resolving(pending) → Idle`. `Hearing` is raised on the **first partial** — the acknowledgement hook. `Deciding` covers the buffer/stability window. Every state is already implicit in `VoxrCommandRecogniser`; the proposal is to name it once instead of having every game re-derive it from three events.
2. **Word-level partials.** With P1's `set_partial_words`, `OnPartialResult` can carry `VoxrWord[]` — text, confidence, start, end — enabling a running transcript that dims low-confidence words.
3. **A rejection signal distinct from the acknowledgement.** `OnUnrecognisedSpeech` already exists with a published, exhaustive fire/silent table (`scoring.md:309-321`); the new hazard is acknowledging at `Hearing` and then rejecting, so the docs must pair the ack earcon with a distinct rejection earcon.
4. **Discoverability: `IReadOnlyList<string> DescribeActiveCommands()`** over `CommandSetManager` (`CommandSetManager.cs:12`) for a "what can I say?" panel. The package holds the full active grammar at all times and makes every game rebuild this from its own assets.

Ship a sample: a world-space prompt prefab bound to `OnCommandPending` + `PendingAmbiguity`, rendering candidates with per-candidate identity colour and uncertainty opacity, honouring `IsTruncated` with "…or say the whole command again".

**Grounding.** Maslych et al., CUI '25 (n=54): QoE degrades at every delay step 1.5 → 4.0 → 6.5 s (p<.0001); **natural/diegetic fillers significantly improved perceived response time at 4.0 s (p<.01) and 6.5 s (p<.0001) while artificial ones did not; 64.8 % preferred natural fillers** (*reported*). Nowrin & Vertanen, PETRA '25 (n=48): marking low-confidence output raised error-detection accuracy **85 % vs 80/79/76 %** and cut decision time **11 %** (1.86 s vs 1.98–2.26 s) (*reported*) — and this uses VoXR's flat confidence where miscalibration is harmless, sidestepping the fact that `minConfidence` cannot be raised (`VoxrCommandRecogniser.cs:27`; `KNOWN_LIMITATIONS.md:121`). Meta *Voice Best Practices* (normative): always signal mic state via earcon or visual; display transcription as light guidance; announce options. Tsai et al., CHI '26 (n=60, n=40) supplies the ambiguity feedforward vocabulary. Nowrin & Vertanen also report participants detect only **2 %** of insertions — which is the reframing argument: *humans will not catch a phantom command, so prevention (the bar) matters more than rejection UX, and any speculative execution (P7) needs machine-loud retraction rather than a visual the player is expected to notice.*

**R2 reframes what the confidence number *is*, and it makes the display case stronger, not weaker.** With `max_alternatives == 0`, VOSK word-aligns the lattice and writes `word["conf"]` from `kaldi::MinimumBayesRisk::GetOneBestConfidences()` — **MBR word posteriors normalised over a lattice that is already grammar-constrained**. So `conf` answers "given the speaker said something this grammar can represent, which word was it", and never "was this in-grammar at all". Two consequences:
- **`minConfidence` is structurally incapable of rejecting noise.** That is not a tuning problem, it is what the number means. (Corroborated in the wild by vosk-api issue #741: out-of-grammar speech returns in-grammar words "with confidence almost always 1".)
- **"two" ≈ 0.50 is not flatness, it is correctness.** The grammar contains both "to" (a required literal) and "two" (`VoxrNumberParser.DigitVocabulary`); the lattice keeps both arcs and the MBR sausage splits the mass evenly. 0.50 is a *well-calibrated statement about a genuine two-way ambiguity*. **This turns the number into a feature:** a matched literal at ≈0.5 whose homophone is also in the grammar is exactly the sibling-tie situation `disambiguateSiblingTies` detects structurally — detectable here *acoustically*, per token, at zero cost. That is a new and cheap input to the disambiguation prompt this proposal ships. (R2 marks this as an inference from mechanism, **not a measurement**, and asks for a red-then-green pin: force a grammar containing "two" but not "to" and check whether the confidence rises. Do that before building anything on it.)

**What to display, and in what order.** Raw `conf` first (cheap, and now interpretable). Then R2's recommendation 5: a **~4-feature logistic regression** over features the parser already has — match score, coverage charge, admission margin, leading-miss flag, tied-rival flag, span duration, min/mean `conf` — producing one calibrated `P(command is correct)`. R2 is emphatic about how it ships: "**as a reported number on `VoxrCommand` and in the session log, gating nothing**", because a calibrator fitted on 699 synthesised utterances plus 73 field utterances from one speaker will be miscalibrated for anyone who is not the author.

**Code impact.** Stage 7. New public event + enum in `VoxrCommandRecogniser`; `OnPartialResult` word-carrying overload in `VoxrSpeechRecogniser` (shared with P1); `DescribeActiveCommands` on `VoxrCommandRecogniser` / `CommandSetManager`; a `Samples~` prefab and script. No parser change, no native change, no runtime cost beyond one enum assignment per transition.

**Expected effect.** Perceived latency: the CUI '25 effect is *reported* on LLM agents at 4.0/6.5 s, not on a 0.5–2.0 s command system — **transfer is unmeasured**. Error detection: **+12 % relative, 11 % faster** is Nowrin & Vertanen's number on a dictation task at WER 15 %, *reported*, and its transfer depends on whether VOSK's grammar-mode confidence carries any information at all — which R6 §11 says is itself an open measurement.

**Cost.** 4–6 days including the sample prefab; 1 day for the states alone.

**Risk and kill criterion.** Acknowledge-then-reject is a new failure shape and is worse than silence if the two signals are confusable. **Kill criterion for the confidence marking specifically:** compute AUC of per-word confidence against known substitutions on the fixture/field corpus first (R5 3.6's own cheap check, R6 §11's NCE/ECE). **If NCE is at or near zero, ship the listening states and drop the confidence display** — marking noise is worse than not marking. **Kill criterion for the calibrated score:** R2's own — "if it does not beat two thresholds on 699 rows, it will not beat them in the field either", judged on AUC and ECE against a held-out split.

**A cross-track hazard this proposal must be defended against.** The accuracy track's leading recommendation (R2 rec 1, R1 rec 1) is to enable `set_max_alternatives`. **In N-best mode VOSK stops emitting per-word `conf` entirely** (R2 finding 3, source-read: `NbestResult` writes only `word`/`start`/`end`), so adopting it would silently delete the input this proposal displays *and* push every command onto the `-1` "no data" bypass of `minConfidence` (rule S14, `VoxrCommandParser.cs:4090-4113`). If both tracks proceed, the calibrated score above must replace `conf` **in the same change** — which is an argument for building it early rather than late.

**Cheapest validating experiment.** Offline, three runs, none touching the package: (1) the confidence AUC/NCE computation over existing session logs, which already record `words[].confidence` (R6 §11 — "the alignment is the only new code"); (2) R2's red-then-green pin on the "two"/"to" grammar, which tests the *explanation* rather than the correlation and is the difference between displaying a number and understanding it; (3) fit the 4-feature logistic regression on dumped 699-corpus attempt features and report AUC/ECE. In-game telemetry, no user study: count **re-utterances within 2 s of a prior utterance** — the player's "did it hear me?" behaviour — before and after the ack (R5 recommendation 2). **Human-only, on device:** whether the earcon is audible over game audio and legible in a headset.

**Migration.** Purely additive events; `OnPartialResult(string)` stays. `api/speech-recogniser.md` and `api/command-recogniser.md` gain rows; the new event must state main-thread delivery like its neighbours (`api/speech-recogniser.md:56,57,59`) — note `OnResult` is currently the only result event with **no** thread claim (audit §6.1), and the new one should not repeat that.

---

### P7 — Speculative execution with retraction for reversible commands; acknowledge-first for irreversible ones

**Problem.** Every safety mechanism VoXR has costs latency on *correct* utterances: the stability gate in P1, the full buffer window on the `HoldExtendable` and pending paths, and `requiresConfirmation`, which charges a whole extra turn to every correct invocation to protect against the rare wrong one. That is the wrong trade for anything the game can undo.

**Mechanism.** Add a `VoxrCommandReversibility { Reversible, Irreversible }` to `VoxrCommandDefinition`, and split the fire event into three:
- `OnCommandSpeculative(VoxrCommand)` — fired at the earliest defensible moment (P1's partial commit, before the stability gate has fully elapsed) for `Reversible` definitions only. The game *acts*.
- `OnCommandRetracted(VoxrCommand)` — fired when the final contradicts the speculation. The game undoes, **loudly** — a retraction the player does not notice is strictly worse than a delay.
- `OnCommandCommitted(VoxrCommand)` — the speculation survived; the game may drop its undo record.

`Irreversible` definitions take the opposite path: they *acknowledge* at the same early moment (`Deciding`/`Resolving` from P6, or the officer's "Aye, captain…") and execute only on the final, after the full gate. This is what `requiresConfirmation` becomes for the cases that genuinely need it — and for cases that do not, it is deleted from the authoring surface's routine use.

**Grounding.** R5's confirmation-and-undo doctrine, stated directly: "*prefer undo over confirmation where the action is reversible — a confirmation costs every correct utterance a turn; an undo costs only the wrong ones*", and "*reserve explicit confirmation for destructive, non-undoable actions*". R5 3.3 (Bashar et al., CHI '26): repetitive vocal interaction is quickly judged tedious, so every added turn is a real cost. Nowrin & Vertanen's **2 % insertion detection** (*reported*) is the constraint on the design: retraction must not rely on the player noticing. Baumann/Skantze (NAACL/EACL 2009, numbers `UNVERIFIED`): naive users *preferred* an incremental system and rated it more human-like — the justification for acting early at all. R6's `userSignal` field (undo / repeat / cancel within a short window) is the telemetry that would measure whether this helps.

**The speculation gate should be an agreement test, not a timer.** If P1 adopts N-best parse agreement (R2 finding 3), the natural rule is: **speculate when all N alternatives parse to the same intent; wait when they do not.** That is free of latency, unlike an N-partial temporal gate, and it fails in the right direction — divergent alternatives are exactly the sibling-tie case, which already has a designed answer (ask). Same caveat as in P1: N-best deletes per-word `conf`.

**Code impact.** Stage 7 and the public API. `VoxrCommandDefinition` is a **public readonly struct with public readonly fields** (`VoxrCommandDefinition.cs:13-19`) — adding a field is source-compatible but not binary-compatible (audit §6.5), and `VoxrCommandAsset` (`VoxrCommandAsset.cs:13`) needs a new serialized field with a safe default. `VoxrCommandRecogniser` gains the three events and a small speculation ledger (intent + token span + a game-supplied token). Debounce (`:991`) must not suppress the *commit* of an already-speculated command, and `OnCommandRecognised` must still fire exactly once per accepted command per the published contract (`api/command-recogniser.md:42`) — the cleanest reading is that `OnCommandCommitted` and `OnCommandRecognised` fire together, and a speculative fire is never an `OnCommandRecognised`.

**Expected effect.** Removes the stability gate's cost (P1's N-partial wait, tens to low hundreds of ms) from every reversible command, and removes a whole conversational turn from every reversible command that today sets `requiresConfirmation`. Magnitude: **unmeasured** — no report in the corpus measures speculative-execution-with-undo for voice commands, and the retraction rate is exactly P1's contradiction rate, which is also unmeasured.

**Cost.** 5–7 days in the package. The larger cost is on the game side: an undo path per reversible intent, which the package can specify but cannot supply.

**Risk and kill criterion.** A retracted action the player already reacted to is worse than a slow correct one — a speculatively-fired "launch missiles" cannot be un-launched by an event. **Kill criterion: any command whose retraction is not itself a visible, instantaneous state change is not `Reversible`, full stop**, and the docs must say so in the same voice as the `allowPartialMatch` warning. Second kill criterion: if P1's measured contradiction rate exceeds ~1 %, the retraction rate makes the feature user-hostile and it dies with P1. Third: this must not ship before FA/h telemetry (P11) exists, because it increases the number of actions taken per utterance and the project currently has **no false-accept denominator at all** (R6 §7).

**Cheapest validating experiment.** Offline, from P1's harness instrumentation: the contradiction rate *is* the retraction rate, so P1's experiment answers this one too at zero additional cost. Then in-game telemetry: `userSignal` rates (undo/repeat/cancel within 3 s) with speculation on vs off. **Human-only, on device:** whether a retraction is perceivable at all in a headset mid-combat — this is the question that decides the feature and it cannot be answered from WSL.

**Migration.** New enum defaults to `Irreversible`, so every existing authored command keeps today's behaviour exactly. New events are additive; existing subscribers see no change unless a definition opts in. `requiresConfirmation` is not removed — it is re-documented as the `Irreversible` path's optional second gate.

---

### P8 — Deixis fusion: bind slot values to gaze/pointing through VOSK's per-word timings

**Problem.** The player must name what they can see. Naming is where the substitutions live ("hotel one" against a grammar full of alphanumeric siblings), and every named value is a grammar entry: `GenerateGrammarJson` emits every slot value and alias key as a phrase *plus* each of its words (`VoxrCommandParser.cs:3124-3134`). Meanwhile the decoder already emits `start` and `end` per word, `VoxrJsonParser` already parses them (`:86-87`) into `VoxrWord` (`VoxrResult.cs:9`) — and **only `Text` and `Confidence` ever reach the parser** (`InstanceBuildWordConfidence`, `VoxrCommandParser.cs:3802-3838`; audit §3.2: "timing is never consulted by any scoring rule").

**Mechanism.** The maintainer's own recommended integration point: a `{ship*}` slot suffix opting a position into deixis, resolved by a game-supplied `IVoxrDeixisResolver` at slot-match time, between parse and accept (`anaphora-resolution-analysis.md`, "Slot-time hook … **Recommended**"). The addition this proposal makes is the **fusion window**: resolve against a ring buffer of gaze/pointing samples over `[word.start − 1.0 s, word.end + 1.0 s]`, weighted by proximity to the word — *not* against gaze at command-fire time, which under a 2.0 s buffer window can be two seconds stale.

**Two hazards the plan does not yet name, both found in the code:**
1. **The timing base is broken across a buffered utterance.** `UtteranceBuffer.Append` copies each final's `VoxrWord[]` into one flat array with `Array.Copy` and no rebasing (`UtteranceBuffer.cs:22-36`). VOSK's `start`/`end` are relative to the recogniser's own utterance segment, so a buffer assembled from three finals has three restarting timelines. Fusion needs each final stamped with the Unity time at which it was dispatched, and word times rebased against it. **This must be fixed before any timing-based feature, and it is invisible today because nothing reads the timings.**
2. **A dropped in-grammar word leaves no timing hole** — field-verified (`00-brief.md` §3): neighbouring alignments absorb it. So timings are trustworthy only for words that were actually emitted, and the fusion window must never be inferred from an absence.

**Grounding.** Bovo et al. (2025): **+26.5 %** coreference-resolution accuracy from adding gaze + laser pointing + scene metadata to a VR speech transcript versus speech alone, 12 participants (*reported*). Chen, Grubert & Kristensson (2024, AssistVR, n=24, 2 592 trials): at 4 targets, search **19.8 s vs 35.9 s** (30.5 % faster) and repeat **12.5 s vs 23.3 s** (86.4 %), NASA-TLX **4.06 vs 4.94** — but at **1 target the baseline won** (9.34 s vs 14.5 s) (*reported*), which is the warning not to make voice the only path to a single-target action. Fusion timing: gesture overlapped speech in 95 % of constructions, intermodal lags ~0.3–0.8 s, ~1 s threshold sufficient (Chen et al., OZCHI 2004 — R5 marks this **`UNVERIFIED`**, so the ±1.0 s window is a starting hypothesis to be measured, not a constant to hardcode). Oviatt's 19–41 % mutual-disambiguation error suppression is likewise **`UNVERIFIED`** per R5 3.13 and must not be quoted as fact.

**Code impact.** Stages 5–6. `VoxrSlotDefinition` gains a category; the pattern DSL gains `*` (parsed at `VoxrCommandAsset.cs:40`, with `ExtractSlotName`/`IsOptionalSlot` at `VoxrCommandParser.cs:4115,4127`); `GenerateGrammarJson` adds ~10 closed-class words; `TryMatchScored` (`:3467`) gains one delegate call at the slot-match site; word timings must be threaded through `UtteranceBuffer` → `ProcessParsedResultsCore` → the resolver (the rebase fix above). New public interface `IVoxrDeixisResolver` — note this would be the **first C# interface anywhere in `Runtime/`** (audit §4.1).

**Expected effect.** Grammar *shrinks* where a value list is replaced by a pronoun. Utterances shorten, so fewer words can be misheard. Effect size on VoXR: **unmeasured**; the +26.5 % is Bovo et al.'s on GPT coreference over VR transcripts, a different system and task.

**Cost.** Phase 1 (ellipsis auto-fill, no grammar change, no timings) 2–3 days per the maintainer's own estimate; Phase 2 (typed pronouns + resolver + timing rebase) 8–12 days; Phase 3 (gaze) mostly game-side.

**Risk and kill criterion.** A wrong referent is a **silent wrong action** — the same failure shape as the phantom command the leading-required-miss bar was shipped to stop (`scoring.md:213-215`), reintroduced on a new axis, and per Nowrin & Vertanen players detect ~2 % of insertions. **Kill criterion:** the resolver must return a confidence and route near-ties into the existing pending/disambiguation path rather than picking (R5 3.1's own mitigation, and the maintainer's stated ambiguity policy). If the resolver cannot produce a usable confidence, the feature does not ship past Phase 1.

**Cheapest validating experiment.** Phase 1 only, offline: replay the 699-row corpus through the A/B rig with last-target auto-fill on and off, and count previously-unfilled required slots that resolve correctly — zero device time, zero new fixtures (R5 recommendation 1). Separately and independently: a unit test that the timing rebase produces monotonic word times across a three-final buffered utterance. **Human-only, on device:** one in-headset session logging utterances-per-successful-intent with and without ellipsis (HandProxy's **1.09 attempts/command**, *reported*, is the external reference point).

**Migration.** `*` is new syntax, so existing assets are untouched; `{ship}` keeps strict semantics. `VoxrSlotMatch.Value` must continue to be a registered slot value (the resolver substitutes *before* delivery), preserving the documented guarantee (`VoxrCommand.cs:15-21`). `VoxrSlotDefinition` gaining a field is source- but not binary-compatible (audit §6.5).

---

### P9 — Activation: gaze-gated listening, a bounded repair protocol, and an activation *hook* rather than a wake word

**Problem.** VoXR ships two modes (`VoxrListeningMode { Continuous, PushToTalk }`, `push-to-talk.md:31`) and the docs correctly argue PTT is the default because grammar mode has no silence output (`push-to-talk.md:143`). But `Continuous` is the only hands-free option and it inherits that whole false-trigger problem. Separately, repair is unbounded: `pendingTimeout` bounds **silence**, not **attempts** (rule R22; `VoxrCommandRecogniser.cs:105`, `PendingCommandHandler.cs:256-268`), so a noisy room can loop a disambiguation indefinitely.

**Mechanism.** Three additions, one rejection.
1. **`VoxrListeningMode.GazeGated`.** The controller starts recognition when a game-supplied attention predicate fires and — crucially — does **not** stop the moment attention leaves: it holds while speech is in progress (first partial received, i.e. P6's `Hearing`) and stops on end-of-utterance or timeout. This is Look and Talk's asymmetric rule: **strict before speech onset, relaxed once the user starts speaking**.
2. **`IVoxrActivationGate`** — a one-method interface the game implements (head-gaze ray, eye gaze on Quest Pro/3, controller proximity, a wake-word engine of its own choosing). The package ships the *seam*, not a model.
3. **A repair-attempt counter and `OnRepairExhausted`.** Meta's conversation-design standard is **two repair attempts then hand off** to another modality. VoXR has `OnCommandPending`/`PendingAmbiguity`/`pendingTimeout` and no counter.

**The rejection:** do **not** ship a wake word inside the package for v1. openWakeWord's *code* is Apache-2.0 but its **pre-trained models are CC-BY-NC-SA 4.0** (*reported*, R5 3.9) — putting them in an Apache-2.0 package is the finding's headline hazard. Training a clean one from the package's own TTS pipeline is genuinely feasible (openWakeWord documents custom words from 100 % synthetic speech in under an hour, and the input contract — 16-bit 16 kHz PCM in 80 ms frames — already matches what `recognition_loop` produces at `vosk_bridge.cpp:122`). But hosting it on Quest means a second inference runtime, and Meta's own guidance says Unity Inference Engine **does not use the NPU and runs on the Unity main thread**, mitigated only by layer-per-frame scheduling (*reported*, R5 3.11). R1 closes the door harder from the native side: **Meta does not expose the Hexagon NPU to third-party Quest apps at all**, so ONNX Runtime's QNN/HTP execution provider is unavailable regardless of what it supports — "plan for CPU only" — and XR2 Gen 2 has only 2 performance cores with a CPU "33 % improved" over Gen 1. Taking ONNX Runtime into the bridge also costs **~15 MB** of `libonnxruntime.so` on arm64 (R1), nearly doubling the package's native payload for a wake word. That is a whole project. `IVoxrActivationGate` lets a game do it without the package taking on the licence, the runtime, or the APK.

**Grounding.** Google *Look and Talk* (2022): shipped three-phase on-device activation with the strict-then-relaxed attention rule and latency "comparable with hotword-based systems" (*normative/vendor*). Meta *Voice Best Practices*: never open the mic without consent; two repair attempts then hand off (*normative*). openWakeWord's operating target: **<5 % false rejects at <0.5 false accepts/hour** (*reported*). Porcupine's <1 FA/10 h and <200 KB are **vendor claims** under a commercial licence and are not adoptable.

**Code impact.** Stage 7 only; nothing touches the native bridge. `VoxrListeningMode` gains a member (`Runtime/VoxrListeningMode.cs`, documented at `api/data-types.md`); `VoxrPushToTalkController` gains the gate reference and the hold-while-speaking logic — note its existing `Update` already reconciles the Android permission race (`VoxrPushToTalkController.cs:189-195`) and the new hold logic belongs beside it. The repair counter lives in `PendingCommandHandler` beside `CreatedTime` (`:296`).

**Expected effect.** False starts per minute in `GazeGated` vs `Continuous`: **unmeasured**. The repair bound is not a performance claim — it converts an unbounded loop into a bounded one, which is a correctness property.

**Cost.** 3–4 days for all three.

**Risk and kill criterion.** Gaze gating narrows *when* the mic is open; it does **not** fix in-grammar noise decoding, and claiming otherwise would be unfounded (R5 3.7's own caveat). Quest 2 has no eye tracking, so head-gaze is a coarse proxy. **Kill criterion:** if `GazeGated` false starts per minute (with game audio playing) do not land materially below `Continuous`, the mode is not worth its documentation surface and PTT remains the only recommendation.

**Cheapest validating experiment.** The repair counter is testable entirely in PlayMode through `InjectText` (`VoxrCommandRecogniser.cs:299`) — no audio at all. **Human-only, on device:** one in-headset session in a quiet room and one with game audio, logging false starts per minute in `GazeGated` vs `Continuous` (R5 recommendation 5).

**Migration.** New enum member — a `switch` in game code over `VoxrListeningMode` without a `default` will now be non-exhaustive, which is the only source-level break and is worth calling out in `CHANGELOG.md`. `IVoxrActivationGate` is optional; unset ⇒ `GazeGated` behaves as `PushToTalk`.

---

### P10 — Decouple the decoder's grammar scope from the parser's active set: gap-free switching *and* out-of-context rejection

**Problem.** Two documented failures share one cause. (a) The audio gap exists because switching sets rebuilds the grammar (`SetActiveSets` → `RebuildParser` + `RebuildGrammar`, `VoxrCommandRecogniser.cs:279-292,435-439`; `GrammarManager.ForceApply:38-53`). (b) "Wrong-mode rejection has no explanation" (`00-brief.md` §3) exists because with a restricted grammar the out-of-set word decodes as `[unk]`, so the game cannot say "that's a navigation command; you're in weapons mode". Both disappear if the decoder is given the **union** vocabulary while the parser keeps the **active** set.

**Mechanism.** A `grammarScope` option: `ActiveSets` (today) | `AllRegisteredSets` (new). Under `AllRegisteredSets`, `GenerateGrammarJson` is built once over every registered set and never rebuilt on a set switch — `SetActiveSets` becomes a pure parser rebuild, which is exactly what `NotifySlotChanged` already is (`:340-346`; audit §9.12 notes these are "two different costs behind two similarly-named methods"). Then add a **shadow parse**: when the active-set parse yields nothing, re-run the parser over the inactive sets' definitions and, on a good match, fire `OnCommandOutOfContext(intent, setName, transcript)` instead of (or alongside) `OnUnrecognisedSpeech`.

There is an important precedent for this move already in the codebase: rule R23 states the grammar **always reflects the full universe of slot values** — "a dynamic provider narrows the parser, never the decoder" (`api/command-recogniser.md:59`). P10 applies the same principle one level up, from slot values to command sets. It is not a new idea in this system; it is the existing idea, unfinished.

**The lever that makes this safe — and it is already shipping, unused.** The obvious objection to a union grammar is that a wider phrase list means more competing hypotheses and therefore more substitutions. But `vosk_recognizer_new_grm` does **not** build a hard grammar: R2's source read (finding 1, `VERIFIED`) shows it maps each JSON token through the model's symbol table and calls `estimator.AddCounts(sentence)` on a Kaldi `LanguageModelEstimator` with `ngram_order = 2, discount = 0.5`, then lookahead-composes the estimated bigram FST with `HCLr.fst`. Kaldi documents `AddCounts` as "for each n-gram in the sentence, `count[n-gram] += 1`". **So the grammar is a bag of *observed sentences* and multiplicity is a weight — an entry listed three times contributes three times the counts.** `GenerateGrammarJson` accumulates into a `HashSet<string>` and emits each entry exactly once (`VoxrCommandParser.cs:3076-3175`, `AddPhrase` at `:3182-3192`), **throwing the lever away**.

That converts P10's central risk into a tunable: emit **active-set** phrases at weight *k* and **inactive-set** phrases at weight 1, so out-of-set commands remain *decodable* (enabling the shadow parse and the out-of-context message) while remaining *unlikely* (preserving most of what grammar-narrowing buys today). A set switch then becomes a change of weights, and — if the weights must change at all — that is still a grammar rebuild, so the strongest form is a single static union grammar whose weights are set once and whose narrowing happens entirely in the parser. R2 prices the emission change at "~20 lines with no native, API or asset-format impact"; note its warning that the effect "passes through a hard-backoff estimator with a 0.5 discount, so it is **monotone but not proportional**", and its open question — "how many repetitions move the decision boundary at all?" — is unanswered. Ownership: the weighting mechanism belongs to the accuracy track (it is also how `[unk]`'s prior would be raised for rejection); P10 is a consumer of it.

**Grounding.** R5's error-repair doctrine: *distinguish "not heard", "not understood", and "not valid here"*, and the explicit proposal of "a shadow index of all registered commands across all sets … then fire a distinct `OnCommandOutOfContext` event". R5 3.3: voice's best job is mode-switching, so the gap sits on the most-used path. R3's "what this area cannot fix" concedes that explaining a wrong-mode rejection "requires decoding outside the active set, i.e. two grammars or a full-vocabulary second pass" — P10 is the third option R3 does not consider: **one wider grammar and a second *parse*, which is free.**

**Code impact.** Stage 4 and 6. `GrammarManager.Rebuild` (`:19-24`) takes the full command list under the new scope; `VoxrCommandRecogniser.SetActiveSets` (`:279`) stops calling the grammar rebuild; a second `VoxrCommandParser` instance (or a second definition array on the same instance) holds the inactive sets; `ProcessParsedResultsCore`'s no-match branch (`:1087-1096`) gains the shadow attempt.

**Expected effect.** Audio gap on set switch: → **0 ms** by construction (no rebuild happens). Accuracy cost of the wider grammar: **unmeasured and potentially significant** — a bigger phrase list means more competing hypotheses, which is precisely the mechanism behind the documented "cease fire" out of set → `safe five` substitution class. This is the proposal's whole tension and it is honest to state it as the main open question.

**Cost.** 4–6 days including a second parser instance and its memory/allocation review (the zero-allocation steady state is pinned by `ZeroAllocPollPathTests.cs`; the shadow parse runs only on the no-match path, which is the right place for an occasional cost).

**Risk and kill criterion.** The wider grammar makes in-grammar substitution *worse*, trading a 50 ms gap for a permanent accuracy loss — and the documented "cease fire" out of set → `safe five` substitution is exactly the mechanism. **Kill criterion — and this is the one that must be measured before any code:** replay the fixture corpus through the real decoder at three settings — (i) active-set grammar, (ii) unweighted union, (iii) union with active-set phrases weighted — and compare intent accuracy. **If the weighted union does not recover essentially all of (i)'s accuracy, `AllRegisteredSets` ships off by default and is documented as a latency-for-accuracy trade, not a free win.** Secondary risk: `OnUnrecognisedSpeech`'s fire/silent set is published as **exhaustive** (`scoring.md:309-321`), so adding a sixth outcome is a documentation rewrite, not just a code change (audit §8.11). Tertiary: R2's warning that over-biasing has a measured cost in the biasing literature — TCPGen's own paper reports book-level bias lists "increased error rates in unbiased words" — so *k* has a top as well as a bottom.

**Cheapest validating experiment.** The three-grammar corpus replay above, on the WSL harness — the decisive experiment costs one harness run and no package code, and it doubles as the accuracy track's measurement of the weighting lever ("how many repetitions move the decision boundary at all?", R2 finding 1). The out-of-context attribution half is testable entirely offline: replay the corpus against a restricted active set and count how many utterances the shadow parse correctly attributes to an inactive set instead of reporting them unrecognised (R5 recommendation 5). **Human-only, on device:** confirming the gap actually reaches zero on Quest, since the 50 ms figure is a Quest claim.

**Migration.** New option defaults to `ActiveSets` — bit-identical to today. `OnCommandOutOfContext` is additive. `command-sets.md:146-162` ("The Audio Gap") and `scoring.md:309-321` both need rewriting. R18 (deferred rebuild while pending, `:369-375`) becomes mostly moot under the new scope but must stay for the old one.

---

### P11 — The instrument: T0–T5 latency stamps, player-build session logs, and the two missing log fields

**Problem.** Nothing in the table above except the constants can be checked, and row 7 — the Quest decoder catch-up, possibly the single largest term — exists only as prose in `command-recognition.md:286`. The session log is **Editor-only**, so the actual target device produces no log at all; only the round *winner* is logged, so "what nearly fired" is invisible; **barred rounds leave no trace**, so the cost of the rule 2.0.0 shipped on cannot be measured from the log it should appear in; and there is **no negative-hours denominator anywhere**, so VoXR's scariest failure — the phantom command — has never had a rate attached to it (R6 §7, §"the session debug log").

**Mechanism.** R6's Tier 4 and Tier 5, adopted essentially as written:
- Timestamps at six boundaries: **T0** ring-buffer write (`audio_capture_audiorecord.cpp:201` — a 48 kHz sample counter is exact and free), **T1**/**T2** either side of `vosk_recognizer_accept_waveform_s` (`vosk_bridge.cpp:125`) and the result push (`:130-132`), **T3** the main-thread drain (`VoxrSpeechRecogniser.cs:526-536`), **T4** the buffer close *recording which path closed it* (window / eager / VAD / release flush), **T5** event dispatch (`VoxrCommandRecogniser.cs:1100`).
- Report **p50/p90/p99 per stage, never means** — Shangguan et al.: model size and FLOPS "are not always strongly correlated with observed UPL" (*reported*).
- End-of-speech reference is free on the fixture corpus: `generate.py` pads `TAIL_SILENCE_S = 1.0`, so EOS = duration − 1.0 s by construction — FastEmit's forced-alignment trick without an aligner (R6 §8).
- Session log schema v3: add **`barred`** and **`runnerUpIntent`/`runnerUpScore`**, add `latencyMs`, add `sessionSeconds` (**the FA/h denominator — this one field is why telemetry beats bug reports**), add `userSignal` (undo/repeat/cancel within a short window). Compile the log into player builds behind an opt-in.
- Fix the locale bug while in there: `rejectReason` numbers are culture-formatted (`scoring.md:305`), which breaks any parser built on the log.

**Grounding.** R6 §8, §12, Tiers 4–5, and recommendation 3 verbatim: the 2.0.0 bar "was justified by 'zero legitimate leading-miss recoveries in 73 field utterances', which is a *false-reject* argument evaluated on a positive-only corpus; the bar's actual purpose is reducing false accepts, and no false-accept rate was ever measured". Picovoice's public benchmark fixes FA at **1 per 10 h** and compares miss rate there (*methodology*). Verma & Murari (NeurIPS 2021 HCAI workshop) for `userSignal` as unprompted implicit feedback.

**Code impact.** Every stage. **Native ABI change** — the result struct crossing the C ABI widens by two `int64`s — so the rebuilt arm64 `.so` ships in `Runtime/Plugins/Android/arm64-v8a/` and the change is human-verified on device per the project bindings. Managed: `VoxrSpeechRecogniser`, `UtteranceBuffer`, `VoxrCommandRecogniser`, the Editor session-log writer (moved out of `#if UNITY_EDITOR` behind a runtime opt-in). Consent UI is a sample, not a package requirement.

**Expected effect.** No latency or accuracy effect. It converts rows 7, 11 and the audio gap from prose into numbers, and converts the phantom-command class from an anecdote into a rate. **Everything else in this document is conditional on it.**

**Cost.** R6 estimates ~4 days for T0–T5 + schema v3, ~5 days for telemetry/consent/aggregation. Call it 8–10 including the log-in-player work.

**Risk and kill criterion.** Two real ones. (1) Instrumentation on the hot path must not allocate — the zero-allocation steady state is pinned (`ZeroAllocPollPathTests.cs`, audit §8.3), and a green alloc test must be mutation-verified. (2) The ABI change is the highest-risk native edit in this whole document, since it touches the struct every result crosses. **Kill criterion:** any allocation regression on the poll path, or any device-side instability from the widened struct, reverts to a *sampled* (1-in-N utterances) stamping scheme.

**Cheapest validating experiment.** T1–T2 alone, in the WSL harness, in sample time — that measures decode cost with no ABI change and no device. Then the full chain. **Human-only, on device:** the entire measurement of row 7, which is the point of the exercise. Per project bindings this is explicitly not claimable from WSL.

**Migration.** Schema v3 is additive over v2, which is already documented as an additive-change schema. Telemetry defaults **off** with a single explicit consent prompt and no audio leaving the device by default (R6 Tier 5).

---

### P12 — Developer surface: decoder-in-the-loop A/B, a confusability lint, annotation sets, a most-confused-pairs dashboard, and a player "voice check"

**Problem.** The A/B rig stages **nine parser `.cs` files and zero VOSK code** (R6 §"the 699-utterance A/B rig"), so no decoder-side, grammar-side or DSP-side change is measurable at all — which is most of what this document proposes. The three named field substitutions (`switch to weapons` → `switch two weapons`, `all modes` → `fall modes`, `cease fire` → `safe five`) are all *author-time* problems that none of the six existing construction warnings can see. And the package has no way for a player to find out whether their mic is even working before they are asked to give a tactical order.

**Mechanism.** Six items, all Editor/tooling except the last.
1. **Put the decoder in the A/B loop.** The push-audio seam already exists and has **zero C# call sites** — `vosk_bridge_start_push`/`vosk_bridge_push_audio` bound at `BridgeNative.cs:53,60` with the comment "No runtime caller yet" (`:49-50`). Wiring the rig through it is plumbing, not architecture (R6 recommendation 4). Move the rig out of gitignored `Planning~/`, where a rename sweep can silently break it.
2. **Stop silently dropping OOV grammar words — an Editor error naming the word.** This is R2's single cheapest high-value finding and it is a pure DX bug. `Recognizer::UpdateGrammarFst` emits `KALDI_WARN "Ignoring word missing in vocabulary: '<token>'"` for any grammar token the model does not know — and `vosk_set_log_level(-1)` at `vosk_bridge.cpp:215` **suppresses it**. So "cqb" is dropped from the grammar with no signal to anyone: not the parser, not the bridge, not the log; "the failure surfaces only as `[unk]` in the field", which is exactly how `KNOWN_LIMITATIONS.md` currently documents it — as a mystery rather than as a detectable condition. Fix: iterate the generated grammar entries through `vosk_model_find_word` at `Configure` time and raise an **Editor error naming the word**. R2 prices it at ~30 lines and calls it the change that "converts a documented mystery into an Editor error". Pair with a G2P-driven utility proposing a spelled alias for any rejected word (which is also item 3's mechanism). **One caveat before this is planned: `02-verified-facts.md`'s symbol dump enumerates only `vosk_recognizer_*` symbols, so `vosk_model_find_word`'s presence in the shipped `libvosk.so` is asserted by R2 but not confirmed by the authoritative export list. Confirm it with `nm -D` before committing to this design** — if it is absent, the same check can be done Editor-side against the model's symbol table with more work.

3. **A seventh authoring warning: pairwise phonetic confusability.** Not the patent's acoustic simulation — a G2P dictionary (CMUdict, BSD-style) plus weighted phone-edit distance across every pair of pattern literal runs and slot values, warning when two entries with *different intents* fall under a threshold. Editor-only so the dictionary never ships in the APK. Delivered like Alexa's Utterance Conflict Detection: runs on every grammar build, **warns, never blocks** — which is the convention the existing five passes already establish (audit §5.7).
4. **`VoxrAnnotationSet`** (utterance → expected intent + slots) run through `InjectText` as an EditMode test and an Editor button, modelled on Alexa's NLU Evaluation Tool. VoXR has the injection path and the fixtures and lacks only the asset and the report.
5. **A most-confused-pairs dashboard** over the session logs (P11's schema v3, with `runnerUp*` making near-misses visible for the first time), plus per-round outcome counters — fired / barred / below `minScore` / below `minConfidence` / debounced / diverted — so `scoring.md`'s gate table becomes a measurable dashboard rather than prose. Static HTML, not a service. Headline metric: **utterances per successfully-executed intent** (HandProxy's **1.09**, *reported*, as the external reference point).
6. **A player-facing "voice check" scene.** `vosk_bridge_get_input_level` (`vosk_bridge.cpp:342`, bound at `BridgeNative.cs:64`) is a working rolling RMS with **no consumer anywhere** — its intended Tier-D consumer was dropped (audit §9.5). A first-run scene that shows a live level meter, asks for one phrase, and reports back is a ~half-day sample built entirely on code that already exists and currently rots.

**Grounding.** R5 3.14: Microsoft's Grammar Confusability Metric (US7844456B2) for the concept, Alexa's build-time conflict detection for the delivery model, LLM-Synth4KWS (Interspeech 2025, **+11.3 % confusable-AUC**, *reported*) for author-time generation of confusable variants. R6 recommendations 4 and 5, and R6's audit of the rig and the log. R5 3.12 for utterances-per-intent as the single headline player metric. Meta's guidance that transcript display doubles as mic-state feedback and as instruction.

**Code impact.** Editor and `Tests~`/`Tools~` only, except the voice-check sample. No runtime cost. Note the rig compiles the parser outside Unity against a `UnityStub`, so `UNITY_EDITOR` is undefined there and the Editor-only diagnostics vanish (audit §7) — a decoder-in-the-loop rig must not accidentally start depending on them.

**Expected effect.** The confusability lint's value is testable retrospectively and cheaply. Everything else is enablement, not effect.

**Cost.** ~3 days for the decoder-in-the-loop rig, ~4 for the lint, ~2 for annotation sets, ~3 for the dashboard, ~0.5 for the voice check.

**Risk and kill criterion.** "A confusability warning that fires on half of a real grammar is noise and will be ignored" (R5 3.14). **Kill criterion:** run the candidate metric over the *existing* sample command sets; if the three known field substitutions do not rank in the top confusable pairs, or if the false-positive count on the shipped sample grammar is not small enough to be actionable, the metric is wrong and does not ship (R5 recommendation 3's own retrospective test — ground truth already exists).

**Cheapest validating experiment.** Exactly that retrospective scoring run: pure offline, no device, no Unity, against ground truth the project already has.

**Migration.** All additive. New assets and an Editor window; the lint is a warning like its five neighbours — though note `RunValidationWarnings` is the one existing pass that is **not** `[Conditional("UNITY_EDITOR")]` and therefore logs in shipped players on every rebuild (`VoxrCommandParser.cs:732`, audit §9.9); the new pass must not repeat that mistake.

---

## Roadmap

Phases are ordered by dependency, not by value. **Nothing in Phase 2 or later should ship before Phase 0 exists**, because every one of those proposals changes *what fires* and the project currently has no instrument that can see the decoder (R6 §"the compound problem") and no false-accept denominator at all (R6 §7).

### Phase 0 — Measure (blocking; ~1.5–2 weeks; nothing ships to users)

| Item | Depends on | Device? |
|---|---|---|
| **P11** T0–T5 stamps, log schema v3 (`barred`, `runnerUp*`, `sessionSeconds`, `latencyMs`), log in player builds | — | **Human-only** for row 7 |
| **P12.1** decoder in the A/B loop via the existing push-audio seam | — | no |
| Measure the audio gap in sample time (P5's first experiment) | P12.1 | **Human-only** for the Quest number |
| Measure the partial→final contradiction rate (P1's kill criterion) | P12.1 | no |
| Measure confidence NCE/ECE over existing session logs (P6's kill criterion) | — | no |
| R2's "two"/"to" red-then-green confidence pin — tests the *explanation*, not the correlation | — | no |
| Classify the 73 field-log false triggers into non-speech vs speech-like (P4's free upper bound) | — | no |
| Adopt CHiME-4 dual reporting + rename the 16 fixtures / 699 rows to *development* sets | — | no |

R6's recommendation 1 — recording `human-core`, 10 speakers × 30 phrases × 3 conditions, split into a used half and a **sealed** half — belongs here and **cannot be delegated**. It is the only input that makes any accuracy ruling below meaningful.

### Phase 1 — ≤ 1 week wins, independent of each other (ship as they land)

- **P2** endpointer profile via `model.conf` at extraction (+ the cache-key fix). *Config only, no native change — this is the largest single latency cut available without touching C++.*
- **P3** read chunk 4096→1024, sleep 10→5 ms. *Two constants; gated on a device CPU check.*
- **P6 (part)** `OnListeningStateChanged` + acknowledgement at first partial + docs for a diegetic filler. *No native change, no parser change; the cheapest UX win in the whole document.*
- **P9 (part)** repair-attempt counter + `OnRepairExhausted`. *Testable entirely through `InjectText`.*
- **P12.6** the "voice check" sample over the already-dead `get_input_level`.
- **P12.2** the OOV Editor error (`vosk_model_find_word` over the generated grammar at `Configure`). *~30 lines; converts a documented mystery into an authoring-time error.*

### Phase 2 — The endpointing redesign (multi-week; the core of the thesis)

- **P1** partial-driven semantic endpointing: `set_partial_words`, word-carrying partials, stability gate, **commit watermark**. Depends on Phase 0's contradiction measurement and on P3 for partial cadence; benefits from P2 (a shorter endpointer also produces partials sooner).
- **P4** VAD close for everything P1 deliberately leaves on the timer — the pending, ambiguous and incomplete paths. Depends on the licence review; independently useful for the noise cue.
- **P6 (rest)** word-level partial transcript + confidence marking. Depends on P1's `set_partial_words` **and** on Phase 0's NCE result.

### Phase 3 — Structural (multi-week, parallelisable with Phase 2)

- **P10** grammar-scope decoupling (weighted union grammar) → gap-free switching + `OnCommandOutOfContext`. Gated on the three-grammar accuracy measurement, which needs P12.1, and on the accuracy track shipping the multiplicity-weighting change to `GenerateGrammarJson`.
- **P5** keep-capture-running across a rebuild. Not an alternative to P10 — P10 removes the *set-switch* rebuild, P5 removes the audio loss from the rebuilds that remain (dynamic slots, explicit `RebuildGrammar`). Do P5 only if the measured gap is under ~400 ms.
- **P12.3–5** confusability lint, annotation sets, dashboard.

### Phase 4 — New interaction models (multi-week; each needs everything above)

- **P8** deixis. Phase 1 (ellipsis) can start any time and is independently valuable; Phase 2 (pronouns + resolver) needs the word-timing rebase fix; Phase 3 (gaze) needs P8.2 and is mostly game-side.
- **P7** speculative execution with retraction. Hard-depends on P1 (its retraction rate *is* P1's contradiction rate), on P6 (retraction must be loud), and on P11's FA/h telemetry.
- **P9 (rest)** `GazeGated` mode + `IVoxrActivationGate`.

### What needs the evaluation harness first (explicitly)

P1, P2, P4, P7, P10 all change which commands fire or when. Each has a stated kill criterion above, and **not one of those criteria is checkable today**. P3, P5 and P11 change timing without changing decisions, and can be validated on latency numbers alone. P6, P9 and P12 are additive surface and need no accuracy evidence — which is exactly why they belong in Phase 1.

---

## What to stop doing

1. **Stop recommending `bufferWindow = 2.0` on Quest** (`command-recognition.md:282`, "Set it to `2.0` for Quest 3"). *Evidence:* it is two thirds of the entire controllable budget; the audit calls it "the dominant latency term in the whole pipeline" (§2.11); and its stated justification — "Quest 3 VOSK latency adds ~0.5–1.0 s to inter-result gaps" (`:286`) — is **unmeasured prose**. A number that large should not rest on an estimate nobody has checked. Replace the recommendation with a measurement (P11) and then with evidence-driven closing (P1/P4).

2. **Stop paying for split-avoidance twice.** *Evidence:* R3's one-line budget conclusion — rows 6 and 9 are both silence timers set conservatively against the *same* failure. Whichever mechanism ends up owning split-avoidance, the other should be relaxed in the same change, and the docs should say which one owns it.

3. **Stop shipping `disambiguateSiblingTies` off-by-default with no prompt.** *Evidence:* it is `false` at `VoxrCommandRecogniser.cs:126`, and the docs themselves say the flag is worse than the coin flip it replaces without a subscriber (`command-recognition.md` §Ambiguous Commands; audit §6.5). A feature that is dangerous unless the integrator writes UI the package does not ship is not finished. Ship the reference prompt (P6) or ship the flag on with one.

4. **Stop reporting "N of 699 rows changed" as evidence.** *Evidence:* R6 recommendation 5 — "a count of changed rows is not a test, and because the rows are dependent, no off-the-shelf test applies"; all 699 rows descend from the same **16** transcripts by four mechanical families, so the ±0.019 CI computed as if n = 699 "is a fiction" (R6 §"the 699-utterance A/B rig"). Replace with exact McNemar on per-utterance semantic accuracy plus a session/voice-blocked bootstrap, and internalise the floor: **fewer than 6 discordant utterances in one direction is never significant.**

5. **Stop treating a green 16-fixture run as acceptance.** *Evidence:* 95 % Wilson CI is [0.806, 1.000] at 16/16 and [0.640, 0.965] at 14/16 — the corpus cannot resolve a difference smaller than ~15 points (R6 §"the TTS fixture corpus"); and under Dwork et al. (*Science* 2015) those fixtures and the 699 rows are now part of the *training* procedure for the parser's rules, not a test set (R6 §12). Rename them to development sets in the docs and seal a holdout.

6. **Stop describing `minConfidence` as a noise filter, and stop tuning it.** *Evidence:* this is now a structural claim, not a tuning observation. With `max_alternatives == 0` VOSK writes `word["conf"]` from `kaldi::MinimumBayesRisk::GetOneBestConfidences()` — **MBR word posteriors normalised over an already-grammar-constrained lattice** (R2 finding 2, source-read of `recognizer.cc`), so the number answers "which in-grammar word was it", never "was this in-grammar at all". **`minConfidence` is therefore structurally incapable of rejecting noise**, and `KNOWN_LIMITATIONS.md`'s framing of it as a false-trigger workaround should be rewritten. The corollary is that "two" ≈ 0.50 is *correct*, not flat — it is a well-calibrated posterior over a genuine "to"/"two" ambiguity the grammar itself creates, which is why no tuning has ever fixed it. R6 §11 independently names the live possibility of near-zero NCE. Measure it (one alignment script over logs that already record `words[].confidence`), use the number for *display* and for acoustic sibling-tie detection instead (P6), and replace the gate with a calibrated score shipped **reported-not-gating** (R2 rec 5).

7. **Stop carrying the AAudio capture backend.** *Evidence:* 141 lines never selected by the default CMake config, which itself calls it "unmaintained legacy (broken on Quest)" (`CMakeLists.txt:11`; audit §9.5). It is a compile option nobody compiles, in a file sweep everybody reads. (By contrast, do **not** delete `vosk_bridge_get_input_level` or the push-audio seam — P12 gives both a consumer.)

8. **Stop treating the two C#/C++ DSP copies as parity.** *Evidence:* they have **already silently diverged** — the C++ AGC gates on the smoothed level (`agc.h:53`), the C# on the instantaneous sample (`Agc.cs:66`) — and the comment at `Agc.cs:63-64` points at a "divergence note in the class summary" that **does not exist** (audit §9.4). Every Editor-measured audio result is therefore taken on a slightly different signal than the device sees. Either test the two against each other or stop claiming parity in `Documentation~`.

9. **Stop deduplicating the grammar as though multiplicity were meaningless.** *Evidence:* `vosk_recognizer_new_grm` estimates a bigram LM by counting the JSON entries as observed sentences (`LanguageModelEstimator`, `ngram_order = 2`, `discount = 0.5`; Kaldi's `AddCounts` increments per n-gram), so **an entry listed three times weighs three times as much** — and `GenerateGrammarJson` collapses everything into a `HashSet<string>` and emits each entry once (`VoxrCommandParser.cs:3076-3175`). R2 finding 1 is `VERIFIED` on the VOSK source and prices the fix at ~20 lines with no native, API or asset-format impact. The package has been treating a weighted language model as a flat word list for its entire life. This is not primarily a latency finding — but it is the lever that makes P10 (and therefore the audio gap and out-of-context rejection) affordable, so the latency track has a direct stake in the accuracy track shipping it.

10. **Stop adding parser rules to fix decoder problems without an instrument that can see the decoder.** *Evidence:* six of the seven documented failure modes are decoder-side (`00-brief.md` §3), text injection and the A/B rig both cover only the parser, and the field set has no retained audio to replay (R6 §"the compound problem"). This is not an argument against the existing rules — each was earned — but the next one should not be proposed until P11 and P12.1 exist.

---

## Open questions for the maintainer

1. **How stable are grammar-mode partials on Quest?** Everything in P1, P6 and P7 rests on this and it is unmeasured. Related: does `vosk_recognizer_new_grm`'s bag-of-phrases graph have meaningful final states for multi-word phrases? R3 finding 1 raises the possibility that *every phrase boundary is a final state*, which would mean Kaldi's rule2 fires 0.5 s after any complete phrase — and would explain the observed mid-command splits exactly.

2. **Does the union grammar (P10) cost accuracy?** This is the decisive question for the audio gap, for out-of-context rejection, and for how much grammar-narrowing is actually buying today. One harness run answers it.

3. **Is the ~50 ms audio gap real?** It is Quest prose that has driven documented workarounds ("wait ~500 ms before speaking", `command-sets.md:160`). If it is actually 200 ms it is a priority; if it is 15 ms the workaround should be deleted rather than engineered around. **Note that R1 and R2 give opposite answers about whether `set_grm` fixes it** (R1: "an implementation artefact"; R2, from `recognizer.cc`: `SetGrm` refuses while running and rebuilds the FST anyway, so it "shortens one term"). I have taken R2's, because it read the implementation and R1 read the header comment — but this is a factual disagreement between two of your own reports and you may want it settled independently before P5 or P10 is planned.

4. **Cross-track sequencing: N-best or confidence display, but not both by accident.** The accuracy track's top recommendation (`set_max_alternatives`) **deletes per-word `conf` from VOSK's output entirely** (R2 finding 3, source-read). That would silently disable P6's confidence marking *and* push every command onto the `-1` bypass of `minConfidence` (rule S14). Which track goes first, and does the calibrated score (R2 rec 5) get built early enough to be the bridge between them?

5. **Which commands in the real game are genuinely reversible?** P7 lives or dies on this list, and only you can write it. My prior: "switch to navigation", "orient heading …", targeting and mode changes are reversible; anything that expends ordnance is not.

6. **Would you accept an occasional retraction in exchange for ~200 ms?** This is a taste question about the bridge-officer fantasy, not an engineering one, and it determines whether Phase 4 exists at all.

7. **Where does the gaze/pointing data come from, and does the game already publish targetable entities with categories?** R5 is blunt that "deixis cannot resolve what the game does not tell it". If the FTL-like already has a salience notion, P8 Phase 1 is days; if not, most of the cost is game-side.

8. **`set_partial_words` on Quest — does it slow the decode?** It is exported (`02-verified-facts.md`) and cheap in principle, but it makes VOSK build a word array on every partial, and P3 multiplies partial frequency by ~4. Untested combination.

9. **Is a second native dependency acceptable for the VAD (P4), and does TEN VAD's "Apache-2.0 with additional conditions" clear your licence bar?** This decides a 30× APK difference, not a rounding error: TEN VAD is a **532 KB self-contained `.so`**, while Silero is MIT but is an ONNX graph and therefore brings **`libonnxruntime.so` at ~15 MB** on arm64 (R1) — roughly tripling the package's native payload for a voice-activity detector. Worth a licence read *before* any prototyping, since the answer changes which one to prototype.

10. **Is `vosk_model_find_word` actually exported by the shipped `libvosk.so`?** R2 asserts it is and builds its cheapest high-value recommendation on it (the OOV Editor error, P12.2); `02-verified-facts.md`'s authoritative symbol dump enumerates only `vosk_recognizer_*` symbols and so neither confirms nor refutes it. One `nm -D` settles it.

11. **Are you willing to seal a holdout you do not look at?** R6 is explicit that this "is a human commitment, not a mechanism", and that the history in `Planning~/` shows how strong the pull is to look. Everything in Phase 0 is worth less if the answer is no.

12. **Is `OnPartialResult` allowed to change shape?** Adding a word-carrying overload is additive, but a game that currently subscribes with a method group would face an ambiguity. If the published contract in `api/speech-recogniser.md:56` is to be treated as frozen, P1 and P6 need a differently-named event and the docs get slightly worse for it.

13. **R4 was missing.** If it lands and says anything about the AGC, the mic front-end, exertion/Lombard robustness, or per-speaker adaptation, P2 (which also owns `--beam`/`--lattice-beam`), P4 (which sits directly on the AGC's output) and P11's metric set should be re-read against it before anything in Phase 1 is built.
