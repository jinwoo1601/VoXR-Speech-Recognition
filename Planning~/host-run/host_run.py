# ============================================================================
# Purpose:  Commands 1-2 bracket: host lock, repoint, Unity test runs, restore
# Layer:    Planning~.host-run
# Owns:     (script, no public types)
# Depends:  (none)
# Hosts:    HUB, TG and UNITY as .claude/bindings/unity.md Command 1 (:6) names them
# Wait:     WAIT_DEFAULT 1800 s, provisional → Phase 4
# ============================================================================
import json
import msvcrt
import os
import subprocess
import sys
import time
import xml.etree.ElementTree as ElementTree
from datetime import datetime, timezone

HUB = "D:/Workspace/VoXR-Speech-Recognition"
TG = "D:/Workspace/VoXR TestGround"
UNITY = "C:/Program Files/Unity/Hub/Editor/6000.4.7f1/Editor/Unity.exe"
WAIT_DEFAULT = 1800
POLL_SECONDS = 2
PACKAGE = "com.jinwoo1601.voxr"
PLATFORMS = ("EditMode", "PlayMode")
PACKAGE_FILES = ("manifest.json", "packages-lock.json")
COUNT_KEYS = ("total", "passed", "failed", "inconclusive")

TREE = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))).replace("\\", "/")
HL = f"{HUB}/.scratch/host-lock"
REST = f"{HL}/rest"
OWNER = f"{HL}/owner"
RES = f"{TREE}/.scratch/test-results"
TESTS_TILDE = f"{TREE}/Tests~"
TESTS = f"{TREE}/Tests"
TESTS_META = f"{TREE}/Tests.meta"
MANIFEST = f"{TG}/Packages/manifest.json"
PACKAGES_LOCK = f"{TG}/Packages/packages-lock.json"
UNITY_LOCKFILE = f"{TG}/Temp/UnityLockfile"


def out(line):
    print(line, flush=True)


def stop(what):
    out(f"stop: {what}")
    return 3


def same_path(a, b):
    return os.path.normcase(os.path.normpath(a)) == os.path.normcase(os.path.normpath(b))


def is_file_path(value, path):
    return isinstance(value, str) and value.startswith("file:") and same_path(value[len("file:"):], path)


def read_bytes(path):
    with open(path, "rb") as f:
        return f.read()


def write_bytes(path, data):
    with open(path, "wb") as f:
        f.write(data)


def parse_json(data):
    try:
        return json.loads(data)
    except ValueError:
        return None


def dig(value, *keys):
    for key in keys:
        if not isinstance(value, dict):
            return None
        value = value.get(key)
    return value


def git(*args):
    try:
        done = subprocess.run(["git", "-C", TG, *args], capture_output=True)
    except OSError:
        return None, b""
    return done.returncode, done.stdout


def parse_args(argv):
    platforms = []
    options = {}
    index = 0
    while index < len(argv):
        arg = argv[index]
        if arg in PLATFORMS and arg not in platforms:
            platforms.append(arg)
        elif arg in ("--filter", "--wait") and arg not in options and index + 1 < len(argv):
            index += 1
            options[arg] = argv[index]
        else:
            return None, f"bad argument: {arg}"
        index += 1
    if not platforms:
        return None, "bad argument: no platform named (EditMode, PlayMode)"
    target = options.get("--filter")
    if target == "":
        return None, "bad argument: --filter empty"
    wait = options.get("--wait", str(WAIT_DEFAULT))
    if not (wait.isascii() and wait.isdigit()):
        return None, f"bad argument: --wait {wait}"
    return (platforms, target, int(wait)), None


def tree_ready():
    found = []
    cwd = os.getcwd().replace("\\", "/")
    if not same_path(TREE, cwd):
        found.append(f"tree {TREE} is not the working directory {cwd}")
    if not os.path.isdir(TESTS_TILDE):
        found.append(f"{TESTS_TILDE} absent")
    for path in (TESTS, TESTS_META):
        if os.path.lexists(path):
            found.append(f"{path} present")
    return found


