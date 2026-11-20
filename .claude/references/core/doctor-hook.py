# =============================================================================
# Purpose:  SessionStart hook relaying `harness.py doctor`'s report into a session
# Layer:    Hooks
# Owns:     (script, no public types)
# Depends:  hook-common.py
# =============================================================================

import json
import os
import re
import subprocess
import sys
import tempfile
from pathlib import Path

try:
    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "hook-common.py"), "rb") as common:
        exec(compile(common.read(), common.name, "exec"), globals())
    COMMON_ERROR = None
except Exception as error:
    COMMON_ERROR = error

MANIFEST_REL = ".claude/harness.json"
BINDINGS_REL = ".claude/bindings"
WAIT_SECONDS = 30
LIST_WAIT_SECONDS = 5


def trees_section(root, filler):
    path = root / BINDINGS_REL / f"{binding_stem(filler.get('trees'))}.md"
    return binding_section(path.read_text(encoding="utf-8"), "trees") if path.is_file() else None


def harness_script(root):
    manifest = root / MANIFEST_REL
    if not manifest.is_file():
        return None
    try:
        document = json.loads(manifest.read_text(encoding="utf-8"))
    except (OSError, ValueError, RecursionError):
        return None
    base = document.get("base") if isinstance(document, dict) else None
    if not isinstance(base, str) or not base:
        return None
    base_dir = (root / base).resolve()
    if not base_dir.is_dir():
        filler = document.get("bindings")
        filler = filler if isinstance(filler, dict) else {}
        owner = root.resolve()
        try:
            hub = trees_hub(trees_section(root, filler))
        except (OSError, UnicodeDecodeError):
            hub = None
        pointer = document.get("parent")
        if hub is None and isinstance(pointer, str) and pointer:
            owner = (root / pointer).resolve()
            try:
                parent = json.loads((owner / MANIFEST_REL).read_text(encoding="utf-8"))
            except (OSError, ValueError, RecursionError):
                parent = None
            beneath = parent.get("bindings") if isinstance(parent, dict) else None
            beneath = beneath if isinstance(beneath, dict) else {}
            try:
                hub = trees_hub(trees_section(owner, {**beneath, **filler}))
            except (OSError, UnicodeDecodeError):
                hub = None
        if hub is not None:
            try:
                candidate = (Path(hub) / os.path.relpath(root.resolve(), owner) / base).resolve()
            except ValueError:
                candidate = None
            if candidate is not None and candidate.is_dir():
                base_dir = candidate
    script = base_dir / "scripts/harness.py"
    return script if script.is_file() else None


def child_env():
    env = dict(os.environ)
    venv = env.pop("VIRTUAL_ENV", None)
    if venv and "PATH" in env:
        inside = os.path.join(os.path.normcase(os.path.abspath(venv)), "")
        env["PATH"] = os.pathsep.join(
            entry for entry in env["PATH"].split(os.pathsep)
            if not os.path.join(os.path.normcase(os.path.abspath(entry)), "").startswith(inside))
    return env


def first_line(stderr):
    lines = (line.strip() for line in stderr.decode("utf-8", "replace").splitlines())
    return next((line for line in lines if line), "no output")


def doctor_stdout(root, script):
    env = child_env()
    argv = [str(script), "doctor", "--root", str(root)]
    try:
        try:
            result = subprocess.run(["py", *argv], cwd=str(root), env=env,
                                    stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                    timeout=WAIT_SECONDS)
        except OSError:
            result = subprocess.run([sys.executable, *argv], cwd=str(root), env=env,
                                    stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                    timeout=WAIT_SECONDS)
    except subprocess.TimeoutExpired:
        return b"", None, f"timed out after {WAIT_SECONDS} s"
    except OSError:
        return b"", None, None
    return result.stdout, result.returncode, first_line(result.stderr)


def slot_value(lines, label):
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
    try:
        for path in sorted((root / BINDINGS_REL).glob("*.md")):
            lines = binding_section(path.read_text(encoding="utf-8"), "session-launch")
            if lines is not None:
                break
        else:
            return None
        command = slot_value(lines, "List command")
        if command is None:
            return None
        where = path.relative_to(root).as_posix()
        name = slot_name(slot_text(lines, "Hub session name"))
        if name is None:
            return f"warn  HUB  {where}  Hub session name unbound while the List command is bound"
        with tempfile.TemporaryFile() as out, tempfile.TemporaryFile() as err:
            result = subprocess.run(command, shell=True, stdout=out, stderr=err,
                                    timeout=LIST_WAIT_SECONDS)
            out.seek(0)
            stdout = out.read()
        if result.returncode != 0:
            return None
        sessions = json.loads(stdout)
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
    start = report.rstrip(b"\r\n").rfind(b"\n") + 1
    ending = b"\r\n" if b"\r\n" in report else b"\n"
    return report[:start] + line.encode("utf-8") + ending + report[start:]


def main(argv):
    try:
        if COMMON_ERROR is not None:
            raise COMMON_ERROR
        root = resolve_root(argv)
        script = harness_script(root)
        if script is not None:
            report, code, reason = doctor_stdout(root, script)
            if report:
                warning = hub_warning(root)
                if warning is not None:
                    report = before_last_line(report, warning)
                sys.stdout.buffer.write(report)
                sys.stdout.buffer.flush()
            elif code:
                line = (f"warn  DOCTOR  {script}  harness.py doctor exited {code} "
                        f"with no report: {reason}\n")
                sys.stdout.buffer.write(line.encode("utf-8"))
                sys.stdout.buffer.flush()
            elif code is None and reason is not None:
                line = f"warn  DOCTOR  {script}  harness.py doctor {reason} with no report\n"
                sys.stdout.buffer.write(line.encode("utf-8"))
                sys.stdout.buffer.flush()
    except Exception:
        pass
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
