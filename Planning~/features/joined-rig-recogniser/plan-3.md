# Phase 3 — persisted plan (2026-10-15)

Serial: F2-F9, §6, F11; bound by §2 Out 1, the *Staging* trap. AB = `Planning~/features/coverage-in-selection/ab-rig/`; J = `.scratch/joined/`; lines at `8f85679`.

## Files to change

| Path | Change | Why |
|---|---|---|
| AB `JoinedInput.cs`, `JoinedScorer.cs` | modify | F6, F8 |
| AB `JoinedRecogniser.cs` | modify | F3-F8 |
| AB `Joined.cs`, `JoinedReport.cs` | modify | F2, F7-F9, §6 |
| AB `joined.sh`; `Rig.csproj` | modify; comment only | F9, F11 |
| `plan-3.md`; `architecture.md` | create; link line | this plan |

## Contracts and interfaces

**Dispatch.** A, verification agent: Step 0. B, `code-writer`: Steps 1-6, atomic. C, as A: criteria 2-15. No agent git write; scripts as `env -u MSYS_NO_PATHCONV bash AB/<script>`.

**Step 0.** `joined.sh --vary parser --dump J/jrr-p2-a.jsonl --ref-a main --ref-b main --out J/jrr-p3-base`: exit 0, kept, `Flips:` recorded; DocCheck, warnings counted; `git status --short`.

**Step 1 - `JoinedInput.cs`.** `DumpCase` gains `long Samples, SampleRate`, from :224-225.

**Step 2 - `JoinedScorer.cs`.** `internal static string Classify(bool hasPrimary, VoxrCommand primary, JoinedInput.ManifestCase expected, out double? slotCredit)`: steps 4-5 (:77-93) moved verbatim; `Score` calls it. So a negative case (empty expected intent) pending, unconfirmed and unfired is `correct-no-fire` (architecture *Readings*, F6). `Matched` reads null `Slots` as empty. `Outcome` gains `double[] FireTimes`, `double? Delay`, `bool Pending`, `string AppliedGrammar`, `string Trace`.

**Step 3 - `JoinedRecogniser.cs`.**
- `RunWorld` (:123-127): an empty timeline's last time is 0; pending-at-K stop (:181-188) kept.
- `Observation` gains `VoxrCommand Command`, set in `Event`.
- `Line(Observation)`: `Print`'s object as text, null `Slots` `[]`; `Print` uses it.
- `ScoreCase(c, d, slots, commands)`: `SampleRate` <= 0 or a `Raw` result: exit 2, case named. `RunWorld(c.Name, ...)` on `d.Results` in order as (`Consumed` / `SampleRate`, double; `Final`; `Payload`). Fires: `fire` events in order, the first's `Command` primary, the rest `ExtraFires`; `FinalCount` = `d.Finals.Count`; `Classify`. `FireTimes`: their `FrameTime`s. `Delay`: the first's - `Samples` / `SampleRate`, else null. `Pending`: a `pending` event, no later `confirmed`. `AppliedGrammar`: `BridgeNative.RigAppliedGrammar`. `Trace`: each `Line` plus LF (seq = dump `seq`, :252).
- `Config()`: a fresh `VoxrCommandRecogniser`'s `DeclaredOnly` instance fields marked `[SerializeField]` (`UnityStub.cs` :94), by `MetadataToken`, as (name, text), invariant: null `null`; floats `R`; bool `true`/`false`; array `[a, b]`; else `Convert.ToString`. Plus `TickRate`.
- `Register` also sets the two hooks below.

