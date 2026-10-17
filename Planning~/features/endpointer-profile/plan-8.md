# Phase 8 — persisted plan (2026-10-17)

**Human-only check on a Quest 3 (§9)**, unsplit, serial, no units: F11; §8's arm64 conf assumption; the first `File.Replace` on IL2CPP Android (Risks). Human-only: no code, no agent on the device (none runs adb, builds, deploys or judges a device result, `.claude/bindings/unity.md` Failure rule); the human runs Steps 1-6 and reports to the PM. `$E`=`.scratch/endpointer-profile-phase-8`; REC=`Planning~/verification-recipe.md`.
**Out of scope:** any `Runtime/`, `Tests~/`, `NativeBridge~/` or host change; product docs; the end-of-build reconcile (B1-B4, stale cites).

**Departures from the Build-plan row and rulings** (the human, 2026-10-17):
- D1. §9's "(session log)" cannot be met on device (Editor Play Mode only, `Documentation~/editor-testing.md:40-48`): the Tactical scene's CommandLog and LastCommand panels stand in, read in-headset, by cast or screenshot, plus logcat for the load lines. This reads the parenthetical as an evidence source, not a decision; read as a decision, it reopens §9 on a design branch.
- D2. "Model default" is a second build: the same scene, `endpointerProfile` Model Default, `adb install -r` over the tuned install so the cache persists. It adds P3: the pulled `conf/model.conf` byte-equal to `.voxr-archive-model.conf` (design rule 4 on device; a second `File.Replace`).
- D3. Extra split: more than one CommandLog entry for one spoken command. Changed words: a wrong intent or slot value against the phrase. The Model-default run is the "sooner" baseline, no pass bar. A tuned miss also seen under Model default: the human rules retake or fail, never passed silently.
- D4. Host-side setup (sample import, build scene, Development Build, `RECORD_AUDIO`) is the human's, in the host repo; this repo commits nothing from it.

## Files to change

| Path | Change | Why |
|---|---|---|
| code | none | human-only |
| `Planning~/features/endpointer-profile/plan-8.md` | create | F11 |
| `Planning~/features/endpointer-profile/architecture.md` | modify: the `## Build plan` link line only | process-docs |
| `$E/`, the five evidence files (Steps 1-5) | create, untracked, by the human | §9; the report |
| `D:\Workspace\VoXR TestGround` setup | the human's; commits nothing here | D4 |

## Contracts and interfaces

**Facts.**
- Host: IL2CPP (`scriptingBackend Android: 1`), ARM64, the model zip in StreamingAssets, only Voice Check imported; `applicationIdentifier` `com.UnityTestRunner.UnityTestRunner` (likely a test leftover): read `<pkg>` from Player Settings at build time.
- `CommandRecognition_Tactical` has no push-to-talk controller: it loads Continuous (`PushToTalk.unity` loads Push-to-talk). CommandLog: a `[<t>s] <intent> (score <s>)` or `[<t>s] <unrecognised> "<text>"` line per fire; LastCommand: intent, score, slots (`TacticalSceneController.cs:99-185`).
- `endpointerProfile`: private, serialized, default `Automatic` (`VoxrSpeechRecogniser.cs:26-30`; inspector "Model Default" is `ModelDefault`), resolved once per load (:219-222), no runtime switch. Only rule2 differs: `EndpointerOverlay.Responsive` (:78) is `Row("0.45", null, null)`.
- Load lines, tag `Unity` (`LogLine`, `EndpointerOverlay.cs:119-135`; `VoxrSpeechRecogniser.cs:249-253`):
  - T: `[VoxrSpeechRecogniser] Endpointer profile Responsive (Continuous default): rule2=0.45 rule3=0.75 rule4=1.0`
  - MD: `[VoxrSpeechRecogniser] Endpointer profile ModelDefault (explicit choice): rule2=0.5 rule3=0.75 rule4=1.0`
  - an IO failure, a `File.Replace` throw included (`Apply` :315-373, `Publish` :402-408): a warning ending `— not applied: IO failure: … (<conf path>)`.
- `<model dir>`: the text between `model=` and `, sample_rate=` in `Bridge initialised: model=…` (`vosk_bridge.cpp:263`): `persistentDataPath/VoxrModels/vosk-model-small-en-us-0.15` (`ModelExtractor.cs` `ModelCacheFolder` :22, :34), holding `conf/model.conf` and `.voxr-archive-model.conf` (`ArchiveCopyFileName` :137; the pristine copy, rule2 `0.5`). Every load re-derives the conf from the copy.

**Prerequisites (the human).** A Quest 3, developer mode, USB. Git Bash at the project root, never WSL: `A="/c/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe"; P=<pkg>; E=.scratch/endpointer-profile-phase-8; mkdir -p $E && cd $E` (REC :41); `MSYS_NO_PATHCONV=1` on any command with a `/`-path argument (REC :47). Host: import the Command Recognition sample (`file:`-linked: it tracks `100a987`+); `CommandRecognition_Tactical` the only build scene; Android, IL2CPP ARM64; Development Build on (for `run-as`); `RECORD_AUDIO` on.

**Step 1 - tuned build.** `endpointerProfile` stays `Automatic`. Build; `"$A" install -r <apk>`; launch; `"$A" logcat -s "vosk-bridge:*" "Unity:*" | tee logcat-tuned.txt` (REC :44). Wait for the profile line.

