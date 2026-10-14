---
type: architecture
feature: joined-rig-parser
topic: joined-rig
status: draft
updated: 2026-10-14
sources:
  - Planning~/features/joined-rig-parser/requirements.md
  - Planning~/design-docs/joined-rig.md
  - Planning~/design-docs/joined-rig/decisions/adr-0001-file-handoff-pipe.md
  - Planning~/design-docs/joined-rig/decisions/adr-0002-phased-parser-then-recogniser.md
  - Planning~/design-docs/joined-rig/decisions/adr-0003-outcome-classes-never-netted.md
  - Planning~/design-docs/joined-rig/decisions/adr-0004-on-demand-instrument.md
  - Planning~/design-docs/joined-rig/decisions/adr-0005-measurement-discipline.md
  - Planning~/design-docs/joined-rig/decisions/adr-0006-host-neutral-runner.md
  - Planning~/design-docs/joined-rig/recon-2026-10-13.md
  - "NativeBridge~/harness/main.cpp, NativeBridge~/src/vosk_bridge.cpp, NativeBridge~/harness/expectations.json, Planning~/features/coverage-in-selection/ab-rig/{stage.sh, Rig.csproj, Program.cs, DocCheck.cs}, Planning~/features/fidelity-miss-cost/ab-rig/, Runtime/Commands/VoxrCommandParser.cs, Runtime/Commands/VoxrCommandRecogniser.cs, Runtime/Commands/VoxrCommand.cs, Runtime/VoxrResult.cs, Tests~/Fixtures/audio/manifest.json, Planning~/verification-recipe.md, .claude/bindings/unity.md (## verification)"
---

# feat-joined-rig-parser — Architecture

