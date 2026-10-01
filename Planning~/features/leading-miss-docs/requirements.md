---
type: requirements
feature: leading-miss-docs
topic: voice
status: draft
updated: 2026-08-27
sources: [Planning~/design-docs/leading-miss-bar.md, Planning~/features/leading-miss-bar/requirements.md, Planning~/features/leading-miss-bar/architecture.md, https://github.com/jinwoo1601/VoXR-Speech-Recognition/issues/129, https://github.com/jinwoo1601/VoXR-Speech-Recognition/issues/130]
---

# feat-leading-miss-docs — requirements

Backlog **item 2** of the G1-locked `leading-miss-bar` design (design §9). Depends on item 1, which is merged (#125, `41ea036`). Blocks item 4 (`chore/release-2.0.0`).

## 1. What this feature is

**Bring the shipped product documentation into agreement with the behaviour that merged in item 1.** Item 1 changed what the parser does and touched no file under `Documentation~/`; this feature closes that gap and nothing else.

The behaviour being documented, stated once so every requirement below can refer to it — **the bar (DR-1)**:

> A candidate whose **first required element** matched nothing may compete and may consume its span, but may not fire.

Three properties of that sentence do most of the work in the docs, and each is a place the existing text goes wrong:

- **Refuse-to-FIRE, not refuse-to-compete (DR-4).** The barred candidate still wins its round and still consumes its tokens. That consumption is the point — it is what keeps leading debris off the next command (design §2.3; item-1 requirements §4.1). Docs that describe a barred round as "no candidate" or "never became a candidate" describe the rejected fork, not the shipped one.
- **No score moves.** The bar decides whether an already-computed score may produce a result. Docs that reach for a threshold or a weight to explain the new silence are reaching for the wrong knob.
- **Positional, not statistical.** The first required element is the verb. `minScore` sees `2/3` and cannot ask *which* third went missing; the bar can. This is why length-based and threshold-based advice does not transfer to a leading miss.

## 2. Why this is a feature and not a chore

Item-1 requirements §1 says that if item 1 ships alone "the package is correct — it is merely under-documented." **That sentence is wrong and must not be used to size this work.** #129's audit establishes the docs are *mis*-documented: in three places they give advice that is now actively reversed, and a reader who follows it does the wrong thing.

The concrete cases, in the reader's own terms:

- `troubleshooting.md:81` tells a user with a short pattern to **lengthen it**. For a leading miss, no length recovers the command — `cease fire` heard as "fire" scores `0.67` at three elements and is still barred.
- `command-recognition.md:439` lists "make the difference come earlier in the pattern" under **what does not help**. It is now one of the more effective mitigations available, at any pattern length.
- `KNOWN_LIMITATIONS.md:678-690` offers `disambiguateSiblingTies` as the workaround for an entry whose own repro the bar has made unreproducible.

**Sequencing consequence (#130).** Today `main` carries the new behaviour with old docs, but no tag captures the mismatch, so no installed version is affected yet. Cutting 2.0.0 first would stamp a release whose troubleshooting page cannot work. Item 2 lands before item 4.

## 3. Non-goals

- **No behaviour change of any kind.** No parser edit, no threshold, no flag, no test expectation rewritten to match new prose. If a doc correction appears to require a code change, that is a finding to file, not licence to edit — the exception in F18 is a shipped *string*, not logic.
- **The construction-time sibling over-warning is out.** The warning claims "the wrong intent can fire" about grammars where nothing can fire, deliberately deferred in code at `VoxrCommandParser.cs:1646-1647`. #129 P3 proposed disclosing it here as a known limitation; **ruled at G0: item 3 owns it, item 2 says nothing about it.** The consequence is recorded in §6.
- **No version number anywhere.** `CHANGELOG.md` stays under `[Unreleased]`; item 4 names the version via the `release` skill (DR-10).
- **Fork C is not documented as forthcoming.** It is deferred to its own design topic (design §10) and has no user-facing existence.
- **No restructuring for its own sake.** Sections are corrected where they are false and extended where they are silent. A section that is merely awkward but true is left alone.
- **Item 1's own process docs are not revised.** Their errata are already recorded (architecture §10) and are inherited here as §7, not re-litigated.

## 4. Requirements

Priority: **Must** = required for G2. **Should** = expected, droppable only by explicit ruling. **Could** = worth doing if cheap.

Every acceptance check below is the same shape and can be run by reading the site and comparing it against the merged behaviour: **a reader who follows this text must not be led to a false conclusion or an ineffective action.**

### 4.1 Tier 1 — advice that is now wrong

| # | requirement | pri | acceptance check | source |
|---|---|---|---|---|
| F1 | `troubleshooting.md:81`'s remedy list distinguishes a **leading** required miss from a non-leading one. Lengthening is kept for the non-leading case, where it still works, and explicitly ruled out for the leading case. | Must | The text names which case each remedy serves; a reader with a leading miss is not told to lengthen. | #129 P1 |
| F2 | `command-recognition.md:439` no longer lists "make the difference come earlier" under what does not help. The `weapons mode active` / `navigation mode active` example is corrected: both now miss element 0, both are barred, nothing fires. | Must | The claim is inverted and the worked figures match the shipped parser. Moving a discriminator to the first required element reads as an effective mitigation at any length. | #129 P1; design §5.6 |
| F3 | `KNOWN_LIMITATIONS.md:603-607` carries the same correction as F2. The "grow the pattern back and the wrong-command behaviour returns at 0.67" claim is removed — it does not return. | Must | No surviving sentence asserts the behaviour returns with length. | #129 P1 |
| F4 | The repro at `KNOWN_LIMITATIONS.md:661-664` is replaced with one that **actually reproduces**. The current one drops each pattern's first required element, so both candidates are barred and the tie is not live at `minScore` 0.4 or any value. | Must | The replacement uses a **non-leading** discriminator, and a reader following it observes the documented symptom. | #129 P1 |
| F5 | `KNOWN_LIMITATIONS.md:678-690`'s `disambiguateSiblingTies` workaround is stated against a repro it can actually affect. A barred candidate is never offered as a disambiguation choice. | Must | The workaround is not claimed for a case where it is inert. | #129 P1; interacts with #126 |
| F6 | `troubleshooting.md:97-101` distinguishes the two causes of "reports nothing though it clearly part-matched": the pre-existing admission refusal (never became a candidate) and the bar (**did** become one, won its round, consumed its span). | Must | The diagnostic instruction reaches both. Counting matched-vs-missed elements does not find a barred round; the text directs the reader to check **which** element missed. | #129 P1 |

### 4.2 Tier 2 — false statements

| # | requirement | pri | acceptance check | source |
|---|---|---|---|---|
| F7 | `scoring.md` §7 worked example **D** is amended per DR-9: **step 1 unchanged, step 2 inverts.** The `:445` "It fires too", the `:450-452` JSON entry that is never emitted, and the `:455` "both commands fire … which is the shape to expect" all go, along with `:438`'s heading and `:457`'s failure-mode rationale. The old control becomes a **second worked trace** (design §5.7) so the #82 half — still correct, still the point — survives. | Must | The example is traceable end to end against the shipped parser, and the #82 half is not lost. | DR-9; design §5.7 |
| F8 | `scoring.md` §5's three pending-routing sites (`:210`, `:242`, `:263`) no longer assert that `allowPartialMatch` routes an incomplete winner to pending *unconditionally*. **A barred winner opens no pending at any score.** | Must | Each of the three sites carries the bar's precedence. | #129 P2 |
| F9 | `scoring.md:298-307`'s eager-verdict list states **seven** conditions, not six, and its "none of the six is implied by its neighbours" claim is restated for seven. The bar's eager condition is explicit and **not** inherited from the comparator. | Must | The count matches `TryEagerCommit`'s gates, and the independence claim covers the new one. | #129 P2; DR-5 |
| F10 | `scoring.md:148`'s cross-reference to worked example D is moved. D is now the opposite case — it no longer shows an orphan run and a later round landing in the same place. | Must | The reference points at text that supports the claim being made. | #129 P2 |
| F11 | No site asserts one log entry per extraction round, nor that a pattern's absence means it lost selection. **A barred round logs nothing**, so absence now also means "won and was barred". Sites: `scoring.md:18` (the glossary definition of *Winner*), `scoring.md:463`, `troubleshooting.md:48`, `editor-testing.md:24`, `:64`, `:157`, and the `Readme` string of F18. | Must | **All seven** sites admit the second reading of an absent entry, and the list is closed by an independent search rather than inherited from #129. *(Corrected 2026-08-27: this read "all four sites" and enumerated four; the validation pass found three more, including the definitional one.)* | #129 P2; item-1 requirements §8.4 |
| F12 | `command-recognition.md:620-626` lists **six** situations for `OnUnrecognisedSpeech`, not five. | Must | The count and the new entry match the shipped behaviour. | #129 P2 |
| F13 | `command-recognition.md:523` no longer says the outcome is "decided by completeness alone, independently of `minScore`". The bar is consulted first. | Must | The precedence stated matches the code path. | #129 P2 |
| F14 | `troubleshooting.md:77` and `:79` split the `~0.50` signature and the "on three or more elements this no longer happens" claim by leading vs non-leading. On three or more elements a *non-leading* drop still fires at `0.67`; a *leading* drop does not fire at any length. | Must | Both sentences are true as written for both cases. | #129 P2 |
| F15 | The exhaustive cause-lists at `api/command-recogniser.md:44` and `:45` account for the bar, and the three copies of the `allowPartialMatch` claim — `inspector-authoring.md:40`, `api/scriptable-objects.md:49`, `api/command-definitions.md:17` — carry F8's correction. | Must | No cause-list presented as exhaustive omits the bar; the three copies agree with `scoring.md` §5. | #129 P2 |
| F16 | `api/batch-test-runner.md:44`, `:49`, `:51` record that a barred round never reaches the runner, so the runner reports `"no pattern matched"` with `Score` `0` and `Confidence` `-1` **even where the pattern matched every element but its first and scored well above `minScore`** — up to `0.86` on a seven-element pattern. `Score` `0` no longer implies nothing matched. | Must | A reader interpreting a batch-run report is not misled by a zero, **and the bound stated is one a barred candidate can actually reach**. *(Corrected 2026-08-27: this read "matched at 1.00", inherited from #129 and false — the latch fires only at a miss site, so a barred score is bounded by `(N−1)/N`. Precisely the F28 failure F28 predicts.)* | #129 P2; validation pass |
| F17 | `scoring.md` §1's `cease fire` row (`:70`, `:74`) states the mechanism that now applies. **Ride-along, ruled at G0** — the site sits outside DR-9's "§7 D" wording. | Must | The row's stated mechanism matches the shipped parser. | #129 "outside DR-9"; G0 ruling 2026-08-27 |
| F18 | The `Readme` string at `Editor/VoxrDebugSessionLog.cs:31-35`, embedded in **every exported session log**, no longer claims each attempt "is one extraction round, reporting the command pattern that won selection that round". **Ride-along, ruled at G0** — shipped code, not documentation. | Must | The exported string is true of a session containing a barred round. The change is to a string literal only; no logic moves. | #129 "outside DR-9"; G0 ruling 2026-08-27 |

### 4.3 Tier 3 — missing coverage

| # | requirement | pri | acceptance check | source |
|---|---|---|---|---|
| F19 | `KNOWN_LIMITATIONS.md` gains an entry for **the bar's own cost**: where the leading word *was* spoken and the decoder dropped it, the command used to be recovered at a reduced score and is now silent. The entry states plainly that **no transcript-level discriminator exists** between "never spoken" and "spoken and lost", so re-uttering is the only remedy. Natural neighbours are the entries at `:336` and `:550`. | Must | The entry is honest about the trade rather than presenting the bar as free, and its corpus figure is **measured, not chosen between canon's two disagreeing values** — #129 P3 says *"17 of 699"* (rows firing nothing) and design §7.3 says *"9–12"* (genuine recoveries lost); they count different things. Derived from the committed `corpus-bar-off.tsv` / `corpus-bar-on.tsv` via `issue124/analyse-arms.py`, **not** from F24, which measures a different question with a different instrument. | design §9 item 2; §7.3; #129 P3; validation pass |
| F20 | `command-recognition.md:41-42`'s pipeline diagram shows the bar as its own stage, **before** the Threshold Filter — it is not part of it. | Should | The diagram's stage order matches the parse path. | #129 P3 |
| F21 | `command-recognition.md:601` lists the third non-preempting class: a barred utterance no longer cancels a live pending nor preempts follow-up slot-fill. | Should | The list is complete for the shipped behaviour. | #129 P3 |
| F22 | Sequential extraction's description admits that **a round can win, consume tokens and yield nothing**: `scoring.md:193`, `:269-282`, `:497-498`. The `OnUnrecognisedSpeech` table gains a row for it, and **`scoring.md:493-500`'s symptom table** drops the row for the now-unreachable symptom and gains one for the new one. `index.md:14` is a one-line table-of-contents blurb and takes only the mention. | Must | A reader forming a mental model of extraction from these sites can predict a barred round. *(Corrected 2026-08-27: this said "`index.md:14`'s symptom table". **`index.md` has no symptom table** — the premise came from #129 P3 and is false.)* | #129 P3; validation pass |
| F23 | `editor-testing.md:212` states a **fourth** rejection cause for `expectedIntent`, and `:217`'s "that third rejection cause" is renumbered accordingly. | Should | The count and the ordinal agree. | #129 P3 |

### 4.4 Numbers, and the CHANGELOG

| # | requirement | pri | acceptance check | source |
|---|---|---|---|---|
| F24 | The published figure *"28 candidates were blocked this way and 27 were recovered by a later round"* (`scoring.md:175`, `KNOWN_LIMITATIONS.md:428-432`) is **re-derived from the 699-row corpus with the rig**, not adjusted by hand. A "recovery by a later round" is precisely what the bar now refuses, so the second number cannot survive unexamined. | Must | The figure in the docs is reproduced by a rig run recorded in the architecture doc. If the shape of the claim no longer holds, the claim changes rather than its digits. | #129 verification note |
| F25 | The `DocCheck` pin (`Planning~/features/coverage-in-selection/ab-rig/DocCheck.cs`) is updated to protect the amended `scoring.md` §7 D and every other published number this feature touches. **This pin is item 2's**, explicitly deferred here by item-1 architecture §7. | Must | `DocCheck` passes against the real parser, and it fails if F7's trace is reverted. Validated against the committed pin **before** its output is trusted. | DR-9; item-1 architecture §7 |
| F26 | `CHANGELOG.md`'s existing `[Unreleased]` entry — originated by item 1 under a human ruling — is **extended**, not duplicated or replaced, with the user-facing documentation consequences of this feature. No version number is introduced. | Should | One coherent `[Unreleased]` entry describes both the behaviour and its documented remedies; `git log` shows item 1's lines surviving. | item-1 requirements §4.13/F18; design §9 |

### 4.5 Cross-cutting

| # | requirement | pri | acceptance check | source |
|---|---|---|---|---|
| F27 | The **§5.6 grammar-side mitigations** are documented, in the user's terms, in `command-recognition.md`: let a legitimate pattern claim the tail; register a benign intent for the standalone fragment; `requiresConfirmation` on destructive intents. Canon: these are documented *"regardless of anything else, because they help users on 1.5.0 today"*. | Must | All three appear as actionable authoring guidance, not as design history. | design §9; §5.6 |
| F28 | No correction introduces a **new** false claim. Every worked figure, score and count written by this feature is either derived from the parser or checked against it. | Must | The review pass finds no unverified number. This is the requirement most likely to be violated quietly. | #129; item-1 architecture §10 |
| F29 | Internal cross-links and anchors remain valid. Several corrections rename or retarget headings that other pages link to by anchor. | Must | Every intra-doc link resolves to an existing heading after the change. | audit of the edit set |

## 5. Non-functional requirements

- **Voice-robustness honesty.** The package's contract when it does *not* understand is a first-class user concern, not a footnote. Where the bar produces silence, the docs say so plainly and say what the speaker does about it (re-utter) rather than implying a knob exists.
- **No knob-shaped false hope.** `minScore`, `coverageWeight` and `allowPartialMatch` must not be offered anywhere as a remedy for a leading miss. They cannot reach it, and the existing text repeatedly implies they can.
- **House voice.** Corrections match the surrounding register — worked figures inline, mechanism before remedy, `KNOWN_LIMITATIONS.md` entries carrying repro / root cause / workaround as its existing entries do.
- **The docs must survive item 3 being dropped.** Nothing written here may depend on the construction-time warning existing (§3, §6).

## 6. Dependencies, assumptions and consequences

**Depends on:** item 1, merged at `41ea036`. **Blocks:** item 4.

**Assumed:** the behaviour merged in item 1 is correct as shipped and is not revisited here. #126, #127 and #128 are open findings against it; none blocks this feature, and none is documented as forthcoming.

**Consequence of the G0 ruling on item 3.** By ruling that item 2 says nothing about the sibling over-warning, **item 3 stops being droppable before 2.0.0**. Canon (design §9) calls it *"independently valuable and independently droppable"*; that is no longer true, because dropping it now ships a false author-facing claim with nothing disclosing it. This is a change to the backlog's dependency structure. **It falsifies a sentence in the locked design, so it was taken to the human as a canon-authority call rather than absorbed** — ruled 2026-08-27 as **erratum E-2**, no design branch and no G1 re-lock, on the E-1 precedent. Full record: architecture §9. Also belongs on #130.

**Environment constraints** carried from project memory: `grep` here honours `.gitignore`, so `Planning~/` needs `--no-ignore-files`; `gh pr edit` and `gh issue view` fail on this repo (projectCards deprecation) — use `gh api`; `DocCheck`'s `-p:StartupObject=` does not survive an incremental rebuild, which produces false greens.

## 7. Inherited errata — carry, do not rediscover

Both were ruled during item 1 as erratum-not-design-branch, with DR rulings unaffected. Full record: item-1 architecture §10.

1. **E-1 — "an eager commit IS a fire" is false.** Design §2.4, §5.2 and DR-5's own justification cell all claim it. `Commit` routes through `FlushBuffer` → `ProcessParsedResults` → `ParseInternal`, where the bar refuses the winner anyway. DR-5's condition protects the **buffer**, not G-1. **This bears directly on F9:** the eager condition is real and load-bearing, but a doc that explains it as "otherwise a phantom would fire early" repeats the erratum.
2. **§6.2's "five or more" is false** — the completeness gate precedes length and score. The correction carries a condition: `AllowPartialMatch` routes an incomplete winner to `EnterPending` rather than rejecting it, so *"can never fire at any length"* holds **only where that flag is off**. **This bears directly on F8 and F15**, which are about exactly that flag.

## 8. Verification

- **Both Unity suites through the host project**, delegated to `compile-check`. Baseline to beat: EditMode **133/133**, PlayMode **569/569**. A docs feature should move neither number; F18's string edit is the only thing that can, and it must not.
- **`DocCheck` against the real parser** (F25), validated against the committed pin first.
- **The rig re-derivation for F24**, recorded in the architecture doc with its command line, so the number is reproducible rather than asserted.
- **`review-cycle` on the branch**, then **`review-pr` at full profile** on the PR. Docs need adversarial checking as much as code does — this is the lesson `disambiguation-pending` recorded, and this feature is nothing but claims.
- **Deferred, human-only:** on-device Quest verification. This feature touches no native bridge, no MonoBehaviour lifecycle and no audio path. Stated rather than assumed away; never claimed.

## 9. Open questions

1. **F4's replacement repro needs a grammar that is actually live.** The entry's discriminator must be non-leading, and the pair must clear admission and tie for real. Whether the demo grammar supplies one or the entry must introduce a fixture is an architecture-doc question.
2. **F22's symptom-table row for the now-unreachable symptom** — remove it, or keep it marked as pre-2.0.0 behaviour? *(Re-scoped 2026-08-27: the question was asked of `index.md`, which has no symptom table. It applies to `scoring.md:493-500`, and is settled there while editing — see architecture §3.1's `:494`/`:497` rows.)*
3. **F26's extension scope.** Whether `[Unreleased]` gains prose about the documentation corrections at all, or whether a docs-only pass is correctly invisible in a user-facing changelog. Leaning: one line naming the reversed guidance, because a user on 1.5.0 who read the old advice needs to know it changed.
4. **F24 may not survive as a number.** If the re-derivation shows the "recovered by a later round" shape is largely gone, the honest correction may be to drop the sweep figure and state the mechanism instead. Deciding that needs the rig output, so it is left open here deliberately.
