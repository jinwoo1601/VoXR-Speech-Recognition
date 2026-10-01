# =============================================================================
# Purpose:  SubagentStop hook filing each agent's final report beside the brief it answers
# Layer:    Hooks
# Owns:     (script, no public types)
# Depends:  (none)
# =============================================================================
"""Claude Code `SubagentStop` hook: the delegation contract's filing step, done
by the platform rather than remembered by the delegator.

Standard library only, and never an import of `harness.py`, for the agent
guard's reason: the hook runs under whatever interpreter the platform starts.

The report is the message of the agent's last hand-back tool call where its
transcript holds one, else the stop's `last_assistant_message`; a stop with no
agent type files nothing. The report's first non-blank line names the brief it
answers; a brief that resolves to a `.md` file under a `.scratch/`
directory gets `<stem>-report.md` beside it, any other first line a file under
`.scratch/reports/`. Files are created in exclusive mode
and numbered, so parallel answers to one brief never overwrite each other.

A harness agent - one with a file under `.claude/agents/` - has its receipt
checked: a malformed first line or DONE trailer blocks the stop once, with the
remedy as the agent's next input, and files nothing; the stop that follows a
block is filed whatever it holds, with a system message naming the defect. A
passing report is filed in silence. There is no generic catch anywhere here, so
an unforeseen bug is a traceback and a non-blocking error, never a block.
"""

import json
import re
import sys
from pathlib import Path

AGENTS_REL = ".claude/agents"
REPORTS_REL = ".scratch/reports"
STATUS_LINE = re.compile(r"^(DONE|PARTIAL|BLOCKED|NO-BINDING) [—–-] \S")
DASH = re.compile(r"\s[—–-]\s")
TRAILER_LINE = re.compile(r"^\s*(\*\*)?Trailer:")
TRAILER_KEYS = ("criteria", "files", "verification")
FULL_REPORT = "# Full report —"
HANDBACK_TOOL = "SubagentHandback"
UNSAFE = re.compile(r"[^A-Za-z0-9._-]")
REMEDY = ("end with the contract's report: first line `<STATUS> — <brief path>`, and the "
          "trailer (criteria; files; verification) — "
          "`.claude/references/core/delegation-contract.md` `## The report (up)`")


def resolve_root(argv):
    """The project root: argv[1] when it names a directory, else the process cwd."""
    if len(argv) > 1:
        candidate = Path(argv[1])
        if candidate.is_dir():
            return candidate
    return Path.cwd()


def parse_payload(data):
    """The hook payload as a mapping, or None for stdin the filer cannot read."""
    try:
        document = json.loads(data)
    except ValueError:
        return None
    return document if isinstance(document, dict) else None


def text_field(payload, key):
    """The payload's string under `key`, or "" for an absent or non-string value."""
    value = payload.get(key)
    return value if isinstance(value, str) else ""


def first_line(message):
    """The report's first non-blank line, stripped, or "" when there is none."""
    for line in message.splitlines():
        if line.strip():
            return line.strip()
    return ""


def safe_name(value):
    """`value` with every character outside `[A-Za-z0-9._-]` made `-`."""
    return UNSAFE.sub("-", value)


def resolve_brief(line, root):
    """The brief the first line names, resolved, or None when it names none.

    The text after the first spaced dash, up to the next one, unquoted; relative
    to the root; a resolved `.md` file under a `.scratch/` directory.
    """
    parts = DASH.split(line, maxsplit=2)
    if len(parts) < 2:
        return None
    target = parts[1].strip().strip("`'\"").strip()
    if not target:
        return None
    path = Path(target)
    if not path.is_absolute():
        path = root / path
    path = path.resolve()
    if not path.name.endswith(".md") or not path.is_file():
        return None
    if ".scratch" in [part.lower() for part in path.parts[:-1]]:
        return path
    return None


def assistant_blocks(transcript):
    """The content blocks of the transcript's assistant entries, in order.

    Empty for a path that is not a string and a transcript that is missing or not
    UTF-8; a line that does not parse is skipped.
    """
    if not isinstance(transcript, str) or not transcript:
        return []
    try:
        lines = Path(transcript).read_text(encoding="utf-8").splitlines()
    except (OSError, ValueError):
        return []
    blocks = []
    for line in lines:
        try:
            entry = json.loads(line)
        except ValueError:
            continue
        if not isinstance(entry, dict) or entry.get("type") != "assistant":
            continue
        message = entry.get("message")
        content = message.get("content") if isinstance(message, dict) else None
        if not isinstance(content, list):
            continue
        blocks.extend(block for block in content if isinstance(block, dict))
    return blocks


def full_report(blocks):
    """The last assistant text among the blocks opening `# Full report —`, or None."""
    found = None
    for block in blocks:
        if (block.get("type") == "text" and isinstance(block.get("text"), str)
                and block["text"].lstrip().startswith(FULL_REPORT)):
            found = block["text"]
    return found


