# =============================================================================
# Purpose:  WorktreeCreate hook cutting a parallel writer's tree per the trees binding
# Layer:    Hooks
# Owns:     (script, no public types)
# Depends:  (none)
# =============================================================================
"""Claude Code `WorktreeCreate` hook: cuts a parallel writer's tree and branch
where the project's `trees` binding says, from the dispatching branch's tip, or
refuses.

Standard library only, and never an import of `harness.py` - the hook runs under
whatever interpreter `python3` names, and `harness.py` exits at import time when
PyYAML or jsonschema is missing. So the reading of a bindings section is
re-implemented here rather than imported.

A refusal is one stderr line and exit 2, with nothing made; a failed cut is
rolled back to nothing and exits 1; success prints the tree's path alone on
stdout and exits 0. Claude Code takes whatever stdout carries as the tree, so
every git command's output is captured. Beyond the stdin parse, only the module's
own two exception classes are caught, so an unforeseen bug is a traceback and a
non-zero exit, which aborts the creation too.
"""

import json
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path

BINDINGS_REL = ".claude/bindings/vc-git.md"
PREFIX = "worktree-create-hook: "
NOT_AN_OBJECT = "the input on stdin is not a JSON object"
SESSION = ("session worktree requests are refused: open a plain claude session in a "
           "branch tree cut by the Create procedure of ## trees in .claude/bindings/vc-git.md")
NO_CWD = "input key cwd is missing or names no directory"
NO_NAME = "input key name is missing or not a plain name"
ABSENT = ("## trees in .claude/bindings/vc-git.md reads absent ({form}); writer trees are "
          "refused until it is filled")
NO_LOCATION = ("## trees slot Location needs a backticked absolute Windows-form path holding "
               "both <branch> and <unit>")
NO_NAMING = ("## trees slot Writer-branch naming needs a backticked name holding both "
             "<branch> and <unit>")
NO_BRANCH = ("the dispatching tree {cwd} is on no branch; a writer tree is cut from a "
             "branch's tip")
PATH_EXISTS = "the writer tree path {path} already exists"
BRANCH_EXISTS = "the writer branch {branch} already exists"
CUT_FAILED = "the cut failed: git worktree add exited {code}: {message}"


class Refused(Exception):
    """A request the hook declines before anything is made: exit 2."""


class CutFailed(Exception):
    """A `git worktree add` that failed, rolled back: exit 1."""


def resolve_root(argv):
    """The project root: argv[1] when it names a directory, else the process cwd."""
    if len(argv) > 1:
        candidate = Path(argv[1])
        if candidate.is_dir():
            return candidate
    return Path.cwd()


def parse_payload(data):
    """The hook payload as a mapping, or None for stdin the hook cannot read.

    None covers bytes that are not JSON at all, bytes that are not UTF-8, and JSON
    that parses to a list, a string or a number rather than to an object.
    """
    try:
        document = json.loads(data)
    except ValueError:
        return None
    return document if isinstance(document, dict) else None


def is_plain_name(value):
    """True for a non-empty string with no `/`, no `\\` and no `:` that is not
    dots alone - a name that can only fill `<unit>`, never steer a path.
    """
    if not isinstance(value, str) or not value:
        return False
    if "/" in value or "\\" in value or ":" in value:
        return False
    return value.strip(".") != ""


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


def absent_form(root):
    """`(form, None)` where `trees` reads absent - `missing`, `empty`, `TODO` or
    `none`, checked in that order - else `(None, lines)`, the filled section's lines.

    A content line is one that, stripped, is neither blank nor a comment.
    """
    path = root / BINDINGS_REL
    if not path.is_file():
        return "missing", None
    lines = trees_section(path.read_text(encoding="utf-8"))
    if lines is None:
        return "missing", None
    stripped = [line.strip() for line in lines]
    content = [line for line in stripped
               if line and not (line.startswith("<!--") and line.endswith("-->"))]
    if not content:
        return "empty", None
    if any("TODO" in line for line in lines):
        return "TODO", None
    if len(content) == 1 and content[0].removeprefix("- ").replace("`", "") == "none":
        return "none", None
    return None, lines


