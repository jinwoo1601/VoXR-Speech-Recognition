# Hooks

`core` declares seven hooks in its settings template, so every project composed from `core` runs all seven. This page says what they do, what they rely on, and how to turn each off.

## What fires, and when

- **The session-start hook** — `.claude/references/core/doctor-hook.py`, on `SessionStart` with the matcher `startup|resume|clear`. It runs `harness.py doctor` for the project — the manifest's `base` resolved against the project root, and from a branch tree where that names no directory, against the main checkout the `trees` binding's Hub slot names, as `harness.py` does — and relays the report into the session's context: the manifest / base / packs banner, each failure and warning row, and the `doctor: PASS` or `doctor: FAIL` line. It starts `harness.py` as bare `py`, with an active venv's `VIRTUAL_ENV` and its `PATH` entries dropped from the child's environment, so the script's shebang picks the registered Python, and falls back to the hook's own interpreter where there is no `py`; a child that prints no report and exits non-zero gives one `DOCTOR` warning line carrying its first stderr line. Where the `session-launch` binding names a List command, the hook also warns (`HUB`) when two or more running sessions carry the Hub session name, or when that name is unbound; this reaches only sessions on this machine. Outside a harness project — no `.claude/harness.json` — it prints nothing. It never fails a session: whatever the child returns, the hook exits 0. `compact` and `fork` are deliberately not matched, because the context they carry forward already holds the report.
- **The agent guard** — `.claude/references/core/agent-guard-hook.py`, on `PreToolUse` with the matcher `Agent`. It inspects every `Agent` dispatch and nothing else; on any other tool the platform starts no process at all. A dispatch it permits gets silence, never an explicit allow.
- **The report filer** — `.claude/references/core/report-filer-hook.py`, on `SubagentStop` with no matcher, so it runs whenever an agent stops, foreground or background. It files the agent's final message: beside the brief the message's first line names — a `.md` file under a `.scratch/` directory — as `<brief name without -brief.md>-report.md`, numbered `-2`, `-3` for further answers to the same brief and never overwriting one; on any other first line, under `.scratch/reports/` as `<agent type>-<agent id>-report.md`. Where the agent's transcript holds a recon agent's `# Full report —` message, that text is filed ahead of the final message. For a harness agent — one with a file under `.claude/agents/` — it also checks the receipt: the first line `<STATUS> — <brief path or task>`, and, on `DONE` from an agent whose file declares the one-line `Trailer:`, a trailer line naming criteria, files and verification. Its one refusal: a malformed receipt keeps the agent running once, with the remedy as its next input, and files nothing; the stop that follows is filed whatever it holds, with a system message naming the defect. Built-in agents are filed, never checked. A report it files without a defect, it files in silence.
- **The write guard** — `.claude/references/core/write-guard-hook.py`, on `PreToolUse` twice, given the project directory as its argument: with the matcher `Write|Edit|NotebookEdit|SendMessage`, and with the matcher `Bash` and `"if": "Bash(*--b*)"`, so a Bash call whose text holds no `--b` starts no process. One process holds three rules, and the first that refuses decides: the handoff rule, the hub memory rule and the locked-doc rule. The handoff rule watches a SendMessage beginning `HANDOFF -` and, by the project's `trees` section, a Bash call placing a brief or a Write or Edit under a `memory/handoffs/` directory; every call not watched by its shape gets silence, with no file and no transcript read. The hub memory rule watches a Write, Edit or NotebookEdit aimed beneath the hub's `memory/` where the `trees` section is filled; a path holding no `memory` component gets silence, with no file read. The locked-doc rule watches a Write or Edit of a `.md` file. Its timeout on the first entry is 10 seconds, provisional; past it, the call proceeds unchecked.
- **The rm fence** — `.claude/references/core/rm-fence-hook.py`, on `PreToolUse` with the matcher `Bash`, as four handlers running one command, each with its own `"if"` — `"Bash(*rm*)"`, `"Bash(*rM*)"`, `"Bash(*Rm*)"` and `"Bash(*RM*)"` — so a call whose text holds no `rm` in any letter case starts no process. It reads each `rm` and `rmdir` in the call. Its timeout is 10 seconds, provisional; past it, the call proceeds unchecked. Within that, the expansion of the call's pattern targets has a walk budget of 5 seconds, also provisional: a pattern still expanding when it is spent, and every pattern after it, is unresolved, so the call stops.
- **The context-bound hook** — `.claude/references/core/context-bound-hook.py`, on `PostToolUse` with no matcher, so it runs after every tool call, given the project directory as its argument. In the main thread, once the session's context passes the `session-launch` binding's *Context bound*, it puts one line into the model's context on every call; at or below the bound, in a subagent and on any error it is silent (`## What the context-bound hook prints`). Its timeout is 5 seconds, provisional; past it, the tool's result stands and no line is added.
- **The stop-line hook** — `.claude/references/core/stop-line-hook.py`, on `Stop` with no matcher, so it runs at each main-thread turn end, given the project directory as its argument. It passes an end whose final text closes on a stop line and, in skill work, blocks any other end once; in conversation and on any error it is silent (`## What the stop-line hook passes`). Its timeout is 10 seconds, provisional; past it, the turn ends unchecked.

