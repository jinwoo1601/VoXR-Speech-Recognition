---
type: system
topic: endpointer-profile
tags: [latency, decoder, endpointer, model-conf]
status: draft
updated: 2026-10-16
sources:
  - Planning~/research/2026-09-05-next-level/03-critique-and-ranking.md (R11, rank 6; R10 at :253)
  - Planning~/research/2026-09-05-next-level/05-rulings.md (rulings 2, 10; shortlist item 6)
  - Planning~/design-docs/joined-rig.md (rules 2, 10)
  - Planning~/features/joined-rig-recogniser/architecture.md (Timing measures)
  - Planning~/design-docs/endpointer-profile/decisions/adr-0001-runtime-overlay.md
  - Planning~/design-docs/endpointer-profile/decisions/adr-0002-default-flip-continuous-gated.md
  - Planning~/design-docs/endpointer-profile/decisions/adr-0003-named-presets.md
  - Planning~/design-docs/endpointer-profile/decisions/adr-0004-pause-ladder-evidence.md
  - "Session direction 2026-10-15 (G0)"
  - "Session direction 2026-10-16 (settle)"
---

# Design: The Endpointer Profile via conf/model.conf

**Branch:** `design-endpointer-profile`
**Origin:** next-level plan item R11 (`03-critique-and-ranking.md` rank 6; Phase D).
**Status:** **G1 LOCKED 2026-10-16.** Rulings in adr-0001 through adr-0004. This document is immutable from here; a contradiction found during implementation reopens it on a new design branch and re-locks.

**Scope, agreed at G0 (2026-10-15):** choose the endpointer values in `conf/model.conf` (which trailing-silence rules change, and to what) by a sweep on the joined rig, whose first sweep is `rule2 ∈ {0.5, 0.35, 0.25}`; read the measures from the joined rig's recogniser layer (`Planning~/features/joined-rig-recogniser/architecture.md`, its *Timing* measures) — finals per utterance and end-of-audio→fire delay, in sample time, since the harness pushes faster than real time; adopt the kill criteria below as scope; define the delivery route, meaning how a chosen profile reaches a developer's `conf/model.conf` given that the package bundles no model (the developer places a vosk model `.zip` in `StreamingAssets`, `Documentation~/getting-started.md` :51) and how that route meets the R9 cache stamp (hand-edits to the extracted copy are invisible to the stamp, `Documentation~/getting-started.md` :63); and state the standing rule "re-pin, never edit" (rulings 2 and 10), so that any moved acoustic baseline is re-pinned by running the harness and never hand-edited. The kill criteria: if, on the joined rig, the rise in finals-per-utterance multiplied by the buffer window exceeds the endpointer time saved, the profile is a mean-latency regression and is rejected; any change to transcript *content* rather than segmentation is an immediate stop.

> **What's decided going in.** The human (the maintainer) ruled at G0 on 2026-10-15: the work runs on the **design track**, branch `design-endpointer-profile`, **R11 alone**, with the scope above agreed as proposed; R10 stays out, a later topic. The item is needed because it is the largest latency cut available with no native code: the mechanism is corroborated in the binary — `strings` on the arm64 `libvosk.so` contains `/conf/model.conf` and vosk's hardcoded V1 defaults `--endpoint.rule2.min-trailing-silence=0.5`, `--endpoint.rule3.min-trailing-silence=1.0` and `--endpoint.rule4.min-trailing-silence=2.0` (`03-critique-and-ranking.md` :122) — and `conf/model.conf` is the only route because the runtime setters are not exported (C2). It unblocks nothing but it is where 500–1000 ms lives (`03-critique-and-ranking.md` :232). The kill criteria: if, on the joined rig, the rise in finals-per-utterance multiplied by the buffer window exceeds the endpointer time saved, the profile is a mean-latency regression and is rejected; any change to transcript *content* rather than segmentation is an immediate stop.
>
> **Non-goals:** R10, lattice width (`--lattice-beam`, `--max-active`), deferred to immediately behind R11 as a later topic; any native-code or bridge ABI change; VAD (R16); eager-commit on partials (R17); ranking two decoder configurations on TTS-only audio (standing rule, critique C12) (see §0).

