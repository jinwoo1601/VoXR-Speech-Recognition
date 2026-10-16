You're picking up: build Phase 4 (overlay core and `confgen`) of `feat-endpointer-profile` through `implement`. Read the files below, then start at Next steps.

## Objective

Run `implement` for Phase 4 of `endpointer-profile` on its persisted, validated plan `Planning~/features/endpointer-profile/plan-4.md`: `code-writer` builds Steps 1-3, `compile-check` runs the Step 4 gate (checks 1-5), and the build is checked in. The task is done when the gate is green on its evidence and the phase is checked in.

## Lane

feature

## Context

- Branch `feat-endpointer-profile`, the one checkout (`trees` is none), head `b5acdab`, pushed. STATUS `## Current` says Phase 4 planned and next `implement` Phase 4. The session notes are `memory/2026-10-16.md` (its last entry is "Phase 4 pickup (planned)") and `memory/2026-10-17.md` (this handoff).
- Requirements: `Planning~/features/endpointer-profile/requirements.md` (F3, F7, F8, F9, §6, §10 items 3-4). Architecture: `Planning~/features/endpointer-profile/architecture.md` (`## Decision register` "Pristine source", "Rewrite rules", "Atomic writes", "One Unity-free source", "`confgen`"; `## Build plan` Phase 4).
- Phase 4 is the first phase that touches `Runtime/` and the Unity tests: Commands 1-2 (batchmode through the host, Editor closed) and Command 3 (`NativeBridge~/` changes) run at its gate.

## Current state

- plan-4.md is persisted (10,222 B, LF), validated `full` (`.scratch/plan-validator-endpointer-profile-phase-4-report.md`, with the PM's and the human's rulings at its end), fixed in two passes, and checked in at `b5acdab`. Nothing of Phase 4's code exists yet.
- Pickup trail in `.scratch/`: `code-mapper-`, `doc-extractor-`, `architect-` (brief, fix, fix2) and `plan-validator-endpointer-profile-phase-4-*`; the approved plan is `.scratch/endpointer-profile-phase-4-approved-plan.md`.
- Baselines: EditMode 203 and PlayMode 704, all passed (`.scratch/test-results/editmode.xml`, `playmode.xml`, 2026-10-16).

## Key files & locations

- The plan: `Planning~/features/endpointer-profile/plan-4.md` — files table, Steps 1-4, stop rules, `## Runtime claims` a-k (these go to implement's first gate).
- New: `Runtime/EndpointerOverlay.cs` and its `.meta`, `Tests~/Editor/EndpointerOverlayTests.cs`, `NativeBridge~/harness/confgen/confgen.csproj` and `Program.cs`, and probes under `.scratch/endpointer-profile-phase-4/`. Modified: `Tests~/Runtime/ModelExtractorCacheTests.cs`.
- Verification: `.claude/bindings/unity.md` `## verification` (Commands 1-3, *Failure rule*); procedure and toolchain in `Planning~/verification-recipe.md`.
- Vendor model (ignored): `NativeBridge~/vendor/vosk-model-small-en-us-0.15/`; pin `NativeBridge~/harness/expectations.json` (`modelConf` :368).

## Constraints & decisions

- Human-approved boundary reading B1-B4: the enum `VoxrEndpointerProfile`, `Resolve`, the log text, tuned rows and `confgen --profile/--mode` are Phase 6, never built here.
- Human ruling 2026-10-16: no Command 4 run in Phase 4. The host compiles Runtime at apiCompatibilityLevel 6 (.NET Standard 2.1), so Commands 1-2 carry the surface.
- `.gitignore:15` ignores `*.csproj`: the PM force-adds `confgen.csproj` (`git add -f`) at check-in. Agents never touch VC state; the vc-guard hook refuses it.
- Build facts: agents' Bash tool collapses `\\` to `\`, so probes stay backslash-free, with Python in `.py` files run by `python3 -I`. `compile-check` has no Write tool, so probe files are written by `code-writer`.
- Writer choices left open by plan-3, held for G2: n = 0 S line, per-side finals mean, S/KC2 on both-side-scored cases, the raw-JSON differs stop (see `memory/2026-10-16.md`).
- Owed to the as-built reconcile at the build plan's end:
  - the B1-B4 departure from the *One Unity-free source* and *`confgen`* entries;
  - stale cites: `stage.sh` closure :43-54, :56-78; `main.cpp` args :174-182, conf read :186-194, pin :223-232, header :280-295; and those listed in `memory/2026-10-16.md`;
  - `joined-rig.md`'s rule 2 wording.
- Compile and test output goes to `compile-check`. File every agent's report by hand beside its brief in `.scratch/`. Windows-native in Git Bash only, never WSL. A contradiction with the locked design reopens it on a `design-` branch; it is never patched.

## Next steps

1. Invoke `implement` for `feat-endpointer-profile` Phase 4, plan `Planning~/features/endpointer-profile/plan-4.md`.

## Open questions

- None known.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-endpointer-profile; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 92457e2e-7fd1-42bc-b02e-53bf0d0850a0". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
