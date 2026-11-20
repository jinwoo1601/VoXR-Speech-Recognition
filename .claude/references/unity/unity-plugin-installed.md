# unity-plugin-installed — the check's definition

The preflight check this pack declares under `provides.preflight-checks`, run by the gate's preflight audit as one of its items. It asserts that the `unity` Claude Code plugin is installed, which is what this pack defers its Unity CLI control to: **no pack skill or agent duplicates the plugin, and the pack's contract reference, which agents read (`.claude/references/unity/unity-cli-contract.md`), is not one**, so the plugin's presence is the whole of the pack's answer to driving the editor from a session.

## What it scans

One command, `claude plugin list --json`, run once from the project root — the directory holding the project's manifest `.claude/harness.json`, which is the audit's own working directory — with no `cd` before it and no path argument, since the command takes none and reads the settings merged for the directory it runs from.

It prints a JSON array of objects, one per install of a plugin — a `scope` `user` entry, and a `scope` `project` entry per project the plugin is installed in, which also carries a `projectPath` — each carrying the plugin's `id`, written `<name>@<marketplace>`, and `enabled`, whether the plugin is enabled in the settings merged for the run directory, managed settings included. The check takes the entries whose `id` is `unity@<marketplace>`: the name half `unity`, the marketplace half any — on the machine this definition was read from, `claude-plugins-official`.

The shape was read from Claude Code 2.1.328 on 2026-11-19, run from three directories, `enabled` the same on every entry of the id in each run; if the command's output changes shape, this definition is what drifted, not the check's design.

## What counts as a hit

Installed is at least one entry of the parsed array whose `id` is `unity@<marketplace>`. Enabled is those entries' `enabled`, read together: all `true` is enabled, all `false` is disabled, and any disagreement among them, or an entry with no boolean `enabled`, is `UNVERIFIABLE`.

One row per category:

- No entry whose `id` is `unity@<marketplace>` in the parsed array, an empty array included — the plugin is not installed.
- Entries of the id, every one carrying `enabled` `false` — installed but disabled. This is reported as its own distinct state: the remediation differs, enable it rather than install it.

**Not a hit:** the marketplace half of the id being anything in particular, the plugin's version being anything in particular, and the `scope` or `projectPath` any entry carries — the run directory, not an entry, makes the verdict the project's.

## What it reports

The gate auditor's three words, so the audit needs no translation. Every report — `PASS`, `FAIL` or `UNVERIFIABLE` — carries the limits clause: a session launched with `--settings` or `--add-dir` may enable or disable the plugin otherwise than this check reads, each being per session, not per project.

- `PASS` — naming the id found, every entry of it carrying `enabled` `true`, with the limits clause.
- `FAIL` — one row per hit, naming which of the two states it is, with its remediation and the limits clause: not installed — install the `unity` plugin; installed but disabled — enable it in settings, or managed settings block it, which no project setting overrides.
- `UNVERIFIABLE` — naming the reason, with the limits clause, never `PASS`: a failed run — the command missing, a non-zero exit, or output that does not parse as a JSON array — or entries of the id disagreeing on `enabled`, or an entry of the id with no boolean `enabled`, or an element of the array that is not an object with a string `id`. This is a real outcome here, not a hedge: the check reports only what a completed run printed, and a run it could not complete says nothing of the plugin.

A parsed array with no entry of the id is `FAIL`, not installed, never `UNVERIFIABLE`: the array is Claude Code's own statement of what is installed, so a missing id there is a fact, not an unread.

An array holding an element that is not an object with a string `id` is no such statement: `UNVERIFIABLE`, never `FAIL`.

Worked example, enabled: the command exits 0 and prints an array of four entries, each `id` `unity@claude-plugins-official` — one `scope` `user`, three `scope` `project`, each with its own `projectPath` — every one carrying `enabled` `true` — `PASS`, naming `unity@claude-plugins-official`, with the limits clause.

Worked example, not installed: the command exits 0 and prints `[]`, or an array whose every `id` names another plugin — `FAIL`, not installed, with its remediation and the limits clause.

Worked example, disabled: the command exits 0 and prints the same four entries, every one carrying `enabled` `false` — `FAIL`, installed but disabled, with its remediation and the limits clause.

Worked example, a failed run: `claude` is not on the PATH, or the command exits non-zero, or it prints text that is not a JSON array — `UNVERIFIABLE`, naming which, with the limits clause, never `PASS`.

Worked example, disagreeing entries: the `scope` `user` entry carries `enabled` `true` and a `scope` `project` entry `enabled` `false`, or an entry of the id carries no `enabled` — `UNVERIFIABLE`, naming the disagreement or the entry, with the limits clause.

## What it does not do

Reach the network, or query a marketplace; install or enable anything; assert a version floor; verify that the plugin's skills actually resolve in the running session — a session cannot see its own plugin resolution from disk; read any settings file or plugin file itself — the command's `enabled` is the merged setting, managed settings included, and a session launched with `--settings` or `--add-dir` is the limit every report names; and ruling — the check reports, the human rules at the gate.

## Status

`gate-preflight` item 5, *Pack-declared preflight checks*, runs this check and reports it as that item says.

The pack ships this check as a script beside this file, `preflight-check.py`, which a project reaches at `.claude/references/unity/preflight-check.py`; the audit runs it from its own working directory, the project root, alone in its call with no `cd`, as `py .claude/references/unity/preflight-check.py unity-plugin-installed`, and reports the one line it prints, whose first word is the report's: `PASS: unity-plugin-installed: …`, exit 0; `FAIL: unity-plugin-installed: …`, naming which of the two states with its remediation, exit 2; or `UNVERIFIABLE: unity-plugin-installed: …`, naming the reason, exit 3 — each line ending with the limits clause. The script runs `## What it scans`'s command under a time bound of its own, a run ending on the bound being a failed run. A refusal, one stderr line and exit 1, is a call to correct; where the script is absent, or the call cannot be corrected, the audit performs the sections above as they say. `## What it scans`, `## What counts as a hit` and `## What it reports` stay the rule the script implements.
