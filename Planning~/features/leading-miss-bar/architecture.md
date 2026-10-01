---
type: architecture
feature: leading-miss-bar
topic: leading-miss-bar
status: draft
updated: 2026-08-27
sources:
  - Planning~/features/leading-miss-bar/requirements.md
  - Planning~/design-docs/leading-miss-bar.md
  - Runtime/Commands/VoxrCommandParser.cs
  - Tests~/Runtime/VoxrCommandParserTests.cs
  - Tests~/Runtime/VoxrEagerCommitTests.cs
---

# Leading-Required-Miss Bar — Architecture

## 1. Shape of the change

One rule, one new `bool`, four seams. The rule (DR-1): **a candidate whose first required element matched nothing may compete and consume, but may not fire.**

The change is unusually shallow for the size of the behaviour it removes, and that is the point of DR-4's fork A. Nothing about scoring moves; nothing about selection moves; the coverage terms are not touched. A flag is computed where the pattern is already being walked, carried on the match result that already carries the required-element ledger, and consulted at the two places a command can be *fired*.

| seam | file | what |
|---|---|---|
| **A — the latch** | `Runtime/Commands/VoxrCommandParser.cs` — `MatchResult`, `TryMatchScored` | one `bool` field, one local, two latch sites |
| **B — the flush suppression** | same — `ParseInternal` | one `best*` local, one adopt-site assignment, one guard |
| **C — the eager refusal** | same — `TryEagerCommit` | one `best*` local, one adopt-site assignment, one refusal, one comment amendment |
| **D — the tests** | `Tests~/Runtime/VoxrCommandParserTests.cs`, `Tests~/Runtime/VoxrEagerCommitTests.cs` | one transformed + renamed, five new |

Plus `CHANGELOG.md` under `[Unreleased]` (requirements §4.13 — moved here from backlog item 2 by ruling).

**What is deliberately not touched.** Two lists, and they carry different authority — worth keeping apart, because only the first is canon's:

- **Canon's #82 guarantee (design §2.2, §2.3):** `BuildCoverageTables`, `SkippedBefore`, `OrphanedAfter`, `IsAdmissibleStart`. Issue #82's orphan-run termination runs entirely through `IsAdmissibleStart`, which applies its own count test to a `forStartProbe` match and never calls the comparator — so the bar is two removes from it by construction (requirements §4.7, G-2). These four being byte-identical in the diff **is** the G-2 evidence.
- **This feature's own scope fence, inferred not quoted:** `CompareCandidate`, `ComputeConfidence`, and every scoring constant. Canon does not state these as unmodifiable — what §5.2 says about `ComputeConfidence` is that it must not *run* on a barred round, which is a claim about ordering, not about source stability. They are on the list because DR-3 rejects the comparator placement and design §3 puts the scoring numbers out of scope, so touching them would mean the build had drifted off the ruled fork.

A diff that touches any of the seven is a finding; a diff that touches any of the **first four** is a G-2 regression.

### 1.1 The one decision this doc makes that the design doc did not

Design §5.1 describes the latch as *"two locals (`sawFirstRequired`, `leadingRequiredMissed`)"*. **This build uses one local and two latch sites instead of two locals and four**, deriving "no required element has been seen yet" from the ledger that already exists. §2.2 has the reasoning and the rejected alternative. This is an implementation-shape choice inside DR-1, not a change to the rule — the output is identical for every input — but it is recorded prominently because a reviewer comparing the diff to §5.1 will notice the difference and should find it explained rather than have to infer it.

## 2. Seam A — the latch

### 2.1 The data contract

`MatchResult` (`VoxrCommandParser.cs:2926`) gains exactly one field, beside the two completeness flags it already carries:

```csharp
// Whether the pattern's FIRST REQUIRED element matched nothing — the verb, in a command
// grammar. Optional elements are skipped when locating it, so a pattern led by an
// unmatched optional is not flagged; a pattern with no required elements at all never
// sets it. Drives the leading-miss bar in ParseInternal and the eager refusal below
// (issue #124, design DR-1/DR-3/DR-5).
public bool LeadingRequiredMissed;
```

The struct stays a `struct`, stays `internal`, and gains no other member (requirements F2). It is populated at the single `return new MatchResult { … }` at `:3323`.

**Why on `MatchResult` rather than an out-parameter or a second return.** The two existing completeness flags (`MissedRequiredSlot`, `HasUnmatchedRequiredTail`) are precisely this shape — a boolean about the *match*, consumed by a caller that decides whether to fire — and they are carried here. A third one carried differently would be a divergence with no gain, and both call sites already hold a `MatchResult`.

### 2.2 The latch itself, and why one local beats two

The requirement is that the flag reflect the outcome of the **first required element**, with optionals skipped (F3). Design §5.1 reaches that with `sawFirstRequired` + `leadingRequiredMissed`, latched at all four sites that touch the required ledger.

**Chosen: derive "first" from the ledger.** `matchedRequired` and `missedRequired` are incremented at exactly four sites and nowhere else — required-slot match, required-slot miss, required-literal match, required-literal miss. Optional branches (matched or not) touch neither, by DR-7 of the scoring model. So, evaluated *before* the increment:

```
matchedRequired == 0 && missedRequired == 0   <=>   no required element has been reached yet
```

which makes the latch one local and two sites, both of them miss sites:

```csharp
// required-slot miss, VoxrCommandParser.cs:3234
if (matchedRequired == 0 && missedRequired == 0)
    leadingRequiredMissed = true;
missedRequired++;
```

```csharp
// required-literal miss, VoxrCommandParser.cs:3280
if (matchedRequired == 0 && missedRequired == 0)
    leadingRequiredMissed = true;
missedRequired++;
```

The match sites need nothing: if the first required element *matched*, the flag must stay `false`, and it already is.

**Why this over §5.1's two locals.** The trap design §9 names third — *"the latch must skip optionals … and must not fire on an unmatched optional leading element"* — becomes **structurally impossible** rather than merely avoided. The two-local form skips optionals because the author remembered to put the latch only in required branches; a fifth latch site added later in an optional branch would compile, pass most tests, and quietly invert F3. The one-local form skips optionals because it reads the counters that *define* "required", so the skip is not a thing the code can forget. It is also two sites instead of four, and one stack slot instead of two.