**Step 4 - `Joined.cs`.**
- Hooks beside `DriveCheck`: `ScoreRecogniser` (`Func<ManifestCase, DumpCase, VoxrSlotDefinition[], VoxrCommandDefinition[], JoinedScorer.Outcome>`), `RecogniserConfig` (`Func<(float, KeyValuePair<string, string>[])>`).
- `ParseOptions`: optional `--layer parser|recogniser`, default `parser`, else exit 2, in its wording; usage (:14, :56-57). `Score` gets `slots` from `Run` (:88).
- Below, recogniser layer only; under `parser`, `gate` stays, no `trace-*` file is deleted or written.
- The stale delete (:149-151) adds `trace-<side>.jsonl` beside `--out`. The new stops follow it (no scored file after a stop, :23): null `ScoreRecogniser`, exit 2 as drive-check (:96); after `ReadDump` (:155), schema 1, exit 2, `ReadTimed`'s text (:470-471).
- Header: `gate` (:282-285) becomes `layer` `"recogniser"`, `config` (name to text), `tickRate`. Disposition: `sealed` without `--evaluate`, else `scored`.
- Per case, the applied grammar (`ParseGrammar`, labelled) vs `pin.Grammar` by `FirstDifference`: else exit 2, `applied-grammar guard: case "<c>"`, index, entries. Record: phase 1's keys plus `fireTimes`, `delay` (or null), `pending` (bool).
- Traces written after the scored file (:366-368).

