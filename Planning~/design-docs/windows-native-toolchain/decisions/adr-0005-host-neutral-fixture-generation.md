---
id: adr-0005
title: One host-neutral generate.sh over a PowerShell twin or Windows System.Speech voices
status: accepted
date: 2026-10-13
topic: windows-native-toolchain
---

# adr-0005 — One host-neutral generate.sh over a PowerShell twin or Windows System.Speech voices

## The fork

`Tests~/Fixtures/generate.sh` makes the committed fixture WAVs that the harness baseline is judged on, and §0 of the design (Forks for the design) left open how fixtures are generated on Windows. Python 3.12.10 is native on the host, and piper-tts 1.6.0 and soxr 1.1.0 publish win_amd64 wheels covering CPython 3.12; what differs between hosts is the venv layout (`Scripts\` against `bin/`) and the interpreter name.

## Options considered

1. **(a) One host-neutral `generate.sh`.** The same script runs on both hosts, with the same piper-tts 1.6.0, the same voice `en_US-lessac-medium` and the same soxr; the venv layout and the interpreter name are fixed to work on either.
2. **(b) A PowerShell twin.** Native to Windows; two scripts to keep in sync.
3. **(c) Windows System.Speech voices.** No installs; a different voice changes every fixture and voids the baseline.

## The decision

**Option (a): `Tests~/Fixtures/generate.sh` is made host-neutral, with the same piper-tts 1.6.0, voice `en_US-lessac-medium` and soxr, and the venv layout and interpreter name made to work on either host.**

## Why the rejected branches lost

- **(b) A PowerShell twin** — duplication. What would have to change for it to win: Git Bash being unable to run `generate.sh`.
- **(c) System.Speech voices** — it breaks the committed corpus and baseline. What would have to change for it to win: only a deliberate re-pin of the corpus.

## Consequences

- The output recipe of the committed WAVs is unchanged: same engine, same voice, same resampler.
- The script changes only where the hosts differ, the venv layout and the interpreter name (design intent, not yet built).

## Status

Accepted on 2026-10-13 by the human's ruling at SETTLE ("All recommended"), option (a).
