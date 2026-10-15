# Phase 4 — persisted plan (2026-10-15)

Serial, one unit: tuning pass and closing gate; F1-F9 re-made, F7 (record), F10, F11, §10 item 3; bound by §2 Out 1, the *Staging* trap. AB = `Planning~/features/coverage-in-selection/ab-rig/`; J = `.scratch/joined/`; `<s>` = `set` | `unset`; lines at `388c1d3`.

**Rulings** (the maintainer, at plan approval, 2026-10-15, in session): (1) the tuning pass records, it does not re-pick: 90 Hz stays; (2) one notch = the Quest 3's neighbouring rates, 80 and 120 Hz (it offers 72/80/90/120); (3) re-made: the `joined.sh` acceptance runs, Command 3, the `--dump` runs with `timing`, DocCheck, the F6 canary; Phase 3's §6 stops stand; (4) Commands 1-2 not re-run if `git diff --name-status ca8c6ea..HEAD -- Runtime/ NativeBridge~/ Tests~/` is empty; no Command 4; (5) the `pending` labels stay unexercised on real data, no seeded canary; for G2.

## Files to change

| Path | Change | Why |
|---|---|---|
| `plan-4.md` | create | this plan |
| `architecture.md` | link line now; *Tuning record* cell (15) | F7; §10 item 3 |
| `memory/2026-10-15.md` | *Probe record* (13), by the PM | F7 |

No rig, shipped or test change.

## Contracts and interfaces

**Dispatch.** A, `compile-check`: 1-14 in order, never the main thread. B, `architect-reconcile`: 15. No agent git write.

**Conventions.** Claude's Git Bash, project root (F10). `stage.sh` always `env -u MSYS_NO_PATHCONV bash AB/stage.sh …` (recipe :232). Edits and compares: `python3 -I` scripts under J; JSON LF (`newline=''`); a staged C# edit keeps the file's endings (CR count unchanged; rig files CRLF). A leftover out dir: `find <dir> -delete` first, never a `jrr-p2-*`/`jrr-p3-*` path.

**Set and unset.** `joined.sh`: `MSYS_NO_PATHCONV=1 bash AB/joined.sh … --out J/jrr-p4-<x>-set`, `env -u MSYS_NO_PATHCONV bash AB/joined.sh … --out J/jrr-p4-<x>-unset` (its `stage.sh` stays under `env -u`, :142). A hand sequence: `( export MSYS_NO_PATHCONV=1; <seq> )`, `( unset MSYS_NO_PATHCONV; <seq> )`; the subshell holds Command 3's `cd` (WND-1). A pair: same exit; each written `scored-a.jsonl`, `scored-b.jsonl`, `report.md`, `trace-a.jsonl`, `trace-b.jsonl` `cmp`-equal across it and to the named Phase 3 dir's; a refusing pair writes none.

**Probe line** (13), `<r>` = `80`, `120`:
1. `stage.sh main J/jrr-p4-tune-<r> recogniser`.
2. Staged `JoinedRecogniser.cs`: `internal const float TickRate = 90f;` (:38) gets `<r>f`, exactly one replacement, else stop.
3. There: `dotnet build -v q --nologo`, 0 errors; `dotnet run --no-build -- --joined drive-check`, `5/5` (follows `DeliveryFrame` :82, `AfterFrame` :92); side a's score as `joined.sh` :163-165, `--layer recogniser --side a --vary parser --pinned`, `--dump J/jrr-p2-a.jsonl`, default manifest and pin, `--out J/jrr-p4-tune-<r>/scored-a.jsonl`, `cygpath -am`: exit 0, trace beside it.
4. `J/jrr-p4-tune.py` vs `J/jrr-p3-f3/scored-a.jsonl` (keys `Joined.cs` :436-447): only `fireTimes`, `delay`, `pending`, trace `frameTime` differ freely. A moved `class` is a result: exit 0, moved cases printed. Header off save `tickRate`, or a held class with `score`, `slotCredit`, `extraFires`, `finalCount` or `finals` off: exit 1, a stop.

**Probe record**, per rate: `tickRate`, class counts, each moved case or "none", each pending case or "none".

