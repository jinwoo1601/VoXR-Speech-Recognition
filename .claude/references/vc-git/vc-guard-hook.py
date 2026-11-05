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
MKTEMP_OPTIONS = re.compile(r"-[dqu]+")
REFERENCE = re.compile(r"\$(?:\{([A-Za-z_][A-Za-z0-9_]*)\}|([A-Za-z_][A-Za-z0-9_]*))")
NAMED = re.compile(r"([A-Za-z_][A-Za-z0-9_]*)(\+?=|$)")
PIN ="vc-guard-mktemp-placeholder"
REASON = ("vc-guard: subagents never change version-control state (git {verb} in {tree}); "
          "report the need to the PM — read forms such as git status, diff, log, show and "
          "stash list stay allowed.")
READ_VERBS = frozenset((
    "status", "diff", "log", "show", "rev-parse", "ls-files", "ls-tree", "merge-base", "grep",
    "blame", "describe", "shortlog", "cat-file", "for-each-ref", "show-ref", "rev-list",
    "name-rev", "check-ignore", "count-objects", "var", "whatchanged", "help", "version",
    "--version", "diff-tree", "diff-index", "diff-files"))
WRITES_FILE = frozenset(("diff", "log", "show", "diff-tree", "diff-index", "diff-files"))
GLOBAL_FLAGS = frozenset(("--no-pager", "-P", "-p", "--paginate", "--bare",
                          "--no-optional-locks", "--literal-pathspecs"))
GLOBAL_VALUED = ("--git-dir=", "--work-tree=")
BRANCH_FLAGS = frozenset(("--show-current", "--list", "-l", "-a", "-r", "-v", "-vv", "--all",
                          "--remotes"))
BRANCH_VALUED = frozenset(("--contains", "--no-contains", "--merged", "--no-merged",
                           "--points-at"))
CONFIG_READS = frozenset(("--get", "--get-all", "--get-regexp", "--list", "-l"))
CONFIG_WRITE_SUBCOMMANDS = frozenset(("set", "unset", "rename-section", "remove-section", "edit"))
CONFIG_WRITE_OPTIONS = frozenset(("--add", "--replace-all", "--rename-section", "--remove-section",
                                  "-e", "--edit"))
INIT_VALUED = ("initial-branch", "template", "separate-git-dir", "object-format", "ref-format")


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


def lift(command):
    marks, code = [], 0xE000
    while len(marks) < 3:
        if chr(code) not in command:
            marks.append(chr(code))
        code += 1
    opening, ending, dollar = marks
    text, bodies, quote = [], [], None
    index, length = 0, len(command)

    def closing(position):
        depth, inner_quote = 1, None
        while position < length:
            character = command[position]
            if inner_quote == "'":
                if character == "'":
                    inner_quote = None
            elif character == "\\":
                position += 1
            elif inner_quote == '"':
                if character == '"':
                    inner_quote = None
            elif character in "'\"":
                inner_quote = character
            elif character == "(":
                depth += 1
            elif character == ")":
                depth -= 1
                if depth == 0:
                    return position
            position += 1
        return None

    while index < length:
        character = command[index]
        if quote == "'":
            if character == "'":
                quote = None
            text.append(dollar if character == "$" else character)
            index += 1
            continue
        if character == "\\":
            text.append(character)
            following = command[index + 1:index + 2]
            if following:
                text.append(dollar if following == "$" else following)
            index += 2
            continue
        if character in "'\"" and quote in (None, character):
            quote = character if quote is None else None
        elif character == "$" and command[index + 1:index + 2] == "(":
            end = closing(index + 2)
            if end is not None:
                text.append(opening + str(len(bodies)) + ending)
                bodies.append(command[index + 2:end])
                index = end + 1
                continue
        text.append(character)
        index += 1
    return "".join(text), bodies, tuple(marks)


def is_break(token):
    if token in ("{", "}"):
        return True
    return (bool(token) and all(character in PUNCTUATION for character in token)
            and token not in REDIRECTS)


def split_segments(tokens):
    segments, breaks = [[]], [""]
    for token in tokens:
        if is_break(token):
            segments.append([])
            breaks.append(token)
        else:
            segments[-1].append(token)
    return segments, breaks


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


