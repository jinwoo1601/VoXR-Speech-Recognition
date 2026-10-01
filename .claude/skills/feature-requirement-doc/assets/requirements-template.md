---
type: requirements
feature: <kebab-name>
topic: <the design topic this feature belongs to>
status: draft
updated: <YYYY-MM-DD>
sources:
  - <the locked design doc(s) this feature derives from>
  - <the decision record(s) behind them>
---

# <Feature Name> — Requirements

<!-- This doc is the G2 acceptance checklist for the feature, written in advance. What and why, never how (the "how" is the architecture doc). Keep each paragraph and list item on one logical line — no hard-wrapped prose; match the link and date form the project's existing process docs use. Delete these guidance comments as you fill each section. -->

## 1. User story

<!-- The story or stories this feature serves, each in the form *As a <user>, I want <capability>, so that <value>.*, captured verbatim as the human states it, numbered `S1`, `S2`, …; every requirement below traces to one of them. -->

- **S1** — As a <user>, I want <capability>, so that <value>.

## 2. Feature & scope

<!-- The one-line scope (the backlog line), then two to four sentences: what this feature is, and the slice of locked design it realizes. Link the parent design docs and decision records in the project's link form so they stay navigable. -->

## 3. Why — the value it delivers

<!-- The goal of the locked design this feature carries, and what its users get that they did not have before. This is the "why" behind every requirement below — the thing you protect when a tradeoff forces a cut. -->

## 4. Observable behaviour

<!-- The experiential spec: what someone using this feature does, sees and gets back, step by step. Written before the table below, because every row in it is a slice of this. -->

## 5. Functional requirements

<!-- Numbered, testable, prioritized. Each row: the requirement (what and why, not how), its priority, how it is verified, and its parent in locked design; each row names the story it serves. -->

| #  | Requirement | Story | Priority | Acceptance check | Source |
|----|-------------|-------|----------|------------------|--------|
| F1 | <observable behaviour> | S1 | Must / Should / Could | <how it is verified> | <design doc / decision record> |
| F2 |             |       |          |                  |        |

## 6. Non-functional requirements

<!-- First-class: on some platforms these are the feature-killers. Keep the rows that apply, add the ones this project's platform makes load-bearing, and cut the rest rather than leaving them blank. -->

- **Performance:** <the budget this feature must hold, and any per-operation ceiling if it is on a hot path>
- **Robustness at the input boundary:** <what the feature does with malformed, ambiguous or low-confidence input — the response it gives, never silence>
- **Degraded dependencies:** <what it does when something it depends on is slow, unavailable or partially wrong>
- **Accessibility:** <if applicable>

## 7. Non-goals / deferred

<!-- As sharp as the goals. What this feature does NOT do; what is explicitly deferred to a later feature; which adjacent systems it touches but does not own. This is the scope fence — the thing that stops a two-week feature becoming two months. -->

## 8. Dependencies & assumptions

<!-- What must already exist or be merged first (features are dependency-ordered — declare the order). What is assumed about the people using this and about the rest of the system. Unstated assumptions are where requirements rot. -->

## 9. Acceptance criteria (G2)

<!-- The consolidated "done" checklist the human walks at G2. Pull the Must-priority checks from §5, then add the verification only a human can do and the commands the project's verification binding names. If every §5 row already carries its check, this can just collect the Musts plus those two. -->

- [ ] <criterion — from the §5 Musts>
- [ ] Human-only verification: <what the human must exercise by hand to accept this>
- [ ] The commands the project's `verification` binding names are green

## 10. Open questions

<!-- The unknowns you did NOT resolve — handed forward to the architecture doc. Honest beats complete; the architecture doc would rather inherit a known unknown than hit it mid-build. -->
