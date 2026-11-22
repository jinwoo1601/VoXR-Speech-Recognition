# Phase 2 — persisted plan (2026-11-22)

Scope: the row, plus `Bench.cs`, a `.scratch/` helper; taken on: `matched-check`'s voice-block refusal (F12), `--voices --condition` refused until Phase 4; `run`'s `--paced` refusal waits for Phase 3's block; no report; `CHANGELOG.md` waits for Phase 3.

## Files to change

| Path | Action | Why |
|---|---|---|
| `R/BenchVoices.cs` | create | F2, F4, F8, §6 |
| `R/Bench.cs` | modify | dispatch; F12 |
| `R/Program.cs`, `Rig.csproj`, `stage.sh` | modify | verbs, comment, copy |
| `R/bench.sh` | modify | *Naming voices*, *Delivery* |
| `.scratch/bench-voices/p2/p2.py` | create | check copies, read-back |

## Contracts and interfaces

- `BenchVerbs` and `Main`'s comment list (rule 1 overridden) end `voice`, `voice-check`; `Rig.csproj`'s `(bench-conditions);` drops `;`, then `or voice, voice-check (bench-voices);`; `stage.sh`'s recogniser `cp` adds `BenchVoices.cs`; its header holds.
- **Bench.cs.** Purpose, Depends + the voice verbs' dispatch, `BenchVoices`. `Run`, after `condition`: `voice-check` (length 2) → `BenchVoices.Check()`; `voice` (20: `--dump --manifest --pin --fixtures-root --harness-pin --out --voices --negatives --audio-root` at 2..18) → `BenchVoices.Voice`; usage as `condition`'s. Before the conditioned peel, `--voice-dump` stops 2: `matched-check: --voices with --paced: paced voices are refused (adr-0003)`, else `run: a voice block is not built yet (bench-voices Phase 3)`. `Expect`, `Caught`, `checks`, `failures` → `internal`.
- **BenchVoices.** Header: Purpose its verbs, survey, delivery; Layer `Planning.AbRig`; Owns `BenchVoices (internal static class)`; Depends `Bench, JoinedInput`.
  - `Ids`, `Pick`: voices.py's `IDS`, `pick`, texts. `OutCheck`: `voices: --out must lie under .scratch/ (the voiced audio is written there): <out>`. `Fresh`: `voices: <out>/voice exists: empty it first`.
  - *Voice*: `Pick`, `OutCheck`, `Bench.FastPrelude(dump, null, manifest, pin, root)` (exit 3, nothing written), `Fresh`, the set (voices.py `voiced_set`, raw elements kept), `Survey`, `Deliver`; prints per id `voices: <id>: <n> clips, render <id12>` (voices.py `render`) or `voices: <id>: missing: <reason>`, then the status; exit 0.
  - *Survey*, before any write. No `voices/`, or no non-dot directory there with `voice.json`: status `voices: baseline only — Kokoro not provisioned`. Else per id: missing `no directory`, `no voice.json`; stops (2) `voices: <id>: voice.json` + `: malformed: not a JSON object` / `: malformed: key "<k>" missing or not a <type>` / ` names "<x>"` / at the first unequal index `'s clip <i+1> is "<file|(none)>", the voiced set's "<file|(none)>"`; `voices: <id>: <stem>.wav: "<key>" differs from its manifest` (*Cache*'s text keys, as JSON values); missing `<k>/<n> listed clips absent, first <stem>.wav`; per clip `voices: <id>: <stem>.wav: ` + `sha256 differs from voice.json` / `not 48 kHz mono 16-bit PCM: <ReadPcm's line>` / `peak <v> outside [0.04, 0.4]` (max\|s\|/32767, `F6`). Status `voices: <p>/<n> named voices present, <c> clips written to voice/`.
  - *Deliver*: `<out>/voice/`; with clips, `<stem>__<id>.wav` copies (id, then set order), `pin.json` as `cond/`'s, `manifest.json` (`voice` `"voices.json"`, `provenance` `"bench-voices"`; per case `file` `voice/<name>`, the record's `speech`, the source's other members), read back by `ReadManifest`, then `Bench.TruthCheck(back, out)`, whose stop deletes `voice/` and rethrows. Last `voices.json`: `status`, `named`, `present`, `missing` `[{id, reason}]`, `voices` (the record's eight top members, `renderId`, `clips` count), `clips` `[{file, source, voice, condition: "clean"}]`.
  - *Check*: `Bench.Expect`, `Caught`; last `<p>/<n> voice checks verified against known-value toys.`, exit 1 on a FAIL. 26 rows, exact: `Pick` 4, `OutCheck` 3 (`/out-x`, `.scratch/a/../b`, `.scratch/v`), `Fresh` 1, `Survey` 14 (two baselines, two malformed, two peaks: 0, 32767), `Deliver` 4 (one: end + 2000, the truth stop, no `voice/`); a temp root, a `clean` and a negative case, clips 0.3 s zeros, 4800 samples of 6553, 1 s zeros.
- **bench.sh.** :3 Purpose + the `--voices` pass, `voice` reading `--audio-root`; :6 + `BenchVoices`. `VOICES` by `take`. After :68, `die`: `--voices with --paced: paced voices are refused (adr-0003)`; `--voices with --condition is not built yet (bench-voices Phase 4)`; :67 `--audio-root needs --voices or --condition`; :78's test, `--out must lie under .scratch/ for a voiced run (the voiced audio is written there): $OUT`; :94 either. Cleanup adds `voice/`, `voice.jsonl`, `.json`, `.err`. After the conditioned block, `condition`'s call as `--bench voice --voices "$VOICES"`; with `voice/pin.json`, the `cond/` pass on `voice/`; `echo "$OUT_W/voice/voices.json"; exit 0`.
- **p2.py.** `mutate <src> <dst> <k>`: `<dst>` emptied, `af_heart` copied; first clip: `sha` a byte flipped; `format` rate 24000; `peak` zeroed; `truth` end + 2000; `malformed` `[]`; `id` `af_bella`; `set` dropped; `text` `peak` 0.3; `format`, `peak` re-record sha. `read <out> <root> <pin>`: V7's last four lines (status; present, clips, WAVs, `voice.jsonl` cases; render ids, records; manifest, `pin.json` keys, no `expectedFinals`).

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| truth on the copies | before the copy | *Provisioning*: root `<out>` |
| `WriteJson`, `.scratch/` check modelled on | `internal` | `BenchConditions.cs` is Phase 4's |
| F8 keyed on the cache | named ids | "no voice with a record" |

## Acceptance criteria

- [ ] Toys (V4); DocCheck (V2); parser stop (V3); EOL (V14).
- [ ] F3: `cmp=0` (V5); 0 flips (V6); V13.
- [ ] F2 (V7). F4 (V8). F8 (V9, V10). F12, §6 (V11, V12).

**Steps.** Git Bash, tree root, each call: `R="Planning~/features/coverage-in-selection/ab-rig" B=.scratch/bench-voices; T=$B/p2 S=$B/stand-in; mkdir -p $T; A=$(cygpath -am .); W=$A/$T F=$A/Tests~/Fixtures P=$A/NativeBridge~/harness/expectations.json; vb() { (cd $T/v/stage && dotnet run --no-build -- --bench voice --dump $W/v/dump.jsonl --manifest $F/audio/manifest.json --pin $P --fixtures-root $F --harness-pin $P --out $W/$1 --voices $3 --negatives $F/audio/negatives/manifest.json --audio-root $2 >$W/$1.out 2>$W/$1.err); echo "$1 exit=$?"; }`. BC is `Planning~/features/bench-conditions/`, E its `plan-4.md` EOL line. Timeout 600000.

- V0: E over the five rig files `> $T/eol.txt`. V1: the table, one change.
- V2: recipe :229, green per :232. V3: BC `plan-1.md` V6, `voice voice-check`, `BenchVoices.cs`: its line, `exit=2`.
- V4: `env -u MSYS_NO_PATHCONV bash "$R/stage.sh" WORKTREE $T/stage recogniser && (cd $T/stage && dotnet build -v q --nologo && dotnet run --no-build -- --bench voice-check) > $T/vc.txt; echo "exit=$?"; grep -c '^FAIL' $T/vc.txt; tail -n 1 $T/vc.txt`: `exit=0`, `0`, `26/26 voice checks …`.
- V5, a call each: plan-1 V1's `bench.sh` runs into `$T/f`, `$T/g`, then `cmp` with `$B/baseline-fast.md`, `-grid.md`: `cmp=0` ×2.
- V6: BC `plan-1.md` V7 on `$T/f/dump.jsonl`: `## Flips: 0` ×2.
- V7, own call: `bash "$R/bench.sh" --voices all --audio-root $S --out $T/v > $T/v.out; echo "exit=$?"; grep -cE '^voices: [a-z_]+: 31 clips, render [0-9a-f]{12}' $T/v.out; grep -F "372/372 clips' speech truth" $T/v.out; py -I -B $T/p2.py read $T/v $S $P`: `exit=0`, `12`, the line, `voices: 12/12 named voices present, 372 clips written to voice/`, `12 372 372 372`, `True`, `['cases', 'provenance', 'voice'] 372 voice/launch_one_missiles_target_hotel_one__af_heart.wav True`.
- V8: BC `plan-1.md` V10, no `--ref`, `--voices all --audio-root $S` for the condition, `voice` for `cond`: `exit=3`, `1`, `no voice`.
- V9: `test ! -e "${XDG_CACHE_HOME:-$HOME/.cache}/voxr-bench" && echo absent; bash "$R/bench.sh" --voices all --out $T/b > $T/b.out; echo "exit=$?"; grep -cF 'voices: baseline only — Kokoro not provisioned' $T/b.out; ls $T/b/voice $T/b/voice.jsonl`: `absent`, `exit=0`, `1`, `voices.json`, no `voice.jsonl`.
- V10: `mkdir -p $T/part/voices && cp -r $S/voices/{af_heart,am_puck} $T/part/voices/ && find $T/part/voices/am_puck -name '*.wav' -delete && vb o-p $W/part af_heart,am_puck; grep '^voices: [a-z0-9]' $T/o-p.out`: `exit=0`, `voices: af_heart: 31 clips, render <hex>`, `voices: am_puck: missing: 31/31 listed clips absent, first launch_one_missiles_target_hotel_one.wav`, `voices: 1/2 named voices present, 31 clips written to voice/`.
- V11: `for a in "--voices all --paced" "--voices all --out out-x" "--voices all --condition grid --seed 7" "--audio-root x"; do bash "$R/bench.sh" $a 2>&1 >/dev/null | tail -n 1; echo "exit=${PIPESTATUS[0]}"; done`: `bench.sh: ` + each text, `exit=2`.
- V12: `for k in sha format peak truth malformed id set text; do py -I -B $T/p2.py mutate $S $T/m-$k $k && vb o-$k $W/m-$k af_heart; tail -n 1 $T/o-$k.err; done; vb o-x $A/$S xx; vb o-y $A/$S af_heart,af_heart; mkdir -p $T/o-z/voice; vb o-z $A/$S af_heart; tail -qn 1 $T/o-[xyz].err; for v in run matched-check; do (cd $T/v/stage && dotnet run --no-build -- --bench $v --dump d --voice-dump v); echo "$v exit=$?"; done`: each `exit=2`, `bench: ` + its text, `L` = `launch_one_missiles_target_hotel_one`: the kind's on `L.wav` (truth: `truth check failed on 1/31 clips: L__af_heart.wav`; set's `"audio/tts/L.wav"`); unknown (with `IDS`), twice, `exists`; both refusals.
- V13: `git diff main --stat -- 'NativeBridge~' Runtime 'Tests~/Fixtures/audio'; git status --porcelain`: nothing; the six rig paths, plan docs.
- V14: E after: CRLF 0 where `eol.txt` has 0, else equal to LF; `BenchVoices.cs` as `Bench.cs`. The PM converts LF.

**Stops:** a FAIL, `cmp=1`, a flip, a line missing or other exit: stop for the human; never a loosened check or a write to `$S` or a baseline.

## Open questions that block implementation

None.

## Blast radius

Recogniser stages compile `BenchVoices.cs`; Phase 3 reads `voice/` as fixed, its block opening `--voice-dump`; Phase 4 extends `voice`.

## Runtime claims

- V7 fits one 600 s call; the harness takes `voice/pin.json`, exit 0 or 1, as `cond/`'s; `TruthCheck` passes each stand-in copy; V9's em dash greps as printed.

Hazards: V0 before V1; V5 before V6; V7 before V8–V12; `bench.sh`, `joined.sh`, the harness, `host_run.py` never concurrent; no step runs `voices.sh`.
