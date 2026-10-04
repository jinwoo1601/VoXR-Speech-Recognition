# STATUS — VoXR-Speech-Recognition

updated: 2026-10-03

## Current

- **`main` at the PR #167 merge (`design-resolver-slot-scoring`, G1 2026-10-02); release `v2.0.0`.** `[Unreleased]` holds #143–#154, #146, #147, #148, #160; next release via `release`.
- **Open issues:** #161 ellipsis vs the score gate — design locked at G1 2026-10-02 (`scoring-model.md` A5); closes with its feature.
- **`feat-resolver-slot-exemption`** · feature (Full) · built, reviewed (`a658a53`), reconciled (`8a77f2c`) 2026-10-03 · next: G2; F11 manual Editor check due at G2.
- **Plan of action** (2026-09-06 rulings, `Planning~/research/2026-09-05-next-level/05-rulings.md`): Phase A complete (#143–#148, #150, #153, #154 merged).
- Phase B free probes, none run: classify the 73 field false triggers; NCE/ECE of grammar `conf`; recorded coughs through the WebRTC VAD; Quest adb audio-effects session.
- Phase C instruments, none started: R1 parser A/B rig joined to the push-audio harness (gates Phase D); R5 human corpus; R4 negative corpus.
- Phase D after R1, none started: R11 endpointer, R15+R14 AGC/filter unit, R17 partials, R12 N-best, R22 union grammar, R41 lgraph, R30 listening states, model download-on-demand.

## Deferred

Open entries only, one line each, at most 200 characters: `**<ID>** · <VERDICT> · <route> · v<date> — <title>`, VERDICT OPEN or OPEN-DRIFTED.


- **RSE-1** · OPEN · release · v2026-10-04 — `[Unreleased]` #148 and #144 entries predate #161 (short pattern never reaches a resolver; runner-up order without DR-9): reword at release
- **RSE-2** · OPEN · light-lane · v2026-10-04 — codex page contract wants frontmatter on product pages; no `Documentation~` page has any: rule on the contract vs this surface
- **RSE-3** · OPEN · issue · v2026-10-04 — a `VoxrTestCase` added by the Inspector + button gets word confidence 0.00, not -1 (`VoxrTestCase.cs:25`): file as an issue?
