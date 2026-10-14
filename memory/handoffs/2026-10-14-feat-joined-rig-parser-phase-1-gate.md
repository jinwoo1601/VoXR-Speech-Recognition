You're picking up: the close of `implement` Phase 1 of `feat-joined-rig-parser`, at its GATE step. Read the files below, then start at Next steps.

## Objective

Finish `implement` Phase 1 (the harness dump). First get a ruling on gate A's two recorded runtime claims, which contradict the architecture doc's text. Then fix that text, run gate B (plan criteria 1 and 8), file the reports, report, and commit. Done means:
- `main.cpp` is committed on `feat-joined-rig-parser`;
- the architecture text matches what the run showed;
- the plan's criteria 1–8 are green with evidence.

## Lane

feature

## Context

- Feature `joined-rig-parser`, Full track. Design `joined-rig` was G1-locked on 2026-10-13. Branch `feat-joined-rig-parser` is the one checkout (`trees` none). Pushed to `origin` at `1cfd09c`.
- `1cfd09c` persisted and validated the Phase 1 plan: `Planning~/features/joined-rig-parser/plan-1.md`, linked from the architecture doc's `## Build plan`, validated at narrow depth, five findings fixed. `memory/STATUS.md` says: stage implement, next `implement` Phase 1.
- This session ran `phase-pickup` in full and `implement` through BRIEF, DISPATCH, RECEIPT and the first half of GATE. It stopped at the context bound of about 200k.

## Current state

- **Step 0 (pre-edit baseline), green.** `.scratch/joined/p0-before.stdout` holds 3,806 bytes: the harness JSON report, CRLF, 16/16. Report: `.scratch/compile-check-joined-rig-parser-phase-1-step0-report.md`.
- **Code, DONE.** `NativeBridge~/harness/main.cpp` has +108/−2 lines, uncommitted, and is the only file modified. Report: `.scratch/implement-joined-rig-parser-phase-1-report.md`.
- **Gate A, DONE: plan criteria 2–7, all PASS.** Report: `.scratch/compile-check-joined-rig-parser-phase-1-gate-a-report.md`.
  - Command 3 ran on both halves: 16/16, exit 0, empty symbol diff. The `.so` was not rewritten, so no restore is needed.
  - Stdout is byte-identical to `p0-before.stdout` with and without `--dump`.
  - The `p1.jsonl` check passed every clause: 16 cases, 123 results, 91 partials, 32 finals. Partials are `{"partial":…}` only.
  - A second dump gave equal final texts and equal partial sequences.
  - Criterion 7 exited 2 and named the path.
- **The finding to rule on (runtime claims g and h).**
  - Vosk word times do **not** restart at zero after `vosk_recognizer_reset`. They run on across cases: the first case's first word is at 0.36 s, the last case's at 60.86 s.
  - The run-on origin differs between two runs of one build. Finals' `result[]` arrays differ in 14 of 16 cases, by a constant shift per case. For example, `cease_fire` `cease` starts at 5.0486 s in `p1` and 5.134 s in `p1b`.
  - So a final's word times are consistent within a case, but the case's absolute origin is run-dependent and is not recoverable exactly from the dump.
  - This contradicts the architecture doc in two places:
    - `## Decision register` *Audio time* says "exact word times on non-empty finals".
    - `## Risks` says "word times restart at zero after `vosk_recognizer_reset` … else phase 2 offsets per case — Phase 1 records which".
  - Both are tier 2 (architecture), not a locked doc. Requirements F1 ("words with `conf` and timings") still holds.
  - Plan rule (`plan-1.md` stops): "a false runtime claim (PM-ruled; the text fixed before the phase closes)".
- **Gate B not run:** criterion 1 (Commands 1–2) and criterion 8 (the diff-stat and opt-in read).

## Key files & locations

