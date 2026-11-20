# Hooks

`core` declares ten hooks in its settings template, so every project composed from `core` runs all ten. This page says what they do, what they rely on, and how to turn each off.

## What fires, and when

- **The session-start hook** — `.claude/references/core/doctor-hook.py`, on `SessionStart`, in the first item with the matcher `startup|resume|clear`. It runs `harness.py doctor` for the project — the manifest's `base` resolved against the project root, and from a branch tree where that names no directory, against the main checkout the `trees` binding's Hub slot names, as `harness.py` does — and relays the report into the session's context: the manifest / base / packs banner, each failure and warning row, and the `doctor: PASS` or `doctor: FAIL` line. It starts `harness.py` as bare `py`, with an active venv's `VIRTUAL_ENV` and its `PATH` entries dropped from the child's environment, so the script's shebang picks the registered Python, and falls back to the hook's own interpreter where there is no `py`; a child that prints no report and exits non-zero gives one `DOCTOR` warning line carrying its first stderr line; a child still running after 30 seconds gives one in its place, `warn  DOCTOR  <script>  harness.py doctor timed out after 30 s with no report`; a child that cannot be started, by `py` or the fallback, gives silence. Where the `session-launch` binding names a List command, the hook also warns (`HUB`) when two or more running sessions carry the Hub session name, or when that name is unbound; this reaches only sessions on this machine. The List command runs under a five-second wait that holds whatever the command's children do: its output goes to temporary files, so a child still running past it is left behind, never waited on. Outside a harness project — no `.claude/harness.json` — it prints nothing. It never fails a session: whatever the child returns, the hook exits 0. This item matches neither `compact` nor `fork`, because the context they carry forward already holds the report; a compaction has the compact orders. Its timeout is 40 seconds — the doctor child's 30 and the List command's 5, plus 5 for start-up; past it, no report is relayed.
- **The agent guard** — `.claude/references/core/agent-guard-hook.py`, on `PreToolUse` with the matcher `Agent`. It inspects every `Agent` dispatch and nothing else; on any other tool the platform starts no process at all. A dispatch it permits gets silence, never an explicit allow. On its own error it refuses nothing: the call takes the normal permission flow, and a system message reads `agent-guard: internal error (<type>: <message>); the call was not checked`.
- **The report filer** — `.claude/references/core/report-filer-hook.py`, on `SubagentStop` with no matcher, so it runs whenever an agent stops, foreground or background. It files the agent's final message: beside the brief the message's first line names — a `.md` file under a `.scratch/` directory — as `<brief name without -brief.md>-report.md`, numbered `-2`, `-3` for further answers to the same brief and never overwriting one; on any other first line, under `.scratch/reports/` as `<agent type>-<agent id>-report.md`. Where the agent's transcript holds a recon agent's `# Full report —` message, that text is filed ahead of the final message. For a harness agent — one with a file under `.claude/agents/` — it also checks the receipt: the first line `<STATUS> — <brief path or task>`, and, on `DONE` from an agent whose file declares the one-line `Trailer:`, a trailer line naming criteria, files and verification. Its one refusal: a malformed receipt keeps the agent running once, with the remedy as its next input, and files nothing; the stop that follows is filed whatever it holds, with a system message naming the defect. Built-in agents are filed, never checked. A report it files without a defect, it files in silence. On its own error it never blocks: the agent stops, and a system message reads `report-filer: internal error (<type>: <message>); no report was filed`.
- **The write guard** — `.claude/references/core/write-guard-hook.py`, on `PreToolUse` with the matcher `Write|Edit|NotebookEdit`, given the project directory as its argument. It holds one rule, the hub memory rule. The hub memory rule watches a Write, Edit or NotebookEdit aimed beneath the hub's `memory/` where the `trees` section is filled; a path holding no `memory` component gets silence, with no file read. On its own error it refuses nothing: the call takes the normal permission flow, and a system message reads `write-guard: internal error (<type>: <message>); the call was not checked`. Its timeout is 30 seconds, provisional — about three times the worst case measured under the suite's load (2026-11-09); past it, the call proceeds unchecked.
- **The context-bound hook** — `.claude/references/core/context-bound-hook.py`, on `PostToolUse` with no matcher, so it runs after every tool call, given the project directory as its argument. In the main thread, once the session's context passes the `session-launch` binding's *Context bound*, it puts one line into the model's context on every call, by the session's class; at or below the bound, in a subagent and on any error it is silent (`## What the context-bound hook prints`). Its timeout is 5 seconds, provisional; past it, the tool's result stands and no line is added.
- **The compact orders** — `.claude/references/core/context-bound-hook.py` again, on `SessionStart` with the matcher `compact`, given the project directory and then `compact`. A hub session's standing orders go into the model's context; otherwise, in a subagent and on any error it is silent (`## What the context-bound hook prints`). Its timeout is 5 seconds, provisional; past it, no orders are added.
- **The platform checks** — two inline commands, no script, on `SessionStart` in a second item with the matcher `startup|resume|clear`, after the session-start hook's. The first runs in the hook shell and needs no Python: it prints one line where the machine is not Windows (`OS` is not `Windows_NT`) or, on Windows, where `py` is not a command, and nothing otherwise; under PowerShell it fails to parse before anything runs, and prints nothing. The second runs with `"shell": "powershell"` and prints one line where Git Bash is not found by any of three ways: `CLAUDE_CODE_GIT_BASH_PATH` naming an existing file, `bin\bash.exe` two levels above the first `git` on `PATH`, or `Git\bin\bash.exe` under `ProgramFiles`. Each line opens `harness platform check:`, names the cause, says the harness guards are off on this machine, names the fix, and asks Claude to say so in its first reply. On a guarded machine both are silent, and each exits 0 in every case it handles. Their timeouts are 5 and 15 seconds, provisional; past one, that check adds nothing.
- **The stop-line hook** — `.claude/references/core/stop-line-hook.py`, on `Stop` with no matcher, so it runs at each main-thread turn end, given the project directory as its argument. It blocks an end once where, in skill work, its final text closes on no stop line, or where, in any context, it closes on a stop line of a waiting kind with no AskUserQuestion call since the newest human prompt; every other end passes, and on any error it is silent (`## What the stop-line hook passes`). Its timeout is 10 seconds, provisional; past it, the turn ends unchecked.
- **The task-leftover hook** — `.claude/references/core/task-leftover-hook.py`, on `Stop` with no matcher, in a second entry after the stop-line hook's, so it too runs at each main-thread turn end, given the project directory as its argument. It reads the session's transcript for task notifications and the main thread's `TaskStop` calls, and blocks an end while an agent that has handed back its report is still waiting on its own background work, its task not stopped; every other end passes, and on any error it is silent (`## What the task-leftover hook passes`). Its timeout is 10 seconds, provisional; past it, the turn ends unchecked.

