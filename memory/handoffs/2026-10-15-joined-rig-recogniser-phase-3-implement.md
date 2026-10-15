You're picking up: `implement` Phase 3 of `feat-joined-rig-recogniser` from its DISPATCH step. Read the files below, then start at Next steps.

## Objective

Run `implement` for Phase 3 ("recogniser-layer scoring and report") of `joined-rig-recogniser`, starting at DISPATCH:
- one `code-writer` on the filed brief;
- then GATE (criteria 2-15 via `compile-check`), LOOP if needed, FILE, REPORT;
- then the PM commit of the seven AB files.

Done when Phase 3 is built, gated green and committed, with STATUS advanced.

## Lane

feature

## Context

- Branch `feat-joined-rig-recogniser`, in the one checkout `D:\Workspace\VoXR-Speech-Recognition` (`trees` none). Head is `81e2aa3` (Phase 3 plan persisted), pushed, plus this handoff's commit.
- Design `joined-rig` is G1-locked; the track is Full.
- Phases 1 and 2 are committed (`ef1ed67`, `594f783`).
- Phase 3 plan: `Planning~/features/joined-rig-recogniser/plan-3.md`, validated full, with the fix pass applied.
- STATUS `memory/STATUS.md` `## Current` line 8: next `implement` Phase 3. The session note is `memory/2026-10-15.md`, entries "Phase 3 pickup" and this handoff's.

## Current state

- Step 0 (Dispatch A) is done and green. Report filed: `.scratch/compile-check-joined-rig-recogniser-phase-3-step0-report.md`.
  - Parser-layer `main`-vs-`main` on `jrr-p2-a.jsonl`: exit 0, `## Flips: 0`.
  - The F11 baseline is kept in `.scratch/joined/jrr-p3-base/` (`scored-a.jsonl`, `scored-b.jsonl`, `report.md`, sha256 in the report). A and B differ only in the header `side` key, which is expected.
  - DocCheck passes 66/66, 0 warnings, no `FAIL`.
  - `git status --short` shows only the two untracked `.claude/` entries.
- The code-writer brief is filed and not yet dispatched: `.scratch/implement-joined-rig-recogniser-phase-3-brief.md`. It covers Steps 1-6 as one atomic dispatch, runtime claims a-n, and the writer's check run W1-W9 into `.scratch/jrr-p3-writer/`.
- Fix iterations used: 0. No rig source has been edited yet.

## Key files & locations

- Plan: `Planning~/features/joined-rig-recogniser/plan-3.md` (Dispatch: A Step 0, done; B `code-writer` Steps 1-6; C `compile-check` criteria 2-15; Stops; Runtime claims a-n).
- Architecture: `Planning~/features/joined-rig-recogniser/architecture.md` (register entries; the `## Risks` *Readings* bullet, whose last sentence is the F6 negative-case ruling).
- Rig: `Planning~/features/coverage-in-selection/ab-rig/`. The seven files in scope are `JoinedInput.cs`, `JoinedScorer.cs`, `JoinedRecogniser.cs`, `Joined.cs`, `JoinedReport.cs`, `joined.sh` and `Rig.csproj`.
- Recon: `.scratch/code-mapper-joined-rig-recogniser-phase-3-report.md`, `.scratch/plan-validator-joined-rig-recogniser-phase-3-report.md`.
- Verification: `.claude/bindings/unity.md` `## verification`; DocCheck in `Planning~/verification-recipe.md`. Commands 1-4 do not trigger, since nothing changes under `Runtime/`, `NativeBridge~/` or `Samples~/`.
- Prior gate brief, for form: `.scratch/compile-check-joined-rig-recogniser-phase-2-gate-c-brief.md`.

## Constraints & decisions

- Maintainer ruling, 2026-10-15: a negative case whose command goes pending, is never confirmed and never fires is `correct-no-fire`, shown "pending, timed out". F6 is not amended.
- *Staging* trap: under the rig, `Main` runs on managed thread 2. Never drive through `InjectResult`, `InjectPartialResult`, `InjectText` or `FlushPendingBuffer`.
- No edit to shipped `Runtime/`, `NativeBridge~/` or `Tests~/`, `expectations.json`, the manifest or the dumps. Canaries edit only `.scratch/` staged copies.
- A runtime claim reported false or unchecked at the first gate stops for the human, with no LOOP. Never relax a compare or lengthen the horizon.
- Canary files are written LF (`newline=''`).
- The report filer misses background agents' reports: after each dispatch check `.scratch/`, and file by hand from the transcript `~/.claude/projects/D--Workspace-VoXR-Speech-Recognition/<session>/subagents/agent-<id>.jsonl`.
- Agents never run git writes; commits are the PM's (`vc` binding: `git add -- <paths>`, `git commit -F <file> -- <paths>`, `git push`).
- Compiler and test output goes to `compile-check`, never the main thread.
- No CHANGELOG line per phase; it comes post-G2.
- Windows-native only; `stage.sh` and `joined.sh` run under `env -u MSYS_NO_PATHCONV`.

## Next steps

1. Invoke `implement` for `joined-rig-recogniser` Phase 3, resuming at DISPATCH with the filed brief `D:\Workspace\VoXR-Speech-Recognition\.scratch\implement-joined-rig-recogniser-phase-3-brief.md`. BRIEF and Dispatch A are done.

## Open questions

- None known. The Quest on-device check stays the human's, at G2.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-joined-rig-recogniser; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor f2da0fe0-38a2-444c-8938-f64707be4859". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
