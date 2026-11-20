# PR review brief — #<n> <title>

<!-- Written by the orchestrating session to `.scratch/review-pr-<n>-brief.md`. Every review-angle and
     finding-verifier agent reads this file by ABSOLUTE path; it is their entire shared
     context. Anything not written here does not exist for them. -->

## Task

- Agents: `review-angle`, one per angle of the depth profile; `finding-verifier`, one per numbered finding
- Baseline model: `<the agent file's model:>`; downgrade for this dispatch: `<none>`
- Binding to consult: the project's `vc` binding, quoted under `## Scope`
- Task: review PR `#<n>` at `<headRefOid>` on the angles your call prompt gives and return findings, each with `file:line`

## Scope

- PR: `#<n>` — <url>
- Lane / depth profile: <issue | feature | design | prototype | no match> → <slim | prose | trivial | full>
- Head: `<headRefName>` @ `<headRefOid>` (the exact commit under review)
- Base: `<baseRefName>`, merge-base `<base-sha>`
- Base content for comparisons — use EXACTLY this idiom: `git show <base-sha>:<repo-relative-path>`. Repo root is `<absolute path>`; strip it from the paths in the table below to form the repo-relative path.
- Full unified diff, already fetched — read it here: `<absolute path to .scratch/review-pr-<n>.diff>`
- Review intent: <one sentence — what this PR was supposed to accomplish>

## What the PR claims

<!-- Verbatim, not paraphrased — the CLAIM angle audits this section against the diff.
     A paraphrase launders the very wording that needs checking. -->

- Title: <verbatim>
- Linked issue: `#<n>` (parsed from `Closes #…`/`Fixes #…` in the body) — <the reported problem, restated from the issue thread itself, NOT from the PR's summary of it>
- Body claims: <verbatim bullets from the PR body>
- Validation boxes ticked: <verbatim, including who/what supposedly ran each>
- Validation explicitly deferred by the author: <verbatim>

## Changed files

| File (absolute path) | Status | Intent (one line) |
|---|---|---|
| ... | Changed/Added/Deleted | ... |

## Load-bearing invariants

<!-- First, in both lanes: the entries of .claude/references/core/prose-invariants.md,
     verbatim and numbered, under one lead-in line (standing rules no check can read;
     a finding is a breach in the change, never that no check enforces the rule).
     Then from the feature's requirements/architecture docs and ADRs: what verifiers
     refute findings against and what GAP checks for enforcement.
     Issue lane: the linked issue's stated expected behavior follows the list. -->
1. ...
2. ...

## Canon pointers

- Requirements: `<absolute path>` (feature lane; issue lane: the issue thread)
- Architecture: `<absolute path>` (§ the sections bearing on this change)
- Project conventions: `<absolute path to project CLAUDE.md>`
- ADRs in play: ...

## Hot paths

<!-- Paths EFF treats as per-frame/per-tick; everything else is cold unless listed. -->
- ...

## Deliverable

- Findings in `review-angle`'s Output form, each with `file:line`; verdicts in `finding-verifier`'s, one per numbered finding.

## Acceptance criteria for this dispatch

1. ...

## Declared out of scope

<!-- Known intentional changes angles must not report; deferred items with their
     deferral record; adjacent code deliberately left alone. -->
- ...

## Not verifiable from this environment

<!-- Named so angles report them as unverified claims rather than as passing checks. -->
- <e.g. on-device or hardware-only behavior; a host project's test run, if no host is reachable>

## Project rules

- <the lines of the project's `CLAUDE.md` or other standing instruction the task must obey, quoted, or `none`>

## Constraints

- Read-only: no file is written, created, or edited; Bash runs the base-revision read named under `## Scope`, and read-only analysis over the tree, the saved diff and those reads — scripted comparisons, counts, extracts — that prints to its output; nothing is written inside the working tree, and a temporary file, where one is unavoidable, goes outside it in the system's temporary directory and is removed before the report. No version-control command beyond the named read, no PR-host command, no installs.

## Verification command

`<the bound command compile-check runs at POST's CHECK — or: none bound — or: absent>` — expected green: `<pattern, or n/a>`

## Stop-and-report rule

On a missing field, an unreadable base revision or diff file, or a criterion that cannot be met as briefed: stop and report `BLOCKED` naming it. Do not adapt.
