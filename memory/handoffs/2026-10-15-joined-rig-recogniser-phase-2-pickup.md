You're picking up: plan Phase 2 ("bridge timing and dump schema 2") of `feat-joined-rig-recogniser`. Read the files below, then start at Next steps.

## Objective

Run `phase-pickup` for Phase 2 of `joined-rig-recogniser`: recon, plan, persist `Planning~/features/joined-rig-recogniser/plan-2.md`, validate it (`plan-validator`), fix, link it from the architecture's `## Build plan`, commit. Done when plan-2 is persisted, validated and committed with STATUS advanced to `implement` Phase 2. Nothing past the pickup.

## Lane

feature

## Context

- Branch `feat-joined-rig-recogniser` in the one checkout `D:\Workspace\VoXR-Speech-Recognition` (`trees` none), head `ef1ed67` plus this handoff's commit, pushed. Design `joined-rig` G1-locked; track Full.
- Phase 1 (the staged world, rig only) built, gated (C2-C8 green) and committed `ef1ed67` on 2026-10-15.
- STATUS `memory/STATUS.md` `## Current` line 8: next `phase-pickup` Phase 2. Session note `memory/2026-10-15.md`, entries "Phase 1 implement" and "handoff".

## Current state

- Phase 2 not started: no recon, no plan.
- Phase 2 scope (architecture `## Build plan`, Phase 2 bullet): bridge counters, `QueuedResult`, two additive exports; the harness's dump schema 2 and drain under `--dump`; `JoinedInput.cs` reading both schemas; `timing`; the `.so` rebuilt and committed. Green: Command 3 verbatim (16/16, exit 0); Commands 1-2 (the committed `.so` is under `Runtime/`); two `--dump` re-runs with stdout identical to Command 3's and `timing` passing F1; Phase 1's parser-layer `main`-vs-`main` on a schema-2 dump, 0 flips, Phase 1's counts. Discharges F1, F11 (schema), §6 degraded dependencies, §10 item 2.

## Key files & locations

- Architecture: `Planning~/features/joined-rig-recogniser/architecture.md` — register *Bridge timing*, *ABI route*, *Deterministic dump stream*, *Staging* (with its 2026-10-15 amendment); `## Build plan`; `## Risks` (Phase 2 unrecorded behaviour); `## As-built deltas` Phase 1 entry.
- Requirements: `Planning~/features/joined-rig-recogniser/requirements.md` (F1, F2, F11, §6, §10 item 2).
- Phase 1 plan, for the plan-file form: `Planning~/features/joined-rig-recogniser/plan-1.md` (budget 10 KB).
- Rig: `Planning~/features/coverage-in-selection/ab-rig/` (`JoinedInput.cs`, `Joined.cs`, `JoinedRecogniser.cs`, `BridgeNative.cs` stand-in, `stage.sh`, `joined.sh`).
- Bridge and harness: `NativeBridge~/` (`src/`, `harness/main.cpp`, `harness/README.md`, `harness/expectations.json` — the pin, never hand-edited).
- Verification: `.claude/bindings/unity.md` `## verification` (Commands 1-3 apply this phase); `Planning~/verification-recipe.md` (Building NativeBridge; Parser A/B rig — DocCheck).
- Phase 1 reports, if a fact is needed: `.scratch/*joined-rig-recogniser-phase-1-*report.md`.

## Constraints & decisions

- Trap (register *Staging*, amended 2026-10-15): under the rig `Main` runs on managed thread 2, so the shipped `ManagedThreadId == 1` asserts (`VoxrSpeechRecogniser.AssertMainThread` via `InjectResult`/`InjectPartialResult`; `VoxrCommandRecogniser.InjectText`, `FlushPendingBuffer`) stop a world through `RigErrors`. No phase drives through those four members without a ruling.
- Phase 2 is the feature's only shipped-tree change; Commands 1-2 need the host Editor closed (`D:\Workspace\VoXR TestGround\Temp\UnityLockfile` absent); a Command 3 transcript mismatch stops for the human, never a re-pin.
- Horizon K + 3 frames (ruled 2026-10-15). Plan claims are checked at implement's first gate; a false one is a stop.
- Agents never run git writes; commits are the PM's per the `vc` binding. Subagents return reports as text; the PM files them under `.scratch/`. Every brief names the project root. Compiler/test output goes to `compile-check`, never the main thread.
- Windows-native only (Git Bash, .NET SDK 10, MSVC 2022); never WSL. `stage.sh` under `env -u MSYS_NO_PATHCONV`.

## Next steps

1. Invoke `phase-pickup` for feature `joined-rig-recogniser`, Phase 2 (architecture `## Build plan` Phase 2 bullet; plan path `Planning~/features/joined-rig-recogniser/plan-2.md`).

## Open questions

- Whether the host Editor is closed is the human's; only matters at Phase 2's implement, not its pickup.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-joined-rig-recogniser; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 4805aad2-20e8-440c-a8e9-c1cf1d747593". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
