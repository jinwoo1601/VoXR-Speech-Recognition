#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Claude Code hook shim for surgical CSharpier formatting.
#
#   csharpier-hook.sh snapshot   -> PreToolUse(Edit|Write):  stash pre-edit bytes
#   csharpier-hook.sh format     -> PostToolUse(Edit|Write): format edited lines
#
# Both hooks fire on every Edit/Write, so bail out cheaply before paying for a
# Python start-up when the payload cannot possibly name a .cs file.
# All real work (and all logging) lives in csharpier-hook.py.
# ---------------------------------------------------------------------------
set -uo pipefail

input=$(cat)
case "$input" in
  *.cs\"*) ;;
  *) exit 0 ;;
esac

printf '%s' "$input" | python3 "$(dirname "$0")/csharpier-hook.py" "${1:-}"
exit 0