**Evidence cell** (15): "Provisional → Phase 4's tuning pass. … records whether any class moves." becomes: the 90 Hz citations re-verified at HEAD (`KNOWN_LIMITATIONS.md` :363; `Runtime/ModelExtractor.cs` :444); 6's counts; each probe's moves or "none" (80, 120 Hz); "provisional (TTS-only, C12)". Net growth at most 200 bytes.

**Stops (human):** harness exit 1 (never a re-pin); a non-empty ruling-4 diff; a set-vs-unset difference; a re-made output off Phase 3's; probe exit 2 (pending at K) or drive-check failure; `tune.py` exit 1; a false or unchecked claim (no LOOP); CMake refusing a stale `NativeBridge~/build` cache (the PM moves it to `.scratch/`, never deletes); the body over 40,960 after 15.

**Commit set (PM):** `architecture.md`, `plan-4.md`, memory notes; nothing under `.scratch/`, `Runtime/`, `NativeBridge~/`, `Tests~/`.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| Record 90 Hz | Re-pick | Ruling 1; F7; C12 |
| 80, 120 Hz | ± n Hz | Ruling 2 |
| Staged edit, side a, script compare | `joined.sh`; `report` | const re-staged each run (`stage.sh` :61, :78); `Compare` refuses unequal `tickRate` (`JoinedReport.cs` :456-465) |
| §6 stops kept; Commands 1-2 stand | Re-made | Rulings 3, 4 |

## Acceptance criteria

The gate: 1-14 (A), each `<s>` from Claude's Git Bash both ways (F10); then 15 (B).

1. [ ] Ruling-4 diff empty; `python3 -I -c` prints `body_bytes` of `architecture.md`, importing `D:\Workspace\harness\scripts\harness.py` by a `sys.path` insert (40,712 at plan time); `git status --short` recorded. (§9 binding commands; claims k, m)
2. [ ] `stage.sh WORKTREE .scratch/jrr-p4-stage recogniser`: 0 errors, 0 warnings; `drive-check` `5/5`. (F3; claim n)
3. [ ] Command 3 verbatim, `<s>`: its `windows-x64` configure and build and arm64 per the recipe's `### Building NativeBridge`, no errors either pass; harness stdout to `../.scratch/joined/jrr-p4-cmd3-<s>.stdout`: exit 0, 16/16, `cmp`-equal to `J/jrr-p2-cmd3.stdout` (stderr apart). (F1; §9 Command 3; claims c, l, o)
4. [ ] `<s>`, in `NativeBridge~`: the harness, Command 3's arguments plus `--dump ../.scratch/joined/jrr-p4-dump-<s>.jsonl`, stdout `cmp`-equal to `J/jrr-p2-cmd3.stdout`. In 2's stage: `timing --dump` each, then `--dump ../joined/jrr-p4-dump-set.jsonl --dump-b ../joined/jrr-p4-dump-unset.jsonl`: exit 0, `<n>/<n>`. (F1; claims c, d, o)
5. [ ] `joined.sh --vary parser --dump J/jrr-p2-a.jsonl --ref-a main --ref-b main --out J/jrr-p4-f11-<s>`; plus `--layer parser`, `-f11p-<s>`: `Flips: 0`, phase 1's counts (15 scored a side; correct 15, no fire 1; not scored 1; else 0); `-f11p` equal to `-f11`, both to `J/jrr-p3-base`; no trace. (F11; claims a, b, q)
6. [ ] `joined.sh --layer recogniser --vary parser --dump J/jrr-p2-a.jsonl --ref-a main --ref-b main --out J/jrr-p4-f3-<s>`: `Flips: 0`, stderr empty, traces equal; equal to `J/jrr-p3-f3` (split case `missed`, 17 config fields a side, `Tick rate: A 90 Hz, B 90 Hz`, `## Timing`); counts recorded. (F3, F5, F7, F8; claims a, b)
7. [ ] Per `<s>`, a `python3 -I` copy `J/jrr-p4-c7-<s>.py` of `J/jrr-p3-c7.py`, trace path (:9) `J/jrr-p4-f3-<s>/trace-a.jsonl`, dump (:2) kept: last line `bad 0`, 124 dump and 124 trace results. (F4)
8. [ ] `--layer recogniser --vary decoder --dump-a J/jrr-p2-a.jsonl --dump-b J/jrr-p2-b.jsonl --ref main --out J/jrr-p4-f2-<s>`: `Flips: 0`, the one-read line; equal to `J/jrr-p3-f2`. (F2; claims a, b)
9. [ ] 6's form, `--manifest J/jrr-p3-sealed.json --out J/jrr-p4-f9s-<s>`: `sealed-skipped` 1; plus `--evaluate`, `-f9e-<s>`: `HOLDOUT EVALUATION`; equal to `J/jrr-p3-f9s`, `-f9e`. (F9; claims a, b)
10. [ ] 6's form, `--dump J/jrr-p3-kill.jsonl --out J/jrr-p4-f9k-<s>`: exit 3, `cease_fire.wav` named; no scored or trace file. (F9; claim j)
11. [ ] F6, a hand sequence `<s>` into `J/jrr-p4-f6-<s>`: `stage.sh main <dir>/stage-a recogniser`, `stage-b`; stage-b's `Runtime/Commands/VoxrCommandRecogniser.cs` :31 `[SerializeField] float minScore = 0.6f;` (only staged hit) gets `1.5f`, exactly one replacement; both built, not re-staged; each side scored in its stage as `joined.sh` :163-165, `--pinned --vary parser --layer recogniser`; `report` as :182; all `--dump`, `--manifest`, `--pin`, `--out` `cygpath -am` (`joined.sh` `abspath` :130-139, :155-157): Phase 3's headers embed them, relative breaks the `cmp`. Equal to `J/jrr-p3-f6`: `Flips: 14`; B `minScore=1.5`, 15 `missed`, `silence_negative.wav` `correct (no fire)`. (F6, F9; claims a, b, h)
12. [ ] DocCheck verbatim (recipe :227), a hand sequence `<s>`: exit 0, no `FAIL`, `66/66`. (F11; claim i)
13. [ ] *Probe line* at 80, 120 Hz: header `tickRate` `80`, `120`; `J/jrr-p4-tune.py` exit 0, moves listed; *Probe record* reported. (F7; §10 item 3; claims e, f, g, p)
14. [ ] `git status --short` as 1's; nothing under `Runtime/ NativeBridge~/ Tests~/`. (§2 Out 1)
15. [ ] *Evidence cell* written; body re-measured as 1, at most 40,960. (F7; §10 item 3)

