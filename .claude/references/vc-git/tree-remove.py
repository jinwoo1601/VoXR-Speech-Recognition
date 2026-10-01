# =============================================================================
# Purpose:  Tree removal per the trees binding's Remove procedure, read back
# Layer:    Scripts
# Owns:     (script, no public types)
# Depends:  (none)
# =============================================================================
"""`python3 .claude/references/vc-git/tree-remove.py "<path>" [--wait <seconds>]`,
run from the hub or any tree of the repository: makes the path absolute, waits for
the tree's directory to be released, removes the tree by
`git worktree remove "<path>"`, never forced, and reads back.

Standard library only; git is run by argument list, never by a shell string.
Exactly one verdict line is printed, after git's own refusal where there is one:

- `removed: <path>` - unlisted, directory gone; exit 0
- `absent: <path>` - no such path, nothing run; exit 1
- `refused: <path> is not a worktree` - not listed, nothing done; exit 1
- `refused: <path> is still listed` - exit 1
- `held: <path> is still in use after <n> s` - nothing removed; exit 2
- `half-removed: <path> is unlisted but its directory remains` - exit 3

A leftover directory is reported, never deleted.
"""

import argparse
import os
import subprocess
import sys
import time

PROBE_SUFFIX = ".release-probe"


def normalise(path):
    """`path` as compared: `\\` separators, no trailing separator, casefolded."""
    return path.replace("/", "\\").rstrip("\\").casefold()


def released(path):
    """True once the directory can be renamed to its probe sibling and straight back;
    False while a rename is refused, which means something still holds it.
    """
    probe = path.rstrip("\\/") + PROBE_SUFFIX
    try:
        os.rename(path, probe)
    except OSError:
        return False
    os.rename(probe, path)
    return True


def is_listed(path):
    """Whether a `worktree <path>` line of `git worktree list --porcelain` names `path`."""
    listed = subprocess.run(["git", "worktree", "list", "--porcelain"],
                            stdin=subprocess.DEVNULL, capture_output=True,
                            encoding="utf-8", errors="replace").stdout
    wanted = normalise(path)
    return any(line.startswith("worktree ") and normalise(line[len("worktree "):]) == wanted
               for line in listed.splitlines())


def main(argv):
    parser = argparse.ArgumentParser(prog="tree-remove.py")
    parser.add_argument("path")
    parser.add_argument("--wait", type=int, default=30)
    args = parser.parse_args(argv[1:])
    path = os.path.abspath(args.path)
    if not os.path.exists(path):
        print(f"absent: {path}")
        return 1
    if not is_listed(path):
        print(f"refused: {path} is not a worktree")
        return 1
    deadline = time.monotonic() + args.wait
    while not released(path):
        if time.monotonic() >= deadline:
            print(f"held: {path} is still in use after {args.wait} s")
            return 2
        time.sleep(1)
    removal = subprocess.run(["git", "worktree", "remove", path],
                             stdin=subprocess.DEVNULL, capture_output=True)
    if removal.returncode != 0:
        sys.stdout.flush()
        sys.stdout.buffer.write(removal.stderr)
        sys.stdout.buffer.flush()
    if is_listed(path):
        print(f"refused: {path} is still listed")
        return 1
    if os.path.isdir(path):
        print(f"half-removed: {path} is unlisted but its directory remains")
        return 3
    print(f"removed: {path}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
