# R4 — Audio front-end robustness, headset acoustics, adaptation/personalization, augmentation

Research report for the 2026-09-05 "next level" fan-out. Scope: everything between the microphone and
`vosk_recognizer_accept_waveform_s` (brief §2 steps 1–3), plus speaker/domain adaptation of the decoder and the
data used to measure it. Prepared 2026-09-05.

Verification convention: every citation below was fetched (abstract page, repo page, or spec page) unless tagged
`UNVERIFIED`. Numbers I computed myself from the repository source are tagged `MEASURED (this report)` and the
method is given so they can be re-derived.

---

## Executive summary

- **The brief's premise about the downsampler is wrong, and the truth is worse.** `downsampler.h` is *not* plain
  decimation-by-3 — it has a 15-tap Hamming windowed-sinc anti-alias filter. But that filter is badly
  specified: **DC gain 1.3358 (+2.51 dB, un-normalised)**, **−3.3 dB at 3 kHz, −6.1 dB at 4 kHz, −16.0 dB at
  6 kHz, −25.9 dB at 7 kHz**, worst alias rejection ≈29 dB. The model's own `conf/mfcc.conf` sets
  `--high-freq=7600`, so the top third of its 40 mel bins are being fed 6–26 dB of unintended attenuation. This
  is a permanent, every-utterance train/test mismatch concentrated exactly on the band that separates /s/, /f/,
  /ʃ/ and /θ/ — i.e. on `cease fire → safe five` and `all → fall`. A 95-tap Kaiser replacement gives ≤0.36 dB
  droop to 7 kHz and ≥54 dB alias rejection for **1.52 MMAC/s** (~6× the current 0.24 MMAC/s; still <0.2 % of
  one XR2 core). This is the single cheapest, highest-expected-value change in this whole report.
- **The AGC has no voice-activity gate, and its release constant guarantees it amplifies room noise by up to
  +26 dB inside every pause.** `kNoiseFloor = 1e-5` (−100 dBFS) is far below any real mic noise floor, so during
  silence `desired_gain` saturates at `kMaxGain = 20` and `current_gain_` reaches ~10× within ~190 ms and ~19×
  within ~0.9 s. That is a mechanically sufficient explanation for the documented "cough, hum and breathing
  decode as short in-grammar words" failure. Every production AGC of this class (WebRTC AGC2) is VAD-driven for
  exactly this reason.
- **The AGC target is also mis-scaled.** −18 dBFS is applied to a *smoothed mean-absolute* estimator, not RMS or
  peak. Speech peak ≈ mean-abs + 14–20 dB, so post-gain peaks land at roughly −4 to +2 dBFS and the `fast_tanh`
  soft limiter compresses on essentially every syllable. Combined with the 10 ms attack after a silence at 20×
  gain, the **first 20–30 ms of every utterance is gain-transient-distorted** — the same 1–2 frames whose loss
  produced the leading-required-miss bar shipped in 2.0.0. Hypothesis, not measured; a cheap A/B exists.
- **AGC is nonetheless justified here, for a reason the code does not state.** Kaldi's nnet3 online pipeline
  feeds the network *un-adapted, non-mean-normalised* MFCCs plus i-vectors (kaldi-asr.org online decoding doc).
  A gain change is a pure additive offset on c0 only, so the net sees an out-of-training-range c0 for quiet
  input. The int16 quantisation argument is much weaker than it looks: at the documented 0.04 peak (−28 dBFS)
  the residual quantisation SNR is still ≈68 dB, at or below typical MEMS-mic self-noise.
- **Do not put a neural denoiser in front of VOSK as a first move.** The strongest 2024–2026 evidence is
  bimodal: enhancement in front of an ASR that never saw enhanced audio **degraded WER in 40/40 tested
  configurations** (Chondhekar et al. 2025, +1.1 % to +46.6 % absolute semWER), while purpose-built decoupled
  front-ends do help (Yang/Pandey/Wang 2024; Du et al. 2018: +11.78 % relative WERR for a DNN back-end on
  CHiME-3). VoXR's operating point — mostly clean close-talk speech, a 40 MB model, no ability to retrain — sits
  on the "degrades" side of that line.
- **A neural VAD gate is the better use of the same CPU budget.** Silero VAD v5 is MIT, ~2 MB, and measured at
  **0.8 ms per 32 ms frame on Pixel 7 arm64 with 2 threads (0.3 ms with NNAPI)** in a published Android
  write-up that pairs it with *Vosk grammar mode* for exactly this problem, reaching 11 % FP / 4 % FN in a noisy
  facility simulation. It attacks the "grammar mode has no silence output" failure at the source instead of
  trying to make the decoder smarter.
- **Echo/game-audio bleed is a real, unmeasured hazard with a cheap platform answer and an expensive DSP one.**
  Quest speakers sit centimetres from the mics; VoXR captures `VOICE_RECOGNITION` (source 6) with no
  `AcousticEchoCanceler` attached and no far-end reference plumbed. AOSP is explicit that default preprocessing
  is *device-specific* (`/vendor/etc/audio_effects.xml`), so what Horizon OS actually applies to source 6 is
  **unknown and adb-checkable in one device session**. The full WebRTC APM (AEC3+NS+AGC2) port costs 4.2 µs per
  10 ms frame at 16 kHz on an M4 Max with NEON — negligible even after a 30× derating — but AEC3 needs the
  game's output mix as a reference signal, which Unity can supply via `OnAudioFilterRead` on the AudioListener.
- **Personalization has one nearly-free win already in the box, and VoXR currently throws it away.** The shipped
  model has an `ivector/` directory (`final.ie`, `final.dubm`, `final.mat`, `global_cmvn.stats`), so VOSK runs
  online i-vector speaker adaptation. That state is per-recognizer and accumulates left-to-right across
  utterances — but `vosk-api`'s `SetGrm()` reinitialises the decoder *and the feature pipeline*, and VoXR frees
  and recreates the recognizer on every command-set / dynamic-slot change. **Every grammar rebuild resets
  speaker adaptation to the prior, not just the ~50 ms audio gap.**
- **Test-time adaptation is off the table on this decoder; augmentation of the measurement corpus is not.**
  SUTA-family TTA (Interspeech 2022 → Interspeech 2025) needs gradients through the acoustic model at test time;
  Kaldi nnet3 on Quest has no such path. Conversely, playback-interference augmentation is cheap and directly
  targeted: Raju et al. (2018) report **30–45 % relative false-reject reduction under device audio playback**
  purely from mixing playback content into training data — and VoXR can apply the same idea to its *fixture
  corpus* today to find out whether game audio is actually hurting it.
- **What none of this fixes:** the bag-of-phrases grammar's word-order freedom, uncalibrated per-word
  confidence, OOV abbreviations, the grammar-rebuild gap, the single-recognizer-per-process limit, and the
  ~9.85 % LibriSpeech-test-clean floor of a 40 MB model. Front-end work moves the input distribution closer to
  what the model was trained on; it cannot create posterior mass the model never had.

---

## Findings table

