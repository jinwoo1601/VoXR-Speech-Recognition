# Phase 2 — persisted plan (2026-10-14)

**Phase 2 - inputs and guards**, unsplit: F2, F8's tag, F11's guard, the NFRs, requirements section 10 items 2, 3, 5. P: = `Program.cs`, S: = `stage.sh`. `ab-rig/` = `Planning~/features/coverage-in-selection/ab-rig/`.

## Files to change

| Path | Change | Why |
|---|---|---|
| `ab-rig/JoinedInput.cs` | create | *Input boundary*; *Marks*; *Provenance* |
| `ab-rig/Joined.cs` | create | F2 *Kill check*; F11 *Grammar-pin guard*; *Scored records* |
| `ab-rig/joined.sh` | create | *Run script*; *Degraded dependencies* |
| `ab-rig/Program.cs` | modify | *Parser configuration*; *Staging* |
| `ab-rig/stage.sh` | modify | *Staging* |
| `Tests~/Fixtures/audio/manifest.json` | modify | *Provenance*; F8 |
| `Planning~/verification-recipe.md` | modify | *Staging* |

One change set, before any build or stage: `JoinedInput.cs`, `Joined.cs`, the P: dispatch, the S: list. `ab-rig/Rig.csproj` (globs `*.cs`, unedited) is git-ignored: the PM adds it with `git add -f`.

## Contracts and interfaces

**C#.** Namespace `AbRig`; top-level types only `Joined`, `JoinedInput` (static), others nested. In-box `System.Text.Json`, no `PackageReference`. A stop: nested exception `JoinedInput.Stop` (`Code` 2 or 3); `Joined.Run` prints its lines to stderr prefixed `joined: `, returns `Code`; any other exception (JSON, I/O, any) it catches: exit 2 naming the file, and line where known.

**`Program.cs`.** After the `--grammar` block (P:156-162): if `args` holds `--joined`, build the parser as P:298 into `joinedParser`, never `parser` (CS0136), and `return Joined.Run(args, joinedParser, VoxrCommandParser.GenerateGrammarJson(Slots(), Commands(), FollowUpWords()));`.

**`stage.sh`.** S:41-42's `cp` list gains `"$HERE/JoinedInput.cs" "$HERE/Joined.cs"`; nothing else.

**`JoinedInput.cs`.** Readers finish first; each *Input boundary* stop exits 2, named as it says.
- Dump (*Dump records*), split on LF: CR, non-object line, unknown `kind`, missing or mistyped field -> `<file>:<line>`. Line 1 `header`, `schema` 1. Frames `case`, `result`s (the open case; `seq` from 0, contiguous; `final` bool; `payload` object or `raw` string; final payload string `text`, else string `partial`, unless `error`), `case-end` (`results` = count; `errors` array). One `end`, last, `cases` = frames. Open frame, repeated case: stops. Unknown record keys ignored. Finals: ordered `text`s of `final:true` records, empty dropped. *Failed* case: `raw`, `error` payload, or non-empty `errors`.
- Manifest: top level only `voice` (optional), `provenance` (required string), `cases`. Per case the eleven *Corpus manifest* keys, `sealed` bool, `layer` `parser`|`recogniser`; `file` required. Join key: `file` after its last `/`; shared -> stop.
- Pin: `_comment`, `cases` (each `file`, `expectedFinals`), `gain`, `grammar`, `libvosk`; other or missing key, repeated `file` -> stop.
- Joins: dump case not in manifest or the reverse; with `--pinned`, pin and dump case sets unequal -> stop naming the case.

