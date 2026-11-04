You're picking up: build Phase 1 (pause-ladder corpus) of `feat-endpointer-profile` through `implement`. Read the files below, then start at Next steps.

## Objective

Run `implement` for Phase 1 of `endpointer-profile` from its persisted, validated plan `Planning~/features/endpointer-profile/plan-1.md`. Done when Phase 1's green criteria hold — Command 3 at 37/37 with the pin diff harness-written, Commands 1–2 green — and the work is reviewed and checked in, or when a stop rule of the plan fires and the human has been asked.

## Lane

feature

## Context

- Branch `feat-endpointer-profile`, the one checkout (`trees` is none), head `827fa20`, pushed. Stage: implement (STATUS `## Current`). Session note: `memory/2026-10-16.md` (last entry "Phase 1 pickup").
- Requirements `Planning~/features/endpointer-profile/requirements.md` (F1, §4.5, §8, §9); architecture `Planning~/features/endpointer-profile/architecture.md` (`## Decision register` "Pause ladder — 21 split entries, pinned by the harness"; `### The pause ladder`; `## Build plan` Phase 1 row and its link line); design (locked) `Planning~/design-docs/endpointer-profile.md`, ADRs `Planning~/design-docs/endpointer-profile/decisions/`.
- The plan was approved by the human in plan mode on 2026-10-16. It departs from the Build-plan row in two ways, both approved: the recipe's step 2b "16/16" → "37/37" alongside the binding (`doc-writer`), and the band-check method (a `.scratch/` probe with 10 ms RMS frames, at −40 and −50 dBFS).

## Current state

- Plan persisted (10,204 B, at the 10 KB budget). Validated at `full` depth, with one fix pass. Nothing implemented yet; the tree is clean apart from the untracked `.claude/csharpier/` and `.claude/references/vc-git/__pycache__/`, which predate this work and are not ours.
- Plan trail in `.scratch/`:
  - `architect-endpointer-profile-phase-1-{brief,report,fix-brief,fix-report}.md`
  - `plan-validator-endpointer-profile-phase-1-{brief,report}.md` (its report closes with the PM rulings)
  - recon `code-mapper-…-phase-1-report.md` and `doc-extractor-…-phase-1-report.md`
  - the approved plan text `endpointer-profile-phase-1-approved-plan.md`
- Already on disk, verified by Glob: the piper venv `C:\Users\excal\.cache\voxr-fixture-gen\venv\Scripts\piper.exe`, the voice `…\voices\en_US-lessac-medium.onnx`, `NativeBridge~/vendor/vosk-model-small-en-us-0.15`, and `NativeBridge~/build-windows/bin/vosk-bridge-harness.exe`. No human download step is needed.

## Key files & locations

- Plan: `Planning~/features/endpointer-profile/plan-1.md`. Its steps 1–7, stop rules, dispatch split (code-writer Steps 1–6, then doc-writer Step 7 only after 1–6 complete with no stop; compile-check runs Commands 1–3) and `## Runtime claims` a–i are the build.
- Sites: `Tests~/Fixtures/audio/manifest.json`, `Tests~/Fixtures/audio/tts/ladder_*.wav`, `NativeBridge~/harness/expectations.json`, `Tests~/Runtime/VoxrWavReplayTests.cs` (`ManifestFileNames`), `.claude/bindings/unity.md` Command 3, `Planning~/verification-recipe.md` step 2b.
- Bindings: `.claude/bindings/unity.md` (`verification`), `.claude/bindings/core.md` (`process-docs`, `review`, `session-launch`), `.claude/bindings/vc-git.md` (`vc`).

## Constraints & decisions

- **F1:** pinned values are written only by `--write-baseline`. The 21 bare `{file}` entries are not pins.
- **Stop rules (plan):**
  - A band-check rung out of band at either threshold: stop and ask the human.
  - `--write-baseline` moving any of the 16 existing pins: restore the file and stop for the human. It is never a re-pin.
  - On either stop, leave the tree uncommitted and send BLOCKED to the hub.
- **Band-check headroom** is only 0.05 s at P 0.2 and P 0.3, because piper's edge silence adds to the gap. A Step 3 stop is plausible. Never trim the audio or regenerate it on your own judgment.
- **WND-2 (STATUS `## Deferred`):** with `core.autocrlf=true`, `generate.sh` is checked out CRLF. Runtime claim a is the first real Git Bash run of it, so a failure there is that issue, not a plan defect.
- **Line endings:** the manifest and `expectations.json` are CRLF throughout. Keep them so.
- The design is locked. If the build contradicts it, the doc is reopened on a `design-` branch, never patched.
- Windows-native only, never WSL. Agents never run git writes. Compile and test output goes to `compile-check`.
- File every background agent's report by hand beside its brief in `.scratch/`; briefs tell agents to return reports as text.
- Every brief names the project root `D:\Workspace\VoXR-Speech-Recognition` and says to write by it if `pwd -W` disagrees.
- A subagent's Unity run leaves `memory/**/*.meta`: commit them, never delete.
- STATUS lines stay at or under 200 characters. The architecture doc is at about 33.9 KB of its 40 KB budget.

## Next steps

1. Invoke `implement` for `feat-endpointer-profile` Phase 1. The plan is persisted and validated; the validator report is `.scratch/plan-validator-endpointer-profile-phase-1-report.md`.

## Open questions

- None before the build. The band check or the pin check may raise one for the human.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-endpointer-profile; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor a482f5ee-89c4-4f22-8924-6fd3c7a16f33". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