A version-control pack may add its own `PreToolUse` guard on `Bash` for subagents; that pack describes it.

## What the agent guard refuses

Four things, checked in this order.

1. **A subagent's dispatch** — an `Agent` call whose payload carries a non-empty `agent_id`, so it comes from a subagent: agents never dispatch agents (`CONSTITUTION.md` `## The orchestration arrow`). The sentinel never lifts it; the agent is told to stop and report what it needed dispatched.
2. **A named call** — an `Agent` call carrying `name`, the teammate spawn form, which is outside the delegation contract (`.claude/references/core/delegation-contract.md`).
3. **A writing dispatch without a brief** — an `Agent` call to a roster agent, one with a file `.claude/agents/<subagent_type>.md`, whose `tools:` include `Write`, `Edit` or `Bash`, when its prompt names no existing brief: a path in the prompt, absolute or relative to the project root, in backticks or double quotes or bare — a bare one holding a space only if absolute, and on Windows an absolute one in the POSIX shell's drive form, a slash before the drive letter and no colon, read as that drive's path — that resolves, as the report filer resolves a brief, to an existing `.md` file under a `.scratch` directory whose name contains `brief`. A read-only roster agent, a built-in agent with no file, an agent file the guard cannot read and one with no `tools:` line are allowed. The sentinel never lifts it; the delegator is told to file the brief and name its path.
4. **An upgrade** — a dispatch whose `model` ranks above the baseline recorded in `.claude/agents/<subagent_type>.md`. A brief may downgrade an agent below its baseline, never upgrade. An agent with no file, with no `model:` key, or naming a tier the guard does not rank is allowed.

Each refusal's reason names its own remedy, so the fix arrives with the denial.

## What the write guard's handoff rule refuses

Its watch follows the `trees` section beneath the project directory it is given. Filled: a Bash call placing a brief — `hub.py write` with `--brief`, or any prefix of it from `--b`, alone or with `=<file>`, by any interpreter or path form, in a chained call, inside a `bash -c` string or across a line continuation — is watched, and a Write or Edit under a `memory/handoffs/` directory is not. Absent, unreadable, or no argument: the reverse. A SendMessage beginning `HANDOFF -` is watched in both.

A watched call passes when this session's transcript shows `handoff` invoked: the Skill tool naming `handoff` or a directory-scoped `<scope>:handoff`, or the human's typed `/handoff` command (its scoped form `/<scope>:handoff` accepted, unprobed). A subagent's watched call is refused outright, and a missing, unreadable or non-UTF-8 transcript refuses a watched call. Each reason names `handoff` as the remedy.

