# v4: Dialogue & Contextual State

## Context

v1 solved hearing (offline speech recognition on Quest). v2 solved understanding (stateless command parsing — pattern matching, scoring, slots, utterance buffering, command sets, inspector authoring). v3 solved iteration speed (text injection, Editor live mic). All shipped and stable.

The gap: every utterance is parsed in complete isolation. The parser has zero memory of what came before. This makes multi-turn voice interaction impossible — you can't say "launch missiles" then follow up with "target hotel one", you can't ask for confirmation before a high-stakes command fires, and slot values are frozen at definition time even though the game world changes constantly.

v4 introduces **state that persists across utterances**, making the SDK capable of dialogue rather than dictation.

---

## Theme

**Conversation** — the SDK remembers what was just said and what the game world currently looks like, so voice commands can span multiple turns and respond to live state.

---

## Versions

### v4.0 — Dynamic Slots

Slot values come from a live game query instead of a static list.

**Problem:** In a naval sim, valid targets change constantly. Ships sink, new contacts appear. Static slots force developers to pre-enumerate every possible target, which is impractical and degrades recognition accuracy (larger grammar = more word confusion).

**Key design decision — parser-only updates by default.** Today `RebuildParserAndGrammar` couples two operations that have very different costs:

| Concern | What changes | Cost |
|---------|-------------|------|
| Parser matching | Which values the C# parser accepts for a slot | Zero — swap the lookup table |
| Grammar vocabulary | Which words VOSK will recognize at all | Expensive — recognizer destroy/create, audio gap |

v4.0 splits `RebuildParserAndGrammar` into two independent operations:

- **`RebuildParser`** — swaps C# parser with new slot values. Instant, no native side effects, safe to call at any time.
- **`RebuildGrammar`** — the expensive stop -> destroy recognizer -> create recognizer -> start cycle. Explicit, not automatic.

Dynamic slot changes call only `RebuildParser`. The grammar stays wide — it contains the full vocabulary universe established at `Configure()` time. The developer populates the grammar with all *possible* values upfront (every target name that could ever appear), and the value provider controls which subset the parser currently accepts.

If the developer truly needs new vocabulary added to the grammar (e.g. player-named units with novel words), they call `RebuildGrammar` explicitly.

**Scope:**

- Value provider delegate on `VoskSlotDefinition` (or a registration API on `VoskCommandRecogniser`) that returns current valid values.
- `RebuildParser` / `RebuildGrammar` split — existing `SetActiveSets` and `Configure` call both; dynamic slot updates call only `RebuildParser`.
- `NotifySlotChanged()` method that triggers parser-only rebuild.
- Inspector path for `VoskSlotAsset`: likely limited to code-first API for value providers, since ScriptableObjects can't hold delegates.

**Out of scope:** Automatic grammar regeneration on slot change. Dirty-tracking / coalescing (defer to v4.1 if needed).

---

### v4.1 — Pending Commands & Follow-Ups

Unified mechanism for incomplete commands, confirmation, and multi-turn dialogue.

**Problem:** "Launch missiles" should be able to wait for "target hotel one" as a follow-up rather than requiring the full phrase in one breath. Separately, high-stakes commands like "launch missiles target hotel one" should be confirmable ("confirm" / "cancel") before firing. These are the same underlying pattern.

**Key design decision — unified pending state.** Both incomplete commands and confirmation are the same flow:

1. Command recognized -> enters pending state (not fired yet)
2. System waits for follow-up speech
3. Follow-up completes the command (fills slots) or confirms/cancels it
4. Timeout -> configurable behavior (cancel, fire as-is, discard)

Confirmation is just a degenerate case of follow-up where the "missing piece" is a yes/no gate. One mechanism, not two.

**Key design decision — hybrid trigger model (Option C).** Missing required slots do NOT automatically trigger pending state (this would break the current contract where missing slots tank the score). Instead:

- Developer marks specific commands with `AllowPartialMatch` or `RequiresConfirmation` — only those enter pending state.
- Current behavior (missing required slot = rejection) is preserved for all unmarked commands.

**Key design decision — grammar stays wide during follow-ups.** Command set switching costs an audio gap (stop -> rebuild recognizer -> start). The system does NOT narrow the grammar during a follow-up window. Instead:

