# Phase 5 — persisted plan (2026-10-17)

**Phase 5 - Sweep and tuning pass**, unsplit, serial, no units, no code: F4, §4 item 6, §10 item 1. `$S`, RIG=`$R`: as `X` sets them; live lines.
**Out of scope:** edits to `Runtime/`, `Tests~/`, `NativeBridge~/` source, RIG, Phase 2-4 `.scratch/`; presets in code (Phase 6); the flip (Phase 7); product docs; the stale-cite reconcile; filling the records in this build.

**Departures from the build-plan row** (human-approved 2026-10-17):
- D1. `code-writer` runs Steps 0-5, one Bash call each, writing only `$S/sweep.py`; `compile-check` re-runs the read-only checks as the gate.
- D2. DocCheck once, at the start.
- D3. Side A dumped fresh into `$S/`, not Phase 2's `a.jsonl`.
- D4. The first report listing a KC2 case stops the runs, BLOCKED for the human. Order: 0.35, then 0.25; within each, time 0.5, time 2.0, eager 0.5, eager 2.0.
- D5. `architect-reconcile`, not the PM, records the *Sweep record* (`table`'s rows verbatim, with a side-A provenance line) and, after the human's ruling, the *Tuning record*.

## Files to change

| Path | Change | Why |
|---|---|---|
| `$S/sweep.py` | create (`code-writer`) | Steps 3-5 |
| `$S/`: `a.jsonl`, `conf-<v>.out`, `pin-<v>.json`, `b-<v>.jsonl`, `r<v>-<b>-<w>/`, `table.txt` | the tools create | Steps 2-5; F4 |
| `NativeBridge~/vendor/overlaid/rule2-{0.35,0.25}/` | re-served; create | Step 3 |
| `plan-5.md`; `architecture.md` | create; link line (this dispatch) | this plan |

Build outputs are ignored (`.gitignore` :37, :45): no tracked change in the build.

## Contracts and interfaces

**Dispatch.** A, `code-writer`: Steps 0-5. B, `compile-check`: the gate once A ends unstopped. No agent git writes; Git Bash, project root, never WSL.

**Rules.** (a) The Bash tool collapses a doubled backslash: commands backslash-free; Python only in `$S/*.py`, run `python3 -I`. (b) Git Bash `grep -c` misses a trailing CR: count CR in Python. (c) `sed -i` strips CR (`sed -b -i` keeps it); no sed edit planned. (d) `compile-check` has no Write: `sweep.py` is `code-writer`'s. (e) vc-guard refuses subagents VC-state changes. (f) A distinct `--out` per `joined.sh` run: it clears the default `.scratch/joined` (:137).

Each call opens `X` = `N=NativeBridge~; S=.scratch/endpointer-profile-phase-5; V=$N/vendor/vosk-model-small-en-us-0.15; H=$N/build-windows/bin/vosk-bridge-harness.exe; F=Tests~/Fixtures/audio/tts; P=$N/harness/expectations.json; G="dotnet $N/build-confgen/bin/confgen/debug/confgen.dll"; R=Planning~/features/coverage-in-selection/ab-rig;`.

**Tools.**
- `confgen/Program.cs`: `UsageLine` :27-28; existing `--out` cached (:84-95); exit 1 :92-93, :104-107; exit 0, `sha256 <64 hex>` :111, rule lines :113-118.
- `main.cpp`: flags :174-184; a conf not the pin's exits 2 (:228-229): a pin copy per B; `modelConf` header :286-289; baseline write :385-395; exits :427-429.
- `joined.sh`: A `--pinned` :190, B unpinned :191. `stage.sh`: initializer stop :111, `sed -b -i` :119, :122.
- `JoinedReport.cs`: `Corpus` :547-551; `Kill check` :608-612; `Model conf` :613-618; `## Timing` :699-710, `AppendTiming` :917-945; `AppendSweep` :761-836; `Seconds` `0.000` :947-948.
- `Joined.cs`: kill check only `if (pinned)` :215, its `modelConf` line :235, `Stop(3` :276.

**`$S/sweep.py`** (no doubled backslash; LF; reads UTF-8; first, `sys.stdout.reconfigure` to `encoding="utf-8"`, LF `newline`: it prints `→`, `Δ`, `—`). Each mode prints `ok`, else names the mismatch and exits 1.
- `conf <dump> <confgen-out>`: line 1's `modelConf`, UTF-8, has the SHA-256 on the out's `sha256` line; its bytes equal `$V/conf/model.conf`'s but for the rule lines the run set (the out's rule lines the vendor conf lacks), terminators and final one included.
- `check <out-dir> <v> <b> <w>`: `<out-dir>/report.md` has `- Corpus:` naming `37 cases`; `- Kill check: A passed`; `- Model conf B:` carrying `--endpoint.rule2.min-trailing-silence=<v>`; a `- Buffer:` line naming `time-driven` (`time`) or `eager commit` (`eager`) and `W = <w as 0.000>`; the five `## Sweep measures` lines (`- Buffer:`, `- Finals per utterance A:`, `- S:`, `- KC1`, `- KC2:`). After `ok`: the KC2 line and its case lines, verbatim.
- `table <S>`: one *Sweep record* row per `<S>/r<v>-<b>-<w>/report.md`, D4's order, the record's columns, values from the report lines (`rule2 (B)` from `- Model conf B:`, not the dir name); each candidate's eligibility (four KC1 `pass`, every KC2 `none`); per report, each `ladder_*` case's A and B finals and delay from `## Timing`, the only per-rung evidence (no per-category rollup). Exit 1 unless the reports are all eight, or D4's order through one whose KC2 is STOP, named; `ok` last, only on eight.

