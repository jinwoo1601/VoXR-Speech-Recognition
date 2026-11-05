# Phase 1 — persisted plan (2026-11-05)

Scope: Phase 1 as the row reads it — `doc-writer` rewrites `.claude/bindings/core.md` :22's `<tree>` note (*Launch note*) and :27's Release wait reason, the value `none` kept (*Release wait*), by the register's texts, then the PM reads both lines and runs `validate` and `doctor` against their rows at `bd6f1b3`, the baseline the PM captured at `ce46a45` (S0) — taking on and dropping nothing, and not taking the harness's "not cd-free…" note, which the register does not take.

## Files to change

| Path | Change | Why |
|---|---|---|
| `.claude/bindings/core.md` | modify (`doc-writer`, S1): :22 and :27 only, each replaced in place; 42 lines, CRLF 42/42 before and after | F4 (A4), *Launch note*; F5, *Release wait* |

## Contracts and interfaces

**The two lines, before and after.** Each is one whole line; every other byte of the file is unchanged, the Interview comment :21 included. On :22 only the `<tree>` note changes; the command span and the CLI reference stay verbatim.

- :22 before: ``- Launch command: `cd "<tree>" && CLAUDE_CODE_FORCE_SESSION_PERSISTENCE=1 claude --bg --remote-control <branch> -n <branch> --permission-mode auto "<prompt>"`, run in Git Bash from the launching session, `<tree>` the project root in the Path form (`trees` is none) — the CLI's behaviour: `.claude/references/core/claude-code-cli.md` ``
- :22 after: ``- Launch command: `cd "<tree>" && CLAUDE_CODE_FORCE_SESSION_PERSISTENCE=1 claude --bg --remote-control <branch> -n <branch> --permission-mode auto "<prompt>"`, run in Git Bash from the launching session, `<tree>` the branch's tree, `D:\Workspace\VoXR-Speech-Recognition.trees\<branch>` by the Location slot, in the Path form — the CLI's behaviour: `.claude/references/core/claude-code-cli.md` ``
- :27 before: ``- Release wait: none — `trees` is none, so no tree is removed ``
- :27 after: ``- Release wait: none — the vc-git Remove procedure (`.claude/references/vc-git/tree-remove.py`) waits for the directory's release itself ``

Nothing follows the CLI reference on :22: the harness's `; not cd-free, so no allow rule matches it whole: …` tail (harness repo `.claude/bindings/core.md` :41) is not added.

**Steps.**
- S0 PM, already done, before S1: at `ce46a45`, before any Phase 1 edit, the PM ran S3's two commands into `.scratch/trees-fill-baseline-validate.txt` and `.scratch/trees-fill-baseline-doctor.txt`, each exit 1. `git diff --name-only bd6f1b3..HEAD` (HEAD `ce46a45`) lists only `Planning~/features/trees-fill/architecture.md`, `Planning~/features/trees-fill/requirements.md` and `memory/STATUS.md` — no binding, no `.claude/` file — so these rows stand for `bd6f1b3`. The two files are S3's comparison ground. They already fail on rows not this phase's: V1/BEHIND, V11, V23 and DRIFT on 16 skill files, `handoff/SKILL.md` and `close-tree/SKILL.md` among them, and doctor warnings.
- S1 `doc-writer`: the two lines, by in-place replacement; nothing else; CRLF kept; runs nothing.
- S2 PM: reads :22 — the after text, no "`trees` is none" left in it (F4, A4); reads :27 — value `none`, the after reason (F5); `git diff -- .claude/bindings/core.md` touches :22 and :27 only; the CR count by a `py` byte count equals the line count, 42.
- S3 PM, from the project root in Git Bash: `py ../harness/scripts/harness.py validate --root "D:/Workspace/VoXR-Speech-Recognition"`, then `py ../harness/scripts/harness.py doctor --root "D:/Workspace/VoXR-Speech-Recognition"`; each output compared row by row with its S0 baseline file (`## Build plan`). Green: each exits 1, as at baseline, and no row in either output is new against its baseline file, other than a V23 row, or a change to its message, for `.claude/skills/handoff/SKILL.md` or `.claude/skills/close-tree/SKILL.md` (the copies at :58 and :59, which Phase 4 recomposes), and a matching DRIFT row for those two files only if the binding change brings one.
- No `verification` Command runs: a binding-only change fires neither the Command 3 trigger nor the Command 4 trigger (`.claude/bindings/unity.md` :8-9).

