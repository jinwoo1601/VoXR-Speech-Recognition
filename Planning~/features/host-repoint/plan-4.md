# Phase 4 — persisted plan (2026-11-05)

Scope: Phase 4 as the row reads it — timed Commands 1-2 runs by the script, R1 and R2 from a PM-made throwaway worktree, R3 from the hub, the re-import cost per repoint taken as named differences per platform and recorded in *Performance*, the wait bound set by the PM by this plan's rule, written as the script's default (:7, :21), in the binding (:6) and recipe (:14), listed at G2; the row's green in the one checkout — taking on R4, a hub run with no path change as R3's baseline, dropping nothing; mutations, Commands 3-4 and the worktree acceptance are Phase 5's.

## Files to change

| Path | Change | Why |
|---|---|---|
| `Planning~/host-run/host_run.py` | modify (`code-writer`, S8): :7, :21 only; LF kept | F9; §10 item 5 |
| `.claude/bindings/unity.md` | modify (`doc-writer`, S9): :6's parenthesis only; CRLF | the row; §10 item 5 |
| `Planning~/verification-recipe.md` | modify (`doc-writer`, S9): :14's parenthesis only; CRLF | paired with :6 |
| `Planning~/features/host-repoint/architecture.md` | `## Build plan` link line (this dispatch); :75, :98, :104 by `architect-reconcile` record (S10) | process-docs; F9; §10 items 5-6 |
| `Planning~/features/host-repoint/plan-4.md` | create | process-docs |
| `memory/2026-11-05.md` | the PM's dated note (S7) | the bound's `## Decisions` line |
| P, WT, HL `owner` and `lock` | created or rewritten (PM; the script); never committed; WT removed at S6 | evidence |
| TG, requirements, design doc, ADRs, other plans, README, `.gitattributes`, `Runtime/`, `Samples~/`, `Tests~/`, other `.claude/**` | none | the brief |

## Contracts and interfaces

**Names.** HUB = `D:/Workspace/VoXR-Speech-Recognition`; WT = `D:/Workspace/voxr-host-repoint-p4`; TG = `D:/Workspace/VoXR TestGround`; PF = TG's `Packages/manifest.json` and `Packages/packages-lock.json`; HL = HUB `.scratch/host-lock`; P = `D:/Workspace/VoXR-Speech-Recognition/.scratch/host-repoint-p4`; RES = a tree's `.scratch/test-results`; RUN = `py "Planning~/host-run/host_run.py" EditMode PlayMode`. Git Bash; paths per the binding's Path form.

**Timed run Rn from tree T** (WT or HUB), PM: `mkdir -p "P/Rn"`; TG `Temp/UnityLockfile` absent; in the background `cd "<T>" && (s=$(date +%s); RUN; echo "exit=$?"; echo "bracket=$(( $(date +%s) - s ))s") > "P/Rn/run.txt" 2>&1`, waited on to its `bracket=` line; then `cp` T's RES `editmode.xml`, `playmode.xml`, `editmode.log`, `playmode.log` to `P/Rn/`; `cmp` each PF with `P/pre`. It passes on `exit=0`, `lock wait: 0s`, both platform lines `failed` 0 `inconclusive` 0, `resolved: file:<T> (packages-lock.json)` after each, `host: at rest`, `host status: unchanged`, `result: green`.

