# v3 Expansion Plan: Iteration Speed Tooling

## Context

v3.0 (text injection) and v3.1 (Editor live mic) eliminated the deploy-test-logcat cycle. Developers can now test command definitions and acoustic behavior entirely in the Unity Editor. But two friction categories remain:

1. **Observability** — When a command doesn't fire, developers must reason backward from silence. There's no visibility into *why* the match failed: was the score too low? Did VOSK misrecognise a word? Was the confidence below threshold? The information exists inside the pipeline but isn't surfaced.

2. **Repeatability** — Every test requires the developer to speak again. There's no way to capture a tricky utterance once and replay it while tuning thresholds, no way to batch-test 50 phrases after changing an alias, and no way to share test cases between team members.

v3.2–v3.5 address these gaps. Together with v3.0 and v3.1, they complete the v3 theme: **iteration speed is never the bottleneck**.

---

## Version Overview

| Version | Feature | Impact | Effort |
|---------|---------|--------|--------|
| **v3.2** | Command Debug Window | High | Medium |
| **v3.3** | Batch Test Runner | High | Low–Medium |
| **v3.4** | Audio Capture & Replay | Medium | Medium |
| **v3.5** | Hot-Reload Command Definitions | Medium | Low |

Recommended implementation order: v3.2 → v3.3 → v3.5 → v3.4. The debug window makes every subsequent feature more useful because you can see the results. The batch runner compounds that with repeatability. Hot-reload is small and self-contained. Audio replay is the most work and benefits from all three prior features being in place.

---

## v3.2 — Command Debug Window

### Problem

When a spoken command doesn't fire, the developer sees nothing. When it does fire, they see the intent name in a log. Neither case reveals what happened inside the pipeline: which words VOSK recognised, what confidence each had, how the pattern matcher scored the utterance, which threshold rejected it, or what the alternative hypotheses were.

Developers currently diagnose matching failures by adding `Debug.Log` calls, re-entering Play Mode, speaking again, reading the Console, and repeating. This is the new bottleneck now that the deploy cycle is gone.

### What we're adding

A custom `EditorWindow` that displays the command recognition pipeline state in real time:

**Left panel — Audio & Recognition:**
- Audio input level meter (RMS of the last chunk, pre- and post-AGC)
- Current AGC gain value
- VOSK partial result (live, updates as the user speaks)
- VOSK final result text
- Per-word confidence bars (green ≥ 0.8, yellow ≥ 0.5, red < 0.5)
- N-best alternatives with their confidence scores

**Right panel — Command Matching:**
- Active command sets (names, command count per set)
- Current grammar string (collapsible)
- Last match attempt breakdown:
  - Matched intent (or "No match")
  - Pattern that was tested, with alignment showing which words matched which tokens
  - Per-slot extraction: slot name, matched value, source words, confidence
  - Raw score and minimum score threshold → pass/fail
  - Aggregate word confidence and minimum confidence threshold → pass/fail
  - If rejected: the specific reason ("score 0.42 < minScore 0.60" or "word confidence 0.31 < minConfidence 0.40")
- Match history (scrollable list of the last ~20 attempts with pass/fail indicator)

**Bottom bar:**
- Quick inject field: type text and press Enter to run it through the pipeline (uses existing `InjectText`)
- Clear history button
- Pause/resume toggle (stops updating without stopping recognition)

### Design decisions

- **Editor-only.** The window is in an `Editor/` assembly. It reads state from the runtime components via public/internal accessors but adds no runtime code or overhead.
- **Pull model, not push.** The window polls state every `EditorApplication.update` tick (or every `Repaint` during Play Mode). No events or callbacks added to the runtime pipeline for the window's benefit.
- **Read-only.** The window observes but does not modify pipeline state. The quick inject field calls the existing public `InjectText()` API.
- **Works with both backends.** Displays the same information whether audio comes from the Editor mic backend or text injection. The data sources are the same: `OnResult`, `OnPartialResult`, and parser internals.

### Data access

