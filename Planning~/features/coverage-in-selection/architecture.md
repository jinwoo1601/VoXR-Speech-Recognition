---
type: architecture
feature: coverage-in-selection
topic: scoring-model
status: draft
updated: 2026-08-14
sources:
  - Planning~/features/coverage-in-selection/requirements.md
  - Planning~/design-docs/scoring-model.md
  - Runtime/Commands/VoxrCommandParser.cs
  - Runtime/Commands/VoxrCommandRecogniser.cs
  - Runtime/Testing/VoxrBatchTestRunner.cs
---

# Coverage In Selection — Architecture

## 1. Purpose & responsibilities

Charge a candidate for the in-grammar tokens it leaves unexplained on **both** sides of its match, and compute that charge **inside** selection so it can reorder candidates.

The feature is one formula change and one relocation. The formula gains a trailing term; the relocation is what makes the trailing term able to do its job. Everything else in this doc exists to make the relocation cheap enough to sit in the parser's innermost loop and safe enough not to break the four invariants that currently depend on coverage being applied *after* selection.

Realizes design §5.2 and DR-4; requirements F1–F18.

## 2. The shape of the change

Today (`VoxrCommandParser.cs:665-670`), coverage is a post-selection adjustment on the winner alone:

```
select winner by (start, score, span, literals, order)   ← score has no coverage
then: winner.score = rawScore / (denominator + skipped × weight)
```

After:

```
each candidate: score = rawScore / (denominator + (skippedBefore + orphanedAfter) × coverageWeight)
then: select winner by (start, score, span, literals, order)   ← score carries coverage
```

The post-selection block is deleted, not moved. Nothing recomputes a score after selection, which is what F1's acceptance check ("the score a candidate is selected on equals the score it is reported with") verifies.

## 3. Key decisions

### D1 — Coverage is computed inside `TryMatchScored`, not in the extraction loop

`TryMatchScored` already owns every other term in the score and returns the normalised value at `:1133`. Adding coverage there means **one** site computes the score and both callers — `ParseInternal`'s extraction loop (`:600-644`) and `TryEagerCommit`'s scan (`:1490-1520`) — inherit it identically.

**Alternative kept on record:** adjust `matchResult.Score` in each caller after `TryMatchScored` returns and before `IsBetterCandidate` is consulted. Rejected precisely because it is two sites. The invariant F12 protects — *an eager verdict names the command the subsequent flush actually fires* — currently holds because the two paths compute the same number; duplicating the coverage adjustment puts the one thing that must not drift in the two places most likely to. The design's own §5.4 leans on "both paths now compute the trailing term identically", and D1 is what makes that true by construction rather than by discipline.

**Cost:** `TryMatchScored` gains a `searchStart` parameter (for the leading term). It is a private method, so this is not an API change.

### D2 — Both coverage counts are O(1) lookups into per-utterance tables

Coverage sits inside a triple-nested loop (commands × patterns × start index). A per-candidate scan of the trailing tokens would multiply parse cost by utterance length — NFR §5 forbids it. Two tables, built once per utterance, reduce both counts to an array index:

**`_recognisedPrefix[i]`** — count of non-`[unk]` tokens in `tokens[0..i)`. Then:

```
skippedBefore = _recognisedPrefix[startIdx] − _recognisedPrefix[searchStart]
```

This is exactly what `CountRecognisedTokens(tokens, searchStart, startIdx)` computes today at `:667`, in O(1) instead of O(n). It does not depend on `searchStart`, so it is built once per utterance and reused across extraction rounds.

**`_orphanRun[i]`** — count of orphaned tokens in the run starting at `i`. Built backwards in one pass:

```
_orphanRun[n]  = 0
_orphanRun[i]  = 0                    if canStartPattern[i]     ← counting stops
               = _orphanRun[i+1]      if tokens[i] == "[unk]"   ← free, and does not stop the run
               = 1 + _orphanRun[i+1]  otherwise
```

Then `orphanedAfter = _orphanRun[matchResult.ConsumedEndIdx]`.

The `[unk]` line encodes both halves of F5 at once: `[unk]` is never charged, and it does not terminate the run either — it is **transparent** rather than a stopper. `"decelerate [unk] hard burn"` must therefore charge the same 2 orphans as `"decelerate hard burn"`.

> **Reason corrected 2026-08-14 by plan validation.** This originally justified transparency by "*it cannot begin a pattern (the extraction loop already skips `[unk]` start indices at `:607`)*". That is a non-sequitur — `:607` governs candidate *enumeration*, not the `canStartPattern` predicate. The conclusion is right for a different reason: `[unk]` is by construction the decoder's marker for a token it could not place in the grammar, so no pattern element can equal it, and treating it as a stopper would let a single noise token shield every real orphan behind it. The only theoretical exception — a grammar declaring `"[unk]"` as a slot value — is unreachable in practice and would be a grammar-authoring error.

Both tables are `int[]` fields grown on demand and reused, matching the existing `_matchSlotBuf` / `_resultBuf` pooling discipline — no per-utterance allocation (NFR §5).

### D3 — `canStartPattern` is derived from **active patterns**, never from admitted candidates

This is F11, and it is the decision that keeps DR-7 and coverage orthogonal.

The predicate answers *"could any registered active pattern begin a match at this token?"* It reads the grammar and the token, and nothing else. It must not consult `IsBetterCandidate`, `MissedRequired`/`MatchedRequired`, the `Score <= 0f` floor, or which candidates survived admission.

**Alternative kept on record and rejected:** define orphans against surviving candidates — *"did any admitted candidate begin here?"*. It is the tempting shortcut, because the selection loop already has candidates in hand and it looks more precise. It would couple the two rules: DR-7 rejecting a candidate would withdraw a pattern's claim on a token, turning that token into an orphan and lowering a *different* candidate's score. Coverage would then depend on DR-7's verdicts, and §A2.5's "orthogonal by construction" would quietly become false — a locked property broken by an implementation detail nobody would think to review. Requirements §9.1 works the argument through.

**Structure.** Two grammar-derived caches, built **eagerly in the constructor** alongside the other grammar-derived tables (`_slotNameCache`, `_optionalSlotElements`, `_optionalLiteralCache`):

- `_startLiterals` — `HashSet<string>` of every literal that can appear as a pattern's first matchable element.
- `_startSlots` — `int[]` of slot indices that can appear as a pattern's first matchable element.

> **CORRECTED 2026-08-14 by plan validation.** This originally read "built alongside the existing `_canCommitEarly` analysis (`:1614`) and invalidated on the same events". That is a **bug**, not a placement preference. `EnsureCanCommitEarly` (`:1445-1451`) is **lazy** and reached only from `CanCommitEarly` (`:1456`) and `TryEagerCommit` (`:1475`); with `eagerFlushOnCompleteMatch = false` — **the shipped default** (`VoxrCommandRecogniser.cs:55`) — the flush path never reaches either. Built as originally written, both caches would be empty on every default-configured parse, `canStartPattern[i]` would be false everywhere, **every** trailing token would be charged, and the parser would ship exactly fork F5's behaviour — which design §6 rejects outright — visible only on the non-eager path and therefore invisible to every eager test. `ComputeCanCommitEarly` also early-returns `null` at `:1629` past `MaxOptionalExpansion`, which would lose the caches for those grammars too. Constructor-time construction avoids all of it, and invalidation is automatic: `SetActiveSets` → `RebuildParserAndGrammar` → `RebuildParser` → `new VoxrCommandParser(...)` (`VoxrCommandRecogniser.cs:199-211, 265-275`), so a changed active set always yields a fresh parser and fresh caches.

