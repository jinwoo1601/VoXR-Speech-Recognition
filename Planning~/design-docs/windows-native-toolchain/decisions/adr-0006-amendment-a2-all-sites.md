---
id: adr-0006
title: One Amendment A2 over every WSL/Linux line of automated-verification.md, over the three G0-named sites only
status: accepted
date: 2026-10-13
topic: windows-native-toolchain
---

# adr-0006 — One Amendment A2 over every WSL/Linux line of automated-verification.md, over the three G0-named sites only

## The fork

§0 In item 7 of the design named three sites of the locked `Planning~/design-docs/automated-verification.md` to amend: Goal 2 `:76`, Tier C purpose `:151`, what Tier C verifies `:183`. More lines of that doc name WSL or Linux as Tier C's host, among them DR-4 (`:238-242`), "Tier C targets Linux/WSL only". Removing WSL (adr-0001) contradicts each of them, so the amendment's reach had to be decided before G1 re-locks the doc.

## Options considered

1. **(a) One Amendment A2 over every WSL/Linux line.** In Amendment A1's form, covering `:25`, `:36`, `:76`, `:95`, `:139`, `:151`, `:167`, `:172`, `:175`, `:183`, DR-4 `:238-242` and backlog row `:261`. The locked doc ends consistent with the new host; it widens G0 scope item 7.
2. **(b) The three G0-named sites only.** `:76`, `:151` and `:183`, inside the G0 scope; it leaves DR-4's "Tier C targets Linux/WSL only" contradicting the new host.

## The decision

**Option (a): one Amendment A2, in Amendment A1's form, over every WSL/Linux line of `Planning~/design-docs/automated-verification.md` — `:25`, `:36`, `:76`, `:95`, `:139`, `:151`, `:167`, `:172`, `:175`, `:183`, DR-4 `:238-242` and backlog row `:261`.** This widens G0 scope item 7, by the human's ruling.

## Why the rejected branches lost

- **(b) Three sites only** — it leaves a locked doc contradicting itself. What would have to change for it to win: DR-4 already being superseded.

## Consequences

- Amendment A2 is written into the locked `Planning~/design-docs/automated-verification.md` on this branch, and the doc is re-locked by this topic's G1 (design intent, not yet built).
- G0 scope item 7 of this design is widened from three sites to every WSL/Linux line of that doc, DR-4 included.
- DR-4's editor-side rejection (no Windows bridge in the editor) is unchanged; A2 amends only its host.
- Line numbers of `automated-verification.md` cited in this record are those before Amendment A2 was inserted; A2 itself cites sections.

## Status

Accepted on 2026-10-13 by the human's ruling at SETTLE ("All recommended"), option (a).
