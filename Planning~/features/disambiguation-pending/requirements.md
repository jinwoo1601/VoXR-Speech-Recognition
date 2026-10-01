---
type: requirements
feature: disambiguation-pending
topic: sibling-tie-disambiguation
status: draft
updated: 2026-08-16
sources: [Planning~/design-docs/sibling-tie-disambiguation.md, Planning~/features/sibling-set-detection/requirements.md, Planning~/features/sibling-set-detection/architecture.md, Planning~/features/tie-aware-selection/requirements.md, Planning~/features/tie-aware-selection/architecture.md]
---

# Disambiguation Pending — Requirements

## 1. Feature & scope

Backlog line (design §9, row 3): `feat-disambiguation-pending` — DR-4's third `VoxrPendingReason`, the widened `VoxrPendingCommand`, the choice vocabulary, DR-6's timeout semantics, and DR-7's opt-in flag. *"The flush-side behavioural change lands here, behind the flag."* Depends on item 2.

This is the third of four features under the G1-locked `sibling-tie-disambiguation` design (locked 2026-08-15). **It is the item where the tie stops being deferred and starts being answered.** Item 1 made the hazard visible to the author at construction. Item 2 made the tie visible to the parser and stopped the eager gate guessing early. Neither changed what the flush does: it still fires the first-registered sibling. This feature gives the flush a third option — ask.

It differs from item 2 in kind, not just in size. Item 2's runtime change was timing-only; this one changes **what fires**, touches the pending state machine, and adds the first configuration surface of the design. It is also the first item that adds public API (§4.10), which items 1 and 2 both avoided.

**It deliberately builds none of item 4.** No guide-level narrative in `Documentation~/command-recognition.md`, no rewritten `KNOWN_LIMITATIONS.md` entry spanning all three items. This feature's own post-G2 product docs are written per the workflow at its close-out; item 4 is the connective tissue across 1–3.

**Two deferred issues are pulled in, and one is filed.**

- **Issue #91** (the sibling warning's element number can index a different word than the pattern it quotes) is **in scope** (F14). It was deferred to this item because this item rewrites that message anyway to add the flag clause. It was **widened on 2026-08-16**: item 2's member retention re-opened the *symmetric* case #91's original text records as already fixed by the longest-frame collapse, so the fix now has to cover both shapes. Ruled by the human, 2026-08-16.
- **Issue #93** stays deferred. Unchanged by this feature and not touched by it.
- **Issue #95** was **filed by this feature** (2026-08-16): design §5.3's promise that non-sibling ties stay *"visible to the editor diagnostic"* is the one half of §5.3 this design never builds, and §4.4 explains why it cannot be built here.

## 2. Why — what items 1 and 2 bought, and what they left on the table

Item 2 confirmed the design's load-bearing finding by execution: on a medial discriminator the eager gate committed the wrong sibling early, and it now refuses. The refusal is a real improvement and it is also, on its own, **incomplete** — and the PR #94 review is what made that precise.

Design §5.8 justified the eager refusal by saying *"the missing word may still arrive, so refusing costs only latency."* That is true of the **trailing** shape, which #70 already refused. It is **false of the medial shape DR-5 exists for**: reaching the sibling condition means #70's condition passed, so an element *after* the dropped word already matched, and `HandleResult` only appends. The word can never land in a position the match has gone past.

So what does the eager refusal actually buy? Exactly one thing:

> The decision moves from the eager path to the flush, **where this feature can ask.**

That is the whole payoff, and until this feature ships it is unrealised. Today the sequence on a medial sibling tie is: eager refuses (item 2), the buffer window expires, the flush selects, the keys all tie, registration order decides, and the first-registered sibling fires — the same coin flip, reached later. Item 2 bought a pause and spent it on nothing. **This feature is what makes that latency worth paying.** The human ruled 2026-08-16 that the design stays locked and the correction is recorded in the source comment and the product docs rather than amended into §5.8; this section is where the correction becomes a *requirement's* motivation rather than a footnote.

The user-visible shape is unchanged from item 2's §2:

```
set_mode  : ["set", "{ship}", "mode",  "on"]
set_level : ["set", "{ship}", "level", "on"]
```

Speaker says one; VOSK drops the discriminator; the buffer reads `set alpha on`. With this feature's flag on, the recogniser stops, enters pending, and the integrator prompts. The speaker says `mode` — a word already in the decoder's grammar, because it is a pattern literal — and the right intent fires.

`KNOWN_LIMITATIONS.md:538` currently records this as a limitation with a workaround. After this feature it becomes a limitation with a **supported remedy**, which is the sentence item 4 is named for.

## 3. Observable behavior

**With `disambiguateSiblingTies` off — the default — nothing changes at all.** Byte-identical behaviour to `337e758` on every path: the same intents fire at the same moment with the same slots, scores and events, and a player build pays no new per-parse cost. This is DR-7 and it is the single most important acceptance property of the feature (§4.2).

**With the flag on**, one new thing happens and it happens only on the flush path. When the flush's winner is tied by one or more sibling rivals — a three-way set such as `set auto pilot on` / `off` / `standby` ties three ways and offers three choices (F19):

