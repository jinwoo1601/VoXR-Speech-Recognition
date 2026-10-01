# no-secrets-in-diff — the check's definition

The preflight check this pack declares under `provides.preflight-checks`, run by the gate's preflight audit as one of its items. It is a check and not an agent: the gate auditor runs it and reports its result alongside its own items, which is the half of the security question the harness adopted (the other half, dependency and CVE scanning, was not).

## What it scans

The **added and modified lines** of the range the audit names, and nothing else — the changed-file list for that range, and each changed file's diff, read through the `vc` binding's *Base-revision reads* slot. Not the working tree, not history outside the range, not dependencies, not files the range leaves untouched.

## What counts as a hit

One row per category:

- A private-key or certificate block — a `-----BEGIN … PRIVATE KEY-----` opener.
- A credential assignment with a literal value — a `password`, `secret`, `token`, `api key`, or `access key` name on the left of `=` or `:`.
- A URL or connection string carrying inline credentials.
- A provider token with a recognizable fixed prefix.
- An added credential or environment file (`.env`, a `*.pem`, a private-key file) that the change does not also add to the ignore file.

**Not a hit:** a placeholder or obvious example value; a fixture the repo already declares as such; a *removed* line — that secret is already published, so it is named as a rotation note and never as a hit clearable by reverting.

## What it reports

The gate auditor's three words, so the audit needs no translation:

- `PASS` — naming the range scanned.
- `FAIL` — one row per hit, `<repo-relative path>:<line> — <category>`, with **the matched value never quoted or echoed** (a report that quotes a secret republishes it), and the remediation: remove it from the change and rotate the credential.
- `UNVERIFIABLE` — when the diff is not resolvable through the `vc` binding, saying which part is not resolvable.

## What it does not do

CVE or dependency scanning; entropy scoring of arbitrary strings; anything outside the named range; and ruling — the check reports, the human rules at the gate.

## The mechanical part

`pre-commit-check.py`, beside this file, does the pattern half. For the audited range the runner first runs `python3 .claude/references/vc-git/pre-commit-check.py --range <base>..<head>` and cites its output lines as the evidence: the `PASS — <range>` or `FAIL — <range>` line, each row, and the closing `pre-commit hook: installed | not installed | a different hook` line. An `UNVERIFIABLE — <reason>` line, at exit 2, is a range that does not resolve. The script reads added lines only, writes each row in the form above, save a credential-file row, which reads `<repo-relative path> — credential file` since a file has no line, and never writes the value, and flags every category above by pattern — a private-key opener, a credential name assigned a literal of eight or more characters, a URL carrying a password, a provider token by its fixed prefix, an added credential or environment file by its name — and a conflict marker besides. It already passes over the plain placeholders: a value opening with `$`, `%`, `<`, `@` or a bracket, one holding a template brace, one character repeated, one naming an example, placeholder, changeme, dummy, fake, redacted or xxxx value, an expression such as a call, an attribute read or an index, a hash digest such as `sha256:<hex>`, or a name that is a file path.

The runner then judges what the script cannot — a flagged value that is a placeholder all the same, a fixture the repo declares, and the ignore-file clause of the credential-file category — and reports in this file's three words.

## The pre-commit hook

`python3 .claude/references/vc-git/pre-commit-check.py --install`, run once per clone, writes a four-line `pre-commit` shim into the repository's hooks directory — the one `git rev-parse --git-path hooks` names, which follows `core.hooksPath`, and which every tree of the repository shares — so each commit's staged change is scanned first, and a hit refuses the commit with its rows. The shim exits quietly where the tree being committed has no copy of the script. Run again, `--install` answers `already installed`; where another `pre-commit` hook holds the name it refuses, at exit 1, and leaves that file untouched — joining the two is the human's call. Skipping the hook with `--no-verify` is denied, for `git commit` and `git push` alike, by this pack's settings template.

## Status

`gate-preflight` is the runner. Its audit reads the project's manifest and takes each composed pack's definition files from `.claude/references/<pack>/`, so a project composing this pack reaches this file at `.claude/references/vc-git/no-secrets-in-diff.md`; the audit performs what is written above and reports it in the three words above, beside its own items. Where there is no manifest to read, the audit marks this UNVERIFIABLE and says so rather than passing it.
