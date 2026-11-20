# g2-accept — page groups

How `g2-accept`'s `## Workflow` PRODUCT DOCS runs where the `product-docs` binding's *Page groups* is filled, after step 6's stage rule.

## The read

The base's `scripts/harness.py doctor --page-groups --root <project> --main <main branch> --branch <branch>`, one command, alone in its call, its Bash call given 600000 ms, `<main branch>` the `vc` binding's *Main branch* and `<branch>` the feature's branch, each with any leading `/` dropped, the base being the `base` path in `.claude/harness.json`, runs the `vc` binding's changed-file read, among its *Base-revision reads*, from the branch's merge base with the main branch to its head itself; `scripts/harness.py doctor --help` states each rule. It writes nothing in the project and exits 0 on a read. Its first line is `page groups: absent | none | filled | fault — <fault>`; where it reads `filled`, the lines after it are `front door: <path> | none`, that path the *Location* path whose stem is `readme`, then `changelog: <path> | none`, then one `dispatch <n>: <pages> (front door | group <g>[, part <k> of <m>])` per dispatch, then `not dispatched: <pages> | none`; the table below reads it.

| `doctor --page-groups` prints | PRODUCT DOCS runs |
|---|---|
| `page groups: filled` | `## The dispatches`, one per `dispatch <n>:` line |
| `page groups: absent`, `page groups: none` or `page groups: fault — <fault>` | `## Today's one dispatch` |
| `manual: <slot>` alone | nothing was read: the changed-file read runs as the `vc` binding's *Base-revision reads* writes it, from the branch's merge base with the main branch to its head, `<base>` that merge base where the read takes one, as its own bare call, its printed output saved as printed, each tab kept, to `.scratch/g2-accept-<feature>-changed.txt` by the Write tool, never by a redirect; then the base's `scripts/harness.py doctor --page-groups --root <project> --changed .scratch/g2-accept-<feature>-changed.txt`, its first line read by the other rows; where the slot writes no such read, `## Today's one dispatch` |
| exit 1, `doctor: refused` on stderr, or any other output | `## Today's one dispatch`, the report's first line `page groups: not read — <the refusal>` |

A source ending `/` is that directory and every file under it; any other source is one file or a glob of `*`, `**` and `?`; every path is written with `/`. A *Location* page no group lists, the front door and the `changelog:` file aside, is reconciled by no dispatch.

A touched group is split into runs of at most 5 pages, in the slot's order — the cap, provisional → the tuning pass.

## The dispatches

The reconciling agent missing from the composed set is checked once, before any dispatch, as step 6 says. Each `dispatch <n>:` line, in order, is one dispatch, and the dispatches run serially: the next brief is filed only once the last dispatch has returned, one writer in the tree. Each brief is `.scratch/g2-accept-<feature>-product-docs-<n>-brief.md`, today's, filed from `.claude/references/core/decision-brief.md`, the human's G2 ruling — who, when, in what form — its `## Decision record` `Ruling:` line in every brief alike, its `## Task` gaining, after `- Task:`, the lines `- Pages: <pages>`, the line's pages verbatim, and `- CHANGELOG: the PM's — write no entry`. A dispatch returning other than `DONE` is reported with its status and its pages, and the next dispatch goes on; its pages stay off `not dispatched:`.

## The CHANGELOG line

After the last dispatch returns, whatever the statuses, or, where the verb prints no `dispatch` line, once the read is done, the PM writes the branch's one line into the `changelog:` file, in the entry form that file holds, from the phases' `implement` REPORT suggested lines; `changelog: none` writes none and says so.

## The report

Relayed with the edits at step 7's relay, or, where the steps run as `references/tree-path.md` orders them, at its SHOW; its lines, in order:

1. The verb's first line verbatim, or `page groups: not read — <why>`.
2. `dispatch <n>: <pages> — <STATUS>`, one per dispatch.
3. `not dispatched: <pages> | none`.
4. `outside the page list: <file> — <what it needs>`, one per such line the agent reported.
5. `changelog: <the line> | none — <why>`.

## Today's one dispatch

Where the verb prints `absent`, `none` or a fault, or where the changed-file read or the verb is refused, fails or cannot run, the *Base-revision reads* naming no changed-file read included, step 6's one dispatch runs as written: no `Pages:` line, the CHANGELOG as today. The report's first line is then the verb's line, or `page groups: not read — <why>`.

## Bindings

- `product-docs` [Location, Reconciling agent, Page groups] (optional) — the front door's *Location*, the agent each dispatch goes to, the groups.
- `vc` [Main branch, Base-revision reads] (required) — the changed-file read.

<!-- harness:binding:start product-docs .claude/bindings/wiki.md -->
- Location: `Documentation~/` (the hub `Documentation~/index.md`, the per-type API reference under `Documentation~/api/`), plus `CHANGELOG.md` and `KNOWN_LIMITATIONS.md` at the root
- Layout: docs+changelog
- Reconciling agent: codex-reconcile
- Absent: no `Page groups` line in `## product-docs` of `.claude/bindings/wiki.md`; read `Page groups` as absent.
<!-- harness:binding:end product-docs -->

<!-- harness:binding:start vc .claude/bindings/vc-git.md -->
- Main branch: `main`
- Auto-mode entries: excluded
- Base-revision reads: `git show <revision>:<repo-relative-path>`; `git diff --name-status <base>..<head>` for the changed-file list; `git diff <base>..<head> -- <path>`; `git log --oneline <base>..<head>`; `git status --short` for the working-tree read; `git branch --show-current` for the current branch; `git rev-parse HEAD` for the current commit; `git diff --numstat --no-renames <base>..<head>` for the per-file line-count read; `git config --get <key>` for a config key's read, a bare `git config <key>` matching no allow rule
<!-- harness:binding:end vc -->
