# Phase 1 — persisted plan (2026-10-02)

DR-8 at every in-package site, and DocCheck. Anchors at `3b16338`; new tests' comments name their requirement. P / R / H / D = `Runtime/Commands/` `VoxrCommandParser.cs` / `VoxrCommandRecogniser.cs` / `PendingCommandHandler.cs` / `DynamicSlotManager.cs`; PT / IT / PE / DS = `Tests~/Runtime/` `VoxrCommandParserTests.cs` / `VoxrCommandRecogniserInjectionTests.cs` / `VoxrPendingCommandTests.cs` / `VoxrDynamicSlotTests.cs`; DC = `Planning~/features/coverage-in-selection/ab-rig/DocCheck.cs`. Reg {x} = `registeredSlots: new RegisteredSlotNames(new[] { "x" })`; demo set = `weapon, target, range, heading`.

## Files to change

| Path | Change | Why |
|---|---|---|
| P | modify | F1, F2, F6 (steps 1-2); comments, one log string (step 3) |
| R, H, D | modify | comments only (H, D ruled out-of-row at PLAN) |
| PT, IT, PE, DS | modify | criteria 2-11; re-pins, test comments (step 4) |
| DC | modify | F9 (step 5) |

Ordered steps; 1-2 turn the re-pinned tests red until Step 4, the gate runs only after Step 4, the phase landing as one check-in after it.

1. **P `TryMatchScored` (P:3686).** `byte exemptedSlots = 0;` beside `missedRequiredSlot` (P:3715). In `else if (!isOptional)` (P:3771), P:3773-3774 run only if `!(_anyExempt && _exemptSlot[slotIdx])`, else `exemptedSlots++` (`_anyExempt` first: the mask is null with no source, stale with none registered). P:3779-3789 (latch, `missedRequired++`, `missedRequiredSlot`, `requiredAfterLastMatch++`) stay unconditional (adr-0003). The return (P:3875-3887) sets `ExemptedSlots`. The start probe takes the same arithmetic.
2. **P `ScoreFollowUp` (P:4589).** The unfilled-required branch (P:4636-4640) skips its -1/+1 when `_anyExempt && _slotIndex.TryGetValue(slotName, out int idx) && _exemptSlot[idx]`. The snapshot (P:4592) stays first.
3. **Comments and one runtime log string** (the sibling-tie warning P:2439-2441, pinned by no test), no logic change:

