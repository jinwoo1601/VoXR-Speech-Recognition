# Phase 2 — persisted plan (2026-10-03)

F10 (Should, R4), no source change. Anchors at `d52d481`. R = `.scratch/lab-resolver-slot-exemption/`; L = `.scratch/lab-resolver-slot-scoring/`, read-only (`../../lab-resolver-slot-scoring` from a stage); P = `Runtime/Commands/VoxrCommandParser.cs`; LC/LP = L`Lab.cs`/L`lab/Program.cs`; the set = `AllRequiredSlotNames` (LC:127) on Set_Combat, `weapon,target,range,heading` on the demo grammar. Departures from the Phase 2 row, human-approved at PLAN 2026-10-03: a record-only probe ("no patch"); the A0 baseline and `plain/` inertness stage (`plain/`: the probe's declarations only, ruled option (i)), DocCheck as staging sanity, the `--lab tb` cross-check, `eager`/`probe`/`demo` named.

## Files to change

|Path|Change|Why|
|---|---|---|
|R`probe.py`, `Lab.cs`, `Program.cs`, `compare.py`, `report.md`|create|F10, steps 2-4, 6|
|R`plain/`, `built/`, `nokey/`; `out/`, `out-plain/`, `out-nokey/`|create (stages; their outputs)|F10, steps 1, 4|
|`plan-2.md`; `architecture.md` `## Build plan`|create; one link line|the plan|

1. **Stages.** Create `probe.py`, `Lab.cs`, `Program.cs`, `compare.py` first; then per stage: stage, copy, probe, build. In Git Bash from the project root (CRLF fails under WSL): `env -u MSYS_NO_PATHCONV bash "Planning~/features/coverage-in-selection/ab-rig/stage.sh" WORKTREE .scratch/lab-resolver-slot-exemption/<stage>`, never `rig-src/`; copy in R`Lab.cs`, R`Program.cs` (stage.sh :29 wipes the stage); `python3 R/probe.py R/<stage>/VoxrCommandParser.cs` with `--decls` for `plain/`, no flag for `built/`, `--nokey` for `nokey/`. Each stage is fresh from `stage.sh`, so no hunk applies twice. Then `mkdir -p` R`out` R`out-plain` R`out-nokey`. Build, run under WSL: `wsl.exe -e bash -lc 'cd "/mnt/d/Workspace/VoXR-Speech-Recognition/.scratch/lab-resolver-slot-exemption/<stage>" && dotnet build -v q --nologo && dotnet run --no-build -- <args>'`.
2. **`probe.py <file> [--decls|--nokey]`** edits the staged file byte-wise, keeping line endings; every inserted line, declarations included, ends `// LABPROBE`; an anchor not matching exactly once where stated exits 1, nothing written. L`a4l.patch` hunks:
   - declarations (`--decls` inserts only these): after P:173 `const float RequiredSlotMissPenalty = -1.0f;`, patch :11-16 (the statics, `LabCand`, `LabRound`);
   - hooks, in `ParseInternal` (P:2517, before `TryEagerCommit` P:4726): `labRound` (:26) after `int bestExemptedSlots = 0;` (P:2562); `LabCands.Add` (:34-35) after the `);` (P:2641) closing `var matchResult = TryMatchScored(` (P:2636); `LabRounds.Add` (:61-62) before `if (bestLeadingRequiredMissed)` (P:2881);
   - `--nokey` also deletes the four lines from `if (candidate.ExemptedSlots != bestExemptedSlots)` (P:3587-3590, `CompareCandidate`).
   No other hunk; `LabCand.Exempted` widens the `byte` `ExemptedSlots` (P:3463).
3. **R`Lab.cs`, R`Program.cs`**, copies; LC, LP unedited:
   - `SetArm(arm, fill)` (LC:134-142): the `LabArm`/`LabFillable` lines and write-only `_parserKey` go; the constructor gets `registeredSlots:` `null` for an empty fill, else `new RegisteredSlotNames(fill)` (P:154; last parameter P:522).
   - Arms `A0` (0, no set) and `A4L` (6, the set) only in `RunOcc` (LC:262-271, per-slot loop dropped), `RunThin` (:313), `RunFull` (:360); elsewhere (`eager`, `demo`, `doc5`, `probe` armB, `LAB_TB_ARM`) arm 0 = no set, else the set.
   - Same constructor change in `probe` (LC:441-451, both `mk`), `demo` (:464-470), `doc5` (:502-506, set `weapon,target`). `probe` also calls `pp.BuildCoverageTables(t)` per line before its position loop (LC:453), filling the mask via `SnapshotRegisteredSlots` (P:4203, :4172) as the runtime does.
   - Program: `LAB_ARM` (LP:158-160) goes; `LAB_FILL` (LP:161-163) feeds `registeredSlots:` at `--prefix-verdicts` (LP:256), `--verdicts` (:279), default replay (:308).
   - Output names and columns kept, for L`analyze.py`.
4. **Passes**, the lab's commands (L`report.md` :538-552, :571, :689, :699); `<o>` = `../out`, `../out-plain` or `../out-nokey`; U = `L/out/utt-occ.txt L/out/utt-thin.txt L/out/utt-full.txt`:

|Pass|Args|Stage|Expected|Against|
|---|---|---|---|---|
|DocCheck|`--doccheck`|plain|66/66, exit 0|-|
|occ A0|`--lab occ <o>`|built|(a) 18/40, (b) 5/40, 0 barred|L`out/occ.tsv` A0 rows|
|occ A4L|same run|built|35/40, 28/40; `{track}` 15/15, 12/15; launch_missile 4/4|A4L rows; `analyze.py` totals|
|thin|`--lab thin <o>`|built|16/124 (2 one-word, 14 two-word)|L`out/thin.tsv` A4L rows|
|full|`--lab full <o>`|built, nokey|0/81 vs A0; nokey 5|L`out/full.tsv`; `lab-2026-10-02.md` §Collateral changes, L`report.md` :643|
|corpus|< `L/corpus-utts.txt`, no set and `LAB_FILL` = the demo set|plain, built|66 fired diffs; 0 A0 fires removed or altered|L`out/corpus-a4l.tsv`|
|prefix|`--prefix-verdicts` < `L/out/corpus-nonblank.txt`, both sets|plain, built|4014, A4L = A0|L`out/prefix-arm6.tsv`|
|eager|`--lab eager 6 U`|built|Commit 40/40 over 378 prefixes|L`out/eager-set-{6,0}.tsv`|
|probe|`--lab probe 6 U L/corpus-utts.txt`|built|4959 positions, 0 differ|-|
|demo|`--lab demo {0,6}` x 3 Q1 utterances; `--lab doc5 {0,6}` the stutter|built|Q1 A4L column|`lab-2026-10-02.md` Q1|
|rounds|the loop below|built, nokey|14|-|
|tb|`LAB_TB_ARM=6`, `--lab tb <o> U`|built|14|L`out/tb-arm6.tsv`|

   - `rounds`, the stage's WSL command (nokey: `> ../out-nokey/rounds.txt`): `declare -A s; d=../../lab-resolver-slot-scoring/out; cat $d/utt-occ.txt $d/utt-thin.txt $d/utt-full.txt | while IFS= read -r u; do [[ ${s[x$u]} ]] && continue; s[x$u]=1; echo "== $u"; dotnet run --no-build -- --lab utt 6 all $u < /dev/null; done > ../out/rounds.txt`; deduped in first-seen order like `tb` (LC:388-391).
   - Key rounds, primary (the register's method): utterances whose round-1 winner (first `round` line under `== <utterance>`) differs, built against nokey, a missing side counting. Secondary: each utterance's first differing round only, so rounds shifted by a different consumed span do not count. Both and `tb`'s 14 are reported, none forced to 14; a difference falls under step 5.
   - Cross-check: distinct (utterance, `round`) rows, each mapped (the cumulative `round` is unaligned across runs) to the built `rounds.txt` round with its winner, winnerScore, winnerExempted; unmatched or ambiguous rows reported.
   - Comparisons against L outputs strip `\r` first: "identical after CRLF->LF normalisation". Plain against built (both WSL) stays raw bytes.
   - Corpus fires: results >= 0.6 as intent plus slots (L`report.md` :492).
   - `compare.py` (from R) computes every figure, diff and inertness check from the outputs, L`out/`, P and the stages; prints the F10 table (eager in F10's form: A4L/A0 Commits over prefixes); no count typed by hand. `analyze.py` runs from R: `python3 ../lab-resolver-slot-scoring/analyze.py`.
5. **Mismatch rule** (no tolerance in the docs): each F10 figure match or mismatch; a mismatch explained by row-level evidence, first checking `ScoreFollowUp` is off these paths, the Editor runner-up uncompiled (`UNITY_EDITOR`), `ExemptedSlots` a byte (the lab's an int); else `debugger`. No source change here. One implicating DR-8 or DR-9 stops for the human; others go to the gate, recorded at the as-built reconcile.
6. **R`report.md`**: staging record (each `STAGED_FROM`, inertness (a), (b), DocCheck); commands as run; the F10 table; both key-round figures, the cross-check; mismatch analysis. A failure from outside Phase 2: "not run", with its reason.

## Contracts and interfaces

- Inertness (a): R`plain/`'s and R`built/`'s parser minus its `// LABPROBE` lines byte-equals P. (b): plain and built corpus and prefix outputs byte-equal under both sets, proving the hooks inert. nokey differs from built by exactly the four deleted lines.
- Outputs: the lab's names, plus `doccheck.txt`, `corpus-{a0,a4l}.tsv`, `prefix-{a0,arm6}.tsv`, `probe.txt`, `demo.txt`, `rounds.txt`.
- `compare.py` stdout: per F10 figure, lab A4L, built, match or mismatch.

## Trade-offs

|Choice|Alternative|Why|
|---|---|---|
|Record-only probe|`a4l.patch`|the register rejects the patch; occ, thin, full, tb read `LabRounds`/`LabCands`|
|`// LABPROBE` per line, declarations too|hand edit|(a) becomes strip-and-compare on `plain/` and `built/`|
|`--lab utt` per utterance|a new mode|no adaptation beyond step 3|

## Acceptance criteria

- [ ] 1. Green (F10): DocCheck 66/66 on `plain/`; inertness (a), (b) empty; the A0 baseline matches L's; `compare.py` run, its table in R`report.md`. No source change: `verification` Commands 1-2 not re-run. An F10 mismatch is a result, not a failed gate, unless step 5 stops it.
- [ ] 2. Every step 4 pass run, or "not run" recorded with its reason (requirements §9).
- [ ] 3. The diff touches only `.scratch/**`, `plan-2.md`, the `architecture.md` link line, `memory/**`.

Out of scope: Phases 3-4; `Runtime/`, `Tests~/`, ab-rig or L edits; product docs; tier-1 docs, the lab record; as-built deltas.

## Open questions that block implementation

None.

## Blast radius

Nothing outside `.scratch/` (the ab-rig and L only read); `memory/` takes the session note.

## Runtime claims

- `stage.sh WORKTREE` stages the Phase 1 parser (no `+dirty`); DocCheck prints 66/66.
- WSL: `dotnet` 10 on PATH, builds on `/mnt/d`, env vars and stdin redirects pass `wsl.exe -e bash -lc`; `dotnet run` emits LF (L's outputs: CRLF).
- The probe hunks apply at the live anchors and compile in each mode, inert per (b) and per (a) on `plain/` and `built/`; nokey differs only in the removed comparison.
- R`Lab.cs` compiles, reaching `internal` `RegisteredSlotNames` in the one `abrig` assembly (`Rig.csproj` :25).
- The A4L `probe` arm reads a non-empty mask.
- With `< /dev/null`, `dotnet run` leaves the `rounds` loop's stdin unread.
- Each F10 figure; L's `utt-*.txt` are LF; `analyze.py` prints `## M1 totals`, then a `KeyError` (no A4 rows): only the totals are read.
