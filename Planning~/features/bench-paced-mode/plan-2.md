# Phase 2 — persisted plan (2026-11-09)

Scope: `main.cpp`'s flag-gated `--paced <samples>` mode (t0 and the schedule, the yield-spin loop, the padding scheme, the paced dump keys), the harness `README.md` usage and note, and `--bench matched-check` with `Program.cs` `BenchVerbs` and the `Rig.csproj` comment, probed on the 52 cases at 16384 and at 960; this plan also takes on `Bench.cs` (the verb, fast mode's prelude shared with it) and `BenchPaced.cs` (the two-dump check, per *Code home*), which the row implies but does not name, and a refusal of `--paced` with `--write-baseline`; nothing of Phase 3.

## Files to change

`R` is `Planning~/features/coverage-in-selection/ab-rig`, `H` is `NativeBridge~/harness`. Serial: one coupled change, one gate.

| Path | Action | Why |
|---|---|---|
| `H/main.cpp` | modify | register *Paced push path*, *Pacing*, *Flush padding*, *Stamps*; F1, F10 |
| `H/README.md` | modify | row 2: usage and a note; :108 as it reads under the flag |
| `R/Bench.cs` | modify | the verb and its dispatch; the prelude shared (*Matched-chunk check*: "exactly that prelude"); unnamed in the row (recon scope flags 2-3) |
| `R/BenchPaced.cs` | modify | *Code home*: it holds the matched check; pairing and the stop, which Phase 3's `run` reuses |
| `R/Program.cs` | modify | `BenchVerbs` :154, its comment :246-247 |
| `R/Rig.csproj` | modify | the `flag bench` comment :21-23; :30-33 holds as written |

Unedited (V14): against `main`, `NativeBridge~/src/`, `H/expectations.json`, `H/CMakeLists.txt`, `Tests~`, `Runtime/`; against `HEAD`, `JoinedInput.cs`, `Joined.cs`, `JoinedRecogniser.cs`, `JoinedScorer.cs`, `JoinedReport.cs`, `BenchMetrics.cs`, `BenchReport.cs`, `DocCheck.cs`, `stage.sh` (already copies `BenchPaced.cs`, :115), `bench.sh`, `joined.sh`.

## Contracts and interfaces

