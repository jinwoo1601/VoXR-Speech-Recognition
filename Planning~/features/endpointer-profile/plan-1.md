# Phase 1 — persisted plan (2026-10-16)

**Phase 1 - Pause-ladder corpus**, unsplit, serial, no units: F1, §4.5, §10 item 2; bound by §8 and the register's *Pause ladder*. MAN = `Tests~/Fixtures/audio/manifest.json`; PIN = `NativeBridge~/harness/expectations.json`; live lines. Human-approved 2026-10-16 with two departures from the Build-plan row: (1) the recipe's step 2b count beside the binding (Step 7); (2) the band-check method (Step 3).
**Out of scope:** the harness README (no count); DocCheck (no rig file touched, it reads no manifest, the row's Green omits it); the joined rig; CHANGELOG (Phase 7); tier-1 docs stating "16" (pre-ladder corpus); Phases 2-8. Left as is: `Runtime/Testing/VoxrAudioTestCase.cs` :20, Tooltip listing the six categories (shipped source; editing widens the phase, triggers Command 4); `Documentation~/api/scriptable-objects.md` :154 (product docs, after G2).

## Files to change

| Path | Change | Why |
|---|---|---|
| MAN | modify, +21 | F1; §4.5 |
| `Tests~/Fixtures/audio/tts/ladder_*.wav` | create 21, by `generate.sh` | F1; §8 |
| PIN | modify: +21 `{file}`, then harness-pinned | F1 |
| `Tests~/Runtime/VoxrWavReplayTests.cs` | modify | register: Tier B skip |
| `.claude/bindings/unity.md` | modify :8 | §8 |
| `Planning~/verification-recipe.md` | modify :21 | departure 1 |
| Step 3's `.scratch/` probe and band table | create, untracked | departure 2 |
| `plan-1.md`; `architecture.md` | create; link line | this plan |

## Contracts and interfaces

**Dispatch.** A, `code-writer`: Steps 1-6 in order, one tree; a stop ends it BLOCKED. B, `doc-writer`: Step 7, only after Steps 1-6 complete with no stop. C, `compile-check`: Commands 1-3. No agent commits; Windows-native Git Bash, never WSL; reports as text (PM-filed).

**On a Step 3 or 5 stop** the tree is left uncommitted as it stands; Command 3 is not run. A Step 3 stop leaves MAN and WAVs without PIN entries, so the harness drift guard (`main.cpp` :224-243) exits 2: expected red until the ruling.

**Recon facts.** piper venv and voice on disk; `generate.sh` downloads nothing (:54-58), no human step. `--only` is one substring of `file` (`generate.py` :116); no MAN entry contains `ladder`. `--write-baseline` forces pass (`main.cpp` :352), pins every case (:355-356), rewrites the file (:362-367); `expectedFinals` optional (:214). `ManifestCaseKeys` (`JoinedInput.cs` :132-149) rejects other keys.

**Sets** (*The pause ladder*; `segments` · `expectedIntent` · `expectedSlots`):

| Set | Stem | Segments | Intent | Slots |
|---|---|---|---|---|
| P1 | `p_set_heading_two_seven_zero` | "set heading two", "seven zero" | `set_heading` | heading "two seven zero" |
| P2 | `p_orient_heading_two_seven_zero_mark_one_five` | "orient heading two seven zero", "mark one five" | `set_heading` | heading "two seven zero", elevation "one five" |
| P3 | `p_orient_heading_one_eight_zero` | "orient heading one", "eight zero" | `set_heading` | heading "one eight zero" |
| M1 | `m_launch_missiles_target_hotel_one` | "launch missiles target", "hotel one" | `launch_weapon` | weapon "missiles", target "hotel one" |
| M2 | `m_fall_back_from_target_bravo_two` | "fall back from", "target bravo two" | `retreat_from_target` | target "bravo two" |
| M3 | `m_approach_target_alpha_one` | "approach", "target alpha one" | `approach_target` | target "alpha one" |

**The 21 entries**, in insertion order; basename = `ladder_` + the set's stem + `_gap` + gap in ms + `ms.wav` (P1 at 0.2: `ladder_p_set_heading_two_seven_zero_gap200ms.wav`); `file` = `audio/tts/` + basename; gap = `gapSeconds`; band = effective pause (s):

| Set | Gap | Band |
|---|---|---|
| P1 | 0.2 | <0.25 |
| P1 | 0.3 | 0.25-0.35 |
| P1 | 0.4 | 0.35-0.5 |
| P1 | 0.6 | >0.5 |
| P2 | 0.2 | <0.25 |
| P2 | 0.3 | 0.25-0.35 |
| P2 | 0.4 | 0.35-0.5 |
| P2 | 0.6 | >0.5 |
| P3 | 0.2 | <0.25 |
| P3 | 0.3 | 0.25-0.35 |
| P3 | 0.4 | 0.35-0.5 |
| P3 | 0.6 | >0.5 |
| M1 | 0.3 | <0.75 |
| M1 | 0.6 | <0.75 |
| M1 | 0.9 | >0.75 |
| M2 | 0.3 | <0.75 |
| M2 | 0.6 | <0.75 |
| M2 | 0.9 | >0.75 |
| M3 | 0.3 | <0.75 |
| M3 | 0.6 | <0.75 |
| M3 | 0.9 | >0.75 |

**Step 1 - MAN.** Insert the 21 after the split entry (:166-183), before silence (:184). Keys exactly the split entry's: `file`, `category: "ladder"`, `layer: "recogniser"`, `segments`, `gapSeconds`, `peak: 0.2`, `expectedIntent`, `expectedSlots` (`name`/`value` objects), `expectedTranscript: ""`, a one-line `description` naming set and rung. No other key. MAN stays CRLF throughout, no mixed endings.

**Step 2 - WAVs.** From `Tests~/Fixtures`: `./generate.sh --only ladder_`. Check: `git status --short "Tests~/Fixtures/audio/tts"` shows exactly 21 new `ladder_*.wav`, no modified WAV.

**Step 3 - band check.** Write `.scratch/endpointer-profile-band-check.py`; run it with `"$HOME/.cache/voxr-fixture-gen/venv/Scripts/python.exe"` (`generate.sh` :22-28; numpy present). 10 ms frames, RMS in dBFS; effective pause = the longest contiguous below-threshold run between the first and last speech frame (excludes the 0.3 s lead, 1.0 s tail); measured at -40 and at -50 dBFS, bands the same at both. Output a 21-row table (file, gap, pause@-40, pause@-50, band, verdict) to `.scratch/endpointer-profile-phase-1-band-check.md`. **Stop:** any rung out of band at either threshold -> BLOCKED to the hub, wait for the human's ruling; no trimming, no regeneration on own judgment.

**Step 4 - PIN entries.** 21 `{ "file": "<basename>" }`, MAN's order and position: after the split case (:88-94), before silence (:95-98). No `expectedFinals`. PIN stays CRLF throughout, no mixed endings.

**Step 5 - pin.** From `NativeBridge~`: Command 3's desktop configure and build lines, then `./build-windows/bin/vosk-bridge-harness.exe --model vendor/vosk-model-small-en-us-0.15 --fixtures "../Tests~/Fixtures/audio/tts" --manifest harness/expectations.json --write-baseline`. Check: exit 0, `baselineWritten: true`; `git diff HEAD -- NativeBridge~/harness/expectations.json` shows 21 new entries, each `expectedFinals` then `file`, and nothing else changed: the 16 existing cases, `gain`, `grammar`, `libvosk`, `_comment` byte-unchanged. **Stop:** any existing case moved -> `git checkout -- NativeBridge~/harness/expectations.json`, re-add Step 4's entries, BLOCKED, wait for the human's ruling; never a re-pin. A ladder case pinned as two finals is the measurement, not a defect.

**Step 6 - Tier B skip.** `ManifestFileNames` (`VoxrWavReplayTests.cs` :128-132) yields only cases with `category != "ladder"`, with a one-line comment: the replay asserts one full command, and a rung past the threshold splits by design. The EditMode corpus tests (`VoxrFixtureCorpusTests.cs` :98, :135, :148: peak, listing, shape) still cover all 37.

**Step 7 - counts** (`doc-writer`). `.claude/bindings/unity.md` Command 3 (:8) and `Planning~/verification-recipe.md` step 2b (:21): "16/16" -> "37/37"; nothing else.

**Verification** (`compile-check`). Command 3 per the edited binding: arm64 build plus desktop harness, exit 0 with 37/37; the pin diff is Step 5's harness output. Commands 1-2: EditMode green, corpus tests over 37 entries; PlayMode green, `Replay_ManifestCase` instances still 16; then the revert. Unity-made `memory/**/*.meta` are committed, never deleted.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| `--only ladder_` | regenerate all | piper is not bit-deterministic (§8) |
| Skip in `ManifestFileNames` | skip in the test body | the register names it; drops discovery |
| -40 and -50 dBFS | one threshold | verdict not threshold-bound |
| Probe in `.scratch/` | committed tool | Phase 1 only (departure 2) |

## Acceptance criteria

1. [ ] MAN holds the 21 as tabled, Step 1's keys only; basenames unique, each with `ladder_`. (F1; §4.5; §10 item 2)
2. [ ] Step 2's check holds. (F1; §8)
3. [ ] Band table filed, 21 rows, all in band at both thresholds. (§4.5)
4. [ ] Step 5's check holds: the PIN diff is harness-written, the 21 only. (F1)
5. [ ] `ManifestFileNames` excludes `ladder`, comment present. (F1)
6. [ ] Binding :8 and recipe :21 read "37/37". (§8)
7. [ ] Command 3: `libvosk-bridge.so` builds; harness exit 0, 37/37. (F1; §9)
8. [ ] Commands 1-2 green as Verification says; revert clean. (§9)

## Open questions that block implementation

None. A Step 3 or Step 5 stop opens one for the human.

## Blast radius

- Joined rig: reads MAN's keys (`JoinedInput.cs` :132-149) and the PIN (`ReadPin` needs `expectedFinals`; a bare entry fails until Step 5); Phases 3 and 5 sweep the 37.
- Later runs: Command 3 decodes 37, EditMode corpus tests 37, PlayMode discovery 16; Phase 2 adds `modelConf` to the PIN; Phase 7 re-pins it, ladder included.

## Runtime claims

- a (Step 2): `./generate.sh --only ladder_` (Git Bash) synthesises all 21 segment pairs without error, single-word "approach" included, and no existing WAV.
- b (Step 5): `--write-baseline` exits 0, `baselineWritten: true` (a case error skips the write, `main.cpp` :362); the 16 pins and other keys stay byte-identical; alphabetical `dump(4)` matches the committed form; text-mode `std::ofstream` (:363, :366) writes CRLF like it.
- c (criterion 8): ladder WAVs pass `EveryFixture_Exists_Parses_AndPeaksInItsRange` (peak in [0.04, 0.4], :31-32).
- d (Step 6): the filter removes the ladder from PlayMode discovery without breaking `ValueSource`.
- e (Step 3): every rung's effective pause is in band at -40 and -50 dBFS, resting on piper's edge silence (headroom 0.05 s at P 0.2, P 0.3); at both, first and last speech frames fall inside the speech, excluding lead and tail.
- f (Step 5): the 16 existing cases still decode to their committed finals on this host: the stop rule's premise, as `--write-baseline` forces pass (:352).
- g (Step 5): a second, normal run on the pinned file reproduces all 37, exit 0 (decode determinism for the 21).
- h (Commands 1-2): the `Tests~`->`Tests` rename and revert, with 21 new WAVs, leaves no orphans; results XML shows 16 `Replay_ManifestCase` instances.
- i (Command 3 arm64 half): builds with `NativeBridge~/src` unchanged, leaving tracked `Runtime/Plugins/Android/arm64-v8a/libvosk-bridge.so` unmodified (`Planning~/verification-recipe.md` :22).
