You're picking up: open row 2 `windows-native-dotnet-fixtures` of the locked `windows-native-toolchain` design at G0. Read the files below, then start at Next steps.

## Objective

Agree the G0 scope of feature `windows-native-dotnet-fixtures` with the human and cut `feat-windows-native-dotnet-fixtures` from an up-to-date `main`, via `g0-open`. Done = the human's agreed scope recorded and the branch cut (or the human declines/holds it).

## Lane

feature

## Context

- Design `windows-native-toolchain` G1 LOCKED 2026-10-13; backlog `Planning~/design-docs/windows-native-toolchain.md` `## 6. Feature backlog` (row at :146), quoted: "`windows-native-dotnet-fixtures` | Command 4 (the `Samples~` compile) and the parser A/B rig's DocCheck run procedure on native dotnet with Windows-form paths; the host-neutral `Tests~/Fixtures/generate.sh` (adr-0005); the verification binding's prerequisites, Shell and Path-form lines; the recipe's remaining WSL sections. Closes with the sweep for `wsl` and `/mnt/` over the live areas that closes deferral WSL-1 (adr-0001). **Full.** | depends on `windows-native-harness`".
- Its prerequisite row 1 `feat-windows-native-harness` is accepted (G2 2026-10-14) and merged: PR #175, `d07de12`; close-out `aa42aa7`. `main` is the dev branch; one checkout (`trees` none); no board — `memory/STATUS.md` is the state.
- Still held by the human (2026-10-13): `feat-joined-rig-parser`, `release`, `lab-next-level-probes` — do not pick them up.

## Current state

- Nothing for row 2 exists yet: no branch, no docs.
- Deferrals touching it in `memory/STATUS.md` `## Deferred`: **WSL-1** (this row closes it), **WNH-1** (`NativeBridge~/include/vosk_api.h:3` names only `libvosk.so` — a light-lane candidate, not this row unless the human folds it in at G0).

## Key files & locations

- Design + ADRs: `Planning~/design-docs/windows-native-toolchain.md`, `Planning~/design-docs/windows-native-toolchain/decisions/adr-0001…adr-0005`.
- Row 1's docs (precedent, Full track): `Planning~/features/windows-native-harness/{requirements,architecture,plan-1,plan-2,plan-3}.md`.
- The sites row 2 owns: `.claude/bindings/unity.md` `## verification` (Command 4, Prerequisites, Shell, Path form); `Planning~/verification-recipe.md` (its WSL sections; `### Building NativeBridge` is already Git Bash form); `Tests~/Fixtures/generate.sh`; the DocCheck run procedure (Claude's auto-memory entry "DocCheck run procedure", outside the repo, says it stages in Git Bash and builds in WSL).

## Constraints & decisions

- G0 before any doc or code; the scope is the human's to agree.
- Lessons from row 1 (`memory/notes-for-future-features.md`, 2026-10-14 lines): subagent network downloads are refused — a plan needing one names a human `!` step; Unity runs generate `memory/**/*.meta` for new notes — commit them, never delete as orphans; keep the architecture doc under its 40 KB body budget with headroom for the as-built pass.

## Next steps

1. Invoke `g0-open` for `windows-native-dotnet-fixtures` (from the locked backlog row above).

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-windows-native-dotnet-fixtures; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor fc69259b-a2d0-4064-93d0-65e9f23a2dbd". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; send to the one on this machine. If the bare name is refused as ambiguous, use the ref `ListAgents` shows for the local one.)
