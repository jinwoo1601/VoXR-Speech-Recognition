You're picking up: `phase-pickup` for `feat-windows-native-dotnet-fixtures` Phase 2. Read the files below, then start at Next steps.

## Objective

Plan Phase 2 of `windows-native-dotnet-fixtures` (host-neutral `generate.sh` and the closing gate): recon, plan, persist `plan-2.md`, validate, commit. Done = `plan-2.md` persisted, validated, committed and pushed, ready for `implement`.

## Lane

feature

## Context

- Row 2 of the locked design `Planning~/design-docs/windows-native-toolchain.md`. Branch `feat-windows-native-dotnet-fixtures`, one checkout (`trees` none), project root `D:\Workspace\VoXR-Speech-Recognition`. No board; `memory/STATUS.md` is the state.
- Phase 2's row: `Planning~/features/windows-native-dotnet-fixtures/architecture.md` `## Build plan` (:124), with its unrecorded-behaviour list (:130) and assets (:125). Requirements: `Planning~/features/windows-native-dotnet-fixtures/requirements.md`. Discharges F3, F4, F7, F9, the NFRs for `generate.sh` and Commands 1-3; closes WSL-1 at G2.

## Current state

- Phase 1 built, gated green and committed: `e6b98be`, pushed. It covers the binding (`.claude/bindings/unity.md` Commands 3-4, Prerequisites, Shell, Path form), the recipe (`Planning~/verification-recipe.md`) and the `vosk_api.h` :3 comment. Gate evidence is in `memory/2026-10-14.md`, in the entry "Phase 1 implement". Reports are at `.scratch/compile-check-windows-native-dotnet-fixtures-phase-1-{gate,canary-a,canary-b}-report.md`.
- Phase 1 has not been reconciled into `architecture.md` yet (as-built pass later, before G2).

## Key files & locations

- `Planning~/features/windows-native-dotnet-fixtures/plan-1.md`: format precedent for `plan-2.md` (same directory).
- `generate.sh` (the Tests~ fixture generator; the architecture doc's *Components* names its path and edits); adr-0005 (venv layout, interpreter).
- Precedent recon briefs: `.scratch/code-mapper-windows-native-dotnet-fixtures-phase-1-brief.md`, `.scratch/doc-extractor-windows-native-dotnet-fixtures-phase-1-brief.md`; architect/validator: `.scratch/architect-windows-native-dotnet-fixtures-phase-1-brief.md`, `.scratch/plan-validator-windows-native-dotnet-fixtures-phase-1-brief.md`.

## Constraints & decisions

- **vc-guard:** subagents may not run git writes, `git checkout --` included. On 2026-10-14 the hook refused Phase 1's gate-3 revert. The human ruled "PM reverts": the gate split into two `compile-check` runs, and the PM ran `git checkout --` between them. Phase 2's F3 check (one fixture regenerated, then restored) and any other gate revert must be planned the same way: the restore is a PM step between dispatches. As-built delta for the reconcile: Phase 1's gate 3 revert was a PM step.
- Agents cannot download. The human's `!` step covers the pip and voice downloads (architecture :125).
- Builds and tests run only in `compile-check`, never on the main thread. Agents can't write `.scratch/` report files, so the PM files each report from the subagent transcript. The helper is at `C:\Users\excal\.claude\jobs\be6bd62b\tmp\file_report.py <agent id> <out>`, and it may be gone; call it with the `C:/…` path form. Under `MSYS_NO_PATHCONV=1`, a `/c/…` argument to python is not converted.
- Don't write WSL or `/mnt/` anywhere new. Don't touch `.claude/settings.local.json`, history docs, or product docs. Leave `.claude/references/vc-git/__pycache__/` untracked. Commit Unity-generated `memory/**/*.meta`, and never delete them.
- No CHANGELOG line (dev tooling).
- Two sessions are named `voxr-hub`: one on this machine and a stale Remote Control session on another machine. A bare-name send fails as ambiguous, so send to the one `ListAgents` shows as on this machine, by its `[ref]`.

## Next steps

1. Invoke `phase-pickup` for `windows-native-dotnet-fixtures` Phase 2, starting at ORIENT. The earlier session's phase boundary is spent, so ORIENT continues to RECON.

## Open questions

- Listed for the next gate (carried): adr-0005's Consequences text still names only the venv layout and interpreter (covered by the path-form ruling). The recipe's sanity pipeline `tr '\\' '/'` prints a portability warning in Git Bash, with the count unaffected; noted for review.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-windows-native-dotnet-fixtures; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 49fb79e7-6f64-4aa2-b6b8-dbea2f135594". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer.
