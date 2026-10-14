You're picking up: `review-cycle` for `feat-windows-native-harness`, then G2. Read the files below, then start at Next steps.

## Objective

Run `review-cycle` over the feature branch `feat-windows-native-harness` (all three build-plan phases built and gated), rule its findings with the human, then take the feature to G2 (`g2-accept`). Done = review findings ruled and any CONFIRMED ones fixed and re-gated, then the human's G2 ruling.

## Lane

feature

## Context

- The human, 2026-10-13: taking WSL out of the VoXR pipeline is top priority; `feat-joined-rig-parser`, `release` and `lab-next-level-probes` stay held.
- Design `windows-native-toolchain` G1 LOCKED 2026-10-13 (PR #174). This feature is its backlog row 1, **Full** track. Row 2 `windows-native-dotnet-fixtures` follows after this merges.
- Branch `feat-windows-native-harness`, one checkout (`trees` none), clean at `9c56b12`, pushed. No board; `memory/STATUS.md` is the state (its line updated 2026-10-14: next `review-cycle`, then G2).

## Current state

- Phase 1: plan `0e74bc3`, build `bd8443e` (cross-compiler source; arm64 symbols unchanged).
- Phase 2: plan `8bf279d`, build `5c10602` (`CMakePresets.json` `windows-x64`; `expectations.json:236` label; first MSVC build 0 warnings, 16/16).
- Phase 3: plan `3c04f8f` (`plan-3.md`, validated, one fix pass), build `84ac534` — `.claude/bindings/unity.md:8` Command 3 and `Planning~/verification-recipe.md:21` step 2b bound to `<CMD>` verbatim; `NativeBridge~/harness/README.md` rewritten (curl + SHA-256 + unzip, then `<CMD>`).
- Phase 3 gate: EditMode 203/203, PlayMode 704/704; the human downloaded the zip; README followed from a `vendor/` emptied to the model: sha256 OK, six files; Command 3 run verbatim from the binding: arm64 symbol diff empty, harness exit 0 16/16 from a fresh `build-windows/`; F4 scoped grep 0 at all six sites; `<CMD>` fixed-string count 1 in binding, recipe, README; `git diff --stat main -- Runtime/` empty (F8). Reports: `.scratch/compile-check-windows-native-harness-phase-3-gate-part{1,2}-report.md`, `.scratch/implement-windows-native-harness-phase-3-report.md`.
- The architecture doc's `## As-built deltas` is still empty; the reconcile to as-built (`architecture-doc` / `architect-reconcile`) is due before G2 per the feature track.

## Key files & locations

- Docs: `Planning~/features/windows-native-harness/{requirements.md,architecture.md,plan-1.md,plan-2.md,plan-3.md}`; design `Planning~/design-docs/windows-native-toolchain.md` + ADRs in `.../windows-native-toolchain/decisions/`.
- Changed paths vs `main` (code/bindings): `NativeBridge~/CMakeLists.txt`, `NativeBridge~/CMakePresets.json`, `NativeBridge~/harness/CMakeLists.txt`, `NativeBridge~/harness/expectations.json`, `NativeBridge~/harness/main.cpp`, `NativeBridge~/src/vosk_bridge.h`, `NativeBridge~/harness/README.md`, `.claude/bindings/unity.md`, `Planning~/verification-recipe.md`.
- Host: old `vendor/` contents and the old `build-windows/` kept in `.scratch/windows-native-harness-phase-3-{vendor,build}-aside/` (green run; safe to leave).

## Constraints & decisions

- Locked: MSVC from VS 2022 via the "Visual Studio 17 2022" generator; Unity's cmake 3.22.1 by absolute path; pinned `vosk-win64-0.3.45` (SHA-256 `f1dcc9cc...f34b`); WSL removed outright; no Windows bridge in the Editor (F8).
- Row 2 owns Command 4, DocCheck, `generate.sh`, the binding's Prerequisites/Shell/Path-form lines (:9, :12, :15, :17), recipe :3, :17, :24, :35-47 and other WSL sections: findings there are row 2's, not this feature's.
- Product docs (`Documentation~/native-bridge.md:59-61`, `CHANGELOG.md:74`) only after G2, by `codex-reconcile`. No CHANGELOG line so far (dev tooling).
- Agent network downloads are refused by permission policy: don't retry in another form; the human runs downloads.
- Subagents can't write under `.scratch/`: their reports are in `~/.claude/projects/D--Workspace-VoXR-Speech-Recognition/<session-id>/subagents/agent-<agentId>.jsonl`, the `SubagentHandback` tool call's `message`; the PM extracts and files them (open the file with a `C:/Users/...` path in Python; `MSYS_NO_PATHCONV=1` blocks `/c/` conversion).
- Gate pattern: Commands 1-2 in full with the host Editor closed, then the revert and untracked `.meta` orphan deletion (also under `memory/`); build steps go to `compile-check`, never the main thread. Leave `.claude/references/vc-git/__pycache__/` untracked.

## Next steps

1. Invoke `review-cycle` on `feat-windows-native-harness` (range `main..HEAD`).
2. Then the as-built reconcile of the architecture doc, then `g2-accept`.

## Open questions

- None pending for the human.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-windows-native-harness; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor c3cb9e21-f253-403a-9ed3-f851a9b8c677". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; send to the one on this machine. If the bare name is refused as ambiguous, use the ref `ListAgents` shows for the local one.)
