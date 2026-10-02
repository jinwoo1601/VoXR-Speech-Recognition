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

_To be written by feature-requirement-doc._

## 4. Observable behaviour

_To be written by feature-requirement-doc._

## 5. Functional requirements

_To be written by feature-requirement-doc._

## 6. Non-functional requirements

_To be written by feature-requirement-doc._

## 7. Non-goals / deferred

_To be written by feature-requirement-doc._

## 8. Dependencies & assumptions

_To be written by feature-requirement-doc._

## 9. Acceptance criteria (G2)

_To be written by feature-requirement-doc._

## 10. Open questions

_To be written by feature-requirement-doc._
