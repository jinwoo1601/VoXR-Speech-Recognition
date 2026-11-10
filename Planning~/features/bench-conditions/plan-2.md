# Phase 2 — persisted plan (2026-11-09)

Scope: `Tests~/Fixtures/audio/negatives/manifest.json` with the 16 phrases below, rendered once by `generate.sh` on the cached venv and voice with its truth, `--bench truth-check` on it, and the phrase-list rows in `condition-check`; it takes on `condition-check`'s optional `--negatives --pin` arguments in `Bench.cs` (the rows' inputs, register *negative set*), a control row (a near-miss row that cannot fire proves nothing, same entry) and a truth-check before the render (§6; architecture *Input boundary*).

## Files to change

`R` is `Planning~/features/coverage-in-selection/ab-rig`; `NG` is `Tests~/Fixtures/audio/negatives`.

| Path | Action | Why |
|---|---|---|
| `NG/manifest.json` | create | F6; register *negative set* (1) |
| `NG/neg_*.wav` (16) | create, by `generate.sh` only | F6 |
| `R/BenchConditions.cs` | modify | register *negative set*: `condition-check` checks the list |
| `R/Bench.cs` | modify | `condition-check` dispatch |

## Contracts and interfaces

**The list** (manifest order; file `audio/negatives/<stem>.wav`, stem `neg_oos_` or `neg_near_` + the phrase's words joined by `_`):

| # | Phrase | Group | Shares | Near |
|---|---|---|---|---|
| 1 | what time is the briefing | negative-out-of-set | | |
| 2 | the coffee here is terrible | negative-out-of-set | | |
| 3 | could you repeat the question | negative-out-of-set | | |
| 4 | where is everybody | negative-out-of-set | | |
| 5 | my headset feels heavy | negative-out-of-set | | |
| 6 | check the shield status | negative-out-of-set | | |
| 7 | good job pilot | negative-out-of-set | | |
| 8 | let me think about this | negative-out-of-set | | |
| 9 | the fire alarm went off | negative-near-miss | fire | `cease_fire` |
| 10 | we lost the target | negative-near-miss | target | `approach_target` |
| 11 | keep your distance | negative-near-miss | distance | `set_distance_named` |
| 12 | the weapons are offline | negative-near-miss | weapons | `mode_weapons` |
| 13 | change the heading later | negative-near-miss | heading | `set_heading` |
| 14 | resume the meeting | negative-near-miss | resume | `resume_fire` |
| 15 | navigation is broken | negative-near-miss | navigation | `mode_navigation` |
| 16 | when does the launch start | negative-near-miss | launch | `launch_weapon` |

Each near-miss shares one word of a pattern in `Program.cs` `Commands()`, never a whole pattern; the rows decide. The maintainer's approval is the G2 packet's (F6, §9).

- **NG/manifest.json.** Corpus schema, 4-space indent, CRLF throughout, ending `}` CRLF. Top level `voice` `en_US-lessac-medium`, `provenance` `Stand-in negative set (adr-0007): voice en_US-lessac-medium, synthesized with piper-tts by Tests~/Fixtures/generate.py. Synthetic TTS evidence cannot rank decoders (C12).`, `cases`. Per case, at 12 spaces, in order: `"file": "audio/negatives/<stem>.wav",` first, then `category`, `phrase`, `"peak": 0.2`, `"expectedIntent": ""`, `"expectedSlots": []`, `description` (`Out-of-set negative (adr-0007): no word in the pin's grammar; no command may fire.` / `Near-miss negative (adr-0007): shares "<word>" with <intent> but is no command; no command may fire.`); no `speech` (`write_truth` inserts it after `file`), no `expectedTranscript`. The register's "no `expectedIntent`" is read as the silence case carries it (`audio/manifest.json` :589-590), which register (3) itself calls "no `expectedIntent`"; `ReadManifest` requires both keys (`JoinedInput.cs` :653-656). If the Write tool cannot emit CRLF: `py -I -B -c "import sys;p=sys.argv[1];d=open(p,'rb').read().replace(b'\r\n',b'\n').replace(b'\n',b'\r\n');open(p,'wb').write(d)" $NM`, before V1.
- **Bench.cs** `Run`: `condition-check` with length 2 → `BenchConditions.Check(null, null, slots, commands)`; length 6, `args[2] == "--negatives"`, `args[4] == "--pin"` → `Check(args[3], args[5], slots, commands)`; else `usage: --bench condition-check [--negatives <manifest.json> --pin <expectations.json>]`. Rule 1: `Run` holds no comment; nothing removed.
- **BenchConditions.cs.** `using VoXR.Commands;`; Depends adds `Joined, JoinedScorer, VoxrSlotDefinition, VoxrCommandDefinition`. `Check(string negatives, string pin, VoxrSlotDefinition[] slots, VoxrCommandDefinition[] commands)`: rows 1–23 unchanged; after row 23, `negatives != null` → `PhraseList(negatives, pin, slots, commands)`; the summary line unchanged. New `static void PhraseList`, those four parameters: `ReadManifest(negatives)`, `p = ReadPin(pin)`; words = each `p.Grammar` entry but `[unk]` split on `' '`, ordinal set. Rows in `Expect`'s form:
  - 24 `the list`: got `<n> cases: <o> negative-out-of-set, <k> negative-near-miss, <x> other; no-command <e>; lowercase <l>` (e: `ExpectedIntent` `""` and no slots; l: `Phrase` matching `^[a-z]+( [a-z]+)*$`, no `Segments`); want `16 cases: 8 negative-out-of-set, 8 negative-near-miss, 0 other; no-command 16; lowercase 16`.
  - 25 `control: cease fire`: a `ManifestCase` `{ Name = "control", File = "control", Phrase = "cease fire", ExpectedIntent = "cease_fire", ExpectedSlots = [] }` through `OneFinal`; got `<Class> fires=<FireTimes.Length>`, want `correct-fire fires=1`.
  - 26–41, one per case in order: g = the `Phrase`'s words (split on `' '`) in words, phrase order, space-joined, else `none`; k = g's word count (0 for `none`); `what` `<Name> [<g>]`. `Category` `negative-out-of-set`: got `<g>`, want `none`. Otherwise through `OneFinal`: got `shares <k> | <Class> fires=<FireTimes.Length> pending=<Pending>` (`bool.ToString()`), want `shares 1 | correct-no-fire fires=0 pending=False`. No word table: the shared word shows in `what`.
  - `static JoinedScorer.Outcome OneFinal(JoinedInput.ManifestCase c, JoinedInput.Pin p, VoxrSlotDefinition[] slots, VoxrCommandDefinition[] commands)`, a class member; `PhraseList` passes its `p` and `Check`'s `slots`, `commands`: `d` = `DumpCase { Name = c.Name, SampleRate = 48000, Samples = 48000·w }` (w words), one result `{ Seq = 0, Final = true, Consumed = 48000·w, Payload = {"result":[{"conf":1.0,"end":i.5,"start":i,"word":"<w_i>"},…],"text":"<phrase>"} }` (i invariant), `Finals` the phrase; `s = Joined.ScoreRecogniser(c, d, slots, commands)`, then `Joined.AppliedGrammarGuard(c.Name, s.AppliedGrammar, p)` (`p` the pin, as `FastRun`'s `pin`, `Bench.cs` :280-286); returns `s`.
  - Rule 1: `Check` and the class body hold no comment; nothing removed.
