---
type: architecture
feature: sibling-set-detection
topic: sibling-tie-disambiguation
status: draft
updated: 2026-08-15
sources: [Planning~/features/sibling-set-detection/requirements.md, Planning~/design-docs/sibling-tie-disambiguation.md]
---

# Sibling Set Detection — Architecture

Drafted as the eventual product page for this behaviour, per the workflow. Line numbers are against `main` at `ef84690` and were verified by recon on 2026-08-15.

## 1. Shape of the change

One new pure function and one new Editor-only consumer of it, sitting beside the three construction-time scans the parser already runs. Nothing else in the parser is touched.

```
VoxrCommandParser ctor (:393-395)
    RunValidationWarnings(slots)                 ← existing, untouched
    WarnOnDroppableRequiredLiteral(commands)     ← existing, untouched (F9)
    WarnOnExcessiveOptionalExpansion(commands)   ← existing, untouched
    WarnOnSiblingDiscriminator(commands)         ← NEW, [Conditional("UNITY_EDITOR")]
                │
                └─ FindSiblingSets(commands) → List<SiblingSet>   ← NEW, the DR-2 primitive
                       │
                       ├─ BuildSiblingWarning(set)          → the §5.2 message
                       └─ BuildCancelCollisionWarning(...)  → the §5.5 report
```

The new call is appended **last**, after `WarnOnExcessiveOptionalExpansion`. An earlier draft placed it between the two existing scans so the pattern-pair scans would read together; plan validation found that `ConfigureForUnanalysableGrammar` (`Tests~/Runtime/VoxrCommandRecogniserInjectionTests.cs:722-742`) queues **ordered** log expectations with a comment stating the constructor's scan order is load-bearing. Appending cannot disturb an ordered queue; inserting can. Placement is otherwise cosmetic — the scans are independent — so the safe choice costs nothing.

## 2. The primitive (DR-2, §5.1)

### 2.1 Contract

```csharp
// The sole definition of "sibling" in the codebase. Pure: no logging, no state, no
// Unity API. Item 2 promotes the call site to runtime; the relation does not move.
internal static List<SiblingSet> FindSiblingSets(VoxrCommandDefinition[] commands)
```

`internal`, not `private`, for two reasons: `Runtime/AssemblyInfo.cs:9-11` grants `InternalsVisibleTo` the two test assemblies, so tests can assert the **predicate** directly rather than only through log text; and item 2 calls it from selection without redefining anything (DR-2).

It is **not** `[Conditional]`. The `[Conditional]` sits on the *caller*, so in a player build the call disappears entirely and the function is never invoked — F4's "zero cost in a built player" is satisfied by the call site, not by excluding the code. Leaving the function itself unconditional is what lets item 2 call it from a runtime path without moving it.

### 2.2 Data contract

```csharp
internal readonly struct SiblingMember
{
    public readonly int    CommandIndex;   // index into commands[]
    public readonly int    PatternIndex;   // index into commands[ci].Patterns
    public readonly string Intent;
    public readonly string Value;          // the raw discriminating literal
}

internal readonly struct SiblingSet
{
    public readonly int      DiscriminatorIndex;  // 0-based position within the form
    public readonly string[] Frame;               // normalized form; Frame[DiscriminatorIndex] is null
    public readonly SiblingMember[] Members;      // ≥2, in registration order
}
```

`CommandIndex`/`PatternIndex` are carried because they are what makes this *the shared primitive* rather than a warning-local helper: §5.1 specifies selection consulting "a precomputed lookup keyed by `(commandIdx, patternIdx)`", and item 2 needs exactly that key. Without them the type would have to change in item 2, which is what DR-2 exists to prevent.

`Members` rather than parallel `string[] Intents` / `string[] Values` arrays keeps a member's intent and its discriminating value from drifting apart when the set is filtered or reordered.

### 2.3 The predicate

Per DR-1, two expanded forms are siblings when they are **equal length**, **element-wise equal at every position but one**, and that one position holds a **required literal in both**.

Two element tests are needed, and they are deliberately different:

```csharp
// Frame equality — decoration-insensitive. See requirements §4.2.
//   "?to"     ≡ "to"        (an included optional literal matches the same token)
//   "{?ship}" ≡ "{ship}"    (an included optional slot matches the same value)
static string NormalizeElement(string element)
{
    if (IsOptionalLiteral(element))                       // "?word"  → "word"
        return element.Substring(1);
    string slot = ExtractSlotName(element);               // handles both {n} and {?n}
    return slot != null ? "{" + slot + "}" : element;
}

// Discriminator eligibility — decoration-SENSITIVE, on the raw element.
// Required literal only: not a slot of either kind, not an optional literal.
static bool IsRequiredLiteral(string element)
    => !string.IsNullOrEmpty(element)
       && element[0] != '?'
       && ExtractSlotName(element) == null;
```

