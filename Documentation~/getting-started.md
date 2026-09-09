# Getting Started

Everything you need to install the SDK, set up a VOSK model, and get your first speech recognition or voice command working in Unity.

---

## Requirements

- **Unity 6 (6000.0+)** -- the package targets the Unity 6 runtime.
- **Android arm64** for device builds -- the native bridge ships for that ABI only, so Quest 2/3/Pro and other Android arm64 headsets are the supported deployment targets.
- **Windows Editor (x86_64)** for live-microphone testing in the Editor -- on macOS and Linux the Editor is limited to text injection.

Full per-platform detail, including what is deferred and what is untested, is in the [platform support table](troubleshooting.md#platform-support).

---

## Installation

### Via Git URL

1. Open Unity Package Manager (Window > Package Manager).
2. Click **+** > "Add package from git URL..."
3. Enter: `https://github.com/jinwoo1601/VoXR-Speech-Recognition.git`

To pin a specific version (recommended):

```
https://github.com/jinwoo1601/VoXR-Speech-Recognition.git#v2.0.0
```

### Via manifest.json

Add to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.jinwoo1601.voxr": "https://github.com/jinwoo1601/VoXR-Speech-Recognition.git#v2.0.0"
  }
}
```

---

## Model Setup

The SDK does not include VOSK models. You must download one separately.

1. Visit [VOSK Models](https://alphacephei.com/vosk/models).
2. Download `vosk-model-small-en-us-0.15` (~50 MB) or another compatible model.
3. Place the `.zip` archive at `Assets/StreamingAssets/vosk-model-small-en-us-0.15.zip`.

The SDK extracts the model to `Application.persistentDataPath/VoxrModels/<modelName>`, where `<modelName>` is the archive's file name without the `.zip`. Extraction unpacks to a temporary sibling folder and finishes with an atomic rename, so an interrupted extraction cannot leave a half-written cache behind.

A cached extraction is reused only when it passes validation **and** its stamp agrees with the archive, and the SDK establishes that in two tiers. First it identifies the *source* -- the file the archive lives in, which on Android is the APK the assets were packed into and elsewhere is the `.zip` itself -- with a single `stat`. If the stamp already records that same identity, the cache is served immediately and the archive is never opened at all; that is the ordinary cached launch. Only when this does not settle the question -- no stamp, no recorded hash, no recorded identity, or an identity that has moved -- does the SDK read the archive and compute a SHA-256 hash of its bytes, as every launch used to. If the recorded **hash** still matches, the cache is kept and its recorded identity is refreshed in place: nothing is re-extracted. Only a **hash** mismatch, or a cache whose stamp records no hash for the SDK to compare, re-extracts, keeping the existing cache in place as the working model and deleting it only once the replacement has been extracted, validated and stamped, immediately before the atomic rename.

The identity check is deliberately one-sided: it over-invalidates rather than under-invalidating. A source whose identity moved while its bytes did not costs one read and one hash and no re-extraction -- reinstalling the same build is exactly that case. How strong the other direction is depends on what the identity is taken over. On Android the archive is packed inside the APK and an install always replaces that file, so a changed archive cannot fail to move the identity. In the Editor and in standalone builds the `.zip` itself is the source, and its identity is its path, byte length and last-write time -- so a changed archive moves it as long as the write updates the timestamp, which an ordinary copy, build, download or save does. So shipping a different model `.zip` under the same file name, or editing the `conf/model.conf` inside it, still takes effect on the next launch instead of being ignored on any device that had already run the app. The one replacement that escapes this -- same byte length *and* a deliberately preserved last-write time, which needs something like `touch -r` or a timestamp-normalising build pipeline -- is recorded in `KNOWN_LIMITATIONS.md`; forcing a re-extraction (below) is the remedy.

The stamp is a `.voxr-model-stamp` file inside the extracted folder. An extraction writes it into the temporary folder before the rename that publishes it, so an extraction can never publish a cache stamped with a key it does not match; the identity line is later refreshed in place, in the already-published folder, and a refresh interrupted mid-write leaves a stamp that reads as no match -- which costs one re-extraction on the next launch, never a stale serve. It holds up to two lines: the archive hash, the string `sha256:` followed by lowercase hex, and -- whenever the source could be identified -- a second line, `src:` followed by the source-identity token. **That token is opaque.** It is not a format to parse. Hand-editing the `src:` line only makes the stamp lie about a cache the SDK will then go on serving; deleting or corrupting the hash line has the opposite effect, since a stamp with no readable hash satisfies neither tier and the SDK re-extracts.

**Upgrading an existing install costs one re-extraction.** A cache extracted by an earlier version of the SDK carries no stamp, which reads as a mismatch: the first launch after the upgrade extracts the model once more, then stamps it. It is not an error and it does not repeat.

**To force a re-extraction** -- after an upgrade, after a hand-edit, or in the one recorded case where a replacement archive's identity did not move -- delete the extracted model folder under `Application.persistentDataPath/VoxrModels/`, or just the `.voxr-model-stamp` inside it, or clear the app's data; the next launch extracts from scratch. Hand-edits to the *extracted* copy are invisible to both tiers -- the stamp describes the archive, not the extracted tree -- so an edited `conf/model.conf` in the cache is neither noticed nor protected, and the next re-extraction silently replaces it. Tune the `.zip` in `StreamingAssets` instead.

Any VOSK-compatible model works. Larger models improve accuracy at the cost of memory and download size.

### Model Validation

The SDK validates extracted models by checking for:
- `am/final.mdl`
- `conf/mfcc.conf`
- `conf/model.conf`
- `graph/` directory

`conf/model.conf` holds the decoder's live configuration -- beam, lattice beam, and the endpointer's trailing-silence rules -- and is now part of that list. A model archive that genuinely lacks it fails validation and raises `ModelLoadFailed` where it previously passed.

If an existing cache fails validation, the SDK re-extracts immediately, within the same call -- no restart is needed to recover a bad cache -- and the old copy is replaced only once the new one is extracted, validated and stamped. A **hash** mismatch takes the same path. A source-identity mismatch on its own does not: the SDK reads and hashes the archive, finds the bytes unchanged, refreshes the recorded identity and keeps the cache.

If the freshly extracted copy fails validation, the archive itself is the problem: the SDK deletes the partial extraction, raises `ModelLoadFailed`, and returns no model. A previously extracted cache is left untouched by that failure -- it is replaced only once a replacement has passed validation -- so a corrupt archive costs you an error rather than the model the device already had. It is not loaded in the replacement's place, though: the stamp still mismatches, so the re-extraction is attempted and fails again on **every** launch -- there is no next-launch self-heal for a corrupt archive. Replace the `.zip` in `StreamingAssets` to fix it.

If the archive cannot be read at all -- missing, or unreadable -- but a cached extraction is present and valid, that cache is still used; only its freshness goes unchecked. `ModelLoadFailed` is raised only when there is no usable cache to fall back on, and the message tells the two apart: a missing archive reports `Model archive not found in StreamingAssets: <modelName>.zip`, while a read that failed reports that the archive could not be read and names the underlying reason. Before raising, this path cleans up after itself: a cache that failed validation is deleted, and a stale `.tmp_<modelName>` directory left by an interrupted extraction is swept.

---

## Quick Start -- Transcription

Attach a `VoxrSpeechRecogniser` component to a GameObject (**Add Component > VoXR > Speech Recogniser**), then drag that component into the script's `recogniser` field in the Inspector. The field is a serialised reference and nothing looks the component up for you -- an unassigned field throws a `NullReferenceException` on the first event subscription.

With the reference assigned, subscribe to its events:

```csharp
using UnityEngine;
using VoXR;

