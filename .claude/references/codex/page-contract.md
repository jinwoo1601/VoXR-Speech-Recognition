# The page contract

What every page of a codex owes, in any project that composes this pack. The split is by kind: this file owns what a page *owes*, and the project owns what a page is *about* and where it sits, in the schema file the `product-docs` binding's `Schema file` slot names. A reconciler reads both — this file for the obligations below, the project schema for the subjects, the locations, the naming and the linking form. Where a project has no schema file, every obligation below still holds, and what the schema would have answered is read off the existing pages' own shape rather than invented.

## What a page is, and the three page types

A page records **what IS**: one subject, as the built system now stands, as of the date in the page's `updated` key. Not the sequence of changes that produced it, and not the work that built it.

Three page types are defined here:

- **`pack-page`** — a page whose subject is one of the standing components the project's pages are organized around, as the project schema names them.
- **`hub`** — the page that lists the codex's pages and stands as its entry point.
- **`decision-record`** — a page whose subject is a single decision, kept as a record in its own right.

The set is **extensible**: a project may define further types in its schema, and the project schema names which types that project uses. *Absence:* where the schema names no types, the three above are the available set, and a page is never written under a type no home has defined.

## One subject per page

Exactly one subject per page, named in the `subject` key, and a subject that has outgrown the page it shares gets a page of its own. Material about another subject goes to that subject's page or to no page at all — never onto the nearest page that will hold it. *Absence:* a durable clause whose subject has no page is carried to no page, and it is named in the ingest's receipt.

## Frontmatter

Every page opens with five keys, plus whatever the project schema adds:

| Key | What it holds |
|---|---|
| `type` | One of the page types above, or one the project schema adds. |
| `subject` | The one subject the page is about. |
| `status` | One of the three values below. |
| `updated` | The date the page was last read against live source — the date its "what IS" is as of. |
| `sources` | What the page cites, and what it lifted from. |

`status` takes exactly three values:

- **`draft`** — written, and not yet read against this contract.
- **`current`** — the page records what IS as of `updated`.
- **`superseded`** — the subject has left the tree, and the page is kept rather than deleted.

*Absence:* a page missing one of the five keys, or carrying a `status` outside the three, is a draft whatever it says of itself; the reconciler fills the key from live source or reports that it cannot, and never writes the page with the key left off.

## Cite everything

Every claim on a page is traceable to what it rests on, and **live source is the evidence** — code and configuration first, process docs second. The `sources` key carries what the page cites and what it lifted; a claim inherited from a process doc is re-verified against live source before it is written, never copied on trust, because an implementation drifts from even a reconciled record of it. *Absence:* a claim that cannot be verified is not written, and it is named in the reconciler's receipt.

## What crosses — the layer rule

**What crosses:** of a project's process docs, only the tier-2 working record's `## Decision register` crosses into a page; every other process doc is cited where it helps a reader and lifted never. The sorting principle is the findability of the source — what is locked, organized and readable where it sits is cited, and what is buried in a working record is lifted. Its four clauses:

- **Tier-1 design docs and their decision records are cited, not lifted.** They are locked and readable where they are, and a page that copies them carries a second copy to keep current.
- **The tier-2 `## Decision register` is lifted**, together with its `**Amended YYYY-MM-DD:**` lines — clause by clause, and only what the criterion below passes.
- **Requirements docs and the rest of the architecture doc cross nothing.** Not the build plan, not the phase table, not the as-built pass.
- **Live source remains the evidence**, whatever a lifted clause asserts: the lift supplies the *why*, and the *what* is verified against the tree.

*Absence:* the lift clause is the one arm that needs a tier-2 source at all; where a project has none, the last section of this file says what happens, and the other three clauses are unaffected.

## The durable/transient criterion

A clause crosses only if it is **durable** — about the system that stands — rather than **transient** — about the act of building it. Take each clause and ask the three questions in order; the first that returns a verdict settles it.

