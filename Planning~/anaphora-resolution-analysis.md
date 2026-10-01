# Pronoun / Anaphora Resolution for VoXR — Analysis

Working notes from a discussion about adding pronoun and anaphora resolution to
the voice command pipeline. Captured here so the conversation can continue in a
later session.

## Context: what VoXR is today

Package: `com.jinwoo1601.voxr` (v1.2.0), pulled from
`github.com/jinwoo1601/VoXR-Speech-Recognition`. Cached at
`Library/PackageCache/com.jinwoo1601.voxr@…/`.

Pipeline:

- `VoxrSpeechRecogniser` — VOSK-backed offline STT, emits partial / final
  results.
- `VoxrCommandRecogniser` — facade MonoBehaviour. Runs an utterance buffer,
  per-intent debounce, pending/confirm flow, dynamic slots, and grammar.
- `VoxrCommandParser` — pure C#, scores tokenised text against pattern
  templates with `{slot}` / `{?optionalSlot}` placeholders. Pre-allocated
  buffers throughout — perf-sensitive hot path.
- `DynamicSlotManager` — per-slot `Func<string[]>` providers; the active
  value list for a slot is rebuilt at runtime (e.g. ships currently on
  sensors). Triggers parser + grammar rebuild.
- `PendingCommandHandler` — partial-match / awaiting-confirmation state
  machine, with `confirmVocabulary` / `cancelVocabulary` and a timeout policy.
- `GrammarManager` — builds the constrained VOSK grammar from active
  slots/commands plus follow-up vocabulary. **In constrained mode, VOSK only
  transcribes words present in this grammar.**
- Free-speech mode bypasses grammar constraints (debug only).

Implication for anaphora: any pronoun we want recognised must be in the
grammar JSON, otherwise VOSK turns it into `[unk]` before we ever see it.

## What "anaphora" actually covers in this game

For a VR space-captain command interface, the useful cases split four ways
with very different costs:

1. **Ellipsis / zero-anaphora** — bare commands like "Fire." with no object;
   resolves to the last-targeted thing.
2. **Pronouns** — "it / them / him / her / they". Needs a typed salience
   stack (so "him" can't bind to a ship, etc.).
3. **Demonstratives** — "that ship / this one". In VR these are most
   naturally resolved by **gaze ray**, not by prior text. This is exophora,
   not anaphora.
4. **Definite descriptions** — "the cruiser". Already mostly handled by
   existing `{ship}` slot when the dynamic provider yields one cruiser;
   degrades to ambiguity otherwise.

## Functional requirements

- Resolve a pronoun/demonstrative/null-object to a concrete entity ID that
  fills a slot, **typed** by the slot's semantic category (vessel, crew,
  system, sector). Type-mismatched candidates are filtered, not just
  scored down.
- Per-type salience model: ring buffer of recent referents per category,
  with time decay. Genre tempo suggests ~20–30s; tunable.
- Salience inputs come from gameplay, not just from prior utterances:
  last hailed, last targeted, last damaged, currently gaze-locked. Game
  pushes these in; VoXR does not own them.
- Gaze / pointing as the deictic resolver for "that / this". Cleanly
  separable from textual anaphora — different input source, same resolver.
- Ambiguity policy: when multiple candidates score within ε, **enter
  pending and ask back** ("which ship, captain?"). Reuses
  `PendingCommandHandler`; do not reinvent.
- Failure policy: referent gone (destroyed / jumped / out of sensor range)
  → cancel the command and surface feedback. Do not silently retarget.

## Integration requirements (the design-defining ones)

### Where in the pipeline does resolution run?

Three options, ordered by invasiveness:

- **Pre-rewrite** (token-stream substitution, "it" → "rocinante" before
  parse). Forces every referent value into the constrained grammar
  permanently — bloats it and fights `DynamicSlotManager`.
- **Slot-time hook** — new slot kind, e.g. `{ship*}`, that accepts the
  literal slot value *or* a pronoun. When the matcher sees a pronoun in
  that position, it calls a resolver delegate to substitute the value.
  Cleanest; couples parser to a context provider via a single delegate.
  **Recommended.**
- **Post-parse** — slot value is the literal pronoun ("it"); a downstream
  resolver fills it before `OnCommandRecognised` fires. Easiest to bolt
  on, but skips parser-level type/value validation and breaks the
  guarantee that a delivered slot value is always one of the slot's
  registered values.

### Grammar

- Grammar build (`GrammarManager.Rebuild`) must include the closed-class
  pronoun/demonstrative set: `it`, `them`, `him`, `her`, `they`, `that`,
  `this`, `those`, `these`, `one`. ~10 words, cheap.
- Pronouns also need to coexist with the existing follow-up grammar words
  (confirm/cancel vocabularies).

### Authoring surface

- Slot assets gain a `Category` field: `Vessel | Crew | System | Sector`
  (extensible). Drives type-matching in the resolver.
