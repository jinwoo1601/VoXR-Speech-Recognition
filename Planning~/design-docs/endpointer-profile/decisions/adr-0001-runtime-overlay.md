---
id: adr-0001
title: Deliver the profile by a runtime overlay on the extracted conf, over an Editor rewrite of the archive, docs-only values, or native setters
status: accepted
date: 2026-10-16
topic: endpointer-profile
---

# adr-0001 — Deliver the profile by a runtime overlay

## The fork

The scope agreed at G0 (`Planning~/design-docs/endpointer-profile.md` §0, In 4) asks how a chosen profile reaches the decoder's `conf/model.conf`. The package passes vosk only the model path (`VoxrSpeechRecogniser.cs:252`; `vosk_bridge.cpp:237`, `:245`), and the model is the developer's: a vosk model `.zip` placed in `StreamingAssets` (`Documentation~/getting-started.md` :51), with download-on-demand a future feature (ruling 6, `05-rulings.md`). The runtime endpointer setters are declared in `include/vosk_api.h:33-35` but are not exported by the shipped arm64 binary (C2, `03-critique-and-ranking.md` :134-135), so `conf/model.conf` is the only route. Nothing writes into the extracted model but the R9 stamp, which records the archive (`ModelExtractor.cs:420-428`), and the product text says hand-edits to the extracted copy are invisible to the stamp (`Documentation~/getting-started.md` :63). Where and when the conf is changed decides whether it works for any model source, whether the stamp stays truthful, and whether R10 can reuse it, so every other rule of the design sits downstream of it (`Planning~/design-docs/endpointer-profile.md` §1, rules 3 and 4).

## Options considered

1. **(A) A runtime overlay.** On every launch, on both serve paths (cached and freshly extracted), after the extracted model is validated and before the decoder is initialised, the package rewrites only the endpoint trailing-silence keys of the extracted `conf/model.conf`. It works for any model source, and R10 can extend it. It costs the package writing into the cache, and the product text "hand-edits to the extracted copy are invisible" changes after G2.
2. **(B) An Editor tool that rewrites the conf inside the StreamingAssets zip.** R9's stamp detects the change, because the archive itself differs. It mutates the developer's asset and gives no coverage for downloaded models.
3. **(C) Docs only.** Publish the values; the developer edits the zip. Nothing is built, nothing is versioned or tested, and no default is possible.
4. **(D) Native setters** `vosk_recognizer_set_endpointer_mode` / `_delays`. They are not exported by the shipped arm64 binary (C2), so this would need a vosk rebuild.

## The decision

**Option (A): a runtime overlay that rewrites only the endpoint trailing-silence keys of the extracted `conf/model.conf` on every launch, on both serve paths, after validation and before decoder initialisation.** It commits the design to one invariant: the effective conf is always a function of the archive's own conf and the chosen profile, never of the cache's previous state, and the R9 stamp keeps describing the archive. It touches endpoint keys only (`Planning~/design-docs/endpointer-profile.md` §1, rules 3 and 4).

## Why the rejected branches lost

- **(B) Editor rewrite of the archive** — it fails downloaded models and edits the developer's asset. What would have to change for it to win: models were only ever StreamingAssets zips and the package owned them.
- **(C) Docs only** — it cannot carry a default (adr-0002). What would have to change for it to win: no default flip were wanted.
- **(D) Native setters** — the shipped binary closes it. What would have to change for it to win: a rebuilt vosk exporting the setters ships.

## Consequences

- The package writes into the cache, which today holds nothing but the extracted model and the R9 stamp; it does so on every launch, cached or freshly extracted.
- Returning to the model's own values restores them exactly, because the effective conf never depends on the cache's previous state.
- The overlay works for any model source, including the download-on-demand models of ruling 6.
- R10's lattice keys extend the overlay under their own design; this decision covers endpoint keys only.
- The product text "hand-edits to the extracted copy are invisible" (`Documentation~/getting-started.md` :63) changes after G2.
- Left open, for the architecture doc: how the overlay reads the archive's own conf and how it writes the keys.

## Status

Accepted on 2026-10-16 by the human's (the maintainer's) ruling in conversation. The design doc is `Planning~/design-docs/endpointer-profile.md`; it was locked at G1 on 2026-10-16.
