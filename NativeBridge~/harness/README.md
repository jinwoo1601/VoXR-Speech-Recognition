# Tier C Desktop Harness — WAV → transcript regression on the Windows-native bridge

Replays the committed fixture corpus (`Tests~/Fixtures/audio/tts/`) through the **real native bridge** (ring buffer → front end (`voxr::FrontEnd`: 48 kHz decimated to 16 kHz, buffered into 10 ms frames, levelled by the AGC2 chain, emitted as int16) → Vosk) compiled Windows-native with MSVC, and compares the final transcripts against the committed baseline in `expectations.json`. Runs entirely on the Windows host in Git Bash — no Unity, no device. Design contract: `Planning~/design-docs/automated-verification.md` §7 (Tier C).

What it verifies: bridge *logic* against a real Windows libvosk 0.3.45, the same DLL the Editor ships. What it is blind to: the arm64 binary, JNI AudioRecord capture, and Quest audio routing.

## One-time provisioning

Everything lands in `NativeBridge~/vendor/` (gitignored, never committed). Run each code block below from the project root in Git Bash; each block returns there when it ends.

1. **Toolchain**: Visual Studio 2022 Community with the x64 C++ tools (the "Desktop development with C++" workload), at its default path `C:/Program Files/Microsoft Visual Studio/2022/Community`; Unity 6000.3.7f1 with Android Build Support, whose SDK ships the cmake 3.22.1 the build command uses by absolute path; the .NET SDK 10 (`dotnet` on PATH) for `confgen`. The shell is Git Bash.
2. **libvosk** — Alphacephei's Windows prebuilt, pinned **0.3.45** (must match `expectations.json`'s `libvosk` pin; a different version is a conscious re-baseline). Download, check the zip's SHA-256, then extract:
   ```bash
   mkdir -p NativeBridge~/vendor && cd NativeBridge~/vendor
   curl -LO https://github.com/alphacep/vosk-api/releases/download/v0.3.45/vosk-win64-0.3.45.zip
   echo "f1dcc9cca460630f81ea8f71794f69c80bed6556d2a4e6237b5785e1d2dff34b  vosk-win64-0.3.45.zip" | sha256sum -c -
   unzip vosk-win64-0.3.45.zip
   cd ../..
   ```
   The check must print `vosk-win64-0.3.45.zip: OK`. On a mismatch, stop: do not extract, and raise it (adr-0004). The result is `vendor/vosk-win64-0.3.45/` holding its six files exactly as unzipped (`libvosk.dll`, `libvosk.lib`, `vosk_api.h`, `libstdc++-6.dll`, `libgcc_s_seh-1.dll`, `libwinpthread-1.dll`); nothing is copied up a level.
3. **Model** — the same small English model the package uses on-device, `vosk-model-small-en-us-0.15`. Either unzip the copy already in the host test project (`VoXR TestGround/Assets/StreamingAssets/vosk-model-small-en-us-0.15.zip`) into `vendor/`, or download it from <https://alphacephei.com/vosk/models>:
   ```bash
   mkdir -p NativeBridge~/vendor && cd NativeBridge~/vendor
   curl -LO https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip
   unzip vosk-model-small-en-us-0.15.zip
   cd ../..
   ```
4. **AGC2 source** — the two archives the agc-filter feature compiles AGC2 from (adr-0003), each pinned by SHA-256: `webrtc-audio-processing` **2.1** and the abseil archive its `subprojects/abseil-cpp.wrap` names. Download each, then check its SHA-256:
   ```bash
   mkdir -p NativeBridge~/vendor && cd NativeBridge~/vendor
   curl -LO https://freedesktop.org/software/pulseaudio/webrtc-audio-processing/webrtc-audio-processing-2.1.tar.xz
   echo "ae9302824b2038d394f10213cab05312c564a038434269f11dbf68f511f9f9fe  webrtc-audio-processing-2.1.tar.xz" | sha256sum -c -
   curl -LO https://github.com/abseil/abseil-cpp/releases/download/20240722.0/abseil-cpp-20240722.0.tar.gz
   echo "f50e5ac311a81382da7fa75b97310e4b9006474f9560ac46f54a9967f07d4ae3  abseil-cpp-20240722.0.tar.gz" | sha256sum -c -
   cd ../..
   ```
   Each check must print `<file>: OK` — `webrtc-audio-processing-2.1.tar.xz: OK` and `abseil-cpp-20240722.0.tar.gz: OK`. The archives are **not** extracted: the build extracts them under its own build directory. On a mismatch, stop: delete the file and raise it. Published checksums: the abseil pin equals the `source_hash` in the 2.1 archive's `subprojects/abseil-cpp.wrap`; none is recorded for `webrtc-audio-processing-2.1.tar.xz`, whose pin is the hash taken 2026-11-06.
