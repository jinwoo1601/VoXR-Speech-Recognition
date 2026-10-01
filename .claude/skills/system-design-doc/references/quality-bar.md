# The quality bar — worked examples

Read this while drafting the hard parts of a system design doc: arguing the *dynamics* a model produces, war-gaming your own rules against degeneracy, and keeping *numbers* and *implementation* out of the *model*. It expands the eight principles from `SKILL.md` — most with a good/bad pair, the rest with worked examples or prose alone.

> The examples below are illustrative of **form**. They are invented systems — a cache, a retry policy, a job scheduler — chosen because they are nobody's project and everybody's vocabulary. When you cite one of your own project's docs instead, re-read it first; design moves, and a locked decision may have been revised.

## Contents
- The anchor
- The altitude stack — model vs numbers vs requirements vs implementation
- 1. Traced to a stated goal
- 2. The decision space is the heart
- 3. Model altitude
- 4. Argue the dynamics — and war-game them
- 5. Draw the boundaries
- 6. Cash out into what it delivers
- 7. Forks recorded as ADRs
- 8. Right-sized & honest
- Smell tests

## The anchor

Every line earns its place by answering: *is this a decision you could lock at G1, and would it survive the numbers being tuned later?* If not, it is not design yet — it is a number (defer it to the tuning pass), an implementation choice (architecture doc), or a buildable check (requirements doc). Send each to where it belongs; keep the model, the dynamics, and the resolved forks.

## The altitude stack — model vs numbers vs requirements vs implementation

The single most common failure is writing at the wrong altitude. Four docs, four floors, using a read-through cache as the worked example:

| Floor | Doc | Worked example |
|---|---|---|
| **Model + dynamics + resolved forks** | **system design doc** *(this)* | "An entry is admitted on its second reference and evicted by least-recent use, except that an entry pinned by an in-flight read is never evicted. Produces a cache a full scan cannot flush, and a bounded worst case under a burst of one-shot keys." |
| **Numbers / tuning** | the tuning pass | "Capacity 50 000 entries; the admission window is 2 minutes; a pin expires after 30 seconds." *(provisional — not locked at G1)* |
| **Buildable, testable contract** | feature-requirement doc | "A pinned entry survives an eviction sweep. *Verify:* fill the cache past capacity while one entry is pinned, and assert it is still resident." *(illustrative)* |
| **Implementation (classes, data)** | architecture doc | "`RecencyIndex` holds the eviction order; `Admission` gates inserts on the second-reference bit." *(illustrative)* |

The design doc owns the **top floor only**. Numbers belong one floor down (flag them `provisional → the tuning pass`); class names two floors down.

## 1. Traced to a stated goal

- ❌ "The cache keeps recently read records in memory." *(a mechanism floating free of any reason to exist)*
- ✅ "This is the layer where the design's stated goal — *a read stays fast while the store behind it is degraded* — becomes mechanism: the working set is served from memory, and it keeps being served from memory exactly when the store slows down." *(the opening names the goal and cites where it is stated)*

A system that traces to no stated goal is flavour or scope creep. Open by naming what it carries, and cite the doc that states it.

## 2. The decision space is the heart

A system is interesting because of the decisions it puts to its operator — the person, or the calling system, that drives it.

- ❌ "The scheduler runs submitted jobs in submission order." *(no live decision = a queue, not a system)*
- ✅ "Every submission is the three-question loop: *cheap-and-late or expensive-and-now? · how much of the quota does this job spend? · what gets preempted if it will miss its deadline?* — each answered against a fixed budget and partial information about what else is about to arrive." *(the decision space)*

For each decision, give the choice, the information available when choosing, the opportunity cost, and *why no option dominates*. If one option always wins, it is not a decision.

## 3. Model altitude

- ❌ "A retry waits 200 ms, then 400 ms, then 800 ms, for at most five attempts." *(numbers presented as decided — these are tuning)*
- ❌ "Add a `RetryPolicy` that multiplies a stored delay by a factor before each attempt." *(implementation — → architecture doc)*
- ✅ "A failed call is retried after a delay that **grows multiplicatively and is drawn from a range rather than fixed**, and the caller stops when the *operation's deadline* is spent — not when an attempt count runs out." *(the model: the shape of the rule, not its constants or its code)*