**Step 5 - `JoinedReport.cs`.**
- `layer` header: `HeaderKeys`' `gate` becomes `layer config tickRate`, typed as written; cases `sealed` or `scored` plus `fireTimes delay pending`.
- `Compare`: first, `layer` on one side only or unequal `tickRate`, exit 2; `gate` out of its loop at this layer.
- `Render`, this layer only: under `parser`, `Side varied: parser (the C# side: parser and recogniser together)`; `- Layer: recogniser`; `- Per-record positions may differ between runs by at most one decoder read.`; for `Gate:`, `- Tick rate: A <t> Hz, B <t> Hz`, `- Configuration A:`/`B:` as `name=text`. No not-scored row or section; `## Timing`: per case and side, fires (`fireTimes`, each fire's audio time, in order, `, `-joined) and delay, s `0.000`; finals; pending (`pending, timed out` when `pending` and no fire, whatever the class; `pending` with a fire). Empty or null: `-`.

**Step 6 - `joined.sh`, `Rig.csproj`.** `--layer parser|recogniser` once, default `parser`, else `die`; header (:8-35). `recogniser`: `stage()` (:130-133) passes `recogniser`, `score()` (:149-158) `--layer recogniser`; parser lines unchanged. `trace-*.jsonl` joins :114, :155. `Rig.csproj` (:13-15) names the layer option, no double dash.

**Stops (PM):** a false runtime claim, the human's: never a relaxed compare or longer horizon. A changed file outside the files table; a canary or fixture edit outside `.scratch/` (never `Runtime/`, `NativeBridge~/`, `Tests~/`, `expectations.json`, committed dumps); a drive through the *Staging* trap's four members.

**Commit set (PM):** the seven AB files.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| `pending` bool, label rendered | label string | label from `pending` and fires |
| Empty timeline from 0 | exit 2 | silence is an outcome |
| Grammar compared in `Joined` | in the hook | pin and helpers there |
| Config hook apart | one hook | header precedes cases |
| Trace written last | streamed | a stop leaves none |

## Acceptance criteria

The gate: 2-15, in order, from Claude's Git Bash (F10).

1. [ ] Step 0 recorded. (F11)
2. [ ] `stage.sh WORKTREE .scratch/jrr-p3-stage recogniser`; there 0 errors, 0 warnings; `drive-check` exit 0, `5/5`. (F3; F11)
3. [ ] DocCheck verbatim: exit 0, no `FAIL`, `<n>/<n>`; warnings as Step 0. (F11)
4. [ ] Step 0's line with `--out J/jrr-p3-f11`, then plus `--layer parser`, `--out J/jrr-p3-f11p`: exit 0; Step 0's `Flips:` is 0; scored files and `report.md` `cmp`-equal to Step 0's; no trace. (F11)
5. [ ] `joined.sh --layer recogniser --vary parser --dump J/jrr-p2-a.jsonl --ref-a main --ref-b main --out J/jrr-p3-f3`: exit 0, `Flips: 0`; `trace-a.jsonl`, `trace-b.jsonl` `cmp`-equal. (F3)
6. [ ] `--layer recogniser --vary decoder --dump-a J/jrr-p2-a.jsonl --dump-b J/jrr-p2-b.jsonl --ref main --out J/jrr-p3-f2`: exit 0, `Flips: 0`, the one-read line. (F2)
7. [ ] `python3 -I`, criterion 5's `trace-a.jsonl`: the split case's `result` lines match its dump records, seq 0..n-1, `audioTime` = `consumed` / `sampleRate`, `frameTime` >= `audioTime`. (F4)
8. [ ] Criterion 5: the split case `scored`, a class both sides; no not-scored section. (F5)
9. [ ] `J/jrr-p3-f6`: `stage.sh main <dir>/stage-a recogniser`, then `stage-b`; in stage-b's `VoxrCommandRecogniser.cs`, `[SerializeField] float minScore = 0.6f;` gets `1.5f` (`python3 -I`, `newline=''`); built, not re-staged; scored as `score()`, `--pinned --vary parser --layer recogniser`. B: 15 `missed`, `silence_negative.wav` `correct (no fire)`, `minScore=1.5`; each A case not `missed` flips. (F6, F9)
10. [ ] Criterion 5's report: per side 17 fields, `speechRecogniser=null` first, `disambiguateSiblingTies` last; `Tick rate: A 90 Hz, B 90 Hz`; a timing row's fires are its `fireTimes` in order, `0.000`. Scored: `delay` null iff `fireTimes` empty; `finalCount` as Step 0's. (F7, F8)
11. [ ] `J/jrr-p3-kill.jsonl`: `jrr-p2-a.jsonl`, one final's text changed (`python3 -I`, LF); criterion 5's form: exit 3, case named, no scored or trace file. (F9)
12. [ ] `J/jrr-p3-sealed.json`, the manifest with one case `"sealed": true`, criterion 5's form: exit 0, `sealed-skipped` 1; plus `--evaluate`: `HOLDOUT EVALUATION`, scored. (F9)
13. [ ] Exit 2, named, no scored file: `J/p4.jsonl` here (`schema 1`); `score --layer recogniser` in a parser stage; `--layer x`; `--layer` twice; `report` on `jrr-p3-f11/scored-a.jsonl`, `jrr-p3-f3/scored-b.jsonl`, in criterion 2's stage. First two: a file pre-placed at `--out` is gone. `J/p4.jsonl`, parser layer: exit 0. (§6; F11)
14. [ ] `stage.sh WORKTREE J/jrr-p3-grm recogniser`; its `GrammarManager.cs` :22 (edit as 9): the `GenerateGrammarJson(...)` call `G` becomes `"[\"0canary\", " + G.Substring(1)`; built, scored as 9's B: exit 2, `applied-grammar guard`, first case, index 0, `0canary`; no scored or trace file. (§6)
15. [ ] `git status --short`: nothing under `Runtime/ NativeBridge~/ Tests~/`; beyond Step 0's lines, only the seven AB files (`.scratch/` aside). (§2 Out 1)

## Open questions that block implementation

None.

## Blast radius

- `Joined*.cs` save `JoinedRecogniser.cs`: every stage, DocCheck's build; `joined.sh`: every `--out`.
- Phase 4 binds `--layer recogniser`, `TickRate`, the `Tick rate` line.

## Runtime claims

- a (5, 6, 10): the 16 fixtures raise no `Debug.LogError`, failed assert or pending at K.
- b (5): every applied grammar is the pin's.
- c (6): a one-read shift may move a delivery frame, never a class.
- d (5): one ref's two worlds trace byte-equal; no state leaks between cases.
- e (8): the split case lands in a class.
- f (9): no `Score` exceeds 1: `minScore` 1.5 leaves no fire, pending or error.
- g (4): parser-layer outputs byte-identical.
- h (7): each `jrr-p2-a.jsonl` payload raises a speech event.
- i (10): `MetadataToken` order is source order; the stub attribute is found.
- j (5): a recogniser built outside a world leaves the next unchanged.
- k (10): `Utf8JsonWriter` writes float `tickRate` 90 as `90`: `90 Hz`.
- l (10): `Convert.ToString` gives the `pendingTimeoutBehavior` enum's member name.
- m (9): stage-b holds `[SerializeField] float minScore = 0.6f;` once; the edit keeps LF.
- n (14): the edit passes `SetGrammar`'s validator; the guard stops at the first case, index 0.
