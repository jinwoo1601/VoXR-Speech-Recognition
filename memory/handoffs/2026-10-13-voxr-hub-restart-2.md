You're picking up: the `voxr-hub` role for VoXR-Speech-Recognition, restarted after the PR #172 merge so the updated hooks load. Read the files below, then start at Next steps.

## Objective

Take over as this project's hub session (`voxr-hub`) on the core 0.59.0 harness. Orient, then start `release`, STATUS's named next action, with RSE-1 first. Done = oriented, the release version named by the human, and the `chore/release-X.Y.Z` branch cut with RSE-1 under way.

## Lane

light, class slim

The hub itself has no branch. This is the lane of the named next item, `release`, which runs on a `chore/release-X.Y.Z` branch per `.claude/bindings/light-lane.md` `## release`.

## Context

- Repo `D:\Workspace\VoXR-Speech-Recognition` (Unity package `com.jinwoo1601.voxr`), one checkout (`trees` is none). It is on `main` at `d638a9e`, the PR #172 merge, and `light-harness-upgrade-3` is deleted, local and `origin`.
- Composed packs: core 0.59.0, unity 0.14.0, vc-git 0.22.0, design-track 0.6.0, feature-track 0.19.0, light-lane 0.19.0, codex 0.7.0.
  - B122: the vc-git `.pyc` is out of the `harness.json` ledger.
  - B123: the hooks read `none` plus a reason as absent.
- `memory/STATUS.md` `## Current`:
  - `main` is at the PR #172 merge; the last release is `v2.0.0`.
  - `[Unreleased]` holds #143-#154, #146-#148, #160, #161.
  - Next: `release`, RSE-1 first; the version is the human's to name, `2.1.0` proposed.
- `memory/STATUS.md` `## Deferred`:
  - RSE-1: reword the `[Unreleased]` entries for #148 and #144, which predate #161.
  - RSE-2: the codex frontmatter contract vs `Documentation~`.
  - LHU-1: the clustered `git branch -vD` gap, with harness-hub.
- Why `2.1.0` was proposed:
  - `[Unreleased]` adds public API: `RegisterSlotResolver` / `UnregisterSlotResolver`, and `InputLevel`.
  - It also tightens model validation: `conf/model.conf` is now required. That could break someone's existing setup.
  - The project ships such changes as minors, per the binding's versioning rule.

## Key files & locations

- `memory/STATUS.md`; `memory/2026-10-13.md`. The last entries are the upgrade-3 check-in, review and G2-lite close-out, and this handoff.
- `.claude/bindings/light-lane.md` `## release`: the branch, the publish procedure, the version file and install pins, the release notes, and the evidence before and after.
- `.claude/skills/release/SKILL.md`; `CHANGELOG.md` `## [Unreleased]` (line 8); `package.json` `"version"` (now `2.0.0`).

## Constraints & decisions

- Leave `.claude/references/vc-git/__pycache__/` untracked (the human's ruling, 2026-10-13).
- `CLAUDE.md` shows a line-endings-only change in the working tree. It is outside every changeset so far; restore it only on the human's word.
- These handoff files are uncommitted on `main`: this brief, its `.meta` once Unity writes it, and the 2026-10-13 note's handoff entry. Check them in on the release branch's first memory commit, the `.meta` included; never commit on `main`.
- A `compile-check` brief must list every untracked `memory/**/*.meta` in its expected post-revert `git status`. Otherwise the revert deletes them as Unity orphans; it did so on 2026-10-13.
- Display, don't ask between declared stops. Irreversible acts (merge, delete, publish, `gh workflow run release.yml`) wait for the human's word. Claude never merges, and never hand-tags.

## Next steps

1. Invoke `orient`.
2. Ask the human for the release version (`2.1.0` proposed).
3. Invoke `release` with that version. RSE-1 (the `[Unreleased]` rewording) lands on the release branch before the CHANGELOG section moves.

## Open questions

- The release version: the human names it.
- LHU-1: is `Bash(git branch -v:*)` word-bounded, so it only matches `git branch -v <args>`? If it is a plain prefix, it also admits `git branch -vD x`, which no ask rule matches. Check the Claude Code permission docs (e.g. via `claude-code-guide`) before the human rules on closing it.
- The previous `voxr-hub` session, which wrote this brief, is the human's to close.
