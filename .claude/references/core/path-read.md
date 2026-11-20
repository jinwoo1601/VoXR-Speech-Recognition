# Path read

Skills cite this file by path and section heading rather than restate it; agents never run the read.

## The read

The base's `scripts/harness.py doctor --paths`, the base being the `base` path in `.claude/harness.json`: `--feature <feature> --root <project>` reads that feature's backlog row on this branch; `--name <path> --root <project>` reads a path a STATUS line names. It writes nothing and exits 0 whatever it finds.

It prints `path:`, `source:`, `items: <numbers> (declared|default)`, `depth: <profile> (declared|default)`, one `stage: <name> <skill or ->` per stage, `skills: <the composed paths' stage skills, or ->`, then `moves-up-to: <the path's declared up moves, comma-separated, or ->`. Only a name on `moves-up-to:` is an up move; it prints `-` where the path declares none or its list cannot be read, and on every `source:` but `row` and `named`. A path may also declare `computed` or `none`, printed `depth: computed (declared)` or `depth: none (declared)`: `computed` is the class `bounds` measures from the diff at the close, read by the light skills, never off this line; `none` is no review. Any other word naming no profile prints `depth: full (default)`. On each `source:`:

- `row`, `named`: the lines hold.
- `default`: no row names a path: its stages, all six items a path may pick, `full`.
- `none`: no path applies: the same, no stages.
- `uncomposed`, `unreadable — <reason>`: the same; the skill says so and goes on.
- No `path:` line, as where no manifest names a base: as `unreadable`.

## The word

Where the feature form's `items:` holds fewer than six, `g2-accept` PREFLIGHT's brief to `gate-preflight` carries `Preflight items: <numbers>, picked by path <name>`, and where the `--name light` form's does, `g2-lite` PREFLIGHT's brief on an effective slim carries it; otherwise no line. No line: all six. One out of form: all six, saying so. The agent reports each item the line does not name, save item 6, `SKIPPED BY PATH <name>`.

## The STATUS line

**The forms.** A line names its path and, where its form has one, its stage in its head, the text before its first ` - `, its lane word first, in its lane's form: a feature's ``- **`<branch>`**: feature; full; path development; stage architecture - <detail>``, a prototype's ``- **`<branch>`**: prototype; path prototype; stage prototype - <detail>``, a design topic's ``- **`<branch>`**: design; path design; stage design-doc - <detail>``, a re-lock's ``- **`<branch>`**: design; path design; stage design-doc; re-lock of <feature-branch> - <detail>``, a light branch's ``- **`<branch>`**: light; path light - <detail>`` and a lab branch's ``- **`<branch>`**: lab; path lab - <detail>``. Fields part at `;`; one starting `path ` holds the path, `stage ` the stage; others, such as a `class <c>` a site writes after the path's field, are ignored, and all detail.

**Whose line.** Every branch's line in `## Current`, whatever its lane, a re-lock's included, is written in its lane's form and read for its path. A `feature` line names the path the read's feature form, run first, prints, and holds no `path` field where that prints `none`; every other lane's line names its form's path, which no row holds, read by `--name` alone. An entry landed before the branch's G0 names no path, none being agreed. Where `trees` is filled, the line is read in the hub's `memory/STATUS.md`, by absolute path under the `trees` binding's *Hub* path, since every `memory/` write lands there (`.claude/references/core/trees-contract.md` `## The hub operation`); else in the project's own.

**The lane word.** It is the text after the line's `**: ` up to its first `;`, `,` or ` ·`, or the line's end. A line whose head holds no `path` field and whose lane word is `light` or `lab` names path `light` or `lab`, that word read as its alias, on a `;` head (`light; class prose`) and a `·` head (`light · class prose`, `light, class slim · …`) alike; the hub's own `·` lines name a path only so, a `path` field being read only as a `;`-part of the head. Any other lane word with no `path` field, `lightweight`, a hub `feature · …` line's, or one running on to ` - ` as in `light - next` among them, names no path, never guessed.

**One path field.** An unindented `## Current` line opening so names a path where its head holds exactly one `path` field, not `path none`, and a stage where it holds one `stage` field. A head holding more than one `path` field names no path, takes no alias and no reading by its branch's form, and so is neither light nor lab work wherever it is read; `doctor`'s STAGE row says it repeats the path field. A head whose one `path` field reads `path none` names no path either, and likewise takes no alias and no reading by its branch's form, so it too is neither light nor lab work wherever it is read. A line naming a path is read by that path whatever its branch's form: a site reading a lane from the form, `light-<topic>` or `lab-<topic>`, reads it only where the head holds no `path` field and no alias, so a `light-` branch whose line names `path development` is not light work. Any other line reads as it always did, by form, not date.

**Which stage.** The one running or, between stages, the next, which the line's next action starts. Where the read prints `stage:` lines, one of those names, whatever word the site uses: at G0 the first or, on a lite feature, which runs no requirements stage, the first after `requirements`. Otherwise free. A design topic's line and a re-lock's open at `stage design-doc`, the first stage the `design` path declares; a light or lab line carries no `stage` field, whatever the read prints.

**Who moves it.** Whoever writes the line: the citing sites, and the act 6 duty, `.claude/references/core/trees-contract.md` `## Who does what`, at every stage change. A later write is a new line and carries the form; a line nobody writes stays as it is. Nothing writes `path none`.

**The move.** A path changes only by a rewrite of the line, the branch keeping its name. A move from the path the line names, by its field or its alias, to a path named on the `moves-up-to:` line of that path's `--name` read (`## The read`) is up, read from the declaration and never judged: the PM states it and rewrites the line in the target's form, at the stage its next action starts, with no stop. Any other move, or one where that read prints `moves-up-to: -`, no `moves-up-to:` line or no `path:` line, takes the human's word, put to the human by `.claude/references/core/ruling-form.md`, before the line changes.
