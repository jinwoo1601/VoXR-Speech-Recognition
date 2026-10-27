# =============================================================================
# Purpose:  PreToolUse guard refusing a subagent's Agent call, a named teammate, a writing dispatch with no brief or a model upgrade
# Layer:    Hooks
# Owns:     (script, no public types)
# Depends:  (none)
# =============================================================================

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
                "the prompt, in backticks if it holds a space, in its drive-letter form "
                "or relative to the project root; only a read-only agent may take its brief "
                "inline (`.claude/references/core/constitution-annex.md` `## Agent and skill "
                "contracts`).")
DRIVE_FORM = re.compile(r"/([A-Za-z])(?=/|$)")
ANCHOR = re.compile(r"[A-Za-z]:[\\/]|[\\/]")


def resolve_root(argv):
    if len(argv) > 1:
        candidate = Path(argv[1])
        if candidate.is_dir():
            return candidate
    return Path.cwd()


def parse_payload(data):
    try:
        document = json.loads(data)
    except (ValueError, RecursionError):
        return None
    return document if isinstance(document, dict) else None


def is_plain_name(value):
    if not isinstance(value, str) or not value:
        return False
    if "/" in value or "\\" in value or ":" in value:
        return False
    return value.strip(".") != ""


def agent_frontmatter(root, subagent_type):
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
    for line in lines:
        if line.startswith(key + ":"):
            return line[len(key) + 1:].strip().strip("'\"")
    return None


def baseline_from(lines):
    model = None if lines is None else frontmatter_value(lines, "model")
    return None if model is None else model.lower()


def baseline_model(root, subagent_type):
    return baseline_from(agent_frontmatter(root, subagent_type))


def writes(lines):
    tools = frontmatter_value(lines, "tools")
    if tools is None:
        return False
    return any(tool.strip() in ("Write", "Edit", "Bash") for tool in tools.split(","))


def spaced_spans(prompt):
    spans = []
    for line in prompt.splitlines():
        tokens = list(re.finditer(r"\S+", line))
        for index, token in enumerate(tokens):
            if not token.group().rstrip("`'\")]}>.,;:").endswith(".md"):
                continue
            spans.extend(line[earlier.start():token.end()] for earlier in tokens[:index]
                         if ANCHOR.match(earlier.group().lstrip("`'\"([{<")))
    return spans


def prompt_candidates(prompt):
    texts = re.findall(r"`([^`]*)`", prompt) + re.findall(r'"([^"]*)"', prompt)
    return [text.lstrip("`'\"([{<").rstrip("`'\")]}>.,;:")
            for text in texts + prompt.split() + spaced_spans(prompt)]


def brief_path(text, root):
    if any(ord(char) < 32 or ord(char) == 127 for char in text):
        return None
    name = re.split(r"[/\\]", text)[-1]
    if not name.endswith(".md") or "brief" not in name.lower():
        return None
    match = DRIVE_FORM.match(text) if os.name == "nt" else None
    if match:
        text = match.group(1).upper() + ":" + (text[match.end():] or "/")
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
    return any(brief_path(text, root) is not None for text in prompt_candidates(prompt))


def decide(payload, root, environ):
    if payload.get("tool_name") != "Agent":
        return None
    agent_id = payload.get("agent_id")
    if isinstance(agent_id, str) and agent_id:
        return DISPATCH_REASON
    tool_input = payload.get("tool_input")
    if not isinstance(tool_input, dict):
        return None
    name = tool_input.get("name")
    if isinstance(name, str) and name and environ.get(SENTINEL, "").strip() != "1":
        return NAME_REASON
    subagent_type = tool_input.get("subagent_type")
    if not is_plain_name(subagent_type):
        return None
    lines = agent_frontmatter(root, subagent_type)
    prompt = tool_input.get("prompt")
    if (lines is not None and writes(lines) and isinstance(prompt, str)
            and not names_brief(prompt, root)):
        return BRIEF_REASON.format(agent=subagent_type)
    if "model" not in tool_input:
        return None
    attempted = tool_input["model"]
    baseline = baseline_from(lines)
    if baseline is None or baseline == "inherit":
        return None
    attempted_rank = RANKS.get(attempted) if isinstance(attempted, str) else None
    if attempted_rank is None or baseline not in RANKS:
        return None
    if attempted_rank > RANKS[baseline]:
        return MODEL_REASON.format(agent=subagent_type, baseline=baseline,
                                   attempted=attempted)
    return None


def deny_document(reason):
    return json.dumps({"hookSpecificOutput": {"hookEventName": "PreToolUse",
                                              "permissionDecision": "deny",
                                              "permissionDecisionReason": reason}})


def main(argv):
    payload = parse_payload(sys.stdin.buffer.read())
    if payload is None:
        return 0
    reason = decide(payload, resolve_root(argv), os.environ)
    if reason is not None:
        print(deny_document(reason))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