"First matchable element" walks the pattern from index 0 forward, taking each element and continuing past it only while it is **optional** — an optional leading element may be omitted, so the element after it can also legitimately start the match. The walk stops at (and includes) the first required element.

An optional **literal** must be stored in its **stripped** form (`"?mark"` → `"mark"`, via `_optionalLiteralCache`); storing the raw element would contribute a token that can never match a real utterance and would silently weaken the predicate.

Per utterance, `canStartPattern[i]` = `_startLiterals.Contains(tokens[i])` or any slot in `_startSlots` matches at `i`. The slot test reuses `TryMatchSlot` / `TryMatchNumberSequence` — the same predicate the matcher itself uses, so the two cannot drift.

**Cost:** O(tokens) for the literal half; O(tokens × `_startSlots.Length`) for the slot half, which is zero for every pattern in this package's demo grammar (all are literal-initial) and small in practice.

### D4 — The predicate is deliberately conservative

Where "could a pattern begin here?" is uncertain, the answer is **yes** and nothing is charged.

Over-charging orphans is what fork F5 was rejected for: it destroys sequential extraction, turning `cease fire` in *"cease fire launch missiles target hotel one"* from `2/2 = 1.00` into `2/7 = 0.29`. Under-charging merely leaves a score higher than ideal — i.e. today's behaviour. The failure modes are not symmetric, so the bias is not either.

> **CORRECTED 2026-08-14 by review (ALT-1).** The asymmetry argued above held only while coverage sat *below* the selection barrier. Once coverage reorders, under-charging **one** candidate relative to its sibling is no longer "today's behaviour" — it is precisely the wrong-command class Amendment A3 was raised to close (§9.4): `mode_navigation` at `0.667` beating the correct `mode_weapons` at `0.600` because the shorter, wrong match was *under*-charged. The conservative bias remains correct as a default, but "the failure modes are not symmetric" is false in the reordering regime, and it is exactly the rationale a future admissibility probe would be argued against. Ruling stands, rationale corrected — the D5/D6 precedent.

**Consequence, to be reported not hidden (requirements §9.3):** a grammar with a slot-initial pattern over a permissive slot — `heading` is an open-ended `NumberSequence` — can make a large class of tokens a potential start, driving `orphanedAfter` to zero and silently disabling the trailing half of the feature for that grammar. That is safe (it reverts to pre-feature behaviour) but invisible to the author. If measurement confirms it, it goes in `KNOWN_LIMITATIONS.md` at G2 with the authoring note that a slot-initial pattern weakens coverage grammar-wide.

### D5 — DR-4's prescribed shim has no target; the decision stands, the mechanism is adapted

DR-4 specifies `[FormerlySerializedAs("skippedWordPenalty")]` **plus an `[Obsolete]` forwarding property for one minor version**. Checked against the code, the second half has nothing to forward from:

| Surface | Accessibility | Break? |
|---|---|---|
| `VoxrCommandRecogniser.skippedWordPenalty` (`:39`) | **private** `[SerializeField]` | Inspector/serialized only |
| `VoxrCommandParser.DefaultSkippedWordPenalty` (`:77`) | **internal** const on an **internal** type | No |
| `VoxrCommandParser(..., float skippedWordPenalty)` (`:151`) | `public` ctor on **`internal class`** (`:28`) | **No** — see correction |
| `VoxrBatchTestRunner(..., float skippedWordPenalty)` (`:23`, `:36`) | **public** ctor params on `public class` (`:14`) | **Yes** — named arguments |

> **CORRECTED 2026-08-14 by plan validation.** The parser row originally read "**public** ctor param — **Yes** — named arguments". `VoxrCommandParser` is declared `internal class` (`:28`), and `Runtime/AssemblyInfo.cs:9-11` grants `InternalsVisibleTo` to three first-party assemblies only. A `public` constructor on an `internal` type is not externally reachable, so no external caller can name that parameter. Left uncorrected, the `CHANGELOG` note DR-5 requires would have overstated the break by one surface.

There is no public property and no public constant. The serialized field is private, so `[FormerlySerializedAs]` fully covers it — inspector values survive, which is DR-4's stated goal. The **only** genuine external break is a named-argument call site on `VoxrBatchTestRunner` (`new VoxrBatchTestRunner(..., skippedWordPenalty: 0.5f)`); positional callers are unaffected.

C# offers no clean one-version bridge for a renamed parameter — overloads cannot differ by parameter name alone. So:

- `[FormerlySerializedAs("skippedWordPenalty")]` on the renamed field — exactly as DR-4 says, and it carries the whole serialized-value promise.
- Constructor parameters renamed, with the named-argument break called out in `CHANGELOG.md` under the behaviour-change heading (DR-5 already requires that heading).
- `DefaultSkippedWordPenalty` retained one minor version as an `[Obsolete]` alias forwarding to `DefaultCoverageWeight`, which is the nearest thing to the prescribed forwarder that a real surface here supports. It is `internal`, so its value is warning our own call sites, not users'.

**This is a mechanism adaptation, not a decision change** — DR-4's ruling (rename, preserve serialized values, accept the public rename) is realized in full.

**Ruled by the human, 2026-08-14: rationale correction, no G1 reopen.** Plan validation raised this as a blocking canon-conflict, on the ground that A1 and A2 both reopened G1 for a locked decision resting on a false premise. The ruling is that DR-4's clause is *inapplicable* rather than *wrong* — the decision is realizable in full, only the named surface is absent — and it is recorded here as an as-built deviation carried to G2, on the §A2.3/§A2.5 "rationale only, ruling stands" precedent. The alternative of *creating* the missing surface (a public `CoverageWeight` property plus an `[Obsolete]` forwarder) was offered and rejected: it would add public API to a released package that DR-4 never asked for.

### D6 — The eager path inherits coverage; §5.4's argument is re-derived, not inherited

Requirements §9.2 establishes that §5.4's *conclusion* holds and its *stated reason* does not. What the implementation must preserve, and how:

**Trailing.** The eager gate requires `bestEndIdx == tokens.Length` (`:1588`) — over `EndIdx`. Orphans count from `ConsumedEndIdx`. These differ, so "reaches the end of the buffer" does not by itself give zero orphans. What does: the region `[ConsumedEndIdx, EndIdx)` is provably all `[unk]`, and `[unk]` is never charged.

*Verified against the matcher, not the comment at `:1540`:* `consumedEndIdx` is assigned only at the three match sites (`:1070`, `:1101`, `:1116`), each time to `tokenIdx` immediately after it advanced over the matched token(s). Between matches, `tokenIdx` advances **only** through the `[unk]` skip loop at `:1043-1044`. `EndIdx = tokenIdx` at `:1144`. Therefore every token in `[ConsumedEndIdx, EndIdx)` was skipped by that loop and is `[unk]`. ∎

