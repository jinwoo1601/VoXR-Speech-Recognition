# Phase 1 — persisted plan (2026-11-22)

Scope: the row as quoted, after the PM's provisioning and Command 3 as the build; it takes on a `--self-check --live` hook for F9's live toy, and runs the two Kokoro voices, that toy and the environment listing only if the maintainer has provisioned Kokoro first, else they are Phase 5's.

## Files to change

`F` is `Tests~/Fixtures`.

| Path | Action | Why |
|---|---|---|
| `F/generate.py` | modify | *Render home* |
| `F/voices.py` | create | F1, F2, F9, F10; register *Render home* to *The voiced set* |
| `F/voices.sh` | create | *Venv and model*, *Stand-in engine* |

## Contracts and interfaces

- **generate.py.** `build_case(case, speak)`, `speak(text)` → float32 at `TARGET_RATE`: its two `synth(…)` calls become `speak(<text>)`; `main`, after `tmp_path`, binds `speak = lambda text: synth(args.piper_bin, args.voice, Path(args.data_dir), text, tmp_path)` and its one call (:252) becomes `build_case(case, speak)`; nothing else moves. EOL kept.
- **voices.py** (header as `generate.py`'s). Puts its own directory first on `sys.path`, then `import generate`; Kokoro imported in its paths only. Never calls `main` or `write_truth`; reads the two manifests beside it.
  - CLI: `--self-check [--live]`, or `--engine kokoro|piper --root <dir> [--voices all|<id>,…]` (default `all`) with `--model-dir` or `--piper-bin`, `--data-dir`. Exit 0; 1 a toy FAIL or render refusal (token rule, chunks, `TruthStop`, `sys.exit`); 2 before synthesis: usage, `voices: unknown voice "<x>": valid: <ids>`, `voices: voice "<x>" named twice`, a missing file, the environment. `IDS`: F1's order, the run order.
  - Set: *The voiced set*'s predicate, then all negatives; stem = `file`'s basename less `.wav`.
  - Kokoro: `KModel(config=…/config.json, model=…/kokoro-v1_0.pth)`; per language (`a` for `af_`/`am_`, else `b`) `misaki.en.G2P(trf=False, british=…, fallback=None)` and a `KPipeline` on that model; pack `voices/<id>.pt`; over one result → `voices: <id>: "<text>": split into <k> chunks`; resampled per *24 → 48 kHz*. Piper: `generate.synth`, `en_US-lessac-medium`, temp WAV in a `TemporaryDirectory`.
  - `unspoken(tokens)` (pure): `(text, phonemes)` pairs → texts with a letter or digit whose phonemes are `None`, `""` or hold `❓`; all set texts before synthesis, `voices: <lang>: "<text>": no phonemes for: <words>` each. `gpl_dists(names)` (pure): names whose lower case holds `espeak` or `phonemizer`, first, over `importlib.metadata.distributions()`, `voices: GPL fallback installed: <list>`. Both Kokoro only.
  - Per voice: into `<root>/voices/.<id>.partial/`, emptied; per case `build_case` → `write_wav` `<stem>.wav` → `speech_truth(dict(case, file="<stem>.wav"), <partial>)`; `voice.json`; then it replaces `voices/<id>/`. A refusal removes the partial and ends the run; later voices are not rendered. Last line `voices: <id>: <len(clips)> clips, render <render id>`.
  - `voice.json` (`indent=2`, `ensure_ascii=False`, LF), *Cache*'s fields as `id`, `licence`, `licenceSource`, `engine`, `kokoroVersion`, `misakiVersion`, `modelSha256`, `voicePackSha256`, `clips`; Kokoro: `kokoro`, `Apache-2.0`, `huggingface.co/hexgrad/Kokoro-82M (packs by inference)`, the `.pth` and `.pt` digests; stand-in: *Stand-in engine*'s engine and licence, `en_US-lessac-medium (Piper)`, the `.onnx` digest, the rest null. Clip: `file` (source), `phrase` or `segments`, `gapSeconds`, `peak` as the manifest holds them, `speech` in the manifest's shape and unit, read alike by Phase 2: `{"start": s, "end": e}` per `speech_truth` span, 48 kHz sample indices, `e` exclusive; `realizedPeak` max|pcm|/32767 to 6 places, `sha256` of the file. Render id per *Determinism*, digests joined by `\n`, unstored.
  - Self-check: `PASS <n> <name>` / `FAIL <n> <name>: got <g> want <w>`, last `<p>/<n> voices checks verified against known-value toys.`. 1–5 `unspoken`: `("open","ˈOpən")` → `[]`; `("zorblax", p)` for `p` `None`, `""`, `"❓"` → `["zorblax"]`; `(",", None)` → `[]`. 6–8 `gpl_dists`: `["numpy","espeakng-loader","phonemizer-fork"]` → the last two; `["misaki"]` → `[]`; `["EspeakNG"]` → itself. 9 a speaker of 4800 samples at 0.5, `{"file":"toy.wav","phrase":"toy","peak":0.2}`, `build_case`, `write_wav`, `speech_truth` → exactly `[(L, L+4800)]`, `L = int(LEAD_SILENCE_S*TARGET_RATE)`. 10 as 9, `segments ["a","b"]`, `gapSeconds` 0.7 → `[(L, L+4800), (L+4800+G, L+9600+G)]`, `G = len(silence(0.7))`. 11 the set: 15, 16, 31 distinct stems. 12 `all` → `IDS`; 13 `bf_emma,af_heart` → F1 order; 14 a repeat, 15 `xx` → their texts. `--live` 16–17: each G2P on `open zorblax fire` → `["zorblax"]`.
- **voices.sh** (header as `generate.sh`'s; LF). `--stand-in`, `--self-check`, `--live`, `--root`, `--voices`; else exit 2. `--root` made absolute (`cygpath -am`, else `realpath -m`) before the `cd`; default `${XDG_CACHE_HOME:-$HOME/.cache}/voxr-bench`, Kokoro only. `--self-check`, `--stand-in`: `generate.sh`'s venv and voice, else exit 2 `run generate.sh first`. Kokoro, `--live`: *Venv and model*; no venv → its Python rule (exit 2), `pip install --quiet "kokoro==0.9.4" "soxr==1.1.0" numpy`; files absent → `hf_hub_download` from `hexgrad/Kokoro-82M`; `voices.py` under `HF_HUB_OFFLINE=1`.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| truth on `dict(case, file=…)` | `write_truth` on a copy | flag 2; no manifest written |
| partial dir renamed in | write in place | a refusal keeps the prior render |
| checks Kokoro-only | also the stand-in | F9 is Kokoro's G2P |
| `--stand-in` needs `--root` | cache default | *Stand-in engine*: `.scratch/` roots |

## Acceptance criteria

- [ ] F3 baselines from the unmodified tree (V1).
- [ ] F2 truth exact, F9 rule, F1 set (V3).
- [ ] Manifests unmoved; `main` renders (V4, V5).
- [ ] F1 layout and record, F2, F10: 12 records of 31 (V6, V7).
- [ ] Commands 1–2 (V8); nothing new outside `.scratch/` (V9).

**Steps.** P0, the PM's: `NativeBridge~/harness/README.md` *Provisioning a branch tree*, then Command 3: 52/52. One Git Bash at the tree root: `R="Planning~/features/coverage-in-selection/ab-rig" F="Tests~/Fixtures" B=.scratch/bench-voices; T=$B/p1 S="$(pwd -W)/$B/stand-in" C="$(cygpath -m "${XDG_CACHE_HOME:-$HOME/.cache}")/voxr-fixture-gen"; V=$C/venv/Scripts; mkdir -p $T; ls "$V"/{python,piper}.exe "$C/voices/en_US-lessac-medium.onnx" >/dev/null && echo toolchain`: `toolchain`; serial, foreground.

- V1, before any edit: `test -z "$(git status --porcelain -- "Tests~" "$R" Runtime)" && test ! -e $B/baseline-fast.md && echo fresh && bash "$R/bench.sh" --out $T/f >/dev/null && cp $T/f/report.md $B/baseline-fast.md && bash "$R/bench.sh" --condition grid --seed 7 --out $T/g >/dev/null && cp $T/g/report.md $B/baseline-grid.md && echo kept`: `fresh`, `kept`.
- V2: edit the three files, as one change.
- V3: `bash "$F/voices.sh" --self-check > $T/sc.txt; echo "exit=$?"; tail -n 1 $T/sc.txt`: `exit=0`, `15/15 …`.
- V4: `for m in manifest.json negatives/manifest.json; do py -B "$F/generate.py" --truth --manifest "$F/audio/$m" --fixtures-root "$F"; echo "exit=$?"; done; git diff --exit-code --stat -- "$F/audio"; echo "diff=$?"`: `truth: 52 case(s) written …`, `truth: 16 …`, `exit=0` each, `diff=0`.
- V5, `main` off the committed files: `cp -r "$F/audio" $T && "$V/python.exe" "$F/generate.py" --manifest $T/audio/manifest.json --fixtures-root $T --data-dir "$C/voices" --piper-bin "$V/piper.exe" --only tts/split_close; echo "exit=$?"`: `1 fixture(s) written …`, `truth: 52 case(s) written …`, `exit=0`.
- V6, one call per group (timeout 600000): `for v in af_heart,af_bella,af_nicole,af_sarah bf_emma,bf_isabella,am_michael,am_fenrir am_puck,bm_george,bm_fable,bm_lewis; do bash "$F/voices.sh" --stand-in --root "$S" --voices $v > $T/s.txt; echo "exit=$?"; grep -c ': 31 clips, render ' $T/s.txt; done`: `exit=0`, `4`.
- V7: `py -I -B $T/v7.py "$S" "$F" stand-in "stand-in, not a Kokoro voice"`: `12 12 372 372 0 31 0`; `$T/v7.py` prints counts of: `<root>/voices` entries; records (not dot-led); their clips; their WAVs; clips whose `speech` length ≠ `len(segments)` (else 1), WAV sha256 ≠ `sha256` or WAV not 48 kHz mono 16-bit; *The voiced set*'s stems from `F`'s manifests; records whose `id`, `engine`, `licence` ≠ (its name, argument 3, argument 4) or clip stems ≠ that set.
- V8: `py "Planning~/host-run/host_run.py" EditMode PlayMode`, foreground, timeout 600000: green per the binding.
- V9, after the PM commits `plan-1.md` and the link line: `git status --short; git diff --numstat -- "$F/generate.py"; py -c "import sys;print([open(p,'rb').read().count(13) for p in sys.argv[1:]])" "$F/voices.py" "$F/voices.sh"`: ` M` `generate.py`, `??` `voices.py`, `voices.sh` alone; both numstat counts under 10; `[0, 0]`.
- K, the PM's, if Kokoro is provisioned: `bash "$F/voices.sh" --voices af_heart,bm_george --root "$(pwd -W)/$B/kokoro"`, V7 there, `kokoro Apache-2.0` last (`2 2 62 62 0 31 0`); `--self-check --live` (`17/17`); the venv's `pip list`, no `espeak`.

**Stops:** P0 without `toolchain` (fetching is the maintainer's `generate.sh`); V1 without `fresh`; any red, FAIL or missing line; V4 `diff=1` (the restore is the PM's git write); never a loosened check.

## Open questions that block implementation

None.

## Blast radius

`generate.sh`; the manifests (V4); Unity's import of `F` (V8); Phase 2's `BenchVoices` (set, `voice.json`, render id); the baselines Phases 2–5 `cmp`; the hub's `NativeBridge~/vendor/`; Command 3's build dirs.

## Runtime claims

- `build_case` drives Piper as before through `main` (V5) and `voices.py` (V6); `--truth` keeps both manifests' bytes (V4).
- Every rendered clip passes `speech_truth`, the split gap included (V5–V7); `voices.py` imports `generate` (V3); `bench.sh` green unmodified, the grid without `~/.cache/voxr-bench` (V1).
- Kokoro's API, tokens, chunks, offline load, an espeak-free install: unrun until K or Phase 5.

Hazards: P0, V1 before V2; `bench.sh`, the harness, `host_run.py`, `voices.sh`, V5 never concurrent; agents run `voices.sh` only as `--stand-in` or bare `--self-check` (else it downloads); V4 writes committed files.
