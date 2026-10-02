You're picking up: GitHub issue #161 (should a resolver-filled slot still be charged by the score gate?). Read the files below, then start at Next steps.

## Objective

Take up issue #161, the only open issue, through its G0 scope: open a design topic for it, agree the scope with the human, and cut its design branch. The human asked to "tackle" the next issue; #161 declares itself design-track (below), so the G0 scope stop is where the human confirms that lane or overrules it.

## Lane

design

## Context

- Repo `D:\Workspace\VoXR-Speech-Recognition` (Unity package `com.jinwoo1601.voxr`), on `main` at `0acbc9d` (PR #166 merged), one checkout (`trees` is none). `memory/` lives here; the uncommitted `memory/` edits from the previous session (this brief and its dated-note entry) carry onto the branch you cut.
- Issue #161 — read it whole: `gh issue view 161 --json title,body,labels,assignees,comments` (no comments yet). In short: ellipsis (#148, the slot resolver) and the score gate pull against each other; a required-slot omission costs `-1`, so short patterns — the aggressive `{track}` orders the resolver exists for — fall below `minScore` (0.6) before a resolver is ever asked (`"launch missiles"` scores 0.250). Measured on `VR FTL-Like 3` `Set_Combat`: resolver reachable for 18/40 pattern×slot occurrences dropping the slot alone, **5/40** in the natural spoken form. Options: (1) ship as built — taken for #148, not a resolution; (2) credit a resolver-filled slot as matched — not recommended (conflicts with ruling 3: "I don't want commands firing that were not of user's intention"); (3) waive the penalty without crediting — still under the gate; (4) **exempt a resolver-fillable slot from the denominator** — `"launch missiles"` → 0.667, the issue's strongest candidate, with open questions (configuration-dependent score only when a resolver is registered? interaction with the leading-miss bar when the exempted slot is the anchor? effect on the sibling-tie reachability scan, which reads the same `minScore`?).
- **Why design, not light:** the issue says "This needs a design branch (G0/G1), not a feature branch — every option below changes `scoring-model.md`, which is locked and complete." `Planning~/design-docs/scoring-model.md` is LOCKED (re-locked at G1 2026-08-14 with Amendment A3); per the constitution a locked doc changes only by unlocking on a design branch and re-locking at G1. `tackle-issue`'s prerequisites send work like this to the tracks.
- Non-goal (from the issue): do not reopen the leading-required-miss bar — 0/40 barred; ruling 3 is absolute. Any design must say why it beats the existing workaround (a slot-less sibling pattern matching the elided utterance at 1.000).
- Not measured (from the issue): acoustics, the confidence gate, and real speakers' elision shapes; a human corpus (Phase C instrument R5 in `memory/STATUS.md`) would change what the design can be argued from.

## Key files & locations

- `Planning~/design-docs/scoring-model.md` — the locked scoring model every option amends.
- `Planning~/design-docs/leading-miss-bar.md` — ruling 3 / the leading-miss bar (non-goal).
- `Planning~/design-docs/sibling-tie-disambiguation.md` — the reachability scan that reads `minScore`.
- `Planning~/features/slot-resolver/requirements.md` §6 **E-2** (around line 142) — the provenance of #161; F7 (line 68).
- `Planning~/features/slot-resolver/architecture.md` — the resolver as built (#148).
- `Runtime/Commands/VoxrCommandParser.cs` — scoring and the unfilled-required-slot walk (`UnfilledRequiredSlots`, unified by #160); `Runtime/Commands/VoxrCommandRecogniser.cs` — `TryResolveMissingSlots`, the score gate.
- `memory/STATUS.md`; `memory/2026-10-01.md` (today's entries, latest last).
- Design docs live at `Planning~/design-docs/<topic>.md` (the `process-docs` binding, `.claude/bindings/core.md`).

## Constraints & decisions

- No doc or code before the design branch exists (G0). The branch form is `design-<topic>`, cut from an up-to-date `main` (`.claude/bindings/vc-git.md` `## vc`); suggested topic `resolver-slot-scoring`.
- Amending `scoring-model.md` happens on the design branch and is re-locked by the human at G1 — never patched in place.
- #148 shipped option 1 by the maintainer's ruling of 2026-09-16; that ruling stands until G1 says otherwise.

## Next steps

1. Invoke `g0-open` for a design topic from issue #161 (suggested branch `design-resolver-slot-scoring`). At its SCOPE stop, put to the human that #161 is design-track (reason above) although the ask was "tackle the next issue" — if the human wants the light lane instead, stop and say `tackle-issue` cannot amend a locked doc.
2. After G0, the design doc is `system-design-doc`'s work.

## Open questions

- Lane: design (per the issue and the lock) vs. the human's "tackle" — settled at G0.
- Whether the design waits on, or proceeds without, a human corpus (the issue says the 45% / 12.5% split is a property of the grammar, not of observed speech).

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - design-resolver-slot-scoring; lane: design; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps. BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer.
