---
type: requirements
feature: slot-resolver
topic: voice
status: draft
updated: 2026-09-16
sources: [Planning~/research/2026-09-05-next-level/05-rulings.md, Planning~/anaphora-resolution-analysis.md, Planning~/design-docs/leading-miss-bar.md, Planning~/handoff-issue-148.md, https://github.com/jinwoo1601/VoXR-Speech-Recognition/issues/148]
---

# feat-slot-resolver — requirements

Item R36a of the 2026-09-06 rulings (`05-rulings.md` §9), tracked at **#148**. Feature lane, **design track skipped**: ruling 9 is the lock, and a design doc now would restate it. G2 only.

## 1. What this feature is

**A hook that lets the game fill a required slot the speaker omitted.** "Launch missiles" can mean `launch missiles target ⟨1721⟩` when the game knows there is one obvious target — designated by the player, implicitly highlighted through interception, or the only hostile on the map. That knowledge lives in the game, so the package ships the hook and never the rule.

Ruling 9's definition, which is the whole scope fence:

> ellipsis is **game-state resolution of an omitted required slot**, not "reuse the last-mentioned target".

Three properties of that definition are load-bearing below, each for a different requirement:

- **No memory in the package.** The package keeps no history of what was said, targeted or filled. Every resolution is a fresh question put to the game at the moment of parsing. This is what makes a stale answer impossible: there is nothing to go stale.
- ~~**Resolution is not evidence.** A resolver-filled slot was never spoken. Nothing about the transcript changed, so nothing about the *score* may change (F20) and nothing about the **bar** may soften (F9, F10).~~
  > **AMENDED 2026-10-02 by `design-resolver-slot-scoring` (scoring-model A5, ratified at G1 2026-10-02).** **Resolution is not evidence.** A resolver-filled slot was never spoken, so it is never credited as matched. The scoring model declines to *charge* for a slot whose name has a registered resolver (scoring-model A5, DR-8), but that is fixed at parse time from registration, not from the resolver's answer; resolving changes nothing about the score (F19) and nothing about the **bar** may soften (F9, F10).
- **The hook is per-slot, context-carrying and synchronous.** `Func<VoxrSlotResolutionRequest, VoxrSlotResolution>`, called on the main thread inside the parse→accept spine. Registration is keyed by slot name (ruling 9's shape); the *ask* names the command asking, so a game can answer one intent and refuse another. It must be cheap because it runs inside the recognition callback.

**The precedent it is shaped after, and is not.** `RegisterSlotValueProvider` (`Runtime/Commands/VoxrCommandRecogniser.cs:330`) is the only existing extension point that takes a delegate from game code, and the new pair should read as its sibling. The contract is different in every other respect: a value *provider* returns a value **set**, runs at parser-rebuild time, needs `NotifySlotChanged()`, and narrows what the parser will **match**. A *resolver* returns one value **or none**, runs per utterance, needs no rebuild, and fills what the parser did **not** match. #148(d) keeps the two orthogonal on purpose.

## 2. Why this is a feature and not a chore

It adds public API (two methods, one new public type, a new member on `VoxrCommand`) and it changes the complete/incomplete ruling on the core accept path — the gate issue #73 installed and #113 and #77 have each amended since. Both halves are exactly what the issue lane excludes.

Player value: the aggressive-order fantasy. A captain says "launch missiles", not "launch missiles target track one seven two one", and today the second form is the only one that fires. The alternative the package can already offer — `allowPartialMatch`, which asks "which target?" — is the right answer when the game genuinely does not know, and the wrong one when it does.

## 3. Non-goals — the scope fence

- **Pronouns and demonstratives** ("it", "that one"), the `{target*}` slot kind, and gaze or pointing fusion. Deferred by ruling 9 to a design branch after the human corpus exists. `anaphora-resolution-analysis.md`'s Phase 2 belongs there, not here.
- **Any last-mentioned memory in the package** — a salience ring buffer, a per-category "last target", time decay. `anaphora-resolution-analysis.md`'s **Phase 1** proposes exactly this and is **superseded** by ruling 9; so is that doc's own definition of ellipsis ("resolves to the last-targeted thing"). Read it for its integration-point reasoning only.
- **Optional slots.** Only *required* slots are ever offered to a resolver; an unfilled optional slot is not a completeness problem and needs no hook.
- **The pattern's first required element.** Absolute, by ruling 3. See F9/F10.
- **Partial resolution.** All-or-nothing, F6.
- **Validation of the resolved value** against the slot's registered values or a provider's active set. #148(d) scopes provider semantics to "grammar and matching"; a resolver bypasses both by construction. The consequence is real and must be documented rather than defended: a game can deliver a value no grammar word could have produced, and the package will pass it through. Stated as a contract in the product docs, not enforced in code.
- **Async or coroutine resolvers.** Synchronous only.
- **Grammar, vocabulary, or native-bridge change.** None. No `.so` rebuild, no desktop harness run, no device surface.

## 4. Requirements

Priority: **Must** = required for G2. **Should** = expected, droppable only by explicit ruling. **Could** = worth doing if cheap.

Acceptance checks run through `InjectText` on a configured `VoxrCommandRecogniser`, per #148 ("Tests entirely through `InjectText`"), unless a row says otherwise.

### 4.1 Public surface

| #  | Requirement | Priority | Acceptance check | Source |
|----|-------------|----------|------------------|--------|
| F1 | `RegisterSlotResolver(string slotName, Func<VoxrSlotResolutionRequest, VoxrSlotResolution> resolver)` registers a resolver for one slot. A second registration for the same slot replaces the first silently, as `Register` does for providers. Each call is handed a `VoxrSlotResolutionRequest` naming the slot, the `Intent` of the command that would fire, and its `MatchedPatternIndex`. *(Signature widened by PR #159's F-5; shipped context-free as `Func<VoxrSlotResolution>` — see E-1.)* | Must | Register twice; the second resolver's value is the one that fills. A resolver that returns a value only for intent `A` fills on `A` and leaves the same slot unfilled on intent `B`. | #148; PR #159 F-5; `VoxrCommandRecogniser.cs:330` |
| F2 | `bool UnregisterSlotResolver(string slotName)` removes a resolver and returns whether one was removed. After it, the slot resolves no longer. | Must | Unregister an unregistered slot → `false`. Register, unregister, speak the bare command → the pre-feature behaviour returns. | #148 |
| F3 | Both methods reject a null `slotName`, and `RegisterSlotResolver` rejects a null resolver, with `ArgumentNullException` — the provider pair's validation, in the same place (the manager, not the facade). | Must | Three `Assert.Throws<ArgumentNullException>` mirroring `VoxrDynamicSlotTests.RegisterSlotValueProvider_Null*_Throws`. | `DynamicSlotManager.cs:17-33` |
| F4 | A public `VoxrSlotResolution` carries a value **or none**, plus a short `Reason` string for crew readback ("main target", "only hostile"). `default(VoxrSlotResolution)` is *none* — a resolver that returns nothing needs no ceremony. | Must | `default` and the explicit "none" form both leave the command incomplete; the resolved form fills it and its `Reason` reaches `VoxrCommand` (F16). | #148; ruling 9 |
| F5 | Registering or unregistering a resolver does **not** rebuild the parser, change the grammar, or require `NotifySlotChanged()`. | Must | Register a resolver mid-session with no `NotifySlotChanged()`; the next `InjectText` resolves. A resolver registered for a slot name that is not in the grammar at all changes nothing and throws nothing. | Ruling 9 ("no grammar change"); #148 |
| F6 | The public API is additive. Existing integrator code — including any call to the public `VoxrCommand` constructor — compiles unchanged. | Must | The package compiles in the host project **and** `Samples~` compiles out-of-band with `dotnet`, both with no edit to an existing call site. A Unity EditMode/PlayMode run does **not** compile `Samples~`, so the Unity run alone does not discharge this. | House rule; `input-level-147-feature` memory |

### 4.2 The ruling

| #   | Requirement | Priority | Acceptance check | Source |
|-----|-------------|----------|------------------|--------|
| F7  | A candidate that is otherwise complete — it passed the score gate and its only defect is one or more unfilled **required** slots of its matched pattern — has each of those slots offered to its registered resolver **before** the incomplete ruling is taken. | Must | A pattern long enough that the omitted slot leaves it above `minScore` (see **E-2** — `launch missiles target {track}` spoken as `"launch missiles"`, #148's own example, scores **0.25** and never reaches a resolver), with a resolver for the slot → `OnCommandRecognised` fires with `GetSlot` returning the resolved value. | #148; E-2 |
| F8  | **All-or-nothing.** The resolutions are applied only if *every* unfilled required slot of the pattern resolves. If any does not, none is applied and the command is exactly the command the parser produced. | Must | A two-slot-short pattern with a resolver for one slot only → pending with **both** slots unfilled, byte-identical to the pre-feature outcome. | Derived from #148 ("Not resolved → today's behaviour") |
| F9  | A resolver is **never consulted for a barred round** — a round whose winner missed its pattern's first required element. | Must | #148(c): a pattern whose first required element is the resolvable slot, spoken without it → nothing fires, and the resolver's invocation count is **0**. | Ruling 3; `leading-miss-bar.md` §5.1 |
| F10 | F9 must hold **structurally** — because a barred round constructs no `VoxrCommand` at all — not because a new gate checks for the anchor and could be bypassed by a later path. | Must | The test in F9 passes with no anchor test anywhere in the resolution code. A reviewer can trace it: `VoxrCommandParser.cs:2789-2848` `continue`s before the construction at `:2865`; `TryEagerCommit` returns `None` at `:4721-4722`. | Ruling 3; DR-8 |
| F11 | The resolution is **uniform across every site that consults the incomplete ruling** — the fresh-parse completeness flag, the follow-up slot-fill path, and the main accept gate. No two sites may see a different completeness answer for the same command. | Must | A resolvable bare command spoken while a pending is live produces the same outcome as one spoken with no pending, save for the pending's own cancellation. A resolver-completable follow-up fill completes rather than re-arming the pending. | Handoff trap 2; `VoxrCommandRecogniser.cs:770`, `:797`, `:990` |
| F12 | A resolved command proceeds through **every** downstream gate unchanged — confidence, debounce, sibling-tie disambiguation, `RequiresConfirmation`. None is bypassed or reordered. | Must | A resolvable bare command on a definition with `RequiresConfirmation` asks for confirmation rather than firing. | #148 ("proceeds through the normal gates") |
| F13 | Unresolved → **today's behaviour exactly**: pending under `allowPartialMatch`, otherwise rejected, with `OnUnrecognisedSpeech` and the pending's `UnfilledSlots` unchanged. | Must | #148(b): resolver returns none → the existing pending/reject tests pass **unedited**. | #148 |
| F14 | A missing slot with no registered resolver simply does not resolve. With **no** resolvers registered at all the recogniser's behaviour is bit-identical to today's and does no additional work. | Must | The whole existing PlayMode suite passes unedited (638). | House rule |
| F15 | A resolution whose value is null or empty is **not** a resolution. | Must | A resolver returning a "resolved" empty string leaves the command incomplete, exactly as `None` does. | Derived from F4 |
| F16 | A slot with both a value provider and a resolver keeps **provider semantics for grammar and matching**. The resolver is not filtered through the provider's active value set, and the provider is not consulted for a resolution. | Must | #148(d): a provider narrowing `track` to `["1721"]` and a resolver returning `"4407"` → the spoken form still refuses `4407`, and the bare form still fills `4407`. | #148(d) |
| F17 | A registered resolver is invoked **at most once per distinct question per utterance** — a question being the slot plus the `Intent` and `MatchedPatternIndex` of the candidate asking. Repeat asks from the same intent and pattern (a second candidate, Step 5's follow-up, a later site) reuse the first answer; a candidate of a *different* intent missing the same slot is a different question and is put separately. *(Widened by PR #159's F-5; shipped as "once per slot per utterance", which silently reused one intent's answer for another and is the hazard F-5 removes.)* | Must | Two candidates of **different** intents both missing `track` → the resolver's invocation count after one `InjectText` is **2**. A repeat ask for `track` from the same intent and pattern adds no call. | Ruling 9 ("must be cheap"); PR #159 F-5; determinism of F9's count assertion |
| F18 | A resolver-filled slot is **indistinguishable from a spoken one** to a handler that does not ask: `GetSlot(name)` returns the resolved value and `HasSlot(name)` returns true. | Must | F7's test reads the value through `GetSlot` alone. | #148 |
| F19 | ~~A resolver-filled slot **does not change the command's `Score` or `Confidence`.** The command passed the score gate on what was actually spoken and keeps that number.~~ **AMENDED 2026-10-02 by `design-resolver-slot-scoring` (scoring-model A5, ratified at G1 2026-10-02).** Resolution **does not change** the command's `Score` or `Confidence`; the score is fixed at parse, where a missed required slot with a registered resolver is exempt from the score (scoring-model A5, DR-8). | Must | ~~The fired command's `Score` equals the score the same utterance produces today with the resolver unregistered (which today lands in pending).~~ The fired command's `Score` equals the score the same utterance produces with the same resolver registered but declining (which lands in pending or is rejected). | §1 ("resolution is not evidence"); `leading-miss-bar.md` §4.1 |
| F20 | An exception thrown by a game's resolver **propagates**; the package neither swallows it nor substitutes a default. This is the provider precedent (`BuildEffectiveSlots` calls `provider()` unguarded) and it is what keeps a game's bug visible. The contract "a resolver must not throw" is stated in the product docs. | Should | A throwing resolver surfaces the exception rather than silently producing a rejected command. | `DynamicSlotManager.cs:54`; house rule |

### 4.3 Observability

| #   | Requirement | Priority | Acceptance check | Source |
|-----|-------------|----------|------------------|--------|
| F21 | `VoxrCommand` exposes **which** slots were resolver-filled and **the reason each resolver gave**. A command with no resolver-filled slots reports an empty set and allocates nothing extra. | Must | F7's command reports `track` filled with reason `"main target"`; a normally-matched command reports none. | #148 |
| F22 | The exported session log records resolver-filled slots **distinguishably**, with the reason, following the precedent of how `barred` and `tiedRival` were added. | Must | An EditMode test over the log DTOs shows a resolver-filled slot carrying its reason, and a spoken slot carrying none. | #148; `VoxrDebugSessionLog.cs:341-348` (`SlotDto`) |
| F23 | The session-log `readme` prose that ships inside every exported log describes the new field, as it already describes `barred` and `runnerUpIntent`. | Should | The exported JSON's `session.readme` names the new field. | `VoxrDebugSessionLog.cs:30-86` |
| F24 | The in-Editor debug window marks a resolver-filled slot too. It renders `attempt.Slots` at `Editor/VoxrDebugWindow.cs:459-467` and is the primary authoring surface; a resolver-filled slot there would otherwise print an unmarked `words[-1..-1]`. | Should | A resolver-filled slot in the debug window reads as filled-by-resolver with its reason. | Same hazard D-10 exists to prevent |
| F25 | The divergence the change opens in `Runtime/Testing/VoxrBatchTestRunner.cs:206` is **documented, not silently left**. That harness calls `HasUnfilledRequiredSlot` directly against its own parser, has no access to the resolver registry, and after this change reports `required slot unfilled` for utterances the runtime fires — the exact inverse of the failure its own comment at `:191-193` says it exists to prevent. | Must | `KNOWN_LIMITATIONS.md` carries the entry (post-G2), and the harness's comment names it. | Plan-validation audit, 2026-09-16 |

## 5. Non-functional requirements

- **Main thread, parse time, synchronous.** The hook runs inside the recognition callback. It is the game's job to keep the resolver cheap and the package's job not to multiply the cost: F17 caps it at one call per *distinct question* per utterance — no longer one per resolver, since F-5 made two intents asking about the same slot two questions. The bound is therefore the number of distinct (slot, intent, pattern) triples that reach the resolution pass, which is at most one per surviving candidate per unfilled required slot. The memo key is a value tuple, so a warm hit still allocates nothing.
- **Zero cost when unused.** With no resolver registered, the resolution step must be a single field test and no allocation — the `HasProviders` precedent.
- **Zero steady-state allocation on the unresolved path.** An utterance where nothing resolves must not allocate. Allocation on the *resolved* path is licensed: the command crosses into a public event, which is already where `PendingCommandHandler` and `CopySiblingRivalSlots` allocate rather than lend a pooled array.
- **No public API removal or signature change.** Additive only (F6).
- **No grammar, vocabulary, native-bridge or build-pipeline change.** Nothing here touches `NativeBridge~/`.
- **Editor-only observability stays Editor-only.** F22/F23 ride the existing `#if UNITY_EDITOR` diagnostics channel and must be elided from player builds exactly as `barred` is.

## 6. Dependencies, assumptions and consequences

**Depends on:** nothing. Ruling 9 records R36a as "S-effort, unblocked... rides on the existing dynamic-slot/pending machinery", and states explicitly that R23 and the timing rebase are **not** prerequisites.

### E-1 — three departures from ruling 9's wording, recorded rather than inherited

Ruling 9's mechanism sentence reads: *"a required slot may be **marked resolvable**; when the transcript leaves it empty, **the parser** asks a game-supplied resolver (`Func<string>`-shaped, per slot) before ruling the command incomplete."* #148 — written later, by the maintainer, and named in the handoff as the requirements source — specifies something different in three places, and **#148 governs**. Recorded here because a plan audit read the ruling's words against this doc and found them silently diverged.

1. **There is no separate "marking".** Registering a resolver for a slot name *is* the marking (F1). A second opt-in flag on `VoxrSlotDefinition` would be a grammar-shaped change ruling 9 also forbids.
2. **`Func<VoxrSlotResolutionRequest, VoxrSlotResolution>`, not `Func<string>`.** The *return* type is forced by the same ruling's own demand that "the resolver reports which rule it used"; a bare string cannot carry the reason, and #148 names `VoxrSlotResolution`. The *parameter* is a later departure again — PR #159's F-5 — and is the subject of the consequence below.
3. **The recogniser asks, not the parser.** #148 places the ask "before the incomplete ruling", and that ruling is `VoxrCommandRecogniser.IsIncomplete`. The parser is not touched.

**The consequence of (1) is the one that bites, and the ask — not the key — is what was changed to blunt it.** Slot names are **global** — `VoxrCommandParser._slotNames` is built once from the flat `VoxrSlotDefinition[]` registry, and `{track}` in any pattern of any command binds to that single entry. So registering a resolver for `track` makes `{track}` resolvable **everywhere it appears**, including on a command where filling it silently is the last thing anyone wants (`self destruct target {track}`).

As shipped, the resolver was invoked as `resolver()` and could not tell which command was asking, so it could not refuse: the only fences were register/unregister timing and renaming the dangerous command's slot. PR #159's F-5 changed the **ask**, not the key. **Keying stays per-slot** — ruling 9 fixed that shape and §9.2 keeps it — but every call now carries a `VoxrSlotResolutionRequest` naming the slot, the `Intent` of the command that would fire, and its `MatchedPatternIndex`. The primary fence is therefore now one `if` inside the resolver, and that is what the product docs must teach; timing and renaming remain available and are no longer the only options. The memo key widened to match the ask (F17), because otherwise the first intent's answer would be handed silently to the second — the exact hazard the context exists to remove.

**Residual hazard, smaller but not nil.** (a) The fence is opt-in: a resolver that ignores its request behaves exactly as the shipped one did, so "resolves everywhere" is still the default for a game that never reads `Intent`. (b) The request describes the command that *would fire*, not what firing it does — a game still has to know which of its own intents are destructive, and a renamed or newly added intent is not caught by anything. (c) Where two definitions are registered under one intent (D-12 records this as reachable), both reach the resolver as the same `Intent` string, and `MatchedPatternIndex` indexes whichever definition the recogniser is resolving against — so the pair cannot be told apart from the request alone. (d) Resolution is offered only to a candidate that also cleared the round's score floor (D-5, E-2), so a resolver is not even asked about most near-miss utterances; that narrows exposure but is a gate, not a fence a game controls.

**Assumption, verified from source rather than inherited — this is what makes F9/F10 true without a new gate.** A barred round `continue`s at `VoxrCommandParser.cs:2846-2847`, *before* the `new VoxrCommand(...)` at `:2865`, so it writes no result and `_resultCount` never advances; `TryEagerCommit` returns `EagerCommitVerdict.None` at `:4721-4722` on the same flag. A barred round therefore reaches neither the result buffer nor — by DR-8, pinned by `LeadingRequiredMiss_OpensNoPending` — a pending, so it can seed no follow-up fill either. There is no `VoxrCommand` for a resolver to be offered. **F9 is a regression test on a structural fact, not a gate.**

**Assumption: "the session log" is the Editor-only export.** There is no runtime-side session log. `Editor/VoxrDebugSessionLog.cs` is the only one, it is Play-Mode-only and in a different assembly from the recogniser, and it is fed by the `#if UNITY_EDITOR` `DiagnosticsPublished` event. F22 therefore lands on the diagnostics structs, not on `VoxrCommand`'s own surface. If #148 meant something else, F22 changes.

**Consequence — the anchor becomes an authoring hazard.** Ruling 3 notes in passing that making a verb optional moves the anchor onto the next required element. This feature makes that sharper: an author who marks a slot resolvable and then finds it never resolves is most likely looking at a pattern where that slot *is* the anchor. The product docs must say so plainly, or the first support question will be exactly this.

## 7. Inherited findings — carry, do not rediscover

- **`anaphora-resolution-analysis.md` is superseded in part and carries no erratum marker.** Its Phase 1 mechanism and its definition of ellipsis are both rejected by ruling 9; the doc still states them as design. Its §"Where in the pipeline does resolution run?" **is** still good — including its own warning that a post-parse fill "breaks the guarantee that a delivered slot value is always one of the slot's registered values", which is precisely the cost §3 accepts under #148(d).
- **Its open question is our F11.** That doc asks "does the resolver run before or after the existing follow-up slot-fill?" and answers "likely after — let an active pending command consume the utterance first", without resolving it. F11 takes a different shape: not an ordering, a *uniformity*. See §9.1.
- **The bar is winner-only, and #126 is closed but its caution stands.** No text this feature writes may generalise to "a leading-missed candidate never fires by any path". F9/F10 are stated about the round winner.
- **Issue text is evidence, not canon.** On the last three features an issue's own stated preconditions did not survive checking. #148's contract has been checked against source here (§6) and held; the session-log clause (§6, second assumption) is the one part that rests on a reading rather than a check.
- **Docs need adversarial checking too** — the `leading-miss-docs` lesson: a behaviour change falsifies docs that never name the changed symbol. The post-G2 sweep is for the *outcome* ("a command missing a required slot does not fire"), not for the word "resolver".
- **A global CSharpier hook reformats every `.cs` edit** and silently reverts manual de-indents. Restructure, do not re-indent.

## 8. Verification

- `compile-check` on **both** platforms after every batch of edits. Baseline to beat: **EditMode 186, PlayMode 638.** New tests raise both counts; no pre-existing test may go red or be edited to stay green (F13, F14 depend on this).
- #148's four acceptance cases (a)-(d) map to F7, F13, F9, F16 and all run through `InjectText`.
- **No device verification is applicable.** No mic path, no MonoBehaviour lifecycle change, no native bridge. Nothing here may be claimed as device-verified, and nothing here needs to be.
- **No A/B rig run.** The grammar does not change, so there is no decoder-facing delta to measure.

### E-2 — the score gate bites before the completeness gate, and #148's own example never fires

**Measured from source, 2026-09-16.** `RequiredSlotMissPenalty = -1.0` (`VoxrCommandParser.cs:143`), `RequiredLiteralMissPenalty = 0` (`:187`), default `minScore = 0.6` (`VoxrCommandRecogniser.cs:31`).

#148's motivating example is `"launch missiles"` against `launch missiles target {track}`. Two elements match, the literal `target` is missed at no cost, `{track}` is missed at `-1`: **(1 + 1 + 0 − 1) / 4 = 0.25**, against a gate of 0.60. It is refused by the *score* gate, and a resolver is never reached. The general rule, for a pattern of N elements where everything but one required slot was spoken: the score is **(N − 2) / N**, so a single omitted required slot clears the default gate only at **N ≥ 5**.

**This is not D-5's doing.** Remove D-5's gate and the candidate still cannot fire — Step 7 refuses on `cmd.Score < minScore` independently of completeness. The constraint is structural: **F19 says a slot nobody spoke earns no score, and the score gate then refuses the command for not having been spoken.** Ellipsis and the score gate pull against each other, and ruling 9 did not foresee it because it framed the completeness ruling as the only obstacle.

#### MEASURED 2026-09-16 — against the real parser, on the real game grammar

Staged from `0d9aceb` into a desktop harness, validated first against the committed 66-claim DocCheck pin, and mutation-verified (moving `RequiredSlotMissPenalty` to `-0.5` moved `"launch missiles"` 0.2500 → 0.3750). Every number below is the parser's, not arithmetic.

**The arithmetic above is confirmed, with one correction: `N` is the count of *required* elements (`D`), not authored ones.** Optional elements drop out of both numerator and denominator, so a 5-element pattern carrying one optional is `D = 4` and scores 0.500, not 0.600. And there are **two** formulas, not one — the second was missing and is the load-bearing one:

> **(a) drop the slot alone: `(D−2)/D`, needs `D ≥ 5`. (b) drop the slot *and* its orphaned literal — the natural spoken form: `(D−3)/D`, needs `D ≥ 8`.**

`VR FTL-Like 3`, `Set_Combat`, 20 live commands, 40 pattern×required-slot occurrences, all three scenes at the shipped `minScore: 0.6`:

| | (a) slot only | (b) natural form |
|---|---|---|
| Resolver reachable | 18/40 (45 %) | **5/40 (12.5 %)** |
| Clears the gate but loses its round to a rival | 5 | 4 |
| Below the gate | 17 | 27 |
| **Barred by the anchor rule** | **0** | **0** |

- **The anchor rule costs this grammar nothing.** Not one authored pattern opens with a required slot. The bar was counted separately on the expectation that it mattered; the bucket is empty. (The mechanism was pinned anyway on a synthetic leading-slot pattern: scores 0.714, `LeadingRequiredMissed`, `Parse` returns nothing.)
- **The aggressive orders — the `{track}` family, 15 occurrences: (a) 7/15, (b) 2/15**, and both survivors are `adjust_range`'s 8-element pattern, which is not an aggressive order. **Under the natural form, 0 of 4 weapon-release occurrences are reachable.** `"launch jackal"` scores **0.250** — the same as #148's example, which is therefore not an unlucky pick.
- **A majority of `{track}` orders needs `minScore ≤ 0.25`** — effectively no score gate. `minScore` is one global `[SerializeField]`, shared with the sibling-tie reachability scan and the eager gate; there is no per-command or per-slot threshold.
- **The grammar already solves this for `{burn_level}` without a resolver.** Where a slot-less sibling was authored (`intercept track {track}` beside `intercept track {track} {burn_level}`), the sibling matches the elided utterance at **1.000** and wins the round outright, so the with-slot pattern never becomes a command and its missing slot is correctly never offered. That idiom costs a second pattern and leaves each handler to do its own fallback — no uniform record of what was filled or why, and no way to *ask* when the game does not know.

**Consequence for the ruling.** The hook is correct, but it is close to **inert for the use case that motivated it**. The structural reason is now measured rather than argued: a resolver needs the slot to be *required*, required-slot omission costs `-1`, and that penalty is proportionally worst on short patterns — which is exactly what aggressive orders are. Option 2 above remains dangerous for the same reason it always was; a **fourth option** the measurement suggests is to exempt a resolver-fillable slot from the *denominator* rather than crediting it as matched (`"launch jackal"` → 0.667, not 0.75) — declining to charge for what the game supplies rather than manufacturing evidence it was spoken.

**Any of options 2-4 is a change to `scoring-model.md`, which is locked and complete — a design branch and a G1 re-lock, not a feature-branch edit.**

**Not measured:** acoustics (whether VOSK emits the elided text at all), the confidence gate (`Parse()` supplies no word confidences, so every "reachable" verdict is score-and-completeness only), and the distribution of shapes (a) vs (b) in real speech — the 699-utterance corpus's `delete` arm is single-*word* deletion, not slot elision, and contains no shape-(b) cases at all. The 45 % / 12.5 % split is a property of the grammar, not of observed speech.

#### RULED 2026-09-16 (maintainer) — option 1: ship as built, document the arithmetic

The hook ships with its measured reachability stated honestly. **The scoring question goes to a design branch**, not this feature branch, because every option that changes it is a change to `scoring-model.md`, which is locked and complete — so it needs G0/G1, not a feature-branch edit. Tracked at **#161**.

What this ruling does *not* do: it does not accept the feature (G2 is separate and still outstanding), and it does not decide the scoring question — only where that question is answered. Option 4 (exempt a resolver-fillable slot from the denominator) is the strongest candidate the measurement surfaced and is recorded in the issue, not chosen here.

**Post-G2 documentation obligation created by this ruling.** The product docs must carry the arithmetic, not a vague caveat: the two formulas, the `D ≥ 5` / `D ≥ 8` thresholds, and plainly that an author's levers are pattern length or a lowered global `minScore`. An author who marks a slot resolvable and finds it never fires is overwhelmingly looking at a pattern too short to clear the gate, and the docs must let them diagnose that without reading the parser.

**The feature still works** — on long patterns, and at a lowered `minScore` — but not on the short aggressive orders the ruling was written for, which is the shape the maintainer's own game note describes. **This needs a ruling before G2**, and it is the one open item that could change what the feature is. Options, with the recommendation first:

1. **Ship as built; document the arithmetic.** The limitation is real but honest, and the author's levers are to lengthen the pattern or lower `minScore`. Recommended, because option 2 is dangerous.
2. **Credit a resolver-filled slot as matched and re-score.** Makes #148's example fire (0.75). It also lets a resolver push a **0.25-scoring utterance** over the gate — a command firing on very little evidence that it was said at all, which is precisely what ruling 3 refused at the anchor and what the maintainer's stated reason ("I don't want commands firing that were not of user's intention") rejects. Not recommended without a design branch.
3. **Waive only the penalty, without crediting a match.** Half-measure: the example reaches 0.50, still under the gate. Buys nothing here.

## 9. Open questions

1. **F11 takes uniformity as a Must — the fork is worth naming.** The alternative is *fresh-parse only*: the resolver fills on a first-utterance candidate but is not consulted when a follow-up slot-fill merges a pending. That reading has a real argument behind it — on the follow-up path the speaker is actively supplying slots, so a resolver firing there can only mean game state changed between two utterances, and the package would be completing a command the speaker was mid-way through completing themselves. The argument for uniformity, which is why it is written as the Must: with the split, the *same utterance* produces a different outcome depending on whether a pending happens to be live, and nothing in the test suite or the session log would show it. If the maintainer prefers the split, F11 and its architecture section change together; this is the decision the handoff explicitly reserved for the architecture doc, and it is recorded here so it is ruled rather than defaulted.
2. **Should a resolution be offered per slot, or per command?** *(Partly answered by PR #159's F-5: the ask now carries `Intent` and `MatchedPatternIndex`, so a game can already answer `target` differently for `launch missiles` than for `scan` — by branching inside one resolver. What stays open is only whether a game would rather **register** per command than branch.)* F7 is written per slot (`Func<VoxrSlotResolutionRequest, VoxrSlotResolution>` keyed by slot name, ruling 9's shape). A per-command hook — "here is an incomplete command, fill it or don't" — would let a game answer "target" differently for `launch missiles` than for `scan`, which is plausibly what an aggressive-orders rule actually wants. Ruling 9 fixed the per-slot shape, so this is not open *for this feature*; it is recorded because the first game-side integration is where it will surface, and a later per-command overload would be additive rather than a break.
3. **Does the `Reason` string need any constraint at all?** It is opaque to the package and exists for crew readback. Left unconstrained — no length cap, no null check beyond F15's on the *value*. A null `Reason` on a resolved slot is legal and reads as "filled, reason unstated". Flagged only so the architecture doc does not invent a validation the requirements did not ask for.

## Related

`Planning~/research/2026-09-05-next-level/05-rulings.md` (rulings 3 and 9) · `Planning~/anaphora-resolution-analysis.md` (integration point only) · `Planning~/design-docs/leading-miss-bar.md` · `Planning~/features/disambiguation-pending/architecture.md` (the pending machinery this sits beside) · `Planning~/handoff-issue-148.md`
