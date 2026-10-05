You're picking up: open feature `feat-resolver-slot-exemption` at G0 and write its requirements doc. Read the files below, then start at Next steps.

## Objective

Take `feat-resolver-slot-exemption` — the one row of the locked `resolver-slot-scoring` backlog — through G0 (scope agreed with the human, user story, branch cut), then author its requirements doc. Done = branch `feat-resolver-slot-exemption` exists, `Planning~/features/resolver-slot-exemption/requirements.md` §1-§2 recorded at G0, and the requirements doc drafted via `feature-requirement-doc`.

## Lane

feature

## Context

- Repo `D:\Workspace\VoXR-Speech-Recognition` (Unity package `com.jinwoo1601.voxr`), one checkout (`trees` is none), on `main` at `4c7ebb6` (PR #167, the `design-resolver-slot-scoring` G1 lock, merged as `42ef0bd`). `memory/` lives here.
- Issue #161 (ellipsis vs the score gate) — design locked at G1 2026-10-02; #161 closes when this feature ships.
- The backlog row, verbatim from `Planning~/design-docs/resolver-slot-scoring.md` §7: "`feat-resolver-slot-exemption` | Build DR-8 and DR-9 (`scoring-model.md` §0E) in the parser: the exemption and the fewer-exempted-slots selection key, the resolver registry read at parse time, and every scoring site agreeing with it (flush, eager, pending follow-up re-score, Editor runner-up comparison, the batch test runner given the registered set — M5); tests; the two DocCheck pin moves (§7 A start 2 0.167 → 0.400; `fire at` 0.333 → 1.000). After G2: `scoring.md`, `command-recognition.md` authoring guidance, a `KNOWN_LIMITATIONS.md` entry for thin firing and the two-command rows, and `CHANGELOG.md`. **Full.**"
- The model in one line: a missed required slot whose name has a registered resolver contributes 0 raw / 0 den to the score; it still counts as missed for DR-7 admission, the start probe, the leading-miss latch and the eager gate (arithmetic only — lab arm A4L); selection keys become earliest start → score → fewer exempted slots → consumed span → literal count → registration order.
- Track **Full**: the design leaves open how the parser sees the registry (today the parser has no registry reference and `RegisterSlotResolver` deliberately triggers no rebuild, `VoxrCommandRecogniser.cs` ~439-443) and how each scoring tool gets the same registered set (M5). Those are requirements/architecture choices.

## Key files & locations

- `Planning~/design-docs/resolver-slot-scoring.md` — locked design (§1 model M1-M6, §3 dynamics, §5 boundaries, §7 backlog, §8 owners).
- `Planning~/design-docs/resolver-slot-scoring/decisions/adr-0001..0005-*.md` — the five rulings.
- `Planning~/design-docs/resolver-slot-scoring/lab-2026-10-02.md` — measured A0 vs A4L numbers and the exact `a4l.patch`.
- `Planning~/design-docs/scoring-model.md` §0E — Amendment A5, DR-8, DR-9 (locked).
- `Planning~/features/slot-resolver/requirements.md` and `architecture.md` — the resolver (#148) as built; F19 and the "Resolution is not evidence" bullet carry A5 amendments.
- `Runtime/Commands/VoxrCommandParser.cs` — `TryMatchScored` (~3617; slot miss ~3704), `CompareCandidate` (~3457), `ParseInternal`, `TryEagerCommit` (~4593); `Runtime/Commands/VoxrCommandRecogniser.cs` — `RegisterSlotResolver` (~431), Step 3b resolver pass (~923-994); `Runtime/Commands/DynamicSlotManager.cs` — the registry; `Runtime/Testing/VoxrBatchTestRunner.cs` — scores without a registry today.
- DocCheck rig: `Planning~/features/coverage-in-selection/ab-rig/DocCheck.cs`.
- Feature docs go at `Planning~/features/<feature>/requirements.md` and `architecture.md` (`.claude/bindings/core.md` `## process-docs`).

## Constraints & decisions

- The locked design and A5 are immutable; a contradiction found while building stops and reopens them on a design branch (constitution).
- The branch is cut from an up-to-date `main` per `.claude/bindings/vc-git.md` `## vc`.
- No product docs until G2.
- Untracked `.claude/csharpier/` (a CSharpier hook artefact) is in the working tree — never stage it.

## Next steps

1. Invoke `g0-open` for backlog feature `feat-resolver-slot-exemption` (track Full, from the row above). Its SCOPE stop puts the scope and user story to the human.
2. After G0, invoke `feature-requirement-doc` for the requirements doc.

## Open questions

- The user story is the human's to state at G0.
- Whether the batch test runner and Editor tools take the registered set by constructor, by reference, or otherwise — a requirements/architecture question, not settled by the design.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-resolver-slot-exemption; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps. BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer.
