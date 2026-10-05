You're picking up: upgrade this project's harness packs to the base's current versions. Read the files below, then start at Next steps.

## Objective

Run the `upgrade` skill for `D:\Workspace\VoXR-Speech-Recognition` on a light-lane branch, through its dry-run stop (the human rules on the whole report), apply on the human's word, then review and close at G2-lite. Done = composed files at the base's pack versions, `validate` and `doctor` PASS, PR merged by the human.

## Lane

light, class slim

## Context

- Repo `D:\Workspace\VoXR-Speech-Recognition`, one checkout (`trees` is none), on `main` at `95b9fdc`, clean except the untracked `.claude/csharpier/` (CSharpier hook artefact - never stage it). No branch sessions running. Issue list empty (#161 closed by PR #168).
- Requested by `harness-hub` at the human's word (2026-10-05); the human chose to run it in this fresh session. Base `D:\Workspace\harness` (`base` in `.claude/harness.json`) has moved: core 0.35.0 -> 0.42.0, unity 0.8.0 -> 0.11.0, vc-git 0.16.0 -> 0.17.0, light-lane 0.15.0 -> 0.17.0, feature-track 0.16.0 -> 0.17.0; codex 0.6.0 and design-track 0.5.0 unchanged (as stated by harness-hub; the dry run is the authority).
- Permission-relevant, per harness-hub: core 0.42.0 adds a PreToolUse hook fencing rm/rmdir to the project, its trees, the scratch area and the temp folder, and drops core's rm/rmdir ask rules; unity 0.11.0 brings the unity pack's settings rules this project has never had. Put both to the human at the dry-run stop.
- Hazard: core 0.41.0 changed the hub report lines (STARTED names the predecessor; HANDOFF lines). No branch session is running now, so nothing needs to catch up first; this session itself is a branch session, and the hub (`voxr-hub`) still runs the old composition until it restarts.
- Precedent: `light-harness-adopt` (PR #163) ran as light, class slim; see `memory/2026-10-01.md`.

## Key files & locations

- `.claude/harness.json` - manifest (packs and versions, `base`).
- `.claude/bindings/*.md` - bindings; `vc-git.md` `## vc` holds the branch procedure and the snapshot procedure the upgrade's SNAPSHOT uses.
- `.claude/settings.json` (composed) and `.claude/settings.local.json` (local; ~66 dead or one-off rules - its cleanup is NOT part of this upgrade).
- `memory/STATUS.md`; `memory/2026-10-05.md`.

## Constraints & decisions

- Branch `light-harness-upgrade`, cut from an up-to-date `main` per `.claude/bindings/vc-git.md` `## vc`. Never upgrade on `main`.
- `upgrade` does not check in; the light lane's own ceremony commits it.
- Out of scope: cleaning `.claude/settings.local.json`; the human's global `~/.claude/settings.json` rm edit (F14), which harness-hub applies after this upgrade.
- A Claude Code restart may be needed for the new hooks/settings to load; say so in the report.

## Next steps

1. Declare the lane (light, class slim), cut `light-harness-upgrade`.
2. Invoke `upgrade` for this project; its HARD STOP shows the dry run whole to the human.
3. After apply: review and close via `g2-lite`.

## Open questions

- Whether the class stays slim once the dry run shows the diff size (`harness.py bounds`).

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - light-harness-upgrade; lane: light, class slim; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps. BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer.
