---
type: architecture
feature: disambiguation-pending
topic: sibling-tie-disambiguation
status: draft
updated: 2026-08-16
sources: [Planning~/features/disambiguation-pending/requirements.md, Planning~/design-docs/sibling-tie-disambiguation.md]
baseline: 337e758
---

# Disambiguation Pending — Architecture

All line references are at `337e758` and must be re-verified before editing.

## 1. Shape of the change

Five seams, in dependency order. The first three are the feature; the last two are the deferred debts it discharges.

| # | Seam | Files | Nature |
|---|---|---|---|
| A | **Runtime tie record** — promote item 2's Editor-only flush record to a runtime carrier, gated so a flag-off player pays nothing | `VoxrCommandParser.cs` | Un-gating + a parallel buffer |
| B | **Pending machinery** — `AwaitingDisambiguation`, the widened `VoxrPendingCommand`, the choice arm, DR-6's degrade | `VoxrPendingCommand.cs`, `PendingCommandHandler.cs` | State machine |
| C | **Wiring and surface** — the flag, the flush routing, the public ambiguity accessor | `VoxrCommandRecogniser.cs`, new public type | Integration + public API |
| D | **Vocabulary plumbing and report narrowing** — item 1 §4.1's deferred limitation, F13's reachability test | `VoxrCommandParser.cs`, `VoxrCommandRecogniser.cs` | Editor-only correctness |
| E | **Message fixes** — #91's per-member index, the flag clause | `VoxrCommandParser.cs` | Editor-only strings |

A→B→C is a chain: C cannot route what A does not record, and it cannot hold what B cannot represent. D and E are independent of all three and of each other, which makes them the natural first commits.

**B and C build as one phase.** They are separable on paper and not in verification: the only producer of an `AwaitingDisambiguation` pending is C's routing, so a B-only commit could be tested only by calling the state machine as a pure function — the exact shortcut §7 forbids and item 2's review caught. §10 records the merge.

**Seam A is larger than "un-gating".** Plan validation found that the flush path does not build the sibling lookup at all outside the Editor (§2.2), and that the pair test discards the discriminating values the whole feature needs (§2.4). Both are inside seam A and neither was visible from the design.

## 2. Seam A — the runtime tie record

### 2.1 What exists, and why it is not usable

Item 2's flush record (`VoxrCommandParser.cs:1692-1699`, `:1751-1774`, `:1828-1832`) is `#if UNITY_EDITOR` at three levels: the loop locals, the `else if` that writes them, and `ParseDiagnosticEntry` itself. The runtime path can read none of it (requirements §4.3).

The eager path's copy (`:3026-3027`, `:3062-3072`, `:3199`) is **already unconditional** — it has to be, because DR-5's refusal is not behind the flag. So the two copies item 2 deliberately kept in step now differ in build gating, and that difference is what this seam removes.

### 2.2 The lookup itself is Editor-only on the flush path — found by plan validation

**This is the finding that would have shipped an inert feature.** `AreSiblingRivals` reads `_siblingMemberships`, built lazily by `EnsureSiblingLookup()`. On the flush path that call sits **inside `#if UNITY_EDITOR`** (`:1676-1680`):

```csharp
            int searchStart = 0;
#if UNITY_EDITOR
            var diagnosticEntries = new List<ParseDiagnosticEntry>();
            EnsureSiblingLookup();
#endif
```

The only other callers are `WarnOnSiblingDiscriminator` (`[Conditional("UNITY_EDITOR")]`, so elided in a player) and `TryEagerCommit` (`:2996-2997`), which is reached only when `eagerFlushOnCompleteMatch` is set — and that defaults to **`false`** (`VoxrCommandRecogniser.cs:79`).

So in the default player configuration `_siblingMemberships` is never assigned, `AreSiblingRivals` short-circuits on its null check (`:1486-1487`), no rival is ever recorded, `PendingAmbiguity` is permanently null, and the flush fires the first-registered sibling exactly as today. **The feature would work in the Editor and do nothing in a shipped game.**

It is deliberate, not an oversight: item 2's architecture §3.4 states *"the flush path needs it only for the recording in §4.3, so a player's flush path never builds it."* True when the recording was Editor-only; false the moment a player can record.

The fix is one line — the flush path builds the lookup when it is going to record:

```csharp
            if (_recordSiblingTies)
                EnsureSiblingLookup();
```

with the `#if UNITY_EDITOR` block above keeping only `diagnosticEntries`. Because `_recordSiblingTies` is `flag || UNITY_EDITOR` (§2.3), item 2's cost decision is preserved exactly for flag-off players: they still never build it.

**Nothing in this project's verification would have caught it.** Unity always compiles with `UNITY_EDITOR` defined, so every EditMode and PlayMode test passes either way; the A/B rig is the only build in the player configuration and it checks **compilation**, not behaviour. That combination — a runtime path gated on a symbol every test defines — is the standing hazard of this codebase, and it is the second time this design has hit it (item 2's `_siblingSets` player break, its architecture §11.1).

### 2.3 The gate

A parser instance field, set at construction:

```csharp
// Whether the flush loop records which sibling rival tied the winner. The Editor always
// records (the parse diagnostic reads it); a player records only when the recogniser was
// configured with disambiguateSiblingTies, because with the flag off nothing reads it and
// DR-7 promises the opt-in costs nothing.
readonly bool _recordSiblingTies;
```

initialised `recordSiblingTies || UNITY_EDITOR`. It is a **constructor parameter**, not a settable property, and that is load-bearing: `VoxrCommandRecogniser` builds a new parser at two sites (`:199`, `:303`) on `Configure` / `RebuildParser`, and a post-construction setter is one site away from a silent flag-off-after-rebuild bug. The same constructor call already has to change for seam D, so this costs nothing extra.

The loop locals lose their `#if UNITY_EDITOR`; the `else if` keeps its condition and gains `_recordSiblingTies` **as its first conjunct**.

**The flag-off cost is per candidate, not per tie** — a draft of this section said otherwise and plan validation corrected it. Today the whole `else if` is compiled out of a player build (`#if UNITY_EDITOR` at `:1751`, `#endif` at `:1774`). Un-gated, a flag-off player evaluates the leading conjunct once for **every candidate that is not `Better`** — the innermost body of the `ci × pi × startIdx` loop, hundreds of evaluations per parse on the demo grammar, not the handful of ties. That is `O(commands × patterns × tokens)`.

`_recordSiblingTies` is a `readonly` field fixed at construction, so the branch predicts perfectly and the added work is one predictable test; but "one test per candidate" is the honest claim, and requirements §4.2(3) makes not adding per-parse work the thing a reviewer should press on. F18(d) measures it rather than arguing it.

### 2.4 The discriminating values have to be plumbed out of the pair test

`AreSiblingRivals` computes exactly the two strings the choice vocabulary needs and **throws them away** (`:1537-1551`):

```csharp
            var a = _siblingMemberships[ci1][pi1];
            var b = _siblingMemberships[ci2][pi2];
            ...
                if (a[i].SetId == b[j].SetId
                    && !string.Equals(a[i].Value, b[j].Value, StringComparison.Ordinal))
                    return true;
```

`a[i].Value` is the winner's value in that set; `b[j].Value` is the rival's. Without them there is no vocabulary and DR-4 cannot be built, so the pair test gains an out-parameter form:

```csharp
bool TryFindSiblingRival(int ci1, int pi1, int ci2, int pi2,
                         out int setId, out string winnerValue, out string rivalValue)

// The eager path's existing shape, now a wrapper. F17 forbids changing the eager
// CONDITION; it does not require the helper it calls to stay byte-identical.
bool AreSiblingRivals(int ci1, int pi1, int ci2, int pi2)
    => TryFindSiblingRival(ci1, pi1, ci2, pi2, out _, out _, out _);
```

**The truncated arm cannot supply values, and that decides what it does here.** When either pattern's expansion was truncated, `AreSiblingRivals` falls back to `RequiredElementsAreSiblings` (`:1531-1535`), which answers a `bool` about all-optionals-omitted readings and knows no set. That over-approximation is right for *refusing* on the eager path — it is the direction `ComputeCanCommitEarly` already fails in — but a refusal needs no vocabulary and a question does.

**As-built correction (2026-08-16).** A draft of this section asked for `TryFindSiblingRival` to return **false** under truncation while `AreSiblingRivals` was a pure wrapper over it. Those two cannot both hold: together they silently delete the eager path's truncated refusal, which is a behaviour change F17 forbids and which `TryEagerCommit_SiblingAnalysisTruncatedByTheExpansionCap_RefusesAnyway` and `_TruncatedPatternAsTheRival_RefusesToo` both pin. Caught at implementation.

What is built instead splits the answer rather than the method. The truncated arm returns the over-approximating **bool** and leaves `setId` at `-1`:

- the eager gate reads only the bool, so it refuses exactly as item 2 built it;
- the flush's recorder requires `setId >= 0`, so a truncated tie records no rival and fires the winner exactly as it would with the flag off.