The window needs read access to state that is currently private inside `VoskCommandParser` and `VoskCommandRecogniser`. Rather than making internals public, add a lightweight diagnostic struct:

```csharp
// Runtime/Commands/VoskMatchDiagnostics.cs
public readonly struct VoskMatchDiagnostics
{
    public readonly string InputText;
    public readonly VoskWord[] Words;
    public readonly string MatchedIntent;       // null if no match
    public readonly string MatchedPattern;       // the pattern string that matched
    public readonly float Score;
    public readonly float MinScore;
    public readonly float AggregateConfidence;   // lowest word confidence in matched span
    public readonly float MinConfidence;
    public readonly VoskSlotMatch[] Slots;
    public readonly string RejectReason;         // null if accepted

    // ... constructor ...
}

public readonly struct VoskSlotMatch
{
    public readonly string SlotName;
    public readonly string Value;
    public readonly int StartWord;
    public readonly int EndWord;
    public readonly float Confidence;            // min confidence across slot's words
}
```

`VoskCommandRecogniser` populates this struct at the end of `HandleResult()` and exposes it as `LastMatchDiagnostics`. This is a single struct copy — zero allocation, negligible cost. The Editor window reads it on each repaint.

For audio levels, `EditorMicBackend` exposes two float properties: `PreAgcRms` and `PostAgcRms`, computed from the last processed chunk. On Android (where the Editor window is irrelevant), these are not available.

### Files to create

| File | Description |
|------|-------------|
| `Editor/VoskDebugWindow.cs` | The `EditorWindow` subclass. IMGUI or UI Toolkit layout. |
| `Runtime/Commands/VoskMatchDiagnostics.cs` | Diagnostic struct and `VoskSlotMatch`. |
| `Editor/Jinwoo1601.VoskXR.Editor.asmdef` | Editor assembly definition (if not already present). References the runtime assembly. |

### Files to modify

| File | Change |
|------|--------|
| `Runtime/Commands/VoskCommandRecogniser.cs` | Add `LastMatchDiagnostics` property. Populate it at the end of `HandleResult()`. |
| `Runtime/Commands/VoskCommandParser.cs` | Return scoring details from `TryMatch` (or a new `TryMatchWithDiagnostics` overload) so the recogniser can populate the struct. |
| `Runtime/EditorMicBackend.cs` | Add `PreAgcRms` and `PostAgcRms` float properties, computed in `Tick()`. |

### What this does NOT change

- No changes to the Android native bridge.
- No changes to the public event API (`OnCommandRecognised`, `OnResult`, etc.).
- No new runtime dependencies. The diagnostic struct is passive data.
- No changes to existing tests.

---

## v3.3 — Batch Test Runner

### Problem

After changing a command definition, alias, threshold, or slot value, developers have no way to systematically verify that the change didn't break other commands. They speak a few phrases, spot-check, and hope. This is especially brittle for threshold tuning — lowering `minScore` from 0.60 to 0.55 might fix the command that was failing but also start accepting garbage phrases that were correctly rejected before.

### What we're adding

A test runner that feeds a list of utterances through the command pipeline and produces a results matrix. Two interfaces:

**1. Editor Window (visual)**

A two-column table:

| Input | Expected | Result | Score | Status |
|-------|----------|--------|-------|--------|
| "launch all missiles target hotel one" | launch_weapon | launch_weapon(target:hotel_one) | 0.87 | PASS |
| "attack da jackel" | attack | attack(target:jackal) | 0.52 | FAIL (score < 0.60) |
| "heading two seven zero" | heading | heading(value:270) | 0.91 | PASS |
| "hello world" | (none) | (none) | 0.00 | PASS |

Features:
- Load/save test case lists as JSON or ScriptableObject assets
- "Run All" button feeds every case through `InjectText()` sequentially
- Green/red pass/fail based on whether the result matches the expected intent (or expected rejection)
- Per-row detail expansion showing full diagnostics (reuses `VoskMatchDiagnostics` from v3.2)
- "Re-run Failed" button
- Export results as CSV for diffing across runs

