---
id: adr-0001
title: Every live tool runs Windows-native and WSL is removed outright, over a generic Linux preset kept or WSL kept as fallback
status: accepted
date: 2026-10-13
topic: windows-native-toolchain
---

# adr-0001 — Every live tool runs Windows-native and WSL is removed outright, over a generic Linux preset kept or WSL kept as fallback

## The fork

§0 of the design (`Planning~/design-docs/windows-native-toolchain.md`, Forks for the design) left open whether WSL stays as an optional fallback or is removed outright. It is the keystone: every other fork in this design (compiler, cmake and ninja, libvosk, fixtures, the amendment) is decided either for the only desktop build or for an addition beside a WSL one, and the verification binding, the recipe, the harness README and `NativeBridge~/CMakePresets.json` carry WSL/Linux routes today. The human had said on 2026-10-13: "Why are we talking WSL? We moved out of wsl to windows native."

## Options considered

1. **(a) Remove outright.** Delete the `desktop-linux` preset, the Linux provisioning steps and every `wsl.exe` route; git history keeps them. One path to keep.
2. **(b) Remove WSL, keep a generic Linux preset.** WSL leaves the pipeline, but a Linux preset stays for a possible Linux CI. A Linux build stays ready if one is ever wanted.
3. **(c) Keep WSL as a documented fallback.** The WSL build, which ran 16/16 at G0, stays available beside the Windows one. Two paths to keep.

## The decision

**Option (a): every live tool runs Windows-native, and WSL is removed outright — the `desktop-linux` preset, the Linux provisioning steps and every `wsl.exe` route are deleted, and git history keeps them.** Every other fork of this design is decided for a Windows-native build that is the only desktop build.

## Why the rejected branches lost

- **(b) Keep a generic Linux preset** — nothing exercises it, so it rots. What would have to change for it to win: a Linux CI or a Linux host joining the pipeline.
- **(c) Keep WSL as a fallback** — two paths to keep, the opposite of the goal and of the human's word "We moved out of wsl to windows native." What would have to change for it to win: only the Windows build proving impossible.

## Consequences

- Commands 3 and 4, the prerequisites, the Shell and Path-form lines of the verification binding (`.claude/bindings/unity.md`), the recipe (`Planning~/verification-recipe.md`), the harness README (`NativeBridge~/harness/README.md`) and the preset (`NativeBridge~/CMakePresets.json`) lose every WSL/Linux route (design intent, not yet built).
- No Linux desktop build remains to fall back on; a Windows build failure is fixed on Windows, not routed around.
- Deferral WSL-1 (`memory/STATUS.md` `## Deferred`) closes at this topic's G2.

## Status

Accepted on 2026-10-13 by the human's ruling at SETTLE ("All recommended"), option (a).
