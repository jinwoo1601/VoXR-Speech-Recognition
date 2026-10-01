---
type: measurement
feature: coverage-in-selection
phase: 0
status: complete
updated: 2026-08-14
---

# Phase 0 — Baseline and corpus census

Discharges requirements **F15** ("the corpus is verified to contain the phenomenon before any A/B delta is treated as evidence") and captures the pre-change baseline for **F14**.

## Rig validation — PASS

The A/B rig copied from `Planning~/features/fidelity-miss-cost/ab-rig/` was re-validated against the committed grammar pin before being trusted, per project memory `grammar-ab-rig`.

- Staged real parser sources from `main` (`f182c78`) via `stage.sh main`, built under `dotnet 10.0.110` with the `UnityEngine.Debug` stub. Build clean, 0 warnings.
- `abrig --grammar` output vs `NativeBridge~/harness/expectations.json` → `grammar`: **133 entries both sides, identical.**

The rig reproduces committed behaviour, so deltas it reports are trustworthy.

## Census — the committed corpus cannot exercise this feature

The 16 committed harness transcripts, replayed through the baseline parser:

| Result | Count |
|---|---|
| Exact match, score `1.000000`, one result | **14 of 16** |
| `close distance safe range` → `set_distance_named:0.400` | 1 |
| `target hotel one` → `approach_target:0.667` | 1 |

**Leading skips: 0. Trailing orphans: 0.** Every one of the 16 begins its match at the first recognised token and consumes through to the end of the utterance. The one transcript with `[unk]` (`[unk] launch three torpedoes target alpha three`) carries it *leading*, where it is free on both the old rule and the new one.

The two sub-`1.0` cases are fidelity misses, not coverage misses:

- `close distance safe range` matches `close distance {range} target {target}` and strands the trailing `target` literal and `{target}` slot. Nothing is left over in the *utterance* — the shortfall is in the pattern.
- `target hotel one` matches `approach target {target}` from index 0 with `approach` missed. Again no leftover tokens.

**Consequence for Phase 7:** the A/B over the committed corpus will be identically zero by construction. It remains a valid **regression check** — a non-zero delta would mean something out of scope moved — but it is **not evidence about this change**. This is the same finding Amendment A1 recorded for item 1 (design §0/A1.4: "the committed harness corpus cannot exercise item 1 at all").

## Second-order finding — the inherited ablation strategy does not reach the trailing term either

Item 1's `ablate.py` generates single-word **deletions** from the committed transcripts. A deletion shortens the utterance, so the winning pattern simply misses an element — it does not leave tokens unexplained. Deletion can only produce a trailing orphan **indirectly**: when dropping a word causes a *shorter sibling pattern* to win, stranding the rest of the utterance. That is precisely symptom 2, so the strategy is not useless — but it only fires where a bare/short sibling exists to win.

Leading skips need in-grammar tokens **before** the match; trailing orphans need in-grammar tokens **after** it that start no pattern. Neither is reachable by deletion alone from a corpus of exact matches.

**Phase 7 therefore needs a corpus-generation strategy the previous feature did not have** — insertion and concatenation of in-grammar tokens around a real transcript, alongside the inherited ablation pass. Recorded here so Phase 7 does not inherit a method that cannot see what it is measuring.

## Third-order finding — the #42 pair is already pinned as a test, asserting the current (wrong) behaviour

**Corrected 2026-08-14 after plan validation.** My first reading of this — "the #42 pair is not in the demo grammar, so F7 needs a purpose-built fixture" — was right about `DemoGrammar.cs` and wrong about the repo. The pair exists as an **inline test grammar**.

`Tests~/Runtime/VoxrCommandParserTests.cs:1584` `HazardSplitAcrossTwoIntents_Warns` registers exactly `decelerate` and `decelerate_by` = `["decelerate","by","{burn_level}"]` over `BurnSlots()`, parses `"decelerate hard burn"`, and asserts:

```csharp
Assert.AreEqual("decelerate", result.Command.Intent,
    "the bare intent wins and the spoken burn level is stranded");
Assert.IsFalse(result.Command.HasSlot("burn_level"));
```

That is symptom 2, pinned as current behaviour. **It inverts under this feature** — bare `1/(1+2)` = `0.333` loses to slot-filled `2/3` = `0.667` — so both assertions fail at the wiring phase.

This is better than a new fixture: F9 wants the symptom reproduced as a failing test *before* the change, and the pin already exists and already passes on `main`. F7's work is to **invert it with a re-derived, argued expectation** (design §7.3 discipline — argued as an improvement, never mechanically updated), not to author a fresh grammar.

`Tests~/Runtime/VoxrCommandParserTests.cs:1613` `BareFormReachableOnlyByOmittingAnOptional_Warns` inverts for the same reason.

Both tests also carry `LogAssert.Expect(Warning, "is a bare form of")` and close with `LogAssert.NoUnexpectedReceived()`, which ties them to the construction-time `WarnOnDroppableRequiredLiteral` detector (`VoxrCommandParser.cs:365`) — a warning whose stated rationale ("*no penalty tuning can: P scores a clean 1.0 and nothing normalized to 1.0 can beat it*") this feature falsifies. That site is now owned by the plan.

## Baseline artifacts

- `<scratchpad>/baseline-corpus.tsv` — the 16-row replay above, at `main` `f182c78`.
- Unity baseline to beat: EditMode 116/116, PlayMode 373/373 (`Planning~/verification-runs/fe33f33/`).
