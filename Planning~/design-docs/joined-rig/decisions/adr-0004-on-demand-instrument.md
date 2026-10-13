---
id: adr-0004
title: Run the joined rig as an on-demand instrument, over a standing check in the verification binding
status: accepted
date: 2026-10-13
topic: joined-rig
---

# adr-0004 — The joined rig is an on-demand instrument

## The fork

The joined rig measures what the items it gates change — R10, R11, R12, R13, R14's acceptance, R21, R22 and both R41 screens (`Planning~/research/2026-09-05-next-level/03-critique-and-ranking.md`, line 175). The existing harness (`NativeBridge~/harness/main.cpp`) already passes or fails on exact equality with `expectations.json` `expectedFinals` (`:227`) and serves the locked Tier C design (`Planning~/design-docs/automated-verification.md`, whose backlog is complete, `:264`). Whether the joined rig joins that standing check or runs only when an item asks for it decides what every gate pays and what baseline has to be kept pinned.

## Options considered

1. **(a) An on-demand instrument**, run for A/B measurement by the items it gates.
2. **(b) A standing check in the verification binding.**

## The decision

**Option (a): the joined rig is run on demand, for A/B measurement by the items it gates, and their PRs list every flip, per the re-pin rule (rulings 2 and 10).** The existing harness stays the standing Tier C check, and the verification binding is unchanged.

## Why the rejected branches lost

- **(b) A standing check** — every gate would pay for a dotnet plus harness run, and its baseline would need re-pinning whenever parser behaviour moves. What would have to change for it to win: the decision record gives the reason it lost and nothing more.

## Consequences

- The verification binding is unchanged; the harness verdict is unchanged, and the dump it gains is additive.
- Each gated item's PR lists every flipped case with its old and new transcript and outcome (ruling 10), and any pass→fail flip needs a named explanation before merge.
- The operator — whoever runs an A/B for a gated item — chooses the side to vary, the corpus and when to spend the holdout (`Planning~/design-docs/joined-rig.md` §2).
- The run script is the architecture doc's.

## Status

Accepted on 2026-10-13 by the human's ruling in conversation. The design doc (`Planning~/design-docs/joined-rig.md`) is in design and not locked.
