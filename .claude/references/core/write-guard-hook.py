# =============================================================================
# Purpose:  PreToolUse write guard holding the handoff, hub memory and locked-doc rules
# Layer:    Hooks
# Owns:     (script, no public types)
# Depends:  (none)
# =============================================================================
"""Claude Code `PreToolUse` hook on `Write`, `Edit`, `NotebookEdit`, `SendMessage`
and, for a call holding `--b`, `Bash`: the write guard, three rules in one
process. `decide` returns the first rule's refusal - the handoff rule's, then the
hub memory rule's, then the locked-doc rule's - and silence where none refuses.

The handoff rule: a hand-off goes through the `handoff` skill, enforced rather
than remembered. The watches follow the project root (argv[1]) and the `trees`
section read from the bindings file beneath it the manifest's `bindings` filler
names for `trees`, `core`'s by default and for a manifest that cannot be read.
With `trees` filled, a Bash call placing a brief - `hub.py write` with `--brief`,
or any prefix of it from `--b` - is watched, and a Write or Edit under a
`memory/handoffs/` directory is not. With `trees` absent, unreadable, or no
argument, the reverse: the Write or Edit is watched, and the Bash call is not. A
SendMessage whose `message` begins `HANDOFF -` is watched in both. The bindings
are read only for a call already watched by its shape; every other call gets
silence, with no file and no transcript read. A watched call from a subagent is
refused outright; a watched call from the session passes when its transcript
shows `handoff` invoked - the Skill tool naming `handoff`, or a directory-scoped
`<scope>:handoff`, or the human's typed `/handoff` or `/<scope>:handoff` - and is
refused otherwise, a transcript that cannot be read included.

The hub memory rule refuses a Write, Edit or NotebookEdit aimed beneath the hub's
`memory/`, since that folder is written only through the base's `scripts/hub.py`.
The hub is the main checkout the `- Hub:` line of the `trees` section names.
Paths are compared lexically in Windows form through `ntpath`: `..` resolved,
either separator, case-folded; a target is the hub's `memory/` when it lies
strictly beneath `<hub>\\memory\\`. The checks run in cost order, and every input
the rule cannot read up to that comparison is silence: a tool not watched, a
target that is not drive-absolute or holds no `memory` component, no root, a
`trees` section absent or unreadable, no readable Hub path. Beneath the hub's
`memory/`, a session whose root is not the hub is refused with no process
started. The hub's own session passes only on a branch other than the main
branch, read through the base's `scripts/hub.py` in one child under a timeout;
every other answer - no manifest, no `hub.py`, a refused read, an empty branch, a
timeout, output not the object - is refused as the main branch.

The locked-doc rule refuses, off a `design-` branch, a Write or Edit of a `.md`
file that sets a front-matter `status` of `locked` - a Write whose `content` opens
`---` and whose front matter holds such a line, an Edit whose `new_string` holds
one and whose `old_string` holds none - or whose current front matter on disk,
read from the file's head, already holds one. The branch is read only on such a
hit, through the same `hub.py` child, from the project root; its last `/`
component starting `design-` passes. A failed or empty read is refused.

The branch is read at most once per call, whichever rules ask for it.

Standard library only, and never an import of `harness.py` - the hook runs under
whatever interpreter the platform starts, and `harness.py` exits at import time
when PyYAML or jsonschema is missing.

A denial is an affirmative document at exit 0; an allowed call is silence, which
is no opinion rather than approval. There is no generic catch anywhere here, so an
unforeseen bug is a traceback and a non-zero exit, a non-blocking error under
which the call passes.
"""

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
# Copied verbatim from the rm fence (rm-fence-hook.py).
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
    """The hook payload as a mapping, or None for stdin the guard cannot read.

    None covers bytes that are not JSON at all, bytes that are not UTF-8, and JSON
    that parses to a list, a string or a number rather than to an object.
    """
    try:
        document = json.loads(data)
    except (ValueError, RecursionError):
        return None
    return document if isinstance(document, dict) else None


# Copied verbatim from the rm fence (rm-fence-hook.py).
def tokenize(command):
    """The command's words and operators: quotes group, backslashes stay literal,
    and a newline is an operator character; unbalanced quotes fall back to a
    whitespace split with a newline token at each line's end."""
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
    """True for a token that ends a segment: a run of operator characters that is
    not a redirection, or a brace."""
    if token in ("{", "}"):
        return True
    return (bool(token) and all(character in PUNCTUATION for character in token)
            and token not in REDIRECTS)


def command_name(token):
    """The token's basename by either separator, lowercased, a trailing `.exe` dropped."""
    name = token.replace("\\", "/").rsplit("/", 1)[-1].lower()
    return name[:-len(".exe")] if name.endswith(".exe") else name


def places_brief(command):
    """True when the command places a brief, read by `hub.py`'s own grammar.

    Each line continuation, a backslash before a newline, is made a space; the
    command is tokenized and split into segments at each break. A segment places a
    brief when it holds a word whose `command_name` is `hub.py`, a later word
    `write`, and a later word that is `--brief` or a prefix of it from `--b`, alone
    or with `=<file>`. A token holding whitespace that is not the command itself -
    a `bash -c` string - is read again by the same rule.
    """
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
        r"none(\s|\N{EM DASH}|$)", content[0].removeprefix("- ").replace("`", "")) is not None


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


