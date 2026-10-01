# =============================================================================
# Purpose:  PreToolUse guard refusing an Agent call that upgrades a model or names a teammate
# Layer:    Hooks
# Owns:     (script, no public types)
# Depends:  (none)
# =============================================================================
"""Claude Code `PreToolUse` hook on the `Agent` tool: the delegation contract's
two structural rules, enforced rather than remembered.

Standard library only, and never an import of `harness.py` - the hook runs under
whatever interpreter the platform starts, and `harness.py` exits at import time
when PyYAML or jsonschema is missing. The baseline is taken by a line scan rather
than a YAML parse for the same reason: the agent schema already constrains
`model` to a plain scalar from a three-value enum, and V6 re-checks it.

A denial is an affirmative document at exit 0; an allowed call is silence, which
is no opinion rather than approval. Every input the guard cannot interpret falls
to allow through a branch of its own - there is no generic catch anywhere here,
so an unforeseen bug is a traceback and a non-zero exit, and neither denies.
"""

import json
import os
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
    except ValueError:
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


def baseline_model(root, subagent_type):
    """The agent file's declared `model`, stripped, unquoted and lowercased.

    None for an absent file, an unreadable or non-UTF-8 file, a file with no
    leading `---` block, and a block with no `model:` key: each allows the call.
    """
    try:
        text = (root / AGENTS_REL / (subagent_type + ".md")).read_text(encoding="utf-8")
    except (OSError, ValueError):
        return None
    lines = text.split("\n")
    if lines[0].strip() != "---":
        return None
    for line in lines[1:]:
        if line.strip() == "---":
            return None
        if line.startswith("model:"):
            return line.split(":", 1)[1].strip().strip("'\"").lower()
    return None


def decide(payload, root, environ):
    """Steps 2 through 10: the reason to deny with, or None to allow.

    `environ` is a parameter rather than an `os.environ` read so the sentinel is
    drivable with a plain dict. The name refusal precedes the model comparison and
    the sentinel gates the name refusal alone; a tier the table does not rank - on
    either side, including a `model` that is not a string - allows.
    """
    if payload.get("tool_name") != "Agent":
        return None                                          # [step 2, F15]
    tool_input = payload.get("tool_input")
    if not isinstance(tool_input, dict):
        return None                                          # [step 3]
    name = tool_input.get("name")
    if isinstance(name, str) and name and environ.get(SENTINEL, "").strip() != "1":
        return NAME_REASON                                   # [step 4, F11, F12]
    subagent_type = tool_input.get("subagent_type")
    if not is_plain_name(subagent_type):
        return None                                          # [step 5, F14]
    if "model" not in tool_input:
        return None                                          # [step 6, F8]
    attempted = tool_input["model"]
    baseline = baseline_model(root, subagent_type)            # [step 7, F10, F14]
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
