# Phase 7 — persisted plan (2026-10-17)

**Default flip and re-pin**, unsplit, serial, no units: F6 (Continuous default), F10. `Automatic` in Continuous resolves to `Responsive` (rule2 `0.45`, rule3/rule4 the archive's own; *Tuning record*). OV=`Runtime/EndpointerOverlay.cs`; ET=`Tests~/Editor/EndpointerOverlayTests.cs`; LT=`Tests~/Runtime/VoxrEndpointerProfileLoadTests.cs`; UB=`.claude/bindings/unity.md`; RM=`NativeBridge~/harness/README.md`; REC=`Planning~/verification-recipe.md`; `$S`=`.scratch/endpointer-profile-phase-7`.
**Out of scope:** Phase 8; product docs; reconcile items (B1-B4, stale cites).

**Departures from the Build-plan row and rulings** (the human, 2026-10-17):
- D1. `Resolve_AutoContinuous_ModelDefault`, `Load_Default_LogsLine` pin today's default and move with the flip as its tests; any other red under the flip is flip evidence: the human, never a test edit (Risks).
- D2. The re-pin runs in two halves around the human's review stop: a scratch re-pin and its diff; the standing pin only on approval.
- D3 (R2). Command 3's trigger widens: "only when `NativeBridge~/` or a source `confgen` links (`NativeBridge~/harness/confgen/confgen.csproj`) changed".
- D4. REC §1 2b, a third copy of the run line, changes with UB and RM.
- D5 (R1: the CHANGELOG line lands in Phase 7). The PM writes it (PM write scope; the row names `doc-writer`): `### Changed`, `[Unreleased]`, at most 200 characters.
- D6. Command 4 and DocCheck do not run: no public API or parser change.

## Files to change

| Path | Change | Why |
|---|---|---|
| OV; ET; LT | modify: Step 1 | F6, F12; D1 |
| `$S/review.py`, `$S/runline.py` | create, untracked: Step 1 | Steps 3, 6 |
| `NativeBridge~/harness/expectations.json` | Step 4, harness `--write-baseline` only | F10, F1 |
| UB; RM; REC | modify: Step 5 | F10; D3, D4 |
| `CHANGELOG.md` | modify: Step 7 | F10; D5 |

## Contracts and interfaces

**Dispatch.** `code-writer`: Step 1, probes. `compile-check`: Gate A, Step 4, Gate B. `doc-writer` (sonnet, dictated): Step 5. The PM: Steps 3, 7, VC state. Git Bash, project root, never WSL.

**Step 1.**
- OV `Resolve` (:96-115): :109 becomes `// Continuous: the tuned preset, Responsive, is the default (adr-0002).`; :111-112 `VoxrEndpointerProfile.Responsive,`, `Responsive,`; label (:113), Push-to-talk (:102-107) kept.
- ET :485 renamed `Resolve_AutoContinuous_Responsive`; for `Automatic` and `(VoxrEndpointerProfile)99`: Preset `Responsive`, Row `AreSame` `EndpointerOverlay.Responsive`, label `"Continuous default"`.
- LT `Load_Default_LogsLine` (:95) expects `[VoxrSpeechRecogniser] Endpointer profile Responsive (Continuous default): rule2=0.45 rule3=0.75 rule4=1.0`.
- LT, same test, :109-113: the conf's `File.ReadAllLines` equal the copy's but for the one starting `Rule2Key + "="`, reading `Rule2Key + "=0.45"`; message `The Continuous default is Responsive: the archive's conf, rule2 at 0.45.` TearDown (:64-67) kept.
- `$S/review.py <old> <new>`: same `file` list; per case `' '.join(' '.join(f).split())` of `expectedFinals` equal, else `words <file>`; `modelConf` lines equal but rule2, new `=0.45`, else `conf`; other keys equal, else `key <k>`. Lists differing, joined words equal: `moved <file>`, informational; no `expectedFinals`: `key <file>`. Last line `stop` (1) after a `words`, `conf` or `key` line, else `ok` (0).
- `$S/runline.py`: in UB, RM, REC (bytes, UTF-8) the text from `cd NativeBridge~ && ls -d` to the first backtick, CR or LF is equal, and in those and `CHANGELOG.md` every LF follows a CR: `same` (0), else the files (1).

**Step 5 text (`doc-writer`).** UB, RM, REC, `CHANGELOG.md` are CRLF; Steps 5, 7 keep it (c).
- UB Command 3 (:8): D3's trigger; its run line gains, before `./build-windows/`, `dotnet build harness/confgen/confgen.csproj --artifacts-path build-confgen && dotnet build-confgen/bin/confgen/debug/confgen.dll --model vendor/vosk-model-small-en-us-0.15 --out vendor/overlaid/continuous-default --profile Automatic --mode Continuous && `, and the harness's `--model` becomes `vendor/overlaid/continuous-default`. Green as written, its arm64 clause unchanged. Prerequisites (:12): Command 3's clause gains `, and the .NET SDK 10 with dotnet on PATH (confgen)` after its `vendor/` parenthetical.
- RM :35 and REC §1 2b (:21): the new run line verbatim. REC :19: D3's trigger.
- RM :11 (Toolchain) gains ".NET SDK 10 (`dotnet` on PATH) for `confgen`". "What each part does" (:38) gains, before :43: "`dotnet build` builds `confgen`; `confgen --profile Automatic --mode Continuous` writes the Continuous default's model copy to `vendor/overlaid/continuous-default`, which the harness decodes." Re-baselining (:62) gains the occasion "a change to the Continuous default's conf"; :65's `--model ...` becomes `--model vendor/overlaid/continuous-default`; :69 gains: `modelConf` changes in the rule lines alone; a moved `expectedFinals` keeps its joined words (segmentation only), otherwise the human rules.

**Gate rules** (in every brief): (a) Bash collapses `\\` to `\`: backslash-free probes, Python in `$S/*.py` via `python3 -I`; (b) `grep -c` for a trailing CR prints 0 on CRLF: count CR in Python; (c) `sed -i` strips CR (`sed -b -i` keeps it); (d) `compile-check` has no Write; (e) vc-guard refuses subagents VC-state changes: restore by probe, never `git checkout`; (f) commands run as written, no added pipe, redirect or capture file; (g) `--write-baseline` touches the standing pin only at Step 4, after approval; a Command 3 exit 1, a joined-words change, or a red but D1's two stops for the human, never a re-pin or test edit; (h) Unity-made `memory/handoffs/*.meta` commit with the phase.

`X` = `N=NativeBridge~; S=.scratch/endpointer-profile-phase-7; V=$N/vendor/vosk-model-small-en-us-0.15; O=$N/vendor/overlaid/continuous-default; D=$N/build-confgen/bin/confgen/debug/confgen.dll; K='C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe'; H="$N/build-windows/bin/vosk-bridge-harness.exe --model $O --fixtures Tests~/Fixtures/audio/tts --manifest"; e(){ echo "exit $?"; };`, one Bash call per check.

**Step 2 - Gate A.**
1. Commands 1-2: `failed="0"`, `inconclusive="0"`, EditMode 230, PlayMode 715 (Phase 6's); no `Tests` leftovers. Tier B under the tuned default.
2. `X dotnet build $N/harness/confgen/confgen.csproj --artifacts-path $N/build-confgen; e; ls $D; e`: `exit 0` twice.
3. `X dotnet $D --model $V --out $O --profile Automatic --mode Continuous; e; diff $V/conf/model.conf $O/conf/model.conf; e`: `exit 0`; `exit 1`, one hunk, rule2 `0.5` to `0.45` alone. The hunk proves `$D` fresh (stale: ModelDefault, no diff); `ls $D` shows a only.
4. `X (cd $N && "$K" --build --preset windows-x64); e`: `exit 0`.
5. `X $H $N/harness/expectations.json; e`: `exit 2`, naming `$O`'s conf (`main.cpp` :228-229).
6. `X cp $N/harness/expectations.json $S/pin.json; $H $S/pin.json --write-baseline; e; $H $S/pin.json; e`: `exit 0` twice, then 37/37.
7. `X git diff --no-index -- $N/harness/expectations.json $S/pin.json; e`: the diff, reported whole; `exit 1`.

**Step 3 - review (the PM, then the human; stop).** `X python3 -I $S/review.py $N/harness/expectations.json $S/pin.json; e`: `ok`. The PM presents 2.7's diff and this output. A `stop`: the human's ruling, never re-pinned. Step 4 only on approval.

**Step 4 - re-pin.** `X $H $N/harness/expectations.json --write-baseline; e; cmp $N/harness/expectations.json $S/pin.json; e`: `exit 0` twice.

**Step 6 - Gate B.**
1. UB's Command 3 whole: the arm64 build per REC "Building NativeBridge", then its run line: `libvosk-bridge.so` built with no errors; exit 0, 37/37.
2. `X cmp $N/harness/expectations.json $S/pin.json; e`: `exit 0` (as at Step 4).
3. `X python3 -I $S/runline.py; e`: `same`, `exit 0`.

**Step 7 (the PM).** Last `### Changed` bullet (after :26), then 6.3: `same`. Exactly (192 characters): Continuous mode ends commands sooner: `VoxrSpeechRecogniser`'s Endpointer Profile (`VoxrEndpointerProfile`) defaults to `Responsive` there (rule2 0.45 s); Push-to-talk keeps the model's own.

**Stop rules.**
- `$S` present before Step 1: BLOCKED, no `rm -rf`.
- D1's two tests red: `code-writer` (two passes, then the human); any other 2.1 red: the human.
- No `$D`, 2.3 beyond rule2, 2.5 not `exit 2`, 2.6 off, Step 4 or Gate B 1-2 red: the human. Gate B 3 red: `doc-writer` (two passes, then the human).

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| Gate B 2 `cmp` with `$S/pin.json` | `git diff --exit-code` | HEAD keeps the old pin until the PM commits |

## Acceptance criteria

1. [ ] 2.1 (F6).
2. [ ] 2.2-2.3 (F6, F3).
3. [ ] 2.4-2.7; Step 3 `ok`, approved (F10, F1).
4. [ ] Step 4: the standing pin by `--write-baseline` alone, equal to the reviewed one (F10, F1).
5. [ ] Gate B (F10; §9).
6. [ ] The CHANGELOG line (F10).

## Open questions that block implementation

None; a stop opens one.

## Blast radius

- Every `Automatic` Continuous load (`Samples~`, Tier B) decodes at rule2 `0.45`; `confgen --profile Automatic` and `stage.sh` stages follow OV; Command 3 needs .NET SDK 10.
- Not edited: REC 2b prose, `Documentation~/native-bridge.md` :61; `Planning~/features/windows-native-harness/architecture.md` :97/:102, intentionally left: a closed feature's record.
- G2 still requires Phase 8; its fail reverts Phase 7 whole.

## Runtime claims

- a (2.2): the rebuilt dll lands at `$D`.
- b (2.5): the standing pin exits 2 on the overlaid model before the re-pin.
- c (2.6, 4): `--write-baseline` is deterministic: Step 4's pin is byte-equal to 2.6's.
- d (2.1): the host's StreamingAssets conf holds rule3 `0.75` / rule4 `1.0`, as the vendor conf.
- e (2.1): Tier B's replay loads `Automatic`, no controller: Continuous, so `Responsive`.
- f (6.1): `confgen` on an existing `--out` re-applies the row from the archive copy; Command 3 repeats.
- g (6.1): `confgen` creates an absent, ignored `vendor/overlaid/` (fresh checkout).
- h (2.7): vc-guard lets `compile-check` run `git diff --no-index`, `cp`, `cmp`, `diff`.
- i (2.6): the first `--write-baseline` exits 0, no `run_error`: written.
- j (6.1): Command 3's `dotnet build --artifacts-path build-confgen` exits 0 when up to date.
