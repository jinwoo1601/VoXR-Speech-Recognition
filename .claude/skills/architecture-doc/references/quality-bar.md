# The quality bar — worked examples

Read this while drafting the hard parts of an architecture doc: holding the right altitude, realizing the non-functional rows the platform makes load-bearing, and writing so a maintainer who did not sit in the feature can build, verify or reopen what was built. It expands the eight principles from `SKILL.md` — most with a good/bad pair, the rest with worked examples or prose alone.

> The examples below are illustrative of **form**. They are invented systems — a bulk file upload, a search index, a notification digest — chosen because they are nobody's project and everybody's vocabulary. When you cite your own project's code instead, re-read the live file first; code is the source of truth and it drifts.

## Contents
- The anchor
- The altitude stack — where the architecture doc sits
- What crosses over at G2 — the one obligation
- 1. Written as the feature's working record
- 2. Load-bearing decisions, with the rejected alternative
- 3. Right altitude
- 4. Seams are the spine
- 5. Non-functional realization
- 6. Cite real paths, symbols, changesets
- 7. The build plan is broken down and linked here
- 8. Honest about risk & as-built drift
- A lite feature
- Smell tests

## The anchor

Every line earns its place by answering: *would a maintainer who did not sit in this feature need this to build, verify or reopen what was built?* If it is trivia the code already states, or a signature that will churn, leave it out. If it is a load-bearing decision, a seam, a flow or a non-functional realization — it stays, cited and reconciled to as-built. The architecture doc is the feature's working record — the living document that holds the how, the build plan, and what the build taught. It is not a draft of the product page: the product docs are authored after G2 from live source. One obligation crosses over — `## Decision register` is the design rationale a reader cannot recover from the code, and it is what the product docs carry forward.

## The altitude stack — where the architecture doc sits

The architecture doc is the **bottom floor** — the only one that touches code, and the only one that goes on living after the feature ships. Using an upload's retry behaviour as the worked example across all four floors:

| Floor | Doc | Retry example |
|---|---|---|
| Model + dynamics + forks | system design doc | "A failed upload is retried only on a retryable error, with backoff, and the queue drains oldest-first. Produces steady progress under partial failure and a bounded worst case." |
| Numbers / tuning | the tuning pass | "Three attempts; backoff doubling from one second; at most ten uploads in flight." |
| Buildable, testable contract | feature requirement doc — for a lite feature, its backlog row | "A file that fails on a retryable error is retried without the person re-selecting it. *Verify:* fail one file's first attempt; confirm it ends stored, with the attempt visible." |
| **How it is built** | **architecture doc** *(this)* | "`UploadQueue` holds per-file attempt state in a flat array — no allocation per retry; `RetryPolicy` classifies the failure and schedules the next attempt; the drain runs on the batch worker off the completion queue." |

Stay on your floor: the numbers belong to the tuning pass, the acceptance check to requirements. The architecture doc owns the seams between components, the non-functional realization, the phase breakdown and the links to the plan files, not the plans.

## What crosses over at G2 — the one obligation

Nothing here is folded onto the product surface at G2: that surface is written fresh at that point, from live source, by the agent that reconciles it. One obligation crosses over, and every other section either stays here as the working record or has no successor to fold into — this is the whole of it:

| Architecture doc section (process, tier 2 — lives) | What happens to it at G2 |
|---|---|
| Opening + `## Context & scope` | Authored fresh from live source — the product page states its own purpose and scope from the code, and takes nothing from here |
| `## Decision register` (decisions + rejected alternatives) | **The one obligation that crosses over** — the why behind each costly-to-reverse choice and the alternative it beat, which no reading of the built tree recovers; the agent that authors the product docs lifts it from here, so write it to survive that lift |
| `## Seams` · `## Non-functional realization` | Authored fresh from live source — the product page describes the behaviour that was built; these record the seams and the budgets the build was held to, the maintainer's map of the tree as built, not a draft of the product page's component list |
| `## What this implements` · `## Build plan` | **Stays here** — the working record: the breakdown, the link lines to the phase plans in their files, and the sequence the build actually ran in |
| Risks · `## As-built deltas` | **Stays here** — the working record of the plan; `## As-built deltas` is governed by the charter the template's `## As-built deltas` guidance carries (`assets/architecture-template.md`), and its two amended kinds go into `## Decision register` instead |
| `## Related` | Authored fresh from live source — the product docs carry their own links; cross-linking from here to a product page that already exists stays fine |

So nothing on this map is folded or trimmed: every section is written for the maintainer who did not sit in the feature, and the register is written to survive the one carry.

## 1. Written as the feature's working record

- ❌ A scratch build guide: "TODO: make the upload thing. Steps: 1) add class 2) hook up 3) test." *(throwaway — it records no decision, no contract and no result, so nothing of it survives the session that wrote it)*
- ✅ Sections carrying what a maintainer needs in order to build, verify or reopen the feature: context and scope, the decision register, the seams, the non-functional realization, the build plan's breakdown and link lines, with the phase plans in their files, the risks, and the as-built deltas. The best architecture docs are the ones someone who did not sit in the feature can work from a year later.

## 2. Load-bearing decisions, with the rejected alternative

- ❌ "Files and their attempt records are unified into one indexed list." *(states the what; no why, no alternative — nothing to evaluate or reopen)*
- ✅ "**Positional alignment** — the attempt list is built one-to-one with the file list, so `Attempts.Count == Files.Count` by construction. *Why:* it makes the no-drift invariant *structural*. *Rejected:* an explicit `int FileIndex` back-link (keeps exactly the dual-list drift it is meant to kill); one merged record per file (rewrites the immutable half on every attempt, defeating the version gate that skips untouched batches)."