def to_path(text, base, word=False):
    if word and ("$" in text or "`" in text):
        return None
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


def invocations(command, cwd, pinned=None, pinning=None):
    text, bodies, marks = lift(command)
    sentinel = re.compile(re.escape(marks[0]) + r"(\d+)" + re.escape(marks[1]))
    pinned = set() if pinned is None else set(pinned)
    pinning = "TMPDIR" not in command if pinning is None else pinning
    working = cwd
    found = []
    segments, breaks = split_segments(tokenize(text))
    breaks.append("")
    stack, body, pending, restores = [], None, None, []

    def blank(segment):
        return all(word in REDIRECTS for word in segment[::2])

    def before(p):
        text = breaks[p]
        while p > 0 and blank(segments[p - 1]):
            p -= 1
            text = breaks[p] + text
        return text

    def after(p):
        p += 1
        text = breaks[p]
        while p < len(segments) and blank(segments[p]):
            p += 1
            text += breaks[p]
        return text, p

    def run(text):
        return re.sub(r"[(){}\n]", "", text)

    def terminator(p):
        depth, joined = 0, False
        while True:
            text, following = after(p)
            for match in re.finditer(r"&&|\|\||\|&|[(){};&|\n]", text):
                mark = match.group(0)
                if mark in ("(", "{"):
                    depth += 1
                elif mark in (")", "}"):
                    depth -= 1
                    if depth < 0:
                        return None
                elif depth == 0 and mark == "&":
                    q, offset = p + 1, match.start()
                    while offset >= len(breaks[q]):
                        offset -= len(breaks[q])
                        q += 1
                    return q, offset
                elif depth == 0 and (mark == ";" or (mark == "\n" and not joined)):
                    return None
                joined = mark in ("&&", "||", "|", "|&") or (joined and mark == "\n")
            if following >= len(segments):
                return None
            p, joined = following, False

    def backgrounded(p):
        return terminator(p) is not None

    def definition(p):
        segment = segments[p]
        if len(segment) != 1 and not (len(segment) == 2 and segment[0] == "function"):
            return None
        characters, q = [], p + 1
        while True:
            characters.extend((character, (q, offset))
                              for offset, character in enumerate(breaks[q])
                              if character != "\n")
            if q == len(segments) or segments[q]:
                break
            q += 1
        if [character for character, _location in characters[:2]] == ["(", ")"]:
            characters = characters[2:]
        elif len(segment) == 1:
            return None
        if characters and characters[0][0] in "{(":
            return characters[0][1]
        return None

    def close():
        nonlocal body
        if body is not None and len(stack) < body:
            body = None

    def pin_body(number):
        words = tokenize(bodies[number])
        return pinning and words[:1] == ["mktemp"] and all(MKTEMP_OPTIONS.fullmatch(word)
                                                           for word in words[1:])

    def expand(word, seen):
        whole = sentinel.fullmatch(word)
        made = bool(whole) and pin_body(int(whole.group(1)))
        pin = None
        if made or any((match.group(1) or match.group(2)) in seen
                       for match in REFERENCE.finditer(word)):
            try:
                pin = os.path.join(tempfile.gettempdir(), PIN)
            except OSError:
                pass
        if made and pin is not None:
            return pin
        word = REFERENCE.sub(lambda match: pin if pin is not None
                             and (match.group(1) or match.group(2)) in seen
                             else match.group(0), word)
        word = sentinel.sub(lambda match: "$(" + bodies[int(match.group(1))] + ")", word)
        return word.replace(marks[2], "$")

    loops, head, count = [], None, 0
    for position, segment in enumerate(segments):
        start = command_start(segment)
        word = command_name(segment[start]) if start < len(segment) else None
        if word in ("for", "select", "while", "until"):
            if head is None:
                head = position
            count += 1
        elif word == "done" and head is not None:
            count -= 1
            if count == 0:
                loops.append((head, position))
                head = None
    if head is not None:
        loops.append((head, len(segments) - 1))
    regions = []
    for first, last in loops:
        names = set()
        for segment in segments[first:last + 1]:
            for word in segment:
                named = NAMED.match(word)
                if named:
                    names.add(named.group(1))
        regions.append((first, last, names))

    for position, segment in enumerate(segments):
        for offset, character in enumerate(breaks[position]):
            if character in "({":
                piped = character == "{" and run(before(position)) in ("|", "|&")
                stack.append((character, working, set(pinned), piped))
                if pending == (position, offset):
                    body = len(stack)
                    pending = None
            elif character == ")":
                if any(entry[0] == "(" for entry in stack):
                    while stack[-1][0] != "(":
                        stack.pop()
                    _opener, working, pinned, _piped = stack.pop()
                    close()
            elif character == "}" and stack and stack[-1][0] == "{":
                _opener, saved, kept, piped = stack.pop()
                if piped or run(after(position - 1)[0]) in ("|", "&", "|&"):
                    working, pinned = saved, kept
                else:
                    ending = terminator(position)
                    if ending is not None:
                        restores.append((ending, saved, kept))
                close()
            elif character == "&":
                for location, saved, kept in reversed(restores):
                    if location == (position, offset):
                        working, pinned = saved, kept
        if not segment:
            continue
        opening = definition(position)
        if body is None and pending is None:
            pending = opening
        subshell = (run(after(position)[0]) in ("|", "&", "|&")
                    or run(before(position)) in ("|", "|&") or backgrounded(position))
        for first, _last, names in regions:
            if position == first:
                pinned -= names
        seen = set() if body is not None else pinned
        for word in segment:
            for match in sentinel.finditer(word):
                found.extend(invocations(bodies[int(match.group(1))], working, seen, pinning))
        start = command_start(segment)
        if start >= len(segment):
            looped = any(first <= position <= last for first, last, _names in regions)
            for word in segment:
                if not ASSIGNMENT.match(word):
                    continue
                name, value = word.split("=", 1)
                whole = sentinel.fullmatch(value)
                if (whole and pin_body(int(whole.group(1))) and not subshell
                        and body is None and not looped):
                    pinned.add(name)
                else:
                    pinned.discard(name)
            continue
        name = command_name(segment[start])
        arguments = [expand(word, seen) for word in segment[start + 1:]]
        if name == "cd":
            if subshell:
                working = None
            else:
                operands = [word for word in arguments if word not in ("-L", "-P")]
                working = (to_path(operands[0], working, True)
                           if operands and operands[0] != "-" else None)
        else:
            found.append((name, arguments, working))
        for word in segment[start:]:
            named = NAMED.match(word)
            if named:
                pinned.discard(named.group(1))
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
                target = to_path(value, target, True)
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


