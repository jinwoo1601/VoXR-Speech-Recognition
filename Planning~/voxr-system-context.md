# CONTEXT SOURCE: the system I am trying to understand and improve

This source is not a research paper. It is documentation of my own software: VoXR, an
offline speech-recognition package for Unity, running on Meta Quest (Android arm64) and in
the Unity Editor on Windows.

Architecture, in the order audio flows:

1. Microphone capture (AudioRecord via JNI on Android; Unity Microphone in the Editor).
2. A DSP front end: automatic gain control, then downsampling to 16 kHz mono PCM.
3. VOSK, which is built on the Kaldi toolkit, running a small English acoustic model
   (vosk-model-small-en-us-0.15). VOSK is run in GRAMMAR-CONSTRAINED mode: at runtime I
   generate a JSON grammar containing only the phrases reachable from the currently active
   command set, and the decoder is restricted to that grammar. VOSK returns a transcript
   string plus per-word confidence values.
4. An utterance buffer that merges consecutive VOSK results within a time window, so that a
   command split across a mid-utterance pause is reassembled before parsing.
5. My own command parser: it matches the transcript against author-defined token patterns
   (literal tokens plus typed slots), extracts slot values, and computes a normalised match
   score between 0 and 1. It can extract several commands left-to-right from one utterance.
6. Filters: a minimum score threshold, a minimum confidence threshold, a bar that refuses a
   match whose winner missed its first required element, and a debounce that suppresses
   duplicate intents within a cooldown.
7. A pending state for commands that are incomplete (missing a required slot), ambiguous
   (two intents tie), or explicitly require confirmation. Follow-up speech resolves them.

The two documents below are the package's own documentation. The first records the failures
I have observed and their suspected causes; the second describes the recognition pipeline in
detail. When I ask how a research paper relates to my system, these are what I mean by "my
system".


---

# DOCUMENT 1: KNOWN LIMITATIONS (observed failures)

# Known Limitations

This document collects known limitations of the VoXR that aren't
bugs to fix but rather constraints rooted in the underlying VOSK acoustic model,
voice recognition in general, or deliberate architectural choices. The goal is
to give consumers (and our future selves) a single place to look when something
"weird" happens, before assuming it's a regression.

Each entry includes a short repro, the root cause, and a workaround (if any).

---

## VOSK Acoustic Model

Limitations rooted in the small English VOSK model
(`vosk-model-small-en-us-0.15`) we ship with. Switching to a larger model would
mitigate some of these but at the cost of memory and download size.

### "to" misrecognised as "two"

