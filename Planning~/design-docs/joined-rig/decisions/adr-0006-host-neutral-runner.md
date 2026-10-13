---
id: adr-0006
title: Runnable by Claude from its own shell, host left to the architecture doc, over Windows-native required or WSL kept
status: accepted
date: 2026-10-13
topic: joined-rig
---

# adr-0006 — The joined rig is runnable by Claude from its own shell; the host is the architecture doc's

## The fork

Rule 7 of the design (`Planning~/design-docs/joined-rig.md` §1) said the rig runs "From WSL, with no Unity", restating `Planning~/design-docs/automated-verification.md` §3 goal 2 (`:76`), whose text literally says WSL. Claude Code has since moved off WSL to run natively on Windows, while the project's build bindings still route tools through WSL. Whether rule 7 keeps naming WSL, names Windows, or names no host decides where each half of the rig (the C++ harness and the dotnet rig) is built and run.

## Options considered

1. **(a) Host-neutral.** The rig is runnable by Claude from its own shell, with no Unity and no device. Which host runs each half, Windows-native or WSL, is the architecture doc's.
2. **(b) Windows-native required.** The dotnet rig on Windows, and the C++ harness gains a Windows build (libvosk win64, cmake/ninja).
3. **(c) WSL stays for builds.** Rule 7 unchanged.

## The decision

**Option (a): the rig is runnable by Claude from its own shell, with no Unity and no device; which host runs each half, Windows-native or WSL, is decided in the architecture doc.** Rule 7 (`Planning~/design-docs/joined-rig.md` §1) carries the intent of `automated-verification.md` goal 2; that goal's literal "from WSL" named the host of its day and is not adopted.

## Why the rejected branches lost

- **(b) Windows-native required** — it would put a Windows native build target into the parser feature's scope before anyone has shown it is needed. What would have to change for it to win: the decision record gives the reason it lost and nothing more.
- **(c) WSL stays for builds** — it contradicts the human's move of Claude Code off WSL. What would have to change for it to win: the decision record gives the reason it lost and nothing more.

## Consequences

- Rule 7 no longer requires WSL; it requires only that Claude can run the rig from its own shell, with no Unity and no device.
- The host for each half, Windows-native or WSL, is left open for the architecture doc of `feat-joined-rig-parser` (`Planning~/design-docs/joined-rig.md` §7).
- Lines of the design that describe today's tools as fact, such as dotnet in WSL and the WSL push-audio harness, are unchanged.
- The decision line of adr-0001 now reads "running with no Unity, on a host the architecture doc picks (adr-0006)".

## Status

Accepted on 2026-10-13 by the human's ruling in conversation (option (a)). The design doc (`Planning~/design-docs/joined-rig.md`) was re-locked at G1 on 2026-10-13 with this amendment (Amendment A1), on the human's ruling "Re-locked".
