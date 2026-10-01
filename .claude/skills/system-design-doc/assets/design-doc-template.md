---
type: system
topic: <the design topic this system belongs to>
tags: [<kebab, tags>]
status: draft
updated: <YYYY-MM-DD>
sources:
  - <the parent doc and the section this system derives from>
  - <the topic's decisions/adr-NNNN-*.md>
  - "<Session direction YYYY-MM-DD (...)>"
---

# <System Name>

<!-- This is a DESIGN-track doc: specify the MODEL, argue the DYNAMICS it produces, trace it to a goal the locked design states. The design intent is what G1 locks; numbers are provisional (→ the tuning pass). It is NOT implementation (→ architecture doc) and NOT a buildable acceptance check (→ requirements doc). Match the prose and linking convention the project's existing process docs use. Order the middle sections to fit the system; tables and matrices sharpen a model. Keep the top callout and the two bottom bookends. Delete these guidance comments as you fill each section. -->

<!-- OPENING (one paragraph): what this system is, where it sits in the topic, the keystone fork/ADR it resolves, and the one governing constraint that shapes everything below. Name the goal(s) of the locked design it carries, and cite where they are stated. -->

> **What's decided.** <The locked skeleton in a sentence or two — the decisions G1 locks.> All numbers on this page are **provisional → the tuning pass** (the design intent is the load-bearing part).

## <The model>

<!-- The mechanism at MODEL altitude: the state it tracks, the rules, how one step — a request, a turn, an order — resolves. Use a table or matrix where it sharpens the model. NO numbers presented as decided — flag them provisional → the tuning pass. NO class names or components — that is the architecture doc. -->

## The decisions it puts to its operator

<!-- The heart. What choices does this system put in front of the person or the caller driving it? For each: the choice, the information available when choosing, the opportunity cost, and why no single option dominates. A system with no interesting decision is an animation, not a system. -->

## Dynamics & failure modes

<!-- Reason FORWARD from the rules to the behaviour that emerges. Then attack your own rules: the dominant strategy, the degenerate loop, the dead zone — and name the rule that kills each. This section is what separates a design from a rulebook. -->

## Boundaries & couplings

<!-- What this system OWNS; what it CONSUMES from sibling systems; what it HANDS OFF and to where; and what it explicitly does NOT decide. This is the scope fence that keeps the doc independently lockable. -->

## What it delivers

<!-- Close the loop: this model → this behaviour → the goal the locked design promised. If you cannot state what it is like when it goes right, the dynamics argument above is not finished. -->

## Status / openness

<!-- The lock-state and the honest unknowns. -->

- **Status: draft.** <The locked skeleton vs what is still drafting; which forks are recorded in ADRs.>
- **All numbers provisional → the tuning pass:** <list the key deferred numbers.>
- **Hard dependencies / open:**
  - <a forward dependency or an open fork — mark RESOLVED ones with their ADR and where they landed.>

## Related

<!-- Dense list of links, in the link form this project's process docs use: the coupled systems, the ADRs, the parent hub, and the pages this touches. -->
<links>
