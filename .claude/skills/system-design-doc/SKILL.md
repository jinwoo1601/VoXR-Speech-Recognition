---
name: system-design-doc
description: "Author a design doc, locked at G1, for a system or a deliverable (document, deck, dataset), or resolve a fork into a decision record. Use when a design topic starts or an as-built system needs reverse-documenting."
kind: orchestration
agents: [doc-writer]
stops: [the agreement on the settled model before the brief is filed, the call on each open fork before it is recorded]
status: active
bindings: [process-docs, vc, board]
removal-date: null
---

# system-design-doc

## What this is

A system design doc specifies **one system** — a cache's admission and eviction rules, a retry policy, a scheduler, an interaction model — as a **model**, and argues the **dynamics** that model produces: the behaviour the rules create, and why that behaviour delivers what the locked design says the project is for. It is the *earliest* doc in the pipeline — a design-track doc authored on a design branch and **locked at G1**, upstream of the feature-requirement doc (the buildable contract) and the architecture doc (the how).

The anchor idea, and the test of every line:

> **Specify the model; argue the dynamics. The design *intent* is what G1 locks — the numbers are provisional.**

A doc that only *describes mechanics* and never reasons about what *emerges* from them is a rulebook, not a design. The value is the argument that *these rules → this behaviour → the goal the locked design states*. So the test of every line is: *is this a decision you could lock at G1, and would it survive the numbers being tuned later?* If the line is really a number, defer it to the tuning pass and flag it provisional. If it is an implementation choice, it belongs in the architecture doc. If it is a buildable acceptance check, it belongs in requirements. Everything that stays is model, dynamics, or a resolved fork.

## The two forms

`## What this is` describes the **systems form**. The skill has a second, the **deliverable form**, for work whose design is its content and audience — a document, a deck, a dataset — rather than a mechanism's dynamics.

**The test:** is the work's design content and audience, or a mechanism's dynamics? The PM applies it and names the form with the topic's scope at ORIENT; the human rules on it at G1. One form per doc: a work with both gets two docs.

**What differs by form in the workflow:** only SETTLE's content, DRAFT's template and BAR's bar, each where its step says, and the frontmatter `type`, where `## Filing and bookkeeping` says. Everything else — the prerequisites, GATHER, FORKS and `## The ADR convention`, FILE and the rest of `## Filing and bookkeeping` — holds for both forms as written; for a deliverable doc, a "system" in that text reads as the deliverable, and "the model" or "the settled model" as its settled content.

**The test of every line**, for a deliverable, is item 4 of `## The deliverable quality bar`, content altitude, in place of the anchor above.

**Changes after G1:** a deliverable doc is unlocked and the changed section rewritten in place; the form has no amendments section.

## Bindings

- `process-docs` (required) — where design docs live and what they are called, and therefore where this topic's directory and its `decisions/` subdirectory sit. Absent → stop and say so before any brief is filed.
- `vc` (required) — the branch procedure (the design branch this work runs on, never the main branch) and the check-in procedure for the files drafted here.
- `board` (optional) — where cross-session state lives beyond `memory/STATUS.md`, and therefore where this topic's row is updated. Absent → `memory/STATUS.md` is the state; orient from what the project binds.
- Agents — `doc-writer` missing → stop after SETTLE and say so.

<!-- harness:binding:start process-docs .claude/bindings/core.md -->
<!-- Interview: Where do this project's design docs and feature docs live, what are the files called, and at which path is a phase's plan file written, and at most how many KB may an architecture doc's body, a plan file and a brief each hold, at which path pattern? -->
- Design docs: `Planning~/design-docs/<topic>.md`, one file per design topic, its decision records beside it per design-track's ADR convention; the root-level `Planning~/*.md` vN docs are pre-harness history, read-only
- Feature docs: `Planning~/features/<feature>/requirements.md` and `Planning~/features/<feature>/architecture.md`, one directory per feature
- Persisted plan: `Planning~/features/<feature>/plan-<id>.md`
- Architecture-doc body budget: 40 KB over `Planning~/features/<feature>/architecture.md`; the closed features' oversized docs, all written before the harness, are ruled in `memory/doctor-rulings.md`
- Plan-file budget: 10 KB over `Planning~/features/<feature>/plan-*.md`
- Brief budget: 15 KB over `.scratch/**/*brief*.md`
<!-- harness:binding:end process-docs -->

