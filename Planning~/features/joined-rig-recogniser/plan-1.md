# Phase 1 — persisted plan (2026-10-15)

**Phase 1 - the staged world (prototype, rig only)**, unsplit, serial: F3 (staging), F4 (path), §10 item 1; bound by §8, §2 Out 1. AB = `Planning~/features/coverage-in-selection/ab-rig/`; VSR `Runtime/VoxrSpeechRecogniser.cs`, VCR `Runtime/Commands/VoxrCommandRecogniser.cs`; live lines.

## Files to change

| Path | Change | Why |
|---|---|---|
| AB `UnityStub.cs` | modify | *Staging* superset; §10 item 1 |
| AB `BridgeNative.cs`, `ModelExtractor.cs` | create | *Staging* stand-ins; *Delivery* |
| AB `JoinedRecogniser.cs` | create | *The player loop*, *The clock*, *Delivery*; F3, F4 |
| AB `Joined.cs`, `Program.cs` | modify | `drive-check` and its hook only (*Stage layering*) |
| AB `stage.sh` | modify | `recogniser` argument; F11 (`joined.sh` unedited) |
| AB `Rig.csproj` | modify, comment :9-24 | entry points |
| `plan-1.md`; `architecture.md` | create; link line | this plan |

## Contracts and interfaces

**Dispatch.** A, `compile-check` (output off the main thread): Step 0. B, `code-writer`: Steps 1-6, one atomic dispatch and one check-in (no green state between). C, the same: criteria 2-8. Agents: no git write; report as text (PM-filed under `.scratch/`); byte compares by `python3 -I`; briefs name the project root; scripts as `env -u MSYS_NO_PATHCONV bash AB/<script>`.

**Step 0 - baseline.** Record `git status --short`; DocCheck verbatim (recipe), its `warning` lines counted; `.scratch/joined/p4.jsonl` present, else re-made per `joined-rig-parser/plan-4.md` criterion 3 (`--dump`, built harness).

**Step 1 - `UnityStub.cs`**, compile-only in a parser stage. Public, all four: `Object` (`name`; no `==` overload); `MonoBehaviour : Object` (rig-set `isActiveAndEnabled`; `SendMessage(string, SendMessageOptions)` reflects to the named parameterless method of any visibility); `ScriptableObject : Object`; `SendMessageOptions` — public VCR :15, `VoxrSlotAsset.cs` :14, VSR :20 need them (else CS0060/CS0050/CS0051). `Time.time` (internal setter); `AddComponentMenu, SerializeField, Tooltip, Header, CreateAssetMenu(menuName), Serialization.FormerlySerializedAs`. `Debug` keeps `Log`, `LogWarning`, `LogError(object)`; adds `LogError(object, Object)`, `Assert(bool, string)`, `isDebugBuild` (true). Each `LogError` and failed `Assert` appends to `internal static List<string> RigErrors`. Header (:1) drops "ONLY".

**Step 2 - stand-ins**, shipped names, only members the closure calls (*Drift watch*):
- `BridgeNative.cs`: `VoXR.Native.BridgeNative`, no `unsafe`. `vosk_bridge_init(string, float, float)`, `_start`, `_reset`, `_set_grammar` (records `RigAppliedGrammar`) return 0; `_destroy`, `_stop`; `_is_initialised`, `_is_running` 1 from init/start to destroy/stop; `_get_input_level` 0; `GetLastError()` empty. `vosk_bridge_get_result(out int, out int)`: next queued result with delivery frame <= `RigFrame`, in order, as token `(IntPtr)(index + 1)`; else `IntPtr.Zero`. `SpanFromPtr`: token to `new ReadOnlySpan<byte>(bytes, 0, length)`. Rig: `RigLoad(timeline)` (resets all), `RigFrame`, `RigLastServed`.
- `ModelExtractor.cs`: `VoXR.ModelExtractor.ExtractModelAsync(string, Action<VoxrBridgeErrorCode, string>)` returns `Task.FromResult` of its path (claim a).

