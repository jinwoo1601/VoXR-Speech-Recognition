# Design: Sibling Tie Disambiguation

**Branch:** `design-sibling-tie-disambiguation`
**Origin:** issue #74, split out of #65 at G2; raised by the independent review of PR #72.
**Status:** **G1 LOCKED 2026-08-15.** Rulings in §10. This document is immutable from here; a contradiction found during implementation reopens it on a new design branch and re-locks (see the workflow's "When implementation contradicts the design").

**Scope, agreed at G0 (2026-08-15):** the runtime disambiguation path **and** closing the construction-time warning gap, under **one shared definition of "sibling hazard"**. Designing the two halves apart would produce two rival definitions of the same grammar shape; §5.1 exists to prevent that.

---

## 1. Problem

Two patterns share every element but one:

```
mode_weapons     : ["switch", "to", "weapons"]
mode_navigation  : ["switch", "to", "navigation"]
```

The speaker says one. VOSK drops the discriminating word. The surviving transcript is `switch to`, and the evidence fits **both** patterns exactly equally — same start, same score `(1+1+0)/3 = 0.67`, same consumed span, same literal count. Selection exhausts every key it has and falls through to its last: the order the patterns were registered in. `mode_weapons` fires because it happens to be first.

The word that would have decided is precisely the word that went missing. **No scorer can recover the intent** — the evidence is not merely weak, it is absent. Reducing the required-literal miss cost (#65 §5.1) extended this from four-element patterns down to three, which is where two-word-prefix grammars live, so the shape is now common rather than exotic.

This is not a scoring bug and this design does not propose a scoring change. The parser's arithmetic is correct; the problem is that a correct tie is resolved by a coin flip, silently, and the coin flip is *visible to the parser at the moment it happens*.

### 1.1 Why refusing is not the whole answer

On the eager path a refusal is cheap — the missing word may still arrive, so declining to commit costs only latency. #70's tail guard already does exactly this.

On the **flush** path the transcript is final. Refusing there means firing *nothing*, which is the exact failure class #65 exists to rescue. That asymmetry is why #70's guard does not generalise to flush, and it is why this design uses a third option instead of the binary of guess-or-refuse: **ask**.

The package already owns the machinery for asking. `requiresConfirmation` and `OnCommandPending` exist and are load-bearing. "Did you mean weapons or navigation?" is a strictly better answer than a coin flip, and the parser already knows it is holding a coin.

---

## 2. Verified current state

Every claim below was read from source on this branch (off `main` at `ef84690`). §2.8 is the one item reasoned from source rather than observed, and is flagged as such.

### 2.1 Selection and its keys

`IsBetterCandidate` — `Runtime/Commands/VoxrCommandParser.cs:1105`. After the DR-7 admission rule (`MissedRequired > MatchedRequired` ⇒ refuse), the comparison keys run in strict order:

| Priority | Key | Direction |
|---|---|---|
| 1 | `startIdx` | lower wins |
| 2 | `Score` | higher wins |
| 3 | `ConsumedEndIdx` | higher wins |
| 4 | `LiteralCount` | higher wins |

The function ends at:

```csharp
return candidate.LiteralCount > bestLiteralCount;
```

A **strict `>`**. On a full tie every key compares equal, the final line returns `false`, and the incumbent — whichever candidate was seen first — silently keeps the win.

Two facts follow, and both constrain the design:

- **The comparator returns `bool`.** It can say "better" or "not better". It has no way to say "exactly equal". A caller therefore cannot distinguish *a tie* from *a clear loss*. Detecting the tie requires a third state (§5.3).
- **The tie is never recorded.** No field, no diagnostic, no event. The information exists for the duration of one comparison and is discarded.

### 2.2 What "registration order" actually means

Both selection loops nest `ci` (command) → `pi` (pattern) → `startIdx` (token). Because `startIdx` is the *highest-priority* key but the *innermost* loop, the effective fallback among candidates that tie on start is the `(ci, pi)` enumeration order — i.e. the order commands were registered, then pattern order within a command. This is deterministic and is the basis of the documented authoring workaround ("register the safer one first").

### 2.3 Two call sites, deliberately sharing one comparator

- **Flush path** — `ParseInternal`, call at `VoxrCommandParser.cs:790`.
- **Eager path** — `TryEagerCommit`, call at `VoxrCommandParser.cs:1997`.

The comment at `:1088-1089` states the reason they share: *"so the eager verdict always names the pattern the subsequent flush will fire."* This is an invariant, not an incidental. Any tie-awareness added to the comparator must either preserve it or restate it explicitly — see DR-5 and §5.8.

### 2.4 Coverage does not break the tie — verified

The score is `normalizedScore = rawScore / (denominator + coverage)` (`:1345`), with coverage weighted by `_coverageWeight` (`:1342`). Coverage charges orphaned and skipped tokens.

Two tied siblings match the same tokens over the same span and strand the same orphans, so their coverage terms are **identical** and cancel. The issue's claim that `feat-coverage-in-selection` does not break this tie is **confirmed**, and the stale comment at `:1141` ("the coverage term that will later enter the score") predates the merge.

### 2.5 The pending machinery, and where it does not fit

`Runtime/Commands/PendingCommandHandler.cs` and `Runtime/Commands/VoxrPendingCommand.cs`:

```csharp
internal enum VoxrPendingReason { PartialMatch, AwaitingConfirmation }

internal struct VoxrPendingCommand
{
    public VoxrCommand Command;                 // ← exactly one
    public VoxrCommandDefinition Definition;    // ← exactly one
    public string[] UnfilledSlots;
    public VoxrPendingReason Reason;
    public float CreatedTime;
}
```

Three mismatches with what a tie needs:

1. **The struct holds one candidate.** An ambiguous tie is inherently two or more. There is nowhere to put the rival. "Reuse the pending path as-is" — open question 2 in the issue — is therefore **not free**; the type must widen or gain a sibling.
2. **The vocabulary is yes/no, not choice.** `VoxrFollowUpVocabulary` offers `DefaultConfirm` (`confirm/affirmative/yes/go ahead/do it`) and `DefaultCancel` (`cancel/abort/negative/belay that/never mind`). These answer *"do it?"*. They cannot answer *"weapons or navigation?"*. Note also that `MatchPhraseAgainstTokens` (`PendingCommandHandler.cs:289`) requires the phrase to consume **all** tokens — it is an exact whole-utterance match, not a substring search.
3. **`VoxrPendingTimeoutBehavior` is public** with values `Cancel` and `FireAsIs`. Under a tie, `FireAsIs` has no defined meaning — fire *which* one? This is shipped public API, so whatever we decide is a compatibility question, not just a semantic one (DR-6).

### 2.6 The public surface that constrains us

```
OnCommandRecognised(VoxrCommand)     OnCommandPending(VoxrCommand)
OnCommandsRecognised(VoxrCommand[])  OnCommandConfirmed(VoxrCommand)
OnUnrecognisedSpeech(string)         OnCommandCancelled(VoxrCommand)
```

`VoxrCommand` (public readonly struct): `Intent`, `Slots`, `Confidence`, `Score`, `RawText`, `MatchedPatternIndex`. **No verdict or reason field.** `VoxrCommandResult`: `IsMatch`, `Command`, `RawText`.

There is no event for "I am not sure", and no field on which to hang one. `VoxrMatchDiagnostics` exists but is `#if UNITY_EDITOR` and `internal` — a fine home for an editor-only tie diagnostic, useless as a runtime signal.

### 2.7 The construction-time warning, and the gap it leaves

`WarnOnDroppableRequiredLiteral` — `VoxrCommandParser.cs:534`. For every ordered pair of expanded pattern forms it requires **all three**:

```csharp
if (extended.Length <= bare.Length) continue;   // (a) strictly longer
if (!IsElementPrefix(bare, extended)) continue; // (b) bare is an element-prefix
...                                             // (c) ≥1 required literal added,
if (requiredLiterals == 0 || k >= extended.Length) continue;  //   and a SLOT after it
```

**Issue #74's shape satisfies none of them.** `["switch","to","weapons"]` vs `["switch","to","navigation"]` are equal length (fails a), neither is a prefix of the other (fails b), and the divergent element is a trailing **literal** with no slot behind it (fails c).

So the issue's own premise — *"the parser already detects sibling hazards at construction (#42's warning), so it could warn rather than act"* — **is wrong**. The existing warning detects a *different* hazard: a stranded **slot**. The #74 shape is, today, entirely unwarned. This is the gap the agreed scope requires us to close, and it is why §5.1 defines the sibling relation once for both halves rather than bolting a second scan alongside the first.

### 2.8 The eager gate does not cover a *medial* discriminator

> **Reasoned from source, not observed.** Backlog item 1 must land a test that confirms or refutes this before item 2 builds on it (§7.5). If it is refuted, DR-5 loses its motivation and should be reconsidered on a new design branch.

Issue #74 reports a **trailing** discriminator. A **medial** one is the same shape structurally:

```
["set", "{ship}", "mode",  "on"]
["set", "{ship}", "level", "on"]
```

Speaker says one, "mode" is dropped, transcript is `set alpha on`. Walking `TryEagerCommit`'s guards in order:

| Guard | Outcome |
|---|---|
| `bestMissedRequiredSlot` | **false** — the discriminator is a literal, not a slot |
| `bestHasUnmatchedRequiredTail` (#70) | **false** — the miss is medial; the subsequent match of `"on"` resets `requiredAfterLastMatch` |
| whole-buffer span | **satisfied** — the match covers `set alpha on` exactly |
| `minScore` | `2.0 / 2.5 = 0.8` — comfortably above default |

Both siblings reach `0.8` with identical span and identical matched-literal count. The tie resolves to registration order and **the eager gate commits the wrong sibling early**.

The comment at `:2060` calls a medial miss "genuinely safe", and it is — for **completeness**, the question #70 was written to answer ("nothing the speaker says next was owed to it"). It is silent on **ambiguity**. Nothing in the eager path tests for that.

The consequence sets the shape of this design: **the medial case is worse than the trailing case the issue reported.** The trailing case is at least refused on the eager path by #70's guard; the medial case is unguarded on *both* paths. A sibling definition drawn around the reported example would exclude the more dangerous variant (DR-1).

---

## 3. Goals and non-goals

**Goals**

- G-1. The parser detects, at selection, that the winner was decided by registration order alone — on **both** the flush and eager paths.
- G-2. On flush, where the transcript is final, it asks the speaker instead of guessing.
- G-3. On eager, where more speech may still arrive, it declines to commit and defers to flush.
- G-4. The grammar shape that produces the tie is reported to the author **at construction**, so it can be designed out before it ever ships.
- G-5. One definition of "sibling hazard" serves G-1 and G-4.
- G-6. Existing integrations keep working. Nothing that fires today stops firing.

**Non-goals**

- **Not a scoring change.** §5 touches selection's *reporting*, not its arithmetic. The keys and their order stay exactly as they are.
- **Not dropped-word recovery.** We cannot recover the missing word and do not try. We route around the loss.
- **Not a general n-best API.** Exposing full candidate rankings is a much larger surface; §6.4 rejects it for this topic.
- **Not a fix for author-duplicated patterns.** Two genuinely identical patterns are an authoring error, reported by the warning, not disambiguated at runtime.
- **Not a change to #70's tail guard.** §5.8 adds a guard beside it; it does not modify it.

---

## 4. Diagnosis

One root cause, surfacing in three places.

A grammar carries a **discriminator of last resort** when two patterns are separated only by an element that carries no structural weight — a single required literal that is the sole difference between two intents. Such a grammar is *fragile by construction*: it survives only as long as the recogniser never drops that one word.

The parser encounters this fragility three times, and today handles none of them:

| Where | State today |
|---|---|
| **Construction** — the shape is statically visible and cheap to detect | Blind to it (§2.7) |
| **Eager selection** — the tie is in local variables; more speech may still arrive | Silent; commits early on a medial discriminator (§2.8) |
| **Flush selection** — the tie is in local variables; the transcript is final | Silent; fires the first-registered (§2.1) |

All three are failures of *reporting*, not of *scoring*. The arithmetic reached the right answer — "these are indistinguishable" — and then discarded that answer and returned a guess dressed as a decision.

The fix, in one sentence: **compute the sibling relation once at construction; let the warning report it, let the eager gate refuse on it, and let flush ask about it.**

---

## 5. The model

### 5.1 Sibling sets, computed once at construction

Define, over the same expanded pattern forms `WarningForms` already produces:

> Two pattern forms are **siblings** when they have equal length, are element-wise equal at every position but one, and that one position holds a **required literal** in both.

The differing position is the **discriminator**; the two literals are the **discriminating values**. Per DR-1 the discriminator may sit at **any** position, not only the last.

Properties that make this the right shared primitive:

- It is **static** — computable in the constructor, alongside the existing scan, over the same expanded forms.
- It is **cheap at runtime** — selection consults a precomputed lookup keyed by `(commandIdx, patternIdx)`; it never compares patterns during parsing.
- It **generalises to n-ary sets** — `set auto pilot on` / `off` / `standby` form one sibling set with three discriminating values, not three pairs.
- It **yields the disambiguation vocabulary for free** (§5.5). The discriminating values *are* the choices.

**Cost.** This adds constructor work on every parser rebuild, which an editor session performs often. It is the same order as the existing scan — `O(patterns² × forms²)`, bounded by `MaxWarningExpansion = 6` — so it changes nothing asymptotically, but it is not free and §7.3 measures it.

This definition deliberately does **not** subsume `WarnOnDroppableRequiredLiteral`'s stranded-slot hazard. That scan stays as it is; the two are different shapes with different consequences, and merging them would blur both messages. What they share after this design is the vocabulary the author reads, not the code path.

### 5.2 The warning half (goal G-4)

Extend the construction-time scan to emit a distinct **Editor-only** warning for every sibling **set** found — once per set, not once per pair — naming the intents, the shared frame, and the discriminating values:

```
[VoxrCommandParser] Intents 'mode_weapons' and 'mode_navigation' differ only at
element 3 ("weapons" vs "navigation"). If that word is dropped, both patterns match
the remainder equally and selection falls through to registration order — the wrong
intent can fire. Diverge earlier, mark the more destructive one requiresConfirmation,
or enable ambiguity disambiguation (<flag name>).
```

Editor-only follows the precedent #81 set for `WarnOnDroppableRequiredLiteral`, and the once-per-set dedup follows that scan's existing `reported` HashSet.

**The volume risk is real and is this half's main hazard.** This shape is *extremely* normal authoring — every `on`/`off` pair and every mode switch is a sibling set — so the warning may fire on a large fraction of perfectly healthy grammars, which is exactly the noise problem #81 just spent effort fixing.

It cannot be narrowed by pattern length, because the arithmetic runs the wrong way: a longer shared frame yields a *higher* score after one miss (`0.875` for an 8-element pattern versus `0.67` for a 3-element one), so long patterns are **more** likely to tie-and-fire, not less. Length is therefore not a usable filter.

The mitigations available are the three already applied — once per set, Editor-only, an actionable message naming the opt-in flag — plus measurement: §7.3 gates this on observed volume, and if volume is bad the warning is downgraded to opt-in rather than shipped noisy.

### 5.3 Tie detection at selection (goal G-1)

`IsBetterCandidate` cannot express a tie (§2.1). Per DR-3 its return type changes from `bool` to a three-state result — `Better` / `Tied` / `Worse` — and both call sites consume the new state.

The alternative, an equality probe at the call site, was rejected because it duplicates the key list, and that list has already changed twice (#41 added span, #65 added the admission rule). A key added to the comparator and forgotten in the probe is a silent divergence. With DR-5 putting tie-awareness on *both* paths, the probe would have had to be maintained in two places.

Each selection loop then records, alongside the winner, whether an equally-good rival was seen and which pattern it was. Restricting the *action* to sibling rivals (§5.1) keeps non-sibling ties — authoring errors, not speech ambiguity — out of the runtime paths, while leaving them visible to the editor diagnostic.

### 5.4 Carrying rivals through pending

Per DR-4, `VoxrPendingCommand` gains an alternatives field and `VoxrPendingReason` gains a third value, `AwaitingDisambiguation`. Both types are `internal`, so this breaks no public contract, and every existing path ignores the new field.

A parallel `VoxrAmbiguousPending` type was rejected: the state machine's hard parts — timeout, cancel-on-release (`VoxrPushToTalkController.cs:103`), re-entry, disable-safety — are already solved once in `PendingCommandHandler` and should not be solved twice.

### 5.5 The disambiguation vocabulary

The confirm/cancel vocabulary cannot pick between two options (§2.5). §5.1 supplies the answer: **the discriminating values are the choice vocabulary**. The speaker says the word that got dropped.

```
speaker: "switch to [navigation]"   ← dropped
parser : ambiguous — weapons or navigation?
speaker: "navigation"
parser : fires mode_navigation
```

This needs no new authoring, no new vocabulary constant, and no ordinal indirection. It reuses `IsVocabularyMatchTokens` against a per-pending array built from the sibling set.

**A full re-utterance is also accepted** ("switch to navigation"), by re-parsing the follow-up normally and preferring an unambiguous result. It costs little and catches the natural correction.

**Ordinals were rejected.** "First" / "second" is meaningless to a speaker who has no idea what order the patterns were registered in; it only works if the integrator renders a numbered prompt, and this package ships no TTS, so prompting is entirely the integrator's job. The discriminating values work regardless of how the integrator words the question.

**Cancel vocabulary continues to apply; confirm vocabulary does not** — "yes" is not an answer to "which?".

**Discriminator/cancel collision.** `TryHandleConfirmCancel` checks cancel before confirm, so a discriminating value that collides with the cancel vocabulary (a grammar with a literal `negative`, say) would be swallowed by cancel and the choice made unreachable. Cancel keeps precedence — safety wins — and the collision is reported by the same construction scan that builds the sibling set, so the author learns about it at build time rather than in the field.

### 5.6 Timeout semantics

Per DR-6, under `AwaitingDisambiguation` the public `VoxrPendingTimeoutBehavior.FireAsIs` **degrades to `Cancel`**: an unanswered ambiguity fires nothing.

The reasoning is semantic. `FireAsIs` today means *"the intent is known, fire it with the slots I have."* Under ambiguity the **intent itself** is unknown. Those are categorically different situations wearing one flag, and firing the first-registered after a pause would conflate them. It is also the only reading coherent with DR-7: an integrator who opts into disambiguation has explicitly said *don't coin-flip*, and firing the first-registered on timeout coin-flips anyway, merely later.

A new enum value was rejected as public API for an edge case; it can be added later without breaking anything, so it is not added now.

### 5.7 Rollout

Per DR-7: the **warning ships on by default** (Editor-only, §5.2); **runtime disambiguation is opt-in** for the first release.

The decisive argument for opt-in is a failure mode, not a preference. If disambiguation were default-on and an integration does not subscribe to `OnCommandPending` — which any integration that never uses `requiresConfirmation` has no reason to do — then an ambiguous utterance enters pending, nothing prompts the user, the timeout expires, and **nothing fires**. Default-on would silently degrade those integrations from "fires a possibly-wrong command" to "fires nothing at all", which is a worse outcome than the defect being fixed. That is a direct violation of G-6.

Default-on is reconsidered only after the warning has been in the field, as a separate design topic. The warning is the part that is safe on by default, and it is also what makes the opt-in discoverable — which is why §5.2's message names the flag.

### 5.8 The eager gate refuses on a sibling tie (goal G-3)

Per DR-5, and motivated entirely by §2.8: when eager selection detects a sibling tie, `TryEagerCommit` returns `EagerCommitVerdict.None`.

This is cheap and correct there for the reason §1.1 gives — on the eager path the missing word may still arrive, so refusing costs only latency. It is the natural extension of #70's guard to the case #70 structurally cannot reach, and it sits **beside** that guard rather than modifying it.

**The `:1088` invariant is restated, not broken.** The old wording — *"the eager verdict always names the pattern the subsequent flush will fire"* — assumed flush always fires something. It now reads: *the eager verdict never names a pattern the flush would not fire.* A refusal names nothing, exactly as #70's guard already does.

**This guard is not behind the opt-in flag,** and the reason is that it is safe in both configurations:

- **Disambiguation off:** eager refuses → flush selects → tie → first-registered fires. Same intent as today, reached slightly later.
- **Disambiguation on:** eager refuses → flush detects the tie → asks.

So the only unconditional behavioural change is eager **timing** on a medial sibling tie, with the fired intent unchanged. §7.2 measures that.

---

## 6. Forks considered and rejected

**6.1 Break the tie with a better scorer.** Rejected on the issue's own reasoning, confirmed in §2.4: the discriminating evidence is *absent*, not weak. Coverage, confidence, and span are all identical by construction. There is nothing left to measure.

**6.2 Refuse on any tie, on both paths.** Rejected per §1.1. On the flush path refusing means firing nothing, which is the regression class #65 was built to fix. The refusal is correct only on eager, which is what §5.8 does.

**6.3 Warn at construction only, no runtime path.** A live option at G0, not chosen. Recorded because it remains the fallback if §5.3–5.8 prove too invasive: it needs no public API change and no pending surgery, and it still removes the hazard from any grammar whose author reads warnings. Its weakness is that it does nothing for grammars already shipped. Backlog item 1 delivers exactly this, which is why item 1 is independently valuable.

**6.4 Expose full n-best candidates and let the integrator decide.** Rejected for this topic. It is a much larger public surface (a candidate-ranking type, ordering guarantees, allocation strategy on a hot path) and it pushes the decision onto every integrator rather than solving it once. A worthwhile separate design if demand appears.

**6.5 Auto-select the less destructive sibling.** Requires a notion of destructiveness the package does not have; `requiresConfirmation` is the closest and is already the documented workaround. Rejected as guessing with extra steps.

**6.6 Restrict the sibling definition to the final element.** Rejected at G1 — see DR-1 and §2.8. It would exclude the medial case, which is the *more* dangerous of the two.

---

## 7. Measurement plan

The grammar A/B rig measures a change against the real decoder in WSL over the 699-utterance corpus. Its standing trap applies with full force: **a clean 699-corpus A/B is not evidence a selection change is safe** — the corpus was not built to contain sibling ties, so a null result means "the corpus is silent", not "no regression".

Required evidence before G2, per backlog item:

1. **A purpose-built fixture set.** Sibling-tie utterances with the discriminator deliberately elided, covering **both** trailing and medial discriminators. This is new fixture work, not a rerun of anything.
2. **A/B on the 699 corpus** to show the *absence* of change on non-sibling grammars, plus the eager-timing delta from §5.8. The null result is the point in this direction and is meaningful.
3. **Construction-time warning volume** measured across every grammar in `Tests~` and the samples, plus the added constructor cost from §5.1. A warning that fires on healthy grammars is worse than no warning — the lesson #81 just paid for. **This measurement gates DR-7's "warning on by default"**: if volume is bad, the warning ships opt-in instead.
4. **PlayMode tests** for the pending state machine's third reason — timeout under DR-6, cancel-on-release, re-entry — per the project's test bindings (most parser and command tests are PlayMode; an EditMode-only run misses them).
5. **A test confirming or refuting §2.8**, landed in backlog item 1 before item 2 builds on it. If the medial eager commit does not reproduce, DR-5 loses its motivation and must be reconsidered on a new design branch rather than quietly dropped.

---

## 8. Decision records — RATIFIED at G1, 2026-08-15

| # | Decision | Ratified | Rejected alternatives |
|---|---|---|---|
| DR-1 | Sibling definition | Equal length, differ at exactly one position, required literal in both — at **any** position | Final position only. Excludes the medial case, which §2.8 shows is the more dangerous one |
| DR-2 | Shared primitive | One sibling-set computation at construction, consumed by the warning, eager selection, and flush selection | Two independent detectors — produces rival definitions of one shape |
| DR-3 | Tie detection | Three-state comparator (`Better`/`Tied`/`Worse`) | Call-site equality probe — duplicates a key list that has already changed twice, now in two places |
| DR-4 | Pending & vocabulary | Widen `VoxrPendingCommand`; third `VoxrPendingReason.AwaitingDisambiguation`; the discriminating values are the vocabulary; full re-utterance also accepted; cancel still applies, confirm does not | Parallel pending type; ordinal vocabulary; re-utterance only |
| DR-5 | Eager path | The eager gate **refuses** on a sibling tie (§5.8). The `:1088` invariant is restated, not broken. Not behind the opt-in flag | Leave the eager path unchanged — leaves the medial case firing early and wrong |
| DR-6 | Timeout | Under `AwaitingDisambiguation`, `FireAsIs` degrades to `Cancel` | Fire the first-registered (conflates "unknown arguments" with "unknown intent"); new public enum value (API for an edge case) |
| DR-7 | Rollout | Warning **on** by default, Editor-only; runtime disambiguation **opt-in** for the first release | Both default-on — silently degrades integrations that do not handle `OnCommandPending` to firing nothing (violates G-6); both opt-in; warning only (§6.3) |

---

## 9. Feature backlog — canonical, locked at G1

Dependency-ordered. Each is one feature branch under the implementation track.

1. **`feat-sibling-set-detection`** — the §5.1 primitive computed at construction, plus the §5.2 Editor-only author warning and the §5.5 discriminator/cancel collision report. **Also lands the §2.8 confirming test** (§7.5), since everything downstream depends on that finding. Ships G-4 and G-5 on its own; no runtime behaviour change; no public API change. *Independently valuable — this is the whole of §6.3 if the rest is ever abandoned.*

2. **`feat-tie-aware-selection`** — DR-3's three-state comparator; both selection loops record a tied sibling rival; DR-5's eager refusal (§5.8). Depends on 1. The flush-side recording is inert here, consumed only by the editor diagnostic; **the eager refusal is a real, unconditional behaviour change** — same intent fired, deferred from eager to flush — which is why it gets its own review and its own A/B (§7.2) rather than riding along with item 3.

3. **`feat-disambiguation-pending`** — DR-4's third `VoxrPendingReason`, the widened `VoxrPendingCommand`, the choice vocabulary, DR-6's timeout semantics, and DR-7's opt-in flag. The flush-side behavioural change lands here, behind the flag. Depends on 2.

4. **`feat-disambiguation-docs`** — `Documentation~/command-recognition.md`, the `KNOWN_LIMITATIONS.md` entry rewritten from "documented limitation" to "documented limitation with a supported remedy", and `CHANGELOG.md`. Depends on 3. *Post-G2 product docs for 1–3 are written per the workflow at each feature's own close-out; this item covers the guide-level narrative spanning all three.*

Splitting 2 from 3 is deliberate: it isolates the change that touches a hot path shared with the eager gate, so a regression there is unambiguous.

---

## 10. G1 rulings

**Ruled by the human in conversation, 2026-08-15.** All seven decision records ratified as recommended; see the table in §8 for the ratified value and the rejected alternatives of each.

Two revisions were made between the draft presented at §8 and the rulings recorded here, both arising from the §2.8 finding surfaced while reasoning through DR-1:

- **DR-5** changed from *"eager path unchanged"* to *"the eager gate refuses on a sibling tie"*. The draft's rationale — that #70's tail guard already covers the in-progress case — holds only for a trailing discriminator.
- **DR-6** changed from *"`FireAsIs` fires the first-registered"* to *"`FireAsIs` degrades to `Cancel`"*, on the semantic argument in §5.6 and for coherence with DR-7's opt-in.

Both open questions carried in the draft are closed: §5.1's discriminator position is resolved by DR-1 (any position), and §5.5's re-utterance question by DR-4 (accepted).

**One finding is load-bearing and unconfirmed.** §2.8 is reasoned from source, not observed. Backlog item 1 must land a test that confirms or refutes it (§7.5). A refutation invalidates DR-5's motivation and reopens this document on a new design branch — it is not to be quietly dropped during implementation.

**Design track complete.** §9 is the canonical feature backlog. The implementation track picks item 1 when it is actually going to be built, per the workflow.

---

## Appendix — a note on this branch

`Planning~/` is gitignored (`.gitignore:44`), so this document is local-only and `design-sibling-tie-disambiguation` carries **zero diff** from `main`. The workflow's "merge the design branch to main" step is vacuous in this repo, exactly as it was for `scoring-model.md`. The branch is a bookkeeping marker, not a change set; nothing needs to be pushed or merged for this design to be locked.
