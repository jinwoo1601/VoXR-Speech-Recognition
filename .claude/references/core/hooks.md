# Hooks

`core` declares three hooks in its settings template, so every project composed from `core` runs all three. This page says what they do, what they rely on, and how to turn each off.

## What fires, and when

- **The session-start hook** — `.claude/references/core/doctor-hook.py`, on `SessionStart` with the matcher `startup|resume|clear`. It runs `harness.py doctor` for the project and relays the report into the session's context: the manifest / base / packs banner, each failure and warning row, and the `doctor: PASS` or `doctor: FAIL` line. Where the `session-launch` binding names a List command, the hook also warns (`HUB`) when two or more running sessions carry the Hub session name, or when that name is unbound; this reaches only sessions on this machine. Outside a harness project — no `.claude/harness.json` — it prints nothing. It never fails a session: whatever the child returns, the hook exits 0. `compact` and `fork` are deliberately not matched, because the context they carry forward already holds the report.
- **The agent guard** — `.claude/references/core/agent-guard-hook.py`, on `PreToolUse` with the matcher `Agent`. It inspects every `Agent` dispatch and nothing else; on any other tool the platform starts no process at all. A dispatch it permits gets silence, never an explicit allow.
- **The report filer** — `.claude/references/core/report-filer-hook.py`, on `SubagentStop` with no matcher, so it runs whenever an agent stops, foreground or background. It files the agent's final message: beside the brief the message's first line names — a `.md` file under a `.scratch/` directory — as `<brief name without -brief.md>-report.md`, numbered `-2`, `-3` for further answers to the same brief and never overwriting one; on any other first line, under `.scratch/reports/` as `<agent type>-<agent id>-report.md`. Where the agent's transcript holds a recon agent's `# Full report —` message, that text is filed ahead of the final message. For a harness agent — one with a file under `.claude/agents/` — it also checks the receipt: the first line `<STATUS> — <brief path or task>`, and, on `DONE` from an agent whose file declares the one-line `Trailer:`, a trailer line naming criteria, files and verification. Its one refusal: a malformed receipt keeps the agent running once, with the remedy as its next input, and files nothing; the stop that follows is filed whatever it holds, with a system message naming the defect. Built-in agents are filed, never checked. A report it files without a defect, it files in silence.

A version-control pack may add its own `PreToolUse` guard on `Bash` for subagents; that pack describes it.

## What the guard refuses

Two things, checked in this order.

1. **A named call** — an `Agent` call carrying `name`, the teammate spawn form, which is outside the delegation contract (`.claude/references/core/delegation-contract.md`).
2. **An upgrade** — a dispatch whose `model` ranks above the baseline recorded in `.claude/agents/<subagent_type>.md`. A brief may downgrade an agent below its baseline, never upgrade. An agent with no file, with no `model:` key, or naming a tier the guard does not rank is allowed.

Each refusal's reason names its own remedy, so the fix arrives with the denial.

## What the hooks rely on

Platform behaviour observed in a lab on 2026-09-29, against Claude Code 2.1.284; an upgrade of the platform may change any of it.

- A `PreToolUse` payload from a subagent carries `agent_id` and `agent_type` — the agent's name — and one from the main thread carries neither. The platform's hook documentation omits `agent_id` from `PreToolUse`; the lab saw it there.
- A `SubagentStop` payload carries `agent_id`, `agent_type`, `agent_transcript_path`, `last_assistant_message` — the agent's whole final message — and `stop_hook_active`, beside the common fields such as `cwd` and `session_id`, for foreground and background agents alike.
- Where agents hand back through a hand-back tool call, the report is that call's message, read from the agent's transcript.
- A hook entry's `"if"`, beside `type` and `command`, takes permission-rule syntax and limits when the hook starts at all. A prefix pattern of the form `Bash(<word> *)` misses the same command reached through a full path, an `.exe` suffix or an `env` wrapper; a pattern with `*` on both sides of the word catches each of those, and the hook's own parser then decides precisely.
- Bash `allow`, `ask` and `deny` rules load from the project's `.claude/settings.json`. `Edit` allows there are ignored, and every path under `.claude/` is a sensitive file whose `Edit` or `Write` always asks, so the scratch area sits at `.scratch/`, outside `.claude/`.
- A `SubagentStop` hook that prints `{"decision":"block","reason":…}` at exit 0 keeps the agent running with the reason as its next input, and `stop_hook_active` is true on the stop that follows the block — which is how the filer blocks once only.
- Hook stdin is UTF-8 and is read as bytes: text-mode stdin on Windows decodes it as cp1252 and garbles `—`. Each hook's stdout JSON stays ASCII.

## Allowing named calls

Set `HARNESS_ALLOW_NAMED_AGENTS` to `1` in `.claude/settings.local.json`'s `env` block:

```json
{ "env": { "HARNESS_ALLOW_NAMED_AGENTS": "1" } }
```

Only the exact value `1` lifts the refusal; unset, empty, `0`, `true` and `yes` all leave it in force.

The sentinel lifts **the named-call refusal only**. The model comparison is never gated by it: with the sentinel set, a named call proceeds and an over-baseline dispatch is still denied.

## Turning the hooks off entirely

`"disableAllHooks": true` in `.claude/settings.local.json` is the blunt alternative, and the only switch for the report filer and for any hook a composed pack adds — and its cost is the reason to prefer the sentinel for named calls: it disables every hook at once. The project's `doctor` report stops arriving, reports stop being filed and receipts stop being checked, and nothing says so; the delegator then files by hand (`.claude/references/core/delegation-contract.md` `## Filing`).

Hook *lists* merge, and a local settings file cannot remove one hook from a list, so there is no narrower switch than these two: the sentinel for the guard's named-call refusal, `disableAllHooks` for every hook.

## Where `python3` is not a working interpreter

Every hook command starts with `python3`, the name this harness already requires of a machine everywhere else it needs Python. A project whose `python3` does not resolve to a real interpreter gets none of the hooks: the doctor hook fails silently, so no report arrives; the agent guard fails with a visible but non-blocking error on each `Agent` dispatch, denying nothing; the report filer fails the same way at each agent's stop, refusing and filing nothing; and a version-control pack's guard, where one is composed, fails the same way, refusing nothing. None stops a session, and none can refuse a call — the cost of the missing interpreter is a rule that stops being enforced and reports that stop being filed. The fix is to make `python3` resolve to a real Python 3 on the machine's `PATH`, which is the same thing this harness's own verification commands need.
