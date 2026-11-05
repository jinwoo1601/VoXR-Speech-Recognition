---
id: adr-0003
title: Compile only AGC2's classes from pinned webrtc-audio-processing 2.1 source through our CMake, over building the whole library behind its public API
status: accepted
date: 2026-11-05
topic: agc-filter
---

# adr-0003 — AGC2 classes from pinned source

## The fork

The scope agreed at G0 brings the WebRTC APM dependency in as source into the `NativeBridge~` build, BSD-3 licensed, its cost counted against the 70 MB APK ceiling, and leaves how it is vendored to the design (`Planning~/design-docs/agc-filter.md` §0, In 3; ruling 6, `05-rulings.md` :57). The freedesktop `webrtc-audio-processing` 2.1 package builds with meson only, is "tested on Linux only", is C++17, and pulls abseil ≥ 20240722 through a meson wrap; no maintained CMake port was found (`Planning~/design-docs/agc-filter/recon-2026-11-05.md`, WebRTC APM research). The bridge builds with CMake through the NDK for arm64-v8a, with hidden visibility (`Planning~/design-docs/agc-filter/recon-2026-11-05.md`, Code map). The voice detector's speech probability, which R16 will need later, is not exposed by APM's public API; only an internal wrapper returns it (`Planning~/design-docs/agc-filter/recon-2026-11-05.md`, WebRTC APM research). How the dependency is brought in decides its size, its build, and whether R16 can reach the speech probability without a second dependency decision (`Planning~/design-docs/agc-filter.md` §1, rule 9).

## Options considered

1. **(a) Pin the source and compile only the AGC2 subset.** Pin the freedesktop `webrtc-audio-processing` 2.1 source by SHA-256, as libvosk is pinned (`Planning~/design-docs/agc-filter/recon-2026-11-05.md`, Code map), with its abseil; compile only the AGC2 subset — the gain controller, the RNN voice detector, the limiter and `common_audio` — through our CMake, statically, with hidden symbols. The speech probability is reachable for R16 later. It costs building on internal classes, which carry no API promise, and a version bump re-pins.
2. **(b) The whole library through the public `AudioProcessing::Config`.** The supported interface; the whole 2.1 package is about 1.42 MB installed on x86_64 (`Planning~/design-docs/agc-filter/recon-2026-11-05.md`, WebRTC APM research). It brings more code, and needs meson or a CMake port.

## The decision

**Option (a): the freedesktop `webrtc-audio-processing` 2.1 source and its abseil, pinned by SHA-256, with only the AGC2 subset compiled through our CMake, statically, with hidden symbols.** It commits the design to AGC2's internal classes rather than APM's public API, and keeps the speech probability reachable for R16 (`Planning~/design-docs/agc-filter.md` §1, rule 9).

## Why the rejected branches lost

- **(b) The whole library through the public API** — more code, a meson build or a CMake port to carry, and the public API does not expose the speech probability R16 needs. What would have to change for it to win (proposed, for the human at G1): it wins if the AGC2 subset cannot be cut cleanly from the source, if another APM module (e.g. AEC3) enters scope, or if upstream exposes the speech probability publicly.

## Consequences

- The design builds on internal classes with no API promise; a version bump re-pins the source and re-checks the subset.
- The source and abseil download is a human step.
- The NDK and MSVC builds of the subset are unverified, and the arm64 `.so` growth is unmeasured (estimated ~1–2 MB, `R4-frontend-robustness-personalization.md` :321-322; ceiling 70 MB, today ~49 MB, `05-rulings.md` :55, :57); the first architecture phase proves both (`Planning~/design-docs/agc-filter.md` §6).
- Abseil on Android carries a known duplicate-symbol (ODR) risk, and abseil version drift has needed patches downstream (`Planning~/design-docs/agc-filter/recon-2026-11-05.md`, WebRTC APM research); pinning abseil with the source brings that risk into this build.
- Licences: WebRTC is BSD-3 with a PATENTS grant that terminates on patent litigation against WebRTC; the RNN voice detector's weights come from rnnoise, BSD-3; abseil is Apache-2.0; pffft and abseil were not read from source (`Planning~/design-docs/agc-filter/recon-2026-11-05.md`, WebRTC APM research).
- AGC2's internal constants — the voice detector's threshold, the estimators' time constants — are unread (`Planning~/design-docs/agc-filter/recon-2026-11-05.md`, WebRTC APM research).
- Left open, for the architecture doc: the exact file list of the subset and its CMake.

## Status

Accepted on 2026-11-05 by the human's (the maintainer's) ruling in conversation. The design doc is `Planning~/design-docs/agc-filter.md`; it is in design, not locked.
