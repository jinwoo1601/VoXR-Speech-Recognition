# Path read

Skills cite this file by path and section heading rather than restate it; agents never run the read.

## The read

The base's `scripts/harness.py doctor --paths`, the base being the `base` path in `.claude/harness.json`: `--feature <feature> --root <project>` reads that feature's backlog row on this branch; `--name <path> --root <project>` reads a path a STATUS line names. It writes nothing and exits 0 whatever it finds.

It prints `path:`, `source:`, `items: <numbers> (declared|default)`, `depth: <profile> (declared|default)`, one `stage: <name> <skill or ->` per stage, then `skills: <the composed paths' stage skills, or ->`. On each `source:`:

- `row`, `named`: the lines hold.
- `default`: no row names a path: its stages, all six items a path may pick, `full`.
- `none`: no path applies: the same, no stages.
- `uncomposed`, `unreadable — <reason>`: the same; the skill says so and goes on.
- No `path:` line, as where no manifest names a base: as `unreadable`.

## The word

Where the feature form's `items:` holds fewer than six, `g2-accept` PREFLIGHT's brief to `gate-preflight` carries `Preflight items: <numbers>, picked by path <name>`; otherwise no line. No line: all seven. One out of form: all seven, saying so. The agent reports each item the line does not name, save items 6 and 7, `SKIPPED BY PATH <name>`; item 6 unnamed instead runs `budget` without `--feature`, its verdict the STATUS budget's, and says the feature's own doc budget, lite sum included, is skipped by path.

## The STATUS line

A feature's line names its path and stage in its head, the text before its first ` - `: ``- **`<branch>`**: feature; full; path development; stage architecture - <detail>``. Fields part at `;`; one starting `path ` holds the path, `stage ` the stage; others are ignored, and all detail. An unindented `## Current` line opening so names a path where its head holds one `path` field, not `path none`, and a stage where it holds one `stage` field; any other reads as it always did, by form, not date.

**Whose line.** Only a line whose lane, as the site writes it, is `feature`, where the feature form, run first, prints a `path:` other than `none`, or is `prototype`. A `prototype` line, the second lane's, has no row but its own form, ``- **`<branch>`**: prototype; path prototype; stage prototype - <detail>``, read by `--name` alone. A `design`, `light` or `lab` line, a re-lock's included, has no row and no form, whatever the read prints; nor has an entry landed before the branch's G0, no path being agreed.

**Which stage.** The one running or, between stages, the next, which the line's next action starts. Where the read prints `stage:` lines, one of those names, whatever word the site uses: at G0 the first or, on a lite feature, which runs no requirements stage, the first after `requirements`. Otherwise free.

**Who moves it.** Whoever writes the line: the citing sites, and the act 6 duty, `.claude/references/core/trees-contract.md` `## Who does what`, at every stage change. A later write is a new line and carries the form; a line nobody writes stays as it is. Nothing writes `path none`.
