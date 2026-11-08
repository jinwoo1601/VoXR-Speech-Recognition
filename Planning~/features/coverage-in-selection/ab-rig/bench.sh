#!/usr/bin/env bash
# =============================================================================
# Purpose:  Run script for the bench's fast run: confgen, the harness's --dump run (on a pin copy for a named configuration), stage and build a ref, --bench run
# Layer:    Planning.AbRig
# Owns:     (script, no public types)
# Depends:  stage.sh, Bench, vosk-bridge-harness, confgen, py
# =============================================================================
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

die() {
  echo "bench.sh: $1" >&2
  exit 2
}

REF="" BUFFER="" WINDOW="" PIN="" OUT="" PROFILE="" MODE="" GAIN=""

take() {
  [ -z "${!1}" ] || die "$2 given twice"
  case "$3" in "" | --*) die "$2 needs a value" ;; esac
  printf -v "$1" '%s' "$3"
}

while [ $# -gt 0 ]; do
  opt="$1"
  case "$opt" in
    --ref | --buffer | --window | --pin | --out | --profile | --mode | --gain)
      [ $# -ge 2 ] || die "$opt needs a value" ;;
    *) die "unknown argument: $opt" ;;
  esac
  case "$opt" in
    --ref) take REF "$opt" "$2" ;;
    --buffer) take BUFFER "$opt" "$2" ;;
    --window) take WINDOW "$opt" "$2" ;;
    --pin) take PIN "$opt" "$2" ;;
    --out) take OUT "$opt" "$2" ;;
    --profile) take PROFILE "$opt" "$2" ;;
    --mode) take MODE "$opt" "$2" ;;
    --gain) take GAIN "$opt" "$2" ;;
  esac
  shift 2
done

[ -z "$BUFFER" ] || [ -n "$WINDOW" ] || die "--buffer needs --window"
[ -z "$WINDOW" ] || [ -n "$BUFFER" ] || die "--window needs --buffer"

if [ -n "$PROFILE$MODE$GAIN" ]; then NAMED=1; else NAMED=""; fi
case "$PROFILE" in
  "" | Automatic | ModelDefault | Responsive) ;;
  *) die "--profile must be Automatic, ModelDefault or Responsive: $PROFILE" ;;
esac
case "$MODE" in
  "" | Continuous | PushToTalk) ;;
  *) die "--mode must be Continuous or PushToTalk: $MODE" ;;
esac
GAIN_RE='^(0|-[0-9]*[1-9][0-9]*(\.[0-9]+)?|-[0-9]+\.[0-9]*[1-9][0-9]*)$'
[ -z "$GAIN" ] || [[ $GAIN =~ $GAIN_RE ]] ||
  die "--gain must be 0 or a negative decimal in dBFS, as -18 or -12.5: $GAIN"

REF="${REF:-WORKTREE}"
PIN="${PIN:-NativeBridge~/harness/expectations.json}"
OUT="${OUT:-.scratch/bench}"
PROFILE="${PROFILE:-Automatic}"
MODE="${MODE:-Continuous}"
[ -z "$NAMED" ] || { [[ $OUT == .scratch/?* ]] && [[ $OUT != *..* ]]; } ||
  die "--out must lie under .scratch/ for a named configuration (the harness's pin copy is written there): $OUT"
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
[ -z "$NAMED" ] || command -v py >/dev/null 2>&1 ||
  die "no py on PATH: a named configuration writes the harness's pin copy with py"

mkdir -p "$OUT"
for d in "$OUT/model" "$OUT/stage"; do
  if [ -e "$d" ]; then find "$d" -delete; fi
done
find "$OUT" -maxdepth 1 \( -name dump.jsonl -o -name harness.json -o -name harness.err -o -name report.md -o -name harness-pin.json \) -delete

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
dotnet "$CONFGEN_W" --model "$MODEL_W" --out "$OUT_W/model" --profile "$PROFILE" --mode "$MODE" || rc=$?
[ "$rc" -eq 0 ] || die "confgen exit $rc: no report"

HPIN="harness/expectations.json"
if [ -n "$NAMED" ]; then
  rc=0
  py -I -B -c "import json,sys;s,c,g,o=sys.argv[1:];p=json.load(open(s,encoding='utf-8'));p['modelConf']=open(c,'rb').read().decode('utf-8');g and p.__setitem__('gain',float(g));open(o,'w',encoding='utf-8',newline='\n').write(json.dumps(p,ensure_ascii=False,indent=4)+'\n')" \
    "$(abspath "NativeBridge~/harness/expectations.json")" "$OUT_W/model/conf/model.conf" "$GAIN" \
    "$OUT_W/harness-pin.json" || rc=$?
  [ "$rc" -eq 0 ] || die "pin copy: py exit $rc: no report"
  HPIN="$OUT_W/harness-pin.json"
fi

rc=0
(cd "NativeBridge~" && ./build-windows/bin/vosk-bridge-harness.exe --model "$OUT_W/model" \
  --fixtures "../Tests~/Fixtures/audio/tts" --manifest "$HPIN" \
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
  --harness-exit "$HARNESS_EXIT" --report "$OUT_W/report.md" \
  ${NAMED:+--profile "$PROFILE" --mode "$MODE"}) || exit $?

echo "$OUT_W/report.md"
exit 0
