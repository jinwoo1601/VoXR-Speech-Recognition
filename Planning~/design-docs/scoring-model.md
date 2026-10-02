# Design: Re-derived Command Scoring Model

- **Status:** **LOCKED — re-locked at G1 (human sign-off, 2026-10-02) with Amendment A5** (§0E), which exempts a resolver-fillable missed required slot from the score (DR-8) and adds the fewer-exempted-slots selection key (DR-9). Immutable again from here. Previously: re-locked at G1 (human sign-off, 2026-08-14) with Amendment A3 (§0C), which corrects **§5.2's orphan test**: a token the candidate's own next required element failed to match is charged rather than tested against the start predicate. Found by measurement during item 2's Phase 7 A/B, not by re-reading — the rule as locked let a candidate be *rewarded for matching less* and fired the wrong command. Immutable again from here. Previously: re-locked at G1 (human sign-off, 2026-08-13) with Amendment A2 which adds **DR-7** — admission to selection requires more evidence for a candidate than against it — and **supersedes A1 ruling 2**, whose stated rationale implementation proved false. Immutable again from here. Previously: LOCKED at G1 (human sign-off, 2026-08-12), DR-1…DR-5 ratified as proposed with DR-3 routed to the issue lane as #66 (§10); re-locked at G1 (human sign-off, 2026-08-13) with **Amendment A1** (§0), which adds **DR-6** and a second cross-lane prerequisite ahead of backlog item 1. DR-3 is *completed* by DR-6, not superseded. The §9 backlog is canonical.

  **Why reopened (2026-08-13):** implementing backlog item 1 revealed that **A1 ruling 2's stated rationale is false**. That ruling accepted the zero-crossing on the premise that "`minScore` at the recogniser is the real gate — at default settings a user sees nothing new." Three independently reproduced mechanisms show otherwise: at default settings the change can **create a false positive**, **lose a true positive**, and **widen pending entry**. The *decision* may well still be right; the *premise it was ruled on* is not, so the ruling is re-put with correct facts. Per the workflow's rule — implementation contradicting a locked decision means reopening and re-locking, never patching in place — `feat-fidelity-miss-cost` is **halted with its code unchanged** pending this ruling. Everything outside A1 ruling 2 stands exactly as locked.
- **Branch:** `design-scoring-model`
- **Date:** 2026-08-12
- **Scope agreed at G0:** re-derive the scoring model as one coherent model — miss cost, utterance coverage on both the leading and trailing side, the `minScore` gate, and the eager-flush threshold — rather than patching the two symptoms in #65 individually. The fix ships **default-on with no opt-in flag and no new tuning knob**.
- **Origin:** issue #65, carried forward from #42 (closed by #58). Starting point is the model documented in `Documentation~/scoring.md` (#47), which this design treats as an accurate description of current behaviour.

---

## 0. Amendment A1 — the eager gate's literal exemption (RATIFIED 2026-08-13)

> **RATIFIED — re-locked at G1 (human sign-off in conversation, 2026-08-13: "locked").** The three questions A1 raised are ruled in §A1.5, and DR-6 follows from ruling 1. This section is immutable from here. Raised by the plan-validation pass on `feat-fidelity-miss-cost`, re-verified by hand against the live tree.

### A1.1 What was found

**DR-3's stated rationale is false in a case the design never tested.** §5.4 and the shipped code (`Runtime/Commands/VoxrCommandParser.cs:1363-1366`) exempt missed required *literals* from the eager completeness guard on the reasoning that "the command is still fully determined and its arguments are all present." That holds when the dropped literal is **medial**. It fails when the dropped literal is the **terminal, discriminating** word of the pattern — then the command is precisely *not* determined, and the word that would determine it is the next one the speaker is about to say.

The eager gate's end-of-buffer condition (`:1386`) cannot catch this: a miss consumes no token, so it never advances `EndIdx`. That is the identical mechanism §5.4 already describes for slots — the design applied it to one element class and not the other. `ConsumedEndIdx` does not catch it either: a trailing miss leaves both indices at the buffer end, so **no index-based test can distinguish "the pattern ended exactly at the buffer end" from "the pattern ran out of buffer with elements still owing."**

Demonstrable in this package's own demo grammar (`Tests~/Runtime/DemoGrammar.cs:136,143`), which registers `["switch","to","weapons"]` and `["switch","to","navigation"]`. With `"switch to"` buffered:

| | score | eager verdict |
|---|---|---|
| today | `(1+1−0.5)/3` = **0.50** | `None` — blocked by the score gate alone |
| after §5.1 | `2/3` = **0.667** | **`Commit`** |

Both patterns tie at `0.667`, so registration order decides (`:884-891`) and `mode_weapons` wins. A speaker saying *"switch to navigation"* eagerly fires **`mode_weapons`** — the wrong command, mid-utterance, before the disambiguating word arrives.

**The defect is already live on `main`, independent of §5.1.** Any pattern ending in a required literal, with that literal unspoken, scores `(N−1.5)/N` — at `N=4` that is `0.625`, over the default gate, with every other eager condition satisfied. §5.1 does not create the hole; it widens it from `N≥4` down to `N≥3`, which is where two-word-prefix grammars live. Structurally this is #66 one element class over, and §5.4's claim that "smaller patterns are unaffected" is true only of the slot class it was computed for.

### A1.2 The fork — what the eager gate should require

**Option A — symmetric guard.** No verdict above `None` when the winner missed **any** required literal, exactly mirroring #66's slot rule. One extra bool set at `:993`. Conservative and trivially correct. **Cost:** the medial-drop case `:1363-1366` defends stops committing early and pays the full `bufferWindow` before firing. It still fires — this trades latency, never correctness — and dropped function words are common (that is §1's whole premise), so this class is not rare.

**Option B — unmatched-required-tail guard.** No verdict above `None` when any **required** pattern element sits after the last element that actually matched. Blocks exactly the dangerous case (the buffer ended where the pattern still needs a word) and preserves eager latency for medial drops. Costs a tracked pattern index plus a short tail scan in `TryMatchScored`. #66's slot rule stays alongside it unchanged — that rule is strictly stronger for slots, since a *medial* missed slot means an absent argument and must block wherever it occurs.

**Recommendation: Option B**, with A as the fallback if implementation proves it costlier than it looks. B is the principled statement of what the gate has always been trying to express — *nothing the speaker says next can change this answer* — and it is what makes the end-of-buffer condition honest rather than vacuous. A is the safe approximation of B, and its cost lands on exactly the degraded-recognition utterances §5.1 exists to rescue.

**Option C — make the miss cost position-dependent** (keep the penalty for a trailing literal, zero it for a medial one). **Rejected on the same grounds as F2/DR-2:** it changes the *score* to fix an *eager-gate* problem, and it makes the score depend on where a word fell rather than on what was matched — reintroducing precisely the "not derivable from stated principles" defect goal 5 exists to remove.

### A1.3 Second ruling needed — §5.1 moves candidates across zero

Raising a missed literal's contribution raises `rawScore` by `0.5` per miss, and zero is a hard disqualifier at `:610` and `:880`. Candidates with `0 < C−S ≤ 0.5m` therefore move from *rejected* to *emitted* as additional results in later extraction rounds. Two existing tests see this (`Tests~/Runtime/VoxrCommandParserTests.cs:648` and `:1600`, each 1 → 2 results).

