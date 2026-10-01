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

Each key is looked up as `.claude/references/core/delegation-contract.md` `## Bindings` says: `.claude/bindings/<pack>.md` first, the project `CLAUDE.md` *Bindings* section as the pre-scaffold fallback.

- `verification` (optional) — the command that actually recompiles, so a change to an assembly definition is confirmed by a real compile rather than by reading the graph. Absent, or `none` → the advice stays an inspection, and the skill says so rather than implying the change was verified.
- No agent: this skill dispatches nothing. Every step below is performed in the session that invoked it.

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
