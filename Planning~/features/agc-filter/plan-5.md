# Phase 5 — persisted plan (2026-11-06)

Scope: the row, takes 1-7, and two managed seams its tests need (a playback chunk length, a probe delegate); the CHANGELOG line is the PM's (`.claude/agents/code-writer.md` :36), suggested by the writer. Serial, this tree. B = `Runtime/EditorMicBackend.cs`, FN = `Runtime/Native/FrontEndNative.cs`, X = `Runtime/Plugins/x86_64/`, TE = `Tests~/Editor/`, TR = `Tests~/Runtime/`, NB = `NativeBridge~/`, Reg = register bullet, A2 = AGC2 2.1 `agc2/`.

## Files to change

| Path | Change | Why |
|---|---|---|
| X`voxr-frontend.dll`, `.dll.meta` | create (copy; takes 1-2) | F8; §6 Size |
| FN, `.cs.meta` | create | F7 |
| B | modify | F7, F10; §4.2 |
| `Runtime/Dsp/` (`Agc.cs`, `Downsampler.cs`, `.meta`s), `Runtime/Dsp.meta` | delete | F2, F7; take 3 |
| TE`AgcTests.cs`, `DownsamplerTests.cs` | delete | F7 |
| TE`FrontEndNativeTests.cs`, `FrontEndLoadTests.cs` | create | F5, F6, F8, F10, F11; §4.2 |
| TR`VoxrFrontEndParityTests.cs` | create | F9 |
| TR`VoxrPlaybackSeamTests.cs`, `VoxrWavReplayTests.cs` | modify | row |
| `THIRD_PARTY_NOTICES.md` | modify | take 5 |
| `CHANGELOG.md` | modify (the PM) | F15 |
| `.claude/bindings/unity.md`, `core.md` | modify (`doc-writer`) | row; take 4 |

## Contracts and interfaces

Existing files: Edit, never Write; all are CRLF (rg). New `.cs`: Write, then the CRLF rewrite (`.scratch/implement-agc-filter-3-brief.md` :162-166). Each `.meta`: its model copied by bytes, only the guid replaced by `uuid.uuid4().hex`. Header block, no other comment; edited definitions lose theirs (implementation-rules rule 1). No agent commits.

**Step 1: baseline.** C3's Windows half verbatim: exit 0, 52/52. Record size, sha256 of NB`build-windows/bin/voxr-frontend.dll`; dumpbin (plan-3 Step 7): the seven exports, no CRT dependent. A needed NB edit stops and reports. Else BLOCKED.

**Step 2: DLL.** `cp` to X, `cmp` equal; `.meta` from `libvosk.dll.meta`.

**Step 3: FN**, `VoxrNative.cs`'s form and `.meta`, `[DllImport("voxr-frontend", CallingConvention = CallingConvention.Cdecl)]`. Consts `AbiVersion` 1, `DecimationFactor` 3, `FrameSamples` 160; `[StructLayout(LayoutKind.Sequential)] struct voxr_fe_stats { ulong input_samples, output_samples, digest; float gain_db, pre_rms, post_rms; }`; `int voxr_fe_abi_version()`; `IntPtr voxr_fe_create(float)`; `int voxr_fe_process(IntPtr, [In] float[], int n, [Out] short[], int cap)`; `void voxr_fe_begin_stream|reset|destroy(IntPtr)`; `int voxr_fe_get_stats(IntPtr, out voxr_fe_stats)`.

