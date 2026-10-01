---
type: requirements
feature: fidelity-miss-cost
topic: scoring-model
status: draft
updated: 2026-08-13
sources: [Planning~/design-docs/scoring-model.md]
---

# Fidelity Miss Cost — Requirements

## 1. Feature & scope

Backlog line (design §9, row 1): `feat-fidelity-miss-cost` — §5.1. `RequiredLiteralMissPenalty` → `0`; re-derive the 15 affected score assertions by hand; A/B per §7. Depends on issue #66 (merged as PR #67, on `main` at `5a03592`).

This feature realizes §5.1 of the locked scoring-model design and nothing else. It removes the **double-charge** on a missed required literal: today a dropped literal both withholds its credit *and* subtracts a penalty while still occupying the denominator, so one drop costs `1.5/N` of a ceiling that is fixed at `1.0`. After this feature a drop costs `1/N`. It is deliberately the smaller half of a two-part fix — §5.2's coverage term is backlog item 2 and is out of scope here (§6).

The design's diagnosis (§4) makes the order load-bearing: #58 blocked this work for months on the objection that reordering selection "flips selection but leaves the winner at `0.50`, under the `minScore` gate." That objection holds only while the fidelity defect stands. **This feature is what dissolves it**, by lifting the eventual reordered winner from `0.50` to `0.67`. Its value is therefore both direct (symptom 1) and enabling (item 2 becomes possible).

## 2. Why — user-visible cost of the defect

Short, specific patterns are structurally the most fragile to exactly the drop VOSK makes most often: a short unstressed function word. Observed in-headset during the #42 cycle, "time to target" heard as "time target" scored `0.50`, under the default `minScore` of `0.60`, and **the command did not fire at all** — while a 7-element pattern absorbed the identical single-word drop at `0.79` and fired cleanly. A grammar author who follows #58's authoring guidance perfectly still gets this: no bare sibling is involved, so neither #58's validation warning nor its `?by` remedy reaches the case.

The result is that pattern length, not pattern quality, decides whether a command survives a dropped function word. That is the model behaving exactly as documented (`Documentation~/scoring.md:49-61` already describes it under *"Short patterns are disproportionately fragile"*) — which is why the design frames this as a decision that the written-down model is wrong, not as a bug fix.

## 3. Observable behavior

A user speaks a registered command and VOSK drops one function word from it. Where the pattern has **three or more elements**, the command now fires with its slots filled, at the default `minScore`, instead of being silently rejected. Where the pattern has **two** elements it still does not fire — half the evidence is not enough, and "cease fire" heard as "fire" stays ambiguous with the `fire` command.

Scores reported everywhere the score surfaces — `VoxrCommand.Score`, session logs, `OnUnrecognisedSpeech` diagnostics, the batch test runner — move upward for any candidate that missed a required literal. Nothing else about recognition changes: the same commands are selected in the same order, the same slots are extracted, grammar generation and decoder output are byte-identical, and no new setting appears in the inspector.

## 4. Functional requirements

