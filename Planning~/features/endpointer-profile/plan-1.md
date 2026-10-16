# Phase 1 — persisted plan (2026-10-16)

**Phase 1 - Pause-ladder corpus**, unsplit, serial, no units: F1, §4.5, §10 item 2; bound by §8, the register's *Pause ladder*. MAN = `Tests~/Fixtures/audio/manifest.json`; PIN = `NativeBridge~/harness/expectations.json`; GEN = `Tests~/Fixtures/generate.py`; live lines. Departures, human-ruled 2026-10-16: (1) recipe 2b's count (Step 7); (2) band-check method (Step 3); (3) edge trim (Step 1a).
**Out of scope:** harness README (no count); DocCheck (no rig file/manifest; not in Green); joined rig; CHANGELOG (Phase 7); tier-1 docs' "16" (pre-ladder); Phases 2-8; six-category Tooltip, `Runtime/Testing/VoxrAudioTestCase.cs` :20 (shipped: adds Command 4); `Documentation~/api/scriptable-objects.md` :154 (product docs, post-G2).

## Files to change

| Path | Change | Why |
|---|---|---|
| MAN | modify, +21 | F1; §4.5 |
| GEN | modify | departure 3 |
| `Tests~/Fixtures/audio/tts/ladder_*.wav` | create 21 | F1; §8 |
| PIN | modify: +21 `{file}`, harness-pinned | F1 |
| `Tests~/Runtime/VoxrWavReplayTests.cs` | modify | register: Tier B skip |
| `.claude/bindings/unity.md` | modify :8 | §8 |
| `Planning~/verification-recipe.md` | modify :21 | departure 1 |
| Step 3 probe, table (`.scratch/`) | create, untracked | departure 2 |
| `plan-1.md`; `architecture.md` | create; link line, register | this plan |

## Contracts and interfaces

**Dispatch.** A, `code-writer`: Steps 1, 1a, 2-6 in order, one tree; a stop ends A BLOCKED. The 21 MAN entries and untrimmed WAVs predate the trim ruling, uncommitted: A checks the entries against Step 1, no re-insert. B, `doc-writer`: Step 7 once A ends unstopped. C, `compile-check`: Commands 1-3. No agent commits; Windows-native Git Bash, never WSL; text reports (PM-filed). MAN, PIN stay CRLF, no mixed endings.

**On a stop** (Step 3, 5, 5a) the tree stays uncommitted, Command 3 unrun; after Step 3's, the drift guard (`main.cpp` :224-243) exits 2 on the 21 unpinned until the ruling.

**Recon.** piper venv, voice on disk (`generate.sh` :54-58: no download); `--only` = one `file` substring (GEN :116), only the 21 hold `ladder`; `--write-baseline` pins every case (`main.cpp` :355-356), rewrites the file (:362-367); `expectedFinals` optional (:214); `ManifestCaseKeys` (`JoinedInput.cs` :132-149) rejects other keys.

**Sets** (*The pause ladder*): `segments`; `expectedIntent`; `expectedSlots`. Stem = `p_`/`m_` + the segments' words, `_`-joined. P1-P3 are `set_heading`.
- P1: "set heading two", "seven zero"; heading "two seven zero"
- P2: "orient heading two seven zero", "mark one five"; heading "two seven zero", elevation "one five"
- P3: "orient heading one", "eight zero"; heading "one eight zero"
- M1: "launch missiles target", "hotel one"; `launch_weapon`; weapon "missiles", target "hotel one"
- M2: "fall back from", "target bravo two"; `retreat_from_target`; target "bravo two"
- M3: "approach", "target alpha one"; `approach_target`; target "alpha one"

**The 21**, in order (sets as listed, gaps ascending): basename `ladder_<stem>_gap<ms>ms.wav` (0.2 -> `gap200ms`); `file` `audio/tts/<basename>`; gap = `gapSeconds`; band = effective pause (s). P1-P3 gaps (bands): 0.2 (<0.25), 0.3 (0.25-0.35), 0.4 (0.35-0.5), 0.6 (>0.5). M1-M3: 0.3 (<0.75), 0.6 (<0.75), 0.9 (>0.75).

