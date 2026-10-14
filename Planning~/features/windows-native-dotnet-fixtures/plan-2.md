# Phase 2 — persisted plan (2026-10-14)

**Phase 2 - host-neutral `generate.sh` and the closing gate** (`architecture.md` `## Build plan` :124). Unsplit. Discharges F3, F4, F7, F9 and both §6 NFRs for `generate.sh` and Commands 1-3; closes WSL-1 at G2 (adr-0001). Decisions: the PM's brief, 2026-10-14. Lines cited at `e6b98be` (Phase 1); if shifted, the named section governs.

## Files to change

| Path | Change | Why |
|---|---|---|
| `Tests~/Fixtures/generate.sh` | modify (:6, :10-11, after :17, :22-26, :31, :34, :39) | F3, F4, F7; Components :79; Data contract :106; register :59, :61, :63 |

## Contracts and interfaces

**Conventions.** Line numbers are pre-edit; apply bottom-up or by anchor text. Lines not named stay byte-unchanged, among them :8-9 (usage; canon gives no new text), :12 `set -euo pipefail`, :13 (the `cd`). Keep LF (no `.gitattributes`; recon item 4). No helper function. No WSL or `/mnt/` text.

**Step 1 - header.** :6 -> Data contract :106's `# Depends:` line, verbatim. :10-11 -> `# ` lines, at the file's width, carrying :106's content: bash on Linux or Git Bash; Python 3; network on first run (pip, voice); Git Bash paths reach Python as `C:/...` (`cygpath -m`), so `MSYS_NO_PATHCONV` does not matter; WAVs are committed, so it runs only to add or change fixtures.

**Step 2 - path form.** Directly after :17, one new line: register :63's `cygpath` line, verbatim.

**Step 3 - bootstrap.** :22-26 become, in order:
1. The layout read (register :61), one line: `$VENV/Scripts` a directory -> `VBIN="$VENV/Scripts"`, `EXE=.exe`; else `VBIN="$VENV/bin"`, `EXE=` (empty).
2. `if [ ! -x "$VBIN/piper$EXE" ]; then`, enclosing 3-7, closed by `fi`.
3. The interpreter probe (register :59): `$PY -c 'import sys; sys.exit(sys.version_info[0] != 3)' >/dev/null 2>&1`, an `if`/`||` condition so `set -e` does not fire; `PY` holds the candidate, expanded unquoted so `py -3` is two words. `PY=""` before the candidate loop; the no-candidate case tests `${PY:-}`, so `set -u` cannot abort before the named error.
   - `${VOXR_PYTHON:-}` non-empty: `PY` is its value, the sole candidate. On failure, to stderr, `generate.sh: VOXR_PYTHON=<value> does not run as Python 3`, then `exit 1`. Register :59's "when set" reads as non-empty (empty counts as unset); the reconcile logs this narrowing.
   - Else `PY` is the first of `py -3`, `python3`, `python` to pass. None passing: to stderr, `generate.sh: no Python 3 found (tried: VOXR_PYTHON unset, py -3, python3, python)`, then `exit 1`.
   - Nothing before a pass writes under `$CACHE`; no venv is made.
4. `echo "Bootstrapping venv with $PY ($($PY --version 2>&1))..."`
5. `$PY -m venv "$VENV"`
6. The layout read of 1, again.
7. `"$VBIN/pip$EXE" install --quiet "piper-tts==1.6.0" "soxr==1.1.0" numpy`

**Step 4 - calls** (:28-40, logic unchanged): :31 and :34 `"$VENV/bin/python"` -> `"$VBIN/python$EXE"`; :39 `"$VENV/bin/piper"` -> `"$VBIN/piper$EXE"`.

**F3's restore is the PM's** (human ruling 2026-10-14, "PM reverts", `memory/2026-10-14.md` :232, :255): vc-guard refuses subagent git writes, `git checkout --` included, so it runs on the main thread between dispatches B and C. This departs from `architecture.md` :108, unedited until the reconcile logs it under As-built deltas.

