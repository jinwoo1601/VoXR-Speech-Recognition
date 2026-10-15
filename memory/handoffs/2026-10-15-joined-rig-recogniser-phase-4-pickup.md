You're picking up: `phase-pickup` Phase 4 of `feat-joined-rig-recogniser`. Read the files below, then start at Next steps.

## Objective

Plan Phase 4 ("tuning pass and closing gate") of `joined-rig-recogniser` through `phase-pickup`. Done when `Planning~/features/joined-rig-recogniser/plan-4.md` is persisted and validated, the architecture doc's `## Build plan` links it, and STATUS names `implement` Phase 4 as next.

## Lane

feature

## Context

- Branch `feat-joined-rig-recogniser`, in the one checkout `D:\Workspace\VoXR-Speech-Recognition` (`trees` none). Head is `ca8c6ea` (Phase 3 built), pushed, plus this handoff's commit.
- Design `joined-rig` is G1-locked; the track is Full. Phases 1-3 are committed (`ef1ed67`, `594f783`, `ca8c6ea`).
- Phase 4 per `architecture.md` `## Build plan`: the tick-rate probe into the *Tuning record* (one notch each way from 90 Hz on the 16 fixtures; record whether any class moves); every acceptance run re-made with `MSYS_NO_PATHCONV` set and unset; Command 3; DocCheck; phase 1's F11 run. Discharges F7 (record), F10, F11, requirements §10 item 3. It is the build plan's last phase; `review-cycle` follows it, then G2.
- STATUS `memory/STATUS.md` `## Current` line 8: next `phase-pickup` Phase 4. The session note is `memory/2026-10-15.md`, entries "Phase 3 implement" and this handoff's.

## Current state

- Phase 3 is done: gate C criteria 2-15 PASS, runtime claims a-n true. Claim j came back unchecked at the gate; the human ruled for a direct check, which passed (trace byte-identical with the extra `Config()` recogniser removed).
- `architecture.md` `## As-built deltas` has `### Phase 3 — as built (2026-10-15)`.
- Nothing for Phase 4 has been started: no recon, no plan.

## Key files & locations

- Architecture: `Planning~/features/joined-rig-recogniser/architecture.md` (`## Build plan` Phase 4; `## Non-functional realization` tick-rate row, provisional 90 Hz; *Tuning record*; `## As-built deltas` Phases 1-3).
- Requirements: `Planning~/features/joined-rig-recogniser/requirements.md` (F7, F10, F11, §10 item 3).
- Prior plan, for form and the 10 KB budget: `Planning~/features/joined-rig-recogniser/plan-3.md`.
- Rig: `Planning~/features/coverage-in-selection/ab-rig/` (`joined.sh --layer parser|recogniser`, `stage.sh`, `JoinedRecogniser.cs` `TickRate`).
- Phase 3 reports: `.scratch/implement-joined-rig-recogniser-phase-3-report.md`, `.scratch/compile-check-joined-rig-recogniser-phase-3-gate-c-report.md`, `.scratch/compile-check-joined-rig-recogniser-phase-3-claim-j-report.md`. Dumps: `.scratch/joined/jrr-p2-a.jsonl`, `jrr-p2-b.jsonl`.
- Verification: `.claude/bindings/unity.md` `## verification`; `Planning~/verification-recipe.md` (Building NativeBridge; DocCheck).

## Constraints & decisions

- Maintainer ruling 2026-10-15: a negative case that goes pending, is never confirmed and never fires is `correct-no-fire`, shown "pending, timed out". F6 is not amended.
- *Staging* trap: under the rig `Main` runs on managed thread 2. Never drive through `InjectResult`, `InjectPartialResult`, `InjectText` or `FlushPendingBuffer`.
- No edit to shipped `Runtime/`, `NativeBridge~/` or `Tests~/`, `expectations.json`, the manifest or the dumps. Canaries edit only `.scratch/` staged copies.
- A runtime claim reported false or unchecked at the first gate stops for the human, with no LOOP. Never relax a compare or lengthen the horizon. Make each plan claim checkable by a named criterion; j was not.
- Line endings: canary dumps are written LF (`newline=''`). Staged C# copies are CRLF (`core.autocrlf=true`); a staged edit keeps the file's endings and is never checked for "no CR".
- The report filer misses background agents' reports: after each dispatch check `.scratch/`, and file by hand from the agent's `SubagentHandback` tool input in `~/.claude/projects/D--Workspace-VoXR-Speech-Recognition/<session>/subagents/agent-<id>.jsonl` (open it by its `cygpath -w` path in Python).
- Agents never run git writes; commits are the PM's (`vc` binding).
- Compiler and test output goes to `compile-check`, never the main thread.
- No CHANGELOG line per phase; it comes post-G2.
- Windows-native only; `stage.sh` and `joined.sh` run under `env -u MSYS_NO_PATHCONV`.

## Next steps

1. Invoke `phase-pickup` for `joined-rig-recogniser` Phase 4.

## Open questions

- `## Timing`'s `pending` / `pending, timed out` labels were never exercised on real data, since no fixture case goes pending. Raise it if Phase 4's tick-rate probe could show it.
- The Quest on-device check stays the human's, at G2.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-joined-rig-recogniser; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 5ee4ad8d-0334-4713-b82e-1b71d4d92aba". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
