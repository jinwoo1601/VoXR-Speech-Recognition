---
type: architecture
feature: leading-miss-docs
topic: voice
status: draft
updated: 2026-08-27
sources:
  - Planning~/features/leading-miss-docs/requirements.md
  - Planning~/design-docs/leading-miss-bar.md
  - Planning~/features/leading-miss-bar/architecture.md
  - Documentation~/
  - KNOWN_LIMITATIONS.md
  - Runtime/Commands/VoxrCommandParser.cs
  - Planning~/features/coverage-in-selection/ab-rig/
---

# feat-leading-miss-docs — architecture

The deliverable is product documentation, so this doc is not a component design. It is **the fact set every edit draws from**, then **the site-by-site edit map**, then the measurement work that pins the numbers. §3 is the spine; §2 is what keeps §3's forty edits saying the same thing.

## 1. Shape of the change

Eleven files, ~40 sites, one shipped string literal, one test-instrument pin, one corpus re-derivation. No runtime behaviour changes.

| kind | files | why it is here |
|---|---|---|
| guides | `scoring.md`, `command-recognition.md`, `troubleshooting.md`, `editor-testing.md`, `index.md` | where the mechanism is explained and where the wrong advice lives |
| API reference | `api/command-recogniser.md`, `api/batch-test-runner.md`, `api/scriptable-objects.md`, `api/command-definitions.md`, `inspector-authoring.md` | exhaustive cause-lists and three copies of one `allowPartialMatch` claim |
| root | `KNOWN_LIMITATIONS.md`, `CHANGELOG.md` | one broken repro, two reversals, one new entry, one changelog extension |
| code | `Editor/VoxrDebugSessionLog.cs` | a `Readme` string shipped inside every exported session log |

**The one structural fact that makes this a feature rather than a find-and-replace:** the docs do not describe the bar wrongly in forty independent ways. They describe it wrongly in **four** ways, forty times. Fix the four statements in §2, then apply them.

## 2. The fact set — four statements, verified against the merged code

Every edit in §3 is an application of one of these. If an edit cannot be traced to one, it is scope creep.

### S1 — a barred round wins, consumes, and yields nothing

`VoxrCommandParser.ParseInternal:2549`, as merged:

```csharp
if (bestLeadingRequiredMissed)
{
    searchStart = bestEndIdx;
    continue;
}
```

It sits **after** the progress guard and **above** `ComputeConfidence:2557`, the slot-array allocation, the `VoxrCommand` construction, the `_tiedSiblingBuf` write (`:2590`) and the Editor diagnostic block. So a barred round:

- **advances `searchStart`** past the consumed span — this is the whole point, and it is what keeps leading debris off the next command;
- **writes no result**, so `_resultCount` does not advance;
- **records no tie**, so no `tiedRival` appears;
- **logs no attempt**, so the session log and the debug window show nothing for that round.

The last of these is the one the docs get wrong most often, because four separate sites promise one log entry per round.

### S2 — refuse-to-FIRE, not refuse-to-compete

The barred candidate still competes and still wins. This is DR-4, reversed from the G0 ruling on measurement: refuse-to-compete destroys **11 clean 1.0000 commands** in 699 rows, because the phantom is the only thing shielding a later command from leading debris (design §10; item-1 requirements §4.1).

**Consequence for the prose:** "the candidate never became one" is the *admission* refusal, which still exists and is a different thing. A doc that explains a barred round as "no candidate" has described the rejected fork.

### S3 — the bar precedes every downstream diversion

A barred winner is refused before completeness, before `allowPartialMatch`, before `requiresConfirmation`, before the sibling-tie question. So **no pending of any kind opens** — pinned by `LeadingRequiredMiss_OpensNoPending` (`Tests~/Runtime/VoxrPendingCommandTests.cs`).

**Deliberately not claimed anywhere in this feature:** anything about *rivals*. #126 reports that a leading-missed **tied rival** may still be offered as a disambiguation choice, and whether DR-1 extends from winners to rivals is a scope question canon did not rule. Every edit below is phrased about the **round winner**, which is what DR-1 actually rules and what the test pins. This is the F28 discipline in its most load-bearing instance.

### S4 — positional, not arithmetic; no score moves

The bar asks *which* element missed, which no threshold can ask. `minScore`, `coverageWeight` and `allowPartialMatch` cannot reach it, and pattern length cannot outrun it.

**Consequence for the prose:** every remedy the docs currently offer for a low-scoring miss must be checked for whether it addresses a *leading* miss. Three of them do not, and say so with confidence.

**S4's arithmetic corollary — a barred candidate can never score `1.00`.** The latch fires only at a *miss* site (`:3380-3382`, `if (matchedRequired == 0 && missedRequired == 0)` immediately above `missedRequired++`), so `missedRequired ≥ 1` whenever the flag is set. `RequiredLiteralMissPenalty` is `0f` (`:185`), the missed element keeps its denominator slot, and coverage can only enlarge the denominator — so the score is bounded by **`(N−1)/N < 1.00`**, at best `6/7` = `0.86` on a seven-element pattern. Any sentence claiming a barred round "matched at 1.00" is false. **This corrects a claim inherited verbatim from #129** and is the F28 breach the validation pass caught in this doc's own §3.6.

**Inherited erratum (E-1), which bears on the eager text:** the eager condition at `TryEagerCommit:4255` is real and load-bearing, but *not* because a phantom would otherwise fire early — `Commit` routes through `FlushBuffer` → `ProcessParsedResults` → `ParseInternal`, where the bar refuses the winner anyway. It protects the **buffer**. Any doc sentence explaining the eager condition as fire-prevention repeats the erratum.

## 3. The edit map

Priority tiers are #129's. **P1** = a reader who follows this does the wrong thing. **P2** = false. **P3** = silent.

**Phase 2 sweep — the site list is closed by search, not inherited.** Four claim classes were swept over `Documentation~/**`, `KNOWN_LIMITATIONS.md` and `README.md`: *extraction round / lost selection / won selection / never became*; `allowPartialMatch` routes to pending; `OnUnrecognisedSpeech` cause lists; *three or more elements … fires*. Beyond #129 it added `scoring.md:18`, `:72`, `:494`, `troubleshooting.md:48` (from the validation pass) and `command-recognition.md:577`, `KNOWN_LIMITATIONS.md:608-610` (from the sweep itself).

Three sites were examined and **ruled to survive unedited**, recorded so a reviewer does not re-raise them: `scoring.md:397` and `:493` both key on an entry the reader **observed in the log**, and a barred round produces none, so an observed `0.67` still implies a fire; and `command-recognition.md:669-671` enumerates the cases where a no-command utterance is *silent*, which the bar is not — a barred utterance does fire `OnUnrecognisedSpeech`.

