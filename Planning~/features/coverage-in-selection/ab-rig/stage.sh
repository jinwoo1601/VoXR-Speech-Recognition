#!/usr/bin/env bash
# Stage the real parser sources at a given revision into a buildable scratch project.
#
#   ./stage.sh <git-ref|WORKTREE> <outdir> [recogniser [--buffer time|eager --window <s>]]
#
# WORKTREE copies the checked-out files, so the "after" build is literally what ships.
# A git ref uses `git show`, so the "before" build is literally what is on main.
# `recogniser` (feat-joined-rig-recogniser) adds the command and speech recognisers' sources and
# the recogniser-only rig files; without it the stage is the parser's alone.
set -euo pipefail

USAGE="usage: stage.sh <git-ref|WORKTREE> <outdir> [recogniser [--buffer time|eager --window <s>]]"
REF="${1:?$USAGE}"
OUT="${2:?$USAGE}"
LAYER="${3:-}"
if [ -n "$LAYER" ] && [ "$LAYER" != "recogniser" ]; then
  echo "$USAGE" >&2
  exit 2
fi
# Args 4-7 (endpointer-profile, *Buffer setups*): after `recogniser` only, `--buffer time|eager`
# and `--window <digits>[.<digits>]`, once each, either order. A repeated flag leaves the
# other unset and an unknown one sets neither, so each is caught as missing.
BUFFER=""
WINDOW=""
if [ $# -gt 3 ]; then
  if [ "$LAYER" = "recogniser" ] && [ $# -eq 7 ]; then
    for i in 4 6; do
      j=$((i + 1))
      case "${!i}" in
        --buffer) BUFFER="${!j}" ;;
        --window) WINDOW="${!j}" ;;
      esac
    done
  fi
  if ! [[ "$BUFFER" =~ ^(time|eager)$ && "$WINDOW" =~ ^[0-9]+(\.[0-9]+)?$ ]]; then
    echo "$USAGE" >&2
    exit 2
  fi
fi
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(git -C "$HERE" rev-parse --show-toplevel)"

SOURCES=(
  Runtime/Commands/VoxrCommandParser.cs
  Runtime/Commands/VoxrSlotDefinition.cs
  Runtime/Commands/VoxrCommandDefinition.cs
  Runtime/Commands/VoxrNumberParser.cs
  Runtime/Commands/VoxrSlotType.cs
  Runtime/Commands/VoxrSlotResolution.cs
  Runtime/Commands/VoxrCommand.cs
  Runtime/Commands/VoxrPendingCommand.cs
  Runtime/Commands/VoxrMatchDiagnostics.cs
  Runtime/VoxrResult.cs
)

# The recogniser stage's closure. Not staged: Runtime/Native/BridgeNative.cs and
# Runtime/ModelExtractor.cs (the rig's stand-ins take their place); EditorMicBackend.cs,
# Native/VoxrNative.cs and Native/VoxrGrammarVocabulary.cs (wholly #if UNITY_EDITOR_WIN).
if [ "$LAYER" = "recogniser" ]; then
  SOURCES+=(
    Runtime/Commands/VoxrCommandRecogniser.cs
    Runtime/Commands/GrammarManager.cs
    Runtime/Commands/UtteranceBuffer.cs
    Runtime/Commands/CommandDebouncer.cs
    Runtime/Commands/CommandSetManager.cs
    Runtime/Commands/DynamicSlotManager.cs
    Runtime/Commands/PendingCommandHandler.cs
    Runtime/Commands/VoxrCommandSet.cs
    Runtime/Commands/VoxrPendingAmbiguity.cs
    Runtime/Commands/VoxrSlotAsset.cs
    Runtime/Commands/VoxrCommandSetAsset.cs
    Runtime/Commands/VoxrCommandAsset.cs
    Runtime/VoxrSpeechRecogniser.cs
    Runtime/VoxrBridgeErrorCode.cs
    Runtime/VoxrJsonParser.cs
    Runtime/VoxrGrammarValidator.cs
  )
fi

# `rm -rf` is blocked in this environment; `find -delete` is not.
if [ -e "$OUT" ]; then find "$OUT" -mindepth 1 -delete; fi
mkdir -p "$OUT"

for src in "${SOURCES[@]}"; do
  dest="$OUT/$(basename "$src")"
  if [ "$REF" = "WORKTREE" ]; then
    cp "$REPO/$src" "$dest"
  else
    git -C "$REPO" show "$REF:$src" > "$dest"
  fi
done

cp "$HERE/UnityStub.cs" "$HERE/Program.cs" "$HERE/Check.cs" "$HERE/DocCheck.cs" \
   "$HERE/PlayerCheck.cs" "$HERE/Sweep.cs" "$HERE/Rig.csproj" \
   "$HERE/JoinedInput.cs" "$HERE/Joined.cs" "$HERE/JoinedScorer.cs" "$HERE/JoinedReport.cs" \
   "$OUT/"
if [ "$LAYER" = "recogniser" ]; then
  cp "$HERE/JoinedRecogniser.cs" "$HERE/BridgeNative.cs" "$HERE/ModelExtractor.cs" "$OUT/"
fi

# The buffer setup: the staged recogniser's initializers edited, each text on exactly one
# line before and after its edit, else a stop by name. Literal sed (`.` escaped, no `^`/`$`),
# in binary mode (`-b`), so each line keeps its CR.
if [ -n "$BUFFER" ]; then
  REC="$OUT/VoxrCommandRecogniser.cs"
  # once <text>: exactly one line of the staged recogniser holds <text>.
  once() {
    local n
    n="$(grep -F -c "$1" "$REC" || true)"
    if [ "$n" != "1" ]; then
      echo "stage.sh: initializer '$1' matches $n times in $REF's VoxrCommandRecogniser.cs" >&2
      exit 2
    fi
  }
  once "bufferWindow = 0.5f;"
  if [ "$BUFFER" = "eager" ]; then
    once "eagerFlushOnCompleteMatch = false;"
  fi
  sed -b -i "s/bufferWindow = 0\.5f;/bufferWindow = ${WINDOW}f;/" "$REC"
  once "bufferWindow = ${WINDOW}f;"
  if [ "$BUFFER" = "eager" ]; then
    sed -b -i "s/eagerFlushOnCompleteMatch = false;/eagerFlushOnCompleteMatch = true;/" "$REC"
    once "eagerFlushOnCompleteMatch = true;"
  fi
fi

# Pin what was staged, so a report can never be attributed to the wrong revision.
if [ "$REF" = "WORKTREE" ]; then
  DIRTY=""
  git -C "$REPO" diff --quiet HEAD -- Runtime || DIRTY="+dirty"
  echo "worktree at $(git -C "$REPO" rev-parse --short HEAD)$DIRTY" > "$OUT/STAGED_FROM"
else
  echo "$REF $(git -C "$REPO" rev-parse --short "$REF")" > "$OUT/STAGED_FROM"
fi
grep -n 'RequiredLiteralMissPenalty *=' "$OUT/VoxrCommandParser.cs" >> "$OUT/STAGED_FROM"
if [ -n "$BUFFER" ]; then
  echo "edit: bufferWindow = 0.5f -> ${WINDOW}f" >> "$OUT/STAGED_FROM"
  if [ "$BUFFER" = "eager" ]; then
    echo "edit: eagerFlushOnCompleteMatch = false -> true" >> "$OUT/STAGED_FROM"
  fi
fi
cat "$OUT/STAGED_FROM"
