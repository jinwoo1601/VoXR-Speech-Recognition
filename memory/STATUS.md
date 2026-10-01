# STATUS — VoXR-Speech-Recognition

updated: 2026-10-01

## Current

- **`main` at the PR #164 merge (`light-workspace-move`, closed at G2-lite 2026-10-01); latest release `v2.0.0`.** `CHANGELOG.md` `[Unreleased]` holds the work since (#143–#154, #146, #147, #148); next release via the `release` skill.
- **Open issues:** #160 unify the three unfilled-required-slot walks; #161 ellipsis vs the score gate (should a resolver-filled slot still be charged?). Both enhancement.
- **Plan of action** (2026-09-06 rulings, `Planning~/research/2026-09-05-next-level/05-rulings.md`): Phase A complete (#143–#148, #150, #153, #154 merged).
- Phase B free probes, none run: classify the 73 field false triggers; NCE/ECE of grammar `conf`; recorded coughs through the WebRTC VAD; Quest adb audio-effects session.
- Phase C instruments, none started: R1 parser A/B rig joined to the push-audio harness (gates Phase D); R5 human corpus; R4 negative corpus.
- Phase D after R1, none started: R11 endpointer, R15+R14 AGC/filter unit, R17 partials, R12 N-best, R22 union grammar, R41 lgraph, R30 listening states, model download-on-demand.

## Deferred

Open entries only, one line each, at most 200 characters: `**<ID>** · <VERDICT> · <route> · v<date> — <title>`, VERDICT OPEN or OPEN-DRIFTED.

- **A1** · OPEN · light-lane · v2026-10-01 — install instructions in `README.md` / `getting-started.md` use a git URL to a now-private repo; consumers need credentials.
- **A2** · OPEN · light-lane · v2026-10-01 — `CLAUDE.md` bullet "pair with the build command below" dangles; the build command left with the old workflow section.
- **A3** · OPEN · host branch · v2026-10-01 — Unity EditMode/PlayMode waived for `light-workspace-move`; run them once `VoXR TestGround` fixes its `file:` path.
