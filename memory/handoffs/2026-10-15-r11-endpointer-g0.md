You're picking up: open design topic R11, the endpointer profile via `conf/model.conf`, at G0. Read the files below, then start at Next steps.

## Objective

Open R11 as a design topic through `g0-open`: agree its scope with the human, cut its `design-` branch, and record the scope. Done for this brief when G0 is recorded. The design doc itself is the next step, not this one.

## Lane

design

## Context

- `main` is at `3daaf5d`, after the PR #178 merge (`feat-joined-rig-recogniser`, G2 2026-10-15). R1, the joined rig, is complete: #177 merged the parser half and #178 the recogniser half.
- The shortlist after rulings, `Planning~/research/2026-09-05-next-level/05-rulings.md` :114-126, item 6: "R11 endpointer profile via `model.conf` — GO after 3 and 4". Item 3, R9 (cache version key), is merged; item 4, R1, is merged now.
- The maintainer picked R11 as the next task on 2026-10-15, over R5 (recorder scene) and the Phase B lab probes.
- STATUS `memory/STATUS.md` `## Current`: the Phase D list starts with R11 endpointer.
- `release` is held by the human (RSE-1 first when it resumes).

## Key files & locations

- R11's case:
  - `Planning~/research/2026-09-05-next-level/03-critique-and-ranking.md`: the ranking row at :185, the rationale at :232, and R10 (lattice width) as the co-tenant behind it at :253.
  - The binary evidence that `model.conf` is read: same file, :122.
  - R9 as a hard prerequisite: same file, :52.
- Rulings: `Planning~/research/2026-09-05-next-level/05-rulings.md`. The standing rule "re-pin, never edit" covers acoustic baselines via the harness (rulings 2 and 10).
- The instrument R11 will be measured with:
  - Design `Planning~/design-docs/joined-rig.md`.
  - The recogniser layer, `Planning~/features/joined-rig-recogniser/architecture.md` (its *Timing* measures: finals count, end-of-audio→fire delay).
- Product facts: CHANGELOG `[Unreleased]` › Changed, "A model directory must now contain `conf/model.conf`" (#145). `KNOWN_LIMITATIONS.md` :434 (a decoder-value edit in `conf/model.conf`).
- Process docs: design docs at `Planning~/design-docs/<topic>.md` (`.claude/bindings/core.md` `## process-docs`).

## Constraints & decisions

- Process docs before any code. On the design track, nothing is written before the branch exists (G0), and G1 is the human's.
- Windows-native only; never write WSL into anything.
- `memory/` commits go straight to `main` through `hub.py commit`.
- Compiler and test output goes to `compile-check`, never the main thread.
- Agents never run git writes.
- The report filer misses background agents' reports. After each dispatch, check `.scratch/`, and file by hand from the agent's `SubagentHandback` tool input in `~/.claude/projects/D--Workspace-VoXR-Speech-Recognition/<session>/subagents/agent-<id>.jsonl`.

## Next steps

1. Invoke `g0-open` for design topic R11, the endpointer profile. A suggested slug is `endpointer-profile`, giving branch `design-endpointer-profile`; the human may rename it at SCOPE.

## Open questions

1. Scope: R11 alone, or R11 with R10 (lattice width) as its co-tenant in the same file? The ranking defers R10 to directly behind R11, swept one knob at a time.
2. Two human-only items from the last feature are open in STATUS `## Deferred`, JRR-1 (Quest on-device check) and JRR-2 (16-fixture report read). They do not gate R11's G0. Mention them only if the human brings them up.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - design-endpointer-profile; lane: design; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 646f85ca-4e4c-4a60-845e-bc6b25c0a4ba". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
