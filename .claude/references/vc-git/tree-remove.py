# =============================================================================
# Purpose:  Tree removal per the trees binding's Remove procedure, after its merged and session checks, read back
# Layer:    Scripts
# Owns:     Refusal (exception class)
# Depends:  (none)
# =============================================================================

import argparse
import datetime
import json
import os
import re
import signal
import subprocess
import sys
import time
from pathlib import Path

BINDINGS_REL = os.path.join(".claude", "bindings", "vc-git.md")
PROBE_SUFFIX = ".release-probe"
DRIVE_FORM = re.compile(r"^/([A-Za-z])(?=/|$)")
SPAN = re.compile(r"`([^`]*)`")
LIST_PREFIX = "- List command:"
UNBOUND_VALUES = ("todo", "none")
LIST_TIMEOUT = 30
READ_KILL_WAIT = 2
WINDOWS = os.name == "nt"
VC_HEADING = re.compile(r"^#{2,}\s*vc(?=\s|$)", re.IGNORECASE | re.MULTILINE)
NEXT_HEADING = re.compile(r"^#{1,6}\s")
MAIN_SLOT = "- Main branch:"
ABSOLUTE = re.compile(r"[A-Za-z]:[\\/]")
HUB_CHECKOUT = re.compile(r"the main checkout is `([^`]+)`")
DATE = re.compile(r"[0-9]{4}-[0-9]{2}-[0-9]{2}")
ITEM_SLUG = re.compile(r"[a-z0-9]+(?:-[a-z0-9]+)*")
ITEM_SLUG_MAX = 64
ITEM_LINE = re.compile(r"([a-z]+): (.+)")
HALTS_DIR = "memory/halts"
HALT_KEYS = ("branch", "head", "date", "stage", "item", "fate", "writer")
HALT_BRANCH = re.compile(r"(?![/.])(?!.*\.\.)[A-Za-z0-9._/-]+")
HALT_HEAD = re.compile(r"[0-9a-f]{40}|[0-9]+")


class Refusal(Exception):
    pass


def read_lf(path):
    return path.read_bytes().decode("utf-8").replace("\r\n", "\n")


def first_line(*outputs):
    for output in outputs:
        for line in output.splitlines():
            if line.strip():
                return line.strip()
    return "no output"


def normalise_path(path):
    path = DRIVE_FORM.sub(r"\1:", path)
    return path.replace("/", "\\").rstrip("\\").casefold()


def is_at_tree(cwd, tree):
    cwd, tree = normalise_path(cwd), normalise_path(tree)
    return cwd == tree or cwd.startswith(tree + "\\")


def list_command_in(text):
    lines = text.split("\n")
    if "## session-launch" not in lines:
        return None
    for line in lines[lines.index("## session-launch") + 1:]:
        if line.startswith("## "):
            return None
        if line.startswith(LIST_PREFIX):
            value = line[len(LIST_PREFIX):].strip()
            span = SPAN.search(value)
            if (value.casefold().startswith(UNBOUND_VALUES) or span is None
                    or span.group(1).strip().casefold() in UNBOUND_VALUES):
                return None
            return span.group(1)
    return None


def session_line(entry):
    fields = [str(entry.get(key) or "-") for key in ("id", "name", "kind")]
    return "  ".join(fields + [entry["cwd"], session_key(entry)])


def session_key(entry):
    return str(entry.get("sessionId") or "-")


def at_tree(sessions, tree):
    return [entry for entry in sessions
            if isinstance(entry, dict) and isinstance(entry.get("cwd"), str)
            and is_at_tree(entry["cwd"], tree)]


def kill_tree(process):
    if WINDOWS:
        try:
            subprocess.run(["taskkill", "/F", "/T", "/PID", str(process.pid)],
                           stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                           stderr=subprocess.DEVNULL, timeout=READ_KILL_WAIT)
        except (OSError, subprocess.TimeoutExpired):
            pass
    else:
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except OSError:
            pass
    try:
        process.kill()
    except OSError:
        pass
    try:
        process.wait(READ_KILL_WAIT)
    except subprocess.TimeoutExpired:
        pass


