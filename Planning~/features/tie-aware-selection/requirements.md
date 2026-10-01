---
type: requirements
feature: tie-aware-selection
topic: sibling-tie-disambiguation
status: draft
updated: 2026-08-16
sources: [Planning~/design-docs/sibling-tie-disambiguation.md, Planning~/features/sibling-set-detection/requirements.md, Planning~/features/sibling-set-detection/architecture.md]
---

# Tie-Aware Selection — Requirements

## 1. Feature & scope

Backlog line (design §9, row 2): `feat-tie-aware-selection` — DR-3's three-state comparator; both selection loops record a tied sibling rival; DR-5's eager refusal (§5.8). Depends on item 1. *"The flush-side recording is inert here, consumed only by the editor diagnostic; **the eager refusal is a real, unconditional behaviour change** — same intent fired, deferred from eager to flush — which is why it gets its own review and its own A/B (§7.2) rather than riding along with item 3."*

This is the second of four features under the G1-locked `sibling-tie-disambiguation` design (locked 2026-08-15). Item 1 built the construction half: `FindSiblingSets` as the DR-2 primitive, an Editor-only author warning, and the cancel-collision report. **It is the first item with a runtime behaviour change**, and the whole of that change is one word of timing: on a sibling tie the eager gate stops committing early and lets the flush decide. The intent that fires is the same one that fires today.

It deliberately builds **none** of item 3: no pending machinery, no `AwaitingDisambiguation`, no choice vocabulary, no timeout semantics, no opt-in flag. Nothing in this feature asks the speaker anything. The flush path still fires the first-registered sibling exactly as it does today — this feature makes the tie *visible*, it does not yet make it *answerable*.

**One deferred issue is pulled in.** Issue #90 (`FindSiblingSets` drops duplicate-valued members rather than grouping them) was filed at item 1's review as a reporting defect deferred to item 2. It is more than that here: a member dropped from a set is invisible to this feature's `(commandIdx, patternIdx)` lookup, so a genuine sibling tie escapes the eager refusal. It is a correctness prerequisite, not a cleanup, and is in scope (§4.6). Issue #91 (the warning's element number can index a different word than the pattern it quotes) stays deferred — it is cosmetic, Editor-only, and item 3 already rewrites that string to add the flag clause. Ruled by the human, 2026-08-16.

## 2. Why — user-visible cost of the defect

Item 1 confirmed §2.8 by observation rather than reasoning, and the finding is the whole motivation for this feature.

A grammar registers a medial discriminator:

```
set_mode  : ["set", "{ship}", "mode",  "on"]
set_level : ["set", "{ship}", "level", "on"]
```