### 3.1 `Documentation~/scoring.md` — the worst affected

| site | tier | current | becomes | fact |
|---|---|---|---|---|
| `:70`, `:74` | P2 | the `cease fire` / "fire" row and *"Two elements is the floor"* explain the rejection as **score** (`0.50`, below the gate) | the row is unchanged arithmetically, but the mechanism gains the bar: "fire" misses `cease`, element 0, so this pattern is barred **as well as** under-scoring, and stays refused at any `minScore`. Ride-along ruling — the site is outside DR-9's "§7 D" wording. | S4 |
| `:18` | P2 | the glossary definition of **Winner**: *"Only winners reach the gates, and only winners are logged as a scored attempt"* | **both halves false** for a barred winner — it reaches no gate and is logged nowhere. The definitional site the rest of the document keys off, and **missed by #129**. | S1 |
| `:72` | P2 | *"a single dropped function word no longer silences a pattern of three or more elements"* | false for a **leading** drop, and the same generalisation F14 splits at `troubleshooting.md:79`. Sits **between** `:70` and `:74`, both already edited — the exact drift A1 exists to prevent. **Missed by #129.** | S4 |
| `:148` | P2 | cross-references worked example D as showing the orphan run and a later round landing in the same place | D no longer shows that. The reference moves to the following sentence's caveat, which is the half D still illustrates. | S1 |
| `:175` | — | *"28 candidates were blocked this way and 27 were recovered by a later round"* | **re-derived, not edited** (§4). A "recovery by a later round" is the shape the bar can now refuse. | §4 |
| `:193` | P3 | extraction stops when nothing is admitted, or a match consumes no tokens, or the buffer is full | gains the fourth ending it does not have: a round can win, consume and emit nothing, and extraction continues past it. | S1 |
| `:210`, `:242`, `:263` | P2 | three assertions that `allowPartialMatch` routes an incomplete winner to pending, *at any score*, unconditionally | each gains the bar's precedence: a barred winner opens no pending at any score. `:242` is the primary statement; `:210` and `:263` are cross-references to it and take a clause each. | S3 |
| `:269-282` | P3 | the `OnUnrecognisedSpeech` table | gains a row: *"The winner's first required element was never heard"* → **fires**. Sits with the other diversions that still report. | S1 |
| `:298-307` | P2 | *"A verdict above `None` requires **all** of:"* six conditions, then *"None of the six is implied by its neighbours"* | **seven**. The new condition is *"the winner's first required element matched something"*, and its bullet must state E-1's true rationale — it protects the buffer from being consumed and cleared, not G-1 from a phantom. | S1, E-1 |
| `:438`-`:457` (§7 D) | P2 | the whole worked example: `:445` *"It fires too"*, the `:450-452` JSON entry, `:455` *"both commands fire … which is the shape to expect"*, `:438`'s heading, `:457`'s rationale | **DR-9's amendment: step 1 unchanged, step 2 inverts.** Round 1 still scores `cease fire` at `1.00` — the orphan run still terminates at "target", because the start test is untouched. Round 2 still wins and still consumes `target hotel one` (which is *what keeps step 1 true*) and emits nothing. The closing remark *"say the second command in full and both fire at 1.00"* is promoted to a **second worked trace**, so the #82 half survives. | S1, S2 |
| `:463` | P2 | *"one entry per extraction round … a pattern's absence means it lost selection, not that it was never tried"* | absence now carries a third reading: **won and was barred**. | S1 |
| `:494` | P2 | symptom row *"`score` ≈ 0.5 on a **two**-element pattern → one dropped required literal … half the evidence stays rejected"* | true but narrowed: on two elements the drop is element 0 or element 1, and only the second is a score story. Same split as `:70`/`:74`. **Missed by #129.** *(`:493` and `:397` survive unedited — both key on an entry the reader **observed in the log**, and a barred round produces none.)* | S4 |
| `:497` | P3 | symptom row *"The command that lost a word fired; the one spoken cleanly was rejected at ≈0.40"* | that symptom is now **unreachable**. Replaced by *"a stranded tail produced no second command"* → this is the bar; the fix is a pattern that claims the tail (design §5.6). | S1 |
| `:498` | P2 | *"no result at all for a pattern that clearly part-matched"* → admission refusal | gains the second cause, and the discriminator between them: admission is a **count**, the bar is a **position**. | S1, S2 |

### 3.2 `Documentation~/command-recognition.md`

| site | tier | current | becomes | fact |
|---|---|---|---|---|
| `:41-42` | P3 | the pipeline diagram's `Threshold Filter` stage | the bar gets its **own stage, above** `Threshold Filter` — it is not part of it. | S1, S4 |
| `:439` | **P1** | *"**Making the difference come earlier in the pattern does not help.** … `weapons mode active` and `navigation mode active` tie at `0.67` and fire the first-registered again."* | **reversed.** "mode active" misses element 0 of both patterns, so both are barred and nothing fires — at any length. This moves **out** of "what is not on that list" and **into** the remedy list, where it is now among the more effective options. The `(0 + 1) / 2` arithmetic for the two-element case stays true and stays. | S4 |
| `:523` | P2 | *"decided by completeness alone, independently of `minScore`"* | the bar is consulted first; completeness decides only among winners that were not barred. | S3 |
| `:601` | P3 | two classes of non-preempting utterance | a **third**: a barred utterance neither cancels a live pending nor preempts follow-up slot-fill, because it produces no result to preempt with. | S1, S3 |
| `:577` | P2 | *"A command missing a required argument does not fire on the ordinary path: it is refused outright, or, with `allowPartialMatch`, held pending for slot-fill."* — a two-way dichotomy | a **third** branch: a barred winner is refused before either, so it is neither rejected-for-completeness nor held pending. This is the section the three `allowPartialMatch` copies all link to as *"both ways an incomplete command still fires"*, so it must agree with §3.6. **Found by the Phase 2 sweep; missed by #129.** | S3 |
| `:620-626` | P2 | *"This happens in five situations"* | **six**. The new one: the winner's first required element was never heard. | S1 |
| §5.6 guidance (new) | — | absent | the three grammar-side mitigations, as authoring guidance: **let a legitimate pattern claim the tail**; **register a benign intent for the standalone fragment**; **`requiresConfirmation` on destructive intents**. Canon requires these *"regardless of anything else, because they help users on 1.5.0 today"* (design §9). Placed with the one-word-hazard guidance, which is where a reader with this problem already is. | design §5.6 |

### 3.3 `Documentation~/troubleshooting.md`