- Pattern syntax adds the `*` suffix on a slot reference to opt that
  position into anaphora: `"target {ship}"` is strict;
  `"target {ship*}"` accepts pronouns and bare ellipsis.
- `{?ship*}` combines with the existing `{?slot}` optional marker for
  "fire" → "fire on it" → "fire on the freighter" all hitting the same
  intent.

### Threading & perf

- Main-thread only, matching parser invariants.
- Salience store is a small per-category ring buffer; resolver is
  O(small N).
- No per-utterance allocation. Reuse the parser's allocation-free style:
  pre-allocated context buffers, no LINQ in the hot path.

### Diagnostics

- Extend `VoxrMatchDiagnostics` with a resolution trace per matched slot:
  `resolved 'it' → vessel 'rocinante' (Δt=3.2s, salience=0.78)`. Surfaces
  in the editor HUD so designers can see why a pronoun bound where it did.

### Free-speech mode

- Unaffected. Once the matcher receives tokens, the resolver runs the
  same way regardless of grammar mode.

## The main tradeoff to flag

Rule-based salience + gaze gets ~80% of the immersion benefit cheaply,
on-device, deterministic, testable. Going further — full coreference for
sentences like "tell engineering to vent the deck where the fire is" —
wants LLM-style NLU and breaks VoXR's offline / constrained-grammar model.
Hold the line: ship rule-based, keep the door open for an LLM tier later,
do not try to build that inside VoXR.

## Suggested staging

### Phase 1 — ellipsis only

Track a "last target" per slot category. Auto-fill missing required slots
on bare commands. ~1–2 days. Big perceived win for the captain fantasy
("fire", "scan", "hail" all just work after a target is established).

No grammar changes (no new words). No pattern syntax changes (uses the
existing `{?slot}` optional-slot mechanism plus a fill callback).

### Phase 2 — typed pronouns with `{ship*}`

In scope:

- `Category` field on slot definitions.
- `{ship*}` pattern syntax: pronoun-accepting variant of `{ship}`.
- Pronoun set in the grammar build.
- `AnaphoraResolver` service: typed salience stack, time decay, query API
  takes (category, recency-window) and returns candidate list.
- Slot-time hook in `VoxrCommandParser` that calls the resolver when it
  sees a pronoun in a `{ship*}` position.
- Ambiguity → pending command, reusing `PendingCommandHandler`.
- Game-side push API: `AnaphoraContext.NoteTargeted(entityId, category)`,
  `NoteHailed(...)`, `NoteDamaged(...)`, etc.
- Diagnostics extension.
- Edit-mode tests: feed transcript + synthetic context, assert resolved
  slots; assert ambiguity → pending; assert type-mismatch rejection.

Out of scope (deferred to Phase 3):

- Gaze/pointing as a deictic input.
- Visual highlight of the resolved entity on the bridge HUD.
- Voice TTS feedback ("targeting it — the *Rocinante*").

### Phase 3 — VR deixis (not specified here)

Gaze provider feeding `AnaphoraContext`, "that / this" resolution, visual
confirmation on the resolved entity. Lives as much in the game project as
in VoXR.

## Open questions to resolve before implementing Phase 2

- Salience decay curve: linear vs. exponential, half-life value.
- Tie-breaking when multiple candidates have identical recency: prefer
  player-targeted? prefer in-FOV? prefer hostile?
- Plural pronouns ("them") — bind to a *set* of recent referents, or
  reject as ambiguous?
- Cross-category fallback: should "it" ever bind to a non-vessel if the
  vessel stack is empty but a system was just discussed? Default: no.
- Does the resolver run before or after the existing follow-up slot-fill
  in `PendingCommandHandler.TryFollowUpSlotFill`? Likely after — let an
  active pending command consume the utterance first.
- Should the package surface a public `AnaphoraResolver` interface so the
  game can override the default rule-based implementation (e.g. with an
  LLM-backed one later)?

## Relevant files in the current package

- `Library/PackageCache/com.jinwoo1601.voxr@…/Runtime/Commands/VoxrCommandRecogniser.cs`
  — `ProcessParsedResultsCore` is where Phase 2's slot-time hook would
  integrate (between Step 3 parse and Step 7 accept).
- `…/Runtime/Commands/VoxrCommandParser.cs` — pattern matcher, would gain
  recognition of the `*` suffix and the slot-time delegate call.
- `…/Runtime/Commands/DynamicSlotManager.cs` — pattern to mirror for the
  resolver's lifecycle (register / unregister / rebuild).
- `…/Runtime/Commands/PendingCommandHandler.cs` — reuse for ambiguity
  prompts.
- `…/Runtime/Commands/GrammarManager.cs` — add pronoun word list to the
  constrained grammar build.
- `Assets/VR FTL-Like/Scripts/VoxrCommandTestHud.cs` — debug HUD; extend
  to display resolution traces alongside existing match diagnostics.
