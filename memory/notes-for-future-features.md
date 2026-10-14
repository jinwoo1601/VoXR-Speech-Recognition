# Notes for future features

Process and methodology lessons routed out of architecture docs' `## As-built deltas`, one line each, dated, with the feature that taught it.

- 2026-10-03 (resolver-slot-exemption) — Subagents may not write report files under `.scratch/` (a harness policy refuses it): briefs have the agent return its report as text, and the PM files it beside the brief from the hand-back.
- 2026-10-03 (resolver-slot-exemption) — A mutation check's byte restore is confirmed by a Python binary compare; `grep -v | cmp` in Git Bash strips CR and fails falsely, and `git checkout --` is refused to subagents by the vc-guard hook.
- 2026-10-14 (windows-native-harness) — Subagent network downloads are refused by permission policy: a plan that needs a download names it as a human step.
- 2026-10-14 (windows-native-harness) — MSBuild output through `cmake --build` carries no reliable Warning(s)/Error(s) summary line in the agents' capture: gates grep the output for warning/error counts.
- 2026-10-14 (windows-native-harness) — An architecture doc near its 40 KB body budget needs headroom for its as-built pass, whose admitted kinds cannot be routed out.
- 2026-10-14 (windows-native-harness) — The repo root is the package root, so Unity imports `memory/`: a Unity run's untracked `memory/**/*.meta` belong to new notes and are committed, never deleted as orphans; only `Tests~` leftovers are reverted.
- 2026-10-14 (windows-native-dotnet-fixtures) — Where the vc-guard hook refuses agent `git` writes, plan every canary revert or fixture restore as a PM step between `compile-check` dispatches from the start, not as a gate-agent step.
