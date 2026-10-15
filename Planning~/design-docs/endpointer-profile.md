---
type: system
topic: endpointer-profile
tags: [latency, decoder, endpointer, model-conf]
status: draft
updated: 2026-10-15
sources:
  - Planning~/research/2026-09-05-next-level/03-critique-and-ranking.md (R11, rank 6; R10 at :253)
  - Planning~/research/2026-09-05-next-level/05-rulings.md (rulings 2, 10; shortlist item 6)
  - "Session direction 2026-10-15 (G0)"
---

# Design: The Endpointer Profile via conf/model.conf

**Branch:** `design-endpointer-profile`
**Origin:** next-level plan item R11 (`03-critique-and-ranking.md` rank 6; Phase D).
**Status:** In design — opened at G0 2026-10-15; not locked.

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
