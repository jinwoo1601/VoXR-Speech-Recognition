# =============================================================================
# Purpose:  Taking a ruled side on one path a stopped catch-up merge lists, past its checks, per the trees binding's Catch-up procedure
# Layer:    Scripts
# Owns:     Refusal (exception class)
# Depends:  (none)
# =============================================================================

import argparse
import ntpath
import os
import re
import subprocess
import sys

ATTRS_FILE = ".claude/references/vc-git/merge-attributes.txt"
BINDINGS_REL = os.path.join(".claude", "bindings", "vc-git.md")
DRIVE_ABSOLUTE = re.compile(r"[A-Za-z]:[\\/]")
DRIVE_FORM = re.compile(r"^/([A-Za-z])(?=/|$)")
HUB_CHECKOUT = re.compile(r"the main checkout is `([^`]+)`")
TREE_OID = re.compile(r"[0-9a-f]{40}|[0-9a-f]{64}")
SIDE_STAGE = {"ours": 2, "theirs": 3}
LINK_MODES = ("120000", "160000")
QUIET = ("unspecified", "unset")
MERGE_OK = ("set", "unset", "unspecified", "text", "binary", "union")
MARKS = (b"<<<<<<<", b"|||||||", b">>>>>>>")
SNIFF = 8000


class Refusal(Exception):
    def __init__(self, word, detail):
        super().__init__(f"{word}: {detail}")
        self.word = word
        self.detail = detail


def verb_of(args):
    index = 0
    while args[index].startswith("-"):
        index += 2 if args[index] == "-c" else 1
    return args[index]


def decoded(data, args):
    try:
        return data.decode("utf-8")
    except UnicodeDecodeError:
        raise Refusal("git", f"undecodable output of git {verb_of(args)}") from None


def git(*args, ok=(0,)):
    try:
        result = subprocess.run(["git", *args], stdin=subprocess.DEVNULL, capture_output=True)
    except OSError as error:
        raise Refusal("git", f"git not runnable: {error}") from None
    if ok is not None and result.returncode not in ok:
        lines = [line.strip() for line in decoded(result.stderr, args).splitlines()]
        first = next((line for line in lines if line), "no output")
        raise Refusal("git", f"git {verb_of(args)} exited {result.returncode}: {first}")
    return result


def read(*args):
    return decoded(git(*args).stdout, args)


def normalise_path(path):
    path = DRIVE_FORM.sub(r"\1:", path)
    return path.replace("/", "\\").rstrip("\\").casefold()


def drive_path(text):
    if not DRIVE_ABSOLUTE.match(text):
        return None
    path = ntpath.normpath(text).replace("\\", "/").rstrip("/")
    return None if len(path) == 2 else path


def binding_section(text, key):
    heading = re.compile(r"^#{2,}\s*" + re.escape(key) + r"\b", re.IGNORECASE)
    body = None
    for line in text.splitlines():
        if body is not None and re.match(r"^#{1,6}\s", line):
            break
        if body is not None:
            body.append(line)
        elif heading.match(line):
            body = []
    return body


def section_content(lines):
    stripped = [line.strip() for line in lines]
    return [line for line in stripped
            if line and not (line.startswith("<!--") and line.endswith("-->"))]


def none_answer(content):
    return len(content) == 1 and re.match(
        r"none(\s|—|$)", content[0].removeprefix("- ").replace("`", "")) is not None


def trees_absent(lines):
    if lines is None:
        return True
    content = section_content(lines)
    if not content:
        return True
    if any("TODO" in line for line in lines):
        return True
    return none_answer(content)


def trees_hub(lines):
    line = next((line for line in lines if line.startswith("- Hub:")), None)
    match = HUB_CHECKOUT.search(line) if line else None
    return match.group(1) if match else None


def norm(data):
    out = []
    for line in data.replace(b"\r\n", b"\n").split(b"\n"):
        if line[:7] in MARKS and (len(line) == 7 or line[7:8] == b" "):
            line = line[:7]
        out.append(line)
    return b"\n".join(out)


def check_tree():
    top = read("rev-parse", "--show-toplevel").rstrip("\r\n")
    cwd = os.getcwd()
    if normalise_path(cwd) != normalise_path(top):
        raise Refusal("tree", f"{cwd} is not the tree's top level {top}")
    return top


def check_hub(top):
    try:
        with open(os.path.join(top, BINDINGS_REL), encoding="utf-8") as handle:
            text = handle.read()
    except (OSError, ValueError):
        raise Refusal("hub", f"{BINDINGS_REL} is not read") from None
    lines = binding_section(text, "trees")
    raw = None if trees_absent(lines) else trees_hub(lines)
    if raw is None:
        raise Refusal("hub", f"## trees in {BINDINGS_REL} binds no Hub")
    hub = drive_path(raw)
    if hub is None:
        raise Refusal("hub", f"the Hub value {raw} is not a drive-absolute path below a drive "
                             "root")
    if normalise_path(hub) == normalise_path(top):
        raise Refusal("hub", f"{top} is the hub, the main checkout")


