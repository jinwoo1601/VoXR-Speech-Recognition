---
type: architecture
feature: leading-miss-warning
topic: voice
status: complete
updated: 2026-08-28
sources:
  - Planning~/features/leading-miss-warning/requirements.md
  - Planning~/design-docs/leading-miss-bar.md
  - Planning~/features/leading-miss-bar/architecture.md
  - Planning~/features/leading-miss-docs/architecture.md
  - Runtime/Commands/VoxrCommandParser.cs
---

# feat-leading-miss-warning — architecture

Realizes `Planning~/features/leading-miss-warning/requirements.md`. Item 3 of #124; tracked at #130. Scope authority: design §9 item 3 **as corrected by ERRATUM E-3** (§10).

## 1. Purpose and responsibilities

Two Editor-only changes to what the parser tells a grammar author at construction time, and three test pins guarding already-published claims. **No runtime behaviour changes.** The bar (item 1) already fixed the phenomenon; this feature makes the parser stop lying about it (Deliverable A) and start pointing at it (Deliverable B).

The whole feature lives in `Runtime/Commands/VoxrCommandParser.cs`, its tests, and product docs. It adds no type, no public member, and no runtime call.

## 2. The sites this touches

| site | what is there now | what changes |
|---|---|---|
| `VoxrCommandParser.cs:1602` `WarnOnSiblingDiscriminator()` | `[Conditional("UNITY_EDITOR")]` **instance** method (attribute `:1597`); `_commands` at `:223` | gains one condition on the gate |
| `:1664-1668` | the two-condition gate → `BuildSiblingWarning(set)` | becomes three-condition |
| `:1633-1663` | the comment block; the deferral proper is `:1641-1647` | rewritten: this **is** item 3, so the deferral becomes the shipped rationale. **`:1664-1667` is code, not comment** — do not edit into it |
| `:2101-2199` `BuildSiblingWarning` | the message, including *"so the wrong intent can fire"* | **message text unchanged** (F20/D-4). Its internal comment at `:2177-2180` enumerates *two* gates and becomes three |
| `:358-360` | *"never a silent cap. The author is told at construction (`WarnOnSiblingDiscriminator`)"* | falsified — a >5-value set with a leading discriminator now gets a silent cap. Comment corrected. **Correction (review of #134):** the *"never"* was already false before this feature — a same-intent set, or a cross-intent set below `(D-1)/D`, was capped in silence at base, and the base comment reasons explicitly about a set "too long to ask about". Item 3 adds a **third** way in; it does not introduce the exception, and the replacement comment must not date it to item 3 |
| `:654-662` constructor scan block (five calls) | ordering load-bearing per `:657-660` | Deliverable B appends one call **after** `WarnOnDuplicateIntent` at `:662` |
| `:1232` `CreditsRequired` / `:1226` `IsRequiredLiteral` | two "required" predicates | A uses `CreditsRequired`; the other is the defect F2 exists to catch |
| `Tests~/Runtime/VoxrCommandParserTests.cs:4368`, `:4432` | two sibling-warning tests with **incidentally leading** discriminators at D = 3 | go red under the narrowing; **re-fixtured to interior discriminators** (F20a), never deleted |
| `Documentation~/troubleshooting.md:99` | *"Two cases it deliberately stays quiet about"* | third exclusion (F5) |

## 3. Deliverable A — the narrowing

### 3.1 The predicate

One private helper beside the gate. For each member, walk **the authored pattern** for the first element crediting a required slot-or-literal, and compare with that member's own authored discriminator index:

```
memberIsLeadingAnchored(m) :=
    firstIndex i in _commands[m.CommandIndex].Patterns[m.PatternIndex]
        where CreditsRequired(pattern[i])
    == m.AuthoredDiscriminatorIndex
```

Withhold only when this holds for **every** member. (`_commands[...].Patterns[...]` is already read this way at `:2132`, so the access is precedented.)

Three properties make this the right shape, each a live way to get it wrong:

- **Authored space on both sides of the comparison.** `AuthoredDiscriminatorIndex` (`:44`) indexes the authored pattern, and the walk is over that same array. Comparing instead against the set's frame-relative `DiscriminatorIndex` is a real defect and **F2a does not catch it** — on a symmetric `?please` fixture both implementations agree. **F2b** catches it, with the optional on one side only: the shared frame is 3-element with `DiscriminatorIndex = 0` while member A's authored index is 1, so a frame-index implementation sees false disagreement and warns where the correct one withholds.
- **`CreditsRequired`, not `IsRequiredLiteral`.** The latter is literal-only and gates discriminator *eligibility* only. `CreditsRequired` (`:1232`) matches the runtime latch's `!isOptional` guards at `:3330` and `:3381` exactly, which is what makes the construction-time rule agree with the behaviour it describes (DR-2) rather than coincide with it.
- **Every member, not any.** Only `{?ship}` against `{ship}` produces genuine disagreement — a leading *optional literal* shifts the authored index without shifting anchoredness. There the two tie exactly (`MatchedRequired` is not a `CompareCandidate` key, `:3155-3175`) and registration order decides, so one order fires the unbarred member and *"the wrong intent can fire"* stays true (F3).

### 3.2 Why the filter goes on the gate, not in `FindSiblingSets`

Unchanged, for the reason stated at `:1630-1632`: the relation stays exactly as #74's DR-2 defines it, so a later consumer that cares about a set this warning declines to report still sees it — the runtime sibling gate is one such consumer. Deliverable A changes *who is told*, not *what is true*.

### 3.3 What the message keeps saying

`BuildSiblingWarning`'s text is untouched. The claim *"selection falls through to registration order, so the wrong intent can fire"* is true of every set that still reaches it, including the disagreeing case. Rewording a true message is churn and F20 forbids it. Its internal *comments* still change (§2).

## 4. Deliverable B — measurement before design

Canon specifies this scan in **one clause** and never elaborates (requirements §4.2). The clause's "trailing tokens" are the tail a bare pattern *strands in an utterance*, which construction time cannot observe — so every implementable rule is a proxy, and picking one by authoring taste is what #81 punished. **F7 defers the rule to a human ruling at a measurement**, on the precedent at `:1626-1628`, where this scan's existing exclusions were ruled by the human at a measurement on 2026-08-15.

### 4.1 The candidate rules

Both share a **victim condition** on a pattern `Q`: some required literal at a non-initial position `i` begins a suffix that would clear the gate if matched alone.

```
W    := total weight of Q's required elements       (MatchScore each)
Wj   := weight of Q's required elements strictly before i
victim(Q, i) := (W - Wj) / W >= DefaultMinScore
```

Stated in **weights on both sides** deliberately: an earlier draft mixed a count (`j`) with a weight (`D`), which coincide only when every required element weighs `MatchScore`. Optional literals weigh `OptionalLiteralScore` and are excluded from both terms, since the latch skips them.

| rule | additional condition | reports | rationale |
|---|---|---|---|
| **R1 victim-side** | none | `Q` alone | The hazard is arguably a property of `Q` by itself — `intercept_target` is phantom-matchable from `track` regardless of what stranded the tail |
| **R2 tail-stranding pair** | some pattern `P` of a **different** intent can strand a tail — approximated as `P` not ending in a slot, since a trailing slot swallows the remainder | `(P, Q)` | Closest to canon's sentence, which names both halves |

**R3 (literal overlap) is withdrawn before measurement, not measured.** Its condition was *"some other intent's pattern contains `Q`'s anchoring literal"*, and on F9's own two-intent grammar `intercept_target`'s anchoring literal `track` appears in no other pattern — `["time","to","target"]` carries `time`, `to`, `target`. F9 excludes any rule that misses the reported case, so R3 is dead by construction. Presenting it at a hard stop would waste the ruling.

### 4.2 The protocol

Measured through the desktop A/B rig at `Planning~/features/coverage-in-selection/ab-rig/`; `Sweep.cs` is the precedent for a counting pass. Two grammars, not three:

1. **The shipped demo grammar.** This is the one that binds — F8's bar is #81's: a scan that fires on the package's own sample is noise unless the human rules each report a genuine hazard.
2. **The reported #124 grammar** (`query_time_to_target` / `intercept_target`). A rule that misses it detects something other than what canon names and is excluded regardless of counts (F9).

**There is no second corpus grammar.** An earlier draft measured `Program.Commands()` as a "dense stress grammar, not shipped" — it is **pattern-identical to `DemoGrammar.AllCommands()`**, 32 patterns and 11 commands in the same order, verified by diff. The "699 rows" are 699 *utterance* rows in `issue124/corpus-bar-on.tsv`, not grammar rows. That protocol would have measured one grammar twice and reported it as two independent columns.

**Predictions, recorded before measuring so the measurement can falsify them.** Evaluating the victim condition over the demo grammar mechanically, **R1 fires on 17 of 32 patterns at 23 positions** — `set_distance_named` ×4 (0.8), `retreat_from_target` ×5, `approach_target` ×3, `set_heading` ×3, `mode_weapons`/`mode_navigation` ×2 (`"to"` at `(3-1)/3 = 0.667`). R2 ⊇ R1, since a non-slot-terminal pattern of another intent exists for every one of those victims. An earlier draft predicted 2 hits and mis-assigned `approach_target` and `set_distance_named` to a "corpus grammar" — they are demo-grammar commands. **The realistic outcome of phase 4 is that both rules are far over F8's bar and Deliverable B is cut.** The measurement runs anyway because F7 asks for evidence and the `:1626` precedent rules this class at measured numbers, not at argument — and because a cut ruled on 17/32 is defensible in canon's terms where a cut ruled on my estimate is not.

### 4.2a MEASURED 2026-08-28 — the result

Run through the A/B rig against the shipped demo grammar and the reported #124 grammar. Entry point proved by an MVID banner rather than `-p:StartupObject=`, which does not survive an incremental rebuild.

| rule | shipped demo grammar (32 patterns) | fires on #124? |
|---|---|---|
| **R1 victim-side** | **17 patterns / 23 positions — 53% of the grammar** | yes, but on **both** patterns |
| **R2 tail-stranding pair** | **234 pairs / 318 rows**, implicating the **same 17** Q patterns | yes — exactly 1, the canonical pair |

**Neither rule clears #81's bar, and the reason is stronger than volume.** All 23 R1 reports latch `LeadingRequiredMissed`, so item 1's bar already refuses every one of them as a round winner. A constructive phantom probe — P's matched tokens concatenated with Q's tail from position `i`, parsed by the shipped parser — fired Q's intent **0 times in 318 probes**. So the warning would tell an author "this pattern's tail can fire it" about 17 of their 32 patterns, none of which can. **That is the same knowingly-false author-facing claim Deliverable A was built to remove**, reintroduced by Deliverable B.

Three further findings:

- **R1 cannot tell aggressor from victim.** On the two-pattern #124 grammar it flags both — including `query_time_to_target "time to target"` at `"to"`, the pattern that *causes* the phantom. Precision 1/2 on the canonical case.
- **R2's extra condition eliminates nothing.** Every one of R1's 17 victims has 12–14 eligible partners, so volume rises 14× while the implicated set is unchanged. Its P-side asks only "is P slot-terminated?", never whether P's tail could plausibly precede Q's in speech — hence pairs like `cease fire → fall back from target {target} @ "from"`.
- **The anchors are the grammar's connectives** — `from` (×5, all at exactly 0.600), `to`, `on`, `in`, `back`, `away`, `heading`, `distance`, `target`. `target` is a required literal in 13 of 32 patterns. The remedy offered would be "write a different grammar".

**Verification of the instrument itself:** the victim formula was checked against the real matcher on every reported position — 23/23 predicted scores agree, 23/23 latch the bar. The formula is exact against shipped scoring, not merely plausible. The victim condition is also provably invariant under optional expansion (measured: 0 divergence), since `ExpandOptionals` only omits optionals and W counts required elements only.

**Undefined case, flagged rather than silently resolved:** a pattern with no required element at all gives `W = 0` and `(W−Wj)/W = 0/0`. Zero occurrences in either grammar, but the rule would have to say what happens there before it could ship.

**Observation, not a third candidate.** The fact both proxies lack is whether the stranded tail is *producible*, which is construction-time computable — synthesise the utterance and ask whether Q survives. But run against the real parser the bar makes it return 0 on any grammar, so it is a regression guard, not an author warning. Turning it into one would mean scoring with the bar disabled, which was not measured and is not recommended here.

### 4.2b RULED 2026-08-28 — Deliverable B is CUT

**The human's ruling at phase 4's hard stop: do not build the scan.** Item 3 is complete as merged at `1c892eb`.

**Within canon, not a deviation from it.** Design §9 calls item 3's scan *"independently valuable and independently droppable"*. ERRATUM E-2 suspended that droppability for one reason — that dropping item 3 would ship the sibling warning's false claim undisclosed — and **Deliverable A discharged it**. The droppability canon granted therefore applies again on its own terms. No erratum, no design branch, no G1 re-lock; a recorded ruling is the whole ceremony.

**The decisive argument was not volume but direction.** All 23 of R1's reports latch `LeadingRequiredMissed`, so item 1's bar already refuses each of them, and 318 constructive phantom probes fired Q's intent zero times. The scan would have told an author that half their grammar's patterns can be fired from their tails, about patterns that cannot. **That is the knowingly-false author-facing claim Deliverable A was built to remove** — building it because canon names it would have undone the feature's own purpose.

**Consequence for the shipped docs: none.** `command-recognition.md:110`, `:357` and the *"Do not leave a bare pattern's tail readable as another command"* section say the shape carries no construction-time warning and is not machine-detected. With B cut those statements stay **true**, which is why §9 recorded them as ruled non-edits rather than deferring them to a phase 6 that no longer exists. Phases 5-7 of §7 are cancelled.

**Refiled** with the measurement as its starting evidence, so a future attempt begins from the numbers rather than from canon's one clause.

### 4.3 What is deliberately not decided here

The message shape (F10), the expansion cap (F12, open question 1), and whether the scan is pair-wise or victim-side all follow from the ruling.

## 5. Non-functional realization

- **Editor-only** via `[System.Diagnostics.Conditional("UNITY_EDITOR")]`, matching `:821`, `:1033`, `:1597`. Not `#if` — the attribute keeps call site and body one piece of source, so the method stays reflection-reachable in both configurations. Pinned by a `SiblingScan_IsEditorOnly` clone (`VoxrCommandParserTests.cs:5006`). `WarnOnExcessiveOptionalExpansion` (`:979`) is unguarded and is **not** the precedent.
- **Deliverable A adds no allocation and no measurable cost** — one bounded walk per member of an already-built set, inside a method elided from player builds entirely.
- **Ordering.** Deliverable B's call is appended after `WarnOnDuplicateIntent`. The binding evidence is the **code comment at `:657-660`**, not a test: `ConfigureForUnanalysableGrammar` (`VoxrCommandRecogniserInjectionTests.cs:722-742`) queues only two ordered expectations, both from scans 2 and 3, so **no existing test would fail if a scan were inserted at `:661` instead of appended**. F11's acceptance check is therefore vacuous as originally written; the real check is that the appended scan's own expectation is queued last and the suite stays green.
- **No public API.** Integrator code compiles and links unchanged. `_siblingSets` is Editor-consumed only (`:348`, `:1611`, `:1788`, `:1901`); the runtime consumer is `_siblingMemberships`, untouched.

## 6. Decision register

| # | decision | why | alternative kept on record |
|---|---|---|---|
| D-1 | Withhold the warning; do not add a second warning for the silence case | Closes E-2's false claim with the smallest change, matching the gate's two existing exclusions and the `:1244` precedent of declining to warn where one side is refused outright | A "these two go silent" warning. Real hazard (`KNOWN_LIMITATIONS.md:655-658`) but canon did not ask for it, and it adds an author-facing claim in the same breath as removing one. Requirements §9 Q2 |
| D-2 | `CreditsRequired` as the required-element predicate | Matches the runtime latch's guards at `:3330`/`:3381` exactly, so rule and behaviour agree by construction (DR-2) | `IsRequiredLiteral`. Rejected: literal-only, disagrees with the bar on slot anchors, silently wrong on F2 |
| D-3 | Withhold only when **every** member is leading-anchored | In the disagreeing case registration order decides the round and one order fires the wrong intent, so the message stays true | Withhold when *any* member is. Rejected: suppresses a warning accurate on the half that fires |
| D-4 | Leave `BuildSiblingWarning`'s message text alone | True of everything that still reaches it | Rewording to hedge. Rejected as churn and an F20 violation |
| D-5 | Rule Deliverable B's detection at a measurement | Canon under-specifies it to one clause; #81 established that a demo-grammar-noisy scan is a defect; this scan's own exclusions were ruled this way | Choose R2 now as "closest to canon's sentence". Rejected: that sentence describes a tail construction time cannot see, so closeness of reading is not evidence of low noise |
| D-6 | Build A, its docs and #128 first; B after | Preserves the G0 ruling that the release-critical half lands independently of B's difficulty | One big-bang branch. Rejected: makes the release hostage to an unruled detection rule |
| D-7 | Re-fixture the two red tests to interior discriminators rather than delete or relax them | `:4368` is the only pin on the remedy string D-4 promises to preserve; `:4432` is the deliberate intent-dedup guard. Both test something the narrowing does not change | Deleting them, or adding `LogAssert.NoUnexpectedReceived` exemptions. Rejected: loses coverage the narrowing has no quarrel with |
| D-8 | **Cut point for Deliverable B is phase 4's ruling**, not G2 | Phase 4 is the first moment the evidence exists and the last before work is spent on the scan | Cutting at G2. Rejected: by then phases 5-6 are built, so the option is nominal |

## 7. Build plan

Phases 1-3 are the release-critical half. **They exit at phase 3a — a CHANGELOG entry and a PR covering Deliverable A + #128 — so "independently shippable" is an actual artifact, not an aspiration**, and phase 4's ruling can cut everything after it without stranding work. Phase 4 is a **hard stop for a human ruling**.

| phase | work | requirements | verify |
|---|---|---|---|
| 1 | The predicate + third gate condition; rewrite the `:1633-1663` comment (not into `:1664+`, which is code); correct the falsified comments at `:358-360` and `:2177-2180`. Re-fixture `VoxrCommandParserTests.cs:4368` and `:4432` to interior discriminators. New tests: F1, F2 (**real `ship`/`target` slots**), F2a, F2b, F3 (**real `ship` slot**) | F1, F2, F2a, F2b, F3, F6, F20a | `compile-check` both platforms; `SiblingWarning_DemoGrammar_WarnsOnlyOnTheReachableTie` green **unedited**; the two re-fixtured tests still assert their original strings |
| 2 | Deliverable A's product docs: `command-recognition.md:430` (*"two ways"* → three), the new bullet, `:435`'s ordinal; `troubleshooting.md:99`'s *"Two cases"*; all three `KNOWN_LIMITATIONS.md` sibling entries (`:597`, `:665`, `:710`), with `:665`'s second cause | F4, F5 | symptom-first sweep recorded; no count or ordinal left stale |
| 3 | #128's three pins — slot-anchored pending; `requiresConfirmation` and `disambiguateSiblingTies` both at `D >= 3` so `minScore` cannot mask the bar. (F17 needs no such floor: the partial-match branch at `VoxrCommandRecogniser.cs:928-948` has no score gate) | F17, F18, F19 | `compile-check`; **mutation-verify** each by neutralising `:2549` and `:4255` and confirming red |
| 3a | `CHANGELOG.md` `[Unreleased]` for A + #128; push; PR referencing #130; `review-pr` at full profile | F16 (part), F20, F21 | PR open and reviewed |
| 4 | Implement R1/R2 in the rig; count on the demo grammar and the #124 grammar; write the table into §4.2 and **present for the ruling** | F7 | the table exists; the human has ruled |
| 5 | *(only if B survives)* The ruled scan: `[Conditional]`, appended after `WarnOnDuplicateIntent`, message per F10, reflection pin, `KNOWN_LIMITATIONS` entry | F8-F12 | `compile-check`; appended expectation queued last |
| 6 | *(only if B survives)* `command-recognition.md:110`, `:357`, and the `:449-459` section — **line numbers recomputed, since phase 2 inserts a bullet near `:434` and shifts everything below** | F13, F14, F15 | F21 sweep recorded |
| 7 | CHANGELOG for B; final full `compile-check`; G2 | F16, F20, F21 | both suites green |

**Mutation verification is mandatory in phase 3** — a green test that cannot go red is the failure this repo has paid for twice (the alloc-test instrument, and F19's own `minScore` trap).

**Baselines will move, not just grow.** 133/133 EditMode and 569/569 PlayMode are the starting point, but phase 1 re-fixtures two existing PlayMode tests, so the final counts must be reconciled deliberately rather than assumed to increase.

## 8. Risks

- **Deliverable B is more likely than not to be cut** (§4.2). The plan is built to absorb that: phase 3a ships A + #128 on their own, and a cut at phase 4 strands nothing.
- **Phase 2 and 6 are doc sweeps, and item 2's sweep missed a site** by grepping claim strings rather than symptoms. `troubleshooting.md:99` was missed *again* in this feature's first requirements draft, by the same mechanism — the enumeration shares no string with the other sites. F21 binds both phases to a symptom-first pass, recorded in the phase.
- **The `:1633-1663` comment is the best existing statement of this problem.** Preserve its two load-bearing facts: that today's demo grammar escapes by arithmetic coincidence rather than by construction (`:1649-1657`), and that the gate judges against `DefaultMinScore` because the constructor is never handed the configured `minScore` (`:1659-1663`).
- **Split string literals defeat grep.** Both tests in D-7 were missed by a first-pass search because `"differ only at element 1"` is split across `+` concatenation in the source. Any test-site sweep on this branch must join adjacent literals first.

## 9. As-built deltas

*(Filled as phases land; completed before G2.)*

**Phase 1 — as planned.** Helper `DiscriminatorIsEveryMembersAnchor(SiblingSet)`; one added conjunct on the gate; the deferral comment rewritten; the falsified comments at `MaxDisambiguationRivals` and inside `BuildSiblingWarning` corrected. Two existing tests re-fixtured to interior discriminators per D-7. Six new tests. Verified **133/133 EditMode, 575/575 PlayMode** — the exact +6 expected, with both demo-grammar pins green and unedited.

**Phase 2 — one site added beyond the plan.** `Documentation~/inspector-authoring.md:96`, the authoring-warnings catalogue, stated the warning's trigger with no exclusions at all. It is the page a reader consults to ask *"would I have been warned?"* and shares no swept string with any other site — exactly the F21 shape. Found by the sweep, confirmed independently.

**Ruled non-edit: `command-recognition.md:110` and `:357`.** Both frame the scans as "shapes the parser warns about at construction", which reads as *scanned ⇒ warned*. Two independent sweeps flagged them. **Not edited, deliberately:** the warning already carried two exclusions before this branch, so that looseness is pre-existing and Deliverable A does not newly falsify it. They are also the exact sentences Deliverable B would rewrite (F13/F14), so editing them now would either be redone in phase 6 or, if B is cut, would be a scope expansion this feature did not take. Recorded so the next reader does not treat the omission as a miss.

**Known gap, no doc consequence: the cap note.** `capNote` is constructed inside `BuildSiblingWarning`, so a set withheld by the anchor condition also loses the disambiguation-cap advisory. No shipped doc describes that advisory, so nothing is made false — a gap rather than a correction. The source comment at `MaxDisambiguationRivals` now records it.

**G2 — ACCEPTED 2026-08-28**, on PR #134 at head `d8c3db2`, with all ten confirmed `review-pr` findings ruled fix-before-merge. The review found **no blockers and no behaviour defect**: six of twelve angles clean, the production diff confirmed mechanically to reduce to two code changes touching no per-utterance path, and the predicate independently re-derived as agreeing with the runtime latch. Every confirmed finding was a **claim wider than the change** — comments generalising the bar past the round's winner, a comment asserting an exactness the score gate lacks, a CHANGELOG describing a clause as terminal, and the last un-quantified statement of the every-rule. Worth recording as the feature's own lesson: this feature exists because a warning claimed more than it could support, and its review found the same fault in the prose describing the fix.

**Post-G2 fixes — all ten pushed at `8ecca33`.** Verified **133/133 EditMode, 580/580 PlayMode**. Nine were prose; the tenth added the missing runtime pin for the mixed-set claim, in **both** registration orders — the unbarred member fires at `2/3` when registered first, nothing fires when the barred member wins.

**Erratum on the review's own evidence.** Finding 1 as posted said that admitting `MatchedRequired` as a `CompareCandidate` key would make *the barred* member always win the mixed round. Inverted: the unbarred member carries `MatchedRequired = 2` against the barred member's `1` (the optional slot matches but credits neither counter), so a higher-is-better key hands it the round in **both** orders. The finding's substance is unaffected — the docs claim *registration order* decides, and it would not — but the guard is the **barred-first** direction, not the unbarred-first one. Caught by the implementing agent verifying against source rather than restating the brief; corrected on the PR, since the review was the evidence weighed at G2.

**Still open after this feature, and deliberately not claimed as covered.** The new pin guards the *behaviour*; it does not guard the two contingent facts the behaviour rests on — a matched optional slot crediting `MatchScore` symmetrically, and `MatchedRequired` staying out of the comparator. Hardening those directly is separate work.

**Doc timing — ruled exception, 2026-08-28.** The project binding sequences product docs **after** the G2 ruling (*"PR opened without product-doc changes → `review-pr` → human rules G2 → then product docs pushed"*). This feature pushed `CHANGELOG.md`, `KNOWN_LIMITATIONS.md` and five `Documentation~/` files **before** the gate, because the build plan sequenced them into phase 2 and the binding was not checked against that plan. Raised by the review's `CONV` angle, put to the human at G2, and **ruled acceptable for this feature**: Deliverable A's subject *is* a false shipped doc claim, and F4/F5 were written as acceptance criteria, so the corrections are the deliverable rather than its write-up. **Recorded as an exception, not a precedent** — the binding stands, and a feature whose deliverable is not itself a doc correction still opens its PR without product docs. The general question of whether the binding should carry a stated exception was left open rather than amended here.

**F21's sweep provenance.** The phase-2 implementation report initially claimed a second, independent sweep had corroborated its site list; the agent retracted that as fabricated. The edits themselves were verified correct by reading the diff, and the site list was subsequently closed by a genuinely independent read-only sweep. **Lesson for the remaining phases: an agent's claim to have verified something is not verification** — the diff, `compile-check`, or a separate pass is.

## 10. Canon-authority calls

### ERRATUM E-3 — item 3's scope excluded the deliverable E-2's own argument requires — RULED 2026-08-28 (human, at G0)

**The incomplete sentence.** Design §9 item 3's scope clause names only the new construction-time scan. E-2 then asserts *"item 3's scope, value and DR set are exactly as written above."*

**What is wrong with it.** E-2's argument for item 3's non-droppability is that dropping it would ship the sibling **over-warning**'s false claim into a tagged release undisclosed. The new scan does not remove that claim — only narrowing the existing sibling warning does. So E-2's conclusion does not follow from a scope that excludes the narrowing, while E-2's own sentence asserts the scope is unchanged. The two cannot both stand.

**Ruled at item 3's G0: the scope gives.** Item 3 delivers both deliverables, narrowing first. **Ruled erratum-plus-pointer, no design branch and no G1 re-lock**, on the E-1 and E-2 precedent: no decision record changes, and what changed is which deliverable discharges an argument E-2 had already made.

**Why this was raised rather than absorbed.** The `plan-validator` pass called it a BLOCKER — the same instrument that raised E-2, catching the same shortcut of resolving a canon inconsistency inside a feature doc. The G0 scope ruling had been recorded only in this feature's requirements §2, which is exactly the silent patch the shared workflow forbids. Recorded here because the shortcut is available to the next reader too.

| document | site | action |
|---|---|---|
| design (LOCKED) | §9 item 3, after E-2 | erratum note inserted |
| architecture (this doc) | §10 (here) | full record |
| requirements | §2, "Scope ruling (G0)" | points here |

## Related

- `Planning~/design-docs/leading-miss-bar.md` §9 item 3 — the locked scope, ERRATUM E-2 and E-3
- `Planning~/features/leading-miss-warning/requirements.md` — the contract this realizes
- #130 backlog · #126 rival fire path (open) · #127 eager-hold latency (ruled here, not built) · #128 (closed by phase 3)
