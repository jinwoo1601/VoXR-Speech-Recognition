# =============================================================================
# Purpose:  Unity preflight checks per unity-plugin-installed.md and unity-pipeline-package.md, each printed as one PASS, FAIL or UNVERIFIABLE line
# Layer:    Scripts
# Owns:     Refusal (exception class)
# Depends:  (none)
# =============================================================================

import json
import os
import re
import shutil
import subprocess
import sys

PLUGIN_TIMEOUT = 60
KILL_TIMEOUT = 5
PASS = 0
REFUSED = 1
FAIL = 2
UNVERIFIABLE = 3
WORDS = {PASS: "PASS", FAIL: "FAIL", UNVERIFIABLE: "UNVERIFIABLE"}
PLUGIN = "unity-plugin-installed"
PIPELINE = "unity-pipeline-package"
PACKAGE = "com.unity.pipeline"
MANIFEST = os.path.join("Packages", "manifest.json")
BREAKS = re.compile(r"[\r\n]+")
LIMITS = ("limits: a session launched with --settings or --add-dir may enable or disable the "
          "plugin otherwise than this check reads, each being per session, not per project")
USAGE = f"usage: preflight-check.py {PLUGIN} | {PIPELINE} --host <value>"


class Refusal(Exception):
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


def where(entry):
    named = [entry.get(key) for key in ("scope", "projectPath")]
    return " ".join(str(part) for part in named if part is not None)


def plugin_reading(out):
    try:
        listed = json.loads(out)
    except ValueError:
        listed = None
    if not isinstance(listed, list):
        return UNVERIFIABLE, f"output is not a JSON array: {folded(out) or 'no output'}"
    for index, element in enumerate(listed):
        if not (isinstance(element, dict) and isinstance(element.get("id"), str)):
            return UNVERIFIABLE, (f"element {index} of the array is not an object with a string "
                                  f"id: {json.dumps(element)}")
    entries = [element for element in listed if element["id"].startswith("unity@")]
    if not entries:
        return FAIL, ("not installed - no entry whose id is unity@<marketplace>; remediation: "
                      "install the unity plugin")
    for entry in entries:
        if not isinstance(entry.get("enabled"), bool):
            return UNVERIFIABLE, (f"an entry of the id carries no boolean enabled: "
                                  f"{json.dumps(entry)}")
    ids = ", ".join(dict.fromkeys(entry["id"] for entry in entries))
    states = {entry["enabled"] for entry in entries}
    if states == {True}:
        return PASS, f"{ids} installed, every one of its {len(entries)} entries enabled true"
    if states == {False}:
        return FAIL, (f"installed but disabled - {ids}, every one of its {len(entries)} entries "
                      "enabled false; remediation: enable it in settings, or managed settings "
                      "block it, which no project setting overrides")
    return UNVERIFIABLE, "entries of the id disagree on enabled: " + ", ".join(
        f"{where(entry)}={'true' if entry['enabled'] else 'false'}" for entry in entries)


def plugin_verdict():
    try:
        claude = shutil.which("claude")
        if claude is None:
            return UNVERIFIABLE, "claude not found on PATH"
        try:
            process = subprocess.Popen([claude, "plugin", "list", "--json"],
                                       stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
                                       stderr=subprocess.PIPE)
        except OSError as error:
            return UNVERIFIABLE, f"claude plugin list --json could not start: {folded(str(error))}"
        try:
            out, err = process.communicate(timeout=PLUGIN_TIMEOUT)
        except subprocess.TimeoutExpired:
            stop(process)
            return UNVERIFIABLE, f"claude plugin list --json timed out after {PLUGIN_TIMEOUT} s"
        out = out.decode("utf-8", "replace")
        err = err.decode("utf-8", "replace")
        if process.returncode != 0:
            return UNVERIFIABLE, (f"claude plugin list --json exited {process.returncode}: "
                                  f"{said(out, err)}")
        return plugin_reading(out)
    except Exception as error:
        return UNVERIFIABLE, f"unexpected error: {type(error).__name__}: {folded(str(error))}"