---

## 0. Scope (agreed at G0)

**In**

1. The endpointer values: which trailing-silence rules in `conf/model.conf` change, and to what, chosen by a sweep on the joined rig (rank 6 names the first sweep: `rule2 ∈ {0.5, 0.35, 0.25}`).
2. The measures, read from the joined rig's recogniser layer (`Planning~/features/joined-rig-recogniser/architecture.md`, its *Timing* measures): finals per utterance and end-of-audio→fire delay, in sample time, since the harness pushes faster than real time.
3. The kill criteria below, adopted as scope.
4. The delivery route: how a chosen profile reaches a developer's `conf/model.conf`, given that the package bundles no model (the developer places a vosk model `.zip` in `StreamingAssets`, `Documentation~/getting-started.md` :51), and how that route meets the R9 cache stamp (hand-edits to the extracted copy are invisible to the stamp, `Documentation~/getting-started.md` :63: "Tune the `.zip` in `StreamingAssets` instead.").
5. The standing rule "re-pin, never edit" (rulings 2 and 10): any moved acoustic baseline is re-pinned by running the harness, never hand-edited.

**Kill criteria**

- If, on the joined rig, the rise in finals-per-utterance multiplied by the buffer window exceeds the endpointer time saved, the profile is a mean-latency regression and is rejected.
- Any change to transcript *content* rather than segmentation is an immediate stop.
- Cheapest first experiment: the first sweep rank 6 names, `rule2 ∈ {0.5, 0.35, 0.25}`.

**Out**

- R10, lattice width (`--lattice-beam`, `--max-active`): deferred to immediately behind R11 as its co-tenant, "same file, must be swept one knob at a time, and its CPU cost is unmeasured on Quest" (`03-critique-and-ranking.md` :253), a later topic.
- Any native-code or bridge ABI change.
- VAD (R16).
- Eager-commit on partials (R17).
- Ranking two decoder configurations on TTS-only audio (standing rule, critique C12).

**Prerequisites**

- R9, the model cache version key: merged.
- R1, the joined rig: merged, PRs #177 and #178.

---

This system is the endpointer profile: the set of endpoint trailing-silence values the vosk decoder runs with, resolved when the model loads and delivered by rewriting those keys in the extracted model's `conf/model.conf`. It sits between the R9 model cache, whose stamp describes the developer's model archive, and the decoder, and it is measured on the joined rig's recogniser layer (`Planning~/design-docs/joined-rig.md`; `Planning~/features/joined-rig-recogniser/architecture.md`, its *Timing* measures). It resolves four forks, recorded as adr-0001 through adr-0004: how a profile reaches the decoder (adr-0001), what the default is (adr-0002), what shape a profile takes (adr-0003), and what evidence chooses its values (adr-0004). One constraint governs everything below: the runtime endpointer setters are not exported by the shipped binary (C2, `03-critique-and-ranking.md` :134-135), so `conf/model.conf` is the only route. No locked design doc states a latency target (200–300 ms appears only in research, `ideas/I3-latency-ux-redesign.md` :41 and `research/R3-kws-slu-endpointing-latency.md` :18); the goal this system carries is the maintainer's shortlist item 6 (`05-rulings.md`) and rank 6's rationale, that the endpointer is where 500–1000 ms lives (`03-critique-and-ranking.md` :232).

