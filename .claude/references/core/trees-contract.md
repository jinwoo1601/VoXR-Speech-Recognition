# Trees contract

What a filled `trees` binding means — the hub, the comparison of paths, the hub clause and the hub operation — and the form of a report to the hub; whether `trees` or `session-launch` reads filled is `.claude/references/core/delegation-contract.md` `## Bindings`'s.

## The hub and the comparison of paths

Where `trees` is filled, the hub is the path its Hub slot names, `memory/` means the hub's `memory/`, and a session whose project root is not the hub path — compared in the Path form the `verification` binding names — is in a branch tree; a path lies under another when it is that path followed by a separator and further components, compared the same way, so a sibling that merely shares its prefix does not; where the `verification` binding carries no *Path form*, or is a single `none`, a step that needs this comparison stops and says so, naming the slot.

The base's `scripts/hub.py sessions-at "<path>"`, run from the project root, compares in Windows form, case-insensitively and with either separator, so it applies this comparison to the `session-launch` List command's output where the Path form is Windows form, and a step that asks which sessions are at a tree runs it rather than comparing by eye; under another Path form the step compares by that Path form itself. The base's `scripts/hub.py predecessor "<path>" <session id>`, run from the project root, applies the same comparison where the Path form is Windows form and prints one of `gone`, `elsewhere <cwd>`, `alone`, `no-id` and `stop <id>`, on which `close-tree` `## A handed-off session` acts.

## The hub clause

The hub clause: where `trees` is filled, a command a step runs in the hub runs there as the binding writes it when the session's project root is the hub, and by the Hub slot's idiom from any other session. A slot whose command the binding says runs as written from any tree is run that way wherever a step names the hub clause or the Hub slot's idiom.

## The hub operation

Under a filled `trees`, every write to `memory/`, by any skill — a STATUS update made through the `board` binding's default included — is one hub operation: first the hub is read on the main branch, by the `vc` binding's current-branch read, and clean, by the Clean read, both run in the hub by the Hub slot's idiom, and a hub off the main branch, or not clean, stops the operation, saying which; then the write is made by absolute path, re-reading the dated note before appending to it, editing only the session's own branch's block of STATUS `## Current`, and writing a handoff brief under the hub's `memory/handoffs/`; then it is checked in on the main branch in the hub, per the `vc` binding's check-in procedure; and nothing else runs in the hub in between. The base's `scripts/hub.py` makes those writes as LF bytes — `status line <section> <key> --from <file>`, which adds or replaces one entry of STATUS by its key, and `status line <section> <key> --remove`; `status set <section> --from <file>`, which replaces a whole section and with it whatever another session wrote there since the file was made, so it is kept for a rewrite no single entry carries; and `note append --from <file>`; each with `--root <hub>` — one command each.

One case is excepted: a session whose project root is the hub, while the hub is checked out on that session's own branch — a branch with no tree of its own — writes `memory/` as it would with `trees` absent, committed on that branch, and hub operations resume for the hub once that branch's merge returns it to the main branch; a session in a branch tree that finds the hub off the main branch still stops, as above.

## A report to the hub

A report to the hub is one plain-ASCII line, `<EVENT> - <branch>; lane: <lane>; <detail>`, the event `STARTED`, `FINISHED`, `BLOCKED` or `HANDOFF`, sent to the Hub session name of the project the sending session's tree belongs to — the one its brief's Reporting names, or, for a session sending on its own account, its own `session-launch` binding's — and never answered. On a hand-off launch the outgoing session sends `HANDOFF - <branch>; lane: <lane>; brief <file name>` after the brief and before the launch, `<file name>` the brief's file name, not its path, and `BLOCKED - <branch>; lane: <lane>; launch failed` when that launch fails; a session whose brief carries a predecessor sends `STARTED - <branch>; lane: <lane>; <skill>; predecessor <session id>`, the id in full.