**DR-6 strengthens this.** `bestHasUnmatchedRequiredTail` (`:1571`) already refuses a candidate with any required element after its last match, so a committed candidate's pattern genuinely ended rather than ran out of buffer. That guarantee did not exist when §5.4 was written.

**Leading — the genuinely new exposure.** The eager scan today computes its score *without* the leading penalty, and `:1596-1601` explains why: condition 2 forces `bestStartIdx == firstRecognisedIdx`, so the skip count is zero and the eager and flush scores are identical. Under D1 the eager scan can no longer omit the leading term — it comes from the shared method. Two things to discharge:

1. **Gate ordering.** The score gate (`:1524`) runs *before* the start-position condition (`:1588`). A candidate with a non-zero leading skip now reaches that gate with a reduced score. Both routes still end at `None`, so no correctness break — but the eager *winner* can differ. Selection's first key is earliest start, and the earliest-starting candidate has the fewest leading skips by construction, so leading coverage should be inert here. **Expected, therefore measured** (Phase 4), not assumed.
2. **Same rule, different inputs.** `TryEagerCommit` scans the in-progress buffer; `ParseInternal` scans the flushed utterance. The orphan test depends on the token array it is handed, so identical *rules* do not imply identical *numbers*. The invariant holds only if the buffer a verdict was computed on is the buffer the subsequent flush parses. Phase 4 pins this with a test rather than an argument.

> **AMENDED 2026-08-14 by plan validation — §5.4's claim is false, and this section inherited the wrong half of it.** The paragraphs above discharge the trailing term for a candidate that *already won*, and the leading term for verdict stability. Both hold. Neither addresses the case that actually moves verdicts: **the trailing term differs between candidates sharing a start index, so it reorders the eager scan's winner — and the winner is what `:1548`, `:1569` and `:1588` are then applied to.**
>
> Worked counter-example, every gate re-checked against live code. Grammar `["fire"]` and `["fire","at","{target}"]`, buffer `"fire hotel one"` (medial "at" dropped):
>
> | | bare `fire` | `fire at {target}` | winner | verdict |
> |---|---|---|---|---|
> | today | `1/1` = **1.000** | `2/3` = 0.667 | bare | `bestEndIdx` 1 ≠ 3 → **`None`** |
> | after | `1/(1+2)` = 0.333 | `2/3` = **0.667** | slot-filled | passes every gate → **`Commit`** |
>
> The slot-filled winner clears each completeness gate in turn: `bestMissedRequiredSlot` false ({target} matched); `bestHasUnmatchedRequiredTail` false (`requiredAfterLastMatch` reset by the `{target}` match at `:1075`); `bestStartIdx == firstRecognisedIdx == 0`; `bestEndIdx == 3 == tokens.Length`.
>
> So design §5.4's "*The trailing term **cannot** destabilise the eager gate, structurally*" is **false as stated**. The new verdict looks *correct* — a medial drop with every slot filled is exactly the case DR-6 blesses as safe to commit early (`:1576-1580`) — but "cannot" is not what the code does.
>
> **Ruled by the human, 2026-08-14: rationale correction plus measurement, no G1 reopen.** Phase 4 measures the **full verdict-change class**, not merely the leading term's inertness, and reports it at G2 for a ruling on whether the new verdicts are desirable. Suppressing the trailing term inside the eager scan was offered and rejected — it would preserve §5.4's sentence at the cost of the eager and flush paths disagreeing, which breaks the invariant D1 exists to protect.

## 4. Runtime flow

Per utterance, in `ParseInternal`:

1. Build `_recognisedPrefix` and `canStartPattern` → `_orphanRun`. Once, before the extraction loop. O(tokens × `_startSlots.Length`).
2. Extraction round, from `searchStart`:
   - For each command × pattern × start index: `TryMatchScored(tokens, startIdx, pattern, searchStart)` returns a score already carrying both coverage terms via two array lookups.
   - `IsBetterCandidate` ranks on that score. DR-7's admission filter and the `Score <= 0f` floor are unchanged and still read element counts and sign — neither is affected, because the denominator only grows and division by a larger positive number cannot change a sign.
   - Winner recorded. **No post-selection adjustment.**
3. Advance `searchStart`; repeat. `_recognisedPrefix` and `_orphanRun` are reused unchanged — the leading term re-bases via the `searchStart` subtraction, and the trailing term is `searchStart`-independent by construction.

`TryEagerCommit` runs the same per-buffer setup over its own token array, then the same scan.

## 5. Non-functional realization

- **Per-candidate cost** is two array indexes and one divide — O(1), unchanged in complexity from today's single divide. Setup is O(tokens) plus the slot-start probe. Baseline to report against: item 1 measured `≈7.7 µs` per utterance and `344.4 bytes/utterance`. **Measured at review — see §9.7.** (The A3 amendment briefly broke the O(1) property with a per-candidate `[unk]` scan; review caught it and it is now tabulated. See §9.7.)
- **Allocation** does not grow: both tables are reused `int[]` fields grown on demand, and the grammar-derived caches are built once with `_canCommitEarly`.
- **`[0,1]` bound (DR-1)** holds and is argued rather than inherited: coverage only ever *adds* a non-negative quantity to the denominator, so the ceiling of `1.0` is unchanged and the floor is unchanged.
- **Determinism** holds: both tables are functions of the token array and the grammar alone, and are built before any candidate is evaluated — so no candidate's score depends on evaluation order.
- **Grammar generation untouched**, so `NativeBridge~/harness/expectations.json` does not re-baseline.

## 6. Build plan

**Revised 2026-08-14 after plan validation.** Six ordering and coverage defects were found in the first draft; the table below is the corrected plan. What changed and why is recorded in §8.

Risk-ordered. **Phases 3–6 are one atomic unit for suite-green purposes**: Phase 3 inverts two existing behavioural tests (see below), so the suite is legitimately red from the start of 3 until Phase 6 closes. Only Phase 3's own arithmetic is verifiable in isolation before then. The first draft's claim that every phase "leaves the suite green" was false for exactly this reason.

