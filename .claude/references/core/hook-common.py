# =============================================================================
# Purpose:  Shared readers the core hooks load beside them: payload, project root, bindings file and section, slots, transcript tail, session name and scratch path
# Layer:    Hooks
# Owns:     (script, no public types)
# Depends:  (none)
# =============================================================================

import json
import ntpath
import os
import re
from pathlib import Path

BINDING_KEY = "session-launch"
BLOCK_BYTES = 65536
SCAN_BYTES = 1048576
DRIVE_FORM = re.compile(r"/([A-Za-z])(?=/|$)")
HUB_CHECKOUT = re.compile(r"the main checkout is `([^`]+)`")


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


def resolve_root(argv):
    if len(argv) > 1:
        candidate = Path(argv[1])
        if candidate.is_dir():
            return candidate
    return Path.cwd()


def binding_stem(named):
    if (isinstance(named, str) and re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]*", named)
            and ".." not in named):
        return named
    return "core"


def bindings_file(root, key=BINDING_KEY):
    try:
        with open(ntpath.join(root, ".claude", "harness.json"), encoding="utf-8") as handle:
            document = json.load(handle)
    except (OSError, ValueError, RecursionError):
        document = None
    filler = document.get("bindings") if isinstance(document, dict) else None
    named = filler.get(key) if isinstance(filler, dict) else None
    return ntpath.join(root, ".claude", "bindings", binding_stem(named) + ".md")


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


def trees_hub(lines):
    line = next((line for line in lines or [] if line.startswith("- Hub:")), None)
    match = HUB_CHECKOUT.search(line) if line else None
    return match.group(1) if match else None


def tail_lines(handle, position):
    carry, scanned = b"", 0
    while position > 0 and scanned < SCAN_BYTES:
        size = min(BLOCK_BYTES, position, SCAN_BYTES - scanned)
        position -= size
        handle.seek(position)
        lines = (handle.read(size) + carry).split(b"\n")
        scanned += size
        carry = lines.pop(0) if position > 0 else b""
        yield from reversed(lines)


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
            for line in tail_lines(handle, position):
                name = line_name(line)
                if name is not None:
                    return name
            if position > SCAN_BYTES:
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


def scratch_md(text, root, word=""):
    match = DRIVE_FORM.match(text) if os.name == "nt" else None
    if match:
        text = match.group(1).upper() + ":" + (text[match.end():] or "/")
    path = Path(text)
    if not path.is_absolute():
        path = root / path
    try:
        path = path.resolve()
        if not path.name.endswith(".md") or word not in path.name.lower() or not path.is_file():
            return None
    except (OSError, ValueError, RuntimeError):
        return None
    if ".scratch" in [part.lower() for part in path.parts[:-1]]:
        return path
    return None
