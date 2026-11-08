---
type: requirements
feature: bench-paced-mode
topic: audio-bench
status: draft
updated: 2026-11-09
sources:
  - Planning~/design-docs/audio-bench.md
  - Planning~/design-docs/audio-bench/decisions/adr-0001-two-clocks-split.md
  - Planning~/design-docs/audio-bench/decisions/adr-0002-bridge-and-rig.md
  - Planning~/design-docs/audio-bench/decisions/adr-0003-fast-and-paced-modes.md
  - Planning~/features/bench-fast-replay/requirements.md
---

# feat-bench-paced-mode — requirements

Track **Full**. Design `audio-bench` (`Planning~/design-docs/audio-bench.md`), locked at G1 2026-11-08, merged as PR #197. Depends on `bench-fast-replay` (merged as PR #198, `63434cb`, accepted at G2 2026-11-09). Path `development`.

## 1. User story

- **S1** — As the maintainer measuring a Phase D change, I want a paced bench mode that replays the TTS fixtures into the real native bridge in 20 ms chunks at real speed, stamps each stage on wall time beside audio time, splits each stage's latency into the audio-time part and a desktop-only compute part, gives the decoder's real-time factor, and carries the command event's wall stamp through the joined rig, so that I can see what a change costs in decode and queue lag — the part fast mode is blind to — and trust that split because a matched-chunk self-check guards it.

## 2. Feature & scope

Backlog row (`audio-bench.md` §6):

> `bench-paced-mode` | Paced 20 ms replay with wall stamps and the real-time factor (adr-0001, adr-0003); the matched-chunk split self-check; the command event's paced-arrival wall stamp (adr-0002). Path: `development`. **Full.** | `bench-fast-replay`

**Agreed at G0 on 2026-11-09** by the maintainer, in session, as drafted; concurrent with `lab-next-level-probes` (independent; the lab writes only under `.scratch/lab-next-level-probes/` and merges nothing).

**In**

1. Paced mode: the TTS fixtures pushed into the real native bridge in 20 ms chunks at real speed (adr-0002, adr-0003); the 20 ms chunk is provisional → the tuning pass.
2. Wall stamps — a monotonic clock read at the bench's push and at its pop of each result — for first partial and final = endpoint, beside the audio-time stamps; each stage's latency split into an audio-time part and a compute part (wall − audio), the compute part labelled desktop-only (adr-0001).
3. The real-time factor: decode time ÷ audio duration, for paced runs only (adr-0003).
4. The command event's paced-arrival wall stamp: the joined rig delivers each result at its recorded paced wall-arrival time, on its frame clock; the command stage's compute part is its paced time − its fast time (adr-0002).
5. The matched-chunk split self-check: paced timing at fast mode's 16384-sample pushes must match fast mode's audio-time stamps to within one decoder read (4096 samples), or the split is wrong and the bench stops, quoting no numbers (adr-0001, adr-0003).
6. Paced-vs-fast transcript differences at 20 ms pacing reported as a finding, not a self-check failure (adr-0003).
7. Reports extending `bench-fast-replay`'s: they name the mode and carry the paced stamps, the split and the real-time factor.

**Out**

1. The seeded condition pipeline, the stand-in negative set and per-condition curves — `bench-conditions`; Kokoro-82M voices — `bench-voices`; repeated and paired A/B runs, p50/p90/p99 and McNemar — `bench-ab-report`.
2. Measuring the C# recogniser's frame cost live (the rig's frame clock models it, adr-0002); Quest's absolute timing and how the on-device stamp is taken; comparing wall numbers across machines.
3. Developer usability; ranking two decoders or decoder configurations on TTS-only audio (C12) — the design's non-goals.
4. Left untouched: the bridge interface (a change found needed is a scope stop, design §4); Command 3 and its pinned transcripts ("re-pin, never edit"); the committed fixture WAVs.

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
