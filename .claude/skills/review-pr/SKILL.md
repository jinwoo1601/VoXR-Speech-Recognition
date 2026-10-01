---
name: review-pr
description: "Review an already-open pull request only, posting the verdict as a PR review; a changeset with no PR goes to review-cycle. Use on 'review PR 42' or 'is that PR safe to merge'."
kind: orchestration
agents: [review-angle, finding-verifier, compile-check]
stops: [the ruling on the posted review before any feature-lane fix, the merge or approval or close of the PR]
status: active
bindings: [vc, issues, verification]
removal-date: null
---

# Review PR — Design & Build Workflow

## What this is

**Independent review of an already-open PR, written back onto the PR.** The PR's own title, body, and ticked validation boxes are treated as *claims to audit*, never as context to trust. Finder agents inherit nothing from the session that wrote the code — that is the whole source of the independence.

> The PR says what it did; the reviewer establishes what it did. Findings land on the PR, and only the human decides what happens to them.

This is the PR-bound sibling of `review-cycle`. Use `review-cycle` on a working tree or branch before a PR exists; use this once a PR is open. On a feature that ran both, the two are not redundant: `review-cycle` reviewed the tree mid-build, this one reviews the final diff the human will actually merge.

## Bindings

Each key is looked up as `.claude/references/core/delegation-contract.md` `## Bindings` says: `.claude/bindings/<pack>.md` first, the project `CLAUDE.md` *Bindings* section as the pre-scaffold fallback.

- `vc` (required) — the *PR read*, *PR diff* and *PR comment* slots are how a pull request is read, diffed and commented on; any of them `none`, `TODO` or missing — a project with no pull-request host, or one whose binding predates the slots — has no PR to review → stop and report, pointing at `review-cycle` instead. The *Base-revision reads* slot supplies the base-content idiom step 2 states literally in the brief. The *Branch procedure* slot names the project's branch forms, and step 1 reads the lane from it (see *Depth profiles*). Absent → stop and say so before any brief is filed.
- `issues` (optional) — the *Read* slot, by which the linked issue is read, and the *Link* slot, whose form the PR body is parsed for. Absent, empty, `TODO`, or its *Tracker* reading `none` → no linked issue is read, never a stop.
- `verification` (optional for this skill) — the commands CHECK runs through `compile-check` on the prose and trivial profiles. `none` → CHECK is recorded as `nothing to run`. Absent → CHECK is not run and the posted review says so, never a stop.
- Agents — `review-angle` or `finding-verifier` missing → stop before any dispatch and say so. `compile-check` missing → on the prose and trivial profiles, CHECK is not run and the posted review says so.

## When it applies — and prerequisites

**Trigger:** an open PR needs review — invoked directly on a PR number, or as the bound lane step below.

Confirm before proceeding:
- The PR exists and is open, and its head commit is pushed.
- `git fetch origin` has run, so base and head are resolvable locally.
- You are not about to review a PR whose branch has moved since you last looked — pin `headRefOid` in the brief and cite it in the posted review.

## Depth profiles

Depth is chosen by lane and class, not by taste. State which profile you ran in the posted review.

| Profile | Lane | Angles | CHECK |
|---|---|---|---|
| **slim** | Issue lane — a head branch in the form the `vc` binding's *Branch procedure* names for light-lane work, of class slim or of no declared class; gate-exempt | `CLAIM` + `LINE`, `GONE` | none — `g2-lite`'s preflight audits run evidence |
| **prose** | Issue lane, of class prose | `CLAIM` + `LINE`, `GONE`, `XFILE`, `CONV` | once, at the pinned head |
| **trivial** | Issue lane, of class trivial | none — no fan-out; the posted review names the profile and the human reads the PR diff | once, at the pinned head |
| **full** | Feature/design lane — a head branch in the design or feature form that slot names — any PR where G2 is at stake, and any head the lane read cannot place | `CLAIM` + the entire composed roster set | none — G2's preflight audits run evidence |

**The lane read.** Match the PR's `headRefName` against the forms the `vc` binding's *Branch procedure* names, a `<…>` placeholder matching any text. The light-lane form is the issue lane, its profile chosen by the class read; the design and feature forms select full. A head that matches no named form, or a slot that names no light-lane form, runs full, and the posted review's profile line says which case applied and why. This is never a stop.

