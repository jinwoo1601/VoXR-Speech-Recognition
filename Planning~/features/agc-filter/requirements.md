---
type: requirements
feature: agc-filter
topic: agc-filter
status: draft
updated: 2026-11-06
sources:
  - Planning~/design-docs/agc-filter.md
  - Planning~/design-docs/agc-filter/decisions/adr-0001-editor-native-dsp.md
  - Planning~/design-docs/agc-filter/decisions/adr-0002-own-fir-decimator.md
  - Planning~/design-docs/agc-filter/decisions/adr-0003-agc2-classes-pinned-source.md
  - Planning~/design-docs/agc-filter/decisions/adr-0004-tts-no-regression-acceptance.md
---

# feat-agc-filter — requirements

Track **Full**. Design `agc-filter` (`Planning~/design-docs/agc-filter.md`), locked at G1 2026-11-05, merged as PR #190. Depends on no feature. Path `development`.

## 1. User story

- **S1** — As a developer shipping VoXR voice commands on Quest and testing them in the Windows Editor, I want one native front end (a DC-normalised FIR decimator feeding WebRTC AGC2) run the same way on device and in the Editor, so that commands spoken after silence are recognised without the front end's gain errors, and what I test in the Editor is what ships.

## 2. Feature & scope

Backlog row (`agc-filter.md` §6):

> `agc-filter` | The DC-normalised FIR decimator in place of the 15-tap table (rules 1–2, adr-0002); the AGC2 subset from pinned `webrtc-audio-processing` 2.1 source through our CMake (rules 6, 9, adr-0003); one native front end on device and in the Windows Editor by P/Invoke, the C# `Agc.cs` and `Downsampler.cs` retired, and the parity test (rules 7–8, adr-0001); the learned level kept across stop/start and `micGainTargetDb` mapped to AGC2's headroom (rules 10–11); the silence-then-command fixtures, the joined-rig A/B as a no-regression gate with the kill criteria, and the two measured defects verified directly (rules 12–13, adr-0004). Path: `development`. **Full.** | None.

**Agreed at G0 on 2026-11-05** by the maintainer, in session, as drafted; concurrent with `light-batch-b` (independent).

**In**

1. The DC-normalised FIR decimator in place of the 15-tap table (rules 1–2, adr-0002).
2. The AGC2 subset from pinned `webrtc-audio-processing` 2.1 source through our CMake (rules 6, 9, adr-0003).
3. One native front end on device and in the Windows Editor by P/Invoke, the C# `Agc.cs` and `Downsampler.cs` retired, and the parity test (rules 7–8, adr-0001).
4. The learned level kept across stop/start and `micGainTargetDb` mapped to AGC2's headroom (rules 10–11).
5. The silence-then-command fixtures, the joined-rig A/B as a no-regression gate with the kill criteria, and the two measured defects verified directly (rules 12–13, adr-0004).

**Out**

