# =============================================================================
# Purpose:  PreToolUse guard refusing a subagent's git command that changes a protected tree
# Layer:    Hooks
# Owns:     (script, no public types)
# Depends:  (none)
# =============================================================================
"""Claude Code `PreToolUse` hook on `Bash`: subagents never change version-control
state in the project's trees; the PM and `vc-checkin` are never guarded.

Standard library only, and never an import of `harness.py`, for the agent
guard's reason: the hook runs under whatever interpreter the platform starts.

The command is split into segments at the shell's control operators and at
newlines; each segment's command word, past assignments and wrapper words, is
matched by basename, a trailing `.exe` dropped. A `git` segment's verb is read
past the global options; a read form is allowed anywhere, and any other verb is
refused when its target directory - `-C`, else the last `cd`, else the payload's
`cwd` - lies inside the project root or another tree of the same repository, or
cannot be resolved. The trees are read from the `.git` files, never from a
subprocess, and paths compare case-insensitively with either separator.

A denial is an affirmative document at exit 0; anything else is silence. There
is no generic catch anywhere here, so an unforeseen bug is a traceback and a
non-blocking error, never a denial.
"""

import json
import os
import re
import shlex
import sys
import tempfile
from pathlib import Path

PUNCTUATION = "();<>|&\n"
REDIRECTS = frozenset(("<", ">", ">>", "<<", "<<<", ">&", "<&", "&>", "&>>", ">|", "<>"))
ASSIGNMENT = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*=")
WRAPPERS = frozenset(("command", "exec", "time", "nohup", "then", "do", "else", "!"))
ENV_VALUED = frozenset(("-u", "--unset", "-C", "--chdir", "-S", "--split-string"))
XARGS_VALUED = frozenset(("-a", "-d", "-E", "-I", "-L", "-n", "-P", "-s"))
SUDO_VALUED = frozenset(("-u", "--user", "-g", "--group", "-C", "--close-from", "-D", "--chdir",
                         "-h", "--host", "-p", "--prompt", "-r", "--role", "-t", "--type",
                         "-U", "--other-user", "-T", "--command-timeout", "-R", "--chroot"))
UNRESOLVED = "an unresolved directory"
REASON = ("vc-guard: subagents never change version-control state (git {verb} in {tree}); "
          "report the need to the PM — read forms such as git status, diff, log, show and "
          "stash list stay allowed.")
READ_VERBS = frozenset((
    "status", "diff", "log", "show", "rev-parse", "ls-files", "ls-tree", "merge-base", "grep",
    "blame", "describe", "shortlog", "cat-file", "for-each-ref", "show-ref", "rev-list",
    "name-rev", "check-ignore", "count-objects", "var", "whatchanged", "help", "version",
    "--version"))
WRITES_FILE = frozenset(("diff", "log", "show"))
GLOBAL_FLAGS = frozenset(("--no-pager", "-P", "-p", "--paginate", "--bare",
                          "--no-optional-locks", "--literal-pathspecs"))
GLOBAL_VALUED = ("--git-dir=", "--work-tree=")
BRANCH_FLAGS = frozenset(("--show-current", "--list", "-l", "-a", "-r", "-v", "-vv", "--all",
                          "--remotes"))
BRANCH_VALUED = frozenset(("--contains", "--no-contains", "--merged", "--no-merged",
                           "--points-at"))
CONFIG_READS = frozenset(("--get", "--get-all", "--get-regexp", "--list", "-l"))


def resolve_root(argv):
    """The project root: argv[1] when it names a directory, else the process cwd."""
    if len(argv) > 1:
        candidate = Path(argv[1])
        if candidate.is_dir():
            return candidate
    return Path.cwd()


def parse_payload(data):
    """The hook payload as a mapping, or None for stdin the guard cannot read."""
    try:
        document = json.loads(data)
    except ValueError:
        return None
    return document if isinstance(document, dict) else None


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


def split_segments(tokens):
    """The tokens between breaks, empty segments dropped."""
    segments = [[]]
    for token in tokens:
        if is_break(token):
            segments.append([])
        else:
            segments[-1].append(token)
    return [segment for segment in segments if segment]


