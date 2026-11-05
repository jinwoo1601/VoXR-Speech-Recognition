# Phase 2 — persisted plan (2026-11-05)

Scope: Phase 2 as the row reads it — `doc-writer` rewrites `.claude/bindings/unity.md` :5 Run from (*Run from*) and :14 Tree-safe (*Tree-safe*) by the register's texts, then the PM reads both lines and runs `validate` and `doctor` against the comparison ground already captured (S0) — taking on and dropping nothing; :8 and :12's "one checkout" phrases and every other line stay (*Run from*, :123).

## Files to change

| Path | Change | Why |
|---|---|---|
| `.claude/bindings/unity.md` | modify (`doc-writer`, S1): :5 and :14 only, each replaced in place; 17 lines, CRLF 17/17 before and after | F3 (A3), *Run from*, *Tree-safe* |

## Contracts and interfaces

**The two lines, before and after.** Each is one whole line; every other byte of the file is unchanged, the Interview comment :4 and the "one checkout" phrases of :8 and :12 included. On :5 the clause before "which the host project" changes, and the host clause "which the host project ... consumes by `file:` path" becomes "; the host project ... consumes the session's tree by `file:` path", so the host consumes the session's tree (Command 1's bracket points the host's `file:` at the tree, :6); the compile-only-through-host and recipe clauses stay word for word.

- :5 before: ``- Run from: the project root `D:\Workspace\VoXR-Speech-Recognition`, the one checkout, which the host project `D:\Workspace\VoXR TestGround` (Unity 6000.4.7f1) consumes by `file:` path — this bare UPM package compiles and tests only through that host; the full procedure, the toolchain paths and the Samples~ csproj are in `Planning~/verification-recipe.md` ``
- :5 after: ``- Run from: the root of the tree the session runs in, the hub `D:\Workspace\VoXR-Speech-Recognition` or a tree in the trees directory `D:\Workspace\VoXR-Speech-Recognition.trees`; the host project `D:\Workspace\VoXR TestGround` (Unity 6000.4.7f1) consumes the session's tree by `file:` path — this bare UPM package compiles and tests only through that host; the full procedure, the toolchain paths and the Samples~ csproj are in `Planning~/verification-recipe.md` ``
- :14 before: ``- Tree-safe: no — the host consumes this checkout by path, and a batchmode run takes the host's Editor lock ``
- :14 after: ``- Tree-safe: yes — Command 1's bracket, `py "Planning~/host-run/host_run.py"`, takes the host lock, reads the host at rest and restores the host in a `finally`; Command 3 never runs in a writer tree; Command 4 runs without the lock ``

The double backticks and the space before the closing pair are this plan's quoting only; neither is in the line.

**Steps.**
- S0 PM, already done, before S1: the comparison ground is captured. Baseline at `ce46a45`, standing for `bd6f1b3` (plan-1 S0): `.scratch/trees-fill-baseline-validate.txt` and `.scratch/trees-fill-baseline-doctor.txt`. After Phase 1, at `b5b2e79`: `.scratch/trees-fill-p1-validate.txt` and `.scratch/trees-fill-p1-doctor.txt`. Each run exits 1. The after-Phase-1 files differ from the baseline files only in the V23 rows of `close-tree/SKILL.md` and `handoff/SKILL.md`, whose messages gained `session-launch changed` (Phase 1's allowed class; validate :5, :11, doctor :21, :27). At baseline 16 skill files already carry a V23 row "verification changed" and a DRIFT row. At PLAN, after `b5b2e79` and before this plan's ruled fixes, the PM measured the two process docs this phase adds to: `plan-2.md` 8355 of 10240 bytes (`harness.py budget`: `WITHIN`), and `architecture.md` 25595 bytes, under the 40 KB budget; neither is the largest named in either BUDGET row (`.scratch/trees-fill-p1-doctor.txt` :41-42), so both rows are expected unchanged.
- S1 `doc-writer`: the two lines, by in-place replacement; nothing else; CRLF kept; runs nothing.
- S2 PM, the reads: line 5 equals the :5 after text as a fixed string, whole line, byte for byte; line 14 equals the :14 after text the same way (F3, A3). A fixed-string count of `one checkout` over the file gives 2 lines, :8 and :12. `git diff -- .claude/bindings/unity.md` touches :5 and :14 only; the CR count by a `py` byte count equals the line count, 17.
- S3 PM, from the project root in Git Bash: `py ../harness/scripts/harness.py validate --root "D:/Workspace/VoXR-Speech-Recognition"`, then `py ../harness/scripts/harness.py doctor --root "D:/Workspace/VoXR-Speech-Recognition"`; each output compared row by row with its after-Phase-1 file, the baseline as Phase 1 left it (`## Build plan`). Green: each exits 1, as at baseline, and no row in either output is new against its baseline file, other than a V23 or DRIFT row, or a change to its message, for a file carrying a copy of the `verification` block — the 16 files whose baseline V23 rows read `verification changed`, the 12 holding a :5 or :14 copy among them (code-mapper Q5).
- No `verification` Command runs: a binding-only change fires neither the Command 3 trigger nor the Command 4 trigger (`.claude/bindings/unity.md` :8-9).

**Stops**, never worked around: S2's diff beyond the two lines, a CR count off, or a read not equal to its after text — stop with the evidence, no hand repair. S3 any other row new against its baseline file, or an exit other than 1 — stop for the human; `.claude/skills/` is never edited by hand (Phase 4's).

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| :5 names the hub's and the trees directory's paths | "the hub or a tree in the trees directory", no paths | the hub path is the old line's own and :6's; the trees directory is the *Location line* entry's; `harness.py` reads no Run from wording (code-mapper Q6) |
| :14's reason is the register's three items only | the harness's "read-only" reason, or the H1-H6 list | *Tree-safe* fixes the three; this project's Commands are not read-only |
| One `doc-writer` dispatch, serial | one per line, or a unit split | one file, two lines |
| Spliced copies left stale | hand-edit the 12 copies | adr-0005 :23 and the layout binding: `.claude/**` is generated, recomposed by `upgrade` in Phase 4 (*Composed copies*) |
| `doc-writer` executes | `code-writer` | binding prose is its work (`## Build plan`) |

