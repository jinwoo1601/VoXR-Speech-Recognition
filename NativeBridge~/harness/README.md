# Tier C Desktop Harness — WAV → transcript regression on the Windows-native bridge

Replays the committed fixture corpus (`Tests~/Fixtures/audio/tts/`) through the **real native bridge** (ring buffer → downsampler → AGC → int16 → Vosk) compiled Windows-native with MSVC, and compares the final transcripts against the committed baseline in `expectations.json`. Runs entirely on the Windows host in Git Bash — no Unity, no device. Design contract: `Planning~/design-docs/automated-verification.md` §7 (Tier C).

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
4. **Linux leftovers** — `vendor/libvosk.so`, `vendor/vosk-linux.zip` and `vendor/vosk-linux-x86_64-0.3.45/`, from before the Windows-native build, are unused: delete them.

## Build and Run

From the project root, in Git Bash — this line is copied verbatim from Command 3 of the `unity` verification binding (`.claude/bindings/unity.md`), which is its source:

```bash
cd NativeBridge~ && ls -d 'C:/Program Files/Microsoft Visual Studio/2022/Community' 'C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe' >/dev/null && 'C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe' --preset windows-x64 '-DCMAKE_GENERATOR_INSTANCE=C:/Program Files/Microsoft Visual Studio/2022/Community' && 'C:/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/cmake/3.22.1/bin/cmake.exe' --build --preset windows-x64 && dotnet build harness/confgen/confgen.csproj --artifacts-path build-confgen && dotnet build-confgen/bin/confgen/debug/confgen.dll --model vendor/vosk-model-small-en-us-0.15 --out vendor/overlaid/continuous-default --profile Automatic --mode Continuous && ./build-windows/bin/vosk-bridge-harness.exe --model vendor/overlaid/continuous-default --fixtures "../Tests~/Fixtures/audio/tts" --manifest harness/expectations.json
```

What each part does:

- `ls -d` checks the toolchain paths first — the VS 2022 Community instance and Unity's `cmake.exe` — and fails naming any that has moved.
- The first `cmake.exe` call configures with preset `windows-x64` (the "Visual Studio 17 2022" generator, x64), pinned to the VS 2022 Community instance by `CMAKE_GENERATOR_INSTANCE`.
- The second `cmake.exe` call builds Release.
- `dotnet build` builds `confgen`; `confgen --profile Automatic --mode Continuous` writes the Continuous default's model copy to `vendor/overlaid/continuous-default`, which the harness decodes.
- Then the harness runs from `build-windows/bin/`, beside the vendor DLLs the configure copies there.

The command behaves the same with or without `MSYS_NO_PATHCONV`. It begins with `cd NativeBridge~`, so wrapping it in `( … )` leaves your shell at the project root. The preset selects the **stub** capture backend (`VOSK_BRIDGE_CAPTURE=stub` — no JNI, no Android anywhere) and builds the harness (`VOSK_BRIDGE_BUILD_HARNESS=ON`).

Output: a JSON report (per-fixture expected/actual/pass plus a summary). Exit codes: **0** = all fixtures match the baseline; **1** = at least one transcript mismatch; **2** = operational error (unreadable model/manifest/WAV, schema-invalid manifest, bridge error, `{"error": ...}` result entries such as ring overflow).

Guards that make a green run meaningful (all exit 2 when violated):

- The decode-regime pins `gain`, `grammar`, and `libvosk` are **required** manifest keys — a run without them (e.g. a free decode) would silently invalidate the baseline.
- The `modelConf` pin, the exact text of `<model>/conf/model.conf`, is a **required** manifest key and must equal the decoded model's conf, checked before any decode — a conf that is absent or different is refused naming the conf. Only `--write-baseline` writes it.
- A manifest with zero cases is refused (no vacuous greens).
- Every `.wav` in the fixtures directory must have a manifest entry (corpus-drift guard — the mirror of Tier B's `EveryCommittedWav_IsListedInTheManifest`).

Fixture WAVs must be 48 kHz mono 16-bit PCM (the corpus format) — anything else is rejected naming the actual and required format.

## The baseline (`expectations.json`)

`expectedFinals` per fixture are the **raw bridge-level transcripts** (Vosk `[unk]` tokens included), which deliberately differ from the parser-level expectations in `Tests~/Fixtures/audio/manifest.json` — see the `_comment` in the file. The grammar (verbatim `GenerateGrammarJson` output), AGC gain (−18 dB, the device value), and libvosk version are pinned in-file because the baseline is only meaningful under the exact decode regime that produced it.

**Re-baselining** (after corpus regeneration, a libvosk bump, or a change to the Continuous default's conf) is a conscious, reviewed step, per the Tier B F7-amendment policy. From `NativeBridge~/`:

```bash
./build-windows/bin/vosk-bridge-harness.exe --model vendor/overlaid/continuous-default --fixtures ... \
  --manifest harness/expectations.json --write-baseline
```

then review the `expectations.json` diff against the corpus manifest's intents before committing. In that review, `modelConf` changes in the rule lines alone, and a moved `expectedFinals` keeps its joined words (segmentation only); otherwise the human rules. A transcript divergence on the Windows build (exit 1 against the committed baseline) is not a re-baselining occasion: it stops for the human's ruling and is never re-pinned.

## Notes

- The harness pushes faster than real time; verdicts are timing-independent (Vosk endpointing consumes sample time, and full consumption before stop is guaranteed by pushing ring-capacity-plus-one-second of trailing silence).
- `VOSK_BRIDGE_CAPTURE=aaudio` exists as an option value but the AAudio backend is unmaintained legacy (broken on Quest, predates the AudioRecord fix) — selecting it is not verified and may not compile.
- `third_party/nlohmann/json.hpp` is the vendored single-header nlohmann/json v3.11.3 (MIT). `NativeBridge~/` (harness included) is stripped from the published package by `release.yml`.