- **Commands.** `generate.sh --manifest audio/negatives/manifest.json` (appended, argparse's last wins; cwd `Tests~/Fixtures`); `--bench truth-check --manifest <NG manifest> --fixtures-root <Tests~/Fixtures>`; `--bench condition-check --negatives <NG manifest> --pin <expectations.json>`, absolute paths from the stage.
- **BFR-1.** This render is the full list, so `write_truth` runs after the loop on all 16; checked: the `truth: 16` line (V4) and 16/16 (V5). The raised-loop path is not exercised.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| optional `--negatives --pin` | fixed repo-relative paths | the stage's cwd is `$T/stage`; the bare form keeps Phase 1's rows and V5 |
| the pin's `grammar` | the rig's `grammarJson` | register: "the pin's `grammar`"; the guard ties the two |
| control row | none | a timeline the recogniser ignores passes every near-miss |
| `result` words at conf 1.0 | `text` only | the harness's finals carry words (`VoxrJsonParser` :49-51); no confidence gate excuses a silence |
| rows before the render | after | a redraft costs no render |
| `neg_` prefixes | bare stems | basenames stay unique when Phase 3 joins both manifests |

## Acceptance criteria

- [ ] F6: 16 renders and manifest in `NG/`, 8 and 8, no-command, cached voice, no download (V2 row 24, V4).
- [ ] F6, register: out-of-set rows `none`, near-miss rows fire nothing, control fires; 41/41; bare 23/23, its rows unchanged (V2).
- [ ] F6 truth, §6: the pre-render stop (V3); 16/16 (V5); CRLF kept (V6).
- [ ] F14, F4: V4's and V11's diffs empty; fast `cmp=0` (V8); joined 0 flips (V9); DocCheck (V7).
- [ ] Commands 1–2 green (V10).

**Steps:** one Git Bash at the tree root: `R=Planning~/features/coverage-in-selection/ab-rig T=.scratch/bench-conditions/p2 F=.scratch/bench-conditions/baseline-report.md BREF=fcec2cf NM="Tests~/Fixtures/audio/negatives/manifest.json"; mkdir -p $T; N=$(cygpath -am "$NM") P=$(cygpath -am "NativeBridge~/harness/expectations.json") FX=$(cygpath -am "Tests~/Fixtures")`. V0, then the three files, then in order, serial. Let EOL be `py -I -B -c "import sys; [print(p, (d:=open(p,'rb').read()).count(b'\r\n'), d.count(b'\n')) for p in sys.argv[1:]]"`.

- V0: EOL `$R/Bench.cs $R/BenchConditions.cs > $T/eol-before.txt` — two lines.
- V1: EOL `"$NM"` — CRLF count = LF count.
- V2: `env -u MSYS_NO_PATHCONV bash "$R/stage.sh" WORKTREE $T/stage recogniser && (cd $T/stage && dotnet build -v q --nologo && { dotnet run --no-build -- --bench condition-check > ../cc-bare.txt; echo "bare=$?"; dotnet run --no-build -- --bench condition-check --negatives "$N" --pin "$P" > ../cc-neg.txt; echo "neg=$?"; }); tail -n 1 $T/cc-bare.txt $T/cc-neg.txt; grep -c '^FAIL' $T/cc-neg.txt; diff <(grep -E '^(ok|FAIL)' $T/cc-bare.txt) <(grep -E '^(ok|FAIL)' $T/cc-neg.txt | head -n 23) && echo same23` — green: `bare=0`, `neg=0`, `23/23 …` and `41/41 condition checks verified against known-value toys.`, `0`, `same23`.
- V3: `(cd $T/stage && dotnet run --no-build -- --bench truth-check --manifest "$N" --fixtures-root "$FX") 2> $T/tc-pre.err; echo "exit=$?"; grep -c 'no key `speech`' $T/tc-pre.err` — green: `exit=2`, `16`.
- V4: `C=$(cygpath -m "${XDG_CACHE_HOME:-$HOME/.cache}/voxr-fixture-gen"); test -x "$C/venv/Scripts/piper.exe" && test -e "$C/voices/en_US-lessac-medium.onnx" && echo cached && { bash "Tests~/Fixtures/generate.sh" --manifest audio/negatives/manifest.json > $T/gen.out 2>&1; echo "exit=$?"; }; grep -c -E '^(Bootstrapping|Downloading)' $T/gen.out; grep -c '^  audio/negatives/neg_' $T/gen.out; grep -E '^(16 fixture\(s\) written under|truth: 16 case\(s\) written to )' $T/gen.out; ls "Tests~/Fixtures/audio/negatives" | wc -l; git status --short -- "Tests~/Fixtures/audio/tts" "Tests~/Fixtures/audio/manifest.json"; git diff main --stat -- "Tests~/Fixtures/audio/tts" "Tests~/Fixtures/audio/manifest.json" "NativeBridge~/harness/expectations.json"` — green: `cached`, `exit=0`, `0`, `16`, both lines, `17`, nothing, nothing.
- V5: `(cd $T/stage && dotnet run --no-build -- --bench truth-check --manifest "$N" --fixtures-root "$FX") > $T/tc.txt; echo "exit=$?"; grep -c '^ok  ' $T/tc.txt; tail -n 1 $T/tc.txt` — green: `exit=0`, `16`, `16/16 clips' speech truth verified against the committed WAVs.`
- V6: EOL `"$NM" $R/Bench.cs $R/BenchConditions.cs` — manifest as V1; each `.cs` CRLF 0 where `$T/eol-before.txt`'s is, else CRLF = LF.
- V7: recipe :229 verbatim — green per :232.
- V8: `bash "$R/bench.sh" --ref $BREF --out $T/fast >/dev/null; echo "exit=$?"; cmp $F $T/fast/report.md; echo "cmp=$?"` — green: `exit=0`, `cmp=0` (Phase 1's harness build; absent, bench-paced-mode `plan-1.md` V1–V2 first).
- V9: this feature's `plan-1.md` V7 with `--dump $T/fast/dump.jsonl` — green: `## Flips: 0`, each layer.
- V10: Commands 1–2, `py "Planning~/host-run/host_run.py" EditMode PlayMode`, one foreground call, timeout 600000 — green per recipe :14.
- V11: `git diff --name-only main -- 'NativeBridge~' Runtime 'Tests~/Editor' 'Tests~/Runtime' 'Tests~/Fixtures/audio/tts' 'Tests~/Fixtures/audio/manifest.json' 'Tests~/Fixtures/generate.sh' 'Tests~/Fixtures/generate.py'; git status --porcelain` — green: nothing; the files table and this plan's docs (`?? memory/**.meta` excepted), no `.meta` under `NG/`.

**Stops,** for the human with the output in `$T/`: a V2 `FAIL` (a failing phrase is a redraft by the architect, never a loosened check, edited want or swapped phrase); V3 not `exit=2`; V4 without `cached`, a download line, or exit ≠ 0 or no `truth: 16` (WAVs without truth, BFR-1); V5 a `FAIL`; V8 `cmp=1`; V9 a flip; V10 not green, exit 3 included.

## Open questions that block implementation

None. The maintainer's approval of the list is not awaited (G2 packet; a refusal is a re-render).

## Blast radius

Unity imports `NG/` in the host run (`.meta` made and deleted by `host_run.py`); Phase 3's `condition` reads this manifest and these names; later gates' toys run V2's argument form; `Bench.cs` cites in the architecture move (as-built reconcile); a refused list re-renders all 16 and rewrites truth; `.scratch/bench-conditions/p2`.

## Runtime claims

- `generate.sh` uses the cached venv and voice, the appended `--manifest` wins, and it writes only under `NG/` (V4).
- `write_truth` keeps CRLF and the file lines' order (V4, V6); each render passes the truth rule there and in `TruthCheck` (V4, V5).
- A one-final timeline drives the staged recogniser without `RecogniserConfig` first: `cease fire` fires, each near-miss fires nothing and leaves nothing pending (V2).
- The bare form's rows are unchanged (V2); the fast report's bytes are unchanged (V8); the host run takes the new folder (V10).

Hazards: V0 before the first edit; V2 before V4, so a redraft precedes any render; V3 before V4; the render once, never after V5 passes (Piper is not byte-deterministic; the committed WAVs are those V5 checked); V8 before V9 (its dump); V10 after V4; V11 last; `bench.sh`, `joined.sh`, the harness and `host_run.py` never concurrent.