- the command does **not** fire. `OnCommandRecognised` / `OnCommandsRecognised` do not raise for it.
- the recogniser enters pending with the new reason `AwaitingDisambiguation` and raises `OnCommandPending` carrying the command that *would* have fired.
- the integrator can now read that this pending is an ambiguity and what the choices are, and word a prompt (§4.10). This package ships no TTS; prompting is the integrator's job, as §5.5 says.
- the speaker answers with a **discriminating value** — `mode`, `level` — and that intent fires through the ordinary confirmation route (`OnCommandConfirmed`, then `OnCommandRecognised`, then `OnCommandsRecognised`).
- a **full re-utterance** also works: `set alpha level on` parses normally, is unambiguous, and preempts the pending.
- **cancel** works, with its existing precedence. **Confirm does not** — "yes" is not an answer to "which?".
- on **timeout**, nothing fires, even when `pendingTimeoutBehavior` is `FireAsIs` (DR-6).

**In the Editor at construction**, three message changes: the sibling warning gains the clause naming the flag (which item 1 deliberately omitted because the identifier did not exist); its element number now indexes each quoted pattern correctly (#91); and the cancel-collision report stops reporting collisions on values that can never be an answer (§4.11).

**In the Editor at parse time**, the record item 2 built is unchanged in shape and meaning. It stays sibling-only. Issue #95 carries the widening.

**Public API grows; nothing breaks.** One serialized field, and a read-only way to inspect a pending ambiguity. No existing public type, member, event or serialized field is removed, renamed or re-typed.

## 4. Functional requirements

| # | Requirement | Priority | Acceptance check | Source |
|---|---|---|---|---|
| F1 | **The opt-in flag exists and gates every runtime behaviour change here (DR-7).** `disambiguateSiblingTies`, a `[SerializeField]` **private** `bool` on `VoxrCommandRecogniser` defaulting to `false`, with an `internal` test setter matching the file's existing idiom (`PendingTimeout`, `ConfirmVocabulary`, `CancelVocabulary`). Not a public API addition — every knob on this component has this shape. | Must | The field exists, defaults to `false`, appears in the inspector with a tooltip, and is settable from tests without reflection. Every new runtime branch in this feature is downstream of it. | DR-7; design §5.7 |
| F2 | **With the flag off, behaviour is byte-identical to `337e758`, and costs nothing.** This is G-6 restated for the one feature that could violate it. | Must | Both suites pass with **no edited expected values** except the tests F13–F15 explicitly change (all Editor-message tests). The 699-corpus A/B shows zero difference in fired intents, slots and scores with the flag off. A per-parse cost measurement with the flag off shows no regression against the `337e758` baseline. | design G-6, §5.7; DR-7 |
| F3 | **`VoxrPendingReason` gains `AwaitingDisambiguation`; `VoxrPendingCommand` widens to carry the rival and the choice words (DR-4).** Both types stay `internal`. The rejected alternative stays rejected: a parallel `VoxrAmbiguousPending` would solve timeout, cancel-on-release, re-entry and disable-safety a second time, when `PendingCommandHandler` already solves them once. | Must | One pending type, one handler, one timeout path. The new reason flows through `EnterPending`/`Cancel`/`HandleTimeout` without a parallel state machine. Existing paths ignore the new fields. | DR-4; design §5.4, §2.5 |
| F4 | **On a sibling tie the flush enters disambiguation instead of firing.** With the flag on, a flush winner carrying a tied sibling rival routes to pending with `AwaitingDisambiguation`, raising `OnCommandPending`; `OnCommandRecognised` does not raise for it. | Must | A recogniser-level test injects the medial sibling utterance, flushes, and asserts `OnCommandPending` raised once and `OnCommandRecognised` zero times. With the flag off the same fixture fires exactly as it does on `337e758`. | design §5.3, §9 row 3; G-2 |
| F5 | **The tie record is promoted to runtime, gated so a flag-off player pays nothing; the Editor record keeps item 2's shape and meaning.** Item 2 built the flush record under `#if UNITY_EDITOR` inside `ParseDiagnosticEntry`, so it is not readable by the runtime path that must now act on it. It gains a runtime carrier. It stays **sibling-only** in both configurations. | Must | The runtime path reads the tie without `#if UNITY_EDITOR`. The Editor diagnostic's `TiedSiblingIntent` / `TiedSiblingPatternIndex` keep their current values on every existing test, unedited. A flag-off player build performs no per-parse sibling work the `337e758` player did not. Issue #95 stays open and is referenced in the source. | design §5.3 vs §9 row 2; human ruling 2026-08-16; issue #95 |
| F6 | **Each alternative fires with the slots its own match produced.** An alternative's slot matches are captured at selection, not reconstructed from the winner's. Siblings are element-wise equal but for one required literal, so in practice the slots agree — but "in practice" is not an argument for firing a command with another candidate's arguments. | Must | A sibling pair carrying a slot (`["set","{ship}","mode","on"]` / `["set","{ship}","level","on"]`) disambiguates to the alternative and the fired command carries `ship=alpha`, asserted on the fired `VoxrCommand`, not inferred. | **Derived here** — see §4.6 |
| F19 | **The choice set is n-ary, not binary (design §5.1).** A three-way sibling set offers three choices. The flush loop records **every** tied sibling rival up to a fixed cap, not just the first; item 2's first-rival rule was sized for an Editor diagnostic that needed to name *a* rival, and is undersized for a vocabulary. Two rivals carrying the *same* discriminating value are duplicates of each other (item 1's F8) and only the first is offered. | Must | `set auto pilot on` / `off` / `standby` with the discriminator elided offers **three** choices, and each of the three one-word answers fires its own intent. The Editor diagnostic still reports the *first* rival, so item 2's five diagnostic tests pass unedited. A set exceeding the cap offers the first N and is reported, never silently truncated. | design §5.1; human ruling 2026-08-16 |
| F7 | **The discriminating values are the choice vocabulary (DR-4), matched as whole utterances, and they need no grammar change.** Reuses the existing whole-utterance matcher — `MatchPhraseAgainstTokens` consumes **all** tokens, so this is not a substring search. The values are pattern literals, so they are already in the decoder grammar; unlike confirm/cancel they need no addition to `GetFollowUpGrammarWords`. | Must | Answering with a discriminating value fires that intent. Answering with an utterance that merely *contains* one does not. The generated grammar JSON is byte-identical with the flag on and off — pinned, not assumed. | DR-4; design §5.5 |
| F8 | **A full re-utterance is accepted, and an ambiguous one re-enters disambiguation.** An unambiguous re-parse preempts the pending and fires, via the existing preemption path. A re-utterance that is *itself* a sibling tie re-arms the disambiguation rather than firing or cancelling — it is the same question asked again. | Must | `set alpha level on` while pending on `set alpha on` fires `set_level` and clears the pending. A second ambiguous utterance while pending leaves exactly one pending, with `AwaitingDisambiguation`, and raises `OnCommandPending` again. | DR-4; design §5.5 |
| F9 | **Cancel applies and keeps precedence; confirm does not apply.** Under `AwaitingDisambiguation`, cancel vocabulary cancels. Confirm vocabulary is inert — it must not fire the winner. A discriminating value colliding with the cancel vocabulary is swallowed by cancel; safety wins, and the author was told at construction (F13). | Must | Under `AwaitingDisambiguation`: a cancel word raises `OnCommandCancelled` and clears; a confirm word raises nothing and leaves the pending live; a discriminating value equal to a cancel word cancels. Under `AwaitingConfirmation` and `PartialMatch`, confirm behaves exactly as today. | DR-4; design §5.5 |
| F10 | **DR-6: under `AwaitingDisambiguation`, `FireAsIs` degrades to `Cancel`.** An unanswered ambiguity fires nothing. No new value is added to the public `VoxrPendingTimeoutBehavior`. | Must | With `pendingTimeoutBehavior = FireAsIs` and an `AwaitingDisambiguation` pending, timeout raises `OnCommandCancelled` and no `OnCommandRecognised`. With the same setting and an `AwaitingConfirmation` pending, it still fires as today. The public enum has two values. | DR-6; design §5.6 |
| F11 | **A read-only public way to see that a pending is an ambiguity, and what the choices are.** Without it the opt-in is unusable: `OnCommandPending` is `Action<VoxrCommand>` and carries no reason, so an integrator already subscribed for `requiresConfirmation` would prompt "yes/no" — and "yes" does nothing under F9. Narrow and additive: the pending ambiguity only. Not §6.4's rejected n-best — no rankings, no scores, no ordering guarantees, no general candidate API. | Must | From `OnCommandPending`, an integrator can distinguish a disambiguation from a confirmation, and can read the competing intents with their discriminating values, without reflection or `InternalsVisibleTo`. Nothing existing is removed, renamed or re-typed. | design §2.6 (unresolved constraint), §5.5; human ruling 2026-08-16 |
| F12 | **The parser sees the *effective* cancel vocabulary (item 1 architecture §4.1).** Deferred to this item deliberately, because the collision has no consequence until this item ships and this is where the vocabulary must be plumbed anyway. `VoxrCommandParser` is `internal`, so a constructor parameter is not a public change; `VoxrBatchTestRunner`'s two construction sites must keep compiling unchanged. | Must | A recogniser with an overridden `cancelVocabulary` produces a collision report computed against that override, not against `DefaultCancel`. A grammar whose collision the override resolves reports nothing. The two `VoxrBatchTestRunner` call sites are untouched. | item 1 architecture §4.1; design §5.5 |
| F13 | **The collision report is narrowed to values that can actually become choice vocabulary.** Item 1 left this open in a source comment — *"whether a same-intent tie is ever routed to the speaker is left open for the later items to decide"*. It is decided here: **same-intent ties are never routed** (`AreSiblingRivals` already refuses them, and the same command dispatches either way). So a value is reachable as an answer only if some co-member of its set carries **both** a different value **and** a different intent. Per-pair, not per-set — item 2 proved a set can be cross-intent overall while a pair inside it is not. | Must | A value whose only cross-member relationships are same-intent produces no collision report. The cross-intent collision item 1 measured still reports. Exactly one code site expresses "reachable as an answer", shared with the runtime gate's rule rather than a second copy. | item 1 architecture §4 comment; human ruling 2026-08-16; design §5.5 |
| F14 | **#91: the warning's element number indexes each quoted pattern correctly, in both shapes.** A per-member discriminator index (or per-member form length) on `SiblingMember`. Covers the **asymmetric** case the issue was opened for *and* the **symmetric** case it explicitly excluded — the carve-out died when item 2's #90 fix removed member normalisation, so `IndexOfSameMembers` returns `-1` and the longest-frame preference never runs. | Must | The issue's fixture (`["?please","switch","to","weapons"]` / `["switch","to","navigation"]`) and item 2's architecture §6.4 fixture (`["set","?now","mode","on"]` / `["set","?now","level","on"]` / `["set","mode","on"]`) both produce a message whose element number indexes the word the message is about, in every quoted pattern. Issue #91 closes on merge. | issue #91 + its 2026-08-16 comment; item 2 architecture §6.4, §11.2 |
| F15 | **The sibling warning names the flag.** Design §5.2's trailing clause — *"or enable ambiguity disambiguation (`<flag name>`)"* — was omitted from item 1 by ruling because the identifier did not exist. It exists now. | Must | The message names `disambiguateSiblingTies` and every remedy it offers exists in the shipped version. The affected message tests are amended to the new text. | item 1 requirements §8.1, F6; design §5.2, §5.7 |
| F16 | **DR-5's corrected rationale is recorded where a reader will meet it.** §5.8's *"the missing word may still arrive"* is false for the medial shape (§2). The source comment already says so after item 2; this feature adds the other half — that what the refusal buys is the *ask* — and the post-G2 product docs state it. The locked design is **not** amended; that is a G1 matter, ruled 2026-08-16. | Must | The source comment at the eager sibling condition and the product-doc paragraph both state what the refusal buys, and neither repeats the false rationale. `sibling-tie-disambiguation.md` §5.8 is unedited. | human ruling 2026-08-16; item 2 architecture §11.2 |
| F17 | **The eager path is not changed.** DR-5's refusal is already built and is not behind the flag. This feature adds no condition to `TryEagerCommit` and removes none. | Must | The diff touches no eager condition. Every item 2 eager test passes unedited, with the flag both on and off. | DR-5; design §5.8 |
| F18 | **The design's §7 measurement lands before the PR opens, and the numbers go in the PR body.** Four figures: (a) the 699-corpus A/B null result with the flag **off**, as the G-6 control; (b) the purpose-built disambiguation-flow fixtures, which item 2 explicitly deferred here as *"the item that has a flow to exercise"*; (c) construction-time warning volume across `Tests~` and the samples, re-measured because F13–F15 change what is emitted; (d) per-parse cost with the flag off. | Must | All four in the PR body at open, measured interleaved rather than sequentially. The 699 null result is cited **as a control**, with what it is blind to stated alongside it. | design §7 items 1–4; item 2 requirements §4.7 ruling; human ruling 2026-08-16 |

