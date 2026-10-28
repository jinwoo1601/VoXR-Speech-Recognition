---
name: g0-open
description: "Open a design topic or feature at G0: agree its scope, cut its branch, record the scope. Use on 'start a design topic' or 'open a new feature'."
kind: orchestration
agents: [doc-writer]
stops: [the agreement on the topic and its scope, the ruling to run concurrently or wait where an overlap or dependency is found, the post-scope re-check on overlapping write sets]
status: active
bindings: [vc, process-docs, trees, session-launch, board]
---

# g0-open

## What this is

G0 opens a design topic or a feature: the human and the PM agree the scope, the branch is created, and the scope, with a feature's user story beside it, is recorded as the doc's opening sections — no doc or code exists before the branch does.

> The branch first, then the scope in writing. Nothing is built on the main branch.

## Bindings

- `vc` (required) — the branch procedure (the design or feature form the branch procedure names, off the main branch) and the check-in procedure. Absent → stop and say so before anything is created.
- `process-docs` (required) — the layout and the file names the opening sections are written into.
- `trees` (optional) — the Hub, Location, Create procedure and List read slots `references/tree-path.md` `## The tree path` uses; the Release files slot, the release class INDEPENDENCE sets aside, as the contract's `## The release files` reads it; and the Hub slot's idiom by which a command runs in another tree. Absent → today's path. Filled → the steps run as `references/tree-path.md` orders them, and SCOPE reads the hub's `memory/backlog-candidates.md` and `memory/notes-for-future-features.md` by absolute path under the Hub path.
- `session-launch` (optional) — the Hub session name, to which the branch run's SCOPE sends `FINISHED` for a declined or waiting item and its RE-CHECK sends `BLOCKED`; the List command, by which the active branches of `references/tree-path.md` `## The tree path` see the sessions at each tree; the launch and the brief are `handoff`'s, which LAUNCH invokes and which reads this key by its own `## Bindings`. Absent → no report is sent, the active branches read as `references/tree-path.md` has them where the key reads absent, and `handoff` gives the paste line. The Hub session name missing or `none` → no report is sent, and `handoff` gives the paste line; the List command missing or `none` → the active branches read as where the key reads absent, and `handoff` gives the paste line.
- `board` (optional) — the *STATUS line cap*, which STATUS keeps the branch's line within. Absent, `none` or missing the slot → no cap stated, never a stop.
- Agents — `doc-writer` missing → stop after BRANCH.

<!-- harness:binding:start vc .claude/bindings/vc-git.md -->
<!-- Interview: Which version-control system does this project use, and how are branches created, changes checked in, base revisions read, branches merged, the working tree recorded before an upgrade, and pull requests read, diffed and commented on? -->
- System: git; GitHub, remote `origin` `https://github.com/jinwoo1601/VoXR-Speech-Recognition.git`, public; `gh` on the Windows PATH (`C:\Program Files\GitHub CLI\gh.exe`), authenticated as `jinwoo1601`
- Main branch: `main`
- Auto-mode entries: excluded
- Branch procedure: a re-lock — a `design-<topic>` branch cut from the feature branch's head in the feature's own tree, declared on its STATUS line `re-lock of <feature-branch>`, merged back into that feature branch after G1 — is cut by `git switch -c design-<topic>` in the feature's tree and merged back by `git switch <feature-branch>`, then `git merge design-<topic>`, in that tree; branches are `design-<topic>`, `feat-<feature>`, `light-<topic>` (an issue's `light-<n>-<slug>`), `lab-<topic>`, which merges nothing, and the release prep `chore/release-X.Y.Z`; each is cut from an up-to-date `main` — `git fetch origin`, then on `main` `git merge --ff-only origin/main`, then `git switch -c <branch>` — and runs in the one checkout, since `trees` is none
- Check-in procedure: inline on the main thread — `git add -- <paths>`, then `git commit -F <message file> -- <paths>`, each one command, never a heredoc or a chain, never `git add -A` — a hub operation's `memory/` check-in by the base's `scripts/hub.py commit --message <file> --root <hub> <paths>`, which commits the named paths under `memory/` alone; then `git push -u origin <branch>` on a branch's first push and `git push` after; the commit sha is the receipt
- Base-revision reads: `git show <revision>:<repo-relative-path>`; `git diff --name-status <base>..<head>` for the changed-file list; `git diff <base>..<head> -- <path>`; `git log --oneline <base>..<head>`; `git status --short` for the working-tree read; `git branch --show-current` for the current branch; `git diff --numstat --no-renames <base>..<head>` for the per-file line-count read
- Merge procedure: a pull request against `main`, opened by `gh pr create --base main --title "<title>" --body-file <file>` after the push, merged by the human on GitHub behind the gate's stop; Claude never merges
- Snapshot procedure: `git stash create`, then, where it prints a sha, `git stash store -m "harness pre-upgrade <date>" <sha>` — the working tree and the index are left as they are; where it prints nothing, the snapshot is `git rev-parse HEAD`; confirmed by `git rev-parse --verify <sha>^{commit}`; restored by the human from that sha; `.claude/` and `CLAUDE.md` are tracked from `light-harness-adopt` on, so the record covers them — `.claude/settings.local.json` and `.scratch/` stay ignored and are not covered
- PR read: `gh pr view <n> --json number,title,body,url,state,isDraft,headRefName,headRefOid,baseRefName,files,labels` and `gh pr list --head "<branch>" --json number`; `gh` has no `baseRefOid` and no `closingIssuesReferences` field, and `--json` never goes without an explicit field list
- PR diff: `gh pr diff <n> > <file>`
- PR comment: `gh pr review <n> --comment --body-file <file>`, falling back to `gh pr comment <n> --body-file <file>`; never `--approve` or `--request-changes`, which GitHub refuses on one's own pull request; `gh pr edit` fails on the Projects-classic `projectCards` deprecation, so a PR body is patched by `gh api repos/jinwoo1601/VoXR-Speech-Recognition/pulls/<n> -X PATCH -F body=@<file>`
<!-- harness:binding:end vc -->

