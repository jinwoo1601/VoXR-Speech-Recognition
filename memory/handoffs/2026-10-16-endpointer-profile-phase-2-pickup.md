You're picking up: plan Phase 2 (conf record) of `feat-endpointer-profile` through `phase-pickup`. Read the files below, then start at Next steps.

## Objective

Run `phase-pickup` for Phase 2 of `endpointer-profile`: recon, plan, persist `Planning~/features/endpointer-profile/plan-2.md`, validate it with `plan-validator`. Done when the plan is persisted, validated and approved by the human, and checked in. Building it is not part of this task.

## Lane

feature

## Context

- Branch `feat-endpointer-profile`, the one checkout (`trees` is none), head `2aed6fa`, pushed. STATUS `## Current`: Phase 1 built; next `phase-pickup` Phase 2. Session note: `memory/2026-10-16.md` (last entries "Phase 1 implement (built)" and this handoff).
- Requirements `Planning~/features/endpointer-profile/requirements.md` (F2; F3's record half); architecture `Planning~/features/endpointer-profile/architecture.md` (`## Build plan` "Phase 2 — Conf record"; `## Decision register` "Conf record" and "the rig"); design (locked) `Planning~/design-docs/endpointer-profile.md`, ADRs `Planning~/design-docs/endpointer-profile/decisions/`.
- Phase 2 row, as written: header `modelConf`; the pin key, check and write, `--write-baseline` adding it (diff that key alone; a moved final stops); the rig's pin key, kill check and conf line; README guards (`doc-writer`). Green: Command 3; a `--dump` header with `modelConf`; a `.scratch/` model copy with an edited rule2 line exits 2 against the standing pin, naming the conf, and, dumped against a `.scratch/` pin copy, shows a different conf line in a decoder-shape report; DocCheck.

## Current state

- Phase 1 done at `2aed6fa`: pause ladder of 21 cases in `Tests~/Fixtures/audio/manifest.json`, 21 `Tests~/Fixtures/audio/tts/ladder_*.wav`, `NativeBridge~/harness/expectations.json` harness-pinned (37 cases), `Tests~/Fixtures/generate.py` ladder edge trim (human ruling 2026-10-16, register "Pause ladder" amended), `ManifestFileNames` skips `ladder`, Command 3 green count "37/37" in `.claude/bindings/unity.md` :8 and `Planning~/verification-recipe.md` :21.
- Gate evidence (Phase 1): EditMode 203/203, PlayMode 704/704, Command 3 harness 37/37. At the model's own conf, P1 0.6, P3 0.6 and M1-M3 0.9 pin as two finals.
- Phase 1's trail is in `.scratch/` (`implement-endpointer-profile-phase-1-*`, `architect-…-fix2/fix3-*`, `plan-validator-…-fix2-*`, `compile-check-…-gate-*`). It is not needed for Phase 2.
- The architecture doc's as-built reconcile is not done per phase. This project reconciles at the build plan's end, before `review-cycle`.

## Key files & locations

- Persisted plans: `Planning~/features/<feature>/plan-<id>.md`, a 10 KB budget each (`.claude/bindings/core.md` `## process-docs`). The model plan is `Planning~/features/endpointer-profile/plan-1.md`.
- Harness: `NativeBridge~/harness/main.cpp` (pin read and write around :214, :224-243, :352-367), `NativeBridge~/harness/README.md`, `NativeBridge~/harness/expectations.json`.
- Joined rig: `Planning~/features/coverage-in-selection/ab-rig/` (`JoinedInput.cs` `ManifestCaseKeys` :132-149, `ReadPin`). DocCheck procedure: `Planning~/verification-recipe.md` `## Parser A/B rig — DocCheck` and memory `doccheck-run-procedure`.
- Bindings: `.claude/bindings/unity.md` (`verification`), `.claude/bindings/core.md` (`process-docs`, `review`, `session-launch`), `.claude/bindings/vc-git.md` (`vc`).

## Constraints & decisions

- F1 holds for every pin change: pinned values are written only by `--write-baseline`, never by hand. A moved `expectedFinals` stops for the human and is never a re-pin.
- The design is locked. A contradiction reopens it on a `design-` branch and is never patched.
- Windows-native only, never WSL. Agents never run git writes. Compile and test output goes to `compile-check`.
- File every agent's report by hand beside its brief in `.scratch/`; briefs tell agents to return reports as text and name the project root `D:\Workspace\VoXR-Speech-Recognition`.
- `expectations.json` and `manifest.json` are CRLF throughout and stay so.
- A Unity run leaves `memory/**/*.meta`: commit them, never delete.
- STATUS lines stay at or under 200 characters. The architecture doc is at 34,603 B of its 40 KB budget.

## Next steps

1. Invoke `phase-pickup` for `feat-endpointer-profile` Phase 2.

## Open questions

- None known. The plan approval is the human's.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-endpointer-profile; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor b7bdfc3b-3f5e-4152-9950-f783105d3087". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
