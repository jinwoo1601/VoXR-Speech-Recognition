# core bindings

## process-docs
<!-- Interview: Where do this project's design docs and feature docs live, what are the files called, and at which path is a phase's plan file written, and at most how many KB may an architecture doc's body, a plan file and a brief each hold, at which path pattern? -->
- Design docs: `Planning~/design-docs/<topic>.md`, one file per design topic, its decision records beside it per design-track's ADR convention; the root-level `Planning~/*.md` vN docs are pre-harness history, read-only
- Feature docs: `Planning~/features/<feature>/requirements.md` and `Planning~/features/<feature>/architecture.md`, one directory per feature
- Persisted plan: `Planning~/features/<feature>/plan-<id>.md`
- Architecture-doc body budget: 40 KB over `Planning~/features/<feature>/architecture.md`; the closed features' oversized docs, all written before the harness, are ruled in `memory/doctor-rulings.md`
- Plan-file budget: 10 KB over `Planning~/features/<feature>/plan-*.md`
- Brief budget: 15 KB over `.scratch/**/*brief*.md`

## board
<!-- Interview: Does this project track cross-session state on a board — which, and how is it read — or is memory/STATUS.md the only state? And at most how many characters may one memory/STATUS.md line carry, or is there no cap? And at most how many KB may memory/STATUS.md hold, or is there no budget? -->
- Mechanism: none — memory/STATUS.md
- STATUS line cap: 200
- STATUS budget: 10 KB

## session-launch
<!-- Interview: Does the PM launch sessions itself — by which command a session is started in a given tree for a given branch with a one-line prompt, keeping its transcript and in the permission mode it runs in; under which name the hub session is opened and receives the launched sessions' reports; and by which commands the running sessions are listed and a finished one is stopped; how long a launch or a stop is waited on in that list before it is read as failed; and how long a stopped session may hold its tree's directory; past how many tokens of context a session hands off inside a phase, and by which command it reads its own context — or a single `none` where the human opens every session? -->
- Launch command: `cd "<tree>" && CLAUDE_CODE_FORCE_SESSION_PERSISTENCE=1 claude --bg --remote-control <branch> -n <branch> --permission-mode auto "<prompt>"`, run in Git Bash from the launching session, `<tree>` the project root in the Path form (`trees` is none) — the CLI's behaviour: `.claude/references/core/claude-code-cli.md`
- Hub session name: `voxr-hub` — this project's own hub session, which the human opens in the project root with `claude --remote-control voxr-hub -n voxr-hub`
- List command: `claude agents --json` — one object per running session on this machine, each with its `cwd`; see the reference for its shape
- Stop command: `claude stop <id>` — the `id` the list command shows
- Confirm window: 30 seconds (provisional → the tuning pass)
- Release wait: none — `trees` is none, so no tree is removed
- Context bound: about 200k tokens (provisional → the tuning pass)
- Context read: `python3 -c "import glob,json,os;p=glob.glob(os.path.expanduser('~/.claude/projects/*/'+os.environ['CLAUDE_CODE_SESSION_ID']+'.jsonl'))[0];t=[n for n in (u['input_tokens']+u['cache_creation_input_tokens']+u['cache_read_input_tokens'] for u in (e['message']['usage'] for e in map(json.loads,open(p,encoding='utf-8')) if e.get('type')=='assistant')) if n>0];print(t[-1])"` — run in Git Bash from the project root; prints the context of this session's last assistant turn that carries usage, from its transcript under Claude Code's projects directory in the user's home; lags at most one turn

## review
<!-- Interview: Which review angles does each depth profile run in this project — slim, prose and full, `default` keeping the roster's own list — and which code paths are hot (per-frame, per-tick, per-event or otherwise cost-critical), where the efficiency angle EFF runs? `Hot paths` is repo-relative paths or globs, or a single `none`. -->
- Slim angles: default (LINE, GONE, XFILE)
- Prose angles: default (LINE, GONE, XFILE, CONV)
- Full angles: default (the composed roster set)
- Hot paths: `Runtime/VoxrSpeechRecogniser.cs`, `Runtime/Dsp/**`, `Runtime/Native/**`, `Runtime/Commands/**`, `NativeBridge~/src/**` — the per-frame poll, the per-chunk audio path, the bridge, and the per-utterance parser

## layout
<!-- Interview: Which directories hold this project's source, which its tests, and which are generated or composed output never edited by hand — or a single `none`? -->
- Source: `Runtime/**`, `Editor/**`, `NativeBridge~/src/**`, `NativeBridge~/include/**`
- Tests: `Tests~/**`, `NativeBridge~/harness/**`
- Generated: `.claude/**` except `.claude/bindings/**` (composed by `harness.py`); `Runtime/Plugins/Android/arm64-v8a/*.so` (built from `NativeBridge~/` by its `build.sh`); `NativeBridge~/build*/` and `NativeBridge~/vendor/` (ignored)