| # | Phase | Why here | Verify |
|---|---|---|---|
| 0 | **DONE.** Rig copied and validated against the committed grammar pin; corpus census run; baseline captured. | Baseline before any code moves; F15 forbids treating a delta as evidence until the corpus is known to contain the phenomenon. | ✅ Rig reproduces the 133-entry pin identically. ✅ Census: **0 leading skips, 0 trailing orphans** across all 16 transcripts. Recorded in `phase0-census.md`. |
| 1 | **Red tests.** Invert-in-place is the vehicle: `HazardSplitAcrossTwoIntents_Warns` (`VoxrCommandParserTests.cs:1584`) and `BareFormReachableOnlyByOmittingAnOptional_Warns` (`:1613`) already pin symptom 2 as current behaviour. Add F4 (`ConsumedEndIdx`-not-`EndIdx`) and F5 (`[unk]` transparency) as new cases. | F9 wants the symptom reproduced as a failing test *before* the change — and the pin already exists and already passes on `main`. | The two inversion tests are red the moment Phase 3 lands and green after; F4/F5 authored and passing against `main` where they describe unchanged behaviour. **Not** "F7/F8 red against `main`" — see §8/H-6. |
| 2 | **Tables and predicate, unwired.** `_recognisedPrefix`, `canStartPattern`, `_orphanRun`, `_startLiterals`/`_startSlots`, built in the **constructor** (D3). Computed but not consumed by the score. | The riskiest logic lands with zero behavioural blast radius and can be unit-tested directly. | Suite unchanged green; direct tests on table contents. Needs an `internal` test seam — `InternalsVisibleTo` reaches `internal`, not `private`. |
| 3 | **Wire it.** `TryMatchScored` takes `searchStart` and applies both terms; delete `:665-671` (the block **and** its closing brace) and the then-orphaned `bestRawScore`/`bestDenominator` locals (`:590-591`, assigned `:624-625`, read nowhere else). **Owns the two inverted assertions from Phase 1**, re-derived and argued per §7.3 — never mechanically updated. **Owns `WarnOnDroppableRequiredLiteral`** (`:365`). | The behaviour change, on tested foundations. | Phase 1 inversions go green with argued expectations; arithmetic hand-checked. Suite red overall until Phase 6 — expected. |
| 4 | **Eager discharge (D6).** Verify the all-`[unk]` gap in tests; pin verdict-and-flush agreement; **measure the full verdict-change class** — every utterance whose eager verdict moves under the *trailing* term, not merely the leading term's inertness. | Isolated from Phase 3 so a failure is attributable. The trailing term provably reorders the eager winner (D6 amendment), so this is a real class, not a formality. | F12's checks; no verdict names a command the flush does not fire; the verdict-change class enumerated for the G2 ruling. |
| 5 | **DR-4 rename (D5).** Eleven sites, `[FormerlySerializedAs]`, `[Obsolete]` alias on the internal const. | After the behaviour is settled — a rename in the same commit as a behaviour change makes both unreviewable. | Serialized round-trip checked against a **purpose-built fixture** (no `Samples~` asset serializes the field today). `CHANGELOG.md:56` keeps the historical name — it is a shipped `1.4.0` release note, not a live reference. `editor-testing.md:153`'s anchor moves with the heading. |
| 6 | **Reconcile assertions (F16).** | Needs the final numbers. | See the reshaping note below. |
| 7 | **A/B, both directions (F14).** | The measurement the design calls this feature "most in need of". | Both directions stated in advance (below) and both populated. |

**Phase 6 is reshaped by a validation finding.** All 28 numeric `Score` assertions in the suite were hand-derived under the new formula and **none changes value** — every one is a whole-utterance match (`ConsumedEndIdx == tokens.Length`) or an existing leading-skip case. So the suite's numeric assertions are near-**blind** to this feature, and F16's original framing ("re-derive every numeric score assertion") would have produced a zero delta and a false sense of coverage. The real movement is in **behavioural** assertions. Phase 6 therefore owns:

- The two inverted tests from Phase 3, with argued expectations.
- **Two span-tie-break tests that stay green while going vacuous** — `SpanTieBreak_LongerSpanBeatsHigherLiteralCount` (`:1259`) and `SpanTieBreak_ChoosesBetweenCommands_NotJustPatterns` (`:1291`). Both currently exercise a genuine score *tie* resolved by the consumed-span key; after this feature the candidates no longer tie (`3/(3+1)` = 0.75 vs 1.0, and `2/(2+1)` = 0.667 vs 1.0), so the winner is decided on score and the span key is never consulted. The assertions still pass and prove nothing. **F10's acceptance check — "a test still exercises the consumed-span tie-break on two candidates of genuinely equal score" — lapses silently unless this phase restores a real tie.** (`SpanTieBreak_TailedPatternWins…` at `:1196` survives, but only because `CreateTailedParser` registers the slot-initial pattern `["{burn_level}"]` — i.e. D4's degenerate case is what keeps it honest.)
- The fixture-manifest delta, reported as `0 of 16` **with the census as its stated reason**, not as an unexamined pass.

**Stated before Phase 7 runs, per F14 and item 1's §13.1 lesson** — the two directions this feature is expected to move, so that measuring only the flattering one is not an option:

- **Newly winning:** slot-filled patterns beating bare siblings (symptom 2, the intended fix); longer patterns beating short ones swallowed inside them; **eager verdicts moving `None` → `Commit`** where a medial drop now wins (D6 amendment).
- **Newly losing:** bare or short patterns stranded mid-utterance that used to win at `1.0`; **single-pattern grammars** where a demoted winner has no sibling to replace it and falls below `minScore`, firing nothing where it used to fire (requirements §8); **trailing filler** under `freeSpeechMode`/`InjectText`/batch, where a real out-of-grammar token is charged (§8/M-6).

**Phase 7 needs a corpus strategy the previous feature did not have.** The census proved the committed corpus contains zero instances of the phenomenon, and item 1's inherited `ablate.py` does single-word **deletions** — which shorten the utterance and so cannot manufacture orphans directly. Deletion reaches symptom 2 only where dropping a word lets a shorter sibling win and strand the rest. Phase 7 must add an **insertion/concatenation** pass (in-grammar tokens placed around a real transcript) alongside the inherited ablation. Without it, Phase 7 measures nothing and would report `0` in both columns — a number that looks like a clean regression check and is actually an instrument reading zero because it is pointed at the wrong thing.

## 7. Risks and unresolved items

- **The requirements §8 open question is the real one.** A bare pattern demoted to `0.333` fires nothing when no sibling exists. Phase 7's "newly losing" column exists to size it. If material, it is a `KNOWN_LIMITATIONS.md` entry or a §5.2 reopen — the human's call at G2, not this feature's.
- **`ScoreFollowUp` is a post-selection score site with no coverage term — needs a ruling, not a silent choice.** `VoxrCommandParser.cs:1370`, called from `PendingCommandHandler.cs:166`, re-scores a pending command after slot-fill; the result reaches `OnCommandConfirmed`/`OnCommandRecognised` and the diagnostics at `VoxrCommandRecogniser.cs:640`. After this feature it is the **only** score in the system not carrying coverage, which contradicts F1's acceptance check as written ("no site recomputes or re-adjusts a score after selection") and NFR §5's legibility promise. **Proposed carve-out, to be confirmed at review:** leave it uncovered and say so, because a follow-up score measures *pattern completion* against a **different utterance** than the one the pending command was matched in — there is no single token array over which "orphaned" is even defined. It is not gated against `minScore`, so the harm is legibility, not firing. Either way F1's wording needs the carve-out stated explicitly rather than being quietly false.
- **F5's promise is broader than the implementation.** Requirements F5 says out-of-grammar preamble, hesitation and noise are free on both sides. The implementation exempts the literal token `[unk]` only, so any non-`[unk]` token that starts no pattern is charged whether or not it is in the grammar. In the normal decoder path out-of-grammar words *arrive* as `[unk]`, so the two coincide — but `freeSpeechMode` (`VoxrCommandRecogniser.cs:22`), `InjectText`, and `VoxrBatchTestRunner.Run` all deliver real tokens. The leading term already behaves this way, so it is symmetric (goal 3) — but today only a preamble was exposed and now a **tail** is, and trailing politeness is the commoner shape (`"cease fire please"`: `1.0` → `0.667`). `VoxrBatchTestRunner` needs no code change beyond the rename, but `Documentation~/editor-testing.md:153`'s claim that batch results "predict runtime behaviour" weakens for free-speech grammars. Report at G2.
- **D4's conservatism can hide the feature.** A slot-initial permissive pattern can zero out trailing coverage grammar-wide, silently. Reported at G2.
- ~~**Active-set invalidation.**~~ **Resolved by validation — this was a non-risk.** `SetActiveSets` → `RebuildParserAndGrammar` → `RebuildParser` → `new VoxrCommandParser(...)` (`VoxrCommandRecogniser.cs:199-211, 265-275`) constructs a fresh parser on every active-set change, so constructor-built caches (D3) invalidate automatically.

