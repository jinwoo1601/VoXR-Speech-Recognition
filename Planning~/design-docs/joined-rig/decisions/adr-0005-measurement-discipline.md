---
id: adr-0005
title: Refuse sealed cases unless --evaluate, vary one side per run, mark recogniser-layer cases, and check only the pinned side, over reporting, allowing or scoring them otherwise
status: accepted
date: 2026-10-13
topic: joined-rig
---

# adr-0005 — The measurement discipline: sealed cases, one side per run, recogniser-layer marks, the pinned-side kill check

## The fork

One record for four forks, rules 9, 10 and 11 of the model and rule 2, the fourth added at the quality-bar pass on 2026-10-13 (`Planning~/design-docs/joined-rig.md` §1), each a way a joined run could produce a number nobody should trust. The rulings fix what the rig must protect (`Planning~/research/2026-09-05-next-level/05-rulings.md`): half of the human corpus is sealed as a holdout and never used for tuning (ruling 1), and R14 and R15 are evaluated as one unit (ruling 10). Phase 1 scores the parser layer only (adr-0002), and the split fixture in `Tests~/Fixtures/audio/manifest.json` expects one command across two finals.

- **Sealed cases.** What the rig does with cases the corpus manifest seals as the holdout.
- **Attribution.** Whether one A/B run may vary the decoder side and the parser at once.
- **Split cases.** How phase 1 treats cases only the recogniser can resolve.
- **The kill check under a decoder-side A/B.** What the run's built-in kill check compares against `expectations.json` when the decoder side is the side that varies. The rule as first written compared every run's transcript layer with the pin, which blocked every decoder-side A/B, the very items the rig exists to measure.

## Options considered

- **Sealed cases:**
  - (a) Refuse them unless `--evaluate` is given.
  - (b) Report them separately.
  - (c) Not the rig's job.
- **Attribution:**
  - (a) One side per run.
  - (b) Allow both.
- **Split cases:**
  - (a) Mark them as recogniser-layer.
  - (b) Score them as misses.
  - (c) Exclude them silently.
- **The kill check under a decoder-side A/B:**
  - (a) Check the pinned side only: the run's pinned decoder configuration must reproduce the committed `expectations.json` final texts byte for byte.
  - (b) Apply the check only to parser-side runs.

## The decision

- **Sealed cases — (a).** Corpus manifests mark cases as sealed, and the rig skips them by default; it scores them only under an explicit evaluation flag, `--evaluate`, and stamps that report "holdout evaluation". Leakage becomes a deliberate act, never an accident.
- **Attribution — (a).** An A/B run varies either the decoder side (model, front end, endpointer) or the parser, with the other side pinned, and the report states which side varied. Under ruling 10, R14 and R15 together count as one decoder-side unit.
- **Split cases — (a).** Manifest cases that only the recogniser can resolve, such as the split fixture, are tagged; phase 1 reports them as "not scored at this layer", never as misses, until phase 2 scores them.
- **The kill check under a decoder-side A/B — (a).** The run's pinned decoder configuration must reproduce the committed `expectations.json` final texts byte for byte. In a decoder-side A/B that is side A; in a parser-side A/B it is the only decoder side. If it does not match, the run reports no command-level numbers. A decoder-side B's transcripts are the change being measured, so they are not checked against the pin: they are listed per ruling 10, each case with its old and new transcript, and they become the new pin only through the harness re-pin at merge, never by hand.

## Why the rejected branches lost

- **Sealed cases, (b) report separately and (c) not the rig's job** — both leave nothing stopping tuning against the holdout. What would have to change for either to win: the decision record gives the reason they lost and nothing more.
- **Attribution, (b) allow both** — a moved number could not be attributed. What would have to change for it to win: the decision record gives the reason it lost and nothing more.
- **Split cases, (b) scored as misses** — it leaves a known structural miss in every phase-1 baseline. What would have to change for it to win: the decision record gives the reason it lost and nothing more.
- **Split cases, (c) excluded silently** — it leaves phase 2 no prior baseline to diff. What would have to change for it to win: the decision record gives the reason it lost and nothing more.
- **The kill check under a decoder-side A/B, (b) apply the check only to parser-side runs** — a broken pipe on a decoder-side run would then go unnoticed. What would have to change for it to win: the decision record gives the reason it lost and nothing more.

## Consequences

- **Sealed cases.** The R5 and R4 manifests must carry the sealed mark. Spending the holdout is the operator's choice; an evaluation run uses up its freshness, so it is reserved for a decision's final check.
- **Attribution.** Measuring both sides at once is never an option; the operator chooses which side to vary, and every report names it.
- **Split cases.** The R5 and R4 manifests must carry the recogniser-layer mark. The split fixture stays out of the phase-1 miss count, and phase 2 inherits it with a prior baseline to diff.
- **The kill check under a decoder-side A/B.** A decoder-side A/B is no longer blocked by the check: only side A is checked against the pin, and side B's transcripts are listed, each case with its old and new transcript. The pin moves only through the harness re-pin at merge, never by hand.
- The sealed and recogniser-layer manifest marks and their handling are owned by this topic.

## Status

Accepted on 2026-10-13 by the human's ruling in conversation. The fourth sub-fork, the kill check under a decoder-side A/B, was added at the quality-bar pass on 2026-10-13, also by the human's ruling; the human ruled the recommended option. The design doc (`Planning~/design-docs/joined-rig.md`) was locked at G1 on 2026-10-13 and re-locked the same day with Amendment A1 (adr-0006).
