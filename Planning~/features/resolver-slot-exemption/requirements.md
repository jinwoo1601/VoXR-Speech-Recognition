---
type: requirements
feature: resolver-slot-exemption
topic: resolver-slot-scoring
status: draft
updated: 2026-10-02
sources:
  - Planning~/design-docs/resolver-slot-scoring.md
  - Planning~/design-docs/scoring-model.md (§0E, Amendment A5: DR-8, DR-9)
  - Planning~/design-docs/resolver-slot-scoring/decisions/adr-0001-exempt-fillable-slot-in-parser.md
  - Planning~/design-docs/resolver-slot-scoring/decisions/adr-0002-fillable-means-registered.md
  - Planning~/design-docs/resolver-slot-scoring/decisions/adr-0003-exemption-changes-arithmetic-only.md
  - Planning~/design-docs/resolver-slot-scoring/decisions/adr-0004-fewer-exempted-slots-tie-break.md
  - Planning~/design-docs/resolver-slot-scoring/decisions/adr-0005-thin-firing-accepted.md
  - https://github.com/jinwoo1601/VoXR-Speech-Recognition/issues/161
---

# feat-resolver-slot-exemption — requirements

Track **Full**. Design `resolver-slot-scoring` (`Planning~/design-docs/resolver-slot-scoring.md`), locked at G1 2026-10-02. Tracked at **#161**.

## 1. User story

- **S1** — As a game author wiring slot resolvers to my grammar, I want a required slot my resolver can fill to cost nothing in the score when the player leaves it out, so that short orders like "launch missiles" clear the gate and reach my resolver instead of being rejected.

## 2. Feature & scope

Backlog row (`resolver-slot-scoring.md` §7):

> `feat-resolver-slot-exemption` | Build DR-8 and DR-9 (`scoring-model.md` §0E) in the parser: the exemption and the fewer-exempted-slots selection key, the resolver registry read at parse time, and every scoring site agreeing with it (flush, eager, pending follow-up re-score, Editor runner-up comparison, the batch test runner given the registered set — M5); tests; the two DocCheck pin moves (§7 A start 2 0.167 → 0.400; `fire at` 0.333 → 1.000). After G2: `scoring.md`, `command-recognition.md` authoring guidance, a `KNOWN_LIMITATIONS.md` entry for thin firing and the two-command rows, and `CHANGELOG.md`. **Full.**

**Agreed at G0 on 2026-10-02** by the maintainer, in session.

**In**

1. DR-8 in the parser: a missed required slot whose name has a registered slot resolver contributes 0 raw / 0 den; it still counts as missed for DR-7 admission, the start probe, the leading-miss latch and the eager gate (design M1, M2, M4 — arithmetic only).
2. DR-9 selection keys: earliest start → score → fewer exempted slots → consumed span → literal count → registration order (M3).
3. The resolver registry read by the parser at parse time (today the parser holds no registry reference and `RegisterSlotResolver` triggers no rebuild).
4. Every scoring site agreeing with it (M5): flush, eager, pending follow-up re-score, Editor runner-up comparison, and the batch test runner given the registered set. How each site gets the set is left to the requirements and architecture docs.
5. Tests, and the two DocCheck pin moves (§7 A start 2 0.167 → 0.400; `fire at` 0.333 → 1.000).
6. After G2 only: `scoring.md`, `command-recognition.md` authoring guidance, a `KNOWN_LIMITATIONS.md` entry for thin firing and the two-command rows, `CHANGELOG.md`; #161 closes.

**Out**

- Changing `minScore` or adding a per-command threshold; the tuning pass (deferred — numbers stay provisional).
- The leading-miss bar, the confidence gate, acoustics.
- Any guard against thin firing (accepted by adr-0005).
- The game project's broken `Prototype_CIC` / `Prototype_TacticalMap` scenes.
- The human speech corpus (R5).

## 3. Why — the value it delivers

Ruling 9 defines ellipsis as **game-state resolution of an omitted required slot** (`resolver-slot-scoring.md` §6). The short orders a resolver exists for never reach it today, because a missed required slot costs −1/+1: `"launch missiles"` against `launch missiles target {track}` scores **0.250** against a 0.6 gate (`slot-resolver/requirements.md` §8 E-2). This feature lets them reach the resolver **without crediting anything unspoken** (ruling 3): `scoring-model.md` §3 goal 2's coverage is untouched, and goal 4 holds — eager never commits an unfilled required slot.

