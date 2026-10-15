---
type: requirements
feature: endpointer-profile
topic: endpointer-profile
status: draft
updated: 2026-10-16
sources:
  - Planning~/design-docs/endpointer-profile.md
  - Planning~/design-docs/endpointer-profile/decisions/adr-0001-runtime-overlay.md
  - Planning~/design-docs/endpointer-profile/decisions/adr-0002-default-flip-continuous-gated.md
  - Planning~/design-docs/endpointer-profile/decisions/adr-0003-named-presets.md
  - Planning~/design-docs/endpointer-profile/decisions/adr-0004-pause-ladder-evidence.md
---

# feat-endpointer-profile — requirements

Track **Full**. Design `endpointer-profile` (`Planning~/design-docs/endpointer-profile.md`), locked at G1 2026-10-16, merged as PR #179. Depends on no feature.

## 1. User story

- **S1** — As a developer using VoXR in Continuous mode, I want a finished command to fire sooner after the player stops speaking, and to be able to go back to the model's own values with one choice, so that commands feel responsive without my grammar being split or reworded.
- **S2** — As the VoXR maintainer, I want the tuned values chosen by pause-ladder evidence on the joined rig, with every run naming the conf it decoded with, so that each latency claim traces to the profile that produced it.

## 2. Feature & scope

Backlog row (`endpointer-profile.md` §6):

> `endpointer-profile` | The pause-ladder corpus pinned by the harness, and each run recording the conf it decoded with (rule 9); the rule2 sweep on the joined rig with both buffer setups, rule3 and rule4 only on evidence, and both kill criteria (rule 8, adr-0004); named presets with the measured values (adr-0003); the profile resolved by listening mode with an explicit choice overriding, and the runtime overlay with its invariant (adr-0001); the Continuous default flip, `expectations.json` re-pinned to it through the harness, and the human-only check before G2 (adr-0002). **Full.** | None.

**Agreed at G0 on 2026-10-16** by the maintainer, in session.

**In**

1. The pause-ladder corpus — the same commands with silences of controlled length spliced after a complete-command prefix and mid-phrase — pinned by the harness (rule 8, adr-0004).
2. Each run records which conf it decoded with, as a dump-header field; the harness and the rig decode with exactly the conf the package would produce (rule 9). This is an edit to the test harness (`NativeBridge~/harness/main.cpp`); how the harness decodes with the overlaid conf is settled in the architecture doc.
3. The rule2 sweep on the joined rig, first `rule2 ∈ {0.5, 0.35, 0.25}`, timed with `bufferWindow` time-driven and with eager commit on; rule3 and rule4 move only on measured evidence, rule1 and rule5 never; both kill criteria apply — the rise in finals-per-utterance times the buffer window exceeding the endpointer time saved rejects the profile, and any change to transcript content rather than segmentation is an immediate stop (rules 2 and 8, adr-0004).
4. Named presets: "Model default", the archive's own values, plus tuned preset(s) carrying the measured values; never raw floats (adr-0003).
5. The profile resolved at model load: an explicit developer choice overrides; otherwise the tuned preset in Continuous mode and "Model default" in Push-to-talk. Delivered by a runtime overlay that rewrites only the endpoint trailing-silence keys of the extracted `conf/model.conf` on both the cached-serve and fresh-extraction paths, after validation and before the decoder is initialised; its invariant — the effective conf is a function of the archive's own conf and the chosen profile only, never of the cache's previous state (rules 3, 4 and 6, adr-0001).
6. The Continuous default flip, with `expectations.json` re-pinned to it through the harness, never hand-edited, and a human-only check before G2 (rule 7, adr-0002).

**Out**

1. R10, lattice width (`--lattice-beam`, `--max-active`) — a later topic.
2. Any change to the shipped bridge under `NativeBridge~/src/` or its ABI.
3. VAD (R16), eager-commit on partials (R17), retuning `bufferWindow`, and download-on-demand.
4. Product docs, including the `Documentation~/getting-started.md` :63 text — after G2, by the codex reconciler.
5. `release` and `lab-next-level-probes`, held by the human.

