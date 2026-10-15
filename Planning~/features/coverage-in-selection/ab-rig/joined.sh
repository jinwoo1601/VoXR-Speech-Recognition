#!/usr/bin/env bash
# =============================================================================
# Purpose:  Run script for the rig's joined mode: stage and build each ref, score each side, report
# Layer:    Planning.AbRig
# Owns:     (script, no public types)
# Depends:  stage.sh, Joined (the rig's --joined score and --joined report)
# =============================================================================
# feat-joined-rig-parser. Run from the project root in Git Bash, one of two shapes:
#
#   bash Planning~/features/coverage-in-selection/ab-rig/joined.sh --vary parser \
#       --dump D --ref-a R --ref-b R2 [--manifest M] [--pin P] [--out O] [--evaluate]
#   bash Planning~/features/coverage-in-selection/ab-rig/joined.sh --vary decoder \
#       --dump-a D --dump-b D2 --ref R [--manifest M] [--pin P] [--out O] [--evaluate]
#
# A ref is a git ref or WORKTREE (stage.sh). Defaults: --manifest
# Tests~/Fixtures/audio/manifest.json, --pin NativeBridge~/harness/expectations.json,
# --out .scratch/joined. Varying one side per run is structural: the two shapes cannot
# express both.
#
# Procedure. The operator makes each dump with the harness Command 3 built, re-run with
# --dump <path>. Then this script:
#   1. checks the arguments against the shape and that each dump, the manifest and the pin
#      is a file (else `joined.sh: <problem>`, exit 2); creates the out dir and removes its
#      scored-*.jsonl and report.md;
#   2. stages each distinct ref (ref-a, or --ref, into <out>/stage-a; ref-b into
#      <out>/stage-b unless equal) and builds it, all before any score (else
#      `joined.sh: <ref>: stage failed` / `build failed`, exit 2);
#   3. scores each side in its stage, side a first, every path in drive-letter form:
#      --vary parser, the one dump through ref-a (a) and ref-b (b), both pinned;
#      --vary decoder, dump-a pinned (a) and dump-b unpinned (b) through the one ref.
#      A score exit other than 0 (2 an input or guard stop, 3 the kill check) removes this
#      run's scored files and is this script's exit; --evaluate goes to both scores;
#   4. reports: `--joined report` in stage-a on the two scored files writes <out>/report.md
#      and prints it (an exit other than 0 is this script's exit); then prints the scored
#      and report paths.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

die() {
  echo "joined.sh: $1" >&2
  exit 2
}

VARY="" DUMP="" DUMP_A="" DUMP_B="" REF="" REF_A="" REF_B="" MANIFEST="" PIN="" OUT="" EVALUATE=""

# take <var> <option> <value>: each option once, with a value.
take() {
  [ -z "${!1}" ] || die "$2 given twice"
  case "$3" in "" | --*) die "$2 needs a value" ;; esac
  printf -v "$1" '%s' "$3"
}

while [ $# -gt 0 ]; do
  opt="$1"
  case "$opt" in
    --evaluate)
      # No value, at most once.
      [ -z "$EVALUATE" ] || die "--evaluate given twice"
      EVALUATE=1
      shift
      continue
      ;;
    --vary | --dump | --dump-a | --dump-b | --ref | --ref-a | --ref-b | --manifest | --pin | --out)
      [ $# -ge 2 ] || die "$opt needs a value" ;;
    *) die "unknown argument: $opt" ;;
  esac
  case "$opt" in
    --vary) take VARY "$opt" "$2" ;;
    --dump) take DUMP "$opt" "$2" ;;
    --dump-a) take DUMP_A "$opt" "$2" ;;
    --dump-b) take DUMP_B "$opt" "$2" ;;
    --ref) take REF "$opt" "$2" ;;
    --ref-a) take REF_A "$opt" "$2" ;;
    --ref-b) take REF_B "$opt" "$2" ;;
    --manifest) take MANIFEST "$opt" "$2" ;;
    --pin) take PIN "$opt" "$2" ;;
    --out) take OUT "$opt" "$2" ;;
  esac
  shift 2
done

