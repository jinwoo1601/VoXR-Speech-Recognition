# STATUS — VoXR-Speech-Recognition

updated: 2026-11-09

## Current

- **`main` at PR #198 `63434cb`; `v2.0.0`; running: `feat-bench-paced-mode`; RSE-2 held.** `[Unreleased]`: #143–#154, #160, #161, #178, #185, batch B, EPR-11, #193, batches C–E, #198.
- **`lab-next-level-probes` (lab, marker branch, never checked out): Phase B probes in `.scratch/lab-next-level-probes/`; next: probe plan.**
- **`release` held** by the human 2026-10-13 (RSE-1 first when it resumes; `2.1.0` proposed).
- **Open issues:** none from the plan; follow-ups in `## Deferred` and `memory/backlog-candidates.md`.
- **Plan of action** (2026-09-06 rulings, `Planning~/research/2026-09-05-next-level/05-rulings.md`): Phase A complete (#143–#148, #150, #153, #154 merged).
- Phase B probes, re-ruled 2026-10-13, none run: suspect field-trigger table; TTS NCE/ECE dry run; Quest cough VAD test; Quest adb audio-effects session.
- Phase C instruments: R1 merged (#177 parser half, #178 recogniser half; Phase D unblocked); R5 human corpus and R4 negative corpus not started.
- Phase D after R1: R11 merged (#185), R15+R14 AGC/filter unit merged (#193); not started: R17 partials, R12 N-best, R22 union grammar, R41 lgraph, R30 listening states, model download-on-demand.
- **`feat-bench-paced-mode`**: feature; full; path development; stage product-docs - audio-bench row 4; head `f66b597` (review `0bfed42`, as-built `6c6c3c7`); next: re-preflight, G2 HARD STOP

## Deferred

Open entries only, one line each, at most 200 characters: `**<ID>** · <VERDICT> · <route> · v<date> — <title>`, VERDICT OPEN or OPEN-DRIFTED.


- **RSE-1** · OPEN · release · v2026-10-04 — `[Unreleased]` #148 and #144 entries predate #161 (short pattern never reaches a resolver; runner-up order without DR-9): reword at release
- **RSE-2** · OPEN · light-lane · v2026-10-04 — codex page contract wants frontmatter on product pages; no `Documentation~` page has any: rule on the contract vs this surface
- **LHU-1** · OPEN · harness-hub · v2026-10-07 — vc-git `Bash(git branch:*)` allow admits a clustered force-delete (`git branch -vD x`) no ask rule matches: fix in the base template
- **WND-4** · OPEN · design-relock · v2026-10-14 — locked `windows-native-toolchain.md:44` cites the recipe's pre-Phase-1 `:21`, `:35`, `:71`: re-point at the next unlock
- **JRR-1** · OPEN · human · v2026-10-15 — joined-rig-recogniser bridge change (2 exports, rebuilt `.so`): Quest on-device check not stated at G2
- **JRR-2** · OPEN · human · v2026-10-15 — joined-rig-recogniser: human read of the 16-fixture recogniser-layer report (split case, flips legible) not stated at G2
- **EPR-1** · OPEN · design-relock · v2026-11-04 — locked `joined-rig.md` rule 2 (:66, :84, :108): kill check compares final texts; `ab-rig/Joined.cs` :214-277 also compares gain, libvosk, conf
- **EPR-5** · OPEN · human · v2026-11-04 — TestGround's modified `OpenXRPackageSettings.asset` and `EditorBuildSettings.asset` from the Phase 8 sitting: keep or revert in the host
- **TFL-1** · OPEN · harness-hub · v2026-11-05 — vc Merge procedure flagged `slots to revisit` at vc-git 0.47.0, not acted on in trees-fill P4: rule adopt/merge/keep at the next upgrade
- **TFL-2** · OPEN · harness-hub · v2026-11-05 — generated settings.json: no allow for `git pull`, `git merge`, `git -C`; `hub.py write` allows miss from a `.trees` tree: fix in the base template
- **TFL-3** · OPEN · harness-hub · v2026-11-05 — `chore/release-X.Y.Z` yields a nested tree dir under the trees Location; `tree-remove.py` leaves an empty `chore\`
- **TFL-5** · OPEN · human · v2026-11-05 — snapshot ref `refs/harness/pre-upgrade/6d07a5f` (trees-fill review recompose) kept until the human drops it by `git update-ref -d`
- **AGF-1** · OPEN · design-relock · v2026-11-05 — locked joined-rig docs cite `native-bridge.md` :61 for the AGC divergence, which it no longer states: re-point at the next unlock
- **VCG-1** · OPEN · harness-hub · v2026-11-05 — vc-guard READ_VERBS lacks `check-attr`: a subagent's attribute read is refused (light-batch-b WND-2 debugger); add it upstream
- **AGF-2** · OPEN · human · v2026-11-07 — agc-filter PM-2: `vosk_bridge_init`'s failure path when `FrontEnd` throws (frees model and recogniser, MODEL_LOAD_FAILED) is unrun on device and untested
- **LBC-2** · OPEN · human · v2026-11-07 — EPR-3 (#194) Editor scene-open + Play (line breaks, five panels) and Quest check (both eyes, legible at 1 m, head-follow) not stated at G2
- **LBD-1** · OPEN · human · v2026-11-07 — HRP-1 (#195): a real hung Unity on the host, and whether `taskkill /T` reaches every Unity child (AssetImportWorker), never run
- **BFR-1** · OPEN · light-lane · v2026-11-09 — bench-fast-replay: `generate.py` regeneration runs `write_truth` after the WAV loop (:264), unverified: nothing was regenerated
- **BFR-2** · OPEN · light-lane · v2026-11-09 — bench-fast-replay: four `bench.sh` stops unexercised (build failed, applied-grammar guard, staged error log, command pending at the horizon)
- **BFR-3** · OPEN · light-lane · v2026-11-09 — `verification` binding Command 3 expects `37/37`, stale: `NativeBridge~/harness/expectations.json` holds 52 cases; fix the binding
