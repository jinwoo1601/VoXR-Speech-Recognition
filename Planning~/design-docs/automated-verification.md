# Design: Automated Verification of On-Device Behavior

- **Status:** **LOCKED** — G1 (human sign-off, 2026-07-30), DR-1…DR-4 ratified as proposed; **re-locked at G1 (human sign-off, 2026-08-11) with Amendment A1, which drops Tier D.** Delivered scope is Tiers A/B/C; backlog closed; design complete.
- **Branch:** `design-automated-verification`
- **Date:** 2026-07-30 (amended 2026-08-11)
- **Scope agreed at G0:** move the currently human-only verification (in-headset Quest testing: mic capture, lifecycle/pause behavior, native-bridge changes) to automated checks Claude can run, via the four-tier architecture below.

---

## 0. Amendment A1 — Tier D dropped (2026-08-11)

**Decision:** Tier D (the automated headset rig, §8) is **dropped**. The design's delivered scope is **Tiers A, B and C**. Tiers A–C are built, G2-accepted and merged; nothing further is planned against this design.

**What this changes.** §8 (Tier D), DR-3 (`device-check` binding), and backlog row 3 (`feat-device-rig`) are struck — retained below as historical record, each carrying a DROPPED banner. Every other Tier D reference in this doc (§3 goals 3 and 5, §4's table row and backstop paragraph, §5's and §6.3's and §7.1's "blind to" notes) should now be read as: **that blind spot is permanent and stays human-verified.** The prose is left as written because it correctly records what was true when Tiers A–C were designed and built.

**How far Tier D got.** `feat-device-rig` reached Phase 4 (rig scaffolding, host provisioning, adb prep/runbook, on-device push-audio suite, mic liveness) on a local-only branch — never pushed, no PR, never merged. It is archived at tag **`archive/tier-d`** (`ff3ea62`) and recoverable if Tier D is ever revisited; its requirement and architecture docs stay in `Planning~/features/device-rig/`, marked abandoned. §8.3.3 (acoustic loopback + latency measurement) and §8.3.4 (lifecycle delivery) were never built.

**Residue this creates.**

