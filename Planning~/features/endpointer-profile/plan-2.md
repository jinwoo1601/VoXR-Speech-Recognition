# Phase 2 — persisted plan (2026-10-16)

**Phase 2 - Conf record**, unsplit, serial, no units: F2; F3's record half; register: *Conf record*, *The rig*. MAIN=`NativeBridge~/harness/main.cpp`; PIN=`NativeBridge~/harness/expectations.json`; RIG=`Planning~/features/coverage-in-selection/ab-rig/`; CONF=`<model>/conf/model.conf`; live lines.
**Out of scope:** `confgen`, overlay, presets, `stage.sh`, `joined.sh` flags (Phases 3-8); README run line, re-baselining (Phase 7); PIN `_comment`; Commands 1-2; product docs; CHANGELOG; `NativeBridge~/src/`.

## Files to change

| Path | Change | Why |
|---|---|---|
| MAIN | modify: Step 1 | F2; *Conf record* |
| PIN | modify: + `modelConf` (Step 3) | F2; F1 |
| RIG `JoinedInput.cs`, `Joined.cs`, `JoinedReport.cs` | modify: Step 2 | F2; *The rig* |
| `NativeBridge~/harness/README.md` | modify: Step 6 | *Flip and re-pin* |
| `$S` | create, untracked | Step 4 |
| `plan-2.md`; `architecture.md` | create; link line | this plan |

## Contracts and interfaces

**Dispatch.** A, `code-writer`: Steps 1-4 in order, one tree; a stop ends A BLOCKED. Its brief grants per dispatch each command Steps 3-4 name (git reads only), Step 4 writing only under `$S`. B, `compile-check`: Step 5; C, `doc-writer`: Step 6; each once A ends unstopped. No git writes; Git Bash, never WSL; text reports.

**One change.** Step 3's PIN edit lands with Step 2's `PinKeys` edit: `ReadPin` (`JoinedInput.cs` :584-592) stops on an unknown key today, a missing one after. **On a stop** (Step 3) PIN stays uncommitted; the PM restores it (`git checkout --`) after the human's ruling, leaving harness and rig at exit 2 (no `modelConf`).

**Shapes.** Dump header: `modelConf` after `model`, CONF's exact text or `null`; `schema` stays 2. PIN: top-level `modelConf`, same text, written last.

**Harness messages**, via `Fail` (:161-164, exit 2); `<conf>`=`model_dir + "/conf/model.conf"`, `<manifest>`=`manifest_path`:
- M1: `manifest 'modelConf' pin missing or not a string (record <conf> with --write-baseline): <manifest>`
- M2: `model conf <conf> is absent or differs from the manifest 'modelConf' pin: <manifest>`
- M3: `cannot pin 'modelConf': model conf <conf> is absent (the pin has no null form): <manifest>`

**Rig.** Kill-check line: ``kill check: header `modelConf`: expected sha256 <12 hex>, actual sha256 <12 hex>``, actual `not recorded` if the dump has none. Report, after `- Kill check:` (`JoinedReport.cs` :603-607): `- Model conf A: sha256 <12 hex>; <rule lines>`, then `- Model conf B: ...`, or `- Model conf A: not recorded`. Hash: internal `JoinedReport.ConfSha12(string)`, SHA-256 of the UTF-8 bytes, first 12 lowercase hex; the kill check calls it.

**Step 1 - harness (MAIN).** After the CLI parse (:172-182), read CONF `std::ios::binary` into an optional string (unopenable = absent). In validation, after `libvosk` (:208-210), before the drift guard (:224-243): without `--write-baseline`, pin missing or not a string -> M1, CONF absent or != pin -> M2; with it, CONF absent -> M3. Header: `header["modelConf"]` after :263, text or `nullptr`. Under `write_baseline`, `manifest["modelConf"] = conf` after the case loop (:358), before the write (:362). Nothing else changes.