What authors get: **one uniform path for elided orders** — filled, with a recorded reason, or declined into pending or rejection — instead of hand-written slot-less sibling patterns (`resolver-slot-scoring.md` §4).

**The thing to protect in a cut:** never credit the unspoken; never commit early.

## 4. Observable behaviour

1. The author registers a resolver for `track`. The player says "launch missiles" against `launch missiles target {track}` → the candidate scores 2/3 ≈ **0.667** (was 0.250), clears `minScore`, and the resolver is asked. It fills → the command fires with the slot in `ResolvedSlots`. It declines → pending (`allowPartialMatch`) or rejection, as today. The fired `Score` is the same in both cases.
2. With no resolver for `track`, the same utterance scores 0.250 and is rejected, exactly as today. Unregistering takes effect from the next utterance; no rebuild and no `NotifySlotChanged()`.
3. A pattern whose anchor is the resolver-registered slot, spoken without it, is still barred first; the resolver is not asked.
4. The elided command is never eager-committed; it fires at the flush.
5. Where the author kept a slot-less sibling (`intercept track {track}` beside `intercept track {track} {burn_level}`), the sibling still wins and no resolver is asked for `burn_level`.
6. The batch test runner, given the same registered slot names, reports the same score, and reports "would ask resolver for `<slot>`" for an elided case; given none, it behaves as today.

## 5. Functional requirements

**Ruled at SETTLE on 2026-10-02** by the maintainer, in session: **R1** the batch test runner takes the registered slot *names*, not resolver delegates, scores as the runtime does, and reports "would ask resolver for `<slot>`" for an utterance whose only gap is a resolver-registered slot; **R2** a Batch Test window field for registered slot names is Could; **R3** making the exemption visible in diagnostics is out of scope; **R4** reproducing lab arm A4L against the built parser is a Should acceptance check.

| #   | Requirement | Story | Priority | Acceptance check | Source |
|-----|-------------|-------|----------|------------------|--------|
| F1  | A missed required slot whose name has a registered resolver contributes **0 raw / 0 den**; a spoken fillable slot scores +1/+1; every other missed required slot keeps −1/+1. The coverage term and A3's charged-token rule are unchanged. | S1 | Must | Unit tests: `launch missiles target {track}` with a `track` resolver → 0.667, without → 0.250; `drive cut drive` coverage 1/3. | `scoring-model.md` §0E DR-8, M1; adr-0001 |
| F2  | The exempt miss **still counts as missed** for DR-7 admission, the start probe, the leading-miss latch and the eager gate. | S1 | Must | The stutter `launch launch all missiles target hotel one` keeps today's winner (0.833, all slots); an exempt anchor is barred and the resolver not consulted; the elided command gets no eager Commit and fires at the flush. | `scoring-model.md` §0E DR-8, M2; adr-0003 |
| F3  | Selection orders earliest start → score → **fewer exempted slots** → consumed span → literal count → registration order. | S1 | Must | The sibling case in §4 step 5; a candidate-comparison test per key. | `scoring-model.md` §0E DR-9, M3; adr-0004 |
| F4  | "Fillable" means a resolver is registered for that slot name, read at parse time. Register and unregister take effect from the next utterance with no rebuild and no `NotifySlotChanged()`; a resolver for a name not in the grammar changes nothing and throws nothing. | S1 | Must | Register mid-session → the next `InjectText` scores exempt; unregister → the next scores −1/+1. | adr-0002; `scoring-model.md` §0E M4; `slot-resolver/requirements.md` F5 |
| F5  | **Resolution never changes the score:** the fired command's `Score` equals the score of the same utterance with the same resolver registered but declining. | S1 | Must | A test pair, fill vs decline. | `scoring-model.md` §0E M4; `slot-resolver/requirements.md` F19 (amended) |
| F6  | **Every in-package scoring site** applies F1–F3 given the same registered set: flush, eager, the pending follow-up re-score, and the Editor runner-up comparison. | S1 | Must | A pending command whose remaining unfilled required slot has a resolver re-scores per F1 after a follow-up; the Editor runner-up score equals that candidate's selection score. | `scoring-model.md` §0E M5; adr-0001 |
| F7  | The batch test runner can be given the registered slot names. Given them, its scores equal the runtime's, and an utterance whose only gap is a resolver-registered slot gets the verdict "would ask resolver for `<slot>`", distinct from "required slot unfilled"; given none, its behaviour is unchanged. It never calls resolvers. | S1 | Must | Runner tests for all three. | `scoring-model.md` §0E M5; adr-0002; ruling R1 |
| F8  | With **no resolver registered**, scores, selection, eager verdicts and diagnostics are identical to today. | S1 | Must | The existing EditMode and PlayMode suites pass unchanged, except tests the amended canon re-pins (F5); every DocCheck pin other than the two in F9 unchanged. | `scoring-model.md` A5.5 "Not affected"; `slot-resolver/requirements.md` F14 |
| F9  | The two DocCheck score pins move — §7 A start 2 0.167 → **0.400**, `fire at` 0.333 → **1.000** — under a registered set, and DocCheck passes **66/66**. | S1 | Must | DocCheck run output. | `scoring-model.md` A5.5; adr-0001 |
| F10 | The built parser reproduces lab arm **A4L** on Set_Combat (`Prototype_Mauevering`) and the demo corpus: reachable 35/40 shape (a), 28/40 shape (b); 16/124 thin fires; 0/81 fully spoken utterances change; eager Commit 40/40 over 378 prefixes; 4014 corpus prefix verdicts byte-identical; start probe 0 differ over 4959 positions; the new key decides 14 rounds. | S1 | Should | A rig report against the built parser. | `lab-2026-10-02.md`; ruling R4 |
| F11 | The Batch Test window lets the author list registered slot names for a run. | S1 | Could | Manual Editor check. | Ruling R2 |

