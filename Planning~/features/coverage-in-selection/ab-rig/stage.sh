#!/usr/bin/env bash
# Stage the real parser sources at a given revision into a buildable scratch project.
#
#   ./stage.sh <git-ref|WORKTREE> <outdir>
#
# WORKTREE copies the checked-out files, so the "after" build is literally what ships.
# A git ref uses `git show`, so the "before" build is literally what is on main.
set -euo pipefail

REF="${1:?usage: stage.sh <git-ref|WORKTREE> <outdir>}"
OUT="${2:?usage: stage.sh <git-ref|WORKTREE> <outdir>}"
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
   "$HERE/PlayerCheck.cs" "$HERE/Sweep.cs" "$HERE/Rig.csproj" "$OUT/"

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
