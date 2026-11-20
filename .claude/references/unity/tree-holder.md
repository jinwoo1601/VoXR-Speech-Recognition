# tree-holder — whether a Unity Editor holds a tree

The probe run on a tree before it is removed, to learn whether a Unity Editor holds the tree's project open: the one command, how its result reads, and what it misses.

## The probe

```
unity status --project-path "<tree>" --json
```

`<tree>` is the tree's path as the version-control binding's `trees` List read prints it, taken by `unity-cli-contract.md` M1's form rule for a `unity status` call, `<root>` there that path, any conversion run from the same root just before the probe. `unity status` matches its value raw and separator-sensitive (`unity-cli-contract.md`, fact 11), so a path in another form lists no Editor, and a held tree would read not held. A conversion that fails or prints no path, or a `verification` binding that is absent, a single `none`, or whose `Path form` names neither, is unreadable. The probe carries no caller label: `unity status` exits 2 on one (`unity-cli-contract.md`, fact 1), which would read every tree unreadable. `--json` is what prints the error code `## Reading` reads; without it, `unity status` prints no code (`unity-cli-contract.md`, fact 9).

## Reading

Read by `errors[].code` with `data.instances`, never by exit alone: `AMBIGUOUS_EDITOR` and `STATUS_NO_INSTANCES` both exit 6 (`unity-cli-contract.md`, M1 and facts 6 and 9).

- Held — an instance listed in `data.instances` for the tree, in any state, or `AMBIGUOUS_EDITOR`; the holder is named as `data.instances` lists it. An Editor in any state holds the tree's `Library/` and `Temp/`, so its state is not read.
- Not held — `STATUS_NO_INSTANCES`.
- Unreadable — anything else.

## The miss

The miss is either of two Editors that `unity status` does not list, a batch Editor and an Editor without the Pipeline package; either reads not held. Its backstop is the Remove procedure's own mechanism: a Remove procedure that waits for the directory's release refuses with its held result and nothing removed, and one that does not half-removes, told, never retried.

## Where a project reaches it

A project composing this pack reaches this file at `.claude/references/unity/tree-holder.md`; `close-tree` runs the script below on each tree it closes, `orient`'s direct removal reads `## The probe` and `## Reading` there, and `## The miss` is for the human and the reviewer.

The pack ships the probe as a script beside this file, `tree-holder.py`, which a project reaches at `.claude/references/unity/tree-holder.py` and runs as `py .claude/references/unity/tree-holder.py "<tree>"`, `<tree>` the tree's path as the List read prints it, drive-lettered. The script writes that path in the Editor's own form, backslash separators (`unity-cli-contract.md`, fact 11), reading a path that is not drive-absolute as unreadable; runs `## The probe`'s command with no caller label, under a time bound of its own; reads its output as `## Reading` does; and prints one line: `held: <each instance as data.instances lists it, or AMBIGUOUS_EDITOR>`, exit 2; `not-held: STATUS_NO_INSTANCES`, exit 0; or `unreadable: exit <code>: <output>`, the output's line breaks folded to spaces, `<code>` `none` where the command never ran or ended on the bound, exit 1. `## The probe` and `## Reading` stay the rule the script implements.
