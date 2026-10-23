# =============================================================================
# Purpose:  PreToolUse fence on a Bash call's rm and rmdir targets
# Layer:    Hooks
# Owns:     (script, no public types)
# Depends:  (none)
# =============================================================================
"""Claude Code `PreToolUse` hook on `Bash`: the rm fence, which finds each `rm`
and `rmdir` in a call, its target words and the working directory each one runs
in, and stops a delete whose target lies outside the project, its trees,
`.scratch/`, the temp folders and the job folder.

Standard library only, and never an import of `harness.py`, for the agent
guard's reason: the hook runs under whatever interpreter the platform starts.

A quote-aware prescan lifts nested text out of the command - command and
process substitutions, each replaced by one sentinel word, and heredoc bodies,
removed; a `cygpath` of one word, or a `mktemp`, stands for the path it prints,
and `${NAME:-WORD}` for NAME's value where NAME is set and not empty - and
the rest is tokenized by the vc guard's tokenizer and walked segment by segment:
parentheses, piped or backgrounded brace groups and backgrounded and-or lists scope the working directory and the variables, a `cd`
moves the directory, assignments and `export` set variables, and an `rm` or
`rmdir` yields a delete; a function body's deletes and the names it sets are
unpinned, and after a loop's `done` the directory and the names its body may
change are unknown. Nested text is searched with the same reader,
never trusted: a delete found in it has one unpinned target; a runner's text is
nested text - a shell's or `su`'s `-c` operand, a shell's past `xargs`, `eval`'s
and `ssh`'s command words, a here-string a runner receives, and the words
written into a `>(...)` a runner reads. A heredoc body under a quoted delimiter
word is data unless a runner receives it, as its receiver's command word or down
the pipe or compound command its receiver hands it to, or through a `>(...)` in
those segments - an unresolvable command word and a function the call defines
counting as runners there - and its `rm` words leave the surplus; one under an unquoted word is data only to a note-writer (`cat`,
`tee`) in a call no runner receives. The command word is
read past the wrapper words, `timeout`, `nice` and `stdbuf` with their options
among them, and a call holding no `rm` word is not walked. Paths
are read in Windows form through `ntpath`; a spelling the reader cannot
normalize is None.

The class rules are gated by a surplus: the call's `rm` words, counted over the
outer text and every lifted piece, a data heredoc body's aside, outnumbering the
`rm` and `rmdir` command words the top-level walk read. A `tally` threaded
through the top-level walk and every nested one gathers that read count, the
`rm` words of the top-level data heredoc bodies, the first segment whose command
word is a runner, and whether a top-level segment chains the call; `nested` marks a
walk of nested text, which adds nothing to the count. With a surplus, Rule 2
keeps each command word the walk cannot resolve to a literal name as a delete of
its segment that does not chain the call, and Rule 3, when any walk saw a runner,
adds one delete listing the first runner's segment; without one, Rule 2's deletes
are dropped and the call reads as before the class rules.

The fence roots are the project root named by argv[1], its `.scratch`, the trees
roots read from the bindings file the manifest's `bindings` filler names for
`trees`, `core`'s by default, and the `TMP`, `TEMP` and `CLAUDE_JOB_DIR` folders; each is real-path resolved and a drive root dropped. A target
is real-path resolved, a glob tested match by match, its expansion bounded by a
walk budget in time, and is inside only when it lies strictly beneath a root.
No target outside is silence; outside targets ask,
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
import threading
import time

PUNCTUATION = "();<>|&\n"
REDIRECTS = frozenset(("<", ">", ">>", "<<", "<<<", ">&", "<&", "&>", "&>>", ">|", "<>"))
ASSIGNMENT = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*=")
WRAPPERS = frozenset(("command", "exec", "time", "nohup", "then", "do", "else", "!",
                      "if", "elif", "while", "until", "builtin", "noglob"))
RESERVED = frozenset(("then", "do", "else", "!", "if", "elif", "while", "until", "time"))
ENV_VALUED = frozenset(("-u", "--unset", "-C", "--chdir", "-S", "--split-string"))
SUDO_VALUED = frozenset(("-u", "--user", "-g", "--group", "-C", "--close-from", "-D", "--chdir",
                         "-h", "--host", "-p", "--prompt", "-r", "--role", "-t", "--type",
                         "-U", "--other-user", "-T", "--command-timeout", "-R", "--chroot"))
TIMEOUT_VALUED = frozenset(("-s", "--signal", "-k", "--kill-after"))
NICE_VALUED = frozenset(("-n", "--adjustment"))
STDBUF_VALUED = frozenset(("-i", "-o", "-e"))
# The shells: each reads a `-c` operand, and a body it receives, as commands.
SHELLS = frozenset(("sh", "bash", "dash", "zsh", "ksh", "mksh", "ash"))
# The runners, Rule 3's list: each runs text it is given, anywhere in the call.
RUNNERS = SHELLS | frozenset(("cmd", "pwsh", "powershell", "eval", "su", "ssh", "source", ".",
                              "xargs"))
# The note-writers: the closed list of receivers that keep a heredoc body data.
NOTE_WRITERS = frozenset(("cat", "tee"))
# The options that take a separate value, of `xargs` and of `ssh`.
XARGS_VALUED = frozenset(("-a", "-d", "-E", "-I", "-L", "-n", "-P", "-s", "--arg-file",
                          "--delimiter", "--eof", "--replace", "--max-lines", "--max-args",
                          "--max-procs", "--max-chars", "--process-slot-var"))
SSH_VALUED = frozenset(("-B", "-b", "-c", "-D", "-E", "-e", "-F", "-I", "-i", "-J", "-L", "-l",
                        "-m", "-O", "-o", "-P", "-p", "-Q", "-R", "-S", "-W", "-w"))
OUTPUT_REDIRECTS = frozenset((">", ">>", ">|", "&>", "&>>"))
# A heredoc operator fused into another operator's token, its receiver unnamed.
FUSED_HEREDOC = re.compile(r"(?<!<)<<(?!<)")
MKTEMP_OPTIONS = re.compile(r"-[dqu]+|--directory|--quiet|--dry-run")
# The `cygpath` options whose output stands for its one operand.
CYGPATH_OPTIONS = re.compile(r"-[uwma]+")
SENTINEL = re.compile(r"\x01(\d+)\x02")
APPEND = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*\+=")
# A brace list or range, read by `expand` and by Rule 2.
BRACE = re.compile(r"\{[^{}]*(?:,|\.\.)[^{}]*\}")
PLACEHOLDER = "rm-fence-mktemp-placeholder"
MATCH_CAP = 1000
# The name of a Rule 2 delete: an unresolvable command word, listed from that word on.
RULE_2 = "rule 2"
# The walk budget, in seconds, of a call's pattern expansions: provisional, the tuning pass's.
WALK_BUDGET = 5.0


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


def rm_words(text):
    """The `rm` and `rmdir` words of `text`, in text order: the text split at
    whitespace, the operator characters, `=` and the backtick, each part with its
    quotes removed read through `command_name`; empty for none."""
    words = []
    for part in re.split(r"[\s;&|()<>=`]+", text):
        name = command_name(part.replace("'", "").replace('"', ""))
        if name in ("rm", "rmdir"):
            words.append(name)
    return words


def command_start(segment):
    """The index of the segment's command word: past leading assignments, past
    redirections, and past the wrapper words, `builtin` and `noglob` among them,
    `sudo`, `env`, `timeout`, `nice` and `stdbuf`, each with the options after it,
    and `timeout` with its duration."""
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
        elif name == "busybox":
            valued = frozenset()
        elif name == "timeout":
            valued = TIMEOUT_VALUED
        elif name == "nice":
            valued = NICE_VALUED
        elif name == "stdbuf":
            valued = STDBUF_VALUED
        else:
            break
        index += 1
        while index < len(segment) and (segment[index].startswith("-")
                                        or (name == "env" and ASSIGNMENT.match(segment[index]))):
            index += 2 if segment[index] in valued else 1
        if name == "timeout" and index < len(segment):
            index += 1
    return index


def past_options(words, valued):
    """The index of the first word in `words` that is not an option, each option
    in `valued` taking the word after it."""
    index = 0
    while index < len(words) and words[index].startswith("-"):
        index += 2 if words[index] in valued else 1
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
    each piece as (written, inner, literal) - literal None for a substitution, and
    for a heredoc body True when its delimiter word holds any quoting, else False -
    the mktemp assignments as piece index to name,
    and whether the nesting or the quoting is unbalanced. A comment - a `#` outside
    quotes at a word start - is dropped up to its newline unless `${`, `$[` or `[[`
    is written before it in the outer text, closed or not; a `$((` is lifted whole,
    as a substitution is, so a `#` inside it stays in its piece. A command that
    already holds a sentinel character is returned whole and unbalanced."""
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
        if quote is None and character == "#" and (
                not outer or outer[-1] in (" ", "\t", "\n", ";", "&", "|", "(", ")")) and (
                not any(opener in "".join(outer) for opener in ("${", "$[", "[["))):
            while index < length and command[index] != "\n":
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
            pieces.append((written, inner, None))
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
            word_start, delimiter, word_quote, literal = index, [], None, False
            while index < length:
                letter = command[index]
                if word_quote is not None:
                    if letter == word_quote:
                        word_quote = None
                    else:
                        delimiter.append(letter)
                elif letter in "'\"":
                    word_quote, literal = letter, True
                elif letter == "\\":
                    delimiter.append(command[index + 1:index + 2])
                    literal = True
                    index += 1
                elif letter in " \t\n;&|()<>":
                    break
                else:
                    delimiter.append(letter)
                index += 1
            outer.append(command[start:index])
            if index > word_start:
                pending.append(("".join(delimiter), tabs, literal))
            continue
        if quote is None and character == "\n" and pending:
            outer.append("\n")
            index += 1
            for delimiter, tabs, literal in pending:
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
                pieces.append((command[start:index], command[start:body_end], literal))
            pending = []
            continue
        outer.append(character)
        index += 1
    text = "".join(outer)
    return text, pieces, mktemp, unbalanced or quotes_open(text)


