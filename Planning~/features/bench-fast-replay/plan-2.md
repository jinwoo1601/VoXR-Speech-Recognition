# Phase 2 — persisted plan (2026-11-08)

Scope: `generate.py` truth mode (stock `py`; wired into regeneration, not run), one `speech` line per manifest case and nothing else, `JoinedInput` reading `speech`, `phrase`, `segments`, and `--bench truth-check`, taking on the header lines named below; nothing of Phases 3-4.

## Files to change

`R` is `Planning~/features/coverage-in-selection/ab-rig`; `F` is `Tests~/Fixtures`.

| Path | Action | Why |
|---|---|---|
| `R/JoinedInput.cs` | modify | *Truth record*: reads `speech`, `phrase`, `segments` |
| `R/Bench.cs` | modify | *Truth check*; header :2 |
| `F/generate.py` | modify | *Truth derivation*; header :3, :5, :6 |
| `F/audio/manifest.json` | modify | *Truth record*; written by V2 only |

## Contracts and interfaces

- **Derived.** Floor: −40 dBFS (`TRIM_DBFS`) is 10^(−40/20) = 0.01 of full scale; `write_wav` writes `round(x * 32767)`, so the floor is 0.01 × 32767 = 327.67 int16 units and a written sample is at or above it iff |s| ≥ 328 (the −60 dBFS noise is 32.767 RMS). Tolerance: 20 ms × 48000 Hz = 960 samples (design :193). Tail: `int(1.0 * 48000)` = 48000.
- **generate.py.** Imports gain `math`, `struct`; `numpy`/`soxr` move into `try:` … `except ImportError: np = soxr = None`. After `TRIM_DBFS`: `TRUTH_FLOOR = math.ceil(32767 * 10 ** (TRIM_DBFS / 20))`. New `--truth` (`store_true`); `--data-dir` loses `required=True`. `main`: `--truth` runs `write_truth(Path(args.manifest), Path(args.fixtures_root))` and returns, other options ignored; else no `--data-dir` is `ap.error("the following arguments are required: --data-dir")` and `np is None` is `sys.exit("regeneration needs numpy and soxr: run generate.sh")`; the loop is unchanged, and after its last print `write_truth` runs on the same paths.
- **speech_truth(case, root)**, raising `TruthStop(reason)`. n = `len(segments)` if `case.get("segments")`, else 1 if `case.get("phrase")`, else 0 (`build_case`'s order); 0 gives `[]` unread. `read_pcm`: `wave` `"rb"`, 1 channel, width 2, `TARGET_RATE` (else `not 16-bit mono 48000 Hz`), `struct.unpack("<%dh" % n, frames)`. lead `int(case.get("leadSilenceSeconds", LEAD_SILENCE_S) * TARGET_RATE)`, tail `int(TAIL_SILENCE_S * TARGET_RATE)`, gap g `int(case.get("gapSeconds", DEFAULT_GAP_S) * TARGET_RATE)`, `silence()`'s expression; block [lead, len − tail). Stops: `lead <a> + tail <b> leaves no block in <len> samples`; `a sample at or above the floor 328 in the lead at <i>`; `no sample at or above the floor 328 in the block`. A = the block's indices with |s| ≥ 328, clip-absolute (the block is a range of clip indices), so spans are too; for n > 1 the split runs are the gaps q − p − 1 ≥ g between consecutive p < q in A, and not exactly n − 1 stops `<n> segments need exactly <n-1> sub-floor runs of at least <g> samples; found <k>`. Spans: start A[0]; a run p→q ends one at p + 1, starts the next at q; last end A[-1] + 1.
- **write_truth.** Manifest bytes, UTF-8; EOL `"\r\n"` if present, else `"\n"`; `json.loads`; every case computed. Refusals: each `truth: <file>: <reason>` to stderr, then `sys.exit("truth: <k> case(s) refused; <manifest> not written")` (exit 1). Else split on EOL; drop lines starting 12 spaces + `"speech": `; each line starting 12 spaces + `"file": ` must equal that + `json.dumps(file)` + `,` for the next case, all met (else `truth: <manifest>: file lines do not match the cases; not written`), and is followed by 12 spaces + `"speech": [` + `{ "start": s, "end": e }` joined `, ` + `],`; join on EOL, `write_bytes`, print `truth: <n> case(s) written to <manifest>`. A re-run replaces in place.
- **JoinedInput.** `ManifestCase` gains `internal string Phrase; internal string[] Segments; internal (long Start, long End)[] Speech;` (null when absent). `ReadManifest`'s key loop parses `speech` before the allow-list test, then `continue`: an array of objects of exactly `start`, `end`, each `TryGetInt64`, 0 ≤ start < end, start ≥ the previous end, else `named + ": key `speech` is not an array of ascending {start, end} integer pairs (entry <i>)"`. The else-if chain gains `phrase` (string, else `key `phrase` is not a string`) and `segments` (string array, else `key `segments` is not an array of strings`). `ManifestCaseKeys` unchanged.
- **Bench.** `Run`: `truth-check` takes exactly `--bench truth-check --manifest <file> --fixtures-root <dir>` (else `usage: --bench truth-check --manifest <manifest.json> --fixtures-root <dir>`) and returns `TruthCheck(manifest, root)`; the stub becomes `verb + " is not built yet (bench-fast-replay Phase 3)"`. `TruthCheck`: `JoinedInput.ReadManifest`; each null `Speech` a line `<manifest>: case "<file>": no key `speech``, all in one `Stop(2, …)` before any WAV. `ReadPcm(Path.Combine(root, File))`: `File.ReadAllBytes`, RIFF/WAVE chunk walk (odd sizes padded), `fmt ` PCM 1, mono, 48000, 16-bit, `data` by `BinaryPrimitives.ReadInt16LittleEndian`, else `Stop(2, "<path>: not a 16-bit mono 48000 Hz PCM WAV")`. `TruthFloor = 328`, `FrameTolerance = 960`, `TailSamples = 48000`; n as the generator's. n > 0: l = 1 + the last index < samples − 48000 with |s| ≥ 328, d = last `End` − l; row `ok  `/`FAIL`, `{Name,-68}`, `segments=<k>/<n> end=<e|none> last=<l|none> diff=<d|none>`, ok iff k = n, every `End` ≤ samples, l found, |d| ≤ 960. n 0: `segments=<k>/0 floor-samples=<c>`, ok iff k = c = 0. All ok: blank line, `<n>/<n> clips' speech truth verified against the committed WAVs.`, 0; else `Stop(2, "truth check failed on <f>/<n> clips: <names, comma-separated>")`.
- **Comments (rule 1).** `generate.py`: `main`'s :143 comment goes; Purpose :3 ends `, and write its speech-boundary truth (--truth)`; Owns :5 reads `# Owns:     TruthStop (exception)`; Depends :6 reads `piper-tts, soxr, numpy (venv-installed by generate.sh) to regenerate; --truth the standard library only`; :8-14 stay. `JoinedInput.cs`: `ManifestCase` and `ReadManifest` hold no comment; header unchanged (no new type). `Bench.cs`: `Run` and the class body hold none; Purpose :2 ends `, the speech-truth check`. New code: no comment.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| Guarded import | imports in functions; `global` | no definition edited; venv regeneration unchanged |
| One line after `"file"` | `json.dump`; before `description` | no reflow; the first key anchors a re-run |
| `speech` before the allow-list | `ManifestCaseKeys` gains it | that edit is in `JoinedInput`'s class body; rule 1 would strip its members' comments |
| n-th run shorter than g | than the (n−1)-th | :118 stops a pause as long as the gap |
| Absent `speech` null, stopped in Bench | the reader requires it | :57, :90; joined inputs unchanged |
| WAV reader in `Bench.cs` | `VoxrWavReader`; a new file | unstaged; `stage.sh` in no Phase 2 row |
| All refusals listed | the first | one ruling covers them |

## Acceptance criteria

- [ ] F1, §10.1: stock `py` imports the generator, floor 328 (V1); all 52 written (V2), idempotent (V3).
- [ ] §4.1: 52 `speech` lines added, 0 removed, CRLF kept, parsed equal to `main`'s minus `speech` (V4-V6).
- [ ] F1, R1: the C# rescan green on 52, each `diff=0` (V9); 960 holds, 961 and a missing key stop by name (V11).
- [ ] *Code home*: the parser stage stops `truth-check`, `run` (V8); `metrics-check`, `run`, bare `--bench` unchanged (V10).
- [ ] Preamble: DocCheck (V7); joined `main`-versus-`main` 0 flips, both layers (V12); Commands 1-2 (V13).
- [ ] F13: V14.

**Steps:** `JoinedInput.cs`, `Bench.cs`, `generate.py`; then V1-V14 in one Git Bash at the tree root after `R=Planning~/features/coverage-in-selection/ab-rig F=Tests~/Fixtures T=.scratch/truth; mkdir -p $T; M=$(cygpath -am "$F/audio/manifest.json") X=$(cygpath -am "$F")`.

- V1: `py -I -B -c "import sys; sys.path.insert(0, sys.argv[1]); import generate; print(generate.TRUTH_FLOOR)" "$F"` - green: `328`.
- V2: `py "$F/generate.py" --truth --manifest "$F/audio/manifest.json" --fixtures-root "$F"; echo "exit=$?"` - green: `truth: 52 case(s) written to …`, `exit=0`.
- V3: `cp "$F/audio/manifest.json" $T/m1.json && py "$F/generate.py" --truth --manifest "$F/audio/manifest.json" --fixtures-root "$F" >/dev/null && cmp $T/m1.json "$F/audio/manifest.json"; echo "exit=$?"` - green: `exit=0` alone.
- V4: `git diff --numstat main -- "$F/audio/manifest.json"; git diff -U0 main -- "$F/audio/manifest.json" | grep -E '^[+-]' | grep -vE '^(\+\+\+|---) ' | grep -vxE '\+ {12}"speech": \[(\{ "start": [0-9]+, "end": [0-9]+ \}(, \{ "start": [0-9]+, "end": [0-9]+ \})*)?\],'; echo "exit=$?"` - green: `52	0	Tests~/Fixtures/audio/manifest.json`, `exit=1` alone.
- V5: `py -I -c "import sys; d=open(sys.argv[1],'rb').read(); print(d.count(b'\r\n'), d.count(b'\n'))" "$F/audio/manifest.json"` - green: two equal numbers.
- V6: `git show 'main:Tests~/Fixtures/audio/manifest.json' > $T/main.json && python3 -I -c "import json,sys; a,b=(json.load(open(p,encoding='utf-8')) for p in sys.argv[1:]); [c.pop('speech') for c in b['cases']]; print('equal' if a==b else 'differ')" $T/main.json "$F/audio/manifest.json"` - green: `equal`.
- V7: recipe :229 verbatim - green per :232.
- V8: `for v in truth-check run; do (cd .scratch/doccheck && dotnet run --no-build -- --bench "$v"); echo "$v exit=$?"; done` - green: each `bench: <verb> needs a recogniser stage: …`, `exit=2`.
- V9: `env -u MSYS_NO_PATHCONV bash "$R/stage.sh" WORKTREE .scratch/bench-stage recogniser && (cd .scratch/bench-stage && dotnet build -v q --nologo && dotnet run --no-build -- --bench truth-check --manifest "$M" --fixtures-root "$X"); echo "exit=$?"` - green: 52 `ok` rows, each speech row `diff=0`, `52/52 clips' …`, `exit=0`.
- V10: `cd .scratch/bench-stage; dotnet run --no-build -- --bench metrics-check | tail -n 1; for a in truth-check run; do dotnet run --no-build -- --bench $a; echo "$a exit=$?"; done; printf 'cease fire\nfire missiles at hotel one\n' | dotnet run --no-build -- --bench; echo "bare exit=$?"; cd -` - green: `23/23 metric checks …`; the usage stop, then `bench: run is not built yet (bench-fast-replay Phase 3)`, each `exit=2`; plan-1 V3's line, `bare exit=0`.
- V11: `for k in 960 961 0; do python3 -I -c "import json,sys;m=json.load(open(sys.argv[1]));c={c['file'][10:]:c for c in m['cases']};k=int(sys.argv[3]);c['cease_fire.wav']['speech'][-1]['end']+=k;k or c['resume_fire.wav'].pop('speech');json.dump(m,open(sys.argv[2],'w'))" "$F/audio/manifest.json" $T/m$k.json $k; p=$(cygpath -am $T/m$k.json); (cd .scratch/bench-stage && dotnet run --no-build -- --bench truth-check --manifest "$p" --fixtures-root "$X"); echo "$k exit=$?"; done` - green: `960` 52/52, `exit=0`; `961` a `FAIL` row for `cease_fire.wav` with `diff=961`, `bench: truth check failed on 1/52 clips: cease_fire.wav`, `exit=2`; `0` `bench: <p>: case "audio/tts/resume_fire.wav": no key `speech``, `exit=2`.
- V12: plan-1 V11 verbatim, `P=.scratch/bench-probe`, `--out .scratch/joined-p2` - green: exit 0 and `## Flips: 0`, each layer. Phase 1's `d1.jsonl` stands: no native change since `cdc7f7f`, so no harness run.
- V13: `py "Planning~/host-run/host_run.py" EditMode PlayMode`, foreground, `timeout` 600000 - green per the binding's Commands 1-2.
- V14: `git diff --name-only main -- 'NativeBridge~' Runtime "$F/audio/tts" 'NativeBridge~/harness/expectations.json'; git status --porcelain` - green: nothing, then only the files table and this plan's two docs (`?? memory/2026-11-08.md.meta` excepted, never committed).

**Truth stop:** a V2 refusal, a V9 `FAIL` or any `diff` but 0 stops for the human with the output; never a moved boundary, an edited constant or a touched WAV.

**Human step (G2 packet, no gate item):** the maintainer inspects `launch_one_missiles_target_hotel_one.wav` (phrase), `ladder_p_set_heading_two_seven_zero_gap200ms.wav` (tightest gap) and `pause_lead_cease_fire.wav` (peak 0.05 over noise) against their `speech`.

## Open questions that block implementation

None.

## Blast radius

The manifest's `JsonUtility` readers (`VoxrFixtureCorpusTests`, `VoxrWavReplayTests`, `VoxrFrontEndParityTests`, `VoxrAudioTestSuiteAsset`); joined runs (`Joined.cs` :190, `JoinedReport.cs` :156); `generate.sh` regeneration now rewrites `speech`; `Documentation~/api/scriptable-objects.md` :142 (tier 3, after G2); `.scratch/`.

## Runtime claims

- Stock `py` imports `generate.py` without `numpy`, `soxr` (V1).
- Every committed block splits as its segments, none refused (V2; risk :118).
- `git diff` reads the CRLF working copy as LF lines (V4).
- Regeneration's `write_truth` after the WAVs: unverified, nothing is regenerated (:110).
- `JsonUtility` ignores `speech` (V13).

Hazards: the `JoinedInput.cs` edit precedes V2 — a manifest holding `speech` before the reader knows it stops every joined run, exit 2; V2 is the manifest's only write.