| Name | Key citation | What it does | Reported effect (numbers, dataset) | CPU/mem on ARM | License | Stage (§2) | Expected gain for VoXR | Main risk |
|---|---|---|---|---|---|---|---|---|
| **Proper 48→16 k anti-alias decimator** | Own design + Pohlhausen & Bitzer, arXiv:2508.02483 (2025); model `conf/mfcc.conf` | Replaces the 15-tap filter with a ~95-tap Kaiser windowed-sinc, DC-normalised | 95-tap: ≤0.36 dB droop 0–7 kHz, ≥53.9 dB alias rejection vs current −25.9 dB @7 kHz / 29 dB rejection / +2.51 dB DC error (MEASURED, this report) | 1.52 MMAC/s (vs 0.24 now); ~400 B state | n/a (own code) | 2 | **High** — restores mel bins 25–40 to trained levels; direct at /s/–/f/ confusions | Changes every score in the corpus; all thresholds must be re-validated |
| **VAD-gated AGC (freeze gain in silence)** | WebRTC AGC2 design (webrtc.googlesource.com APM g3doc; Sonora port) | Only lets gain rise while speech is present; holds through pauses | AGC2 ships an RNN-VAD-driven adaptive digital controller + limiter as standard practice | negligible (state machine) or 4.2 µs/10 ms frame for the whole APM @16 k on M4 Max (NEON) | BSD-3 (WebRTC/Sonora) | 2 | **High** — removes the +26 dB noise boost that feeds cough/hum false triggers | Gate misfires clip genuine quiet onsets |
| **Silero VAD v5 gate before the recognizer** | snakers4/silero-vad; "On-Device Wake Word Detection with Vosk Grammar Mode and Silero VAD v5" (zenn.dev/diced) | Neural VAD gates audio into VOSK; supplies the "silence" class grammar mode lacks | 0.8 ms/32 ms frame, 2 threads, Pixel 7 arm64; 0.3 ms with NNAPI. Tuned gate (thr 0.65, 160 ms min speech) → 11 % FP / 4 % FN in facility-noise sim | ~2 MB model; RTF ≈0.025 | MIT | 1→2 | **High** for false triggers; some latency cost | Onset clipping; extra 2 MB APK; ONNX runtime dependency |
| **WebRTC APM: AEC3 + NS + AGC2** | webrtc APM g3doc; dignifiedquire/sonora (Rust port of WebRTC M145) | Echo cancellation against a far-end reference, Wiener NS, VAD-driven AGC | 4.2 µs per 10 ms frame @16 kHz mono, 13.3 µs @48 kHz, Apple M4 Max NEON, all three enabled | Trivially affordable even at 30× derating; ~1–2 MB code | BSD-3 | 1–2 | **Medium-high** for game-audio bleed; **AGC2 alone is a high-value drop-in** | AEC3 needs a sample-aligned far-end reference; NS is enhancement (see risk row below) |
| **Platform audio source / effects survey on Quest** | AOSP "Configure preprocessing effects"; `MediaRecorder.AudioSource`; google/oboe#951 | Determines what Horizon OS already does to source 6 vs `VOICE_COMMUNICATION`/`UNPROCESSED` | AOSP: defaults are per-device in `/vendor/etc/audio_effects.xml`; explicitly warns NS must **not** default-on for `VOICE_RECOGNITION` | zero | n/a | 1 | **Unknown but cheap to learn** — could reveal free AEC/NS | Effects may silently no-op (oboe#951 shows AEC/NS failing to engage with AAudio) |
| **GTCRN (ultra-light SE)** | Rong, Sun, Zhang, Hu, Zhu, Lu — ICASSP 2024 | Grouped TCRN speech enhancement | 48.2 K params, 33.0 MMAC/s; PESQ 2.87 / STOI 0.940 VCTK-DEMAND; DNSMOS-P.808 2.70 DNS3; RTF 0.07 on i5-12400 | 33 MMAC/s ≈ 2 % of a core est.; ~200 KB | MIT | 2 (new stage) | **Low / possibly negative** as a blind front end | Enhancement artefacts on a model never trained on enhanced audio |
| **DeepFilterNet 2/3** | Schröter et al., IWAENC 2022 (arXiv:2205.05474); Interspeech 2023 demo (arXiv:2305.08227) | Two-stage ERB-gain + deep-filtering SE, 48 kHz full-band | RTF 0.04 (DFN2) / 0.19 (DFN3) on single-thread notebook Core-i5 | Heavier than GTCRN; Rust `libDF` real-time binary exists | Code MIT/Apache-2.0 (repo); paper CC BY-SA | 2 | **Low** for the same reason | Same; plus 48 kHz operation before decimation |
| **RNNoise / DTLN (classic light SE)** | Valin, MMSP 2018 (arXiv:1709.08243) + jmvalin.ca; Westhausen & Meyer, Interspeech 2020 | Hybrid DSP/GRU band gains; dual-signal LSTM | RNNoise: 85 kB weights, 60× real time on x86, 7× on RPi3. DTLN: <1 M params, 0.27 ms/frame i5-6600K, 2.2 ms/frame RPi 3B+ (TFLite quant), 16 kHz | Both comfortably real-time on XR2 | BSD (RNNoise), MIT (DTLN) | 2 | **Low**; useful only as an A/B control | Over-suppression of low-energy fricatives — precisely VoXR's weak band |
| **Decoupled / ASR-aware enhancement** | Yang, Pandey, Wang, arXiv:2403.06387 (2024); Du, Zhang, Han, arXiv:1810.09067 (2018) | Enhancement trained so its output stays in the ASR's input distribution | CHiME-2 5.57 % WER (−28.4 % rel vs prior best); CHiME-4 1-ch 3.32/4.44 % sim/real. Du: −36.40 % rel WER (GMM), −11.78 % rel (DNN) on CHiME-3 | Research-scale models, not XR2-sized | research code | 2 | **Not adoptable as-is**; it is the *evidence* that naive SE is the wrong shape | Requires retraining/matched back-end VoXR cannot do |
| **Evidence: SE degrades an unmatched ASR** | Chondhekar et al., arXiv:2512.17562 (Dec 2025) | Systematic SE-before-ASR study | Noisy audio beat enhanced audio in **40/40** configurations (4 ASR × 10 noise conditions); +1.1 % to +46.6 % absolute semWER from enhancement | n/a | n/a | 2 | Negative result that should gate any SE work | Medical-domain, modern E2E back-ends; a Kaldi hybrid may behave differently |
| **VOSK online i-vector adaptation (already present)** | Kaldi online-decoding doc; `vosk-api/src/recognizer.cc`; model `ivector/` dir | 100-dim online i-vector accumulates left-to-right, incl. speaker history | i-vector SAT canonical gain ≈10 % rel WERR (Switchboard 300 h) — `UNVERIFIED` (Saon et al., ASRU 2013) | already paid (8.3 MB `final.ie` in the 40 MB model) | Apache-2.0 (model) | 3 | **Medium** — stop destroying it on grammar rebuild | Adaptation to *wrong* speaker/channel is also possible; CleanUp fires every ~10 min anyway |
| **Speaker codes + entropy-minimisation adaptation** | van Dalen, Zhang, Parcollet, Bhattacharya — Interspeech 2025 (arXiv:2506.10653) | Unsupervised per-speaker adaptation from unlabelled audio | **−20 % rel WER from 1 minute**, −29 % from 10 minutes, far-field noise-augmented Common Voice | training-capable back-end required | research | 3 | **High if reachable** — but not reachable on nnet3/Quest | Needs backprop at run time; VOSK exposes none |
| **Few-shot on-device KWS personalization** | Rusci & Tuytelaars, Interspeech 2023 (arXiv:2306.02161); Cioflan, Cavigelli, Benini, tinyML 2024 (arXiv:2403.07802) | Prototype/embedding personalization from a handful of user utterances | 76 % acc @5 % FAR, 10-shot, 10 GSC classes. Cioflan: error 30.1 %→24.3 % (−19 % rel), 23.7 kparams + 1 MFLOP/epoch on-device | MCU-class | research | 6/7 (verifier layer) | **Medium** as a *rejection* verifier over VOSK output | A second model to ship, train, and keep in sync with authored command sets |
| **TTS / VC data augmentation** | Ogun, Colotte, Vincent, arXiv:2503.08954 (2025); Quintas, Ferrané, Pellegrini, arXiv:2409.12745 (2024) | Synthesise command speech to adapt/finetune | −11 % rel WER Common Voice, up to −35 % rel LibriSpeech (Conformer-T) from multi-attribute joint augmentation; **pitch aug and VC speaker aug ineffective**. Quintas: ASR-based filtering of synthetic data materially improves downstream commands; synthetic vs real still separable in SSL space | offline | research/CC | 3 (offline) + 8 | **Medium** for corpus quality; **low** for direct model gain without finetuning | Overfitting to TTS voices — VoXR's *entire* fixture corpus is already TTS |
| **Playback-interference augmentation** | Raju, Panchapagesan, Liu, Mandal, Strom, arXiv:1808.00563 (2018) | Mix device self-playback (music/TV) into training/eval at varied SIR | **30–45 % relative false-reject reduction** under device playback | offline | research | 8 (then 2/3) | **High as a measurement tool** — tells you whether game audio is actually the problem before you build AEC | Simulated bleed ≠ Quest's real speaker→mic path |
| **Lombard / exertion robustness** | Ma, Petridis, Pantic — Interspeech 2019 (arXiv:1906.02112) | Models Lombard speech explicitly | "Properly modelling Lombard speech is always beneficial"; testing on plain-noisy speech **overestimates** audio-only performance | offline | research | 8 | **Medium** — VR players are exerting and talking over game audio; the TTS corpus contains zero Lombard speech | Cannot be synthesised convincingly by TTS; needs human recordings |
| **MUSAN + RIR + SpecAugment** | Snyder, Chen, Povey, arXiv:1510.08484; Park et al., Interspeech 2019 (arXiv:1904.08779) | Standard noise / reverb / feature-masking augmentation | SpecAugment: 6.8 %→5.8 % WER LibriSpeech test-other w/ LM; 7.2 %/14.6 % SWBD/CH | offline | CC (MUSAN) | 8 (+3 if finetuning) | **Medium** for corpus realism | Only pays off if you actually finetune the acoustic model (~1 h data per VOSK docs) |
| **libsoxr / SpeexDSP resamplers** | soxr (SourceForge); lastique src_test comparison; xiph/speexdsp | Off-the-shelf high-quality SRC | Soxr HQ/VHQ conversion times competitive with Speex quality-1 and ~15–20× faster than Speex quality-10 | fine on ARM | **soxr = LGPL-2.1**; SpeexDSP = Xiph BSD-style (`UNVERIFIED`) | 2 | **Low** — a fixed 3:1 FIR is simpler and license-clean | LGPL in an Apache-2.0 Unity package is a packaging problem |
| **Bigger dynamic-graph model** | alphacephei.com/vosk/models | `vosk-model-en-us-0.22-lgraph` supports grammar/dynamic graph | 7.82 / 8.20 WER (LibriSpeech test-clean / TEDLIUM) vs 9.85 / 10.38 for the shipped 40 MB model → ~21 % rel WERR | 128 MB model (vs 40 MB) | Apache-2.0 | 3 | **High accuracy, high APK cost** | 88 MB APK growth; RAM and grammar-rebuild time on Quest unmeasured |

---

## Detailed findings

### D1 — The decimation filter is the largest measurable defect in the front end `VERIFIED`

**Citations.** Repository source `NativeBridge~/src/downsampler.h` and `Runtime/Dsp/Downsampler.cs` (identical
coefficients); model config `NativeBridge~/vendor/vosk-model-small-en-us-0.15/conf/mfcc.conf`; Pohlhausen &
Bitzer, *Revisiting the Privacy of Low-Frequency Speech Signals*, arXiv:2508.02483 (Aug 2025),
<https://arxiv.org/html/2508.02483v1>; Kong, Mullangi & Kokkinakis, *Classification of Fricative Consonants for
Speech Enhancement in Hearing Devices*, PLOS ONE 2014,
<https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0095001>.

**Mechanism.** The filter is a 15-tap symmetric windowed sinc, applied at 48 kHz with output taken every third
sample. Its comment claims "cutoff at 1/6 of sample rate (8 kHz)… ~7.5 kHz passband". Evaluating
`H(f) = |Σ h[n] e^{-jωn}|` on the shipped coefficients gives something quite different.

`MEASURED (this report)` — response normalised to DC:

| f (Hz) | 1000 | 2000 | 3000 | 4000 | 5000 | 6000 | 7000 | 7500 |
|---|---|---|---|---|---|---|---|---|
| dB | −0.35 | −1.42 | −3.28 | −6.08 | −10.10 | −15.97 | −25.90 | −36.68 |

- `Σh = 1.3358` → the filter applies **+2.51 dB of un-normalised broadband gain**.
- Worst-case alias image rejection for output bins 0–7 kHz: **−29.2 dB** (input 9450 Hz folding to 6550 Hz).
- Cost: 15 MAC × 16 000 output/s = **0.24 MMAC/s**.

So the filter *does* anti-alias (the brief's "plain decimation-by-3" premise is incorrect and should be
corrected wherever else it appears) — but it does so by rolling off through the middle of the speech band. That
is not a decimation filter, it is a 3 kHz shelf.

**Why it matters.** `conf/mfcc.conf` reads `--num-mel-bins=40 --num-ceps=40 --low-freq=20 --high-freq=7600`.
The acoustic model was trained on features that use the full band to 7.6 kHz. On Quest, mel bins covering
4–7.6 kHz arrive 6–37 dB below where the model expects them. In log-mel terms that is a shift of roughly
−0.7 to −4.2 nats in the top ~13 of 40 bins on **every frame of every utterance** — a systematic, not random,
feature-space translation. Sibilant/non-sibilant and place-of-articulation cues for fricatives live in exactly
this region (Kong et al. 2014 locate the discriminating spectral slope at and above 8 kHz and the place cues in
the high-frequency region generally). The two most-documented VoXR substitutions —
`cease fire → safe five` (/s/↔/f/) and `all → fall` (inserted /f/) — are precisely the confusions you would
predict from starving the 4–7.6 kHz band. **This is a hypothesis with a mechanism, not a proven cause.**

Pohlhausen & Bitzer (2025) is the closest recent quantitative work: they show ASR models *exploit* aliasing
artefacts when anti-alias filtering is omitted, i.e. WER is measurably a function of the resampling filter, not
just of bandwidth. Their absolute numbers (8.90 % WER at 1600 Hz, 27.55 % at 800 Hz, 52.15 % at 500 Hz,
LibriSpeech test-clean) are at far lower rates than VoXR's and are cited here only for the direction of the
effect.

