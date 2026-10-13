---
type: requirements
feature: windows-native-harness
topic: windows-native-toolchain
status: draft
updated: 2026-10-14
sources:
  - Planning~/design-docs/windows-native-toolchain.md
  - Planning~/design-docs/windows-native-toolchain/decisions/adr-0001-windows-native-wsl-removed.md
  - Planning~/design-docs/windows-native-toolchain/decisions/adr-0002-msvc-compiler.md
  - Planning~/design-docs/windows-native-toolchain/decisions/adr-0003-unity-bundled-cmake.md
  - Planning~/design-docs/windows-native-toolchain/decisions/adr-0004-pinned-vosk-win64.md
---

# feat-windows-native-harness — requirements

Track **Full**. Design `windows-native-toolchain` (`Planning~/design-docs/windows-native-toolchain.md`), locked at G1 2026-10-13.

## 1. User story

- **S1** — As the VoXR maintainer, I want the native bridge and push-audio harness to build and run Windows-native, so that Command 3's desktop check works from Claude Code's Windows shell with no WSL.

## 2. Feature & scope

Backlog row (`windows-native-toolchain.md` §6):

> `windows-native-harness` | The Windows x64 desktop build of the bridge and harness: a cross-compiler export macro, the GNU link flags guarded off MSVC, the libvosk name per platform, a Windows preset replacing `desktop-linux`, and harness README provisioning of the pinned `vosk-win64-0.3.45` (adr-0001–adr-0004); the verification binding's Command 3 desktop half and the recipe's desktop-harness section go Windows-native. Acceptance: 16/16 against the committed `NativeBridge~/harness/expectations.json`. **Full.** | None.

**Agreed at G0 on 2026-10-14** by the maintainer, in session.

**In**

1. The bridge and the push-audio harness build on Windows x64 with MSVC from Visual Studio 2022 (adr-0002), using the Unity-bundled cmake 3.22.1 and ninja 1.10.2 by absolute path (adr-0003).
2. A cross-compiler export macro in `NativeBridge~/src/vosk_bridge.h`.
3. The GNU link flags in `NativeBridge~/CMakeLists.txt` guarded off MSVC.
4. The libvosk library name chosen per platform (today hard-coded `libvosk.so`).
5. A Windows preset in `NativeBridge~/CMakePresets.json` replacing `desktop-linux`, which is deleted (adr-0001).
6. `NativeBridge~/harness/README.md` provisioning of the pinned `vosk-win64-0.3.45` into the gitignored `NativeBridge~/vendor/` (adr-0004), plus its build and run steps.
7. The verification binding's Command 3 desktop half (`.claude/bindings/unity.md`) and the desktop-harness section of `Planning~/verification-recipe.md` go Windows-native.
8. Acceptance: 16/16 against the committed `NativeBridge~/harness/expectations.json`. A divergent Windows transcript stops for the human; never a silent re-pin.
9. Left to this feature's architecture doc (named by the design, §6 and its Open list): the route into the Visual Studio environment ("Visual Studio 17 2022" generator vs Ninja inside vcvars); the preset shape; the export-macro form; confirming the vosk-win64 zip contains `libvosk.lib` and whether its `libvosk.dll` is identical to the shipped `Runtime/Plugins/x86_64/libvosk.dll` — a difference stops for the human.

**Out**

1. Owned by backlog row 2, `windows-native-dotnet-fixtures`: Command 4 (the `Samples~` compile); the parser A/B rig's DocCheck run procedure; `Tests~/Fixtures/generate.sh` (adr-0005); the verification binding's prerequisites, Shell and Path-form lines; the recipe's other WSL sections; the closing `wsl` / `/mnt/` sweep that closes deferral WSL-1.
2. Any Windows bridge in or loaded into the Unity Editor (DR-4's editor half stands, design §4).
3. Product docs (e.g. `Documentation~/native-bridge.md`) — after G2, by the codex reconciler.
4. `.claude/settings.local.json` and history docs.
