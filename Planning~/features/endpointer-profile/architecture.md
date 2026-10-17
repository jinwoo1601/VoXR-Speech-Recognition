---
type: architecture
feature: endpointer-profile
topic: endpointer-profile
status: draft
updated: 2026-10-16
sources:
  - Planning~/features/endpointer-profile/requirements.md
  - Planning~/design-docs/endpointer-profile.md
  - Planning~/design-docs/endpointer-profile/decisions/adr-0001-runtime-overlay.md
  - Planning~/design-docs/endpointer-profile/decisions/adr-0002-default-flip-continuous-gated.md
  - Planning~/design-docs/endpointer-profile/decisions/adr-0003-named-presets.md
  - Planning~/design-docs/endpointer-profile/decisions/adr-0004-pause-ladder-evidence.md
  - Planning~/features/joined-rig-recogniser/architecture.md
  - "Runtime/{VoxrSpeechRecogniser.cs, ModelExtractor.cs, VoxrPushToTalkController.cs, VoxrListeningMode.cs, EditorMicBackend.cs, Commands/VoxrCommandRecogniser.cs}, NativeBridge~/harness/{main.cpp, expectations.json, README.md}, Planning~/features/coverage-in-selection/ab-rig/{Joined.cs, JoinedInput.cs, JoinedReport.cs, ModelExtractor.cs, UnityStub.cs, joined.sh, stage.sh}, Tests~/Fixtures/{generate.py, generate.sh, audio/manifest.json}, Tests~/Runtime/{ModelExtractorCacheTests.cs, VoxrWavReplayTests.cs, DemoGrammar.cs}, Tests~/Editor/VoxrFixtureCorpusTests.cs, Planning~/verification-recipe.md, .claude/bindings/unity.md (## verification)"
---

# feat-endpointer-profile — Architecture