### 4.1 F1 — why a serialized field is the right shape, and why it is not a public API addition

The brief that opened this feature framed a `[SerializeField]` on `VoxrCommandRecogniser` as *"a public API addition, which items 1 and 2 both avoided"*. That framing is imprecise and worth correcting before it becomes a review argument.

Every tuning knob on this component is a `[SerializeField]` **private** field: `minConfidence`, `minScore`, `bufferWindow`, `eagerFlushOnCompleteMatch`, `prefixHoldSeconds`, `commandCooldown`, `pendingTimeout`, `pendingTimeoutBehavior`, `confirmVocabulary`, `cancelVocabulary`. None is public C#. Several carry an `internal` set-only property for tests (`VoxrCommandRecogniser.cs:937-943`). `disambiguateSiblingTies` matches that shape exactly and adds **no public C# member**.

What it *does* add is **serialized surface** — an inspector row that persists into scenes and prefabs, and which cannot later be renamed without a `FormerlySerializedAs`. That is a real commitment and the reason the name was settled by ruling (§8.2) rather than chosen in passing. It is not, however, the thing items 1 and 2 were avoiding; item 2's F17 was about not adding `public` members while the feature had no configuration to expose. This feature has one, by DR-7.

The genuine public API addition in this feature is **F11**, and it is treated as such: §4.10.

