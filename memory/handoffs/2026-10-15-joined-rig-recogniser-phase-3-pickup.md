You're picking up: plan Phase 3 ("recogniser-layer scoring and report") of `feat-joined-rig-recogniser`. Read the files below, then start at Next steps.

## Objective

Run `phase-pickup` for Phase 3 of `joined-rig-recogniser`: recon, plan, persist `Planning~/features/joined-rig-recogniser/plan-3.md`, validate it (`plan-validator`), fix, link it from the architecture's `## Build plan`, commit. Done when plan-3 is persisted, validated and committed with STATUS advanced to `implement` Phase 3. Nothing past the pickup.

## Lane

feature

## Context

- Branch `feat-joined-rig-recogniser` in the one checkout `D:\Workspace\VoXR-Speech-Recognition` (`trees` none), head `594f783` plus this handoff's commit, pushed. Design `joined-rig` G1-locked; track Full.
- Phase 1 (the staged world) committed `ef1ed67`; Phase 2 (bridge timing, dump schema 2) built, gated (criteria 2-11 green, claims a-l true) and committed `594f783` on 2026-10-15.
- STATUS `memory/STATUS.md` `## Current` line 8: next `phase-pickup` Phase 3. Session note `memory/2026-10-15.md`, entry "Phase 2 implement".

## Current state

- Phase 3 not started: no recon, no plan.
- Phase 3 scope (architecture `## Build plan`, Phase 3 bullet): `--layer` in `Joined.cs` and `joined.sh`, shared classifier, fires, pending label, timings, configuration, guards, trace, report, schema-1 refusal. Green: DocCheck; F3 `main`-vs-`main` C# side, 0 flips; F2 two dumps of one build, 0 flips; F4 a case's trace; F5 the split case in a class; F6 the staged-copy canary in its class, report kept in `.scratch/joined/`; F7, F8 read on the 16 fixtures; F9 the kill canary (exit 3) and sealed/`--evaluate` on a `.scratch/` manifest copy; a schema-1 dump refused at this layer, read at the parser layer. Discharges F2-F9, §6 input boundary.
- Phase 2 gave Phase 3 its inputs: `DumpCase.Results` (`DumpResult`: `Seq`, `Final`, `Consumed`, `Origin`, payload raw text), `Dump.Schema`, `Dump.DecoderRate` in AB `JoinedInput.cs`; schema-2 dumps `.scratch/joined/jrr-p2-a.jsonl`, `jrr-p2-b.jsonl` (same build, `timing` exact, difference 0); schema-1 dump `.scratch/joined/p4.jsonl`.

## Key files & locations

- Architecture: `Planning~/features/joined-rig-recogniser/architecture.md` — `## Decision register` (*Staging* with its 2026-10-15 amendment, *Schema 2*, *Checks as commands*, the scoring/report entries); `## Build plan`; `## As-built deltas` Phase 1 and Phase 2 entries (Phase 2's lists the landed citations; some plan-time `main.cpp` line cites in `Components` and the register are stale — re-verify any you use).
- Requirements: `Planning~/features/joined-rig-recogniser/requirements.md` (F2-F9, §6).
- Plan form: `Planning~/features/joined-rig-recogniser/plan-2.md` (budget 10 KB; doctor warns over it).
- Rig: `Planning~/features/coverage-in-selection/ab-rig/` (`Joined.cs`, `JoinedInput.cs`, `JoinedRecogniser.cs`, `JoinedScorer.cs`, `JoinedReport.cs`, `BridgeNative.cs`, `stage.sh`, `joined.sh`).
- Parser-layer precedent for scoring and report: `Planning~/features/joined-rig-parser/` (architecture *Tuning record*, `plan-3.md`, `plan-4.md`).
- Verification: `.claude/bindings/unity.md` `## verification`; `Planning~/verification-recipe.md` (DocCheck). Phase 3 is rig-only: no `NativeBridge~/` or `Runtime/` change expected, so Commands 1-3 should not trigger — confirm in recon.
- Phase 2 reports, if a fact is needed: `.scratch/implement-joined-rig-recogniser-phase-2-report.md` (indexes the rest).

## Constraints & decisions

- *Staging* trap: under the rig `Main` runs on managed thread 2; no world drives through `InjectResult`, `InjectPartialResult`, `InjectText`, `FlushPendingBuffer` without a ruling.
- No edit to shipped `Runtime/` source (§2 Out 1, §8); canaries edit only `.scratch/` staged copies. `expectations.json` and committed dumps are never edited.
- Horizon K + 3 frames (ruled 2026-10-15). Plan runtime claims are checked at implement's first gate; a false one is a stop for the human.
- Canary scripts on Windows must write LF (`newline=''`): the rig refuses CR ("CR in the line") — Phase 2's gate hit it.
- Agents never run git writes; commits are the PM's per the `vc` binding. The report filer misses many reports: check `.scratch/` after each dispatch and file by hand from the hand-back. Every brief names the project root. Compiler/test output goes to `compile-check`, never the main thread.
- No CHANGELOG line per phase: it comes with the post-G2 product docs (Phase 2's suggested line is in its implement report).
- Windows-native only (Git Bash, .NET SDK 10); never WSL. `stage.sh`, `joined.sh` under `env -u MSYS_NO_PATHCONV`.

## Next steps

1. Invoke `phase-pickup` for feature `joined-rig-recogniser`, Phase 3 (architecture `## Build plan` Phase 3 bullet; plan path `Planning~/features/joined-rig-recogniser/plan-3.md`).

## Open questions

- None known. The Quest on-device check stays the human's, at G2. JRP-1's STATUS deferral closes at this feature's G2, not before.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-joined-rig-recogniser; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor e8647675-1727-4d8b-a3d1-ef06e64c1304". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
