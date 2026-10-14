# Phase 4 — persisted plan (2026-10-15)

**Phase 4 - tuning pass and closing gate**, unsplit (no units, every step serial): F9, F10, F11 at G2, §2 In 8, §10 item 2, and the F1-F8 runs re-made as F9 requires. `ab-rig/` = `Planning~/features/coverage-in-selection/ab-rig/`; JS `JoinedScorer.cs`, J `Joined.cs`, VCP `Runtime/Commands/VoxrCommandParser.cs`, VCR `Runtime/Commands/VoxrCommandRecogniser.cs`. † = approved departure from the row.

**Ruling - the tuning pass records, it does not re-pick** (the maintainer, at plan approval, 2026-10-15): `MinScore` 0.6 and `MinConfidence` 0.4 stay (they mirror VCR :31, :27; F10 "each marked provisional"; *Fire gate* *Why*), evidenced by their citations re-verified, the 16-fixture class counts at the gate and a `.scratch/`-only sensitivity probe; no tracked code change.

## Files to change

| Path | Change | Why |
|---|---|---|
| `Planning~/features/fidelity-miss-cost/ab-rig/` (tracked: `Check.cs`, `Program.cs`, `UnityStub.cs`, `ablate.py`, `stage.sh`; ignored: `Rig.csproj`) | delete, by the PM | *Home*; §10 item 2 |
| `Planning~/features/joined-rig-parser/architecture.md` | `## Build plan` link line; the two *Evidence* cells and the closing sentence (criterion 14) | F10 |
| `Planning~/features/joined-rig-parser/plan-4.md` | create | persisted plan |
| `Runtime/Commands/VoxrCommandParser.cs` | canary edit (10), PM-reverted (11), never committed | F5 re-made |

## Contracts and interfaces

No seam changes: everything is as `plan-3.md` *Contracts* built it.

**Conventions.** Every run in Claude's Git Bash at the project root (F9); JSON copies, edits and compares by `python3 -I`, LF kept. `sj/` = `.scratch/joined/`, from the project root; `J.sh` = `ab-rig/joined.sh`, run as *Set and unset* says; L, R, U as plan-3 names them.

**Set and unset.** Each `J.sh` run is made twice: to `<out>-set` as `MSYS_NO_PATHCONV=1 bash ab-rig/joined.sh …`, to `<out>-unset` as `env -u MSYS_NO_PATHCONV bash ab-rig/joined.sh …`, so neither rests on the dispatch shell's environment. Same exit code; `cmp` finds each written `scored-a.jsonl`, `scored-b.jsonl`, `report.md` byte-equal; a refusing pair writes none.

**Probe line** (9), per `<v>` of `ms0.5`, `ms0.7` (`MinScore`), `mc0.3`, `mc0.5` (`MinConfidence`):
1. `env -u MSYS_NO_PATHCONV bash` on `ab-rig/stage.sh main sj/p4-tune-<v>`.
2. `python3 -I` replaces `MinScore = 0.6f` (JS :23) or `MinConfidence = 0.4f` (JS :24) in the staged `JoinedScorer.cs`: exactly one replacement, else stop.
3. In the stage, `dotnet build -v q --nologo`, then side a's `score` line as `joined.sh` :152-153 runs it, with `--side a --vary parser --pinned`: `--dump` `sj/p4.jsonl`, `--manifest`, `--pin` the defaults (:104-105), `--out sj/p4-tune-<v>/scored-a.jsonl`, all drive-letter (`cygpath -am`). Exit 0.

**Probe record**, per value, to the dated note: the header's `gate` (J :243-244 write JS's constants), class counts, and each case whose `class` differs from `sj/p4-f3-set/scored-a.jsonl`'s, or "none".