| site | tier | current | becomes | fact |
|---|---|---|---|---|
| `:48` | P2 | *"Losing candidates are not shown — a pattern's absence means it lost selection, not that it was never tried."* | identical false claim to `scoring.md:463`, on the page a reader hits **first** with this symptom. **Missed by #129.** | S1 |
| `:77` | P2 | the `~0.50` signature is *"exactly one dropped required literal on a two-element pattern"* | true, and now under-specified: on a two-element pattern the dropped literal is element 0 or element 1, and only the second is a score story. The leading case is barred as well as under-scoring. | S4 |
| `:79` | P2 | *"**On three or more elements this no longer happens.**"* | holds only for a **non-leading** drop. A leading drop on a three-element pattern scores `0.67`, clears the gate, and still does not fire. | S4 |
| `:81` | **P1** | *"For the two-element case, lengthen the pattern or accept the ambiguity."* | split by case. Lengthening still works for a non-leading miss and is kept for it. For a **leading** miss neither lengthening nor any threshold recovers the command — the speaker re-utters, or the grammar is changed per §5.6. | S4 |
| `:97-101` | **P1** | *"there is no scored attempt in the log either, because the candidate never became one"*, with the diagnostic *"count the pattern's required elements against how many the transcript supplied"* | two causes, not one. The admission refusal is unchanged; the bar is added, and for it the candidate **did** become one, won its round and consumed its span. The counting diagnostic cannot find a barred round — the reader is directed to check **which** element missed, not how many. | S1, S2 |

### 3.4 `Documentation~/editor-testing.md`

| site | tier | current | becomes | fact |
|---|---|---|---|---|
| `:24` | P2 | last-match panel shows *"for each command extracted … the pattern that won selection"* | a barred round is extracted from nothing and shown nowhere; absence has the third reading. | S1 |
| `:64` | P2 | *"one entry per decision … Losing candidates are not recorded, so a pattern's absence means it lost selection"* | same correction as `scoring.md:463`, phrased for the log schema. | S1 |
| `:157` | P2 | per-row diagnostics carry *"one line per extraction round"* | a barred round contributes no line. | S1 |
| `:212` | P3 | `expectedIntent` empty means *"no match, below a threshold, or a required slot left unfilled"* — three causes | a **fourth**: the winner's first required element was never heard. | S1 |
| `:217` | P3 | *"That third rejection cause is the completeness check"* | renumbered, since the new cause is inserted before it in the reader's list. | — |

### 3.5 `Documentation~/index.md`

| site | tier | current | becomes | fact |
|---|---|---|---|---|
| `:14` | P3 | the `scoring.md` blurb enumerates the guide's contents | gains the bar, so the hub's table of contents matches the guide. | — |

**Correction to requirements F22 and §9.2, both of which rest on a false premise.** `index.md` contains **no symptom table** — `:14` is a one-line table-of-contents blurb and the file is 35 lines of link lists. The real symptom table is `scoring.md:493-500`, so the "does the hub grow a version caveat?" question belongs there and is answered in §3.1's `:494`/`:497` rows. Found by the validation pass; #129's P3 wording ("`index.md:14`'s symptom table") propagated the error into this feature's contract.

### 3.6 API reference

| site | tier | current | becomes | fact |
|---|---|---|---|---|
| `api/command-recogniser.md:44` | P2 | `OnUnrecognisedSpeech`'s cause list, presented as exhaustive | gains the barred-winner cause, matching `command-recognition.md:620-626`. | S1 |
| `api/command-recogniser.md:45` | P2 | `OnCommandPending`'s trigger list, presented as exhaustive | gains the bar's precedence — a barred winner raises none of the three. | S3 |
| `api/scriptable-objects.md:49`, `api/command-definitions.md:17`, `inspector-authoring.md:40` | P2 | three copies of *"enters pending state when matched with unfilled required slots, instead of being rejected"* | each gains the same clause. **They must agree verbatim** — they are the same claim in three places and have already drifted once. | S3 |
| `api/batch-test-runner.md:44`, `:49`, `:51` | P2 | `VoxrTestResult` fields; `Score` *"`0` when nothing matched at all"* | a barred round never reaches the runner (`Runtime/Testing/VoxrBatchTestRunner.cs:114-123`), so it reports `"no pattern matched"` with `Score` `0` and `Confidence` `-1` **even where the pattern matched every element but its first and scored well above `minScore`** — up to `0.86` on a seven-element pattern, per S4's corollary. `Score` `0` no longer implies nothing matched — the strongest statement in this feature about a *diagnostic* becoming ambiguous. ***Corrected 2026-08-27:*** this row previously read *"even where a pattern matched at `1.00`"*, inherited verbatim from #129 and **false** — a barred candidate cannot reach `1.00`, and had some other pattern done so it would have been accepted. | S1 |

### 3.7 `KNOWN_LIMITATIONS.md`

| site | tier | current | becomes | fact |
|---|---|---|---|---|
| `:428-432` | — | *"28 blocked, 27 recovered"* | **re-derived** (§4), jointly with `scoring.md:175`. | §4 |
| `:603-607` | **P1** | *"**What does not work**: moving the difference *earlier* … Grow the pattern back (`weapons mode active`) and the wrong-command behaviour returns at `0.67`."* | reversed, as `command-recognition.md:439`. It does not return; both candidates are barred at any length. Moves out of "what does not work". | S4 |
| `:661-664` | **P1** | the repro: register `["cease","fire"]` / `["resume","fire"]`, lower `minScore` to `0.4`, drop the first word | **does not reproduce.** The dropped word is each pattern's first required element, so both are barred; the tie is not live at `0.4` or any value and nothing fires. Replaced with a **non-leading** discriminator — the entry is about the warning being silent below the default gate, and that shape is still real; only the fixture must change. | S1, S4 |
| `:678-690` | **P1** | *"**Workaround**: Turn on `disambiguateSiblingTies`"*, argued through the runtime-vs-warning asymmetry | inert on the entry's *current* repro, because a barred round opens no pending. Once the repro is fixed per the row above, the workaround becomes live again and the asymmetry argument stands. **Phrased about the winner only** (S3). | S3 |
| `:608-610` | P3 | the entry's closing **Note**: *"reducing the miss cost extends it down to three-element patterns"* | true only for a **non-leading** discriminator; carries the same split the `:603-607` correction introduces, and sits in the same entry, so it must not be left contradicting it. **Found by the Phase 2 sweep.** | S4 |
| `:691-693` | — | *"Warning unconditionally would put a 'the wrong intent can fire' claim in front of every author … for grammars where nothing fires at all."* | **left alone.** This is item 3's territory and the G0 ruling put it out of scope. Flagged here so a reviewer sees it was considered, not missed. | §5 |
| new entry | P3 | absent | **the bar's own cost**: where the leading word *was* spoken and the decoder dropped it, the command used to be recovered at a reduced score and is now silent. No transcript-level discriminator exists between "never spoken" and "spoken and lost"; the only remedy is re-uttering. Corpus figure from design §7.3, re-checked in §4. Placed near the entries at `:336` and `:550`. | S2, S4 |

