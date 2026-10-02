---
type: architecture
feature: slot-resolver
topic: voice
status: draft
updated: 2026-09-16
sources:
  - Planning~/features/slot-resolver/requirements.md
  - Planning~/research/2026-09-05-next-level/05-rulings.md
  - Planning~/design-docs/leading-miss-bar.md
  - Planning~/anaphora-resolution-analysis.md
  - Runtime/Commands/VoxrCommandRecogniser.cs
  - Runtime/Commands/VoxrCommandParser.cs
  - Runtime/Commands/DynamicSlotManager.cs
  - Runtime/Commands/VoxrCommand.cs
  - Runtime/Commands/PendingCommandHandler.cs
  - Runtime/Commands/VoxrMatchDiagnostics.cs
  - Runtime/Testing/VoxrBatchTestRunner.cs
  - Editor/VoxrDebugSessionLog.cs
  - Editor/VoxrDebugWindow.cs
---

# feat-slot-resolver — architecture

Realizes `Planning~/features/slot-resolver/requirements.md` (#148, ruling 9). Feature lane, G2 only.

**Audited before implementation** (plan-validation pass, 2026-09-16). That audit found one high-severity missed site (§5.5), one that would not have compiled where the plan placed it (§5.1), one unaddressed definition divergence (§5.4), and falsified the first draft's organizing rule (§2). Everything below is the post-audit plan; §13 records what the audit confirmed against source so it is not re-derived.

## 1. Purpose and responsibilities

**A game-supplied answer to one question, asked at one moment: "this command is ready to fire except for slot `X` — do you know what `X` is?"**

The package owns the question and the moment. The game owns the answer. Nothing is remembered between questions — that is not an optimisation, it is the definition ruling 9 adopted, and it is why a resolver cannot aim a missile at a stale target.

Three responsibilities, and nothing else:

1. **Hold the resolvers.** One delegate per slot name, registered and unregistered like a slot value provider, consulted like nothing else in the package.
2. **Ask at exactly one moment, uniformly.** Between "the parser produced a candidate that would fire if it were complete" and "the completeness ruling is taken" — at *every* site that takes that ruling, not the convenient one.
3. **Carry the answer forward visibly.** The filled value reaches the handler as an ordinary slot; the fact that it was filled, and the reason, reach anyone who asks — the `VoxrCommand` at runtime, the session log and debug window in the Editor.

**What it explicitly does not own:** which value is right (the game), whether the round may fire at all (the bar, upstream and untouched), and what the score was (the parser, unchanged — a slot nobody spoke earns no score).

## 2. The one rule that organizes everything

The first draft of this doc said *"a resolver is offered only a command that would fire if it were complete"*, and derived D-5 and D-6 from it. The audit falsified it: resolution **is** offered to candidates that will fail the confidence gate (`:772`, `:1042`) and the debounce gate (`:1055-1056`), and a below-`minScore` candidate *can* fire — through the pending it enters, by `Complete` or by a `FireAsIs` timeout (`PendingCommandHandler.cs:378`). The conclusions survived; the reason did not. The true rule is narrower and stronger:

> **Resolution changes exactly one thing: the completeness answer. It may never move a command across any other gate, in either direction.**

Everything falls out of it:

- **The bar.** Resolution cannot unbar a round, because a barred round constructs no `VoxrCommand` at all — `VoxrCommandParser.cs:2846-2847` `continue`s before the `new VoxrCommand(...)` at `:2865`, and `TryEagerCommit` returns `None` at `:4721-4722`. There is nothing to offer. **This is what satisfies F9/F10, and it is a fact about the parser, not a gate this feature adds** (D-4).
- **~~Score and confidence.~~ Untouched by the copy-with (F19), so no candidate moves across those gates in either direction.** **This argument is wrong, and PR #159's review caught it (F-3).** It reasons about *carried values* — and it is true about them: `WithResolvedSlots` copies `Score` and `Confidence` unchanged, so no gate ever measures a different number. The defect is about **which branch becomes reachable**. Step 7's pending branch is `if (cmd.Score < minScore || incomplete)` and it `continue`s; the confidence and debounce gates sit *below* it. An incomplete candidate therefore never reaches them at all. Resolve it and the branch is skipped, the candidate falls to a gate it was never measured against, and it is dropped with `anyThresholdFiltered` set — which suppresses `OnUnrecognisedSpeech` too. The speaker who would have been *prompted* gets **silence**. A value being carried through unchanged says nothing about whether the gate that reads it is now on the path.
- **The fresh-parse gate needed three floors, not one (D-5).** Each is the same movement across a different gate, and each is unguarded for the same reason the argument above missed: the gate sits below the branch resolution removes.
  - **`minScore`.** Resolving a below-`minScore` candidate moves it from *pending* to *rejected* — which F13 forbids. The audit traced it line by line.
  - **`minConfidence`.** Above `minScore`, incomplete, `AllowPartialMatch`, below `minConfidence`: *prompted* → *silent*.
  - **The debounce.** The same shape one gate later, for a resolved command whose intent is on cooldown.
- **`Score > 0` on the follow-up path (D-6).** Without it, resolving a non-positive fill moves it from *re-armed* to *refused*.
- **Rivals in a sibling tie are not resolved at all (D-19).** The bar's structural guarantee above covers the **round winner** only; a tied rival is built with `LeadingRequiredMissed` deliberately not carried (#126, "recorded, not gated"), so resolving one *can* supply the anchor the bar refused — the one movement the rule forbids that the parser cannot prevent for us.
- **Optional slots.** Never a reason a command fails to fire, so never resolved.

## 3. The sites this touches

| # | Site | Change |
|---|---|---|
| S1 | `Runtime/Commands/VoxrSlotResolution.cs` | **New file.** Public `VoxrSlotResolution` and `VoxrResolvedSlot`. |
| S2 | `Runtime/Commands/DynamicSlotManager.cs:12-33` | The resolver registry, beside the provider registry. Header block updated. |
| S3 | `Runtime/Commands/VoxrCommandRecogniser.cs:330-338` | `RegisterSlotResolver` / `UnregisterSlotResolver`, under the existing section comment at `:328`. |
| S4 | `Runtime/Commands/VoxrCommand.cs:33-135` | `ResolvedSlots` field (+ one assignment in the existing constructor body at `:49-59`), `GetSlotResolutionReason`, and an `internal` copy-with beside `WithScore` (`:66-70`). |
| S5 | `VoxrCommandRecogniser.cs` — new private members | `ResolveOnce`, `TryResolveMissingSlots`, the per-utterance cache, the scratch lists, the effective-command buffer. |
| S6 | `VoxrCommandRecogniser.cs`, **immediately after `:760-761`** | **New Step 3b** — the resolution pass. See §5.1: it must sit *after* the `parser`/`resultBuf` snapshot, not at `:722`. |
| S7 | `:767-780` (Step 4) | Reads the effective command. |
| S8 | `:783-895` (Step 5) | Resolves the follow-up fill; the branch reads one local instead of `followUpResult.Value`. **11 references across 10 lines**, of which `:797` becomes the declaration and 10 are substituted — `:797, 833, 839, 840, 841, 844, 858, 863, 883×2, 884`. `:864` is *not* one of them (`_pending.Current.Value.Definition`). *(The first draft said 12, double-counting `:884`, where `minScore`/`minConfidence` are bare locals. Corrected from the implementation's mechanical count.)* |
| S9 | `:972, :990` (Step 7) | Reads the effective command. |
| S10 | `:1074` → `TryBuildAmbiguity` (`:1226`, `:1239`) | **The winner comes from the caller, not from a re-read of the pooled buffer.** See §5.5 — without this the feature fires incomplete commands. *(The originally planned second half — "and each rival is offered resolution" — was withdrawn by PR #159's F-2; see D-19. Rivals are built and offered exactly as before this feature.)* |
| S11 | `Runtime/Commands/VoxrMatchDiagnostics.cs:122-142` | `VoxrDiagnosticSlotMatch.ResolutionReason`, as an **optional trailing constructor parameter** (the `barred` precedent) — three existing tests use the 5-arg form. |
| S12 | `:1412-1461` (`BuildAttempt`), `:880-890` (the follow-up accept diagnostics) | Emits the resolved slots. |
| S13 | `Editor/VoxrDebugSessionLog.cs:30-86`, `:215-283`, `:341-348` | `SlotDto.resolvedReason`, the copy in `BuildEntry`, the `Readme` prose. |
| S14 | `Editor/VoxrDebugWindow.cs:459-467` | Marks a resolver-filled slot (F24). `:361-364` renders a pending command's slots and needs no change — it reads values, not spans. |
| S15 | `Runtime/Testing/VoxrBatchTestRunner.cs:191-210` | **Comment only** (F25, D-13). The harness's verdict diverges from the runtime after this change and it cannot be fixed here. |

**Untouched, deliberately:** `VoxrCommandParser` (no signature, no behaviour — only two existing `internal static` helpers are *called*, `ExtractSlotName` at `:4350` and `IsOptionalSlot` at `:4362`); `IsIncomplete` at `:1314` and `HasUnfilledRequiredSlot` at `VoxrCommandParser.cs:4387`; the grammar; `NativeBridge~/`.

**`PendingCommandHandler` was on that list and no longer is — scope growth, recorded rather than quietly absorbed.** The PR review found that `TryFollowUpSlotFill` rebuilds its merged command through the *public* constructor, silently dropping `ResolvedSlots` (review F-17; the same drop `WithScore`'s comment says the copy-with exists to prevent for `_registeredSlotNames`). Closing it meant copying only the matched prefix of the pending's slots, re-appending the resolved tail before `ScoreFollowUp`, and returning through `WithResolvedSlots`. **Unreachable today** — a partial-match pending never carries resolved slots, and the confirmation and disambiguation pendings both return at the method's first guard — so the change is behaviour-neutral now and the existing pending suite is what proves it. It was taken anyway because the failure mode is silent: the values survive in `Slots` while the provenance is lost, so the command still fires while `GetSlotResolutionReason`, the debug window and the session log all report a resolver-filled slot as spoken, and `Slots.Length - ResolvedSlots.Length` begins miscounting.

## 4. Key types

### 4.1 `VoxrSlotResolutionRequest` and `VoxrSlotResolution` — the question and the answer

**The question.** *(Added by PR #159's F-5; the shipped delegate took no parameter — D-21.)*

```
public readonly struct VoxrSlotResolutionRequest
    string SlotName             // the unfilled required slot; never null
    string Intent               // the intent of the command that would fire
    int    MatchedPatternIndex  // which of that command's patterns matched
    VoxrSlotResolutionRequest(string slotName, string intent, int matchedPatternIndex)
```

Every field is read straight off the candidate `VoxrCommand` the recogniser already holds, so building a request computes nothing — which is the whole admission price, since this runs per distinct question per utterance on the main thread inside the recognition callback. `MatchedPatternIndex` is always a real index here: `TryResolveMissingSlots`' three guards (§5.2) refuse a command whose index cannot address a pattern, so the `-1` that field carries elsewhere never reaches a resolver.

**What is deliberately absent, and why.** The raw transcript (`VoxrCommand.RawText`) is equally free to supply and is still left out. The three fields above *are* the memo key (§5.3), and that identity is load-bearing: a field a resolver can read but the key cannot see is a field that can be served an answer given for a different question. `RawText` is effectively constant across one utterance's candidates, so keying on it would buy nothing and leaving it out of the key would reintroduce in miniature the staleness F-5 exists to remove. `Score`/`Confidence` are excluded on the same test plus a second one: they vary per candidate, so they would multiply the call count, and §2 says resolution is not evidence — a resolver that reads them is being invited to treat them as such. A resolver that wants the words rather than the slot is doing the parser's job.

**The answer.** A value or nothing, plus a reason. `HasValue` is **derived** from the value rather than stored, so `default(VoxrSlotResolution)` is *none* and a resolution carrying an empty string is also *none* — F4 and F15 fall out of one line instead of two code paths that can disagree (D-7).

```
public readonly struct VoxrSlotResolution
    string Value            // null or empty => not a resolution
    string Reason           // short, opaque to the package, for crew readback
    bool   HasValue         // => !string.IsNullOrEmpty(Value)
    VoxrSlotResolution(string value, string reason)
    static VoxrSlotResolution None    // => default
```

No `Resolved(...)` factory beside the constructor: one way to build one.

### 4.2 `VoxrResolvedSlot` — the record of an answer

`(Name, Reason)`. The *value* is not repeated here — it is in `VoxrCommand.Slots`, where a handler reads it without knowing or caring how it got there (F18).

### 4.3 `VoxrCommand` — the additive change

```
public readonly VoxrResolvedSlot[] ResolvedSlots   // Array.Empty on every constructed value
public string GetSlotResolutionReason(string name) // null when the slot was not resolver-filled
internal VoxrCommand WithResolvedSlots(VoxrSlotMatch[] slots, VoxrResolvedSlot[] resolved)
```

**The public constructor's *signature* does not change**; its body gains one assignment, because C# requires definite assignment of a public readonly field. That distinction is the whole of D-1: `WithResolvedSlots` follows the `WithScore` precedent at `VoxrCommand.cs:66-70` — an `internal` copy-with that carries every field including the private `_registeredSlotNames` no caller can reach — so **all four construction sites stay unedited** (`VoxrCommand.cs:68`, `VoxrCommandParser.cs:2865` and `:3164`, `PendingCommandHandler.cs:246`; the audit confirmed there are exactly four in the tree, `Samples~` and `Tests~` included).

*"`Array.Empty` by default"* holds for every **constructed** `VoxrCommand`, not for `default(VoxrCommand)` — which shipped code does produce, at `VoxrCommand.cs:155`. `Slots` already carries the identical latent shape and is dereferenced unguarded at `:1435`, so this adds no new hazard and gets no new guard.

One accessor, not two. `GetSlotResolutionReason` answers "which" and "why" in a single call because a null `Reason` is **normalised to the empty string** when recorded (D-8) — so non-null means resolver-filled, always, and `string.Empty` means "filled, reason unstated". Without that normalisation the single-accessor design would be a lie and the type would need `IsSlotResolved` too.

**Append-last is an invariant, not an implementation detail.** Resolved slots are appended after the parser's matched slots, never interleaved, because `BuildAttempt` (`:1435-1448`) index-matches `cmd.Slots[s]` against `parseDiag[index].SlotStartWords[s]`. Interleaving would silently mislabel every diagnostic slot after the first resolved one. §7 relies on the invariant to recover the matched count exactly.

The `#if DEBUG` typo warning in `GetSlot` (`:94-113`) stays inert for a resolver-filled name: `FindSlotIndex` searches `Slots`, finds it, and returns at `:91-92` before the warning is reached. Confirmed by the audit.

## 5. Runtime flow

```
HandleResult / FlushBuffer
  └─ ProcessParsedResultsCore
       Step 1   confirm / cancel                         — untouched
       Step 2   TryFollowUpSlotFill                      — untouched
       Step 3   ParseInternal                            — untouched (the bar runs in here)
                parser/resultBuf snapshot (:760-761)     — untouched
    ▸  Step 3b  resolution pass over the result buffer   — NEW, after the snapshot
       Step 4   hasCompleteNewCommand                    — reads effective[i]
       Step 5   follow-up vs new command                 — resolves followUp, then as before
       Step 6   no results                               — untouched
       Step 7   per-candidate accept                     — reads effective[i]
         └─ TryBuildAmbiguity                            — takes the effective winner; rivals unresolved (D-19)
```

### 5.1 Step 3b — the resolution pass, and where it must sit

The whole of the uniformity guarantee (F11) is here: **resolution happens once, in one place, and the downstream sites read the result.** They cannot disagree, because there is nothing for them to disagree about (D-2).

```
clear the per-utterance resolution cache            ← before the early-out, always
if (!_slotManager.HasResolvers || resultCount == 0) -> effective = null; done
for i in 0..resultCount:
    cmd = resultBuf[i].Command
    if cmd.Score < minScore                                          -> effective[i] = cmd   ┐ D-5's
    if cmd.Confidence >= 0 && cmd.Confidence < minConfidence          -> effective[i] = cmd   ├ three
    if commandCooldown > 0 && IsOnCooldown(cmd.Intent, now, cooldown) -> effective[i] = cmd   ┘ floors
    effective[i] = TryResolveMissingSlots(cmd, def-from-lookup, out var r) ? r : cmd
```

**The clear precedes the early-out, and that ordering is load-bearing.** Step 5 is reachable with `resultCount == 0` — a follow-up fill on an utterance the parser produced nothing for — and it resolves against the same cache. Clearing after the early-out would leave the previous utterance's answers live on exactly that path, which is cross-utterance memory: the one thing ruling 9 forbids outright.

**It sits after the `var parser = _parser; var resultBuf = parser.ResultBuffer;` snapshot at `:760-761`, not at `:722`** (D-14). Two reasons, and the second is the real one:

1. The pass reads `resultBuf`, which does not exist as a local before `:761`.
2. `:754-760` explains why that snapshot exists: a subscriber may answer an event by calling `Configure()` (setting `_parser` to null) or `SetActiveSets` (installing differently-sized buffers). **Step 3b runs game code — the resolver — and today no game code whatsoever runs between `:722` and `:760`.** Putting the first such call inside the unsnapshotted window would add a live NRE path at `:761`. Behind the snapshot it inherits the existing protection instead.

A resolver that synchronously calls `Configure`, `SetActiveSets` or `InjectText` is **unsupported**, exactly as an event handler that does is (`:762-766`), and for the same reason: the pooled arrays are the parser's, not copies. The first draft called this "the same pre-existing caveat"; it is the same *caveat* only once the step sits behind the same snapshot, which is why the placement is a decision and not a detail. The contract goes in the product docs beside "must be cheap".

`effective` is a recogniser-owned array grown on demand and reused. A null `effective` means "no resolvers registered" and the read sites fall back to `resultBuf[i].Command` — one branch, no copy, F14's bit-identical path.

### 5.2 `TryResolveMissingSlots` — all-or-nothing

```
TryResolveMissingSlots(in VoxrCommand cmd, VoxrCommandDefinition def, out VoxrCommand resolved)

if no resolvers                                     -> false
if def.Patterns == null                             -> false   ┐ all three of
if cmd.MatchedPatternIndex < 0                      -> false   ├ HasUnfilledRequiredSlot's
if cmd.MatchedPatternIndex >= def.Patterns.Length   -> false   ┘ guards (:4393-4399)
for each element of def.Patterns[cmd.MatchedPatternIndex]:
    name = ExtractSlotName(element)
    if name != null && !IsOptionalSlot(element) && !cmd.HasSlot(name):
        res = ResolveOnce(name)
        if !res.HasValue                            -> false   ← ALL-OR-NOTHING, F8
        record (name, res.Value, res.Reason ?? "")
if nothing was recorded                             -> false   (the command was already complete)
materialise and return cmd.WithResolvedSlots(...)
```

The definition is a **parameter**, not looked up inside. That is what lets the follow-up path resolve against the definition that will actually fire the command (§5.4, D-12), and it keeps the fresh-parse path honest: it passes the definition `_setManager.TryLookupCommand` returns, which is the one `IsIncomplete` reads (`:1316-1317`).

All three guards are copied from `HasUnfilledRequiredSlot` (`VoxrCommandParser.cs:4393-4399`), not two. `MatchedPatternIndex == -1` is legitimately reachable — it is the public constructor's default (`VoxrCommand.cs:50`) and `PendingCommandHandler.cs:253` forwards it — and omitting the `Patterns == null` guard would NRE on the length test. The walk then mirrors that method element for element, using the same two helpers, so the set of slots offered to a resolver is *exactly* the set that makes `IsIncomplete` true.

**Nothing is materialised until every slot has resolved** — the scratch lists are recogniser fields, cleared and reused. So the unresolved path allocates nothing (NFR), and the resolved path allocates two small arrays on its way to a public event, which is where `PendingCommandHandler` and `CopySiblingRivalSlots` already allocate for the same reason.

### 5.3 `ResolveOnce` — one question per distinct ask per utterance

A `Dictionary<(string, string, int), VoxrSlotResolution>` cleared at the top of Step 3b, keyed by **slot name + asking `Intent` + `MatchedPatternIndex`** — every field of the request the resolver receives (§4.1). *(Widened by PR #159's F-5 from `Dictionary<string, …>`; D-21.)* The key is a value tuple, so a warm hit allocates nothing, and its default comparer compares the two strings with `string.Equals`, i.e. ordinally, which is what the `StringComparer.Ordinal` it replaced asked for.

Slot names are **global** — `VoxrCommandParser._slotNames` (`:234`) is built once from the flat `VoxrSlotDefinition[]` registry (`:502-506`), and `{track}` in any pattern of any command binds to that one entry — so two candidates missing `track` are missing the *same* slot. That is exactly why the slot alone is **not** a sufficient key: the same slot asked about on behalf of two different intents is two different questions, and memoising on the slot silently reused the first intent's answer for the second — the hazard the request exists to let a game refuse. Same slot, same intent, same pattern: one answer, reused.

The cache **bounds** the call count at one per distinct (slot, intent, pattern) triple; it does not make the count a pure function of the utterance, because §5.2's all-or-nothing early exit stops asking a candidate's remaining slots. F9's `count == 0` assertion does not rest on the cache — it rests on the structural fact (D-4) — so nothing is lost. The first draft claimed determinism here and was wrong.

An exception from a game's resolver propagates through it uncaught (F20, D-9).

### 5.4 Step 5 — the follow-up path

```
var followUp = followUpResult.Value;
bool followUpIncomplete = IsIncomplete(followUp);
if (followUpIncomplete
    && followUp.Score > 0f
    && TryResolveMissingSlots(followUp, _pending.Current.Value.Definition, out var resolved)
    && !IsIncomplete(resolved))                       // ← D-12
{
    followUp = resolved;
    followUpIncomplete = false;
}
```

and the rest of the branch reads `followUp` where it read `followUpResult.Value` — **11 references across 10 lines, through `:884`**, which is past the `InterpretResolution` call and outside the range the first draft gave. The declaration consumes the first, so 10 are substituted; making `followUp` the only name in scope after the split is what makes a missed reference impossible rather than merely unlikely. `:864`'s `_pending.Current.Value.Definition` is *not* one of them; it reads the pending's definition, which resolution does not touch.

**`Score > 0f` is load-bearing, not tidiness** (D-6). This path has no `minScore` gate by design (#77, #113); its own fire-floor is `Score <= 0`, checked *below* the completeness split at `:833`. Resolving a non-positive fill would convert today's "re-arm the pending and keep the progress" into "refuse and report unrecognised" — the exact stall the `:824-834` comment says that placement exists to prevent. The audit confirmed the floor is exactly complementary: a resolution is permitted iff the refusal cannot subsequently trigger.

**The fill is discarded outright if the pending is gone** (D-15). Immediately after Step 3b, a `followUpResult` whose pending no longer exists is set to null. This is not defensive coding for an impossible case: Step 3b runs a game's resolver, `CancelPendingCommand()` and `Configure()` both clear the pending, and every read in this branch — the D-12 definition read *and* the shipped `Complete(...)` one at `:864` — assumes it is still there. Discarding at the premise fixes both; guarding one read would fix neither honestly.

**One guard was not enough, and the reason the first draft thought it was, was false** (D-15, amended by PR #159's F-1). The Step 3b guard's own comment claimed Step 3b is *"the first game code this method has ever run between Step 2's fill and Step 5's use of it"*. It is not — **this feature's own Step 5 resolution attempt is game code too**, and it sits *below* that guard. Worse, it is routinely the utterance's **first** resolver invocation: Step 3b resolves nothing when `resultCount == 0` (exactly the follow-up-on-an-empty-parse shape §5.1's cache-clear ordering exists for) and nothing more once its all-or-nothing pass returns at the first unresolvable slot. So a resolver that calls `CancelPendingCommand()` or `Configure()` from *this* call left `_pending.Current` empty with the upstream guard already behind it, and **both** arms of the `followUpRes` ternary — `AdvanceSlotFill`'s `_pendingCommand.Value` and the `Complete(followUp, _pending.Current.Value.Definition, …)` read — dereferenced an empty `Nullable` and threw `InvalidOperationException` out of the recognition callback.

Fixed the same way D-15 fixed the first one: **at the premise.** The remainder of Step 5 — from the `Score <= 0` floor down to the `return` — now sits inside `if (_pending.HasPending)`, re-established immediately after the resolution attempt. It is not guarded read by read, because every read there is a read *of the exchange this fill belongs to*, and with the pending gone none of them has anything to answer.

**What the branch does instead is fall out of Step 5, not return.** `hasCompleteNewCommand` is false by this branch's own entry condition, so the preemption cancel below it is a no-op, and the utterance simply continues as the plain fresh utterance it now is:

- **With results**, Step 7 accepts, pends or rejects the candidates on their own merits, exactly as it would have had no pending ever been live. That is the right answer by construction — the pending is gone, so there is nothing left for the follow-up arbitration to arbitrate *against*.
- **With no results**, Step 6 reports the utterance unrecognised. This is the half worth stating explicitly, because returning early would have been the easy choice and it is the wrong one: the game's own resolver cancelled the exchange, the speaker still spoke, and a silent return hands them no answer of any kind — the same "no command, no pending, no prompt" failure F-3 is about, arrived at from the other direction.

**The second `IsIncomplete` is not belt-and-braces** (D-12). `:806-813` documents, and calls reachable, a shape where `ScoreFollowUp` and `IsIncomplete` resolve one intent to *different* definitions under duplicate registration — "the short definition then calls the command complete while the long one charges it for required slots the matched pattern never had". Resolving against the pending's definition (which `Complete` fires under, `:862-866`) while `IsIncomplete` reads the lookup's would let a resolution flip `followUpIncomplete` false while the other definition still has unfilled required slots. Requiring both to agree discards the resolution rather than firing a command one of them calls incomplete — all-or-nothing, extended from slots to definitions.

### 5.5 Step 7's disambiguation arm — the site the first draft missed

`TryBuildAmbiguity` is called with the **index** (`:1074`), and re-reads the winner from the pooled buffer at `:1226`. `choices[0]` is therefore the **unresolved** command while the pending's own `Command` is the resolved one (`:1091-1102`), and `PendingCommandHandler.cs:156-160` fires `pending.Choices[i]` on an answer — **not** `pending.Command`.

Unfixed, this fires a command missing its required slot the moment a resolver-filled command meets a sibling tie and the speaker answers with the winner's own value: the exact shape #73 refuses, and it falsifies the load-bearing comment at `PendingCommandHandler.cs:142-146` ("*the chosen alternative is not re-tested for completeness. Issue #73's gate proved the WINNER complete*") — after this feature the winner is proved complete only in its *resolved* form.

The first draft filed this at §12 as "not reachable today". It becomes reachable in the same PR that introduces it. Two changes, both in `TryBuildAmbiguity` (D-11):

- **The winner is passed in.** `TryBuildAmbiguity` takes the effective `cmd` and uses it for `choiceBuf.Add(winner)` at `:1239` instead of re-reading `parser.ResultBuffer[i].Command` at `:1226`. The intent lookup at `:1227` is unaffected — resolution does not change `Intent`.
- **~~Each rival is offered resolution too.~~ Withdrawn — rivals are never resolved (D-19).** The first form of this bullet argued that a rival built by `BuildSiblingRivalCommand` is a different pattern that may miss the same slot, so offering it the same all-or-nothing pass "keeps the choice list internally consistent". PR #159's review (F-2) showed what that consistency would cost. `VoxrCommandParser.cs:~3036` records that `LeadingRequiredMissed` is **deliberately not** carried onto a tied-rival record (#126, ruled "recorded, not gated") and states outright: *"Do not read this as 'a leading-missed candidate can never fire': it can, here, by being chosen."* A barred rival therefore reaches `TryBuildAmbiguity` as an ordinary `VoxrCommand` with no flag on it, and resolving it could fill **the very anchor the bar refused** — leaving an accepted transcript that does not contain the command's first required element, which ruling 3 requires. #126 ruled it acceptable for **the speaker** to supply a missing anchor *by answering the question*; it did not licence the game to supply it from state. The winner's exemption (D-4) is structural and does not extend here: the winner had to survive the bar to exist at all, and a rival did not. The cost is accepted: a rival that misses a slot the winner had resolved is offered unresolved, exactly as before this feature, and an incomplete choice routes through the completeness machinery it always did.

## 6. Why the resolution is uniform across all three `IsIncomplete` sites

This is the decision the handoff reserved, and requirements §9.1 names the fork. Recording the argument rather than the conclusion, because the conclusion is cheap to reverse and the argument is not.

**Sites 4 and 7 cannot disagree — that one is forced.** Both read the same `resultBuf[i].Command`. Step 7 firing a resolved command while Step 4 calls it incomplete means `hasCompleteNewCommand` stays false, so a live pending is *not* cancelled at `:896-897` and a follow-up fill may preempt the very command that then fires. The `:736-745` comment installed that flag precisely to keep these two answers in step; splitting them re-opens the bug it closed, mirrored. The audit confirmed this trace.

**Site 5 is the real choice.** The case for excluding it: on the follow-up path the speaker is mid-way through completing the command themselves, and a resolver firing there can only mean game state changed between two utterances. The case for including it, which wins:

- The split is **invisible**. The same utterance, with the same resolvers and grammar, produces a different outcome depending on whether a pending happens to be live. Nothing in the suite, the session log or the debug window would show it, and the first report arrives as "sometimes it fills the target, sometimes it asks".
- The split is **not what the speaker experiences**. A follow-up that fills one of two missing slots re-arms the pending and asks again. If the game knows the second slot, asking is the wrong behaviour whether or not the first answer arrived by speech.
- Excluding it **buys nothing measurable**. `ResolveOnce` caps the cost at one call per distinct (slot, intent, pattern) ask per utterance (§5.3), so site 5 costs a dictionary hit whenever it repeats an ask already put.

The alternative is kept on record at D-3; the maintainer can overrule it at G2 by deleting one condition from §5.4.

## 7. Observability

`VoxrDiagnosticSlotMatch` gains `ResolutionReason` — null for a spoken slot, non-null for a resolver-filled one. The same shape `Barred` took: one field whose presence *is* the distinction, plus prose in the exported log's own `readme`. It is an **optional trailing constructor parameter**, as `barred` was (`:1456`), because `Tests~/Editor/VoxrDebugSessionLogTests.cs:27` and `Tests~/Editor/VoxrMatchDiagnosticsTests.cs:96,:104` use the 5-arg form and §10's rule forbids editing an existing test to keep it green.

`BuildAttempt` (`:1412-1461`) changes in one way that matters. It currently sizes the diagnostic slot array by `Math.Min(cmd.Slots.Length, SlotStartWords.Length)`. Those two are equal today — `diagStartWords` is allocated at exactly `bestSlotCount` (`VoxrCommandParser.cs:2906-2912`) — so appending resolved slots would *happen* to keep working. It will instead compute the matched count explicitly:

```
int matchedCount = cmd.Slots.Length - cmd.ResolvedSlots.Length;   // append-last invariant, §4.3
```

then fill `matchedCount` parser-matched entries followed by `cmd.ResolvedSlots.Length` resolved ones, the latter carrying `StartWord = EndWord = -1` and `Confidence = -1f` — no word span, because no word was spoken. Relying on the incidental equality would make a diagnostics mislabelling the consequence of an unrelated future change to `diagStartWords`.

**Resolved entries are appended outside *both* guards** — the inner `cmd.Slots.Length > 0 && SlotStartWords != null` and the outer `parseDiag != null && index < parseDiag.Length` at `:1428`. `diagSlots` is initialised to `Array.Empty` at `:1426`, before both, so a command whose only slots were resolved needs the append outside both to be logged at all. (The outer guard is never false in practice — `parseDiag.Length == resultCount` — but the first draft answered half the question and the half it skipped is the one that matters.)

**The follow-up path logs separately, and F22 needs it.** `BuildAttempt` has seven call sites, all inside the Step 7 loop; Step 5 constructs `VoxrMatchAttempt` directly at `:836-847` and `:880-890`, passing `null` for slots. So a resolver-*completed follow-up fill* — the case §6 argues hardest to include — would leave no trace at all. `:880-890` (the accept diagnostics) gains a slot array built from `followUp.ResolvedSlots`, spans `-1`. Spoken slots stay absent there, as today; the `readme` says so. `:836-847` is the refusal path and needs nothing — D-6 guarantees nothing resolved on it.

**`JsonUtility` cannot express a null string — it writes `""` — so one field is not enough**, and this doc said it was. A resolver-filled slot whose resolver gave no reason (reachable exactly because D-8 normalises a null `Reason` to `string.Empty`) would serialise identically to a spoken one, leaving the `-1` span as the only discriminator in the exported JSON. That makes F22's "distinguishably" true only by span archaeology. `Barred`, the precedent this section leans on, is a **bool** — so `SlotDto` carries **`resolved` (bool, the discriminator) beside `resolvedReason` (string, possibly empty)**, and the `-1` spans stay as corroboration rather than as the signal. The DTO layer's own `ResolutionReason` null-vs-`""` distinction was already correct and is unchanged. (D-17.)

**The exported log's `SchemaVersion` bumps 3 → 4.** This doc omitted it and should not have: every prior field addition to the export bumped it (`3493256` #28, `09e91cd` #112, `4576ce8` #144), and `SlotDto`'s shape has changed. That retargets `Tests~/Editor/VoxrDebugSessionLogTests.SchemaVersion_IsThree`, which is an **intentional** edit to an existing test — the test exists to pin the version, the version moved, and retargeting it is the correct response rather than a green-keeping workaround. Called out in the PR body as such, since §10's rule otherwise forbids editing an existing test.

`BuildEntry` copies both fields, the `Readme` const describes them, and `VoxrDebugWindow.cs:459-467` marks the slot (F24). All of this is `#if UNITY_EDITOR` or `Editor/`-assembly and elides from player builds unchanged.

## 8. Non-functional realization

- **Zero cost unused.** `HasResolvers` is the `HasProviders` pattern: one null-and-count test at the top of Step 3b, and `effective` stays null. F14's "bit-identical" claim is this branch.
- **Zero steady-state allocation unresolved.** The cache, the scratch lists and the effective buffer are recogniser fields, cleared not reallocated. Nothing is materialised before every slot has resolved (§5.2). Pinned with `Is.Not.AllocatingGCMemory()` — `GC.GetAllocatedBytesForCurrentThread` is inert in this project — and the green must be mutation-verified.
- **Bounded cost resolved.** One resolver call per distinct (slot, intent, pattern) ask per utterance (§5.3) — at most one per surviving candidate per unfilled required slot, not one per slot; two small arrays per resolved command, on a path already licensed to allocate.
- **A resolved command never commits eagerly.** `TryEagerCommit` refuses on `bestHasUnmatchedRequiredTail` (`VoxrCommandParser.cs:4690-4691`), so a command needing resolution always waits the full `bufferWindow`. Latency only, no behaviour change, and it is not worth chasing — but F12's "every downstream gate unchanged" should not be read as "no timing changes".
- **No grammar, vocabulary or native change**, so no rebuild, no `.so`, no device surface, no A/B rig run.
- **Additive public API** (D-1): no existing signature moves.

## 9. Decision register

| # | Decision | Why | Alternative kept on record |
|---|---|---|---|
| D-1 | `VoxrCommand` grows a field plus an `internal` copy-with; the public constructor's **signature** is untouched (its body gains one assignment) | The `WithScore` precedent exists for exactly this, and it keeps all four construction sites unedited — the change cannot break a caller it did not read | An added optional constructor parameter. Source-compatible too, but it forces every construction site to be read and agreed, for no gain |
| D-2 | Resolve **once**, in a pass over the result buffer; the downstream sites read the result | Makes F11 structural. Sites reading the same command cannot disagree if there is nothing to disagree about | Calling the same pure resolve function at each site. Same answer by construction, but it materialises twice per utterance and leaves uniformity as an argument rather than a fact |
| D-3 | The resolver participates at **all three** incomplete-ruling sites, including the follow-up fill | §6 | Fresh-parse only. Defensible — the speaker is mid-exchange — but the split is invisible to every instrument we have |
| D-4 | **No anchor check anywhere.** F9/F10 rest on a barred round constructing no `VoxrCommand` | An explicit check would be a second statement of the bar, able to drift from the real one at `VoxrCommandParser.cs:2846`. The bar is the parser's and stays the parser's | A defensive "is this slot the anchor?" test. Rejected as a duplicate rule; F9's test pins the structural fact instead |
| D-5 | Resolution is gated on **three** floors on the fresh-parse path: `Score >= minScore`, the confidence gate, and the debounce gate — a candidate that would fail any of them is left unresolved and routes exactly as it does today *(amended by PR #159's F-3; shipped with `minScore` alone)* | **Correctness, not economy.** Without the score floor a below-`minScore` incomplete command with `AllowPartialMatch` resolves, `ComputeUnfilledSlots` returns empty, and `:998` falls through to the reject at `:1024-1038` instead of entering pending at `:1010` — F13 forbids it, and the audit traced it. The other two are the identical movement one and two gates later: Step 7's pending branch `continue`s *above* both, so an incomplete candidate is never measured against them; resolved, it skips the branch, meets the gate, and is dropped with `anyThresholdFiltered` set — which also suppresses `OnUnrecognisedSpeech`. Prompted becomes **silent**. §2's "score and confidence are carried through unchanged" did not cover this: it reasons about carried *values*, and the defect is about which *branch* becomes reachable | No score gate. Cheaper to write, and wrong. Guarding only `minScore`, as this shipped: the same defect, twice, one gate further down. Re-checking the gates *after* resolution instead of before: it would have to reconstruct what the unresolved candidate would have done, which is the routing this gate preserves for free |
| D-6 | Resolution is gated on `Score > 0f` on the follow-up path | The same rule (§2) against that path's own fire-floor. Resolving a non-positive fill turns a re-arm into the refusal `:824-834` exists to prevent. Exactly complementary to `:833` | Reusing `minScore` here. Wrong floor: this path deliberately has none |
| D-7 | `HasValue` is derived from `Value`, not stored | F4 (`default` is none) and F15 (empty is none) become one expression instead of two branches that can disagree | A stored `bool`. Then `default` and an empty value need separate handling |
| D-8 | A null `Reason` is normalised to `string.Empty` when recorded | Lets `GetSlotResolutionReason` be the only accessor: non-null ⇒ resolver-filled, always | Keeping null and adding `IsSlotResolved`. Two methods where one will do |
| D-9 | A resolver's exception **propagates** | The `BuildEffectiveSlots` precedent (`DynamicSlotManager.cs:54` calls `provider()` unguarded), and a swallowed exception makes a game's bug invisible — the resolution silently becomes "none" and the command silently goes pending | Catch, log, treat as none. Kinder to a buggy game, and it hides the bug behind behaviour that looks like a working feature |
| D-10 | Resolved slots are **appended last** to `cmd.Slots` | `BuildAttempt` index-matches slots against the parser's word spans (§4.3, §7). Interleaving mislabels every diagnostic slot after the first resolved one | Inserting each at its pattern position. Reads nicer in `Slots`; silently corrupts the session log |
| D-11 | `TryBuildAmbiguity` takes the winner **from the caller** — and **nothing else**. *(Narrowed by PR #159's F-2: the "and offers each rival resolution" half is withdrawn, see D-19.)* | §5.5. Otherwise `choices[0]` is the unresolved command and answering the question fires an incomplete one. That was the actual defect and it stays fixed; the rival half was a second change riding along on the same site, and it turned out to be the one that broke canon | Leaving it and re-testing completeness in `PendingCommandHandler`. That moves the fix into the machinery this feature promised not to touch, and re-opens a decision `:142-146` settled |
| D-12 | The follow-up resolution requires **both** definitions to agree before it is taken | `:806-813` documents one intent resolving to two definitions as reachable. Resolving against the firing definition while `IsIncomplete` reads the other fires a command one of them calls incomplete | Resolving against the lookup definition only. Then the resolution is keyed to a pattern `Complete` will not fire |
| D-13 | `VoxrBatchTestRunner`'s divergence is **documented, not fixed** | It builds its own parser with no recogniser, so it has no registry to consult; wiring one through is a second feature. The honest move is to name the limitation where an author will hit it | Threading a resolver registry into the harness. Real value, wrong branch — a follow-up issue |
| D-14 | Step 3b sits **after** the `:760-761` snapshot | It introduces the first game-code call between the parse and the snapshot; in front of the snapshot that is a live NRE path when a resolver calls `Configure` | At `:722`, right after the parse. Reads better in the step numbering; does not compile, and would be wrong if it did |
| D-15 | A follow-up fill is **discarded at the premise** when a resolver cancelled the pending it was built against, rather than guarded at each read | Step 3b is the first game code ever to run between Step 2's fill and Step 5's use of it, and `CancelPendingCommand`/`Configure` both clear `_pending`. The fill merged *into* that pending, so without it there is nothing to complete or re-arm. Guarding only the new definition read would leave the shipped `Complete(...)` read still dereferencing an empty `Nullable` — a safe new path beside a crashing old one, the invisible split this feature exists to avoid | Declaring a resolver that touches the recogniser unsupported, as `:762-766` does for event subscribers. Consistent, but an NRE is a worse failure than the buffer staleness that caveat covers, and "cancel the pending and ask the crew" is a plausible thing for a game's resolver to do |
| D-16 | `_effectiveBuf` is cleared at the end of the utterance | The `_acceptedBuf` precedent, for the same reason: it retains `VoxrCommand` references and their slot arrays until the next utterance would otherwise overwrite them | Leaving it. The plan said "grown on demand and reused" and did not rule on clearing; the implementation flagged the gap rather than guessing |
| D-17 | The session log carries a **bool** `resolved` plus the reason string, not the reason string alone | `JsonUtility` writes `""` for null, so a reason-less resolution is indistinguishable from a spoken slot by the string alone. `Barred` — the precedent this design cited — is itself a bool | One string field, as this doc originally specified. Works at the DTO layer and fails at the JSON layer, which is the layer F22 is about |
| D-18 | `BuildAttempt` keeps its `Math.Min` floor, now over the explicitly derived `matchedCount` | The doc's point was that the count must be derived rather than inferred from an incidental equality; `matchedCount` does that. Dropping the floor too would turn a future length disagreement into an `IndexOutOfRangeException` thrown from inside the diagnostics — taking down a session log rather than reporting one | The bare form, as this doc said. Marginally simpler, strictly more fragile |
| D-19 | A **tied sibling rival is never offered resolution** — `TryBuildAmbiguity` builds it and adds it to the choice list exactly as it did before this feature *(added by PR #159's F-2; narrows D-11)* | D-4's exemption is structural and covers the **round winner** only: the winner exists because it survived the bar, so a resolver can never be handed its pattern's first required element. A rival carries no such guarantee — `VoxrCommandParser.cs:~3036` records that `LeadingRequiredMissed` is deliberately *not* carried onto a tied-rival record (#126, "recorded, not gated") and says in as many words that a leading-missed candidate *can* fire here, by being chosen. Resolving one would let **game state** supply the anchor the bar refused, so the accepted transcript would not contain the command's first required element — which ruling 3 requires. #126 ruled it acceptable for **the speaker** to supply that anchor by *answering the question*, never for the game to supply it from state | Offering the rival the same all-or-nothing pass, as D-11 originally did. It buys choice-list consistency — a rival that misses the slot the winner had resolved is offered unresolved — at the price of a command that can fire on words nobody said. An incomplete choice is a question answered badly and routes through the completeness machinery; a resolved barred rival is the one outcome the bar exists to prevent. Also considered and rejected: carrying `LeadingRequiredMissed` onto the rival record so rivals could be resolved selectively. That re-opens #126's ruling from inside this feature, on a branch that is not a design branch |
| D-20 | Step 5's own resolution attempt gets its **own** premise guard: the remainder of the branch sits inside `if (_pending.HasPending)`, re-established immediately after the attempt, and falls out of Step 5 rather than returning when the pending is gone *(added by PR #159's F-1; extends D-15)* | D-15's guard sits above Step 5 and cannot cover a call below it. Step 5's attempt is routinely the utterance's **first** resolver invocation — Step 3b resolves nothing at `resultCount == 0`, and stops at the first unresolvable slot otherwise — so a resolver that calls `CancelPendingCommand()`/`Configure()` from it left both arms of the `followUpRes` ternary dereferencing an empty `Nullable`, throwing `InvalidOperationException` out of the recognition callback. Guarded at the premise for D-15's reason: with the pending gone the fill has nothing to complete *or* re-arm, so picking one read to guard only picks which meaningless outcome to produce. Falling out rather than returning is what keeps the utterance answered — Step 7 treats it as the fresh utterance it now is, and with no results Step 6 reports it unrecognised instead of returning in silence | Extending D-15's guard to re-check after Step 5's attempt from up at `:886`. Impossible: the call has not happened yet there. Returning instead of falling through: silence after the game's own resolver cancelled the exchange, and it would discard live parse results that have nothing to do with the pending |
| D-21 | The resolver is handed a **`VoxrSlotResolutionRequest`** (slot, asking `Intent`, `MatchedPatternIndex`), and `ResolveOnce`'s memo key widens to that same triple *(added by PR #159's F-5)* | Slot names are global, so a resolver registered for `track` is consulted on `self destruct target {track}` as readily as on a harmless command, and the shipped `resolver()` gave it nothing to refuse with — the only fences were register/unregister timing and renaming the slot (requirements E-1). Keying stays **per-slot**: ruling 9 fixed that shape, and only the *ask* gains context. The key had to widen with it or the change would be a lie — two intents asking about the same slot would still share one answer, which is the precise reuse a game is now being given the means to refuse. The three fields are the key, so nothing a resolver can read can disagree with the answer it gets | Leaving it context-free, with register/unregister timing as the only fence. It makes the game's safety depend on getting a registration window exactly right around every utterance, across a code path the game does not control the timing of, and it fails silently and destructively when the window is wrong. Also rejected: keying the **registry** per (slot, intent) — that is the per-command hook ruling 9 excluded, and it forces a game to enumerate every intent that uses a slot at registration time. Also rejected: putting `RawText` in the request — §4.1 |

## 10. Build plan

Each phase is compile-clean and independently verifiable. `compile-check` runs **both** platforms after every phase. Baseline to beat: **EditMode 186, PlayMode 638** — counts rise, nothing pre-existing goes red, and no existing test is edited to stay green.

| phase | work | requirements | verify |
|---|---|---|---|
| 1 | **Surface, no behaviour.** S1, S2, S3, S4 | F1-F3, F6, and the surface halves of F4/F5/F21 | Both platforms. **Plus `Samples~` compiled out-of-band with `dotnet`** — the Unity run does not compile it, so F6's stated check is otherwise unrun. New tests, limited to what is observable with nothing reading the registry yet: the three null-arg throws, unregister-not-registered returns `false`, register-then-unregister returns `true`, registering for a slot name absent from the grammar throws nothing, and `default(VoxrSlotResolution).HasValue` / an empty-valued one are both false. **The register-overwrite and no-rebuild-needed checks are behaviour and move to Phase 2** — the first draft of this row listed them here, where they cannot be written. Every existing test passes **unedited** |
| 2 | **The ruling.** S5, S6 (Step 3b behind the snapshot), S7, S9, S8 (the 12-reference substitution), S10 (the ambiguity fix) | F7-F20 | Both platforms. #148's (a)-(d) as four named tests, plus: all-or-nothing; at-most-once-per-utterance; score and confidence unchanged; `RequiresConfirmation` still asks; empty value is not a resolution; a throwing resolver propagates (F20); register→unregister→speak restores pre-feature behaviour (F2's second half); a resolvable bare command with a pending live matches one with none (F11's first half); follow-up completion (F11's second half); **the sibling-tie test (F12) — answer a tie on a resolver-filled command and assert the fired command carries the slot**; and **the D-5 regression guard** — a below-`minScore` incomplete command with `AllowPartialMatch` still enters pending with its slots unfilled. The barred test asserts an invocation **count of 0** |
| 3 | **Observability.** S11, S12, S13, S14, S15 | F22-F25 | **Both** platforms (not EditMode alone — S12 touches the runtime recogniser). A session-log test shows a resolver-filled slot carrying its reason and a spoken slot carrying none; a second covers the follow-up path's own attempt. The three existing `VoxrDiagnosticSlotMatch` call sites compile **unedited** |
| 4 | **Close-out.** Reconcile §11 to as-built; PR; `review-pr` at the full profile; G2 | — | Product docs only *after* the G2 ruling — and swept for the **outcome** ("a command missing a required slot does not fire"), not for the word "resolver"; `KNOWN_LIMITATIONS.md` gains F25's entry |

**Risk order.** Phase 1 is inert by construction — it ships types and a registry nothing reads, so it cannot change a speaker-visible outcome, which is why its verify demands every behaviour test pass unedited. Phase 2 is the only phase that can break shipped behaviour and is sequenced after the surface it depends on exists, so its diff is behaviour and nothing else. Phase 3 touches Editor-gated code plus one runtime diagnostics block.

**Mutation-verify the green.** Three tests can pass for the wrong reason, and all three shapes have burned this repo before: the barred test (F9) passes if the resolver is never reached *for any reason*; the at-most-once test (F17) passes if resolution never runs at all; the allocation test passes because the instrument is inert. Each must be shown to go red when the mechanism it pins is broken, and the mutation evidence goes in the PR body — it dies with the working tree otherwise.

**No editor-only assets, no scene wiring, no prefab.** Nothing here needs the Editor except the test run itself.

## 11. As-built deltas

*(Filled as phases land; completed before G2.)*

### Phase 3

- **§7 got the export layer wrong, and S13 was silent on the schema version.** Two corrections, both found during implementation:
  - **"One field whose presence *is* the distinction" does not survive export.** That is the right shape for `VoxrDiagnosticSlotMatch.ResolutionReason` and it is built that way — null for spoken, non-null for resolver-filled. But `JsonUtility` cannot express a null string: it writes `""`. Because D-8 normalises a null `Reason` to `string.Empty`, a resolver-filled slot whose resolver stated no reason serialises identically to a spoken one, leaving the `-1` span as the only discriminator in the exported JSON — F22's "distinguishably" true by span archaeology alone. `SlotDto` therefore carries **two** fields where the struct carries one: `resolved` (bool, the discriminator, the `barred` precedent followed properly) and `resolvedReason` (the prose, which may legitimately be empty). The `-1` spans stay as corroboration. **The struct layer is unchanged** — it was already correct.
  - **The exported log's `schemaVersion` goes 3 → 4.** §7/S13 never mentioned it. `git log -G "SchemaVersion = [0-9]"` shows the precedent is unbroken: #28 → 1, #112 → 2, #144 → 3, one bump per field addition to the exported shape. Two slot fields are added here, so 3 became a lie to consumers.
- **One existing test edited, deliberately and with explicit authorisation:** `Tests~/Editor/VoxrDebugSessionLogTests.cs` `SchemaVersion_IsThree` → `SchemaVersion_IsFour`, expected value and message retargeted to #148. This is the one test whose *purpose* is to pin the version; the version legitimately moved, and re-pointing it is the response the test was written to force, not a test weakened to accommodate a change. Every other existing test — including the three 5-arg `VoxrDiagnosticSlotMatch` construction sites §10's Phase 3 row names — is unedited.
- **`BuildAttempt` keeps its `Math.Min` floor**, as `Math.Min(matchedCount, SlotStartWords.Length)`. §7's point was that the *count* must be derived explicitly rather than inferred from an incidental equality, and `matchedCount` does that; the floor is then a guard that keeps a future length disagreement a silent clip, as it has always been, rather than an `IndexOutOfRangeException` thrown from inside the diagnostics — which would take down a session log rather than report one.
- **The `-1`-span entries are built by one shared helper**, `BuildResolvedSlotDiagnostics`, used by both `BuildAttempt` and the follow-up accept path, so the two sites cannot come to describe the same slot differently. It returns `Array.Empty` when nothing resolved, so the follow-up path allocates nothing new on an ordinary utterance.
- **`Planning~/features/coverage-in-selection/ab-rig/stage.sh` repaired.** Its `SOURCES` list did not include `Runtime/Commands/VoxrSlotResolution.cs`, so after Phase 1 the rig staged a `VoxrCommand.cs` whose `VoxrResolvedSlot[]` field had no defining type. Added; staging re-run and verified.
- **Deferred to Phase 4 (product docs, post-G2):** `Documentation~/editor-testing.md:68-72` states the schema version as `3` and enumerates the per-slot fields; `CHANGELOG.md` needs the `[Unreleased]` entry naming `resolved`, `resolvedReason` and the `3` → `4` bump (additive, so tooling written against `3` still reads the file). `KNOWN_LIMITATIONS.md` gains F25's entry as already planned.
- **Line numbers in §3 and §7 are stale for `VoxrCommandRecogniser.cs` by roughly +307** (Phases 1 and 2), and by +166 inside the Step 5 `#if UNITY_EDITOR` blocks. The other four files did not move. Re-verify before citing.

### Addendum 2026-10-03 — resolver-slot-exemption (#161)

Later work supersedes four passages, left as written (this feature's record of its own build). Decisions: `../resolver-slot-exemption/architecture.md`.

- **§1 "does not own" (the parser, unchanged — a slot nobody spoke earns no score) and §3 "Untouched, deliberately: `VoxrCommandParser`"** no longer hold. Under A5 (DR-8, DR-9) a missed required slot with a registered resolver costs 0 raw / 0 den in the parser (still counted missed elsewhere), and fewer exempted slots ranks right after score. The parser reads the registry through a read-only seam, `IRegisteredSlotNames` (implemented by `DynamicSlotManager`), snapshotted once per pass. A registered resolver now changes the score; resolution itself still does not (F5).
- **§9 D-13 ("documented, not fixed"), §3 row S15 (comment only) and the §12 runner risk** are superseded for a runner given names: `VoxrBatchTestRunner` takes `registeredSlotNames` (last parameter, both constructors), scores as the runtime does, and reports `would ask resolver for '…'` where registered slots are the only gap. A runner given none behaves as before.

## 12. Risks

- **The Step 5 substitution is mechanical and wide.** 11 references across 10 lines, through `:884`, inside a block whose comments carry more reasoning than its code. A missed reference compiles and runs, and produces a follow-up that resolves for the completeness split but fires the unresolved command. Mitigated by making `followUp` the only name in scope after the split — deleting the substitution's source rather than trusting the sweep.
- **`GetSlotResolutionReason` returning `string.Empty` is a contract, not an accident** (D-8). A later change that stops normalising null would make it silently under-report. The product docs must state it and a test must pin it.
- **The append-last invariant (D-10) is enforced by nothing but the code that appends.** Pinned by the Phase 3 session-log test, the only place it is observable.
- **The global slot-name scope** (requirements E-1) is the feature's sharpest edge. A resolver on `track` still makes `{track}` resolvable on `self destruct target {track}` too — the *registry* is per-slot and stays that way under ruling 9. What D-21 added is the means to refuse: the ask carries the asking `Intent`, so the fence is now one `if` inside the resolver rather than register/unregister timing. It is opt-in, so the product docs must teach it; a resolver that ignores its request is exactly as wide as before.
- **`VoxrBatchTestRunner` now disagrees with the runtime** (D-13, F25) and only a comment and a `KNOWN_LIMITATIONS` entry stand between that and an author trusting a `FAIL`.

## 13. What the pre-implementation audit confirmed against source

Recorded so it is not re-derived, and so a reviewer can see what was checked rather than argued.

**Confirmed:** `IsIncomplete` has exactly three call sites (`:770`, `:797`, `:990`). `new VoxrCommand(` has exactly four sites tree-wide. `:1074`/`:1226` is the **only** index-based re-read downstream of `:972` — every other read flows from that single assignment. D-5's regression traced line by line, and its converse (the only pending behaviour that changes is the intended F7 one). D-6's floor exactly complementary to `:833`. `_registeredSlotNames` reachable from an instance copy-with; the field set complete; the `#if DEBUG` typo warning inert for resolved names. `matchedCount` exact at all seven `BuildAttempt` sites, and a resolved command cannot reach `:1016` or `:1028` at all. `diagStartWords` allocated at exactly `bestSlotCount`. Slot names global, so the `ResolveOnce` cache is sound. No path reaches Step 5 without passing Step 3b. The bar's structural claim (D-4) confirmed to the line. `DynamicSlotManager`'s unguarded `provider()` at `:54` (D-9's precedent).

**Not verified, and not claimable:** the EditMode 186 / PlayMode 638 baseline (needs a Unity run — `compile-check`'s job); the allocation NFR (no instrument run; §8 names the instrument); `Documentation~/` was not swept (Phase 4); the `Tests~/` suite was not audited beyond the three `VoxrDiagnosticSlotMatch` construction sites, so other tests asserting on `VoxrCommand.ToString()` or debug-window rendering may still surface in Phase 1.

## Related

`Planning~/features/slot-resolver/requirements.md` · `Planning~/research/2026-09-05-next-level/05-rulings.md` (rulings 3, 9) · `Planning~/design-docs/leading-miss-bar.md` · `Planning~/features/disambiguation-pending/architecture.md` · `Planning~/anaphora-resolution-analysis.md` (integration point only — its Phase 1 is superseded)