def acquire(wait):
    os.makedirs(HL, exist_ok=True)
    fd = os.open(f"{HL}/lock", os.O_RDWR | os.O_CREAT | os.O_BINARY)
    start = time.monotonic()
    deadline = start + wait
    while True:
        try:
            os.lseek(fd, 0, os.SEEK_SET)
            msvcrt.locking(fd, msvcrt.LK_NBLCK, 1)
            return fd, round(time.monotonic() - start)
        except OSError:
            now = time.monotonic()
            if now >= deadline:
                os.close(fd)
                return None, round(now - start)
            time.sleep(min(POLL_SECONDS, deadline - now))


def release(fd):
    os.lseek(fd, 0, os.SEEK_SET)
    msvcrt.locking(fd, msvcrt.LK_UNLCK, 1)
    os.close(fd)


def write_owner(platforms):
    start = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    with open(OWNER, "w", encoding="utf-8", newline="\n") as f:
        f.write(f"tree={TREE} platforms={','.join(platforms)} pid={os.getpid()} start={start}\n")


def owner_text():
    try:
        with open(OWNER, encoding="utf-8", errors="replace") as f:
            return f.read().strip()
    except OSError:
        return "absent"


def at_rest():
    files = {}
    found = []
    for name in PACKAGE_FILES:
        try:
            files[name] = read_bytes(f"{TG}/Packages/{name}")
        except OSError as error:
            found.append(f"Packages/{name} unreadable ({error})")
    if found:
        return files, found
    manifest = parse_json(files["manifest.json"])
    packages_lock = parse_json(files["packages-lock.json"])
    code, head_bytes = git("show", "HEAD:Packages/manifest.json")
    head = parse_json(head_bytes) if code == 0 else None
    value = dig(manifest, "dependencies", PACKAGE)
    if not is_file_path(value, HUB):
        found.append(f"manifest.json {PACKAGE} is {value!r}, not file:{HUB}")
    if head is None:
        found.append("git show HEAD:Packages/manifest.json unreadable")
    elif dig(manifest, "testables") != dig(head, "testables"):
        found.append("manifest.json testables differ from HEAD's")
    version = dig(packages_lock, "dependencies", PACKAGE, "version")
    if not is_file_path(version, HUB):
        found.append(f"packages-lock.json {PACKAGE} version is {version!r}, not file:{HUB}")
    if os.path.lexists(UNITY_LOCKFILE):
        found.append(f"{UNITY_LOCKFILE} present")
    return files, found


def write_snapshots(files):
    os.makedirs(REST, exist_ok=True)
    for name, data in files.items():
        write_bytes(f"{REST}/{name}", data)


def host_status():
    code, output = git("status", "--short")
    if code != 0:
        return None
    return output.decode("utf-8", errors="replace").splitlines()


def status_line(baseline, after):
    if after is None:
        return "host status: changed — git status --short failed"
    changes = [f"-{line}" for line in baseline if line not in after]
    changes += [f"+{line}" for line in after if line not in baseline]
    if not changes:
        return "host status: unchanged"
    return f"host status: changed — {'; '.join(changes)}"


def listing(top):
    paths = set()
    for dirpath, dirnames, filenames in os.walk(top):
        for name in dirnames + filenames:
            paths.add(os.path.relpath(os.path.join(dirpath, name), top))
    return paths


def unity_argv(platform, filter):
    name = platform.lower()
    argv = [
        UNITY,
        "-runTests",
        "-batchmode",
        "-projectPath",
        TG,
        "-testPlatform",
        platform,
        "-testResults",
        f"{RES}/{name}.xml",
        "-logFile",
        f"{RES}/{name}.log",
    ]
    if filter is not None:
        argv += ["-testFilter", filter]
    return argv


def read_counts(path):
    try:
        root = ElementTree.parse(path).getroot()
        if root.tag != "test-run":
            return None
        return tuple(int(root.get(key)) for key in COUNT_KEYS)
    except (OSError, ElementTree.ParseError, TypeError, ValueError):
        return None


def resolved_version():
    try:
        packages_lock = parse_json(read_bytes(PACKAGES_LOCK))
    except OSError:
        return None
    version = dig(packages_lock, "dependencies", PACKAGE, "version")
    return version if isinstance(version, str) else None


def not_green_reason(code, counts, version):
    if code != 0:
        return f"exit {code}"
    if counts is None:
        return "no results XML written by this run"
    total, _, failed, inconclusive = counts
    if total == 0:
        return "total 0"
    if failed:
        return f"failed {failed}"
    if inconclusive:
        return f"inconclusive {inconclusive}"
    if not is_file_path(version, TREE):
        return f"resolved {version if version is not None else '-'}"
    return None


