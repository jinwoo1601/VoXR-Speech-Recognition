You're picking up: plan Phase 4 (overlay core and `confgen`) of `feat-endpointer-profile` through `phase-pickup`. Read the files below, then start at Next steps.

## Objective

Run `phase-pickup` for Phase 4 of `endpointer-profile`: recon, plan, persist `Planning~/features/endpointer-profile/plan-4.md`, validate it, and have the human approve it before check-in. The task is done when plan-4 is persisted, validated, approved and checked in.

## Lane

feature

## Context

- Branch `feat-endpointer-profile`, the one checkout (`trees` is none), head `c678528`, pushed. STATUS `## Current` says Phase 3 built and next `phase-pickup` Phase 4. The session note is `memory/2026-10-16.md`; its last entries are "Phase 3 implement (built)" and this handoff.
- Requirements: `Planning~/features/endpointer-profile/requirements.md` (F3, F7, F8, F9). Architecture: `Planning~/features/endpointer-profile/architecture.md`, `## Build plan` Phase 4 (:145) and `## Decision register`. Design (locked): `Planning~/design-docs/endpointer-profile.md` (§6, §10 items 3-4).
- Phase 4 scope, from the build plan: `EndpointerOverlay.cs` with only the "Model default" row and no call site; EditMode tests of the rules and the §6 cases; PlayMode tests on the core extractor (extract; apply a tuned row; re-extract, with the stamp bytes unchanged and a planted file surviving; apply "Model default", giving the archive's bytes); and `confgen`. Green: Commands 1-2; Command 3; a `--dump` on `confgen --rule2 0.35`'s output whose `modelConf` hashes to `confgen`'s SHA-256; and a `diff` against the vendor conf that shows only the rule2 line.

## Current state

- Phases 1-3 are built and checked in: `2aed6fa` (pause-ladder corpus), `9617e63` (conf record), `c678528` (rig sweep machinery: `stage.sh`/`joined.sh` `--buffer time|eager --window <s>`, the `## Sweep measures` report section).
- Phase 3's trail is in `.scratch/`: `implement-`, `compile-check-` and `architect-endpointer-profile-phase-3-*`. Its probes are under `.scratch/endpointer-profile-phase-3/`.
- Phase 4 is the first phase that touches `Runtime/` and the Unity tests, so bound Commands 1-2 (batchmode through the host, Editor closed) run at its gate. Command 3 runs too, per the build plan.

## Key files & locations

- Verification: `.claude/bindings/unity.md` `## verification` (Commands 1-4, *Failure rule*); the procedure and toolchain are in `Planning~/verification-recipe.md`.
- The rig, for the `--dump`/`confgen` green: `Planning~/features/coverage-in-selection/ab-rig/`. The pin `NativeBridge~/harness/expectations.json` carries `modelConf`. The vendor conf is `NativeBridge~/vendor/vosk-model-small-en-us-0.15/conf/model.conf`.
- Prior plans, for format and budget (10 KB): `Planning~/features/endpointer-profile/plan-{2,3}.md`.

## Constraints & decisions

- Facts the build has taught (also for the as-built reconcile at the build plan's end):
  - Agents' Bash tool collapses `\\` to `\`, so probes stay backslash-free, with Python in `.py` files run by `python3 -I`.
  - In Git Bash, `grep -c $'\r$'` prints 0 on CRLF files.
  - Git Bash's GNU sed 4.9 `sed -i` strips CR from every line it writes, and `sed -b -i` keeps it.
  - The vc-guard hook refuses `git init` (and any VC-state change) to subagents. A check that needs it is run by the human with `!`.
  - `compile-check` has no Write tool, so probe files are written by `code-writer`.
- Writer choices left open by plan-3, held for G2: the S line with n = 0 reads `n/a s over 0 cases`; each side's finals mean is over its own scored cases; S and KC2 count only cases scored on both sides; the differs stop prints raw JSON.
- Phase 5 observation: on the self-pair, W 0.5 scored 31 correct while W 2.0 scored 37/37.
- Still owed to the as-built reconcile (from the Phase 3 handoff): `joined-rig.md`'s rule 2 wording and the stale `:143-195` cite. Stale rig cites: the `Joined.cs` kill check is :214-277 (not :214-266) and the dump-header copy :318-322 (not :307-311); `JoinedInput.cs` :153-161 and :606-608 (not :151 and :587-592).
- Compile and test output goes to `compile-check`. File every agent's report by hand beside its brief in `.scratch/`. Windows-native in Git Bash only, never WSL. A contradiction with the locked design reopens it on a `design-` branch; it is never patched.

## Next steps

1. Invoke `phase-pickup` for `feat-endpointer-profile` Phase 4.

## Open questions

- None known.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-endpointer-profile; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor d34dc111-5b60-4380-86e4-ddbd55a470a7". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; if the bare name is refused as ambiguous, use the ref `ListAgents` shows for the one on this machine.)
