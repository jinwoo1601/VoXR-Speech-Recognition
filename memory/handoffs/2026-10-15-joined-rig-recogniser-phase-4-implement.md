You're picking up: `implement` Phase 4 of `feat-joined-rig-recogniser`. Read the files below, then start at Next steps.

## Objective

Build Phase 4 ("tuning pass and closing gate") of `joined-rig-recogniser` through `implement`, from the persisted, validated plan `Planning~/features/joined-rig-recogniser/plan-4.md`. Done when the gate (criteria 1-14) is green, the record pass (criterion 15) has written the *Tuning record* tick-rate Evidence cell, and the phase is committed. `review-cycle` follows, then G2.

## Lane

feature

## Context

- Branch `feat-joined-rig-recogniser`, the one checkout `D:\Workspace\VoXR-Speech-Recognition` (`trees` none). Head `59abe70` ("Persist joined-rig-recogniser Phase 4 plan"), pushed.
- Design `joined-rig` is G1-locked; Full track. Phases 1-3 are built and committed (`ef1ed67`, `594f783`, `ca8c6ea`). Phase 4 is the build plan's last phase.
- STATUS `memory/STATUS.md` `## Current`: next `implement` Phase 4. Session note `memory/2026-10-15.md`, entry "Phase 4 pickup" (its rulings), and this handoff's.

## Current state

- `plan-4.md` (10,176 bytes) persisted by `architect`, validated at full depth by `plan-validator` (go after fixes; six low findings), and a fix pass applied findings 1-5. Finding 6 was not a defect. The link line is in `architecture.md` `## Build plan`.
- Nothing of the gate has run. No `J/jrr-p4-*` output exists yet.
- The plan has no code step: the phase is runs, a `.scratch/`-only probe and canary, and one doc cell.

## Key files & locations

- Plan: `Planning~/features/joined-rig-recogniser/plan-4.md`. Its preface carries rulings 1-5. It also holds criteria 1-15, the Stops and Runtime claims a-q.
- Architecture: `Planning~/features/joined-rig-recogniser/architecture.md` (*Tuning record (F7)*, `## As-built deltas`). Requirements: `Planning~/features/joined-rig-recogniser/requirements.md` (§9).
- Recon: `.scratch/code-mapper-joined-rig-recogniser-phase-4-report.md`, `.scratch/doc-extractor-joined-rig-recogniser-phase-4-report.md`. Validation: `.scratch/plan-validator-joined-rig-recogniser-phase-4-report.md`. Plan briefs and reports: `.scratch/architect-joined-rig-recogniser-phase-4-{brief,report,fix-brief,fix-report}.md`.
- Rig: `Planning~/features/coverage-in-selection/ab-rig/` (`joined.sh`, `stage.sh`, `JoinedRecogniser.cs` :38 `TickRate`).
- Phase 3 oracle dirs and inputs: `.scratch/joined/` (`jrr-p2-a.jsonl`, `jrr-p2-b.jsonl`, `jrr-p2-cmd3.stdout`, `jrr-p3-base`, `jrr-p3-f3`, `jrr-p3-f2`, `jrr-p3-f6`, `jrr-p3-f9s`, `jrr-p3-f9e`, `jrr-p3-kill.jsonl`, `jrr-p3-sealed.json`, `jrr-p3-c7.py`). Never edit them.
- Phase 3 gate briefs, for command forms: `.scratch/compile-check-joined-rig-recogniser-phase-3-gate-c-brief.md` and its report.
- Verification: `.claude/bindings/unity.md` `## verification` (Command 3); `Planning~/verification-recipe.md` (Building NativeBridge; DocCheck :227).
- Body budget: measure with `D:\Workspace\harness\scripts\harness.py` `body_bytes`, which leaves out `## Build plan`. It reads 40,712 of 40,960 bytes now.

## Constraints & decisions

- Rulings at plan approval, by the maintainer, 2026-10-15:
  - The tuning pass records and does not re-pick: 90 Hz stays.
  - One notch is 80 Hz and 120 Hz.
  - The set/unset re-made list is as the plan says. The §6 negative stops are not re-made.
  - Commands 1-2 are not re-run if `Runtime/`, `NativeBridge~/` and `Tests~/` are unchanged since `ca8c6ea`.
  - The pending labels stay unexercised.
- *Staging* trap: under the rig `Main` runs on managed thread 2. Never drive through `InjectResult`, `InjectPartialResult`, `InjectText` or `FlushPendingBuffer`.
- Never edit shipped `Runtime/`, `NativeBridge~/` or `Tests~/`, nor `expectations.json`, the manifest or the dumps. Probes and canaries edit only `.scratch/` staged copies.
- `stage.sh` cannot run with path conversion off: it always runs under `env -u MSYS_NO_PATHCONV`. Hand `score`/`report` arguments are drive-letter paths (`cygpath -am`).
- Line endings: canary JSON is written LF (`newline=''`). Staged C# copies are CRLF and keep their endings; never check them for "no CR".
- A runtime claim reported false or unchecked at the first gate stops for the human, with no LOOP. Never relax a compare or lengthen the horizon.
- The report filer misses background agents' reports. After each dispatch, check `.scratch/`, and file by hand from the agent's `SubagentHandback` tool input in `~/.claude/projects/D--Workspace-VoXR-Speech-Recognition/<session>/subagents/agent-<id>.jsonl`. Open it by its `cygpath -w` path in Python.
- Process rules:
  - Compiler and test output goes to `compile-check`, never the main thread.
  - Agents never run git writes; commits are the PM's.
  - No CHANGELOG line per phase.
  - Windows-native only.

## Next steps

1. Invoke `implement` for `joined-rig-recogniser` Phase 4. The plan is `plan-4.md`; its validation report is `.scratch/plan-validator-joined-rig-recogniser-phase-4-report.md`.

## Open questions

- After criterion 15 the body sits near 40.9 KB, and the Phase 4 as-built entry will cross 40,960. The human rules at the as-built pass: route Phases 1-3 as-built out to a dated record, or take a `memory/doctor-rulings.md` exemption.
- The Quest on-device check, and reading the 16-fixture recogniser-layer report, stay the human's, at G2.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-joined-rig-recogniser; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 2b40e44f-f68f-4c0e-9f87-a9d67fafdb13". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
