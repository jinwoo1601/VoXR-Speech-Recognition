---
type: architecture
feature: tie-aware-selection
topic: sibling-tie-disambiguation
status: draft
updated: 2026-08-16
sources:
  - Planning~/features/tie-aware-selection/requirements.md
  - Planning~/design-docs/sibling-tie-disambiguation.md
  - Planning~/features/sibling-set-detection/architecture.md
  - Runtime/Commands/VoxrCommandParser.cs
---

# Tie-Aware Selection — Architecture

Drafted as the eventual product page for this behaviour, per the workflow. Line numbers are against `main` at `f482154` (item 1 merged) and were read from source on 2026-08-16.

## 1. Shape of the change

One comparator gains a third state, one precomputed lookup is promoted from Editor-only to runtime, and one condition is added to the eager gate. Everything else is consumption of those three.

```
IsBetterCandidate  →  CompareCandidate : CandidateOrder {Better, Tied, Worse}   ← DR-3
       │                     shared by both loops, exactly as before (§2.3)
       ├── ParseInternal (:1329)          Better → adopt · Tied → record (Editor-only) · Worse → skip
       └── TryEagerCommit (:2536)         Better → adopt · Tied → record sibling rival · Worse → skip
                     │
                     └── new condition, after #70's:  tied sibling rival ⇒ None   ← DR-5

FindSiblingSets(commands)                 ← item 1's DR-2 primitive, unchanged in meaning
       ├── WarnOnSiblingDiscriminator     ← existing Editor-only consumer, message reworded
       └── EnsureSiblingLookup()          ← NEW runtime consumer, lazy, mirrors EnsureCanCommitEarly
                     │
                     └── AreSiblingRivals(ci1,pi1, ci2,pi2)
```

The primitive itself needs no move and no signature change: item 1 made `FindSiblingSets` `internal static` and deliberately **not** `[Conditional]`, putting the `[Conditional("UNITY_EDITOR")]` on the *caller* precisely so this feature could add a runtime caller without touching it (item 1 architecture §2.1). Its emission gate does change, for #90 (§6) — that is a change to which members survive, not to what the relation admits.

## 2. The three-state comparator (DR-3)

### 2.1 Contract

```csharp
// Better is returned in exactly the cases the old bool returned true.
internal enum CandidateOrder { Worse, Tied, Better }

static CandidateOrder CompareCandidate(
    in MatchResult candidate, int startIdx,
    float bestScore, int bestStartIdx, int bestConsumedEndIdx, int bestLiteralCount)
```

An enum rather than an `int` sign or a `bool?`: the three states are named at every call site, and a `switch` over them is exhaustive. It is `internal` so tests can assert each outcome directly rather than only through selection results (`Runtime/AssemblyInfo.cs:9-11` grants `InternalsVisibleTo`).

**`MatchResult` must widen from private to `internal` for this to compile.** `struct MatchResult` (`:1566`) carries no access modifier, so it is private-nested; `VoxrCommandParser` is `internal class` (`:64`). C# forbids a member more accessible than its parameter types (CS0051), so `internal CandidateOrder CompareCandidate(in MatchResult …)` does not build — today's `static bool IsBetterCandidate(in MatchResult …)` is legal only because it is itself private. Caught by plan validation; an earlier draft specified `internal` on the method and did not notice.

Widening `MatchResult` is the right resolution rather than making the comparator private, because requirements F3 asks for one test per outcome — including `Score <= 0f → Worse` and the no-incumbent sentinel → `Better` — and neither can be isolated from a selection result. The widening is contained: `MatchResult` has **zero** references outside `VoxrCommandParser.cs` (no test, no rig, no sample), and `internal` is not `public`, so F17 is untouched. The F3 tests become the first construction site of a `MatchResult` outside the parser.

`IsBetterCandidate` is **replaced**, not wrapped. A `bool IsBetterCandidate(...) => CompareCandidate(...) == Better` kept beside it would leave two names for one question and invite a future caller to pick the lossy one — which is the divergence DR-3 rejected the call-site probe to avoid.

### 2.2 The equivalence, and the one branch that actually changes

The current chain (`:1645-1697`) loses information on exactly one line. Every earlier line either refuses outright or resolves a strict inequality, so it already knows which side won; only the final `>` collapses *equal* and *less* into one `false`:

| Line | Today | Three-state |
|---|---|---|
| `candidate.Score <= 0f` | `false` | `Worse` |
| `MissedRequired > MatchedRequired` (DR-7) | `false` | `Worse` |
| `bestScore <= 0f` — no incumbent | `true` | `Better` |
| `startIdx != bestStartIdx` | `startIdx < bestStartIdx` | `Better` / `Worse` |
| `Score != bestScore` | `Score > bestScore` | `Better` / `Worse` |
| `ConsumedEndIdx != bestConsumedEndIdx` | `>` | `Better` / `Worse` |
| `LiteralCount != bestLiteralCount` | — | `Better` / `Worse` |
| all four equal | `false` (the strict `>`) | **`Tied`** ← the only new outcome |

The property to hold, and the one a reviewer can check by reading the two functions side by side:

> `CompareCandidate(...) == Better` **⟺** `IsBetterCandidate(...)` on `main`, for every input.

**The `bestScore <= 0f` sentinel is the trap.** `bestScore` starts at `float.MinValue` in both loops (`:1303`, `:2513`), and a real incumbent always has `Score > 0f` because the first line refuses anything else — so `bestScore <= 0f` means *"no incumbent yet"* and must yield `Better`, never `Tied`. Written as a fall-through chain of equality tests it is easy to reach `Tied` there against a sentinel, which would record a tie with a candidate that does not exist. Requirements §4.1 states the same trap; it is repeated here because this is where the code gets written.

### 2.3 The `:1088` invariant is restated in the comment (F15)

The comment above the comparator (now `:1628-1631`) says the two paths share it *"so the eager verdict always names the pattern the subsequent flush will fire."* DR-5 makes the eager path return `None` where the flush will still fire something, so the old wording becomes false as written. Per design §5.8 it is **restated, not broken**:

> the eager verdict never names a pattern the flush would not fire.

A refusal names nothing, which is exactly what #66's and #70's conditions already do. The comment carries both the new wording and a line saying which conditions rely on it, so the next reader meets the restatement rather than the contradiction.

## 3. The sibling lookup — promoting the DR-2 primitive to runtime

### 3.1 Data contract

```csharp
// One pattern's membership of one sibling set. The value is carried so the pair test can
// reject two members that share it (they are duplicates, not siblings — item 1 F8, §6).
readonly struct SiblingMembership
{
    public readonly int    SetId;
    public readonly string Value;
}

// [commandIdx][patternIdx] → the sets that pattern belongs to, or null. Null is the common
// case: on the demo grammar most patterns are in no set at all.
SiblingMembership[][][] _siblingMemberships;
bool _siblingMembershipsComputed;
```

`(commandIdx, patternIdx)` is the key design §5.1 specifies, and it is why item 1 carried `CommandIndex`/`PatternIndex` on `SiblingMember` rather than emitting a warning-local shape (item 1 architecture §2.2). No change to `SiblingSet` or `SiblingMember` is needed.

**A pattern can belong to several sets**, which is why the leaf is an array rather than a single set id: `["a","b","c"]` is a sibling of `["a","b","d"]` at position 2 and of `["a","x","c"]` at position 1. A scalar `int[][] _siblingSetOf` would silently keep only one and lose the other hazard.

**Carrying `Value` here rather than resolving it from the set** keeps the query self-contained: it never walks `SiblingSet.Members` looking for a matching `(ci,pi)`, so the `List<SiblingSet>` need not be retained past construction of the lookup.

### 3.2 The pair test

```csharp
// Two candidates are sibling rivals when they share a set AND carry different values.
bool AreSiblingRivals(int ci1, int pi1, int ci2, int pi2)
```

All three halves are load-bearing:

- **Share a set** — this is the sibling relation, restricted per design §5.3 so that non-sibling ties (authoring errors, not speech ambiguity) stay out of the runtime path.
- **Different values** — two members of one set carrying the same discriminating literal are duplicates of each other, which item 1's F8 places out of scope. Without this, #90's fix (§6) would make the eager gate refuse on author-duplicated patterns, quietly widening scope.
- **Different intents** — requirements F9. Both members dispatch the same command with the same slots when the intent matches, so refusing buys latency for nothing.