def slot_token(lines, label):
    """On the first `- <label>:` line, the first backticked token holding both
    `<branch>` and `<unit>`; None where there is no such line or token.
    """
    for line in lines:
        if line.lstrip().startswith(f"- {label}:"):
            for token in re.findall(r"`([^`]+)`", line):
                if "<branch>" in token and "<unit>" in token:
                    return token
            return None
    return None


def git(cwd, *args):
    """git run in `cwd`, every stream captured so nothing reaches the hook's own."""
    return subprocess.run(["git", "-C", cwd, *args], stdin=subprocess.DEVNULL,
                          capture_output=True, encoding="utf-8", errors="replace")


def plan_writer(payload, root):
    """Steps 2 through 6: `(cwd, path, writer_branch)`, or `Refused`."""
    prompt_id = payload.get("prompt_id")
    if not isinstance(prompt_id, str) or not prompt_id:
        raise Refused(SESSION)                                # [step 2]
    cwd = payload.get("cwd")
    if not isinstance(cwd, str) or not cwd or not Path(cwd).is_dir():
        raise Refused(NO_CWD)                                 # [step 3]
    name = payload.get("name")
    if not is_plain_name(name):
        raise Refused(NO_NAME)                                # [step 3]
    form, lines = absent_form(root)
    if form is not None:
        raise Refused(ABSENT.format(form=form))               # [step 4]
    location = slot_token(lines, "Location")
    if location is None or not re.match(r"^[A-Za-z]:[\\/]", location):
        raise Refused(NO_LOCATION)                            # [step 4]
    naming = slot_token(lines, "Writer-branch naming")
    if naming is None:
        raise Refused(NO_NAMING)                              # [step 4]
    branch = git(cwd, "branch", "--show-current").stdout.strip()
    if not branch:
        raise Refused(NO_BRANCH.format(cwd=cwd))              # [step 5]
    path = location.replace("<branch>", branch).replace("<unit>", name).replace("/", "\\")
    writer_branch = naming.replace("<branch>", branch).replace("<unit>", name)
    if os.path.lexists(path):
        raise Refused(PATH_EXISTS.format(path=path))          # [step 6]
    if git(cwd, "show-ref", "--verify", "--quiet", "refs/heads/" + writer_branch).returncode == 0:
        raise Refused(BRANCH_EXISTS.format(branch=writer_branch))  # [step 6]
    return cwd, path, writer_branch


def cut(cwd, path, writer_branch):
    """Step 7: the writer tree and its branch from `HEAD`, or `CutFailed` with
    whatever this call made removed - the tree first, since git will not delete a
    branch a tree has checked out.
    """
    added = git(cwd, "worktree", "add", "-b", writer_branch, path, "HEAD")
    if added.returncode == 0:
        return
    wanted = os.path.normcase(os.path.normpath(path))
    listed = git(cwd, "worktree", "list", "--porcelain").stdout.splitlines()
    if any(line.startswith("worktree ")
           and os.path.normcase(os.path.normpath(line[len("worktree "):])) == wanted
           for line in listed):
        git(cwd, "worktree", "remove", "--force", path)
    if os.path.isdir(path):
        shutil.rmtree(path, ignore_errors=True)
    ref = "refs/heads/" + writer_branch
    if git(cwd, "show-ref", "--verify", "--quiet", ref).returncode == 0:
        git(cwd, "branch", "-D", writer_branch)
    message = " ".join(line.strip() for line in added.stderr.splitlines() if line.strip())
    text = CUT_FAILED.format(code=added.returncode, message=message)
    remains = []
    if os.path.lexists(path):
        remains.append(path)
    if git(cwd, "show-ref", "--verify", "--quiet", ref).returncode == 0:
        remains.append(writer_branch)
    if remains:
        text += "; left behind: " + ", ".join(remains)
    raise CutFailed(text)


def main(argv):
    try:
        payload = parse_payload(sys.stdin.buffer.read())
        if payload is None:
            raise Refused(NOT_AN_OBJECT)                      # [step 1]
        cwd, path, writer_branch = plan_writer(payload, resolve_root(argv))
        cut(cwd, path, writer_branch)                         # [step 7]
    except Refused as refusal:
        print(PREFIX + str(refusal), file=sys.stderr)
        return 2
    except CutFailed as failure:
        print(PREFIX + str(failure), file=sys.stderr)
        return 1
    print(path)                                               # [step 8]
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
