You're picking up: `review-cycle` CHECK and COMMIT for `feat-windows-native-dotnet-fixtures`. Read the files below, then start at Next steps.

## Objective

Finish the review cycle: gate the three applied (uncommitted) review fixes through `compile-check`, then commit and push them with the review record. Done = CHECK green with evidence, the fix commit pushed; next is the as-built reconcile of `architecture.md` and G2.

## Lane

feature

## Context

- Row 2 of the locked design `Planning~/design-docs/windows-native-toolchain.md`. Branch `feat-windows-native-dotnet-fixtures`; one checkout (`trees` none); project root `D:\Workspace\VoXR-Speech-Recognition`. No board: `memory/STATUS.md` is the state.
- Feature docs: `Planning~/features/windows-native-dotnet-fixtures/requirements.md`, `architecture.md`, `plan-1.md`, `plan-2.md`.
- `review-cycle` ran profile `full`, 11 angles. Steps BRIEF, FAN OUT, DEDUP, VERIFY, RULE and FIX are done; CHECK and COMMIT remain. There was no hard stop: all three findings were CONFIRMED and fixed by default, and they are listed at G2.

## Current state

- The review brief is `.scratch/review-windows-native-dotnet-fixtures-brief.md`. The collation (findings, verdicts, Notes, ruling) is `.scratch/review-windows-native-dotnet-fixtures-collation.md`.
- The three findings, all minor:
  1. Relative `cd` left the shell in the scratch directory, so a repeat from the same shell failed. Fixed with subshells.
  2. `VOXR_PYTHON` is split on spaces. Confirmed as a clarity issue; the script header and error text now say so.
  3. The sanity pipeline `tr '\\' '/'` warned under the agent Bash tool. Fixed with `tr '\134' '/'`.
- Fixes applied, **uncommitted** in the working tree (`git diff --stat`: 4 files, 15+/13-):
  - `.claude/bindings/unity.md` Command 4 (:9) is now `(cd .scratch/samples-compile && dotnet build SamplesCompile.csproj -v m -nologo)`.
  - `Planning~/verification-recipe.md`:
    - :84 is `(cd "$SP" && dotnet build …)`.
    - :136-137 is `(cd "$SP" && … | tr '\134' '/' | … | wc -l)`.
    - :227 DocCheck is `… WORKTREE .scratch/doccheck && (cd .scratch/doccheck && dotnet build -v q --nologo && dotnet run --no-build -- --doccheck)`.
  - `Tests~/Fixtures/generate.sh` has two header lines (:15-16) and an extended `VOXR_PYTHON` error text (:33).
  - `Planning~/features/windows-native-dotnet-fixtures/architecture.md` has four `**Amended 2026-10-14:**` register clauses (csproj location, sanity count, DocCheck shape, interpreter) and in-place Data-contract corrections.
- The fix briefs are `.scratch/architect-windows-native-dotnet-fixtures-review-fix-brief.md` and `.scratch/implement-windows-native-dotnet-fixtures-review-fix-brief.md`. The code-writer returned PARTIAL: edits exact, but it could not run its criterion-7 read-only checks (outside its Bash scope). The PM read `git diff` and it matches the dictation; checks 7d (`bash -n`), 7f (F7 sweep) and 7g (subshell probes) fold into CHECK.

## Key files & locations

- The verification binding is `.claude/bindings/unity.md` `## verification`; the recipe is `Planning~/verification-recipe.md` (`## Compiling Samples~` :70-, `## Parser A/B rig — DocCheck` :222-).
- The precedent CHECK brief is `.scratch/compile-check-windows-native-harness-review-fix-brief.md`. The Phase 1 and 2 gate briefs are `.scratch/compile-check-windows-native-dotnet-fixtures-phase-{1-gate,2-gate-a}-brief.md` (Command 4, DocCheck, and the negative-run idioms).
- The check-in procedure is in `.claude/bindings/vc-git.md` `## vc`: `git add -- <paths>`, `git commit -F <file> -- <paths>`, then `git push`.

## Constraints & decisions

