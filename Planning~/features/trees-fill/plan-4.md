# Phase 4 — persisted plan (2026-11-05)

Scope: Phase 4 as the row reads it — the PM runs the `upgrade` skill on this project, dry run shown whole, HARD STOP, apply, then `validate` and `doctor`; it takes on, at the row's own step-3 stop, the writes outside `.claude/skills/` and the pack bumps the dry run carries, and the route for the 16 `skipped: locally modified` copies, and drops nothing.

## Files to change

| Path | Change | Why |
|---|---|---|
| `.claude/skills/**`: the 9 `replaced:` lines of the probe, and the 16 `skipped: locally modified` ones (= `trees-fill-p3-doctor.txt` DRIFT rows :3-18) | modify (`upgrade`) | F8; *Composed copies* |
| `.claude/references/core/constitution-annex.md` | modify (`replaced (LOCKED)`) | the bump; clears V11 |
| `.claude/references/core/{delegation-contract.md,hooks.md,rm-fence-hook.py,trees-contract.md}`, `.claude/references/vc-git/vc-guard-hook.py`, `.claude/settings.json` | modify (`replaced:`) | the bump (adr-0005 :23) |
| `.claude/references/core/task-leftover-hook.py` | create (`added:`) | the bump |
| `.claude/harness.json` | modify (`manifest:`): core 0.141.0 -> 0.147.0, vc-git 0.43.0 -> 0.46.0, the ledger | clears V1, BEHIND, DRIFT |

All 34 are written by `harness.py upgrade`, none by hand; the probe's 18, plus the 16 once restored.

## Contracts and interfaces

**Commands**, from the project root in Git Bash, `<root>` = `"D:/Workspace/VoXR-Speech-Recognition"`: dry run `py ../harness/scripts/harness.py upgrade --dry-run --root <root>`; apply as SKILL.md step 6 writes it (`py ../harness/scripts/harness.py upgrade --root <root>`), `<file>` = `.scratch/upgrade-d-workspace-voxr-speech-recognition-report.txt`; VERIFY `harness.py validate`, then `harness.py doctor`, same root, saved to `.scratch/trees-fill-p4-validate.txt` and `.scratch/trees-fill-p4-doctor.txt`.