## 3. Why — the value it delivers

This is the largest latency cut available with no native code: the endpointer is where 500–1000 ms lives (`Planning~/research/2026-09-05-next-level/03-critique-and-ranking.md:232`; the maintainer's shortlist item 6, `Planning~/research/2026-09-05-next-level/05-rulings.md`). Continuous-mode players get sooner fires; the maintainer gets latency numbers that trace to the conf that produced them (design §5). No locked doc states a latency target (design §7).

## 4. Observable behaviour

1. Under the tuned default, a finished Continuous-mode command fires sooner after the speaker stops; Push-to-talk behaves exactly as before.
2. The developer chooses a named preset; "Model default" restores the archive's own values exactly; with no choice, the mode at model load decides (Continuous → tuned preset, Push-to-talk → "Model default"; no push-to-talk controller in the scene counts as Continuous).
3. At load, the resolved profile and its values are logged; a later listening-mode change takes effect at the next load.
4. Every harness dump and rig report names the conf it decoded with.
5. The pause-ladder corpus: set P — 3 commands, each with a silence of 0.2, 0.3, 0.4 and 0.6 s spliced right after a prefix that is itself a complete command in the test grammar (12 cases; each rung falls between the sweep points 0.25/0.35/0.5, so each rule2 value's split threshold shows); set M — 3 commands, each with a silence of 0.3, 0.6 and 0.9 s spliced mid-phrase at an incomplete grammar state (9 cases; straddling rule3's 0.75 s). 21 cases, synthesized by the existing generator's split-entry form (segments and a gap), as the existing `split` fixture is. The exact phrases are chosen in the architecture doc by these criteria.
6. The sweep result — per rule2 value, both buffer setups, both kill-criteria verdicts — is recorded in the architecture doc.

## 5. Functional requirements

**Ruled on 2026-10-16** by the maintainer, in session: fork 1, the human check, on-device on a Quest 3; fork 2, the ladder, the 21 cases as proposed; fork 3, the mode at load decides and is logged, no push-to-talk controller counting as Continuous; fork 4, kill criterion 1 must pass at both a 0.5 s and a 2.0 s window; the rest taken as drafted, the load-time log (F12) a Must under fork 3's ruling.

| #  | Requirement | Story | Priority | Acceptance check | Source |
|----|-------------|-------|----------|------------------|--------|
| F1 | The pause-ladder cases (§4.5) are in the fixture manifest and pinned in `NativeBridge~/harness/expectations.json`, written only by a harness run, never by hand. | S2 | Must | Harness green; the pin's diff comes from a harness run. | rule 8; adr-0004; rulings 2 and 10 |
| F2 | Each harness dump header and rig report names the conf it decoded with. | S2 | Must | The field is present; two presets give two different values. | rule 9 |
| F3 | The harness and the rig decode with exactly the conf the package would produce for a given archive and preset. | S2 | Must | Byte-equal confs. | rule 9 |
| F4 | The sweep runs on the 16 fixtures plus the ladder at the recogniser layer: rule2 ∈ {0.5, 0.35, 0.25}, with `bufferWindow` time-driven and with eager commit on; it records finals per utterance and end-of-audio→fire delay in sample time; kill criterion 1 (rise in finals per utterance × buffer window must not exceed the endpointer time saved) is evaluated at a 0.5 s window and at a 2.0 s window and must pass at both; kill criterion 2 (any change to transcript content rather than segmentation) stops the work; rule3 and rule4 move only on measured evidence, rule1 and rule5 never. | S2 | Must | The sweep record in the architecture doc with both setups and both verdicts. | rules 2 and 8; adr-0004; design §0 |
| F5 | Profiles are named presets: "Model default", the archive's own values, plus the tuned preset(s) carrying the measured values; nothing public takes raw floats. | S1 | Must | A test lists the presets; no public float input. | rule 5; adr-0003 |
| F6 | Resolution at model load: an explicit developer choice wins; otherwise Continuous → the tuned preset, Push-to-talk → "Model default"; no push-to-talk controller counts as Continuous. | S1 | Must | Tests over each case. | rule 6; adr-0002 |
| F7 | The overlay rewrites only the endpoint trailing-silence keys of the extracted `conf/model.conf`, on both the cached-serve and fresh-extraction paths, after validation and before decoder initialisation; every other line is byte-identical. | S1 | Must | A conf diff test. | rule 3; adr-0001 |
| F8 | The invariant: the effective conf is a function of the archive's own conf and the chosen profile only; a load under a tuned preset followed by a load under "Model default" gives the archive's conf exactly. | S1 | Must | Byte-equal to the archive's conf. | rule 4; adr-0001 |
| F9 | The R9 stamp keeps describing the archive, and a cached model stays served from the cache (the overlay causes no re-extraction). | S1 | Must | Stamp unchanged; cache hit after an overlaid load. | rule 4; adr-0001 |
| F10 | The Continuous default flips to the tuned preset; `expectations.json` is re-pinned to it through the harness; a CHANGELOG line records the behaviour change. | S1 | Must | The harness-made pin diff; the CHANGELOG entry. | rules 6 and 9; adr-0002 |
| F11 | The human-only check (§9) passes before G2; a fail blocks the flip. | S1 | Must | By hand at G2. | rule 7; adr-0002 |
| F12 | The resolved profile and its values are logged at model load. | S1 | Must | The log line is present. | rule 1; fork 3 ruling |

## 6. Non-functional requirements

- **Performance:** the overlay runs once per model load; no work on the per-frame or per-chunk path.
- **Robustness at the input boundary:** an archive conf whose endpoint keys cannot be parsed → the model loads with the archive's own conf unchanged and a warning names it; never a half-written conf. A conf missing a rule key receives it from a tuned preset; under "Model default" it is left as is.
- **Degraded dependencies:** none beyond the row above.

## 7. Non-goals / deferred

- The G0 Out list (§2).
- Re-applying the profile on a listening-mode change before the next load.
- The R5 human corpus.
- Ranking two decoders — the sweep chooses a profile, it does not rank decoders (C12, `Planning~/research/2026-09-05-next-level/03-critique-and-ranking.md:164-165`).

## 8. Dependencies & assumptions

- R9 (model cache stamp) and the joined rig (#177, #178) merged; no feature dependency.
- The piper generator (`Tests~/Fixtures/generate.py`, its venv by `generate.sh`) for the ladder; piper is not bit-deterministic, so the committed WAVs are the baseline.
- The Command 3 toolchain (harness, Windows-native); a Quest 3 for the human check.
- The `unity` binding's Command 3 green line names "16/16"; once the ladder is pinned it is edited to the new count by `doc-writer` within this feature.
- Assumed: the arm64 `.so` reads `conf/model.conf` as win64 vosk 0.3.45 does — confirmed by the human check (design §7).

## 9. Acceptance criteria (G2)

- [ ] Every Must in §5 (F1–F12) checked.
- [ ] Human-only verification, on a Quest 3, Continuous mode: (1) after a load under the tuned default, the extracted `conf/model.conf` pulled by adb shows the tuned values; (2) about 10 commands spoken naturally, a few with a hesitation, under the tuned default and then under "Model default": under tuned every command fires its intended intent with no extra split and no changed words (session log), and the maintainer judges that fires come sooner. A fail blocks the flip (adr-0002, option A).
- [ ] The `unity` binding's Commands 1, 2 and 3 green (Command 3 runs because the harness changes).

## 10. Open questions

1. The tuned presets' count, names and values — the tuning pass (adr-0003).
2. The ladder's exact phrases, by §4.5's criteria — the architecture doc.
3. The dump-header field's name and form, and how the harness decodes with the overlaid conf — the architecture doc.
4. How the overlay reads the archive's own conf and writes the keys — the architecture doc (adr-0001).