**VoXR mapping.** Stage §2.2, `Downsampler` (both C++ and C# copies). Drop-in replacement of the coefficient
table plus a longer history buffer; the `Process` loop structure does not change.

**Feasibility.** `MEASURED (this report)` — Kaiser-windowed sinc designs, DC-normalised, evaluated the same way:

| N | fc / β | max droop 0–7 kHz | worst alias into 0–7 kHz | cost |
|---|---|---|---|---|
| 63 | 7800 / 7.0 | −0.99 dB | −31.7 dB | 1.01 MMAC/s |
| 81 | 7800 / 8.0 | −0.59 dB | −42.3 dB | 1.30 MMAC/s |
| **95** | **7800 / 8.5** | **−0.36 dB** | **−53.9 dB** | **1.52 MMAC/s** |
| 127 | 7800 / 9.5 | −0.10 dB | −95.2 dB | 2.03 MMAC/s |

N=95 is the recommended point. 1.52 MMAC/s on a Cortex-A78-class core at ~2 GHz is under 0.2 % of one core even
without NEON; the filter is symmetric so the fold-and-multiply trick halves it again. Latency rises from 7 to
47 input samples of group delay = **0.98 ms** at 48 kHz — irrelevant next to the 0.5–2 s buffer window.

**Risk.** Every score in the 699-utterance A/B corpus and the fixture corpus shifts. `minScore` 0.6 /
`minConfidence` 0.4 and the leading-required-miss bar were tuned against the *current* spectral tilt; they must
be re-validated, not assumed. Also: the +2.51 dB DC gain is currently absorbed by the AGC, so removing it
changes effective gain — normalise the filter and re-check the AGC target together, not separately.

**Open questions.** Does the Editor path (C# `Downsampler`) see the same input spectrum as Quest? Is the
"batch, injected and free-speech scores read lower than live grammar-mode score" limitation
(`KNOWN_LIMITATIONS.md` L385) partly a DSP-path artefact?

---

### D2 — Speech enhancement as a blind front end: the evidence says don't `VERIFIED`

**Citations.** Chondhekar et al., *When De-noising Hurts: A Systematic Study of Speech Enhancement Effects on
Modern Medical ASR Systems*, arXiv:2512.17562 (19 Dec 2025), <https://arxiv.org/abs/2512.17562>; Yang, Pandey &
Wang, *Towards Decoupling Frontend Enhancement and Backend Recognition in Monaural Robust ASR*,
arXiv:2403.06387 (Mar 2024), <https://arxiv.org/abs/2403.06387>; Du, Zhang & Han, *Investigation of Monaural
Front-End Processing for Robust ASR without Retraining or Joint-Training*, arXiv:1810.09067 (Oct 2018),
<https://arxiv.org/abs/1810.09067>; Zhao, Wang & Qian, *Lightweight Front-end Enhancement for Robust ASR via
Frame Resampling and Sub-Band Pruning*, Interspeech 2025, arXiv:2509.21833.

**Mechanism.** A denoiser trained on an SNR/artefact distribution produces output whose *distortion* profile
the ASR has never seen. Suppression is not free: it removes low-energy speech components (fricative bursts,
stop releases) along with noise, and adds musical-noise/gating artefacts.

**Evidence, both directions.**
- Against: Chondhekar et al. tested MetricGAN+ denoising in front of Whisper, NVIDIA Parakeet, Gemini Flash 2.0
  and Parrotlet on 500 medical recordings × 9 noise conditions. **Original noisy audio beat enhanced audio in
  all 40 configurations**, with degradations of **+1.1 % to +46.6 % absolute** semantic WER.
- For, conditionally: Du et al. (2018) on CHiME-3 report **−36.40 % relative WER for a GMM back-end and only
  −11.78 % relative for a DNN back-end** — the stronger the back-end, the smaller the gain. Yang/Pandey/Wang
  (2024) do achieve decoupling (CHiME-2 5.57 % WER, −28.4 % relative vs prior best; CHiME-4 1-channel
  3.32/4.44 % simulated/real) but only with enhancement models designed and trained for the purpose, at
  research scale.
- Zhao/Wang/Qian (Interspeech 2025) reduce SE front-end compute **>66×** vs BSRNN while keeping ASR
  performance — evidence that ASR-serving SE can be made cheap, but again only when trained for the job.

**VoXR mapping.** Would insert a new stage between §2.2 and §2.3. The relevant asymmetry: VoXR's dominant
condition is close-talk speech at moderate SNR into a *grammar-constrained* decoder, where the failure mode is
already "the decoder picks the wrong in-grammar word", not "the decoder hears nothing". Suppression can only
subtract evidence.

**Feasibility on Quest.** GTCRN at 33 MMAC/s and RNNoise at 85 kB are both affordable; that is not the
constraint. The constraint is that VoXR cannot retrain `vosk-model-small-en-us-0.15` to match.

**Risk.** High: a change that improves subjective audio quality and *worsens* command accuracy is very easy to
ship by accident, because the A/B rig measures the parser outcome and a human listening to samples will
disagree with it.

**Open questions.** Would enhancement help specifically in the "game audio loud" condition, where SNR is
genuinely poor? That is the only regime where the evidence supports it, and it is testable with D12's
augmented corpus before any model is integrated.

**Verdict: do not adopt now. Re-open only if D12 shows a low-SNR regime that actually occurs in the field.**

---

### D3 — Lightweight enhancement candidates, if D2's verdict is ever reversed `VERIFIED`

**Citations.** Rong, Sun, Zhang, Hu, Zhu & Lu, *GTCRN: A Speech Enhancement Model Requiring Ultralow
Computational Resources*, ICASSP 2024, <https://sigport.org/documents/gtcrn-speech-enhancement-model-requiring-ultralow-computational-resources>,
code <https://github.com/Xiaobin-Rong/gtcrn>; Schröter et al., *DeepFilterNet2*, IWAENC 2022,
arXiv:2205.05474; *DeepFilterNet* (DFN3 demo), Interspeech 2023, arXiv:2305.08227, repo
<https://github.com/Rikorose/DeepFilterNet>; Valin, *A Hybrid DSP/Deep Learning Approach to Real-Time Full-Band
Speech Enhancement*, MMSP 2018, arXiv:1709.08243 + <https://jmvalin.ca/demo/rnnoise/>; Westhausen & Meyer,
*Dual-Signal Transformation LSTM Network for Real-Time Noise Suppression*, Interspeech 2020,
<https://github.com/breizhn/DTLN>; Braun, Gamper, Reddy & Tashev, *Towards efficient models for real-time deep
noise suppression*, arXiv:2101.09249 (NSNet2 family).

**Numbers.**

| Model | Size | Complexity | Quality | Licence |
|---|---|---|---|---|
| GTCRN | 48.2 K params | **33.0 MMAC/s** | PESQ 2.87 / STOI 0.940 (VCTK-DEMAND); DNSMOS-P.808 2.70 (DNS3); RTF 0.07 on i5-12400 | MIT |
| RNNoise | **85 kB** weights (8-bit) | 60× real time on x86; **7× real time on RPi 3** | band-gain quality, no PESQ quoted on the demo page | BSD |
| DTLN | <1 M params | 0.27 ms/frame (i5-6600K, TFLite-quant); **2.2 ms/frame on RPi 3B+** (real-time budget 8 ms) | Interspeech 2020 DNS results | MIT |
| DeepFilterNet2 / 3 | full-band 48 kHz | RTF **0.04** (DFN2) / **0.19** (DFN3), single-thread notebook Core-i5 | SOTA-class on DNS | code MIT/Apache-2.0 |
| NSNet2 | GRU-parameterised (R nodes/layer) | 16 kHz, 20 ms sqrt-Hann, 320-pt FFT, 161-dim log-power input | DNS-challenge baseline | MIT (MS DNS-Challenge) |

**VoXR mapping.** GTCRN and RNNoise are the only two whose cost is unarguably free on XR2 alongside a 90 fps
game; RNNoise additionally has no ML runtime dependency (plain C), which matters for a Unity package that
currently ships one 8.9 MB `libvosk.so` and nothing else. DTLN's ONNX/TFLite path would drag in a runtime.
DeepFilterNet operates at 48 kHz, i.e. *before* the decimator — architecturally the cleanest insertion point,
but the heaviest.

**Risk.** Beyond D2: RNNoise's 22-band gain model is coarse in exactly the 4–8 kHz region VoXR needs preserved.

**Open questions.** None worth answering until D2's precondition is met.

---

### D4 — Neural VAD gating: the right use of the enhancement CPU budget `VERIFIED`

**Citations.** Silero VAD, <https://github.com/snakers4/silero-vad> (MIT); "On-Device Wake Word Detection with
Vosk Grammar Mode and Silero VAD v5", <https://zenn.dev/diced/articles/vosk-silero-vad-wakeword-android>;
`KNOWN_LIMITATIONS.md` §"Cough, hum, and noise can trigger false matches in grammar mode".

**Mechanism.** Grammar-constrained VOSK has no silence output — `KNOWN_LIMITATIONS` states the root cause
exactly: the decoder *must* choose an in-vocabulary word, so any voiced noise maps to the nearest short word.
`[unk]` only catches out-of-grammar *speech*. A VAD in front of the recognizer supplies the missing class by
simply not feeding non-speech to the decoder.

**Evidence.** Silero VAD: ~2 MB JIT model, 8/16 kHz, "one audio chunk of 30+ ms processes in under 1 ms on a
single CPU thread", ONNX 4–5× faster still, MIT. The Android write-up is the closest published analogue to
VoXR's exact architecture — Vosk *grammar mode* + Silero VAD v5 on arm64 — and reports **0.8 ms per frame on
two threads on a Pixel 7, 0.3 ms with NNAPI**, with 32 ms frames at 16 kHz. Tuned to threshold 0.65 and a
160 ms minimum-speech requirement, its facility-noise simulation reached **11 % false positives / 4 % false
negatives**. The same article independently confirms two VoXR-relevant facts: `[unk]` must be present in the
grammar or the WFST search stalls on out-of-grammar audio, and **Vosk is not thread-safe** (it serialises
through a `HandlerThread`) — consistent with VoXR's single-recognizer-per-process limitation.

**VoXR mapping.** New stage between §2.1 and §2.2 (or between §2.2 and §2.3, at 16 kHz, which is cheaper).
It composes with, rather than replaces, `VoxrPushToTalkController`: PTT users get nothing, continuous-mode users
get the false-trigger fix. It also gives the AGC (D5/A2) the speech/no-speech signal it currently lacks — one
VAD serves both purposes.

**Feasibility.** 2 MB APK, ~2.5 % of one core at v5's measured rate. Needs an ONNX runtime or a hand-written
inference kernel; that is the real integration cost, not the CPU.

**Risk.** A VAD that trims onsets makes the leading-word-drop problem *worse*. Mitigation is standard: run the
VAD on a delayed copy and emit a 200–300 ms pre-roll when it opens. Also: the 11 % FP figure is from someone
else's environment and tuning, and must not be quoted as VoXR's expected rate.

**Open questions.** Does a VAD gate interact badly with VOSK's own endpointer (`model.conf`
`--endpoint.rule2.min-trailing-silence=0.5`)? Gating audio out may make the decoder's trailing-silence rules
fire differently and change how `UtteranceBuffer` reassembles.

---

### D5 — WebRTC APM (AEC3 + NS + AGC2): AGC2 is the drop-in, AEC3 is the project `VERIFIED`

**Citations.** WebRTC APM design doc,
<https://webrtc.googlesource.com/src//+/7c793a7dbe548735fe9e1d107e00d17937202f47/modules/audio_processing/g3doc/audio_processing_module.md>;
`dignifiedquire/sonora` — pure-Rust port of WebRTC M145, <https://github.com/dignifiedquire/sonora> (BSD-3);
Unity `MonoBehaviour.OnAudioFilterRead` docs.

**Mechanism.** AGC2 combines an input-volume controller, an adaptive digital controller and a fixed digital
controller with a limiter, driven by an **RNN-based VAD** plus a clipping predictor and speech-level estimator.
That is exactly the architecture VoXR's `Agc` lacks (see A2/A3). AEC3 is an adaptive-filter echo canceller with
delay estimation; NS is Wiener-filter based.

**Cost.** Sonora reports processing a 10 ms frame **with AEC3 + NS + AGC2 all enabled**: **4.2 µs at 16 kHz
mono**, 13.3 µs at 48 kHz mono, on an Apple M4 Max with the NEON backend, running at 1.07–1.24× the speed of
the C++ reference. Even derating 30× for XR2 that is ~126 µs per 10 ms frame = **~1.3 % of one core**. The
APM is documented as fully thread-safe and handles input rates below 384 kHz.

**VoXR mapping.** Two independent decisions:
1. **AGC2 alone**, replacing `agc.h` at stage §2.2. No reference signal needed. This is the smallest change
   that fixes the silence-gain-runaway and the mis-scaled target in one move, with a component that has a
   decade of production tuning behind it.
2. **AEC3**, needing the game's output mix as a far-end reference. In Unity that means an
   `OnAudioFilterRead` MonoBehaviour on the AudioListener GameObject capturing the final mix and pushing it
   across the C ABI as the far-end signal, sample-rate-converted and delay-aligned to the capture stream. Unity
   documents `OnAudioFilterRead` as running on the audio thread at ~20 ms granularity with Unity API calls
   forbidden inside it; the AudioListener-attachment pattern is widely used but is `UNVERIFIED` against the
   official docs, which only describe the AudioSource case.

**Feasibility.** The C++ `webrtc-audio-processing` standalone (BSD-3) builds for Android arm64 and is what
PulseAudio ships. Sonora is a Rust alternative if the C++ build proves painful. Either adds ~1–2 MB.

**Risk.** (a) AEC3 without accurate delay alignment does nothing or actively damages the near-end signal.
(b) NS is enhancement — D2 applies; enable AEC3 + AGC2, leave NS off, and measure NS separately.
(c) A second AGC stacked on any platform AGC (see D6) fights it.

**Open questions.** What is the actual acoustic coupling from Quest 3's speakers to its mics at game-audio
levels? Nobody has published it. Until it is measured (D12's augmented corpus, or a direct on-device
recording), AEC3 is a solution to a hazard of unknown size.

---

### D6 — Android audio source and platform effects on Horizon OS: unknown, and cheap to find out `VERIFIED`

**Citations.** AOSP, *Configure preprocessing effects*,
<https://source.android.com/docs/core/audio/implement-pre-processing>; Android `MediaRecorder.AudioSource`
reference; `google/oboe` issue #951, <https://github.com/google/oboe/issues/951>; Meta *Voice SDK Overview*,
<https://developers.meta.com/horizon/documentation/unity/voice-sdk-overview/>; Meta *Voice Technology* design
guide, <https://developers.meta.com/horizon/design/voice-technology/>; Meta v64 blog,
<https://www.meta.com/blog/meta-quest-v64-update-passthrough-improvements-external-microphones/>.

**What is established.**
- **Defaults are per-device, not per-Android-version.** AOSP: "The default preprocessing effects applied for
  each `AudioSource` instance are specified in the `/vendor/etc/audio_effects.xml` file." The only universal
  guidance is a prohibition: for `VOICE_RECOGNITION`, "don't enable the noise suppression preprocessing effect.
  It shouldn't be turned on by default." AOSP's own example (Nexus 10) attaches AEC+NS to `VOICE_COMMUNICATION`
  and AGC to `CAMCORDER`.
- `VOICE_RECOGNITION` (the constant `6` VoXR passes) is documented as the source tuned for recognition and as
  not employing AGC or noise suppression. **VoXR's choice of source is correct by the book.**
- `UNPROCESSED` is not universally available; `AudioManager.getProperty(PROPERTY_SUPPORT_AUDIO_SOURCE_UNPROCESSED)`
  must be checked first.
- Effects can silently fail to engage. oboe#951 documents `AcousticEchoCanceler` and `NoiseSuppressor` being
  created and enabled against an AAudio-backed stream with no error and no effect. VoXR already knows this
  hardware lies about audio: `KNOWN_LIMITATIONS.md` records AAudio *input delivering silence* on Quest 3, which
  is why the JNI `AudioRecord` path exists.
- **Meta publishes nothing useful here.** The Voice SDK overview confirms only that "Wit.ai processes the voice
  data on your behalf" — Meta's own first-party voice stack is *cloud*, so there is no first-party offline
  precedent to copy. The Voice Technology design guide covers activation models (UI affordance, immersion,
  gaze, gesture) and is silent on echo cancellation, noise suppression and speaker-to-mic interference. The v64
  blog only adds experimental USB-C external mic support. No official mic count, frequency response, noise
  floor, or gain figure for Quest 3 could be located; treat all of those as **unpublished**, not as unknown-to-me.

**VoXR mapping.** Stage §2.1, `audio_capture_audiorecord.cpp`. Confirmed as-built: source `6`
(`VOICE_RECOGNITION`), 48 000 Hz, `CHANNEL_IN_MONO` (16), `ENCODING_PCM_FLOAT` (4), buffer
`max(2 × minBufferSize, 15360 bytes)`, blocking reads of 960 frames (~20 ms). **No `AudioEffect` of any kind is
attached, and no audio session id is used.**

**Feasibility of learning more.** One device session, no code change: `adb shell cat /vendor/etc/audio_effects.xml`,
plus a scratch build that logs `AcousticEchoCanceler.isAvailable()`, `NoiseSuppressor.isAvailable()`,
`AutomaticGainControl.isAvailable()` and, if created against the AudioRecord session id, their `getEnabled()`
state, and `AudioManager.getProperty(PROPERTY_SUPPORT_AUDIO_SOURCE_UNPROCESSED)`.

**Risk.** `VOICE_COMMUNICATION` would likely bring platform AEC — and, on most devices, platform NS and AGC
tuned for *telephony intelligibility*, which is the exact class of processing AOSP tells you not to put in
front of a recogniser. Switching to it is a plausible experiment and a bad default.

**Open questions.** Does Horizon OS apply beamforming or its own noise suppression to source 6 regardless of
`audio_effects.xml`? The documented "Quest 3 noise-cancelling microphone suppresses voice" community reports
suggest *something* is being applied, but that is forum hearsay — `UNVERIFIED`.

---

### D7 — Is AGC needed at all for a Kaldi nnet3 chain model? Yes, but for a narrower reason than assumed `VERIFIED`

**Citations.** Kaldi online decoding documentation, <https://kaldi-asr.org/doc/online_decoding.html>; model
`conf/mfcc.conf` (`--use-energy=false --num-ceps=40`); `KNOWN_LIMITATIONS.md` §"Quest 3 microphone gain is low";
WebRTC AGC2 (D5).

**Mechanism.** Kaldi's online nnet3 pipeline provides the network with "un-adapted and non-mean-normalized
features (MFCCs, in our example recipes)" plus an i-vector; CMVN is applied inside the i-vector branch, not to
the network input. Therefore input gain is **not** normalised away before the acoustic model.

A scalar gain *g* multiplies every mel-filterbank energy by *g²*, i.e. adds a constant `2·ln g` to every log-mel
bin. The DCT of a constant vector is non-zero only in the 0th coefficient, so **gain shifts c0 and leaves
c1…c39 unchanged**. With `--use-energy=false`, c0 is the only gain-sensitive dimension of 40. The network sees a
c0 far outside its training range for very quiet input; deltas and the LDA-like input transform spread that
somewhat, but it is a one-dimensional, systematic offset — not a catastrophe.

The quantisation argument is weaker than the code comment implies. `KNOWN_LIMITATIONS` records Quest 3 peaks of
0.04–0.4 on a [−1,1] scale. At peak 0.04 (−27.96 dBFS), int16 conversion leaves an effective quantisation SNR of
≈96 − 28 = **68 dB** `MEASURED (this report)`, which is at or below typical MEMS microphone self-noise. So
converting to int16 at native level costs almost nothing; the gain applied to the *analogue-domain* signal was
already the limiting factor.

**Conclusion.** Keep an AGC — but its job is "keep c0 in the trained range", which means a **slow, speech-only,
utterance-level** normaliser, not a fast per-sample compressor. That is precisely the AGC2 design.

**VoXR mapping.** Stage §2.2. See §"Audit" A2–A5 for the specific defects.

**Risk.** Removing AGC entirely on the strength of "CMVN handles it" would be wrong here — this model does not
CMVN the network input. Any experiment must include an AGC-off arm to establish the true baseline.

---

### D8 — VOSK already does online speaker adaptation, and VoXR destroys it on every grammar rebuild `VERIFIED`

**Citations.** `NativeBridge~/vendor/vosk-model-small-en-us-0.15/ivector/` (`final.ie` 8.29 MB, `final.dubm`,
`final.mat`, `global_cmvn.stats`, `online_cmvn.conf`, `splice.conf` with ±3 context); `vosk-api/src/recognizer.cc`,
<https://raw.githubusercontent.com/alphacep/vosk-api/master/src/recognizer.cc>; Kaldi online decoding doc;
Saon, Soltau, Nahamoo & Picheny, *Speaker adaptation of neural network acoustic models using i-vectors*, ASRU
2013 — **`UNVERIFIED`** (the ~10 % relative WERR on Switchboard 300 h figure reached me only via search
snippets; the primary was not fetched).

**Mechanism.** The shipped 40 MB model contains a full i-vector extractor — 8.29 MB of its 40 MB is `final.ie`.
VOSK therefore builds an `OnlineNnet2FeaturePipeline` with an i-vector branch. Kaldi estimates the i-vector
left-to-right, "accumulating information progressively through an utterance and incorporating speaker history
when available". So the longer one recognizer instance lives, the better adapted it becomes to this speaker and
this microphone.

**The defect.** In `recognizer.cc`, the feature pipeline is created per-`Recognizer` and is destroyed only by
`CleanUp()` — which the source gates on `decoder_ == nullptr || state_ == RECOGNIZER_FINALIZED ||
frame_offset_ > 20000` with the comment "Each 10 minutes we drop the pipeline to save frontend memory in
continuous processing" — **and by `SetGrm()`, which "reinitializes the decoder and feature pipeline"**.
`Recognizer::Reset()` does *not* touch it.

VoXR frees and recreates the recognizer whenever the active command set or a dynamic slot's values change
(brief §2.4, `KNOWN_LIMITATIONS.md` §"Active set switching has a brief audio gap"). So the documented cost of a
set switch — "a ~50 ms+ audio gap" — is **understated**: it also discards all accumulated i-vector and online-CMVN
adaptation and returns the decoder to the speaker-independent prior. In a game that switches between weapons /
navigation / helm modes, the decoder may spend most of its life in the unadapted state.

**VoXR mapping.** Stage §2.3/§2.4. Two candidate fixes, both in the bridge:
- Prefer VOSK's own `SetGrm` over free-and-recreate only if it can be shown to preserve more state — from the
  source above, **it does not**; it reinitialises the pipeline too. So this is an upstream limitation.
- Therefore: reduce the *frequency* of grammar rebuilds (superset grammar + application-level set gating, which
  `KNOWN_LIMITATIONS` already suggests for a different reason), and/or **re-prime** the new recognizer by
  replaying a few hundred ms of recent buffered speech through it before live audio resumes. Re-priming costs
  CPU at switch time, not steady-state, and it also fills the audio gap.

**Feasibility.** Re-priming is pure bridge work: keep a rolling 1–2 s of the 16 kHz post-AGC signal, and after
constructing the new recognizer feed it that history with results discarded. It composes with the existing ring
buffer.

**Risk.** Re-priming lengthens the switch stall (though the audio need not be real-time — 2 s of audio decodes
in ~0.2 s at the model's 0.11×RT desktop speed, more on Quest). Also the i-vector may adapt to the *wrong*
speaker in a shared-headset or spectator-audio scenario.

**Open questions.** How much WER does the i-vector actually buy on *this* model for *this* speaker? Directly
measurable offline: run the 699-utterance A/B corpus (a) as one continuous recognizer session, (b) with the
recognizer recreated between every utterance. The delta is the adaptation VoXR is discarding.

---

### D9 — Test-time adaptation (SUTA and successors): strong results, wrong runtime `VERIFIED`

**Citations.** Lin, Li & Lee, *Listen, Adapt, Better WER: Source-free Single-utterance Test-time Adaptation for
Automatic Speech Recognition*, Interspeech 2022, arXiv:2203.14222; Lin, Huang & Lee, *Continual Test-time
Adaptation for End-to-end Speech Recognition on Noisy Speech* (DSUTA), EMNLP 2024, arXiv:2406.11064; van Dalen,
Zhang, Parcollet & Bhattacharya, *Robust Unsupervised Adaptation of a Speech Recogniser Using Entropy
Minimisation and Speaker Codes*, Interspeech 2025, arXiv:2506.10653.

**Mechanism.** SUTA adapts a CTC model to a single test utterance by minimising output entropy over a few
gradient steps, with no source data and no labels. DSUTA extends this to continual adaptation with a dynamic
reset that detects domain shift. The 2025 speaker-codes work replaces single-hypothesis entropy with a
"conditional entropy over complete hypotheses" and adds compact per-speaker code vectors.

**Evidence.** The 2025 paper is the most directly relevant number: **−20 % relative WER from one minute of
adaptation data, −29 % from ten minutes**, on far-field noise-augmented Common Voice. That is the size of prize
a personalization feature could aim at. (SUTA's and DSUTA's own numeric tables were not obtainable from the
abstract pages; treat their magnitudes as `UNVERIFIED`.)

**VoXR mapping.** Stage §2.3. **Not adoptable.** All three require backpropagation through the acoustic model at
run time. VOSK's C ABI exposes no such path; Kaldi nnet3 on device is inference-only; and doing gradient steps
per utterance on an XR2 core alongside a 90 fps renderer is not a budget that exists. The *concept* survives as
D8 (i-vectors are the nnet3-era answer to the same question) and as the offline path in D11.

**Risk / open question.** The one adoptable idea from this line is the *speaker code* framing: a small,
per-user vector learned once and injected as an auxiliary input. i-vectors already are that; the question is
whether a **stored, enrolment-derived i-vector** could be injected into a fresh recognizer instead of
re-estimating from zero. Kaldi's online i-vector extractor supports priors and speaker history; VOSK's public
API does not expose them. Fixing that would need a patched `libvosk` — a real but bounded piece of work,
and the highest-leverage upstream change in this whole report.

---

### D10 — Few-shot personalization as a *verifier*, not a recogniser `VERIFIED`

**Citations.** Rusci & Tuytelaars, *Few-Shot Open-Set Learning for On-Device Customization of KeyWord Spotting
Systems*, Interspeech 2023, arXiv:2306.02161; Cioflan, Cavigelli & Benini, *Boosting keyword spotting through
on-device learnable user speech characteristics*, tinyML Research Symposium 2024, arXiv:2403.07802.

**Evidence.** Rusci & Tuytelaars: a deep feature encoder + prototype classifier reaches **76 % accuracy in a
10-shot scenario at a 5 % false-acceptance rate on unknown data**, over 10 Google Speech Commands classes,
with a triplet-loss-trained normalised encoder beating prototypical networks with dummy unknown-class
prototypes. Cioflan et al.: a user-aware embedding layer over a pretrained backbone cuts error from
**30.1 % to 24.3 % (−19 % relative)** on the 35-class GSC problem, at **23.7 k parameters and 1 MFLOP per epoch
of on-device training** — i.e. cheap enough for a microcontroller, let alone XR2.

**VoXR mapping.** Not as a replacement decoder — VoXR's command space is compositional (slots, digit sequences,
dynamic values), which few-shot KWS is not built for. The realistic mapping is a **rejection verifier at stage
§2.6/§2.7**: given the audio span the parser matched to a command, score it against user-enrolled prototypes of
that command's required literals, and use the score to (a) veto low-scoring fires and (b) supply a *calibrated*
confidence to replace VOSK's flat per-word `conf`.

**Feasibility.** A ~24 k-parameter encoder plus per-user prototypes is a few hundred KB. On-device enrolment
(5–10 utterances) and on-device prototype updates are both within the cited budgets.

**Risk.** A second model to ship, train and keep aligned with Inspector-authored command sets; developers who
add a command must not be forced to re-record. Mitigation: prototypes only for the *phrases the user actually
gets wrong*, mined from the debug log.

**Open questions.** Would a verifier trained on GSC-style isolated words transfer to VoXR's connected,
multi-word, mid-game speech? Untested.

---

### D11 — Synthetic/TTS data: good for the corpus, uncertain for the model `VERIFIED`

**Citations.** Ogun, Colotte & Vincent, *An Exhaustive Evaluation of TTS- and VC-based Data Augmentation for
ASR*, arXiv:2503.08954 (2025); Quintas, Ferrané & Pellegrini, *Enhancing Synthetic Training Data for Speech
Commands: From ASR-Based Filtering to Domain Adaptation in SSL Latent Space*, arXiv:2409.12745 (2024); VOSK
adaptation docs, <https://alphacephei.com/vosk/adaptation>; VOSK model list,
<https://alphacephei.com/vosk/models>.

**Evidence.**
- Ogun et al.: joint augmentation across multiple speech attributes gives **−11 % relative WER on Common Voice
  and up to −35 % relative on LibriSpeech** with a Conformer-Transducer, versus real data only. Crucially they
  report two *negative* results: **pitch augmentation and voice-conversion-based speaker augmentation were
  ineffective**, and "naively combining synthetic and real data often does not yield the best results".
- Quintas et al.: a simple **ASR-based filter on generated data** materially improves downstream speech-command
  performance, and synthetic vs real speech remains linearly separable in self-supervised feature space —
  which they treat with CycleGAN domain adaptation. The separability result is the warning: a model tuned on
  TTS is tuned on a detectable, distinct distribution.
- VOSK's own guidance: acoustic-model finetuning needs "**about 1 hour of data**" in Kaldi format; runtime
  vocabulary updates require a dynamic-graph model; **no i-vector or on-device speaker adaptation is offered**.

**VoXR mapping.** Two very different uses, and VoXR is currently making only the risky one:
1. **Corpus** (§2.8) — the 699-utterance A/B corpus and the `Tests~/Fixtures` WAV corpus are TTS-generated.
   Per Quintas et al., that corpus is measurably *not* the field distribution. Every threshold VoXR tunes on it
   (`minScore`, `minConfidence`, `bufferWindow`, the leading-miss bar) inherits that bias. This is a
   measurement-validity problem that no amount of front-end work fixes.
2. **Model** (§2.3) — finetuning `vosk-model-small-en-us-0.15` on ~1 h of command-domain speech is technically
   documented and would be the strongest single accuracy lever available, but Ogun et al.'s negative results
   say TTS-only data with pitch/VC speaker augmentation is the *wrong* recipe. It needs real recorded speech in
   the mix.

An intermediate that costs nothing: `vosk-model-en-us-0.22-lgraph` is 128 MB, supports the dynamic graph
(so grammar mode still works), Apache-2.0, and reports **7.82 / 8.20 WER** (LibriSpeech test-clean / TEDLIUM)
against the shipped model's **9.85 / 10.38** — ~21 % relative WERR for +88 MB of APK. Whether Quest 3 can hold
it in RAM and rebuild its graph quickly enough is unmeasured.

**Risk.** Overfitting to TTS voices; APK size; and the fact that a finetuned model is a fork VoXR must then
maintain.

**Open questions.** How many real human utterances exist? The field evidence base is 10 sessions / 73
utterances / 361 words from **one speaker**. That is far too small for finetuning and, more importantly, too
small to validate *any* of this report's recommendations on real speech.

---

### D12 — Playback-interference and Lombard augmentation: the missing test conditions `VERIFIED`

**Citations.** Raju, Panchapagesan, Liu, Mandal & Strom, *Data Augmentation for Robust Keyword Spotting under
Playback Interference*, arXiv:1808.00563 (2018); Ma, Petridis & Pantic, *Investigating the Lombard Effect
Influence on End-to-End Audio-Visual Speech Recognition*, Interspeech 2019, arXiv:1906.02112; Snyder, Chen &
Povey, *MUSAN: A Music, Speech, and Noise Corpus*, arXiv:1510.08484 (2015); Park et al., *SpecAugment*,
Interspeech 2019, arXiv:1904.08779.

**Mechanism and evidence.**
- Raju et al. define exactly VoXR's hazard: "imperfect cancellation of the audio playback from the device,
  resulting in residual echo, after being processed by the AEC system". Mixing music and TV/movie content into
  training at varied signal-to-interference ratios yielded **30–45 % relative reduction in false-reject rates**
  across a range of false-alarm rates, under device playback.
- Ma et al.: "properly modelling Lombard speech is always beneficial"; testing on *plain* speech mixed with
  noise **overestimates** audio-only recogniser performance. VR players speaking over game audio while
  physically exerting are producing Lombard speech; VoXR's entire corpus is calm TTS. So VoXR's offline
  measurements are, per this result, optimistic by an unknown margin.
- SpecAugment (time/frequency masking) improved LibriSpeech test-other to 6.8 % WER without an LM and 5.8 % with
  — the canonical cheap feature-level augmentation, applicable only if VoXR ever finetunes.
- MUSAN supplies the noise/music/speech corpus under a permissive Creative Commons licence.

**VoXR mapping.** Stage §2.8 first, §2.2/§2.3 second. Concretely: take the existing WAV-replay fixture corpus
and generate variants with (a) real game audio mixed at, say, +20 / +10 / +5 / 0 dB SIR, (b) MUSAN noise,
(c) a small set of **human-recorded** Lombard/exerted takes of the top 20 commands. Replay through the real
DSP+VOSK pipeline via the existing push-audio bridge. The output is a per-condition accuracy curve — which is
the evidence needed to decide whether AEC (D5) is worth building at all, and which of D1/D2/D4 actually pays.

**Feasibility.** Pure offline tooling on top of infrastructure that already exists (WAV replay, push-audio
bridge, A/B rig). No device time except the human Lombard recordings.

**Risk.** Digitally mixed game audio is not the Quest's real speaker→mic transfer function (which is
unpublished). It is a lower bound on difficulty, not a model of it. The honest version records the same
utterances on-device with game audio actually playing.