Realizes `Planning~/features/joined-rig-parser/requirements.md` (Track **Full**; S1, F1–F11, the §6 NFRs, the §10 items, §2 In 8's tuning pass): phase 1 of design `joined-rig` (adr-0002). The harness writes an opt-in JSON-lines dump of every decoder result (adr-0001); the parser A/B rig gains a joined mode that reads one dump (parser side varied) or two (decoder side varied), refuses a run whose pinned side is off its pin, and scores each final through `Parse(text, words)` into one outcome class with slot credit, never netted (adr-0003, adr-0005). The governing driver is the **kill criterion** (design §0, rule 2): no command-level number from a run whose pinned decoder side does not reproduce `NativeBridge~/harness/expectations.json` byte for byte — so the rig reads its finals from the dump's own records, checks them and the decoder configuration before parsing anything, and every input defect stops the run by name.

## What this implements

| Requirement | Work | Phase | Section |
|---|---|---|---|
| F1 | Harness `--dump`; writer fed from `DrainResults` | 1 | Register: audio time, opt-in, schema |
| F2 | Kill check on the pinned dump's records and header | 2 | Register: kill check |
| F3, F4 | `joined.sh`'s two shapes; `Parse(text, words)` | 3 | Register: run script; Data contract |
| F5 | Gate, primary result, classes, slot credit, extra fires | 3 | Register: gate to multi-final |
| F6 | Joined report | 3 | Data contract |
| F7, F8 | `sealed`, `layer` marks; split tagged; `--evaluate` | 2, 3 | Register: marks |
| F9 | Both halves Windows-native in Git Bash | all, 4 | Register: hosts |
| F10 | Tuning record | 4 | Data contract |
| F11 | Automated pin compare per ref; DocCheck exits 0 | 2, 4 | Register: grammar-pin guard |
| NFRs | Strict readers; named stops in `joined.sh` | 2 | Non-functional realization |
| §10 items 1–6 | Schema; home; run script; hosts; provenance; multi-final | 1–3 | Register |

## Context & scope

The harness (`NativeBridge~/harness/main.cpp`) keeps only non-empty final `text` (`DrainResults` :41-61) and passes on ordered equality with its manifest, the pin `expectations.json`, regenerated only by `--write-baseline` (:227). The bridge queues a final per utterance (`vosk_bridge.cpp` :128-134), a partial only when it changed (:135-141), a tail final at stop (:158-160); word times exist on finals only (:235). The rig (`Planning~/features/coverage-in-selection/ab-rig/`) stages ten parser sources from a ref or the worktree (`stage.sh` :15-39) plus its own files (:41-42), dispatches one mode per flag (`Program.cs` :154-), parses text only (:304), and has no scorer; the recipe's DocCheck runs it (`Planning~/verification-recipe.md` :222-232).

**Owns:** the dump contract and writer; the joined mode (readers, guard, kill check, scorer, scored records, report); `joined.sh`; the `sealed`/`layer`/`provenance` keys; the split tag; the tuning record. **Untouched:** `NativeBridge~/src/**` (no `.so` to commit); `expectations.json`; the binding and Command 3 (adr-0004); `Runtime/**` (§2 Out 3), `VoxrBatchTestRunner.cs` included (fork 2); `DocCheck.cs` and the rig's other modes; product docs (after G2).

## Decision register

- **Audio time — push counters on every record; Vosk word times on finals (decision 1).** Each `result` record carries `pushed`, the input samples pushed for the case when the harness drained it, and `prevPushed`, the same at the previous drain; the header carries `ringCapacity` (`RingBuffer<float>::kCapacity`, 65536, read at :196). The ring never holds more than its capacity, so a record's emission position — input samples the decoder had read when it emitted — lies in `[prevPushed − ringCapacity, pushed]`. *Phase 2 can rely on:* exact per-case order of finals and partials (after the bridge's dedup); word times on non-empty finals exact relative to the decoder stream, with a run-dependent per-case origin, so usable within a case only; for other records a guaranteed bracket never narrower than the ring, scheduler-dependent, so it differs between runs. *Why:* §2 In 1 scopes the change to `NativeBridge~/harness/`, Out 3 excludes recognition changes, and the harness sees nothing else of decoder progress. *Rejected:* a bridge-side counter or `vosk_recognizer_set_partial_words` (exact, but a `NativeBridge~/src/**` hot-path change outside In 1, with Command 3's arm64 build and a `.so` on an ABI change; `schema` lets `joined-rig-recogniser` add one); lockstep pushing (no consumption count to wait on; new chunking risks the 16/16 verdict).
  **Amended 2026-10-14:** measured in Phase 1 (two `--dump` runs of one build): Vosk word times do not restart at zero after `vosk_recognizer_reset`; they run on across cases (first case's first word at 0.36 s, last case's at 60.85625 s), and the origin is run-dependent — finals' `result[]` differ between the two dumps in 14 of 16 cases, each by one constant shift per case (0.0854 s to 1.3654 s), while final `text`s and partial sequences are equal. Nothing in `Runtime/` or the rig reads `VoxrWord.StartTime`/`EndTime`, so `Parse(text, words)` and scoring are unaffected. Exact per-case word timing joins deferral JRP-1 as `joined-rig-recogniser`'s (a bridge change), beside exact partial timing. See `plan-1.md` runtime claims (criterion 5 finals' `result` arrays, criterion 6 word times; claims g and h of the gate-A check).

- **Opt-in — the flag `--dump <path>` (decision 7).** Absent, nothing changes; present, the harness opens the path in binary mode before loading the model, creating its parent directory (exit 2, named, on failure); verdict, stdout and exit code are unchanged. Command 3's line passes no `--dump`. *Why:* adr-0004; the command that made a dump shows it, as does the usage text (:111). *Rejected:* probe 1's environment variable (a variable left exported makes every standing Command 3 write a file, invisibly); always dumping (changes the standing check's side effects).

- **Schema — JSON lines, framed per case, payload verbatim (§10 item 1).** UTF-8, LF only. `header`; per case `case`, its `result`s, `case-end`; closing `end` (fields in *Data contract*). *Why:* adr-0001; a verbatim payload leaves Vosk's shape to Vosk; framing and trailer prove a dump complete (NFR). *Rejected:* the harness's own `actual` list as input (the kill check would re-test the harness, not the plumbing — adr-0001 has it read the finals *in the dump*); one JSON document (lost whole on a crash).

- **Kill check — finals derived from the pinned dump's records, plus its configuration (F2).** For the pinned dump (the one dump of a parser-side run; side A of a decoder-side run): header `gain`, `libvosk`, `grammar` equal the pin's; no case has errors; case sets are equal; per case the ordered non-empty final `text`s equal `expectedFinals` ordinally. Any difference: exit 3, each case named with expected and actual, no command-level number. Sealed and recogniser-layer cases are checked too: a transcript compare scores nothing, and the standing harness already reports them. Side B is listed, never checked. *Why:* rule 2, adr-0005. *Rejected:* finals only (a dump decoded at another gain could pass); checking side B (adr-0005's rejected branch).

- **Fire gate — `Score >= MinScore`, `Confidence < 0 || Confidence >= MinConfidence`, and complete (decision 2).** Complete = `VoxrCommandParser.HasUnfilledRequiredSlot(cmd, def)` (:4539) false for the intent's definition in the rig's `Commands()`: the recogniser's flush gate (`VoxrCommandRecogniser.cs` :1027-1029, `IsIncomplete` :1684-1688) minus resolution, which is recogniser layer. `MinScore` 0.6 and `MinConfidence` 0.4 are rig constants mirroring the recogniser's serialized defaults (:31, :27), the same for both sides, printed in every report, provisional (*Tuning record*). *Why:* `Parse` has no gate (`VoxrCommandParser.cs` :455-462); the shipped line is the recogniser's. *Rejected:* the staged parser's private `DefaultMinScore` (:181) (a parser-side ref could move the gate, scoring the sides against different lines — rule 10); no gate (sub-threshold results fire, inflating false fires); an operator flag (per-run tuning).

- **The fire — the first gate-passing result in `Parse`'s order; any further one is an extra fire.** The primary decides the class; extras are counted per case and per side, never a class, never netted. *Why:* `Parse`'s order is its selection (precedent: `VoxrBatchTestRunner` :161-182); ruling 3 wants unwanted fires seen. *Rejected:* searching for the expected intent (credits a hit ranked below a wrong command); a sixth class (fork 1).

- **Parser configuration — exactly `Program.cs` :298's (decision 3).** `new VoxrCommandParser(Slots(), Commands(), VoxrCommandParser.DefaultCoverageWeight, FollowUpWords())`, no `registeredSlots`, built by `Program` and handed to the joined mode. *Why:* with `registeredSlots` a missed registered slot costs nothing because a resolver will fill it (`VoxrCommandParser.cs` :512-515) — recogniser layer, phase 2; every earlier A/B replay used :298. *Rejected:* DocCheck's registered set (`DocCheck.cs` :119-128), which pins resolver-path doc claims (issue #161).

- **Slot matching — by name, ordinal value, order-insensitive; an extra slot is wrong (decision 4).** An expected `{name, value}` matches an actual slot of that name with an ordinally equal value (canonical: aliases resolved, number words as spoken). Credit = matched ÷ expected, `n/a` when none expected. Correct fire = right intent, all expected matched, no actual slot outside the expected names; else right intent, wrong slot(s). *Why:* F5's "matched exactly"; slot order follows the pattern, not meaning; an unspoken slot is an invented argument (ruling 3). *Rejected:* `CheckSlots`' subset rule (:303-329: an invented slot passes); position-sensitive compare.

- **Two or more finals on a case not marked recogniser-layer — each parsed alone, pooled in final order (§10 item 6).** Fire candidates are the gate-passing results of all finals, first final first; the fire rule above applies; the record notes the final count. *Why:* rule 3 sends *each final* to `Parse`; a decoder change that newly splits an utterance must show, as a miss or wrong slot. *Rejected:* joining the finals (the recogniser's buffer window, phase 2); stopping the run (blocks the decoder-side runs that cause splits).

- **Grammar-pin guard — automated, per staged ref, every run (decision 5, F11).** Before scoring, each ref's `GenerateGrammarJson(Slots(), Commands(), FollowUpWords())` — what `--grammar` prints (:156-162) — must equal the pin's `grammar` as an ordered array; else exit 2 naming the ref and first differing entry. `--grammar` is unchanged. *Why:* rule 8 keeps the pin check as the guard and `Program.cs` :6-9 requires it before any delta is trusted, yet nothing runs it; a ref that changes grammar emission changes the decoder's input, varying both sides (rule 10). *Rejected:* manual compare only (a drifted hand copy scores silently); per-corpus grammars (rule 8 defers them).

- **Home — `coverage-in-selection/ab-rig/` stays the one rig; `fidelity-miss-cost/ab-rig/` is removed (decision 6, §10 item 2).** The joined files go beside the rig's own; the old copy's five tracked files go by a PM `git rm` (Phase 4) and its on-disk `Rig.csproj` (ignored, `.gitignore` :15) is deleted with them; history docs citing it stay. *Why:* the superset, run by the recipe's DocCheck, whose command line F11 keeps; the locked design, ADRs and recon cite this path; a second runnable rig without the joined mode is a drift trap. *Rejected:* a neutral new home (edits the DocCheck line, orphans citations in locked docs); a third copy; keeping the old copy.

- **Staging — the joined files join `stage.sh`'s list; `Program.cs` dispatches `--joined`.** `stage.sh` :41-42 gains the files; its path handling (why the recipe prefixes `env -u MSYS_NO_PATHCONV`) is untouched. `Program.cs` gains one dispatch handing `Joined.Run` the args, the :298 parser and the grammar JSON. Recipe :232's "`stage.sh` is not edited" becomes "`stage.sh`'s path handling is not edited". *Why:* the rig's convention is flag dispatch (`Rig.csproj` :9-18); DocCheck then compiles the joined files every run. *Rejected:* `joined.sh` copying them after `stage.sh` under a `StartupObject` override (against that convention; reflection to reach `Program`'s private grammar); everything inside `Program.cs`.

- **Run script — `ab-rig/joined.sh`, dumps and refs in, report out (§10 item 3).** Two shapes only: `--vary parser --dump D --ref-a R --ref-b R'` and `--vary decoder --dump-a D --dump-b D' --ref R`; optional `--manifest` (default `Tests~/Fixtures/audio/manifest.json`), `--pin` (default `NativeBridge~/harness/expectations.json`), `--evaluate`, `--out` (default `.scratch/joined/`). The operator makes dumps with the harness Command 3 built. *Why:* adr-0004 leaves it here; two shapes make varying both sides unexpressible (rule 10). *Rejected:* also running the harness (duplicates the binding's toolchain, cannot know side B's build); a recipe section (the recipe is what `compile-check` runs — an on-demand instrument there drifts into the gate, against adr-0004); the procedure lives in the script header and here.

- **Hosts — both halves Windows-native in Git Bash (§10 item 4, F9).** The harness is Command 3's MSVC `windows-x64` build; the rig runs on native .NET SDK 10 (`net10.0`); `joined.sh` is bash. *Why:* `windows-native-toolchain` rule 1 removed WSL; adr-0006 leaves the choice here; DocCheck already runs natively. *Rejected:* any other host — none is provisioned.

- **Provenance — a required top-level `provenance` string in the corpus manifest (§10 item 5).** Printed verbatim in every report; the TTS manifest gains one naming voice, generator and that it cannot rank decoders (C12). *Why:* it is the corpus's property; R5 and R4 carry their own (design §7). *Rejected:* `expectations.json` (never hand-edited); an operator flag (two runs could describe one corpus differently); `voice` (a human corpus has none).

- **Marks — per-case `"sealed": true` and `"layer": "recogniser"`, read strictly (F7, F8).** Absent = unsealed, parser layer. The rig knows every key the manifest carries today plus `sealed`, `layer`, `provenance`; any other key, a non-boolean `sealed` or a `layer` other than `parser`/`recogniser` stops the run naming case and key. The split case (`manifest.json` :165-181) is tagged. *Why:* §6 ("an unknown mark … stops the run"); a lenient reader would score a misspelt `seald` holdout (rule 9). *Rejected:* ignoring unknown keys; marks in a side file.

- **Scored records per side, compared by the report, no combined score.** Each (dump, ref) writes `scored-<side>.jsonl`, the run-output format gated items consume (design §0 In 3). *Why:* adr-0003; two refs share type names, so cannot share a process. *Rejected:* one weighted total (adr-0003's rejected branch).

## Components & responsibilities

- **Dump writer** — `NativeBridge~/harness/main.cpp`, anonymous namespace: `--dump` at :105-112 and the usage (:111); header after validation; `case` before each session (:213); each payload `DrainResults` pops written as `result` before its handling, with the push counters `PushAll` (:66-92) and the case loop (:218-222) keep; `case-end` after :222; `end` after the loop.
- **`Joined.cs`** (new, ab-rig) — `Joined.Run(args, parser, grammarJson)`: `score` and `report`, exit codes, guard, kill check.
- **`JoinedInput.cs`** (new) — strict dump, manifest and pin readers on in-box `System.Text.Json`; case join by WAV basename.
- **`JoinedScorer.cs`** (new) — gate constants; `VoxrWord` from `result[]` (`Runtime/VoxrResult.cs` :9-25); `Parse(text, words)` (:2484); fire, class, credit; scored records.
- **`JoinedReport.cs`** (new) — two scored files to the report.
- **Edited:** `Program.cs` (one dispatch), `stage.sh` (:41-42 list), `Tests~/Fixtures/audio/manifest.json` (`provenance`, split `layer`), the recipe (:232 clause). **New:** `joined.sh`. **Removed:** `Planning~/features/fidelity-miss-cost/ab-rig/` (tracked: Check.cs, Program.cs, UnityStub.cs, ablate.py, stage.sh; on disk: Rig.csproj). **Generated, never committed:** everything under `.scratch/joined/`.

## Data contract & runtime flow

**Dump records** (counts in input samples):

- `{"kind":"header","schema":1,"manifest","fixtures","model","gain","libvosk","grammar":[…],"ringCapacity"}` — paths as given; `gain`, `libvosk`, `grammar` from the harness manifest.
- `{"kind":"case","case":"<file>","samples","sampleRate"}` — rate from `WavData::sample_rate`.
- `{"kind":"result","case","seq","final":bool,"pushed","prevPushed","payload":{…}}` — the bridge JSON re-emitted (`text` and `result[]`, `partial`, or `error`); an unparseable one as `"raw":"…"`.
- `{"kind":"case-end","case","results","pushed","errors":[…]}`; `{"kind":"end","cases","passed"}`.

**Corpus manifest:** top level `voice` (optional), `provenance` (required), `cases`; per case today's keys (`file`, `category`, `phrase`, `segments`, `gapSeconds`, `silenceSeconds`, `peak`, `expectedIntent`, `expectedSlots`, `expectedTranscript`, `description`) plus `sealed`, `layer`. Empty `expectedIntent` is a negative. `JsonUtility` and `generate.py` read only keys they know (Commands 1–2 prove it).

**One joined run**, `joined.sh` from the project root in Git Bash:

1. Arguments checked against the two shapes, inputs present; else exit 2 naming the first problem.
2. Each distinct ref: `env -u MSYS_NO_PATHCONV bash …/stage.sh <ref> <out>/stage-<a|b>` then `(cd <stage> && dotnet build -v q --nologo)`; a failure stops naming ref and step.
3. Per (dump, ref): `dotnet run --no-build -- --joined score --dump … --manifest … --pin … [--pinned] [--evaluate] --out scored-<side>.jsonl` — validate all inputs, the guard, the kill check if `--pinned` (exit 3), then per case: sealed without `--evaluate` → skipped; `layer: recogniser` → not scored at this layer, finals listed; else scored. Parser-side: one dump, each ref, both pinned. Decoder-side: dump A pinned, dump B unpinned, one ref.
4. `--joined report --a … --b …` writes `report.md` and prints it.

Paths go to `dotnet` in drive-letter form (`cygpath -m` where present, as `generate.sh` does), so `MSYS_NO_PATHCONV` set or unset makes no difference.

**Scored record:** header (side, `--vary`, `STAGED_FROM`, dump path and header configuration, manifest, provenance, gate, `evaluate`); per case `case`, `disposition` (`scored`, `recogniser-layer`, `sealed`), `finals`, and if scored `class`, `intent`, `slots`, `score`, `slotCredit`, `extraFires`, `finalCount`. Classes: `correct-fire`, `correct-no-fire` (fork 1), `wrong-slots`, `wrong-intent`, `missed`, `false-fire`.

**Report:** corpus (manifest, case count); provenance verbatim; side varied and both identities (each `STAGED_FROM`, or each dump with every header field where B differs); the pinned side's kill-check result; the gate; `HOLDOUT EVALUATION` under `--evaluate`. Per side, each class count, extra fires, not-scored and sealed-skipped counts, side by side, no total. Every flip — transcripts, disposition, class, intent, slots, credit or extra fires differing between A and B — with old and new transcript and outcome. A decoder-side report lists every case's A and B transcripts (F4).

### Tuning record (F10)

Every number the rig uses, each **provisional → the tuning pass** (Phase 4), which writes its evidence here:

| Number | Value | Evidence |
|---|---|---|
| `MinScore` | 0.6 | Mirrors `VoxrCommandRecogniser` `minScore` default (:31) and `VoxrCommandParser.DefaultMinScore` (:181); `report.py`/`ablate.py` use 0.6. Tuning pass: pending. |
| `MinConfidence` | 0.4 | Mirrors `VoxrCommandRecogniser` `minConfidence` default (:27). Tuning pass: pending. |

No class weight; slot credit is the bare fraction; the kill check has no tolerance; no combined score is offered.

## Non-functional realization

- **Input boundary.** Readers finish before guard, check or scorer: a non-object line, unknown `kind`, missing field, `seq` gap, case without `case-end`, no `end`, `schema` ≠ 1 → exit 2 with file and line; a dump case not in the manifest, a manifest case not in the dump, pin and pinned-dump case sets unequal, a shared basename → exit 2 naming the case; an unknown key, bad `sealed`/`layer`, no `provenance` → exit 2 naming case and key. A `raw` or `error` record or case errors mean a failed decode: exit 3 on the pinned side, exit 2 on side B.
- **Degraded dependencies.** `joined.sh` runs under `set -euo pipefail`; an unshowable ref, a failed build (a ref older than an API the rig compiles), a missing dump or manifest each stop naming ref, step or path, with no report. A harness that cannot open its dump path exits 2 before decoding.
- **Standing check unchanged by construction.** Without `--dump` nothing new runs; with it the writer only observes payloads `DrainResults` already pops. Phase 1 diffs the two stdout reports.
- **One side per run, structurally:** two shapes; the guard catches a parser ref that moves the grammar. **Leakage only by intent:** no sealed outcome is computed without `--evaluate`.

## Build plan

Lowest-risk first and in dependency order: Phase 1 is a few harness lines, shown feasible by probe 1, and every later phase reads its dump; Phase 2 builds readers and guards on that real dump before anything scores; Phase 3 adds the outcome rules; Phase 4 tunes and closes. Every phase runs the binding's Commands 1–2; each rig phase also runs the recipe's DocCheck (exit 0, `<n>/<n>`).

- **Phase 1 — the harness dump.** The writer and `--dump`. Green: Command 3 verbatim (both halves; 16/16, exit 0; no `.so`); the built harness re-run from `NativeBridge~` with Command 3's arguments plus `--dump ../.scratch/joined/p1.jsonl`, stdout identical, exit 0; the dump read for header, 16 framed cases, partials, counters on every record, trailer; a second dump compared (*Risks*). *Discharges:* F1, §10 item 1.
- **Phase 2 — inputs and guards.** First the PM's `Rig.csproj` check (*Risks*). `JoinedInput.cs`; guard and kill check in `Joined.cs`; `score` listing dispositions and transcripts; the `Program.cs` dispatch, `stage.sh` list, `joined.sh`; the manifest's `provenance` and split tag; the recipe clause. Green: Commands 1–2; DocCheck; a `main`-vs-`WORKTREE` run on a fresh dump passes guard and kill check; F2's canary — a `.scratch/` dump copy with one final altered — exits 3 naming the case; `.scratch/` negatives (malformed line, extra case, missing case, unknown key, missing dump, unshowable ref, a pin copy with one grammar entry altered) each stop naming their cause. *Discharges:* F2, F8's tag, F11's guard, the NFRs, §10 items 2, 3, 5.
- **Phase 3 — scorer and report.** `JoinedScorer.cs`, `JoinedReport.cs`, `report`, sealed and `--evaluate`. Green: DocCheck; F3 `main` vs `main`, 0 flips; F4 with a `.scratch/` side-B copy holding one changed final, listed, not refused; F7 on a `.scratch/` manifest copy with one case sealed, without and with `--evaluate`; F8's split case in the not-scored row; F5's canary — the writer seeds a regression in one worktree parser source and runs `main` vs `WORKTREE`, the report kept in `.scratch/joined/` for the maintainer's read (F6); the PM restores the file with `git checkout --`; the next dispatch shows `git status --short -- Runtime/` empty, then Commands 1–2. *Discharges:* F3–F8, §10 item 6.
- **Phase 4 — tuning pass and closing gate.** The tuning pass runs the rig on the 16 fixtures and writes each number's evidence into the *Tuning record*; the PM runs `git rm -r Planning~/features/fidelity-miss-cost/ab-rig` and deletes the leftover `Rig.csproj`; every acceptance run re-made from Claude's Git Bash, `joined.sh` with `MSYS_NO_PATHCONV` set and unset; Command 3 and DocCheck; the predecessor feature's F7 sweep (`windows-native-dotnet-fixtures`, *The sweep*) run over the new and edited files prints nothing. *Discharges:* F9, F10, F11 at G2, §2 In 8, §10 item 2.
- **Assets code cannot author:** none new — the provisioned `NativeBridge~/vendor/` (if absent, a human `!` download per `NativeBridge~/harness/README.md`), Command 3's toolchain, .NET SDK 10.0.300, Unity 6000.4.7f1 and the host. Unity runs generate `memory/**/*.meta`, which the PM commits, never deletes.
- Phase 1 — persisted plan (2026-10-14): [plan-1.md](plan-1.md)

## Risks, tradeoffs & open questions

- **Unrecorded behaviour, proved at each phase's first run** (a difference changes the text before the phase closes). Phase 1: partials are `{"partial": "…"}`; word times run on after `vosk_recognizer_reset` (`vosk_bridge.cpp` :307) rather than restarting at zero, and the origin is not recoverable exactly from the dump; two dumps of one build hold the same partial sequence (finals are fixed by the verdict, partials are not); the dump moves no verdict. Phase 2: the rig's grammar equals the pin's today — if not, the hand copy has drifted, a stop for the human, never a re-pin; `JsonUtility` ignores the new keys; no NuGet download. Phase 3: `Parse` returns its selection order, best first; the rig reaches the internal `HasUnfilledRequiredSlot`.
- **Is `ab-rig/Rig.csproj` tracked?** `stage.sh` copies it, `.gitignore` :15 ignores `*.csproj`, and the old copy's tracked list has none. If `git ls-files` prints nothing for it, a fresh clone cannot stage the rig: the PM checks before Phase 2 and, if so, force-adds it (`git add -f`) in Phase 2's commit.
- **Partials are coarsely timed.** Phase 2 gets exact order, and final-word times exact within a case only; partial times only by the bracket or by inference from the next final; more, per-case origin included, needs a bridge change in that feature, with its own ruling and arm64 cost.
- **Drift watch.** The gate mirrors recogniser defaults the rig cannot see: moving either moves the *Tuning record*. The guard checks the hand-copied grammar against the pin, not `Tests~/Runtime/DemoGrammar.cs`. Refs older than an API the rig compiles fail to build. A new manifest key stops the rig until taught. TTS-only evidence cannot rank decoders (C12).
- **Readings, ruled by the maintainer in session on 2026-10-14.** F1's "a dump from that run": Command 3 writes none, so the dump comes from its build re-run with `--dump`, stdout diffed — accepted ("Accept both"). F1's "audio-time stamps" on partials are the bracket — ruled "(a) Bracket now": exact partial timing is `joined-rig-recogniser`'s, with its own ruling, re-dumping as it needs (STATUS deferral JRP-1). Recipe :232 cites `windows-native-toolchain` §1 for "`stage.sh` is not edited"; that row described its own migration, and only the copy list changes here — accepted. The four-phase build plan was approved as stated.
- **Open questions:** none blocking.

## As-built deltas

<!--
**The `## As-built deltas` charter.** This section is about the plan, and the plan lives in the plan files this document links: four kinds of entry are **admitted** here, two are **amended into `## Decision register`** in place with a dated line instead, a fact the landed tree has made false in an always-current section is **corrected in place** where it stands, and every other kind is **routed** to a home that already receives it — the session's dated note, `memory/<YYYY-MM-DD>.md`; the cross-session state the `board` binding names, `memory/STATUS.md` where none is bound; or the lessons file, `memory/notes-for-future-features.md`. No new home is built for what is routed out.

**Admitted**, each a statement about the plan: (1) divergence between the persisted plan and the tree as built, with the ruling that settled it; (2) "built as planned" confirmations, which are a real result; (3) citation re-pointing, plan-time line numbers read against the landed tree; (4) post-G2 addenda from unrelated later work, carrying their date.

**Amended into `## Decision register`**, in place and carrying the amendment's date, in the form `**Amended YYYY-MM-DD:**` appended to the decision it extends: a ruling that extends a decision this document already carries, and a trap or do-not-undo notice for a later maintainer. Both are design rationale, and the register is the one section carried forward into the product surface the `product-docs` binding names; filed here instead, a decision would reach that surface without the category later added to it.

**Corrected in place**, by the reconcile pass: a fact the landed tree has made false in an always-current section — `What this implements`, `Context & scope`, `Components & responsibilities`, `Data contract & runtime flow`, `Non-functional realization`, `Risks, tradeoffs & open questions` — is corrected where it stands, and each correction is logged in that phase's entry here, with what the passage said and what it says now, as citation re-pointing is. A change of decision is never a correction: it goes to a dated `## Decision register` amendment. Dated passages — plan files, the entries here, `**Amended YYYY-MM-DD:**` clauses — stay records and are not corrected.

**Routed out**, each to a home that already receives it: gate evidence (commit shas, test counts, exit codes, live-run transcripts), review-cycle records, and authorship or provenance confessions about the document itself go to the session's dated note, `memory/<YYYY-MM-DD>.md`; deferrals and named limitations go to the cross-session state the `board` binding names — `memory/STATUS.md` under *Deferred*, one line each, where none is bound; process and methodology lessons aimed at future features go to `memory/notes-for-future-features.md`.

**Frontmatter `updated:`** — the reconcile pass also moves the doc's frontmatter `updated:` to its own date: bookkeeping, never logged as a correction, never a change of decision.

**A persisted phase plan is never edited** — it is the record of what its planner knew on its own date, which is why re-pointing its citations is an admitted kind here rather than a correction there.
-->

## Related

The frontmatter's sources, plus: design `joined-rig.md` §0, §1, §3, §4, §7 · `Planning~/design-docs/windows-native-toolchain.md` §1 · `Planning~/features/windows-native-dotnet-fixtures/architecture.md` (the DocCheck line) · `NativeBridge~/harness/README.md` · feature `joined-rig-recogniser` (phase 2, reads the dump)
