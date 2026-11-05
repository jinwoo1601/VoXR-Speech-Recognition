# Notes for future features

Process and methodology lessons routed out of architecture docs' `## As-built deltas`, one line each, dated, with the feature that taught it.

- 2026-10-03 (resolver-slot-exemption) — Subagents may not write report files under `.scratch/` (a harness policy refuses it): briefs have the agent return its report as text, and the PM files it beside the brief from the hand-back.
- 2026-10-03 (resolver-slot-exemption) — A mutation check's byte restore is confirmed by a Python binary compare; `grep -v | cmp` in Git Bash strips CR and fails falsely, and `git checkout --` is refused to subagents by the vc-guard hook.
- 2026-10-14 (windows-native-harness) — Subagent network downloads are refused by permission policy: a plan that needs a download names it as a human step.
- 2026-10-14 (windows-native-harness) — MSBuild output through `cmake --build` carries no reliable Warning(s)/Error(s) summary line in the agents' capture: gates grep the output for warning/error counts.
- 2026-10-14 (windows-native-harness) — An architecture doc near its 40 KB body budget needs headroom for its as-built pass, whose admitted kinds cannot be routed out.
- 2026-10-14 (windows-native-harness) — The repo root is the package root, so Unity imports `memory/`: a Unity run's untracked `memory/**/*.meta` belong to new notes and are committed, never deleted as orphans; only `Tests~` leftovers are reverted.
- 2026-10-14 (windows-native-dotnet-fixtures) — Where the vc-guard hook refuses agent `git` writes, plan every canary revert or fixture restore as a PM step between `compile-check` dispatches from the start, not as a gate-agent step.
- 2026-10-14 (joined-rig-parser) — A session's working directory can drift (to `.scratch/`) mid-run, and a writer's `pwd -W` then prints the drifted path: every brief names the project root explicitly and says to write by it if `pwd -W` disagrees.
- 2026-11-04 (endpointer-profile) — Agent shells collapse `\` in paths: briefs give paths with `/` or quoted drive-letter forms.
- 2026-11-04 (endpointer-profile) — `grep -c $'\r$'` misses CRLF in Git Bash; count CR with `grep -U`. GNU `sed -i` strips CR; edit CRLF files with `sed -b -i`.
- 2026-11-04 (endpointer-profile) — Subagents are refused `git init`: a plan needing a scratch repo names it as a PM step.
- 2026-11-04 (endpointer-profile) — A review-fix commit after the as-built reconcile stales the architecture doc's cites: re-point them before the G2 preflight.
- 2026-11-05 (trees-fill) — A plan's runtime claim that the apply's dry run equals a plan-time probe fails when the shared base moves in between: re-run the dry run at apply and plan for a pack bump.

- 2026-11-06 (agc-filter) — A plan states a count the tree can give (exports, fixtures) only as read from a named command; "the committed 15" was a recalled figure and was 17.
- 2026-11-06 (agc-filter) — A plan's claim that a CMake setting reaches a dependency is checked against that dependency's own CMakeLists: abseil overrides `CMAKE_MSVC_RUNTIME_LIBRARY` unless `ABSL_MSVC_STATIC_RUNTIME` is on.
