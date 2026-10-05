# unity-cli-contract — the harness's contract with the Unity CLI

The contract every agent doing Unity work follows when it calls `unity status`, `unity command` and `unity test`: how a call names its tree, how the route is selected, how green is read, the caller label, and the CLI facts these rest on.

## What it is not

- Not the command catalog: read it at run time with `unity command --format json`; the catalog is the Editor's own.
- Not the plugin's guidance: the `unity` Claude Code plugin is the PM's guide and the CLI's install, and no pack skill or agent duplicates it.
- Not what a project owns: the Fallback's exit codes are the project's Failure rule, and a cold tree's order is its Prerequisites prose.
- Not scene or asset work, which this reference does not cover.

## M1 — the tree is named

Every `unity status` and `unity command` call names this tree with `--project-path "<root>"`, `<root>` the tree's root as the binding's `Working-dir read` prints it; `unity test` takes the root positionally. With no unique match the CLI fails rather than guesses (`AMBIGUOUS_EDITOR`, exit 6), so no route can reach another tree's project.

## M2 — the selector

`unity status --project-path "<root>"` selects the route: an instance `ready` selects the live route, the binding's Commands; exit 6, `STATUS_NO_INSTANCES`, selects the Fallback, batchmode `unity test` opening this tree's own project. The selector's exit is never a FAIL.

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

Each fact names its probe and its date. Facts 1–8 are tlb-hub's probes, cited as reported and not re-run, save fact 6's `unity status` half, which `tree-holder-check`'s G2 live probe re-dated; fact 9 is that probe's. A fact the CLI contradicts is re-probed and re-dated, never patched by hand.

1. `--caller plugin --skill <name>` is accepted by `unity command` only; `unity status` exits 2 on it — tlb-hub, 2026-10-01.
2. `run_tests` exits 0 with outer `success:true` on an invalid `--filter_type`; green reads `data.result.success` and `data.result.Summary` — tlb-hub, 2026-10-01.
3. An unmatched filter reports 0 tests as success on both routes; green needs a total above zero — tlb-hub, 2026-10-01.
4. `run_tests` neither refreshes nor recompiles; only `recompile` calls `AssetDatabase.Refresh` — tlb-hub, 2026-10-01.
5. `recompile`'s domain reload can drop the connection; pollers retry — tlb-hub, 2026-10-01.
6. `unity test` refuses while an Editor holds the project (exit 6, `COMMAND_FAILED`) — tlb-hub, 2026-10-01; with no Editor `unity status` exits 6 and prints `STATUS_NO_INSTANCES` only with `--json` (fact 9) — tree-holder-check G2, 2026-10-02.
7. `run_tests` writes no XML; `unity test` writes NUnit XML and its JSON envelope carries no counts — tlb-hub, 2026-10-01.
8. `--project-path` on every `unity status`/`unity command` call; `unity test` takes the path positionally — tlb-hub, 2026-10-01.
9. With no Editor running, `unity status --project-path "<tree>"` exits 6 and prints the header row alone (`Port`, `State`, `Project`, `Version`, `PID`, tab-separated), with no error code; with `--json` it exits 6 and prints `{"success": false, "data": {"count": 0, "instances": []}, "errors": [{"code": "STATUS_NO_INSTANCES", …}]}`; `unity status --help` lists `--json`; an Editor without the Pipeline package is unlisted ("No Unity Editor instances found with the Pipeline package installed"); `--project-path` matches a substring, case-insensitive — tree-holder-check G2, 2026-10-02.

M3 rests on facts 2–5.

## Where a project reaches it

A project composing this pack reaches this file at `.claude/references/unity/unity-cli-contract.md`; its facts are dated, so drift from the CLI reads as a dated record.
