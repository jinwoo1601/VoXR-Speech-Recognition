# handoff — the launch

How `handoff` launches the next session and what the brief's Reporting section carries, where the `session-launch` binding reads filled; "the contract" is `.claude/references/core/delegation-contract.md`.

## The launch

After the brief is written, this session launches the next one where all four conditions below hold; where any fails, nothing is launched, and `## What you show me` gives the paste line. The target is the tree the next session opens in, as the brief's Context names it: where `trees` is filled, the branch's own tree by its path per the List read, or the hub; where it reads absent, the project root.

1. **A handoff point** — the human invoked this skill or asked for this handoff, or ordered the branch opened for which `g0-open` (for one item or a set) or `tackle-issue` invokes this skill, or this session reached a handoff point of `## When to hand off` and invoked this skill itself; and the human did not say they will open the session themselves. The Launch command's permission prompt is the human's check: nothing is asked before it.
2. **The slots** — the `session-launch` binding reads filled with its Launch command, Hub session name, List command and Confirm window, as the contract reads it, and the `verification` binding carries its *Path form*; one missing or `none` is named with the paste line.
3. **Not the hub** — where `trees` is filled, the target is not the hub: a second session there would be a second writer in the hub's one tree.
4. **A plain-ASCII prompt** — the paste line's text, the file's absolute path written in, is plain ASCII.

The steps, in order:

1. **BEFORE** — the List command. A session is at the target when its working directory, as the list shows it, is the target's path or lies under it, compared as the trees contract (`.claude/references/core/trees-contract.md`) compares paths. Where the target is not this session's project root and a session is at it → no launch: one line saying that tree has a session running, then the paste line.
2. **RUN** — the Launch command, its `<tree>` the target's path in the `verification` binding's *Path form*, its `<branch>` the branch the next session works on, and its `<prompt>` the paste line's text; then the shell returns to this session's project root.
3. **CONFIRM** — the List command, re-read until a session at the target appears that BEFORE did not show, within the `session-launch` binding's *Confirm window*. It appears → one line: the branch launched, and its tree. The Launch command errored, or the window passed → one line naming which and telling the human to check the session list before opening a session there, then the paste line; the tree is kept.
4. **LAST ACT** — where the target is this session's project root — its own tree, or the project root where `trees` reads absent: the launch, confirmed or not, ends this session's work, and it takes no further step, so one writer per tree holds. Where CONFIRM saw the new session and the list shows an `<id>` for it, its last act is one line to the hub by `SendMessage`, sent after `## What you show me`: `HANDOFF - <branch>; lane: <lane>; successor <id>`, `<branch>` this session's branch and `<lane>` the Lane of the brief this session has just written, and nothing after it; the hub then stops this session, as `close-tree` `## A handed-off session` says. Otherwise this session stays listed until the human closes it or the tree's close stops every session there.

## Reporting

The text below, written into the brief in plain ASCII, with `<hub session name>` the Hub session name, and `<branch>` and `<lane>` the next session's branch and the brief's Lane, written out; `<EVENT>`, `<detail>` and `<commit>` stay as they stand.

```
Report to the hub session `<hub session name>` by SendMessage, one plain-ASCII line per event, in the form `<EVENT> - <branch>; lane: <lane>; <detail>`. STARTED: your first act, before anything else; detail the skill named in Next steps. BLOCKED: whenever you stop on something only the human or the hub can clear; detail what blocks you. FINISHED: at once when the human declines this item at its G0 scope, detail `declined at G0, nothing written`; otherwise after the merge and close-out, detail `merged <commit>` - g1-lock, g2-accept and g2-lite send it at their REMOVE step, and where your closing skill has none, send it yourself after the close-out. The hub never replies and never gives you work: do not wait for an answer.
```
