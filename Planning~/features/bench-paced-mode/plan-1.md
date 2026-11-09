# Phase 1 — persisted plan (2026-11-09)

Scope: before any edit, the tree provisioned, Command 3 run as the build and a fast-only `bench.sh` run's report kept as the F10 baseline; then `BenchPaced.cs`'s pure functions with toys T1–T5 in `metrics-check`, the `Stamps` selection extracted in `Bench.cs` (its command-frame arithmetic with it), `JoinedInput` reading the paced keys, checked on hand-written dump lines, and `stage.sh` copying `BenchPaced.cs`; this plan also takes on a pinned `--ref` for every fast-only compare; nothing of Phases 2–3.

## Files to change

`R` is `Planning~/features/coverage-in-selection/ab-rig`.

| Path | Action | Why |
|---|---|---|
| `R/BenchPaced.cs` | create | register *Code home*, *Wall reference*, *Decode time*, *Matched-chunk check*, *Command stage*; F2–F4, F6, F9 |
| `R/Bench.cs` | modify | *Code home* (shared selection); *Known-value toys*; header `Depends` |
| `R/JoinedInput.cs` | modify | register *Stamps* (paced keys, exactly when `pace`) |
| `R/stage.sh` | modify | *Code home*: `BenchPaced.cs` in the `recogniser` copy, :115 |