def bounded_run(args, *, timeout=None, input=None, **kwargs):
    kwargs.setdefault("stdout", subprocess.PIPE)
    kwargs.setdefault("stderr", subprocess.PIPE)
    if input is not None:
        kwargs["stdin"] = subprocess.PIPE
    if not WINDOWS:
        kwargs["start_new_session"] = True
    process = subprocess.Popen(args, **kwargs)
    try:
        out, err = process.communicate(input, timeout=timeout)
    except subprocess.TimeoutExpired as expired:
        kill_tree(process)
        try:
            expired.stdout, expired.stderr = process.communicate(timeout=READ_KILL_WAIT)
        except subprocess.TimeoutExpired:
            pass
        raise expired
    except BaseException:
        kill_tree(process)
        raise
    return subprocess.CompletedProcess(process.args, process.returncode, out, err)


def list_sessions(root):
    command = None
    for path in sorted((root / ".claude" / "bindings").glob("*.md")):
        text = read_lf(path)
        if "## session-launch" in text.split("\n"):
            command = list_command_in(text)
            break
    if command is None:
        return None
    try:
        result = bounded_run(command, shell=True, timeout=LIST_TIMEOUT)
    except subprocess.TimeoutExpired as expired:
        raise Refusal(f"List command timed out after {LIST_TIMEOUT}s: {command}: "
                      + first_line((expired.stderr or b"").decode("utf-8", "replace"),
                                   (expired.stdout or b"").decode("utf-8", "replace"))) from None
    if result.returncode:
        raise Refusal(f"List command exited {result.returncode}: {command}: "
                      + first_line(result.stderr.decode("utf-8", "replace"),
                                   result.stdout.decode("utf-8", "replace")))
    try:
        sessions = json.loads(result.stdout.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError):
        sessions = None
    if not isinstance(sessions, list):
        raise Refusal(f"List command output is not a JSON array: {command}")
    return sessions


def normalise(path):
    return path.replace("/", "\\").rstrip("\\").casefold()


def binding_text(root):
    try:
        with open(os.path.join(root, BINDINGS_REL), encoding="utf-8") as handle:
            return handle.read()
    except (OSError, ValueError):
        return None


def bound_main(root):
    text = binding_text(root)
    heading = None if text is None else VC_HEADING.search(text)
    if heading is None:
        return None
    for line in text[heading.end():].split("\n")[1:]:
        if NEXT_HEADING.match(line):
            break
        if not line.startswith(MAIN_SLOT):
            continue
        value = line[len(MAIN_SLOT):].strip()
        span = SPAN.search(value)
        name = span.group(1).strip() if span else ""
        if value.casefold().startswith(UNBOUND_VALUES) or name.casefold() in ("", *UNBOUND_VALUES):
            return None
        return name
    return None


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


def trees_roots(lines):
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
            key = normalise(os.path.normpath("\\".join(kept) + "\\"))
            if not re.fullmatch(r"[a-z]:", key) and key not in roots:
                roots.append(key)
        break
    return roots


def location_roots(root):
    text = binding_text(root)
    lines = None if text is None else trees_section(text)
    return [] if lines is None else trees_roots(lines)


def bound_hub(lines):
    line = next((line for line in lines if line.startswith("- Hub:")), None)
    match = HUB_CHECKOUT.search(line) if line else None
    if match is None or not ABSOLUTE.match(match.group(1)):
        return None
    path = os.path.normpath(match.group(1)).rstrip("\\/")
    return None if len(path) == 2 else path


def hub_root(root):
    text = binding_text(root)
    lines = None if text is None else trees_section(text)
    return None if lines is None else bound_hub(lines)


def released(path):
    probe = path.rstrip("\\/") + PROBE_SUFFIX
    try:
        os.rename(path, probe)
    except OSError:
        return False
    os.rename(probe, path)
    return True


def worktrees():
    try:
        result = bounded_run(["git", "-c", "core.longpaths=true", "worktree", "list",
                              "--porcelain"], stdin=subprocess.DEVNULL, timeout=LIST_TIMEOUT)
    except subprocess.TimeoutExpired:
        raise Refusal(f"git worktree list timed out after {LIST_TIMEOUT}s") from None
    output = result.stdout.decode("utf-8", "replace")
    trees = []
    for block in output.replace("\r\n", "\n").split("\n\n"):
        path = name = None
        for line in block.split("\n"):
            if line.startswith("worktree "):
                path = line[len("worktree "):]
            elif line.startswith("branch refs/heads/"):
                name = line[len("branch refs/heads/"):]
        if path is not None:
            trees.append((path, name))
    return trees


def is_listed(path):
    return any(normalise(tree) == normalise(path) for tree, _ in worktrees())


