---
type: requirements
feature: bridge-push-audio
topic: automated-verification
status: accepted   # G2 ruled in-conversation 2026-07-30: accepted with fix scope = review M1 (shipped .so) + baseline-integrity cluster + arch-doc corrections; remaining confirmed findings = future issue-lane candidates
updated: 2026-07-30
sources: [Planning~/design-docs/automated-verification.md]
---

# Bridge Push Audio (Tier C) — Requirements

## 1. Feature & scope

Backlog line (design §11, row 2): `vosk_bridge_push_audio` + push-mode start + input-level getter in the bridge, backend-selection refactor (neutral include + CMake option) with stub backend, desktop Linux CMake preset, WSL WAV→transcript harness with expectation manifest.

This feature realizes Tier C of the locked automated-verification design (`Planning~/design-docs/automated-verification.md` §7): "the tier that makes native-bridge changes verifiable by Claude directly in WSL, with no Unity and no device." It consumes the fixture corpus Tier B committed (§11 "Depends on: 1 (fixtures)", merged 2026-07-30) and creates the push seam and input-level getter that Tier D's on-device tests reuse (§8.3.1, §8.3.2).

## 2. Why — verification value

Today a `NativeBridge~/` change is verifiable only as "the arm64 build compiles"; whether the bridge still *recognizes* is a human-on-Quest question. Yet almost everything the bridge does — ring buffer, downsampler, AGC, float→int16 conversion, result queue, grammar handling, error paths — is platform-independent logic. Tier C compiles that logic for x86_64 against a real Linux libvosk, pushes the committed fixture corpus through it, and asserts on the transcripts — making bridge-logic regressions catchable on every change, from WSL, in seconds (§7.3). It is explicitly blind to the arm64 binary, JNI AudioRecord, and Quest audio routing (§4 row C); Tier D remains the backstop for the shipped binary on the shipped hardware.

## 3. Observable behavior

In WSL, after a one-time documented provisioning step (Linux `libvosk.so` + small model), a developer — or Claude, unattended — configures the desktop preset, builds, and runs the harness against the committed corpus with the committed expectation manifest. The harness prints per-fixture final transcripts as JSON and exits zero exactly when every fixture matches its expectation; a mismatch exits nonzero naming the fixture, expected, and actual. The Android library, built with the documented commands and default options, is behaviorally identical to before the feature. The push seam and input-level getter exist (exported, callable) in that Android build too, unused by any current caller.

## 4. Functional requirements