**The ruling at HARD STOP** (SKILL.md step 4, by `ruling-form.md`), one ruling, the S1 dry run shown whole beside it:
- Q1, the writes outside row step 3: each non-skill line of the dry run listed (annex `replaced (LOCKED)`, the five `replaced:` references and hooks, `settings.json`, `added: task-leftover-hook.py`, `manifest:`) with the two bumps and every base `CHANGELOG.md` entry in the span, by version and line: core 0.142.0 :25 `upgrade-acts-on-revisit`, 0.143.0 :17 `new-project-free-form`, 0.144.0 :15 `item-register`, 0.145.0 with vc-git 0.45.0 :11 `read-hook-fixes`, 0.146.0 :10 `light-bg-shells`, 0.147.0 :9 `stage-and-create`; vc-git 0.44.0 :26 `light-vc-git-revisits`, 0.46.0 :8 `light-push-allow`; named, never quoted. Options: apply all (Recommended: `upgrade` takes no same-version path off the base's current packs, and adr-0005 :23 regenerates `.claude/` by it); or stop the phase — F8 unmet, a scope change. This is the row's own stop, not a reopen.
- Q2, the 16: (a) restore, then one apply (Recommended); (b) one plain apply, then 16 applies each with one `--take-theirs <file>`.
- Q3, the snapshot: the record S2 states.

**Route (a).** After the word and the snapshot: `git checkout 37cb475~1 -- <the 16 paths>`, one command, reverting batch A's hand re-splice so disk equals the ledger (the S0 ledger check: 16 of 16). The post-restore dry run is shown whole; the S1 word covers it where it differs from S1's only in those 16 lines reading `replaced:` and the count line reading 34 — as step 7's re-run rule is covered — and any other difference stops. Step 6's compare is against this dry run.

**Snapshot.** `git stash create` prints nothing (after the S0 persist commit the tree is clean but for the untracked `.claude/references/core/__pycache__/`, which it leaves out), so the snapshot is `git rev-parse HEAD` at S2, confirmed by `git rev-parse --verify <sha>^{commit}`; `__pycache__` is not covered, and `upgrade` never reads it.

**F8's second half.** No V23 row is the tool's comparison of every spliced block with its live section. Beside it, the named read: `.claude/skills/handoff/SKILL.md`'s `vc`, `trees`, `session-launch` and `verification` blocks (between `harness:binding:start <key>` and its `end`), each line equal byte for byte to the same-label line of `vc-git.md` `## vc`, `## trees`, `core.md` `## session-launch`, `unity.md` `## verification`.

**Steps** (PM):
- S0, done: the Phase 3 ground, `.scratch/trees-fill-p3-{validate,doctor}.txt`; the probe, `.scratch/trees-fill-p4-dryrun-probe.txt`; the ledger check, `.scratch/trees-fill-p4-ledgercheck.py`, output `.scratch/trees-fill-p4-ledgercheck.txt`: each of the 16 at `37cb475~1` hashes equal to its ledger entry by `ledger_hash`, "matched 16 of 16".
- S0, before S2: the persist commit (`plan-4.md`, the link line, the dated note, STATUS) lands, so the tree is clean but for the untracked `__pycache__/`.
- S1 BINDINGS, then DRY RUN shown whole; HARD STOP, the ruling above. Nothing is written before the word.
- S2 SNAPSHOT: record, confirming read, id shown; a failed confirm stops.
- S3 (a) only: the restore, then the post-restore dry run, as above; the dry run ruled on is saved to `.scratch/trees-fill-p4-dryrun.txt`.
- S4 APPLY per step 6: one run under (a); under (b) the plain run, then per file its own dry run, word, snapshot and `--take-theirs` run, the S1 dry run and each per-file one saved to `.scratch/trees-fill-p4-dryrun.txt`. Exit 0 and an equal compare, or stop.
- S5 step 7: no trigger expected (`bindings to fill: none`, no `design docs:` or `path form:` line, Brief budget under `.scratch`); any trigger stops.
- S6 VERIFY, each file compared row by row with its Phase 3 file. Green: no V1, V11, V23, DRIFT or BEHIND row; V27's count may change; every other row as in Phase 3. Any new row — a check new in core 0.142-0.147 included — is shown and put to the human, never fixed by hand. Then the named read.
- S7 REPORT per step 9; the human restarts the session (`settings.json` changed).
- S8 Check-in by the vc Check-in procedure, one commit on `feat-trees-fill`: the written lines of `.scratch/trees-fill-p4-dryrun.txt` (every line but `unchanged`, `preserved` and `skipped`) plus `.claude/harness.json`, cross-checked against `git status --short` (the 34 paths and nothing else under `.claude/`, `__pycache__` aside); `.scratch/` ignored, `__pycache__` not added.
- No `verification` Command runs (§6 :75).

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| (a) restore, one apply | (b) 16 `--take-theirs` applies | Both end in `upgrade`'s bytes. (a) restores the ledger's own composed text, undoing batch A's hand edit, so *Composed copies* holds with one word, one snapshot and one step 6 compare over all 34; (b) costs 16 more stops, snapshots and compares, `--take-theirs` being one file per run |
| Restore after the word and S2 | before the shown dry run | step 4: nothing written before the word; step 5: the snapshot precedes writes |
| One ruling, Q1-Q3 | separate stops | row step 2's one HARD STOP |
| Apply all recommended | recompose without the bump | the CLI has no such mode |

## Acceptance criteria

1. [ ] The dry run is shown whole, and the human's word on Q1-Q3 is taken before any write (F8; row steps 1-3).
2. [ ] The snapshot id is shown and confirmed before any write (SKILL.md step 5).
3. [ ] Every apply exits 0, and its body equals the dry run ruled on (F8; step 6).
4. [ ] `validate` and `doctor` print no V1, V11, V23, DRIFT or BEHIND row (F8).
5. [ ] The named read holds for all four blocks (F8).
6. [ ] No new row against the Phase 3 files save V27's count (§6 :76; §9 :110).
7. [ ] The commit holds exactly S8's paths; no `.claude/bindings/*.md`, `memory/` or `Planning~/` path (adr-0005 :23).
8. [ ] No `verification` Command runs, and no agent commits (§6 :75; §9 :111; CONSTITUTION).

## Open questions that block implementation

None. Q1-Q3 are the HARD STOP's, planned. For the as-built reconcile: :141 says "same-version recompose" and :189 limits writes, and the run carries the two bumps.

## Blast radius

- `settings.json`'s permissions and hook registrations, the new Stop hook, `vc-guard-hook.py` and `rm-fence-hook.py`: every session of this project, after a restart.
- `constitution-annex.md` (LOCKED) and `trees-contract.md`, `delegation-contract.md`, `hooks.md`: every agent and skill that reads them.
- The 25 skill copies: every skill run after S4, `upgrade` itself included.
- The hub stays on `feat-trees-fill` (plan-3 Hazards).

## Runtime claims

- a (S1): the dry run equals the probe, `trees-fill-p4-dryrun-probe.txt`.
- b (S2): `git stash create` prints nothing on this tree.
- c (S3): the vc-guard hook admits the `git checkout` restore; settings' `permissions.ask` rule `Bash(git checkout * -- *)` prompts, a prompt the human answers, never routed around; and the restored bytes hash equal to the ledger, line endings included, so the 16 print `replaced:`.
- d (S4): the apply is not in `permissions.allow` (only `upgrade --dry-run` is) and prompts by permission mode, a prompt the human answers, never routed around; it writes what the dry run printed and records each written file's ledger hash.
- e (S6): V1, V11, V23, DRIFT and BEHIND clear, and no new check adds a row.
- f (S8): the replaced `vc-guard-hook.py` admits the check-in commands.

Hazards: ordering — between S3 and S4 the 16 copies read their pre-batch-A text, `upgrade/SKILL.md` among them (batch A changed 2 lines each, inside the verification block); a stop there leaves them so, restored by the human from the snapshot. Runtime — from S4 the replaced hook scripts run at their next call, while `settings.json`'s new registrations load only at restart; a refused command is reported, never retried in another form; `plan-4.md` over 10 KB changes the BUDGET row, a stop.