- Plan: `Planning~/features/joined-rig-parser/plan-1.md`: `## Acceptance criteria` 1–8 and `## Runtime claims`.
- Architecture: `Planning~/features/joined-rig-parser/architecture.md`, the sections `## Decision register` *Audio time*, `## Risks, tradeoffs & open questions` and `## As-built deltas`. It is 30.8 KB against a 40 KB budget.
- Briefs, already filed and reusable as precedent:
  - `.scratch/implement-joined-rig-parser-phase-1-brief.md`
  - `.scratch/compile-check-joined-rig-parser-phase-1-gate-a-brief.md`
  - `.scratch/compile-check-joined-rig-parser-phase-1-step0-brief.md`
- Gate artefacts: `.scratch/joined/` (`p1.jsonl`, `p1b.jsonl`, `check_p1.py`, the stdout and stderr captures).
- Verification: `.claude/bindings/unity.md` `## verification`, Commands 1–2 (line 6 onward); the procedure is in `Planning~/verification-recipe.md`.
- Bridge site behind the finding: `NativeBridge~/src/vosk_bridge.cpp` :296-318 (`vosk_bridge_reset`) and :307 (`vosk_recognizer_reset`). The ring and the stop join drop a scheduler-dependent tail.

## Constraints & decisions

- Harness only: no `NativeBridge~/src/**` change. Exact word or partial timing is a bridge change, owned by `joined-rig-recogniser` (deferral JRP-1 is the precedent). `expectations.json` is never re-pinned.
- The PM writes only `memory/`, CHANGELOG lines and `.scratch/` briefs. Architecture-text fixes go to `architect-reconcile` (a record pass) from a brief.
- Agents never run `git` writes (vc-guard); restores are PM steps.
- Some reports are not filed by the hook (read-only agents). File them from `~/.claude/projects/D--Workspace-VoXR-Speech-Recognition/<session>/subagents/agent-<id>.jsonl` without printing them; the extractor script pattern is a `python3 -I` walk for the report's first line.
- Commands 1–2 need the host's Editor closed (`D:\Workspace\VoXR TestGround\Temp\UnityLockfile` absent) and the `Tests~`→`Tests` rename and its revert. Unity runs generate `memory/**/*.meta`, which are committed, never deleted.
- Commit set (plan): `NativeBridge~/harness/main.cpp`, the architecture doc if it changes, and `memory/**/*.meta` if any. Nothing under `.scratch/joined/`. No CHANGELOG line (dev tooling).

## Next steps

1. Rule on claims g/h with the human. Recommended: record the finding as-built. The ruling decides three things:
   - the doc's *Audio time* entry says word times are exact relative to the decoder stream, with a run-dependent per-case origin, so they are usable within a case;
   - the "restart, else offset" sentence in `## Risks` is replaced by the measured "run on, origin not recoverable";
   - whether exact per-case timing joins JRP-1 as `joined-rig-recogniser`'s.
   Before presenting, check whether the parser layer (Phase 3, `Parse(text, words)`) reads word `start`/`end` at all. If it reads only `word`/`conf`, the finding does not touch this feature's scoring.
2. File `.scratch/architect-reconcile-joined-rig-parser-phase-1-ruling-brief.md` recording the ruling in the architecture doc, and dispatch `architect-reconcile`.
3. Resume `implement` Phase 1 at GATE: a `compile-check` gate-B dispatch for criterion 1 (Commands 1–2), then criterion 8. Then FILE and REPORT.
4. Commit per the `vc` binding's *Check-in procedure*, push, and set the STATUS line to stage implement, Phase 1 built; next `phase-pickup` Phase 2. Phase 2's pickup is a phase boundary, so hand off there.

## Open questions

- The ruling above (claims g/h). The Recommended option is in Next steps 1.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-joined-rig-parser; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor edf2ee01-c1ff-4885-a001-b27f02973361". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; send to the one on this machine. If the bare name is refused as ambiguous, use the ref `ListAgents` shows for the local one.)