**The intent test must be per-*pair*, not per-*set*, and getting this wrong is easy.** Once #90 retains duplicate-valued members, a set can be cross-intent overall while a particular tied pair inside it shares an intent. The §6 fixture is exactly that shape: `set_mode` contributes two patterns and `set_level` one, so `IsSingleIntent(set)` is false while the pair (`set_mode`'s two patterns) is same-intent and must **not** refuse. A set-level filter alone would refuse on it, violating F9 on a set that passed the set-level check.

`IsSingleIntent(set)` is still applied when the lookup is built (§3.3), but only as an **optimisation** — a wholly same-intent set contains no cross-intent pair, so it can be skipped entirely. The semantics live in the pair test.

Allocation-free: both operands are pre-built arrays and the loops are over them. The arrays are one or two entries in practice, and the common case exits on the first `null`.

**It is only consulted on a `Tied` result**, which is rare — so the structure is chosen for clarity and zero allocation rather than for lookup speed.

### 3.3 Wholly same-intent sets are skipped when the lookup is built — as an optimisation only

Item 1 deliberately left the same-intent filter in the warning rather than in `FindSiblingSets`, "so a later consumer that does care about a same-intent tie still sees one" (item 1 architecture §7.1). **This feature is that later consumer, and it decides it does not care** (requirements F9): both members dispatch the same command with the same slots, so refusing the eager commit would buy latency for nothing.

So a set whose members all share one intent is skipped when the lookup is built, using item 1's `IsSingleIntent` (`:1182`, made `internal` at its review precisely so a second copy of the rule could not drift). `FindSiblingSets` is untouched. **This is a cheap pre-filter, not the rule** — the rule is the per-pair intent test in §3.2, and a set that survives this skip can still contain same-intent pairs.

The pair test needs no stored intent: `SiblingMembership` carries `SetId` and `Value`, and the intents are reachable from the `(ci, pi)` the caller already has — identical `ci` means the same command and therefore the same intent, and distinct `ci` compares `_commands[ci].Intent` ordinally, since two commands may legitimately share an intent string.

### 3.4 Lazy, mirroring `EnsureCanCommitEarly`

```csharp
void EnsureSiblingLookup()   // beside EnsureCanCommitEarly (:2480)
```

The constructor comment at `:431` gives the reason the eager precompute is lazy: *"callers who leave eager flush off never pay the O(2^optionals)+O(forms^2) precompute on Configure/RebuildParser/NotifySlotChanged."* The same argument applies here and more strongly, because the parser is rebuilt on every slot change in an editor session but a given parser may never eagerly commit.

Call sites — and the third one is what keeps this to **one** computation:

- `TryEagerCommit` (`:2510`), beside the existing `EnsureCanCommitEarly()` — the path that needs it in every build.
- `ParseInternal`, **under `#if UNITY_EDITOR`** — the flush path needs it only for the recording in §4.3, so a player's flush path never builds it.
- **`WarnOnSiblingDiscriminator` (`:1097`) calls `EnsureSiblingLookup()` and reads its stored sets, instead of calling `FindSiblingSets` itself.** Without this the Editor would compute the sets **twice** per parser — once at construction for the warning, once lazily for the lookup — which plan validation flagged, and which no measurement in this feature would have caught (`Rig.csproj` defines no `UNITY_EDITOR`, so the constructor call elides there). Routing the warning through the same lazy accessor gives one computation in both configurations: in the Editor the `[Conditional]` caller forces it at construction, and in a player that call vanishes and the build happens on first eager check. The `List<SiblingSet>` is therefore retained alongside the membership table, not discarded after building it.

**That also settles DR-2 on its own terms.** DR-2's ratified cell reads *"One sibling-set computation **at construction**, consumed by the warning, eager selection, and flush selection"* — stronger than §5.1's prose, and an earlier draft of §3.4 argued only against the weaker sentence. With the warning routed through the accessor, the Editor genuinely computes once at construction, exactly as DR-2 says; the player defers a computation DR-2 was not reasoning about, because DR-2's subject is the warning-plus-selection sharing *one definition*, which is preserved absolutely.

**Consequence, stated because item 1's requirements §4.1 was corrected specifically to surface it:** a player build that uses eager flush now pays one `FindSiblingSets` over the whole grammar, once, on first eager check. That is the unconditional cost DR-5 accepts by not being behind the opt-in flag. Item 1 measured the scan at ≈0.097 ms on the demo grammar; F18(c) re-measures it here as a runtime rather than a constructor cost.

**What remains of the departure is player-side only, and it is deliberate.** Design §5.1 lists as a property of the primitive that it is *"**static** — computable in the constructor, alongside the existing scan"*, and its cost paragraph reasons about *"constructor work on every parser rebuild."* In the Editor that is now literally what happens. In a **player** the build is deferred to first eager check, which §5.1's sentence does not describe — but it is what that sentence *wants*: §5.1's operative requirement is that selection consult **a precomputed lookup keyed by `(commandIdx, patternIdx)`** and *"never compare patterns during parsing"*, which lazy satisfies exactly, while also honouring item 1's requirements §4.1, whose whole argument is that a parser must not pay a construction cost for something nothing reads. A shipped game whose parser is rebuilt on every slot change but never eagerly commits would, under the literal reading, pay the scan every rebuild for nothing. Recorded rather than glossed: if a reviewer reads §5.1 as binding on the player path too, that is a scope question for the human, not a silent choice — the same flag item 1 raised about DR-2 and for the same reason.

## 4. Recording the tie

### 4.1 The state machine, and why it is sticky rather than last-wins

Both loops nest `ci` → `pi` → `startIdx` and keep a first-wins incumbent. The recording rides that:

```
Better :  adopt the new incumbent;  CLEAR the recorded rival
Tied   :  if none recorded yet AND AreSiblingRivals(incumbent, candidate) → record it
Worse  :  nothing
```

**Clearing on `Better` is correct, not merely conservative.** `Tied` means the whole key tuple compared equal, so a candidate that beats the incumbent beats everything tied with it too — the recorded rival is stale by construction and must not survive into the verdict.

**Recording the first *sibling* rival rather than the most recent rival is the load-bearing choice.** A winner can be tied by several rivals, and only some of them siblings. Keeping "the last thing that tied" and testing sibling-ness after the loop would drop a sibling rival whenever a non-sibling one happened to be enumerated later — a silent false negative in exactly the shape this feature exists to catch. Evaluating `AreSiblingRivals` at record time and keeping the first hit costs one lookup per tie and cannot lose one. The `if none recorded yet` short-circuit also means the lookup runs at most once per selection round.

Alternatives rejected: collecting all tied rivals into a list (allocates on the innermost loop of both hot paths, for information item 2 has no use for — item 3 can widen it when it has a consumer); and a bare `bool` (loses which pattern tied, which §5.3 asks for and which the §4.3 diagnostic needs).

### 4.2 Eager path — record and refuse

`TryEagerCommit` gains two locals beside the existing `bestMissedRequiredSlot` / `bestHasUnmatchedRequiredTail` (`:2520-2521`), and one condition.

**Placement: at the very end, guarding only the `Commit` return.** The final ternary (`:2648-2650`) becomes three statements:

```csharp
if (_canCommitEarly == null)
    return EagerCommitVerdict.HoldExtendable;

if (!CanCommitEarly(bestCommandIdx, bestPatternIdx))
    return EagerCommitVerdict.HoldExtendable;

// A sibling tie: the two intents are indistinguishable on this buffer, and the word
// that would decide may still arrive. Refuse rather than commit (design DR-5).
if (bestTiedSiblingCommandIdx >= 0)
    return EagerCommitVerdict.None;

return EagerCommitVerdict.Commit;
```

**An earlier draft put it at `:2608`, immediately after #70's condition, and that was wrong.** Plan validation caught it: `:2608` precedes *both* `HoldExtendable` returns (`:2643` when `_canCommitEarly` is null, `:2648` when the pattern is extendable). A sibling tie on an extendable pattern, or in a grammar past `MaxOptionalExpansion`, would therefore have moved `HoldExtendable → None` — stretching the wait from `prefixHoldSeconds` to the full `bufferWindow` **on a buffer that was already not committing early**. That is precisely what requirements §5 forbids: *"The refusal must not lengthen a wait it was not responsible for."* There is nothing to refuse when nothing was going to commit.

