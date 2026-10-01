# The quality bar — worked examples

Read this while drafting the hard parts of a requirements doc: cashing a subjective "feel" target out into something observable, and keeping implementation (*how*) out of requirements (*what*). It expands the eight principles from `SKILL.md` — most with a good/bad pair, the rest with worked examples or prose alone.

> The examples below are illustrative of **form**. They are invented features — a search box, a bulk upload, a digest of notifications — chosen because they are nobody's project and everybody's vocabulary. When you cite one of your own project's design docs or decision records instead, re-read it first; a locked decision may have been amended and re-locked since.

## Contents
- The anchor
- 1. Testable — done is observable
- 2. What and why, never how
- 3. Traced upward
- 4. Scoped — non-goals as sharp as goals
- 5. Prioritized — Must / Should / Could
- 6. Non-functional — the feature-killers
- 7. Dependencies & assumptions
- 8. Right-sized & honest
- Operationalizing "feel"
- Smell tests

## The anchor

Every line earns its place by answering: *could the human tick this off at G2 against the built thing?* If not, it is not a requirement yet — it is a wish, a design decision, or an implementation note. Send each to where it belongs.

## 1. Testable — done is observable

- ❌ "Search should feel fast and forgiving." *(a feel target — a why, not a requirement)*
- ✅ "A query returns a first page of results, or a stated 'still working' marker, before the person can finish typing the next word — and a misspelling of a term that exists returns that term's results rather than an empty list. *Verify:* run a query against the seeded corpus and a one-character misspelling of it; confirm a first page in both cases." *(the observable proxy)*

The bad version names the feeling the feature is for. The good version is the thing you can watch happen — with the feel target kept alongside as the rationale, not deleted.

## 2. What and why, never how

- ❌ "Add an index writer that debounces each keystroke and merges the postings list on a background worker." *(an implementation — it belongs in the architecture doc)*
- ✅ "The result list stays consistent with what has been typed: an in-flight query whose terms are already stale never overwrites a newer one's results." *(the what; the architecture doc gets to choose debouncing, cancellation, or a sequence number)*

Constraints are the exception. "The first page must render within the frame budget the platform binding names" is a legitimate requirement: it bounds the solution space without choosing the solution.

## 3. Traced upward

- ❌ "Results are grouped by source, collapsed to three per group." *(a mechanism floating free of any locked decision — nothing to check it against)*
- ✅ "Results are grouped by source because the locked design's stated goal is *one place to look across every store*, and the grouping fork was resolved in favour of source-first grouping (source: the search design doc, and its decision record on result grouping)."

A requirement you cannot trace to a locked design doc or a decision record is scope creep — or a sign the design is not actually locked. Surface that; do not invent the rationale to cover the gap.

Each requirement also traces to a user story in the requirements doc's §1, captured verbatim as the human stated it; a requirement with no story behind it is scope creep. For the *bulk upload* feature:

- ✅ "**S1** — As a person archiving a project, I want to upload a whole folder of files in one action, so that I do not have to pick them one by one." — and the row that serves it: "F1 · A batch of files selected in one action uploads, and every file ends as *stored* or *rejected with a stated reason* · Story: S1."

## 4. Scoped — non-goals as sharp as goals

The non-goals section is what stops a two-week feature becoming two months. For a *bulk upload* feature:

- ❌ "Out of scope: anything not listed above." *(a fence that encloses nothing — every later argument about scope is still open)*
- ✅ "Goal: a batch of files is uploaded in one action and each file ends in a stated final state. Non-goal: this feature does **not** transform, compress or validate file *contents* — that is a later feature, and it consumes the stored files this one produces. Adjacent and not owned: the storage quota (it is read here, and enforced elsewhere)."

Always name the adjacent systems the feature touches but does not own, and say what is deferred rather than merely absent.

## 5. Prioritized — Must / Should / Could

The Musts are the minimal G2-acceptable feature; the Shoulds and Coulds are the richer target. This is what lets a feature ship instead of sprawling.

- **Must:** "A batch of files selected in one action uploads, and every file ends as *stored* or *rejected with a stated reason*."
- **Should:** "A file that fails on a retryable error is retried without the person re-selecting it, with the attempt visible."
- **Could:** "An interrupted batch resumes from where it stopped when the same batch is selected again."

## 6. Non-functional — the feature-killers

A feature that is functionally perfect still fails on the rows its platform makes load-bearing. Take the ones that apply and cut the rest rather than leaving them blank:

- ❌ "The upload must be performant and handle errors gracefully." *(two categories named, neither made observable, nothing to fail against)*
- ✅ "**Throughput:** a batch of two hundred files holds the interface responsive throughout — no interaction is blocked for longer than the budget the platform binding names. **Input boundary:** a file the store refuses is reported by name with the reason, and never fails the batch silently. **Degraded dependency:** when the store is unavailable the batch pauses with every file in a stated pending state, and nothing is reported as stored that is not."

The three headings that generalize are the budget the platform makes non-negotiable, the behaviour at the input boundary when input is lossy or unrecognized, and the behaviour when a dependency is degraded — plus accessibility wherever people interact with the feature directly.

## 7. Dependencies & assumptions

- **Dependency:** "Requires the storage-quota feature merged — the upload reads the remaining quota before it starts, and refuses a batch it cannot finish."
- **Assumption:** "Assumes a person selecting a batch has already authenticated, so no requirement here covers a session that expires mid-batch. If that assumption is wrong, this is a new requirement, not an edge case."

Features are dependency-ordered; declare the ordering here so the build sequence is legible to whoever picks this up. An unstated assumption is where requirements rot.

## 8. Right-sized & honest

A page or three, not a specification suite. If it is twenty, you are either documenting more than one feature (split it) or smuggling in architecture (cut it down a floor). End with the open questions you did *not* resolve — the architecture doc would rather inherit a known unknown than hit it mid-build.

## Operationalizing "feel"

Requirements are full of subjective targets — "fast", "reliable", "personal", "readable". Do not ban them; they are the *why*. Convert each into an observable proxy that becomes the actual requirement, and keep the feel target alongside as the rationale.

| Feel target (the why) | Observable proxy (the requirement) |
|---|---|
| "Search feels instant" | A first page of results, or a visible pending marker, before the next word can be typed — never a blank panel. |
| "The upload feels reliable" | Every file in a batch ends in exactly one stated final state, and no file is ever left with none. |
| "The digest feels personal" | Every item in a digest names what the reader subscribed to and why it was included; a digest with no such item is not sent. |

## Smell tests

- Cannot test it → rewrite it as something observable.
- Names a class, component or algorithm → it is architecture; move it to the architecture doc.
- A "feel" word with no proxy → unfinished.
- No non-goals → unscoped.
- No upward citation → unparented, or the design is not actually locked.