**Step 1 - MAN.** After the split entry (:166-183), before silence (:184). Keys exactly the split entry's: `file`, `category: "ladder"`, `layer: "recogniser"`, `segments`, `gapSeconds`, `peak: 0.2`, `expectedIntent`, `expectedSlots` (`name`/`value` objects), `expectedTranscript: ""`, a one-line `description` (set, rung); no other.

**Step 1a - trim** (GEN). Add `TRIM_FRAME_S = 0.010`, `TRIM_DBFS = -40.0`, `trim_edges(samples)`: frames from sample 0, RMS dBFS as the probe's `measure`, remainder never speech; keeps the first..last frame >= `TRIM_DBFS`; none -> `sys.exit` like `scale_to_peak`. `build_case` (:64-81) wraps each `scale_to_peak(synth(...), peak)` in it, before the gap, if `case.get("category") == "ladder"`; else (`split` too) unchanged. Header (:8-11) adds the ladder edge trim (10 ms, -40 dBFS, after peak scaling). Why -40: Trade-offs.

**Step 2 - WAVs.** From `Tests~/Fixtures`: `./generate.sh --only ladder_`, overwriting the 21. Check: prints `21 fixture(s) written`; the 21 `ladder_*.wav` modified after GEN's edit; `git status --short "Tests~/Fixtures/audio/tts"` lists only those 21, as new.

**Step 3 - band check.** Run `.scratch/endpointer-profile-band-check.py`, unedited, with `"$HOME/.cache/voxr-fixture-gen/venv/Scripts/python.exe"` (`generate.sh` :22-28; numpy present): 10 ms RMS dBFS frames; pause = longest below-threshold run between first and last speech frame; at -40 and -50 dBFS, same bands. 21-row table (file, gap, pause@-40, pause@-50, band, verdict) to `.scratch/endpointer-profile-phase-1-band-check.md`. **Stop:** a rung out of band at either -> BLOCKED to the hub for the human's ruling; no own-judgment trim or regeneration.

**Step 4 - PIN entries.** 21 `{ "file": "<basename>" }`, MAN's order: after the split case (:88-94), before silence (:95-98). No `expectedFinals`.

**Step 5 - pin.** From `NativeBridge~`: Command 3's desktop configure and build lines, then `./build-windows/bin/vosk-bridge-harness.exe --model vendor/vosk-model-small-en-us-0.15 --fixtures "../Tests~/Fixtures/audio/tts" --manifest harness/expectations.json --write-baseline`. Check: exit 0, `baselineWritten: true`; `git diff HEAD --` PIN: only 21 new entries (`expectedFinals`, `file`); the 16 cases, `gain`, `grammar`, `libvosk`, `_comment` byte-unchanged. **Stop:** an existing case moved -> `git checkout --` PIN, re-add Step 4's entries, BLOCKED for a ruling; never a re-pin. A ladder case's two finals are measurement, not defect.

**Step 5a - joined words.** Per ladder case, the pinned `expectedFinals` (trimmed, lowercased as the harness writes them) joined by single spaces equal MAN's `segments` joined so. **Stop:** a mismatch -> tree as is (PIN pinned, uncommitted), BLOCKED to the hub, the human rules; never a re-pin or re-trim.

**Step 6 - Tier B skip.** `ManifestFileNames` (`VoxrWavReplayTests.cs` :128-132) yields only `category != "ladder"` cases; a one-line comment: replay asserts one full command; a rung past threshold splits by design. EditMode corpus tests (`VoxrFixtureCorpusTests.cs` :98, :135, :148: peak, listing, shape) still cover 37.

**Step 7 - counts** (`doc-writer`). The binding's Command 3 (:8) and the recipe's step 2b (:21): "16/16" -> "37/37"; nothing else.

