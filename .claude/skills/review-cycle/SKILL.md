---
name: review-cycle
description: "Run a multi-agent, adversarially verified review of a changeset range or feature branch. Use when any code review, review pass or simplify pass is due; it replaces /code-review and /simplify."
kind: orchestration
agents: [review-angle, finding-verifier, compile-check]
stops: [the ruling on a disputed finding, the ruling on a deferral, the ruling on a scope change, the ruling on a canon fork]
status: active
bindings: [vc, verification, review, session-launch]
---

# review-cycle

## What this is

**One union review cycle: correctness and quality in the same fan-out.** Finder angles read a shared brief and hunt in parallel; adversarial verifiers kill the weak findings; CONFIRMED findings are fixed by default and listed at the next gate, and the human rules only on a disputed finding, a deferral, a scope change or a canon fork; each fix goes to its owner from a brief. Its depth follows the lane and the changeset's class, as `## Depth profiles` says.

> Finders find, verifiers kill, a writing agent fixes what is CONFIRMED by default, and the human rules on what is disputed. No finding reaches the human unverified; every fix is listed at the next gate.

Scope boundary: this cycle reviews a working tree or branch in the checkout. A vc pack may ship a sibling bound to the hosted review its system provides; a feature may run both, and they are not redundant.

## Bindings

Each key is looked up as `.claude/references/core/delegation-contract.md` `## Bindings` says: `.claude/bindings/<pack>.md` first, the project `CLAUDE.md` *Bindings* section as the pre-scaffold fallback.

- `vc` (required) — the base-revision reads the brief quotes under `## Scope` (the changed-file list, a file at a revision, a diff between two revisions), and the check-in procedure COMMIT follows. Absent → stop and say so before any brief is filed.
- `verification` (required) — the commands CHECK runs through `compile-check`. `none` → CHECK is recorded as `nothing to run`. Absent → stop and say so.
- `review` (optional) — the angles each profile runs and the hot paths, `core`'s `## review` section. Absent, empty or `TODO` → the defaults in `## Depth profiles`, and no hot paths declared, so EFF does not run; never a stop. A slot reading `default` means the table's default.
- `session-launch` (optional) — the *Context bound* and the *Context read*, which the rhythm reads at each step's end, as `handoff` `## When to hand off` says. Absent, either slot missing or `none`, or the read failed → the phase-boundary handoff only, the slot named once; never a stop.
- Agents — `review-angle`, `finding-verifier`, or `compile-check` missing → stop before any dispatch and say so.

## Prerequisites

Confirm before proceeding:
- The build is complete and green (run `compile-check` first if unsure).
- The scope is expressible as a revision range or branch in the `vc` binding's terms.
- The feature's process docs are locatable (they supply the invariants).

## Depth profiles

Depth is chosen by lane and class, not by taste.

| Profile | When | Angles (default) | Materiality floor | CHECK |
|---|---|---|---|---|
| **full** | a design or feature branch — wherever G2 is at stake, feature-lite included — and any branch the lane read cannot place | the composed roster set | no | as written |
| **slim** | a light-lane changeset of class slim, or of no declared class | LINE, GONE, XFILE | yes | as written |
| **prose** | a light-lane changeset of class prose | LINE, GONE, XFILE, CONV | yes | once, at the head after the last fix; a fix round does not re-run it |
| **trivial** | a light-lane changeset of class trivial | none — no fan-out | — | as written |

- **The lane read.** Match the branch under review against the forms the `vc` binding's *Branch procedure* names (a `<…>` placeholder matches any text), as `review-pr` does: the light-lane form is the light lane; the design and feature forms select full; no match, or a slot naming no light-lane form, selects full, and the brief says which case applied. Never a stop.
- **The class read.** For a light-lane branch, the effective class the invoking skill's check named — its `bounds` verdict, handed in by `tackle-issue` REVIEW or `g2-lite` REVIEW — before the declared one; else the class the work declared at its start — in its launch or handoff brief, or its line in `memory/STATUS.md` — slim when none was declared.
- **The `review` binding** replaces a profile's default angle list where its slot names one. The profile's name, the angles it runs, and where they came from (default or binding) are recorded under the brief's `## Angles`.
- **EFF** is listed only where the `review` binding's *Hot paths* slot names paths; the brief quotes them. With none declared EFF is not dispatched, in any profile, and the brief says so.
- **Widening and narrowing.** The human may widen any profile (e.g. "review thoroughly" → full). Never narrow below the profile without the human saying so; a narrowing is recorded in the brief and said at RULE.

## Materiality floor — slim and prose

A finding becomes a **numbered finding** only if it asserts something in the merged result is *wrong*: code that misbehaves, a doc statement that is false, a claim the change does not deliver, or a removal that lost behaviour. That is the whole bar — severity is not the test.