Realizes `Planning~/features/endpointer-profile/requirements.md` (Track **Full**; S1–S2, F1–F12, §6, §10 items 2–4; item 1 is the tuning pass's) for design `endpointer-profile` (adr-0001 – adr-0004). A Unity-free overlay, `Runtime/EndpointerOverlay.cs`, rewrites only the endpoint trailing-silence keys of the extracted `conf/model.conf` from a pristine copy of the archive's own conf and a named preset, on every load, between the extractor's return and decoder initialisation; the preset is a serialized choice or, by default, follows the listening mode the push-to-talk controller hands the recogniser. The harness records the conf it decoded with and decodes what the package's own source writes (`confgen`); the joined rig reports the conf and both kill criteria; a 21-case pause ladder feeds the rule2 sweep that names the preset the Continuous default flips to. The governing driver is **one conf function** (rules 4, 9): the effective conf depends on the archive's conf and the preset alone, computed by one source wherever a conf is produced, so every latency number traces to the exact bytes behind it.

## What this implements

| Requirement | Work | Phase | Register |
|---|---|---|---|
| F1 | Ladder entries, WAVs, harness-written pins; Tier B skip | 1 | Pause ladder |
| F2 | `modelConf` in header and pin; the rig's conf line | 2 | Conf record; the rig |
| F3 | `confgen` compiles the package's overlay source | 4 | One source; `confgen` |
| F4 | Buffer setups; *Sweep measures*; the sweep | 3, 5 | Buffer setups; kill criteria |
| F5 | `VoxrEndpointerProfile`; the internal preset table | 4, 6 | Presets; choice surface |
| F6 | `Resolve`; the controller's mode hint | 6, 7 | Mode at load |
| F7 | Rewrite rules; the seam | 4, 6 | Seam; rewrite rules |
| F8, F9 | Pristine copy; extractor and stamp untouched | 4, 6 | Pristine source |
| F10 | Flip, harness re-pin, CHANGELOG line | 7 | Flip and re-pin |
| F11 | Human-only check on a Quest 3 | 8 | Build plan |
| F12 | One info line per load | 6 | Load-time log |
| §6 | Once per load; parse-failure path; atomic writes | 4, 6 | Rewrite rules; atomic writes |

## Context & scope

A load runs in `VoxrSpeechRecogniser.InitialiseAsync` (`Runtime/VoxrSpeechRecogniser.cs` :188): claim (:204), `await ModelExtractor.ExtractModelAsync` (:210-213), ownership re-check (:223-224), then the Editor backend (`#if UNITY_EDITOR_WIN` :226; `vosk_model_new`, `Runtime/EditorMicBackend.cs` :85) or `vosk_bridge_init` (:252). The extractor returns a validated path at :89, :115, :169 and :230 (`Runtime/ModelExtractor.cs`; `ValidateModelDirectory` :250-261); tier 1 never reads the archive (:69-91), and only the R9 stamp (:23, :420-428) writes into the model. The recogniser knows no controller (`Runtime/VoxrPushToTalkController.cs` :18 holds the reverse reference). The harness decodes any `--model` and records no conf (`NativeBridge~/harness/main.cpp` :174, :258-269); the joined rig replays dumps behind a stand-in extractor (`Planning~/features/coverage-in-selection/ab-rig/ModelExtractor.cs` :17-23).

**Owns:** `Runtime/EndpointerOverlay.cs`, `Runtime/VoxrEndpointerProfile.cs`; the field, hint, resolution, overlay call and log in the recogniser; the controller's hand-off; `NativeBridge~/harness/confgen/`; `modelConf` in the harness and pin; the ladder; the rig's conf line, pin key, buffer setups and *Sweep measures*. **Untouched:** `ModelExtractor.cs` and the stamp format; `NativeBridge~/src/`, its ABI and `Runtime/Plugins/` (§2 Out 2); the rig's stand-ins; every recogniser default (§2 Out 3); R10's keys (§2 Out 1). **Owed after G2**, by the codex reconciler: `Documentation~/getting-started.md` :63, the pages naming the field, `KNOWN_LIMITATIONS.md` (§2 Out 4).

## Decision register

- **Overlay seam — `InitialiseAsync`, after the ownership re-check, before the backend branch (F7; rule 3).** All four extractor returns, each validated (:72, :114, :141, :210), reach the call, and both decoders (:228, :252) read its result; it is synchronous, so the re-check that guards init stays valid. *Why:* one site covers both serve paths and both backends, and the extractor stays R9's. *Rejected:* the overlay inside `ModelExtractor` (the profile would cross its signature, breaking the rig's stand-in, `ab-rig/ModelExtractor.cs` :19-22, and every cache test's call); one call per backend (two sites to drift); `Task.Run` (an await reopens the ownership window for two small files).

- **Pristine source — `.voxr-archive-model.conf`, captured before the first write (F8, F9; recon Q2).** Beside the stamp the overlay keeps a byte copy of the archive's own `conf/model.conf`, taken from the extracted conf when absent and before any rewrite; only the overlay writes that conf, so an untouched cache's is the archive's. Every load computes from the copy and the preset alone. A changed archive or forced re-extraction (`KNOWN_LIMITATIONS.md` :456-459) replaces the directory (`ModelExtractor.cs` :225-229), and the copy is re-taken. Validation checks four paths only (:255-258) and the stamp is untouched, so the next launch serves the cache (F9). *Why:* tier 1 never opens the archive; reading it per launch is the Quest cost tier 1 removed (:62-68). *Rejected:* re-reading the archive per load (undoes tier 1); the archive's values in the stamp (F9); deriving from the current conf (the cache's previous state, rule 4); record comments in the conf (F7).

- **Rewrite rules (F7; §6 robustness; §10 item 4).** The keys: `--endpoint.rule2.min-trailing-silence`, `--endpoint.rule3.min-trailing-silence`, `--endpoint.rule4.min-trailing-silence`; rule1 and rule5 never (rule 2). A key line is one whose whitespace-trimmed text begins `<key>=`. A row gives each key a value or "the archive's own": a value replaces its line's text with `<key>=<value>`, terminator kept; a value the conf lacks is appended in rule order with the conf's terminator (`\r\n` if the first line ends so, else `\n`), a missing final terminator added first; "the archive's own" leaves the key as it is, present or absent (§6). Every other byte passes through, so "Model default" yields the pristine bytes. The conf **cannot be parsed** when the copy is not valid UTF-8, a key appears twice, or a key's value is not a non-negative invariant-culture decimal: the effective conf is then the copy, unchanged, and a reason goes to the warning. No `conf/model.conf`, no action. *Why:* the §6 row; byte preservation is F7's check and makes F3 a property of text, not of a float formatter. *Rejected:* parse and re-serialise (reformats lines F7 keeps); appended overrides on Kaldi's last-wins (a second line per key); Kaldi defaults for keys missing under "Model default" (not the archive's conf).

- **Atomic writes (§6).** Each write — the copy, the conf — goes to `<name>.voxr-tmp` beside its target, then `File.Replace` (a rename when the target is absent); the conf is written only when its bytes would change, so a steady-state load writes nothing. A crash leaves the old conf or the new; an IO failure is reported like a parse failure, and the decoder reads the conf on disk. *Rejected:* writing over the live conf (a truncation window); delete then move (a no-conf window that validation turns into a re-extraction).

- **Presets — public enum `VoxrEndpointerProfile`; values internal, as decimal text (F5; adr-0003).** `Automatic = 0` (the mode decides), `ModelDefault = 1`, tuned presets from `2`, appended and never renumbered (the field serializes the integer). The internal table maps each preset to a row — rule2, rule3, rule4, each a decimal string or null for "the archive's own"; "Model default" is all null. The overlay takes a row, so `confgen`'s sweep candidates and the shipped presets share one path; tuned rows, count and names come from the *Tuning record* at Phase 6. *Why:* no public floats (F5); text cannot format differently under Mono, IL2CPP and .NET 10 (F3). *Rejected:* floats or a Custom preset (adr-0003); floats in the table; a tuned member before the sweep (every shipped preset a measured point).

- **Choice surface — one serialized field (F5, F6; recon Q4).** `VoxrSpeechRecogniser` gains `[SerializeField] VoxrEndpointerProfile endpointerProfile = VoxrEndpointerProfile.Automatic` with a tooltip, beside `modelRelativePath` (:22-23); no public property. *Why:* the recogniser's load-time settings are private serialized fields (:22-34). *Rejected:* a public property (no requirement asks; additive later); a ScriptableObject (an asset for one value); a field on the controller (a scene without one still loads).

- **Mode at load — the controller hands its mode over; none means Continuous (F6; recon Q3).** The recogniser gains an internal, unserialized `ListeningModeHint`, default `Continuous`. The controller writes its `_listeningMode` (:27) there in a new `Awake`, before each call that can begin a load (`OnEnable` :154, :164; `PressTalk` :112; the setter :80; `Start` :138), on a mode or reference change (:52-99, :197), and writes `Continuous` back in `OnDestroy`. `InitialiseAsync` resolves (`EndpointerOverlay.Resolve`) before its first await, design §1's order: an explicit choice wins; `Automatic` is "Model default" in Push-to-talk and, from Phase 7, the tuned preset in Continuous. A later mode change reaches the next load (§4.3). *Why:* `Awake` precedes `OnEnable`, so a load begun in the controller's `OnEnable` carries the mode, and the recogniser still knows no controller. *Rejected:* the recogniser finding controllers (reverses the dependency; the rig's stage would need the controller and `UnityEvent`); a public mode setter; an `Initialise` argument (a developer's own call has none).

- **Load-time log — one info line per load, the package's first (F12; recon Q4).** `Debug.Log` of `[VoxrSpeechRecogniser] Endpointer profile <preset> (<explicit choice | Continuous default | Push-to-talk default>): rule2=<v> rule3=<v> rule4=<v>`, values from the effective conf, `absent` for a missing key (Kaldi's default applies); when the row was not applied the line ends `— not applied: <reason> (<conf path>)` and goes through `Debug.LogWarning`, the package's `[ClassName]` form (`VoxrCommandRecogniser.cs` :340). `EndpointerOverlay` composes it, so EditMode tests need no model. *Rejected:* a warning for the normal line (not a fault); `OnError` (:663; not an error); a verbosity flag (F12 has no off state).

