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
  - Planning~/design-docs/windows-native-toolchain/decisions/adr-0001-windows-native-wsl-removed.md
  - Planning~/design-docs/windows-native-toolchain/decisions/adr-0002-msvc-compiler.md
  - Planning~/design-docs/windows-native-toolchain/decisions/adr-0003-unity-bundled-cmake.md
  - Planning~/design-docs/windows-native-toolchain/decisions/adr-0004-pinned-vosk-win64.md
  - Planning~/design-docs/windows-native-toolchain/decisions/adr-0005-host-neutral-fixture-generation.md
  - Planning~/design-docs/windows-native-toolchain/decisions/adr-0006-amendment-a2-all-sites.md
  - "Session direction 2026-10-13 (settle)"
---

# Design: Windows-Native Toolchain

**Branch:** `design-windows-native-toolchain`
**Origin:** the human's priority ruling 2026-10-13; subsumes deferral WSL-1.
**Status:** **G1 LOCKED 2026-10-13.** Rulings in adr-0001 through adr-0006; Amendment A2 written into `automated-verification.md` and re-locked by the same ruling. This document is immutable from here; a contradiction found during implementation reopens it on a new design branch and re-locks.

**Scope, agreed at G0 (2026-10-13):** take WSL out of the VoXR pipeline, so that every verification command, rig and fixture tool this project runs works from Claude Code's Windows-native shell (Git Bash / PowerShell), with no `wsl.exe` anywhere in the live pipeline. The sites in scope are: the verification binding `.claude/bindings/unity.md` (Commands 3 and 4, the prerequisites, Shell, Path form); `Planning~/verification-recipe.md` (the desktop-harness section, "WSL Usage", the `Samples~` note); the native desktop build on Windows (`NativeBridge~/CMakePresets.json`, `NativeBridge~/CMakeLists.txt`, `NativeBridge~/harness/CMakeLists.txt`, `NativeBridge~/harness/README.md`, and the Windows libvosk it links); the C++ portability of `NativeBridge~/harness/main.cpp` and `NativeBridge~/src/vosk_bridge.cpp` where they hold POSIX-only code; fixture generation (`Tests~/Fixtures/generate.sh`); the parser A/B rig `Planning~/features/coverage-in-selection/ab-rig/` and its run procedure; the amendment of the locked `Planning~/design-docs/automated-verification.md` at its named sites under this topic's G1; the host choice that settles, for `feat-joined-rig-parser`, what adr-0006 left to that feature's architecture doc; and deferral WSL-1, which this topic subsumes and which closes at its G2.

> **What's decided going in.** The human (the maintainer) ruled on 2026-10-13: "Taking WSL out of Voxr pipeline is top priority. I don't know why it wasnt done before. Forget everything else." The work runs on the **design track**, not the light lane, then the build; at G0 on 2026-10-13 the branch is `design-windows-native-toolchain` and the scope above was agreed as proposed ("Agreed"). The design track is required because the locked `Planning~/design-docs/automated-verification.md` names WSL as Tier C's host, so changing the host contradicts a locked decision; this design amends that doc at the named sites (Goal 2 `:76`, Tier C purpose `:151`, what Tier C verifies `:183`) under its G1. Forks F1–F6 ruled by the human at SETTLE 2026-10-13 ('All recommended'); see adr-0001…adr-0006.
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
   - Widened at SETTLE 2026-10-13 to every WSL/Linux line of that doc, DR-4 included (adr-0006). Line numbers of that doc on this page are those before A2 was inserted.
8. Coupling: this ruling settles, for `feat-joined-rig-parser`, the host choice that `Planning~/design-docs/joined-rig/decisions/adr-0006-host-neutral-runner.md` left to that feature's architecture doc ("which host runs each half (Windows-native or WSL)").
9. Deferral WSL-1 (`memory/STATUS.md` `## Deferred`) is subsumed by this topic and closes at its G2.

**Out**

- Product docs (e.g. `Documentation~/native-bridge.md:61`), updated only after G2 by the codex reconciler.
- WSL mentions in closed features' docs and in `Planning~/research/`, which are history and stay.
- The Android arm64 build, which already runs Windows-native with the cmake and ninja that Unity 6000.3.7f1's Android SDK ships.
- Any change to recognition behaviour.

**Forks for the design** (named, not ruled at G0; each is put to the human)

