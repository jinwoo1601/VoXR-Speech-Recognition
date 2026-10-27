# =============================================================================
# Purpose:  PreToolUse write guard holding the handoff, hub memory and locked-doc rules
# Layer:    Hooks
# Owns:     (script, no public types)
# Depends:  (none)
# =============================================================================

import json
import ntpath
import os
import re
import shlex
import subprocess
import sys
from pathlib import Path

WATCHED_TOOLS = ("Write", "Edit", "SendMessage", "Bash")
HANDOFF_PREFIX = "HANDOFF -"
SKILL_NAME = "handoff"
COMMAND_MARKER = re.compile(r"<command-name>/(?:[^<]*:)?handoff</command-name>")
NO_PROOF_REASON = "this session's transcript shows no `handoff` invoked. A brief placed under `memory/handoffs/`, by Write or by `hub.py write --brief`, and a `HANDOFF` line go through it: invoke the `handoff` skill, which places the brief and sends `HANDOFF` itself."
SUBAGENT_REASON = "a hand-off runs in the session through the `handoff` skill, never in an agent. Stop, and report to the session that dispatched you."
PUNCTUATION = "();<>|&\n"
REDIRECTS = frozenset(("<", ">", ">>", "<<", "<<<", ">&", "<&", "&>", "&>>", ">|", "<>"))
WATCHED = ("Write", "Edit", "NotebookEdit")
DRIVE_ABSOLUTE = re.compile(r"^[A-Za-z]:[\\/]")
HUB_CHECKOUT = re.compile(r"the main checkout is `([^`]+)`")
MANIFEST_REL = ".claude/harness.json"
BRANCH_TIMEOUT = 5
PROGRAM = """import importlib.util
import json
import pathlib
import sys
spec = importlib.util.spec_from_file_location("hub", sys.argv[1])
hub = importlib.util.module_from_spec(spec)
sys.modules["hub"] = hub
spec.loader.exec_module(hub)
try:
    backend = hub.backend_for(pathlib.Path(sys.argv[2]))
    print(json.dumps({"branch": backend.branch(), "main": backend.main_name(None)}))
except hub.Refusal:
    sys.exit(1)
"""
REASON = "{target} lies under the hub's memory/, which is written only through the base's scripts/hub.py, never by Edit, Write or NotebookEdit. Draft the change under this tree's .scratch/ and apply it with scripts/hub.py write --root {hub}."
LOCK_WATCHED = ("Write", "Edit")
LOCK_LINE = re.compile(r"status:\s*['\"]?locked\b")
DESIGN_PREFIX = "design-"
HEAD_BYTES = 65536
LOCKED_REASON = "`{target}` sets or edits a locked doc's `status: locked` off a design branch (`{branch}`); a doc is locked only on a `design-` branch at G1 (`g1-lock` LOCK), and a locked doc changes only there."
LOCKED_READ_REASON = "`{target}` sets or edits a locked doc's `status: locked`, and the branch read for `{root}` through the base's scripts/hub.py failed or came back empty, so it is refused as off a design branch; a doc is locked only on a `design-` branch at G1 (`g1-lock` LOCK), and a locked doc changes only there."


def parse_payload(data):
    try:
        document = json.loads(data)
    except (ValueError, RecursionError):
        return None
    return document if isinstance(document, dict) else None


def tokenize(command):
    lexer = shlex.shlex(command, posix=True, punctuation_chars=PUNCTUATION)
    lexer.whitespace_split = True
    lexer.escape = ""
    lexer.commenters = ""
    lexer.whitespace = lexer.whitespace.replace("\n", "")
    try:
        return list(lexer)
    except ValueError:
        tokens = []
        for line in command.split("\n"):
            tokens.extend(line.split())
            tokens.append("\n")
        return tokens


def is_break(token):
    if token in ("{", "}"):
        return True
    return (bool(token) and all(character in PUNCTUATION for character in token)
            and token not in REDIRECTS)


def command_name(token):
    name = token.replace("\\", "/").rsplit("/", 1)[-1].lower()
    return name[:-len(".exe")] if name.endswith(".exe") else name


def places_brief(command):
    command = command.replace("\\\r\n", " ").replace("\\\n", " ")
    tokens = tokenize(command)
    segments = [[]]
    for token in tokens:
        if is_break(token):
            segments.append([])
        else:
            segments[-1].append(token)
    for segment in segments:
        names = [command_name(word) for word in segment]
        if "hub.py" not in names:
            continue
        rest = segment[names.index("hub.py") + 1:]
        if "write" not in rest:
            continue
        for word in rest[rest.index("write") + 1:]:
            prefix = word.split("=", 1)[0]
            if len(prefix) >= 3 and "--brief".startswith(prefix):
                return True
    return any(token != command and len(token.split()) > 1 and places_brief(token)
               for token in tokens)


