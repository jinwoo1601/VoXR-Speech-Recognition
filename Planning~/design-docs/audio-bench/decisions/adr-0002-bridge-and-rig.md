---
id: adr-0002
title: Drive the real native bridge and the joined rig's recogniser layer, over Unity Editor PlayMode
status: accepted
date: 2026-11-08
topic: audio-bench
---

# adr-0002 — The native bridge and the joined rig

## The fork

The two-clocks split (adr-0001) makes the compute part a measurement of whatever code path the replay drives, so the bench has to decide which path that is. The device path is the native bridge: its recognition thread reads up to 4096 samples (~85 ms) at a time from the ring, sleeps 10 ms when the ring is empty, and queues each result with `consumed` and `origin` (`NativeBridge~/src/vosk_bridge.cpp:62`, `:87`, `:106-111`, `:130-140`). The Unity Editor runs a different native path: its mic backend calls libvosk and the front end directly, with no bridge thread or queue, and plays back a 4800-sample (100 ms) chunk per frame, not real time (`EditorMicBackend.cs:456`, `:531`). The command stage lives in the C# recogniser, whose only time source is `Time.time` (`VoxrCommandRecogniser.cs:694`) and which fires `OnCommandRecognised` (`:1516`); the joined rig already drives staged copies of the real Runtime recogniser sources (`JoinedRecogniser.cs:113`). Which path the bench drives decides whether its compute part resembles the device's at all (`Planning~/design-docs/audio-bench.md` §0, In 1 and 2).

## Options considered

1. **(A) The native bridge + the joined rig.** Paced push goes into the real native bridge — the device path, recognition thread and result queue, built on Windows as the harness is. The command stage runs through the real C# recogniser sources in the joined rig's recogniser layer, fed that run's timeline. It measures the device's threading and queueing on the desktop and the real command logic, on a rig that is already merged (PRs #177 and #178). It couples the bench to the joined rig, which lives outside the layout's Source and Tests slots (`Planning~/features/coverage-in-selection/ab-rig/`).
2. **(B) Unity Editor PlayMode.** The package runs end to end in one place, Unity's own frame loop included, with nothing staged. Its native path is not the device's: libvosk direct with no bridge thread, frame-bound playback, and slower.
3. **(C) The command event in audio time only.** Wall time ends at the final result, and the command event is stamped in audio time alone. It is simpler, with no modelled clock.

## The decision

**Option (A): both modes push into the real native bridge, and the command stage runs through the real C# recogniser sources in the joined rig's recogniser layer, fed that run's timeline.** Fast mode pushes in 16384-sample pushes, as the verdict harness does (`NativeBridge~/harness/main.cpp:160`), since the pins come from that path; paced mode pushes 20 ms chunks at real speed. Fast and paced modes therefore share one code path; the cost is that fast mode is tied to the harness's push size, and changing it later means re-pinning. In paced mode the joined rig delivers each result at its recorded paced wall-arrival time, on its frame clock, so the command event is stamped on both clocks, and the command stage's compute part is its paced time − its fast time; the cost is that the C# side's own compute, the recogniser's frame cost, is modelled by the rig's frame clock, not measured live. It commits the design to consuming the bridge's existing interface — push, results with `consumed`/`origin`, the consumed counter — with no bridge interface change as design intent, and to consuming the real recogniser sources through the joined rig rather than a copy of their logic.

## Why the rejected branches lost

- **(B) Unity Editor PlayMode** — the Editor runs a different native path (libvosk direct, no bridge thread), frame-bound and slower: "A latency harness that runs in the Editor measures the Editor" (`R6-evaluation-methodology.md` :456). What would have to change for it to win: the question were the Editor's own behaviour, or the Editor ran the bridge path.
- **(C) The command event in audio time only** — the latency axis would end one stage short of the command event, so any C#-side wait shows only in audio time. What would have to change for it to win: the C# command stage were shown to add no wall-clock wait beyond its audio-time part, e.g. once a measured frame cost proved negligible.

## Consequences

- Command 3, the pinned pass/fail harness, is not the path the bench drives, and it stays untouched (adr-0008).
- If the architecture finds a bridge interface change is needed, that is a scope stop.
- The joined-rig recogniser feature lists "real-time pacing of the harness" and "Decoder latency is not modelled" as its non-goals (`Planning~/features/joined-rig-recogniser/requirements.md` :89; `architecture.md` :150). They are that feature's non-goals, not a standing rule, so the bench supplying both breaks no rule.
- The bench depends on the joined rig's recogniser layer and its scorer's command classes; a change there is a change to the bench's command stage.
- Left open, for the architecture doc: how the paced push reaches the bridge, how the run's timeline is fed to the recogniser layer, and where the bench's code lives (an open item of the design doc). The recon found no managed binding for the bridge's `get_result_position` or `get_consumed` (`BridgeNative.cs`).

## Status

Accepted on 2026-11-08 by the human's (the maintainer's) ruling in session; the ruling took the recommended option. The design doc is `Planning~/design-docs/audio-bench.md`; it is not yet locked at G1. Amended on 2026-11-08 with the maintainer's BAR gap rulings; still not locked.