Unedited, diffed against `HEAD` (V10): `Joined.cs`, `JoinedRecogniser.cs`, `JoinedScorer.cs`, `JoinedReport.cs`, `Program.cs`, `DocCheck.cs`, `Rig.csproj`, `joined.sh`, `bench.sh`, `BenchMetrics.cs`, `BenchReport.cs`. `stage.sh` :9 stays ("among them" holds); `bench.sh` :3 (the fast run's steps) and :6 (`Depends` naming `stage.sh` and `Bench`) stay: Phase 1 adds no `bench.sh` step, and `BenchPaced.cs` reaches the stage through `stage.sh`.

## Contracts and interfaces

- **JoinedInput records.** `Dump` gains `internal long? Pace;`, `DumpResult` `internal long? Wall;`, `DumpCase` `internal List<(long End, long Due, long Made)> Pushes;` and `internal List<(long Consumed, long At, long Pushed)> Reads;` — all null unless read paced.
- **JoinedInput.ReadDump.** `internal static Dump ReadDump(string path) => ReadDump(path, null);` and `internal static Dump ReadDump(string path, long? pace)`, today's body. `pace` null: today's reader, no paced key looked at (`--joined`, `FastRun` — callers unchanged). `pace` 0: a fast dump expected, each paced key refused. `pace` > 0: a paced dump expected. `where` is `path + ":" + (i + 1)` (:192). Header, after `Int(rec, "ringCapacity", where)` (:233) and before `dump.Header = rec` (:234), only with `pace` set: a present `pace` is read by `Int` into `dump.Pace`; expected 0 and present → ``<where>: `pace` <p>, expected none (a fast dump)``; expected > 0 and absent → ``<where>: no `pace`, expected <n>``; present and unequal → ``<where>: `pace` <p>, expected <n>``. Result, after the schema-2 block (ends :334), before `open.Results.Add(entry)` (:335) and so before the `raw` branch (:337): `pace` > 0 → `entry.Wall = Int(rec, "wall", where)` (`entry` declared :290). Case-end, after the `errors` check (:401-403), before `open = null` (:404): `pace` > 0 → `open.Pushes = Triples(rec, "pushes", where)`, then `open.Reads` likewise (`open` declared :181). With `pace` 0, a present `wall`, `pushes` or `reads` → ``<where>: `<key>` without `pace` in the header``, built by one helper `static Stop WithoutPace(string where, string key)`, thrown at the three sites. All `Stop(2, …)`. New `static List<(long, long, long)> Triples(JsonElement obj, string key, string where)`: `Arr`, each element an array of exactly three `TryGetInt64` numbers, else the helpers' stop text ``<where>: missing or mistyped field `<key>` `` (`Field` :858, `Int` :884, `StrArray` :906).
- **Bench.** `internal static (JoinedInput.DumpResult FirstPartial, List<(JoinedInput.DumpResult Result, string Text)> Finals) Selected(JoinedInput.DumpCase d)` — :318-334's loop moved: the first non-final with non-empty `partial` (null if none), the finals with non-empty `text`, in order. `internal static long? FireFrame(JoinedScorer.Outcome s, long t)` — `Bench.cs` :339's `(long)Math.Round(s.FireTimes[0] * t)` (read off :339, `long k = …`; :337 the `FireTimes.Length > 0` test, :340 the `Offset`), null with no fire. `internal static string FrameOffset(long k, long e, long r, long t) => BenchReport.Offset(k * r - t * e, t * r);`. `Stamps` keeps its silence branch first, then calls the three; its strings are unchanged. Header `Depends` gains `BenchPaced` after `BenchMetrics`.
- **BenchPaced** (`internal static class`; header Purpose `The bench's paced arithmetic: stamps on both clocks and their split, the command stage's split, the real-time factor from the read trace, push lateness, the matched-chunk compare`, Owns `BenchPaced (internal static class), BenchPaced.Factor (internal sealed class)`, Depends `Bench, BenchMetrics, BenchReport, Joined, JoinedInput`). `internal const long ReadSamples = 4096;`, `internal const long PacedPadding = 48000;`, `internal const string Unpaced = "unpaced";` (the literal's only site), 10^6 µs per second.
  - `Split(long consumed, long wall, long e, long r, long clip)` → `(string Audio, string Wall, string Compute)`: Audio `Offset(consumed - e, r)`; `consumed > clip + PacedPadding` → Wall and Compute `Unpaced`; else Wall `Offset(wall * r - e * 1000000, 1000000 * r)`, Compute `Offset(wall * r - consumed * 1000000, 1000000 * r)`.
  - `CommandSplit(long? fast, long? paced, long e, long r, long t)` → same tuple: Audio `FrameOffset(fast)`, Wall `FrameOffset(paced)`, Compute `Offset(paced - fast, t)`; each `none` where a frame it needs is null.
  - `Factor` { `int Measured; int Merged; long Decode; long Samples;` }. `Reads(List<(long Consumed, long At, long Pushed)> reads, long clip)`: change k's size is its `Consumed` minus the previous (0 first); merged when size > `ReadSamples`; within the clip when `Consumed <= clip`; `Merged` counts merged changes within the clip; change k (not the last) is measured when within the clip, neither it nor k+1 merged, and `Consumed < Pushed`: `Decode += At[k+1] - At[k]`, `Samples += size`.
  - `Ratio(long decode, long samples, long r)` → `BenchMetrics.Value`: samples 0 → `Undefined("no measured reads")`, else `Of((double)decode * r / (1000000.0 * samples))`. `Corpus(List<Factor> cases, long r)` → `Ratio` of the summed `Decode` and `Samples`.
  - `Lateness(List<(long End, long Due, long Made)> pushes)` → the largest `Made - Due` µs, `(max / 1000.0).ToString("0.000", CultureInfo.InvariantCulture) + " ms"`.
  - `Matched(JoinedInput.DumpCase fast, JoinedInput.DumpCase matched)` → null on pass, else one line, the first failing check: the `Selected` finals' texts, `string[] a` (fast) and `b` (matched), unequal (`Joined.FirstDifference(a, b) >= 0`; Joined.cs :823-830 returns the shorter's length on unequal lengths) → `matched-check: case "<fast.Name>": finals fast <Joined.JsonArray(a)>, matched <Joined.JsonArray(b)>` (Joined.cs :837-848, compact, `WriterOptions` :50-53: `["fire","missiles"]`) (`…` below is that prefix through `": `); a final i (1-based) with `|Δconsumed| > ReadSamples` → `… final <i> "<text>" at consumed fast <a>, matched <b>: more than 4096 apart`; first partial on one side only → `… first partial at consumed fast <a|none>, matched <b|none>`, both and more than 4096 apart → that line plus `: more than 4096 apart`.
- **Toy helpers (Bench).** `static JoinedInput.DumpCase Timeline(long? partial, params (string Text, long Consumed)[] finals)`: `new JoinedInput.DumpCase { Name = "toy", SampleRate = 48000 }` (no constructor; initialiser as JoinedInput.cs :255-260), then `Results.Add` (readonly list, :71) of `new JoinedInput.DumpResult { Seq, Final, Consumed, Payload }` (as :290): a `{"partial":"fire"}` non-final at `partial` where not null, then each `{"text":"<Text>"}` final, `Seq` from 0. `static string Factored(BenchPaced.Factor f)` → `measured=<M> merged=<G> decode_us=<D> samples=<S> factor=<Ratio(D, S, 48000)>`. `static string ReadLines(string text, long? pace)`: writes `text` to `Path.GetTempFileName()` with `File.WriteAllText(path, text, JoinedInput.StrictUtf8)` (:136, no BOM), `ReadDump(path, pace)`, deletes it in a `finally`; returns `pace=<Pace|none> wall=<first result's Wall|none> push=<End>,<Due>,<Made>|none read=<Consumed>,<At>,<Pushed>|none` (first case's first entries), or on a `JoinedInput.Stop` (:24-35, `Code` and `Lines`) `stop <Code>: ` + `Lines[0]` less the exact prefix `path + ":"` (ordinal `StartsWith`, never a split at the first `:` — the temp path holds the drive's).
- **stage.sh** :115: `"$HERE/Bench.cs" "$HERE/BenchMetrics.cs" "$HERE/BenchPaced.cs" "$HERE/BenchReport.cs" "$OUT/"`.
- **Comments.** Rule 1 as written, no override: the body comments of `DumpCase` (:51-70) and `Dump` (:91) and inside `ReadDump` (:185-186, :293-294) go; the overload, `Triples` and `WithoutPace` sit directly in `JoinedInput`'s body, so its own `//` lines go too — :22-23, :37-38, :74-75, :170, :445-446, :660, :736, :738-739, :785, :824-825, :843-844 (`//`, not `///`, so no member's leading doc comment). `Bench.cs` holds none past its header. Override of rule 1 at :7-11 (lessons, notes-for-future-features :29): its "Unknown dump record keys are ignored" no longer holds for a read with `pace` set, so that sentence must also say a read with `pace` set reads `pace`, `wall`, `pushes` and `reads` strictly, refusing them for a fast dump and requiring them for a paced one (bench-paced-mode, *Stamps*); the rest of :7-11 stays.