Safe direction on both paths, from one call, and the wrapper stays a wrapper.

### 2.5 The runtime carrier

**Not on `VoxrCommandResult`.** That is a public readonly struct (`VoxrCommand.cs:126`) and adding a field to it is a public API change nobody asked for.

A parallel internal buffer aligned with `_resultBuf`, which the recogniser already indexes in lockstep. **It holds up to `MaxDisambiguationRivals` rivals, not one** (F19, §2.8):

```csharp
internal struct TiedSiblingRival
{
    public int CommandIndex;
    public int PatternIndex;
    public string Value;          // this rival's discriminating value
    public int SlotCount;
    public int EndIdx;            // its own — see below
}

internal struct TiedSiblingRecord
{
    public int RivalCount;        // 0 when nothing tied
    public int SetId;             // the ONE set this question is about (§2.6)
    public string WinnerValue;    // the winner's value in that set
    public int StartIdx;          // shared by every tied candidate — see below
    public bool Truncated;        // more rivals tied than the cap holds
}
```

`StartIdx` is on the record, not per rival, and the reason is an invariant rather than an economy: `CompareCandidate` returns `Tied` only when `startIdx == bestStartIdx` (`:2147-2148`), so every tied candidate begins at the same token. It is recorded because **`ComputeConfidence` takes a span** — `ComputeConfidence(tokens, startIdx, endIdx, …)` (`:1788`) — and a draft of this section asked for a per-alternative confidence while storing only `EndIdx`, which is not enough to compute one. `EndIdx` stays per rival because the tie compares `ConsumedEndIdx`, not `EndIdx`, so two tied candidates can genuinely differ by trailing `[unk]` neither consumed.

Rival detail lives in flat preallocated arrays rather than inside the struct, so nothing allocates per round: `_tiedRival[resultIdx * MaxDisambiguationRivals + n]`, and one `VoxrSlotMatch[]` slab of `MaxDisambiguationRivals * _maxSlotsPerPattern` per result slot (§2.7). All sized in the constructor from `_resultBuf.Length` and `_maxSlotsPerPattern`.

Written once per extraction round beside `_resultBuf[_resultCount++]`, so index `i` of one describes index `i` of the other. `RivalCount` is reset to `0` at the top of each round — a stale record from a previous round surviving into one that found no tie is the exact failure item 2's review caught when these locals were hoisted out of the extraction loop, and item 2's own review left it **unpinned** (its GAP-3: *"no multi-round test pins the per-round tie reset"*). This feature moves those locals, so it pins it (§7).

### 2.6 One question at a time — the winner can belong to several sibling sets

A pattern can be a member of more than one set, and the source says so at `:243-252`: *"`["a","b","c"]` is a sibling of `["a","b","d"]` at position 2 and of `["a","x","c"]` at position 1."* `AreSiblingRivals` answers true when the two patterns share **any** set, so a naive n-ary rival list can mix a rival that differs at position 2 with one that differs at position 1 — two different questions, and two different winner values, presented as one choice list.

It is reachable, not theoretical: `["set","the","ship","mode","on"]` / `[…,"level","on"]` / `[…,"mode","off"]` on the transcript `set the ship` gives all three the same score and span, so all three tie.

**The record therefore fixes one `SetId` — the set matched by the first accepted rival — and later rivals are accepted only if they tie the winner in that same set.** `TryFindSiblingRival` already returns the `SetId` it matched, so this is a comparison, not a search. The question stays coherent: one discriminator position, one winner value, one list of alternatives to it.

Which set wins is registration order, deterministically, consistent with the determinism requirement. A winner carrying two live ambiguities at once has the second one unasked; that is the same class as the cap (§2.8) and is reported the same way.

### 2.7 The rivals' slots — why they are captured, not derived

F6 requires each alternative to fire with the slots **its own match produced**. Siblings are element-wise equal but for one required literal, so the slots agree in every case anyone can construct — but the tie is between *authored* patterns that may reach the sibling relation through different optional expansions, and "they agree in practice" is not a reason to fire a command with another candidate's arguments. F6 is derived here rather than inherited: requirements §4.6 records that DR-4 and §5.4 say nothing about slots, so the citation is ours.

Capture needs a rival slab beside `_matchSlotBuf` / `_bestSlotBuf`:

```csharp
// MaxDisambiguationRivals slots' worth per result, preallocated in the ctor.
VoxrSlotMatch[] _rivalSlotBuf;   // _resultBuf.Length * MaxDisambiguationRivals * _maxSlotsPerPattern
```

`Array.Copy` from `_matchSlotBuf` at the moment a rival is recorded, mirroring what the `Better` branch already does at `:1742-1749`. This keeps the flush loop allocation-free (requirements §5): every buffer is preallocated, and the only allocation on the whole path is the `VoxrSlotMatch[]` built per alternative for its `VoxrCommand` — once per *ambiguity*, in the recogniser, not once per candidate.

### 2.8 The rival cap, and what happens past it

`MaxDisambiguationRivals` bounds the preallocation. A sibling set with more members than the cap offers the first N choices and sets `Truncated`.

**The overflow is reported at construction, not merely flagged at parse time.** A draft set `Truncated` and nothing read it, which is the silent cap F19 forbids wearing a boolean. Set *sizes are known at construction*, so `WarnOnSiblingDiscriminator` is where an author can be told, once, with no per-parse cost and while they can still act:

> …this set has 6 members, and runtime disambiguation offers at most 4 choices, so the remaining values cannot be answered in one word.

`Truncated` stays on the record and is surfaced on the public ambiguity (§4.4) so an integrator can word "…or say the whole command again", but the construction warning is what discharges F19's acceptance check.

The cap is chosen against measured set sizes across `DemoGrammar` and `Tests~` rather than guessed.