**Open questions.** What SIR does game audio actually reach at the Quest mics at typical volume? Unmeasured and,
per D6, unpublished by Meta.

---

## Audit of the current front end

Source of record: `NativeBridge~/src/audio_capture_audiorecord.cpp`, `downsampler.h`, `agc.h`,
`vosk_bridge.cpp` (lines 116–126 establish the order: **downsample → AGC → float→int16 → VOSK**), plus the C#
mirrors `Runtime/Dsp/Downsampler.cs` and `Runtime/Dsp/Agc.cs`.

### A1 — The downsampler *does* anti-alias; the brief is wrong, and the filter is still the top defect

`downsampler.h` implements a 15-tap symmetric windowed-sinc FIR at 48 kHz with output every 3rd sample. It is
not naive decimation. **But** `MEASURED (this report)`: DC gain 1.3358 (**+2.51 dB un-normalised**), passband
−3.28 dB @3 kHz / −6.08 dB @4 kHz / −15.97 dB @6 kHz / −25.90 dB @7 kHz, worst alias image rejection −29.2 dB
into 0–7 kHz. The header comment ("cutoff at 1/6 of sample rate… ~7.5 kHz passband") does not describe the
coefficients that are actually there.

**Evidence it matters:** the model's `conf/mfcc.conf` uses `--high-freq=7600` over 40 mel bins, so bins covering
4–7.6 kHz are systematically starved; Kong et al. (2014) place fricative place/sibilance cues in the high band;
`KNOWN_LIMITATIONS` documents `cease fire → safe five` and `all → fall` — /s/↔/f/ class confusions. **Evidence
it might not matter:** VoXR has never measured with a corrected filter, and grammar-mode decoding is
constrained enough that some of these confusions may survive any spectral fix. See D1 for the 95-tap
replacement and its 1.52 MMAC/s cost.