**Verification** (`compile-check`). Command 3 as edited (arm64 build, desktop harness): exit 0, 37/37; pin diff is Step 5's. Commands 1-2: EditMode green, corpus tests over 37; PlayMode green, 16 `Replay_ManifestCase` instances; then revert. Unity-made `memory/**/*.meta` committed, never deleted.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| `--only ladder_` | regenerate all | piper not bit-deterministic (§8) |
| Skip in `ManifestFileNames` | test body | register names it; drops discovery |
| -40 and -50 dBFS | one threshold | verdict not threshold-bound |
| Probe in `.scratch/` | committed tool | Phase 1 only (departure 2) |
| Trim at -40, ladder only | -50; all split entries | probe's upper threshold, both read the bare gap; -50 leaves -50..-40 edges in pause@-40 (P1 0.3: 0.70 vs 0.63) |

## Acceptance criteria

1. [ ] MAN holds the 21 as listed, Step 1's keys only; basenames unique, all `ladder_`. (F1; §4.5; §10 item 2)
2. [ ] GEN trims per Step 1a; Step 2's check holds. (F1; §4.5; §8)
3. [ ] Band table filed, 21 rows, all in band at both thresholds. (§4.5)
4. [ ] Step 5's check holds (PIN diff harness-written, the 21 only); Step 5a's words match, all 21. (F1)
5. [ ] `ManifestFileNames` excludes `ladder`, comment present. (F1)
6. [ ] Binding :8 and recipe :21 read "37/37". (§8)
7. [ ] Command 3: `libvosk-bridge.so` builds; harness exit 0, 37/37. (F1; §9)
8. [ ] Commands 1-2 green per Verification; revert clean. (§9)

## Open questions that block implementation

None; a stop opens one.

## Blast radius

- Joined rig reads MAN's keys (`ManifestCaseKeys`) and PIN (`ReadPin` needs `expectedFinals`: a bare entry fails until Step 5); Phases 3, 5 sweep the 37.
- Later: Command 3, EditMode see 37, PlayMode 16; Phase 2 adds PIN `modelConf`; Phase 7 re-pins, ladder included; GEN trims only the ladder.

## Runtime claims

- a (Step 2): `./generate.sh --only ladder_` synthesises the 21 segment pairs (lone "approach" included) without error, no other WAV.
- b (Step 5): `--write-baseline` exits 0, `baselineWritten: true` (a case error skips the write, `main.cpp` :362); the 16 pins, other keys byte-identical; alphabetical `dump(4)` matches the committed form; text-mode `std::ofstream` (:363, :366) writes its CRLF.
- c (criterion 8): ladder WAVs pass `EveryFixture_Exists_Parses_AndPeaksInItsRange` (peak in [0.04, 0.4], :31-32).
- d (Step 6): the filter drops the ladder from PlayMode discovery, `ValueSource` intact.
- e (Step 3): each rung's pause is in band at both thresholds. Premise: measured pause = max(gap, longest intra-segment below-threshold run); trimmed, the gap reads within a frame (headroom 0.05 s at P 0.2, P 0.3); at both, speech lies inside the 0.3 s lead and 1.0 s tail.
- f (Step 5): the 16 cases still decode to their committed finals here (the stop rule's premise; `--write-baseline` forcing pass, `main.cpp` :352).
- g (Step 5): a second, normal run reproduces all 37, exit 0 (the 21 decode deterministically).
- h (Commands 1-2): rename `Tests~`->`Tests` and back, 21 new WAVs included, leaves no orphans; results XML: 16 `Replay_ManifestCase`.
- i (Command 3 arm64 half): builds, `NativeBridge~/src` unchanged; tracked arm64-v8a `libvosk-bridge.so` unmodified (recipe :22).
- j (Steps 1a, 2): every segment keeps a frame >= -40 dBFS; peaks stay 0.200; lead and gaps are whole frames: trimmed edges sit on the probe's 10 ms grid.
- k (Step 3 checks): no intra-segment below-threshold run exceeds the rung's gap or band cap.
- l (Steps 1a, 3): float32 trim-time and int16 probe-time RMS agree on kept edge frames within one frame.
- m (Step 5a): the trim clips no speech: every command's words decode in full.