A version-control pack may add its own `PreToolUse` guard on `Bash`, refusing a subagent's state-changing verbs and, where that pack says so, a call from any caller, the main thread included, such as a check-in on the main branch; that pack describes it. On its own error such a guard names the error, in a system message while the call takes the normal permission flow, or in an ask.

## What the agent guard refuses

Four things, checked in this order.

1. **A subagent's dispatch** — an `Agent` call whose payload carries a non-empty `agent_id`, so it comes from a subagent: agents never dispatch agents (`CONSTITUTION.md` `## The orchestration arrow`). The sentinel never lifts it; the agent is told to stop and report what it needed dispatched.
2. **A named call** — an `Agent` call carrying `name`, the teammate spawn form, which is outside the delegation contract (`.claude/references/core/delegation-contract.md`).
3. **A writing dispatch without a brief** — an `Agent` call to a roster agent, one with a file `.claude/agents/<subagent_type>.md`, whose `tools:` include `Write`, `Edit` or `Bash`, when its prompt names no existing brief: a path in the prompt, absolute or relative to the project root, in backticks or double quotes or bare — a bare one holding a space only if absolute, and on Windows an absolute one in the POSIX shell's drive form, a slash before the drive letter and no colon, read as that drive's path — that resolves, as the report filer resolves a brief, to an existing `.md` file under a `.scratch` directory whose name contains `brief`. A read-only roster agent, a built-in agent with no file, an agent file the guard cannot read and one with no `tools:` line are allowed. The sentinel never lifts it; the delegator is told to file the brief and name its path.
4. **An upgrade** — a dispatch whose `model` ranks above the baseline recorded in `.claude/agents/<subagent_type>.md`. A brief may downgrade an agent below its baseline, never upgrade. An agent with no file, with no `model:` key, or naming a tier the guard does not rank is allowed.

