# STATUS — VoXR-Speech-Recognition

updated: 2026-10-14

## Current

- **`main` at the PR #176 merge (row 2, G2 2026-10-14); `v2.0.0`; toolchain backlog done; in flight: `feat-joined-rig-parser`.** `[Unreleased]` holds #143–#154, #146–#148, #160, #161.
- **`feat-joined-rig-parser` (feature, Full): stage architecture: doc filed, 4-phase plan approved 2026-10-14; next: `phase-pickup` Phase 1 (harness dump).**
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
- **WND-1** · OPEN · light-lane · v2026-10-14 — binding Command 3's `cd NativeBridge~ && …` leaves the shell in the subdirectory (the shape fixed for Command 4): wrap in a subshell
- **WND-2** · OPEN · light-lane · v2026-10-14 — `core.autocrlf=true` checks `generate.sh` out CRLF; Git Bash proven on CRLF `stage.sh` only: run a CRLF `generate.sh` or pin `eol=lf`
- **WND-3** · OPEN · light-lane · v2026-10-14 — binding Command 4 expects "one CS0618 warning", but an incremental rebuild prints 0 warnings: say the warning shows on a fresh compile
- **WND-4** · OPEN · design-relock · v2026-10-14 — locked `windows-native-toolchain.md:44` cites the recipe's pre-Phase-1 `:21`, `:35`, `:71`: re-point at the next unlock
- **JRP-1** · OPEN · feature · v2026-10-14 — joined-rig dump times partials only by a ring-wide bracket (ruled (a)): exact partial timing is `joined-rig-recogniser`'s, a bridge change
