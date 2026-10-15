---
type: requirements
feature: joined-rig-parser
topic: joined-rig
status: accepted   # G2 ruled in-conversation 2026-10-15: accepted at 2c61018
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

## 3. Why — the value it delivers

This feature carries `automated-verification.md` §3 goals 1 and 2 as design rule 7 carries them (adr-0006): every decoder-, grammar- and DSP-side item the design gates — R10, R11 (phase 2), R12, R13, R14 with R15, R21, R22 and both R41 screens — becomes measurable at the command level players feel, by Claude from its own shell (design §5). Phantom fires stay visible as their own number; each change is attributable to one side; the holdout stays sealed.

The kill criterion protects all of it: no number is quoted from a run whose pinned side is off its pin (design §0).

## 4. Observable behaviour

1. The operator runs the harness over a corpus; it reports exactly as today (16/16 on the TTS fixtures) and also writes a dump.
2. The operator runs the rig on a dump (parser-side A/B: two parser refs) or on two dumps (decoder-side A/B: one parser ref), naming the side that varies.
3. The run first checks the pinned side's finals against `expectations.json`; on any difference it stops, names the case, and reports no command-level numbers.
4. Otherwise the report names the corpus, its provenance and the side varied; gives per-case outcome class and slot credit, with wrong-intent and false-fire counts separate; lists recogniser-layer cases as "not scored at this layer" and skips sealed cases (unless `--evaluate`, which stamps "holdout evaluation"); and lists every flipped case with old and new transcript and outcome.

## 5. Functional requirements

**Ruled on 2026-10-14** by the maintainer, in session: a negative that correctly does not fire is counted correct and shown "correct (no fire)", with no sixth class and no design re-lock (fork 1); `Runtime/Testing/VoxrBatchTestRunner.cs`'s exact-equality check is untouched (fork 2); this feature's tuning pass (§2 In 8) delivers a record of every number the rig uses, or none (fork 3); the rest "Settled as stated".

| #  | Requirement | Story | Priority | Acceptance check | Source |
|----|-------------|-------|----------|------------------|--------|
| F1 | The harness writes an additive dump of every decoder result — finals and partials, words with `conf` and timings, audio-time stamps; its verdict and existing output are unchanged. | S1 | Must | Verification Command 3 green, 16/16; a dump from that run holds partials and timestamps. | adr-0001; adr-0002 |
| F2 | The kill check: the pinned side's final texts reproduce the committed `expectations.json` byte for byte, or the run reports no command-level numbers. | S1 | Must | Passes on the 16 fixtures; a deliberately altered final makes the run refuse, naming the case. | design rule 2; adr-0005 |
| F3 | Parser-side A/B: one dump, two parser git refs, no re-decode; each final goes to `Parse(text, words)` — words passed, not text only. | S1 | Must | A `main`-vs-`main` run reports 0 flips. | adr-0001; adr-0002 |
| F4 | Decoder-side A/B: two dumps, one parser ref; only side A is kill-checked; side B's transcripts are listed per case, old and new, never checked against the pin. | S1 | Must | A run with a side-B dump carrying a changed final lists it and is not refused. | design rule 2; adr-0005 |
| F5 | Each scored case lands in exactly one outcome class — correct fire; right intent, wrong slot(s); wrong intent; missed (no fire); false fire on a negative — with slot credit = the fraction of expected slots matched exactly. A negative that correctly does not fire is counted correct and shown "correct (no fire)" (fork 1). Wrong-intent fires and false fires are always separate counts, never netted against hits. | S1 | Must | A parser ref seeded with a deliberate regression shows the case in its class; no count nets another. | adr-0003 |
| F6 | The report names its corpus and the corpus's provenance and the side varied, and lists every flipped case with old and new transcript and outcome. | S1 | Must | Read on the F5 canary run. | design rule 10; adr-0004; adr-0005 |
| F7 | Cases a manifest marks sealed are skipped by default; `--evaluate` scores them and stamps the report "holdout evaluation". | S1 | Must | A test manifest with one sealed case, run with and without `--evaluate`. | design rule 9; adr-0005 |
| F8 | Cases a manifest marks recogniser-layer are reported "not scored at this layer", never as misses; the split fixture in `Tests~/Fixtures/audio/manifest.json` is tagged. | S1 | Must | The split case sits in that row of the 16-fixture report. | design rule 11; adr-0005 |
| F9 | Runnable by Claude from its own Windows-native shell, with no Unity, no device and no WSL. | S1 | Must | Every G2 run above is made that way. | design rule 7; adr-0006; `windows-native-toolchain.md` rule 1 |
| F10 | The tuning pass ends with a record in the architecture doc of every number the rig uses, or "none", each marked provisional with its evidence; any single combined score, if offered, is shown beside the class counts, never instead (fork 3). | S1 | Must | The record is present at G2. | design §7; adr-0003 |
| F11 | The existing grammar-pin check stays the sync guard, and the rig's existing DocCheck run procedure still exits 0. | S1 | Should | Both run at G2. | design rule 8 |

