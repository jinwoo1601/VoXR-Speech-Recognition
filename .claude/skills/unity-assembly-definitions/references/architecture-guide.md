# Unity Assembly Definitions: Practical Reference

> This guide extends the principles in SKILL.md with concrete steps, settings, and workflows.
> Read SKILL.md first for the architectural philosophy; come here for the how-to.

## Table of Contents

1. [How Unity Compiles Scripts](#how-unity-compiles-scripts)
2. [Building an Assembly from Scratch](#building-an-assembly-from-scratch)
3. [Assembly Definition References (.asmref)](#assembly-definition-references-asmref)
4. [Editor Assembly Setup](#editor-assembly-setup)
5. [Key Inspector Settings](#key-inspector-settings)
6. [.asmdef JSON Field Reference](#asmdef-json-field-reference)
7. [Version Defines and Define Constraints](#version-defines-and-define-constraints)
8. [Test Assembly Configuration](#test-assembly-configuration)
9. [InternalsVisibleTo for Testing](#internalsvisibleto-for-testing)
10. [Working with Third-Party Assets](#working-with-third-party-assets)
11. [UPM Package Structure](#upm-package-structure)
12. [Incremental Adoption Strategy](#incremental-adoption-strategy)
13. [Debugging Tips](#debugging-tips)

---

## How Unity Compiles Scripts

See SKILL.md Principle 5 for the full rationale. In short: without assembly definitions,
Unity compiles everything into `Assembly-CSharp` and recompiles the entire unit on any
change. Assembly definitions break this into independent modules with compiler-enforced
boundaries.

---

## Building an Assembly from Scratch

Even in an existing project, deleting and recreating an assembly definition can be
valuable — it forces a rethink of what actually belongs and what it truly depends on.

### Steps

1. **Create the `.asmdef` file** in the root folder of the assembly's code. Unity compiles
   all scripts in that folder and its subfolders (unless a child `.asmdef` claims them).

2. **Add references.** Each reference declares an explicit dependency. Adding a reference
   says "this assembly is allowed to use this code." Omitting a reference means the
   compiler enforces that boundary automatically.

3. **Handle scripts in non-standard locations** using `.asmref` files (see next section).

---

## Assembly Definition References (.asmref)

If scripts that logically belong to an assembly don't live under the same folder tree,
create an **Assembly Definition Reference** (`.asmref`) asset inside the relevant folder
and point it at the target `.asmdef`. This compiles those scripts into the specified
assembly without moving folders.

Use cases:
- Plugin or package code that must live in a specific location but belongs to your assembly.
- Editor scripts in nested folders that should compile into a shared editor assembly.
- Third-party code that you want to include in a specific compilation unit.

---

## Editor Assembly Setup

A parent `.asmdef` absorbs all scripts beneath it — including scripts in Editor/ folders.
This pulls editor-only code (referencing `UnityEditor`) into runtime assemblies, causing
build errors on non-editor platforms.

### Fix

Place a dedicated `.asmdef` (or `.asmref`) inside each Editor folder that falls under a
parent `.asmdef`. The editor assembly should be:

- **Editor-only** in platform settings (`includePlatforms: ["Editor"]`).
- Given its own dependency references.
- Allowed to depend on its parent runtime assembly (but never the reverse).

### Example

```
Assets/
  MyGame/
    MyGame.Gameplay.asmdef          ← runtime assembly
    Player/
      PlayerController.cs
    Editor/
      MyGame.Gameplay.Editor.asmdef ← editor-only assembly, references MyGame.Gameplay
      PlayerInspector.cs
```

This also frees editor scripts from needing to live in folders literally named "Editor."
They can exist anywhere, as long as an editor-only `.asmdef` claims them.

---

## Key Inspector Settings

| Setting | What It Does | When to Use |
|---|---|---|
| **Auto Reference** | Controls whether Unity's predefined assemblies (`Assembly-CSharp`) automatically reference this custom assembly. | Disable for internal-only assemblies that shouldn't be visible to the default compile unit. |
| **Override References** | Instead of Unity providing default references, you explicitly list every allowed precompiled DLL dependency. | Enforcing strict constraints on third-party DLLs. |
| **Use GUIDs** | References use the `.asmdef` file's GUID instead of its name string. | Always prefer this. Renaming assemblies won't break GUID-based references. |
| **Root Namespace** | Default namespace for scripts created within the assembly. IDEs use this when generating new scripts. | Set for every assembly to maintain consistent namespacing. |
| **Define Constraints** | Assembly only compiles when specific scripting symbols are defined. | Feature toggles, platform-specific assemblies, test-only assemblies. |
| **Version Defines** | Define preprocessor symbols based on installed package/module versions. | Optional integrations with external packages. See dedicated section below. |
| **No Engine References** | Removes the implicit dependency on `UnityEngine`. | Pure C# logic assemblies with no Unity API usage. |
| **Allow Unsafe Code** | Permits use of the C# `unsafe` keyword in this assembly. | Assemblies that need pointer operations or native interop. |

---

## .asmdef JSON Field Reference

A typical `.asmdef` file:

```json
{
    "name": "MyGame.Core",
    "rootNamespace": "MyGame.Core",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

| Field | Type | Default | Purpose |
|---|---|---|---|
| `name` | string | — | Assembly name. Use dotted naming (e.g., `MyGame.Gameplay`). |
| `rootNamespace` | string | `""` | Default namespace for new scripts. Set for every assembly. |
| `references` | string[] | `[]` | Other `.asmdef` names or GUIDs. Format: `"GUID:<value>"`. |
| `includePlatforms` | string[] | `[]` | Compile only for these platforms. Empty = all platforms. |
| `excludePlatforms` | string[] | `[]` | Exclude these platforms. Mutually exclusive with `includePlatforms`. |
| `allowUnsafeCode` | bool | `false` | Permits the C# `unsafe` keyword. |
| `overrideReferences` | bool | `false` | When true, explicitly list every precompiled DLL dependency. |
| `precompiledReferences` | string[] | `[]` | DLL filenames (e.g., `"nunit.framework.dll"`). Requires `overrideReferences: true`. |
| `autoReferenced` | bool | `true` | Whether `Assembly-CSharp` automatically references this assembly. |
| `defineConstraints` | string[] | `[]` | Symbols that must be defined for the assembly to compile. Supports `!` and `||`. |
| `versionDefines` | object[] | `[]` | Define preprocessor symbols based on package/module versions. See below. |
| `noEngineReferences` | bool | `false` | Removes the implicit dependency on `UnityEngine`. |

---

## Version Defines and Define Constraints

These two features work together to handle optional dependencies and conditional compilation
at the assembly level.

### Version Defines

Version Defines let you define preprocessor symbols based on which packages or Unity versions
are installed. This is how you write code that *optionally* integrates with a package without
creating a hard dependency on it.

Each entry has three parts:
- **Resource**: The package, module, or "Unity" whose version you're checking.
- **Expression**: A version range using mathematical interval notation (e.g., `[1.0,2.0)`
  means >= 1.0 and < 2.0; `1.0` alone means >= 1.0).
- **Define**: The symbol to define when the condition is met.

**Example**: Optionally use TextMeshPro if installed.

In the `.asmdef` JSON:
```json
{
    "versionDefines": [
        {
            "name": "com.unity.textmeshpro",
            "expression": "1.0",
            "define": "HAS_TEXTMESHPRO"
        }
    ]
}
```

Then in your C# code:
```csharp
#if HAS_TEXTMESHPRO
using TMPro;
#endif

public class ScoreDisplay : MonoBehaviour
{
#if HAS_TEXTMESHPRO
    [SerializeField] private TMP_Text _label;
#else
    [SerializeField] private UnityEngine.UI.Text _label;
#endif
}
```

This pattern is essential for reusable libraries and UPM packages that shouldn't force
consumers to install optional dependencies.

### Define Constraints

Define Constraints determine whether Unity compiles the assembly at all. All constraints
must be satisfied (logical AND between lines), with `||` for OR within a line and `!` for
negation.

Common uses:
- `UNITY_INCLUDE_TESTS` — only compile this assembly when tests are enabled.
- `!ENABLE_IL2CPP` — exclude from IL2CPP builds.
- `UNITY_IOS || UNITY_ANDROID` — mobile-only assembly.

Symbols from Version Defines can be used as Define Constraints, allowing you to create
assemblies that only exist when a specific package is installed.

---

## Test Assembly Configuration

A test assembly requires references to:
- `nunit.framework.dll`
- `UnityEngine.TestRunner`
- `UnityEditor.TestRunner`

### Platform Settings by Test Type

| Test Type | Platform Setting | Why |
|---|---|---|
| **Edit-mode tests** | Editor-only (`includePlatforms: ["Editor"]`) | Run in the editor without entering Play mode. |
| **Play-mode tests** | All platforms (leave `includePlatforms` empty) | Run inside Play mode; need access to runtime assemblies. |

### Example: Edit-Mode Test Assembly

```json
{
    "name": "MyGame.Core.Tests",
    "rootNamespace": "MyGame.Core.Tests",
    "references": [
        "GUID:<core-assembly-guid>",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "noEngineReferences": false
}
```

Setting `autoReferenced: false` and `defineConstraints: ["UNITY_INCLUDE_TESTS"]` ensures
test code is never included in player builds.

---

## InternalsVisibleTo for Testing

When you split code into assemblies, the `internal` access modifier becomes meaningful —
`internal` types and members are only visible within the same assembly. This is good for
encapsulation, but creates a problem: your test assembly can't see `internal` types in the
assembly it's testing.

The solution is the `InternalsVisibleTo` attribute. By convention, place it in an
`AssemblyInfo.cs` file in the assembly's root folder:

```csharp
// AssemblyInfo.cs — inside MyGame.Core assembly
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("MyGame.Core.Tests")]
```

This grants the named test assembly access to all `internal` types and members, while
keeping them hidden from every other assembly. The string must match the **assembly name**
(the `name` field in the `.asmdef`), not the filename.

**Why this matters**: Without `InternalsVisibleTo`, developers tend to make everything
`public` so tests can reach it — which defeats the purpose of having assembly boundaries.
The attribute lets you keep proper encapsulation while still writing thorough tests.

You can also use `InternalsVisibleTo` to grant access to an editor assembly, so editor
tooling can access internal runtime types without making them public:

```csharp
[assembly: InternalsVisibleTo("MyGame.Core.Editor")]
[assembly: InternalsVisibleTo("MyGame.Core.Tests")]
```

Other useful assembly-level attributes you can place in `AssemblyInfo.cs`:
- `[assembly: UnityEngine.Scripting.Preserve]` — prevents Unity from stripping unused code
  from this assembly during builds.
- Standard .NET metadata attributes like `AssemblyCompany`, `AssemblyTitle`, `AssemblyCopyright`.

---

## Working with Third-Party Assets

One of the most common friction points when adopting assembly definitions: third-party
assets from the Asset Store that don't include their own `.asmdef`. These assets stay in
the predefined `Assembly-CSharp` assembly, which means your custom assemblies can't
reference them (custom assemblies cannot reference predefined assemblies — only the
reverse is allowed).

### Strategies

**1. Add an .asmdef to the third-party asset (simplest)**

Place a `.asmdef` file in the asset's root folder (and a separate editor-only `.asmdef`
in any Editor folders). Your custom assemblies can then reference it.

Pros: Quick, straightforward.
Cons: Asset Store updates may overwrite or conflict with your `.asmdef`. You'll need to
re-add it after updating the asset. Consider keeping a note or script to automate this.

**2. Leave the asset in Assembly-CSharp and use adapter/interface patterns**

Define interfaces in your Core assembly that describe the functionality you need. Implement
those interfaces in a thin adapter script that lives in `Assembly-CSharp` (or in a dedicated
"Bridges" assembly that references the third-party asset). Your custom assemblies depend only
on the Core interfaces.

```
Core (defines IAudioService)  ◄── Gameplay (uses IAudioService)
         ▲
Bridges (implements IAudioService using ThirdPartyAudio in Assembly-CSharp)
```

Pros: Cleanest architecture, survives asset updates, decouples your code from the asset.
Cons: More upfront work; requires dependency injection or a service locator to wire it up.

**3. Use .asmref to pull the asset into an existing assembly**

If the third-party asset is small and logically belongs with one of your assemblies, place
an `.asmref` in the asset's folder pointing at your assembly. This avoids creating yet
another assembly.

Pros: No additional assembly.
Cons: Only works if the asset's dependencies align with your assembly's dependencies.

### General advice

- Check whether the asset author provides a UPM or `.asmdef` version. Many popular assets
  have added assembly definitions in recent updates.
- When adding your own `.asmdef` to a third-party asset, name it distinctly
  (e.g., `ThirdParty.AssetName`) and disable Auto Reference if your own code only needs it
  in specific assemblies.

---

## UPM Package Structure

Unity Package Manager packages **require** assembly definitions — scripts in the `Packages/`
folder won't compile without them. If you're creating reusable code, internal tools, or
open-source libraries, structuring them as UPM packages is the standard approach.

### Standard Layout

```
com.yourcompany.packagename/
├── package.json                                    ← package manifest (required)
├── README.md
├── CHANGELOG.md
├── LICENSE.md
├── Runtime/
│   ├── YourCompany.PackageName.asmdef              ← runtime assembly
│   └── YourCode.cs
├── Editor/
│   ├── YourCompany.PackageName.Editor.asmdef       ← editor assembly, references Runtime
│   └── YourEditorCode.cs
├── Tests/
│   ├── Runtime/
│   │   ├── YourCompany.PackageName.Tests.asmdef    ← play-mode tests
│   │   └── RuntimeTests.cs
│   └── Editor/
│       ├── YourCompany.PackageName.Editor.Tests.asmdef ← edit-mode tests
│       └── EditorTests.cs
└── Samples~/                                       ← tilde hides from Project window
    └── ExampleUsage/
        └── ...
```

### Key rules for package assemblies

- **Naming convention**: `CompanyName.PackageName[.Editor][.Tests]`. This mirrors how Unity's
  own packages name their assemblies.
- **Editor assembly must reference Runtime**: If your editor code uses runtime types, add the
  runtime `.asmdef` as a reference. Runtime code must never reference the editor assembly.
- **Test assemblies**: Follow the same configuration as project test assemblies — set
  `overrideReferences: true`, add `nunit.framework.dll` to precompiled references, and add
  `UNITY_INCLUDE_TESTS` as a define constraint.
- **Samples use the tilde (`~`) suffix**: Unity ignores folders ending in `~` during normal
  compilation but exposes them through Package Manager's Samples UI for users to import.
- One `.asmdef` per package folder containing code is the minimum. Additional `.asmdef` files
  are needed if you create subfolders at the same level as the four standard folders (Runtime,
  Editor, Tests/Runtime, Tests/Editor).

---

## Incremental Adoption Strategy

For projects that already feel messy, this ordering gives the best return for effort:

1. **Core assembly first.** Extract shared interfaces, data types, and stable utility code.
   Easiest and highest-value first step — everything else will depend on it.

2. **Editor assemblies next.** Create explicit editor assemblies to prevent editor code
   from leaking into runtime builds. This catches real bugs immediately.

3. **Major systems one at a time.** Gameplay, UI, Networking — each becomes its own
   assembly when you're ready. Let compiler errors guide which scripts need to move or
   which references need to be added.

4. **Test assemblies.** Once runtime assemblies are stable, create corresponding test
   assemblies. Add `InternalsVisibleTo` attributes so tests can verify internal behaviour
   without making types public.

5. **Refine over time.** Circular dependency errors reveal where responsibilities need
   to be rethought. The graph gradually starts telling the truth about how the codebase
   is actually organised.

**Important**: During this transition, scripts remaining in `Assembly-CSharp` will still
recompile on every change to any custom assembly. The compile-time benefits only fully
materialise once all code is covered. Move quickly through the transition or accept the
temporary overhead — but don't stop halfway and wonder why compile times haven't improved.

---

## Debugging Tips

- **Check which assembly owns a script**: Select the script in the Project window and
  look at the **Assembly Information** section in the Inspector. It shows the compiled
  assembly and which `.asmdef` owns it.

- **"Type or namespace not found" after adding an asmdef**: The new assembly doesn't
  automatically see other assemblies. Add explicit references to every assembly it needs.

- **Build fails but editor works fine**: Likely editor-only code in a runtime assembly.
  Check for `UnityEditor` usages in assemblies that don't have `includePlatforms: ["Editor"]`.

- **Script changes don't trigger recompilation**: The script might not be claimed by any
  `.asmdef`. Check Assembly Information in the Inspector — it should show your custom
  assembly, not `Assembly-CSharp`.

- **"Assembly for reference does not exist on the file system"**: This usually means a
  referenced assembly has compilation errors of its own, or it has no scripts associated
  with it (empty assemblies aren't built). Check the full console log for upstream errors.

- **Compile times got worse after adding assembly definitions**: You're likely in partial
  adoption — some scripts are still in `Assembly-CSharp`, which recompiles whenever any
  custom assembly changes. Move all code into custom assemblies to fix this.

- **.asmdef not included in .unitypackage export**: Unity omits `.asmdef` files from
  exports because the dependency direction is reversed. Use `t:asmdef` in the Project
  search to find and manually include them.

- **Deleting Library/ScriptAssemblies to force a clean rebuild**: If something seems
  stuck or you suspect stale compilation state, delete the `Library/ScriptAssemblies`
  folder. Unity will do a full recompilation on next focus. You can also try
  Assets → Reimport All as a heavier-handed alternative.
