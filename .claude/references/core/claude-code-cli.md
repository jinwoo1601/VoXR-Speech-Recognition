# Claude Code CLI — session commands

The behaviour of the Claude Code CLI that the `session-launch` binding's commands rely on — the same for every project; the binding holds only the commands and the project's own facts.

## Launch

- `claude --bg "<prompt>"` starts a session in the background, with no window, in the current working directory, `<prompt>` its first message.
- `--remote-control <name>` makes the session reachable remotely, from the phone; it must be given its name, or it takes the next argument — the prompt — as one.
- `-n <name>` names the session: the name it carries in the session list, and its messaging address.
- `--permission-mode <mode>` sets the permission mode the session runs in.
- `CLAUDE_CODE_FORCE_SESSION_PERSISTENCE=1` in the session's environment keeps its transcript.
- A background launch prints `backgrounded · <id> · <name>`.
- An interactive session opened with `claude --remote-control <name> -n <name>` is reachable and addressed under `<name>` the same way.

## List

- `claude agents --json` prints one object per running session on the machine, interactive and background alike, each with its `cwd`. A background session's object carries an `id`; an interactive session's carries none, and it is not stopped from the command line.

## Stop

- `claude stop <id>` ends one background session, by the `id` the list shows — never by its `-n` name. Its conversation is kept and can be resumed.