**Evidence cell** (14): "Tuning pass: pending." becomes the citation re-verified at this phase's HEAD (VCR :31 or :27; VCP `DefaultMinScore` :181; `ab-rig/report.py` :23, `ab-rig/ablate.py` :28), the 16-fixture counts at the gate (6), each probe's class moves or "none", and "provisional (TTS-only, C12)". The record's closing sentence gains one clause: the parser runs at its own defaults (e.g. `VoxrCommandParser.DefaultCoverageWeight`, `ab-rig/Program.cs` :317), staged with each ref — parser configuration, not a rig number. Net growth at most about 700 bytes.

**Human stops:** harness exit 1 (never a re-pin); the kill check failing on unaltered `p4.jsonl`; a set-vs-unset difference; a sweep hit; a false runtime claim; CMake refusing a stale `NativeBridge~/build` cache (the PM moves it to `.scratch/`, never deletes it); the architecture body measured over 40,960 bytes after the record pass (14) or the as-built pass (route-out per the `## As-built deltas` charter, never a silent trim).

**Commit set:** the five removals, `architecture.md`, `plan-4.md`, any `memory/**/*.meta`, the memory notes; nothing under `.scratch/` or `Runtime/`. No CHANGELOG line (dev tooling).

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| Record 0.6, 0.4 | Re-pick on 16 fixtures | Ruling; F10; C12; design §3 overfitting |
| Probe in a stage | Flag; tracked edit | No tracked change; *Fire gate* rejects a flag |
| One notch, side a | Grid; both sides | Ruling; `main` vs `main` sides score alike |
| Canary re-made | Keep `p3-canary` | F9: every G2 run made that way; the row's "every acceptance run re-made" carries F5 |
| Sweep verbatim | Changed-file grep | Its pathspec covers every changed path outside the feature docs and `memory/` (criterion 5; claim h) |

## Acceptance criteria

Order: 1 PM, before any dispatch; 2-8 `compile-check` dispatch A; 9-10 one `code-writer` dispatch B; 11 PM; 12-13 `compile-check` dispatch C; 14 `architect-reconcile`, record pass.

