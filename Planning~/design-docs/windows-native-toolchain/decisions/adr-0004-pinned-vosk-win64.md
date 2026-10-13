---
id: adr-0004
title: The pinned vosk-win64-0.3.45 release in vendor/ over reusing the shipped libvosk.dll
status: accepted
date: 2026-10-13
topic: windows-native-toolchain
---

# adr-0004 — The pinned vosk-win64-0.3.45 release in vendor/ over reusing the shipped libvosk.dll

## The fork

The desktop harness on Windows links a Windows libvosk in place of the Linux `libvosk.so` that `NativeBridge~/CMakeLists.txt:59` hard-codes, and §0 of the design (Forks for the design) left its source open. MSVC (adr-0002) links a DLL through an import library, so the source chosen decides whether one comes ready-made or has to be produced by hand.

## Options considered

1. **(a) The pinned `vosk-win64-0.3.45.zip`.** Alphacephei's release (14,882,445 bytes), provisioned into the gitignored `NativeBridge~/vendor/`: the same version as the committed baseline and the same provisioning pattern; it is built by a MinGW cross-compile whose script emits `libvosk.lib` (the zip's contents are inferred, not opened).
2. **(b) Reuse the shipped `Runtime/Plugins/x86_64/libvosk.dll`.** The exact binary the Editor runs (a MinGW build, from the v0.3.45 Windows release per `Planning~/v3.1-editor-mic-plan.md:258`), with no download; it has no import library, so one must be made with `dumpbin` and `lib /def`, from a Visual Studio prompt only.

## The decision

**Option (a): the desktop harness links the pinned `vosk-win64-0.3.45.zip`, provisioned into the gitignored `NativeBridge~/vendor/`.**

## Why the rejected branches lost

- **(b) The shipped DLL** — the hand-made import library is a fiddly extra step. What would have to change for it to win: the zip lacking an import library, or its library diverging from the shipped binary.

## Consequences

- The architecture doc confirms the zip's contents and whether its `libvosk.dll` is identical to the shipped `Runtime/Plugins/x86_64/libvosk.dll`.
- A difference between the two is a stop for the human, not a choice the build makes.
- `NativeBridge~/vendor/` stays gitignored, so the zip is provisioned rather than committed (design intent, not yet built).

## Status

Accepted on 2026-10-13 by the human's ruling at SETTLE ("All recommended"), option (a).
