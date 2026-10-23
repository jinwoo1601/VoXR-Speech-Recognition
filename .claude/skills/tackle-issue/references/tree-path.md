# tackle-issue — the tree path

The order `tackle-issue`'s `## Workflow` steps run in where `trees` is filled, and the binding sentences only that order uses; "the contract" is `.claude/references/core/trees-contract.md`.

## Bindings

- `trees` (optional) — the Hub, Location, Create procedure and List read slots `## The tree path` uses; the Release files slot, the release class INDEPENDENCE sets aside, as the contract's `## The release files` reads it; and the Hub slot's idiom by which a command runs in another tree.
- `session-launch` (optional) — read by `handoff`, which the cutting run's LAUNCH invokes, for the launch and the brief's Reporting section.

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
- Context read: `python3 -c "import glob,json,os;p=glob.glob(os.path.expanduser('~/.claude/projects/*/'+os.environ['CLAUDE_CODE_SESSION_ID']+'.jsonl'))[0];t=[n for n in (u['input_tokens']+u['cache_creation_input_tokens']+u['cache_read_input_tokens'] for u in (e['message']['usage'] for e in map(json.loads,open(p,encoding='utf-8')) if e.get('type')=='assistant')) if n>0];print(t[-1])"` — run in Git Bash from the project root; prints the context of this session's last assistant turn that carries usage, from its transcript under Claude Code's projects directory in the user's home; lags at most one turn
<!-- harness:binding:end session-launch -->

## The tree path

Where `trees` is filled, as the contract reads it, a run first settles whether it is resumed, before `## When it applies — and prerequisites` is confirmed. It is resumed when this session's project root is not the Hub path, as the trees contract (`.claude/references/core/trees-contract.md`) compares them, the List read names that root as a tree, and the block of the hub's `memory/STATUS.md` `## Current` for that tree's branch names the issue, or the batch, the human named for this run; any other run is a cutting run.

**A cutting run** takes these steps, each naming the `## Workflow` steps it runs; CUT stands in for BRANCH, and the run ends at LAUNCH.

1. **SELECT, UNDERSTAND and DECLARE** — `## Workflow`'s SELECT, UNDERSTAND and DECLARE, as written. DECLARE's record is written at STATUS.
2. **INDEPENDENCE** — read the other active branches from the blocks of the hub's `memory/STATUS.md` `## Current`, by absolute path under the Hub path, and state for each whether this branch is independent of it: neither depends on the other, and their expected write sets do not overlap outside the release class, `memory/` exempt; and, where a release class exists, name each overlap outside it (code, locked docs, annex register rows, the shared test file) and the release files whose overlap is set aside. The release class is the `trees` binding's Release files slot, as the contract's `## The release files` reads it: an entry with no rule is outside it, and where the slot names no release class, every overlap is outside it. Where no release class exists, the human rules run concurrently or wait; where one exists, the human rules only where any branch is stated not independent, and where none is, or no other active branch exists → no ruling: the branch runs concurrently, shown as a default in the run's output. Wait → stop; nothing is made. No other active branch → nothing to rule.
3. **CHECKS** — the section carries the Hub, Location, Create procedure and List read slots; the tree's path, as Location names it for this branch, does not exist; and the Create procedure's branch check finds no branch of this name. Any failing → stop and say which; nothing is made.
4. **CUT** — the Create procedure, run in the hub by the Hub slot's idiom, cuts the branch the `vc` binding's branch procedure names for this work and its tree at that path; then the List read must name the tree at its path, on its branch. It does not → stop and say what the List read shows.
5. **STATUS** — the branch's line — its name, the light lane, its class as `class <c>`, the issue or a batch's items as `batch <id>, <id>...`, and the next action, with DECLARE's word, within the `board` binding's *STATUS line cap* (absent or `none` → no cap stated) — written as a draft into `.scratch/tackle-issue-<slug>-record/`, `<slug>` the topic of the branch CUT cut, as the one entry line, opening `- **` and its key, for `--entry Current <branch> <file>` on the hub's `memory/STATUS.md` `## Current`; LAUNCH's call lands it in the hub.
6. **LAUNCH** — invoke `handoff` for the branch's launch brief: the target the new tree, by its path per the List read; the lane `light, class <c>`, or for a batch `light, class <c>, batch <id>, <id>...`; the next step `tackle-issue` for the same issue or batch in that tree, whose resumed run goes on from DIAGNOSE; and STATUS's draft. `handoff`'s one call lands that entry and the brief as one commit, and only then launches the session, or gives the paste line, telling the human the tree's path and to open a plain `claude` session there, never `claude -w`. The run stops here: DIAGNOSE and every step after it run in that session, as a resumed run.

**A resumed run** works that issue in the branch's own tree. SELECT takes the issue, or the batch, the block names; the prerequisite that no changeset for this issue is already in flight, and SELECT's skip of an issue already in flight, are met by this run's own branch, not failed by it; UNDERSTAND runs as written; DECLARE takes the class the block's line records; BRANCH cuts nothing, since the session is already in the branch's own tree; and DIAGNOSE through REPORT AND STOP run as written, in this tree.
