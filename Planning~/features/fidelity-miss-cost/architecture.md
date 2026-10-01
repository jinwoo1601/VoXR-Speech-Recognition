---
type: architecture
feature: fidelity-miss-cost
topic: scoring-model
status: accepted-g2
updated: 2026-08-13
sources:
  - Planning~/features/fidelity-miss-cost/requirements.md
  - Planning~/design-docs/scoring-model.md
  - Runtime/Commands/VoxrCommandParser.cs
  - Tests~/Runtime/VoxrCommandParserTests.cs
---

# Fidelity Miss Cost — Architecture

## 1. Purpose & responsibilities

Realizes §5.1 of the locked scoring-model design: a missed required literal stops being charged twice. The production change is **one constant**; the architecture is almost entirely about *what else that constant reaches* and *how the claim is proved*. Requirements F1–F12.

## 2. The change

`Runtime/Commands/VoxrCommandParser.cs:35` at `5957fdf` — **`:65` on the branch head**, since the declaration gained its explanatory comment block.

```csharp
const float RequiredLiteralMissPenalty = -0.5f;   // →  0f
```

**The constant has exactly one consumption site**, `:993`, in `TryMatchScored`'s required-literal branch (`:980-995`). The denominator is credited unconditionally at `:982` *before* the match test, so removing the penalty leaves the miss occupying the denominator while contributing nothing to the numerator — which is precisely the §5.1 model. No control flow changes.

## 3. Verified reach — what the constant does and does not touch

Recon on this branch, 2026-08-13. This section is the load-bearing part of the doc: the design's §2 blast-radius estimate was taken at `d081e11`, two merges ago, and one of its numbers does not survive re-checking.

**Reached, by construction:**

- `TryMatchScored` (`:898-1012`) — the sole scorer for initial parses. Every score in the package flows from here.
- **The eager-flush path inherits it.** `TryEagerCommit` reuses the same selection over the same `MatchResult`s, so eager scores rise with parse scores automatically. This is what F9/F10 exist to bound.
- **Selection keeps its keys, but not its candidate set.** `IsBetterCandidate` (`:883-903`) still orders on earliest start → score → consumed span → literal count → registration order, and no *ordering* key changes meaning.

  > **CORRECTED 2026-08-13 (was: "No candidate that lost on an earlier key can now win").** That claim is **false**. `IsBetterCandidate`'s first test is `candidate.Score <= 0f` (`:892`) — a hard admission filter that sits *above* the start-index key, and the same filter guards the extraction loop at `:610`. Raising a missed literal's contribution raises `rawScore` by `0.5` per miss, so any candidate sitting in `0 < C − S ≤ 0.5m` moves from **rejected** to **admitted**. Two existing tests see exactly this — see the withdrawal note below.

  > **CORRECTED AGAIN 2026-08-13, by the review cycle (was: "It does not out-rank an existing winner; it becomes an extra result in a *later* extraction round, once the winner's span is consumed").** **That is also false, and it is the sentence Amendment A1 ruling 2 was ratified on.** `IsBetterCandidate` ranks **earliest start above score**, so a newly-admitted candidate *can* win round 1 outright — it does not have to wait for a later round. Once it does, it consumes tokens and moves `searchStart`, which is the origin issue #31's skipped-word charge measures from. Reproduced on this package's own demo grammar at both revisions: `"alpha one weapons mode"` goes from `mode_weapons 0.500` (suppressed) to `mode_weapons 1.000` (**fires**), because the junk winner absorbs the leading slot value that #31 would otherwise have charged for. The junk candidate is itself rejected by `minScore` — the harm is to a *different*, gate-passing result. Two further harms follow from the same root (result-buffer eviction; widened pending entry). All three are written up as **Amendment A2** (`scoring-model.md` §0B). *(The feature was halted here pending A2's G1 ruling; that ruling landed the same day and DR-7 closes all three — see §12.0 and §13.)*

**Not reached — and this matters:**

