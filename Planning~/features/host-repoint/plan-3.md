# Phase 3 — persisted plan (2026-11-05)

Scope: Phase 3 as the row reads it — `Planning~/host-run/host_run.py` created; the binding's Commands 1-2, Targeted and Failure-rule lines and the recipe's Unity procedure rewritten to it; `testables` never written; a provisional `--wait` default → Phase 4; the row's green in the one checkout — taking on Prerequisites naming `py`, the recipe's killed-script restore (risk :101) and a no-op Unity argv as the planted-XML mutation, dropping nothing; worktree runs are Phase 5's, the bound's value Phase 4's.

## Files to change

| Path | Change | Why |
|---|---|---|
| `Planning~/host-run/host_run.py` | create (`code-writer`, S2), LF, final newline; S10b inserts a line, S10e removes it, `cmp`-checked | F1-F5, §6; register :41-53 |
| `.claude/bindings/unity.md` | modify (`doc-writer`, S1): :6, :7, :11-:13 only; :12 gains `py` only; Fallback :10 stays true, unedited; :5, :14 not owned | the row; :49; risk :100 |
| `Planning~/verification-recipe.md` | modify (`doc-writer`, S1): :13-18 only, line count unchanged | the row; risk :101 |
| `.scratch/host-repoint-p3/`; the hub's `.scratch/host-lock/` | create (PM; the script); never committed | evidence; register :41 |
| TG `Packages/manifest.json`, `Temp/UnityLockfile` | hand-mutated and restored (PM, S7-S9, per OQ1); net none, `cmp`-checked | F2's checks |
| `Planning~/features/host-repoint/plan-3.md` | create | process-docs |
| `Planning~/features/host-repoint/architecture.md` | `## Build plan` link line | process-docs |
| Command 3, README, ADRs, requirements, `Runtime/`, `Samples~/`, `Tests~/` sources, other `.claude/**`, TG's other files | none | the brief |

## Contracts and interfaces

**Names.** ROOT = HUB = `D:/Workspace/VoXR-Speech-Recognition` (`pwd -W`); TG = `D:/Workspace/VoXR TestGround`; UNITY = `C:/Program Files/Unity/Hub/Editor/6000.4.7f1/Editor/Unity.exe`; PF = TG's `Packages/manifest.json` and `Packages/packages-lock.json`; HL = HUB's `.scratch/host-lock`; P = `.scratch/host-repoint-p3`; RES = `.scratch/test-results`; RUN = `py "Planning~/host-run/host_run.py"`. Steps run from ROOT in Git Bash; paths compare per the binding's Path form.

**Script** — stdlib only, Python 3.12 by `py`, `sys` imported at module level; HUB, TG and UNITY are constants, their header comment citing binding :6 as S1 writes it, which names all three:
- Args: `<EditMode|PlayMode>…` (one or both, each once, run in that order), `[--filter "<target>"]` (→ `-testFilter`), `[--wait <seconds>]`, default `WAIT_DEFAULT = 1800`, marked provisional → Phase 4 in the header. A bad argument: exit 3. Targeted names the platform or platforms the target's tests live on.
- Order as the *Flow* (:69): the tree (two levels above the script) equals the working directory, `Tests~` present, `Tests` and `Tests.meta` absent, else exit 3; HL `lock` opened (created if absent), `msvcrt.locking(fd, LK_NBLCK, 1)` at byte 0 polled every 2 s up to the wait, past it exit 2, nothing written; held: `owner` rewritten, the at-rest read (:43), snapshots to HL `rest/manifest.json` and `rest/packages-lock.json`, TG's `git status --short` read as the baseline, the repoint (:45), the `Tests~` listing and rename (:53); per platform RES created if absent, `<p>.xml` deleted, Unity run as Seams :64 into RES `<p>.xml` and `<p>.log` (`<p>` lowercased), argv built only in `unity_argv(platform, filter)`; `finally`: the restore (:45, :53), the at-rest read again, TG's status compared with the baseline, the lock released. `owner` is never deleted.
- `owner`: one line, `tree=<ROOT> platforms=<p,…> pid=<n> start=<UTC ISO 8601, Z>`.
- A platform is green on Unity exit 0, `<p>.xml` absent after the delete and present after Unity, its root `test-run`'s `total` above 0 and `failed` and `inconclusive` 0, and the lock file's `version` `file:<tree>`; `total` 0 is not green — a filter matching nothing proves nothing.
- Stdout, each line flushed: `lock wait: <s>s`; per platform `<Platform>: <total>/<passed>/<failed>/<inconclusive> exit=<n> wall=<s>s` (`-` for counts with no XML), where not green `<Platform>: not green — <reason>`, the first that applies of `exit <n>`, `no results XML written by this run` (not deleted, or absent after Unity), `total 0`, `failed <n>`, `inconclusive <n>`, `resolved <version>`; then `resolved: <version> (packages-lock.json)`; `host: at rest`; `host status: unchanged` or `host status: changed — <lines>`; `tree: kept new path <path>` per new non-`.meta` path; last `result: green` or `result: not green`. Stops: `BLOCKED: host lock held past <s>s — owner: <its text | absent>`; `stop: <what was found>`, after writes also naming HL `rest/`.
- Exit 0 green; 1 not green; 2 BLOCKED; 3 a stop — before any write, or the restore check or second at-rest read missed — 3 over 1.