**2. Edit Mode test integration**

A `VoskBatchTestRunner` utility class that can be called from Edit Mode tests:

```csharp
var runner = new VoskBatchTestRunner(commandDefinitions, commandSets);
var results = runner.RunAll(testCases);
Assert.IsTrue(results.AllPassed, results.FailureSummary);
```

This enables CI-safe regression testing of command definitions without Play Mode or audio hardware. It reuses the existing `VoskCommandParser` directly (the same path that `InjectText` uses internally).

### Test case format

```json
{
    "cases": [
        {
            "input": "launch all missiles target hotel one",
            "expectedIntent": "launch_weapon",
            "expectedSlots": { "target": "hotel_one" },
            "description": "Full launch command with target"
        },
        {
            "input": "hello world",
            "expectedIntent": null,
            "description": "Out-of-grammar phrase should be rejected"
        },
        {
            "input": "cease fire",
            "expectedIntent": "cease_fire",
            "wordConfidence": 0.3,
            "description": "Low confidence should be rejected by threshold"
        }
    ]
}
```

Optional `wordConfidence` field uses `CreateSimulatedWords()` to set uniform confidence for threshold testing.

### Design decisions

- **Builds on v3.0 and v3.2.** Uses `InjectText()` for execution and `VoskMatchDiagnostics` for result inspection. Minimal new code.
- **ScriptableObject test assets.** `VoskTestSuiteAsset` holds a list of test cases in the Inspector. Non-technical designers can author test cases alongside their command definitions. JSON import/export for portability.
- **No audio in batch mode.** Batch testing is text-only, deliberately. Audio replay (v3.4) is a separate feature for acoustic testing. Keeping them separate means the batch runner works in Edit Mode without Play Mode or mic dependencies.

### Files to create

| File | Description |
|------|-------------|
| `Runtime/Testing/VoskBatchTestRunner.cs` | Core runner. Instantiates a `VoskCommandParser`, feeds text, collects results. No MonoBehaviour dependency. |
| `Runtime/Testing/VoskTestCase.cs` | Data class: input, expected intent, expected slots, optional word confidence, description. |
| `Runtime/Testing/VoskTestResult.cs` | Data class: test case, actual intent, actual slots, score, pass/fail, diagnostics. |
| `Runtime/Testing/VoskTestSuiteAsset.cs` | ScriptableObject wrapping a `List<VoskTestCase>`. |
| `Editor/VoskBatchTestWindow.cs` | EditorWindow for visual test running. |
| `Tests/Editor/VoskBatchTestRunnerTests.cs` | Tests for the runner itself (meta-tests: verify the runner correctly reports pass/fail). |

### Files to modify

| File | Change |
|------|--------|
| `Runtime/Commands/VoskCommandParser.cs` | Possibly expose a `ParseWithDiagnostics()` method if not already available from v3.2 work. |

### Dependency

- Benefits significantly from v3.2 (`VoskMatchDiagnostics`). Can be implemented without it but would have less detailed failure reporting.

---

## v3.4 — Audio Capture & Replay

### Problem

Text injection tests the command parser but not VOSK's acoustic recognition. The Editor live mic tests acoustics but requires the developer to speak every time. There's no middle ground: capture a real utterance once, then replay it repeatedly while adjusting thresholds, aliases, or grammar. This matters most for:

- Debugging homophones ("to" vs "two") — requires actual audio, not text
- Tuning AGC parameters — need consistent input amplitude
- Sharing problematic utterances between team members ("can you reproduce this?")
- Building a regression corpus of known-tricky phrases

### What we're adding

**Recording:**
- A "Record" toggle button in the Debug Window (v3.2) or a standalone toolbar
- While recording, the raw audio from `EditorMicBackend` (post-capture, pre-DSP) is written to a ring buffer
- On stop, the buffer is saved as a 48 kHz mono 16-bit WAV file to a user-chosen path
- Optional: auto-save each utterance (between silence gaps) as a separate WAV