## 8. Plan validation — 2026-08-14

An independent `plan-validator` pass audited §3 and §6 against the locked design, the requirements, and `main @ f182c78`, with none of this session's reasoning passed to it. It returned one blocking canon-conflict, six high findings, six medium and seven low. Both canon items were put to the human and **both were ruled rationale corrections, not G1 reopens** (recorded inline at D5 and D6).

**Defects fixed in this doc, in severity order:**

| ID | Finding | Fix |
|---|---|---|
| CC-1 | D5's accessibility table wrong: `VoxrCommandParser` is `internal class` (`:28`), so its `public` ctor is not externally reachable. The break was overstated by one surface. | Table corrected; only `VoxrBatchTestRunner` breaks. |
| H-1 | Two existing behavioural tests **invert** at wiring (`:1584`, `:1613`) and no phase owned them. | Phase 3 owns them; Phase 6 owns their argued re-derivation. They are also the F9 "before" pin, which removes the need for a new grammar fixture. |
| H-2 | `WarnOnDroppableRequiredLiteral` (`:365`) — a construction-time warning that exists to warn about symptom 2, whose stated rationale this feature falsifies — was not in the plan. | Phase 3 owns it. |
| H-3 | D3 placed the start caches on the **lazy, eager-only** `_canCommitEarly` path, which the shipped default never reaches — shipping fork F5's rejected behaviour on the default path. | D3 corrected to constructor-time construction. This was the most serious defect found. |
| H-4 | D6 discharged the trailing term only for an already-won candidate, missing that it **reorders the eager winner** and so changes verdicts. | D6 amended with the worked counter-example; Phase 4 measures the full class. |
| H-5 | Phase 0's gate required a trailing-orphan count that only Phase 2's predicate can produce. | Narrowed — the census conclusion stands regardless, because every transcript has `ConsumedEndIdx == tokens.Length`, so there are no trailing tokens *at all* to classify. |
| H-6 | Phase 1's criterion "F7/F8 red against `main`" is unmeetable: F8 is a **guard** describing behaviour that is already correct on `main` and stays correct after. "F5/F4 red or absent" was vacuous. | Criteria rewritten around invert-in-place. |
| M-1 | "Every phase leaves the suite green" is false once H-1 lands. | Phases 3–6 declared one atomic unit. |
| M-2 | Two span-tie-break tests stay green while going vacuous, silently lapsing F10's acceptance check. | Phase 6 owns restoring a genuine tie. |
| M-3 | Phase 7's stated measurement is inapplicable to the committed corpus. | Confirmed independently by Phase 0; Phase 7 gains an insertion/concatenation strategy. |
| M-4 | `ScoreFollowUp` is an uncovered post-selection score site. | Recorded in §7 with a proposed carve-out, for a review ruling. |
| M-5 | F2/F7's examples need grammar the demo set lacks. | Superseded by H-1 — the pair exists inline at `:1584`. |
| M-6 | F5 promises out-of-grammar tokens are free; only `[unk]` is exempt. | Recorded in §7. |
| L-1…L-7 | Non-risk active-set invalidation; `:665-671` not `:665-670` plus orphaned locals; private-field test seam; D2's `[unk]` justification a non-sequitur; stripped optional-literal form; `CHANGELOG.md:56` must keep the historical name; F13's round-trip needs a purpose-built fixture. | All folded into D2/D3 and Phases 2/3/5. |

**Confirmed clean by the same pass** — worth recording because these are the claims most likely to be wrong: all three of §5.2's worked arithmetic cases; the `_orphanRun` recurrence (traced over five token shapes, including `[unk]`/starter interactions); D6's all-`[unk]` derivation, independently re-derived from the three `consumedEndIdx` write sites; D6's leading-term inertness; `TryMatchScored` having exactly two callers; `:665-670` being the only post-selection adjustment inside `ParseInternal`; F13's eleven-site list being complete; sign preservation across all three `Score <= 0f` / `> 0f` gates; and DR-7's orthogonality surviving under D3's active-patterns rule.

**Explicitly not verified by that pass** (its own caveat, carried here so it is not mistaken for coverage): it did not run the Unity suite, the A/B rig, or the grammar pin, and did not trace all ~135 test utterances for outcome changes. Every red/green claim above is hand derivation against source. H-1's inversions and M-2's vacuous ties must be confirmed by a real EditMode + PlayMode run before they are acted on.

## 9. Build log — findings from implementation

Accumulated as each phase lands, so the doc reconciles to as-built rather than to the plan. Same discipline as §8: a claim only enters here once it has been derived against source or measured.

### 9.1 Phase 1 — F4 is a guard, not a red test, and provably so

§6/H-6 established that **F8** is a guard describing behaviour already correct on `main`. Deriving F4's test case showed **F4 is the same**, for a stronger reason than F8's — not "already correct" but **structurally unobservable**:

- D6's derivation proves the region `[ConsumedEndIdx, EndIdx)` is always all-`[unk]` — the only thing that advances `tokenIdx` without recording a match is the skip loop at `:1043-1044`.
- D2's `[unk]` line makes `[unk]` **transparent**: `_orphanRun[i] = _orphanRun[i+1]`.

Compose the two and `_orphanRun[ConsumedEndIdx] == _orphanRun[EndIdx]` **identically, for every candidate on every utterance**. So no test can distinguish the two origins while F5 holds. F4's stated failure mode — "a candidate sheds orphans by absorbing noise" — is unreachable, because the noise it would absorb is exactly the thing that costs nothing anyway.

**What was built instead.** `TrailingUnk_AbsorbedIntoEndIdx_ShedsNoLeftover` pins the *conjunction*: it fails if `[unk]` stops being transparent while the origin stays at `ConsumedEndIdx`, which is the shape that would make absorption pay. Its fixture (`CreateTrailingOptionalParser`) is guarded by a second test proving the `EndIdx > ConsumedEndIdx` gap is real in it, so a change to the skip loop cannot quietly reduce the first test to a comparison of two identical shapes.

