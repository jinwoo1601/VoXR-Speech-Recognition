# Phase 0 — persisted plan (2026-11-05)

Scope: Phase 0's evidence probe as the row reads it — one hand bracket by a probe under `.scratch/` from a throwaway detached worktree the PM creates and removes, the lock's `version` read before, after the run and after the restore, the restore checked byte-identical and at rest, the Unity run timed as F9's first sample, then Commands 1-2 in the one checkout under today's procedure — taking on a pre-run at-rest read and a host `git status --short` compare (*At-rest read*, *Manifest edit and restore*), dropping nothing.

## Files to change

| Path | Change | Why |
|---|---|---|
| `.scratch/host-repoint-probe/probe.py` | create (`code-writer`); never committed or shipped | the row; §10 item 4; *Resolved-path evidence* |
| `.scratch/host-repoint-probe/rest/manifest.json`, `rest/packages-lock.json` | created by the run (the snapshot) | *Manifest edit and restore* |
| `.scratch/host-repoint-probe/run.txt` | created by the PM's run (stdout and exit line) | the record |
| HOST `Packages/manifest.json`, `Packages/packages-lock.json` | rewritten by the run, restored from the snapshot; net none | F3; the row |
| `Planning~/features/host-repoint/plan-0.md` | create | process-docs |
| `Planning~/features/host-repoint/architecture.md` | `## Build plan` link line | process-docs |
| `Runtime/`, `Editor/`, `Tests~`, bindings, recipe, README | none | Phases 1-3 |

## Contracts and interfaces

**Names.** HUB `D:/Workspace/VoXR-Speech-Recognition`; HOST `D:/Workspace/VoXR TestGround`; WT `D:/Workspace/voxr-host-repoint-p0`; LOCK is HOST `Packages/packages-lock.json`, its `version` the `dependencies."com.jinwoo1601.voxr".version` value; at rest is *At-rest read*'s four conditions (`architecture.md` :43); paths compare per the Path form (case-insensitive, either separator).

**Probe command.** From the HUB root in Git Bash: `py -u ".scratch/host-repoint-probe/probe.py" "D:/Workspace/voxr-host-repoint-p0"`. One argument, the worktree root in `D:/…` form; HUB, HOST, the Unity path `C:/Program Files/Unity/Hub/Editor/6000.4.7f1/Editor/Unity.exe` and the filter are constants. Files are read and written in binary; JSON is parsed only to read, never re-serialised.

**The probe's bracket, in order:**
1. Argument: absolute, no `"` or `\`, `package.json` under it, not HUB, neither path a prefix of the other; WT `Tests~` present, `Tests` and `Tests.meta` absent. A miss: exit 2, nothing written.
2. At-rest read on HOST: the manifest's `com.jinwoo1601.voxr` is `file:`+HUB; `testables` parsed equal to those of `git -C "<HOST>" show HEAD:Packages/manifest.json`; LOCK `version` is `file:`+HUB (V0); `Temp/UnityLockfile` absent. S0 = `git -C "<HOST>" status --short`. A miss: exit 2, nothing written.
3. Snapshot: both files' bytes written to `rest/`, read back and compared equal to the bytes held; a miss: exit 2, no host write.
4. Repoint: the count of `"com.jinwoo1601.voxr": "file:D:/Workspace/VoXR-Speech-Recognition"` in the manifest bytes must be 1, else exit 2 unwritten; that occurrence becomes `"com.jinwoo1601.voxr": "file:D:/Workspace/voxr-host-repoint-p0"`, every other byte, CRLF included, kept; written. All that follows sits in one `try`/`finally`.
5. WT `Tests~` renamed `Tests`; WT `.scratch/test-results/` created, any `editmode.xml` there deleted.
6. Unity, by argument list with no shell: `-runTests -batchmode -projectPath "D:\Workspace\VoXR TestGround" -testPlatform EditMode -testFilter "VoXR.Tests.Editor.AudioMetricTests.ComputeRms_EmptyBuffer_ReturnsZero" -testResults "<WT>\.scratch\test-results\editmode.xml" -logFile "<WT>\.scratch\test-results\editmode.log"`, no `-quit` (Command 1's invocation, `.claude/bindings/unity.md` :6); wall seconds from launch to exit on a monotonic clock.
7. After the exit: V1 = LOCK `version`; whether the manifest still equals step 4's bytes; the XML's presence and its `test-run` `total`, `passed`, `failed`, `inconclusive`; from the log, the count of lines naming WT, the count naming HUB, and each line holding `com.jinwoo1601.voxr`, at most 20.
8. `finally`: both snapshots written back to HOST, each read back and compared equal to step 3's bytes; then WT `Tests` renamed `Tests~`, a failure recorded; then the at-rest read again, V2 = LOCK `version`; S1 = HOST `git status --short`, compared to S0.

**Summary**, one `key: value` line each on stdout: `worktree`, `at-rest before`, `version before`, `unity exit`, `wall seconds`, `xml` (the four counts or `absent`), `version after run`, `manifest after run` (`as written` or `changed by Unity`), `log` (the two counts, then the voxr lines), `restore manifest` and `restore lock` (`identical` or `differs`), `version after restore`, `at-rest after` (`ok` or each miss), `host status` (`unchanged` or the difference), `verdict`.

**Exit codes.** 0, 1 and 4 need both files identical and at rest after. 0: V1 is `file:`+WT. 1: V1 any other value, the XML present. 2: stopped before any host write. 3: a file differs or not at rest after; HOST left as found, the snapshot in `rest/`. 4: an exception in steps 5-8 outside the restore, or `Unity did not run` (V1 not `file:`+WT, the XML absent), as `verdict` says.

**Steps.**
- S1 `code-writer`: writes `probe.py` only and runs nothing; its brief's Verification command is `none bound`, since the probe writes the host.
- S2 PM: reads HOST `Temp/UnityLockfile` absent and holds every other Commands 1-2 run until S6 ends; from HUB, `git worktree add --detach "D:/Workspace/voxr-host-repoint-p0" feat-host-repoint`.
- S3 PM: runs the probe command from HUB in the background, `> ".scratch/host-repoint-probe/run.txt" 2>&1; echo "exit=$?" >> ".scratch/host-repoint-probe/run.txt"`, and waits for the exit line.
- S4 PM: records every summary line in the dated note, the wall seconds as F9's first sample (a repoint from a host resting on HUB); reads WT's `editmode.log` as needed before S5.
- S5 PM, on exit 0, 1, 2 or 4 only: from HUB, `git worktree remove --force "D:/Workspace/voxr-host-repoint-p0"` (`--force`: WT holds untracked `.meta` and ignored `.scratch/`).
- S6 PM, on exit 0 only: dispatches `compile-check` for Commands 1-2 in the one checkout under today's procedure (`.claude/bindings/unity.md` :6-7); then reads LOCK `version` and records it.

**Green.** Exit 0 with V0 and V2 `file:`+HUB, V1 `file:`+WT, both files `identical`, `at-rest after: ok`, `host status: unchanged`; then S6 green as Commands 1-2 read: each XML written by its run, `failed="0"`, `inconclusive="0"`, no `Tests` leftovers in `git status --short`. The Unity exit and the XML counts are recorded, not green.

**Stops**, never worked around and never retried in another form:
- Exit 1, a lock left on the hub: stop for the human's ruling on *Resolved-path evidence*; S6 waits for it.
- Exit 2: stop for the human with what was found; HOST untouched.
- Exit 3, a killed probe, or `run.txt` without an exit line: stop for the human, who restores HOST from `rest/`; WT is kept, since the manifest may name it.
- Exit 4: stop for the human, never an evidence ruling; S6 waits.
- `host status` changed: reported to the human, never repaired.
- S6 red: stop for the human; the probe's record stands.

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| The PM runs the probe | `compile-check` | it runs only the binding's commands (`compile-check.md` :22); worktree runs are the PM's (`architecture.md` :99) |
| Python, binary I/O | Bash | byte-exact CRLF and parsed JSON reads, as *One bracket* chose |
| A pre-run at-rest read | the snapshot alone | *At-rest read*: a snapshot of a host not at rest is a wrong rest |
| Snapshot in `.scratch/host-repoint-probe/rest/` | `.scratch/host-lock/rest/` | the prototype claims none of Phase 3's paths |
| One occurrence replaced in bytes | JSON re-serialised | *Manifest edit and restore* |
| WT `D:/Workspace/voxr-host-repoint-p0` | a path under HUB | outside HUB; no space, `"` or `\`; neither path a prefix of the other, so the log counts separate |
| `--force` removal | a `.meta` cleanup by listing | a throwaway tree; the cleanup is Phase 3's |
| Unity's wall time | the probe's whole time | the re-import F9 measures sits in the Unity run |

