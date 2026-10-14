You're picking up: `implement` for `feat-windows-native-dotnet-fixtures` Phase 1, from step 4 GATE. Read the files below, then start at Next steps.

## Objective

Gate Phase 1 of `windows-native-dotnet-fixtures`: dispatch `compile-check` on the plan's gates 0-8. Then loop if needed (at most two fixes), FILE, REPORT, and commit the three edited files. Done = Phase 1 gated green and committed, or stopped at a plan defect for the human.

## Lane

feature

## Context

- Row 2 of the locked design `Planning~/design-docs/windows-native-toolchain.md`. Branch `feat-windows-native-dotnet-fixtures`, one checkout (`trees` none), project root `D:\Workspace\VoXR-Speech-Recognition`. No board; `memory/STATUS.md` is the state.
- Plan `Planning~/features/windows-native-dotnet-fixtures/plan-1.md`: validated (GO) and fixed, committed and pushed at `f30e820`. The human approved it in plan mode on 2026-10-14, including one departure, decision A: the binding's Path form line ends `; the working-dir read `pwd -W` already prints this form (`D:/…`)`, which clears doctor's V21 warning. That decision is recorded in the architecture doc's register and `## Data contract`.

## Current state

- Steps 1-3 of `implement` are done. The code-writer reported DONE: `.claude/bindings/unity.md` (:9, :12, :15, :17), `Planning~/verification-recipe.md` (Step 2 sites; DocCheck section at :222) and `NativeBridge~/include/vosk_api.h` :3. These edits are **uncommitted in the tree**.
- The writer's text checks passed: no `wsl|/mnt/` in the three files; `already prints this form` appears once. Writer notes: the "Then:" connective was dropped in the DocCheck section; the csproj paragraph was rewrapped to five lines; the recipe's :71 date was written as `2026-10-14`, for the PM to confirm after the green Command 4 run (plan F3).
- Fix iterations used: 0.
- Filed: brief `.scratch/implement-windows-native-dotnet-fixtures-phase-1-brief.md`; writer report `.scratch/implement-windows-native-dotnet-fixtures-phase-1-writer-report.md`. Recon, validator and architect reports are in `.scratch/*-windows-native-dotnet-fixtures-phase-1-*report.md` (trust them).

## Key files & locations

- Gate definition: `plan-1.md` `## Acceptance criteria` (gates 0-8, in order) and `## Runtime claims`. Every claim must come back true or false with evidence; one false or unchecked is a plan defect, so stop and report, with no loop.
- Commands: `.claude/bindings/unity.md` `## verification` (Commands 3 and 4, as now edited); the DocCheck line in the recipe's `## Parser A/B rig — DocCheck`.
- Command 3's no-recommit check method: `Planning~/features/windows-native-harness/plan-2.md` criterion 1 (the `llvm-nm` symbol diff against `.scratch/windows-native-harness-phase-1-symbols-committed.txt`).
- Precedent gate briefs: `.scratch/compile-check-windows-native-harness-phase-3-gate-part{1,2}-brief.md`.

## Constraints & decisions

- Builds run only in `compile-check`, never on the main thread. Agents can't write under `.scratch/` report files, and the hook filer is off: the PM files each report from the subagent transcript. A helper that extracts it (handles `SubagentHandback`) is at `C:\Users\excal\.claude\jobs\be6bd62b\tmp\file_report.py <agent id> <out>`. It is job-scoped and may be gone; if so, rewrite it.
- Agents cannot download: a NuGet restore needing network → a human `!` step.
- Don't write WSL or `/mnt/` anywhere new. Don't touch `.claude/settings.local.json`, history docs, or product docs. Leave `.claude/references/vc-git/__pycache__/` untracked. Commit Unity-generated `memory/**/*.meta`; never delete them.
- Commit set at implement: the three edited files, plus `architecture.md` if the run changes it (As-built deltas via `architect-reconcile`). No CHANGELOG line (dev tooling).
- Listed for the next gate: adr-0005's Consequences text still names only the venv layout and interpreter (covered by the path-form ruling); the doc-extractor noted `vosk_api.h` :4 "Do not modify", which concerns declarations, not the :3 comment.

## Next steps

1. Invoke `implement` for `windows-native-dotnet-fixtures` Phase 1, resuming at GATE (step 4); the writer receipt is already DONE.
2. After REPORT: commit, then `phase-pickup` Phase 2. That pickup is a phase boundary, so it hands off.

## Open questions

- None pending for the human.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-windows-native-dotnet-fixtures; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor be6bd62b-4570-47f3-a346-b3681750cfd5". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer.