## 6. Non-functional requirements

- **Performance:** the parser is a hot path (`Runtime/Commands/**`, review binding). With no resolver registered, no added per-candidate cost or allocation; with resolvers registered, no per-candidate allocation in the scoring loop. Verified at review.
- **Robustness at the input boundary:** a registration change made during a parse (from a resolver or an event subscriber) applies no later than the next utterance; one parse pass sees one registered set throughout. Derived from `scoring-model.md` §0E M4.
- **Degraded dependencies:** a declining resolver → pending or rejection, as today; a throwing resolver propagates, as today (`slot-resolver/requirements.md` F20), unchanged.

## 7. Non-goals / deferred

The G0 **Out** list in §2, plus:

- The exemption made visible in diagnostics — the debug window, the session log or `VoxrCommand` (ruling R3).
- The batch test runner calling resolver delegates (ruling R1).
- Any change to the construction-time sibling-tie reachability scan, which stays as it is (`scoring-model.md` §0E M6).
- A resolver `Clear` API.
- Amending `slot-resolver/requirements.md` §8 E-2's pre-A5 arithmetic — superseded by A5, and locked.
- Any change to pending admission (any score > 0, `scoring-model.md` §0E M4).

## 8. Dependencies & assumptions

**Depends on:** #148 `slot-resolver` (merged: the registry, Step 3b) and Amendment A5, locked at G1 2026-10-02.

**Rigs:** F9 needs the DocCheck rig (`Planning~/features/coverage-in-selection/ab-rig/`); F10 needs the lab rig and the game project `VR FTL-Like 3`'s `Prototype_Mauevering` grammar — the only scene that builds a parser.

**Assumption:** resolvers are registered on the main thread — a documentary contract today.

**Assumption:** existing resolver tests that pin the elided score — e.g. `Resolver_FilledSlot_DoesNotChangeTheScoreOrConfidence` — are re-pinned to the amended `slot-resolver/requirements.md` F19.

## 9. Acceptance criteria (G2)

- [ ] Every Must row's check (F1–F9) passes, with evidence.
- [ ] The EditMode and PlayMode suites are green, per the `verification` binding.
- [ ] DocCheck passes 66/66.
- [ ] F10's rig report, or a recorded reason it was not run.
- [ ] F11 built, or recorded as not built.
- [ ] A full review pass.
- [ ] After G2: the product docs named in §2 **In** item 6, and #161 closed.

## 10. Open questions

1. **How the parser sees the registry, and how each site gets the same set** (`scoring-model.md` §0E M5) — the architecture doc's.
2. **The runner's input shape, and the verdict's exact wording and CSV form** — the architecture doc's, within ruling R1.
3. **Stale after this build:** the `slot-resolver` architecture lines saying the parser is untouched (`architecture.md` ~41, ~81); the runner's comment at `Runtime/Testing/VoxrBatchTestRunner.cs:200-217`; the D-13 divergence note. Who updates each — the architecture doc, or the product docs after G2 — is open.
4. **The tuning pass is deferred** (G0); the numbers stay provisional.
