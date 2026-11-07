# Phase 2 — persisted plan (2026-11-06)

Scope: the row as written — `generate.py`'s two fields, 15 `pause-lead` entries and WAVs, their harness-written pins (the 37 unmoved), the Tier B skip, the rig's keys and F13 block (DocCheck, A-against-A), the side A dump, `doc-writer`'s Command 3 count; unsplit, serial, this tree. MAN = `Tests~/Fixtures/audio/manifest.json`, PIN = `NativeBridge~/harness/expectations.json`, GEN = `Tests~/Fixtures/generate.py`, R = `Planning~/features/coverage-in-selection/ab-rig/`, S = `.scratch/agc-filter/`, EP = `Planning~/features/endpointer-profile/architecture.md`; lines are today's.

## Files to change

| Path | Change | Owner | Why |
|---|---|---|---|
| GEN | modify | `code-writer` | F12 |
| MAN | modify, +15 at the end | `code-writer` | F12 |
| `Tests~/Fixtures/audio/tts/pause_lead_*.wav` | create 15, generated | `code-writer` | F12 |
| PIN | modify, +15, harness-written | `code-writer` | F12, F14 |
| `Tests~/Runtime/VoxrWavReplayTests.cs` | modify :131 | `code-writer` | *Fixtures* |
| R`JoinedInput.cs`, R`JoinedReport.cs` | modify | `code-writer` | F13; *Rig → dumps* |
| S | create, untracked (`.scratch/` ignored) | `code-writer` | F13 side A |
| `.claude/bindings/unity.md` :8; `Planning~/verification-recipe.md` :21 | modify, count only | `doc-writer` | row |

## Contracts and interfaces

**Dispatch.** `code-writer` Steps 1–10 in order, Git Bash; `doc-writer` Step 11; then the gate. No agent commits. Edited files keep their endings (all CRLF; hand-added PIN entries too): lone-LF count 0 by bytes.

**Step 1 — baseline.** Command 3 verbatim, before any edit: exit 0, `"total": 37`, `"passed": 37`. Builds the harness Steps 6, 7, 10 use; Step 6's stop premise. Not green: BLOCKED, nothing edited.

**Step 2 — GEN.** :99's lead = `case.get("leadSilenceSeconds", LEAD_SILENCE_S)`. Then, if `noiseFloorDbfs` is set: `np.random.default_rng(zlib.crc32(<basename>.encode("utf-8"))).standard_normal(len(x))` rescaled to RMS exactly `10 ** (dBFS / 20)` on ±1.0 (−60 → 0.001), added over the whole fixture, float32. Speech is peak-scaled first, as today (:87-88, :94-95); the noisy peak must lie in [`MIN_PEAK`, `MAX_PEAK`], else `sys.exit` naming file and peak. A silence entry (:96-97) with either key exits naming the file. Header (:8-14) names both fields; the seed makes the noise reproducible, piper's speech stays not byte-identical.

**Step 3 — MAN.** After `silence_negative`, in the sources' order (non-`ladder` minus `silence_negative`). `<b>` = `pause_lead_<source basename>`: MAN `file` `audio/tts/<b>.wav` (:6), PIN's `<b>.wav` (PIN :8). Keys in order: `file`, `category: "pause-lead"`, `layer: "recogniser"` (split only), the source's `phrase` or `segments` + `gapSeconds`, its `peak` (all 15 have one), `leadSilenceSeconds: 2.0`, `noiseFloorDbfs: -60`, `firstWord`, the source's `expectedIntent`, `expectedSlots`, `expectedTranscript: ""`, an ASCII one-line `description` naming the source. `firstWord` in order: launch, cease, resume, approach, fall, switch, navigation, enable, disable, fire, launch, set, switch, launch (filler, after "okay so"), close (split, first segment).