def expand(word, variables):
    """`word` with `$NAME`, `${NAME}` and `${NAME:-WORD}` replaced from
    `variables`, the last by NAME's value, or None for a sentinel, an extended
    pattern, a brace list or range, an absent name, a `${NAME:-WORD}` whose NAME is
    empty, or any other `$` left. Glob characters stay."""
    if "\x01" in word or "\x02" in word:
        return None
    if any(opener in word for opener in ("@(", "!(", "+(", "?(", "*(")):
        return None
    if BRACE.search(re.sub(r"\$\{[A-Za-z_][A-Za-z0-9_]*\}", "", word)):
        return None
    reference = r"\$(?:\{([A-Za-z_][A-Za-z0-9_]*)(:-[^{}]*)?\}|([A-Za-z_][A-Za-z0-9_]*))"
    if "$" in re.sub(reference, "", word):
        return None
    for braced, default, bare in re.findall(reference, word):
        if (braced or bare) not in variables or (default and not variables[braced]):
            return None
    return re.sub(reference, lambda match: variables[match.group(1) or match.group(3)], word)


def pin_text(inner, environ, mktemp_only=False):
    """The text the substitution `inner` stands for, or None: for `cygpath` with
    `CYGPATH_OPTIONS` and one operand, that operand, its names braced, or the pin
    of the `mktemp` substitution it is whole; for `mktemp`, a fresh name in its
    folder - the `-p`/`--tmpdir` folder, else the template's, else `temp_root`'s
    with no template or with `-t` - the template's own folder joined beneath it.
    No pin for an unbalanced inner text, a word holding an operator, a sentinel
    other than that operand, an unknown option, a second template, or no temp
    root where one is needed; `mktemp_only` refuses `cygpath`."""
    outer, pieces, _mktemp, unbalanced = prescan(inner)
    words = tokenize(outer)
    if unbalanced or not words or any(character in PUNCTUATION
                                      for word in words for character in word):
        return None
    if words[0] == "cygpath" and not mktemp_only:
        operand = words[-1]
        if (len(words) < 3 or operand.startswith("-")
                or not all(CYGPATH_OPTIONS.fullmatch(word) for word in words[1:-1])):
            return None
        whole = SENTINEL.fullmatch(operand)
        if whole and int(whole.group(1)) < len(pieces):
            written, nested, _literal = pieces[int(whole.group(1))]
            return pin_text(nested, environ, True) if written.startswith(("$(", "`")) else None
        if "\x01" in operand or "\x02" in operand:
            return None
        return re.sub(r"\$([A-Za-z_][A-Za-z0-9_]*)", r"${\1}", operand)
    if words[0] != "mktemp":
        return None
    folder, template, temp, index = None, None, False, 1
    while index < len(words):
        word = words[index]
        if "\x01" in word or "\x02" in word:
            return None
        if MKTEMP_OPTIONS.fullmatch(word) or re.fullmatch(r"-[dqut]+", word):
            temp = temp or (not word.startswith("--") and "t" in word)
        elif word in ("-p", "--tmpdir", "--suffix"):
            index += 1
            if index >= len(words):
                return None
            if word != "--suffix":
                folder = words[index]
        elif word.startswith(("--tmpdir=", "--suffix=")):
            if word.startswith("--tmpdir="):
                folder = word.split("=", 1)[1]
        elif word.startswith("-") or template is not None:
            return None
        else:
            template = word
        index += 1
    if folder is None:
        folder = temp_root(environ) if template is None or temp else ""
        if folder is None:
            return None
    return ntpath.join(folder, ntpath.dirname(template or ""), PLACEHOLDER)


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


