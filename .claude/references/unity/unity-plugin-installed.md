# unity-plugin-installed — the check's definition

The preflight check this pack declares under `provides.preflight-checks`, run by the gate's preflight audit as one of its items. It asserts that the `unity` Claude Code plugin is installed, which is what this pack defers its Unity CLI control to: **the pack ships no second implementation of that control**, so the plugin's presence is the whole of the pack's answer to driving the editor from a session.

## What it scans

Two facts, because installed and enabled are separate facts — the first read from one file, the second from up to three:

- Claude Code's own plugin **install ledger**, the `installed_plugins.json` it keeps under the user-level Claude Code directory, shaped `{"version": 2, "plugins": {"<name>@<marketplace>": [ { "scope", "installPath", "version", "installedAt", "lastUpdated", "gitCommitSha" } ] } }`. The check looks in the `plugins` map for a key whose name half is `unity`, written `unity@<marketplace>`; the marketplace half is not fixed, and on the machine this definition was read from it is `claude-plugins-official`.
- The **`enabledPlugins` map**, where that same `<name>@<marketplace>` key carries `true` or `false`, in three settings files, highest precedence first: the project's `.claude/settings.local.json`, then the project's `.claude/settings.json`, then the `settings.json` in the user-level Claude Code directory.

The first of the three files that has an entry for the key decides; a file that is absent, or present with no entry for the key, leaves the decision to the next. A file that is present but cannot be read, or whose entry for the key is neither `true` nor `false`, stops the walk — `UNVERIFIABLE` for enablement, the file named — since a lower file is not consulted past a higher one that might hold the deciding entry.

Both shapes were read from a live installation on 2026-09-14, and the three files' precedence is Claude Code's documented rule, read 2026-09-30; if Claude Code's plugin ledger changes shape, this definition is what drifted, not the check's design.

## What counts as a hit

One row per category:

- No `unity@…` key in the ledger's `plugins` map — the plugin is not installed.
- The key present while the deciding file's entry carries `false` for it — installed but disabled, naming the file that decided. This is reported as its own distinct state: the remediation differs, enable it in that file rather than install it.

**Not a hit:** the marketplace half of the key being anything in particular, and the plugin's version being anything in particular. Nor is the key having no `enabledPlugins` entry in any of the three files: that is not the same fact as `false`.

## What it reports

The gate auditor's three words, so the audit needs no translation:

- `PASS` — naming the key found and the file whose entry decided with `true` for it.
- `FAIL` — one row per hit, naming which of the two states it is and the remediation for that state.
- `UNVERIFIABLE` — when the ledger is absent or unreadable, when the ledger key has no `enabledPlugins` entry in any of the three files, and when a settings file cannot be read at its turn or holds an entry for the key that is neither `true` nor `false`, saying which part could not be read: the ledger in the first case, enablement in the other two, with the file named in the last. This is a real outcome here, not a hedge: these paths are Claude Code's own internals and not a published contract, so an absent ledger is not proof the plugin is absent and a missing `enabledPlugins` entry in all three files is not proof the plugin is disabled, and the check says what it could not read rather than reporting `FAIL`.

Worked example, enabled: the ledger holds the key, the user-level file carries `false` for it, `.claude/settings.json` carries `true`, and there is no `.claude/settings.local.json` — `PASS`, decided by `.claude/settings.json`.

Worked example, disabled: the ledger holds the key, `.claude/settings.local.json` carries `false` for it and `.claude/settings.json` carries `true`, whatever the user-level file says — `FAIL`, installed but disabled, decided by `.claude/settings.local.json`.

## What it does not do

Reach the network, or query a marketplace; install or enable anything; assert a version floor; verify that the plugin's skills actually resolve in the running session — a session cannot see its own plugin resolution from disk; read any enablement source beyond the three files — not a directory added by `--add-dir`, not a file passed by the `--settings` flag, not managed settings, whose value force-enables or blocks the plugin whatever the three say; and ruling — the check reports, the human rules at the gate.

## Status

`gate-preflight` is the runner. Its audit reads the project's manifest and takes each composed pack's definition files from `.claude/references/<pack>/`, so a project composing this pack reaches this file at `.claude/references/unity/unity-plugin-installed.md`; the audit performs what is written above and reports it in the three words above, beside its own items. Where there is no manifest to read, the audit marks this UNVERIFIABLE and says so rather than passing it.