**Toys**, rows 27–49 after row 26 (:508), before :510; `where` the number; r = 48000, e = 48000, t = 90, clip 96000 in T1. Rows 27–30 print `Audio | Wall | Compute`; rows 36–39 `Matched(…) ?? "pass"`. The register's minus is ASCII `-`, as `Offset` prints.

| # | Input | Want | Working |
|---|---|---|---|
| 27 | T1 `Split(43200, 912000, …)` | `-0.100 \| -0.088 \| +0.012` | -4800/48000; 912000-1000000 µs; 912000-900000 µs |
| 28 | T1 `Split(72000, 1534000, …)` | `+0.500 \| +0.534 \| +0.034` | 24000/48000; 534000 µs; 1534000-1500000 µs |
| 29 | T1 `Split(150000, 3200000, …)` | `+2.125 \| unpaced \| unpaced` | 150000 > 96000+48000; 102000/48000 |
| 30 | T2 `CommandSplit(135, 144, …)` | `+0.500 \| +0.600 \| +0.100` | (135-90)/90; (144-90)/90; 9/90 |
| 31 | T3 `Reads` [4096@10000, 8192@18600, 12288@27000, 16384@35600], pushed 16384, clip 16384 | `measured=3 merged=0 decode_us=25600 samples=12288 factor=0.1000` | 8600+8400+8600; the 4th empties the ring; 0.0256 s / 0.256 s |
| 32 | T3 [4096@0, 8192@12800], pushed 8192, clip 8192 | `measured=1 merged=0 decode_us=12800 samples=4096 factor=0.1500` | 12800 µs / 85333 µs |
| 33 | T3 `Corpus` of 31, 32 | `0.1125` | 38400 µs / 16384 samples; mean of ratios 0.1250 |
| 34 | T3 [4096@0, 12288@5000, 16384@9000], pushed 16384, clip 16384 | `measured=0 merged=1 decode_us=0 samples=0 factor=undefined (no measured reads)` | 8192 > 4096: intervals 0→1, 1→2 out; the last has no next |
| 35 | T4 `Lateness` [960,20000,20150], [1920,40000,40020], [2880,60000,61200] | `1.200 ms` | max(150, 20, 1200) µs |
| 36 | T5 `Timeline(24576, ("fire", 49152))` vs `Timeline(28672, ("fire", 53248))` | `pass` | 4096 apart each |
| 37 | matched final at 53249 | `matched-check: case "toy": final 1 "fire" at consumed fast 49152, matched 53249: more than 4096 apart` | 4097 |
| 38 | matched finals fire@53248, missiles@57344 | `matched-check: case "toy": finals fast ["fire"], matched ["fire","missiles"]` | extra final |
| 39 | matched `Timeline(null, ("fire", 53248))` | `matched-check: case "toy": first partial at consumed fast 24576, matched none` | finals pass |

