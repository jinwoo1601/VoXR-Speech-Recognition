# Review brief — <feature / phase>

<!-- Written by the review-cycle skill into .scratch/review-<feature>-brief.md before fan-out. Every review-angle and finding-verifier agent
     reads this file by ABSOLUTE path; it is their entire shared context — anything not written here does not exist for them. Common fields per
     .claude/references/core/delegation-contract.md, then the review extensions. Load-bearing invariants and canon pointers are its inputs. -->

## Task

- Agents: `review-angle`, one per angle listed under `## Angles`; `finding-verifier`, one per finding
- Baseline model: `<the agent file's model:>`; downgrade for this dispatch: `<none>`
- Binding to consult: the project's `vc` binding, quoted under `## Scope`
- Task: review `<the change set>` on the angles below and return findings, each with `file:line`

## Scope

- Revision range: `<base>` → `<head>`, as the `vc` binding names them
- Base revision for comparisons: `<revision>`, read with the idiom the `vc` binding names for "a file at a revision"; repo root `<absolute path>` — strip it from the table's paths
- Review intent: `<one sentence — what this change set was supposed to accomplish>`

## Angles

<!-- The angles of the depth profile review-cycle's ## Depth profiles chooses: the profile's name, the angles it runs, and where they came from (default or the review binding), with the lane and class read that chose it and any narrowing the human ruled. The full profile's composed roster set is .claude/references/core/angle-roster.md plus each composed pack's .claude/references/<pack>/angle-roster.md. -->
- ...

## Changed files

| File (absolute path) | Status | Intent (one line) |
|---|---|---|
| ... | Changed/Added/Deleted | ... |

## Load-bearing invariants

<!-- First, the entries of .claude/references/core/prose-invariants.md, verbatim and numbered, under one lead-in line: standing rules no check can read, so a finding against one is a breach of it in the change, never the observation that no check enforces it. Then the feature's own, from its requirement and architecture docs and its decision records: what verifiers refute findings against, and what GAP checks for enforcement. For a lite feature its backlog row and the locked design stand for its requirement doc (`.claude/references/core/feature-lite.md` `## The acceptance source`). -->
1. ...

## Canon pointers

- Requirements: `<absolute path>` — for a lite feature, by the test of `.claude/references/core/feature-lite.md` `## The mark`, the design's backlog, the feature's row quoted, with the word `lite` and the feature's name (`.claude/references/core/feature-lite.md` `## The acceptance source`)
- Architecture: `<absolute path>` (§ the sections bearing on this change)
- Decision records in play: ...

## Hot paths

<!-- Paths EFF treats as hot — per-frame, per-tick, per-request; everything else is cold unless listed. -->
- ...

## Deliverable

- Findings in `review-angle`'s Output form, each with `file:line`; verdicts in `finding-verifier`'s, one per finding.

## Acceptance criteria for this dispatch

1. ...

## Declared out of scope

<!-- Known intentional removals or changes angles must not report; deferred items with their deferral record; adjacent code left alone deliberately. -->
- ...

## Constraints

- Read-only: no file is written, created, or edited; Bash runs the base-revision reads named under `## Scope`, and read-only analysis over the tree and those reads — scripted comparisons, counts, extracts — that prints to its output; nothing is written inside the working tree, and a temporary file, where one is unavoidable, goes outside it in the system's temporary directory and is removed before the report. No version-control command beyond the named reads, no installs.

## Verification command

`<the bound command compile-check runs at the skill's CHECK step — or: none bound>` — expected green: `<pattern, or n/a>`

## Stop-and-report rule

On a missing field, an unreadable base revision, or a criterion that cannot be met as briefed: stop and report `BLOCKED` naming it. Do not adapt.