| #  | Requirement | Priority | Acceptance check | Source |
|----|-------------|----------|------------------|--------|
| F1 | A **missed required literal** contributes `0` to the numerator and `+1.0` to the denominator — the credit is withheld but no penalty is subtracted (`RequiredLiteralMissPenalty`: `-0.5` → `0`). A single dropped required literal therefore costs exactly `1/N` of the `1.0` ceiling, where `N` is the pattern's element count. | Must | `time to target` heard as "time target" scores `2/3 ≈ 0.667` (was `0.50`) and fires at the default `minScore`; `launch {weapon} target {target} on my mark` with one literal dropped scores `6/7 ≈ 0.857` (was `0.79`). | design §5.1 |
| F2 | **Symptom 1 is reproduced as a failing test before the change is made** — a test that pins the pre-change rejection, fails against the current scorer, and passes after F1. The same applies to every behaviour F3–F5 claims: each is a test, not an argument. | Must | The reproduction test is demonstrably red on the pre-change tree and green after; the commit history shows the test authored before or alongside the constant change, not retrofitted to it. | design §7.4; #65 first suggested starting point |
| F3 | **Two-element patterns still reject a single drop.** `cease fire` heard as "fire" scores `1/2 = 0.50`, below `minScore`, and does not fire as `cease fire`. This is preserved deliberately, not by accident. | Must | A test pins `cease fire` → "fire" at `0.50` and asserts no `cease fire` command is emitted. | design §5.1 |
| F4 | **`RequiredSlotMissPenalty` stays `-1.0`.** A missing required *slot* is materially different from a missing function word. ⚠️ **Two clauses of this row were falsified during the build and are corrected below the table** — the constant does stay `-1.0`, but "stays routed to the pending/partial path" and "`allowPartialMatch` behaviour is untouched" are both false, and the acceptance check discharges vacuously. | Must | The slot-miss constant is unmodified in the diff. Every existing `allowPartialMatch` / pending test passes unchanged — **but see the correction: that check cannot see the candidates this feature moves**, so F13's dedicated test carries the invariant instead. | design §5.1, DR-1 scope |
| F5 | **The `minScore` boundary fires.** The gate stays `≥` and is not tightened to `>`: two dropped literals on a 5-element pattern score `3/5 = 0.60` (was `0.40`) and pass. With all slots extracted, firing is the accepted outcome. | Must | A test pins a 5-element pattern with two dropped literals at `0.60` and asserts the command fires with its slots filled. | design §5.1 accepted consequence; §10 ruling 2 |
| F6 | **Every one of the 15 numeric score assertions is re-derived by hand.** Each changed expected value carries its derivation (`rawScore / denominator`, written out) so a reviewer can check the arithmetic without running the code. An assertion updated to match emitted output is a defect, not a pass. | Must | All 15 assertions in `Tests~/Runtime/VoxrCommandParserTests.cs` are accounted for — each either unchanged with a stated reason, or changed with its arithmetic shown; the review checks the arithmetic independently. | design §7.3 |
| F7 | **Fixture-manifest outcomes are reported, not absorbed.** Any of the 16 cases in `Tests~/Fixtures/audio/manifest.json` whose `expectedIntent`/`expectedSlots` change must be named, with the change argued as an improvement before it is written down. A silently-updated fixture expectation is the same defect as F6's. | Must | The change set is reported as "these N of 16 fixtures change outcome, for these reasons"; N may be `0`. Reviewer sees the list. | design §7.2 |
| F8 | **A/B measured against replayed real decoder output**, not synthetic token streams: the committed harness transcripts are replayed through the parser with the scorer before and after, reporting the score delta on every candidate that wins a round. The A/B rig is validated against the committed pin *before* being trusted. | Must | Rig validated against the pre-change baseline first (it reproduces committed behaviour); then a before/after delta report exists for the fixture corpus. | design §7.1–§7.2; project memory `grammar-ab-rig` |
| F9 | **Eager-flush verdicts stay truthful.** The invariant "an eager verdict always names the command the subsequent flush will actually fire" holds after the change. F1 raises unpenalised selection scores, so more utterances clear the eager gate — that is expected, but no utterance may get a verdict naming one command and then flush a different one. | Must | Existing eager-flush verdict tests pass; a test covers an utterance whose eager score newly crosses `0.6` under F1 and confirms verdict and flush agree. | design §5.4 |
| F10 | **The widened slot-miss hole stays closed.** §5.1 lets a slot-missing candidate newly cross `0.6` at 8+ elements with a literal drop alongside the slot miss (`(6−1)/8 = 0.625`, was `0.5625`). Issue #66's completeness guard must still refuse the eager commit in exactly that case. | Must | A test constructs the 8-element slot-miss case, pins the score above `0.6`, and asserts no eager commit occurs. | design §5.4; issue #66 (PR #67) |
| F11 | **Diagnostics keep rejecting what they rejected.** `VoxrCommandRecogniserDiagnosticTests.ScoreRejection_ReasonFormat` needs "launch missiles" to stay rejected: it scores `0.125` today and `0.25` under F1, still below `0.6`. | Should | That test passes unchanged. | design §2 blast radius |
| F13 | **Admission requires more evidence for a candidate than against it** (DR-7, added by Amendment A2 after the review cycle). A candidate whose missed **required** elements outnumber its matched required elements is refused entry to selection, in `IsBetterCandidate`, alongside the existing `Score <= 0f` filter. Optional elements count toward neither side. Stated over element counts, not over the score, so it stays independent of the coverage term backlog item 2 introduces. | Must | The rule is refused in *both* selection paths (`ParseInternal` and `TryEagerCommit`), which one shared comparator gives for free; a test per zero-crossing harm (round-1 pre-emption, result-buffer eviction, partial/pending entry); a test pinning the eager-gate inheritance; a test pinning the optional clause in **both** directions; and no new serialized field or configurable number. | design §0B, DR-7 |
| F12 | **No new tuning surface and no opt-in.** No new serialized field, no compatibility flag, no way to restore the old miss cost. Existing grammars re-score on upgrade. | Must | Diff introduces no `[SerializeField]` and no public API addition; inspector for `VoxrCommandRecogniser` is unchanged. | design §3 non-goals; DR-5; G0 scope |

