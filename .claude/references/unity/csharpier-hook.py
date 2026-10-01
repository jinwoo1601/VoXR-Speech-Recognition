#!/usr/bin/env python3
"""
Surgical CSharpier formatting for Claude Code edits.

Problem: running `csharpier format <file>` on every edit reformats the WHOLE
file, so a two-line change lands in version control as a hundred-line diff.

Fix: two hooks around each Edit/Write.
  snapshot (PreToolUse)  - stash the file's pre-edit bytes.
  format   (PostToolUse) - format the post-edit content via csharpier's stdin
                           mode (the file on disk is never handed to csharpier,
                           so nothing is written unless we decide to), then keep
                           the formatter's version ONLY for lines the edit
                           actually touched. Every other line is written back
                           byte-identical to what it was before.

Alignment detail: csharpier moves whitespace and nothing else, bar the trailing
comma it adds to or removes from a multi-line initializer. So stripping the
whitespace out of the edited and the formatted content yields two byte streams
that differ only in commas - close enough to map every code character in one to
its counterpart in the other. Splicing on that map, rather than on diffed lines,
is what makes this safe on files that are not csharpier-clean overall: a re-flow
that splits or joins lines can never smear across the edit boundary or drop
code. If the streams ever differ by more than commas, the map is not trusted and
we format the whole file rather than guess.

A missing snapshot (new file, or the Pre hook did not run) means "format the
whole file" - the old behaviour, logged so it cannot rot unnoticed.

Never blocks an edit: always exits 0, failures go to csharpier-format.log.
"""

import difflib
import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile
import time

PROJECT_DIR = os.environ.get("CLAUDE_PROJECT_DIR")
BASE_DIR = (os.path.join(PROJECT_DIR, ".claude", "csharpier") if PROJECT_DIR
            else os.path.join(tempfile.gettempdir(), "csharpier"))
SNAP_DIR = os.path.join(BASE_DIR, "snapshots")
LOG = os.path.join(BASE_DIR, "csharpier-format.log")
SNAP_MAX_AGE = 24 * 60 * 60
BOM = b"\xef\xbb\xbf"
WHITESPACE = frozenset(b" \t\n\r\x0b\x0c")
COMMA = ord(",")


def log(message):
    try:
        os.makedirs(BASE_DIR, exist_ok=True)
        with open(LOG, "a") as handle:
            handle.write("%s  %s\n" % (time.strftime("%Y-%m-%dT%H:%M:%S%z"), message))
    except OSError:
        pass


def read_file_path():
    """Claude Code passes the hook payload as JSON on stdin."""
    try:
        payload = json.load(sys.stdin)
    except Exception:
        return ""
    tool_input = payload.get("tool_input") or {}
    path = tool_input.get("file_path") or ""
    return path if path.endswith(".cs") else ""


def snapshot_path(file_path):
    key = hashlib.sha1(os.path.abspath(file_path).encode("utf-8")).hexdigest()
    return os.path.join(SNAP_DIR, key)


def prune_snapshots():
    cutoff = time.time() - SNAP_MAX_AGE
    try:
        entries = os.listdir(SNAP_DIR)
    except OSError:
        return
    for name in entries:
        path = os.path.join(SNAP_DIR, name)
        try:
            if os.path.getmtime(path) < cutoff:
                os.remove(path)
        except OSError:
            pass


def do_snapshot(file_path):
    try:
        os.makedirs(SNAP_DIR, exist_ok=True)
    except OSError as error:
        log("SNAPSHOT-FAILED  %s\n%s" % (file_path, error))
        return
    prune_snapshots()
    try:
        content = open(file_path, "rb").read()
    except FileNotFoundError:
        content = b""  # new file: every line is new, so the whole file formats
    except OSError as error:
        log("SNAPSHOT-FAILED  %s\n%s" % (file_path, error))
        return
    try:
        with open(snapshot_path(file_path), "wb") as handle:
            handle.write(content)
    except OSError as error:
        log("SNAPSHOT-FAILED  %s\n%s" % (file_path, error))


def run_csharpier(file_path, content):
    """Format `content` as if it were `file_path`, without touching the file."""
    exe = shutil.which("csharpier")
    if exe is None:
        log("FAILED  %s\ncsharpier not found on PATH" % file_path)
        return None
    try:
        result = subprocess.run(
            [exe, "format", "--stdin-path", file_path],
            input=content,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=120,
        )
    except (OSError, subprocess.SubprocessError) as error:
        log("FAILED  %s\n%s" % (file_path, error))
        return None
    if result.returncode != 0:
        log("FAILED  %s\n%s" % (file_path, result.stderr.decode("utf-8", "replace")))
        return None
    if not result.stdout.strip():
        log("SKIPPED  %s  (csharpier returned nothing - ignored file?)" % file_path)
        return None
    return result.stdout


def match_bom(source, formatted):
    """csharpier drops a UTF-8 BOM when fed via stdin; keep the file's own."""
    if source.startswith(BOM) and not formatted.startswith(BOM):
        return BOM + formatted
    if not source.startswith(BOM) and formatted.startswith(BOM):
        return formatted[len(BOM) :]
    return formatted


def edited_lines(before, after):
    """Indices into `after` of lines the edit created or modified."""
    matcher = difflib.SequenceMatcher(None, before, after, autojunk=False)
    edited = set()
    for tag, _, _, after_start, after_end in matcher.get_opcodes():
        if tag == "equal":
            continue
        if after_start == after_end:  # pure deletion: flag the seam it left
            edited.add(after_start - 1)
            edited.add(after_start)
        else:
            edited.update(range(after_start, after_end))
    return edited