The obvious form — `ExtractSlotName(e) == null && !IsOptionalLiteral(e)` — has two holes plan validation found. It admits `""`, which is reachable from the inspector and would then be treated as a discriminating value; and it admits the malformed `"?{ship}"`, which `ExtractSlotName` rejects (it wants `[0]=='{'`) and `IsOptionalLiteral` also rejects (it wants `[1]!='{'`). Testing `element[0] != '?'` covers optional literals, optional slots written the wrong way round, and nothing else.

`ExtractSlotName` (`:1805`) already strips a `?` inside the braces, so it returns `"ship"` for both `{ship}` and `{?ship}` and `null` for any non-slot element — normalizing an optional slot needs no separate `IsOptionalSlot` call. `IsOptionalLiteral` (`:1823`) is `element[0]=='?' && element[1]!='{'`.

**Why the asymmetry.** `ExpandOptionals` (`:2170`) copies the raw element verbatim when an optional is included — it does not strip the `?`. So without normalization, `["switch","?to","weapons"]` and `["switch","to","navigation"]` differ at two positions and the genuine hazard is missed, purely because of an authoring choice about an unrelated element. At the discriminator the `?` genuinely matters: an optional discriminating word means the author already declared the pattern matches with or without it, which makes the pair duplicates rather than siblings (F8, F14). Requirements §4.2 carries the full argument.

### 2.4 Algorithm — bucket by wildcarded frame

Pairwise comparison would be `O(forms² × L)` and would then need a second pass to merge pairs into n-ary sets (F3). Bucketing does both at once:

```
for each (ci, pi) in registration order:
    for each form in WarningForms(patterns[pi]):
        for each position d in form:
            if not IsRequiredLiteral(form[d]): continue
            key ← join(NormalizeElement(form[i]) for i≠d, with d held as a wildcard)
            bucket[key].add(SiblingMember(ci, pi, intent, form[d]))
```

Two forms land in the same bucket exactly when they agree on all positions but `d` and both carry a required literal at `d` — which is the sibling relation, and the bucket holds all `n` members directly. Wildcarding `d` on one side cannot admit a bad pair: every member is filtered through `IsRequiredLiteral(form[d])` before insertion, and a shared key forces a shared `d`.

**Cost: `O(totalForms × L²)`.** *Cheaper* than the existing scan's `O(patterns² × forms²)`, not merely the same order as §5.1 estimated — the design assumed a pairwise scan. It does allocate `totalForms × L` keys per construction where the existing scan allocates none, which is exactly what F11(c) exists to catch.

### 2.5 Key encoding — load-bearing

Join normalized elements with `''`, emitting `''` at position `d`. Neither character can occur in an authored element.

This is not a detail. A printable separator buckets `["switch to","weapons"]` at `d=1` together with `["switch","to","weapons"]` at `d=2` — both key to `switch to *` — pairing forms of **different lengths**, an outright DR-1 violation. Multi-word literal elements never match at runtime (`:1287-1288` compares one element to one token), so this is degenerate authoring, but the claim that the bucket relation *is* the sibling relation depends on the encoding, so the encoding is stated rather than left to the implementer.

### 2.6 Emission filter — four gates, in order

A single `≥2 distinct values` test is wrong in three separate ways, all found by plan validation.

1. **Dedup members by `(CommandIndex, PatternIndex)`, keeping the first.** An earlier draft claimed a pattern cannot be its own sibling, on the grounds that all its forms share every required element. **That is false under normalization**: `["?a","a","b","?b","c"]` yields the equal-length forms `["?a","a","b","c"]` and `["a","b","?b","c"]`, which normalize to `[a,a,b,c]` and `[a,b,b,c]` — differing at exactly position 1, where both raw elements are required literals. One pattern reported as its own sibling.
2. **Then keep one member per distinct `Value`.** Live instance in `Tests~/Runtime/DemoGrammar.cs:117-124`: `set_heading` carries `["orient","heading","{heading}"]` and `["orient","heading","{heading}","?mark","{?elevation}"]`, whose fully-omitted form equals the first, alongside a `set …` variant. Bucket `* heading {heading}` therefore collects three members and renders as `("orient" vs "orient" vs "set")`. This gate also discharges **F8**: two identical patterns collapse to a single member and are never reported.
3. **Require ≥2 members remaining** after gates 1 and 2.
4. **Require the frame to hold ≥1 element** — ruled by the human, 2026-08-15. A single-element pair such as `cease_fire ["disengage"]` / `resume_fire ["reengage"]` (`DemoGrammar.cs:65-79`) satisfies DR-1, but its frame is empty: with the word dropped **nothing** matches, both candidates score 0, and `IsBetterCandidate` rejects them at `Score <= 0f` (`:1114`). There is no tie, so §5.2's message would assert a harm that cannot occur. The relation stays exactly as DR-1 locks it; this scopes the **warning** to cases where the stated harm is reachable — the same move as the adaptive same-intent message in §3, and the false-positive discipline §5.2 names as this half's main hazard. Suppressed sets are counted separately in the F11 volume report so the suppression is visible rather than hidden.