<!-- harness:binding:start vc .claude/bindings/vc-git.md -->
<!-- Interview: Which version-control system does this project use, and how are branches created, changes checked in, base revisions read, branches merged, the working tree recorded before an upgrade, and pull requests read, diffed and commented on? -->
- System: git; GitHub, remote `origin` `https://github.com/jinwoo1601/VoXR-Speech-Recognition.git`, public; `gh` on the Windows PATH (`C:\Program Files\GitHub CLI\gh.exe`), authenticated as `jinwoo1601`
- Main branch: `main`
- Auto-mode entries: included
- Branch procedure: a re-lock — a `design-<topic>` branch cut from the feature branch's head in the feature's own tree, declared on its STATUS line `re-lock of <feature-branch>`, merged back into that feature branch after G1 — is cut by `git switch -c design-<topic>` in the feature's tree and merged back by `git switch <feature-branch>`, then `git merge design-<topic>`, in that tree; branches are `design-<topic>`, `feat-<feature>`, `light-<topic>` (an issue's `light-<n>-<slug>`), `lab-<topic>`, which merges nothing, and the release prep `chore/release-X.Y.Z`; each is cut from an up-to-date `main` — `git fetch origin`, then on `main` `git merge --ff-only origin/main`, then `git switch -c <branch>` — and runs in the one checkout, since `trees` is none
- Check-in procedure: inline on the main thread — `git add -- <paths>`, then `git commit -F <message file> -- <paths>`, each one command, never a heredoc or a chain, never `git add -A` — a hub operation's `memory/` check-in by the base's `scripts/hub.py commit --message <file> --root <hub> <paths>`, which commits the named paths under `memory/` alone; then `git push -u origin <branch>` on a branch's first push and `git push` after; the commit sha is the receipt
- Base-revision reads: `git show <revision>:<repo-relative-path>`; `git diff --name-status <base>..<head>` for the changed-file list; `git diff <base>..<head> -- <path>`; `git log --oneline <base>..<head>`; `git status --short` for the working-tree read; `git branch --show-current` for the current branch; `git diff --numstat --no-renames <base>..<head>` for the per-file line-count read
- Merge procedure: a pull request against `main`, opened by `gh pr create --base main --title "<title>" --body-file <file>` after the push, merged by the human on GitHub behind the gate's stop; Claude never merges
- Snapshot procedure: `git stash create`, then, where it prints a sha, `git stash store -m "harness pre-upgrade <date>" <sha>` — the working tree and the index are left as they are; where it prints nothing, the snapshot is `git rev-parse HEAD`; confirmed by `git rev-parse --verify <sha>^{commit}`; restored by the human from that sha; `.claude/` and `CLAUDE.md` are tracked from `light-harness-adopt` on, so the record covers them — `.claude/settings.local.json` and `.scratch/` stay ignored and are not covered
- PR read: `gh pr view <n> --json number,title,body,url,state,isDraft,headRefName,headRefOid,baseRefName,files,labels` and `gh pr list --head "<branch>" --json number`; `gh` has no `baseRefOid` and no `closingIssuesReferences` field, and `--json` never goes without an explicit field list
- PR diff: `gh pr diff <n> > <file>`
- PR comment: `gh pr review <n> --comment --body-file <file>`, falling back to `gh pr comment <n> --body-file <file>`; never `--approve` or `--request-changes`, which GitHub refuses on one's own pull request; `gh pr edit` fails on the Projects-classic `projectCards` deprecation, so a PR body is patched by `gh api repos/jinwoo1601/VoXR-Speech-Recognition/pulls/<n> -X PATCH -F body=@<file>`
<!-- harness:binding:end vc -->

<!-- harness:binding:start board .claude/bindings/core.md -->
<!-- Interview: Does this project track cross-session state on a board — which, and how is it read — or is memory/STATUS.md the only state? And at most how many characters may one memory/STATUS.md line carry, or is there no cap? And at most how many KB may memory/STATUS.md hold, or is there no budget? -->
- Mechanism: none — memory/STATUS.md
- STATUS line cap: 200
- STATUS budget: 10 KB
<!-- harness:binding:end board -->

## When it applies — and prerequisites

Trigger: the design track for a system is active. Before drafting, confirm:

- **You are on a design branch** (G0 done — branch exists, scope agreed), created per the `vc` binding's branch procedure. Not on the main branch.
- **This is docs, not code.** Design precedes implementation. Do **not** scaffold, move, or edit code in this phase, even to "set things up" — treat that as a hard line.
- **You will not cross G1 on your own judgment.** "Locked" is the human's call, and it is ruled elsewhere: core's `g1-lock` owns that stop and nothing here stops at G1. You draft, explore forks, and hold the bar.
- **A locked decision is immutable.** If drafting a coupled system reveals a locked sibling decision is wrong, **stop**: reopen that design on a design branch, amend it, re-lock at G1. Never silently patch a locked decision from a neighbouring doc.

## What the system derives from

Unlike a requirements doc, which derives from *locked* design, a system design doc derives from the **layer above it**:

