# Review angle roster

Each entry below is pasted into one `review-angle` agent's call prompt, together with the review brief's absolute path. Which angles a cycle runs is its depth profile — `review-cycle`'s `## Depth profiles` — and the review brief's `## Angles` field lists them. The full profile runs the composed roster set: this file plus each composed pack's own `.claude/references/<pack>/angle-roster.md` where it exists, and a pack's angles run exactly as the entries below do. EFF is dispatched only where the brief declares hot paths. No angle reports nits.

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
Check the changed code against the project's stated conventions (the coding-conventions reference, the project's bindings, house patterns visible in neighboring code): naming, file headers, comment discipline, module placement.
Do NOT report: conventions the project doesn't actually state or practice — cite the rule's source for every finding.

### GAP — gap-sweep
Read the brief's invariants list and the full diff, and ask what NO other angle covers: unexercised new code paths, missing test coverage for the change's riskiest claim, brief invariants nothing in the change enforces, TODOs introduced without tracking.
Do NOT report: anything clearly owned by a named angle above — your value is the remainder; duplicating others is noise.
