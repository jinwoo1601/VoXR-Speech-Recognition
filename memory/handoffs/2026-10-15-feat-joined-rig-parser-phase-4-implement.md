You're picking up: `implement` Phase 4 of `feat-joined-rig-parser`. Read the files below, then start at Next steps.

## Objective

Build Phase 4 (tuning pass and closing gate) of `feat-joined-rig-parser` through `implement`, exactly as `Planning~/features/joined-rig-parser/plan-4.md` says, through its gate (criteria 1-14). Done means every criterion is green with evidence, the Tuning record is written, and the phase is committed and pushed. G2 comes after the review that follows, not here.

## Lane

feature

## Context

- Feature `joined-rig-parser`, Full track; design `joined-rig` G1-locked 2026-10-13. Branch `feat-joined-rig-parser` in the one checkout (`trees` none), project root `D:/Workspace/VoXR-Speech-Recognition`, pushed to `origin` at `15e5fda` plus this handoff's commit.
- Phases 1-3 are built (`d5e500d`, `371596c`, `ad37ade`) and reconciled to as-built (`7e8a297`). Phase 4's plan was approved by the maintainer in plan mode, persisted, validated at full depth, fixed, and committed at `15e5fda`.
- STATUS: `memory/STATUS.md` line 8. Session note: `memory/2026-10-15.md` (the last two entries).

## Current state

- `plan-4.md` is persisted and validated; the validator report is `.scratch/plan-validator-joined-rig-parser-phase-4-report.md`, and its six findings are fixed (`.scratch/architect-joined-rig-parser-phase-4-fix-report.md`).
- Nothing of the build has run: `fidelity-miss-cost/ab-rig` is still tracked, no `sj/p4*` output exists, and the Tuning record still reads "Tuning pass: pending.".
- `git status --short -- Runtime/` is empty.

## Key files & locations

- Plan: `Planning~/features/joined-rig-parser/plan-4.md` — its criteria are the gate, in order, with the dispatch each step goes to.
- Architecture: `Planning~/features/joined-rig-parser/architecture.md` (`### Tuning record (F10)`; `## As-built deltas` charter comment). Body 38,627 bytes of 40,960.
- Precedent run lines: `Planning~/features/joined-rig-parser/plan-3.md` `## Acceptance criteria` 2-8.
- Rig: `Planning~/features/coverage-in-selection/ab-rig/` (`joined.sh`, `stage.sh`, `JoinedScorer.cs` :23-24).
- Recon: `.scratch/code-mapper-joined-rig-parser-phase-4-report.md`, `.scratch/doc-extractor-joined-rig-parser-phase-4-report.md`.
- Recipe: `Planning~/verification-recipe.md` (`### Building NativeBridge`; `## Parser A/B rig — DocCheck`). Binding: `.claude/bindings/unity.md` `## verification` (Commands 1-3).
- Phase 2's real dump, for reference only: `.scratch/joined/p2.jsonl`. Phase 4 makes its own `p4.jsonl`.

## Constraints & decisions

- **Ruled 2026-10-15 at plan approval:** the tuning pass records only. `MinScore` 0.6 and `MinConfidence` 0.4 stay. Evidence is the citations re-verified, the 16-fixture counts, and a `.scratch/`-only sensitivity probe. No tracked code change.
- **Ruled 2026-10-14:** `Parse` returns results in utterance order.
- `Runtime/**`, `NativeBridge~/**`, `expectations.json`, ab-rig sources, the recipe and the binding stay untouched, except the canary's temporary `VoxrCommandParser.cs` edit. A harness exit 1, a set-vs-unset difference, a sweep hit, or a body over 40,960 bytes is a stop for the human, never a re-pin or a silent trim (plan-4 *Human stops*).
- **Who runs what:**
  - The vc-guard refuses agents' git writes, so the `git rm -r` (criterion 1) and the `git checkout --` revert (criterion 11) are PM steps between dispatches.
  - Unity runs generate `memory/**/*.meta`: commit them, never delete them. Commands 1-2 need the host Editor closed (`D:\Workspace\VoXR TestGround\Temp\UnityLockfile` absent; it was absent at recon).
  - If CMake refuses a stale `NativeBridge~/build` cache, the PM moves it to `.scratch/`, never deletes it.
- **Briefs and reports:**
  - Name the project root `D:/Workspace/VoXR-Speech-Recognition` explicitly in every brief, with "write by it if `pwd -W` disagrees".
  - Subagents cannot write `.scratch/` reports, and the report-filer hook did not file them in this session. The PM files each report beside its brief from `subagents/agent-<id>.jsonl` under the session's Claude Code project directory.
  - Read-only agents (architect, validator) have no Bash, so byte counts are the PM's to run.
- `core.autocrlf=true`: new LF files check out CRLF (deferral WND-2). No CHANGELOG line (dev tooling).
- The untracked `.claude/references/vc-git/__pycache__/` predates this work: leave it.

## Next steps

1. Invoke `implement` for Phase 4 of `feat-joined-rig-parser` (plan `Planning~/features/joined-rig-parser/plan-4.md`).

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-joined-rig-parser; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor df3d6862-c82f-49be-bbb2-42d8e6d82429". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; send to the one on this machine. If the bare name is refused as ambiguous, use the ref `ListAgents` shows for the local one.)