1. **The stated goals of the locked design.** Every system exists to deliver part of a goal the design states, and conflicts resolve in favour of that goal. Open by naming which goal(s) this system carries, and cite the doc that states them.
2. **The parent hub and its source.** The topic's overview doc and the relevant section of the source it derives from. Where the source numbers its sections, trace to that number.
3. **Already-locked sibling systems.** A new system must stay consistent with the locked decisions of the systems it couples to. Read them first; cite them.

Read those fully before drafting. A mechanism that traces to no stated goal is flavour or scope creep — surface it, do not paper over it.

## Workflow

1. **ORIENT** — confirm the design topic, the branch and the scope: the branch per the `vc` binding's branch procedure, the cross-session state per the `board` binding. Nothing is drafted before the topic and its scope are agreed. The form — systems or deliverable — is named with the topic and its scope, by `## The two forms`.
2. **GATHER** — read the layer above: the goals the locked design states, the parent hub and its source section, and the locked sibling systems this one couples to. Cite each; an uncited parent is an assumption.
3. **SETTLE** — the first hard stop: the PM and the human work the system out in conversation until the model is settled — the state it tracks and the rules that move it, the decisions it puts to its operator, the dynamics reasoned forward and then war-gamed for degeneracy, and the boundaries it owns, couples to, hands off and deliberately does not decide. That settled content is the decision record the next step's brief carries: `doc-writer` records decisions and never makes one, so nothing agreed here may be left for it to invent. Where the model has a genuine unknown rather than a settled answer, it leaves this step as an open item and surfaces at FORKS for the human's ruling. For a deliverable, what is settled is the content G1 locks — its audience and purpose, its form and constraints, each part's intent and key content, its sources and facts, and its exclusions — with any text the human dictates recorded verbatim and marked dictated, and each number recorded either as a limit set from outside, with its source, or as a target the project set itself, provisional.
4. **DRAFT** — file `.scratch/doc-writer-<topic>-<system>-brief.md` from `.claude/references/core/decision-brief.md`: the settled model and its open items, plus the parents and their citations, as the decision record, `assets/design-doc-template.md` as the skeleton, and the target directory and file name per the `process-docs` binding. Dispatch `doc-writer` through the Agent tool without a `name`. The brief says to order the middle sections to fit the system and to keep the top `> **What's decided.**` callout and the two bookends. For a deliverable, the skeleton is `assets/deliverable-doc-template.md` instead, and the brief says to keep its headings in the template's order — the deliverable's own structure lives in `## The parts`.
5. **BAR** — judge the returned draft against `## The quality bar` below. For the hard parts — arguing the dynamics, killing degenerate strategies, keeping numbers out of the model — `references/quality-bar.md` carries the worked examples and the altitude-stack table. A miss is a second brief to `doc-writer`, never a PM edit; where the miss is a gap in the settled model rather than in the writing, the fix is upstream — settle it with the human first, then re-brief. A deliverable draft is judged against `## The deliverable quality bar` instead; `references/quality-bar.md` is the systems form's.
6. **FORKS** — the second hard stop: where a decision is genuinely open, lay the tradeoff out in prose and let the human rule. Do not force a picker, do not silently pick. On a contested fork, independent derivations judged against one another are strong signal where they converge — the technique is kept, the panel is not fixed. Each ruling becomes an ADR; what stays unresolved is parked in `## Status / openness`, and the downstream docs inherit it.
7. **FILE** — the ADRs per `## The ADR convention`, the doc and its bookkeeping per `## Filing and bookkeeping`.

## The ADR convention

A genuine fork — a contested decision with real tradeoffs — is recorded as an ADR, and the record is `doc-writer`'s to write from a brief carrying the human's ruling.

- **Where:** under the design topic's own directory — wherever the `process-docs` binding puts design docs — in a `decisions/` subdirectory, created lazily on that topic's first ADR.
- **Named:** `adr-NNNN-<slug>.md`, with a four-digit sequence. **Check the highest existing number on disk before claiming one** — concurrent sessions share the sequence; never reuse a number.
- **Shaped by** `assets/adr-template.md`.
- **Covering:** every contested decision points at its `adr-NNNN`, and the keystone fork gets its own.
- **Keeping the rejected branch, with the reason it lost.** That is what lets a later contradiction reopen the design from the reasoning instead of guessing which assumption broke.

## The quality bar

Every system design doc must clear these. (Expanded, with good/bad examples: `references/quality-bar.md`.)