1. **Subject.** Is the clause's subject a thing standing in the tree today, or the act of building it? *A phase, a dispatch, a commit, an ordering of work or the moment of a version bump* is a build subject, and a build subject is transient.
2. **Erasure.** Would the clause still say something if the feature had landed in one commit by one author? A clause that dissolves under that hypothesis is transient.
3. **Recovery.** Can a reader recover the clause's *what* from live source but not its *why*? Then the why is exactly what crosses, and the clause is durable.

Questions 1 and 2 can only return *transient*; a clause that survives both reaches question 3, which returns *durable* or — where the thing decided is gone from the tree — *does not cross*. A durable clause is written onto its subject's page in undated present tense, as the rule and its why, never as the ruling event.

Two tie-breaks, and every clause reaches a verdict once they are applied:

- **Classified by subject, not by grounds.** A general rule cited only as a decision's grounds crosses only where some decision makes that rule its subject. Grounds are not a page row, and this is what stops an ingest from inventing a row nobody wrote.
- **Clause by clause.** A register row is often compound, and its lead sentence is never a boundary: split the row and run the three questions on each clause separately. A row that rules both *when* a component's version is raised and *that* the component's declared contents stay exact at every commit splits cleanly — the timing clause is transient under question 1 and crosses nothing, while the exactness clause is durable under question 3 and crosses as a rule about the component.

*Absence:* question 3 carries it. Where neither the what nor the why is in the tree, because the thing decided is gone, the clause does not cross, and a page already carrying it loses it under supersession below.

## The chronicle, by role

Three clauses, stated by role and naming no file:

- A project keeps exactly **one** dated chronological record.
- It is the **only** place a dated entry goes.
- Pages **never** carry dated narrative: what a page says is undated present tense, and the one date on a page is its `updated` key.

*Absence:* where the project names no such record, no dated entry is written anywhere, and the ingest reports that the project has no home for one.

## Supersession

When a decision a page carries is superseded, the old rationale is **removed** from the page, never appended to it — a page that accumulates its own history has become a dated record under a different filename. What is removed is not lost: it survives in the feature's `## Decision register`, dated and tier-2, and in the project's chronicle, which are the two homes that already keep it. *Absence:* a page whose whole subject has left the tree is not deleted — its `status` becomes `superseded` and the page is kept, per preserve-over-delete.

## The silent ingest

"No codex change" is a **stated normal outcome** of an ingest, not a defect of one. Where a feature's decisions are all transient under the criterion above, the codex is silent: the record is the chronicle entry and the register, and the ingest reports no page change. *Absence:* silence is reported rather than merely left — the ingest names the clauses that did not cross, so a silent ingest is auditable, and an ingest that finds nothing durable never manufactures something to write.

## Hub bookkeeping

The `hub` page lists **every** page of the codex and **no** page absent from it; both directions are the obligation, and both are checked in the same pass that writes a page. Every page written, renamed or superseded is reflected on the hub before the ingest closes, in the project schema's linking form. *Absence:* a subject that was ingested and yielded no page is listed on the hub and marked as such, so a reader can tell a subject with nothing durable to say from a subject nobody has looked at yet.

## Precedence

Three homes, in this order: **the project schema > this contract > the reconciler's own judgment.** Where the project schema and this contract disagree, **the project schema wins** — it answers what a page is about and where it sits, and this file answers only what a page owes. The reconciler **reports every conflict it resolved that way**, naming the two statements, which home won, and what the pack-owned half may therefore have wrong: a conflict is a signal that this contract needs amending, and a conflict resolved silently is that signal lost. *Absence:* where there is no project schema, this contract is the top home, the reconciler's judgment fills only what neither home states, and there is no conflict to report.

## Where there is no tier-2 source

The lift clause of the layer rule needs a tier-2 working record to lift from, and a project that keeps none has no register: there the lift rule is **inert, not unsatisfiable** — nothing fails, and nothing waits for a document that will not arrive. The codex is authored from live source instead, citing whatever process docs the project does keep, and the criterion still sorts what is written: the *why* that live source cannot show is what a page adds, and a why that is nowhere in the tree is not manufactured to fill the gap. Every other obligation here stands unchanged — the page types, one subject per page, the frontmatter, cite-everything, the chronicle rule, supersession, the silent outcome, the hub and precedence all hold in a project with no process docs at all.