def nested_deletes(text, shell=False, tally=None):
    """The name of the first delete in nested text that is not a Rule 2 one, else
    `RULE_2` when Rule 2's are all it holds, read with fresh state as a nested walk,
    `tally` threaded in, or None; `shell` True when a runner receives the text: a
    `RUNNERS` word, or a heredoc receiver not in `NOTE_WRITERS`."""
    deletes, _chained = walk(text, None, {}, shell, tally, True)
    names = [delete[0] for delete in deletes if delete[0] != RULE_2]
    if names:
        return names[0]
    return RULE_2 if deletes else None


def expanded_deletes(body, tally=None):
    """The name of the first delete inside the command substitutions an unquoted
    heredoc body expands, or None: quotes in the body are literal, and a backslash
    escapes the character after it. A body the prescan cannot lift a substitution
    from is searched whole."""
    index, fallback = 0, None
    while index < len(body):
        if body[index] == "\\":
            index += 2
            continue
        if body.startswith("$(", index) or body[index] == "`":
            pieces = prescan(body[index:])[1]
            if not pieces:
                return nested_deletes(body, tally=tally) or fallback
            written, inner, _literal = pieces[0]
            found = nested_deletes(inner, tally=tally)
            if found == RULE_2:
                fallback = RULE_2
            elif found is not None:
                return found
            index += len(written)
            continue
        index += 1
    return fallback