<!-- harness:binding:start process-docs .claude/bindings/core.md -->
<!-- Interview: Where do this project's design docs and feature docs live, what are the files called, and at which path is a phase's plan file written, and at most how many KB may an architecture doc's body, a plan file and a brief each hold, at which path pattern? -->
- Design docs: `Planning~/design-docs/<topic>.md`, one file per design topic, its decision records beside it per design-track's ADR convention; the root-level `Planning~/*.md` vN docs are pre-harness history, read-only
- Feature docs: `Planning~/features/<feature>/requirements.md` and `Planning~/features/<feature>/architecture.md`, one directory per feature
- Persisted plan: `Planning~/features/<feature>/plan-<id>.md`
- Architecture-doc body budget: 40 KB over `Planning~/features/<feature>/architecture.md`; the closed features' oversized docs, all written before the harness, are ruled in `memory/doctor-rulings.md`
- Plan-file budget: 10 KB over `Planning~/features/<feature>/plan-*.md`
- Brief budget: 15 KB over `.scratch/**/*brief*.md`
<!-- harness:binding:end process-docs -->

<!-- harness:binding:start trees .claude/bindings/vc-git.md -->
<!-- Interview: Does this project run branches in their own working trees — where the main checkout and the trees live and how trees are named, how a branch and its tree are cut, listed, read clean, caught up with the main branch and removed, how writer branches are named, and whether writer trees are pooled — or a single `none` where every branch runs in the one checkout? -->
- none
<!-- none: every branch runs in the one checkout — the host project `VoXR TestGround` consumes this checkout by `file:` path, so a branch in another tree would never be the one tested -->
<!-- harness:binding:end trees -->

