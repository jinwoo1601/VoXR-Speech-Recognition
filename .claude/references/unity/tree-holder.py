# =============================================================================
# Purpose:  Unity holder probe of a tree per tree-holder.md: unity status read by its error code and instances, printed as one held, not-held or unreadable line
# Layer:    Scripts
# Owns:     Unreadable (exception class)
# Depends:  (none)
# =============================================================================

import json
import os
import re
import shutil
import subprocess
import sys

PROBE_TIMEOUT = 30
KILL_TIMEOUT = 5
HELD = 2
NOT_HELD = 0
UNREADABLE = 1
AMBIGUOUS = "AMBIGUOUS_EDITOR"
NO_INSTANCES = "STATUS_NO_INSTANCES"
DRIVE_ABSOLUTE = re.compile(r"[A-Za-z]:\\[^\\]")
BREAKS = re.compile(r"[\r\n]+")
USAGE = 'usage: tree-holder.py "<tree>"'


class Unreadable(Exception):
    pass


def folded(text):
    return BREAKS.sub(" ", text).strip()


def said(out, err):
    parts = [part for part in (folded(out), folded(err)) if part]
    return " ".join(parts) if parts else "no output"


def stop(process):
    if os.name == "nt":
        try:
            subprocess.run(["taskkill", "/F", "/T", "/PID", str(process.pid)],
                           stdin=subprocess.DEVNULL, capture_output=True, timeout=KILL_TIMEOUT)
        except (OSError, subprocess.TimeoutExpired):
            pass
    try:
        process.kill()
    except OSError:
        pass
    try:
        process.communicate(timeout=KILL_TIMEOUT)
    except subprocess.TimeoutExpired:
        pass


def reading(code, out, err):
    try:
        envelope = json.loads(out)
    except ValueError:
        envelope = None
    if isinstance(envelope, dict):
        data = envelope.get("data")
        instances = data.get("instances") if isinstance(data, dict) else None
        errors = envelope.get("errors")
        codes = ([entry.get("code") for entry in errors if isinstance(entry, dict)]
                 if isinstance(errors, list) else [])
        if isinstance(instances, list) and instances:
            return HELD, "held: " + "; ".join(json.dumps(instance) for instance in instances)
        if AMBIGUOUS in codes:
            return HELD, "held: AMBIGUOUS_EDITOR"
        if set(codes) == {NO_INSTANCES}:
            return NOT_HELD, "not-held: STATUS_NO_INSTANCES"
    return UNREADABLE, f"unreadable: exit {code}: {said(out, err)}"


def probe(argv):
    try:
        if len(argv) != 1:
            raise Unreadable(f"{USAGE}, {len(argv)} arguments given")
        tree = argv[0].replace("/", "\\").rstrip("\\")
        if not DRIVE_ABSOLUTE.match(tree):
            raise Unreadable(f"not a drive-absolute path: {folded(argv[0])}")
        unity = shutil.which("unity")
        if unity is None:
            raise Unreadable("unity not found on PATH")
        try:
            process = subprocess.Popen([unity, "status", "--project-path", tree, "--json"],
                                       stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
                                       stderr=subprocess.PIPE)
        except OSError as error:
            raise Unreadable(folded(str(error)))
        try:
            out, err = process.communicate(timeout=PROBE_TIMEOUT)
        except subprocess.TimeoutExpired:
            stop(process)
            raise Unreadable(f"unity status timed out after {PROBE_TIMEOUT} s")
        return reading(process.returncode, out.decode("utf-8", "replace"),
                       err.decode("utf-8", "replace"))
    except Unreadable as error:
        return UNREADABLE, f"unreadable: exit none: {error}"
    except Exception as error:
        return UNREADABLE, f"unreadable: exit none: unexpected error: {type(error).__name__}: {folded(str(error))}"


def main(argv):
    code, line = probe(argv)
    sys.stdout.buffer.write((line + "\n").encode("utf-8", "replace"))
    sys.stdout.buffer.flush()
    return code


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