**Step 3 - `JoinedRecogniser.cs`** (`AbRig`).
- `TickRate = 90f` (*Tuning record*); frame k: `Time.time = (float)k / TickRate`. Timeline entry `(double AudioTime, bool Final, string Payload)`; delivery frame: least k with `(double)k / TickRate >= AudioTime`.
- `RunWorld(name, timeline, slots, commands)` (*The player loop*): clear `RigErrors`, `RigLoad`; construct both; VCR `SpeechRecogniser` setter while disabled; `Awake` to each (`DontRequireReceiver`); enable, `OnEnable`; observers on VSR `OnResult`, `OnPartialResult` and VCR's command events, in the *Trace* shapes (`case` = world, `seq` = `RigLastServed`); `Configure(slots, commands)`; `StartRecognition()`; then `IsModelReady`, `IsRecognising`, `RigAppliedGrammar` set (VCR `HandleModelReady`), else stop 2. Frames 0..K: clock, `RigFrame`, `Update` to VSR then VCR. At K: read `HasPendingCommand`; disable; `OnDisable`, `OnDestroy` (claim f).
- Horizon H = last `AudioTime` + `bufferWindow` + `pendingTimeout`, read off the instance by name; K = least k with `k / TickRate >= H`, plus 3. Pending at K: stop 2.
- Guard (*Guards at this layer*, error-log part): `RigErrors` non-empty after a message or frame: stop 2 naming world, frame, entry. Stops throw `JoinedInput.Stop`.
- `[ModuleInitializer]` (`#pragma warning disable CA2255`) sets `Joined.DriveCheck`.
- `drive-check`, no options. W = a fresh instance's `bufferWindow`; `Commands()`; Vosk-shaped payloads, `conf` 1.0. Flush frame after delivery frame d: least f > d with `(float)f/TickRate - (float)d/TickRate >= W` (`UtteranceBuffer.ShouldFlush` :159-162). Worlds:
  1. `grid`: final "cease fire" at 1.005 s: one `cease_fire` fire, on the flush frame.
  2. `merge`: finals "cease" at 1.0, "fire" at 1.0 + 0.6W: one `cease_fire` fire, on the second delivery's flush frame.
  3. `apart`: the same at 1.0, 1.0 + 2W: no `cease_fire`.
  4. `partial`: partial "cease" at 0.8, final "cease fire" at 1.0: `OnPartialResult` gets "cease" once, on its delivery frame; then 1's fire.
  5. `pending`: `cease_fire` with `requiresConfirmation: true`; "cease fire" at 1.0: `OnCommandPending` on the flush frame p; `OnCommandCancelled` by the same rule from p, `pendingTimeout` for W; no fire.
  Prints JSON-line observations, `drive-check: <world> ok|FAIL <why>`, `drive-check: <p>/5 worlds passed`; exit 0, 1 on a FAIL, 2 on a stop.

**Step 4 - `Joined.cs`, `Program.cs`.** `internal static Func<string[], VoxrSlotDefinition[], VoxrCommandDefinition[], int> DriveCheck`; `Run` takes `VoxrSlotDefinition[] slots` (Program :175-180 passes `Slots()`). `drive-check` with the hook null: stop 2 naming `stage.sh`'s `recogniser` argument. Unknown-verb stop and header usage (:7-16) name it; header exit codes (:13) gain 1, a `drive-check` FAIL.

**Step 5 - `stage.sh`.** Third argument `recogniser` (else usage, exit 2) stages, by the cp / `git show` rule, from `Runtime/Commands/`: `VoxrCommandRecogniser, GrammarManager, UtteranceBuffer, CommandDebouncer, CommandSetManager, DynamicSlotManager, PendingCommandHandler, VoxrCommandSet, VoxrPendingAmbiguity, VoxrSlotAsset, VoxrCommandSetAsset, VoxrCommandAsset`; from `Runtime/`: `VoxrSpeechRecogniser, VoxrBridgeErrorCode, VoxrJsonParser, VoxrGrammarValidator`; plus the three new rig files. Skipped: shipped `BridgeNative`, `ModelExtractor` (stood in); `EditorMicBackend`, `Native/VoxrNative`, `Native/VoxrGrammarVocabulary` (wholly `#if UNITY_EDITOR_WIN`). Without it, as today; usage (:4, :10-11) names it.