**The measurement.** R1 from WT, the host having last run from HUB; R2 from WT, nothing between; R3 from HUB; R4 from HUB, nothing between. Per run n and platform p: W(n,p) the `<p>: … wall=<s>s` value in `run.txt`; L(n,p) the `Total: <s> seconds` value on the first line of `P/Rn/<p>.log` holding both `Asset Pipeline Refresh` and `Initiated by InitialRefreshV2`; B(n) the `bracket=` value.
- Entry cost (HUB → tree), per p: W(1,p) − W(2,p) and L(1,p) − L(2,p).
- Return cost (tree → HUB, the host's next run on the hub), per p: W(3,p) − W(4,p) and L(3,p) − L(4,p).
- Re-import cost per repoint, per p: entry + return, wall and log each; the bracket view B(1) − B(2) plus B(3) − B(4). Each difference is recorded beside its operands as it reads, zero or negative included.

**The bound rule**, fixed before any run: N = 60 × ⌈3 × max(B(1), B(2), B(3), B(4)) / 60⌉ s, so a product already a multiple of 60 is N itself.

**Edited lines**, `<N>` the value: script :7 `# Wait:     WAIT_DEFAULT <N> s, set by the Phase 4 tuning pass`, :21 `WAIT_DEFAULT = <N>`; binding :6's "(`--wait`, default 1800 s, provisional → Phase 4)" becomes "(`--wait`, default <N> s)"; recipe :14's "(default 1800, provisional → Phase 4)" becomes "(default <N>)"; nothing else on any line, line counts unchanged.

**The record**, `architect-reconcile` in record mode, passages its brief names: *Performance* (:75) — its last sentence replaced by the entry, return and per-repoint costs per platform, wall and log, the bracket view, and the bound: <N> s, the script's `--wait` default and the binding's text, set by the PM as 60 × ⌈3 × <B> / 60⌉ s, <B> s the longest measured bracket (R<k>); :104 closed, naming N and *Performance*; :98's "Phase 4 measures it." becomes "Measured in Phase 4 (*Performance*)."; no batching decision.

**Steps:**
- S0 PM, starting after `phase-pickup`'s RECORD & COMMIT (the PM's check-in of plan-4, the link line and the dated note per `.claude/bindings/vc-git.md` `## vc`), holding every other Commands 1-2 run and any TG Editor open until S12: `mkdir -p P/pre`; `git status --short -- ':!memory'` > `P/g0`; TG `git status --short` > `P/t0` (4 lines); `cp` PF to `P/pre/`; TG `Temp/UnityLockfile` absent; `git worktree list` the hub alone; HL `owner` begins `tree=D:/Workspace/VoXR-Speech-Recognition ` — the check that the host last ran from the hub, since every run since Phase 3 takes the lock and rewrites `owner`, and no other tree exists.
- S1 PM, from HUB: `git worktree add --detach "D:/Workspace/voxr-host-repoint-p4" feat-host-repoint`; WT's script `grep -c $'\r$'` equals its `wc -l` (the CRLF copy, recorded); WT `Tests~` present, `Tests`, `Tests.meta` absent. No `vendor/`.
- S2-S5 PM: timed runs R1 (WT), R2 (WT), R3 (HUB), R4 (HUB), in that order, nothing else run between.
- S6 PM, from HUB: `git worktree remove --force "D:/Workspace/voxr-host-repoint-p4"`; `git worktree list` the hub alone.
- S7 PM: reads W, L and B off `P/Rn/`, applies the rule, and writes in the dated note the measurement with its operands and a `## Decisions` line: the host-repoint wait bound <N> s = 60 × ⌈3 × <B> / 60⌉, <B> s the longest bracket (R<k>), set by the PM under the human's PLAN ruling 2026-11-05 ("PM sets it, listed at G2"); the G2 packet lists N.
- S8 `code-writer`, scope the script alone, verification `not run — the gate runs the suite`: :7 and :21.
- S9 `doc-writer`: binding :6 and recipe :14; its brief carries, as the ruling that dictates the value's source, the human's PLAN ruling (`memory/2026-11-05.md` :406, "PM sets it, listed at G2 (Recommended)"), this plan's bound rule, and S7's `## Decisions` line, from which N is read off, not chosen.
- S10 `architect-reconcile`, record mode: the record, from S7's numbers.
- S11 `compile-check`, Commands 1-2 in the one checkout: exit 0; `EditMode:` and `PlayMode:` `failed` 0 `inconclusive` 0; `resolved: file:D:/Workspace/VoXR-Speech-Recognition (packages-lock.json)` after each; `host: at rest`; `result: green`. Command 4 not run: no `Runtime/` or `Samples~/` change.
- S12 PM: TG status `diff` empty with `P/t0`; `cmp` each PF with `P/pre`; TG `Temp/UnityLockfile` absent; `git status --short -- ':!memory'` against `P/g0` adds exactly the script, the binding, the recipe and the architecture doc, drops none; `git diff` touches only the lines named; the script holds no `\r`; CRLF count equals line count in the three docs; `WAIT_DEFAULT = <N>` once. P kept to G2, never committed.