At the end, the change is exactly one transition — `Commit → None` — and §5's table is true as written.

Two properties survive the move:

- **After the score condition (`:2561`).** Requirements F8 rests on this: that condition uses the **real configured threshold**, so a tie that cannot clear the gate is refused there for the ordinary reason and never reaches the sibling check. The eager path judges reachability exactly, where item 1's warning had to predict it against a default the constructor cannot see. No reachability gate is added; the ordering *is* the gate. Verified: `:2561` is an unconditional early return and nothing between it and the end writes `bestScore`.
- **Beside #70's condition, not modifying it.** Design §5.8's constraint is *"it sits **beside** that guard rather than modifying it"* — a statement about not touching #70, which holds. It fixes no ordinal position (confirmed: canon contains no other placement text). The condition is now further down the method than "beside" suggests textually, and that is the deliberate cost of not lengthening an unrelated wait.

**`None`, not `HoldExtendable`**, per DR-5 and matching what #66 and #70 already return. `HoldExtendable` asserts one complete, confident, *unambiguous* match; on a tie that assertion is false. The cost is the full `bufferWindow`, measured by F18(b).

### 4.3 Flush path — Editor-only recording

`ParseInternal` selects once per extraction round, so the record belongs to the round's winner. `ParseDiagnosticEntry` (`:1700-1705`, already `#if UNITY_EDITOR`) is appended to once per selected command, which makes it the natural home:

```csharp
internal struct ParseDiagnosticEntry
{
    public string PatternString;
    public int[]  SlotStartWords;
    public int[]  SlotEndWords;
    public string TiedSiblingIntent;        // NEW — null when no sibling rival tied the winner
    public int    TiedSiblingPatternIndex;  // NEW — -1 when none
}
```

Additive: the two consumers (`VoxrCommandRecogniser.cs:632`, `Runtime/Testing/VoxrBatchTestRunner.cs:251`) read named fields and are unaffected. The intent string is stored rather than a command index because it is what a diagnostic reader wants and it already exists — no allocation.

The recording branch in the loop is wrapped in `#if UNITY_EDITOR`, so a player's flush loop reads `if (order == Better)` and nothing else. That is the same discipline the loop already applies to the diagnostic slot buffers a few lines below (`:1352-1355`), so it is in-idiom rather than a new pattern.

**No flush-path behaviour changes.** The same command wins with the same score and slots; only an Editor-only field is populated. Requirements §4.5 records why this lands here despite being inert: item 3 inherits a working recording instead of introducing one inside the feature that also changes what happens when a tie is found, which is the coupling design §9's split exists to avoid.

## 5. What the eager refusal does and does not change