The model is the durable, designable thing. The constants will be tuned; the code will be written. Both happen *after* G1. State the rule's *shape* and defer the rest.

## 4. Argue the dynamics — and war-game them

This is what separates a design from a rulebook. Do not stop at the rules — reason forward to what *emerges*, then attack it.

- ❌ "Failed calls are retried with backoff, and the breaker opens after repeated failures." *(stops at the rules — never says what behaviour results)*
- ✅ **Forward:** "These rules give a degraded dependency **three exits**: it recovers under a thinned load, it trips the breaker and sheds entirely, or the callers' deadlines expire and the queue drains — each gradual, none a cliff." *(the dynamics)*
- ✅ **War-gamed:** "Naively, every caller retries on the same schedule, so a dependency that comes back is knocked over by the synchronised second wave — a degenerate loop that makes recovery impossible. **The range rule kills it:** delays are drawn from a range, so the retries spread out instead of arriving together." *(the degeneracy named, and the rule that closes it)*

Degeneracy analysis is first-class here, not an appendix. On a contested fork, independent derivations judged against one another are strong signal where they converge.

## 5. Draw the boundaries

- ✅ **Owns:** "what is admitted, what is evicted, and what a hit or a miss means to the caller."
- ✅ **Consumes:** "the freshness stamp the store writes — an entry is only as current as the write that produced it."
- ✅ **Hands off:** "invalidation on write → the store's change feed; every capacity constant → the tuning pass."
- ✅ **Does NOT decide:** "what *stale* means to a caller — the cache reports an entry's age; the feature reading it decides what age it can accept."

Naming the non-decisions is half the job: it keeps each doc independently lockable and stops the cache quietly deciding a freshness policy on everyone's behalf.

## 6. Cash out into what it delivers

Close the loop back to principle 1: model → behaviour → the goal.

- ❌ "The cache is a bounded in-memory map with least-recently-used eviction." *(true, but: so what? what does it deliver?)*
- ✅ "Because admission needs a second reference, the cache is **the batch job's licence to run**: a nightly full pass no longer flushes the working set, so the interactive reads sharing the machine keep their latency while it runs. Fast when it matters, not fast on average." *(the goal, cashed out)*

If you cannot state what it is like when it goes right, the dynamics argument is not finished.

## 7. Forks recorded as ADRs

- ✅ "The admission fork — strict recency vs. frequency-aware admission vs. a hybrid — is resolved in `adr-0004-cache-admission` (the keystone): admit on second reference, evict by recency, with the rejected strict-recency branch and why it failed the nightly-scan case recorded there."

Every contested decision points to its `adr-NNNN`; the keystone fork gets its own. Keep the rejected branch — a later contradiction reopens the design from the *reasoning*, not a guess. Check the highest ADR number on disk before assigning one (concurrent sessions share the sequence).

## 8. Right-sized & honest

A few screens, not a specification suite. If it is twenty pages you are either documenting more than one system (split it) or smuggling in numbers and implementation (cut them down a floor). Three habits keep it honest:

- the `> **What's decided.**` callout up top — the locked skeleton, separated from the still-open;
- **every number flagged** `provisional → the tuning pass`;
- a `## Status / openness` close listing the forward dependencies and open forks you did *not* resolve (mark RESOLVED ones with their ADR), so downstream docs inherit known unknowns instead of hitting them mid-build.

## Smell tests

- Describes rules but never what emerges → rulebook; add the dynamics.
- A number presented as decided → it is tuning; move it to the tuning pass and flag it provisional.
- Names a class or component → it is architecture; move it down a floor.
- No stated goal cited → unparented (flavour or scope creep).
- No degeneracy analysis → unstress-tested; war-game it.
- No non-decisions or hand-offs → unscoped; draw the boundary.
