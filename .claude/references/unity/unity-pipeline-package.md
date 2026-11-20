# unity-pipeline-package — the check's definition

The preflight check this pack declares under `provides.preflight-checks`, run by the gate's preflight audit as one of its items. It asserts that the Unity project carries the `com.unity.pipeline` package, the Editor-side package the Unity CLI's live-Editor commands (`unity command`, `unity run --command`) talk to — without it those commands cannot reach the Editor.

## What it scans

The `Host project` slot of the `verification` binding, read where the audit reads that binding, picks the one manifest the check reads.

- **The slot filled with a path.** The host root, `<host>`, is that path — an absolute path as written, a relative path resolved against the project root, the root of the tree the audit runs in — and the manifest read is `<host>/Packages/manifest.json`, even where the project root holds a `Packages/manifest.json` of its own.
- **The slot reading as `none`.** An absent `- Host project:` line, or one whose value holds `TODO` or is `none`, reads as `none`, and the manifest read is `Packages/manifest.json` at the project root — the directory holding the project's manifest `.claude/harness.json`, which for a Unity project is also the Unity project root, beside `Assets/`; where that file is absent, the check also asks whether the project root holds a `package.json`, which marks a package root.

The check reads that manifest's `dependencies` map for the key `com.unity.pipeline`. File contents only: it opens no Editor and reads nothing under `Library/`.

## What counts as a hit

One row:

- The manifest read — the host's or the project root's, as `## What it scans` picks it — reads, and its `dependencies` map has no `com.unity.pipeline` key.

**Not a hit:** the version string the key carries, an `-exp` or pre-release version included, or its source form — a registry version, a `file:` path, or a git URL; `packages-lock.json` disagreeing with the manifest; and the package not yet resolved under `Library/PackageCache`.

## What it reports

The gate auditor's three words, so the audit needs no translation. Every report names the manifest it read, or, where no manifest could be read, the path it looked for.

- `PASS` — naming the key found, the version string it carries, and the manifest read.
- `FAIL` — for the hit, naming the manifest read, with the remediation: run `unity pipeline install --project-path <the root whose manifest was read>` — the host root where the slot is filled, else the project root. The human runs it, or the PM on the human's word; it adds the dependency to that root's `Packages/manifest.json`, and the Editor resolves the package on its next open.
- `UNVERIFIABLE` — saying which of these it is rather than reporting `FAIL`: the slot is filled and the host path names no directory, or the host holds no `Packages/manifest.json` — naming the host path, as written and as resolved, as the path to correct, and `<host>/Packages/manifest.json` as the path it looked for; the slot reads as `none` and the project root holds a `package.json` and no `Packages/manifest.json` — a package root with no host named — naming the `Host project` slot of the `verification` binding as the remedy, filled with the host project's root, and the project root's `Packages/manifest.json` as the path it looked for; the slot reads as `none` and the project root holds neither — no Unity project at the root, which is not proof the package is absent — naming the project root's `Packages/manifest.json` as the path it looked for; or the manifest read does not parse as JSON, naming that manifest.

## What it does not do

Run `unity pipeline install` or `unity pipeline upgrade`, or edit the manifest; reach the network, or query the registry; verify that the Editor loaded the package — Safe Mode can stop it loading; assert a version floor; and ruling — the check reports, the human rules at the gate.

## Status

`gate-preflight` item 5, *Pack-declared preflight checks*, runs this check and reports it as that item says.

The pack ships this check as a script beside this file, `preflight-check.py`, which a project reaches at `.claude/references/unity/preflight-check.py`; the audit reads the `Host project` slot where `## What it scans` says and runs the script from its own working directory, the project root, alone in its call with no `cd`, as `py .claude/references/unity/preflight-check.py unity-pipeline-package --host '<the slot's value as written>'`, passing `none` where the line is absent. The script reads an empty value, or one holding `TODO` or `none`, as `none`, resolves a relative path against its working directory, and prints one line whose first word is the report's: `PASS: unity-pipeline-package: …`, naming the key, its version and the manifest read, exit 0; `FAIL: unity-pipeline-package: …`, naming the manifest read and the root its remediation installs into, exit 2; or `UNVERIFIABLE: unity-pipeline-package: …`, naming which case and the path it looked for, exit 3. A refusal, one stderr line and exit 1, is a call to correct; where the script is absent, or the call cannot be corrected, the audit performs the sections above as they say. The sections above stay the rule the script implements.
