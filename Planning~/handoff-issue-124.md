# Handoff — issue #124: definitive solution

You're picking up: find a definitive solution to VoXR issue #124 (an utterance intended as one command fires a second, unspoken command). Read the files below, then start at Next steps.

## Objective

Find a **definitive** solution to issue #124 in `jinwoo1601/VoXR-Speech-Recognition`. The user wants a Fable agent used to think it through.

The user's ruling, verbatim: *"if there is no definitive solution, a single Utterance firing two commands when the intention was one is an unacceptable phenomenon. I lean on ditching sequential command acceptance entirely if there is no solution."*

So the deliverable is either (a) a definitive fix, or (b) a costed evaluation of removing sequential command extraction, for the user to rule on. **Do not implement either without their sign-off.**

## Context

- **Repo:** `/mnt/d/Game Development/VoXR-Speech-Recognition` — bare Unity UPM package (VoXR), wraps the VOSK decoder and adds a command-recognition layer. On `main` at v1.5.0, tree clean.
- **Issue #124** is open, labelled `bug` + `needs-input`. A detailed findings comment is already posted (`#issuecomment-5426014608`) — read it first, it saves an hour.
- **The bug:** grammar registers `query_time_to_target` = `["time","to","target"]` and `intercept_target` = `["intercept","track","{track}"]`. Utterance *"time to target track one two four four"* fires **both** — the second at score 0.6667 with `track = "one two four four"`. The word "intercept" was never spoken. In the reporter's command-and-control grammar a read-only query executed a maneuver order.
- **Lane:** this started in the issue lane (`tackle-issue`) but has escalated past it. Any behavioural fix here is **design-lane** work per the project CLAUDE.md — G0 (design branch + scope), G1 (locked design, human sign-off), then feature branches. `tackle-issue` was correctly halted at its "needs a product/design decision" guardrail.

## Current state — established and verified, do NOT redo

All of the following was measured by replaying the **real parser** through the desktop A/B rig, not reasoned about:

1. **Reproduced exactly.** Matches the reporter's session log to 4 dp.

2. **The failing shape is bit-identical to intended behaviour.** `scoring.md` §7 worked example **D** documents the same shape as *desired*, and it is pinned by `Coverage_SequentialExtraction_SparesTheFirstCommandWhenASecondLosesItsWord` (`Tests~/Runtime/VoxrCommandParserTests.cs:2462`):

   | | round 1 | round 2 pattern | leading literal | matched/missed | score |
   |---|---|---|---|---|---|
   | §7 D (**wanted**) | `cease fire` | `approach target {target}` | `approach` missed | 2/1 | 0.6667 |
   | #124 (**unwanted**) | `time to target` | `intercept track {track}` | `intercept` missed | 2/1 | 0.6667 |

   Barring a leading-required-miss from firing reverts issue #82 and breaks this test.

3. **No transcript-level discriminator exists.** Identical across both cases: continuation-round status, kind of missed element, consume-to-end, matched/missed counts, score, element count, miss position. Every "similarity between round 1 and round 2" metric is **0.000 in both** (pattern-element Jaccard, matched-token Jaccard, slot-name Jaccard, span overlap — spans are disjoint by construction). Round-2 self-completeness is 0.667 in both. Only intent-*name* strings differ (0.20 vs 0.00) — unusable, they're arbitrary identifiers.

4. **⚠ THE FINDING THAT KILLS THE USER'S LEAN — verify this first, then tell them.** #124 is **not** a sequential-extraction bug. Measured:

   ```
   'track one two four four'  (alone)  ->  intercept_target=0.6667 [track=one two four four]   n=1
   ```

   No first command involved. **Removing sequential extraction does not fix #124.** With the reporter's own `bufferWindow 2.0`, a pause longer than the window means the tail never merges and arrives as a round-1 fire. Any fix scoped to "round 2" narrows the incident without closing the class.

5. **A construction-time authoring warning cannot be narrowed.** The two intents share no vocabulary. The only predicate that catches it flags **19 of 32** patterns in the shipped sample grammar — the over-broad-warning failure mode issue #81 exists to fix.

