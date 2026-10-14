# Phase 1 — persisted plan (2026-10-14)

**Phase 1 - the binding, the recipe and the header comment** (`architecture.md` `## Build plan`). Unsplit. Discharges F1, F2, F5 (F7 confirms at Phase 2), F6, F8, both §6 NFRs for Command 4 and DocCheck; runs F9's diff guard. Decisions: the human's approved plan, 2026-10-14. `architecture.md` lines are cited as of 2026-10-14; if an edit shifts them, the named section governs.

## Files to change

| Path | Change | Why |
|---|---|---|
| `.claude/bindings/unity.md` | modify (:9, :12, :15, :17 only) | F1, F5; *Data contract* binding lines; decision A |
| `Planning~/verification-recipe.md` | modify (:3, :17, :24, :35-48, `## Compiling Samples~` :68-209); section appended after :209 | F1, F2, F6; register :47-69 |
| `NativeBridge~/include/vosk_api.h` | modify (:3 only) | F8; register *F8's wording* |

## Contracts and interfaces

**Conventions.** Git Bash, from the project root `D:/Workspace/VoXR-Speech-Recognition`. Every line number is pre-edit; apply the edits bottom-up, or by anchor text. Each replaced line is one line in, one out; every line not named stays byte-unchanged. No WSL or `/mnt/` text is written; paths in `C:/…`, `D:\…` or Git Bash form only. Each DocCheck line, and any gate line containing `cd`, runs in a subshell `( … )` from the project root.

**Step 1 - binding.** :9, :12, :15 are replaced whole by `architecture.md` :86, :88, :90, blockquote marker `> ` removed, leading `- ` kept. :17 is replaced whole by :92 the same way, with decision A's clause appended at its very end: `` ; the working-dir read `pwd -W` already prints this form (`D:/…`) ``. :8, :16 and every other line untouched.

