---
type: requirements
feature: joined-rig-recogniser
topic: joined-rig
status: draft
updated: 2026-10-15
sources:
  - Planning~/design-docs/joined-rig.md
  - Planning~/design-docs/joined-rig/decisions/adr-0001-file-handoff-pipe.md
  - Planning~/design-docs/joined-rig/decisions/adr-0002-phased-parser-then-recogniser.md
  - Planning~/design-docs/joined-rig/decisions/adr-0003-outcome-classes-never-netted.md
  - Planning~/design-docs/joined-rig/decisions/adr-0004-on-demand-instrument.md
  - Planning~/design-docs/joined-rig/decisions/adr-0005-measurement-discipline.md
  - Planning~/design-docs/joined-rig/decisions/adr-0006-host-neutral-runner.md
---

# feat-joined-rig-recogniser — requirements

Track **Full**. Design `joined-rig` (`Planning~/design-docs/joined-rig.md`), locked at G1 2026-10-13, re-locked with Amendment A1 2026-10-13. Depends on `joined-rig-parser`, accepted at G2 2026-10-15, merged as PR #177.

## 1. User story

- **S1** — As the VoXR maintainer, I want the joined rig to drive the real `VoxrCommandRecogniser` on audio time from the harness dump and score the recogniser-layer cases, so that recogniser-side changes — R11 (endpointer) and R17 (partials) first — can be judged at the command level players feel, by Claude from its own shell.

## 2. Feature & scope

Backlog row (`joined-rig.md` §6):

> `joined-rig-recogniser` | Phase 2 (adr-0002). Stage `VoxrCommandRecogniser` outside Unity, driven on audio time from the dump's partials and timestamps, and score the recogniser-layer cases. Unblocks R11 and R17. **Full.** | `joined-rig-parser`

**Agreed at G0 on 2026-10-15** by the maintainer, in session.

**In**

1. `VoxrCommandRecogniser` staged outside Unity from its shipped source; how it stubs `MonoBehaviour` and `Time` is settled in the architecture doc (design §7).
2. The staged recogniser driven on audio time from the dump's partials and timestamps (adr-0002).
3. The recogniser-layer cases scored with phase 1's outcome classes and slot credit, kill check and report discipline (adr-0003, adr-0005); the split fixture leaves "not scored at this layer" for a real score.
4. Runnable by Claude from its own Windows-native shell, with no Unity, no device and no WSL (adr-0006).
5. Deferral JRP-1 (ruled option (a)): exact partial timing and an exact per-case word-time origin in the dump — a bridge change under `NativeBridge~/src/`, its mechanism settled in the architecture doc, with Command 3's arm64 build and a `.so` on an ABI change.

**Out**

1. Any change to recognition, parser or recogniser behaviour.
2. Judging R11 and R17 themselves — this feature makes them judgeable by the rig.
3. The R5 human corpus and the R4 negative corpus themselves, and their manifests' marks — the maintainer, through the R5 and R4 work (design §7).
4. Product docs — after G2, by the codex reconciler.
5. `release` and `lab-next-level-probes`, held by the human.