**Rejected: §5.1's two locals, latched at four sites.** Kept on record because it is the design's own wording and is not wrong — it produces identical output. It is rejected on the maintenance property above, not on cost. If a future change ever makes a required element increment neither counter, this form breaks and the two-local form does not; that is the trade, and it is acceptable because those counters *are* the definition of the required ledger and moving them would be a scoring-model change with its own design cycle.

**Rejected: a local function `LatchFirstRequired(bool missed)`.** Capturing `matchedRequired`/`missedRequired`/`leadingRequiredMissed` by reference from a local function in a hot method risks a display-class allocation, and this method is the innermost body of a triple-nested loop. Not worth the readability for two sites.

### 2.3 The unresolvable-slot early exit

`TryMatchScored` has one early exit before the walk completes: `return default;` at `:3201`, taken when a pattern names a slot that is not in `_slotIndex`. It returns a zeroed `MatchResult`, so the new flag lands `false` there along with every other field.

**That is safe, and it is safe for a reason worth stating rather than assuming.** `default` carries `Score = 0f`, and both consuming paths floor on it before the flag is ever read — `ParseInternal`'s round guard is `bestScore <= 0f` and `CompareCandidate` refuses a non-positive score. So a candidate on this path can never become the round winner, and `false` can never license a fire. No latch is needed on the early exit, and adding one would be dead code.

### 2.4 The start probe computes the flag and nobody reads it

`TryMatchScored` serves two callers: the ordinary scoring path and `IsAdmissibleStart`'s `forStartProbe` call, which builds the coverage tables. The probe reads only `Score`, `MatchedRequired` and `MissedRequired`.

The flag is computed for probe matches too — one comparison, and at most one store, per required miss. **This is deliberate.** Gating it on `!forStartProbe` would add a branch to the hot walk to save a store on a path that already runs the same walk, and would fork the two callers' semantics for no observable gain. More importantly it keeps the invariant simple: *a `MatchResult` always describes its own match*, with no field that means something different depending on who asked. Nothing in `IsAdmissibleStart` or `BuildCoverageTables` reads the new field, which is what F9's "those four members byte-identical" check confirms from the other side.

### 2.5 Cost

Two comparisons and at most one store, on a path that already walks every element of every pattern from every start index. No allocation, no table, no second pass, no dependence on grammar size beyond the walk that was already happening (NFR §5, G-5). The Unity-side pin is through `TryEagerCommit` (§6.4); the byte measurement is the rig's `--bench` (§7).

## 3. Seam B — the flush suppression

### 3.1 Placement

Three additions to `ParseInternal`:

**The local**, beside the other `best*` declarations at `:2278-2285`:

```csharp
bool bestLeadingRequiredMissed = false;
```

**The adopt-site assignment**, at `:2362`, beside `bestConsumedEndIdx = matchResult.ConsumedEndIdx;`:

```csharp
bestLeadingRequiredMissed = matchResult.LeadingRequiredMissed;
```

Unconditional on adopt, like every other `best*` field — a new incumbent brings its own flag, so there is no clear-on-adopt problem of the kind the tie state has.

**The guard**, immediately after the existing progress guard at `:2501-2502`:

```csharp
if (bestEndIdx <= searchStart)
    break;

// The leading-required-miss bar (issue #124, design DR-1/DR-3). The winner matched
// nothing of its pattern's FIRST required element — in a command grammar, its verb — so
// there is no evidence any action was requested, only that some words matched a
// pattern's tail. It still WON the round and still consumes its span: that is what
// re-bases searchStart past leading debris and keeps SkippedBefore off the next
// command, which is the whole of why DR-4 chose refuse-to-FIRE over refuse-to-compete.
// What it may not do is produce a result.
//
// Placed HERE, above ComputeConfidence, the slot-array copy, the VoxrCommand
// construction, the sibling-tie write and the Editor diagnostic, for three reasons and
// not merely for speed:
//   - progress is already guaranteed by the guard above, so this continue cannot spin
//     and needs no second guard;
//   - a barred round should allocate nothing: the Editor diagnostic allocates three
//     times per round and any slot-carrying winner allocates its slot array (G-5);
//   - _tiedSiblingBuf[_resultCount] is written BEFORE _resultCount advances, so
//     suppressing above it keeps a barred round from recording a tie for a result slot
//     it never fills.
// The third is an invariant kept because it is free here, NOT because violating it
// corrupts output — see §3.2's correction.
if (bestLeadingRequiredMissed)
{
    searchStart = bestEndIdx;
    continue;
}
```

Body is exactly `searchStart = bestEndIdx; continue;` — the same advance the bottom of the loop makes, and nothing else.

### 3.2 What sits below it, and therefore does not run

In order: `ComputeConfidence`, the `VoxrSlotMatch[]` allocation and `Array.Copy`, the `VoxrCommand` construction, the `_tiedSiblingBuf` write, `_resultBuf[_resultCount++]`, and — inside `#if UNITY_EDITOR` — the two `int[]` diagnostic copies, the `string.Join`, and the `diagnosticEntries.Add`.

**The Editor diagnostic going quiet on a barred round is a real consequence, and it is forced.** Keeping the entry would mean placing the suppression below the `VoxrCommand` construction and the tie write, which is exactly the placement DR-3 rejects, and the diagnostic block allocates three times per round, which G-5 forbids on a barred path. The two cannot both be had here; DR-3 ruled which wins. Requirements §8.4 carries it to G2 with a cheap remedy sketched for item 3 (an allocation-free barred-round counter, not a full entry).

### 3.3 What the round still does

Everything up to selection. The candidate is enumerated, scored, compared, and adopted as the incumbent exactly as today; `tiedRivalCount` and the rest of the tie state are still maintained during the scan. Only the post-selection block is skipped, and `searchStart` still advances past the consumed span.