def code_positions(content):
    """Byte offset and line number of every non-whitespace byte, in order."""
    positions, lines = [], []
    offset = 0
    for index, line in enumerate(content.splitlines(keepends=True)):
        for column, byte in enumerate(line):
            if byte not in WHITESPACE:
                positions.append(offset + column)
                lines.append(index)
        offset += len(line)
    return positions, lines


def find(parent, node):
    while parent[node] != node:
        parent[node] = parent[parent[node]]
        node = parent[node]
    return node


def spliceable_lines(after_lines, formatted_lines, mapping, edited, line_count):
    """Which code characters the edit owns, once closed over shared lines.

    A line csharpier joined with its neighbours cannot be half-formatted, so any
    line sharing a formatted line with an edited one is pulled in too. Untouched
    code stays out, and every splice boundary lands on a line break in BOTH
    renderings - which is what keeps re-flows from smearing into the diff.
    """
    parent = list(range(line_count + max(formatted_lines, default=-1) + 2))
    for index, after_line in enumerate(after_lines):
        for formatted_index in range(mapping[index], mapping[index + 1]):
            root_a = find(parent, after_line)
            root_f = find(parent, line_count + formatted_lines[formatted_index])
            if root_a != root_f:
                parent[root_f] = root_a
    owned = {find(parent, line) for line in edited if 0 <= line < line_count}
    return [find(parent, line) in owned for line in after_lines]


def boundary_map(after_code, formatted_code):
    """Map each boundary between code characters onto the formatted stream.

    The two streams run in lockstep apart from csharpier's magic trailing
    commas, so a two-pointer walk aligns them; a comma the formatter added is
    handed to the run starting on its right. Returns None on any other
    difference - the signal that this file cannot be spliced safely.
    """
    mapping = [0] * (len(after_code) + 1)
    formatted_length = len(formatted_code)
    index = cursor = 0
    while index < len(after_code):
        if after_code[index] == COMMA and (
            cursor >= formatted_length or formatted_code[cursor] != COMMA
        ):
            mapping[index] = cursor  # comma the formatter dropped: maps to nothing
            index += 1
            continue
        skipped = cursor
        while cursor < formatted_length and formatted_code[cursor] != after_code[index]:
            if formatted_code[cursor] != COMMA:
                return None
            cursor += 1  # comma the formatter added
        if cursor >= formatted_length:
            return None
        mapping[index] = skipped
        index += 1
        cursor += 1
    while cursor < formatted_length:
        if formatted_code[cursor] != COMMA:
            return None
        cursor += 1
    mapping[len(after_code)] = formatted_length
    return mapping


def merge(after, formatted, edited):
    """Formatter's rendering where the edit landed, original rendering elsewhere.

    Walks the shared code-character stream in runs of same edited-ness. A run is
    emitted from its own side, including the whitespace that leads into it - so
    an edited run brings csharpier's indentation and blank lines with it, while
    an untouched run keeps the file's own layout byte for byte. Returns None if
    the file cannot be spliced.
    """
    after_positions, after_lines = code_positions(after)
    formatted_positions, formatted_lines = code_positions(formatted)
    total = len(after_positions)
    if total == 0 or not formatted_positions:
        return None
    mapping = boundary_map(b"".join(after.split()), b"".join(formatted.split()))
    if mapping is None:
        return None
    touched = spliceable_lines(
        after_lines, formatted_lines, mapping, edited, len(after.splitlines())
    )
    if not any(touched):
        return after

    merged = []
    start = 0
    while start < total:
        end = start
        while end < total and touched[end] == touched[start]:
            end += 1
        if touched[start]:
            first, last = mapping[start], mapping[end]
            if last > first:  # not > only when csharpier dropped the whole run
                gap = formatted_positions[first - 1] + 1 if first > 0 else 0
                merged.append(formatted[gap : formatted_positions[last - 1] + 1])
        else:
            gap = after_positions[start - 1] + 1 if start > 0 else 0
            merged.append(after[gap : after_positions[end - 1] + 1])
        start = end

    # Trailing whitespace / final newline belongs to whichever side ended the file.
    tail_source, tail_positions = (
        (formatted, formatted_positions) if touched[-1] else (after, after_positions)
    )
    merged.append(tail_source[tail_positions[-1] + 1 :])
    return b"".join(merged)


def do_format(file_path):
    snapshot = snapshot_path(file_path)
    try:
        before = open(snapshot, "rb").read()
        have_snapshot = True
    except OSError:
        before, have_snapshot = b"", False
        log("NO-SNAPSHOT  %s  (formatting whole file)" % file_path)
    try:
        os.remove(snapshot)
    except OSError:
        pass

    try:
        after = open(file_path, "rb").read()
    except OSError as error:
        log("FAILED  %s\n%s" % (file_path, error))
        return

    formatted = run_csharpier(file_path, after)
    if formatted is None:
        return
    formatted = match_bom(after, formatted)

    merged = None
    if have_snapshot:
        merged = merge(
            after,
            formatted,
            edited_lines(before.splitlines(keepends=True), after.splitlines(keepends=True)),
        )
        if merged is None:
            log("UNMAPPABLE  %s  (formatted whole file)" % file_path)
    if merged is None:
        merged = formatted

    if merged == after:
        return
    try:
        with open(file_path, "wb") as handle:
            handle.write(merged)
    except OSError as error:
        log("FAILED  %s\n%s" % (file_path, error))


def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else ""
    file_path = read_file_path()
    if not file_path:
        return
    if mode == "snapshot":
        do_snapshot(file_path)
    elif mode == "format":
        do_format(file_path)
    else:
        log("BAD-MODE  %r" % mode)


if __name__ == "__main__":
    main()
    sys.exit(0)
