---
id: adr-0001
title: Join the harness to the parser rig by a file handoff, over the rig calling the bridge through P/Invoke or an in-Unity replay
status: accepted
date: 2026-10-13
topic: joined-rig
---

# adr-0001 — Join the harness to the parser rig by a file handoff

## The fork

The scope agreed at G0 (`Planning~/design-docs/joined-rig.md` §0, In 1) asks for a pipe from the WSL push-audio harness into the parser A/B rig, so one run goes audio → decoder → parser → scored outcome. Both instruments exist and nothing joins them: the harness (`NativeBridge~/harness/main.cpp`) replays 48 kHz WAVs through the native pipeline — ring buffer → downsampler → AGC → int16 → VOSK (`NativeBridge~/src/vosk_bridge.cpp:91-126`) — keeps only the final `text` (`main.cpp:41-58`) and passes on exact equality with `expectations.json` `expectedFinals` (`:227`); the parser rig (`Planning~/features/coverage-in-selection/ab-rig/`) compiles the real parser source from any git ref under dotnet in WSL (`stage.sh:15-25`) and reads text only (`Program.cs:304`). Quest and Android run the native pipeline, while the Windows Editor runs its own managed C# pipeline, whose AGC has diverged from the native one (`Documentation~/native-bridge.md:61`; `01-code-audit.md:497`). The pipe's topology decides which pipeline is measured, whether the rig runs without Unity (`Planning~/design-docs/automated-verification.md` §3 goal 2, `:76`), and how the kill check against the committed baseline is made, so every other rule of the design sits downstream of it.

## Options considered

1. **(a) File handoff.** The harness gains a dump of every decoder result as JSON lines — finals and partials, words with `conf` and timings, and audio-time stamps — and the dotnet rig gains a reader and a scorer that consume the dump. Two processes, joined by one file contract; a dump is replayable through any parser ref without decoding again. Feasibility was shown on 2026-10-13 by lab `lab-next-level-probes` probe 1: a few-line dump added to a scratch copy of the harness still matched 16/16.
2. **(b) The rig calls the bridge itself through P/Invoke.** The C# push bindings exist, but have zero call sites.
3. **(c) Inside Unity**, by extending the WAV-replay PlayMode tests.

## The decision

**Option (a): the harness dumps every decoder result as JSON lines, and the dotnet rig reads and scores the dump.** It commits the design to two processes joined by one file contract, to measuring the native (Quest) pipeline, and to running from WSL with no Unity (`Planning~/design-docs/joined-rig.md` §1, rules 1, 6 and 7). The dump schema is the architecture doc's.

## Why the rejected branches lost

- **(b) P/Invoke from the rig** — it couples the rig to native code, lets the harness and the rig drift apart, and would mean reimplementing the byte-for-byte check. What would have to change for it to win: the decision record gives the reason it lost and nothing more.
- **(c) Inside Unity** — it measures the Editor's C# pipeline, not Quest's, needs Unity, and breaks goal 2 (`automated-verification.md` §3, `:76`). What would have to change for it to win: the decision record gives the reason it lost and nothing more.

## Consequences

- The harness gains the dump; the dump is additive, and the harness verdict is unchanged, so the locked Tier C design (`automated-verification.md`) is extended, not reopened.
- The rig gains a dump reader and a scorer; the dump contract, the reader and the scorer are owned by this topic.
- A dump is replayable through any parser ref without decoding again.
- The kill check (rule 2) reads the transcript layer — the final texts in the dump — against the committed `expectations.json` byte for byte; if they differ, the run reports no command-level numbers.
- The pipeline measured is the native (Quest) pipeline; the C# Editor pipeline and its AGC divergence are out of scope and handed off.
- Claude can run it from WSL with no Unity, which restates `automated-verification.md` goal 2.
- Left open, for the architecture doc: the dump schema, the run script, and where the joined rig lives (today it sits under one feature's `Planning~/` directory, and an older copy exists in `fidelity-miss-cost/ab-rig`).

## Status

Accepted on 2026-10-13 by the human's ruling in conversation. The design doc (`Planning~/design-docs/joined-rig.md`) is in design and not locked.
