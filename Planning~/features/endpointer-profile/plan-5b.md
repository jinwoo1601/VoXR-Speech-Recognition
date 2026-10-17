# Phase 5b — persisted plan (2026-10-17)

**Phase 5b - second rule2 sweep**, follow-up to plan-5; unsplit, serial, no units, no code: F4 (requirements :36 calls 0.5/0.35/0.25 the *first* sweep), §4 item 6. Ruled 2026-10-17 by the human, in session: "I'll take your recommendation" - no Phase 5 candidate was eligible (KC1 fails at W 2.0), so rule2 is swept at 0.45, then 0.4; within each, time 0.5, time 2.0, eager 0.5, eager 2.0 (D4's shape). rule1, rule3-rule5 unmoved. plan-5's Rules, `X`, Tools, `sweep.py` contract, Stop rules and Gate Skips are cited by name. Live lines.
**Out of scope:** plan-5's; any write under `$S`; filling the *Sweep record* / *Tuning record*.

## Files to change

| Path | Change | Why |
|---|---|---|
| `$T/sweep.py` | create (`code-writer`): `$S/sweep.py` copied, line 26 edited | Steps 1-3 |
| `$T/`: `conf-<v>.out`, `pin-<v>.json`, `b-<v>.jsonl`, `r<v>-<b>-<w>/`, `table.txt` | the tools create | Steps 1-3; F4 |
| `NativeBridge~/vendor/overlaid/rule2-0.45/`, `rule2-0.4/` | `confgen` creates | Step 1 |
| `plan-5b.md`; `plan-5.md`; `architecture.md` | create; pointer; link line (this dispatch) | this plan |

`$S` (`.scratch/endpointer-profile-phase-5/`) is read only: the first eight rows' evidence. Outputs are ignored (`.gitignore` :37, :45).

## Contracts and interfaces

**Dispatch.** A, `code-writer`: Steps 0-3, one Bash call each. B, `compile-check`: the gate once A ends unstopped. No agent git writes; Git Bash, project root.

**Rules.** plan-5's a-f; (a) reads: Python only in `$T/sweep.py`, `$S/sweep.py`. (g) Commands verbatim, no pipe, redirect or wrapper beyond those written (Phase 5's writer piped a harness into `grep`, the chain reading `grep`'s exit); nothing in `$T` the Files table does not name. (h) Nothing writes under `$S`.

`X` = plan-5's `X` plus `T=.scratch/endpointer-profile-phase-5b;`.

**`$T/sweep.py`.** plan-5's contract, `<S>` as `$T`, D4's order as 5b's. Only :26 changes: `VALUES = ["0.35", "0.25"]` to `VALUES = ["0.45", "0.4"]`; `table`'s names (:233) and rejection (:235-243) follow it. Kept: :2-3 (no value), `SETUPS` :27, `D4` in the :243, :309 messages (D4's order and stop hold).

**Side A.** `$S/a.jsonl`, not re-dumped: 37/37 today, same tree and build; each report's `- Kill check: A passed` re-proves it.

**B models.** `confgen` takes `0.45`, `0.4` (`Program.cs` :60-66, `EndpointerOverlay.IsDecimal` :67-98), copies to an absent `--out` (:84-95), writes the value text verbatim (`EndpointerOverlay.cs` :197, :217). The two dir names are new.

**Steps** (`<v>`, `<b>`, `<w>` literal):
0. `X test -e $T; echo "present $?"; git status --short; find $S/a.jsonl -newer $S/r0.35-time-0.5/report.md; python3 -I $S/sweep.py table $S | cmp - $S/table.txt; echo "exit $?"`: `present 1`; only `??` lines; `find` silent; `exit 0`. `$T` present: BLOCKED, never `rm -rf`. Then `X mkdir $T && cp $S/sweep.py $T/sweep.py; echo "exit $?"`: `exit 0`; the Edit tool changes line 26 alone. Then `X diff $S/sweep.py $T/sweep.py; python3 -I $T/sweep.py; echo "exit $?"`: `26c26`, that line pair alone; the usage line; `exit 2`.
1. Per `<v>`, 0.45 then 0.4: `X O=$N/vendor/overlaid/rule2-<v>; $G --model $V --out $O --rule2 <v> > $T/conf-<v>.out && cp $P $T/pin-<v>.json && $H --model $O --fixtures $F --manifest $T/pin-<v>.json --write-baseline --dump $T/b-<v>.jsonl && python3 -I $T/sweep.py conf $T/b-<v>.jsonl $T/conf-<v>.out; echo "exit $?"`: `"passed": 37`, `ok`, `exit 0`; a failure ends the chain, its exit echoed.
2. Eight runs, in order: `X bash $R/joined.sh --vary decoder --dump-a $S/a.jsonl --dump-b $T/b-<v>.jsonl --ref WORKTREE --layer recogniser --buffer <b> --window <w> --out $T/r<v>-<b>-<w> && python3 -I $T/sweep.py check $T/r<v>-<b>-<w> <v> <b> <w>; echo "exit $?"`: `ok`, `- KC2: none`, `exit 0`. A `- KC2: STOP` stops the runs (D4), reported verbatim.
3. `X python3 -I $T/sweep.py table $T > $T/table.txt && cat $T/table.txt; echo "exit $?"`: eight rows, eligibility, ladder rows, `ok`, `exit 0`; reported whole.

**Stop rules.** plan-5's; nothing under `$S` re-run, the standing pin never re-pinned.

**Gate** (`compile-check`, read-only; each command opens `X`, ends `; echo "exit $?"`):
- `$T/sweep.py conf` (both), `check` (eight): `ok`, `exit 0`. `python3 -I $T/sweep.py table $T | cmp - $T/table.txt`: exit 0.
- Per `<v>`: `$H --model $N/vendor/overlaid/rule2-<v> --fixtures $F --manifest $T/pin-<v>.json`, no `--write-baseline`/`--dump` (`main.cpp` :179-180; pin written only then, :385-395): `"passed": 37`, exit 0.
- Negative controls, mismatch named, `exit 1`: `check $T/r0.45-time-0.5` with `0.45 eager 0.5`, `0.45 time 2.0`, `0.4 time 0.5`; `conf $T/b-0.45.jsonl $T/conf-0.4.out`.
- Phase 5's evidence: `python3 -I $S/sweep.py table $S | cmp - $S/table.txt`, exit 0; Step 0's `find`, silent.
- `diff $S/sweep.py $T/sweep.py`: line 26 alone, exit 1. `ls -A $T`: the Files table's entries alone. `git status --short`: no tracked change.
- Skips: plan-5's, except that Command 3's harness half is Step 1 here, at 37/37 per `<v>`.

**After the build.**
1. The PM proposes from both tables (`$S/table.txt`, `$T/table.txt`).
2. The human rules the preset count, names, values.
3. `architect-reconcile` records the *Sweep record* - Phase 5's eight rows and 5b's, a provenance line each (side A, which plan), and in the record's preamble the 2026-10-17 ruling and 5b's values (0.45, 0.4); the requirements are locked and not edited - and the *Tuning record*.
4. No eligible candidate again: back to the human (on record: reopen fork 4 on a design branch, or no tuned preset).

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| A new plan file | fix pass on plan-5 | plan-5 is near its budget |
| `$T` | outputs in `$S` | `$S` is evidence |
| `$T/sweep.py`, :26 changed | edit `$S/sweep.py` | it rejects other reports (:243) and stays Phase 5's checker |
| Reuse `$S/a.jsonl` | fresh side A | same tree and build; kill check per run |
| No DocCheck | plan-5 Step 1 again | ran today; no tracked change since |

## Acceptance criteria

1. [ ] Step 0 as stated. (F4: evidence kept)
2. [ ] Step 1, both: 37 passed, `ok`, `exit 0`. (F4; F3 record)
3. [ ] Step 2, eight runs: `ok`, `- KC2: none`, `exit 0`. (F4: both setups, both W, KC1, KC2)
4. [ ] Step 3: `$T/table.txt`, eight rows, eligibility, ladder rows, `ok`. (F4; §4 item 6)
5. [ ] Gate: every line as stated. (Out of scope)

## Open questions that block implementation

None; a stop opens one for the human.

## Blast radius

- Two model copies under ignored `vendor/overlaid/`; `rule2-0.35/`, `rule2-0.25/` untouched.
- `$S` and the standing pin read only; `joined.sh` clears (:137) and stages (:159-160) inside each `--out`.
- Phase 6's rows and Phase 7's flip wait on the *Tuning record*.

## Runtime claims

- a (1): `confgen` accepts `0.45`, `0.4`, creates both dirs, exit 0.
- b (1): `--write-baseline --dump` exits 0 on a B model (Phase 5: 0.35 shown, 0.25 inferred).
- c (0, gate): `$S/a.jsonl` is not newer than Phase 5's first report, so unchanged since consumed.
- d (2): `joined.sh` takes a `--dump-a` outside `--out` and `$T` (:131-133, :190).
- e (0-3): `$T/sweep.py` passes `conf`, `check`, `table` on `$T`.
- f (2): `- Model conf B:` carries `=0.45` / `=0.4` verbatim; `check`'s match (:137) holds.
- g (2): each `joined.sh` run fits one Bash call.
- h (2): `joined.sh` exits 0 on a KC2 STOP (plan-3 R4; unchecked).
- i (gate): the plain harness run exits 0 and writes nothing.
- j (2): plan-5 claim l's stage edits still match once each.
- k (0, gate): `python3 -I` and the tools leave nothing in `$T` beyond the Files table (no `__pycache__`).
- l (0): the Edit tool keeps LF, so `diff` shows `26c26` alone.
- m (gate): each negative control exits 1 with its mismatch named; the `table | cmp` compares exit 0.