### 3.8 `Editor/VoxrDebugSessionLog.cs:31-35` — the one code edit

The `Readme` string embedded in every exported session log claims each attempt *"is one extraction round, reporting the command pattern that won selection that round"*. False under S1. **String literal only; no logic moves.**

**CSharpier hazard** (project memory): the PostToolUse hook reformats every `.cs` edit and silently reverts manual de-indents. If the corrected string re-wraps badly, **restructure it — do not re-indent it.**

### 3.9 `CHANGELOG.md`

Item 1's `[Unreleased]` entry (line 12) is **extended, not replaced**. `git log` must show item 1's lines surviving. No version number is introduced (DR-10 / item 4).

## 4. The numbers — re-derived, not edited

Two published figures are the only quantitative claims this feature touches, and they are the same figure in two places: `scoring.md:175` and `KNOWN_LIMITATIONS.md:428-432`, *"28 candidates were blocked this way and 27 were recovered by a later round"*.

**Why it cannot be hand-adjusted.** The claim is about the key-1 bound: a better-scoring candidate that starts later is never promoted over a demoted one starting earlier, and *sequential extraction normally recovers it on the next round*. A **recovery by a later round** is precisely the event the bar may now refuse — if that later round's winner is itself leading-missed, it wins, consumes, and emits nothing. Whether that happens on this corpus, and how often, is measurable and is not guessable.

**What the bar can and cannot move.** The barred path's `searchStart = bestEndIdx` (`:2551`) is byte-identical to the emitting path's (`:2636`), so **the bar can only remove results — it cannot change which later rounds occur.** Two consequences the re-derivation must respect: `blocked` (28) is computed from a bar-blind candidate enumeration and **cannot move**; only `recovered` / `silent` can, and only where the recovering command is itself leading-missed. That is the input to the §9.4 judgment.

**The instrument already exists.** `Planning~/features/coverage-in-selection/ab-rig/Sweep.cs` is the program that produced both the original "29 blocked, 28 recovered" and the post-#82 "28 blocked, 27 recovered". Because item 1 has merged, `stage.sh WORKTREE` now stages the **shipped** parser, so no patch is needed — the arm is simply the current code.

```bash
cd "Planning~/features/coverage-in-selection/ab-rig"
./stage.sh WORKTREE /tmp/.../sweep-after
cd /tmp/.../sweep-after && dotnet build -v q --nologo
dotnet run --no-build -- --sweep < ../phase7-corpus.tsv
```

**Three traps, all previously paid for** (project memory; rig README):

1. `-p:StartupObject=` **does not survive an incremental rebuild** — it produces false greens. Dispatch from `Program.Main` instead.
2. `stage.sh:27` uses `rm -rf`, which is **blocked** in this environment. Stage into a fresh directory, or pre-clear with `find … -delete`.
3. **Validate before trusting any delta.** The rig README's own discipline: `--grammar` must be byte-identical to the committed `grammar` pin in `NativeBridge~/harness/expectations.json` (133 words), and a bar-off replay must reproduce `corpus-bar-off.tsv`. A clean run is not by itself evidence.

### 4.1 Results — measured 2026-08-27

**Both validation gates passed before any delta was read.** `--grammar` byte-identical to the committed 133-word pin (`NativeBridge~/harness/expectations.json`) on both arms; the default replay byte-identical to `corpus-bar-off.tsv` and `corpus-bar-on.tsv` respectively. The corpus must be fed as **column 2 only** (`cut -f2 phase7-corpus.tsv`) — the replay reads the whole stdin line, so passing the raw TSV silently charges the category label as a leading token and every score is wrong.

**F24 — the published sweep figure is UNCHANGED. No edit is owed to `scoring.md:175` or `KNOWN_LIMITATIONS.md:428-432`.**

| arm | blocked | recovered | genuinely silent | restatement mismatches |
|---|---|---|---|---|
| bar-off (`b83b62f`) | 28 | 27 | 1 | 0 |
| **bar-on (`41ea036`, shipped)** | **28** | **27** | **1** | 0 |

The bar-off arm reproduces the published figure exactly, which is the strongest available validation — the instrument reproduces the very number under test. The single silent row is still `"switch navigation mode"`, exactly as `KNOWN_LIMITATIONS.md:424-427` describes.

**Why it does not move, which is the part worth documenting.** #129 reasoned that *"a recovery by a later round is exactly what the bar now refuses"*. That is true only conditionally, and the condition does not occur here. The barred path's `searchStart = bestEndIdx` (`:2551`) is byte-identical to the emitting path's (`:2636`), so **the bar can only remove results — it cannot change which later rounds occur.** `blocked` is computed from a bar-blind candidate enumeration and is structurally immovable; `recovered` could only fall if some recovering round's own winner were leading-missed, and on this corpus none is.

**The self-check had to be repaired before the number could be read** (validation finding 9). `Sweep.cs` restates the selection key order locally and checks it against `Parse`; the restatement is bar-blind while `Parse` is bar-aware, so on the shipped arm **63 rows** have a barred round-1 winner and would have reported as restatement failures against a gate labelled *"must be 0"*. Those rows are now counted separately. Both arms report 0 genuine mismatches.

**F19 — the bar's own cost, measured; canon's two figures resolved.** From the committed arms (no build needed), gated at `0.60`:

| | rows |
|---|---|
| rows changed | 48 |
| of which a survivor's score drops | **0** |
| clean `1.0000` commands destroyed | **0** (G-6) |
| rows that now fire nothing | 17 |
| **of those, genuine leading-drop losses** (all `delete`) | **9** |
| of those, phantoms suppressed | 8 |

This reproduces design §7.3's arm-A row exactly. **The two canon figures were counting different things and both are now pinned:** #129 P3's *"17 of 699"* is the whole fire-nothing set — costs **and** fixes together — while design §9/§7.3's *"9–12"* is the genuine cost, whose measured value is **9**. All nine are `delete`-category rows, i.e. a legitimately spoken command whose leading word the decoder dropped: `"one missiles target hotel one"` (`launch_weapon` 0.80), `"heading two seven zero"` (`set_heading` 0.67), `"to weapons"` (`mode_weapons` 0.67), and six more. **The new `KNOWN_LIMITATIONS.md` entry must publish 9, not 17 and not a range.**