**Playback:**
- Load a WAV file and feed it through the full pipeline: Downsampler → AGC → VOSK → command parser
- Playback runs at real-time speed (or optionally faster, feeding chunks per-frame to match the `Tick()` cadence)
- The same events fire (`OnPartialResult`, `OnFinalResult`, `OnResult`, `OnCommandRecognised`) as if the audio were live
- Results appear in the Debug Window and are compatible with the batch test runner

**Replay asset:**

```csharp
// Runtime/Testing/VoskAudioTestCase.cs
[Serializable]
public class VoskAudioTestCase
{
    public AudioClip clip;           // or a path to a WAV file
    public string expectedIntent;
    public string description;
}
```

A `VoskAudioTestSuiteAsset` ScriptableObject holds a list of these for batch audio replay.

### Architecture

```
WAV file on disk
    ↓  EditorMicBackend.PlaybackFrom(float[] samples, int sampleRate)
    ↓  (feeds chunks into the same Tick() pipeline, replacing Microphone.GetPosition)
Downsampler.Process → Agc.Process → VOSK → result queue → events
```

The key insight is that `EditorMicBackend.Tick()` already processes a chunk of float samples per frame. Replay mode replaces the `AudioClip.GetData` source with a file-backed buffer and advances the read position by the same chunk size per tick. The rest of the pipeline is unchanged.

### Design decisions

- **48 kHz WAV only.** The pipeline expects 48 kHz input (the Downsampler is hardcoded to 48→16 kHz with a decimation factor of 3). Reject files at other sample rates with a clear error message rather than adding a resampler.
- **Pre-DSP recording.** Capture the raw mic signal before downsampling and AGC so that replayed audio goes through the full pipeline identically to live audio. This means DSP parameter changes are tested on replay.
- **No runtime overhead when not recording.** The recording buffer is only allocated when the Record button is pressed. The `Tick()` path has zero additional branches in non-recording mode.
- **WAV format, not a custom format.** WAV is universally readable, diffable, and can be inspected in Audacity. No need for a proprietary container.

### Files to create

| File | Description |
|------|-------------|
| `Editor/VoskAudioRecorder.cs` | Records raw audio chunks from `EditorMicBackend` to a WAV file. |
| `Editor/VoskAudioReplayer.cs` | Loads a WAV file and feeds chunks into `EditorMicBackend` in playback mode. |
| `Runtime/Testing/VoskAudioTestCase.cs` | Data class for audio test cases. |
| `Runtime/Testing/VoskAudioTestSuiteAsset.cs` | ScriptableObject for audio test suites. |

### Files to modify

| File | Change |
|------|--------|
| `Runtime/EditorMicBackend.cs` | Add a playback mode: `StartPlayback(float[] samples)` replaces `Microphone.Start`. `Tick()` reads from the sample buffer instead of `AudioClip.GetData`. Add `OnChunkProcessed` callback for the recorder to capture pre-DSP audio. |
| `Editor/VoskDebugWindow.cs` | Add Record/Stop and Load/Play buttons. Display playback progress bar. |

### Dependencies

- Requires v3.1 (`EditorMicBackend`) — extends its `Tick()` pipeline.
- Benefits from v3.2 (Debug Window) for UI integration and result visibility.
- Benefits from v3.3 (Batch Runner) for batch audio replay with expected-result assertions.

---

## v3.5 — Hot-Reload Command Definitions

### Problem

When tuning command definitions in Play Mode (editing a `VoskCommandAsset` in the Inspector, changing a slot alias, adjusting a threshold), the changes don't take effect until the developer exits and re-enters Play Mode. The grammar and parser state are built once during `Configure()` and remain static. This breaks the tight iteration loop that v3.0–v3.1 enabled: speak → see failure in Debug Window → tweak definition → have to stop and restart Play Mode → speak again.

### What we're adding

Automatic detection of ScriptableObject changes during Play Mode, triggering grammar rebuild and parser reconfiguration without leaving Play Mode.

**Mechanism:**