### 4.2 F2 — what "byte-identical with the flag off" has to cover, and the trap in it

Three surfaces have to be unchanged with the flag off, and only the first is obvious.

1. **Fired commands.** Same intents, slots, scores, confidences, event order and timing. The 699-corpus A/B covers this as a control.
2. **The generated grammar JSON.** F7 notes that discriminating values need no grammar addition because they are already pattern literals. That is a *claim about the decoder's vocabulary*, and if it is wrong the flag changes what VOSK can hear — in **both** configurations, because `GetFollowUpGrammarWords` is not flag-aware today. It is pinned rather than reasoned: the JSON is compared byte-for-byte across the flag.
3. **Per-parse cost.** F5 promotes a record from Editor-only to runtime. The trap is the one item 2 paid for: item 1's requirements claimed the sibling lookup's cost stayed gated "until item 3 ships it behind an opt-in flag", and that was wrong, because DR-5 is not behind the flag. **The lookup is therefore already built in every player build as of item 2.** What this feature must not do is add *per-parse* work on top of it when the flag is off — the lookup is a read, and with the flag off there is nothing to read it for.

The third is the one a reviewer should press on, because the natural implementation — record the tie unconditionally, branch on the flag when acting — is one line simpler and charges every flag-off player for a record nothing reads. That is precisely the shape item 1's §4.1 argued against on its own terms.

### 4.3 F5 — the record item 2 "left for item 3" is not usable as built, and this is not a criticism of item 2

Item 2's requirements §4.5 justified building the flush record early so that *"item 3 inherits a recording it can consume rather than one it must also introduce."* The reasoning holds — the split kept a hot-path change out of the feature that also changes behaviour — but the inherited artifact needs work before it can be consumed, and saying so plainly now prevents a review round later:

