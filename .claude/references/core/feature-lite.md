# Feature-lite

The lite track of a backlog feature, stated here once: how a feature is marked lite, where its acceptance criteria come from, where its records are placed, and how it leaves the track. Skills and agents cite this file by path and section heading rather than restate it. A feature that is not lite runs as each skill is written.

## The mark

A backlog row's Scope cell closes with its mark, `**Full.**` or `**Lite** (<adr>): no requirements doc; acceptance — (1) … (2) …`, `<adr>` the decision record the mark rests on, or the design section where the topic has none. A row is marked lite only where the locked design fixes every file-level choice, under the human's G1 ruling. A feature is lite where its row in the design's backlog — found by its name in the Feature cell — carries `**Lite**` in the Scope cell, and no requirements doc exists at the feature's `process-docs` path. No backlog, no row, `**Full.**`, no mark, or a requirements doc present: full. The skill makes the read and names the track in every brief it files; an agent never reads the mark itself. After G0 the row is located through the architecture doc — its `sources:` and the quote opening `## Context & scope`; the backlog's row is the source, and a quote that differs from it is reported, the row winning.

## The acceptance source

A lite feature has no requirements doc; its acceptance criteria are those its backlog row numbers. Criteria are cited `A1`…`An`, in the row's order, wherever a full feature cites requirement rows: `## What this implements`, the `## Build plan` rows' *Discharges*, a plan's acceptance criteria, the code-writer brief and the G2 record all use them. Every brief a skill files for a lite feature says `lite`, gives the backlog's path and the feature's name, quotes the row, and cites criteria as A1…An; where a template has a requirements-doc field, the backlog row fills it. Where a brief template or an agent names the requirements doc or its rows, a lite feature's backlog row and locked design stand in their place.

## The three placements

The user story, the G0 ruling and the G2 record each have one place, all in the architecture doc. The order is the title, `## User story`, `## Context & scope`, then the template's sections in its order, `## What this implements` first. `## User story` holds the stories and one line saying the feature is lite and has no requirements doc. `## Context & scope` opens with the backlog row quoted in full with the backlog's path, then what the locked design commits the feature to, the G0 ruling, the scope agreed and what is out of scope. `doc-writer` writes both sections at G0, and `architect` writes beneath them and keeps them. The G2 record is last in `## Context & scope`, written by `architect-reconcile` in its record mode: one ruling line, `**Accepted at G2 on <YYYY-MM-DD>** by <who>, <form>, at <head>.`, then `- [x] A<n> — <the criterion as the row words it>` per criterion; evidence stays out of it.

## The escape

A lite build that meets a choice the locked design did not fix stops, and the feature returns to the full track, which writes its requirements doc. A build-time rule that follows from a standing rule of the design is not an unfixed choice: it is recorded in the architecture doc's `## Decision register` with the rule it follows from, and the build goes on. The choice is never ruled in place to keep the feature lite. The PM applies the standing-rule test; the entry is written by the next `architect` or `architect-reconcile` dispatch whose brief names it, and is listed at the next gate.

## The return to full

The return to full takes no ruling and no new stop. The PM states the unfixed choice and the return, then invokes `feature-requirement-doc`; its SETTLE stop is where the human first rules, opening with the choice. GATHER takes §1 and §2 from the architecture doc's two G0 sections, and the row's criteria open §9. Once the requirements doc exists the test of `## The mark` reads full at every later step, so the G2 record goes to the requirements doc; built phases stand. Once the requirements doc is filed, `architect-reconcile` in its record mode corrects the architecture doc at two sites and no others: the line in `## User story` saying the feature is lite and has no requirements doc is removed, and the frontmatter `sources:` entry naming the design's backlog is replaced by the requirements doc.
