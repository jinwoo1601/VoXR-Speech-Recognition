# Review angle roster

Each entry below is pasted into one `review-angle` agent's call prompt, together with the review brief's absolute path. Which angles a cycle runs is its depth profile — `review-cycle`'s `## Depth profiles` — and the review brief's `## Angles` field lists them. The full profile runs the composed roster set: this file plus each composed pack's own `.claude/references/<pack>/angle-roster.md` where it exists, and a pack's angles run exactly as the entries below do. EFF is dispatched only where the brief declares hot paths. MOVED is dispatched only where the brief's `## Master's changes` lists files: a brief with no such section, or none listed, leaves it out of every run, the composed roster set's included. No angle reports nits.

## Correctness family

### LINE — line-by-line re-derivation
Re-derive what each changed line actually does, statement by statement, against what the brief says it should do. Check operator precedence, sign conventions, boundary conditions, off-by-ones, null/None paths, early returns that skip cleanup.
Do NOT report: style, naming, or anything about unchanged code except where a changed line's correctness depends on it.

### GONE — removed-behavior hunt
For every deletion or replacement in scope, establish what the old code did that the new code doesn't. Compare against the base revision (the brief names the base revision and how to read it). Look especially for silently dropped edge-case handling, event unsubscription, cleanup, and clamps.
Do NOT report: removals the brief declares intentional — but verify the declaration covers the whole removal, not just its headline.

### XFILE — cross-file consistency tracer
Trace every contract that crosses file boundaries in the change: call signatures, serialized field names, event payloads, enum ordinals, string keys, units and coordinate frames. Verify both ends agree after the change.
Do NOT report: single-file logic (LINE owns it).

### WRAP — wrapper/adapter correctness
Where the change wraps, adapts, or mirrors another layer (adapter over another layer's model, serialization mirror, interface implementation), verify the wrapper preserves the wrapped contract: value ranges, units, null behavior, ordering, threading assumptions, error propagation.
Do NOT report: the wrapped layer's own internal bugs unless the wrapper amplifies them.

### MOVED — moved-target clash hunt
Dispatched only where the brief's `## Master's changes` lists files.
Read the `touches` and `calls` lines under the brief's `## Master's changes`: files the catch-up brought the target's changes into, that the branch changed or names. For each, read the target's change, as that section says, then what the branch's change assumes of that file, on a `calls` line in the files after `<-`: a signature, a field, a default, an ordering, a stated rule, a behaviour. The branch's change is read whole, from the block's `<base>` to its head before, whatever `## Scope`'s range. Report each assumption the target's change breaks: a semantic clash the catch-up did not mark as a conflict.
Do NOT report: a path on an `other` line; a defect wholly inside the target's own change with no bearing on the branch; a changed line's own logic (LINE owns it), a behaviour the branch removed (GONE), or a contract whose ends the branch's change both holds (XFILE).

## Quality family

### REUSE — reuse of existing code
Find changed code that reimplements something that already exists in the codebase (helpers, extensions, established utilities). Name the existing symbol with file:line.
Do NOT report: near-misses where the existing code's semantics genuinely differ — check before claiming.

### SIMP — simplification
Find changed code that can be materially simpler with identical behavior: collapsible branches, redundant state, dead parameters, conditions provably constant in context.
Do NOT report: simplifications that change behavior, however slightly, or matters of taste with no complexity payoff.

### EFF — efficiency/allocation
Dispatched only where the brief declares hot paths.
Find allocation and cost in paths the brief marks hot (per-frame, per-tick, per-event): hidden allocations (temporary collections, boxing, string concatenation, closures), repeated computation of invariants, O(n²) scans where n grows with content.
Do NOT report: costs in cold paths (setup, editor-only, one-shot) unless egregious.

### ALT — altitude
Judge whether each fix/feature in scope sits at the right abstraction level: is a symptom patched where a cause is reachable? Is special-casing accumulating where the underlying model should change? Is a change fighting the architecture the docs describe?
Do NOT report: line-level issues (other angles own them) — this angle is about the shape of the change, argued from the brief's canon pointers.

## Sweep family

### CONV — conventions
Check the changed code against the project's stated conventions (the coding-conventions reference, the project's bindings, house patterns visible in neighboring code): naming, file headers, any body comment or docstring beyond the header block and the exemptions the coding-conventions reference states, module placement.
Do NOT report: conventions the project doesn't actually state or practice — cite the rule's source for every finding.

### GAP — gap-sweep
Read the brief's invariants list and the full diff, and ask what NO other angle covers: unexercised new code paths, missing test coverage for the change's riskiest claim, brief invariants nothing in the change enforces, TODOs introduced without tracking.
Do NOT report: anything clearly owned by a named angle above — your value is the remainder; duplicating others is noise.