**Determinism** (a non-functional requirement) sits across all four: `Dictionary` iteration order is unspecified, so buckets are emitted by walking a `List<string>` of keys in **first-seen** order, with the dictionary used only for lookup. Otherwise warning order varies between runs and the message tests go flaky.

**Inherited recall limit.** Past `MaxWarningExpansion = 6` optionals, `WarningForms` (`:624-630`) returns the raw decorated pattern rather than its expansions, so such a pattern is only ever compared in its all-optionals-present reading. Inherited from the existing scan, not introduced here, but it bounds this scan's recall too.

## 3. The warning (§5.2, F4–F6)

```csharp
[System.Diagnostics.Conditional("UNITY_EDITOR")]
static void WarnOnSiblingDiscriminator(VoxrCommandDefinition[] commands)
```

Matches `WarnOnDroppableRequiredLiteral`'s shape exactly, singular name included: `[Conditional]` rather than `#if`, so the call site and the helpers stay one piece of code and the tests that `LogAssert.Expect` the message pin it in editor Play Mode, where this package's Runtime suite runs (`VoxrCommandParser.cs:530-532`).

**Dedup is real work — bucketing alone is not enough.** An earlier draft claimed dedup was structural. It is not: bucketing deduplicates form-pairs *within one frame*, but the same hazard surfaces from **different frames of the same pattern pair**.

```
mode_weapons    : ["?please","switch","to","weapons"]
mode_navigation : ["?please","switch","to","navigation"]
```

`ExpandOptionals` yields two forms each, filling both `please switch to *` and `switch to *` with the same members and the same values — **two warnings for one hazard**, which is exactly what F5's acceptance check forbids.

**The dedup lives in `FindSiblingSets`, not in the warning** (revised from the plan): a sibling set is a *hazard*, not a frame, so every consumer should receive one set per hazard rather than each having to collapse them. `IndexOfSameMembers` matches on the member list and the survivor is **the set with the longest frame**.

Longest, not first-seen, and the reason is a message defect the first implementation actually produced. With first-seen, the `?please` pair above reported *element 3* — the discriminator's position in the form that silently dropped the optional — while quoting the pattern **as authored**, where the discriminating word is element 4. An author counting elements in their own pattern would land on `to` rather than on the word the warning is about. Preferring the longest frame makes the quoted pattern and the element number agree whenever any expansion of the pattern is a sibling at full length. `SiblingWarning_SameHazardFromTwoExpansions_WarnsOnce` pins both the count and which frame survives.

**Message.** Per the human's ruling (requirements §8.1), the design's trailing clause *"or enable ambiguity disambiguation (`<flag name>`)"* is omitted; item 3 adds it. The message names only remedies that exist in the shipped version.

**Patterns are quoted as authored**, following `BuildDroppableLiteralWarning` (`:637-644`) and its `"(with its optional elements omitted)"` note. Naming only the normalized frame fails F6 and the "actionable and self-contained" non-functional requirement in two ways: it identifies no pattern, so an author with three `set_heading` patterns cannot tell which two are implicated; and normalization means `["switch","?to","weapons"]` renders as `switch to …`, which cannot be found by searching the asset for the text the author actually wrote.

Cross-intent — the real hazard, and §5.2's own wording:

```
[VoxrCommandParser] Intents 'mode_weapons' and 'mode_navigation' have patterns
"switch to weapons" and "switch to navigation" that differ only at element 3
("weapons" vs "navigation"). If that word is dropped, both patterns match the remainder
equally — same score, same consumed span, same literal count — and selection falls through
to registration order, so the wrong intent can fire. Diverge earlier, or mark the more
destructive one requiresConfirmation.
```

Element position is **1-based**, matching §5.2's "element 3" for index 2.

This is the only message form. A same-intent variant was written and then removed once §7.1 was ruled: same-intent sets no longer warn, so the branch was unreachable.

**Intents are named once each, patterns once per pattern.** One intent can contribute several patterns to a set — the demo grammar's `cease fire` / `hold fire` both tie with `resume fire` — and naming the intent per member printed `Intents 'cease_fire', 'cease_fire' and 'resume_fire'`. Deduplicating the intent list for display gives `Intents 'cease_fire' and 'resume_fire' have patterns "cease fire", "hold fire" and "resume fire" …`, which is what `SiblingWarning_OneIntentContributingTwoPatterns_NamesItOnce` pins.

`BuildDroppableLiteralWarning` (`:649-653`) already branches its text on whether two intents are equal, so an adaptive message is the established idiom here, not a new one.