| Site | Rewrite to say |
|---|---|
| P:201-216 | -1.0 only for a slot with no registered resolver; a registered one is 0/0 yet still missed (DR-8) |
| P:4171-4175 `BuildCoverageTables` | running it first also snapshots the set the exemption reads |
| P:4832-4846 eager #66 note | an exempt miss costs no score; this guard alone refuses a medial one, the tail guard (P:4874) a trailing one too (M2) |
| P:3396-3398 | pending is fed by slot-missing candidates below minScore and incomplete ones above it (#73, R:1326-1331), incl. resolver-declined ones (#148, DR-8) |
| P:1274-1280; warning P:2439-2441 | add "same exempted-slot count" between score and span |
| R:388-397 | from the next utterance a resolver-backed missed slot costs nothing; the rest of what went unspoken holds a candidate under the floor; both floors stay |
| H:142-146 | an exempted rival can equal the score, but `CandidateOrder.Tied` (P:2720, P:4779) needs equal exempted counts: DR-9 parts only a zero-exempted complete winner from exempted rivals; an equally exempted rival still ties and, chosen, fires incomplete (IT:2888, :2907-2912) |
| D:6-7 `Depends:` | (add) `IRegisteredSlotNames` |
| rest of P:3389-3454, P:3498-3501, P:4145-4148, P:1774-1778, P:4850-4873, R:186-197, R:1313-1319 | (true; leave) |

4. **Re-pins and test comments**, each traced to A5. 32 resolver tests (IT 15, PE 13, DS 4); "reg."/"unreg." = with/without a `track` resolver:

| Test | Under DR-8 | Edit |
|---|---|---|
| IT:2728 | 1.0 reg. vs 0.75 unreg.: equality breaks | becomes F5 (crit. 9) |
| PE:2144 | 3/3, resolves, fires: premise breaks | `ConfigureResolvableSync(true)` + track resolver, "launch all missiles three" = 4/7 = 0.571; asserts on `track` |
| IT:2557 | 7/7 both halves | :2566-2569, :2646-2649 |
| IT:3077 | tie 6/7, exempted 1 each | :2993-2998; :3067-3069 adds the key |
| IT:3381 | entry 6/8, over the gate; all-or-nothing stops at `{tube}` | :3462-3464 |
| IT:3529, 3626, 3721 | 7/7, 11/11, 8/8 | :3600, :3639-3640, :3792 |
| IT:2844, 2888 | tie 7/8, exempted 1 each | :2783-2786 |
| PE:2005 | 9/11; the 8-element form 5/7 | :2012-2018 |
| PE:2237 | `PartialLaunch` 5/7, over the gate; `{tube}` keeps it the ordinary pending | :2261-2262 |
| PE:2570 | entry 2/4; follow-up 0/8, still non-positive | :2583-2586, :2606-2607, :2613 |
| IT:2467-2473, PE:1879-1893, DS:431-441 | `BareLaunch` 7/7 = 1.0 reg., 6/8 = 0.75 unreg. | rewrite |
| other 19 (incl. DS's 4) | nothing moves | none |

5. **DC.** The §7 A (DC:116) and §6 (DC:258) parsers take the demo set; DC:141 `0.167f` -> `0.400f` (2/(3+2)); DC:275 `0.333f` -> `1.00f` (2/2). No check added: 64 sites, DC:823 runs 3x = 66.

## Contracts and interfaces

No signature changes. `MatchResult.ExemptedSlots` is now written; `ScoreFollowUp` scores a registered unfilled required slot 0/0.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| F1 `drive cut drive` on an in-package grammar | Set_Combat's `set_burn` | in no input; 1/3 via the forced orphan table, 1/2 if the selector lost the miss |
| F5 by re-pinning IT:2728 | a new test | its purpose under A5 is F5's |
| F2's exempt anchor by IT:2557 | a new test | its first half is that shape under DR-8 |
| Runner-up test in PT, `#if UNITY_EDITOR` | an Editor test file | stays in the ruled test files |

## Acceptance criteria

- [ ] 1. `verification` Commands 1-2 green; no existing test edited beyond step 4 (F8). Command 4 n/a: R:388-397 on public `VoxrCommandRecogniser` is XML-doc prose; no signature or `Samples~` change.
- [ ] 2. PT `ExemptSlot_ElidedSlot_ScoresTwoThirds` (F1): `launch missiles target {track}`, track {alpha, bravo}, "launch missiles": Reg {track} 2/3; no source 0.25.
- [ ] 3. PT `ExemptSlot_KeepsTheForcedOrphanCharge` (F1): burn_level {full, half}; `set_burn` = `cut {burn_level}`, `drive_slow` = `drive slow now`; Reg {burn_level}; "drive cut drive" -> one result, `set_burn`, 1/3 = 1/(1 + 1 skipped + 1 forced orphan).
- [ ] 4. PT `ExemptSlot_Stutter_KeepsTheRealCommand` (F2): `MakeSlots`/`MakeCommands`, demo set; "launch launch all missiles target hotel one" -> one result, `launch_weapon` 5/6, weapon, quantity, target filled.
- [ ] 5. F2: PT `ExemptSlot_MedialMiss_IsNotEagerCommitted`: track {alpha}; `fire_at` = `fire {track} now`; Reg {track}; `TryEagerCommit(["fire","now"], null, 0.6f, 0f)` == `None`: 2/2 = 1.0 (no source 1/3), admitted (missed 1, matched 2), no leading latch (`fire` matched), no tail (`now` matched last), so only P:4847 refuses. IT `Resolver_ElidedCommand_FiresAtTheFlush`: `ConfigureResolvableSync`, `BufferWindow = 1.5f`, track fills; nothing fired after `InjectText(BareLaunchOrder)`; fired, track alpha, after `FlushPendingBuffer()`.
- [ ] 6. IT:2557 green, its comment stating F2's exempt anchor: 7/7, barred, `trackCalls` 0.
- [ ] 7. IT `Resolver_SlotLessSibling_WinsOnFewerExemptedSlots` (F3): track {alpha, bravo}, burn_level {hard burn}; `intercept_target` #0 `intercept track {track} {burn_level}`, #1 `intercept track {track}`; resolvers on both; "intercept track alpha" fires #1; burn_level resolver calls 0.
- [ ] 8. IT `Resolver_RegisteredSet_IsReadFromTheNextUtterance` (F4): `ConfigureResolvableSync(true)`, `BareLaunchOrder`, pending `Score` read, `CancelPendingCommand()` between, no `NotifySlotChanged`: resolver for `bearing` (not in grammar) 0.75, no throw; declining `track` resolver 1.0; after `UnregisterSlotResolver("track")` 0.75.
- [ ] 9. F5: IT:2728 re-pinned — one `track` resolver fills, then declines by a flag; fired and pending `Score` (1.0) and `Confidence` equal; its unregister arm dropped (criterion 8 holds it).
- [ ] 10. F6: PE `Resolver_FollowUpReScore_LeavesARegisteredSlotUncharged`: `PartialLaunch` pends with no resolver; register track (fills); "three" fires at 1.0 (7/7, was 0.75). PT `ExemptSlot_RunnerUpScore_IsItsSelectionScore`: ship {alpha}, track {hotel}; `set_tracked` = `set {ship} on {track}` before `set_plain` = `set {ship} on`; Reg {track}; `Parse("set alpha on", null)` -> `set_plain`; `LastParseDiagnostics[0]` runner-up `set_tracked` at 1.0, its score parsed alone.
- [ ] 11. PT `ExemptSlot_AllocatesNothingPerCall` after PT:5986, in `LeadingMissLatch_AllocatesNothingPerCall`'s form: track {alpha}; `hold` = `bravo charlie`, `launch` = `bravo {track}`; Reg {track}; tokens `bravo charlie`; warm-up `Commit`; 100 calls `Is.Not.AllocatingGCMemory()`. Mutation, once at the gate: `GC.KeepAlive(new object());` first in `SnapshotRegisteredSlots`' loop turns it red.
- [ ] 12. DocCheck (F9), from the project root: `bash "Planning~/features/coverage-in-selection/ab-rig/stage.sh" WORKTREE .scratch/doccheck-rse-1`, then `wsl.exe -e bash -lc 'cd "/mnt/d/Workspace/VoXR-Speech-Recognition/.scratch/doccheck-rse-1" && dotnet build -v q --nologo && dotnet run --no-build -- --doccheck'` prints `66/66 doc claims verified against the real parser.`, exit 0.
- [ ] 13. The diff touches only the files table plus `plan-1.md`, `architecture.md`, `memory/**`, `.scratch/**`; criterion 11's mutation of P is reverted before the check-in.

## Open questions that block implementation

None.

## Blast radius

- Editor resolver tests (`VoxrCommandRecogniserDiagnosticTests.cs` :751, :812; `VoxrDebugSessionLogTests.cs` :489) assert state, not score; a red one stops the run.
- `Sweep.cs` reflects only on unchanged signatures.
- Hot path: one bool load per required-slot miss; no allocation (criterion 11).

## Runtime claims

- Crit. 12: `bash` is Git Bash, not the WSL launcher; the WSL `dotnet build` compiles the working-tree parser; only the two DC pins move; 66/66.
- Step 4, crit. 2-5, 7, 10b: every re-pinned, re-commented or new figure (derived statically); the 19 others stay green.
- Crit. 5: only P:4847 refuses the PT medial miss (deleting it gives `Commit`); P:4874 also refuses the IT tail shape, which a 1.5 s window holds to the flush.
- Crit. 8-10: `Score` reaches the pending or fired command unchanged through Step 3b, Step 5 and `WithResolvedSlots`.
- Crit. 7, 11: neither the intercept nor the `hold`/`launch` pair warns at construction; crit. 11 warms up to `Commit`, is green, and reds under the mutation.
- Crit. 10b: the Runtime test assembly defines `UNITY_EDITOR` (`LastParseDiagnostics`; PE:1319).