**main.cpp** (no new comment, rule 1 as written; `#include <array>` added):
- State after `dump_writer` (:82): `struct PacedTrace { uint32_t chunk = 0; std::chrono::steady_clock::time_point t0; std::vector<std::array<int64_t, 3>> pushes, reads; };` and `PacedTrace paced;` — `chunk` 0 is off. `int64_t Micros()`: whole µs since `paced.t0`.
- Arguments: `else if (arg == "--paced" && i + 1 < argc) paced_arg = argv[++i];` (`std::optional<std::string> paced_arg` beside :222); :232's usage ends `[--write-baseline] [--dump <file> [--paced <samples>]]`. After :234-235, with `paced_arg` set, in this order, each a `Fail` (exit 2) before the model loads: no `--dump` → `--paced needs --dump`; `--write-baseline` → `--paced cannot write the baseline`; not 1–5 digits, or outside 1–16384 → `--paced takes a chunk of 1 to 16384 samples: <value>`; else `paced.chunk` is the value.
- Header, after `decoderRate` (:336): `if (paced.chunk != 0) header["pace"] = paced.chunk;`.
- `DrainResults`: first in the `dump_writer.enabled` block (:115) `const int64_t wall = paced.chunk != 0 ? Micros() : 0;`; after `rec["origin"]` (:126) `if (paced.chunk != 0) rec["wall"] = wall;`.
- `DumpCaseEnd`, after `rec["errors"]` (:100): `if (paced.chunk != 0) { rec["pushes"] = paced.pushes; rec["reads"] = paced.reads; }` — integer triples `[end, due, made]` and `[consumed, at, pushed]`, empty arrays allowed. Schema 2, LF records: the formats recon section 2 quotes from the reader (`JoinedInput.cs` :218-234, :334-337, :407-418, `Triples` :914-930).
- `int64_t Poll(FixtureOutcome& r)`, one spin iteration: `vosk_bridge_get_consumed()`, then `at = Micros()`; a value unlike the last seen (0 at t0) appends `{consumed, at, r.pushed}` to `reads`; then `DrainResults(r)`; returns `at`. The three loops below call `std::this_thread::yield()` between iterations; each stall bound is 10000000 µs without progress.
- `bool PushPaced(const float* data, size_t count, FixtureOutcome& r)`: pushes of `paced.chunk` (the last shorter), sequence position `r.pushed`; a push ending at `end` is due at `(end * 1000000 + 47999) / 48000` µs; once `at >= due` it pushes what is left of the chunk, a short write retried on the next iteration; a completed chunk appends `{end, due, at}`, `at` that of the completing call; `written < 0` → :163-164's error, `false`; the stall → :171's error, `false`.
- `template <class Pause, class Stalled> std::string WaitFrontEndBy(uint64_t samples, uint64_t* out_output_samples, uint64_t* out_digest, Pause pause, Stalled stalled)`, before :185: :186-205 moved, :191's sleep becoming `pause();` and :195-201 `const bool moved = now != seen; if (moved) seen = now; if (stalled(moved)) return` :199-200's text. `WaitFrontEnd` keeps its signature and callers (:371, :385, :452); its body is `return WaitFrontEndBy(samples, out_output_samples, out_digest, [] { std::this_thread::sleep_for(std::chrono::milliseconds(5)); }, [n = 0](bool moved) mutable { if (moved) n = 0; return !moved && ++n > 2000; });`, :190-202's behaviour exactly.
- `std::string WaitFrontEndPaced(uint64_t samples, uint64_t* out_output_samples, uint64_t* out_digest, FixtureOutcome& r)`: `int64_t at = Micros(), since = at;`, then returns `WaitFrontEndBy` with pause `[&] { std::this_thread::yield(); at = Poll(r); }` and stalled `[&](bool moved) { if (moved) since = at; return at - since > 10000000; }`.
- `bool SpinConsumed(FixtureOutcome& r)`: until consumed equals `r.pushed`; stall → :472-474's error, `false`.
- Per case: the loop's top (:426) clears `pushes` and `reads` under the flag. After a good `vosk_bridge_start_push` (:445): `if (paced.chunk != 0)` — `paced.t0 = std::chrono::steady_clock::now();`, `PushPaced` the clip; on `true`, `WaitFrontEndPaced(clip size, …)`; on `""`, :454-455, then `PushPaced(padding.data(), 48000, r)`, on `true` `SpinConsumed`, on `true` `PushAll(padding.data() + 48000, padding.size() - 48000, r)`; a wait error as :458 — `else` today's :450-460, body unchanged. Then :461-488 as today. `reads` spans t0 to `SpinConsumed`; flush results carry `wall`.
- At 16384 the paced padding pushes are 16384, 16384, 15232, so the flush's read grid sits 1152 samples off fast mode's: under one read.

**Bench.cs**:
- `sealed class Prelude { internal string[] StagedFrom; internal JoinedInput.Dump Dump; internal JoinedInput.Manifest Manifest; internal JoinedInput.Pin Pin; internal JsonElement Voice; internal long EndCases; internal long EndPassed; }`; `static Prelude FastPrelude(string dumpPath, long? pace, string manifestPath, string pinPath, string root)` is `FastRun`'s :134-220 moved, :137 reading `ReadDump(dumpPath, pace)`. `FastRun` keeps :131-133, calls `FastPrelude(dumpPath, null, manifestPath, pinPath, root)` and binds today's local names from it, so :222-289 are unchanged.
- `Run`, after `truth-check` (:63): `matched-check` needs `args.Length == 14`, `args[2]`…`args[12]` = `--dump`, `--manifest`, `--pin`, `--fixtures-root`, `--matched`, `--pace`, and `long.TryParse(args[13], NumberStyles.None, CultureInfo.InvariantCulture, out long p)` within 1–16384, else `Stop(2, "usage: --bench matched-check --dump <dump.jsonl> --manifest <manifest.json> --pin <expectations.json> --fixtures-root <dir> --matched <matched.jsonl> --pace <samples>")`; then `MatchedCheck(args[3], args[5], args[7], args[9], args[11], p)`.
- `static int MatchedCheck(string dumpPath, string manifestPath, string pinPath, string root, string matchedPath, long pace)`: `FastPrelude(dumpPath, 0, …)`; `reading = matchedPath`; `ReadDump(matchedPath, pace)`, schema ≠ 2 → :139's stop for `matchedPath`; `reading = null`; `BenchPaced.CheckMatched(prelude.Dump, matched)`; stdout `matched-check: <N>/<N> cases' finals and first partials within 4096 samples of the fast dump's at pace <pace>.`, then, summing `BenchPaced.Reads(c.Reads, c.Samples)` over the matched cases, `matched-check: read trace: <measured> measured reads, <merged> merged changes in <cases with merged > 0>/<N> cases.`; returns 0.
- Header :3 Purpose gains `, the matched-chunk check` after `the fast run`; Owns `Bench (internal static class), Bench.Prelude (sealed class)`.

