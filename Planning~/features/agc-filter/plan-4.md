# Phase 4 — persisted plan (2026-11-06)

Scope: the row, plus rulings 1-3 (named under Contracts) and one pin-reader line in `JoinedInput.cs`. Serial, this tree. NB = `NativeBridge~/`, R = `Planning~/features/coverage-in-selection/ab-rig/`, S = `.scratch/agc-filter/` (ignored), Reg = register bullet, C3 = Command 3 verbatim (arm64 half: recipe :51-63).

## Files to change

| Path | Change | Why |
|---|---|---|
| NB`src/vosk_bridge.cpp`, `.h` | modify | F2, F9, F10 |
| NB`src/downsampler.h`, `agc.h` | delete | F2 |
| NB`CMakeLists.txt` | modify | F2, F3; take 5 |
| NB`harness/main.cpp`; `expectations.json` | modify; harness-written | F9, F10, F14; ruling 2 |
| NB`harness/README.md` | modify (`doc-writer`: prose, Phase 1's precedent) | ruling 3 |
| R`JoinedReport.cs`, R`JoinedInput.cs` | modify | ruling 1; pin reader |
| `Runtime/Plugins/Android/arm64-v8a/libvosk-bridge.so` | generated | F3; §6 |
| `THIRD_PARTY_NOTICES.md` | modify | F16 |
| S`b.jsonl`, `ab-*/`, `bb-parser/` | untracked | F13 |

## Contracts and interfaces

Edit, never Write; NB is CRLF (a Write: the granted CRLF rewrite), others keep theirs. `.claude/references/core/implementation-rules.md` rule 1: surgical, body comments go in each edited definition; no new comment. No agent commits.
Human rulings, pickup 4 (Reg amendments): 1, the retention line's `[unk]` skip (Step 5); 2, the harness owns `_comment` (Step 4); 3, digests host-scoped, no source patch, said by README and `_comment` (Steps 4, 16). Take 5 (PM, fork default): the arm64 prefix map (Step 2).

**Step 1: baseline.** C3: exit 0, 52/52; S`a.jsonl` sha256 = Reg *The A/B*'s; the `.so` 56752 bytes. Else BLOCKED.

**Step 2: CMake.** Drop :30-31. After :42, `if(ANDROID)`: `add_compile_options(-ffile-prefix-map=${CMAKE_SOURCE_DIR}=voxr-src -ffile-prefix-map=${CMAKE_BINARY_DIR}=voxr-build)`, before :47 so abseil and `webrtc_agc2` inherit it. :124-126 add `voxr-frontend-core`; :138 appends `-Wl,--exclude-libs,ALL`.

**Step 3: bridge.** `front_end.h`, `<memory>` replace :4-5; `static std::unique_ptr<voxr::FrontEnd> g_frontend;` replaces :27-28. :56 becomes `kInt16Capacity = kReadChunkSize / voxr::Decimator::kFactor + voxr::FrontEnd::kFrameSamples` (Reg *Frame buffering*); :119 uses `voxr::Decimator::kFactor`. Delete `float_to_int16`, `downsampled_buf`; `int16_buf` is `int16_t[kInt16Capacity]`. `int count = g_frontend->Process(read_buf, (int)read_count, int16_buf, kInt16Capacity)` replaces :127-134; `count > 0` gates :136-139 (`count` for `ds_count`). `reset_pipeline` drops :73-74. Per the Lifecycle table: `BeginStream()` after :196; init's `g_frontend = std::make_unique<voxr::FrontEnd>(mic_gain_target_db)` replaces :258; `Reset()` before :290 and, unconditionally, after :328; stop none. `publish_frontend_stats()` follows each, and each `Process`.
Publish (one writer: the loop, or a call with it joined): `std::atomic<uint32_t> g_fe_seq`, six atomics for `Stats`. Writer: seq odd, release fence, six relaxed stores from `GetStats()`, seq even (release). Reader: retry until seq is even and unchanged across an acquire fence.
After :75: `VOSK_BRIDGE_EXPORT int vosk_bridge_get_frontend_stats(uint64_t* out_input_samples, uint64_t* out_output_samples, uint64_t* out_digest, float* out_gain_db, float* out_pre_rms, float* out_post_rms);` — not initialised: `VOSK_BRIDGE_ERR_NOT_INITIALISED`, nothing written; else each non-null filled, `VOSK_BRIDGE_OK`.

**Step 4: harness.** *Wait*: poll the export until `input_samples` = the fixture's sample count, no drain; ~10 s without progress (2000 × 5 ms) is an error. *Lifecycle check*, after init, before :301: `cases[0]`'s WAV, `dump_writer.enabled` off then restored; g0 cold gain; start_push, PushAll, wait, stop: g1; start_push: g2; stop, `vosk_bridge_reset`: g3. Pass iff g1 != g0, g2 == g1, g3 == g0; else `vosk_bridge_destroy()`, `Fail` naming all three. *Pins*: between :339 and :340, wait, then `frontEndSamples` = `output_samples`, `frontEndDigest` = 16 lowercase hex digits. Schema (:229-240): a present key of another type exits 2; absent is a mismatch. `pass` needs both equal; `--write-baseline` writes both beside :378, and `_comment` beside :382 from one constant: today's text, adding that the two keys pin the front end's output per fixture before padding, harness-written and host-scoped (README), and that regenerating the corpus (`pause-lead` cases included), bumping libvosk or changing the front end re-baselines. Report cases gain the two and `frontEndPass`. Dump records unchanged.

**Step 5: rig.** :1036 compares the first token not `[unk]` (none: not retained). `ReadPin` :649 admits the two keys, unread. DocCheck (recipe :229): exit 0, no `FAIL`, `<n>/<n>`.

**Step 6: C3, exit 1 by design.** arm64 builds; harness exit 1, `total` 52, every `errors` empty, `passed` 0: expected, not a loop. Exit 2: stop. dumpbin `vosk-bridge.dll`: the 17 `vosk_bridge_*`, no CRT dependent.

**Step 7: side B.** From NB, C3's harness + `--dump ../.scratch/agc-filter/b.jsonl`: exit 1, 52 cases, no errors; sha256 recorded.

**Step 8: A/B.** From the root, `bash R/joined.sh --vary decoder --dump-a S/a.jsonl --dump-b S/b.jsonl --ref WORKTREE --out S/ab-parser`; again `--layer recogniser --out S/ab-recogniser`: exit 0, `Kill check: A passed`.

**Step 9: STOP for the human.** Report both `report.md`, F13 rows A/B per layer, retention k/n, every flip with transcripts, pass→fail apart; no verdict. The human reads F13's kill criteria and rules; a kill ends the feature.

**Step 10: re-pin.** From NB, C3's harness + `--write-baseline`: exit 0, `baselineWritten` true. `git diff --ignore-cr-at-eol`: `_comment`, moved `expectedFinals`, the two keys on all 52; nothing else; lone-LF 0. Else BLOCKED; never a hand edit or second run.

**Step 11.** Step 8's first command, S`b.jsonl` as both `--dump-a` and `--dump-b`, `--out S/bb-parser`: exit 0, `Kill check: A passed`, `Flips: 0`. The kill check holds side A to `expectations.json` (`joined-rig.md` :108): the re-pin took side B's finals.

**Step 12: C3**: exit 0, 52/52.

**Step 13: `.so`.** `mv` NB`build` to NB`build-recipe`; from the root, `env -u MSYS_NO_PATHCONV PATH="<recipe CMAKE's dir>:$PATH" CMAKE_GENERATOR=Ninja ANDROID_NDK_HOME="<recipe NDK_WIN>" bash NativeBridge~/build.sh`: exit 0; `mv` NB`build` to NB`build-sh`, NB`build-recipe` back. Record size, growth over 56752, sha256. NDK `llvm-nm -D --defined-only`: exactly 18, the header's 17 `vosk_bridge_*` and `JNI_OnLoad`. `grep -a`: no `NativeBridge~`, no `pwd -W` root. `git status --short -- Runtime/`: the `.so` only.

**Step 14: notices.** After OpenFST, VOSK's form, texts verbatim from NB`build/_src/`: WebRTC (BSD-3; `webrtc/LICENSE`, `webrtc/PATENTS`), rnnoise weights (BSD-3; `webrtc/third_party/rnnoise/COPYING`), PFFFT (in the closure, `webrtc_agc2.cmake` :122; its `LICENSE`), abseil (Apache-2.0, VOSK's text cited; its `NOTICE` if any). Ooura, `spl_sqrt_floor`, `modules/third_party/fft` are unlisted: no entry. `.meta` untouched.