case "$VARY" in
  parser)
    [ -z "$DUMP_A$DUMP_B$REF" ] || die "--vary parser takes --dump, --ref-a, --ref-b; not --dump-a, --dump-b or --ref"
    [ -n "$DUMP" ] || die "--vary parser needs --dump"
    [ -n "$REF_A" ] || die "--vary parser needs --ref-a"
    [ -n "$REF_B" ] || die "--vary parser needs --ref-b"
    A_REF="$REF_A" B_REF="$REF_B"
    INPUTS=("$DUMP")
    ;;
  decoder)
    [ -z "$DUMP$REF_A$REF_B" ] || die "--vary decoder takes --dump-a, --dump-b, --ref; not --dump, --ref-a or --ref-b"
    [ -n "$DUMP_A" ] || die "--vary decoder needs --dump-a"
    [ -n "$DUMP_B" ] || die "--vary decoder needs --dump-b"
    [ -n "$REF" ] || die "--vary decoder needs --ref"
    A_REF="$REF" B_REF="$REF"
    INPUTS=("$DUMP_A" "$DUMP_B")
    ;;
  "") die "--vary parser|decoder is required" ;;
  *) die "--vary must be parser or decoder, not: $VARY" ;;
esac

MANIFEST="${MANIFEST:-Tests~/Fixtures/audio/manifest.json}"
PIN="${PIN:-NativeBridge~/harness/expectations.json}"
OUT="${OUT:-.scratch/joined}"

for f in "${INPUTS[@]}" "$MANIFEST" "$PIN"; do
  [ -f "$f" ] || die "not a file: $f"
done

mkdir -p "$OUT"
# `rm -rf` is blocked in this environment; `find -delete` is not (as stage.sh).
find "$OUT" -maxdepth 1 \( -name 'scored-*.jsonl' -o -name report.md \) -delete

# dotnet runs inside the stage, so every path it gets is absolute, in drive-letter form
# where cygpath exists (as Tests~/Fixtures/generate.sh does): MSYS_NO_PATHCONV set or
# unset makes no difference.
abspath() {
  if command -v cygpath >/dev/null 2>&1; then
    cygpath -am "$1"
  else
    case "$1" in
      /*) printf '%s\n' "$1" ;;
      *) printf '%s\n' "$PWD/$1" ;;
    esac
  fi
}

stage() {
  env -u MSYS_NO_PATHCONV bash "$HERE/stage.sh" "$1" "$2" || die "$1: stage failed"
  (cd "$2" && dotnet build -v q --nologo) || die "$1: build failed"
}

STAGE_A="$OUT/stage-a"
STAGE_B="$OUT/stage-b"
stage "$A_REF" "$STAGE_A"
if [ "$B_REF" = "$A_REF" ]; then
  STAGE_B="$STAGE_A"
else
  stage "$B_REF" "$STAGE_B"
fi

MANIFEST_W="$(abspath "$MANIFEST")"
PIN_W="$(abspath "$PIN")"
OUT_W="$(abspath "$OUT")"

# score <stage> <side> <dump> [--pinned]
score() {
  local stage="$1" side="$2" dump="$3" rc=0
  shift 3
  (cd "$stage" && dotnet run --no-build -- --joined score --dump "$dump" --manifest "$MANIFEST_W" \
    --pin "$PIN_W" --out "$OUT_W/scored-$side.jsonl" --side "$side" --vary "$VARY" "$@") || rc=$?
  if [ "$rc" -ne 0 ]; then
    find "$OUT" -maxdepth 1 -name 'scored-*.jsonl' -delete
    exit "$rc"
  fi
}

if [ "$VARY" = parser ]; then
  DUMP_W="$(abspath "$DUMP")"
  score "$STAGE_A" a "$DUMP_W" --pinned ${EVALUATE:+--evaluate}
  score "$STAGE_B" b "$DUMP_W" --pinned ${EVALUATE:+--evaluate}
else
  score "$STAGE_A" a "$(abspath "$DUMP_A")" --pinned ${EVALUATE:+--evaluate}
  score "$STAGE_B" b "$(abspath "$DUMP_B")" ${EVALUATE:+--evaluate}
fi

# The report, from stage a, on this run's two scored files.
(cd "$STAGE_A" && dotnet run --no-build -- --joined report --a "$OUT_W/scored-a.jsonl" \
  --b "$OUT_W/scored-b.jsonl" --out "$OUT_W/report.md") || exit $?

echo "$OUT_W/scored-a.jsonl"
echo "$OUT_W/scored-b.jsonl"
echo "$OUT_W/report.md"
exit 0
