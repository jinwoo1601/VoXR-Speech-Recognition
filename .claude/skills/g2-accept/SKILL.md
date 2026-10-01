---
name: g2-accept
description: "Take a feature through G2 acceptance on the human's ruling, then its product docs, merge and close-out. Use on 'accept the feature', 'ready for G2', 'merge the feature branch', or after final verification passes."
kind: orchestration
agents: [gate-preflight, doc-writer, compile-check, architect-reconcile]
stops: [the G2 ruling before record or product docs or merge, a conflict or red re-verification at catch-up]
status: active
bindings: [vc, verification, product-docs, board, process-docs, trees, session-launch]
---

# g2-accept

## What this is

**G2 is the human's ruling; everything here prepares evidence for it or executes its consequences.** Preflight audits the preconditions, the human rules, and only then are the ruling recorded, product docs written, the branch merged, and the state closed out.

> Nothing crosses G2 on the assistant's judgment. Preflight prepares, the human rules, and only then do product docs and merge exist.

## Bindings

Each key is looked up as `.claude/references/core/delegation-contract.md` `## Bindings` says: `.claude/bindings/<pack>.md` first, the project `CLAUDE.md` *Bindings* section as the pre-scaffold fallback.

- `vc` (required) — what STAGE needs staged for the ruling form, the read commands the preflight runs, the *Branch procedure*, whose lab form Prerequisites refuses, and MERGE's merge procedure. Absent → stop and say so before any brief is filed.
- `verification` (required) — the commands whose run evidence the preflight's item 2 looks for, quoted into the preflight brief; `none` is a filled value (item 2 reports `nothing to run`). Absent → stop and say so before the brief is filed.
- `process-docs` (required) — the docs `gate-preflight` audits and RECORD writes to. Absent → stop and say so.
- `product-docs` (optional) — the location and the reconciling agent. `none bound` → PRODUCT DOCS is reported as not applicable. A location with a named reconciling agent → that agent is dispatched from a decision-style brief only if it exists in the composed set, else the step is reported, never improvised.
- `board` (optional) — default `none — memory/STATUS.md`. A bound board → CLOSE OUT advances or retires its row.
- `trees` (optional) — the slots `references/tree-path.md` `## Bindings` names. Absent → today's path. Filled → the steps run as `references/tree-path.md` orders them.
- `session-launch` (optional) — the Hub session name, read by `references/tree-path.md`. Absent, or the Hub session name missing or `none` → REMOVE as today and no report sent.
- Agents — `gate-preflight` missing → stop before any dispatch and say so. `doc-writer` missing → RECORD is reported as not done; the PM may not write the doc, and the ruling stands in the conversation and the session note. `compile-check` missing → where CATCH-UP brings the main branch in, stop before the merge and say so. `architect-reconcile` missing → a lite feature's RECORD is reported as not done, the ruling standing as said of `doc-writer`.

## Prerequisites

Confirm before proceeding:
- The review cycle is done and its fixes are committed.
- The checkout is on the feature branch per the `vc` binding's base-revision reads.
- Every command the `verification` binding names is green at the head presented (`compile-check`'s last PASS).
- The branch is not in the lab form: the lab form the `vc` binding's *Branch procedure* names, or `lab-<topic>` where it names none (a `<…>` placeholder matches any text). A lab branch merges nothing → stop and say so before any brief is filed.

## Workflow

Where `trees` is filled, as the contract reads it, these steps run as `references/tree-path.md` orders them; otherwise they run as written.

1. **PREFLIGHT** — file `.scratch/g2-accept-<feature>-preflight-brief.md` from the common fields of `.claude/references/core/delegation-contract.md` (the `vc`, `board`, `verification`, and `trees` bindings quoted; the feature's process docs by absolute path per `process-docs`), then dispatch `gate-preflight` through the Agent tool without a `name`. The skill applies the test of `.claude/references/core/feature-lite.md` `## The mark` here; for a lite feature the brief says `lite`, gives the backlog's path and the feature's name, quotes the row and cites its criteria as A1…An (`.claude/references/core/feature-lite.md` `## The acceptance source`).
2. **PRESENT** — relay the audit verbatim: per-item PASS / FAIL / UNVERIFIABLE with evidence. A FAIL → remediate and re-run the preflight; never present a failing audit as acceptable.
3. **STAGE** — per the `vc` binding: nothing to stage when the ruling form is in-conversation; the vc pack's staging skill when the binding names one.
4. **HARD STOP — the G2 ruling.** Ask for the human's explicit acceptance with the audit in view, putting the ruling by `.claude/references/core/ruling-form.md`. Preflight PASS is not acceptance; silence, enthusiasm, or a "looks good" about anything but the gate is not a ruling. The ruling's form — where and how it is given: in conversation, a pull-request approval, … — is the one the `vc` binding names; `ruling-form.md` governs what the PM's message puts to the human. Only that ruling unlocks steps 5–8.
5. **RECORD** — file `.scratch/doc-writer-<feature>-g2-brief.md` from `.claude/references/core/decision-brief.md`: the ruling — who, when, in what form — as the decision record, the requirements doc as the target per `process-docs`. Dispatch `doc-writer`. For a lite feature the target is the architecture doc, and the brief and dispatch that follow take the place of `doc-writer`'s: file `.scratch/architect-reconcile-<feature>-g2-brief.md` — `lite` and the rest `.claude/references/core/feature-lite.md` `## The acceptance source` asks of a lite feature's brief, the ruling, the head accepted, and the record's form and place as `.claude/references/core/feature-lite.md` `## The three placements` fixes them — and dispatch `architect-reconcile` in its record mode to write the G2 record.
6. **PRODUCT DOCS** — per the `product-docs` binding as `## Bindings` above says. Written only after the ruling, every claim verified against live source.
7. **MERGE** — per the `vc` binding's merge procedure. **The G2 ruling is itself the word to merge**; there is no second stop. Relay first what was authored *after* the ruling — RECORD's edit and any product-doc changes — as a diff the human can read, then merge. Show, do not ask again.
8. **CLOSE OUT** — the PM's `memory/STATUS.md` line (within the `board` binding's *STATUS line cap* — absent or `none` → no cap stated); the board row per the `board` binding when one is bound. The same act writes the session's dated note as `.claude/references/core/gate-close.md` `## The dated note` says.
9. **HANDOFF** — invoke `handoff` for the next session's task.
