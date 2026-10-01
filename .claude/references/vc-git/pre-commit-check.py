# =============================================================================
# Purpose:  Secret and conflict-marker scan of a staged change or a revision range
# Layer:    Scripts
# Owns:     (script, no public types)
# Depends:  (none)
# =============================================================================
"""`python3 .claude/references/vc-git/pre-commit-check.py [--range <base>..<head> | --install]`,
run from a tree of the repository: the mechanical half of the no-secrets-in-diff
check, `no-secrets-in-diff.md` beside this file, in three modes.

- No argument: git runs it as the `pre-commit` hook. The staged change is
  scanned; hits print `pre-commit-check: FAIL` and one row each, exit 1, and the
  commit is refused; no hit is silence and exit 0.
- `--range <base>..<head>`: that range is scanned. `PASS — <range>`, exit 0, or
  `FAIL — <range>` and the rows, exit 1, then the `pre-commit hook:` line for the
  repository; `UNVERIFIABLE — <reason>`, exit 2, where the range does not resolve.
- `--install`: the `pre-commit` shim is written into the repository's hooks
  directory; `already installed` where it is there already, and a refusal, exit
  1, where another hook holds the name.

Only added lines are scanned, and a row names the path, the line and the
category, never the matched value: a report that quotes a secret republishes it.
The patterns are assembled so that this file's own text never matches them.

Standard library only; git is run by argument list, its output read as bytes and
decoded UTF-8, and this script's own output written as UTF-8 bytes.
"""

import argparse
import os
import re
import stat
import subprocess
import sys

SELF = ".claude/references/vc-git/pre-commit-check.py"
SHIM = ("#!/bin/sh\n"
        f"# installed by {SELF} --install\n"
        f"[ -f {SELF} ] || exit 0\n"
        f"exec python3 {SELF}\n").encode("utf-8")

DASHES = "-" * 5
PRIVATE_KEY = re.compile(DASHES + "BEGIN (?:[A-Z0-9]+ )*PRIVATE KEY" + DASHES)
KEYWORD = re.compile(r"(?i)password|passwd|secret|token|api[_-]?key|access[_-]?key")
NAME_CHAR = re.compile(r"(?i)[A-Za-z0-9_.-]")
NAME_RUN = re.compile(r"(?i)[A-Za-z0-9_.-]*")
BOUNDARY = re.compile(r"\b")
ASSIGNED = re.compile(
    r"(?i)\b['\"]?\s*[:=]\s*"
    r"(?:(['\"])([^'\"\s]{8,})\1|([A-Za-z0-9+/_.~-]{8,}))")
EXPRESSION = re.compile(r"[A-Za-z_][A-Za-z0-9_]*[(.\[]")
URL = re.compile(r"[A-Za-z][A-Za-z0-9+.-]*://[^\s:/@]*:([^\s/@]+)@")
PROVIDER = re.compile("|".join((
    "AKIA[0-9A-Z]{16}", "ghp_[A-Za-z0-9]{36}", "github_pat_[A-Za-z0-9_]{20,}",
    "glpat-[A-Za-z0-9_-]{20,}", "xox[abprs]-[A-Za-z0-9-]{10,}", "sk-ant-[A-Za-z0-9_-]{20,}",
    "sk-[A-Za-z0-9]{32,}", "AIza[0-9A-Za-z_-]{35}")))
DIGEST = re.compile(r"^(?:sha1|sha224|sha256|sha384|sha512|md5):[0-9a-fA-F]+$")
PATH_EXTENSION = re.compile(r"\.[A-Za-z]{1,5}$")
PLACEHOLDER_WORDS = ("example", "placeholder", "changeme", "dummy", "fake", "redacted", "xxxx")
MARKER_OPEN, MARKER_CLOSE, MARKER_MIDDLE = "<" * 7 + " ", ">" * 7 + " ", "=" * 7
ENV_EXAMPLES = frozenset((".env.example", ".env.sample", ".env.template"))
KEY_FILES = frozenset(("id_rsa", "id_dsa", "id_ecdsa", "id_ed25519"))
HUNK = re.compile(r"@@ -\d+(?:,\d+)? \+(\d+)(?:,\d+)? @@")


