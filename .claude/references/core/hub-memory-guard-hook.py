# =============================================================================
# Purpose:  PreToolUse guard refusing an Edit, Write or NotebookEdit beneath the hub's memory/
# Layer:    Hooks
# Owns:     (script, no public types)
# Depends:  (none)
# =============================================================================
"""Claude Code `PreToolUse` hook on `Write`, `Edit` and `NotebookEdit`: the hub
memory guard, which refuses a file tool aimed beneath the hub's `memory/`, since
that folder is written only through the base's `scripts/hub.py`.

Standard library only, and never an import of `harness.py`, for the agent
guard's reason: the hook runs under whatever interpreter the platform starts.

The hub is the main checkout the `- Hub:` line of the `trees` section names, read
from the bindings file beneath the project root (argv[1]) the manifest's `bindings`
filler names for `trees`, `core`'s by default and for a manifest that cannot be
read. Paths are compared lexically in Windows form through `ntpath`: `..`
resolved, either separator, case-folded; a target is the hub's `memory/` when it
lies strictly beneath `<hub>\\memory\\`. The checks run in cost order, and every
input the guard cannot read up to that comparison is silence: stdin, a tool not
watched, a target that is not drive-absolute or holds no `memory` component, no
root, a `trees` section absent or unreadable, no readable Hub path.

Beneath the hub's `memory/`, a session whose root is not the hub is refused with
no process started. The hub's own session passes only on a branch other than the
main branch, read through the base's `scripts/hub.py` in one child under a
timeout; every other answer - no manifest, no `hub.py`, a refused read, an empty
branch, a timeout, output not the object - is refused as the main branch.

A denial is an affirmative document at exit 0; silence is no opinion. There is no
generic catch anywhere here, so an unforeseen bug is a traceback and a non-zero
exit, and neither denies.
"""

import json
import ntpath
import os
import re
import subprocess
import sys
from pathlib import Path

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


def target(payload):
    """The path a watched tool writes: the first string of `tool_input`'s
    `file_path` and `notebook_path`; None for any other tool or shape."""
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
    """`path` as compared: normalized lexically, `\\` separators, case-folded."""
    return ntpath.normpath(path).replace("/", "\\").casefold()


# Copied verbatim from the rm fence (rm-fence-hook.py).
def trees_section(text):
    """The body lines of the `trees` section of a bindings file, or None when absent:
    under the first heading of two or more `#` reading `trees`, up to the next heading.
    """
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
    """True where the `trees` section's lines read absent by the worktree hook's
    rules - empty, `TODO` or `none`, checked in that order - else False.

    A content line is one that, stripped, is neither blank nor a comment. The
    `none` form is a lone content line reading `none` in lowercase, bare or
    followed by whitespace or an em dash and a reason.
    """
    stripped = [line.strip() for line in lines]
    content = [line for line in stripped
               if line and not (line.startswith("<!--") and line.endswith("-->"))]
    if not content:
        return True
    if any("TODO" in line for line in lines):
        return True
    return len(content) == 1 and re.match(
        r"none(\s|\u2014|$)", content[0].removeprefix("- ").replace("`", "")) is not None


# Copied verbatim from the rm fence (rm-fence-hook.py).
def trees_file(root, fallback):
    """The path of the bindings file beneath `root` the manifest's `bindings` filler
    names for `trees`, as compose reads it: `core`'s with no manifest, no `bindings`
    object or no `trees` key. A manifest that cannot be read, is not an object, or
    names no bare stem resolves to `fallback` instead: None for None, else that
    stem's file."""
    name = fallback
    try:
        with open(ntpath.join(root, ".claude", "harness.json"), encoding="utf-8") as handle:
            document = json.load(handle)
    except FileNotFoundError:
        document = {}
    except (OSError, ValueError):
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
    """The body lines of the `trees` section in the bindings file the manifest's
    `bindings` filler names for `trees`, `core`'s by default and `core`'s for a
    manifest that cannot be read, and in no other; None for no such section or a
    file that cannot be read."""
    path = trees_file(root, "core")
    if path is None:
        return None
    try:
        with open(path, encoding="utf-8") as handle:
            text = handle.read()
    except (OSError, ValueError):
        return None
    return trees_section(text)


def hub_path(lines):
    """The main checkout the first `- Hub:` line names, normalized by `ntpath` with
    its case kept; None where there is no such line, it lacks the sentence, the
    path is not drive-absolute, or it names a drive root."""
    line = next((line for line in lines if line.startswith("- Hub:")), None)
    match = HUB_CHECKOUT.search(line) if line is not None else None
    if match is None or not DRIVE_ABSOLUTE.match(match.group(1)):
        return None
    path = ntpath.normpath(match.group(1))
    return None if len(path.rstrip("\\/")) == 2 else path


def beneath(target_key, hub_key):
    """True when the target's key lies strictly beneath the hub's `memory\\`."""
    return target_key.startswith(hub_key + "\\memory\\")


def hub_script(root):
    """`<root>/<base>/scripts/hub.py`, `base` read from the manifest as the doctor
    hook reads it; None for no manifest, one that does not parse, no usable
    `base`, or no script where it points."""
    manifest = Path(root) / MANIFEST_REL
    if not manifest.is_file():
        return None
    try:
        document = json.loads(manifest.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return None
    base = document.get("base") if isinstance(document, dict) else None
    if not isinstance(base, str) or not base:
        return None
    script = Path(root) / base / "scripts/hub.py"
    return script if script.is_file() else None


def hub_branch(script, hub):
    """`(branch, main)` as `script`'s backend for `hub` reads them, in one child
    under BRANCH_TIMEOUT; None for a timeout, a child that cannot start, any exit
    but 0, or stdout that is not a UTF-8 JSON object of two strings."""
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
    except ValueError:
        return None
    if not isinstance(document, dict):
        return None
    branch, main = document.get("branch"), document.get("main")
    if not isinstance(branch, str) or not isinstance(main, str):
        return None
    return branch, main


def decide(payload, root):
    """The reason to deny with, or None for silence: silent until the target is
    known to lie beneath the hub's `memory/`, refused after unless the root is the
    hub and the hub is on a branch other than the main branch."""
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
    script = hub_script(root)
    if script is None:
        return reason
    answer = hub_branch(script, hub)
    if answer is None:
        return reason
    branch, main = answer
    if not branch or branch == main:
        return reason
    return None


def deny_document(reason):
    """The denial, one line: the guard's only output, and never an `allow`."""
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
