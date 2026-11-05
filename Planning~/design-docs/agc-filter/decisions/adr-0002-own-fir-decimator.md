---
id: adr-0002
title: Decimate with our own DC-normalised long FIR and run APM at 16 kHz in and out, over letting APM resample 48→16 kHz
status: accepted
date: 2026-11-05
topic: agc-filter
---

# adr-0002 — Our own FIR decimator, APM at 16 kHz

## The fork

The scope agreed at G0 (`Planning~/design-docs/agc-filter.md` §0, In 1) gives the 48→16 kHz downsampler a proper anti-alias low-pass. Today's 15-tap filter is "a 3 kHz shelf": −3.3 dB at 3 kHz, −6.1 dB at 4 kHz, −16.0 dB at 6 kHz, −25.9 dB at 7 kHz and −36.7 dB at 7.5 kHz, with +2.51 dB of DC gain and ~29 dB of alias rejection (`R4-frontend-robustness-personalization.md` :128-130; `03-critique-and-ranking.md` :62-67; `04-synthesis.md` :32), while the model's `mfcc.conf` uses the band up to 7.6 kHz (`R4-frontend-robustness-personalization.md` :132-135). AGC2 can take 48 kHz in and give 16 kHz out, resampling inside APM (`Planning~/design-docs/agc-filter/recon-2026-11-05.md`, WebRTC APM research). So who decimates is open: a filter of our own ahead of AGC2, or APM's own resampler. The filter's response is what every score downstream depends on (`R4-frontend-robustness-personalization.md` :164-167), and it decides whether the R14 half of the unit can be measured on its own terms (`Planning~/design-docs/agc-filter.md` §1, rules 1 and 2).

## Options considered

1. **(a) Our own DC-normalised long FIR in place of the 15-tap table.** The R4 candidate is a 95-tap Kaiser filter, cutoff 7.8 kHz, β 8.5: −0.36 dB at the passband edge, −53.9 dB alias rejection, ~0.98 ms of delay (`R4-frontend-robustness-personalization.md` :157-162) — numbers provisional. AGC2 then sees 16 kHz in and 16 kHz out: no resampler and no band split inside APM.
2. **(b) APM resamples 48→16 kHz.** APM's PushSincResampler, with a Blackman window and a cutoff of about 0.9× Nyquist, ≈7.2 kHz at 16 kHz output, below the 7.6 kHz `mfcc.conf` uses; its response figures are unpublished (`Planning~/design-docs/agc-filter/recon-2026-11-05.md`, WebRTC APM research). It needs no filter of our own.

## The decision

**Option (a): a DC-normalised long FIR of our own replaces the 15-tap table, and AGC2 runs at 16 kHz in and out.** It commits the design to a decimator whose low-pass covers the model's feature band and which keeps every third sample, with no resampler and no band split inside APM (`Planning~/design-docs/agc-filter.md` §1, rules 1 and 2). The taps, the cutoff and the window are provisional → the tuning pass; the R4 candidate's figures are a starting reference, not a ruling.

## Why the rejected branches lost

- **(b) APM resamples** — it is unmeasured: its response figures are not published, and it cannot be inspected apart from APM, so the R14 half of the unit could not be verified on its own. What would have to change for it to win: it wins if APM's resampler response is measured to cover 7.6 kHz with alias rejection no worse than our FIR's.

## Consequences

- The filter's +2.51 dB of DC gain, which today's AGC absorbs, goes away, so the filter and the AGC are measured as one unit (ruling 10, `05-rulings.md` :97). `Tests~/Editor/DownsamplerTests.cs` pins the old tap sum today (`Planning~/design-docs/agc-filter/recon-2026-11-05.md`, Code map).
- Every score shifts: `minScore` 0.6, `minConfidence` 0.4 and the leading-miss bar were tuned to the old tilt and must be re-validated (`R4-frontend-robustness-personalization.md` :164-167); that threshold re-sweep is handed off (`Planning~/design-docs/agc-filter.md` §4).
- The filter's response is measured directly, part of the evidence G2 rests on (adr-0004).
- The filter is ours to maintain, on both platforms, from one source (adr-0001).
- Left open: the taps, cutoff and window (→ the tuning pass); how the filter is implemented (→ the architecture doc).

## Status

Accepted on 2026-11-05 by the human's (the maintainer's) ruling in conversation. The design doc is `Planning~/design-docs/agc-filter.md`; it was locked at G1 on 2026-11-05.
