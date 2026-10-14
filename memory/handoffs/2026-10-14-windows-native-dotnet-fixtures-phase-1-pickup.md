You're picking up: `phase-pickup` for `feat-windows-native-dotnet-fixtures` Phase 1, from ORIENT. Read the files below, then start at Next steps.

## Objective

Plan Phase 1 of `windows-native-dotnet-fixtures`: recon, plan, persist `Planning~/features/windows-native-dotnet-fixtures/plan-1.md` (with its link line in the architecture doc's `## Build plan`), validate, reconcile, commit; then `implement` Phase 1. Done = Phase 1 planned, validated, built and gated, or a handoff at the context bound.

## Lane

feature

## Context

- Row 2 of the locked design `Planning~/design-docs/windows-native-toolchain.md` (G1 2026-10-13), Full track. Row 1 `windows-native-harness` is merged (PR #175).
- Branch `feat-windows-native-dotnet-fixtures`, one checkout (`trees` none), clean at `ee66878`, pushed. No board; `memory/STATUS.md` is the state. This session runs in the project root; the hub session `voxr-hub` also lives there but only reads report lines - you are the one writer.
- G0 2026-10-14 ("Fold it in. Agreed. Proceed": WNH-1 folded in as In item 7). SETTLE ("Agreed"): DocCheck procedure goes in the recipe; `generate.sh` checked by regenerating one fixture then restoring it; the sweep's live-area list. PLAN ("All recommended"): two phases; `generate.sh` may convert the path form it passes to Python (`cygpath -m` on the cache root), within adr-0005, no reopen.

## Current state

- `Planning~/features/windows-native-dotnet-fixtures/requirements.md`: complete (F1-F9, F4 Should; two Must NFRs; no human-only G2 verification).
- `Planning~/features/windows-native-dotnet-fixtures/architecture.md`: complete; body 31,171 of 40,960 bytes (keep plan detail in the plan file). `## Build plan` holds the two ruled phases, no plan links yet. `## Data contract & runtime flow` holds the exact bound text for every edit.
- Phase 1 (from `## Build plan`): every edit to `.claude/bindings/unity.md` (:9, :12, :15, :17), `Planning~/verification-recipe.md` (:3, :17, :24, `### WSL Usage` -> `### Git Bash usage`, `## Compiling Samples~`, a new DocCheck section) and `NativeBridge~/include/vosk_api.h:3`. Green: `ls` on the recipe's adb path; Command 3 (NativeBridge~ changed; no `.so` recommit); Command 4 verbatim (mutation red/green, sanity count 7, negative run); DocCheck verbatim exits 0; Command 4 and DocCheck under `MSYS_NO_PATHCONV` set and unset. Discharges F1, F2, F5, F6, F8, both NFRs for Command 4 and DocCheck. Its runtime claims are listed in the architecture doc's Risks.
- Filed reports (trust them): `.scratch/architect-windows-native-dotnet-fixtures-report.md`, `.scratch/doc-writer-windows-native-dotnet-fixtures-requirements-report.md`.

## Key files & locations

- Docs: `Planning~/features/windows-native-dotnet-fixtures/{requirements.md,architecture.md}`; design + `Planning~/design-docs/windows-native-toolchain/decisions/adr-0001...adr-0006`; row 1 precedent `Planning~/features/windows-native-harness/{architecture,plan-1,plan-2,plan-3}.md`.
- Sites: above; DocCheck rig `Planning~/features/coverage-in-selection/ab-rig/` (`stage.sh` unchanged, `Rig.csproj`, `Program.cs` `--doccheck`).

## Constraints & decisions

- Don't write WSL or `/mnt/` into anything new. Don't touch `.claude/settings.local.json`, history docs, or product docs (`Documentation~/`, `KNOWN_LIMITATIONS.md`, `CHANGELOG.md`).
- Claude Code's Git Bash sets `MSYS_NO_PATHCONV=1`. Use absolute paths in Bash (`cd` persists).
- Subagents can't write report files under `.scratch/` (PM files them) and can't download (refused by permission policy - never retried in another form). A plan needing a download names a human `!` step. Agents have no byte counter: the PM measures doc sizes with `wc -c`.
- Unity runs generate `memory/**/*.meta` for new notes: commit them, never delete as orphans.
- Leave `.claude/references/vc-git/__pycache__/` untracked.
- Listed for the next gate: adr-0005's Consequences text still names only the venv layout and interpreter; the path-form ruling reads it as within adr-0005 (no reopen). The PM's BAR fix made F7's sweep text-only (`git grep -I`).

## Next steps

1. Invoke `phase-pickup` for feature `windows-native-dotnet-fixtures`, Phase 1.
2. Then `implement` Phase 1.

## Open questions

- None pending for the human.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-windows-native-dotnet-fixtures; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps. BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer.
