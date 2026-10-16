# Phase 4 — persisted plan (2026-10-16)

**Phase 4 - Overlay core and `confgen`**, unsplit, serial, no units: F3, F7 (rules), F8, F9, §6 robustness, §10 items 3-4. OV=`Runtime/EndpointerOverlay.cs`; CT=`Tests~/Runtime/ModelExtractorCacheTests.cs`; `$S`=`.scratch/endpointer-profile-phase-4`.
**Out of scope:** seam, enum, `Resolve`, log, tuned presets, `stage.sh`'s closure (Phase 6); README (Phase 7); edits to `VoxrSpeechRecogniser.cs`, `ModelExtractor.cs`, `stage.sh`, rig files; product docs; CHANGELOG; reconcile; stale cites.

**Boundary reading** (human-approved; departs from the end state in *One Unity-free source*, *`confgen`*; the reconcile records it):
- B1. Phase 4 builds the row type; the table, `ModelDefault` alone (all null); rewrite, pristine copy, atomic writes as one `Apply(modelDir, row)`.
- B2. Phase 6 adds the public `VoxrEndpointerProfile` (Command 4 there), `Resolve`, the log text, tuned rows, `confgen --profile/--mode` and its links to `VoxrEndpointerProfile.cs`, `VoxrListeningMode.cs`.
- B3. The PlayMode tests' tuned row is the test's own (rule2 `"0.35"`), no table entry.
- B4. Phase 4 `confgen` takes `--rule2 <v> [--rule3 <v>] [--rule4 <v>]` only, linking OV alone.

**Readings:**
- R1. A value: the text after `<key>=`, trimmed, ASCII `[0-9]+(\.[0-9]+)?`, no `System.Globalization`; row values too: `confgen` rejects bad input before any write.
- R2. Split on `\n`, a preceding `\r` in the terminator; strict UTF-8 `new UTF8Encoding(false, true)`; a leading BOM passes through, outside line 1's key-line test; valid input round-trips byte for byte (an all-null row: the copy's bytes).
- R3. `Apply` returns `ApplyResult` (`ConfPath`, `Reason`), logs nothing; Phase 6's call site logs it.

## Files to change

| Path | Change | Why |
|---|---|---|
| OV, OV`.meta` | create: Step 1 | F7, F8, §6 |
| `Tests~/Editor/EndpointerOverlayTests.cs`; CT | create; modify: Step 2 | F7-F9, §6 |
| `NativeBridge~/harness/confgen/{confgen.csproj,Program.cs}` | create: Step 3 | F3; §10 item 3 |
| `$S/hash.py` | create, untracked: Step 3 | check 5 |
| `plan-4.md`; `architecture.md` | create; link line | this plan |

## Contracts and interfaces

**Dispatch.** A, `code-writer`: Steps 1-3. B, `compile-check`: Step 4 once A ends unstopped. No agent git writes; Git Bash, never WSL.

**Step 1 - OV.** `namespace VoXR`, `internal static class EndpointerOverlay`, C# 9, netstandard 2.1, `System`, `System.IO`, `System.Text` only. Internal:
- `sealed class Row`: `public readonly string Rule2, Rule3, Rule4` via `Row(rule2, rule3, rule4)`; null = the archive's own; no record or `init` (`IsExternalInit`).
- `static readonly Row ModelDefault = new Row(null, null, null)`.
- consts `ArchiveCopyFileName = ".voxr-archive-model.conf"`, `TempSuffix = ".voxr-tmp"`, `Rule2Key`..`Rule4Key` (`--endpoint.rule<n>.min-trailing-silence`).
- `static bool IsDecimal(string)`: R1.
- `static byte[] Rewrite(byte[] pristine, Row row, out string reason)`: pure; *Rewrite rules*, R1-R2; unparseable: `pristine`, a reason.
- `sealed class ApplyResult`: `public readonly string ConfPath, Reason` (null when applied).
- `static ApplyResult Apply(string modelDir, Row row)`: no conf: a reason, no write; else *Pristine source*, *Atomic writes* (`File.Replace(tmp, conf, null)`, `File.Move` if no target), `Rewrite` from the copy; unparseable: the copy written back. `catch (Exception e)`: the reason, any temp it made deleted; never throws.

