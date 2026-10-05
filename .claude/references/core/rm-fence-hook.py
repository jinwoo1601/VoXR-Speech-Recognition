# =============================================================================
# Purpose:  PreToolUse fence on a Bash call's rm and rmdir targets
# Layer:    Hooks
# Owns:     (script, no public types)
# Depends:  (none)
# =============================================================================
"""Claude Code `PreToolUse` hook on `Bash`: the rm fence, which finds each `rm`
and `rmdir` in a call, its target words and the working directory each one runs
in, and stops a delete whose target lies outside the project, its trees,
`.scratch/` and the temp folder.

Standard library only, and never an import of `harness.py`, for the agent
guard's reason: the hook runs under whatever interpreter the platform starts.

A quote-aware prescan lifts nested text out of the command - command and
process substitutions, each replaced by one sentinel word, and heredoc bodies,
removed - and the rest is tokenized by the vc guard's tokenizer and walked
segment by segment: parentheses, piped or backgrounded brace groups and
backgrounded and-or lists scope the working directory and the variables, a `cd`
moves the directory, assignments and `export` set variables, and an `rm` or
`rmdir` yields a delete; a function body's deletes and the names it sets are
unpinned. Nested text is searched with the same reader,
never trusted: a delete found in it has one unpinned target. Paths are read in
Windows form through `ntpath`; a spelling the reader cannot normalize is None.

The fence roots are the project root named by argv[1], its `.scratch`, the trees
roots read from the first bindings file holding a `trees` section, and the `TMP`
and `TEMP` folders; each is real-path resolved and a drive root dropped. A target
is real-path resolved, a glob tested match by match, and is inside only when it
lies strictly beneath a root. No target outside is silence; outside targets ask,
every one listed, when every segment of the call is a delete, and deny otherwise.
The exit is 0 always: the one broad handler, in `main`, turns any error into an
ask, as stdin it cannot read is, since a delete passed unchecked is the wrong side.
"""

import glob
import itertools
import json
import ntpath
import os
import re
import shlex
import sys

PUNCTUATION = "();<>|&\n"
REDIRECTS = frozenset(("<", ">", ">>", "<<", "<<<", ">&", "<&", "&>", "&>>", ">|", "<>"))
ASSIGNMENT = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*=")
WRAPPERS = frozenset(("command", "exec", "time", "nohup", "then", "do", "else", "!",
                      "if", "elif", "while", "until"))
RESERVED = frozenset(("then", "do", "else", "!", "if", "elif", "while", "until", "time"))
ENV_VALUED = frozenset(("-u", "--unset", "-C", "--chdir", "-S", "--split-string"))
SUDO_VALUED = frozenset(("-u", "--user", "-g", "--group", "-C", "--close-from", "-D", "--chdir",
                         "-h", "--host", "-p", "--prompt", "-r", "--role", "-t", "--type",
                         "-U", "--other-user", "-T", "--command-timeout", "-R", "--chroot"))
SHELLS = frozenset(("sh", "bash", "dash", "zsh"))
MKTEMP_OPTIONS = re.compile(r"-[dqu]+|--directory|--quiet|--dry-run")
SENTINEL = re.compile(r"\x01(\d+)\x02")
APPEND = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*\+=")
PLACEHOLDER = "rm-fence-mktemp-placeholder"
MATCH_CAP = 1000


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


def command_start(segment):
    """The index of the segment's command word: past leading assignments, past
    redirections, and past the wrapper words, `sudo` and `env`, each with the
    options after it."""
    index = 0
    while index < len(segment):
        token = segment[index]
        if token in REDIRECTS:
            index += 2
            continue
        if ASSIGNMENT.match(token):
            index += 1
            continue
        name = command_name(token)
        if token in WRAPPERS:
            valued = frozenset()
        elif name == "env":
            valued = ENV_VALUED
        elif name == "sudo":
            valued = SUDO_VALUED
        else:
            break
        index += 1
        while index < len(segment) and (segment[index].startswith("-")
                                        or (name == "env" and ASSIGNMENT.match(segment[index]))):
            index += 2 if segment[index] in valued else 1
    return index