### A2 — The AGC has no voice-activity gate; it amplifies silence by design

`kNoiseFloor = 1e-5f` (≈−100 dBFS) is the only "is there signal" test, and no real microphone noise floor is
below it. So in every pause `desired_gain = target_level_ / smoothed_level_` saturates at `kMaxGain = 20`, and
the gain smoother — release coefficient `1 − exp(−1000/(16000 × 300)) = 2.08e−4`, i.e. τ = 300 ms — carries
`current_gain_` from 1.0 to ~10× in **~190 ms** and ~19× in **~0.9 s** `MEASURED (this report)`. A cough, hum
or breath arriving after a one-second pause is presented to VOSK **~26 dB hotter than speech would be**.

**Evidence it matters:** this is a mechanically sufficient cause for the documented "Cough, hum, and noise can
trigger false matches in grammar mode" limitation, and it compounds it — grammar mode must emit *some*
in-vocabulary word, and it is being handed a loud, well-conditioned noise burst instead of a quiet one. Every
production design of this class (WebRTC AGC2) is VAD-driven precisely to prevent this. **Evidence it might not
matter:** unmeasured on VoXR; the AGC's fast 10 ms attack does pull gain back down within one frame once
energy arrives, so the effect is confined to onsets — which is A4's problem, not a separate escape.

