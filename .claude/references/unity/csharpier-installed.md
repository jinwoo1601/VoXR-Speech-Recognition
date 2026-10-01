# csharpier-installed — the check's definition

The preflight check this pack declares under `provides.preflight-checks`, run by the gate's preflight audit as one of its items. It asserts that the C# formatter this pack's hook invokes resolves in this environment, **by the same invocation the hook uses** — a formatter that does not resolve turns the next formatted write into a hook failure, discovered at the gate instead of before it.

## What it scans

The **environment**, by resolving the invocation the pack's hook uses and asking the formatter for its version. The criterion is the hook's invocation, whatever the hook's is: the check reads it from the hook rather than hardcoding a second invocation of its own, so the two cannot drift apart. The hook ships in this pack as `csharpier-hook.py`, with `csharpier-hook.sh` beside it; where it is not present to read, the check reports `UNVERIFIABLE` naming that, rather than guessing an invocation.

## What counts as a hit

One row per category:

- The invocation does not resolve — nothing runs under the name and path the hook uses.
- The invocation resolves but reports no version, which is the same practical state: the hook will fail on the next formatted write.

**Not a hit:** a version older than some floor. This check asserts that the formatter **resolves**, not which release it is; a floor is a different assertion, and nothing in this pack declares one.

## What it reports

The gate auditor's three words, so the audit needs no translation:

- `PASS` — naming the invocation resolved and the version it reported.
- `FAIL` — one row per hit, naming the invocation as the hook writes it, with the remediation: install the formatter so that invocation resolves in the environment the hook runs under.
- `UNVERIFIABLE` — when the hook is not present to read the invocation from, saying so rather than substituting an invocation of the check's own.

## What it does not do

Install or update anything; format a file, or judge whether any file is formatted; read or validate the formatter's configuration; assert a version floor; and ruling — the check reports, the human rules at the gate.

## Status

`gate-preflight` is the runner. Its audit reads the project's manifest and takes each composed pack's definition files from `.claude/references/<pack>/`, so a project composing this pack reaches this file at `.claude/references/unity/csharpier-installed.md`; the audit performs what is written above and reports it in the three words above, beside its own items. Where there is no manifest to read, the audit marks this UNVERIFIABLE and says so rather than passing it.
