# STATUS — VoXR-Speech-Recognition

updated: 2026-11-06

## Current

- **`main` at PR #190 `71ff1ea` (agc-filter G1); `v2.0.0`; running: `feat-agc-filter` (G0), `light-batch-b`; batch C waits.** `[Unreleased]`: #143–#154, #146–#148, #160, #161, #178, #185.
- **`lab-next-level-probes` (lab, marker branch, never checked out): Phase B probes in `.scratch/lab-next-level-probes/`; next: probe plan.**
- **`release` held** by the human 2026-10-13 (RSE-1 first when it resumes; `2.1.0` proposed).
- **Open issues:** none from the plan; follow-ups in `## Deferred` and `memory/backlog-candidates.md`.
- **Plan of action** (2026-09-06 rulings, `Planning~/research/2026-09-05-next-level/05-rulings.md`): Phase A complete (#143–#148, #150, #153, #154 merged).
- Phase B probes, re-ruled 2026-10-13, none run: suspect field-trigger table; TTS NCE/ECE dry run; Quest cough VAD test; Quest adb audio-effects session.
- Phase C instruments: R1 merged (#177 parser half, #178 recogniser half; Phase D unblocked); R5 human corpus and R4 negative corpus not started.
- Phase D after R1: R11 merged (#185); not started: R15+R14 AGC/filter unit, R17 partials, R12 N-best, R22 union grammar, R41 lgraph, R30 listening states, model download-on-demand.
- **`feat-agc-filter`**: feature; full; path development; stage implement - plan-1 `8741171`; tree `D:\Workspace\VoXR-Speech-Recognition.trees\feat-agc-filter`; next: implement 1 (blocked: human vendor archives, plan Step 0)
- **`light-batch-b`**: light, class slim, batch EPR-8, EPR-9, EPR-10, WND-2; tree `.trees\light-batch-b`; PR #191 @ `6250355`; next: merge · ruling G2 · 2026-11-06 · the human · "Accept (Recommended)"

## Deferred

Open entries only, one line each, at most 200 characters: `**<ID>** · <VERDICT> · <route> · v<date> — <title>`, VERDICT OPEN or OPEN-DRIFTED.


- **RSE-1** · OPEN · release · v2026-10-04 — `[Unreleased]` #148 and #144 entries predate #161 (short pattern never reaches a resolver; runner-up order without DR-9): reword at release
- **RSE-2** · OPEN · light-lane · v2026-10-04 — codex page contract wants frontmatter on product pages; no `Documentation~` page has any: rule on the contract vs this surface
- **LHU-1** · OPEN · harness-hub · v2026-10-07 — vc-git `Bash(git branch:*)` allow admits a clustered force-delete (`git branch -vD x`) no ask rule matches: fix in the base template
- **WND-2** · OPEN · light-lane · v2026-10-14 — `core.autocrlf=true` checks `generate.sh` out CRLF; Git Bash proven on CRLF `stage.sh` only: run a CRLF `generate.sh` or pin `eol=lf`
- **WND-4** · OPEN · design-relock · v2026-10-14 — locked `windows-native-toolchain.md:44` cites the recipe's pre-Phase-1 `:21`, `:35`, `:71`: re-point at the next unlock
- **JRR-1** · OPEN · human · v2026-10-15 — joined-rig-recogniser bridge change (2 exports, rebuilt `.so`): Quest on-device check not stated at G2
- **JRR-2** · OPEN · human · v2026-10-15 — joined-rig-recogniser: human read of the 16-fixture recogniser-layer report (split case, flips legible) not stated at G2
- **EPR-1** · OPEN · design-relock · v2026-11-04 — locked `joined-rig.md` rule 2 (:66, :84, :108): kill check compares final texts; `ab-rig/Joined.cs` :214-277 also compares gain, libvosk, conf
- **EPR-3** · OPEN · light-lane · v2026-11-04 — Tactical sample shows a literal `\n` placeholder; its Quest setup works on a flat screen only
- **EPR-4** · OPEN · light-lane · v2026-11-04 — `MetaQuestFeatureBuildHooks` throws a NullReferenceException on every Android build (host TestGround)
- **EPR-5** · OPEN · human · v2026-11-04 — TestGround's modified `OpenXRPackageSettings.asset` and `EditorBuildSettings.asset` from the Phase 8 sitting: keep or revert in the host
- **EPR-8** · OPEN · light-lane · v2026-11-04 — confgen's echo misses a BOM-prefixed first rule line
- **EPR-9** · OPEN · light-lane · v2026-11-04 — confgen never re-checks an existing `--out` against `--model`
- **EPR-10** · OPEN · light-lane · v2026-11-04 — not-applied warning wording does not tell "nothing to apply" from "could not apply" (duplicate key under Model default)
- **HRP-1** · OPEN · light-lane · v2026-11-05 — host_run.py runs Unity with no timeout: a hung run holds the host lock; bound it and kill before the restore
- **HRP-2** · OPEN · light-lane · v2026-11-05 — binding unity.md Command 1 says Commands 1-2 start "in the background"; Claude Code's memory reap can kill an idle run: say foreground
- **TFL-1** · OPEN · harness-hub · v2026-11-05 — vc Merge procedure flagged `slots to revisit` at vc-git 0.47.0, not acted on in trees-fill P4: rule adopt/merge/keep at the next upgrade
- **TFL-2** · OPEN · harness-hub · v2026-11-05 — generated settings.json: no allow for `git pull`, `git merge`, `git -C`; `hub.py write` allows miss from a `.trees` tree: fix in the base template
- **TFL-3** · OPEN · harness-hub · v2026-11-05 — `chore/release-X.Y.Z` yields a nested tree dir under the trees Location; `tree-remove.py` leaves an empty `chore\`
- **TFL-4** · OPEN · light-lane · v2026-11-05 — vc-git.md drift: Check-in lacks the template's tree-session clauses (review R2); stale trees Interview comment; tree path in 3 bindings
- **TFL-5** · OPEN · human · v2026-11-05 — snapshot ref `refs/harness/pre-upgrade/6d07a5f` (trees-fill review recompose) kept until the human drops it by `git update-ref -d`
- **AGF-1** · OPEN · design-relock · v2026-11-05 — locked joined-rig docs cite `native-bridge.md` :61 for the AGC divergence, which it no longer states: re-point at the next unlock
- **VCG-1** · OPEN · harness-hub · v2026-11-05 — vc-guard READ_VERBS lacks `check-attr`: a subagent's attribute read is refused (light-batch-b WND-2 debugger); add it upstream
