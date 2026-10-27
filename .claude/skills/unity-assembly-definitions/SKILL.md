---
name: unity-assembly-definitions
description: "Design, create, and troubleshoot Unity Assembly Definitions (.asmdef). Use when the user mentions .asmdef files, recompilation times, dependency graphs, circular references, editor or test assemblies, or modularising a Unity codebase."
kind: execution
status: active
removal-date: null
bindings: [verification]
---

# Unity Assembly Definitions

Assembly definitions (`.asmdef` files) are Unity's mechanism for splitting a C# codebase into
explicit, independently compiled modules. They matter not primarily for compile-time improvements
(though that's a welcome side effect), but because they make dependencies between systems **visible
and enforceable** by the compiler. This changes how developers think about architecture.

Read `references/architecture-guide.md` when you need practical how-to details — it covers
inspector settings, building assemblies from scratch, `.asmref` usage, test assembly configuration,
version defines, UPM package structure, working with third-party assets, and an incremental
adoption strategy. The guide does not repeat the principles below; it extends them with concrete
steps.

## Bindings

- `verification` (optional) — the command that actually recompiles, so a change to an assembly definition is confirmed by a real compile rather than by reading the graph. Absent, or `none` → the advice stays an inspection, and the skill says so rather than implying the change was verified.
- No agent: this skill dispatches nothing. Every step below is performed in the session that invoked it.

<!-- harness:binding:start verification .claude/bindings/unity.md -->
<!-- Interview: How is this Unity project compiled and tested from where the human works — which run, in which order, what does green look like for each, and how are only the checks that cover one change run — and may those commands also run in a writer tree, a parallel writer's own working tree, from that tree's root? Which shell does the Bash tool run commands in, which command prints the working directory, and in which form is a path written and two paths compared? One `Command N` line per command in run order; `Fallback` which route replaces which Commands when no Editor holds this tree open, which selector picks it and what its green is, or `none`; `Targeted` a command with `<target>` where the change's checks go, or `none`; `Tree-safe` answered `yes` or `no`, unstated read as `no`; a single `none` when nothing can be run. How a call names its tree, which route runs and how green is read: `.claude/references/unity/unity-cli-contract.md`. -->
- Run from: the project root `D:\Workspace\VoXR-Speech-Recognition`, the one checkout, which the host project `D:\Workspace\VoXR TestGround` (Unity 6000.4.7f1) consumes by `file:` path — this bare UPM package compiles and tests only through that host; the full procedure, the toolchain paths and the Samples~ csproj are in `Planning~/verification-recipe.md`
- Command 1: EditMode through the host, the Editor closed (`D:\Workspace\VoXR TestGround\Temp\UnityLockfile` absent), `Tests~` renamed `Tests` and `"testables": ["com.jinwoo1601.voxr"]` in the host's `Packages/manifest.json`: `"/c/Program Files/Unity/Hub/Editor/6000.4.7f1/Editor/Unity.exe" -runTests -batchmode -projectPath "D:\Workspace\VoXR TestGround" -testPlatform EditMode -testResults "D:\Workspace\VoXR-Speech-Recognition\.scratch\test-results\editmode.xml" -logFile "D:\Workspace\VoXR-Speech-Recognition\.scratch\test-results\editmode.log"`, no `-quit` — expected green: the NUnit results XML this run wrote, `failed="0"` and `inconclusive="0"`
- Command 2: PlayMode, the same with `-testPlatform PlayMode` and `playmode.xml` and `playmode.log` (most parser and command tests are PlayMode, so it is never skipped); then the revert — `Tests` back to `Tests~`, the Unity-generated `.meta` orphans removed (root `Tests.meta` included), the host manifest restored — expected green: as Command 1, and `git status --short` showing no `Tests` leftovers
- Command 3: only when `NativeBridge~/` or a source `confgen` links (`NativeBridge~/harness/confgen/confgen.csproj`) changed: the arm64 build with the Unity 6000.3.7f1 Android SDK's `cmake.exe` and `ninja.exe` per the recipe, then the desktop harness Windows-native in Git Bash — MSVC from Visual Studio 2022 Community through CMake's "Visual Studio 17 2022" generator, the same SDK's `cmake.exe` by absolute path, `NativeBridge~/vendor/` provisioned per `NativeBridge~/harness/README.md` — `cd NativeBridge~ && ls -d 'C:/Program Files/Microsoft Visual Studio/2022/Community' 'C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe' >/dev/null && 'C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe' --preset windows-x64 '-DCMAKE_GENERATOR_INSTANCE=C:/Program Files/Microsoft Visual Studio/2022/Community' && 'C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe' --build --preset windows-x64 && dotnet build harness/confgen/confgen.csproj --artifacts-path build-confgen && dotnet build-confgen/bin/confgen/debug/confgen.dll --model vendor/vosk-model-small-en-us-0.15 --out vendor/overlaid/continuous-default --profile Automatic --mode Continuous && ./build-windows/bin/vosk-bridge-harness.exe --model vendor/overlaid/continuous-default --fixtures "../Tests~/Fixtures/audio/tts" --manifest harness/expectations.json`; on a bridge ABI change the rebuilt `.so` is committed at `Runtime/Plugins/Android/arm64-v8a/libvosk-bridge.so` — expected green: `libvosk-bridge.so` built with no errors, and the harness exits 0 with 37/37 transcripts matching; a transcript mismatch (exit 1) stops for the human's ruling, never a re-pin
- Command 4: only when `Runtime/` public API or any `Samples~/` source changed: the out-of-band Samples~ compile on native dotnet in Git Bash, the csproj generated per the recipe into `.scratch/samples-compile/` — `(cd .scratch/samples-compile && dotnet build SamplesCompile.csproj -v m -nologo)` — the only check that compiles `Samples~`; a missing Unity or host input fails the build naming its path — expected green: `Build succeeded` and `0 Error(s)`, the one CS0618 warning on `PermissionDeniedAndDontAskAgain` expected, and a mutation that breaks a sample turning it red
- Fallback: none — no Command goes through a live Editor: Commands 1 and 2 run batchmode with the host's Editor closed, Commands 3 and 4 run outside Unity
- Failure rule: a non-zero exit, a results XML missing or not written by this run, `failed` or `inconclusive` above 0, or a Samples~ green whose mutation check stayed green; on-device Quest checks — mic capture, `OnApplicationPause` delivery on doff or the system menu, any native-bridge change — are human-only, reported as deferred and never claimed
- Prerequisites: Unity 6000.4.7f1 with an active licence and the host imported at least once; for Command 3 the Unity 6000.3.7f1 Android NDK and SDK (its `cmake.exe` for both halves, `ninja.exe` for the arm64 half), Visual Studio 2022 Community with the x64 C++ tools, and `NativeBridge~/vendor/` provisioned per `NativeBridge~/harness/README.md` (the pinned `vosk-win64-0.3.45` and the small model), and the .NET SDK 10 with dotnet on PATH (confgen); for Command 4 the .NET SDK 10 installed natively with `dotnet` on PATH, the Unity 6000.4.7f1 data folder and the host's `Library/ScriptAssemblies` (`UnityEngine.UI.dll`, `Unity.InputSystem.dll`)
- Targeted: Command 1 or 2 with `-testFilter "<target>"` added — a test name, fixture or namespace regex
- Tree-safe: no — the host consumes this checkout by path, and a batchmode run takes the host's Editor lock
- Shell: Git Bash (an MSYS2 bash on Windows), in which every Command runs directly, each Windows program reached on PATH (`dotnet`) or by absolute path; Claude Code's Git Bash sets `MSYS_NO_PATHCONV=1` and a plain Git Bash does not, and every bound command behaves the same either way
- Working-dir read: `pwd -W`
- Path form: Windows form `D:\…`, compared case-insensitively with either separator; the MSYS forms `/d/…`, `/c/…` and `/tmp` appear only inside commands, as command words bash resolves, and are never written as a path; an argument a Windows program receives and a path inside a generated file are in drive-letter form (`C:/…` or `D:\…`); the working-dir read `pwd -W` already prints this form (`D:/…`)
<!-- harness:binding:end verification -->

## Core Principles

1. **Organise by responsibility and stability, not by script type.** Avoid assemblies named
   "Managers", "Components", or "Utilities". Group code by what it represents in the system
   and how likely it is to change.

2. **Dependencies flow inward toward stable code.** Stable, low-level code sits at the bottom
   of the dependency graph. Higher-level, fast-changing code sits above it. No circular
   dependencies — these are compiler errors, not warnings.

3. **References are NOT transitive.** If Assembly A references Assembly B, and B references C,
   A does **not** automatically see C. This is deliberate — it prevents accidental coupling and
   keeps the dependency graph honest. If A needs types from C, it must reference C explicitly.

4. **Be incremental.** Introduce one assembly at a time. Shape the dependency graph gradually
   rather than designing a perfect structure upfront. See the adoption strategy in the
   architecture guide for a recommended ordering.

5. **Commit to full coverage.** Unity strongly recommends that if you use assembly definitions
   at all, you use them for all code in your project. The predefined assemblies
   (`Assembly-CSharp`) automatically depend on every custom assembly — so any change in a
   custom assembly triggers recompilation of all remaining scripts still in `Assembly-CSharp`.
   Partial adoption can actually make compile times *worse* until you reach full coverage.

## Typical Dependency Graph

A well-structured Unity project generally follows this layering:

```
Tests ──► Gameplay ──► Core ◄── UI
                         ▲
               Editor ───┘ (also ──► Gameplay)
```

- **Core**: Shared interfaces, data types, small utilities. Changes rarely.
- **Gameplay**: Mechanics, game logic. Depends on Core.
- **UI**: Views, widgets. Depends on Core, not on Gameplay internals.
- **Editor & Tooling**: Depends on runtime code. Runtime never depends on Editor.
- **Tests**: Separate assemblies. Reference the systems they test.

This layering is where broader architectural patterns become enforceable. Assembly boundaries
turn conventions into compiler errors — dependency injection containers (Zenject, Extenject,
Reflex), service locators, and event systems all benefit from assemblies that physically prevent
UI code from reaching into Gameplay internals or runtime code from depending on Editor APIs.
Interfaces defined in Core become natural seams for inversion of control.

## When Creating or Reviewing .asmdef Files

1. Check `references/architecture-guide.md` for the full inspector settings table
   (Auto Reference, Override References, GUID vs Name referencing, Root Namespace, etc.).
2. Always verify editor folders — a parent `.asmdef` absorbs Editor/ scripts into runtime
   unless an explicit editor `.asmdef` or `.asmref` exists inside the Editor folder.
3. Prefer GUID-based references over name-based for refactor safety.
4. Don't create micro-assemblies for every feature. Assemblies should represent meaningful
   seams. If two systems always change together, they belong in the same assembly.
5. For test assemblies, see the architecture guide for platform configuration details
   (edit-mode vs play-mode) and the `InternalsVisibleTo` pattern for testing `internal` types.
6. For assemblies that optionally integrate with external packages (e.g., TextMeshPro,
   Addressables), use **Version Defines** to conditionally enable code paths without
   creating hard dependencies. See the architecture guide for details.

## .asmdef File Format

Assembly definitions are JSON files with fields for name, references, platform filtering,
and conditional compilation. Use dotted naming (e.g., `MyGame.Gameplay`), prefer GUID-based
references, and set `rootNamespace` for every assembly. See `references/architecture-guide.md`
for the full field reference table and examples.

## Common Mistakes to Watch For

- **Editor scripts in runtime assemblies**: Caused by a parent `.asmdef` absorbing an Editor/
  folder. Fix: add an explicit editor-only `.asmdef` inside the Editor folder.
- **Circular references**: Two assemblies that want to reference each other. This means the
  responsibility split isn't right. Extract a shared interface/data assembly.
- **Too many micro-assemblies**: More assemblies ≠ better architecture. Group by meaningful
  boundaries, not by individual scripts or features.
- **Name-based references breaking on rename**: Use GUID-based references to avoid this.
- **Third-party assets without .asmdef**: After adopting assembly definitions, Asset Store
  assets that lack their own `.asmdef` remain in `Assembly-CSharp` and become invisible to
  your custom assemblies. See the architecture guide for strategies to handle this.
- **Making everything `public` for tests**: Use `InternalsVisibleTo` instead. Keep types
  `internal` for proper encapsulation and grant access only to your test assembly.
