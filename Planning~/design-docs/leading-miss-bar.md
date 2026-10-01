# Design: The Leading-Required-Miss Bar

**Branch:** `design-leading-miss-bar`
**Origin:** issue #124, reported and triaged by the maintainer against v1.5.0. Third comment (`#issuecomment-5432896895`, 2026-08-27) is the current state of knowledge and supersedes the two above it.
**Status:** **G1 LOCKED 2026-08-27.** Rulings in §10. This document is immutable from here; a contradiction found during implementation reopens it on a new design branch and re-locks (see the workflow's "When implementation contradicts the design").

**Scope, agreed at G0 (2026-08-27):** the bar itself and its amendment to `scoring.md` §7 D, plus three carried items — authoring guidance for the grammar-side mitigations, the construction-time warning for the hazardous grammar shape, and the `KNOWN_LIMITATIONS.md` entry for the recoveries the bar costs. Shipping as **2.0.0**, **default-on**, with **no public opt-out**.

> **What's decided going in.** Three rulings were made by the human on 2026-08-27 before this branch existed, and this document designs *within* them rather than re-opening them: (1) the bar is the solution — not documentation, not a cap-per-utterance, not a threshold change, not removing sequential extraction; (2) it is default-ON; (3) it excludes candidates from *selection* rather than suppressing a round winner after the fact.
>
> **§6.1 asked the human to revisit ruling (3), and only ruling (3)** — not on preference, but on measurement taken after it. **It was revisited and reversed at G1: the bar refuses to FIRE, not to compete** (§10, DR-4). Rulings (1) and (2) stand untouched.

---

## 1. Problem

A pattern scores `matched_required / total_required`. A pattern matched from a later token, having missed its leading required element(s), still clears the default `minScore` of 0.60 whenever `j <= 0.4k` — `j` leading elements dropped, `k` required elements. **Every pattern of three or more required elements can therefore fire with its leading verb absent.**

The reported case. A grammar registers:

```
query_time_to_target : ["time", "to", "target"]
intercept_target     : ["intercept", "track", "{track}"]
```

The speaker says **"time to target track one two four four"**. Round 1 fires `query_time_to_target` at 1.0000. Round 2 fires `intercept_target` at 0.6667 with `track = "one two four four"` — **and the word "intercept" was never spoken.** A read-only query executed a maneuver order.

The per-word confidence table shows every token decoded cleanly. Nothing in the transcript, the readback, or the per-command event marks the second command as invented: `OnCommandRecognised` fires once per command, so a subscriber cannot tell the two came from one utterance.

The maintainer's ruling on the class, verbatim: *"a single Utterance firing two commands when the intention was one is an unacceptable phenomenon"*, and *"it will never simply be a known limitation."*

### 1.1 The phenomenon is broader than the issue's title

Two things widen it beyond "a sequential-extraction bug":

- **It fires standalone, in round 1, with no first command present.** Measured: `'track one two four four'` → `intercept_target=0.6667`, `n=1`. At `bufferWindow 2.0`, any pause longer than the window delivers the tail exactly like this. Anything scoped to "round 2" narrows the incident and leaves the class open.
- **It is not confined to the reporter's grammar.** The same session shows `adjust_range :: close to {range} clicks on track {track}` scoring 0.5714 and 0.4000 against stranded tails — two more near-misses of the gate on a different pattern. And the package's own committed fixture corpus contains the shape: the `baseline` row `target hotel one` fires `approach_target` at 0.6667 today.

---

## 2. Verified current state

Every claim below was read from source on this branch (off `main` at `b83b62f`) or measured through the desktop A/B rig, which was validated against the committed grammar pin before any delta was trusted (§7.1).

### 2.1 Selection already carries a count-based admission rule

`CompareCandidate` (`Runtime/Commands/VoxrCommandParser.cs:3030`) refuses a candidate outright before any ranking key is consulted:

```csharp
if (candidate.MissedRequired > candidate.MatchedRequired)
    return CandidateOrder.Worse;                       // :3071
```

Its own comment states the form the rule is meant to take: *"deliberately a COUNT, not a score threshold — no knob, nothing to configure, and independent of the coverage term."* **The bar is a second count beside this one**, in the same method, at the same point in the sequence. That is the whole of the mechanical change.

`MatchResult` (`:2926`) already carries `MatchedRequired` / `MissedRequired` (`:2985`) as a ledger separate from the score. It does not carry which required element was the first, so the bar needs one new latch in `TryMatchScored` (`:3153`) and one new struct field. Both are stack-resident; the parse path stays zero-alloc.

### 2.2 The bar cannot revert issue #82 — mechanically, not merely empirically

This was the load-bearing objection that blocked this direction for two rounds. It is false, and the reason is structural rather than statistical.

`IsAdmissibleStart` (`:3685`) applies its **own** count test to a `forStartProbe` match and never calls `CompareCandidate`:

```csharp
if (probe.Score > 0f && probe.MissedRequired < probe.MatchedRequired)
    return true;
```

`BuildCoverageTables` (`:3522`) consumes that to build `_orphanRun`. So orphan-run **termination** — #82's win, the mechanism that keeps the first command's 1.0000 instead of charging it `2/(2+3) = 0.40` — is reached by a code path the bar does not touch. A bar placed in the comparator is invisible to it.

Note also that the two tests are deliberately one notch apart (`missed > matched` for admission, `missed < matched` for termination), and the bar is a third, orthogonal test: it asks *which* element was missed, not *how many*.

### 2.3 The two coverage terms are asymmetric, and only one has an excusal

This is the finding that reshapes the design, and it was not known when the G0 rulings were made.

Coverage is computed inside `TryMatchScored` as two independent terms (`:3316`):

```csharp
coverage = ( SkippedBefore(searchStart, startIdx)
           + OrphanedAfter(consumedEndIdx, requiredAfterLastMatch > 0) ) * _coverageWeight;
```

- **The trailing term** `OrphanedAfter` (`:3627`) reads `_orphanRun`, which stops at the first **admissible start**. That excusal *is* issue #82.
- **The leading term** `SkippedBefore` (`:3611`) is a plain prefix subtraction with **no excusal at all**:
  ```csharp
  return _recognisedPrefix[startIdx] - _recognisedPrefix[searchStart];
  ```

So leading debris is charged unconditionally to whichever candidate wins the round. **The only reason this has never hurt anyone is that a leading-missed candidate always wins round 1 and consumes the debris**, re-basing `searchStart` past it before the legitimate command is ever scored.

**The phantom is load-bearing.** It is the shield that keeps leading debris off the real command. §6.1 is entirely about what happens when the shield is removed.

### 2.4 The eager path shares the comparator — which cuts both ways

`TryEagerCommit` (`:3940`) runs its own selection scan and calls the same `CompareCandidate` (call site `:4000`). Its own comment already records that this is how the admission rule reaches it: *"the admission rule in CompareCandidate refuses any candidate whose missed required elements outnumber its matched ones… The eager scan inherits it by sharing the comparator."*

So **a rule placed in the comparator reaches the eager path for free, and a rule placed anywhere else does not.** That is the verified fact; what follows from it depends entirely on which fork DR-4 takes, and the G0 open question about the `:4000` call site turns out not to be closable independently of it:

- Under fork B the bar *is* a comparator rule, so the eager path inherits it and there is nothing to decide.
- Under fork A — **the ruling** — the bar lives in `ParseInternal`, so the eager path inherits **nothing**, and a leading-missed candidate would commit early and bypass the bar entirely. It needs its own explicit condition (§5.2, DR-5).

> **⚠ ERRATUM E-1 (recorded 2026-08-27, implementation of item 1; the RULING below is unaffected).** The premise *"an eager commit is a fire"* is **false**, and so is every claim that the eager path "bypasses the bar" or "defeats G-1". `EagerCommitVerdict.Commit` makes the recogniser call `FlushBuffer`, which routes through `ProcessParsedResults` into `ParseInternal` — the method the bar lives in — so a barred winner is refused there and the utterance reports unrecognised either way. **No phantom can fire on the eager path at all.** DR-5's ruled action (an explicit condition in `TryEagerCommit`) still stands, on the true reason: `Commit` consumes and clears the speech buffer, so without the condition a half-spoken command is flushed early and discarded, and its continuation is parsed as a separate utterance. **Fork C, read this before scoping.** Full record: `Planning~/features/leading-miss-bar/architecture.md` §10, "Canon-authority calls".

An eager commit **is** a fire, so under either fork the eager path must be covered. Only the mechanism differs.

### 2.5 Acoustic timing and `[unk]` are both empirically dead

Recorded so neither is re-proposed. Both were falsified against 10 exported field sessions — 73 utterances, 361 words, 288 inter-word boundaries, spanning v1.3.0–v1.5.0.

- **Timing.** One session is a controlled natural experiment: the same phrase spoken 8×, and on one repetition VOSK dropped the in-grammar word `track` (median duration 0.420 s). The residual gap was **0.090 s** — inside the 0.030–0.120 s range of the 7 utterances where nothing was missing. Largest gap anywhere in 288 boundaries: 0.420 s; 217 are exactly 0.000. VOSK absorbs elided audio into neighbouring alignments rather than leaving a hole. The leading word also swallows arbitrary pre-speech silence (observed `hard` durations of 12.120 s and 7.553 s), so "was there audio before the first matched token" is unavailable as a fallback.
- **`[unk]`.** VOSK emits it only for audio *outside* the grammar vocabulary. An in-grammar word it drops leaves no token at all — proven by the entry above. A rule of the form "only fire a leading-miss when `[unk]` evidence says something was spoken there" would reject exactly the §7 D recoveries it was meant to protect.

There is no transcript-level discriminator between *never spoken* and *spoken and lost*. This is now backed by measurement rather than by enumeration, and it is why the design is a **rule about pattern shape**, not a rule about evidence.

### 2.6 The field cost of the bar is close to zero — for one speaker, one grammar

Every accepted sub-1.0 match across all 73 field utterances, classified by *which* element went missing:

- **6** are interior misses or coverage charges — a dropped `on`, a dropped `mark`, a medial `track`.
- **2** are leading-required misses. **Both are the phantom in this issue.**

The recovery behaviour that §7 D documents and that `Coverage_SequentialExtraction_SparesTheFirstCommandWhenASecondLosesItsWord` pins **did not occur once** in real speech. The phantom occurred twice. One speaker and one grammar, so this is not a general frequency claim — but it inverts the assumed cost of acting.

---

## 3. Goals and non-goals

**Goals.**

- **G-1** — An utterance can never fire a command whose first required element was not matched. Both shapes: the sequential-extraction shape and the standalone round-1 shape.
- **G-2** — #82's orphan-run termination is preserved exactly. The first of two commands keeps its 1.0000.
- **G-3** — Interior and trailing misses are untouched. `decelerate by {burn_level}` against "decelerate hard burn" still fires at 0.6667.
- **G-4** — No public tunable. The bar is a count, like admission (§2.1).
- **G-5** — No new allocation, and no new work proportional to grammar size in the triple-nested parse loop.
- **G-6** — **A legitimate, cleanly-spoken command is never destroyed by the fix.** Added after §7's measurement; §6.1 exists because one candidate rule violates it.

**Non-goals — deliberately not decided here.**

- **The numbers.** `minScore` 0.60 and `coverageWeight` 1.0 are unchanged and out of scope. The bar is explicitly *not* a threshold change.
- **Sequential extraction.** Not removed, not capped. It does not fix the standalone shape (§1.1) and it would silently discard the second of two cleanly-spoken commands, which the merging utterance buffer makes routine.
- **The 0.067 margin.** That a 3-element pattern missing one element lands on 0.6667, only 0.067 above the gate, is a real discomfort `scoring.md` already flags for the `decelerate` case. The bar does not address it and should not be read as addressing it.
- **Re-deriving the leading coverage term.** §6.1 fork C would do this. If it is chosen, it is a **separate design cycle**, not a line item here — it moves scores grammar-wide.

---

## 4. Diagnosis

Two rules, each correct in isolation, interlock:

1. **The orphan-run terminator excuses the first command.** From `track`, `intercept track {track}` matches 2 of its 3 required elements against 1 miss. `2 > 1` clears `IsAdmissibleStart`, so the run terminates, and `time to target` is charged nothing and scores a clean 1.0000. This is correct and is #82.
2. **The same candidate then wins a round and fires.** `2 / 3 = 0.6667` clears `minScore`. This is the defect.

`scoring.md` is explicit that the terminator margin is *"deliberately one notch stronger than admission … Terminating another command's orphan run is a larger claim, because it moves score off a command that is firing, so it takes strictly more evidence for than against."*

**The gap is that firing with the pattern's first required element absent is a larger claim still, and currently needs no extra evidence at all.** The test that excuses the short pattern from paying for the tail is the test that licenses the tail's candidate to fire, and nothing separates the two.

### 4.1 Why the *first* required element specifically

The justification is positional, not statistical.

In a command grammar the first required element is the **verb** — what to *do*. Later required elements are **arguments** — what to do it *to*. Losing an argument still leaves the action identified and the command recognisable as a request; the score reflects the damage and the gate decides. Losing the verb leaves **no evidence that any action was requested at all** — only that some words matched some pattern's tail.

That is a difference in kind, not degree, and it is why this is a rule about *position* rather than another threshold. A threshold cannot express it: `minScore` sees only `2/3` and cannot ask *which* third went missing. This is the same reason `minScore` is unusable against the interior `decelerate by {burn_level}` drop, which also lands on 0.6667 and which the bar correctly leaves alone.

---

## 5. The model

### 5.1 The rule

> A candidate whose **first required element** matched nothing may compete and consume, but **may not fire**.

It still enters selection, can win its round, and consumes the tokens it matched — which is what re-bases `searchStart` and keeps leading debris off the next command (§2.3, DR-4). What it may not do is produce a result.

"First required element" is the first element of the pattern that is not optional — optional literals and optional slots are skipped when locating it, exactly as they are excluded from the `MatchedRequired` / `MissedRequired` ledger (DR-7 of the scoring model). A pattern with no required elements at all has no first required element and the bar is a no-op for it.

The rule is a **single boolean carried on the match**, decided once while the pattern is walked. It is not a threshold, has no weight, reads no configuration, and is independent of coverage — the same properties the admission rule in §2.1 claims. DR-2 makes it uniform over element type, so the latch is two locals (`sawFirstRequired`, `leadingRequiredMissed`) and `MatchResult` gains exactly one `bool`.

### 5.2 Where it lives

DR-4 chose **refuse-to-FIRE**, so the bar is deliberately **not** a comparator rule. It has two sites.

**Flush path — `ParseInternal`.** The winner's flag is tracked beside the other `best*` locals and assigned at the adopt site. The suppression sits at the **top** of the post-selection block, immediately after the existing progress guard:

```csharp
if (bestEndIdx <= searchStart)          // :2503, already present
    break;

if (bestLeadingRequiredMissed)          // the bar
{
    searchStart = bestEndIdx;
    continue;
}
```

That exact placement is load-bearing, and it is *earlier* than the obvious one:

- **Progress is already guaranteed.** The guard above has run, so `bestEndIdx > searchStart` holds and the `continue` can never spin. No second guard is needed.
- **A barred round stays zero-alloc.** `ComputeConfidence`, the `VoxrSlotMatch[]` copy and the `VoxrCommand` construction all sit below this point. Suppressing at the emit site instead would build and discard them every barred round — a real cost in the parse path, and G-5 forbids it.
- **It cannot corrupt the sibling-tie alignment.** `_tiedSiblingBuf[_resultCount]` is written *before* `_resultCount++`, and the recogniser walks the two buffers in lockstep. Suppressing above that write means a barred round never records a tie for a result slot it does not fill.

**Eager path — `TryEagerCommit`.** This is the consequence of DR-4 that DR-5 had to be rewritten for. The comparator is shared (§2.4), so a rule placed *in* it would have reached the eager scan for free — but the bar is not in the comparator, so **the eager path does not inherit it**. Left alone, a leading-missed candidate would commit early and bypass the bar entirely, defeating G-1.

> **⚠ ERRATUM E-1 (recorded 2026-08-27, implementation of item 1; the RULING below is unaffected).** The premise *"an eager commit is a fire"* is **false**, and so is every claim that the eager path "bypasses the bar" or "defeats G-1". `EagerCommitVerdict.Commit` makes the recogniser call `FlushBuffer`, which routes through `ProcessParsedResults` into `ParseInternal` — the method the bar lives in — so a barred winner is refused there and the utterance reports unrecognised either way. **No phantom can fire on the eager path at all.** DR-5's ruled action (an explicit condition in `TryEagerCommit`) still stands, on the true reason: `Commit` consumes and clears the speech buffer, so without the condition a half-spoken command is flushed early and discarded, and its continuation is parsed as a separate utterance. **Fork C, read this before scoping.** Full record: `Planning~/features/leading-miss-bar/architecture.md` §10, "Canon-authority calls".


It therefore gets an explicit condition, tracked at the eager scan's own adopt site and refusing beside the two completeness refusals it belongs with:

```csharp
if (bestMissedRequiredSlot)          return EagerCommitVerdict.None;   // :4060, issue #66
if (bestHasUnmatchedRequiredTail)    return EagerCommitVerdict.None;   // issue #70
if (bestLeadingRequiredMissed)       return EagerCommitVerdict.None;   // the bar
```

`None` — keep buffering — is the right verdict, and for the same reason the other two use it: at the eager gate refusing costs only latency, and the flush path will apply the bar properly when the transcript is final.

The comment at `:4040` that explains what the eager scan inherits from the comparator must **not** be extended to cover the bar. It should be amended to say the opposite explicitly, or the next reader will assume this condition is redundant and delete it.

### 5.3 What happens to a barred round

Under DR-4's fork A the round is **not** empty — the barred candidate wins it. What changes is only that no result is written: `searchStart` advances past the span it consumed and extraction **continues** with the next round. Later commands in the same utterance are unaffected, which is the property fork B loses (§6.1).

Two consequences, both intended:

- **The span is consumed, not re-offered.** A barred candidate's tokens are not re-scanned by the next round, so a shorter fragment of the same phantom cannot fire in its place.
- **`searchStart` is re-based past the debris**, which is exactly what keeps the leading coverage term (§2.3) off the next legitimate command.

The pre-existing empty-round path at `ParseInternal:2497` is untouched and still ends extraction when no candidate is found at all. Either way, an utterance that finishes with zero results reaches the recogniser as **`OnUnrecognisedSpeech`** — which is the whole safety argument:

| | today | with the bar |
|---|---|---|
| leading word **never spoken** | executes a phantom command, silently | nothing fires → `OnUnrecognisedSpeech` |
| leading word **spoken and lost** | recovers the command | nothing fires → `OnUnrecognisedSpeech`, speaker re-utters |

An unrecoverable wrong action becomes a recoverable "didn't catch that". For a command-and-control grammar that is the correct direction of trade, and it is the only option on the table that closes the class rather than narrowing the incident.

### 5.4 The pending / `allowPartialMatch` path

A candidate that missed both its leading required element *and* a required slot previously fed the pending slot-fill path. Under the bar it wins its round but emits no result, so no pending is created — the pending path is in the recogniser and is fed by results.

This is correct and needs no special handling: pending exists for *"the command was heard, an argument is missing"*. If the verb was never heard, there is no command to hold open and nothing for a follow-up utterance to complete. Recorded here so it is not later mistaken for a regression.

### 5.5 No public switch

Per the G0 ruling, and consistent with §2.1: the bar is unconditional, with no serialised field and no constructor parameter. A grammar that legitimately wants verb-less fragments to fire has a supported route that requires no package change — **register a pattern for the fragment** (§5.6). That is strictly better than a global switch, because it is per-intent and it makes the author's intent explicit in the grammar rather than implicit in a config value.

The A/B rig needs both arms from one binary; it gets them by patching its **staged copy** of the parser, never the shipped source. That is how every prior scoring A/B in this package has worked, so no test seam is owed by the shipped build either.

### 5.6 Grammar-side mitigations — available today, on 1.5.0, with no package change

These are not a substitute for the fix. They are documented because they help users *now*, and because they are the supported answer once §5.5 removes the switch.

- **Let a legitimate pattern claim the tail.** The reporter's own 2026-08-19 grammar had `query_time_to_target` carrying a second pattern `time to target track {track}`; across 8 utterances of the exact repro phrase the phantom never appeared. Verified on a stock parser: `n=1` at 1.0000, degrading gracefully to 0.8000 when VOSK drops `track`.
- **Register a benign intent for the standalone fragment.** A `["track", "{track}"]` intent covers the round-1 shape. Measured: it displaces the phantom in both shapes, and it wins on **score**, not registration order.
- **`requiresConfirmation` on destructive intents.** Covers the whole class today, including the round-1 shape, because it diverts on *consequence* rather than on match shape.

### 5.7 The `scoring.md` §7 D amendment

**This design deliberately amends locked design.** §7 D of `Documentation~/scoring.md` (`:438`) is a worked example whose *conclusion* — that both commands fire, the damaged one at 0.67 — is exactly the behaviour being removed. It is amended, not deleted, and the amendment is small: **step 1 is unchanged and step 2 inverts.**

The replacement keeps the same grammar and the same utterance, because the valuable half of the example is step 1:

1. **Round 1 is unchanged.** `cease fire` matches at token 0. The orphan run still terminates at "target", because `approach target {target}` is still matchable from there — the start test is untouched (§2.2). `cease fire` still scores `2 / 2` = **1.00**.
2. **Round 2 now emits nothing.** `approach target {target}` still wins the round and still consumes `target hotel one` — that is what keeps step 1 true — but it missed its first required element, so it is barred from firing. No second result is written, and the utterance ends with one command.

The example gains a contrast pair it did not have — *"say the second command in full and both fire at 1.00"* is currently a closing remark, and becomes a second worked trace. And the reading guidance inverts: the symptom row *"The command that lost a word fired; the one spoken cleanly was rejected at ≈0.40"* is replaced by *"a stranded tail produced no second command"* → this is the bar, and the fix is to register a pattern that claims the tail (§5.6).

Verified against the real parser, both arms:

```
                                        before                                  after
cease fire target hotel one    n=2 cease_fire=1.0000|approach=0.6667   n=1 cease_fire=1.0000
cease fire approach target …   n=2 cease_fire=1.0000|approach=1.0000   n=2 (unchanged)
```

### 5.8 The pinned test, and the gap it leaves

`Coverage_SequentialExtraction_SparesTheFirstCommandWhenASecondLosesItsWord` (`Tests~/Runtime/VoxrCommandParserTests.cs:2462`) pins §7 D. It **transforms rather than dies**, and the transformation is instructive:

- Its first assertion — `cease_fire` scores 1.0000, *"not charged: approach target {target} is matchable from \"target\""* — **is #82's win**, and survives verbatim. It is the regression guard that proves G-2.
- Only `Assert.AreEqual(2, results.Length)` and the `2f/3f` line invert, to `1` and the absence of `approach_target`.
- Its control case (`"cease fire approach target hotel one"` → both at 1.00) survives unchanged and becomes more load-bearing.

The name is still accurate on its first half and misleading on its second; it should become `Coverage_SequentialExtraction_SparesTheFirstCommandAndBarsTheSecond`.

**The gap.** That test covers **trailing** debris. Nothing in the suite pins the **leading**-debris shape (`"target hotel one cease fire"`), which is the shape §6.1 shows is fragile. Whichever fork the human rules, a test for it must land with the bar — this is a required backlog item, not a nicety.

---

## 6. Forks

### 6.1 RESOLVED AT G1 — refuse to fire, or refuse to compete?

**Ruled 2026-08-27: A, refuse-to-FIRE.** This section is kept in full because it is the reasoning the ruling rests on, and because it records what the rejected branches cost.

The G0 ruling chose **refuse-to-COMPETE**: exclude barred candidates from selection so a legitimate rival can win that round, rather than suppressing the round winner after selection has already picked it. The stated rationale — *"strictly better, more work"* — is sound in principle and **is confirmed to work** (§7.4). But the ruling was made before the leading-coverage asymmetry (§2.3) was known, and that asymmetry makes it destructive.

**The mechanism.** Removing the phantom from selection removes the shield (§2.3). The legitimate command is now the round-1 winner, is scored with `searchStart` still at 0, and is charged `SkippedBefore` for every leading debris token:

```
'target hotel one cease fire'
  today                : n=2  approach_target=0.6667  |  cease_fire=1.0000
  refuse-to-FIRE       : n=1  cease_fire=1.0000                      ← phantom gone, real command intact
  refuse-to-COMPETE    : n=1  cease_fire=0.4000       ← 2/(2+3), BELOW the 0.60 gate: NOTHING FIRES
```

`cease_fire` was spoken perfectly and is destroyed. On the 699-row corpus this happens to **11 rows**, every one of them a command scoring 1.0000 today (§7.3). That is the same class of failure as #124 itself — an utterance the speaker intended produces the wrong outcome — merely in the other direction, and it violates **G-6**.

**The three coherent options.**

| | A — refuse to FIRE | B — refuse to COMPETE *(as ruled)* | C — refuse to COMPETE + leading excusal |
|---|---|---|---|
| **What it does** | Barred candidate still competes and consumes its span, but emits no result | Barred candidate is excluded from selection entirely | As B, plus the leading coverage term gains the same admissibility excusal the trailing term has had since #82 |
| **Rows changed / 699** | 48 | 62 | 89 |
| **Clean 1.0000 commands destroyed** | **0** | **11** | **0** |
| **A legitimate rival can win the round** | **no** | **yes** | **yes** |
| **Where it lives** | `ParseInternal`, at the emit site | `CompareCandidate:3071` — the count beside admission | Both, plus `SkippedBefore:3611` |
| **Cost** | Denies an overlapping rival; the bar is not a selection rule, so it reads less cleanly against §2.1 | Destroys clean commands | Re-derives a coverage term. Moves 41 scores, most of them **upward** — prepended in-grammar noise becomes free (`'launch cease fire'` → 1.0000, from 0.6667) |

**What each buys, measured, not argued** (`'time to target track one two four four designate'`, where a legitimate `designate_track` overlaps the phantom's span):

```
  today             n=2  query=1.0000 | intercept_target=0.5000   ← the phantom
  A refuse-to-FIRE  n=1  query=1.0000                             ← rival DENIED
  B refuse-to-COMPETE  n=2  query=1.0000 | designate_track=0.6667 ← rival wins ✓
  C  + lead excusal    n=2  query=1.0000 | designate_track=1.0000 ← rival wins ✓
```

So ruling (3)'s rationale is real. The question is whether it is worth 11 destroyed commands, and whether C's price — re-deriving a coverage term, weakening coverage against exactly the noise-prefix class #65 §5.2 was built to charge — is one this design should pay.

**Ruled: A, with C recorded as a separate future design topic.**

The reasoning is the issue's own. The bar exists because a wrong action is unrecoverable while "didn't catch that" is recoverable (§5.3). B fails on its own terms: it converts 11 recoverable situations into unrecoverable ones. A closes both #124 shapes completely (§7.4), costs zero clean commands, and is the smaller change. The rival case B and C buy is real but rare — **zero occurrences in 699 corpus rows**, and it requires a grammar that registers a pattern overlapping the phantom's span, which is precisely the grammar-side mitigation §5.6 already recommends for other reasons.

C is the *principled* end state — the leading/trailing asymmetry in §2.3 is a genuine wart, and C is what fixing it looks like. But it is a scoring-model change of the same magnitude as #65 §5.2, it moves 41 scores including many upward, and folding it into this branch would make a safety fix carry an unmeasured re-derivation. It should be its own design topic with its own corpus work.

**C is not dropped, it is deferred.** The leading/trailing asymmetry in §2.3 is a real wart and C is what fixing it looks like; it is recorded as a future design topic in §10 so it is reopened deliberately rather than rediscovered. Note that if C is ever taken, the bar can move into the comparator at that time and §5.2's two sites collapse back to one — the two designs compose, they do not conflict.

### 6.2 RESOLVED AT G1 — does the bar apply when the first required element is a SLOT?

**Ruled 2026-08-27: uniform.** Deferred at G0 to the measurement. **The measurement cannot decide it**: arms 1 and 2 are byte-identical across all 699 rows (§7.3). The corpus contains no pattern whose first required element is a slot, so it has no opinion.

On a synthetic grammar the arms do differ, but weakly — `["{track}", "hold", "steady"]` against `"cease fire hold steady"` scores 0.3333, below the default gate, so it does not fire either way at shipped defaults. The fork only bites for a slot-leading pattern with enough required elements to clear 0.60 while missing its slot — five or more.

**Ruled: apply uniformly (any first required element).** Three reasons: it is one count with no element-type branch, which is what §2.1 asks of a rule at this site; §4.1's justification generalises — the first required element is the pattern's *anchor*, and a pattern that matched nothing of its anchor has identified nothing, whether that anchor is a verb or a value; and a type branch here would be a rule the parser cannot explain to an author.

Recorded honestly: this is ruled **on principle, not on numbers**, because no numbers exist.

### 6.3 Rejected without further consideration

Each is refuted above and is recorded so it is not re-proposed.

| Direction | Why it is dead |
|---|---|
| Document it as a known limitation | Ruled out by the maintainer: *"it will never simply be a known limitation."* |
| Raise `minScore` | Cannot see *which* element was missed. Would take the interior `decelerate by {burn_level}` drop with it — the case that proves a threshold is the wrong instrument (§4.1) |
| Cap commands per utterance | Does not fix the standalone round-1 shape (§1.1) |
| Remove sequential extraction | Same, plus it discards the second of two cleanly-spoken commands, which the merging buffer makes routine |
| Acoustic-gap detection | Empirically falsified (§2.5) |
| Require `[unk]` evidence | Empirically falsified (§2.5) |
| Weaken `IsAdmissibleStart` | *Is* reverting #82, and does not stop the round-1 shape |

---

## 7. Measurement — performed, not planned

All arms were produced by replaying the **real parser sources**, staged from `b83b62f` into a buildable .NET project, through `Planning~/features/coverage-in-selection/phase7-corpus.tsv` (699 rows) and gated at the shipped `minScore` of 0.60. Apparatus and raw TSVs: `Planning~/features/coverage-in-selection/ab-rig/issue124/` (`Verify4.cs`, `analyse-arms.py`, `compare-arms.py`, five `corpus-*.tsv`).

### 7.1 Validation performed before any delta was trusted

Per the `grammar-ab-rig` rule that a clean corpus A/B is not by itself evidence:

- `--grammar` output is **byte-identical** to the committed `grammar` pin in `NativeBridge~/harness/expectations.json` (133 words). The rig is scoring what the package scores.
- The bar-off arm is **byte-identical** to the previously saved `corpus-bar-off.tsv`. The patch is inert when off — re-verified after each subsequent arm landed.
- The refuse-to-FIRE arm is **byte-identical** to the previously saved `corpus-bar-on.tsv`. This build independently reproduces the earlier session's numbers from a separately written patch, which validates both.

### 7.2 All four #124 acceptance cases pass, in every ON arm

```
'time to target track one two four four'   n=1  query_time_to_target=1.0000   ← phantom gone
'track one two four four'                  n=0                                ← round-1 shape closed
'intercept track one two four four'        n=1  intercept_target=1.0000       ← genuine, unharmed
'decelerate hard burn'                     n=1  decelerate=0.6667             ← interior miss, untouched
```

### 7.3 Blast radius, 699 rows, gated at 0.60

| arm | rows changed | fire nothing | score drops | **clean 1.0000 commands destroyed** |
|---|---|---|---|---|
| A — refuse-to-FIRE | 48 | 17 | 0 | **0** |
| B — refuse-to-COMPETE | 62 | 28 | 14 | **11** |
| C — refuse-to-COMPETE + lead excusal | 89 | 17 | 41 | **0** |
| B literal-only vs B uniform | *identical* | | | |

Arm A's 48 rows decompose as the issue comment reported: concat 29, delete 9, prepend 5, append 4, baseline 1 — roughly **36 phantoms suppressed against 9–12 genuine leading-drop recoveries lost**, on a corpus deliberately built to contain both.

The `baseline` row is worth its own line: **`target hotel one`, unperturbed, fires `approach_target` at 0.6667 today.** The bug is present in the package's own committed fixture corpus.

### 7.4 What the corpus does **not** contain

Stated explicitly, because it bounds every number above:

- **No row where a legitimate rival wins a round the phantom used to take.** Zero, in all 699. B and C's advantage over A is real (§6.1) but is not exercised by this corpus and had to be demonstrated on a synthetic grammar.
- **No pattern whose first required element is a slot** — which is why §6.2 cannot be settled by measurement.
- **One speaker, one grammar** in the field data behind §2.6. The "zero legitimate leading-miss recoveries" finding is a strong signal, not a frequency claim.

### 7.5 Still owed, at implementation time

- **Both Unity suites** (EditMode + PlayMode) via the host project, per the verification bindings. Most parser tests are PlayMode.
- **`--bench`** on both arms. G-5 claims no new allocation and no measurable cost; the rig measures µs/utterance and bytes/utterance and must be run rather than asserted.
- **The `DocCheck` pin.** `scoring.md` §7 D's published numbers are pinned against the real parser (`-p:StartupObject=AbRig.DocCheck` — dispatch on an argument, the property does not survive an incremental rebuild). The §5.7 amendment must update that pin in the same change, or the page and the pin diverge silently.

---

## 8. Decision records — RATIFIED at G1, 2026-08-27

| # | Decision | Ratified | Rejected alternatives |
|---|---|---|---|
| DR-1 | The rule | A candidate whose **first required element** matched nothing may compete and consume, but may not fire. Optionals skipped when locating it | A score threshold (§4.1 — cannot see *which* element); a rule about the leading *token* rather than the leading *element* |
| DR-2 | Element type | **Uniform** — any first required element, literal or slot (§6.2) | Literal-only. Ruled on principle; the corpus cannot discriminate (§7.4) |
| DR-3 | Placement, flush | **`ParseInternal`, immediately after the progress guard at `:2503`** — above `ComputeConfidence`, the slot-array copy, the `VoxrCommand` construction and the `_tiedSiblingBuf` write; then `searchStart = bestEndIdx; continue;` (§5.2) | `CompareCandidate:3071` as a comparator count — that is fork B, refuted by §6.1. The emit site at `:2557` — allocates and discards a command every barred round (G-5), and writes a tie record for a result slot it never fills |
| DR-4 | **Fire or compete** | **A — refuse to FIRE.** The barred candidate still competes and consumes, so leading debris never lands on the next command | B, refuse-to-COMPETE — destroys 11 clean 1.0000 commands, violates G-6. C, B plus a leading-coverage excusal — re-derives a coverage term and moves 41 scores, most upward; **deferred to its own design topic**, not rejected |
| DR-5 | Placement, eager | **An explicit condition in `TryEagerCommit`**, beside the #66 and #70 completeness refusals, returning `EagerCommitVerdict.None`. The `:4040` comment is amended to say the bar is *not* inherited | Inheritance via the shared comparator — **available only under fork B**. Under A the eager path would bypass the bar entirely and defeat G-1 | <!-- ERRATUM E-1: the rejected-alternative rationale in this cell is false; the ruling stands. See the erratum note in §2.4/§5.2. -->
| DR-6 | Empty round | No change. `ParseInternal:2497` breaks; zero results reaches the recogniser as `OnUnrecognisedSpeech` (§5.3) | A distinct event or reject reason for "barred" — leaks a parser-internal rule into the public surface |
| DR-7 | Public surface | **None.** No serialised field, no constructor parameter, no test seam in shipped code (§5.5) | A default-on opt-out field — a knob, and a way to switch the safety fix off |
| DR-8 | Pending path | A barred candidate creates no pending. Correct by construction, not special-cased (§5.4) | Route barred candidates to slot-fill — there is no command to hold open |
| DR-9 | §7 D | Amended, not deleted. Step 1 unchanged, step 2 inverts; the control becomes a second worked trace (§5.7). `DocCheck` pin updated in the same change | Delete the example — loses the #82 half, which is still correct and still the point |
| DR-10 | Version | **2.0.0.** Default-on behaviour change, 48 corpus rows move, a documented behaviour is removed | 1.6.0 as a bug fix; opt-in in 1.x then on at 2.0 (contradicts the default-on ruling) |

---

## 9. Feature backlog — CANONICAL, locked at G1

Dependency-ordered; each is one feature branch under the implementation track. Picked up only when actually going to be built, per the workflow.

1. **`feat-leading-miss-bar`** — DR-1/2/3/5/6/8. The `MatchResult` bool and the `TryMatchScored` latch; DR-3's flush suppression; **DR-5's eager condition and the `:4040` comment amendment**; the §5.8 test transformation and rename; **the new leading-debris test (§5.8's gap)**; an eager-path test pinning DR-5; and the `--bench` run for G-5. The whole behaviour change lands here.

   Three things this feature must not get wrong, each already a near-miss in the analysis:
   - The suppression goes **above** the allocations and the `_tiedSiblingBuf` write, not at the emit site (DR-3).
   - The eager condition is **not** optional and **not** redundant (DR-5). It is the one place fork A differs from fork B in reachability.
   - The latch must skip optionals when locating the first required element (§5.1), and must not fire on an unmatched *optional* leading element.

2. **`feat-leading-miss-docs`** — DR-9's `scoring.md` §7 D amendment and its `DocCheck` pin; the §5.6 authoring guidance in `Documentation~/command-recognition.md`; the `KNOWN_LIMITATIONS.md` entry for the leading-drop recoveries the bar costs (§7.3: 9–12 rows on the corpus, zero observed in the field); `CHANGELOG.md` under `[Unreleased]`. Depends on 1.

3. **`feat-leading-miss-warning`** — the construction-time author warning for the hazardous grammar shape: a bare pattern whose trailing tokens can begin a match on another intent's **non-initial** required literal. Sits beside the two hazards already scanned at construction (`VoxrCommandParser.cs:723`). Editor-only, no runtime behaviour. *Independently valuable and independently droppable* — it detects the shape rather than fixing it, and item 1 has already fixed it.

   > **⚠ ERRATUM E-2 (recorded 2026-08-27, G0 of item 2; the backlog entry and every DR are unaffected).** *"Independently droppable"* **no longer holds.** At item 2's G0 the human ruled that item 2 documents nothing about the construction-time sibling **over-warning** — the warning says *"the wrong intent can fire"* about grammars where a leading discriminator at `D ≥ 3` clears the `(D−1)/D` reachability test and yet can never fire, because the bar refuses it (deferred in code at `VoxrCommandParser.cs:1646-1647`). With item 2 silent on it, dropping item 3 before item 4 would ship a false author-facing claim with nothing disclosing it — the same class of claim that issue #81 spent a feature reversing. **Item 3 is therefore a hard dependency of item 4**, not an optional one. This is a scheduling consequence of a human ruling, not a defect in the backlog: item 3's scope, value and DR set are exactly as written above. Ruled erratum-plus-pointer, no design branch and no G1 re-lock, on the E-1 precedent. Full record: `Planning~/features/leading-miss-docs/architecture.md` §9, "Canon-authority calls"; tracked at #130.

   > **⚠ ERRATUM E-3 (recorded 2026-08-28, G0 of item 3; no DR changes).** **Item 3's scope as written above is incomplete, and E-2 is internally inconsistent.** The scope clause names only the new scan. But E-2's argument for non-droppability is that dropping item 3 would ship the sibling **over-warning**'s false claim undisclosed — and the new scan does not remove that claim; only *narrowing the existing sibling warning* does. So E-2's conclusion does not follow from a scope that excludes the narrowing, while its sentence *"item 3's scope, value and DR set are exactly as written above"* asserts that it does. One of the two has to give. **Ruled at item 3's G0: the scope gives.** Item 3 delivers **both** — the narrowing of the existing sibling warning (E-2's actual release blocker) and the construction-time scan named in the clause above — with the narrowing built first, so the release-critical half lands and is reviewed independently of the scan's difficulty and the option to cut the scan is retained rather than assumed away. Ruled erratum-plus-pointer, no design branch and no G1 re-lock, on the E-1 and E-2 precedent: no decision record changes, and what changed is which deliverable discharges an argument E-2 already made. Raised by the `plan-validator` pass on item 3's build plan — the same instrument that raised E-2, and stopped the same self-authorising shortcut of booking it to an issue instead of to canon. Full record: `Planning~/features/leading-miss-warning/architecture.md` §10, "Canon-authority calls"; tracked at #130.

4. **`chore/release-2.0.0`** — DR-10, via the `release` skill. Depends on 1–3.

§5.6's mitigations are documented in item 2 regardless of anything else, because they help users on 1.5.0 **today**.

---

## 10. G1 rulings

**Ruled by the human in conversation, 2026-08-27.** All ten decision records ratified; see §8 for each ratified value and its rejected alternatives.

**DR-4 was reversed from the G0 ruling, and this was the point of the branch.** G0 chose refuse-to-COMPETE on the reasoning that letting a legitimate rival win the round is strictly better. That reasoning is confirmed correct in isolation (§6.1 shows the rival genuinely does win), but it was made before §2.3's leading-coverage asymmetry was known. The measurement taken on this branch shows refuse-to-COMPETE destroys **11 clean 1.0000 commands** in 699 rows — the same failure class as #124 itself, pointed the other way — because the phantom candidate is the only thing shielding a later command from leading debris. **Ruled: A, refuse-to-FIRE**, which destroys none and closes both #124 shapes.

**DR-2 ratified as recommended: uniform.** Recorded honestly as ruled on principle rather than on numbers — arms 1 and 2 are byte-identical across all 699 corpus rows, because the corpus contains no pattern whose first required element is a slot (§7.4).

**Two decision records were rewritten after the DR-4 ruling, both forced by it rather than chosen.** They are recorded here because a reader comparing the draft presented at G1 against this table will otherwise see them change with no explanation:

- **DR-3** moved from *"`CompareCandidate:3071`, a second count beside admission"* to *"`ParseInternal`, immediately after the progress guard at `:2503`"*. The comparator placement **is** fork B; it cannot express fork A. The specific position within `ParseInternal` was then chosen to keep a barred round zero-alloc and to avoid writing a sibling-tie record for a result slot that is never filled (§5.2).
- **DR-5** moved from *"inherited automatically by sharing the comparator"* to *"an explicit condition in `TryEagerCommit`"*. Inheritance was a property of fork B alone. Under A the eager path would commit a leading-missed candidate early and bypass the bar entirely — a hole that would have defeated G-1 while every flush-path test stayed green. *(**ERRATUM E-1**: the "hole" is not a G-1 hole — the flush bars it regardless. The condition protects the buffer, not G-1. Ruling unaffected; see §2.4.)*

**One item is deferred rather than rejected: fork C** (§6.1) — giving the leading coverage term the same admissibility excusal the trailing term has carried since #82. It is the principled fix for the §2.3 asymmetry, it costs zero clean commands, and it composes with the bar rather than conflicting (the bar could move into the comparator at that time). It is out of scope here because it re-derives a coverage term and moves 41 of 699 scores, most of them upward — a change of the same magnitude as #65 §5.2, which must not ride along inside a safety fix. **It gets its own design topic and its own corpus work.**

**Two claims are load-bearing and must be pinned by tests in backlog item 1**, not carried on the strength of the rig:
- The eager path refuses a leading-missed winner (DR-5). Nothing on the flush path can detect its absence.
- The leading-debris shape (`"target hotel one cease fire"`) keeps `cease_fire` at 1.0000. This is the assertion that would have caught fork B, and no test in the suite covers it today (§5.8).

**Design track complete.** §9 is the canonical feature backlog. The implementation track picks item 1 when it is actually going to be built, per the workflow.

---

## Appendix — a note on this branch

`Planning~/` is gitignored, so this document is local-only and `design-leading-miss-bar` carries **zero diff** from `main` — exactly as `scoring-model.md` and `sibling-tie-disambiguation.md` did. The workflow's "merge the design branch to main" step is vacuous here; the branch is a bookkeeping marker, not a change set.

The measurement apparatus in `Planning~/features/coverage-in-selection/ab-rig/issue124/` is likewise gitignored and local-only. Note that `grep` in this repo honours `.gitignore`, so a repo-wide search will not find the rig's own C# or any planning doc — search those paths explicitly.
