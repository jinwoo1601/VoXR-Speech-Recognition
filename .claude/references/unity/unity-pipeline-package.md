# unity-pipeline-package — the check's definition

The preflight check this pack declares under `provides.preflight-checks`, run by the gate's preflight audit as one of its items. It asserts that the Unity project carries the `com.unity.pipeline` package, the Editor-side package the Unity CLI's live-Editor commands (`unity command`, `unity run --command`) talk to — without it those commands cannot reach the Editor.

## What it scans

One file: `Packages/manifest.json` at the project root — the directory holding the project's manifest `.claude/harness.json`, which for a Unity project is also the Unity project root, beside `Assets/`. The check reads that file's `dependencies` map for the key `com.unity.pipeline`. File contents only: it opens no Editor and reads nothing under `Library/`.

## What counts as a hit

One row:

- `Packages/manifest.json` reads, and its `dependencies` map has no `com.unity.pipeline` key.

**Not a hit:** the version string the key carries, an `-exp` or pre-release version included, or its source form — a registry version, a `file:` path, or a git URL; `packages-lock.json` disagreeing with the manifest; and the package not yet resolved under `Library/PackageCache`.

## What it reports

The gate auditor's three words, so the audit needs no translation:

- `PASS` — naming the key found and the version string it carries.
- `FAIL` — for the hit, with the remediation: run `unity pipeline install --project-path <project root>`. The human runs it, or the PM on the human's word; it adds the dependency to `Packages/manifest.json`, and the Editor resolves the package on its next open.
- `UNVERIFIABLE` — when `Packages/manifest.json` is absent, which means no Unity project at the root and is not proof the package is absent, and when it does not parse as JSON, saying which of the two it is rather than reporting `FAIL`.

## What it does not do

Run `unity pipeline install` or `unity pipeline upgrade`, or edit the manifest; reach the network, or query the registry; verify that the Editor loaded the package — Safe Mode can stop it loading; assert a version floor; and ruling — the check reports, the human rules at the gate.

## Status

`gate-preflight` is the runner. Its audit reads the project's manifest and takes each composed pack's definition files from `.claude/references/<pack>/`, so a project composing this pack reaches this file at `.claude/references/unity/unity-pipeline-package.md`; the audit performs what is written above and reports it in the three words above, beside its own items. Where there is no manifest to read, the audit marks this UNVERIFIABLE and says so rather than passing it.
