---
type: requirements
feature: sibling-set-detection
topic: sibling-tie-disambiguation
status: draft
updated: 2026-08-15
sources: [Planning~/design-docs/sibling-tie-disambiguation.md]
---

# Sibling Set Detection — Requirements

## 1. Feature & scope

Backlog line (design §9, row 1): `feat-sibling-set-detection` — the §5.1 primitive computed at construction, plus the §5.2 Editor-only author warning and the §5.5 discriminator/cancel collision report. **Also lands the §2.8 confirming test** (§7.5), since everything downstream depends on that finding. Ships goals **G-4** and **G-5** on its own; **no runtime behaviour change; no public API change.**

This is the first of four features under the G1-locked `sibling-tie-disambiguation` design (locked 2026-08-15, `main` at `ef84690`). It is the construction half of that design in full. It deliberately builds **none** of the runtime half: no three-state comparator, no eager refusal, no pending machinery, no opt-in flag. Those are items 2 and 3.

It is also, by the design's own §6.3, **independently valuable**: fork 6.3 ("warn at construction only, no runtime path") was a live option at G0 and remains the recorded fallback if items 2–4 are ever abandoned. This feature *is* that fork. It must therefore stand on its own — a coherent, shippable improvement that leaves nothing dangling if nothing follows it.

## 2. Why — user-visible cost of the defect

A grammar registers `switch to weapons` and `switch to navigation`. VOSK drops the discriminating word. The transcript is `switch to`, and the evidence fits both patterns exactly equally — same start, same score `(1+1+0)/3 = 0.67`, same consumed span, same literal count. Selection exhausts every key it has and falls through to its last: registration order. `mode_weapons` fires because it happened to be registered first.

That runtime behaviour is not what this feature fixes. **What this feature fixes is that the author is never told.**

The shape is statically visible — it is plainly readable off the two patterns, at construction, before a single word is ever spoken. The parser already runs three construction-time scans and reports what it finds. It is blind to this one (design §2.7). So an author ships a grammar that is fragile by construction, and the first evidence they get is a field report of the wrong command firing, with a score of `0.67` in the log that looks entirely healthy and a winner that is *correct* by every rule the parser has.

