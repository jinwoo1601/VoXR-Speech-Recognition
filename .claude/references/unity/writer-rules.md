# writer-rules — the rules code-writer follows in a Unity project

The rules `code-writer` follows in a project composing this pack, and the commands it may run to follow them: `## Granted commands` grants those commands, and the rest of this file is its rules.

## Granted commands

- `unity status` — the contract's `## M1` read before scene, asset or prefab work, run as the contract writes it, with no caller label (the contract's fact 1).
- `unity command` — that work and the catalog read (`unity command --format json`), against this tree's Editor only, with the caller label of the contract's `## The caller label`; every catalog command but `eval`, which is not granted and denied by the settings; compiles and test runs only as the brief's Verification command says.

## Scene, asset and prefab work

Scene, asset and prefab work goes through `unity command` against the Editor holding this tree, as the contract's `## M1` finds it (`.claude/references/unity/unity-cli-contract.md`). YAML is never hand-edited with the Edit or Write tool, and the plugin's file-edit fallback is not taken. The files the work changes stay within the brief's Files in scope. C# source stays the Edit tool's.

## No Editor

Scene, asset or prefab work with no Editor of this tree — none listed, only another tree's, or the read failing or unreadable — or with one in play mode or Safe Mode → `BLOCKED`, naming the cause and what the read showed. Nothing is edited by hand instead. A phase with no such work is not blocked here: its targeted run takes the route its brief's Verification command states.

## Where a project reaches it

A project composing this pack reaches this file at `.claude/references/unity/writer-rules.md`.