- it lives inside `ParseDiagnosticEntry`, which is `#if UNITY_EDITOR`;
- it is written inside an `#if UNITY_EDITOR` block in the flush selection loop;
- `LastParseDiagnostics` is an Editor-only array.

So the runtime path can read **none** of it. This feature promotes the *write* out of the Editor gate (under F2's cost constraint) and gives it a runtime carrier. What it must **not** do is put the carrier on `VoxrCommandResult`: that is a **public** readonly struct (`Runtime/Commands/VoxrCommand.cs:126`), and adding a field to it is a public API change nobody asked for. The parallel-buffer shape — an `internal` array aligned with `ResultBuffer`, which the recogniser already indexes in lockstep — is the constraint this requirement imposes; which exact form it takes is an architecture question.

Item 2's gating decision was correct for item 2, where the field was genuinely unread. The cost of that correctness is one promotion here, and it is smaller than the coupling the split avoided.

### 4.4 F5 — the record stays sibling-only, and that is forced, not preferred

Item 2 carried an open question to G2: canon contradicts itself on whether the flush records *any* tie (§5.3) or only a *sibling* tie (§9 row 2). The human ruled "keep as built" **because the field was inert** — and it is not inert here. The question was therefore re-opened at this feature's requirements stage rather than inherited silently, and the answer is different from the reasoning that produced it.

**The runtime record cannot be widened, for a reason that has nothing to do with cost.** DR-4 makes the discriminating values the choice vocabulary. A non-sibling tie has no discriminating values — that is what "not siblings" means. There is no question to ask and no vocabulary to ask it with. Recording a non-sibling tie at runtime would produce a pending the speaker cannot answer, resolvable only by cancel, re-utterance or timeout. That is strictly worse than firing the first-registered.

So the runtime scope is settled by DR-4. The only live choice was whether the **Editor** diagnostic should additionally record non-sibling ties, honouring §5.3's *"leaving them visible to the editor diagnostic"*. **Ruled by the human, 2026-08-16: keep it sibling-only**, so one concept has one shape across both configurations, and file the gap.

**Issue #95** now carries it, and states what closing it actually needs: not a relaxed gate but a record that separates *"a rival tied"* from *"a **sibling** rival tied"* — a second field or a small enum — plus the surface that renders the difference, plus the two tests that currently assert null. Filed 2026-08-16 so §5.3's unimplemented half sits on the board rather than in a gitignored design doc.

### 4.5 F19 — why the choice set is n-ary, and what item 2's first-rival rule actually was

Design §5.1 lists the properties that make the sibling set the right shared primitive, and two of them are about this feature:

> - It **generalises to n-ary sets** — `set auto pilot on` / `off` / `standby` form one sibling set with three discriminating values, not three pairs.
> - It **yields the disambiguation vocabulary for free (§5.5)**. The discriminating values *are* the choices.

Those two bullets are adjacent for a reason: canon's model runs the n-ary property straight into the runtime choice vocabulary. **No ratified decision record pins it** — DR-4 is silent on arity — and item 1's F3, which does require n-ary handling, is scoped explicitly to the author *warning*. So this is canon's prose model against an implementation detail, not a broken ruling.

The implementation detail is item 2's **first-rival rule**: the flush loop records a tied sibling rival only when `bestTiedSiblingCommandIdx < 0`, so at most one is ever kept. That was right for what item 2 built — an Editor diagnostic answering *"was the winner decided by a coin flip, and against whom?"* needs one exemplar. It is wrong for a vocabulary, because a vocabulary needs every answer the speaker might give.

The information is already there. On a three-way set with the discriminator elided, all three candidates match the same tokens with the same score, span and literal count, so **all three tie** — the loop sees rivals two and three and discards them. This feature keeps them.

**Ruled by the human, 2026-08-16: build n-way in item 3**, over capping at two (which would have left canon's model and the build disagreeing, and made the advertised one-word answer fail on the exact grammar §5.1 uses as its example), and over re-opening the design (disproportionate for a sentence no DR pins).

Two consequences are requirements rather than architecture:

- **The cap is bounded and its overflow is visible.** Recording is allocation-free, so the rival buffer is preallocated and finite. A sibling set larger than the cap offers the first N choices — it does not silently pretend that is all of them (the "no silent caps" discipline).
- **Duplicate-valued rivals are dropped from the offered set.** `AreSiblingRivals` already guarantees each rival differs in value *from the winner*, but two rivals can share a value with *each other*. Answering that word could not choose between them, so only the first is offered — the same rule item 1's F8 applies to author-duplicated patterns, one level up.

### 4.6 F6 — a derived requirement, stated as ours

The doc recon on this feature flagged that F6 originally cited *"DR-4; design §5.4"*, and that neither passage supports it: DR-4's ratified text is about widening the type, the third reason, the vocabulary, re-utterance and cancel/confirm; §5.4 says only that `VoxrPendingCommand` *"gains an alternatives field"* and that a parallel type was rejected. **Neither says anything about slots or about which candidate's match data an alternative carries.**

The citation is corrected to say so. F6 is derived here, from the ordinary correctness expectation that a fired command carries its own arguments, and it is not contradicted by canon. Recorded because a reviewer holding F6 against DR-4 would find the citation overstated, and because the requirements of this design have twice now been strengthened by catching exactly this kind of drift.

### 4.7 F9/F10 — the two places `AwaitingDisambiguation` is not just a third label

Most of the pending machinery genuinely does not care which reason it is holding, which is exactly DR-4's argument for widening rather than adding a parallel type. Two places do care, and they are the feature's real state-machine work:

**Confirm must become reason-dependent.** `TryHandleConfirmCancel` (`PendingCommandHandler.cs:80`) today checks cancel, then confirm, then gives up. Under `AwaitingDisambiguation` the confirm arm must not fire the pending command — "yes" is not an answer to "which?" — and it must not be *swallowed* either: it should leave the pending live so the speaker can still answer. Note the existing order is cancel-before-confirm (`:91` vs `:94`), which is what gives §5.5's collision its direction, and that order does not change.

**`HandleTimeout` must degrade.** `HandleTimeout` (`:236`) branches on `behavior` alone. Under `AwaitingDisambiguation` it must branch on the reason first: `FireAsIs` becomes `Cancel`. DR-6's argument is semantic, not a preference — `FireAsIs` means *"the intent is known, fire it with the slots I have"*, and under ambiguity **the intent itself** is unknown. Firing the first-registered after a pause coin-flips anyway, merely later, which is incoherent with an integrator who opted in specifically to stop coin-flipping.

Everything else — `Cancel()`, `EnterPending`'s cancel-the-previous, re-entry, `CancelPendingCommand()`, the disable/destroy paths at `:187`/`:217`/`:234`/`:460` — is reason-agnostic and stays that way. That is the DR-4 dividend, and the acceptance check for it is that none of those sites gains a reason branch.

### 4.8 F8 — the slot-fill path must not claim a disambiguation pending

`ProcessParsedResultsCore` runs the follow-up slot-fill (Step 2) before the normal parse, on any live pending. An `AwaitingDisambiguation` pending has no unfilled slots by construction — a command missing a required argument does not reach the fire path at all (#73 routes it to `PartialMatch` pending), so a command that reached the tie is complete.

`TryFollowUpSlotFill` already returns `null` when `UnfilledSlots` is empty (`:108`), so the existing guard covers it. This is stated as a requirement rather than left to the guard because the *reason* it is safe is a two-step argument through #73, and a future change to either end could break it silently. The acceptance check is a test that an `AwaitingDisambiguation` pending never advances through `AdvanceSlotFill`.

### 4.9 F4 — a stated limitation: push-to-talk plus cancel-on-release makes the question unanswerable

`VoxrPushToTalkController.ReleaseTalk` calls `FlushPendingBuffer()` and then, when `_cancelPendingOnRelease` is set, `CancelPendingCommand()` (`VoxrPushToTalkController.cs:99-104`). With this feature's flag on, the flush is what *creates* the disambiguation pending — so on that configuration the pending is created and cancelled in consecutive statements and the speaker is never asked.

This is not a defect introduced here; it is the documented meaning of `_cancelPendingOnRelease`, and under push-to-talk the recogniser is not listening after release anyway, so a pending that survived would have nobody to hear the answer. **The decision is to leave cancel-on-release applying to all three reasons** — the field's name is unqualified and safety-first is the right default for a flag whose whole purpose is to discard state on release.

Two consequences are in scope. The field's tooltip currently enumerates the reasons — *"any pending command awaiting confirmation or follow-up slot-fill"* — and that enumeration is now incomplete; it is corrected. And the combination is stated in the post-G2 product docs, because an integrator who turns on `disambiguateSiblingTies` in a push-to-talk project with `_cancelPendingOnRelease` set will observe the feature doing nothing, with no error and no warning.

### 4.10 F11 — the surface canon leaves open, and how narrow it has to be

Design §2.6 states the constraint — *"There is no event for 'I am not sure', and no field on which to hang one"* — and never resolves it. DR-4 says widening the internal types *"breaks no public contract"*, which is a statement about **compatibility**, not about **sufficiency**. Between them sits a gap: §5.5 assigns the wording of the question entirely to the integrator, and the integrator has no way to obtain the choices.

The gap is real and load-bearing in two directions:

- **A new subscriber cannot prompt.** They receive `mode_weapons` and would have to hard-code their own grammar's sibling relationships to know what the alternative was.
- **An existing subscriber silently misbehaves.** Anyone subscribed to `OnCommandPending` for `requiresConfirmation` today will treat a disambiguation as a confirmation and prompt "yes/no". Under F9 "yes" does nothing, so the pending sits until it times out and — under DR-6 — fires nothing. That is precisely the *"silently degrade to firing nothing"* failure DR-7's opt-in exists to prevent, reappearing inside the opt-in for anyone who turns it on.

**Ruled by the human, 2026-08-16: expose the reason and the choices, read-only.** The alternatives were exposing the reason alone (fixes the misprompt, leaves the prompt unwordable), exposing nothing (ships a deliberately hard-to-use opt-in), and stopping to re-open the design at G1 (disproportionate to a gap canon states but never decides).

**This is not §6.4's rejected n-best API**, and the distinction is what keeps the ruling inside the locked design. §6.4 rejected *"expose full candidate rankings"* on three specific grounds — a candidate-ranking type, ordering guarantees, and allocation strategy on a hot path. This surface has none: it describes **one pending ambiguity**, exposes no score, no rank and no ordering guarantee, is read only while a pending is live, and is never touched on the parse path. It is closer in kind to the existing `HasPendingCommand` / `PendingCommand` pair than to a candidate API.

The exact shape — property versus event payload, and what type carries the choices — is an architecture question. What is fixed here is the information (this is an ambiguity; these intents; these discriminating values), that it is read-only, and that it is reachable without `InternalsVisibleTo`.

### 4.11 F13 — deciding the question item 1 left in a comment

Item 1's source comment is explicit that this feature owns the answer:

> *"Whether a same-intent tie is ever routed to the speaker is left open for the later items to decide, so silencing this on their behalf would be guessing."*

**Decided, ruled by the human 2026-08-16: same-intent ties are never routed.** This is not a change — `AreSiblingRivals` already refuses on shared intent, for the reason item 1's §7.1 measured: the same command is dispatched either way, so asking costs the speaker a round-trip to choose between identical outcomes.

The consequence is that the collision report should follow. A discriminating value can only ever be *given as an answer* if the runtime would ever ask about it, and it would ask only about a cross-intent pair with different values. Reporting a collision on a value that can never be an answer is a knowingly false advisory — the same class of wrongness #81 spent a whole feature reversing on `WarnOnDroppableRequiredLiteral`, and the class item 1 was careful about everywhere else.

**The narrowing must be per-pair, not per-set.** Item 2 established that a set can be cross-intent overall while a particular pair inside it shares an intent — one command contributing two patterns alongside a third from another command. A set-level test would keep reporting values whose only cross-intent partner carries the same value, and drop values that do have a genuine partner. The test is: **this member has a co-member with a different value and a different intent.**

Expected effect on volume is small — item 1 measured exactly one collision across the entire corpus — which means F18(c) is the check that it did not accidentally silence the real one.

### 4.12 F18 — measurement discipline, and what the 699 corpus is for here

Item 2 spent two withdrawn claims learning this, and the rules carry forward unchanged:

- **Measure interleaved, never sequential.** If two harnesses disagree, withdraw the number rather than defend it.
- **A null result on the 699 corpus is a control, not evidence.** Here it is a particularly clean control: with the flag **off** the corpus must show zero change, and that is the direct evidence for F2. What it cannot show is anything about the flag being on, because the corpus contains no follow-up answers — it is a single-utterance corpus, and disambiguation is a two-turn exchange by construction. Item 2 deferred the flow fixtures here explicitly, as *"the item that has a flow to exercise"*.
- **The A/B rig cannot exercise this feature's main path.** It does not stage `VoxrCommandRecogniser.cs`, so the pending machinery is out of its reach; F4/F7–F11 are verified Unity-side, in PlayMode. The rig is still needed for two things: the flag-off 699 control, and checking that any new `#if UNITY_EDITOR` boundary compiles in the **player** configuration — it remains the only build in this project that does, since Unity always defines `UNITY_EDITOR`. Item 2 found a real player-build break that way.
- **PlayMode is where the pending tests live.** Design §7 item 4 calls for PlayMode tests of the third reason — timeout under DR-6, cancel-on-release, re-entry — and the project's bindings confirm most parser and command tests are PlayMode. An EditMode-only run misses them.

## 5. Non-functional requirements

- **Selection stays allocation-free per parse.** The flush loop is the innermost path over every `(command, pattern, startIdx)` triple. Recording the rival must not allocate there — no lists per candidate, no closures, no boxing. Capturing the rival's slots (F6) must use a preallocated buffer, exactly as `_bestSlotBuf` already does.
- **The ambiguous path may allocate; it is rare and it crosses into public events.** Building the rival's `VoxrCommand` and the choice array happens once per ambiguity, never per candidate. Following the existing precedent at `PendingCommandHandler.cs:155-159`, anything a subscriber can retain is freshly allocated, never pool-borrowed.
- **Flag-off costs nothing per parse** (F2). The lookup itself is already paid for by item 2 and is not re-litigated here.
- **Determinism.** The order of the choices presented through F11 must be stable across runs — registration order — or the tests that assert on them go flaky and an integrator's prompt reorders itself between sessions. This is the same discipline item 1 needed for warning emission order.
- **Editor-only stays Editor-only.** The warning, the collision report and the parse diagnostic remain Editor-gated. Only the tie record's runtime half crosses, and only under the flag.
- **No `InternalsVisibleTo` for integrators.** F11 must work from an ordinary assembly.

## 6. Non-goals / deferred

- **Item 4's guide-level narrative.** `Documentation~/command-recognition.md`'s spanning treatment and the rewritten `KNOWN_LIMITATIONS.md` entry belong to `feat-disambiguation-docs`. This feature writes only its own post-G2 product docs.
- **Issue #93.** Untouched, still deferred.
- **Issue #95** — the Editor diagnostic recording non-sibling ties. Filed by this feature, not built by it (§4.4).
- **A general n-best API.** §6.4 stays rejected; F11 is narrower by construction (§4.10).
- **TTS or any prompt rendering.** The package ships no speech synthesis and no UI. Wording and presenting the question is the integrator's job, per §5.5.
- **Ordinal choice vocabulary.** Rejected at DR-4: the speaker has no idea what order the patterns were registered in.
- **Routing same-intent ties.** Decided against (§4.11).
- **Default-on disambiguation.** DR-7 ships opt-in for the first release; default-on is reconsidered as a separate design topic after the warning has been in the field.
- **Changing the eager path** (F17), **#70's tail guard**, or any scoring arithmetic. Design §3's non-goals stand.
- **Recovering the dropped word.** Not dropped-word recovery; the design routes around the loss.

## 7. Dependencies & assumptions

- **Item 1 merged** (PR #92): `FindSiblingSets`, `SiblingSet`/`SiblingMember`, `IsSingleIntent`, `BuildSiblingWarning`, the cancel-collision report.
- **Item 2 merged** (PR #94, `337e758`): `CandidateOrder`, `AreSiblingRivals`, `EnsureSiblingLookup`, the `(commandIdx, patternIdx)` runtime lookup, the eager refusal, and the Editor-only flush record this feature promotes.
- **§2.8 is confirmed, not assumed.** Item 1 landed the test; item 2 inverted it. The design's contingency — that a refutation reopens the design — did not fire.
- **Assumption, to verify at implementation:** discriminating values are already in the decoder grammar because they are pattern literals (F7). Pinned by a grammar-JSON comparison rather than trusted.
- **Assumption, to verify:** a command reaching the flush tie is complete, so an `AwaitingDisambiguation` pending has no unfilled slots (§4.8). Rests on #73 routing incomplete commands to `PartialMatch` before the fire path.
- **Baseline to re-measure, not assume:** EditMode 129/129, PlayMode 479/479 at `337e758`.
- **Environment:** `gh pr edit` is broken here — use `gh api -X PATCH`. `rm -rf` is blocked. Local `main` goes stale; stage A/B "before" sides from an explicit SHA.

## 8. Open questions

### 8.1 Flush-side record scope — RESOLVED 2026-08-16

Re-opened from item 2 rather than inherited, because the ruling that settled it there rested on the field being inert. Runtime scope is forced sibling-only by DR-4; Editor scope ruled to match; gap filed as issue #95. See §4.4.

### 8.2 The flag's name and shape — RESOLVED 2026-08-16

`disambiguateSiblingTies`, a `[SerializeField]` private `bool` on `VoxrCommandRecogniser` defaulting to `false`, with an `internal` test setter. Chosen over `askWhenAmbiguous` (broader than what the flag governs) and `resolveAmbiguousCommands` ("resolve" overclaims — the package defers to the speaker and may fire nothing). See §4.1.

### 8.3 Same-intent ties and the collision report — RESOLVED 2026-08-16

Never routed; the report narrows to match, per-pair. See §4.11.

### 8.4 Issue #91 — RESOLVED 2026-08-16

Folded in, covering both the asymmetric and the (newly re-opened) symmetric case. See F14.

### 8.5 The public ambiguity surface — RESOLVED 2026-08-16

Reason **and** choices, read-only. Canon states the constraint (§2.6) but never decides it; DR-4's "breaks no public contract" is about compatibility, not sufficiency. See §4.10.

### 8.6 Arity of the choice set — RESOLVED 2026-08-16

Design §5.1 ties the n-ary sibling-set property directly to §5.5's runtime choice vocabulary; item 2's first-rival rule caps the recorded rivals at one. Surfaced by this feature's doc recon, not by canon. **Ruled: build n-way here** (F19, §4.5), over capping at two and documenting the gap, and over re-opening the design for a sentence no decision record pins.

### 8.7 Whether `OnCommandConfirmed` is the right event for a disambiguation answer — OPEN

Answering a disambiguation resolves through `PendingResolution.Confirmed`, which raises `OnCommandConfirmed` before `OnCommandRecognised`. That is consistent — a pending resolved into a fired command — but `OnCommandConfirmed` is documented in terms of `requiresConfirmation`, and an integrator may treat it as "the user said yes to a destructive action". Carrying to G2 rather than deciding silently. The alternative is raising only `OnCommandRecognised`, which would make a disambiguation answer indistinguishable from an ordinary recognition and lose the "this resolved a pending" signal.

### 8.8 Whether the choice vocabulary should accept a discriminating value that is also a *whole other command* — OPEN

If a grammar has both a sibling pair discriminated by `stop` and a standalone `stop` command, answering `stop` while pending is ambiguous between "choose the `stop` sibling" and "run the stop command". The choice check runs before the normal parse, so the answer wins — which is probably right while a question is on the table, but it is a precedence the design does not discuss. To be raised at G2 with a measurement of whether the shape occurs in the demo grammar at all.

### 8.9 The vacuous design-branch step — carried from item 1 §8.4 and item 2 §8.7, unresolved

`Planning~/` is gitignored, so design branches carry zero diff and the workflow's "merge the design branch to main" step is bookkeeping. Noted for the third time; still not worth a workflow change on its own.

## Related

- Locked design: `Planning~/design-docs/sibling-tie-disambiguation.md` §2.5, §2.6, §5.4, §5.5, §5.6, §5.7, §5.8, §9 row 3, DR-4, DR-6, DR-7
- Predecessors: `Planning~/features/sibling-set-detection/` (item 1 — architecture §4.1 is inherited here), `Planning~/features/tie-aware-selection/` (item 2 — requirements §4.5, §4.8; architecture §6.4, §11.2)
- Successor: `feat-disambiguation-docs` (item 4)
- Closes: issues #74, #91 · Filed: issue #95 · Deferred: issue #93
- Adjacent, not modified: #70 (`bestHasUnmatchedRequiredTail`), #73 (flush completeness), #77 (partial slot-fill re-arm), #65 (DR-7 admission), #90 (member retention)
