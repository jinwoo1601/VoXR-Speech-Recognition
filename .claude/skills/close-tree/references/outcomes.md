# close-tree — outcomes

The outcome table and the line a close that ends short of `removed` tells the human.

| Outcome | The tree | Its sessions | The human hears |
|---|---|---|---|
| not merged | kept | untouched | R3's line |
| a session at the tree without an `<id>` | kept | untouched | R3's line |
| a session still listed after the Confirm window | kept | the stop sent | R3's line |
| held — a holder probe read held or unreadable, or READ BACK found the Remove procedure's held result | kept | stopped, conversations kept | the held line |
| not clean | kept | stopped, conversations kept | R3's line |
| the removal refused — still listed | kept | stopped | R3's line, with the procedure's message |
| half-removed — unlisted, its directory present | unregistered; its directory left, empty or still holding files, as the Remove procedure leaves it | stopped | R3's line naming the directory; `orient` names it as a leftover until the human removes it |
| refused — a step's tool call refused by permission or policy | kept, or as the last read showed it where REMOVE or READ BACK was refused | as the steps before the refusal left them | the refused line |
| removed — unlisted, its directory gone | gone; the branch kept | stopped | nothing |

R3's line is `<branch>: tree kept at <path> - <the check> failed: <what the read showed>`, one plain-ASCII line to the human: `<path>` the tree's path; `<the check>` the step that failed — MERGED, STOPPED, CLEAN, RELEASE or REMOVE; and `<what the read showed>` the read's own words — for MERGED, the branches read and the commits each listed, the sessions without an `<id>` or still listed, the Clean read's lines, the procedure's message, or the directory left in place; where STOPPED or RELEASE could not run, the binding or slot missing.

The held line is one plain-ASCII line to the human, in one of two forms, each opening as R3's line does. Held: `<branch>: tree kept at <path> - held by <source>: <what the read showed>; to remove it, save and close the holder, then say to close the tree again` — `<source>` the pack whose probe read held, or `REMOVE` where READ BACK found the Remove procedure's held result; `<what the read showed>` the holder as that pack's reference names it from the probe's output, or the procedure's own message. Unreadable: `<branch>: tree kept at <path> - <pack> holder probe unreadable: exit <code>: <output>; to remove it, close any holder of the tree, then say to close the tree again` — `<pack>` the pack whose probe could not be read, `<code>` and `<output>` its exit code and raw output, the output's line breaks folded to spaces, and both `none` where no command could be read.

The refused line is one line to the human, its template plain ASCII: `<branch>: tree at <path> <state> - <step> refused by permission or policy: <the refusal message>; nothing was retried; to close it, clear the refusal, then say to close the tree again` — `<path>` the tree's path as TREE read it, or `unread` where TREE's own read was refused; `<step>` the step whose tool call was refused, TREE to READ BACK; `<state>` `kept` where the refusal came before REMOVE, and `listed before REMOVE, unread since` where REMOVE or READ BACK was refused; `<the refusal message>` the refusal's own words, its line breaks folded to spaces.
