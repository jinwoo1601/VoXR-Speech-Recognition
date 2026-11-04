You're picking up: plan Phase 8 (the human-only Quest 3 check) of `feat-endpointer-profile` through `phase-pickup`. Read the files below, then start at Next steps.

## Objective

Run `phase-pickup` for Phase 8 of `endpointer-profile`: plan it from recon, then persist and validate its plan as `Planning~/features/endpointer-profile/plan-8.md`. Done when the plan is persisted, validated, linked from the architecture doc's `## Build plan`, and committed, as `phase-pickup` says. Phase 8 is human-only, so its plan is a procedure for the human on a Quest 3 and the evidence the human reports back. It is not a code-writer build.

## Lane

feature

## Context

- Branch `feat-endpointer-profile`, the one checkout (`trees` is none). Head `100a987` (Phase 7 built) plus this handoff's commit, pushed.
- STATUS `## Current`: Phase 7 built; next `phase-pickup` Phase 8. Session note `memory/2026-10-17.md`; its last entries are "Phase 7 implement (built)" and this handoff.
- Requirements: `Planning~/features/endpointer-profile/requirements.md`, F11 and §9 (the Quest 3 check), plus §8's arm64 conf assumption.
- Architecture doc: `Planning~/features/endpointer-profile/architecture.md`:
  - :164, the Build plan row "Phase 8 — Human-only check on a Quest 3 (§9)". It covers the adb-pulled conf after a tuned-default load, then about ten commands under the tuned default, then under "Model default". A fail blocks the flip: Phase 7 is reverted and Phase 6's opt-in state stands (adr-0002 (A)). It discharges F11 and §8's arm64 conf assumption.
  - :165, the assets: a Quest 3 and the human.
  - :179, Risks: `File.Replace` on IL2CPP Android is first exercised in Phase 8.
- Design: `Planning~/design-docs/endpointer-profile.md` (locked), adr-0002.

## Current state

- Phases 1-7 are built and committed. Phase 7 (`100a987`) made `Responsive` the Continuous default:
  - `EndpointerOverlay.Resolve` returns `Responsive` for `Automatic` in Continuous: rule2 `0.45`, rule3 `0.75`, rule4 `1.0`.
  - The load line is `[VoxrSpeechRecogniser] Endpointer profile Responsive (Continuous default): rule2=0.45 rule3=0.75 rule4=1.0`.
  - The harness pin was re-pinned on the human's approval.
- Phase 7 gate baseline: EditMode 230/230, PlayMode 715/715, Command 3 37/37 on the new pin.
- Nothing of Phase 8 exists yet: no recon and no plan.

## Key files & locations

- `Runtime/EndpointerOverlay.cs` (`Resolve`, `Apply`, `LogLine`; the archive copy `.voxr-archive-model.conf`).
- `Runtime/VoxrSpeechRecogniser.cs`: the serialized `endpointerProfile` field and the resolve/apply/log at model load.
- `Runtime/VoxrPushToTalkController.cs`: the `ListeningModeHint` hand-off.
- `Runtime/ModelExtractor.cs`: where the extracted model and its `conf/model.conf` live on device, which the adb pull needs.
- `KNOWN_LIMITATIONS.md` and `Documentation~/troubleshooting.md`, for on-device gotchas.
- Precedent pickup: `.scratch/` files `code-mapper-`, `doc-extractor-`, `architect-`, `plan-validator-endpointer-profile-phase-7-*`. `plan-7.md` shows the shape (10 KB budget).

## Constraints & decisions

- **Human-only checks:** on-device Quest checks are the human's, reported as deferred and never claimed by an agent (`.claude/bindings/unity.md` Failure rule). No agent runs adb or deploys.
- **If Phase 8 fails:** Phase 7 (`100a987`) is reverted whole, and Phase 6's opt-in state stands. That revert is the human's ruling, not an automatic act.
- **Writer choices held for G2** are not reopened: `memory/2026-10-16.md` (plan-3) and `memory/2026-10-17.md` (plan-4, plan-6, plan-7).
- **Owed to the end-of-build reconcile, not Phase 8's:** B1-B4; the stale cites, including the architecture's README `:61-68`/`:68`; `joined-rig.md` rule 2; plan-6's and plan-7's line anchors.
- **Build facts:**
  - The agents' Bash tool collapses `\\` to `\`.
  - The vc-guard hook refuses VC-state changes to agents.
  - Everything runs Windows-native in Git Bash, never WSL.
  - Unity-made `memory/handoffs/*.meta` are committed with the phase.
  - Reports not filed by the hook are extracted from `subagents/agent-<id>.jsonl`.
- **Process:** a contradiction with locked canon reopens it on a `design-` branch.
- **Background session:** one named `feat-joined-rig-parser` may be listed at this root. It is not this branch's. If it writes in this tree, stop and tell the human.

## Next steps

1. Invoke `phase-pickup` for `feat-endpointer-profile` Phase 8.

## Open questions

- Whether Phase 8 needs a dev build or a specific scene or sample on the Quest, and how the human pulls the conf (the package or path for `adb`). Recon should settle these from the code; ask the human what recon cannot settle.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-endpointer-profile; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 898dfe5a-c050-4a82-9ec0-62a8dd227b09". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