Each refusal's reason names its own remedy, so the fix arrives with the denial.

## What the write guard's hub memory rule refuses

A Write, Edit or NotebookEdit whose target lies strictly beneath the hub's `memory/` — the main checkout the `- Hub:` line of the `trees` section names — while that section is filled, from any session and its agents: that folder is written only through the base's `scripts/hub.py`. Paths are compared as text in Windows form, `..` resolved, either separator, any letter case, so a sibling such as `memory-old` lies outside. The reason names the target and the remedy: draft the change under this tree's `.scratch/` and apply it with `scripts/hub.py write --root <hub>`.

One exception passes: a session whose project directory is the hub, its agents included, while the hub is on a branch other than the main branch, read through the base's `scripts/hub.py` in one child under 5 seconds, so a slow read is refused before the 30-second timeout. Every other answer is refused as the main branch — no manifest, no `base`, no `hub.py` there, a refused read, an empty branch, a timeout, unreadable output — and a session whose project directory is not the hub, a branch tree's and its agents, is refused with no process started.

Every input it cannot read up to the comparison is silence, so a broken binding never blocks an edit: stdin, a target that is not drive-absolute, no project directory, a `trees` section absent or unreadable, no readable Hub path.

It does not catch a write made through Bash, the `cd` into the hub itself, or a target reached through a link; those rest on `.claude/references/core/trees-contract.md` `## The hub operation`. The main branch is the `vc` binding's *Main branch* slot, as `hub.py` reads it, with the backend's default where that slot is unbound; a binding it cannot read is refused. Under the exception, `handoff` writes its brief by Write, which this rule does not refuse.

## What the context-bound hook prints

Past the bound — the session's context strictly above its own bound, the *Hub context bound* for a hub session where that slot is filled, the *Context bound* otherwise — one line, as `hookSpecificOutput.additionalContext` at exit 0, on every tool call while it stays past. A hub session's name, read only past the lower of the two bounds, the *Context bound* and the *Hub context bound*, equals the binding's *Hub session name*, which its template writes first and in backticks; any other, a name missing or unread included, is a branch session. A branch session's line, until it hands off:

`context-bound: context <figure> tokens, past the session-launch Context bound of <bound> - finish the step in hand, then invoke handoff, its objective the next step`

A hub session's line:

`context-bound: context <figure> tokens, past the session-launch Context bound of <bound> - you are the hub session: file anything held only in context to disk, then carry on`

The bound is read at each call from the `session-launch` section of the bindings file the manifest's `bindings` filler names for `session-launch`, `core`'s by default: the first `<n>k` or bare integer on its `- Context bound:` line, so a changed bound takes effect at the next call. A hub session's bound is the first `<n>k` or bare integer on its `- Hub context bound:` line where that slot is filled, and the *Context bound* where it reads `none` or `TODO`, is absent or holds no number. The figure is the one the binding's *Context read* computes, read from the end of the payload's `transcript_path`: the newest assistant entry whose `input_tokens`, `cache_creation_input_tokens` and `cache_read_input_tokens` sum above 0, looked for in 64 KB blocks, at most 1 MB back.

It prints nothing, at exit 0: at or below the lower of the two bounds, before the session's name is read; at or below the session's own bound; for a subagent's call, whose payload carries a non-empty `agent_id`, before anything is read; where the *Context bound* reads `none` or `TODO`, is absent or holds no number; where the *Context read* reads `none` or `TODO` or is absent, so that slot stays the line's on/off switch and the human's manual read; where no such entry lies within the last 1 MB; and on any error. It is a nudge, not a guard: it refuses nothing, and the boundary a branch session hands off at stays its judgment.