| Situation | Today | After |
|---|---|---|
| Medial sibling tie, nothing more spoken | `Commit` — fires immediately, coin-flipped | `None` — flush fires **the same intent**, up to `bufferWindow` later |
| Medial sibling tie, dropped word arrives late | `Commit` — already fired the wrong intent | `None` — flush sees the longer buffer and can select correctly |
| Trailing sibling tie | `None` (#70's condition) | `None` — unchanged |
| Sibling tie on an **extendable** pattern, or a grammar past `MaxOptionalExpansion` | `HoldExtendable` | `HoldExtendable` — **unchanged**, because the condition sits after both `HoldExtendable` returns (§4.2). An earlier placement would have lengthened this wait for no gain. |
| Same-intent tie | `Commit` | `Commit` — unchanged (§3.3) |
| Non-sibling tie | `Commit` | `Commit` — unchanged (design §5.3) |
| Tie below the configured `minScore` | `None` (score condition) | `None` — unchanged, and the sibling check is never reached |
| No tie | unchanged | unchanged |

**Nothing stops firing.** Verified 2026-08-16: `VoxrPushToTalkController` calls `_commandRecogniser.FlushPendingBuffer()` **before** `CancelPendingCommand()` on release (`VoxrPushToTalkController.cs:100-103`), and the recogniser flushes on window expiry (`VoxrCommandRecogniser.cs:464-470`). A deferred commit is always flushed; the cost is latency, bounded by `bufferWindow`.

## 6. #90 — the emission gate, and the guard that has to come back

### 6.1 What changes

`FindSiblingSets`'s member gate (`:963-995`) currently keeps **one member per distinct value**, dropping later members that repeat a value. Issue #90's example:

```
a : ["mode", "on"]     intent A
b : ["mode", "on"]     intent B
c : ["mode", "off"]    intent C
```

emits `{a:"on", c:"off"}` and drops `b` — so this feature's lookup never learns that `b` and `c` are rivals, and the eager gate does not refuse on a hazard identical to the one it does refuse on. The gate becomes:

> keep every member; emit the set when it holds **≥2 distinct values**.

Set membership then covers `a`, `b` and `c`; the *pair* test in §3.2 supplies the other half by requiring two rivals to carry different values, which is what still keeps `a↔b` — author-duplicated patterns, item 1's F8 — out of the runtime path.

This is deliberately **narrower than issue #90 proposes.** The issue suggests reshaping members into "values with the patterns carrying them". Splitting the question across the set gate (§6.1) and the pair test (§3.2) reaches the same observable outcome with no shape change to `SiblingSet`, which keeps item 1's data contract stable for item 3. Recorded because a reviewer holding the issue against the diff will notice the fix is not the one the issue described.

### 6.2 An exact-duplicate member guard is now required — and it is not the dead code the last review removed

Item 1's review removed a `(CommandIndex, PatternIndex)` arm from this gate as unreachable, with the reasoning preserved in the source comment at `:968-975`: with `?` preserved through normalization, two same-length forms of one pattern can never differ at exactly one position. **That reasoning is still correct**, and this feature does not reinstate the guard it was aimed at.

A different case does become reachable once the value arm stops dropping members, and it was previously absorbed by that arm rather than by the removed one. Verified from source on 2026-08-16:

```csharp
// ExpandOptionals (:2710-2733) enumerates 2^optionals subsets and does NOT deduplicate.
["a", "?x", "?x", "b"]  →  [a,?x,?x,b], [a,?x,b], [a,?x,b], [a,b]
                                          ^^^^^^^  ^^^^^^^  identical, from masks 01 and 10
```

Both identical forms key to the same bucket at the same position and add **the same `(ci, pi, value)` member twice**. Under today's gate the value arm silently absorbs the second; under §6.1's gate it survives, and the warning names one pattern twice.

So the gate keeps an **exact-duplicate** dedup on `(CommandIndex, PatternIndex)` — narrower than the removed arm, and for a different reason. Within one bucket a given `(ci, pi)` can only ever carry one value (two forms of one pattern differing at the discriminator and equal elsewhere is the case `:968-975` rules out), so deduplicating on the pair and on the triple are equivalent; the pair is written because it is the cheaper test.

**This must be stated in the source comment**, or the next reviewer reads it as a revert of their own finding.

### 6.3 The demo-grammar pins do not move — derived, not hoped

Plan validation ported `FindSiblingSets` to a simulator, validated it by reproducing item 1's pinned figures exactly on the current gate, then ran the new gate over `DemoGrammar.AllCommands()`:

| | current gate | new gate |
|---|---|---|
| `sets.Count` | 11 | **11** |
| cross-intent | 5 | **5** |
| same-intent | 6 | **6** |
| `crossFrames` order | five strings | **identical** |

The one difference is that the **same-intent** set `* heading {heading}` grows from 2 members to 3: `set_heading`'s pattern `["orient","heading","{heading}","?mark","{?elevation}"]` has an all-omitted expansion identical to its bare `["orient","heading","{heading}"]`, contributing a second `"orient"` member the value gate drops today. It is same-intent, so `IsSingleIntent` suppresses it and the warning never sees it; `SiblingSets_DemoGrammar_VolumeAndOrderAreStable` (`:3805-3860`) inspects only cross-intent sets. `SiblingWarning_DemoGrammar_WarnsOnlyOnTheReachableTie` (`:3863`) still emits exactly one warning.

So §10.2's open question — *does the demo grammar contain a duplicate-valued member?* — is **answered: yes, and the pins hold.** Validation also swept every grammar literal in `Tests~/**` and `Samples~/CommandRecognition/CommandDemo.cs` for the shape and found four instances, all inert (two members, one distinct value, dropped by both the old and the new gate). **No existing test changes warning behaviour, and no `LogAssert.NoUnexpectedReceived()` site is put at risk by this step.** Item 1's discipline still applies if a pin does move in practice: amending a test because the grammar genuinely carries the shape is legitimate; amending it to make a failure go away is a defect.

### 6.4 Retaining members breaks the longest-frame collapse, and that is accepted

`IndexOfSameMembers` (`:1061-1090`) compares member lists **positionally and by length** (`if (members.Length != candidate.Count) continue;`), and the list it compares is `kept` — exactly what §6.1 changes. Today the value gate normalises every bucket to one member per value, which is what makes the comparison work. Retaining members removes that normalisation, so two frames carrying one hazard can now hold different member counts and stop collapsing:

```
A: ["set","?now","mode","on"]   B: ["set","?now","level","on"]   C: ["set","mode","on"]

current: bucket "set ?now * on" → kept [A,B];  bucket "set * on" → kept [A,B] (C dropped, value "mode" seen)
         → same member list → collapse → 1 set, 1 warning
new:     bucket "set ?now * on" → [A,B];       bucket "set * on" → [A,B,C]
         → different lengths → no collapse → 2 sets, 2 warnings
```

**Accepted rather than fixed, and the reasoning is that the two sets are genuinely different hazards.** `set * on` implicates A, B *and* C; `set ?now * on` implicates only A and B. Collapsing them would have to discard C — which is issue #90 all over again, one level up. Reporting a real hazard twice is the right direction to err against silently under-reporting it, which is the whole basis of #90.

Three things bound the cost. It needs a third pattern whose normalized expansion coincides with another's, which is already unusual authoring. §6.3's sweep found **no instance anywhere in this repo** — not in the demo grammar, not in the samples, not in 98 ad-hoc test grammars. And the **runtime is unaffected either way**: `AreSiblingRivals` asks only whether two patterns share *some* set and carry different values, and both A↔B and B↔C answer identically under one set or two.

Pinned by a test built on the shape above, so a future maintainer meets the decision rather than rediscovering it as a bug.

### 6.5 Two message consequences that follow from retention

Both are new, both are in `WarnOnSiblingDiscriminator`'s neighbourhood, and neither was in the first draft of §7's *"this is the only message change in this feature"* — which is therefore not accurate as written:

- **`BuildSiblingWarning` builds `values[]` per member** (`:1203-1212`), so a retained duplicate renders as `("on", "on" or "off")`. The value list is deduplicated for display, exactly as the intent list already is (`:1217-1224`) and for the same reason.
- **The cancel-collision loop iterates `set.Members`** (`:1149-1157`) and emits one warning per matching member, so a duplicated cancel-colliding value now reports twice. Deduplicated by value.

Neither changes which hazards are reported — only how many times a value is printed.

## 7. The warning message (F16)

The shipped remedy is wrong. `BuildSiblingWarning` (`:1229-1232`) ends:

> …so the wrong intent can fire. **Diverge earlier**, or mark the more destructive one requiresConfirmation.

Diverging earlier removes nothing: `weapons mode` / `navigation mode` ties exactly as `switch to weapons` / `switch to navigation` does. The product docs already say the correct thing, and the message is brought into line with them rather than inventing a third phrasing — `Documentation~/command-recognition.md` "Remedies", item 1:

> **Make the two commands differ in more than one element.** This is the only fix that removes the tie rather than moving it: with two differing words, losing one still leaves the other to decide.

Proposed replacement clause, to be settled against the docs during implementation:

> …so the wrong intent can fire. Make them differ in more than one element — that is the only fix that removes the tie — or mark the more destructive one requiresConfirmation.

Scope discipline: this is the only change to message *wording*. Two changes to message *multiplicity* follow mechanically from #90's retention and are covered in §6.5 — a deduplicated value list, and a deduplicated cancel-collision loop. Issue #91's element-number defect is deferred to item 3, which rewrites this same string to add the opt-in flag clause (item 1 requirements §8.1).

**Zero test blast radius, confirmed twice.** `"Diverge earlier"` occurs exactly once in the repo, at `:1231`, and in no test or doc. Validation additionally inspected every warning-text regex in both suites (`VoxrCommandParserTests.cs:3413`, `:3728`, `:3916`, `:3940`, `:3968`, `:4021`, `:4086`; `VoxrEagerCommitTests.cs:1336`, `:1362`, `:1369`) — none reaches the remedy clause.

## 8. Test plan

Placement follows the existing suites — both files are PlayMode.

**`Tests~/Runtime/VoxrEagerCommitTests.cs`** — the behaviour change:

**Every fixture here must be a *medial or leading* discriminator over a frame worth ≥3.** Plan validation caught a first draft whose `#90` fixtures were `["mode","on"]` / `["mode","off"]` — a **trailing** discriminator on a 2-element frame, which is refused twice over before the new condition is reached (at `:2561` for `0.5 < 0.6`, and at `:2606` by #70). Those tests would have been green on `main` and green after, for reasons unrelated to the feature. A fixture that cannot reach the new condition cannot evidence it.

As built, in `VoxrEagerCommitTests.cs`:

| Test | Pins |
|---|---|
| `TryEagerCommit_MedialSiblingDiscriminator_RefusesOnTheTie` | F5 — **inverts** item 1's `…_CommitsOnAnUndecidableBuffer` from `Commit` to `None`, renamed and re-commented to say this is the change |
| `TryEagerCommit_SiblingTieOnAnExtendablePattern_StillHoldsExtendable` | §4.2 — the placement fix. A third pattern extends the winner so `CanCommitEarly` is false; the verdict must stay `HoldExtendable`, **not** become `None` |
| `TryEagerCommit_SameIntentSiblingTie_StillCommits` | F9 — two phrasings of one intent, medial |
| `TryEagerCommit_NonSiblingTie_StillCommits` | F10 — ties but differs at two positions |
| `TryEagerCommit_IdenticalPatternsAcrossIntents_StillCommit` | F11 — one distinct value, so no set is emitted and nothing refuses. Item 1's F8 boundary, at runtime |
| `TryEagerCommit_ShadowedCrossIntentMember_Refuses` | **F11/F12 — the test that fails on `main`.** See below |
| `TryEagerCommit_LeadingTwoElementTie_RefusesOnlyBelowDefaultMinScore` | F8 — the asymmetry. `["cease","fire"]` / `["resume","fire"]` on buffer `fire`: `1/2 = 0.5`, discriminator **leading**, so #70 does not take it. At the `0.6` default the score condition refuses; at `0.4` the sibling condition does — the pair the *warning* is documented as silent about |

`Parse_MedialSiblingDiscriminator_FiresByRegistrationOrderAlone` passes **unchanged**, which is F2 and F6 on the flush side: the same intent still fires, and reversing the declarations still flips it.

**F4 is pinned at the comparator, not the gate.** An inadmissible candidate returns `Worse` and can never be `Tied`, which `CompareCandidate_MissedRequiredExceedsMatched_IsWorse` asserts directly. Constructing a grammar where one sibling is admission-refused *and* a tie survives is not possible — item 1's `HasRequiredElementOutside` drops such forms before a set is built — so there is nothing to assert at the gate.

**The one fixture that genuinely differs before and after**, and the reason #90 is a correctness prerequisite rather than a reporting fix:

```
set_mode  : ["set","{ship}","mode","on"]     ← value "mode"
            ["set","{ship}","level","on"]    ← value "level", SAME intent
set_level : ["set","{ship}","level","on"]    ← value "level", different intent
```

On `main`, the value gate keeps the first member per value — `set_mode`'s two patterns — and **drops `set_level` entirely**. The survivors share an intent, so `IsSingleIntent` suppresses the set, it never enters the lookup, and buffer `set alpha on` commits early on a coin flip between `set_mode` and `set_level`. After #90's fix all three members survive, the set is cross-intent, and the pair (`set_mode`p0, `set_level`p0) differs in both value and intent → refused. This is issue #90's "sharper variant" — an under-report becoming no report at all — reaching the runtime.

**`Tests~/Runtime/VoxrCommandParserTests.cs`** — the comparator, the flush path, and the primitive:

| Test | Pins |
|---|---|
| `CompareCandidate_*` — one per outcome row of §2.2 | F3 — including the no-incumbent sentinel yielding `Better`, not `Tied` |
| `Parse_MedialSiblingDiscriminator_FiresByRegistrationOrderAlone` | F2/F6 — **passes unchanged**; the flush is untouched |
| `SiblingSets_DuplicateValuedMember_IsRetained` | F12 — the #90 fixture yields one set, three members, two values |
| `SiblingSets_DuplicatedOptionalElement_DoesNotDoubleCountAPattern` | §6.2 — the `["a","?x","?x","b"]` case |
| `SiblingWarning_NamesARemedyThatRemovesTheTie` | F16 |

**`Tests~/Editor/VoxrCommandParserDiagnosticTests.cs`** — the Editor-only recording (F14): the medial sibling utterance records the rival's intent and pattern index; an unambiguous utterance records none.

**Recogniser level** (F7): the medial sibling utterance fires after `bufferWindow` rather than immediately, with the same intent and slots; and fires on push-to-talk release.

**Existing suites.** F2's acceptance is "no edited expected values" outside the tests listed above as inverted. A failure anywhere else is a real regression, not fallout, and is treated as one.

## 9. Measurement (F18)

### 9.1 The instrument

**The corpus and its rig are under `coverage-in-selection`, not `fidelity-miss-cost`.** Verified 2026-08-16 — an earlier draft of this section named the wrong one:

- Corpus: `Planning~/features/coverage-in-selection/phase7-corpus.tsv`, exactly 699 lines, `<perturbation-kind>\t<utterance>` — 16 `baseline`, 59 `delete`, 192 `prepend`, 192 `append`, 240 `concat`.
- Rig: `Planning~/features/coverage-in-selection/ab-rig/` (the richer of the two — it also carries `Sweep.cs`, `perturb.py`, `report.py`, `DocCheck.cs`).
- `stage.sh` compiles the **real** parser sources staged from a git ref (`main`) or the worktree, so before/after is two builds of shipped code.
- `Rig.csproj` defines no `UNITY_EDITOR`. Item 1's scan was `[Conditional("UNITY_EDITOR")]` and compiled out entirely, making its delta **zero by construction** — the false green plan validation caught there. This feature's comparator, lookup and eager condition are unconditional runtime code, so they compile in. The Editor-only flush recording compiles out, which is correct: it is inert.
- **The eager modes already exist — nothing needs adding.** An earlier draft of this section said `Program.cs` "drives `Parse` only and needs an eager mode added"; it has two. `--verdicts` (`:253`) runs `TryEagerCommit` once per finished line, and `--prefix-verdicts` (`:230`) runs it over **every growing prefix**, one row per `(utterance, prefix length)`. The file's own comment records why the second exists: *"`--verdicts` samples only the final buffer state — and the final buffer is exactly the state `TryEagerCommit` does NOT run on."* **`--prefix-verdicts` is the correct instrument**; a replay of finished lines would sample the wrong state and is what the first draft of Step 7 specified.
- **Both modes hard-code `minScore = 0.6f`** (`:244`, `:261`). So the rig cannot evidence requirements F8's lowered-threshold asymmetry; that is a Unity test.

**`stage.sh` does NOT stage `VoxrCommandRecogniser.cs`.** The rig instantiates `VoxrCommandParser` directly, so it cannot exercise the buffer window, `prefixHoldSeconds`, or the eager-flush wiring at all — that class is not even compiled into the scratch project. Two consequences: requirements F7's recogniser-level assertions are **Unity tests**, not rig output; and the "eager-timing delta" is measured as a change in `TryEagerCommit`'s **verdict**, never as wall-clock latency.

### 9.2 The corpus is not silent on sibling ties — it contains the reported one

Design §7 warns that *"the corpus was not built to contain sibling ties, so a null result means 'the corpus is silent', not 'no regression'."* Checked rather than inherited, and the premise is half wrong:

```
145: baseline  switch to navigation
147: delete    switch navigation
148: delete    switch to            ← the #74 discriminator elision, on the ONE reachable set
344: baseline  switch to weapons
345: delete    to weapons
346: delete    switch weapons
```

Line 148 is exactly the tie this whole design exists for: frame `switch to *`, `2/3 = 0.667` against the `0.6` default, cross-intent, and the single set item 1 measured as reachable on the demo grammar.

**It is a *trailing* discriminator, which is the shape #70's condition already refuses.** So the corpus is a genuine **control**, not a blank: it should show zero eager-verdict change, because it covers the one sibling shape this feature does not alter. A delta appearing at line 148 would mean the new condition is firing where #70's already did — a redundancy, not a fix. The corpus cannot evidence F5, and now it does not merely fail to; it actively bounds the change from the other side.

### 9.3 The three figures — MEASURED 2026-08-16

Before = `f482154` (`origin/main`, item 1 merged). After = the final worktree. **Not `main`:** local `main` is stale at `de5cff8`, which predates PR #92, so staging from it would have folded item 1's changes into item 2's A/B. Staged from the SHA instead. The rig was validated against the committed grammar pin first — 133 entries, exact match — and the pin is unchanged after.

| | before | after |
|---|---|---|
| **699-corpus `Parse`** | — | **699/699 rows identical.** No change to intents, slots or scores, line 148 included |
| **`--prefix-verdicts`** (4014 rows) | 435 Commit / 96 HoldExtendable / 3483 None | **identical** — zero rows moved |
| **steady `TryEagerCommit`** | ~0.020 ms/call | ~0.020 ms/call — unchanged (independently corroborated at PR review) |
| **ctor** | ~0.030 ms | ~0.030 ms — unchanged |

⚠️ **Two figures that stood here were withdrawn at PR review (2026-08-16).**

- **`FindSiblingSets` "~0.077 → ~0.038 ms, ~2× faster"** — an independent harness measured no delta at all (before 0.0104–0.0116 ms, after 0.0105–0.0134 ms) and an absolute an order of magnitude lower. Re-running the original harness interleaved still reproduced the drop, so the two disagree on both the absolute *and* the existence of a delta. The causal story attached to it was also wrong: the gate did replace a string compare with two int compares, but `DistinctValueCount` reinstates the same `string.Equals` scan one line later over a *larger* list — the string work moved, it did not vanish. A number two harnesses cannot agree on is worth less than no number, and this feature does not need it.
- **The cold "first `TryEagerCommit` +0.04 ms"** figure came from a throwaway probe that no longer exists, as issue #93 records. Not reproducible from the branch; withdrawn rather than presented as a measurement.

⚠️ **"The unchanged `HoldExtendable` count of 96 is the direct check on §4.2's placement" was FALSE and is withdrawn.** PR review built two variants with the condition deliberately placed above the `HoldExtendable` returns — exactly the error §4.2 describes — and **both produced byte-identical corpus output**: 435 / 96 / 3483, zero rows differing. No corpus prefix pairs a sibling tie with an extendable pattern, so the count is blind to the error it was offered as evidence against.

**The actual guard on §4.2's placement is `TryEagerCommit_SiblingTieOnAnExtendablePattern_StillHoldsExtendable`.** The 96 is a null control alongside the rest of the corpus, nothing more. This is the same mistake as item 1's: a null result read as confirmation of something it never touched.

Targeted fixtures, before → after, with the **flush intent unchanged in every row**:

| fixture | before | after |
|---|---|---|
| medial sibling (DR-5 target) | `Commit` | **`None`** |
| shadowed cross-intent (#90 variant) | `Commit` | **`None`** |
| same-intent pair | `Commit` | `Commit` |
| identical across intents (F8) | `Commit` | `Commit` |
| non-sibling tie | `Commit` | `Commit` |
| trailing sibling (#70) | `None` | `None` |
| **extendable + sibling tie** | `HoldExtendable` | **`HoldExtendable`** |
| leading 2-element @ `minScore` 0.6 | `None` | `None` |
| leading 2-element @ `minScore` 0.4 | `Commit` | **`None`** |

### 9.4 The three figures, as planned

1. **699-corpus `Parse` A/B** — zero difference in fired intents, slots and scores across all 699 rows, line 148 included (the flush path is untouched, so it must still fire `mode_weapons`). Evidence for F2.
2. **699-corpus `--prefix-verdicts` replay** — zero verdict changes across every `(utterance, prefix)` row. The trailing control of §9.2, sampled at the buffer states the eager gate actually runs on. Plus **purpose-built medial fixtures**, where the delta must appear in full. This is the only place F5 is evidenced.
3. **Runtime cost of the lookup**, absolute. Item 1's percentage-of-constructor figure read 2.6% then 4.9% because the denominator moved 3.80 → 1.99 ms between runs; the absolute number is the one to quote.

**The rig's standing trap applies to the grammar itself — and this time it is clean.** `Program.cs` carries a *hand transcription* of `DemoGrammar.cs`, and item 1's first volume measurement was wrong because that transcription introduced `hold fire` and dropped `stop firing` (item 1 requirements §8.3). Re-checked 2026-08-16: `Program.cs:27-140` now reproduces all six slot definitions (both aliases, both `NumberSequence` bounds) and all eleven commands with identical patterns in identical order — including `["orient","heading","{heading}","?mark","{?elevation}"]`, the very pattern the earlier transcription got wrong, and the one §6.3's duplicate-member finding turns on. So the re-derivation against `DemoGrammar.AllCommands()` stays in the plan as a **precaution**, not a load-bearing gate. For figures 1 and 2 the transcription would cancel anyway, since both sides use it.

## 10. Open questions

### 10.1 Is a recorded tie *the* sibling tie? — OPEN, carried from requirements §8.6

The lookup answers "are these two patterns co-members of a sibling set", not "did these two candidates tie *because* the discriminator was dropped". A tie could in principle arise between patterns that are siblings under some other pair of expanded forms, on tokens unrelated to the discriminator.

Design §5.3 asks only for sibling *rivals*, so set membership is what is specified, and the failure mode is one extra deferral rather than a wrong command. Tightening it speculatively would put a second definition of the hazard beside DR-1's — the divergence DR-2 exists to prevent. Report at G2 if implementation shows it refusing on shapes that are not really ambiguous.

### 10.2 Does the demo grammar contain a duplicate-valued member? — CLOSED 2026-08-16

**Yes, and the pinned counts do not move.** Derived by simulation at plan validation, cross-checked against item 1's figures on the current gate. §6.3 carries the table. The duplicate is `set_heading`'s all-omitted expansion, and it is same-intent, so `IsSingleIntent` suppresses it before the warning or the lookup sees it.

### 10.3 The `minScore` asymmetry needs a doc sentence at G2

§4.2 / requirements §4.3. After this feature the runtime protects short sibling pairs below the default `minScore` that the author-facing warning is documented as silent about (`KNOWN_LIMITATIONS.md:560`). That is the right direction to be wrong in, but the two now answer different questions and the limitation entry says otherwise. Post-G2 doc pass, along with `command-recognition.md:151` / `:356`, `troubleshooting.md:91` and `KNOWN_LIMITATIONS.md:527-538`, all of which currently state that the medial case commits early.

### 10.4 Whether the eager mode belongs in the rig or in Unity tests — CLOSED 2026-08-16

Moot: `--prefix-verdicts` already exists (§9.1) and is the right sampling. The corpus replay is rig work with no new mode to write; the medial fixtures and F8's lowered-`minScore` asymmetry are Unity tests, because both rig modes hard-code `0.6f`. The question was posed on the false premise that the rig had no eager mode.

## 11. Build plan

### Single phase — persisted plan (2026-08-16, revised after plan validation)

Ordered lowest-risk-first, with the one subtle-but-behaviour-neutral change isolated ahead of the behavioural one so a regression is unambiguous. Line numbers verified by recon against `f482154`.

**Validation produced one blocker and five significant findings; all are folded into §1–§10 above and into the steps below.** The four that changed the build rather than the prose: `MatchResult` must widen or Step 1 does not compile (§2.1); the eager condition moves to the end of `TryEagerCommit` so it cannot lengthen a `HoldExtendable` wait (§4.2); the `#90` eager fixtures were unreachable and are replaced (§8); and the rig already has the eager mode this plan proposed to add, sampling prefixes rather than finished buffers (§9.1). One validation finding was itself wrong and is **not** applied: `_canCommitEarly` / `_canCommitEarlyComputed` are at `:239-240` as originally written, not `:241-242` — re-read from source.

**Step 0 — baseline. DONE 2026-08-16: EditMode 124/124, PlayMode 451/451 at `f482154`, clean tree, no pre-existing failure.** This resolves the 444-vs-451 discrepancy (444 belonged to `d810134`, before item 1's last two commits added tests). Stage the rig's "before" side from `main` while the tree is still clean — and confirm it is clean first, since a `compile-check` run transiently renames `Tests~`→`Tests` and `stage.sh` only stamps `+dirty` from `git diff --quiet -- Runtime`.

**Step 1 — the comparator, alone and behaviour-neutral.** `IsBetterCandidate` (`:1645-1697`) becomes `CandidateOrder CompareCandidate(...)`; both call sites (`:1330` in `ParseInternal`, `:2537` in `TryEagerCommit`) become `== CandidateOrder.Better`.

- **Widen `struct MatchResult` (`:1566`) to `internal`** — otherwise `internal CompareCandidate(in MatchResult …)` is CS0051 and the step does not compile (§2.1). It has zero references outside the parser, so the widening ripples nowhere.
- **The trap** (§2.2): `bestScore <= 0f` is the *no-incumbent sentinel*, not a tie, and must return `Better`. Validation confirmed no path exists by which a genuine incumbent carries `bestScore <= 0f` — it is written only inside the adopt block, reachable only when `candidate.Score > 0f`.
- Restate the `:1088` invariant in the comment at `:1628-1644` (F15).
- **Seven stale prose references, not three.** In-file: `:542`, `:821`, `:1735`, `:1858`, `:2214`, `:2565` — `:821` and `:2565` are load-bearing, being the DR-7 admission-inheritance arguments F4 rests on, and `:2214` additionally carries a wrong line pin (`IsBetterCandidate:1120`; actual `:1645`). In tests: `VoxrEagerCommitTests.cs:1172`, `VoxrCommandParserTests.cs:3227`. All go stale in wording, not compilation.
- Add the `CompareCandidate_*` tests, one per row of §2.2's table.
- **Gate: both suites green with zero edited expected values.** A pure refactor; any red here is a mistranslation of the chain, and this is the only step where that diagnosis is unambiguous.

**Step 2 — #90's emission gate (Editor-visible only).** In `FindSiblingSets`, replace the value-dedup loop (`:976-992`) and the `kept.Count < 2` gate (`:994-995`): keep every member, drop only exact `(CommandIndex, PatternIndex)` duplicates, and require **≥2 distinct values**.

- **The source comment must say why this dedup is not the one PR #92's review deleted** (§6.2). The removed arm guarded a self-sibling shift that normalization made impossible; this one absorbs identical expanded forms from `ExpandOptionals` (`:2710-2733`, which does not deduplicate — `["a","?x","?x","b"]` yields `["a","?x","b"]` twice). Without that sentence the next reviewer reads it as a revert of their own finding.
- **Dedupe the value list in `BuildSiblingWarning` (`:1203-1212`) and the cancel-collision loop (`:1149-1157`)** — §6.5. Retention otherwise prints `("on", "on" or "off")` and can emit a collision report twice.
- **The demo-grammar pins do not move** — derived, not hoped (§6.3): 11 / 5 cross / 6 same and the same `crossFrames` order. The only change is a same-intent set growing 2→3 members, which `IsSingleIntent` suppresses. Validation also swept every test and sample grammar: no existing test changes warning behaviour. If a pin *does* move in practice, the movement goes in the PR body as evidence and is never re-baselined silently.
- Add `SiblingSets_DuplicateValuedMember_IsRetained`, `SiblingSets_DuplicatedOptionalElement_DoesNotDoubleCountAPattern`, and a test pinning §6.4's two-set outcome so the accepted collapse behaviour is met as a decision rather than rediscovered as a bug.
- Runs before Step 3 deliberately: the lookup is built from these sets, so the sets must be right before anything measures them.

**Step 3+4 — the lookup and its consumer, as one commit.** Two steps in review, one commit, because the lookup alone is unread code.

- **3:** `SiblingMembership`, `_siblingMemberships` / `_siblingMembershipsComputed` (field home `:239-240`, beside `_canCommitEarly`), `EnsureSiblingLookup()` mirroring `EnsureCanCommitEarly` (`:2480-2486`), `AreSiblingRivals(...)`. Retain the `List<SiblingSet>` and **route `WarnOnSiblingDiscriminator` (`:1097`) through the accessor** so the Editor computes once at construction rather than twice (§3.4).
- **The pair test is share-a-set **and** different values **and** different intents** (§3.2). `IsSingleIntent` skips wholly same-intent sets at build time as an optimisation only — a set can be cross-intent while a tied pair inside it is not, which is exactly the §8 fixture.
- **4:** two locals beside `:2520-2521`; the record/clear branch in the selection loop (`:2536-2556`); `EnsureSiblingLookup()` beside `EnsureCanCommitEarly()` at `:2510`; and the new condition **at the end of the method**, restructuring the final ternary (`:2648-2650`) so it guards only the `Commit` return. **Not at `:2608`** — that precedes both `HoldExtendable` returns and would lengthen a wait on buffers that were never going to commit (§4.2).
- **Clearing on `Better` is load-bearing** (§4.1), as is recording the *first sibling* rival rather than the last rival of any kind — the latter silently drops a sibling whenever a non-sibling is enumerated after it. Validation could not construct a counter-example to either.
- **No change in `VoxrCommandRecogniser`.** `HandleResult:528-531` tests `== Commit` then falls through to `_eagerHoldArmed = (verdict == HoldExtendable)`, so `None` is already handled as today. Confirmed nothing else in the repo distinguishes the two verdicts.
- Invert `TryEagerCommit_MedialSiblingDiscriminator_CommitsOnAnUndecidableBuffer` (`:1310-1343`) to assert `None`, renamed and re-commented. `Parse_MedialSiblingDiscriminator_FiresByRegistrationOrderAlone` (`:1345-1384`) must pass **unchanged**.
- Add §8's eager table. **Every fixture must be medial or leading over a frame worth ≥3** — a trailing 2-element fixture is refused at `:2561` and again at `:2606` and evidences nothing.
- **`LogAssert` trap, inherited from item 1's [V-9]:** every new fixture here *is* a sibling set and warns at construction. Each test needs its `LogAssert.Expect`, and none may call `LogAssert.NoUnexpectedReceived()` without one. 33 such sites in `VoxrCommandParserTests.cs`, 6 in `VoxrEagerCommitTests.cs`.

**Step 5 — the flush-side Editor-only recording.** Two fields on `ParseDiagnosticEntry` (`:1700-1705`); the `#if UNITY_EDITOR` record branch in `ParseInternal`'s loop (`:1329-1357`); populate at the append site (`:1406-1411`); `EnsureSiblingLookup()` under `#if UNITY_EDITOR`. **Declare the tie locals at `:1303-1310`, inside the `while` at `:1301`, beside the other `best*` round locals** — declaring them beside `diagnosticEntries` (`:1298`) compiles, passes any single-command test, and leaks a round-1 rival into round 2's diagnostic entry. All three current readers use named fields, so adding fields is safe. Tests go in `Tests~/Editor/VoxrCommandParserDiagnosticTests.cs` (181 lines, `CreateParser()` idiom at `:31-32`).

**Step 6 — the warning string (F16).** One clause at `:1231`, matched to `Documentation~/command-recognition.md`'s "Remedies" item 1. Zero test blast radius, confirmed by substring and by inspecting every warning-text regex in both suites (§7). Add a test pinning that the remedy names something that removes the tie.

**Step 7 — measurement (F18), before the PR opens.** Per §9, and **no new rig mode is needed**: stage `main` as before and the worktree as after; run the 699-corpus `Parse` A/B; run **`--prefix-verdicts`** (not `--verdicts`, which samples the finished buffer the eager gate never runs on) and expect zero verdict changes across every `(utterance, prefix)` row, line 148 included — the trailing control. Both rig modes hard-code `minScore = 0.6f`, so F8's lowered-threshold asymmetry is a Unity test, not rig output. Author the medial fixtures where the delta must appear; measure the lookup's absolute runtime cost. Re-derive the rig grammar's sibling-set count against `DemoGrammar.AllCommands()` as a precaution — validation confirms the transcription currently matches.

**Step 8 — verify, reconcile, open.** `compile-check` both platforms; reconcile §1–§10 to as-built; `review-cycle`; open the PR with all three figures in the body; `review-pr` at the full profile. **Stop at G2.**

**Commit contract.** Steps 1, 2, 5, 6 each land green on their own — including Step 2, since §6.3 establishes the demo-grammar pins do not move, so the earlier "may redden the pins" exception is dropped. Steps 3+4 land as one commit. Step 7 is additive.

**Carried to G2:** the player-side lazy-build departure from design §5.1 (§3.4), the eager-vs-warning `minScore` asymmetry and its `KNOWN_LIMITATIONS` consequence (§10.3), the set-membership-vs-actual-tie looseness (§10.1), the accepted two-set collapse outcome (§6.4), and the `#if UNITY_EDITOR` gating of the flush recording, which requirements §4.5 records as *not* mandated by the design.

### As-built (2026-08-16)

Six commits, each verified green on both platforms before the next began, then a review-fix pass (§11.1).

⚠️ **This section first read "Built as planned" and listed three deviations. That was wrong, and the review caught it:** requirements **F7 — the two recogniser-level tests — had not been built at all**, and no deviation recorded it. Every eager test called `TryEagerCommit` directly as a pure function, which structurally cannot observe what happens after a refusal, so the feature's central safety claim rested on inspection. Corrected here and delivered in §11.1. The lesson is the one item 1's requirements §4.3 already states: a doc that says "as planned" is worth nothing unless someone checks it against the diff.

| Commit | Step | Suites after |
|---|---|---|
| `e457a03` | 1 — three-state comparator | EditMode 124/124, PlayMode 460/460 |
| `a6555e8` | 2 — #90's emission gate | 124/124, 465/465 |
| `2cf9faf` | 3+4 — lookup and eager refusal | 124/124, 471/471 |
| `c68e3b4` | 5 — flush-side Editor-only record | **128**/128, 471/471 |
| `69e95f4` | 6 — the warning's remedy | 128/128, **472**/472 |

Baseline at `f482154` was measured, not assumed: EditMode 124/124, PlayMode 451/451. That also settled the 444-vs-451 discrepancy — 444 belonged to `d810134`, before item 1's last two commits added tests.

**Three deviations from the plan, all forced by evidence:**

1. **`SiblingScan_IsEditorOnly` had to be amended**, and the plan did not predict it. Routing the warning through the shared lookup (§3.4) made `WarnOnSiblingDiscriminator` an instance method, and that test looks it up reflectively with `BindingFlags.NonPublic | Static`, so `GetMethod` returned null. The search is now indifferent to staticness (`Instance | Static`); **all three assertions are untouched** — the method is still asserted to exist, to carry exactly one `[Conditional]`, and for its condition to be `UNITY_EDITOR`. The property the test defends is unchanged and still fully pinned. Recorded because item 1's requirements §4.3 draws exactly the right line here: amending a test because the change is legitimate is fine, weakening an assertion to make a failure go away is a defect. This is the former.

2. **The `#90` warning-remedy fixture had to grow an element.** The first version used `weapons mode` / `navigation mode` to show that diverging early does not help — but a 2-element frame scores `0.5` and item 1's own reachability gate suppresses the warning entirely, so the test would have asserted a message that is never emitted. `weapons mode now` / `navigation mode now` reaches `2/3` and still diverges at the first element, so it makes the same point and actually warns.

3. **The `--fixtures`/`--tie`/`--cost` probes were scratch-only.** They were injected into staged copies of the rig under the session scratchpad, never into `Planning~/features/coverage-in-selection/ab-rig/`. The committed rig is unmodified.

**Two predictions that held exactly**, both derived before running Unity and both worth recording because the derivation is what made the build cheap:

- §6.3's claim that the demo-grammar pins do not move. Confirmed by probing the real parser: 11 sets / 5 cross / 6 same, same emission order, with the `* heading {heading}` same-intent set growing 2→3 members and being suppressed before the warning sees it.
- §6.4's accepted two-set outcome, and all five `#90` fixture shapes, confirmed against the real parser before a single Unity cycle was spent.

**One thing the plan asserted that turned out to be unobservable, stated honestly:** §8 listed `TryEagerCommit_CrossIntentSet_SameIntentPair_StillCommits` as pinning the per-pair intent test. It does not exist, because it cannot: whenever a set is cross-intent, some cross-intent rival also ties the winner, so the *verdict* is `None` either way. The per-pair test still earns its place — it decides **which** rival is recorded — and that *is* observable, so it is pinned on the flush side instead by `LastParseDiagnostics_ShadowedRival_IsTheCrossIntentOne`. Item 3 will prompt the speaker with that rival, so naming the wrong one would surface there as a wrong question.

### 11.1 Review pass (2026-08-16)

`review-cycle`, 11 finder angles, 5 findings put through adversarial verification. **No angle found a correctness defect in the shipped selection behaviour** — LINE re-derived all four load-bearing invariants key-by-key against `f482154` including the NaN paths, XFILE returned zero findings, PIT cleared the conditional-compilation hazard in both build configurations, and GONE proved the #90 gate's *emission* criterion is unchanged (old `kept.Count < 2` ⟺ new `DistinctValueCount(kept) < 2`), so set membership grew and set existence did not.

Four findings confirmed, one refuted, all ruled by the human 2026-08-16.

| Finding | Verdict | Outcome |
|---|---|---|
| **WRAP-1** — the refusal was silently absent for patterns with 7–12 optionals | CONFIRMED, with a concrete failing grammar | **Fixed.** See below |
| **ALT-1** — the flush record is narrower than §5.3 asks | CONFIRMED in part; canon self-contradicts | **Kept as built**, deviation recorded — requirements §4.8 |
| **GAP-1** — F7's recogniser-level tests never built | CONFIRMED; behaviour safe by inspection | **Fixed** — three tests added |
| **GONE-1** — the message could print one pattern's text twice | CONFIRMED, message reproduced at both revisions | **Fixed** — pattern texts deduplicated |
| **SIMP-1** — collapse the eager tie locals to a `bool` | **REFUTED** | Not applied. Design §5.3 requires recording *which pattern* tied, and architecture §4.1 had already rejected the `bool`. A comment now says so at the call site |

**WRAP-1 in detail, because it was a real hole in DR-5's guarantee.** `WarningForms` caps expansion at `MaxWarningExpansion = 6` and returns the raw decorated pattern past it; `ComputeCanCommitEarly` abandons only past `MaxOptionalExpansion = 12`, and fails *conservatively*. So a pattern with 7–12 optionals kept a live eager commit while its sibling relations went unanalysed, and `AreSiblingRivals` reported "no hazard" from an analysis that never ran:

```
optionals= 6   siblingSets=1   eagerVerdict=None            ← refusal works
optionals= 7   siblingSets=0   eagerVerdict=Commit          ← silently absent
optionals=12   siblingSets=0   eagerVerdict=Commit
optionals=13   siblingSets=0   eagerVerdict=HoldExtendable  ← conservative fallback engages
```

**New to this feature.** At `f482154` `FindSiblingSets` had exactly one caller and it was `[Conditional("UNITY_EDITOR")]`, so the cap had no runtime consequence at all; promoting the same output to a runtime gate without revisiting it is what opened the window. `EnsureSiblingLookup` now records truncation per pattern and `AreSiblingRivals` refuses on a cross-intent tie involving one — the direction `ComputeCanCommitEarly` already fails in when it gives up. Pinned by `TryEagerCommit_SiblingAnalysisTruncatedByTheExpansionCap_RefusesAnyway`, with a 6-optional control that reaches `None` through the ordinary path.

**One fix was caught by the rig, not by Unity.** Scoping `_siblingSets` to `#if UNITY_EDITOR` broke the **player** build: `[Conditional]` elides the *call*, not the method body, and `WarnOnSiblingDiscriminator` still has to compile. The field is now declared unconditionally and assigned only in the Editor. Unity would never have surfaced this, because Unity always compiles with `UNITY_EDITOR` defined — the A/B rig is the only build in this project that exercises the player configuration.

**Deferred by ruling, filed rather than fixed:** GAP-2 (the two dedup branches #90 forced have no test that fails if deleted), GAP-3 (no multi-round test pins the per-round tie reset), GAP-4 (invariants 8–9 have no executable guard, and the cost probes were scratch-only and are not reproducible from the branch).

### 11.2 PR review pass — PR #94 (2026-08-16)

`review-pr` at the full profile: `CLAIM` plus all 11 roster angles against the merged diff, then three reasoned findings put through adversarial verification. Posted onto the PR. It found more than the branch review, and the most serious finding was **in the fix the branch review produced**.

| Finding | Verdict | Outcome |
|---|---|---|
| The truncated-analysis arm refused on **non-siblings**, in player builds — violating F10 (Must) | CONFIRMED, severity raised | **Fixed.** See below |
| "The unchanged `HoldExtendable` count of 96 is the direct check on placement" | CONFIRMED false, by construction | **Withdrawn** — §9.3 |
| `EagerFlush_SiblingTie_LateDiscriminatorSelectsTheRightIntent` passed unchanged on `main` | CONFIRMED by execution | **Rewritten** — see below |
| #90's retention re-opens the wrong-element-number case #91 records as fixed | CONFIRMED by execution | **Deferred into #91**, whose carve-out is now wrong |
| The `FindSiblingSets` speedup | DISPUTED across harnesses | **Withdrawn** — §9.3 |
| The lookup is built when `_canCommitEarly` is null and can never be read | CONFIRMED, measured 1.2 MB | **Fixed** — guarded |
| New CS0649 in every downstream player build | CONFIRMED, reproduced twice | **Fixed** — `#pragma` at the declaration |
| Only the left operand of the truncation `\|\|` was exercised | CONFIRMED | **Fixed** — reversed-order fixture |
| The per-pair intent test's second arm was unpinned | CONFIRMED | **Fixed** — Editor diagnostic test |
| The truncation predicate was stated twice and could drift | CONFIRMED, 3 angles | **Fixed** — one `ExpansionTruncated` |
| The `HoldExtendable` path shortens the hold on a detected tie | **REFUTED** | Not applied — and it corrected §5.8's rationale, below |
| Loser-loser sibling pairs among tied candidates are never tested | **REFUTED** | Not applied — the shape reaches but is provably inert |

**The F10 violation, and why the first fix was wrong.** Closing the 7–12-optional hole meant answering "I never analysed this pair". The first answer was `return true` — refuse. That fires for pairs *provably not* siblings: two patterns whose discriminator is the same word are duplicates, `FindSiblingSets` returns zero sets for them, and yet past 6 optionals the gate flipped `Commit` → `None` in a **player** build. F10 is a Must and says non-sibling ties stay out of the runtime path.

The narrowing: fall back to the one reading that survives truncation. DR-1 requires the discriminator to be a required literal, and required elements appear in **every** expansion, so comparing the two patterns' required elements asks exactly whether their all-optionals-omitted readings are siblings — and that reading is real, because matching handles optionals natively and never consults `WarningForms`. Zero differing positions means duplicates, so no refusal. Partial by construction: a sibling relation existing only in a *mid* expansion is still missed. That is a narrower over-approximation, not a closed hole, and it is stated rather than glossed.

**§5.8's rationale is wrong for the case DR-5 targets, and the refutation is what surfaced it.** The design says *"on the eager path the missing word may still arrive, so refusing costs only latency"*. True of the **trailing** shape — which #70 already refuses. False of the **medial** shape this feature exists for: reaching the sibling condition means #70 passed, so an element *after* the dropped word already matched, and results only append. The word cannot land in a position the match has gone past. What refusing actually buys is that the decision moves to the flush, where item 3 can **ask**. The source comment now says this; §5.8 keeps its wording, since amending locked design is a G1 matter, not a review outcome.

**A test was deleted rather than repaired** for the same reason: `EagerFlush_SiblingTie_LateDiscriminatorSelectsTheRightIntent` asserted a scenario that cannot occur. It was replaced with the boundary that can — an unambiguous utterance forming no tie and committing exactly as before.

## Related

- Requirements: `Planning~/features/tie-aware-selection/requirements.md`
- Locked design: `Planning~/design-docs/sibling-tie-disambiguation.md` §2.1, §2.3, §2.8, §5.3, §5.8, §7.2, §9 row 2, DR-1, DR-2, DR-3, DR-5
- Predecessor: `Planning~/features/sibling-set-detection/architecture.md` — §2.1 (why the primitive is not `[Conditional]`), §2.2 (why the runtime key is carried), §7.1 (the same-intent filter's placement), §7.4 (the reachability gate)
- Closes: issue #90 · Deferred: issue #91 → item 3
- Adjacent, not modified: #66 (`bestMissedRequiredSlot`), #70 (`bestHasUnmatchedRequiredTail`), #41 (the span key), #65 (DR-7 admission), #42 (`WarnOnDroppableRequiredLiteral`)
