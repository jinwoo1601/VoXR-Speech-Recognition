# Phase 3 — persisted plan (2026-10-03)

The batch runner given names (F7, R1). Anchors at `f645258`; new tests' comments name F7. R = `Runtime/Testing/VoxrBatchTestRunner.cs`; P = `Runtime/Commands/VoxrCommandParser.cs` (not edited); RT = `Tests~/Editor/VoxrBatchTestRunnerTests.cs`; IT = `Tests~/Runtime/VoxrCommandRecogniserInjectionTests.cs`. Long form = RT:222-251's `launch {quantity} {weapon} from tube {tube} at {target}`, 8 required elements; IAG = `IncompleteAboveGate` (RT:220), `launch all missiles from tube three at`. DR-8: matched +1 raw / +1 den, charged miss -1 / +1, exempt miss 0 / 0; every input below consumes all its tokens (coverage 0). Out of scope: Phase 4, diagnostics (R3), P, product docs, locked docs.

## Files to change

| Path | Change | Why |
|---|---|---|
| R | modify | F7 (steps 1-3) |
| RT | modify | F7 tests; test-local helper extraction (step 4) |
| IT | modify | F7 score parity; test-local helper extraction (step 5) |

Ordered steps; the gate runs after step 5, one check-in.

1. **Constructors.** Ctor A (R:21-23) and ctor B (R:38-40) each gain `string[] registeredSlotNames = null` last. New field `readonly IRegisteredSlotNames _registeredSlots;` beside R:16-19. One new `static IRegisteredSlotNames ToRegisteredSlots(string[] registeredSlotNames)`: null or empty -> `null` (no source, today's runner); a null element -> `ArgumentNullException(nameof(registeredSlotNames))`, as `RegisterSlotResolver(null, …)` does (`RegisteredSlotNames`, P:150-165, accepts one); else `new RegisteredSlotNames(registeredSlotNames)` (ordinal copy). Each ctor sets the field after its existing checks (R:25-26; R:42-44) and passes `registeredSlots: _registeredSlots` at R:32 / R:67. A name absent from the grammar needs no code (the snapshot looks up grammar slots only). No overload, no setter.
2. **Verdict, `PassesThresholds` (R:192-234).** Order score (R:194) -> completeness (R:218-225) -> confidence (R:226) unchanged. In the `if` at R:218-225: when `_registeredSlots != null`, walk `new VoxrCommandParser.UnfilledRequiredSlots(cmd, def)` (P:4560; pattern order; a repeated slot yielded twice, P:4557-4559); if every name is `Contains`ed, `rejectReason` = `would ask resolver for ` + each distinct name once, first-occurrence order, single-quoted, `", "`-joined (`would ask resolver for 'track'`); otherwise, and always with no source, `required slot unfilled`. Return false in both (ruled at PLAN). The list and string exist only on this path; complete and no-names runs allocate nothing new. `VoxrTestResult`, `ToCsv`'s header (R:325), other strings and `MakeResult`'s Editor-only call (R:287) unchanged.
3. **Comments.** R:200-217 rewritten to say: completeness still tracks the recogniser's gate (#73); given the game's registered slot names the runner scores as the runtime does (same snapshot, same arithmetic) and reports `would ask resolver for '…'` where the runtime would consult a resolver; it never calls resolvers (it cannot know whether the game fills or declines), so that verdict is a rejection; one-way cut: a case expecting rejection passes on it and a passing row carries no CSV reason, though the runtime may fire if the game fills; with no names it rules on the utterance alone as before, and `required slot unfilled` may be a slot the game resolves. Drop "registry lives on VoxrCommandRecogniser" (it is `DynamicSlotManager`). `Depends:` (R:5) adds `RegisteredSlotNames`.
4. **RT.** RT:224-250's arrays move into `static LongFormSlots()` / `LongFormCommands()`; `CreateLongFormRunner(string[] registeredSlotNames = null, float minScore = 0.6f)` builds ctor A from them. Callers RT:256, RT:289 unedited. New tests after RT:302: criteria 2-8.
5. **IT.** `ConfigureResolvableSync`'s (IT:2478-2513) arrays move into `static ResolvableSlots()` / `ResolvableCommands(bool allowPartial)`, passed to `Configure`; callers unedited. Add `using VoXR.Testing;`. New test after IT:2792: criterion 9.

## Contracts and interfaces

- Both public ctors end `…, float coverageWeight = VoxrCommandParser.DefaultCoverageWeight, string[] registeredSlotNames = null)`. Existing callers compile unchanged (`Editor/VoxrBatchTestWindow.cs` :344, :352; RT; none in `Samples~`).
- New reason `would ask resolver for '<a>'[, '<b>'…]` -> `FailureReason` (`expected intent '<x>' but rejected: …`) -> CSV `Reason`, quoted by `CsvEscape` on a comma. No new field, column or status.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| One helper for both ctors | the rule inline twice | one copy of the rule |
| Walk only with a source | always walk | no-names path is today's code exactly |
| De-dup in the runner | in the walk | P:4557-4559 leaves it to the consumer |
| Parity test in IT | in RT | needs IT's recogniser fixture; the runner is Runtime-assembly |
| Crit. 4 at `minScore: 0.3f`, as ruled | default 0.6 | 5/7 = 0.714 clears 0.6 too; margin only |

## Acceptance criteria

- [ ] 1. `verification` (`.claude/bindings/unity.md`) Commands 1-2 green; every existing test (RT, `VoxrBatchTestWindowCoverageWeightTests`, IT) green unedited beyond steps 4-5's extractions. Command 4 per `Planning~/verification-recipe.md` `## Compiling Samples~`: `Build succeeded`, `0 Error(s)`, the one CS0618, 7 sample files, no `Samples~` edit; mutation: an error in one sample turns it red naming that file and line, `git checkout --` restores, green returns. DocCheck not re-run.
- [ ] 2. RT `Run_RegisteredSlotUnfilled_RejectedAsWouldAskResolver`: names `{ "target" }`, IAG, expected `launch_weapon` -> not passed; `FailureReason == "expected intent 'launch_weapon' but rejected: would ask resolver for 'target'"`; `Score == 1f` (tol 0.001): 7 matched, `{target}` exempt, 7/7.
- [ ] 3. RT `Run_TwoRegisteredSlotsUnfilled_NamedInPatternOrder`: names `{ "tube", "target" }`, input `launch all missiles from tube at` -> `FailureReason` ends `would ask resolver for 'tube', 'target'`; `Score == 1f`: 6 matched, both exempt, 6/6. `ToCsv(runner.RunAll(…))` contains that `FailureReason` in double quotes (score cell not asserted: culture-formatted).
- [ ] 4. RT `Run_UnregisteredSlotUnfilled_KeepsRequiredSlotUnfilled`: names `{ "target" }`, `minScore: 0.3f`, criterion 3's input -> `FailureReason == "expected intent 'launch_weapon' but rejected: required slot unfilled"`; `Score == 5f / 7f`: `{tube}` charged, raw 6-1 = 5, den 6+1 = 7.
- [ ] 5. RT `Run_ExpectedRejection_RegisteredSlotUnfilled_Passes`: names `{ "target" }`, IAG, `expectedIntent = null` -> `Passed`, `ActualIntent == null`, `FailureReason == null`, `Score == 1f`.
- [ ] 6. RT `CommandSetConstructor_RegisteredSlotNames_SameVerdict`: ctor B, set `combat` = `LongFormCommands()`, active `{ "combat" }`, names `{ "target" }`, IAG -> criterion 2's reason, `Score == 1f`.
- [ ] 7. RT `Constructors_NullRegisteredSlotName_Throws`: `{ "target", null }`, other arguments valid -> `ArgumentNullException`, ctor A and ctor B.
- [ ] 8. RT `Run_EmptyRegisteredSlotNames_BehavesAsNoNames`: `new string[0]`, IAG -> `Score == 6f / 8f` (raw 7-1, den 8), reason `required slot unfilled`. RT:254 stays the unedited null-names anchor.
- [ ] 9. IT `Resolver_BatchRunnerGivenNames_ScoresAsTheRecogniser`: `ConfigureResolvableSync(true)`, filling `track` resolver, `InjectText(BareLaunchOrder, CreateSimulatedWords(BareLaunchOrder, 0.9f))` fires; ctor A over `ResolvableSlots()`, `ResolvableCommands(true)`, names `{ "track" }`, `Run` `{ BareLaunchOrder, launch_weapon, wordConfidence 0.9f }` -> `Score` equals the fired `Score` within `1e-5f` (7/7 = 1.0); `FailureReason == "expected intent 'launch_weapon' but rejected: would ask resolver for 'track'"`.
- [ ] 10. Diff: the files table plus `plan-3.md`, `architecture.md`, `memory/**`, `.scratch/**` only; the sample mutation reverted.

## Open questions that block implementation

None.

## Blast radius

- The window's `CreateRunner` (ctor B, positional) compiles unchanged; its reflection test (`VoxrBatchTestWindowCoverageWeightTests.cs` :91-103) is unaffected. Phase 4 adds the field.
- Editor `MakeResult` (R:287) re-runs `PassesThresholds`, so attempts carry the new reason text; no shape change (R3).
- `Documentation~/api/batch-test-runner.md`, `Documentation~/editor-testing.md`, `KNOWN_LIMITATIONS.md` :805-815: `codex-reconcile` after G2.
- R is off the hot paths; P untouched.

## Runtime claims

- Crit. 2, 5, 6, 9: IAG / `BareLaunchOrder` with the tail slot registered scores 7/7 in the runner, as IT:2735 shows at the recogniser.
- Crit. 3, 4: `launch all missiles from tube at` yields a `launch_weapon` result, 6 matched, `{tube}` medial and `{target}` tail misses, all tokens consumed: 6/6 and 5/7; the walk yields `tube` before `target`.
- Crit. 8: an empty array keeps 6/8 and `required slot unfilled`.
- Crit. 9: the recogniser's parser and the runner's score `BareLaunchOrder` equally.
- Crit. 7: the throw comes from `ToRegisteredSlots`.
- Crit. 1: existing tests green unedited; Command 4 green with no sample edit, red under the mutation.
