You're picking up: the `voxr-hub` role for VoXR-Speech-Recognition, restarted after the PR #171 merge so the upgraded hooks and settings load. Read the files below, then start at Next steps.

## Objective

Take over as this project's hub session (`voxr-hub`) on the core 0.57.0 harness. Orient, then put the next item to the human: `release`, STATUS's named next action, with RSE-1 first. Done = oriented, and the next item ruled by the human and started, in this session or a launched one.

## Lane

light, class slim

The hub itself has no branch. This is the lane of the named next item, `release`, which runs on a `chore/release-X.Y.Z` branch per `.claude/bindings/light-lane.md` `## release`. If the human picks other work, that work's own lane applies.

## Context

- Repo `D:\Workspace\VoXR-Speech-Recognition` (Unity package `com.jinwoo1601.voxr`), one checkout (`trees` is none), on `main` at `c5a12ed` (the PR #171 merge).
- Composed packs: core 0.57.0, design-track 0.6.0, feature-track 0.19.0, light-lane 0.19.0, unity 0.13.0, vc-git 0.21.0, codex 0.7.0. New since the last hub: the hub-memory guard hook, and a Bash watch in the handoff guard.
- `.claude/bindings/vc-git.md` `## trees` now reads a bare `- none`, with its reason in a one-line `<!-- -->` comment. Core 0.57.0's guards read only a bare `none` as absent (review finding F1, fixed in PR #171). Keep that form.
- `memory/STATUS.md` `## Current`:
  - Release `v2.0.0`.
  - `[Unreleased]` holds #143-#154, #146-#148, #160, #161.
  - Next: `release`, RSE-1 first.
- `memory/STATUS.md` `## Deferred`:
  - RSE-1: reword the #148/#144 `[Unreleased]` entries at release.
  - RSE-2: the codex frontmatter contract vs `Documentation~`.
  - LHU-1: the clustered `git branch -vD` force-delete gap, with harness-hub.

## Key files & locations

- `memory/STATUS.md`; `memory/2026-10-13.md` (today's entries: the upgrade check-in, review, G2-lite close-out, this handoff).
- `.claude/bindings/light-lane.md` `## release`; `CHANGELOG.md` `## [Unreleased]`; `.claude/skills/release/`.

## Constraints & decisions

- Leave `.claude/references/vc-git/__pycache__/vc-guard-hook.cpython-312.pyc` untracked (the human's ruling, 2026-10-13). The manifest ledger records it, and harness-hub has the packaging slip.
- Display, don't ask between declared stops. Irreversible acts (merge, delete, publish) wait for the human's word. Claude never merges.
- These handoff files (this brief, its `.meta` once Unity writes it, and the 2026-10-13 note's handoff entry) are uncommitted on `main`. Check them in on the next branch's first memory commit; never commit on `main`.
- Reported to harness-hub, not this project's to fix:
  - the strict-`none` reading, where `harness.py _missing_slots` accepts `none —` and `_trees_absent` does not;
  - the shipped `.pyc`.

## Next steps

1. Invoke `orient`.
2. Put the next item to the human, recommending `release` with RSE-1 first. On their word, start it: launch a session per the `session-launch` binding, or run it here.

## Open questions

- The release version number: the human names it (`light-lane.md` versioning rule).
- LHU-1: is it still open after vc-git 0.21.0 narrowed `Bash(git branch:*)` to read-only forms, given that `Bash(git branch -v:*)` is still allowed? Check against the current `.claude/settings.json` before the human rules on closing it.
- The previous `voxr-hub` session (`99073bc6-aebb-4419-a644-40afab4e178f`, which wrote this brief) is the human's to close.
