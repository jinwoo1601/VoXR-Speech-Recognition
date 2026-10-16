# Phase 3 — persisted plan (2026-10-16)

**Phase 3 - Rig sweep machinery**, unsplit, serial, no units: F4 (machinery); *Buffer setups*, *Kill criteria in the report*. RIG=`Planning~/features/coverage-in-selection/ab-rig/`; REC=`Runtime/Commands/VoxrCommandRecogniser.cs`; `$S`=`.scratch/endpointer-profile-phase-3`; live lines. No binding command.
**Out of scope:** the sweep, `confgen`, overlay, presets, flip, `stage.sh`'s closure (Phases 4-8); `Joined.cs`, `JoinedInput.cs`; `Runtime/`; product docs; CHANGELOG; the reconcile.

**Readings** (within the register's text):
- R1. Each *edited* initializer must match exactly once — `time` checks and edits `bufferWindow = 0.5f;` alone; `eager` checks and edits both.
- R2. `correct-no-fire` has no `delay`: S averages over cases `correct` on both sides with a `delay` on both, n their count, printed; n = 0: S `n/a`, KC1 `not evaluated` (never a pass).
- R3. W is the scored headers' `config.bufferWindow`; it and `config.eagerFlushOnCompleteMatch` present and equal across A and B (one stage), else a named report stop (exit 2).
- R4. KC2 STOP is report text, not an exit code (Phase 5: the human reads it); exit codes unchanged.

## Files to change

| Path | Change | Why |
|---|---|---|
| RIG `stage.sh`, `joined.sh` | modify: Steps 1, 2 | F4; *Buffer setups* |
| RIG `JoinedReport.cs` | modify: Step 3 | F4; *Kill criteria* |
| `$S/` probe files | create, untracked | Step 4 |

The three code files stay CRLF. Probe files only under `$S/` and an outside temp dir.

## Contracts and interfaces

**Dispatch.** A, `code-writer`: Steps 1-3 in order. B, `compile-check`: Step 4 once A ends unstopped. No git writes; Git Bash, never WSL.

**Step 1 - `stage.sh`.**
- Usage :4, `USAGE` :12: `stage.sh <git-ref|WORKTREE> <outdir> [recogniser [--buffer time|eager --window <s>]]`. Arg 3's rule (:15-19) stands; args 4-7 need `recogniser` and both flags, once each, either order; `--buffer` `time` or `eager`; `--window` `<digits>` or `<digits>.<digits>` (`2.` rejected); else `$USAGE` on stderr, exit 2.
- After the copy loop (:64-71), before :81: `grep -F -c` (`|| true`) counts `$OUT/VoxrCommandRecogniser.cs` lines holding `bufferWindow = 0.5f;`, then for `eager` `eagerFlushOnCompleteMatch = false;`. Not 1: stderr `stage.sh: initializer '<text>' matches <n> times in <ref>'s VoxrCommandRecogniser.cs`, exit 2. Then `sed -i`, literal, `.` escaped, no `^`/`$` (CR stays): `0.5f;` to `<s>f;` in that text; for `eager`, `false;` to `true;` in the other. Each new text re-counted: not 1, the same stop.
- After :89, before `cat` (:90), appended to `STAGED_FROM`: `edit: bufferWindow = 0.5f -> <s>f` (`--window 2.0`: `-> 2.0f`), then for `eager` `edit: eagerFlushOnCompleteMatch = false -> true`.
- Without the flags the stage is byte-for-byte today's.

**Step 2 - `joined.sh`.**
- Vars (:47) gain `BUFFER`, `WINDOW`; `--buffer`, `--window` join the whitelist (:66) and `take` cases (:70-82). After `--layer` (:107-113), before `mkdir` (:123): `die "--buffer needs --window"`, `die "--window needs --buffer"`; both at `parser`: `die "--buffer and --window need --layer recogniser, not: parser"`.
- `stage()` (:141-144): `--buffer "$BUFFER" --window "$WINDOW"` after `recogniser`, when set.
- Header: usage lines (:10-13) gain `[--buffer time|eager --window S]`; step 2 (:27-29) names the initializer edit.

**Step 3 - `JoinedReport.cs`.** `Render` (:532-746), if `layered && vary == "decoder"`: `## Sweep measures` after `## Transcripts` (:735-744); cases paired by index (`Compare` :440-511); numbers by `Seconds` (:800-801). Lines:
- `- Buffer: <time-driven|eager commit>, W = <W> s`: eager when `config.eagerFlushOnCompleteMatch` is `true`.
- `- Finals per utterance A: <mA>; B: <mB>; Δf: <mB − mA>`: mean `finalCount` over `scored` cases.
- `- S: <S> s over <n> cases correct on both sides`: R2's `correct-fire`/`correct-no-fire` cases; S mean A `delay` − B `delay`.
- `- KC1 (Δf × W ≤ S): <pass|fail|not evaluated>`, unrounded.
- `- KC2: none`, or `- KC2: STOP — <k> case(s)` then `  - <case>: A "<finals space-joined>" → B "<…>"` per case, scored both sides, ordinal, file order.

R3's stop, in `Render` before the write (`Run` :157-160): a side lacking either key: `JoinedInput.Stop(2, "Sweep measures: <path>: config has no <key>")`; a key's texts unequal: `"Sweep measures: config <key> differs: <pathA> <a>, <pathB> <b>"` (as :485). Header comment :7-17 names the section.

**Step 4 - green** (`compile-check`; project root). Rules: (a) the Bash tool collapses `\\` to `\`: probe commands backslash-free; Python in `$S/*.py`, run `python3 -I`; (b) Git Bash `grep -c $'\r$'` prints 0 on CRLF: count CR in Python; (c) a probe runs only the plan's checks, nothing nested in `"$(...)"`. One Bash call per check, opening `P` = `S=.scratch/endpointer-profile-phase-3 R=Planning~/features/coverage-in-selection/ab-rig A=.scratch/endpointer-profile-phase-2/a.jsonl V="bash $R/joined.sh --vary decoder --dump-a $A --ref WORKTREE --dump-b" C="python3 -I $S/check.py";`.

Probe files, written first (`mkdir -p $S`), each printing `ok` or its misses, exit 0 or 1:
- `$S/check.py setup <b> <w>`: `$S/<b>-<w>/report.md` has twice `  - bufferWindow=<0.5|2>` and `  - eagerFlushOnCompleteMatch=<true iff eager>`, and ends in Step 3's lines: W `0.500`/`2.000`, `Δf: 0.000`, S `0.000` over n ≥ 1, KC1 `pass`, `KC2: none`; its `stage-a/STAGED_FROM` has Step 1's edit lines.
- `check.py endings`: `$S/eager-2.0/stage-a/VoxrCommandRecogniser.cs` vs REC, split at `\n`: equal line counts, only lines 72 and 79 differ, equal `\r` counts; RIG's three files: CR count = line count.
- `check.py kc2`: `$S/kc2/report.md` has `- KC2: STOP — 1 case(s)` then `  - cease_fire.wav: A "cease fire" → B "cease resume"`.
- `check.py real`: `$S/real/report.md`'s Step 3 lines, KC2 cases included, equal a recompute from `$S/real/scored-{a,b}.jsonl` (by index; numbers to three decimals; KC1 at W 0.5, any verdict); config, Buffer as `setup time 0.5`'s; `stage-a/STAGED_FROM` has no `edit:`.
- `$S/make_bword.py`: A to `$S/b-word.jsonl`; in the line with `"case":"cease_fire.wav"` and `"text":"cease fire"`, `"word":"fire"` and that text become `"word":"resume"`, `"text":"cease resume"`, once each, else exit 1; LF kept.
- `$S/unmatch.py <root>`: in `<root>`'s REC, `bufferWindow = 0.5f;` to `bufferWindow = 0.75f;`, once, CR kept.

Checks:
1. `P env -u MSYS_NO_PATHCONV bash $R/stage.sh WORKTREE .scratch/doccheck && (cd .scratch/doccheck && dotnet build -v q --nologo && dotnet run --no-build -- --doccheck)` (recipe :227): exit 0, no `FAIL` line, `66/66 doc claims verified against the real parser.`
2. Per `<b> <w>` in `time|eager` × `0.5|2.0`: `P $V $A --layer recogniser --buffer <b> --window <w> --out $S/<b>-<w>; echo "exit $?"; $C setup <b> <w>`: `exit 0`, `ok`.
3. `P git diff --name-only; $C endings`: only RIG's three files (plus `plan-3.md`, `architecture.md` if uncommitted); `ok`.
4. `P python3 -I $S/make_bword.py && $V $S/b-word.jsonl --layer recogniser --buffer time --window 0.5 --out $S/kc2; echo "exit $?"; $C kc2`: `exit 0`, `ok`.
5. `P T=$(cygpath -m "$(mktemp -d)"); git init -q "$T"; mkdir -p "$T/$R"; cp -r Runtime "$T/"; cp -r $R/. "$T/$R"; python3 -I $S/unmatch.py "$T"; env -u MSYS_NO_PATHCONV bash "$T/$R/stage.sh" WORKTREE "$T/stage" recogniser --buffer time --window 0.5; echo "exit $?"; find "$T" -delete; test ! -e "$T"; echo "gone $?"`: `exit 2`, stderr Step 1's stop (`'bufferWindow = 0.5f;' matches 0 times in WORKTREE's`), `gone 0`.
6. `P $V $A --layer recogniser --buffer time --out $S/guard; echo "exit $?"; $V $A --layer parser --buffer time --window 0.5 --out $S/guard; echo "exit $?"; $V $A --layer recogniser --window 0.5 --out $S/guard; echo "exit $?"`: `exit 2` thrice: Step 2's `--buffer needs --window`, its `parser` die, `--window needs --buffer`.
7. `P $V .scratch/endpointer-profile-phase-2/b.jsonl --layer recogniser --out $S/real; echo "exit $?"; $C real`: `exit 0`, `ok`.

**Stop rules.** A red check returns to `code-writer` (fix pass), never an edit to `Runtime/` or the Phase 2 dumps. `a.jsonl`'s pinned kill check failing (exit 3): stop and report; regenerating A is Command 3 `--dump` (`.claude/bindings/unity.md`), the PM's call.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| Edit the staged copy | internal setters (:509-510); a `Runtime/` default | *Buffer setups*; §2 Out 3 |

## Acceptance criteria

1. [ ] Check 1 green. (Build plan)
2. [ ] Checks 2-4, 7: `ok`, `exit 0` where echoed. (F4: both setups, W 0.5 and 2.0, KC1, KC2)
3. [ ] Checks 5, 6: each `exit 2`, its message; `gone 0`. (*Buffer setups*)
4. [ ] Check 3: the three files CRLF; no edit to `Runtime/`, `Joined.cs`, `JoinedInput.cs`. (§2 Out 3)

## Open questions that block implementation

None; a stop opens one.

## Blast radius

- `stage.sh`'s existing calls are unchanged; `ablate.py` (:139-140) reads `STAGED_FROM` line 1 alone.
- Recogniser-layer decoder reports gain a last section; parser-shape reports do not.
- A recogniser-layer decoder report whose config lacks either key now exits 2.
- `--buffer` under `--vary parser` edits both refs' stages alike (one side still varies, rule 10).

## Runtime claims

- a (1): `grep -F -c` counts CRLF lines; literal `sed -i` keeps CR (check 3).
- b (2): `joined.sh` accepts one dump as A and B.
- c (4.2): `a.jsonl` passes the standing pin's kill check.
- d (4.2): W = 2.0 runs reach the horizon without a pending stop (exit 2).
- e (4.5): `git init`, `cp -r`, Python and `stage.sh` work in a `cygpath -m` `mktemp -d` dir; the stop fires before `STAGED_FROM` needs a commit.
- f (4.4): the reader accepts the edited dump; the edit changes that case's finals.
- g (4): each `joined.sh` run fits one Bash call's time cap.
- h (4.2): `ConfigText` prints floats by `"R"` (`JoinedRecogniser.cs` :352-353): W = 2.0 shows `bufferWindow=2`.
- i (4.2): one dump scored twice in one stage: equal finals and delays, n ≥ 1.
- j (1, 2): the new bash runs in CRLF `stage.sh`, `joined.sh` (Git Bash).
- k (4): `check.py` reads `report.md` as UTF-8 (Δ, —, →).
- l (4.5): `find "$T" -delete` removes a `git init`'d dir on Windows.
