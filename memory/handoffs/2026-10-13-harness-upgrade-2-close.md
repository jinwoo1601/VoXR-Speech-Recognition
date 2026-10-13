You're picking up: the `voxr-hub` role, restarted on the upgraded harness, with the applied upgrade on `light-harness-upgrade-2` to check in, review and close. Read the files below, then start at Next steps.

## Objective

As the restarted hub session (`voxr-hub`, this session's restart to load the new `settings.json` and hooks), commit the harness upgrade already applied (uncommitted) on `light-harness-upgrade-2`, then take it through `review-cycle`, a PR against `main`, and the human's G2-lite ruling. Done = PR merged by the human and the g2-lite close-out recorded.

## Lane

light, class slim

## Context

- Project root `D:\Workspace\VoXR-Speech-Recognition`, one checkout (`trees` is none). `memory/STATUS.md` `## Current` still reads `main` at the PR #170 merge; next after this branch: `release` (RSE-1 first).
- The harness-hub session asked for the upgrade (unity 0.13.0); the human ruled "apply on a new branch" on 2026-10-13. The base had moved further, so all packs but codex moved.
- Precedent: the previous upgrade, `light-harness-upgrade` / PR #170 — its run is recorded in `memory/2026-10-07.md` (both entries): managed-files commit, memory check-in with STATUS line, slim review, PR, G2-lite.

## Current state

- `light-harness-upgrade-2` cut from `main` at `0b817e2` (PR #170 merge); no commits on it yet.
- Snapshot: `47b6a0971699603dfa05c72c51692a811bd8f9f1`, `stash@{0}` "harness pre-upgrade 2026-10-13", confirmed.
- `harness.py upgrade --root .` applied: core 0.45.0 -> 0.57.0, design-track 0.5.0 -> 0.6.0, feature-track 0.18.0 -> 0.19.0, light-lane 0.18.0 -> 0.19.0, unity 0.12.0 -> 0.13.0, vc-git 0.18.0 -> 0.21.0, codex 0.7.0 unchanged. 32 files written (29 replaced incl. LOCKED `constitution-annex.md` and `settings.json`, 3 added), 5 bindings preserved, 69 unchanged; applied report body identical to the dry run.
- `validate` PASS; `doctor` PASS, only the three standing warnings (CHANGELOG line cap, `resolver-slot-exemption/plan-1.md` 10279 B, `.scratch/review-resolver-slot-exemption-brief.md` 19877 B). No bindings to fill; no binding interview needed.
- Not yet run: `bounds --class slim`, `budget`, EditMode/PlayMode, review, preflight.

## Key files & locations

- Upgraded (uncommitted): `.claude/harness.json`, `.claude/settings.json`, `.claude/agents/{architect-reconcile,doc-writer}.md`, `.claude/references/core/*` (incl. new `hub-memory-guard-hook.py`), `.claude/references/unity/{unity-cli-contract,writer-rules}.md`, `.claude/skills/**` (incl. new `system-design-doc/assets/deliverable-doc-template.md`) — `git status --short` lists them all.
- Stray: `.claude/references/vc-git/__pycache__/vc-guard-hook.cpython-312.pyc` (added by the upgrade; base packaging slip, reported to harness-hub).
- Not this branch's: `CLAUDE.md` (line endings only), `.claude/csharpier/` (untracked log/snapshots).
- `memory/2026-10-13.md` (this handoff's note), `memory/STATUS.md`.

## Constraints & decisions

- Check-in by named paths only (`vc` binding): never `git add -A`; leave out `CLAUDE.md`, `.claude/csharpier/` and the `.pyc` unless the human rules otherwise.
- Memory files carry Unity `.meta` siblings (preflight checks `meta-files-paired`); new `memory/` files get theirs when the host `D:\Workspace\VoXR TestGround` imports the package — commit them with their files.
- No CHANGELOG line: a harness upgrade changes no package surface (PR #169, #170 precedent).
- Claude never merges; the human merges the PR on GitHub.

## Next steps

1. Invoke `orient`; the previous `voxr-hub` session (`d708c72a`, which wrote this brief) is the human's to close.
2. Check in the 32 upgraded paths on `light-harness-upgrade-2`, then the memory check-in (this note, this brief, a STATUS `## Current` line for the branch in flight); push with `-u`.
3. `review-cycle` over `main..light-harness-upgrade-2`, class slim.
4. Open the PR per the `vc` binding's Merge procedure, then `g2-lite`.

## Open questions

- The `.pyc`: leave uncommitted (doctor may flag it, as the manifest likely records it) or delete — the human's call.
- Discard the `CLAUDE.md` line-ending change and the untracked `.claude/csharpier/`?
- Local merged branch `light-harness-upgrade` still exists; delete it?

