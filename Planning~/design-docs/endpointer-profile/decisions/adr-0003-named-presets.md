---
id: adr-0003
title: Name the profiles as presets, over raw per-rule values or presets plus a Custom option
status: accepted
date: 2026-10-16
topic: endpointer-profile
---

# adr-0003 — Named presets over raw per-rule values

## The fork

The overlay (adr-0001) rewrites the endpoint trailing-silence keys of the extracted conf, and the developer must be given something to choose. What shape a profile takes decides what the package can promise to have measured. The three rules the profile can move are rule2, rule3 and rule4 (`Planning~/design-docs/endpointer-profile.md` §1, the endpoint rules table); lowering all of them together splits pauses in the middle of commands (§3, "Lower every rule"). A game whose commands are prefixes of other commands splits under a tight rule2 (§3), so the developer needs a way back to the model's own values.

## Options considered

1. **(A) Named presets.** "Model default" (the archive's own values) plus tuned preset(s).
2. **(B) Raw rule2/rule3/rule4 floats.**
3. **(C) Presets plus a Custom option** that takes floats.

## The decision

**Option (A): a profile is a named preset — "Model default", the archive's own values, plus tuned preset(s) — never raw floats.** The count, names and values of the tuned presets are provisional → the tuning pass; each preset is a measured point (`Planning~/design-docs/endpointer-profile.md` §1, rule 5).

## Why the rejected branches lost

- **(B) Raw floats** — they ship untested combinations and invite "lower every rule". What would have to change for it to win: developers' grammars demanded values no preset covers, shown by evidence.
- **(C) Presets plus Custom** — the Custom option carries the same exposure as raw floats. What would have to change for it to win: developers' grammars demanded values no preset covers, shown by evidence.

## Consequences

- "Model default" runs the archive's own values exactly and is always available (`Planning~/design-docs/endpointer-profile.md` §2, decision 3), which is the developer's way out for a prefix-heavy grammar.
- Every preset the package ships is a point the sweep measured (adr-0004); no combination ships without a measurement behind it.
- Left open: the count, names and values of the tuned presets — provisional → the tuning pass, with the first sweep `rule2 ∈ {0.5, 0.35, 0.25}` (rank 6) and rule3 or rule4 values moved only on measured evidence.

## Status

Accepted on 2026-10-16 by the human's (the maintainer's) ruling in conversation. The design doc is `Planning~/design-docs/endpointer-profile.md`; it is not yet locked.
