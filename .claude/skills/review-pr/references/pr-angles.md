# PR-specific finder angles

Angles that exist only in a PR review, because only a PR carries a public claim about
itself. Pasted into a `review-angle` agent's call prompt exactly like a roster entry.

Every other angle in a PR review comes verbatim from the composed roster set — the core
roster at `.claude/references/core/angle-roster.md` plus each composed pack's
`.claude/references/<pack>/angle-roster.md`. Do not restate or paraphrase those here — one
definition, one home.

## Claim family

### CLAIM — claim audit
Read the PR title, the PR body, and the linked issue (all reproduced in the brief), then
establish for each claim whether the diff actually delivers it. Three things to report:
claims the diff does not deliver or only partially delivers; ticked validation boxes
asserting a check that the diff, the repo's CI, or the stated environment could not have
performed; and parts of the linked issue's reported problem the diff leaves unaddressed.
Quote the claim and the contradicting (or missing) code with `file:line`.
Do NOT report: code defects, style, or coverage gaps — other angles own those. Your
subject is strictly the gap between what the PR says about itself and what it does.
An unsupported claim is a finding even when the underlying code is correct.