> **What's decided.** The profile is the set of endpoint trailing-silence values the decoder runs with, resolved at model load; rule2 moves first, rule3 and rule4 only on measured evidence, rule1 and rule5 never. The package delivers it as a runtime overlay that rewrites only the endpoint trailing-silence keys of the extracted `conf/model.conf` on every launch, cached or freshly extracted, so the effective conf is always a function of the archive's own conf and the chosen profile, and the R9 stamp keeps describing the archive (adr-0001). The default flips in Continuous mode to the tuned preset and stays the model's own values in Push-to-talk, with an explicit developer choice overriding both; the flip is gated before G2 on the pause-ladder sweep, both kill criteria and a human-only check, and the standing baseline pins the Continuous default, the tuned preset, re-pinned through the harness at merge and never edited, the archive's own conf staying measurable as side A of a decoder-side run (adr-0002). A profile is a named preset — "Model default", the archive's own values, plus tuned preset(s) — never raw floats (adr-0003). A pause-ladder corpus drives the sweep beside the 16 TTS fixtures, timed with `bufferWindow` time-driven and with eager commit on (adr-0004). All numbers on this page are **provisional → the tuning pass** (the design intent is the load-bearing part).

## 1. The model

**The facts it rests on** (recon of 2026-10-15).

- The package passes vosk only the model path (`VoxrSpeechRecogniser.cs:252`; `vosk_bridge.cpp:237`, `:245`). vosk reads `conf/model.conf` through Kaldi option parsing; a rule the conf omits falls to Kaldi's default.
- The shipped model's own conf sets the rule2, rule3 and rule4 trailing silences to 0.5, 0.75 and 1.0 s (`02-verified-facts.md` :21-34). vosk's hardcoded 0.5/1.0/2.0 apply only to models without a conf (`03-critique-and-ranking.md` :122). rule1 (5 s with no speech) and rule5 (20 s of utterance length) are Kaldi defaults.
- An endpoint is declared when any rule holds. rule2 needs the grammar's best state to be a complete sentence (relative cost ≤ 2.0); rule3 needs a relative cost ≤ 8.0; rule4 needs nothing of the grammar. A finished command normally satisfies rule2, so rule2 is the term on a normal command's path; rule3 and rule4 govern pauses in the middle of a command.
- After an endpoint the decoder restarts at the grammar's start: a split's second fragment is decoded with no grammar left context and is forced onto a complete grammar path on its own (inferred from vosk's `recognizer.cc`). A split can change words, not only boundaries.
- In Push-to-talk, release forces the final (`vosk_bridge.cpp:175`); there the endpointer can only split a press.
- `bufferWindow` (0.5 s by default, `VoxrCommandRecogniser.cs:72`; 2.0 s advised on Quest 3, its tooltip `:65-71`) rejoins finals, and each final restarts its wait (`UtteranceBuffer.cs:159-162`). Time-driven, it adds its full wait; with eager commit, the endpointer is the dominant term.
- The runtime endpointer setters are declared in `include/vosk_api.h:33-35` but are not exported by the shipped binary (C2, `03-critique-and-ranking.md` :134-135).
- Nothing writes into the extracted model but the R9 stamp, which records the archive (`ModelExtractor.cs:420-428`); an extracted model is valid only with a `conf/model.conf` (`ModelExtractor.cs:257`).
- The harness takes any model directory and has no conf flag (`NativeBridge~/harness/main.cpp:174`, `:181-182`); its dump header records `model`, `gain`, `libvosk`, `grammar`, `ringCapacity` and `decoderRate`, not the conf (`main.cpp:258-269`).

**The endpoint rules, as the shipped model runs them.**

| Rule | Fires after | Needs of the grammar's best state | Shipped model's value | The profile |
|---|---|---|---|---|
| rule1 | 5 s with no speech | — | Kaldi default | never moved |
| rule2 | trailing silence | a complete sentence (relative cost ≤ 2.0) | 0.5 s | moved first |
| rule3 | trailing silence | relative cost ≤ 8.0 | 0.75 s | moved only on measured evidence |
| rule4 | trailing silence | nothing | 1.0 s | moved only on measured evidence |
| rule5 | 20 s of utterance | — | Kaldi default | never moved |

**The rules** (design intent, not yet built).

| # | Rule | Settled | ADR |
|---|---|---|---|
| 1 | What the profile is | The set of endpoint trailing-silence values the decoder runs with, resolved at model load. A later change reaches the decoder only at the next load. | — |
| 2 | Which rules move | rule2 first; rule3 and rule4 only on measured evidence; rule1 and rule5 never. | — |
| 3 | Delivery | A runtime overlay. On every launch, on both the cached-serve and the fresh-extraction paths, after the extracted model is validated and before the decoder is initialised, the package rewrites only the endpoint trailing-silence keys of the *extracted* `conf/model.conf`. Endpoint keys only; R10 extends the overlay under its own design. The overlay works for any model source. | adr-0001 |
| 4 | The overlay invariant | The effective conf is always a function of the archive's own conf and the chosen profile, never of the cache's previous state, so returning to the model's own values restores them exactly. The R9 stamp keeps describing the archive. | adr-0001 |
| 5 | Profile shape | Named presets: "Model default", the archive's own values, plus tuned preset(s). No raw floats. The count, names and values of the tuned presets are provisional → the tuning pass. | adr-0003 |
| 6 | Default | This feature flips the default. In Continuous mode the default is the tuned preset; in Push-to-talk it stays the model's own values; an explicit developer choice overrides both. | adr-0002 |
| 7 | The gate on the flip | The flip is gated before G2 on the pause-ladder sweep, both kill criteria (§0), and a human-only check, on-device or a recorded listen. It is not gated on the full R5 corpus. | adr-0002 |
| 8 | Evidence | A pause-ladder corpus — the same commands with silences of controlled length spliced after a complete-command prefix and mid-phrase — drives the sweep beside the 16 TTS fixtures. Timing is measured with `bufferWindow` time-driven and with eager commit on. | adr-0004 |
| 9 | Instrument coupling | The harness and the rig decode with exactly the conf the package would produce. The standing baseline pins the Continuous default, the tuned preset, and is re-pinned through the harness at merge, never edited (rulings 2 and 10); the archive's own conf stays measurable as side A of a decoder-side run. Each run records which conf it decoded with; today the dump header does not, and the feature `endpointer-profile` builds that. | adr-0002 |

**One model load, resolved** (design intent, not yet built).

1. The profile is resolved: an explicit developer choice if there is one; otherwise the tuned preset in Continuous mode and "Model default" in Push-to-talk (rule 6).
2. The model is served from the R9 cache or freshly extracted; the stamp, which describes the archive, decides which (rule 4).
3. The extracted model is validated.
4. The overlay rewrites the endpoint trailing-silence keys of the extracted `conf/model.conf` from the archive's own conf and the chosen profile, and nothing else (rules 3 and 4).
5. The decoder is initialised and reads the conf; the profile holds until the next load (rule 1).

## 2. The decisions it puts to its operator

The operator is the developer who integrates the package.

1. **Continuous or Push-to-talk.** In Push-to-talk, release forces the final, so the endpointer never ends a command sooner and can only split a press: a tuned preset there buys no responsiveness and pays split risk, which is why the default there stays the model's own values (rule 6, adr-0002). In Continuous mode the endpointer sits on every command's path, so the profile is where the saving is. Neither mode dominates: Push-to-talk spends a press to make the end of a command exact; Continuous stays hands-free and carries the split risk.
2. **Responsiveness or split tolerance.** A tighter rule2 ends a finished command sooner, but it also ends an utterance at a pause after any complete-command prefix. Which side wins depends on the game's grammar: commands that are prefixes of other commands split under a tight rule2, and a split costs a rejoin wait and can change words (§1). The developer knows the grammar; the evidence behind the tuned preset comes from the pause-ladder corpus and the fixtures, not from that grammar (rule 8, adr-0004).
3. **"Model default" or a tuned preset.** "Model default" runs the archive's own values exactly and is always available (rule 5, adr-0003); a tuned preset ends commands sooner in Continuous mode on the package's evidence, not the developer's. The explicit choice overrides the default in either mode (rule 6, adr-0002).

## 3. Dynamics & failure modes

**Forward.** In Continuous mode a finished command is normally ended by rule2, so lowering rule2 moves the end of a normal command earlier by at most the amount it is lowered. Whether the player sees that depends on the buffer: with `bufferWindow` time-driven, the command still waits the full window after its last final, which can mask the saving; with eager commit, the endpointer is the dominant term and the saving shows. A pause longer than rule2 after a complete-command prefix now ends the utterance; pauses mid-phrase, at an incomplete grammar state, stay governed by rule3 and rule4 unless evidence moves them. Each extra split is paid twice, once by the endpointer and once by the buffer (I1-P7b, `ideas/I1-recognition-core-redesign.md` :152-168): the first kill criterion weighs the rise in finals per utterance times the buffer window against the time saved. Its second fragment is decoded with no left context and can come out as different words: the second kill criterion stops on that. In Push-to-talk under the default, nothing changes.

**Attacks on the rules, and what kills each.**

| Failure mode | What it looks like | Killed by |
|---|---|---|
| Fluent audio flatters low values | TTS commands carry no hesitations, so lower values look free on the 16 fixtures. | The pause-ladder corpus (rule 8, adr-0004) and the human-only check on the flip (rule 7, adr-0002). |
| "Lower every rule" | rule3 and rule4 cut alongside rule2 for a bigger saving, splitting pauses in the middle of commands. | Rule 2: rule2 first; rule3 and rule4 only on measured evidence; rule1 and rule5 never. |
| Prefix-heavy grammars split | A game whose commands are prefixes of other commands splits under a tight rule2. | Developer-facing presets, with "Model default" always available (rule 5, adr-0003), and the explicit choice (rule 6). |
| Splits change words | A second fragment, decoded with no grammar left context, is forced onto a complete grammar path and comes out as different words. | The content-change kill criterion: any change to transcript content rather than segmentation is an immediate stop (§0). |
| The buffer masks the saving | A time-driven `bufferWindow` waits its full window whatever the endpointer does, so a saving measured one way is claimed for the other. | Measuring both buffer setups, time-driven and with eager commit on (rule 8, adr-0004). |
| Stale tuned values | A developer switches back to "Model default", but the cached conf still carries the tuned values. | The overlay invariant (rule 4, adr-0001). |
| Push-to-talk pays for nothing | Tuned values in Push-to-talk add split risk, while release already ends the command. | The Continuous-only default (rule 6, adr-0002). |

## 4. Boundaries & couplings

- **Owns:** the profile and its presets; its resolution at model load; the overlay of the endpoint trailing-silence keys and its invariant; the default.
- **Consumes:** the R9 extractor and its stamp, which keeps describing the archive; the joined rig's recogniser-layer *Timing* measures — finals per utterance and end-of-audio→fire delay in sample time, decode latency not modelled (`Planning~/features/joined-rig-recogniser/architecture.md` :69, :150); the joined rig's one-side-varies rule and its re-pin discipline (`Planning~/design-docs/joined-rig.md` rules 2 and 10).
- **Hands off:** recording which conf a run decoded with, to the feature `endpointer-profile` (rule 9).
- **Does not decide:** R10's lattice keys, which extend the overlay under their own design (I3-P2 proposes building the rewrite once for both, `ideas/I3-latency-ux-redesign.md` :88); retuning `bufferWindow`; eager commit (R17); VAD (R16); download-on-demand (ruling 6), beyond the overlay working for any model source; native setters.

## 5. What it delivers

In Continuous mode, a command the player has finished ends at the tuned preset's rule2 rather than the model's own 0.5 s, on the path where 500–1000 ms lives (`03-critique-and-ranking.md` :232) — the goal of the maintainer's shortlist item 6 (`05-rulings.md`). A developer whose grammar cannot take it keeps "Model default" with one choice, and switching back restores the model's own values exactly. Every moved baseline is re-pinned by the harness, and every run names the conf it decoded with, so a number from the rig traces to the profile that produced it.

When it goes right, a Continuous-mode command fires sooner after the speaker stops; the rise in finals per utterance times the buffer window stays below the time saved; no transcript changes words; and Push-to-talk behaves exactly as before.

## 6. Feature backlog — locked at G1 (2026-10-16)

One feature. It is **Full**: the design leaves the pause-ladder corpus, the overlay mechanics, the conf record in the dump and the preset values to the requirements and architecture docs, so the build writes its own.

| Feature | Scope | Depends on |
|---|---|---|
| `endpointer-profile` | The pause-ladder corpus pinned by the harness, and each run recording the conf it decoded with (rule 9); the rule2 sweep on the joined rig with both buffer setups, rule3 and rule4 only on evidence, and both kill criteria (rule 8, adr-0004); named presets with the measured values (adr-0003); the profile resolved by listening mode with an explicit choice overriding, and the runtime overlay with its invariant (adr-0001); the Continuous default flip, `expectations.json` re-pinned to it through the harness, and the human-only check before G2 (adr-0002). **Full.** | None. |

## 7. Status / openness

- **Status: locked at G1 2026-10-16.** The model was settled with the human (the maintainer) on 2026-10-15 and 2026-10-16; its forks are recorded as four ADRs, adr-0001 through adr-0004.
- **The goal:** no locked design doc states a latency target (200–300 ms appears only in research, `ideas/I3-latency-ux-redesign.md` :41 and `research/R3-kws-slu-endpointing-latency.md` :18). The goal is the maintainer's shortlist item 6 (`05-rulings.md`) and rank 6's rationale (`03-critique-and-ranking.md` :232).
- **All numbers provisional → the tuning pass:** the tuned presets' count, names and values; the first sweep, `rule2 ∈ {0.5, 0.35, 0.25}` (rank 6); any rule3 or rule4 value, moved only on measured evidence.
- **Hard dependencies / open:**
  - The tuned presets' values and count (adr-0003, adr-0004) — owner: the tuning pass.
  - Whether the listening mode can change after model load: the profile is resolved at load, so a later mode change does not re-apply it until the next load — requirements to settle — owner: the requirements of `endpointer-profile`.
  - The human-only check's exact form, on-device or a recorded listen (adr-0002) — owner: the requirements of `endpointer-profile`.
  - The pause-ladder's silence lengths, the commands it uses and its count of cases (adr-0004) — owner: the requirements of `endpointer-profile`.
  - The dump-header field that records the conf a run decoded with — owner: the feature `endpointer-profile` (rule 9).
  - The product-doc text that hand-edits to the extracted copy are invisible to the stamp (`Documentation~/getting-started.md` :63) — to change after G2 — owner: the G2 product-docs pass of `endpointer-profile`.
  - The arm64 `.so`'s vosk version is unconfirmed; the harness runs win64 vosk 0.3.45; both read the same conf — owner: the on-device human check of `endpointer-profile`, which confirms the device honours the overlaid conf.

## 8. Related

`Planning~/design-docs/endpointer-profile/decisions/adr-0001-runtime-overlay.md` · `…/adr-0002-default-flip-continuous-gated.md` · `…/adr-0003-named-presets.md` · `…/adr-0004-pause-ladder-evidence.md` · `Planning~/design-docs/joined-rig.md` (rules 2, 10) · `Planning~/features/joined-rig-recogniser/architecture.md` (*Timing* measures, `:69`, `:150`) · `Planning~/research/2026-09-05-next-level/03-critique-and-ranking.md` (`:122`; C2, `:134-135`; rank 6, `:232`; R10, `:253`) · `…/05-rulings.md` (rulings 2, 6, 10; shortlist item 6) · `…/02-verified-facts.md` (`:21-34`) · `…/ideas/I3-latency-ux-redesign.md` (I3-P2, `:80-98`) · `…/ideas/I1-recognition-core-redesign.md` (I1-P7b, `:152-168`) · `Documentation~/getting-started.md` (`:51`, `:63`) · `NativeBridge~/harness/main.cpp` (`:258-269`) · `NativeBridge~/src/vosk_bridge.cpp` (`:175`)