def git(*args):
    """git as a child in the working directory: its exit code and its stdout,
    read as bytes and decoded UTF-8 with replacement."""
    result = subprocess.run(["git", *args], stdin=subprocess.DEVNULL, capture_output=True)
    return result.returncode, result.stdout.decode("utf-8", errors="replace")


def emit(lines):
    """`lines` on stdout as UTF-8 bytes, each ended by one LF."""
    sys.stdout.buffer.write("".join(line + "\n" for line in lines).encode("utf-8"))
    sys.stdout.buffer.flush()


def is_placeholder(value):
    """Whether a credential value is a placeholder, a template field or a reference."""
    lowered = value.lower()
    return (value[:1] in "([{$%<@" or "{" in value or "}" in value or len(set(value)) == 1
            or any(word in lowered for word in PLACEHOLDER_WORDS))


def credential_matches(text):
    """Each credential assignment in `text`, left to right, as the `ASSIGNED` match
    of its separator and value: group 1 a quote, 2 a quoted value, 3 a bare value.

    A name is a run of name characters holding a keyword; it opens at a word
    boundary at or before a keyword in it, and the assignment follows its end. Each
    keyword is found by a plain search and its name extended one character at a
    time, so a long run of name characters costs linear time, never a nested
    backtracking search; a scan resumes past each assignment it yields."""
    position = 0
    while True:
        keyword = KEYWORD.search(text, position)
        if keyword is None:
            return
        start = keyword.start()
        while start > position and NAME_CHAR.match(text, start - 1):
            start -= 1
        end = NAME_RUN.match(text, keyword.end()).end()
        opening = BOUNDARY.search(text, start, end)
        assigned = ASSIGNED.match(text, end)
        if (opening is not None and assigned is not None
                and KEYWORD.search(text, opening.start(), end) is not None):
            yield assigned
            position = assigned.end()
        else:
            position = end


def is_path_name(text, end):
    """Whether the name ending at `end` in `text` is a file path: the run of name
    characters and path separators ending there holds a `/` or `\\`, or ends in a
    dot and one to five letters."""
    start = end
    while start > 0 and (text[start - 1] in "/\\" or NAME_CHAR.match(text, start - 1)):
        start -= 1
    name = text[start:end]
    return "/" in name or "\\" in name or PATH_EXTENSION.search(name) is not None


def is_credential(match, text):
    """Whether a `credential_matches` match in `text` carries a literal,
    non-placeholder value that is no hash digest, under a name that is no file path."""
    value = match.group(2) if match.group(3) is None else match.group(3)
    if DIGEST.match(value) or is_path_name(text, match.start()):
        return False
    if match.group(3) is not None:
        return (not is_placeholder(match.group(3))
                and EXPRESSION.match(text, match.start(3)) is None)
    return not is_placeholder(match.group(2))


def line_hits(text, opens):
    """The categories one added line hits; `opens` says whether its file also adds
    a conflict opener, without which a bare middle line is no marker."""
    hits = []
    if PRIVATE_KEY.search(text):
        hits.append("private key")
    if any(is_credential(match, text) for match in credential_matches(text)):
        hits.append("credential assignment")
    if any(not is_placeholder(match.group(1)) for match in URL.finditer(text)):
        hits.append("inline credentials in URL")
    if PROVIDER.search(text):
        hits.append("provider token")
    if text.startswith((MARKER_OPEN, MARKER_CLOSE)) or (opens and text == MARKER_MIDDLE):
        hits.append("conflict marker")
    return hits


def is_credential_file(path):
    """Whether an added path's name is a credential or environment file's."""
    name = path.rsplit("/", 1)[-1]
    return ((name == ".env" or (name.startswith(".env.") and name not in ENV_EXAMPLES))
            or name.endswith((".pem", ".key")) or name in KEY_FILES)