**Step 4 — WAVs.** Grep: no existing `file` holds `pause_lead_`. `bash "Tests~/Fixtures/generate.sh" --only pause_lead_` (venv, voice cached; no download). Check: `15 fixture(s) written`, peaks in range; `git status --short -- "Tests~/Fixtures/audio/tts"` lists exactly the 15, new; the venv's python reads each one's first 1.9 s at −60 ± 0.5 dBFS RMS.

**Step 5 — Tier B.** `ManifestFileNames` (:128-133) also drops `pause-lead`; comment: until Phase 5 replay runs the AGC being retired.

**Step 6 — pin.** 15 `{ "file": "<b>.wav" }` after PIN's last case, MAN's order, no `expectedFinals` (EP :75). From `NativeBridge~`: Command 3's harness invocation + `--write-baseline` (README :95-98; no rebuild). Check: exit 0, `"baselineWritten": true`; `git diff --ignore-cr-at-eol -- PIN` (content, not endings): only the 15, each with `expectedFinals`; the 37, `gain`, `grammar`, `libvosk`, `modelConf`, `_comment` unchanged; lone-LF 0. A `pause-lead` final is pinned as heard: side A's measure. **Stop:** anything else moved → PIN as written, uncommitted, BLOCKED to the hub for the human (README :100); never a hand edit or second re-pin.

**Step 7 — Command 3** verbatim: exit 0, `"passed"` = `"total"`; total recorded as read.

**Step 8 — rig.** R`JoinedInput.cs`: `ManifestCaseKeys` (:134-151) + `leadSilenceSeconds`, `noiseFloorDbfs`, `firstWord`; `ManifestCase` (:99-107) + `Category` (`""` absent), `FirstWord` (null absent); `ReadManifest` stores both, checking `category` string, the two fields numbers, `firstWord` non-empty without whitespace on and only on `pause-lead` cases — else `Stop(2)` naming case and key; unknown keys still stop (:514-515). R`JoinedReport.cs`: `Run`, after `Compare(a, b)` (:155), reads the headers' manifest via `JoinedInput.ReadManifest`, mapped by `Name`; a scored case missing from it: `Stop(2)`. If any case is `pause-lead`, `Render` emits after `## Counts` (:651-663), before `## Cases`, at both layers, `## F13 (non-ladder cases)`: `| | A | B |`, rows `correct fires`, `wrong-intent`, `false-fire`: scored non-`ladder` records of `class` `correct-fire`, `wrong-intent`, `false-fire` (:953-969), by new `F13Counts`, `Counts` (:939) untouched. `first-word retention` `k/n` (F13): n = every `pause-lead` record, any disposition (15 per layer), one set on A and B; k = those whose `SpaceJoined` (:874) first token equals `FirstWord`, ordinal; empty or absent `finals`: in n, not k. Else the report is unchanged. `Joined.cs` untouched.

**Step 9 — DocCheck** (recipe :229) verbatim: exit 0, no `FAIL`, `<n>/<n>` equal; n recorded.

**Step 10 — side A; A-against-A.** From `NativeBridge~`: Command 3's harness invocation + `--dump ../.scratch/agc-filter/a.jsonl`: exit 0, Step 7's total; `sha256sum` recorded. From the root, `bash R/joined.sh --vary decoder --dump-a S/a.jsonl --dump-b S/a.jsonl --ref WORKTREE --out S/aa-parser`, again `--layer recogniser --out S/aa-recogniser`: exit 0, `Kill check: A passed`, `Flips: 0`, F13 section with A = B per row; rows recorded. Negatives, parser layer, `--manifest` a MAN copy in S: the 15 as `category: "clean"` without `firstWord` → exit 0, no F13 section; a key `bogus` on one case → exit 2 naming both.

**Step 11 — counts (`doc-writer`).** unity.md :8, recipe :21: "37/37" → Step 7's total, both numbers; nothing else. `plan-1.md` never edited.

**Gate.** After Step 11: Commands 1–2 (one bracket), Command 3 as edited, green per the binding; EditMode corpus tests cover every MAN entry; PlayMode `Replay_ManifestCase` keeps the 16 non-`ladder` cases.

