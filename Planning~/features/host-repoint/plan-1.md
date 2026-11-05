# Phase 1 — persisted plan (2026-11-05)

Scope: Phase 1 as the row reads it — the recipe's listing derives `PkgRoot` from the csproj's own location, the sanity check reads the root from `pwd -W`, the binding's Command 4 line is not edited, and the green is Command 4, the 7-sample count and a canary red then green, all in the one checkout — taking on the recipe prose the change makes false (:73-76, :153), the recipe's git-write revert (:131), a `PkgRoot` read and a wrong-root guard check, dropping nothing; F6 from a worktree stays Phase 5's.

## Files to change

| Path | Change | Why |
|---|---|---|
| `Planning~/verification-recipe.md` | modify (`doc-writer`): :73-76, :131, :136, :137, :153, :177 only | F6; *PkgRoot*; :131 because agents run no git writes |
| `.scratch/samples-compile/SamplesCompile.csproj` | create (PM, S2), extracted from the edited listing; never committed | F6, F8 |
| `.scratch/host-repoint-p1/pre/TacticalSceneController.cs` | create (PM, S5): the sample's pre-mutation bytes | the restore source |
| `.scratch/host-repoint-p1/wrong-root/x/SamplesCompile.csproj` | create (PM, S4c): a copy of the csproj | the guard check |
| `Samples~/CommandRecognition/TacticalSceneController.cs` | canary inserted (`code-writer`, S6), removed (`code-writer`, S8); net none, byte-checked | F6's mutation |
| `Planning~/features/host-repoint/plan-1.md` | create | process-docs |
| `Planning~/features/host-repoint/architecture.md` | `## Build plan` link line | process-docs |
| `.claude/bindings/unity.md` and its composed skill copies, `Planning~/host-run/`, `NativeBridge~/`, `Runtime/`, `Tests~/`, TestGround | none | the row; Phases 2-5 |

## Contracts and interfaces

**Names.** ROOT is the tree root as `pwd -W` prints it, here the hub `D:/Workspace/VoXR-Speech-Recognition`; SP is `.scratch/samples-compile`; SAMPLE is `Samples~/CommandRecognition/TacticalSceneController.cs`; PRE is `.scratch/host-repoint-p1/pre/TacticalSceneController.cs`; TG is `D:/Workspace/VoXR TestGround`. Recipe line numbers are pre-edit; doc-writer edits bottom-up; every line not named stays byte-unchanged; every line stays CRLF. Paths compare per the binding's Path form.

**`PkgRoot` (recipe :177), replaced whole by:** `    <PkgRoot>$([System.IO.Path]::GetFullPath('$(MSBuildThisFileDirectory)../..'))</PkgRoot>`. It evaluates to the directory two levels above the csproj — absolute, no `..` segment, no trailing separator — which is ROOT for a csproj at SP under ROOT. `UnityData` (:176), `HostAsm` (:178), the globs (:187-188) and the guard (:212) are unchanged; the guard's `!Exists('$(PkgRoot)/Samples~')` fires on any location whose grandparent holds no `Samples~`, naming that path.

