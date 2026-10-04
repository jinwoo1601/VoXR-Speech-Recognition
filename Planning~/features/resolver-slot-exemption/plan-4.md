# Phase 4 — persisted plan (2026-10-03)

The Batch Test window field (F11, R2, Could). Anchors at `a9628e5`; new tests' comments name F11. W = `Editor/VoxrBatchTestWindow.cs`; T = `Tests~/Editor/VoxrBatchTestWindowRegisteredSlotsTests.cs` (new); CW = `Tests~/Editor/VoxrBatchTestWindowCoverageWeightTests.cs` (not edited, the pattern T copies); R = `Runtime/Testing/VoxrBatchTestRunner.cs` (not edited). Long form = `launch {quantity} {weapon} from tube {tube} at {target}`, 8 required elements, slots as `VoxrBatchTestRunnerTests.cs` `LongFormSlots()` (:222-229); IAG = `IncompleteAboveGate` (that file :220), `launch all missiles from tube three at`. DR-8: matched +1 raw / +1 den, charged miss -1 / +1, exempt miss 0 / 0; IAG consumes all its tokens (coverage 0). T is a departure from the Build-plan Phase 4 row, which names no test: approved by the human in plan mode on 2026-10-03. Out of scope: `Runtime/`, diagnostics (R3), product docs, the CHANGELOG line, locked docs.

## Files to change

| Path | Change | Why |
|---|---|---|
| W | modify | F11 (steps 1-2) |
| T | create | F11 test (step 3); departure ruled 2026-10-03 |

Ordered steps; the gate runs after step 3, one check-in.

1. **Window field (W).** `[SerializeField] string[] registeredSlotNames;` directly after `activeSetNames` (W:21). `SerializedProperty _propRegisteredSlotNames;` directly after `_propActiveSetNames` (W:39). In `OnEnable`, after W:53: `_propRegisteredSlotNames = _serializedSelf.FindProperty(nameof(registeredSlotNames));`. In `DrawConfiguration`, directly after the "Active Sets" `PropertyField` (W:90-91), before `EditorGUILayout.Space(2)` (W:93): `EditorGUILayout.PropertyField(_propRegisteredSlotNames, new GUIContent("Registered Slots", <tooltip>), true);`, the tooltip exactly: `Slot names your game registers a resolver for; the run scores them as the runtime does.` The `Update` (W:65) / `ApplyModifiedProperties` (W:73) bracket in `OnGUI` covers it; no other change there.
2. **`CreateRunner` (W:326-360).** Append `registeredSlotNames` as the 7th positional argument to both ctor-B calls: W:344-345 (explicit filter) and W:352-353 (all-sets fallback). No window-side null/empty logic: R's `ToRegisteredSlots` (R:95-103) maps null or empty to no source, an Inspector string array holds no null elements, and an empty-string entry is a name absent from the grammar, which the runner ignores. Header `Depends:` (W:5) unchanged.
3. **Test (T).** Namespace `VoXR.Tests.Editor`; the repo's header block (Purpose / Layer `Tests.Editor` / Owns `VoxrBatchTestWindowRegisteredSlotsTests (public class)` / Depends `VoxrBatchTestWindow, VoxrBatchTestRunner, VoxrSlotAsset, VoxrCommandAsset, VoxrCommandSetAsset`); no `.meta` file. CW's pattern: `ScriptableObject.CreateInstance<VoxrBatchTestWindow>()`; `WindowField` / `SetField` by `BindingFlags.Instance | BindingFlags.NonPublic` (CW:70-81); `UseActiveSetNames(bool)` (CW:85-89) giving `{ "combat" }` or `Array.Empty<string>()`; `CreateRunner` by reflection (CW:91-103); every asset `DestroyImmediate`d in `[TearDown]`. Fixture: four `VoxrSlotAsset`s (`slotName`, `values` as `LongFormSlots()`, `slotType` left `Enumerated`, no aliases); one `VoxrCommandAsset`, `intent = "launch_weapon"`, `patterns = { <long form, space-separated> }`; one `VoxrCommandSetAsset`, `setName = "combat"`; window `slotAssets` = the four, `commandSetAssets` = the set. `BuildSlots` (W:362-372) / `BuildSets` (W:374-384) yield the long-form grammar through `ToDefinition` / `ToSet`, so IAG is reused. `minScore`, `minConfidence`, `coverageWeight` left at the window defaults (0.6, 0.4, parser default). Two tests, each `[TestCase(true)]` / `[TestCase(false)]` over `UseActiveSetNames`, so both ctor-B calls are covered — criteria 2-3.

