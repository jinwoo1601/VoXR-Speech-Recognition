---
type: requirements
feature: joined-rig-recogniser
topic: joined-rig
status: accepted   # G2 ruled in-conversation 2026-10-15: accepted at c49c5fa
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

## 3. Why — the value it delivers

This feature carries `Planning~/design-docs/automated-verification.md` §3 goals 1 and 2 (design rule 7, adr-0006) to the layer that decides what a player gets: `VoxrCommandRecogniser` owns the buffer window, eager flush, prefix hold, cooldown and pending timeout, all timed (adr-0002). The rig can then judge R11 (endpointer profile; kill criterion: rise in finals-per-utterance × buffer window vs endpointer time saved, measured in sample time — `Planning~/research/2026-09-05-next-level/03-critique-and-ranking.md:232`) and give R17 the timed partial stream it needs (`Planning~/research/2026-09-05-next-level/03-critique-and-ranking.md:238`); the split fixture gets a real score with phase 1's baseline to diff (adr-0005). Exact timing (JRP-1) makes a timed outcome attributable to one side rather than to the scheduler.

## 4. Observable behaviour

1. The operator runs the harness with `--dump`; its verdict and output are unchanged (16/16); the dump now carries each result's exact audio position and a per-case word-time origin.
2. The operator runs the rig at the recogniser layer, in phase 1's two shapes — the C# side varied (one dump, two refs) or the decoder side varied (two dumps, one ref).
3. The kill check runs as in phase 1; on a difference, no command-level numbers.
4. Per case, the staged recogniser receives the dump's finals and partials in order at their audio times, its clock following the case's audio timeline, and the commands it fires are collected.
5. The report keeps phase 1's shape (corpus, provenance, side varied, counts never netted, cases, flips) and adds: the recogniser configuration used, each fire's audio time, and per case finals count and end-of-audio→fire delay.

## 5. Functional requirements

**Ruled on 2026-10-15** by the maintainer, in session: forks A–G each at option (a) — every non-sealed case scored (A); phase 1's classes, first fire is the fire, pending-never-confirmed is missed shown "pending, timed out" (B); shipped component defaults, tick rate a provisional rig number (C); the recogniser is on the C# side with the parser, a reading of rule 10 (D); exact per run, reproducible in outcome (E); timing measures reported (F); Quest on-device check required at G2 (G); the rest settled as drafted. **Amended 2026-10-15** (PLAN ruling 1, the maintainer, in session): F1's check reads positions against the samples pushed for the case, the WAV plus the harness's padding, not the WAV alone.