**Measured 2026-08-16** (`FindSiblingSets` over `DemoGrammar.AllCommands()`, run in the player configuration through the A/B rig's staging):

| | |
|---|---|
| sets on the demo grammar | 11 (matches item 1's pin) |
| largest set | **4 members, 4 distinct values** — `close`/`set`/`make`/`open` `distance {range} target {target}`, same-intent |
| largest **cross-intent** set | 2 values (1 rival) — every one of the five |
| largest fixture across `Tests~` | 3 members / 3 values |

So the largest shape measured anywhere needs **3** rivals, and every shape that would actually be *routed* needs 1. **`MaxDisambiguationRivals = 4`** — five choices — clears the largest measured set with room to spare, at a cost of `_resultBuf.Length * 4` rival records plus the matching slot slab.

**Two rivals sharing a discriminating value are not both offered.** `AreSiblingRivals` guarantees each rival differs in value *from the winner*, but two rivals can carry the same value as *each other* — the #90 shape one level up. Answering that word could not choose between them, so only the first is kept, exactly as item 1's F8 leaves author-duplicated patterns alone.

**As-built correction (2026-08-16): the dedup runs in the loop, not where the choice arrays are built.** A draft put it in the recogniser "so the hot path stays a bounded append". That makes `Truncated` lie: a duplicate would consume a cap slot, so a two-value set with three duplicate spellings could report truncation having lost nothing, while the count no longer means "choices offered". Deduping at the point of recording costs a bounded scan over at most `MaxDisambiguationRivals` strings — on a comparison that has already walked two membership arrays to get there — and buys a cap that counts offerable choices and a `Truncated` that means a real answer did not fit. It also puts the rule in one place instead of two.

Relatedly, `Truncated` is set when a rival is **rejected** for want of room, not when the buffer happens to fill exactly. A set with precisely `MaxDisambiguationRivals` rivals loses nothing and says so.

**Widened at review (§8b).** Three paths drop a rival the speaker could have answered with, and the flag now covers all three: the cap, the set-coherence rejection (§2.6), and a rival whose intent resolves to no definition (§4.2). It reads "an answerable rival was dropped", not "the cap was hit" — which is what F19's "never silently truncated" actually asks for, and what §2.6's text already claimed.

### 2.9 What stays exactly as item 2 built it, and what does not

`ParseDiagnosticEntry.TiedSiblingIntent` / `.TiedSiblingPatternIndex` keep their type, their meaning and their **sibling-only** scope (requirements §4.4). They are populated from a **diagnostic exemplar** — the first sibling rival of the round, offerable as a choice or not — so every existing Editor diagnostic test asserts unchanged values

*Corrected at review (§8b).* A draft read them from **rival 0 of the choice list**, which quietly broke "keep their meaning": under expansion truncation a pair ties provably but cannot be *named*, so it is refused as a choice — and the diagnostic went null on exactly the shape where the flush still coin-flips. The two questions are different. The choice list asks "what can the speaker say?"; the diagnostic asks "was this a coin flip, and against whom?", and under truncation the answer to the second is still yes. Two ints, cleared on adopt with the rest of the round's tie state — including the five that pin exactly this (`LastParseDiagnostics_SiblingTie_NamesTheRivalThatTiedTheWinner`, `_NoTie_RecordsNoRival`, `_SameIntentTie_RecordsNoRival`, `_ShadowedRival_IsTheCrossIntentOne`, `_SameIntentAcrossTwoCommands_IsNotTheRecordedRival`). Issue #95 carries the widening to non-sibling ties.

The **clear-on-adopt rule** is unchanged: a new `Better` candidate resets the rival count to zero.

**The first-rival rule is replaced** (`bestTiedSiblingCommandIdx < 0` → `rivalCount < MaxDisambiguationRivals`). That rule was correct for what item 2 built — an Editor diagnostic naming *a* rival needs one exemplar — and is undersized for a choice vocabulary, which needs every answer the speaker might give. Requirements §4.5 records the ruling and design §5.1's model that motivates it.

**The eager path keeps the first-rival rule** and is otherwise untouched (F17). It only ever asks *whether* a sibling tie exists, to return `None`; it never needs to know how many. Item 2's comment at `:1758-1764` says the two copies "must stay in step" because the first-rival and clear-on-adopt rules are the same rules — that is now **half true**, and the comment is corrected rather than left to mislead: the clear-on-adopt rule stays shared, the recording depth deliberately diverges, and the reason is that only the flush has a consumer that cares.

## 3. Seam B — the pending machinery

### 3.1 The widened type

```csharp
internal enum VoxrPendingReason
{
    PartialMatch,
    AwaitingConfirmation,
    AwaitingDisambiguation,       // appended — existing values keep their ordinals
}

internal struct VoxrPendingCommand
{
    public VoxrCommand Command;
    public VoxrCommandDefinition Definition;
    public string[] UnfilledSlots;
    public VoxrPendingReason Reason;
    public float CreatedTime;

    // AwaitingDisambiguation only; null otherwise. Parallel arrays: Choices[i] is the command
    // that fires when the speaker says ChoiceValues[i]. Index 0 is always the candidate that
    // would have fired without the flag, so registration order is preserved for the integrator's
    // prompt (the determinism requirement in §5).
    public VoxrCommand[] Choices;
    public string[] ChoiceValues;
    public VoxrCommandDefinition[] ChoiceDefinitions;
}
```

Appending the enum value rather than inserting it matters because `VoxrPendingReason` is `internal` but nothing stops a serialized or logged ordinal from existing; appending is free and inserting is not.

**Arrays because the choice set is genuinely n-ary** (F19). `Choices.Length` is `1 + rivalCount`, capped at `1 + MaxDisambiguationRivals`, with index 0 the candidate that would have fired with the flag off. Design §5.1 is the authority: a sibling set *"generalises to n-ary sets — `set auto pilot on` / `off` / `standby` form one sibling set with three discriminating values, not three pairs"*, and *"the discriminating values **are** the choices."*

### 3.2 The choice arm

`TryHandleConfirmCancel` (`PendingCommandHandler.cs:80`) keeps its name and its single call site, and gains one reason-dependent branch. The order it establishes does not move — cancel is checked first, which is what gives design §5.5's collision its direction and its remedy:

```
cancel vocabulary        → Cancel()                    (all three reasons)
AwaitingDisambiguation?  → choice match → fire that choice
                         → confirm vocabulary: NO-OP, pending stays live
otherwise                → confirm vocabulary → Confirmed
```

The choice match reuses `IsVocabularyMatchTokens` (`:279`) against `ChoiceValues`, so it inherits `MatchPhraseAgainstTokens`'s whole-utterance semantics (`:289`) — the answer must consume all tokens, which is what stops `"set alpha mode on"` being read as the bare choice `"mode"`. That utterance is a full re-utterance and belongs to the parse path (§4.3), not the choice path.

**Confirm under `AwaitingDisambiguation` returns `NoAction`, deliberately not `Cancel`.** "Yes" is not an answer to "which?", but it is also not an instruction to abandon; leaving the pending live lets the speaker follow it with the actual answer inside the same timeout window.

A choice that matches resolves through the existing `Complete(...)` path rather than a new one, which buys DR-4's dividend — but **not for free, and a draft of this section claimed it was**.

`Complete` re-enters `AwaitingConfirmation` by reading `pending.Definition` (`:216-217`, `:220`), which is the definition stored at `EnterPending` — the **winner's**. If the speaker picks a rival, the confirmation decision would be taken from the wrong command: a destructive rival marked `requiresConfirmation` fires immediately without asking, or a benign rival is gratuitously confirmed because the winner required it. Widening the reason test alone does not fix this; plan validation caught it.

So `Complete` takes the resolved definition:

```csharp
internal PendingResolution Complete(VoxrCommand completed,
                                    VoxrCommandDefinition resolvedDefinition,
                                    float currentTime)
```

The two existing callers pass `pending.Definition` and are unchanged in behaviour; the choice arm passes `ChoiceDefinitions[i]`. With that, an ambiguous command whose answer is a `requiresConfirmation` intent asks "which?" and then "are you sure?", in that order — the only coherent order, since you cannot confirm an intent you have not identified.

**Two as-built details (2026-08-16), neither anticipated here:**

- The re-entry test inside `Complete` reads `pending.Reason != AwaitingConfirmation` rather than `== PartialMatch`. Equivalent under the two old reasons, and it covers the third without a second clause.
- **`TryHandleConfirmCancel` gains a `currentTime` parameter.** The choice arm resolves through `Complete`, which may re-enter a confirmation, and that re-entry needs a fresh clock — the method had no time to give it. One call site, and the file already takes time explicitly everywhere else for testability.

**`EnterPending` also has to carry the arrays.** Its signature (`:59-63`) has no parameter for them and its initialiser sets five fields (`:68-75`), so routing through it as-is would leave `Choices` null forever. It gains the three arrays as optional trailing parameters, defaulting null, so the two existing call sites (`VoxrCommandRecogniser.cs:765`, `:821`) stay as they are.

### 3.3 DR-6's degrade

```csharp
internal PendingResolution HandleTimeout(VoxrPendingTimeoutBehavior behavior)
{
    var pending = _pendingCommand.Value;
    _pendingCommand = null;

    // DR-6: under ambiguity the INTENT is unknown, not merely the arguments. FireAsIs means
    // "the intent is known, fire it with the slots I have" — a different situation wearing the
    // same flag. Firing the first-registered after a pause coin-flips anyway, merely later,
    // which is incoherent with an integrator who opted in specifically to stop coin-flipping.
    if (behavior == VoxrPendingTimeoutBehavior.FireAsIs
        && pending.Reason != VoxrPendingReason.AwaitingDisambiguation)
        return PendingResolution.Confirmed(pending.Command);

    return PendingResolution.Cancelled(pending.Command);
}
```

The public `VoxrPendingTimeoutBehavior` keeps two values. A third was rejected at DR-6 as public API for an edge case, and can be added later without breaking anything.

### 3.4 Everything else is reason-agnostic, and stays so — with one exception recon found

`Cancel()`, `EnterPending`'s cancel-the-previous, `AdvanceSlotFill`, `ComputeUnfilledSlots`, the reconfigure and disable cancels (`VoxrCommandRecogniser.cs:187` and `:217` — the two `Configure` overloads, `:234` `SetActiveSets`, `:460` `OnDisable`; there is no `OnDestroy` or scene-change handler, a mislabelling plan validation caught), and `CancelPendingCommand()` do not learn about the new reason. **That none of them gains a reason branch is the acceptance check for DR-4's argument** — it is the evidence that widening the type beat adding a parallel one.

**The exception is `Editor/VoxrDebugWindow.cs:336-338`**, which recon surfaced and which neither the design nor this doc's first draft accounted for. It is an Editor-assembly consumer of `VoxrPendingReason`, reached through `EditorPendingCommand` and `InternalsVisibleTo("Jinwoo1601.VoXR.Editor")`, and it renders the reason as a **two-way ternary**:

```csharp
string reason = p.Reason == VoxrPendingReason.PartialMatch
    ? "Partial match — waiting for follow-up"
    : "Awaiting confirmation";
```

A third enum value makes this silently wrong rather than merely incomplete: an `AwaitingDisambiguation` pending is labelled *"Awaiting confirmation"* in the debug window — the exact confusion F11 exists to prevent, reproduced in the package's own tooling. It becomes a three-way branch, and it is the reason "add an enum value" is not a one-file change. Grep for `VoxrPendingReason` across **both** assemblies before declaring the seam complete.

### 3.5 `TryFollowUpSlotFill` needs no guard

It returns `null` when `UnfilledSlots` is empty (`:108`), and an `AwaitingDisambiguation` pending always has it empty, because a command missing a required argument never reaches the fire path — #73's gate at `:755-756` precedes the routing point at `:818`, so anything reaching the new branch passed `!IsIncomplete`. Requirements §4.8 pins this rather than trusting the two-step argument.

**One guard is added despite being unreachable today.** `AdvanceSlotFill` (`:197-204`) copies `Reason` forward but would drop the three new arrays, producing a pending with `Reason == AwaitingDisambiguation` and `Choices == null` — which the choice arm would dereference at `IsVocabularyMatchTokens`'s `vocabulary.Length` (`:281`). The argument above says that state cannot arise; the guard is what keeps the argument true if either end changes. Plan validation flagged it as a latent hazard rather than a live defect, and it is treated as one: carry the arrays through `AdvanceSlotFill`, and it costs three assignments.

**Also unreachable, also stated:** the alternative that fires is never completeness-checked. #73's gate proves the *winner* complete, and `Complete` → `Confirmed` → `InterpretResolution` (`:898-905`) fires the chosen command with no `IsIncomplete` test. A rival missing a required slot takes `RequiredSlotMissPenalty = -1.0f` (`:70`) and could not have tied on score, so the shape is unreachable — but the safety property is asserted of the winner and *used* on the alternative, which is worth a reviewer's attention and is recorded rather than glossed.

## 4. Seam C — wiring and public surface

### 4.1 The flag

```csharp
[Tooltip("When the recogniser cannot tell two commands apart — they differ only by one word " +
         "and the recogniser dropped it — ask instead of guessing. The pending command is " +
         "raised through OnCommandPending with PendingAmbiguity set; the speaker answers with " +
         "the distinguishing word. Off by default: with no OnCommandPending subscriber an " +
         "ambiguous utterance would fire nothing at all.")]
[SerializeField] bool disambiguateSiblingTies = false;
```

plus `internal bool DisambiguateSiblingTies { set => disambiguateSiblingTies = value; }` beside the existing test setters at `:937-943`.

The tooltip's last sentence is DR-7's reasoning, stated where the integrator makes the decision rather than only in a design doc they cannot see.

### 4.2 The flush routing

In `ProcessParsedResultsCore`'s Step 7 loop, a new branch **after** the confidence and debounce checks and **before** the `RequiresConfirmation` check (`:818`):

- *after debounce* because a command on cooldown should not raise a question the speaker then answers into a cooldown;
- *before confirmation* because "which?" precedes "are you sure?" (§3.2), and because `Complete` then handles the sequencing for free.

The branch reads `_parser.TiedSiblingBuffer[i]` and fires only when `disambiguateSiblingTies` is set and `RivalCount > 0`. It builds the `1 + RivalCount` entry `Choices` / `ChoiceValues` / `ChoiceDefinitions` arrays — index 0 the winner, then each rival — calls `EnterPending(..., AwaitingDisambiguation, choices, values, definitions)`, and `continue`s, so the command never reaches `_acceptedBuf` (`:834`).

*Value-dedup is no longer done here: as built it happens where the rival is recorded (§2.8's as-built correction), so every rival in the buffer is already an offerable choice.*

**As built the branch delegates twice, and both placements are deliberate.** `TryBuildAmbiguity` on the recogniser owns the definition lookups and the fall-through rule — those need `_setManager`. `BuildRivalCommand` on the **parser** owns constructing each alternative's `VoxrCommand`, because `_slotNames` and the confidence span both live on that side and exporting either would widen the parser's surface for no gain. The pending also carries `ChoicesTruncated`, which §3.1's sketch omitted: `Truncated` is on the parser's per-parse record, which is overwritten by the next parse, so the value has to be copied onto the pending to still be readable when the integrator asks.

**Each alternative's definition is resolved through `_setManager.TryLookupCommand`**, the same lookup the surrounding branches use. A rival whose lookup fails is dropped from the choice list rather than offered; if that leaves fewer than two choices, the branch falls through and fires the winner as today.

*As built, that lookup cannot fail* — every construction site builds the parser and the lookup from one command array, so an intent the parser can report is always resolvable. The guard stays for the reason §3.5's `AdvanceSlotFill` guard stays: it keeps the argument true if either end changes. A test written to reach it was removed at review, because the shape it used (registering the rival in an inactive set) rebuilds the parser from the *active* commands and so removes the tie itself. This matches how `IsIncomplete` treats an intent with no definition (`:879-882`) — a tie we cannot fully describe is not a reason to fire nothing.

**Two consequences the first draft of this section missed, both caught by plan validation:**

- **The branch must set `anyThresholdFiltered`.** Without it, an utterance whose only result entered disambiguation reaches `acceptedCount == 0` with the flag clear and raises **`OnUnrecognisedSpeech`** (`:845-853`) — telling the integrator the speech was not understood in the same frame it was asked to prompt about it. Note the existing `RequiresConfirmation` arm (`:818-831`) has this same shape and *does* raise `OnUnrecognisedSpeech` today. That is pre-existing behaviour on a path this feature does not own; it is **filed as an issue, not fixed here**, per the surgical-change rule.
- **The branch must add an Editor diagnostic attempt.** Every other arm appends one under `#if UNITY_EDITOR` — `:771` (partial pending), `:783` (rejected), `:799` (confidence), `:811` (debounce), `:827` (awaiting confirmation). Without one, a disambiguated result vanishes from `LastMatchDiagnostics` and from the debug window, in a feature that exists because integrators cannot otherwise see why nothing fired.

### 4.3 Full re-utterance

No new code, but not for the reason the first draft gave. `set alpha level on` while a disambiguation is pending is complete and unambiguous, so Step 4 sets `hasCompleteNewCommand` and the existing preemption at `:710-711` cancels the pending; Step 7 then fires it.

**An ambiguous re-utterance takes the same route, not the one §4.2 might suggest.** Step 4's test (`:648-660`) is score, confidence and `!IsIncomplete` — it knows nothing about ties — so a second ambiguous utterance *also* sets `hasCompleteNewCommand` and is cancelled at `:710-711`, **before** Step 7. `EnterPending`'s own cancel-the-previous is a no-op by then, and one `OnCommandCancelled` raises for the superseded question. That is correct behaviour (the old question is genuinely abandoned) but it is an event the first draft did not account for, and F8's test asserts it rather than being surprised by it.

### 4.4 The public surface (F11)

```csharp
public readonly struct VoxrPendingAmbiguity
{
    // Choices[i] is the command that fires if the speaker says DiscriminatingValues[i].
    // Index 0 is the candidate that would have fired with disambiguation off.
    public readonly VoxrCommand[] Choices;
    public readonly string[] DiscriminatingValues;

    // True when the sibling set held more members than the runtime offers as choices
    // (§2.8). The remaining intents are reachable only by re-uttering the whole command,
    // so an integrator can word "…or say the whole command again".
    public readonly bool IsTruncated;
}
```

on `VoxrCommandRecogniser`:

```csharp
public VoxrPendingAmbiguity? PendingAmbiguity { get; }   // null unless AwaitingDisambiguation
```

**`HasValue` is the reason signal.** One member answers both halves of F11 — an integrator subscribed to `OnCommandPending` for `requiresConfirmation` checks `PendingAmbiguity.HasValue` to tell a "which?" from a "sure?", and reads the same value to word the prompt. No public reason enum is added, so `VoxrPendingReason` stays `internal` exactly as DR-4 specifies.

Arrays rather than a span or an indexer: `VoxrCommand.Slots` and `OnCommandsRecognised`'s payload are already `T[]` on this surface, and a nullable struct property cannot return a `ReadOnlySpan`. They are allocated once at pending entry and handed out by reference, following the `PendingCommandHandler.cs:155-159` precedent that anything crossing into public events is freshly allocated and never pool-borrowed.

Why this is not §6.4's rejected n-best API, in the terms §6.4 itself used: no candidate-ranking type (these are the tied candidates, not a ranking), no ordering guarantee beyond "index 0 is what would have fired", no allocation on the parse path (it is built on the ambiguous path only), and it is readable only while a pending is live.

### 4.5 The push-to-talk interaction

`ReleaseTalk` (`VoxrPushToTalkController.cs:99-104`) flushes and then, when `_cancelPendingOnRelease` is set, cancels — so on that configuration this feature's pending is created and destroyed in consecutive statements. Left as-is by decision (requirements §4.7): the field's name is unqualified, and after release the recogniser is not listening, so a surviving pending would have nobody to hear the answer.

One code change follows: the field's tooltip enumerates the reasons — *"any pending command awaiting confirmation or follow-up slot-fill"* — and that enumeration is now incomplete. It is corrected to cover all three.

## 5. Seam D — vocabulary plumbing and report narrowing

### 5.1 The constructor parameter (item 1 architecture §4.1)

`VoxrCommandParser` is `internal` (`:64`) — its constructor is declared `public` (`:297`) but is unreachable from outside the assembly, so the signature is free to grow. `WarnOnSiblingDiscriminator()` runs *inside* the constructor (`:477`), so the vocabulary must arrive as a parameter — a settable property would be set after the warning had already been emitted.

```csharp
internal VoxrCommandParser(
    VoxrSlotDefinition[] slots,
    VoxrCommandDefinition[] commands,
    float coverageWeight = DefaultCoverageWeight,
    string[] additionalGrammarWords = null,
    string[] effectiveCancelVocabulary = null,   // new — seam D
    bool recordSiblingTies = false)              // new — seam A
```

Both new parameters are optional, so `VoxrBatchTestRunner`'s two construction sites (`VoxrBatchTestRunner.cs:28`, `:63`) compile untouched — which F12 requires. The recogniser's two sites pass `cancelVocabulary` and `disambiguateSiblingTies`.

The collision test then runs against the effective array, falling back to `VoxrFollowUpVocabulary.DefaultCancel` when the override is null or empty — mirroring exactly what `TryHandleConfirmCancel` does at runtime (`:86-87`), so the report and the behaviour it predicts read from the same rule. Note there is no `CollidesWithCancelVocabulary` method to change: item 1's architecture sketched one, but the shipped code inlines `Array.IndexOf(VoxrFollowUpVocabulary.DefaultCancel, …)` in the loop at `:1257-1260`. Recon confirmed the name exists nowhere in the repo.

### 5.2 The reachability narrowing (F13)

The collision loop (`:1255-1282`) gains one test per member: **this member has a co-member with a different value and a different intent.** That is the set-local form of the runtime pair test, and it must be per-pair rather than per-set for the reason item 2 established — a set can be cross-intent overall while a particular pair inside it shares an intent.

**How the two consumers share the rule matters, and the first draft got it wrong.** That draft said `AreSiblingRivals` should "resolve indices to members" and apply a shared predicate over `SiblingMember`s. That is a null dereference in a shipped player: `_siblingSets` is assigned only inside `#if UNITY_EDITOR` (`:1407-1412`), its declaration carries a `#pragma warning disable CS0649` precisely because a player leaves it null, and `AreSiblingRivals` runs in a player on the eager path because DR-5 is not behind the flag. It would also reverse a decision the source records at `:1385-1386` — *"The value rides along so the pair test below never has to walk `SiblingSet.Members` looking for a matching (command, pattern)."*

What the two consumers actually share is a two-line predicate over values and intents, not a data structure:

```csharp
// A rival is answerable only if the speaker could pick it out: a different word
// (or there is nothing to say) AND a different intent (or the answer changes nothing).
static bool IsAnswerableRival(string valueA, string intentA, string valueB, string intentB)
    => !string.Equals(valueA, valueB, StringComparison.Ordinal)
    && !string.Equals(intentA, intentB, StringComparison.Ordinal);
```

The runtime path feeds it `SiblingMembership.Value` and `_commands[ci].Intent`; the Editor report feeds it `SiblingMember.Value` and `.Intent`. One rule, one definition, no shared structure and no Editor-only field reached from a player.

The source comment at `:1242-1254` — which explicitly leaves this open for "the later items to decide" — is replaced by the decision and its reasoning, not merely deleted.

The source comment at `:1244-1252` — which explicitly leaves this open for "the later items to decide" — is replaced by the decision and its reasoning, not merely deleted.

## 6. Seam E — the message fixes

### 6.1 #91: a per-member discriminator index

`SiblingMember` gains the discriminator's index **in the member's authored pattern**, which is what the message quotes:

```csharp
public readonly int AuthoredDiscriminatorIndex;
```

Computed where the member is created (`:1000-1002`), by mapping the form index `d` back to the authored pattern. **The mapping is exact by counting, not by string matching**, and the reason is worth stating because the obvious implementation — a greedy two-pointer walk comparing element strings — is ambiguous on a pattern carrying two identical optionals (`["?x","?x","b"]`), which item 2 established is reachable since `ExpandOptionals` does not deduplicate.

The exact rule rests on two facts verified from source:

1. `ExpandOptionals` (`:3262-3285`) builds each form by walking the authored pattern in order and **omitting** a subset of the optional positions. Every surviving element is the same string at the same relative order — kept optionals retain their `?` decoration.
2. `FindSiblingSets` only ever takes `d` where `IsRequiredLiteral(form[d])` (`:968-969`), and **required elements are never omitted**.

So the required elements of the form are exactly the required elements of the authored pattern, in the same order. The discriminator is the `(r+1)`-th required element of the form, where `r` is the count of non-optional elements before `d`; therefore it is the `(r+1)`-th non-optional element of the authored pattern. One walk over the authored pattern counting non-optionals finds it. No string comparison, no ambiguity, and no dependence on which optionals a given mask happened to keep.

When `WarningForms` returns the raw pattern — no optionals, or past `MaxWarningExpansion` (`:711`, `:721-727`) — the form *is* the authored pattern and the rule degenerates to `d` itself, as it must.

`BuildSiblingWarning` then renders adaptively, which is the established idiom here (`BuildDroppableLiteralWarning` branches on intent equality at `:746-750`):

- **all members agree on the index** — the overwhelmingly common case — the message keeps **today's exact wording**, `"…that differ only at element 3"`. Most existing message tests do not move.
- **members disagree** — the #91 shape — the number moves onto each quoted pattern, and the shared clause drops its number.

That split is deliberate test economy: the defect only exists when the indices disagree, so only the messages that were wrong change.

The `" (with its optional elements omitted)"` note **stays**. It was introduced as mitigation for this defect, but it carries information the per-member index does not: it explains why two patterns of visibly different lengths are siblings at all. Removing it would leave an author looking at a 4-element and a 3-element pattern told they "differ only at" one element.

Both fixtures from the issue and from item 2's architecture §6.4 are pinned.

**`BuildCancelCollisionWarning` carries the same defect and is fixed with it.** It names one member but prints the **set-level** index (`:1615-1627`):

```csharp
    return $"[VoxrCommandParser] Intent '{member.Intent}' carries the discriminating "
         + $"value \"{member.Value}\" at element {set.DiscriminatorIndex + 1}, which is "
```

That is exactly #91's shape — the number indexes the collapsed frame, not the member's authored pattern — in a message this feature is already editing for F12. It takes the member's `AuthoredDiscriminatorIndex`. F14 says "each quoted pattern", and this message quotes one.

**Its wording also has to follow F12.** The text asserts the value *"is also in the **default** cancel vocabulary"* and offers *"Rename the literal, or **override cancelVocabulary** on VoxrCommandRecogniser"*. Once the report is computed against a configured override (§5.1), both halves are false in that case: the collision is with the author's own vocabulary, and "override it" is advice they have already taken. The message branches — default vs. configured — naming the right source and the remedy that still applies. F13 rejects knowingly-false advisories; leaving this text unchanged would have shipped one.

### 6.2 The flag clause (F15)

Design §5.2's trailing clause lands now that the identifier exists:

```
… Diverge in more than one element, mark the more destructive one requiresConfirmation,
or enable disambiguateSiblingTies on VoxrCommandRecogniser to ask the speaker instead.
```

Every remedy names something that exists in the shipped version, which is F15's whole point and the reason item 1 omitted the clause.

The message already names all members of an n-ary set (item 1's F3), and after F19 the runtime offers all of them too — so the remedy the message advertises is now true for every set shape it can describe, which it would not have been under a two-choice cap.

## 7. Test plan

PlayMode unless marked, per the project's bindings — `Tests~/Runtime` is PlayMode and most parser and command tests live there. Design §7 item 4 names the pending state machine's third reason as a PlayMode requirement specifically.

**Flag off — the G-6 control (F2).** Every fixture below, with the flag off, behaves exactly as at `337e758`. The existing suites are the bulk of this evidence: no expected value moves outside the Editor message tests.

**Seam A.** The runtime record is populated on a medial sibling tie and absent on a non-tie; **a three-way set records two rivals and a two-way set records one** (F19); a rival duplicating another's value is dropped from the offered set; the record **resets between extraction rounds** in a multi-command utterance — item 2's GAP-3, unpinned there and pinned here because this feature moves those locals; the Editor diagnostic's values are unchanged on every existing test; each rival's captured slots match its own match, not the winner's.

**A structural warning inherited from item 2's as-built section, and it governs seams A and C.** Item 2's requirements F7 called for two recogniser-level tests and *none were built* — every eager test called `TryEagerCommit` directly as a pure function, which structurally cannot observe what happens after a refusal, so the feature's central safety claim rested on inspection until the review caught it. The same trap is live here in sharper form: a parser-level test can assert that a rival was *recorded*, and prove nothing about whether the speaker is ever *asked*. **Every behavioural claim in seams B and C is tested through `VoxrCommandRecogniser`**, driving real events, not by calling the parser as a function.

**Seam B**, in `Tests~/Runtime/VoxrPendingCommandTests.cs` — the existing home for the reason × outcome matrix, whose 42 tests already cover the first two reasons (`RequiresConfirmation_Confirm_Fires`, `PartialMatch_TimeoutFireAsIs_FiresWithPartialSlots`, `ConfirmVocabulary_OnAPartialMatchPending_FiresAsIs_ByDesign`, …). The third reason extends that matrix rather than starting a new file: cancel under all three reasons; confirm under `AwaitingConfirmation` (fires, as today) and under `AwaitingDisambiguation` (no-op, pending stays live); a choice value fires the right intent; a choice value colliding with cancel cancels; timeout with `FireAsIs` under `AwaitingConfirmation` (fires) and under `AwaitingDisambiguation` (cancels); a disambiguation whose answer requires confirmation re-enters `AwaitingConfirmation`; an `AwaitingDisambiguation` pending never advances through `AdvanceSlotFill`.

The existing `_ByDesign` tests are the ones to read first: they pin behaviours that are deliberate rather than incidental, and a change here that flips one of them is a design contradiction, not a test to update.

**Seam C.** The `InjectText → assert 0 → FlushPendingBuffer → assert 1` template from `VoxrCommandRecogniserInjectionTests.cs` carries the flush-routing tests: ambiguous utterance raises `OnCommandPending` once and `OnCommandRecognised` zero times; the answer then raises `OnCommandConfirmed` + `OnCommandRecognised`; full re-utterance preempts; a second ambiguous utterance re-arms; `PendingAmbiguity` is null under a confirmation pending and populated under a disambiguation, with index 0 naming the command that fires with the flag off; the rival's definition being missing falls through to firing the winner.

**Seams D and E** are EditMode/Editor-message tests: the collision report against an overridden `cancelVocabulary`; a same-intent-only value producing no report while item 1's measured cross-intent collision still reports; #91's two fixtures; the flag clause; `SiblingSets_DemoGrammar_VolumeAndOrderAreStable` re-baselined only if the counts genuinely move, with the movement explained.

**The grammar-JSON pin (F7).** The generated grammar is compared byte-for-byte with the flag on and off. This exists because F7's claim — that discriminating values need no grammar addition, being pattern literals already — is *reasoned*, and `GetFollowUpGrammarWords` (`:919-934`) is not flag-aware. If the reasoning is wrong the flag changes what the decoder can hear in **both** configurations, which would break F2 in the one way the 699-corpus A/B could not localise. Plan validation found no step delivered it.

**Player-configuration compile check.** The A/B rig is the only build here that compiles without `UNITY_EDITOR` (Unity always defines it), and item 2 found a real player-build break that way. Every new `#if UNITY_EDITOR` boundary in seam A goes through it, with `Rig.csproj`'s `<NoWarn>` stripped so warnings are visible. The rig does **not** stage `VoxrCommandRecogniser.cs`, so it verifies compilation only — seams B and C are verified Unity-side.

## 8. Measurement (F18)

### As measured, 2026-08-16 — before `337e758`, after `d5aaa66`

Rig validated against the committed grammar pin in `NativeBridge~/harness/expectations.json` first, per the standing rule that no delta it reports is trusted until it reproduces the pin. It matched. Re-measured after the review fixes, not carried over from the pre-review run.

| # | Figure | Result |
|---|---|---|
| a | **699-corpus `Parse` A/B, flag off** | **699/699 rows identical** — no change to intents, slots or scores |
| — | Generated grammar | **identical, 1531 bytes**, both across the revision and across the flag |
| b | **Disambiguation-flow fixtures** | **29 tests** — 20 recogniser-level, 9 parser-level; **10 drive the full two-turn exchange**, covering medial *and* trailing discriminators |
| c | **Construction warning volume** | **unchanged: 2 on the demo grammar**, 0 collisions. Text changed by F15 only |
| d | **Per-parse cost, flag off** | **below this harness's noise floor** — see below. Allocation **exactly unchanged at 492.8 bytes/utterance** |

**(d), stated honestly rather than flatteringly.** Twelve interleaved rounds, 1,398,000 parses each:

| | before | after |
|---|---|---|
| mean | 19.362 µs | 19.442 µs |
| range | 18.92 – 20.05 | 19.14 – 20.11 |

Mean paired delta **+0.08 µs (+0.4%)** — but the paired delta **changes sign 4 times in 12** and spans −0.92 to +0.76 µs, a spread an order of magnitude larger than the mean. **This harness cannot resolve the delta and no point estimate is claimed.** What it does resolve exactly is allocation: identical to the byte, which is the claim that matters, since requirements §4.2(3)'s concern is added per-parse work and a preallocated record adds none.

The expected cost — one predictable test against a `readonly` field per non-`Better` candidate — is exactly the shape that should sit under this noise floor. An earlier run of the same measurement, before `EnsureSiblingLookup` moved off the parse path, showed +0.38 µs against much heavier machine drift; interleaving is what kept that from being reported as a real regression, and it is why the rounds alternate rather than running one side then the other.

**(c)'s real check is not the demo grammar.** Item 1 measured exactly one cancel collision across the entire corpus, and it lives in a `Tests~` fixture rather than in `DemoGrammar`. F13's narrowing did not silence it: `SiblingWarning_DiscriminatorCollidingWithCancel_IsAlsoReported` passes **unedited**. Volume across `Tests~` is pinned by the suites themselves — the ordered `LogAssert.Expect` queues fail on a warning that disappears, and `LogAssert.NoUnexpectedReceived()` fails on one that appears.

**What (a) is blind to, stated alongside it.** The 699 corpus is single-utterance and contains no follow-up answers, so it is structurally incapable of exercising a two-turn exchange. It is cited as a **control** for F2 — evidence that the flag-off path did not move — and as nothing else. The feature's own path is evidenced by (b).

---

Four figures, measured **interleaved**, in the PR body at open.

1. **699-corpus A/B, flag off** — zero difference in fired intents, slots and scores. Cited as a **control**: the corpus is single-utterance and contains no follow-up answers, so it is structurally blind to the entire disambiguation flow. Stating what it is blind to is part of the number.
2. **Purpose-built disambiguation-flow fixtures** — the two-turn exchange, covering trailing and medial discriminators: ambiguous utterance → prompt → one-word answer → correct intent. Item 2 deferred these here explicitly as *"the item that has a flow to exercise"*.
3. **Construction-time warning volume** across `Tests~` and the samples, re-measured because seams D and E change what is emitted. The check that matters: F13's narrowing did not silence the one real collision item 1 measured.
4. **Per-parse cost, flag off**, against the `337e758` baseline. Absolute figures, not a share of a noisy denominator — item 1's lesson.

Item 2's discipline carries: if two harnesses disagree, withdraw the number rather than defend it.

## 8b. What the review cycle changed (2026-08-16)

Eleven finder angles, three adversarial verifiers. Five defects survived verification; the human ruled fix-all. Recorded here because three of them are corrections to *this document*, not just to the code.

### The one the 517-test suite could not see

**The rival buffers were indexed by `_resultCount` while the buffer-full guard ran a round later.** `ParseInternal`'s `while (searchStart < tokens.Length)` loop tested `_resultCount >= _resultBuf.Length` at the *bottom* of the body, so a parse that filled the result buffer ran one more full `ci × pi × startIdx` scan with `_resultCount == _resultBuf.Length` — and the rival recorder writes `_tiedRivalBuf[_resultCount * MaxDisambiguationRivals + n]`, exactly one slab past the end. `IndexOutOfRangeException`, reproduced on a two-command sibling grammar with `"set alpha on set alpha on set alpha on"`.

Until this feature the round's tie state was two plain `int` locals, so the extra round was harmless and the guard's position did not matter. **Live in the Editor regardless of the flag**, because `_recordSiblingTies` is forced true there. The guard moved to the top of the loop.

### Four more

- **Step 7 read `_parser` live** where the loop deliberately snapshots `ResultBuffer` (`:698`) because it raises public events between iterations. A subscriber calling `Configure` from `OnCommandCancelled` nulls `_parser` and the next read throws — with **one** parsed result, not several. The parser is now snapshotted alongside its buffer and passed into `TryBuildAmbiguity`.
- **The debounce gate tested only the winner.** Every rival is a different intent by construction, so an answered choice could fire inside its own cooldown — nothing downstream re-checks (`InterpretResolution` calls `RecordFire`, never `IsOnCooldown`). Each choice is now gated. *The matching claim about `minConfidence` was **refuted**: a rival's span differs from the winner's only by trailing `[unk]`, which `ComputeConfidence` skips, so their confidences are equal by construction.*
- **`Truncated` under-reported.** §2.8's correction made the cap honest but two other paths still dropped an answerable rival in silence — the set-coherence rejection (§2.6) and a rival whose intent has no definition (§4.2). §2.6's own text promised the first was "reported the same way" and it was not. All three now set it.
- **The Editor diagnostic went null on a truncated sibling tie.** Deriving `TiedSiblingIntent` from the choice list meant a pair that ties provably but cannot be *named* stopped being reported — and §2.9's "keep their meaning" said otherwise. The diagnostic now reads a separate exemplar set on any sibling tie, offerable or not, restoring `337e758`'s answer exactly. Reproduced at both revisions before and after the fix.

### The standing hazard, now with a detector

Item 2 hit "a runtime path gated on a symbol every test defines" and so did this item (§2.2). The A/B rig gains **`PlayerCheck.cs`** — six assertions run in the only build here that compiles without `UNITY_EDITOR`: that the flag gates recording, that a flag-on player is *not* inert, that both discriminating words survive, that the overrun above stays fixed, and that the n-ary record and its cap behave. `-p:StartupObject=AbRig.PlayerCheck`, exit 0 is green. This is the check that would have caught §2.2 without a review.

### 8c. What the PR review found — including three defects §8b's own fixes introduced

`review-pr` ran the full profile on the open PR (12 angles, fresh eyes, inheriting nothing from the build session) and found fourteen items. Four were ruled fix-now; three of those were **caused by §8b**, which is the case for running both cycles rather than treating the first as sufficient.

**The rival cooldown gate is reverted.** §8b gated each choice on its own cooldown, to close a real hole — Step 7 tests only the winner. The gate was wrong twice over:

- On a two-way set it drops the only rival, `_choiceBuf.Count < 2` falls through, and **the winner fires**. The flag-on path silently degraded to the coin flip the feature exists to remove, with the truncation signal discarded on the way out. A test was written that *pinned this as intended* — the speaker says "set alpha **level** on", is misheard, and gets `set_mode`, a command they never uttered, precisely because they just used `level`.
- It could never have done its job anyway. An answer always waits out `bufferWindow` (the eager path is skipped while a pending is live), so the earliest a choice can fire is `now + 0.5s`, while exclusion requires `now − lastFire < 0.3s`. At shipped defaults the cooldown has **always** expired before the answer could fire.

Reverted, with the reasoning left at the site. The pre-existing confirmation path settles the principle: it enters pending after the debounce check and fires on confirm without re-checking. A deliberate answer to a question the recogniser asked is not the duplicate VOSK result `CommandDebouncer` exists to suppress.

**A truncated rival beside an answerable one now reports truncation.** §2.4's ruling — a truncated tie "records no rival and fires the winner exactly as it would with the flag off" — is true only when it is the *sole* tie. When another rival makes a question happen, the truncated one is an answer the speaker could have given, missing from the list, and saying it matches nothing. Reproduced: a question asked, `IsTruncated` false, and the Editor diagnostic naming the missing rival in the same parse. Tracked with a separate `sawUnnameableRival` local and resolved where the record is written, so arrival order does not change the answer.

**Rival-vs-rival now uses `IsAnswerableRival` too.** The dedup compared values only, so two patterns of ONE intent were offered as two choices — asking the speaker to choose between two spellings of the same command. Reproduced, including the cap harm: four sibling patterns of one intent take all four slots and squeeze out the only genuinely different alternative. Using the shared predicate here rather than a second hand-written comparison is what F13's "exactly one code site expresses *reachable as an answer*" actually asks for.

**`PlayerCheck.cs` is now staged.** §8b called it "the durable half", and it was neither in the PR (it lives under gitignored `Planning~/`) nor copied by `stage.sh` — the recorded 6/6 came from a hand-copy, so the guard against a failure class this design has hit twice would not have fired again. `stage.sh` stages it and `Rig.csproj` lists the entry points; `./stage.sh WORKTREE <out>` then runs it with no hand-copy. **It remains local-only, like the whole rig** — that is a property of `Planning~/` being gitignored, and is stated rather than papered over.

**All remaining items were then addressed on the human's ruling.** The substantive ones: the overrun crash gained a Unity test (it is not player-specific — the record write is not Editor-gated); `IsTruncated`'s public doc now describes what the flag means rather than one of its producers, and deliberately does not enumerate the causes, none of which the speaker can act on; the rival buffers are left **null** when nothing will record, so DR-7's "costs a flag-off player nothing" holds per rebuild as well as per parse; `TiedSiblingRecord.SetId` was write-only and is gone, the set id being round state rather than a property of the answer; the two-set test now asserts the `Truncated` its own comment promised; and `VoxrFollowUpVocabulary.Resolve` is the single definition of the empty-override fallback both the runtime matcher and the collision report use.

Issue **#98** carries the one item not fixed: with the flag on, a slot-only `RebuildParser` recomputes a commands-only analysis. Genuine, measured at ~1.2 ms, and *where the cache should live* is a design question rather than something to settle in a cleanup pass.

Ten items were reported and, before that ruling, left for the human: notably that the overrun crash §8b fixed is not player-specific and still has no Unity test (only `PlayerCheck` covers it), that `IsTruncated`'s public doc describes only one of its four producers, and that the PR body's "byte-identical with the flag clear" drops the Editor-message carve-out F2 actually grants.

### Cleanups worth naming

`TryFindSiblingRival`'s header said it "returns FALSE under expansion truncation" — it returns the bool with `setId` at −1, which is the whole point of the split (§2.4). `RivalIntent` carried a comment describing `BuildRivalCommand`. The buffer-allocation comment justified itself with a hot-path null check that does not exist. `OneValue` reimplemented `MatchPhraseAgainstTokens`, which the same class already had. Step 1's two-way `confirmed`/`cancelled` diagnostic label reported an answered disambiguation that re-entered confirmation as *"cancelled via vocabulary"*. And `EnsureSiblingLookup` moved from the parse path to the constructor: with the flag on, every parse asks, so deferring it only moved an `O(commands × forms)` build into the first utterance's latency window.

## 9. Open questions

### 9.1 `OnCommandConfirmed` on a disambiguation answer — OPEN

Carried from requirements §8.6. Resolving a choice goes through `PendingResolution.Confirmed`, so `OnCommandConfirmed` raises before `OnCommandRecognised`. Consistent, but that event is documented in terms of `requiresConfirmation` and an integrator may read it as "the user approved a destructive action". To G2.

### 9.2 A discriminating value that is also a whole command — OPEN

Carried from requirements §8.7. The choice check runs before the parse, so while a question is on the table the answer wins over a standalone command of the same word. Probably right; not discussed in canon. To be raised at G2 with a check of whether the shape occurs in the demo grammar.

### 9.3 The rival cap — STATED, not open

§2.6. `MaxDisambiguationRivals` bounds a preallocated buffer, so a set larger than the cap offers the first N choices with `Truncated` set. Reported rather than silent. The cap is picked against measured set sizes, not guessed.

## 10. Build plan

### Persisted plan (2026-08-16, revised after plan validation)

Four phases, each ending at a green `compile-check` and its own commit.

**The first draft had five phases and three blockers.** `plan-validator` found that the flush path never builds the sibling lookup in a player build (§2.2), that the pair test discards the very strings the choice vocabulary needs (§2.4), and that the shared reachability predicate as drafted would dereference an Editor-only field in a shipped game (§5.2). It also found the draft's phase 3 could not test its own behaviour, because the only producer of an `AwaitingDisambiguation` pending was in phase 4 — so B and C are now one phase. Fourteen further defects are folded into the steps below; the ones that change behaviour are marked **[V]**.

Line numbers are from the 2026-08-16 recon at `337e758` and are re-verified at each edit.

---

#### Phase 1 — Seam D + E's #91 half (Editor-only, no behaviour change)

Nothing here changes what fires. It lands the two deferred debts while the runtime is untouched, so a regression in phases 2–3 cannot be confused with one from here.

1. **`SiblingMember` gains `AuthoredDiscriminatorIndex`** (`:31-45`), computed at the member-creation site (`:1000-1002`) by §6.1's counting rule — the `(r+1)`-th non-optional element of the authored pattern. Private static helper beside `HasRequiredElementOutside` (`:880`).
2. **`BuildSiblingWarning` (`:1557`) renders adaptively** — members agreeing on the index keep today's exact wording; members disagreeing move the number onto each quoted pattern. Keep the `" (with its optional elements omitted)"` note.
3. **[V] `BuildCancelCollisionWarning` (`:1615-1627`) takes the member's authored index too**, replacing `set.DiscriminatorIndex + 1`. Same #91 defect, same fix, one message over.
4. **Parser constructor gains `string[] effectiveCancelVocabulary = null`** (`:297-300`). Optional, so `VoxrBatchTestRunner.cs:28` and `:63` are untouched. Both recogniser sites (`:199-202`, `:303-306`) pass `cancelVocabulary`.
5. **The collision test reads the effective array** (`:1257-1260`), falling back to `DefaultCancel` when null/empty, mirroring `PendingCommandHandler.cs:86-87`.
6. **[V] The collision message branches on default vs. configured** (§6.1). Its current text asserts "the **default** cancel vocabulary" and advises "override `cancelVocabulary`" — both false once the collision was found against an override the author already wrote.
7. **[V] The collision report narrows (F13)** via the shared `IsAnswerableRival(valueA, intentA, valueB, intentB)` predicate (§5.2) — **not** by resolving indices to `SiblingMember`s, which would dereference the Editor-only `_siblingSets` from a player. Replace the "left open for the later items to decide" comment (`:1242-1254`) with the decision and its reasoning.

**Tests:** #91's two fixtures (the issue's asymmetric pair; item 2 architecture §6.4's three-pattern set); the collision message under an overridden `cancelVocabulary`, asserting both the narrowing and the new wording; a same-intent-only value producing no report while item 1's measured cross-intent collision still reports.
**Verify:** `compile-check`, both suites. Existing sibling-message tests pass unedited except those #91 and F13 deliberately change.
**Note for phase 3:** the message fixtures written here are amended again in step 21 when the flag clause lands. Expected, not a surprise.

---

#### Phase 2 — the flag field + Seam A (runtime record, n-ary)

8. **`disambiguateSiblingTies`** — `[SerializeField] bool`, default `false`, tooltip per §4.1, declared with the pending block (`:104-116`); `internal` setter in the **end-of-file** cluster (`:937-943`), not the mid-file one at `:342-373`.
9. **Parser constructor gains `bool recordSiblingTies = false`**; `_recordSiblingTies = recordSiblingTies || UNITY_EDITOR` (§2.3). Both recogniser sites pass the flag.
10. **[V] `EnsureSiblingLookup()` moves out of `#if UNITY_EDITOR` on the flush path** (`:1676-1680`) to `if (_recordSiblingTies) EnsureSiblingLookup();`. **Without this the entire feature is inert in a shipped player** (§2.2) — and no Unity test can detect it, because Unity always defines `UNITY_EDITOR`.
11. **[V] `TryFindSiblingRival(ci1, pi1, ci2, pi2, out setId, out winnerValue, out rivalValue)`** (§2.4), with `AreSiblingRivals` becoming a wrapper so the eager call site's *condition* is unchanged (F17). Under expansion truncation it returns the over-approximating **bool** with `setId = -1` — the eager gate keeps refusing, the flush's recorder (which requires a set id) records nothing and fires the winner. *Revised at implementation: the draft's "returns false under truncation" would have deleted the eager refusal — see §2.4.*
12. **Preallocate in the constructor** beside `_resultBuf` (`:465`): the rival record array and the rival slot slab, sized from `_resultBuf.Length`, `MaxDisambiguationRivals` and `_maxSlotsPerPattern` (`:444-455`). **`MaxDisambiguationRivals = 4`, measured — see §2.8.**
13. **Rewrite the flush record** (`:1692-1774`, `:1813-1834`). Locals lose `#if UNITY_EDITOR`; `Better` resets the tie state (`:1737-1740`); the `Tied` branch appends when `_recordSiblingTies` and the pair yields a rival, capturing `(ci, pi)`, value, `EndIdx` and slots, and recording `SetId`/`WinnerValue`/`StartIdx` on the record. **[V] A rival is accepted only if it matches the record's already-fixed `SetId`** (§2.6) — otherwise a winner belonging to two sets mixes two different questions into one choice list. **Duplicate-valued rivals are dropped here**, and `Truncated` is set when a rival is *rejected* for want of room (§2.8's as-built correction).
14. **`ParseDiagnosticEntry` is populated from rival 0** (`:1823-1833`) — identical values to today.
15. **`internal TiedSiblingRecord[] TiedSiblingBuffer`** and a rival accessor, following `ResultBuffer` (`:1846`).
16. **[V] The construction warning reports an over-cap set** (§2.8), which is what actually discharges F19's "never silently truncated" — a boolean nothing reads is the silent cap wearing a flag.
17. **Comment work:** correct item 2's "must stay in step" comment (`:1758-1764`) per §2.9; add the issue #95 reference F5 asks for at the record's declaration.

**Tests:** §7's seam A list — n-ary recording, the multi-set coherence case, duplicate-value dedup, GAP-3's multi-round reset, the five Editor diagnostic tests unedited, per-rival slot capture.
**Verify:** `compile-check`, both suites. **Then the A/B rig in the player configuration** with `Rig.csproj`'s `<NoWarn>` stripped — this phase moves `#if UNITY_EDITOR` boundaries, and item 2 found a real player-only break doing exactly that.

---

#### Phase 3 — Seams B + C (pending machinery, wiring, public surface)

**Merged, because they cannot be verified apart.** §7 requires every behavioural claim to be tested through `VoxrCommandRecogniser` rather than by calling the parser as a function — item 2's review found its two most important tests were never built for exactly that reason. But the only producer of an `AwaitingDisambiguation` pending is the Step 7 routing, so a standalone seam-B phase could only test itself with the pure-function unit test the rule forbids. `ForceSetForTest` (`:319`) does not rescue it: `_pending` is private and only the read-only `EditorPendingCommand` getter (`:966`) is exposed.

18. **`VoxrPendingReason` gains `AwaitingDisambiguation`, appended** (`VoxrPendingCommand.cs:17-21`); **`VoxrPendingCommand` gains `Choices` / `ChoiceValues` / `ChoiceDefinitions`** (`:23-30`).
19. **[V] `EnterPending` gains the three arrays as optional trailing parameters** (`:59-63`, initialiser `:68-75`); existing call sites unchanged. **[V] `Complete` gains a `resolvedDefinition` parameter** (§3.2) — it currently reads `pending.Definition`, the *winner's*, so a chosen rival's `requiresConfirmation` would be read off the wrong command. **[V] `AdvanceSlotFill` (`:197-204`) carries the arrays forward**, a guard for a state §3.5 argues is unreachable.
20. **`TryHandleConfirmCancel` (`:80-102`) gains the choice arm** (§3.2); **`HandleTimeout` (`:236-245`) degrades** (§3.3); **[V] `Editor/VoxrDebugWindow.cs:336-338` becomes a three-way branch** — the ternary would otherwise label a disambiguation "Awaiting confirmation".
21. **`VoxrPendingAmbiguity`** public readonly struct with `Choices`, `DiscriminatingValues`, `IsTruncated` (§4.4), with the coding-conventions header; **`public VoxrPendingAmbiguity? PendingAmbiguity`** beside `PendingCommand` (`:180`).
22. **The flush routing** (§4.2) — after debounce (`:806-815`), before `RequiresConfirmation` (`:818`). Builds `1 + RivalCount` choices with value-dedup, resolves each definition via `_setManager.TryLookupCommand` and drops any that fails, falls through to firing the winner if fewer than two choices remain. **[V] Sets `anyThresholdFiltered`** so the utterance does not also raise `OnUnrecognisedSpeech` (`:845-853`). **[V] Adds an `#if UNITY_EDITOR` `attempts.Add`**, as every sibling branch does.
23. **F15 — the sibling warning names the flag** (`BuildSiblingWarning`), amending the phase-1 message tests.
24. **`_cancelPendingOnRelease`'s tooltip** (`VoxrPushToTalkController.cs:33-35`) covers all three reasons (§4.5).
25. **File the `OnUnrecognisedSpeech` observation as an issue** — the existing `RequiresConfirmation` arm (`:818-831`) has the same shape and raises `OnUnrecognisedSpeech` today. Pre-existing, on a path this feature does not own; filed, not fixed.

**Note on F16:** the plan's earlier step here was a no-op. The source comment at `:3185-3187` **already** says *"What refusing buys is that the decision moves to the flush, where … item 3 can ASK which intent was meant instead of guessing."* F16's source clause is satisfied at HEAD; only its product-doc half remains, and that is post-G2.

**Tests:** §7's seam B matrix and seam C list, **all driven through `VoxrCommandRecogniser`** with the `InjectText → assert → FlushPendingBuffer → assert` idiom. Includes the extra `OnCommandCancelled` an ambiguous re-utterance raises (§4.3).
**Verify:** `compile-check`, both suites, flag on and off.

---

#### Phase 4 — verification and measurement

26. **[V] Pin the grammar JSON across the flag** (F7 / requirements §4.2(2)) — byte-identical with `disambiguateSiblingTies` on and off. No step delivered this before; the claim that discriminating values need no grammar addition is *reasoned*, and F7 says it is pinned.
27. Full EditMode + PlayMode run, both platforms, **re-measuring** the baseline rather than assuming `129/129` and `479/479`.
28. The four F18 figures (§8), measured **interleaved**.
29. `review-cycle`, then PR, then `review-pr` at the **full** profile.

---

**Scope guards, restated because they are the easy things to drift into:** no eager-path *condition* changes (F17 — `TryFindSiblingRival` is a refactor beneath an unchanged condition); no `#70` guard change; no scoring arithmetic; no item 4 product docs; `sibling-tie-disambiguation.md` is not edited (F16); issue #93 untouched; issue #95 filed, not built; the `OnUnrecognisedSpeech`-on-confirmation defect filed, not fixed.

**Test-ordering trap, from plan validation:** `cancelVocabulary` and `disambiguateSiblingTies` are both frozen into the parser at `Configure`/`RebuildParser` time, so tests must set them **before** `Configure`. Getting it wrong is invisible for the flag, because `_recordSiblingTies` is `|| UNITY_EDITOR` and every Unity test runs in the Editor.

## Related

- Requirements: `Planning~/features/disambiguation-pending/requirements.md`
- Locked design: `Planning~/design-docs/sibling-tie-disambiguation.md` §2.5, §2.6, §5.4–§5.7, §9 row 3, DR-4, DR-6, DR-7
- Predecessors: `Planning~/features/sibling-set-detection/architecture.md` §4.1 (the deferred vocabulary limitation, discharged in seam D), `Planning~/features/tie-aware-selection/architecture.md` §6.4, §11.2
- Closes: issues #74, #91 · Filed: issue #95 · Deferred: issue #93
