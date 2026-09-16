# Data Types

Result and command data structures returned by the recognition pipeline.

## VoxrResult

`public readonly struct VoxrResult` -- Namespace: `VoXR`

The full recognition result with word-level data.

| Field | Type | Description |
|-------|------|-------------|
| `Text` | `string` | The full recognised text |
| `Words` | `VoxrWord[]` | Per-word confidence and timing for the best hypothesis (empty if unavailable) |

**Constructor** -- `public VoxrResult(string text, VoxrWord[] words)`

## VoxrWord

`public readonly struct VoxrWord` -- Namespace: `VoXR`

A single recognised word with metadata.

| Field | Type | Description |
|-------|------|-------------|
| `Text` | `string` | The recognised word |
| `Confidence` | `float` | Confidence score in range [0, 1] |
| `StartTime` | `float` | Start time in seconds from beginning of utterance |
| `EndTime` | `float` | End time in seconds from beginning of utterance |

**Constructor** -- `public VoxrWord(string text, float confidence, float startTime, float endTime)`

**`ToString()`** -- `"{Text} ({Confidence:F2})"`, e.g. `orient (0.91)`.

## VoxrCommand

`public readonly struct VoxrCommand` -- Namespace: `VoXR.Commands`

A parsed command with intent and extracted slots.

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `Intent` | `string` | The matched command intent (e.g. `"launch_weapon"`) |
| `Slots` | `VoxrSlotMatch[]` | Matched slot name/value pairs |
| `Confidence` | `float` | Minimum word confidence across matched tokens, each read at its own position in the transcript. `-1` means no word data was available *for the matched span* — which is not always the same as the utterance carrying none; see [the two gates](../scoring.md#minconfidence-default-04). |
| `Score` | `float` | Pattern match quality (0.0--1.0). Higher is better. |
| `RawText` | `string` | The original VOSK transcript text |
| `MatchedPatternIndex` | `int` | Index into the definition's `Patterns` array identifying which pattern produced this match. `-1` when unavailable. |
| `ResolvedSlots` | `VoxrResolvedSlot[]` | The slots a registered [slot resolver](command-recogniser.md#methods) filled because the speaker omitted them, each with the reason the resolver gave. Empty for a command whose slots were all spoken — and empty on every command when no resolver is registered. The values live in `Slots` alongside the spoken ones, **appended after** them rather than interleaved, so a handler that does not care reads them through `GetSlot` and never learns the difference. |

**Constructor** -- `public VoxrCommand(string intent, VoxrSlotMatch[] slots, float confidence, float score, string rawText, string[] registeredSlotNames = null, int matchedPatternIndex = -1)`. The two optional parameters change behaviour: `registeredSlotNames` is the list `GetSlot` checks a name against, so leaving it `null` disables the typo warning entirely, and `matchedPatternIndex` defaults to `-1` (unavailable). A `null` `slots` becomes an empty array. `ResolvedSlots` is not a constructor parameter: a command you construct yourself always reports no resolver-filled slots.

### Methods

| Method | Description |
|--------|-------------|
| `GetSlot(string name)` | Returns the value of a named slot, or empty string if not matched. In a `DEBUG` build, logs a warning if the slot name was not registered -- but only when the command carries a registered-slot-name list, so a command rebuilt by follow-up slot fill never warns. For a `NumberSequence` slot the value is the number words as spoken — `"two seven zero"`, not `"270"` (see below); for an `Enumerated` slot it is the canonical value, with any spoken alias already resolved. |
| `HasSlot(string name)` | Returns true if the named slot was matched in this command. |
| `GetSlotResolutionReason(string name)` | The reason a slot resolver gave for filling the named slot, or `null` when the slot was not resolver-filled — because the speaker said it, because it is not a slot of this command, or because no resolver is registered. **Non-null is the discriminator**, not the string's content: `""` means the slot *was* resolver-filled and the resolver stated no reason. |
| `ToString()` | `"{Intent} ({Slots.Length} slots, score={Score:F2})"`, e.g. `orient_heading (1 slots, score=0.92)`. |

> **NumberSequence slots return spoken words, not digits.** `int.TryParse` on the returned value fails on every utterance and yields `0` without throwing. Convert with [`VoxrNumberParser`](number-parser.md) — `ParseDigitSequence` for digit-by-digit values, `ParseCardinal` for cardinal phrases. The canonical fallback snippet is in [Command Recognition → NumberSequence Slots](../command-recognition.md#numbersequence-slots).

## VoxrSlotMatch

`public readonly struct VoxrSlotMatch` -- Namespace: `VoXR.Commands`

A single slot extraction result.

| Field | Type | Description |
|-------|------|-------------|
| `Name` | `string` | Slot name (e.g. `"weapon"`) |
| `Value` | `string` | Matched value. For an `Enumerated` slot this is the canonical value (e.g. `"missiles"`), with any spoken alias already resolved (`"jackals"` → `"jackal"`). For a `NumberSequence` slot it is the number words as spoken (`"two seven zero"`), not a numeric string — convert with [`VoxrNumberParser`](number-parser.md). |

**Constructor** -- `public VoxrSlotMatch(string name, string value)`

**`ToString()`** -- `"{Name}={Value}"`, e.g. `weapon=missiles`.

## VoxrSlotResolutionRequest

`public readonly struct VoxrSlotResolutionRequest` -- Namespace: `VoXR.Commands`

The question a [slot resolver](command-recogniser.md#methods) is asked: which unfilled required slot, and which command is asking. Registration is per slot and slot names are **global**, so `{track}` in a destructive command is the same slot name as `{track}` in a harmless one — this is what lets one resolver answer the first and refuse the second.

| Field | Type | Description |
|-------|------|-------------|
| `SlotName` | `string` | The unfilled required slot, spelled exactly as in the pattern and in `VoxrSlotDefinition.Name`. Never null |
| `Intent` | `string` | The intent of the command that would fire if this slot were filled. The field to branch on when a resolver must serve one command and refuse another. It describes the command that would fire, **not** what firing it does — knowing which of your own intents are destructive is still your job, and a renamed or newly added intent is caught by nothing |
| `MatchedPatternIndex` | `int` | Which of that command's patterns matched, as an index into the definition's `Patterns` — the same value as `VoxrCommand.MatchedPatternIndex`. Always a real index here: a command whose index cannot address a pattern is never offered for resolution, so the `-1` that field carries elsewhere never reaches a resolver |

**Constructor** -- `public VoxrSlotResolutionRequest(string slotName, string intent, int matchedPatternIndex)`

These three fields are also the recogniser's per-utterance memo key: two asks differing in any of them are put separately, so no answer is ever served for a different question. The raw transcript is deliberately absent — a resolver that wants the words rather than the slot is doing the parser's job.

## VoxrSlotResolution

`public readonly struct VoxrSlotResolution` -- Namespace: `VoXR.Commands`

A game's answer to one `VoxrSlotResolutionRequest`: a value, or nothing, plus a short reason.

| Member | Type | Description |
|--------|------|-------------|
| `Value` | `string` | The value to fill the slot with. Null or empty means this is **not** a resolution — the slot stays unfilled and the command is ruled incomplete exactly as it is today. Not validated: it is not checked against the slot's registered values and not filtered through a value provider's active set |
| `Reason` | `string` | Why the game chose this value (`"main target"`, `"only hostile"`). Opaque to the package — carried through to `VoxrCommand.GetSlotResolutionReason` and the Editor diagnostics for crew readback and debugging, never parsed or validated. May be null, which reads as "filled, reason unstated" |
| `HasValue` | `bool` | True when this is a resolution at all. **Derived** from `Value` rather than stored, so `default(VoxrSlotResolution)` and a resolution carrying an empty value both mean "none" with no second way to say it |
| `None` | `static VoxrSlotResolution` | The "I do not know" answer. Returning `default` is equivalent |

**Constructor** -- `public VoxrSlotResolution(string value, string reason)`

The package remembers nothing between questions: a resolver is asked afresh on every utterance, so a resolution cannot go stale. Within one utterance an answer is reused only for a question identical in every field of its request.

## VoxrResolvedSlot

`public readonly struct VoxrResolvedSlot` -- Namespace: `VoXR.Commands`

The record that a slot was filled by a resolver rather than spoken.

| Field | Type | Description |
|-------|------|-------------|
| `Name` | `string` | The slot that was filled |
| `Reason` | `string` | The reason the resolver gave. A null `Reason` on the resolution is normalised to `string.Empty` here, so this is never null on a recorded entry — `""` means "filled, reason unstated" |

**Constructor** -- `public VoxrResolvedSlot(string name, string reason)`

The **value** is not repeated here — it is in `VoxrCommand.Slots`, where a handler reads it through `GetSlot` without knowing or caring how it got there.

## VoxrCommandResult

`public readonly struct VoxrCommandResult` -- Namespace: `VoXR.Commands`

Parser output wrapping match/no-match.

| Field | Type | Description |
|-------|------|-------------|
| `IsMatch` | `bool` | True if a command pattern matched |
| `Command` | `VoxrCommand` | The parsed command (only valid when `IsMatch` is true) |
| `RawText` | `string` | The original VOSK transcript |

**Constructors** -- `public VoxrCommandResult(VoxrCommand command)` sets `IsMatch` true and takes `RawText` from the command; `public VoxrCommandResult(string rawText)` sets `IsMatch` false and leaves `Command` at its default.

## VoxrListeningMode

`public enum VoxrListeningMode` -- Namespace: `VoXR`

How `VoxrPushToTalkController` gates the speech recogniser. Read and written at runtime through its `ListeningMode` property, and set initially in the Inspector.

| Value | Int | Description |
|-------|-----|-------------|
| `Continuous` | 0 | Recognition runs whenever the controller is enabled; `PressTalk()` and `ReleaseTalk()` become no-ops. |
| `PushToTalk` | 1 | Recognition only runs between `PressTalk()` and `ReleaseTalk()`. The controller's default. |

See [Push-to-Talk → Listening Modes](../push-to-talk.md#listening-modes) for the setter semantics of switching mode at runtime.

## VoxrPendingTimeoutBehavior

`public enum VoxrPendingTimeoutBehavior` -- Namespace: `VoXR.Commands`

Determines what happens when a pending command's timeout expires.

| Value | Description |
|-------|-------------|
| `Cancel` | The pending command is cancelled and discarded. `OnCommandCancelled` fires. |
| `FireAsIs` | The pending command fires as-is with whatever slots were filled. `OnCommandConfirmed`, `OnCommandRecognised`, and `OnCommandsRecognised` (as a single-element batch) fire, and the intent's debounce window is recorded. |

`FireAsIs` is one of the two [deliberate exceptions](../command-recognition.md#the-two-ways-an-incomplete-command-still-fires) to the rule that a command missing a required argument does not fire — but only for commands whose definition sets `allowPartialMatch`. A pending that is merely awaiting confirmation always holds a complete command, so `FireAsIs` on its own never fires an incomplete one. Where both apply, the handler must tolerate every required slot being absent.

**`FireAsIs` does not apply to a pending disambiguation.** There the *intent* is unknown, not the arguments — `FireAsIs` means "the command is known, fire it with what I have", which is a different situation wearing the same flag. Firing the first-registered candidate after a pause would be the same coin flip the question was asked to avoid, merely later. An unanswered ambiguity cancels under either setting.

## VoxrPendingAmbiguity

`public readonly struct VoxrPendingAmbiguity` -- Namespace: `VoXR.Commands`

What the recogniser is asking about when a pending command is a sibling-tie disambiguation rather than a confirmation. Read it from `VoxrCommandRecogniser.PendingAmbiguity` inside an `OnCommandPending` handler. Only ever non-null with [`disambiguateSiblingTies`](command-recogniser.md#inspector-fields) enabled.

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `Choices` | `VoxrCommand[]` | The commands the recogniser could not tell apart. `Choices[i]` is what fires if the speaker says `DiscriminatingValues[i]`. Index 0 is the candidate that would have fired with the flag off; beyond that the order is registration order and stable across runs |
| `DiscriminatingValues` | `string[]` | The one word that tells each choice apart — what the speaker says to pick it. Already in the decoder's grammar, because these are pattern literals |
| `IsTruncated` | `bool` | An answer the speaker could have given is **not** on this list, so `Choices` is not the whole set of things they might have meant. Worth wording into the prompt — "…or say the whole command again" — because re-uttering is the only way to reach what is missing |

### Reading it

`HasValue` is the reason signal, and checking it is not optional. `OnCommandPending` carries only a `VoxrCommand`, so an integrator already subscribed for `requiresConfirmation` will otherwise prompt "yes/no" at a speaker who needs to say a *word* — and "yes" does nothing under a disambiguation, so the pending sits until it times out and then fires nothing.

```csharp
recogniser.OnCommandPending += cmd =>
{
    var ambiguity = recogniser.PendingAmbiguity;
    if (ambiguity.HasValue)
    {
        // "Did you mean mode or level?"  — the speaker answers with one word.
        Prompt("Did you mean " + string.Join(" or ", ambiguity.Value.DiscriminatingValues)
               + (ambiguity.Value.IsTruncated ? ", or say the whole command again?" : "?"));
    }
    else
    {
        Prompt("Confirm " + cmd.Intent + "?");   // yes / no
    }
};
```

The arrays are allocated once when the pending is entered and are safe to retain, but they are the live pending's own arrays rather than copies — **do not write to them**, as that would change which word resolves the question and what fires when it does.

Answering is ordinary follow-up speech. The value is matched as a *whole* utterance, so "set alpha mode on" is a re-utterance that preempts the question rather than an answer to it. Cancel vocabulary keeps its precedence, so a discriminating value that is also a cancel word cancels instead of choosing — the grammar author is warned about that collision at construction.

## See Also

- [VoxrSpeechRecogniser](speech-recogniser.md) -- produces `VoxrResult` via `OnResult`
- [VoxrCommandRecogniser](command-recogniser.md) -- produces `VoxrCommand` via `OnCommandRecognised`
- [Command Definitions](command-definitions.md) -- defining patterns and slots
- [Number Parser](number-parser.md) -- converting `NumberSequence` slot values returned by `GetSlot`
- [Command Recognition](../command-recognition.md) -- how matching works