**Step 4: tests first** (rule 5), `#if UNITY_EDITOR_WIN`; WAVs by `VoxrWavReader`, root as `VoxrFixtureCorpusTests.cs` :35-45, + `audio/tts/`; cap `n / 3 + 160`; dB rounded to 0.01. TE`FrontEndNativeTests`, own units, 480 samples a call unless named:
- `Abi_VersionAndStatsLayout`: 1; `Marshal.SizeOf` 40.
- `F5_FullScaleAndOverRange_NoWrap`: plan-3 :36's sines and bar.
- `F6_SilenceThenBurst`, `F6_NoiseThenBurst`: lead `cease_fire.wav` ×4 (plan-3 :37's lead B; gain off 15, else fail), 3 s zeros or seeded -60 dBFS white noise, 200 ms seeded white noise at W's input RMS (W: the lead's last 50 frames with `pre_rms` > 0). Max gain over pause and burst <= W's; burst lift <= W's (out − in; As-built Phase 3). `F6_NoiseOnly`: 10 s -60 dBFS: no gain above the first.
- `F10_Lifecycle`: `launch_one_missiles_target_hotel_one.wav` (harness `cases[0]`): g1 != 15; `begin_stream`: g1; `reset`: 15; destroy, create: 15; exact.
- `F11_FieldUnchanged`: `VoxrSpeechRecogniser.micGainTargetDb` private float, `[SerializeField]`, no `[FormerlySerializedAs]`.
- `F11_SixDbHigherTarget_AboutSixDbLouder`: `cease_fire.wav` × 0.5, whole repeats to >= 30 s, fresh units at -24 and -18; output RMS over the last 10 s: L(-18) − L(-24) in [5.5, 6.5]; each final gain in (0, 50), != 15, else fail naming it. A -12 unit's step logged (`TestContext.WriteLine`), no bar.
- `ChunkInvariance`: `launch_one…` in 1237-sample calls, and cycling 1, 7, 479, 481, 2003: equal counts, digests.

TE`FrontEndLoadTests`: `EditorMicBackend.ProbeFrontEnd` given a throw of `DllNotFoundException`, of `EntryPointNotFoundException`, or `() => 2`: false, one `ModelLoadFailed` naming `voxr-frontend.dll` and `Runtime/Plugins/x86_64/`; given `FrontEndNative.voxr_fe_abi_version`: true, none. `Plugin_EditorWindowsX64Only`: `PluginImporter` at `Packages/com.jinwoo1601.voxr/` + X: Editor, `OS` Windows, `CPU` x86_64; any platform, Android, Win64 off.

TR`VoxrFrontEndParityTests` (`VoxrPlaybackSeamTests`' SetUp/TearDown): a case per manifest `file` (as `VoxrWavReplayTests.cs` :112-126); pin: the one `resolvedPath` + `NativeBridge~/harness/expectations.json` case whose `file` is the basename, by `JsonUtility` (`cases[]` {`file`, `frontEndDigest`, `frontEndSamples` long}; `gain` -18 asserted). Chunk 1237 at even manifest index, 4801 odd; pumped to false; `FrontEndStats`: `input_samples` = WAV length (the harness read point, `main.cpp` :441-447), `output_samples`, `digest.ToString("x16")` = the pin. Same input: int16 / 32768 in both readers (NB`harness/wav_reader.cpp` :106-109, `VoxrWavReader.cs` :92-100), ring floats unscaled (`vosk_bridge.cpp` :104-126), no resample; cold per case (`main.cpp` :475).

TR`VoxrPlaybackSeamTests`: `Playback_QuietSine_EngagesAgc` → `Playback_QuietSine_DoesNotRaiseGain`: `EditorAgcGain` after `StartPlayback` = 10^(15/20) ±1e-4 (`reset`'s 15 dB); after the replay, in dB rounded to 0.01, <= 15.00 (a frame-energy ratio, Reg *AGC2 driver* "Gain readout"). `VoxrWavReplayTests` :131 drops `&& c.category != "pause-lead"`.

**Step 5: B** (Reg *Editor's DLL load*; Seam *Editor backend → DLL*; *Lifecycle*).
- `internal static bool ProbeFrontEnd(Func<int>, Action<VoxrBridgeErrorCode, string>)`, own `try`: the two exceptions, any other, or version != `AbiVersion` → `ModelLoadFailed` naming the DLL and X, plus detail; false. Called after :76, before :78's `try`.
- `voxr_fe_create(micGainTargetDb)` replaces :117, after model and recognizer: no handle on a failed load; null → both freed as :108-112, `ModelLoadFailed`, false. `Release` destroys after `Stop`, zeroes handle, `_stats`; reached by `VoxrSpeechRecogniser.cs` :269-270.
- `_stats`: one `voxr_fe_get_stats` after each create, `begin_stream`, `reset`, `process`. `PreAgcRms`/`PostAgcRms` read it; `AgcGain` 1 with no handle, else 10^(`gain_db`/20); `internal voxr_fe_stats FrontEndStats`.
- `ProcessChunk` :398-405 → `voxr_fe_process` into `_int16Buffer`, cap its length; < 0 throws `InvalidOperationException` naming count, cap (impossible by sizing; no silent drop); stats; 0 returns; count to :407-408; :390-396 and `ComputeRms` stay; its leading comment :382-385 (Downsampler → AGC pipeline) goes (rule 1), the new pipeline stated in the header's `Purpose`.
- `_int16Buffer` = `n / DecimationFactor + FrameSamples`, n `workSize` (:198-199) or chunk (:493-498). The two fields, `_downsampledBuffer` (and `Release`'s nulling, :324), `FloatToInt16`, `using VoXR.Dsp` go.
- `Start` :201-202 `begin_stream`; `Reset` :243-244, `StartPlayback` :500-501 `reset`; `Stop` none.
- `StartPlayback(…, int chunkSamples = PlaybackChunkSamples)`: < 1 → `AudioDeviceUnavailable`, false; buffers grow to it; :521 uses it.
- Header: `Purpose` names the front end; `Depends: FrontEndNative, VoxrNative, BridgeNative, VoxrBridgeErrorCode`.

**Step 6.** Deletions. `rg -n "VoXR\.Dsp|\bAgc\b|Downsampler" Runtime Editor Tests~`: none; `rg -n "0\.2136" Runtime Editor Tests~ NativeBridge~/src NativeBridge~/include`: none (F2).

**Step 7: targeted.** Command 1 with `--filter "FrontEnd|VoxrPlaybackSeamTests"`, both platforms: green, totals > 0.

**Step 8: notices.** :58, :130, :175, :234 also name X`voxr-frontend.dll`; texts unchanged.

**Step 9: Commands 1–2**, one bracket, green: EditMode 230 − 12 (the deleted `[Test]`s) + 14; PlayMode 724 + 52 + 15. Command 4: no public API or `Samples~` change.

**Step 10: report.** Steps 1-2's size, sha256 (§6 Size); 7, 9's lines; F11's levels; for the PM, `[Unreleased]` `### Changed`, <= 200 characters: "- The Editor and Quest share one native front end (a new 48→16 kHz filter and WebRTC AGC2): scores shift, and the Windows Editor now loads `voxr-frontend.dll`."

**Step 11: `doc-writer`.** `unity.md` :8 after the `.so` clause: "; on a front-end change the Release `voxr-frontend.dll` from `NativeBridge~/build-windows/bin/` is committed at `Runtime/Plugins/x86_64/voxr-frontend.dll`". `core.md` :42 Generated adds "`Runtime/Plugins/x86_64/voxr-frontend.dll` (built from `NativeBridge~/` by the `windows-x64` preset)"; :36 drops `Runtime/Dsp/**`, adds `Runtime/EditorMicBackend.cs` (no glob covers it).

**Gate.** Steps 6, 9. As-built: Step 10.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| F11 at -24/-18 | -18/-12 | gain capped at -50 dBFS less a floor read off every non-silent frame (A2`noise_level_estimator.cc` :93-134, `adaptive_digital_gain_controller.cc` :57-66, :140-144): a *F11's step* candidate, biting at the higher target |
| ±0.5 dB | ±1; none | settled gain linear in headroom (ibid. :45-47), level and saturation input-side; 3.16 fails |
| Probe delegate | move the DLL; ABI hook | DLL stays loaded; Reg *fe-probe* |
| Throw on -1 | return | no silent drop (§6) |

## Acceptance criteria

1. [ ] Steps 1-2. (F8; §6)
2. [ ] FN: seven exports, 40 bytes. (F7)
3. [ ] F5, F6, F10, F11, invariance green. (F5-F11; §6)
4. [ ] Probe, plugin green. (§4.2; F8)
5. [ ] Parity, 52 cases. (F9)
6. [ ] Sine restated; Tier B takes `pause-lead`. (row)
7. [ ] Step 6. (F2, F7)
8. [ ] Step 9. (F7)
9. [ ] Notices; the line suggested. (F15)
10. [ ] Step 11 only. (row)
11. [ ] New files CRLF, lone-LF 0; `git status --short -uall`: table paths only.

## Open questions that block implementation

None.

## Blast radius

- `VoxrDebugWindow` :205-211: a fresh gain reads ≈5.62, was 1.
- Product docs stale to G2; Command 1 grows; parity host-scoped.

## Runtime claims

- 1: C3 rebuilds nothing; 52/52 with pins.
- 4, 7, 9: the host run imports the new plugin, no extra step; EditMode loads it; `JsonUtility` reads `long`; lead B moves the gain; the detector rejects zeros, noise, bursts, a 220 Hz sine; ×0.5 `cease_fire` is in the boost region at both targets, uncapped; replay digests equal this host's pins; the 15 `pause-lead` and 16 earlier Tier B cases pass.
- 5: `voxr_fe_create(-18)` succeeds.

Hazards: 1 before any edit; NB never edited; 4-5 one block, compiled at 7; an F11 or parity miss stops for the human, never a retune, new input or re-pin; no loaded DLL replaced while an Editor is open; 11 after the gate.
