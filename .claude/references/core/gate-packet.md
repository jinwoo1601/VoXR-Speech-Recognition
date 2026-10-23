# Gate packet

The form of the packet a gate puts to the human is stated here once, and the gate skills cite it by section rather than restate it: `## The packet` for what every packet carries, `## The G0 packet` for G0's, `## The G2 packet` for G2's, and `## The early word` for a gate word given before the preflight at G1, G2 and G2-lite. Its rules are held in prose at the gate, in the human's view — no check reads a packet or a word.

## The packet

One message, put by `.claude/references/core/ruling-form.md`, carrying: the issue; the one asked question; each default shown, with its option text; any fork meeting an exception of the fork default (`CONSTITUTION.md` *Display, don't ask*), put as a ruling of its own (`ruling-form.md` rule 3), never shown as a default; and what a yes authorizes.

Every packet carries the hub STATUS `## Auto-closed` lines verbatim, read by absolute path when the packet is drafted, or says there are none. The gate's first `memory/` write after its ruling removes each line shown, by its key, and no other: one `--remove-entry Auto-closed <key>` each in a hub operation, else the PM's own edit; a gate that makes no write clears nothing. A close by class puts no packet and clears nothing.

A ruling is the gate's clear word, the word with clear overrides of named defaults, or a clear decline. Any other answer is no ruling and is asked again, the ask naming what was unclear; nothing is inferred from tone.

## The G0 packet

Put by `## The packet`; only the scope is asked: agreement to the topic, or the backlog feature, and its scope. Shown as defaults, each with its text:

- for a feature, the user story the PM drafts from the backlog row and the locked design, in the form *As a \<user\>, I want \<capability\>, so that \<value\>.*;
- for a feature, the track, lite or full, the row's mark as `.claude/references/core/feature-lite.md` `## The mark` reads it;
- the lane, `design` for a design topic or `feature` for a feature;
- on `g0-open`'s tree path only, concurrency — "run concurrently" with each active branch, this one excepted, each stated independent or not, as that path reads them.

A design topic's packet has no story or track; today's path shows no concurrency.

Any default is overridden in the same answer, and recorded with the scope at RECORD:

- a story override is taken as worded;
- a track override to full is taken;
- a track override to lite on a `**Full.**` row amends what G1 ruled — exception (4) — so the PM stops and puts two options: open the feature on the full track now, as the row's `**Full.**` mark reads, or re-lock the row as lite at G1 first (`.claude/references/core/feature-lite.md` `## The mark`);
- a lane override to light or lab is a decline at G0, opening neither lane;
- a lane override between `design` and `feature` changes what is opened, so it is a scope change and the PM stops (`CONSTITUTION.md` *Display, don't ask*);
- a wait stops the branch with nothing written, as `g0-open`'s tree path says.

## The G2 packet

The one message at `g2-accept`'s HARD STOP, put by `## The packet`, carrying:

- the audit verbatim, per item;
- the human-only checks: where the statement is not yet recorded, `gate-preflight` item 2's FAIL on it — the one FAIL the packet carries, every other remediated at PRESENT — with the statement asked beside the word;
- the side rulings: every option the PM took by the fork default for which G2 is the next gate (`CONSTITUTION.md` *Display, don't ask*) — this run's takes, and those the PM finds since the branch's last gate in the session's dated notes (`memory/<YYYY-MM-DD>.md`), the branch's `memory/STATUS.md` line and its handoff briefs (`memory/handoffs/`) — each with its option text; budget, catch-up, CHANGELOG and fix-versus-defer takes are examples, not a list;
- any fork meeting exception (4) or (5) met before the packet, put as a ruling of its own before the word (`ruling-form.md` rule 3), never listed as taken, and cleared as `## The early word` says of another stop;
- the ask, and what a yes authorizes.

The re-run rule. After "Accept, but fix X", or the word given with the human-only statement, the PM makes the change, itself or by brief where composition or code logic is needed. The statement is quoted verbatim and dated in the re-run's preflight brief, where item 2 cites it, and in the dated note's `## Decisions` at CLOSE OUT. A statement given with or before an early word is quoted verbatim and dated in the first preflight's brief, where item 2 cites it, and in that note; it is not asked again. The change, where it touches a tracked file, is checked in per the `vc` binding, the `verification` binding's commands run green at the new head, and STAGE re-runs, all before PREFLIGHT re-runs. There is no ask where the re-run is no worse than the audit ruled on; the packet asks again, the new audit in view, on any FAIL, on the change meeting exception (4) or (5), and on an UNVERIFIABLE item the ruled audit did not carry. RECORD then records the new head. The fix's diff, and any take after the ruling such as a catch-up re-bump, go in MERGE's relay.

The form RECORD records: at the stop, for the word given there; for an early word, "in session, before the preflight, stood at `<head>`", `<head>` the head the passing preflight audited; for "Accept, but fix X", that ruling with the new head.

## The early word

The gate's explicit word — the lock, the acceptance or the close, as each HARD STOP names it — given in the session running the gate, after the invocation and before the stop's message. The phrase that invokes the gate skill — its trigger, such as "accept the feature", "lock the design" or "G2-lite" — is no word; an earlier session's word is none. The PM says it holds the word and runs the steps as written.

- **Stands** where the first preflight returns every item PASS — at G1, every fixed question yes, no changed file outside the area, and `budget: PASS`; at G2 and at a G2-lite slim, every `gate-preflight` item PASS; at a G2-lite prose or trivial, `budget: PASS`. The message is still put, as information, the PM saying the word stands. At G2-lite the word stands on the preflight run at the effective class CLASS reads; a CLASS escalation, or a `bounds` check that could not run, is no FAIL of itself — the preflight at the class it brings decides — and the `bounds` lines go in the information message, as HARD STOP puts them in view. At G1 a stood lock covers the feature marks PRESENT showed, as the stop's ruling does (`g1-lock` PRESENT); the information message carries the marks and says the lock is also the word to merge.
- **Void** on a FAIL — at G1, a fixed question answered no, a changed file outside the area, or `budget: FAIL`. Every FAIL is remediated as today, save G2's human-only item 2 FAIL, which the packet carries; the packet asks again, naming the FAIL.
- **Re-asked** on an UNVERIFIABLE item — a `budget` refusal or no final line among them (`gate-preflight` item 6): the packet is put in full, those items with the ask.
- **Degraded**: no word stands, and the packet is put in full, where `gate-preflight` returns no verdict, or where at G1 `doc-extractor` is missing and the answers are the PM's own read; `gate-preflight` missing stops before dispatch, as each skill says.
- **Suspended**: where a stood word meets another declared stop — a conflict or red re-verification at `.claude/references/core/gate-close.md` `### CATCH-UP`, a G2-lite review ruling, an exception fork — that stop is put as today; once it clears, the word stands and the PM says so, unless the clearing changed scope, which voids the word and runs the gate again from PREFLIGHT. At G2-lite the review runs after PRESENT as today, its CONFIRMED fixes listed with the stood word.

A stood word is the ruling: it unlocks what HARD STOP unlocks and is the word to merge (`adr-0033`). The sentence on what is no ruling stands beside it in each HARD STOP — `g1-lock` HARD STOP, `g2-accept` HARD STOP and `g2-lite` HARD STOP.

No conditional word at G1 or G2-lite: "lock, but fix X" is no ruling; the change is made as work and the gate put again.