5. **Linux leftovers** — `vendor/libvosk.so`, `vendor/vosk-linux.zip` and `vendor/vosk-linux-x86_64-0.3.45/`, from before the Windows-native build, are unused: delete them.

## Provisioning a branch tree

A branch tree (`.claude/references/core/trees-contract.md`) is provisioned by copying `vosk-win64-0.3.45/` and `vosk-model-small-en-us-0.15/` from the one checkout's `D:/Workspace/VoXR-Speech-Recognition/NativeBridge~/vendor/` into its own `NativeBridge~/vendor/` as real directories, never links. `overlaid/` and the vosk zips are not copied, since Command 3's `confgen` writes `vendor/overlaid/continuous-default` in that tree. Run the blocks below, in order, from the tree's root in Git Bash:

```bash
(H='D:/Workspace/VoXR-Speech-Recognition/NativeBridge~/vendor' V='NativeBridge~/vendor' && test -d 'NativeBridge~/harness' && test -d "$H/vosk-win64-0.3.45" && test -d "$H/vosk-model-small-en-us-0.15" && test ! -e "$V/vosk-win64-0.3.45" && test ! -e "$V/vosk-model-small-en-us-0.15" && test ! -e "$V/.copying" && mkdir -p "$V/.copying" && cp -r "$H/vosk-win64-0.3.45" "$H/vosk-model-small-en-us-0.15" "$V/.copying/" && diff -rq "$H/vosk-win64-0.3.45" "$V/.copying/vosk-win64-0.3.45" && diff -rq "$H/vosk-model-small-en-us-0.15" "$V/.copying/vosk-model-small-en-us-0.15" && mv "$V/.copying/vosk-win64-0.3.45" "$V/.copying/vosk-model-small-en-us-0.15" "$V/" && rmdir "$V/.copying") && echo provisioned || echo 'NOT provisioned'
```

The block copies into `vendor/.copying/`, checks each copy against the hub's with `diff -rq` (same files, same bytes), and only then moves both into place, so a copy that stops part-way leaves nothing Command 3 reads; it copies nothing where the working directory is not a tree root (no `NativeBridge~/harness/` under it), the hub lacks either directory, or the tree holds either target or `.copying/`. It prints `provisioned` or `NOT provisioned`: with both directories present and no `.copying/`, the tree was already provisioned; otherwise, in a branch tree only, remove its `NativeBridge~/vendor/` and run the block again.

```bash
(H='D:/Workspace/VoXR-Speech-Recognition/NativeBridge~/vendor' V='NativeBridge~/vendor' && test -d 'NativeBridge~/harness' && test -f "$H/webrtc-audio-processing-2.1.tar.xz" && test -f "$H/abseil-cpp-20240722.0.tar.gz" && test ! -e "$V/webrtc-audio-processing-2.1.tar.xz" && test ! -e "$V/abseil-cpp-20240722.0.tar.gz" && test ! -e "$V/.copying" && mkdir -p "$V/.copying" && cp "$H/webrtc-audio-processing-2.1.tar.xz" "$H/abseil-cpp-20240722.0.tar.gz" "$V/.copying/" && (cd "$V/.copying" && printf '%s  %s\n' ae9302824b2038d394f10213cab05312c564a038434269f11dbf68f511f9f9fe webrtc-audio-processing-2.1.tar.xz f50e5ac311a81382da7fa75b97310e4b9006474f9560ac46f54a9967f07d4ae3 abseil-cpp-20240722.0.tar.gz | sha256sum -c -) && mv "$V/.copying/webrtc-audio-processing-2.1.tar.xz" "$V/.copying/abseil-cpp-20240722.0.tar.gz" "$V/" && rmdir "$V/.copying") && echo provisioned || echo 'NOT provisioned'
```

The second block, run from the tree's root after the vosk block above (the vosk block runs first), copies both archives into `vendor/.copying/`, checks both against their pins with `sha256sum -c` there, and only then moves them into place; it copies nothing where the working directory is not a tree root (no `NativeBridge~/harness/` under it), the hub lacks either archive, or the tree holds either archive or `.copying/`. It prints `provisioned` or `NOT provisioned`: with both archives present and no `.copying/`, the tree was already provisioned; otherwise, in a branch tree only, remove its `NativeBridge~/vendor/` and run both blocks again, the vosk block first.