**Step 15: Commands 1–2**, one bracket, green: the package with the new `.so`; no C# changed.

**Step 16: `doc-writer`.** README :3 the chain via the front end; :78 exit 1 a pin mismatch, exit 2 the check; :80-85 guards add it; `## The baseline` the pins, the harness's `_comment`; :93-100 a front-end change re-pins only on the human's ruling; digests host-scoped (Reg *Parity*).

**Gate.** Steps 12-15; Step 16 follows it, prose only, re-running nothing. As-built: Step 13's figures, S's reports, Step 9's ruling.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| `unique_ptr`, made at init | Static object | target is a constructor argument |
| Seqlock over atomics | Mutex; plain copy | no lock per chunk, no race |
| Digest as hex | JSON number | doubles drop bits past 2^53 (Phase 5) |
| Check before `set_grammar` | After (brief) | :387 zeroes `g_decoder_fed`: origins as today (:341-344) |
| g1 != g0 required | Equalities only | else it cannot fail |
| `ReadPin` admits keys | Untouched | unknown case keys stop later rig runs |
| Fresh `build/` | Reuse | no `--fresh` (recipe :3); android-29 vs 27 |

## Acceptance criteria

1. [ ] Old chain gone from NB`src/`. (F2)
2. [ ] Step 13 as stated. (F3; §6)
3. [ ] Check passes, 6 and 12. (F10)
4. [ ] Steps 10-11. (F9, F14)
5. [ ] Step 12. (row)
6. [ ] Steps 8-9; Step 9's ruling recorded. (F13)
7. [ ] Step 5. (F13)
8. [ ] Step 14. (F16)
9. [ ] Step 15. (row)
10. [ ] NB lone-LF 0; `git status --short -uall`: table paths only.

## Open questions that block implementation

None.

## Blast radius

- `vosk-bridge.dll`; the probe stops building (plan-1 :71); Phase 5 and later rig runs read the pin.
- `Documentation~/native-bridge.md`, `KNOWN_LIMITATIONS.md` stale to G2.

## Runtime claims

- 2, 13: the options reach abseil; clang maps `__FILE__`; `--exclude-libs` hides archives only.
- 3: `Process` never returns -1 at `kInt16Capacity`.
- 4, 6: `cases[0]` moves the gain (else exit 2: stop); transcripts and digests repeat on one host.
- 7-8, 11: a dump completes on exit 1; `--dump` finals equal a plain run's.
- 13: unset `MSYS_NO_PATHCONV` converts `build.sh`'s paths.

Hazards: 1 before any edit; 2-4 one block, built at 6; 5 before 8; 6's exit 1 is by design, exit 2 stops; 7-8 before 10; 9 is a declared human stop; 10 once; 13 after 12, `build/` restored before 14; NB`vendor/` never written; 16 last, after the gate (12-15): prose only, no re-run.