**Step 2 - P1, the conf.** `M=<model dir>; R=files/VoxrModels/vosk-model-small-en-us-0.15`.
- Under `/storage/emulated/0/`: `MSYS_NO_PATHCONV=1 "$A" pull "$M/conf/model.conf" tuned-model.conf; MSYS_NO_PATHCONV=1 "$A" pull "$M/.voxr-archive-model.conf" archive.conf`.
- Under `/data/`: `"$A" exec-out run-as $P cat $R/conf/model.conf > tuned-model.conf; "$A" exec-out run-as $P cat $R/.voxr-archive-model.conf > archive.conf`.
- `diff archive.conf tuned-model.conf`: one hunk, `<n>c<n>`, `< --endpoint.rule2.min-trailing-silence=0.5` then `> --endpoint.rule2.min-trailing-silence=0.45`.
- `logcat-tuned.txt`: line T; no `— not applied`.

**Step 3 - P2, ten commands under tuned.** Natural speech, about 0.5 s apart, in this order (`Samples~/CommandRecognition/README.md:45-54`; intents `CommandDemo.cs:21-30`; 6-7 swap the active sets, 8 restores all, :125-139):
1. `fire missiles at hotel one`: `launch_weapon`
2. `cease fire`: `cease_fire`
3. `approach target alpha one`: `approach_target`
4. `open distance from target bravo two`: `retreat_from_target`
5. `set heading two seven zero`: `set_heading`
6. `weapons mode`: `mode_weapons`
7. `navigation mode`: `mode_navigation`
8. `all modes`: `mode_all`
9. `approach target … alpha one`: a hesitation, about 0.5 s, before the target
10. `set heading two … seven zero`: a hesitation
11. optional, `fire missiles at … hotel one`: a hesitation

`disable all` is left out: it mutes the grammar for 5 s (README :54). Per command: its CommandLog entries, LastCommand intent and slots, and the felt latency.

**Step 4 - Model-default build.** `endpointerProfile` to Model Default; build; `"$A" install -r <apk>`, no uninstall; launch; `"$A" logcat -s "vosk-bridge:*" "Unity:*" | tee logcat-model-default.txt`. Expect line MD; no `— not applied`.

**Step 5 - P3, restoration.** Pull `conf/model.conf` by Step 2's route as `model-default-model.conf`; `cmp model-default-model.conf archive.conf; echo "exit $?"`: `exit 0`.

**Step 6 - the same commands under Model default**, recorded as Step 3.

**Record table** (Steps 3, 6), a row per command per run: `| # | Phrase | Run | CommandLog entries | LastCommand intent; slots | Pass/fail |`.

**Pass.** P1, P3, and every Step 3 command firing its intent with one CommandLog entry and the slots as spoken; the human judges tuned fires sooner than Model default. P1 and that judgment discharge §8: the conf on disk is right, and the decoder behaves differently on it.

**Fail and stop rules.**
- Any of P1, P3 or Step 3 fails, a `— not applied` warning shows, or the human judges no sooner: fail.
- A tuned miss also seen under Model default: the human rules retake or fail (D3).
- On a fail, stop: `100a987` is reverted only on the human's ruling, which the PM records.

**Report back to the PM** (one message): the three pulled and two logcat files, by path; the `diff` and `cmp` outputs; the record table, commands × 2 runs; the "sooner" judgment, in the human's words.

**The PM, after the report.** Record the evidence in `memory/<YYYY-MM-DD>.md` (the As-built charter routes gate evidence there); mark F11 and §8 discharged for G2, or open the fail ruling; then the end-of-build reconcile (B1-B4, stale cites), then G2.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| Panels plus logcat (D1) | the session log | none on device |
| A second build (D2) | a runtime re-load | no code in a human-only phase |
| `adb install -r` | uninstall first | Step 4 re-derives over a tuned conf |
| `cmp` for P3 | `diff` | rule 4 claims the archive's bytes exactly |

## Acceptance criteria

1. [ ] P1, Step 2 (F11; §9 (1); F12).
2. [ ] P2, Step 3 (F11; §9 (2)).
3. [ ] P3, Step 5; line MD (design rule 4; D2).
4. [ ] The "sooner" judgment (§9 (2)).
5. [ ] No `— not applied` line in either logcat file (F12; Risks).
6. [ ] §8 discharged by 1 and 4.

## Open questions that block implementation

None; a fail or a D3 ruling opens one.

## Blast radius

- A pass: F11 and §8 discharged for G2; nothing in the tree changes.
- A fail: stop; the human rules the revert of `100a987`, Phase 7 whole (flip, tests, pin, binding, README, recipe, CHANGELOG line); Phase 6's opt-in state stands (adr-0002 (A)).

## Runtime claims

The human's steps check them, not the validator.
- a (2): `Bridge initialised: model=` prints the absolute model dir on Quest.
- b (2, 5): `persistentDataPath` is reachable by `adb pull` (external) or `run-as` (Development Build).
- c (2, 4): `File.Replace` succeeds on IL2CPP Android.
- d (4): `adb install -r` keeps `persistentDataPath`, so Step 4 loads over the tuned cache.
- e (3): CommandLog is readable in-headset (the canvas renders under the host's OpenXR setup).
- f (2): `run-as` starts in the app's data directory, so `$R` resolves.
- g (1, 4): logcat started after launch replays the load lines from its buffer; Step 4's file may hold Step 1's too.
- h (3, 6): CommandLog keeps 8 lines (`maxLogLines`, `TacticalSceneController.cs:45`): record each command before the next.