def command_name(token):
    """The token's basename by either separator, lowercased, a trailing `.exe` dropped."""
    name = token.replace("\\", "/").rsplit("/", 1)[-1].lower()
    return name[:-len(".exe")] if name.endswith(".exe") else name


def command_start(segment):
    """The index of the segment's command word: past leading assignments, and past
    the wrapper words, `sudo`, `env` and `xargs`, each with the options after it."""
    index = 0
    while index < len(segment):
        token = segment[index]
        if ASSIGNMENT.match(token):
            index += 1
            continue
        name = command_name(token)
        if token in WRAPPERS:
            valued = frozenset()
        elif name == "env":
            valued = ENV_VALUED
        elif name == "xargs":
            valued = XARGS_VALUED
        elif name == "sudo":
            valued = SUDO_VALUED
        else:
            break
        index += 1
        while index < len(segment) and (segment[index].startswith("-")
                                        or (name == "env" and ASSIGNMENT.match(segment[index]))):
            index += 2 if segment[index] in valued else 1
    return index


def to_path(text, base):
    """`text` as a normalized absolute path, relative ones against `base`; None for a
    drive-relative path, a home-relative one, and a relative one with no base. On
    Windows a POSIX drive form - a slash, one drive letter, then a slash or nothing -
    is read as that drive, a rooted path whose first component is `tmp` as lying
    under the directory `tempfile.gettempdir()` names, and any other rooted path with
    no drive is None; on any other OS a rooted path is an absolute path."""
    if os.name != "nt":
        if text[:1] == "/":
            return os.path.normpath(text)
    elif text[:1] == "/" and text.split("/", 2)[1] == "tmp":
        text = tempfile.gettempdir() + text[len("/tmp"):]
    elif (len(text) >= 2 and text[0] == "/" and text[1].isalpha()
            and (len(text) == 2 or text[2] == "/")):
        text = text[1].upper() + ":/" + text[3:]
    if len(text) >= 2 and text[1] == ":" and text[0].isalpha():
        if len(text) == 2 or text[2] not in "/\\":
            return None
        return os.path.normpath(text)
    if not text or text[0] in "/\\~" or base is None:
        return None
    return os.path.normpath(os.path.join(base, text))


def invocations(command, cwd):
    """Each segment's command as (name, arguments, working dir), a `cd` segment
    moving the working dir of the segments after it; None where it is unknown."""
    working = cwd
    found = []
    for segment in split_segments(tokenize(command)):
        start = command_start(segment)
        if start >= len(segment):
            continue
        name = command_name(segment[start])
        arguments = segment[start + 1:]
        if name == "cd":
            operands = [word for word in arguments if word not in ("-L", "-P")]
            working = (to_path(operands[0], working)
                       if operands and operands[0] != "-" else None)
            continue
        found.append((name, arguments, working))
    return found


def path_key(path):
    """The comparison form of a path: one separator, lowercase, no trailing slash."""
    return str(path).replace("\\", "/").lower().rstrip("/")


def inside(path, tree):
    """True when `path` is `tree` or lies under it."""
    key, top = path_key(path), path_key(tree)
    return key == top or key.startswith(top + "/")


def pointed_path(file, prefix):
    """The path a `.git` pointer file names after `prefix`, relative ones against the
    file's directory; None for a missing, unreadable or foreign file."""
    try:
        text = file.read_text(encoding="utf-8").strip()
    except (OSError, ValueError):
        return None
    if not text.startswith(prefix) or not text[len(prefix):].strip():
        return None
    path = Path(text[len(prefix):].strip())
    return Path(os.path.normpath(path if path.is_absolute() else file.parent / path))


def protected_trees(root):
    """The root and every tree of its repository: the hub, the common dir's parent,
    and each tree a `worktrees/*/gitdir` file names."""
    trees = [Path(os.path.normpath(root))]
    dot_git = trees[0] / ".git"
    if dot_git.is_dir():
        common = dot_git
    else:
        gitdir = pointed_path(dot_git, "gitdir:")
        if gitdir is None:
            return trees
        common = gitdir.parent.parent if gitdir.parent.name.lower() == "worktrees" else gitdir
    trees.append(common.parent)
    try:
        entries = sorted((common / "worktrees").iterdir())
    except OSError:
        entries = []
    for entry in entries:
        tree_git = pointed_path(entry / "gitdir", "")
        if tree_git is not None:
            trees.append(tree_git.parent)
    return trees