**Stops for the human:** a false runtime claim (fix PM-ruled, text changed before the phase closes, logged under As-built deltas); a `KNOWN_LIMITATIONS.md` sweep hit; a Command 3 transcript mismatch; the Unity Editor open at dispatch C.

**Commit set:** `Tests~/Fixtures/generate.sh`; `architecture.md` and this plan only if the run changes them. No CHANGELOG line (dev tooling). Nothing in `.scratch/` is committed.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| PM restores `cease_fire.wav` | Agent's `git checkout` (:108) | See ruling above |
| Fixed failure texts | Writer's wording | Item 2 greps the named value |
| `$PY` unquoted, word-split | A bash array | `py -3` is two words; one variable serves both branches; a path with spaces fails the probe, named |
| Probe output silenced | Shown | Store-stub or bash text would blur the named error |
| Layout read inline, twice | Helper function | PM: smallest diff, no helper |
| Empty `VOXR_PYTHON` = unset | Empty as a candidate | `set -u` needs `${VOXR_PYTHON:-}`; an empty command names nothing |

## Acceptance criteria

The gate, in this order. The PM commits `plan-2.md` and the architecture link line before Dispatch A, so item 4's diff sees `generate.sh` alone. Runs go to `compile-check`, never the main thread, from the project root in Claude Code's Git Bash (`MSYS_NO_PATHCONV=1`) unless marked.

**Dispatch A - `compile-check`, offline, after the code-writer:**
1. [ ] `bash -n Tests~/Fixtures/generate.sh` exits 0. (F3)
2. [ ] Negative run (`architecture.md` :115): an empty `.scratch/wndf-p2-neg-cache` (`mkdir -p`), then `XDG_CACHE_HOME="$(pwd -W)/.scratch/wndf-p2-neg-cache" VOXR_PYTHON=no-such-python bash Tests~/Fixtures/generate.sh --only cease_fire`: exit 1; stderr holds Step 3's `VOXR_PYTHON=no-such-python` text; no `venv` under that dir; `C:/Users/excal/.cache/voxr-fixture-gen` still absent. (degraded-dependency NFR)
3. [ ] F4 read-through: with `cygpath` absent and no `$VENV/Scripts`, every path and command word equals the old script's (`$VENV/bin/piper`, `$VENV/bin/pip`, `$VENV/bin/python`; `python3` when `py` is absent), read against `git show HEAD:Tests~/Fixtures/generate.sh`. (F4)
4. [ ] `git diff --stat` lists `Tests~/Fixtures/generate.sh` alone; its hunks sit at Steps 1-4's sites only; `git grep -n -I -i -E 'wsl|/mnt/' -- Tests~/Fixtures/generate.sh` prints nothing. (F7)