1. [ ] `git rm -r Planning~/features/fidelity-miss-cost/ab-rig`; delete the ignored `Rig.csproj` and the empty directory; `ls` finds nothing there. (§10 item 2; claim d)
2. [ ] Command 3 verbatim, both halves: arm64 per the recipe's `### Building NativeBridge`, then the bound desktop line, its harness stdout to `sj/p4-cmd3.stdout`: no build errors; 16/16, exit 0. (F1; claim e)
3. [ ] From `NativeBridge~`, the built harness with Command 3's arguments plus `--dump ../.scratch/joined/p4.jsonl`, stdout to `sj/p4-dump.stdout`: exit 0; `cmp` finds it equal to `sj/p4-cmd3.stdout`; the dump holds `partial` payloads and `pushed`, `prevPushed` on every `result`. (F1; claim b)
4. [ ] DocCheck per the recipe, verbatim: exit 0, no `FAIL`, `<n>/<n>` equal. (F11)
5. [ ] *The sweep (F7)* (`windows-native-dotnet-fixtures/architecture.md`), verbatim: empty, exit 1. Its pathspec covers every changed path outside the feature docs and `memory/` (session records, history the predecessor's F7 excludes): the row's "new and edited files"; checked by listing `git diff --name-status main..HEAD` against it. (F9; claim h)
6. [ ] `J.sh --vary parser --dump sj/p4.jsonl --ref-a main --ref-b main --out sj/p4-f3`, set and unset: exit 0; guard and kill check passed (F11, F2); `Flips: 0` (F3); split case not scored (F8); 15 `scored` per side; class counts recorded; else as plan-3 criterion 2. (claim a)
7. [ ] B copy `sj/p4-dump-b.jsonl` from `p4.jsonl` by plan-3 criterion 3's four edits, U's read off `p4.jsonl` itself, never plan-3's seq 9 or `results` 13: U's final found by case, later `seq` +1, `case-end` `results` = `p4.jsonl`'s own + 1; set and unset: `--vary decoder --dump-a sj/p4.jsonl --dump-b <copy> --ref main --out sj/p4-f4` as plan-3 criterion 3 (F4); 6's line with `--dump <copy> --out sj/p4-f2`: exit 3 naming L, R, U, no scored file (F2). (claims a, g)
8. [ ] Manifest copy `sj/p4-sealed.json`, L sealed; 6's line plus `--manifest <copy>`, set and unset, as plan-3 criterion 4: `--out sj/p4-sealed`; `--evaluate --out sj/p4-evaluate`. (F7; claims a, g)
9. [ ] † Tuning probe (approved with the plan, 2026-10-15) per *Probe line*: each value builds, exits 0, its `gate` shows it; *Probe record* written. (F10, §2 In 8; claims c, i)
10. [ ] Canary: VCP `HasUnfilledRequiredSlot` (:4539) returns `true`. 6's line with `--ref-b WORKTREE --out sj/p4-canary`, set and unset, as plan-3 criterion 6. `sj/p4-canary-set/report.md` is kept for the maintainer's F6 read at G2, superseding `p3-canary`. (F5, F6; claims a, f)
11. [ ] PM: `git checkout -- Runtime/Commands/VoxrCommandParser.cs`.
12. [ ] `git status --short -- Runtime/` empty; Commands 1-2 green as the binding says, `Tests~` reverted, `memory/**/*.meta` kept. (NFRs)
13. [ ] `git status --short`: only commit-set paths and the pre-existing untracked `.claude/references/vc-git/__pycache__/`.
14. [ ] Both *Evidence* cells and the closing sentence's clause per *Evidence cell*; probe detail in the dated note. (F10)

## Open questions that block implementation

None.

## Blast radius

- Removal: history docs citing the old rig stay (*Home*); no recipe, binding or `stage.sh` names it; the sweep excludes it.
- `NativeBridge~/build`, `build-windows/` rebuilt in place.
- Between 10 and 11 the worktree VCP is seeded: no DocCheck, `WORKTREE` stage or Unity run then, in any session.
- `sj/p4-*` generated, never committed.
- Commands 1-2 take the host's Editor lock, write `memory/**/*.meta`.
- Architecture body (38,627 bytes by `wc -c` at plan time, link line in): Tuning-record growth at most about 700 bytes (14), the Phase 4 as-built entry at most about 1,000 bytes, so the body stays within 40,960, else a human stop.
- As-built pass: the register's stale refs; `ab-rig/ablate.py` :28 cites VCR :30 (live :31).
- `joined-rig-recogniser` inherits the provisional gate.

## Runtime claims

- a (6-8, 10): `J.sh` outputs are byte-equal set vs unset; no out-dir or stage path enters them.
- b (3): `sj/p4-dump.stdout` is byte-equal to `sj/p4-cmd3.stdout`.
- c (9): a staged constant edit compiles and `score` applies it; a `score` run outside `joined.sh` takes drive-letter arguments.
- d (1): `git rm -r` removes exactly the five tracked files, leaving the ignored `Rig.csproj`.
- e (2): the arm64 configure succeeds on the existing `NativeBridge~/build`, or refuses on its stale cache (the stop).
- f (10): the canary moves classes as plan-3's claims e and i say.
- g (7, 8): `p4.jsonl`'s final texts equal `p2.jsonl`'s (the verdict fixes them) and U holds exactly one non-empty final, so plan-3's edits and outcomes carry; the edits keep LF and change exactly what is claimed.
- h (5): every path `git diff --name-status main..HEAD` lists outside the feature docs and `memory/` falls under the sweep's pathspec; `git grep` there prints nothing and exits 1.
- i (9): a staged `0.5f`/`0.7f`/`0.3f` constant is written into the scored header's `gate` as `0.5`/`0.7`/`0.3` by `Utf8JsonWriter.WriteNumber(float)`, so the `gate` proves the edit took effect.