6. **The 699-utterance regression corpus cannot contain this shape.** `Planning~/features/coverage-in-selection/ab-rig/perturb.py` generates four families: `delete`, `append`/`prepend` (one probe word), `concat` (two **complete** transcripts). None produces "complete command + another intent's tail". A clean 699-row sweep was never evidence about this class. A fifth family is a precondition for any measurement.

7. **Word timings are plumbed and then discarded.** `VoxrJsonParser.cs:86-90` parses `start`/`end` into `VoxrWord.StartTime/EndTime`; the buffer carries them intact; `Editor/VoxrDebugSessionLog.cs:222-223` **already exports them**; they die at `VoxrCommandParser.cs:2232`, collapsed into a text-keyed confidence dictionary.

8. **The utterance-buffer seam is a dead end as a discriminator.** `UtteranceBuffer` joins `_texts` and flattens word arrays with no per-`Append` markers — plumbable, but semantically useless: a segment boundary argues *for* firing under both readings (deliberate second phrase, or a second result that onset-clipped its first word), and the phantom-with-a-pause case puts a boundary in the same place. Corroborator only.

9. **Why sequential extraction exists** (recover with `git show 82d0437:v2-command-recognition-plan.md`): shipped v0.7.0 as the v2.3 "Continuity" milestone. It is the **counterpart to the utterance buffer** — the buffer merges decoder results across pauses to reassemble a command split by a breath, but that same merging glues separate commands together. Without extraction, adding the buffer would itself have caused dropped commands. Design decisions: ordered by position not score; greedy left-to-right, no backtracking; `OnCommandsRecognised` added simultaneously; intra-batch debounce added because extraction can find one intent twice.

### Options already on the table (from a prior Fable agent pass)

