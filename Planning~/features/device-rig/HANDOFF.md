# Handoff — feat-device-rig (Tier D), 2026-08-09

> **SUPERSEDED 2026-08-11 — do not pick this up.** Tier D was dropped (design Amendment A1); the project stays at Tiers A/B/C. The branch described below has been **deleted** and archived at tag `archive/tier-d` (`ff3ea62`). Phase 5 was never started and is not planned. This file is kept as the record of exactly where the build stopped, should Tier D ever be revisited (`git checkout -b <name> archive/tier-d`).

## State: Phases 0–4 committed and green on hardware. Phase 5 (loopback + lifecycle, F8/F9) is next and needs a quiet room.

Branch `feat-device-rig`, off merged `main`. Working tree clean. No PR open.

- `9639038` Phase 0 · `1e2214c` Phase 1 (F16) · `5cca228` Phase 2 (F2–F4/F11/F15)
- `b1a5f99` — first device contact; four defects fixed, F1's mechanism replaced.
- `c2707cc` — Phase 3, push-audio (F5). 16/16 fixtures identical to Tier C's x86_64.
- `ff3ea62` — **Phase 4, mic liveness + falsifiability (F6/F7).**

`Planning~/` is gitignored, so the process docs are disk-only by design.

## Phase 4's result

Both directions green on Quest 3 `2G0YC1ZF960XJT`:

| | `./run.sh` | `./run.sh --negative-control` |
|---|---|---|
| max input level | `0.000079` | `0.000000` |
| `recognising` | `True` | `False` |
| verdict | `green` (5/0) | `negative-control-falsified` (5/1), exit 0 |

Baselines to preserve: EditMode **116/116**; PlayMode **309 = 304 passed + 5 skipped, 0 failed**; selftest **93 assertions, 0 failures**; device suite **5 tests**.

## THE PIN IS NOW MANDATORY

Run every device command as:

```bash
VOXR_DEVICE_PIN="$(cat ~/.voxr-device-pin)" ./run.sh
```

The PIN file is written by the human, mode 600. **Without it every run fails**: Horizon OS intercepts the app launch with a "controllers required" dialog (`ActivityLaunchInterceptorController: RequiresControllersLaunchInterceptor`) and the test player never starts, so the run can only time out. `pinStep=applied` in prep's output is the confirmation. Env vars do not survive between Claude Bash calls, so exporting it separately does not reach the run.

**This falsifies decision D7** ("the PIN is an optional convenience"). Recorded in §9, flagged for amendment at close-out, **not yet ruled by the human**.

## Read first

`architecture.md` — **§4.4** (F8 loopback, the Phase 5 spec), **§4.5** (F9 lifecycle, with its ratified `prox_far` correction), **§5 D3** (latency trended, not gated) and **D5** (real broadcasts, not `SendMessage`), **§7**'s Phase 5 entry, and **§9's Phase 4 entry**. Then `Tests~/DeviceRig/README.md` — the F15 runbook, now covering `--negative-control` and three things that will bite.

## What Phase 4 changed in the rig

- `run.sh --negative-control` — revokes `RECORD_AUDIO`, skips the grant watcher, rules on an inverted criterion, re-grants in the EXIT trap. Success slug `negative-control-falsified` (exit 0), deliberately **not** `green`.
- `prep.sh --set-mic grant|revoke|verify-revoked` — all targeting `VOXR_RIG_TEST_PACKAGE`. The revoke also sets `USER_FIXED`; the grant clears it.
- Classifier order corrected: the heartbeat pattern is checked **before** `rc=124`.
- `run.sh --print-expected` derives the floor from `Tests~/Device/` — adding tests raises it automatically (now 5).

## Things that will bite

- **A test's own timeout does not bound anything that raises a system dialog.** Horizon OS suspends the player behind a dialog, and a suspended player does not tick `yield return null`. Prevent the dialog; do not try to survive it.
- **`timeout` does not kill Windows `Unity.exe`.** Survivors hold `<host>/Temp/UnityLockfile` and wedge the host; the lockfile cannot even be deleted from WSL while held. **WSL's `ps` cannot see them** — use `tasklist.exe` / `taskkill.exe`, checking the command line first. Killing is currently manual by deliberate choice.
- **Fixture ordering cannot be controlled.** `[Order]` on a class is CS0592 here, and would insert the fixture at the *front* anyway. Fixtures run alphabetically; `VoxrDeviceMicLivenessTests` runs first.
- **Nothing under `Tests~/` may be executing — or be the shell's cwd — when `run.sh` renames it.** Keep the session at the repo root.
- **NUnit repeats a failure message at every nesting level** (nine copies). Scrape the first and stop; prefer whole-file greps for sentinels.
- `rm` is blocked — use `find … -delete`.

## Next steps

1. **Confirm the quiet room with the human** — F8 is acoustic and Phase 5 cannot start without it.
2. Check hardware: `adb devices` via `rig.env`'s `VOXR_RIG_ADB`. Empty → stop and report deferred.
3. Invoke **`phase-pickup`** for **Phase 5**, with this state: spec is architecture §4.4 (F8) and §4.5 (F9), plus F10's twice-and-shuffled run and F15's closure; `VoxrDeviceMicLivenessTests.cs` is the pattern for a capture-mode device test; editor EditMode run is the first compile gate; the PIN is mandatory.
4. Phase 6 is close-out (F12–F14) plus the D7 amendment. No PR until the feature is complete; G2 is the human's explicit ruling on the open PR.

## Open questions

- **D7's amendment wording** — the human has not ruled on it.
- **Should `run.sh` kill surviving Windows Unity processes automatically?** Raised, not decided; currently manual.
- **F8's calibration artifact does not exist yet** (`Tests~/Fixtures/audio/loopback-calibration.json`), nor the trend file (`latency-trend.json`). Both are Phase 5 deliverables per §4.4.
- **Whether the UTF player emits audio at all** is unverified — §4.4 warns a UTF-generated scene has no `AudioListener` and the test must create one.
