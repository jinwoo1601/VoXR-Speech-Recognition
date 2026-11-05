---
type: system
topic: agc-filter
tags: [front-end, dsp, agc, decimation, webrtc-apm]
status: draft
updated: 2026-11-05
sources:
  - Planning~/research/2026-09-05-next-level/05-rulings.md (rulings 5, 6, 10; shortlist items 7–8)
  - Planning~/design-docs/joined-rig.md (rule 10)
  - Planning~/design-docs/agc-filter/decisions/adr-0001-editor-native-dsp.md
  - Planning~/design-docs/agc-filter/decisions/adr-0002-own-fir-decimator.md
  - Planning~/design-docs/agc-filter/decisions/adr-0003-agc2-classes-pinned-source.md
  - Planning~/design-docs/agc-filter/decisions/adr-0004-tts-no-regression-acceptance.md
  - Planning~/design-docs/agc-filter/recon-2026-11-05.md
  - "Session direction 2026-11-05 (G0)"
  - "Session direction 2026-11-05 (settle)"
---

# Design: The R14 Decimation Filter and R15 AGC2 as One Unit

**Branch:** `design-agc-filter`
**Origin:** next-level plan items R14 and R15 (shortlist items 7–8; Phase D).
**Status:** **G1 LOCKED 2026-11-05.** Rulings in adr-0001 through adr-0004. This document is immutable from here; a contradiction found during implementation reopens it on a new design branch and re-locks.

> **What's decided.** The front end is one chain: 48 kHz capture → a DC-normalised long FIR of our own that keeps every third sample (adr-0002) → AGC2's adaptive digital controller in 10 ms frames, 16 kHz in and out, whose gain rises only after a run of confident speech frames from its voice detector, falls ungated, is capped by a noise ceiling and is rate-limited → the limiter, so the int16 handed to Vosk cannot overflow. Of APM only AGC2 runs, with fixed gain 0 dB and the input-volume controller, noise suppression and echo cancellation off. The Editor and the device run one native front end, the Editor calling it by P/Invoke from its managed backend, the C# copies retired, and a test asserts their samples identical (adr-0001); AGC2's classes are compiled from pinned `webrtc-audio-processing` 2.1 source through our CMake (adr-0003). The learned level survives stop/start within a session, cleared only by an explicit reset or re-init; `micGainTargetDb` keeps its name and its dBFS meaning, mapped to AGC2's headroom. With no pilot, the 16-fixture TTS joined-rig A/B is a no-regression gate and the two measured defects are verified directly (adr-0004). All numbers on this page are **provisional → the tuning pass** (the design intent is the load-bearing part).

