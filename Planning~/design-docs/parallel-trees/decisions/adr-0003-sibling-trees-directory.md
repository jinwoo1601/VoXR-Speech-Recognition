---
id: adr-0003
title: Trees live in a sibling `VoXR-Speech-Recognition.trees` directory, over an in-repo `.trees/`
status: accepted
date: 2026-11-05
topic: parallel-trees
---

# adr-0003 — Trees live in a sibling `VoXR-Speech-Recognition.trees` directory, over an in-repo `.trees/`

## The fork

§0 of the design (`Planning~/design-docs/parallel-trees.md`, Forks for the design) left open where trees live and how they are named (In 3). The `trees` binding's Location slot needs the answer, and the constraint is that this repository's root is the UPM package root TestGround consumes by `file:` path (`D:\Workspace\VoXR TestGround\Packages\manifest.json` :3). The precedent is the harness repo's filled `trees` binding (`D:\Workspace\harness\.claude\bindings\vc-git.md` `## trees`), which puts its trees in `D:\Workspace\harness.trees\<branch>` and its writer trees as `<branch>--<unit>`; VR FTL-Like 3 uses `D:\Workspace\VR FTL-Like 3.trees`.

## Options considered

1. **(a) A sibling trees directory.** Trees go in `D:\Workspace\VoXR-Speech-Recognition.trees\<branch>`, and a writer tree is `...\<branch>--<unit>`, as the harness repo and VR FTL-Like 3 do.
2. **(b) An in-repo `.trees/`.** Trees go in a `.trees/` directory inside the repository.

## The decision

**Option (a): trees go in `D:\Workspace\VoXR-Speech-Recognition.trees\<branch>`, and a writer tree is `D:\Workspace\VoXR-Speech-Recognition.trees\<branch>--<unit>`.** No tree sits inside the package root TestGround consumes.

## Why the rejected branches lost

- **(b) An in-repo `.trees/`** — it nests trees inside the UPM package root that TestGround consumes, and it has to be excluded from every recursive tool. What would have to change for it to win: the repository root no longer being the package root TestGround consumes, and no recursive tool walking it.

## Consequences

- The `trees` binding's Location reads `D:\Workspace\VoXR-Speech-Recognition.trees\<branch>`, with writer trees as `<branch>--<unit>` (design intent, not yet built).
- Changed by the follow-up feature after G1, not on this branch: `.claude/bindings/vc-git.md` :19-20, the `trees` value and comment; `.claude/bindings/core.md` `## session-launch`, the Launch command's `<tree>` note "(`trees` is none)".

## Status

Accepted on 2026-11-05 by the human's ruling (AskUserQuestion), option (a).