For `n > 2` members the intent list and value list render as `'a', 'b' and 'c'` / `"x", "y" or "z"`, so a three-way `on`/`off`/`standby` set produces one message naming all three (F3).

## 4. The cancel-collision report (§5.5, F7)

```csharp
static bool CollidesWithCancelVocabulary(string value)
    => Array.IndexOf(VoxrFollowUpVocabulary.DefaultCancel, value) >= 0;
```

`VoxrFollowUpVocabulary` is `internal static` in `Runtime/Commands/VoxrPendingCommand.cs:32`, in `namespace VoXR.Commands` and the same `Jinwoo1601.VoXR.Runtime` assembly as the parser — reachable with no new `using` and no asmdef change (recon B6).

Ordinal equality is sufficient. The reachable collisions are the single-word entries — `cancel`, `abort`, `negative`. An authored element `"belay that"` *would* also match `Array.IndexOf`, since a pattern element is not necessarily a single word; it is harmless only because such an element can never match speech in the first place (`:1287-1288` compares one element against one token). Recorded because the tempting shorter argument — "a discriminating value is one element, so multi-word entries can never match" — is false, and a future reader checking it would find it so.

```
[VoxrCommandParser] Intents 'set_positive' and 'set_negative' differ only at element 2,
and the discriminating value "negative" is also in the default cancel vocabulary.
Follow-up handling checks cancel before anything else (PendingCommandHandler), so a
speaker answering with that word would cancel rather than select it. Rename the literal,
or override cancelVocabulary on VoxrCommandRecogniser.
```

Both remedies name things that exist today (`cancelVocabulary` is `VoxrCommandRecogniser.cs:116`), satisfying F6.

### 4.1 A stated limitation: the report sees only the default vocabulary

`TryHandleConfirmCancel` prefers a configured `cancelVocabulary` over the default when non-empty (`PendingCommandHandler.cs:86-88`), but the `VoxrCommandParser` constructor takes only `(slots, commands, coverageWeight, additionalGrammarWords)` — it **cannot** see a `VoxrCommandRecogniser`'s override.

So the report is **incomplete by construction**: it will miss a collision against a customised vocabulary, and it may report a collision an override has already resolved. Both directions are wrong in the safe direction — advisory noise or a missed advisory, never a behaviour change.

Closing it needs a new constructor parameter, which F13 forbids in this feature. It is also premature: the collision has **no consequence at all** until item 3 ships the disambiguation path, and item 3 is where the effective vocabulary must be plumbed anyway. Deferred there deliberately, named at G2, and carried into item 3's requirements rather than left in a doc nobody re-reads.

## 5. The §2.8 test (F10) — the load-bearing one

§2.8 claims a *medial* discriminator slips every `TryEagerCommit` guard and the gate commits the wrong sibling early. **Reasoned from source, never observed.** DR-5 exists solely because of it, and a refutation reopens the design (requirements §8.2).

### 5.1 The guards as they actually are

Recon corrected the order §2.8 assumes. The real sequence in `TryEagerCommit` (`:1964`):

