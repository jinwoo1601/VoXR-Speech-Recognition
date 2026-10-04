# Phase 0 — persisted plan (2026-10-02)

The seam, zero behaviour change. Anchors live on 2026-10-02: P = `Runtime/Commands/VoxrCommandParser.cs`, D = `Runtime/Commands/DynamicSlotManager.cs`, R = `Runtime/Commands/VoxrCommandRecogniser.cs`, T = `Tests~/Runtime/VoxrCommandParserTests.cs`.

## Files to change

| Path | Change | Why |
|---|---|---|
| P | modify | F4 seam (interface, fixed set, ctor parameter, mask, snapshot); F6 plumbing (snapshot in `ScoreFollowUp`, key at runner-up); F3 (`ExemptedSlots`, DR-9 key); F8 (no source = today's parser) |
| D | modify | F4 — the live source (adr-0002) |
| R | modify | F4 — the recogniser hands the parser its registry |
| T | modify | F3 — a candidate-comparison test per key; the 32-byte pin |

Ordered steps. Steps 5–8 and 11 leave the build red between them: the phase is green only at its end and lands as one atomic check-in; the step order stands.

1. **P, after `SiblingSet` (P:123-135)**: `internal interface IRegisteredSlotNames` with `bool Any { get; }` and `bool Contains(string slotName)` (ordinal); `internal sealed class RegisteredSlotNames : IRegisteredSlotNames` over a `HashSet<string>` with `StringComparer.Ordinal`, copied at construction from `IEnumerable<string> names` (null → empty); `Any` is count > 0. The file header's `Owns:` line (P:4) adds `IRegisteredSlotNames` and `RegisteredSlotNames`.
2. **P, constructor (P:466-472)**: append `IRegisteredSlotNames registeredSlots = null` after `minScore`. Fields beside `_slotNames` (P:234): `readonly IRegisteredSlotNames _registeredSlots`, `readonly bool[] _exemptSlot`, `bool _anyExempt`. After the `_slotNames` fill (P:503-507): store the source; `_exemptSlot = new bool[slots.Length]` only when it is non-null.
3. **P, private `SnapshotRegisteredSlots()`** beside `BuildCoverageTables`: no source → return; `!Any` → `_anyExempt = false`, return; else `_exemptSlot[i] = Contains(_slotNames[i])` for every `i`, `_anyExempt` = any set. First statement of `BuildCoverageTables` (P:4083, before `int n` P:4085 — so before the table fill P:4104 and the start-probe loop P:4139-4150) and of `ScoreFollowUp` (P:4494, before the lookup P:4497). Nothing reads the mask in Phase 0.
4. **P, `MatchResult` (P:3325)**: `internal byte ExemptedSlots;` directly after `LeadingRequiredMissed` (P:3390), before `EndIdx` (P:3394); its comment: this candidate's exempt missed required slots, bound 255 per pattern. Layout note (P:3375-3389): change only its padding fact (`ExemptedSlots` now fills the last byte before `EndIdx`) and its closing "no size pin anywhere" sentence (now: the size test pins it). Nothing writes the field in Phase 0.
5. **P, `CompareCandidate` (P:3457-3526)**: append `int bestExemptedSlots`, no default. After the score key (P:3511-3512), before consumed span (P:3513): unequal → `candidate.ExemptedSlots < bestExemptedSlots` ? `Better` : `Worse`. Floor, admission and no-incumbent (P:3466, P:3498, P:3506) unchanged. Key-list comment (P:3434-3437) names the key between score and span.
6. **P, flush (`ParseInternal`)**: `int bestExemptedSlots = 0;` with the `best*` locals (P:2496-2504); passed last at P:2584-2591; set from `matchResult.ExemptedSlots` in the adopt block (P:2608-2616).
7. **P, Editor runner-up (`#if UNITY_EDITOR`)**: `int runnerUpExemptedSlots = 0;` with the `runnerUp*` locals (P:2519-2523); on displacement (P:2599-2606) takes `bestExemptedSlots`; passed last at P:2760-2767; on adopt (P:2770-2774) takes `matchResult.ExemptedSlots`.
8. **P, eager (`TryEagerCommit`)**: as step 6 — locals P:4612-4621, call P:4654-4661, adopt P:4665-4676. `TryMatchScored` (P:3617) and `TryEagerCommit` (P:4593) signatures unchanged.
9. **D**: `DynamicSlotManager : IRegisteredSlotNames` (D:14), explicit implementation: `IRegisteredSlotNames.Any` → `HasResolvers` (D:108); `IRegisteredSlotNames.Contains(slotName)` → `_resolvers != null && _resolvers.ContainsKey(slotName)`. Nothing else.
10. **R**: `registeredSlots: _slotManager` as last argument in `Configure` (R:277-283) and `RebuildParser` (R:463-469).
11. **T, mechanical**: `Against` (T:6338-6341) and the four explicit calls (T:6367, :6387, :6404, :6416) pass `0` last; assertions unchanged. `Cand()` (T:6320-6334) gains `byte exemptedSlots = 0` last, set as `ExemptedSlots` in its object initialiser. The comparator section comment (T:6312-6313) names the fewer-exempted key between score and span.
12. **T, new tests** after `CompareCandidate_EveryKeyEqual_IsTied` (T:6459-6465): criteria 3–4.

## Contracts and interfaces

- `IRegisteredSlotNames { bool Any { get; } bool Contains(string slotName); }` — internal, ordinal.
- `RegisteredSlotNames(IEnumerable<string> names)` — internal sealed, immutable.
- `VoxrCommandParser(…, float minScore = DefaultMinScore, IRegisteredSlotNames registeredSlots = null)`.
- `MatchResult.ExemptedSlots` — `byte`; struct stays 32 bytes.
- `CompareCandidate(in MatchResult candidate, int startIdx, float bestScore, int bestStartIdx, int bestConsumedEndIdx, int bestLiteralCount, int bestExemptedSlots)` — floor, admission, no-incumbent, start, score, **exempted (fewer better)**, span, literal count, `Tied`.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| Explicit implementation on `DynamicSlotManager` | Implicit members | Ruled by the PM at PLAN, 2026-10-02, in the Phase 0 architect brief (`.scratch/architect-resolver-slot-exemption-phase-0-brief.md`): keeps `DynamicSlotManager`'s internal surface unchanged |
| `IEnumerable<string>` input | `string[]` only | Arrays (Phase 3's runner, tests) pass as-is; one copy either way |
| Locals seeded `0` | A sentinel | The no-incumbent rule (P:3506) answers before the key |
| `UnsafeUtility.SizeOf<T>()` pin | `Marshal.SizeOf`; C# `sizeof` | `Marshal.SizeOf` measures the marshalled layout (bool = 4 bytes); `sizeof` needs unsafe code, which the test asmdef forbids. `SizeOf<T>() where T : struct` has a safe signature, in `UnityEngine.CoreModule`: no unsafe context, no asmdef reference |

## Acceptance criteria

- [ ] 1. `verification` Commands 1–2 green; no existing test edited beyond step 11 (F8 — `ExemptedSlots` always 0).
- [ ] 2. Every Source `CompareCandidate` call (P:2584, P:2760, P:4654) passes an exempted count, compile-enforced (F3, F6).
- [ ] 3. New tests green (F3, a test per key); "incumbent ex N" = `CompareCandidate(c, 0, 0.75f, 0, 4, 2, N)`:
  - `CompareCandidate_ExemptedSlots_FewerWinsWhenStartAndScoreAgree` — `Cand(exemptedSlots: 0)` vs incumbent ex 1 → `Better`.
  - `CompareCandidate_ExemptedSlots_MoreLosesWhenStartAndScoreAgree` — `Against(Cand(exemptedSlots: 1))` → `Worse`.
  - `CompareCandidate_Score_OutranksExemptedSlots` — `Against(Cand(score: 0.8f, exemptedSlots: 2))` → `Better`; `Cand(score: 0.7f)` vs incumbent ex 2 → `Worse`.
  - `CompareCandidate_ExemptedSlots_OutranksConsumedSpanAndLiteralCount` — `Cand(consumedEndIdx: 3, literalCount: 0)` vs incumbent ex 1 → `Better`; `Against(Cand(consumedEndIdx: 9, literalCount: 9, exemptedSlots: 1))` → `Worse`.
  - `CompareCandidate_ExemptedSlots_EqualFallsThroughToConsumedSpan` — `Cand(consumedEndIdx: 5, exemptedSlots: 1)` vs incumbent ex 1 → `Better`; with `consumedEndIdx: 3` → `Worse`.
- [ ] 4. `MatchResult_Size_IsThirtyTwoBytes` — `UnsafeUtility.SizeOf<VoxrCommandParser.MatchResult>()` == 32, via `using Unity.Collections.LowLevel.Unsafe;` (F3; decision *MatchResult*).
- [ ] 5. `_slotManager` passed at both recogniser constructions; the snapshot is the first statement of `BuildCoverageTables` and `ScoreFollowUp` (F4, F6 plumbing).
- [ ] 6. The source and test diff touches only the four files above (the process docs `plan-0.md` and `architecture.md` are outside this statement).

## Open questions that block implementation

None.

## Blast radius

- `Runtime/Testing/VoxrBatchTestRunner.cs` (:32, :67) and the 227 test constructions compile through the default; not edited (runner: Phase 3).
- `Planning~` rigs: DocCheck and Sweep reflect on `TryMatchScored`, `TryEagerCommit`, `BuildCoverageTables` — signatures unchanged; none calls `CompareCandidate`; not edited.
- Hot path (EFF angle): per pass one null test, plus one `HasResolvers` call under the recogniser; one `bool[]` per parser built with a source (each `RebuildParser`).
- Phase 1's comment list (layout note, key list) overlaps steps 4–5; Phase 0 edits them only as stated.
- Out of scope: `RegisteredSlotNames`, `DynamicSlotManager`'s `Any`/`Contains` and `SnapshotRegisteredSlots` get no dedicated test in Phase 0; their behavioural tests are F4's, assigned to Phase 1 by the architecture's `## Build plan`. Phase 0's zero behaviour change is pinned by the existing suites.
- Hand-forward to Phase 1: these enumerate the pre-DR-9 key set and stay true in Phase 0 only because `ExemptedSlots` is always 0 — `Tests~/Runtime/VoxrCommandRecogniserInjectionTests.cs:3068` (comment), `Runtime/Commands/VoxrCommandParser.cs:2389` (runtime warning string), `Runtime/Commands/VoxrCommandParser.cs:1226` (comment). Phase 1 must revisit them.

## Runtime claims

- Criterion 4: `UnsafeUtility.SizeOf<MatchResult>()` returns 32 on the Editor runtime with `ExemptedSlots` in the padding byte. If it does not, stop and report; do not reorder fields.
- Criterion 1: zero behaviour change. It rests on `ExemptedSlots` being 0 for every candidate, so the key never decides, and on the snapshot writing state nothing reads.
- Criterion 4: `UnsafeUtility.SizeOf<T>()` resolves from `Unity.Collections.LowLevel.Unsafe` and compiles in the test asmdef (`allowUnsafeCode: false`) without an unsafe context.
- Criterion 1: Phase 0 adds no new compiler warning (e.g. CS0414 on `_anyExempt`, written but not read until Phase 1); if one appears, the implement gate reports it.
- Criterion 3: the new comparator tests, including the int-literal-to-`byte` argument in `Cand(..., exemptedSlots: N)`, compile and pass.