| #  | Requirement | Priority | Acceptance check | Source |
|----|-------------|----------|------------------|--------|
| F1 | `int vosk_bridge_push_audio(const float* samples, uint32_t count)`: writes pre-DSP 48 kHz mono float samples into the same ring buffer a capture backend writes — the pushed audio then traverses the identical recognition path (ring → downsample → AGC → int16 → Vosk). Returns samples written / error code so a caller can pace against ring-buffer overflow; misuse (not initialised / not started in push mode) returns a documented error, never crashes. | Must | Harness pushes a clean-command fixture and the final transcript matches; pushing more than the ring accepts yields a short count/error, not corruption; calling before init/start returns an error code. | design §7.1 |
| F2 | A push-mode start launches the recognition thread **without** starting the capture backend — it skips exactly the capture start; the recognition loop is untouched except that its capture-error check is gated to capture mode (design §7.1 amendment, ratified 2026-07-30: the capture error flag is sticky and only ever cleared by a capture start, so an unGated check would kill Android push sessions after any errored capture session). Capture and push are mutually exclusive by construction (push mode never starts capture). The stop/destroy path after a push-mode session is safe even though capture never started. | Must | A full push-mode session (init → push-start → push → drain → stop → destroy) runs clean on desktop, where no JNI exists at all; stop-after-push verified a no-op on the never-started capture. | design §7.1 + amendment |
| F3 | `float vosk_bridge_get_input_level()`: read-only rolling RMS of the most recent pre-DSP chunks, computed in the recognition loop and published through an atomic. Lands with this feature because it is a bridge-ABI change; its consumer is Tier D's mic-liveness check (§8.3.2). | Must | During a push of a voiced fixture the reported level exceeds the value reported during the silence fixture; callable at any lifecycle point without error. | design §7.1 |
| F4 | Push mode and the input-level getter exist in the **Android build too** (same sources, same ABI): Tier D's on-device bridge-integration test (§8.3.1) reuses exactly this seam. | Must | arm64 build green; new symbols exported from `libvosk-bridge.so` (symbol-table inspection). | design §7.1 |
| F5 | Backend selection: a backend-neutral `audio_capture.h` forwards to the selected backend's header, and a CMake option (`VOSK_BRIDGE_CAPTURE=audiorecord\|aaudio\|stub`, default `audiorecord`) picks both the compiled source and the forwarded header — replacing today's two hardcodings (source list + direct include). The default Android build stays behaviorally identical. | Must | Default configure compiles exactly the AudioRecord backend as today (source list diff); switching the option swaps backends with no source edits. | design §7.2 |
| F6 | A stub capture backend (`Start` = no-op success, `HasError()` always false), selected by the desktop preset — so even non-push code paths are safe on desktop. | Must | On the desktop build, plain `vosk_bridge_start()` (capture mode, stub backend) succeeds and stops cleanly without audio. | design §7.2 |
| F7 | A desktop CMake preset for Linux x86_64 handling all four Android-isms: stub backend selected; logging falls back to `printf` when `__ANDROID__` is absent; no liblog link; `VOSK_LIB_DIR` points at a local Linux x86_64 `libvosk.so` (Alphacephei prebuilt, downloaded once into gitignored `NativeBridge~/vendor/`, never committed). With `stub` selected, no JNI is compiled or included anywhere in the preset. Linux-only per DR-4 — no Windows preset. | Must | Clean WSL configure + build from a fresh checkout plus the provisioned `vendor/` succeeds with no Android NDK/SDK involvement; grep confirms no `jni.h` in the compiled TUs. | design §7.2, DR-4 |
| F8 | A WSL harness CLI (`NativeBridge~/harness/`) linking the desktop bridge: loads fixture WAVs (int16 WAV → float), runs init → push-start → push chunks → drain results, prints final transcripts as JSON; exit code reflects expected-vs-actual per the expectation manifest. Invocation shape: `harness --model <path> --fixtures Tests~/Fixtures/audio/tts --manifest expectations.json`. Malformed input (missing/non-48 kHz-mono-16-bit WAV, unreadable manifest/model) fails with a clear message naming the file and the problem. | Must | Run against the committed corpus + committed expectations → exit 0; deliberately corrupt one expectation → nonzero exit and a legible per-fixture diff; feed a 44.1 kHz WAV → clear format error naming actual and required. | design §7.3 |
| F9 | The expectation manifest is a **bridge-level committed baseline**: transcript-level expectations for every corpus fixture, including the negatives (silence ⇒ no/empty final transcript). It is seeded from, but not the same file as, the corpus manifest `Tests~/Fixtures/audio/manifest.json` — that manifest's `expectedTranscript`/`expectedIntent` are parser-level expectations (deliberately empty for the filler/split/homophone-negative cases), while the harness sees only what Vosk decodes (e.g. `[unk]` tokens for filler), on a different platform build of libvosk. Per the Tier B regeneration policy (its requirement F7's G2-ratified amendment), raw-transcript baselines belong to the committed corpus: regenerating the corpus re-baselines this manifest consciously. The manifest documents this relationship in-file. | Must | Every one of the 16 corpus fixtures has an entry; the baseline matches what the desktop bridge actually produces at commit time; the relationship note is present. | design §7.3, §11 row 2; Tier B requirements F7 (amendment); design silence flagged in recon |
| F10 | The harness exercises **grammar-constrained decoding** equivalent to device usage (`vosk_bridge_set_grammar` with a grammar covering the demo command vocabulary), since "grammar handling" is part of what the tier verifies. Grammar sourcing/format is architecture's call. | Must | Harness run decodes with a grammar (not free-form); the homophone fixtures behave per their committed baseline under that grammar. | design §7.3 (verifies list), §4 row C |
| F11 | The managed P/Invoke surface (`Runtime/Native/BridgeNative.cs`) gains bindings for the three new entry points, keeping the C# surface in sync with the ABI; nothing managed calls them until Tier D. | Should | Host-project EditMode + PlayMode suites stay green with the bindings present; no behavioral change. | backlog handoff; design §8.3 (Tier D consumer) |
| F12 | Provisioning is documented in a harness README: where to download the Alphacephei Linux prebuilt into `vendor/`, where to get the small model, the preset configure/build commands, and the harness invocation — sufficient for a cold WSL start. | Must | A reader can go from fresh checkout to green harness run using only the README (verified by executing it during implementation). | design §7.2 |

## 5. Non-functional requirements

- **Android build behaviorally identical:** no shipped-runtime behavior change beyond the additive seams (§3 non-goals: push mode, input-level getter); the backend-selection refactor "changes how sources are selected, not what the Android build does." The Android capture chain (`audio_capture_audiorecord.cpp`) is not modified.
- **Single-producer contract preserved:** pushing from one caller thread must be as safe with the concurrent recognition loop as the capture thread it replaces (the ring buffer is SPSC); push mode never has two producers by construction.
- **Determinism:** same fixtures + same model + same code ⇒ same harness verdict; a flaky harness run is a defect, not tolerance.
- **Speed:** the full-corpus harness run completes in well under a minute of wall clock on WSL (bounded by Vosk decode, not fixture durations) — the point of the tier is per-change iteration.
- **Failure legibility:** a failing fixture names the fixture, expected, and actual transcript in the output — diagnosable from the harness output alone.
- **Repo footprint:** `vendor/` and models are gitignored and never committed; `NativeBridge~/` (including the harness) is already stripped from the published package by `release.yml`.

## 6. Non-goals / deferred

