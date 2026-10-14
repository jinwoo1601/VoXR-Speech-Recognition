You're picking up: the architect trim of the windows-native-harness architecture doc to its 40 KB body budget, then the check-in, then G2 (`g2-accept`). Read the files below, then start at Next steps.

## Objective

Bring `Planning~/features/windows-native-harness/architecture.md` under the 40 KB body budget by an `architect` fix pass (human ruling below), commit the as-built reconcile + trim + pending memory edits, then take `feat-windows-native-harness` through `g2-accept`. Done = body under 40,960 bytes, committed and pushed, and the human's G2 ruling.

## Lane

feature

## Context

- Design `windows-native-toolchain` G1 LOCKED 2026-10-13; this feature is its backlog row 1, **Full** track. Row 2 `windows-native-dotnet-fixtures` follows after this merges. `feat-joined-rig-parser`, `release`, `lab-next-level-probes` stay held (human 2026-10-13).
- Branch `feat-windows-native-harness`, one checkout (`trees` none), head `58c17de`, pushed. No board; `memory/STATUS.md` is the state.
- Body budget: `.claude/bindings/core.md:8` — 40 KB over `Planning~/features/<feature>/architecture.md`; `memory/doctor-rulings.md` covers 8 older oversized docs, not this one.

## Current state

- Phases 1-3 built and gated (builds `bd8443e`, `5c10602`, `84ac534`).
- `review-cycle` done: 3 findings verified; 1 (README `vendor/` never created) and 2 (stale "GCC on desktop" LTO comment) CONFIRMED and fixed by default in `58c17de`; 3 REFUTED. CHECK after the fixes green: EditMode 203/203, PlayMode 704/704, Command 3 arm64 symbol diff empty, harness 16/16, `Runtime/` diff empty. Collation `.scratch/review-windows-native-harness-collation.md` (list findings 1-2 with their verdicts at G2).
- As-built reconcile written by `architect-reconcile`, **uncommitted**: four `## As-built deltas` entries, two dated register amendments, in-place corrections logged. Body (excluding frontmatter) now **42,634 bytes**; per section: Decision register 13,556, As-built deltas 5,874, Build plan 4,906, Data contract 4,473, Context 3,262, Risks 3,044.
- Uncommitted also: `memory/2026-10-14.md` (review/reconcile entry), `memory/STATUS.md` (feature line; new deferral WNH-1), `memory/notes-for-future-features.md` (3 lessons). Untracked `.claude/references/vc-git/__pycache__/` — leave it.

## Key files & locations

- Target: `Planning~/features/windows-native-harness/architecture.md` (`## Build plan` — the phase bullets Phase 1/2/3 carry plan-time detail that `plan-1.md`, `plan-2.md`, `plan-3.md` beside it already persist).
- Reconcile brief/report: `.scratch/architect-reconcile-windows-native-harness-asbuilt-brief.md`; requirements `Planning~/features/windows-native-harness/requirements.md`.
- Commit-message precedent: `.scratch/commit-windows-native-harness-review-fix.txt`.

## Constraints & decisions

- **The human, 2026-10-14:** "Trim via architect (Recommended)" — an `architect` fix pass condenses `## Build plan`'s phase bullets to scope + discharges (each row: phase id, scope, requirements discharged, plus its plan link line), bringing the body under 40 KB; no Decision register entry, amendment or As-built deltas entry removed; plan files untouched.
- Measure the body with `awk 'BEGIN{n=0} /^---$/{n++; next} n>=2' <doc> | wc -c` (bytes after the frontmatter) and the whole file with `wc -c`; target under 40,960.
- Product docs (`Documentation~/native-bridge.md:59-61`, `CHANGELOG.md:74`) only after G2, by `codex-reconcile`; no CHANGELOG line for this feature (dev tooling).
- Row 2 owns Command 4, DocCheck, `generate.sh`, the binding's Prerequisites/Shell/Path-form lines and the recipe's other WSL sections.
- Agent network downloads are refused by policy; subagents cannot write under `.scratch/` — the PM files their reports from the hand-back.
- Check-in per the `vc` binding: `git add -- <paths>`, `git commit -F <file> -- <paths>`, `git push`.

## Next steps

1. Dispatch `architect` (fix pass) from a brief filed at `.scratch/architect-windows-native-harness-budget-trim-brief.md` carrying the ruling above; measure the body; file its report.
2. Commit `architecture.md` and the three `memory/` files; push.
3. Invoke `g2-accept` for `feat-windows-native-harness`.

## Open questions

- None pending for the human before G2.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-windows-native-harness; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor ed2be3b1-1f14-4c4d-a960-15a71b5192fb". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; send to the one on this machine. If the bare name is refused as ambiguous, use the ref `ListAgents` shows for the local one.)