def holds(ref, branch, deadline=None):
    remaining = LIST_TIMEOUT if deadline is None else deadline - time.monotonic()
    if remaining <= 0:
        return False
    try:
        result = bounded_run(["git", "-c", "core.longpaths=true", "rev-list", "--count",
                              f"refs/heads/{ref}..refs/heads/{branch}"],
                             stdin=subprocess.DEVNULL, timeout=remaining)
    except subprocess.TimeoutExpired:
        return False
    return result.returncode == 0 and result.stdout.decode("utf-8", "replace").strip() == "0"


def holders(main, trees, path, branch, roots):
    found = [main]
    for tree, name in trees[1:]:
        key = normalise(tree)
        if (name is None or name == branch or key == normalise(path)
                or not any(key.startswith(root + "\\") for root in roots)):
            continue
        found.append(name)
    return found


def item_date(value):
    if not DATE.fullmatch(value):
        return None
    try:
        return datetime.date.fromisoformat(value)
    except ValueError:
        return None


def valid_slug(value):
    return ITEM_SLUG.fullmatch(value) is not None and len(value) <= ITEM_SLUG_MAX


def halt_name(branch):
    return branch.replace("/", "~")


def halt_branch(name):
    return name.replace("~", "/")


def parse_halt(text, stem=None):
    if not text.startswith("---\n"):
        raise ValueError("no opening ---")
    lines = text.split("\n")
    if "---" not in lines[1:]:
        raise ValueError("no closing ---")
    end = lines.index("---", 1)
    pairs = []
    for number, line in enumerate(lines[1:end], start=2):
        match = ITEM_LINE.fullmatch(line)
        if match is None:
            raise ValueError(f"line {number} is not key: value")
        pairs.append(match.groups())
    for index, (key, _value) in enumerate(pairs):
        expected = HALT_KEYS[index] if index < len(HALT_KEYS) else "unit"
        if key != expected:
            raise ValueError(f"line {index + 2} is {key}, not {expected}")
    fields = dict(pairs[:len(HALT_KEYS)])
    for key in HALT_KEYS:
        if key not in fields:
            raise ValueError(f"missing {key}")
    valid = {"branch": lambda value: HALT_BRANCH.fullmatch(value) is not None,
             "head": lambda value: HALT_HEAD.fullmatch(value) is not None,
             "date": lambda value: item_date(value) is not None,
             "stage": lambda value: value.strip() != "",
             "item": lambda value: value == "none" or valid_slug(value),
             "fate": lambda value: value in ("open", "declined", "none"),
             "writer": lambda value: value in ("branch", "hub")}
    for key in HALT_KEYS:
        if not valid[key](fields[key]):
            raise ValueError(f"bad {key}: {fields[key]}")
    if (fields["item"] == "none") != (fields["fate"] == "none"):
        raise ValueError(f"bad fate: {fields['fate']} for item {fields['item']}")
    branch = fields["branch"]
    units = []
    for _key, value in pairs[len(HALT_KEYS):]:
        parts = value.split(" ")
        if (len(parts) != 2 or HALT_BRANCH.fullmatch(parts[0]) is None
                or HALT_HEAD.fullmatch(parts[1]) is None):
            raise ValueError(f"bad unit: {value}")
        unit = parts[0]
        if unit == branch:
            raise ValueError(f"unit {unit} is the halted branch")
        if not unit.startswith(branch):
            raise ValueError(f"unit {unit} does not begin with {branch}")
        if unit in (name for name, _head in units):
            raise ValueError(f"duplicate unit {unit}")
        units.append((unit, parts[1]))
    words = "\n".join(lines[end + 1:])
    if not words.strip():
        raise ValueError("blank halt words")
    if stem is not None and halt_name(branch) != stem:
        raise ValueError(f"branch {branch} is not the file name {stem}")
    return {**fields, "units": units, "words": words}


def halt_for(root, branch):
    try:
        entries = sorted((Path(root) / HALTS_DIR).iterdir(), key=lambda path: path.name)
    except FileNotFoundError:
        return None
    except OSError as error:
        raise Refusal(f"cannot read memory/halts: {error}") from None
    own, found = None, None
    for path in entries:
        if not path.name.endswith(".md"):
            continue
        stem = path.name[:-3]
        if not branch.startswith(halt_branch(stem)):
            continue
        rel = f"{HALTS_DIR}/{path.name}"
        record, why = None, "not a file"
        if path.is_file():
            try:
                record, why = parse_halt(read_lf(path), stem), None
            except (OSError, UnicodeDecodeError) as error:
                why = f"cannot read: {error}"
            except ValueError as error:
                why = str(error)
        if record is None:
            if halt_branch(stem) == branch:
                own = (rel, None, why)
        elif found is None and (record["branch"] == branch
                                or branch in (unit for unit, _head in record["units"])):
            found = (rel, record, None)
    return own or found