This is the property that makes fork A work and fork B destructive, and it is worth stating as an invariant a test can hold: **a barred round changes `searchStart` by exactly what an unbarred round would have.** `bestEndIdx` is the same value the bottom of the loop would have assigned. Requirements F10's leading-debris test is the assertion that catches a violation.

### 3.4 The empty-round path is untouched

`if (bestCommandIdx < 0 || bestScore <= 0f) break;` at `:2497` keeps its meaning: no candidate at all ends extraction. A barred round is not an empty round — it found a winner — and reaches the new guard instead. An utterance whose rounds are all barred returns `_resultCount == 0`, which the recogniser already routes to `OnUnrecognisedSpeech` (DR-6). No new event and no new reject reason. The pending path **needs no special-casing** (DR-8) — pending is fed by *results*, and a barred round produces none — but its behaviour does change, and the doc originally said "no change to the pending path", which is wrong: a leading-missed candidate on an `allowPartialMatch` command used to reach `EnterPending` and open a slot-fill prompt for a command whose verb was never spoken. Under the bar no pending opens. T9 pins this at recogniser level.

## 4. Seam C — the eager refusal

### 4.1 Why this seam exists at all

`TryEagerCommit` runs its own selection scan over the same `(command, pattern, startIdx)` triple and calls the same `CompareCandidate`. Its existing comment records that this is how the admission rule reaches it for free.

**The bar does not reach it that way, because DR-3 put the bar in `ParseInternal` and not in the comparator.** Under fork B it would have been a comparator count and inheritance would have been automatic; under fork A there is no inheritance at all. Left alone, a leading-missed candidate commits early — **while every flush-path test stays green**, because the flush path is correct and simply never runs.

**ERRATUM E-1, 2026-08-27.** This section originally ended *"An eager commit is a fire, so this defeats G-1 outright"*. False — see §10's canon-authority calls. `Commit` routes through `FlushBuffer` → `ProcessParsedResults` → `ParseInternal`, so the bar refuses the winner regardless and no phantom fires. The condition protects the **buffer**: `Commit` consumes and clears the transcript, so a half-spoken command would be flushed early and discarded. DR-5's ruling is unaffected.

This is the single most reachability-sensitive part of the feature and the reason requirements F14 demands the eager test be mutation-verified *in isolation*.

### 4.2 The three additions

**The local**, at `:3966-3967` beside its two siblings:

```csharp
bool bestLeadingRequiredMissed = false;
```

**The adopt-site assignment**, at `:4018-4019`, beside `bestMissedRequiredSlot` / `bestHasUnmatchedRequiredTail` (the full `matchResult` adopt run is `:4011-4019`):

```csharp
bestLeadingRequiredMissed = matchResult.LeadingRequiredMissed;
```

**The refusal**, immediately after the `bestHasUnmatchedRequiredTail` refusal at `:4087`, making a run of three:

```csharp
// The leading-required-miss bar (issue #124, design DR-5). Unlike the admission rule
// noted above, this one is NOT inherited from the comparator — see the amended note.
// A candidate whose first required element matched nothing may never fire, and an
// eager commit IS a fire, so it must be refused here as well as at the flush.
//
// None, for the same reason the two conditions above use it: at this gate refusing
// costs only latency, and the flush applies the bar properly once the transcript is
// final. There is no case where this delays a command that would otherwise have fired
// correctly — a candidate this refuses could not have fired on either path.
if (bestLeadingRequiredMissed)
    return EagerCommitVerdict.None;
```

