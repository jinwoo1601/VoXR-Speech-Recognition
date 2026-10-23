# =============================================================================
# Purpose:  PostToolUse hook signalling a session past its session-launch Context bound
# Layer:    Hooks
# Owns:     (script, no public types)
# Depends:  (none)
# =============================================================================
"""Claude Code `PostToolUse` hook on every tool call: the context-bound hook, a
nudge and never a guard. Past the `session-launch` binding's *Context bound* it
puts one line into the model's context - finish the step in hand, then invoke
`handoff`, its objective the next step - on every call; at or below the bound it
says nothing.

A payload carrying a non-empty `agent_id`, a subagent's tool call, is silence
before anything else is read. The project root is argv[1] where it names a
directory, else the payload's `cwd`. The bound is read at run time from the
`session-launch` section of the bindings file beneath the root the manifest's
`bindings` filler names for `session-launch`, `core`'s by default and for a
manifest that cannot be read: the first `<n>k`, n times 1000, or bare integer on
the `- Context bound:` line. A bound that is absent, reads `none` or `TODO`, or
holds no number, and a `- Context read:` line that is absent or reads `none` or
`TODO`, are silence: the read slot stays the on/off switch and the human's manual
read.

The figure is the one the binding's *Context read* computes, read from the end of
the payload's `transcript_path`: the newest `assistant` entry whose
`message.usage` sum of `input_tokens`, `cache_creation_input_tokens` and
`cache_read_input_tokens` is above 0. The file is read backwards in BLOCK_BYTES
blocks, newest line first, at most SCAN_BYTES in all; a line cut by a block's
start is carried into the next block and never parsed alone. No such entry within
the cap is silence. Nothing checks whether the transcript is stale after a
`/clear`: that risk is deferral B152's open item.

Past the bound - the figure strictly above it - the hook prints one ASCII line, a
`hookSpecificOutput.additionalContext` document for `PostToolUse`. It always exits
0, and any error anywhere is silence, so a broken hook never blocks a tool call.

Standard library only, and never an import of `harness.py` - the hook runs under
whatever interpreter the platform starts, and `harness.py` exits at import time
when PyYAML or jsonschema is missing.
"""

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


# Copied verbatim from the write guard (write-guard-hook.py).
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


def project_root(payload, argument):
    """The project root: `argument`, argv[1], where it names a directory, else the
    payload's `cwd` where that does; None for neither."""
    for candidate in (argument, payload.get("cwd")):
        if isinstance(candidate, str) and candidate and os.path.isdir(candidate):
            return candidate
    return None


def bindings_file(root):
    """The path of the bindings file beneath `root` the manifest's `bindings` filler
    names for `session-launch`, as the write guard's `trees_file` reads it for
    `trees`: `core`'s with no manifest, one that cannot be read or is not an object,
    no `bindings` object, no `session-launch` key, or a value naming no bare stem."""
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


# Copied verbatim from the doctor hook (doctor-hook.py).
def binding_section(text, key):
    """The body lines of the `<key>` section of a bindings file, or None:
    under the first heading of two or more `#` reading it, up to the next heading.
    """
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
    """The text after the colon of the first `- <label>:` line, stripped; None
    where there is no such line."""
    for line in lines:
        stripped = line.strip()
        if stripped.startswith(f"- {label}:"):
            return stripped[len(f"- {label}:"):].strip()
    return None


def unbound(text):
    """True for a slot that is absent or reads `TODO` or `none`, in any letter
    case, as the doctor hook's `slot_value` reads one."""
    return text is None or text.lower().startswith(("todo", "none"))


def context_bound(root):
    """The *Context bound* in tokens beneath `root`, or None for silence: a bindings
    file that cannot be read, no `session-launch` section, the bound or the
    *Context read* unbound (`unbound`), or a bound holding no number. The first
    `<n>k` is n times 1000; a bare integer is itself."""
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
    """The context one transcript line records: for an `assistant` entry, the sum
    of its `message.usage` USAGE_KEYS, each counted where it is an integer; 0 for a
    line that does not parse and for any other entry."""
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
    """The context of the newest `assistant` entry above 0 in the transcript at
    `path` (`usage_sum`), read backwards from its end in BLOCK_BYTES blocks, at most
    SCAN_BYTES in all; None where no such entry lies within the cap. The line a
    block's start cuts is carried into the next block, and dropped at the cap."""
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
    """The line to print, or None for silence.

    A non-empty `agent_id` is silence before anything is read. Then the project
    root (`project_root`), the bound (`context_bound`) and the transcript the
    payload's `transcript_path` names (`context_figure`) are read in that order,
    each missing one silence; a figure at or below the bound is silence. An error
    on the way raises, and `main` makes it silence.
    """
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