- The C++ toolchain for the desktop harness: MSVC, or clang with Unity's cmake and ninja. Not ruled at G0. **RESOLVED** at SETTLE 2026-10-13 — adr-0002.
- The Windows libvosk source: Alphacephei's win64 release, or another. Not ruled at G0. **RESOLVED** at SETTLE 2026-10-13 — adr-0004.
- How fixtures are generated on Windows. Not ruled at G0. **RESOLVED** at SETTLE 2026-10-13 — adr-0005.
- Whether the bridge or harness C++ needs portability work. Not ruled at G0. **RESOLVED** by the recon facts, no ADR: needed, and small — the export macro, the guarded link flags, the per-platform libvosk name.
- Where cmake and ninja come from on PATH. Not ruled at G0. **RESOLVED** at SETTLE 2026-10-13 — adr-0003.
- Whether WSL stays as an optional fallback or is removed outright. Not ruled at G0. **RESOLVED** at SETTLE 2026-10-13 — adr-0001.

**Facts at G0** (verified 2026-10-13)

- `dotnet` is native at `C:\Program Files\dotnet`.
- `cmake` and `ninja` are not on PATH.
- WSL is still installed and a probe build of the harness there ran 16/16, exit 0.

This design is the host-and-toolchain model for every verification, rig and fixture tool the project runs; it carries `Planning~/design-docs/automated-verification.md` §3 goal 2 (`:76`) — Claude-runnable, no Unity, no device — on the host Claude actually runs on, Claude Code's Windows-native shell. Its keystone fork is adr-0001 (Windows-native, WSL removed outright), and its governing constraint is the acceptance invariant: the Windows harness reproduces the committed `NativeBridge~/harness/expectations.json` exactly, 16/16, or the build stops for the human.

> **What's decided.** One host, Claude Code's Windows-native shell (Git Bash / PowerShell), runs every half of verification, and WSL is removed outright (adr-0001); the desktop harness builds with MSVC from Visual Studio 2022 (adr-0002), with Unity's bundled cmake and ninja by absolute path (adr-0003), against the pinned `vosk-win64-0.3.45` release (adr-0004); fixtures come from one host-neutral `generate.sh` (adr-0005); and one Amendment A2 covers every WSL/Linux line of `Planning~/design-docs/automated-verification.md` (adr-0006). The Windows harness must reproduce the committed baseline exactly, 16/16. No number on this page is a tuning value: the pinned versions are external facts, not tuning.

## 1. The model

One host — Claude Code's Windows-native shell — runs every half of verification. A verification run is Claude, in that shell, running the bound commands in turn, each tool reached on PATH or by the absolute path the binding names, and Tier C judged against the committed baseline.

| Half | Today | After (design intent, not yet built) |
|---|---|---|
| Unity tests (Commands 1–2) | Windows-native | Unchanged |
| arm64 bridge build (Command 3, first half) | Windows-native, with the cmake 3.22.1 and ninja 1.10.2 that Unity 6000.3.7f1's Android SDK ships | Unchanged |
| Desktop harness (Command 3, desktop half) | WSL: the `desktop-linux` preset against a Linux `libvosk.so` in the gitignored `NativeBridge~/vendor/` | Windows-native: MSVC (adr-0002), Unity's bundled cmake and ninja by absolute path (adr-0003), the pinned win64 libvosk in `NativeBridge~/vendor/` (adr-0004), the existing model; Windows build output |
| `Samples~` compile (Command 4) | A WSL route in the binding | Native dotnet (SDK 10.0.300) |
| Parser A/B rig and DocCheck | `stage.sh` already runs in Git Bash; the run procedure is a §0 site | Native dotnet; `stage.sh` unchanged |
| Fixture generation | `Tests~/Fixtures/generate.sh` | The same script, host-neutral: same piper-tts 1.6.0, voice `en_US-lessac-medium` and soxr (adr-0005) |

**Rules** (design intent, not yet built):

1. No `wsl.exe` anywhere in the live pipeline (adr-0001).
2. Each tool is pinned, and named by absolute path where it is not on PATH (adr-0003).
3. Paths are written in Windows or Git Bash form only, never `/mnt/`.
4. The acceptance invariant: the Windows harness reproduces the committed `NativeBridge~/harness/expectations.json` exactly, 16/16; a divergent Windows transcript stops the build for the human's ruling and is never re-pinned silently, consistent with joined-rig's standing rule "re-pin, never edit".

## 2. The decisions it puts to its operator

The operator is the PM or Claude running verification, and the human, who alone rules a re-pin or a stop.