**Scope, agreed at G0 (2026-11-05):** give the 48→16 kHz downsampler a proper anti-alias low-pass in place of today's, which adds +2.51 dB of DC gain (R14); replace both of today's diverging AGCs, `NativeBridge~/src/agc.h` (the Quest/Android path) and `Runtime/Dsp/Agc.cs` (the Editor's managed path), with WebRTC APM AGC2, whose built-in voice detector drives its gain adaptation and so is the "speech gate" R15 calls for (R15); bring in the WebRTC APM dependency as source into the `NativeBridge~` build, BSD-3 licensed, with its cost counted against the 70 MB APK ceiling (ruling 6, `05-rulings.md` :57), how it is vendored being decided in the design, not at G0; and measure R14 and R15 as one unit through the R1 joined rig, plus the R5 pilot if it exists, with acoustic baselines re-pinned by running the harness, never hand-edited, and any fixture that flips from pass to fail given a named explanation (ruling 10).

> **What's decided going in.** The human (the maintainer) ruled at G0 on 2026-11-05, choosing "Agree as drafted": the work runs on the **design track**, branch `design-agc-filter`, **R14 and R15 as one unit**, with the scope above agreed as drafted; R16, the voice detector as decoder gate and endpoint, stays out, a later topic. The two are one unit because "the filter removes +2.51 dB of DC gain the AGC currently absorbs; separate evaluation misattributes" (ruling 10, `05-rulings.md` :97); the joined rig counts them as one decoder-side unit (`Planning~/design-docs/joined-rig.md` :92). WebRTC APM is the dependency because ruling 5 put the WebRTC audio-processing module first and merged R15 and R16 into one dependency decision, with no ONNX Runtime this cycle unless its upgrade path triggers (`05-rulings.md` :49–51).
>
> **Non-goals:** R16 (the voice detector gating decoder audio or deciding end-of-utterance); Silero or ONNX Runtime; building the R5 or R4 corpora; tuning thresholds outside this unit; any parser change (see §0).

---

## 0. Scope (agreed at G0)

**In**

1. **R14 decimation filter:** a proper anti-alias low-pass in the 48→16 kHz downsampler; today's one adds +2.51 dB of DC gain.
2. **R15 AGC:** replace both of today's diverging AGCs with WebRTC APM AGC2: `NativeBridge~/src/agc.h` (the Quest/Android path) and `Runtime/Dsp/Agc.cs` (the Editor's managed path). AGC2's built-in voice detector drives its gain adaptation, so it is the "speech gate" R15 calls for.
3. **The WebRTC APM dependency:** brought in as source into the `NativeBridge~` build, BSD-3 licensed, with its cost counted against the 70 MB APK ceiling (ruling 6, `05-rulings.md` :57). How it is vendored is decided in the design, not at G0.
4. **The evaluation plan:** R14 and R15 are measured as one unit through the R1 joined rig, plus the R5 pilot if it exists. Acoustic baselines are re-pinned by running the harness, never hand-edited. Any fixture that flips from pass to fail needs a named explanation (ruling 10).

**Acceptance gate**

- R14 and R15 land as one evaluated unit through the joined rig, R1 (ruling 10, `05-rulings.md` :97).
- Acoustic baselines are regenerated by running the harness, never hand-edited (`05-rulings.md` :98).
- Every fixture whose outcome changes is listed with its old and new transcript; any pass→fail flip needs a named explanation before merge (`05-rulings.md` :98).
- The acceptance gate is R1 plus the pilot (`05-rulings.md` :102).

**Out**

