You're picking up: implement Phase 6 (presets, choice, resolution; default unchanged) of `feat-endpointer-profile` through `implement`. Read the files below, then start at Next steps.

## Objective

Run `implement` for Phase 6 of `endpointer-profile` from the persisted, validated plan `Planning~/features/endpointer-profile/plan-6.md`. Done when its gate (Step 8, checks 1-6) is green on evidence, the build is committed, and the as-built pass is through, as `implement` says. The human said "Go for implement" on 2026-10-17.

## Lane

feature

## Context

- Branch `feat-endpointer-profile`, the one checkout (`trees` is none). Head `5355a19`, pushed.
- STATUS `## Current`: Phase 6 planned; next `implement` Phase 6. Session note `memory/2026-10-17.md`; its last entry is "Phase 6 pickup".
- Requirements: `Planning~/features/endpointer-profile/requirements.md` (F5, F6, F7, F8, F9, F12, §6).
- Architecture doc: `Planning~/features/endpointer-profile/architecture.md`. The register entries that matter are "Overlay seam", "Presets", "Choice surface", "Mode at load", "Load-time log", "`confgen`" and "The rig". The *Tuning record* holds one preset, `Responsive`: rule2 `0.45`, with rule3 and rule4 the archive's own.
- The plan was approved by the human in plan mode, validated at full depth, and fixed twice. It carries departures D1-D4, which the human approved: `confgen --profile/--mode` in Phase 6; Command 3 in the green; the hand-off at `OnApplicationPause` and in the `InitialMode` setter; `ApplyResult` carrying the effective values.

## Current state

- Phases 1-5 are built and committed. Nothing of Phase 6 code is written.
- Recon and validation records, which are ignored and only to be read, sit in `.scratch/`:
  - `code-mapper-endpointer-profile-phase-6-report.md`
  - `doc-extractor-endpointer-profile-phase-6-report.md`
  - `plan-validator-endpointer-profile-phase-6-report.md`
  - the `architect-endpointer-profile-phase-6{,-fix,-fix2}-*` files
- The test baseline is 218 EditMode and 706 PlayMode (`.scratch/test-results/`, 2026-10-16). The plan expects 230 and 715.

## Key files & locations

- Plan: `Planning~/features/endpointer-profile/plan-6.md` (11,261 B; the human ruled an 11 KB budget exception, recorded in `memory/doctor-rulings.md`).
- Precedent implement runs:
  - `.scratch/implement-endpointer-profile-phase-4-brief.md` and its `-report.md`
  - `.scratch/compile-check-endpointer-profile-phase-4-gate-brief.md`
- Code the plan touches:
  - `Runtime/EndpointerOverlay.cs`
  - `Runtime/VoxrSpeechRecogniser.cs`
  - `Runtime/VoxrPushToTalkController.cs`
  - the new `Runtime/VoxrEndpointerProfile.cs` and its hand-made `.meta`
  - `NativeBridge~/harness/confgen/{Program.cs,confgen.csproj}`
  - `Planning~/features/coverage-in-selection/ab-rig/stage.sh`
  - tests in `Tests~/Editor/EndpointerOverlayTests.cs`, `Tests~/Runtime/VoxrPushToTalkControllerTests.cs` and the new `Tests~/Runtime/VoxrEndpointerProfileLoadTests.cs`
- Bindings:
  - `.claude/bindings/unity.md` `## verification` (Commands 1-4)
  - `Planning~/verification-recipe.md` (Samples~ compile and its mutation check; DocCheck :222-232)
  - `.claude/bindings/core.md` `## process-docs` (brief 15 KB)

## Constraints & decisions

- **Default unchanged.** `Automatic` resolves to "Model default" in both modes. Do not re-pin `NativeBridge~/harness/expectations.json`; check 3 asserts it is unchanged. The flip is Phase 7.
- **Pre-feature staging ref:** `b255105`, the merge-base, verified. It has `VoxrListeningMode.cs` and lacks the overlay.
- **Runtime claims go to implement's first gate.** The main one is whether `LogAssert.NoUnexpectedReceived` (Ownership tests :102, :142, :158) tolerates the new info-level line. If it does not, stop for a ruling; never edit the test to pass.
- **Build facts:**
  - The agents' Bash tool collapses `\\` to `\`, so probes must be backslash-free, with Python in `.py` files run under `python3 -I`.
  - `compile-check` has no Write tool.
  - The vc-guard hook refuses VC-state changes to agents, so the mutation check restores files with `mutate.py`, not `git checkout`.
  - Everything runs Windows-native in Git Bash, never WSL. `stage.sh` runs under `env -u MSYS_NO_PATHCONV`.
  - Code-writer and compile-check briefs must forbid adding pipes, redirects or capture files to plan commands. Phase 5's writer masked a harness exit with `| grep`.
- **Writer choices held for G2** are not reopened. They are in `memory/2026-10-16.md` (plan-3) and `memory/2026-10-17.md` (plan-4).
- **Owed to the end-of-build reconcile, not Phase 6's:** B1-B4; the stale cites (stage.sh closure now :43-78, STAGED_FROM now :127-142, the "Overlay seam" register citing validation lines rather than returns); `joined-rig.md` rule 2 wording.
- **Process:**
  - Compile, test and rig output go to agents, never to the main thread.
  - The SubagentStop hook files reports beside their briefs. Where a report's first line does not name the brief, it lands in `.scratch/reports/`; move it beside its brief.
  - A contradiction with locked canon reopens it on a `design-` branch; it is never patched.
- **Background session:** one named `feat-joined-rig-parser` may be listed at this project root. It is not this branch's. If it writes in this tree, stop and tell the human (one writer per tree).

## Next steps

1. Invoke `implement` for `feat-endpointer-profile` Phase 6 (plan `Planning~/features/endpointer-profile/plan-6.md`).

## Open questions

- None known.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-endpointer-profile; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 5bd106d6-226d-4513-b56e-0e94899182e0". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
