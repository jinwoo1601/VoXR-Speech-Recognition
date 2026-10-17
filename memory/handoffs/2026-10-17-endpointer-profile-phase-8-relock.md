You're picking up: the re-lock of `feat-endpointer-profile` requirements §9, then the Phase 8 plan fix pass. Read the files below, then start at Next steps.

## Objective

1. Re-lock: cut a `design-` branch from the `feat-endpointer-profile` head and amend the "sooner" bar in `Planning~/features/endpointer-profile/requirements.md` §9, as the human ruled (quoted under Constraints). Take it to G1, a human re-lock, then merge it back into `feat-endpointer-profile`.
2. Then resume `phase-pickup` Phase 8 at RECONCILE: an `architect` fix pass on `Planning~/features/endpointer-profile/plan-8.md` carrying validator findings 1-8 as ruled real, plus eager commit in both host builds. Then RECORD & COMMIT.

Done when §9 is re-locked and merged back, `plan-8.md` is fixed (≤10,240 B) and committed on `feat-endpointer-profile`, and the plan is ready for the human to run on the Quest.

## Lane

design

## Context

- One checkout (`trees` is none), at `D:\Workspace\VoXR-Speech-Recognition`. Branch `feat-endpointer-profile`: Phase 7 is built at `100a987` (Continuous default `Responsive`, rule2 `0.45`).
- Phase 8 is the human-only Quest 3 check (F11; §8's arm64 conf assumption). This session ran `phase-pickup` Phase 8 through ORIENT, RECON, PLAN (plan mode, approved with departures D1-D4), PERSIST and VALIDATE. RECONCILE stopped on validator finding 1, which reaches the locked requirements doc. The human ruled that §9 is amended on a design branch.
- **Finding 1:**
  - The Tactical scene's `CommandRecogniser` has `bufferWindow: 2` (`Samples~/CommandRecognition/CommandRecognition_Tactical.unity:1413`), and `eagerFlushOnCompleteMatch` is false by default (`Runtime/Commands/VoxrCommandRecogniser.cs:79`). Every fire therefore waits out the full 2 s.
  - The recorded saving for rule2 0.45 is about 0.07 s (`architecture.md` sweep table, about :121-124).
  - The design names this masking: `Planning~/design-docs/endpointer-profile.md` §3, "The buffer masks the saving".
  - So §9's "the maintainer judges that fires come sooner" would read "no sooner", a false fail.
- The re-lock branch procedure is the `vc` binding's (`.claude/bindings/vc-git.md` *Branch procedure*). It is cut by `git switch -c design-<topic>` from the feature head, declared on its STATUS line as `re-lock of feat-endpointer-profile`, and merged back by `git switch feat-endpointer-profile` then `git merge design-<topic>`. A suggested topic is `endpointer-profile-quest-bar`; confirm it at G0.
- STATUS `## Current` still reads "Phase 7 built …; next: `phase-pickup` Phase 8". The session note is `memory/2026-10-17.md`; see its last entry, "Phase 8 pickup (stopped at RECONCILE) and handoff".

## Current state

- **Committed with this handoff:**
  - `plan-8.md`, persisted by `architect`, 9,862 B, **not yet reconciled**.
  - The architecture doc's `## Build plan` link line `- Phase 8 — persisted plan (2026-10-17): [plan-8.md](plan-8.md)`.
- **Filed in `.scratch/` (ignored):**
  - recon: `code-mapper-endpointer-profile-phase-8-{brief,report}.md`, `doc-extractor-endpointer-profile-phase-8-{brief,report}.md`
  - approved plan: `endpointer-profile-phase-8-approved-plan.md`
  - `architect-endpointer-profile-phase-8-{brief,report}.md`
  - `plan-validator-endpointer-profile-phase-8-{brief,report}.md`, which holds the 8 findings verbatim
- **Not started:** the design branch, the §9 amendment, the fix brief.

## Key files & locations

- `Planning~/features/endpointer-profile/requirements.md`. §9's second bullet holds the bar; F11 is in §5.
- `Planning~/features/endpointer-profile/plan-8.md`: about :44 (spacing and hesitations), :67 and :70-71 (the pass and fail rules), :111 (claim d), :32 and :44 (cites to fix).
- `Planning~/features/endpointer-profile/architecture.md` (tier 2, not locked). The Phase 8 row is about :164. Correct it in place if the amended bar changes its wording; that is `architect` or `architect-reconcile` work.
- `.scratch/plan-validator-endpointer-profile-phase-8-report.md`: findings 1-8.

## Constraints & decisions

- **Human ruling, verbatim (2026-10-17):** "Eager commit + amend §9". The option text was: "Turn on eagerFlushOnCompleteMatch on the CommandRecogniser in both host builds, so the endpointer dominates (design §3) and splits stop being masked by the rejoin. Reopen §9 on a design branch, cut from this feature branch, to change the bar to 'fires are not perceptibly later, sooner where perceptible'. That needs a G1 re-lock, then the plan fix."
- **Plan-mode approval (2026-10-17)** of the approved plan, with departures:
  - D1: the Tactical scene's CommandLog/LastCommand panels stand for §9's "(session log)", which is Editor-only.
  - D2: "Model default" is a second build installed by `adb install -r`, with P3 restoration checked by `cmp`.
  - D3: the pass/fail reading.
  - D4: the host-side setup is the human's, in `D:\Workspace\VoXR TestGround`.
  - Do not reopen these.
- **Findings 2-8 are ruled real by the PM** (fix by default):
  - 2: spacing between commands longer than the buffer window plus fire time; each entry seen before the next; hesitation length kept separate.
  - 3: claim d extended. The Model-default load must be a tier-2 cache hit, or else P3 passes vacuously; give the human a tell, such as the copy's mtime predating Step 4.
  - 4: claims added for `Debug.Log` reaching logcat under tag `Unity`, and for `tee` completeness.
  - 5: `ArchiveCopyFileName` is at `EndpointerOverlay.cs:137`; the set swaps are at `CommandDemo.cs:171-195`/`:260-273`.
  - 6: note which hesitation items can split under which run.
  - 7: the sample copy is a snapshot at import.
  - 8: cite criterion 5's source as F11/Risks.
  - With eager commit on, findings 1 and 2 change. Re-read them against eager commit in the fix brief.
- Human-only: no agent runs adb, builds or deploys (`.claude/bindings/unity.md` Failure rule).
- If Phase 8 fails, Phase 7 (`100a987`) is reverted whole, on the human's ruling only.
- The writer choices held for G2 and the end-of-build reconcile items (B1-B4, stale cites, `joined-rig.md` rule 2, plan-6's/plan-7's anchors) are unchanged. They are not this task's.
- Build facts:
  - Agents' Bash collapses `\\` to `\`.
  - vc-guard refuses VC-state changes to agents.
  - Windows-native Git Bash only, never WSL.
  - Unity-made `memory/handoffs/*.meta` are committed with the work.
  - Reports not filed by the hook are extracted from `subagents/agent-<id>.jsonl`. Use `cygpath -m` paths, since `MSYS_NO_PATHCONV` is set.
- A background session `feat-joined-rig-parser` may be listed at this root. It is not this branch's. If it writes in this tree, stop and tell the human.

## Next steps

1. Invoke `g0-open` for the re-lock design topic. Scope: amend `requirements.md` §9's "sooner" bar per the ruling above, plus any F11 wording it touches. Cut it as the `vc` binding's re-lock form.
2. Invoke `feature-requirement-doc` to revise §9 (by `doc-writer`). Then `g1-lock` for the human's re-lock, and merge back into `feat-endpointer-profile`.
3. Resume `phase-pickup` Phase 8 at RECONCILE: file `.scratch/architect-endpointer-profile-phase-8-fix-brief.md`, carrying findings 1-8 as ruled, eager commit in both builds (prerequisites and D4), and the amended §9 bar. Then RECORD & COMMIT.

## Open questions

- Whether the amended bar also needs the architecture doc's Phase 8 row reworded. That doc is tier 2, so the PM decides; it needs no G1.
- The exact wording the doc writer proposes for §9 is the human's call at G1.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-endpointer-profile; lane: design; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor e69d344d-9468-4259-97d3-280a0cf9a46e". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