### 4.1 F4 — corrected 2026-08-13 (PR #72 review)

Two clauses of F4 were written from the constant and not from the code, and both are false. The constant itself is untouched, so F4's *decision* stands; its *reasoning* and its acceptance check do not.

- **"stays routed to the pending/partial path" — false.** That routing is `if (cmd.Score < minScore)` at `VoxrCommandRecogniser.cs:668`, and it additionally requires the command to have opted into `allowPartialMatch`, which defaults **off**. So a slot-missing candidate is routed nowhere on the strength of the missing slot: below the gate at stock settings it is simply dropped, and *above* the gate it fires with the argument absent. Already true on `main` at five elements — the shipped demo pattern `launch {?quantity} {weapon} target {target}` on "launch all missiles target" scores `0.6000` with `target` absent at **both** revisions. This feature lifts one further band over the gate (eight elements, one dropped literal alongside the missed slot: `0.5625 → 0.625`), which is carried to G2 as a named consequence.
- **"`allowPartialMatch` behaviour is untouched" — false, and the acceptance check is vacuous.** "Every existing pending test passes unchanged" cannot detect this feature's effect, because the candidates it moves were previously *invisible* to those tests. §5.1 alone would have widened pending entry (14 of 135 partial demo utterances go from 0 results to pending-eligible); DR-7 then narrows it again, past `main` in the sparse direction. F13's dedicated test is what actually pins the invariant now.

## 5. Non-functional requirements