def temp_root(environ):
    """The first of `TMP` and `TEMP` that is a drive-absolute path and not a drive
    root, normalized; None when neither is."""
    for name in ("TMP", "TEMP"):
        value = environ.get(name)
        if isinstance(value, str) and re.match(r"[A-Za-z]:[\\/]", value):
            path = ntpath.normpath(value)
            if len(path) > 3:
                return path
    return None


def to_path(text, base, environ):
    """`text` as a normalized Windows-form path, or None. `/tmp`, alone or before a
    separator, lies under `temp_root`; a slash, one drive letter, then a slash or
    nothing, is that drive; any other path led by a separator, a `~` path, a
    drive-relative path and a relative one with no base are None; a relative path
    joins `base`."""
    if not text or text[0] == "~":
        return None
    if text[:4] == "/tmp" and text[4:5] in ("", "/", "\\"):
        root = temp_root(environ)
        return None if root is None else ntpath.normpath(root + text[4:])
    if re.match(r"/[A-Za-z](?:/|\Z)", text):
        return ntpath.normpath(text[1].upper() + ":\\" + text[3:])
    if text[0] in "/\\":
        return None
    if re.match(r"[A-Za-z]:", text):
        return ntpath.normpath(text) if text[2:3] in ("/", "\\") else None
    if base is None:
        return None
    return ntpath.normpath(ntpath.join(base, text))


def quotes_open(text):
    """True for a quote left open or one the shell reads as escaped, either of
    which `tokenize`, with escapes off, misreads."""
    quote, index = None, 0
    while index < len(text):
        character = text[index]
        if quote == "'":
            if character == "'":
                quote = None
        elif character == "\\":
            following = text[index + 1:index + 2]
            if following == '"' or (quote is None and following == "'"):
                return True
            index += 1
        elif character in "'\"" and quote in (None, character):
            quote = character if quote is None else None
        index += 1
    return quote is not None


def restore(word, pieces):
    """`word` with each sentinel that indexes `pieces` replaced by the text it
    stands for; any other sentinel stays, unpinned."""
    def written(match):
        number = int(match.group(1))
        return pieces[number][0] if number < len(pieces) else match.group(0)
    return SENTINEL.sub(written, word)