def run_platform(platform, target):
    name = platform.lower()
    results = f"{RES}/{name}.xml"
    os.makedirs(RES, exist_ok=True)
    try:
        os.remove(results)
    except OSError:
        pass
    deleted = not os.path.lexists(results)
    start = time.monotonic()
    code = subprocess.run(
        unity_argv(platform, target),
        stdin=subprocess.DEVNULL,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    ).returncode
    wall = round(time.monotonic() - start)
    counts = read_counts(results) if deleted else None
    version = resolved_version()
    shown = "/".join(str(n) for n in counts) if counts is not None else "-/-/-/-"
    out(f"{platform}: {shown} exit={code} wall={wall}s")
    reason = not_green_reason(code, counts, version)
    if reason is not None:
        out(f"{platform}: not green — {reason}")
    out(f"resolved: {version if version is not None else '-'} (packages-lock.json)")
    return reason is None


def restore_tree(renamed, before):
    found = []
    kept = []
    if not renamed:
        return found, kept
    try:
        os.rename(TESTS, TESTS_TILDE)
    except OSError as error:
        return [f"{TESTS} not renamed back to Tests~ ({error})"], kept
    for path in sorted(listing(TESTS_TILDE) - before):
        full = f"{TESTS_TILDE}/{path.replace(os.sep, '/')}"
        if path.endswith(".meta") and os.path.isfile(full):
            try:
                os.remove(full)
            except OSError as error:
                found.append(f"{full} not deleted ({error})")
        else:
            kept.append(full)
    if os.path.lexists(TESTS_META):
        try:
            os.remove(TESTS_META)
        except OSError as error:
            found.append(f"{TESTS_META} not deleted ({error})")
    return found, kept


def restore_host(files):
    found = []
    for name, data in files.items():
        path = f"{TG}/Packages/{name}"
        try:
            write_bytes(path, data)
            if read_bytes(path) != data:
                found.append(f"Packages/{name} not byte-identical to its snapshot")
        except OSError as error:
            found.append(f"Packages/{name} not written back ({error})")
    return found


def bracket(platforms, target, fd):
    files, found = at_rest()
    if found:
        release(fd)
        return stop(f"host not at rest: {'; '.join(found)}")
    write_snapshots(files)
    baseline = host_status()
    if baseline is None:
        release(fd)
        return stop(f"git status --short failed in {TG}")
    old = f'"{PACKAGE}": "file:{HUB}"'
    new = f'"{PACKAGE}": "file:{TREE}"'
    if '"' in TREE or "\\" in TREE:
        release(fd)
        return stop(f"tree path {TREE} holds a quote or backslash")
    count = files["manifest.json"].count(old.encode())
    if count != 1:
        release(fd)
        return stop(f"manifest.json holds {count} occurrences of {old}, not 1")
    before = listing(TESTS_TILDE)
    results = []
    renamed = False
    try:
        if not same_path(TREE, HUB):
            write_bytes(MANIFEST, files["manifest.json"].replace(old.encode(), new.encode()))
        os.rename(TESTS_TILDE, TESTS)
        renamed = True
        for platform in platforms:
            results.append(run_platform(platform, target))
    finally:
        found, kept = restore_tree(renamed, before)
        found += restore_host(files)
        found += at_rest()[1]
        status = status_line(baseline, host_status())
        release(fd)
        if found:
            out(f"stop: {'; '.join(found)} — pre-run bytes in {REST}/")
        else:
            out("host: at rest")
        out(status)
        for path in kept:
            out(f"tree: kept new path {path}")
        green = not found and len(results) == len(platforms) and all(results)
        out(f"result: {'green' if green else 'not green'}")
        if found:
            sys.exit(3)
    return 0 if green else 1


def main(argv):
    sys.stdout.reconfigure(encoding="utf-8")
    args, error = parse_args(argv)
    if error is not None:
        return stop(error)
    platforms, target, wait = args
    found = tree_ready()
    if found:
        return stop("; ".join(found))
    fd, waited = acquire(wait)
    if fd is None:
        out(f"BLOCKED: host lock held past {wait}s — owner: {owner_text()}")
        return 2
    out(f"lock wait: {waited}s")
    write_owner(platforms)
    return bracket(platforms, target, fd)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
