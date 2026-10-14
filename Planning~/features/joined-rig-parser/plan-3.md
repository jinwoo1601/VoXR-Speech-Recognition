# Phase 3 — persisted plan (2026-10-14)

**Phase 3 - scorer and report**, unsplit: F3-F8 (F8's report row), §10 item 6. `ab-rig/` = `Planning~/features/coverage-in-selection/ab-rig/`; J `Joined.cs`, JI `JoinedInput.cs`, P `Program.cs`, S `stage.sh`, VCP `Runtime/Commands/VoxrCommandParser.cs`. `plan-2.md` *Contracts* hold. † = approved departure from the row.

**Ruling - `Parse`'s order** (the maintainer, at plan approval, 2026-10-14): *The fire* stands; that order is utterance order (one winner per extraction round, no sort: VCP `ParseInternal` :2539-3000), as recogniser :1515-1516 and `VoxrBatchTestRunner` :161-182 fire. The as-built pass corrects *The fire*'s *Why* and *Rejected*, and *Risks*' "best first".

## Files to change

| Path | Change | Why |
|---|---|---|
| `ab-rig/JoinedScorer.cs` | create | *Fire gate*; *The fire*; *Slot matching*; *Two or more finals* |
| `ab-rig/JoinedReport.cs` | create | *Report* (F4, F6-F8) |
| `ab-rig/Joined.cs` | modify | *Scored record*; *Marks* (F7) |
| `ab-rig/joined.sh` | modify | *Run script* |
| `ab-rig/JoinedInput.cs` † | modify | scorer inputs (F3, F5) |
| `ab-rig/Program.cs` † | modify | definitions for the gate |
| `ab-rig/stage.sh` † | modify | *Staging* |

One change set: all seven land before any stage or build; they compile only together.

## Contracts and interfaces

**C#:** top-level `JoinedScorer`, `JoinedReport` (static), others nested. **P:** the `--joined` dispatch (P:167-180) passes `Commands()` (private, P:52) as `Joined.Run`'s fourth argument. **S:** S:41-43's `cp` list gains `"$HERE/JoinedScorer.cs" "$HERE/JoinedReport.cs"`. Nothing else in either.

**JI:** finals and kill check unchanged; each new stop is `Stop(2)`.
- `ManifestCase` (JI:58) keeps `expectedIntent` (string, `""` = negative) and `expectedSlots` (ordered `{name, value}` strings), required, strict; else stop naming case and key.
- `result` branch (JI:184-228): each non-empty final's `payload.result` words, parallel to `Finals` (nested `Word`): required, strict (objects: string `word`, numeric `conf`, `start`, `end`), else stop `<file>:<line>`. Only empty-text finals may lack `result`.

**`JoinedScorer.cs`.** `MinScore = 0.6f`, `MinConfidence = 0.4f` (*Tuning record*). Per scored case:
1. Each final alone, in order: words to `VoxrWord[]` (as `VoxrJsonParser.ParseWordsFromJson` :52); `parser.Parse(text, words)` (VCP :2484).
2. Pool, final then array order, each result passing *Fire gate* (VCP :4539), `def` the first definition of that `Intent` (ordinal), none = complete.
3. Primary = first pooled; `extraFires` = the rest, a count; `finalCount` = the finals.
4. Class: expected `""` -> `false-fire` with a primary, else `correct-no-fire`. Else no primary -> `missed`; other intent (ordinal) -> `wrong-intent`; else `correct-fire` or `wrong-slots` per *Slot matching*.
5. `slotCredit` (*Slot matching*): `"n/a"` whenever none is expected, whatever the fire; else matched / expected, 0 with no primary. No weight, no total.

**J: `Run(args, parser, grammarJson, commands)`**. After `--joined`: `score` (Phase 2's path) or `report`; else the usage stop, exit 2, naming both.
- `score` takes `--evaluate` (once); disposition (J:221-236) as Phase 2, but under it a sealed case goes as unsealed.
- Writer (J:187-258): header keeps Phase 2's keys, then `"gate":{"minScore":0.6,"minConfidence":0.4}` literally, `"evaluate":bool`. A `scored` case appends *Scored record*'s keys in order; `intent`, `score` the primary's or `null`, `slots` its `[{"name","value"}]` or `[]`.
- Stdout (J:260-264): `<name> <n>` per disposition, then the report's class counts; exit 0.

**`JoinedReport.cs`.** `--joined report --a <scored-a> --b <scored-b> --out <report.md>`, each once, required, else the usage stop. Strict: each line exactly `score`'s keys (else exit 2 `<file>:<line>`); `side` `a`, `b`; `vary`, `manifest`, `provenance`, `gate`, case names in order equal (else exit 2 naming the difference). Writes `report.md` whole (UTF-8 no BOM, LF), prints it. Outcome = class (`correct-no-fire` shown `correct (no fire)`), or disposition if unscored.
1. Header per *Report*; kill check `passed` if `pinned`, else `not checked`; `HOLDOUT EVALUATION` if either `evaluate`.
2. Counts, A and B columns: five classes, correct (both `correct-*`), `wrong-slots`, `wrong-intent`, `missed`, `false-fire`; `of which correct (no fire): <n>`; extra fires (sum of `extraFires`); not scored at this layer; sealed-skipped. No total.
3. Cases (§4 item 4), file order: case, A outcome and credit, B outcome and credit, `*` on a flip.
4. Not scored at this layer: their names.
5. `Flips: <n>`; each flip per *Report* (raw JSON): per side transcript (finals joined ` | `, `(none)`, `(sealed)`), outcome, intent, slots, credit, extra fires.
6. `decoder` only: every case's A and B transcripts (F4).

**`joined.sh`.** `--evaluate`: no value, once (else `given twice`), to both `score`s; the :55 refusal and :17's sentence go; :106 also removes `<out>/report.md`. Then `report` (`dotnet run --no-build`, in `$STAGE_A`) on `$OUT_W`'s scored files, `--out "$OUT_W/report.md"`, non-zero exit passed through; echo the paths; exit 0. Header: usage `[--evaluate]`, step 4.

**Human stops:** a false runtime claim (PM-ruled); the kill check failing on unaltered `p2.jsonl`; harness exit 1 on a dump re-make (never a re-pin).

**Commit set:** step 9's set but `__pycache__/`. No CHANGELOG line.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| Definitions as 4th argument | Copy in scorer | `Commands()` private; one source |
| Expected keys required | Absent = negative | Never a silent score |
| Literal match | Consume matches | Register; no case repeats a slot name |
| Canary in `HasUnfilledRequiredSlot` | Pattern edit | Every fire leaves the pool |

## Acceptance criteria

Steps 1-5: a `compile-check` dispatch, clean tree. Step 6: one `code-writer` dispatch, after 1-5 are green. Step 7: the PM. Steps 8-9: the next `compile-check` dispatch. Git Bash, project root; JSON checks and copies by `python3 -I`, LF. `sj/` = `.scratch/joined/`; dump `sj/p2.jsonl` (if absent, re-made per `plan-2.md` 3). `J.sh` = `bash` on `ab-rig/joined.sh`. L, R, U = `launch_one_missiles_target_hotel_one.wav`, `resume_fire.wav`, `filler_launch_three_torpedoes_target_alpha_three.wav`.

1. [ ] DocCheck per the recipe, verbatim: exit 0, no `FAIL`, `<n>/<n>` equal. (F11; claims b, f)
2. [ ] `J.sh --vary parser --dump sj/p2.jsonl --ref-a main --ref-b main --out sj/p3-f3`: exit 0; `Flips: 0`; split case not scored; 15 `scored` per side; class counts recorded; case lines byte-equal, headers equal but `side`. (F3, F8; claims a, d, h)
3. [ ] B copy `sj/p3-dump-b.jsonl`: L's final `text` `launch one missiles`; `cease_fire.wav`'s final `conf`s 0.1; R's final `text` `resume fire enable all`, `result` + `enable`, `all` (conf 1.0, after `fire`); U's final (seq 9) split into `final:true` `[unk]` (first `result` entry) and the rest, later `seq` +1, `case-end` `results` 13. `J.sh --vary decoder --dump-a sj/p2.jsonl --dump-b <copy> --ref main --out sj/p3-f4`: exit 0; all transcripts; four flips; on B `cease_fire.wav` `missed`, R A's class with `extraFires` 1, U A's class and credit with `finalCount` 2; counts move only by `cease_fire.wav` and L; extra fires B = A + 1. (F4, item 6; claims a, c, g)
4. [ ] Manifest copy `sj/p3-sealed.json`, L `"sealed": true`; step 2 plus `--manifest <copy>`, `--out sj/p3-sealed`: exit 0, L `sealed`, no `finals`, in both scored files, sealed-skipped 1 per side, no stamp; plus `--evaluate`, `--out sj/p3-evaluate`: L `scored`, `HOLDOUT EVALUATION`. (F7)
5. [ ] Step 2's line, on `--dump sj/p2.jsonl` unless a copy is named: (i) `--dump <step 3's copy> --out sj/p3-f2`: exit 3 naming L, R, U, expected and actual, no scored file. Then `--out sj/p3-neg-<n>` (n = 2-6), each exit 2, cause named, no scored file or report: (ii) manifest copy adding `"seald": true`; (iii) manifest copy, a case's `expectedSlots` removed; (iv) `--dump` copy, a `conf` a string; (v) `--dump` copy, a non-empty final's `result` removed (file, line); (vi) `--evaluate --evaluate`; (vii) `report` as `joined.sh` runs it, `--a` and `--b` both `sj/p3-f3/scored-a.jsonl`, `--out sj/p3-neg-7/report.md`. (F2, NFRs)
6. [ ] Canary, a temporary edit the PM reverts: VCP `HasUnfilledRequiredSlot` returns `true`. Step 2 with `--ref-b WORKTREE --out sj/p3-canary`: exit 0; B `+dirty`; each case with a primary on A has none on B and is a listed flip; A's counts = step 2's; each count moves by exactly the cases listed. `report.md` kept for the maintainer. (F5, F6; claims e, i)
7. [ ] PM: `git checkout -- Runtime/Commands/VoxrCommandParser.cs`.
8. [ ] `git status --short -- Runtime/` empty; Commands 1-2 (binding `## verification`) green as it says, `Tests~` reverted, `memory/**/*.meta` kept. (NFRs)
9. [ ] `git status --short`: only the seven files, `plan-3.md`, `architecture.md` (link line), `memory/**/*.meta` if any, and the pre-existing untracked `.claude/references/vc-git/__pycache__/`, not this phase's.

## Open questions that block implementation

None.

## Blast radius

DocCheck compiles the joined files. Manifests lacking expected keys and finals lacking `result` stop the rig. `joined-rig-recogniser` and Phase 4 read the scored record. As-built pass: the ruling's wording; `Joined.Run`'s signature; stale P:298, S:41-42, manifest :165-181.

## Runtime claims

- a (2, 3, 6): `Parse` returns utterance order (ruled).
- b (1, 2): the stage compiles a call to `internal` `HasUnfilledRequiredSlot` (`Rig.csproj` `*.cs`, flat).
- c (3): `result[]` words give `Confidence >= 0` on scored finals (`p2.jsonl`'s 16 non-empty finals all carry it).
- d (2): one ref on one dump, run twice, writes byte-equal case lines.
- e (6): the seeded regression moves a case's class; no count nets another.
- f (1, 2, 6): `dotnet build` exits non-zero on a compile error in the new files.
- g (3, 5): `python3 -I` edits keep LF and make exactly the claimed changes.
- h (2-6): `report`'s drive-letter paths reach `dotnet` intact in stage A; `dotnet run --no-build` returns its exit code.
- i (6): the canary compiles in the staged `WORKTREE` rig; the report shows `+dirty`.
