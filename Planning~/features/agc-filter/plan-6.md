# Phase 6 — persisted plan (2026-11-06)

Scope: the row as takes 1–9 (the PM's; record: hub `memory/2026-11-06.md`, `# 2026-11-06 - feat-agc-filter - phase-pickup 6: RECON, PERSIST, PLAN` `## Decisions`) carry it, plus the on-device check of the review-cycle native change (WRAP-1, PM-2; `ea8ed3d`) deferred here; no A/B, no tuning, no source, no `code-writer`. Serial, this tree. T = `D:/Workspace/VoXR-Speech-Recognition.trees/feat-agc-filter`, every command run from it; H = `D:/Workspace/VoXR TestGround/`; S = `.scratch/agc-filter/phase6/` (ignored); APK = `D:/Workspace/VoXR TestGround/Builds/agc-filter-phase6.apk`; A = `MSYS_NO_PATHCONV=1 "C:/Program Files/Unity/Hub/Editor/6000.4.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe"`.

## Files to change

None in the repo: nothing under `Runtime/`, `Editor/`, `Tests~/`, `NativeBridge~/`, `Samples~/` or `.claude/` (take 9). Non-repo:

| Path | Change | Why |
|---|---|---|
| S`hold_host.py`, S`apk_read.py` | create (the PM; LF) | take 3; takes 1, 4 |
| S`tree-before.txt`, `status-before.txt`, `host-rest/`, `hold.log`, `apk.txt`, `logcat.txt`, `tries.md` | create (evidence) | F18 |
| H`Packages/manifest.json`, `packages-lock.json` | pointed at T, restored byte-for-byte | take 3 |
| H`Builds/agc-filter-phase6.apk` | build output (git-ignored), kept | F18 |

## Contracts and interfaces

The PM runs every command; the human builds in the Editor, launches, grants the microphone, speaks. Scratch files LF (Write; Python `newline="\n"`); `logcat.txt` as adb writes it. No host setting changes: `applicationIdentifier.Android` stays `com.UnityTestRunner.UnityTestRunner`; Android's XR loader (OpenXR, `MetaQuestFeature Android` enabled, H`Assets/XR/`) is set; the active build target lives in ignored `Library/`. A setting the build demands stops at Step 3 as an open question, never an edit.

S`hold_host.py` puts T`Planning~/host-run/` on `sys.path`, imports `host_run` and reuses it, copying no logic: `acquire(360)` (none: print `BLOCKED` and `owner_text()`, exit 2); `write_owner(["apk-build"])`; `at_rest()` (a finding: `release`, `stop: …`, exit 3); both files' bytes to S`host-rest/`; `host_status()` to S`status-before.txt`; the manifest's one `"com.jinwoo1601.voxr": "file:<HUB>"` made `file:<TREE>` as `bracket` :358-372 does; prints `ready`. It polls S`release` every 2 s; while H`Temp/UnityLockfile` exists it prints `waiting: Editor open`; then `restore_host(files)`, `at_rest()`, `status_line(baseline, host_status())`, each printed; clean (no finding, `host status: unchanged`): `release(fd)`, `released`, exit 0; else `stop: …`, the lock kept until S`release-held` appears on the human's word.

S`apk_read.py <apk>` (`zipfile`, `hashlib`), one line each: `apk bytes=`; for `assets/vosk-model-small-en-us-0.15.zip`, `lib/arm64-v8a/libvosk.so`, `lib/arm64-v8a/libvosk-bridge.so`: `compressed=` (`compress_size`) `uncompressed=` (`file_size`), or `absent`; `contribution compressed=<sum> uncompressed=<sum> bar=70000000` (decimal MB, 10^6 bytes); `fixtures assets/VoxrFixtures/ entries= compressed= uncompressed=`; the bridge entry's sha256 against `3e8650b60c0c0a945fe04370c58bc2435fc226825fc3967676324fee53927c14` and `libvosk.so`'s against `29ddc0282af7e44c790f2a7dfbcf7ed9e557361dc1acf609142c89b792d714e1`, `match` or `MISMATCH`.

**Step 1: preflight (PM).** H`Temp/UnityLockfile` absent; APK absent; the adb path exists; `git status --short -uall` to S`tree-before.txt`. Else stop.

**Step 2: hold and point (PM).** In the background: `py -I .scratch/agc-filter/phase6/hold_host.py > .scratch/agc-filter/phase6/hold.log 2>&1`. Check: `ready`; H's manifest names T. `BLOCKED` or `stop:`: stop, nothing pointed.

**Step 3: build (human).** Opens H in Unity 6000.4.7f1; after import the PM reads H`Packages/packages-lock.json`: `com.jinwoo1601.voxr` at `file:` T. Build Profiles: Android (switched to if not active), the one scene `CommandRecognition_Tactical.unity`, Development Build off, APK not AAB; Build to APK. Check: the Console's success (the human's word); APK exists (PM). A failed Release build: once with Development Build on (take 2's fallback: the whole-APK figure changes, the contribution does not), recorded; a second failure goes to Step 4, then Step 9 with the error. The human closes the Editor.

**Step 4: restore and release (PM; never waits on 5–9).** `touch .scratch/agc-filter/phase6/release`. Check: `hold.log` ends `released`, no `stop:`; `cmp` each H`Packages/` file with its S`host-rest/` copy: equal; `git -C "D:/Workspace/VoXR TestGround" status --short` equals S`status-before.txt`; T's `git status --short -uall` equals S`tree-before.txt`. A finding: STOP for the human, the lock held.

**Step 5: APK reads (PM).** `py -I .scratch/agc-filter/phase6/apk_read.py "D:/Workspace/VoXR TestGround/Builds/agc-filter-phase6.apk" > .scratch/agc-filter/phase6/apk.txt`. The bridge `MISMATCH` or `absent`: STOP for the human, no install (take 4).

**Step 6: install (PM; headset connected, human present).** `A devices`: one serial, state `device`. `A install -r "D:/Workspace/VoXR TestGround/Builds/agc-filter-phase6.apk"`: `Success`. Else STOP with the output; no uninstall.

**Step 7: launch and capture.** PM: `A logcat -c`; in the background `A logcat -v threadtime -s "vosk-bridge:*" "Unity:*" > .scratch/agc-filter/phase6/logcat.txt`. Human: launches the app, grants the microphone. PM greps: `Bridge initialised: model=` with `agc_target=-18.0 dB` (WRAP-1's in-range path: the serialised −18 reaches `FrontEnd` unchanged), `Recognition started`, `AudioCapture: AudioRecord started at`; these three are the proof, with no absence check: `ModelLoadFailed` and `Native bridge library (libvosk-bridge) not found` only reach `OnError`, which no host sample subscribes. Absent by 180 s: Step 9 with the capture. PM-2's failure path needs fault injection on device: none is planned.

**Step 8: the tries.** On the PM's go: three heading tries, then one mode try, each once, ~10 s apart: the human says `cease fire`, stays silent at least 3 s counted, says `set heading two seven zero` (mode try: `switch to navigation`). Per try S`tries.md` records: the phrase; the capture times of lead and command; the result — heading: *fired* (`[CommandDemo] Heading 270`), *wrong* (another heading or intent, or `Unrecognised speech`), *missed* (none); mode: *decoded* or *not decoded*, by its `[Phase8] FINAL … text=` and the human's word (the demo logs no mode switch) — with its lines, any `[Phase8] FINAL … text=`, and the human's word. A miss is recorded, never retried. The human's note on the command's level is recorded, not a criterion (take 8). The PM stops the capture.

**Step 9: STOP for the human.** Report the build kind, Step 4's lines, S`apk.txt` whole, Step 6's output, Step 7's lines quoted, `tries.md`, the level note, PM-2 unexercised; no verdict. The human rules F18 on both size figures and on the tries; the PM files the ruling in `tries.md`.

**After.** Commands 1–4 do not run: nothing they cover changes. The as-built reconcile (`architect-reconcile`) adds a `**Phase 6 — …**` entry to `## As-built deltas` and the APK figures to *Size*, within the body's 643 bytes left under 40 KB, detail moved to the hub's dated note as the trim did.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| Holder reuses `host_run.py` | own lock code; no lock | Command 1's lock and checks; brackets wait |
| Release build | Development | the shipped form; the contribution is the same |
| 6000.4.7f1 adb | the recipe's 6000.3.7f1 | one adb server version with the Editor that built |
| 3 heading tries + 1 mode, decoded only | 1; until it passes | F18 states no count: one try cannot tell a miss from chance, three is the least with a majority; no loop; the demo logs no mode switch |
| `cease fire` before the pause | silence from launch | §4 item 3: the pause follows speech; the level is learned first (design §3 (a)) |
| Both sums against the bar | compressed only | stored bytes are the APK's; the ruling's ~49 MB is uncompressed arithmetic |

## Acceptance criteria

1. [ ] APK built, Release or the recorded fallback. (F18)
2. [ ] Step 4 clean, lock released. (take 3)
3. [ ] Bridge entry sha256 = the committed `.so`'s. (WRAP-1, PM-2 deferral)
4. [ ] S`apk.txt`: whole APK; contribution, both sums against 70,000,000; fixtures apart. (F18; §6 Size)
5. [ ] `install -r`: `Success`. (F18)
6. [ ] Step 7's three lines, `agc_target=-18.0 dB`. (F18; WRAP-1)
7. [ ] Four tries, each reported (mode: decoded or not). (F18)
8. [ ] Step 9 reported; the human's ruling filed. (F18; §9)
9. [ ] T's `git status --short -uall`: no path beyond S`tree-before.txt`'s.

## Open questions that block implementation

None. For the human at Step 9, not blocking: take 1's reading — F18 says "the APK size is recorded under 70 MB", which no whole APK of this host meets (132–181 MB today), while §6 Size cites the ruling's contribution — and take 6's count.

## Blast radius

- Host: `Library/` reimports against T; the active target may become Android; `Builds/` gains the APK; another session's Command 1 waits up to its 360 s, then BLOCKED, while held.
- Headset: `com.UnityTestRunner.UnityTestRunner` replaced; its model cache may be re-extracted; the mic permission.
- The architecture body's 643 bytes (the reconcile).

## Runtime claims

- 2, 4: importing `host_run.py` runs no bracket (`main` guarded); the lock frees on the holder's exit.
- 3: the Editor resolves T and writes no file into it; Gradle packs both `.so` byte-identical (the Development APKs did: `libvosk.so` 8,862,928 both).
- 7: a Release player logs `Debug.Log` under `Unity`, `LOGI` under `vosk-bridge`; model extraction ends by 180 s.
- 8: the scene's active sets carry `set_heading` and `mode_navigation` from start.

Hazards: 1 before 2; the Editor closed at 2 and before 4; 4 never waits on 5–9, and a finding holds the lock; the holder's death before 4 leaves H pointed at T — the PM copies S`host-rest/` back and runs 4's checks; 5's mismatch stops before 6; 8 has no retry loop; 9 is a declared human stop; no repo file but the link line.
