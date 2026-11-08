# Phase 4 — persisted plan (2026-11-08)

Scope: `bench.sh` gains `--profile`, `--mode`, `--gain` and, for a named configuration, writes the harness's pin copy under `<out>` with `py` and hands it to the harness; `--bench run` carries profile and mode to the header, which names them and the copy; every §9 criterion is re-made, Command 3 included and #8 listed for G2 as human-only; nothing else.

## Files to change

`R` is `Planning~/features/coverage-in-selection/ab-rig`. All three are LF and stay LF; no CRLF file is touched.

| Path | Action | Why |
|---|---|---|
| `R/bench.sh` | modify | *Configuration* (its non-pinned half), *Fast mode is the harness*, *Input boundary*; header :3, :6 |
| `R/Bench.cs` | modify | *Report*: "the configuration" reaches the header |
| `R/BenchReport.cs` | modify | *Report*; F10 for non-default values |

Unedited, diffed against `HEAD` (V14): `Joined.cs`, `JoinedInput.cs`, `JoinedRecogniser.cs`, `JoinedScorer.cs`, `JoinedReport.cs`, `Program.cs` (its bare `--bench` block included), `DocCheck.cs`, `Rig.csproj`, `joined.sh`, `stage.sh`, `BenchMetrics.cs`.

## Contracts and interfaces