**Steps** (`<v>`, `<b>`, `<w>` filled literally):
0. `X test -e $S; echo "present $?"; git status --short`: `present 1`; only `??` lines. `$S` present: BLOCKED, never `rm -rf`. Then `code-writer` writes `sweep.py`.
1. DocCheck, recipe :227 (its path as `$R`): `X env -u MSYS_NO_PATHCONV bash "$R/stage.sh" WORKTREE .scratch/doccheck && (cd .scratch/doccheck && dotnet build -v q --nologo && dotnet run --no-build -- --doccheck)`: exit 0, no `FAIL` line, `66/66`.
2. Side A, standing check plus dump: `X $H --model $V --fixtures $F --manifest $P --dump $S/a.jsonl; echo "exit $?"`: `"passed": 37`, `exit 0`. Exit 1 stops for the human, never a re-pin.
3. One Bash call per `<v>`, 0.35 then 0.25: `X O=$N/vendor/overlaid/rule2-<v>; $G --model $V --out $O --rule2 <v> > $S/conf-<v>.out && cp $P $S/pin-<v>.json && $H --model $O --fixtures $F --manifest $S/pin-<v>.json --write-baseline --dump $S/b-<v>.jsonl && python3 -I $S/sweep.py conf $S/b-<v>.jsonl $S/conf-<v>.out; echo "exit $?"`: `ok`, `exit 0`; a failure ends the chain, its exit echoed.
4. Eight runs, D4's order, one Bash call each: `X bash $R/joined.sh --vary decoder --dump-a $S/a.jsonl --dump-b $S/b-<v>.jsonl --ref WORKTREE --layer recogniser --buffer <b> --window <w> --out $S/r<v>-<b>-<w> && python3 -I $S/sweep.py check $S/r<v>-<b>-<w> <v> <b> <w>; echo "exit $?"`: `ok`, `- KC2: none`, `exit 0`. A `- KC2: STOP` stops the runs there (D4), reported verbatim.
5. `X python3 -I $S/sweep.py table $S > $S/table.txt && cat $S/table.txt; echo "exit $?"`: eight rows, eligibility, ladder rows, `ok`, `exit 0`; reported whole.

