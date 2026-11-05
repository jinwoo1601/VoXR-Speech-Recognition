# =============================================================================
# Purpose:  Stop hook blocking a main-thread turn end that leaves a returned agent's background task running
# Layer:    Hooks
# Owns:     (script, no public types)
# Depends:  (none)
# =============================================================================

import json
import re
import sys

TAIL = "is waiting on its own background work"
DELIVERED = "report was delivered"
HANDBACK = "SubagentHandback"
REASON = ("{ids}: an agent handed back with background work of its own still running "
          "(a call moved to the background at its timeout, or one it started); its report is "
          "in - stop each with TaskStop <id>, then end the turn")
NOTICE = re.compile(r"<task-notification>(.*?)</task-notification>", re.DOTALL)


def parse_payload(data):
    try:
        document = json.loads(data)
    except (ValueError, RecursionError):
        return None
    return document if isinstance(document, dict) else None


def field(body, name):
    match = re.search(f"<{name}>(.*?)</{name}>", body, re.DOTALL)
    return match.group(1).strip() if match else None


def notice_texts(entry):
    attachment = entry.get("attachment")
    if entry.get("type") == "attachment" and isinstance(attachment, dict) \
            and isinstance(attachment.get("prompt"), str):
        return [attachment["prompt"]]
    message = entry.get("message")
    if entry.get("type") == "user" and isinstance(message, dict) \
            and isinstance(message.get("content"), str):
        return [message["content"]]
    return []


def stopped(entry):
    message = entry.get("message")
    content = message.get("content") if isinstance(message, dict) else None
    if entry.get("type") != "assistant" or not isinstance(content, list):
        return []
    return [block["input"]["task_id"] for block in content
            if isinstance(block, dict) and block.get("type") == "tool_use"
            and block.get("name") == "TaskStop" and isinstance(block.get("input"), dict)
            and isinstance(block["input"].get("task_id"), str)]


def left_over(path):
    notices, stops = {}, {}
    with open(path, "rb") as handle:
        for index, line in enumerate(handle):
            if b"task-notification" not in line and b"TaskStop" not in line:
                continue
            try:
                entry = json.loads(line)
            except (ValueError, RecursionError):
                continue
            if not isinstance(entry, dict):
                continue
            for text in notice_texts(entry):
                for match in NOTICE.finditer(text):
                    task = field(match.group(1), "task-id")
                    if task:
                        notices[task] = (index, field(match.group(1), "summary") or "",
                                         field(match.group(1), "result") or "")
            for task in stopped(entry):
                stops[task] = index
    return [task for task, (index, summary, result) in notices.items()
            if summary.endswith(TAIL) and DELIVERED in result and HANDBACK in result
            and stops.get(task, -1) < index]


def decide(payload):
    if not isinstance(payload, dict) or payload.get("agent_id") \
            or payload.get("stop_hook_active"):
        return None
    transcript = payload.get("transcript_path")
    if not isinstance(transcript, str) or not transcript:
        return None
    try:
        tasks = left_over(transcript)
    except (OSError, ValueError):
        return None
    if not tasks:
        return None
    return json.dumps({"decision": "block", "reason": REASON.format(ids=", ".join(tasks))})


def main():
    try:
        payload = parse_payload(sys.stdin.buffer.read())
        if payload is not None:
            line = decide(payload)
            if line is not None:
                print(line)
    except Exception:
        pass
    return 0


if __name__ == "__main__":
    sys.exit(main())