- **The arm64 binary, JNI AudioRecord, Quest audio routing, mic hardware, and Android's actual `OnApplicationPause` delivery have no automated coverage** and revert to the human-only verification paragraph in the project CLAUDE.md — which was never rewritten, so no binding change is needed. Tier C's arm64 *compile* check remains the only automated statement about the shipped binary; it proves the `.so` builds, not that it works.
- **`vosk_bridge_get_input_level()` is now an orphan.** It shipped on `main` with Tier C (PR #51) solely as §8.3.2's consumer (§7.1 says so outright). It is left in place deliberately: removing it is another bridge ABI change plus a rebuilt-and-committed `.so`, for an unused, harmless read-only export.
- **`KNOWN_LIMITATIONS.md` timing figures stay anecdotal.** §12's commitment to replace them with Tier D's measured loopback latency is withdrawn (goal 5, §3).
- **Tier A's blind spot stays open:** whether Android actually delivers `OnApplicationPause` on doff/system-menu is human-verified, per §5.

**Not affected.** Tiers A, B and C and everything they shipped stand unchanged, including the fixture corpus (§6.2), push mode (§7.1), the backend-selection refactor and desktop preset (§7.2), and the WSL harness binding (§7.3) ratified at Tier C's G2.

## 1. Problem

The project's verification bindings mark three classes of change **human-only**: mic capture, MonoBehaviour lifecycle/pause behavior, and any native-bridge change. Every such change ends with "deferred — needs in-headset verification", which means:

- Native-bridge changes ship with **zero automated test coverage** (`NativeBridge~` has no tests at all).
- Acoustic regressions (DSP, AGC, grammar, Vosk integration) are only caught if a human happens to speak the right phrase in the headset.
- The timing figures in `KNOWN_LIMITATIONS.md` (grammar-swap audio gap, final-result latency, `bufferWindow` guidance) are anecdotes from manual test matrices, not measurements that re-run.
- Every issue-lane fix touching these areas stalls on human availability.

The goal is to shrink the human-only residue to what is genuinely human (device provisioning, subjective feel) and make everything else a check Claude can run from WSL.

## 2. Verified current state

Recon inventory from the mapping pass (2026-07-30), which this design builds on:

**Test seams (all text-level, downstream of Vosk):**
- `VoxrSpeechRecogniser.InjectResult` (`Runtime/VoxrSpeechRecogniser.cs:308`), `VoxrCommandRecogniser.InjectText` (`Runtime/Commands/VoxrCommandRecogniser.cs:219`), batch runner in `Runtime/Testing/`. **No PCM/audio injection seam exists anywhere.**

**Android audio path (100 % native):**
- JNI AudioRecord capture (`NativeBridge~/src/audio_capture_audiorecord.cpp`) → ring buffer → recognition thread (`NativeBridge~/src/vosk_bridge.cpp:62-136`). The recognition loop's sole audio source is `g_ring_buffer.Read` at `vosk_bridge.cpp:79`.
- The C ABI (`NativeBridge~/src/vosk_bridge.h:22-39`, 11 functions) accepts **no audio input**.
- Two gates couple recognition to live capture: the `g_audio_capture.HasError()` check in the loop (`vosk_bridge.cpp:70`) and `g_audio_capture.Start(&g_ring_buffer)` in `vosk_bridge_start` (`vosk_bridge.cpp:223`).
- The bridge is nearly portable: five Android-isms — `jni.h` in the AudioRecord backend, `android/log.h` in `logging.h`, the liblog link (`CMakeLists.txt:49`), the `VOSK_LIB_DIR` default (`CMakeLists.txt:39`), and the hardcoded backend include `#include "audio_capture_audiorecord.h"` at `vosk_bridge.cpp:7`. Two capture backends exist (audiorecord, aaudio) declaring the same `class AudioCapture`, but selection is **manual** — `CMakeLists.txt:9-12` hardcodes the audiorecord source and `vosk_bridge.cpp:7` hardcodes its header; no CMake selection mechanism exists today (§7.2 creates one). `BridgeNative.cs` has no platform guards — a desktop `vosk-bridge` library would drive the real C# drain loop unchanged.

**Windows Editor path (separate C# backend):**
- `Runtime/EditorMicBackend.cs` (`#if UNITY_EDITOR_WIN`): monolithic `Tick()` (`:291-367`) couples Microphone capture + DSP + `accept_waveform_s`; no `ProcessChunk` seam; `_recognizer` is private.
- `Planning~/v3-expansion-plan.md:231-309` ("v3.4 Audio Capture & Replay") already specs `EditorMicBackend.StartPlayback(float[])` + audio test-case assets — designed, never built. This design reuses it (§6).

**Lifecycle:**
- `OnApplicationPause` is the **only** uncovered lifecycle hook (`Runtime/VoxrPushToTalkController.cs:133-147`): on pause it stops recognition if `_wantRecognising`; on resume it restarts if wanted and not already recognising. Testable in plain PlayMode via `SendMessage`.
- `OnApplicationFocus` is implemented **nowhere** — the project CLAUDE.md mentions it in error (binding text to be corrected when features land, §12).

**Fixtures / CI:**
- No audio fixtures exist in `Tests~/`. Native-bridge test coverage is zero. `release.yml` strips `Tests~` + `NativeBridge~` from the published package — nothing in this design ships to consumers.

**Hardware constraints (KNOWN_LIMITATIONS.md:346-390):**
- AAudio input delivers silence on Quest 3 — capture must stay on Java AudioRecord via JNI.
- `vosk_recognizer_accept_waveform_f` is broken on the prebuilt arm64 `libvosk.so` — **feed Vosk int16 only** (`accept_waveform_s`), as the bridge already does.
- Quest 3 mic peaks sit at **0.04–0.4** on [-1, 1]; AGC (target `micGainTargetDb`, default −18 dB) compensates before int16 conversion.

**Tooling facts (web-verified):**
- Unity Test Framework supports `-testPlatform Android` from the CLI: builds a test APK, deploys to the connected adb device, runs on-device, and streams the results XML back to the host (`-playerHeartbeatTimeout` to tolerate slow imports).
- Meta Scriptable Testing Services (Quest OS v44+): `adb shell am broadcast -a com.oculus.vrpowermanager.prox_close` fakes "headset worn" (device stays awake); commands exist to disable auto-sleep, guardian, and system dialogs. There is **no OS-level mic injection** — hence the audio seams below.
- The new Unity CLI (docs.unity.com) is only an editor installer — the existing `Unity.exe -batchmode -runTests` bindings remain the test vehicle.

## 3. Goals and non-goals

**Goals**
1. Automated regression coverage for the acoustic pipeline (DSP → Vosk → parser) using real Vosk, runnable per change.
2. Automated coverage of native-bridge logic that Claude can run directly from WSL, with no Unity and no device.
3. An automated on-device rig that validates the arm64 binary, real mic capture, and lifecycle delivery on a connected Quest — replacing the routine human playtest.
4. Direct test coverage of the `OnApplicationPause` state machine.
5. Turn the anecdotal timing figures in `KNOWN_LIMITATIONS.md` into measured, re-runnable numbers.

**Non-goals**
- No changes to shipped runtime behavior beyond the minimal seams specified below: a bridge push-audio mode (§7.1), a read-only bridge input-level getter (§7.1), an editor playback mode (§6.1), and — only if the pause tests prove to need one — a minimal internal seam on the recogniser (§5). The backend-selection refactor inside the bridge build (§7.2) changes how sources are selected, not what the Android build does. No new user-facing features.
- No CI/cloud device farm — the rig targets the local, cable-connected Quest.
- No replacement of the Windows editor mic path with the native bridge (see DR-4).
- No elimination of *all* human verification — §10 states the honest residue.

## 4. Architecture overview

Four tiers, ordered by increasing fidelity and decreasing convenience. Each tier states what it verifies and — as importantly — what it does **not**, so no tier is mistaken for the one above it.

| Tier | What runs | Where | Verifies | Blind to |
|------|-----------|-------|----------|----------|
| A | PlayMode tests on the pause state machine | Host Unity project (existing bindings) | `OnApplicationPause` C# logic | Whether Android actually delivers the callback |
| B | WAV replay through `EditorMicBackend` | Windows editor PlayMode (existing bindings) | Full editor pipeline: Downsampler → AGC → real Vosk → parser | Android capture path, arm64 binary, the native bridge itself |
| C | WSL-native WAV→transcript harness on a desktop build of the bridge | WSL directly (Claude-runnable, no Unity) | Bridge logic: ring buffer, downsampler, AGC, int16 conversion, result queue, grammar handling | The **arm64 binary**, JNI AudioRecord, Quest audio routing |
| ~~D~~ **DROPPED (A1)** | ~~UTF Android run + adb rig on a connected Quest~~ | ~~Host project → device~~ | ~~arm64 bridge + real libvosk.so, mic liveness, acoustic loopback + latency, lifecycle delivery~~ | ~~Subjective feel; anything needing a human voice or head~~ |

Tier D is the backstop that keeps Tiers B/C honest: B and C prove logic, D proves the shipped binary on the shipped hardware. — **A1 (2026-08-11): Tier D is dropped, so this backstop does not exist. B and C prove logic; nothing automated proves the shipped binary on the shipped hardware, which stays human-verified (§0, §10).**

A shared **fixture corpus** (§6.2) feeds B, C, and D: checked-in WAV files at pre-DSP, Quest-realistic amplitude, so all three tiers exercise the same utterances.

## 5. Tier A — lifecycle PlayMode tests

**Design.** PlayMode tests in `Tests~/Runtime/` driving `VoxrPushToTalkController` with `SendMessage("OnApplicationPause", true/false)`, wired through the existing internal setters (`SpeechRecogniser`, `CommandRecogniser`, `VoxrPushToTalkController.cs:157-158`). Cases:

- Pause while recognising (continuous mode) → recognition stops.
- Resume with `_wantRecognising` set → recognition restarts.
- Resume when already recognising → no double-start.
- Pause while *not* recognising (PTT idle) → no stop call / no state corruption.
- Interaction with the `Update()` permission-race reconciliation (`:149-155`): resume ordering doesn't fight the reconciler.

**Observation mechanism (decided at issue time).** The setters are typed to the concrete `VoxrSpeechRecogniser`, whose `IsRecognising`/`StartRecognition`/`StopRecognition` are non-virtual (`Runtime/VoxrSpeechRecogniser.cs:75,168,238`) — there is no seam for intercepting calls with a test double today. The issue picks between: (a) exercising a real, initialised recogniser and asserting `IsRecognising` transitions — on the Windows test host that routes through `EditorMicBackend`, needing a model plus a working `Microphone` device in batchmode, heavier than it sounds; or (b) adding a minimal internal seam (internal virtuals or an internal control interface) so the controller is testable in isolation. Test-only is the aspiration; a minimal internal seam is acceptable and still fits the issue lane (DR-1).

**Verifies / blind to.** Verifies the C# state machine exactly as written. Blind to whether Android/Quest actually invokes `OnApplicationPause` on HMD doff or system-menu entry — that delivery is Tier D's lifecycle test (§8.3.4).

## 6. Tier B — editor WAV replay + fixture corpus

Builds the already-designed v3.4 seam (`Planning~/v3-expansion-plan.md:231-309`), narrowed to what automated verification needs.

### 6.1 Playback seam

Carried over from the v3.4 spec:

- A playback mode on `EditorMicBackend` (v3.4 names the seam `PlaybackFrom(float[] samples, int sampleRate)` / `StartPlayback`): `Tick()` reads successive chunks from a file-backed buffer instead of `AudioClip.GetData`, advancing by the same chunk size per tick. Same events fire (`OnPartialResult`, `OnFinalResult`, `OnResult`, `OnCommandRecognised`); results remain compatible with the batch runner.
- v3.4's locked decisions: **48 kHz mono 16-bit WAV only** (Downsampler is hardcoded 48→16 kHz, decimation 3; reject other rates with a clear error), **pre-DSP input** (replay traverses the full pipeline identically to live audio), **WAV, not a custom format**.
- `VoskAudioTestCase` / `VoskAudioTestSuiteAsset` (v3.4's replay assets) as the test-data containers.

This design adds (not in v3.4, which substitutes the audio source in place inside `Tick()`):

- Extracting an explicit `ProcessChunk(float[] samples, int count)` step from `Tick()` so the capture source (Microphone/AudioClip polling vs. playback buffer) and the processing pipeline (Downsampler → AGC → `accept_waveform_s` → result queue) are cleanly separable. This testability refactor is this design's own addition, and the piece that makes the seam durable.
- Extending v3.4's locked "no runtime overhead when not recording" to the new mode: zero overhead when not in playback.

**Scope delta from v3.4:** the *Record* half (Debug-Window record button, `VoskAudioRecorder`) is **deferred** — it is a human capture tool, not needed for automated verification. It becomes relevant only if human-recorded fixtures are added later (DR-2), and can be built then.

### 6.2 Fixture corpus (shared by B, C, D)

- **Location:** `Tests~/Fixtures/audio/` (stripped by `release.yml` along with the rest of `Tests~`).
- **Format:** 48 kHz mono 16-bit WAV, pre-DSP, amplitude-scaled so peaks land in **0.04–0.4** — the measured Quest 3 mic range. Fixtures at full-scale amplitude would bypass the AGC regime the device actually operates in and would not reproduce device behavior.
- **Generation:** TTS via `piper` in WSL → resample to 48 kHz → amplitude-scale. A checked-in generation script (`Tests~/Fixtures/generate.sh` + phrase manifest) makes the corpus reproducible and extensible; generated WAVs are committed so tests never depend on piper being installed.
- **Content:** utterances covering the demo command grammar — clean commands, slot variants, homophone traps ("to"/"two"), leading filler (`[unk]` handling), a split command with mid-pause (buffer-window behavior), and a silence/noise negative.
- **Provenance convention:** `tts/` and `human/` subfolders. v1 is TTS-only (DR-2); human recordings can be added any time without design change.

### 6.3 Tests

PlayMode-in-editor tests (Windows editor, real `libvosk.dll`, real model — the environment the existing verification bindings already run): load each fixture, `StartPlayback`, await final result, assert expected transcript/intent per the test-case asset. Runs under the existing host-project test procedure; no new binding mechanics.

**Verifies / blind to.** Verifies the full editor pipeline against real Vosk acoustics per change — this is the tier that catches DSP/AGC/grammar regressions cheaply. Blind to the native bridge (separate C# path), the Android capture chain, and the arm64 binary. Also honest: TTS voices are cleaner than human speech, so Tier B is a *regression* detector (same input, changed output), not an absolute recognition-quality benchmark.

## 7. Tier C — desktop bridge push-audio harness

The tier that makes native-bridge changes verifiable by Claude directly in WSL, with no Unity and no device.

### 7.1 Bridge push mode (all platforms)

New C ABI additions in `vosk_bridge.h`:

- `int vosk_bridge_push_audio(const float* samples, uint32_t count)` — writes pre-DSP 48 kHz float samples into `g_ring_buffer`, exactly what a capture backend writes. Returns samples written / error code so a caller can pace against overflow.
- A **push-mode start** (`vosk_bridge_start_push()`, or a mode flag on start) that launches the recognition thread **without** starting the capture backend — it skips `g_audio_capture.Start()` (`vosk_bridge.cpp:223`) and nothing else. The `HasError()` check at the top of the loop (`vosk_bridge.cpp:70`) stays as-is: it is benign in push mode, since the error flag is only ever set by a running capture thread (`audio_capture_audiorecord.h:23-33`) and the desktop stub always reports false. The recognition loop is genuinely untouched — it keeps draining `g_ring_buffer.Read` at `:79`, indifferent to who filled it. (`vosk_bridge_stop` calls `g_audio_capture.Stop()` on a capture that never started; implementation verifies this is a safe no-op.)
- `float vosk_bridge_get_input_level()` — a **read-only input-level getter**: rolling RMS of the most recent pre-DSP chunks, computed in the recognition loop and published through an atomic. Not needed by the Tier C harness itself, but it is a bridge-ABI change, so it lands with this feature; its consumer is Tier D's mic-liveness check (§8.3.2).

Push mode exists on the **Android build too**: Tier D's on-device bridge-integration test (§8.3.1) reuses exactly this seam. Capture and push are mutually exclusive by construction (push mode never starts capture), so mic audio can't mix with pushed fixtures.

> **Amendment (ratified by human in-conversation ruling, 2026-07-30, during Tier C implementation):** the claim above that the `HasError()` check "is benign in push mode … stays as-is" is factually wrong on Android: `error_occurred_` is **sticky** — set by capture errors (`audio_capture_audiorecord.cpp:164,187,193`) and cleared only in `AudioCapture::Start()` (`:46`), which push mode skips — so a prior errored capture session would kill the next push session on its first loop iteration. **Ruled fix:** the check is gated to capture mode (`if (!g_push_mode && g_audio_capture.HasError())`); the recognition loop is otherwise untouched. Options considered: this gate (chosen); a `ClearError()` on the capture class called from push-start (rejected: touches the capture backends' API and leaves a purposeless per-iteration check in push mode); building to the design letter and deferring to Tier D (rejected: knowingly ships a latent failure in the seam Tier D consumes).

### 7.2 Desktop build preset

A desktop CMake preset for Linux x86_64, handling the four Android-isms:

- **Backend selection (small bridge refactor — the mechanism does not exist yet):** today the audiorecord backend is hardcoded twice — in the source list (`CMakeLists.txt:9-12`) and as `#include "audio_capture_audiorecord.h"` in `vosk_bridge.cpp:7`. Introduce a backend-neutral `audio_capture.h` that forwards to the selected backend's header, plus a CMake option (`VOSK_BRIDGE_CAPTURE=audiorecord|aaudio|stub`, default `audiorecord`) that picks both the compiled source and the forwarded header. The default Android build stays behaviorally identical.
- **Capture backend for desktop:** a stub `audio_capture_stub.cpp` (`Start` = no-op success, `HasError()` always false), selected by the preset — so even non-push code paths are safe on desktop.
- **Logging:** `logging.h` falls back to `printf` when `__ANDROID__` is absent; drop the liblog link.
- **libvosk:** preset points `VOSK_LIB_DIR` at a local Linux x86_64 `libvosk.so` (Alphacephei prebuilt, downloaded once into a gitignored `NativeBridge~/vendor/` — not checked in, provisioning documented in the harness README alongside the small-model download).
- With `stub` selected, the AudioRecord source and its `jni.h`-bearing header are neither compiled nor included — no JNI anywhere in the preset.

### 7.3 WSL harness

A small CLI (`NativeBridge~/harness/`) linking the desktop bridge: loads fixture WAVs (int16 WAV → float), `init` → `start_push` → push chunks → drain results → print final transcripts as JSON; exit code reflects expected-vs-actual per a manifest. Claude runs it directly:

```
harness --model <path> --fixtures Tests~/Fixtures/audio/tts --manifest expectations.json
```

**Verifies / blind to.** Verifies bridge *logic* — ring buffer, downsampler, AGC, float→int16 conversion, result queue, grammar handling, error paths — compiled for x86_64 against a real Linux libvosk. **It does not validate the arm64 binary, JNI AudioRecord, or Quest audio routing.** Tier D remains the backstop for the shipped binary; this tier's value is fast iteration and logic regression on every `NativeBridge~` change.

## 8. Tier D — automated headset rig

> **DROPPED by Amendment A1 (2026-08-11).** This section is retained as historical record only — nothing in it is a commitment. The routine human in-headset pass is **not** replaced; it remains the verification path for mic capture, lifecycle behavior, and native-bridge changes. Partial implementation is archived at tag `archive/tier-d`. See §0.

Replaces the routine human in-headset pass with an adb-driven run Claude executes against a cable-connected Quest.

### 8.1 Device prep script

An idempotent script run before each session (`adb shell` commands via the platform-tools binding):

- `am broadcast -a com.oculus.vrpowermanager.prox_close` — fake "headset worn" so the device runs tests without a head in it (Meta Scriptable Testing Services, Quest OS v44+).
- Disable auto-sleep and guardian; dismiss/disable system dialogs (the testing-services command set).
- `pm grant <pkg> android.permission.RECORD_AUDIO` after install, plus a watcher so a permission prompt never stalls a run.

### 8.2 Test execution

Unity Test Framework from the host project with `-testPlatform Android`: UTF builds the test APK, deploys to the connected device over adb, runs on-device, and streams the results XML back to the host (`-playerHeartbeatTimeout` sized for first-install imports). Same green criterion as the existing bindings: NUnit XML with `failed="0"`.

### 8.3 On-device test content

1. **Push-audio bridge integration.** Fixtures shipped via StreamingAssets, pushed through Tier C's push mode (§7.1) into the **real arm64 bridge + libvosk.so** on-device; assert transcripts/intents. This is the arm64 validation Tier C cannot provide, minus only the mic hardware.
2. **Mic liveness.** Start real capture (`vosk_bridge_start`), poll the read-only input-level getter (`vosk_bridge_get_input_level()`, §7.1) for a few seconds, assert RMS above the noise floor within a timeout. Validates the AudioRecord JNI chain, mic routing, and permission state end-to-end. (Ambient-noise dependent — asserts "not silent", not "correct audio".)
3. **Acoustic loopback.** Play fixture WAVs through the Quest speakers; the real mic picks them up; assert commands recognized and **measure** end-to-end latency. This turns the anecdotal figures in `KNOWN_LIMITATIONS.md:224-292` (≈50 ms grammar-swap gap, 0.5–1.0 s final-result latency, `bufferWindow` 2.0 s guidance) into re-runnable measurements. Design for flakiness honestly: speaker→mic differs from mouth→mic (level, spectrum, room), so recognition asserts use generous thresholds and a calibration pass, while latency is *recorded and trended*, hard-failing only on gross regression.
4. **Lifecycle delivery.** Close Tier A's blind spot: drive `prox_open`/`prox_close` broadcasts (doff/don) and verify Android actually delivers `OnApplicationPause` — the on-device test logs a marker on pause/resume receipt, and the host script confirms it via a logcat handshake (`adb logcat -s`) plus recognition-state assertions after resume.

### 8.4 Residue

Even with Tier D green, §10's human items remain — chiefly one-time device provisioning and subjective feel.

## 9. Decision records

Resolutions locked at G1 (2026-07-30), as proposed.

### DR-1 — Tier A routes through the issue lane

**Fork:** Tier A is a trivial test-only addition (no production change). Carry it as a backlog feature, or route it through the issue lane?

**Decision (locked):** **Issue lane.** It matches the lane's definition ("bug fixes and small maintenance"), is test-only or at most a minimal internal seam (§5), needs no requirement/architecture docs, and has no dependency on any other tier. It appears in §11 for completeness, flagged as issue-lane, and can be filed as a GitHub issue immediately after G1.

### DR-2 — Fixture corpus is TTS-only in v1

**Fork:** TTS-generated fixtures only (fully automatable), or TTS plus a set of human-recorded WAVs from the user?

**Decision (locked):** **TTS-only for v1**, with the `tts/`/`human/` provenance convention (§6.2) so human recordings slot in later without design change. Rationale: TTS keeps the corpus reproducible and Claude-extensible (new phrase = one manifest line), which is what regression detection needs. The honest cost — TTS speech is cleaner than human speech, so absolute recognition quality is under-tested — is bounded, because the tiers assert *regression* (same input, changed output), not absolute accuracy. Human fixtures become a cheap later enrichment (optionally via v3.4's deferred Record tooling), not a blocker for any tier.

### DR-3 — Tier D becomes a bound `device-check` agent

> **SUPERSEDED by Amendment A1 (2026-08-11).** Tier D is dropped, so no `device-check` agent is added to the roster and the project CLAUDE.md's "Human-only" verification paragraph is **not** rewritten — it stands exactly as it was. Retained as historical record.

**Fork:** Should the Tier D rig be bound into the project CLAUDE.md verification bindings as an agent (mirroring `compile-check`), or stay an ad-hoc script?

**Decision (locked):** **Yes — bind it**, as a `device-check` agent added to the shared roster, but only as part of the Tier D feature's close-out, once the rig has proven stable in that feature's own verification. The binding rewrites the current "Human-only" paragraph: on-device checks become "run `device-check` when a Quest is connected; report deferred only when no device is present." Binding edits are workflow-layer (local, gitignored), not product docs.

### DR-4 — Tier C targets Linux/WSL only

**Fork:** Build the desktop bridge for Windows too, so the Unity editor could run the *real bridge* instead of the separate `EditorMicBackend` C# path?

**Decision (locked):** **Linux-only.** Tier C's goal is a Claude-runnable harness, which WSL serves directly. A Windows bridge inside the editor is a much larger change — supplanting or forking the editor backend, shipping/loading a DLL, packaging implications — and its verification value is already covered by the B+C+D combination (B: editor pipeline on real Vosk; C: bridge logic; D: real bridge on real hardware). Noted as a possible future design topic if editor/device parity ever becomes a recurring bug source; nothing in this design forecloses it (the CMake preset mechanism would extend to a Windows preset).

## 10. What stays human (honest residue)

> **Amended by A1 (2026-08-11).** With Tier D dropped, the residue is larger than this section originally allowed. Everything Tier D was to automate stays human: **the arm64 binary on real hardware, JNI AudioRecord and mic capture, Quest audio routing, end-to-end acoustic latency, and Android's actual delivery of `OnApplicationPause`.** That is the project CLAUDE.md's existing "Human-only" paragraph, unchanged. The two device-provisioning bullets below are now moot in their Tier D framing — no rig runs, so there is no per-session device requirement at all.

- **One-time device provisioning:** developer mode, Meta account developer verification for Scriptable Testing Services, cable/network setup. Human, once per device. — *(A1: no longer needed; nothing automated drives the device.)*
- **Physical presence:** a Quest must be connected, charged, and awake-able for Tier D; Claude reports "deferred — no device" when absent (per DR-3's binding language). — *(A1: moot — Claude reports on-device verification as deferred unconditionally.)*
- **Human-voice realism:** until/unless human fixtures are added (DR-2), absolute recognition quality with real voices is spot-checked by humans, not asserted by the rig.
- **Subjective feel:** perceived latency, comfort, in-app UX of push-to-talk — never automatable; stays a G2-time human judgment for features where it matters.

## 11. Feature backlog (canonical — locked at G1)

Dependency-ordered; sequential-through-main per the shared workflow. One feature per tier; the fixture corpus rides with its first consumer (Tier B) rather than as a standalone feature, since alone it verifies nothing.

| # | Item | Lane / branch | One-line scope | Depends on |
|---|------|---------------|----------------|------------|
| 0 | Pause state-machine tests (Tier A) | **Issue lane** — filed as [#49](https://github.com/jinwoo1601/VoXR-Speech-Recognition/issues/49) | PlayMode tests for the `OnApplicationPause` state machine via SendMessage; test-only if feasible, at most a minimal internal seam (§5). | — |
| 1 | `feat-wav-replay` (Tier B) | Feature | `ProcessChunk` extraction + `StartPlayback` playback mode in `EditorMicBackend` (v3.4's replay half, Record half deferred), TTS fixture corpus + generation script, PlayMode acoustic regression tests. | — |
| 2 | `feat-bridge-push-audio` (Tier C) | Feature | `vosk_bridge_push_audio` + push-mode start + input-level getter in the bridge, backend-selection refactor (neutral include + CMake option) with stub backend, desktop Linux CMake preset, WSL WAV→transcript harness with expectation manifest. | 1 (fixtures) |
| ~~3~~ | ~~`feat-device-rig` (Tier D)~~ **DROPPED (A1, 2026-08-11)** | ~~Feature~~ | ~~adb prep script, UTF `-testPlatform Android` runbook, on-device suite (push-audio integration, mic liveness, acoustic loopback + latency measurement, lifecycle delivery via logcat handshake), `device-check` agent + binding update.~~ Reached Phase 4 on a local branch; archived at tag `archive/tier-d`, never merged. | ~~1 (fixtures), 2 (push mode, level getter)~~ |

**Backlog status after A1: complete.** Items 0, 1 and 2 are built, G2-accepted and merged (#52/#54, #50, #51). Item 3 is dropped. Nothing remains open against this design.

## 12. Consequences after landing (out of scope here, recorded as commitments)

- **Verification bindings** in the project CLAUDE.md gain, per feature close-out: Tier B fixtures run inside the existing Unity test procedure (no change); a Tier C harness invocation as the standard check on every `NativeBridge~` change (alongside the existing arm64 compile); the `device-check` binding replacing most of the "Human-only" paragraph (DR-3). The erroneous `OnApplicationFocus` mention in the bindings gets corrected at the same time.
  - **A1 (2026-08-11):** the first two landed (Tier B at PR #50's G2; Tier C's harness ratified as the standing check at PR #51's G2, design §12/§7.3). The `device-check` binding is withdrawn with DR-3 — the "Human-only" paragraph stands as written. The **`OnApplicationFocus` correction is now orphaned**: it was to ride along with that rewrite, and the bindings still say "lifecycle/focus/pause behavior" though §2 established `OnApplicationFocus` is implemented nowhere. It survived A1 as a standalone fix to the project CLAUDE.md, unblocked and independent of any tier — **applied 2026-08-11**: `OnApplicationFocus` removed (verified absent from the whole package), the paragraph now names `OnApplicationPause` at `Runtime/VoxrPushToTalkController.cs:133` as the only lifecycle hook, distinguishes its Tier-A-covered *state machine* from the human-only question of Android's *delivery*, and records that the paragraph is permanent because Tier D is dropped. This closes the last item in §12.
- **KNOWN_LIMITATIONS.md** timing entries get measured values (and a note on how they're measured) once Tier D's loopback latency numbers exist — a post-G2 product-doc edit within the Tier D feature.
  - **A1 (2026-08-11): withdrawn.** No loopback measurement exists or is planned; the timing figures stay the anecdotes §1 complained about. If those numbers ever matter, they need a new design topic, not this one.
- Nothing in this design ships to package consumers: `Tests~`, `NativeBridge~` build presets, and fixtures are all stripped by `release.yml`; `Planning~` and the workflow layer are gitignored.

---

*Locked at G1 on 2026-07-30: DR-1…DR-4 ratified as proposed, backlog (§11) canonical. This doc is now immutable per the shared workflow — if implementation contradicts a locked decision, reopen on a design branch and re-lock; never patch silently.*

*Amendment A1 — **re-locked at G1 on 2026-08-11** (human ruling in conversation: "locked"). Tier D dropped; delivered scope is Tiers A/B/C; backlog closed (§0, §8, DR-3, §10, §11, §12). DR-1, DR-2 and DR-4 stand as locked on 2026-07-30; DR-3 is superseded. This doc is immutable again and this design is **complete** — nothing is open against it. (No design branch: `Planning~/` is gitignored and untracked, so this doc has no git history to branch — the amendment was recorded in place and re-locked by the human's ruling.)*