**BenchPaced.cs**: `internal static void CheckMatched(JoinedInput.Dump fast, JoinedInput.Dump matched)`: matched cases by name (`StringComparer.Ordinal`, `TryAdd`; a repeat → `Stop(2, matched.Path + ": case \"<name>\" twice")`); names in one dump only (fast's order, then matched's) → `Stop(2, matched.Path + ": cases differ from the fast dump's: only in fast " + Joined.JsonArray(…) + ", only in matched " + Joined.JsonArray(…))`; per fast case in order, a matched case with `Failures` → `matched-check: case "<name>": failed decode in the matched dump: <Failures joined "; ">`, else `Matched(f, m)` where non-null; any line → those lines and `matched-check failed on <k>/<N> cases against the fast dump <fast.Path>: no numbers, no report`, `Stop(3, …)`. Header Purpose's `the matched-chunk compare` reads `the matched-chunk compare and its check over two dumps`.

**Program.cs** :154 `{ "metrics-check", "truth-check", "run", "matched-check" }`; :246 as is, :247 `// run, matched-check) is dispatched above, to Bench's hook.` **Rig.csproj** :22-23 become three lines at :22's indent, broken after `(bench-fast-replay)` and after `(bench-paced-mode);`: `or metrics-check, truth-check, run (bench-fast-replay) or matched-check (bench-paced-mode); each a recogniser stage`. These comment edits and the two headers override rule 1 at those sites (lessons).

**README.md**: :108 opens ``Without `--paced`, the harness pushes faster than real time;``, the rest unchanged; then a bullet: paced mode, `--dump <file> --paced <samples>` — each case in `<samples>`-sample chunks on a real-time schedule from the case's start (a chunk due when its last sample would be captured at 48 kHz), the padding's first second paced, the rest unpaced; the dump gains header `pace`, result `wall`, case-end `pushes` and `reads`, integer µs from the case's start; exit 2 without `--dump`, with `--write-baseline`, or outside 1–16384; the audio bench's mode (`Planning~/features/bench-paced-mode/`), never Command 3's.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| `PacedTrace paced` beside `dump_writer` | fields on `FixtureOutcome` (:53-68) | main.cpp's opt-in sink pattern; `DrainResults`, `DumpCaseEnd` read it unchanged in signature; the stdout report's struct untouched |
| paced block as `if … else` today's :450-460 | flag tests inside `PushAll`, `WaitFrontEnd` | register: `PushAll` reused unchanged; `WaitFrontEnd`'s signature and behaviour kept (F10) |
| `WaitFrontEndBy` shared by both waits, pause and stall bound passed in | a `WaitFrontEndPaced` copy of :185-206 | register: `WaitFrontEnd`'s condition reused; lessons (reuse); one condition, no drift |
| due rounded up to whole µs | down | "at or after its due time" never early; 960's dues exact (T4) |
| stall bound 10 s of wall time | 2000 iterations | spin iterations are µs apart; fast mode's ~10 s backstop (:170) |
| `--paced` refused with `--write-baseline` | accepted | F10; README re-baselining: pins come from the default run |
| flush untraced | trace it | `BenchPaced.Reads` uses changes within the clip; :461-478 untouched |
| `FastPrelude` shared | a copy in the verb | register "exactly that prelude"; lessons (reuse); Phase 3's `run` calls it |
| fast dump at pace 0 in the verb, null in `FastRun` | null in both | 0 refuses a paced dump given as fast; `FastRun` unchanged (F10) |
| `--pace` argument | 16384 fixed in the rig | the 960 probe makes Phase 3's reader call; the line names the pace; F6's verdict is V10's 16384 run |
| `CheckMatched` in `BenchPaced.cs` | in `Bench.cs` | *Code home*; Phase 3's `run` calls it |
| merged count printed by the verb | a script recount | one counter, `BenchPaced.Reads` (:62); no report section |
| mutation shift 8193 | 4097 | a green matched stamp may lie 4096 from fast's; 8193 keeps the moved one beyond 4096 of it |
| no new toys, 49 | toys for pairing | no arithmetic added; V11, V12 run the paths |

