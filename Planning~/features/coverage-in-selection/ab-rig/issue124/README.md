# issue #124 measurement apparatus

Saved from the 2026-08-27 analysis session so the numbers on
https://github.com/jinwoo1601/VoXR-Speech-Recognition/issues/124#issuecomment-5432896895
can be re-run without rebuilding them.

## What each file is

- `Verify.cs`  — the reporter's 2-intent grammar, plus the benign suffix-shadow
                 mitigation (`designate_track`), registered first and last to prove
                 the shadow wins on SCORE, not registration order.
- `Verify2.cs` — the reporter's own 2026-08-19 field grammar shape, where
                 `query_time_to_target` carries a second pattern `time to target track {track}`
                 ("claim your own tail"). Reproduces the field logs exactly.
- `Verify3.cs` — the A/B for the BAR. Runs both arms off one binary via the
                 `VoxrCommandParser.BarLeadingMissFromFiring` static below.

## How to run

    ./stage.sh WORKTREE <outdir>
    cp issue124/Verify*.cs <outdir>/
    # dispatch each from Program.Main on --verify / --verify2 / --verify3
    # (do NOT use -p:StartupObject= — it does not survive an incremental rebuild)
    cd <outdir> && dotnet build -v q --nologo && dotnet run --no-build -- --verify3

## The parser patch (REFUSE-TO-FIRE variant — see caveat)

Applied to the STAGED copy of `VoxrCommandParser.cs`, never to the repo:

1. `internal struct MatchResult` gains `public bool LeadingRequiredMissed;`
2. In `TryMatchScored`, alongside `matchedRequired` / `missedRequired`, add
   `bool sawFirstRequired` + `bool leadingRequiredMissed`; latch at every
   `matchedRequired++` (→ false) and `missedRequired++` (→ true), first one wins.
   Populate the field on the `return new MatchResult { ... }`.
3. Add `internal static bool BarLeadingMissFromFiring = false;` beside `UnkToken`.
4. In `ParseInternal`, track `bool bestLeadingMissed` beside the other `best*`
   locals, assign it where `bestConsumedEndIdx` is assigned, and immediately
   before `_resultBuf[_resultCount++] = ...` insert:

       if (BarLeadingMissFromFiring && bestLeadingMissed)
       {
           if (bestEndIdx <= searchStart) break;   // must make progress
           searchStart = bestEndIdx;
           continue;
       }

**CAVEAT — this is the refuse-to-FIRE variant.** It suppresses the round winner
AFTER selection has picked it. The ruling (2026-08-27) is REFUSE-TO-COMPETE:
exclude leading-missed candidates from selection so a legitimate rival can win
the round instead. The corpus numbers in the issue comment were produced by the
variant above and MUST be re-measured for the selection variant.

## Corpus A/B (refuse-to-fire variant, 699 rows)

`corpus-bar-off.tsv` / `corpus-bar-on.tsv` — the default Program replay over
`../phase7-corpus.tsv` column 2. Filtered to score >= 0.60, 48 of 699 rows change:
concat 29, delete 9, prepend 5, append 4, baseline 1.

---

## 2026-08-27 addendum — the refuse-to-COMPETE re-measurement (design-leading-miss-bar)

The caveat above is discharged. `Verify4.cs` replaces `Verify3.cs`: one binary, five arms,
selected by `VoxrCommandParser.BarMode` and `--bar=N` on every mode including the default
corpus replay.

| `BarMode` | arm |
|---|---|
| 0 | off |
| 4 | refuse-to-FIRE (suppress the round winner after selection; it still consumes) |
| 1 | refuse-to-COMPETE, uniform (bar on any first required element) |
| 2 | refuse-to-COMPETE, literal-only (bar only when that element is a literal) |
| 3 | refuse-to-COMPETE + the leading-coverage excusal |

### The patch (refuse-to-COMPETE)

Far smaller than the refuse-to-fire recipe above — no `ParseInternal` change at all:

1. `MatchResult` gains `LeadingRequiredMissed` + `LeadingRequiredIsSlot`.
2. `TryMatchScored` latches `firstRequiredKind` (0 none / 1 literal / 2 slot) and
   `leadingRequiredMissed` at the four `matchedRequired++` / `missedRequired++` sites.
3. `CompareCandidate` returns `Worse` for a barred candidate, immediately after the
   DR-7 admission rule.

Arm 3 additionally clamps the leading term:
`lead = min(SkippedBefore(searchStart, startIdx), _orphanRun[searchStart])`.

### Validation performed before any delta was trusted

- `--grammar` is byte-identical to the committed `grammar` pin in
  `NativeBridge~/harness/expectations.json` (133 words).
- `--bar=0` is byte-identical to `corpus-bar-off.tsv`, so the patch is inert when off —
  re-checked after the arm-3 and arm-4 patches landed.
- `--bar=4` is byte-identical to `corpus-bar-on.tsv`, so this build independently
  reproduces the earlier session's refuse-to-fire numbers.

### Results, 699 rows, gated at `minScore` 0.60

| arm | rows changed | fire nothing | score drops | clean 1.0000 commands destroyed | a rival can win the round |
|---|---|---|---|---|---|
| refuse-to-FIRE | 48 | 17 | 0 | **0** | no |
| refuse-to-COMPETE | 62 | 28 | 14 | **11** | yes |
| refuse-to-COMPETE + lead excusal | 89 | 17 | 41 | **0** | yes |

`analyse-arms.py` and `compare-arms.py` regenerate these from the TSVs.

**Arms 1 and 2 are byte-identical on this corpus** — it contains no pattern whose first
required element is a slot, so it cannot discriminate the slot-leading fork.
`Verify4.cs`'s `slot` grammar does.
