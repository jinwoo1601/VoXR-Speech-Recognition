---
id: adr-0001
title: Give the Windows Editor the native front end through P/Invoke, over running the whole bridge in the Editor, a managed AGC2 port, or keeping the Editor's old AGC
status: accepted
date: 2026-11-05
topic: agc-filter
---

# adr-0001 — Editor native DSP library

## The fork

The scope agreed at G0 (`Planning~/design-docs/agc-filter.md` §0, In 2) replaces both of today's diverging AGCs, `NativeBridge~/src/agc.h` on the Quest/Android path and `Runtime/Dsp/Agc.cs` on the Editor's managed path, with WebRTC APM AGC2. AGC2 is C++. The Windows Editor never loads the bridge: it runs its own managed pipeline over `libvosk.dll`, with its own C# downsampler and AGC (`Runtime/EditorMicBackend.cs:386-430`; `Documentation~/native-bridge.md` :61). The two copies already diverge on digital silence, where the C# AGC freezes its gain at once and the C++ one keeps adapting for ~1.9 s toward 20× (`03-critique-and-ranking.md` :85), and "any AGC change validated in the Editor is validated on the wrong AGC" (`I1-recognition-core-redesign.md` :393). How the Editor reaches AGC2 decides whether the Editor and the device run one front end or two, and so whether Editor replay measures the front end Quest runs; the parity test and the fate of the C# copies sit downstream of it (`Planning~/design-docs/agc-filter.md` §1, rules 7 and 8).

## Options considered

1. **(a) A native front end built for Windows x64, called from the managed Editor backend.** The filter and AGC2 are built as a native library for Windows x64 — the `windows-x64` harness preset already compiles the bridge with MSVC (`NativeBridge~/CMakePresets.json:5-19`) — and the managed Editor backend calls it by P/Invoke. Capture and the Vosk calls stay managed. Editor and device run one DSP source, and the C# `Agc.cs` and `Downsampler.cs` are retired. It costs a shipped Windows DLL, where today `Runtime/Plugins/x86_64/` ships no bridge DLL (`Documentation~/native-bridge.md` :61), and an MSVC build of the AGC2 subset that is unverified (`Planning~/design-docs/agc-filter/recon-2026-11-05.md`, WebRTC APM research).
2. **(b) The Editor runs the whole bridge in push mode.** The managed pipeline is retired and the Editor runs the device's own path end to end. The grammar and result paths move across ~12 `#if UNITY_EDITOR_WIN` branches.
3. **(c) A managed C# port of AGC2, without its RNN voice detector.** The Editor keeps a managed pipeline and needs no native front end.
4. **(d) The Editor keeps its old AGC.** Nothing changes in the Editor; only the device gets AGC2.

## The decision

**Option (a): the filter and AGC2 are built as a native library for Windows x64 and called by P/Invoke from the managed Editor backend; capture and the Vosk calls stay managed.** It commits the design to one front-end source for the Editor and the device, the C# `Agc.cs` and `Downsampler.cs` retired, and a test asserting that the Editor's and the native post-front-end samples are identical for the same input (`Planning~/design-docs/agc-filter.md` §1, rules 7 and 8).

## Why the rejected branches lost

- **(b) The whole bridge in the Editor** — scope creep: the grammar and result paths move across ~12 `#if UNITY_EDITOR_WIN` branches, a change well beyond the front end. What would have to change for it to win (proposed, for the human at G1): it wins if R16's gate and pre-roll need the bridge's ring buffer in the Editor too, or if the managed grammar and result paths are retired for another reason.
- **(c) A managed AGC2 port without the RNN voice detector** — it re-creates the divergence R15 exists to fix: the Editor would again validate on an AGC the device does not run (`I1-recognition-core-redesign.md` :393, "validated on the wrong AGC"). What would have to change for it to win (proposed, for the human at G1): it wins only if no native library can load in the Editor — the MSVC build failing beyond repair.
- **(d) The Editor keeps its old AGC** — it contradicts the scope agreed at G0, which replaces both AGCs (§0, In 2), and Editor replay would measure a front end Quest never runs. What would have to change for it to win (proposed, for the human at G1): it wins only if the scope drops the Editor.

## Consequences

- The package ships a Windows x64 DLL for the Editor; today it ships none (`Documentation~/native-bridge.md` :61).
- The MSVC build of the AGC2 subset is unverified (`Planning~/design-docs/agc-filter/recon-2026-11-05.md`, WebRTC APM research); the first architecture phase proves it (`Planning~/design-docs/agc-filter.md` §6).
- The C# `Agc.cs` and `Downsampler.cs` are retired. The tests that pin them today — `Tests~/Editor/DownsamplerTests.cs`, `Tests~/Editor/AgcTests.cs`, and Editor replay in `Tests~/Runtime/VoxrWavReplayTests.cs`, which runs the managed downsampler and AGC (`Planning~/design-docs/agc-filter/recon-2026-11-05.md`, Code map) — pin code this decision removes.
- A test asserts that the Editor's and the native post-front-end samples are identical for the same input; none compares them today (`01-code-audit.md` :497).
- The non-Windows Editor has no backend today, and this decision leaves it unchanged.
- Left open, for the architecture doc: the P/Invoke surface and how the library is packaged.

## Status

Accepted on 2026-11-05 by the human's (the maintainer's) ruling in conversation. The design doc is `Planning~/design-docs/agc-filter.md`; it is in design, not locked.