1. Subscribe to `UnityEditor.EditorApplication.update` (or use `AssetModificationProcessor` / `AssetPostprocessor`) to detect when a `VoskCommandAsset`, `VoskSlotAsset`, or `VoskCommandSetAsset` is modified during Play Mode.
2. On change detection, call `VoskCommandRecogniser.Configure()` again with the updated definitions. This rebuilds the grammar and parser.
3. If recognition is active, execute the existing stop → set grammar → start pattern (the same pattern used by `SetActiveSets()` in v2.4).
4. Log a Console message: `[VoskXR] Command definitions changed — grammar rebuilt (12 commands, 3 sets)`.

**Scope guard:** Only active in Play Mode in the Editor. No runtime impact. No file watching or polling in builds.

### Design decisions

- **Rebuilds the full grammar on any change.** Attempting to diff and patch the grammar incrementally is complex and error-prone. A full rebuild takes <1 ms for typical command sets. The ~50 ms audio gap from stop/start is acceptable for a development tool.
- **Debounced.** Multiple rapid changes (e.g., typing in a text field) are coalesced into a single rebuild after a short delay (~0.3 s) to avoid thrashing.
- **Opt-in.** A toggle in the Debug Window or a static bool `VoskCommandRecogniser.EnableHotReload`. Some developers may prefer explicit control. Defaults to on.

### Files to create

| File | Description |
|------|-------------|
| `Editor/VoskHotReloadWatcher.cs` | Monitors ScriptableObject changes in Play Mode. Triggers reconfiguration on `VoskCommandRecogniser`. |

### Files to modify

| File | Change |
|------|--------|
| `Runtime/Commands/VoskCommandRecogniser.cs` | Add `Reconfigure()` public method that re-runs `Configure()` with the current definitions and re-applies the active grammar. Reuses existing code paths. |

### Dependencies

- Requires v2.5 (ScriptableObject command authoring) — there's nothing to hot-reload without asset-based definitions.
- Benefits from v3.2 (Debug Window) — developers see the grammar rebuild and new match results immediately.

---

## What This Does NOT Include

Features explicitly deferred to later versions:

- **Standalone Windows / PCVR runtime** — v3.1 scope note applies. The Editor mic backend is architecturally the PCVR backend, but shipping it as a runtime requires device selection, hot-plug, and a PCVR test matrix. Deferred.
- **Partial result command preview** — v5 feature (real-time HUD showing what the system thinks you're saying). Requires flicker suppression and UI design beyond developer tooling.
- **Analytics / recognition stats** — v5 feature. The Debug Window shows per-utterance details; aggregate stats over time are a production concern.
- **Dialogue / contextual state** — v4 territory. Orthogonal to iteration speed.
- **CI audio replay** — Running audio tests in CI requires headless VOSK and no Unity Microphone. Possible future extension of v3.4 but out of scope here.

---

## Cumulative v3 Feature Map

| Version | Feature | What It Solves |
|---------|---------|----------------|
| v3.0 | Text Injection | Test command parsing without audio or device. ~80% of iteration. |
| v3.1 | Editor Live Mic | Test acoustic recognition without Quest deploy. Remaining ~20%. |
| v3.2 | Command Debug Window | See *why* commands match or fail. Eliminates diagnostic guesswork. |
| v3.3 | Batch Test Runner | Regression-test command definitions after changes. Repeatable, CI-safe. |
| v3.4 | Audio Capture & Replay | Capture tricky utterances once, replay while tuning. Sharable test corpus. |
| v3.5 | Hot-Reload Definitions | Edit command assets in Play Mode without restart. Tightest possible loop. |

After v3.5, the iteration workflow becomes:

1. Enter Play Mode once.
2. Speak (or replay a saved utterance).
3. See exactly what happened in the Debug Window.
4. Tweak a command definition in the Inspector — grammar rebuilds automatically.
5. Speak again (or replay). See the updated result instantly.
6. Run the batch test suite to verify nothing else broke.
7. Never leave Play Mode.
