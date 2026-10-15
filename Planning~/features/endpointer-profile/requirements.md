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