| #  | Requirement | Story | Priority | Acceptance check | Source |
|----|-------------|-------|----------|------------------|--------|
| F1 | The dump gives each result's audio position as the samples the decoder had consumed when it emitted (no ring-wide bracket), and each case's word times on a per-case origin; harness verdict and existing output unchanged. | S1 | Must | Command 3 green 16/16; two `--dump` runs of one build agree on every final's word times exactly; in each dump every result's audio position lies within the samples pushed for its case (the WAV plus the harness's padding) and never decreases within a case. | JRP-1; adr-0001; adr-0002 |
| F2 | Reproducibility: two dumps of one build give the same recogniser-layer outcome for every case; per-record positions may differ between runs by at most one decoder read, and the report says so. | S1 | Must | Two `--dump` runs of one build, one ref: recogniser-layer run reports 0 flips. | adr-0005 (attribution) |
| F3 | The staged recogniser is the shipped `VoxrCommandRecogniser` source at the ref under test, its behaviour unchanged; its clock follows the case's audio time; it is ticked at a stated rate. | S1 | Must | Built from `stage.sh` of a ref; a `main`-vs-`main` C#-side run reports 0 flips. | adr-0002 |
| F4 | Finals and partials reach the recogniser in dump order at their audio times, through the paths the shipped speech recogniser delivers them on, so a ref whose recogniser consumes partials (R17) is measured with no rig change. | S1 | Must | A trace of one case lists every result delivered with its audio time, in dump order. | backlog row; adr-0002 |
| F5 | Recogniser-layer scope: every non-sealed case is scored at the recogniser layer; a case marked recogniser-layer, reported "not scored" at the parser layer, gets a real class here. | S1 | Must | The split case lands in a class on the 16 fixtures. | adr-0005 split; rule 11 |
| F6 | Classes per case: phase 1's classes and slot credit; the first fire is the case's fire, further fires are extra fires; a command that goes pending and is never confirmed counts as missed and is shown "pending, timed out". | S1 | Must | Read on the 16-fixture run; a seeded regression lands in its class. | adr-0003 |
| F7 | Configuration: the recogniser runs at its shipped component defaults, staged with each ref, and the report names every value; the tick rate is a rig number, provisional → this feature's tuning record. | S1 | Must | The report lists the values; the architecture doc's tuning record holds the tick rate. | design §7; adr-0003 |
| F8 | Timing measures: per case, finals count and end-of-audio→fire delay in audio time; shown beside the classes, never netted into them. | S1 | Must | Read on the 16-fixture run. | `Planning~/research/2026-09-05-next-level/03-critique-and-ranking.md:232` (R11 kill criterion) |
| F9 | Phase 1's discipline holds at the recogniser layer: kill check, sealed/`--evaluate`, one side varied and named, every flip listed old/new. | S1 | Must | Phase 1's F2, F7 checks re-run at the recogniser layer. | adr-0005 |
| F10 | Runnable by Claude from its own Windows-native shell: no Unity, no device, no WSL. | S1 | Must | Every G2 run made that way. | adr-0006 |
| F11 | The parser-layer run is unchanged by this feature. | S1 | Must | Phase 1's `main`-vs-`main` run: 0 flips, same report on a new-schema dump. | adr-0002 |

## 6. Non-functional requirements

- **Performance:** a run never waits out audio time in wall-clock; audio time is simulated.
- **Robustness at the input boundary:** a dump without exact timing (schema 1) is refused at the recogniser layer with a named stop (exit 2) and still read at the parser layer; phase 1's strict reading otherwise.
- **Degraded dependencies:** a pinned-side decode error is exit 3 as phase 1. The bridge change alters no recognition output (Command 3 16/16; `expectations.json` unchanged) and the shipped C# consumer keeps working; on an ABI change the arm64 `.so` is rebuilt and committed.

## 7. Non-goals / deferred

- Any change to recognition, parser or recogniser behaviour (G0 Out 1).
- R11's `model.conf` sweep and R17's contradiction-rate instrumentation (would-be `TryEagerCommit` verdicts) — those items' own work.
- The Editor's managed C# pipeline (design rule 6); real-time pacing of the harness.
- R5/R4 corpora; product docs; `release`, the lab.

## 8. Dependencies & assumptions

- `joined-rig-parser` merged (PR #177): dump, reader, scorer, report, marks.
- Assumed: the recogniser's shipped source compiles outside Unity against stand-ins for the Unity types it uses (verified by the architecture doc).
- Command 3's toolchain (MSVC, the Unity 6000.3.7f1 Android NDK/SDK) is present.

## 9. Acceptance criteria (G2)

- [x] F1: Command 3 green, 16/16; two `--dump` runs of one build agree on every final's word times exactly; in each dump every result's audio position lies within the samples pushed for its case (the WAV plus the harness's padding) and never decreases within a case. (Phase 2 and Phase 4 gates: harness 16/16; `timing` 16/16 on two dumps, largest consumed difference 0)
- [x] F2: two `--dump` runs of one build, one ref: the recogniser-layer run reports 0 flips. (Phase 4 gate A: two dumps, one ref, Flips 0)
- [x] F3: the staged recogniser built from `stage.sh` of a ref; a `main`-vs-`main` C#-side run reports 0 flips. (Phase 4 gate A: staged `main` vs `main`, Flips 0)
- [x] F4: a trace of one case lists every result delivered with its audio time, in dump order. (Phase 4 gate A: 124 dump results, 124 traced, in dump order; trace order fixed in review, `6c9f1a0`)
- [x] F5: the split case lands in a class on the 16 fixtures. (the split case lands in `missed`)
- [x] F6: the classes read on the 16-fixture run; a seeded regression lands in its class. (Phase 4 gate A: classes read; the seeded canary gives 14 flips)
- [x] F7: the report lists the recogniser configuration values; the architecture doc's tuning record holds the tick rate. (the report's configuration block; architecture doc *Tuning record*, 90 Hz)
- [x] F8: per case finals count and end-of-audio→fire delay read on the 16-fixture run, beside the classes, never netted into them. (the report's `## Timing` section, beside the classes)
- [x] F9: phase 1's F2, F7 checks re-run at the recogniser layer. (Phase 4 gate A: sealed and `--evaluate` runs re-made at the recogniser layer)
- [x] F10: every G2 run made from Claude's own Windows-native shell. (every run from Git Bash, Windows-native, both shells)
- [x] F11: phase 1's `main`-vs-`main` run: 0 flips, same report on a new-schema dump. (Phase 4 gate A: Flips 0, correct 15, on a schema-2 dump)
- [x] The `unity` binding's commands as triggered are green: Command 3 (bridge changed); Commands 1–2 if `Runtime/` changes; Command 4 if public API changes. (Command 3 16/16 both shells; Commands 1-2 EditMode 203/203, PlayMode 704/704 after the `.so` change; Command 4 not triggered)
- [ ] Human-only verification: the Quest on-device check after the bridge change. (not stated at the G2 ruling; open at acceptance)
- [ ] Human-only verification: the human reads the 16-fixture recogniser-layer report and finds the split case and flips legible. (not stated at the G2 ruling; open at acceptance)

**Ruled at G2 on 2026-10-15** by the maintainer, in conversation ("Accept"): accepted at head `c49c5fa`, with the second preflight in view — the first preflight's missing handoff `.meta` files added in `c49c5fa`; the two human-only checks not stated at the ruling and left open.

## 10. Open questions

Left to the architecture doc:

1. How the staged recogniser stands in for `MonoBehaviour`, `Time` and the Unity asset types.
2. The ABI route for the timing fields: inside the JSON payload (no signature change) or a new export.
3. The tick rate's value (tuning record).
