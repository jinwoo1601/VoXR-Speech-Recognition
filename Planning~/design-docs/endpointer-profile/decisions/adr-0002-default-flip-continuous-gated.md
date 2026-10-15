---
id: adr-0002
title: Flip the default in this feature, in Continuous mode only and gated on evidence and a human check, over an opt-in tuned profile or an ungated flip
status: accepted
date: 2026-10-16
topic: endpointer-profile
---

# adr-0002 — Flip the default in Continuous mode, gated

## The fork

Once a tuned profile exists (adr-0001, adr-0003), the design must say whether consumers get it by default and in which listening mode (`Planning~/design-docs/endpointer-profile.md` §1, rules 6 and 7). The mode matters because the endpointer does different work in each. In Continuous mode a finished command is normally ended by rule2, so the profile is where the saving is (§3, Forward). In Push-to-talk, release forces the final (`vosk_bridge.cpp:175`), so the endpointer can only split a press (§1). Fluent TTS audio flatters low values (§3, failure modes), so how the flip is gated decides whether it ships measured or unmeasured splits. The default also decides what the standing harness baseline pins (rulings 2 and 10).

## Options considered

1. **(A) Opt-in.** The default stays the model's own values, and the flip is made later on human evidence.
2. **(B) Flip now, ungated.**
3. **(C) Flip in this feature, gated before G2** on the pause-ladder sweep (adr-0004), both kill criteria (§0) and a human-only check, on-device or a recorded listen — not on the full R5 corpus. Within (C) the mode scope was also open: Continuous only, or both modes.

## The decision

**Option (C), Continuous mode only: this feature flips the default to the tuned preset in Continuous mode, gated before G2 on the pause-ladder sweep, both kill criteria and a human-only check.** In Push-to-talk the default stays the model's own values, because release already forces the final and a tighter profile only adds splits within a press. An explicit developer choice overrides the default in both modes (`Planning~/design-docs/endpointer-profile.md` §1, rules 6 and 7).

## Why the rejected branches lost

- **(A) Opt-in** — it leaves the latency win unused by default. What would have to change for it to win: the human check fails.
- **(B) Flip now, ungated** — fluent TTS makes lower values look free, so an ungated flip ships unmeasured splits. What would have to change for it to win: the decision record gives the reason it lost and nothing more.
- **Both modes** — Push-to-talk pays split risk for no latency gain. What would have to change for it to win: the decision record gives the reason it lost and nothing more.

## Consequences

- A behaviour change for every Continuous-mode consumer on upgrade; it goes in the CHANGELOG.
- Acoustic baselines are re-pinned to the shipped default through the harness, never edited (rulings 2 and 10).
- The standing harness baseline (`expectations.json`) pins the Continuous default, the tuned preset: the harness already simulates continuous listening, since it appends silence and lets the endpointer fire. The flip re-pins `expectations.json` through the harness at merge (rulings 2 and 10). The archive's own conf stays measurable as side A of a decoder-side run. Rejected, on the same ruling: pinning "Model default", because the shipped Continuous default would then leave the standing check; and pinning both, because it doubles the harness run and adds a second expectations file.
- G2 carries a human-only item: the check on-device or by a recorded listen.
- Left open: the human-only check's exact form, on-device or a recorded listen (`Planning~/design-docs/endpointer-profile.md` §6); and whether the listening mode can change after model load, since the profile is resolved at load.

## Status

Accepted on 2026-10-16 by the human's (the maintainer's) ruling in conversation; the ruling on what the standing baseline pins was added the same day. The design doc is `Planning~/design-docs/endpointer-profile.md`; it is not yet locked.