- **One Unity-free source (F3; rule 9).** `Runtime/EndpointerOverlay.cs` (internal static) holds the table, `Resolve`, the rewrite, the copy, the writes and the log text, on `System.IO` and `System.Text` alone, in C# 9 against netstandard 2.1 — Command 4's surface (`Planning~/verification-recipe.md` :156-157) — so Unity, `confgen` and the rig's stage compile it unedited. *Rejected:* a C++ rewrite in the harness (equality would be a test, not a fact); Unity types in it (no compile outside Unity).

- **`confgen` — the harness's conf route (F3; §10 item 3).** `NativeBridge~/harness/confgen/`, a .NET 10 console project linking — never copying — `Runtime/EndpointerOverlay.cs`, `VoxrEndpointerProfile.cs` and `VoxrListeningMode.cs`: `confgen --model <dir> --out <dir> (--profile <name> [--mode Continuous|PushToTalk] | --rule2 <v> [--rule3 <v>] [--rule4 <v>])` copies the model to `--out` if absent, runs the recogniser's overlay on it (a second run is the cached-serve path), and prints its conf's SHA-256 and endpoint lines; the harness takes `--model <out>`. `--profile Automatic --mode Continuous` resolves through `Resolve`, so Command 3 names "the Continuous default" and survives a re-tune. Build output under `NativeBridge~/build-confgen/`, outputs under `NativeBridge~/vendor/overlaid/`, both ignored (`.gitignore` :42, :45). *Rejected:* a harness `--conf` flag (vosk reads the conf from the model directory, so C++ would build the same copy); a rig verb (couples Command 3 to the on-demand rig); committed overlaid confs (drift).

