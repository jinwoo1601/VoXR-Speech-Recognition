---
type: requirements
feature: joined-rig-parser
topic: joined-rig
status: draft
updated: 2026-10-14
sources:
  - Planning~/design-docs/joined-rig.md
  - Planning~/design-docs/joined-rig/decisions/adr-0001-file-handoff-pipe.md
  - Planning~/design-docs/joined-rig/decisions/adr-0002-phased-parser-then-recogniser.md
  - Planning~/design-docs/joined-rig/decisions/adr-0003-outcome-classes-never-netted.md
  - Planning~/design-docs/joined-rig/decisions/adr-0005-measurement-discipline.md
  - Planning~/design-docs/joined-rig/decisions/adr-0006-host-neutral-runner.md
---

# feat-joined-rig-parser — requirements

Track **Full**. Design `joined-rig` (`Planning~/design-docs/joined-rig.md`), locked at G1 2026-10-13, re-locked with Amendment A1 2026-10-13. No dependency.

## 1. User story

- **S1** — As the VoXR maintainer, I want the push-audio harness's real recognition output scored by the parser A/B rig in one joined run, so that parser changes are judged against what the recogniser actually hears rather than against hand-typed transcripts.

## 2. Feature & scope

Backlog row (`joined-rig.md` §6):

> `joined-rig-parser` | Phase 1 (adr-0002). The harness's additive result dump (adr-0001); the rig's dump reader, the outcome-class scorer with slot credit (adr-0003), the joined report, and the pinned-side kill check (rule 2, adr-0005); the sealed and recogniser-layer manifest marks and their handling, the split fixture tagged (adr-0005); the report stating the one side varied (adr-0005). **Full.** | None.

**Agreed at G0 on 2026-10-14** by the maintainer, in session.

**In**

1. The push-audio harness (`NativeBridge~/harness/`) gains an additive result dump (adr-0001): its existing output and checks are unchanged. The dump schema is settled in the architecture doc (design §7).
2. The rig's dump reader: the parser A/B rig reads the harness's dump as its input (adr-0001, adr-0002 phase 1 — the parser layer only).
3. The outcome-class scorer with slot credit (adr-0003): outcome classes are counted separately and never netted; slot credit is the fraction of expected slots matched exactly.
4. The joined report, stating the one side varied in the run (adr-0005).
5. The pinned-side kill check (rule 2, adr-0005): a run whose pinned side has changed is stopped.
6. The sealed and recogniser-layer manifest marks and their handling, and the split fixture tagged (adr-0005).
7. The architecture doc settles the items design §7 gives it: the dump schema, where the joined rig lives (today under one feature's `Planning~/` directory, with an older copy in `fidelity-miss-cost/ab-rig`), the run script, and the host each half runs on — host-neutral and runnable by Claude from its own shell (adr-0006).
8. **Amended at G0** (ruled "In scope, per design"): this feature's tuning pass, which design §7 makes the owner of every number the design leaves provisional — any threshold or class weight, and any partial-credit formula beyond "fraction of expected slots matched exactly".

**Out**

1. Phase 2 (adr-0002): staging `VoxrCommandRecogniser` outside Unity and scoring the recogniser-layer cases — feature `joined-rig-recogniser`, the next backlog row.
2. The R5 human corpus and the R4 negative corpus themselves, and their manifests' marks — the maintainer, through the R5 and R4 work (design §7).
3. Any change to recognition or parser behaviour.
4. Product docs — after G2, by the codex reconciler.
5. `release` and `lab-next-level-probes`, held by the human.