**Binding lines**, each one line, CRLF:
- :6 Command 1: Commands 1-2 are one bracket, `py "Planning~/host-run/host_run.py" EditMode PlayMode`, started in the background from the tree root; the script holds as constants the hub `D:\Workspace\VoXR-Speech-Recognition`, the host `D:\Workspace\VoXR TestGround` and `C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe`; it takes the host lock under the hub's `.scratch\host-lock\` (`--wait`, default 1800 s, provisional → Phase 4), reads the host at rest, points its `file:` at the tree, renames `Tests~`, runs Unity per platform into the tree's `.scratch\test-results\` (`editmode`/`playmode` `.xml`, `.log`), restores and checks the host in a `finally`, never writes `testables`; procedure and recovery in the recipe — expected green: exit 0, the `EditMode:` line `failed` 0 and `inconclusive` 0 from the XML this run wrote, `resolved: file:<tree>`, `host: at rest`.
- :7 Command 2: PlayMode, no command of its own: Command 1's one invocation runs it in the same bracket, never a second invocation (most parser and command tests are PlayMode, so it is never dropped) — expected green: the `PlayMode:` line as Command 1's, same exit-0 run, and `git status --short` showing no `Tests` or `Tests.meta` leftovers.
- :11 Failure rule: today's text, plus a named platform whose run reports `total` 0, a `resolved:` other than `file:<tree>`, no `host: at rest`, and the script's exits 1 not green, 2 BLOCKED on the host lock, 3 a stop for the human, never repaired.
- :12 Prerequisites: after "imported at least once", "; for Commands 1-2 Python 3 as `py` on the Git Bash PATH (stdlib only)".
- :13 Targeted: Command 1's invocation naming only the platform or platforms the target's tests live on (one or both), `--filter "<target>"` added — a test name, fixture or namespace regex, passed as `-testFilter`; a named platform at `total` 0 is not green — a filter matching nothing proves nothing.

**Recipe** — :13 keeps its text to "what's tested", its tail now "Procedure, one run of `Planning~/host-run/host_run.py` (binding Commands 1-2):"; :14-18 become sub-steps 1-5:
1. From the tree root in Git Bash, `py "Planning~/host-run/host_run.py" EditMode PlayMode` — the full run names both, `Tests/Editor` EditMode, `Tests/Runtime` PlayMode; targeted names only the platform or platforms the target's tests live on, plus `--filter "<target>"`, and a named platform at `total` 0 is not green; `--wait <s>` the lock wait (default 1800, provisional → Phase 4).
2. It takes `<hub>/.scratch/host-lock/lock` (holder in `owner`), reads TG at rest (`file:` the hub, `testables` as TG's `HEAD`, `packages-lock.json` on the hub, no `Temp/UnityLockfile`), snapshots both `Packages/` files to `<hub>/.scratch/host-lock/rest/`, points `file:` at the tree, never writes `testables`, renames `Tests~` → `Tests`.
3. Per platform it deletes the old XML and runs `"/c/Program Files/Unity/Hub/Editor/6000.4.7f1/Editor/Unity.exe" -runTests -batchmode -projectPath "D:\Workspace\VoXR TestGround" -testPlatform <EditMode|PlayMode> -testResults "<tree>\.scratch\test-results\<editmode|playmode>.xml" -logFile "<tree>\.scratch\test-results\<editmode|playmode>.log"`, `-testFilter "<target>"` when targeted, no `-quit`; minutes on first import.
4. In a `finally`: `Tests` → `Tests~`, run-created `.meta` deleted (root `Tests.meta` incl.), snapshots written back and checked, at-rest re-read, lock released; exit 0 green, 1 not green, 2 BLOCKED (owner quoted), 3 a stop for the human; a run never repairs.
5. Recovery, the human's, after a killed script or an exit 3 naming `rest/`: with no Unity running and no run holding the lock, copy `rest/manifest.json` and `rest/packages-lock.json` over TG's `Packages/` files and `cmp` each, rename the tree's `Tests` back to `Tests~`, delete its new `.meta` and root `Tests.meta`, clear a stale `Temp/UnityLockfile`; never `git checkout` in TG.

**Steps:**
- S0 PM, before S1, the plan and link line committed: `mkdir -p P/pre P/mut`; `git status --short -- ':!memory'` > `P/g0`.
- S1 `doc-writer`: the binding and recipe lines above; nothing else.
- S2 `code-writer`, scope the script alone, verification `not run — the gate runs the suite`: the script per the contract.
- S3 PM: `cp PF P/pre/ && touch P/stamp`; TG's `git status --short` > `P/t0`; TG's `Temp/UnityLockfile` absent; `py -c "import ast,sys;b=open(sys.argv[1],'rb').read();ast.parse(b);assert b'\r' not in b"` on the script exits 0.
- S4 `compile-check`, Commands 1-2: green — exit 0; `EditMode:` and `PlayMode:` `failed`/`inconclusive` 0; `resolved: file:D:/Workspace/VoXR-Speech-Recognition (packages-lock.json)` after each; `host: at rest`; `result: green`.
- S5 PM: `find RES -newer P/stamp` lists both XMLs and logs; `cmp` each PF with `P/pre` and with HL `rest/`; `Tests~` present, `Tests`, `Tests.meta` absent, `find Tests~ -name '*.meta'` empty; HL `owner` names ROOT and `EditMode,PlayMode`; `cp RES/editmode.xml RES/playmode.xml P/`.
- S6 PM, exit 2: `mkdir -p` HL; a `py -c` holder opens HL `lock`, takes `msvcrt.locking(fd, LK_NBLCK, 1)` at byte 0, writes `owner` as `hand-hold S6`, runs `RUN EditMode --wait 5` as its child, exits with its code: 2, `BLOCKED:` quoting `hand-hold S6`; `cmp` each PF with `P/pre`; `Tests` absent.
- S7-S9 PM per OQ1, exit 3, each: mutate; `cp PF P/mut/`; `RUN EditMode --wait 5` exits 3, its `stop:` naming the miss; `cmp` each PF with `P/mut/`; restore; `cmp` each PF with `P/pre`. Byte edits by a PM `py -c` asserting one occurrence; restore `cp P/pre/manifest.json`.
  - S7: `"com.jinwoo1601.voxr": "file:D:/Workspace/VoXR-Speech-Recognition"` → `… "file:../elsewhere"`.
  - S8: `test -e` TG `Temp` recorded as T just before; `mkdir -p` TG `Temp`, `touch` its `UnityLockfile`; restore by `rm`, and `rmdir` `Temp` where T was false.
  - S9: the bytes `,` CRLF `  "testables": [` CRLF `    "com.jinwoo1601.voxr"` CRLF `  ]` deleted.
- S10 planted XML: (a) PM `cp` the script to `P/pre/`; (b) `code-writer` dispatch A, as S2: one Edit inserting `return [sys.executable, "-c", ""]` as the first line of `unity_argv`'s body; (c) PM `cp P/editmode.xml P/playmode.xml RES/`; (d) `compile-check`, Commands 1-2: FAIL — exit 1, both `<Platform>: not green — no results XML written by this run`, `host: at rest`; PM: both RES XMLs absent, `cmp` each PF with `P/pre`; (e) `code-writer` dispatch B: one Edit removing that line; (f) PM `cmp` the script with `P/pre/host_run.py` — S4's bytes, so S4's green stands.
- S11 `compile-check`, Command 4 alone: green.
- S12 PM: TG's status `diff`s empty with `P/t0`; `cmp` each PF with `P/pre`; TG `Temp/UnityLockfile` absent; `Tests`, `Tests.meta` absent; `git status --short -- ':!memory'` against `P/g0` adds exactly the binding, the recipe and `Planning~/host-run/`, drops none; `git diff` touches S1's lines only; CRLF count equals line count in both. After the commit, `rm -rf P`.

**Stops**, never worked around: S4 not green — the human, with the output; an exit 3 after writes is the recipe's step 5, nothing committed. S6-S9 any other exit or a `cmp` miss — the PM first restores PF from `P/pre`, removes S8's file, `cmp`s, then stops. S10d green — S10e-f still run, then stop; S10f a miss — the human restores the script from `P/pre`. S11 red — stop.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| "§9 procedure" read as `## Verification bindings` (:9) item 1, :13-18 | a new section | the recipe has no §9; a reading, not a design change |
| Planted XML by a reverted `code-writer` mutation | a test-only hook in the script | no test surface in the tool; plan-1's precedent; `cmp` proves the revert |
| No-op argv: exit 0, nothing written | `-testResults` redirected | no Unity, no import; only the delete-then-present decision fails |
| S10 by `compile-check` | by the PM | the gate itself reads it not green |
| Exits 2-3 by the PM, `EditMode --wait 5` | `compile-check` | not binding commands; no Unity reached |
| The holder runs the script as its child | a background holder and a wait | no race, no `sleep`; the lock frees as it exits |
| TG mutations by the PM, `cp`/`cmp` | `code-writer` | OQ1's ruling, 2026-11-05 |
| `--wait` 1800 s, provisional | no default | a bound for one run; Phase 4 sets it |
| Script LF | CRLF | Python norm, shebang-safe; process docs keep CRLF |
| Prerequisites names `py`; Fallback kept | neither | `py` is new for Commands 1-2; :10 stays true |
| Targeted names its platforms; `total` 0 not green | both platforms always | a one-platform filter leaves the other empty; zero tests prove nothing |

