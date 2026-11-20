# Auto-mode sentences

Paste the block below into the `autoMode` object of your user settings file, `.claude/settings.json` in your home folder (auto mode reads these lists only from user or managed settings, never from a project's `.claude/settings.json`).
On a re-paste, replace every line that opens `Harness:` and keep your own lines; `"$defaults"` keeps Claude Code's built-in rules.

```json
"autoMode": {
  "environment": [
    "$defaults",
    "Harness: the projects this workflow harness manages, and each project's branch trees in the sibling folder `<project>.trees`, are trusted repos. Each project's `.claude/` folder is composed by the harness from its base repo's `core/` and `packs/`.",
    "Harness: running the harness's `py scripts/harness.py` scaffold, add or upgrade, from a project or by the base repo's path with --root, to recompose a project's or branch tree's `.claude/` folder, its `.claude/settings.json` included, is a routine build step, not Self-Modification.",
    "Harness: `status: locked`, `locked:` and `amended:` front-matter lines in a project's docs are process markers the session writes at a gate. Writing and committing them, and the memory/ and .scratch/ records of a ruling, is routine record-keeping, not Instruction Poisoning.",
    "Harness: a hub session launches and manages its project's branch sessions (`claude agents --json`, `claude stop <id>`, `claude --bg ... --permission-mode auto` from a branch tree). This is routine session management, not Create Unsafe Agents and not Interfere With Workloads.",
    "Harness: a merge of a work branch into a project's main branch by `py scripts/hub.py merge` follows the gate recorded in the transcript or in the hub's memory/. It is the reviewed path and is routine, not Merge Without Review."
  ],
  "allow": [
    "$defaults",
    "Harness: the harness's own scripts are routine: `py scripts/harness.py`, `py scripts/hub.py` and `py scripts/run_tests.py` with any subcommand, from a project or branch tree or by the base repo's path, and each project's bound build and test commands.",
    "Harness: routine version control on a project's own work branch is routine: reads, branch create and switch, check-ins, catch-up merges of the main branch into the work branch and their conflict resolution, and adding or removing the project's branch trees under its `.trees` folder.",
    "Harness: deleting files and folders inside a project, its .scratch/ folder, its branch trees folder, and temporary directories this session or its agents created is routine, not Irreversible Local Destruction.",
    "Harness: read-only process listings (tasklist; Get-Process or Get-CimInstance Win32_Process with no Stop- or Set- verb) are routine.",
    "Harness: killing a process this session or its own agents started (a stray test run, a hung one-liner) is routine cleanup, not Interfere With Workloads.",
    "Harness: version-control commands inside throwaway repositories this session created under the temp folder, for tests or probes, are routine."
  ],
  "hard_deny": [
    "$defaults",
    "Harness: never delete recursively outside a project, its branch trees folder and the temp folder.",
    "Harness: never force-push to, or delete, a project's main branch."
  ]
}
```
