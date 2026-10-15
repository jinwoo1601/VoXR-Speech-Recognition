You're picking up: Phase 1 (the harness dump) of `feat-joined-rig-parser`. Read the files below, then start at Next steps.

## Objective

Pick up and build Phase 1 of `feat-joined-rig-parser`: `phase-pickup` persists and validates `Planning~/features/joined-rig-parser/plan-1.md`, then `implement` builds it through its verification gate. Done = plan-1 persisted, validated and linked from `## Build plan`; the `--dump` writer in `NativeBridge~/harness/main.cpp` built and gated green, as the architecture doc's Phase 1 row says.

## Lane

feature

## Context

- Design `joined-rig` (G1 locked 2026-10-13, A1 adr-0006), backlog row 1, Full track. Branch `feat-joined-rig-parser`, the one checkout (`trees` none), cut from `main` at `02453b7`; pushed to `origin`.
- Commits on the branch: `94b04f1` G0 scope; `baac3cd` requirements §1–§10; `1eb1613` architecture doc and the four-phase plan.
- `memory/STATUS.md` line: stage architecture, next `phase-pickup` Phase 1. Deferral JRP-1 added (partial timing → `joined-rig-recogniser`).
- Rulings this session (all the maintainer's, in session, 2026-10-14):
  - G0: scope as row 1; the tuning pass In (requirements §2 In 8), per design §7.
  - Requirements forks: a silent negative is "correct (no fire)", no sixth class; `Runtime/Testing/VoxrBatchTestRunner.cs` untouched; F10's tuning record lists every number the rig uses, or "none".
  - Architecture: the four phases approved as stated; partial timing ruled "(a) Bracket now"; both readings accepted (F1's dump comes from Command 3's build re-run with `--dump`, stdout diffed; recipe :232 reworded to "`stage.sh`'s path handling is not edited").

## Current state

- No plan file and no code yet. Phase 1 is the first build step.
- Phase 1 row (architecture `## Build plan`): the writer and `--dump`. Green: Command 3 verbatim (both halves; 16/16, exit 0; no `.so`); the built harness re-run from `NativeBridge~` with Command 3's arguments plus `--dump ../.scratch/joined/p1.jsonl`, stdout identical, exit 0; the dump read for header, 16 framed cases, partials, counters on every record, trailer; a second dump compared. Discharges F1, §10 item 1. Every phase also runs the binding's Commands 1–2.
- Runtime claims Phase 1 must prove at its first run (architecture `## Risks`): partials are `{"partial": "…"}`; Vosk word times restart at zero after `vosk_recognizer_reset` (`vosk_bridge.cpp` :307) — record which; two dumps of one build hold the same partial sequence; the dump moves no verdict.

## Key files & locations

- Architecture doc (the how; `## Decision register`, `## Data contract & runtime flow` dump records, `## Build plan`): `Planning~/features/joined-rig-parser/architecture.md`
- Requirements (F1, §9): `Planning~/features/joined-rig-parser/requirements.md`
- Code recon (trusted; harness flow, bridge results, verdict): `.scratch/code-mapper-joined-rig-parser-recon-report.md`
- The site: `NativeBridge~/harness/main.cpp` (`DrainResults` :41-61, `PushAll` :66-92, CLI :105-112, usage :111, case loop :209-222, verdict :227, report :244-264); `NativeBridge~/harness/README.md`; `NativeBridge~/harness/expectations.json` (the pin — never hand-edited).
- Verification: `.claude/bindings/unity.md` `## verification` Command 3 (:8), Commands 1–2; `Planning~/verification-recipe.md`.
- Precedent plan files: `Planning~/features/windows-native-dotnet-fixtures/plan-1.md`, `plan-2.md`.

## Constraints & decisions

- Harness only: no `NativeBridge~/src/**` change (G0 In 1; partial timing ruled bracket). Command 3's bound line stays byte-identical; `--dump` is opt-in, and without it nothing changes; a transcript mismatch stops for the human, never a re-pin.
- Plan-file budget 10 KB (`process-docs`); architecture doc body 40 KB, now 30.5 KB — keep headroom for the as-built pass.
- Lessons (`memory/notes-for-future-features.md`): subagent downloads are refused — any download is a human `!` step (only if `NativeBridge~/vendor/` is missing); agent `git` writes are refused by vc-guard — every revert or restore is a PM step between `compile-check` dispatches; Unity runs generate `memory/**/*.meta`, which are committed, never deleted; MSBuild through `cmake --build` has no reliable summary line — gates grep for warning/error counts.
- Nothing new names WSL or `/mnt/`.
- Still held by the human: `release`, `lab-next-level-probes`.

## Next steps

1. Invoke `phase-pickup` for Phase 1 of `joined-rig-parser`.
2. Then `implement` Phase 1 once its plan is persisted and validated.

## Reporting

Report to the hub session `voxr-hub` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - feat-joined-rig-parser; lane: feature; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps followed by "; predecessor 27485cb0-4466-41a5-9db4-f18e2170ff72". BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer. (Two sessions are named `voxr-hub`; send to the one on this machine. If the bare name is refused as ambiguous, use the ref `ListAgents` shows for the local one.)