**The class read.** For an issue-lane head, the effective class the invoking skill's check named — its `bounds` verdict, handed in by `tackle-issue` REVIEW or `g2-lite` REVIEW — before the declared one; else the class the work declared at its start — in its launch or handoff brief, or its line in `memory/STATUS.md` — slim when none was declared. The posted review's profile line names the class and where it was read.

`CLAIM` is defined in `references/pr-angles.md` — it is PR-specific and exists in no other cycle. The rest are pasted verbatim from the composed roster set — `.claude/references/core/angle-roster.md`, plus, for each name in `.claude/harness.json`'s `packs`, `.claude/references/<pack>/angle-roster.md` where it exists; with no manifest, it is core's roster alone. If core's roster is not locatable, stop and report rather than improvising angle definitions. A composed pack with no roster of its own is not a stop: the posted review names every such pack.

Depth may be widened on request (`review the PR thoroughly` → full, whatever the lane). Never narrow below the chosen profile without the human saying so, and if you do, say which angles you dropped in the posted review — a silently narrowed review reads as a clean one.

**Why slim is three angles.** On a typical issue diff — one or two files, one behavior — `XFILE` and `GAP` mostly re-find what `LINE` and `GONE` already have (measured on one issue-lane PR: a single finding was reported independently by four of the six angles then run), while `GAP` reliably converts a small fix into a long list of could-assert-more remarks. The issue lane is gate-exempt and its PRs are small; it buys its confidence from `CLAIM` auditing the author's own claims plus two correctness angles, not from breadth. Add `XFILE` back when the diff crosses a contract boundary — a call signature, a serialized field name, an event payload, an enum ordinal, or a string key read on both sides of the change — and name the addition in the posted review.

## Materiality floor — slim and prose

A finding becomes a **numbered finding** only if it asserts something in the merged result is *wrong*: code that misbehaves, a doc or API-reference statement that is false, a PR claim the diff does not deliver, or a removal that lost behavior. That is the whole bar — severity is not the test, so a false row in an API reference is numbered even though it is minor.

Everything about what the change *could additionally* have done — coverage that could be stronger, an assertion that could be tighter, an adjacent improvement, style, naming — is **not** a numbered finding. It goes in a flat `### Notes` list at the end of the posted review: one line each, no evidence block, explicitly marked unverified.

This floor is what keeps the issue lane proportionate. It does not apply to the full profile, where a coverage gap can be exactly what blocks G2.

## Workflow

1. **RESOLVE** — pin the PR by the `vc` binding's *PR read* slot — its number, title, body, url, state, draft flag, `headRefName`, `headRefOid`, `baseRefName`, files and labels — then pin the base:

   ```bash
   git fetch origin
   git merge-base "origin/<baseRefName>" "<headRefOid>"    # the base SHA agents compare against
   ```

   Compute the base with `git merge-base` as above (it is the merge-base anyway, which is what you want: immune to an advanced `main`), and derive the linked issue by parsing the PR body for the form the `issues` binding's *Link* slot names; `issues` absent, empty, `TODO`, or its *Tracker* reading `none` → no linked issue is read, never a stop.

   No number given → infer from the current branch, by the *PR read* slot's lookup of the PR for a branch, the branch per the `vc` binding's current-branch read; zero or multiple matches → ask, don't guess. Pick the depth profile by the lane read — `headRefName` against the forms the `vc` binding's *Branch procedure* names — and the class read, as *Depth profiles* says. A closed or merged PR is not reviewable here — its merge-base collapses onto its head and the diff comes back empty; stop and say so rather than posting a review of nothing.
