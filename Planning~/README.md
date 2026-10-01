# Planning~ — process docs

Process-doc area for the harness workflow (the constitution at `.claude/rules/CONSTITUTION.md`; this project's bindings in `.claude/bindings/`, the layout in `core.md` `## process-docs`). Process docs are written **before/during** implementation and are immutable once locked. Product docs live in `Documentation~/` and are updated only **after** a feature is accepted (G2).

This folder is tracked in git and stripped from every release by `.github/workflows/release.yml`, so it never ships with the package (the `~` suffix also keeps Unity from importing it).

## Layout

- `design-docs/<topic>.md` — design-track docs, one per design topic/branch. Locked at G1, then broken into a feature backlog.
- `features/<feature>/requirements.md` + `features/<feature>/architecture.md` — implementation-track docs, one directory per feature branch. The approved build plan is persisted beside them as `features/<feature>/plan-<id>.md`.
- Root `*.md` files (PRD, vN plans, test matrices, analyses) — pre-workflow history from v1–v4 development. Read-only; new work goes in the directories above.
- `verification-recipe.md` — the full verification procedure the `unity` binding's `verification` key cites, moved verbatim from the pre-harness `.claude/verification-bindings.md` on 2026-10-01.
- `verification-runs/` — kept logs of past verification runs, history.