**Stops**, never worked around: an S0 or S1 read off — the human. A timed run not passing, `lock wait:` above 0, no `InitialRefreshV2` line, or a `cmp` miss — stop with `run.txt`; no later run, no edit. An exit 3 naming `rest/`, or a killed run — the recipe's step 5, the human's; WT kept while TG's manifest may name it. S8-S10 not `DONE`, or S11 red — stop, nothing committed; save S9 `PARTIAL` naming the slot, not a stop for the human: the PM makes the two substitutions at binding :6 and recipe :14 itself, an edit it does not compose (CONSTITUTION, *The PM — role and write scope*), its diff shown at G2.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| R4, a hub run with no path change, as R3's baseline (scope taken on) | Phase 3's hub run (80 s) | same session, same Library chain and the script's own overhead; Phase 3's prior state is unknown and Phase 0's 31 s is the old procedure |
| "Last ran from the hub" read off HL `owner` and the worktree list | a hub run before R1 | every run since Phase 3 takes the lock and rewrites `owner`; no extra run |
| Both `wall=` and the log's `InitialRefreshV2` `Total:` | one | `wall=` is the Unity process F9's cost sits in; `Total:` isolates the import from test time |
| Bracket wall by the PM's `date +%s` | a script line | no code beyond :7, :21; plan-0's whole-run timing by the runner |
| N = 60 × ⌈3 × the longest bracket / 60⌉ s | 1 × ; the provisional 1800 s | a waiter waits up to one bracket per run ahead: 3 lets a run queue behind the holder and two more on one machine before H2's BLOCKED shows, and a hung holder surfaces within three brackets (design §3 item 3: the bound makes starvation a visible BLOCKED) |
| The longest bracket, a repointing one | the hub's no-change run | §6 sets the bound from the re-import cost, the row from the measured run time; that bracket carries both |
| WT's CRLF script accepted | `.gitattributes` now | settled at the brief; R1's pass checks it (claim b); to the as-built reconcile |
| Differences as they read | positives only | a zero or negative difference is a finding (claim c) |

## Acceptance criteria

1. [ ] R1-R4 each pass, R1 and R2 `resolved: file:D:/Workspace/voxr-host-repoint-p4`, R3 and R4 the hub (S2-S5) (F9; §6).
2. [ ] Entry, return and per-repoint costs per platform, wall and log, and the bracket view, with operands, in the dated note and *Performance*; :104 closed (S7, S10) (F9; §10 item 6).
3. [ ] N by the rule at script :7 and :21, binding :6, recipe :14, no "provisional" there; the `## Decisions` line written (S7-S9) (F9; §10 item 5).
4. [ ] Commands 1-2 green in the one checkout by `compile-check` (S11) (the row; F8).
5. [ ] TG status equal at S0 and S12; PF `cmp`-equal to `P/pre` after each run and at S12; no TG hand write (§6).
6. [ ] Only the four files changed against `P/g0`, the script LF, the three docs CRLF; no agent git write; WT removed by the PM (S6, S12) (the row; CONSTITUTION).

## Open questions that block implementation

none

## Blast radius

- TG `Library/` and `Temp/` re-imported to WT and back; the human's next Editor open on TG.
- HUB: a `.git/worktrees/` entry (S1-S6); its RES overwritten by R3, R4 and S11; HL `owner` rewritten each run.
- Every later Commands 1-2 run waits up to N by default; contention turns BLOCKED at N, not 1800 s; Phase 5 runs at N.
- Text still naming 1800 or "provisional": plan-3, adr-0002, `architecture.md` :86 (Phase 3's row, a row record), `parallel-trees.md` :96, :167, :172 (records or design, unedited); generated `.claude/skills/*` stale.

## Runtime claims

- a (S1): `git worktree add --detach` succeeds with the branch checked out in HUB, the script checked out CRLF.
- b (S2): the CRLF script runs the same — parses, resolves its tree to WT, and R1 passes with `resolved: file:<WT>`.
- c (S3, S5): R2 and R4 see no path change — Library last imported that path in the run before, the restore between writing only PF with no Unity running — so the re-import shows in R1 and R3; whatever the numbers show is recorded.
- d (S2-S5): each log holds one `Asset Pipeline Refresh … Total: <s> seconds - Initiated by InitialRefreshV2` line, the initial refresh, as Phase 3's hub logs did.
- e (S2-S5): Git Bash redirects to a `D:/…` path, `date +%s` prints epoch seconds, and `run.txt` is complete once `bracket=` appears.
- f (S6): `git worktree remove --force` removes WT holding ignored `.scratch/`.
- g (S8, S9): Edit keeps the script LF and the docs CRLF.
- h (S11): `compile-check` runs one invocation, and an uncontended run under N prints `lock wait: 0s`.

Hazards: from S0 to S12 no other Unity run, no TG Editor and no other Commands 1-2 run — one between R1 and R2 or R3 and R4 voids the no-path-change premise, one during a run skews `lock wait:` and `bracket=`; a killed or exit-3 run from WT leaves TG naming WT, so WT is not removed before the human's restore; from S0 to S12 HUB changes only by S8-S10, or S12's `P/g0` compare misreads.