**The implementation still counts from `ConsumedEndIdx`**, as §5.2 specifies. Not because the tests can tell, but because `ConsumedEndIdx` is the index whose correctness does not depend on the all-`[unk]` derivation continuing to hold.

**Reported at G2, not absorbed:** F4's acceptance check as written ("confirms the orphan count is unchanged by that run's presence") is satisfiable but vacuous. The row is not wrong; it is unfalsifiable. Same class of defect as H-6, found the same way — by trying to make the test fail before writing it.

### 9.2 Phase 2 — two deviations from D2/D3 as written, both to satisfy stated NFRs

**(a) `CountNumberSequenceWords` extracted from `TryMatchNumberSequence`.** D3 says the slot half of the predicate "reuses `TryMatchSlot` / `TryMatchNumberSequence` — the same predicate the matcher itself uses, so the two cannot drift". Calling `TryMatchNumberSequence` directly would have satisfied that and violated NFR §5: it builds the joined match string via `_numberSb.ToString()` whenever the run is longer than one word, so a `NumberSequence`-initial grammar would allocate **once per token per start-slot, per utterance** — exactly the "per-round allocation proportional to token count" the NFR calls a defect rather than a tradeoff.

Resolved by extracting the scanning loop both callers share. `TryMatchNumberSequence` keeps its behaviour bit-for-bit (`consumed = idx - startIdx` becomes `consumed = matchStart + count - startIdx`, the same quantity); the probe calls the shared loop capped at `minWords` and never builds a string. D3's anti-drift property is **strengthened**, not weakened — the two now share code rather than merely calling the same method.

**(b) `_startSlots` is empty for the demo grammar, as D3 predicted.** Verified against the rig's transcription of `DemoGrammar.cs`: every pattern in all three sets begins with a required literal. The slot half of the predicate costs nothing there. The `heading`/`elevation` `NumberSequence` slots — D4's degenerate-case candidates — are reached only behind `orient`/`set`, so they never become pattern starts.

**Side effect to disclose at review:** after Phase 3 deletes the post-selection block, `CountRecognisedTokens` has **no production caller**. It is kept deliberately, not overlooked: `RecognisedPrefix_AgreesWithTheScanItReplaces` pins the O(1) prefix-subtraction against it over every `[from, to)` range of a token array containing `[unk]`, so it survives as the reference implementation the optimisation is checked against. That is a real role, but it is a role a test gave it, and it should be ruled on rather than assumed.

**Phase 2 pre-flight (before Unity):** the A/B rig staged `WORKTREE` and compiled the real parser sources under `dotnet` — **0 errors, 0 warnings** — and `--grammar` still emits **133 entries** identical to the committed pin, confirming NFR "grammar generation untouched" and that `NativeBridge~/harness/expectations.json` does not re-baseline.

### 9.3 Phase 5 — F13's acceptance check is not automatable here

F13 asks for "a scene/prefab with a non-default `skippedWordPenalty` set before the upgrade still reports that value on `coverageWeight` after it". That cannot be tested from this harness, and the reason was **measured, not assumed**: `JsonUtility.FromJsonOverwrite` fed `{"skippedWordPenalty":0.25}` leaves the field at its default, because `JsonUtility` matches JSON keys to field names literally and never consults `[FormerlySerializedAs]`. Only Unity's native YAML serializer honours the attribute, which needs a real asset round-trip through `AssetDatabase`. No shipped scene, prefab or asset serializes the field at all (grepped), so there is nothing to load as a fixture either.

**What is pinned instead**, deliberately split: that Unity honours the attribute is a platform contract this package does not re-verify; that *we declared it correctly* is ours, and `CoverageWeight_CarriesFormerlySerializedAsTheOldName` asserts the attribute is present and names the **old** field. That is not hypothetical — a blanket rename during this phase rewrote the argument to the *new* name, which compiles, reads correctly in a diff, and would have silently reset every upgrading project's tuning. The test now catches exactly that.

**Reported at G2 as a verification gap**, not closed silently.

### 9.4 Phase 7 — the A/B found a defect in the locked §5.2 rule

Corpus: 699 utterances in five families (`baseline`, `delete`, `append`, `prepend`, `concat`), generated by `ab-rig/perturb.py`. Phase 0 proved deletion alone cannot manufacture orphans, so insertion and concatenation were added; probe words deliberately span the predicate (some can begin a demo-grammar pattern, some cannot, some are out-of-grammar). Judged at the recogniser's gate (`minScore` 0.6), with all seven columns fixed in `report.py` before the numbers were known.

| | count |
|---|---|
| newly-firing | 0 |
| slots-gained | 0 |
| intent-change | **10** |
| count-change | 1 |
| slots-lost | 0 |
| newly-silent | **18** |
| score-only | 116 |

The committed 16 baselines are **unchanged**, as the census predicted.

#### The defect

**The orphan predicate asks "could a pattern *begin* a match at this token?" — but the matcher can begin a pattern at any token by missing its leading elements. When a second command is spoken with its head dropped, the first command is charged for the second command's tokens.**

A dropped leading word is the single most common VOSK error and the entire reason this design exists. The feature mis-handles, on the leading side of the *next* command, exactly the input class it was built to fix on the trailing side of the current one.

Two user-visible consequences, both new, both measured:

**1. A real command is silently dropped.** `"cease fire target hotel one"` — a genuine two-command utterance whose second command lost its "approach":

| | before | after |
|---|---|---|
| | `cease_fire:1.000` + `approach_target:0.667` | `cease_fire:0.400` (below gate) + `approach_target:0.667` |

`approach_target` *does* match at "target", by missing its head — so the three tokens `cease_fire` was charged for are tokens a later round explains. Control: speak the second command in full (`"cease fire approach target hotel one"`) and both fire at `1.000`. The breakage needs the dropped head.

**2. The wrong command fires.** Minimal-pair patterns differing only in their final element, where that element is itself a pattern start:

| utterance | before | after |
|---|---|---|
| `switch to weapons target hotel` | `mode_weapons:1.000` | **`mode_navigation:0.667`** |
| `switch to navigation target hotel` | `mode_navigation:1.000` | **`mode_weapons:0.667`** |

Mechanism: `[switch, to, navigation]` *misses* its final element, so its `ConsumedEndIdx` stops at 2 — and token 2 is "weapons", which begins `["weapons","mode"]`, so its orphan run terminates at once and it pays **nothing**. `[switch, to, weapons]` *matches* its final element, so its origin moves past that same token and it pays for everything after: `3/(3+n)`. At n ≥ 2, `0.6 < 0.667` and the wrong pattern wins.

**A candidate is rewarded for matching less.** Matching the third element moved its accounting origin past the very token that would have terminated its own orphan run. Threshold measured exactly: n = 0 or 1 is safe, n ≥ 2 flips.

This is the locked rule behaving as specified, not an implementation error — which is why it is a G1 question and not a bug fix.

### 9.5 Two candidate amendments, measured (prototypes only — repo untouched)

Both prototyped by patching a staged copy of the parser in the scratchpad and re-running the full 699-utterance corpus against `main`. Neither is in the working tree.