**Step 2 - rig.** `JoinedInput.cs`: `PinKeys` (:151) gains `"modelConf"`, required; `Pin` (:115-127) gains `ModelConf`, read in `ReadPin` (:594-600) by `Str` (its Stop 2). `Dump` (:86-96) gains `ModelConf`, read at :205-212: string -> text; null or absent -> null (not recorded); another kind -> Stop 2 in `Field`'s form (:744). `Joined.cs` kill check (:214-266), pinned, after `libvosk` (:225-232): `dump.ModelConf` != `pin.ModelConf` (ordinal, null included) adds the line, thrown with the rest (:264-265). `JoinedReport.cs`: each line from its scored file's `dumpHeader.modelConf` (read as :576-577); rule lines = `\n`-split lines whose trimmed text begins `--endpoint.rule`, in order, `; `-joined; none -> the hash alone. `HeaderKeys`, `RecogniserHeaderKeys` (:34, :67), the differs loop (:575-601) unchanged.

**Step 3 - pin write.** From `NativeBridge~/`: Command 3's desktop configure and build lines, then its harness line plus `--write-baseline`, then that line as is: exit 0, 37/37. Check: `git diff HEAD -- harness/expectations.json` adds `modelConf` alone (`libvosk` gains its comma); no `expectedFinals` moved; CRLF throughout (`grep -U -c $'\r$'` equals `wc -l`). **Stop:** any other hunk, a moved final or a red re-run -> PIN left as the harness wrote it, BLOCKED for the human's ruling; never a re-pin or a restore.

**Step 4 - green probes**, in order, one Git Bash invocation from the project root, echoing each exit; no `\\` (the Bash tool collapses it). Set `S=.scratch/endpointer-profile-phase-2 HX=NativeBridge~/build-windows/bin/vosk-bridge-harness.exe V=NativeBridge~/vendor/vosk-model-small-en-us-0.15 F=Tests~/Fixtures/audio/tts P=NativeBridge~/harness/expectations.json J=Planning~/features/coverage-in-selection/ab-rig/joined.sh`. `$S` present: BLOCKED before 4a (a leftover `model-r2` nests the copy; no `rm -rf`). A probe off its check stops Step 4 there, BLOCKED with its output; no later probe runs.
- a. `"$HX" --model "$V" --fixtures "$F" --manifest "$P" --dump $S/a.jsonl`: exit 0. `head -n 1 $S/a.jsonl | grep -c '"model":"[^"]*","modelConf":'` prints 1; `python3 -c "import json;print(json.loads(open('$S/a.jsonl','rb').readline())['modelConf']==open('$V/conf/model.conf','rb').read().decode())"` prints `True`.
- b. `cp -r "$V" $S/model-r2 && sed -i 's/^--endpoint.rule2.min-trailing-silence=0.5$/--endpoint.rule2.min-trailing-silence=0.35/' $S/model-r2/conf/model.conf`; `"$HX" --model $S/model-r2 --fixtures "$F" --manifest "$P"`: exit 2, stderr names `$S/model-r2/conf/model.conf`.
- c. `cp "$P" $S/pin-r2.json`; `"$HX" --model $S/model-r2 --fixtures "$F" --manifest $S/pin-r2.json --write-baseline --dump $S/b.jsonl`: exit 0 (scratch pin only).
- d. `bash "$J" --vary decoder --dump-a $S/a.jsonl --dump-b $S/b.jsonl --ref WORKTREE --out $S/joined` (standing pin, parser layer): exit 0; `$S/joined/report.md` has `- Kill check: A passed, B not checked`, its two `- Model conf` lines differing in hash and rule2 line.
- e. `bash "$J" --vary decoder --dump-a $S/b.jsonl --dump-b $S/a.jsonl --ref WORKTREE --out $S/joined-k`: exit 3, with Rig's `modelConf` kill-check line (finals lines may join it).
- f. `git show "HEAD:$P" > $S/pin-pre.json` (no `modelConf`); `"$HX" --model "$V" --fixtures "$F" --manifest $S/pin-pre.json`: exit 2, stderr M1's text.
- Unprobed: M3 by choice (reachable: pre-init, no loadable model); `null` header, unreachable (absent CONF exits 2 first); `not recorded`, no pre-Phase-2 dump.