def walk(command, cwd, variables, shell=False, tally=None, nested=False):
    """(deletes, chained): each delete as (name, targets, working dir), each target
    as (word, expansion or None), the working dir None where it is unknown; chained
    is True when any outer segment is not an `rm` or `rmdir`. `shell` True when a
    runner receives the command - a `RUNNERS` word, or a heredoc receiver not in
    `NOTE_WRITERS`; a call that feeds a runner, the runner as `command_start`
    finds it, or is fed to one, reads its nested text as runner-received,
    once the walk knows it. Searched as commands: a shell's or `su`'s `-c`
    operands, a shell's past `xargs`, `eval`'s and `ssh`'s command words joined, a
    here-string a runner receives, and the other words of a segment writing into a
    `>(...)` a runner reads. A heredoc body under a quoted delimiter word is searched
    only where a runner receives it: `shell`, a receiver that is a runner,
    unresolvable or a function defined earlier in the call, or whose segment holds a
    `>(...)` whose command word is a runner or unresolvable, a fused heredoc
    operator, or a body handed down a pipe or out of a group, a loop or a compound
    command in a call that holds any such word. An
    assignment value, a `cd` operand and a delete target led by a `$(...)` or
    backtick substitution read it as its `pin_text`.

    The command word is read after `expand`, as written where it cannot expand. With
    `tally`, a mapping of `runner`, `read`, `chained` and `data` updated in place and
    threaded into every nested walk, `data` gathering the top-level walk's data
    heredoc bodies' `rm` words, Rule 2 applies: a command word whose last component,
    unless exactly `[` or `[[`, still holds a `$`, a sentinel, a glob character or a
    brace list or range is a `RULE_2` delete of the segment from that word on, and
    `decide` keeps it only on a surplus. The first segment whose command word is a
    runner records that segment, from the word on, as `runner` (Rule 3); `nested`
    False marks the top-level walk, whose `rm` and `rmdir` command words add to `read`
    and whose chaining segments, an unresolvable one aside, and escaped-quote
    fallback set `chained`. With no tally, the walk reads as before Rules 2 and 3."""
    seed = dict(variables)
    variables = dict(variables)
    directory = to_path(cwd, None, seed) if isinstance(cwd, str) else None
    outer, pieces, mktemp, _unbalanced = prescan(command)
    deletes, chained, reached, feeds_shell, deferred = [], False, set(), shell, {}
    body_runs, runner_seen, handed = shell, False, False

    def pin(word):
        """`word` with a `$(...)` or backtick substitution at its start replaced by
        its `pin_text`, else as written."""
        whole = SENTINEL.match(word)
        if whole is None or int(whole.group(1)) >= len(pieces):
            return word
        written, inner, _literal = pieces[int(whole.group(1))]
        text = pin_text(inner, seed) if written.startswith(("$(", "`")) else None
        return word if text is None else text + word[whole.end():]

    def resolve(word):
        """(name, unresolved): the command word `word` after `expand`, as written
        where it cannot expand, read through `command_name`, and True when its last
        component, unless exactly `[` or `[[`, still holds a `$`, a sentinel, a glob
        character or a brace list or range."""
        text = expand(word, variables)
        text = word if text is None else text
        last = re.split(r"[\\/]", text)[-1]
        return command_name(text), last not in ("[", "[[") and (
            is_pattern(last) or "$" in last or "\x01" in last
            or BRACE.search(last) is not None)

    def reads_runner(segment):
        """True when a word of the segment holds a `>(...)` whose command word is a
        runner or unresolvable."""
        for token in segment:
            for match in SENTINEL.finditer(token):
                number = int(match.group(1))
                if number < len(pieces) and pieces[number][0].startswith(">("):
                    reader = tokenize(pieces[number][1])
                    start = command_start(reader)
                    if start < len(reader):
                        found, unknown = resolve(reader[start])
                        if found in RUNNERS or unknown:
                            return True
        return False

    def touch(name):
        """In a loop, unpin each target naming `name` recorded since the loop began -
        `$NAME`, or `${NAME` followed by any character not of a name, in the word or
        in the inner text of a substitution it holds; in an open function body, add
        the name to the body's names."""
        if looped:
            loop_names.add(name)
            naming = re.compile(r"\$\{?%s(?![A-Za-z0-9_])" % re.escape(name))

            def names(word):
                """True when `word`, or a substitution's inner text it holds, names it."""
                inners = [pieces[int(match.group(1))][1] for match in SENTINEL.finditer(word)
                          if int(match.group(1)) < len(pieces)]
                return any(naming.search(text) for text in [word] + inners)
            for at in range(loop_start, len(deletes)):
                found, targets, working_dir = deletes[at]
                deletes[at] = (found, [(word, None if names(word) else expansion)
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
        expanded = expand(pin(value), variables)
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
        chained, feeds_shell, body_runs = True, True, True
        if tally is not None and not nested:
            tally["chained"] = True
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
        loop_moved, loop_names, restores, functions = False, set(), [], set()
        heads, ends = 0, 0
        pairable = not (re.search(r"""['"#]|=\(|[@!+?*]\(|\(\(|\[\[""", outer)
                        or re.search(r"\\[\s;&|()<>]", outer)
                        or any("case" in segment for segment in segments))

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

        def terminator(p):
            """The location (position, offset) of the `&` ending the and-or list from
            segment p, followed into each group or parenthesis it opens, whose
            separators do not end it, or None when the list does not end at `&`."""
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
            """True when the and-or list from segment p ends at `&`, followed into
            each group or parenthesis it opens, whose separators do not end it."""
            return terminator(p) is not None

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
            nonlocal loop_moved
            if looped:
                loop_moved = True
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
            if FUSED_HEREDOC.search(breaks[position]):
                feeds_shell = body_runs = True
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
                    else:
                        ending = terminator(position)
                        if ending is not None:
                            restores.append((ending, saved, kept))
                    close()
                elif character == "&":
                    for location, saved, kept in reversed(restores):
                        if location == (position, offset):
                            directory, variables = saved, kept
            if not segment:
                continue
            opening = definition(position)
            if body is None and pending is None:
                pending = opening
            subshell = (run(after(position)[0]) in ("|", "&", "|&")
                        or run(before(position)) in ("|", "|&") or backgrounded(position))
            index = command_start(segment)
            while index < len(segment) and APPEND.match(segment[index]):
                variables.pop(segment[index].split("+=", 1)[0], None)
                index += 1
                index += command_start(segment[index:])
            if not looped and any(word in ("do", "while", "until") for word in segment[:index]):
                looped, loop_start = True, len(deletes)
                loop_moved = False
                loop_names.clear()
            heads += sum(word in ("for", "select", "while", "until") for word in segment)
            if index < len(segment):
                name, unresolved = resolve(segment[index])
            else:
                name, unresolved = "", False
            if tally is not None and tally["runner"] is None and name in RUNNERS:
                tally["runner"] = restore(" ".join(segment[index:]), pieces)
            arguments = segment[index + 1:]
            feeds_shell = feeds_shell or name in RUNNERS or (
                "<<" in segment and name not in NOTE_WRITERS)
            receives = (name in RUNNERS or unresolved or name in functions
                        or reads_runner(segment))
            runner_seen = runner_seen or receives
            if opening is not None:
                functions.add(command_name(segment[-1]))
            if "<<" in segment:
                body_runs = body_runs or receives
                handed = (handed or name in ("", "done", "fi", "esac")
                          or run(after(position)[0]) in ("|", "|&"))
            for token in segment:
                for match in SENTINEL.finditer(token):
                    number = int(match.group(1))
                    if number < len(pieces):
                        reached.add(number)
                        deferred[len(deletes)] = number
                        deletes.append((None, [(match.group(0), None)], None))
            if name in RUNNERS:
                for at, token in enumerate(segment[:-1]):
                    if token == "<<<":
                        word = segment[at + 1]
                        found = nested_deletes(restore(word, pieces), True, tally)
                        if found is not None:
                            deletes.append((found, [(word, None)], None))
            for at, token in enumerate(segment[:-1]):
                whole = SENTINEL.fullmatch(segment[at + 1])
                if (token in OUTPUT_REDIRECTS and whole and int(whole.group(1)) < len(pieces)
                        and pieces[int(whole.group(1))][0].startswith(">(")):
                    reader = tokenize(pieces[int(whole.group(1))][1])
                    start = command_start(reader)
                    if start < len(reader) and command_name(reader[start]) in RUNNERS:
                        words, scan = [], 0
                        while scan < len(arguments):
                            if arguments[scan] in REDIRECTS:
                                scan += 2
                            else:
                                words.append(arguments[scan])
                                scan += 1
                        if words:
                            text = " ".join(words)
                            found = nested_deletes(restore(text, pieces), True, tally)
                            if found is not None:
                                deletes.append((found, [(text, None)], None))
                        break
            shelled = arguments if name in SHELLS or name == "su" else []
            if name == "xargs":
                past = past_options(arguments, XARGS_VALUED)
                if past < len(arguments) and command_name(arguments[past]) in SHELLS:
                    shelled = arguments[past + 1:]
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
                    value = expand(pin(operand), variables)
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
                deletes.append((name, [(word, expand(pin(word), variables))
                                       for word in delete_targets(name, arguments)],
                                directory))
                if tally is not None and not nested:
                    tally["read"] += 1
            elif any(re.fullmatch(r"-[A-Za-z]*c[A-Za-z]*", word) for word in shelled):
                start = next(offset for offset, word in enumerate(shelled)
                             if re.fullmatch(r"-[A-Za-z]*c[A-Za-z]*", word))
                for word in shelled[start + 1:]:
                    if word.startswith("-"):
                        continue
                    found = nested_deletes(restore(word, pieces), True, tally)
                    if found is not None:
                        deletes.append((found, [(word, None)], None))
            else:
                if unresolved and tally is not None:
                    deletes.append((RULE_2, [(" ".join(segment[index:]), None)], directory))
                if name == "eval" and arguments:
                    text = " ".join(arguments)
                    found = nested_deletes(restore(text, pieces), True, tally)
                    if found is not None:
                        deletes.append((found, [(text, None)], None))
                if name == "ssh":
                    remote = arguments[past_options(arguments, SSH_VALUED) + 1:]
                    if remote:
                        text = " ".join(remote)
                        found = nested_deletes(restore(text, pieces), True, tally)
                        if found is not None:
                            deletes.append((found, [(text, None)], None))
                for word in arguments:
                    named = re.match(r"([A-Za-z_]\w*)(\+?=|$)", word)
                    if named:
                        forget(named.group(1))
            if looped and segment[0] == "done":
                if loop_moved:
                    directory = None
                for touched in loop_names:
                    variables.pop(touched, None)
            if (looped and pairable and segment[0] == "done" and blank(segment[1:])
                    and not re.search(r"[()]", before(position) + after(position)[0])):
                ends += 1
                if ends >= heads:
                    looped, heads, ends = False, 0, 0
            if name not in ("rm", "rmdir"):
                chained = True
                if tally is not None and not nested and not unresolved:
                    tally["chained"] = True
        close(True)
    body_runs = body_runs or (handed and runner_seen)
    for at, number in deferred.items():
        _found, targets, working_dir = deletes[at]
        deletes[at] = (nested_deletes(pieces[number][1], feeds_shell, tally), targets,
                       working_dir)
    deletes = [delete for delete in deletes if delete[0] is not None]
    for number, (_written, inner, literal) in enumerate(pieces):
        if number in reached:
            continue
        if literal is None or (not literal and feeds_shell) or (literal and body_runs):
            found = nested_deletes(inner, feeds_shell or body_runs, tally)
        elif literal:
            found = None
            if tally is not None and not nested:
                tally["data"] += len(rm_words(inner))
        else:
            found = expanded_deletes(inner, tally)
        if found is not None:
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


def trees_roots(root):
    """The trees roots from the bindings file the manifest's `bindings` filler names
    for `trees`, `core`'s by default, and from no other: on its section's first
    `- Location:` line, each backticked drive-absolute token cut before its first
    component holding `<`, each prefix once. No root, a manifest that cannot be read
    or names no bare stem, an unreadable or missing file, and an absent section give
    none."""
    if root is None:
        return []
    path = trees_file(root, None)
    if path is None:
        return []
    try:
        with open(path, encoding="utf-8") as handle:
            text = handle.read()
    except (OSError, ValueError):
        return []
    lines = trees_section(text)
    if lines is None or absent_form(lines):
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


def fence_roots(root, environ):
    """The fence roots' keys, in order - the project, its `.scratch`, the trees
    roots, `TMP`, `TEMP` and the job folder `CLAUDE_JOB_DIR` names, whole - each
    real-path resolved, a drive root dropped, each key once."""
    candidates = []
    if root is not None:
        candidates.extend((root, ntpath.join(root, ".scratch")))
    candidates.extend(trees_roots(root))
    for name in ("TMP", "TEMP", "CLAUDE_JOB_DIR"):
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


def is_pattern(path):
    """True when `path` holds a glob character: `*`, `?` or `[`."""
    return any(character in path for character in "*?[")


def matches(path, limit, deadline=None):
    """The paths a target stands for: a pattern's matches, sorted, or the pattern
    itself when nothing matches; any other path alone. A pattern is expanded only
    within `limit` matches: None when the limit is below zero or the pattern
    matches more than `limit` paths. With a deadline, a `time.monotonic` value, the
    expansion runs on a worker joined until then, and a deadline passed, a worker
    still running, or one ended with no result gives None."""
    if not is_pattern(path):
        return [path]
    if limit < 0:
        return None
    if deadline is None:
        found = list(itertools.islice(glob.iglob(path), limit + 1))
    else:
        left = deadline - time.monotonic()
        if left <= 0:
            return None
        slot = []

        def expansion():
            """Store the pattern's bounded expansion in the slot; an error leaves it empty."""
            try:
                slot.append(list(itertools.islice(glob.iglob(path), limit + 1)))
            except (OSError, ValueError, RecursionError):
                pass
        worker = threading.Thread(target=expansion, daemon=True)
        worker.start()
        worker.join(left)
        if worker.is_alive() or not slot:
            return None
        found = slot[0]
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
    outer, pieces, _mktemp, unbalanced = prescan(command)
    count = len(rm_words(outer)) + sum(len(rm_words(inner))
                                       for _written, inner, _literal in pieces)
    if not count:
        return None
    variables = {}
    for name in ("TEMP", "TMP", "HOME", "CLAUDE_JOB_DIR"):
        if name in environ:
            path = to_path(environ[name], None, environ)
            if path is not None:
                variables[name] = path
    tally = {"runner": None, "read": 0, "chained": False, "data": 0}
    deletes, chained = walk(command, cwd, variables, tally=tally)
    if count - tally["data"] > tally["read"]:
        chained = tally["chained"]
        if tally["runner"] is not None:
            deletes.append(("rm", [(tally["runner"], None)], None))
    else:
        deletes = [delete for delete in deletes if delete[0] != RULE_2]
    if not deletes:
        return None
    roots = fence_roots(root, environ)
    listed, budget, deadline = [], MATCH_CAP, None
    for _name, targets, working_dir in deletes:
        for word, expansion in targets:
            path = None
            if not unbalanced and expansion is not None:
                path = to_path(expansion, working_dir, environ)
            if path is None:
                listed.append(restore(word, pieces) + " (unresolved)")
                continue
            if is_pattern(path) and deadline is None:
                deadline = time.monotonic() + WALK_BUDGET
            found = matches(path, budget, deadline)
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
