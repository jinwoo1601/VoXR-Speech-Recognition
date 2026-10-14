You're picking up: `phase-pickup` Phase 4 of `feat-joined-rig-parser`. Read the files below, then start at Next steps.

## Objective

Plan Phase 4 (tuning pass and closing gate) of `feat-joined-rig-parser` through `phase-pickup`: recon, the plan persisted as `Planning~/features/joined-rig-parser/plan-4.md`, validated by `plan-validator`, approved by the human, committed and pushed. Done means STATUS names `implement` Phase 4 as next.

## Lane

feature

## Context

- Feature `joined-rig-parser`, Full track; design `joined-rig` G1-locked 2026-10-13. Branch `feat-joined-rig-parser` in the one checkout (`trees` none), project root `D:/Workspace/VoXR-Speech-Recognition`, pushed to `origin` at `7e8a297` plus this handoff's commit.
- Phases 1 (`d5e500d`), 2 (`371596c`) and 3 (`ad37ade`) are built with green gates. The human accepted the Phase 3 build on 2026-10-15 ("accepted"). That was not G2: G2 comes after Phase 4 and the review.
- The architecture doc was reconciled to as-built for Phases 1-3 at `7e8a297`: three `## As-built deltas` entries, and the `Parse`-order ruling amended into *The fire*. The body is 38,561 bytes of the 40 KB budget, so Phase 4's link line and its as-built pass must stay tight.
- STATUS: `memory/STATUS.md` line 8. Session note: `memory/2026-10-15.md`.

## Current state

- No Phase 4 plan, recon or code exists.
- Phase 4's scope is the architecture's `## Build plan` *Phase 4* row:
  - The tuning pass runs the rig on the 16 fixtures and writes each number's evidence into the *Tuning record* (F10).
  - The PM runs `git rm -r Planning~/features/fidelity-miss-cost/ab-rig` and deletes the leftover `Rig.csproj`.
  - Every acceptance run is re-made from Claude's Git Bash, `joined.sh` with `MSYS_NO_PATHCONV` set and unset.
  - Command 3 and DocCheck.
  - The predecessor feature's F7 sweep (`windows-native-dotnet-fixtures`, *The sweep*) prints nothing over the new and edited files.
  - It discharges F9, F10, F11 at G2, §2 In 8 and §10 item 2.

## Key files & locations

- Architecture: `Planning~/features/joined-rig-parser/architecture.md` (`## Build plan`; `### Tuning record (F10)`; `## As-built deltas`).
- Requirements: `Planning~/features/joined-rig-parser/requirements.md`.
- Earlier plans, for form only: `Planning~/features/joined-rig-parser/plan-1.md`, `plan-2.md` and `plan-3.md` (10 KB plan budget; `plan-3.md` was 10,207 bytes).
- Rig: `Planning~/features/coverage-in-selection/ab-rig/` (`Joined.cs`, `JoinedInput.cs`, `JoinedScorer.cs`, `JoinedReport.cs`, `joined.sh`, `Program.cs`, `stage.sh`, `Rig.csproj`).
- Real dump: `.scratch/joined/p2.jsonl` (untracked). The Phase 3 canary report kept for the maintainer's F6 read at G2: `.scratch/joined/p3-canary/report.md`.
- Recipe: `Planning~/verification-recipe.md` `## Parser A/B rig - DocCheck`. Binding: `.claude/bindings/unity.md` `## verification`.

## Constraints & decisions

- **Ruled 2026-10-14:** `Parse` returns its results in utterance order (one winner per round, no sort). The primary is the first fire-gated result.
- `Runtime/**`, `NativeBridge~/**` (except a planned rebuild), `expectations.json`, `DocCheck.cs`, the recipe and the binding stay untouched unless the plan names them. A grammar mismatch or a harness exit 1 is a stop for the human, never a re-pin.
- **Who runs what:**
  - The vc-guard refuses agents' git writes. Any revert and the `git rm` are PM steps between dispatches.
  - Unity runs generate `memory/**/*.meta`: commit them, never delete them. Commands 1-2 need the host Editor closed (`D:\Workspace\VoXR TestGround\Temp\UnityLockfile` absent).
- **Briefs and reports:**
  - Name the project root `D:/Workspace/VoXR-Speech-Recognition` explicitly in every brief, with "write by it if `pwd -W` disagrees".
  - Subagents cannot write `.scratch/` reports. Each report comes back as hand-back text, which the PM files beside its brief.
- `core.autocrlf=true`: new LF files check out CRLF (deferral WND-2). No CHANGELOG line (dev tooling).
- The untracked `.claude/references/vc-git/__pycache__/` predates this work: leave it.

## Next steps

1. Invoke `phase-pickup` for Phase 4 of `feat-joined-rig-parser`.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-joined-rig-parser; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 0e21981e-c9cb-4818-8b88-6a55d5f1e00f". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; send to the one on this machine. If the bare name is refused as ambiguous, use the ref `ListAgents` shows for the local one.)