- **A Windows transcript diverges from the baseline: investigate, or a ruled re-pin.** Information available: which of the 16 fixtures diverged and how, against `NativeBridge~/harness/expectations.json`, and what changed in the build (compiler, cmake, libvosk). Cost: investigating keeps Tier C red while the cause is found; a re-pin moves the baseline every later run is judged against, so it is the human's ruling, never the operator's default.
- **The Unity editor version moves.** The bundled cmake and ninja paths move with it, so the binding is edited at one place. Information available: the installed editor version and whether its Android SDK ships cmake and ninja. Cost: one binding edit; an editor without a bundled cmake reopens adr-0003 toward a winget install.
- **When to re-provision `NativeBridge~/vendor/`.** The directory is gitignored, so the pinned zip is provisioned rather than committed. Information available: the pinned release, `vosk-win64-0.3.45.zip` (14,882,445 bytes). Cost: a download; the version stays pinned at 0.3.45, so re-provisioning moves no number.
- **The shipped and the vendored libvosk differ.** This is a stop for the human (adr-0004). Information available: the architecture doc's identity check of the zip's `libvosk.dll` against `Runtime/Plugins/x86_64/libvosk.dll`. Cost: a difference is the case in which adr-0004's rejected branch, the shipped DLL with a hand-made import library (`dumpbin` and `lib /def`), would win.

## 3. Dynamics & failure modes

With one host and each tool pinned, a run changes from the last only where a pinned input moved; each failure below is such a move, with the rule that kills it.

- **Toolchain drift.** The Unity editor or Visual Studio 2022 is removed or moved; the build fails loudly at the absolute path rather than silently picking another tool, and the fix is a single binding edit (rule 2).
- **Compiler-induced float differences.** MSVC may compute the bridge front end (the downsampler, the AGC) differently from the Linux build and shift a transcript; the acceptance invariant (rule 4) kills it: 16/16 against the committed baseline, or a stop for the human.
- **Vendor-against-shipped DLL drift.** The vendored libvosk and the one the Editor runs may not be the same binary; the architecture doc's identity check catches it, and a difference is a stop for the human (adr-0004).
- **WSL creeping back as a "quick fix".** Rule 1, no `wsl.exe`, kills it; its enforcer is a sweep for `wsl` and `/mnt/` over the live areas at the review of this topic's features and at G2 — a prose enforcer, stated as such (design intent, not yet built).
- **Git Bash path conversion.** Git Bash converts `/`-prefixed arguments passed to Windows tools; the binding states the path form (rule 3), and its handling is the architecture doc's.

## 4. Boundaries & couplings

- **Owns:** the host and toolchain of Tier C; the provisioning of `NativeBridge~/vendor/`; the fixture-generation host; the shell routes of Commands 3 and 4 and of the parser A/B rig.
- **Couples to:** `Planning~/design-docs/automated-verification.md`, amended by Amendment A2 (adr-0006); `Planning~/design-docs/joined-rig/decisions/adr-0006-host-neutral-runner.md`, whose host question, left to `feat-joined-rig-parser`'s architecture doc, this design settles: Windows-native, both halves; Command 3's arm64 half, unchanged.
- **Hands off to the architecture doc:** the route into the Visual Studio environment (the "Visual Studio 17 2022" generator, or Ninja inside the Visual Studio environment); the preset shape; the form of the export macro; the confirmation of the zip's contents and of the DLL identity; the exact binding and recipe text.
- **Hands off to the codex reconciler, after G2:** `Documentation~/native-bridge.md:59-61`.
- **Does not decide:**
  - The Android build.
  - Recognition behaviour.
  - Any CI.
  - Shipping or loading the Windows bridge inside the Unity Editor: the Windows desktop build is a Tier C harness target only; the editor keeps its separate C# mic path, and the editor-side reasoning of automated-verification.md DR-4 (:240-242) stands — only DR-4's host (Linux/WSL) is amended (adr-0006).
  - History docs, which are left as they are: `Planning~/features/coverage-in-selection/ab-rig/issue124/{compare-arms,analyse-arms}.py` (with their stale `/mnt/d/Game Development` path), the WSL steps in the `Planning~/features/resolver-slot-exemption/` plans, closed features' docs and `Planning~/research/`.
  - The human's local settings: `.claude/settings.local.json` (35 WSL allow rules, untracked) is the human's file and is not touched.

## 5. What it delivers

`Planning~/design-docs/automated-verification.md` §3 goal 2 (`:76`) promised "Automated coverage of native-bridge logic that Claude can run directly from WSL, with no Unity and no device." Its intent — Claude-runnable, no Unity, no device — is kept; its host, "from WSL", named the host of its day and is replaced by Claude Code's Windows-native shell (Git Bash / PowerShell), as joined-rig adr-0006 read the same goal.