## Acceptance criteria

- [ ] F10 pinned run: Command 3 52/52 (V3).
- [ ] F10: fast-only report `cmp=0` against `$F`, fast dump free of paced keys (V8); DocCheck (V5); toys 49/49 (V6); joined 0 flips, both layers (V7).
- [ ] F1 push path; §6 input stops: the harness refusals (V4); the 16384 and 960 passes exit 0 or 1 (V9, V12).
- [ ] *Stamps*: both paced dumps read at their pace (V10, V12); a fast dump refused as matched (V12).
- [ ] F6: matched-check 52/52 at 16384 and the merged-change count recorded (V10); the mutation exits 3 naming the case, no report (V11).
- [ ] Line endings (V2); the texts V13 lists (V13); untouched files (V14).

**Steps:** one Git Bash at the tree root: `R=Planning~/features/coverage-in-selection/ab-rig T=.scratch/bench-paced-mode/p2 F=.scratch/bench-paced-mode/baseline-report.md BREF=5f70937; mkdir -p $T/mut`; `E` runs plan-1 V3's `py` line over `NativeBridge~/harness/main.cpp NativeBridge~/harness/README.md $R/Bench.cs $R/BenchPaced.cs $R/Program.cs $R/Rig.csproj`; `w` is bench-fast-replay plan-4 V12's. After V8: `O=$(cygpath -am $T/gate) M=$(cygpath -am Tests~/Fixtures/audio/manifest.json) P=$(cygpath -am NativeBridge~/harness/expectations.json) X=$(cygpath -am Tests~/Fixtures)`; `HP() { (cd NativeBridge~ && ./build-windows/bin/vosk-bridge-harness.exe --model "$O/model" --fixtures "../Tests~/Fixtures/audio/tts" --manifest harness/expectations.json --paced "$1" --dump "$O/$2"); }` (bench.sh :123-125 plus `--paced`); `MC() { (cd $T/gate/stage && dotnet run --no-build -- --bench matched-check --dump "$O/dump.jsonl" --manifest "$M" --pin "$P" --fixtures-root "$X" --matched "$1" --pace "$2"); }` (bench.sh :136-137's inputs). V1 before any edit; the six files as one change; V2–V14 in order. Command 4 is not triggered; Commands 1–2 as the binding sets them.

- V1: `test -e $F && echo base; git status --porcelain -- $R NativeBridge~/harness | grep -c .; E | tee $T/eol-before.txt` — green: `base`, `0`, six lines each CRLF = LF.
- V2: `E` — green: each CRLF = LF; a file short of it is converted bytes-only (`py -I -B -c "import sys;p=sys.argv[1];d=open(p,'rb').read().replace(b'\r\n',b'\n').replace(b'\n',b'\r\n');open(p,'wb').write(d)" <file>`) and V2 re-run, before V3.
- V3, Command 3: plan-1 V2 verbatim, with this `$T` — green as there: the path, block exit 0, `1`, `exit=0`, `3`.
- V4: `cp NativeBridge~/harness/expectations.json $T/pin.json; for a in "--paced 16384" "--paced 0 --dump ../$T/s.jsonl" "--paced 16385 --dump ../$T/s.jsonl" "--paced 960 --dump ../$T/s.jsonl --write-baseline"; do (cd NativeBridge~ && ./build-windows/bin/vosk-bridge-harness.exe --model vendor/overlaid/continuous-default --fixtures "../Tests~/Fixtures/audio/tts" --manifest ../$T/pin.json $a) 2>&1 >/dev/null | tail -n 1; echo "exit=${PIPESTATUS[0]}"; done; cmp $T/pin.json NativeBridge~/harness/expectations.json && test ! -e $T/s.jsonl && echo untouched` — green: `vosk-bridge-harness: --paced needs --dump`, `…: --paced takes a chunk of 1 to 16384 samples: 0`, `…: 16385`, `…: --paced cannot write the baseline`, each `exit=2`; `untouched`.
- V5: `Planning~/verification-recipe.md` :229 verbatim — green per :232.
- V6: `env -u MSYS_NO_PATHCONV bash "$R/stage.sh" WORKTREE $T/stage recogniser && (cd $T/stage && dotnet build -v q --nologo && dotnet run --no-build -- --bench metrics-check) > $T/mc.txt; echo "exit=$?"; grep -c '^FAIL' $T/mc.txt; tail -n 1 $T/mc.txt` — green: `exit=0`, `0`, `49/49 metric checks verified against hand-computed toy cases.`
- V7: plan-1 V6 with `--dump .scratch/bench-paced-mode/p1/base/dump.jsonl` and `--out $T/jb-$L` — green: exit 0 and `## Flips: 0`, each layer.
- V8: `bash "$R/bench.sh" --ref $BREF --out $T/gate >/dev/null; echo "exit=$?"; cmp $F $T/gate/report.md; echo "cmp=$?"; grep -c -E '"(pace|wall|pushes|reads)":' $T/gate/dump.jsonl` — green: `exit=0`, `cmp=0`, `0`. `$T/gate/dump.jsonl` is the probe's fast dump.
- V9: `SECONDS=0; HP 16384 matched.jsonl > $T/gate/matched.json 2> $T/gate/matched.err; echo "exit=$? secs=$SECONDS"; grep -E '"(passed|failed|total)"' $T/gate/matched.json` — green: `exit=0` or `exit=1` (counts recorded); expected about 245 s (§4.6: 193 s of audio plus 52 × 1 s paced), plus the lifecycle check and the flushes.
- V10: `MC "$O/matched.jsonl" 16384 > $T/mc-16384.out 2> $T/mc-16384.err; echo "exit=$?"; cat $T/mc-16384.out` — green: `exit=0`; `self-check: 52/52 cases' finals equal the pin.`; the `matched-check: 52/52 …at pace 16384.` line; the read-trace line, its merged count recorded for the gate (Risks, *Read separability*).
- V11: `N=$(py -I -B -c "import json,sys;s,o=sys.argv[1:];L=open(s,'rb').read().decode('utf-8').split('\n');R=[(i,json.loads(t)) for i,t in enumerate(L) if t and json.loads(t)['kind']=='result'];C=[(i,r,R[k-1][1]['consumed'] if k and R[k-1][1]['case']==r['case'] else 0,min(r['pushed'],R[k+1][1]['consumed'] if k+1<len(R) and R[k+1][1]['case']==r['case'] else r['pushed'])) for k,(i,r) in enumerate(R) if r['final'] and r.get('payload',{}).get('text')];H=[(i,r,v) for i,r,lo,hi in C for v in (r['consumed']-8193,r['consumed']+8193) if lo<=v<=hi][:1];H or sys.exit(1);i,r,v=H[0];a='\"consumed\":%d,'%r['consumed'];L[i].count(a)==1 or sys.exit(1);L[i]=L[i].replace(a,'\"consumed\":%d,'%v);open(o,'wb').write('\n'.join(L).encode('utf-8'));print(r['case'])" $T/gate/matched.jsonl $T/mut/matched.jsonl); echo "py=$? $N"; MC "$(cygpath -am $T/mut/matched.jsonl)" 16384 > /dev/null 2> $T/mut.err; echo "exit=$?"; grep -c -F "bench: matched-check: case \"$N\": final " $T/mut.err; grep -c -F 'bench: matched-check failed on 1/52 cases against the fast dump ' $T/mut.err; ls $T/mut` — green: `py=0` and a case name, `exit=3`, `1`, `1`, `matched.jsonl` only.
- V12: `SECONDS=0; HP 960 paced.jsonl > $T/gate/paced.json 2> $T/gate/paced.err; echo "exit=$? secs=$SECONDS"; MC "$O/paced.jsonl" 960 > $T/mc-960.out 2> $T/mc-960.err; echo "read=$?"; MC "$O/dump.jsonl" 16384 2>&1 >/dev/null | grep -c -F ': no `pace`, expected 16384'; MC "$O/matched.jsonl" 0 2>&1 >/dev/null | grep -c -F 'bench: usage: --bench matched-check'` — green: `exit=0` or `exit=1`, about 245 s as V9; `read=0` or `read=3` (exit 3 lines are the 20 ms finding's preview, recorded, never a stop); `1`; `1`.
- V13: `w <file> '<text>'` for: in main.cpp `--paced needs --dump`, `--paced cannot write the baseline`, `--paced takes a chunk of 1 to 16384 samples: `, `[--dump <file> [--paced <samples>]]`, `header["pace"]`, `rec["wall"]`, `rec["pushes"]`, `rec["reads"]`, `std::min<size_t>(16384, count - offset)`, the `WaitFrontEndBy` parameter list; in Bench.cs the `FastPrelude` and `MatchedCheck` signatures, `FastPrelude(dumpPath, null,`, the usage, `the fast run, the matched-chunk check`, `Bench (internal static class), Bench.Prelude (sealed class)`; in BenchPaced.cs the `CheckMatched` signature, `failed decode in the matched dump: `, `cases differ from the fast dump's: only in fast `, `the matched-chunk compare and its check over two dumps`; in Program.cs :154's and :247's new text; in Rig.csproj its new text; in README.md ``Without `--paced`, the harness pushes faster than real time;``, `--dump <file> --paced <samples>` — green: `1` each.
- V14: `git diff --name-only main -- NativeBridge~/src NativeBridge~/harness/expectations.json NativeBridge~/harness/CMakeLists.txt 'Tests~' Runtime; git diff --name-only HEAD -- $R/JoinedInput.cs $R/Joined.cs $R/JoinedRecogniser.cs $R/JoinedScorer.cs $R/JoinedReport.cs $R/BenchMetrics.cs $R/BenchReport.cs $R/DocCheck.cs $R/stage.sh $R/bench.sh $R/joined.sh; git status --porcelain` — green: nothing; nothing; only the files table's paths (and this plan's docs if uncommitted; `?? memory/**.meta` excepted).

**Stops:** V3 a toolchain path absent or harness exit 1 → the human, never re-pinned; V6 a `FAIL`, V7 a flip, V8 `cmp=1` → the human, `diff` into `$T/`, never a rounding fix; V9 or V12 exit 2 → stop with the `.err` file and the report's case `errors` (§6); **V10 exit 3 is adr-0001's kill criterion: a stop for the human and a design reopen — never a wider tolerance, a rounding fix or a code change to pass**, `$T/gate` kept; V11 `py=1` (no final movable within the reader's rules) → the human; any need to touch `NativeBridge~/src/` or `vosk_bridge.h` → a scope stop.

## Open questions that block implementation

None.

## Blast radius

The rebuilt harness binary behind Command 3 and every `bench.sh` run; every recogniser stage compiles the `Bench.cs` and `BenchPaced.cs` changes (DocCheck, joined, bench); `Documentation~/native-bridge.md` :153 (G2's product docs); the architecture's `Bench.cs` line cites (as-built reconcile); Phase 3 builds on `FastPrelude`, `CheckMatched`, `--pace`; `.scratch/bench-paced-mode/p2/`; V3 rewrites the ignored `NativeBridge~/build*/`, `vendor/overlaid/`.

## Runtime claims

- The flag-gated harness, `WaitFrontEnd` through `WaitFrontEndBy` included, leaves the default path's results unchanged: Command 3 52/52 (V3), the fast report byte-equal (V8).
- The paced 16384 pass reproduces fast mode's stamps within 4096 — the riskiest assumption (V10).
- `vosk_bridge_get_consumed()` reads 0 right after `vosk_bridge_start_push` (V10's trace line).
- nlohmann writes `std::vector<std::array<int64_t, 3>>` as the nested integer arrays `Triples` reads (V10, V12).
- The front end reaches clip input before the padding inside the spin (V9 exit not 2).
- Each paced pass takes about 245 s plus overhead (V9, V12).
- `cygpath -am` runs in Claude's Git Bash, and V8's `$T/gate/stage` holds the worktree's rig files (stage.sh :109-115) (V9-V12).
- The harness's argument checks fail before any dump or pin is opened (V4).

Hazards: V1 precedes the edit; the six files are one change, no build between; V2 precedes V3; V9–V12 need V8's `$T/gate` (model, dump, stage) and V11 V9's dump — no `bench.sh` re-run into `$T/gate` after V8; never run `bench.sh`, `joined.sh` or the harness passes concurrently with `host_run.py` (it renames `Tests~`) or with each other; V9 and V12 spin one core, so nothing heavy alongside; V4 runs on a pin copy only; `$F` is never written.