## Acceptance criteria

1. [ ] :5 reads the after text: the root of the tree the session runs in, the hub or a tree in the trees directory; the host consumes the session's tree by `file:` path; the compile-only-through-host and recipe clauses unchanged (F3; A3).
2. [ ] :14 reads the after text: value `yes`, its reason Command 1's bracket with the host lock, the at-rest read and the restore, Command 3 never in a writer tree, Command 4 without the lock (F3; A3).
3. [ ] The `unity.md` diff is :5 and :14 only; :8 and :12 still say "one checkout"; CRLF 17/17 (the row; *Run from* :123).
4. [ ] `validate` and `doctor` each exit 1, as at baseline, and show no row new against their after-Phase-1 files, other than a V23 or DRIFT row, or a change to its message, for one of the 16 files carrying a `verification` copy (§6; `## Build plan`).
5. [ ] No `verification` Command runs, and no agent commits (§6; CONSTITUTION).

## Open questions that block implementation

None.

## Blast radius

- The 12 spliced copies of :5 or :14 (code-mapper Q5): `implement/SKILL.md`, `implement/references/split-path.md`, `tackle-issue/SKILL.md`, `g2-lite/SKILL.md`, `g2-accept/SKILL.md`, `g1-lock/SKILL.md`, `review-cycle/SKILL.md`, `review-pr/SKILL.md`, `handoff/SKILL.md`, `release/SKILL.md`, `docs-review/SKILL.md`, `g2-lite/references/tree-path.md` — stale until Phase 4 (V23, already present).
- Readers of Tree-safe: `implement/references/split-path.md` :11 and :45, parked while no WorktreeCreate hook is registered (:7); `.claude/references/core/code-writer-brief.md` :54, :56; `.claude/agents/code-writer.md` :16. They read the spliced copies' `no` until Phase 4.
- Reader of Run from: `.claude/agents/compile-check.md` :22, which takes a named root as that tree's root.
- `Planning~/host-run/host_run.py` and `Planning~/verification-recipe.md`: unchanged (§6).

## Runtime claims

- a (S1): the edit keeps `unity.md` CRLF, 17 lines.
- b (S3): `harness.py` reads neither slot's wording (code-mapper Q6); V23 compares the `verification` key, already `verification changed` in all 16 copies' rows, so the outputs are expected to equal the after-Phase-1 files; each still exits 1 on the baseline's own failures.
- c (S0): no binding and no `.claude/` file changed since `b5b2e79` before S1, so the after-Phase-1 files stand for the state S1 starts from.

Hazards: ordering — from Phase 2 until Phase 3 fills `## trees`, :5 names a trees directory no Create procedure cuts trees into yet, and :14's `yes` takes effect on the split path only when the park lifts; from Phase 2 until Phase 4, the spliced copies still read the old :5 and `Tree-safe: no`. Runtime — doctor's BUDGET rows count this feature's architecture doc and plan files, both within budget at PLAN (S0), so either crossing its budget before S3 brings a row new against the baseline, a stop.
