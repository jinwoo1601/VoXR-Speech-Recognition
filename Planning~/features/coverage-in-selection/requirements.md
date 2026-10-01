---
type: requirements
feature: coverage-in-selection
topic: scoring-model
status: draft
updated: 2026-08-14
sources: [Planning~/design-docs/scoring-model.md]
---

# Coverage In Selection — Requirements

## 1. Feature & scope

Backlog line (design §9, row 2): `feat-coverage-in-selection` — §5.2 + DR-4. Orphan-tail term counted from `ConsumedEndIdx`, coverage moved inside selection, `skippedWordPenalty` → `coverageWeight` with `[FormerlySerializedAs]`. **The large one.** Depends on item 1 (`feat-fidelity-miss-cost`, merged as PR #72, `main` at `126a048`) and, by the human's ruling, on #73 (merged as PR #75, `main` at `f182c78`).

This feature realizes §5.2 of the locked scoring-model design and the DR-4 rename that follows from it. It charges a candidate for in-grammar tokens it leaves **orphaned on either side**, and — the part that makes this the large one — it computes that charge **inside** selection rather than after it.

That relocation is the whole feature. Today's leading skipped-word term (#31) is applied post-selection *specifically so it cannot reorder* (`VoxrCommandParser.cs:665-670`, and the design says so at §5.2). Symptom 2 cannot be fixed without reordering: the bare pattern wins on a perfect `1.0` that nothing normalised to `1.0` can beat, so the only way to demote it is to make its score reflect what it failed to explain. The design names this "the single largest behavioural change in this design and the one most in need of measurement".

This feature closes **symptom 2** of #65 — the symptom item 1 deliberately did not fix — and with it #42. Issue #65 stays open until this lands.

## 2. Why — user-visible cost of the defect

A grammar registers both `decelerate` and `decelerate by {burn_level}`. The speaker says *"decelerate hard burn"*; VOSK drops the unstressed "by". The bare pattern matches perfectly and scores `1.0`; the slot-filled pattern is charged for the dropped literal and scores `2/3 ≈ 0.67` after item 1. **The bare form wins, and "hard burn" — the argument the speaker actually said — is silently discarded** with nothing in the log to signal that anything was dropped.

The user experience is worse than an outright rejection. The command *fires*, so there is no error, no retry prompt, and no diagnostic: the ship decelerates at some default rate while the captain believes they commanded a hard burn. A rejection is legible; a command that fires with its argument quietly deleted is not.

No threshold reaches this. `minScore` cannot help — the winner is at `1.0`, the ceiling. No penalty tuning reaches it either, which is what #58 correctly concluded and why this needed a re-derived model rather than a constant change. Coverage is the only thing that inverts it, because it is the only term that asks what the candidate *left unexplained* rather than how well it matched.

The asymmetry is the root: leading coverage has existed since #31, trailing coverage has never existed at all. In-grammar tokens left unconsumed *after* a match cost nothing (`:665-670` guards on `bestStartIdx > searchStart` and counts only to the match start). Bare `decelerate` keeps a clean `1.0` while "hard burn" sits unexplained on the floor.

## 3. Observable behavior

A speaker says a command whose optional-argument form was registered alongside a bare form, and VOSK drops a function word. The **slot-filled** command now fires with its argument, where the bare form used to fire without it. The score the user sees on the winner is lower than before — it is now the fraction of the utterance the pattern actually explains, on both sides — and a bare pattern stranded in the middle of a longer in-grammar phrase is demoted rather than winning at `1.0`.

Multi-command utterances are unaffected: *"cease fire, launch missiles target hotel one"* still extracts both commands, because a trailing token that could begin another pattern is never charged as an orphan.

The inspector shows a field named **`coverageWeight`** where it showed `skippedWordPenalty`; any value previously set on it survives the upgrade. Code referencing the old property name still compiles, with an obsolescence warning naming the replacement.

Nothing else changes: same slots extracted, same grammar JSON, same decoder output, same `minScore`, no new setting.

## 4. Functional requirements

| # | Requirement | Priority | Acceptance check | Source |
|---|---|---|---|---|
| F1 | **Coverage is computed per candidate and applied before selection compares candidates.** The post-selection penalty block at `VoxrCommandParser.cs:665-671` is removed; the coverage-adjusted score is what `IsBetterCandidate` ranks on and what `VoxrCommand.Score` reports. **Carve-out:** `ScoreFollowUp` (`:1370`) is exempt — see the note below the table. | Must | The score a candidate is *selected* on equals the score it is *reported* with — no site recomputes or re-adjusts a score after selection, `ScoreFollowUp` excepted. A test shows a candidate winning a round it would lose under post-selection application, and vice versa. | design §5.2 |
| F2 | **The score formula is** `rawScore / (denominator + (skippedBefore + orphanedAfter) × coverageWeight)`, with `coverageWeight` defaulting to `1.0`. One weight governs both sides — leading and trailing are one rule, not two mechanisms (goal 3). | Must | Hand-derived arithmetic for the §5.2 worked table reproduces exactly: `decelerate` on "decelerate hard burn" = `1/(1+2)` = `0.333`; `decelerate by {burn_level}` with "by" dropped = `2/3` = `0.667`; `cease fire` in the multi-command utterance = `2/2` = `1.000`. | design §5.2 |
| F3 | **A trailing token is orphaned iff it follows the candidate's `ConsumedEndIdx` and no active pattern could begin a match at it. Counting stops at the first token that could begin one** — it is a run from the consumed end, not a total of all unexplained tokens. **Amended by A3 (see below):** at the run's first position only, a token the candidate's own next required element failed to match is charged outright rather than tested against the predicate. | Must | A test pins each half: a trailing in-grammar token that starts no pattern is charged; a trailing token that starts a pattern is not charged, **and neither is anything after it**. Plus the A3 case: the mis-predicted token is charged even when it could begin a pattern. | design §5.2 |
| F4 | **The count starts at `ConsumedEndIdx`, never `EndIdx`.** `EndIdx` runs past `[unk]` the pattern never matched, so counting from it would let a candidate shed orphans by absorbing noise. | Must | A test constructs a candidate whose `EndIdx` exceeds its `ConsumedEndIdx` over a trailing `[unk]` run and confirms the orphan count is unchanged by that run's presence. | design §5.2; #41 precedent |
| F5 | **`[unk]` is never charged, on either side**, and does not terminate an orphan run. ⚠️ **The second sentence of this row was overclaimed and is corrected below the table** — the exemption is for the `[unk]` *token*, not for out-of-grammar words generally. | Must | An utterance with in-grammar content flanked by `[unk]` on both sides scores identically to the same utterance without the `[unk]`; and `"X [unk] Y"` charges the same orphans as `"X Y"`. | design §5.2 |
| F6 | **Counting restarts at each extraction round** on the leading side, as today; on the trailing side the orphan test performs the same job. A second command in a multi-command utterance starts clean. | Must | Existing #31 multi-command tests pass unchanged; a test confirms the second extracted command is not charged for tokens the first consumed. | design §5.2 |
| F7 | **Symptom 2 inverts, and the winner clears the gate.** For the #42 pair, `decelerate by {burn_level}` at `0.667` beats bare `decelerate` at `0.333`, and `0.667 ≥ minScore`, so the slot-filled command fires with `burn_level` extracted. | Must | A test speaks "decelerate hard burn" against both registered patterns and asserts the emitted command is the slot-filled one, carrying `burn_level = "hard burn"`. | design §5.2; goal 2 |
| F8 | **Sequential extraction survives — this is the fork-F5 guard.** `cease fire` in "cease fire launch missiles target hotel one" scores `2/2 = 1.00`, not `2/7 = 0.29`, and both commands are still extracted in order. | Must | A test pins both extracted intents and the leading command's score at `1.00`. Charging all trailing tokens (no orphan test) must make this test fail — verify by temporary ablation, so the test is known to discriminate. | design §5.2, §6 fork F5 |
| F9 | **Both #65 symptoms are reproduced as failing tests before the change is made.** Each behaviour F7/F8 claims is a test that is demonstrably red on the pre-change tree, not an argument. | Must | The reproduction tests fail against `main` and pass after; commit history shows them authored before or alongside the change. | design §7.4 |
| F10 | **Selection keys are unchanged**: earliest start → score → consumed span → literal count → registration order. The consumed-span key (#41) **demotes** to a genuine tie-break because the score key now carries coverage — it is preserved, not superseded or removed. | Must | The key list in `IsBetterCandidate` is unchanged in order and membership; a test still exercises the consumed-span tie-break on two candidates of genuinely equal score. | design §5.3 |
| F11 | **DR-7 stays independent of coverage, and the orphan test is defined over *active patterns*, not over *admitted candidates*.** DR-7 is a unary admission filter over element counts; coverage is a ranking term over the score. No coverage value may lift a DR-7-rejected candidate back into selection, and **no candidate's orphan count may depend on any *other* candidate's verdict** — a pattern's claim on a token comes from the pattern being registered and active, never from whether a candidate anchored there survived admission. | Must | The orphan test consults the registered active patterns only. A test pins a DR-7-rejected candidate staying rejected at every `coverageWeight` including `0`; a second pins that a DR-7-**refused** candidate's pattern still terminates an orphan run, which is the half that distinguishes the two definitions. See §9.1 and §4.3. | design §0B/§A2.5; DR-7 |
| F12 | **The eager gate's verdict stays truthful**: an eager verdict still names the command the subsequent flush actually fires, and no candidate that reaches a verdict above `None` carries a non-zero trailing orphan count. §5.4's argument for this is **re-derived against DR-6/DR-7, not inherited** — see §9.2. | Must | A test pins verdict-and-flush agreement on an utterance whose score newly moves under coverage; and the claim that the `ConsumedEndIdx`→`EndIdx` gap is provably all-`[unk]` is verified against the matcher, not assumed. | design §5.4; DR-6 |
| F13 | **`skippedWordPenalty` is renamed to `coverageWeight`, preserving serialized values.** `[FormerlySerializedAs("skippedWordPenalty")]` on the `[SerializeField]`, plus an `[Obsolete]` forwarding property kept for one minor version. All eleven identified sites move together: parser `:77/91/151/158`, recogniser `:39/173/275`, batch runner `:23/28/36/63`. | Must | A scene/prefab with a non-default `skippedWordPenalty` set before the upgrade still reports that value on `coverageWeight` after it; code using the old property name compiles with an obsolescence warning naming the new one; no site retains the old name except the compatibility shims. | DR-4; design §10 ruling 1 |
| F14 | **The A/B is reported in BOTH directions** — what newly wins *and* what newly loses — stated in the measurement plan **before** it is run. This is a hard requirement, not a reporting nicety: it is the failure mode that cost this design two G1 reopens. | Must | The measurement plan names both directions in advance; the report lists newly-winning candidates **and** newly-losing ones, with `0` an acceptable count only if stated as a measured result, never as an unexamined default. | design §7; item 1 architecture §13.1 |
| F15 | **The corpus is verified to contain the phenomenon before any A/B delta is treated as evidence.** A zero delta over a corpus that cannot exercise coverage is not a regression check passing — it is a measurement that never ran. | Must | Before reporting, the utterances in the corpus that actually carry leading skips or trailing orphans are enumerated and counted. If none do, the A/B is reported as inapplicable and hand-derivations plus new tests carry the evidence instead. | design §7.2; A1 precedent (§0/A1.4); #73 verified fact |
| F16 | **Every numeric score assertion is re-derived by hand**, with its arithmetic shown, and every fixture-manifest outcome change is named and argued as an improvement before it is written down. An assertion updated to match emitted output is a defect. | Must | Each changed expected value carries its `rawScore / (denominator + orphans × weight)` derivation in the test or its comment; the fixture delta is reported as "these N of 16 change outcome, for these reasons". | design §7.3, §7.2 |
| F17 | **No new tuning surface.** `coverageWeight` is the existing knob under its honest name, not a new one. No second weight, no separate leading/trailing weights, no opt-in flag, no compatibility mode. | Must | The diff adds no `[SerializeField]` beyond the rename and no new public configuration member; the `VoxrCommandRecogniser` inspector has the same field count as before. | design §3 non-goals; DR-4; G0 scope |
| F18 | **`coverageWeight = 0` disables coverage on both sides coherently.** The design flags that a user who set the old field to `0` would otherwise silently also disable the #42 fix; under one field that is now explicit rather than hidden. | Should | A test pins that at `0` the score reduces exactly to `rawScore / denominator` and symptom 2 reverts to its pre-feature behaviour. | DR-4 rationale |

### 4.1 F1 and F5 — corrected 2026-08-14 (plan validation)

Two rows were written from the design's wording rather than from the code, and both overclaim. The *decisions* stand; the wording did not.

- **F1's "no site recomputes a score after selection" — false as an absolute.** `ScoreFollowUp` (`VoxrCommandParser.cs:1370`, called from `PendingCommandHandler.cs:166`) re-scores a pending command after slot-fill and hands the result to `OnCommandConfirmed`/`OnCommandRecognised` and to diagnostics (`VoxrCommandRecogniser.cs:640`). After this feature it is the only score in the system without a coverage term. **Proposed carve-out:** it stays uncovered, because a follow-up score measures *pattern completion* against a **different utterance** than the one the pending command matched in — there is no single token array over which "orphaned" is even defined. It is not gated against `minScore`, so the harm is legibility, not firing. To be confirmed at review rather than assumed; architecture §7 carries it.
- **F5's "out-of-grammar preamble, hesitation and noise are free trailing" — false.** The implementation exempts the literal token `[unk]` only; any non-`[unk]` token that starts no pattern is charged, in-grammar or not. In the normal decoder path out-of-grammar words arrive *as* `[unk]`, so the two coincide — but `freeSpeechMode`, `InjectText` and `VoxrBatchTestRunner.Run` all deliver real tokens, and there the promise fails. The leading term already behaves this way (so the rule stays symmetric, per goal 3), but this feature newly exposes the **trailing** side, where filler is commoner: `"cease fire please"` drops `1.0` → `0.667`. Reported at G2, not silently absorbed.

### 4.2 F3 — amended 2026-08-14 by design Amendment A3 (G1 re-lock)

Phase 7's A/B found that the orphan test as locked **rewards a candidate for matching less**, and fires the wrong command on an ordinary grammar shape. `["switch","to","navigation"]` heard against *"switch to weapons target hotel"* misses its final element, so its consumed span stops at "weapons" — which begins `["weapons","mode"]`, terminating its orphan run at zero for `2/3` = 0.667 — while `["switch","to","weapons"]` matches that element, moves its origin past the very token that would have terminated its own run, and pays `3/(3+2)` = 0.600. The wrong command wins and fires.

A3 charges the mis-predicted token instead of testing it, at the run's first position only. Measured against `main` over 699 utterances: the wrong-command class goes 2 → **0**, `newly-silent` 18 → **17**, and every acceptance case (F7 ×3, F8, F11, F18, §8, both span ties) is unchanged.

**Residual, ruled acceptable at the same G1 and bound for `KNOWN_LIMITATIONS.md`:** where a *second* command in one utterance loses its own leading word, the first command is still charged for the second's tokens and can fall below `minScore`. It loses a command; it never fires a wrong one.

### 4.3 F11 — narrowed 2026-08-14 after the PR #78 review

F11 originally said the orphan test *"takes no input from `IsBetterCandidate`, `MissedRequired`/`MatchedRequired`, or the `Score <= 0f` filter."* Amendment A3 makes it read exactly one piece of per-candidate state — *did this candidate's own next required element fail at this token?* — so the row as written now contradicts the shipped code.

**The code is right and the wording was too broad.** The property F11 exists to protect is that coverage must not become a function of DR-7's verdicts, because that is what would make §A2.5's orthogonality claim false: a rejected candidate withdrawing its pattern's claim on a token would silently lower a *different* candidate's score. A3 creates no such coupling — it reads only the candidate's own match state, and every candidate at a given position reads its own independently.

Narrowed to that property, and it is now tested in both directions rather than one: `Coverage_CannotLiftACandidateTheAdmissionRuleRefused` covers the first half, and `Coverage_ResidualHazard_WhenTheStrandedValueBeginsAPattern` — whose fixture is deliberately DR-7-**refused** — covers the second, since it only passes if a refused candidate's pattern still terminates the orphan run.

Recorded because the deferred admissibility probe will be argued against this row, and the broad wording would have ruled out a sound design for the wrong reason.

## 5. Non-functional requirements

- **Bounded per-candidate cost.** Coverage is evaluated inside a triple-nested loop (commands × patterns × start index), so a naive per-candidate scan of the trailing tokens multiplies parse cost by the utterance length. The orphan count for a given consumed-end position must be obtainable in **O(1)** at candidate-evaluation time, with any preparation done **once per extraction round**, not per candidate. Item 1 measured `+2–9 %` parse time and established the baseline (`≈7.7 µs` per utterance, `344.4 bytes/utterance`); this feature reports against that baseline.
- **Allocation must not grow per utterance.** Any precomputed structure is pooled or reused across rounds and utterances, matching the existing `_matchSlotBuf` / `_resultBuf` discipline. A per-round allocation proportional to token count is a defect, not a tradeoff.
- **Determinism and purity.** The scorer stays a pure function of the token sequence and the registered patterns: same input, same score, every run. Coverage introduces no ordering dependence between candidates — a candidate's score must not depend on which candidates were evaluated before it.
- **Grammar generation untouched.** `GenerateGrammarJson` is not modified, so decoder output is unchanged and `NativeBridge~/harness/expectations.json` does **not** re-baseline. If the harness baseline moves, something out of scope was changed.
- **Public API shape unchanged apart from DR-4.** `VoxrCommand.Score` keeps its type and its meaning as a single normalised value in `[0,1]` (DR-1). The `[0,1]` bound must be re-verified, not assumed: the denominator only grows, so the ceiling holds, but the argument is stated rather than inherited.
- **Failure legibility preserved.** Rejection diagnostics still name the score and the threshold, and the score they print is the coverage-adjusted one — the number a user reads matches the number the gate used. A command rejected *because of* coverage should be distinguishable in diagnostics from one rejected on fidelity, if that costs nothing; if it costs a new field, it is deferred (DR-1).
- **Verification is EditMode + PlayMode only.** Nothing here touches capture, the bridge, or lifecycle, so on-device verification is not required (design §7.5).

## 6. Non-goals / deferred

- **No product-doc rewrite of the scoring model.** Per the human's G0 ruling, item 2's post-G2 docs are the **DR-4 rename across the six files that name `skippedWordPenalty`, plus a `CHANGELOG` behaviour-change note** — nothing more. `scoring.md` §1–§3/§6–§7 and `command-recognition.md`'s Scored Matching section stay backlog item 3 (`feat-scoring-docs-reconcile`).
- **No `minScore` change**, and no length-scaled threshold (DR-2, rejecting #65 option (c)).
- **No additive coverage form** `(F + w·C)/(1+w)` (fork F3): rejected for needing a free parameter with no principled value.
- **No promotion of consumed span above score in selection** (fork F4).
- **No charging of all trailing tokens** without the orphan test (fork F5): it breaks multi-command utterances.
- **No re-derivation of DR-7** (design §A2.5 scope note). Its independence is *verified* here (F11), not reopened.
- **No change to `RequiredLiteralMissPenalty` or `RequiredSlotMissPenalty`.** Item 1 settled the first; the second stays `-1.0`.
- **No confidence-axis change.** `minConfidence` is orthogonal and untouched.
- **Issue #74** (route ambiguous sibling ties to confirmation) is not a prerequisite and is not addressed here.
- **Issues #76 and #77** (filed 2026-08-14) are out of scope. #76 is relevant — it hardens three tests pinned at exactly `0.60`, a denominator this feature rewrites — but it is tracked separately by ruling.

## 7. Dependencies & assumptions

- **Item 1 must be merged first** — it is, as PR #72 (`main` at `126a048`). The dependency is load-bearing, not administrative: §4's diagnosis is that fixing fidelity first is what dissolves #58's blocking objection, because the reordered winner lands at `0.67` rather than `0.50`.
- **#73 must be merged first** — it is, as PR #75 (`main` at `f182c78`), by the human's explicit ruling that it lands ahead of item 2 to avoid rebasing this feature through the lines it rewrites.
- **The A/B rig is copied and extended, not rebuilt.** `Planning~/features/fidelity-miss-cost/ab-rig/` (`stage.sh`, `Program.cs`, `Check.cs`, `ablate.py`) stages real parser sources from any git ref and compiles them under `dotnet` with a `UnityEngine.Debug` stub. It reproduces the committed 133-entry grammar pin byte-identically. It must be re-validated against the pin before any delta it reports is trusted (project memory `grammar-ab-rig`).
- **Unity verification** runs through the host project `VoXR TestGround` (Unity 6000.4.7f1) per the project bindings — **both** EditMode and PlayMode, since the parser tests are PlayMode. Baseline to beat: EditMode 116/116, PlayMode 373/373 (`Planning~/verification-runs/fe33f33/`).
- **Assumed, and to be verified during the build:** that the token region between a match's `ConsumedEndIdx` and its `EndIdx` consists only of `[unk]`. The parser asserts this in a comment (`VoxrCommandParser.cs:1540-1543`) and §5.2 relies on it ("`[unk]` between the two indices is free regardless, so the two rules agree"). F12 requires it verified against the matcher rather than inherited from either.

## 8. Open questions

**Does a bare pattern demoted by coverage still deserve to fire when it is the *only* candidate?** §5.2 demotes bare `decelerate` on "decelerate hard burn" to `0.333`, below `minScore`. That is correct when the slot-filled sibling exists to win instead. But the same arithmetic applies when **no sibling is registered**: a grammar with only `decelerate` registered, hearing "decelerate hard burn", now scores `0.333` and **fires nothing**, where today it fires `decelerate` at `1.0`.

This is a real behaviour change for single-pattern grammars and the design does not name it. It may be exactly right — the utterance contains two words the grammar cannot explain, so treating it as "not this command" is defensible, and it is the same logic #31 already applies on the leading side (a one-element pattern reached past one skipped word scores `0.5` and is rejected). It may equally be a regression for authors whose users add natural trailing words the grammar was never meant to model ("decelerate please", "decelerate now").

Not resolvable from the locked design, and it is **not** a G1 reopen unless measurement shows it biting: the rule is exactly as locked, this is a consequence of it. **Deferred to G2 as a named consequence**, with F14/F15's measurement expected to quantify how many corpus utterances land in it. If the count is material, the honest options are to document it in `KNOWN_LIMITATIONS.md` or to reopen §5.2 — the human's call, not this feature's.

## 9. Questions resolved into this doc

Three questions carried in from the handoff, resolved here rather than discovered during implementation.

### 9.1 DR-7 versus coverage — orthogonal, but conditionally

§A2.5 calls DR-7 and coverage "orthogonal by construction". A review angle argued this is really *one-way precedence* — DR-7 a fidelity veto coverage is structurally unable to overturn — and that goal 2 ("a candidate that explains more of the utterance must be able to beat one that explains less") is precisely a promise that coverage overrides fidelity. Rated a nit, never verified.

**Resolution: both readings are right about the mechanism, they do not conflict, and the orthogonality holds — but it is conditional on an implementation constraint that nothing in the design states.**

The mechanism is not in dispute. DR-7 is a **unary** predicate on one candidate (`MissedRequired > MatchedRequired` → reject), evaluated without reference to any other candidate and without reading the score. Coverage is a **ranking** term affecting pairwise comparison. A filter and a sort key do not compete: DR-7 removes candidates from the pool, coverage reorders what remains. So "one-way precedence" is an accurate description, and "orthogonal" is accurate about the consequence — no coverage value can un-reject a DR-7 casualty, because DR-7 never reads the score.

The apparent conflict with goal 2 dissolves on inspection of *where* DR-7 bites. A candidate is rejected only when it missed more required elements than it matched — that is, when most of its pattern went unspoken. Such a candidate consumes proportionally few tokens, so it leaves *more* orphaned, not fewer: it is systematically **low**-coverage. DR-7's rejection region and coverage's "explains more" region do not overlap, so DR-7 never vetoes the candidate goal 2 is about. The two point the same direction wherever DR-7 applies.

**The condition, which is F11 and is the part worth writing down:** this holds only if the orphan test is defined over **active patterns** ("could any registered pattern begin a match at this token?") and not over **admitted candidates** ("did any candidate that survived admission begin here?"). The second formulation is a natural implementation shortcut — the selection loop already has candidates to hand — and it would couple the two rules: DR-7 rejecting a candidate would remove a pattern's claim on a token, turning that token into an orphan and lowering some *other* candidate's score. Coverage would then depend on DR-7's verdicts, the orthogonality would be false, and the design's scope note would silently stop holding. F11 forbids it.

Per §13.1's lesson this is stated as a claim to **verify**, not to assert: F11's acceptance check includes searching the corpus for any utterance where a DR-7-rejected candidate has strictly higher coverage than the admitted winner. If one exists, the analysis above is wrong and it needs a G1 reopen — the human's call.

### 9.2 The eager gate's argument does not survive intact — it needs restating on a different basis

§5.4 claims the trailing term cannot destabilise the eager gate "structurally: a verdict above `None` already requires the match to reach the end of the buffer, so any candidate that gets a verdict has zero trailing orphans by construction," and that condition 1's soundness argument is "untouched". That predates DR-6 and DR-7. Checked against the live code (`VoxrCommandParser.cs:1524-1590`):

**The conclusion holds. The stated reason does not.** The end-of-buffer condition is `bestEndIdx != tokens.Length` (`:1588`) — it is over **`EndIdx`**, while §5.2 counts orphans from **`ConsumedEndIdx`**. Those differ: the parser's own comment (`:1540-1543`) notes `EndIdx` "can still carry over a trailing `[unk]` run". So "reaches the end of the buffer" does **not** by itself imply zero trailing orphans. What implies it is that the gap between the two indices is all-`[unk]`, and `[unk]` is never charged (F5). The argument is sound on that basis, and F12 requires the all-`[unk]` claim verified against the matcher rather than taken from a comment.

**DR-6 strengthens it.** `bestHasUnmatchedRequiredTail` (`:1571`) already refuses any candidate with a required element after the last matched one, so a committed candidate's pattern genuinely ended rather than ran out of buffer. That is a stronger guarantee than §5.4 had available when it was written — good news, but it is a *new* reason, and inheriting the old sentence would hide that the old reason was incomplete.

**The genuinely new exposure is the leading term, which §5.4 explicitly declines to consider.** The eager scan today computes its score **without** the leading penalty, and the code says why (`:1596-1601`): condition 2 forces `bestStartIdx == firstRecognisedIdx`, so the skip count is zero and the eager and flush scores are *identical*. This feature moves the leading term inside selection, so the eager scan can no longer omit it — it is computed by the same code path. Two consequences to discharge in the architecture doc:

1. **Ordering.** The score gate (`:1524`) runs *before* condition 2 (`:1588`). A candidate with a non-zero leading skip now reaches that gate with a coverage-reduced score. Both paths still end at `None`, so no correctness break — but the *winner* of the eager scan can differ. Selection's first key is earliest start, which already dominates, so leading coverage is expected to be inert here; expected, and therefore to be measured rather than assumed.
2. **The two paths must compute coverage over the same input.** §5.4 asserts the verdict-truthfulness invariant is preserved "because both paths now compute the trailing term identically". They compute it by the same *rule*, but `TryEagerCommit` scans the in-progress buffer while `ParseInternal` scans the flushed utterance, and the orphan test depends on the token array it is given. The invariant holds only if the buffer the verdict was computed on is the buffer the flush then parses. That is F12's check, and it is the one place this feature could break an invariant the design believes is free.

### 9.3 "Could begin a match" — the definition, and its degenerate case

Neither §5.2 nor the code pins down what "no active pattern could begin a match at it" means for a pattern whose first element is a **slot**. No pattern in the demo grammar starts with one, but nothing forbids it, and `heading` is an open-ended `NumberSequence` slot — so a user grammar can make a large class of tokens a potential pattern start.

**Resolution: the test is deliberately conservative — a token is *not* an orphan whenever any active pattern could plausibly begin there.** A token counts as a potential start if it matches the first element of some active pattern, where "first element" means the pattern's leading element and, if that element is optional, each following element up to and including the first required one. For a literal element that is a string comparison; for a slot element it is whether the slot could match at that token.

Conservatism is the correct bias and the design already implies it: over-charging orphans is what fork F5 was rejected for, because it destroys sequential extraction. Under-charging merely leaves a candidate's score higher than ideal — the pre-feature behaviour. So where the predicate is uncertain, it must answer "could begin" and charge nothing.

**The degenerate case is real and must be reported, not hidden.** A grammar with a slot-initial pattern over a permissive slot can make nearly every token a potential start, driving `orphanedAfter` to zero everywhere and silently disabling the trailing half of this feature for that grammar. That is *safe* — it reverts to today's behaviour — but an author would have no way to know. If measurement confirms it, it belongs in `KNOWN_LIMITATIONS.md` at G2, with the authoring guidance that a slot-initial pattern weakens coverage for the whole grammar.

## Related

- Locked design: `Planning~/design-docs/scoring-model.md` §5.2, §5.3, §5.4, DR-4, §7, §9 row 2, §0B/A2.5
- Predecessor: `Planning~/features/fidelity-miss-cost/` (item 1, PR #72) — read its architecture §13.1 before writing the measurement plan
- Prerequisite: #73 / PR #75 (`main` at `f182c78`)
- Closes: #65 symptom 2, and #42
- Successor: backlog item 3, `feat-scoring-docs-reconcile` (DR-5)
- Out of scope but adjacent: #74, #76, #77