T3's unstated `pushed` (row 34) and clips, T4's end samples (b ÷ 48000 s = due) and T1's clip are the plan's; none moves a stated value.

**Reader rows** (`ReadLines`). P is five LF lines: `{"kind":"header","schema":2,"decoderRate":100,"manifest":"m","fixtures":"f","model":"o","gain":-18,"libvosk":"v","grammar":[],"ringCapacity":65536,"pace":960}`, `{"kind":"case","case":"toy","samples":96000,"sampleRate":48000}`, `{"kind":"result","case":"toy","seq":0,"final":false,"pushed":96000,"prevPushed":95040,"consumed":72000,"origin":0,"wall":1534000,"payload":{"partial":"fire"}}`, `{"kind":"case-end","case":"toy","results":1,"pushed":96000,"consumed":96000,"errors":[],"pushes":[[960,20000,20150]],"reads":[[4096,10000,16384]]}`, `{"kind":"end","cases":1,"passed":1}`; Q is P with `,"pace":960`, `,"wall":1534000`, `,"pushes":[[960,20000,20150]]`, `,"reads":[[4096,10000,16384]]` removed. N = `pace=none wall=none push=none read=none`. 40: P@960 → `pace=960 wall=1534000 push=960,20000,20150 read=4096,10000,16384`. 41: P@null → N. 42: P@16384 → ``stop 2: 1: `pace` 960, expected 16384``. 43: P@0 → ``stop 2: 1: `pace` 960, expected none (a fast dump)``. 44: P less `,"wall":1534000` @960 → ``stop 2: 3: missing or mistyped field `wall` ``. 45: P less `,"reads":[[4096,10000,16384]]` @960 → ``stop 2: 4: missing or mistyped field `reads` ``. 46: P with `[[960,20000]]` for pushes @960 → ``stop 2: 4: missing or mistyped field `pushes` ``. 47: Q@960 → ``stop 2: 1: no `pace`, expected 960``. 48: Q@0 → N. 49: Q plus `,"wall":1534000` after `"origin":0` @0 → ``stop 2: 3: `wall` without `pace` in the header``. A span ending in a key carries one trailing space for Markdown only; the want ends at the closing backtick.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| Every fast-only run `--ref 5f70937` | default `WORKTREE` | the report's `- Staged from:` names `HEAD`, which each commit moves; rig files still come from the worktree (stage.sh :109-115), so the edits run; Phases 2–3 reuse it |
| Baseline copied once to `$F` | `<out>/report.md` | bench.sh :88 deletes `report.md` in its `--out` |
| `ReadDump(path, pace)`, null = today | one strict reader | register: `--joined` reads paced dumps as today; `FastRun` untouched for F10 |
| Flush compute part `unpaced` | `undefined (…)` | it is wall − audio, and the register prints a flush wall `unpaced` |
| `Lateness` formats ms itself | `BenchReport.Offset` | `Offset` prints signed seconds; F9's value is `1.200 ms` |
| `FireFrame`, `FrameOffset` extracted | a paced copy of :339-340 | register: "computed as fast mode's (`Bench.cs` :337-341)"; lessons |
| Reader rows via temp files | a reader verb | `Program.cs` is Phase 2's; the row's "hand-written lines" |

