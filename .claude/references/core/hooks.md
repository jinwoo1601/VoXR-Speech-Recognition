# Hooks

`core` declares five hooks in its settings template, so every project composed from `core` runs all five. This page says what they do, what they rely on, and how to turn each off.

## What fires, and when

- **The session-start hook** — `.claude/references/core/doctor-hook.py`, on `SessionStart` with the matcher `startup|resume|clear`. It runs `harness.py doctor` for the project and relays the report into the session's context: the manifest / base / packs banner, each failure and warning row, and the `doctor: PASS` or `doctor: FAIL` line. Where the `session-launch` binding names a List command, the hook also warns (`HUB`) when two or more running sessions carry the Hub session name, or when that name is unbound; this reaches only sessions on this machine. Outside a harness project — no `.claude/harness.json` — it prints nothing. It never fails a session: whatever the child returns, the hook exits 0. `compact` and `fork` are deliberately not matched, because the context they carry forward already holds the report.
- **The agent guard** — `.claude/references/core/agent-guard-hook.py`, on `PreToolUse` with the matcher `Agent`. It inspects every `Agent` dispatch and nothing else; on any other tool the platform starts no process at all. A dispatch it permits gets silence, never an explicit allow.
- **The report filer** — `.claude/references/core/report-filer-hook.py`, on `SubagentStop` with no matcher, so it runs whenever an agent stops, foreground or background. It files the agent's final message: beside the brief the message's first line names — a `.md` file under a `.scratch/` directory — as `<brief name without -brief.md>-report.md`, numbered `-2`, `-3` for further answers to the same brief and never overwriting one; on any other first line, under `.scratch/reports/` as `<agent type>-<agent id>-report.md`. Where the agent's transcript holds a recon agent's `# Full report —` message, that text is filed ahead of the final message. For a harness agent — one with a file under `.claude/agents/` — it also checks the receipt: the first line `<STATUS> — <brief path or task>`, and, on `DONE` from an agent whose file declares the one-line `Trailer:`, a trailer line naming criteria, files and verification. Its one refusal: a malformed receipt keeps the agent running once, with the remedy as its next input, and files nothing; the stop that follows is filed whatever it holds, with a system message naming the defect. Built-in agents are filed, never checked. A report it files without a defect, it files in silence.
- **The handoff guard** — `.claude/references/core/handoff-guard-hook.py`, on `PreToolUse` with the matcher `Write|Edit|SendMessage`. It watches a Write or Edit under a `memory/handoffs/` directory and a SendMessage beginning `HANDOFF -`; every other call gets silence, with no transcript read.
- **The rm fence** — `.claude/references/core/rm-fence-hook.py`, on `PreToolUse` with the matcher `Bash`, as four handlers running one command, each with its own `"if"` — `"Bash(*rm*)"`, `"Bash(*rM*)"`, `"Bash(*Rm*)"` and `"Bash(*RM*)"` — so a call whose text holds no `rm` in any letter case starts no process. It reads each `rm` and `rmdir` in the call. Its timeout is 10 seconds, provisional; past it, the call proceeds unchecked.

A version-control pack may add its own `PreToolUse` guard on `Bash` for subagents; that pack describes it.

## What the agent guard refuses

Two things, checked in this order.

1. **A named call** — an `Agent` call carrying `name`, the teammate spawn form, which is outside the delegation contract (`.claude/references/core/delegation-contract.md`).
2. **An upgrade** — a dispatch whose `model` ranks above the baseline recorded in `.claude/agents/<subagent_type>.md`. A brief may downgrade an agent below its baseline, never upgrade. An agent with no file, with no `model:` key, or naming a tier the guard does not rank is allowed.

Each refusal's reason names its own remedy, so the fix arrives with the denial.

## What the handoff guard refuses

A Write or Edit under a `memory/handoffs/` directory, and a SendMessage beginning `HANDOFF -`, unless this session's transcript shows `handoff` invoked: the Skill tool naming `handoff`, or the human's typed `/handoff` command. A subagent's watched call is refused outright, and a missing, unreadable or non-UTF-8 transcript refuses a watched call. Each reason names `handoff` as the remedy.

It does not catch a brief written through Bash, or a session that invokes `handoff` and then writes by hand. After a clear, `handoff` must run again. A Skill call and its Write in one message may meet a transcript that does not yet hold the Skill entry, and be refused; the same call a turn later passes.

## What the rm fence refuses

A delete whose target lies outside its roots:

- the project directory Claude Code passes it;
- `.scratch/` beneath it;
- the trees folder, from the `- Location:` line of the first bindings file holding a `trees` section — none where that section reads absent;
- the `TMP` and `TEMP` folders of the hook's environment.

Each root is real-path resolved, and a drive root is dropped. A target is inside only when it lies strictly beneath a root, links followed; a root itself is outside. A target it cannot pin — a substitution, an unknown variable, a pattern it cannot expand, nested shell text such as `sh -c` or a heredoc, a spelling it cannot read — counts as outside.

