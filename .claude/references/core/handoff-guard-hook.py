# =============================================================================
# Purpose:  PreToolUse guard refusing a hand-off made outside the handoff skill
# Layer:    Hooks
# Owns:     (script, no public types)
# Depends:  (none)
# =============================================================================
"""Claude Code `PreToolUse` hook on `Write`, `Edit` and `SendMessage`: a hand-off
goes through the `handoff` skill, enforced rather than remembered.

Two watches: a Write or Edit under a `memory/handoffs/` directory, and a
SendMessage whose `message` begins `HANDOFF -`. Every other call gets silence,
with no transcript read. A watched call from a subagent is refused outright; a
watched call from the session passes when its transcript shows `handoff` invoked -
the Skill tool naming it, or the human's typed `/handoff` - and is refused
otherwise, a transcript that cannot be read included.

Standard library only, and never an import of `harness.py` - the hook runs under
whatever interpreter the platform starts, and `harness.py` exits at import time
when PyYAML or jsonschema is missing.

A denial is an affirmative document at exit 0; an allowed call is silence, which
is no opinion rather than approval. There is no generic catch anywhere here, so an
unforeseen bug is a traceback and a non-zero exit, a non-blocking error under
which the call passes.
"""

import json
import sys
from pathlib import Path

WATCHED_TOOLS = ("Write", "Edit", "SendMessage")
HANDOFF_PREFIX = "HANDOFF -"
SKILL_NAME = "handoff"
COMMAND_MARKER = "<command-name>/handoff</command-name>"
NO_PROOF_REASON = "this session's transcript shows no `handoff` invoked. A brief under `memory/handoffs/` and a `HANDOFF` line go through it: invoke the `handoff` skill, which writes the brief and sends `HANDOFF` itself."
SUBAGENT_REASON = "a hand-off runs in the session through the `handoff` skill, never in an agent. Stop, and report to the session that dispatched you."


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


def watched(payload):
    """True for the two calls the guard governs, False for every other.

    A Write or Edit is watched when its `file_path`, split on either separator and
    compared case-insensitively, holds `memory`, `handoffs` and a non-empty
    component, adjacent. A SendMessage is watched when its `message`, leading
    whitespace stripped, begins `HANDOFF -`; never `content`, which carries a
    truncated copy.
    """
    if payload.get("tool_name") not in WATCHED_TOOLS:
        return False
    tool_input = payload.get("tool_input")
    if not isinstance(tool_input, dict):
        return False
    if payload["tool_name"] == "SendMessage":
        message = tool_input.get("message")
        return isinstance(message, str) and message.lstrip().startswith(HANDOFF_PREFIX)
    file_path = tool_input.get("file_path")
    if not isinstance(file_path, str):
        return False
    parts = [part.lower() for part in file_path.replace("\\", "/").split("/")]
    return any(parts[index] == "memory" and parts[index + 1] == "handoffs"
               and parts[index + 2] != "" for index in range(len(parts) - 2))


def proof(transcript):
    """True when the transcript shows `handoff` invoked, read once.

    The proof is an `assistant` entry holding a `tool_use` block named `Skill`
    whose input's `skill` is `handoff`, or a `user` entry whose string content
    holds the typed command's marker. False for a path that is not a non-empty
    string and for a transcript that is missing, unreadable or not UTF-8. Lines
    are split on the newline alone, never on a separator JSON allows raw in a
    string; a line that does not parse is skipped.
    """
    if not isinstance(transcript, str) or not transcript:
        return False
    try:
        lines = Path(transcript).read_text(encoding="utf-8").split("\n")
    except (OSError, ValueError):
        return False
    for line in lines:
        try:
            entry = json.loads(line)
        except ValueError:
            continue
        if not isinstance(entry, dict):
            continue
        message = entry.get("message")
        content = message.get("content") if isinstance(message, dict) else None
        if entry.get("type") == "assistant" and isinstance(content, list):
            for block in content:
                if (isinstance(block, dict) and block.get("type") == "tool_use"
                        and block.get("name") == "Skill"
                        and isinstance(block.get("input"), dict)
                        and block["input"].get("skill") == SKILL_NAME):
                    return True
        elif entry.get("type") == "user" and isinstance(content, str):
            if COMMAND_MARKER in content:
                return True
    return False


def decide(payload):
    """The reason to deny with, or None to allow.

    An unwatched call is decided before any transcript is read, and so is a
    watched call from a subagent, whose payload carries `agent_id`.
    """
    if not watched(payload):
        return None
    if "agent_id" in payload:
        return SUBAGENT_REASON
    if proof(payload.get("transcript_path")):
        return None
    return NO_PROOF_REASON


def deny_document(reason):
    """The denial, one line: the guard's only output, and never an `allow`."""
    return json.dumps({"hookSpecificOutput": {"hookEventName": "PreToolUse",
                                              "permissionDecision": "deny",
                                              "permissionDecisionReason": reason}})


def main():
    payload = parse_payload(sys.stdin.buffer.read())
    if payload is None:
        return 0
    reason = decide(payload)
    if reason is not None:
        print(deny_document(reason))
    return 0


if __name__ == "__main__":
    sys.exit(main())
