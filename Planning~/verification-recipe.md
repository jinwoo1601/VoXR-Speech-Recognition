> Moved verbatim on 2026-10-01 (branch `light-harness-adopt`) from `.claude/verification-bindings.md`, the pre-harness workflow's verification procedure; the `unity` binding's `verification` key cites this file.
> The file is now tracked at `Planning~/verification-recipe.md`, and `compile-check` reaches it through the `unity` binding's `verification` key rather than reading it directly. The body below is kept verbatim as it stood before the move, so these statements in it are superseded: that the file is gitignored with the rest of the workflow layer (`.claude/`); that `compile-check` reads it directly; that further detail lives in project memory `running-package-tests` (the maintainer's out-of-repo Claude memory, not this repo's `memory/`); and that the csproj is regenerated from `.claude/verification-bindings.md` (read: this file).
> On 2026-10-01 (branch `light-workspace-move`) this repository moved from `D:\Game Development` to `D:\Workspace`, and the host `VoXR TestGround` with it; the body's paths were updated in place, nothing else. The `NativeBridge~/build*` CMake caches still name the old source tree, which CMake refuses, so the next native build configures fresh: delete `NativeBridge~/build` before configuring — CMake 3.22 has no `--fresh`. *Edited 2026-10-14: a clause on the old Linux desktop cache dropped; the desktop harness now configures fresh into `build-windows/`.*

# Verification bindings — VoXR Speech Recognition

Moved verbatim from the project `CLAUDE.md` on 2026-08-23 (session-start token budget). This file is what the `compile-check` agent runs (it reads this file when present); the Android toolchain and NativeBridge build recipe travel with it because binding 2a references them. Gitignored with the rest of the workflow layer (`.claude/`). Not auto-loaded — keep it out of `.claude/rules/`.

## Verification bindings

What the `compile-check` agent runs, in this order. This is a bare UPM package — no `Assets/` or `ProjectSettings/` — so nothing compiles or tests standalone; all Unity verification goes through the host project.

1. **Unity tests (authoritative):** host project `D:\Workspace\VoXR TestGround` (Unity **6000.4.7f1**), which references this package by local path (`file:D:/Workspace/VoXR-Speech-Recognition` in its manifest) — the checked-out working tree is what's tested. Procedure (full detail in project memory `running-package-tests`):
   1. Unity editor must be closed (`<host>/Temp/UnityLockfile` gone).
   2. Rename `Tests~` → `Tests` in this repo; ensure `"testables": ["com.jinwoo1601.voxr"]` in the host manifest.
   3. Run **both** platforms — `Tests/Editor` is EditMode, `Tests/Runtime` is PlayMode (most parser/command tests are PlayMode; an EditMode-only run misses them):
      `"/c/Program Files/Unity/Hub/Editor/6000.4.7f1/Editor/Unity.exe" -runTests -batchmode -projectPath "D:\Workspace\VoXR TestGround" -testPlatform <EditMode|PlayMode> -testResults "<win path>.xml" -logFile "<win path>.log"` — no `-quit`; takes minutes on first import. Green = NUnit result XML with `failed="0"`.
   4. Revert: `Tests` → `Tests~`, delete Unity-generated `.meta` orphans (incl. root `Tests.meta`), restore the manifest.