- **A.** `KNOWN_LIMITATIONS.md` entry beside the residual at :498 — this *is* that residual with an intent-tail instead of a slot value. Issue lane, safe.
- **B.** Surface `MissedLeadingRequired` + first-matched-token on the public `VoxrCommand` struct. Additive, zero behaviour change, zero hot-path cost. Lets game code veto inside `OnCommandRecognised`, and instruments the wild for future data. Issue lane, safe.
- **C.** Opt-in (default-off) routing of a suspect winner into the existing pending/confirmation machinery (`VoxrCommandRecogniser.cs:1044-1058`) instead of firing. Dominates the reporter's cap-per-utterance idea, which silently *discards* §7 D's legitimate second command instead of asking about it. Needs the user's ruling on name/shape.
- **D.** Acoustic check — measure the silence gap where the missed leading word would have been. Hole ⇒ a word was spoken and lost ⇒ fire; no hole ⇒ nothing was said ⇒ don't. **Unvalidated hypothesis**: unknown whether VOSK leaves a gap or stretches neighbouring word alignments over elided audio. Design-lane, gated on measurement. Only helps the one-breath case, not the standalone tail.
- **Already covers the whole class today, zero code:** `requiresConfirmation` on destructive intents (`VoxrCommandRecogniser.cs:1044-1058`) diverts to pending regardless of how the match arose — including the standalone-tail and round-1 shapes nothing in A–D reaches.
- **Discarded:** bar leading-miss from firing (reverts #82); construction warning (see 5); bare cap-per-utterance (dominated by C); score-bar raises; retroactive re-charging.

## Key files & locations

- `Documentation~/scoring.md` — the model. §2 Coverage, §3 Selection, §4 Sequential extraction, §7 worked examples B/B2/**D**.
- `Runtime/Commands/VoxrCommandParser.cs` — `TryMatchScored` :3157, `CompareCandidate` :3031 (admission :3073), `OrphanedAfter` :3627, `IsAdmissibleStart` :3691, `CanStartPattern` :3736, `ParseInternal` :2244, confidence collapse :2232.
- `Runtime/Commands/VoxrCommandRecogniser.cs` — tuning fields :17-126, events :128-136, event raising :1070-1100, confirmation branch :1044-1058.
- `Runtime/Commands/VoxrCommand.cs` — public result struct :34.
- `Runtime/Commands/UtteranceBuffer.cs` — `Append` :22-36, `Flush` :46-70.
- `Runtime/VoxrResult.cs` :8-30, `Runtime/VoxrJsonParser.cs` :86-90 — word timings.
- `Editor/VoxrDebugSessionLog.cs` :222-223 — timings already exported.
- `KNOWN_LIMITATIONS.md` :498 — the analogous residual entry.
- `Tests~/Runtime/VoxrCommandParserTests.cs` :2462 — the pinned §7 D test.
- `Tests~/Runtime/DemoGrammar.cs` — test copy of the shipped sample grammar.
- **A/B rig:** `Planning~/features/coverage-in-selection/ab-rig/` — `stage.sh WORKTREE <outdir>` stages the real parser into a buildable .NET project. Add a `--repro` branch to the staged `Program.cs` and build with the **default** StartupObject.

## Constraints & decisions

- **Never merge, never tag, never commit to `main`.** Output is a PR the user merges, or a design doc.
- **Gates are hard stops.** G1 (design locked) and G2 (feature accepted) are the user's calls, in conversation. Do not cross them on your own judgement.
- **The user has explicitly overridden their own global rule twice** to request a **Fable** subagent for this problem. `~/.claude/CLAUDE.md` says *"Subagents never run on Fable — always Opus or below."* Honour the in-session override; mention it once, don't re-litigate.
- **§7 D and issue #82 are locked design.** Changing them requires reopening the design doc on a design branch and re-locking at G1 — not an issue-lane patch.
- **Codebase values:** filters are counts not tunable knobs where possible; parser is deterministic; coverage's start test is deliberately conservative ("over-charging destroys sequential extraction; under-charging merely leaves a score where it already was"); default-on behaviour changes get corpus-measured and listed in `KNOWN_LIMITATIONS.md`; parse path is zero-alloc and sits in a triple-nested loop.
- **Rig traps (learned the hard way):** `-p:StartupObject=` does **not** survive an incremental rebuild — false greens. A clean 699-corpus A/B is **not** evidence a scoring change is safe. `grep` here honours `.gitignore`, so the rig's own C# is invisible to a rename sweep.
- `CLAUDE.md`, `.claude/`, and `Planning~/` are gitignored — local-only, never ship.
- This is a **bare UPM package**: nothing compiles or tests standalone. Unity verification goes through host project `D:\Game Development\VoXR TestGround` (Unity 6000.4.7f1), **both** EditMode and PlayMode. Delegate to the `compile-check` agent; never run builds on the main thread.

## Next steps

1. Read issue #124 and its posted comment: `gh issue view 124 --json title,body,comments`.
2. **Verify finding 4 yourself** (stage the rig, parse `"track one two four four"` alone). It is the load-bearing fact and it contradicts the user's stated lean — removing sequential extraction would *not* fix the reported bug.
3. Spawn a **Fable** agent (`model: "fable"`, `general-purpose`) with the full case file above. Brief it to: (a) find a definitive discriminator or prove none exists; (b) cost out removing sequential extraction entirely — what breaks, specifically the utterance-buffer coupling in finding 9, and confirm it doesn't even close #124; (c) rank what to do instead. Fresh agents inherit nothing — spell out every fact.
4. Report back to the user with a recommendation and the cost of their lean. **Stop there** — G0/G1 are theirs.
5. If they authorise a design cycle: `design-<topic>` branch off main, design doc in `Planning~/design-docs/`, then G1.

## Open questions — ask, don't guess

- **Does the user still want to ditch sequential extraction now that it's shown not to fix #124?** Removing it also re-breaks commands split across a breath (finding 9) — the failure the utterance buffer was built to fix. This needs an explicit re-ruling.
- **Would they accept a non-definitive answer** — options A + B now (safe, unblocks the reporter this week) plus `requiresConfirmation` guidance — while a definitive fix goes through a design cycle?
- **Has the reporter been asked for the `words[]` array** from the failing utterance's session log? It already contains the timings that would validate or kill option D, and costs them a copy-paste. Not yet requested as of this handoff.
- **If option C is wanted:** what should the flag be called, and should a suspect winner require a confirm word, or fire on a timeout unless cancelled?