- R16, using the voice detector to gate decoder audio or to decide end-of-utterance. It waits on Phase B probes 2 and 3 (not run; they need the human's Quest session), carries its own kill criterion, and touches the R11 endpointer: a later topic.
- Silero or ONNX Runtime.
- Building the R5 or R4 corpora.
- Tuning thresholds outside this unit.
- Any parser change.

**Prerequisites**

- R1, the joined rig: merged, PRs #177 (parser half) and #178 (recogniser half).
- R5, the human corpus, whose pilot is the human check: not started.

---

This system is the front end: the stage between the 48 kHz capture and the Vosk decoder that decimates to 16 kHz, normalises the speech level and hands the decoder bounded int16 samples. On the device it sits in the recognition loop between the ring buffer and `vosk_recognizer_accept_waveform_s` (`NativeBridge~/src/vosk_bridge.cpp:80`, `:128-138`); in the Windows Editor it sits in the managed backend's chunk path (`Runtime/EditorMicBackend.cs:386-430`); and it is measured as one unit on the joined rig, which runs the native pipeline only (`Planning~/design-docs/joined-rig.md` :88). It resolves four forks, recorded as adr-0001 through adr-0004: how the Editor runs the same front end as the device (adr-0001, the keystone), who decimates (adr-0002), how AGC2 is brought in (adr-0003), and what G2 rests on with no pilot (adr-0004). One constraint governs everything below: AGC2 is C++, and the Windows Editor never loads the bridge (`Documentation~/native-bridge.md` :61). The goal it carries is the accuracy axis of the next-level brief — "fewer substitutions/drops on the words that matter; real out-of-grammar/noise rejection; ... robustness to the Quest mic ...; fewer phantom or wrong commands" (`00-brief.md` :40) — through the plan to "fix the instrument and the two measured defects" (`04-synthesis.md` :28); R14 and R15 are tagged accuracy (`03-critique-and-ranking.md` :188-189). No numeric goal is stated. Below, `R4` is `Planning~/research/2026-09-05-next-level/research/R4-frontend-robustness-personalization.md`, `I1` is `…/ideas/I1-recognition-core-redesign.md`, and the numbered files are in `Planning~/research/2026-09-05-next-level/`.

## 1. The model

**The facts it rests on** (recon of 2026-11-05, `Planning~/design-docs/agc-filter/recon-2026-11-05.md`).

- Capture is 48 kHz mono float; 48000 is hard-coded in each backend, and the Editor mic, Editor playback and the harness take 48 kHz only (`Planning~/design-docs/agc-filter/recon-2026-11-05.md`, Code map).
- Today's downsampler, identical in C++ and C#, is a 15-tap Hamming-windowed sinc that decimates by 3 (`NativeBridge~/src/downsampler.h:7-15`; `Runtime/Dsp/Downsampler.cs:13-27`). Its taps sum to 1.3358, +2.51 dB of DC gain, which `Tests~/Editor/DownsamplerTests.cs:71-96` pins, its comment saying the AGC "compensates for this constant pre-gain". Measured, it is "a 3 kHz shelf": −6.1 dB at 4 kHz, −36.7 dB at 7.5 kHz, ~29 dB of alias rejection (`R4` :128-130; `03-critique-and-ranking.md` :62-67), while the model's `mfcc.conf` uses the band up to 7.6 kHz (`R4` :132-135).
- Today's AGC, in C++ and C#, works per sample: a smoothed level, a desired gain of target over level clamped to [0.5, 20], a smoothed gain and a tanh soft clip, with a −18 dBFS target by default and a noise floor of 1e-5 (`NativeBridge~/src/agc.h:17-32,47-80,93-98`; `Runtime/Dsp/Agc.cs:13-29,58-112`). The floor sits below any real mic floor, so the gain climbs toward 20× in pauses (10× at 193 ms, 19× at 883 ms) and a post-pause cough lands ~26 dB hotter than speech (`R4` :634-638; `03-critique-and-ranking.md` :83). The copies differ only in their gate (`agc.h:53`; `Agc.cs:66`): on digital silence the C# one freezes its gain at once, the C++ one keeps adapting for ~1.9 s toward 20× (`03-critique-and-ranking.md` :85).
- `micGainTargetDb` is the only AGC knob (`Runtime/VoxrSpeechRecogniser.cs:35-41`); it reaches `vosk_bridge_init` on the device (`NativeBridge~/src/vosk_bridge.h:33-34`) and the managed backend in the Editor, and the harness baseline pins it as `"gain": -18.0` (`NativeBridge~/harness/expectations.json:232`).
- The device resets the downsampler and the AGC at init and at every start (`NativeBridge~/src/vosk_bridge.cpp:260`, `:196`).
- AGC2's adaptive digital controller, read from upstream source: a target gain of −(speech level + headroom), clamped to [0, max gain], capped by the maximum output noise level minus the noise level, rate-limited per 10 ms frame; its voice detector gates gain increases only, after a run of consecutive confident frames, and decreases are ungated; a limiter always follows, ending in a hard clip (`Planning~/design-docs/agc-filter/recon-2026-11-05.md`, WebRTC APM research). Its upstream defaults are headroom 5 dB, max gain 50 dB, initial gain 15 dB, 6 dB/s and a −50 dBFS output noise ceiling (`Planning~/design-docs/agc-filter/recon-2026-11-05.md`, WebRTC APM research); none is a value this design has decided.

**The chain** (design intent, not yet built).

| Stage | What it does | Rate |
|---|---|---|
| Capture | Mono float, as today | 48 kHz |
| Decimator | A DC-normalised low-pass covering the model's feature band (`mfcc.conf` to 7.6 kHz), keeping every third sample (adr-0002) | 48 → 16 kHz |
| AGC2 adaptive digital | The gain of rules 3 and 4, applied in 10 ms frames | 16 kHz |
| Limiter | Bounds the output (rule 5) | 16 kHz |
| int16 | The bounded samples, converted for the decoder | 16 kHz |
| Vosk | The decoder, unchanged | 16 kHz |

**The state it tracks.**

| State | What it holds |
|---|---|
| Decimator history | The recent input samples the filter needs |
| Speech level | AGC2's estimate of the speech level |
| Noise level | AGC2's estimate of the noise level |
| Current gain | The gain applied now, in dB |
| Confident-speech run | How many consecutive frames AGC2's voice detector has judged confident speech |
| Limiter envelope | The limiter's running envelope |

**The rules** (design intent, not yet built).

| # | Rule | Settled | ADR |
|---|---|---|---|
| 1 | The chain | 48 kHz float capture → decimator → 16 kHz → AGC2 adaptive digital in 10 ms frames → limiter → int16 → Vosk. AGC2 sees 16 kHz in and out: no resampler, no band split inside APM. | adr-0002 |
| 2 | The decimator | A DC-normalised long FIR whose low-pass covers the model's feature band, keeping every third sample. Its taps, cutoff and window are provisional → the tuning pass. | adr-0002 |
| 3 | The target gain | Target gain = −(speech level + headroom), clamped to [0, max gain]; capped by the noise ceiling minus the noise level; reached at a limited rate in dB/s. Headroom, max gain, noise ceiling and rate limit are provisional → the tuning pass. | — |
| 4 | The voice gate | The gain increases only after a run of confident speech frames from AGC2's voice detector; it decreases ungated. | — |
| 5 | Bounded output | The limiter bounds the output so the int16 conversion cannot overflow — the property today's front end has, kept (`R4` :706-707). | — |
| 6 | What of APM runs | AGC2 adaptive digital, fixed gain 0 dB, and the limiter; the input-volume controller, noise suppression and echo cancellation off (`R4` :324-330). | — |
| 7 | One source | The Editor and the device run the same native front end: the Editor's managed backend calls it by P/Invoke, capture and the Vosk calls stay managed, and the C# `Agc.cs` and `Downsampler.cs` are retired. | adr-0001 |
| 8 | Parity | A test asserts that the Editor's and the native post-front-end samples are identical for the same input; none compares them today (`01-code-audit.md` :497). | adr-0001 |
| 9 | The dependency | The AGC2 subset — gain controller, RNN voice detector, limiter, `common_audio` — compiled from the freedesktop `webrtc-audio-processing` 2.1 source and its abseil, pinned by SHA-256, through our CMake, statically, with hidden symbols. The speech probability stays reachable for R16. | adr-0003 |
| 10 | The level across stop/start | The AGC keeps its learned level across stop/start within a session; only an explicit reset or a re-init clears it. Today the device resets at every start (`NativeBridge~/src/vosk_bridge.cpp:196`). The rig resets per fixture, so it measures the cold-start worst case. | — |
| 11 | The target knob | `micGainTargetDb` keeps its name and its dBFS meaning, "target speech level", mapped to AGC2's headroom. Its default is provisional → the tuning pass. No `[FormerlySerializedAs]` migration. | — |
| 12 | The kill criteria | On the joined rig at equal parser thresholds, the unit is killed if wrong-intent or false fires rise, or if first-word retention on silence-then-command fixtures falls — on every corpus; and if correct fires do not improve on the pilot (human speech) whenever it exists. Before the pilot exists, the TTS gate requires correct fires not to fall. Any pass→fail flip without a named explanation blocks merge (ruling 10). | adr-0004 |
| 13 | Acceptance with no pilot | With no pilot, the 16-fixture TTS joined-rig A/B is a no-regression gate — no unexplained flips, no rise in false or wrong-intent fires — not a ranking. The case rests on the two measured defects verified fixed directly: the filter's response measured, and synthetic silence-then-burst signals showing the gain does not rise in pauses. The pilot ranks the unit whenever it exists. | adr-0004 |

**One 10 ms frame, resolved** (design intent, not yet built).

1. The decimator filters the 48 kHz input and keeps every third sample, giving a 10 ms frame at 16 kHz (rules 1 and 2).
2. AGC2's voice detector judges the frame; a confident speech frame extends the confident-speech run (rule 4).
3. The speech-level and noise-level estimates update.
4. The target gain is computed: −(speech level + headroom), clamped to [0, max gain], capped by the noise ceiling minus the noise level (rule 3).
5. The current gain moves toward the target by at most the rate limit: upward only when the confident-speech run is long enough, downward always (rules 3 and 4).
6. The gain is applied, the limiter bounds the frame, and it is converted to int16 for Vosk (rules 5 and 6).

## 2. The decisions it puts to whoever drives it

The driver is the developer who integrates the package. The package itself decides nothing at runtime beyond the rules of §1.

1. **The target speech level, `micGainTargetDb`** (rule 11). It keeps today's name and its dBFS meaning, the level speech should reach, now mapped to AGC2's headroom, so a developer's existing setting keeps its sense and no serialised field migrates; a renamed field would need `[FormerlySerializedAs]`, which does not migrate prefab-instance overrides (`I1` :59). A higher target brings speech closer to the limiter's bound; a lower one hands the decoder quieter speech; neither dominates, and the decoder's thresholds were tuned to today's level (`R4` :164-167). Today's −18 dBFS on a mean-absolute level is aggressive enough that today's soft clip compresses every syllable (`R4` :647-653). The default is provisional → the tuning pass.
2. **When to reset** (rule 10). By default the learned level survives stop/start within a session, so a speaker's next command meets the gain their last one left. An explicit reset, or a re-init, clears it, and the next utterance starts cold, from the provisional initial gain (§3, failure (a)). The developer knows what the package does not: when the speaker or the room changes. Keeping the level spares the same speaker the cold start; a reset pays the cold start so one speaker's level does not carry to the next. Neither dominates.

## 3. Dynamics & failure modes

**Forward.** The gain rises only on confident speech and is capped by the noise ceiling, so in a pause it holds or falls: today's climb toward 20× in pauses, and the post-pause cough ~26 dB hotter than speech that it produces (`R4` :634-638; `03-critique-and-ranking.md` :83), cannot occur. The rate limit — 6 dB/s upstream, provisional — lets the gain follow a speaker's level across utterances, not across syllables: the unit is "a slow, speech-gated, utterance-level normaliser, not a per-sample compressor with a 10 ms attack" (`R4` :678-681). An onset meets the gain the last speech left, not a gain climbed in the pause, so there is no onset overshoot (today's, ~20–30 ms, is R4's hypothesis, `R4` :660-664). The decimator passes the band the model's features use: the 4–7.6 kHz band reaches the decoder, where today it arrives 6.1 dB down at 4 kHz and 36.7 dB down at 7.5 kHz (`R4` :128-130; `03-critique-and-ranking.md` :62-67). The filter's +2.51 dB of DC gain goes, and every score shifts with it (`R4` :164-167). The limiter keeps the int16 conversion from overflowing, as today.

**War-game: attacks on the rules, and what holds each.**

| Failure mode | What it looks like | Held by |
|---|---|---|
| (a) A quiet speaker, cold start | The first utterances arrive under-gained while the level is learned, since the gain rises only on confident speech and only at the rate limit. The rig, which resets per fixture, measures this worst case. | The provisional initial gain, and rule 10: the learned level survives stop/start, so the cold start is paid once, not on every command. |
| (b) The voice detector misses speech under game audio | No confident-speech run builds, so the gain does not rise. | The gain holds, floored by the initial gain (rules 3 and 4). |
| (c) The voice detector takes a cough for speech | The gain rises on a burst that is not speech. | The rate limit bounds the rise (rule 3). |
| (d) Digital-silence fixtures | The noise estimate is near zero, so the noise ceiling is loose. | Increases are still voice-gated, so the gain stays flat (rule 4). |
| (e) A loud or close speaker | Speech arrives above the target; the gain floors at 0 dB and cannot cut, so the limiter's curve does any compressing — today's per-syllable squashing could return on hot input. | The limiter's gain curve before its hard clip (rule 5); how hot Quest speech arrives is unmeasured. |
| Two front ends drift apart | The Editor validates a front end the device does not run, as today (`03-critique-and-ranking.md` :85; `I1` :393). | One native source and the parity test (rules 7 and 8, adr-0001). |

## 4. Boundaries & couplings

- **Owns:** decimation, the gain, and the bounded int16 output.
- **Couples to:** the joined rig, which measures the unit on the native pipeline only (`Planning~/design-docs/joined-rig.md` :88); the R11 endpointer values, which see a changed pause signal — their re-sweep is a later item, one knob at a time (`03-critique-and-ranking.md` :158).
- **Hands off:** R16, the voice detector as decoder gate and endpoint; the threshold re-sweep of `minScore` and `minConfidence` (`R4` :164-167); the product docs, after G2.
- **Does not decide:** the filter's taps and cutoff, the headroom, the max gain, the noise ceiling, the initial gain, the rate limit — all provisional → the tuning pass.

## 5. What it delivers

The decoder hears the band its features use, with no DC gain for the AGC to absorb, at a level normalised per utterance and held through pauses, bounded so it cannot overflow: the two measured defects fixed, the plan's "fix the instrument and the two measured defects" (`04-synthesis.md` :28) on the accuracy axis (`00-brief.md` :40). The Editor and the device run one front end, so a change validated in the Editor is validated on the AGC the device runs. Whether the filter fixes particular confusions, `cease fire→safe five` or `all→fall`, is a hypothesis (`R4` :139-140; `03-critique-and-ranking.md` :71); the pilot ranks the unit whenever it exists (adr-0004).

When it goes right, a command after a pause arrives at the level of the speech before it; a cough in a pause is not lifted above speech; the filter's measured response covers the model's band; the joined rig shows no unexplained flips and no rise in false or wrong-intent fires; and the Editor's samples match the device's.

## 6. Feature backlog — locked at G1 (2026-11-05)

One feature, because R14 and R15 land as one evaluated unit (ruling 10). It is **Full**: the design leaves the P/Invoke surface, the AGC2 subset's file list and CMake, the filter's implementation and the silence-then-command fixtures to the requirements and architecture docs, so the build writes its own.

| Feature | Scope | Depends on |
|---|---|---|
| `agc-filter` | The DC-normalised FIR decimator in place of the 15-tap table (rules 1–2, adr-0002); the AGC2 subset from pinned `webrtc-audio-processing` 2.1 source through our CMake (rules 6, 9, adr-0003); one native front end on device and in the Windows Editor by P/Invoke, the C# `Agc.cs` and `Downsampler.cs` retired, and the parity test (rules 7–8, adr-0001); the learned level kept across stop/start and `micGainTargetDb` mapped to AGC2's headroom (rules 10–11); the silence-then-command fixtures, the joined-rig A/B as a no-regression gate with the kill criteria, and the two measured defects verified directly (rules 12–13, adr-0004). Path: `development`. **Full.** | None. |

## 7. Status / openness

- **Status: locked at G1 2026-11-05.** The model was settled with the human (the maintainer) on 2026-11-05; its forks are recorded as four accepted ADRs, adr-0001 through adr-0004, and the defaults settled in the same session are rules 6, 8, 10, 11 and 12.
- **The goal:** the accuracy axis (`00-brief.md` :40), through "fix the instrument and the two measured defects" (`04-synthesis.md` :28); no numeric goal is stated.
- **All numbers provisional → the tuning pass:** the filter's taps, cutoff and window (the R4 candidate, 95-tap Kaiser at 7.8 kHz, β 8.5, `R4` :157-162, is a reference); the headroom and the default of `micGainTargetDb`; the max gain; the noise ceiling; the initial gain; the rate limit.
- **Hard dependencies / open:**
  - RESOLVED: how the Editor runs AGC2 (adr-0001); who decimates (adr-0002); how the APM dependency is vendored, left to the design at G0 (adr-0003); what G2 rests on with no pilot (adr-0004); how rule 12's "correct fires improve" reads when only TTS exists — ruled by the human on 2026-11-05: improvement is read on the pilot whenever it exists, and before it exists the TTS gate requires correct fires not to fall (rule 12, adr-0004).
  - The NDK and MSVC builds of the AGC2 subset are unverified, and the arm64 `.so` growth is unmeasured (estimated ~1–2 MB, `R4` :321-322; ceiling 70 MB, today ~49 MB, `05-rulings.md` :55, :57) — the first architecture phase proves both; the abseil and source download is a human step (adr-0001, adr-0003).
  - The silence-then-command fixtures the kill criteria read do not exist; today's fixtures carry 0.3 s of lead silence (`Tests~/Fixtures/generate.py:28-29`) — the requirements name them (rule 12, adr-0004).
  - AGC2's internal constants — the voice detector's threshold, the estimators' time constants — are unread (`Planning~/design-docs/agc-filter/recon-2026-11-05.md`, WebRTC APM research) — the architecture doc reads them when the AGC2 subset is cut (adr-0003).
  - Quest speech level at the mic is unmeasured, so whether the 0 dB gain floor leaves the limiter compressing (failure (e)) is open; the tuning pass reads it.
  - The non-Windows Editor has no backend today; it is out of scope, not an open item — this unit leaves it unchanged.
  - The locked joined-rig docs cite `Documentation~/native-bridge.md` :61 for the AGC divergence, which that line no longer states (`Planning~/design-docs/agc-filter/recon-2026-11-05.md`, Doc facts) — noted, not amended here; deferred as `design-relock` entry AGF-1 in `memory/STATUS.md`, re-pointed at the next joined-rig unlock.

## 8. Related

`Planning~/design-docs/agc-filter/decisions/adr-0001-editor-native-dsp.md` · `…/adr-0002-own-fir-decimator.md` · `…/adr-0003-agc2-classes-pinned-source.md` · `…/adr-0004-tts-no-regression-acceptance.md` · `Planning~/design-docs/joined-rig.md` (`:77`, `:88`, `:111`, `:118`; rule 10) · `Planning~/design-docs/endpointer-profile.md` (R11) · `Planning~/research/2026-09-05-next-level/00-brief.md` (`:40`) · `…/01-code-audit.md` (`:497`) · `…/03-critique-and-ranking.md` (`:62-67`, `:71`, `:83`, `:85`, `:158`, `:188-189`) · `…/04-synthesis.md` (`:28`) · `…/05-rulings.md` (rulings 5, 6, 10; `:9`, `:55`, `:57`, `:97`, `:98`, `:100`, `:102`) · `…/research/R4-frontend-robustness-personalization.md` (`:128-130`, `:132-135`, `:139-140`, `:157-162`, `:164-167`, `:321-322`, `:324-330`, `:634-638`, `:647-653`, `:660-664`, `:678-681`, `:706-707`) · `…/ideas/I1-recognition-core-redesign.md` (`:59`, `:393`) · `Planning~/design-docs/agc-filter/recon-2026-11-05.md` · `NativeBridge~/src/vosk_bridge.cpp` (`:80`, `:196`) · `NativeBridge~/src/agc.h` · `NativeBridge~/src/downsampler.h` · `Runtime/EditorMicBackend.cs` (`:386-430`) · `Runtime/Dsp/Agc.cs` · `Runtime/Dsp/Downsampler.cs` · `Documentation~/native-bridge.md` (`:61`) · `Tests~/Fixtures/generate.py` (`:28-29`)
