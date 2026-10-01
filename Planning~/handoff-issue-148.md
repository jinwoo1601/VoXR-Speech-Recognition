# Handoff — issue #148: slot resolver hook (feature lane)

You're picking up VoXR issue #148 — the slot resolver hook. Read this file, then start at **Start here**. Nothing has been created yet: no branch, no docs, no code.

## Objective

Let a game fill an **omitted required slot** from game state, so `"launch missiles"` can mean `"launch missiles target ⟨the main target⟩"` when the game knows there is one obvious target. The package ships the *hook*; the game decides what "the obvious target" is.

Ship: `RegisterSlotResolver` / `UnregisterSlotResolver` on `VoxrCommandRecogniser`, a new public `VoxrSlotResolution` type (value-or-none + a short `Reason` string like `"main target"` for crew readback), and `VoxrCommand` exposing which slots were resolver-filled and why.

`gh issue view 148` is the requirements source — read it in full, it is unusually complete.

## Lane — settled 2026-09-16, do not re-litigate

**Feature lane, skipping the design track.** The maintainer approved this explicitly.

- It is *not* issue lane: it adds public API and changes the complete/incomplete ruling on the core accept path. The issue lane is bug fixes and small maintenance, and it skips the gates.
- It does *not* need G0/G1: the design was already locked by **ruling 9 (2026-09-06)** in `Planning~/research/2026-09-05-next-level/05-rulings.md`. Writing a design doc now would restate rulings already made.
- So: feature branch → requirements doc → architecture doc → implement → `review-pr` → **G2 is the maintainer's explicit in-conversation ruling**, then product docs, then they merge. Claude never merges.

## Repo state

- `/mnt/d/Game Development/VoXR-Speech-Recognition`, on `main` at `03831a8`, tree clean.
- **No open PRs. #148 is the only open issue.** Phase A of the September plan is otherwise complete.
- Bare Unity UPM package: nothing compiles or tests standalone. All Unity verification goes through the host project `D:\Game Development\VoXR TestGround` (Unity 6000.4.7f1).

## Start here

```bash
cd "/mnt/d/Game Development/VoXR-Speech-Recognition"
git fetch origin && git switch -c feat-slot-resolver origin/main
```

Then, in order:
1. `feature-requirement-doc` skill → `Planning~/features/slot-resolver/requirements.md`
2. `architecture-doc` skill → `Planning~/features/slot-resolver/architecture.md` (write it as the draft of the eventual product page, and **persist the implementation plan into it** — plan-mode output is lost at session end)
3. Implement. Delegate code/test/doc edits to subagents; keep the main thread for planning, gates and git ceremony.
4. `compile-check` agent for every verification run — never run builds or tests on the main thread.
5. `gh pr create`, then **immediately** `review-pr` at the **full** profile (feature lane). Standing grant: no permission ask.
6. Stop at G2 and wait for the maintainer's ruling.

## The contract (from #148 + the rulings)

- Ellipsis here means **game-state resolution of an omitted slot**, never "reuse the last-mentioned value".
- A resolver is consulted **only for a round that actually produced a candidate**, and only when the command is otherwise complete except for required slots.
- Resolved → command completes and proceeds through the normal gates. Not resolved → today's behaviour exactly (pending with `allowPartialMatch`, else rejected).
- **A resolver can never supply a pattern's first required element.** The leading-required-miss bar is absolute (ruling 3). If the missing slot is the anchor, the round is already barred and no resolver runs.
- Runs on the main thread at parse time — must be cheap. No grammar change, no rebuild.
- Session log must record resolver-filled slots distinguishably.

Acceptance tests go entirely through `InjectText`. Four cases, (c) is the one that matters most: a pattern whose first required element is the resolvable slot, spoken without it, **stays barred and the resolver is never called**.

## Key code sites — verified line numbers, 2026-09-16

| Site | Why it matters |
|---|---|
| `Runtime/Commands/VoxrCommandRecogniser.cs:330` `RegisterSlotValueProvider` / `:335` `UnregisterSlotValueProvider` | The precedent pair. Match their shape, naming and null/validation handling. |
| `…VoxrCommandRecogniser.cs:1314` `bool IsIncomplete(VoxrCommand)` | The incomplete ruling itself; delegates to `VoxrCommandParser.HasUnfilledRequiredSlot`. |
| `…VoxrCommandRecogniser.cs:770`, `:797`, `:990` | **Three** call sites of `IsIncomplete` — the main accept gate, the follow-up path, and a third. See trap 2. |
| `…VoxrCommandRecogniser.cs:649` `ProcessParsedResultsCore` | The parse→accept spine; `Planning~/anaphora-resolution-analysis.md` names it as the slot-time hook's integration point. Reached from `:576` and `:621`. |
| `Runtime/Commands/VoxrCommandParser.cs` | `internal`. Anything the sample or a game must reach has to be public on the recogniser. |

## Traps

1. **`Planning~/anaphora-resolution-analysis.md` is superseded in part.** Its "Phase 1 — ellipsis only" proposes *"track a 'last target' per slot category"*. Ruling 9 and #148 **forbid** exactly that — no last-mentioned memory in the package. Read the analysis for its integration-point reasoning (§"Where in the pipeline does resolution run?"), not for its Phase 1 mechanism.
2. **Don't patch only the main gate.** `IsIncomplete` is consulted in three places. Decide deliberately which of them a resolver participates in, and say so in the architecture doc — a resolver that fires on the fresh-parse path but not the follow-up path is a behaviour split nothing will catch.
3. **Pronouns, demonstratives, gaze/pointing fusion are out of scope** — deferred to a design branch after the human corpus exists. Don't let the analysis doc pull you into Phase 2.
4. A global CSharpier PostToolUse hook reformats every `.cs` edit; it over-indents wrapped object-initializer members and silently reverts manual de-indents. Restructure, don't re-indent.
5. `gh pr edit` is broken in this environment — post a comment instead of editing a PR body.

## Verification

Procedure lives in `.claude/verification-bindings.md` (gitignored); the `compile-check` agent reads it. Facts to plan around:

- Unity editor must be **closed** (`<host>/Temp/UnityLockfile` gone) or the run is refused.
- Both platforms must run — `Tests~/Editor` is EditMode, `Tests~/Runtime` is PlayMode, and most parser/command tests are PlayMode.
- Current green baseline on `main`: **EditMode 186, PlayMode 638.**
- `NativeBridge~/` is not involved here — no `.so` rebuild, no desktop harness run.
- Nothing in this change should need device verification. If something does, report it deferred; it is human-only and never claimable from WSL.

## Gates

G2 only. It is the maintainer's **explicit in-conversation ruling** on the open PR — GitHub forbids self-approval in this solo repo, so `review-pr`'s posted comment is evidence, never the ruling. Order: PR opened *without* product-doc changes → `review-pr` (full) posts its verdict → maintainer inspects and rules → approved fixes, then product docs pushed to the same branch → maintainer merges.

Product docs, post-G2 only: `Documentation~/command-recognition.md` (Pending Commands — a new step before "missing required slot"), `Documentation~/api/command-recogniser.md`, `Documentation~/api/data-types.md`, and `CHANGELOG.md` under `[Unreleased]` → `### Added`.

## Related memory

`plan-next-level-2026-09` (item 6 — update its Status when this lands), `next-level-research-2026-09-05`, `disambiguation-pending-feature` (the pending-command machinery this sits next to), `leading-miss-bar-design` (why the bar is absolute), `feedback-delegate-implementation`, `running-package-tests`.
