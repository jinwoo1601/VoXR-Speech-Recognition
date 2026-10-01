# =============================================================================
# Purpose:  SessionStart hook relaying `harness.py doctor`'s report into a session
# Layer:    Hooks
# Owns:     (script, no public types)
# Depends:  (none)
# =============================================================================
"""Claude Code `SessionStart` hook: the project's `doctor` report, or silence.

Standard library only, and never an import of `harness.py` - the hook runs under
whatever interpreter the platform starts, and `harness.py` exits at import time
when PyYAML or jsonschema is missing. The child is started as `sys.executable`,
which is a real interpreter by construction, being the one already running here.

Nothing in this file fails a session: the only exit is 0. Where there is no
manifest, no base, no `harness.py`, or no time left, the hook says nothing at all.

Where doctor printed a report, the hub check follows: where the
`session-launch` binding names a List command, a `HUB` warn line goes before
doctor's final line when two or more running sessions carry the Hub session name,
or when that name is unbound. Any failure of the check is silence.
"""

import json
import re
import subprocess
import sys
from pathlib import Path

MANIFEST_REL = ".claude/harness.json"
BINDINGS_REL = ".claude/bindings"
WAIT_SECONDS = 10
LIST_WAIT_SECONDS = 5


def resolve_root(argv):
    """The project root: argv[1] when it names a directory, else the process cwd."""
    if len(argv) > 1:
        candidate = Path(argv[1])
        if candidate.is_dir():
            return candidate
    return Path.cwd()


def harness_script(root):
    """`<root>/<base>/scripts/harness.py`, or None when this is not a project.

    None is every step of the manifest probe: no manifest file (so `doctor`'s own
    MANIFEST failure row never reaches a session opened outside a project), a
    manifest that does not parse, no usable `base`, or no script where it points.
    """
    manifest = root / MANIFEST_REL
    if not manifest.is_file():
        return None
    try:
        document = json.loads(manifest.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return None
    base = document.get("base") if isinstance(document, dict) else None
    if not isinstance(base, str) or not base:
        return None
    script = root / base / "scripts/harness.py"
    return script if script.is_file() else None


def doctor_stdout(root, script):
    """The child's stdout as bytes, whatever it returned; b"" if it never spoke.

    stderr is captured and dropped, never inherited: a traceback from a broken
    base must not be injected into a session's context as if it were a report.
    The relay is byte-for-byte, so nothing here decodes what the child printed.
    """
    try:
        result = subprocess.run([sys.executable, str(script), "doctor", "--root", str(root)],
                                cwd=str(root), stdout=subprocess.PIPE,
                                stderr=subprocess.PIPE, timeout=WAIT_SECONDS)
    except (subprocess.TimeoutExpired, OSError):
        return b""
    return result.stdout


def session_launch_section(text):
    """The body lines of the `session-launch` section of a bindings file, or None:
    under the first heading of two or more `#` reading it, up to the next heading.
    """
    heading = re.compile(r"^#{2,}\s*session-launch\b", re.IGNORECASE)
    body = None
    for line in text.splitlines():
        if body is not None and re.match(r"^#{1,6}\s", line):
            break
        if body is not None:
            body.append(line)
        elif heading.match(line):
            body = []
    return body


def slot_value(lines, label):
    """The first backticked span on the `- <label>:` line; None where the slot is
    unbound - no such line, text after the colon starting `TODO` or `none`, or no span.
    """
    for line in lines:
        stripped = line.strip()
        if stripped.startswith(f"- {label}:"):
            text = stripped[len(f"- {label}:"):].strip()
            if text.lower().startswith(("todo", "none")):
                return None
            match = re.search(r"`([^`]+)`", text)
            return match.group(1) if match else None
    return None


def hub_warning(root):
    """The `HUB` warn line, or None - for no List command, fewer than two sessions
    carrying the Hub session name, or any failure along the way.
    """
    try:
        for path in sorted((root / BINDINGS_REL).glob("*.md")):
            lines = session_launch_section(path.read_text(encoding="utf-8"))
            if lines is not None:
                break
        else:
            return None
        command = slot_value(lines, "List command")
        if command is None:
            return None
        where = path.relative_to(root).as_posix()
        name = slot_value(lines, "Hub session name")
        if name is None:
            return f"warn  HUB  {where}  Hub session name unbound while the List command is bound"
        result = subprocess.run(command, shell=True, stdout=subprocess.PIPE,
                                stderr=subprocess.PIPE, timeout=LIST_WAIT_SECONDS)
        if result.returncode != 0:
            return None
        sessions = json.loads(result.stdout)
        if not isinstance(sessions, list):
            return None
        count = sum(1 for session in sessions
                    if isinstance(session, dict) and session.get("name") == name)
        if count < 2:
            return None
        return f"warn  HUB  {where}  {count} running sessions named {name}"
    except Exception:
        return None


def before_last_line(report, line):
    """`report` with `line` inserted before its final line, in the report's line ending."""
    start = report.rstrip(b"\r\n").rfind(b"\n") + 1
    ending = b"\r\n" if b"\r\n" in report else b"\n"
    return report[:start] + line.encode("utf-8") + ending + report[start:]


def main(argv):
    try:
        root = resolve_root(argv)
        script = harness_script(root)
        if script is not None:
            report = doctor_stdout(root, script)
            if report:
                warning = hub_warning(root)
                if warning is not None:
                    report = before_last_line(report, warning)
                sys.stdout.buffer.write(report)
                sys.stdout.buffer.flush()
    except Exception:
        pass
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
