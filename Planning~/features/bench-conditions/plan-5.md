# Phase 5 — persisted plan (2026-11-10)

Scope: `bench.sh --paced --condition <id>` with the `grid` stop, `matched-check` and `run` pairing the paced passes with the conditioned fast dump, the paced section naming the condition, and the closing gate re-making every §9 criterion; it also takes on metrics-check toys 61–64 (the conditioned pairing) and a PM-run Command 3 (the row's G2 criterion).

## Files to change

`R` is `Planning~/features/coverage-in-selection/ab-rig`. One unit, serial: `bench.sh` and the `Bench.cs` tails change as a pair.

| Path | Action | Why |
|---|---|---|
| `R/Bench.cs` | modify | F13: dispatch, `matched-check` and `run` pairing; toys 61–64 |
| `R/BenchCurves.cs` | modify | F13: the conditioned dump's read pace |
| `R/BenchReport.cs` | modify | F13: the paced section names the condition |
| `R/bench.sh` | modify | F13; *Input boundary* (`grid` with `--paced`) |

`BenchPaced.cs` (its seams take dumps, manifest and outcomes as parameters), `Program.cs`, `Rig.csproj`, `stage.sh` (no file, no verb): unchanged.

## Contracts and interfaces

- **bench.sh.** :68-69 → `[ -z "$PACED" ] || [ "$CONDITION" != grid ] || die "--condition grid with --paced: pacing the grid is refused (adr-0003); name one condition"`, in the same place, before any harness pass. A game id needs nothing more: `condition` stops it without game audio before any paced pass. `pass` takes `$3` fixtures and `$4` manifest in place of its two literals. In the paced block, before the first `pass`: `PFIX="../Tests~/Fixtures/audio/tts" PPIN="$HPIN"`, then with a condition `PFIX="$OUT_W/cond" PPIN="$OUT_W/cond/pin.json"`; both calls append `"$PFIX" "$PPIN"`. The `matched-check` call appends `run`'s `${CONDITION:+…}` block verbatim. The clean paced passes do not run under a condition. Purpose appends `; with --paced and one --condition, both paced passes replay <out>/cond with its pin copy, and --bench matched-check and run pair them with the conditioned dump`.
- **Bench.Run.** The cond-block parse (:143-171) moves before `if (verb == "matched-check")`, its usage text becoming `usage: --bench run|matched-check <its arguments> --cond-dump <cond.jsonl> --cond-manifest <manifest.json> --conditions <conditions.json> --cond-exit 0|1 --condition <name> --seed <n>`. `matched-check` then takes 14 arguments (as today) or 26, its usage appending ` [<the conditioned block>]`, and with `curves` set and `Condition == "grid"` stops `matched-check: --condition grid with --paced: pacing the grid is refused (adr-0003); name one condition`, exit 2. :213-217 → the same text with `run:` for `matched-check:`, when `curves != null && paced && curves.Condition == "grid"`.
- **MatchedCheck** gains `BenchCurves.Inputs curves` last. `FastPrelude(dumpPath, 0, …)` as today; with `curves`: `curves.Pace = 0`, `BenchCurves.Read(curves)`, and the dump checked is `curves.Dump`. `CheckMatched(that dump, matched)` exits 3 naming the case, before the 20 ms pass (bench.sh's `|| exit $?`). Without `curves` the two lines are today's bytes; with it they read `matched-check: <n>/<n> conditioned clips' finals and first partials within 4096 samples of the conditioned fast dump's at pace <p>, condition <id>.` and `matched-check: read trace: <a> measured reads, <b> merged changes in <c>/<n> conditioned clips.` In both, `<n>` (today `cases`, :579, `prelude.Dump.Cases.Count`) is the checked dump's `Cases.Count`: `curves.Dump`, 63 for V9. The read-trace loop (:592) still iterates `matched.Cases`, here the conditioned paced dump. So V9's `63/63` and `/63 conditioned clips.` follow.
- **FastRun.** Before `Read`: `curves.Pace = matchedPath == null ? null : 0`. The paced pairing uses `curves == null ? prelude.Dump : curves.Dump`. `condByCase` and `entries` are declared before `if (curves != null)` (null otherwise). `Assemble` takes `curves.Manifest`, `condByCase` and `entries.ConvertAll(e => e.S)` (manifest order) under a condition, else today's three, and today's `pin` either way. That is the clean `--pin`, the one :388 (`Joined.AppliedGrammarGuard(c.Name, s.AppliedGrammar, pin);`, inside FastRun's conditioned block) already gets, because `cond/pin.json` carries no `expectedFinals`. Then `section.Condition = curves.Condition` when both are set.
- **BenchCurves.** `Inputs` gains `internal long? Pace;`, and `Read` calls `ReadDump(x.DumpPath, x.Pace)`; null keeps today's read.
- **BenchReport.** `Paced` gains `internal string Condition;`. With `Condition == null` every text is today's. With it: the matched-chunk line is `- Matched-chunk check: <n>/<n> conditioned clips' finals and first partials within 4096 samples of the conditioned fast dump's at pace 16384, condition <id>`, where n is `Factors.Count`; the self-check line stays (clean, `h.Cases`). The heading is `## Paced — condition <id>`, and after the *Desktop-only* paragraph come a blank line and `The paced passes replay condition <id>'s <n> conditioned clips; the clean cases are not paced.`, its `<n>` also `Factors.Count`. The report has no path, clock or hash order.
- **Case set.** `cond/` holds every clip of the condition, so both passes and the paced section cover all of them. For a non-game condition that is 63 clips: 16 cases, 16 clean negatives, 16 conditioned negatives and 15 twins (Phase 4 V5's per-condition terms, BenchConditions.cs :1365-1381).
- **Toys,** in `MetricsCheck` after 60. `static string Caught(Func<string> f)` returns `f()`, or `"stop " + code + ": " + Lines[0]` on a `JoinedInput.Stop`. Names are `a__s1.0-snr10.wav` and so on, and dumps are built with `new JoinedInput.Dump { Path = "x" }`. 61: `Paired` with both sides holding `a__…` and `b__…` (other reversed) gives `.Count` = `2`. 62: other holds `a__…` and `c__s1.0-snr10.wav`, giving `stop 2: x: cases differ from the fast dump's: only in fast ["b__s1.0-snr10.wav"], only in 20 ms ["c__s1.0-snr10.wav"]`. 63: `CheckMatched` on `a__…`, with fast `Timeline(24576, ("fire", 49152))` and matched `Timeline(28672, ("fire", 53248))`, each `Name` set, gives `1`. 64: matched at 53249 gives `stop 3: matched-check: case "a__s1.0-snr10.wav": final 1 "fire" at consumed fast 49152, matched 53249: more than 4096 apart`. The tallies become `64/64`; `34/34` and `52/52` hold.
- **Headers.** Bench Purpose `+ , paced under one condition`; BenchReport Purpose `+ , the paced section under one condition`.
- **Rule 1.** Strip body comments in: Bench.cs `Run`, `FastRun`, `MatchedCheck`, `MetricsCheck` and the class body (`Caught`); BenchCurves.cs `Inputs`, `Read`; BenchReport.cs `Paced`, `Build`; bench.sh top level and `pass`. Headers stay; new code has no comment.
- **EOL.** Writers write LF; the PM converts to CRLF mechanically and counts by bytes (V0, V13).

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| no clean paced passes under a condition | run them too | register :78 names the passes on `cond/`; one paced set per section |
| `matched-check` reuses `run`'s 12-argument block | a new verb or block | one parse, one bench.sh tail; no `Program.cs`/`stage.sh` change |
| clean `FastPrelude` first, then `BenchCurves.Read` | a conditioned prelude | register :56; `Read` is the one conditioned reader |
| outcomes from `Entry.S` | scoring again | the same objects, in manifest order |
| pace 0 for the conditioned dump when paced | null | as the clean dump (:287); null keeps the no-paced path's bytes |
| `BenchPaced.cs` unchanged | a condition parameter on `Assemble` | its parameters already carry the data |
| `grid` stopped in bench.sh and in both verbs | bench.sh only | a direct verb call cannot pace the grid |
| toys in `metrics-check` | `condition-check` | it holds the paced toys; condition tallies are kept |
| gate condition `s1.0-snr10`, seed 7 | another | speed 1.0 keeps the clips' lengths; a middle SNR step |

## Acceptance criteria

- [ ] F13: V9–V11. F14 and §9 :111: V12, V15. §6 `grid` with `--paced`: V11.
- [ ] §9 :104 V5; :105 V3; :106 V7; :107 V5, V6; :108 V1; :109–110 V8; :112 owed, not claimed: the maintainer listens to one conditioned case per SNR step (`$T/a/cond/`) and approves the phrase list, at G2; :113 V14, V15, with Command 4 not triggered (no `Runtime/` or `Samples~/` change).
- [ ] DocCheck V2; joined V4; EOL V13.

**Steps:** run in one Git Bash at the tree root, never concurrently. Prelude: `R=Planning~/features/coverage-in-selection/ab-rig T=.scratch/bench-conditions/p5 F=.scratch/bench-conditions/baseline-report.md G=.scratch/bench-conditions/p3-grid-seed7-report.md BREF=fcec2cf NM="Tests~/Fixtures/audio/negatives/manifest.json" M0="Tests~/Fixtures/audio/manifest.json"; mkdir -p $T; M=$(cygpath -am "$M0") P=$(cygpath -am "NativeBridge~/harness/expectations.json") FX=$(cygpath -am "Tests~/Fixtures") N=$(cygpath -am "$NM") W=$(cygpath -am $T)`. Then `CB="--cond-dump $W/pc/cond.jsonl --cond-manifest $W/pc/cond/manifest.json --conditions $W/pc/cond/conditions.json --cond-exit 1 --condition s1.0-snr10 --seed 7"` and `MC() { (cd $T/pc/stage && dotnet run --no-build -- --bench matched-check --dump "$W/pc/dump.jsonl" --manifest "$M" --pin "$P" --fixtures-root "$FX" --matched "$1" --pace 16384 $CB); }`. EOL is plan-4's `py` line over `$R/Bench.cs $R/BenchCurves.cs $R/BenchReport.cs $R/bench.sh`.

- V0: EOL `> $T/eol-before.txt`, before any edit.
- V1: plan-3 V1 with this `$T`. Expect `exit=0` and `0` FAIL each, then `64/64 metric checks verified against hand-computed toy cases.`, `34/34 …`, `52/52 …`.
- V2: recipe :229, green per :232 (`66/66`). V3: plan-4 V3 here, `exit=0`, `cmp=0`. V4: plan-4 V4, `## Flips: 0` at each layer.
- V5: plan-4 V5's `absent` test, then plan-3 V5. Expect `absent`, `exit=0` ×2, `2`, plan-4 V5's 580-clip line and `cmp=0`; then `cmp $G $T/a/report.md; echo "cmp=$?"` gives `cmp=0`.
- V6: `bash "$R/bench.sh" --ref $BREF --condition grid --seed 8 --out $T/c >/dev/null; echo "exit=$?"; n=0; for f in $T/a/cond/*__*.wav; do cmp -s "$f" $T/c/cond/${f##*/} || n=$((n+1)); done; echo "differ=$n of $(ls $T/a/cond/*__*.wav | wc -l)"`. Expect `exit=0` and the two numbers equal. The clean negatives, which no seed touches, are excluded.
- V7: plan-1 V10 with this `$T`. Expect `exit=3`, `1`, `no cond`.
- V8: plan-3 V7 and V8 over `$T/a/report.md`. Expect `1` ten times then `0`; then `21 7 True True False` and `6`.
- V9, in the background (`run_in_background`), polled until `$T/pc.exit` exists, then a stop at 30 min. The expected wall time is on the order of bench-paced-mode's 52-case `--paced` run, 518–526 s, since both passes play the 63 clips in real time: `SECONDS=0; bash "$R/bench.sh" --paced --ref $BREF --condition s1.0-snr10 --seed 7 --out $T/pc > $T/pc.out 2> $T/pc.err; echo "exit=$? secs=$SECONDS" > $T/pc.exit`. Then `cat $T/pc.exit; grep -c "^self-check: 52/52 cases' finals equal the pin.$" $T/pc.out; grep -E '^(condition: wrote|matched-check): ' $T/pc.out`. Expect `exit=0` with secs recorded; `3`; `condition: wrote 63 clips: 16 cases x 1 conditions, 16 negatives clean and x 1, 15 noise-only twins x 1, seed 7`; and the two conditioned `matched-check` lines with `63/63` and `/63 conditioned clips.`.
- V10: `Q=$T/pc/report.md; head -n 1 $Q; for p in '## Paced — condition s1.0-snr10' "The paced passes replay condition s1.0-snr10's 63 conditioned clips; the clean cases are not paced." "- Matched-chunk check: 63/63 conditioned clips' finals and first partials within 4096 samples of the conditioned fast dump's at pace 16384, condition s1.0-snr10" "- Self-check: 52/52 cases' finals equal the pin" '- Conditions: s1.0-snr10 — ' '- Seed: 7'; do grep -c -F -- "$p" $Q; done; grep -c -x '## Paced' $Q; for s in 'first partial' 'command event'; do grep -c -E "^\| [^|]+ \| $s \| " $Q; done`. Expect `# Bench report — paced replay`, `1` six times, `0`, `63`, `63`. Then the matched exit 3: `C=$(py -I -B -c "import json,sys;s,o=sys.argv[1:];L=open(s,'rb').read().decode('utf-8').split('\n');i=next(k for k,t in enumerate(L) if t and (j:=json.loads(t))['kind']=='result' and j['final'] and j.get('payload',{}).get('text'));j=json.loads(L[i]);j['payload']['text']+=' mutated';L[i]=json.dumps(j,ensure_ascii=False,separators=(',',':'));open(o,'wb').write('\n'.join(L).encode('utf-8'));print(j['case'])" $T/pc/matched.jsonl $T/mm.jsonl); MC "$W/mm.jsonl" >/dev/null 2>$T/mm.err; echo "mc=$?"; grep -c -F "matched-check: case \"$C\": finals fast " $T/mm.err`. Expect `mc=3`, `1`.
- V11: (a) plan-1 V11's `bench.sh` loop: four texts, `exit=2` each, the third now `bench.sh: --condition grid with --paced: pacing the grid is refused (adr-0003); name one condition`. This replaces plan-1 V11's "not built yet" evidence. (b) In `$T/pc/stage`, `dotnet run --no-build -- --bench matched-check --dump d --manifest m --pin p --fixtures-root r --matched x --pace 16384 ${CB/s1.0-snr10/grid}`, then `--bench run --dump d --manifest m --pin p --fixtures-root r --harness-exit 0 --report x.md --matched a --matched-exit 0 --paced b --paced-exit 0 --pace 960 ${CB/s1.0-snr10/grid}`. Expect `bench: matched-check: --condition grid …` and then `bench: run: …`, `exit=2` each. (c) `bash "$R/bench.sh" --paced --ref $BREF --condition game-sir5 --seed 7 --out $T/gs >/dev/null 2>$T/gs.err; echo "exit=$?"; grep -c -F 'needs game audio (game-audio/ under the add-on root): not supplied' $T/gs.err; ls $T/gs | grep -c -E '^(matched|paced)\.|^report\.md$'`. Expect `exit=2`, `1`, `0`.
- V12: `git diff main --stat -- 'NativeBridge~' 'NativeBridge~/src/vosk_bridge.h' 'NativeBridge~/harness/expectations.json' 'Tests~/Fixtures/audio/manifest.json' 'Tests~/Fixtures/audio/tts' 'Runtime'; git diff HEAD --stat -- 'Tests~/Fixtures/audio/negatives' $R/Joined*.cs $R/BenchMetrics.cs $R/BenchPaced.cs $R/Program.cs $R/Rig.csproj $R/stage.sh; git status --porcelain`. Expect nothing, nothing, then the files table and the plan docs (`?? memory/**.meta` excepted). This plan reads F14's "committed WAVs" as `audio/tts/`, compared with `main`, and the negatives this feature commits, compared with `HEAD` (plan-4 :71). That reading is listed for G2.
- V13: EOL after: CRLF = LF for each file.
- V14 (§9 :113; no `Tests~/` change, run for §9): `py "Planning~/host-run/host_run.py" EditMode PlayMode`, in the foreground, Bash `timeout` 600000. Green per the binding; Phase 4 recorded 239/239/0/0 and 792/792/0/0.
- V15, the PM's, after the writer returns and before the gate is dispatched (no writer in the tree): the recipe's *Building NativeBridge* block (:51-64) from the tree root, then `(cd NativeBridge~ && ls -d 'C:/Program Files/Microsoft Visual Studio/2022/Community' 'C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe' >/dev/null && 'C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe' --preset windows-x64 '-DCMAKE_GENERATOR_INSTANCE=C:/Program Files/Microsoft Visual Studio/2022/Community' && 'C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe' --build --preset windows-x64 && dotnet build harness/confgen/confgen.csproj --artifacts-path build-confgen && dotnet build-confgen/bin/confgen/debug/confgen.dll --model vendor/vosk-model-small-en-us-0.15 --out vendor/overlaid/continuous-default --profile Automatic --mode Continuous && ./build-windows/bin/vosk-bridge-harness.exe --model vendor/overlaid/continuous-default --fixtures "../Tests~/Fixtures/audio/tts" --manifest harness/expectations.json)`. Green: `libvosk-bridge.so` built with no errors, harness exit 0, 52/52.

The add-on root run is not repeated: Phase 5 edits nothing on the add-on path, and V5's `cmp` against `$G` covers the conditioned block it touches.

**Stops,** for the human, with output in `$T/`: a FAIL; any `cmp=1`; a flip; a V5, V6 or V9 exit ≠ 0; a value off in V8–V11; an exit 3 from V9's `matched-check` (adr-0001's kill criterion, never a wider tolerance); a PlayMode timeout (exit 3); a V15 exit 1 (never a re-pin). Never a re-capture of `$F` or `$G`.

## Open questions that block implementation

None.

## Blast radius

Plan-1 V11's third text changes; plan-3 and plan-4 V1's `60/60` becomes `64/64`. A paced run with a condition now runs (it exited 2 before). The paced path without a condition is unchanged by construction: no stored paced report exists, since wall times vary. The `matched-check` usage text grows. Architecture cites move (for the as-built reconcile). A paced conditioned run takes minutes of wall time. Command 3 rebuilds the gitignored `NativeBridge~/build*/` and `vendor/overlaid/`. Touches `.scratch/bench-conditions/p5`.

## Runtime claims

- The harness's paced passes accept `cond/` and `cond/pin.json` and exit 0 or 1 (V9).
- At pace 16384, conditioned audio matches the conditioned fast pass within 4096 (V9).
- The conditioned fast dump carries no paced keys, so pace 0 reads it (V9).
- Seed 7 at `s1.0-snr10` writes 63 clips; each final's text re-serialized by `json.dumps` stays readable (V9, V10).
- No default add-on root exists (V5, V11c).
- Phase 4's host totals hold (V14).

Hazards: V0 precedes every edit; the four files are one change; V15 runs after the writer returns and before the gate, never concurrently with V3–V14; nothing else runs while V9 runs in the background; V10–V11 read `$T/pc` after V9; `$F` and `$G` are never written; `bench.sh`, `joined.sh`, the harness and `host_run.py` are never run concurrently.
