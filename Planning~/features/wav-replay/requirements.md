---
type: requirements
feature: wav-replay
topic: automated-verification
status: accepted   # G2 ruled in-conversation 2026-07-30: accepted as-is (incl. D-2/D-6/D-7 readings, F7 amendment, public containers)
updated: 2026-07-30
sources: [Planning~/design-docs/automated-verification.md, Planning~/v3-expansion-plan.md]
---

# WAV Replay (Tier B) — Requirements

## 1. Feature & scope

Backlog line (design §11, row 1): `ProcessChunk` extraction + `StartPlayback` playback mode in `EditorMicBackend` (v3.4's replay half, Record half deferred), TTS fixture corpus + generation script, PlayMode acoustic regression tests.

This feature realizes Tier B of the locked automated-verification design (`Planning~/design-docs/automated-verification.md` §6): a replay seam in the Windows-editor mic backend that feeds committed WAV fixtures through the recognition pipeline exactly as live microphone audio traverses it, the fixture corpus those tests replay, and the PlayMode tests that assert on the results. It is the first feature off the design's backlog; Tiers C (`feat-bridge-push-audio`) and D (`feat-device-rig`) consume the corpus it creates (§11 rows 2–3, "Depends on: 1 (fixtures)").

## 2. Why — regression value

Today the acoustic path (Downsampler → AGC → int16 → Vosk → parser) is exercised only by a human speaking into the editor microphone; the text-injection seams (`InjectResult`/`InjectText`) bypass it entirely. Tier B makes that path assertable per change: replay the same committed utterance, and a changed outcome is a caught regression. This is the tier that catches DSP/AGC/grammar regressions cheaply (design §6.3). It is explicitly a *regression detector*, not an absolute recognition-quality benchmark — TTS speech is cleaner than human speech (§6.3, DR-2).

## 3. Observable behavior

A developer runs the PlayMode suite through the standard host-project procedure (project CLAUDE.md verification bindings) with no microphone attached and without speaking. Each audio test case loads its committed WAV fixture, replays it through the full pipeline, and passes or fails against its expected transcript/intent. The silence/noise fixture passes only if no command is recognized. Replaying a WAV at any rate other than 48 kHz produces a clear error naming the actual and required rates — no crash, no silent misrecognition. Live-microphone behavior in the editor is byte-for-byte what it was before the feature.

## 4. Functional requirements

| #  | Requirement | Priority | Acceptance check | Source |
|----|-------------|----------|------------------|--------|
| F1 | A WAV fixture can be replayed through the complete editor recognition pipeline (downsample → AGC → int16 → Vosk → result dispatch), firing the same events as live audio (`OnPartialResult`, `OnFinalResult`, `OnResult`, `OnCommandRecognised`). | Must | PlayMode test replays a clean-command fixture and observes a final result and a recognized command via the public events. | design §6.1 |
| F2 | Replayed audio enters the pipeline pre-DSP, so DSP/AGC parameter changes affect replay identically to live audio. | Must | A fixture at Quest-realistic amplitude (peaks 0.04–0.4) is recognized — which requires the AGC stage to have engaged; backend AGC diagnostics (`AgcGain`) report a gain change during replay. | design §6.1, §6.2; KNOWN_LIMITATIONS.md (Quest mic gain) |
| F3 | A WAV at any sample rate other than 48 kHz (or not mono 16-bit PCM) is rejected with a clear error naming actual and required format; nothing is fed to the recognizer. | Must | Test feeds a 44.1 kHz WAV; asserts the error fires with both rates named and no recognition events follow. | design §6.1 |
| F4 | Playback adds zero overhead when not in playback: the live-mic path performs no additional per-tick work and no new allocations when the feature is unused. | Must | Existing lifecycle and zero-allocation tests (`ZeroAllocPollPath`, recogniser lifecycle) stay green; review confirms no added work on the mic path in non-playback mode. | design §6.1 (extends v3.4's "no runtime overhead" decision) |
| F5 | Audio test cases are data assets pairing a fixture reference with an expected transcript/intent and a description; a suite asset batches them for a corpus-wide run. | Must | The committed suite covers every corpus fixture; the PlayMode run consumes the suite, not hand-listed paths. | design §6.1 (v3.4 replay assets) |
| F6 | A committed fixture corpus at `Tests~/Fixtures/audio/tts/` covers, at minimum: clean commands over the demo command grammar, slot variants, a homophone trap ("to"/"two"), leading filler (`[unk]` handling), a split command with mid-pause (buffer-window behavior), and a silence/noise negative. Format: 48 kHz mono 16-bit WAV, pre-DSP, amplitude-scaled to peaks in 0.04–0.4. A `human/` sibling folder exists as the provenance convention (empty in v1). | Must | Inspect corpus + manifest: every category present; format and peak range verified mechanically (script or test) for every committed WAV. | design §6.2, DR-2 |
| F7 | The corpus is reproducible: a checked-in generation script + phrase manifest (`Tests~/Fixtures/generate.sh` + manifest) regenerate it end-to-end (TTS → resample to 48 kHz → amplitude-scale); generated WAVs are committed so tests never depend on the TTS tool being installed. | Must | Run the script in WSL from a clean state; it produces the corpus; the PlayMode suite passes against the regenerated files **at the intent/slot level** (TTS is non-deterministic across runs, so raw-transcript assertions are pinned to the *committed* baseline and re-baseline on regeneration — amendment flagged at G2, discovered in Phase 5: a regenerated fixture decoded as a synonymous pattern of the same intent). | design §6.2 |
| F8 | PlayMode-in-editor acoustic regression tests (real `libvosk.dll`, real model): for each test case — load fixture, start playback, await final result, assert expected transcript/intent per the test-case asset. | Must | Suite green under the existing host-project test procedure (both EditMode and PlayMode runs per bindings). | design §6.3 |
| F9 | The audio tests never fail in environments where the editor mic backend doesn't exist (non-Windows editor, players). | Should | Suite compiles everywhere; outside `UNITY_EDITOR_WIN` the replay tests are compiled out or marked skipped — absent from results or ignored, never failed. | design §6.3 (tests bound to the Windows-editor environment) |
| F10 | Replay can run faster than real time to keep suite duration down. | Could | Corpus suite completes materially faster than the summed fixture durations. | v3.4 ("optionally faster") |

## 5. Non-functional requirements

- **Shipped runtime unchanged:** no behavior change outside the playback seam; the seam is internal (no new public game-facing API) — design §3 non-goals ("no new user-facing features").
- **Determinism:** same fixture + same model + same code ⇒ same verdict; a flaky audio test is a defect, not tolerance.
- **Suite duration:** the corpus PlayMode run stays in single-digit minutes on the host project (fixtures are seconds long); duration must not grow past what a per-change verification run tolerates.
- **Repo footprint:** committed WAVs are short utterances; total corpus on the order of a few MB (it lives in `Tests~/`, which `release.yml` strips from the published package).
- **Failure legibility:** a failing audio test names the fixture, the expected transcript/intent, and the actual result — enough to diagnose without re-running locally with a debugger.

## 6. Non-goals / deferred

- **Record half of v3.4 deferred** (Debug-Window record button, recorder, auto-save of utterances): human capture tooling, not needed for automated verification — design §6.1 scope delta. Becomes relevant only if human fixtures are added later (DR-2).
- **Human-recorded fixtures deferred** (DR-2): v1 is TTS-only; `human/` exists as an empty convention.
- **No resampler:** non-48 kHz input is rejected, never converted (v3.4 locked decision, carried in §6.1).
- **No playback UI** (Debug-Window load/play buttons, progress bar from v3.4): the consumer is the test suite, not a human at a window.
- **No bridge or Android changes:** Tier C/D territory (`feat-bridge-push-audio`, `feat-device-rig`).
- **No CI/cloud integration** (design §3).
- **Not a recognition-quality benchmark:** asserts regression (same input, changed output), not absolute accuracy (§6.3).
- **Adjacent but not owned:** the demo command grammar (this feature replays it, doesn't change it); the batch text-test runner (`VoxrBatchTestRunner` stays text-in; "compatible" means replay results surface through the same public events/result types as live recognition, not through the runner's API).

## 7. Dependencies & assumptions

- **Upstream:** none — first feature off the backlog; independent of issue #49 (Tier A, issue lane).
- **Downstream contract:** Tiers C and D consume `Tests~/Fixtures/audio/tts/` + the phrase manifest (§7.3 harness invocation, §8.3 on-device suite). Corpus location, WAV format, and manifest shape become a cross-feature contract on merge — changing them later touches C/D.
- **Environment:** Windows editor host project (VoXR TestGround) with real `libvosk.dll` and model, per existing verification bindings; no new binding mechanics (design §6.3).
- **Generation tooling:** piper TTS in WSL — verified available (`piper-tts` 1.6.0 installs in a venv); needed only for (re)generation, never for running tests (F7).
- **Assumption:** the demo command grammar shipped with the package defines the utterance domain — 11 intents in 3 sets (weapons, navigation, common) over 6 slots (4 enumerated incl. aliases, 2 number-sequence), canonical in `Samples~/CommandRecognition/CommandDemo.cs` with a data-identical ScriptableObject mirror under `Samples~/CommandRecognition/AssetAuthoring/`. Fixtures key to the shipped patterns as they exist at implementation time; note the two copies are independently maintained (nothing enforces sync) — the corpus targets the `CommandDemo.cs` text. The grammar's own vocabulary supports the required homophone trap ("switch **to** weapons" vs. quantity "**two**").
- **Assumption:** `EditorMicBackend` remains the single editor audio entry point (no backend interface exists today); the seam lives inside it.

## 8. Acceptance criteria (G2)

- [ ] Clean-command replay fires the full event chain and recognizes the expected command (F1).
- [ ] Quest-amplitude fixture recognized; AGC demonstrably engaged during replay (F2).
- [ ] Wrong-rate WAV rejected with a clear both-rates-named error; no recognition follows (F3).
- [ ] Zero overhead when not in playback: existing zero-alloc and lifecycle tests green; no live-path change on review (F4).
- [ ] Suite asset covers the whole corpus; PlayMode run consumes it (F5).
- [ ] Corpus committed with every §6.2 category, format + amplitude mechanically verified (F6).
- [ ] `generate.sh` + manifest regenerate the corpus in WSL; suite passes on regenerated files (F7).
- [ ] Full test pass per verification bindings: EditMode and PlayMode both green on the host project (F8).
- [ ] `review-pr` verdict posted on the PR; human rules G2 in conversation (project bindings — replaces in-headset playtest: Tier B is editor-only, nothing device-facing to verify).

## 9. Open questions

1. **Container naming:** v3.4 says `VoskAudioTestCase`/`VoskAudioTestSuiteAsset`; the codebase has since rebranded every type to the `Voxr` prefix, and the design doc cites the old names only as provenance ("v3.4's replay assets"). Recommendation: `VoxrAudioTestCase`/`VoxrAudioTestSuiteAsset`. Architecture doc decides; flag to the human if this reads as contradicting locked design rather than applying the rebrand.
2. **Playback API shape:** v3.4 itself is split between `PlaybackFrom(float[] samples, int sampleRate)` and `StartPlayback(float[] samples)`; the design leaves both on the table (§6.1). Architecture decides the signature.
3. **Fixture reference representation:** v3.4's container held `AudioClip clip; // or a path to a WAV file`. Fixtures live in `Tests~/` (not `Resources/`, not necessarily imported as assets) — path-based loading vs. imported `AudioClip` changes how the host project sees them. Architecture decides.
4. **Phrase manifest contents and intent coverage:** the design pins categories, not phrases (§6.2). The demo grammar is now inventoried (11 intents, 6 slots); the open call is coverage depth — every intent (11+ fixtures; recommended: cheap at one manifest line each, and 6 of the 11 intents currently have *no* test coverage of any kind) vs. a representative subset. The manifest is drafted in the architecture doc.
5. **Piper voice model:** which voice, and how it's pinned for reproducibility (the design doesn't name one). Architecture/implementation.
6. **Split-command expectation:** what the mid-pause fixture asserts (command recognized via buffer window? pending-then-commit sequence?) depends on the command-recogniser's buffer-window semantics — pin the exact expectation in the manifest during architecture.
7. **Pacing v1:** real-time replay is acceptable per v3.4; faster-than-real-time (F10) only if it falls out cheaply. Architecture decides.
