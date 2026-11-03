# =============================================================================
# Purpose:  PostToolUse hook signalling a session past its session-launch Context bound, by class, and its SessionStart compact mode restating a hub's standing orders
# Layer:    Hooks
# Owns:     (script, no public types)
# Depends:  (none)
# =============================================================================

import json
import ntpath
import os
import re
import sys

BINDING_KEY = "session-launch"
BLOCK_BYTES = 65536
SCAN_BYTES = 1048576
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


def parse_payload(data):
    try:
        document = json.loads(data)
    except (ValueError, RecursionError):
        return None
    return document if isinstance(document, dict) else None


def project_root(payload, argument):
    for candidate in (argument, payload.get("cwd")):
        if isinstance(candidate, str) and candidate and os.path.isdir(candidate):
            return candidate
    return None


def bindings_file(root):
    name = "core"
    try:
        with open(ntpath.join(root, ".claude", "harness.json"), encoding="utf-8") as handle:
            document = json.load(handle)
    except (OSError, ValueError, RecursionError):
        document = None
    if isinstance(document, dict):
        filler = document.get("bindings")
        named = filler.get(BINDING_KEY) if isinstance(filler, dict) else None
        if (isinstance(named, str) and re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]*", named)
                and ".." not in named):
            name = named
    return ntpath.join(root, ".claude", "bindings", name + ".md")


def binding_section(text, key):
    heading = re.compile(r"^#{2,}\s*" + re.escape(key) + r"\b", re.IGNORECASE)
    body = None
    for line in text.splitlines():
        if body is not None and re.match(r"^#{1,6}\s", line):
            break
        if body is not None:
            body.append(line)
        elif heading.match(line):
            body = []
    return body


def slot_text(lines, label):
    for line in lines:
        stripped = line.strip()
        if stripped.startswith(f"- {label}:"):
            return stripped[len(f"- {label}:"):].strip()
    return None


def unbound(text):
    return text is None or text.lower().startswith(("todo", "none"))


def context_bound(root):
    try:
        with open(bindings_file(root), encoding="utf-8") as handle:
            lines = binding_section(handle.read(), BINDING_KEY)
    except (OSError, ValueError):
        return None
    if lines is None:
        return None
    bound, read = slot_text(lines, "Context bound"), slot_text(lines, "Context read")
    if unbound(bound) or unbound(read):
        return None
    match = NUMBER.search(bound)
    if match is None:
        return None
    return int(match.group(1)) * (1000 if match.group(2) else 1)


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
        position = handle.seek(0, os.SEEK_END)
        carry, scanned = b"", 0
        while position > 0 and scanned < SCAN_BYTES:
            size = min(BLOCK_BYTES, position, SCAN_BYTES - scanned)
            position -= size
            handle.seek(position)
            lines = (handle.read(size) + carry).split(b"\n")
            scanned += size
            carry = lines.pop(0) if position > 0 else b""
            for line in reversed(lines):
                figure = usage_sum(line)
                if figure > 0:
                    return figure
    return None


def line_name(line):
    try:
        entry = json.loads(line)
    except (ValueError, RecursionError):
        return None
    if not isinstance(entry, dict):
        return None
    kind = entry.get("type")
    if kind == "custom-title":
        value = entry.get("customTitle")
    elif kind == "agent-name":
        value = entry.get("agentName")
    else:
        return None
    return value if isinstance(value, str) and value else None


def session_name(path):
    try:
        with open(path, "rb") as handle:
            position = handle.seek(0, os.SEEK_END)
            carry, scanned = b"", 0
            while position > 0 and scanned < SCAN_BYTES:
                size = min(BLOCK_BYTES, position, SCAN_BYTES - scanned)
                position -= size
                handle.seek(position)
                lines = (handle.read(size) + carry).split(b"\n")
                scanned += size
                carry = lines.pop(0) if position > 0 else b""
                for line in reversed(lines):
                    name = line_name(line)
                    if name is not None:
                        return name
            handle.seek(0)
            for line in reversed(handle.read(BLOCK_BYTES).split(b"\n")):
                name = line_name(line)
                if name is not None:
                    return name
    except (OSError, ValueError):
        return None
    return None


def slot_name(text):
    if not text or re.match(r"(?i)todo(?![\w-])", text):
        return None
    head = re.split("\\s[\\u2014\\u2013-]\\s| \\(|,", text, maxsplit=1)[0]
    if "`" not in head:
        name = head.strip("*\" \t")
    else:
        match = re.search(r"`([^`]*)`", text)
        if match is None:
            return None
        group = match.group(1)
        if " -n " in group:
            words = group.split(" -n ", 1)[1].split()
            name = words[0] if words else ""
        else:
            name = group.strip()
    if not name or name.casefold() in ("none", "todo"):
        return None
    return name


def hub_name(root):
    try:
        with open(bindings_file(root), encoding="utf-8") as handle:
            lines = binding_section(handle.read(), BINDING_KEY)
    except (OSError, ValueError):
        return None
    if lines is None:
        return None
    return slot_name(slot_text(lines, "Hub session name"))


def is_hub(root, transcript):
    try:
        hub = hub_name(root)
        return hub is not None and session_name(transcript) == hub
    except Exception:
        return False


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
    figure = context_figure(transcript)
    if figure is None or figure <= bound:
        return None
    line = HUB_LINE if is_hub(root, transcript) else LINE
    return json.dumps({"hookSpecificOutput": {
        "hookEventName": "PostToolUse",
        "additionalContext": line.format(figure=figure, bound=bound)}},
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
        payload = parse_payload(sys.stdin.buffer.read())
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