def branch_tip(branch):
    try:
        result = bounded_run(["git", "-c", "core.longpaths=true", "rev-parse", "--verify",
                              f"refs/heads/{branch}^{{commit}}"],
                             stdin=subprocess.DEVNULL, timeout=LIST_TIMEOUT)
    except FileNotFoundError:
        raise Refusal(f"tip {branch}: git not found") from None
    except subprocess.TimeoutExpired:
        raise Refusal(f"tip {branch}: git rev-parse timed out after {LIST_TIMEOUT}s") from None
    out = result.stdout.decode("utf-8", "replace")
    if result.returncode:
        raise Refusal(f"tip {branch}: " + first_line(result.stderr.decode("utf-8", "replace"), out))
    return out.strip()


def halt_pass(hub, branch, tip):
    try:
        found = halt_for(Path(hub), branch)
    except Refusal:
        return None, ""
    if found is None:
        return None, ""
    rel, record, why = found
    if record is None:
        return None, f"; the halt record {rel} is unreadable: {why}"
    recorded = (record["head"] if record["branch"] == branch
                else next(head for unit, head in record["units"] if unit == branch))
    try:
        current = tip(branch)
    except Refusal as refusal:
        return None, (f"; the halt record {rel} names {branch} at {recorded}; its head was not "
                      f"read: {refusal}")
    if current == recorded:
        return current, ""
    return None, f"; the halt record {rel} names {branch} at {recorded}, its head is {current}"


def main(argv):
    parser = argparse.ArgumentParser(prog="tree-remove.py")
    parser.add_argument("path")
    parser.add_argument("--wait", type=int, default=30)
    args = parser.parse_args(argv[1:])
    path = os.path.abspath(args.path)
    if not os.path.exists(path):
        print(f"absent: {path}")
        return 1
    try:
        listed = is_listed(path)
    except Refusal as refusal:
        print(f"refused: {refusal}")
        return 1
    if not listed:
        print(f"refused: {path} is not a worktree")
        return 1
    root = os.getcwd()
    main_branch = bound_main(root)
    if main_branch is None:
        print("refused: ## vc in .claude/bindings/vc-git.md binds no Main branch")
        return 1
    try:
        trees = worktrees()
    except Refusal as refusal:
        print(f"refused: {refusal}")
        return 1
    branch = next((name for tree, name in trees if normalise(tree) == normalise(path)), None)
    if branch is None:
        print(f"refused: {path}: no branch is checked out")
        return 1
    if branch == main_branch:
        print(f"refused: {path} is on the main branch {main_branch}")
        return 1
    candidates = holders(main_branch, trees, path, branch, location_roots(root))
    read_by = time.monotonic() + LIST_TIMEOUT
    if not any(holds(holder, branch, read_by) for holder in candidates):
        hub = hub_root(root)
        head, suffix = (None, "") if hub is None else halt_pass(hub, branch, branch_tip)
        if head is None:
            print(f"refused: {path}: {branch} is not merged; neither {main_branch} nor any one "
                  f"other tree's branch holds all of it{suffix}")
            return 1
    try:
        sessions = list_sessions(Path(root))
    except Refusal as refusal:
        print(f"refused: {refusal}")
        return 1
    if sessions is None:
        print("sessions: not read")
    else:
        found = at_tree(sessions, path)
        if found:
            print(f"refused: {path} is in use by a listed session")
            for entry in found:
                print(session_line(entry))
            return 1
    deadline = time.monotonic() + args.wait
    while not released(path):
        if time.monotonic() >= deadline:
            print(f"held: {path} is still in use after {args.wait} s")
            return 2
        time.sleep(1)
    removal = subprocess.run(["git", "-c", "core.longpaths=true", "worktree", "remove", path],
                             stdin=subprocess.DEVNULL, capture_output=True)
    if removal.returncode != 0:
        sys.stdout.flush()
        sys.stdout.buffer.write(removal.stderr)
        sys.stdout.buffer.flush()
    try:
        listed = is_listed(path)
    except Refusal as refusal:
        print(f"refused: {refusal}")
        return 1
    if listed:
        print(f"refused: {path} is still listed")
        return 1
    if os.path.isdir(path):
        print(f"half-removed: {path} is unlisted but its directory remains")
        return 3
    print(f"removed: {path}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