def git_verb(arguments, working):
    """(verb, its arguments, target dir, where, risky) past the global options, or
    None when no verb follows them. The target is None when it cannot be resolved;
    risky is True for a `-c` whose key can run a command."""
    target, where, risky = working, working or UNRESOLVED, False
    index = 0
    while index < len(arguments):
        option = arguments[index]
        if option in ("-C", "-c"):
            if index + 1 >= len(arguments):
                return None
            value = arguments[index + 1]
            if option == "-C":
                target = to_path(value, target)
                where = target or value
            else:
                key = value.split("=", 1)[0].lower()
                risky = risky or not (key.startswith("color.") or key == "core.quotepath")
            index += 2
        elif option in GLOBAL_FLAGS or option.startswith(GLOBAL_VALUED):
            index += 1
        else:
            break
    if index >= len(arguments):
        return None
    return arguments[index], arguments[index + 1:], target, where, risky


def branch_reads(rest):
    """True when a `branch` invocation only lists."""
    listing = False
    index = 0
    while index < len(rest):
        word = rest[index]
        if word in BRANCH_FLAGS or word.startswith("--format="):
            listing = listing or word in ("--list", "-l")
            index += 1
        elif word in BRANCH_VALUED:
            index += 1
            if index < len(rest) and not rest[index].startswith("-"):
                index += 1
        elif "=" in word and word.split("=", 1)[0] in BRANCH_VALUED:
            index += 1
        elif listing and not word.startswith("-"):
            index += 1
        else:
            return False
    return True


def is_read_form(verb, rest):
    """True when the verb and its arguments only read."""
    if verb in READ_VERBS:
        return not (verb in WRITES_FILE
                    and any(word == "--output" or word.startswith("--output=") for word in rest))
    first = rest[0] if rest else None
    if verb in ("stash", "notes"):
        return first in ("list", "show")
    if verb == "worktree":
        return first == "list"
    if verb == "reflog":
        return first in (None, "show")
    if verb == "branch":
        return branch_reads(rest)
    if verb == "tag":
        return not rest or (first in ("-l", "--list") and len(rest) <= 2)
    if verb == "config":
        return any(word in CONFIG_READS for word in rest)
    if verb == "remote":
        return not rest or rest == ["-v"] or first in ("show", "get-url")
    return False


def decide(payload, root):
    """The reason to deny this call with, or None to allow it."""
    if not payload.get("agent_id") or payload.get("agent_type") == "vc-checkin":
        return None
    if payload.get("tool_name") != "Bash":
        return None
    tool_input = payload.get("tool_input")
    command = tool_input.get("command") if isinstance(tool_input, dict) else None
    if not isinstance(command, str):
        return None
    cwd = payload.get("cwd")
    working = to_path(cwd, None) if isinstance(cwd, str) else None
    trees = None
    for name, arguments, directory in invocations(command, working):
        if name != "git":
            continue
        parsed = git_verb(arguments, directory)
        if parsed is None:
            continue
        verb, rest, target, where, risky = parsed
        if not risky and is_read_form(verb, rest):
            continue
        if target is None:
            return REASON.format(verb=verb, tree=where)
        if trees is None:
            trees = protected_trees(root)
        for tree in trees:
            if inside(target, tree):
                return REASON.format(verb=verb, tree=tree)
    return None


def deny_document(reason):
    """The denial, one ASCII line: the guard's only output."""
    return json.dumps({"hookSpecificOutput": {"hookEventName": "PreToolUse",
                                              "permissionDecision": "deny",
                                              "permissionDecisionReason": reason}})


def main(argv):
    payload = parse_payload(sys.stdin.buffer.read())
    if payload is None:
        return 0
    reason = decide(payload, resolve_root(argv))
    if reason is not None:
        print(deny_document(reason))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
