# Voice Check Sample

A first-run microphone check: a live input-level meter, a phrase to say, and a
pass/fail readout on two independent checks -- is audio reaching the recogniser
at all, and did a command actually come out of it.

## Setup

1. **Import the sample** via Package Manager > VoXR Speech Recognition > Samples > Voice Check > Import.

2. **Download a VOSK model:**
   - Get [vosk-model-small-en-us-0.15](https://alphacephei.com/vosk/models) (~50 MB).
   - Place the `.zip` in `Assets/StreamingAssets/vosk-model-small-en-us-0.15.zip`.

3. **Open the scene** at `Assets/Samples/VoXR Speech Recognition/<version>/Voice Check/VoiceCheck.unity`.

4. **Run:**
   - **Windows Editor:** press Play. Wait for the model to load, then speak -- the bar should move. Say the prompted phrase to clear the second check. Click **Run check again** to reset both checks.
   - **Quest:** switch platform to Android (arm64), enable `RECORD_AUDIO` in Player Settings, build, deploy. The retry button is pointer-driven and works with controller raycast input.

## What's in the scene

| GameObject | Role |
|---|---|
| `Recogniser` | `VoxrSpeechRecogniser` with the default model path and AGC target. |
| `CommandRecogniser` | `VoxrCommandRecogniser` wired to `Recogniser`. Its grammar is supplied from code by `VoiceCheckDemo`, not from ScriptableObject assets. `Buffer Window` ships at `2` s -- the value the field's own tooltip recommends for Quest 3, where VOSK stretches inter-result gaps to ~1.9-2.1 s. At the `0.5` s PC default a two-word phrase can arrive as two separate results on a headset, which would fail the command check on a perfectly healthy device. Lower it to `0.5` if you only ever run this in the Editor and want the check to settle faster. |
| `VoiceCheckDemo` | Configures the check grammar, renders the prompt from it, polls `InputLevel` for the meter, and latches the two checks. |
| `Canvas/PromptText` | The phrase to say. Filled at runtime by joining the tokens of the first registered pattern -- so the prompt cannot drift from the grammar. |
| `Canvas/LevelBarTrack` | Meter background. `LevelBarTrack/LevelBarFill` is the moving bar; its width is driven from `InputLevel`. |
| `Canvas/AudioCheckText` | Check 1 -- audio reaching the recogniser. |
| `Canvas/CommandCheckText` | Check 2 -- a command was recognised. |
| `Canvas/StatusText` | Overall state: loading, listening, passed, or the error code and description on `OnError`. |
| `Canvas/RetryButton` | `Button` whose `onClick` calls `VoiceCheckDemo.Retry()`, clearing both latches. |

The recognised phrases are `voice check`, `microphone check`, and
`testing one two`. They live in `VoiceCheckDemo.Patterns`; the prompt is
rendered from the first of them. There is no public API to read the active
grammar back out of the recogniser, so the sample's own definitions **are** the
active grammar -- change `Patterns` and both the grammar and the prompt follow.

## What the checks mean

The two checks are deliberately independent, because the two failures they
catch have different fixes.

**Check 1 -- audio reaching the recogniser.** Latches the first frame
`InputLevel` rises above `Audio Seen Threshold` (default `0.02`). It says
nothing about whether anything was *understood*; only that samples are arriving.

*If the bar stays flat:* nothing is reaching the recogniser at all. On Quest,
`RECORD_AUDIO` is missing from the manifest or was denied at runtime. In the
Editor, no microphone is connected or the default Windows input device is set
to something silent. Both cases, and how to tell them apart, are in
[Troubleshooting](../../Documentation~/troubleshooting.md).

**Check 2 -- a command was recognised.** Latches on `OnCommandRecognised` for
the `voice_check` intent.

*If the bar moves but this check never passes:* either the mic gain is too low
for the AGC to rescue -- the bar twitches near the left edge while you speak,
rather than reaching a third or so of the track -- or the phrase was not said as
written. Say it exactly as the prompt shows it, one phrase at a time. If the bar
is healthy and the phrase is right, raise `micGainTargetDb` on the
`Recogniser` (the default is `-18` dBFS) and check the Console for the raw
transcript.

## Reading the level in your own game

`InputLevel` is a rolling ~300 ms RMS of the audio reaching the recogniser,
linear `0..1`. It allocates nothing and is safe to poll every frame from the
main thread; it reads `0` whenever nothing is being captured.

```csharp
using UnityEngine;
using UnityEngine.UI;
using VoXR;

public class MicMeter : MonoBehaviour
{
    [SerializeField] VoxrSpeechRecogniser recogniser;
    [SerializeField] RectTransform track;
    [SerializeField] RectTransform fill;

    void Update()
    {
        // 0.0-0.5 RMS maps to a full bar: speech rarely exceeds 0.3.
        float f = Mathf.Clamp01(recogniser.InputLevel * 3f);
        var size = fill.sizeDelta;
        size.x = track.rect.width * f;
        fill.sizeDelta = size;
    }
}
```

The same `* 3f` scaling is used by the Editor's Command Debug Window level
meters, so a reading here means the same thing as a reading there.

**The meter reads `0` on platforms with no capture backend.** Per the platform
support table in [Troubleshooting](../../Documentation~/troubleshooting.md), the
macOS and Linux Editors are text-injection only -- there is no live mic backend,
so no audio ever reaches the recogniser and both the meter and check 1 stay at
zero no matter how loudly you speak. That is expected, not a fault: use the
[Text Injection API](../../Documentation~/editor-testing.md) there instead.
Live capture is available on Quest and other Android arm64 devices, and in the
Windows Editor.

## See Also

- [Troubleshooting](../../Documentation~/troubleshooting.md) -- platform support table, permission and gain issues
- [SpeechRecogniser API](../../Documentation~/api/speech-recogniser.md) -- full event, property, and method reference
