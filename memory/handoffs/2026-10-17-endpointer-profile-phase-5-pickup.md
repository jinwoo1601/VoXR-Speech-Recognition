You're picking up: plan Phase 5 (sweep and tuning pass) of `feat-endpointer-profile` through `phase-pickup`. Read the files below, then start at Next steps.

## Objective

Run `phase-pickup` for Phase 5 of `endpointer-profile`: recon, a plan the human approves, persisted as `Planning~/features/endpointer-profile/plan-5.md` with its link line in the architecture doc, validated by `plan-validator`, and checked in. The task is done when plan-5 is persisted, validated and committed; building it is the next session's `implement`.

## Lane

feature

## Context

- Branch `feat-endpointer-profile`, the one checkout (`trees` is none), head `992246f` (Phase 4 built), pushed. STATUS `## Current` says Phase 4 built and next `phase-pickup` Phase 5. Session notes: `memory/2026-10-16.md`, `memory/2026-10-17.md`.
- Requirements: `Planning~/features/endpointer-profile/requirements.md` (F4, §4.6, §10 item 1). Architecture: `Planning~/features/endpointer-profile/architecture.md` — `## Build plan` Phase 5 (:146), `### Sweep record (F4)` (:107-120, the eight runs: rule2 0.35 and 0.25 x buffer time-driven/eager x W 0.5/2.0), `### Tuning record` (:122-129, eligibility rule), `## Decision register` *Kill criteria in the report* (:73), *Buffer setups*, *Presets*. Design: `Planning~/design-docs/endpointer-profile.md` (locked; adr-0002 (A), adr-0003, adr-0004 beside it).
- Phase 5 is "No code": dumps A (vendor model, standing pin) and B per candidate, the eight recogniser-layer runs, the *Sweep record* filled, any rule3/rule4 sweep it justifies, and the human's ruling into the *Tuning record*. A KC2 STOP stops the work; no eligible candidate blocks the flip pending the human (adr-0002 (A)).

## Current state

- Phases 1-4 built and checked in. Phase 4 gate (2026-10-17): EditMode 218/218, PlayMode 706/706 (the new baselines, `.scratch/test-results/`), Command 3 37/37.
- Tools Phase 5 consumes, all built:
  - `confgen` (Phase 4): `dotnet NativeBridge~/build-confgen/bin/confgen/debug/confgen.dll --model <dir> --out <dir> --rule2 <v> [--rule3 <v>] [--rule4 <v>]`; prints `sha256 <hex>` + the three rule lines; exit 1 with `confgen: not applied: ...` on a reason. Source `NativeBridge~/harness/confgen/Program.cs`.
  - Harness `--dump`, `--write-baseline` and the `modelConf` header/pin key (Phase 2); a pin copy is needed for a non-vendor model (the standing pin exits 2 on another conf).
  - The rig's `joined.sh`/`stage.sh` `--buffer time|eager --window <s>` and the `## Sweep measures` section (Phase 3), `Planning~/features/coverage-in-selection/ab-rig/`.
- Leftovers from Phase 4's gate, ignored by git: `NativeBridge~/vendor/overlaid/rule2-0.35/` (a confgen output) and `.scratch/endpointer-profile-phase-4/` (`c1.out`, `pin.json`, `b.jsonl`). Phase 2's and 3's green dumps and probes under `.scratch/endpointer-profile-phase-{2,3}/` are not to be edited.
- Nothing of Phase 5 exists yet.

## Key files & locations

- Prior plans as models: `Planning~/features/endpointer-profile/plan-2.md` (dump/pin-copy sequence), `plan-3.md` (rig sweep machinery, R1-R4), `plan-4.md` (confgen).
- Rig: `Planning~/features/coverage-in-selection/ab-rig/{joined.sh,stage.sh,JoinedReport.cs}`; its doc `joined-rig.md` there.
- Harness: `NativeBridge~/harness/main.cpp`, `expectations.json` (standing pin, `modelConf` :368), `README.md`.
- Bindings: `.claude/bindings/unity.md` `## verification`; procedure `Planning~/verification-recipe.md`; process-docs `.claude/bindings/core.md` (plan budget 10 KB, brief budget 15 KB).

## Constraints & decisions

- Writer choices held for G2 (do not reopen): plan-3's (n = 0 S line, per-side finals mean, S/KC2 on both-side-scored cases, raw-JSON differs stop) in `memory/2026-10-16.md`; plan-4's nine in `memory/2026-10-17.md` "Phase 4 implement (built)".
- Owed to the as-built reconcile at the build plan's end (not Phase 5's work): the B1-B4 departure from *One Unity-free source* and *`confgen`*; stale cites (`stage.sh` closure :43-54, :56-78; `main.cpp` args :174-182, conf read :186-194, pin :223-232, header :280-295; those in `memory/2026-10-16.md`); `joined-rig.md`'s rule 2 wording.
- The tuning ruling is the human's: the pass proposes, the human rules (Tuning record :129). rule3/rule4 swept only on a measured reason; rule1/rule5 never.
- Build facts: agents' Bash tool collapses `\\` to `\` — probes backslash-free, Python in `.py` files under `python3 -I`; `compile-check` has no Write tool, so probe files are `code-writer`'s; agents never touch VC state (vc-guard hook). Windows-native in Git Bash only, never WSL. `joined.sh` handles `MSYS_NO_PATHCONV` itself.
- Compile and test output goes to `compile-check`. File every agent's report by hand beside its brief in `.scratch/`. A contradiction with the locked design reopens it on a `design-` branch; never patched.
- A background session named `feat-joined-rig-parser` is listed at this project root in state `blocked` (`claude agents --json`); it is not this branch's. If it writes in this tree, stop and tell the human (one writer per tree).

## Next steps

1. Invoke `phase-pickup` for `feat-endpointer-profile` Phase 5.

## Open questions

- None known. Whether Phase 5's runs go to `code-writer` + `compile-check` or are run by the human (no code) is for the pickup's plan to settle with the human.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-endpointer-profile; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor cfd99b82-9616-438a-8c2b-d05e3b5b754e". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