**Step 6 - `Rig.csproj`** comment: `joined` lists `drive-check`; `JoinedRecogniser` registers by module initializer.

**Stops (PM):** a build error naming anything unlisted; any `Runtime/` change; a drive-check FAIL or stop; a false runtime claim; criterion 5 or 6 off. A canary edits a `.scratch/` staged copy only.

**Commit set (PM, `vc` binding):** the eight AB files, `plan-1.md`, `architecture.md`, memory notes; nothing under `.scratch/` or `Runtime/`. No CHANGELOG line.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| K + 3 frames | K | Delivery, flush, timeout each up to a frame late; a timely timeout pends at K |
| `isDebugBuild` true | false | Debug build (`VoxrCommand.cs` :130-149 `#if DEBUG`; claim h); gates VCR :619's warning |

## Acceptance criteria

The gate: criteria 2-6, in order; 7-8 follow, outside it. Warnings: grep of MSBuild output.

1. [ ] Step 0 recorded. (F11)
2. [ ] `stage.sh main .scratch/jrr-p1 recogniser`; there `dotnet build -v q --nologo`: 0 errors, warnings counted; top level (bar `bin/`, `obj/`) = criterion 7's plus Step 5's files. (F3; §10 item 1; §8; claims b, h, i)
3. [ ] `git status --short -- Runtime/` empty. (§2 Out 1)
4. [ ] There `dotnet run --no-build -- --joined drive-check`: exit 0, `5/5`. (F3; F4; claims a, c, e-h, j)
5. [ ] DocCheck verbatim: exit 0, no `FAIL` line, `<n>/<n>` equal; warnings as Step 0. (F11; claim d)
6. [ ] `joined.sh --vary parser --dump .scratch/joined/p4.jsonl --ref-a main --ref-b main --out .scratch/joined/jrr-p1-f11`: exit 0, `Flips: 0`, 15 scored per side; counts per `joined-rig-parser/architecture.md` Tuning record (:124): correct 15 (no-fire 1), others 0, not scored 1, sealed-skipped 0. (F11)
7. [ ] Criterion 4's run in `.scratch/doccheck`: exit 2, one `joined: ` line naming the recogniser stage; top level (bar `bin/`, `obj/`) only the pre-edit `SOURCES=(` (ten) and rig-file `cp` lists' files, `STAGED_FROM`. (F11; claim d)
8. [ ] `git status --short`: Step 0's lines plus commit-set paths. (§2 Out 1)

## Open questions that block implementation

None.

## Blast radius

- Every parser stage (DocCheck, `joined.sh`) compiles the stub and new `Joined.Run`.
- Phase 3 binds to `RunWorld`, the timeline entry, the hook.
- Horizon K + 3 frames, by the ruling of 2026-10-15, recorded in the register's *The clock*.

## Runtime claims

- a (4): the completed stand-in task keeps `InitialiseAsync` on thread 1; the shipped one yields (`ModelExtractor.cs` :88) and, no `SynchronizationContext`, resumes off it, failing the main-thread asserts (VSR :561-564; VCR :332, :350).
- b (2): the staged sources compile unedited against the stub, no Unity define (§8); `UnityEngine.Object`, `Debug` resolve beside `System`.
- c (4): the stand-in needs no `unsafe` (no `AllowUnsafeBlocks`).
- d (5, 7): the module initializer sets the hook before `Main`; a parser stage compiles and runs as today.
- e (4): `DispatchJsonResult` parses the scripted payloads as Vosk's; at shipped defaults "cease fire" fires `cease_fire`; "cease", "fire" apart do not.
- f (4): `pending` times out before K; `OnDestroy` (VSR :658-661) frees the claim (:53) via `ReleaseNativeResources` (:283-312) for the next world.
- g (4): reflected `SendMessage` reaches private lifecycle methods, skipping absent ones.
- h (2, 4): `dotnet build`'s default Debug configuration defines `DEBUG`.
- i (2): `git show main:<path>` works for each Step 5 path.
- j (4): under `dotnet run`, `Main` runs at `ManagedThreadId == 1` (claim a's premise).
