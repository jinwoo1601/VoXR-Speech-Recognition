---
id: adr-0002
title: Score the parser layer first and the recogniser layer in a later phase, over staging the recogniser now or scoring the parser only
status: accepted
date: 2026-10-13
topic: joined-rig
---

# adr-0002 — Score the parser layer first, then the recogniser layer

## The fork

This is the keystone. The recogniser decides what a player gets: `Runtime/Commands/VoxrCommandRecogniser.cs` is a 2,072-line `MonoBehaviour` driven by `Time.time`, and it owns the buffer window (split-utterance merging), eager flush, pending follow-ups and cooldown. The parser rig reads text only — no words or confidences are passed (`Planning~/features/coverage-in-selection/ab-rig/` `Program.cs:304`) — and the only intent/slot check today, `Runtime/Testing/VoxrBatchTestRunner.cs`, matches intent and slots exactly (`:208`, `:303-329`). Among the 16 fixtures (`Tests~/Fixtures/audio/manifest.json`), the split case expects one command across two finals, which only the recogniser can resolve. Which layer the joined rig scores decides which gated items it can judge and how soon: R1 gates R10, R11, R12, R13, R14's acceptance, R21, R22 and both R41 screens (`Planning~/research/2026-09-05-next-level/03-critique-and-ranking.md`, line 175).

## Options considered

1. **(a) Phased: the parser first, then the recogniser.** Phase 1 sends each final to `Parse(text, words)` and scores its outcome. The dump already carries partials and timestamps (adr-0001), so a later consumer can drive the staged recogniser on audio time; phase 2 is a separate feature that follows phase 1.
2. **(b) The recogniser level now**, by staging `VoxrCommandRecogniser` with a stubbed `MonoBehaviour` and `Time`.
3. **(c) The parser level only.**

## The decision

**Option (a): phase 1 scores the parser layer — each final to `Parse(text, words)` — and phase 2, a separate feature that follows, scores the recogniser layer by driving the staged recogniser on audio time from the dump.** It commits the design to a dump that carries partials and timestamps from the start, and to marking the cases only the recogniser can resolve rather than scoring them as misses at phase 1 (rule 11, adr-0005). How phase 2 stubs `MonoBehaviour` and `Time` is the architecture doc's.

## Why the rejected branches lost

- **(b) The recogniser level now** — it is the largest staging job and would delay every gated item. What would have to change for it to win: the decision record gives the reason it lost and nothing more.
- **(c) The parser level only** — R11 and R17 could never be judged by the rig. What would have to change for it to win: the decision record gives the reason it lost and nothing more.

## Consequences

- Accepted cost: R11 and R17 wait for phase 2; phase 2 must land before the rig can judge them.
- The rig passes words to the parser (`Parse(text, words)`), where today it passes text only (`Program.cs:304`).
- The dump must carry partials and audio-time stamps even though phase 1 reads only the finals, so phase 2 can consume the same dumps.
- Recogniser-layer cases, such as the split fixture, are tagged in the manifest and reported at phase 1 as "not scored at this layer", never as misses (rule 11, adr-0005); the R5 and R4 manifests must carry that mark.
- Phase 2 recogniser staging is handed off to its own feature; how it stubs `MonoBehaviour` and `Time` is left to the architecture doc.

## Status

Accepted on 2026-10-13 by the human's ruling in conversation. The design doc (`Planning~/design-docs/joined-rig.md`) was locked at G1 on 2026-10-13 and re-locked the same day with Amendment A1 (adr-0006).