## 6. Non-functional requirements

- **Performance:** an on-demand instrument (adr-0004); no budget beyond F3's no re-decode.
- **Robustness at the input boundary:** a malformed dump line, a dump case absent from the manifest, a manifest case absent from the dump, an unknown mark, each stops the run with a named error — never a silent score.
- **Degraded dependencies:** a parser ref that fails to stage or compile, or a missing dump, stops the run with a named error and no numbers.

Row cut as not applicable: accessibility.

## 7. Non-goals / deferred

- Phase 2, the recogniser layer — `joined-rig-recogniser`.
- Any decoder, DSP, grammar or parser behaviour change.
- Building the R5 or R4 corpus.
- Any change to the verification binding (adr-0004).
- `Runtime/Testing/VoxrBatchTestRunner.cs` — untouched, superseded as the measurement by the rig's scorer (fork 2).
- The C# Editor pipeline and its AGC divergence.
- Unifying the four hand-synced grammar copies.
- Ranking decoders on TTS-only audio (critique C12).
- Re-pinning by hand, ever — the pin moves only through the harness re-pin at merge.

## 8. Dependencies & assumptions

**Depends on:** the Windows-native toolchain, merged (PR #175, #176): Command 3's Windows-native harness and the recipe's DocCheck run procedure.

**Assumption:** the operator is the maintainer or Claude.

**Assumption:** the 16 TTS fixtures and `expectations.json` are the corpus and pin at G2.

**Assumption:** a TTS-only report cannot rank decoders (C12), which its provenance line makes visible.

## 9. Acceptance criteria (G2)

- [x] F1: Verification Command 3 green, 16/16; a dump from that run holds partials and timestamps. (Phase 4 and review-fix gates: harness 16/16, exit 0; the dump holds partials and timestamps)
- [x] F2: the kill check passes on the 16 fixtures; a deliberately altered final makes the run refuse, naming the case. (Phase 4: an altered-final dump copy exits 3 naming the cases)
- [x] F3: a `main`-vs-`main` parser-side run reports 0 flips. (Phase 4 and review-fix gates: `main` vs `main`, Flips 0, byte-equal set and unset)
- [x] F4: a run with a side-B dump carrying a changed final lists it and is not refused. (Phase 4: a side-B dump with changed finals listed, not refused)
- [x] F5: a parser ref seeded with a deliberate regression shows the case in its class; no count nets another. (Phase 3 canary: 14 cases to `missed`; review F4 scratch run, ruled 2026-10-15: one case each in `false-fire`, `wrong-intent`, `wrong-slots`; the Phase 4 re-made canary not run, ruled 2026-10-15)
- [x] F6: the F5 canary run's report names its corpus, the corpus's provenance and the side varied, and lists every flipped case with old and new transcript and outcome. (Phase 3 canary report: corpus, provenance, side varied, every flip with both transcripts and outcomes)
- [x] F7: a test manifest with one sealed case, run without `--evaluate`, skips it; run with `--evaluate`, scores it and stamps the report "holdout evaluation". (Phase 4: sealed case skipped; with `--evaluate` scored and stamped `HOLDOUT EVALUATION`)
- [x] F8: the split case sits in the "not scored at this layer" row of the 16-fixture report. (Phase 4: the split case in the not-scored row)
- [x] F9: every G2 run above is made from Claude's own Windows-native shell, with no Unity, no device and no WSL. (every run from Git Bash, `MSYS_NO_PATHCONV` set and unset)
- [x] F10: the tuning-pass record of every number the rig uses, or "none", is present in the architecture doc. (architecture doc *Tuning record*)
- [x] Human-only verification: the maintainer reads the F5 canary report and confirms each flip is legible with its old and new transcript and outcome. (confirmed by the maintainer at G2, 2026-10-15)
- [x] The commands the project's `verification` binding names for the paths changed are green (Command 3 at least). (review-fix gate: Command 3 both halves, DocCheck 66/66, EditMode 203/203, PlayMode 704/704)

**Ruled at G2 on 2026-10-15** by the maintainer, in conversation: accepted at head `2c61018`, with the preflight in view, the canary report's flips confirmed legible — the Phase 4 as-built entry added after the first preflight, and the Phase 4 re-made canary not run by the maintainer's ruling.

## 10. Open questions

Left to the architecture doc:

1. **The dump schema.**
2. **Where the joined rig lives** — today `Planning~/features/coverage-in-selection/ab-rig/`, an older copy in `fidelity-miss-cost/ab-rig`.
3. **The run script.**
4. **The host of each half** — Windows-native in practice (F9).
5. **Where a corpus's provenance comes from.**
6. **How phase 1 scores a case not marked recogniser-layer that yields two or more non-empty finals.**
