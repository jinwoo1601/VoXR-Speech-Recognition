---
type: system
topic: windows-native-toolchain
tags: [verification, toolchain, native-harness, windows]
status: draft
updated: 2026-10-13
sources:
  - Planning~/design-docs/automated-verification.md (§3 goal 2 :76; Tier C :151, :183)
  - Planning~/design-docs/joined-rig/decisions/adr-0006-host-neutral-runner.md
  - memory/STATUS.md (## Deferred, WSL-1)
  - "Session direction 2026-10-13 (G0)"
---

# Design: Windows-Native Toolchain

**Branch:** `design-windows-native-toolchain`
**Origin:** the human's priority ruling 2026-10-13; subsumes deferral WSL-1.
**Status:** In design — opened at G0 2026-10-13; not locked.

**Scope, agreed at G0 (2026-10-13):** take WSL out of the VoXR pipeline, so that every verification command, rig and fixture tool this project runs works from Claude Code's Windows-native shell (Git Bash / PowerShell), with no `wsl.exe` anywhere in the live pipeline. The sites in scope are: the verification binding `.claude/bindings/unity.md` (Commands 3 and 4, the prerequisites, Shell, Path form); `Planning~/verification-recipe.md` (the desktop-harness section, "WSL Usage", the `Samples~` note); the native desktop build on Windows (`NativeBridge~/CMakePresets.json`, `NativeBridge~/CMakeLists.txt`, `NativeBridge~/harness/CMakeLists.txt`, `NativeBridge~/harness/README.md`, and the Windows libvosk it links); the C++ portability of `NativeBridge~/harness/main.cpp` and `NativeBridge~/src/vosk_bridge.cpp` where they hold POSIX-only code; fixture generation (`Tests~/Fixtures/generate.sh`); the parser A/B rig `Planning~/features/coverage-in-selection/ab-rig/` and its run procedure; the amendment of the locked `Planning~/design-docs/automated-verification.md` at its named sites under this topic's G1; the host choice that settles, for `feat-joined-rig-parser`, what adr-0006 left to that feature's architecture doc; and deferral WSL-1, which this topic subsumes and which closes at its G2.

> **What's decided going in.** The human (the maintainer) ruled on 2026-10-13: "Taking WSL out of Voxr pipeline is top priority. I don't know why it wasnt done before. Forget everything else." The work runs on the **design track**, not the light lane, then the build; at G0 on 2026-10-13 the branch is `design-windows-native-toolchain` and the scope above was agreed as proposed ("Agreed"). The design track is required because the locked `Planning~/design-docs/automated-verification.md` names WSL as Tier C's host, so changing the host contradicts a locked decision; this design amends that doc at the named sites (Goal 2 `:76`, Tier C purpose `:151`, what Tier C verifies `:183`) under its G1.
>
> **Non-goals:** product docs (e.g. `Documentation~/native-bridge.md:61`), updated only after G2 by the codex reconciler; WSL mentions in closed features' docs and in `Planning~/research/`, which are history and stay; the Android arm64 build, which already runs Windows-native with the cmake and ninja that Unity 6000.3.7f1's Android SDK ships; any change to recognition behaviour (see §0).

---

## 0. Scope (agreed at G0)

**Goal**

- Done means: every verification command, rig and fixture tool this project runs works from Claude Code's Windows-native shell (Git Bash / PowerShell), with no `wsl.exe` anywhere in the live pipeline.

**In** (the sites; the design decides what changes at each)

1. The verification binding `.claude/bindings/unity.md`: Command 3 (desktop harness, line 8), Command 4 (`Samples~` compile, line 9), the prerequisites (line 12), Shell (line 15), Path form (line 17).
2. `Planning~/verification-recipe.md`: the desktop-harness section (`:21`), "WSL Usage" (`:35`), the `Samples~` note (`:71`).
3. The native desktop build on Windows: `NativeBridge~/CMakePresets.json` (only `desktop-linux` today, against a Linux `libvosk.so` in the gitignored `NativeBridge~/vendor/`), `NativeBridge~/CMakeLists.txt` (harness added about `:84-88`), `NativeBridge~/harness/CMakeLists.txt`, `NativeBridge~/harness/README.md` (provisioning `:7-24`, build `:28-31`), and the Windows libvosk it links.
4. C++ portability of `NativeBridge~/harness/main.cpp` and `NativeBridge~/src/vosk_bridge.cpp`, where they hold POSIX-only code.
5. Fixture generation: `Tests~/Fixtures/generate.sh`.
6. The parser A/B rig `Planning~/features/coverage-in-selection/ab-rig/` (`stage.sh`, `Rig.csproj`, DocCheck) and its run procedure.
7. Amending the locked `Planning~/design-docs/automated-verification.md` at its named sites — Goal 2 `:76`, Tier C purpose `:151`, what Tier C verifies `:183` — under this topic's G1.
8. Coupling: this ruling settles, for `feat-joined-rig-parser`, the host choice that `Planning~/design-docs/joined-rig/decisions/adr-0006-host-neutral-runner.md` left to that feature's architecture doc ("which host runs each half (Windows-native or WSL)").
9. Deferral WSL-1 (`memory/STATUS.md` `## Deferred`) is subsumed by this topic and closes at its G2.

**Out**

- Product docs (e.g. `Documentation~/native-bridge.md:61`), updated only after G2 by the codex reconciler.
- WSL mentions in closed features' docs and in `Planning~/research/`, which are history and stay.
- The Android arm64 build, which already runs Windows-native with the cmake and ninja that Unity 6000.3.7f1's Android SDK ships.
- Any change to recognition behaviour.

**Forks for the design** (named, not ruled at G0; each is put to the human)

- The C++ toolchain for the desktop harness: MSVC, or clang with Unity's cmake and ninja. Not ruled.
- The Windows libvosk source: Alphacephei's win64 release, or another. Not ruled.
- How fixtures are generated on Windows. Not ruled.
- Whether the bridge or harness C++ needs portability work. Not ruled.
- Where cmake and ninja come from on PATH. Not ruled.
- Whether WSL stays as an optional fallback or is removed outright. Not ruled.

**Facts at G0** (verified 2026-10-13)

- `dotnet` is native at `C:\Program Files\dotnet`.
- `cmake` and `ninja` are not on PATH.
- WSL is still installed and a probe build of the harness there ran 16/16, exit 0.