1. **Traced to a stated goal — open by naming what it serves.** Every system delivers part of a goal the locked design states; name which, and cite the doc. A system that traces to no stated goal is flavour or scope creep.
2. **The decision space is the heart.** A system is interesting because of the *decisions* it puts to its operator. Enumerate them — the choice, the information available when choosing, the opportunity cost, why no option dominates. No interesting decision → it is not a system.
3. **Model altitude — not numbers, not implementation.** Specify the *model* (the state it tracks, the rules, how one step resolves). Defer the **numbers** to the tuning pass and flag them provisional. Defer **implementation** (classes, components) to the architecture doc. Defer the **buildable acceptance check** to requirements. A good design survives all three being filled in later.
4. **Argue the dynamics — and war-game them.** Reason forward from the rules to the behaviour that emerges. Then attack your own rules: where is the dominant strategy, the degenerate loop, the dead zone — and the rule that kills each? This is first-class here, not an appendix.
5. **Draw the boundaries — own / couple / hand off / explicitly do not decide.** State what this system owns, what it consumes from siblings, what it pushes elsewhere, and what it deliberately does *not* decide. Naming the non-decisions keeps each doc independently lockable and stops one system silently deciding another's.
6. **Cash the model out into what it delivers.** Close the loop: this model → this behaviour → the goal's promised outcome. If you cannot say what it is like when it goes right, the dynamics argument is not finished.
7. **Forks recorded as ADRs.** Every contested decision points to its `adr-NNNN`; the keystone fork gets its own. Keep the rejected branch and *why it lost* — that is what lets a later contradiction reopen the design intelligently instead of guessing which assumption broke.
8. **Right-sized, and honest about openness.** A few screens, not a specification suite. Mark the locked skeleton (the `> **What's decided.**` callout), flag every number provisional, and end with the open items and forward dependencies you did *not* resolve.

**Smell tests:** describes rules but never what emerges → rulebook; add the dynamics. A number presented as decided → move it to the tuning pass. Names a class → architecture, not design. No goal cited → unparented. No degeneracy analysis → unstress-tested. No non-decisions or hand-offs → unscoped.

## The deliverable quality bar

Every deliverable design doc must clear these.

1. **Traced to a stated goal.** Name which goal of the locked design the deliverable serves, and cite the doc.
2. **Audience and purpose explicit.**
3. **Every part has an intent, not only content.**
4. **Content altitude.** G1 locks intent and key content; final wording, layout and styling are the build's, except text the human dictated, which is recorded verbatim and marked dictated. Numbers follow the numbers rule: a limit set from outside is locked, with its source cited; a target the project set itself is provisional.
5. **Every fact is sourced** to a material or to the human's dated word; an unsourced fact is invented.
6. **Exclusions as sharp as inclusions.**
7. **What it delivers is stated.**
8. **Right-sized, ending with the open items.**

**Smell tests:** a part with no intent; a fact with no source; no audience; a pixel-level layout spec, which is the build; no exclusions.

## Filing and bookkeeping

- **Location:** where the `process-docs` binding says design docs live, kebab-case, one system per page; the topic's ADRs in its `decisions/` subdirectory. New files are added to version control per the `vc` binding's check-in procedure.
- **Frontmatter:**
  ```yaml
  ---
  type: system        # system (a designed mechanism) | concept (a cross-cutting tension or legibility model) | overview (a topic hub)
  topic: <the design topic this system belongs to>
  tags: [kebab, tags]
  status: draft        # draft while designing; stable only for an as-built design-of-record reverse-documented from shipped code
  updated: YYYY-MM-DD   # absolute date, never "today"
  sources:
    - <the parent doc and the section this system derives from>
    - <the topic's decisions/adr-NNNN-*.md>
    - "Session direction YYYY-MM-DD (...)"
  ---
  ```
- **Deliverable frontmatter:** a deliverable doc's frontmatter is its template's, `assets/deliverable-doc-template.md` — `type: deliverable`, the other keys as above.
- **Prose and linking convention:** keep each paragraph and list item on one logical line — no hard-wrapped prose. For link and date form, match what the project's existing process docs already do; where there are none, link by relative path and write dates absolute (`YYYY-MM-DD`). End with `## Status / openness`, then `## Related`.
- **Mark unbuilt content:** a line that states forward design intent rather than as-built fact carries `(design intent, not yet built)` inline, so a reader of a design-of-record can tell the two apart line by line.
- **Update the cross-session state:** add or update the topic's row where the `board` binding says — track `design`, stage `design-doc`, the pre-G1 design stage. No board bound → `memory/STATUS.md` is the state.
- **Do not touch the product surface.** A project's product docs are written only after G2 acceptance, by the agent that reconciles them; writing there now is a layer violation. Design docs are process docs, and their only bookkeeping is the row above. Cross-*linking* to a product page is fine; creating or editing one from here is not.
- **Check in only when the human asks**, per the `vc` binding's check-in procedure. Concurrent sessions edit the same cross-session state and share the ADR sequence — re-read both, and re-check the highest ADR number on disk, before appending.