- **No target outside** — silence, the normal flow.
- **Outside, in a chained call** — any segment of the call not an `rm` or `rmdir`: deny, with the reason `rm-fence: run the delete as its own command`.
- **Outside, alone** — every segment a delete: ask, listing each outside target, resolved or marked `(unresolved)`.
- **Unreadable input, or its own error** — ask, naming it.

It checks the main thread and agents alike. It guards against model error, beside Claude Code's critical-path breaker; it is not a security boundary. `xargs rm`, `find -delete`, `unlink` and other disguised deletes are left to the classifier.

## What the hooks rely on

Platform behaviour observed in a lab on 2026-09-29, against Claude Code 2.1.284; an upgrade of the platform may change any of it.

- A `PreToolUse` payload from a subagent carries `agent_id` and `agent_type` — the agent's name — and one from the main thread carries neither. The platform's hook documentation omits `agent_id` from `PreToolUse`; the lab saw it there.
- A `SubagentStop` payload carries `agent_id`, `agent_type`, `agent_transcript_path`, `last_assistant_message` — the agent's whole final message — and `stop_hook_active`, beside the common fields such as `cwd` and `session_id`, for foreground and background agents alike.
- Where agents hand back through a hand-back tool call, the report is that call's message, read from the agent's transcript.
- A hook entry's `"if"`, beside `type` and `command`, takes permission-rule syntax and limits when the hook starts at all. A prefix pattern of the form `Bash(<word> *)` misses the same command reached through a full path, an `.exe` suffix or an `env` wrapper; a pattern with `*` on both sides of the word catches each of those, and the hook's own parser then decides precisely. An `"if"` holds exactly one permission rule, so a second pattern takes its own handler. Its matching is case-sensitive, observed on 2026-10-05.
- Bash `allow`, `ask` and `deny` rules load from the project's `.claude/settings.json`. `Edit` allows there are ignored, and every path under `.claude/` is a sensitive file whose `Edit` or `Write` always asks, so the scratch area sits at `.scratch/`, outside `.claude/`.
- A `SubagentStop` hook that prints `{"decision":"block","reason":…}` at exit 0 keeps the agent running with the reason as its next input, and `stop_hook_active` is true on the stop that follows the block — which is how the filer blocks once only.
- Hook stdin is UTF-8 and is read as bytes: text-mode stdin on Windows decodes it as cp1252 and garbles `—`. Each hook's stdout JSON stays ASCII.

Observed in a probe on 2026-10-03, against Claude Code 2.1.288:

- A SendMessage call reaches `PreToolUse`, its full text in `tool_input.message`; `tool_input.content` is a truncated copy.
- A Write's or Edit's `tool_input.file_path` is absolute, in Windows form, and an Edit reaches `PreToolUse` before its own validation.
- The transcript at the payload's `transcript_path` records a Skill call as an assistant `tool_use` block with `input.skill`, and a typed slash command as a user entry whose string content holds the `<command-name>` marker, with no Skill `tool_use`; both are present at the next tool call's `PreToolUse`.
- A subagent's `PreToolUse` `transcript_path` names the parent session's transcript.

Observed in a probe on 2026-10-04, against Claude Code 2.1.289:

- A hook's `ask` prompts in auto mode too, and the prompt shows the hook's reason.

## Allowing named calls

Set `HARNESS_ALLOW_NAMED_AGENTS` to `1` in `.claude/settings.local.json`'s `env` block:

```json
{ "env": { "HARNESS_ALLOW_NAMED_AGENTS": "1" } }
```

Only the exact value `1` lifts the refusal; unset, empty, `0`, `true` and `yes` all leave it in force.

The sentinel lifts **the named-call refusal only**. The model comparison is never gated by it: with the sentinel set, a named call proceeds and an over-baseline dispatch is still denied.

## Turning the hooks off entirely

`"disableAllHooks": true` in `.claude/settings.local.json` is the blunt alternative, and the only switch for the report filer, the handoff guard, the rm fence and any hook a composed pack adds — and its cost is the reason to prefer the sentinel for named calls: it disables every hook at once. The project's `doctor` report stops arriving, reports stop being filed and receipts stop being checked, and nothing says so; the delegator then files by hand (`.claude/references/core/delegation-contract.md` `## Filing`).

Hook *lists* merge, and a local settings file cannot remove one hook from a list, so there is no narrower switch than these two: the sentinel for the agent guard's named-call refusal, `disableAllHooks` for every hook.

## Where `python3` is not a working interpreter

Every hook command starts with `python3`, the name this harness already requires of a machine everywhere else it needs Python. A project whose `python3` does not resolve to a real interpreter gets none of the hooks: the doctor hook fails silently, so no report arrives; the agent guard fails with a visible but non-blocking error on each `Agent` dispatch, denying nothing; the report filer fails the same way at each agent's stop, refusing and filing nothing; the handoff guard fails the same way on each Write, Edit and SendMessage, refusing nothing; the rm fence fails the same way on each Bash call naming `rm`, refusing nothing; and a version-control pack's guard, where one is composed, fails the same way, refusing nothing. None stops a session, and none can refuse a call — the cost of the missing interpreter is a rule that stops being enforced and reports that stop being filed. The fix is to make `python3` resolve to a real Python 3 on the machine's `PATH`, which is the same thing this harness's own verification commands need.