The one checkout copies nothing — its `vendor/` is the one `## One-time provisioning` makes, and the block there prints `NOT provisioned`, changing nothing. Where the hub lacks either directory, the human first runs that section's blocks 2 and 3 from the hub's root (downloads are a human step; agents download nothing), then the block. Where the hub lacks either archive, the human first runs that section's item 4 from the hub's root, then the second block.

Writer trees (`<branch>--<unit>`, cut fresh from the branch's tip and removed after the join — `Planning~/design-docs/parallel-trees/decisions/adr-0004-no-writer-pool.md`) are never provisioned, and Command 3 never runs in one; run there, its harness half stops at the configure step on the missing `vendor/vosk-win64-0.3.45/libvosk.lib` (`CMakeLists.txt`'s required-file check), before `confgen`, so nothing under `vendor/` is written.

## Build and Run

From the project root, in Git Bash — this line is copied verbatim from Command 3 of the `unity` verification binding (`.claude/bindings/unity.md`), which is its source:

```bash
(cd NativeBridge~ && ls -d 'C:/Program Files/Microsoft Visual Studio/2022/Community' 'C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe' >/dev/null && 'C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe' --preset windows-x64 '-DCMAKE_GENERATOR_INSTANCE=C:/Program Files/Microsoft Visual Studio/2022/Community' && 'C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe' --build --preset windows-x64 && dotnet build harness/confgen/confgen.csproj --artifacts-path build-confgen && dotnet build-confgen/bin/confgen/debug/confgen.dll --model vendor/vosk-model-small-en-us-0.15 --out vendor/overlaid/continuous-default --profile Automatic --mode Continuous && ./build-windows/bin/vosk-bridge-harness.exe --model vendor/overlaid/continuous-default --fixtures "../Tests~/Fixtures/audio/tts" --manifest harness/expectations.json)
```

What each part does:

- `ls -d` checks the toolchain paths first — the VS 2022 Community instance and Unity's `cmake.exe` — and fails naming any that has moved.
- The first `cmake.exe` call configures with preset `windows-x64` (the "Visual Studio 17 2022" generator, x64), pinned to the VS 2022 Community instance by `CMAKE_GENERATOR_INSTANCE`.
- The second `cmake.exe` call builds Release.
- `dotnet build` builds `confgen`; `confgen --profile Automatic --mode Continuous` writes the Continuous default's model copy to `vendor/overlaid/continuous-default`, which the harness decodes.
- Then the harness runs from `build-windows/bin/`, beside the vendor DLLs the configure copies there.

The command behaves the same with or without `MSYS_NO_PATHCONV`. It runs in a subshell, so your shell stays at the project root. The preset selects the **stub** capture backend (`VOSK_BRIDGE_CAPTURE=stub` — no JNI, no Android anywhere) and builds the harness (`VOSK_BRIDGE_BUILD_HARNESS=ON`).

Output: a JSON report (per-fixture expected/actual/pass plus a summary). Exit codes: **0** = all fixtures match the baseline, transcripts and pins alike; **1** = at least one transcript or front-end pin mismatch (a case lacking `frontEndSamples` or `frontEndDigest` counts as a mismatch); **2** = operational error (unreadable model/manifest/WAV, schema-invalid manifest, bridge error, `{"error": ...}` result entries such as ring overflow, a failed front-end lifecycle check, a pin key present with the wrong type — `frontEndSamples` not a non-negative integer, `frontEndDigest` not a string).

Guards that make a green run meaningful (all exit 2 when violated):