## Acceptance criteria

- [ ] F10 baseline: V3 keeps `$F` before the first edit.
- [ ] F2, F3: rows 27–29; F3/F5 command arithmetic: row 30 (V5).
- [ ] F4: rows 31–34; F9: row 35; F6 rule: rows 36–39 (V5).
- [ ] Register *Stamps*: rows 40–49 (V5).
- [ ] *Code home*: toys 1–26 byte-unchanged, `49/49` (V5).
- [ ] Row checks: Command 3 52/52 (V2); DocCheck (V4); joined `main`-versus-`main`, 0 flips, both layers (V6).
- [ ] F10, §9 "byte-identical": fast-only run `cmp=0` against `$F` (V7).
- [ ] Line endings (V8); plan-fixed text (V9); untouched files (V10).

**Steps:** one Git Bash at the tree root, first `R=Planning~/features/coverage-in-selection/ab-rig T=.scratch/bench-paced-mode/p1 F=.scratch/bench-paced-mode/baseline-report.md BREF=5f70937; mkdir -p $T`; V1–V3 before any edit; then all four files of the files table edited as one change before any stage build (no intermediate state builds green: `Bench.cs` and `BenchPaced.cs` call each other, and `BenchPaced.cs` compiles only once `stage.sh` copies it); then V4–V10.

- V1: `NativeBridge~/harness/README.md` *Provisioning a branch tree* blocks :45, :51 verbatim, then `ls -d NativeBridge~/vendor/{vosk-win64-0.3.45,vosk-model-small-en-us-0.15,webrtc-audio-processing-2.1.tar.xz,abseil-cpp-20240722.0.tar.gz} | grep -c .; test ! -e NativeBridge~/vendor/.copying && echo clean` — green: `provisioned` each, or `NOT provisioned` with `4` and `clean` (already provisioned, README :48, :54).
- V2, Command 3: `ls -d "C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/NDK"`; `Planning~/verification-recipe.md` *Building NativeBridge* :52-63 verbatim; `find NativeBridge~/build -name libvosk-bridge.so | grep -c .`; `.claude/bindings/unity.md` :8's parenthesised command verbatim with `> $T/c3.json; echo "exit=$?"; grep -c -E '^  "(passed": 52|failed": 0|total": 52),?$' $T/c3.json` appended — green: the path, the block exit 0, `1`, `exit=0`, `3`.
- V3: `test -z "$(git status --porcelain -- "$R")" && test ! -e $F && echo fresh && { bash "$R/bench.sh" --ref $BREF --out $T/base >/dev/null; X=$?; echo "exit=$X"; test $X = 0 && cp $T/base/report.md $F && echo kept; }; (cd $T/base/stage && dotnet run --no-build -- --bench metrics-check) > $T/mc-before.txt; echo "mc=$?"; tail -n 1 $T/mc-before.txt; py -I -B -c "import sys; [print(p, (d:=open(p,'rb').read()).count(b'\r\n'), d.count(b'\n')) for p in sys.argv[1:]]" $R/Bench.cs $R/JoinedInput.cs $R/stage.sh | tee $T/eol-before.txt` — green: `fresh`, `exit=0`, `kept`, `mc=0`, `26/26 metric checks verified against hand-computed toy cases.`, three count lines.
- V4: `Planning~/verification-recipe.md` :229 verbatim — green per :232.
- V5: `env -u MSYS_NO_PATHCONV bash "$R/stage.sh" WORKTREE $T/stage recogniser && (cd $T/stage && dotnet build -v q --nologo && dotnet run --no-build -- --bench metrics-check) > $T/mc-after.txt; echo "exit=$?"; grep -c '^FAIL' $T/mc-after.txt; tail -n 1 $T/mc-after.txt; cmp <(grep -E '^(ok  |FAIL)' $T/mc-before.txt) <(grep -E '^(ok  |FAIL)' $T/mc-after.txt | head -n 26); echo "cmp=$?"` — green: `exit=0`, `0`, `49/49 metric checks verified against hand-computed toy cases.`, `cmp=0`.
- V6: `for L in parser recogniser; do bash "$R/joined.sh" --vary parser --dump $T/base/dump.jsonl --ref-a main --ref-b main --layer $L --out $T/jb-$L && grep -x '## Flips: 0' $T/jb-$L/report.md; done` — green: exit 0 and the line, each.
- V7: `bash "$R/bench.sh" --ref $BREF --out $T/gate >/dev/null; echo "exit=$?"; cmp $F $T/gate/report.md; echo "cmp=$?"` — green: `exit=0`, `cmp=0`.
- V8: V3's `py` line over `$R/Bench.cs $R/JoinedInput.cs $R/stage.sh $R/BenchPaced.cs` — green: a file's CRLF count is 0 where `$T/eol-before.txt`'s is 0, else equals its LF count; `BenchPaced.cs` as `Bench.cs`.
- V9: plan-4 V12's `w` function, then `w <file> '<text>'` for: in `JoinedInput.cs` the `ReadDump(string path, long? pace)` signature, the four new fields' declarations (`Dump.Pace`, `DumpResult.Wall`, `DumpCase.Pushes`, `DumpCase.Reads`), `expected none (a fast dump)`, ``without `pace` in the header``, ``: no `pace`, expected``; in `Bench.cs` the `Selected`, `FireFrame`, `FrameOffset` signatures; in `BenchPaced.cs` `ReadSamples = 4096`, `PacedPadding = 48000`, `"unpaced"`, `"no measured reads"`, `more than 4096 apart`; in `stage.sh` :115's new text — green: `1` each, `more than 4096 apart` `1` or `2`.
- V10: `git diff --name-only main -- 'NativeBridge~' Runtime 'Tests~'; git diff --name-only HEAD -- $R/Joined.cs $R/JoinedRecogniser.cs $R/JoinedScorer.cs $R/JoinedReport.cs $R/Program.cs $R/DocCheck.cs $R/Rig.csproj $R/joined.sh $R/bench.sh $R/BenchMetrics.cs $R/BenchReport.cs; git status --porcelain` — green: nothing; nothing; only the files table's paths (and this plan's docs if uncommitted; `?? memory/**.meta` excepted).