After a `/clear`, the `transcript_path` the platform names may be the pre-clear session's file, so the line may come where this session's context is below the bound, or fail to come where it is past it. No check guards against it: the risk is an open item of deferral B152, until its probe settles the cause.

In `compact` mode, after a compaction, it prints for a hub session alone, as `additionalContext`, at exit 0, whatever the *Context bound* and *Context read* read:

`Standing orders after a compaction. You are this project's hub session. Run close-tree on report lines. If a skill was in flight, invoke it again now: its body may be cut. Re-read state from disk before you act; never trust the summary.`

Otherwise, in a subagent and on any error, nothing.

## What the stop-line hook passes

A stop line is the final text's last non-blank line, read with `*`, `_` and backticks ignored, each whitespace run collapsed to one space, in any letter case: `Stop: <kind>` or `Stop: <kind> — <detail>`, the dash an em dash, an en dash or a hyphen, spaced. The kinds are nine: `declared`, `gate`, `irreversible act`, `scope change`, `BLOCKED`, `background agents`, `answering the human`, `fork` and `handoff`.

`declared` needs a detail, and the detail, normalized the same way, its backticks and one final `.` dropped, must equal an entry of the frontmatter `stops:` of a composed `.claude/skills/*/SKILL.md`; otherwise the line reads as no line. A skill whose `stops:` is not an unquoted flow list, or whose SKILL.md cannot be read, is skipped, and the detail is matched against the other skills' entries.

Six kinds wait on the human: `declared`, `gate`, `irreversible act`, `scope change`, `BLOCKED` and `fork`; an end on `background agents`, `answering the human` or `handoff` passes before anything is read. An end on a valid line of a waiting kind, in skill work or conversation alike, is checked for the call alone: the hook reads the transcript back to the newest human prompt in the same blocks and cap, and the end passes where that window holds an assistant `tool_use` block named `AskUserQuestion`, whatever its result, since no result is read. A window reaching the prompt, the file's start or the 1 MB cap with no such call blocks: a call beyond the cap counts as none. An unreadable transcript passes.

With no valid line, the hook reads the transcript back to the newest human prompt, in 64 KB blocks, at most 1 MB. The end is skill work where that window holds a Skill call whose `input.skill`, after its last `:`, names a directory under `.claude/skills/`, or where that prompt's typed command names one; or by the carry, where the assistant text before that prompt closed on a stop line of a kind other than `answering the human` or `handoff`. Anything else, nothing found within the cap and an unreadable transcript included, is conversation, and the end passes.

A payload with a non-empty `agent_id`, a true `stop_hook_active` or no string `last_assistant_message` passes before anything is read. Skill work blocks: `{"decision": "block", "reason": …}` at exit 0, the reason "invoke the next skill, or `handoff` past the context bound or at a phase boundary"; for a hub session, "invoke the next skill, or end on a stop line; past the context bound, file anything held only in context to disk and carry on", a failed read of the name giving the first. A waiting line with no call blocks the same way, the reason "put this stop to the human through AskUserQuestion, its question stating the issue on its own, then end again; where AskUserQuestion is unavailable or refused, say so and end again". The end that follows a block passes, so an end is blocked once, by one check only. Any error is silence, and the end passes.

Elsewhere, a legitimate stop in skill work ended without the line costs one turn: the end is blocked once, and the next end passes. The reminder lines in `phase-pickup` and `implement` give the form and point here.

It leaves unchecked: whether the stop the line names is the true one, the end after a block, skill work its window misses, and the carry's one block per reply while the human chats after a stop in skill work; and, of the call, whether it put the stop the line names, an earlier call covering a later stop, a stop to the human on no line or an exempt kind, a result or another entry recorded as a human prompt after the call — a task notification or an interrupt marker, as observed, or a dismissal's result, as an unobserved one's might be — closing the window for one block, and, after a `/clear`, a `transcript_path` that may name the pre-clear file (deferral B152).

## What the task-leftover hook passes

A payload with a non-empty `agent_id`, a true `stop_hook_active`, or a `transcript_path` missing or unreadable passes, and any error is silence: the end passes.

