# =============================================================================
# Purpose:  PreToolUse write guard holding the hub memory rule
# Layer:    Hooks
# Owns:     (script, no public types)
# Depends:  hook-common.py
# =============================================================================

import json
import ntpath
import os
import re
import subprocess
import sys
from pathlib import Path

try:
    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "hook-common.py"), "rb") as common:
        exec(compile(common.read(), common.name, "exec"), globals())
    COMMON_ERROR = None
except Exception as error:
    COMMON_ERROR = error

WATCHED =("Write", "Edit", "NotebookEdit")
DRIVE_ABSOLUTE = re.compile(r"^[A-Za-z]:[\\/]")
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
    root = pathlib.Path(sys.argv[2])
    backend = hub.backend_for(root)
    try:
        main = hub.main_branch(root, hub.manifest_fillers(root), backend)
    except hub.Refusal as error:
        if not str(error).endswith("## vc binds no Main branch"):
            raise
        main = backend.main_name(None)
    print(json.dumps({"branch": backend.branch(), "main": main}))
except hub.Refusal:
    sys.exit(1)
"""
REASON = "{target} lies under the hub's memory/, which is written only through the base's scripts/hub.py, never by Edit, Write or NotebookEdit. Draft the change under this tree's .scratch/ and apply it with scripts/hub.py write --root {hub}."
ERROR_REASON = "write-guard: internal error (%s: %s); the call was not checked"


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


def trees_lines(root):
    path = bindings_file(root, "trees")
    try:
        with open(path, encoding="utf-8") as handle:
            text = handle.read()
    except (OSError, ValueError):
        return None
    return binding_section(text, "trees")


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
    raw = trees_hub(lines)
    if raw is None or not DRIVE_ABSOLUTE.match(raw):
        return None
    path = ntpath.normpath(raw)
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
    base_dir = Path(root) / base
    if not base_dir.is_dir():
        lines = trees_lines(root)
        hub = hub_path(lines) if lines is not None and not absent_form(lines) else None
        if hub is not None:
            base_dir = Path(hub) / base
    script = base_dir / "scripts/hub.py"
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


def deny_document(reason):
    return json.dumps({"hookSpecificOutput": {"hookEventName": "PreToolUse",
                                              "permissionDecision": "deny",
                                              "permissionDecisionReason": reason}})


def main(argv):
    try:
        root = argv[1] if len(argv) > 1 and os.path.isdir(argv[1]) else None
        data = sys.stdin.buffer.read()
        if COMMON_ERROR is not None:
            raise COMMON_ERROR
        payload = parse_payload(data)
        if payload is None:
            return 0
        reason = hub_memory_decide(payload, root)
        document = None if reason is None else deny_document(reason)
    except Exception as error:
        document = json.dumps({"systemMessage": ERROR_REASON % (type(error).__name__, error)})
    if document is not None:
        print(document)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
