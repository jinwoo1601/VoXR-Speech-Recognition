# Phase 8 — persisted plan (2026-10-17)

**Human-only Quest 3 check (§9)**: F11; §8's arm64 conf assumption; the first `File.Replace` on IL2CPP Android (Risks). No code; no agent runs adb, builds, deploys or judges a device result (`.claude/bindings/unity.md` Failure rule). REC=`Planning~/verification-recipe.md`.

**Departures and rulings** (the human, 2026-10-17):
- D1. §9's "(session log)" is Editor-only (`Documentation~/editor-testing.md:40-48`): the Tactical scene's CommandLog and LastCommand panels stand in, plus logcat. Read as a decision, it reopens §9 on a design branch.
- D2. "Model default" is a second build (`endpointerProfile` Model Default), `adb install -r` over the tuned one, keeping the cache; it adds P3: `conf/model.conf` byte-equal to `.voxr-archive-model.conf` (rule 4; a second `File.Replace`).
- D3. Extra split: over one CommandLog entry per command; changed words: a wrong intent or slot value. Model default is the baseline. A tuned miss also seen under it: the human rules retake or fail, never a silent pass.
- D4. Host setup (sample import, build scene, Development Build, `RECORD_AUDIO`, eager commit) is the human's; nothing from it is committed here.

## Files to change

| Path | Change | Why |
|---|---|---|
| code | none | human-only |
| `plan-8.md` | create | F11 |
| `architecture.md` | `## Build plan` link line, Phase 8 row | process-docs |
| `.scratch/endpointer-profile-phase-8/` evidence | the human's, untracked | §9 |
| `D:\Workspace\VoXR TestGround` setup | the human's | D4 |

## Contracts and interfaces

**Facts.**
- `CommandRecognition_Tactical`: no push-to-talk controller, so Continuous; CommandLog a line per fire, LastCommand intent, score, slots (`TacticalSceneController.cs:99-185`); its `CommandRecogniser` has `bufferWindow: 2` (scene :1413), `prefixHoldSeconds` 0, eager commit unset, so off (`VoxrCommandRecogniser.cs:74-89`).
- `endpointerProfile` (`VoxrSpeechRecogniser.cs:26-30`; default `Automatic`; "Model Default" is `ModelDefault`) resolves once per load (:219-222); only rule2 differs, `EndpointerOverlay.Responsive` (:78) being `Row("0.45", null, null)`.
- Load lines, tag `Unity` (`LogLine`, `EndpointerOverlay.cs:119-135`; `VoxrSpeechRecogniser.cs:249-253`), ending:
  - T: `Responsive (Continuous default): rule2=0.45 rule3=0.75 rule4=1.0`
  - MD: `ModelDefault (explicit choice): rule2=0.5 rule3=0.75 rule4=1.0`
  - IO failure (`Apply` :315-373, `Publish` :402-408, `File.Replace` included): a warning ending `— not applied: IO failure: … (<conf path>)`.
- `<model dir>` (`Bridge initialised: model=<dir>, sample_rate=`, `vosk_bridge.cpp:263`): `persistentDataPath/VoxrModels/vosk-model-small-en-us-0.15` (`ModelCacheFolder`, `ModelExtractor.cs:22`, :34), holding `conf/model.conf` and the pristine copy `.voxr-archive-model.conf` (rule2 `0.5`; `ArchiveCopyFileName`, `EndpointerOverlay.cs:137`), written only if absent (:328-334), from which each load re-derives the conf.

**Expected difference.** With eager commit, a complete command no more speech can extend (E) fires at the endpointer's end: tuned saves the recorded ≈0.07 s (*Sweep record*, rule2 0.45: S 0.068 s at W 0.5, 0.069 s at W 2.0), likely imperceptible. An extendable one (W) waits the 2 s window in both runs, no difference: items 5, 10 (`set heading {heading}`, `CommandDemo.cs:119`, ends in a 1-3-word number slot, :67). The rest are E.

**Prerequisites (the human).** Quest 3, developer mode, USB. Git Bash at the project root, never WSL: `A="/c/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe"; P=<pkg>; E=.scratch/endpointer-profile-phase-8; mkdir -p $E && cd $E` (REC :41; `<pkg>` from Player Settings). Host (Android IL2CPP ARM64, model zip in StreamingAssets, only Voice Check): import the Command Recognition sample (the Runtime is `file:`-linked to this repo; the `Assets/Samples` copy is an import-time snapshot of `Samples~`); `CommandRecognition_Tactical` the only build scene; Development Build (for `run-as`); `RECORD_AUDIO`. **Both builds:** that scene's `CommandRecogniser` has **Eager Flush On Complete Match** ticked, `bufferWindow` 2, `prefixHoldSeconds` 0 (retuning `bufferWindow` is out, requirements §2 Out 3); check the Inspector, scene saved, before each build.

**Step 1 - tuned build.** `endpointerProfile` `Automatic`. Build; `"$A" install -r <apk>`; launch; `"$A" logcat -s "vosk-bridge:*" "Unity:*" | tee logcat-tuned.txt` (REC :44); wait for the profile line.