def prescan(command):
    """(outer, pieces, mktemp, unbalanced): the command with nested text lifted out,
    each piece as (written, inner), the mktemp assignments as piece index to name,
    and whether the nesting or the quoting is unbalanced. A command that already
    holds a sentinel character is returned whole and unbalanced."""
    if "\x01" in command or "\x02" in command:
        return command, [], {}, True
    outer, pieces, mktemp, pending = [], [], {}, []
    unbalanced, quote = False, None
    index, length = 0, len(command)

    def closing(position):
        """The index of the `)` that closes a `(` just before `position`, or None."""
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
        following = command[index + 1:index + 2]
        if quote == "'":
            if character == "'":
                quote = None
            outer.append(character)
            index += 1
            continue
        if character == "\\":
            outer.append(command[index:index + 2])
            index += 2
            continue
        if character in "'\"" and quote in (None, character):
            quote = character if quote is None else None
            outer.append(character)
            index += 1
            continue
        if quote is None and character in "0123456789" and (
                not outer or outer[-1][-1] in " \t\n;&|()"):
            end = index
            while end < length and command[end] in "0123456789":
                end += 1
            if command[end:end + 1] in ("<", ">"):
                index = end
                continue
        if (character == "$" and following == "(") or character == "`" or (
                quote is None and character in "<>" and following == "("):
            if character == "`":
                opener, end = 1, index + 1
                while end < length and command[end] != "`":
                    end += 2 if command[end] == "\\" else 1
                if end >= length:
                    end = None
            else:
                opener, end = 2, closing(index + 2)
            if end is None:
                written, inner, index = command[index:], command[index + opener:], length
                unbalanced = True
            else:
                written, inner, index = command[index:end + 1], command[index + opener:end], end + 1
            if character == "$":
                assigned = re.search(r"(?:^|[\s;&|()])([A-Za-z_][A-Za-z0-9_]*)=\"?\Z",
                                     "".join(outer))
                words = tokenize(inner)
                if (assigned and words[:1] == ["mktemp"]
                        and all(MKTEMP_OPTIONS.fullmatch(word) for word in words[1:])):
                    mktemp[len(pieces)] = assigned.group(1)
            outer.append("\x01%d\x02" % len(pieces))
            pieces.append((written, inner))
            continue
        if quote is None and command.startswith("<<<", index):
            outer.append("<<<")
            index += 3
            continue
        if quote is None and command.startswith("<<", index):
            tabs = command.startswith("<<-", index)
            start = index
            index += 3 if tabs else 2
            while index < length and command[index] in " \t":
                index += 1
            word_start, delimiter, word_quote = index, [], None
            while index < length:
                letter = command[index]
                if word_quote is not None:
                    if letter == word_quote:
                        word_quote = None
                    else:
                        delimiter.append(letter)
                elif letter in "'\"":
                    word_quote = letter
                elif letter == "\\":
                    delimiter.append(command[index + 1:index + 2])
                    index += 1
                elif letter in " \t\n;&|()<>":
                    break
                else:
                    delimiter.append(letter)
                index += 1
            outer.append(command[start:index])
            if index > word_start:
                pending.append(("".join(delimiter), tabs))
            continue
        if quote is None and character == "\n" and pending:
            outer.append("\n")
            index += 1
            for delimiter, tabs in pending:
                start, body_end = index, length
                while index < length:
                    newline = command.find("\n", index)
                    stop = length if newline < 0 else newline
                    line = command[index:stop]
                    after = length if newline < 0 else newline + 1
                    if (line.lstrip("\t") if tabs else line) == delimiter:
                        body_end, index = index, after
                        break
                    index = after
                pieces.append((command[start:index], command[start:body_end]))
            pending = []
            continue
        outer.append(character)
        index += 1
    text = "".join(outer)
    return text, pieces, mktemp, unbalanced or quotes_open(text)


def expand(word, variables):
    """`word` with `$NAME` and `${NAME}` replaced from `variables`, or None for a
    sentinel, an extended pattern, a brace list or range, an absent name, or any
    other `$` left. Glob characters stay."""
    if "\x01" in word or "\x02" in word:
        return None
    if any(opener in word for opener in ("@(", "!(", "+(", "?(", "*(")):
        return None
    if re.search(r"\{[^{}]*(?:,|\.\.)[^{}]*\}",
                 re.sub(r"\$\{[A-Za-z_][A-Za-z0-9_]*\}", "", word)):
        return None
    reference = r"\$(?:\{([A-Za-z_][A-Za-z0-9_]*)\}|([A-Za-z_][A-Za-z0-9_]*))"
    if "$" in re.sub(reference, "", word):
        return None
    if any((braced or bare) not in variables for braced, bare in re.findall(reference, word)):
        return None
    return re.sub(reference, lambda match: variables[match.group(1) or match.group(2)], word)


def delete_targets(name, arguments):
    """The target words of one `rm` or `rmdir`: every word not an option before a
    `--` and every word after it, a redirection and its word dropped; for `rmdir`
    with `-p` or `--parents`, each operand's prefixes at each separator too."""
    targets, parents, options = [], False, True
    index = 0
    while index < len(arguments):
        word = arguments[index]
        if word in REDIRECTS:
            index += 2
            continue
        if options and word == "--":
            options = False
        elif options and word.startswith("-"):
            parents = parents or word == "--parents" or (
                not word.startswith("--") and "p" in word)
        else:
            targets.append(word)
        index += 1
    if name != "rmdir" or not parents:
        return targets
    widened = []
    for word in targets:
        widened.append(word)
        for position in range(len(word) - 1, 0, -1):
            prefix = word[:position]
            if word[position] in "/\\" and not re.fullmatch(r"[A-Za-z]:", prefix):
                widened.append(prefix)
    return widened


