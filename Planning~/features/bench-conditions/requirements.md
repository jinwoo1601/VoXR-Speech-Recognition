---
type: requirements
feature: bench-conditions
topic: audio-bench
status: draft
updated: 2026-11-09
sources:
  - Planning~/design-docs/audio-bench.md
  - Planning~/design-docs/audio-bench/decisions/adr-0004-seeded-on-the-fly-conditions.md
  - Planning~/design-docs/audio-bench/decisions/adr-0007-stand-in-negatives.md
  - Planning~/features/bench-fast-replay/requirements.md
---

# feat-bench-conditions — requirements

Track **Full**. Design `audio-bench` (`Planning~/design-docs/audio-bench.md`), locked at G1 2026-11-08, merged as PR #197. Depends on `bench-fast-replay` (merged as PR #198, `63434cb`). Path `development`.

## 1. User story

- **S1** — As the maintainer, I want the bench to replay the fixtures under seeded noise, echo, mic, speed and level conditions, plus a stand-in negative set, and report accuracy and false fires as one curve per condition, so that I can see where a recognition change breaks and whether it adds phantom commands, before a Quest session.

## 2. Feature & scope

Backlog row (`audio-bench.md` §6):

> `bench-conditions` | The seeded on-the-fly condition pipeline (adr-0004); the stand-in negative set (adr-0007); per-condition curves. Path: `development`. **Full.** | `bench-fast-replay`

**Agreed at G0 on 2026-11-09** by the maintainer, in session, as drafted; concurrent with `lab-next-level-probes` (independent; the lab writes only under `.scratch/lab-next-level-probes/` and merges nothing).

**In**

1. The seeded on-the-fly condition pipeline: per case and from a fixed seed — speed change, synthetic room echo, generated noise at SNR steps, mic-like filtering, and level into the Quest mic range; each clip's speech boundaries kept (adr-0006); only clean renders committed (adr-0004). The steps — speed {0.9, 1.0, 1.1}, RT60 0.2–0.5 s, SNR {20, 15, 10, 5} dB, level [0.04, 0.4] — are provisional → the tuning pass.
2. MUSAN (CC BY 4.0) noise and OpenSLR SLR28 room impulse responses (Apache-2.0) used as an optional add-on where the maintainer has downloaded them; until then noise and echo are the generated and synthetic kinds only (adr-0004).
3. Every report names the condition and the seed it ran with (adr-0004; design §3, hidden config differences).
4. The stand-in negative set: generated noise-only clips at each condition, silence, and TTS phrases that are not commands (out-of-set words, near-misses); its false-fire rate marked provisional and stand-in until R4 replaces it; zero false fires reads as "set too easy", not "zero" (adr-0007).
5. Per-condition curves — e.g. accuracy against SNR — never pooled; significance counted per phrase, not per phrase × condition (adr-0004; `R6-evaluation-methodology.md` :198, :336).
6. The open item this feature owns (design :208): whether the stand-in negative set needs game audio — settled in requirements; if it does, the maintainer supplies the title audio.

**Out**

1. Kokoro-82M voices — `bench-voices`; paired A/B runs, p50/p90/p99 and McNemar — `bench-ab-report`.
2. The R4 negative corpus and the R5 human corpus (human recording); committing conditioned audio (adr-0004).
3. The MUSAN and SLR28 download itself — a human step, the maintainer's (adr-0004).
4. Ranking two decoders or decoder configurations on TTS-only audio (C12), and gating verification (adr-0008); developer usability and Quest's absolute timing — the design's non-goals.
5. Left untouched: the bridge interface (a change found needed is a scope stop, design §4, :159).

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
