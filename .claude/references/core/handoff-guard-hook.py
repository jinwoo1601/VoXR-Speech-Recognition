# =============================================================================
# Purpose:  PreToolUse guard refusing a hand-off made outside the handoff skill
# Layer:    Hooks
# Owns:     (script, no public types)
# Depends:  (none)
# =============================================================================
"""Claude Code `PreToolUse` hook on `Write`, `Edit`, `SendMessage` and `Bash`: a
hand-off goes through the `handoff` skill, enforced rather than remembered.

The watches follow the project root (argv[1]) and the `trees` section read from
the bindings file beneath it the manifest's `bindings` filler names for `trees`,
`core`'s by default and for a manifest that cannot be read. With `trees` filled, a
Bash call placing a brief - `hub.py write` with `--brief`, or any prefix of it from
`--b` - is watched, and a Write or Edit under a `memory/handoffs/` directory is not. With
`trees` absent, unreadable, or no argument, the reverse: the Write or Edit is
watched, and the Bash call is not. A SendMessage whose `message` begins
`HANDOFF -` is watched in both. The bindings are read only for a call already
watched by its shape; every other call gets silence, with no file and no
transcript read. A watched call from a subagent is refused outright; a watched
call from the session passes when its transcript shows `handoff` invoked - the
Skill tool naming it, or the human's typed `/handoff` - and is refused otherwise,
a transcript that cannot be read included.

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
import sys
from pathlib import Path

WATCHED_TOOLS = ("Write", "Edit", "SendMessage", "Bash")
HANDOFF_PREFIX = "HANDOFF -"
SKILL_NAME = "handoff"
COMMAND_MARKER = "<command-name>/handoff</command-name>"
NO_PROOF_REASON = "this session's transcript shows no `handoff` invoked. A brief placed under `memory/handoffs/`, by Write or by `hub.py write --brief`, and a `HANDOFF` line go through it: invoke the `handoff` skill, which places the brief and sends `HANDOFF` itself."
SUBAGENT_REASON = "a hand-off runs in the session through the `handoff` skill, never in an agent. Stop, and report to the session that dispatched you."
# Copied verbatim from the rm fence (rm-fence-hook.py).
PUNCTUATION = "();<>|&\n"
REDIRECTS = frozenset(("<", ">", ">>", "<<", "<<<", ">&", "<&", "&>", "&>>", ">|", "<>"))


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


# Copied verbatim from the hub memory guard (hub-memory-guard-hook.py).
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
    """True for the calls the guard governs by their shape, False for every other.

    A Write or Edit is watched when its `file_path`, split on either separator and
    compared case-insensitively, holds `memory`, `handoffs` and a non-empty
    component, adjacent. A SendMessage is watched when its `message`, leading
    whitespace stripped, begins `HANDOFF -`; never `content`, which carries a
    truncated copy. A Bash call is watched when its `command` is a string that
    places a brief (`places_brief`). Which of the Write or Edit and the Bash watch
    holds is `decide`'s, by `trees`; no file is read here.
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
    whose input's `skill` is `handoff`, or a `user` entry whose string content
    holds the typed command's marker. False for a path that is not a non-empty
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
        except ValueError:
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
                        and block["input"].get("skill") == SKILL_NAME):
                    return True
        elif entry.get("type") == "user" and isinstance(content, str):
            if COMMAND_MARKER in content:
                return True
    return False


def decide(payload, root=None):
    """The reason to deny with, or None to allow.

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
