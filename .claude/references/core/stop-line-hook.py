# =============================================================================
# Purpose:  Stop hook blocking, once, a skill-work turn end that closes on no stop line, or an end on a waiting stop line with no AskUserQuestion call
# Layer:    Hooks
# Owns:     (script, no public types)
# Depends:  (none)
# =============================================================================

import json
import ntpath
import os
import re
import sys

REASON = "invoke the next skill, or `handoff` past the context bound or at a phase boundary"
HUB_REASON = "invoke the next skill, or end on a stop line; past the context bound, file anything held only in context to disk and carry on"
ASK_REASON = "put this stop to the human through AskUserQuestion, its question stating the issue on its own, then end again; where AskUserQuestion is unavailable or refused, say so and end again"
KINDS = ("declared", "gate", "irreversible act", "scope change", "BLOCKED",
         "background agents", "answering the human", "fork", "handoff")
WAITING = ("declared", "gate", "irreversible act", "scope change", "BLOCKED", "fork")
CARRY_PASS = ("answering the human", "handoff")
BINDING_KEY = "session-launch"
BLOCK_BYTES = 65536
SCAN_BYTES = 1048576
DASH = re.compile("\\s[\\u2014\\u2013-]\\s")
COMMAND = re.compile(r"<command-name>/([^<]*)</command-name>")


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


def normal(text):
    for mark in ("*", "_", "`"):
        text = text.replace(mark, "")
    return re.sub(r"\s+", " ", text).strip().casefold()


def stop_line(text):
    lines = [line for line in text.splitlines() if line.strip()]
    if not lines:
        return None
    line = normal(lines[-1])
    if not line.startswith("stop:"):
        return None
    parts = DASH.split(line[len("stop:"):].strip(), maxsplit=1)
    kind = parts[0].strip()
    detail = parts[1].strip() if len(parts) > 1 else ""
    if kind not in [entry.casefold() for entry in KINDS]:
        return None
    if kind == "declared" and not detail:
        return None
    return kind, detail


def declared_key(text):
    key = normal(text)
    if key.endswith("."):
        key = key[:-1]
    return key.strip()


def skill_names(root):
    if root is None:
        return None
    skills = ntpath.join(root, ".claude", "skills")
    try:
        return frozenset(name.casefold() for name in os.listdir(skills)
                         if os.path.isdir(ntpath.join(skills, name)))
    except OSError:
        return None


def stops_entries(text):
    lines = text.split("\n")
    if lines[0] != "---":
        return []
    closing = next((index for index in range(1, len(lines)) if lines[index] == "---"), None)
    if closing is None:
        return []
    for line in lines[1:closing]:
        if not line.startswith("stops:"):
            continue
        value = line[len("stops:"):].strip()
        if not (value.startswith("[") and value.endswith("]")):
            return None
        entries = [entry.strip() for entry in value[1:-1].split(",")]
        if any(entry.startswith(('"', "'")) for entry in entries):
            return None
        return [entry for entry in entries if entry]
    return []


def declared_names(root, names):
    declared = set()
    for name in names:
        path = ntpath.join(root, ".claude", "skills", name, "SKILL.md")
        if not os.path.isfile(path):
            continue
        try:
            with open(path, encoding="utf-8") as handle:
                entries = stops_entries(handle.read())
        except (OSError, UnicodeDecodeError):
            return None
        if entries is None:
            return None
        declared.update(declared_key(entry) for entry in entries)
    return declared


def entries_backwards(path):
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
                try:
                    entry = json.loads(line)
                except (ValueError, RecursionError):
                    continue
                if isinstance(entry, dict):
                    yield entry


def invoked(entry, names):
    message = entry.get("message")
    content = message.get("content") if isinstance(message, dict) else None
    if entry.get("type") == "assistant":
        if not isinstance(content, list):
            return False
        for block in content:
            if (isinstance(block, dict) and block.get("type") == "tool_use"
                    and block.get("name") == "Skill"
                    and isinstance(block.get("input"), dict)
                    and isinstance(block["input"].get("skill"), str)
                    and block["input"]["skill"].rsplit(":", 1)[-1].casefold() in names):
                return True
        return False
    if not human_prompt(entry):
        return False
    if isinstance(content, str):
        text = content
    else:
        text = "\n".join(block["text"] for block in content
                         if isinstance(block, dict) and block.get("type") == "text"
                         and isinstance(block.get("text"), str))
    match = COMMAND.search(text)
    return match is not None and match.group(1).rsplit(":", 1)[-1].casefold() in names


def human_prompt(entry):
    if entry.get("type") != "user" or entry.get("isMeta") or entry.get("isCompactSummary") \
            or "toolUseResult" in entry:
        return False
    message = entry.get("message")
    content = message.get("content") if isinstance(message, dict) else None
    if isinstance(content, str):
        return True
    return isinstance(content, list) and not any(
        isinstance(block, dict) and block.get("type") == "tool_result" for block in content)


def final_text(entry):
    if entry.get("type") != "assistant":
        return None
    message = entry.get("message")
    content = message.get("content") if isinstance(message, dict) else None
    if not isinstance(content, list):
        return None
    texts = [block["text"] for block in content
             if isinstance(block, dict) and block.get("type") == "text"
             and isinstance(block.get("text"), str)]
    return texts[-1] if texts else None


def unasked(transcript):
    if not isinstance(transcript, str) or not transcript:
        return False
    try:
        for entry in entries_backwards(transcript):
            message = entry.get("message")
            content = message.get("content") if isinstance(message, dict) else None
            if entry.get("type") == "assistant" and isinstance(content, list) and any(
                    isinstance(block, dict) and block.get("type") == "tool_use"
                    and block.get("name") == "AskUserQuestion" for block in content):
                return False
            if human_prompt(entry):
                return True
    except (OSError, ValueError):
        return False
    return True


def skill_work(transcript, names):
    if not isinstance(transcript, str) or not transcript:
        return False
    try:
        entries = entries_backwards(transcript)
        for entry in entries:
            if invoked(entry, names):
                return True
            if human_prompt(entry):
                break
        else:
            return False
        for entry in entries:
            if human_prompt(entry):
                return False
            text = final_text(entry)
            if text is not None:
                line = stop_line(text)
                return line is not None and line[0] not in CARRY_PASS
    except (OSError, ValueError):
        return False
    return False


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
    if not isinstance(payload, dict) or payload.get("agent_id") \
            or payload.get("stop_hook_active"):
        return None
    text = payload.get("last_assistant_message")
    if not isinstance(text, str):
        return None
    line = stop_line(text)
    waiting = line is not None and line[0] in [kind.casefold() for kind in WAITING]
    if line is not None and not waiting:
        return None
    ask = json.dumps({"decision": "block", "reason": ASK_REASON})
    if waiting and line[0] != "declared":
        return ask if unasked(payload.get("transcript_path")) else None
    root = project_root(payload, argument)
    names = skill_names(root)
    if names is None:
        return None
    if line is not None:
        declared = declared_names(root, names)
        if declared is None:
            return None
        if declared_key(line[1]) in declared:
            return ask if unasked(payload.get("transcript_path")) else None
    if skill_work(payload.get("transcript_path"), names):
        reason = HUB_REASON if is_hub(root, payload.get("transcript_path")) else REASON
        return json.dumps({"decision": "block", "reason": reason})
    return None


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