**Step 2 - recipe**, listed in file order:
1. :3, per register :65: delete the parenthetical `` (and `build-desktop` in WSL) `` and close the line with the register's italic *Edited <date>: …* sentence, `<date>` the day of the edit.
2. :17: `/mnt/c/` → `/c/`, nothing else (`architecture.md` :94). The word becomes `"/c/Program Files/Unity/Hub/Editor/6000.4.7f1/Editor/Unity.exe"`; :94's `…/Editor/Data/…` sketch misnames the tail; its "nothing else" governs.
3. :24, the parenthetical as :94 gives (register :69).
4. :35-48 (heading through closing fence; blank :49 stays) → :96's `### Git Bash usage`: heading; intro line; a `bash` fence holding the `# adb` entry, a blank line, the `# logcat …` entry; after it, the device-path paragraph and the CMake pointer. No cmake entry.
5. `## Compiling Samples~` (heading :68; the doc's ":70-209" is its body), per :98:
   - :71 → :98's text; `<run date>` is set, or confirmed, after gate 3 passes, as the date of that green Command 4 run.
   - :73-76 → the bold lead `**The csproj is generated, never committed.**` kept, then :98's content for :73-76, its first sentence capitalised ("It lives at …").
   - :80-85 → one `bash` fence: :98's `SP=` line, `mkdir -p "$SP"`, :83's comment unchanged, the `cd "$SP" && dotnet build …` line.
   - After :89: :98's one line on `VoxrCheckInputs`, after a blank line.
   - :107's first sentence → :98's `<UnityData>` sentence; the rest of :107-111 stays.
   - :134 kept; :135 → `  | tr '\\' '/' | grep -oiE 'D:/Workspace/VoXR-Speech-Recognition/Samples~/[^ ";]*\.cs' | sort -fu | wc -l` (register :53's tail); :138 "must print **7**." unchanged.
   - :174-176 → the three values of register :49, element names and indent unchanged.
   - Between :207 (blank, kept) and :208 `</Project>`: :98's comment, then `<Target Name="VoxrCheckInputs" BeforeTargets="BeforeBuild">` holding five self-closing `<Error Condition="!Exists('<path>')" Text="<text>" />`, in register :51's order, each `<text>`: `Missing package samples folder: $(PkgRoot)/Samples~`; `Missing Unity BCL facade: <path>`; `Missing Unity engine module: <path>`; `Missing host assembly: <path> (import the host project once)` for each `$(HostAsm)` input — `<path>` the condition's path, as written; then one blank line: one blank line each side, never two.
   - The comment at :150-151 (:151 names the absent `.claude/verification-bindings.md`) stays: out of scope, noted.
6. After :209 (end of file): a blank line, then :102's `## Parser A/B rig — DocCheck`: its description and prerequisite sentences; a `bash` fence holding register :57's command line exactly; then the rest of :102's text.

**Step 3 - `vosk_api.h` :3** → register :71's comment, exactly. :4's "Do not modify" is about the declarations; a comment leaves the ABI unchanged.

**Stops for the human:** a non-empty symbol diff; harness exit 1 or 2; a NuGet download needed (a human `!` step); a runtime claim proving false — the fix is reported and PM-ruled, the text changed before the phase closes, logged in `## As-built deltas` at reconcile.

**Commit set:** `plan-1.md` and the `architecture.md` link line at plan time (phase-pickup's RECORD & COMMIT); at implement, the three edited files, plus `architecture.md` if the run changes it. No CHANGELOG line (dev tooling). Nothing in `.scratch/` is committed.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| :17 carries clause A (approved departure) | :92's text as written | doctor's V21 warns every session otherwise; `pwd -W` does print `D:/…` |
| `.scratch/samples-compile/` emptied first | Build over the WSL-made `obj/`, `bin/`, `.bak` | Makes "restores with no NuGet download" a clean test |
| Committed `.so` restored by `git checkout` | Recommit | A comment changes no ABI (register :71) |
| Fixed `<Error>` texts | Writer's wording | The negative run greps one wording |

## Acceptance criteria

The gate, run by the verification agent (never the main thread), in this order:

0. [ ] `find .scratch/samples-compile -mindepth 1 -delete`; then `.scratch/samples-compile/SamplesCompile.csproj` written from the new recipe listing, verbatim. (F1)
1. [ ] `ls` on the recipe's `### Git Bash usage` adb path exits 0. (F6)
2. [ ] Command 3 from binding :8 verbatim, both halves. Arm64: per recipe `### Building NativeBridge`; symbol list taken as `windows-native-harness/plan-2.md` criterion 1 does, into `.scratch/windows-native-dotnet-fixtures-phase-1-symbols-post.txt`; its `diff` against `.scratch/windows-native-harness-phase-1-symbols-committed.txt` empty; if `git status --short` lists `Runtime/Plugins/Android/arm64-v8a/libvosk-bridge.so`, `git checkout --` it; no recommit. Desktop: exits 0 with 16/16. (F8)
3. [ ] Command 4 verbatim from binding :9: `Build succeeded`, `0 Error(s)`, warning CS0618 only. Canary: `MutationCanaryMethodThatDoesNotExist();` inserted as a new line 56 of `Samples~/CommandRecognition/TacticalSceneController.cs`; rebuild red with `error CS0103` at that file, line 56; `git checkout --` the file (no `.bak`); rebuild green. The recipe's sanity pipeline prints `7`. Negative: Command 4 with `-p:HostAsm=D:/no-such-host/Library/ScriptAssemblies` appended prints the `UnityEngine.UI.dll` `Missing host assembly` error, exits non-zero, no `error CS` line. (F1; degraded-dependency NFR)
4. [ ] DocCheck verbatim from the new recipe section: exit 0; no line starts `FAIL`; the closing `<n>/<n> doc claims verified against the real parser.` has both numbers equal. (F2)
5. [ ] Command 4 (green form) and DocCheck again, each as `env -u MSYS_NO_PATHCONV bash -c '<command>'`: the same results as 3 and 4. (path-conversion NFR)
6. [ ] `git diff --stat main -- Runtime/ Samples~/ Tests~/Fixtures/audio/` prints nothing. (F9 guard)
7. [ ] `python3 .claude/references/core/doctor-hook.py "D:/Workspace/VoXR-Speech-Recognition"` (the SessionStart hook, `.claude/settings.json` :193): output non-empty (a doctor line or its `doctor:` summary present) and no `V21` row; a silent hook fails. (decision A)
8. [ ] Text: `git diff main -- .claude/bindings/unity.md` touches :9, :12, :15, :17 only; `git diff main -- NativeBridge~/include/vosk_api.h` :3 only; `git diff main -- Planning~/verification-recipe.md`, read hunk by hunk, touches only Step 2's sites; `git grep -n -I -i -E 'wsl|/mnt/' -- .claude/bindings/ Planning~/verification-recipe.md NativeBridge~/include/` prints nothing. (F5, F6, F8)

## Open questions that block implementation

None. The stops above are the human's at run time.

## Blast radius

- Every later Command 4 and DocCheck run reads this text; `.scratch/doccheck/` appears; `NativeBridge~/build/`, `build-windows/` rebuild for the header.
- Recipe lines shift after :35, :89 and :208: `architecture.md`'s recipe refs become plan-time refs (re-pointed at reconcile); `Planning~/design-docs/windows-native-toolchain.md:44` cites :21, :35, :71 (tier 1, left).
- `architecture.md` :92 does not carry clause A; this dispatch wrote only its link line.
- Untouched: `generate.sh:10` (Phase 2); recipe :150-151; `Documentation~/native-bridge.md`, `.claude/settings.local.json` (after G2; the human's).
- Every session start: doctor's V21 row clears.

## Runtime claims

- Gate 0: Git Bash's `find` is GNU find (not `find.exe`), so `-mindepth 1 -delete` works; it empties the directory, no file locks held.
- Gate 1: Git Bash's `ls` resolves the `/c/…` adb path.
- Gate 2: Command 3 stays green with only a comment changed: the symbol list unchanged, 16/16.
- Gate 3: on SDK 10.0.300, Command 4's output (`-v m`, piped) prints `Build succeeded` and `0 Error(s)` in that form, with no warning but CS0618 (on record: 10.0.112 only); MSBuild expands the drive-letter globs with their `Exclude`; `VoxrCheckInputs` runs before reference resolution, stopping at the first missing input; the sanity pipeline prints 7 on Windows output; `SamplesCompile.csproj` restores with no NuGet download (else a human `!` step).
- Gate 4: `stage.sh`, CRLF in the worktree, runs under Git Bash; `Rig.csproj` restores with no NuGet download (else a human `!` step), builds and runs natively on 10.0.300; `dotnet run` returns DocCheck's exit code.
- Gate 5: Command 4 and DocCheck give the same results with `MSYS_NO_PATHCONV` set and unset.
- Gate 7: the hook prints a doctor report; doctor's V21 check passes a Path form saying the working-dir read `already prints` the form.