**If the shape does not survive.** Requirements §9.4 leaves this open deliberately: should the re-derivation show the "recovered by a later round" mechanism is largely gone, the honest correction is to **drop the sweep figure and state the mechanism**, not to publish a smaller number for a claim that no longer means what it did.

**The `DocCheck` pin (F25).** `DocCheck.cs` verifies every numeric claim the scoring docs make against the real parser. It gains the amended §7 D trace — both rounds of it, including the second worked trace — and must **fail if F7's amendment is reverted**. Item-1 architecture §7 defers this pin here explicitly, so it is owed, not optional. Validate `DocCheck` against the committed pin **before** trusting its output.

## 5. Load-bearing decisions

| # | decision | why | rejected alternative |
|---|---|---|---|
| A1 | **Four canonical statements (§2), applied ~40 times** — rather than 40 independently-worded corrections | The failure mode of a large doc pass is drift: the same fact stated four slightly different ways, one of which is wrong. The three `allowPartialMatch` copies have already drifted once. | Edit each site on its own terms. Faster per site, and how the current inconsistencies arose. |
| A2 | **Every claim is phrased about the round *winner*** | DR-1 rules on a candidate that wins its round; that is what the test pins. #126 reports a rival-side path and canon has not ruled it. | Claim the general case ("a leading-missed candidate never fires"). Simpler prose, and **false** while #126 stands. |
| A3 | **§7 D is amended, keeping the same grammar and utterance** | DR-9, locked. The valuable half is step 1 — the orphan-run termination that issue #82 bought — and it is untouched by the bar. Deleting the example loses it. | Delete D and write a fresh example. Loses the #82 half, which is still correct and still the point. |
| A4 | **The `~0.50` and "three or more elements" text is split by case, not rewritten** | Both statements are *true for a non-leading miss*, which is the common case a reader arrives with. Rewriting wholesale would lose correct guidance to fix an incomplete one. | Replace with bar-first prose. Over-corrects: most `~0.50` reports are still ordinary score rejections. |
| A5 | **The `VoxrDebugSessionLog` string rides along** (G0 ruling) | It is a user-facing claim shipped in every exported log, false under S1, and would otherwise need its own branch, PR and full two-suite run for one string literal. | A separate issue. Keeps the feature purely documentation, but ships a known-false string in 2.0.0. |
| A6 | **The sibling over-warning is documented nowhere** (G0 ruling) | Item 3 owns it. | A known-limitation entry disclosing it (#129 P3's proposal). Rejected at G0 — with the consequence in §6. |
| A7 | **#128's two unpinned tests are not written here** | They are test work, not documentation. #128 offers item 2 *or* item 3 as home; this feature's scope ruling was the doc surface. | Write them here. Would make a docs branch carry runtime-behaviour tests and a Unity test-authoring risk for no doc benefit. |

## 6. Risks and fragile parts

- **Drift across the ~40 sites is the primary risk**, and it is the one A1 exists to manage. The three `allowPartialMatch` copies and the two copies of the reversed "make the difference earlier" claim must be checked against each other, not just against the code.
- **Over-correction is the second risk.** Most low-score reports are still ordinary score rejections; a reader arriving at `troubleshooting.md` with a garbled utterance must not be pushed toward the bar. A4 is the mitigation.
- **The unreachable symptom row (`scoring.md:497`).** Removing a row from a symptom-first table is the kind of edit that quietly loses a real diagnosis. It is genuinely unreachable — the ≈0.40 rejection was the *pre-#82* behaviour and the bar removes what replaced it — but this deserves a reviewer's eye rather than a silent deletion.
- **Anchors.** Several corrections retarget headings other pages link to by fragment (`#d-two-commands-in-one-breath-and-one-of-them-loses-a-word` has exactly one referrer repo-wide, `scoring.md:148` — itself an edit site; `:497` names "§7 D" as prose, not as a fragment link). F29 is a whole-repo link check, not a per-file one.
- **Item 3 is now a hard dependency of the 2.0.0 release** (A6). Canon calls item 3 *"independently droppable"*; after the G0 ruling it is not, because dropping it ships a false author-facing warning with nothing disclosing it. **This belongs on #130** and is the one piece of bookkeeping this feature owes outside its own tree.
- **The `[Unreleased]` entry already carries two unpinned claims** (#128). F26's extension must not add a third.

## 7. Open questions

1. **`index.md:14`'s symptom row** — remove the unreachable one, or mark it as pre-2.0.0? Bears on whether the hub grows a version caveat. (Requirements §9.2.)
2. **F24's shape** — resolved only by the §4 run. (Requirements §9.4.)
3. **F4's replacement repro** needs a live non-leading sibling pair. Whether the demo grammar supplies one, or the entry must introduce a fixture, is settled while editing. (Requirements §9.1.)

## 8. Build plan

### Persisted plan (2026-08-27)

**Approved by the human 2026-08-27**, after an independent `plan-validator` pass that returned 21 findings — 1 BLOCKER, 10 MAJOR. All were folded in before approval, and three required human rulings (Phase 0's canon disposition, Phase 3's measurement scope, Phase 2's sweep). This feature is a single coherent phase set, so there is one plan.

Persisted verbatim from plan mode, which is otherwise lost at session end. Source plan file: `/home/seraphim/.claude/plans/mutable-watching-feigenbaum.md`.

**Three defects the validation pass found in *this doc*, corrected in Phase 1:** §3.6's `"even where a pattern matched at 1.00"` (false — a barred candidate's score is bounded by `(N−1)/N`); §3.5's `index.md` symptom-table premise (that file has no symptom table); and §3's site list, which inherited #129's omissions.

#### Phases

##### Phase 0 — canon erratum E-2 (BLOCKER; ruled)

The G0 ruling that item 2 stays silent on the sibling over-warning **falsified locked canon**: design §9 calls item 3 *"independently valuable and independently droppable"*, and it no longer is — dropping it now ships a false author-facing claim with nothing disclosing it.

**Ruled: erratum plus an in-canon pointer. No design branch, no G1 re-lock.** Same disposition item 1 used for E-1 and the §6.2 error.

- Annotate `Planning~/design-docs/leading-miss-bar.md` §9 item 3 with the E-2 note (the doc is immutable; erratum annotation is the established pattern, precedent `:103`, `:205`, `:424`).
- Record E-2 in full in `…/features/leading-miss-docs/architecture.md`, in the shape of item 1's §10 "Canon-authority calls" — quote the falsified sentence, the ruling, the location table.
- Post the dependency change to **#130**: item 3 is now a hard dependency of item 4.

→ **Verify:** the erratum appears at all three locations, and no DR ruling is altered — only the droppability claim.

##### Phase 1 — reconcile requirements and architecture to the audit

Validation found defects in this feature's own process docs. Fix them before implementing against them.

- **`architecture.md` §3.6 and F16** — strike the `"even where a pattern matched at 1.00"` claim; replace with the (N−1)/N bound. Inherited verbatim from #129, and false.
- **F22 / requirements §9.2 rest on a false premise** — `Documentation~/index.md` contains **no symptom table**; `:14` is a one-line table-of-contents blurb. The real symptom table is `scoring.md:493-500`. Re-scope F22, and move the version-caveat question to where it applies.
- **F11's count** — F11 says "all four sites"; there are at least **seven** (see Phase 2).
- **F25 is wider than the draft plan honoured** — it requires the pin to cover *every* published number this feature touches, not just §7 D (see Phase 10).
- **F19's mechanism** — record that `blocked` cannot move: the barred path's `searchStart = bestEndIdx` (`:2551`) is identical to the emit path's (`:2636`), so **the bar can only remove results; it cannot change which later rounds occur.**

##### Phase 2 — independent claim sweep (ruled)

#129 missed at least four false sites, including `scoring.md:18` — the glossary definition of **Winner** (*"only winners are logged as a scored attempt"*), the definitional site the whole document keys off. That is evidence the audit was not exhaustive, so run one independent pass rather than trusting it.

Known additions: `scoring.md:18`, `scoring.md:72` (*"no longer silences a pattern of three or more elements"* — false for a leading drop, and it sits **between** `:70` and `:74`, both already being edited), `scoring.md:494`, `troubleshooting.md:48`.

Sweep `Documentation~/**` + `KNOWN_LIMITATIONS.md` + `README.md` on the four claim classes: *extraction round / lost selection / won selection / never became*; `allowPartialMatch` routes to pending; `OnUnrecognisedSpeech` cause lists; *three or more elements … fires*.

→ **Verify:** the site list is closed by search, not by inheritance. Fold anything new into the right file phase below.

##### Phase 3 — measurement (ruled: do the rig surgery)

**3a. Make the instrument runnable.** `Sweep.cs` produced the published figure but cannot currently be invoked: `stage.sh:41-42` does not copy it, `Program.cs:154-260` has no `--sweep` dispatch, and `Sweep.Main()` is private. All three files are gitignored scratch, so nothing ships.

- `ab-rig/stage.sh` — copy `Sweep.cs` alongside the other sources.
- `ab-rig/Program.cs` — add a `--sweep` dispatch. **Not** `-p:StartupObject=`, which does not survive an incremental rebuild and yields false greens.
- `ab-rig/Sweep.cs` — make `Main` reachable, and **fix its bar-blind self-check**: the local key-order restatement applies only the admission rule, while `Parse` is bar-aware, so its `"winner restatement mismatches: must be 0"` gate is structurally unable to read 0 on exactly the leading-debris rows of interest.
- `stage.sh:27`'s `rm -rf "$OUT"` runs **unconditionally** and is blocked here — a fresh directory does not avoid it. Pre-clear with `find … -delete`, or patch that line in the scratch copy.

**3b. Two arms.** `stage.sh WORKTREE` → bar-on; `stage.sh b83b62f` → bar-off. Validate before trusting any delta: `--grammar` byte-identical to the `grammar` pin in `NativeBridge~/harness/expectations.json` (133 words), and the bar-off replay reproducing `corpus-bar-off.tsv`.

**3c. F24.** Re-derive the *recovered* half only — `blocked` (28) is immovable by the Phase 1 derivation. If the recovery shape is largely gone, requirements §9.4 sanctions dropping the figure for the mechanism rather than publishing a smaller number for a changed claim.

**3d. The bar's-own-cost figure (F19/Phase 8), which the draft plan left unmeasured.** Canon gives two irreconcilable values: #129 P3 says *"17 of 699"* (rows firing nothing) and design §7.3 says *"9–12"* (genuine recoveries lost). They count different things. Derive from the **already-committed** TSVs with `issue124/analyse-arms.py` / `compare-arms.py` — no build needed. Writing a soft range in unmeasured would be an F28 breach.

→ **Verify:** validation arms pass first; every published number traced to a recorded command line.

##### Phase 4 — `Documentation~/scoring.md` (anchor)

Everything cross-references this file, and §7 D is the canonical worked trace.

`:18` (glossary **Winner** — both halves false) · `:70`/`:74` (§1 ride-along; mechanism gains the bar) · `:72` (the 3+-elements generalisation) · `:148` (move the worked-example-D cross-reference) · `:175` (Phase 3c) · `:193` (fourth ending for extraction) · `:210`/`:242`/`:263` (bar precedes `allowPartialMatch`; no pending at any score) · `:269-282` (`OnUnrecognisedSpeech` row) · `:298-307` **plus `:309-313`** (six conditions → **seven**; the independence bullets live in the *second* range, and condition (6) has no bullet today, so "which is why each exists" is already under-evidenced) · **`:438-457` — DR-9's amendment** (step 1 unchanged, step 2 inverts; promote the closing remark to a second worked trace so the #82 half survives) · `:463` (absence gains its third reading) · `:494` (the ~0.5 two-element row) · `:497` (the ≈0.40 symptom row is now unreachable → *"a stranded tail produced no second command"*) · `:498` (admission is a **count**, the bar is a **position**).

The new eager bullet must state **E-1's true rationale**: the condition protects the **buffer**, not G-1. `Commit` routes through `FlushBuffer` → `ProcessParsedResults` → `ParseInternal`, where the bar refuses the winner anyway.

##### Phase 5 — `Documentation~/command-recognition.md`

- `:37-38` — the bar becomes its own pipeline stage above `Threshold Filter`. (#129 cites `:41-42`, which is the *claim*, not the edit site.) ⚠ **Flag for review, do not silently resolve:** the bar lives inside `ParseInternal`, which the diagram already calls **Sequential Extraction**, so drawing it as a stage *between* extraction and the filter implies a command is produced then dropped — the opposite of S1. F20 mandates the wording; the reviewer decides.
- **`:439` — the P1 reversal.** "Make the difference come earlier" moves *out* of "what is not on that list" and *into* the remedy list. The two-element `(0+1)/2` arithmetic stays; the "grows back at 0.67" conclusion goes.
- `:523` — the bar is consulted before completeness.
- `:601` — ⚠ **re-derive before editing.** The line names exactly **one** non-preempting class, not two, and a barred utterance may not be a new class at all: it produces no accepted command, which is already the general case. Both the ordinal and the categorisation are unevidenced in #129 and in architecture §3.2.
- `:620-626` — five situations become **six**.
- **§5.6 guidance (new)** — the three grammar-side mitigations, placed with the one-word-hazard guidance. Canon requires them regardless of anything else, because they help users on 1.5.0 today. Any measured figure carried across (`0.8000`, "wins on score, not registration order") must be pinned in Phase 10.

##### Phase 6 — `troubleshooting.md`, `editor-testing.md`, `index.md`

- `troubleshooting.md:48` — same false claim as `scoring.md:463`, on the page a reader hits **first** with this symptom.
- `:77`/`:79` — split the ~0.50 signature and the "three or more elements" claim by leading vs non-leading. **Both halves stay true**; do not over-correct, since most low-score reports are still ordinary score rejections.
- **`:81` — the P1 reversal.** Lengthening kept for the non-leading case; ruled out for the leading case.
- **`:97-101` — the P1 diagnostic.** Two causes now. The counting diagnostic cannot find a barred round — direct the reader to **which** element missed.
- `editor-testing.md:24`/`:64`/`:157` — a barred round contributes no entry or line. `:212` gains a fourth rejection cause; `:217` renumbers.
- `index.md:14` — the table-of-contents blurb only. **It has no symptom table** (Phase 1); the version-caveat question belongs at `scoring.md:493-500`.

##### Phase 7 — API reference (5 files)

- `api/command-recogniser.md:44`/`:45` — the two exhaustive lists gain the bar.
- `api/scriptable-objects.md:49`, `api/command-definitions.md:17`, `inspector-authoring.md:40` — three copies of one `allowPartialMatch` claim. **One pass, agreeing verbatim** — they have drifted once already.
- `api/batch-test-runner.md:44`/`:49`/`:51` — a barred round never reaches the runner (`VoxrBatchTestRunner.cs:114-123`), so it reports `"no pattern matched"`, `Score` `0`, `Confidence` `-1`. **Use the (N−1)/N bound, not "1.00".** `Score 0` no longer implies nothing matched.

##### Phase 8 — `KNOWN_LIMITATIONS.md`

- `:428-432` — apply Phase 3c, jointly with `scoring.md:175`.
- **`:603-607` — the P1 reversal** (cross-check against `command-recognition.md:439`).
- **`:661-664` — the broken repro.** The current one drops each pattern's first required element, so both candidates are barred and the tie is not live at any `minScore`. Replace with a **non-leading** discriminator; the entry's subject is still real, only the fixture must change. The new grammar/utterance/score triple **must be pinned in Phase 10** — it is invented here and nothing else checks it.
- `:678-690` — the `disambiguateSiblingTies` workaround becomes live again once the repro is fixed. Phrase about the **winner** only.
- `:691-693` — **leave alone.** Item 3's territory; out by G0 ruling, and now covered by E-2.
- **New entry — the bar's own cost.** Where the leading word *was* spoken and the decoder dropped it, the command used to be recovered at a reduced score and is now silent. No transcript-level discriminator exists between "never spoken" and "spoken and lost"; the only remedy is re-uttering. Figure from Phase 3d. Place near `:336` / `:550`.

##### Phase 9 — `Editor/VoxrDebugSessionLog.cs:31-35`

The `Readme` string shipped in every exported session log claims each attempt "is one extraction round, reporting the command pattern that won selection that round". False under S1. **String literal only; no logic moves.** No test asserts its content.

⚠ CSharpier PostToolUse hook reformats every `.cs` edit and silently reverts manual de-indents — **restructure, do not re-indent.**

##### Phase 10 — the `DocCheck` pin (F25)

**This is a repair, not an addition.** `DocCheck.cs:460-463` is **already red against merged `main`**: it asserts `"cease_fire:1.000 | approach_target:0.667[target=hotel one]"`, and `approach_target` is now barred, so `ParseSummary` returns `cease_fire:1.000` alone. Item-1 architecture §7 deferred the pin here for exactly this reason. `:462` is the **only** affected assertion of the fourteen.

Widen to F25's actual scope — pin every published number this feature touches: the amended §7 D (both rounds), Phase 3c's re-derived figure, **Phase 8's replacement repro**, Phase 5's §5.6 figures, and the §1 `:70`/`:74` row.

→ **Verify:** run `DocCheck` as committed and confirm it fails at `:462` (the expected red — *not* a validation failure), then green after the amendment, then confirm it goes red again if §7 D is reverted. A pin that cannot fail is not a pin.

⚠ **State at G2:** `.gitignore:44` matches `Planning~/`, so this whole deliverable is invisible in the commit, the diff and `review-pr`. F25 is a **Must** whose evidence must be reported in the PR body rather than shown.

##### Phase 11 — `CHANGELOG.md`

**Extend** item 1's `[Unreleased]` entry; do not replace it (`git log` must show item 1's lines surviving). No version number — item 4's job.

One line naming the reversed guidance, because a user on 1.5.0 who read the old advice needs to know it changed. #128 records **three** downstream-subtraction claims, of which **two** are unpinned; add no further unpinned claim.

##### Phase 12 — verification

1. **Anchor/link sweep (F29)** — whole-repo. The one anchor cited in review, `#d-two-commands-in-one-breath-and-one-of-them-loses-a-word`, is referenced from **`scoring.md:148`** (not `:497`, which names "§7 D" as prose) — and `:148` is itself a Phase 4 edit.
2. **`compile-check`, both suites.** Baseline: EditMode **133/133**, PlayMode **569/569** — confirmed correct for `41ea036`. Nothing here should move either.
3. **`review-cycle`** on the branch. Docs need adversarial checking as much as code, and this feature is nothing but claims.

**Deferred, human-only:** on-device Quest verification. No native bridge, no MonoBehaviour lifecycle, no audio path touched. Stated, never claimed.

#### Risks

- **Drift across ~45 sites** — hence four canonical statements applied, not 45 rewordings. Cross-check the paired sites: the three `allowPartialMatch` copies, and the two copies of the reversed "make the difference earlier" claim.
- **Inherited false claims.** The "1.00" error came verbatim from #129 into requirements, architecture and the plan before anything caught it. #129 is evidence, not canon — every number gets re-derived.
- **Over-correction.** A reader arriving with a garbled utterance must not be pushed toward the bar.
- **Removing `scoring.md:497`'s row** could quietly lose a real diagnosis. It is genuinely unreachable, but wants a reviewer's eye.
- **Two sites are flagged for reviewer judgment rather than silent resolution:** the pipeline-diagram stage placement (Phase 5) and `:601`'s ordinal (Phase 5).

#### Out of scope

No behaviour change. No version number. #126/#127/#128 stay open — including #128's two unpinned tests, which are test work, not documentation. Fork C has no user-facing existence. The sibling over-warning belongs to item 3.

#### Close-out

Commit → push → `gh pr create` referencing **#130** (not "Closes #124" — already closed) → `review-pr` at full profile → present for **G2**. Never merge, never tag.

`gh pr edit` and `gh issue view` fail on this repo (projectCards deprecation) — use `gh api`.

## 9. Canon-authority calls

### ERRATUM E-2 — "independently droppable" is falsified by item 2's G0 ruling — RULED 2026-08-27 (human, at G0)

**The falsified sentence.** Design §9 item 3, inside a section headed *"Feature backlog — CANONICAL, locked at G1"*:

> *Independently valuable and independently droppable* — it detects the shape rather than fixing it, and item 1 has already fixed it.

**What falsified it.** At item 2's G0 the human ruled that item 2 documents **nothing** about the construction-time sibling over-warning (#129 P3 had proposed disclosing it as a known limitation). The warning claims *"the wrong intent can fire"* about grammars where a leading discriminator at `D ≥ 3` clears the `(D−1)/D` reachability test and yet can never fire, because the bar refuses it — deliberately deferred in code at `VoxrCommandParser.cs:1646-1647`. With item 2 silent, dropping item 3 before item 4 ships a false author-facing claim with nothing disclosing it. **Item 3 is now a hard dependency of item 4.**

**Ruled: erratum plus an in-canon pointer. No design branch, no G1 re-lock.** On the precedent of E-1 and the §6.2 error, both ruled the same way during item 1. The reasoning: item 3's scope, value and DR set are exactly as canon writes them, and no decision record changes — what changed is the backlog's *dependency structure*, which is scheduling rather than design. Expanding item 2's surface beyond §9's line-item list was ruled the same way, on the same distinction.

**Why this was raised rather than absorbed.** The draft plan disposed of it by booking a note to #130. The `plan-validator` pass called that a BLOCKER, correctly: the shared workflow says *"If building reveals a locked decision was wrong, **stop** … Never silently patch the design mid-implementation"*, and the disposition is the human's to make. Recorded here because the same self-authorising shortcut is available to the next reader.

| document | site | action |
|---|---|---|
| design (LOCKED) | §9 item 3 — *"independently droppable"* | erratum note inserted after it |
| architecture (this doc) | §9 (here) | full record |
| requirements | §6, "Consequence of the G0 ruling on item 3" | corrected in place, pointing here |
| GitHub | #130's backlog table and its "Item 3 has grown a dependency" section | posted at close-out |

**Not an erratum, but recorded beside it:** #129 is *evidence*, not canon, and the validation pass found it carries at least one false claim of its own (the `1.00` in its `api/batch-test-runner.md` row, §3.6 below) and misses at least four true sites (§3.1, §3.3). Requirements F28 exists for exactly this; the plan's Phase 2 closes the site list by search rather than by inheritance.

## 10. As-built deltas

Where the build diverged from §3's edit map. Recorded because this doc, not the plan, is the record.

**1. Two new sections were written, which the edit map did not anticipate.** §3 assumed each site would explain the rule in place. That is exactly the drift A1 warns about, so instead:

- `scoring.md` §5 gained **"The leading-required-miss bar"** — the canonical statement, and the anchor (`#the-leading-required-miss-bar`) that all 20-odd other sites now link to rather than restate. It is where S1–S4 live in the reader's world.
- `command-recognition.md` gained **"A bare pattern's tail can be read as another command"**, under *Authoring hazards*, carrying F27's three mitigations.

**2. §7 D's heading changed, so its anchor changed.** *"…loses a word"* → *"…loses its first word"*. Exactly one referrer exists repo-wide (`scoring.md:148`) and was updated in the same edit. The F29 sweep confirms all intra-doc anchors across 20 files resolve.

**3. The remedy list at `command-recognition.md:434-437` was renumbered 1–5.** The reversal (F2) is inserted as the new item 2 rather than merely removed from "what does not help", so item 4's *"Worth doing alongside 2"* became *"alongside 3"*. Renumbering was forced; the plan did not name it.

**4. The pipeline diagram departs from F20's literal wording, deliberately.** F20 says the bar becomes "its own stage, before the Threshold Filter". Drawn as a separate stage box that reads as *a command is produced, then dropped* — the opposite of S1. As built it is a labelled step **within** the Command Parser stage, stated as happening "before any threshold", which serves F20's intent (visible, distinct, not part of the filter) without asserting something false. Flagged in the plan for a reviewer to rule rather than resolved silently; still open to being overruled.

**5. `DocCheck` had TWO red assertions against merged `main`, not one.** The validation pass derived that `:462` (§7 D) was "the only affected assertion of the fourteen". Running it found a second: `scoring.md §1` *"2-element floor"* expected `cease_fire:0.500` for the utterance `"fire"` and now gets `(none)` — because that candidate is *barred*, not merely under the gate. This is worth keeping because it **independently confirms the `:70`/`:74` ride-along edit was necessary**: the site's stated mechanism really did change, which is what the G0 ruling assumed on inspection alone.

**6. A `--doccheck` dispatch was added to `Program.cs`** beside `--sweep`, for the same reason: `-p:StartupObject=AbRig.DocCheck` does not survive an incremental rebuild and yields false greens.

**7. F24 required no documentation edit at all.** The figure is unchanged (§4.1). A re-sweep sentence was added to `KNOWN_LIMITATIONS.md:428-432` in the entry's own established style — it already records the #82 re-sweep the same way — because "we checked and it did not move" is worth more to a future reader than silence.

**8. The new known-limitation entry was placed after *"Nothing fires although a better-scoring match exists later in the utterance"***, not at the planned `:336` / `:550` neighbours. The two entries are the closest thematic pair in the file: both are "nothing fired and you expected something to".

**9. F19's figure is 9, measured — not 17, and not canon's "9–12" range.** §4.1 has the derivation and the resolution of the two disagreeing canon values.

**10. `DocCheck` grew from 50 assertions to 66**, all green against the shipped parser, and the mutation check (running the same amended `DocCheck` against `b83b62f`) turns **9** of them red — one for each behavioural claim item 2 publishes, including the P1 reversal. F25 is met in substance rather than only at §7 D. *(Updated after the `review-pr` pass added the P1-reversal pins; this delta previously read 62 and 7.)*

## Related

- `Planning~/features/leading-miss-docs/requirements.md`
- `Planning~/design-docs/leading-miss-bar.md` — LOCKED; DR-9 and §5.6/§5.7 scope this feature
- `Planning~/features/leading-miss-bar/architecture.md` — §7 defers the `DocCheck` pin here; §10 holds both errata
- #129 — the audit this implements · #130 — backlog tracking · #126/#127/#128 — open findings, none blocking
