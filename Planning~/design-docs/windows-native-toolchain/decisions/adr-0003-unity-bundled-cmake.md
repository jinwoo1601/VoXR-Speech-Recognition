---
id: adr-0003
title: Unity's bundled cmake and ninja by absolute path over a winget install on PATH
status: accepted
date: 2026-10-13
topic: windows-native-toolchain
---

# adr-0003 — Unity's bundled cmake and ninja by absolute path over a winget install on PATH

## The fork

`cmake` and `ninja` are not on PATH on the host (§0, Facts at G0), and §0 of the design (Forks for the design) left open where they come from. The answer fixes which CMake version the desktop build runs, and so which Visual Studio generator it can use: CMake 3.22 supports the "Visual Studio 17 2022" generator, while "Visual Studio 18 2026" needs CMake 4.2.

## Options considered

1. **(a) Unity's bundled cmake and ninja, by absolute path.** Unity 6000.3.7f1's Android SDK ships cmake 3.22.1 and ninja 1.10.2, already used by Command 3's arm64 half. No install, and the same tools as the arm64 half; tied to that editor install; CMake 3.22 knows Visual Studio 2022, not Visual Studio 2026.
2. **(b) A current CMake and Ninja installed by winget, on PATH.** Current versions, and Visual Studio 2026 works; a setup step on every machine, and PATH drift.

## The decision

**Option (a): the desktop build uses the cmake 3.22.1 and ninja 1.10.2 that Unity 6000.3.7f1's Android SDK ships, named by absolute path, and targets Visual Studio 2022 accordingly.**

## Why the rejected branches lost

- **(b) winget on PATH** — it adds a prerequisite the arm64 half already does without. What would have to change for it to win: the bundled CMake proving unable to build the desktop target, or the Unity editor version moving to one without a bundled cmake.

## Consequences

- The verification binding names the cmake and ninja paths absolutely, one place to edit when the editor version moves (design intent, not yet built).
- Visual Studio 2026 is not used for this build, although it is installed.
- The desktop build is tied to the Unity 6000.3.7f1 install, as the arm64 half already is.
- Ninja needs a pre-configured compiler environment, so the route into the Visual Studio environment (adr-0002) is the architecture doc's to settle with this CMake.

## Status

Accepted on 2026-10-13 by the human's ruling at SETTLE ("All recommended"), option (a).