- **Builds and tests run only in `compile-check`.** Agents can't write `.scratch/` reports, so the PM files each one from the transcript, with `python C:/Users/excal/.claude/jobs/be6bd62b/tmp/file_report.py <agent id> <out>` if the helper still exists.
- **vc-guard:** subagents may not run git writes (`git checkout --` included). Any revert is a PM step between dispatches.
- CHECK scope is what the fixed sites call for. Commands 1-2 are not needed (no `Runtime/` change) and Command 3 is not needed (no `NativeBridge~/` change). The mutation canary is not needed (the csproj listing is unchanged).
- If a Commands 1-2 brief is ever written, it must exclude `memory/**/*.meta` from the revert's orphan removal.
- Don't write WSL or `/mnt/` anywhere new. Don't touch `.claude/settings.local.json`, history docs, or product docs. Leave `.claude/references/vc-git/__pycache__/` untracked. No CHANGELOG line (dev tooling).
- `core.autocrlf` is `true`; Git's CRLF warning on add is harmless.
- The Bash tool halves backslashes you type (`x='\\'; echo ${#x}` → 1). Keep that in mind when a brief asks an agent to grep for `'\134'`.

## Next steps

1. Re-enter `review-cycle` at **CHECK**. Dispatch `compile-check` from a brief under `.scratch/` with these checks, all from the project root in Git Bash:
   - (a) The csproj at `.scratch/samples-compile/SamplesCompile.csproj` exists (regenerate from the recipe's listing if not). Command 4 verbatim from the binding, run **twice in the same shell**. Each run must show `Build succeeded`, `0 Error(s)` and only the CS0618 warning, and `pwd -W` after each must print the project root.
   - (b) The recipe's sanity block verbatim (with `SP=".scratch/samples-compile"` set): it must print `7`, with no `tr: warning` line.
   - (c) DocCheck verbatim from recipe :227, twice in the same shell: exit 0, 66/66, no `FAIL` line, and `pwd -W` = the root after each.
   - (d) (a) and (c) once more under `env -u MSYS_NO_PATHCONV`, with the same results.
   - (e) `bash -n Tests~/Fixtures/generate.sh` exits 0. Then the negative run: `XDG_CACHE_HOME` set to an absolute, empty `.scratch/` directory and `VOXR_PYTHON=no-such-python`. Expected: exit 1, with stderr the new text `generate.sh: VOXR_PYTHON=no-such-python does not run as Python 3 (it is split on spaces, so a path containing spaces cannot be used)`, no venv made.
   - (f) The F7 sweep prints nothing: the `git grep` in the review brief invariant 9, exit 1.
   - (g) The F9 diff `git diff main -- Runtime/ Samples~/ Tests~/Fixtures/audio/` is empty.

   File the report. If anything is red, run `debugger` and do not patch on the main thread.
2. **COMMIT**: write a message file under `.scratch/` that summarises the three findings, then commit the four fix files plus `memory/` per the vc binding, then `git push`.
3. Then, still before G2: run `architecture-doc` reconcile to as-built via `architect-reconcile`. The deltas to log:
   - The five listed in `memory/2026-10-14.md` under "Phase 1 implement" and "Phase 2 implement" → Decisions:
     - F3's restore was the PM's (:108).
     - Phase 1's gate-3 revert was the PM's.
     - Empty `VOXR_PYTHON` reads as unset (:59).
     - Command 3's bound line runs only the desktop half.
     - The `:22-39` citation should read `:22-40`.
   - The review fix pass's Data-contract corrections (Command 4 quote, the :80-85 item, the `generate.sh` header description). Their register amendments are already in place.
   - Then `g2-accept`.

## Open questions

Carried to G2:
- adr-0005's Consequences text still names only the venv layout and interpreter; the path-form ruling covers it.
- The 7 Unity-generated `memory/handoffs/*.md.meta` files deleted by Phase 2's gate. Commit the regenerated ones after the next host import.
- Review Note: Command 3's `cd NativeBridge~ && …` (row 1's text, out of this feature's scope) has the same leave-the-shell-elsewhere shape fixed here for Command 4. Should it be fixed, and if so where?

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-windows-native-dotnet-fixtures; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor ebc3f0ba-4106-45c6-8af2-5d698cccaef9". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer.