def trees_section(text):
    heading = re.compile(r"^#{2,}\s*trees\b", re.IGNORECASE)
    body = None
    for line in text.splitlines():
        if body is not None and re.match(r"^#{1,6}\s", line):
            break
        if body is not None:
            body.append(line)
        elif heading.match(line):
            body = []
    return body


def absent_form(lines):
    stripped = [line.strip() for line in lines]
    content = [line for line in stripped
               if line and not (line.startswith("<!--") and line.endswith("-->"))]
    if not content:
        return True
    if any("TODO" in line for line in lines):
        return True
    return len(content) == 1 and re.match(
        r"none(\s|\N{EM DASH}|$)", content[0].removeprefix("- ").replace("`", "")) is not None


def trees_file(root, fallback):
    name = fallback
    try:
        with open(ntpath.join(root, ".claude", "harness.json"), encoding="utf-8") as handle:
            document = json.load(handle)
    except FileNotFoundError:
        document = {}
    except (OSError, ValueError, RecursionError):
        document = None
    if isinstance(document, dict):
        filler = document.get("bindings")
        if not isinstance(filler, dict):
            filler = {}
        named = filler.get("trees", "core")
        if (isinstance(named, str) and re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]*", named)
                and ".." not in named):
            name = named
    if name is None:
        return None
    return ntpath.join(root, ".claude", "bindings", name + ".md")


def trees_lines(root):
    path = trees_file(root, "core")
    if path is None:
        return None
    try:
        with open(path, encoding="utf-8") as handle:
            text = handle.read()
    except (OSError, ValueError):
        return None
    return trees_section(text)


def trees_filled(root):
    if root is None:
        return False
    lines = trees_lines(root)
    if lines is None or absent_form(lines):
        return False
    return True


def watched(payload):
    if payload.get("tool_name") not in WATCHED_TOOLS:
        return False
    tool_input = payload.get("tool_input")
    if not isinstance(tool_input, dict):
        return False
    if payload["tool_name"] == "Bash":
        command = tool_input.get("command")
        return isinstance(command, str) and places_brief(command)
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
    if not isinstance(transcript, str) or not transcript:
        return False
    try:
        lines = Path(transcript).read_text(encoding="utf-8").split("\n")
    except (OSError, ValueError):
        return False
    for line in lines:
        try:
            entry = json.loads(line)
        except (ValueError, RecursionError):
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
                        and isinstance(block["input"].get("skill"), str)
                        and (block["input"]["skill"] == SKILL_NAME
                             or block["input"]["skill"].endswith(":" + SKILL_NAME))):
                    return True
        elif entry.get("type") == "user" and isinstance(content, str):
            if COMMAND_MARKER.search(content):
                return True
    return False


def handoff_decide(payload, root=None):
    if not watched(payload):
        return None
    if payload["tool_name"] != "SendMessage":
        filled = trees_filled(root)
        if payload["tool_name"] == "Bash" and not filled:
            return None
        if payload["tool_name"] in ("Write", "Edit") and filled:
            return None
    if "agent_id" in payload:
        return SUBAGENT_REASON
    if proof(payload.get("transcript_path")):
        return None
    return NO_PROOF_REASON


def target(payload):
    if payload.get("tool_name") not in WATCHED:
        return None
    tool_input = payload.get("tool_input")
    if not isinstance(tool_input, dict):
        return None
    for name in ("file_path", "notebook_path"):
        value = tool_input.get(name)
        if isinstance(value, str):
            return value
    return None


def key(path):
    return ntpath.normpath(path).replace("/", "\\").casefold()


def hub_path(lines):
    line = next((line for line in lines if line.startswith("- Hub:")), None)
    match = HUB_CHECKOUT.search(line) if line is not None else None
    if match is None or not DRIVE_ABSOLUTE.match(match.group(1)):
        return None
    path = ntpath.normpath(match.group(1))
    return None if len(path.rstrip("\\/")) == 2 else path


def beneath(target_key, hub_key):
    return target_key.startswith(hub_key + "\\memory\\")