def check_merge():
    head_file = read("rev-parse", "--path-format=absolute", "--git-path",
                     "MERGE_HEAD").rstrip("\r\n")
    try:
        with open(head_file, "rb") as handle:
            heads = [line for line in handle.read().splitlines() if line.strip()]
    except OSError:
        raise Refusal("merge", "no merge is in progress") from None
    if len(heads) != 1:
        raise Refusal("merge", f"MERGE_HEAD names {len(heads)} commits, not one")
    stages = {}
    for record in read("ls-files", "-u", "-z").split("\0"):
        if record:
            meta, path = record.split("\t", 1)
            mode, oid, stage = meta.split(" ")
            stages.setdefault(path, {})[int(stage)] = (mode, oid)
    if not stages:
        raise Refusal("merge", "the merge in progress has no unmerged path")
    return stages


def check_path(arg):
    p = arg.replace("\\", "/")
    listed = [name for name in read("diff", "--name-only", "--diff-filter=U", "-z").split("\0")
              if name]
    if p not in listed:
        raise Refusal("path", f"{p} is not a path the merge lists unmerged")
    return p


def check_side(side, p, entries):
    if side == "ours" and p.casefold().startswith(".claude/") and not p.startswith(
            ".claude/bindings/"):
        raise Refusal("side", f"--ours on {p}: a .claude/ path outside .claude/bindings/ "
                              "takes theirs")
    if SIDE_STAGE[side] not in entries:
        raise Refusal("side", f"{p} has no {side} side, stage {SIDE_STAGE[side]}")


def attributes(p):
    parts = read("-c", f"core.attributesFile={ATTRS_FILE}", "check-attr", "-z", "merge",
                 "filter", "working-tree-encoding", "diff", "--", p).split("\0")
    return {parts[index + 1]: parts[index + 2] for index in range(0, len(parts) - 2, 3)}


def blob(oid):
    return git("cat-file", "blob", oid).stdout


def renamed_into(p):
    base = read("merge-base", "HEAD", "MERGE_HEAD").strip()
    for rev in ("HEAD", "MERGE_HEAD"):
        args = ("diff", "--no-ext-diff", "-M", "--name-status", "-z", base, rev)
        result = git(*args)
        warned = [line.strip() for line in decoded(result.stderr, args).splitlines()
                  if line.strip()]
        if warned:
            raise Refusal("kind", f"git diff warned on its rename read from the merge base: "
                                  f"{'; '.join(warned)}")
        records = decoded(result.stdout, args).split("\0")
        index = 0
        while index < len(records) - 1:
            if records[index].startswith("R"):
                if records[index + 2] == p:
                    return True
                index += 3
            else:
                index += 2
    return False


def check_kind(p, entries):
    if any(mode in LINK_MODES for mode, _oid in entries.values()):
        raise Refusal("kind", f"{p} has a symlink or submodule stage")
    attrs = attributes(p)
    if attrs.get("filter") not in QUIET or attrs.get("working-tree-encoding") not in QUIET:
        raise Refusal("kind", f"{p} has a filter or working-tree-encoding attribute")
    if attrs.get("merge") not in MERGE_OK:
        raise Refusal("kind", f"{p} has the merge driver {attrs.get('merge')}")
    if attrs.get("diff") == "unset" or any(b"\0" in blob(oid)[:SNIFF]
                                           for _mode, oid in entries.values()):
        raise Refusal("kind", f"{p} has binary content")
    if len(entries) == 1:
        raise Refusal("kind", f"{p} has one stage only")
    if set(entries) == {2, 3} and renamed_into(p):
        raise Refusal("kind", f"{p} is a path a side renamed into")


def check_content(top, p):
    args = ("-c", f"core.attributesFile={ATTRS_FILE}", "merge-tree", "--write-tree", "HEAD",
            "MERGE_HEAD")
    result = git(*args, ok=None)
    tree = decoded(result.stdout.split(b"\n", 1)[0], args).strip()
    if not TREE_OID.fullmatch(tree):
        raise Refusal("kind", f"git merge-tree gave no tree for HEAD and MERGE_HEAD, exit "
                              f"{result.returncode}")
    entries = [entry for entry in read("--literal-pathspecs", "ls-tree", "-z", tree, "--",
                                       p).split("\0") if entry]
    found = [entry.split("\t", 1) for entry in entries]
    if len(found) != 1 or found[0][1] != p or found[0][0].split(" ")[1] != "blob":
        raise Refusal("kind", f"the recomputed merge holds no one blob at {p}")
    stored = blob(found[0][0].split(" ")[2])
    try:
        with open(os.path.join(top, p), "rb") as handle:
            work = handle.read()
    except OSError:
        raise Refusal("content", f"{p} is not read in the work tree") from None
    if norm(work) != norm(stored):
        raise Refusal("content", f"{p} no longer matches what the stopped merge left")


def take(side, arg):
    top = check_tree()
    check_hub(top)
    stages = check_merge()
    p = check_path(arg)
    check_side(side, p, stages[p])
    check_kind(p, stages[p])
    check_content(top, p)
    git("--literal-pathspecs", "checkout", f"--{side}", "--", p)
    return p


def main(argv):
    sys.stdout.reconfigure(errors="backslashreplace")
    sys.stderr.reconfigure(errors="backslashreplace")
    parser = argparse.ArgumentParser(prog="conflict-side.py")
    sides = parser.add_mutually_exclusive_group(required=True)
    sides.add_argument("--ours", action="store_const", const="ours", dest="side")
    sides.add_argument("--theirs", action="store_const", const="theirs", dest="side")
    parser.add_argument("path")
    args = parser.parse_args(argv[1:])
    try:
        p = take(args.side, args.path)
    except Refusal as refusal:
        print(f"refused: {refusal.word}: {refusal.detail}")
        return 1
    print(f"taken: --{args.side} {p}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
