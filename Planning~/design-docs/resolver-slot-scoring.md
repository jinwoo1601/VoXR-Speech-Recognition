---
type: system
topic: resolver-slot-scoring
tags: [scoring, slot-resolver, ellipsis]
status: draft
updated: 2026-10-02
sources:
  - Planning~/design-docs/scoring-model.md (§3 goals 2 and 4, §5.1, §5.3; amended by A5, §0E)
  - Planning~/design-docs/leading-miss-bar.md
  - Planning~/features/slot-resolver/requirements.md §8 E-2
  - Planning~/design-docs/resolver-slot-scoring/decisions/adr-0001-exempt-fillable-slot-in-parser.md
  - Planning~/design-docs/resolver-slot-scoring/decisions/adr-0002-fillable-means-registered.md
  - Planning~/design-docs/resolver-slot-scoring/decisions/adr-0003-exemption-changes-arithmetic-only.md
  - Planning~/design-docs/resolver-slot-scoring/decisions/adr-0004-fewer-exempted-slots-tie-break.md
  - Planning~/design-docs/resolver-slot-scoring/decisions/adr-0005-thin-firing-accepted.md
  - Planning~/design-docs/resolver-slot-scoring/lab-2026-10-02.md
  - "Session direction 2026-10-01 (G0) and 2026-10-02 (settle)"
---

# Design: Resolver-Filled Slots and the Score Gate

**Branch:** `design-resolver-slot-scoring`
**Origin:** issue #161, raised from #148's requirements §8 E-2 (measurement 2026-09-16).
**Status:** **G1 LOCKED 2026-10-02.** Rulings in adr-0001 through adr-0005 and `scoring-model.md` §0E A5.6. This document is immutable from here; a contradiction found during implementation reopens it on a new design branch and re-locks.

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

---