## Acceptance criteria

1. [ ] Binding :6, :7, :11-:13 and recipe :13-18 only, CRLF; the script LF; nothing else changed against `P/g0` (S12) (the row).
2. [ ] Commands 1-2 green by the script via `compile-check`, the XMLs and logs in RES newer than the stamp, `resolved: file:<hub>` each, `host: at rest` (S4, S5) (F4, F5, F8).
3. [ ] PF byte-identical to `P/pre`; `Tests~` back, no `Tests`, `Tests.meta` or `.meta` (S5) (F3, §6).
4. [ ] Lock held by a second process: exit 2, owner quoted, PF unchanged (S6) (F1).
5. [ ] Each of S7-S9 exits 3 naming its miss, PF unchanged by the run, then restored, `cmp` 0 (F2, §6).
6. [ ] Planted XMLs, own write prevented: exit 1, plants deleted, the script restored, `cmp` 0 (S10) (F4).
7. [ ] Command 4 green (S11) (F8).
8. [ ] TG status equal at S3 and S12; no git write by any agent or in TG (§6; CONSTITUTION).

## Open questions that block implementation

none

OQ1 (who makes S7-S9's TG edits) ruled by the human, 2026-11-05: for this phase only, the PM makes S7-S9's byte edits to TG's `Packages/manifest.json` and creates and removes `Temp/UnityLockfile`, each edit restored from `P/pre` and `cmp`-checked, TG's `git status --short` equal at start and end; not a design change.

## Blast radius

- Every later Commands 1-2 run and `compile-check` brief reads the new lines; generated `.claude/skills/*` naming the old Commands 1-2 stay stale (out of scope).
- HL appears, ignored; S4 rewrites TG's `Library/` and `Temp/`; S10 empties RES of XMLs.
- adr-0002 :13 still says `testables` is added (a record, unedited); the layout binding omits `Planning~/host-run/` (risk :102).
- Phases 4-5 run this script.

## Runtime claims

- a (S4): `compile-check` reads :7 as no command and runs one invocation.
- b (S4, S6): `msvcrt.locking` `LK_NBLCK` at byte 0 fails while another process holds it, and frees when that process ends.
- c (S4): Unity, launched by argv list, writes the XML and log at the drive-letter paths, exits 0 on all-pass, root `test-run` carrying `failed` and `inconclusive`.
- d (S4): in the one checkout `packages-lock.json` keeps `file:<hub>`.
- e (S7-S9): the at-rest read stops each mutation before any TG write.
- f (S10): `sys.executable -c ""` exits 0 and writes nothing.
- g (S10): an inserted line removed by the inverse Edit restores the bytes.
- h (S0, S12): `git status --short -- ':!memory'` lists every change but `memory/`.
- i (Targeted; no step runs it): Unity's exit and XML when `-testFilter` matches no test; `total 0` or no XML is not green either way.
- j (S10d): `compile-check` reports the script's exit 1 as FAIL, no retry, no tool error.
- k (S6): the holder's child, a separate process, is refused the byte lock while the holder holds it.
- l (S10b): the script's module-level `import sys` makes the inserted `sys.executable` resolve.

Hazards: from S0 to S12 nothing committed and ROOT changed only by S1, S2, S10b and S10e, or S12's `P/g0` compare misreads; from S4 to S12 no other Unity run and no Editor open on TG; between S7 and S9's restores TG's manifest or `Temp/` is hand-mutated, a stopped step leaving it to the PM's restore; between S10b and S10f the hub's script runs no Unity, so no commit and no other Commands 1-2 meanwhile.
