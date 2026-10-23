# Claude Code CLI — session commands

The behaviour of the Claude Code CLI that the `session-launch` binding's commands rely on — the same for every project; the binding holds only the commands and the project's own facts.

## Launch

- `claude --bg "<prompt>"` starts a session in the background, with no window, in the current working directory, `<prompt>` its first message.
- `--background` is the same flag as `--bg`: `claude --help` lists them together as `--bg, --background` (Claude Code 2.1.296).
- A background session meets Claude Code's background-session guard: Edit, Write and NotebookEdit on a file inside the session's current working directory are refused ("This background session hasn't isolated its changes yet. Call EnterWorktree first …") where that directory is a repository's main checkout rather than a linked worktree, or lies outside any repository while the settings register a `WorktreeCreate` hook. The vc packs' settings templates register none while writer trees are parked, so a background session outside any repository, whatever the vc pack, meets no such refusal while the hook is parked: that edit refusal is lost until a template registers the hook again. Bash is not gated. An edit is not refused where the current directory or the file is inside a linked worktree, or the file is outside the current directory, so a session launched into its own tree that does not `cd` out of it meets no refusal.
- The guard's opt-outs are the setting `worktree.bgIsolation: "none"` and the environment variable `CLAUDE_BG_ISOLATION=none`. The harness keeps the guard on and sets neither in its tracked settings: a session launched into a main checkout moves its edits into a tree by `EnterWorktree`, or the human rules the opt-out into that project's untracked `.claude/settings.local.json`. As observed on Claude Code 2.1.295.
- `--remote-control <name>` makes the session reachable remotely, from the phone; it must be given its name, or it takes the next argument — the prompt — as one.
- `-n <name>` names the session: the name it carries in the session list, and its messaging address.
- `--permission-mode <mode>` sets the permission mode the session runs in.
- `CLAUDE_CODE_FORCE_SESSION_PERSISTENCE=1` in the session's environment keeps its transcript.
- A background launch prints `backgrounded · <id> · <name>`.
- An interactive session opened with `claude --remote-control <name> -n <name>` is reachable and addressed under `<name>` the same way.

## List

- `claude agents --json` prints one object per running session on the machine, interactive and background alike, each with its `cwd`. A background session's object carries an `id`; an interactive session's carries none, and it is not stopped from the command line.
- Each object also carries `name`, the `-n` name the session was launched or opened under, and `status`: `idle` for a session waiting on nothing, otherwise another value, such as `busy` or `waiting`.
- Every object carries `sessionId`, the session's full id, the value its own `CLAUDE_CODE_SESSION_ID` holds, a background session's `id` its first 8 characters, and `startedAt`, the session's start in integer milliseconds since the epoch; both as observed on Claude Code 2.1.288, not in the CLI's own documentation.

## Stop

- `claude stop <id>` ends one background session, by the `id` the list shows — never by its `-n` name. Its conversation is kept and can be resumed.