- **`ScoreFollowUp` (`:1196-1263`) does not use the constant.** Its required-literal branch (`:1253-1259`) *credits* every required literal unconditionally, on the stated reasoning that the original parse matched the initial literals and a follow-up implies the rest. So follow-up scores are byte-identical after this change, and the gap between an initial score and its follow-up score **narrows** — the two paths agree more than before, not less. `PendingCommandHandler.cs:166` is the only caller.
- **`GenerateGrammarJson` is untouched**, so decoder output and `NativeBridge~/harness/expectations.json` do not move (requirements §5). If the harness baseline shifts, something out of scope was changed.
- **`RequiredSlotMissPenalty` (`-1.0f`) is untouched** in both scorers.

  > **CORRECTED 2026-08-13, by the review cycle (was: "…so `allowPartialMatch` and pending-command routing are unchanged (F4)").** **Non sequitur, and empirically false.** Routing into pending is not gated by that constant. It is gated by *emission* (the parse loop's `<= 0f` floor) and then by `cmd.Score > 0f` at `VoxrCommandRecogniser.cs:670` — the exact floor §5.1 moves candidates across, and a branch that sits *inside* `if (cmd.Score < minScore)`, so `minScore` never protects it. On the demo grammar, 14 of 135 partial utterances go from 0 results to pending-eligible. F4's acceptance check ("every existing `allowPartialMatch`/pending test passes unchanged") therefore discharged **vacuously** — no existing test covers a formerly-invisible candidate, and this change set adds none. Mitigated by two opt-ins that both default off. Written up as **A2/M3**.

**The design's "15 affected score assertions" over-states the blast radius — the verified answer is zero.** All 15 `Assert…Score` sites in `Tests~/Runtime/VoxrCommandParserTests.cs` (`:358, 740, 741, 755, 1041, 1055, 1077, 1108, 1109, 1123, 1137, 1138, 1151, 1153, 1164`) assert on utterances with **no missed required literal**: perfect matches (`1.0`), leading skipped-word cases (#31 coverage, a different term), or range checks. A 16th numeric score assertion at `:1091` asserts a `ScoreFollowUp` return and sits on the untouched path. **No existing expected value changes.** F6 therefore discharges as "all 16 accounted for, none changed, here is why" rather than as a re-derivation exercise — but the arithmetic is still checked by hand, and the suite run is what proves it.

This is a recon correction, not a design contradiction: §5.1's decision is untouched, only its estimate of collateral. No G1 reopen.

**The real collateral is prose that encodes the old arithmetic**, which the change falsifies:

| Site | Says today | After |
|---|---|---|
| `Tests~/Editor/VoxrBatchTestRunnerTests.cs:191` | "Normalized score = (1.0 + -0.5) / 2 = 0.25" | `1 / 2 = 0.50` — still below `0.6`, test still passes |
| `Runtime/Commands/VoxrCommandParser.cs:304` | "the longer pattern is charged `RequiredLiteralMissPenalty`" | charged nothing; it loses the credit. The rest of the #42 block stays true — including "nothing normalized to 1.0 can beat it", which item 2 is what changes |
| `Tests~/Editor/VoxrCommandRecogniserDiagnosticTests.cs:83` | "low score (~0.08)" | already wrong (`0.125`); becomes `0.25` |

**Two behavioural tests do flip.** *Correction, 2026-08-13 — §3's original "so no behavioural test flips" is **withdrawn**.* Both flips are the zero-crossing above, and both are `ParseOne`'s `Assert.AreEqual(1, results.Length)` going `1 → 2`. Neither changes the result the test is actually about — the new result is an **extra, sub-threshold candidate in a later extraction round**, derived by hand here and re-derived as an improvement in §4.1 before either expectation is written down (F6's discipline applies to these too).

| Test | Utterance | Round 2 candidate that newly clears zero | Was | Now |
|---|---|---|---|---|
| `VoxrCommandParserTests.cs:643` `NumberSequence_RespectsMaxWords` | "orient to heading five mark one two three" — `{?elevation}` maxes at "one two", leaving `three` at idx 7 | `orient to heading {heading}` from idx 7: three literal misses, then `{heading}` matches "three" | `(−0.5×3 + 1) / 4` = **−0.125** → rejected at `:610`/`:892` | `(0×3 + 1) / 4` = **0.25** → emitted |
| `VoxrCommandParserTests.cs:1582` `HazardSplitAcrossTwoIntents_Warns` | "decelerate hard burn" — bare `decelerate` wins round 1, leaving `hard burn` at idx 1 | `decelerate by {burn_level}` from idx 1: two literal misses, then `{burn_level}` matches "hard burn" | `(−0.5 − 0.5 + 1) / 3` = **0.0** → rejected (the gate is `<= 0f`) | `(0 + 0 + 1) / 3` = **0.333** → emitted |

Round 1 is untouched in both: `set_heading` still wins the first, and bare `decelerate` still beats `decelerate by {burn_level}` (`1.0` vs `2/3`) in the second — which is precisely why **symptom 2 stays open** (§6 non-goals). Both new results are far below the default `minScore` of `0.6`, so nothing changes at recogniser level. The honest residue, per A1 ruling 2: a user running `minScore` below ~`0.35` starts seeing these tail candidates.

**Confirmed still-rejecting after the change** (F11):

- "cease xyz" vs `cease fire`: `1/2 = 0.50` (was `0.25`) — below `0.6`. ✔
- "launch missiles" vs `launch ?a {?quantity} {weapon} target {target}`: `(1+1+0−1)/4 = 0.25` (was `0.125`) — below `0.6`. ✔ Matches the design's figure.
- "hello world", "something random", `""`, whitespace, `null`: no literal matches at all, so the numerator stays `≤ 0` either way. ✔
- "enter five" (NumberSequence below `minWords`): a *slot* miss at `-1.0`, untouched. ✔

## 4. Test architecture

**Two levels, because the gate is not in the parser.** `VoxrCommandParser.Parse` returns matches *with* their scores and applies no threshold — `SkippedWord_ShortPatternInUtteranceTail` (`:1105-1110`) demonstrates this, returning a `0.5` command and merely asserting it is below `0.6`. `minScore` lives on `VoxrCommandRecogniser` (`:30`). So:

- **Parser tests pin the arithmetic** (`2/3`, `1/2`, `3/5`, `6/7`) — cheap, exact, and where the 15 existing assertions live.
- **At least one recogniser-level test proves the user-visible claim** — "the command now fires" / "still does not fire" — via the `InjectText` seam used by `Tests~/Runtime/VoxrCommandRecogniserInjectionTests.cs`. Without this, F1 and F3 would be asserted as numbers only, and the requirements promise behaviour.

**A dedicated fixture parser for the §5.1 cases.** The shared `MakeCommands()` grammar has no 3-element pattern with a droppable required literal (its patterns are 1, 2, 5 and 6 elements), so the new tests build their own parser mirroring the design's §5.1 table — `time to target`, `decelerate by {burn_level}`, plus a 5-element pattern for the boundary case. Precedent: `MakeOptionalLiteralParser()` and the inline parser in `Score_ShortPatternOptionalOmitted_AboveDefaultThreshold`. The payoff is traceability: each test reads as one row of the design's table.

New tests, in a new region `// --- Required-literal miss cost (issue #65 §5.1) ---` in `VoxrCommandParserTests.cs` unless noted:

| Test | Pins | Req |
|---|---|---|
| `MissedLiteral_ThreeElementPattern_ClearsThreshold` | "time target" vs `time to target` → `2/3 ≈ 0.667` | F1, F2 |
| `MissedLiteral_SlotStillExtracted` | "decelerate hard burn" vs `decelerate by {burn_level}` → `0.667` **and the slot is filled** | F1 |
| `MissedLiteral_LongPattern_CostIsProportional` | 7-element pattern, one drop → `6/7 ≈ 0.857` | F1 |
| `MissedLiteral_TwoElementPattern_StillRejected` | "fire" vs `cease fire` → `1/2 = 0.50` | F3 |
| `MissedLiteral_BoundaryCase_ExactlySixTenths` | 5-element, two drops → `3/5 = 0.60` exactly, slot filled | F5 |
| `MissedLiteral_ThreeElementPattern_NowFires` (recogniser) | `InjectText` at default `minScore` emits the command | F1 |
| `MissedLiteral_TwoElementPattern_DoesNotFire` (recogniser) | `InjectText` emits nothing | F3 |
| eager: verdict/flush agreement on a newly-crossing utterance → `VoxrEagerCommitTests.cs` | F9 |
| eager: 8-element slot-miss at `(6−1)/8 = 0.625` still refused by #66's guard → `VoxrEagerCommitTests.cs` | F10 |

**F9's utterance must carry a *medial* miss.** *Added 2026-08-13, after #70 merged.* The row above was written before the unmatched-required-tail guard existed. A **trailing** missed literal — the obvious "newly crosses `0.6`" case — is now refused by `HasUnmatchedRequiredTail` (`:1417`) regardless of score, so it would prove nothing about F9 and everything about #70. The case that genuinely newly-crosses *and* survives both completeness guards is a medial drop whose pattern still lands its final element on the last buffered token: `time to target` heard as "time target" scores `0.50 → 0.667`, `requiredAfterLastMatch` is cleared by the final `target` match, and `EndIdx` reaches the buffer end honestly. That is the utterance F9 pins, and it is the shape DR-6 deliberately preserves.

**The boundary test is the one to get exactly right.** `3/5 = 0.60` against a `≥` gate is a float-equality question, and §10 ruling 2 says it must fire. The parser assertion uses the existing `0.001f` tolerance; the recogniser-level claim is what actually proves the gate admits it. If the two disagree, that is a finding, not a tolerance to widen.

### 4.1 The two flipped tests — re-derived expectations, argued

A1 ruling 2 requires these to be *argued as improvements*, not mechanically updated. Taking that seriously means separating two questions: is the new emission defensible, and is the new *assertion* an improvement on the old one.

**Is the extra candidate defensible?** Yes, on the parser's own contract. `Parse` does not gate — it reports every candidate it can find with the score it earned, and `minScore` at `VoxrCommandRecogniser` is the gate (§4's two-level split; `SkippedWord_ShortPatternInUtteranceTail` already demonstrates `Parse` returning a `0.5` command it expects nobody to fire). Under that contract the *old* behaviour is the anomaly: "decelerate hard burn" leaves `hard burn` genuinely explainable by `decelerate by {burn_level}`, and the parser discarded that explanation not because it scored badly but because a penalty happened to drag it to exactly `0.0` — an artifact of negative penalties, which is precisely what A1.3 says the `≤ 0f` floor was. Reporting it at `0.333` and letting the gate reject it is the more honest arrangement, and it is a preview of the candidate item 2 will promote.

**Is the new assertion an improvement?** Only if it is written to *pin* the residue rather than to tolerate it. Mechanically swapping `ParseOne` for `results[0]` would hide the second result — the exact defect F6 forbids. So both tests keep asserting what they were written to assert, and gain an explicit assertion on the new candidate:

- `NumberSequence_RespectsMaxWords` exists to prove `maxWords` stops `{?elevation}` at two digits. It keeps that, and now also states *where the third digit goes*: a second, sub-threshold `set_heading` at `1/4 = 0.25`. That is strictly more information about `maxWords` than the old version carried — the old test proved the digit was not consumed, and quietly assumed it vanished.
- `HazardSplitAcrossTwoIntents_Warns` exists to prove the bare-form hazard is real: the bare intent wins and the burn level is stranded. It keeps that verbatim — round 1 is untouched — and now also pins that the stranded value resurfaces as a `1/3 = 0.333` candidate the gate rejects. This makes the test a live marker for item 2: when `feat-coverage-in-selection` lands, *this* assertion is the one that must change, and it will change in round 1, not round 2.

Both tests therefore end up asserting `results.Length == 2` with a derivation comment for the second entry, and both keep `LogAssert.NoUnexpectedReceived()`. If either flips in some way not derived above, the derivation was wrong and gets redone — it does not get widened to fit.

## 5. Decision register

**D1 — Keep the named constant at `0f`; do not delete the constant and its `rawScore +=`.**
The alternative is to drop the `else` branch at `:991-994` entirely (a `+= 0f` is arguably dead code a reviewer will flag) and let the denominator credit at `:982` carry the whole model. Rejected for three reasons: it is the design's literal instruction ("One constant"); it keeps the four scoring constants (`MatchScore`, `OptionalLiteralScore`, `RequiredSlotMissPenalty`, `RequiredLiteralMissPenalty`) legible as a set at `:32-35`, where the model is read; and it makes F8's A/B a one-number flip rather than a code edit, so the before/after builds differ by a literal. The `+= 0f` gets a comment saying the zero is deliberate — an uncommented one is the smell, not the constant itself. **Kept on record:** if review prefers the deletion, the cost is only that the A/B rig needs a patch instead of a value.

> **CORRECTION 2026-08-13, by the review cycle — D1's stated alternative is unsafe as written.** "Drop the `else` branch entirely" is **not** behaviour-preserving: that branch holds `requiredAfterLastMatch++` as well as the `rawScore +=`, and that counter feeds `HasUnmatchedRequiredTail` — issue #70's tail guard. Deleting the branch would stop counting literal misses toward the tail and **reopen #70**. Only the single `rawScore +=` line is safe to delete. The decision to keep the constant is unaffected (and review endorsed it, on the constant-set-legibility reason alone — reason 1 was judged circular and reason 3 did not hold, since Phase 4 drove the rig by revision rather than by editing a literal).

**D2 — Correct the falsified comments, nothing adjacent.**
The three sites in §3 become factually wrong on this change, so they are in scope. `VoxrCommandParser.cs:300-327`'s #42 block gets one clause fixed, not a rewrite — most of it (including the `1.0`-ceiling argument) stays true until item 2 lands, and rewriting it now would pre-empt that feature's own doc pass.

**D3 — Product docs are deferred to the human's G2 ruling** (requirements §8). Nothing in `Documentation~/` is touched during implementation; the fork is presented at G2 with the evidence in hand.

## 6. Non-functional realization

No new state and no ordering dependence is introduced, so determinism is unchanged;

> **CORRECTED 2026-08-13, by the review cycle (was: "the change **removes** an addition from the hot loop, so parse cost cannot regress; no allocation…").** Both halves are wrong. **Nothing is removed from the inner loop** — `rawScore += RequiredLiteralMissPenalty` still executes for every missed required literal; `x + 0f` is not a float identity (`-0.0`), so no compiler elides it. And the reasoning is unsound at the *round* level: candidates that used to break the extraction loop now emit and force another full `commands × patterns × startIdx` scan, each newly-admitted slotted result allocating a `VoxrSlotMatch[]`. Bounded (rounds ≤ `tokens.Length`, capped by `_resultBuf.Length`) and small in absolute terms, but it is a per-utterance increase, not a decrease. The honest statement is: unchanged per-element cost, plus roughly one extra scan and one small array per newly-admitted candidate.

Also trivially met, and worth stating only because the requirements make them checkable: `VoxrCommand.Score` keeps its type and its `[0,1]` normalisation (DR-1); and no `[SerializeField]` is added, so the inspector is untouched (F12).

Verification is EditMode + PlayMode only — nothing here touches capture, the bridge, or lifecycle (design §7.5). This is the rare feature in this package with **no** human-only deferral.

## 7. Build plan

Ordered lowest-risk-first. The Unity run is the expensive step (editor closed, `Tests~`→`Tests` rename, both platforms, minutes each), so the plan spends exactly two of them.

**Phase 0 — RETIRED 2026-08-13.** Its purpose was to find out cheaply whether the A/B rig had rotted. It has not: two independent verifier agents rebuilt it from scratch during PR #71's review, compiled it against the *current* parser, and drove it at two revisions at once (project memory `grammar-ab-rig`, updated 2026-08-13) — which is exactly the before/after shape F8 needs. The rig-validation step it existed to perform is not dropped; it moves into Phase 4A, where it doubles as the regression check.

**Phase 1 — Write the tests, red.** All nine tests from §4, asserting post-change values. *Verify:* Unity EditMode + PlayMode run #1 — the new tests fail with the *predicted* pre-change numbers (`0.50` where `0.667` is expected, etc.), and every pre-existing test passes. A new test that fails for an unpredicted reason is a modelling error to resolve before touching the constant.

**Phase 2 — Flip the constant** (`:35`), add D1's comment, correct the falsified comments from §3, and write the two re-derived expectations from §4.1. *Verify:* **not independently dischargeable** — this is a bare UPM package with no `Assets/`, so nothing compiles standalone and "it compiles" is only observable as part of Phase 3's Unity run. Phase 2 therefore carries no verification of its own and is not a checkpoint; a syntax error here surfaces as a Phase 3 compile failure.

**Phase 3 — Green.** Unity EditMode + PlayMode run #2. *Verify:* the whole suite is green; specifically all 16 pre-existing score assertions pass **unchanged**, which is F6's evidence.

**Phase 4 — Measure and report. REPLANNED 2026-08-13; shape agreed with the human before implementation.**

*Why the original plan could not work.* §7's measurement, and F8 behind it, assumed the committed harness corpus could A/B this change. It cannot, structurally: all 16 committed transcripts are **exact phrase matches**, so `m = 0` on every one of them and the before/after delta is identically zero *by construction* — not because the change is inert, but because the corpus contains no instance of the phenomenon under test. Running it and reporting "0 candidates moved" would be a vacuous report dressed as evidence. Amendment A1.4 records the same finding against the design's §7.

*What replaces it.* Two passes over the same real decoder output, both through the real parser under `dotnet` per `grammar-ab-rig`, driven at `5957fdf` and at `HEAD`:

- **4A — Regression + rig validation.** Replay the 16 committed transcripts unmodified. *Verify:* the rig reproduces pre-change behaviour at `5957fdf` (this is Phase 0's retired check, discharging F8's "validate against the pin first" clause), and the before/after delta is **exactly zero** on all 16. A non-zero delta here means something out of scope moved.
- **4B — Single-word ablation.** Derive every single-word-deletion variant of those same transcripts (**62** variants across the 62 words; the `[unk]` token and the empty `silence_negative` case are excluded, as `[unk]` is never charged). Replay each at both revisions. *Report,* per variant: winning intent before/after, score before/after and the delta, and whether it crosses the default `minScore` of `0.6`. *Summarise:* how many variants newly fire, and how many change winner.

Ablation keeps the measurement grounded in **real decoder output** — F8's actual requirement is that the utterances not be invented, and no utterance is invented here; each variant is a committed transcript minus one word, which is exactly the degradation §1 says VOSK makes most often. It is the only way to exercise `m ≥ 1` without regenerating the fixture corpus, which the NFRs forbid (the harness baseline must not move).

*Also in Phase 4:* report which of the **16** cases in `Tests~/Fixtures/audio/manifest.json` change `expectedIntent`/`expectedSlots` (F7 — expected to be zero, but reported either way, and argued if not). Note the count: F7 and design §7.2 both say "17"; the manifest holds 16, corrected by A1.4.

*Where the artifacts live.* The rig and the delta report go under `Planning~/features/fidelity-miss-cost/` — gitignored, so measurement scaffolding never ships with the package. The report's findings are carried into the PR body and the G2 hand-off.

**Phase 5 — Review and close out.** `review-cycle` on the branch, fixes, then PR + `review-pr` at the full profile, then the human's G2 ruling — including the §8 product-doc fork.

## 8. Risks & open questions

- ~~**The A/B rig may have rotted.**~~ **RETIRED 2026-08-13** — confirmed working against the current parser during PR #71's review (Phase 0 above). The measurement's real problem turned out to be the corpus, not the rig, and is answered by Phase 4B.
- **§3's "zero assertions change" is a claim about 16 sites read by hand.** Phase 3's suite run is what proves it. If any pre-existing assertion moves, the reading was wrong and the arithmetic gets re-derived on the spot. *Now narrowed:* the claim is about the 16 **numeric score assertions** only — §3's separate claim about behavioural tests is withdrawn, and the two known flips are derived in §4.1.
- ~~**The 5-element boundary is the sharpest edge.**~~ **RESOLVED 2026-08-13** — `3f/5f` is bit-identical to `0.6f`, so the `≥` gate admits it as §10 ruling 2 requires. The test still gets written; the worry was unfounded.
- **Product-doc fork open** until G2 (requirements §8): minimal correction now vs. full rewrite at item 3. **Widened by the validation pass:** `KNOWN_LIMITATIONS.md:389` also carries falsified arithmetic, and it is a *product* doc, so it falls on the same side of the fork as `scoring.md` — nothing in `Documentation~/`, `CHANGELOG.md`, or `KNOWN_LIMITATIONS.md` is touched before the human rules.
- **FOUND BY PHASE 4B, 2026-08-13 — §5.1 can newly fire the *wrong* sibling on the flush path.** The ablation measured 10 of 61 degraded real transcripts newly clearing `minScore`. **Nine recover the intent the undamaged transcript produces.** The tenth does not: `switch to navigation` with `navigation` dropped leaves `switch to`, which fits `switch to weapons` and `switch to navigation` *equally* at `2/3 = 0.667`, so registration order decides and **`mode_weapons` fires** where before nothing fired at `0.50`.

  This is the same shape Amendment A1 built its case on, one path over. A1/DR-6 closed it at the **eager** gate, where "the speaker may still be talking" justifies a tail rule. This is the **flush** path: the transcript is final, nothing more is coming, and no tail rule applies — the discriminating word is simply gone. No scorer can recover it, so the honest question is whether firing a coin-flip sibling beats firing nothing. §5.1's own F3 argument ("half the evidence is not enough") points one way; its `N≥3` promise points the other.

  **Not a deviation from the locked design** — it is a consequence of §5.1 exactly as ratified, and item 2's coverage term would not change it either (both siblings have identical coverage). It is therefore carried to G2 as a named consequence for the human to rule on, not patched here, and pinned by a test so it is a known tested tradeoff rather than a surprise.

  > **INCOMPLETE — corrected 2026-08-13 by the review cycle.** The paragraph above omits the premise that decides how the human should route this: **the wrong-sibling flush fire is already live on `main` at N≥4.** Reproduced at `5957fdf`, before this change: the in-tree sibling pair `set auto pilot on` / `set auto pilot off` on the buffer `"set auto pilot"` scores `(1+1+1−0.5)/4 = 0.625`, clears the default `0.6`, and fires `autopilot_on` through the **flush** path — `HasUnmatchedRequiredTail` is consulted only inside `TryEagerCommit`, so the flush path has no tail guard. §5.1 does not create this; it widens it from N≥4 to N≥3. §10 above states exactly that fact, but only for the *eager* gate, and never carries it across even though the score is the identical `TryMatchScored` output. Nothing in `Documentation~/scoring.md` or `KNOWN_LIMITATIONS.md` records the flush-path case at all.
  >
  > Review's proposal to route it to the issue lane on the #66/#70 precedent was **not** upheld: three disanalogies hold — at the eager gate refusing costs only latency (defer, and the right word arrives) whereas on the flush path refusing means firing nothing, which is the class §5.1 exists to rescue; A1.1's case rested on a *falsified stated rationale*, whereas registration-order tie-breaking is already documented as deliberate; and at N=3 the widening **is** §5.1's ratified purpose. So this stays a G2 question — but the human must be told it predates §5.1.
  >
  > Also on the record: A1.5's three rulings cover the eager tail guard, the zero-crossing, and issue-lane routing. **None of them rules on the flush-path dropped discriminator.** It has never been ruled on at all.

- **The zero-crossing may reach further than two tests.** §4.1 derives the two flips the validation pass found, but the `0 < C − S ≤ 0.5m` band is a property of every grammar, not of these two. Phase 3's suite run is the check; a third flip is a derivation to do, not a number to accept.

## 9. Plan validation (2026-08-13) — HALTED

An independent validation pass against the locked design and live code returned **UNSOUND**. Two blockers were re-verified by hand on this branch before the halt.

**B1 — §5.1 makes a live eager-flush bug dramatically worse, and falsifies DR-3's stated rationale.** The eager gate's end-of-buffer condition (`:1386`, `bestEndIdx != tokens.Length`) is satisfied *vacuously* by a trailing missed required literal, because a miss consumes no token and never advances `EndIdx` — the same mechanism the #66 comment describes for slots (`:1355-1358`), which #66 fixed for slots only (`:1367-1368`). DR-3 exempts literals deliberately, on the reasoning quoted at `:1363-1366`: "the command is fully determined and must not be blocked from committing." **That reasoning fails when the missing literal is the terminal, discriminating word.**

Demonstrable in this package's own demo grammar (`Tests~/Runtime/DemoGrammar.cs:136,143`), which registers `["switch","to","weapons"]` and `["switch","to","navigation"]`. Buffer `"switch to"`:

| | score | eager verdict |
|---|---|---|
| today | `(1+1−0.5)/3` = **0.50** | `None` — blocked by the score gate alone |
| after §5.1 | `(1+1+0)/3` = **0.667** | **`Commit`** |

Both patterns tie at `0.667`, so registration order decides and `mode_weapons` wins (`:884-891`). A speaker saying *"switch to navigation"* eagerly fires **`mode_weapons`** — the wrong command, not merely a premature one.

**The bug is already live on `main`, at 4+ elements.** Any pattern ending in a required literal, with that literal unspoken, scores `(N−1−0.5)/N` — at `N=4` that is `0.625`, already over the default `0.6`, with every other eager condition passing. §5.1 does not create the hole; it widens it from `N≥4` down to `N≥3`, which is where the common two-word-prefix grammars live. This is the exact shape of #66, one element class over.

**B2 — the change moves candidates across zero, which the plan's site sweep never considered.** New `rawScore` = old + `0.5m`. Zero is a hard disqualifier at `:610` and `:880`, so any candidate with `0 < C−S ≤ 0.5m` goes from *rejected* to *emitted* as an extra parse result in a later extraction round. Two existing tests break on this — `VoxrCommandParserTests.cs:648` (`NumberSequence_RespectsMaxWords`, 1 → 2 results) and `:1600` (`HazardSplitAcrossTwoIntents_Warns`, 1 → 2 results) — both via `ParseOne`'s single-result assertion. §3's claim "no behavioural test flips" is **withdrawn**; §3's "no candidate that lost on an earlier key can now win" is **false**, because the `Score <= 0f` disqualifier sits above the start-index key.

At recogniser level these extra candidates are still filtered by `minScore`, so B2 is not known to be user-visible — but it is a real behavioural change the plan must own.

**Also to fix on resume** (not blockers): five more falsified-arithmetic comments beyond §3's three, including **`KNOWN_LIMITATIONS.md:389`**, which is a *product* doc and therefore widens the §8 G2 fork (`Tests~/Runtime/VoxrCommandParserTests.cs:1460,1514,1538`, `Tests~/Runtime/VoxrEagerCommitTests.cs:801`); F8's A/B is **structurally vacuous** — all 16 harness transcripts are exact phrase matches, so `m = 0` throughout and the before/after delta is identically zero by construction, meaning §7's measurement plan cannot exercise the phenomenon under test; the fixture count is **16**, not the design's 17; `MissedLiteral_SlotStillExtracted` (§4) only yields `2/3` if the bare `decelerate` sibling is absent, which §4 never states; Phase 2's "*verify:* compiles" is not independently dischargeable in a bare UPM package; and `TryMatchScored`/`IsBetterCandidate` are at `:904-1012`/`:871-891`, not the design-era line numbers §3 cites.

**Confirmed sound and unchanged:** all 16 numeric score assertions re-derived independently — none moves, including `:1137`'s `5/6`, which arises purely from the #31 skipped-word penalty. `:993` is the sole consumption site. `ScoreFollowUp` is genuinely untouched. `"cease xyz"` (`0.50`) and `"launch missiles"` (`0.25`) still reject. #66's guard does block the design's `(6−1)/8 = 0.625` slot case. `3f/5f` is bit-identical to `0.6f`, so the boundary case does fire — §8's worry was unfounded.

## 10. Resumption (2026-08-13) — the §9 halt is lifted

Both of §9's blockers are discharged, and the plan resumes from Phase 1.

**B1 — discharged by merge.** The eager-flush hole was ruled at G1 into the issue lane as **Amendment A1 ruling 3**, filed as **issue #70**, and merged as **PR #71** — now on `main` at `5957fdf`, one of two cross-lane prerequisites this feature waited on (the other being #66 / PR #67). The guard is live at `VoxrCommandParser.cs:1417` (`HasUnmatchedRequiredTail`), and `DR-6` records the rule. This branch is rebased onto `5957fdf`, so the `2/3 = 0.667` "switch to" case §9 raised is now refused by the tail guard before the score gate is ever consulted. Its one consequence for this plan is §4's F9 row, corrected above.

**B2 — discharged by ruling.** The zero-crossing is **accepted and recorded** as Amendment A1 ruling 2. §3 is corrected, the two flips are derived in §4.1, and the residue is stated for G2.

**Line numbers in §2–§3 predate #70** and have drifted by roughly +24 in `VoxrCommandParser.cs`. Verified on `5957fdf`: the constant is still `:35`; its sole consumption site is now **`:1019`** (was `:993`); `TryMatchScored` is `:916-1040`; `IsBetterCandidate` is `:883-903` with its zero filter at `:892`; the extraction-loop zero filter is `:610`; `ScoreFollowUp` is `:1224-1291`.

**Phase 4's replan was agreed with the human before implementation**, per the Constraints requirement that a vacuous report not be shipped. The product-doc fork (§8, requirements §8) remains open and is presented at G2 — it is *not* resolved by this resumption.

## 11. Build record — as built (2026-08-13)

| Phase | Outcome |
|---|---|
| 0 | Retired before implementation. The rig was healthy; the corpus was the problem. |
| 1 | `7dd66f9` — 10 tests written red. Unity run #1: EditMode **116/116**, PlayMode **353/362**. Exactly **9** failures, all in the intended set, each with the predicted pre-change value. `MissedLiteral_TwoElementPattern_DoesNotFire` passed pre-change, correctly — it pins preservation, not change. **No pre-existing test regressed.** |
| 2 | `3367808` — the constant, its two comments, 8 falsified comments across 5 files, the 2 re-derived expectations, and 1 new pin test. |
| 3 | Unity run #2: EditMode **116/116**, PlayMode **363/363** — fully green, 0 failed / 0 inconclusive / 0 skipped across 479 cases. PlayMode's total rose 362 → 363, accounting for the one added pin test. **F6's evidence:** every pre-existing numeric score assertion passed **unchanged**, confirming the §3 reading that none of the 16 moves. |
| 4 | `phase4-measurement.md`, generated by `ab-rig/` at `5957fdf` vs `3367808`. |

**The rig is validated, not assumed.** It reproduces the committed 133-entry `grammar` pin in `NativeBridge~/harness/expectations.json` **byte-identically and in order**, which is the check project memory `grammar-ab-rig` demands before any delta it reports is trusted. It was then driven at both revisions and reproduced **all ten** hand-derived values — before and after — ahead of either Unity run. Every number in §3, §4 and §4.1 is therefore hand-derived *and* independently confirmed by the shipped parser at both revisions *and* asserted by the suite.

### 11.1 Phase 4 results

- **4A (F7) — zero delta.** All 15 non-empty fixtures replayed as the parser sees them are unchanged, so **0 of the 16 cases** in `Tests~/Fixtures/audio/manifest.json` change `expectedIntent`/`expectedSlots`. F7 discharges at N = 0.
- **A correction to the locked design's §7 / A1.4.** "All 16 committed transcripts are exact phrase matches" is true of **15**. `split_…` carries two finals that are each half of one command, and replayed individually they *do* move (`0.30 → 0.40`; `0.50 → 0.667`, the latter crossing the gate). This does not change any ruling — A1.4's *conclusion*, that the corpus cannot A/B this change usefully, still holds — but the premise was overstated and is recorded here rather than left standing.
- **4B — 61 variants** (62 words minus the single `[unk]`, which the scorer never charges). **10 newly clear `minScore`**; 0 reorder among visible results; 4 appear as sub-threshold candidates (the A1 ruling 2 residue, all far under the gate); 22 rise but stay rejected; 25 unchanged.
- **9 of the 10 rescues recover the correct intent.** The tenth is the dropped-discriminator case in §8, now pinned by `MissedLiteral_DroppedDiscriminator_FiresTheFirstRegisteredSibling`.

## 12. Review cycle (2026-08-13) — resolved; A2 ratified, DR-7 shipped

Eleven finder angles, five adversarial verifiers. **Verdict: the feature is sound but rests on a falsified premise, so it stops here.** The human ruled the routing in conversation: reopen G1 with an amendment, code unchanged.

**Confirmed defects — all reproduced by compiling the real parser at both revisions.** Written up as `scoring-model.md` §0B (Amendment A2), M1–M3. They share one root: `IsBetterCandidate` ranks earliest start above score, so a candidate newly admitted over the `<= 0f` floor can win round 1 rather than trailing as a harmless tail result. Harms: a false positive created (#31's guard defeated), a true positive lost (`_resultBuf` eviction), pending entry widened (`> 0f` floor). **Two of the three sentences that hid this were written in this doc during this session**, and both are corrected in place above (§3's bullets).

**Refuted:** the claim that DR-6 merely *delays* the wrong sibling and that the affected population is materially larger than measured. The eager→flush drain is real but is pre-disclosed canon (`CHANGELOG.md:30`), `eagerFlushOnCompleteMatch` defaults **off** so the flush path is primary anyway, and the ablation already samples that class.

**Downgraded to a doc gap:** the dropped-discriminator classification (see §8's correction) — the factual core holds, the issue-lane routing does not.

**Verified sound, and worth keeping on the record:** every numeric expectation independently re-derived from the pattern definitions (F6 intact — no assertion is fitted to output); `3f/5f` bit-identical to `0.6f` at `0x3F19999A` through the *actual* accumulation path, so the boundary passes on true equality; `RequiredLiteralMissPenalty` has exactly two occurrences in shipped C#; `ScoreFollowUp` genuinely never uses it; eager and flush selection still pass identical arguments to the same comparator; `[0,1]` preserved; no new tuning surface; region markers, naming and comment discipline compliant.

### 12.0 What shipped for DR-7, and what the PR review then found

**Implementation (commit `21717e5`).** `TryMatchScored` gains two locals, `matchedRequired` / `missedRequired`, incremented in exactly four branches (required slot matched / missed, required literal matched / missed) and in none of the optional branches; they ride out on `MatchResult` as `MatchedRequired` / `MissedRequired`; `IsBetterCandidate` gains one condition, `MissedRequired > MatchedRequired → reject`, sited beside the existing `Score <= 0f`. One placement covers both selection paths, since `ParseInternal` and `TryEagerCommit` share the comparator. Three tests pin the three harms; `NumberSequence_RespectsMaxWords` and `HazardSplitAcrossTwoIntents_Warns` reverted to byte-identical with `main`.

**The PR #72 review (12 angles, 3 verifiers) found no behaviour defect** — every major reduced to an inaccurate explanation of correct code. Two reductions were decisive and both went against the finder:

- *DR-7 destroys true positives in the `1/3 < f < 0.5` band* — **refuted.** It does refuse candidates `main` admitted, but `main`'s behaviour there is **non-monotone**: `"hotel two cease fire"` was suppressed at `0.500` while `"cqb hotel two cease fire"` — same command, *more* debris — fired at `1.000`. DR-7 is monotone across that ladder. No rule can separate those cases from the ruled-harmful `"alpha one weapons mode"`; they are the same case, and `main` differed only because an invisible fragment happened to clear `1/3`.
- *DR-7 wrongfully vetoes gate-passing candidates via matched optionals* — **refuted as a harm, confirmed as a real edge.** It needs more than `2.0` of matched-optional credit; no shipped pattern exceeds `1.5`. Zero corpus impact, and the alternative produces byte-identical output.

**What the review did change:** three false-rationale sites (DR-7 "restores" → *tightens*), the slot-miss routing comment, the optional/denominator analogy — all corrected in code and, as dated notes, in A2; the "Zero cost" NFR amended against measurement; F13 added; four more product-doc sites found, in a new class (§12.2); and four coverage gaps closed with three new tests (eager-gate inheritance, the optional clause in both directions, and the partial/pending path at recogniser level).

### 12.1 Fixes queued behind the G1 ruling

Held deliberately — the human's routing choice was "code unchanged until you rule". None of these is a design question; all are safe to apply the moment the ruling lands.

| # | Site | Fix |
|---|---|---|
| 1 | `VoxrCommandParser.cs:474` | **Runtime warning string**, not a comment — tells grammar authors the longer pattern is "penalized for the miss". Nothing is penalized now; it loses the credit. A shipped diagnostic giving a wrong explanation. |
| 2 | `VoxrEagerCommitTests.cs:915`, `:925` | Still state `0.625` as current fact; now `0.75`. Missed by the original sweep, which grepped for `- 0.5` and never matched a bare `0.625`. |
| 3 | `VoxrCommandParserTests.cs` (`NumberSequence_RespectsMaxWords`) | `results[0]` is indexed before the `Length == 2` assertion, so a regression surfaces as `IndexOutOfRange` instead of the intended message. Moot under A2 Option B, which reverts this test. |
| 4 | `MissedLiteral_ThreeElementPattern_ClearsThreshold` | Name claims a threshold the body never asserts; siblings in the same region do assert it. |
| 5 | New region, both files | `LogAssert.NoUnexpectedReceived()` applied unevenly across the nine new tests — pick one rule and apply it. |
| 6 | `VoxrCommandParser.cs:328-333` | Ragged re-wrap after the #65 clause was spliced into the #42 block. |
| 7 | `VoxrCommandRecogniserInjectionTests.cs:826` | Pointless `"…" + "…"` literal split; joined it is inside the file's width. |
| 8 | `VoxrCommandParser.cs:1046` | Third copy of the same derivation; D1 only asks the use site to say the zero is deliberate. |
| 9 | Constant comment | "Halved rather than abolished" — `1.5/N → 1/N` is a third off, not a half. Canon uses the same word, so state the arithmetic and drop the fraction. |

### 12.2 Product-doc sites for the G2 fork

The out-of-scope list named `scoring.md` §1–§3/§6–§7, `command-recognition.md`'s Scored Matching, `KNOWN_LIMITATIONS.md:389`, and the `CHANGELOG` #70 entry. Review found **four more** that must join the same ruling, or the fork will be ruled on an incomplete list:

- `Documentation~/troubleshooting.md:59-63` — the section **heading** "A command scores ~0.50 and is rejected" is now unreachable for 3-element patterns (they score `0.667` and pass); its "seven-element pattern scores `0.79`" is now `0.857`.
- `Documentation~/scoring.md:346` — the "Reading a session log" symptom table, outside the declared §-ranges.
- `CHANGELOG.md:12` (#47) and `:14` (#42) — under `[Unreleased]`, i.e. not yet shipped, both asserting the old arithmetic.

**Widened again by the PR #72 review — and the gap is one of *kind*, not count.** Every site listed above is *arithmetic*; **not one records a DR-7 doc need**. A ruling that corrects only the numbers would leave the docs describing a selection model the code no longer implements:

- `Documentation~/scoring.md:118` — §4 *Sequential extraction* enumerates the stopping conditions ("no candidate scores above `0`, a match consuming no tokens, or the result buffer full"). **DR-7 adds a fourth**, and §4 is outside every listed range.
- `Documentation~/scoring.md:92` — "Every candidate with a positive score competes" is now **simply false**. §3 *is* on the list, but listed for the `−0.5` arithmetic, so a numbers-only correction leaves this sentence wrong.
- `Documentation~/command-recognition.md:107` and `:114` — the prose twin of the runtime warning string this feature corrected ("the slot-filled pattern is **penalised** for the missing required literal"), plus the `0.7` counterfactual that becomes `4/5 = 0.8`. Both sit in *"Never leave a required function word between a bare pattern and its slot"*, **not** in the Scored Matching range this list named.

That makes **eleven** sites across four files, in two classes: falsified arithmetic (§5.1) and a falsified selection model (DR-7).

## 13. G2 (2026-08-14) — ACCEPTED

Human's explicit ruling in conversation, on the four forks presented with the preflight audit (all 8 items PASS) and the posted PR #72 review.

| Fork | Ruling |
|---|---|
| Product docs | **Correct now, scoped to correctness not rewrite.** The review found the problem was in two classes, and the second — `scoring.md` stating a selection model the code no longer implements — is what made deferral the wrong call. `scoring.md`'s structural rewrite stays item 3's. |
| Dropped discriminator | **Accept as a named consequence**, documented in `KNOWN_LIMITATIONS.md`; the interesting half (route ambiguous ties to confirmation rather than guessing) filed as **#74**, design lane. |
| N=8 slot-miss band | **Filed as #73**, issue lane, on the #66/#70 precedent. Pre-existing at N≥5 on `main`; this feature widens it by one band. Not fixed here. |
| Symptom 2 | **Stays open.** G2 accepts the cases #65 reports under symptom 1 only; #65 remains open until item 2. |

Also ruled: no re-run of `review-pr` on the post-review head (`01511a6`→`5e5865e` is test methods and comment text, Unity green, and preflight independently confirmed every CONFIRMED finding corrected and every coverage gap closed).

Product docs landed in `e5eff7b`. **The merge is the human's** — never Claude's, per the project's VC binding.

### 13.1 The lesson worth carrying to item 2

Three claims in this feature were asserted from a constant rather than measured against the code, and each was caught by an independent angle rather than by the author:

1. **A1 ruling 2** — "at default settings a user sees nothing new". False; three mechanisms said otherwise.
2. **A2's Option B table** — "preserves every intended §5.1 behaviour exactly". The prototype measured only the newly-*admitted* direction, never the removed one.
3. **F4** — "`allowPartialMatch` behaviour is untouched", argued from `RequiredSlotMissPenalty` being unchanged rather than from the code path, which is gated on a different floor entirely.

The common shape: **measuring the direction you expect to move and not the one you don't.** Backlog item 2 touches the same selection path and will face the same temptation — its measurement plan should state both directions explicitly before it runs.

## Related

- Requirements: `Planning~/features/fidelity-miss-cost/requirements.md`
- Locked design: `Planning~/design-docs/scoring-model.md` §5.1, §7, §9 row 1
- Prerequisite: issue #66 / PR #67 (eager completeness guard) — on `main` at `5a03592`
- Successor: backlog item 2, `feat-coverage-in-selection` (§5.2 + DR-4) — closes #42's symptom 2