It does not catch a brief written through Bash where `trees` is absent, or placed other than by `hub.py write` where it is filled, nor a session that invokes `handoff` and then writes by hand. After a clear, `handoff` must run again. A Skill call and its watched call in one message may meet a transcript that does not yet hold the Skill entry, and be refused; the same call a turn later passes.

A quoted mention of the placement is refused as one: a word holding whitespace is read again as a command, so `hub.py`, `write` and a `--b…` option spelled inside a commit message given by `-m "…"`, a `grep "…"` pattern or an interpreter's `-c` string count as a placement — refused without `handoff` from the main thread, and outright from a subagent. It errs on the safe side, kept by ruling on 2026-10-11; reword the mention, or invoke `handoff`.

## What the write guard's hub memory rule refuses

A Write, Edit or NotebookEdit whose target lies strictly beneath the hub's `memory/` — the main checkout the `- Hub:` line of the `trees` section names — while that section is filled, from any session and its agents: that folder is written only through the base's `scripts/hub.py`. Paths are compared as text in Windows form, `..` resolved, either separator, any letter case, so a sibling such as `memory-old` lies outside. The reason names the target and the remedy: draft the change under this tree's `.scratch/` and apply it with `scripts/hub.py write --root <hub>`.

One exception passes: a session whose project directory is the hub, its agents included, while the hub is on a branch other than the main branch, read through the base's `scripts/hub.py` in one child under 5 seconds, so a slow read is refused before the 10-second timeout. Every other answer is refused as the main branch — no manifest, no `base`, no `hub.py` there, a refused read, an empty branch, a timeout, unreadable output — and a session whose project directory is not the hub, a branch tree's and its agents, is refused with no process started.

Every input it cannot read up to the comparison is silence, so a broken binding never blocks an edit: stdin, a target that is not drive-absolute, no project directory, a `trees` section absent or unreadable, no readable Hub path.

It does not catch a write made through Bash, the `cd` into the hub itself, or a target reached through a link; those rest on `.claude/references/core/trees-contract.md` `## The hub operation`. A project whose main branch is not the backend's default passes `--main` to `hub.py write`, which the guard cannot see, so the hub's own session on that main branch passes. Under the exception, `handoff` writes its brief by Write, which neither rule refuses.

## What the write guard's locked-doc rule refuses

Off a `design-` branch, a Write or Edit of a `.md` file that sets a locked doc's lock or edits a locked doc: a Write whose `content` opens `---` and whose front matter holds a line reading `status: locked`, quoted or not; an Edit whose `new_string` holds such a line and whose `old_string` holds none; and a Write or Edit whose target's current front matter on disk already holds one, read from the file's head in the hook's own process. A doc is locked only on a `design-` branch at G1 (`g1-lock` LOCK), and a locked doc changes only there; the reason names the target and the branch.

The branch is read only on such a hit, through the base's `scripts/hub.py` in the child the hub memory rule uses, from the project directory, and at most once per call, whichever rules ask for it; its last `/` component starting `design-` passes, so a nested branch name `main/design-x` passes as a flat `design-x` does. A failed or empty read — no manifest, no `hub.py`, a refused read, a detached head, a timeout, unreadable output, or no project directory — is refused, the reason naming the failed read.

A target that is not a `.md` file, a NotebookEdit, a SendMessage, a body line such as `**Status: locked**` and a lock spelled only in the body are not read. It does not catch a write made through Bash, a feature-requirement doc, whose status reads `accepted` and never `locked`, or a locked doc written on a `design-` branch other than its own.

## What the rm fence refuses

A delete whose target lies outside its roots:

- the project directory Claude Code passes it;
- `.scratch/` beneath it;
- the trees folder, from the `- Location:` line of the `trees` section in the bindings file the manifest's `bindings` filler names for `trees` (`core`'s by default) — none where that section reads absent, or where the manifest cannot be read;
- the `TMP`, `TEMP` and `CLAUDE_JOB_DIR` folders of the hook's environment, `/tmp` read as the first of `TMP` and `TEMP`; the job folder `CLAUDE_JOB_DIR` names is a root whole, and there is none where the hook's environment holds no `CLAUDE_JOB_DIR`, so a job path then stops.

Each root is real-path resolved, and a drive root is dropped. A target is inside only when it lies strictly beneath a root, links followed; a root itself is outside. A target it cannot pin — a substitution other than the pins below, an unknown variable, a pattern it cannot expand (past 1000 matches across the call, or past the walk budget), nested shell text such as `sh -c` or a heredoc, a spelling it cannot read — counts as outside. Three forms pin a target, each pinned path still tested against the roots: `cygpath -u`, `-w`, `-m` or `-a` of one word stands for that word, and the one word may itself be a `mktemp` substitution (`cygpath -w "$(mktemp -d)"`); `mktemp` in any form — in `$(…)` or backticks, assigned or as the target itself, with `-p`/`--tmpdir`, a template, `-t` or `--suffix` — stands for a fresh name in its folder: the `-p`/`--tmpdir` folder, else its template's, else the temp folder, so `mktemp -p` into a folder outside the roots still stops; and `${NAME:-WORD}` gives NAME's value where NAME is set and not empty, and is unpinned otherwise. A body under a quoted delimiter word (`'EOF'`, `"EOF"`, `\EOF`) is data, whoever receives it and whatever else the call runs, unless a runner — one of rule 3's sixteen, below — receives it, as its command word or down the pipe, loop or group its receiver hands it to, or through a `>(…)` process substitution in those segments (`bash <<'EOF'`, `cat <<'EOF' | bash`, `tee >(bash) <<'EOF'`); there a command word it cannot resolve and a function defined earlier in the call count as runners too (`cat <<'EOF' | $SH`, `f <<'EOF'`); its `rm` words do not count toward rule 3's surplus. A body under an unquoted word is data only where a note-writer — `cat` or `tee` — receives it and no runner is in the call, and is then searched only in the `$(…)` and backtick substitutions it expands; any other receiver, a word the fence does not know included, reads it as commands. A heredoc operator written straight after another operator, as in `)<<` or `>&<<`, is read as commands whatever receives it.

It reads a call only when the call holds an `rm` word: a word equal to `rm` or `rmdir` in any letter case once the call's text — quoted text, substitutions and heredoc bodies included, a comment outside them not unless `${`, `$[` or `[[` is written before it — is split at whitespace, the operators `;&|()<>`, `=` and the backtick, and each part has its quotes, a leading directory path and a trailing `.exe` removed. `--rm`, `rmtree`, `rm-cache` and `rm.log` are not `rm` words; a call holding none gets silence and is not walked.

Three class rules find its deletes:

1. **The command word.** `rm` or `rmdir` as a segment's command word, read past leading assignments, redirections and keywords such as `if` and `then`, past the wrappers `command`, `exec`, `time`, `nohup`, `builtin`, `noglob`, `env`, `sudo`, `busybox`, `timeout`, `nice` and `stdbuf` with their options and `timeout`'s duration, and through a variable the call sets (`x=rm; $x -rf a`), is a delete whose targets it resolves.
2. **An unresolvable command word.** A command word whose last path component, the call's variables applied, still holds a `$`, a substitution, a glob character or a brace list or range, `[` and `[[` excepted — `$(which rm) -rf a` — is a delete of its segment from that word on, unpinned, and does not chain the call.
3. **A runner.** A runner as the command word of any segment, nested text included, adds one delete, unpinned, listed as the first runner's segment; a runner's segment in the command line itself chains the call, so `xargs rm < list` and `echo 'rm -rf a' | bash` are denied. The runners are sixteen: `sh`, `bash`, `dash`, `zsh`, `ksh`, `mksh`, `ash`, `cmd`, `pwsh`, `powershell`, `eval`, `su`, `ssh`, `source`, `.` and `xargs`.

