# tree-holder — whether a Unity Editor holds a tree

The probe run on a tree before it is removed, to learn whether a Unity Editor holds the tree's project open: the one command, how its result reads, and what it misses.

## The probe

```
unity status --project-path "<tree>" --json
```

`<tree>` is the tree's path as the version-control binding's `trees` List read prints it, in double quotes. The probe carries no caller label: `unity status` exits 2 on one (`unity-cli-contract.md`, fact 1), which would read every tree unreadable. `--json` is what prints the error code `## Reading` reads; without it, `unity status` prints no code (`unity-cli-contract.md`, fact 9).

## Reading

Read by `errors[].code` with `data.instances`, never by exit alone: `AMBIGUOUS_EDITOR` and `STATUS_NO_INSTANCES` both exit 6 (`unity-cli-contract.md`, M1 and facts 6 and 9).

- Held — an instance listed in `data.instances` for the tree, in any state, or `AMBIGUOUS_EDITOR`; the holder is named as `data.instances` lists it. An Editor in any state holds the tree's `Library/` and `Temp/`, so its state is not read.
- Not held — `STATUS_NO_INSTANCES`.
- Unreadable — anything else.

## The miss

The miss is either of two Editors that `unity status` does not list, a batch Editor and an Editor without the Pipeline package; either reads not held. Its backstop is the Remove procedure's own mechanism: a Remove procedure that waits for the directory's release refuses with its held result and nothing removed, and one that does not half-removes, told, never retried.

## Where a project reaches it

A project composing this pack reaches this file at `.claude/references/unity/tree-holder.md`; the skill that closes a tree reads `## The probe` and `## Reading` there, and `## The miss` is for the human and the reviewer.