- **Conf record — `modelConf` in the dump header and the pin (F2; rule 9; §10 item 3).** Before `vosk_bridge_init` (`main.cpp` :274) the harness reads `<model>/conf/model.conf` and writes its exact text as header `modelConf`, after `model` (:263), `null` if absent; `schema` stays 2 (the rig ignores unknown header keys, `JoinedInput.cs` :9). `expectations.json` gains a top-level `modelConf`, written by `--write-baseline` (beside :355-356, :362-367) and on every other run required and compared before any decode: absent or different exits 2 naming the conf, so the standing check decodes only at its pinned conf. *Why:* full text names the conf with no hashing in C++, two presets differ by construction, and the standing baseline records what it pins (adr-0002). *Rejected:* a hash (none in the harness; opaque in a diff); the endpoint lines only (other keys unseen); a preset name (only bytes are truthful); `schema` 3 (every reader edited).

- **The rig — reads the conf, stands in nothing new (F2, F3; recon Q6, scope flag 1).** The rig replays dumps, so its conf is the dump's `modelConf`, already copied into scored headers (`Joined.cs` :307-311). The report header gains `Model conf A|B: sha256 <12 hex>; <its --endpoint.rule lines>` or `not recorded`; the strict pin reader (`JoinedInput.cs` :151, :587-592) gains `modelConf`, which the kill check (`Joined.cs` :214-266) compares when pinned. The stand-in extractor is unchanged: the staged recogniser's overlay meets its relative path, finds no conf, writes nothing and warns into the stub's no-op `LogWarning` (`UnityStub.cs` :70). From Phase 6 `stage.sh`'s closure (:39-58) adds the overlay, profile and listening-mode sources, each when the ref has it, so pre-feature refs still stage. *Why:* the stand-ins are the device boundary (`joined-rig-recogniser`, *Staging*). *Rejected:* an overlay member on the stub (pre-feature refs could not compile against it).

