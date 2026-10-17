You're picking up: plan Phase 6 (presets, choice, resolution; default unchanged) of `feat-endpointer-profile` through `phase-pickup`. Read the files below, then start at Next steps.

## Objective

Run `phase-pickup` for Phase 6 of `endpointer-profile`. Done when `Planning~/features/endpointer-profile/plan-6.md` is persisted, validated by `plan-validator`, linked from the architecture doc's `## Build plan`, and committed. Stop there; `implement` is the next session's.

## Lane

feature

## Context

- Branch `feat-endpointer-profile`, the one checkout (`trees` is none). Head `0398c62`, pushed.
- STATUS `## Current`: Phase 5 is done and the next step is `phase-pickup` Phase 6. Session notes: `memory/2026-10-17.md`; its last entries are "Phase 5b implement", "tuning ruling 2" and this handoff.
- Requirements: `Planning~/features/endpointer-profile/requirements.md` (F5, F6, F7, F8, F9, F12, §6).
- Architecture doc: `Planning~/features/endpointer-profile/architecture.md`. The parts that matter:
  - `## Build plan` Phase 6 row (about :162): the enum and tuned rows; the field; the hint and hand-off; `Resolve` (`Automatic` → "Model default" in **both** modes; the flip is Phase 7); the seam; the log; `stage.sh`'s closure. Its green: Commands 1-2, Command 4, DocCheck, and recogniser stages of the worktree and a pre-feature ref.
  - Decision register: *Presets* (about :55), *Mode at load* (about :59), *The rig* (about :69).
  - *Tuning record* (about :122-129): **one tuned preset, `Responsive`, rule2 `0.45`, rule3 and rule4 the archive's own** (tuning ruling 2, 2026-10-17). That is the only tuned row Phase 6 adds (enum value `2`).
- Design: `Planning~/design-docs/endpointer-profile.md` (locked), with adr-0001 to adr-0004 in `Planning~/design-docs/endpointer-profile/decisions/`.

## Current state

- Phases 1-5 are built and committed:
  - Phase 4 delivered `Runtime/EndpointerOverlay.cs` (`Row`, `ModelDefault`, `Rewrite`, `Apply`) and `confgen`.
  - Phase 5 delivered the sweeps: rule2 0.35/0.25, then plan-5b's 0.45/0.4. Both gates passed, and both records are filled.
- Nothing of Phase 6 has been planned or written.

## Key files & locations

- Plans for shape and precedent: `Planning~/features/endpointer-profile/plan-4.md` (code phase, Commands 1-3 gate), `plan-5.md`, `plan-5b.md`.
- Phase 5 evidence (ignored, read only): `.scratch/endpointer-profile-phase-5/`, `.scratch/endpointer-profile-phase-5b/`.
- Code the row names: `Runtime/VoxrSpeechRecogniser.cs` (`InitialiseAsync`), the listening controller (its `_listeningMode` :27 per the register), `Runtime/EndpointerOverlay.cs`, `Planning~/features/coverage-in-selection/ab-rig/stage.sh` (closure :39-58), `NativeBridge~/harness/confgen/Program.cs`.
- Bindings:
  - `.claude/bindings/unity.md` `## verification` (Commands 1-4);
  - `Planning~/verification-recipe.md` (DocCheck :222-232);
  - `.claude/bindings/core.md` `## process-docs` (plan 10 KB, brief 15 KB).

## Constraints & decisions

- **Default unchanged in Phase 6.** `Automatic` resolves to "Model default" in both modes. The Continuous flip to `Responsive` and the re-pin are Phase 7, and the standing pin `NativeBridge~/harness/expectations.json` is not re-pinned in Phase 6.
- Enum values: `Automatic = 0`, `ModelDefault = 1`, `Responsive = 2`. They are appended and never renumbered. Row values are decimal text (`"0.45"`), null for "the archive's own", and there are no public floats (F5).
- **Writer choices held for G2** are not reopened. They are in `memory/2026-10-16.md` (plan-3) and `memory/2026-10-17.md` (plan-4).
- **Owed to the as-built reconcile at the build plan's end** (not Phase 6's): B1-B4; the stale cites in the Phase 4/5 pickup notes; `Planning~/design-docs/joined-rig.md` rule 2 wording.
- **Build facts:**
  - The agents' Bash tool collapses `\\` to `\`, so probes must be backslash-free, with Python in `.py` files run under `python3 -I`.
  - `compile-check` has no Write tool.
  - The vc-guard hook refuses VC-state changes to agents.
  - Everything runs Windows-native in Git Bash, never WSL.
  - Code-writer briefs must forbid adding pipes, redirects or capture files to plan commands. Phase 5's writer masked a harness exit with `| grep`.
- **Process:**
  - Compile, test and rig output go to agents, never to the main thread.
  - File every agent's report by hand beside its brief in `.scratch/`.
  - A contradiction with the locked design reopens it on a `design-` branch; it is never patched.
- **Background session:** one named `feat-joined-rig-parser` may be listed at this project root, in state `blocked`. It is not this branch's. If it writes in this tree, stop and tell the human (one writer per tree).

## Next steps

1. Invoke `phase-pickup` for `feat-endpointer-profile` Phase 6.

## Open questions

- None known.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-endpointer-profile; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 449df365-b981-45df-8a10-1738cde040d9". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
