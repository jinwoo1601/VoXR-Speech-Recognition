---
id: adr-0002
title: MSVC over clang or MinGW gcc
status: accepted
date: 2026-10-13
topic: windows-native-toolchain
---

# adr-0002 — MSVC over clang or MinGW gcc

## The fork

The desktop harness needs a Windows C++ compiler once WSL is removed (adr-0001), and §0 of the design (Forks for the design) left the toolchain open. The bridge and harness C++ hold one GCC/Clang-only construct, the export macro at `src/vosk_bridge.h:6` (under `NativeBridge~/`), `__attribute__((visibility("default")))`; `NativeBridge~/CMakeLists.txt:74` sets the GNU linker flags `-Wl,--no-undefined -Wl,--strip-all` unconditionally; no POSIX API is used. The compiler picked decides what must be installed and which of these must be made portable.

## Options considered

1. **(a) MSVC.** Visual Studio Community 2022 (17.14, MSVC 14.44) and 2026 (18.6, MSVC 14.51) are installed with the x64 C++ tools, so nothing is installed; `cl` is not on PATH, so the build must reach the Visual Studio environment.
2. **(b) clang, via winget LLVM.** One compiler family with the Android build; a new prerequisite on every machine.
3. **(c) MinGW gcc.** The same compiler that built libvosk; an install that buys nothing across a plain C API.

## The decision

**Option (a): MSVC, from the installed Visual Studio 2022.** The C++ is made to compile under MSVC, and the build reaches the Visual Studio environment because `cl` is not on PATH; Visual Studio 2022 rather than 2026 follows from the bundled CMake (adr-0003).

## Why the rejected branches lost

- **(b) clang via winget LLVM** — an install for no gain the build needs. What would have to change for it to win: MSVC rejecting the code, or a single compiler family becoming a requirement.
- **(c) MinGW gcc** — an ABI match with libvosk is irrelevant across `extern "C"`. What would have to change for it to win: a C++ type having to cross the libvosk boundary.

## Consequences

- The C++ needs an export macro that works on every compiler it builds with, in place of the GCC/Clang-only one at `src/vosk_bridge.h:6` (design intent, not yet built).
- The GNU link flags at `NativeBridge~/CMakeLists.txt:74` are guarded off MSVC (design intent, not yet built).
- How the Visual Studio environment is reached — the Visual Studio generator, or Ninja inside the Visual Studio environment — is the architecture doc's.
- The desktop harness is built by a different compiler family from the Android arm64 bridge.

## Status

Accepted on 2026-10-13 by the human's ruling at SETTLE ("All recommended"), option (a).
