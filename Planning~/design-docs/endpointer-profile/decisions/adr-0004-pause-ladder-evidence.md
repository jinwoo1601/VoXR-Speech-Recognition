---
id: adr-0004
title: Choose the profile's values on a pause-ladder corpus and a human confirm, over the 16 TTS fixtures alone or waiting for the R5 pilot
status: accepted
date: 2026-10-16
topic: endpointer-profile
---

# adr-0004 — A pause-ladder corpus plus a human confirm as the sweep's evidence

## The fork

The scope agreed at G0 (`Planning~/design-docs/endpointer-profile.md` §0, In 1 and 2) has the sweep choose the endpointer values on the joined rig, read from the recogniser layer's *Timing* measures: finals per utterance and end-of-audio→fire delay, in sample time. What the sweep runs on decides whether it can see the cost the endpointer trades for latency. The existing corpus is 16 fixtures, all TTS (lessac), with one `split` case, and fluent speech carries no hesitations, so lower values look free on it (§3, "Fluent audio flatters low values"). The measured delay also depends on the buffer: a time-driven `bufferWindow` waits its full window whatever the endpointer does, while with eager commit the endpointer is the dominant term (§1; §3, "The buffer masks the saving").

## Options considered

1. **(A) The 16 TTS fixtures only.** They exist, and the harness pins them.
2. **(B) A pause-ladder corpus beside the 16 fixtures, plus a human confirm before the default flips.** The pause-ladder corpus is the same commands with silences of controlled length spliced after a complete-command prefix and mid-phrase.
3. **(C) Wait for the R5 human pilot,** which has not started.

## The decision

**Option (B): a pause-ladder corpus drives the sweep beside the 16 TTS fixtures, and a human confirm precedes the default flip; timing is measured with `bufferWindow` time-driven and with eager commit on.** It commits the design to new fixtures and manifests (`Planning~/design-docs/endpointer-profile.md` §1, rule 8; the human confirm is the human-only check of rule 7, adr-0002).

## Why the rejected branches lost

- **(A) The 16 fixtures only** — with one split case and fluent speech they cannot see the split cost the endpointer trades for latency. What would have to change for it to win: the decision record gives the reason it lost and nothing more.
- **(C) Wait for the R5 pilot** — R5 is not started, so R11 would wait indefinitely. What would have to change for it to win: the pause-ladder and the human check disagree.

## Consequences

- New fixtures and manifests are added; their expectations are pinned by running the harness, never by hand (rulings 2 and 10).
- C12 still holds: the sweep chooses a profile; it does not rank two decoders (`03-critique-and-ranking.md` :164-165).
- Timing is measured in both buffer setups, so a saving measured one way is not claimed for the other.
- The two kill criteria (`Planning~/design-docs/endpointer-profile.md` §0) are read off the pause-ladder sweep before the flip.
- Left open: the pause-ladder's silence lengths, the commands it uses and its count of cases; and the tuned presets' values the sweep produces (adr-0003).

## Status

Accepted on 2026-10-16 by the human's (the maintainer's) ruling in conversation. The design doc is `Planning~/design-docs/endpointer-profile.md`; it was locked at G1 on 2026-10-16.