- Grammar stays as configured.
- Parser tries follow-up matching first (pending command's unfilled slots against new utterance).
- Falls back to new command matching if follow-up doesn't apply.
- New complete command match takes priority over a weak follow-up match (prevents the system from hijacking unrelated commands into follow-ups).

**Key design decision — grammar rebuilds are deferred during pending state.** If a dynamic slot change (v4.0) triggers an explicit `RebuildGrammar`, it is queued until the pending command resolves. Parser-only rebuilds remain instant and unaffected.

**Scope:**

- `AllowPartialMatch` flag on `VoskCommandDefinition` — commands with this flag enter pending state when matched with unfilled required slots.
- `RequiresConfirmation` flag on `VoskCommandDefinition` — fully-matched commands enter pending state awaiting confirm/cancel.
- Pending command state in `VoskCommandRecogniser` — tracks one pending command, its unfilled slots, and a timeout.
- Follow-up matching — next utterance tried against pending context first (slot-filling or confirm/cancel vocabulary), then against normal command set.
- Configurable timeout and timeout behavior (cancel, fire-as-is).
- Events: `OnCommandPending(VoskCommand)`, `OnCommandConfirmed(VoskCommand)`, `OnCommandCancelled(VoskCommand)`.
- Cancel vocabulary: built-in set ("cancel", "abort", "negative", "belay that") — configurable.
- Confirm vocabulary: built-in set ("confirm", "affirmative", "yes") — configurable.
- Deferred grammar rebuild queue that drains after pending resolution.
- Inspector support on `VoskCommandAsset` for the new flags.

**Out of scope:** Multiple simultaneous pending commands (one at a time). Nested follow-ups (pending within pending). Custom follow-up grammar narrowing.

---

### v4.2 — Anaphora (Pronoun Resolution)

"Fire torpedoes at it" -> "it" resolves to the last matched target slot value.

**Problem:** Natural speech uses pronouns constantly. Without this, the voice interface feels robotic — every command must name its target explicitly even when context is obvious.

**Depends on:** v4.1's dialogue context tracking. The pending command flow already maintains state about recent commands; anaphora extends that into a lightweight dialogue memory.

**Scope:**

- `DialogueMemory` — tracks the last matched value per slot name across recent commands (not just pending ones).
- Pronoun vocabulary: "it", "that", "them", "there", "same" — configurable.
- Resolution rule: when a pronoun appears where a slot is expected, substitute the most recent value for a slot of the same name. If no history exists, treat as no match.
- Opt-in: developer marks specific slots as pronoun-resolvable (not all slots make sense — "set heading it" is nonsensical).
- Memory decay: configurable window (time or turn count) after which old values are forgotten.

**Out of scope:** Cross-slot resolution ("fire at the destroyer" then "go there" where "there" maps to the destroyer's location — that's game logic, not SDK logic). Full NLU. Multi-referent resolution ("fire at them" meaning multiple targets).

---

## Architecture Summary

```
v3 (current):

  VOSK result
    -> VoskCommandRecogniser.HandleResult()
    -> utterance buffer (text accumulation, timer)
    -> VoskCommandParser.Parse() — stateless
    -> threshold filter -> debounce -> fire events

v4 (new):

  VOSK result
    -> VoskCommandRecogniser.HandleResult()
    -> utterance buffer (unchanged)
    -> VoskCommandParser.Parse() — still stateless, but slot lookup
    |                                tables updated by value providers
    -> pending command check:
    |    if pending exists -> try follow-up match (slot fill / confirm / cancel)
    |    else              -> normal threshold filter
    -> anaphora resolution (substitute pronouns from DialogueMemory)
    -> pending state machine:
    |    complete match + no confirmation needed -> fire
    |    complete match + confirmation needed    -> enter pending
    |    partial match + AllowPartialMatch       -> enter pending
    |    follow-up completes pending             -> fire (via OnCommandConfirmed)
    |    timeout / cancel                        -> discard (via OnCommandCancelled)
    -> debounce -> fire events
    -> update DialogueMemory with fired command's slot values
```

The parser itself (`VoskCommandParser`) remains stateless and pure. All dialogue state lives in `VoskCommandRecogniser` — pending command, dialogue memory, deferred grammar queue. This preserves the clean separation where the parser is a function and the recogniser is the state machine.

---

## Backward Compatibility

- All new behavior is opt-in. Commands without `AllowPartialMatch` or `RequiresConfirmation` behave exactly as v3.
- Dynamic slots are additive — static slot definitions work unchanged.
- Anaphora requires explicit opt-in per slot.
- `RebuildParserAndGrammar` continues to work as before; the split into `RebuildParser` / `RebuildGrammar` is an internal refactor with the existing method calling both.
- No public API removals or signature changes.
