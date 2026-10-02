# Notes for future features

Process and methodology lessons routed out of architecture docs' `## As-built deltas`, one line each, dated, with the feature that taught it.

- 2026-10-03 (resolver-slot-exemption) — Subagents may not write report files under `.scratch/` (a harness policy refuses it): briefs have the agent return its report as text, and the PM files it beside the brief from the hand-back.
- 2026-10-03 (resolver-slot-exemption) — A mutation check's byte restore is confirmed by a Python binary compare; `grep -v | cmp` in Git Bash strips CR and fails falsely, and `git checkout --` is refused to subagents by the vc-guard hook.