<!-- harness:binding:start session-launch .claude/bindings/core.md -->
<!-- Interview: Does the PM launch sessions itself — by which command a session is started in a given tree for a given branch with a one-line prompt, keeping its transcript and in the permission mode it runs in; under which name the hub session is opened and receives the launched sessions' reports; and by which commands the running sessions are listed and a finished one is stopped; how long a launch or a stop is waited on in that list before it is read as failed; and how long a stopped session may hold its tree's directory; past how many tokens of context a session hands off inside a phase, and by which command it reads its own context — or a single `none` where the human opens every session? -->
- Launch command: `cd "<tree>" && CLAUDE_CODE_FORCE_SESSION_PERSISTENCE=1 claude --bg --remote-control <branch> -n <branch> --permission-mode auto "<prompt>"`, run in Git Bash from the launching session, `<tree>` the project root in the Path form (`trees` is none) — the CLI's behaviour: `.claude/references/core/claude-code-cli.md`
- Hub session name: `voxr-hub` — this project's own hub session, which the human opens in the project root with `claude --remote-control voxr-hub -n voxr-hub`
- List command: `claude agents --json` — one object per running session on this machine, each with its `cwd`; see the reference for its shape
- Stop command: `claude stop <id>` — the `id` the list command shows
- Confirm window: 30 seconds (provisional → the tuning pass)
- Release wait: none — `trees` is none, so no tree is removed
- Context bound: about 200k tokens (provisional → the tuning pass)
- Context read: `py -c "import glob,json,os;p=glob.glob(os.path.expanduser('~/.claude/projects/*/'+os.environ['CLAUDE_CODE_SESSION_ID']+'.jsonl'))[0];t=[n for n in (u['input_tokens']+u['cache_creation_input_tokens']+u['cache_read_input_tokens'] for u in (e['message']['usage'] for e in map(json.loads,open(p,encoding='utf-8')) if e.get('type')=='assistant')) if n>0];print(t[-1])"` — run in Git Bash from the project root; prints the context of this session's last assistant turn that carries usage, from its transcript under Claude Code's projects directory in the user's home; lags at most one turn
<!-- harness:binding:end session-launch -->

<!-- harness:binding:start board .claude/bindings/core.md -->
<!-- Interview: Does this project track cross-session state on a board — which, and how is it read — or is memory/STATUS.md the only state? And at most how many characters may one memory/STATUS.md line carry, or is there no cap? And at most how many KB may memory/STATUS.md hold, or is there no budget? -->
- Mechanism: none — memory/STATUS.md
- STATUS line cap: 200
- STATUS budget: 10 KB
<!-- harness:binding:end board -->

## Workflow

Where `trees` is filled, as the contract reads it, these steps run as `references/tree-path.md` orders them; otherwise they run as written.

1. **SCOPE** — the hard stop: the PM puts the G0 packet (`.claude/references/core/gate-packet.md` `## The G0 packet`) by `.claude/references/core/ruling-form.md` — one question asking only agreement to the topic, or the backlog feature, and its scope, its defaults shown: for a feature the user story, drafted by the PM in the form *As a \<user\>, I want \<capability\>, so that \<value\>.*, and the track; the lane; on the tree path, concurrency — each overridable in the same answer. Before putting the scope, the PM reads `memory/backlog-candidates.md` and `memory/notes-for-future-features.md`, where they exist. For a feature, the PM names with the scope the design doc it comes from, locked at G1 by `g1-lock`, and the feature's row in that design's backlog, or the locked design doc that names the feature, each where the `process-docs` binding puts design docs; where it can name neither, it puts no feature scope and creates nothing for the feature: it stops and offers a design topic for the work in its place, which this skill opens as a design topic where the human takes it. Nothing is created before that agreement. For a backlog feature the PM reads the row's mark and names the track, lite or full, with the scope (`.claude/references/core/feature-lite.md` `## The mark`).
2. **BRANCH** — per the `vc` binding's branch procedure; the ceremony is the PM's.
3. **RECORD** — file `.scratch/doc-writer-<slug>-g0-brief.md` from `.claude/references/core/decision-brief.md`: the agreed scope with each default shown and how it was ruled — taken, or overridden in the human's words — as the decision record, the target and form per `process-docs` — the design doc's scope section for a design topic, the requirements doc's §1, the user story, and §2, the scope, for a feature. For a lite feature the target is the architecture doc's `## User story` and `## Context & scope`, no requirements doc is made (`.claude/references/core/feature-lite.md` `## The three placements`), and the brief says `lite`, gives the backlog's path and quotes the row (`.claude/references/core/feature-lite.md` `## The acceptance source`). Dispatch `doc-writer` through the Agent tool without a `name`.
4. **STATUS** — the PM adds the branch's line to `memory/STATUS.md` (name, lane, stage, next action; within the `board` binding's *STATUS line cap* — absent or `none` → no cap stated) and checks in per the `vc` binding's check-in procedure.

Where `trees` is filled, as the contract reads it, several items the human orders at once start under one ruling, as `references/tree-path.md` `## Several items` orders them; where it reads absent, one item at a time through `## Workflow`, as today.