def handed_back(blocks):
    """The message of the last hand-back tool call among the blocks, or None."""
    found = None
    for block in blocks:
        tool_input = block.get("input")
        if (block.get("type") == "tool_use" and block.get("name") == HANDBACK_TOOL
                and isinstance(tool_input, dict) and isinstance(tool_input.get("message"), str)):
            found = tool_input["message"]
    return found


def report_of(payload, blocks):
    """The agent's report: the message of its last hand-back tool call where the
    transcript's blocks hold one - the final message is then only what the agent
    said after handing back - else the stop's `last_assistant_message`."""
    message = handed_back(blocks)
    return message if message is not None else text_field(payload, "last_assistant_message")


def declares_trailer(agent_file):
    """True when the agent file carries the text `Trailer:` - the contract's one-line
    DONE trailer; an unreadable or non-UTF-8 file declares nothing."""
    try:
        return "Trailer:" in agent_file.read_text(encoding="utf-8")
    except (OSError, ValueError):
        return False


def receipt_defect(payload, root, message):
    """The part of the receipt that fails - "first line" or "trailer" - or None when
    it passes or goes unchecked: an agent with no file under `.claude/agents/` is
    built in, and is filed, never checked."""
    agent_type = text_field(payload, "agent_type")
    if not agent_type or safe_name(agent_type) != agent_type or not agent_type.strip("."):
        return None
    agent_file = root / AGENTS_REL / (agent_type + ".md")
    if not agent_file.is_file():
        return None
    line = first_line(message)
    if not STATUS_LINE.match(line):
        return "first line"
    if not line.startswith("DONE") or not declares_trailer(agent_file):
        return None
    for candidate in message.splitlines():
        lowered = candidate.lower()
        if TRAILER_LINE.match(candidate) and all(key in lowered for key in TRAILER_KEYS):
            return None
    return "trailer"


def decide(payload, root):
    """What this stop gets: ("skip", None) for a stop with no agent type or an empty
    report, ("block", reason) for a harness agent's malformed receipt on its first
    stop, and ("file", defect) otherwise - the defect None when the receipt passed
    or went unchecked."""
    if not text_field(payload, "agent_type"):
        return "skip", None
    message = report_of(payload, assistant_blocks(payload.get("agent_transcript_path")))
    if not message.strip():
        return "skip", None
    defect = receipt_defect(payload, root, message)
    if defect is not None and not payload.get("stop_hook_active"):
        return "block", f"the report's {defect} is malformed; {REMEDY}"
    return "file", defect


def report_content(payload, message, blocks):
    """The filed bytes: the marker line, then the recon full report and a `---` line
    where the transcript's blocks hold one, then the report; UTF-8 with LF."""
    marker = (f"<!-- report-filer: {text_field(payload, 'agent_type')} "
              f"{text_field(payload, 'agent_id')} -->")
    body = message
    extracted = full_report(blocks)
    if extracted is not None:
        body = extracted.rstrip("\n") + "\n\n---\n\n" + message
    text = (marker + "\n" + body).replace("\r\n", "\n")
    if not text.endswith("\n"):
        text += "\n"
    return text.encode("utf-8")


def file_report(payload, root):
    """Create the report file - beside the resolved brief, else under the reports
    directory - in exclusive mode, numbering `-2`, `-3` past a taken name; its path."""
    blocks = assistant_blocks(payload.get("agent_transcript_path"))
    message = report_of(payload, blocks)
    brief = resolve_brief(first_line(message), root)
    if brief is not None:
        directory = brief.parent
        name = brief.name
        stem = name[:-len("-brief.md")] if name.endswith("-brief.md") else name[:-len(".md")]
    else:
        directory = root / REPORTS_REL
        directory.mkdir(parents=True, exist_ok=True)
        stem = f"{safe_name(text_field(payload, 'agent_type'))}-{text_field(payload, 'agent_id')}"
    content = report_content(payload, message, blocks)
    number = 1
    while True:
        suffix = "-report.md" if number == 1 else f"-report-{number}.md"
        path = directory / (stem + suffix)
        try:
            with open(path, "xb") as handle:
                handle.write(content)
        except FileExistsError:
            number += 1
            continue
        return path


def handle(payload, root):
    """Carry out the verdict: the stdout document to print, or None for silence."""
    action, detail = decide(payload, root)
    if action == "skip":
        return None
    if action == "block":
        return json.dumps({"decision": "block", "reason": detail})
    path = file_report(payload, root)
    if detail is None:
        return None
    return json.dumps({"systemMessage": f"report-filer: {text_field(payload, 'agent_type')} "
                                        f"report filed with a malformed {detail}: {path}"})


def main(argv):
    payload = parse_payload(sys.stdin.buffer.read())
    if payload is None:
        return 0
    document = handle(payload, resolve_root(argv))
    if document is not None:
        print(document)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
