You're picking up: the `voxr-hub` role for VoXR-Speech-Recognition, restarted on the upgraded harness. Read the files below, then start at Next steps.

## Objective

Take over as this project's hub session (`voxr-hub`) after the harness upgrade (PR #169), orient, and put the next item to the human - `release` is STATUS's named next action (RSE-1 first). Done = oriented, the next item ruled by the human and started (in this session or a launched one).

## Lane

light, class slim

(The hub itself has no branch; this is the lane of the named next item, `release`, which runs on a `chore/release-X.Y.Z` branch per `.claude/bindings/light-lane.md` `## release`. If the human picks other work, its own lane applies.)

## Context

- Repo `D:\Workspace\VoXR-Speech-Recognition` (Unity package `com.jinwoo1601.voxr`), one checkout (`trees` is none), on `main` at `4e61ec3` (PR #169 merged). `CLAUDE.md` shows modified with no content diff (line endings only); `.claude/csharpier/` untracked (CSharpier hook artefact) - never stage it.
- This session runs the upgraded composition (core 0.42.0, unity 0.11.0, vc-git 0.17.0, light-lane 0.17.0, feature-track 0.17.0); the previous hub ran the old one. Core 0.41.0 changed hub report lines: STARTED names the predecessor; `close-tree` now runs on a FINISHED line or a STARTED line naming a predecessor (read its skill).
- `memory/STATUS.md` `## Current`: release `v2.0.0`; `[Unreleased]` holds #143-#154, #146-#148, #160, #161; next `release` (RSE-1 first). `## Deferred`: RSE-1 (reword #148/#144 `[Unreleased]` entries at release), RSE-2 (codex frontmatter contract vs `Documentation~`).
- Open issues on GitHub: none (#161 closed by PR #168).
- Sessions: `light-harness-upgrade` `93d7075a` reported FINISHED (merged `4e61ec3`) and is idle; stopping it awaits the human's word.

## Key files & locations

- `memory/STATUS.md`; `memory/2026-10-05.md` (latest entries, incl. the upgrade's close-out open issues: rm-fence false positive on a heredoc naming rm/rmdir; items for harness-hub).
- `.claude/bindings/light-lane.md` `## release`; `CHANGELOG.md` `## [Unreleased]`.

## Constraints & decisions

- Display, don't ask between declared stops; irreversible acts (merge, delete, publish) behind the human's word. Claude never merges.
- Kept branches awaiting the human's word to delete: `design-resolver-slot-scoring`, `feat-resolver-slot-exemption`, `lab-resolver-slot-scoring` (local only), `feat-slot-resolver`, `feat-slot-resolver-docs`, `light-harness-upgrade`.
- Harness-side, not this project's to fix: B65 (close-tree handed-off path under trees none), B81 (filled binding sections can silently miss new pack slots); `settings.local.json` cleanup and the F14 global rm edit come from harness-hub with the human.

## Next steps

1. Invoke `orient`.
2. Put the next item to the human (recommend `release`, RSE-1 first); on their word, start it - launch a session per the `session-launch` binding or run it here.

## Open questions

- Release version number - the human names it (`light-lane.md` versioning rule).
- Stop `93d7075a`, and delete the kept merged branches - the human's word.