2. **NativeBridge (only when `NativeBridge~/` changed):**
   a. **arm64 build:** CMake/Ninja build per *Building NativeBridge* below. Green = `libvosk-bridge.so` builds with no errors.
   b. **Desktop harness (Windows-native, Git Bash, from the project root — MSVC from Visual Studio 2022 Community through CMake's "Visual Studio 17 2022" generator; the Unity 6000.3.7f1 Android SDK's `cmake.exe` by absolute path):** `cd NativeBridge~ && ls -d 'C:/Program Files/Microsoft Visual Studio/2022/Community' 'C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe' >/dev/null && 'C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe' --preset windows-x64 '-DCMAKE_GENERATOR_INSTANCE=C:/Program Files/Microsoft Visual Studio/2022/Community' && 'C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe' --build --preset windows-x64 && ./build-windows/bin/vosk-bridge-harness.exe --model vendor/vosk-model-small-en-us-0.15 --fixtures "../Tests~/Fixtures/audio/tts" --manifest harness/expectations.json`. The leading `ls -d` fails naming any toolchain path that has moved; the two `cmake.exe` calls configure (preset `windows-x64`, pinned to the VS 2022 Community instance) and build Release; the harness then runs. No argument is one Git Bash path conversion rewrites, so the command behaves the same with or without `MSYS_NO_PATHCONV`. Green = exit 0 (16/16 fixture transcripts match the committed baseline); exit 1 = a transcript mismatch, which stops for the human's ruling and is never re-pinned; exit 2 = an operational error. One-time provisioning (gitignored `NativeBridge~/vendor/`: the pinned `vosk-win64-0.3.45` prebuilt + the small model) per `NativeBridge~/harness/README.md`; ratified as the standing check at Tier C's G2 (2026-07-30, design §12), its host Windows-native under Amendment A2 (`Planning~/design-docs/automated-verification.md` §0a).
   c. **On a bridge ABI change:** the rebuilt arm64 `.so` must be committed to `Runtime/Plugins/Android/arm64-v8a/` (the `build.sh` copy step — the shipped plugin, not just the build output; a PR #51 review finding).
3. **`Samples~` compile gate (run whenever `Runtime/` public API or any `Samples~/` source changes):** out-of-band `dotnet build` of `Runtime/**/*.cs` + `Samples~/**/*.cs`, per *Compiling Samples~* below. **This gate is the only thing in this project that compiles `Samples~` at all** — there is no `.asmdef` under `Samples~/` and the samples are not imported into the host project, so step 1 can be fully green while the shipped samples are broken by an API change. Green = `Build succeeded` / `0 Error(s)`. It does **not** need the Unity editor lock (it only reads `Library/ScriptAssemblies`), so it can run with the editor open.
4. **Human-only (needs the Quest headset — report as deferred, never claim it):** on-device Quest verification for mic capture, MonoBehaviour lifecycle behavior, and any native-bridge change. `OnApplicationPause` is the only lifecycle hook this package implements (`Runtime/VoxrPushToTalkController.cs:133`); its *state machine* is covered by PlayMode tests under step 1, so what stays human is whether Android actually **delivers** the callback on doff/system-menu. There is no `OnApplicationFocus` handler anywhere in this package. This paragraph is permanent: Tier D (the automated headset rig that would have replaced most of it) was dropped at the 2026-08-11 G1 re-lock, so no `device-check` binding exists or is planned.

## Android Toolchain Paths

All tools are inside the Unity 6000.3.7f1 installation:

- **NDK**: `C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\PlaybackEngines\AndroidPlayer\NDK`
- **Platform Tools (adb)**: `C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools`
- **CMake**: `C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\cmake\3.22.1\bin\cmake.exe`
- **Ninja**: `C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\cmake\3.22.1\bin\ninja.exe`

### Git Bash usage

From Git Bash, call the Android SDK's `.exe` tools by their `/c/…` path:

```bash
# adb
"/c/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe"

# logcat (filtered for vosk-bridge)
"/c/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe" logcat -s "vosk-bridge:*" "Unity:*"
```

A plain Git Bash rewrites `/`-prefixed arguments passed to a Windows program, so a device path such as `/sdcard/…` given to `adb` arrives mangled; prefix such a command with `MSYS_NO_PATHCONV=1` (Claude Code's Git Bash already sets it). CMake is in *Building NativeBridge* below and step 2b.

### Building NativeBridge

```bash
CMAKE="/c/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe"
NDK_WIN="C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/NDK"

"$CMAKE" -B "D:/Workspace/VoXR-Speech-Recognition/NativeBridge~/build" \
         -S "D:/Workspace/VoXR-Speech-Recognition/NativeBridge~" \
         -DCMAKE_TOOLCHAIN_FILE="$NDK_WIN/build/cmake/android.toolchain.cmake" \
         -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-27 -DANDROID_STL=c++_shared \
         -DCMAKE_BUILD_TYPE=Release \
         -DCMAKE_MAKE_PROGRAM="C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/ninja.exe" \
         -G Ninja

"$CMAKE" --build "D:/Workspace/VoXR-Speech-Recognition/NativeBridge~/build" --config Release -j 4
```


## Compiling Samples~

Binding 3. Establishes that the seven shipped sample scripts still compile against the package's
current public API. Verified working 2026-10-14 (native .NET SDK 10.0.300 in Git Bash, Unity 6000.4.7f1).

**The csproj is generated, never committed.** It lives at
`.scratch/samples-compile/SamplesCompile.csproj` under the project root (`.scratch/` is gitignored)
and names the package sources, the Unity data folder and the host's assemblies by absolute path
in drive-letter form (`C:/…`, `D:/…`) — MSBuild reads the file, so `/c/…` forms do not resolve
there — so nothing is added to the repo; recreate it from the listing each time.

### Command

```bash
SP=".scratch/samples-compile"   # from the project root
mkdir -p "$SP"
# ... write SamplesCompile.csproj (listing below) ...
cd "$SP" && dotnet build SamplesCompile.csproj -v m -nologo
```

**Green** = `Build succeeded` and `0 Error(s)`. One warning is expected and is *not* a failure:
`Runtime/VoxrSpeechRecogniser.cs(359,13): warning CS0618: 'PermissionCallbacks.PermissionDeniedAndDontAskAgain' is obsolete`.
Report errors as `file:line` from the `Samples~/...` or `Runtime/...` absolute paths in the output.

A missing input stops the build before compiling — `VoxrCheckInputs` fails naming the missing path.

### Reference set, and how to relocate it if the Unity version changes

Three sources, all referenced with `<Private>false</Private>`:

1. **BCL facade** — `<UnityData>/NetStandard/ref/2.1.0/netstandard.dll`, with `NoStdLib`/
   `NoStandardLibraries`/`DisableImplicitFrameworkReferences` all set, so the compile sees Unity's
   BCL surface rather than the .NET 10 SDK's.
2. **Engine + all engine modules** — glob `<UnityData>/Managed/UnityEngine/*.dll`, excluding
   `UnityEditor*.dll` (see *player shape* below). 293 files in this install; globbing avoids
   hand-maintaining a module list.
3. **Package assemblies the samples use** — `UnityEngine.UI.dll` (uGUI, for `UnityEngine.UI` /
   `UnityEngine.EventSystems`) and `Unity.InputSystem.dll`, taken from the **host project's**
   `D:\Workspace\VoXR TestGround\Library\ScriptAssemblies\`. These are compiled per-project,
   so they exist only after the host has imported at least once. No TextMeshPro reference is
   needed — no sample uses TMP.

`<UnityData>` is `C:/Program Files/Unity/Hub/Editor/<version>/Editor/Data`. **If the editor
version changes**, update the `UnityData` property and the `UNITY_6000_4_7` / `UNITY_*_OR_NEWER`
defines. The authoritative source for both the reference set and the define list is the host
project's Unity-generated `D:\Workspace\VoXR TestGround\Assembly-CSharp.csproj` — read its
`<DefineConstants>` and `<Reference>` hint paths and mirror them.

### Player shape: UNITY_EDITOR is deliberately not defined

`Runtime/` is heavily `#if UNITY_EDITOR`-guarded (dozens of sites in `VoxrCommandParser.cs` and
`VoxrCommandRecogniser.cs`). This gate compiles **without** `UNITY_EDITOR`, which is the
player-build shape — the configuration that actually ships and the one no other check in this file
exercises (step 1 runs in-editor). Consequence to keep in mind: editor-only code paths are *not*
covered by this gate.

### Mutation verification is required, not optional

The `Samples~` glob contains a `~` and the samples have no asmdef, so a misconfigured csproj
produces a **green that compiled nothing**. Never report this gate green without first proving it
can go red:

1. Insert a deliberate error into one sample (e.g. a call to a method that does not exist).
2. Rebuild; confirm the failure names **that file and line**.
3. `git checkout -- <the sample>`; rebuild; confirm green returns.

A cheap standing sanity check on the compile set itself:

```bash
cd "$SP" && dotnet build SamplesCompile.csproj -nologo -t:CoreCompile -v diag 2>&1 \
  | tr '\\' '/' | grep -oiE 'D:/Workspace/VoXR-Speech-Recognition/Samples~/[^ ";]*\.cs' | sort -fu | wc -l
```

must print **7**.

*Verified 2026-09-16:* inserting `MutationCanaryMethodThatDoesNotExist();` at
`Samples~/CommandRecognition/TacticalSceneController.cs:56` produced
`error CS0103: The name 'MutationCanaryMethodThatDoesNotExist' does not exist in the current context`
at that exact file:line, and green returned after revert.

### SamplesCompile.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!-- Out-of-band compile gate for Samples~ (VoXR Speech Recognition).
       Nothing here is committed; regenerate from .claude/verification-bindings.md. -->

  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <OutputType>Library</OutputType>
    <AssemblyName>VoXR.SamplesCompileCheck</AssemblyName>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <EnableDefaultItems>false</EnableDefaultItems>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <NoStdLib>true</NoStdLib>
    <NoStandardLibraries>true</NoStandardLibraries>
    <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <ProduceReferenceAssembly>false</ProduceReferenceAssembly>
    <!-- Unity ships module DLLs whose transitive closure we do not fully model. -->
    <MSBuildWarningsAsMessages>MSB3245;MSB3277</MSBuildWarningsAsMessages>
    <NoWarn>0169;0414;0649;CS1701;CS1702</NoWarn>
  </PropertyGroup>

  <PropertyGroup>
    <UnityData>C:/Program Files/Unity/Hub/Editor/6000.4.7f1/Editor/Data</UnityData>
    <PkgRoot>D:/Workspace/VoXR-Speech-Recognition</PkgRoot>
    <HostAsm>D:/Workspace/VoXR TestGround/Library/ScriptAssemblies</HostAsm>
  </PropertyGroup>

  <!-- Player-build shape: UNITY_EDITOR is deliberately NOT defined. -->
  <PropertyGroup>
    <DefineConstants>UNITY_6000_4_7;UNITY_6000_4;UNITY_6000;UNITY_5_3_OR_NEWER;UNITY_5_4_OR_NEWER;UNITY_5_5_OR_NEWER;UNITY_5_6_OR_NEWER;UNITY_2017_1_OR_NEWER;UNITY_2017_2_OR_NEWER;UNITY_2017_3_OR_NEWER;UNITY_2017_4_OR_NEWER;UNITY_2018_1_OR_NEWER;UNITY_2018_2_OR_NEWER;UNITY_2018_3_OR_NEWER;UNITY_2018_4_OR_NEWER;UNITY_2019_1_OR_NEWER;UNITY_2019_2_OR_NEWER;UNITY_2019_3_OR_NEWER;UNITY_2019_4_OR_NEWER;UNITY_2020_1_OR_NEWER;UNITY_2020_2_OR_NEWER;UNITY_2020_3_OR_NEWER;UNITY_2021_1_OR_NEWER;UNITY_2021_2_OR_NEWER;UNITY_2021_3_OR_NEWER;UNITY_2022_1_OR_NEWER;UNITY_2022_2_OR_NEWER;UNITY_2022_3_OR_NEWER;UNITY_2023_1_OR_NEWER;UNITY_2023_2_OR_NEWER;UNITY_2023_3_OR_NEWER;UNITY_6000_0_OR_NEWER;UNITY_6000_1_OR_NEWER;UNITY_6000_2_OR_NEWER;UNITY_6000_3_OR_NEWER;UNITY_6000_4_OR_NEWER;PLATFORM_ANDROID;UNITY_ANDROID;UNITY_ANDROID_API;ENABLE_INPUT_SYSTEM;ENABLE_AUDIO;ENABLE_MICROPHONE;ENABLE_VR;ENABLE_UNITYEVENTS;ENABLE_PROFILER;ENABLE_MONO;NET_STANDARD_2_0;NET_STANDARD;NET_STANDARD_2_1;NETSTANDARD;NETSTANDARD2_1;DEBUG;TRACE;UNITY_ASSERTIONS;CSHARP_7_OR_LATER;CSHARP_7_3_OR_NEWER</DefineConstants>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="$(PkgRoot)/Runtime/**/*.cs" />
    <Compile Include="$(PkgRoot)/Samples~/**/*.cs" />
  </ItemGroup>

  <ItemGroup>
    <!-- BCL facade Unity compiles scripts against. -->
    <Reference Include="$(UnityData)/NetStandard/ref/2.1.0/netstandard.dll">
      <Private>false</Private>
    </Reference>
    <!-- Engine + every engine module; UnityEditor* excluded on purpose (player shape). -->
    <Reference Include="$(UnityData)/Managed/UnityEngine/*.dll"
              Exclude="$(UnityData)/Managed/UnityEngine/UnityEditor*.dll">
      <Private>false</Private>
    </Reference>
    <!-- Package assemblies the samples use, built by the host project. -->
    <Reference Include="$(HostAsm)/UnityEngine.UI.dll">
      <Private>false</Private>
    </Reference>
    <Reference Include="$(HostAsm)/Unity.InputSystem.dll">
      <Private>false</Private>
    </Reference>
  </ItemGroup>

  <!-- Fail before compiling, naming any missing input; nothing falls back. -->
  <Target Name="VoxrCheckInputs" BeforeTargets="BeforeBuild">
    <Error Condition="!Exists('$(PkgRoot)/Samples~')" Text="Missing package samples folder: $(PkgRoot)/Samples~" />
    <Error Condition="!Exists('$(UnityData)/NetStandard/ref/2.1.0/netstandard.dll')" Text="Missing Unity BCL facade: $(UnityData)/NetStandard/ref/2.1.0/netstandard.dll" />
    <Error Condition="!Exists('$(UnityData)/Managed/UnityEngine/UnityEngine.CoreModule.dll')" Text="Missing Unity engine module: $(UnityData)/Managed/UnityEngine/UnityEngine.CoreModule.dll" />
    <Error Condition="!Exists('$(HostAsm)/UnityEngine.UI.dll')" Text="Missing host assembly: $(HostAsm)/UnityEngine.UI.dll (import the host project once)" />
    <Error Condition="!Exists('$(HostAsm)/Unity.InputSystem.dll')" Text="Missing host assembly: $(HostAsm)/Unity.InputSystem.dll (import the host project once)" />
  </Target>

</Project>
```

## Parser A/B rig — DocCheck

Checks every number the scoring docs publish against the real parser (`Planning~/features/coverage-in-selection/ab-rig/`, `DocCheck.Run` via `--doccheck`). Not a bound Command; the one procedure for whoever runs it. From the project root in Git Bash, with the .NET SDK 10 on PATH (`Rig.csproj` targets `net10.0`):

```bash
env -u MSYS_NO_PATHCONV bash "Planning~/features/coverage-in-selection/ab-rig/stage.sh" WORKTREE .scratch/doccheck && cd .scratch/doccheck && dotnet build -v q --nologo && dotnet run --no-build -- --doccheck
```

`stage.sh` copies the checked-out parser sources and the rig into `.scratch/doccheck/` (emptied first, so every build is fresh) and prints `STAGED_FROM`; `dotnet` builds and runs the rig natively. Green = exit 0, no line starting `FAIL`, and the closing `<n>/<n> doc claims verified against the real parser.` with both numbers equal.

`env -u MSYS_NO_PATHCONV` applies to `stage.sh` alone, which passes MSYS-form paths to native `git -C`; those fail when path conversion is off, as Claude Code's Git Bash sets it, and in a plain Git Bash the prefix changes nothing. `stage.sh` is not edited (design `windows-native-toolchain` §1). No other argument starts with `/`, so the line behaves the same in either shell.