Everything about what the change *could additionally* have done — stronger coverage, a tighter assertion, an adjacent improvement, style, naming — is a **Note**: one line, no evidence block, marked unverified; a Note is not verified and not ruled.

The floor does not apply to the full profile, where a coverage gap can be exactly what blocks G2.

## Workflow

1. **BRIEF** — build `.claude/references/core/review-brief.md` into `.scratch/review-<slug>-brief.md` before any fan-out: the revision range and the base-revision read idiom per the `vc` binding, stated literally (agents use exactly what the brief says); the changed-file list per the same binding; per-file intent from the persisted plan; under `## Load-bearing invariants`, first the entries of `.claude/references/core/prose-invariants.md`, copied verbatim and numbered under one lead-in line saying they are standing rules no check can read, so a finding against one is a breach of it in the change and never the observation that no check enforces it — whatever narrowing the human rules, since a narrowing chooses angles, not canon; a missing file is named there and again when the survivors are presented at RULE, and the cycle runs on — then the feature's own invariants; canon pointers from the feature docs; under `## Angles`, the angles of the profile `## Depth profiles` chooses, recorded as it says; the full profile's composed roster set is `.claude/references/core/angle-roster.md`, plus, for each name in `.claude/harness.json`'s `packs`, `.claude/references/<pack>/angle-roster.md` where it exists (no manifest, or a composed pack with no angle file of its own, is core's roster alone). On the trivial profile, FAN OUT, DEDUP and VERIFY are skipped and RULE follows.
2. **FAN OUT** — one `review-angle` per angle listed under `## Angles`, each through the Agent tool without a `name`. Each call prompt = the brief's absolute path + that angle's roster entry pasted in full. Subagents inherit no context from this thread: every prompt must be self-contained. Read-only agents run in parallel — batch the dispatches in as few messages as possible.
3. **DEDUP** — main-thread reasoning, no agents: merge duplicate findings across angles, keep the strongest statement of each, drop anything the brief declared out of scope. On slim and prose, then apply `## Materiality floor — slim and prose`: each survivor is a numbered finding or a Note. The floor asks *wrong vs. could-be-better*, never *big vs. small*; never sort a real defect into Notes to shorten the review.
4. **VERIFY** — one `finding-verifier` per surviving numbered finding, its text passed verbatim plus the brief path; Notes are not verified. Batch ~5 at a time when findings exceed 10. No finding is presented to the human without a verifier verdict.
5. **RULE** — every CONFIRMED finding, a CONFIRMED-AS-CLARITY one included, goes to FIX without a ruling, and is listed, with its verdict and decisive evidence, at the next gate. The hard stop is only for a finding the PM disputes, one it proposes to defer, a fix that changes scope, or one a canon fork decides: present those ranked by severity then confidence, each with its verdict and decisive evidence, and each put to the human by `.claude/references/core/ruling-form.md`, its rule 3; the human rules fix / defer / reject per finding. Notes are listed after the findings, one line each, unverified, no ruling asked. On the trivial profile the human is shown the whole diff, by the `vc` binding's diff read, and reads it; anything the human raises is ruled as a finding. File the collation — the profile run, findings, verdicts, and per finding `fixed by default` or its ruling — beside the brief as `.scratch/review-<slug>-collation.md` (the delegation contract's filing rule: read-only agents' reports are not filed raw). Deferred items are recorded in `memory/STATUS.md`, or where the project's deferral convention says.
6. **FIX** — a fix goes to its owner, from a brief filed under `.scratch/`: `code-writer` through `implement` for code and pack files, `doc-writer` for prose docs and bindings, `architect` for the architecture doc; the reviewed code's shape is canon, and the review is not a license to refactor. The main thread applies nothing (the PM's write scope is `memory/` and CHANGELOG lines); agents find and verify, and a writing agent edits from the finding, or from the ruling where one was asked.
7. **CHECK** — dispatch `compile-check` on the `verification` binding. On prose, it runs once, at the head after the last fix; a fix round does not re-run it.
8. **COMMIT** — per the `vc` binding's check-in procedure: inline on the main thread, or the agent the binding names; the findings summary is the message. This skill never commits by itself.

**The rhythm.** At the end of each step above the session applies `handoff` `## When to hand off`: it runs the *Context read*, and past the *Context bound* it invokes `handoff`, its objective the next step, instead of taking that step. A handoff taken after FAN OUT and before RULE files the findings the cycle holds, with any verdicts, as the collation `.scratch/review-<slug>-collation.md`, and its brief names that file and the review brief by path. After the last step there is no next step, and the objective is what follows — the invoking skill's next step where `tackle-issue` or `g2-lite` REVIEW invoked this cycle, otherwise the branch's gate skill — as `handoff` `## When to hand off` says of a skill's last step.