**As-built record.** Side A: its path, the Phase 2 build commit (it holds every dump input; Step 11 touches none), `sha256sum`, Step 7's total.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| Append at MAN, PIN ends | Beside sources | 37 keep places; diff additions only |
| `--only pause_lead_` | Regenerate all | piper not bit-deterministic (GEN :12-14) |
| Seed = basename CRC-32 | Global, none | per-file, order-free, reproducible |
| RMS rescaled | Nominal σ | each floor exactly −60 dBFS |
| `expectedTranscript: ""` | Source's | transcripts are the pin's (F12, F14) |
| First-token retention | String prefix | "launch" ≠ "launched" |
| Report reads MAN | Category in scored files | no format change; the seam |
| Step 1 before edits | Build only | a Step 6 move is this change's |

## Acceptance criteria

1. [ ] MAN holds the 15 per Step 3; basenames unique. (F12)
2. [ ] Step 4's checks hold. (F12)
3. [ ] Step 6's check: the 15 only, harness-written. (F12, F14)
4. [ ] Step 7: exit 0, passed = total. (F12)
5. [ ] Tier B skips `pause-lead`, comment present. (*Fixtures*)
6. [ ] Step 8 as stated; Step 9 green. (F13)
7. [ ] Step 10: kill check passed; `Flips: 0` both layers; F13 A = B; negatives as stated. (F13 measure, side A)
8. [ ] Both count sites read Step 7's total. (row)
9. [ ] Gate green. (row)
10. [ ] `git status --short -uall`: ` M` the table's other paths, `??` the 15 WAVs, else only `Planning~/features/agc-filter/`; lone-LF 0. (row)

## Open questions that block implementation

None block Steps 1–11. For the human before Phase 4 reads retention: the filler's committed final begins `[unk]` (PIN :84), so under the register's words the filler case, and any case the noise prefixes with `[unk]`, is not retained on either side. The plan builds the register's words; skipping leading `[unk]` is a ruling, one line in Step 8.

## Blast radius

- R is shared: every joined run reads MAN's new keys; an older dump (`.scratch/endpointer-profile-phase-5/a.jsonl`) no longer joins the default MAN (`Join` :642-685).
- Joined runs on this MAN gain the F13 section; `Planning~/features/joined-rig-parser/architecture.md`'s report text omits it, not edited.
- Command 3 and EditMode run 15 more fixtures.
- `Runtime/Testing/VoxrAudioTestCase.cs` :20 tooltip lists six categories; shipped, not edited.
- Phase 4 reads `a.jsonl`; Phase 5 adds `pause-lead` to Tier B.
- README :85, :91, :93 accurate, unchanged. PIN `_comment` :2: its "filler/split/silence" empty-transcript list omits `pause-lead`, stale, not hand-edited (F14), reported.

## Runtime claims

- 2, 4: numpy draws repeat per seed; noisy peaks stay in [0.04, 0.4]; `generate.sh` needs no download, writes no other WAV; `read_text()` still reads MAN.
- 6: entries without `expectedFinals` accepted (`main.cpp` :229-240); `--write-baseline` exits 0, writes sorted `dump(4)` in CRLF (text-mode `std::ofstream`, :387); the 37 and `modelConf` reproduce.
- 7: a normal run reproduces every pin.
- 8, 10: `ReadManifest` opens the header's drive-letter path (`joined.sh` :162) from stage-a; one dump scored twice is identical; the recogniser stage builds here.
- 5: the filter drops `pause-lead` from discovery, `ValueSource` intact.

Hazards: Step 1 precedes every edit; no harness run between Steps 4 and 6 (drift guard, `main.cpp` :245-261, exit 2); Step 6 precedes 7 and 10 (`Join`'s equal sets); Step 8 precedes 9, 10; Step 7 precedes 11, 11 the gate; a Step 6 stop halts all later steps.