### A3 — The AGC target is applied to the wrong statistic

`target_level_ = db_to_linear(−18) = 0.1259` is compared against `smoothed_level_`, an EMA of `|x|` with 5 ms
attack / 200 ms release — a **mean-absolute** estimator. Speech peak-to-mean-abs is roughly +14 to +20 dB, so
post-gain peaks land at about −4 to +2 dBFS and `fast_tanh` compresses on essentially every syllable
(`tanh(1.0) = 0.762`, i.e. −2.4 dB; `tanh(1.5) = 0.905`). A −18 dBFS target is a sensible *RMS* target and an
aggressive *mean-abs* one.

**Evidence it matters:** soft clipping is waveform distortion; it adds harmonics that smear the very
high-frequency detail A1 is trying to preserve. **Evidence it might not matter:** tanh is smooth and
odd-symmetric, so the distortion products are relatively benign compared to hard clipping, and VOSK is a
robust model. Nobody has measured it. A cheap arm: target −24 dBFS on the same estimator and re-run the A/B.

### A4 — The onset transient is the most suspicious interaction in the file

Coming out of silence at gain ≈20, the first speech samples are amplified 20× and hard-limited by tanh; the
gain smoother's attack τ is 10 ms, so it takes ~20–30 ms (2–3 τ) to settle. That is 2–3 chain frames at the
model's `--frame-subsampling-factor=3` — one phoneme.