**Step 2 - P1, the conf.** `M=<model dir>; R=files/VoxrModels/vosk-model-small-en-us-0.15`. Define pull `g` and listing `l` by `$M`'s root:
- `/storage/emulated/0/`: `g(){ MSYS_NO_PATHCONV=1 "$A" pull "$M/$1" $2; }; l(){ MSYS_NO_PATHCONV=1 "$A" shell ls -l "$M/$1"; }`
- `/data/`: `g(){ "$A" exec-out run-as $P cat $R/$1 > $2; }; l(){ "$A" shell run-as $P ls -l $R/$1; }`
- `g conf/model.conf tuned-model.conf; g .voxr-archive-model.conf archive.conf; l .voxr-archive-model.conf` (the copy's time).
- `diff archive.conf tuned-model.conf`: one hunk, rule2 `0.5` → `0.45` (`--endpoint.rule2.min-trailing-silence`).
- `logcat-tuned.txt`: line T; no `— not applied`.

**Step 3 - P2, ten commands under tuned.** Natural speech, in order (`Samples~/CommandRecognition/README.md:45-54`; intents `CommandDemo.cs:21-30`; 6-7 swap active sets, 8 restores all: `OnCommand` :171-195, `SwitchTo*` :260-273). Between commands, over 2 s (window plus fire): each CommandLog entry seen before the next is spoken. Hesitation: a pause of about 0.5 s inside the command.
1. `fire missiles at hotel one`: `launch_weapon`, E
2. `cease fire`: `cease_fire`, E
3. `approach target alpha one`: `approach_target`, E
4. `open distance from target bravo two`: `retreat_from_target`, E
5. `set heading two seven zero`: `set_heading`, W
6. `weapons mode`: `mode_weapons`, E
7. `navigation mode`: `mode_navigation`, E
8. `all modes`: `mode_all`, E
9. `approach target … alpha one`: hesitation, E
10. `set heading two … seven zero`: hesitation, W
11. optional, `fire missiles at … hotel one`: hesitation, E

Hesitations 9 and 11 fall at incomplete states (after `approach target`, :105; `fire missiles at`, :75), under rule3 0.75 / rule4 1.0 in both runs; 10 follows `set heading two`, already a complete command (:119), and at ≈0.5 s straddles rule2 0.45 vs 0.5, so may split under tuned alone: D3's "also seen under Model default" ruling applies knowingly. No `disable all` (mutes the grammar 5 s, README :54). Record per command: CommandLog entries, LastCommand intent and slots, felt latency.

**Step 4 - Model-default build.** `endpointerProfile` Model Default; build; `"$A" install -r <apk>`, no uninstall; launch; Step 1's logcat into `logcat-model-default.txt`. Expect line MD; no `— not applied`.

**Step 5 - P3, restoration.** `l .voxr-archive-model.conf`: Step 2's time, a cache hit (claim d); a later one, a re-extraction: P3 indeterminate, not passed. `g conf/model.conf model-default-model.conf; cmp model-default-model.conf archive.conf; echo "exit $?"`: `exit 0`.

**Step 6 - the same commands under Model default**, recorded as Step 3.

**Pass.** P1; P3; every Step 3 command fires its intent once, slots as spoken; the human judges tuned fires not perceptibly later, sooner where perceptible, than Model default (sooner is expected; no perceptible difference is a pass). P1 and the judgment discharge §8.

**Fail and stop rules.**
- Fail: P1 or P3 fails; a Step 3 split, changed word or wrong intent; a `— not applied` warning; tuned judged perceptibly later.
- P3 indeterminate: no pass; the human rules a rerun from Step 1.
- A tuned miss also seen under Model default: the human rules retake or fail (D3).
- On a fail, stop: `100a987` is reverted only on the human's ruling, which the PM records.

**Report to the PM**, one message: the five files by path; `diff` and `cmp` outputs; both copy times; eager commit checked per build; the record table, a row per command per run; the judgment, in the human's words.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| Panels plus logcat | the session log | D1 |
| A second build | a runtime re-load | D2; no code |
| `adb install -r` | uninstall first | D2 |
| `cmp` for P3 | `diff` | rule 4 |
| Eager commit, both builds | time-driven only | §9; design §3 |

## Acceptance criteria

1. [ ] P1, Step 2 (F11; §9 (1); F12).
2. [ ] P2, Step 3, eager commit on (F11; §9 (2)).
3. [ ] P3, Step 5, on a cache hit; line MD (design rule 4; D2).
4. [ ] The judgment, not perceptibly later, sooner where perceptible (§9 (2)).
5. [ ] No `— not applied` line in either logcat file (F11; Risks, `architecture.md:180`).
6. [ ] §8 discharged by 1 and 4.

## Open questions that block implementation

None; a fail or a D3 ruling opens one.

## Blast radius

- A pass: F11 and §8 discharged for G2; nothing in the tree changes.
- A fail: stop; the human rules the revert of `100a987` (Phase 7 whole); Phase 6's opt-in state stands (adr-0002 (A)).

## Runtime claims

Checked by the human's steps.
- a (2): `Bridge initialised: model=` prints the absolute model dir.
- b (2, 5): `persistentDataPath` is reachable by `adb pull` (external) or `run-as` (Development Build).
- c (2, 4): `File.Replace` succeeds on IL2CPP Android.
- d (4, 5): `adb install -r` keeps `persistentDataPath`; the Model-default load is a tier-2 archive-hash hit (`ModelExtractor.cs:141-170`; tier 1 misses, new APK token, :327), not a re-extraction (:225-229), which renews the copy and passes `cmp` vacuously.
- e (3): CommandLog renders readably in-headset.
- f (2): `run-as` starts in the app's data directory.
- g (1, 4): logcat started after launch replays the load lines.
- h (3, 6): CommandLog keeps 8 lines (`maxLogLines`, `TacticalSceneController.cs:45`).
- i (1, 4): `Debug.Log`/`Debug.LogWarning` (`VoxrSpeechRecogniser.cs:250-253`) reach logcat as tag `Unity` in a Development Build.
- j (1, 4): `adb logcat … | tee` writes without block-buffering loss; the file is complete after Ctrl-C.
