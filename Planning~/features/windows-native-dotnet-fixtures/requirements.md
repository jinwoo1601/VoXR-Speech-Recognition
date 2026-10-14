---
type: requirements
feature: windows-native-dotnet-fixtures
topic: windows-native-toolchain
status: draft
updated: 2026-10-14
sources:
  - Planning~/design-docs/windows-native-toolchain.md
  - Planning~/design-docs/windows-native-toolchain/decisions/adr-0001-windows-native-wsl-removed.md
  - Planning~/design-docs/windows-native-toolchain/decisions/adr-0005-host-neutral-fixture-generation.md
---

# feat-windows-native-dotnet-fixtures — requirements

Track **Full**. Design `windows-native-toolchain` (`Planning~/design-docs/windows-native-toolchain.md`), locked at G1 2026-10-13. Depends on `windows-native-harness`, merged.

## 1. User story

- **S1** — As the VoXR maintainer, I want the `Samples~` compile, the DocCheck rig and fixture generation to run from Claude Code's Windows-native shell, so that no step of verification needs WSL installed or started.

## 2. Feature & scope

Backlog row (`windows-native-toolchain.md` §6):

> `windows-native-dotnet-fixtures` | Command 4 (the `Samples~` compile) and the parser A/B rig's DocCheck run procedure on native dotnet with Windows-form paths; the host-neutral `Tests~/Fixtures/generate.sh` (adr-0005); the verification binding's prerequisites, Shell and Path-form lines; the recipe's remaining WSL sections. Closes with the sweep for `wsl` and `/mnt/` over the live areas that closes deferral WSL-1 (adr-0001). **Full.** | `windows-native-harness`

**Agreed at G0 on 2026-10-14** by the maintainer, in session.

**In**

1. Command 4, the `Samples~` compile, runs on native dotnet with Windows-form paths: its line in the verification binding (`.claude/bindings/unity.md` Command 4) and the `Samples~` sections of `Planning~/verification-recipe.md`, including the generated csproj's `/mnt/...` paths.
2. The parser A/B rig's DocCheck run procedure (`Planning~/features/coverage-in-selection/ab-rig/`) runs on native dotnet. The procedure is written down in the repo; today it exists only outside the repo. Where it lives is settled in this doc's later sections. `stage.sh` is unchanged (design §1).
3. `Tests~/Fixtures/generate.sh` is made host-neutral (adr-0005): only the venv layout and the interpreter name change; piper-tts 1.6.0, voice `en_US-lessac-medium` and soxr stay.
4. The verification binding's Prerequisites, Shell and Path-form lines (`.claude/bindings/unity.md`) drop WSL.
5. The recipe's remaining WSL text in `Planning~/verification-recipe.md`: the `/mnt/c/...` Unity.exe path of Commands 1–2, the "cannot be verified from WSL" wording, the "WSL Usage" section, and the `Samples~` note.
6. A closing sweep for `wsl` and `/mnt/` over the live areas, which closes deferral WSL-1 (adr-0001). The exact list of live areas is settled in this doc's later sections; history docs are excluded per design §4.
7. **Folded in at G0** (ruled "Fold it in"): deferral WNH-1 — the comment at `NativeBridge~/include/vosk_api.h:3` names only `libvosk.so`, while the desktop build now links `libvosk.dll`; it is reworded.

**Out**

1. Command 3 (both halves) and the recipe's desktop-harness section and `### Building NativeBridge` — done by `windows-native-harness` (merged, PR #175).
2. Amendment A2 to `Planning~/design-docs/automated-verification.md` — written and re-locked at the design's G1.
3. The Android arm64 build, and any change to recognition behaviour.
4. Product docs (e.g. `Documentation~/native-bridge.md`) — after G2, by the codex reconciler.
5. `.claude/settings.local.json` (the human's file) and history docs: closed features' docs, `Planning~/research/`, `Planning~/handoff-issue-*.md`, `Planning~/features/coverage-in-selection/ab-rig/issue124/*.py`, the `Planning~/features/resolver-slot-exemption/` plans.
6. `feat-joined-rig-parser`, held by the human.
