#!/usr/bin/env bash
# =============================================================================
# Purpose:  Run script for the bench's fast run: confgen, the harness's --dump run, stage and build a ref, --bench run
# Layer:    Planning.AbRig
# Owns:     (script, no public types)
# Depends:  stage.sh, Bench, vosk-bridge-harness, confgen
# =============================================================================
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

die() {
  echo "bench.sh: $1" >&2
  exit 2
}

REF="" BUFFER="" WINDOW="" PIN="" OUT=""

take() {
  [ -z "${!1}" ] || die "$2 given twice"
  case "$3" in "" | --*) die "$2 needs a value" ;; esac
  printf -v "$1" '%s' "$3"
}

while [ $# -gt 0 ]; do
  opt="$1"
  case "$opt" in
    --ref | --buffer | --window | --pin | --out)
      [ $# -ge 2 ] || die "$opt needs a value" ;;
    *) die "unknown argument: $opt" ;;
  esac
  case "$opt" in
    --ref) take REF "$opt" "$2" ;;
    --buffer) take BUFFER "$opt" "$2" ;;
    --window) take WINDOW "$opt" "$2" ;;
    --pin) take PIN "$opt" "$2" ;;
    --out) take OUT "$opt" "$2" ;;
  esac
  shift 2
done

[ -z "$BUFFER" ] || [ -n "$WINDOW" ] || die "--buffer needs --window"
[ -z "$WINDOW" ] || [ -n "$BUFFER" ] || die "--window needs --buffer"

REF="${REF:-WORKTREE}"
PIN="${PIN:-NativeBridge~/harness/expectations.json}"
OUT="${OUT:-.scratch/bench}"
HARNESS="NativeBridge~/build-windows/bin/vosk-bridge-harness.exe"
CONFGEN="NativeBridge~/build-confgen/bin/confgen/debug/confgen.dll"
MODEL="NativeBridge~/vendor/vosk-model-small-en-us-0.15"
MANIFEST="Tests~/Fixtures/audio/manifest.json"
FIXTURES="Tests~/Fixtures/audio/tts"

[ -f "$HARNESS" ] || die "no harness build at $HARNESS: build it with Command 3 (.claude/bindings/unity.md)"
[ -f "$CONFGEN" ] || die "no confgen build at $CONFGEN: build it with Command 3 (.claude/bindings/unity.md)"
[ -d "$MODEL" ] || die "no model at $MODEL: provision NativeBridge~/vendor (NativeBridge~/harness/README.md, Provisioning a branch tree)"
for f in "$PIN" "$MANIFEST"; do
  [ -f "$f" ] || die "not a file: $f"
done
[ -d "$FIXTURES" ] || die "not a directory: $FIXTURES"

mkdir -p "$OUT"
for d in "$OUT/model" "$OUT/stage"; do
  if [ -e "$d" ]; then find "$d" -delete; fi
done
find "$OUT" -maxdepth 1 \( -name dump.jsonl -o -name harness.json -o -name harness.err -o -name report.md \) -delete

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

OUT_W="$(abspath "$OUT")"
CONFGEN_W="$(abspath "$CONFGEN")"
MODEL_W="$(abspath "$MODEL")"
MANIFEST_W="$(abspath "$MANIFEST")"
PIN_W="$(abspath "$PIN")"
ROOT_W="$(abspath "Tests~/Fixtures")"

rc=0
dotnet "$CONFGEN_W" --model "$MODEL_W" --out "$OUT_W/model" --profile Automatic --mode Continuous || rc=$?
[ "$rc" -eq 0 ] || die "confgen exit $rc: no report"

rc=0
(cd "NativeBridge~" && ./build-windows/bin/vosk-bridge-harness.exe --model "$OUT_W/model" \
  --fixtures "../Tests~/Fixtures/audio/tts" --manifest harness/expectations.json \
  --dump "$OUT_W/dump.jsonl") >"$OUT/harness.json" 2>"$OUT/harness.err" || rc=$?
if [ "$rc" -ne 0 ] && [ "$rc" -ne 1 ]; then
  cat "$OUT/harness.err" >&2
  die "harness exit $rc: no report"
fi
HARNESS_EXIT="$rc"

env -u MSYS_NO_PATHCONV bash "$HERE/stage.sh" "$REF" "$OUT/stage" recogniser \
  ${BUFFER:+--buffer "$BUFFER" --window "$WINDOW"} || die "$REF: stage failed"
(cd "$OUT/stage" && dotnet build -v q --nologo) || die "$REF: build failed"

(cd "$OUT/stage" && dotnet run --no-build -- --bench run --dump "$OUT_W/dump.jsonl" \
  --manifest "$MANIFEST_W" --pin "$PIN_W" --fixtures-root "$ROOT_W" \
  --harness-exit "$HARNESS_EXIT" --report "$OUT_W/report.md") || exit $?

echo "$OUT_W/report.md"
exit 0
