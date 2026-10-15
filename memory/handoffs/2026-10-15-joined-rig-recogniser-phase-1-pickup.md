You're picking up: plan Phase 1 ("the staged world") of `feat-joined-rig-recogniser` through `phase-pickup`, from PLAN on. Read the files below, then start at Next steps.

## Objective

Finish `phase-pickup` for Phase 1: PLAN (from the recon already done), PERSIST `Planning~/features/joined-rig-recogniser/plan-1.md` with its link line, VALIDATE, RECONCILE, RECORD & COMMIT. Done when plan-1.md is validated, linked from the architecture doc's `## Build plan`, and committed with STATUS advanced. Nothing is implemented.

## Lane

feature

## Context

- Branch `feat-joined-rig-recogniser` (checked out in the one checkout, `D:\Workspace\VoXR-Speech-Recognition`; `trees` none), head `bfd065d`, pushed. Design `joined-rig` G1-locked; track Full.
- Today (2026-10-15): G0 agreed (`e81ff9a`); requirements §1-§10 settled ("All recommended", forks A-G; `5527c53`); architecture authored, PLAN ruled ("All recommended": F1 check against pushed samples, frame-grid delivery, drain only under `--dump`, four phases approved as ordered; `bfd065d`).
- STATUS `memory/STATUS.md` `## Current` line 8: stage architecture done, next `phase-pickup` Phase 1. Session note `memory/2026-10-15.md` (entries "feat-joined-rig-recogniser — G0 / requirements / architecture").

## Current state

- `phase-pickup` ORIENT and RECON done. Both recon reports are filed, condensed and verified-in-part, in `D:\Workspace\VoXR-Speech-Recognition\.scratch\joined-rig-recogniser-phase-1-recon.md` (trust it; do not re-run recon). Briefs that produced it: `.scratch/code-mapper-joined-rig-recogniser-phase-1-brief.md`, `.scratch/doc-extractor-joined-rig-recogniser-phase-1-brief.md`.
- No plan file exists yet; no code changed.

## Key files & locations

- Requirements: `Planning~/features/joined-rig-recogniser/requirements.md` (F3, F4, §8, §10 item 1).
- Architecture: `Planning~/features/joined-rig-recogniser/architecture.md` — `## Build plan` Phase 1 row; register *Staging*, *Stage layering*, *The player loop*, *Delivery*, *The clock*, *Guards at this layer* (error-log part), *Trace*; Components list; Risks "Unrecorded behaviour … Phase 1".
- Rig: `Planning~/features/coverage-in-selection/ab-rig/` (`stage.sh`, `UnityStub.cs`, `Rig.csproj`, `Program.cs`, `Joined.cs`, `joined.sh`).
- Plan form precedent: `Planning~/features/joined-rig-parser/plan-1.md` … `plan-4.md`; plan budget 10 KB; link-line form `- Phase N — persisted plan (date): [plan-N.md](plan-N.md)`.

## Constraints & decisions

- The PM's PLAN read so far (confirm, then state it in one message — no plan mode needed unless you find a departure): Phase 1's files stay within the row — `UnityStub.cs` (superset), new `BridgeNative.cs` and `ModelExtractor.cs` stand-ins and `JoinedRecogniser.cs`, `stage.sh` (`recogniser` argument), `Joined.cs`/`Program.cs` (the `drive-check` verb and the module-initializer hook only; `--layer`, `timing`, schema-1 refusal stay with Phases 2-3), `Rig.csproj` entry-points comment. Unsplit (tightly coupled). Runtime claims to list: the stand-in `ModelExtractor` returns a completed task so the whole drive stays on thread 1 (the shipped one never completes synchronously — `ModelExtractor.cs` :88 — and would trip `Debug.Assert(ManagedThreadId == 1)`); the shipped sources compile unedited against the stub (§8); the stand-in `BridgeNative` needs no `unsafe` (token → managed bytes). Parser-layer `main`-vs-`main` 0-flip check uses an existing local schema-1 dump (e.g. `.scratch/joined/p1.jsonl`, gitignored) since Phase 1 triggers no binding command — the plan says so and names a re-make with the already-built harness if absent.
- Recon facts the plan must carry: `VoxrCommandRecogniser` has no `OnDestroy` (teardown is `OnDisable`); `VoxrSpeechRecogniser` has no `Awake`/`OnEnable`; `#if DEBUG` in `VoxrCommand.cs` :130-149 is live in a Debug build; `Debug.LogError(object, Object)` overload needed; `VoxrMatchDiagnostics.cs` compiles empty.
- No edit to any shipped `Runtime/` source (requirements §2 Out 1); a canary edits only a `.scratch/` staged copy (the auto-mode classifier refused worktree canaries in the parser feature).
- Agents never run git writes; commits are the PM's per the `vc` binding. Subagents return reports as text; the PM files them under `.scratch/`. Every brief names the project root and says to write by it if `pwd -W` differs.
- Windows-native only (Git Bash, .NET SDK 10); never WSL.

## Next steps

1. Invoke `phase-pickup` for feature `joined-rig-recogniser`, Phase 1; it resumes at PLAN with RECON done (recon record above).

## Open questions

- None open; Phase 2 (later) needs the host Editor closed for Commands 1-2.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-joined-rig-recogniser; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 86dbc095-ed1c-4192-b98a-d26977ab30c7". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