- The decode-regime pins `gain`, `grammar`, and `libvosk` are **required** manifest keys — a run without them (e.g. a free decode) would silently invalidate the baseline.
- The `modelConf` pin, the exact text of `<model>/conf/model.conf`, is a **required** manifest key and must equal the decoded model's conf, checked before any decode — a conf that is absent or different is refused naming the conf. Only `--write-baseline` writes it.
- The **front-end lifecycle check** runs after `vosk_bridge_init` and before the grammar is set and any case is decoded. It pushes the first case's WAV (dump records off for it); the front end's digest once the unit has consumed the whole fixture is the **cold digest**. The front end's gain after that stream stops (g1) must differ from its cold gain after init (g0); a stop then `vosk_bridge_start_push` must keep it (g2 equal to g1). Then the same WAV is pushed again, and its digest is the **continued digest**, which must differ from the cold digest. Then a stop and `vosk_bridge_reset` must restore the cold gain (g3 equal to g0). The condition is `g1 != g0 && g2 == g1 && g3 == g0 && continued_digest != cold_digest`; otherwise the harness destroys the bridge and exits 2 naming all four gains and both digests. It proves `BeginStream` keeps the learned level, so the replay does not reproduce the cold run, and `Reset` clears it.
- The front-end pin keys' types are checked up front: a case's `frontEndSamples`, when present, must be a non-negative integer, and its `frontEndDigest`, when present, a string. An absent pin is no error here but a mismatch (exit 1).
- A manifest with zero cases is refused (no vacuous greens).
- Every `.wav` in the fixtures directory must have a manifest entry (corpus-drift guard — the mirror of Tier B's `EveryCommittedWav_IsListedInTheManifest`).

Fixture WAVs must be 48 kHz mono 16-bit PCM (the corpus format) — anything else is rejected naming the actual and required format.

## The baseline (`expectations.json`)

`expectedFinals` per fixture are the **raw bridge-level transcripts** (Vosk `[unk]` tokens included), which deliberately differ from the parser-level expectations in `Tests~/Fixtures/audio/manifest.json` — see the `_comment` in the file. The grammar (verbatim `GenerateGrammarJson` output), AGC gain (−18 dB, the device value), and libvosk version are pinned in-file because the baseline is only meaningful under the exact decode regime that produced it.

Each case also carries two front-end pins: `frontEndSamples`, the front end's int16 output sample count for the fixture before the harness's trailing padding, and `frontEndDigest`, the unit's 64-bit FNV-1a digest of that output, as 16 lowercase hex digits. The harness reads them by waiting until the unit has consumed the whole fixture (no drain), then pushes the padding. A case passes only when its transcripts and both pins equal the baseline. The pins and the `_comment` are **harness-written**: only `--write-baseline` writes them, and `_comment` comes from one constant in `main.cpp` (`kBaselineComment`); neither is hand-edited. The digests are **host-scoped** to the pinning host's CPU features — on x64 the AGC2 wrapper's 16→24 kHz resampler is selected at run time as AVX2, SSE or plain C, with no injection point, and no source patch is made — so a digest pin may fail on a host with different CPU features; such a failure is not a re-baselining occasion (below).

**Re-baselining** (after corpus regeneration — `pause-lead` cases included —, a libvosk bump, a change to the front end, or a change to the Continuous default's conf) is a conscious, reviewed step, per the Tier B F7-amendment policy; a front-end change re-pins only on the human's ruling (the run after the change exits 1 by design until the human has ruled on its A/B). From `NativeBridge~/`:

```bash
./build-windows/bin/vosk-bridge-harness.exe --model vendor/overlaid/continuous-default --fixtures ... \
  --manifest harness/expectations.json --write-baseline
```

then review the `expectations.json` diff against the corpus manifest's intents before committing. In that review, `modelConf` changes in the rule lines alone, a moved `expectedFinals` keeps its joined words (segmentation only), and the two front-end pins change on every case after a front-end change and on no case otherwise; otherwise the human rules. A transcript divergence on the Windows build (exit 1 against the committed baseline), like a front-end pin divergence against the committed baseline — one on another host included —, is not a re-baselining occasion: it stops for the human's ruling and is never re-pinned.

## Notes

- Without `--paced`, the harness pushes faster than real time; verdicts are timing-independent (Vosk endpointing consumes sample time, and full consumption before stop is guaranteed by pushing ring-capacity-plus-one-second of trailing silence).
- Paced mode, `--dump <file> --paced <samples>`: each case is pushed in `<samples>`-sample chunks on a real-time schedule from the case's start (a chunk is due when its last sample would be captured at 48 kHz), the padding's first second paced and the rest unpaced. The dump gains the header's `pace`, each result's `wall`, and each case-end's `pushes` and `reads`, in integer µs from the case's start. It exits 2 without `--dump`, with `--write-baseline`, or with a chunk outside 1–16384. It is the audio bench's mode (`Planning~/features/bench-paced-mode/`), never Command 3's.
- `VOSK_BRIDGE_CAPTURE=aaudio` exists as an option value but the AAudio backend is unmaintained legacy (broken on Quest, predates the AudioRecord fix) — selecting it is not verified and may not compile.
- `third_party/nlohmann/json.hpp` is the vendored single-header nlohmann/json v3.11.3 (MIT). `NativeBridge~/` (harness included) is stripped from the published package by `release.yml`.