**`Joined.cs`: `--joined score`.** `--dump`, `--manifest`, `--pin`, `--out`, `--side a|b`, `--vary parser|decoder`, each once; `--pinned` optional; else `Stop(2)`. `--side`/`--vary` (header) exceed *One joined run* step 3. Order:
1. Delete an existing `--out`; read `./STAGED_FROM` (the stage), dump, manifest, pin.
2. Guard: `grammarJson` as a string array = the pin's `grammar`, ordinal; else `Stop(2)` naming `STAGED_FROM`'s first line, first differing index, both entries.
3. `--pinned`: kill check (case sets are the readers') - header `gain`, `libvosk`, `grammar` = the pin's; no failed case; per case finals = `expectedFinals`. Else `Stop(3)`, a line per failing field or case, expected and actual.
4. Unpinned, a failed case: `Stop(2)` naming each.
5. Write `--out`, UTF-8 no BOM, LF only, a compact object per line: `{"kind":"header","side","vary","stagedFrom":[its lines],"dump","dumpHeader":{less kind},"manifest","provenance","pinned"}`; per manifest case `{"kind":"case","case":<basename>,"disposition","finals"}`: `sealed` -> `sealed`, no `finals`; `layer` `recogniser` -> `recogniser-layer`; else `scored`. Scoring: Phase 3's `JoinedScorer`.
6. stdout `scored <n>`, `recogniser-layer <n>`, `sealed <n>`, a line each; exit 0. `parser` waits for Phase 3.

**`joined.sh`.** Usage and procedure in its header; `set -euo pipefail`; `bash <path>/joined.sh` from the project root; `HERE` as S:12.
1. *Run script*'s shapes, options, defaults. Else (`--evaluate` until Phase 3; a repeat; a missing value or shape option; the other shape's) `joined.sh: <problem>`, exit 2; a dump, manifest or pin not a file: exit 2 naming it. `mkdir -p` the out dir; remove its `scored-*.jsonl`.
2. ref-a (or `--ref`) -> `<out>/stage-a`; ref-b -> `<out>/stage-b` unless equal. Each `env -u MSYS_NO_PATHCONV bash "$HERE/stage.sh" <ref> <stage>`, then `(cd <stage> && dotnet build -v q --nologo)`; non-zero -> `joined.sh: <ref>: stage failed` / `build failed`, exit 2. All before any score.
3. All paths absolute for `dotnet`: `cygpath -am` if present, else `$PWD/` prefixing a relative one. Per side `(cd <stage> && dotnet run --no-build -- --joined score ... --out <out>/scored-<side>.jsonl --side S --vary V [--pinned])`. `parser`: one dump, a by ref-a, b by ref-b, both pinned. `decoder`: a dump-a pinned, b dump-b unpinned. Side a first; a non-zero `score` exit: remove this run's scored files, pass the exit through.
4. Print the scored paths; exit 0. No `report` (Phase 3).

**Manifest.** After `"voice"`: `"provenance": "Synthetic TTS corpus: voice en_US-lessac-medium, synthesized with piper-tts by Tests~/Fixtures/generate.py. Synthetic TTS evidence cannot rank decoders (C12).",`. Split case (:165-181): `"layer": "recogniser",` after `"category": "split",`. Nothing else.

**Recipe :232.** "`stage.sh` is not edited" -> "`stage.sh`'s path handling is not edited"; nothing else.

**Stops for the human:** criterion 0 unequal (never a re-pin); harness exit 1; a NuGet download; `NativeBridge~/vendor/` absent (a human `!` download); a false runtime claim (PM-ruled).

**Commit set:** the six `ab-rig/` files, manifest, recipe, this plan, its link line; `memory/**/*.meta` if any; not `.scratch/`. No CHANGELOG line.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| Case sets unequal: reader stop, 2 | Kill check's 3 | Ruled 2026-10-14, "Exit 2, reader stop" |
| Two top-level types | Free names | Shared assembly |
| Clear scored files at start and on a stop | Keep | *Degraded dependencies*: "with no report" |
| Unknown dump keys ignored | Stop | `schema` versions; manifest strict (*Marks*) |
| `pinned` in header | Omit | Report names the pinned side |
| Equal refs, one stage | Two | "Each distinct ref" |

