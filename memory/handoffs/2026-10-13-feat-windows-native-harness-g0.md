You're picking up: open `feat-windows-native-harness` at G0 and take it through requirements and architecture. You run as the hub (`voxr-hub`). Read the files below, then start at Next steps.

## Objective

Open backlog feature `windows-native-harness` (row 1 of `Planning~/design-docs/windows-native-toolchain.md` §6, **Full** track) at G0 on branch `feat-windows-native-harness`, then write its requirements doc and architecture doc. Done for this session = G0 recorded, and the architecture doc and first phase plan persisted, or a handoff at the context bound.

## Lane

feature

## Context

- The human, 2026-10-13: "Taking WSL out of Voxr pipeline is top priority. ... Forget everything else." Everything else stays on hold: `feat-joined-rig-parser`, `release`, lab `lab-next-level-probes`.
- Design `windows-native-toolchain` is **G1 LOCKED 2026-10-13** and merged to `main` (PR #174, `0791cda`). The locked `Planning~/design-docs/automated-verification.md` was re-locked with Amendment A2 (`## 0a`): Tier C runs Windows-native.
- The backlog row, quoted: "`windows-native-harness` | The Windows x64 desktop build of the bridge and harness: a cross-compiler export macro, the GNU link flags guarded off MSVC, the libvosk name per platform, a Windows preset replacing `desktop-linux`, and harness README provisioning of the pinned `vosk-win64-0.3.45` (adr-0001–adr-0004); the verification binding's Command 3 desktop half and the recipe's desktop-harness section go Windows-native. Acceptance: 16/16 against the committed `NativeBridge~/harness/expectations.json`. **Full.** | None."
- Row 2, `windows-native-dotnet-fixtures`, depends on this one. It owns Command 4, DocCheck, `generate.sh`, the binding's prerequisites, Shell and Path-form lines, the recipe's other WSL sections, and the final `wsl`/`/mnt/` sweep that closes WSL-1. Keep those out of this feature.
- One checkout (`trees` none). `main` is the dev branch; memory-only commits on `main` are fine.

## Current state

- `main` at `0791cda`. No `feat-windows-native-harness` branch yet.
- STATUS `## Current` line: "Next: `feat-windows-native-harness` at G0 once the `design-windows-native-toolchain` PR merges". It has merged, so update that line at G0.
- Recon is already filed (trust it; don't redo it):
  - `.scratch/code-mapper-windows-native-toolchain-recon-report.md`: every live WSL/Linux site, with file:line.
  - `.scratch/probe-windows-native-toolchain-host-report.md`: host tools.
  - `.scratch/researcher-windows-native-toolchain-report.md`: vosk-win64, piper, CMake facts.
- Host facts:
  - VS Community 2022 17.14 (MSVC 14.44) and 2026 18.6 are installed; `cl` is not on PATH.
  - cmake 3.22.1 and ninja 1.10.2 are at `C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\cmake\3.22.1\bin\`.
  - CMake 3.22 knows the "Visual Studio 17 2022" generator; VS 2026 needs CMake 4.2.
  - Windows SDK 10.0.26100.0.
  - `NativeBridge~/vendor/` holds only Linux vosk files plus `vosk-model-small-en-us-0.15/`.
  - `Runtime/Plugins/x86_64/libvosk.dll` is a MinGW build with no import library.

## Key files & locations

- Design: `Planning~/design-docs/windows-native-toolchain.md` (§1 model, §3 failure modes, §4 boundaries, §6 backlog) and `Planning~/design-docs/windows-native-toolchain/decisions/adr-0001`…`adr-0006`.
- Code sites:
  - `NativeBridge~/src/vosk_bridge.h:6` (export macro, 15 uses);
  - `NativeBridge~/CMakeLists.txt:59` (hard-coded `libvosk.so`), `:74` (GNU link flags, unconditional), `:84-89` (harness);
  - `NativeBridge~/CMakePresets.json` (only `desktop-linux`);
  - `NativeBridge~/harness/CMakeLists.txt`;
  - `NativeBridge~/harness/README.md:7-52` (provisioning, build, run);
  - `NativeBridge~/harness/expectations.json` (the baseline).
- Binding: `.claude/bindings/unity.md:8` (Command 3, desktop half via `wsl.exe`).
- Recipe: `Planning~/verification-recipe.md:21` (desktop-harness section).
- Feature docs go to `Planning~/features/windows-native-harness/` (`requirements.md`, `architecture.md`, `plan-<id>.md`), per the `process-docs` binding.

## Constraints & decisions

- Locked (don't reopen):
  - MSVC from VS 2022 (adr-0002);
  - Unity-bundled cmake and ninja by absolute path (adr-0003);
  - pinned `vosk-win64-0.3.45` in the gitignored `vendor/` (adr-0004);
  - WSL removed outright, with the `desktop-linux` preset deleted (adr-0001);
  - acceptance is 16/16 against the committed `expectations.json`, and a divergent Windows transcript **stops for the human**, never a silent re-pin;
  - no Windows bridge ships in or loads into the Editor (DR-4's editor half stands).
- Left to this feature's architecture doc, and owned by it:
  - the route into the VS environment ("Visual Studio 17 2022" generator vs Ninja inside vcvars);
  - the preset shape and the export-macro form;
  - confirming the zip contains `libvosk.lib` and whether its `libvosk.dll` is identical to the shipped one. A difference is a stop for the human.
- Don't write WSL into anything new. Don't touch `.claude/settings.local.json` (the human's), history docs, or product docs (`Documentation~/native-bridge.md:59-61` is for the codex reconciler after G2).
- Git Bash mangles `/`-prefixed args to Windows tools. `MSYS_NO_PATHCONV` handling belongs in the architecture doc.
- Subagents can't write report files under `.scratch/`. Briefs have them return text, and the PM files it.
- Leave `.claude/references/vc-git/__pycache__/` untracked.

## Next steps

1. Invoke `orient`.
2. Invoke `g0-open` for backlog feature `windows-native-harness` (Full), branch `feat-windows-native-harness`, design `Planning~/design-docs/windows-native-toolchain.md` §6 row 1.
3. Then `feature-requirement-doc`, then `architecture-doc`, then `phase-pickup`.

## Open questions

- None pending for the human before G0. The scope comes from the row above; the user story is the human's to state at G0.
- Later, not now: the auto-memory `doccheck-run-procedure` (WSL-based) gets rewritten when `windows-native-dotnet-fixtures` lands.