- **Buffer setups — a staged-copy edit (F4; adr-0004; recon Q6).** `joined.sh --buffer time|eager --window <s>` (recogniser layer) passes to `stage.sh … recogniser`, which edits the staged `VoxrCommandRecogniser.cs` initializers `bufferWindow = 0.5f` (:72) and, for `eager`, `eagerFlushOnCompleteMatch = false` (:79), stopping by name unless each matches once, and appends the edit to `STAGED_FROM` (:81-90); the scored `config` and the horizon follow. *Why:* the rig reads configuration off the staged instance and rejected rig-set values (`joined-rig-recogniser`, *Configuration*); its F6 canary set the precedent. *Rejected:* the internal setters (:509-510) at run time (that feature's ruling to reopen); a `Runtime/` default (§2 Out 3).

- **Kill criteria in the report (F4; design §0; fork 4).** Recogniser layer, `--vary decoder`: *Sweep measures* — per side the mean finals per utterance over scored cases; Δf, B − A; time saved S, the mean over cases `correct` on both sides of A's `delay` − B's (sample time); **KC1** at the staged window W, pass when Δf × W ≤ S; **KC2**, every case whose space-joined finals differ — any one a **STOP**. A candidate passes KC1 only when it passes in both buffer setups at W = 0.5 and 2.0 s (adr-0004: a saving measured one way is not claimed for the other). *Rejected:* S over every fired case (a split's early wrong fire is not a saving); KC2 by intent (F4 names transcript content).

- **Pause ladder — 21 split entries, pinned by the harness (F1; §4.5; §10 item 2).** Phrases in *The pause ladder*. Each entry carries `segments`, `gapSeconds`, `category: "ladder"`, `layer: "recogniser"`, the full command's `expectedIntent` and `expectedSlots`, and an empty `expectedTranscript`, as the `split` case does (`Tests~/Fixtures/audio/manifest.json` :166-183); files `audio/tts/ladder_<p|m>_<phrase>_gap<ms>ms.wav`, by `./generate.sh --only ladder_` (`generate.py` :64-81, :103-104). The harness refuses unlisted WAVs (`main.cpp` :224-243), so the 21 `{ "file": … }` entries are hand-added without `expectedFinals`; every pinned value is written by `--write-baseline` — F1's "never by hand". Tier B's replay asserts one full command per fixture (`VoxrWavReplayTests` :136-199), which a rung past the threshold breaks by design, so `ManifestFileNames` (:128-132) skips the category; the corpus tests still cover the files. Phase 1 reads each WAV's effective pause and stops for a ruling if one leaves its band (P: <0.25, 0.25–0.35, 0.35–0.5, >0.5 s; M: <0.75, <0.75, >0.75 s). *Why:* F1 names the manifest; the harness and Tier B scan one directory (`main.cpp` :232; `VoxrFixtureCorpusTests` :139-140). *Rejected:* a separate manifest or directory; ladder intents in Tier B (red by design); trimming piper's edge silence (§4.5 names the existing form). **Amended 2026-10-16:** the human ruled, after Phase 1's band check put 12 of 21 rungs out of band at both thresholds, that `generate.py` trims each ladder segment's leading and trailing edge silence before splicing — 10 ms RMS frames under -40 dBFS, after peak scaling, `category: "ladder"` only — so the effective pause ≈ `gapSeconds`: piper's own near-silence at segment edges added about 0.13–0.40 s to the spliced gap, varying run to run. This supersedes the rejection of trimming above; §4.5's gaps and the split-entry form (`segments`, `gapSeconds`) stand, and the `split` fixture's generation is unchanged.

- **Flip and re-pin (F10; adr-0002; rules 2, 10).** Phase 7 makes `Automatic` in Continuous resolve to the tuned preset, runs `confgen --profile Automatic --mode Continuous` and the harness `--write-baseline` on its output, and reviews the diff: `modelConf` changes; a moved `expectedFinals` keeps its joined words (segmentation only), else the human rules — never a re-pin (`NativeBridge~/harness/README.md` :68). `doc-writer` work items, named not made: Command 3 gains `confgen` and `--model vendor/overlaid/continuous-default`, .NET SDK 10 a prerequisite (Phase 7); the README's run line (:35, Phase 7), guards (:49-53, Phase 2), re-baselining (:61-68, Phase 7); the binding's "16/16" to the new count (Phase 1; requirements §8); the CHANGELOG line (Phase 7). *Rejected:* pinning "Model default", or both (adr-0002); the tuned preset's name in the binding (a re-tune would edit it).

## Components & responsibilities

- **Runtime** — `EndpointerOverlay` (new; table, `Resolve`, rewrite, copy, writes, log text); `VoxrEndpointerProfile` (new, public); `VoxrSpeechRecogniser` (field, hint, resolution, seam, log); `VoxrPushToTalkController` (writes the hint).
- **Harness** — `main.cpp` (`modelConf` header, pin check and write); `confgen` (new).
- **Rig** — `JoinedInput.cs` pin key; `Joined.cs` kill check; `JoinedReport.cs` conf line and *Sweep measures*; `stage.sh` buffer edit and closure; `joined.sh` flags.
- **Fixtures and tests** — ladder entries, WAVs and pins; the Tier B skip; EditMode tests (rules, presets, resolution, log text); PlayMode tests on the core extractor (`ModelExtractor.cs` :42; `BuildModelArchive`, `ModelExtractorCacheTests.cs` :788-798) and on a real load.

## Data contract & runtime flow

**The model directory** (the cache, or `confgen`'s `--out`): `conf/model.conf`, effective; `.voxr-archive-model.conf`, the archive's, written once; `.voxr-model-stamp`, untouched; `*.voxr-tmp`, a write in flight. **Dump header** and **`expectations.json`** each carry `"modelConf"`, the conf's text (`null` if absent in a dump).

**One model load:** (1) claim, then resolve — the explicit choice, else `Automatic` by the hint; (2) the extractor serves or extracts and validates, unchanged; (3) after the re-check, the overlay: no conf, nothing; capture the copy if absent; compute from it and the row; replace the conf only if the bytes differ; (4) one log line; (5) the backend loads and reads the conf, which holds until the next load.

**One sweep run** (Git Bash, project root): `confgen --rule2 <v>` per candidate; the Command 3 harness with `--dump` on the vendor model (A, pinned) and each `confgen` output (B); `joined.sh --vary decoder --layer recogniser --buffer time|eager --window 0.5|2.0`; *Sweep measures* from each report.

### The pause ladder

Every word is in the grammar the harness pins (`expectations.json` :101-235); the patterns are the demo grammar's (`Tests~/Runtime/DemoGrammar.cs` :58, :104, :111, :122-124; its hand-synced copy, :7-11). A P prefix is a complete command the continuation extends; an M prefix still owes a slot or literal.

| Set | Intent; expected slots | Segments | Rungs (s) | Prefix |
|---|---|---|---|---|
| P | `set_heading`; heading "two seven zero" | "set heading two" · "seven zero" | 0.2, 0.3, 0.4, 0.6 | `set heading {heading}`, complete |
| P | `set_heading`; heading "two seven zero", elevation "one five" | "orient heading two seven zero" · "mark one five" | 0.2, 0.3, 0.4, 0.6 | `orient heading {heading}`, complete; the whole adds `?mark {?elevation}` |
| P | `set_heading`; heading "one eight zero" | "orient heading one" · "eight zero" | 0.2, 0.3, 0.4, 0.6 | `orient heading {heading}`, complete |
| M | `launch_weapon`; weapon "missiles", target "hotel one" | "launch missiles target" · "hotel one" | 0.3, 0.6, 0.9 | `{target}` owed |
| M | `retreat_from_target`; target "bravo two" | "fall back from" · "target bravo two" | 0.3, 0.6, 0.9 | `target {target}` owed |
| M | `approach_target`; target "alpha one" | "approach" · "target alpha one" | 0.3, 0.6, 0.9 | `target {target}` owed |

### Sweep record (F4)

Side A is the archive's own conf (rule2 0.5, the first sweep point); one row per recogniser-layer run on the 16 fixtures plus the ladder. Side A is `.scratch/endpointer-profile-phase-5/a.jsonl`, dumped fresh by Phase 5 (2026-10-17) and passing the standing pin's kill check in every run. The first eight rows are Phase 5's (`plan-5.md`, rule2 0.35 and 0.25). After Phase 5 found no eligible candidate (both fail KC1 at W 2.0), the maintainer ruled on 2026-10-17 (tuning ruling 1), on the PM's recommendation, a second rule2 sweep at milder values; the next eight rows are Phase 5b's (`plan-5b.md`, rule2 0.45 and 0.4, the same side A). The first sweep is the requirements' `rule2 ∈ {0.5, 0.35, 0.25}`. Tuning ruling 2 (2026-10-17), on these rows, is in the *Tuning record*.

| rule2 (B) | Buffer | W (s) | Finals/utt A → B | S (s) | KC1 | KC2 |
|---|---|---|---|---|---|---|
| 0.35 | time-driven | 0.500 | 1.135 → 1.216 | 0.136 | pass | none |
| 0.35 | time-driven | 2.000 | 1.135 → 1.216 | 0.130 | fail | none |
| 0.35 | eager commit | 0.500 | 1.135 → 1.216 | 0.136 | pass | none |
| 0.35 | eager commit | 2.000 | 1.135 → 1.216 | 0.130 | fail | none |
| 0.25 | time-driven | 0.500 | 1.135 → 1.270 | 0.238 | pass | none |
| 0.25 | time-driven | 2.000 | 1.135 → 1.270 | 0.244 | fail | none |
| 0.25 | eager commit | 0.500 | 1.135 → 1.270 | 0.238 | pass | none |
| 0.25 | eager commit | 2.000 | 1.135 → 1.270 | 0.244 | fail | none |
| 0.45 | time-driven | 0.500 | 1.135 → 1.162 | 0.068 | pass | none |
| 0.45 | time-driven | 2.000 | 1.135 → 1.162 | 0.069 | pass | none |
| 0.45 | eager commit | 0.500 | 1.135 → 1.162 | 0.068 | pass | none |
| 0.45 | eager commit | 2.000 | 1.135 → 1.162 | 0.069 | pass | none |
| 0.4 | time-driven | 0.500 | 1.135 → 1.189 | 0.079 | pass | none |
| 0.4 | time-driven | 2.000 | 1.135 → 1.189 | 0.083 | fail | none |
| 0.4 | eager commit | 0.500 | 1.135 → 1.189 | 0.079 | pass | none |
| 0.4 | eager commit | 2.000 | 1.135 → 1.189 | 0.083 | fail | none |

Eligibility (four KC1 (Δf × W ≤ S) pass, every KC2 none), per candidate:

- rule2 0.35: not eligible — KC1 pass, fail, pass, fail; KC2 none, none, none, none
- rule2 0.25: not eligible — KC1 pass, fail, pass, fail; KC2 none, none, none, none
- rule2 0.45: eligible — KC1 pass, pass, pass, pass; KC2 none, none, none, none
- rule2 0.4: not eligible — KC1 pass, fail, pass, fail; KC2 none, none, none, none

### Tuning record

| Preset | rule2 | rule3, rule4 | Evidence |
|---|---|---|---|
| Model default | the archive's own | the archive's own | adr-0003 |
| Responsive | 0.45 | the archive's own | *Sweep record* 0.45 rows (plan-5b); tuning ruling 2, 2026-10-17 |

A candidate is eligible when its four KC1 verdicts pass and KC2 lists no case; the tuning pass proposes, the human rules. rule3 and rule4 are swept only on a measured reason the record names; rule1 and rule5 never (rule 2). Tuning ruling 2 (2026-10-17, the maintainer): one tuned preset, "Responsive", rule2 0.45 (eligible; 0.4 is not, KC1 failing at W 2.0), rule3 and rule4 the archive's own (no measured reason to move them); it is the Continuous default at the flip (adr-0002).

## Non-functional realization

- **Once per load (§6 Performance).** The overlay runs synchronously in `InitialiseAsync`: two small files read, at most two replaced, none on a steady-state load. `Update` (:567-606), `Runtime/Commands/**` and the bridge — the `review` binding's hot paths — are untouched.
- **Input boundary (§6 Robustness).** An unparseable conf yields the pristine bytes and a warning; a missing key comes only from a tuned row; every write is temp-then-replace. The invariant is structural: the effective bytes are computed from the copy and the row, never from the current conf (F8).
- **R9 intact (F9); degraded dependencies (§6).** Extractor and stamp are unchanged and validation never inspects the added file; an IO failure is a warning, and no load fails for the profile.
- **One function, three producers (F3).** Device, Editor and `confgen` compile one file with values as text; the harness records the bytes it decoded and its pin checks them.

## Build plan

Lowest-risk-first, retiring the riskiest unknown — whether any rule2 below 0.5 survives both kill criteria — before any shipped behaviour changes: test data, instruments, an uncalled Runtime file, the sweep, the wiring with the default unchanged, the flip, the human. Every rig phase runs DocCheck (`Planning~/verification-recipe.md` :222); every run is Windows-native in Git Bash.

- **Phase 1 — Pause-ladder corpus.** 21 manifest entries; the ladder edge trim in `generate.py`; WAVs; the band check; 21 `{file}` entries; `--write-baseline` on the vendor model; the Tier B skip; `doc-writer` edits the binding's "16/16". Green: Command 3 at the new count, the pin diff harness-written; Commands 1–2. *Discharges:* F1, §4.5, §10 item 2.
- **Phase 2 — Conf record.** Header `modelConf`; the pin key, check and write, `--write-baseline` adding it (diff that key alone; a moved final stops); the rig's pin key, kill check and conf line; README guards (`doc-writer`). Green: Command 3; a `--dump` header with `modelConf`; a `.scratch/` model copy with an edited rule2 line exits 2 against the standing pin, naming the conf, and, dumped against a `.scratch/` pin copy, shows a different conf line in a decoder-shape report; DocCheck. *Discharges:* F2; F3's record half.
- **Phase 3 — Rig sweep machinery.** `--buffer`, `--window`, the staged edit, *Sweep measures*. Green: DocCheck; one dump against itself in all four setups (the edit in config, Δf 0, KC1 pass, KC2 empty); a `.scratch/` B dump with one word changed gives KC2 STOP; an unmatched initializer stops the stage. No binding command. *Discharges:* F4 (machinery).
- **Phase 4 — Overlay core and `confgen`.** `EndpointerOverlay.cs`, "Model default" alone, no call site; EditMode tests of the rules and §6 cases; PlayMode tests on the core extractor — extract, apply a tuned row, re-extract (stamp bytes unchanged, a planted file survives), apply "Model default" (the archive's bytes); `confgen`. Green: Commands 1–2; Command 3; a `--dump` on `confgen --rule2 0.35`'s output whose `modelConf` hashes to `confgen`'s SHA-256, and a `diff` against the vendor conf showing the rule2 line alone. *Discharges:* F3, F7 (rules), F8, F9, §6 robustness, §10 items 3–4.
- **Phase 5 — Sweep and tuning pass.** Dumps A (vendor, pinned) and B per candidate; the eight runs; the *Sweep record*; any rule3 or rule4 sweep it justifies; the human's ruling into the *Tuning record*. A KC2 STOP stops the work; no eligible candidate blocks the flip (adr-0002 (A)) pending the human. No code. *Discharges:* F4, §4.6, §10 item 1.
- **Phase 6 — Presets, choice, resolution; default unchanged.** The enum and tuned rows; the field; the hint and hand-off; `Resolve` (`Automatic` → "Model default" in both modes); the seam; the log; `stage.sh`'s closure. Green: Commands 1–2 (F5's list, no public float; F6's explicit and Push-to-talk cases; a PlayMode Editor-backend load logs the line, stamp unchanged, cache hit); Command 4 (a public type); DocCheck; recogniser stages of the worktree and a pre-feature ref build. *Discharges:* F5, F6 (explicit, Push-to-talk, no controller), F7 (seam), F8, F9, F12, §6 performance.
- **Phase 7 — Default flip and re-pin.** `Automatic` in Continuous → the tuned preset; the reviewed re-pin; `doc-writer`: Command 3, the README, the CHANGELOG line. Green: Commands 1–2 (Tier B under the tuned default); Command 3 in its new form. *Discharges:* F6 (Continuous default), F10.
- **Phase 8 — Human-only check on a Quest 3 (§9).** The adb-pulled conf after a tuned-default load; about ten commands under the tuned default, then "Model default". A fail blocks the flip: Phase 7 reverted, Phase 6's opt-in state stands (adr-0002 (A)). *Discharges:* F11; §8's arm64 conf assumption.
- **Assets code cannot author:** the piper venv and voice, downloaded by `generate.sh`'s first run (:51, :54-58) — a human step, subagent downloads being refused; generation is offline after. `NativeBridge~/vendor/` if absent (human, per the harness README). Command 3's toolchain, .NET SDK 10, Unity 6000.4.7f1, the host. For Phase 8, a Quest 3 and the human.
- Phase 1 — persisted plan (2026-10-16): [plan-1.md](plan-1.md)
- Phase 2 — persisted plan (2026-10-16): [plan-2.md](plan-2.md)
- Phase 3 — persisted plan (2026-10-16): [plan-3.md](plan-3.md)
- Phase 4 — persisted plan (2026-10-16): [plan-4.md](plan-4.md)
- Phase 5 — persisted plan (2026-10-17): [plan-5.md](plan-5.md)
- Phase 5b — persisted plan (2026-10-17, follow-up sweep): [plan-5b.md](plan-5b.md)
- Phase 6 — persisted plan (2026-10-17): [plan-6.md](plan-6.md)
- Phase 7 — persisted plan (2026-10-17): [plan-7.md](plan-7.md)
- Phase 8 — persisted plan (2026-10-17): [plan-8.md](plan-8.md)

## Risks, tradeoffs & open questions

- **The decoder's "complete" is not the parser's.** rule2 tests the best state of vosk's grammar model (design §1, inferred from upstream `recognizer.cc`); whether a P prefix ends at rule2 and an M pause holds to rule3 is what the ladder measures. An M rung that splits under "Model default" is evidence, not a defect.
- **Piper's edge silence.** Segments are synthesized alone; the generator trims each ladder segment's edge near-silence (10 ms frames, -40 dBFS), so a pause is the gap. Phase 1's band check catches a rung out of band, and its joined-words check a clipped word.
- **Tier B under the tuned default (Phase 7).** A red is flip evidence and stops for the human, never a test edit. **`File.Replace` on IL2CPP Android** is first exercised in Phase 8.
- **Out-of-contract edits.** Deleting `.voxr-archive-model.conf` alone after a tuned load re-takes a tuned conf — a cache hand-edit; `getting-started.md` :63 says so after G2. A developer's own `Initialise()` from another component's `Awake` or `OnEnable`, before the controller's `Awake`, resolves as Continuous; the explicit choice covers it.
- **Readings.** F1's "never by hand" covers pinned values, not the drift guard's `{file}` entries (scope flag 2). F10's CHANGELOG line is written in Phase 7: F10 is a Must checked at G2 (§9), the narrow exception to §2 Out 4. KC1's S counts cases correct on both sides.
- **Drift watch.** `confgen`'s links and `stage.sh`'s closure follow the overlay's dependencies; the README copies Command 3; the rig's pin keys follow `expectations.json`. TTS evidence cannot rank decoders (C12); decode latency is not modelled.

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

The frontmatter's sources, plus: design `endpointer-profile.md` §0, §1 (rules 1–9, the load order), §3, §7 · feature `joined-rig-recogniser` (*Staging*, *Configuration*, *Timing measures*, the F6 canary) · `Planning~/design-docs/joined-rig.md` (rules 2, 10) · `Planning~/verification-recipe.md` ("Building NativeBridge" :49; Samples~ compile :67; DocCheck :222) · `Documentation~/getting-started.md` (:51, :63) · `KNOWN_LIMITATIONS.md` (:417-462) · `Planning~/research/2026-09-05-next-level/03-critique-and-ranking.md` (C2 :134-135; C12 :164-165; rank 6 :232; R10 :253)
