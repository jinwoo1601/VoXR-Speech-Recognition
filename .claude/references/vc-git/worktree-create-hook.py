# =============================================================================
# Purpose:  WorktreeCreate hook cutting a parallel writer's tree per the trees binding
# Layer:    Hooks
# Owns:     (script, no public types)
# Depends:  (none)
# =============================================================================

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
    pass


class CutFailed(Exception):
    pass


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


def is_plain_name(value):
    if not isinstance(value, str) or not value:
        return False
    if "/" in value or "\\" in value or ":" in value:
        return False
    return value.strip(".") != ""


def trees_section(text):
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
    if len(content) == 1 and re.match(
            r"none(\s|\u2014|$)", content[0].removeprefix("- ").replace("`", "")) is not None:
        return "none", None
    return None, lines


def slot_token(lines, label):
    for line in lines:
        if line.lstrip().startswith(f"- {label}:"):
            for token in re.findall(r"`([^`]+)`", line):
                if "<branch>" in token and "<unit>" in token:
                    return token
            return None
    return None


def git(cwd, *args):
    return subprocess.run(["git", "-C", cwd, *args], stdin=subprocess.DEVNULL,
                          capture_output=True, encoding="utf-8", errors="replace")


def plan_writer(payload, root):
    prompt_id = payload.get("prompt_id")
    if not isinstance(prompt_id, str) or not prompt_id:
        raise Refused(SESSION)
    cwd = payload.get("cwd")
    if not isinstance(cwd, str) or not cwd or not Path(cwd).is_dir():
        raise Refused(NO_CWD)
    name = payload.get("name")
    if not is_plain_name(name):
        raise Refused(NO_NAME)
    form, lines = absent_form(root)
    if form is not None:
        raise Refused(ABSENT.format(form=form))
    location = slot_token(lines, "Location")
    if location is None or not re.match(r"^[A-Za-z]:[\\/]", location):
        raise Refused(NO_LOCATION)
    naming = slot_token(lines, "Writer-branch naming")
    if naming is None:
        raise Refused(NO_NAMING)
    branch = git(cwd, "branch", "--show-current").stdout.strip()
    if not branch:
        raise Refused(NO_BRANCH.format(cwd=cwd))
    path = location.replace("<branch>", branch).replace("<unit>", name).replace("/", "\\")
    writer_branch = naming.replace("<branch>", branch).replace("<unit>", name)
    if os.path.lexists(path):
        raise Refused(PATH_EXISTS.format(path=path))
    if git(cwd, "show-ref", "--verify", "--quiet", "refs/heads/" + writer_branch).returncode == 0:
        raise Refused(BRANCH_EXISTS.format(branch=writer_branch))
    return cwd, path, writer_branch


def cut(cwd, path, writer_branch):
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
            raise Refused(NOT_AN_OBJECT)
        cwd, path, writer_branch = plan_writer(payload, resolve_root(argv))
        cut(cwd, path, writer_branch)
    except Refused as refusal:
        print(PREFIX + str(refusal), file=sys.stderr)
        return 2
    except CutFailed as failure:
        print(PREFIX + str(failure), file=sys.stderr)
        return 1
    print(path)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
