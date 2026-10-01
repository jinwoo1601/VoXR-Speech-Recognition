---
type: requirements
feature: leading-miss-bar
topic: leading-miss-bar
status: draft
updated: 2026-08-27
sources: [Planning~/design-docs/leading-miss-bar.md, Planning~/features/coverage-in-selection/requirements.md, Planning~/features/coverage-in-selection/architecture.md, Planning~/features/tie-aware-selection/architecture.md]
---

# Leading-Required-Miss Bar — Requirements

## 1. Feature & scope

Backlog line (design §9, row 1): `feat-leading-miss-bar` — DR-1/2/3/5/6/8. *"The `MatchResult` bool and the `TryMatchScored` latch; DR-3's flush suppression; DR-5's eager condition and the `:4040` comment amendment; the §5.8 test transformation and rename; the new leading-debris test (§5.8's gap); an eager-path test pinning DR-5; and the `--bench` run for G-5. The whole behaviour change lands here."*

This is the first of four features under the G1-locked `leading-miss-bar` design (locked 2026-08-27, issue #124). **It is the entire behaviour change.** Items 2–4 are documentation, an author-facing warning, and the release; none of them alters what the parser does. If this feature ships and the other three never do, issue #124 is closed and the package is correct — it is merely under-documented.

The rule it builds, in one line (DR-1):

> A candidate whose **first required element** matched nothing may compete and consume, but **may not fire**.

**It deliberately builds none of items 2–4.** No `scoring.md` §7 D amendment, no `DocCheck` pin update, no `KNOWN_LIMITATIONS.md` entry, no `command-recognition.md` authoring guidance, no construction-time warning, no version bump. Those are items 2 and 3, and the version bump is item 4. The one exception is `CHANGELOG.md`, moved here by human ruling — see §4.13.

**One thing this feature is not.** It is not a threshold change, not a cap on commands per utterance, and not a removal of sequential extraction. Design §6.3 records each of those as refuted, and §3's non-goals put `minScore` 0.60 and `coverageWeight` 1.0 explicitly out of scope. A reader who arrives at this feature expecting a scoring change has the wrong model of it: **no score moves.** What moves is whether a already-computed score is allowed to produce a result.

## 2. Why — an unrecoverable wrong action, traded for a recoverable miss

The reported case, verbatim from issue #124. A grammar registers `query_time_to_target : ["time", "to", "target"]` and `intercept_target : ["intercept", "track", "{track}"]`. The speaker says **"time to target track one two four four"**. Round 1 fires `query_time_to_target` at 1.0000. Round 2 fires `intercept_target` at 0.6667 with `track = "one two four four"` — **and the word "intercept" was never spoken.** A read-only query executed a maneuver order.

Nothing marks the second command as invented. Every token decoded cleanly, so the per-word confidence table is clean; `OnCommandRecognised` fires once per command, so a subscriber cannot tell the two came from one utterance. The maintainer — who is also the reporter — ruled the class: *"a single Utterance firing two commands when the intention was one is an unacceptable phenomenon"*, and *"it will never simply be a known limitation."*

**The phenomenon is wider than the issue title.** Design §1.1 establishes two things that forbid a narrow fix:

- It fires **standalone, in round 1**, with no first command present: `'track one two four four'` → `intercept_target=0.6667`, `n=1`. At `bufferWindow 2.0`, any pause longer than the window delivers a tail exactly like this. Anything scoped to "round 2" narrows the incident and leaves the class open.
- It is **not confined to the reporter's grammar**. The package's own committed fixture corpus contains the shape: the `baseline` row `target hotel one` fires `approach_target` at 0.6667 today.

**Why the *first* required element specifically** (design §4.1) — this is the justification the whole feature rests on, and it is positional, not statistical. In a command grammar the first required element is the **verb**: what to *do*. Later required elements are **arguments**: what to do it *to*. Losing an argument still leaves the action identified and the command recognisable as a request, and the score reflects the damage while the gate decides. Losing the verb leaves **no evidence that any action was requested at all** — only that some words matched some pattern's tail. A threshold cannot express that difference: `minScore` sees `2/3` and cannot ask *which* third went missing. That is the same reason the interior `decelerate by {burn_level}` drop, which also lands on 0.6667, must be left alone (F8).

**The trade, stated plainly** (design §5.3):

| | today | with the bar |
|---|---|---|
| leading word **never spoken** | executes a phantom command, silently | nothing fires → `OnUnrecognisedSpeech` |
| leading word **spoken and lost** | recovers the command | nothing fires → `OnUnrecognisedSpeech`, speaker re-utters |

An unrecoverable wrong action becomes a recoverable "didn't catch that". The recovery in row 2 is real and is the price; §7.3 measures it at 9–12 rows of 699, and the field data behind design §2.6 observed it **zero** times in 73 utterances while observing the phantom twice.

**Carry canon's caveat with that figure, because it is easy to overstate.** The field data is **one speaker and one grammar**, and design §2.6 and §7.4 both say so explicitly: it *"is not a general frequency claim"*, and the zero-recoveries finding is *"a strong signal, not a frequency claim."* What it does is invert the assumed cost of acting — it does not establish a rate. The 48-row corpus decomposition is the wider evidence (§7.3: concat 29, delete 9, prepend 5, append 4, baseline 1 — roughly 36 phantoms suppressed against 9–12 genuine recoveries lost, on a corpus deliberately built to contain both).

## 3. Observable behaviour

**One thing changes, and it is a subtraction.** An utterance that today produces a command whose pattern's first required element matched nothing now produces no such command. Everything else about the parse is identical: the same candidates are enumerated, the same scores are computed, the same candidate wins each round, and the same tokens are consumed. Only the result is withheld.

Concretely, across the two shapes:

- **Sequential-extraction shape.** `'time to target track one two four four'` fired two commands and now fires one — `query_time_to_target` at 1.0000, unchanged. The phantom `intercept_target` at 0.6667 is gone.
- **Standalone round-1 shape.** `'track one two four four'` fired one command and now fires none. The utterance reaches the recogniser as `OnUnrecognisedSpeech`.

**What deliberately does not change:**

- **A genuine command is unharmed.** `'intercept track one two four four'` still fires `intercept_target` at 1.0000.
- **Interior and trailing misses are untouched.** `'decelerate hard burn'` still fires `decelerate` at 0.6667. The bar asks *which* element was missed; this one's first element matched.
- **Issue #82's win is preserved exactly.** `'cease fire target hotel one'` still scores `cease_fire` at **1.0000**, not 2/(2+3) = 0.40. This is the single most important non-regression in the feature (§4.7).
- **Leading debris still lands nowhere.** `'target hotel one cease fire'` still gives `cease_fire` **1.0000**. The barred candidate still wins its round and still consumes `target hotel one`, which re-bases `searchStart` past the debris. This is the property fork B loses and the reason DR-4 reversed the G0 ruling.
- **No public surface moves.** No new field, no new constructor parameter, no new event, no new enum value, no changed signature. An integrator's code compiles and links unchanged.
- **No score moves.** Every fired command carries exactly the score it carries today.

**In the Editor**, a barred round records **no** parse-diagnostic entry. The diagnostic block sits below the emit site, so DR-3's placement necessarily skips it — and that is *forced* by G-5 rather than merely permitted, because the block itself allocates three times per round (two `int[]`, a `string.Join`, and the `List` append). The consequence is real and is recorded as an open question: an author debugging a grammar sees a round that consumed tokens and produced neither a result nor a diagnostic (§8.4).

**On the eager path**, a leading-missed winner now returns `EagerCommitVerdict.None` — keep buffering — where it previously returned `Commit`. The user-visible effect is that such an utterance waits for the buffer window instead of firing early, and is then barred by the flush. It never fires either way; the eager condition exists so it does not fire *sooner* (§4.5).

## 4. Functional requirements

| # | Requirement | Priority | Acceptance check | Source |
|---|---|---|---|---|
| F1 | **The rule (DR-1).** A candidate whose first required element matched nothing may compete and consume, but may not fire. It still enters selection, can win its round, and consumes the tokens it matched. What it may not do is produce a result. | Must | `'time to target track one two four four'` → `n=1`, `query_time_to_target=1.0000`. `'track one two four four'` → `n=0`. Both asserted through the public `Parse` surface. | DR-1; design §5.1 |
| F2 | **The rule is a single boolean decided while the pattern is walked.** Not a threshold, no weight, reads no configuration, independent of coverage — the same properties the admission rule at `CompareCandidate:3071` claims for itself. `MatchResult` gains exactly one `bool`; the latch is two stack locals. | Must | `MatchResult` grows by one `bool` field and no other. No new field, constant, or configuration value is read to evaluate the bar. The parse path allocates nothing new (F15). | DR-1; design §2.1, §5.1 |
| F3 | **Optionals are skipped when locating the first required element, and an unmatched *optional* leading element never trips the bar (§5.1).** "First required element" means the first element that is not optional — optional literals and optional slots are skipped exactly as they are excluded from the `MatchedRequired`/`MissedRequired` ledger (scoring DR-7). A pattern with no required elements at all has no first required element and the bar is a no-op for it. | Must | A pattern led by an unmatched optional (`["?please", "switch", "to", "weapons"]` against `"switch to weapons"`) fires at its usual score. A pattern led by a *matched* optional whose first **required** element then misses is barred. A pattern of only optional elements is never barred. | DR-1; design §5.1; scoring DR-7 |
| F4 | **Uniform over element type (DR-2).** The bar applies to any first required element, literal or slot. No element-type branch. | Must | A slot-leading pattern (`["{track}", "hold", "steady"]`) whose slot matched nothing is barred on the same code path as a literal-leading one, with no type test in the condition. | DR-2; design §6.2 |
| F5 | **Flush suppression is placed above the allocations and above the `_tiedSiblingBuf` write (DR-3).** In `ParseInternal`, immediately after the existing progress guard, before `ComputeConfidence`, the `VoxrSlotMatch[]` copy, the `VoxrCommand` construction and the sibling-tie record. Body is exactly `searchStart = bestEndIdx; continue;` — no second progress guard. | Must | The suppression sits between the progress guard and the `ComputeConfidence` call. A barred round allocates nothing (F15) and writes no `_tiedSiblingBuf` entry. Verified by reading the diff, and by F15's allocation measurement over an utterance whose every round is barred. | DR-3; design §5.2 |
| F6 | **The eager path gets its own explicit condition (DR-5).** `TryEagerCommit` returns `EagerCommitVerdict.None` for a leading-missed winner, beside the issue #66 and #70 completeness refusals. The bar is **not** in the comparator, so the eager scan inherits nothing. | Must | A dedicated test in `VoxrEagerCommitTests.cs` drives `TryEagerCommit` with a leading-missed winner and asserts `None`. Mutation check: deleting the condition must fail that test and **no other** (F14). | DR-5; design §2.4, §5.2 |
| F7 | **The `:4040` comment is amended to say the bar is *not* inherited.** The existing comment explains what the eager scan gets free from the shared comparator. Left unamended, the next reader assumes F6's condition is redundant and deletes it. | Must | The comment explicitly states that the bar, unlike the admission rule, does not reach the eager path through the comparator and that the condition below is load-bearing. | DR-5; design §5.2 |
| F8 | **Interior and trailing misses are untouched (G-3).** A pattern whose first required element matched but which missed a later one scores and fires exactly as today. | Must | `'decelerate hard burn'` → `n=1`, `decelerate=0.6667`. `Coverage_SequentialExtraction_ChargesNothingForALaterCommand` passes **untouched**. | G-3; design §7.2 |
| F9 | **Issue #82 is preserved exactly, and mechanically (G-2).** `BuildCoverageTables`, `SkippedBefore`, `OrphanedAfter` and `IsAdmissibleStart` are **not modified**. Orphan-run termination runs through `IsAdmissibleStart`'s own count test and never calls `CompareCandidate`, so the bar is invisible to it by construction rather than by luck. | Must | Those four members are byte-identical in the diff. `'cease fire target hotel one'` → `cease_fire=1.0000`, with the surviving verbatim assertion *"not charged: approach target {target} is matchable from \"target\""*. | G-2; DR-3; design §2.2, §2.3 |
| F10 | **The leading-debris shape keeps its clean command.** `'target hotel one cease fire'` → `cease_fire=1.0000`. The barred candidate consumes the debris, which re-bases `searchStart` and keeps `SkippedBefore` off the next command. **No test in the suite covers this today.** | Must | A new test asserts `cease_fire` at 1.0000 on that utterance. Mutation check: converting the bar to refuse-to-COMPETE must fail it (it would score 0.4000 and fire nothing). This is the assertion that catches fork B. | G1 ruling 2026-08-27; design §5.8, §6.1 |
| F11 | **No public surface (DR-7, G-4).** No serialised field, no constructor parameter, no public enum value, no test seam in shipped code. The bar is unconditional and default-on. | Must | The diff adds no `public` member and no `[SerializeField]`. `MatchResult`'s new field is `internal` by containment. The A/B rig gets both arms by patching its **staged** copy, never the shipped source. | DR-7; design §5.5 |
| F12 | **The empty-round and pending paths are unchanged (DR-6, DR-8).** `ParseInternal`'s existing empty-round `break` is untouched; an utterance finishing with zero results reaches the recogniser as `OnUnrecognisedSpeech`. A barred candidate creates no pending — correct by construction, because pending is fed by results and there is no command to hold open. | Must | No new event, reject reason or enum value. `'track one two four four'` raises `OnUnrecognisedSpeech` and enters no pending state. No `allowPartialMatch` code path is edited. | DR-6, DR-8; design §5.3, §5.4 |
| F13 | **The §5.8 test transforms and is renamed.** `Coverage_SequentialExtraction_SparesTheFirstCommandWhenASecondLosesItsWord` → `...SparesTheFirstCommandAndBarsTheSecond`. Its first assertion survives **verbatim** (it is #82's win and F9's guard); `Assert.AreEqual(2, results.Length)` → `1`; the `2f/3f` line and the slot assertion go; the control case survives unchanged and becomes more load-bearing. | Must | The renamed test exists, the #82 assertion is character-identical to its current text, and the control (`"cease fire approach target hotel one"` → both 1.00) is unedited. | design §5.8; DR-9 |
| F14 | **Both load-bearing claims are pinned by NEW tests, and each is mutation-verified.** The eager refusal (F6) and the leading-debris shape (F10). Neither is covered today, and design §10 names both as required in this item rather than carried on the rig's word. | Must | Two new tests. For each: revert the implementation half and confirm that test fails; restore and confirm it passes. The eager mutation must fail **only** the eager test — if a flush test also fails, the eager condition was never the thing under test. | G1 ruling 2026-08-27; design §5.8, §10 |
| F15 | **No new allocation and no new work proportional to grammar size (G-5).** The latch is two stack locals in a method already on the innermost `(command, pattern, startIdx)` path; the flush suppression precedes every allocation in the round. | Must | **Primary:** the rig's `--bench` on both arms, reporting µs/utterance and bytes/utterance — ordinary .NET, where the byte counter works. Design §7.5 asks for this specifically, and G-5 is measured there, not asserted. **Unity-side:** the latch is pinned through `TryEagerCommit`, which returns an enum and can be held at `Is.Not.AllocatingGCMemory()`; `Parse` **cannot** be (§4.8). Mutation-verified either way. | G-5; design §7.5, §9 |
| F16 | **All seven acceptance cases hold, measured on the rig and asserted in the suite.** See §4.14. | Must | The table in §4.14, every row. | design §7.2, §5.7, §5.8 |
| F17 | **Both Unity suites pass, with no expected value edited except F13's.** | Must | EditMode and PlayMode both green through the host project. The only edited assertions in the whole diff are the three lines F13 names. Any other edited expectation is a finding, not a fix. | design §7.5; verification bindings |
| F18 | **`CHANGELOG.md` gains an `[Unreleased]` entry.** Moved here from backlog item 2 by human ruling, 2026-08-27. Records the behaviour removal as breaking. | Should | An `[Unreleased]` entry describes the bar in user-facing terms and is marked as a breaking behaviour change. It does **not** name a version number — that is item 4's job via the `release` skill. | human ruling 2026-08-27; §4.13 |

### 4.1 F1/F5 — why "refuse to FIRE" and not "refuse to compete", restated as a requirement

This is the reversal at the centre of the design and the single easiest thing to get wrong while implementing, because refuse-to-COMPETE is the *tidier* change: one count beside the admission rule at `CompareCandidate:3071`, in the method whose own comment invites exactly that shape. It is also the one that destroys clean commands.

VoXR's two coverage terms are asymmetric (design §2.3). The trailing term `OrphanedAfter` reads `_orphanRun`, which stops at the first admissible start — that excusal *is* issue #82. The leading term `SkippedBefore` is a bare prefix subtraction with **no excusal at all**. So leading debris is charged unconditionally to whichever candidate wins the round, and **the only reason this has never hurt anyone is that a leading-missed candidate always wins round 1 and consumes the debris**, re-basing `searchStart` past it before the legitimate command is ever scored.

The phantom is therefore a **shield**, and removing it from selection removes the shield:

```
'target hotel one cease fire'
  today                : n=2  approach_target=0.6667  |  cease_fire=1.0000
  refuse-to-FIRE       : n=1  cease_fire=1.0000                      <- phantom gone, real command intact
  refuse-to-COMPETE    : n=1  cease_fire=0.4000       <- 2/(2+3), BELOW the 0.60 gate: NOTHING FIRES
```

On the 699-row corpus that destroys **11 rows**, every one a command scoring 1.0000 today — the same failure class as #124 itself, pointed the other way, and a violation of G-6.

**The requirement this generates:** the barred candidate must still be selected and must still advance `searchStart` past its consumed span. An implementation that removes it from selection passes F1 and F8 and fails F10. That is why F10 exists, and why F14 requires it be mutation-verified against exactly this mutation.

**What fork A costs, measured rather than asserted.** Fork A is not free, and the price is that an overlapping legitimate rival is denied the round the phantom takes. Canon §6.1 demonstrates it on a grammar where `designate_track` overlaps the phantom's span:

```
'time to target track one two four four designate'
  today                n=2  query=1.0000 | intercept_target=0.5000   <- the phantom
  A refuse-to-FIRE     n=1  query=1.0000                             <- rival DENIED
  B refuse-to-COMPETE  n=2  query=1.0000 | designate_track=0.6667    <- rival wins
  C  + lead excusal    n=2  query=1.0000 | designate_track=1.0000    <- rival wins
```

This is recorded here because it is the one thing fork A is *worse* at, and a reader who meets only the "0 clean commands destroyed" figure would not know it exists. It was ruled acceptable on two grounds: **zero occurrences in 699 corpus rows** (it had to be demonstrated synthetically), and the grammar shape it needs — a pattern registered overlapping the phantom's span — is exactly the mitigation §5.6 already recommends for other reasons. It is **not** an acceptance case for this feature: no test pins it, because pinning it would pin a behaviour fork C is expected to change.

### 4.2 F5 — the placement is load-bearing, and the obvious placement is wrong

Three separate reasons put the suppression at the top of the post-selection block rather than at the emit site, and each was a near-miss in the design's own analysis (§9's "three things this feature must not get wrong"):

- **Progress is already guaranteed.** The existing guard has run, so `bestEndIdx > searchStart` holds and the `continue` can never spin. A second guard would be dead code that reads as a real safety check.
- **A barred round must stay zero-alloc.** `ComputeConfidence`, the `VoxrSlotMatch[]` copy and the `VoxrCommand` construction all sit below. Suppressing at the emit site builds and discards all three every barred round — a real cost on the parse path, forbidden by G-5.
- **It cannot corrupt the sibling-tie alignment.** `_tiedSiblingBuf[_resultCount]` is written *before* `_resultCount++`, and the recogniser walks the two buffers in lockstep. Suppressing above that write means a barred round never records a tie for a result slot it does not fill. Suppressing below it leaves a stale record aligned to a slot that never arrives.

The third is the subtlest and the only one that produces *wrong output* rather than merely wasted work, so it is the one to hold onto: this is not an optimisation, it is a correctness constraint that happens to also be the fast path.

### 4.3 F6 — the eager condition is not redundant, and nothing on the flush path can prove it

This is the one place fork A differs from fork B in **reachability**, and it is a hole that would have stayed invisible.

`TryEagerCommit` runs its own selection scan and calls the same `CompareCandidate`. Its own comment records that this is how the admission rule reaches it — *"The eager scan inherits it by sharing the comparator."* Under fork B the bar would have been a comparator count and would have reached the eager path the same free way. **Under fork A it does not.** The bar lives in `ParseInternal`, which `TryEagerCommit` does not call.

Left alone, a leading-missed candidate commits early — **while every flush-path test stays green**, because the flush path is correct and is simply never reached.

**ERRATUM E-1, 2026-08-27 (implementation).** This paragraph originally continued *"An eager commit is a fire, so this defeats G-1 outright"*, echoing design §2.4/§5.2. That is **false**: `Commit` makes the recogniser call `FlushBuffer`, which routes into `ParseInternal`, where the bar refuses the winner anyway. G-1 is never at risk on this path. What the condition actually protects is the **buffer** — `Commit` consumes and clears the accumulated transcript, so without it a half-spoken command is flushed early and thrown away and its continuation is parsed as a separate utterance. F6's requirement is unchanged; only this rationale is.

`None` is the right verdict for the same reason #66 and #70 use it: at the eager gate, refusing costs only latency, and the flush will apply the bar properly once the transcript is final.

**The requirement this generates:** F14's mutation check must confirm the eager test fails *alone*. A mutation that also breaks a flush test proves the fixture was reaching the flush, not the eager gate, and the test is not pinning what it claims.

### 4.4 F3 — the latch's two failure modes

Both are silent, and both produce a bar that is subtly the wrong rule rather than an obviously broken one:

- **Not skipping optionals.** If the latch fires on the first *element* rather than the first *required* element, a pattern led by an unmatched optional is barred for omitting something the author marked skippable. This inverts the whole point of optionality and would bar `["?please", "switch", "to", "weapons"]` on the perfectly ordinary utterance `"switch to weapons"`.
- **Firing on an unmatched optional.** The mirror error: latching `leadingRequiredMissed` in an optional branch. Optional elements contribute to neither side of the required ledger (scoring DR-7) and must contribute nothing here either.

The safe construction follows the ledger exactly: the latch is touched only at the four sites that already increment `matchedRequired` or `missedRequired`, and nowhere else. If a latch site does not sit beside one of those four increments, it is wrong.

### 4.5 F6/F12 — what the eager refusal actually costs, stated honestly

Nothing, in outcome. A leading-missed candidate cannot fire on either path once this feature ships, so the eager condition changes only *when* the non-firing is decided, not *whether*. Its entire value is preventing an early flush that discards the buffer (ERRATUM E-1 — not G-1, which the flush-path bar secures on its own).

The latency it adds is bounded by the buffer window and is paid only by utterances that were going to fire nothing anyway. There is no case where the eager refusal delays a command that would otherwise have fired correctly.

### 4.6 F13 — why the pinned test transforms rather than dies

`Coverage_SequentialExtraction_SparesTheFirstCommandWhenASecondLosesItsWord` pins `scoring.md` §7 D, and half of it is still true and still valuable:

- Its **first assertion** — `cease_fire` scores 1.0000, *"not charged: approach target {target} is matchable from \"target\""* — **is #82's win**, and is the regression guard that proves G-2. It survives verbatim. Editing it is a finding.
- Only `Assert.AreEqual(2, results.Length)` and the `2f/3f` line invert.
- Its **control case** (`"cease fire approach target hotel one"` → both at 1.00) survives unchanged and becomes *more* load-bearing, because it is now the only place in that test where a second command fires at all.

The name is accurate on its first half and misleading on its second, hence the rename. **Deleting this test would be the error** — it would take #82's guard with it (DR-9 makes the same argument for the `scoring.md` example the test pins).

### 4.7 F9 — why #82 cannot regress, mechanically rather than empirically

This was the objection that blocked this direction for two design rounds, and it deserves restating as a requirement because "the tests pass" is a weaker guarantee than what is actually true here.

`IsAdmissibleStart` applies its **own** count test to a `forStartProbe` match and never calls `CompareCandidate`:

```csharp
if (probe.Score > 0f && probe.MissedRequired < probe.MatchedRequired)
    return true;
```

`BuildCoverageTables` consumes that to build `_orphanRun`. So orphan-run **termination** — the mechanism that keeps the first command's 1.0000 — is reached by a code path the bar does not touch. Under DR-3 the bar is not even in the comparator, so it is two removes away.

Note also that the three tests are deliberately distinct: `missed > matched` for admission, `missed < matched` for termination, and the bar is a third, **orthogonal** test — it asks *which* element was missed, not *how many*. Adding it near the other two must not be allowed to blur them.

**The requirement this generates:** F9 asks for a *diff-level* check (those four members byte-identical), not merely a passing test, because that is the form the guarantee actually takes.

### 4.8 F15 — what can actually be measured, and the two instruments that do not work

Two constraints collide here, and getting them wrong produces a test that is green and blind.

**`Parse` can never be pinned at zero allocation.** It allocates the results it returns, and in the Editor it also allocates a `List<ParseDiagnosticEntry>` per call plus its `ToArray()`. The existing sibling alloc test already records this and works around it by measuring over `TryEagerCommit` instead — *"Parse necessarily allocates the results it returns and so can never be pinned at zero."* So "an allocation test over a barred parse" is not a thing that can be written, and F15 does not ask for one.

**`GC.GetAllocatedBytesForCurrentThread` is inert here** — it reads 0 B after a deliberate 1 MB allocation, so an assertion written against it passes whether or not the code allocates. `Is.Not.AllocatingGCMemory()` is the working constraint; `Recorder.Get("GC.Alloc").sampleBlockCount` gives a count where the path must allocate.

What follows is the split F15 states:

- **The latch (Seam A) is pinnable in Unity**, through `TryEagerCommit`. It returns an enum, allocates nothing, and drives `TryMatchScored` over the whole `(command, pattern, startIdx)` triple — which is exactly the code the latch lives in. `Is.Not.AllocatingGCMemory()` holds there, and the existing sibling test is the working precedent to follow, including its hoisted token array and its warm-up call.
- **The flush suppression (Seam B) is pinnable by the rig**, not by Unity. `--bench` runs in ordinary .NET where the byte counter works, and reports bytes/utterance across both arms. Design §7.5 asks for exactly this, and §9's item-1 list names `--bench` and no Unity alloc test.

The mutation for the rig half is specific: move the suppression from F5's position to the emit site and confirm bytes/utterance rises on barred rows. For the Unity half, the mutation is an allocation deliberately introduced in the latch. A green alloc test that survives its mutation is measuring nothing.

### 4.9 F11 — no test seam, and why the rig does not need one

DR-7 forbids a test seam in shipped code, which raises the obvious question of how the A/B arms were produced. They were produced by patching the rig's **staged copy** of the parser — `stage.sh` copies the real sources into a buildable .NET project — never the shipped source. Every prior scoring A/B in this package worked this way, so no seam is owed by the shipped build.

This also means the `--bench` run in F15 measures a *staged* build, not the Unity build. That is the right instrument for µs/utterance and bytes/utterance comparisons between arms; the Unity-side allocation claim is F15's `Is.Not.AllocatingGCMemory()` test, and the two are complementary rather than redundant.

### 4.10 F16 — what the corpus measurement can and cannot support

The design's numbers are already taken and are **not to be re-measured** (design §7). What matters at implementation time is knowing what they bound:

- **48 rows change; 17 fire nothing; 0 clean 1.0000 commands destroyed.** That last figure is the G-6 guarantee and is the reason fork A was ruled over fork B.
- The corpus contains **no row where a legitimate rival wins a round the phantom used to take**, so fork A's cost (denying an overlapping rival) is unexercised by it and was demonstrated only on a synthetic grammar.
- The corpus contains **no pattern whose first required element is a slot**, which is why DR-2 is ruled on principle. F4 is therefore pinned by a **written test**, not by the corpus — the corpus has no opinion about it.

  Canon's three reasons for uniformity are worth carrying whole, since the corpus supplies none of them: it is one count with no element-type branch, which is what §2.1 asks of a rule at this site; §4.1's justification generalises — *"the first required element is the pattern's **anchor**, and a pattern that matched nothing of its anchor has identified nothing, whether that anchor is a verb or a value"*; and *"a type branch here would be a rule the parser cannot explain to an author."*

### 4.11 F17 — the "no edited expectations" rule is a finding detector

The bar changes exactly one thing (§3), and design §7.3 predicts which shapes move. So a test that starts failing and is not one of F13's three lines is telling you something the design did not predict. The rule is: **investigate before editing.** An edited expectation that turns out to be a genuine behaviour change the design did not anticipate is a contradiction with locked design and triggers the workflow's stop-and-reopen, not a green suite.

### 4.12 The three near-misses, collected

Design §9 names three things this feature must not get wrong, each already a near-miss in the analysis. They are F5, F6 and F3 respectively, and they are collected here because they are the checklist for the review pass rather than for the build:

1. The suppression goes **above** the allocations and the `_tiedSiblingBuf` write, not at the emit site (§4.2).
2. The eager condition is **not** optional and **not** redundant (§4.3).
3. The latch must **skip optionals**, and must not fire on an unmatched *optional* leading element (§4.4).

### 4.13 F18 — CHANGELOG, moved here by ruling

Design §9 assigns `CHANGELOG.md` to backlog item 2 (`feat-leading-miss-docs`), alongside the `scoring.md` amendment and the `KNOWN_LIMITATIONS.md` entry. **The human ruled that the `[Unreleased]` entry lands on this branch instead**, following the conventional practice of writing the changelog entry with the change it describes.

**Where that ruling lives.** It was made in the **implementation session of 2026-08-27**, when this feature was picked up — *not* at G1 earlier the same day. So it is deliberately absent from design §10, which records only the G1 rulings (DR-4's reversal, DR-2, the fork-C deferral), and canon is not wrong for omitting it. A reader auditing this line against canon will find §9 unqualified and should stop here rather than conclude the requirements doc invented a ruling; canon is immutable and could not have recorded a decision taken after it was locked.

This is a **scheduling change to the backlog partition, not a design contradiction.** §9 is a dependency-ordered work split; no decision record pins which branch writes the changelog, and DR-9 (the only DR touching documentation) is about `scoring.md` §7 D specifically. Recorded explicitly here so item 2 **extends** the entry rather than originating it, and so a reader comparing this feature against §9 sees why one line item moved.

The entry names no version. DR-10 rules 2.0.0, and item 4's `release` skill is what promotes `[Unreleased]` to a numbered heading.

### 4.14 F16 — the seven acceptance cases

Every row must hold. The first four are design §7.2, measured on the rig in every ON arm; rows 5–6 are §5.7's before/after pair; row 7 is §5.8's gap and is the new test.

| utterance | expected | what it proves |
|---|---|---|
| `time to target track one two four four` | `n=1`, `query_time_to_target=1.0000` | the reported phantom is gone; the legitimate command is unharmed |
| `track one two four four` | `n=0` | the standalone round-1 shape is closed (§1.1) |
| `intercept track one two four four` | `n=1`, `intercept_target=1.0000` | a genuine command with its verb present is untouched |
| `decelerate hard burn` | `n=1`, `decelerate=0.6667` | interior misses untouched (G-3, F8) |
| `cease fire target hotel one` | `n=1`, `cease_fire=1.0000` | §7 D / #82 preserved (G-2, F9) — the first assertion of F13's test |
| `cease fire approach target hotel one` | `n=2`, both `1.0000` | the control: spoken in full, both still fire |
| `target hotel one cease fire` | `cease_fire=1.0000` | **leading debris** — the shield holds; the assertion that catches fork B (F10) |

## 5. Non-functional requirements

- **The parse path stays allocation-free.** `TryMatchScored` runs on the innermost `(command, pattern, startIdx)` triple. The latch must be stack locals — no lists, no closures, no boxing — and `MatchResult` must stay a `struct`. The flush suppression must precede every allocation in the round (§4.2).
- **No new work proportional to grammar size.** The bar adds one field write per required element in a loop that already walks every element. It must not add a second pass, a lookup, or a per-candidate table read.
- **Determinism.** The bar reads only the pattern's own shape and the match's own outcome, so it is independent of registration order, of what else is registered, and of round number. The same `(pattern, tokens, startIdx)` triple must produce the same verdict every time.
- **Editor-only stays Editor-only.** The parse diagnostic keeps recording the barred round. No `#if UNITY_EDITOR` block gains runtime behaviour and no runtime path gains an Editor dependency.
- **The bar is unconditional.** No configuration is read, no field is consulted, and there is no code path in the shipped build that disables it (F11).
- **Verification is human-deferred where it must be.** On-device Quest verification of mic capture and lifecycle delivery is human-only and never claimable from WSL. This feature touches neither the native bridge nor any MonoBehaviour lifecycle path, so no ABI rebuild is owed and no `.so` is committed — but the deferral is stated rather than assumed away.

## 6. Non-goals / deferred

- **Fork C — giving the leading coverage term an admissibility excusal.** Deferred at G1 to **its own design topic**, with its own corpus work. It is the principled fix for the §2.3 asymmetry, it costs zero clean commands, and it composes with the bar (the bar could move into the comparator at that time, collapsing §5.2's two sites back to one). It moves 41 of 699 scores, most of them **upward**, which is a scoring-model change of the same magnitude as #65 §5.2 and must not ride along inside a safety fix. **Do not implement it here.**
- **Item 2's product docs.** `scoring.md` §7 D and its `DocCheck` pin, `command-recognition.md`'s §5.6 authoring guidance, the `KNOWN_LIMITATIONS.md` entry for the recoveries the bar costs. `feat-leading-miss-docs`. The one carve-out is `CHANGELOG.md` (§4.13).
- **Item 3's construction-time warning.** The author-facing detector for the hazardous grammar shape. Editor-only, independently valuable, independently droppable — and item 1 has already *fixed* the shape it detects.
- **Item 4's version bump.** DR-10's 2.0.0, via the `release` skill.
- **The numbers.** `minScore` 0.60 and `coverageWeight` 1.0 are unchanged and out of scope. The bar is explicitly not a threshold change.
- **The 0.067 margin.** That a 3-element pattern missing one element lands on 0.6667, only 0.067 above the gate, is a real discomfort `scoring.md` already flags. The bar does not address it and must not be described as addressing it.
- **Sequential extraction.** Not removed, not capped (design §3, §6.3).
- **Dropped-word recovery.** There is no transcript-level discriminator between *never spoken* and *spoken and lost* (design §2.5, measured against 73 field utterances). The bar routes around the loss; it does not detect it.
- **Acoustic-gap detection and `[unk]` evidence.** Both empirically falsified (§2.5). Recorded here so neither is re-proposed during implementation or review.
- **A distinct event or reject reason for "barred".** DR-6: it leaks a parser-internal rule into the public surface. Zero results reaches the recogniser as `OnUnrecognisedSpeech`, as it already does.
- **Routing barred candidates to slot-fill.** DR-8: there is no command to hold open.

## 7. Dependencies & assumptions

- **Branch:** `feat-leading-miss-bar`, off `main` at `b83b62f` (v1.5.0). No feature dependency — this is item 1 and depends on nothing in its own backlog.
- **Design locked:** `Planning~/design-docs/leading-miss-bar.md`, G1 2026-08-27, all ten DRs ratified. Immutable. A contradiction found while building reopens it on a new design branch and re-locks (§4.11).
- **Measurement is complete and is not to be redone** (design §7). 699-corpus A/B across 5 arms; rig validated against the committed grammar pin; the refuse-to-FIRE arm reproduced byte-identically from two independently written patches. What is still owed at implementation time is only §7.5's list: both Unity suites, `--bench`, and — in item 2 — the `DocCheck` pin.
- **Verified code sites**, read on `main` at `b83b62f`: `ParseInternal:2244`, adopt site `:2362`, progress guard `:2501-2502`, `_tiedSiblingBuf` write `:2537`, emit `:2552`, `MatchResult:2926`, the required ledger `:2985`, `CompareCandidate:3030`, admission `:3071`, `TryMatchScored:3153`, `MatchResult` construction `:3323`, `BuildCoverageTables:3522`, `SkippedBefore:3611`, `OrphanedAfter:3627`, `IsAdmissibleStart:3685`, `TryEagerCommit:3940`, eager adopt `:4016-4018`, the inheritance comment `~:4040`, eager refusals `:4060`/`:4087`. **Design §5.2 cites the progress guard as `:2503`; the verified line is `:2501`.** A stale citation, not a contradiction — the design quotes the code exactly and anchors the placement as "immediately after the existing progress guard", which is unambiguous.
- **`TryEagerCommit` is `internal` and directly callable from tests** (`VoxrEagerCommitTests.cs` already does so at four sites). F6's test needs no seam, which is what makes DR-7 costless here.
- **Assumption, to verify:** the four latch sites in `TryMatchScored` are exactly the four that increment `matchedRequired`/`missedRequired` — two in the slot branch, two in the required-literal branch — and the optional-literal and optional-slot branches touch neither. Read from source, to be re-confirmed against the diff.
- **Environment:** bare UPM package; nothing compiles or tests standalone. All Unity verification runs through host project `D:\Game Development\VoXR TestGround` (Unity 6000.4.7f1), **both EditMode and PlayMode** — most parser tests are PlayMode. Delegated to `compile-check`; never run on the main thread.
- **Environment traps:** the CSharpier PostToolUse hook reformats every `.cs` edit and silently reverts manual de-indents — restructure, do not re-indent. `grep` honours `.gitignore`, and `Planning~/` is gitignored, so repo-wide greps miss the design doc and the rig's C#. The rig's `-p:StartupObject=` does not survive an incremental rebuild — dispatch on an argument (`Verify4.cs` already does).
- **Baseline to re-measure, not assume:** the EditMode and PlayMode pass counts at `b83b62f`, taken before any edit, so F17's "no edited expectations" claim has a floor to compare against.

## 8. Open questions

### 8.1 Feature-doc granularity — RESOLVED 2026-08-27

The handoff asked whether a change this specified needs full requirements *and* architecture docs. **Ruled: both, full-length.**

### 8.2 The `:4040` comment amendment's ownership — RESOLVED by canon

Design §9 row 1 names *"DR-5's eager condition and the `:4040` comment amendment"* explicitly as item 1. It is a source comment, not a product doc. No ruling needed; the backlog already answers it.

### 8.3 `CHANGELOG.md` — RESOLVED 2026-08-27

Moved from item 2 to this branch by human ruling. See §4.13. Item 2 extends the entry rather than originating it.

### 8.4 The barred round is invisible in the Editor parse diagnostic — OPEN, leaning accept-for-now

Sharper than it first looked. The diagnostic block sits **below** the emit site, so DR-3's placement does not merely leave a barred round *unmarked* — it records **no entry for it at all**. An author debugging a grammar sees a round that consumed tokens and produced neither a result nor a diagnostic line: from the diagnostic's point of view the round did not happen.

That is forced rather than chosen. Moving the suppression below the diagnostic to keep the entry would put it below the `VoxrCommand` construction and the `_tiedSiblingBuf` write, which is precisely the placement DR-3 rejects, and the diagnostic block allocates three times per round, which G-5 forbids on a barred path. **So the two cannot both be had at this placement**, and DR-3 already ruled which one wins.

Leaning **accept for this feature**: DR-6 refuses to leak the rule into the public surface, and the author-facing half of this design is item 3, which is explicitly independently droppable. If the answer is that the author does need to see it, the cheap form is a separate, allocation-free counter (barred-round count per parse) rather than a full `ParseDiagnosticEntry`, and it belongs in item 3. Carrying to G2 rather than deciding silently — and flagged for the review pass, since "the diagnostic went quiet" is exactly the kind of consequence a reviewer should be given rather than left to find.

### 8.5 Whether a pattern of only optional elements can reach the bar at all — STATED, not open

§5.1 says a pattern with no required elements has no first required element and the bar is a no-op for it. Such a pattern's `MatchedRequired` and `MissedRequired` are both zero, so it also clears the admission rule vacuously. Whether that shape is *reachable* — whether the constructor permits an all-optional pattern — is not established here. F3 requires the bar be a no-op for it either way, which is safe under both answers.

### 8.6 The vacuous design-branch step — carried from three prior features, unresolved

`Planning~/` is gitignored, so `design-leading-miss-bar` carries zero diff from `main` and the workflow's "merge the design branch to main" step is pure bookkeeping. Noted for the fourth time; still not worth a workflow change on its own.

## Related

- Locked design: `Planning~/design-docs/leading-miss-bar.md` — §1.1, §2.1–2.6, §4.1, §5.1–5.5, §5.8, §6.1, §6.2, §7.2–7.5, §9 row 1, §10; DR-1, DR-2, DR-3, DR-5, DR-6, DR-7, DR-8
- Successors: `feat-leading-miss-docs` (item 2 — DR-9, `DocCheck`, `KNOWN_LIMITATIONS.md`), `feat-leading-miss-warning` (item 3), `chore/release-2.0.0` (item 4 — DR-10)
- Deferred sibling topic: **fork C**, the leading-coverage excusal (design §6.1, §10)
- Closes: issue #124
- Adjacent, not modified: #82 (`IsAdmissibleStart`, orphan-run termination — F9), #65 (the admission rule and the coverage terms), #66 / #70 (the eager completeness refusals the bar sits beside), #74 (the sibling-tie record the suppression must sit above), #41 (the span tie-break in `CompareCandidate`)
- Measurement apparatus: `Planning~/features/coverage-in-selection/ab-rig/issue124/` (`README.md`, `Verify4.cs`, `analyse-arms.py`, `compare-arms.py`, five `corpus-*.tsv`), `ab-rig/stage.sh`, `phase7-corpus.tsv`
