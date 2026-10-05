# STATUS — VoXR-Speech-Recognition

updated: 2026-10-05

## Current

- **`main` at the PR #168 merge (`feat-resolver-slot-exemption`, G2 2026-10-03, closed #161); release `v2.0.0`.** `[Unreleased]` holds #143–#154, #146–#148, #160, #161; next: `release` (RSE-1 first).
- **`light-harness-upgrade`** · light, class slim · harness pack upgrade (core 0.42.0, unity 0.11.0, vc-git/light-lane/feature-track 0.17.0) at `d0d461d`: review 0 findings, EditMode 203/203, PlayMode 704/704 · next: G2-lite.
- **Open issues:** none from the plan; follow-ups in `## Deferred` and `memory/backlog-candidates.md`.
- **Plan of action** (2026-09-06 rulings, `Planning~/research/2026-09-05-next-level/05-rulings.md`): Phase A complete (#143–#148, #150, #153, #154 merged).
- Phase B free probes, none run: classify the 73 field false triggers; NCE/ECE of grammar `conf`; recorded coughs through the WebRTC VAD; Quest adb audio-effects session.
- Phase C instruments, none started: R1 parser A/B rig joined to the push-audio harness (gates Phase D); R5 human corpus; R4 negative corpus.
- Phase D after R1, none started: R11 endpointer, R15+R14 AGC/filter unit, R17 partials, R12 N-best, R22 union grammar, R41 lgraph, R30 listening states, model download-on-demand.

## Deferred

Open entries only, one line each, at most 200 characters: `**<ID>** · <VERDICT> · <route> · v<date> — <title>`, VERDICT OPEN or OPEN-DRIFTED.


- **RSE-1** · OPEN · release · v2026-10-04 — `[Unreleased]` #148 and #144 entries predate #161 (short pattern never reaches a resolver; runner-up order without DR-9): reword at release
- **RSE-2** · OPEN · light-lane · v2026-10-04 — codex page contract wants frontmatter on product pages; no `Documentation~` page has any: rule on the contract vs this surface