## Contracts and interfaces

- New serialized field `registeredSlotNames` (`string[]`) on `VoxrBatchTestWindow`, persisted by Unity's EditorWindow serialization like `activeSetNames`; the field name is its serialization key. Label "Registered Slots".
- The window passes it to `VoxrBatchTestRunner`'s ctor B as `registeredSlotNames` (Phase 3's contract, unchanged). No public surface change: the window class stays `public`, its fields and `CreateRunner` private.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| A new test file T, as ruled | extend CW | CW's fixture is slotless and named for coverage weight |
| Reuse the long form and IAG | a smaller one-slot grammar | the assets express it, and Phase 3 already proved its arithmetic |
| No window-side filtering | strip null/empty in the window | the runner owns the rule (R:95-103); one copy |
| Unset case sets the field to `null` explicitly | rely on `CreateInstance`'s default | the serializer may initialize the array empty; explicit null states the case the test names |

## Acceptance criteria

- [ ] 1. `verification` (`.claude/bindings/unity.md`) Command 1: EditMode `failed="0"`, `inconclusive="0"`, total 201 (Phase 3's 197 plus T's 4 cases); Command 2: PlayMode 702/702, `failed="0"`, revert clean. CW and `VoxrBatchTestRunnerTests.cs` green unedited. Command 4 not run: no `Runtime/` public API and no `Samples~/` change (no `Samples~` reference to the window). Mutation: back up W byte-for-byte under `.scratch/`, drop `registeredSlotNames` from the W:344-345 call, Command 1 → `CreateRunner_RegisteredSlotNames_ReportsWouldAskResolver(True)` red and its `(False)` case green; restore W from the backup, confirm by a Python byte compare (never `git checkout`, never `grep -v | cmp`), Command 1 green.
- [ ] 2. T `CreateRunner_RegisteredSlotNames_ReportsWouldAskResolver(bool explicitFilter)`: `registeredSlotNames = { "target" }`, `Run` `{ input = IAG, expectedIntent = "launch_weapon" }` → `Passed` false; `FailureReason == "expected intent 'launch_weapon' but rejected: would ask resolver for 'target'"` (R:205, R:276-277); `Score == 1f` (tol 0.001): 7 matched, `{target}` exempt, 7/7.
- [ ] 3. T `CreateRunner_NoRegisteredSlotNames_KeepsRequiredSlotUnfilled(bool explicitFilter)`: `registeredSlotNames` set to `null`, same case → `Passed` false; `FailureReason` ends `required slot unfilled` (R:257); `Score == 6f / 8f` (tol 0.001): raw 7-1 = 6, den 8, clears `minScore` 0.6, so the completeness gate is the reason.
- [ ] 4. Manual Editor check (F11's acceptance), human-only, reported as deferred, never claimed: open Window > VoXR > Batch Test Runner; the "Registered Slots" list is drawn directly under "Active Sets", with the tooltip; with a suite case that leaves a registered required slot unfilled, Run All → the expanded row shows `Failure: … would ask resolver for '<slot>'`; the entry survives a domain reload (a script recompile).
- [ ] 5. Diff: the files table plus `plan-4.md`, `architecture.md`, `memory/**`, `.scratch/**` only; the mutation reverted.

## Open questions that block implementation

None.

## Blast radius

- Saved window state from before this phase has no `registeredSlotNames`; it deserializes empty — no source, today's run.
- CW's fixture never sets the field, so its runners get null or empty — no source; unaffected.
- `RunAll` (W:303) and `RerunFailed` (W:314) reach the field through `CreateRunner`; no other window code reads it.
- `Documentation~/editor-testing.md`, `Documentation~/api/batch-test-runner.md` ("F11's field if built"): `codex-reconcile` after G2. CHANGELOG at close-out.
- Editor-only; R, the parser and `Samples~` untouched.

## Runtime claims

- Crit. 2: the window's assets, through `BuildSlots` / `BuildSets` and ctor B with `{ "target" }`, score IAG 7/7 and give the `would ask resolver for 'target'` reason, on both ctor-B calls.
- Crit. 3: with `null` names, IAG scores 6/8 and the reason ends `required slot unfilled`, on both calls.
- Crit. 1: CW and the runner tests stay green unedited; EditMode totals 201; the mutation turns only the `(True)` case of criterion 2 red, and the restore returns green.
- Crit. 4: the list draws, the run reports the verdict, and the entry survives a domain reload — the human's check.