**Step 5 - verification.** Command 3 in full (arm64 build, then the harness): exit 0, 37/37. DocCheck (`Planning~/verification-recipe.md` :222-232, command :227): exit 0, no `FAIL`, `<n>/<n>`.

**Step 6 - README.** Guards (:49-53) gain a bullet: the `modelConf` pin, the exact text of `<model>/conf/model.conf`, is required and must equal the decoded model's conf, checked before any decode (absent or different exits 2 naming the conf); only `--write-baseline` writes it. Nothing else.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| `modelConf` required in `PinKeys` | optional | PIN carries it from Step 3 |
| CONF read binary | text mode | the pin compares exact bytes |
| Check before the drift guard | after init | decode only at the pinned conf |
| Other-kind dump `modelConf`: Stop 2 | not recorded | input defects are named stops (`JoinedInput.cs` :7-9) |
| Hash from `dumpHeader`, one helper | a scored key | `dumpHeader` has it (`Joined.cs` :307-311) |

## Acceptance criteria

1. [ ] Step 3: check and re-run as Step 3 states. (F1; F2)
2. [ ] Command 3: `libvosk-bridge.so` builds; harness exit 0, 37/37. (§9)
3. [ ] Step 4 a-f: each exit, line and comparison as Step 4 states. (F2; F3 record half)
4. [ ] DocCheck green. (Build plan)
5. [ ] README guards carry Step 6's bullet, nothing else changed. (*Flip and re-pin*)

## Open questions that block implementation

None; a stop opens one.

## Blast radius

- Every pin the rig reads needs `modelConf` (older refs stop); pre-Phase-2 dumps fail a pinned kill check.
- Command 3 refuses a conf other than PIN's (Phase 7 re-pins); Phase 4 hashes the dump's `modelConf`.
- Scored `dumpHeader`s and the differs list carry the full conf.
- `joined-rig.md` rule 2 names finals only; `joined-rig-recogniser`'s kill-check cite (:143-195) is stale: reconcile's.

## Runtime claims

- a (1, 3): nlohmann-escaped CONF text reads back byte-equal; text-mode write (:363) keeps PIN CRLF.
- b (1, 3): `ordered_json` puts `modelConf` after `model`; sorted `json`, PIN's last.
- c (3): adding `modelConf` moves no `expectedFinals`.
- d (2): `System.Text.Json` reads dump's and PIN's `modelConf` as one string; its UTF-8 SHA-256 is the file's (Phase 4 relies on it).
- e (4d): `stage.sh` stages the working tree's rig for any ref (:73-76).
- f (4c): vosk initialises on the edited copy.
- g (4b): `sed -i` changes the rule2 line alone; LF kept.
- h (4d): no b.jsonl case is a failed decode (unpinned stop, `Joined.cs` :267-282).
- i (4d, 5): `System.Security.Cryptography` resolves in `Rig.csproj` (net10.0).
- j (4a): CONF has no `"` or `\` and ends in LF: the check is exact.
- k (4d): the scored `dumpHeader` round trip (`Joined.cs` :307-311, `UnsafeRelaxedJsonEscaping`) keeps the conf exact: report hash = kill-check hash.
- l (4d): `ReadManifest` accepts the 37-case manifest (Phase 1's `ladder_*`), first `joined.sh` run on it.
- m (4d): the vendor dump's finals are PIN's, all 37 (determinism): kill check passes.
- o (3): the diff is `modelConf` and the `libvosk` comma only, CRLF tree unchanged under the user's `core.autocrlf` (no `.gitattributes`).
- r (4b): M2 fires before `vosk_bridge_init` and the dump's open.
- s (4a): the backslash-free check reaches bash as written; its JSON parse un-escapes nlohmann's `\n`: exact.
- t (4f): `git show "HEAD:$P"` takes `~` literally after `:`, writes the raw blob.
- u (4): one invocation (two 37-case `--dump` runs, two cold `stage.sh` + `dotnet build`) fits the Bash call's time cap.
