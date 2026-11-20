# =============================================================================
# Purpose:  SessionStart hook naming a newer release tag for a base that is a release clone
# Layer:    Hooks
# Owns:     (script, no public types)
# Depends:  (none)
# =============================================================================

import datetime
import json
import os
import re
import subprocess
import sys
import tempfile
from pathlib import Path

MANIFEST_REL = ".claude/harness.json"
RELEASE_TAG = re.compile(r"v(\d+)\.(\d+)\.(\d+)")
RELEASE_WAIT_SECONDS = 5
GIT_WAIT_SECONDS = 5
RELEASE_CACHE = "harness-release-check.json"


def resolve_root(argv):
    if len(argv) > 1:
        candidate = Path(argv[1])
        if candidate.is_dir():
            return candidate
    return Path.cwd()


def manifest_base(root):
    manifest = root / MANIFEST_REL
    if not manifest.is_file():
        return None
    document = json.loads(manifest.read_text(encoding="utf-8"))
    base = document.get("base") if isinstance(document, dict) else None
    if not isinstance(base, str) or not base:
        return None
    base_dir = (root / base).resolve()
    return base_dir if base_dir.is_dir() else None


def release_key(tag):
    match = RELEASE_TAG.fullmatch(tag) if isinstance(tag, str) else None
    return tuple(int(group) for group in match.groups()) if match else None


def newest_release(tags):
    found = [tag for tag in tags if release_key(tag) is not None]
    return max(found, key=release_key) if found else None


def git_lines(base, *args):
    with tempfile.TemporaryFile() as out:
        result = subprocess.run(["git", "-C", str(base), *args], stdin=subprocess.DEVNULL,
                                stdout=out, stderr=subprocess.DEVNULL, timeout=GIT_WAIT_SECONDS)
        out.seek(0)
        stdout = out.read()
    if result.returncode != 0:
        return None
    return stdout.decode("utf-8").splitlines()


def same_path(path):
    return os.path.normcase(str(Path(path).resolve()))


def remote_release(base):
    env = {**os.environ, "GIT_TERMINAL_PROMPT": "0", "GCM_INTERACTIVE": "never"}
    try:
        with tempfile.TemporaryFile() as out:
            result = subprocess.run(["git", "-C", str(base), "-c", "credential.interactive=false",
                                     "ls-remote", "--tags", "--refs", "origin"],
                                    stdin=subprocess.DEVNULL, stdout=out,
                                    stderr=subprocess.DEVNULL, env=env,
                                    timeout=RELEASE_WAIT_SECONDS)
            out.seek(0)
            stdout = out.read()
    except (OSError, subprocess.SubprocessError):
        return None
    if result.returncode != 0:
        return None
    names = []
    for line in stdout.decode("utf-8", "replace").splitlines():
        ref = line.partition("\t")[2]
        if ref.startswith("refs/tags/"):
            names.append(ref[len("refs/tags/"):])
    return newest_release(names)


def cached_release(git_dir, base, today):
    path = Path(git_dir) / RELEASE_CACHE
    stamp = today.isoformat()
    try:
        document = json.loads(path.read_bytes())
    except (OSError, ValueError, RecursionError):
        document = None
    if isinstance(document, dict) and document.get("read") == stamp:
        return document.get("newest")
    newest = remote_release(base)
    try:
        path.write_bytes(json.dumps({"newest": newest, "read": stamp}).encode("utf-8") + b"\n")
    except OSError:
        pass
    return newest


def release_warning(root, base=None, today=None):
    try:
        if base is None:
            base = manifest_base(root)
            if base is None:
                return None
        if not (base / ".git").exists():
            return None
        lines = git_lines(base, "rev-parse", "--show-toplevel", "--absolute-git-dir",
                          "--symbolic-full-name", "HEAD")
        if lines is None or len(lines) != 3 or lines[2] != "HEAD":
            return None
        if same_path(lines[0]) != same_path(base):
            return None
        tags = git_lines(base, "tag", "--points-at", "HEAD")
        current = newest_release(tags) if tags is not None else None
        if current is None:
            return None
        newest = cached_release(lines[1], base,
                                datetime.date.today() if today is None else today)
        key = release_key(newest)
        if key is None or key <= release_key(current):
            return None
        return (f"warn  NEWER  {base}  the base is at release {current}, and {newest} is out: "
                f"in the base, git fetch --depth 1 origin tag {newest} and git checkout {newest}, "
                f"then harness.py upgrade in each project")
    except Exception:
        return None


def main(argv):
    try:
        line = release_warning(resolve_root(argv))
        if line is not None:
            sys.stdout.buffer.write((line + "\n").encode("utf-8"))
            sys.stdout.buffer.flush()
    except Exception:
        pass
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