2. **BRIEF** — build the brief from `assets/pr-review-brief-template.md` into the session scratchpad (absolute path — NOT the project tree, so the working tree stays clean). Save the full diff beside it, by the `vc` binding's *PR diff* slot, as `<scratchpad>/pr-<n>.diff`, and point the brief at it by absolute path. Base content idiom for agents: `git show <base-sha>:<repo-relative-path>`, using the merge-base SHA from step 1 — state the resolved idiom literally in the brief; agents use exactly what the brief says. Transcribe the PR's claims **verbatim** into the claims section, including every ticked validation box. The brief's `## Load-bearing invariants` opens, in both lanes and whatever the depth profile, with the entries of `.claude/references/core/prose-invariants.md`, copied verbatim and numbered under one lead-in line saying they are standing rules no check can read — so a finding against one is a breach of it in the change, never the observation that no check enforces it; a missing file is named in the brief and in the posted review, and the review runs on. The lane's own invariants follow the list: the feature's process docs supply them, with the canon pointers, where the lane has them; the issue lane puts the linked issue's reported problem after the list, read from the issue itself, by the `issues` binding's *Read* slot, rather than from the PR's summary of it — where no linked issue is read, the brief says so. On the trivial profile, FAN OUT, DEDUP and VERIFY are skipped and POST follows.
3. **FAN OUT** — one `review-angle` agent per angle in the chosen profile. Each call prompt = the brief's ABSOLUTE path + that angle's entry pasted in full. Subagents inherit no context from this thread: every prompt must be self-contained. Batch the spawns in as few messages as possible.
4. **DEDUP, THEN SORT** — main-thread reasoning, no agents: merge duplicates across angles, keep the strongest statement of each, drop anything the brief declared out of scope. **If this session wrote the code under review, that is exactly when not to soften a finding** — dedup edits for overlap, never for comfort. On the slim and prose profiles, now apply the materiality floor above: each survivor is either a numbered finding or a Note. Sorting a real defect down into Notes to shorten the review is the one failure this step must not produce — the floor asks *wrong vs. could-be-better*, never *big vs. small*.
5. **VERIFY** — one `finding-verifier` per **numbered** finding, its text passed verbatim plus the brief path. Notes are not verified (that is what earns them their one line and their unverified label). Batch ~5 at a time when numbered findings exceed 10. No numbered finding is posted or presented without a verifier verdict.
6. **POST** — first, on the prose and trivial profiles, CHECK: dispatch `compile-check` on the `verification` binding once, in the tree that holds the PR's head branch, only where that tree's head is the pinned `headRefOid` (`git rev-parse HEAD` there); where no such tree is at hand, CHECK is not run. Then write the review onto the PR by the `vc` binding's *PR comment* slot, as a comment-only review whose body is the file at `<scratchpad path>` — never an approval or a request for changes; if the review is rejected for any reason, fall back to the slot's plain comment with the same body file. The posted body carries: depth profile run and why — the lane matched, or which no-match case sent it to full (and any dropped or added angles), each composed pack with no angle roster of its own, a missing standing list of prose-held invariants, head SHA reviewed, on the prose and trivial profiles the CHECK result, or `not run` and why, numbered findings ranked severity then confidence with verdict and decisive evidence each, refuted findings named in one line so the human can see what was considered and killed, the `### Notes` list (slim and prose profiles), and an explicit statement of what was **not** verifiable from here (device checks, host-project test runs). Zero findings is a valid, good review — post it and say so plainly.

   **Slim-profile length budget:** ~150 words per numbered finding — the claim, the decisive `file:line` evidence, the fix, and nothing else. One code block only where the code is the argument. Drop the per-finding provenance ("reported by four of six angles"), the mutation-test narratives, and the recap of what verified clean; a two-sentence header line covers the last of those. If a finding genuinely needs more than that to be believed, it is a blocker and the extra words are earned — otherwise the budget holds. The full profile has no budget: at G2 the human is weighing evidence, not scanning.
7. **RULE, THEN ACT** — relay the posted review in-conversation and hand the ruling to the human. Fixes are governed by the lane:
   - **Feature/design lane:** nothing is fixed without a ruling. The posted review is evidence the human weighs at G2; `g2-accept` owns what follows.
   - **Issue lane:** a CONFIRMED **blocker** is the run's own unfinished work — fix it on the same branch, push to the same PR, and post a one-line follow-up comment, by the *PR comment* slot's plain comment, saying which finding the push addresses. Everything below blocker stays on the PR for the human, unruled and unfixed.
   - Either way: **never merge, never approve, never close the PR.**
