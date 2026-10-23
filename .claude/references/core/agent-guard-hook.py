# =============================================================================
# Purpose:  PreToolUse guard refusing a subagent's Agent call, a named teammate, a writing dispatch with no brief or a model upgrade
# Layer:    Hooks
# Owns:     (script, no public types)
# Depends:  (none)
# =============================================================================
"""Claude Code `PreToolUse` hook on the `Agent` tool: the orchestration arrow's
rule that agents never dispatch agents, the annex's rule that a writing roster
agent's dispatch names an existing brief file, and the delegation contract's two
structural rules, enforced rather than remembered.

Standard library only, and never an import of `harness.py` - the hook runs under
whatever interpreter the platform starts, and `harness.py` exits at import time
when PyYAML or jsonschema is missing. The baseline and the `tools:` list are taken
by a line scan rather than a YAML parse for the same reason: the agent schema
already constrains `model` to a plain scalar from a three-value enum and `tools`
to one comma-separated line, and V6 re-checks both.

A denial is an affirmative document at exit 0; an allowed call is silence, which
is no opinion rather than approval. Every input the guard cannot interpret falls
to allow through a branch of its own - there is no generic catch anywhere here,
so an unforeseen bug is a traceback and a non-zero exit, and neither denies.
"""

import json
import os
import re
import sys
from pathlib import Path

AGENTS_REL = ".claude/agents"
SENTINEL = "HARNESS_ALLOW_NAMED_AGENTS"
RANKS = {"sonnet": 1, "opus": 2}
MODEL_REASON = ("`{agent}`'s baseline is `{baseline}` (`.claude/agents/{agent}.md`); this "
                "call names `{attempted}`, which ranks above it. A brief may downgrade an "
                "agent below its baseline, never upgrade (the subagent model rule; "
                "`.claude/references/core/delegation-contract.md`).")
NAME_REASON = ("this `Agent` call carries `name`, the teammate spawn form, which is outside "
               "the delegation contract. To allow named calls in this project, set "
               "`HARNESS_ALLOW_NAMED_AGENTS` to `1` in `.claude/settings.local.json`'s `env` "
               "block (see `.claude/references/core/hooks.md`).")
DISPATCH_REASON = ("this `Agent` call comes from a subagent (`agent_id` set); agents never "
                   "dispatch agents (`CONSTITUTION.md` `## The orchestration arrow`). Stop, "
                   "and report to the session that dispatched you what you needed dispatched.")
BRIEF_REASON = ("`{agent}`'s `tools:` in `.claude/agents/{agent}.md` include Write, Edit or "
                "Bash, and this call's prompt names no existing brief. File the brief under "
                "`.scratch/` as a `.md` file whose name contains `brief`, and name its path in "
                "the prompt; only a read-only agent may take its brief inline "
                "(`.claude/references/core/constitution-annex.md` `## Agent and skill "
                "contracts`).")


def resolve_root(argv):
    """The project root: argv[1] when it names a directory, else the process cwd."""
    if len(argv) > 1:
        candidate = Path(argv[1])
        if candidate.is_dir():
            return candidate
    return Path.cwd()


def parse_payload(data):
    """The hook payload as a mapping, or None for stdin the guard cannot read.

    None covers bytes that are not JSON at all, bytes that are not UTF-8, and JSON
    that parses to a list, a string or a number rather than to an object.
    """
    try:
        document = json.loads(data)
    except (ValueError, RecursionError):
        return None
    return document if isinstance(document, dict) else None


def is_plain_name(value):
    """True when this caller-supplied name can only address a file in the agents
    directory: a non-empty string with no `/`, no `\\`, no `:` and no `.` segment -
    so `.`, `..`, `a/b`, `a\\b`, `C:secrets` and `../secrets` are all refused before
    a path is built.
    """
    if not isinstance(value, str) or not value:
        return False
    if "/" in value or "\\" in value or ":" in value:
        return False
    return value.strip(".") != ""


def agent_frontmatter(root, subagent_type):
    """The agent file's leading `---` block: its lines after the opening `---`, up
    to the next `---` or the end of the file.

    None for an absent file, an unreadable or non-UTF-8 file, and a file with no
    leading `---` block: each falls through the brief step and the model step. The
    guard's one agent-file read, shared by both.
    """
    try:
        text = (root / AGENTS_REL / (subagent_type + ".md")).read_text(encoding="utf-8")
    except (OSError, ValueError):
        return None
    lines = text.split("\n")
    if lines[0].strip() != "---":
        return None
    block = []
    for line in lines[1:]:
        if line.strip() == "---":
            break
        block.append(line)
    return block


def frontmatter_value(lines, key):
    """The value of the first line starting `key:`, stripped and unquoted, or None."""
    for line in lines:
        if line.startswith(key + ":"):
            return line[len(key) + 1:].strip().strip("'\"")
    return None


def baseline_from(lines):
    """The block's declared `model`, stripped, unquoted and lowercased; None for a
    None block or one with no `model:` key."""
    model = None if lines is None else frontmatter_value(lines, "model")
    return None if model is None else model.lower()