1. R16, using the voice detector to gate decoder audio or to decide end-of-utterance. It waits on Phase B probes 2 and 3 (not run; they need the human's Quest session), carries its own kill criterion, and touches the R11 endpointer: a later topic.
2. Silero or ONNX Runtime.
3. Building the R5 or R4 corpora.
4. Tuning thresholds outside this unit.
5. Any parser change.
6. The non-Windows Editor, which has no backend today and stays unchanged (design §7).

## 3. Why — the value it delivers

This carries the design's goal: the accuracy axis (`00-brief.md` :40), through "fix the instrument and the two measured defects" (`04-synthesis.md` :28) (design §5). What users get: the decoder hears the 4–7.6 kHz band its features use, where today it arrives 6.1 dB down at 4 kHz and 36.7 dB down at 7.5 kHz, and the +2.51 dB DC gain is gone (adr-0002); a command after a pause no longer meets a gain climbed in the pause, and a cough in a pause is not lifted ~26 dB above speech (design §3); an AGC change validated in the Editor is validated on the AGC the Quest runs (adr-0001). No numeric accuracy goal is stated; the pilot ranks the unit whenever it exists (adr-0004).

## 4. Observable behaviour

1. After the upgrade a developer's `micGainTargetDb` keeps its meaning — the target speech level in dBFS — and no serialised field migrates.
2. In the Windows Editor, entering Play loads the native front-end DLL beside `libvosk.dll`; if it is missing or fails to load, an error naming it is raised through the recogniser's existing error path — never a silent fallback, the C# path being gone.
3. A command spoken after a pause arrives at about the level of the speech before the pause; a cough or burst in the pause is not lifted above speech.
4. Stop then start keeps the learned level; `ResetRecogniser()` (the existing public reset) or a re-init returns the front end to its cold start.
5. The Quest runs the same front end as the Editor.

## 5. Functional requirements

**Ruled on 2026-11-05** by the maintainer, in session: fork 1, the filter's G2 bar, numeric (F1); fork 2, starting values with the tuning pass a later item (F17, §7); fork 3, 16 silence-then-command fixtures at a 2 s pause, with first-word retention defined (F12, F13); fork 4, a Quest 3 check is a Must at G2 (F18); the rest settled as drafted.

**Amended on 2026-11-06** by the maintainer at the architecture-doc PLAN stop: F12 is 15 fixtures, one per command case of the 16 demo fixtures (the manifest's non-`ladder` cases: 14 single-phrase commands, a split case and a silence case); the `ladder` cases are out of F12's scope; F13's first word for the filler case is the first word after its filler.

| #  | Requirement | Story | Priority | Acceptance check | Source |
|----|-------------|-------|----------|------------------|--------|
| F1 | 48→16 kHz decimation by a DC-normalised low-pass covering the model's feature band (0–7.6 kHz). | S1 | Must | Measured response: DC gain 0 dB ±0.1 dB; at most 1 dB down anywhere in 0–7.6 kHz; at least 50 dB rejection from 8.4 kHz up. | rules 1–2; adr-0002; fork 1 |
| F2 | The 15-tap downsampler is removed from both the C++ and the C# code. | S1 | Must | The old table appears nowhere in source. | rule 2; adr-0002 |
| F3 | The AGC2 subset (gain controller, RNN voice detector, limiter, `common_audio`) is compiled from `webrtc-audio-processing` 2.1 source and its abseil, pinned by SHA-256, through our CMake, statically, with hidden symbols, for Android arm64-v8a (NDK) and Windows x64 (MSVC); the voice detector's speech probability is not cut out of the subset (kept reachable for R16). | S1 | Must | Both builds green; a fetch with a wrong SHA-256 refuses; the exported-symbol list holds only the bridge's own exports. | rule 9; adr-0003 |
| F4 | Of APM only AGC2 adaptive digital and the limiter run, fixed gain 0 dB; the input-volume controller, noise suppression and echo cancellation are off. | S1 | Must | A test or the configuration read shows exactly this. | rule 6 |
| F5 | The output is bounded: full-scale or clipping input never overflows the int16 conversion. | S1 | Must | A full-scale and an over-range input give int16 output with no wrap. | rule 5 |
| F6 | The gain does not rise in pauses. | S1 | Must | Synthetic silence-then-burst and noise-then-burst signals: the gain at the burst is no higher than the gain before the pause; a burst after a pause is not lifted above the preceding speech's level; noise-only input does not raise the gain. | rules 3–4; rule 13; adr-0004 |
| F7 | The Editor's managed backend calls the native front end by P/Invoke; capture and the Vosk calls stay managed; `Runtime/Dsp/Agc.cs` and `Runtime/Dsp/Downsampler.cs` are deleted and their tests (`Tests~/Editor/DownsamplerTests.cs`, `Tests~/Editor/AgcTests.cs`) removed or replaced. | S1 | Must | The files are gone; the Editor test suites are green. | rule 7; adr-0001 |
| F8 | The Windows x64 front-end DLL ships under `Runtime/Plugins/x86_64`, imported for the Windows Editor only. | S1 | Must | The plugin import settings; the Editor loads it. | adr-0001 |
| F9 | Parity: the Editor's and the native post-front-end int16 samples are bit-identical for the same input, across the fixture corpus, including chunk lengths that are not multiples of 3 or of a 10 ms frame. | S1 | Must | A parity test, green. | rule 8; adr-0001 |
| F10 | The learned level survives stop/start within a session; `ResetRecogniser()` and a re-init clear it. | S1 | Must | Tests: the gain after start equals the gain at stop; after a reset or re-init it is the initial gain. | rule 10 |
| F11 | `micGainTargetDb` keeps its name and dBFS meaning, mapped to AGC2's headroom; no `[FormerlySerializedAs]`. | S1 | Must | The serialised field is unchanged; on steady speech a target 6 dB higher gives output about 6 dB louder. | rule 11 |
| F12 | Silence-then-command fixtures: each of the 15 command cases among the 16 demo fixtures (the manifest's cases outside the `ladder` category: 14 single-phrase commands and the split case; the silence case has no command) preceded by a 2 s pause at a −60 dBFS noise floor (15 new fixtures), generated by `Tests~/Fixtures/generate.py` and pinned by a harness run. | S1 | Must | The fixtures are in the manifest and pinned by a harness run, never by hand. | rule 12; adr-0004; fork 3 |
| F13 | The joined-rig A/B, main's front end against the new one at equal parser thresholds, on the 16 demo fixtures (the manifest's non-`ladder` cases) plus F12's: the unit is killed by a rise in wrong-intent or false fires, a fall in first-word retention (the share of F12 fixtures whose final transcript starts with the command's first word; for the filler case, the first word after its filler, `launch`), or a fall in correct fires; every fixture whose outcome changes is listed with its old and new transcript, and any pass→fail flip without a named explanation blocks merge. | S1 | Must | The rig report, recorded in the architecture doc. | rules 12–13; adr-0004; ruling 10 |
| F14 | `NativeBridge~/harness/expectations.json` is re-pinned only by a harness run. | S1 | Must | The pin's diff comes from a harness run. | design §0 acceptance gate; adr-0004 |
| F15 | A CHANGELOG line records the behaviour change: scores shift with the new filter and gain, and the Editor now loads a native DLL. | S1 | Must | The CHANGELOG entry. | adr-0001; adr-0002 |
| F16 | `THIRD_PARTY_NOTICES.md` names WebRTC (BSD-3 with its PATENTS grant), the rnnoise weights (BSD-3) and abseil (Apache-2.0). | S1 | Must | The notices entries. | adr-0003 |
| F17 | The front end ships starting values: the R4 candidate filter (95-tap Kaiser, cutoff 7.8 kHz, β 8.5) or one clearing F1's bar; AGC2's upstream defaults for max gain (50 dB), initial gain (15 dB), rate limit (6 dB/s) and output noise ceiling (−50 dBFS); the `micGainTargetDb` default stays −18 dBFS. Tuning these is a later item. | S1 | Must | The values in source match this row or are recorded as deltas in the architecture doc. | design §7; fork 2 |
| F18 | Human check on a Quest 3: the APK builds and installs, the bridge loads, a command spoken after a ~3 s pause fires, and the APK size is recorded under 70 MB. | S1 | Must | By hand at G2. | adr-0003; fork 4 |

## 6. Non-functional requirements

- **Size:** the arm64 `.so` growth and the Windows DLL size are measured and recorded; the APK stays under the 70 MB ceiling (today ~49 MB; `05-rulings.md` :55, :57).
- **Performance:** the front end's per-frame cost is measured on the harness and recorded; the latency it adds (filter delay plus frame buffering) is recorded and at most 15 ms.
- **Robustness at the input boundary:** any chunk length is handled, with no sample dropped or duplicated (F9 checks it).
- **Degraded dependencies:** a front-end DLL that fails to load in the Editor raises the §4.2 error, never a silent bypass.
- **Platform:** the non-Windows Editor, which has no backend today, is unchanged.

## 7. Non-goals / deferred

- The G0 Out list (§2).
- The tuning pass — the filter's taps, cutoff and window, the headroom default, max gain, initial gain, rate limit and noise ceiling (fork 2).
- The threshold re-sweep of `minScore`, `minConfidence` and the leading-miss bar (design §4).
- The R11 endpointer re-sweep (design §4).
- Ranking the unit, which the pilot does whenever it exists (adr-0004).
- The product docs, after G2.

## 8. Dependencies & assumptions

- No feature dependency; the joined rig (#177, #178) and R11 (#185) merged.
- The source and abseil download is a human step (adr-0003).
- The NDK and MSVC toolchains (the `windows-x64` harness preset) and the piper generator (`Tests~/Fixtures/generate.py`, its venv by `generate.sh`).
- A Quest 3 for F18.
- Assumed: AGC2's internal classes are stable at the pinned SHA-256 (adr-0003), and AGC2's speech level estimate behaves as a dBFS speech level so the F11 mapping holds.

## 9. Acceptance criteria (G2)

- [ ] Every Must in §5 (F1–F18) checked, with run evidence.
- [ ] The F13 rig report, the F1 filter-response measurement and the F6 silence-then-burst evidence recorded in the architecture doc.
- [ ] Human-only verification, on a Quest 3: F18.
- [ ] The commands the project's `verification` binding names are green.

## 10. Open questions

1. The tuning pass: its values and where it runs; Quest speech level at the mic is unmeasured, so whether the 0 dB gain floor leaves the limiter compressing (design §3 failure (e)) is open — a later item.
2. The P/Invoke surface and how the Windows library is packaged — the architecture doc (adr-0001).
3. The AGC2 subset's exact file list and its CMake; whether the NDK and MSVC builds succeed and the arm64 `.so` growth — the architecture doc's first phase (adr-0003, design §7).
4. AGC2's internal constants (the voice detector's threshold, the estimators' time constants) — the architecture doc, when the subset is cut (adr-0003).
5. How the filter is implemented — the architecture doc (adr-0002).
6. How F6's synthetic signals and F1's response measurement are run (harness, test or tool) — the architecture doc.
