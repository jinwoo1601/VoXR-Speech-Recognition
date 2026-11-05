# Endpointer Profile

VOSK decides that an utterance has ended with its endpointer: trailing-silence rules read from the model's `conf/model.conf` when the decoder loads. `VoxrSpeechRecogniser`'s **Endpointer Profile** field picks a named preset for three of those rules, and every model load writes the preset into the extracted model's conf before the decoder reads it. The model archive in `StreamingAssets` is never modified.

---

## Presets

The field takes a [`VoxrEndpointerProfile`](api/data-types.md#voxrendpointerprofile) and defaults to `Automatic`.

| Preset | `--endpoint.rule2.min-trailing-silence` | `--endpoint.rule3.min-trailing-silence` | `--endpoint.rule4.min-trailing-silence` |
|--------|------|------|------|
| `ModelDefault` | the archive's own | the archive's own | the archive's own |
| `Responsive` | `0.45` | the archive's own | the archive's own |

`Automatic` is not a third set of values. It lets the listening mode choose at load:

| Listening mode when the load begins | Preset |
|-------------------------------------|--------|
| `Continuous` | `Responsive` |
| `PushToTalk` | `ModelDefault` |
| No `VoxrPushToTalkController` driving the recogniser | `Responsive` -- a recogniser nobody has handed a mode counts as `Continuous` |

An explicit `ModelDefault` or `Responsive` overrides the mode in both cases.

For `vosk-model-small-en-us-0.15`, whose own values are rule2 `0.5`, rule3 `0.75` and rule4 `1.0`, `Responsive` moves rule2 alone, from `0.5` to `0.45` s.

**Why the two modes differ.** In Push-to-talk, releasing the button stops recognition, and stopping forces the decoder's final result -- the release ends the command, and the endpointer can only split a press into more results, so the model's own values stand. In Continuous mode the endpointer is what ends a command, and `Responsive` ends it after less trailing silence.

**No raw values.** Nothing public takes a raw number. A preset's values are held as decimal text, so the conf it writes is the same bytes whichever runtime writes it.

---

## What a model load does

1. **Resolve.** When a load begins -- `InitialiseAsync()`, which `Initialise()` and a first `StartRecognition()` both reach -- the recogniser resolves the profile from the field and the listening mode it was last handed (see [When the mode is read](#when-the-mode-is-read)).
2. **Serve or extract.** The model is served from the cache or extracted and validated, exactly as [Model Setup](getting-started.md#model-setup) describes.
3. **Overlay.** On the extracted model, before the decoder loads:
   - The first load that finds no `.voxr-archive-model.conf` in the model folder takes one: a byte copy of the extracted `conf/model.conf`, made before anything rewrites it -- the archive's own conf.
   - The effective conf is computed from that copy and the preset alone, never from the conf currently on disk.
   - `conf/model.conf` is replaced only when its bytes would change, so a load under an unchanged preset writes nothing.
4. **Log.** One line, [below](#the-load-time-log-line).
5. **Load the decoder.** The Windows Editor backend or the device bridge loads the model and reads the conf, which holds until the next load.

**Why a copy.** The ordinary cached launch never opens the archive -- that is the cost the [cache stamp](getting-started.md#model-setup) removes -- so the archive's conf is kept beside the model instead of being read from the `.zip` at every launch. A changed archive, or a forced re-extraction, replaces the whole model folder, copy included, and the next load takes a fresh copy from the new conf. The `.voxr-model-stamp` is never touched and validation does not look at the copy, so applying a profile never causes a re-extraction.

**Writes are atomic.** The copy and the conf are each written to a `.voxr-tmp` file beside their target and then swapped into place, so an interruption leaves the old file or the new one, never a half-written conf.

### The rewrite

- **Three keys only:** `--endpoint.rule2.min-trailing-silence`, `--endpoint.rule3.min-trailing-silence` and `--endpoint.rule4.min-trailing-silence`. Every other line -- rule1, rule5, beam, lattice beam, the rest -- passes through byte for byte, line terminators included.
- A line belongs to a key when its trimmed text begins `<key>=`. A preset value replaces that line's text with `<key>=<value>` and keeps its terminator.
- A key the preset sets but the conf lacks is appended at the end, using the conf's own line terminator.
- Where the preset says "the archive's own", the key is left exactly as it is, present or absent. Under `ModelDefault` the effective conf is therefore the archive's conf, byte for byte -- including after a load under `Responsive`.

**When the conf has a problem.** If the copy is not valid UTF-8, if one of the three keys appears more than once, or if one's value is not a non-negative decimal (digits, optionally followed by `.` and more digits), the conf is left as the archive's own, unchanged, and the log line becomes a warning naming the reason -- under `Responsive`, that the preset was not applied; under `ModelDefault`, which writes no values, that there was nothing to apply but the conf has a problem. A model with no `conf/model.conf` at all, which [validation](getting-started.md#model-validation) normally rules out, is reported the same way, with every value `absent`. A file-system failure during the overlay is logged as not applied under either preset, and the decoder reads whatever conf is on disk. No load fails because of the profile.

---

## The load-time log line

Each load that reaches the model logs one line through `Debug.Log`, after the overlay and before the decoder loads:

```
[VoxrSpeechRecogniser] Endpointer profile <preset> (<explicit choice | Continuous default | Push-to-talk default>): rule2=<v> rule3=<v> rule4=<v>
```

The values are read from the effective conf, and `absent` stands for a key the conf has no line for. With `vosk-model-small-en-us-0.15` under `Automatic` in Continuous mode:

```
[VoxrSpeechRecogniser] Endpointer profile Responsive (Continuous default): rule2=0.45 rule3=0.75 rule4=1.0
```

When the overlay hits a problem, the line gains a tail naming the reason and the conf's path, and is logged through `Debug.LogWarning` instead. Under `Responsive`, and after a file-system failure under either preset, the tail is `— not applied: <reason> (<conf path>)`. Under `ModelDefault`, which has no values to write, any other problem gives `— nothing to apply, but the conf has a problem: <reason> (<conf path>)`.

---

## When the mode is read

Under `Automatic` the mode is fixed when the load **begins**, and it holds for that load.

`VoxrPushToTalkController` hands its mode to the recogniser it drives in its own `Awake`, again immediately before each of its calls that can begin a load or start recognition -- `Start` with **Initialise On Start**, an enable, a resume from `OnApplicationPause`, `PressTalk()`, a switch to `Continuous` -- and whenever its mode changes. When the controller is destroyed the recogniser goes back to `Continuous`. The recogniser holds no reference to the controller, so a scene without one loads as `Continuous`.

A mode change after the model has loaded does not touch the conf. It reaches the **next** load: call `ReleaseNativeResources()` and then `Initialise()` to apply it now. The orderings in which a load begins before the controller has handed over the mode you expect are recorded in [Known Limitations](../KNOWN_LIMITATIONS.md#under-automatic-the-endpointer-profile-is-the-listening-modes-at-the-moment-the-model-load-begins); an explicit preset sidesteps all of them.

---

## Tuning

- **Choose a preset** in the recogniser's **Endpointer Profile** field, and confirm it in the load-time log line.
- **For the model's own values** in every mode, choose `ModelDefault`.
- **For any other value**, or any other key of the conf, edit `conf/model.conf` inside the `.zip` in `StreamingAssets`. A changed archive is re-extracted on the next launch ([Model Setup](getting-started.md#model-setup)) and its conf becomes the new copy: under `ModelDefault` your values hold exactly; under `Responsive` rule2 is `0.45` and the rest are yours.
- **Not the extracted copy.** Every load rewrites the extracted `conf/model.conf` from the copy, so an edit there is reverted at the next load. `.voxr-archive-model.conf` is the SDK's own record: editing it or deleting it is a cache hand-edit, and a deleted copy is re-taken from the conf as it then stands -- after a `Responsive` load, that is the `Responsive` conf. Force a re-extraction to recover ([Model Setup](getting-started.md#model-setup), *To force a re-extraction*).

---

## See Also

- [VoxrSpeechRecogniser](api/speech-recogniser.md#inspector-fields) -- the `endpointerProfile` field
- [Data Types](api/data-types.md#voxrendpointerprofile) -- `VoxrEndpointerProfile`
- [Push-to-Talk](push-to-talk.md#listening-modes) -- listening modes and the controller
- [Getting Started](getting-started.md#model-setup) -- the model cache, its stamp, and forcing a re-extraction
- [Known Limitations](../KNOWN_LIMITATIONS.md) -- hand-edits to the extracted conf, and when the mode is read
