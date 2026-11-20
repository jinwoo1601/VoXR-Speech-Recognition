# The design path

The constitution's **Paths** paragraph has a path's pack hold its stages. These are the design path's, binding on every piece of work on it, each held by the enforcer `## The enforcers` names.

## The steps

1. **G0** — create the design branch; agree the scope and the path. No docs or code before the branch exists.
2. Write the design doc where the process-docs binding says. Explore forks, surface trade-offs, lock decisions. Do not touch product docs.
3. **G1 — design locked (human sign-off; hard stop).**
4. Break the locked design into a feature backlog: feature name, one-line scope and the path each row runs on.
5. Merge the design branch to the main branch.

## The enforcers

| Rule | Enforcer |
|---|---|
| G0: the branch is created and the scope agreed before any doc or code exists. | `g0-open` SCOPE, a hard stop, then BRANCH, both before RECORD writes a doc. |
| The design doc is written where the process-docs binding says. | `g0-open` RECORD writes the design doc's scope section at the target the `process-docs` binding names; where composed, the `system-design-doc` skill's DRAFT step files its draft there; `g1-lock` Prerequisites: the design doc exists where that binding says. |
| Design work does not touch product docs. | `g1-lock` PREFLIGHT: every file changed on the design branch must lie in the process-docs area, and one outside it is reported at PRESENT. |
| G1, the design lock, comes before the design is broken into features, a G1 a path holds inside it included; it is the human's sign-off and a hard stop. | The G1 gate: `g1-lock` HARD STOP, the G1 ruling, before LOCK and BACKLOG; a path pack's own G1 stop, for a G1 a path holds inside it: where composed, `deliverable-outline` HARD STOP, the G1 ruling, before RECORD; where composed, the `system-design-doc` skill's prerequisites leave G1 to `g1-lock`; partial guard: V7, which sees only that an orchestration skill's `stops:` is non-empty. |
| The locked design is broken into a feature backlog: a name, a one-line scope and the path each feature runs on. | `g1-lock` BACKLOG. |
| The design branch is merged to the main branch. | `g1-lock` MERGE, the G1 ruling being the word to merge (`.claude/references/core/prose-invariants.md` `## Entries` entry 1). |