**Proposed: accept and record.** The `≤ 0` floor was only ever meaningful *because* penalties drove scores negative; with the literal penalty gone it is a weaker filter by construction, and `minScore` at the recogniser (`VoxrCommandRecogniser.cs:30`) is the real gate — at default settings a user sees nothing new. Raising the parse-loop floor instead would be a new tuning decision, against the G0 "no new tuning surface" posture. **Honest residue:** a user who lowers `minScore` below roughly `0.35` will start seeing these low-scoring tail candidates, and the two tests above need re-derived expectations argued as improvements (per §7.3's discipline), not mechanically updated.

### A1.4 What this changes if ratified

- **§5.4 and DR-3** gain the literal case; the "Required LITERALS are deliberately exempt" reasoning at `:1363-1366` is withdrawn as written and replaced by the ruled option.
- **§9 gains a prerequisite**, ahead of item 1, on the #66 precedent: the eager-gate fix is a live bug on `main` and lands before `RequiredLiteralMissPenalty` moves. Whether it runs through the issue lane (as #66 did) or as a design-lane feature is part of this ruling.
- **§7's measurement plan needs a note:** the committed harness corpus cannot exercise item 1 at all — all 16 fixture transcripts are exact phrase matches, so `m = 0` throughout and the A/B delta is identically zero by construction. §7.2's A/B remains correct as a *regression* check but is not evidence about the change; the hand-derivations and the new tests are.
- **§2's blast-radius numbers are corrected:** the fixture manifest holds **16** cases, not 17; and none of the 15 numeric score assertions changes value under §5.1 (every one is a perfect match, a #31 skipped-word case, or a range check), so §9 row 1's "re-derive the 15 affected score assertions" is a verification exercise, not a rewrite.

**Not affected.** §5.1's constant decision itself, §5.2's coverage model, §5.3, DR-1, DR-2, DR-4, DR-5, and backlog items 2 and 3 all stand exactly as locked on 2026-08-12. A1 concerns only the eager gate's completeness condition and the two consequences above.

### A1.5 Rulings (human, in conversation, 2026-08-13)

1. **A1.2 → Option B, the unmatched-required-tail guard.** No eager verdict above `None` when any required pattern element sits after the last element that actually matched. #66's slot rule stays alongside it, unchanged — it is strictly stronger for slots, since a medial missed slot means an absent argument and must block wherever it occurs. Options A and C stay on record above as considered-and-rejected.
2. **A1.3 → accept and record.** The zero-crossing consequence is accepted: `minScore` at the recogniser is the real gate, the `≤ 0f` floor was an artifact of negative penalties, and no new tuning surface is introduced. The residue is recorded as stated — sub-threshold tail candidates become visible to anyone running `minScore` below roughly `0.35`, and `Tests~/Runtime/VoxrCommandParserTests.cs:648` and `:1600` get re-derived expectations argued as improvements, not mechanically updated.
3. **A1.4 → issue lane, on the #66 precedent.** The eager-gate fix is a live bug on `main` (`N≥4` today), so it is filed as a GitHub issue and fixed through the issue lane — branch, PR, `review-pr` at the slim profile, human merge — rather than becoming a design-lane feature. It becomes a **second cross-lane prerequisite in §9, ahead of item 1**, exactly as #66 is.

**DR-6 (from ruling 1) — the eager gate's completeness condition is stated over unmatched required *elements at the tail*, not over element class.** A verdict above `None` requires both: no missed required slot anywhere in the pattern (DR-3, shipped as #66), and no required element after the last matched element (this amendment). The second is what makes the end-of-buffer condition honest — without it, that condition is satisfied vacuously by exactly the utterances that are still in progress.

## 0B. Amendment A2 — the zero-crossing's true reach (RATIFIED 2026-08-13)

> **RATIFIED — re-locked at G1 (human sign-off in conversation, 2026-08-13).** The fork is ruled in §A2.5, and **DR-7** follows from ruling 1. This section is immutable from here. Raised by the review cycle on `feat-fidelity-miss-cost`. Eleven finder angles and five adversarial verifiers; every claim below was reproduced by compiling the **real parser sources at both revisions** (`5957fdf` vs the branch head) and replaying utterances through them. The branch is halted with its code unchanged pending this ruling.

### A2.1 What was found

**A1 ruling 2 accepted the zero-crossing on a premise that is false.** The ruling reads: *"`minScore` at the recogniser is the real gate, the `≤ 0f` floor was an artifact of negative penalties… at default settings a user sees nothing new,"* with the residue scoped to *"anyone running `minScore` below roughly `0.35`."*

The mechanism that breaks it is one line of selection order. `IsBetterCandidate` filters on `Score <= 0f` and **then ranks earliest start ABOVE score**. So a candidate lifted over that floor does not merely appear as a late, low-scoring tail result — **it can win round 1**, consume tokens, and change what happens to the genuine command afterwards. Three distinct harms follow, all at the **default** `minScore = 0.6`.

**M1 — a false positive is created, by defeating issue #31's guard.** The junk winner consumes the leading tokens, so `searchStart = bestEndIdx` and round 2's `bestStartIdx > searchStart` is false — the skipped-word charge that #31 exists to apply never fires. On **this package's own demo grammar**:

| utterance | before | after |
|---|---|---|
| `alpha one weapons mode` | `mode_weapons 0.500` — suppressed | `approach_target 0.333` \| **`mode_weapons 1.000` — fires** |
| `alpha one disengage` | `cease_fire 0.333` — suppressed | `approach_target 0.333` \| **`cease_fire 1.000` — fires** |
| `zzz weapons mode` *(control)* | `0.667` | `0.667` — unchanged |

The control is the proof of mechanism: out-of-grammar preamble cannot be consumed, so the charge survives. Replace it with a **slot value** and the guard is gone. The lifted candidate is `approach target {target}` at `(−0.5−0.5+1)/3 = 0` → `(0+0+1)/3 = 0.333`.

**M2 — a true positive is lost, by eviction from a fixed buffer.** `_resultBuf` is sized `Math.Max(commands.Length, 1)` over the **active** command set, and extraction `break`s silently when full. A junk candidate takes a slot a real command needed:

| grammar | utterance | before | after |
|---|---|---|---|
| 2 commands | `hard burn set burn to coast fire` | `[set_burn 0.667]` `[fire 1.000]` | `[set_burn 0.25]` `[set_burn 1.000]` — **`fire` lost** |
| 1 command | `hard burn set burn to coast` | `[set_burn 0.667]` — fires | `[set_burn 0.25]` — **below gate, nothing fires** |

Verified to be *purely* eviction, not score damage: widening the grammar by one unrelated command restores the lost result untouched. Exposure needs a small active set, a ≥3-element pattern with at most one slot, and a stray in-grammar word ahead — narrow, but `SetActiveSets` makes small active sets ordinary. No document in `Planning~/` mentions `_resultBuf` at all.

**M3 — pending entry widens.** The partial-match branch sits *inside* `if (cmd.Score < minScore)` and is gated on `cmd.Score > 0f` — the very floor that moved — so it is not protected by `minScore` at all. It `continue`s, so the confidence gate and cooldown never run, and `EnterPending` **cancels any live pending command**. On the demo grammar, **14 of 135** partial utterances go from 0 results to pending-eligible (`close in`, `fall back`, `set safe range`, `distance target` …, all at `0.20`); pending-eligible utterances rise 25 → 39. Mitigated by two opt-ins that both default off (`AllowPartialMatch`, and `FireAsIs` for the timeout path), and the path itself pre-exists §5.1 — **the change widens the trigger set, it does not open the path.**

### A2.2 What is *not* in question

§5.1's constant decision is sound and its evidence holds. Every numeric expectation was independently re-derived; the `3/5` boundary is bit-exact (`0x3F19999A`) through the real accumulation path; `ScoreFollowUp` is genuinely untouched; selection *keys* are unchanged; `[0,1]` holds; no tuning surface was added; and on 61 single-word ablations of real committed transcripts, **10 newly clear `minScore` and 9 of those recover the correct intent**. A2 concerns only what the zero-crossing reaches — nothing else in this design moves.

### A2.3 The fork

**Option A — accept the true reach, correct only the record.** No code change. A1 ruling 2 stands, its rationale restated honestly, M1–M3 documented as known consequences. **Cost:** ships a false-positive class and a lost-command class that a user experiences as regressions, on exactly the small/narrow-active-set grammars a new adopter starts with.

**Option B — state the admission rule the penalty was accidentally enforcing.** Admit a candidate only when it **matched at least as many required elements as it missed** (`MissedRequired > MatchedRequired` → reject), as a structural condition in `IsBetterCandidate` alongside the existing `Score <= 0f`. Not a knob: no serialized field, no number to configure, so the G0 "no new tuning surface" posture holds. This restores as a *stated rule* what the `-0.5` penalty was providing as an *arithmetic artifact* — which is goal 5's whole point.

> **CORRECTED 2026-08-13 by the PR #72 review (rationale only — ruling 1 stands).** "Restores" is wrong: DR-7 **tightens**. The old filter admitted iff `missed < 2 × matched`; DR-7 admits iff `missed ≤ matched`. The band between — two matched against three missed, say — was admitted on `main` and is refused now, and the table below has no row for it because the prototype measured only the newly-*admitted* direction. Measured after the fact: in that band the old model was not merely lenient but **incoherent** — a fragment surviving there consumes the tokens ahead of a real command and hands it a clean score, so `"hotel two cease fire"` was suppressed at `0.500` on `main` while `"cqb hotel two cease fire"` — the same command with *more* debris — fired at `1.000`. DR-7 is monotone across that ladder. The tightening is therefore endorsed on its merits, but it is a stronger rule than the one ruled on, and the record should say so.

*Prototyped and measured* (patched into the real parser, replayed at all three revisions):

| | before | shipped §5.1 | §5.1 + Option B |
|---|---|---|---|
| all six §5.1 arithmetic cases | — | as designed | **identical** |
| F9 / F10 eager verdicts | `None` / `None` | `Commit` / `None` | **`Commit` / `None`** |
| M1 `alpha one weapons mode` | `0.500` suppressed | **`1.000` fires** | **`0.500` suppressed** |
| M3 `close in` / `fall back` | 0 results | pending-eligible | **0 results** |
| ablation rescues | — | 10 (9 correct) | **10 (9 correct)** |
| A1 ruling 2 residue | — | 4 | **0** |
| F7 fixture delta | — | 0 | **0** |

Option B fixes all three harms, preserves every intended §5.1 behaviour exactly, and **dissolves A1 ruling 2 rather than requiring it** — the residue goes to zero, so `NumberSequence_RespectsMaxWords` and `HazardSplitAcrossTwoIntents_Warns` revert to their original assertions and need no re-derivation. **Unvalidated risk:** only the parser-level rig has been run; the full Unity suite has not, and some existing test may rely on a sub-threshold candidate being emitted. It also needs a deliberate reading against backlog item 2, which moves coverage into the score the same selection consults.

**Option C — protect the two damaged resources only.** Keep every candidate emitted (A1 ruling 2's "the parser reports, the gate decides" intent) but forbid a sub-`minScore` candidate from evicting an above-gate one from `_resultBuf` and from resetting the #31 baseline. Surgical and preserves the reporting contract, but it is two special-case rules bolted onto selection rather than one statement of the model — the shape goal 5 exists to avoid — and it leaves M3 untouched.

**Recommendation: Option B**, with A as the fallback if the suite run turns up collateral Option B cannot absorb cheaply. B is the only option that makes the model *say* what it is doing; A ships known regressions; C trades a principle for two patches.

### A2.4 What this changes if ratified

- **A1 ruling 2 is superseded or restated**, depending on the option. Under B it becomes moot — there is no residue left to accept.
- **§5.1, DR-1…DR-6, A1 rulings 1 and 3, and the §9 backlog are untouched.** So are backlog items 2 and 3.
- Under B, **selection gains one admission condition** and §5.3's key list gains a sentence — the keys themselves do not change.
- `feat-fidelity-miss-cost` resumes from its current two commits; under B it gains the rule, a test per harm, and reverts the two flipped tests.

### A2.5 Rulings (human, in conversation, 2026-08-13)

1. **A2.3 → Option B, the admission rule.** A candidate is admitted to selection only when it **matched at least as many required elements as it missed**. Stated structurally in `IsBetterCandidate` alongside the existing `Score <= 0f`, counting required elements only (optional literals and optional slots are excluded from both sides).

   > **CORRECTED 2026-08-13 by the PR #72 review (rationale only — the rule stands as ruled).** The clause originally read "…excluded from both sides, **exactly as they are excluded from the denominator**". That analogy is false, and §2's own table says so: an *omitted* optional leaves both sides of the ratio, but a **matched** one credits both (`+0.5` literal, `+1.0` slot). DR-7's ledger is therefore a second, deliberately different one, and the consequence is that it can refuse a candidate whose *score* would have passed — measured at more than `2.0` of matched-optional credit, which no pattern in this package carries (the maximum shipped is `1.5`). Across the committed corpus and every single-word ablation, zero utterances lose a result. Counting matched optionals as evidence was tested and produces byte-identical output, so the choice is free either way; it is kept as ruled and its true reason recorded: an optional the author marked skippable says nothing about whether the required elements were spoken. No serialized field, no configurable number — the G0 "no new tuning surface" posture holds. Options A and C stay on record above as considered-and-rejected.

2. **A1 ruling 2 is superseded, not merely restated.** Under DR-7 the zero-crossing has no surviving residue to accept: the four sub-threshold candidates it produced fall back below admission, `Tests~/Runtime/VoxrCommandParserTests.cs`'s `NumberSequence_RespectsMaxWords` and `HazardSplitAcrossTwoIntents_Warns` return to one result each, and the "`minScore` below roughly `0.35`" residue note is withdrawn as moot. A1 ruling 1 (DR-6) and ruling 3 (issue-lane routing) are untouched.

3. **The flush-path dropped discriminator remains unruled and goes to G2**, not to the issue lane, on the three disanalogies in the feature's architecture §8 — but it must be presented with the premise that it is **already live on `main` at N≥4** and that §5.1 widens it to N≥3.

**DR-7 (from ruling 1) — admission to selection requires more evidence for a candidate than against it.** A candidate whose missed required elements outnumber its matched required elements is not a candidate. This is what `RequiredLiteralMissPenalty = -0.5f` was enforcing as an arithmetic side effect, and what zeroing it removed without replacement; DR-7 states it as a rule instead, which is goal 5's requirement that the model be derivable from stated principles rather than from the residue of penalty values. It composes with, and does not replace, the `Score <= 0f` floor — a required-slot miss still costs `-1.0` and still sinks candidates on its own.

**Scope note for backlog item 2.** §5.2 moves coverage into the score that selection consults. DR-7 is stated over *element counts*, not over the score, so the two are orthogonal by construction and item 2 does not need to re-derive it. That independence is deliberate and should survive item 2's implementation.

## 0C. Amendment A3 — the orphan test rewards matching less (RATIFIED 2026-08-14)

Raised during backlog item 2's Phase 7 A/B, which is the measurement §7 calls this feature "the one most in need of". Unlike A1 and A2, this one was found by **measurement**, not by re-reading: the rule as locked was implemented faithfully, the suite went green (EditMode 116/116, PlayMode 395/395), and the defect appears only in a 699-utterance corpus built to contain the phenomenon.

### A3.1 What was found

§5.2 says a trailing token is orphaned if **no active pattern could begin a match at it**. The matcher, however, can begin a pattern at *any* token by missing its leading elements. So the predicate and the matcher disagree about where a pattern starts, and the gap has two user-visible consequences.

**The severe one — the wrong command fires.** Two patterns share a prefix and differ only in their final element, and that element is itself a pattern start:

| utterance | on `main` | under §5.2 as locked |
|---|---|---|
| `switch to weapons target hotel` | `mode_weapons:1.000` | **`mode_navigation:0.667`** |
| `switch to navigation target hotel` | `mode_navigation:1.000` | **`mode_weapons:0.667`** |

`[switch, to, navigation]` **misses** its final element, so its consumed span stops at token 2 — and token 2 is "weapons", which begins `["weapons","mode"]`. Its orphan run therefore terminates at once and it pays nothing: `2/3` = 0.667. `[switch, to, weapons]` **matches** that element, so its origin moves past the very token that would have terminated its own run, and it pays for everything after: `3/(3+n)`. At n ≥ 2, `0.600 < 0.667` and the wrong pattern wins. **A candidate is rewarded for matching less.**

Threshold measured exactly: n = 0 or 1 is safe, n ≥ 2 flips. `switch to X` is not an exotic shape — minimal pairs differing in a final element are ordinary command-grammar authoring, and this one is in the shipped demo grammar.

**The milder one — a real command is dropped.** `"cease fire target hotel one"`, a two-command utterance whose second command lost its "approach": `cease_fire` falls from `1.000` to `0.400` and stops firing, because "target" begins no pattern *as a first element* even though `approach target {target}` does in fact match there. Control: say the second command in full and both fire at `1.000`. The charge lands on tokens a later extraction round then explains.

### A3.2 What is *not* in question

The feature works. Symptom 2 inverts as designed (`0.667` beats `0.333`, slot filled, above `minScore`), fork F5 is still refused, sequential extraction survives (`cease fire launch missiles target hotel one` → `1.00` + `1.00`), the committed 16 baselines are unchanged, and the eager verdict class is provably one-way (`None → Commit` only, never withdrawn, never a different command). DR-1…DR-7 are untouched. Only the orphan test's boundary is in question.

### A3.3 The fork

**Option A — literal DR-6 alignment.** Refuse candidates with an unmatched required tail in `IsBetterCandidate`, as the eager gate already does at `:1569`. **Rejected on measurement:** it roughly doubles the newly-silent count (35 vs 18) and it starves the `allowPartialMatch`/pending path, which is fed precisely by the candidates it would discard — the hazard the parser's own #73 comment already warns about.

**Option B — deny the mis-predicted token.** A candidate whose own next required element failed at a position may not claim *"some other pattern could begin here"* for the very token it mis-predicted. Charge that token (skipping `[unk]`), then continue the run normally from the one after.

**Option C — accept and document.** Ship as locked; record both consequences as known limitations.

### A3.4 Measured comparison, 699 utterances vs `main`

| | §5.2 as locked | **Option B** | Option A |
|---|---|---|---|
| **wrong command fires** | **2** | **0** | **0** |
| newly-silent | 18 | **17** | 35 |
| intent-change | 10 | 11 | 11 |
| count-change | 1 | 1 | 1 |
| newly-firing / slots-gained / slots-lost | 0/0/0 | 0/0/0 | 0/0/0 |
| score-only | 116 | 116 | 129 |

Under Option B every acceptance case is unchanged, re-measured rather than assumed: F7 across intents `0.667`, F7 within one intent `#1 0.667`, F7 via omitted optional `#1 0.75`, F8 sequential `1.00` + `1.00`, F18 at weight 0 `1.000`, the single-pattern consequence `0.333`, DR-7 rejection, and both restored span tie-breaks.

### A3.5 What this changes if ratified

§5.2 gains one sentence: **counting begins at the candidate's consumed end, and where the candidate's own next required element failed there, that token is charged rather than tested against the start predicate.** The worked table is unaffected — none of its three rows has an unmatched required tail. Nothing else in the model moves; no new parameter; no selection key changes.

**Residual, accepted rather than hidden:** 11 intent-changes and 1 count-change remain, all one shape — a two-command utterance whose *second* command lost its leading word, where the first command is demoted below the gate. It loses a command; it never fires a wrong one. Closing it needs a per-position admissibility probe; a crude widening of the predicate collapses into the degenerate case where a permissive slot makes nearly every token a start and the feature disables itself grammar-wide. Deferred to `KNOWN_LIMITATIONS.md` and a follow-up issue.

### A3.6 Rulings (human, in conversation, 2026-08-14)

1. **Option B adopted**, amending §5.2's orphan test. Ruled after seeing the measured comparison in A3.4, and against Option A — which was *my* proposal and which measurement showed to be the worse of the two.
2. **The residual dropped-command class is accepted as a documented limitation** → `KNOWN_LIMITATIONS.md` plus a follow-up issue. The admissibility-probe question is not opened now.
3. Apply on the feature branch, run the Unity suite, and **add a test pinning the `switch to X` flip** so the case that forced this amendment cannot regress unnoticed.

*No design branch: `Planning~/` is gitignored and untracked, so this doc has no git history to branch — recorded in place, on the A1/A2 precedent.*

## 0D. Amendment A4 — the admissibility probe (RECORDED 2026-08-15, NOT YET RATIFIED)

> **Status: recorded, not locked.** Built under the **issue lane** (issue #82, gate-exempt) on
> branch `issue/82-orphan-start-admissibility`, PR open for the human's review. §5.2 and A3.5
> below still carry the pre-#82 wording; this section is what the code now does. **The re-lock
> is the human's call** — nothing here claims G1.

A3.5 deferred the residual dropped-command class and named what would close it: *"a per-position
admissibility probe; a crude widening of the predicate collapses into the degenerate case."* A3.6.2
ruled the question not opened at that time and bound it to a follow-up issue. That issue is #82.

**What changed.** §5.2's orphan test — "no active pattern could **begin** a match at it" — asked
whether a pattern's *first matchable element* matches the token. Selection asks nothing of the kind:
it tries every pattern at every non-`[unk]` index, so a pattern whose leading elements the decoder
dropped begins wherever its surviving elements do. The test now asks the matcher: a token is a start
when some active pattern, **matched from that token**, ends with `MissedRequired < MatchedRequired`
and a positive raw score.

**Why this is not the crude widening A3.5 refused, and why the threshold is DR-7 + 1.** The first
implementation used DR-7's own rule (`MissedRequired <= MatchedRequired`) and **that was wrong** —
caught by the PR #88 review, confirmed against `main` by replay. DR-7 asks "is this a candidate at
all?", a question answered for a candidate that may lose its round and never fire. Terminating
another command's orphan run moves score off a command that *is* firing, so it needs strictly more
evidence for than against. At `<=`, `["decelerate"]` + `["decelerate","?by","{burn_level}"]` — the
pair the docs prescribe as the #42 remedy — on "decelerate hard burn please" fired the **bare**
command at `1.00` with the burn level discarded, against `0.67` on `main`: #42 reverted on the
recommended grammar. The same rule also broke the documented "anchor it behind a literal" workaround
for permissive slot-initial patterns, since one literal ahead of the slot gives exactly 1 missed
against 1 matched. At `<` both close, and the corpus result gets *tighter* rather than looser.

**F11.** Argued against the row as narrowed on 2026-08-14 (requirements §4.3), which forbids a
candidate's orphan count depending on *another candidate's verdict*. The probe does not: it is
consulted **after** `CanStartPattern` and never overrules it, so no pattern's claim is ever
withdrawn, and its answer is a function of (grammar, tokens, index) alone — no `searchStart`, no
selection state, no other candidate. Only claims are added.

**Measured, 699 utterances vs `main` at the recogniser's gate:**

| | vs main |
|---|---|
| intent-change | 11 |
| count-change | 1 |
| **newly-firing / newly-silent / slots-lost / slots-gained** | **0 / 0 / 0 / 0** |
| score-only | 4 |

The 11 + 1 are A3.5's residue closing: in every case the first command returns to `1.000` and the
second was already firing. Also measured: **0 of 699 eager verdicts move** (so §5.4's one-way
verdict property is untouched); exactly **1 of ~50 pinned documentation numbers** moves (worked
example D, which was this defect written up); the §9.9 start-index sweep goes **29/28/1 → 28/27/1**,
so the neighbouring blocked-match limitation keeps both its root cause and its one measured case;
`GenerateGrammarJson` is byte-identical, so the Tier C baseline is unaffected. Cost **~12.3 → ~16.1
µs per `Parse`** at **no added allocation** (492.8 bytes/utterance both sides), flush path.

**The eager gate is measured, not argued.** `--verdicts` samples only the finished line, which is
the one buffer state `TryEagerCommit` never runs on. `ab-rig/Program.cs` gained
`--prefix-verdicts`, which replays every growing prefix: **0 of 4,014 prefix buffers change
verdict**. That is the evidence §5.4's one-way property needed and previously did not have.

**Two costs the probe does not pay**, both closed after the review: it asks the number matcher to
skip building the joined slot value it discards (so a `NumberSequence` grammar adds no allocation,
the thing `CanStartPattern` avoids by construction), and it short-circuits entirely at
`coverageWeight = 0`, where no orphan count can reach a score. The second makes `_orphanRun`
weight-dependent — invisible to scoring, visible to a test reading `OrphanedAfter`, and pinned as
such.

**What did NOT change:** selection keys, DR-7 itself, `[unk]` handling, the A3 exception and
`_forcedOrphanRun`, `coverageWeight` and its clamping, and `CanStartPattern` (still the cheap first
half of the answer, and still separately tested).

**If ratified**, §5.2's orphan sentence becomes: *a trailing token is orphaned if no active pattern
matched from it matches strictly more of its required elements than it misses*, and A3.5's
"residual, accepted rather than hidden" paragraph is struck.

**Method note worth keeping.** The 699-utterance corpus did **not** catch the `<=` defect: the demo
grammar contains no bare-pattern/extension pair of the shape that exposes it, so the corpus returned
zero regressions on a build that reverted #42. What caught it was an adversarial reviewer reasoning
about the *rule* rather than replaying the *corpus*. Treat a clean A/B on this corpus as evidence
about the demo grammar, not about the grammar space — the #42 shapes live in `DocCheck`'s synthetic
grammars, and even there only the three-element form was pinned.

## 0E. Amendment A5 — resolver-fillable slots are not charged (RATIFIED 2026-10-02)

> **RATIFIED — re-locked at G1 (human sign-off in conversation, 2026-10-02).** Raised on `design-resolver-slot-scoring` (issue #161) and settled with the human in conversation on 2026-10-02; the rulings are in §A5.6. The design is `Planning~/design-docs/resolver-slot-scoring.md`, its forks are ADRs under `Planning~/design-docs/resolver-slot-scoring/decisions/`, and the measurement is `Planning~/design-docs/resolver-slot-scoring/lab-2026-10-02.md`. The lab's arms are named A0, A4 and A4L; lab arm A4 is unrelated to Amendment A4 (§0D).

### A5.1 What was found

**A slot resolver never sees the short orders it was built for.** Resolvers (#148) are registered by slot name only, globally (`RegisterSlotResolver(slotName, …)`), at runtime with no parser rebuild, and are consulted only for a candidate at or above `minScore`. §5.1 keeps a missed required slot at `-1` raw and `+1` den, so a pattern whose only gap is the slot the game would fill is sunk by the score gate before the completeness gate is ever asked. #148's own example, `"launch missiles"` against `launch missiles target {track}`, scores **0.250** (`Planning~/features/slot-resolver/requirements.md` §8 E-2). E-2's measurement on the game's `Set_Combat` found a resolver reachable for **18/40** pattern×required-slot occurrences dropping the slot alone and **5/40** in the natural spoken form (slot and its orphaned literal dropped), with **0/4** weapon-release occurrences reachable in that form.

The lab reproduced the baseline exactly on the staged parser (A0: (a) 18/40, (b) 5/40, `{track}` 7/15 and 2/15, `launch_missile` (b) 0/4, 0 barred; `"launch missiles"` 0.250000 on the demo grammar), then measured the exemption shapes in §A5.3 against it. Ellipsis — game-state resolution of an omitted required slot, ruling 9 — and the score gate pull against each other, and #148's requirements record the scoring question as a change to this doc, not to the feature.

### A5.2 What is *not* in question

The leading-required-miss bar (`Planning~/design-docs/leading-miss-bar.md`, ruling 3) is absolute and unchanged: an exempt anchor is still refused first. The eager gate is unchanged: an exempt miss still counts as a missed required slot for DR-3, so eager never commits it. `minScore` stays one global, flat value (DR-2), 0.6 provisional, with no per-command threshold. The coverage term — §5.2 with A3's charged-token rule — is unchanged: the exemption touches only the slot's own term.

### A5.3 The fork

How should the scoring model charge a required slot that a registered resolver can fill?

1. **Ship as built** — the option taken for #148 on 2026-09-16.
2. **Credit a resolver-filled slot as matched** — `"launch missiles"` → 0.75.
3. **Waive the miss penalty only** — `"launch missiles"` → 0.50, still under the gate.
4. **Exempt a resolver-fillable missed slot from the score** — 0 raw, 0 den: **(4a)** in the parser, everywhere a pattern is scored; **(4b)** only in a recogniser re-score for the `minScore` check.

Each option, and why the others lost, is in `adr-0001-exempt-fillable-slot-in-parser.md`. Option 4 opened four further forks, each with its own record: what makes a slot fillable (`adr-0002-fillable-means-registered.md`); whether the exemption also leaves the DR-7 ledger and the start probe, lab arm A4, or changes score arithmetic only, lab arm A4L (`adr-0003-exemption-changes-arithmetic-only.md`); whether selection gains a fewer-exempted-slots key (`adr-0004-fewer-exempted-slots-tie-break.md`); and whether thin firing gets a guard (`adr-0005-thin-firing-accepted.md`).

### A5.4 Measured comparison, A0 vs A4L

`Set_Combat` on the one buildable scene grammar (`Prototype_Mauevering`), F = all 9 required slot names, `minScore` 0.6; the corpus rows use the demo grammar with F = `weapon,target,range,heading`. Figures from the recon record.

| | A0 | A4L |
|---|---|---|
| reachable, slot dropped alone (shape (a)) | 18/40 | **35/40** |
| reachable, slot and its literal dropped (shape (b)) | 5/40 | **28/40** (37/40 per command) |
| `{track}` reachable, (a) / (b) | 7/15 / 2/15 | 15/15 / **12/15** |
| weapon release reachable, (b) | 0/4 | **4/4** |
| barred | 0 | 0 |
| thin utterances firing with a missing slot (of 124) | 0 | **16** (2 one-word, 14 two-word) |
| fully spoken utterances whose fire changes (of 81) | — | **0** |
| 699-row corpus, fired differences | — | 66 (37 new single fires, 29 extra fires; 0 A0 fires removed or altered) |
| eager Commit, 378 Set_Combat prefixes | 40 | 40 |
| corpus prefix verdicts, 4014 prefixes | — | byte-identical |
| start probe, 4959 positions | — | 0 differ |
| rounds decided by the new key | — | 14, all same-intent siblings |
| DocCheck | 66/66 | 64/66 (the two direct score pins) |

Lab arm A4 — the exemption also leaving the DR-7 ledger and the start probe — reached the same 35/40 and 28/40 but let stutter fragments win round 1 on the start key (`launch launch all missiles target hotel one` → a 0.333 fragment) and failed DocCheck 62/66; that is why A4L was ruled (adr-0003).

### A5.5 What this changes if ratified

§5.1 gains the exemption (DR-8) and §5.3's key list gains one key (DR-9). The model rules, in full:

- **M1.** A missed required slot whose name has a registered resolver contributes 0 to raw and 0 to den. A spoken fillable slot scores as today (+1/+1). Every other missed required slot keeps −1/+1.
- **M2.** The exempt miss still counts as missed for DR-7 admission and the admissibility/start probe; still sets the leading-miss latch (the bar refuses an exempt anchor first, ruling 3); still counts as a missed required slot for the eager gate (DR-3: eager never commits it; elided commands wait for the flush). The coverage term and A3's charged-token rule are unchanged — the exemption touches only the slot's own term (lab: `drive cut drive` 1/3).
- **M3.** Selection keys become: earliest start → score → fewer exempted slots → consumed span → literal count → registration order.
- **M4.** Registration is read at parse time; the score is fixed then. Resolution (Step 3b) never changes it; a declining resolver leaves the command incomplete → pending (`allowPartialMatch`) or rejected, as today — pending already admits any score > 0, so no new exposure there.
- **M5.** The rule is uniform over every place a pattern is scored (flush, eager, pending follow-up re-score, Editor runner-up comparison, the batch test runner given the registered set) — how is the architecture doc's.
- **M6.** Unchanged: `minScore` (one global, 0.6 provisional), the bar, the sibling-tie reachability warning (construction-time, discriminator is a literal), the confidence gate.

**DR-8 (M1 + M2 + M4) — a resolver-fillable missed required slot is not charged, and is still missed.** A missed required slot whose name has a registered resolver contributes 0 to raw and 0 to den; every other missed required slot keeps −1/+1. The exempt miss still counts as missed for DR-7 admission, the admissibility/start probe, the leading-miss latch and the eager gate, and the coverage term is untouched. Registration is read at parse time and the score is fixed then; resolution never changes it.

**DR-9 (M3) — fewer exempted slots ranks better, immediately after score.** Selection orders earliest start → score → fewer exempted slots → consumed span → literal count → registration order.

**Not affected.** DR-1…DR-7, §5.2's coverage model and A3's rule, `minScore` (DR-2), the bar, and the confidence gate. Two DocCheck score pins move — §7 A start 2 0.167 → 0.400 and `fire at` 0.333 → 1.000 — and the product docs follow after G2.

### A5.6 Rulings (human, in conversation, 2026-10-01 and 2026-10-02)

1. **(G0, 2026-10-01) Design track.** The work runs on `design-resolver-slot-scoring`.
2. **(G0, 2026-10-01) Proceed from the grammar measurement.** Real speakers' elision shapes are recorded as unmeasured.
3. **(Settle, 2026-10-02) Measure in a lab before G1.** Done: `Planning~/design-docs/resolver-slot-scoring/lab-2026-10-02.md`.
4. **A5.3 → option 4a**, exempt from the score, in the parser (adr-0001).
5. **"Fillable" means a resolver is registered for that slot name**, not an author mark on the pattern; the configuration-dependent score, the global slot names and tool disagreement without the registry are accepted costs (adr-0002).
6. **The exemption changes score arithmetic only — lab arm A4L**, ruled after the lab, over arm A4 (adr-0003).
7. **Tie-break: fewer exempted slots ranks better, immediately after score** (adr-0004).
8. **Thin firing is accepted and documented, with no guard**; its residues — 16 thin fires under A4L and 2 corpus rows firing two commands — are accepted, and the ruling is reopenable on R5 human-corpus evidence (adr-0005).

## 1. Problem

Two symptoms, which #65 correctly identifies as one underlying cause.

1. **Pattern length determines drop fragility.** A dropped function word pushes a 3-element pattern to `0.50` — under the default `minScore` of `0.60`, so the command does not fire — while the *identical* single-word drop on a 7-element pattern lands at `0.79` and fires cleanly. Observed in-headset during the #42 cycle ("time to target" → "time target").
2. **Coverage never enters sibling comparison.** `decelerate` scores `1.0` while `decelerate by {burn_level}`, charged for the dropped "by", scores `0.50`. The bare pattern wins and the "hard burn" the speaker actually said is discarded with nothing in the log to signal it.

Neither is a new discovery: `scoring.md:49-61` already documents symptom 1 under the heading *"Short patterns are disproportionately fragile"*, and `scoring.md:255-287` documents symptom 2 as worked example B. The model is behaving exactly as written down. **What is missing is not a bug fix but a decision that the written-down model is wrong.**

## 2. Verified current state

Recon on `main` (d081e11), 2026-08-12. Line references are `Runtime/Commands/VoxrCommandParser.cs` unless stated.

**The score formula** (`TryMatchScored`, `:898-1003`):

```
score = rawScore / denominator        (0 when denominator is 0)
```

| Element | Outcome | Raw | Denominator | Site |
|---|---|---|---|---|
| Required literal | matched | +1.0 | +1.0 | `:974-982` |
| Required literal | **missed** | **−0.5** | +1.0 | `:974`, `:985` |
| Required slot | matched | +1.0 | +1.0 | `:949-950` |
| Required slot | **missed** | **−1.0** | +1.0 | `:954-955` |
| Optional literal | matched | +0.5 | +0.5 | `:964-965` |
| Optional literal | omitted | 0 | 0 | `:970` |
| Optional slot | matched | +1.0 | +1.0 | `:949-950` |
| Optional slot | omitted | 0 | 0 | `:957` |

Constants at `:32-35`. Normalisation at `:991`. The denominator is dynamic, so a perfect match always normalises to exactly `1.0` — **a hard ceiling no candidate can exceed.**

**A miss is charged twice** (`scoring.md:44`): it withholds the credit *and* subtracts a penalty, while still occupying the denominator. A single dropped literal therefore costs `1.5/N` of the ceiling. That single fact is symptom 1, in full:

| Pattern | One literal dropped | Score |
|---|---|---|
| `decelerate` (1 element, nothing dropped) | `1 / 1` | **1.00** |
| `time to target` (3) | `(1 − 0.5 + 1) / 3` | **0.50** — rejected |
| `decelerate by {burn_level}` (3) | `(1 − 0.5 + 1) / 3` | **0.50** — rejected |
| `launch {weapon} target {target} on my mark` (7) | `(6 − 0.5) / 7` | **0.79** — accepted |

**Selection** (`IsBetterCandidate`, `:865-885`) orders: earliest start → highest score → longer consumed span → most matched literals → registration order. Coverage appears nowhere, and nothing normalised to `1.0` can be beaten on the score key. This is why #58 concluded no penalty tuning reorders the #42 pair — correctly, *given the rest of the model held fixed*.

**Leading coverage already exists, applied after selection** (`:617-634`). The skipped-word penalty (#31) charges in-grammar tokens the sliding start walked past:

```
finalScore = rawScore / (denominator + skippedWords × skippedWordPenalty)
```

Four load-bearing rules (`scoring.md:72-77`): applied **after** selection so it filters via `minScore` without reordering; `[unk]` is never charged; counting restarts at each extraction round; it is proportional.

**Trailing coverage does not exist.** In-grammar tokens left unconsumed *after* a match cost nothing. `:628` guards on `bestStartIdx > searchStart` and counts only to the match start. **This asymmetry is symptom 2**: bare `decelerate` keeps a clean `1.0` while "hard burn" sits unexplained on the floor.

**The gates** (`scoring.md:127-168`): `minScore` (default `0.6`, `VoxrCommandRecogniser.cs:30`) against the final score; `minConfidence` (default `0.4`) against minimum per-word VOSK confidence — an independent axis this design does not touch.

**The eager-flush gate** (`TryEagerCommit`, `:1287-1360`). Reuses the same selection order, then requires: score ≥ `minScore` computed *without* the skipped-word penalty (`:1337`); the match starts at the first **recognised** token (`:1352`); the match reaches the **end** of the buffer (`scoring.md:213`); confidence ≥ `minConfidence` or `-1`. Condition 1 is sound only because of condition 2.

**Public tuning surface.** `skippedWordPenalty` is **not** internal — it is a `[SerializeField]` on `VoxrCommandRecogniser` (`VoxrCommandRecogniser.cs:39`), documented as user-facing with tuning guidance (`command-recognition.md:145-162`, `scoring.md:64-86`). Any re-derivation must rule on its fate; it is shipped public API on a released package.

**Blast radius, measured — and narrower than #65 assumes.** All **15** numeric score assertions live in a **single** file, `Tests~/Runtime/VoxrCommandParserTests.cs`. Six other test files mention `Score` but pin no value: they reference `minScore` thresholds, a CSV header, or use it in a test name. The one adjacent risk, `VoxrCommandRecogniserDiagnosticTests.ScoreRejection_ReasonFormat:82`, asserts only that the reject reason *contains* "score"/"minScore" and needs "launch missiles" to stay rejected — it scores `0.125` today and `0.25` under §5.1, so it survives.

`Tests~/Fixtures/audio/manifest.json` carries **17** cases with `expectedIntent` (16 also with `expectedSlots`). These are behavioural, so they move only if an *outcome* changes — which is the point of the exercise, not collateral.

**The harness baseline does not move.** `NativeBridge~/harness/expectations.json` pins raw decoder transcripts under a pinned grammar — bridge-level, upstream of the scorer. Every change proposed here is scorer-only and leaves the grammar JSON untouched, so no `--write-baseline` re-baselining is required.

## 3. Goals and non-goals

**Goals**

1. A single dropped required *literal* must not, on its own, sink an otherwise fully-matched pattern at the default `minScore`.
2. A candidate that explains more of the utterance must be able to beat one that explains less, including beating a perfect `1.0` bare sibling.
3. Leading and trailing coverage are one concept with one rule and one weight, not two mechanisms with different application points.
4. The eager-flush gate must never commit a command whose required slots are unfilled.
5. The model after this design is derivable from stated principles, not from the accreted history of #31/#41/#42/#58.

**Non-goals**

- **No change to `minConfidence` or the confidence axis.** Orthogonal.
- **No new user-facing tuning knob**, per the G0 scope. (`skippedWordPenalty` is pre-existing public API — its fate is DR-4, not a new knob.)
- **No opt-in flag and no preservation of current scores.** Existing grammars re-score on upgrade; that is the accepted cost.
- **No change to grammar generation** (`GenerateGrammarJson`), so decoder output and the harness baseline are untouched.
- **No change to the authoring guidance or the `?by` remedy from #58.** They remain valid; this design removes the need to rely on them, it does not retract them.
- No change to `allowPartialMatch` / pending-command routing beyond what DR-3 requires.

## 4. Diagnosis

The score conflates two different questions into one number normalised to a `1.0` ceiling:

- **Fidelity** — how well does this candidate satisfy *the pattern*?
- **Coverage** — how much of *the utterance* does this candidate explain?

Selection compares candidates on fidelity alone (`:880-881`), because the only coverage term in the model is applied *after* selection, deliberately (`:625`). Fidelity has a hard ceiling of `1.0` that any bare pattern trivially attains. So:

- **Symptom 1 is a fidelity defect**: the miss cost is a fixed numerator penalty against a variable denominator, making cost `1.5/N`.
- **Symptom 2 is a coverage defect**: the coverage half of the model was only ever built for the leading side, and was deliberately built not to reorder.

This is why the two interact, and why #58's analysis — that a new ordering key above `Score` "flips selection but leaves the winner at 0.5, under the `minScore` gate" — is true *only while the fidelity defect stands*. **Fix fidelity first and that objection dissolves**, because the reordered winner is no longer at `0.50`. The two symptoms are not independent problems that happen to collide; they are one problem whose fixes must land in a specific order.

## 5. The re-derived model

### 5.1 Fidelity — remove the double-charge (symptom 1)

**Proposal: a missed required literal contributes `0` to the numerator and `+1.0` to the denominator.** `RequiredLiteralMissPenalty` goes from `-0.5` to `0`. One constant.

A miss then costs `1/N` instead of `1.5/N` — still length-proportional, but halved:

| Pattern | One literal dropped | Now | Was |
|---|---|---|---|
| `cease fire` (2) | `1 / 2` | **0.50** — still rejected | 0.25 |
| `time to target` (3) | `2 / 3` | **0.67** — fires | 0.50 |
| `decelerate by {burn_level}` (3) | `2 / 3` | **0.67** — fires | 0.50 |
| `launch … on my mark` (7) | `6 / 7` | **0.86** — fires | 0.79 |

This satisfies goal 1 for every pattern of **3 or more elements**. Two-element patterns still reject a single drop at `0.50`, which is *correct* and deliberately preserved: "cease fire" heard as "fire" is genuinely ambiguous with the `fire` command, and should not fire on half its evidence.

**`RequiredSlotMissPenalty` stays at `-1.0`.** A missing required *slot* means the command's argument is absent — materially different from a missing function word, and already routed to the pending/partial path. Leaving it untouched keeps `allowPartialMatch` behaviour intact.

> **AMENDED by Amendment A5 (§0E, ratified at G1 2026-10-02):** except for a slot with a registered resolver — see DR-8.

**Accepted consequence — ratified 2026-08-12.** Two dropped literals on a 5-element pattern now score `3/5 = 0.60` and pass the gate (was `0.40`). With all slots extracted, firing is the right outcome. **The gate stays `≥`**; it is not tightened to `>` to exclude the boundary case.

### 5.2 Coverage — one rule, both sides (symptom 2)

**Proposal: charge a candidate for in-grammar tokens it leaves *orphaned* on either side, during selection.**

A trailing token is **orphaned** if it follows the candidate's consumed span and **no active pattern could begin a match at it**. Counting stops at the first token that could start a pattern.

> **AMENDED by Amendment A3 (ratified at G1, 2026-08-14, §0C).** One exception, at the first position only: **where the candidate's own next required element failed to match, that token is charged rather than tested against the start predicate.** A candidate that just mis-predicted a token may not then claim some *other* pattern could have begun there. Without the exception a pattern that MISSES its final element stops short of a token that terminates its orphan run, while the pattern that MATCHES that element moves past it and pays for everything after — so the wrong, shorter match scores higher and fires. Counting continues normally from the token after the charged one.

```
score = rawScore / (denominator + (skippedBefore + orphanedAfter) × coverageWeight)
```

The orphan test is what makes this safe for sequential extraction, and it is the direct analogue of the existing rule that counting restarts after each extracted command. Worked through:

| Utterance | Candidate | Consumed | Trailing | Orphans | Score |
|---|---|---|---|---|---|
| `decelerate hard burn` | `decelerate` | 1 | `hard burn` — starts no pattern | 2 | `1 / (1+2)` = **0.33** |
| `decelerate hard burn` | `decelerate by {burn_level}` ("by" dropped) | 3 | — | 0 | `2 / 3` = **0.67** |
| `cease fire launch missiles target hotel one` | `cease fire` | 2 | `launch …` — **starts a pattern** | 0 | `2 / 2` = **1.00** |

The #42 pair inverts — `0.67` beats `0.33` — **and the winner clears `minScore` at 0.67.** Goal 2 met, at `coverageWeight = 1.0`, the weight the leading term already ships with. No calibration of a new free parameter is required, which is the main reason to prefer this shape over the additive `(F + w·C)/(1+w)` form #65 sketches: that form needs `w > 0.5` even after §5.1 and puts coverage in direct competition with fidelity at a weight nobody can justify from first principles.

Rules carried over unchanged by symmetry, so leading and trailing are genuinely one rule (goal 3):

- **`[unk]` is never charged**, trailing as well as leading.
- **Counting restarts at each extraction round** on the leading side; on the trailing side the orphan test performs the same job.
- **Proportional**, so it bites short patterns swallowed by longer utterances.

**The count starts at `ConsumedEndIdx`, not `EndIdx`** — the same distinction #41 drew for the span tie-break (`:909-915`). `EndIdx` runs past `[unk]` the pattern never matched, so counting from it would let a candidate shed orphans by absorbing noise. `[unk]` between the two indices is free regardless, so the two rules agree; naming `ConsumedEndIdx` explicitly keeps them from drifting apart.

**This moves coverage above the selection barrier.** Today the leading term is applied post-selection *specifically so it cannot reorder* (`:625`). A trailing term that fixes symptom 2 must reorder — that is its entire purpose. So the barrier goes, and both sides are computed per-candidate inside `TryMatchScored`/selection. That is the single largest behavioural change in this design and the one most in need of measurement (§7).

### 5.3 Selection and gates

Selection keys are otherwise **unchanged**: earliest start → score → consumed span → literal count → registration order. The score key now carries coverage, so the consumed-span key (#41) demotes to what it was always meant to be — a tie-break for genuinely equal candidates — rather than the sole carrier of "explains more". #41's behaviour is preserved, not superseded.

> **AMENDED by Amendment A5 (§0E, ratified at G1 2026-10-02):** a key is added immediately after score — fewer exempted slots ranks better — so the order becomes earliest start → score → fewer exempted slots → consumed span → literal count → registration order; see DR-9.

`minScore` stays at `0.6`, and **does not scale with pattern length** (option (c) in #65, rejected as DR-2).

### 5.4 Eager flush

The eager scan reuses selection, so it inherits coverage automatically, and the invariant *"an eager verdict always names the command the subsequent flush will actually fire"* (`scoring.md:104`) is preserved because both paths now compute the trailing term identically.

**The trailing term cannot destabilise the eager gate**, structurally: a verdict above `None` already requires the match to reach the end of the buffer, so any candidate that gets a verdict has zero trailing orphans by construction. Condition 1's existing soundness argument (score computed without the *leading* penalty, sound because condition 2 requires the match to start at the first recognised token) is untouched.

**The gate silently relies on a filter it does not enforce.** A pattern with an unfilled required slot is *usually* held below `minScore` by the arithmetic alone — emergent, not enforced. It already fails today: a **5-element pattern missing one required slot scores exactly `3/5 = 0.60`** and clears the gate. It then clears the span check too, because the end condition compares `bestEndIdx` (`:1356`) and **a missed slot consumes nothing, so it does not advance the index** — the match "reaches the end of the buffer" without having filled the slot. That is a live latent bug on `main`, independent of everything else in this design.

§5.1 widens the hole, but only narrowly: since it changes the *literal* miss cost and leaves `RequiredSlotMissPenalty` at `-1.0`, a slot-missing candidate only newly crosses `0.6` at **8 or more elements** with a literal drop alongside the slot miss (`(6−1)/8 = 0.625`, was `0.5625`). Smaller patterns are unaffected.

> **CORRECTED by Amendment A1 (ratified at G1, 2026-08-13, §0).** The paragraph above is arithmetically right but asks only about *slot*-missing candidates. The same vacuous end-of-buffer mechanism admits a candidate whose **terminal required literal** was never spoken — already live on `main` at `N≥4` (`(N−1.5)/N = 0.625`), widened by §5.1 to `N≥3` (`2/3 = 0.667`). "Smaller patterns are unaffected" is therefore true of the slot class only; DR-6 closes it.

**Proposal (DR-3): make it explicit — a verdict above `None` requires zero missed required *slots*.** Missed required *literals* remain fine: the command is still fully determined and its arguments are all present. **Ruled at G1 (2026-08-12) to leave the design lane entirely: it is filed as issue #66 and fixed through the issue lane**, since it is a live bug on `main` rather than a consequence of this redesign. It must merge before backlog item 1 (§5.1) begins.

**Checked against the documented `fire at` case** (`scoring.md:223`), with `["fire"]` and `["fire","at","{target}"]` registered — the full model preserves its `None` verdict by a different route:

| | Today | After §5.1 + §5.2 |
|---|---|---|
| `fire` (bare) | `1/1` = **1.00** — wins | `1 / (1+1)`, "at" is an orphan = **0.50** — still wins |
| `fire at {target}` | `(1+1−1)/3` = **0.33** | `(1+1−1)/3` = **0.33** — §5.1 does not touch slot misses |
| Verdict | `None` — winner leaves `at` unconsumed | `None` — winner is now also below `minScore` |

## 6. Forks considered and rejected

**F1 — Length-independent miss cost** (`F = 1 − k × misses`), #65's option (b) read literally. Rejected. It fixes fragility by making short patterns *too permissive* in the opposite direction: a 2-element pattern missing half its evidence would score `0.75` at `k = 0.25`. Length-proportionality is not purely a defect — a drop on a 3-element pattern genuinely destroys a larger share of the evidence than the same drop on a 7-element one. §5.1 halves the slope rather than abolishing it, which is the defensible middle.

**F2 — `minScore` that scales with pattern length**, #65's option (c). Rejected as DR-2. It leaves the broken number in place and compensates at the gate, so every consumer reading `VoxrCommand.Score` — session logs, the batch test runner, `OnUnrecognisedSpeech` diagnostics — still sees `0.50` and still has to know the pattern's element count to interpret it (`scoring.md:287` currently instructs readers to do exactly that). It also cannot address symptom 2 at all, since it never touches selection.

**F3 — Additive coverage term** `(F + w·C)/(1+w)`, #65's option (a). Rejected in favour of §5.2's denominator form. It introduces a free parameter with no principled value (crossover at `w > 0.5` after §5.1, `w > 0.75` before), it double-counts against the existing leading term, and at any weight that fixes #42 coverage rivals fidelity itself.

**F4 — Promote consumed span above score in selection.** Rejected. It is the change #58 already ruled out, and it remains wrong even after §5.1 fixes the gate problem: it would let a long low-fidelity pattern beat a short high-fidelity one in cases having nothing to do with orphaned tokens.

**F5 — Charge all trailing tokens, no orphan test.** Rejected outright: it breaks multi-command utterances. `cease fire` in "cease fire launch missiles target hotel one" would score `2/7 = 0.29` and be rejected, destroying sequential extraction (`scoring.md:108-118`).

## 7. Measurement plan

Ratified as a precondition on implementation, not a nice-to-have. Because every change here is scorer-only:

1. **No decoder run is needed.** The committed fixture transcripts in `NativeBridge~/harness/expectations.json` are replayed *through the parser* with the scorer varied. The grammar A/B rig (project memory `grammar-ab-rig`) compiles the real parser under `dotnet` with a `UnityEngine.Debug` stub in WSL, which is the exact vehicle — validate the rig against the committed pin first, per that memory.
2. **A/B each change independently and in order** (§5.4 guard → §5.1 fidelity → §5.2 coverage), reporting for each: which of the 17 fixture cases change `expectedIntent`/`expectedSlots`, and the score delta on every candidate that wins a round.
3. **Every one of the 15 score assertions is re-derived by hand**, not mechanically updated to whatever the code emits. An assertion updated to match new output is not a test.
4. **Both #65 symptoms are reproduced as failing tests first** (#65's own first suggested starting point), so each change is measured rather than argued.
5. **On-device verification is not required** — nothing here touches capture, the bridge, or lifecycle. Standard Unity EditMode + PlayMode per the project bindings.

## 8. Decision records — proposed, ratified at G1

**DR-1 — Fidelity and coverage are separated conceptually but stay one number.** The public `VoxrCommand.Score` remains a single normalised value. Exposing F and C separately would be a wider API change for diagnostic benefit that the session log already provides by other means.

**DR-2 — `minScore` stays a flat `0.6`.** Rejects #65 option (c). Rationale in F2.

**DR-3 — The eager gate gains an explicit completeness condition.** No verdict above `None` when the winner has a missed required slot. Lands first, independently valuable.

> **COMPLETED by Amendment A1 (ratified at G1, 2026-08-13, §0).** The slot rule stands and shipped as #66. Its unstated half — that missed required *literals* need no such condition — was **incomplete, not wrong**: a terminal missed literal leaves the command undetermined where the eager gate cannot see it. **DR-6 supplies the missing half**; DR-3 itself is unchanged.

**DR-4 — `skippedWordPenalty` is renamed to `coverageWeight`, preserving serialized values. Ratified at G1 (2026-08-12).** Under §5.2 the field governs both sides, so the existing name becomes actively misleading — and a user who set it to `0` would silently also disable the #42 fix. Rename the `[SerializeField]` with `[FormerlySerializedAs("skippedWordPenalty")]` so inspector values survive; keep an `[Obsolete]` forwarding property for one minor version. The public rename on a released package is **accepted**. It is not a new knob — it is the existing knob's honest name under the re-derived model, consistent with the G0 "no new tuning surface" scope.

**DR-5 — Existing grammars re-score on upgrade, with no compatibility mode.** Per the G0 posture. `CHANGELOG.md` calls it out under a behaviour-change heading, and `scoring.md` is rewritten rather than amended.

## 9. Feature backlog — proposed, canonical once locked at G1

Dependency-ordered; each row is one feature branch off the previous one's merge.

**Cross-lane prerequisite: issue #66** (DR-3, the eager-flush completeness guard) — ruled out of the design lane at G1 and fixed through the issue lane. **It must merge before item 1 starts**, because §5.1 widens the hole it closes. **Merged 2026-08-12 as PR #67.**

**Cross-lane prerequisite 2 (Amendment A1, ruling 3): the unmatched-required-tail guard** (DR-6) — a live bug on `main` at `N≥4`, filed as **issue #70** (2026-08-13) and fixed through the issue lane on the #66 precedent. **It must merge before item 1 starts**, because §5.1 widens the hole it closes from `N≥4` to `N≥3`. Item 1's own §9 row is otherwise unchanged.

| # | Feature | Scope | Depends on |
|---|---|---|---|
| 1 | `feat-fidelity-miss-cost` | §5.1. `RequiredLiteralMissPenalty` → `0`; re-derive the 15 affected score assertions by hand; A/B per §7. | issue #66 |
| 2 | `feat-coverage-in-selection` | §5.2 + DR-4. Orphan-tail term counted from `ConsumedEndIdx`, coverage moved inside selection, `skippedWordPenalty` → `coverageWeight` with `[FormerlySerializedAs]`. The large one. | 1 |
| 3 | `feat-scoring-docs-reconcile` | DR-5. Rewrite `scoring.md` §1–§3 and §6–§7, `command-recognition.md`'s Scored Matching section, and the `CHANGELOG` behaviour-change note. Product docs — post-G2 of item 2. | 2 |

## 10. G1 rulings (2026-08-12)

All three questions raised for G1 are resolved. No open questions remain.

1. **DR-4 — public rename accepted.** `skippedWordPenalty` → `coverageWeight`, with `[FormerlySerializedAs]` preserving serialized values and an `[Obsolete]` forwarding property for one minor version.
2. **§5.1's consequence accepted as correct.** The `minScore` gate stays `≥`; the `0.60` boundary case fires.
3. **DR-3 leaves the design lane.** Filed as issue #66 and fixed through the issue lane, as a live bug on `main` rather than part of this redesign. It is a prerequisite of backlog item 1, recorded in §9.

---

*Amendment A1 — **re-locked at G1 on 2026-08-13** (human ruling in conversation: "locked"). Adds DR-6, the eager gate's unmatched-required-tail condition, and a second cross-lane prerequisite ahead of backlog item 1 (§0, §5.4, §8/DR-3, §9). DR-1…DR-5 stand as locked on 2026-08-12; DR-3 is completed by DR-6, not superseded. This doc is immutable again. Raised during item 1's plan validation, which also corrected three of §2's recon figures (16 fixture cases not 17; no numeric score assertion moves under §5.1; the harness corpus cannot A/B a scorer change whose phenomenon it never contains). No design branch: `Planning~/` is gitignored and untracked, so this doc has no git history to branch — the amendment was recorded in place and re-locked by the human's ruling.*