public class VoiceDemo : MonoBehaviour
{
    [SerializeField] VoxrSpeechRecogniser recogniser;

    void OnEnable()
    {
        recogniser.OnPartialResult += text => Debug.Log($"Partial: {text}");
        recogniser.OnFinalResult += text => Debug.Log($"Final: {text}");
        recogniser.OnResult += result =>
        {
            foreach (var word in result.Words)
                Debug.Log($"  {word.Text} conf={word.Confidence:F2} [{word.StartTime:F2}-{word.EndTime:F2}]");
        };
        recogniser.OnError += (code, msg) => Debug.LogError($"VOSK [{code}]: {msg}");
        recogniser.StartRecognition();
    }

    void OnDisable()
    {
        recogniser.StopRecognition();
    }
}
```

`OnPartialResult` fires continuously as you speak. `OnFinalResult` fires at utterance boundaries with the complete transcript. `OnResult` provides the same final text plus per-word confidence scores and timing.

---

## Quick Start -- Commands

Add a `VoxrCommandRecogniser` component alongside your `VoxrSpeechRecogniser` (**Add Component > VoXR > Command Recogniser**), and drag **both** components into the script's serialised fields in the Inspector -- as above, neither is resolved automatically. Then define slots (allowed values) and commands (patterns that reference those slots):

```csharp
using UnityEngine;
using VoXR;
using VoXR.Commands;