**Variant A — literal DR-6 alignment.** `if (candidate.HasUnmatchedRequiredTail) return false;` in `IsBetterCandidate`, applying to selection the rule the eager gate already applies at `:1569`.

**Variant B — deny the mis-predicted token.** In `TryMatchScored`: when `requiredAfterLastMatch > 0`, the candidate's own next required element failed at this position, so it may not claim "some other pattern could begin here" for the very token it mis-predicted. Charge that token (skipping `[unk]`), then continue the run normally from the one after it.

| measured vs `main` | §5.2 as locked | **variant B** | variant A |
|---|---|---|---|
| **wrong command fires** | **2** | **0** | **0** |
| newly-silent | 18 | **17** | 35 |
| intent-change | 10 | 11 | 11 |
| count-change | 1 | 1 | 1 |
| newly-firing / slots-gained / slots-lost | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 |
| score-only | 116 | 116 | 129 |

**Variant A is the worse option, and it was my suggestion.** It roughly doubles the newly-silent count, and it carries a structural hazard the parser already documents: `IsBetterCandidate` is what feeds the `allowPartialMatch`/pending path, and the comment added for #73 warns that dropping candidates there "would also delete the only input the allowPartialMatch/pending path has, since that path is fed precisely by slot-missing candidates scoring below minScore". The A/B rig exercises `Parse` only, so it cannot see that breakage at all.

**Variant B eliminates the wrong-command class and costs nothing.** Every acceptance case re-measured under it and unchanged: F7 across intents (`0.667`), F7 within one intent (`#1 0.667`), F7 via omitted optional (`#1 0.75`), F8 sequential extraction (`1.0` + `1.0`), F18 at weight 0 (`1.0`), requirements §8 single-pattern (`0.333`), F11 DR-7 rejection, and both restored span tie-breaks. Its extra intent-change versus as-locked is a reclassification, not a new harm — `disable all target hotel one` moves from `newly-silent` into `intent-change`.

**Residual under B**, stated rather than buried: 11 intent-changes and 1 count-change remain, all one shape — a two-command utterance whose **second** command lost its leading word, where the first command is demoted below the gate. It loses a command; it never fires a wrong one. Closing it needs the predicate to recognise a position where a pattern will actually match with its head missed. A crude widening (any token any pattern could match anywhere) collapses into D4's degenerate case — it makes "hard burn" a terminator and disables the feature outright — so it would need a per-position admissibility probe, a materially larger amendment.

**Not yet Unity-verified.** These numbers come from the A/B rig only.

### 9.6 Amendment A3 applied — as-built

Ruled by the human 2026-08-14: adopt Option B, re-lock §5.2 at G1, apply on this branch, run Unity, and pin the flip with a test. Design doc §0C carries the amendment and §5.2 the amended rule.

**Code.** Where `requiredAfterLastMatch > 0`, the token at `consumedEndIdx` (skipping `[unk]`) is charged outright and the run resumes from the one after it; otherwise the ordinary `_orphanRun` lookup applies. `CanStartPattern`, the start caches and the leading term are unchanged. **D4's rationale is not** — see the correction inline at D4, raised by review: its "the failure modes are not symmetric" argument was written for a post-selection coverage term and does not survive reordering. D1–D3 stand as written.

*Originally landed as a branch at the call site; review (LINE, EFF, SIMP, all three independently) showed that broke the O(1)-per-candidate property, and it is now a second table filled in the same backward pass. See §9.7.*

**Tests.** Three added: the flip itself, its mirror with the pair swapped (a rule that merely favoured registration order would pass the first and still be wrong), and a guard that the charge does not bite a complete match.

**Final A/B — 699 utterances vs `main`, gate `minScore` 0.6:**

| | count |
|---|---|
| newly-firing | 0 |
| slots-gained | 0 |
| intent-change | 11 |
| count-change | 1 |
| slots-lost | 0 |
| newly-silent | 17 |
| score-only | 116 |

The 16 committed baselines are unchanged; the grammar pin still emits 133 entries identical to `expectations.json`, so `NativeBridge~/harness/` does not re-baseline. All 11 intent-changes and the single count-change are now the benign shape — a leading command demoted below the gate on a two-command utterance whose *second* command lost its head, with the second command still firing. **No wrong command fires anywhere in the corpus.**

