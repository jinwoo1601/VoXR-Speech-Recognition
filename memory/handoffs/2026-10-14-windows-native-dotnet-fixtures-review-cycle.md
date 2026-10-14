You're picking up: `review-cycle` for `feat-windows-native-dotnet-fixtures`. Read the files below, then start at Next steps.

## Objective

Run the multi-agent review of the whole feature branch `feat-windows-native-dotnet-fixtures` against `main` (both phases built and gated green). Done = review findings verified, the CONFIRMED ones fixed and re-gated, the review recorded, committed and pushed; next is the as-built reconcile of `architecture.md` and G2.

## Lane

feature

## Context

- This is row 2 of the locked design `Planning~/design-docs/windows-native-toolchain.md`. Branch `feat-windows-native-dotnet-fixtures`; one checkout (`trees` none); project root `D:\Workspace\VoXR-Speech-Recognition`. There is no board: `memory/STATUS.md` is the state.
- The feature docs are `Planning~/features/windows-native-dotnet-fixtures/requirements.md`, `architecture.md`, `plan-1.md` and `plan-2.md`.
- Review profile: `full`, because G2 is at stake. The angles are in `.claude/bindings/core.md` `## review`.

## Current state

- Phase 1 is built at `e6b98be`: the verification binding `.claude/bindings/unity.md` (Commands 3-4, Prerequisites, Shell, Path form), `Planning~/verification-recipe.md`, and the `NativeBridge~/include/vosk_api.h` :3 comment.
- Phase 2 is planned at `32ca848` and built at `06e3385`, pushed. It is `Tests~/Fixtures/generate.sh`, made host-neutral (interpreter probe, venv layout, `cygpath -m`, header).
- The gate evidence for both phases is in `memory/2026-10-14.md`, in the entries "Phase 1 implement" and "Phase 2 implement". The reports are `.scratch/compile-check-windows-native-dotnet-fixtures-phase-{1,2}-*-report.md`. Every runtime claim was true.
- Not done yet: the review, the as-built reconcile of `architecture.md` (both phases), and G2.

## Key files & locations

- Changed on the branch (`git diff --name-status main..HEAD`): `.claude/bindings/unity.md`, `Planning~/verification-recipe.md`, `NativeBridge~/include/vosk_api.h`, `Tests~/Fixtures/generate.sh`, plus the process docs and `memory/`.
- Precedent review record: row 1's `memory/handoffs/2026-10-14-windows-native-harness-review-cycle.md`.

## Constraints & decisions

- **vc-guard:** subagents may not run git writes, `git checkout --` included. Any revert or restore is a PM step between dispatches (the human's "PM reverts" ruling, 2026-10-14).
- **Builds and tests run only in `compile-check`.** Agents can't write `.scratch/` reports, so the PM files each one from the transcript with `python C:/Users/excal/.claude/jobs/be6bd62b/tmp/file_report.py <agent id> <out>`. Use the `C:/` path form; the helper may be gone.
- **A Commands 1-2 brief must exclude `memory/**/*.meta` from the revert's orphan removal.** Phase 2's gate deleted 7 untracked Unity-generated `memory/handoffs/*.md.meta` files. The standing rule is to commit Unity-generated `memory/**/*.meta` and never delete them; commit the regenerated ones after the next host import.
- Don't write WSL or `/mnt/` anywhere new.
- Don't touch `.claude/settings.local.json`, history docs, or product docs (product docs come after G2). Leave `.claude/references/vc-git/__pycache__/` untracked.
- No CHANGELOG line (dev tooling).
- `core.autocrlf` is `true`; working copies are LF, and Git warns about CRLF on add. This is harmless: Phase 1 proved Git Bash runs CRLF scripts.
- Two sessions are named `voxr-hub`. Send to the one `ListAgents` shows on this machine, by its `[ref]`.

## Next steps

1. Invoke `review-cycle` on `feat-windows-native-dotnet-fixtures` against `main`, `full` profile.
2. Then, still before G2: `architecture-doc` reconcile to as-built (via `architect-reconcile`). The as-built deltas to log are listed in `memory/2026-10-14.md` under "Phase 2 implement" → Decisions, and "Phase 1 implement" → Decisions:
   - F3's restore was the PM's (:108);
   - Phase 1's gate-3 revert was the PM's;
   - empty `VOXR_PYTHON` reads as unset (:59);
   - Command 3's bound line runs only the desktop half;
   - `:37`'s `:22-39` should be `:22-40`.

## Open questions

Carried to the next gate:
- adr-0005's Consequences text still names only the venv layout and interpreter; the path-form ruling covers it.
- The recipe's sanity pipeline `tr '\\' '/'` prints a portability warning in Git Bash; the count is unaffected.
- The 7 deleted `memory/handoffs/*.meta` files (see Constraints).

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-windows-native-dotnet-fixtures; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 00c6493a-75bc-46c6-860e-f3a69f5487bb". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer.