def trees_filled(root):
    """True when the `trees` section beneath `root` reads filled; False for no root,
    no section or a file that cannot be read on the way, and the absent form."""
    if root is None:
        return False
    lines = trees_lines(root)
    if lines is None or absent_form(lines):
        return False
    return True


def watched(payload):
    """True for the calls the handoff rule governs by their shape, False for every other.

    A Write or Edit is watched when its `file_path`, split on either separator and
    compared case-insensitively, holds `memory`, `handoffs` and a non-empty
    component, adjacent. A SendMessage is watched when its `message`, leading
    whitespace stripped, begins `HANDOFF -`; never `content`, which carries a
    truncated copy. A Bash call is watched when its `command` is a string that
    places a brief (`places_brief`). Which of the Write or Edit and the Bash watch
    holds is `handoff_decide`'s, by `trees`; no file is read here.
    """
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
    """True when the transcript shows `handoff` invoked, read once.

    The proof is an `assistant` entry holding a `tool_use` block named `Skill`
    whose input's `skill` is `handoff`, or a directory-scoped `<scope>:handoff`,
    or a `user` entry whose string content holds the typed command's marker,
    `/handoff` or `/<scope>:handoff`. False for a path that is not a non-empty
    string and for a transcript that is missing, unreadable or not UTF-8. Lines
    are split on the newline alone, never on a separator JSON allows raw in a
    string; a line that does not parse is skipped.
    """
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
            # The scoped typed form `/<scope>:handoff` is unprobed: no transcript
            # has shown it; accepted by ruling on 2026-10-17.
            if COMMAND_MARKER.search(content):
                return True
    return False


def handoff_decide(payload, root=None):
    """The handoff rule: the reason to deny with, or None to allow.

    An unwatched call is decided before any file is read. A watched Bash or Write
    or Edit then reads `trees` beneath `root`, None reading as absent: a Bash call
    is silence unless `trees` is filled, a Write or Edit is silence when it is. A
    SendMessage reads no bindings. A call still watched from a subagent, whose
    payload carries `agent_id`, is refused before any transcript is read.
    """
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
    except (OSError, ValueError, RecursionError):
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
    except (ValueError, RecursionError):
        return None
    if not isinstance(document, dict):
        return None
    branch, main = document.get("branch"), document.get("main")
    if not isinstance(branch, str) or not isinstance(main, str):
        return None
    return branch, main


def branch_read(root, checkout, reads):
    """`(branch, main)` of `checkout` through the `hub.py` of `root`'s base
    (`hub_script`, `hub_branch`), read at most once per call: `reads` keeps each
    answer by `root`'s key. None for no script or a failed read."""
    name = key(root)
    if name not in reads:
        script = hub_script(root)
        reads[name] = None if script is None else hub_branch(script, checkout)
    return reads[name]


def hub_memory_decide(payload, root, reads=None):
    """The hub memory rule: the reason to deny with, or None for silence: silent
    until the target is known to lie beneath the hub's `memory/`, refused after
    unless the root is the hub and the hub is on a branch other than the main
    branch. `reads` is the call's branch-read cache, a fresh one for None."""
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
    """The front-matter lines of `text`: those after a first line reading `---`, up
    to the next such line or the end; None where the first line is not `---`."""
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
    """True when one of `lines` opens with a `status` of `locked` (LOCK_LINE)."""
    return any(LOCK_LINE.match(line) for line in lines)


def sets_lock(tool, tool_input):
    """True for a Write whose `content` opens `---` and whose front matter holds a
    locked `status` line, or an Edit whose `new_string` holds such a line and whose
    `old_string` holds none; False for any other shape."""
    if tool == "Write":
        content = tool_input.get("content")
        lines = front_matter(content) if isinstance(content, str) else None
        return lines is not None and holds_lock(lines)
    new, old = tool_input.get("new_string"), tool_input.get("old_string")
    if not isinstance(new, str) or not holds_lock(new.split("\n")):
        return False
    return not (isinstance(old, str) and holds_lock(old.split("\n")))


def locked_on_disk(path):
    """True when the file at `path` opens with front matter holding a locked
    `status` line, read from its first HEAD_BYTES bytes in this process; False for
    a file that cannot be opened or read."""
    try:
        with open(path, "rb") as handle:
            head = handle.read(HEAD_BYTES)
    except OSError:
        return False
    lines = front_matter(head.decode("utf-8", "replace"))
    return lines is not None and holds_lock(lines)


def locked_doc_decide(payload, root, reads=None):
    """The locked-doc rule: the reason to deny with, or None for silence.

    Watched: a Write or Edit whose `file_path` ends `.md`, any letter case. A hit
    is a call that sets the lock (`sets_lock`) or a target whose current front
    matter holds it (`locked_on_disk`); only a hit reads the branch, through the
    `hub.py` of the project root's base, from the root itself, `reads` the call's
    cache, a fresh one for None. The branch's last `/` component starting
    `design-` passes; any other branch is refused, and a failed or empty read, or
    no root, is refused naming the failed read.
    """
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
    """The reason to deny with, or None to allow: the first refusal of the handoff
    rule, the hub memory rule and the locked-doc rule, in that order, the last two
    sharing one branch read."""
    reads = {}
    reason = handoff_decide(payload, root)
    if reason is None:
        reason = hub_memory_decide(payload, root, reads)
    if reason is None:
        reason = locked_doc_decide(payload, root, reads)
    return reason


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