## Acceptance criteria

1. [ ] `probe.py` is the only file `code-writer` writes; no shipped file changes; nothing is committed (the row).
2. [ ] `at-rest before: ok`, V0 `file:`+HUB (*At-rest read*).
3. [ ] V1 `file:`+WT after Unity exits (§10 item 4; *Resolved-path evidence*; F4).
4. [ ] Both HOST files byte-identical to the pre-run bytes after the restore (F3; §6).
5. [ ] V2 `file:`+HUB and `at-rest after: ok` (the row; F2).
6. [ ] `host status: unchanged` (§6).
7. [ ] The wall seconds recorded as F9's first sample (F9).
8. [ ] WT removed by the PM (the row).
9. [ ] Commands 1-2 green in the one checkout under today's procedure (the row; F8).

## Open questions that block implementation

None; exit 1 opens the *Resolved-path evidence* ruling.

## Blast radius

- HOST: the two `Packages/` files, net unchanged; `Library/` and `Temp/`, re-imported by Unity into WT and back to HUB at S6; the human's next Editor open.
- HUB: the `.git/worktrees/` entry (S2, S5); `.scratch/test-results/` and today's `Tests~` rename and revert at S6.
- Any session's Commands 1-2 during S3 would collide with the probe; S2 holds them.
- Phase 3: its at-rest read, restore and evidence decisions rest on this record.

## Runtime claims

- a (steps 6-7): batchmode `-runTests` resolves the repointed manifest and rewrites LOCK `version` to `file:`+WT.
- b (step 7): Unity leaves `manifest.json` as written — recorded; the restore covers either.
- c (step 6): `subprocess` with an argument list quotes `D:\Workspace\VoXR TestGround` correctly for `Unity.exe`.
- d (step 8): Unity removes `Temp/UnityLockfile` on exit.
- e (step 6): WT's tracked content compiles in HOST without `vendor/`, and the filter selects one test.
- f (S2): `git worktree add --detach` succeeds while `feat-host-repoint` is checked out in HUB.
- g (S5): `git worktree remove --force` removes a tree holding untracked and ignored files.
- h (S6): Unity re-resolves to HUB on the next run, LOCK naming HUB after.
- i (S3): `py -u` output redirected to a file is complete when the exit line appears.

Hazards: no host lock exists before Phase 3, so any Commands 1-2 run during S3 collides with the probe (S2 holds them); a killed probe leaves HOST pointed at WT, and removing WT before the human restores leaves the manifest naming a missing path.