Reasons: `no conf/model.conf`; `conf is not valid UTF-8`; `<key> appears more than once`; `<key> value '<v>' is not a non-negative decimal` (`row ` first for a row's); `IO failure: <message>`. OV`.meta`: `fileFormatVersion: 2`, `guid: <32 random hex>` (`VoxrListeningMode.cs.meta` :1-2).

**Step 2 - tests.** EditMode `EndpointerOverlayTests`, `namespace VoXR.Tests.Editor`, as `ModelExtractorValidationTests.cs` :1-24, `[Test]`s:
- `Rewrite_` + `ModelDefault_ReturnsPristineBytes` (LF, CRLF, BOM, no final terminator); `TunedRule2_ReplacesOneLine_KeepsTerminator`; `KeyLineWithLeadingWhitespace_IsMatched`; `MissingKeys_AppendedInRuleOrder_FinalTerminatorFirst` (LF, CRLF); `ArchiveOwn_LeavesMissingKeyAbsent`; `{InvalidUtf8,DuplicateKey,BadValue}_ReturnsCopyWithReason` (BadValue: `-0.5`, `abc`, `1e3`, empty).
- `Apply_` + `NoConf_WritesNoCopy`; `FirstApply_CapturesPreRewriteBytes`; `TunedThenModelDefault_RestoresPristine`; `SteadyState_DoesNotWrite` (conf `LastWriteTimeUtc` 2020-01-01 UTC before the second `Apply`, unchanged after); `LeavesNoTempFile`; `UnparseableCopy_RestoresConfToCopy`; `IoFailure_ReportsReason` (a directory at `conf/model.conf.voxr-tmp`, tuned row: `Reason` set, conf bytes kept, no throw).
- Invalid-UTF-8, unparseable-copy fixtures: raw bytes (`File.WriteAllBytes`, `byte[]` to `Rewrite`), not `BuildModelArchive`.

PlayMode, CT (+ `using System.Text;`), two `[UnityTest]`s on `BuildModelArchive` (:789-803), `ExtractWithToken` (:735-743), `CountingArchiveSource` (:770-783), `ReadStamp` (:752-753): extract; `Apply` rule2 `"0.35"`; plant `marker.txt`; re-extract; stamp unchanged, marker kept, conf tuned; `Apply(ModelDefault)`: the archive conf's bytes.
- `OverlaidConf_Tier{1,2}ReExtract_KeepsCacheAndStamp`: 1, token both times, `Reads` 1; 2, no token.

**Step 3 - `confgen`.**
- `confgen.csproj`: Exe, net10.0, `Nullable` disable, `EnableDefaultCompileItems` false (`Rig.csproj` :3-5, :35), `LangVersion 9.0`, `AssemblyName confgen`; compiles `Program.cs`, `../../../Runtime/EndpointerOverlay.cs` (`Link="EndpointerOverlay.cs"`).
- `Program.cs`, usage comment, then `Console.Out.NewLine = "\n"` (or equivalent). Exit 2, stderr `confgen: <problem>` + `usage: confgen --model <dir> --out <dir>` and B4's flags: unknown or repeated flag (`--profile` too), missing value or flag, a value failing `IsDecimal`, no `--model` dir, full-path `--out` equal to or inside `--model`, or the reverse (both named). `--out` absent: `--model` copied there recursively (failure: `confgen: copy failed: <message>`, exit 1); present: the cached path. A reason: stderr `confgen: not applied: <reason> (<conf path>)`, exit 1 (no sweep silently decodes the pristine conf); else stdout `sha256 <64 lowercase hex>` of the out conf's bytes, then its lines trimmed-beginning `--endpoint.rule`; exit 0.
- `$S/hash.py <dump> <out>`: SHA-256 of line 1's `modelConf` UTF-8 = `<out>` line 1's: `ok`, else both; exit 0/1.

**Step 4 - green** (`compile-check`, project root). Rules: (a) Bash collapses `\\` to `\`: backslash-free probes, Python in `$S/*.py` via `python3 -I`; (b) `compile-check` lacks Write: probes are `code-writer`'s; (c) vc-guard blocks subagents: the PM force-adds `confgen.csproj` (`.gitignore` :15 `*.csproj`).

One Bash call per check, opening `X` = `N=NativeBridge~; S=.scratch/endpointer-profile-phase-4 V=$N/vendor/vosk-model-small-en-us-0.15 O=$N/vendor/overlaid/rule2-0.35; C="dotnet $N/build-confgen/bin/confgen/debug/confgen.dll --model $V --out $O --rule2 0.35" M=$O/conf/model.conf H="$N/build-windows/bin/vosk-bridge-harness.exe --model $O --fixtures Tests~/Fixtures/audio/tts --manifest $S/pin.json";`.

1. Commands 1-2 (`.claude/bindings/unity.md`), `X sha256sum Runtime/EndpointerOverlay.cs.meta` before and after: `failed="0"`, `inconclusive="0"`, totals above 203 and 704 by the new tests; no `Tests` leftovers; hash unchanged. The host builds Runtime at apiCompatibilityLevel 6 (.NET Standard 2.1, `D:\Workspace\VoXR TestGround\ProjectSettings\ProjectSettings.asset:925`): Commands 1-2 carry that surface; Phase 6's Command 4 compiles OV.
2. Command 3 as bound: `.so` built; harness exit 0, 37/37, standing pin.
3. `X dotnet build $N/harness/confgen/confgen.csproj --artifacts-path $N/build-confgen; echo "exit $?"; $C > $S/c1.out; echo "exit $?"; stat -c %y $M > $S/t1; $C > $S/c2.out; echo "exit $?"; stat -c %y $M > $S/t2; cmp $S/c1.out $S/c2.out && cmp $S/t1 $S/t2; echo "same $?"; cat $S/c1.out`: `exit 0` thrice, `same 0`; `sha256 <64 hex>`, three rule lines, rule2 `=0.35`.
4. `X diff $V/conf/model.conf $M; echo "diff $?"`: `diff 1`, one hunk, rule2 `0.5` to `0.35`.
5. `X cp $N/harness/expectations.json $S/pin.json; $H --write-baseline; echo "exit $?"; $H --dump $S/b.jsonl; echo "exit $?"; python3 -I $S/hash.py $S/b.jsonl $S/c1.out`: `exit 0` twice, `ok`. Pin copy: the standing pin exits 2 on `$O` (`main.cpp` :228-229).

**Stop rules.**
- Before check 3, `$O` present or `$S` holding more than `hash.py`: BLOCKED, no `rm -rf`.
- No dll at `C`'s path: rebuild with `-o $N/build-confgen`, `C` naming its `confgen.dll` (obj in `confgen/obj/`, `.gitignore` :4), reported.
- A red check: back to `code-writer` (two fix passes, then the human); no edit out of scope or to Phase 2-3 `.scratch/`.
- Command 3 exit 1: the human's ruling, never a re-pin; `--write-baseline` writes `$S/pin.json` alone.
- OV needing a Unity type or `System.Globalization`: stop.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| Stop at B1 | the end state now | the Phase 6 row; human-ruled |

## Acceptance criteria

1. [ ] Check 1. (F7, F8, F9, §6; *One Unity-free source*)
2. [ ] Check 2. (§9)
3. [ ] Check 3. (F3; §10 item 3; F9)
4. [ ] Check 4. (F7)
5. [ ] Check 5. (F3; §10 items 3-4)

## Open questions that block implementation

None; a stop opens one.

## Blast radius

- OV compiles into Runtime, uncalled (IL2CPP `File.Replace`: Phase 8), both suites reach it (`AssemblyInfo.cs` :9-10); unstaged by `stage.sh` until Phase 6.
- The copy sits beside `ModelExtractor.StampFileName` (:23), unread by `ValidateModelDirectory` (:250-261): no re-extraction (F9).

## Runtime claims

- a, g (4.3): `--artifacts-path` puts the dll at `bin/confgen/debug/` (else `-o`); `stat -c %y` resolves sub-second on NTFS.
- b (1, 2): `File.Replace`, `File.Move` behave alike in Mono and .NET 10.
- c (4.1): Unity keeps the hand-made `.meta`.
- d, i (1, 2): the strict decoder throws on invalid bytes; its `GetString`/`GetBytes` round trip is byte-exact, leading BOM too, on Mono, IL2CPP, .NET 10.
- e, k (4.5): `--write-baseline` into a pin copy, then `--dump` on it (same finals), exits 0.
- h (4.5): the dump's `modelConf` is the conf's exact text (plan-2 d, s).
- j (3, 4.5): that `NewLine` holds on Windows (`hash.py` reads line 1).
