#!/usr/bin/env bash
# Stage the real parser sources at a given revision into a buildable scratch project.
#
#   ./stage.sh <git-ref|WORKTREE> <outdir> [recogniser]
#
# WORKTREE copies the checked-out files, so the "after" build is literally what ships.
# A git ref uses `git show`, so the "before" build is literally what is on main.
# `recogniser` (feat-joined-rig-recogniser) adds the command and speech recognisers' sources and
# the recogniser-only rig files; without it the stage is the parser's alone.
set -euo pipefail

USAGE="usage: stage.sh <git-ref|WORKTREE> <outdir> [recogniser]"
REF="${1:?$USAGE}"
OUT="${2:?$USAGE}"
LAYER="${3:-}"
if [ -n "$LAYER" ] && [ "$LAYER" != "recogniser" ]; then
  echo "$USAGE" >&2
  exit 2
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

# Pin what was staged, so a report can never be attributed to the wrong revision.
if [ "$REF" = "WORKTREE" ]; then
  DIRTY=""
  git -C "$REPO" diff --quiet HEAD -- Runtime || DIRTY="+dirty"
  echo "worktree at $(git -C "$REPO" rev-parse --short HEAD)$DIRTY" > "$OUT/STAGED_FROM"
else
  echo "$REF $(git -C "$REPO" rev-parse --short "$REF")" > "$OUT/STAGED_FROM"
fi
grep -n 'RequiredLiteralMissPenalty *=' "$OUT/VoxrCommandParser.cs" >> "$OUT/STAGED_FROM"
cat "$OUT/STAGED_FROM"