The 17 newly-silent split three ways: 8 `append` (trailing filler charged — the M-6 exposure, working as designed), 8 `delete` (partially-heard utterances that used to land on exactly `0.600` and now land on `0.500` — issue #76's territory, and arguably a fix), and 1 `prepend` — `"switch navigation mode"`, where `mode_navigation` matching `["navigation","mode"]` from index 1 is charged for the leading `"switch"` it skipped, `2/(2+1) = 0.667 → 0.500`. That one is the *leading* term doing exactly what #31 always did; it moves only because the score it produces is now the reported score.

**Still outstanding for G2:** `KNOWN_LIMITATIONS.md` entries (post-G2 per the project bindings) for the A3 residual, the M-6 trailing-filler exposure, requirements §8's single-pattern demotion, and D4's degenerate case; plus a follow-up issue for the admissibility probe.

### 9.7 Review cycle — 11 angles, 5 adversarial verifiers, human-ruled 2026-08-14

Full-profile `review-cycle` on `68a7f57`. 21 findings after dedup; the human ruled fix on 18, defer-to-issue on 2, and the remaining one was refuted in verification.

**What review caught that the build did not.** All of it passed a green suite (116/116, 398/398) and the 699-utterance A/B.

| Finding | Angles | Outcome |
|---|---|---|
| A3's call-site branch broke the **O(1)-per-candidate** property NFR §5 requires, under a comment asserting the opposite | LINE, EFF, SIMP | **Fixed** — tabulated as `_forcedOrphanRun` in the same backward pass. Verifier brute-forced the recurrence's equivalence over **380,713 cases, 0 mismatches**; re-running the 699-corpus after the refactor gave byte-identical output. |
| A3's `[unk]` exemption **survived mutation** — three inputs execute it, no assertion observes it | GAP | **Fixed** — verifier compiled a mutant with the two lines deleted and confirmed all asserted output byte-identical. New test pins `"switch to [unk] target hotel"` at `2/5`. |
| `Coverage_CannotLiftACandidateTheAdmissionRuleRefused` was a DR-7-**existence** test wearing a coverage-independence label — its fixture's coverage was identically zero, so all three weights ran the same arithmetic | GAP | **Fixed** — verifier built a mutant that genuinely violates invariant 6 and **passed the old test**. New fixture `"zulu alpha yankee"` has live, weight-sensitive coverage (`1/3`, `1/5`, `1/13`) plus direct table pins. |
| `:1852`'s eager rationale asserted two things this change made false | GONE, WRAP, +3 | Fixed |
| `requiredAfterLastMatch`'s "currently dominated" comment falsified — A3 gave it a second consumer that acts alone | ALT | Fixed |
| `Editor/VoxrDebugSessionLog.cs:52` — a **missed rename site**, shipped inside every exported log's schema preamble | XFILE, WRAP | Fixed |
| D4's asymmetry rationale falsified by A3, while §9.6 claimed "D1–D4 stand as written" | ALT | Fixed (correction inline at D4) |
| `[FormerlySerializedAs]` comment promised more than §9.3 says was verified — it does not reach prefab-instance overrides | PIT | Comment scoped; **G2 human-verification item** |
| 6 test/convention defects: verdict-equality test that passes if both are `None`; missing header block; an assertion message contradicting its own comment; two misnamed retired tests; two tests missing `LogAssert.Expect` | CONV, GAP, REUSE | Fixed |

**Judgment calls, all ruled fix:** the coverage tables are now bound to the array they describe (`AssertCoverageTablesMatch`, editor-only) so a future caller that forgets `BuildCoverageTables` fails loudly instead of silently reading another utterance's answers; coverage short-circuits at `coverageWeight = 0` rather than building a table that gets multiplied away; the duplicated rationale is trimmed to one statement per site.

**Deferred to issues after G2:** the batch-test window never passes `coverageWeight` (pre-existing on `main` — the verifier **refuted** the claim that this change escalated it in kind, by compiling the base parser and reproducing a wrong-intent batch report there); and `WarnOnDroppableRequiredLiteral`'s breadth now that its contract has changed.

**NFR measurement (F19, requirements §5's promise, never discharged during the build):** `ab-rig --bench`, 2000 reps × the 16-transcript corpus, three runs a side.

| | `main` | after |
|---|---|---|
| bytes/utterance | **343.0** | **343.0** |
| µs/utterance | 12.22 – 12.65 | 12.34 – 12.81 |

**Allocation is identical — zero growth, which is invariant 10 discharged by measurement rather than by inspection.** Time ranges overlap; medians differ by ~2%, comparable to item 1's measured `+2–9 %`. The absolute µs is not comparable to the recorded `≈7.7 µs` baseline — different harness, machine and runtime — so the delta is the meaningful figure.

### 9.8 PR review (#78) — one real defect, found by reading the recogniser rather than the corpus

Independent `review-pr`, full profile: CLAIM + all 11 roster angles, 5 verifier passes. Posted to the PR. The human ruled fix on everything.

**The defect (B1).** `VoxrCommandRecogniser` widens the **decoder** grammar with confirm/cancel vocabulary via `GetFollowUpGrammarWords()`, so VOSK returns `yes`/`confirm`/`cancel`/`abort`/… as **real tokens, not `[unk]`** — while `_startLiterals` was pattern-derived and could never match them. Measured on the shipped demo grammar: `"disengage yes"` `1.000 → 0.500`, below the gate, **firing nothing**. Not the ruled F5/M-6 carve-out (that names `freeSpeechMode`/`InjectText`/batch); this is the default path, charging words the package itself put in the grammar.

Fixed by giving the parser the same word list the recogniser gives the decoder (`additionalGrammarWords`, folded into `_startLiterals` — a follow-up word *can* legitimately begin something). The A/B is unchanged at 17/11, which is the finding's own epitaph: **no corpus utterance contains follow-up vocabulary**, so no amount of re-running it would ever have surfaced this.

**What that says about the measurement.** A separate finding alleging the corpus was "synthetic" was **refuted** — 635 of 699 rows contain only decoder-emittable tokens, the out-of-grammar probes touch 2 of 28 behavioural rows and 0 of 11 intent-changes, and design §7.1 had already ruled the decoder run out. The real residual is **frequency, not vocabulary**: exhaustive-by-construction perturbations say nothing about which shapes speakers actually produce, and B1 is exactly what falls through that gap.

**Also fixed:** the leading half of the change was untested — a mutant reverting it to post-selection application passed the whole suite (121/121, diff empty over 1095 utterances); `+infinity` slipped the weight sanitiser and could yield a `NaN` score that every `<= 0f` floor lets through; §9.6's `prepend` attribution was self-refuting (that case is a *trailing* demotion, and it exposes a structural limit — **coverage cannot reorder across start indices**, so goal 2 is unreachable when the better candidate starts later); the `[Tooltip]` asserted something A3 falsifies; D4's corrected rationale had been fixed in this doc but not in the code comment that carries it; `Coverage_ResidualHazard_…` used a DR-7-*admitted* candidate and so could not discriminate the active-patterns rule from the admitted-candidates rule it exists to pin; and no score was asserted at any weight but 0 and 1.

**Removed:** the zero-weight short-circuit added by the *previous* review cycle. Four angles independently showed it dead — `_coverageWeight` is `readonly`, so such an instance never writes non-zero into the tables and the `Array.Clear`s cannot change a stored value.

**Verified:** EditMode 116/116, PlayMode 403/403, no fallout in `VoxrCommandRecogniserInjectionTests` (40/40), `VoxrPendingCommandTests` (35/35), `VoxrCommandSetTests` (5/5), `VoxrDynamicSlotTests` (17/17). Grammar pin still 133 entries.

**F11 narrowed** to the property it actually protects — see requirements §4.3. The code was right; the row banned more than the danger, and would have wrongly ruled out the deferred admissibility probe.

### 9.9 Measuring the "coverage cannot reorder across start indices" limit

Ruled at review: measure the class before ruling on it. Enumerated every `(command, pattern, startIdx)` over all 699 utterances with full round-1 scoring (`searchStart = 0`, so a later candidate's score **includes** its leading charge — the honest number, not the flattering suffix-parse one), and looked for a round-1 winner under the gate with a later-starting admissible candidate over it.

| | count |
|---|---|
| round-1 winner under gate, later candidate over it | 29 |
| **recovered** — a later extraction round still fires something | **28** |
| **genuinely silent** | **1** |

**The property is real; the class is one utterance.** `"switch navigation mode"` (`mode_navigation` `0.667 → nothing`). It needs a shape the other 28 lack: the round-1 winner must *consume through* the start index the better candidate needed. `["switch","to","navigation"]` matches at 0 and again at 1 — skipping the "to" — so it consumes both tokens and round 2 begins at "mode", where nothing starts. In every other case the winner consumed a single token and sequential extraction reached the alternative on the next round.

**This corrects the review's own framing.** M1 said goal 2 is "unreachable wherever the better candidate starts later", which is true as a statement about the selection keys and badly overstated as a statement about behaviour — sequential extraction recovers 28 of 29. The limit is worth naming in `KNOWN_LIMITATIONS.md`; it does not warrant reopening §5.3's key order, which would be a fourth G1 re-lock for one prepend-shaped utterance.

## Related

- Requirements: `Planning~/features/coverage-in-selection/requirements.md`

## Related

- Requirements: `Planning~/features/coverage-in-selection/requirements.md`
- Locked design: `Planning~/design-docs/scoring-model.md` §5.2, §5.3, §5.4, DR-4, §7, §9 row 2, §0B/A2.5
- Predecessor: `Planning~/features/fidelity-miss-cost/architecture.md` — §13.1's lesson drives F14 and Phase 7
- Prerequisite: #73 / PR #75 (`main` at `f182c78`); baseline `Planning~/verification-runs/fe33f33/`
- Closes: #65 symptom 2, #42
- Successor: backlog item 3, `feat-scoring-docs-reconcile` (DR-5)