The speaker says one; VOSK drops the discriminating word; the buffer reads `set alpha on`. `TryEagerCommit` walks every condition it has and passes all of them: the discriminator is a literal, so the missed-required-slot condition (#66) is false; the miss is *medial*, so the later match of `"on"` resets the tail check and #70's condition is false; the match spans the whole buffer; the score is `3.0/4.0 = 0.75`, comfortably over the `0.6` default; and the extendability precompute says a four-element pattern cannot be extended by another four-element pattern. **The verdict is `Commit`.** Pinned on `main` by `TryEagerCommit_MedialSiblingDiscriminator_CommitsOnAnUndecidableBuffer`.

The command then fires **immediately**, on evidence that cannot tell the two intents apart, and `Parse_MedialSiblingDiscriminator_FiresByRegistrationOrderAlone` shows what decides it: reverse the two declarations in the asset and the same utterance fires the other intent. Nothing about the speech changed. The coin flip is not merely a fallback the parser reaches after exhausting its keys — on this shape it happens *early*, before the word that would have settled it has had a chance to arrive.

That last clause is the cost, and it is why the trailing case the issue originally reported is the *milder* one. On `switch to weapons` / `switch to navigation`, #70's tail condition already refuses the eager commit, so the parser waits and a late-arriving `"navigation"` still lands. On the medial shape there is no wait at all. The speaker is still mid-utterance and the wrong command has already gone out.

`KNOWN_LIMITATIONS.md:538` states this today in as many words — *"Both paths guess; the medial one guesses sooner."* This feature removes the "sooner".

**What it does not fix.** After this feature the medial case guesses at exactly the same moment the trailing case does, by exactly the same rule, and gets it wrong exactly as often when nothing further arrives. Making the guess *answerable* is item 3. What changes here is that the parser stops throwing away its one remaining chance to be told.

## 3. Observable behavior

**At runtime, on a sibling tie the eager gate refuses.** A buffer whose best candidate ties an equally-scoring rival from a different intent in the same sibling set no longer commits early; the recogniser waits out `bufferWindow` and the flush fires. Where more speech arrives in that window — the dropped word, or a correction — the flush sees a longer buffer and can select on evidence the eager scan did not have. Where nothing arrives, **the same intent fires, up to `bufferWindow` later.**

**Nothing else about selection moves.** The same command wins on the same utterance with the same score, the same slots, the same consumed span and the same registration-order fallback. The flush path is behaviourally untouched. The trailing sibling shape is untouched, because #70's condition already refused it. The grammar JSON handed to the decoder is untouched.

**In the Editor, at construction**, one warning message changes wording: the remedy *"Diverge earlier"* is replaced, because it is wrong — `weapons mode` / `navigation mode` ties exactly as `switch to weapons` / `switch to navigation` does, and diverging earlier removes nothing. Separately, a sibling set now names **every** pattern that carries the hazard rather than silently dropping one that happens to share a discriminating value with an earlier member (#90).

**In the Editor, at parse time**, the flush path records which rival tied the winner, alongside the per-command diagnostics the parser already exposes. Nothing reads it to make a decision; it exists so the tie is inspectable, and item 3 promotes it.

**No public type, member, serialized field or event is added, removed, renamed or re-typed.** The `VoxrCommandRecogniser` inspector has the same field count as before. There is no flag to turn any of this on or off (§4.2).

## 4. Functional requirements

| # | Requirement | Priority | Acceptance check | Source |
|---|---|---|---|---|
| F1 | **The comparator gains a third state (DR-3).** `IsBetterCandidate`'s `bool` becomes a three-state result — `Better` / `Tied` / `Worse` — and both call sites consume it. The rejected alternative, an equality probe at the call site, stays rejected: it duplicates a key list that has already changed twice (#41 added span, #65 added the admission rule), and with DR-5 putting tie-awareness on both paths it would have to be maintained in two places. | Must | Exactly one routine defines candidate ordering, and both selection loops call it. No call site re-derives equality from `MatchResult` fields. | DR-3; design §5.3, §2.3 |
| F2 | **Selection is bit-identical.** `Better` is returned in exactly the cases the current `bool` returns `true`; every other outcome is a case it returns `false`. Adding the third state must not move a single winner. | Must | Both suites pass with **no edited expected values** anywhere except the tests F5 and F11 explicitly invert. The 699-utterance A/B shows zero difference in fired intents, slots and scores (§4.7). `NativeBridge~/harness/expectations.json` does not move. | design §3 non-goals ("not a scoring change") |
| F3 | **`Tied` means the keys compared equal on an admissible candidate — it is not "not better".** A candidate refused by the `Score <= 0f` floor or by DR-7's admission rule is `Worse`. A candidate met by the no-incumbent sentinel is `Better`. `Tied` is reachable only when a real incumbent exists and start index, score, consumed span and literal count all compare equal. | Must | Direct tests on the comparator pin one case per outcome: zero score → `Worse`; `MissedRequired > MatchedRequired` → `Worse`; no incumbent → `Better`; each key differing in each direction → `Better`/`Worse`; all four equal → `Tied`. | design §2.1; DR-7 |
| F4 | **Admission is inherited, never re-derived.** A candidate DR-7 refuses can never be recorded as a tied rival, because the comparator returns `Worse` before any key is compared. Item 1's warning needed a separate reachability argument for this; the runtime path gets it for free. | Must | A grammar where one sibling is refused by admission and the other is admitted alone produces **no** tie and **no** refusal — the admitted one commits exactly as today. | design §5.3; item 1 architecture §7.4 |
| F5 | **The eager gate refuses on a sibling tie (DR-5, §5.8).** When the eager selection's winner is tied by a rival that is a sibling of it, `TryEagerCommit` returns `EagerCommitVerdict.None` — not `HoldExtendable`, matching what #70's condition already returns for the trailing case. | Must | `TryEagerCommit_MedialSiblingDiscriminator_CommitsOnAnUndecidableBuffer` **inverts**: the same fixture that pinned `Commit` on `main` now pins `None`, and the test is renamed and re-commented to say that this is the change, not a regression. The trailing shape still returns `None` for #70's reason, unchanged. | DR-5; design §5.8 |
| F6 | **The refusal is unconditional — not behind the opt-in flag.** Design §5.8 rules it safe in both configurations: with disambiguation off, eager refuses and flush fires the same intent slightly later; with it on (item 3), flush asks. There is no flag in this feature to be behind. | Must | The diff adds no serialized field, no property and no conditional compilation around the refusal. The behaviour is present in a built player. | DR-5; design §5.8; item 1 requirements §4.1 (corrected) |
| F7 | **Nothing that fires today stops firing (G-6).** The refusal costs latency and nothing else. A deferred commit is still flushed — including on push-to-talk release, which calls `FlushPendingBuffer()` before `CancelPendingCommand()`. | Must | A recogniser-level test drives the medial sibling utterance through the full buffer window and asserts the command fires, with the same intent and slots as today, after `bufferWindow` rather than immediately. A second asserts it also fires when push-to-talk is released instead of the window expiring. | design goal G-6; §5.8 |
| F8 | **The refusal honours the *configured* `minScore`, not a default — and this is a deliberate asymmetry with item 1's warning.** The eager path *observes* a tie that has already happened; item 1's scan had to *predict* one. `TryEagerCommit` is handed the real threshold, so a tie below it is refused by the existing score condition and never reaches the sibling check. No reachability gate is added here. See §4.3. | Must | With `minScore` at the default, a two-element sibling pair scores `0.5`, the existing score condition returns `None`, and the sibling check is not what refused it. With `minScore` lowered to `0.4` — the case item 1's warning is documented as silent about (`KNOWN_LIMITATIONS.md:560`) — the same pair now ties above the gate and the sibling check refuses it. | design §5.8; item 1 architecture §7.4 |
| F9 | **A same-intent tie does not trigger the refusal.** Both patterns dispatch the same command with the same slots, so the wrong-intent harm cannot occur and refusing would buy latency for nothing. The filter reuses item 1's `IsSingleIntent` rather than a second copy of the rule. | Must | A grammar whose sibling set is two phrasings of one intent commits early exactly as today. Exactly one code site tests intent equality for this purpose. | design §7.1 ruling (item 1 architecture); DR-2 |
| F10 | **A non-sibling tie does not trigger the refusal.** §5.3 restricts the *action* to sibling rivals so that authoring errors — two unrelated patterns that happen to tie — stay out of the runtime path. | Must | A grammar with two same-length, same-scoring patterns that are **not** siblings (differing at two positions, or at a slot) ties at selection and still commits early. ⚠️ **Amended 2026-08-16** — this row originally also required such a tie to be "recorded on the flush path", quoting §5.3's *"while remaining visible to the editor diagnostic"*. The build does not do that, and the review found why: **canon contradicts itself here**. See §4.8. | design §5.3, §9 row 2 |
| F11 | **A tie between two members carrying the *same* discriminating value is not a sibling tie.** Those two patterns are duplicates of each other, which item 1's F8 puts out of scope: an authoring error, reported by the warning, not disambiguated at runtime. The pair test is therefore "co-members of one set **and** different values". | Must | The #90 fixture (`a:["mode","on"]`, `b:["mode","on"]`, `c:["mode","off"]`) yields a sibling tie for `a↔c` and for `b↔c`, and **not** for `a↔b`. | item 1 requirements F8; issue #90 |
| F12 | **#90 — duplicate-valued members are retained, not dropped.** A set names every pattern carrying the hazard. Today the later member is discarded, so `b` above is never named and never sees the refusal; in the sharper variant the survivors are left sharing one intent and the same-intent filter then suppresses the set entirely. The emission gate becomes "≥2 distinct values among the members" rather than "one member per value". | Must | The #90 fixture produces **one** set with three members and two distinct values. The warning names all three intents. `SiblingSets_DemoGrammar_VolumeAndOrderAreStable` is re-baselined only if the demo grammar's counts genuinely move, with the movement explained. Issue #90 closes on merge. | issue #90; design §5.1 |
| F13 | **The runtime lookup is keyed by `(commandIdx, patternIdx)` and computed once per parser, not per parse.** §5.1 specifies exactly this key; item 1 carried `CommandIndex`/`PatternIndex` on `SiblingMember` for it. Selection never compares patterns during parsing. | Must | Parsing the same utterance N times calls `FindSiblingSets` once. A stopwatch test shows per-parse cost unchanged within noise against the pre-feature baseline. | design §5.1 |
| F14 | **The flush loop records the tied sibling rival, Editor-only.** Design §9 calls this inert here and consumed only by the editor diagnostic; it is built so item 3 inherits a working recording rather than adding one under a behaviour change. Gated so a built player pays nothing for a record nothing reads. | Must | An Editor-only test parses the medial sibling utterance and reads back which rival tied the winner. A player build contains no such field or write. No flush-path behaviour changes: the same command fires with the same score and slots. | design §9 row 2; §5.3; human ruling 2026-08-16 |
| F15 | **The `:1088` invariant is restated in the source comment, not silently broken.** Old wording: *"the eager verdict always names the pattern the subsequent flush will fire."* New: *"the eager verdict never names a pattern the flush would not fire."* A refusal names nothing, exactly as #70's condition already does. | Must | The comment above the shared comparator carries the new wording and says why. A reviewer reading it against DR-5 finds the invariant addressed rather than contradicted. | design §2.3, §5.8 |
| F16 | **The warning's remedy is corrected.** *"Diverge earlier"* does not work: `weapons mode` / `navigation mode` ties identically to `switch to weapons` / `switch to navigation`. Only differing in **more than one element** removes the tie, which is what `Documentation~/command-recognition.md` already says. The message is brought into line with the docs. | Must | The message no longer says "diverge earlier" and names a remedy that actually removes the tie. The affected message test is amended, and the wording matches the product docs rather than inventing a third phrasing. | human ruling 2026-08-16; `Documentation~/command-recognition.md` |
| F17 | **No public API change.** No public type, member, serialized field or event added, removed, renamed or re-typed. The three-state result type and the lookup are `internal`. | Must | The diff adds no `public` member and no `[SerializeField]`. The recogniser inspector has the same field count. | design §9 row 2 (item 3 owns the flag) |
| F18 | **The design's §7 item 2 measurement lands before the PR opens, and the numbers go in the PR body.** (The design writes this citation "§7.2"; §7 is a flat numbered list with no subsections, so "item 2" is what is meant.) Three figures: the **absence** of change on non-sibling grammars over the 699-utterance corpus, the **eager-timing delta** on sibling-shaped utterances, and the **runtime cost** of the lookup. The first two are the two A/B directions; the third is a cost measurement that rides along because F13 needs it. | Must | (a) 699-corpus A/B: zero difference in fired intents, slots and scores. (b) On sibling-shaped utterances with the discriminator elided, covering **both** trailing and medial: how many buffers that committed early now defer, and that the fired intent is unchanged in each. (c) Runtime cost of the lookup, absolute, per F13. All three in the PR body at open. | design §7.2, §7 preamble; human ruling 2026-08-16 |

### 4.1 F1 — what "bit-identical" has to mean, and why it is checkable by inspection

The current comparator is a chain of early returns ending at a strict `>`:

```csharp
if (candidate.Score <= 0f) return false;
if (candidate.MissedRequired > candidate.MatchedRequired) return false;
if (bestScore <= 0f) return true;
if (startIdx != bestStartIdx) return startIdx < bestStartIdx;
if (candidate.Score != bestScore) return candidate.Score > bestScore;
if (candidate.ConsumedEndIdx != bestConsumedEndIdx) return candidate.ConsumedEndIdx > bestConsumedEndIdx;
return candidate.LiteralCount > bestLiteralCount;
```

Only the **last** line loses information. Every earlier line either refuses outright or resolves a strict inequality, so it already knows which side won; only the final `>` collapses "equal" and "less" into one `false`. The three-state form therefore splits exactly one branch, and the property to hold is stated as an equivalence rather than a hope:

> `Compare(...) == Better` **⟺** `IsBetterCandidate(...)` today, for every input.

That is checkable by reading the two functions side by side, and it is what F2's "no edited expected values" acceptance is really testing. Stated here because the tempting shortcut — rewriting the chain as a sequence of comparisons that returns `Tied` by falling through — is easy to get subtly wrong at the `bestScore <= 0f` sentinel, where "no incumbent yet" must yield `Better` and never `Tied`.

### 4.2 F6 — the cost item 1 deferred lands here, and it lands in every build

Item 1's requirements §4.1 originally said the sibling lookup's cost stayed gated "until item 3 ships it behind an opt-in flag", and corrected itself at review: DR-5 is **not** behind the flag, so **item 2 makes every player build compute the lookup**. This feature is where that bill arrives, and it is not treated as a surprise.

Two things follow. The lookup must be built **once per parser** and not per parse (F13) — the parser is rebuilt often in an editor session but parses far more often than it is built. And its cost must be measured in absolute terms rather than as a share of a noisy denominator, which is the lesson item 1 paid for when the same scan read 2.6% and 4.9% of a constructor whose own total had moved by nearly 2×.

Whether the lookup is built eagerly in the constructor or lazily on first use is an architecture question, not a requirement — but the requirement that it be built once, and that a parser which never parses is not penalised for a runtime-only structure, is stated here because it bounds the answer.

### 4.3 F8 — the eager path judges reachability exactly, and the asymmetry is deliberate

Item 1's warning needed three gates beyond DR-1 before it could claim a tie was real: admission (a form with no required element outside the discriminator is refused by DR-7 before any comparison key), `minScore` (a frame worth `D` drops to `(D−1)/D`, so a two-element frame lands at `0.5` and cannot clear the default), and intent (a same-intent set dispatches the same command either way). All three were **predictions**, made at construction, about a tie that had not happened yet — and the `minScore` one had to be made against a **default the constructor cannot see**, which is the documented limitation at `KNOWN_LIMITATIONS.md:560`.

The eager path is in a different position. It is looking at a tie that **has** happened, in local variables, with the real configured threshold in a parameter. So:

- **Admission** needs no gate — the comparator refuses an inadmissible candidate before any key is compared, so it can never be a tied rival (F4).
- **`minScore`** needs no gate — `TryEagerCommit` already refuses when `bestScore < minScore` using the configured value, so a tie that cannot clear the gate never reaches the sibling check (F8). Placing the sibling check after that condition is what buys this, and it is the only ordering requirement this feature imposes on the gate's internals. **This ordering is not inherited from canon.** The locked design constrains placement only as far as §5.8's *"it sits beside that guard rather than modifying it"*, referring to #70's; it fixes no ordinal position among the conditions. The after-`minScore` requirement is derived here, and is stated as ours so a reviewer does not go looking for it in the design.
- **Intent** does carry over (F9), because it is not about whether a tie occurs but about whether the tie is worth acting on, and that answer is the same in both places.

**The consequence is an asymmetry, and it is stated rather than discovered at review: the eager refusal is strictly more accurate than the author-facing warning.** An author who lowers `minScore` below `(D−1)/D` gets no warning about a short sibling pair — and now gets the eager refusal on it anyway. That is the right direction to be wrong in (the runtime protects a grammar the warning failed to flag), and it narrows a documented limitation rather than widening one, but it does mean the warning and the runtime no longer answer the same question. `KNOWN_LIMITATIONS.md:560` needs a sentence saying so in the post-G2 doc pass.

### 4.4 F5/F7 — what "same intent, later" is and is not promising

The eager refusal defers a decision; it does not change one. Two consequences worth separating, because they are easy to conflate at review:

- **When nothing more arrives in the window**, the flush sees the same tokens the eager scan saw, selects with the same comparator, and fires the same first-registered sibling. The intent is unchanged and the only delta is up to `bufferWindow` of latency. This is the case the A/B measures (F18b).
- **When more speech arrives**, the flush sees a longer buffer and may select something else entirely — which is the whole point of refusing. This is a *behaviour* change, not merely a timing one, and it is the change DR-5 wants: the dropped word landing late now has somewhere to land.

Neither case can produce "nothing fires". Verified 2026-08-16: `VoxrPushToTalkController.OnTalkEnded` calls `_commandRecogniser.FlushPendingBuffer()` **before** `CancelPendingCommand()` (`VoxrPushToTalkController.cs:100-103`), so releasing the trigger flushes rather than discards, and the ordinary window expiry flushes by construction. F7 pins both.

### 4.5 F14 — why the flush-side recording is built here despite being inert

Design §9 row 2 puts it in this feature and describes it as *"inert here, consumed only by the editor diagnostic"*. Building an unread field is normally a defect, so the reason it is not one here is recorded rather than assumed:

The design splits items 2 and 3 precisely so that *"a regression [on the hot path shared with the eager gate] is unambiguous"* (§9). Adding the flush-side recording in item 3, alongside the pending machinery and the opt-in flag, would put a change to the shared selection loop inside the feature that also changes what happens when a tie is found — which is the coupling the split exists to avoid. Landing it here, where the flush path's *behaviour* is provably unchanged, means item 3 inherits a recording it can consume rather than one it must also introduce.

It is Editor-only for the same reason item 1's scan was: a built player must not pay for a diagnostic nothing reads. The parser already exposes `LastParseDiagnostics` under `#if UNITY_EDITOR`, read by `VoxrCommandRecogniser` and `VoxrBatchTestRunner`, so there is an existing home and an existing precedent for the gating. Whether the record rides that structure or sits beside it is an architecture question.

**The Editor-only gating is not in the design either.** Canon says the recording is *"inert here, consumed only by the editor diagnostic"* (§9 row 2) and that each loop *"records, alongside the winner, whether an equally-good rival was seen and which pattern it was"* (§5.3) — it never says `#if UNITY_EDITOR`. The gating is chosen here, from item 1's precedent and the non-functional requirement that a player pay nothing for an unread field, and it is compatible with both canon sentences. Stated so it is not mistaken for something the design mandated.

**Ruled by the human, 2026-08-16**, against the alternatives of deferring it to item 3 (contradicts locked canon) and recording it unconditionally (charges every player build for an unread field, which item 1's §4.1 argued against on its own terms).

### 4.6 F11/F12 — #90 is a correctness prerequisite here, not a deferred cleanup

Issue #90 was filed at item 1's review as an under-report in a diagnostic. In this feature it is more:

```
a : ["mode", "on"]     intent A
b : ["mode", "on"]     intent B
c : ["mode", "off"]    intent C
```

`FindSiblingSets` today keeps one member per distinct value, so the emitted set is `{a:"on", c:"off"}` and **`b` is dropped**. Item 2's lookup is built from those members, so `b↔c` — exactly the same hazard as `a↔c` — is not a sibling pair, the eager gate does not refuse, and the wrong intent commits early on the shape this whole design exists to catch. The sharper variant is worse: where the dropped member was the only one carrying a second intent, the survivors share one intent and F9's filter then suppresses the set outright.

**The fix is at two levels, and they are different questions.** The *set* keeps every member and the emission gate becomes "≥2 distinct values" rather than "one member per value" (F12), so nothing carrying the hazard is silently absent. The *pair* test then requires two co-members with **different** values (F11), which is what keeps author-duplicated patterns — item 1's F8 — out of the runtime path. Collapsing these into one rule gets one of the two wrong.

**"Retained" has exactly one exception, and it is not a softening of F12.** Verified from source 2026-08-16: `ExpandOptionals` enumerates `2^optionals` subsets and does **not** deduplicate, so a pattern carrying two identical optional elements — `["a","?x","?x","b"]` — yields the expanded form `["a","?x","b"]` **twice**, from two different subset masks. Both copies key to the same bucket at the same position and add the *same* `(CommandIndex, PatternIndex, Value)` member twice. Today the value gate silently absorbs the second; once that gate stops dropping members, it survives and the warning names one pattern twice. So an **exact-duplicate** member is still dropped. That is a distinct member of the set being removed only in the sense that it was never distinct — every *pattern* carrying the hazard is still named, which is what F12 requires and what issue #90 asks for. Recorded because a reader of F12 alone would take "every member retained" as unqualified.

This is a narrower change than issue #90 proposes: the issue suggests reshaping members into "values with the patterns carrying them", and the two-level treatment achieves the same observable outcome without changing `SiblingSet`'s shape. Recorded because a reviewer holding the issue against the diff will notice the fix is not the one the issue described.

**#91 stays deferred**, ruled by the human 2026-08-16. It is Editor-only, cosmetic, already mitigated by the `"(with its optional elements omitted)"` note, and needs a per-member index this feature has no runtime use for. Item 3 rewrites that same message to add the opt-in flag clause (item 1 requirements §8.1), which is the cheaper moment.

### 4.7 F18 — what the 699-corpus A/B can and cannot show

The rig's standing trap applies and is repeated here because item 1's measurement went wrong twice: **a clean 699-corpus A/B is not evidence a selection change is safe.** The design states the reason as *"the corpus was not built to contain sibling ties, so a null result means 'the corpus is silent', not 'no regression'."*

**That premise is half wrong, and checking it changes the measurement for the better.** Verified 2026-08-16: the corpus is built from word-deletion, prepend, append and concat perturbations of the demo grammar, and its `delete` rows include line 148, `switch to` — the elision of the discriminator from `switch to navigation`, which is the #74 pair itself and the *one* sibling tie item 1 measured as reachable at the default `minScore`. Lines 147, 345 and 346 carry the neighbouring deletions. So the corpus is **not** silent on sibling ties; it contains the reported one.

What it contains is specifically a **trailing** discriminator, which is the shape #70's condition already refuses. That makes the corpus a genuine control rather than a blank: it should show **no** eager-timing delta, because the case it covers is one this feature does not change. A delta appearing there would mean the new condition is firing on shapes it should not.

So the evidence splits three ways rather than two:

- **699-corpus `Parse` A/B** — evidence for F2 only. The flush path is untouched, so `switch to` must still fire `mode_weapons` and every other row must be byte-identical.
- **699-corpus eager replay** — the trailing control. Expect zero verdict changes across all 699 rows, including line 148.
- **Purpose-built medial fixtures** — the only place the delta should appear, and where F5 is actually evidenced. A measurement that skipped the trailing control could not distinguish "the refusal works" from "the refusal fires on everything".

Scope note: design §7 item 1 calls for a purpose-built fixture *set* covering the sibling shapes. This feature builds the cases it needs to measure the eager-timing delta. The corpus-scale fixture work for the disambiguation *flow* — where a follow-up answer has to be recognised — belongs to item 3, which is the item that has a flow to exercise.

**Ruled by the human, 2026-08-16: the measurement runs before the PR opens and its numbers go in the PR body.** Item 1 measured late and it cost three review rounds, twice on numbers that did not reproduce.

### 4.8 The design contradicts itself on what the flush records — ruled, not resolved

Found by the review (2026-08-16), confirmed against canon, and carried to G2 rather than patched over.

Two sentences of the locked design disagree:

- **§5.3:** *"Restricting the **action** to sibling rivals (§5.1) keeps non-sibling ties — authoring errors, not speech ambiguity — out of the runtime paths, **while leaving them visible to the editor diagnostic**."* → the diagnostic records **any** tie.
- **§9 row 2:** *"both selection loops record a tied **sibling** rival"* → the diagnostic records **sibling** ties only.

F10 was drawn from the first, F14 from the second, and they cannot both be satisfied. Neither sentence is a ratified decision record — DR-1 through DR-7 are silent on what the diagnostic stores — so this is two prose sentences drifting apart, not a broken ruling.

**Ruled by the human, 2026-08-16: keep the build as-is — the record stays sibling-only (§9 row 2 and F14), and F10's record clause is withdrawn.** The reasoning: the field is inert in this feature, item 3 is its only real consumer, and it can widen when it has one; widening now would cost a field and rewrite two passing tests to record rivals nothing reads. Reopening the design for a G1 re-lock is disproportionate to two prose sentences about an Editor-only diagnostic.

**Consequence to state at G2:** a non-sibling tie, a same-value tie, and a tie inside a wholly same-intent set all leave `TiedSiblingIntent` null, which is indistinguishable from "nothing tied". No other Editor surface reports two unrelated patterns tying. If item 3 wants the wider record it must widen the field and separate "a rival tied" from "a *sibling* rival tied", not merely relax the gate.

**The opposite direction was briefly true and is now closed (PR #94 review, 2026-08-16).** The first version of the truncated-analysis arm answered "rivals" for any cross-intent tie touching an over-cap pattern, so the record could name a rival that was not a sibling *and* the eager gate could refuse on one — a false positive contradicting F10, in player builds, not merely a diagnostic mismatch. The arm now compares required elements and answers correctly for the duplicate case. The residue is a false *negative* only: a sibling relation existing solely in a mid-expansion of an over-cap pattern is still unseen, in both the gate and the record.

## 5. Non-functional requirements

- **Selection stays allocation-free per parse.** The comparator is called in the innermost loop of both paths, over every `(command, pattern, startIdx)` triple. Neither the three-state result nor the tie recording may allocate on that path — no lists built per candidate, no closures, no boxing of the result. The sibling lookup is a read, not a build (F13).
- **The added runtime cost is measured in absolute terms, not as a share.** Item 1's constructor-cost figure read 2.6% and 4.9% of a denominator that itself moved 3.80 ms → 1.99 ms between runs. The absolute number is the one to quote.
- **Deterministic.** Given the same grammar and the same tokens, the same candidate wins and the same rival is recorded, every run. Nothing may depend on hash iteration order — item 1's `FindSiblingSets` already emits in first-seen order and that guarantee must survive the #90 change.
- **Zero player cost for the Editor-only recording.** Not merely "the field is not read": the write must not execute and the storage must not exist in a built player, which is what `#if UNITY_EDITOR` buys over an `if`.
- **The refusal must not lengthen a wait it was not responsible for.** Returning `None` costs the full `bufferWindow`. Returning `HoldExtendable` would cost the shorter `prefixHoldSeconds` where configured. `None` is what DR-5 specifies and what #70 already returns for the analogous case, so the two conditions stay consistent — but the consequence is a real latency cost on a real utterance and is measured (F18b), not waved through.
- **Verification is EditMode + PlayMode only.** Nothing here touches capture, the native bridge, or MonoBehaviour lifecycle. On-device verification is not required and must not be claimed.

## 6. Non-goals / deferred

- **Asking the speaker anything.** No pending machinery, no `AwaitingDisambiguation`, no choice vocabulary, no timeout semantics (DR-4, DR-6) — backlog item 3. `VoxrPendingCommand` and `VoxrPendingReason` are untouched.
- **The opt-in flag (DR-7).** Item 3. This feature adds no flag, and the eager refusal is deliberately not behind one (F6).
- **Changing what the flush fires.** The flush still selects the first-registered sibling on a tie. Making that answerable is item 3; this feature only makes the tie visible.
- **Any scoring change.** The keys, their order, and the arithmetic are untouched. This is selection's *reporting*, exactly as the design's non-goals state.
- **Redefining "sibling".** DR-1 and `FindSiblingSets` keep their meaning. #90 changes which members survive the emission gate, not what the relation admits.
- **Modifying #70's tail condition or #66's slot condition.** The sibling check sits beside them.
- **A general n-best API.** Rejected for this topic at §6.4.
- **Issue #91.** Deferred to item 3 (§4.6).
- **Product docs.** `Documentation~/`, `CHANGELOG.md` and the `KNOWN_LIMITATIONS.md` updates are written **after** G2 per the workflow. Note that this feature invalidates live text in three places — `command-recognition.md:151` and `:356`, `troubleshooting.md:91`, and `KNOWN_LIMITATIONS.md:527-538`, all of which currently state that the medial case commits early with the wrong sibling — so the post-G2 pass is not optional here.
- **Closing issue #74.** It stays open; item 3 delivers the behaviour it asks for. Issue #90 **does** close on merge (F12).

## 7. Dependencies & assumptions

- **Item 1 (`feat-sibling-set-detection`) is merged.** PR #92 merged 2026-08-15 22:46 UTC; this branch is off `main` at `f482154`. `FindSiblingSets`, `SiblingSet`, `SiblingMember` and `IsSingleIntent` are all `internal` and present.
- **VERIFIED 2026-08-16 — the primitive is already callable from a runtime path.** `FindSiblingSets` is `internal static` and deliberately **not** `[Conditional]`; item 1's architecture §2.1 states that the `[Conditional]` sits on the *caller* precisely so item 2 can call it from selection without moving it. No change to its signature or gating is needed to promote the invocation.
- **VERIFIED 2026-08-16 — `SiblingMember` already carries the runtime key.** `CommandIndex` and `PatternIndex` are on the struct because §5.1 specifies a lookup keyed by `(commandIdx, patternIdx)` and item 1 carried them forward for this feature (architecture §2.2). No shape change is needed for F13.
- **VERIFIED 2026-08-16 — `IsSingleIntent` is `internal`** (`VoxrCommandParser.cs:1182`), made so at item 1's review specifically so a second copy of the intent rule would not drift from the shipped one. F9 reuses it.
- **VERIFIED 2026-08-16 — the eager gate is handed the real threshold.** `TryEagerCommit(string[] tokens, Dictionary<string,float> wordConfidence, float minScore, float minConfidence)` (`VoxrCommandParser.cs:2504`), called from `VoxrCommandRecogniser.ProbeEagerCommit` (`:561-574`) with the recogniser's configured `minScore`. F8 rests on this.
- **VERIFIED 2026-08-16 — deferral cannot swallow a command.** `VoxrPushToTalkController` calls `FlushPendingBuffer()` before `CancelPendingCommand()` on release (`:100-103`), and `VoxrCommandRecogniser` flushes on window expiry (`:464-470`, `:528-529`). F7 rests on this.
- **VERIFIED 2026-08-16 — §2.8 is confirmed, not reasoned.** Item 1 landed `TryEagerCommit_MedialSiblingDiscriminator_CommitsOnAnUndecidableBuffer` and `Parse_MedialSiblingDiscriminator_FiresByRegistrationOrderAlone` (`Tests~/Runtime/VoxrEagerCommitTests.cs:1285-1370`), with the `MedialSiblingParser()` fixture. DR-5's motivation is observed. The corrected score is `3.0/4.0 = 0.75`; the design's `2.0/2.5 = 0.8` was a constant borrowed from a five-element example and is not load-bearing.
- **Unity verification** runs through the host project `VoXR TestGround` (Unity 6000.4.7f1) per the project bindings — **both** EditMode and PlayMode, since most parser and command tests are PlayMode. Delegated to the `compile-check` agent. **MEASURED 2026-08-16 at `f482154`, on a clean tree, before any edit: EditMode 124/124, PlayMode 451/451, both green, no pre-existing failure.** This is the number this feature is judged against. Two figures were in circulation beforehand — item 1's architecture records 444/444 at `d810134`, and 451/451 was reported at `8e20647` — and the fresh run resolves it: the 444 belongs to an earlier commit, before item 1's last two commits added tests. Re-measured rather than inherited, so a later red cannot be argued away as a pre-existing failure.
- **VERIFIED 2026-08-16 — the A/B rig can see this change, and the corpus is not where the design implies.** Unlike item 1's `[Conditional("UNITY_EDITOR")]` scan, which compiled out of the rig entirely and would have produced a false-zero delta, this feature's change is unconditional runtime code and compiles in. The 699-utterance corpus is `Planning~/features/coverage-in-selection/phase7-corpus.tsv` (699 lines exactly), and the rig beside it — `Planning~/features/coverage-in-selection/ab-rig/` — is the one that owns it. `stage.sh` does **not** stage `VoxrCommandRecogniser.cs`, so the rig can drive `Parse` and `TryEagerCommit` directly but **cannot** exercise the buffer window, `prefixHoldSeconds`, or eager-flush wiring. F7's recogniser-level assertions are therefore Unity tests, not rig output, and F18(b)'s delta is measured as a change in `TryEagerCommit`'s **verdict**, not as wall-clock latency.

## 8. Open questions

### 8.1 A/B timing — RESOLVED 2026-08-16

Runs **before** the PR opens; numbers go in the PR body (F18). Item 1 ran its measurement late and it cost three review rounds, twice on figures that did not reproduce.

### 8.2 Reachability at the eager gate — RESOLVED 2026-08-16

The eager path uses the **real configured `minScore`** and adds no reachability gate of its own, because it observes a tie rather than predicting one (§4.3). The resulting asymmetry with item 1's default-relative warning is stated in §4.3 and carried to G2, and needs a sentence in `KNOWN_LIMITATIONS.md:560` at the post-G2 doc pass.

### 8.3 Flush-side recording — RESOLVED 2026-08-16

Built here, Editor-only, per design §9 row 2 (F14, §4.5).

### 8.4 #90 and #91 — RESOLVED 2026-08-16

#90 lands here as a correctness prerequisite (F11, F12, §4.6). #91 stays deferred to item 3.

### 8.5 The warning's remedy — RESOLVED 2026-08-16

*"Diverge earlier"* is wrong and is corrected here (F16). Open sub-question carried to the architecture doc: the replacement wording should match `Documentation~/command-recognition.md` rather than invent a third phrasing, so the exact string is settled against the docs during implementation, not guessed.

### 8.6 Whether a tie recorded at selection is *the* sibling tie — OPEN

The lookup answers "are these two patterns co-members of a sibling set", not "did these two candidates tie *because* the discriminator was dropped". A tie could in principle arise between two patterns that are siblings for some unrelated pair of expanded forms, on tokens that have nothing to do with the discriminator.

Design §5.3 asks only for sibling *rivals*, so set membership is what is specified, and the failure mode is one extra deferral rather than a wrong command. Left open rather than pre-empted: if implementation shows the looser test refusing on shapes that are not really ambiguous, that is worth reporting at G2 and possibly tightening in item 3 — but tightening it speculatively would add a second definition of the hazard beside DR-1's, which is exactly what DR-2 exists to prevent.

### 8.7 The vacuous design-branch step — carried from item 1 §8.4, unresolved

`Planning~/` is gitignored, so design branches in this repo always carry a zero diff. Worth amending the project bindings. Does not block this feature.

## Related

- Locked design: `Planning~/design-docs/sibling-tie-disambiguation.md` §2.1, §2.3, §2.8, §5.3, §5.8, §7.2, §9 row 2, DR-1, DR-2, DR-3, DR-5, §10
- Predecessor: `Planning~/features/sibling-set-detection/{requirements,architecture}.md` — especially requirements §4.1 (the deferred cost, corrected) and §4.5 (the three-layer reachability model), and architecture §7.4 (the reachability gate)
- Origin: issue #74 — stays **open**, closed by item 3
- Closes: issue #90
- Deferred: issue #91 → item 3
- Adjacent, not modified: #42 (`WarnOnDroppableRequiredLiteral`), #66 (the missed-slot condition), #70 (the tail condition), #41 (the span key), #65 (DR-7 admission)
- Successors: item 3 `feat-disambiguation-pending`, item 4 `feat-disambiguation-docs`
