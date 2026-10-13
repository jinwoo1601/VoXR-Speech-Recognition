# STATUS — VoXR-Speech-Recognition

updated: 2026-10-13

## Current

- **`main` at the PR #172 merge (`light-harness-upgrade-3`, G2-lite 2026-10-13); release `v2.0.0`.** `[Unreleased]` holds #143–#154, #146–#148, #160, #161.
- **Next: `feat-joined-rig-parser` at G0 once the `design-joined-rig` PR merges** (`joined-rig` G1 LOCKED 2026-10-13; backlog: parser, then recogniser).
- **`lab-next-level-probes` (lab, marker branch, never checked out): Phase B probes in `.scratch/lab-next-level-probes/`; next: probe plan.**
- **`release` held** by the human 2026-10-13 (RSE-1 first when it resumes; `2.1.0` proposed).
- **Open issues:** none from the plan; follow-ups in `## Deferred` and `memory/backlog-candidates.md`.
- **Plan of action** (2026-09-06 rulings, `Planning~/research/2026-09-05-next-level/05-rulings.md`): Phase A complete (#143–#148, #150, #153, #154 merged).
- Phase B probes, re-ruled 2026-10-13, none run: suspect field-trigger table; TTS NCE/ECE dry run; Quest cough VAD test; Quest adb audio-effects session.
- Phase C instruments, none started: R1 parser A/B rig joined to the push-audio harness (gates Phase D); R5 human corpus; R4 negative corpus.
- Phase D after R1, none started: R11 endpointer, R15+R14 AGC/filter unit, R17 partials, R12 N-best, R22 union grammar, R41 lgraph, R30 listening states, model download-on-demand.

## Deferred

Open entries only, one line each, at most 200 characters: `**<ID>** · <VERDICT> · <route> · v<date> — <title>`, VERDICT OPEN or OPEN-DRIFTED.


- **RSE-1** · OPEN · release · v2026-10-04 — `[Unreleased]` #148 and #144 entries predate #161 (short pattern never reaches a resolver; runner-up order without DR-9): reword at release
- **RSE-2** · OPEN · light-lane · v2026-10-04 — codex page contract wants frontmatter on product pages; no `Documentation~` page has any: rule on the contract vs this surface
- **LHU-1** · OPEN · harness-hub · v2026-10-07 — vc-git `Bash(git branch:*)` allow admits a clustered force-delete (`git branch -vD x`) no ask rule matches: fix in the base template
- **WSL-1** · OPEN · light-lane · v2026-10-13 — verification binding (Commands 3–4) and recipe still route harness and Samples~ compile via WSL; Claude Code is Windows-native: rule on toolchain