| # | Line | Guard | §2.8's prediction on `set alpha on` |
|---|---|---|---|
| 1 | `:1967` | tokens null/empty | passes |
| 2 | `:2021` | `bestScore < minScore` | passes — the real score is `3.0/4.0 = 0.75` (see below) |
| 3 | `:2045` | `bestMissedRequiredSlot` (#66) | false — the discriminator is a **literal**, not a slot |
| 4 | `:2066` | `bestHasUnmatchedRequiredTail` (#70) | false — the miss is **medial**, so the later `"on"` resets `requiredAfterLastMatch` |
| 5 | `:2087` | `bestStartIdx != firstRecognisedIdx \|\| bestEndIdx != tokens.Length` | passes — the match spans the buffer |
| 6 | `:2091` | confidence | passes — `null` confidence returns `-1f` from `ComputeConfidence` (`:1802`) and bypasses the gate |
| 7 | `:2103` | `_canCommitEarly == null` → `HoldExtendable` | **not considered by §2.8** |
| 8 | `:2108` | `CanCommitEarly(ci, pi) ? Commit : HoldExtendable` | **not considered by §2.8** |

**§2.8 stops at guard 6.** Guards 7 and 8 are the issue #32/#44 extendability precompute, and they are the reason this test can genuinely refute rather than merely confirm: the verdict is `Commit` **only if** `CanCommitEarly` returns true. Two equal-length siblings do not extend one another, so it plausibly does — but that is exactly the kind of "plausibly" that put §2.8 in this position, and it is not being reasoned about a second time.

`requiredAfterLastMatch` is also not local to `TryEagerCommit`: it lives in the scoring loop at `:1203` and reaches the gate as `HasUnmatchedRequiredTail = requiredAfterLastMatch > 0` (`:1353`). §2.8's table reads as though the gate computes it. Immaterial to the claim, recorded so the next reader is not misled.

### 5.2 Fixture and expected outcomes

Grammar — the §2.8 example verbatim, two intents, a `ship` slot carrying `alpha`:

```
set_mode  : ["set", "{ship}", "mode",  "on"]
set_level : ["set", "{ship}", "level", "on"]
```

Utterance `set alpha on` — the discriminator elided. Driven exactly as `Tests~/Runtime/VoxrEagerCommitTests.cs` already drives the gate: `parser.TryEagerCommit(Tok("set alpha on"), null, 0.6f, 0.4f)`, directly, as a pure function. No `VoxrCommandRecogniser`, no clock, no coroutine (requirements §7).

| Observed verdict | Reading | Consequence |
|---|---|---|
| `Commit` | **§2.8 CONFIRMED.** The gate fires early on evidence that cannot distinguish the siblings. | DR-5 keeps its motivation; the test lands as the pin item 2 builds on. |
| `HoldExtendable` | **§2.8 partially refuted.** The gate does not commit — it holds for the shorter window, which is already most of what DR-5 asks for. | DR-5's value shrinks to the difference between a short hold and a refusal. A design question, not a silent adjustment. |
| `None` | **§2.8 REFUTED.** Some guard catches it after all. | DR-5 loses its motivation. Work stops, the human is told, the design reopens on a new branch. |

The nearest existing test, `TryEagerCommit_MissedRequiredLiteral_AllSlotsFilled_StillCommits` (`VoxrEagerCommitTests.cs:904`), pins `Commit` for a medial drop — but on a **single** command, with no sibling competing. It is the closest analog and it is **not** a substitute: adding a second same-length pattern is precisely what could change `CanCommitEarly`'s answer.

### 5.3 The second half — "the *wrong* sibling"

`TryEagerCommit` returns a verdict, not a command, so it cannot by itself show that the *wrong* sibling fires. §2.3's invariant is that the eager verdict names the pattern the subsequent flush will fire, so the claim needs two tests, and they establish different halves:

1. **The gate lets it through early** — §5.2 above.
2. **The flush picks by registration order** — `Parse("set alpha on")` returns `set_mode` (first-registered), and the same grammar with the registration order reversed returns `set_level` on the same utterance. The second assertion is what makes this a *coin flip* rather than a defensible preference: the only thing that changed was declaration order.

Test 2 asserts today's behaviour and changes none of it (F12). It is also the regression pin item 3 will invert.

**§2.8's arithmetic is wrong, and reported rather than asserted.** The design predicts `2.0/2.5 = 0.8`. Derived from `TryMatchScored` (`:1212-1345`) the real value is **`3.0/4.0 = 0.75`**: `"set"` matches (+1, den 1), `{ship}`→`alpha` matches (+1, den 2), `"mode"` misses at `RequiredLiteralMissPenalty = 0f` (`:70`, den 3), `"on"` matches (+1, den 4), and coverage is 0 because nothing is skipped and `consumedEndIdx == tokens.Length`. The design's `0.8` is the constant belonging to the *five*-element analog at `VoxrEagerCommitTests.cs:904`.

Both clear the `0.6` default, so the guard-2 outcome §2.8 predicts is unaffected and this is not a design contradiction — it is an illustrative number carried from the wrong example. The test reports the observed score rather than asserting it; the claim under test is the verdict.

**Source predicts `Commit`.** Guard 8 resolves through `flags[pi] = IsTerminalPattern && !IsPrefixOfAnyOtherPattern` (`:2150-2151`): `"on"` is a required literal so terminal holds (`:2208-2210`), and `IsPrefixOfAnyOtherPattern` (`:2246-2265`) fails its length test at 4-vs-4 while `BoundaryWordPrefixHazard` bails at `SharedLiteralPrefixLen(p,q) != k` (`:2284`) because the run breaks at `{ship}` (i=1) with k=3. The run still happens — a prediction reasoned from source is precisely what put §2.8 in this position.

## 6. Test plan

Placement follows the existing suites (recon D11/D12) — both files are PlayMode:

**`Tests~/Runtime/VoxrCommandParserTests.cs`** — beside the existing construction-warning tests:

| Test | Pins |
|---|---|
| `SiblingSet_TrailingDiscriminator_Warns` | F1 — the #74 shape |
| `SiblingSet_MedialDiscriminator_Warns` | F1, DR-1 — the case a trailing-only definition would miss |
| `SiblingSet_ThreeWay_WarnsOnce` | F3, F5 — one message, three intents, three values |
| `SiblingSet_UnequalLength_DoesNotWarn` | F1 |
| `SiblingSet_TwoDifferences_DoesNotWarn` | F1 |
| `SiblingSet_DifferingAtASlot_DoesNotWarn` | F1 |
| `SiblingSet_DifferingAtAnOptionalLiteral_DoesNotWarn` | F14 |
| `SiblingSet_IdenticalPatterns_DoNotWarn` | F8 — duplicates differ at *zero* positions |
| `SiblingSet_OptionalDecorationInTheFrame_StillWarns` | F14 — `?to` vs `to` |
| `SiblingSet_SameIntent_UsesTheSameIntentMessage` | §3, §7.1 |
| `SiblingSet_CancelCollision_IsReported` | F7 |
| `SiblingSet_NoCancelCollision_IsNotReported` | F7 |
| `SiblingSet_DoesNotDisturbTheDroppableLiteralWarning` | F9 |

Several assert on the predicate through `FindSiblingSets` directly rather than through log text (§2.1) — set count, discriminator index, member intents and values. Log-text assertions are reserved for the message tests, so a wording change does not break thirteen tests.

**`Tests~/Runtime/VoxrEagerCommitTests.cs`** — the §2.8 pair from §5.2/§5.3.

**Existing tests (F11b, requirements §4.3).** Every grammar in the suite that carries a sibling shape starts emitting a new warning, and `NonHazardPatternShapes_DoNotWarn` (`:2126`) asserts absence via `LogAssert.NoUnexpectedReceived()`. Amendments are legitimate where the grammar genuinely carries the shape and are justified individually; weakening an absence assertion to make a failure go away is a defect, not a fix.

## 7. Open questions

### 7.1 Same-intent sibling sets — RESOLVED 2026-08-15 on the F11 measurement

DR-1 and §5.2 do not mention intent: the relation is over pattern *forms*, and §5.2 says warn on "every sibling set found". But the harm §1 describes is **"the wrong intent can fire"**, and within a single intent that harm does not occur — the same command is dispatched whichever pattern wins, and the "tie" is between two phrasings the author deliberately made equivalent.

This was left open deliberately, to be settled by F11's evidence rather than guessed at. The evidence, measured over `DemoGrammar.cs` — **11 sibling sets, 5 cross-intent and 6 same-intent** — is one-sided:

| Cross-intent (5) — all genuine | Same-intent (6) — all synonym authoring |
|---|---|
| `cease fire` / `resume fire` | `resume fire` / `resume firing` |
| `stop firing` / `resume firing` | `set` / `orient` `heading {heading}` |
| `enable all` / `disable all` | `disable all` / `disable commands` |
| `weapons mode` / `navigation mode` | `fall back` / `pull back from target` |
| `switch to weapons` / `switch to navigation` | …and two more of the same shape |

**Three of the five cross-intent sets name opposite actions** (`cease`/`resume`, `stop`/`resume`, `enable`/`disable`) — firing the wrong one is the worst outcome the design contemplates. **Every same-intent set is a synonym pair** with no remedy short of using fewer synonyms.

**Ruled by the human, 2026-08-15: suppress same-intent sets from the warning; DR-7's default-on stands.** Warning volume on the package's own sample grammar drops from 11 to 5, with a 0% false-positive rate. Reporting the other six would have made this scan noise on the shipped sample — precisely the failure #81 just spent a feature reversing for `WarnOnDroppableRequiredLiteral`.

Both counts and the emission order are pinned by `SiblingSets_DemoGrammar_VolumeAndOrderAreStable`, which reads `DemoGrammar.AllCommands()` directly. That test exists because the first version of this measurement was taken from a hand transcription of the grammar and was wrong — see requirements §8.3.

**The filter lives in `WarnOnSiblingDiscriminator`, not in `FindSiblingSets`.** The relation stays exactly as DR-1 defines it and the primitive still returns same-intent sets, so items 2–3 can decide independently whether a same-intent tie is worth acting on. Only the author-facing warning is narrowed. `SiblingSets_SameIntent_IsDetectedButNotWarnedAbout` pins both halves.

**Consequence for §3:** the adaptive same-intent message variant is now unreachable and has been removed. A single cross-intent message remains.

### 7.2 Whether `FindSiblingSets` should be stored rather than recomputed

Settled in requirements §4.1: invoked Editor-only here, promoted to a stored runtime lookup by item 2, when there is finally a consumer. Repeated here because a reviewer meeting `internal static` on a function with one `[Conditional]` caller will reasonably ask why it is not `private`, and §2.1 is the answer.

### 7.4 The reachability gate — CLOSED 2026-08-16, found by the PR #92 review and fixed on the human's ruling

**Resolved.** The three findings below were confirmed and fixed on this branch; what follows records what was wrong and where the fix lives. Measured outcome: the shipped demo grammar now emits **one** warning, not five, and that one is the #74 pair itself.

The fix sits at **two layers**, and the split is the load-bearing part:

- **In the primitive** — a form with no required element outside the discriminator is never collected (`HasRequiredElementOutside`). Such a candidate misses one required element and matches none, so DR-7's admission rule refuses it before any comparison key; it cannot be a rival in a tie, whatever it scores. This is **knob-free**, which is why it belongs to the relation's reachability layer rather than to the warning. It closes findings 2 and 3 together.
- **In the warning** — a tie is reported only if it would clear the default `minScore` (`ScoreAfterDroppingDiscriminator(frame) >= DefaultMinScore`). This is judged against a **default the constructor cannot see**, so it is an audience judgement, not a property of the relation. A runtime consumer applies its own threshold and must not inherit an assumption about someone else's.

The check **weighs** elements rather than counting them, because the weights differ: a matched slot credits `MatchScore` whether optional or required, and only an optional literal credits `OptionalLiteralScore`. So `switch ?to weapons` reaches exactly `1.5/2.5 = 0.60` and warns, while `cease fire` sits at `0.5` and does not — a length count would get both wrong.

**The residual limitation, and it is real:** `minScore` is user-configurable. An author who lowers it below `(D−1)/D` makes the shorter ties live and now gets **no** warning. Bound for `KNOWN_LIMITATIONS.md` in the post-G2 doc pass. Erring the other way would put a knowingly false claim in front of every author who never touched the knob.

---

#### What was wrong (recorded)

The gate this feature added asks whether the frame leaves a **remainder**. It does not ask whether that remainder is enough to produce a tie. Three findings, each independently CONFIRMED, are three faces of that one omission:

1. **A 2-element frame cannot tie at default settings.** A dropped required literal scores `(L−1)/L`, so `L=2` gives `0.5` against `minScore = 0.6`. Both siblings are rejected and **nothing fires** — the message's "the wrong intent can fire" is false, and `requiresConfirmation` is inert when no command fires. Pinned twice already in this repo (`MissedLiteral_TwoElementPattern_StillRejected`, `MissedLiteral_TwoElementPattern_DoesNotFire`). **Four of the demo grammar's five warned sets are this shape**, so the reachable count at defaults is **1, not 5**, and "0% false-positive rate" is unsupported. Threshold: `(L−1)/L ≥ 0.6 ⟺ L ≥ 3`.
2. **A remainder of only optional elements credits no `matchedRequired`**, so DR-7's admission rule refuses both siblings before any comparison key.
3. **`{?x}` → `{x}` folding is score-equivalent but not admission-equivalent** — narrowly, where the remainder credits zero required elements to one member, that member is refused and the other is admitted alone.

**Not a design contradiction.** DR-1 is untouched; this is the same reachability scoping §7.1 and the empty-frame ruling already applied, extended to three cases they did not reach.

**Consequences beyond the gate itself.** Fixing this exposed two related defects, both closed in the same pass:

- The `(command, pattern)` half of the member dedup was **dead code**. It guarded against a pattern being its own sibling via a shift that aligned a required literal against a different one — a shift that required the optional-literal folding removed earlier in the review. With the `?` preserved, two same-length forms of one pattern differ at every position they disagree on, never exactly one. Removed, and the test that "covered" it rewritten to pin why the case cannot arise rather than a mechanism the code no longer has.
- The warning-volume test computed its cross/same split with its **own copy** of the intent filter, so the evidence the DR-7 ruling rests on could drift from the filter it claimed to measure. `IsSingleIntent` is now `internal` and the test calls it.

### 7.3 What F10 does if it refutes

Requirements §8.2. Work stops; the human is told; the design reopens on a new design branch. The refuting test still lands — a refutation is a finding, not a failure — but item 1 does not proceed to G2 as though nothing happened.

## 8. Build plan

### Single phase — persisted plan (2026-08-15)

Approved by the human in plan mode, after a `plan-validator` pass that produced two blockers and nine other findings. Findings that changed the design are marked **[V-n]** and have been folded into §1–§7 above; this section is the ordered build, not a second copy of the design.

**Step 1 — the §2.8 test, first, as a gate.** `Tests~/Runtime/VoxrEagerCommitTests.cs`, fixture and outcomes per §5.2–§5.3. `Commit` → continue; `HoldExtendable` or `None` → **stop and report**, the design reopens. **[V-9]** These tests must not call `LogAssert.NoUnexpectedReceived()` — once Step 3 lands, their own fixture is a sibling set and warns at construction. **[V-6]** Test 2 is the *medial* analog of the existing trailing pin `MissedLiteral_DroppedDiscriminator_FiresTheFirstRegisteredSibling` (`VoxrCommandParserTests.cs:3388`), and should say so. This run doubles as the pre-change suite baseline that Step 5 needs to attribute failures honestly.

**Step 2 — the primitive.** §2.1–§2.6: the two structs, `FindSiblingSets`, `NormalizeElement`, `IsRequiredLiteral`, the bucketing, the `''`/`''` key encoding, the four-gate emission filter, first-seen determinism. **[V-12]** Update the file's `// Owns:` header (`:4`).

**Step 3 — the warning and the collision report.** §3 and §4, appended last in the ctor. Includes the `reported` HashSet **[V-1]**, patterns quoted as authored **[V-8]**, and the adaptive same-intent variant.

**Step 4 — tests for the primitive and the messages.** §6's table, plus the cases validation added: same pair reached from two expansion frames warns once **[V-1]**; one pattern's own two forms are not a sibling set **[V-5]**; a single-element pair is suppressed **[V-4]**. Predicate tests go through `FindSiblingSets` directly; log-text assertions are confined to the message tests.

**Step 5 — absorb the fallout in existing tests (F11b).** **[V-3]** There are 35 `LogAssert.NoUnexpectedReceived()` sites across four files. `NonHazardPatternShapes_DoNotWarn` (`:2126`) **cannot** fail — its grammar has lengths 1/2/4/3, so no two forms are equal-length. The guaranteed amendment is `:3388` (assertion at `:3437`), which constructs the literal #74 grammar. Validation swept 12 of the 22 sites in that file, so the true count is ≥1 and unbounded by that sweep — this step begins with a full run, not an estimate. **[V-12]** F9's sweep must include the 12th `LogAssert.Expect` site at `VoxrCommandRecogniserInjectionTests.cs:731-734`; there are 11, not ten, in the main file.

**Step 6 — the measurement gate (F11).** (a) Volume over `DemoGrammar.cs` as a cross-intent / same-intent split, plus a separate count of `[V-4]`-suppressed empty-frame sets; **[V-13]** stated explicitly as DemoGrammar-only, since `Samples~/CommandRecognition/AssetAuthoring/` is ScriptableObject-authored and not scannable by a C# test without conversion. (b) Test fallout from Step 5, with per-amendment justifications. (c) Constructor cost — **[V-2] a Unity stopwatch test, not the A/B rig.** The rig cannot produce this number: `Rig.csproj` defines no `UNITY_EDITOR`, so a `[Conditional("UNITY_EDITOR")]` call compiles out of the constructor entirely and the delta is **zero by construction** — a false green of exactly the kind that rig is known for — and neither rig has a ctor-timing entry point.

**Step 7 — verification and reconciliation.** `compile-check`, both platforms, per the project bindings. Reconcile this doc to as-built before review.

**[V-9] Commit contract.** Step 1 lands green on its own. Steps 2–5 land as **one atomic commit** — the tree is red between Step 3 and Step 5 by construction, since wiring the warning reddens `:3388` until its expectation is amended. Steps 6–7 are additive.

**Carried to G2:** the DR-2 scope question (§7.2, required to be surfaced by requirements §4.1), the same-intent narrowing (§7.1), the cancel-vocabulary limitation (§4.1), and the F11 volume verdict on DR-7.

### Measured outcome (2026-08-15)

The build ran as planned. Results, all from `Planning~/verification-runs/sibling-final/`:

| | |
|---|---|
| **§2.8** | **CONFIRMED** — `TryEagerCommit` returns `Commit`; the flush then fires by registration order, and reversing the declarations flips the intent. DR-5 keeps its motivation. Committed as `d90d460`. |
| **Suites** | EditMode **124/124**, PlayMode **444/444** (at `d810134`). |
| **F11(a) volume** | **11 sibling sets in `DemoGrammar.cs`** — 5 cross-intent, 6 same-intent (all synonym authoring). After the §7.1 intent ruling and the §7.4 reachability gate, **exactly ONE warns**: the `switch to weapons` / `switch to navigation` pair from issue #74 itself. Pinned by `SiblingSets_DemoGrammar_VolumeAndOrderAreStable` (the relation) and `SiblingWarning_DemoGrammar_WarnsOnlyOnTheReachableTie` (the emission). |
| **F11(b) fallout** | **One** test amended — `MissedLiteral_DroppedDiscriminator_FiresTheFirstRegisteredSibling`, which builds the #74 grammar itself. Nothing else in the suite asserted absence over a sibling-shaped grammar. |
| **F11(c) cost** | `FindSiblingSets` ≈ **0.097 ms** per construction over the demo grammar, stable across runs. As a share of the constructor it read 2.6% then 4.9%, because the constructor total itself moved 3.80 → 1.99 ms. Quote the absolute. |
| **Suite-wide** | 12 distinct warning messages across the whole PlayMode corpus, **every one cross-intent**. Before the ruling: 23 distinct. One cancel-collision report, from the test written to provoke it. |

Two figures in the plan were superseded. Validation's hand-derivation of *12 sets, 6 cross / 6 same* became **9 sets, 4 cross / 5 same** once the emission gates removed the empty-frame and self-pairing defects — the gates worked, and the hand count had included exactly what they were built to exclude. And the measurement was expected to threaten DR-7's default-on; instead it **saved** it, by showing the noise was not spread across the shape but concentrated entirely in the same-intent half.