def nested_deletes(text):
    """The name of the first delete in nested text, read with fresh state, or None."""
    deletes, _chained = walk(text, None, {})
    return deletes[0][0] if deletes else None


def walk(command, cwd, variables):
    """(deletes, chained): each delete as (name, targets, working dir), each target
    as (word, expansion or None), the working dir None where it is unknown; chained
    is True when any outer segment is not an `rm` or `rmdir`."""
    seed = dict(variables)
    variables = dict(variables)
    directory = to_path(cwd, None, seed) if isinstance(cwd, str) else None
    outer, pieces, mktemp, _unbalanced = prescan(command)
    holding = [nested_deletes(inner) for _written, inner in pieces]
    deletes, chained, reached = [], False, set()

    def touch(name):
        """In a loop, unpin each target naming `name` recorded since the loop began;
        in an open function body, add the name to the body's names."""
        if looped:
            naming = re.compile(r"\$(?:\{%s\}|%s(?![A-Za-z0-9_]))"
                                % (re.escape(name), re.escape(name)))
            for at in range(loop_start, len(deletes)):
                found, targets, working_dir = deletes[at]
                deletes[at] = (found, [(word, None if naming.search(word) else expansion)
                                       for word, expansion in targets], working_dir)
        if body is not None:
            body["names"].add(name)

    def forget(name):
        """Make the name unknown, then touch it."""
        variables.pop(name, None)
        touch(name)

    def assign(word):
        """Set the assignment's name by the rule, or make it unknown."""
        name, value = word.split("=", 1)
        whole = SENTINEL.fullmatch(value)
        if whole and mktemp.get(int(whole.group(1))) == name:
            root = temp_root(seed)
            if root is None:
                variables.pop(name, None)
            else:
                variables[name] = ntpath.join(root, PLACEHOLDER)
            touch(name)
            return
        expanded = expand(value, variables)
        if expanded is None or re.search(r"[$*?\[\s]", expanded):
            variables.pop(name, None)
        else:
            variables[name] = expanded
        touch(name)

    if quotes_open(outer):
        for word in re.split(r"[\s;&|()<>]+", outer):
            name = command_name(word.replace("'", "").replace('"', ""))
            if word and name in ("rm", "rmdir"):
                deletes.append((name, [(word, None)], None))
        chained = True
    else:
        segments, breaks = [[]], [""]
        for token in tokenize(outer):
            if is_break(token):
                segments.append([])
                breaks.append(token)
            else:
                segments[-1].append(token)
        breaks.append("")
        stack, looped, loop_start, body, pending = [], False, 0, None, None

        def blank(segment):
            """True for a segment empty or made only of redirections, each with the
            word after it."""
            return all(word in REDIRECTS for word in segment[::2])

        def before(p):
            """The break before segment p, preceded by each earlier break reached back
            across blank segments."""
            text = breaks[p]
            while p > 0 and blank(segments[p - 1]):
                p -= 1
                text = breaks[p] + text
            return text

        def after(p):
            """(text, next): the break after segment p followed by each later break
            across blank segments, and the index of the segment after the last."""
            p += 1
            text = breaks[p]
            while p < len(segments) and blank(segments[p]):
                p += 1
                text += breaks[p]
            return text, p

        def run(text):
            """`text` without parentheses, braces and newlines."""
            return re.sub(r"[(){}\n]", "", text)

        def backgrounded(p):
            """True when the and-or list from segment p ends at `&`."""
            while True:
                text, following = after(p)
                if re.search(r"[(){}]", text):
                    return False
                if run(text) in ("&&", "||", "|", "|&") and following < len(segments):
                    p = following
                else:
                    return run(text) == "&"

        def definition(p):
            """The location (position, offset) of the `{` or `(` opening the body of
            a function segment p defines, or None."""
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

        def moving():
            """A `cd`, `pushd` or `popd`: in a loop, the directory of each delete
            recorded since the loop began is unknown; in a function body, it moved."""
            if looped:
                for at in range(loop_start, len(deletes)):
                    found, targets, _working_dir = deletes[at]
                    deletes[at] = (found, targets, None)
            if body is not None:
                body["moved"] = True

        def close(ended=False):
            """End the open function body once the stack is below its depth, or when
            the walk ends: its deletes are unpinned, a move in it leaves the
            directory unknown, and each name it set is forgotten."""
            nonlocal body, directory
            if body is None or (not ended and len(stack) >= body["depth"]):
                return
            opened, body = body, None
            for at in range(opened["start"], len(deletes)):
                found, targets, _working_dir = deletes[at]
                deletes[at] = (found, [(word, None) for word, _expansion in targets], None)
            if opened["moved"]:
                directory = None
            for name in opened["names"]:
                forget(name)

        for position, segment in enumerate(segments):
            for offset, character in enumerate(breaks[position]):
                if character in "({":
                    piped = character == "{" and run(before(position)) in ("|", "|&")
                    stack.append((character, directory, dict(variables), piped))
                    if pending == (position, offset):
                        body = {"depth": len(stack), "start": len(deletes), "names": set(),
                                "moved": False}
                        pending = None
                elif character == ")":
                    if any(entry[0] == "(" for entry in stack):
                        while stack[-1][0] != "(":
                            stack.pop()
                        _opener, directory, variables, _piped = stack.pop()
                        close()
                    else:
                        directory = None
                elif character == "}" and stack and stack[-1][0] == "{":
                    _opener, saved, kept, piped = stack.pop()
                    if piped or run(after(position - 1)[0]) in ("|", "&", "|&"):
                        directory, variables = saved, kept
                    close()
            if not segment:
                continue
            if body is None and pending is None:
                pending = definition(position)
            subshell = (run(after(position)[0]) in ("|", "&", "|&")
                        or run(before(position)) in ("|", "|&") or backgrounded(position))
            index = command_start(segment)
            while index < len(segment) and APPEND.match(segment[index]):
                variables.pop(segment[index].split("+=", 1)[0], None)
                index += 1
                index += command_start(segment[index:])
            if not looped and any(word in ("do", "while", "until") for word in segment[:index]):
                looped, loop_start = True, len(deletes)
            name = command_name(segment[index]) if index < len(segment) else ""
            arguments = segment[index + 1:]
            for token in segment:
                for match in SENTINEL.finditer(token):
                    number = int(match.group(1))
                    if number < len(pieces):
                        reached.add(number)
                        if holding[number] is not None:
                            deletes.append((holding[number], [(match.group(0), None)], None))
            if not name:
                words, scan = [], 0
                while scan < len(segment):
                    if segment[scan] in REDIRECTS:
                        scan += 2
                    else:
                        words.append(segment[scan])
                        scan += 1
                if all(word in RESERVED or ASSIGNMENT.match(word) or APPEND.match(word)
                       for word in words):
                    for word in words:
                        if APPEND.match(word) or (subshell and ASSIGNMENT.match(word)):
                            forget(re.match(r"[A-Za-z_][A-Za-z0-9_]*", word).group(0))
                        elif ASSIGNMENT.match(word):
                            assign(word)
            elif name == "cd":
                operand, options = None, True
                for word in arguments:
                    if options and word == "--":
                        options = False
                    elif not (options and word.startswith("-") and word != "-"):
                        operand = word
                        break
                if subshell or operand == "-":
                    directory = None
                elif operand is None:
                    directory = (to_path(variables["HOME"], None, seed)
                                 if "HOME" in variables else None)
                else:
                    value = expand(operand, variables)
                    directory = (None if value is None
                                 else to_path(value, None if looped else directory, seed))
                moving()
            elif name in ("pushd", "popd"):
                directory = None
                moving()
            elif name == "export":
                for word in arguments:
                    named = re.match(r"([A-Za-z_][A-Za-z0-9_]*)(\+?=|$)", word)
                    if named and (subshell or named.group(2) == "+="):
                        forget(named.group(1))
                    elif ASSIGNMENT.match(word):
                        assign(word)
            elif name in ("rm", "rmdir"):
                deletes.append((name, [(word, expand(word, variables))
                                       for word in delete_targets(name, arguments)],
                                directory))
            elif name in SHELLS and any(re.fullmatch(r"-[A-Za-z]*c[A-Za-z]*", word)
                                        for word in arguments):
                start = next(offset for offset, word in enumerate(arguments)
                             if re.fullmatch(r"-[A-Za-z]*c[A-Za-z]*", word))
                for word in arguments[start + 1:]:
                    if word.startswith("-"):
                        continue
                    found = nested_deletes(restore(word, pieces))
                    if found is not None:
                        deletes.append((found, [(word, None)], None))
            else:
                for word in arguments:
                    named = re.match(r"([A-Za-z_]\w*)(\+?=|$)", word)
                    if named:
                        forget(named.group(1))
            if name not in ("rm", "rmdir"):
                chained = True
        close(True)
    for number, found in enumerate(holding):
        if found is not None and number not in reached:
            deletes.append((found, [("\x01%d\x02" % number, None)], None))
    return deletes, chained


