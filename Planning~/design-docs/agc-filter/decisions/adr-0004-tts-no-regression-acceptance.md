---
id: adr-0004
title: With no pilot, accept on the TTS joined-rig A/B as a no-regression gate and on the two defects verified directly, over holding G2 for the pilot
status: accepted
date: 2026-11-05
topic: agc-filter
---

# adr-0004 — The TTS A/B as a no-regression gate when there is no pilot

## The fork

The acceptance gate agreed at G0 is "R1 plus the pilot" (`Planning~/design-docs/agc-filter.md` §0, Acceptance gate; `05-rulings.md` :102). The pilot, R5's human check of 2 speakers × 16 phrases, has not started (`05-rulings.md` :9, :100). No TTS-only comparison ranks two decoder-side configurations (critique C12; `Planning~/design-docs/joined-rig.md` :118), and the joined rig's corpus is 16 TTS fixtures (`Planning~/design-docs/joined-rig.md` :77). Ruling 10 measures the unit through the pilot "if it exists by then" (`05-rulings.md` :100). What G2 rests on when the pilot does not exist has to be settled before the requirements can name the evidence, and the kill criteria are read against it (`Planning~/design-docs/agc-filter.md` §1, rules 12 and 13).

## Options considered

1. **(a) With no pilot, the TTS A/B is a no-regression gate, and the defects are verified directly.** The 16-fixture TTS joined-rig A/B gates on no regression — no unexplained pass→fail flips, no rise in false or wrong-intent fires — and does not rank. The case for the unit rests on the two measured defects being verified fixed directly: the filter's response is measured, and synthetic silence-then-burst signals show the gain does not rise in pauses. The pilot ranks the unit whenever it exists.
2. **(b) G2 waits for the pilot.** The unit is ranked on human speech before it merges.

## The decision

**Option (a): with no pilot, the 16-fixture TTS joined-rig A/B is a no-regression gate, not a ranking, and the case rests on the two measured defects being verified fixed directly; the pilot ranks the unit whenever it exists.** It commits the design to two kinds of direct evidence — the filter's measured response, and synthetic silence-then-burst signals on which the gain does not rise in pauses — beside the joined rig's no-regression gate (`Planning~/design-docs/agc-filter.md` §1, rules 12 and 13).

## Why the rejected branches lost

- **(b) G2 waits for the pilot** — it blocks the unit on an unscheduled, human-only session. What would have to change for it to win: it wins if the pilot exists by G2 — it is then the gate as ruling 10 wrote it.

## Consequences

- The TTS A/B does not claim an accuracy gain; it can only stop the unit. A pass→fail flip without a named explanation blocks merge (ruling 10, `05-rulings.md` :98), and every fixture whose outcome changes is listed with its old and new transcript (`Planning~/design-docs/joined-rig.md` :111).
- The direct evidence has to be built: a measurement of the filter's response, and synthetic silence-then-burst signals for the gain.
- The first-word-retention kill criterion reads silence-then-command fixtures, which do not exist; today's fixtures carry 0.3 s of lead silence (`Tests~/Fixtures/generate.py:28-29`). The requirements name them.
- Acoustic baselines are still re-pinned by running the harness, never hand-edited (`05-rulings.md` :98).
- The ranking stays owed: when the pilot exists, it ranks the unit.
- The kill criteria are read accordingly, by the human's ruling of 2026-11-05 (`Planning~/design-docs/agc-filter.md` §1, rule 12): a rise in wrong-intent or false fires, or a fall in first-word retention on silence-then-command fixtures, kills the unit on every corpus; correct fires that do not improve kill it on the pilot (human speech) whenever the pilot exists; before the pilot exists, the TTS gate requires correct fires not to fall, and does not ask them to improve.

## Status

Accepted on 2026-11-05 by the human's (the maintainer's) ruling in conversation. The design doc is `Planning~/design-docs/agc-filter.md`; it was locked at G1 on 2026-11-05.