def added_lines(patch):
    """`(path, line number, text)` for each added line of a `-U0` patch; a `+++`
    header is told from an added line by whether a hunk is open."""
    path, number, in_hunk = None, 0, False
    for line in patch.split("\n"):
        hunk = HUNK.match(line)
        if hunk:
            number, in_hunk = int(hunk.group(1)), True
        elif in_hunk and line[:1] in ("+", "-", " ", "\\"):
            if line.startswith("+"):
                if path is not None:
                    yield path, number, line[1:].rstrip("\r")
                number += 1
            elif line.startswith(" "):
                number += 1
        elif line.startswith("+++ "):
            name = line[4:].rstrip("\t")
            if name.startswith("b/"):
                path = name[2:]
            elif name.startswith('"b/'):
                path = name[3:-1]
            else:
                path = None
        else:
            in_hunk = False


def scan(name_status, patch):
    """The rows of one change: its added credential files, then its added lines' hits."""
    rows = []
    for line in name_status.split("\n"):
        status, _tab, path = line.partition("\t")
        if status == "A" and is_credential_file(path):
            rows.append(f"{path} — credential file")
    added = {}
    for path, number, text in added_lines(patch):
        added.setdefault(path, []).append((number, text))
    for path, lines in added.items():
        opens = any(text.startswith(MARKER_OPEN) for _number, text in lines)
        for number, text in lines:
            rows.extend(f"{path}:{number} — {category}" for category in line_hits(text, opens))
    return rows


def hook_path():
    """The repository's `pre-commit` path, absolute, by `git rev-parse --git-path
    hooks` (which follows `core.hooksPath` and worktrees); None outside a repository."""
    code, out = git("rev-parse", "--git-path", "hooks")
    if code != 0 or not out.strip():
        return None
    return os.path.join(os.path.abspath(out.strip()), "pre-commit")


def hook_state(path):
    """`installed`, `not installed` or `a different hook`, for the `pre-commit` line."""
    if not os.path.isfile(path):
        return "not installed"
    with open(path, "rb") as handle:
        return "installed" if handle.read() == SHIM else "a different hook"


def unresolved(revision_range):
    """Why `revision_range` is no `<base>..<head>` of two commits, or None."""
    base, dots, head = revision_range.partition("..")
    if not (dots and base and head):
        return f"{revision_range} is not <base>..<head>"
    for revision in (base, head):
        if git("rev-parse", "--verify", "--quiet", revision + "^{commit}")[0] != 0:
            return f"{revision} does not resolve to a commit"
    return None


def check_range(revision_range):
    reason = unresolved(revision_range)
    if reason is not None:
        emit([f"UNVERIFIABLE — {reason}"])
        return 2
    rows = scan(git("diff", "--name-status", revision_range)[1],
                git("diff", "-U0", "--no-color", revision_range)[1])
    emit([f"{'FAIL' if rows else 'PASS'} — {revision_range}", *rows,
          f"pre-commit hook: {hook_state(hook_path())}"])
    return 1 if rows else 0


def install():
    path = hook_path()
    if path is None:
        emit(["refused: not a git repository"])
        return 1
    if os.path.exists(path):
        with open(path, "rb") as handle:
            if handle.read() == SHIM:
                emit(["already installed"])
                return 0
        emit([f"refused: {path} holds another hook"])
        return 1
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "xb") as handle:
        handle.write(SHIM)
    os.chmod(path, os.stat(path).st_mode | stat.S_IXUSR | stat.S_IXGRP | stat.S_IXOTH)
    emit([f"installed: {path}"])
    return 0


def main(argv):
    parser = argparse.ArgumentParser(prog="pre-commit-check.py")
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--range", metavar="<base>..<head>")
    mode.add_argument("--install", action="store_true")
    args = parser.parse_args(argv[1:])
    if args.install:
        return install()
    if args.range is not None:
        return check_range(args.range)
    rows = scan(git("diff", "--cached", "--name-status")[1],
                git("diff", "--cached", "-U0", "--no-color")[1])
    if rows:
        emit(["pre-commit-check: FAIL", *rows])
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