Record these inline, in a decision register — a feature has no separate decision-record files. The rejected branch is what lets a later contradiction reopen the call from the reasoning instead of guessing which assumption broke.

## 3. Right altitude

- ❌ A paragraph walking every method of `IndexWriter` line by line. *(a duplicate of the code that rots on the next edit)*
- ✅ "`IndexWriter` applies updates in term-bucket order and merges each bucket once per flush, gated by `_builtVersion` so a bucket no document touched is never rewritten." *(the organizing shape plus the one non-obvious invariant — not the line-by-line)*

Pick the level: context → seams → only the load-bearing code detail; the per-function detail goes to the plans. Let the code speak for the rest.

## 4. Seams are the spine

The seams matter more than the internals.

- ❌ A bullet per method of `IndexWriter`, with its parameters and return. *(per-function detail — it belongs to the phase's plan)*
- ✅ **What crosses:** "Two read-only surfaces: `IQuerySource` (per-query status records) and `ICorpusSource` (immutable per-snapshot documents). The result renderer reads both by the same index `i`."
- ✅ **The flow across it:** "The 'still working' marker is sustained state, polled from `QueryState.InFlightQueryId`, **not** an event on the notification bus — the bus carries one-shot results; persistent 'while-active' state is polled."

A reader should trace a query from producer across the seam to renderer without opening the code.

## 5. Non-functional realization

The non-functional rows from the requirements doc *drive* the architecture — so show them *met*, structurally, rather than restated.

- ❌ "The digest build will stay inside the budget." *(a restated requirement, not an architecture)*
- ✅ "The requirement that a digest never reveals an unsubscribed source is enforced **structurally**: the digest builder reads only through `SubscriptionScope`, which is constructed from the reader's subscriptions and exposes no unscoped query, and the renderer takes items solely from that scope — so an unsubscribed item *cannot* reach a digest, whatever a later caller does."
- ✅ "Per-file attempt state is a flat array rather than a record graph — zero allocation per retry; the headroom is tracked in the budget table."

## 6. Cite real paths, symbols, changesets

- ❌ "See the upload code." *(nothing to check, nothing that can be seen to have moved)*
- ✅ "`Upload/Contract/Enums.cs` · `FailureKind` (was `ErrorClass`, cs:66 → cs:68)"; "scheduled through the idempotent setup entry point on `UploadQueue`."

Code is primary and it moves; cited symbols make drift *detectable* on a later read. Document slow-drifting rationale heavily and fast-drifting signatures lightly.

## 7. The build plan is broken down and linked here

Persist the plan — it is otherwise lost at session end. `## Build plan` holds the breakdown rows, each carrying its id, its scope and the requirements it discharges, and one link line per phase; each phase's plan lives in its own file.

- ✅ "Ordered lowest-risk-first, each phase green by the `verification` binding's commands and shippable: **Phase 0** the contract refactor, *zero behaviour change* — the broadest diff, reviewed as a pure refactor (*discharges:* none; it is the seam every later phase uses); **Phase 1** the queue and its attempt state (*discharges:* F1, F2); **Phase 2** the retry policy — the riskiest, because the failure taxonomy is the part the store can surprise us on: **prototype first**, outside the shipped tree (*discharges:* F3, and decision B); **Phase 3** the progress surface (*discharges:* F4); **Phase 4** the resumable batch, last because it is the Could row (*discharges:* F7). **Assets to flag:** the seeded failure corpus the retry tests run against — code cannot author it."

The riskiest part gets a throwaway prototype first, and the assets code cannot author get flagged, because nobody finds them mid-phase.

## 8. Honest about risk & as-built drift

- ✅ **Risk (before build):** "Riskiest sub-items: the failure taxonomy the store actually returns (prototype against it first); a two-hundred-file batch holding the interface responsive on the slowest supported device."
- ✅ **As-built delta (after build):** "`RetryPolicy` has **no jitter** — the decision register planned jitter against a synchronized-retry storm; the prototype showed the queue's own drain order already staggers attempts. Simpler, and the storm case is still covered."

**The `## As-built deltas` charter** — what the section admits, what is amended into `## Decision register` or corrected in place, where each routed kind goes, the frontmatter `updated:` line and the rule that a persisted phase plan is never edited — is stated once, in the template's `## As-built deltas` guidance (`assets/architecture-template.md`); read it there.

## A lite feature

A lite doc is `## User story`, `## Context & scope`, `## What this implements`, `## Decision register`, `## Build plan` (its rows and plan links), `## As-built deltas` and `## Related`, in that order, and no other section. Its `## What this implements` and `## Decision register` cite the locked design and hold only what the design does not state. It meets principles 5 and 8 through the locked design it cites, since lite means the design fixed every choice: its non-functionals and risks stay there, and its `## As-built deltas` records drift. `## Decision register` holds each standing-rule derivation with the rule it follows from, and what the build teaches. The track is stated once, in `.claude/references/core/feature-lite.md`.

## Smell tests

- Transcribes the code method by method → it is a duplicate; document the *why* and the shape.
- Lists seams with no rationale → nothing to evaluate or reopen; add the decision and the rejected branch.
- No non-functional realization → the load-bearing half of the architecture is missing; show how the budget and the structural laws are met.
- A line no maintainer who did not sit in the feature would need → it fails the anchor; cut it, or move it to the home the charter gives it.
- Uncited or vague → drift-blind; cite paths and symbols, with a line number where it helps.
- A plan with no risk ordering → sequence lowest-risk-first, and prototype the riskiest part early.
- A separate decisions directory for the feature → architectural decisions go *inline*; only design-track decision records live in the design topic's own `decisions/`.
