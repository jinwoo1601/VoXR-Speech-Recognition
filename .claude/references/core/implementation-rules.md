# Implementation rules

One page for every writing agent. The brief says what; these say how.

1. **Surgical changes.** Touch only what the plan names. Match the existing style, even where you would do it differently. Do not improve adjacent code, comments, or formatting.
2. **No speculative code.** No features beyond the plan, no abstractions for single-use code, no configurability nobody asked for, no error handling for impossible cases.
3. **Match existing patterns.** Reuse what the codebase already has before writing anything new; name the pattern you followed in your per-file line. A new code file opens with the header block of `.claude/references/core/coding-conventions.md`.
4. **Clean up only your own orphans.** Remove the imports, variables, and functions that *your* change made unused. Leave pre-existing dead code alone: mention it, do not delete it.
5. **Tests first where a test harness is bound.** If the verification binding names a test runner, write or extend the test before the code it covers, and run the bound command before returning.
6. **Refactor only inside the plan's scope.** A refactor the plan did not ask for is a plan defect to report, not a change to make.
7. **Never bypass verification.** No `--no-verify`, no skipped hooks, no edited expected output, no commented-out failing test. A failing check is reported, not silenced.

If a rule and the plan conflict, one of them is wrong: stop and report `BLOCKED`.