It reads the whole transcript line by line, and parses only a line holding `task-notification` or `TaskStop`. A task notification is a `<task-notification>` element carried as an `attachment` entry's `attachment.prompt` or as a `user` entry's string content; of each `<task-id>`, only the newest notice counts. A `TaskStop` call is an assistant `tool_use` block named `TaskStop`, its `input.task_id` the task.

A task is left over when its newest notice's `<summary>` ends `is waiting on its own background work`, that notice's `<result>` says the report was delivered, naming `SubagentHandback`, and no `TaskStop` naming the task comes after that notice. One or more left over block the end: `{"decision": "block", "reason": …}` at exit 0, the reason `<id>[, <id>…]: an agent handed back with background work of its own still running (a call moved to the background at its timeout, or one it started); its report is in - stop each with TaskStop <id>, then end the turn`. The end that follows a block passes.

Everything else passes: an interim waiting notice whose `<result>` says the agent has not reported yet, as a `compile-check` wait still running gives; a later finished notice for the same task; a `TaskStop` on the task after the notice. It does not check that a `TaskStop` succeeded, nor a background shell the main thread started itself; `handoff` `## Where it goes` stops those.

## What the hooks rely on

The core hooks other than the task-leftover hook read `hook-common.py`, the shared readers beside them, and each takes its own internal-error path where it cannot be read.

Platform behaviour observed in a lab on 2026-09-29, against Claude Code 2.1.284; an upgrade of the platform may change any of it.

- A `PreToolUse` payload from a subagent carries `agent_id` and `agent_type` — the agent's name — and one from the main thread carries neither. The platform's hook documentation omits `agent_id` from `PreToolUse`; the lab saw it there.
- A `SubagentStop` payload carries `agent_id`, `agent_type`, `agent_transcript_path`, `last_assistant_message` — the agent's whole final message — and `stop_hook_active`, beside the common fields such as `cwd` and `session_id`, for foreground and background agents alike.
- Where agents hand back through a hand-back tool call, the report is that call's message, read from the agent's transcript.
- A hook entry's `"if"`, beside `type` and `command`, takes permission-rule syntax and limits when the hook starts at all. A prefix pattern of the form `Bash(<word> *)` misses the same command reached through a full path, an `.exe` suffix or an `env` wrapper; a pattern with `*` on both sides of the word catches each of those, and the hook's own parser then decides precisely. An `"if"` holds exactly one permission rule, so a second pattern takes its own handler. Its matching is case-sensitive, observed on 2026-10-05.
- Bash `allow`, `ask` and `deny` rules load from the project's `.claude/settings.json`. `Edit` allows there are ignored, and every path under `.claude/` is a sensitive file whose `Edit` or `Write` always asks, so the scratch area sits at `.scratch/`, outside `.claude/`.
- A `SubagentStop` hook that prints `{"decision":"block","reason":…}` at exit 0 keeps the agent running with the reason as its next input, and `stop_hook_active` is true on the stop that follows the block — which is how the filer blocks once only.
- Hook stdin is UTF-8 and is read as bytes: text-mode stdin on Windows decodes it as cp1252 and garbles `—`. Each hook's stdout JSON stays ASCII.

Observed in a probe on 2026-10-03, against Claude Code 2.1.288:

- A Write's or Edit's `tool_input.file_path` is absolute, in Windows form, and an Edit reaches `PreToolUse` before its own validation.
- The transcript at the payload's `transcript_path` records a Skill call as an assistant `tool_use` block with `input.skill`, and a typed slash command as a user entry whose string content holds the `<command-name>` marker, with no Skill `tool_use`; both are present at the next tool call's `PreToolUse`.
- A subagent's `PreToolUse` `transcript_path` names the parent session's transcript.

Observed in a probe on 2026-10-04, against Claude Code 2.1.289:

- A hook's `ask` prompts in auto mode too, and the prompt shows the hook's reason.

Observed in a live check on 2026-10-11, its Claude Code version not recorded, of the handoff guard and the hub memory guard, the two scripts the write guard's handoff and hub memory rules then were:

- `"${CLAUDE_PROJECT_DIR}"` in a hook command expands as the argument: the handoff guard and the hub memory guard each found the `trees` section through it.
- A hook's deny holds over an allow rule: a real `hub.py write … --brief …` call, which the allow rule for `scripts/hub.py write` allows, was refused by the handoff guard.
- A Write beneath the hub's `memory/` was refused by the hub memory guard, its reason naming the target and `scripts/hub.py write --root <hub>`.
- NotebookEdit's only path field is `tool_input.notebook_path`, and the hub memory guard, fed such a payload beneath the hub's `memory/`, denies it. A live NotebookEdit refused end to end was not observed: the tool's own read-before-edit check rejects a notebook not yet Read before any hook runs, and the tool edits only an existing notebook.
- A `hub.py write … --note <f>` committed with no prompt. The session ran in auto mode, so this does not by itself show the allow rule sparing the prompt.
- Not observable from outside: whether a Bash call holding no `--b` starts the handoff guard, silent either way, and the platform's default hook timeout; the entries without `"timeout"` ran to completion.

Observed in a probe on 2026-10-15, against Claude Code 2.1.300:

- On Windows a command hook with no `shell` key runs under `bash` — `MSYSTEM` `MINGW64` — for a `PreToolUse` and a `SubagentStop` hook alike. The platform's hook documentation says so too, and that PowerShell runs it where that `bash` is not installed, where the wrapped guard commands' POSIX `[ -f ]` test would not run; the wrapped commands rely on that `bash`, as this harness's other `bash` uses already do.
- `CLAUDE_PROJECT_DIR` reaches the hook as a Windows path with forward slashes (`D:/…`).
- The wrapped guard form exits 0 where neither file exists, and 2, `python3`'s "can't open file … No such file or directory" on stderr, where the manifest alone exists.
- A `SubagentStop` command that exits 2 with nothing on stdout and a missing-file stderr is a non-blocking error: the agent stops, its result returns, and the hook does not fire again. The documentation names the rule: at `Stop` and `SubagentStop`, exit 2 whose stderr says a file is missing is non-blocking; any other exit 2 blocks the stop.