**Stops:** V1 stops only for an item missing at the hub (README :56, the human's download); any other `NOT provisioned` follows README :48, :54 in this branch tree; a V2 toolchain path absent, or harness exit 1, stops for the human, never re-pinned; V3 without `fresh` stops (never re-capture, never overwrite `$F`); V5 `cmp=1` or a `FAIL`, V6 a flip, V7 `cmp=1` stop for the human with `diff` into `$T/`, never a rounding fix.

## Open questions that block implementation

None.

## Blast radius

Every stage compiles the edited `JoinedInput.cs` (DocCheck, parser and recogniser joined runs, `bench.sh`); every recogniser stage compiles `BenchPaced.cs`; `metrics-check`'s count goes 26 → 49; JoinedInput's stripped comments; Phases 2–3's fast-only compares use `--ref 5f70937` and `$F`; the architecture's `stage.sh :113-115` cite (as-built reconcile); `.scratch/bench-paced-mode/`; V2 writes ignored `NativeBridge~/build/`, `build-windows/`, `build-confgen/`, `vendor/overlaid/`; V1 `vendor/`.

## Runtime claims

- The extraction and the overload leave the fast report's bytes unchanged (V7).
- Two fast runs on one `--ref` repeat every byte, `Staged from` included (V7; bench-fast-replay plan-4 V5).
- `bench.sh --ref <sha>` stages and builds through `git show` under Claude's Git Bash (V3, V7).
- `metrics-check`'s first 26 `ok`/`FAIL` lines are toys 1–26 (V3, V5).
- A temp file under `%TEMP%` is writable from the stage (rows 40–49, V5).
- The hub holds the provisioned items and Command 3's toolchains exist (V1, V2).

Hazards: V1–V3 precede the first edit, and V3 writes `$F` once, only on `fresh` in the same command, never overwritten here or by a later phase; the four files are one change, no stage build between; V5 needs V3's `mc-before.txt`, V6 V3's dump; V2 runs in this branch tree only, never a writer tree.
