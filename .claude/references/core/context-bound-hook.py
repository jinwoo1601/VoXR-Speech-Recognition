# =============================================================================
# Purpose:  PostToolUse hook signalling a session past its session-launch Context bound, by class, and its SessionStart compact mode restating a hub's standing orders
# Layer:    Hooks
# Owns:     (script, no public types)
# Depends:  hook-common.py
# =============================================================================

import json
import os
import re
import sys

try:
    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "hook-common.py"), "rb") as common:
        exec(compile(common.read(), common.name, "exec"), globals())
    COMMON_ERROR = None
except Exception as error:
    COMMON_ERROR = error

NUMBER = re.compile(r"\b(\d+)([kK]?)\b")
USAGE_KEYS = ("input_tokens", "cache_creation_input_tokens", "cache_read_input_tokens")
LINE = ("context-bound: context {figure} tokens, past the session-launch Context bound of "
        "{bound} - finish the step in hand, then invoke handoff, its objective the next step")
HUB_LINE = ("context-bound: context {figure} tokens, past the session-launch Context bound of "
            "{bound} - you are the hub session: file anything held only in context to disk, "
            "then carry on")
ORDERS = ("Standing orders after a compaction. You are this project's hub session. Run "
          "close-tree on report lines. If a skill was in flight, invoke it again now: its body "
          "may be cut. Re-read state from disk before you act; never trust the summary.")


def unbound(text):
    return text is None or text.lower().startswith(("todo", "none"))


def slot_figure(text):
    if unbound(text):
        return None
    match = NUMBER.search(text)
    if match is None:
        return None
    return int(match.group(1)) * (1000 if match.group(2) else 1)


def context_bound(root, label="Context bound"):
    try:
        with open(bindings_file(root), encoding="utf-8") as handle:
            lines = binding_section(handle.read(), BINDING_KEY)
    except (OSError, ValueError):
        return None
    if lines is None:
        return None
    if unbound(slot_text(lines, "Context read")):
        return None
    return slot_figure(slot_text(lines, label))


def usage_sum(line):
    try:
        entry = json.loads(line)
    except (ValueError, RecursionError):
        return 0
    if not isinstance(entry, dict) or entry.get("type") != "assistant":
        return 0
    message = entry.get("message")
    usage = message.get("usage") if isinstance(message, dict) else None
    if not isinstance(usage, dict):
        return 0
    values = [usage.get(key) for key in USAGE_KEYS]
    return sum(value for value in values
               if isinstance(value, int) and not isinstance(value, bool))


def context_figure(path):
    with open(path, "rb") as handle:
        for line in tail_lines(handle, handle.seek(0, os.SEEK_END)):
            figure = usage_sum(line)
            if figure > 0:
                return figure
    return None


def decide(payload, argument=None):
    if payload.get("agent_id"):
        return None
    root = project_root(payload, argument)
    if root is None:
        return None
    bound = context_bound(root)
    if bound is None:
        return None
    transcript = payload.get("transcript_path")
    if not isinstance(transcript, str) or not transcript:
        return None
    hub_bound = context_bound(root, "Hub context bound")
    floor = bound if hub_bound is None else min(bound, hub_bound)
    figure = context_figure(transcript)
    if figure is None or figure <= floor:
        return None
    hub = is_hub(root, transcript)
    limit = hub_bound if hub and hub_bound is not None else bound
    if figure <= limit:
        return None
    return json.dumps({"hookSpecificOutput": {
        "hookEventName": "PostToolUse",
        "additionalContext": (HUB_LINE if hub else LINE).format(figure=figure, bound=limit)}},
        separators=(",", ":"))


def compact_orders(payload, argument=None):
    if payload.get("agent_id"):
        return None
    root = project_root(payload, argument)
    if root is None:
        return None
    transcript = payload.get("transcript_path")
    if not isinstance(transcript, str) or not transcript:
        return None
    if not is_hub(root, transcript):
        return None
    return json.dumps({"hookSpecificOutput": {
        "hookEventName": "SessionStart",
        "additionalContext": ORDERS}},
        separators=(",", ":"))


def main(argv):
    try:
        data = sys.stdin.buffer.read()
        if COMMON_ERROR is not None:
            raise COMMON_ERROR
        payload = parse_payload(data)
        if payload is not None:
            choose = compact_orders if len(argv) > 2 and argv[2] == "compact" else decide
            line = choose(payload, argv[1] if len(argv) > 1 else None)
            if line is not None:
                print(line)
    except Exception:
        pass
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