This system is the scoring rule for a required slot that a registered slot resolver (#148) can fill: it sits between #148's resolver hook, which is consulted only for candidates at or above `minScore`, and the locked scoring model, which charges a missed required slot `-1` raw and `+1` den (`Planning~/design-docs/scoring-model.md` §5.1), so the short orders the hook exists for never reach it — `"launch missiles"` against `launch missiles target {track}` scores 0.250 today (`Planning~/features/slot-resolver/requirements.md` §8 E-2). It serves #148's player value ("A captain says 'launch missiles' … today the second form is the only one that fires", requirements §2) and ruling 9, ellipsis as game-state resolution of an omitted required slot (`Planning~/research/2026-09-05-next-level/05-rulings.md`). It resolves the keystone fork in adr-0001, and one constraint governs everything below: ruling 3 — the bar is absolute, and "I don't want commands firing that were not of user's intention" — together with `scoring-model.md` §3 goals 2 and 4, so the design declines to *charge* for what the game supplies and never credits it as spoken.

> **What's decided.** A missed required slot whose name has a registered resolver is exempt from the score — 0 raw, 0 den — in the parser, everywhere a pattern is scored (adr-0001), "fillable" meaning a resolver is registered for that slot name (adr-0002); the exemption changes score arithmetic only, so admission, the start probe, the bar and the eager gate still see a missed slot (adr-0003); fewer exempted slots ranks better immediately after score (adr-0004); thin firing is accepted and documented, with no guard (adr-0005). The locked model is amended by `scoring-model.md` Amendment A5 (§0E: DR-8, DR-9). All numbers on this page are **provisional → the tuning pass** (the design intent is the load-bearing part).

## 1. The model

The model adds one fact per candidate: which of its missed required slots are *exempt* — missed, and named by a registered resolver (design intent, not yet built). The score stays raw / (den + coverage).

| Element | Outcome | Raw | Den | |
|---|---|---|---|---|
| Required slot | spoken (fillable or not) | +1 | +1 | unchanged |
| Required slot, a resolver registered for its name | missed | **0** | **0** | new — DR-8 |
| Required slot, no resolver registered | missed | −1 | +1 | unchanged |
| Required literal | missed | 0 | +1 | unchanged |

- **M1.** A missed required slot whose name has a registered resolver contributes 0 to raw and 0 to den. A spoken fillable slot scores as today (+1/+1). Every other missed required slot keeps −1/+1 (design intent, not yet built).
- **M2.** The exempt miss still counts as missed for DR-7 admission and the admissibility/start probe; still sets the leading-miss latch, so the bar refuses an exempt anchor first (ruling 3); and still counts as a missed required slot for the eager gate (DR-3), so eager never commits it and an elided command waits for the flush. The coverage term and A3's charged-token rule are unchanged — the exemption touches only the slot's own term (lab: `drive cut drive` 1/3) (design intent, not yet built).
- **M3.** Selection keys become: earliest start → score → fewer exempted slots → consumed span → literal count → registration order (design intent, not yet built).
- **M4.** Registration is read at parse time and the score is fixed then. Resolution (Step 3b) never changes it; a declining resolver leaves the command incomplete → pending (`allowPartialMatch`) or rejected, as today. Pending already admits any score > 0, so there is no new exposure there (design intent, not yet built).
- **M5.** The rule is uniform over every place a pattern is scored — flush, eager, pending follow-up re-score, Editor runner-up comparison, and the batch test runner given the registered set. How is the architecture doc's (design intent, not yet built).
- **M6.** Unchanged: `minScore` (one global, 0.6 provisional), the bar, the sibling-tie reachability warning (construction-time; its discriminator is a literal), and the confidence gate.

**One utterance, resolved.** `"launch missiles"` against `launch missiles target {track}` with a resolver registered for `track`: two literals matched (+2/+2), `target` missed (0/+1), `{track}` exempt (0/0) → 2/3 ≈ 0.667, where today it is 0.250 (the lab measured 0.250000 → 0.666667 on the demo grammar's `launch {?quantity} {weapon} target {target}` under the same arithmetic). It clears the provisional `minScore`; the recogniser then asks the resolver, which fills the slot or declines (M4).

**The two elision shapes** (provisional numbers, `D` = the pattern's required elements). Dropping a fillable slot alone scores (D−1)/(D−1) = 1.000 at any length, against (D−2)/D today. Dropping it with the literal before it — the natural spoken form — scores (D−2)/(D−1), clearing 0.6 at D ≥ 4 against D ≥ 8 today.

## 2. The decisions it puts to its operator

The operator is the game's author, wiring resolvers to a grammar.

1. **Which slot names to register.** Registering is a global opt-in: it exempts that slot in every pattern that carries the name (adr-0002). The author knows the grammar's patterns and which of them name the slot; the cost of registering is that every one of those patterns becomes reachable from its elided form, wanted or not, and the cost of not registering is that the short orders stay below the gate. No choice dominates because the same slot name serves orders the game wants to complete and orders it does not.
2. **When to decline, per intent.** A resolver may decline per intent — the request carries the intent — and that is the author's lever to keep, say, weapon release from firing on a thin utterance. Declining does not change the score (M4); it sends the command to pending or rejection. The resolver knows game state at the moment of asking; what it gives up by declining is the elided order the player meant.
3. **Whether to keep or drop slot-less siblings.** A slot-less sibling still wins its tie against the with-slot pattern (adr-0004), so keeping one means the elided form never asks the resolver; dropping it hands the elided form to the resolver, with its uniform record and its ask-on-decline (§4). Keeping costs a second pattern and a handler fallback; dropping costs the resolver a question it must answer.
4. **Ask or reject on decline.** `allowPartialMatch` decides whether a declined resolution becomes a pending command that asks the player, or a rejection.

## 3. Dynamics & failure modes

**Forward, measured** (lab arm A4L vs A0, `Set_Combat`, F = all 9 required slot names; `Planning~/design-docs/resolver-slot-scoring/lab-2026-10-02.md`). Reachable shape (a) 35/40 vs 18/40; shape (b) 28/40 vs 5/40 (37/40 counting a same-command sibling winning); `{track}` (b) 12/15 vs 2/15; weapon release (b) 4/4 vs 0/4. 0/81 fully spoken utterances change. Eager commits are unchanged (Commit 40/40 over 378 Set_Combat prefixes; 4014 corpus prefixes byte-identical). The start probe is identical at 4,959 positions. The tie-break key decides 14 rounds, all between same-command siblings.

**Attacks on the rules, and what answers each.**

- **Stutter fragments winning on earliest start.** If the exempt miss also left the DR-7 ledger, a fragment like `launch` would be admitted and outrank the real command: `launch launch all missiles target hotel one` → a 0.333 fragment, DocCheck 62/66. Killed by M2 (adr-0003): under A4L the case returns to A0's 0.833 with all slots.
- **The slot-less sibling losing its tie.** Without M3, 5 fully spoken Set_Combat commands asked the resolver for a `burn_level` the speaker deliberately left out, the tie falling to the later selection keys. Killed by M3 (adr-0004).
- **An exempt anchor.** A pattern opening with a fillable slot, spoken without it, would score well; M2 keeps the leading-miss latch, so the bar refuses it first (ruling 3). 0 occurrences were barred in the measured grammar.
- **Thin firing — accepted, not killed** (adr-0005). A pattern whose unspoken elements are all fillable slots scores 1.000 at any length, so bare verbs and two-word fragments fire: 16 of 124 thin utterances under A4L (2 one-word, `drive` and `thrusters`; 14 two-word, among them `launch track` and `set to`). It is bounded by the bar (the anchor was spoken), the confidence gate, the author's opt-in, and the resolver's per-intent decline (§2).
- **Two commands where one was meant — accepted.** 2 corpus rows (`disengage close distance safe range` and its reverse) fire two commands: A0 already fired the debris word `disengage` → `cease_fire`, and A4L adds the speaker's real phrase.
- **No slack at the shape-(b) threshold — residue.** Shape (b) needs D ≥ 4, and one stray word drops it: `please launch jackal` 0.500.
- **Ties the key cannot separate — residue.** Equal score and equal exemptions across intents still fall to registration order: `orient to` → `orient_to_heading` over `orient_to_track`.
- **The eager hold still moves, never to Commit.** 3 Set_Combat prefixes go HoldExtendable → None, because the changed score elects a different, slot-incomplete eager winner; M2 keeps every such winner from committing.
- **Tools that score without the registry.** A batch test runner or Editor tool given no registered set disagrees with the runtime (adr-0002). M5 requires each scoring site to apply the rule given the same set; the architecture doc owns how.
- **Pending.** A declining resolver sends the command to pending exactly as today, and pending already admits any score > 0, so the exemption opens nothing new there (M4).

## 4. Why it beats the slot-less sibling workaround

The idiom the grammar already uses (`intercept track {track}` beside `intercept track {track} {burn_level}`, issue #161) costs a second pattern per command and a per-handler fallback, and leaves no uniform record of what was filled or why (`ResolvedSlots`, the resolution reason) and no way to ask when the game declines. The exemption gives every elided order the resolver's uniform path — filled with a recorded reason, or declined into pending or rejection — and does not take the idiom away: a sibling the author wrote still wins its tie (adr-0004).

## 5. Boundaries & couplings

- **Owns:** the charge for a fillable missed required slot (M1, M2, M4) and the fewer-exempted-slots selection key (M3).
- **Consumes:** the resolver registry (#148's `RegisterSlotResolver`), read at parse time.
- **Does not decide:** `minScore`, per-command thresholds, the bar, acoustics, the confidence gate, eager, or the game project's scene assets.
- **Hands off:** implementation and tool agreement (M5) to the architecture doc; the DocCheck pins — the two direct score pins move, §7 A start 2 0.167 → 0.400 and `fire at` 0.333 → 1.000; product docs after G2 — `scoring.md`, `command-recognition.md` authoring guidance, and a `KNOWN_LIMITATIONS.md` entry for thin firing and the two-command rows.

## 6. What it delivers

A captain says "launch missiles": the pattern scores as though the target slot were not part of it, clears the gate, and the recogniser asks the game, which names the target or declines — the elided order fires with its filled slot on record, or turns into a question (#148's player value; ruling 9). Nothing the speaker did not say is credited as said, and the bar is untouched (ruling 3). The exempt miss still counts for the eager gate, so no command with an unfilled required slot commits early (`scoring-model.md` §3 goal 4), and the coverage term is untouched (goal 2). Where the author already wrote a slot-less sibling, it keeps winning (adr-0004).

## 7. Feature backlog — locked at G1 (2026-10-02)

One feature. It is **Full**: the design leaves open how the parser sees the registry and how each tool is given the same registered set (M5), so the build writes its own requirements and architecture docs.

| Feature | Scope |
|---|---|
| `feat-resolver-slot-exemption` | Build DR-8 and DR-9 (`scoring-model.md` §0E) in the parser: the exemption and the fewer-exempted-slots selection key, the resolver registry read at parse time, and every scoring site agreeing with it (flush, eager, pending follow-up re-score, Editor runner-up comparison, the batch test runner given the registered set — M5); tests; the two DocCheck pin moves (§7 A start 2 0.167 → 0.400; `fire at` 0.333 → 1.000). After G2: `scoring.md`, `command-recognition.md` authoring guidance, a `KNOWN_LIMITATIONS.md` entry for thin firing and the two-command rows, and `CHANGELOG.md`. **Full.** |

## 8. Status / openness

- **Status: G1 LOCKED 2026-10-02.** The model was settled on 2026-10-02; its five forks are recorded in adr-0001 through adr-0005 and amend `scoring-model.md` as Amendment A5 (§0E, ratified at the same G1).
- **All numbers provisional → the tuning pass:** `minScore` (0.6); the shape-(b) threshold D ≥ 4; every score quoted on this page — owner: the tuning pass of `feat-resolver-slot-exemption`.
- **Hard dependencies / open:**
  - Real elision shapes and acoustics are unmeasured; R5 human-corpus evidence could reopen adr-0005 — owner: the maintainer, through Phase C instrument R5 (human corpus).
  - The game's `Prototype_CIC` and `Prototype_TacticalMap` scenes cannot build a parser (5 dangling slot-asset GUIDs, `burn_level` and `direction` among the missing slots) — the game's bug, outside this topic — so only `Prototype_Mauevering` was measured — owner: the maintainer, in the `VR FTL-Like 3` project, outside this topic.
  - The feature backlog is §7's, written at G1.

## 9. Related

`Planning~/design-docs/scoring-model.md` (§0E Amendment A5, §3, §5.1, §5.3) · `Planning~/design-docs/leading-miss-bar.md` · `Planning~/features/slot-resolver/requirements.md` (§2, §6 E-1, §8 E-2, F19) · `Planning~/research/2026-09-05-next-level/05-rulings.md` (rulings 3 and 9) · `Planning~/design-docs/resolver-slot-scoring/lab-2026-10-02.md` · `Planning~/design-docs/resolver-slot-scoring/decisions/adr-0001-exempt-fillable-slot-in-parser.md` · `…/adr-0002-fillable-means-registered.md` · `…/adr-0003-exemption-changes-arithmetic-only.md` · `…/adr-0004-fewer-exempted-slots-tie-break.md` · `…/adr-0005-thin-firing-accepted.md`
