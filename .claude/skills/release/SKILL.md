---
name: release
description: "Cut and publish one release behind the human's word; never merges. Use on 'cut a release', 'ship a release', 'publish the release', 'bump the version for a release', or to resume a release whose prep has landed."
disable-model-invocation: true
kind: orchestration
agents: [compile-check]
stops: [the merge of the prep changeset, the word before anything is published, a destructive or outward-facing cleanup]
status: active
bindings: [release, vc, verification]
removal-date: null
---

# release

## What this is

**One release, prepped behind the human's merge and published behind the human's word.** The flow is two phases split by that merge: the bump is prepped on a branch and the run stops; the human merges; only then is anything published, and what came out is read back rather than assumed.

Everything a release needs to know about this project — where it is published, what the prep branch is called, what publishes it, which file states the version, where the notes come from, which versioning rule applies and what counts as evidence — is read from the `release` binding's seven slots. None of it is stated here, and none of it is guessed: a release published from a guess is exactly what those slots exist to prevent.

> One release per run. The evidence is fresh for the revision being cut, the merge is the human's, the word to publish is the human's, and a published release is never re-published.

## Bindings

Each key is looked up as `.claude/references/core/delegation-contract.md` `## Bindings` says: `.claude/bindings/<pack>.md` first, the project `CLAUDE.md` *Bindings* section as the pre-scaffold fallback.

- `release` (optional) — this pack declares the key `required: false`, so a project may compose the lane having answered nothing; the seven slots this skill reads: *Repository* for where a release lands and what is read back from, *Release branch* for the prep branch and what it is cut from and merges to, *Publish procedure* for what publishes and how it is triggered, *Version file* for the file and field that state the version and every other file that pins it, *Release notes* for the notes source and its format, *Versioning rule* for how the version is chosen, and *Release evidence* for both halves of green. Absent → stop and say so before anything is cut. A slot still `TODO` stops the same way, naming the short slot. There is no fallback and this skill invents none.
- `vc` (required) — the *Branch procedure* the prep branch is cut by, the changeset the prep is opened as, and the *Merge procedure* the review step branches on and the human's merge follows. Absent → stop and say so before the prep branch is cut: no branch idiom, no changeset, and no route for the review.
- `verification` (required) — the commands `compile-check` runs at GREEN, quoted into its brief and run in-agent. `none` is a filled value: the brief says `none bound`, GREEN is recorded as `nothing to run`, and the *Release evidence* slot's pre-cut half is then the only green this release has. Absent → stop and say so before the dispatch.
- Agents — `compile-check` missing → stop before any dispatch and say so.

## When it applies — and prerequisites

**Trigger:** one release is cut or resumed under the light lane, declared into the lane at the start of the work rather than claimed into it afterwards.

Confirm before proceeding — if any item fails, stop and say which step or lane is actually needed:
- Everything meant to be in this release has already landed on the branch the *Release branch* slot names as the one the prep is cut from. This skill ships what is there; it does not land work.
- No other release is in flight.
- The work is one release. A change that still needs writing belongs to the lane's issue flow or to the implementation track, not to a release run.

## Workflow

0. **PHASE** — establish which phase this run is in before anything else, read from the published state at the *Repository* slot and from the prep changeset: the version not yet landed on the branch the *Release branch* slot merges to → start at step 1; landed with nothing published → the publish half, at step 6; already published → report what is there, verify it per step 7, and stop. A release is resumable across sessions, and a run that skips this check re-does work that is already done or publishes a second time.
1. **VERSION AND PAYLOAD** — state plainly what is shipping: the changes landed since the last release, and the entries in the section the *Release notes* slot names as the source of a release's notes. The version follows the *Versioning rule* slot — the version the human named where one is named, otherwise proposed from the payload and asked. Never auto-correct a version the human named: where it reads wrong against the slot, say so once and then use theirs. Never pick one silently; where the slot leaves the choice open or ambiguous, say which part it does not settle and ask. Where the notes source is empty but changes have landed, that is a real gap and the notes would be near-useless: say so and offer to write the entries before continuing.
2. **GREEN** — dispatch `compile-check` on the `verification` binding's commands and on whatever the *Release evidence* slot's pre-cut half names as having to be green or present. The evidence must be fresh for the revision being cut: a result inherited from an earlier session looks identical to a fresh one and is not evidence about this revision. Where the slot names a check that cannot be run from here at all, report it as deferred and let the human decide whether to release without it — never claim it.
3. **PREP** — cut the branch the *Release branch* slot names, from the branch it names, per the `vc` binding's *Branch procedure*. Set the version in the file and field the *Version file* slot names, and roll every other file that slot says pins it — **found by search rather than by a remembered count or remembered line numbers**, then the same search re-run before the changeset is opened and expected to come back clean of stale pins. Move the notes into the section the *Release notes* slot's format gives this release. Open the changeset per the `vc` binding, naming the version, what is shipping, and what GREEN actually ran and returned.
4. **REVIEW** — review per the `vc` binding. Where its *Merge procedure* names a pull-request route, run the pull-request review skill the project's version-control pack provides — it is in `.claude/skills/`. Where it names none, run core's `review-cycle` over the changeset. No `vc` binding: stop and say so. Run it at the lightest profile the project's lane offers: a version bump is mechanical, and what earns its keep is the audit of the claims in the changeset's own description against what the changeset actually does.
5. **HARD STOP — the human's merge.** Report the prep changeset and stop. **Never merge it:** that merge is the human's ruling that the release is going out, and the publish half exists only after it.
6. **PUBLISH** — only once the prep is merged, per the *Publish procedure* slot, and **behind the human's word** that this release goes out now. Where the procedure refuses, its refusal names the precondition that failed: fix it the way it got there — a new prep changeset through steps 3 to 5 — and re-run this step. Never publish around a failed precondition and never reach past the procedure to publish by hand.
7. **VERIFY** — read back what was actually published against the *Release evidence* slot's post-publish half: what the published artefact must contain, what must be absent from it, and that the notes came out of the section the *Release notes* slot names. An exit code alone is not evidence. A published release is immutable — where what came out is wrong, the answer is the next version, never a re-publish of this one. Where a publish that died half-way left a branch or an artefact behind, removing it is destructive and outward-facing: report it and ask the human, never clean it up unprompted.
8. **REPORT AND STOP** — the version released, what shipped, the green evidence with what it ran and how fresh it is, the prep changeset, the publish run, the read-back against the evidence slot, where the release now lives, and anything deferred. Then stop: one release per run.

The green is the agent's work and the prep and the read-back are this skill's; the version, the merge and the word to publish are the human's, and every project fact any of it rests on is the binding's.
