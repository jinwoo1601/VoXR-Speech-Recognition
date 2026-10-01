# Design: Resolver-Filled Slots and the Score Gate

**Branch:** `design-resolver-slot-scoring`
**Origin:** issue #161, raised from #148's requirements §6 E-2 (measurement 2026-09-16).
**Status:** In design — opened at G0 2026-10-01; not locked.

**Scope, agreed at G0 (2026-10-01):** decide how the scoring model charges a required slot that a registered slot resolver (#148) can fill, choosing among the issue's four options; settle option 4's open questions (when the exemption applies, its interaction with the leading-required-miss bar, its effect on the sibling-tie reachability scan and the eager gate); say why the chosen shape beats the existing slot-less sibling-pattern workaround; and amend `Planning~/design-docs/scoring-model.md` (LOCKED) on this branch, re-locked by the human at G1.

> **What's decided going in.** The human (the maintainer) ruled at G0 on 2026-10-01: (1) the work runs on the **design track**, branch `design-resolver-slot-scoring` — the light lane is impossible, because every option amends the LOCKED `scoring-model.md`; (2) the design **proceeds from the grammar measurement** without waiting for a human speech corpus, and real speakers' elision shapes are recorded as **unmeasured**; (3) option 1, ship as built, is the option taken for #148 by the maintainer's ruling of 2026-09-16 and **stands until G1**.
>
> **Non-goals:** reopening the leading-required-miss bar, acoustics, the confidence gate, and a per-command `minScore` (see §0).

---

## 0. Scope (agreed at G0)

**In**

1. Decide how a required slot that a registered slot resolver (#148) can fill is charged by the scoring model, choosing among the issue's four options:
   - Option 1 — ship as built: the option taken for #148 by the maintainer's ruling of 2026-09-16, which stands until G1.
   - Option 2 — credit a resolver-filled slot as matched (`"launch missiles"` → 0.75); the issue does not recommend it, citing ruling 3, "I don't want commands firing that were not of user's intention".
   - Option 3 — waive the omission penalty without crediting a match (→ 0.50, still under the gate).
   - Option 4 — exempt a resolver-fillable slot from the denominator (`"launch missiles"` → 0.667); the issue's strongest candidate.
2. Option 4's open questions:
   - whether the exemption applies only when a resolver is actually registered for that slot, which would make the score configuration-dependent, which no score currently is;
   - its interaction with the leading-required-miss bar when the exempted slot is the anchor — does the bar still refuse first;
   - its effect on the sibling-tie reachability scan and the eager gate, both of which read the same global `minScore`.
3. The design must say why it beats the existing workaround: a slot-less sibling pattern (`intercept track {track}` beside `intercept track {track} {burn_level}`) matching the elided utterance at 1.000.
4. Amend `Planning~/design-docs/scoring-model.md` (LOCKED, re-locked at G1 2026-08-14 with Amendment A3) on this branch, re-locked by the human at G1. Another locked doc is amended only where the design names the exact site and the human's G1 covers it.

**Out**

- Reopening the leading-required-miss bar (`Planning~/design-docs/leading-miss-bar.md`): ruling 3 is absolute, and it bars 0/40 occurrences in the measured grammar.
- Acoustics: whether VOSK emits the elided text at all.
- The confidence gate.
- A per-command `minScore`.

**Evidence basis**

- The issue's measurement on `VR FTL-Like 3` `Set_Combat` at `minScore` 0.6: the resolver is reachable for 18/40 pattern×required-slot occurrences dropping the slot alone, and 5/40 in the natural spoken form (slot and its orphaned literal dropped).
- Stated limit: this split is a property of the grammar, not of observed speech. Real speakers' elision shapes are unmeasured, and a human corpus could change the conclusion.