## Acceptance criteria

Verification agent, in order; Git Bash, project root; JSON checks and copies by `python3 -I`, copies LF-only (binary I/O or `newline='\n'`); git writes are PM steps.

0. [ ] Before code: `env -u MSYS_NO_PATHCONV bash "Planning~/features/coverage-in-selection/ab-rig/stage.sh" WORKTREE .scratch/joined/p2-grammar && (cd .scratch/joined/p2-grammar && dotnet build -v q --nologo && dotnet run --no-build -- --grammar > ../p2-grammar.json)`; parsed, ordinal-equal to the pin's `grammar`. (F11)
1. [ ] Commands 1-2 (binding `## verification`) green; `Tests~` reverted; untracked `memory/**/*.meta` reported (PM commits them after). (NFRs)
2. [ ] DocCheck per the recipe, verbatim: exit 0, no `FAIL`, `<n>/<n>` equal. (F11)
3. [ ] From `NativeBridge~` (desktop half rebuilt if absent or stale): `./build-windows/bin/vosk-bridge-harness.exe`, Command 3's arguments plus `--dump ../.scratch/joined/p2.jsonl`: exit 0, 16/16. Then `bash Planning~/features/coverage-in-selection/ab-rig/joined.sh --vary parser --dump .scratch/joined/p2.jsonl --ref-a main --ref-b WORKTREE --out .scratch/joined/p2-run`: exit 0; both scored files 16 cases, split `recogniser-layer`, 15 `scored`, `finals` = `expectedFinals`. (F2, F8)
4. [ ] F2 canary: a `p2.jsonl` copy, one final's `text` altered, run as 3 (`--out .scratch/joined/p2-canary`): exit 3 naming the case, expected and actual; no scored file. (F2)
5. [ ] Negatives, run as 3, `--out .scratch/joined/p2-neg-<name>`: non-zero, cause named, no scored file, exit recorded. `malformed` (a line truncated); `extra` (a frame copied as `extra.wav`, `end.cases` 17); `missing` (a frame removed, `end.cases` 15); `key` (manifest copy, `"seald": true`); `nodump`; `ref` (`--ref-b no-such-ref-p2`); `grammar` (pin copy, one entry altered: exit 2). (NFRs, F11)
6. [ ] `git status --short`: commit set and `memory/**/*.meta` only; of `Runtime/`, `NativeBridge~/`, `Tests~/`, the manifest alone. (Out 3)

## Open questions that block implementation

None.

## Blast radius

- DocCheck compiles the joined files; an error breaks it.
- `JsonUtility`, `generate.py` read the manifest; a new key stops the rig until taught.
- Phase 3 appends to the scored record; `Rig.csproj` tracked; `.scratch/joined/` fills.
- Stale `main.cpp` refs, "hand-copied grammar JSON": as-built pass.

## Runtime claims

- 0: the rig's grammar equals the pin's.
- 0, 3: `dotnet build -v q --nologo` exits non-zero on a compile error.
- 1: `JsonUtility` ignores `provenance` and `layer`.
- 0-3: no NuGet download.
- 3: `git show main:<path>` succeeds for all ten `stage.sh` sources; staged `main` builds with the worktree's rig files.
- 3: Command 3's harness binary present and current, its `--dump` re-run 16/16, exit 0; `NativeBridge~/vendor/` provisioned.
- 3: the dump's finals, empty dropped, equal the pin's `expectedFinals`.
- 3: `dotnet run --no-build` in the stage returns the app's exit code.
- 3-5: `cygpath -am` paths reach `dotnet` intact with `MSYS_NO_PATHCONV` set; root-relative arguments survive the `cd <stage>`.
- 3-5: no path exits with an unhandled-exception code.
- 4-5: Python copies keep LF, so each negative reaches its claimed stop.
- 5: `stage.sh` exits non-zero on an unshowable ref.
