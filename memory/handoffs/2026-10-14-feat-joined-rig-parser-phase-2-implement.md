You're picking up: `implement` Phase 2 of `feat-joined-rig-parser`. Read the files below, then start at Next steps.

## Objective

Build Phase 2 (inputs and guards) of `feat-joined-rig-parser` exactly as `Planning~/features/joined-rig-parser/plan-2.md` says, through `code-writer` and its verification gate (criteria 0-6), then commit and push. Done means the gate is green with evidence, the commit set is pushed, and STATUS names the next step (`phase-pickup` Phase 3).

## Lane

feature

## Context

- Feature `joined-rig-parser`, Full track; design `joined-rig` G1-locked 2026-10-13. Branch `feat-joined-rig-parser` in the one checkout (`trees` none), pushed to `origin` at `83b78e6`.
- Phase 1 (harness `--dump` writer) built at `d5e500d`. Phase 2's plan persisted, validated (full depth) and fixed at `83b78e6`; `memory/STATUS.md` says: next `implement` Phase 2.

## Current state

- `plan-2.md` (9,968 bytes) is persisted and validated; the validator report is `.scratch/plan-validator-joined-rig-parser-phase-2-report.md` (eight findings fixed by an architect pass; finding 1 ruled by the maintainer, below).
- No Phase 2 code is written. Recon reports: `.scratch/code-mapper-joined-rig-parser-phase-2-report.md`, `.scratch/doc-extractor-joined-rig-parser-phase-2-report.md`.
- `Planning~/features/coverage-in-selection/ab-rig/Rig.csproj` is untracked and ignored (`git ls-files` printed nothing; `.gitignore:15`): Phase 2's commit force-adds it (`git add -f`), a PM step.

## Key files & locations

- Plan: `D:\Workspace\VoXR-Speech-Recognition\Planning~\features\joined-rig-parser\plan-2.md` (files table, contracts, gate 0-6, `## Runtime claims`).
- Architecture: `Planning~/features/joined-rig-parser/architecture.md` (32,086 bytes; 40 KB budget). Requirements: `Planning~/features/joined-rig-parser/requirements.md`.
- Rig: `Planning~/features/coverage-in-selection/ab-rig/` (`Program.cs`, `stage.sh`, `Rig.csproj`); manifest `Tests~/Fixtures/audio/manifest.json`; pin `NativeBridge~/harness/expectations.json`; recipe `Planning~/verification-recipe.md` (`## Parser A/B rig - DocCheck`).
- Real dump for orientation: `.scratch/joined/p1.jsonl` (untracked).

## Constraints & decisions

- Maintainer ruling 2026-10-14, "Exit 2, reader stop": pin and pinned-dump case sets unequal stop in the readers (exit 2); the kill check's exit 3 is for configuration, finals and failed decodes. Recorded in the architecture's *Kill check* row and *Readings* bullet.
- Gate step 0 (the rig's `--grammar` vs the pin's `grammar`) unequal is a stop for the human: never a re-pin. Harness exit 1 likewise.
- `Runtime/**`, `NativeBridge~/**` (incl. `expectations.json`), `DocCheck.cs` and the binding's Commands stay untouched.
- The PM writes only `memory/`, CHANGELOG lines and `.scratch/` briefs; code by `code-writer`, builds and tests by `compile-check`. Git writes (`git checkout --`, restores, commits) are PM steps between dispatches. Subagents may lack Write/Bash: file their reports beside the brief from the hand-back.
- Unity runs generate `memory/**/*.meta`: commit them, never delete. Commands 1-2 need the host Editor closed (`D:\Workspace\VoXR TestGround\Temp\UnityLockfile` absent).
- No CHANGELOG line (dev tooling).

## Next steps

1. Invoke `implement` for Phase 2 of `feat-joined-rig-parser`.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-joined-rig-parser; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 417b09b6-7f8c-4573-92b0-bbdb29a66af1". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; send to the one on this machine. If the bare name is refused as ambiguous, use the ref `ListAgents` shows for the local one.)