def plugin():
    code, message = plugin_verdict()
    return code, f"{WORDS[code]}: {PLUGIN}: {message}; {LIMITS}"


def host_value(raw):
    value = raw.strip()
    if len(value) >= 2 and value.startswith("`") and value.endswith("`"):
        value = value[1:-1]
    if not value or "TODO" in value or value.lower() == "none":
        return None
    return value


def pipeline_verdict(raw):
    try:
        root = os.path.normpath(os.getcwd())
        named = host_value(raw)
        if named is None:
            target = root
            manifest = os.path.join(root, MANIFEST)
            if not os.path.isfile(manifest):
                if os.path.isfile(os.path.join(root, "package.json")):
                    return UNVERIFIABLE, (
                        f"a package root with no host named - the project root holds "
                        f"package.json and no {MANIFEST}; fill the Host project slot of the "
                        f"verification binding with the host project's root; looked for "
                        f"{manifest}")
                return UNVERIFIABLE, (f"no Unity project at the project root, which is not proof "
                                      f"the package is absent; looked for {manifest}")
        else:
            target = os.path.normpath(os.path.join(root, named))
            manifest = os.path.join(target, MANIFEST)
            if not os.path.isdir(target):
                return UNVERIFIABLE, (f"the Host project path {named} (resolved {target}) names "
                                      f"no directory; correct that path; looked for {manifest}")
            if not os.path.isfile(manifest):
                return UNVERIFIABLE, (f"the host {named} (resolved {target}) holds no "
                                      f"{MANIFEST}; correct that path; looked for {manifest}")
        try:
            with open(manifest, "rb") as handle:
                data = handle.read()
        except OSError as error:
            return UNVERIFIABLE, f"{manifest} cannot be read: {error.strerror}"
        try:
            parsed = json.loads(data.decode("utf-8"))
        except (ValueError, RecursionError):
            return UNVERIFIABLE, f"{manifest} does not parse as JSON"
        dependencies = parsed.get("dependencies") if isinstance(parsed, dict) else None
        if isinstance(dependencies, dict) and PACKAGE in dependencies:
            version = dependencies[PACKAGE]
            shown = version if isinstance(version, str) else json.dumps(version)
            return PASS, f"{PACKAGE} {shown} in {manifest}"
        return FAIL, (f"{manifest} has no {PACKAGE} in its dependencies; remediation: unity "
                      f'pipeline install --project-path "{target}", run by the human or by the '
                      "PM on the human's word")
    except Exception as error:
        return UNVERIFIABLE, f"unexpected error: {type(error).__name__}: {folded(str(error))}"


def pipeline(raw):
    code, message = pipeline_verdict(raw)
    return code, f"{WORDS[code]}: {PIPELINE}: {message}"


def checked(argv):
    if not argv:
        raise Refusal(f"no subcommand given; {USAGE}")
    if argv[0] == PLUGIN and len(argv) != 1:
        raise Refusal(f"{PLUGIN} takes no argument, {len(argv) - 1} given; {USAGE}")
    if argv[0] == PIPELINE and (len(argv) != 3 or argv[1] != "--host"):
        raise Refusal(f"{PIPELINE} takes exactly --host <value>; {USAGE}")
    if argv[0] not in (PLUGIN, PIPELINE):
        raise Refusal(f"unknown subcommand: {argv[0]}; {USAGE}")
    manifest = os.path.join(os.path.normpath(os.getcwd()), ".claude", "harness.json")
    if not os.path.isfile(manifest):
        raise Refusal(f"no {manifest}; run from the project root")
    return plugin() if argv[0] == PLUGIN else pipeline(argv[2])


def emit(stream, line):
    stream.write((BREAKS.sub(" ", line) + "\n").encode("ascii", "replace"))
    stream.flush()


def main(argv):
    try:
        code, line = checked(argv)
    except Refusal as error:
        emit(sys.stderr.buffer, f"preflight-check.py: {error}")
        return REFUSED
    emit(sys.stdout.buffer, line)
    return code


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
