---
type: requirements
feature: bench-fast-replay
topic: audio-bench
status: draft
updated: 2026-11-08
sources:
  - Planning~/design-docs/audio-bench.md
  - Planning~/design-docs/audio-bench/decisions/adr-0001-two-clocks-split.md
  - Planning~/design-docs/audio-bench/decisions/adr-0002-bridge-and-rig.md
  - Planning~/design-docs/audio-bench/decisions/adr-0003-fast-and-paced-modes.md
  - Planning~/design-docs/audio-bench/decisions/adr-0006-generator-speech-truth.md
---

# feat-bench-fast-replay — requirements

Track **Full**. Design `audio-bench` (`Planning~/design-docs/audio-bench.md`), locked at G1 2026-11-08, merged as PR #197. Depends on no feature. Path `development`.

## 1. User story

- **S1** — As the maintainer measuring a Phase D change, I want a fast desktop bench that replays the TTS fixtures through the real native bridge and C# command recogniser, stamps each stage in audio time from end of speech, and scores command accuracy, WER and confidence calibration against the pinned transcripts, so that I can see a change's effect on end-of-speech → command latency (the part that carries over to Quest) and on accuracy before any Quest session, and stop a regression early.

## 2. Feature & scope

Backlog row (`audio-bench.md` §6):

> `bench-fast-replay` | Fast mode through the native bridge in 16384-sample pushes (adr-0002, adr-0003); audio-time stamps (adr-0001); the generator's speech-boundary truth in `generate.py` (adr-0006); the command stage through the joined rig (adr-0002); accuracy scoring: command classes, WER, ECE/NCE; reports naming their full config; the pinned-transcript self-check (adr-0003). Path: `development`. **Full.** | None.

**Agreed at G0 on 2026-11-08** by the maintainer, in session, as drafted; concurrent with `lab-next-level-probes` (independent; shares the confidence-calibration subject through its TTS NCE/ECE dry-run probe).

**In**

1. Fast mode: the TTS fixtures replayed unpaced into the real native bridge in the harness's 16384-sample pushes (adr-0002, adr-0003).
2. Audio-time stamps per stage — first partial, final = endpoint, command event — from the bridge's `consumed` sample counts (adr-0001).
3. The generator's speech-boundary truth: `Tests~/Fixtures/generate.py` writes each clip's speech start and end; every latency counts from end of speech (adr-0006).
4. The command stage: the run's timeline fed to the real C# recogniser through the joined rig, the command event stamped (adr-0002).
5. Accuracy scoring: the command classes (correct-fire, correct-no-fire, wrong-slots, wrong-intent, missed, false-fire) and from them intent accuracy, semantic accuracy and phantom rate; WER against the expected transcript; confidence calibration, per-word ECE and NCE.
6. Reports naming their full config — recogniser settings, endpointer profile, gain, voice, seed — and the corpus provenance.
7. The pinned-transcript self-check: fast mode must reproduce the harness's pinned transcripts exactly, or none of its numbers may be quoted (adr-0003).

**Out**

1. Paced 20 ms replay, wall-clock stamps and the real-time factor — `bench-paced-mode`.
2. The seeded condition pipeline (noise, echo, mic filtering, level, speed), the stand-in negative set and per-condition curves — `bench-conditions`.
3. Kokoro-82M voices — `bench-voices`.
4. Paired A/B runs, p50/p90/p99 and McNemar — `bench-ab-report`.
5. Developer usability; Quest's absolute timing; ranking two decoders or decoder configurations on TTS-only audio (C12) — the design's non-goals.
6. Left untouched: the bridge interface (a change found needed is a scope stop, design §4); Command 3 and its pinned transcripts ("re-pin, never edit"); the committed fixture WAVs.

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
