---
type: requirements
feature: agc-filter
topic: agc-filter
status: draft
updated: 2026-11-05
sources:
  - Planning~/design-docs/agc-filter.md
  - Planning~/design-docs/agc-filter/decisions/adr-0001-editor-native-dsp.md
  - Planning~/design-docs/agc-filter/decisions/adr-0002-own-fir-decimator.md
  - Planning~/design-docs/agc-filter/decisions/adr-0003-agc2-classes-pinned-source.md
  - Planning~/design-docs/agc-filter/decisions/adr-0004-tts-no-regression-acceptance.md
---

# feat-agc-filter — requirements

Track **Full**. Design `agc-filter` (`Planning~/design-docs/agc-filter.md`), locked at G1 2026-11-05, merged as PR #190. Depends on no feature. Path `development`.

## 1. User story

- **S1** — As a developer shipping VoXR voice commands on Quest and testing them in the Windows Editor, I want one native front end (a DC-normalised FIR decimator feeding WebRTC AGC2) run the same way on device and in the Editor, so that commands spoken after silence are recognised without the front end's gain errors, and what I test in the Editor is what ships.

## 2. Feature & scope

Backlog row (`agc-filter.md` §6):

> `agc-filter` | The DC-normalised FIR decimator in place of the 15-tap table (rules 1–2, adr-0002); the AGC2 subset from pinned `webrtc-audio-processing` 2.1 source through our CMake (rules 6, 9, adr-0003); one native front end on device and in the Windows Editor by P/Invoke, the C# `Agc.cs` and `Downsampler.cs` retired, and the parity test (rules 7–8, adr-0001); the learned level kept across stop/start and `micGainTargetDb` mapped to AGC2's headroom (rules 10–11); the silence-then-command fixtures, the joined-rig A/B as a no-regression gate with the kill criteria, and the two measured defects verified directly (rules 12–13, adr-0004). Path: `development`. **Full.** | None.

**Agreed at G0 on 2026-11-05** by the maintainer, in session, as drafted; concurrent with `light-batch-b` (independent).

**In**

1. The DC-normalised FIR decimator in place of the 15-tap table (rules 1–2, adr-0002).
2. The AGC2 subset from pinned `webrtc-audio-processing` 2.1 source through our CMake (rules 6, 9, adr-0003).
3. One native front end on device and in the Windows Editor by P/Invoke, the C# `Agc.cs` and `Downsampler.cs` retired, and the parity test (rules 7–8, adr-0001).
4. The learned level kept across stop/start and `micGainTargetDb` mapped to AGC2's headroom (rules 10–11).
5. The silence-then-command fixtures, the joined-rig A/B as a no-regression gate with the kill criteria, and the two measured defects verified directly (rules 12–13, adr-0004).

**Out**

1. R16, using the voice detector to gate decoder audio or to decide end-of-utterance. It waits on Phase B probes 2 and 3 (not run; they need the human's Quest session), carries its own kill criterion, and touches the R11 endpointer: a later topic.
2. Silero or ONNX Runtime.
3. Building the R5 or R4 corpora.
4. Tuning thresholds outside this unit.
5. Any parser change.
6. The non-Windows Editor, which has no backend today and stays unchanged (design §7).

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