def baseline_model(root, subagent_type):
    """The agent file's declared `model`, stripped, unquoted and lowercased.

    None for an absent file, an unreadable or non-UTF-8 file, a file with no
    leading `---` block, and a block with no `model:` key: each allows the call.
    """
    return baseline_from(agent_frontmatter(root, subagent_type))


def writes(lines):
    """True when the block's `tools:` value, split at commas, holds `Write`, `Edit`
    or `Bash` exactly; False for a block with no `tools:` line."""
    tools = frontmatter_value(lines, "tools")
    if tools is None:
        return False
    return any(tool.strip() in ("Write", "Edit", "Bash") for tool in tools.split(","))


def prompt_candidates(prompt):
    """The prompt's path candidates: each span between a pair of backticks, each
    between a pair of double quotes, then each whitespace-separated word - every
    one stripped of the quotes and brackets around it and of trailing sentence
    punctuation, a leading `.` kept for a root-relative `.scratch/` path.
    """
    texts = re.findall(r"`([^`]*)`", prompt) + re.findall(r'"([^"]*)"', prompt)
    return [text.lstrip("`'\"([{<").rstrip("`'\")]}>.,;:")
            for text in texts + prompt.split()]


def brief_path(text, root):
    """The brief `text` names, resolved, or None when it names none.

    The text screen first, so only a candidate whose last component, at either
    separator, ends `.md` and holds `brief` reaches the disk, and none holding a
    control character. Then the report filer's rule (`report-filer-hook.py`
    `resolve_brief`), mirrored: relative to the root, resolved, an existing `.md`
    file under a `.scratch/` directory - and its name still holding `brief`.
    """
    if any(ord(char) < 32 or ord(char) == 127 for char in text):
        return None
    name = re.split(r"[/\\]", text)[-1]
    if not name.endswith(".md") or "brief" not in name.lower():
        return None
    path = Path(text)
    if not path.is_absolute():
        path = root / path
    path = path.resolve()
    if not path.name.endswith(".md") or "brief" not in path.name.lower() or not path.is_file():
        return None
    if ".scratch" in [part.lower() for part in path.parts[:-1]]:
        return path
    return None


def names_brief(prompt, root):
    """True at the first of the prompt's candidates that names an existing brief."""
    return any(brief_path(text, root) is not None for text in prompt_candidates(prompt))


def decide(payload, root, environ):
    """Steps 2 through 10: the reason to deny with, or None to allow.

    `environ` is a parameter rather than an `os.environ` read so the sentinel is
    drivable with a plain dict. A call from a subagent - a non-empty string
    `agent_id` - is refused first, whatever the sentinel. The name refusal precedes
    the brief step, and the brief step the model comparison; the sentinel gates the
    name refusal alone. After the name screen, the agent file is read once: a
    writing roster agent whose string prompt names no existing brief is denied, and
    every other call falls through to the model step, never allowed there. A tier
    the table does not rank - on either side, including a `model` that is not a
    string - allows.
    """
    if payload.get("tool_name") != "Agent":
        return None                                          # [step 2, F15]
    agent_id = payload.get("agent_id")
    if isinstance(agent_id, str) and agent_id:
        return DISPATCH_REASON                               # [the orchestration arrow]
    tool_input = payload.get("tool_input")
    if not isinstance(tool_input, dict):
        return None                                          # [step 3]
    name = tool_input.get("name")
    if isinstance(name, str) and name and environ.get(SENTINEL, "").strip() != "1":
        return NAME_REASON                                   # [step 4, F11, F12]
    subagent_type = tool_input.get("subagent_type")
    if not is_plain_name(subagent_type):
        return None                                          # [step 5, F14]
    lines = agent_frontmatter(root, subagent_type)
    prompt = tool_input.get("prompt")
    if (lines is not None and writes(lines) and isinstance(prompt, str)
            and not names_brief(prompt, root)):
        return BRIEF_REASON.format(agent=subagent_type)      # [brief step, F1]
    if "model" not in tool_input:
        return None                                          # [step 6, F8]
    attempted = tool_input["model"]
    baseline = baseline_from(lines)                          # [step 7, F10, F14]
    if baseline is None or baseline == "inherit":
        return None                                          # [step 8, F9]
    attempted_rank = RANKS.get(attempted) if isinstance(attempted, str) else None
    if attempted_rank is None or baseline not in RANKS:
        return None                                          # [step 9, decision A]
    if attempted_rank > RANKS[baseline]:                      # [step 10, F6]
        return MODEL_REASON.format(agent=subagent_type, baseline=baseline,
                                   attempted=attempted)
    return None                                              # [F7]


def deny_document(reason):
    """The denial, one line: the guard's only output, and never an `allow`."""
    return json.dumps({"hookSpecificOutput": {"hookEventName": "PreToolUse",
                                              "permissionDecision": "deny",
                                              "permissionDecisionReason": reason}})


def main(argv):
    payload = parse_payload(sys.stdin.buffer.read())
    if payload is None:
        return 0                                             # [step 1, F14]
    reason = decide(payload, resolve_root(argv), os.environ)
    if reason is not None:
        print(deny_document(reason))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