Documented by the platform (https://code.claude.com/docs/en/hooks), not yet observed here — to be probed at the context-bound hook's first live check:

- A `PostToolUse` payload from a subagent carries `agent_id`, and one from the main thread carries none.
- A `PostToolUse` payload carries `transcript_path`, the session's transcript, which is written asynchronously and may trail by one entry.
- A `PostToolUse` hook that prints a `hookSpecificOutput` document whose `additionalContext` holds text, at exit 0, adds that text to the model's context beside the tool's result.

Observed in a live check on 2026-10-25, against Claude Code 2.1.310:

- A `Stop` hook that prints `{"decision":"block","reason":…}` at exit 0 keeps the turn going with the reason as its next input.
- The next end passes: `stop_hook_active` is true in that continuation, and stays true past a tool call made in it.
- `last_assistant_message` carries the final response's text with its last line: a valid last line passed untouched, and an invalid one blocked.
- The feedback enters the transcript as three entries: a `user` entry with `isMeta` true and the string content `Stop hook feedback:` followed by the reason, an `attachment` entry of type `hook_blocking_error`, and a `system` entry of subtype `stop_hook_summary` whose `hookErrors` holds the reason and whose `preventedContinuation` is false.
- A skill's body enters the transcript as a `user` entry with `isMeta` true after the Skill `tool_use`, and a tool's result as a `user` entry with a `toolUseResult` key and a `tool_result` block.
- A typed slash command enters as a `user` entry with no `isMeta` whose string content holds the `<command-name>` marker, followed by the skill's body as a `user` entry with `isMeta` true and no Skill `tool_use`; a turn end in that window is skill work.
- The hook runs in a `claude -p` session and leaves its stdout intact: a passing end prints the final response, and after a block stdout holds the continuation's final response.
- A running session picks up a `Stop` hook that `upgrade` adds to `.claude/settings.json`, from its next turn end.

Observed in a probe on 2026-10-26, against Claude Code 2.1.310:

- A running session re-reads the project's `.claude/settings.json` hooks when the file changes: after a mid-session switch onto a branch registering another `PreToolUse` hook, the next tool call ran that branch's hook, not the one loaded at start, and after a switch onto a branch with no `.claude/settings.json`, no hook ran on the calls that followed.

Documented by the platform (https://code.claude.com/docs/en/hooks), read on 2026-10-25, not yet observed here:

- The transcript at the payload's `transcript_path` may not yet hold the final response.
- Past eight blocks in a row, a count a tool call resets, the next block is overridden and the turn ends.
- A command that times out has its output discarded.
- `Stop` does not run when the user interrupts the turn.

Observed in a probe on 2026-10-28, its Claude Code version not recorded, of three answered AskUserQuestion calls in one session's transcript:

- An `AskUserQuestion` call is an `assistant` entry whose `message.content` holds a `tool_use` block named `AskUserQuestion`.
- Its answer is a `user` entry holding a `tool_result` block and a top-level `toolUseResult` holding `answers` and `questions`, which the stop-line hook reads as no human prompt.
- A dismissed or refused call's result was not observed, and the hook reads no result.

Observed in a live check on 2026-10-28, against Claude Code 2.1.312, of the stop-line hook's AskUserQuestion check in a running session:

- The replaced hook ran in the running session after `upgrade`, with no restart: the first end only it could block was blocked.
- An end on `Stop: gate` with no `AskUserQuestion` call since the newest human prompt was blocked once with the waiting line's reason, and the end that followed passed.
- An end on `Stop: gate` after an `AskUserQuestion` call in that window passed: the call's `tool_use` entry was in the transcript at the `Stop` read.
- The call was answered inside the turn, and the turn went on.
- A dismissed or refused call's result was not shown.

Observed in a lab on 2026-10-30, against Claude Code 2.1.314, `--bg --remote-control` sessions only (design `hub-compaction`):

- A `SessionStart` hook on the matcher `compact` ran at every boundary, and its `additionalContext` reached the model; reaching is not obedience, so safety rests on the skills' disk reads.

Read at rest on 2026-10-31, against Claude Code 2.1.314, on one machine's sample, undocumented, as observed:

- The name is the newest top-level `custom-title` (`customTitle`) or `agent-name` (`agentName`) line; a stretch over 1 MiB can lie between such lines, so a read stopped short of the file's start takes the head too.

Observed on 2026-11-04, against Claude Code 2.1.318, in one session and its agents' transcripts, and read at rest on 2026-11-05 in other sessions' transcripts:

- A subagent's Bash call that runs past its timeout is moved to the background ("Command did not complete within its 120s timeout and was moved to the background"), and keeps running after the agent hands back.
- Roster agents have no `TaskStop` tool: a call to it returns "No such tool available".
- Claude Code does not stop a returned agent's background work: the shell stayed a live `bash.exe` child of the session. The parent is sent a task notification, carried as an `attachment` entry whose `attachment.prompt` holds `<task-notification>…</task-notification>`, with `<task-id>`, `<status>`, `<summary>`, `<note>` and `<result>` elements: the `<summary>` reads `Agent "<description>" is waiting on its own background work`, and the `<result>` that the agent's report was delivered as a message (its SubagentHandback call). Other sessions' transcripts carry the same element as a `user` entry's string content, and an interim notice's `<result>` there says the agent has not reported yet. A finished agent's `<summary>` reads `Agent "<description>" finished`.
- The parent's `TaskStop` on the agent's task id stopped that agent's shell: its output file reads `[killed]`.

Observed in a probe on 2026-11-11, against Claude Code 2.1.322, on Windows with Git Bash installed:

- A `SessionStart` hook's stdout at exit 0 reaches the model's context, as `SessionStart:startup hook success:` followed by that stdout; a `SessionStart` hook that fails — exit 1, 2 or 127 — adds nothing to it.
- An entry with `"shell": "powershell"` runs its command under Windows PowerShell on a machine where Git Bash is installed too; Claude Code rewrote `${CLAUDE_PROJECT_DIR}` in such a command to `${env:CLAUDE_PROJECT_DIR}`.
- Windows PowerShell refuses a command that opens with `[` with the parse error `Missing type name after '['`, and runs none of it.

## Allowing named calls

Set `HARNESS_ALLOW_NAMED_AGENTS` to `1` in `.claude/settings.local.json`'s `env` block:

```json
{ "env": { "HARNESS_ALLOW_NAMED_AGENTS": "1" } }
```

Only the value `1`, surrounding whitespace aside, lifts the refusal; unset, empty, `0`, `true` and `yes` all leave it in force.

The sentinel lifts **the named-call refusal only**. The subagent-dispatch refusal and the model comparison are never gated by it: with the sentinel set, a named call proceeds, and a subagent's dispatch and an over-baseline dispatch are still denied.

## Turning the hooks off entirely

`"disableAllHooks": true` in `.claude/settings.local.json` is the blunt alternative, and the only switch for the platform checks, the report filer, the write guard, the stop-line hook, the task-leftover hook and any hook a composed pack adds, and for the context-bound hook the one beside its *Context read* slot (`## What the context-bound hook prints`), which its compact orders do not read — and its cost is the reason to prefer the sentinel for named calls: it disables every hook at once. The project's `doctor` report stops arriving, reports stop being filed and receipts and turn ends stop being checked, a machine the guards cannot run on stops being told so, and nothing says so; a report then stays in the agent's returned message (`.claude/references/core/delegation-contract.md` `## Filing`).

Hook *lists* merge, and a local settings file cannot remove one hook from a list, so there is no narrower switch than these two: the sentinel for the agent guard's named-call refusal, `disableAllHooks` for every hook.

## Where `py` is not a working interpreter

Every hook command but the two platform checks runs `py`, directly or through a pack's `bash` shim that pipes to it, and `py` finds Python by the registry, not `PATH`. A machine with no `py` (a Store Python, macOS, Linux) gets none of the hooks that run it: the doctor hook fails silently, so no report arrives; the agent guard fails with a visible but non-blocking error on each `Agent` dispatch, denying nothing; the report filer fails the same way at each agent's stop, refusing and filing nothing; the write guard fails the same way on each Write, Edit and NotebookEdit, refusing nothing; the context-bound hook fails the same way after each tool call and compaction, printing nothing; the stop-line hook and the task-leftover hook fail the same way at each turn end, blocking nothing; and a version-control pack's guard, where one is composed, fails the same way, refusing nothing. None stops a session, and none can refuse a call — the cost of the missing interpreter is a rule that stops being enforced and reports that stop being filed. The fix is Python 3 from python.org, which carries `py`. On such a machine the first platform check, which needs no Python, puts one line into Claude's context — naming `py` on Windows, the platform elsewhere — so the loss is told, not silent.

The wrapped commands — core's two (the agent guard and the write guard) on `PreToolUse`, its one on `PostToolUse` (the context-bound hook), its one on `SessionStart` (its compact orders) and its two on `Stop` (the stop-line hook and the task-leftover hook), six in all, and a version-control pack's guard, where one is composed — first test two files. Where the project directory holds neither `.claude/harness.json` nor the hook's script — a checkout from before the harness, reached while the registration still stands, since a running session re-reads `.claude/settings.json`'s hooks when the file changes and runs none after a switch onto a branch with no settings file (the probe of 2026-10-26) — the command exits 0 and prints nothing, so it refuses no call. Where the manifest is present and the script is not, `py` cannot open the script and exits 2, which refuses a `PreToolUse` call: a broken composition fails closed; the context-bound hook's exit 2 comes after its call has run, so it refuses nothing, and the compact orders' is shown and the session continues, as the doctor hook's; the stop-line hook's and the task-leftover hook's exit 2, its stderr naming the missing script, is a non-blocking error at a turn end, by the documented rule the probe of 2026-10-15 records, so the turn ends as usual. The doctor hook and the report filer keep the bare command: on such a checkout the doctor hook's exit 2 is shown and the session continues, and the report filer's exit 2, its stderr naming the missing script, is a non-blocking error at an agent's stop, so the agent stops as usual (`## What the hooks rely on`, the probe of 2026-10-15). A version-control pack's `WorktreeCreate` hook is parked: no settings template registers it while writer trees are parked. Registered, it would keep the bare command too: there exit 2 aborts the tree's creation, as the hook itself refuses wherever `trees` reads absent.