**Evidence it matters:** `KNOWN_LIMITATIONS.md` documents "A command whose first word the decoder dropped is
silent rather than recovered" (L449), and 2.0.0's entire leading-required-miss bar exists because leading words
go missing. Field evidence also shows that an in-grammar word VOSK drops leaves *no* token and *no* timing hole
— consistent with a distorted, mis-scaled first phoneme being absorbed into a neighbouring alignment rather
than producing garbage. **Evidence it might not matter:** this is an untested hypothesis; leading-word loss has
at least three other plausible causes (PTT button latency, VOSK's endpointer, and the utterance buffer). But it
is the cheapest of the four to falsify.

### A5 — AGC is nonetheless the right thing to have here

Per D7: Kaldi's nnet3 online pipeline feeds the network non-mean-normalised MFCCs, so gain is not normalised
away; it lands entirely on c0 (because `--use-energy=false` and the DCT maps a constant log-mel offset to the
0th cepstral coefficient only). The quantisation argument is much weaker: at the documented 0.04 peak the
residual int16 quantisation SNR is ≈68 dB, at or below MEMS-mic self-noise `MEASURED (this report)`. So the
justification for AGC is c0-range matching — which argues for a slow, speech-gated, utterance-level normaliser,
not a per-sample compressor with a 10 ms attack.

### A6 — The audio-source choice is correct; the platform-effects question is unexamined

`AudioRecord(6 /*VOICE_RECOGNITION*/, 48000, 16 /*CHANNEL_IN_MONO*/, 4 /*ENCODING_PCM_FLOAT*/, bufferSize)`,
blocking `read(float[], …, READ_BLOCKING)` of 960 frames. Source 6 is the documented right choice for a
recogniser (no platform AGC, no NS). **Evidence it matters that nothing else was tried:** AOSP is explicit that
what any given device attaches to any given source is defined in `/vendor/etc/audio_effects.xml`, so VoXR does
not currently know whether Horizon OS applies anything at all to source 6 — and `UNPROCESSED` availability was
never queried. **Evidence the status quo is fine:** AOSP forbids default-on NS for `VOICE_RECOGNITION`, and
`VOICE_COMMUNICATION` would likely bring telephony-tuned NS/AGC, which is the wrong class of processing.
Nothing here should change without the one-session measurement in D6.

### A7 — No echo cancellation, no far-end reference, on a device whose speakers are centimetres from its mics

No `AudioEffect` is attached (no session id is even requested), and no game-audio reference signal is plumbed
into the native bridge. Whether this matters is genuinely unknown: Meta publishes no coupling figure and no mic
specification. Raju et al.'s 30–45 % relative FRR reduction under device playback is the closest evidence that
the hazard is real for a comparable class of system. The right order is **measure (D12) → then decide on AEC3
(D5)**, not the reverse.

### A8 — Float capture, int16 delivery: correct, and the reason is documented

`ENCODING_PCM_FLOAT` capture then `float_to_int16` before `vosk_recognizer_accept_waveform_s` is a deliberate
workaround for `vosk_recognizer_accept_waveform_f` being broken in the prebuilt arm64 `libvosk.so`
(`KNOWN_LIMITATIONS.md` L884). No change recommended. Note only that the AGC's tanh output is bounded to
(−1, 1), so the int16 conversion cannot overflow — a correctness property worth keeping if the AGC is replaced.

### A9 — Duplicated DSP in C++ and C#

`Runtime/Dsp/Downsampler.cs` and `Runtime/Dsp/Agc.cs` carry byte-identical coefficient tables and logic to their
C++ counterparts (verified by inspection). Any change from D1/A2/A3 must be made in both, and the WAV-replay
and A/B rigs must be checked for which implementation they exercise — otherwise a fix validated offline may not
be the fix that ships to Quest. This is a real hazard given `KNOWN_LIMITATIONS.md` L385 already records that
"batch, injected, and free-speech scores read lower than the live grammar-mode score".

### A10 — Chunking

`vosk_bridge.cpp` reads 4096 samples (~85 ms at 48 kHz) per iteration, decimating to ~1365 samples at 16 kHz per
`accept_waveform` call. This bounds the minimum end-of-speech reaction granularity at ~85 ms before any of the
buffering in §2.5. Not a robustness issue, but it belongs in any latency budget.

---

## Personalization options

Ordered by cost. All four are compatible with each other.

### P1 — Preserve and re-prime the i-vector state (engineering only, no user-visible enrolment)

**What.** Stop discarding VOSK's online speaker adaptation on every command-set / dynamic-slot change. Two
mechanisms: (a) reduce rebuild frequency by shipping a superset grammar and gating sets in the application
layer; (b) keep a rolling 1–2 s buffer of post-AGC 16 kHz audio and, immediately after constructing a new
recognizer, feed it that history with results discarded, so the i-vector and online-CMVN state restart warm.

**Cost.** Bridge-only. ~32 KB of extra ring buffer. A one-off decode of ≤2 s of audio at recognizer-creation
time (well under 1 s of CPU even on Quest, given the model's 0.11×RT desktop figure). No API change, no
user-facing flow, no model change.

**Expected gain.** Unquantified for this model. The i-vector SAT literature's ~10 % relative WERR
(`UNVERIFIED`) is the order of magnitude to hope for, and only in sessions with frequent set switching. The
side benefit is real and certain: it partly fills the ~50 ms audio gap.

**Cheapest validation.** Offline, no device: run the 699-utterance A/B corpus twice through the real decoder —
once as a single continuous recognizer session, once recreating the recognizer between every utterance. The
delta is exactly the adaptation currently being thrown away. If it is ~0, drop this option.

### P2 — Enrolment phrases → per-user threshold calibration (cheapest user-visible option)

**What.** A 30-second first-run flow: the user speaks 5–8 phrases drawn from the developer's own command set
(chosen to cover the digit vocabulary, the sibilant-initial commands, and the shortest commands). VoXR records
the resulting per-word `conf` distribution and score distribution and derives **per-user** `minScore` /
`minConfidence` offsets, plus per-command confidence baselines. It does *not* change the model.

**Why it works.** `KNOWN_LIMITATIONS` documents that "'two' consistently scores low confidence" regardless of
clarity, and that `minConfidence` cannot be raised globally without rejecting NumberSequence commands. That is a
*calibration* failure, and calibration is exactly what a handful of labelled utterances fixes. A per-user,
per-word confidence baseline turns VOSK's uncalibrated `conf` into a z-score against that user's own
distribution, which is comparable across words in a way the raw value is not.

**Cost.** Pure C#. A small serialised profile (a few hundred bytes). One new UX flow developers may opt into.
No native change, no model change, no APK growth.

**Expected gain.** Unmeasured. Plausibly the difference between `minConfidence` being unusable and being a real
rejection lever — which would bear directly on the cough/hum false-trigger problem. This is the single
best cost/benefit item in the personalization list, and it is entirely within VoXR's existing architecture.

**Cheapest validation.** Replay the 73 field utterances plus the cough/hum recordings, compute per-word
confidence distributions, and check whether a per-user z-score threshold separates true commands from noise
triggers where the raw threshold does not.

### P3 — Enrolment-derived alias and pronunciation mining

**What.** During enrolment (and, opt-in, during play), log every case where the user's speech produced a
recognised-but-wrong or unrecognised transcript for a known intent. Mine the recurring substitutions
(`switch to → switch two`, `cqb → [unk]`) and offer the developer generated **alias** entries and grammar
additions. This is personalization of the *grammar*, not the model — which is the only layer VoXR fully
controls.

**Cost.** Editor tooling plus a debug-log analysis pass; the per-session JSON debug log in
`Library/VoxrDebugLogs/` already contains the raw material. No runtime cost.

**Expected gain.** Directly attacks the documented OOV limitation ("developers must spell phonetic aliases by
hand") and the in-grammar substitution class, for the specific speaker in front of the headset. Bounded by the
fact that adding aliases enlarges the grammar and therefore the confusion set — a real trade-off the tooling
should surface, and one the existing A/B rig can measure.

**Cheapest validation.** Run the mining pass over the existing 10 field sessions and see whether the aliases it
proposes match the ones the maintainer already hand-wrote. If it rediscovers them, it works.

### P4 — Per-command few-shot verifier (largest, most speculative)

**What.** Ship a small embedding encoder (Rusci-style, triplet-trained, normalised features) plus per-user
prototypes built from 5–10 enrolment utterances per *high-risk* command. At §2.6/§2.7 the matched audio span is
scored against the prototype and the score gates the fire. Cioflan et al.'s user-projection layer is the
on-device-updatable variant: **23.7 k parameters, 1 MFLOP per training epoch**, error 30.1 %→24.3 % on 35-class
GSC.

**Cost.** A second model (a few hundred KB), an inference runtime, an enrolment UX, and a synchronisation
problem with Inspector-authored command sets. This is a feature, not a patch.

**Expected gain.** Rusci & Tuytelaars' 76 % accuracy at 5 % FAR in a 10-shot open-set setting is the honest
ceiling for a from-scratch few-shot classifier — worse than VOSK on its own. So the value is **not** as a
recogniser; it is as a *rejection* signal and a calibrated confidence source, applied only to commands the user
demonstrably gets wrong. Framed that way it is P2 with a learned feature space instead of a scalar baseline.

**Cheapest validation.** Do P2 first. If per-user scalar calibration already fixes the rejection problem, P4 is
unnecessary.

---

## What this area cannot fix

Front-end and adaptation work moves the *input distribution* closer to what the acoustic model was trained on,
and moves the *decision thresholds* closer to this user. Neither creates information the decoder never had.
Specifically, none of the above touches:

- **Bag-of-phrases word-order freedom.** `vosk_recognizer_new_grm` treats the grammar JSON as an unordered set
  of phrase entries, so any sequence decodes in any order. No amount of clean audio constrains that; it is a
  language-model/graph problem.
- **Uncalibrated posteriors.** Per-word `conf` will still be flat and word-dependent. P2 calibrates it
  *externally* by conditioning on a user; it does not make VOSK's internal posterior meaningful, and it does not
  give you N-best, lattices, acoustic scores or phone-level output, which VoXR currently does not request at all.
- **The absence of a garbage/silence class inside the grammar.** A VAD (D4) suppresses non-speech *before* the
  decoder; it does not give the decoder a "reject" path. Out-of-grammar *speech* still becomes `[unk]`, and
  wrong-mode speech still yields an unexplainable rejection.
- **OOV and abbreviations.** `cqb`, `pdc` are lexicon problems. Alias mining (P3) is a workaround, not a fix; a
  real fix means a lexicon/G2P change, i.e. a model-side change.
- **The grammar-rebuild audio gap.** P1 warms the *adaptation* state back up; the ~50 ms+ gap itself is caused by
  freeing and rebuilding the decoder and is an upstream `vosk-api` structural cost.
- **Single recognizer per process.** File-scope C++ state and no handle in the C ABI. Purely an ABI/refactor
  issue.
- **Endpointing splits.** `model.conf`'s `--endpoint.rule2/3/4.min-trailing-silence` (0.5 / 0.75 / 1.0 s) is
  where that behaviour lives — tunable, but a decoder-config topic, not a front-end one.
- **The model's floor.** `vosk-model-small-en-us-0.15` reports 9.85 % WER on LibriSpeech test-clean and 10.38 %
  on TEDLIUM. Perfect audio does not get below that. The only lever that moves it is a different or finetuned
  model (D11).
- **Measurement validity.** The corpus is TTS and the field set is 73 utterances from **one speaker**. Every
  recommendation in this report will be validated against a distribution that Quintas et al. show is
  distinguishable from real speech, and that Ma et al. show is optimistic for a Lombard-speaking user. That is
  the binding constraint on all of it, and D12 only partly relieves it.

---

## Ranked recommendations for VoXR

### 1. Replace the 48→16 kHz decimation filter (and normalise its DC gain)

**Rationale.** The current 15-tap filter attenuates the 4–7.6 kHz band by 6–37 dB against a model whose
features run to 7.6 kHz (`conf/mfcc.conf --high-freq=7600`), and adds +2.51 dB of unintended broadband gain. A
95-tap Kaiser design gives ≤0.36 dB droop to 7 kHz and ≥53.9 dB alias rejection for 1.52 MMAC/s — under 0.2 % of
one XR2 core, +1 ms group delay. It is a coefficient-table change in two files, it is the only item in this
report whose defect is *measured rather than hypothesised*, and its mechanism points straight at the two
best-documented substitution failures.

**Cheapest validating experiment.** The existing offline A/B rig on the 699-utterance corpus against the real
decoder: old coefficients vs new, same everything else. It already exists, it runs in WSL, and it produces the
per-utterance diff. Expect *all* scores to shift; the pass criterion is command-level accuracy, not score
parity. Then re-pin the documentation numbers with `DocCheck.cs` before anything ships.

### 2. Make the AGC speech-gated, and re-target it

**Rationale.** `kNoiseFloor = 1e-5` plus a 300 ms release means room noise gets up to +26 dB of gain inside
every pause — a mechanically sufficient cause of the documented cough/hum false triggers — and a −18 dBFS target
on a mean-absolute estimator drives syllable peaks into the tanh limiter. Both are design errors with textbook
fixes. Two implementation options: (a) minimal — freeze `current_gain_` when the VAD says no speech, raise
`kNoiseFloor` to a realistic value (~−60 dBFS), and drop the target to about −24 dBFS on the same estimator;
(b) principled — replace `agc.h` wholesale with WebRTC AGC2 (BSD-3, VAD-driven, with clipping predictor and
limiter), at a measured 4.2 µs per 10 ms frame for the whole APM at 16 kHz on NEON.

**Cheapest validating experiment.** Build a fixture set of *silence-then-command* and *silence-then-cough* WAVs
(2 s of real Quest room tone, then the event), replay through the push-audio bridge with (i) current AGC,
(ii) gain-frozen AGC, (iii) AGC off. Count false triggers and leading-word losses. This directly tests both A2
and A4, needs no device, and reuses existing infrastructure.

### 3. Gate the recognizer with a neural VAD

**Rationale.** Grammar mode has no silence output — this is stated as the root cause in `KNOWN_LIMITATIONS`.
The fix belongs in front of the decoder, not inside the parser. Silero VAD v5 is MIT, ~2 MB, and measured at
0.8 ms per 32 ms frame on Pixel 7 arm64 (0.3 ms with NNAPI) in a published write-up that pairs it with **Vosk
grammar mode specifically**. It also supplies the speech/no-speech signal recommendation 2 needs, so the two
share one component. Mandatory design constraint: 200–300 ms pre-roll on gate-open, or it will make leading-word
loss worse.

**Cheapest validating experiment.** Same silence-then-event fixture set as recommendation 2, plus the 73 field
utterances, replayed with the VAD gate in and out. Measure false triggers *and* first-word retention — both, or
the experiment is worthless.

### 4. Spend one device session measuring the platform instead of guessing

**Rationale.** VoXR currently does not know what Horizon OS applies to `VOICE_RECOGNITION`, whether
`UNPROCESSED` exists on Quest, whether `AcousticEchoCanceler`/`NoiseSuppressor`/`AutomaticGainControl` are
available or already enabled on its capture session, what the mic's real noise floor and gain are, or what SIR
game audio reaches the mics at. AOSP says all of this is device-specific; Meta publishes none of it. Every other
recommendation in this report is sized against assumptions that this session would replace with facts.

**Cheapest validating experiment.** One human-only Quest session: `adb shell cat /vendor/etc/audio_effects.xml`;
a scratch build logging `isAvailable()`/`getEnabled()` for the three effects against the AudioRecord session id
and `AudioManager.getProperty(PROPERTY_SUPPORT_AUDIO_SOURCE_UNPROCESSED)`; and raw 48 kHz captures of the same
10 phrases through sources `MIC`(1), `VOICE_RECOGNITION`(6), `VOICE_COMMUNICATION`(7) and `UNPROCESSED`(9), plus
one capture of room tone and one of game audio at normal volume with no speech. That last file alone answers
whether AEC is worth building.

### 5. Build the adverse-condition corpus before building anything to survive adverse conditions

**Rationale.** Raju et al. report 30–45 % relative false-reject reduction from playback-interference
augmentation; Ma et al. show that evaluating on plain speech mixed with noise *overestimates* audio-only
performance versus real Lombard speech. VoXR's whole measurement base is calm TTS from one speaker. Until
there is a corpus with game-audio bleed, room noise, and human exerted speech, no result about front-end
robustness is trustworthy — including every result recommendations 1–3 will produce.

**Cheapest validating experiment.** Extend the existing WAV-replay fixture generator: mix real game audio at
+20 / +10 / +5 / 0 dB SIR and MUSAN noise (CC-licensed) into the current corpus, and record ~20 human Lombard/
exerted takes of the top commands on-device. Replay all of it through the push-audio bridge and publish the
per-condition accuracy curve as the new baseline. Everything else in this report should then be re-scored
against it.

**Explicitly not recommended now:** dropping GTCRN, RNNoise, DeepFilterNet or DTLN in front of VOSK as a
general-purpose denoiser. Chondhekar et al. (2025) found enhancement degraded WER in 40 of 40 configurations
against back-ends that had not been trained on enhanced audio; VoXR cannot retrain its back-end. Revisit only if
recommendation 5's corpus reveals a genuinely low-SNR regime that occurs in the field, and then only with an
A/B that scores commands, not audio quality.

---

## Sources

Fetched and verified unless marked.

**Repository / model artefacts (this codebase)**
1. `NativeBridge~/src/agc.h`, `downsampler.h`, `audio_capture_audiorecord.cpp`, `vosk_bridge.cpp`;
   `Runtime/Dsp/Agc.cs`, `Runtime/Dsp/Downsampler.cs`; `KNOWN_LIMITATIONS.md`.
2. `NativeBridge~/vendor/vosk-model-small-en-us-0.15/` — `conf/mfcc.conf`, `conf/model.conf`, `ivector/*`,
   `README` (accuracy 10.38 TEDLIUM / 9.85 LibriSpeech test-clean; 0.11×RT desktop; 0.15 s right context).

**Speech enhancement and its effect on ASR**
3. Chondhekar, Murukuri, Vasani, Goyal, Badami, Rana, SN, Pandia, Katiyar, Jagadeesh, Gulati — *When De-noising
   Hurts: A Systematic Study of Speech Enhancement Effects on Modern Medical ASR Systems*, arXiv:2512.17562
   (2025). <https://arxiv.org/abs/2512.17562>
4. Yang, Pandey, Wang — *Towards Decoupling Frontend Enhancement and Backend Recognition in Monaural Robust
   ASR*, arXiv:2403.06387 (2024). <https://arxiv.org/abs/2403.06387>
5. Du, Zhang, Han — *Investigation of Monaural Front-End Processing for Robust ASR without Retraining or
   Joint-Training*, arXiv:1810.09067 (2018). <https://arxiv.org/abs/1810.09067>
6. Zhao, Wang, Qian — *Lightweight Front-end Enhancement for Robust ASR via Frame Resampling and Sub-Band
   Pruning*, Interspeech 2025, arXiv:2509.21833. <https://arxiv.org/abs/2509.21833>
7. Rong, Sun, Zhang, Hu, Zhu, Lu — *GTCRN: A Speech Enhancement Model Requiring Ultralow Computational
   Resources*, ICASSP 2024. <https://sigport.org/documents/gtcrn-speech-enhancement-model-requiring-ultralow-computational-resources>
   · code <https://github.com/Xiaobin-Rong/gtcrn>
8. Schröter, Escalante-B., Rosenkranz, Maier — *DeepFilterNet2: Towards Real-Time Speech Enhancement on
   Embedded Devices for Full-Band Audio*, IWAENC 2022, arXiv:2205.05474. <https://arxiv.org/abs/2205.05474>
9. Schröter, Rosenkranz, Escalante-B., Maier — *DeepFilterNet: Perceptually Motivated Real-Time Speech
   Enhancement*, Interspeech 2023 demo, arXiv:2305.08227. <https://arxiv.org/abs/2305.08227> · repo
   <https://github.com/Rikorose/DeepFilterNet>
10. Valin — *A Hybrid DSP/Deep Learning Approach to Real-Time Full-Band Speech Enhancement*, MMSP 2018,
    arXiv:1709.08243. <https://arxiv.org/abs/1709.08243> · <https://jmvalin.ca/demo/rnnoise/>
11. Westhausen, Meyer — *Dual-Signal Transformation LSTM Network for Real-Time Noise Suppression*, Interspeech
    2020. <https://github.com/breizhn/DTLN>
12. Braun, Gamper, Reddy, Tashev — *Towards efficient models for real-time deep noise suppression*,
    arXiv:2101.09249 (2021). <https://arxiv.org/abs/2101.09249>

**Echo cancellation, VAD, AGC, platform audio**
13. WebRTC — *Audio Processing Module (APM)* design doc.
    <https://webrtc.googlesource.com/src//+/7c793a7dbe548735fe9e1d107e00d17937202f47/modules/audio_processing/g3doc/audio_processing_module.md>
14. Sonora — pure-Rust WebRTC APM port (AEC3 / NS / AGC2), BSD-3, NEON, benchmarks.
    <https://github.com/dignifiedquire/sonora>
15. Zhao, Wang — *Why Not Put a Microphone Near the Loudspeaker? A New Paradigm for Acoustic Echo Cancellation*,
    arXiv:2511.03244 (2025). <https://arxiv.org/abs/2511.03244>
16. Silero VAD. <https://github.com/snakers4/silero-vad>
17. "On-Device Wake Word Detection with Vosk Grammar Mode and Silero VAD v5" (Android/arm64 engineering
    write-up). <https://zenn.dev/diced/articles/vosk-silero-vad-wakeword-android>
18. AOSP — *Configure preprocessing effects*. <https://source.android.com/docs/core/audio/implement-pre-processing>
19. Android — `MediaRecorder.AudioSource` reference.
    <https://developer.android.com/reference/android/media/MediaRecorder.AudioSource>
20. google/oboe issue #951 — AcousticEchoCanceler/NoiseSuppressor not engaging.
    <https://github.com/google/oboe/issues/951>
21. Unity — `MonoBehaviour.OnAudioFilterRead`.
    <https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnAudioFilterRead.html>
22. Meta — *Voice SDK Overview* (Wit.ai cloud processing).
    <https://developers.meta.com/horizon/documentation/unity/voice-sdk-overview/>
23. Meta — *Voice Technology* design guide. <https://developers.meta.com/horizon/design/voice-technology/>
24. Meta — *Mic Switcher*. <https://developers.meta.com/horizon/documentation/unity/ps-mic-switcher/>
25. Meta — *Quest v64 update* (experimental USB-C external mic support).
    <https://www.meta.com/blog/meta-quest-v64-update-passthrough-improvements-external-microphones/>

**Resampling and bandwidth**
26. Pohlhausen, Bitzer — *Revisiting the Privacy of Low-Frequency Speech Signals: Exploring Resampling Methods,
    Evaluation Scenarios, and Speaker Characteristics*, arXiv:2508.02483 (2025).
    <https://arxiv.org/html/2508.02483v1>
27. Kong, Mullangi, Kokkinakis — *Classification of Fricative Consonants for Speech Enhancement in Hearing
    Devices*, PLOS ONE 2014. <https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0095001>
28. libsoxr project page (LGPL-2.1). <https://sourceforge.net/projects/soxr/>
29. Audio sample-rate converter comparison (Speex vs Soxr timings). <https://lastique.github.io/src_test/>
30. xiph/speexdsp. <https://github.com/xiph/speexdsp> — component list and licence text `UNVERIFIED`.

**Adaptation and personalization**
31. Kaldi — *Online decoding in Kaldi* (nnet3 feature pipeline, i-vectors).
    <https://kaldi-asr.org/doc/online_decoding.html>
32. alphacep/vosk-api — `src/recognizer.cc` (per-recognizer `OnlineNnet2FeaturePipeline`; `CleanUp()` at
    `frame_offset_ > 20000`; `SetGrm()` reinitialises decoder and feature pipeline).
    <https://raw.githubusercontent.com/alphacep/vosk-api/master/src/recognizer.cc>
33. Alpha Cephei — *VOSK models* (sizes, WER, licences). <https://alphacephei.com/vosk/models>
34. Alpha Cephei — *Model adaptation for VOSK* (~1 h for acoustic finetuning; no speaker adaptation offered).
    <https://alphacephei.com/vosk/adaptation>
35. Lin, Li, Lee — *Listen, Adapt, Better WER: Source-free Single-utterance Test-time Adaptation for ASR*,
    Interspeech 2022, arXiv:2203.14222. <https://arxiv.org/abs/2203.14222>
36. Lin, Huang, Lee — *Continual Test-time Adaptation for End-to-end Speech Recognition on Noisy Speech*
    (DSUTA), EMNLP 2024, arXiv:2406.11064. <https://arxiv.org/abs/2406.11064>
37. van Dalen, Zhang, Parcollet, Bhattacharya — *Robust Unsupervised Adaptation of a Speech Recogniser Using
    Entropy Minimisation and Speaker Codes*, Interspeech 2025, arXiv:2506.10653.
    <https://arxiv.org/abs/2506.10653>
38. Rusci, Tuytelaars — *Few-Shot Open-Set Learning for On-Device Customization of KeyWord Spotting Systems*,
    Interspeech 2023, arXiv:2306.02161. <https://arxiv.org/abs/2306.02161>
39. Cioflan, Cavigelli, Benini — *Boosting keyword spotting through on-device learnable user speech
    characteristics*, tinyML Research Symposium 2024, arXiv:2403.07802. <https://arxiv.org/abs/2403.07802>
40. Saon, Soltau, Nahamoo, Picheny — *Speaker adaptation of neural network acoustic models using i-vectors*,
    ASRU 2013 — **`UNVERIFIED`** (the ~10 % relative WERR on Switchboard 300 h figure was not confirmed against
    the primary source).

**Augmentation**
41. Ogun, Colotte, Vincent — *An Exhaustive Evaluation of TTS- and VC-based Data Augmentation for ASR*,
    arXiv:2503.08954 (2025). <https://arxiv.org/abs/2503.08954>
42. Quintas, Ferrané, Pellegrini — *Enhancing Synthetic Training Data for Speech Commands: From ASR-Based
    Filtering to Domain Adaptation in SSL Latent Space*, arXiv:2409.12745 (2024).
    <https://arxiv.org/abs/2409.12745>
43. Raju, Panchapagesan, Liu, Mandal, Strom — *Data Augmentation for Robust Keyword Spotting under Playback
    Interference*, arXiv:1808.00563 (2018). <https://arxiv.org/abs/1808.00563>
44. Ma, Petridis, Pantic — *Investigating the Lombard Effect Influence on End-to-End Audio-Visual Speech
    Recognition*, Interspeech 2019, arXiv:1906.02112. <https://arxiv.org/abs/1906.02112>
45. Snyder, Chen, Povey — *MUSAN: A Music, Speech, and Noise Corpus*, arXiv:1510.08484 (2015).
    <https://arxiv.org/abs/1510.08484>
46. Park, Chan, Zhang, Chiu, Zoph, Cubuk, Le — *SpecAugment: A Simple Data Augmentation Method for Automatic
    Speech Recognition*, Interspeech 2019, arXiv:1904.08779. <https://arxiv.org/abs/1904.08779>

**Not found / unpublished**
- No official Meta specification for Quest 2/3/Pro microphone count, frequency response, noise floor, gain, or
  speaker-to-microphone acoustic coupling could be located. Community reports of Quest 3 mic muffling and
  aggressive noise suppression exist but are forum hearsay and are treated here as `UNVERIFIED`.
- No published Meta Reality Labs paper specifically on AEC for AR/VR headset near-field speaker–mic geometry
  was found; the closest is source 15, which is generic near-loudspeaker-reference AEC.