## Open questions that block implementation

None.

## Blast radius

- `build-windows/` rebuilt in place; `NativeBridge~/build` absent, so arm64 configures fresh; `J/jrr-p4-*`, `.scratch/jrr-p4-stage`, `.scratch/doccheck` generated, never committed.
- `body_bytes` skips `## Build plan` (link line free). After 15 the body nears 40.9 KB; the Phase 4 as-built entry and G2's reconcile will cross it: the as-built pass's ruling, not here.
- G2: `pending` labels unexercised (ruling 5); the Quest check and report read are the human's.

## Runtime claims

- a (5, 6, 8-11): each pair is byte-equal; no out-dir, stage or shell path enters an output.
- b (5, 6, 8, 9, 11): each re-made output equals its Phase 3 dir's (rig, `main`, manifest, pin, dumps unchanged).
- c (3, 4): every harness stdout equals `J/jrr-p2-cmd3.stdout`, either shell.
- d (4): two dumps of one build agree on finals and word times exactly.
- e (13): the rate edit compiles, reaches the header, and changes only `tickRate` and *Probe line* 4's free fields unless a class moves.
- f (13): `drive-check` passes 5/5 at 80 and 120 Hz.
- g (13): no case pending at K at either rate.
- h (11): the canary gives Phase 3's classes; the edit keeps the file's endings.
- i (12): DocCheck gives 66/66 in either shell.
- j (10): the kill check exits 3 before writing, either shell.
- k (1): an empty ruling-4 diff keeps Phase 2's Commands 1-2 green.
- l (3): the arm64 configure succeeds on a fresh `NativeBridge~/build` (stale-cache refusal: the recipe's contingency, a stop).
- m (1): `harness.py` imports under `-I` via a `sys.path` insert; `body_bytes` gives the measured body.
- n (2): 0 build warnings.
- o (3, 4): the `windows-x64` configure and build succeed in each shell, twice over one `build-windows`.
- p (13): the probe stage, its own out dir, builds and runs `score` outside `joined.sh`, trace beside it.
- q (5): explicit `--layer parser` output equals the default.
