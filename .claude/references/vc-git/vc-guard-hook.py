# =============================================================================
# Purpose:  PreToolUse guard refusing a subagent's git command that changes a protected tree
# Layer:    Hooks
# Owns:     (script, no public types)
# Depends:  (none)
# =============================================================================

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
    if len(argv) > 1:
        candidate = Path(argv[1])
        if candidate.is_dir():
            return candidate
    return Path.cwd()


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


def split_segments(tokens):
    segments = [[]]
    for token in tokens:
        if is_break(token):
            segments.append([])
        else:
            segments[-1].append(token)
    return [segment for segment in segments if segment]


def command_name(token):
    name = token.replace("\\", "/").rsplit("/", 1)[-1].lower()
    return name[:-len(".exe")] if name.endswith(".exe") else name


def command_start(segment):
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
    return str(path).replace("\\", "/").lower().rstrip("/")


def inside(path, tree):
    key, top = path_key(path), path_key(tree)
    return key == top or key.startswith(top + "/")


def pointed_path(file, prefix):
    try:
        text = file.read_text(encoding="utf-8").strip()
    except (OSError, ValueError):
        return None
    if not text.startswith(prefix) or not text[len(prefix):].strip():
        return None
    path = Path(text[len(prefix):].strip())
    return Path(os.path.normpath(path if path.is_absolute() else file.parent / path))


def protected_trees(root):
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