public class CommandExample : MonoBehaviour
{
    [SerializeField] VoxrSpeechRecogniser recogniser;
    [SerializeField] VoxrCommandRecogniser commandRecogniser;

    void Start()
    {
        var targets = VoxrSlotDefinition.OneOf("target", "alpha one", "bravo two", "hotel one");
        var weapons = VoxrSlotDefinition.OneOf("weapon", "missiles", "torpedoes");

        var commands = new[]
        {
            new VoxrCommandDefinition("launch_weapon",
                new[] { new[] { "launch", "{weapon}", "target", "{target}" } }),
            new VoxrCommandDefinition("cease_fire",
                new[] { new[] { "cease", "fire" } }),
        };

        commandRecogniser.Configure(new[] { targets, weapons }, commands);
        commandRecogniser.OnCommandRecognised += cmd =>
        {
            Debug.Log($"Intent: {cmd.Intent} score={cmd.Score:F2}");
            Debug.Log($"  target={cmd.GetSlot("target")} weapon={cmd.GetSlot("weapon")}");
        };

        recogniser.StartRecognition();
    }
}
```

When the user says "launch missiles target alpha one", the `OnCommandRecognised` event fires with `Intent="launch_weapon"`, `weapon="missiles"`, and `target="alpha one"`.

---

## Understanding the Lifecycle

The SDK uses a two-tier lifecycle that separates the expensive model load from the cheap audio start/stop.

### Heavyweight (model load / teardown)

- `Initialise()` / `InitialiseAsync()` -- loads the VOSK model and creates the recogniser. Takes seconds only when the model genuinely has to be extracted from StreamingAssets: the first launch, and any launch on which the archive's bytes changed. An ordinary cached launch costs a single `stat` of the file the archive lives in and no archive I/O at all. On a Quest 3, hashing the archive on every launch cost 1303 ms of time-to-voice-ready (Development build, so an upper bound), of which ~719 ms was that read and hash; removing it should leave ~584 ms, a figure derived from those two rather than measured on device. Where the source's identity moved but its bytes did not -- a reinstall, most commonly -- the archive is read and hashed once and the cache is kept; budget for that read there and on the first launch, not on every launch. That read is not all off the main thread. The hash always runs on a thread-pool thread, and the read joins it there in the Editor and in standalone builds; on Android the archive comes through `UnityWebRequest`, whose transfer is awaited on a completion callback that does not hold the frame, leaving roughly 19 ms of main-thread work to materialise the ~39 MB archive as a managed byte array.
- `ReleaseNativeResources()` -- frees all native resources. Called automatically by `OnDestroy()`.

The native bridge is one per process, so **only one `VoxrSpeechRecogniser` can be initialised at a time**. A second one logs an error and stays inert rather than sharing the first's model; see [VoxrSpeechRecogniser](api/speech-recogniser.md#one-recogniser-per-process).

### Lightweight (audio stream start / stop)

- `StartRecognition()` / `StartRecognitionAsync()` -- opens audio stream, starts recognition. Milliseconds. Calls `Initialise()` if needed.
- `StopRecognition()` -- stops audio, joins recognition thread. Model stays loaded.

This separation enables push-to-talk without model reload:

```
Initialise() --> StartRecognition() --> StopRecognition() --> StartRecognition() --> ...
    slow              fast                   fast                   fast
```

On Android, audio is captured on a native thread via JNI `AudioRecord`. Results are queued and delivered on Unity's main thread during `Update()`. In the Windows Editor, `EditorMicBackend` captures via `UnityEngine.Microphone` and processes synchronously on the main thread.

---

## Next Steps

- [Command Recognition](command-recognition.md) -- Learn how the full command parsing pipeline works, including patterns, slots, scoring, and grammar modes
- [Command Sets](command-sets.md) -- Organise commands into switchable groups for mode-specific grammars
- [Inspector Authoring](inspector-authoring.md) -- Set up commands without writing code using ScriptableObject assets
- [Editor Testing](editor-testing.md) -- Iterate without deploying to Quest using the debug window, session debug log, live mic, and text injection
- [Push-to-Talk and Error Handling](push-to-talk.md) -- Implement push-to-talk and handle errors gracefully
