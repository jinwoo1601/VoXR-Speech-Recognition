# meta-files-paired — the check's definition

The preflight check this pack declares under `provides.preflight-checks`, run by the gate's preflight audit as one of its items. It asserts that every asset the change touches carries its `.meta` in the same change: Unity keeps an asset's GUID in its `.meta` file, so an asset that lands without one, or a `.meta` orphaned from its asset, breaks every reference to that asset in every scene, prefab and serialised field in the project.

## What it scans

The **changed-file list** for the range the audit names, and nothing else — read through the `vc` binding's *Base-revision reads* slot. Not the working tree, not history outside the range, not assets the range leaves untouched. It pairs paths; it does not read file contents.

## What counts as a hit

One row per category:

- An asset added without its `.meta`.
- A `.meta` added without its asset.
- An asset deleted with its `.meta` left behind.
- A moved or renamed asset whose `.meta` did not move with it.

**Not a hit:** a path Unity generates no `.meta` for — anything outside the project's asset and package roots (a package root is an individual package's own directory, so the project-level package-manager manifests that sit above them carry no `.meta` and are not in scope); a file or folder whose name begins with `.`; one whose name ends with `~`, which is why a `Documentation~` folder carries none; one named `cvs`; and a `.tmp` file.

## What it reports

The gate auditor's three words, so the audit needs no translation:

- `PASS` — naming the range scanned.
- `FAIL` — one row per hit, `<repo-relative path> — <category>`, with the remediation: add the missing `.meta` as Unity generated it, or delete the orphan.
- `UNVERIFIABLE` — when the changed-file list is not resolvable through the `vc` binding, saying which part is not resolvable.

## What it does not do

A working-tree scan; a Unity import, or any claim about the asset database; repair of a missing `.meta` — a `.meta` Unity regenerates carries a new GUID, a different asset identity, which is the damage this check exists to prevent; and ruling — the check reports, the human rules at the gate.

## Status

`gate-preflight` is the runner. Its audit reads the project's manifest and takes each composed pack's definition files from `.claude/references/<pack>/`, so a project composing this pack reaches this file at `.claude/references/unity/meta-files-paired.md`; the audit performs what is written above and reports it in the three words above, beside its own items. Where there is no manifest to read, the audit marks this UNVERIFIABLE and says so rather than passing it.
