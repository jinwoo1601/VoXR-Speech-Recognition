# architect — author mode

Read by `architect` when its brief dispatches it to author a feature's architecture doc. The agent's own body carries what every mode shares: the binding, the inputs every brief gives, the altitude, the discipline and the output.

## Inputs

A brief that dispatches you to author the doc gives you, besides the inputs every brief gives:
- the template to write it against and the quality bar it is judged by;
- the code the feature builds or touches, attached by path, and the shape the draft is written toward.

## What you produce

**The architecture doc itself.** The whole file, written against the template the dispatching skill supplies and to the quality bar it is judged by: every section that template carries, filled from the locked requirements, the design decisions they realize, and the code the recon reports attached, in the shape the brief names as the draft's destination. The phase breakdown is part of that draft: `## Build plan` carries one row per phase — the phase id, its scope, and the requirements it discharges — ordered lowest-risk-first. Only the phases' plan files, each linked from `## Build plan`, arrive later, one per phase, one dispatch each. On a lite feature the sections are filled from the backlog row the brief quotes and the locked design, a phase's row cites the criteria it discharges, A1…An, and the two G0 sections already in the file are kept, the rest written beneath them (`.claude/references/core/feature-lite.md` `## The three placements`).
