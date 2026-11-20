---
name: add-pack
description: "Grow an existing project by one pack from the catalog, on the human's agreement. Use on 'add a pack', 'add <pack> to this project', 'compose in <pack>', or when a project needs a lane it does not yet compose."
disable-model-invocation: true
kind: orchestration
agents: [doc-writer]
stops: [the pack and its binding answers agreed before ADD, the ruling where the recompose dry run writes more than replaced skills or moves a pack version, the applied recompose differing from its dry run]
status: active
bindings: []
removal-date: null
---

# add-pack

## What this is

**A project grows by one pack at a time, and only the manifest grows.** This skill runs inside an existing project: the human chooses a pack and agrees the answers its binding keys need, `harness.py add` composes it in, `doc-writer` writes the new pack's bindings file from those answers, and `validate` confirms the project is green.

> Agree first, add second, interview third, validate last. Nothing else in the project changes, save the composed skills that splice the keys it fills or re-points.

## Bindings

- No binding of the project is consulted: the pack's binding keys come from the catalog, and their answers from the human.
- Agents — `doc-writer` missing → stop before ADD, the first write, and say so.

## Workflow

Every command is written `harness.py <verb> …` and runs as the base's `scripts/harness.py`. Walk up from the working directory one directory at a time. A directory holding `.claude/harness.json` is the project. Otherwise, a directory that is a version-control repository's root — it holds the repository's metadata entry — is a repository root without a manifest: it is not a composed project, never inside an ancestor project, and the walk stops there. Otherwise, go up. Its manifest's `base`, resolved against the project, is the base. Where the walk finds no project — a repository root without a manifest, or no manifest up to the filesystem root — this is not a composed project: stop before CHOOSE and point to `new-project`. `add`, `upgrade` and `validate` read the base from the manifest and need only `--root`.

1. **CHOOSE** — the pack, named by the human or matched from the catalog `harness.py list --json` returns. An unknown name is answered with the catalog, never guessed.
2. **STOP** — the human agrees the pack **and the answers for its binding keys**, which the catalog's `bindings` field names, before anything is written.
3. **ADD** — `harness.py add <pack> --root <project>`. A refusal, `add: refused — <reason>` on stderr at exit 1 with nothing written, is reported to the human with its reason. On success, read:
   - `  added: <pack> <version>`;
   - any `  adopted: <destination>` — a file already sitting at that destination was byte-identical to what `add` would have written there — the pack's file, or, for a skill file that reads binding keys, its composed bytes with the binding blocks filled in; a copy of the uncomposed source is refused as an overwrite — so `add` wrote nothing there, kept the local bytes and entered it in the ledger; the file is a pack copy now, not a local item;
   - any `  re-pointed: <key> from core to <pack>; the ## <key> section of .claude/bindings/core.md is now inert` — tell the human that the project's existing answer for that key now has to live in the new pack's file;
   - the `  bindings to fill:` line, `.claude/bindings/<pack>.md (<keys>), …` or `none`;
   - the tail `add: wrote N files into <project>`.
4. **BINDING INTERVIEW** — ask only what step 2's agreement left unanswered. Then file `.scratch/doc-writer-<slug>-brief.md` from `.claude/references/core/decision-brief.md`, carrying the human's answers as the decision record and `.claude/bindings/<pack>.md` as its one target, and dispatch `doc-writer` through the Agent tool without a `name`. One brief per bindings file; the dispatches run serially.
5. **VALIDATE** — where step 3 printed a `re-pointed:` line, or step 4 filled a section a composed file under `.claude/skills/` splices, those files read `V23` until recomposed: run `harness.py upgrade --recompose-only --root <project>`, one command alone in its call, its Bash call given 600000 ms, step 2's agreement covering a recompose within the bound, `harness.py upgrade --help` stating the bound, and the table below this step reads its first line; otherwise `harness.py validate --root <project>`. Green is the last line `validate: PASS` at exit 0, with no `V23` row. A failure is fixed by a further `doc-writer` brief, recomposed as above, or recorded as still TODO.

   | `upgrade --recompose-only` prints | The act |
   |---|---|
   | `recomposed: <n> files; validate PASS`, then the dry run and `validate`'s report | green where that report holds no `V23` row, else a failure |
   | `outside: <n> lines; nothing written`, those lines, then the dry run | stop, and put it to the human by `.claude/references/core/ruling-form.md`, the lines outside the bound shown — a `->` on the `packs:` line means the project is behind its base, which is the `upgrade` skill's run; where the display is cut, the human runs `harness.py upgrade --dry-run --root <project>` themselves |
   | `differs: <n> lines`, then the dry run, the lines and the applied report | stop, and put it to the human by `.claude/references/core/ruling-form.md`: the applied recompose differs from its dry run |
   | `failed: exit <code>`, then the dry run and the output | stop, show its output whole, and put it to the human by `.claude/references/core/ruling-form.md` |
   | `invalid: validate FAIL`, then the dry run and `validate`'s report | a failure |
   | exit 1, `upgrade: refused` on stderr, or any other output | stop, and put it to the human by `.claude/references/core/ruling-form.md`, the refusal shown |

The choice and the answers are the human's; the bindings file is `doc-writer`'s; the commands are the PM's.