Rules 2 and 3 apply only on a surplus: the call holds more `rm` words than the `rm` and `rmdir` command words rule 1 read outside nested text, a data heredoc body's `rm` words not counted. Without one, the call reads by rule 1 alone, so `rm -rf .scratch/out && bash build.sh` gets silence.

- **No target outside** — silence, the normal flow.
- **Outside, in a chained call** — any segment of the call not a delete: deny, with the reason `rm-fence: run the delete as its own command`.
- **Outside, alone** — every segment a delete: ask, listing each outside target, resolved or marked `(unresolved)`.
- **Unreadable input, or its own error** — ask, naming it.

It checks the main thread and agents alike. It guards against model error, beside Claude Code's critical-path breaker; it is not a security boundary. Two classes are left to the classifier: a delete whose call holds no `rm` word — `find -delete`, `unlink`, another program's delete, `source FILE`, a script written and run, in one call or two — and an `rm` word run by a program that is not a shell — `find … -exec rm`, or `rm` in `py -c`, `perl -e` or `node -e` code. A spelling with no `rm` in any letter case, such as `r''m`, never starts the hook.

### Known gaps

After this fix the fence is frozen: a new gap goes on this list, not onto a branch, unless a real deletion outside the roots occurs (design `rm-fence-freeze`, D4).