**Stop rules.** Any other exit or output: BLOCKED with it; no later command. `joined.sh` exit 3 (A's kill check): stop, report. The standing pin is never re-pinned.

**Gate** (`compile-check`, read-only; each command opens `X`, ends `; echo "exit $?"`):
- Re-runs: `sweep.py conf` (both), `check` (all eight, `<v> <b> <w>`): `ok`, `exit 0`. `python3 -I $S/sweep.py table $S | cmp - $S/table.txt`: exit 0. `git status --short`: no tracked change.
- Negative controls, each the mismatch named, `exit 1`: `check $S/r0.35-time-0.5` with a wrong `<b>`, `<w>`, `<v>` in turn: `0.35 eager 0.5`, `0.35 time 2.0`, `0.25 time 0.5`; `conf $S/b-0.35.jsonl $S/conf-0.25.out` (runs halted on a KC2 STOP before 0.25 exists: `conf $S/a.jsonl $S/conf-0.35.out`).
- Skips: Commands 1-2, not run by the approved plan (D1: no tracked source changes; the binding sets them no condition); Command 3's condition (`NativeBridge~/` changed) and 4's (`Runtime/` public API or `Samples~/` changed) are false; Step 2 is Command 3's harness half at 37/37.

**After the build.**
1. The PM proposes the tuning from the table: eligible candidates, preset names, an M-rung reason for any rule3/rule4 sweep the evidence shows.
2. The human rules: preset count, names, values, any rule3/rule4 follow-up. No eligible candidate: the flip is blocked pending the human (Phase 5 row).
3. A rule3/rule4 sweep the ruling justifies is planned at that ruling (its own fix pass on this plan): a `--rule2` value as `confgen` requires (`Program.cs` :54-58), its own file-name tag so no Step 3 name collides, its *Sweep record* rows naming rule3/rule4 in the `rule2 (B)` cell. The rule2 follow-up the 2026-10-17 ruling chose is planned in `plan-5b.md`.
4. `architect-reconcile` records the *Sweep record* and the *Tuning record*.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| D1-D5 | the row as written | human-approved |
| A pin copy per B | the standing pin | exits 2 on another conf |

## Acceptance criteria

1. [ ] Step 1 green. (Build plan: DocCheck)
2. [ ] Step 2: `exit 0`, 37/37. (F4 side A)
3. [ ] Step 3, both: `ok`, `exit 0`. (F4; *Conf record*)
4. [ ] Step 4, eight runs: `ok`, `- KC2: none`, `exit 0`. (F4: both setups, both W, KC1, KC2)
5. [ ] Step 5: `table.txt` with eight rows, eligibility, ladder rows, `ok`. (F4; §4 item 6; §10 item 1)
6. [ ] Gate: re-runs `ok`, `cmp` exit 0, four negative controls `exit 1`; no tracked change. (Out of scope)

## Open questions that block implementation

None; a stop opens one for the human.

## Blast radius

- `rule2-0.25/` is a model copy under ignored `vendor/overlaid/`; `rule2-0.35/` re-served, `Apply` writing nothing on equal bytes (`Runtime/EndpointerOverlay.cs` :249-255).
- DocCheck re-stages `.scratch/doccheck/`; the standing pin is read only.
- Phase 6's tuned rows and Phase 7's flip wait on the *Tuning record*.

## Runtime claims

- a (3): `confgen` on the existing `rule2-0.35` dir re-serves it, exit 0.
- b (3): `--write-baseline --dump` in one invocation exits 0 on a B model (plan-2 4c).
- c (2, 4): the fresh `a.jsonl` passes the standing pin's kill check.
- d (4): each `joined.sh` run fits one Bash call (Phase 3: about 8 s).
- e (5): `## Timing` ladder case names parse as `ladder_<p|m>_*`.
- f (3): `confgen` copies the vendor model to the absent `rule2-0.25`, exit 0.
- g (4): the rig takes a B dump whose header `manifest`, `model`, `modelConf` differ from A's (Phase 3 check 7).
- h (4): `joined.sh` exits 0 on a KC2 STOP (plan-3 R4).
- i (4, 5): `sweep.py` reads `report.md` as UTF-8 (Δ, —, →).
- j (4, 5): `sweep.py`'s stdout carries `→`, `Δ`, `—` through a pipe under `python3 -I` on Windows, LF newlines.
- k (1): DocCheck closes `66/66` today (figure from `memory/2026-10-16.md`, not a run).
- l (4): the `--window 2.0` and `--buffer eager` stage edits match once each in current `Runtime/Commands/VoxrCommandRecogniser.cs`.