def hub_script(root):
    manifest = Path(root) / MANIFEST_REL
    if not manifest.is_file():
        return None
    try:
        document = json.loads(manifest.read_text(encoding="utf-8"))
    except (OSError, ValueError, RecursionError):
        return None
    base = document.get("base") if isinstance(document, dict) else None
    if not isinstance(base, str) or not base:
        return None
    script = Path(root) / base / "scripts/hub.py"
    return script if script.is_file() else None


def hub_branch(script, hub):
    try:
        result = subprocess.run([sys.executable, "-B", "-c", PROGRAM, str(script), hub],
                                stdin=subprocess.DEVNULL, capture_output=True,
                                timeout=BRANCH_TIMEOUT)
    except (subprocess.TimeoutExpired, OSError):
        return None
    if result.returncode != 0:
        return None
    try:
        document = json.loads(result.stdout.decode("utf-8"))
    except (ValueError, RecursionError):
        return None
    if not isinstance(document, dict):
        return None
    branch, main = document.get("branch"), document.get("main")
    if not isinstance(branch, str) or not isinstance(main, str):
        return None
    return branch, main


def branch_read(root, checkout, reads):
    name = key(root)
    if name not in reads:
        script = hub_script(root)
        reads[name] = None if script is None else hub_branch(script, checkout)
    return reads[name]


def hub_memory_decide(payload, root, reads=None):
    path = target(payload)
    if path is None or not DRIVE_ABSOLUTE.match(path):
        return None
    if "memory" not in key(path).split("\\"):
        return None
    if root is None:
        return None
    lines = trees_lines(root)
    if lines is None or absent_form(lines):
        return None
    hub = hub_path(lines)
    if hub is None:
        return None
    if not beneath(key(path), key(hub)):
        return None
    reason = REASON.format(target=path, hub=hub)
    if key(root) != key(hub):
        return reason
    answer = branch_read(root, hub, {} if reads is None else reads)
    if answer is None:
        return reason
    branch, main = answer
    if not branch or branch == main:
        return reason
    return None


def front_matter(text):
    lines = text.split("\n")
    if lines[0].strip() != "---":
        return None
    block = []
    for line in lines[1:]:
        if line.strip() == "---":
            break
        block.append(line)
    return block


def holds_lock(lines):
    return any(LOCK_LINE.match(line) for line in lines)


def sets_lock(tool, tool_input):
    if tool == "Write":
        content = tool_input.get("content")
        lines = front_matter(content) if isinstance(content, str) else None
        return lines is not None and holds_lock(lines)
    new, old = tool_input.get("new_string"), tool_input.get("old_string")
    if not isinstance(new, str) or not holds_lock(new.split("\n")):
        return False
    return not (isinstance(old, str) and holds_lock(old.split("\n")))


def locked_on_disk(path):
    try:
        with open(path, "rb") as handle:
            head = handle.read(HEAD_BYTES)
    except OSError:
        return False
    lines = front_matter(head.decode("utf-8", "replace"))
    return lines is not None and holds_lock(lines)


def locked_doc_decide(payload, root, reads=None):
    if payload.get("tool_name") not in LOCK_WATCHED:
        return None
    tool_input = payload.get("tool_input")
    if not isinstance(tool_input, dict):
        return None
    path = tool_input.get("file_path")
    if not isinstance(path, str) or not path.lower().endswith(".md"):
        return None
    if not sets_lock(payload["tool_name"], tool_input) and not locked_on_disk(path):
        return None
    answer = None if root is None else branch_read(root, root, {} if reads is None else reads)
    branch = "" if answer is None else answer[0]
    if not branch:
        return LOCKED_READ_REASON.format(target=path, root=root)
    if branch.rsplit("/", 1)[-1].startswith(DESIGN_PREFIX):
        return None
    return LOCKED_REASON.format(target=path, branch=branch)


def decide(payload, root=None):
    reads = {}
    reason = handoff_decide(payload, root)
    if reason is None:
        reason = hub_memory_decide(payload, root, reads)
    if reason is None:
        reason = locked_doc_decide(payload, root, reads)
    return reason


def deny_document(reason):
    return json.dumps({"hookSpecificOutput": {"hookEventName": "PreToolUse",
                                              "permissionDecision": "deny",
                                              "permissionDecisionReason": reason}})


def main(argv):
    root = argv[1] if len(argv) > 1 and os.path.isdir(argv[1]) else None
    payload = parse_payload(sys.stdin.buffer.read())
    if payload is None:
        return 0
    reason = decide(payload, root)
    if reason is not None:
        print(deny_document(reason))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
