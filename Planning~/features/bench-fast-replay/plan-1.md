# Phase 1 — persisted plan (2026-11-08)

Scope: `BenchMetrics.cs`, a `Bench.cs` skeleton running the toys below, the `Program.cs` hook (old `--bench` kept), `stage.sh`'s recogniser-only copies and the probe, taking on the paired `Rig.csproj` comment and a dump compare covering partials; nothing of Phases 2-4.

## Files to change

`R` is `Planning~/features/coverage-in-selection/ab-rig`.

| Path | Action | Why |
|---|---|---|
| `R/BenchMetrics.cs` | create | F6-F8; register *Command scoring*, *WER*, *Calibration* |
| `R/Bench.cs` | create | *Code home*; the toys |
| `R/Program.cs` | modify | *Code home* hook; its comment |
| `R/stage.sh` | modify | *Code home*: `recogniser` only; comment :8-9 |
| `R/Rig.csproj` | modify | paired comment :9-30 |

## Contracts and interfaces

- **Program.cs.** `BenchVerbs = { "metrics-check", "truth-check", "run" }`; `internal static Func<string[], string, VoxrSlotDefinition[], VoxrCommandDefinition[], int> BenchHook` (args, `--grammar`'s JSON, `Slots()`, `Commands()`). Just before the old block, if the argument after the first `--bench` is in `BenchVerbs`: a null hook prints `bench: <verb> needs a recogniser stage: this stage has no Bench; stage with stage.sh <ref> <out> recogniser` to stderr, 2; else the hook's result. The old block :211-247 is byte-unchanged; its comment notes the verbs dispatched above.
- **Bench.cs.** A `[ModuleInitializer]` sets `Program.BenchHook = Run`, as `JoinedRecogniser`'s does. `metrics-check` with another argument stops `usage: --bench metrics-check`; `truth-check` and `run` stop `<verb> is not built yet (bench-fast-replay Phase <2|3>)`. A `JoinedInput.Stop` prints its lines prefixed `bench: `, exit its code; another exception `bench: <Type>: <message>`, 2. Output: per toy a `DocCheck.ExpectText`-form row (DocCheck.cs :83: `ok  `/`FAIL`, where its #, what, got, want) on Bench's own counters, `DocCheck.cs` unedited; a blank line, `<passed>/<total> metric checks verified against hand-computed toy cases.`; exit 0, 1 any FAIL.
- **BenchMetrics.cs**, the register's definitions plus: values print `F4` invariant, else `undefined (<reason>)`; `EceBins = 10`, provisional. `Reference(phrase, segments)`; `Hypothesis(List<Word[]>)`. `Align(ref, hyp)` -> S, D, I, N, `Correct[]` per hyp word (a diagonal match only), Levenshtein table T, diagonal when T[i-1][j-1] + (differ) = T[i][j], else deletion, else insertion. `Score(list of (string[] ref, Word[] hyp))` -> S, D, I, N, stray, WER (N 0 -> `undefined (no reference words)`), ECE, NCE; an empty ref is the silence case, its hyp length counted as stray only. `Ece` and `Nce` over (conf, correct): bin floor(10c), 10 -> 9; conf outside [0, 1] stops 2; NCE's undefined reasons in row order 17, 13, 14, 10, 15; a word's cross-entropy term: -log2 c if right, -log2(1-c) if wrong. `Command(list of (class, extraFires))`: counts in `JoinedScorer.Classes` order, positives P the classes correct-fire, wrong-slots, wrong-intent, missed; P 0 -> `no cases with an expected intent`; an unknown class stops 2.

**Toys.** `w@c` a hyp word at conf c; R/W a right/wrong pair. Rows 1-5 print `S=<s> D=<d> I=<i> N=<n> correct=<T/F per hyp word> wer=<F4>`; rows 6, 23 `S= D= I= N= stray= wer=`; row 18 `<class>=<n>` in `Classes` order; row 22 the three rates joined by ` | `.

| # | Input | Want | Working |
|---|---|---|---|
| 1 | `fire missiles`; `fire@1 torpedoes@0.5` | S=1 D=0 I=0 N=2 TF 0.5000 | T[2][2] = T[1][1]+1 = 1, diagonal; 1/2 |
| 2 | segments `cease`, `fire`; `fire@1` | S=0 D=1 I=0 N=2 T 0.5000 | ref `cease fire`; (2,1) match, (1,0) deletion; 1/2 |
| 3 | `disengage`; `disengage@1 now@0.5` | S=0 D=0 I=1 N=1 TF 1.0000 | at (1,2) diagonal 2, deletion 3, insertion T[1][1]+1 = 1; 1/1 |
| 4 | `alpha one`; `one@0.5 alpha@0.5` | S=2 D=0 I=0 N=2 FF 1.0000 | T[2][2] = 2 by all three moves; diagonal first: 2 substitutions (deletion first: D=1 I=1 FT); 2/2 |
| 5 | `resume fire`; `[unk]@0.5 resume@1 fire@1` | S=0 D=0 I=1 N=2 FTT 0.5000 | (2,3), (1,2) match, (0,1) inserts `[unk]`; 1/2 |
| 6 | Score 1-5 + silence `[unk]@0.5 fire@0.5` | S=3 D=1 I=2 N=9 stray=2 0.6667 | errors 1+1+1+2+1 = 6 / words 2+2+1+2+2 = 9; per-case mean 3.5/5 = 0.7000 differs |
| 7 | row 6 ECE | 0.2500 | 5 R@1 (bin 9, gap 0), 5 W@0.5 (bin 5, gap 0.5); 5/10 * 0.5 |
| 8 | row 6 NCE | 0.5000 | p = 1/2, H_base = 1; H_conf = (5*0 + 5*1)/10; (1-0.5)/1 |
| 9 | ECE R@1 W@1 R@0.25 R@0.25 W@0 | 0.5000 | bin 9: 2/5 * (1-1/2) = 0.2; bin 2: 2/5 * (1-0.25) = 0.3; bin 0: 0 |
| 10 | NCE row 9's | undefined (a wrong word at confidence 1) | p = 3/5; -log2(1-1) infinite |
| 11 | NCE R@1 R@0.5 W@0.5 W@0 | 0.5000 | H_base = 1; H_conf = (0+1+1+0)/4 |
| 12 | ECE row 11's | 0.0000 | bins 9, 5, 0: acc = conf |
| 13 | NCE R@0.5 R@0.75 | undefined (all words right) | p = 1 |
| 14 | NCE W@0.5 W@0.25 | undefined (all words wrong) | p = 0 |
| 15 | NCE R@0 W@0.5 | undefined (a right word at confidence 0) | -log2 0 infinite |
| 16 | ECE, none | undefined (no words) | N = 0 |
| 17 | NCE, none | undefined (no words) | N = 0 |
| 18 | (class, extra fires): correct-fire 0, correct-fire 1, wrong-slots 0, wrong-intent 3, missed 0, correct-no-fire 0, false-fire 1 | 2, 1, 1, 1, 1, 1 | counted |
| 19 | row 18 intent | 0.6000 | P = 5; (2+1)/5 |
| 20 | row 18 semantic | 0.4000 | 2/5 |
| 21 | row 18 phantom | 0.8000 | (1+3)/5; false-fire's 1 excluded (else 5/5) |
| 22 | correct-no-fire 0, false-fire 2 | undefined (no cases with an expected intent), x3 | P = 0 |
| 23 | Score, silence `fire@0.5` only | S=0 D=0 I=0 N=0 stray=1 undefined (no reference words) | N = 0 |

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| New mode only on a bench verb after `--bench` | any argument; a rename | old name, input, output kept; "stops `--bench` by name" read per verb |
| Verbs and hook arguments fixed now | per phase | `Program.cs` is in no later row |
| NCE term by correctness | y log c + (1-y) log(1-c) | 0 * infinity is NaN |
| Whole dumps minus results' `pushed`/`prevPushed` | `timing` alone | `timing` pairs finals only; F4 reads partials; `DumpResult` never reads those keys |

## Acceptance criteria

- [ ] F7 rows 1-6, 23, F8 rows 7-17, F6 rows 18-22 `ok` (V4).
- [ ] *Code home*: a parser stage without `Bench*.cs` builds and stops each bench verb by name (V1, V2).
- [ ] Old `--bench` unchanged in both stages (V3, V5).
- [ ] F12: V8-V10 green, or the risk raised to the human.
- [ ] Preamble: DocCheck (V1); joined `main`-versus-`main` at both layers, 0 flips (V11).
- [ ] F13: V12.

**Steps:** the files table in order (`stage.sh` :112's `cp` gains `"$HERE/Bench.cs" "$HERE/BenchMetrics.cs"`; `Rig.csproj`'s `flag bench` row the bench verbs, its last sentence Bench's initializer); then V1-V12 in one Git Bash at the tree root after `R=Planning~/features/coverage-in-selection/ab-rig P=.scratch/bench-probe`.

- V1: `env -u MSYS_NO_PATHCONV bash "$R/stage.sh" WORKTREE .scratch/doccheck && (cd .scratch/doccheck && dotnet build -v q --nologo && dotnet run --no-build -- --doccheck)` - green per recipe :232.
- V2: `! ls .scratch/doccheck/Bench*.cs 2>/dev/null && for v in metrics-check truth-check run; do (cd .scratch/doccheck && dotnet run --no-build -- --bench "$v"); echo "$v exit=$?"; done` - green: each verb's stop line, `exit=2`.
- V3: `(cd .scratch/doccheck && printf 'cease fire\nfire missiles at hotel one\n' | dotnet run --no-build -- --bench); echo "exit=$?"` - green: one line, `parses=4000` (2000 reps, :220, x 2 lines), tab, `us_per_utterance=` F3, tab, `bytes_per_utterance=` F1; `exit=0`.
- V4: `env -u MSYS_NO_PATHCONV bash "$R/stage.sh" WORKTREE .scratch/bench-stage recogniser && (cd .scratch/bench-stage && dotnet build -v q --nologo && dotnet run --no-build -- --bench metrics-check)` - green: exit 0, no `FAIL`, `23/23 metric checks verified against hand-computed toy cases.`
- V5: V2's loop over `truth-check run`, and V3, in `.scratch/bench-stage` - green: the not-built stops, `exit=2`; as V3.
- V6: `NativeBridge~/harness/README.md` `## Provisioning a branch tree` blocks 1, 2 verbatim - green: `provisioned`, or the items already present; one missing at the hub is the human's download.
- V7: Command 3's desktop harness line (`.claude/bindings/unity.md`) verbatim - green: exit 0, 52/52; exit 1 never re-pinned.
- V8: `mkdir -p $P && for n in 1 2; do (cd NativeBridge~ && ./build-windows/bin/vosk-bridge-harness.exe --model vendor/overlaid/continuous-default --fixtures "../Tests~/Fixtures/audio/tts" --manifest harness/expectations.json --dump "../$P/d$n.jsonl" > "../$P/h$n.json"); echo "dump $n exit=$?"; done` - green: both `exit=0`.
- V9: `(cd .scratch/bench-stage && dotnet run --no-build -- --joined timing --dump "$(cygpath -am ../../$P/d1.jsonl)" --dump-b "$(cygpath -am ../../$P/d2.jsonl)"); echo "exit=$?"` - green: `timing: largest consumed difference 0 input samples`, `timing: 52/52 cases passed`, `exit=0`.
- V10: `s='s/"pushed":[0-9]+,"prevPushed":[0-9]+,//'; cmp <(sed -E "$s" $P/d1.jsonl) <(sed -E "$s" $P/d2.jsonl); echo "exit=$?"` - green: no output, `exit=0`; else `diff` the same into `$P/diff.txt`.
- V11: `bash "$R/joined.sh" --vary parser --dump $P/d1.jsonl --ref-a main --ref-b main --out .scratch/joined-p1 && grep -x '## Flips: 0' .scratch/joined-p1/report.md`, again with `--layer recogniser` - green: exit 0 and the line, each.
- V12: `git diff --name-only main -- 'NativeBridge~' 'Tests~' Runtime; git status --porcelain` - green: nothing, then only the files table and this plan's two docs.

**Probe stop:** a harness exit other than 0 (V7, V8) or any difference in V9 or V10 stops for the human with the output; never a rounding fix, a re-run, or a changed compare.

## Open questions that block implementation

None.

## Blast radius

`--bench` in `leading-miss-bar` architecture :509-513, `coverage-in-selection` architecture :415; recogniser stages compile `Bench*.cs`; ignored `NativeBridge~/vendor/`, `build-*/`; `.scratch/`.

## Runtime claims

- The module initializer sets `BenchHook` before `Main` (V4, V5).
- `dotnet run` passes piped stdin to the old mode (V3).
- Two harness runs repeat every result but `pushed`/`prevPushed` (V9, V10; F12's risk).

Hazards: V6-V8 precede V9-V11; Command 3 runs in this branch tree only, never a writer tree.