**Stops**, never worked around: S2's diff beyond the two lines, a CR count off, or a read not as the after text — stop with the evidence, no hand repair. S3 any other row new against the baseline, or an exit other than 1 — stop for the human; `.claude/skills/` is never edited by hand (Phase 4's).

## Trade-offs

| Choice | Alternative | Why |
|---|---|---|
| The register's after texts, verbatim | the harness's :41 with its "not cd-free…" tail | the register does not take it; the tail is the harness's own permission note |
| Release wait value `none` kept, reason replaced | a seconds value | `tree-remove.py` waits for the directory itself, so `close-tree` RELEASE waits for nothing (*Release wait*) |
| One `doc-writer` dispatch for both lines, serial | one per line, or a unit split | one file, two lines |
| Spliced copies left stale | hand-edit `handoff/SKILL.md:58` and `close-tree/SKILL.md:59` | adr-0005 :23 and the layout binding: `.claude/**` is generated, recomposed by `upgrade` in Phase 4 (*Composed copies*) |
| `doc-writer` executes | `code-writer` | binding prose is its work (`## Build plan`) |

## Acceptance criteria

1. [ ] :22 reads the after text, and no longer says `trees` is none (F4; A4).
2. [ ] :27 keeps the value `none`, its reason that the Remove procedure (`tree-remove.py`) waits for the directory's release itself (F5).
3. [ ] The `core.md` diff is :22 and :27 only; CRLF 42/42 (the row).
4. [ ] `validate` and `doctor` each exit 1, as at baseline, and show no row new against their S0 baseline files, other than a V23 row, or a change to its message, for `handoff/SKILL.md` or `close-tree/SKILL.md`, and a matching DRIFT row for those two only if the binding change brings one (§6; `## Build plan`).
5. [ ] No `verification` Command runs, and no agent commits (§6; CONSTITUTION).

## Open questions that block implementation

None.

## Blast radius

- `.claude/skills/handoff/SKILL.md` :58 and `.claude/skills/close-tree/SKILL.md` :59, the spliced copies: stale until Phase 4 (V23). Their readers — `handoff/SKILL.md` :6, `handoff/references/launch.md`, `close-tree/SKILL.md` :70 RELEASE — read the old text until then.
- The other seven files holding a `session-launch` block carry neither line: unaffected.
- From this phase to Phase 3, :22 names a Location slot and :27 a Remove procedure that `.claude/bindings/vc-git.md` `## trees`, still `none`, does not carry.
- No `harness.py` check reads either line's wording; it checks slot presence and `TODO` only.

## Runtime claims

- a (S1): the edit keeps `core.md` CRLF, 42 lines.
- b (S3): `harness.py`, run from the project root, resolves `../harness`; `validate` and `doctor` read neither line's wording, so the only rows new against the baseline the edit can bring are V23, or a changed V23 message, for the two copies — DRIFT for them only if the binding change brings one — and each still exits 1 on the baseline's own failures.
- c (S0): with no binding and no `.claude/` file changed in `bd6f1b3..ce46a45`, `validate` and `doctor` at `ce46a45` print the rows they would at `bd6f1b3`.

Hazards: ordering — from Phase 1 until Phase 3 fills `## trees`, the live :22 points `<tree>` at a branch tree no Create procedure cuts yet, and :27 names a Remove procedure not yet bound, so no session is launched by the live :22 in that window; from Phase 3 until Phase 4, the spliced copies still name the project root and `trees` as none. Runtime — doctor's BUDGET rows count this feature's architecture doc and plan files, so either crossing its budget before S3 brings a row new against the baseline, a stop.
