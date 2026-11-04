You're picking up: build Phase 5 (sweep and tuning pass) of `feat-endpointer-profile` through `implement`. Read the files below, then start at Next steps.

## Objective

Run `implement` for Phase 5 of `endpointer-profile` from its persisted, validated plan `Planning~/features/endpointer-profile/plan-5.md`. `code-writer` runs Steps 0-5 and `compile-check` runs the gate. Then come the after-build items: the PM proposes the tuning, the human rules, and `architect-reconcile` records the *Sweep record* and the *Tuning record*. The phase is done when both records are filled and checked in, or when a stop is BLOCKED for the human.

## Lane

feature

## Context

- Branch `feat-endpointer-profile`, in the one checkout (`trees` is none). Head `d633517` ("Plan endpointer-profile Phase 5"), pushed.
- STATUS `## Current` says Phase 5 is planned and the next step is `implement` Phase 5. Session notes: `memory/2026-10-17.md`; its last entry is "Phase 5 pickup (planned)".
- Requirements: `Planning~/features/endpointer-profile/requirements.md` (F4, §4 item 6, §10 item 1).
- Architecture doc: `Planning~/features/endpointer-profile/architecture.md`. The parts that matter are:
  - *Sweep record (F4)* at about :107-120, eight empty rows;
  - *Tuning record* at about :122-129, including the eligibility rule;
  - `## Build plan` Phase 5 and its plan-5 link line.
- Design: `Planning~/design-docs/endpointer-profile.md` (locked), with adr-0002, adr-0003 and adr-0004 beside it.
- Phase 5 writes no code. It runs the built tools only:
  - `confgen`, built at `NativeBridge~/build-confgen/bin/confgen/debug/confgen.dll`;
  - the harness, built at `NativeBridge~/build-windows/bin/vosk-bridge-harness.exe`;
  - the rig's `joined.sh`, at the recogniser layer, with `--buffer`/`--window`.

## Current state

- `plan-5.md` (10,110 B) is persisted, validated `full`, has had one fix pass, and is committed.
- The human approved departures D1-D5 in plan mode on 2026-10-17:
  - D1: `code-writer` runs the steps and writes only `.scratch/endpointer-profile-phase-5/sweep.py`; `compile-check` runs the gate.
  - D2: DocCheck runs once.
  - D3: side A is dumped fresh.
  - D4: the first KC2 STOP halts the runs. The order is 0.35 then 0.25; within each, time 0.5, time 2.0, eager 0.5, eager 2.0.
  - D5: `architect-reconcile` records both records.
- The plan's 12 runtime claims a-l go to implement's first gate.
- Nothing of Phase 5 has been run. `.scratch/endpointer-profile-phase-5/` must be absent at Step 0.
- `NativeBridge~/vendor/overlaid/rule2-0.35/` exists from Phase 4 and is re-served on confgen's cached path. `rule2-0.25/` does not exist yet.
- The pickup trail is in `.scratch/`:
  - `code-mapper-`, `doc-extractor-`, `architect-` (brief, fix) and `plan-validator-endpointer-profile-phase-5-*`;
  - `endpointer-profile-phase-5-approved-plan.md`.

## Key files & locations

- Plan: `Planning~/features/endpointer-profile/plan-5.md`. It holds the steps, the `sweep.py` contract (`conf`, `check`, `table`), the gate with four negative controls, the stop rules, After the build items 1-4, and runtime claims a-l.
- Rig: `Planning~/features/coverage-in-selection/ab-rig/{joined.sh,stage.sh,JoinedReport.cs,Joined.cs}`.
- Harness: `NativeBridge~/harness/main.cpp`, with the standing pin `NativeBridge~/harness/expectations.json`. The pin is never re-pinned in Phase 5.
- Bindings:
  - `.claude/bindings/unity.md` `## verification`;
  - `Planning~/verification-recipe.md`, where DocCheck is at :222-232;
  - `.claude/bindings/core.md` for process-docs budgets: plan 10 KB, brief 15 KB.

## Constraints & decisions

- **The tuning ruling is the human's.** The PM proposes from `table.txt`: eligible candidates, preset names, and any measured M-rung reason for a rule3/rule4 sweep. The human rules on the preset count, names and values.
  - No eligible candidate blocks the flip, pending the human.
  - A ruled rule3/rule4 follow-up is planned at its ruling, as a fix pass on plan-5 (After the build item 3).
  - rule1 and rule5 are never moved.
- **A KC2 STOP** in any report halts the runs at that point, BLOCKED for the human (plan-3 R4: the human reads it).
- **Writer choices held for G2** are not to be reopened. They are recorded in `memory/2026-10-16.md` (plan-3) and `memory/2026-10-17.md` (plan-4).
- **Owed to the as-built reconcile at the build plan's end** (not Phase 5's work):
  - B1-B4;
  - stale cites, listed in the Phase 4 and Phase 5 pickup notes;
  - `joined-rig.md`, which lives at `Planning~/design-docs/joined-rig.md`; its rule 2 wording.
- **Build facts:**
  - The agents' Bash tool collapses `\\` to `\`, so probes must be backslash-free, with Python in `.py` files run under `python3 -I`.
  - `compile-check` has no Write tool.
  - The vc-guard hook refuses VC-state changes to agents.
  - Everything runs Windows-native in Git Bash, never WSL.
  - `joined.sh` handles `MSYS_NO_PATHCONV` itself.
- **Process:**
  - Compile, test and rig output go to agents, never to the main thread.
  - File every agent's report by hand beside its brief in `.scratch/`.
  - A contradiction with the locked design reopens it on a `design-` branch; it is never patched.
- **Background session:** one named `feat-joined-rig-parser` may be listed at this project root, in state `blocked`. It is not this branch's. If it writes in this tree, stop and tell the human (one writer per tree).

## Next steps

1. Invoke `implement` for `feat-endpointer-profile` Phase 5.

## Open questions

- None known. Any stop in the plan opens one for the human.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-endpointer-profile; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 7e82f95b-dc95-4b62-b377-1a22194430c0". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