- A delete under `/tmp`, `$TEMP`, a `mktemp` folder or `$CLAUDE_JOB_DIR` passes silently (accepted by the ruling).
- If Claude Code does not pass `CLAUDE_JOB_DIR` to hooks, job paths keep asking.
- The whole job folder becomes a root, not only its `tmp`.
- MSYS-form `TMP`/`TEMP` in the hook's environment: `/tmp` and `mktemp` stay unresolved.
- `$$`, `$RANDOM`, `${TMPDIR:-/tmp}` with `TMPDIR` unknown, `$(cat FILE)` reading a temp file, and a single-quoted literal `$` (`'a$n'`, whose quotes the tokenizer drops) stay unresolved.
- A `TMPDIR` set inside the call is not read, so a bare `mktemp` still pins to the temp folder and a delete of it passes (`export TMPDIR=<outside>; rm -rf $(mktemp -d)`).
- `cygpath` with `-p`, `-f` or `-F`, or of a non-`mktemp` substitution, stays unresolved.
- `mktemp` inside nested text stays unknown.
- `trap 'rm …' EXIT` passes silently whatever its target, already the case before this fix.
- `cat > x.sh <<'EOF' … EOF && bash x.sh` newly passes silently.
- `py - <<'EOF'` running `os.system("rm …")` passes (the classifier's class ii).
- A program that runs its stdin as shell but is not one of the sixteen runners, under a quoted delimiter, passes.
- Unquoted-delimiter bodies beside a runner or to a receiver other than a note-writer (a commit message fed on stdin by `-F -`) still stop.
- A quoted body inside a substitution beside a runner (a commit message built by `-m "$(cat <<'EOF' … )"`, then `&& bash b.sh`) still stops, because the data count is top-level only.
- One running heredoc makes every body in the call commands.
- The ask's reason still says "the temp folder" while the roots now include the job folder.
- A command word set through a variable is counted once per use, so `x=rm; $x a; $x b; echo 'rm -rf <o>' | bash` reads as many `rm` command words as `rm` words, meets no surplus, and the piped runner's delete passes in silence (B146, a design call against F7 and `rm-fence-reach` §2).

## What the context-bound hook prints

Past the bound — the session's context strictly above the *Context bound* — one line, as `hookSpecificOutput.additionalContext` at exit 0, on every tool call until the session hands off:

`context-bound: context <figure> tokens, past the session-launch Context bound of <bound> - finish the step in hand, then invoke handoff, its objective the next step`

The bound is read at each call from the `session-launch` section of the bindings file the manifest's `bindings` filler names for `session-launch`, `core`'s by default: the first `<n>k` or bare integer on its `- Context bound:` line, so a changed bound takes effect at the next call. The figure is the one the binding's *Context read* computes, read from the end of the payload's `transcript_path`: the newest assistant entry whose `input_tokens`, `cache_creation_input_tokens` and `cache_read_input_tokens` sum above 0, looked for in 64 KB blocks, at most 1 MB back.

It prints nothing, at exit 0: at or below the bound; for a subagent's call, whose payload carries a non-empty `agent_id`, before anything is read; where the *Context bound* reads `none` or `TODO`, is absent or holds no number; where the *Context read* reads `none` or `TODO` or is absent, so that slot stays the on/off switch and the human's manual read; where no such entry lies within the last 1 MB; and on any error. It is a nudge, not a guard: it refuses nothing, and the boundary the session hands off at stays its judgment.

After a `/clear`, the `transcript_path` the platform names may be the pre-clear session's file, so the line may come where this session's context is below the bound, or fail to come where it is past it. No check guards against it: the risk is an open item of deferral B152, until its probe settles the cause.

## What the stop-line hook passes

A stop line is the final text's last non-blank line, read with `*`, `_` and backticks ignored, each whitespace run collapsed to one space, in any letter case: `Stop: <kind>` or `Stop: <kind> — <detail>`, the dash an em dash, an en dash or a hyphen, spaced. The kinds are nine: `declared`, `gate`, `irreversible act`, `scope change`, `BLOCKED`, `background agents`, `answering the human`, `fork` and `handoff`.

`declared` needs a detail, and the detail, normalized the same way, its backticks and one final `.` dropped, must equal an entry of the frontmatter `stops:` of a composed `.claude/skills/*/SKILL.md`; otherwise the line reads as no line. A `stops:` that is not an unquoted flow list, or a SKILL.md that cannot be read, passes the end.

With no valid line, the hook reads the transcript back to the newest human prompt, in 64 KB blocks, at most 1 MB. The end is skill work where that window holds a Skill call whose `input.skill`, after its last `:`, names a directory under `.claude/skills/`, or where that prompt's typed command names one; or by the carry, where the assistant text before that prompt closed on a stop line of a kind other than `answering the human` or `handoff`. Anything else, nothing found within the cap and an unreadable transcript included, is conversation, and the end passes.

A payload with a non-empty `agent_id`, a true `stop_hook_active` or no string `last_assistant_message` passes before anything is read. Skill work blocks: `{"decision": "block", "reason": …}` at exit 0, the reason "invoke the next skill, or `handoff` past the context bound or at a phase boundary". The end that follows a block passes. Any error is silence, and the end passes.

Elsewhere, a legitimate stop in skill work ended without the line costs one turn: the end is blocked once, and the next end passes. The reminder lines in `phase-pickup` and `implement` give the form and point here.

It leaves unchecked: whether the stop the line names is the true one, the end after a block, skill work its window misses, and the carry's one block per reply while the human chats after a stop in skill work.

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

Observed in a live check on 2026-10-11, its Claude Code version not recorded, of the handoff guard and the hub memory guard, the two scripts the write guard's handoff and hub memory rules then were:

- `"${CLAUDE_PROJECT_DIR}"` in a hook command expands as the argument: the handoff guard and the hub memory guard each found the `trees` section through it.
- `"if": "Bash(*--b*)"` started the handoff guard for a brief placement alone, chained, inside a `bash -c` string and split across a backslash-newline; each was refused.
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

Relied on, unobserved — the platform's tool schema, not seen in a lab or probe here:

- A Write's `tool_input.content` carries the whole new file, and an Edit's `tool_input.old_string` and `tool_input.new_string` the text replaced and its replacement; the write guard's locked-doc rule reads them.

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

## Allowing named calls

Set `HARNESS_ALLOW_NAMED_AGENTS` to `1` in `.claude/settings.local.json`'s `env` block:

```json
{ "env": { "HARNESS_ALLOW_NAMED_AGENTS": "1" } }
```

Only the value `1`, surrounding whitespace aside, lifts the refusal; unset, empty, `0`, `true` and `yes` all leave it in force.

The sentinel lifts **the named-call refusal only**. The subagent-dispatch refusal and the model comparison are never gated by it: with the sentinel set, a named call proceeds, and a subagent's dispatch and an over-baseline dispatch are still denied.

## Turning the hooks off entirely

`"disableAllHooks": true` in `.claude/settings.local.json` is the blunt alternative, and the only switch for the report filer, the write guard, the rm fence, the stop-line hook and any hook a composed pack adds, and for the context-bound hook the one beside its *Context read* slot (`## What the context-bound hook prints`) — and its cost is the reason to prefer the sentinel for named calls: it disables every hook at once. The project's `doctor` report stops arriving, reports stop being filed and receipts and turn ends stop being checked, and nothing says so; the delegator then files by hand (`.claude/references/core/delegation-contract.md` `## Filing`).

Hook *lists* merge, and a local settings file cannot remove one hook from a list, so there is no narrower switch than these two: the sentinel for the agent guard's named-call refusal, `disableAllHooks` for every hook.

## Where `py` is not a working interpreter

Every hook command runs `py`, directly or through a pack's `bash` shim that pipes to it, and `py` finds Python by the registry, not `PATH`. A machine with no `py` (a Store Python, macOS, Linux) gets none of the hooks: the doctor hook fails silently, so no report arrives; the agent guard fails with a visible but non-blocking error on each `Agent` dispatch, denying nothing; the report filer fails the same way at each agent's stop, refusing and filing nothing; the write guard fails the same way on each Write, Edit, NotebookEdit and SendMessage and each Bash call holding `--b`, refusing nothing; the rm fence fails the same way on each Bash call naming `rm`, refusing nothing; the context-bound hook fails the same way after each tool call, printing nothing; the stop-line hook fails the same way at each turn end, blocking nothing; and a version-control pack's guard, where one is composed, fails the same way, refusing nothing. None stops a session, and none can refuse a call — the cost of the missing interpreter is a rule that stops being enforced and reports that stop being filed. The fix is Python 3 from python.org, which carries `py`.

The wrapped commands — core's seven (the agent guard, the write guard's two entries and the rm fence's four) on `PreToolUse`, its one on `PostToolUse` (the context-bound hook) and its one on `Stop` (the stop-line hook), nine in all, and a version-control pack's guard, where one is composed — first test two files. Where the project directory holds neither `.claude/harness.json` nor the hook's script — a checkout from before the harness, reached while the registration still stands, since a running session re-reads `.claude/settings.json`'s hooks when the file changes and runs none after a switch onto a branch with no settings file (the probe of 2026-10-26) — the command exits 0 and prints nothing, so it refuses no call. Where the manifest is present and the script is not, `py` cannot open the script and exits 2, which refuses a `PreToolUse` call: a broken composition fails closed; the context-bound hook's exit 2 comes after its call has run, so it refuses nothing; the stop-line hook's exit 2, its stderr naming the missing script, is a non-blocking error at a turn end, by the documented rule the probe of 2026-10-15 records, so the turn ends as usual. The doctor hook and the report filer keep the bare command: on such a checkout the doctor hook's exit 2 is shown and the session continues, and the report filer's exit 2, its stderr naming the missing script, is a non-blocking error at an agent's stop, so the agent stops as usual (`## What the hooks rely on`, the probe of 2026-10-15). A version-control pack's `WorktreeCreate` hook is parked: no settings template registers it while writer trees are parked. Registered, it would keep the bare command too: there exit 2 aborts the tree's creation, as the hook itself refuses wherever `trees` reads absent.