Placed after the tail condition rather than before it so the three read in the order they were added, and so the two completeness conditions stay adjacent — they are one idea (issue #66 and #70), and the bar is a different one. In the eager gate's full sequence of ten `return EagerCommitVerdict.*` statements this becomes the fifth gate, at `:4089`, still ahead of the whole-buffer condition at `:4108`.

Note the house formatting: each existing refusal is **two** physical lines (`if (...)` then an indented `return`), not the single line design §5.2 quotes. CSharpier reformats every `.cs` edit here, so match the two-line form rather than fighting it.

### 4.3 The comment amendment (F7)

The note at `:4039-4043` currently reads, in part:

> *"Note one condition that is NOT listed below because it has already run: the admission rule in CompareCandidate (issue #65, DR-7) … The eager scan inherits it by sharing the comparator with ParseInternal."*

Left as-is, the next reader applies that sentence to the bar, concludes §4.2's refusal is redundant, and deletes it — restoring the bypass with the suite still green. DR-5 requires the comment say the opposite explicitly. It gains a paragraph:

```
// That inheritance is specific to rules that live IN the comparator, and the
// leading-required-miss bar (issue #124) is deliberately not one of them: DR-3 places
// it in ParseInternal's post-selection block, which this method does not call. So the
// bar reaches this path only through the explicit condition below, and that condition
// is load-bearing rather than defensive — delete it and a leading-missed candidate
// commits early and fires, with every flush-path test still passing.
```

## 5. Data flow, end to end

```
TryMatchScored (per command x pattern x startIdx)
    walks the pattern
    at a required-element MISS with no required element seen yet:
        leadingRequiredMissed = true                        [Seam A]
    returns MatchResult { …, LeadingRequiredMissed }

  ├── IsAdmissibleStart / BuildCoverageTables (forStartProbe)
  │       reads Score, MatchedRequired, MissedRequired only
  │       -> the new field is inert here; #82 untouched          [F9]
  │
  ├── ParseInternal, per round                                   [Seam B]
  │       selection ... adopt -> bestLeadingRequiredMissed
  │       progress guard
  │       if (bestLeadingRequiredMissed) { searchStart = bestEndIdx; continue; }
  │       ...ComputeConfidence, slots, VoxrCommand, tie write, emit, diagnostic
  │
  └── TryEagerCommit                                             [Seam C]
          selection ... adopt -> bestLeadingRequiredMissed
          score gate -> #66 -> #70 -> BAR -> whole-buffer -> sibling -> Commit/Hold
```

The two consuming paths are independent, and that is the whole architectural content of DR-5: there is no shared chokepoint through which one condition could serve both, because DR-3 chose a placement that is not the comparator.

## 6. Test plan

Six test changes: one transformed, five new. Every fixture is built from an explicit local grammar rather than the shared demo grammar, matching the house pattern of the tests it sits beside, so a demo-grammar edit cannot silently change what these pin.

### 6.1 T1 — the transformed test (F13)

`Coverage_SequentialExtraction_SparesTheFirstCommandWhenASecondLosesItsWord` → **`Coverage_SequentialExtraction_SparesTheFirstCommandAndBarsTheSecond`** (`VoxrCommandParserTests.cs:2462`).

- The `cease_fire` = 1.0000 assertion and its message — *"not charged: approach target {target} is matchable from \"target\""* — survive **character-identical**. This is #82's guard and F9's evidence. Editing it is a finding, not a fix.
- `Assert.AreEqual(2, results.Length)` → `1`.
- The `2f/3f` score line and the `GetSlot("target")` line are removed.
- The control (`"cease fire approach target hotel one"` → both 1.00) is unedited and becomes the only place in the test where a second command fires.
- The comment gains a paragraph on why round 2 now emits nothing while step 1 stays true, mirroring design §5.7.

### 6.2 T2 — the leading-debris test (F10) — NEW

The shape design §5.8 names as the suite's gap, and the assertion that catches fork B.

`"target hotel one cease fire"` on the same grammar → `cease_fire` at **1.0000**. The barred `approach_target` consumes `target hotel one`, re-basing `searchStart`, so `SkippedBefore` charges `cease_fire` nothing.

**Mutation:** convert the bar to refuse-to-compete (drop the candidate in `CompareCandidate` instead). `cease_fire` then scores 2/(2+3) = 0.4000, falls under the gate, and the utterance fires nothing. The test must fail.

### 6.3 T3 — the eager refusal (F6, F14) — NEW

In `VoxrEagerCommitTests.cs`, beside the #66 and #70 refusal pins: `TryEagerCommit_LeadingRequiredMiss_ReturnsNone`.

Driven through `TryEagerCommit` directly — it is `internal` and four tests in that file already call it, so DR-7's "no test seam" costs nothing here.

The fixture must reach the bar and **no earlier refusal — and clear every LATER gate too**, which is the whole difficulty. *(Corrected 2026-08-27: the original list of three was incomplete. A fixture meeting only those three makes the mutation vacuous — deleting the bar would still return `None` from a gate BELOW it, and the test would still pass while pinning nothing, which is exactly the failure mode F14 exists to prevent.)* Five conditions: clear `minScore` (`:4036`); fill every required slot (#66, `:4060`); leave no unmatched required *tail* (#70, `:4087`) — a leading literal miss satisfies this for free, since `requiredAfterLastMatch` is reset by the elements that follow; **span the whole buffer**, `bestStartIdx == firstRecognisedIdx && bestEndIdx == tokens.Length` (`:4108`); and register **no sibling** (`:4157`). Built as `["alpha","bravo","charlie"]` against `Tok("bravo charlie")` — matched 2 / missed 1 at 0.6667, no slot, tail clear, start 0, end 2 = length, lone command — plus a spoken-anchor control asserting `Commit`.

**Mutation, and the isolation requirement:** delete the refusal; this test must fail and **no other test may**. A flush test failing alongside proves the fixture was reaching the flush and the test is not pinning the eager gate.

### 6.4 T4 — the reported #124 shapes (F1) — NEW

The issue's own grammar (`query_time_to_target : ["time","to","target"]`, `intercept_target : ["intercept","track","{track}"]`), covering all three of design §7.2's parser-visible rows in one fixture:

| utterance | expected |
|---|---|
| `time to target track one two four four` | `n=1`, `query_time_to_target` = 1.0000 |
| `track one two four four` | `n=0` — the standalone round-1 shape |
| `intercept track one two four four` | `n=1`, `intercept_target` = 1.0000, `track` = "one two four four" |

The third row is the one that keeps the test honest: without it, a bar that barred everything would pass the first two.

### 6.5 T5 — the interior miss stays untouched (F8) — NEW

`decelerate by {burn_level}` against `"decelerate hard burn"` → `n=1`, `decelerate` at 0.6667. The first required element matched; a later one did not. Pins that the bar asks *which*, not *how many* — the case design §4.1 uses to show a threshold is the wrong instrument.

`Coverage_SequentialExtraction_ChargesNothingForALaterCommand` (`:2432`) also stays green **untouched**, which covers the trailing half.

### 6.6 T6 — optionals are skipped, both ways (F3) — NEW

Two assertions on one fixture, covering the latch's two failure modes (requirements §4.4):

- A pattern led by an **unmatched optional** (`["?please","switch","to","weapons"]` against `"switch to weapons"`) fires normally — the optional is skipped when locating the first required element, so `switch` is it, and it matched.
- The same pattern against an utterance that drops `switch` is **barred** — the first *required* element is what missed.

The one-local latch (§2.2) makes the first case correct by construction, but the test pins the behaviour rather than the mechanism, so it survives a future re-shaping of the latch.

### 6.7 T7 — uniform over element type (F4) — NEW

A slot-leading pattern whose slot matched nothing is barred on the same path as a literal-leading one. DR-2 is ruled **on principle** — the 699-row corpus contains no slot-leading pattern and has no opinion (design §6.2, §7.4) — so this test is the *only* evidence for it and must be written rather than inferred from the corpus.

Canon gives three reasons for uniformity, and the two beyond "one count, no element-type branch" are the ones a reviewer needs in order to judge this test: *"the first required element is the pattern's **anchor**, and a pattern that matched nothing of its anchor has identified nothing, whether that anchor is a verb or a value"*, and *"a type branch here would be a rule the parser cannot explain to an author."* The test exists to pin the anchor property, not merely the absence of a branch.

The pattern needs enough required elements to clear `minScore` while missing its leading slot, which at the shipped 0.60 gate means five or more; a three-element slot-leading pattern scores 0.3333 and is rejected by the gate either way, proving nothing.

### 6.8 T8 — allocation (F15)

Extends the existing precedent rather than inventing an instrument, because both obvious instruments are broken here (requirements §4.8): `Parse` can never be pinned at zero, and `GC.GetAllocatedBytesForCurrentThread` is inert in this environment.

- **Unity side:** the latch is pinned through `TryEagerCommit` under `Is.Not.AllocatingGCMemory()`, following `EagerSelection_OverASiblingTie_AllocatesNothingPerCall` exactly — hoisted token array, warm-up call, literal-only grammar. Mutation-verified with a deliberate allocation in the latch.
- **Rig side:** `--bench` on both arms for bytes/utterance and µs/utterance (§7). This is what design §7.5 asks for and is the only instrument that can see the flush suppression's allocation saving.

## 7. Measurement

**The corpus work is done and is not to be repeated** (design §7): 699 rows, five arms, rig validated against the committed grammar pin, refuse-to-FIRE reproduced byte-identically from two independently written patches. 48 rows change, 17 fire nothing, **0 clean 1.0000 commands destroyed**.

What this feature still owes (design §7.5, minus item 2's share):

1. **Both Unity suites** — EditMode *and* PlayMode through the host project. Most parser tests are PlayMode. Baseline pass counts taken at `b83b62f` *before* any edit, so requirements F17's "no edited expectations" claim has a floor.
2. **`--bench` on both arms** — µs/utterance and bytes/utterance, per G-5.
3. **A re-validation of the rig before either number is trusted:** `--grammar` against `NativeBridge~/harness/expectations.json`, and `--bar=0` against the saved `corpus-bar-off.tsv`. A clean A/B is not by itself evidence.

**The `DocCheck` pin is item 2's**, not this feature's — it belongs with the `scoring.md` §7 D amendment it protects (DR-9). Recorded here so it is not mistaken for an omission.

**Deferred, human-only:** on-device Quest verification. This feature touches no native bridge code and no MonoBehaviour lifecycle path, so no ABI rebuild is owed and no `.so` is committed — but the deferral is stated rather than assumed away.

## 8. Risks and fragile parts

- **The eager gap is silent.** Its absence cannot be detected by any flush-path test. Mitigated by T3 and by F14's isolation requirement. *(ERRATUM E-1: originally "the eager **bypass**… the one defect that would ship green" — it is not a bypass and not a firing defect. The flush bars the winner either way; what ships broken is buffer handling, which is real but is not G-1.)*
- **The `:4040` comment is load-bearing documentation.** Deleting the eager condition as "redundant" is a plausible future cleanup, and the amended comment is the only thing standing against it.
- **~~The tie-buffer alignment is invisible until it is wrong.~~ CORRECTED 2026-08-27 — this risk does not exist.** The claim was that suppressing below the `_tiedSiblingBuf` write leaves a stale record aligned to a result slot that never arrives. It does leave a record, but that record is unreachable: a later emitting round rewrites index `_resultCount` before incrementing, and an index at or past `_resultCount` is never read — `VoxrCommandParser.cs:2705-2707` states the contract (*"valid for the count ParseInternal returned"*) and the sole consumer (`VoxrCommandRecogniser.cs:1129`) is indexed by a loop bounded by `resultCount`. There is no input for which the below-write placement produces wrong output. The placement stands on DR-3 and on allocation; **locked canon never made the stronger claim** (design `:199` says only that suppressing above the write means a barred round *never records a tie for a slot it does not fill*, which is true), so this was an architecture-doc overstatement and needs no erratum against canon.
- **The one-local latch depends on the ledger's meaning.** If a future change ever increments `matchedRequired`/`missedRequired` outside the four required sites, or adds a required site that increments neither, the latch silently misidentifies "first". T6 and T7 pin the behaviour, not the mechanism, so they would catch it.
- **DR-2 rests on one test.** The corpus cannot discriminate literal-leading from slot-leading. If T7 is weak or wrong, nothing else covers uniformity.
- **The Editor diagnostic goes quiet on barred rounds** (§3.2). Forced by DR-3, carried to G2 as requirements §8.4.

## 9. Open questions

Inherited from requirements §8, unchanged: **§8.4** (the barred round's invisibility in the Editor diagnostic — leaning accept, remedy sketched for item 3) and **§8.5** (whether an all-optional pattern is constructible at all — F3 is safe under both answers). **§8.6**, the vacuous design-branch step, is bookkeeping.

One added here:

### 9.1 Whether T7's slot-leading fixture should also be added to the corpus — OPEN, leaning no

Design §7.4 records that the 699-row corpus contains no slot-leading pattern, which is why DR-2 is ruled on principle. Adding one would give the arm-to-arm comparison an opinion about DR-2 — but it would also change the corpus, invalidating the byte-identical validation that every prior arm rests on (`corpus-bar-off.tsv`, `corpus-bar-on.tsv`). Leaning **no**: the corpus is a fixed instrument with saved baselines, and T7 is the right place for a claim the corpus was never built to carry. Raised at G2 rather than decided silently.

## 10. Build plan

### Phase 1 — persisted plan (2026-08-27)

**Approved by the human 2026-08-27**, after `plan-validator` review; every finding folded in before approval (B1–B3, M1, M3, M4, m1–m4). This feature is a single coherent phase, so there is one plan, not a sequence of phase plans.

Persisted verbatim from plan mode, which is otherwise lost at session end. Source plan file: `/home/seraphim/.claude/plans/floating-drifting-peach.md`.

> **Step 0 is already DONE** (working tree repaired, `git status --porcelain` = 0 entries). It is kept below for the record.

## Step 0 — repair the working tree

An interrupted test run left the repo dirty. The damage is larger than one file: Unity generated **60** `.meta` files inside the imported tree, and `.meta` is **not** gitignored here (none were ever tracked). Removing only the root `Tests.meta` leaves 60 `?? Tests~/…` entries and the gate below can never be met.

```bash
cd "/mnt/d/Game Development/VoXR-Speech-Recognition"
mv Tests Tests~
find Tests~ -name '*.meta' -delete      # the 60 generated orphans
find . -maxdepth 1 -name 'Tests.meta' -delete
git status --short                       # must be empty
```

Host manifest was already correct and untouched — nothing to restore there.

**Gate:** `git status --short` is empty. Do not start Phase 1 until it is.

---

#### Phase 1 — Seam A: the flag and the latch

`Runtime/Commands/VoxrCommandParser.cs`

1. **`MatchResult` (`:2926`)** — add one `bool LeadingRequiredMissed;` after `MissedRequired` (`:2985`), commented in the tone of the two existing completeness flags. Exactly one new field.
2. **`TryMatchScored` (`:3153`)** — one local beside `missedRequired` (`:3173`): `bool leadingRequiredMissed = false;`
3. **Two latch sites**, each *before* the existing increment, at `:3234` (required **slot** miss) and `:3280` (required **literal** miss):
   ```csharp
   if (matchedRequired == 0 && missedRequired == 0)
       leadingRequiredMissed = true;
   ```
   The two **match** sites (`:3227`, `:3268`) need nothing.
4. **`return new MatchResult { … }` (`:3323`)** — add `LeadingRequiredMissed = leadingRequiredMissed,`.

**Why one local.** Verified exhaustively: exactly four increment sites in the whole 4512-line file, and **no optional branch touches either counter** (matched optional slot is gated `if (!isOptional)` at `:3226`; the optional-literal branch touches neither in either outcome). So the condition *is* "no required element reached yet", and design §9's third trap — "the latch must skip optionals" — becomes structurally impossible rather than a convention. Reasoning and the rejected two-local alternative are recorded in **architecture §1.1 and §2.2**; that doc, not this plan, is the record.

Two traced cases confirm it: *optional slot matched → required literal missed* correctly **flags** (optionals are skipped when locating the first required element); *required slot matched → required literal missed* correctly **does not**.

**Leave alone:** the early `return default;` at `:3201`. The flag lands `false`; that result scores 0 and is floored by `CompareCandidate:3039` in both loops. A latch there is dead code. **Do not gate on `forStartProbe`** — the probe computes it and nothing reads it.

---

#### Phase 2 — Seam B: the flush suppression (DR-3)

Same file, `ParseInternal`.

1. `bool bestLeadingRequiredMissed = false;` with the other `best*` locals (`:2278-2285`, correctly inside the `while` so it resets per round).
2. Assign at the adopt site in the run `:2356-2363`, beside `:2362`. Unconditional on adopt.
3. Insert **immediately after** the progress guard (`:2501-2502`), **above** `ComputeConfidence` (`:2506`):
   ```csharp
   if (bestLeadingRequiredMissed)
   {
       searchStart = bestEndIdx;
       continue;
   }
   ```
   **No second progress guard** — `:2501` already proved `bestEndIdx > searchStart`.

**Why this placement.** *(Corrected 2026-08-27 — the original ranking below was wrong; see §8.)* The load-bearing reason is **DR-3, which is locked**, supported by allocation. The "correctness" argument as first written — that suppressing below the `_tiedSiblingBuf` write (`:2537`, before `_resultCount++` at `:2552`) would leave a tie record aligned to a result slot that never arrives, and therefore produce wrong output — is **false**: such a record is overwritten by the next emitting round or sits past `_resultCount` where nothing reads it. Keeping the write out of a barred round is still worth doing because it is free at this placement. On allocation: `ComputeConfidence` allocates nothing, and `VoxrCommand`/`VoxrCommandResult`/`VoxrSlotMatch` are all `readonly struct`, so in a player build with a literal-only winner and `_recordSiblingTies` off, nothing below the insertion point heap-allocates. It still allocates in the Editor and on any slot-carrying winner, and the placement is still right.

**Accepted consequence:** the Editor diagnostic block (`:2553-2580`) is *below* the emit, so a barred round records **no diagnostic entry**. Forced — keeping it demands the rejected placement. Carried to G2 as requirements §8.4.

---

#### Phase 3 — Seam C: the eager refusal (DR-5)

Same file, `TryEagerCommit`. **This is the one place fork A differs from fork B in reachability.** Left alone, a leading-missed candidate commits early and fires **while every flush-path test stays green**.

1. `bool bestLeadingRequiredMissed = false;` with its two siblings (`:3966-3967`).
2. Assign at the eager adopt site (`:4018-4019`, in the run `:4011-4019`).
3. Refusal immediately after the `bestHasUnmatchedRequiredTail` refusal (`:4087-4088`) — the 5th of 10 gates, ahead of the whole-buffer check at `:4108`. Two physical lines (CSharpier house form):
   ```csharp
   if (bestLeadingRequiredMissed)
       return EagerCommitVerdict.None;
   ```
4. **Amend the comment at `:4039-4043`** to say the bar is *not* inherited from the comparator and the condition below is load-bearing, not defensive.
5. **Amend `TryEagerCommit_LeadingTwoElementTie_RefusesForScoreAtDefaultAndForTheTieBelowIt`** (`VoxrEagerCommitTests.cs:1533`). On `["fire"]` against `cease_fire`/`resume_fire`, both candidates are now leading-missed, so the **new gate pre-empts the sibling refusal at `:4157`**. The test stays green while its message — *"below the default the tie is live, and the sibling condition is what refuses"* — becomes false, and the only default-grammar coverage of DR-5's sibling refusal at a *leading* discriminator silently evaporates. Update the message and comment to record the domination, matching the house idiom already at `VoxrCommandParser.cs:3236` (*"Dominated as an eager-REFUSAL cause…"*). If a leading-discriminator sibling case is still wanted, it needs a fixture the bar does not reach.

---

#### Phase 4 — Seam D: tests

`Tests~/Runtime/VoxrCommandParserTests.cs` (T1, T2, T4–T9) and `Tests~/Runtime/VoxrEagerCommitTests.cs` (T3).

| | test | pins |
|---|---|---|
| **T1** | **Transform + rename** `Coverage_SequentialExtraction_SparesTheFirstCommandWhenASecondLosesItsWord` (`:2462`) → `…SparesTheFirstCommandAndBarsTheSecond`. **Four** assertion lines change, not three: `:2486` `2`→`1`, and `:2494`, `:2495`, `:2496` all index `results[1]` and must go — leaving `:2494` in place throws `IndexOutOfRangeException`. **`:2488-2493` (`cease_fire`=1.0000 and its message) survives character-identical** — it is #82's guard. Control unedited. | DR-9, G-2 |
| **T2** | **NEW** — leading debris: `"target hotel one cease fire"` → `n=1`, `cease_fire`=1.0000. | §5.8 gap, §10 |
| **T3** | **NEW** — `TryEagerCommit_LeadingRequiredMiss_ReturnsNone`, in `VoxrEagerCommitTests.cs`. Fixture must register **no sibling** (a lone command/pattern), or `:4157` confounds the isolation. | DR-5, §10 |
| **T4** | **NEW** — the reported #124 grammar: phantom gone, round-1 shape `n=0`, genuine command 1.0000. | G-1 |
| **T5** | **NEW** — `decelerate by {burn_level}` on `"decelerate hard burn"` → 0.6667. `Coverage_SequentialExtraction_ChargesNothingForALaterCommand` (`:2432`) stays green **untouched**. | G-3 |
| **T6** | **NEW** — optionals both ways: leading unmatched optional fires normally; same pattern missing its first *required* element is barred. | F3 |
| **T7** | **NEW** — slot-leading pattern, six required elements, leading slot missed → `Parse` returns nothing. ~~Plus a control asserting the same fixture scores `4/6` today.~~ **CORRECTED 2026-08-27: that control is unbuildable** — if `Parse` returns nothing for the fixture, no assertion can observe `4/6` on it, and a barred round records no Editor diagnostic either (§8.4). Built instead as a **different utterance on the same grammar**: anchor present, one later element dropped → `5/6`, fires. That is what actually rules out a mis-built grammar passing vacuously. See the scope note below. | DR-2 |
| **T8** | **Extend the alloc pin** — the exemplar `EagerSelection_OverASiblingTie_AllocatesNothingPerCall` is at **`VoxrCommandParserTests.cs:4704`**, not in `VoxrEagerCommitTests.cs`, and the `Is.Not.AllocatingGCMemory()` constraint needs `using Is = UnityEngine.TestTools.Constraints.Is;` (`VoxrCommandParserTests.cs:12`) which `VoxrEagerCommitTests.cs` lacks. Keep T8 in `VoxrCommandParserTests.cs`. Its `["set","on"]` fixture is a **medial** miss, so the new gate does not shadow it. | G-5 |
| **T9** | **NEW — DR-8**, which the first draft omitted entirely though canon §9 scopes this feature as "DR-1/2/3/5/6/8". A leading-missed candidate on an `allowPartialMatch` command previously produced a result that fed `EnterPending`; under the bar it produces none, so no pending opens. ~~Pin at `Parse` level.~~ **CORRECTED 2026-08-27: a `Parse`-level pin cannot pin DR-8 at all** — pending lives in `VoxrCommandRecogniser`, so at `Parse` level this collapses to "Parse returns nothing", which T4's round-1 row already asserts. Built instead in `VoxrPendingCommandTests.cs` as `LeadingRequiredMiss_OpensNoPending`: `"missiles target"` against `launch {weapon} target {target}` with `allowPartialMatch` — matched 2 / missed 2, admitted at 0.25, `{target}` unfilled, so today it opens a pending for a command whose verb was never spoken. Asserts `HasPendingCommand == false` plus `OnUnrecognisedSpeech`, with a spoken-verb control. Also corrects **§3.4** (done). | DR-8 |

**T-existing — `MissedLiteral_TwoElementPattern_StillRejected` (`:3170`) goes red, and the first draft did not predict it.** Grammar `cease_fire: ["cease","fire"]` on `"fire"`: `"cease"` is the first required element and misses, so the round is barred, `Parse` returns zero, and `ParseOne`'s length assertion (`:71`) fails.

- **No user-visible behaviour changes.** It scored 0.50, below the 0.60 gate, so nothing fired before and nothing fires now. This is the reporting layer, exactly where DR-3 puts the suppression — an expected consequence of the ruled design, not a regression.
- **Transform it** to assert zero results, renamed to say the bar refuses it by *position* before score is ever consulted.
- **Add a companion pinning the 0.50 floor** on a *trailing* miss (`cease fire` heard as `"cease"` → `(1+0)/2` = 0.50, leading element intact, bar not tripped), so the published number stays pinned by the suite.
- **Flag for item 2:** `Documentation~/scoring.md` §1 publishes this row as `cease fire` / "fire" → 0.50 / rejected. The number survives; the *mechanism* no longer does. Canon DR-9 scopes doc work to §7 D only, so §1 is unowned — record it, do not fix it here.

**Two instruments are broken and must not be used:** `Parse` can never be pinned at zero allocation (it allocates its results, plus the Editor diagnostic list), and `GC.GetAllocatedBytesForCurrentThread` is inert here.

**Mutation-verify T2 and T3** (F14). T3's mutation must fail **T3 alone**.

---

#### Phase 5 — CHANGELOG

`CHANGELOG.md` under `[Unreleased]`, long-form house style, marked as a breaking behaviour change, naming **no** version (DR-10's 2.0.0 is item 4's, via the `release` skill).

**Ruled this session: proceed here as a scheduling call**, over-riding canon §9's assignment to item 2. Rationale of record: §9 is a dependency-ordered work partition, not a decision record — no DR pins the changelog's branch — so moving a line item between branches is scheduling, not design amendment. Recorded in requirements §4.13/§8.3; item 2 **extends** the entry rather than originating it.

---

#### Verification

1. **`compile-check` — both suites.** EditMode baseline **133/133 `failed="0"`** at `b83b62f` (consistent with an independent count of 133 Editor `[Test]`/`[TestCase]` attributes). **PlayMode has no baseline** — it was never captured.
2. **Expected-value edits are now: T1 (four lines) + `MissedLiteral_TwoElementPattern_StillRejected` + the message on `…LeadingTwoElementTie…`.** Anything else that fails gets **investigated, not edited**.
3. **Watch one PlayMode fixture by name:** `Tests~/Runtime/VoxrWavReplayTests.cs:182` asserts `expectedIntent` against the real decoder, and the corpus contains `split_close_distance_safe_range_target_hotel_one.wav` — a mid-utterance-pause fixture whose second half is literally the stranded-tail shape the bar targets, carrying its own manifest note *"Expectation provisional"*. If the buffer window fails to merge on a given run, its behaviour changes under the bar with no baseline to appeal to.
4. **Rig `--bench` A/B** for G-5, from `Planning~/features/coverage-in-selection/ab-rig/`:
   ```bash
   ./stage.sh b83b62f  <scratch>/before
   ./stage.sh WORKTREE <scratch>/after
   # cut -f2 ../phase7-corpus.tsv | dotnet run --no-build -- --bench   (each side)
   ```
   Two staged trees give both arms with no test seam (DR-7). Validate `--grammar` against `NativeBridge~/harness/expectations.json` before trusting a delta.
5. **Deferred, human-only:** on-device Quest verification. No native-bridge or lifecycle code is touched — no ABI rebuild, no `.so` commit owed. Report as deferred, never as passed.

#### Close-out

`review-cycle` → commit → push → `gh pr create` → `review-pr` at **full** profile (standing grant) → present for **G2**. Never merge, never tag.

#### Out of scope

Fork C; `scoring.md` §7 D + `DocCheck` pin, `KNOWN_LIMITATIONS.md`, `command-recognition.md` (item 2); the construction warning (item 3); the version bump (item 4). No public surface, no serialised field, no constructor parameter, no test seam (DR-7). `minScore` and `coverageWeight` unchanged.

---

#### Canon-authority calls — both ruled this session

**1. CHANGELOG route — proceed here.** See Phase 5. No design branch, no re-lock.

**2. Canon §6.2 carries a factual error; DR-2 stands — record as erratum.** §6.2 argues the literal-vs-slot fork *"only bites for a slot-leading pattern with enough required elements to clear 0.60 while missing its slot — five or more."* That is false: `VoxrCommandRecogniser.cs:928` rejects any winner carrying an unfilled required slot **regardless of score** (`scoring.md` §5: *"A winner missing a required slot does not fire, whatever it scored"*), so such a pattern can never fire today at any length.

### ERRATUM E-1 — "an eager commit IS a fire" is false — RULED 2026-08-27 (human, at G2 review)

**Ruled: erratum plus an in-canon pointer, no design branch and no re-lock.** The premise is false wherever it appears; DR-5's ruled action is unaffected and stands on the buffer-protection rationale instead.

**Every location carrying the claim, so the correction is not left to be rediscovered:**

| doc | location | disposition |
|---|---|---|
| design (LOCKED) | §2.4 — *"would commit early and bypass the bar entirely"* | erratum note inserted after it |
| design (LOCKED) | §2.4 — *"An eager commit **is** a fire"* | covered by the same note |
| design (LOCKED) | §5.2 — *"bypass the bar entirely, defeating G-1"* | erratum note inserted after it |
| design (LOCKED) | **DR-5's own decision cell** — *"Under A the eager path would bypass the bar entirely and defeat G-1"* | HTML-comment pointer in the cell (this was the location the first erratum missed) |
| design (LOCKED) | §10 rulings recap — *"a hole that would have defeated G-1"* | inline correction appended |
| requirements | §4.3, §4.5 | corrected in place, erratum stated |
| architecture | §4.1, §8 | corrected in place, erratum stated |
| shipped source | the `TryEagerCommit` note and T3's comment | rewritten to the true mechanism |

No ruling text was edited in the locked design — only annotations that point at this record. **Fork C is the reader this exists for**: canon says it may collapse the two bar sites back into one, and it would scope that work wrong if it believed the eager path were the G-1 hole.

---

**Ruled: erratum, no design branch.** DR-2's ruling is implementable exactly as written — it was ruled on principle, explicitly *"not on numbers"* — and only a supporting sentence is wrong. Actions:

> **CORRECTED 2026-08-27 (review-pr on #125), and the correction matters because this erratum annotates an immutable document.** The falsifying sentence above is itself overstated. `VoxrCommandRecogniser.cs:929` is `if (cmd.Score < minScore || incomplete)`, and the very next block (`:931-948`) is an **escape hatch**: a command with `AllowPartialMatch` and unfilled slots is routed to `EnterPending`, not rejected — and a pending that is later completed fires through `InterpretResolution` → `OnCommandRecognised`. So "such a pattern can never fire today at any length" holds only where `AllowPartialMatch` is **off**. Where it is on, the pattern cannot fire *directly*, but can fire once the missing argument is supplied by a follow-up.
>
> §6.2's claim is still false, and for the reason originally given — the completeness gate gets there before length or score ever matter, so the fork does not "only bite at five or more" — but the supporting fact must carry its condition. DR-2 is unaffected either way.


- Record the erratum in **requirements §8** and **architecture §6.7**, quoting the false sentence and the source rule that falsifies it, so a future reader (especially one taking fork C, which revisits this reasoning) meets the correction rather than the claim.
- **Reframe T7.** It cannot be "the only evidence for DR-2" in the *firing* sense. It pins what is actually observable: element-type uniformity in `ParseInternal` — a slot-leading leading-miss yields no result. The one behaviour uniformity genuinely changes is on the **pending path**, which is T9's territory. Keep the six-element fixture and its control anyway: it keeps the test honest about scoring even though completeness, not score, is what refuses today.
- Carry to G2 in the PR body so the human sees it beside the code.


## Related

- Requirements: `Planning~/features/leading-miss-bar/requirements.md`
- Locked design: `Planning~/design-docs/leading-miss-bar.md` — DR-1, DR-2, DR-3, DR-5, DR-6, DR-7, DR-8; §2.2, §2.3, §2.4, §4.1, §5.1–5.4, §5.8, §6.1, §6.2, §7.2–7.5, §9 row 1, §10
- Successors: `feat-leading-miss-docs` (item 2), `feat-leading-miss-warning` (item 3), `chore/release-2.0.0` (item 4)
- Deferred sibling topic: **fork C**, the leading-coverage excusal (design §6.1, §10)
- Closes: issue #124
- Adjacent, not modified: #82, #65, #66, #70, #74, #41