**Human `!` step (F3's check, :108):**
5. [ ] Human: `bash Tests~/Fixtures/generate.sh --only cease_fire` from the project root, the cache `C:/Users/excal/.cache/voxr-fixture-gen` absent, with network. PM files the output verbatim to `.scratch/windows-native-dotnet-fixtures-phase-2-f3-human-run.txt`. (F3)

**Dispatch B - `compile-check`, offline:**
6. [ ] From that file: the bootstrap line names `py -3` and `3.12.10`; `generate.py`'s line for `audio/tts/cease_fire.wav` shows a peak within 0.04-0.4. (F3)
7. [ ] `C:/Users/excal/.cache/voxr-fixture-gen/venv/Scripts/python.exe` (`bin/python` on a `bin` layout) opens `Tests~/Fixtures/audio/tts/cease_fire.wav` with `wave` and prints rate, sample width, channels: `48000 2 1`. (F3)
8. [ ] Conversion-on rerun, filled cache: `env -u MSYS_NO_PATHCONV bash -c 'bash Tests~/Fixtures/generate.sh --only cease_fire'`: exit 0; no bootstrap line, no `Downloading voice` line; the `generate.py` line, peak in range; item 7 again prints `48000 2 1`. (F3; path-conversion NFR)

**PM step:**
9. [ ] PM, main thread: `git checkout -- Tests~/Fixtures/audio/tts/cease_fire.wav`. (F3)

**Dispatch C - `compile-check`.** Precondition: the Unity Editor closed (`D:\Workspace\VoXR TestGround\Temp\UnityLockfile` absent); else stop and report.
10. [ ] `git status --short -- Tests~/Fixtures/audio/` prints nothing. (F3)
11. [ ] The F7 sweep, `architecture.md` :110 verbatim: no output (exit 1). A live-area hit outside `KNOWN_LIMITATIONS.md` is fixed in this phase; one in it is a stop. (F7; confirms F5, F6)
12. [ ] `git diff --stat main -- Runtime/ Samples~/ Tests~/Fixtures/audio/` prints nothing. (F9)
13. [ ] Commands 1-3 of `.claude/bindings/unity.md` `## verification`, verbatim; unset is `env -u MSYS_NO_PATHCONV bash -c '<command>'`, or for Command 3 (single quotes) `env -u MSYS_NO_PATHCONV bash <file>`, a `.scratch/` file holding its line verbatim. Order, each result read before the next run: (a) Commands 1, 2 set, then Command 2's revert; (b) the same unset, then the revert; (c) Command 3 set; (d) Command 3 unset. Before each Commands 1-2 run, delete `.scratch/test-results/editmode.xml` and `playmode.xml`: a stale green XML must not pass a crashed run (`unity.md` failure rule). Command 3 runs only with `Tests~` in place, after a revert. Same results both ways: Commands 1-2 `failed="0"`, `inconclusive="0"`, no `Tests` leftovers after each revert; Command 3 green, 16/16 on the committed corpus, no `.so` recommit (a dirtied `.so` goes to the PM's restore). (F9; path-conversion NFR)
14. [ ] Last: `git diff --stat main -- Runtime/ Samples~/ Tests~/Fixtures/audio/` again prints nothing, and `git status --short -- Tests~/ Runtime/` no leftovers. Either dirty: the PM restores it and this item re-runs; a subagent never restores. (F9)

## Open questions that block implementation

None. The stops above are the human's at run time.

## Blast radius

- Every later fixture regeneration runs this script, on either host; `VOXR_PYTHON` is a new input.
- Outside the repo: the cache `C:/Users/excal/.cache/voxr-fixture-gen` (venv, voice), created and kept.
- In the repo: `.scratch/` run files; `NativeBridge~/build/`, `build-windows/` rebuilt incrementally; `cease_fire.wav` regenerated, then restored.
- `architecture.md` :108 (restore owner) and :37 (`:22-39`; the calls end at :40) are reconcile items.
- At G2: deferral WSL-1 closes (adr-0001).

## Runtime claims

- `architecture.md` `## Risks, tradeoffs & open questions` bullet 1, Phase 2, every item; also `core.autocrlf` unread, and bash taking `C:/...` in `-x`, `-d`, `-e`, `mkdir -p` and as a command word.
- Item 2: a missing `VOXR_PYTHON` command fails the probe before anything is created, `>/dev/null 2>&1` silencing bash's own "command not found"; `$(pwd -W)` prints `D:/...`.
- Item 5: in the human's Git Bash (`MSYS_NO_PATHCONV=1`), `bash` is Git's, not a `System32` WSL launcher; `XDG_CACHE_HOME` is unset, `$HOME` is `C:/Users/excal`; `py -3` passes first, printing `Python 3.12.10`.
- Item 6: `generate.py`'s peak line is parseable (`peak 0.050`).
- Item 8: the rerun on the filled cache needs no network.
- Item 13: each batchmode run writes a fresh results XML; `env -u MSYS_NO_PATHCONV bash <file>` runs Command 3's single-quoted line unchanged from the project root; set and unset give the same results; Commands 1-2 leave no `.meta` orphans under F9's pathspecs; Command 3 does not dirty the committed `.so`.
