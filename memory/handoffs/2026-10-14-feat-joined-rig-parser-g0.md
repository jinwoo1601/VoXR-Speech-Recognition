You're picking up: open row 1 `joined-rig-parser` of the locked `joined-rig` design at G0. Read the files below, then start at Next steps.

## Objective

Agree the G0 scope of feature `joined-rig-parser` with the human and cut `feat-joined-rig-parser` from an up-to-date `main`, via `g0-open`. Done = the human's agreed scope recorded and the branch cut (or the human declines/holds it). This next task is inferred from STATUS's headline; the human may redirect.

## Lane

feature

## Context

- Design `joined-rig` G1 LOCKED 2026-10-13 and re-locked with Amendment A1 (adr-0006, host-neutral runner); merged in PR #173. Backlog `Planning~/design-docs/joined-rig.md` `## 6. Feature backlog` (row at :162), quoted: "`joined-rig-parser` | Phase 1 (adr-0002). The harness's additive result dump (adr-0001); the rig's dump reader, the outcome-class scorer with slot credit (adr-0003), the joined report, and the pinned-side kill check (rule 2, adr-0005); the sealed and recogniser-layer manifest marks and their handling, the split fixture tagged (adr-0005); the report stating the one side varied (adr-0005). **Full.** | None."
- It was held by the human on 2026-10-13 behind the WSL removal ("WSL removal first"). That removal is done: `windows-native-toolchain` rows 1-2 accepted at G2 2026-10-14 and merged (#175 `d07de12`, #176 `990c4ef`; close-out `6c3184c`). Confirm with the human at G0 that the hold is lifted.
- `main` is the dev branch; one checkout (`trees` none); no board: `memory/STATUS.md` is the state. This is R1 of the plan of action (Phase C instrument; gates Phase D).
- Still held by the human: `release`, `lab-next-level-probes`. Do not pick them up.

## Current state

- No branch and no feature docs for `joined-rig-parser` yet.
- The design's §7 carries a host-choice item owned by this feature's architecture doc; adr-0006 settles the runner as host-neutral.
- Open deferrals in `memory/STATUS.md` `## Deferred` touching the toolchain: WND-1 to WND-4 (light-lane or relock items, not this feature's).

## Key files & locations

- Design and ADRs: `Planning~/design-docs/joined-rig.md`, `Planning~/design-docs/joined-rig/decisions/adr-0001…adr-0006`, recon record `Planning~/design-docs/joined-rig/recon-2026-10-13.md`.
- The harness the dump extends: `NativeBridge~/harness/` (`main.cpp`, `expectations.json`, `README.md`), bound as Command 3 in `.claude/bindings/unity.md` `## verification`.
- The parser A/B rig and its DocCheck: `Planning~/features/coverage-in-selection/ab-rig/`, run per `Planning~/verification-recipe.md` `## Parser A/B rig — DocCheck`.
- Precedent Full-track docs: `Planning~/features/windows-native-dotnet-fixtures/{requirements,architecture,plan-1,plan-2}.md`.

## Constraints & decisions

- G0 before any doc or code; the scope is the human's to agree.
- Lessons (`memory/notes-for-future-features.md`): subagent network downloads are refused, so name a human `!` step; agent `git` writes are refused by vc-guard, so plan every canary revert or restore as a PM step; Unity runs generate `memory/**/*.meta`, which are committed, never deleted as orphans; keep the architecture doc under its 40 KB body budget with headroom for the as-built pass.
- Nothing new names WSL or `/mnt/`.

## Next steps

1. Invoke `g0-open` for `joined-rig-parser` (from the locked backlog row above).

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-joined-rig-parser; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 869bfe38-77f4-4997-b3f8-e4ad23b98e8f". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; send to the one on this machine. If the bare name is refused as ambiguous, use the ref `ListAgents` shows for the local one.)