DENY_REASON = "rm-fence: run the delete as its own command"
ASK_PREFIX = "rm-fence: outside the project, its trees, .scratch/ and the temp folder: "
ERROR_REASON = "rm-fence: internal error (%s: %s); the delete was not checked"
READ_REASON = "rm-fence: the hook input could not be read (%s); the delete was not checked"


def path_key(path):
    """`path` as compared: `/` separators, lowercased, no trailing `/`."""
    return path.replace("\\", "/").lower().rstrip("/")


# Copied verbatim from the worktree hook.
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

    A content line is one that, stripped, is neither blank nor a comment.
    """
    stripped = [line.strip() for line in lines]
    content = [line for line in stripped
               if line and not (line.startswith("<!--") and line.endswith("-->"))]
    if not content:
        return True
    if any("TODO" in line for line in lines):
        return True
    return len(content) == 1 and content[0].removeprefix("- ").replace("`", "") == "none"


def trees_roots(root):
    """The trees roots from the first bindings file beneath `root` holding a `trees`
    section, in sorted order: on its first `- Location:` line, each backticked
    drive-absolute token cut before its first component holding `<`, each prefix
    once. An absent section, an unreadable file or no root gives none."""
    if root is None:
        return []
    for path in sorted(glob.glob(ntpath.join(glob.escape(root), ".claude", "bindings", "*.md"))):
        try:
            with open(path, encoding="utf-8") as handle:
                text = handle.read()
        except (OSError, ValueError):
            return []
        lines = trees_section(text)
        if lines is None:
            continue
        if absent_form(lines):
            return []
        roots = []
        for line in lines:
            if not line.strip().startswith("- Location:"):
                continue
            for token in re.findall(r"`([^`]+)`", line):
                if not re.match(r"^[A-Za-z]:[\\/]", token):
                    continue
                kept = []
                for component in re.split(r"[\\/]", token):
                    if "<" in component:
                        break
                    kept.append(component)
                prefix = ntpath.normpath("\\".join(kept) + "\\")
                if prefix not in roots:
                    roots.append(prefix)
            break
        return roots
    return []


def fence_roots(root, environ):
    """The fence roots' keys, in order - the project, its `.scratch`, the trees
    roots, `TMP` and `TEMP` - each real-path resolved, a drive root dropped, each
    key once."""
    candidates = []
    if root is not None:
        candidates.extend((root, ntpath.join(root, ".scratch")))
    candidates.extend(trees_roots(root))
    for name in ("TMP", "TEMP"):
        if name in environ:
            path = to_path(environ[name], None, environ)
            if path is not None:
                candidates.append(path)
    keys = []
    for candidate in candidates:
        key = path_key(os.path.realpath(candidate))
        if len(key) > 3 and key not in keys:
            keys.append(key)
    return keys


def matches(path, limit):
    """The paths a target stands for: a pattern's matches, sorted, or the pattern
    itself when nothing matches; any other path alone. A pattern is expanded only
    within `limit` matches: None when the limit is below zero or the pattern
    matches more than `limit` paths."""
    if not any(character in path for character in "*?["):
        return [path]
    if limit < 0:
        return None
    found = list(itertools.islice(glob.iglob(path), limit + 1))
    if len(found) > limit:
        return None
    return sorted(found) or [path]


def beneath(real, roots):
    """True when the real path `real` lies strictly beneath a root key."""
    key = path_key(real)
    return any(key.startswith(root + "/") for root in roots)


def inside(path, roots):
    """True when `path`, real-path resolved, lies strictly beneath a root key."""
    return beneath(os.path.realpath(path), roots)


def decide(payload, root, environ):
    """None for no opinion, else `(decision, reason)`: `ask` listing every outside
    target in call order, or `deny` when the call holds more than deletes."""
    if payload.get("tool_name") != "Bash":
        return None
    tool_input = payload.get("tool_input")
    if not isinstance(tool_input, dict) or not isinstance(tool_input.get("command"), str):
        return "ask", READ_REASON % "tool_input.command is not a string"
    command = tool_input["command"]
    cwd = payload.get("cwd") if isinstance(payload.get("cwd"), str) else None
    variables = {}
    for name in ("TEMP", "TMP", "HOME"):
        if name in environ:
            path = to_path(environ[name], None, environ)
            if path is not None:
                variables[name] = path
    deletes, chained = walk(command, cwd, variables)
    if not deletes:
        return None
    _outer, pieces, _mktemp, unbalanced = prescan(command)
    roots = fence_roots(root, environ)
    listed, budget = [], MATCH_CAP
    for _name, targets, working_dir in deletes:
        for word, expansion in targets:
            path = None
            if not unbalanced and expansion is not None:
                path = to_path(expansion, working_dir, environ)
            if path is None:
                listed.append(restore(word, pieces) + " (unresolved)")
                continue
            found = matches(path, budget)
            if found is None:
                listed.append(restore(word, pieces) + " (unresolved)")
                budget = -1
                continue
            if found != [path]:
                budget -= len(found)
            for match in found:
                real = os.path.realpath(match)
                if not beneath(real, roots):
                    listed.append(real)
    if not listed:
        return None
    if chained:
        return "deny", DENY_REASON
    return "ask", ASK_PREFIX + "; ".join(listed)


def document(decision, reason):
    """The decision, one ASCII line."""
    return json.dumps({"hookSpecificOutput": {"hookEventName": "PreToolUse",
                                              "permissionDecision": decision,
                                              "permissionDecisionReason": reason}})


def main(argv):
    try:
        root = argv[1] if len(argv) > 1 and os.path.isdir(argv[1]) else None
        payload = parse_payload(sys.stdin.buffer.read())
        if payload is None:
            result = ("ask", READ_REASON % "stdin is not a JSON object")
        else:
            result = decide(payload, root, os.environ)
    except Exception as error:
        result = ("ask", ERROR_REASON % (type(error).__name__, error))
    if result is not None:
        print(document(*result))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