- **Determinism and purity.** The scorer stays a pure function of the token sequence and the registered patterns: same input, same score, every run. No new allocation, no new state, no ordering dependence introduced.
- ~~**Zero cost.**~~ **AMENDED 2026-08-13 (PR #72 review).** The original text — *"The change must not add per-candidate work — it removes an addition. Any measurable parse-time regression is a defect"* — was written before DR-7 and is false on both counts. Nothing is removed from the inner loop (`rawScore += 0f` still executes; `x + 0f` is not a float identity), and DR-7 *adds* two `int` increments per required element, two fields to a by-value struct (`MatchResult` 32 → 40 bytes), and one comparison per candidate. Measured: **+2–9 %** parse time, ≈`0.15–0.8 µs` on a ~`7.7 µs` per-utterance parse, on desktop x64. DR-7 does not offset it by cutting extraction rounds — base and head are byte-identical at 21 rounds over the demo corpus. **Amended requirement:** bounded per-element cost is accepted; allocation must not change, and it does not — `344.4 bytes/utterance` on both revisions, identical to the tenth.
- **Grammar generation untouched.** `GenerateGrammarJson` is not modified, so decoder output is unchanged and `NativeBridge~/harness/expectations.json` does **not** re-baseline. If the harness baseline moves, something out of scope was changed.
- **Public API shape unchanged.** `VoxrCommand.Score` keeps its type and its meaning as a single normalised value in `[0,1]` (DR-1). Only the values it takes move.
- **Failure legibility preserved.** Rejection diagnostics still name the score and the threshold; the score they print is the new one, so the number a user reads matches the number the gate used.
- **Verification is EditMode + PlayMode only.** Nothing here touches capture, the bridge, or lifecycle, so on-device verification is not required for this feature (design §7.5) — the one place in this project where "deferred to human" would otherwise be the standing answer.

## 6. Non-goals / deferred

- **Symptom 2 is not fixed by this feature.** After F1, `decelerate by {burn_level}` with "by" dropped rises to `0.67`, but bare `decelerate` still scores `1.0` and still wins selection, and the spoken "hard burn" is still discarded. #42's case closes at backlog item 2 (`feat-coverage-in-selection`), not here. This must not be claimed as fixed at G2.
- **No coverage term, leading or trailing** (design §5.2) — item 2.
- **No `skippedWordPenalty` → `coverageWeight` rename** (DR-4) — item 2. The field keeps its current name and meaning here.
- **No `minScore` change and no length-scaled threshold** (DR-2, rejecting #65 option (c)).
- **No length-independent miss cost** (fork F1 in design §6, rejecting #65 option (b) read literally): miss cost stays length-proportional, halved rather than abolished.
- **No selection-order change.** Keys stay: earliest start → score → consumed span → literal count → registration order.
- **No confidence-axis change.** `minConfidence` is orthogonal and untouched.
- **No retraction of #58's authoring guidance or the `?by` remedy.** They remain valid; this feature removes the need to lean on them.
- **No product-doc rewrite.** `Documentation~/scoring.md` §1–§3/§6–§7 and `command-recognition.md`'s Scored Matching section are backlog item 3's scope (see §8 open question).

## 7. Dependencies & assumptions

- **Issue #66 must be merged first** — it is, as of PR #67 (`main` at `5a03592`). §5.1 widens the hole #66 closes (F10), so the order is a hard prerequisite, not a preference.
- **The A/B rig** (project memory `grammar-ab-rig`) compiles the real parser under `dotnet` in WSL with a `UnityEngine.Debug` stub. Assumed still working against the current parser; F8 requires validating it against the committed pin before trusting any delta it reports.
- **Unity verification** runs through the host project `VoXR TestGround` (Unity 6000.4.7f1) per the project bindings — **both** EditMode and PlayMode, since the parser tests are PlayMode.
- **Assumed accurate:** `Documentation~/scoring.md` describes current behaviour correctly (the design's stated starting point, from #47). If implementation finds it does not, that is a finding to report, not to quietly work around.

## 8. Open questions

**Do the product docs lag this feature, or get a minimal correction with it?** The design assigns the full doc rewrite to backlog item 3, post-G2 of item 2 (§9 row 3, DR-5). Taken literally, item 1 merges to `main` with `Documentation~/scoring.md` still stating the old arithmetic — its worked examples, its fragility table, and its `1.5/N` claim all become wrong the moment this lands, and stay wrong until item 3. That is harmless if items 1–3 land inside one release; it ships incorrect documentation if a release is cut in between.

The fork, for the human to rule at G2: **(a)** ship a minimal factual correction with this feature — the affected numbers in `scoring.md` plus a `CHANGELOG` entry under `[Unreleased]` — leaving item 3's full rewrite intact; or **(b)** ship no product docs here and hold the release until item 2 merges, keeping the doc history clean at the cost of a window where `main` and its docs disagree. Nothing in the locked design forbids (a); §9 row 3 assigns the *rewrite*, not every sentence.
