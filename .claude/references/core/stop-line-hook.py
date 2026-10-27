# =============================================================================
# Purpose:  Stop hook blocking, once, a skill-work turn end that closes on no stop line
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
KINDS = ("declared", "gate", "irreversible act", "scope change", "BLOCKED",
         "background agents", "answering the human", "fork", "handoff")
CARRY_PASS = ("answering the human", "handoff")
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


def decide(payload, argument=None):
    if not isinstance(payload, dict) or payload.get("agent_id") \
            or payload.get("stop_hook_active"):
        return None
    text = payload.get("last_assistant_message")
    if not isinstance(text, str):
        return None
    line = stop_line(text)
    if line is not None and line[0] != "declared":
        return None
    root = project_root(payload, argument)
    names = skill_names(root)
    if names is None:
        return None
    if line is not None:
        declared = declared_names(root, names)
        if declared is None or declared_key(line[1]) in declared:
            return None
    if skill_work(payload.get("transcript_path"), names):
        return json.dumps({"decision": "block", "reason": REASON})
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
