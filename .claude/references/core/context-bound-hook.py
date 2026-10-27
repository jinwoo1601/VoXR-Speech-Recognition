# =============================================================================
# Purpose:  PostToolUse hook signalling a session past its session-launch Context bound
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
    return json.dumps({"hookSpecificOutput": {
        "hookEventName": "PostToolUse",
        "additionalContext": LINE.format(figure=figure, bound=bound)}},
        separators=(",", ":"))


def main(argv):
    try:
        payload = parse_payload(sys.stdin.buffer.read())
        if payload is not None:
            line = decide(payload, argv[1] if len(argv) > 1 else None)
            if line is not None:
                print(line)
    except Exception:
        pass
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
