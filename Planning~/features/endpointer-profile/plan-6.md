# Phase 6 — persisted plan (2026-10-17)

Validated 2026-10-17; fix pass applied (plan-validator findings 1-8 and its omitted runtime claims).

**Presets, choice, resolution; default unchanged**, unsplit, serial, no units: F5, F6 (explicit, Push-to-talk, no controller), F7 (seam), F8, F9, F12, §6 performance. OV=`Runtime/EndpointerOverlay.cs`; SR=`Runtime/VoxrSpeechRecogniser.cs`; PT=`Runtime/VoxrPushToTalkController.cs`; CG=`NativeBridge~/harness/confgen/`; ST=`Planning~/features/coverage-in-selection/ab-rig/stage.sh`; `$S`=`.scratch/endpointer-profile-phase-6`.
**Out of scope:** Phase 7 (flip, re-pin, Command 3's new form, README, CHANGELOG); product docs; reconcile items (B1-B4, stale cites, `joined-rig.md` rule 2); G2-held writer choices.

**Departures from the Build-plan row** (human-approved):
- D1. `confgen --profile/--mode` here (plan-4 B2); links forced (`confgen.csproj` :8).
- D2. Command 3 in the green (CG under `NativeBridge~/`); pin untouched.
- D3. Hand-off also at `OnApplicationPause` (PT :185), `InitialMode` (:213-216).
- D4. `ApplyResult` (OV :37-47) gains the effective values.

## Files to change

| Path | Change | Why |
|---|---|---|
| `Runtime/VoxrEndpointerProfile.cs`, `.meta` | create: 1 | F5 |
| OV; SR; PT; CG `confgen.csproj`, `Program.cs`; ST | modify: 2-6 | F3, F5-F7, F12; D1-D4 |
| `Tests~/Editor/EndpointerOverlayTests.cs`, `Tests~/Runtime/VoxrPushToTalkControllerTests.cs`; `Tests~/Runtime/VoxrEndpointerProfileLoadTests.cs` | modify; create: 7 | F5-F9, F12 |
| `$S/sha.py`, `$S/mutate.py` | create, untracked | 8 |
| `plan-6.md`; `architecture.md` | create; link | plan |

## Contracts and interfaces

**Dispatch.** A, `code-writer`: Steps 1-7, probes (B lacks Write). B, `compile-check`: Step 8 once A ends unstopped. Never WSL.

**Step 1.** `namespace VoXR`, `public enum VoxrEndpointerProfile { Automatic = 0, ModelDefault = 1, Responsive = 2 }`, appended, never renumbered; header as `VoxrListeningMode.cs`. `.meta`: `fileFormatVersion: 2`, `guid: <32 random hex>`.

**Step 2 - OV** (usings kept; header updated):
- `internal static readonly Row Responsive = new Row("0.45", null, null)` after :49.
- Table: `internal static Row RowOf(VoxrEndpointerProfile)`, a switch; null for `Automatic`, undefined.
- `internal sealed class Resolution`: readonly `Preset`, `Row`, `Label`.
- `internal static Resolution Resolve(VoxrEndpointerProfile choice, VoxrListeningMode mode)`: a row -> (choice, row, `explicit choice`); else (`ModelDefault`, its row, `Continuous default` | `Push-to-talk default`); Continuous: Phase 7's flip site.
- `ApplyResult` + `public readonly string Rule2, Rule3, Rule4`, from the effective bytes on every path: applied, the rewritten bytes; unparseable copy (:257 with a reason), the copy's restored bytes; IO failure (:271), a best-effort read of the on-disk conf. Lenient decode (key lines ASCII); a key's last occurrence wins (as Kaldi), trimmed; missing key null. No conf (:230) or an unreadable one: all null, the reason naming it. Never throws.
- `internal static string LogLine(VoxrEndpointerProfile, string label, ApplyResult)`: `[VoxrSpeechRecogniser] Endpointer profile <preset> (<label>): rule2=<v> rule3=<v> rule4=<v>`, null `absent`; a `Reason` appends ` — not applied: <Reason> (<ConfPath>)`, values kept.

**Step 3 - SR.** After :23: `[Tooltip("Endpointer preset, applied at model load; Automatic lets the listening mode decide.")]`, `[SerializeField] VoxrEndpointerProfile endpointerProfile = VoxrEndpointerProfile.Automatic;`, no property. `internal VoxrListeningMode ListeningModeHint { get; set; }`, `Continuous`. After :205, local `profile = Resolve(endpointerProfile, ListeningModeHint)`. After :224, before :226: `Apply(modelPath, profile.Row)`, `LogLine(...)`; `Debug.Log(line)` with no reason, else `Debug.LogWarning(line)` instead, never both; no context (`UnityStub.cs` :68, :70). Header updated.

**Step 4 - PT.** `HandOffMode()`: `_listeningMode` into a non-null recogniser's hint; in a new `Awake`, after :61, before :80, :112, :138, :154, :164, :185, in `InitialMode`. `SpeechRecogniser` (:197): old non-null `!= value` -> `Continuous`; assign; hand off. New `OnDestroy`: back to `Continuous`. `_wantRecognising` stays in `OnEnable` (#109).

**Step 5 - CG.** csproj: `Link`s to `VoxrEndpointerProfile.cs`, `VoxrListeningMode.cs` after :13. `Program.cs`: `Flags` (:30) + `--profile`, `--mode` (5-6); mandatory loop :54-58, 0-2 to 0-1 (`--model`, `--out`); `IsDecimal` loop :60-66, 2-on to 2-4 (`--rule2/3/4` only); `--profile`/`--mode` exact enum names (ordinal in `Enum.GetNames`, integers refused). Usage (:27-28, header): `usage: confgen --model <dir> --out <dir> (--profile <name> [--mode Continuous|PushToTalk] | --rule2 <v> [--rule3 <v>] [--rule4 <v>])`. Exit 2 before any write, as today: neither or both of `--profile`, `--rule2`/`3`/`4`; `--rule3`/`4` without `--rule2`; `--mode` without `--profile`; a bad name. `--mode` defaults `Continuous`; `Resolve(...).Row` to `Apply`; `--rule` path unchanged.

**Step 6 - ST.** `OPTIONAL=()` after :54; in :59-78 `OPTIONAL=( Runtime/EndpointerOverlay.cs Runtime/VoxrEndpointerProfile.cs Runtime/VoxrListeningMode.cs )`. After :91, `if`/`then` loop: WORKTREE `[ -f "$REPO/$src" ]`; ref `git -C "$REPO" cat-file -e "$REF:$src" 2>/dev/null`; else skip. `SOURCES` still aborts on missing.

**Step 7 - tests**, plain `[Test]`/`[UnityTest]`. `EndpointerOverlayTests`:
`Enum_IsExactlyThree`
`Table_Rows`
`ChoiceField_IsNonPublicEnum`
`NoPublicFloat`: SR's public `float` members and parameters = an allowlist listed in the test, the pre-feature set read from source at merge-base `b255105` (e.g. `InputLevel` :89); the enum's underlying type `int`; no public member of a public Runtime type named `*Endpoint*`/`*Profile*` exposes or takes `float`/`double`
`Resolve_Explicit_WinsBothModes`
`Resolve_AutoContinuous_ModelDefault`
`Resolve_AutoPushToTalk_ModelDefault`
`LogLine_Applied_ListsValues`
`LogLine_MissingKey_Absent`
`LogLine_NotApplied_NamesReason` (parse failure: copy's real values)
`Apply_Tuned_CarriesValues`
`Apply_ParseFailure_ValuesFromCopy`

`VoxrPushToTalkControllerTests`:
`Hint_NoController_Continuous`
`Hint_Awake_HandsOff` (inactive GO, hint reset, activated)
`Hint_ModeSetter_HandsOff`
`Hint_InitialMode_HandsOff`
`Hint_Swap_ResetsOld`
`Hint_OnDestroy_Continuous`

`VoxrEndpointerProfileLoadTests`, `#if UNITY_EDITOR_WIN`, shaped on `VoxrPlaybackSeamTests` :22-39, the load moved into the test body; per test: field by reflection (Ownership :189), `LogAssert.Expect`, then the load; dir by `ModelExtractor.ExtractModelAsync` (:26):
`Load_Default_LogsLine`
`Load_Responsive_Rule2Is045`
`Load_ModelDefault_CacheHit` (conf = the copy; stamp (:23) read after load 1 returns, as :153-158 may refresh it on a tier-2 hit, unchanged after load 2; planted file kept)
TearDown: `Apply(dir, ModelDefault)`, plant deleted.

**Probes.** `sha.py <conf> <out>...`: `ok` if each out's line 1 is `sha256 <conf's SHA-256>`; exit 0/1. `mutate.py apply|restore`: backs up `Samples~/CommandRecognition/TacticalSceneController.cs` in `$S`, appends `class Canary { void M() { Missing(); } }`; restore prints `restored` only if byte-equal; exit 0/1.

**Step 8 - green** (`compile-check`, project root). Rules: (a)-(c) as `plan-5.md` **Rules**; (d) vc-guard: force-adds are the PM's; (e) both agents run commands as written, no added pipe, redirect or capture (Phase 5's `| grep` masked an exit).

`X` = `N=NativeBridge~; S=.scratch/endpointer-profile-phase-6; V=$N/vendor/vosk-model-small-en-us-0.15; C="dotnet $N/build-confgen/bin/confgen/debug/confgen.dll --model $V"; E="env -u MSYS_NO_PATHCONV bash Planning~/features/coverage-in-selection/ab-rig/stage.sh"; e(){ echo "exit $?"; };`, one call each.
1. `X sha256sum Runtime/VoxrEndpointerProfile.cs.meta > $S/meta.sha; e`; Commands 1-2; `X sha256sum -c $S/meta.sha; e`: `failed="0"`, `inconclusive="0"`, EditMode 230 (218 + 12), PlayMode 715 (706 + 9); no `Tests` leftovers; `OK`.
2. Command 4: green, one CS0618; `X python3 -I $S/mutate.py apply; e`; Command 4: red at the appended line; `X python3 -I $S/mutate.py restore; e`: `restored`; Command 4: green.
3. Command 3: exit 0, 37/37; `X git diff --exit-code -- $N/harness/expectations.json; e`: `exit 0`.
4. `X dotnet build $N/harness/confgen/confgen.csproj --artifacts-path $N/build-confgen; e`; then:
   - `X $C --out $S/resp --profile Responsive; e; diff $V/conf/model.conf $S/resp/conf/model.conf; e`: `exit 0`, `exit 1`, rule2 `0.5` to `0.45` alone.
   - `X $C --out $S/ac --profile Automatic --mode Continuous > $S/ac.out; e`; so `ap` (`--mode PushToTalk`), `md` (`--profile ModelDefault`); `X python3 -I $S/sha.py $V/conf/model.conf $S/ac.out $S/ap.out $S/md.out; e`: `exit 0` each, `ok`.
   - `X $C --out $S/r035 --rule2 0.35; e`: `exit 0`, rule2 `=0.35`.
   - `X $C --out $S/bad --profile Responsive --rule2 0.35; e`; so `--profile 2`, `--profile Fast`, `--rule2 0.35 --mode Continuous`, no `--profile` or `--rule2`, `--rule3 0.5` alone, `--profile Responsive --mode Fast`: `exit 2` each.
5. DocCheck, recipe :227: exit 0, no `FAIL`, `<n>/<n>` equal.
6. Recipe :227-230: `X $E WORKTREE $S/wt recogniser && (cd $S/wt && dotnet build -v q --nologo); e`; so `b255105` into `$S/base`: `exit 0` each.

**Stop rules.**
- `$S` holding more than the probes before check 1: BLOCKED, no `rm -rf`.
- No dll at `C`: rebuild `-o $N/build-confgen`, reported. No `SamplesCompile.csproj`: `code-writer` regenerates it.
- A red check: `code-writer` (two passes, then the human). Command 3 exit 1: the human, never a re-pin.
- Ownership `NoUnexpectedReceived` red, the `.meta` changed, no `restored`, or OV needing Unity or `System.Globalization`: stop.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| undefined value as `Automatic` | throw | no load fails for the profile |
| values from the effective bytes, every path | null with a reason | the line shows the decoder's conf (*Load-time log*) |

## Acceptance criteria

1. [ ] Check 1 (F5-F9, F12, §6).
2. [ ] Check 2 (F5).
3. [ ] Check 3 (§9).
4. [ ] Check 4 (F3; D1).
5. [ ] Check 5 (Build plan).
6. [ ] Check 6 (*The rig*).

## Open questions that block implementation

None; a stop opens one.

## Blast radius

- Every real load applies and logs (`Samples~` as Continuous); the Editor cache gains the copy (F9); scenes load `Automatic`; `joined.sh` stages carry OV.

## Runtime claims

- a (8.1): `NoUnexpectedReceived` (Ownership :102, :142, :158) ignores a setup-time `LogType.Log`.
- b (8.1): Unity keeps the hand-made `.meta`.
- c (4, 7): `Awake` precedes `OnEnable`, inactive-built GOs too; its hand-off runs in `AddComponent` on an active GO with a null recogniser, a no-op.
- d (8.6): b255105 builds, optional files absent.
- e (6, 8.5): an empty `"${OPTIONAL[@]}"` passes `set -u`, in checks 6 and 5 (DocCheck's parser-layer stage).
- f (2): enum `ToString()` is the name (IL2CPP: Phase 8).
- g (7): `LogAssert.Expect` sees an async continuation's line.
- h (8.4): the dll is at `bin/confgen/debug/`.
- i (6): Step 6's `cat-file -e` exits non-zero, quiet, for a path absent at the ref.
- j (7, 8.1): Load tests hit a real tier-1/2 cache serve in the host's Editor.
- k (8.2): the red is at `TacticalSceneController.cs`'s appended line.