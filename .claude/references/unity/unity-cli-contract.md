# unity-cli-contract — the harness's contract with the Unity CLI

The contract every agent doing Unity work follows when it calls `unity status`, `unity command` and `unity test`: how a call names its tree, how the route is selected, how green is read, the caller label, and the CLI facts these rest on.

## What it is not

- Not the command catalog: read it at run time with `unity command --format json`; the catalog is the Editor's own.
- Not the plugin's guidance: the `unity` Claude Code plugin is the PM's guide and the CLI's install, and no pack skill or agent duplicates it.
- Not what a project owns: the Fallback's exit codes are the project's Failure rule, and a cold tree's order is its Prerequisites prose.
- Not scene or asset work, which this reference does not cover.

## M1 — the tree is named

Every `unity status` and `unity command` call names this tree with `--project-path`, and `unity test` names it positionally; `<root>` is the tree's root as the binding's `Working-dir read` prints it. `--project-path` matches a substring, case-insensitive (fact 9), so a unique match can be another tree whose path contains this one's. `unity status` matches its value raw, separator-sensitive (fact 11), so a `unity status` call takes `<root>` in the Editor's own path form, converted from the `Working-dir read`'s output by the conversion the binding's `Path form` names, run as its own call just before, its output double-quoted into `--project-path`, or taken as printed where the `Path form` says that read already prints that form; `unity command` resolves the path and takes `--project-path "<root>"` as printed, and `unity test` takes `<root>` as printed, positionally. A live call is made only after `unity status --project-path "<root>" --json`, `<root>` so converted, lists an Editor whose `project` equals `<root>`, the two compared as the binding's `Path form` compares paths; a listing whose instances are none of them this tree's selects the Fallback (M2). With more than one Editor matching, the CLI fails rather than guesses (`AMBIGUOUS_EDITOR`, exit 6).

## M2 — the selector

`unity status --project-path "<root>" --json`, `<root>` taken by M1's form rule, selects the route, read by `errors[].code` with `data.instances` from output of the shape facts 9 and 10 record, never by exit alone. This tree's Editor — an entry of `data.instances` whose `project` equals `<root>` by M1's read, listed with no error code — in any state selects the live route, the binding's Commands: that Editor holds the project, so batchmode would be refused (fact 6), and the live Commands' polls absorb a busy Editor (M3). Anything else selects the Fallback, batchmode `unity test` opening this tree's own project: `STATUS_NO_INSTANCES`, instances none of which is this tree's, `AMBIGUOUS_EDITOR`, or unreadable output — any other code, or output not of that shape; a conversion that fails or prints no path is unreadable output. A Fallback refused because an Editor holds this project is the FAIL that names the holder, as `data.instances` lists it, or, where it lists none, by the selector's and the refused Fallback's raw output. The selector's exit is never a FAIL.

## M3 — reading green

- Green is read from `data.result`, not the outer `success`.
- A test run is green only with a total above zero, on both routes: an unmatched filter reports 0 tests as success.
- A targeted run recompiles first, since `run_tests` does not refresh.
- A poll that loses the connection during the domain reload retries.
- On the Fallback, the counts are read from the NUnit XML that `unity test` writes; its JSON envelope carries none.
- The bounds: poll ~5 s, cap 5 minutes, `run_tests` timeout 900 s — provisional → the tuning pass.

## The caller label

`--caller plugin --skill unity-cli` goes on `unity command` calls only; `unity status` exits 2 on it (fact 1), and nothing is claimed for `unity test`. The label is provisional: fact 1 shows this form accepted, no more. If a probe changes it, this reference and every binding that copies it change together.

## The CLI facts

Each fact names its probe and its date. Facts 1–8 are tlb-hub's probes, cited as reported and not re-run, save fact 6's `unity status` half, which `tree-holder-check`'s G2 live probe re-dated; fact 9 is that probe's, fact 10 its run of 2026-10-03 with an Editor open, save its envelope, which `unity-selector-exact-path`'s F8 probe of 2026-10-12 re-dated, and fact 11 that F8 probe's. A fact the CLI contradicts is re-probed and re-dated, never patched by hand.

1. `--caller plugin --skill <name>` is accepted by `unity command` only; `unity status` exits 2 on it — tlb-hub, 2026-10-01.
2. `run_tests` exits 0 with outer `success:true` on an invalid `--filter_type`; green reads `data.result.success` and `data.result.Summary` — tlb-hub, 2026-10-01.
3. An unmatched filter reports 0 tests as success on both routes; green needs a total above zero — tlb-hub, 2026-10-01.
4. `run_tests` neither refreshes nor recompiles; only `recompile` calls `AssetDatabase.Refresh` — tlb-hub, 2026-10-01.
5. `recompile`'s domain reload can drop the connection; pollers retry — tlb-hub, 2026-10-01.
6. `unity test` refuses while an Editor holds the project (exit 6, `COMMAND_FAILED`) — tlb-hub, 2026-10-01; with no Editor `unity status` exits 6 and prints `STATUS_NO_INSTANCES` only with `--json` (fact 9) — tree-holder-check G2, 2026-10-02.
7. `run_tests` writes no XML; `unity test` writes NUnit XML and its JSON envelope carries no counts — tlb-hub, 2026-10-01.
8. `--project-path` on every `unity status`/`unity command` call; `unity test` takes the path positionally — tlb-hub, 2026-10-01.
9. With no Editor running, `unity status --project-path "<tree>"` exits 6 and prints the header row alone (`Port`, `State`, `Project`, `Version`, `PID`, tab-separated), with no error code; with `--json` it exits 6 and prints `{"success": false, "data": {"count": 0, "instances": []}, "errors": [{"code": "STATUS_NO_INSTANCES", …}]}`; `unity status --help` lists `--json`; an Editor without the Pipeline package is unlisted ("No Unity Editor instances found with the Pipeline package installed"); `--project-path` matches a substring, case-insensitive — tree-holder-check G2, 2026-10-02.
10. With an Editor open on the root, `unity status --project-path "<root>" --json` exits 0 and prints `{"success": true, "command": "status", "data": {"count": 1, "instances": [{"port": …, "project": …, "version": …, "pid": …, "state": "ready"}]}, "errors": [], "warnings": []}` (CLI `1.0.0-beta.11`), `project` a drive-lettered path in backslash form — unity-selector-exact-path F8, 2026-10-12; that `project` matched the tree's path as a backslash List read printed it, with a lower-case drive — tree-holder-check G2, 2026-10-03.
11. With an Editor `ready` on a git repository (CLI `1.0.0-beta.11`), `unity status --project-path` matches its value as a raw, case-insensitive substring of the Editor's `project`, separator-sensitive: the root as `pwd -W` printed it, forward separators, exited 6 with `count: 0` and `STATUS_NO_INSTANCES`; the backslash form exited 0 with `count: 1` and `project` the backslash form, and a lower-case backslash value also listed it; `unity command --project-path … --format json` reached the Editor with either separator, `target.projectPath` the backslash form; `unity command` on a path holding no project printed `COMMAND_FAILED` ("Not a Unity project") and exited 0 — unity-selector-exact-path F8, 2026-10-12.

M1 and M2 rest on facts 6, 9, 10 and 11. M3 rests on facts 2–5.

## Where a project reaches it

A project composing this pack reaches this file at `.claude/references/unity/unity-cli-contract.md`; its facts are dated, so drift from the CLI reads as a dated record.