def init_targets(rest):
    operand, separate, options = None, [], True
    index = 0
    while index < len(rest):
        word = rest[index]
        if options and word == "--":
            options = False
        elif options and word.startswith("--"):
            key = word[2:].split("=", 1)[0]
            option = next((name for name in INIT_VALUED if key and name.startswith(key)), None)
            if option is not None:
                if "=" in word:
                    value = word.split("=", 1)[1]
                else:
                    index += 1
                    value = rest[index] if index < len(rest) else None
                if option == "separate-git-dir" and value is not None:
                    separate.append(value)
        elif options and word.startswith("-"):
            if word.find("b") == len(word) - 1:
                index += 1
        elif operand is None:
            operand = word
        index += 1
    return operand, separate


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
        first = rest[0] if rest else ""
        if first in CONFIG_WRITE_SUBCOMMANDS or any(
                word in CONFIG_WRITE_OPTIONS or word.startswith("--unset") for word in rest):
            return False
        return (first in ("get", "list") or any(word in CONFIG_READS for word in rest)
                or (len(rest) == 1 and not first.startswith("-")))
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
        checks = [(target, where)]
        if verb == "init":
            operand, separate = init_targets(rest)
            if operand is not None:
                options = arguments[:len(arguments) - len(rest) - 1]
                if not ("GIT_DIR" in command
                        or any(word.startswith("--git-dir=") for word in options)):
                    checks = []
                checks.append((to_path(operand, target, True), UNRESOLVED))
            checks.extend((to_path(value, target, True), UNRESOLVED) for value in separate)
        for path, label in checks:
            if path is None:
                return REASON.format(verb=verb, tree=label)
            if trees is None:
                trees = protected_trees(root)
            for tree in trees:
                if inside(path, tree):
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