The cost is compounded by the fact that this shape is not exotic. Reducing the required-literal miss cost (#65 §5.1) extended the hazard from four-element patterns down to three, which is where two-word-prefix grammars live. And it worsens with pattern length rather than improving: a longer shared frame scores *higher* after one miss (`0.875` at 8 elements versus `0.67` at 3), so the grammars most likely to tie-and-fire are the elaborate ones.

**The existing warning does not cover it, and the issue's premise that it does is wrong.** `WarnOnDroppableRequiredLiteral` (#42, narrowed by #81) requires all three of: one pattern strictly longer than the other, the shorter being an element-prefix of the longer, and a **slot** stranded behind an added required literal. The #74 shape satisfies none of them — equal length, neither a prefix of the other, and the divergent element is a literal with nothing behind it. That scan detects a stranded *slot*; this feature detects a coin-flipped *intent*. Different hazards, different remedies, different messages.

## 3. Observable behavior

**In the Editor, at parser construction**, an author whose grammar contains a sibling set sees a new warning in the console naming the intents involved, which element position they differ at, and the two (or more) discriminating values — with actionable remedies. One warning per sibling *set*, not per pair, so a three-way `on`/`off`/`standby` set produces one message rather than six.

Separately, if a discriminating value collides with the cancel follow-up vocabulary, that is reported too — because cancel is checked first, such a value would be swallowed and the choice made unreachable once item 3 ships.

**Nothing else changes.** Same commands fire, on the same utterances, with the same scores, the same slots, the same selection order and the same eager-commit timing. The grammar JSON handed to the decoder is untouched. No public type, member or serialized field is added, removed or renamed. In a **built player** there is no change whatsoever, including no added construction cost — the scan is Editor-only, following the precedent #81 set for the existing scan.

The one further artifact is a **test**, not a behaviour: a test that pins whether the eager-commit path really does commit the wrong sibling early on a *medial* discriminator (design §2.8). That test asserts on today's behaviour; it changes none of it.

## 4. Functional requirements

| # | Requirement | Priority | Acceptance check | Source |
|---|---|---|---|---|
| F1 | **The sibling relation is exactly DR-1.** Two expanded pattern forms are siblings when they have **equal length**, are element-wise equal at **every position but one**, and that one position holds a **required literal in both**. The discriminator may sit at **any** position, not only the last. | Must | Tests pin siblings at a **trailing** position (`switch to weapons` / `switch to navigation`) and at a **medial** position (`set {ship} mode on` / `set {ship} level on`). Tests pin each non-sibling rejection separately: unequal length; differing at two positions; differing at a **slot** position; differing where either side is an **optional** literal; and two identical forms (differ at *zero* positions — see F8). | DR-1; design §5.1 |
| F2 | **One definition, one routine (DR-2).** The relation is computed by a single named routine that is the sole definition of "sibling" in the codebase. The warning and the collision report both consume its output; neither re-derives the relation. Item 2 promotes the *invocation* to runtime without redefining the *relation*. | Must | Exactly one code site tests the sibling predicate. A reviewer can point to one routine and say "this is the definition". The warning and the collision report are consumers of its result, not parallel scans. | DR-2 |
| F3 | **Sibling sets are n-ary, not pairwise.** `set auto pilot on` / `off` / `standby` is **one** sibling set with three discriminating values, not three pairs. The set is the unit of grouping and of reporting. | Must | A three-pattern grammar differing at one shared position produces **one** warning naming all three intents and all three values — not three, and not six. | design §5.1 |
| F4 | **The warning is Editor-only.** Gated with `[System.Diagnostics.Conditional("UNITY_EDITOR")]` in the same style as `WarnOnDroppableRequiredLiteral`, so it costs a built player nothing — not the message, and not the scan that produces it. | Must | A player build contains no call to the scan; the added constructor cost measured under F11 is Editor-only. Tests that assert on the message run where this package's suites run (editor Play Mode), matching how the existing scan's tests are pinned. | design §5.2; #81 precedent |
| F5 | **One warning per sibling set, deduplicated.** A set reachable from several form pairs is reported once, following the existing scan's `reported` HashSet pattern. | Must | A grammar whose optional-element expansion yields the same sibling set from multiple form pairs logs exactly one warning. Test asserts the count, not merely the presence. | design §5.2 |
| F6 | **The message names the intents, the patterns as authored, the differing element position, and the discriminating values**, and offers remedies that exist today. Per the human's ruling (2026-08-15) the design's trailing clause *"or enable ambiguity disambiguation (`<flag name>`)"* is **omitted from this feature** and added by item 3 when the flag exists. | Must | The message contains each intent **once**, every implicated pattern quoted as the author wrote it, the 1-based element position, and each discriminating value quoted. The element position must agree with the quoted pattern — see §4.4. It names only remedies available in the shipped version: diverge earlier, or mark the more destructive sibling `requiresConfirmation`. It references **no** identifier that does not exist. | design §5.2; human ruling 2026-08-15 (§8.1) |
| F15 | **Only cross-intent sets are warned about.** A set whose members all share one intent is detected by the primitive but not reported to the author: the same command is dispatched whichever pattern wins, so the wrong-intent harm the message describes cannot occur. Ruled on F11's evidence, 2026-08-15 (§8.3). | Must | A same-intent grammar produces a set from `FindSiblingSets` and **no** warning. A cross-intent grammar produces both. The **intent** filter is in the warning, not the primitive. ⚠️ **Corrected:** this row previously said "items 2–3 still see every set the relation admits", which is false — the primitive itself drops self-pairs, duplicate-valued members, and empty frames. The accurate statement is two-layer: the primitive returns *reportable hazards* (DR-1 minus exclusions no consumer could act on), and the warning narrows further to cross-intent. See §4.5. | human ruling 2026-08-15; design §1 harm model |
| F7 | **Discriminator/cancel collisions are reported (§5.5).** A discriminating value that matches the cancel follow-up vocabulary is reported, because `TryHandleConfirmCancel` checks cancel before confirm — such a value would be swallowed by cancel and the choice made unreachable once item 3 ships. Cancel keeps precedence; the author is told at build time rather than in the field. | Must | A grammar whose siblings differ on a value present in the cancel vocabulary (e.g. a literal `negative`) produces the collision report. A grammar whose discriminating values are free of collisions produces none. The report is distinguishable from F6's message. | design §5.5 |
| F8 | **Author-duplicated patterns are out of scope and must not be caught.** Two genuinely identical patterns differ at *zero* positions, not one, so DR-1 excludes them by construction. This feature does not report them and does not disambiguate them. | Must | A grammar with two identical patterns produces **no** sibling warning from this feature. Pinned as an explicit test so a future loosening of the predicate cannot silently absorb this class. | design §3 non-goals |
| F9 | **`WarnOnDroppableRequiredLiteral` is not modified.** The new warning is a separate message from a separate scan. #81 has just narrowed that scan to cut false positives; widening it back to cover this shape would undo that work. | Must | The diff shows no change to that method or to `BuildDroppableLiteralWarning`. Every existing test asserting on its message passes **unchanged**, with no edited expected strings. | design §5.1, §2.7; #81 |
| F10 | **The §2.8 finding is confirmed or refuted by a landed test.** §2.8 claims a *medial* discriminator slips every `TryEagerCommit` guard — `bestMissedRequiredSlot` false (the discriminator is a literal, not a slot), #70's `bestHasUnmatchedRequiredTail` false (the miss is medial, so the later match resets `requiredAfterLastMatch`), whole-buffer satisfied, `minScore` cleared at `2.0/2.5 = 0.8` — so the eager gate commits the wrong sibling early. **This was reasoned from source, never observed.** | Must | A test drives the eager path on a medial-sibling grammar with the discriminator elided and asserts what actually happens. **If it confirms:** the test lands as the pin DR-5 rests on, and its guard-by-guard outcome is recorded. **If it refutes:** work stops and the human is told — DR-5 loses its motivation and the design reopens on a new design branch. It is not to be quietly dropped either way. | design §2.8, §7.5, §10 |
| F11 | **Warning volume and constructor cost are measured, and the measurement gates DR-7.** Volume is counted across **every** grammar in `Tests~` and the samples; constructor cost is measured against the pre-feature baseline. This shape is extremely normal authoring — every `on`/`off` pair is a sibling set — and it **cannot be narrowed by pattern length**, because the arithmetic runs backwards. **Volume has a second, harder edge (see §4.3): every existing test whose grammar contains a sibling set starts emitting an unexpected warning**, and the count of tests that must be amended is itself a volume signal. | Must | Three numbers reported before G2: (a) sibling sets found in `DemoGrammar.cs` — the only coherent multi-intent grammar, 11 commands / 32 patterns, and it contains the `mode_weapons`/`mode_navigation` pair from #74 itself; (b) existing tests that newly warn, and how many needed amending; (c) the constructor cost delta. If volume is bad, DR-7's "warning on by default" **downgrades to opt-in** — a real decision deferred to this evidence, and the human's call, not this feature's. A warning that fires on healthy grammars is worse than no warning: the lesson #81 just paid for. | design §7.3; DR-7 |
| F14 | **Frame comparison folds optional SLOT decoration only; the discriminator test folds nothing.** `{?ship}` ≡ `{ship}` because a matched slot credits `MatchScore` either way, so two forms differing only there genuinely tie. `?to` is **not** ≡ `to`: a matched optional literal credits `OptionalLiteralScore` to both sides, and `(r−0.5)/(d−0.5) < r/d` for `r < d`, so those two patterns score differently and never tie. The **discriminator** position is tested on the raw element and must be a **required** literal in both. See §4.2. | Must | A test pins that `["switch","?to","weapons"]` / `["switch","to","navigation"]` is **not** a sibling set (they score `0.60` vs `0.667`), a test pins that `["set","{?ship}","mode","on"]` / `["set","{ship}","level","on"]` **is** one, and a test pins that `["turn","light","?on"]` / `["turn","light","?off"]` is not, the differing position being optional in both. | design §5.1 + DR-1, corrected against `TryMatchScored`'s scoring branches — see §4.2 |
| F12 | **No runtime behaviour change.** Selection, scoring, the eager gate, slot extraction and grammar generation are untouched. | Must | Both suites pass with **no edited expected values** anywhere. The `NativeBridge~/harness/expectations.json` baseline does not move. A diff review confirms no edit to `IsBetterCandidate`, `TryEagerCommit`, `ParseInternal`, or `GenerateGrammarJson`. | design §9 row 1 |
| F13 | **No public API change.** No public type, member, serialized field or event is added, removed, renamed or re-typed. | Must | The diff adds no `public` member and no `[SerializeField]`. The `VoxrCommandRecogniser` inspector has the same field count as before. | design §9 row 1 |

### 4.1 F2 — the primitive is computed Editor-only in this feature

DR-2 requires **one** sibling-set computation shared by all consumers, and §5.1 describes the end state as a precomputed lookup keyed by `(commandIdx, patternIdx)` that selection consults at runtime. This feature has **no runtime consumer**: the warning and the collision report are both Editor-only, and selection does not read the lookup until item 2.

**Decision: the routine is invoked Editor-only here, and item 2 promotes the invocation to runtime.** Storing a runtime lookup now would make every player build pay a constructor cost for a field nothing reads yet — cost with no benefit, in a package whose parser is rebuilt often.

**Corrected 2026-08-15 (review finding):** an earlier draft of this paragraph said the field would go unread "until item 3 ships it behind an opt-in flag". That is wrong by one item. DR-5 — the eager gate's refusal on a sibling tie — lands in **item 2**, and design §5.8 states explicitly that it is *not* behind the opt-in flag, because it is safe in both configurations. So item 2 makes every player build compute the lookup unconditionally. The deferral this section argues for buys exactly one feature's delay, not "until a flag exists", and item 2 should plan for an unconditional runtime lookup rather than inheriting the belief that the cost stays gated. The decision for item 1 is unchanged; only its horizon was misstated.

This does not weaken DR-2. DR-2 exists to prevent **two rival definitions** of one grammar shape (that is the reason §5.1 was written as a shared primitive at all). One routine, invoked from one place today and from three places after item 2, satisfies it exactly. What item 2 changes is *where the call sits*, not *what a sibling is*.

Recorded here rather than discovered at review, and flagged for the reviewer: if the reviewer reads DR-2 as requiring the stored lookup to land in item 1, that is a scope question for the human, not a silent choice.

### 4.2 F14 — `ExpandOptionals` does not strip decoration, and the predicate must account for it

Design §5.1 defines siblings "over the same expanded pattern forms `WarningForms` already produces" but does not say how two elements are compared. That gap has to be closed, because of a fact verified from source on 2026-08-15:

**`ExpandOptionals` (`VoxrCommandParser.cs:2170`) copies the raw element verbatim when an optional is included — it does not strip the `?`.** So an included optional literal still reads `"?to"` in the expanded form, and an included optional slot still reads `{?ship}`. The existing scan's `IsElementPrefix` compares with `StringComparison.Ordinal` against that raw text.

A naïve raw comparison therefore makes the predicate **arbitrary**: `["switch","?to","weapons"]` and `["switch","to","navigation"]` would differ at *two* positions rather than one, and would be missed — even though, at match time, an included `?to` consumes the token `"to"` exactly as a required `to` does. The two patterns compete on precisely the same tokens; only the authoring decoration differs. Under a raw comparison, whether the author gets warned about a genuine hazard would depend on an unrelated stylistic choice about a *different* element.

**Decision: normalize decoration for the frame comparison, and only for the frame.** Two positions match when they denote the same matchable element. The discriminator position is judged on the raw element, where the `?` genuinely matters: DR-1 requires a **required literal in both**, so an optional literal there disqualifies the pair. That is correct on its own terms and not merely a fallout of the encoding — if the discriminating word is already optional, the grammar author has said the pattern matches with or without it, so the two forms are duplicates rather than siblings, and F8 already excludes duplicates.

This refines DR-1 rather than contradicting it: DR-1 fixes *what* a sibling is, and this fixes *how equality is tested* — a question DR-1 is silent on. Recorded because a reviewer reading §5.1 against the code will hit this discrepancy, and because the cheaper raw comparison is the one a future maintainer would naturally "simplify" back to.

### 4.3 The volume hazard has a second edge: existing tests

§7.3's volume concern is written as an *authoring-noise* problem — will this warning cry wolf on healthy grammars? Verified recon surfaces a sharper, immediate form of the same question.

`Tests~/Runtime/VoxrCommandParserTests.cs` alone builds **98** ad-hoc grammars, and many of them deliberately construct near-identical pattern pairs to exercise scoring and selection. `Tests~/Runtime/DemoGrammar.cs` — the only coherent multi-intent grammar in the repo, and the hand-synced mirror of the shipped sample — registers `mode_weapons`, `mode_navigation`, `mode_all` and `mode_disable`, which is **the #74 example itself**. Every one of those grammars will start emitting a new construction-time warning.

That matters concretely because `NonHazardPatternShapes_DoNotWarn` (`VoxrCommandParserTests.cs:2126`) asserts warning *absence* via `LogAssert.NoUnexpectedReceived()`, and any test using that assertion over a sibling-shaped grammar will fail.

**How this is handled is itself a requirement, not an implementation detail.** Amending a test to expect the new warning is legitimate — the warning is correct and the grammar really does carry the hazard. Amending a test to *suppress* it, or weakening `NonHazardPatternShapes_DoNotWarn`'s assertion to make a failure go away, is a defect: it destroys the very check that would catch this warning going noisy later. So the count from F11(b) is reported as evidence, and each amendment is justified as "this grammar genuinely carries the shape", never as "the test was in the way".

If that count is large, it is the most direct evidence available that the warning is too noisy for DR-7's default-on — the repo's own test corpus standing in for a user's grammar.

**Measured: exactly one test needed amending** — `MissedLiteral_DroppedDiscriminator_FiresTheFirstRegisteredSibling`, which constructs the #74 grammar itself. Nothing else in the suite asserted warning absence over a sibling-shaped grammar. The binding volume evidence turned out to be §8.3's demo-grammar count, not the test corpus.

### 4.4 F6 — the element number must agree with the quoted pattern

Found in the first implementation, not in review. When one hazard surfaces under several optional-expansion frames, the dedup must choose which frame to report from, and the obvious choice — first-seen, following the existing scan — produces a message whose two halves disagree.

`["?please","switch","to","weapons"]` versus `["?please","switch","to","navigation"]` fills one bucket for the frame with `?please` included and another for the frame with it omitted. Reporting from the omitted frame gives *"element 3"*, its position in **that form** — while the message quotes the pattern **as authored**, where the discriminating word is element 4. An author counting elements in their own pattern lands on `to`.

**The surviving set is the one with the longest frame.** That makes the quoted text and the element number agree whenever any expansion of the pattern is a sibling at full length, and it removes the `"(with its optional elements omitted)"` note in the common case. Where no full-length expansion is a sibling, the note still appears and is then genuinely informative.

### 4.5 The primitive returns reportable hazards, not the bare relation

Recorded after the PR #92 review, which found the routine advertising two incompatible contracts three lines apart — "the sole definition of *sibling* (DR-2)" and then "what comes back is REPORTABLE sets, not every pair the relation admits".

Both sentences describe something true, and the two-layer model is the right shape. The confusion is that they share one name. Stated properly:

- **Layer 1 — the relation (DR-1).** Equal length, differ at exactly one position, required literal in both, at any position. Untouched, and not up for revision here.
- **Layer 2 — harm reachability.** Exclusions applied inside `FindSiblingSets` because *no* consumer could act on them, not because the warning does not want them: a pattern paired with its own other expansion (it cannot tie itself), a set whose members carry the same discriminating value (they are duplicates), and a frame with no remainder (both candidates score 0 and are rejected outright).
- **Layer 3 — audience.** The same-intent filter, which lives in the warning because it is about who needs telling, not about whether a tie occurs.

The distinction matters because DR-2 routes item 2 through this function. An item-2 author reading "the sole definition of the relation" would inherit layer-2 judgements made against *today's* selection arithmetic while believing they received DR-1. That is a subtler instance of the divergence DR-2 exists to prevent, moved inside one routine rather than between two — which is why it is worth one paragraph rather than a rename.

§7.4 of the architecture doc carries the open question of whether layer 2 is currently **complete**; the review found three cases it does not reach.

## 5. Non-functional requirements

- **Constructor cost stays the same order as the existing scan.** §5.1 puts the computation at `O(patterns² × forms²)`, bounded by `MaxWarningExpansion = 6`, alongside a scan that is already `O(patterns² × forms²)`. Asymptotically nothing changes, but it is not free — an editor session rebuilds the parser often — and F11 measures it rather than assuming it.
- **Zero cost in a built player.** Not merely "the message does not print": the scan itself must not run, and must not allocate. This is what `[Conditional]` buys over an `if`, and it is why the existing scan is written that way.
- **Deterministic output.** The same grammar produces the same warnings, in the same order, every run. Warning order must not depend on hash iteration order, or the tests that assert on it become flaky.
- **Messages are actionable and self-contained.** An author reading one message in isolation can identify which two patterns are implicated, which element is the discriminator, and what to do about it — without opening the design doc, and without being pointed at anything that does not exist in the version they are running (F6).
- **No new authoring surface.** No new attribute, field, or grammar syntax. The scan reads the patterns as authored.
- **Verification is EditMode + PlayMode only.** Nothing here touches capture, the native bridge, or MonoBehaviour lifecycle, so on-device verification is not required and must not be claimed.

## 6. Non-goals / deferred

- **The three-state comparator (DR-3) and the eager refusal (DR-5)** — backlog item 2, `feat-tie-aware-selection`. `IsBetterCandidate` keeps its `bool` return and its exact key list in this feature. F10's test *observes* the eager path; it does not change it.
- **The pending machinery (DR-4), the choice vocabulary, the timeout semantics (DR-6) and the opt-in flag (DR-7)** — backlog item 3, `feat-disambiguation-pending`. `VoxrPendingCommand` and `VoxrPendingReason` are untouched here.
- **Product docs.** `Documentation~/`, `CHANGELOG.md` and the `KNOWN_LIMITATIONS.md` rewrite are written **after** G2 per the workflow, and the guide-level narrative spanning all three features is backlog item 4.
- **Widening `WarnOnDroppableRequiredLiteral`** (F9). Two hazards, two messages.
- **Any scoring change.** The design says so in its own non-goals: this touches selection's *reporting*, never its arithmetic.
- **Dropped-word recovery.** The discriminating evidence is absent, not weak. Not attempted.
- **Author-duplicated patterns** (F8) — an authoring error, excluded by DR-1 rather than handled.
- **Closing issue #74.** It stays open; item 3 is where the behaviour it asks for arrives. A comment recording the locked design was posted 2026-08-15.

## 7. Dependencies & assumptions

- **No blocking predecessor.** Item 1 is the root of the backlog. Branched off `main` at `ef84690`.
- **#81 (PR #89) is merged and is the precedent this feature follows** on two points: the Editor-only `[Conditional]` gating, and the principle that a construction-time warning firing on healthy grammars is a defect in the warning. Its narrowing must survive this feature intact (F9).
- **Unity verification** runs through the host project `VoXR TestGround` (Unity 6000.4.7f1) per the project bindings — **both** EditMode and PlayMode, since most parser and command tests are PlayMode and an EditMode-only run misses them. Delegated to the `compile-check` agent.
- **VERIFIED 2026-08-15 — the collision report can only see the *default* cancel vocabulary.** `VoxrFollowUpVocabulary` is `internal static` in `Runtime/Commands/VoxrPendingCommand.cs:32`, same namespace and same assembly as the parser, so it is reachable with no new `using` and no asmdef change. **But** the vocabulary is overridable: `VoxrCommandRecogniser` carries `[SerializeField] string[] cancelVocabulary` (`:116`), and `TryHandleConfirmCancel` prefers it over the default when non-empty (`PendingCommandHandler.cs:86-88`). The `VoxrCommandParser` constructor takes only `(slots, commands, coverageWeight, additionalGrammarWords)` — **it cannot see that override.** F7's report is therefore **incomplete by construction** against a customised vocabulary. Accepted for this feature and stated rather than discovered: closing it needs a new constructor parameter, which F13 forbids, and item 3 — which is where the collision actually bites — is the right place to plumb the effective vocabulary. Carried to the architecture doc and named at G2.
- **VERIFIED 2026-08-15 — F10's test needs no clock seam.** `TryEagerCommit` is `internal` (`VoxrCommandParser.cs:1964`) and `Runtime/AssemblyInfo.cs:9-11` grants `InternalsVisibleTo` the two test assemblies. The existing `Tests~/Runtime/VoxrEagerCommitTests.cs` calls it directly as a pure function on a token array — no `VoxrCommandRecogniser`, no `Time.time`, no coroutine. F10's test follows that idiom exactly.
- **VERIFIED 2026-08-15 — the existing warning's tests are PlayMode, not EditMode.** All ten `LogAssert.Expect` sites for `WarnOnDroppableRequiredLiteral` live in `Tests~/Runtime/VoxrCommandParserTests.cs`; `Tests~/Editor` has zero coverage of it. This is deliberate (the `[Conditional]` comment at `VoxrCommandParser.cs:530-532` says so) and the new warning's tests go to the same file.

## 8. Open questions

### 8.1 The warning's flag clause — RESOLVED 2026-08-15

Design §5.2's message text ends *"…or enable ambiguity disambiguation (`<flag name>`)"*, but that flag is backlog item 3. Shipping the clause in item 1 would point an author at an identifier that does not exist in the version they are running, in a feature that explicitly declares no public API change — and §6.3/§9 make item 1 the standalone fallback if items 2–4 are abandoned, in which case the clause would be permanently wrong.

**Ruled by the human, 2026-08-15: omit the clause from item 1; item 3 adds it when the flag exists.** Cost is one string edit in item 3. Recorded as F6.

### 8.2 What happens if F10 refutes §2.8

§2.8 is flagged in the design's own G1 rulings as **load-bearing and unconfirmed**: DR-5 (the eager gate refuses on a sibling tie) exists solely because of it. If the medial eager commit does not reproduce, DR-5 loses its motivation.

The design's instruction is explicit and is followed here: **work stops and the human is told.** It reopens `sibling-tie-disambiguation.md` on a new design branch for amendment and re-lock. It is not silently dropped, and item 1's other deliverables do not proceed to G2 as though nothing happened. The refuting test still lands — a refutation is a finding, not a failure.

### 8.3 Whether volume forces DR-7 to opt-in — RESOLVED 2026-08-15, default-on stands

Deferred to F11's measurement by the design itself (§7.3). **Measured, and ruled: the warning ships on by default.**

The evidence was one-sided. `DemoGrammar.cs` — 11 commands, 32 patterns — yields **11 sibling sets: 5 cross-intent and 6 same-intent**. All five cross-intent sets are genuine wrong-intent hazards, three of them naming opposite actions (`cease fire`/`resume fire`, `stop firing`/`resume firing`, `enable all`/`disable all`). All six same-intent sets are ordinary synonym authoring.

So the noise was not distributed across the shape — it was **entirely** in the same-intent half. Suppressing that half (architecture §7.1) was the first narrowing.

**A second narrowing followed, and the "0% false-positive rate" claimed at that point was wrong.** The PR #92 review found that four of the five surviving cross-intent sets have two-element frames, which drop to `0.5` when the discriminator goes — under the default `minScore`, so **both** siblings are rejected and nothing fires rather than the wrong thing. The reachability gate in architecture §7.4 now suppresses those too.

**Final measured volume: one warning on the shipped sample grammar**, and it is the #74 pair itself. That is a far stronger case for default-on than the number this section originally carried — the warning fires precisely where the issue that prompted the feature actually bites. The alternative, shipping behind an opt-in flag, would have hidden it from exactly the authors most likely to hit #74, and would have pulled DR-7's flag into a feature that declares no public API change.

**These figures were wrong when first reported, and the correction is instructive.** The measurement was taken from a temporary scaffold that *hand-transcribed* the demo grammar, and the transcription introduced a pattern (`hold fire`) the real grammar does not contain while dropping one it does (`stop firing`). Two review angles caught it independently by re-deriving against source. The fix is structural rather than clerical: `DemoGrammar` now exposes `AllCommands()`, and `SiblingSets_DemoGrammar_VolumeAndOrderAreStable` reads the shipped definitions directly, so the evidence is reproducible from the branch and no transcription sits between the test and the grammar. The ruling was unaffected — the extra cross-intent set is a genuine hazard and the ratio barely moved — but the numbers carried to G2 would have been wrong.

Constructor cost was never the binding constraint. `FindSiblingSets` costs **~0.097 ms** per construction over the demo grammar, stable across runs; as a share of the whole constructor that read 2.6% and 4.9% on two runs, because the constructor's own total moved (3.80 ms → 1.99 ms) far more than the scan did. **The absolute figure is the one to quote** — a percentage of a noisy denominator is not evidence.

### 8.4 The vacuous design-branch step (process, not this feature)

`Planning~/` is gitignored, so design branches in this repo always carry a zero diff and the workflow's "merge the design branch to main" step can never do anything here — as was already true for `scoring-model.md`. Worth amending the project bindings to drop the step or replace it with something that records the lock. Low priority, unresolved, and does not block this feature.

## Related

- Locked design: `Planning~/design-docs/sibling-tie-disambiguation.md` §5.1, §5.2, §5.5, §2.7, §2.8, §6.3, §7.3, §7.5, DR-1, DR-2, §9 row 1, §10
- Origin: issue #74 (design comment posted 2026-08-15; stays **open**, closed by item 3)
- Precedent: #81 / PR #89 — Editor-only warning gating, and the false-positive lesson
- Adjacent, not modified: #42 (`WarnOnDroppableRequiredLiteral`), #70 (the eager tail guard)
- Successors: item 2 `feat-tie-aware-selection`, item 3 `feat-disambiguation-pending`, item 4 `feat-disambiguation-docs`
