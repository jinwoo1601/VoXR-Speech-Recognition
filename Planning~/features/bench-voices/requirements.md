---
type: requirements
feature: bench-voices
topic: audio-bench
status: draft
updated: 2026-11-22
sources:
  - Planning~/design-docs/audio-bench.md
  - Planning~/design-docs/audio-bench/decisions/adr-0005-kokoro-voices.md
  - Planning~/features/bench-fast-replay/requirements.md
---

# feat-bench-voices — requirements

Track **Full**. Design `audio-bench` (`Planning~/design-docs/audio-bench.md`), locked at G1 2026-11-08, merged as PR #197. Depends on `bench-fast-replay` (merged as PR #198, `63434cb`). Path `development`.

## 1. User story

- **S1** — As the maintainer measuring a Phase D change on the desktop bench, I want the bench to run its cases in several Kokoro-82M voices beside the baseline Piper voice, so that a change that breaks on other speakers shows up before a Quest session, while the baseline's pinned transcripts never move.

## 2. Feature & scope

Backlog row (`audio-bench.md` §6):

> `bench-voices` | Kokoro-82M voices beside the baseline Piper voice (adr-0005). Path: `development`. **Full.** | `bench-fast-replay`

**Agreed at G0 on 2026-11-22** by the maintainer, in session, as drafted; concurrent with `lab-next-level-probes` (independent; the lab writes only under `.scratch/lab-next-level-probes/` and merges nothing) and with `release` (held, no branch; the only overlap `CHANGELOG.md`, a release file).

**In**

1. Kokoro-82M (Apache-2.0) as a second voice source in the bench: each case is a clip × condition × voice (adr-0005; design §2 "State, per run").
2. The baseline Piper voice `en_US-lessac-medium` kept as the baseline voice; its pinned transcripts never move (adr-0005; `05-rulings.md` :98, "re-pin, never edit").
3. Every report names the voice it ran with (adr-0005; design §3, hidden config differences).
4. Until the maintainer has downloaded the Kokoro model, the bench runs the baseline voice only (adr-0005 Consequences).
5. The voice count is provisional → the tuning pass (~12 per R6) (adr-0005; design Status, "All numbers provisional").

**Out**

1. The Kokoro model download itself — a human step, the maintainer's (adr-0005).
2. Re-pinning or editing the existing Piper fixtures and their pins.
3. Ranking two decoders or decoder configurations on TTS-only audio, any voice set included (C12; adr-0005 Consequences), and gating verification (adr-0008); developer usability and Quest's absolute timing — the design's non-goals.
4. Paced mode — `bench-paced-mode`; paired A/B runs — `bench-ab-report`.

## 3. Why — the value it delivers

<!-- The goal of the locked design this feature carries, and what its users get that they did not have before. This is the "why" behind every requirement below — the thing you protect when a tradeoff forces a cut. -->

## 4. Observable behaviour

<!-- The experiential spec: what someone using this feature does, sees and gets back, step by step. Written before the table below, because every row in it is a slice of this. -->

## 5. Functional requirements

<!-- Numbered, testable, prioritized. Each row: the requirement (what and why, not how), its priority, how it is verified, and its parent in locked design; each row names the story it serves. -->

| #  | Requirement | Story | Priority | Acceptance check | Source |
|----|-------------|-------|----------|------------------|--------|
| F1 | <observable behaviour> | S1 | Must / Should / Could | <how it is verified> | <design doc / decision record> |
| F2 |             |       |          |                  |        |

## 6. Non-functional requirements

<!-- First-class: on some platforms these are the feature-killers. Keep the rows that apply, add the ones this project's platform makes load-bearing, and cut the rest rather than leaving them blank. -->

- **Performance:** <the budget this feature must hold, and any per-operation ceiling if it is on a hot path>
- **Robustness at the input boundary:** <what the feature does with malformed, ambiguous or low-confidence input — the response it gives, never silence>
- **Degraded dependencies:** <what it does when something it depends on is slow, unavailable or partially wrong>
- **Accessibility:** <if applicable>

## 7. Non-goals / deferred

<!-- As sharp as the goals. What this feature does NOT do; what is explicitly deferred to a later feature; which adjacent systems it touches but does not own. This is the scope fence — the thing that stops a two-week feature becoming two months. -->

## 8. Dependencies & assumptions

<!-- What must already exist or be merged first (features are dependency-ordered — declare the order). What is assumed about the people using this and about the rest of the system. Unstated assumptions are where requirements rot. -->

## 9. Acceptance criteria (G2)

<!-- The consolidated "done" checklist the human walks at G2. Pull the Must-priority checks from §5, then add the verification only a human can do and the commands the project's verification binding names. If every §5 row already carries its check, this can just collect the Musts plus those two. -->

- [ ] <criterion — from the §5 Musts>
- [ ] Human-only verification: <what the human must exercise by hand to accept this>
- [ ] The commands the project's `verification` binding names are green

## 10. Open questions

<!-- The unknowns you did NOT resolve — handed forward to the architecture doc. Honest beats complete; the architecture doc would rather inherit a known unknown than hit it mid-build. -->
