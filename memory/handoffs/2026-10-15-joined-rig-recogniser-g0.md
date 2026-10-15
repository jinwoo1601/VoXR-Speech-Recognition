You're picking up: open the backlog feature `joined-rig-recogniser` at G0. Read the files below, then start at Next steps.

## Objective

Agree the G0 scope of `joined-rig-recogniser` with the human, then cut `feat-joined-rig-recogniser` and record its scope, through `g0-open`. Done when the branch exists, the requirements doc holds §1 (user story) and §2 (scope), and STATUS carries the branch's line. Nothing beyond G0.

## Lane

feature

## Context

- Design `joined-rig` (`Planning~/design-docs/joined-rig.md`), G1-locked 2026-10-13, re-locked with Amendment A1 (adr-0006). Its `## 6. Feature backlog` (line 163) row, verbatim: "`joined-rig-recogniser` | Phase 2 (adr-0002). Stage `VoxrCommandRecogniser` outside Unity, driven on audio time from the dump's partials and timestamps, and score the recogniser-layer cases. Unblocks R11 and R17. **Full.** | `joined-rig-parser`". Track: **Full** (writes its own requirements and architecture docs).
- Its prerequisite `feat-joined-rig-parser` is accepted at G2 (2026-10-15) and merged into `main` as PR #177 (`49c19e3`); close-out `da417c9`. Project root `D:/Workspace/VoXR-Speech-Recognition`, one checkout (`trees` none), currently on `main`.
- STATUS: `memory/STATUS.md` `## Current` line 7 (main headline names this as next). Latest session note: `memory/2026-10-15.md` (the last two entries).

## Key files & locations

- Design and ADRs: `Planning~/design-docs/joined-rig.md`; `Planning~/design-docs/joined-rig/decisions/adr-0001` … `adr-0006`.
- The parser feature's canon, for what phase 1 built: `Planning~/features/joined-rig-parser/requirements.md`, `architecture.md` (decision register; *Dump* and *Audio time* decisions; `## As-built deltas`).
- The built rig: `Planning~/features/coverage-in-selection/ab-rig/` (`Joined*.cs`, `joined.sh`, `stage.sh`); the dump writer: `NativeBridge~/harness/main.cpp` (`--dump`).
- SCOPE inputs `g0-open` reads: `memory/backlog-candidates.md`, `memory/notes-for-future-features.md`.

## Constraints & decisions

- Deferral **JRP-1** (STATUS `## Deferred`, line 28) routes to this feature: exact partial timing (the dump brackets partials only ring-wide, ruled (a)) and the exact per-case word-time origin (Vosk times run on across resets) — a bridge change. Name it at SCOPE.
- The rig is an on-demand instrument (adr-0004), not shipped; no CHANGELOG line for dev tooling.
- Windows-native only: Git Bash, native .NET SDK 10, MSVC harness; never WSL.
- The auto-mode classifier refused edits to `Runtime/Commands/VoxrCommandParser.cs` three times during the parser feature ("[Security Test Removal]"); a refused call is reported, never retried another way.
- Agents never run git writes; commits are the PM's per the `vc` binding. Commands 1-2 need the host Editor closed; Unity runs generate `memory/**/*.meta` — commit them, never delete.

## Next steps

1. Invoke `g0-open` for the backlog feature `joined-rig-recogniser` (design `joined-rig`, backlog row line 163, Full).

## Open questions

- The user story and scope are the human's to state at SCOPE, including whether JRP-1's bridge change is in this feature's scope.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-joined-rig-recogniser; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor ebd39c8d-125c-4126-9ecc-94e3562830fc". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; send to the one on this machine. If the bare name is refused as ambiguous, use the ref `ListAgents` shows for the local one.)
