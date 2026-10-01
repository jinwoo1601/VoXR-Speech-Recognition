# unity-cli-on-path — the check's definition

The preflight check this pack declares under `provides.preflight-checks`, run by the gate's preflight audit as one of its items. It asserts that the Unity CLI, the `unity` command the `unity` Claude Code plugin's skills drive, resolves in this environment — a CLI that does not resolve means every plugin skill that shells out to it fails at the first call.

## What it scans

The **environment**: the check resolves `unity` on the PATH of the shell the audit runs in, and runs `unity --version`. On the machine this definition was read from, on 2026-09-28, that printed `1.0.0-beta.11`, one line; if the CLI's version output changes shape, this definition is what drifted, not the check's design.

## What counts as a hit

One row per category:

- Nothing resolves under `unity` — the CLI is not on the PATH.
- `unity` resolves but prints no version, which is the same practical state: the first plugin skill that calls it will fail.

**Not a hit:** any particular version or release channel, a beta included; the sign-in state; and which Unity Editors are installed.

## What it reports

The gate auditor's three words, so the audit needs no translation:

- `PASS` — naming the path `unity` resolved to and the version it printed.
- `FAIL` — one row per hit, naming which of the two states it is, with the remediation: install the Unity CLI so that `unity` resolves on the PATH.
- `UNVERIFIABLE` — when the audit cannot run a command in the environment at all, saying so rather than reporting `FAIL`.

## What it does not do

Install or update anything; sign in, or read the authentication state; reach the network; assert a version floor; check which Unity Editors are installed; and ruling — the check reports, the human rules at the gate.

## Status

`gate-preflight` is the runner. Its audit reads the project's manifest and takes each composed pack's definition files from `.claude/references/<pack>/`, so a project composing this pack reaches this file at `.claude/references/unity/unity-cli-on-path.md`; the audit performs what is written above and reports it in the three words above, beside its own items. Where there is no manifest to read, the audit marks this UNVERIFIABLE and says so rather than passing it.
