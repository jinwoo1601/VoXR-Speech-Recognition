You're picking up: open feature `endpointer-profile` (R11) at G0. Read the files below, then start at Next steps.

## Objective

Open the backlog feature `endpointer-profile` through `g0-open`: agree its scope and user story with the human, cut `feat-endpointer-profile`, and record the requirements doc's §1 (user story) and §2 (scope). Done for this brief when G0 is recorded; the rest of the requirements doc is the next step, not this one.

## Lane

feature

## Context

- `main` is at `66610de`, after PR #179 merged the locked design (`f9466bb`, G1 2026-10-16).
- The design: `Planning~/design-docs/endpointer-profile.md`, G1 LOCKED 2026-10-16, with ADRs `Planning~/design-docs/endpointer-profile/decisions/adr-0001..0004`. Its backlog, §6, holds one feature, `endpointer-profile`, marked **Full**: it gets a requirements doc and an architecture doc (`.claude/bindings/core.md` `## process-docs`: `Planning~/features/endpointer-profile/requirements.md`, `architecture.md`).
- What the feature delivers, per the backlog row:
  - the pause-ladder corpus, pinned by the harness;
  - each run recording which conf it decoded with (rule 9);
  - the rule2 sweep on the joined rig, timed with both buffer setups; rule3 and rule4 move only on evidence; both kill criteria apply (rule 8, adr-0004);
  - named presets carrying the measured values (adr-0003);
  - the profile resolved by listening mode, with an explicit choice overriding, delivered by a runtime overlay with its invariant (adr-0001);
  - the Continuous default flip, `expectations.json` re-pinned to it through the harness, and a human-only check before G2 (adr-0002).
- STATUS (`memory/STATUS.md` `## Current`): the main headline names `feat-endpointer-profile` at G0 as the next action.
- `release` is held by the human. When it resumes, RSE-1 goes first.

## Key files & locations

- Design: `Planning~/design-docs/endpointer-profile.md`. §1 holds the model and rules 1–9; §3 the failure modes; §6 the backlog row; §7 the open items, each with its owner.
- Recon facts with file:line (code sites, harness dump fields, buffer, upstream Kaldi rules): `.scratch/endpointer-profile-recon-collation.md`.
- Form for §1–§2: the last feature's requirements doc, `Planning~/features/joined-rig-recogniser/requirements.md`, and its G0 brief, `.scratch/doc-writer-joined-rig-recogniser-g0-brief.md`.

## Constraints & decisions

- The design is locked. If building contradicts it, stop and reopen it on a `design-` branch; never patch it.
- Windows-native only; never write WSL into anything.
- `memory/` commits go straight to `main` through `hub.py commit`; branch work commits on the branch.
- Agents never run git writes. Compiler and test output goes to `compile-check`.
- The report filer misses background agents' reports: file each one by hand beside its brief in `.scratch/`.
- STATUS lines stay at or under 200 characters.

## Next steps

1. Invoke `g0-open` for the backlog feature `endpointer-profile`: Full track, branch `feat-endpointer-profile`, from design `Planning~/design-docs/endpointer-profile.md` §6.

## Open questions

1. The §7 items the requirements doc owns, to raise once SCOPE is agreed rather than at G0: the human-only check's form (on-device or a recorded listen); the pause-ladder's silence lengths, commands and case count; whether the listening mode can change after model load.
2. JRR-1 and JRR-2 (STATUS `## Deferred`) are human-only items from the last feature and do not gate this G0.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-endpointer-profile; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 3590d8f8-f958-48d4-b4f9-0edda97e64a9". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
