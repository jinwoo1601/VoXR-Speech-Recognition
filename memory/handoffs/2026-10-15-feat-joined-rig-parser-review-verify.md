You're picking up: `review-cycle` on `feat-joined-rig-parser` from its VERIFY step. Read the files below, then start at Next steps.

## Objective

Finish the full-depth `review-cycle` of `feat-joined-rig-parser` against `main`: VERIFY the ten deduped findings F1-F10 in the collation, then RULE, FIX, CHECK and COMMIT as the skill says. Done means every finding has a verifier verdict, every CONFIRMED one is fixed (or ruled by the human where the skill requires), the checks are green, and the fixes are committed and pushed. G2 follows, not here.

## Lane

feature

## Context

- Feature `joined-rig-parser`, Full track; design `joined-rig` G1-locked 2026-10-13. Branch `feat-joined-rig-parser` in the one checkout (`trees` none), project root `D:/Workspace/VoXR-Speech-Recognition`, pushed at `ac61cc7`.
- All four build phases are built. Phase 4 (`ac61cc7`): criteria 1-9 and 12-14 green; the canary (10-11) was refused three times by the auto-mode classifier, and the maintainer ruled "Just go for review cycle" (recorded in `memory/2026-10-15.md`, last entry), so Phase 3's `.scratch/joined/p3-canary/report.md` is the latest canary evidence for F5/F6 at G2.
- STATUS: `memory/STATUS.md` line 8. Session note: `memory/2026-10-15.md` (the last two entries).

## Current state

- BRIEF, FAN OUT and DEDUP are done. All 11 full-profile angles (LINE, GONE, XFILE, WRAP, REUSE, SIMP, EFF, ALT, CONV, GAP, PIT) returned `DONE`; six had no findings.
- The deduped findings F1-F10, with their source angles, `file:line` and evidence, and the unnumbered Notes, are in the collation. No verifier has run.
- Uncommitted: one ruling line added to `memory/2026-10-15.md` (plus this handoff's entry). Untracked and left alone: `.claude/csharpier/` (a csharpier pre-edit hook snapshot from a refused edit) and `.claude/references/vc-git/__pycache__/`.

## Key files & locations

- Review brief: `D:/Workspace/VoXR-Speech-Recognition/.scratch/review-joined-rig-parser-brief.md`. Its `## Changed files`, `## Load-bearing invariants` 1-13 and `## Declared out of scope` are what verifiers refute against.
- Collation: `D:/Workspace/VoXR-Speech-Recognition/.scratch/review-joined-rig-parser-collation.md`. F1-F10 verbatim, plus a `## Next` line with the per-finding owners.
- Code under review: `Planning~/features/coverage-in-selection/ab-rig/` (`Joined.cs`, `JoinedInput.cs`, `JoinedScorer.cs`, `JoinedReport.cs`, `joined.sh`, `stage.sh`, `Program.cs`, `Rig.csproj`), and `NativeBridge~/harness/main.cpp`.
- Canon: `Planning~/features/joined-rig-parser/requirements.md`, `architecture.md` (39,355 bytes of 40,960) and `plan-1.md` to `plan-4.md`.
- Phase 4 evidence: `.scratch/compile-check-joined-rig-parser-phase-4-gate-{a,c}-report.md`, and `.scratch/joined/p4*`.

## Constraints & decisions

- **Ruled 2026-10-15:** the tuning pass records only. `MinScore` 0.6 and `MinConfidence` 0.4 stay, and a finding arguing for other values is out of scope.
- **Ruled 2026-10-15:** "Just go for review cycle". The Phase 4 canary is not re-made.
- **Ruled 2026-10-14:** `Parse` returns results in utterance order, with no sort.
- **F4 (scorer branches never exercised)** touches the canary ruling and may widen scope. The collation flags it for a human ruling, not fix-by-default.
- **Fix owners:** `code-writer` via `implement` for `ab-rig/*.cs`, `Rig.csproj` and `main.cpp`. A `main.cpp` change re-runs Command 3, and the arm64 half per the recipe. A rig change re-runs DocCheck and `joined.sh` set and unset on `.scratch/joined/p4.jsonl`.
- `Runtime/**`, `expectations.json`, the recipe and the bindings stay untouched. Harness exit 1 is a human stop, never a re-pin.
- The auto-mode classifier refused the canary edit to `Runtime/Commands/VoxrCommandParser.cs` ("[Security Test Removal]") and, once, the PM's `git rm`. A refused call is reported, never retried another way.
- **Who runs what:**
  - Agents never run git writes, so commits are the PM's, per the `vc` binding's check-in procedure.
  - Commands 1-2 need the host Editor closed (`D:\Workspace\VoXR TestGround\Temp\UnityLockfile` absent). Unity runs generate `memory/**/*.meta`: commit them, never delete them.
- **Briefs and reports:**
  - Name the project root `D:/Workspace/VoXR-Speech-Recognition` in every brief, with "write by it if `pwd -W` disagrees".
  - The report-filer hook does not file reports. The PM files each one beside its brief from `subagents/agent-<id>.jsonl` under this session's Claude Code project directory (`~/.claude/projects/D--Workspace-VoXR-Speech-Recognition/<session-id>/subagents/`), passing the path to `python3 -I` in Windows form (`cygpath -w`).
- No CHANGELOG line (dev tooling). `core.autocrlf=true` (deferral WND-2).

## Next steps

1. Invoke `review-cycle` for `feat-joined-rig-parser` against `main`, resuming at VERIFY with the existing brief and collation above: one `finding-verifier` per F1-F10, then RULE onward.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-joined-rig-parser; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 9d0d8073-3b67-43a3-9bc1-5199c785ea29". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; send to the one on this machine. If the bare name is refused as ambiguous, use the ref `ListAgents` shows for the local one.)