- **Status**: still open. One TTS fixture stopped reproducing it after
  phrase-chunked grammar emission
  ([#45](https://github.com/jinwoo1601/VoXR-Speech-Recognition/issues/45)):
  `switch to navigation` is now a single grammar entry, that fixture decodes
  correctly and fires `mode_navigation`, and it has been re-baselined from a
  negative pin to a positive one. That is the whole of the evidence — one
  synthesised-speech fixture. The limitation was originally observed in **human
  speech on device**, and has **not** been re-tested there since; the in-headset
  A/B against this and the other documented confusion pairs has not been run.
  Treat #45 as a plausible mitigation, not a fix.
- **Repro**: Say "switch to weapons". VOSK transcribes `switch two weapons`.
  Observed in human speech on Quest (v2.5 test matrix Phase 4.5); not re-tested
  since #45. The TTS fixture for the same phrase has always been recognised
  correctly, and before #45 the "switch to navigation" fixture was transcribed
  `switch two navigation` and matched no command — so the substitution is phrase-
  and delivery-dependent, not uniform.
- **Where seen**: v2.5 test matrix Phase 4.5; WAV-replay acoustic suite (v1.5 dev).
- **Root cause**: The small English model is acoustically biased toward "two"
  in this context, especially when the speaker emphasises the vowel slightly
  or says it quickly. The grammar token splitter is correct — it produces
  `["switch", "to", "weapons"]` — but VOSK never feeds it the right phonemes
  to match.
- **Workaround**:
  - Prefer alternate patterns that avoid short function words. The sample's
    `mode_weapons` command uses both `["switch", "to", "weapons"]` and
    `["weapons", "mode"]`; the latter recognises reliably.
  - When designing your own commands, avoid `to`, `for`, `four`, `or`, `are`
    and similar short homophones inside required tokens — and where you must
    use one, prefer keeping it inside a run of required literals rather than
    adjacent to a slot boundary, so that phrase chunking has a chance to help.
    A slot or optional literal ends a run, so a function word stranded beside
    one gets no phrase entry spanning it at all.

### "all" misrecognised as "fall" when navigation words dominate grammar

- **Repro**: While the `navigation` set is active (contains the
  `fall back from target {target}` pattern), say "all modes". VOSK frequently
  transcribes `fall modes`, and the command fails to match.
- **Where seen**: v2.4 test matrix Phase 4.5 — took 3+ attempts to trigger
  `mode_all` from navigation mode. Documented in v2.4 Known Bugs.
- **Root cause**: When a phonetically similar word ("fall") is prominent in
  the active grammar, VOSK's constrained decoder prefers it over less frequent
  words ("all"). This is a general property of grammar-mode decoding: words
  that appear in more patterns are effectively weighted higher.
- **Workaround**:
  - The sample's `mode_all` command uses both `all modes` and `enable all`.
    `enable all` is the reliable trigger when navigation words are active.
  - For your own commands, offer a phonetically distinct alias for any short
    keyword that clashes with another grammar word.

### "cease fire" misrecognised as "safe five" when weapons set inactive

- **Repro**: Switch to navigation-only mode, then say "cease fire". VOSK
  transcribes `safe five` (or similar) because "cease" and "fire" are no
  longer in the active grammar.
- **Where seen**: v2.4 test matrix Phases 3.5, 4.4 (notes).
- **Root cause**: Same constrained-decoder behaviour as the "all"/"fall"
  issue. With "cease" and "fire" removed from the grammar, VOSK maps the
  phonemes to the nearest in-grammar words. This is actually *working as
  intended* for set restriction — the command is correctly rejected — but it
  means you cannot rely on the raw transcript to explain why recognition
  failed when the user is in the "wrong" mode.
- **Workaround**: None needed for correctness. If you surface raw transcripts
  for debugging, expect surprising substitutions when the user speaks out of
  the active grammar.

### Abbreviations and letter sequences map to [unk]

- **Repro**: Say "close distance cqb target alpha three". VOSK produces
  `close distance [unk] target alpha three`; the `range=cqb` slot fails.
- **Where seen**: v2.0 test matrix Phase 3.2.
- **Root cause**: The small English model has no entries for military/radio
  abbreviations like "cqb", "pdc", etc. Grammar mode forces VOSK to choose
  something in-vocabulary, so abbreviations become `[unk]`.
- **Workaround**:
  - Spell out the phrase in the slot value (e.g. `close quarters` instead of
    `cqb`).
  - Alternatively, add phonetic aliases (`see queue bee` → `cqb`) so VOSK can
    match the phoneme sequence.

### Single-character literals ("a") unreliable

- **Repro**: Say "launch a missiles target hotel one" against a pattern of your
  own carrying `?a` as an optional literal (the shipped sample avoids this shape —
  it routes "a" through the alias below instead). VOSK transcribes "a" as "on" or
  drops it entirely.
- **Where seen**: v2.1 test matrix Phase 3.1 (and Phases 3.3–3.4, 6A.2 where
  VOSK silently dropped "a").
- **Root cause**: Very short function words carry almost no acoustic
  information. In grammar mode, any phonetically similar word that exists in
  the active grammar ("on" from `close on target`) outcompetes "a".
- **Workaround**:
  - Don't use single-character literals or single-character alias keys inside
    patterns. The parser now emits a validation warning for single-character
    slot values and alias keys.
  - If you need quantity "a" (as in "fire a torpedo"), declare it as an alias
    to a longer canonical value (`a` → `one`). The alias resolves correctly
    when VOSK *does* hear it (see v2.1 Phase 4.2), and is harmless when
    dropped.

### "two" consistently scores low confidence

- **Repro**: Say any number-sequence command containing "two" (e.g.
  "orient heading two seven zero"). The per-word confidence for "two" is
  almost always 0.50 regardless of grammar size or pronunciation clarity.
- **Where seen**: v2.4 test matrix Phase 9 notes, v2.2 Phase 10.1.
- **Root cause**: Quirk of the small English model — "two" shares phonemes
  with "to"/"too" and the acoustic posterior for this phoneme cluster is flat.
  The grammar constraint only narrows the candidate list; it doesn't sharpen
  the posterior.
- **Workaround**: None. If you set `minConfidence` too strictly, NumberSequence
  commands containing "two" will be rejected. The default `minConfidence=0.4`
  accommodates this; don't push it above 0.5 unless you've verified your
  vocabulary avoids "two".

### Free speech mode is unreliable for numeric and literal commands

- **Repro**: Set `freeSpeechMode=true`, rebuild, say "orient heading two seven
  zero". VOSK transcribes `korean heading to seven zero` ("orient" → "korean",
  "two" → "to" homophone). Pattern fails to match.
- **Where seen**: v2.2 test matrix Phase 10.1 (FAIL), Phase 10.4 notes; v2.0
  Phase 6.2 (PARTIAL — "launch" heard as "lunch or").
- **Root cause**: Without grammar constraint, VOSK's small model has a much
  larger candidate vocabulary and commits to phonetically plausible but
  wrong transcriptions. Homophones ("to"/"two", "four"/"for") and uncommon
  words ("orient") are the first to break.
- **Workaround**:
  - Keep `freeSpeechMode=false` (the default) for command-driven UX.
  - Only enable free speech when you *need* arbitrary dictation (e.g. a
    note-taking feature) and accept that command matching will be best-effort.

---

## Voice Recognition (General)

Limitations inherent to streaming speech recognition, independent of which
model you use.

### Cough, hum, and noise can trigger false matches in grammar mode

- **Repro**: Cough, hum, or tap the microphone while recognition is active.
  VOSK occasionally transcribes the noise as a short in-grammar word ("on",
  "from", "four"), which may match a pattern prefix.
- **Where seen**: v2.0 test matrix Phase 5.2 (PARTIAL — deferred to voice
  activation); v2.1 Phase 5A.2 (cough heard as "on", "from").
- **Root cause**: Grammar-constrained VOSK *must* choose an in-vocabulary
  word; it has no "silence" output. Short noises that have any voicing will
  be mapped to whichever grammar word their phoneme vector is closest to.
  Since short words ("on", "to", "four") sit closest to low-energy noise in
  phoneme space, they are the most frequent false triggers.
- **Workaround**:
  - Gate recognition with a push-to-talk button: call
    `VoxrSpeechRecogniser.StopRecognition()` / `StartRecognition()` around
    the button press so the parser only looks at intentional speech. This
    is the recommended approach for noisy environments.
  - Tune `minConfidence` upward — noise-derived matches usually have
    confidence well below 0.5. Don't push it past ~0.5 or you will reject
    NumberSequence commands (see "'two' consistently scores low" above).
  - Prefer longer, multi-token commands; false triggers rarely produce
    more than one in-grammar word in a row.

### VOSK's VAD splits mid-command pauses into separate utterances

- **Repro**: Say "orient heading" *pause ~0.8s* "two seven zero". VOSK
  emits two independent final results; neither matches a pattern alone
  ("orient heading" is missing the digit slot, "two seven zero" is missing
  the command prefix).
- **Where seen**: v2.2 test matrix Phase 9.2 (KNOWN LIMITATION).
- **Root cause**: VOSK's voice activity detector treats pauses as utterance
  boundaries and flushes an interim final. The parser sees two disconnected
  transcripts, not one.
- **Workaround**:
  - `bufferWindow` (default 0.5s) is exactly for this case — it merges
    consecutive VOSK results before parsing. See the *Architecture* section
    below for tuning notes.
  - If the pause is longer than `bufferWindow`, the command is lost.
    Re-prompt the user or widen the window (up to ~2.0s on Quest 3 — see
    below).
  - A cross-utterance buffer that merges arbitrarily distant utterances
    was evaluated and deferred: the false-positive risk from concatenating
    genuinely unrelated speech outweighs the benefit for this edge case.

### Set restriction cannot produce meaningful rejection transcripts

- **Repro**: In weapons-only mode, say "approach target alpha one". The
  recogniser correctly rejects the command, but the logged transcript is
  `[unk] target alpha one`, not `approach target alpha one`.
- **Where seen**: v2.4 test matrix Phases 2.4–2.6, 3.5–3.7, 4.2, 4.4.
- **Root cause**: Grammar is rebuilt to contain only the active sets' words,
  so "approach" is no longer in the vocabulary and maps to `[unk]` at the
  acoustic level. This is *correct behaviour* for set restriction — the
  smaller grammar is the whole point — but you cannot tell the user
  "you said X, but X isn't available in the current mode" because the SDK
  never saw the word "X".
- **Workaround**: If you need "wrong-mode" UX (e.g. a hint that says "you
  asked for a navigation command while in weapons mode"), use the single-set
  + superset grammar approach and do mode gating in your `OnCommand` handler
  rather than via `SetActiveSets()`. See the *Active set switching* entry
  below for the same trade-off.

### Smaller grammars don't always yield measurably higher recognition scores

- **Repro**: Run the same phrase with all three sets active, then with only
  the one set it belongs to. Compare scores and confidences.
- **Where seen**: v2.4 test matrix Phase 9 (all four sub-tests passed but
  showed *no measurable difference*).
- **Root cause**: VOSK's decoder is already very confident on commands built
  from distinctive multi-word phrases. Restricting the grammar further does
  reduce the search space, but if the full grammar was already producing
  `score=1.00` / `conf=1.00`, there is no headroom for the restriction to
  improve. The benefit of set restriction is primarily *rejecting out-of-set
  commands* (which Phases 2, 3, 5 confirmed), not boosting in-set accuracy.
- **Workaround**: None needed. Don't expect confidence gains from set
  switching alone — its value is correctness (blocking wrong-mode commands),
  not accuracy.

---

## Architecture and Design

Limitations that come from how the SDK is structured. Most of these are
deliberate trade-offs rather than oversights.

### Only one `VoxrSpeechRecogniser` can be initialised per process

- **Repro**: Put two `VoxrSpeechRecogniser` components in a scene (two GameObjects,
  or two additively-loaded scenes each carrying one) and call `InitialiseAsync()`
  on both. The second logs an error, reports `IsInitialised == false`, and never
  loads its own model.
- **Root cause**: The native bridge is file-scope C++ state — `g_model`,
  `g_recognizer`, `g_initialised` in `NativeBridge~/src/vosk_bridge.cpp` — and its
  C ABI carries no handle, so on device there is exactly one bridge per process.
  Nothing on the managed side can make two components genuinely independent there
  without changing that ABI.
- **Why it also applies in the Windows Editor, where it need not**: the Editor
  backend (`EditorMicBackend`) is per-instance — it loads its own VOSK model and
  never calls `vosk_bridge_*` — so two recognisers could coexist there, and did
  before #57. The rule is enforced uniformly anyway. The alternative is worse: a
  two-recogniser scene that runs in the Editor and silently corrupts on device is
  harder to diagnose than one that fails identically in both, and the Editor is
  where the developer can still see the error. Enforcing on both branches is also
  what makes the constraint testable at all — the automated coverage this has runs
  in EditMode/PlayMode, not on device.
- **What this used to do**: Before the enforcement landed (#57) the sharing was
  silent. The second component's `InitialiseAsync()` early-returned on the *first*
  one's `IsInitialised` and quietly discarded its own model path, sample rate, and
  AGC target; then either component's `OnDestroy` called the unconditional
  `vosk_bridge_destroy()` and freed the survivor's recognizer and model, which on
  ordinary additive scene unload left the survivor calling into freed memory.
- **Workaround**: Keep one recogniser for the lifetime of the process — a
  persistent GameObject (`DontDestroyOnLoad`) that per-scene code holds a reference
  to, rather than one recogniser per scene. Where a handover is genuinely needed,
  call `ReleaseNativeResources()` on the outgoing recogniser first: that frees the
  claim **synchronously**, so the incoming one initialises in the same frame.
  Destroying it also frees the claim, but only via `OnDestroy` — which Unity defers
  to the end of the frame, so `Destroy(outgoing); incoming.Initialise();` in one
  frame is rejected with an error, as is an `UnloadSceneAsync` that overlaps the
  incoming scene's `Start()`. Under the default push-to-talk wiring the next press
  retries and succeeds (losing only the pre-warm); in `Continuous` listening mode
  nothing retries, so the explicit `ReleaseNativeResources()` handover matters there.
- **Note**: Refcounting the bridge, or giving the ABI a per-instance handle so two
  recognisers could genuinely coexist on device, remain open as native-side work —
  unfiled; this entry is their only record. What ships today makes the constraint
  explicit and loud instead of silently corrupting state.

### Active set switching has a brief audio gap

- **Repro**: Trigger a `SetActiveSets()` call (e.g. via a `mode_*` command),
  then immediately try to speak the next command. The first one or two words
  of the second utterance are dropped.
- **Where seen**: v2.5 test matrix Phase 5.4. After saying "navigation mode"
  the user said "fall back from target hotel two" too quickly, and VOSK only
  heard `target hotel two` — the leading three words were lost.
- **Root cause**: `SetActiveSets()` calls `RebuildParserAndGrammar()` which
  stops AudioCapture, applies the new grammar to the VOSK recogniser, and
  restarts AudioCapture. The full sequence takes ~50ms minimum on Quest 3,
  during which the microphone isn't being read. Any speech in that window is
  dropped at the audio layer, before VOSK ever sees it.
- **Workaround**:
  - After triggering a mode switch, pause for ~500ms before speaking the next
    command. In a game UI, gate user input by wiring a callback off
    `OnCommandRecognised` for the `mode_*` intents (a visual "Mode: Weapons"
    indicator doubles as the ready signal).
  - If you need seamless switching, prefer the **single-set + grammar
    superset** approach: configure all your commands in one set and gate them
    in your `OnCommand` handler instead of swapping active sets at runtime.

### Validation warnings re-emit on every active-set switch

- **Repro**: Wire any slot with a single-character alias (e.g. `a` → `one`)
  and call `SetActiveSets()` repeatedly. The
  `[VoxrCommandParser] Slot 'quantity' has single-character alias "a"...`
  warning fires on every switch, not just at initial Configure.
- **Where seen**: v2.5 test matrix Phases 5–8. Visible in logcat after every
  mode-switch command.
- **Root cause**: `SetActiveSets()` constructs a fresh `VoxrCommandParser` via
  `RebuildParserAndGrammar()`, and the parser ctor unconditionally re-runs its
  validation passes — `RunValidationWarnings()` for slot values and aliases, and
  `WarnOnExcessiveOptionalExpansion()` for a pattern past the eager-flush
  expansion cap. The warnings are correct, just noisier than they should be.
  Three further scans are Editor-only: the droppable-required-literal check
  (demoted in [#81](https://github.com/jinwoo1601/VoXR-Speech-Recognition/issues/81) —
  in a player build it no longer contributes), the sibling-discriminator
  warning, which is the loudest of the set, and the duplicate-intent check
  ([#120](https://github.com/jinwoo1601/VoXR-Speech-Recognition/issues/120)).
  That last one compounds this limitation rather than merely joining it: the
  duplication it reports can be *created* by the switch — two sets that each
  carry a command under the same intent are only in conflict while both are
  active — so it re-reports on every switch that keeps them active together.
  So in the Editor all **five** passes re-run on every switch; a player build
  re-runs only the unconditional two.
- **Workaround**: None at user level. This is a candidate for cleanup —
  validation should run once per `Configure()` call, not per parser rebuild.
  Filed as a low-priority follow-up.

### Coverage demotes a command when it explains too little of what was said

- **Repro**: Register only `decelerate` (no slot-filled sibling) and say
  "decelerate hard burn". The command scores `1 / (1 + 2)` = `0.333` against the
  default `minScore` of `0.6` and does not fire; before the coverage change (#65)
  it scored `1.0` and fired. Same shape for any short pattern followed by words the grammar cannot
  place: "cease fire please" drops `1.0` → `0.667`, "approach target alpha one now"
  → `0.750`.
- **Where seen**: measured across a 699-utterance A/B during #65 §5.2. 17 of 699
  stopped clearing the gate; 8 of those were partially-heard utterances that had
  been sitting on exactly `0.600`.
- **Root cause**: coverage charges a candidate for the in-grammar tokens it leaves
  unexplained on either side, so a command is no longer judged on how neatly it
  matched the part it chose but on how much of the utterance it accounts for. That
  is deliberate — it is what stops a bare pattern silently discarding a spoken
  argument (#42) — but it applies whether or not a better sibling exists to win
  instead.
- **Workaround**:
  - Register the fuller phrasing as an additional pattern, so the demotion has
    somewhere to land. This is the intended authoring response.
  - Mark natural trailing words optional (`?please`) to bring them into the
    grammar.
  - Lower `minScore`, or set `coverageWeight` below `1.0`. Setting it to `0`
    switches coverage off on *both* sides, so it reverts further than undoing #65 —
    back to pre-1.4.0 scoring, before the leading skipped-word charge existed —
    and brings the discarded-argument bug back with it. Note the value is read
    when the parser is built, so a change takes effect at the next
    `RebuildParser` / `Configure` / `SetActiveSets` / `NotifySlotChanged`.

### A prefab-instance override of the old `skippedWordPenalty` may not survive the rename (unverified)

- **Symptom**: After upgrading past the `skippedWordPenalty` → `coverageWeight`
  rename (#65), a prefab **instance** that overrode the old field runs at the
  default `coverageWeight` of `1.0` instead of its override. Commands stop firing
  exactly as in the coverage-demotion entry above, which makes the two easy to
  confuse.
- **Root cause**: the renamed field carries `[FormerlySerializedAs]`, which governs
  the field's own deserialization — component values on scene objects, assets, and
  prefab *sources* migrate. A prefab-instance override, though, is stored as a
  literal property-path string in the instance's modification list, and whether
  Unity remaps those paths through the attribute is not established here; no
  automated instrument in this package can settle it.
- **Status**: **unverified** — recorded as an upgrade hazard, not a confirmed
  defect.
- **Workaround**: after upgrading, re-check any prefab instance that overrode
  `skippedWordPenalty` and re-apply the value to the renamed **Coverage Weight**
  field if the override was lost. `Documentation~/troubleshooting.md` carries the
  same check in its post-upgrade entry.

### Batch, injected, and free-speech scores read lower than the live grammar-mode score

- **Repro**: With `freeSpeechMode` enabled, or via `InjectText`, or through
  `VoxrBatchTestRunner.Run`, feed "cease fire please". The score is `0.667`, not
  the `1.0` the same utterance gets through the grammar-constrained decoder.
- **Where seen**: #65 §5.2 review; `requirements.md` §4.1 records the derivation.
- **Root cause**: coverage exempts the literal token `[unk]`, which is what a
  grammar-constrained VOSK returns for a word outside its vocabulary. The three
  paths above deliver real text instead, so a word the decoder would have hidden
  arrives verbatim and is charged as unexplained. The leading half of the rule has
  always behaved this way; #65 newly exposes the trailing half, where filler is
  commoner.
- **Workaround**: treat batch and free-speech scores as a lower bound on the
  grammar-constrained score, not as equal to it.
  `Documentation~/editor-testing.md` states the same caveat in its
  batch-runner Programmatic API section.

### The discarded-argument protection weakens in grammars with slot-initial patterns

- **Repro**: Register any pattern whose first matchable element is a permissive
  slot — an open-ended `NumberSequence`, say. Trailing coverage then goes quiet at
  every token *that slot could match*: for a `NumberSequence`, every position where
  enough digit words follow becomes a run terminator, so digit-heavy trailing
  speech escapes the charge. The effect is scoped to the slot's own vocabulary —
  a value-list slot qualifies only its listed values, and unrelated words are
  charged exactly as before.
- **Where seen**: identified at design time (#65 architecture D4), confirmed by
  the `CanStartPattern` tests.
- **Root cause**: the orphan test is deliberately conservative — where it is
  uncertain whether a pattern could start at a token, it charges nothing, because
  over-charging destroys multi-command utterances while under-charging only leaves
  a score where it was. A permissive slot-initial pattern makes the predicate say
  "yes" wherever that slot could begin a match.
- **Workaround**: none needed for correctness — at those positions the grammar
  reverts to pre-#65 scoring. If the #42 protection matters, avoid slot-initial
  patterns over open-ended slots, or anchor them behind a literal.

### Nothing fires although a better-scoring match exists later in the utterance

- **Repro**: With the demo grammar, say "switch navigation mode" (the "to"
  dropped and a stray "switch" leading). `switch to navigation` matches at token 0
  by skipping the "to", scores `0.500`, and fires nothing; `navigation mode` at
  token 1 would have scored `0.667`.
- **Where seen**: #65 §5.2 review. Swept exhaustively over 699 utterances: 29
  candidates were blocked this way and **28 were recovered** by a later extraction
  round. This is the only one that was not. Re-swept after #82 changed the orphan
  run's terminator: **28 blocked, 27 recovered**, and the same single case still
  silent. Re-swept again after the leading-required-miss bar (#124): **unchanged
  at 28 blocked, 27 recovered**, still this one case. The bar suppresses a round's
  result but leaves the search restarting in the same place, so it cannot change
  where later rounds start — only whether they produce a command. (A barred round
  does not consume a result-buffer slot, so in a grammar small enough for that
  buffer to fill it can let a later round run that would otherwise never have been
  reached; the corpus grammar registers eleven commands, well clear of that bound.)
  It would reduce the recovered count only where a recovering round's own winner
  missed its first required element, which no row of this corpus does.
- **Root cause**: selection ranks earliest start above score, so a later-starting
  candidate cannot be promoted however much better it scores — coverage can only
  reorder candidates that begin at the same token. Normally sequential extraction
  picks the better one up on the next round; it fails only when the winner's
  consumed span covers the start the better candidate needed, as it does here.
- **Workaround**: none at user level. Rare by measurement (1 in 699), and it
  requires the winning pattern to span the alternative's start.

### A command whose first word the decoder dropped is silent rather than recovered

- **Symptom**: A command you said in full produces nothing at all. The transcript
  shows every word except the first one of the pattern, and before this change the
  command fired at a reduced score. The round leaves no record — no scored attempt
  for that pattern and no `rejectReason` naming it. Where nothing else in the
  utterance fired either, the log carries only the synthetic `no match` entry, so
  there is nothing to distinguish it from an utterance that matched nothing.
- **Repro**: With the demo grammar, say "heading two seven zero" (the leading "set"
  dropped by the decoder). `set_heading` matches the rest, scores `2 / 3` = `0.667`,
  clears the default `minScore` — and does not fire. Before #124 it fired.
- **Where seen**: issue #124, and the package's own 699-utterance fixture corpus.
  Measured over that corpus, gated at `0.60`: 48 rows change and 17 stop firing
  anything at all — of those 17, **9 lose a genuinely spoken command this way** and
  the other 8 were invented commands the bar exists to suppress (39 invented
  commands are suppressed across all 48 rows). No row loses a command that scored a
  clean `1.00`, and no surviving command's score changes.
- **Root cause**: a round's **winner** whose first required element matched nothing
  is refused, whatever it scored — see
  [the bar](Documentation~/scoring.md#the-leading-required-miss-bar). The rule is
  positional because the first required element is the verb: losing an argument
  leaves the action identified, losing the verb leaves no evidence that any action
  was requested. **Nothing in the transcript distinguishes "the speaker never said
  it" from "the speaker said it and the decoder dropped it"** — both arrive as the
  same token sequence — so the refusal cannot be made selective, and this silence is
  the deliberate price of not firing commands nobody uttered.
- **Workaround**: none at the moment of failure — the speaker says the command
  again. `minScore` and `coverageWeight` do not reach it, and lengthening the pattern
  does not either. What reduces how often it happens is grammar-side: keep a
  pattern's first required element a word the decoder hears reliably (not a short
  unstressed function word), and give the intent an additional phrasing that reaches
  the words your speakers actually produce — see
  [A bare pattern's tail](Documentation~/command-recognition.md#do-not-leave-a-bare-patterns-tail-readable-as-another-command).
- **Note**: the trade is deliberate and was measured before it was taken. The
  alternative — excluding such candidates from selection rather than from firing —
  destroys 11 cleanly spoken commands on the same corpus, because a barred candidate
  winning its round is what absorbs leading debris that would otherwise be charged to
  the next command.

### A candidate barred for its missing first word can still fire as a disambiguation choice

- **Symptom**: With `disambiguateSiblingTies` on, the recogniser asks which of two
  commands you meant, and the one you pick fires although its own first required
  word was never spoken. The same pattern on the same utterance is
  [barred](Documentation~/scoring.md#the-leading-required-miss-bar) and silent
  when it has to win a round on its own — the entry above. The flag is **off by
  default**, and with it off nothing here applies.
- **Repro**: Register `fire_at : ["{ship}", "fire", "at", "{target}", "now"]` and
  `fire_to : ["{?ship}", "fire", "to", "{target}", "now"]`, with slots
  `ship: alpha` and `target: bravo`, turn `disambiguateSiblingTies` on, and feed
  the transcript "alpha bravo now". Both candidates score `3 / 5` = `0.60`,
  clearing the default `minScore`, so the tie is offered as a question — "at" or
  "to".
  Answer "to" and `fire_to` fires, although "fire" was never spoken. Registering
  `fire_at` first is load-bearing, not incidental — see **Root cause**.
- **Where seen**: issue #126, raised against the leading-required-miss bar (#124)
  and ruled *recorded, not gated*. Pinned by three tests in
  `Tests~/Runtime/VoxrCommandRecogniserInjectionTests.cs`.
- **Root cause**: the bar is applied to the round's **winner** only. A tied
  sibling rival is recorded before the bar runs and carries no leading-miss
  information, so it reaches the choice list intact and fires when it is picked.
  Getting there needs a **mixed-anchor** set — one whose members disagree about
  which element is first required — and, just as necessarily, the *unbarred*
  member has to **win** the round. A tie never displaces an incumbent, so
  registration order decides it: register the barred-prone member first and it
  wins, the bar refuses it, and the round yields nothing at all. Here `fire_at` is
  anchored on the required slot `{ship}`, which matched "alpha"; `fire_to`'s
  leading slot is optional, so its own first required element is "fire", which
  nothing matched. The two are
  siblings at all only because `{?ship}` and `{ship}` normalise to the same
  element. Note that the word settling the question ("to") is not the word
  `fire_to` is missing ("fire"), so answering supplies no evidence for the anchor
  that went unheard.
- **Why this is recorded rather than fixed**: in the set above both members missed
  the same two words — the shared verb `fire` and the discriminator — and the
  member that survives the bar is admitted only because a matched leading *slot*
  precedes its verb. So there, both choices fire a command whose verb went unheard.
  Suppressing the barred rival would, in a two-member set, drop the choice list
  below two — and `TryBuildAmbiguity` returns false below two choices, so the
  question would not be asked at all and the surviving member would fire anyway. A
  larger set keeps its question and simply loses an option. That narrows the
  question, not the hazard.
- **And where the discriminator *is* the barred member's anchor, gating would be
  worse.** Register `resume_fire : ["{ship}", "resume", "fire"]` and
  `cease_fire : ["{?ship}", "cease", "fire"]` and say "alpha fire": both match
  `fire`, and each misses only its *own* discriminating word, so the question
  offers "resume" or "cease" — and answering **supplies** the anchor that went
  missing. The command that fires is one whose first required element the speaker
  really did utter. Refusing barred rivals wholesale would delete that question and
  hand the round to whichever member was registered first, which is the coin flip
  `disambiguateSiblingTies` exists to replace.
- **Not the uniform case**: where the discriminating word is **every** member's
  first required element, the round's winner is barred, the round yields nothing
  and no question opens. That case is unchanged — see *Sibling patterns that
  differ at one word fire the first-registered one* below.
- **Workaround**:
  - Leave `disambiguateSiblingTies` off, which is the default.
  - Do not author one member of a sibling pair with a leading **optional** slot
    where its sibling leads with the required form; that mismatch is what makes
    the set mixed-anchored.
  - Register the barred-prone member **first**. The tie-break is deterministic, so
    that member wins the round, the bar refuses it, and the path closes outright —
    at the cost of the whole set: no command fires and no question opens for it.
  - Give the riskier intent `requiresConfirmation`.
    `PendingCommandHandler.Complete` re-enters pending on the **chosen
    alternative's own** definition, so a confirmation declared on that intent does
    apply to it.

### Default `bufferWindow` is too short for split commands on Quest 3

- **Repro**: Speak a two-part command with a deliberate mid-command pause:
  "launch missiles" ... "target hotel one". On Quest 3 the gap measured
  between VOSK results is often ~1.9–2.1s, so any window shorter than that
  flushes before the second half arrives and the command is lost. The current
  0.5s default is well short of this; even the former 1.5s default (v2.3) was
  marginal — just under the typical gap.
- **Where seen**: v2.3 test matrix Phase 4.1 (pass-with-note), Phase 8.2
  retry (v2.4), and general notes — 2.0s is a more reliable value on Quest 3.
- **Root cause**: VOSK on Quest 3 emits final results with ~0.5–1.0s latency
  after speech ends, compounding any mid-command pause the speaker takes.
  The default matches typical PC latency (1.5s in v2.3, later lowered to 0.5s
  for a snappier PC baseline); on Quest hardware the buffer needs more slack.
- **Workaround**:
  - Set `bufferWindow=2.0` in the inspector for Quest builds.
  - Do not push beyond ~2.5–3.0s: the test matrices found that long windows
    start merging genuinely unrelated utterances ("cross-command bleed").

### Utterance buffer drops split commands if the pause exceeds `bufferWindow`

- **Repro**: Pause longer than `bufferWindow` mid-command. The first half
  flushes before the second half arrives, and neither matches a pattern.
- **Where seen**: v2.3 Phase 5.3, v2.3 Phase 4.1 note.
- **Root cause**: By design — the buffer has to flush eventually or it would
  merge unrelated speech. There is no retry.
- **Workaround**:
  - Tell users to speak commands in one breath, or provide a visible "hold
    to talk" affordance that gates recognition to a single burst.
  - For conversational UX, prefer shorter patterns that complete within one
    VOSK final result.

### A command that is also a prefix of a longer one can't be both instant and split-safe

- **Repro**: Register `["fire"]` and `["fire", "at", "{target}"]`, enable
  `eagerFlushOnCompleteMatch`, then say "fire" and pause longer than
  `bufferWindow` before "at hotel one". Eager flush deliberately does *not* fire
  "fire" early (it is a prefix of the longer command), so it waits the full
  window — and if the pause exceeds it, only "fire" is recognised.
- **Root cause**: Inherent, not a bug. A complete command that is also a prefix
  of a longer command is genuinely ambiguous until either more speech arrives or
  the window expires. No time/parse strategy makes it both zero-latency and
  correct: firing instantly would drop the longer command; waiting adds latency.
  Eager flush resolves this conservatively by waiting (correctness over latency);
  non-prefix commands are unaffected and fire instantly.
- **Workaround**:
  - Set `prefixHoldSeconds` (e.g. 0.5–0.8) to bound the wait. The ambiguity is
    only over a continuation, which a continuing speaker begins almost
    immediately, so the prefix command fires after that much silence instead of
    the full `bufferWindow`. It shortens the wait rather than removing it — the
    tradeoff above is unchanged, just cheaper.
  - Use push-to-talk (`VoxrPushToTalkController`); `ReleaseTalk` calls
    `FlushPendingBuffer()`, giving the prefix command a deterministic,
    zero-latency endpoint.
  - Avoid registering commands that are exact prefixes of others when low latency
    matters — e.g. give the shorter command a distinct extra keyword.

### A spoken slot value is silently discarded when its introducing word is dropped (residual case)

> **Narrowed by #65.** Coverage (#65 §5.2) closed the common form of this: with
> `["decelerate"]` and `["decelerate", "by", "{burn_level}"]` registered,
> "decelerate hard burn" now fires the slot-filled pattern at `2/3` = `0.67` with
> the burn level extracted, where it used to fire the bare command at `1.0` and
> discard it. What remains is the case below, which coverage cannot reach.

- **Repro**: Take the pair above and additionally register any pattern that can
  *begin* on the stranded value's first word — `["hard", "stop"]` will do. Say
  "decelerate hard burn" with the "by" elided by the speaker or dropped by VOSK.
  The bare pattern fires at score 1.0 and `burn_level` is empty — the command runs
  at its default level, with nothing reporting that a burn level was heard and
  thrown away. This is at the **default** `coverageWeight` of `1.0`. The original
  form was observed in-headset; this residue is measured, not observed in the wild.
- **Root cause**: Coverage charges a candidate for what it leaves unexplained, but
  the trailing count is a *run* that stops at the first token which could begin
  another match — the rule that keeps multi-command utterances intact. When the
  stranded value's own first word is such a token, the run terminates at once, the
  bare pattern is charged nothing, and it is back to matching perfectly at `1/1` =
  `1.0` against the slot-filled `2/3` = `0.67`. No threshold or weight tuning
  reaches it: a score normalised to 1.0 is the ceiling, so nothing can outrank the
  bare pattern while it matches exactly. The same applies wherever the orphan test
  charges nothing, including a grammar with a slot-initial pattern over a
  permissive slot (see the entry above). Short unstressed function words are the
  most-dropped tokens in practice, which is what makes the shape worth avoiding
  rather than tolerating.
- **Workaround**: Mark the droppable literal optional — `["decelerate", "?by",
  "{burn_level}"]`. An omitted optional leaves both sides of the score ratio, so the
  slot-filled pattern also scores 1.0 with or without the word, and wins as the
  candidate covering more of the utterance. This still works in the residual case,
  where coverage alone does not — which is why the construction-time warning was
  deliberately *not* narrowed when coverage shipped, even though the parser now has
  the information to narrow it. Removing the literal outright
  (`["decelerate", "{burn_level}"]`) works too, at the cost of the phrasing.
- **The warning**: `VoxrCommandParser` logs it at construction, naming the literal
  and the slot at risk — in the **Editor only**, so look for it there rather than
  in a device log. The scan follows what the parser itself compares, so it covers
  the hazard across *different intents* as well as within one command, behind a run
  of two or more literals, and after expanding a bare pattern's own optional
  elements; only patterns with more than six optionals are compared unexpanded.
- **The optional-literal swap has two costs**, neither of them a no-op:
  - A matched optional literal scores 0.5 on both sides of the ratio where a
    required one scores 1.0, so any *imperfect* match scores strictly lower than
    before and may now fall under `minScore`.
  - An optional literal no longer anchors the element after it, so a following slot
    can claim adjacent tokens the literal never introduced (with
    `orient heading {heading} ?mark {?elevation}`, a stray fourth digit is absorbed
    as `elevation` and wins on span, where the required form scored 0.8 and dropped
    it). Prefer the swap where the trailing slot's vocabulary is distinct from its
    neighbours'; be careful where it is a `NumberSequence`.

### Sibling patterns that differ at one word fire the first-registered one

- **Symptom**: Two commands share every element but one — `switch to weapons` and
  `switch to navigation`, or `set {ship} mode on` and `set {ship} level on`. The
  speaker says one of them, VOSK drops the discriminating word, and the *other*
  command fires. Not an early fire: the wrong command. (With
  `disambiguateSiblingTies` on, the speaker is asked instead — see the remedy below.
  This entry describes the default, which is off.)
- **Repro**: Register both `["switch", "to", "weapons"]` and
  `["switch", "to", "navigation"]`, then feed the transcript "switch to". Both
  patterns score `(1 + 1 + 0) / 3` = 0.67, which clears the default `minScore`.
- **Root cause**: The surviving evidence fits both siblings *equally*, so they tie
  on score, on consumed span and on literal count, and selection falls through to
  its final key — registration order. The word that would have decided is exactly
  the one that went missing, so no scorer can recover the intent; the parser is
  guessing, and it guesses consistently rather than randomly.
- **Timing: the eager gate refuses on both shapes, which changes when the guess
  happens, not what fires** (with the flag off, which is the default). For a
  *trailing* discriminator it always refused — it will not commit a pattern whose
  trailing required element never matched. A discriminator in the *middle* clears
  that particular rule (`set {ship} mode on` heard as "set alpha on" scores
  `3 / 4` = 0.75 and spans the buffer) and used to commit **early** with the wrong
  sibling; the gate now declines whenever the buffer fits two different intents
  equally, one required word apart. The same command still fires at the end of
  `bufferWindow` — deferring cannot let the missing word arrive, since speech only
  appends and the position it would have occupied is already behind the match.
  What deferring buys is that the decision is made once, on a final transcript —
  **which is where the recogniser can ask you instead of guessing.** (The refusal
  is an eager-gate rule and needs `eagerFlushOnCompleteMatch`; the remedy below
  does not, and works on default settings.)
- **There is now a supported remedy: `disambiguateSiblingTies`.** With it on, a flush
  that ties this way stops guessing and asks. `OnCommandPending` raises with
  `PendingAmbiguity` set, carrying the competing commands and the one word that tells
  each apart; the speaker says that word and the right intent fires with its slots
  intact. Off by default, because an ambiguous utterance then fires *nothing* until it
  is answered — with no `OnCommandPending` subscriber that is worse than the coin flip.
  See [Ask instead of guessing](Documentation~/command-recognition.md#ambiguous-commands-ask-instead-of-guessing).
  **What the rest of this entry describes is what remains with the flag off**, which
  is the default.
- **The parser now warns about this shape at construction**, in the Editor, naming
  the intents, the patterns as authored, the differing element and the competing
  values. It reports only what it can see going wrong: two patterns of *different*
  intents (within one intent the same command dispatches either way), where the tie
  would actually clear the `minScore` configured on the recogniser, and where the
  differing word is not *every* pattern's **first required element** — where it is,
  dropping it bars whichever candidate wins the round, so nothing fires and there is
  nothing to report (see *What now works, and used to not* below). Since #140 that
  second condition reads the threshold you configured rather than a copy of the
  default, so the scan tracks your settings: lower `minScore` and the short pairs it
  makes live are reported, raise it and pairs that can no longer fire wrongly go
  quiet. See the note below for which pattern lengths that works out to. The third
  condition reads no threshold at all and is unaffected either way.
- **Workaround**: Turn on `disambiguateSiblingTies` and prompt from
  `OnCommandPending`, which is the only remedy that keeps both phrasings and still
  gets the right command. Where you cannot prompt: make the two commands differ in
  **more than one element**, so losing one word still leaves another to decide
  (`arm weapons` / `show navigation`, not `switch to weapons` /
  `switch to navigation`); or move the differing word to the front, so it is **every**
  pattern's **first required element** — dropping it then bars whichever candidate
  wins the round and that round yields nothing, which buys silence instead of a wrong
  command at the price of the speaker having to say it again, does nothing for an
  utterance where the word *was* heard, and correctly silences the construction-time
  warning for that pair (see *What now works, and used to not* below) — note it also
  puts that pair beyond `disambiguateSiblingTies`, since the round yields nothing and
  no pending opens for the speaker to be asked from; or give the
  more destructive of the pair `requiresConfirmation`; or — where both phrasings must
  exist verbatim — register the safer one first, since the tie-break is deterministic.
- **What now works, and used to not**: moving the difference *earlier* in the
  pattern, so the differing word is each pattern's **first required element**.
  `weapons mode` and `navigation mode` still tie at `0.5`, and `weapons mode active`
  against `navigation mode active` still ties at `0.67` — but dropping a leading
  required element now bars whichever of the two wins the round, at any pattern
  length, so the outcome is silence rather than the wrong command. Note what it buys
  and what it does not: silence instead of a wrong action,
  with the speaker having to repeat themselves, and no help at all on an utterance
  where the discriminating word *was* heard. It also stops the construction-time
  warning above reporting that pair, correctly — the claim that warning makes would
  no longer be true of it — so a warning that vanishes when you apply this is the
  remedy landing, not the pair going away.
- **Note**: This shape predates the current miss cost. At four or more elements it
  already cleared the gate; reducing the miss cost extends it down to three-element
  patterns, which is where two-word-prefix grammars live. Those lengths are read off
  the `minScore` you configured — at the default `0.6` a three-element pattern's
  `0.67` clears and a two-element pattern's `0.5` does not; lower the threshold and
  shorter pairs become live and are reported. That reach stops only where
  the differing word leads **every** pattern in the set — there the round's winner is
  barred and nothing fires at any length, per the entry above. Where it leads some but
  not all of them the reach still holds, because the round can be handed to a
  candidate the bar does not touch.

### A pattern with more than six optional elements is not checked for siblings

- **Symptom**: A grammar carries the sibling shape above and **no construction-time
  warning is logged** — while an otherwise identical grammar with one fewer optional
  element warns as expected. With `disambiguateSiblingTies` on, the tie is also not
  offered as a question. (The eager-flush gate still refuses on this repro, for the
  reason below — but it is not exempt in general either.)
- **First check it is this entry at all.** A missing sibling warning has a
  *second* cause, and it has nothing to do with optional elements: where the
  discriminating word is **every** member pattern's **first required element**,
  the warning is withheld on purpose — dropping that word bars whichever
  candidate wins the round, so nothing fires and there is nothing to report.
  Look at the discriminating word before you count optional elements: if it
  leads every pattern in the set, that deliberate withholding is what you are
  seeing and this entry does not apply. See *Sibling patterns that differ at
  one word fire the first-registered one* above.
- **Repro**: Register `["engage", "?a", "?b", "?c", "?d", "?e", "?f", "?g",
  "shields", "online"]` against `["engage", "weapons", "online"]` and say
  "engage online". Remove any one of the seven optionals and the same utterance
  behaves differently.
- **Root cause**: Deciding whether two patterns are siblings means comparing every
  reading of each — a pattern with `N` optional elements has `2^N` of them — so past
  six the **set-building scan** stops expanding and takes the pattern only **as
  authored**, `?` markers and all. (An optional *slot* still folds with its required
  form for comparison; an optional *literal* `?word` buckets only with another
  literal `?word`.) It still builds a set from that reading — two patterns spelling
  the same seven optionals and differing at one word are still warned about — but a
  relation visible only in some expanded reading is never seen, and the repro above
  is that case. That bound was set when the comparison fed nothing but
  an Editor warning, where it cost recall on one message. It now also feeds runtime
  behaviour, so an unexpanded pattern's relations are *unknown* rather than absent.
- **What the parser does about it**: it does not assume, and this is a *second*
  mechanism rather than the same one. Where the set-building scan gave up, the
  **runtime pair test** falls back to comparing the two patterns' required elements —
  the all-optionals-omitted reading — which is a real reading the matcher can produce.
  So the eager gate still refuses to commit on such a pair. A relation visible only in
  some *middle* reading is the part that stays invisible to both.
- **Where seen**: repository test suite, eager-commit coverage for the expansion cap.
- **With `disambiguateSiblingTies` on, such a rival is never offered as a choice.**
  That fallback proves the two patterns tie but cannot say *which word* tells them
  apart, and without that word there is no question to phrase. If it is the only rival,
  the flush fires the winner exactly as it would with the flag off; if another rival
  makes a question happen anyway, the unnameable one is missing from the list and
  `PendingAmbiguity.IsTruncated` says so, so you can offer "…or say the whole command
  again".
- **Workaround**: Keep patterns under seven optional elements, which is well inside
  normal authoring. If you need more, do not also rely on a single required word to
  separate two intents — that combination is the one this cannot see.
- **Note**: This bound (6) is deliberately lower than the one that governs
  eager-flush eligibility (12). The sibling comparison runs at construction on every
  parser rebuild in the Editor — and in a player build whenever
  `disambiguateSiblingTies` is on; a flag-off player builds the lookup lazily from
  the eager path — where the eligibility analysis is always lazy.

### Two intents on duplicate or overlapping patterns: the second can never fire, and nothing warns

- **Symptom**: Two commands carry the same pattern (or patterns that overlap
  completely on an utterance). The first-registered intent fires every time, at a
  clean score; the other is permanently dead. No construction-time warning is
  logged, and a batch run shows a healthy `1.00` PASS for the winner.
- **Repro**: Register `["shields", "up"]` under `raise_shields`, then again under
  `activate_defence`. Say "shields up" — `raise_shields` fires, always.
- **Root cause**: The two candidates tie on every selection key — same start, same
  score, same span, same literal count — and a tie keeps the incumbent, so
  registration order decides permanently. The sibling machinery does not apply:
  both the construction-time warning and `disambiguateSiblingTies` require the
  patterns to differ at exactly **one** position, and these differ at none — there
  is no discriminating word to warn about, and no word the speaker could answer
  with.
- **Detection**: the Editor names the rival since
  [#95](https://github.com/jinwoo1601/VoXR-Speech-Recognition/issues/95). The debug
  window's last-match breakdown and the Batch Test Runner's per-row diagnostics
  show a `Tied with:` line reading `— not a sibling; check for duplicate or
  overlapping patterns` whenever a rival scored exactly as well as the winner. The
  exported session log records the same finding per attempt, as `tiedRival` and
  `tiedRivalIsSibling`, so a whole playtest can be swept for it after the fact.
- **Workaround**: This is a grammar defect rather than a recognition limitation —
  remove the duplicate pattern, or differentiate the two in more than one element.
  If two intents genuinely share a phrasing, register the pattern under one intent
  and branch in your handler.

### Confidence of `-1.00` means "no data", not "zero confidence"

- **Repro**: Inject text through `InjectText` without a `VoxrWord[]`, or take
  any result VOSK delivered with no per-word data. The logged per-command
  confidence is `-1.00`.
- **Where seen**: v2.1 Phase 2.3 (triggered the bug that introduced the
  sentinel), v2.2 Phase 6.2, v2.3 Phase 3.2–3.3, v2.4 Phase 3.8.
- **Root cause**: The aggregate is the *minimum* per-word confidence over the
  matched span, never an average. When no per-word confidence is available for
  that span, there is nothing to take the minimum of, so the parser returns
  `-1.0` as a sentinel meaning "no data" and `VoxrCommandRecogniser` treats
  this as *not subject to* `minConfidence`, so the command still fires based
  on pattern-match score. A leading `[unk]` run is **not** a cause — selection
  never starts a match on `[unk]`, so a winning span always holds at least one
  matched word. See
  [Matching and Scoring](Documentation~/scoring.md#minconfidence-default-04)
  for the second, less obvious way `-1` arises.
  Without this sentinel (v2.0 used raw `0.0`), genuine noise that drove
  confidence to 0 was indistinguishable from "no data" and either bypassed
  the threshold or was falsely rejected.
- **Workaround**: None needed. If you display confidence in a debug UI,
  treat `-1.00` specially ("no data" / "n/a"), not as "zero".

---

## Hardware Audio

Limitations specific to capturing audio on target hardware. These are
pre-solved inside `NativeBridge~` but are worth documenting because they
constrain how the native layer is structured and will matter again on any
future device port.

### AAudio input delivers silence on Quest 3 — must use Java AudioRecord

- **Repro**: Build the SDK with `audio_capture_aaudio.cpp` as the active
  capture backend. `AAudioStream` opens and starts without error and
  callbacks fire on schedule, but the audio buffer is near-zero regardless
  of input preset (`GENERIC`, `VOICE_RECOGNITION`, `UNPROCESSED`).
- **Where seen**: Quest 3 bring-up (2026-03-31).
- **Root cause**: Platform-specific AAudio input bug on Quest 3 firmware.
  The native layer switched to the Java `AudioRecord` API via JNI
  (`audio_capture_audiorecord.cpp`), which routes correctly to the headset
  microphone. The old AAudio implementation is retained for reference.
- **Workaround**: Already applied — the shipped build uses AudioRecord. If
  porting to another Android device, test AAudio first; it may work outside
  Quest 3. Do not remove the AAudio files; they are a fallback seed.

### `vosk_recognizer_accept_waveform_f` is broken on prebuilt arm64 `libvosk.so`

- **Repro**: Feed float samples directly to VOSK via
  `vosk_recognizer_accept_waveform_f`. Audio levels reach the library
  correctly, but every recognition result is empty.
- **Where seen**: Quest 3 bring-up.
- **Root cause**: Bug in the prebuilt arm64 `libvosk.so` — the int16 entry
  point (`vosk_recognizer_accept_waveform_s`) works, but the float entry
  point does not produce usable output. Not investigated upstream; likely a
  build-config issue in Alphacephei's prebuilt.
- **Workaround**: Already applied — `vosk_bridge.cpp` converts float samples
  to int16 before feeding VOSK. If you rebuild `libvosk.so` yourself, retest
  the float path before switching to it — the workaround cost is one extra
  copy per buffer, which is negligible.

### Quest 3 microphone gain is low; AGC is applied before VOSK

- **Repro**: Capture raw samples from Quest 3's `VOICE_RECOGNITION` source
  while speaking at normal volume. Peak levels sit around 0.04–0.4 on a
  `[-1, 1]` scale.
- **Where seen**: Quest 3 bring-up.
- **Root cause**: Quest 3's mic pipeline outputs conservatively low levels
  — likely intentional headroom for louder-than-expected input — but this
  leaves VOSK's acoustic frontend operating near its noise floor for quiet
  speech.
- **Workaround**: Already applied — an AGC stage in `vosk_bridge.cpp`
  targets a configurable dB level before converting to int16. The target is
  exposed on `VoxrSpeechRecogniser` as the `micGainTargetDb` inspector field
  (default `-18 dB`, calibrated for Quest 3). Tune it if you observe clipping
  or under-gain on a different device.

---

## Notes

This file is meant to grow as we discover more limitations. When adding a new
entry, follow the existing structure: short repro, where seen (test matrix
reference if applicable), root cause, workaround. Group entries by category —
the categories above are a starting point but feel free to add more (e.g.
"Threading", "Build/Deploy") as needed.

Version-shaped references here come from two separate namespaces, and the
shape tells them apart rather than the range. A `v`-prefixed **two**-part
number — `v2.1`, `v2.0` — is one of the project's internal pre-1.0 verification
phases. That holds whether or not the surrounding text says "test matrix
Phase": several entries cite a phase in passing, as in "(v2.0 used raw
`0.0`)". A **three**-part number — `1.4.0`, `2.0.0` — is a package release, and
every one of those has its own dated section in `CHANGELOG.md`. The two
numbering schemes ran independently and any overlap between them is
coincidence. Issue numbers (`#65`, `#82`, …) refer to the GitHub issue that
introduced or changed the behaviour.

---

# DOCUMENT 2: COMMAND RECOGNITION PIPELINE

# Command Recognition

This guide explains how the SDK turns raw speech into structured commands. It covers the full parsing pipeline, pattern syntax, slot types, scoring, and the choice between grammar-constrained and free-speech recognition.

---

## Overview: How an Utterance Becomes a Command

When the user speaks, the audio passes through a multi-stage pipeline before your `OnCommandRecognised` handler fires. Understanding these stages helps you diagnose matching issues and tune the system effectively.

```
Microphone Audio
    |
    v
VOSK Recogniser (speech-to-text)
    |  produces a transcript string + per-word confidence
    v
Utterance Buffer
    |  merges consecutive VOSK results within bufferWindow seconds
    |  (handles mid-command pauses that VOSK splits into separate utterances)
    v
Pending Command Check (only while a command is pending)
    |  the flushed transcript is offered to the pending command FIRST:
    |  cancel, then a disambiguation choice, then confirm, then slot-fill
    |  cancel/choice/confirm answers bypass every stage below; a slot-fill
    |  still parses, and a complete new command wins over the fill
    |  (speech that answers nothing falls through and is parsed normally)
    v
Parser (pattern match + scoring)
    |  tries each command pattern against the transcript
    |  uses sliding start to skip preamble/filler words
    |  extracts slot values, computes normalised score (0.0-1.0)
    v
Sequential Extraction
    |  extracts multiple commands left-to-right from a single utterance
    |  ("cease fire launch missiles target hotel one" -> two commands)
    |
    |  leading-required-miss bar -- applied within extraction, before any
    |  threshold: a round whose winner missed its FIRST required element
    |  consumes its span and yields no command, so a round can produce nothing
    v
Threshold Filter
    |  rejects commands below minScore or minConfidence
    |  confidence of -1 (no data) bypasses the minConfidence check
    |  rejects commands missing a required slot, at ANY score --
    |  with allowPartialMatch they enter pending state instead
    v
Debounce
    |  suppresses duplicate intents within commandCooldown seconds
    v
Pending Entry
    |  sibling ties enter pending state when disambiguateSiblingTies is on
    |  commands with requiresConfirmation enter pending state
    v
Events: OnCommandRecognised, OnCommandsRecognised, OnUnrecognisedSpeech
        OnCommandPending, OnCommandConfirmed, OnCommandCancelled
```

Each stage is configurable. The most common tuning points are `bufferWindow` (how long to wait for split speech), `minScore` / `minConfidence` (quality thresholds), and `commandCooldown` (debounce window).

Four terms recur throughout this guide:

- **Flush** -- the moment the utterance buffer hands its merged transcript to the parser: at the end of `bufferWindow`, or early via eager flush or a push-to-talk release.
- **Pending** -- a command held waiting for follow-up speech: missing slots, a confirmation, or a disambiguation answer. See [Pending Commands](#pending-commands).
- **Eager flush** -- the opt-in that fires a complete, unambiguous, unextendable command before the window closes. See [Eager flush](#eager-flush-low-latency-complete-commands).
- **Sibling tie** -- two patterns of *different* intents that differ at exactly one required word, so dropping that word leaves them indistinguishable. See [Authoring hazards](#do-not-separate-two-commands-by-a-single-word).

---

## Patterns and Slots

Commands are defined as token arrays. **Literal tokens** must appear in the speech exactly as written. **Slot tokens** (wrapped in `{}`) match against registered slot values.

```csharp
// Pattern: "launch {weapon} target {target}"
// Matches: "launch missiles target alpha one"
// Extracts: weapon="missiles", target="alpha one"
new VoxrCommandDefinition("launch_weapon",
    new[] { new[] { "launch", "{weapon}", "target", "{target}" } })
```

Multi-word slot values (e.g. `"alpha one"`) are consumed greedily -- the parser tries longer matches first to avoid partial matches.

A command can have multiple alternative patterns, each representing a different way the user might phrase the same intent:

```csharp
new VoxrCommandDefinition("launch_weapon", new[] {
    new[] { "launch", "{?quantity}", "{weapon}", "target", "{target}" },
    new[] { "fire", "{weapon}", "at", "{target}" },
})
```

---

## Optional Slots

Prefix a slot reference with `?` to make it optional. The parser consumes it if present and skips it if absent -- both phrasings match the same intent.

```csharp
// "{?quantity}" is optional
new[] { "launch", "{?quantity}", "{weapon}", "target", "{target}" }
// Matches both: "launch missiles target alpha one"
//           and: "launch two missiles target alpha one"
```

Optional literal tokens also work: `"?the"`, `"?a"`. However, single-character words are unreliable in VOSK grammar mode -- the acoustic model frequently misrecognises or drops them. Prefer slot value aliases instead (see below).

**The `?` for an optional slot goes *inside* the braces: `{?quantity}`.** Writing `?{quantity}` does not make the slot optional -- it parses as a required *literal* token no utterance can ever produce, so every match of that pattern silently misses a required element, with no warning and no exception.

Two pattern shapes carry authoring hazards the parser warns about at construction: a required function word standing between a bare pattern and its slot, and two commands separated by a single word. A third hazard is not a pattern shape at all -- one intent registered by more than one command definition. A fourth shape, a bare pattern whose tail can be read as another command, carries no construction-time warning at all. All are covered in [Authoring hazards](#authoring-hazards), after the scoring and buffering concepts they depend on.

---

## Scored Matching

> This section is the working summary. For the full model — the per-element score table, the selection and tie-break order, the eager-flush verdict rules, and worked examples traced through to their session-log entries — see [Matching and Scoring](scoring.md).

Every match produces a normalised **score** (0.0--1.0) built from two halves: how well the transcript satisfied the pattern, and how much of the utterance the match left unexplained. The parser uses a sliding start to tolerate preamble, hesitations, and false starts, so a pattern can match anywhere in the transcript -- and what it walks past or leaves behind counts against it (see [Coverage](#coverage)).

Two independent thresholds control what gets through, both set on the `VoxrCommandRecogniser` component **in the Inspector**:

```
minScore        0.6    // Reject low-quality pattern matches
minConfidence   0.4    // Reject low VOSK word confidence
```

They are serialized fields with no public setter, so there is no code path for changing them at runtime -- tune them on the component, and regression-test the change with the [Batch Test Runner](api/batch-test-runner.md), which does take both as constructor arguments.

**Score** (`VoxrCommand.Score`) is computed by the parser based on how well the transcript satisfies the pattern, normalised against a *dynamic* denominator. Required tokens always count toward that denominator; optional tokens (`?word` literals and `{?slot}` slots) count only when they are actually spoken. An omitted optional therefore drops out of both sides of the ratio rather than diluting it, so a perfect match scores 1.0 whether or not its optional tokens were uttered — taking advantage of optionality is never penalized. A missed *required* token still pulls the score down, and so does anything the match left unexplained — see [Coverage](#coverage).

**Confidence** (`VoxrCommand.Confidence`) is the minimum per-word VOSK acoustic confidence across matched tokens. This reflects how certain VOSK was about the words it heard. A value of `-1` means no word-level data was available *for the matched span* (usually injected text, which carries none), which bypasses the `minConfidence` check entirely -- the command is accepted or rejected on score alone. See [the two gates](scoring.md#minconfidence-default-04) for the second, less obvious way `-1` arises and for how a repeated word resolves.

### Coverage

The sliding start can begin a match anywhere in the utterance, and a pattern stops when its elements run out. A command is therefore scored on how much of the utterance it **explains**, not only on how neatly it matched the part it chose: `coverageWeight` (default `1.0`, named `skippedWordPenalty` before #65) adds every in-grammar token the match leaves unexplained to the score denominator — both those the start walked past to reach the match and those left over after it.

Without the leading half, any stray sentence whose *tail* happened to resemble a short pattern would execute it at a full 1.0 — "thrusters port", misheard as "thrusters report", would skip the unmatched "thrusters" and fire a one-word `report` command.

| Utterance | Matched pattern | Score |
|-----------|-----------------|-------|
| `disengage` | `["disengage"]` | `1 / 1` = 1.0 |
| `target disengage` | `["disengage"]` | `1 / (1 + 1)` = 0.5 -- rejected at the default `minScore` |
| `disengage target` | `["disengage"]` | `1 / (1 + 1)` = 0.5 -- the trailing side, charged the same |
| `launch launch all missiles target hotel one` | 5-element `launch_weapon` form | `5 / (5 + 1)` = 0.83 -- still accepted |

The charge is proportional, so it only bites patterns short enough to be swallowed whole by a longer utterance; longer commands still absorb a false start.

**It is applied while candidates are compared, not to the winner afterwards.** So it decides *which pattern wins* — and that is what stops a bare pattern out-ranking a slot-filled sibling that explained more of what was said (see [the function-word hazard](#never-leave-a-required-function-word-between-a-bare-pattern-and-its-slot) above).

Three things go uncharged (the third with one exception, noted below):

- **`[unk]` tokens.** Out-of-grammar preamble and hesitation are exactly what the sliding start is for, so filler VOSK could not resolve stays free — and it is transparent rather than a run terminator, so one noise token cannot hide the real leftovers behind it. Only the literal `[unk]` is exempt, which is why `freeSpeechMode`, `InjectText`, and the batch runner charge trailing filler that the grammar-constrained decoder would have hidden.
- **Words before a previous match ended.** Counting restarts after each extracted command, so chained commands in one utterance ("cease fire resume fire") do not penalise each other.
- **Trailing tokens that could begin another match.** Counting stops at the first token some active pattern can be matched from with more of its required elements matched than missed — including a pattern that gets there by missing leading elements the decoder dropped — which is what keeps multi-command utterances intact: "cease fire launch missiles target hotel one" scores `cease_fire` at a full `2 / 2`, not `2 / 7`. The exception is a token the candidate's own next required element just tried and failed to match, which is always charged — see [the full rule](scoring.md#what-counts-as-orphaned).

Set `coverageWeight` to `0` to restore the pre-#31 behaviour — note this also switches off the #42 protection added in #65, so a bare pattern can once more win over its slot-filled sibling. Raise it above `1.0` to demand that a command be an even larger share of what was said.

Existing grammars re-score on upgrade, with no compatibility mode. The visible change is that a short command trailed by words the grammar cannot place may stop firing where it used to — see [Known Limitations](../KNOWN_LIMITATIONS.md) for the measured cases and the authoring responses. The full rule, including the orphan test above and the exception that keeps it from rewarding a worse match, is in [Matching and Scoring](scoring.md#2-coverage).

When tuning thresholds:
- Start with the defaults (`minScore=0.6`, `minConfidence=0.4`) and adjust based on testing.
- Don't push `minConfidence` above `0.5` unless you've verified your vocabulary avoids "two" and other low-confidence words (see [Known Limitations](../KNOWN_LIMITATIONS.md)).
- Use the [Batch Test Runner](editor-testing.md#batch-test-runner) to regression-test threshold changes.

---

## Slot Value Aliases

Map variant words to canonical values so the parser normalises them automatically:

```csharp
var quantity = new VoxrSlotDefinition("quantity",
    new[] { "one", "two", "three", "all" },
    new Dictionary<string, string> { { "a", "one" }, { "jackals", "jackal" } });
```

When VOSK transcribes `"a"`, the alias resolves it to `"one"` in the extracted slot value. Aliases are included in the generated grammar JSON, so VOSK knows to listen for the variant words.

**Validation:** The parser warns at configure time about slot *values* and alias keys alike that are uppercase (VOSK outputs lowercase, so such a form can never match), that carry punctuation (VOSK strips it -- write `oclock`, not `o'clock`), or that are a single character. The last is informational: short tokens are recognised unreliably, but `a` above is deliberate, since an alias to a longer canonical resolves when VOSK does hear the word and costs nothing when it is dropped. Unlike the Editor-only authoring scans in [Authoring hazards](#authoring-hazards), these warnings fire in player builds too.

---

## Dynamic Slot Filtering

Slot value providers let you narrow which values the parser accepts for a slot at runtime, without changing the VOSK grammar. This is useful when the set of valid targets, items, or options changes based on game state -- for example, only allowing the player to target enemies currently on screen, or restricting weapon selection to what's in their inventory.

```csharp
// Register a provider that returns only currently visible targets
commandRecogniser.RegisterSlotValueProvider("target", () =>
{
    return visibleTargets.Select(t => t.voiceName).ToArray();
});

// When targets change (spawn, die, enter/leave view), rebuild the parser
commandRecogniser.NotifySlotChanged();
```

### How it works

The **grammar** (VOSK vocabulary) always contains the full universe of slot values registered via `Configure()`. This means VOSK can transcribe any value at any time. The **parser** is rebuilt with only the provider's active values, so excluded values produce `OnUnrecognisedSpeech` instead of `OnCommandRecognised` -- or, on a command that sets `allowPartialMatch`, `OnCommandPending` asking for the slot to be filled again, since an excluded value reads as an unfilled slot rather than as a wrong one.

This two-layer design avoids the audio gap that grammar rebuilds cause (see [Command Sets](command-sets.md)). The trade-off: VOSK may still transcribe an excluded value since it's in the grammar, but the parser will reject it.

### Alias filtering

Aliases that point to excluded canonical values are automatically pruned. If "hotel one" is excluded, the alias "h one" → "hotel one" is also removed from the parser.

### Null and empty providers

- A provider returning **null** is treated as "no opinion" -- the slot uses its full static values.
- A provider returning an **empty array** means nothing matches -- all values for that slot are excluded.
- `NumberSequence` slots are unaffected by providers.

### When to use dynamic slots vs command sets

| | Dynamic Slot Filtering | Command Sets |
|---|---|---|
| **What it narrows** | Which *values* a slot accepts | Which *commands* are active |
| **Grammar impact** | None -- no audio gap | Full rebuild -- ~50ms audio gap |
| **Best for** | Contextual value lists (targets, items, locations) | Mode switching (weapons, navigation) |
| **Combines with** | Command sets (orthogonal) | Dynamic slots (orthogonal) |

The two features are complementary. Use command sets for coarse mode switching and dynamic slots for fine-grained value filtering within a mode.

---

## NumberSequence Slots

Capture spoken number words for headings, frequencies, grid coordinates, and similar numeric commands:

```csharp
var heading = VoxrSlotDefinition.NumberSequence("heading", minWords: 1, maxWords: 3);

// "heading two seven zero" -> heading="two seven zero"
// "heading one eight"      -> heading="one eight"
```

The parser greedily consumes consecutive number words within the configured `minWords`/`maxWords` range. The accepted set is the full `VoxrNumberParser.DigitVocabulary` — zero through nineteen, the tens (twenty, thirty, …, ninety), plus `hundred` and `thousand`. The full vocabulary is merged into the grammar JSON automatically.

> **The slot value is the spoken words, not a number.** `cmd.GetSlot("heading")` returns `"two seven zero"`, never `"270"`. `int.TryParse` on it fails on every utterance and returns `0` — silently, since `TryParse` does not throw — which reads as a command that simply never works. Convert the value yourself with [`VoxrNumberParser`](api/number-parser.md).

### Converting the value

`VoxrNumberParser.ParseDigitSequence()` handles digit-by-digit utterances ("two seven zero" → `270`) and rejects anything outside `zero`–`nine`. `VoxrNumberParser.ParseCardinal()` handles cardinal phrases ("two hundred" → `200`) and accepts the whole vocabulary. Both throw `FormatException` on words they do not accept, rather than returning a sentinel you could branch on, so the canonical pattern is to try the digit path first and fall back to the cardinal one. Guard for the empty string separately: an unmatched slot makes `GetSlot` return `""`, and both parsers map that to `0` rather than throwing — without the guard an absent slot silently becomes heading zero.

```csharp
using System;
using VoXR.Commands;

// Returns false when the slot is absent or the words parse as neither form.
static bool TryParseNumberSlot(VoxrCommand cmd, string slotName, out int value)
{
    value = 0;
    string words = cmd.GetSlot(slotName);   // e.g. "two seven zero" — words, not digits
    if (string.IsNullOrEmpty(words))
        return false;

    try { value = VoxrNumberParser.ParseDigitSequence(words); return true; }
    catch (FormatException) { }             // contains "ten"+ or a cardinal — try the other path

    try { value = VoxrNumberParser.ParseCardinal(words); return true; }
    catch (FormatException) { return false; }
}

commandRecogniser.OnCommandRecognised += cmd =>
{
    if (cmd.Intent == "set_heading" && TryParseNumberSlot(cmd, "heading", out int heading))
        Debug.Log($"Heading: {heading}");
};
```

The order is load-bearing, because the two parsers read the same words differently: `"two seven zero"` is `270` on the digit path but `9` on the cardinal one, so trying the digit path first is what lets digit dictation win. And a phrase only the cardinal path accepts gets its reading whatever the speaker meant — `"two seventy"` throws on the digit path, then parses as `72`, not the `270` most speakers intend by it.

So the fallback resolves *which parser* to use, not what the speaker meant. Pick one convention per slot: where a slot is always dictated digit-by-digit, call `ParseDigitSequence` alone and treat the `FormatException` as a misrecognition.

---

## Utterance Buffer

VOSK's voice activity detector can split mid-command pauses into separate utterances. The utterance buffer merges consecutive VOSK results within `bufferWindow` seconds before parsing.

`bufferWindow` is an Inspector field on `VoxrCommandRecogniser` -- like the thresholds, it is a serialized field with no public setter, so tune it on the component. Set it to `2.0` for Quest 3. Setting it to `0` disables buffering entirely: each VOSK result is parsed the moment it arrives, and eager flush and prefix hold below no longer apply.

If the speaker says "launch missiles" *pause* "target hotel one" and both results arrive within the window, they are concatenated and parsed as a single command.

**Tuning:** The default is 0.5s (tuned for typical PC latency). Quest 3 VOSK latency adds ~0.5--1.0s to inter-result gaps, so the default is usually too short on device — 2.0s is more reliable. Don't exceed ~2.5--3.0s or unrelated utterances may merge ("cross-command bleed").

### Eager flush (low-latency complete commands)

By default the buffer is purely time-driven: every command -- complete or not -- waits the full `bufferWindow` before firing. Enable **Eager Flush On Complete Match** in the Inspector to fire a command the instant the buffered speech forms a complete match that *cannot* be extended or completed by more words:

- **Complete and unambiguous** -> fires immediately, with zero buffer latency.
- **A prefix of a longer command**, or a **trailing slot that could still grow** (a multi-word enumerated value such as `"red"` -> `"red dragon"`, or a variable-length number sequence) -> keeps waiting the full window, so split commands are still recovered. "Prefix" is judged against slot vocabularies, not just pattern shape: a lone `{burn_level}` is *not* a prefix of `decelerate {burn_level}`, because no value of the slot begins with "decelerate".
- **Missing a required slot** (the pattern's literals are all in, but an argument has not been spoken yet) -> keeps waiting the full window, even where the arithmetic leaves the partial match above `minScore`. Unlike the case above it never qualifies for the shortened `prefixHoldSeconds` hold, because it is not a complete match. An unspoken slot consumes no words, so such a buffer otherwise looks complete right up to the moment the missing words arrive.
- **Still owing its last word** (the pattern's final required element has not been spoken yet, as in "switch to" against `switch to weapons`) -> keeps waiting the full window. A missing word consumes nothing, so such a buffer looks complete by every other measure; where a sibling command shares the prefix, committing would fire whichever of them happens to be registered first.
- **Missing its own first required element** -> keeps waiting the full window. The eager gate refuses a winner whose first required element matched nothing, above the point where the shortened `prefixHoldSeconds` hold would be armed, so such a buffer never qualifies for it. See [the bar](scoring.md#the-leading-required-miss-bar).
- **Ambiguous between two intents** -> keeps waiting the full window. Where the buffer fits two patterns of *different* intents exactly equally — same score, same span, same literal count — and they differ at just one required word, the winner would be decided by registration order alone. The gate declines rather than commit a coin flip early. This covers the *medial* drop the tail rule above cannot see: `set {ship} mode on` against `set {ship} level on`, heard as "set alpha on". The same command still fires at the end of the window — or, with `disambiguateSiblingTies` on, the speaker is asked which they meant. See [the one-word hazard](#do-not-separate-two-commands-by-a-single-word).
- **Split command** -> fires as soon as its second half completes, instead of waiting another full window on top.
- **Out-of-grammar preamble** (a station address such as "Helm, ...", reported by VOSK as `[unk]`) is skipped, so an addressed command commits as fast as the bare one. Only a *leading* run is skipped: anything left over at the end -- recognised or `[unk]` -- is treated as an in-progress tail and keeps waiting, as does a leading word VOSK did resolve.
- **While a command is pending, the eager path is skipped entirely.** Confirmations, slot-fills, and disambiguation answers always wait the full `bufferWindow` -- `prefixHoldSeconds` does not shorten them either. Push-to-talk's release flush is the way to give follow-up speech a deterministic endpoint.

The feature is off by default; leaving it off preserves the exact time-only behaviour above. Each command's eligibility is computed once when commands are configured, so the per-utterance cost is one speculative parse of the buffer per VOSK result, not per command.

**Grammars past the analysis limit.** Deciding eligibility means expanding a pattern's optional elements, which is exponential (2^optionals), so a pattern carrying more than 12 of them is refused rather than partially analysed -- and since a partially analysed set could commit the wrong command, the refusal covers the whole command set. Nothing in it then commits early; every complete match is *held* instead, so it waits `prefixHoldSeconds` where that is set and the full `bufferWindow` where it is not. The parser names the offending pattern, its intent, and its optional count in a warning at construction, so the condition surfaces when the grammar is authored rather than mid-session.

### Prefix hold (shortening the ambiguous wait)

The second bullet above -- a complete command that more speech could still extend -- has to wait, but it does not have to wait the *whole* window. It is only waiting on a continuation, and a speaker who is continuing starts almost immediately; the rest of `bufferWindow` is dead air. `prefixHoldSeconds` gives that state its own, shorter timer:

Set the three fields together in the Inspector (all serialized, none settable from code):

```
Buffer Window                    2.0    // Quest 3
Eager Flush On Complete Match    [x]
Prefix Hold Seconds              0.6    // held matches wait 0.6s, not 2.0s
```

With `["fire"]` and `["fire", "at", "{target}"]` registered, "fire" alone now fires ~0.6s after the speaker stops instead of ~2.0s, while "fire at hotel one" still parses as the longer command -- the continuation lands well inside 0.6s.

- Applies **only** to a buffer that already parses as one complete, confident command spanning the whole buffer (bar a leading `[unk]` run). Partial speech mid-split-command and speech that matches nothing keep the full `bufferWindow` — and so does a buffer whose winner was [barred](scoring.md#the-leading-required-miss-bar) for missing its first required element, which the eager gate refuses before it can arm the shortened hold. The barred candidate never fires on either path; what happens at the end of the window depends on the rest of the utterance, which may still yield a command from a later round.
- A grammar too complex for the eligibility precompute to analyse (above) never commits early, but its complete matches are held like any other, so the hold applies to them too.
- **Never lengthens** the wait: a value above `bufferWindow` is ignored.
- Re-evaluated on every VOSK result, so a continuation that does arrive puts the buffer back on the full window for the rest of the utterance.
- Requires `eagerFlushOnCompleteMatch`. Default `0` keeps the full window, i.e. the pre-`prefixHoldSeconds` behaviour.

Tune it against the pause you expect *inside* a command, not between commands: too short and the extended form becomes unspeakable, too long and you are back to paying the full window.

> With `prefixHoldSeconds` left at `0`, a command that is *also* a prefix of a longer one is the one case eager flush can't accelerate -- see [Known Limitations](../KNOWN_LIMITATIONS.md). Push-to-talk (`VoxrPushToTalkController.ReleaseTalk` -> `FlushPendingBuffer()`) gives those a deterministic, zero-latency endpoint.

---

## Sequential Extraction

Multiple commands in a single utterance are extracted left-to-right:

```
"cease fire launch missiles target hotel one"
  -> cease_fire + launch_weapon(weapon=missiles, target=hotel one)
```

Both `OnCommandRecognised` (fired once per command) and `OnCommandsRecognised` (fired once with the full batch array) events fire.

`OnCommandsRecognised` is not exclusive to multi-command utterances: a command resolved through the pending path (confirmed, disambiguated, or slot-filled) also arrives in it, as a one-element batch, after `OnCommandConfirmed` and `OnCommandRecognised`. A handler subscribed to both events must expect every command to appear in both.

---

## Debounce

Per-intent debounce suppresses duplicate firings within `commandCooldown` seconds (an Inspector field, default `0.3`). This applies both across separate VOSK results and within a single parse batch from sequential extraction.

If the user says the same command twice quickly (or VOSK produces overlapping results), the second firing is suppressed.

---

## Authoring hazards

The parser scans the grammar at construction for shapes that silently misbehave -- two that go wrong when the recogniser drops a word, and one that is wrong in the registration list itself -- and warns about each in the Editor. A third drop-a-word shape below — a bare pattern whose tail can be read as another command — is **not** machine-detected: nothing warns about it at construction. All are worth designing away rather than discovering in the field.

### Never leave a required function word between a bare pattern and its slot

If a command has a bare pattern *and* a longer one that extends it with a required literal followed by a slot, mark that literal optional:

```csharp
// Warned about -- a dropped "by" leaves the slot-filled form barely above the gate
new VoxrCommandDefinition("decelerate", new[] {
    new[] { "decelerate" },
    new[] { "decelerate", "by", "{burn_level}" },
})

// Safe -- the same two phrasings, with the droppable word optional
new VoxrCommandDefinition("decelerate", new[] {
    new[] { "decelerate" },
    new[] { "decelerate", "?by", "{burn_level}" },
})
```

Short unstressed function words (`by`, `at`, `to`, `mark`) are the tokens VOSK drops most, and speakers elide them too. When one goes missing, the slot-filled pattern loses that element's credit while still counting it in its denominator, so it falls to `2/3` = 0.67 while the bare pattern still matches everything it claims.

**Until #65 that decided it.** The bare pattern scored a flat 1.0, won selection, and the slot value the speaker *did* say was discarded with nothing to signal it -- "decelerate hard burn" executed a default-level decelerate. No threshold tuning reached it, since nothing normalised to 1.0 can be out-scored. [Coverage](#coverage) closes the common case: the bare pattern is now charged for the "hard burn" it leaves unexplained, scores `1/(1+2)` = 0.33, and loses to the 0.67. The command fires **with** its argument.

**The warning stands, and the swap is still worth making** -- what changed is the cost of ignoring it, not the advice. Three reasons: `0.67` clears the default `minScore` by only `0.07`, so anything else going wrong in the same utterance puts it back under the gate; setting `coverageWeight` to `0` brings the old selection behaviour back; and coverage does **not** reach the case where the stranded value's own first word begins some other pattern, because the orphan run terminates there and the bare form is charged nothing -- register `["hard", "stop"]` here and the bug returns in full at the default weight. With the literal optional, an omitted optional drops out of both sides of the ratio, so the slot-filled pattern scores 1.0 whether or not the word was spoken and wins outright -- in the residual case too. Both phrasings then extract the slot, and a bare "decelerate" still matches the bare pattern.

**The swap is not free.** Two costs, both worth knowing before you apply it wholesale:

- **It lowers the score of imperfect matches.** A matched *required* literal adds 1.0 to both sides of the ratio; a matched *optional* literal adds only 0.5 to both. Those are equivalent only when everything else in the pattern matches. As soon as something else misses, `(r - 0.5) / (d - 0.5)` is strictly below `r / d` -- so a partial match that used to clear `minScore` can fall under it. Usually an improvement (a half-heard command stops firing with slots missing), but it is a behaviour change, not a no-op.
- **It stops anchoring what follows it.** A required literal is a word that *must be spoken* before the next element can consume anything. Make it optional and the following slot can claim adjacent tokens the literal never introduced. With `orient heading {heading} ?mark {?elevation}`, a spurious digit after a full three-word heading -- "orient heading two seven zero **four**" -- is now absorbed as `elevation = "four"` and wins on span, where the required form scored `4/5` = 0.8, lost, and dropped the stray digit. Be wary when the slot after the literal is a `NumberSequence` or otherwise shares vocabulary with the slot before it.

The parser logs a validation warning at construction naming the literal and the slot at risk. This holds whether the trailing slot is required or optional (`{?elevation}` after a required `mark` strands the elevation exactly the same way). The warning is **Editor-only**: it is authoring guidance, and since coverage closed the common case it fires on many grammars that now behave correctly, so it is kept out of player builds and device logs where it would only be suppressed wholesale.

**The check follows what the parser actually compares**, so it covers the hazard in every form it takes:

- **Across commands, not just within one.** Selection runs over every pattern of every command through a single comparison, so declaring the two phrasings as separate intents (`decelerate` and `decelerate_by`) reproduces the hazard exactly. It is warned about.
- **Over a run of required literals, not just one.** Dropping any single word in `decelerate by the {burn_level}` strands the value just as dropping `by` alone does.
- **Over optional forms.** `fire {?quantity} {weapon}` is not literally a prefix of `fire {weapon} at {target}`, but it is once its own optional is omitted -- which is exactly the form the parser matches when no quantity is spoken. Patterns are expanded before comparison, as the eager-flush prefix analysis already does.

The one limit: a pattern carrying more than six optional elements is compared unexpanded, since this scan runs on every parser rebuild an Editor session makes and expansion is exponential. For *this* scan that costs recall on that pattern only. The same bound applies to the scan below, where it costs more, because those forms now decide whether the recogniser warns you and whether it can ask the speaker -- see `KNOWN_LIMITATIONS.md`.

### Do not separate two commands by a single word

If two *different* intents differ at exactly one required word, the parser cannot tell them apart when the recogniser drops it:

```csharp
// Warned about -- one dropped word and registration order picks the intent
new VoxrCommandDefinition("mode_weapons",    new[] { new[] { "switch", "to", "weapons" } })
new VoxrCommandDefinition("mode_navigation", new[] { new[] { "switch", "to", "navigation" } })

// Safe -- the two phrasings differ in TWO places, so losing one word still leaves
// the other to decide
new VoxrCommandDefinition("mode_weapons",    new[] { new[] { "arm", "weapons" } })
new VoxrCommandDefinition("mode_navigation", new[] { new[] { "show", "navigation" } })
```

Say "switch to navigation", lose the last word, and the surviving `switch to` fits **both** patterns exactly equally: same start, same `(1 + 1 + 0) / 3` = 0.67, same consumed span, same literal count. Selection exhausts every key it has and falls through to its last — the order the patterns were registered in — so `mode_weapons` fires because it happens to be declared first. It fires consistently, not randomly, which is what makes it easy to miss in testing and easy to hit in the field.

This is not a scoring bug and no threshold reaches it. The word that would have decided is exactly the word that went missing; the evidence is not weak but **absent**. So there are two things to do about it, and this section covers both: notice the shape before you ship it, and — where you cannot design it away — [ask the speaker](#ambiguous-commands-ask-instead-of-guessing) rather than guess.

**The differing word can sit anywhere.** A word at the *end* is caught by the eager-flush gate's tail rule, which refuses to commit a pattern whose trailing required element never matched. A word in the *middle* clears that rule, because the elements after it still match and the tail check resets:

```
set {ship} mode  on      "set alpha on"  ->  3/4 = 0.75, spans the buffer,
set {ship} level on                          and fits both intents exactly equally
```

**The eager gate refuses to commit on either shape.** When two patterns of *different* intents tie on a buffer and differ only at one required word, firing early would commit a coin flip before the utterance is even over — so the gate declines and lets the buffer run its full window. The same command still fires; it fires at the end of the window rather than immediately.

That does not *resolve* the ambiguity, and it is worth being clear why the wait helps at all — because the obvious reason is the wrong one. Deferring does **not** give the missing word a chance to arrive: speech only ever appends to the buffer, and for a medial drop the position that word would have occupied is already behind the match. It can never land.

What deferring buys is exactly one thing: the decision happens once, at the flush, on a final transcript — **which is where the recogniser can ask you instead of guessing.** Turn `disambiguateSiblingTies` on and it does. Leave it off and the flush picks the first-registered pattern, exactly as it always has.

**What the warning reports, and what it does not.** It fires in the Editor at construction, naming the intents, the patterns as you wrote them, the element they differ at, and the competing values. It is deliberately narrow in three ways:

- **Same-intent patterns are not reported.** Two phrasings of one command dispatch the same intent whichever wins, so the tie is between things you made equivalent on purpose — `set` / `hold` / `keep` / `maintain distance {range}` is not a hazard.
- **Ties that cannot clear `minScore` are not reported.** Losing one required element from a pattern worth `D` leaves `(D − 1) / D`, so a two-element pattern falls to 0.5 — below the default `0.6`, where *both* siblings are rejected and nothing fires. That is a different problem from the wrong command firing, and saying "the wrong intent can fire" about it would be untrue. The threshold is the one you configured, so at `0.4` that same pair *is* live and *is* reported.
- **Ties whose discriminating word is every pattern's *first required element* are not reported.** Dropping that word triggers [the leading-required-miss bar](scoring.md#the-leading-required-miss-bar): whichever candidate wins the round missed its own first required element, so it is barred, the round yields nothing, and no command fires — nor does a disambiguation question open for that set. Saying "the wrong intent can fire" there would be false, as in the case above — though here the reason is positional rather than arithmetic. It takes **every** member of the set: where one pattern is anchored on the discriminator and another is not, the two still tie exactly, registration order can hand the round to the *unbarred* one, and the warning fires as before.

**The second exclusion tracks your threshold; the third reads none at all.** The recogniser hands the parser its configured `minScore` when it builds it, so the second is judged against the value that will actually gate rather than against a copy of the default (#140): lower `minScore` and the short pairs it makes live are reported, raise it and pairs that can no longer fire wrongly go quiet. The third is positional, so it holds at every `minScore` — where the discriminator leads **every** member of the set, no threshold makes the tie live, because whichever candidate wins the round is barred either way. That reach is exact: in a mixed set, where the discriminator leads only some of the members, the tie is live and a lowered `minScore` does reach it. The threshold is read when the parser is built, so an Inspector edit to `minScore` reaches these warnings at the next `RebuildParser` / `Configure` / `SetActiveSets` / `NotifySlotChanged` rather than on the next utterance — unlike the runtime gate of the same name, which is read fresh on every parse.

**Remedies**, in the order worth trying. Note that 2 and 3 are a **fork, not a ladder**: making the discriminator lead *every* pattern in the set means nothing fires on the tie, so there is never a pending for 3 to ask from or for 4 to confirm. Choose between silence and a question; 1 avoids the choice.

1. **Make the two commands differ in more than one element.** The only fix that removes the tie rather than managing it: with two differing words, losing one still leaves the other to decide. Prefer it whenever the phrasing is yours to choose.
2. **Make the differing word *every* pattern's *first required element*.** It has to be every one of them, not just one of the pair: where the discriminator leads them all, dropping it triggers [the leading-required-miss bar](scoring.md#the-leading-required-miss-bar): whichever of the two wins the tie missed its own first required element, so it is barred and the round yields nothing — **nothing fires instead of the wrong thing**, at any pattern length. `weapons mode` and `navigation mode` still tie at `(0 + 1) / 2`, and so do `weapons mode active` and `navigation mode active` at `0.67`; the difference is that neither pair can now fire on the tie. This converts a wrong command into silence, which is a real improvement but not a free one: the speaker must say the command again, it does nothing for an utterance where the discriminator *was* heard, and it forecloses remedies 3 and 4 for that pair. It also **silences the warning above** for that pair, which is correct — nothing can fire, so there is nothing left to report — but means a warning that disappears after you apply this is the remedy landing, not the pair going away.
3. **[Turn on `disambiguateSiblingTies`](#ambiguous-commands-ask-instead-of-guessing) and let the speaker settle it.** The only remedy that keeps both phrasings *and* gets the right command. Needs somewhere to put the question — see below. Note it cannot rescue the case in 2: whichever candidate wins that round is barred, so the round yields nothing and there is no pending to ask from.
4. **Give the more destructive of the pair `requiresConfirmation`**, so a coin flip costs a confirmation prompt rather than an action. Worth doing *alongside* 3, not instead of it: the two combine into "which did you mean?" then "are you sure?".
5. **Where both phrasings must exist verbatim and you cannot prompt, register the safer one first** — the tie-break is deterministic, so first-registered is what fires. This is choosing which way to lose, not a fix.

Remedy 2 is a reversal of earlier advice. Before the bar, moving the difference earlier genuinely did not help — the pair tied identically wherever the discriminating word sat, and a short pair fell under `minScore` only by accident of length. That accident is now a rule, and it holds at every length.

One further warning fires if a discriminating value is also cancel vocabulary. Follow-up handling checks cancel before anything else, so if that ambiguity is routed back to the speaker, answering with that word would cancel rather than choose it. It is judged against *your* `cancelVocabulary` if you set one, and against the defaults (`cancel`, `abort`, `negative`, …) if you did not — so overriding the vocabulary to dodge a collision actually silences the warning, and a collision you introduce *with* an override is reported. It is also only raised for values that could really be offered as an answer, which rules out three shapes: a value whose only same-set partners share its intent is never asked about; neither is a set whose discriminating word leads *every* pattern in it — remedy 2 above bars whichever member wins that round, so no question is ever posed for it; and neither is a set whose tie cannot clear your configured `minScore`, where both members are rejected on score, nothing fires and there is again no question for the answer to be swallowed (#140). The last two are the same conditions the warning above is narrowed by, so applying remedy 2 — or running a threshold the pair cannot clear — silences this warning along with that one.

### Do not leave a bare pattern's tail readable as another command

Sequential extraction offers whatever a winning command leaves behind to the next round, and a leftover tail can match the *tail* of some other intent's pattern — one whose leading word was never spoken. Say **"time to target track one two four four"** at a grammar holding `query_time_to_target : ["time","to","target"]` and `intercept_target : ["intercept","track","{track}"]`, and round 1 fires the query while round 2 finds `track one two four four` matching `intercept_target` minus its verb.

[The leading-required-miss bar](scoring.md#the-leading-required-miss-bar) stops that from firing, and it needs no configuration. But silence is not the same as being understood, and the bar cannot tell "the speaker never said it" from "the decoder dropped it" — so where the tail is a shape your speakers actually produce, fix it in the grammar. Three approaches, in the order worth trying:

1. **Let a legitimate pattern claim the tail.** Give the intent that *owns* those words a phrasing that reaches them: adding `time to target track {track}` as a second pattern of `query_time_to_target` makes the whole utterance one command scoring `1.00`, degrading to `0.80` when the decoder drops `track`. This is the best outcome, because the speaker gets the command they asked for rather than silence.
2. **Register a benign intent for the standalone fragment.** An intent on `["track", "{track}"]` covers the case where the tail arrives alone — after a pause longer than `bufferWindow`, say. It displaces the phantom on **score**, not on registration order, so it does not depend on declaration sequence.
3. **Put `requiresConfirmation` on destructive intents.** This covers the whole class rather than one shape of it, because it diverts on *consequence* rather than on match shape. Worth doing regardless of the other two.

All three work on any version and are worth applying whether or not you rely on the bar.

### Register each intent exactly once

An intent is the identity of a command, and the package treats it as one: **two `VoxrCommandDefinition`s under the same `Intent` are an authoring mistake**, whether they arrive in one `Configure(slots, commands)` call or in two command sets made active together. Both definitions' patterns stay live in the parse — either can win an utterance — but only one of them is reachable *back from the intent*, and the two lookups disagree about which.

```csharp
// Warned about -- one intent, two definitions: both patterns still match, but only
// one definition is reachable back from the intent
new VoxrCommandDefinition("fire_at", new[] { new[] { "fire", "at", "{target}", "now" } }),
new VoxrCommandDefinition("fire_at", new[] { new[] { "fire", "at", "{target}" } }),

// Safe -- the same two phrasings as two patterns of one definition
new VoxrCommandDefinition("fire_at", new[] {
    new[] { "fire", "at", "{target}", "now" },
    new[] { "fire", "at", "{target}" },
})
```

Note what is *not* wrong: selection walks the command list and never consults the intent lookup, so the second definition's patterns are matched and scored exactly like any other — "the second one never fires" is not what goes wrong here. What breaks is everything downstream of the parse, because the two places which resolve an intent back to a definition break the tie in *opposite* directions. The command-set lookup is a dictionary keyed on intent, so the **last** registration wins there; the follow-up re-score scans the command list and stops on the **first**. A `VoxrCommand` carries its `Intent` and `MatchedPatternIndex` but not the command that produced it, so every consumer re-derives the definition from the intent string and they can disagree -- `MatchedPatternIndex` applied to a pattern of a different length, a different unfilled-slot set for a follow-up to chase, a different `allowPartialMatch`, and a different `requiresConfirmation`. That last one is the one to state plainly: with two definitions under one intent, one of them `requiresConfirmation`, whether a destructive command asks before firing depends on registration order rather than on the command that matched.

**Two registrations that are identical are a different mistake, and get a different warning.** If the two definitions the lookups reach are ones no consumer could tell apart -- the same patterns in the same order, the same `allowPartialMatch`, the same `requiresConfirmation` -- then nothing disagrees, because both resolutions land on the same thing. What remains is that only one registration is reachable from the intent and each extra copy adds a parse candidate that ties the original exactly, so registration order breaks a tie between a command and itself. The two usual causes are a set named twice in one `SetActiveSets` call (or in `initialActiveSetNames`), and one command asset placed in two sets that are active together -- neither is caught anywhere else, since `Activate` concatenates the active sets without de-duplicating. The remedy is to remove the duplicate registration; merging patterns does not apply, because there is only one distinct definition.

Per-intent debounce is keyed on the intent too, so duplicate definitions share one cooldown.

The warning names the intent, how many definitions carry it, and the first pattern of each of the two the resolutions reach -- or, for identical registrations, of the one definition involved. Like the two scans above it is **Editor-only**. Intents are compared ordinally, matching both lookups -- `fire_at` and `Fire_At` are distinct commands, not duplicates.

---

## Ambiguous Commands: Ask Instead of Guessing

Everything above stops the recogniser committing to a coin flip early. It does not stop it coin-flipping at the end — the flush still picks the first-registered pattern. **`disambiguateSiblingTies` is what replaces that guess with a question — with one exception: where the discriminating word is **every** competing pattern's **first required element** — two members or ten — dropping it [bars](scoring.md#the-leading-required-miss-bar) whichever candidate wins the round, so nothing fires and no pending opens for that set. That case is remedy 2 above, and this flag does not reach it.** Wherever that exception does not apply — the discriminating word is not **every** competing pattern's first required element — the round can resolve normally — it does when the member the bar does **not** touch is the one registration order hands it to — and a member whose *own* first required element went unheard can then be offered among the choices and fire if it is picked — see [Known Limitations](../KNOWN_LIMITATIONS.md).

**This is independent of `eagerFlushOnCompleteMatch`.** The refusal described above is an eager-gate rule and only applies when you have turned eager flush on; the flag below acts on the *flush* path, which every utterance takes. Eager flush is off by default — that does not exempt you from this hazard, and does not stop the flag fixing it.

```csharp
// Inspector: Follow-Up / Pending Commands > Disambiguate Sibling Ties
set_mode  : ["set", "{ship}", "mode",  "on"]
set_level : ["set", "{ship}", "level", "on"]
```

The speaker says "set alpha mode on". VOSK drops `mode`. With the flag off, `set_mode` fires because it was declared first — and would have fired even if they had said `level`. With the flag on:

1. Nothing fires. `OnCommandPending` raises, carrying the candidate that *would* have fired.
2. `PendingAmbiguity` is non-null, and carries the competing commands with the one word that tells each apart: `mode` or `level`.
3. You prompt however suits your game — this package ships no speech synthesis and no UI.
4. The speaker says **`level`**, one word. `set_level` fires with its slots intact (`ship = alpha`), through `OnCommandConfirmed`, then `OnCommandRecognised`, then `OnCommandsRecognised` (a one-element batch).

**If the chosen command sets `requiresConfirmation`, step 4 asks again instead of firing** — "which?" first, then "are you sure?", which is the only coherent order, since you cannot confirm an intent you have not identified. `OnCommandPending` raises a second time, `PendingAmbiguity` is now null (this question is a confirmation), and the confirm vocabulary resolves it. Worth planning for: remedy 4 above tells you to mark the more destructive sibling `requiresConfirmation`, so taking both remedies together is exactly what produces this two-stage exchange.

The answer needs no grammar work: discriminating values are pattern literals, so the decoder already knows them.

**Off by default, and that is not timidity.** The flag makes an ambiguous utterance fire *nothing* until it is answered. With no `OnCommandPending` subscriber the speaker is never prompted, the pending times out, and the command is lost — worse than the coin flip it replaced. Turn it on when you have somewhere to put the question.

### What answering looks like

- **A discriminating value** picks that choice. Matched as a *whole* utterance, so "set alpha mode on" is a re-utterance that preempts the question, not an answer to it — both work, they just take different routes.
- **Cancel** works, and keeps its precedence. A value that is also a cancel word cancels rather than choosing; the construction warning above tells you when your grammar has one.
- **"Yes" does nothing.** It is not an answer to "which?" — but it is not an abandonment either, so the question stays open for the real answer.
- **Silence** cancels. Even with `pendingTimeoutBehavior = FireAsIs`: there the *intent* is unknown rather than the arguments, and firing the first-registered after a pause is the same coin flip, merely later.
- **Saying it all again** works, and preempts the question. A second *ambiguous* utterance re-asks it. Either way the superseded question is cancelled first, so `OnCommandCancelled` precedes the new command's events -- or the fresh `OnCommandPending`.

### Three or more, and what does not fit

A sibling set is n-ary: `set auto pilot on` / `off` / `standby` offers three choices, and each one-word answer fires its own intent. The runtime offers the winner plus up to four alternatives.

Past that — or where the winner is ambiguous in two different ways at once, or where a pattern carries too many optional elements to analyse fully — some intent that also matched will not be on the list. `PendingAmbiguity.IsTruncated` tells you, so you can word "…or say the whole command again", which is the only way to reach what is missing.

### Push-to-talk: one setting to check

Under push-to-talk, **Cancel Pending On Release** discards the question the instant it is raised — release flushes, and the flush is what creates it. Leave that setting off and the question survives for the speaker to answer on their next press. See [Cancel Pending On Release](push-to-talk.md#cancel-pending-on-release) for the ordering and the timer arithmetic.

---

## Pending Commands

Sometimes a command partially matches (some required slots are unfilled), or needs explicit confirmation before a high-consequence action fires, or cannot be told apart from another command at all. The pending command system handles all three by holding the command in a "pending" state and listening for follow-up speech.

The third kind is [ambiguity](#ambiguous-commands-ask-instead-of-guessing), and it only ever arises with `disambiguateSiblingTies` enabled. Everything in this section applies to it — preemption, cancellation, `CancelPendingCommand()` — **with one exception, called out under Timeout Behaviour below.**

### Partial Match with Follow-Up Slot-Fill

Set `allowPartialMatch: true` on a command definition to let it enter pending state when matched with unfilled required slots, instead of being refused. The diversion is decided by completeness alone, independently of `minScore` -- a command scoring `0.8` with a missing argument routes to pending exactly as a sub-threshold one does. One thing is consulted before completeness: [the leading-required-miss bar](scoring.md#the-leading-required-miss-bar). A winner that missed its own first required element is refused outright, so it never reaches this diversion at any score.

```csharp
var launchCmd = new VoxrCommandDefinition("launch_weapon",
    new[] { new[] { "launch", "{weapon}", "target", "{target}" } },
    allowPartialMatch: true);
```

If the user says "launch missiles" without specifying a target, the command enters pending state and `OnCommandPending` fires. The system then listens for follow-up speech. If the user says "hotel one" within the `pendingTimeout` window, the target slot is filled and the command fires via `OnCommandConfirmed`, then `OnCommandRecognised`, then `OnCommandsRecognised` (a one-element batch).

**Several missing slots take several utterances if they have to.** Follow-up speech fills the unfilled slots in pattern order and stops at the first one it cannot find, so an utterance that answers only part of what is missing leaves the command *still pending* — with what it just filled kept, and `OnCommandPending` fired again carrying the updated command. It fires only once no required slot is left. A prompt driven off `OnCommandPending` therefore sees each fill as it lands and can name what is still outstanding. Nothing is lost by answering one slot at a time, and each fill restarts the `pendingTimeout` window: `pendingTimeout` bounds how long the command waits for *you*, so answering buys another window rather than eating into a fixed one. What ends a stalled exchange is silence — the first window nobody answers. The same restart applies when the last slot lands on a command that also sets `requiresConfirmation`: the confirmation stage begins its own window.

```csharp
commandRecogniser.OnCommandPending += cmd =>
    Debug.Log($"Waiting for: {cmd.Intent}");

commandRecogniser.OnCommandConfirmed += cmd =>
    Debug.Log($"Confirmed: {cmd.Intent} target={cmd.GetSlot("target")}");

commandRecogniser.OnCommandCancelled += cmd =>
    Debug.Log($"Cancelled: {cmd.Intent}");
```

### Explicit Confirmation

Set `requiresConfirmation: true` to require the user to say a confirmation phrase before the command fires, even when fully matched.

```csharp
var selfDestruct = new VoxrCommandDefinition("self_destruct",
    new[] { new[] { "self", "destruct" } },
    requiresConfirmation: true);
```

After saying "self destruct", the command enters pending state. The user must say "confirm" (or another confirm phrase) to fire it, or "cancel" to discard it.

Default confirm vocabulary: "confirm", "affirmative", "yes", "go ahead", "do it". Default cancel vocabulary: "cancel", "abort", "negative", "belay that", "never mind". Override these with the `confirmVocabulary` and `cancelVocabulary` Inspector arrays on `VoxrCommandRecogniser`. The *matcher* reads the live arrays on every utterance, so a changed vocabulary is honoured immediately. What is frozen when the parser is built: the construction-time cancel-collision warning's copy of the cancel vocabulary, the `minScore` both that warning and the sibling warning beside it are judged against, and `disambiguateSiblingTies` (in a player build -- the Editor records ties regardless). And the decoder *grammar* learns new words only when it is rebuilt -- `Configure`, `SetActiveSets`, or `RebuildGrammar` -- so a novel overridden word is matched as an answer at once but may not be *decodable* until then.

Follow-up speech is checked against a live pending in a fixed order: **cancel first** (under every reason -- a cancel word always cancels), then a disambiguation choice, then a confirm phrase, then slot-fill. Because confirm outranks slot-fill, a confirm phrase also resolves a pending command that is waiting on slots rather than on confirmation — see below.

### Combined Partial + Confirmation

A command with both `allowPartialMatch` and `requiresConfirmation` goes through two pending stages: follow-up speech fills missing slots first; once nothing required is left, the confirmation stage begins with a fresh `pendingTimeout` window. A confirm phrase spoken *during* the slot-fill stage does not skip to stage two -- it fires the command immediately, as it stands, absent slots and all (see [the two ways an incomplete command still fires](#the-two-ways-an-incomplete-command-still-fires)).

### Timeout Behaviour

Configure `pendingTimeout` (default 5s) and `pendingTimeoutBehavior` on `VoxrCommandRecogniser`:

- **Cancel** (default) -- the pending command is discarded and `OnCommandCancelled` fires.
- **FireAsIs** -- the pending command fires with whatever slots were filled, even if some are still missing.

**`FireAsIs` does not apply to a pending ambiguity, which always cancels.** The two settings answer different questions: `FireAsIs` means "the command is known, fire it with what I have", and under an ambiguity the *command itself* is what is unknown. Firing the first-registered candidate after a pause would be the same coin flip the question was asked to avoid, arriving later.

### The two ways an incomplete command still fires

A command missing a required argument does not fire on the ordinary path: it is refused outright, or, with `allowPartialMatch`, held pending for slot-fill. (A third case reaches neither branch: a winner that missed its *first required element* is [barred](scoring.md#the-leading-required-miss-bar) before completeness is consulted, so it is not rejected for incompleteness and not held pending either — the round simply yields nothing.) Two opt-ins deliberately override the ordinary path, and both hand your handler a command with arguments absent:

- **Confirming a partial match.** Saying a confirm phrase while a command waits for slot-fill fires it as it stands. The confirm check runs before slot-fill, and it is taken at face value — the user has been shown what is missing (via `OnCommandPending`) and said go anyway.
- **`FireAsIs` on timeout.** The name is the contract: whatever was filled when the window closed is what fires.

**Both have the same precondition: `allowPartialMatch`.** Only a partial-match pending ever holds a command with a required slot still absent — a pending that is merely awaiting confirmation was complete when it entered, because the completeness rule refused it otherwise. So `pendingTimeoutBehavior = FireAsIs` on its own cannot fire an incomplete command; it needs a command that opted into partial matching to have one to fire.

Set `allowPartialMatch` on a command and its handler **must** tolerate every required slot being absent — via a confirm phrase, which needs no second opt-in, or on timeout if `pendingTimeoutBehavior` is `FireAsIs`:

```csharp
commandRecogniser.OnCommandConfirmed += cmd =>
{
    // Do not assume a slot is present just because the pattern requires it.
    if (!cmd.HasSlot("target")) { PromptForTarget(); return; }
    Launch(cmd.GetSlot("target"));
};
```

Test with `HasSlot`, not with the value: `GetSlot` returns `string.Empty` for a slot that was never filled, so a null check will not catch it.

### Preemption

If a new complete command is recognised while a command is pending, the pending command is cancelled and the new command fires normally. This prevents stale pending commands from blocking normal operation.

The order is worth knowing for prompt UIs: the superseded pending is cancelled *first*, so `OnCommandCancelled` fires before the new command's events -- and before the fresh `OnCommandPending` when the new utterance is itself ambiguous or incomplete. An *incomplete* new command whose definition does **not** allow partial matching never preempts: taking a half-finished command away to put nothing in its place would be a loss, so the live pending stays. An incomplete command that *does* set `allowPartialMatch` enters pending itself and displaces the old one -- cancel first, as above. An utterance whose only winner was [barred](scoring.md#the-leading-required-miss-bar) does not preempt either, for the same reason and by the same general rule: it produces no accepted command, so there is nothing to put in the pending's place. It also does not cancel a live pending or interrupt follow-up slot-fill.

### Grammar Integration

Confirm and cancel vocabulary is merged into the VOSK grammar JSON **word by word**, so it is recognised reliably in grammar mode. A multi-word phrase like "belay that" contributes `belay` and `that` as individual entries -- unlike pattern literals, follow-up phrases get no multi-word phrase entry to bias their word order (see [What the grammar contains](#what-the-grammar-contains)). An overridden vocabulary is merged the same way, so a novel phrase of your own is decodable too. The two layers treat an override differently: the *matcher* uses your arrays **instead of** the defaults, while the *grammar* keeps the default words **alongside** yours -- so a default word stays audible to the decoder even though it is no longer accepted as an answer.

### Programmatic Control

Call `CancelPendingCommand()` to cancel the pending command from code (e.g. on a scene transition or mode switch). Check `HasPendingCommand` and `PendingCommand` to inspect the current pending state, and [`PendingAmbiguity`](api/data-types.md#voxrpendingambiguity) to tell an ambiguity from a confirmation — `OnCommandPending` carries no reason of its own, so that property is how you know which question you were asked.

Two lifecycle interactions worth knowing:

- **Reconfiguration cancels a live pending.** `Configure` (either overload), `SetActiveSets`, and disabling the component all discard the pending command and raise `OnCommandCancelled` -- so a mode switch mid-confirmation produces a cancellation your handler should expect.
- **`RebuildGrammar()` defers while a command is pending.** Rebuilding the decoder grammar would destroy the utterance the pending is waiting on, so the rebuild is queued silently and applied when the pending resolves. A `RebuildGrammar()` call that appears to do nothing is usually this.

---

## Unrecognised Speech

When speech passes through the pipeline but no command is produced, `OnUnrecognisedSpeech` fires with the raw transcript. This happens in five situations:

1. **No pattern match** -- the parser could not match any command pattern against the transcript.
2. **Every match fell under `minScore`** -- patterns matched, but no candidate scored high enough to fire.
3. **A match was missing a required argument** -- the command may have scored well, but a required slot went unfilled and the command does not set `allowPartialMatch`. The completeness rule is independent of score, so this is the one case where "unrecognised" does not mean "scored badly".
4. **The winning candidate's first required element was never heard** -- the round's winner was [barred](scoring.md#the-leading-required-miss-bar). It may have scored well above `minScore`; the refusal is positional, not arithmetic, and the round leaves no session-log attempt behind.
5. **A follow-up fill was refused for re-scoring at or below zero** -- follow-up speech completed a pending command, but the re-score landed at or below zero, so the same floor the flush paths apply refused it (see [the session-log table](scoring.md#reading-a-session-log)). The pending is left standing. Reaching this at all means two definitions share one intent.

It does **not** fire in three others: a candidate rejected by `minConfidence`, one suppressed by `commandCooldown` debounce, or one diverted to a pending of **any** kind -- partial-match, confirmation, or disambiguation alike. The first two are silent on the reasoning that the user did say a valid command, just not confidently or not soon enough after the last one. A pending is silent because telling you the speech was not understood, in the same frame you were asked to prompt the speaker about it, is a contradiction: the prompt is the recogniser saying it understood enough to ask ([#133](https://github.com/jinwoo1601/VoXR-Speech-Recognition/issues/133)). See [the gates](scoring.md#what-onunrecognisedspeech-actually-means) for the full table.

The `string` parameter is the full buffered transcript (after utterance merging), exactly as VOSK transcribed it.

### When it does not fire

- If `Configure` has not been called -- or the set-based `Configure(slots, sets)` overload was called without a following `SetActiveSets()` -- speech is silently dropped and no events fire.
- If speech arrives during a grammar rebuild (stop/set/start cycle), it is discarded before reaching the parser.

### Common uses

**Diagnostics and tuning** -- Log unrecognised speech to identify patterns that need adding, thresholds that need adjusting, or VOSK transcription issues:

```csharp
commandRecogniser.OnUnrecognisedSpeech += text =>
{
    Debug.Log($"[VoXR] Unrecognised: \"{text}\"");
};
```

**Player feedback** -- Show a subtle UI hint so the player knows they were heard but their words didn't match a command:

```csharp
commandRecogniser.OnUnrecognisedSpeech += text =>
{
    hudController.ShowTransientMessage("Command not recognised");
};
```

**Dynamic slot interaction** -- When a value provider excludes a target, speech that names the excluded target fires `OnUnrecognisedSpeech` instead of `OnCommandRecognised`. You can use this to explain *why* the command failed:

```csharp
commandRecogniser.OnUnrecognisedSpeech += text =>
{
    if (text.Contains("target"))
        hudController.ShowTransientMessage("Target not available");
};
```

That recipe assumes the command leaves `allowPartialMatch` off, which is the default. With the flag **on**, an excluded value is an unfilled required slot, so the utterance opens a slot-fill pending and is *not* reported unrecognised -- put the same message on `OnCommandPending` instead, testing the pending command with `HasSlot("target")` to see which argument went missing.

### Relationship to other events

`OnUnrecognisedSpeech` and `OnCommandRecognised`/`OnCommandsRecognised` are mutually exclusive per utterance: a transcript that produces at least one accepted command never also fires `OnUnrecognisedSpeech`.

The converse does not hold. A transcript that produces *no* accepted command is silent when a candidate was filtered by `minConfidence`, suppressed by debounce, or diverted to a pending of any kind, so "no command fired" and "`OnUnrecognisedSpeech` fired" are not the same condition.

---

## Grammar Mode vs Free Speech

By default, `VoxrCommandRecogniser` constrains VOSK's decoder to only the words that appear in registered commands and slots. This is **grammar mode**, and it dramatically improves recognition accuracy for command-driven UX.

Enabling **Free Speech Mode** in the Inspector disables the grammar constraint, allowing VOSK to recognise any word in its vocabulary. Command matching becomes best-effort.

### What the grammar contains

The grammar is not just a bag of words. Each **contiguous run of required literals** in a pattern, and each **multi-word slot value or alias**, is emitted as a single multi-word entry, alongside the individual words:

```csharp
new[] { "close", "distance", "{range}", "target", "{target}" }
// phrase entries: "close distance" (a one-word run like "target" yields none),
// plus the single words "close", "distance", "target",
// plus each {range}/{target} surface form ("safe range", "hotel one", ...)
```

VOSK charges one language-model transition per entry, so a three-word entry costs one transition where the same three words cost three. That makes the order you declared the cheaper path through the decoder's search, which is what stops in-grammar words substituting freely for one another -- `switch to navigation` no longer decodes as `switch two navigation`.

Two consequences worth knowing when you author patterns:

- **A slot or an optional literal ends a run.** Neither is guaranteed to be spoken, so the words either side of it are not reliably adjacent and are never welded together. Literals stranded alone between two slots get no phrase protection -- that is one more reason to prefer runs of required literals over lone function words (see [Known Limitations](../KNOWN_LIMITATIONS.md)).
- **The single words are still there.** The phrase entries bias the decoder; they do not forbid anything. An utterance the VAD splits mid-phrase still decodes as fragments, and the parser's sliding start reassembles what it can.

This is automatic -- there is no setting, and nothing about your pattern or slot declarations changes.

### When to use each mode

| | Grammar Mode (default) | Free Speech Mode |
|---|---|---|
| **Accuracy** | High -- VOSK only considers in-vocabulary words | Significantly lower for commands -- homophones and uncommon words break frequently |
| **Vocabulary** | Limited to words in your commands and slots | Unrestricted |
| **Best for** | Voice commands, menu navigation, game controls | Dictation, note-taking, chat, any feature that needs arbitrary text |
| **NumberSequence** | Reliable -- digit words are constrained | Unreliable -- "two" becomes "to", "orient" becomes "korean" |
| **False matches** | Possible from noise (grammar must pick *something*) | Fewer false matches, but fewer true matches too |

### Recommendation

Use grammar mode (the default) for all command-driven features. Only enable free speech when your feature genuinely needs arbitrary vocabulary, and accept that command matching will be best-effort in that mode. **The mode is an authoring-time choice, not a runtime switch**: `freeSpeechMode` is a serialized Inspector field with no public accessor, and the command layer never lifts an applied grammar itself -- treat the mode as fixed. (The low-level escape hatch exists: `VoxrSpeechRecogniser.SetGrammar` with an empty grammar, while stopped, puts the *decoder* in free dictation -- but the command recogniser re-applies its grammar on its next rebuild, so it is not a supported mode switch.)

---

## See Also

- [Matching and Scoring](scoring.md) -- The score formula, coverage, selection order, gates, and eager-flush verdicts in full
- [Command Sets](command-sets.md) -- Group commands into switchable named sets for mode-specific grammars
- [Number Parser](api/number-parser.md) -- Convert a `NumberSequence` slot's spoken words into an integer
- [Inspector Authoring](inspector-authoring.md) -- Define commands and slots with ScriptableObject assets instead of code
- [Editor Testing](editor-testing.md) -- Test commands with the debug window, session debug log, text injection, and batch runner
- [Known Limitations](../KNOWN_LIMITATIONS.md) -- VOSK model quirks, homophones, and recognition edge cases