- **Pinned or named.** A run is *named* when any of `--profile`, `--mode`, `--gain` is given, a value equal to the pin's included; else *pinned*, byte-identical to Phase 3 (confgen, harness and `--bench run` command lines, report). `--profile` alone runs mode `Continuous`, `--mode` alone profile `Automatic` (confgen always gets both, so its `--mode needs --profile` never arises); an unnamed gain keeps the pin's.
- **bench.sh options.** :17 adds `PROFILE="" MODE="" GAIN=""`; the accepting `case`'s arm list (:27-31, at :28) gains `| --profile | --mode | --gain`; the dispatch `case` (:32-38) gains, after `--out)`, `--profile) take PROFILE "$opt" "$2" ;;`, `--mode) take MODE "$opt" "$2" ;;`, `--gain) take GAIN "$opt" "$2" ;;` (`given twice`, `needs a value` hold). After :43, in order: `NAMED=1` if `$PROFILE$MODE$GAIN` is non-empty, else `NAMED=""`; a profile neither empty nor `Automatic`, `ModelDefault`, `Responsive` → `die "--profile must be Automatic, ModelDefault or Responsive: $PROFILE"`; a mode neither empty nor `Continuous`, `PushToTalk` → `die "--mode must be Continuous or PushToTalk: $MODE"`; `GAIN_RE='^(0|-[0-9]*[1-9][0-9]*(\.[0-9]+)?|-[0-9]+\.[0-9]*[1-9][0-9]*)$'` (`0`, or a minus and a non-zero digit: `-0`, `-0.0`, `-00` fail), a non-empty gain failing `[[ $GAIN =~ $GAIN_RE ]]` → `die "--gain must be 0 or a negative decimal in dBFS, as -18 or -12.5: $GAIN"`. After :47: `PROFILE="${PROFILE:-Automatic}"`, `MODE="${MODE:-Continuous}"`; with `NAMED`, an `$OUT` that contains `..` or does not begin `.scratch/` and one more character → `die "--out must lie under .scratch/ for a named configuration (the harness's pin copy is written there): $OUT"`. After :60, with `NAMED`, `command -v py >/dev/null 2>&1` failing → `die "no py on PATH: a named configuration writes the harness's pin copy with py"`. Every one fires before :62, so nothing is written or deleted.
- **Clean.** :66's names gain `-o -name harness-pin.json`.
- **confgen.** :87's literals become `--profile "$PROFILE" --mode "$MODE"`.
- **Pin copy.** After :88, `HPIN="harness/expectations.json"`; with `NAMED`: `rc=0; py -I -B -c "<COPY>" "$(abspath "NativeBridge~/harness/expectations.json")" "$OUT_W/model/conf/model.conf" "$GAIN" "$OUT_W/harness-pin.json" || rc=$?`, non-zero → `die "pin copy: py exit $rc: no report"`, then `HPIN="$OUT_W/harness-pin.json"`. `<COPY>` is `import json,sys;s,c,g,o=sys.argv[1:];p=json.load(open(s,encoding='utf-8'));p['modelConf']=open(c,'rb').read().decode('utf-8');g and p.__setitem__('gain',float(g));open(o,'w',encoding='utf-8',newline='\n').write(json.dumps(p,ensure_ascii=False,indent=4)+'\n')`. The source is always the committed pin, never `--pin` (the self-check's); the committed file is only read. The conf is read as bytes, so its line endings and trailing newline reach `modelConf` as the harness compares them (`main.cpp` :271); every other key, case and key order survives the round trip, which the harness parses, so formatting is free.
- **Harness.** :92's `--manifest harness/expectations.json` becomes `--manifest "$HPIN"` (absolute when named; cwd `NativeBridge~`). Exit 0 or 1 continues, 2 stops, unchanged.
- **bench run.** :106 gains, after `--report "$OUT_W/report.md"`, `${NAMED:+--profile "$PROFILE" --mode "$MODE"}` (the `--buffer` form at :101).
- **Bench.Run.** Before :66, `bool named = args.Length == 18 && args[14] == "--profile" && args[16] == "--mode";`; :67 `args.Length != 14` becomes `(args.Length != 14 && !named)`; the usage becomes `usage: --bench run --dump <dump.jsonl> --manifest <manifest.json> --pin <expectations.json> --fixtures-root <dir> --harness-exit 0|1 --report <report.md> [--profile <name> --mode <name>]`; the `FastRun` call gains `named ? args[15] : null, named ? args[17] : null` after `args[13]`. Values are not re-checked (bench.sh checked them).
- **Bench.FastRun.** `static int FastRun(string dumpPath, string manifestPath, string pinPath, string root, int harnessExit, string reportPath, string profile, string mode, string grammarJson, VoxrSlotDefinition[] slots, VoxrCommandDefinition[] commands)`; the header initializer opens `Profile = profile, Mode = mode,`. Nothing else: the self-check still reads `--pin`, finals only.
- **BenchReport.** `Header` gains `internal string Profile;` and `internal string Mode;` before `Gain` (null: pinned). `Build` :79 prints, for `h.Profile == null`, the Phase 3 literal unchanged, else `"- Endpointer profile: " + h.Profile + ", mode " + h.Mode + " (confgen, named configuration)"`; after the `modelConf` line, when `h.Profile != null`, `- Harness pin: a copy of the committed pin, gain and modelConf replaced by this run's`. `- Gain:` is untouched: `dump.Gain` is the copy's. No path is printed (F12); a pinned report is unchanged.
- **Comments (rule 1).** `Bench.Run`, `Bench.FastRun`, `BenchReport.Header`, `BenchReport.Build` hold none and gain none; the `.cs` headers stay. bench.sh :3 reads `# Purpose:  Run script for the bench's fast run: confgen, the harness's --dump run (on a pin copy for a named configuration), stage and build a ref, --bench run`; :6 reads `# Depends:  stage.sh, Bench, vosk-bridge-harness, confgen, py`.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| Named = any of the three given | only values off the pin | the register's "named"; no resolution table in bash (`--profile Responsive` resolves to the pinned conf) |
| bench.sh checks profile and mode names | confgen's exit 2 | stops before `<out>` is cleaned; confgen re-checks |
| Gain `0` or a negative decimal | any finite value | the front end clamps above 0 (`NativeBridge~/src/frontend/front_end.cpp` :50), so the header would name a gain never applied |
| Copy by `py` between confgen and harness | a `--bench` verb | a verb needs the stage built before the harness, reordering *Flow*; `py` is in *Assets* |
| `<out>/harness-pin.json`, named `--out` under `.scratch/` | a fixed `.scratch/` path | register: a `.scratch/` copy; one per `<out>`, no clobbering; a textual test, no path normalisation |
| Profile and mode as an optional `--bench run` pair | an environment variable | explicit, usage-checked; gain needs none, the dump carries it |
| Pinned header unchanged | a "pinned" line | V4 compares it byte-equal to Phase 3's; "as Command 3" says it |
| Command 3 last | first | V4 compares against V1's baseline on the same binaries |

## Acceptance criteria

- [ ] Preamble: joined `main`-versus-`main`, 0 flips at both layers (V2); DocCheck `66/66` (V3).
- [ ] §9 #1, #9 (pinned): green 52-case run, F10 fields, report byte-equal to Phase 3's (V4).
- [ ] §9 #3 / F12: two runs byte-identical (V5).
- [ ] §9 #6, #7: `23/23`, `52/52`; run usage (V6).
- [ ] §9 #2 / F3: the pin mutation exits 3, names the case, no report (V7).
- [ ] §4.2, F10 non-default, the register's copy: a named run with numbers (V8); the row's run, numbers or a named refusal (V9).
- [ ] Each option's stop (V10).
- [ ] Line endings (V11); new strings and the signature verbatim (V12).
- [ ] §9 #4: Command 3 green 52/52 (V13).
- [ ] §9 #5, F13: `git diff main` empty; committed pin unchanged (V14).
- [ ] §9 #8: human-only; listed for G2, never run or claimed.

**Steps:** one Git Bash at the tree root, first `R=Planning~/features/coverage-in-selection/ab-rig P=.scratch/bench-probe T=.scratch/bench-p4 F=.scratch/bench/report.md; mkdir -p $T`; V1 before any edit; then all three files edited as one change before any stage build; then V2-V14 in order.

- V1: `bash "$R/bench.sh" --out $T/base >/dev/null; echo "exit=$?"` - green: `exit=0`.
- V2: `for L in parser recogniser; do bash "$R/joined.sh" --vary parser --dump $P/d1.jsonl --ref-a main --ref-b main --layer $L --out $T/jb-$L && grep -x '## Flips: 0' $T/jb-$L/report.md; done` - green: exit 0 and the line, each.
- V3: `Planning~/verification-recipe.md` :229 verbatim - green per :232, `66/66`.
- V4: `bash "$R/bench.sh"; echo "exit=$?"; cmp $T/base/report.md $F; echo "cmp=$?"; test ! -e .scratch/bench/harness-pin.json && echo "no copy"; py -I -B -c "import json,sys;print(json.loads(open(sys.argv[1],encoding='utf-8').readline())['manifest'])" .scratch/bench/dump.jsonl; for s in '- Endpointer profile: Automatic, mode Continuous (confgen, as Command 3)' '- Gain: -18' '- libvosk: ' '- modelConf: sha256 ' '- Grammar: `[' '- Staged from: `worktree at ' '- Recogniser settings (tick rate 90 Hz): ' '- Voice: ' '- Provenance: ' '- Seed: none — clean renders, no condition applied' '- Harness exit: 0' '- Self-check: 52/52'; do grep -F -c -e "$s" $F; done; grep -c -E -e '^- Harness pin:' -e '[A-Za-z]:/|\.scratch' $F` - green: `self-check: 52/52 cases' finals equal the pin.`, `exit=0`, `cmp=0`, `no copy`, `harness/expectations.json`, `1` each, `0`.
- V5: `bash "$R/bench.sh" --out $T/g2 >/dev/null; echo "exit=$?"; cmp $F $T/g2/report.md; echo "cmp=$?"` - green: `exit=0`, `cmp=0`.
- V6: `M=$(cygpath -am Tests~/Fixtures/audio/manifest.json) X=$(cygpath -am Tests~/Fixtures); (cd .scratch/bench/stage && dotnet run --no-build -- --bench metrics-check | tail -n 1; dotnet run --no-build -- --bench truth-check --manifest "$M" --fixtures-root "$X" | tail -n 1; dotnet run --no-build -- --bench run; echo "exit=$?")` - green: `23/23 …`, `52/52 …`, `bench: ` and the new usage, `exit=2`.
- V7: `py -I -B -c "import json,sys;p=json.load(open(sys.argv[1],encoding='utf-8'));[c['expectedFinals'].__setitem__(0,'cease fire mutated') for c in p['cases'] if c['file']=='cease_fire.wav'];json.dump(p,open(sys.argv[2],'w',encoding='utf-8'))" NativeBridge~/harness/expectations.json $T/pin-mut.json && bash "$R/bench.sh" --pin $T/pin-mut.json --out $T/mut; echo "exit=$?"; test ! -e $T/mut/report.md && echo "no report"` - green: `bench: self-check: case "cease_fire.wav": finals expected ["cease fire mutated"], actual ["cease fire"]`, `bench: self-check failed on 1/52 cases …`, `exit=3`, `no report`.
- V8: `pc() { py -I -B -c "import json,sys;a=json.load(open(sys.argv[1],encoding='utf-8'));d=sys.argv[2];b=json.load(open(d+'/harness-pin.json',encoding='utf-8'));c=open(d+'/model/conf/model.conf','rb').read().decode('utf-8');h=json.loads(open(d+'/dump.jsonl',encoding='utf-8').readline());print(b['modelConf']==c,h['modelConf']==c,h['manifest'].endswith('/harness-pin.json'),b['gain'],h['gain'],b['modelConf']==a['modelConf']);b['gain']=a['gain'];b['modelConf']=a['modelConf'];print(a==b)" NativeBridge~/harness/expectations.json "$1"; }; bash "$R/bench.sh" --profile Responsive --out $T/n2 >/dev/null; echo "exit=$?"; pc $T/n2; diff $F $T/n2/report.md` - green: `exit=0`; `True True True -18.0 -18.0 True`, `True`; `diff`'s `<`/`>` lines exactly `< - Endpointer profile: Automatic, mode Continuous (confgen, as Command 3)`, `> - Endpointer profile: Responsive, mode Continuous (confgen, named configuration)`, `> - Harness pin: a copy of the committed pin, gain and modelConf replaced by this run's`.
- V9, the row's run: `bash "$R/bench.sh" --mode PushToTalk --gain -12 --out $T/n1 >$T/n1.out 2>$T/n1.err; echo "exit=$?"; pc $T/n1; cat $T/n1.err; test -e $T/n1/report.md || echo "no report"; for s in '- Endpointer profile: Automatic, mode PushToTalk (confgen, named configuration)' '- Gain: -12' '- Harness pin: a copy of the committed pin'; do grep -F -c -e "$s" $T/n1/report.md; done; grep -F -x -c -e "$(grep -e '^- modelConf: ' $F)" $T/n1/report.md; grep -c -E '[A-Za-z]:/|\.scratch' $T/n1/report.md` - green: `pc` prints `True True True -12.0 -12.0 False`, `True`; then either numbers: `exit=0`, `1` each, `0`, `0`; or a named refusal: `exit=3`, `n1.err` holding `bench: self-check: case "…": finals expected …` lines and `bench: self-check failed on <k>/52 cases against the pin …: no numbers, no report`, `no report`.
- V10: `s() { bash "$R/bench.sh" "$@"; echo "exit=$?"; }; s --profile Fast --out $T/stop; s --mode Push --out $T/stop; s --gain 3 --out $T/stop; s --gain -1e2 --out $T/stop; s --gain -0 --out $T/stop; s --profile Automatic --profile Automatic; s --gain --out; s --gain -12 --out out-p4; s --mode Continuous --out .scratch/../out-p4; env PATH=/usr/bin:/bin bash "$R/bench.sh" --gain -12 --out $T/stop; echo "exit=$?"; test ! -e $T/stop && test ! -e out-p4 && echo "nothing written"` - green, each `exit=2`, in order `bench.sh: ` and: the `--profile` text with `Fast`; the `--mode` text with `Push`; the `--gain` text with `3`, then `-1e2`, then `-0`; `--profile given twice`; `--gain needs a value`; the `--out` text with `out-p4`, then `.scratch/../out-p4`; the `no py` text; then `nothing written`.
- V11: `py -I -B -c "import sys; [print(p, (d:=open(p,'rb').read()).count(b'\r\n'), d.count(b'\n')) for p in sys.argv[1:]]" $R/bench.sh $R/Bench.cs $R/BenchReport.cs $F $T/n2/report.md $T/n2/harness-pin.json` - green: `0 <n>` each.
- V12: `w() { py -I -B -c "import re,sys;n=lambda t:re.sub(r'\s+','',t);print(n(open(sys.argv[1],encoding='utf-8').read()).count(n(sys.argv[2])))" "$@"; }`, then `w <file> '<text>'` per text quoted under *Contracts*: in `Bench.cs` the `FastRun` signature, the usage, the `named` line; in `BenchReport.cs` the two `Header` fields, `" (confgen, named configuration)"`, the `- Harness pin:` line, the Phase 3 profile literal; in `bench.sh` each `die` text and `<COPY>` as written, `--manifest "$HPIN"`, :3, :6 - green: `1` each; `--profile "$PROFILE" --mode "$MODE"` in `bench.sh` `2`.
- V13, Command 3: `ls -d "C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/NDK"`; the recipe's *Building NativeBridge* block (:52-63) verbatim; `find NativeBridge~/build -name libvosk-bridge.so | grep -c .`; then `.claude/bindings/unity.md` :8's parenthesised command verbatim with `> $T/c3.json; echo "exit=$?"; grep -c -E '^  "(passed": 52|failed": 0|total": 52),?$' $T/c3.json` appended - green: the path, the block exit 0, `1`, `exit=0`, `3`. This branch tree is not a writer tree (`unity.md` :14). Present: the harness and confgen builds, the model, `vendor/`'s two tarballs; `NativeBridge~/build/` is absent, so the arm64 build configures fresh.
- V14: `git diff --name-only main -- 'Tests~/Fixtures/audio/tts' NativeBridge~/harness/expectations.json NativeBridge~/src/vosk_bridge.h 'NativeBridge~' Runtime; git diff --quiet -- NativeBridge~/harness/expectations.json && echo unchanged; git diff --name-only HEAD -- 'Tests~' $R/Joined.cs $R/JoinedInput.cs $R/JoinedRecogniser.cs $R/JoinedScorer.cs $R/JoinedReport.cs $R/Program.cs $R/DocCheck.cs $R/Rig.csproj $R/joined.sh $R/stage.sh $R/BenchMetrics.cs; git status --porcelain` - green: nothing; `unchanged`; nothing; only the files table and this plan's docs (`?? memory/2026-11-08.md.meta` excepted, never committed).

**Stops:** V4 `cmp=1` (a pinned-path change) or V5 `cmp=1` stops for the human with `diff` into `$T/` (risk *F12 rests on read boundaries repeating*; never a rounding fix); a harness exit other than 0 in V4 or V8 stops, never re-pinned; V9 exiting 2, or any other exit, stops with `n1.err`; a V13 toolchain path absent stops for the human naming it, Command 3 reported not run, never installed or claimed; V13 harness exit 1 stops for the human's ruling, never a re-pin.

Phase 3's unexercised stops (`<ref>: build failed`, the applied-grammar guard, a staged error log, a command pending at the horizon) serve no §9 criterion and stay out of scope.

## Open questions that block implementation

None.

## Blast radius

Every recogniser stage, `joined.sh`'s included, compiles the edited `Bench.cs`, `BenchReport.cs`; plan-3 V5's and its gate's run-usage greens go stale; a pinned run now deletes `<out>/harness-pin.json`; `.scratch/bench-p4/` (model copies and stages of `base`, `g2`, `mut`, `n1`, `n2`); V13 writes ignored `NativeBridge~/build/`, `build-windows/`, `build-confgen/`, `vendor/overlaid/continuous-default`; the as-built reconcile's *Configuration* and *Flow* (the copy step between confgen and harness).

## Runtime claims

- The pinned path is unchanged by the edits: V4's report equals V1's.
- The harness accepts the copy's byte-copied `modelConf` and parses the round-tripped JSON as the committed pin (V8 exit 0, its report differing only in the two lines).
- `--profile Responsive` writes the pinned conf (`EndpointerOverlay.cs` :78), so every final holds (V8).
- `Automatic` with `PushToTalk` writes a conf off the pin's (V9's `False`); gain −12 changes the front-end digests, so the harness likely exits 1 and continues; whether every final holds is unknown, and either branch is green (V9).
- `py` is not under `/usr/bin:/bin` (V10).
- Two runs repeat every `consumed` (V5).
- Command 3's toolchains are present and the fresh arm64 build succeeds (V13).
- Unexercised: `pin copy: py exit <rc>: no report`, reached only when `py` cannot read the committed pin or the conf confgen just wrote, or cannot write `<out>`, none made by any input this plan may write.

Hazards: V1 precedes the first edit; V6 needs V4's stage, V8 and V9 V4's report; V13 rebuilds the harness the earlier steps used, so it runs after them, in this branch tree only, never a writer tree.