When it goes right, goal 2's promise holds on the host Claude actually runs on: one shell, no second OS to install or start, and deferral WSL-1 closed (design intent, not yet built). The same 16 fixtures are judged against the same baseline, so no number moves.

## 6. Feature backlog — locked at G1 (2026-10-13)

Two features, in dependency order. Both are **Full**: the design leaves the Visual Studio environment route, the preset shape, the export-macro form, the zip check, the exact binding and recipe text and `generate.sh`'s venv-layout handling to the architecture docs, so each build writes its own requirements and architecture docs.

| Feature | Scope | Depends on |
|---|---|---|
| `windows-native-harness` | The Windows x64 desktop build of the bridge and harness: a cross-compiler export macro, the GNU link flags guarded off MSVC, the libvosk name per platform, a Windows preset replacing `desktop-linux`, and harness README provisioning of the pinned `vosk-win64-0.3.45` (adr-0001–adr-0004); the verification binding's Command 3 desktop half and the recipe's desktop-harness section go Windows-native. Acceptance: 16/16 against the committed `NativeBridge~/harness/expectations.json`. **Full.** | None. |
| `windows-native-dotnet-fixtures` | Command 4 (the `Samples~` compile) and the parser A/B rig's DocCheck run procedure on native dotnet with Windows-form paths; the host-neutral `Tests~/Fixtures/generate.sh` (adr-0005); the verification binding's prerequisites, Shell and Path-form lines; the recipe's remaining WSL sections. Closes with the sweep for `wsl` and `/mnt/` over the live areas that closes deferral WSL-1 (adr-0001). **Full.** | `windows-native-harness` |

## Status / openness

- **Status: G1 LOCKED 2026-10-13.** Forks F1–F6 were ruled by the human at SETTLE 2026-10-13 ("All recommended") and are recorded in adr-0001…adr-0006; locked at G1 by the human's ruling "Design locked".
- **Numbers:** none of note to tune; the pinned versions (cmake 3.22.1, ninja 1.10.2, libvosk 0.3.45, piper-tts 1.6.0, soxr 1.1.0, Visual Studio 2022) are external facts, not tuning values.
- **Hard dependencies / open:**
  - RESOLVED: F1 the compiler (adr-0002); F2 cmake and ninja (adr-0003); F3 the Windows libvosk (adr-0004); F4 fixture generation (adr-0005); F5 WSL removed outright (adr-0001); F6 the amendment's reach (adr-0006); C++ portability, settled by the recon facts with no ADR.
  - Open: the zip's contents and whether its `libvosk.dll` is identical to the shipped one — the architecture doc's (adr-0004) — owner: the architecture doc of `feat-windows-native-harness`.
  - Open: the route into the Visual Studio environment — the architecture doc's (adr-0002) — owner: the architecture doc of `feat-windows-native-harness`.
  - RESOLVED: Amendment A2 re-locked at G1 2026-10-13 (adr-0006).
  - RESOLVED: the feature backlog, §6, written at G1.

## Related

- [automated-verification.md](../design-docs/automated-verification.md) — the locked parent; goal 2 and Tier C, amended by Amendment A2.
- [joined-rig.md](../design-docs/joined-rig.md) — the coupled design whose rig this host serves.
- [joined-rig adr-0006](../design-docs/joined-rig/decisions/adr-0006-host-neutral-runner.md) — the host question this design settles.
- [adr-0001](../design-docs/windows-native-toolchain/decisions/adr-0001-windows-native-wsl-removed.md) — Windows-native, WSL removed outright (keystone).
- [adr-0002](../design-docs/windows-native-toolchain/decisions/adr-0002-msvc-compiler.md) — MSVC.
- [adr-0003](../design-docs/windows-native-toolchain/decisions/adr-0003-unity-bundled-cmake.md) — Unity's bundled cmake and ninja.
- [adr-0004](../design-docs/windows-native-toolchain/decisions/adr-0004-pinned-vosk-win64.md) — the pinned vosk-win64-0.3.45.
- [adr-0005](../design-docs/windows-native-toolchain/decisions/adr-0005-host-neutral-fixture-generation.md) — host-neutral `generate.sh`.
- [adr-0006](../design-docs/windows-native-toolchain/decisions/adr-0006-amendment-a2-all-sites.md) — Amendment A2 over every WSL/Linux line.
- [verification-recipe.md](../verification-recipe.md) — the recipe whose WSL routes go.
- [unity.md](../../.claude/bindings/unity.md) — the verification binding.
- [harness README](../../NativeBridge~/harness/README.md) — provisioning and build of the desktop harness.
- [generate.sh](../../Tests~/Fixtures/generate.sh) — fixture generation.