- **No Windows desktop bridge** (DR-4, locked): "desktop" in this feature means Linux/WSL only. The Windows editor keeps its existing, entirely separate C# path (`VoxrNative.cs` + `EditorMicBackend` + managed DSP) — this feature neither touches nor supplants it.
- **No AAudio re-validation:** the `aaudio` option value exists per §7.2, but the AAudio backend files are unmaintained legacy (they predate the AudioRecord fix; AAudio is broken on Quest per KNOWN_LIMITATIONS). Selecting `aaudio` is provided by the mechanism, not verified by this feature.
- **No on-device work:** Tier D territory (`feat-device-rig`) — this tier is blind to real hardware by design (§4).
- **No desktop microphone capture:** the desktop backend is a stub; audio enters via push only.
- **No new managed features:** the C# bindings (F11) are surface-only; recogniser/editor behavior unchanged.
- **No CI/cloud integration** (design §3).
- **Not a recognition-quality benchmark:** the expectation baseline pins current behavior to catch *change*, not to certify accuracy.
- **Adjacent but not owned:** the fixture corpus and its manifest (consumed as-is; shape frozen by Tier B's merge); the vendored `include/vosk_api.h` (do-not-modify); `build.sh` (the observed `android-29` vs. CLAUDE.md `android-27` platform discrepancy is pre-existing and stays untouched).

## 7. Dependencies & assumptions

- **Upstream:** Tier B merged to main (fixture corpus + manifest present) — satisfied 2026-07-30. Independent of issue #49 (Tier A, issue lane).
- **Downstream contract:** Tier D consumes the push ABI, the input-level getter, and the Android availability of both (F4); the new ABI shape becomes a cross-feature contract on merge — changing it later touches Tier D.
- **Environment:** WSL with a Linux C++17 toolchain + CMake ≥ 3.21 (presets) + a generator (probed 2026-07-30: **absent** — human provisioning required, documented in the README — the Android builds use Windows-side CMake/Ninja, which the desktop preset cannot).
- **Assumption:** Alphacephei publishes a Linux x86_64 prebuilt `libvosk.so` compatible with the vendored `vosk_api.h`; exact version pinned in the architecture doc/README.
- **Assumption:** the small English model used on-device (or a compatible small model) is downloadable for desktop use; named in the README.
- **Assumption:** one-time internet access for provisioning (libvosk prebuilt, model); the committed repo never depends on it after provisioning.
- **Assumption:** the demo command grammar as canonicalized by Tier B (`Tests~/Runtime/DemoGrammar.cs` mirroring `Samples~/CommandRecognition/CommandDemo.cs`) defines the vocabulary the harness grammar must cover.

## 8. Acceptance criteria (G2)

- [ ] Push API + push-mode start + input-level getter exist, behave per F1–F3, and are exported in both desktop and arm64 builds (F4).
- [ ] Backend selection via neutral include + CMake option; default Android build compiles the same sources as before (F5).
- [ ] Stub backend safe for non-push paths on desktop (F6).
- [ ] Desktop preset builds clean in WSL with no Android/JNI anywhere (F7); arm64 build stays green per verification bindings.
- [ ] Harness runs the committed corpus against the committed expectation baseline: exit 0; corrupted expectation demonstrably fails legibly (F8, F9).
- [ ] Grammar-constrained decoding in the harness (F10).
- [ ] C# bindings present, host-project EditMode + PlayMode green (F11) — full test pass per verification bindings.
- [ ] Harness README takes a cold WSL start to a green run (F12).
- [ ] Proposal decided at G2: the harness invocation becomes a standing verification binding in the project CLAUDE.md for every `NativeBridge~` change (design §12 names this the standard check; wording proposed in the architecture doc, human ratifies).
- [ ] `review-pr` verdict posted on the PR; human rules G2 in conversation (project bindings; on-device behavior is explicitly out of scope — nothing to defer to headset).

## 9. Open questions

1. **Push-start shape:** separate `vosk_bridge_start_push()` vs. a mode flag on `vosk_bridge_start()` — design §7.1 leaves both open. Architecture decides.
2. **Expectation-manifest schema and location:** file lives with the harness (e.g. `NativeBridge~/harness/expectations.json`); exact fields, the empty/no-transcript semantics for negatives, and the seeding/re-baselining procedure are architecture's call. The *policy* (bridge-level committed baseline, full corpus coverage, relationship documented) is pinned here (F9).
3. **Grammar sourcing:** hand-maintained grammar file in the harness vs. generated from the corpus manifest phrases vs. duplicated demo-grammar vocabulary — architecture decides; drift risk with `DemoGrammar.cs` should be weighed.
4. **Version pins:** which Alphacephei libvosk release and which small model — architecture/README; must match the vendored `vosk_api.h` ABI.
5. **Input-level units and window:** linear RMS vs. dBFS, window length, and pre- vs. post-AGC placement (design says pre-DSP chunks) — architecture decides; Tier D only needs "above noise floor within a timeout."
6. **F11 timing:** bindings could equally land with Tier D; kept here as Should per the backlog handoff so the ABI and managed surface merge together. Human may strike at G2.
7. **Desktop toolchain availability in WSL** (compiler/CMake/generator versions actually present) — verified at implementation; README documents what it needs.