**Sanity check (recipe :136-137), replaced whole by two lines:** :136 `(R="$(pwd -W)" && cd "$SP" && dotnet build SamplesCompile.csproj -nologo -t:CoreCompile -v diag 2>&1 \` and :137 `  | tr '\134' '/' | grep -oiE "$R/Samples~/[^ \";]*\.cs" | sort -fu | wc -l)`. R is read inside the subshell before the `cd`, from the root the block runs from; the token shape is today's, the literal hub replaced by `$R`. :133 and :140 ("must print **7**.") unchanged.

**Other recipe lines.**
- :73-76: the paragraph opened by :72's bold lead (kept) must say: the csproj lives at `.scratch/samples-compile/SamplesCompile.csproj` under the root of the tree it compiles (`.scratch/` is gitignored); `PkgRoot` is derived from the file's own location, two directories up, so the listing is written verbatim in any tree and compiles that tree, no path in it edited per tree; the Unity data folder and the host's assemblies are named by absolute path in drive-letter form (`C:/…`, `D:/…`), since MSBuild reads the file and `/c/…` forms do not resolve there; recreate it from the listing each time. Wrapped as its neighbours, kept at :72-76 where it fits; any added line is named in doc-writer's report.
- :131, item 3, replaced whole by one line saying, in this order: restore the sample from a copy of its bytes taken under `.scratch/` before step 1 — not `git checkout`, a git write agents do not run — and confirm it byte-identical (`cmp`); rebuild; confirm green returns.
- :153 → `       Nothing here is committed; regenerate from Planning~/verification-recipe.md. -->`

**Steps**, each from ROOT in Git Bash:
- S1 `doc-writer`: the recipe edits above; nothing else.
- S2 PM: T0 = `git -C "D:/Workspace/VoXR TestGround" status --short`; `grep -c '^<Project' "Planning~/verification-recipe.md"` prints 1; then `SP=".scratch/samples-compile"; mkdir -p "$SP" && sed -n '/^<Project Sdk=/,/^<\/Project>/p' "Planning~/verification-recipe.md" > "$SP/SamplesCompile.csproj"`; the file's first line is `<Project Sdk="Microsoft.NET.Sdk">`, its last `</Project>`, and it holds the :177 line above once.
- S3 `compile-check`, its brief naming Command 4 alone: binding :9's command verbatim.
- S4 PM: (a) `(cd .scratch/samples-compile && dotnet msbuild SamplesCompile.csproj -nologo -getProperty:PkgRoot)` prints ROOT; (b) `SP=".scratch/samples-compile"`, then the edited recipe's :136-137 verbatim, prints 7; (c) `W=".scratch/host-repoint-p1/wrong-root/x"; mkdir -p "$W" && cp .scratch/samples-compile/SamplesCompile.csproj "$W/" && (cd "$W" && dotnet build SamplesCompile.csproj -v m -nologo)` exits non-zero with `Missing package samples folder:` naming ROOT's `.scratch/host-repoint-p1/Samples~`, and no `error CS` line.
- S5 PM: `mkdir -p .scratch/host-repoint-p1/pre && cp "Samples~/CommandRecognition/TacticalSceneController.cs" .scratch/host-repoint-p1/pre/`, then `cmp` of the two exits 0.
- S6 `code-writer` dispatch A, files in scope SAMPLE alone, verification `not run — the gate runs the suite`: one Edit anchored on :54-56 (`void Awake()`, `{`, `if (targets == null) return;`) inserting `        MutationCanaryMethodThatDoesNotExist();` (8 spaces) as a new line 56, the file's CRLF kept.
- S7 `compile-check`, Command 4 alone: red, `error CS0103` naming `MutationCanaryMethodThatDoesNotExist` at SAMPLE under ROOT, line 56.
- S8 `code-writer` dispatch B, same scope and verification: one Edit removing exactly that line, the inverse of S6.
- S9 PM: `cmp "Samples~/CommandRecognition/TacticalSceneController.cs" .scratch/host-repoint-p1/pre/TacticalSceneController.cs` exits 0; `git status --short -- Samples~/` prints nothing.
- S10 `compile-check`, Command 4 alone: green.
- S11 PM: T1 = TG's `git status --short`, equal to T0; `git status --short` names no changed path but the recipe and `memory/`; `git diff -- Planning~/verification-recipe.md`, read hunk by hunk, touches only S1's lines; the recipe's CRLF line count equals its line count.

**Stops**, never worked around: S7 green (the gate compiled nothing) — S8-S9 still run, then stop; S9 a miss — stop for the human, who restores SAMPLE from PRE, nothing committed meanwhile; S3 or S10 red, S4 (a) not ROOT, (b) not 7, or (c) not firing — stop for the human with the output, a runtime claim proved false, corrected by plan correction.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| `GetFullPath` on `$(MSBuildThisFileDirectory)../..` | the raw `../..` concatenation; `[MSBuild]::NormalizeDirectory` | raw leaves `..` in the compile list and NormalizeDirectory a trailing separator, so `$(PkgRoot)/Samples~` prints `//`; either way the grep misses |
| R read inside the subshell before `cd` | a variable set beside `SP` at :81 | one line in, one out; the block reads its root where it runs |
| `$R` interpolated into today's ERE | a fixed-string second grep | keeps the token shape that counts 7; a root holding an ERE metacharacter other than `.` is unsupported, and neither ROOT nor Phase 5's worktree holds one |
| csproj extracted by `sed` from the edited recipe | written by hand | the file is the recipe as edited, byte for byte |
| Canary by two `code-writer` dispatches, restore by the inverse Edit, checked by PM `cmp` against PRE | `git checkout` (a git write); the PM writing `Samples~` (outside `memory/`, `.scratch/`); `compile-check` (never edits) | the one agent that writes source; PRE makes a miss recoverable |
| Recipe :131 rewritten | left | otherwise every agent run must skip a step the recipe prescribes |
| Binding :9 not edited | an edit | the brief's reading of "unchanged in form"; the line stays true: the csproj is still generated per the recipe into `.scratch/samples-compile/` |
| A `-getProperty` read and a wrong-root build | the 7 count alone | F6 checks that `PkgRoot` reads the root; the guard is what catches a misplaced csproj |

## Acceptance criteria

1. [ ] The recipe changes at :73-76, :131, :136, :137, :153, :177 only, every line CRLF; no binding, script, `Runtime/`, `Samples~/`, `Tests~/` or TG file changes (the row; S11).
2. [ ] The csproj at SP is the edited listing (S2) (F6).
3. [ ] `PkgRoot` evaluates to ROOT (S4a) (F6).
4. [ ] Command 4 green: `Build succeeded`, `0 Error(s)`, the one CS0618 warning or `0 Warning(s)` (S3) (F8; F6).
5. [ ] The sanity check prints 7, read against `pwd -W` (S4b) (the row; F6).
6. [ ] The guard fires on a wrong root (S4c) (*PkgRoot*).
7. [ ] The canary turns Command 4 red at SAMPLE line 56, CS0103 (S7) (F6).
8. [ ] SAMPLE byte-identical to PRE, no `Samples~/` status (S9) (the row).
9. [ ] Command 4 green after the restore (S10) (F8).
10. [ ] TG's `git status --short` unchanged, no lock taken (S11) (H5; F6).
11. [ ] No agent step runs a git write; nothing committed by an agent (CONSTITUTION).

## Open questions that block implementation

None; the binding's Command 4 line stays true after the change, so none is listed.

## Blast radius

- Every later Command 4 and sanity run, in any tree, reads the new listing and block; the binding line and its composed copies are untouched.
- An added line in :72-76 shifts later recipe lines, staling the cites in `architecture.md` :35, :66, :109 and `parallel-trees.md` :144 until reconcile.
- `.scratch/samples-compile/` and `.scratch/host-repoint-p1/` appear (gitignored).
- TG: `Library/ScriptAssemblies`' two DLLs read, unlocked.
- Phase 3's Command 4 and Phase 5's worktree F6 rest on this listing.

## Runtime claims

- a (S2): `sed` in Git Bash passes the recipe's CRLF through, and the range is the listing alone.
- b (S3): `GetFullPath` returns ROOT in backslash form with no trailing separator, and MSBuild expands `$(PkgRoot)/Runtime/**/*.cs` and `$(PkgRoot)/Samples~/**/*.cs` from it with no NuGet download.
- c (S4a): SDK 10's `dotnet msbuild -getProperty` prints the evaluated value without building.
- d (S4b): the diag compile list, after `tr`, names each sample as ROOT's `/Samples~/…` with no `..`, matching `$R` case-insensitively.
- e (S4c): `VoxrCheckInputs` stops the misplaced build before compiling, naming the path.
- f (S6, S8): an inserted line removed by the inverse Edit leaves SAMPLE's bytes as they were.
- g (S7): the canary's error names SAMPLE line 56.
- h (S10): the restore changes SAMPLE's timestamp, so the compile reruns and shows CS0618; either green form holds.

Hazards: between S6 and S9 the hub's SAMPLE carries the canary, so no commit and no other session's Command 4 until S9 passes, and a stopped S8 leaves the restore from PRE to the human; Command 4 reads TG's `Library/ScriptAssemblies` unlocked, so a concurrent Unity run there can give a transient file error (`architecture.md` :96), rerun, never fixed.
