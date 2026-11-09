#!/usr/bin/env bash
# =============================================================================
# Purpose:  Run script for the bench: confgen, the harness's --dump run (on a pin copy for a named configuration), stage and build a ref, --bench run; with --paced, the --paced 16384 pass and --bench matched-check, then the --paced PACE pass before --bench run; with --condition and --seed, --bench condition after the build, the harness on <out>/cond, then --bench run with the conditioned block
# Layer:    Planning.AbRig
# Owns:     (script, no public types)
# Depends:  stage.sh, Bench, BenchConditions, vosk-bridge-harness, confgen, py
# =============================================================================
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

die() {
  echo "bench.sh: $1" >&2
  exit 2
}

REF="" BUFFER="" WINDOW="" PIN="" OUT="" PROFILE="" MODE="" GAIN="" PACED="" CONDITION="" SEED=""

take() {
  [ -z "${!1}" ] || die "$2 given twice"
  case "$3" in "" | --*) die "$2 needs a value" ;; esac
  printf -v "$1" '%s' "$3"
}

while [ $# -gt 0 ]; do
  opt="$1"
  case "$opt" in
    --paced)
      [ -z "$PACED" ] || die "--paced given twice"; PACED=1; shift; continue ;;
    --ref | --buffer | --window | --pin | --out | --profile | --mode | --gain | --condition | --seed)
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
    --condition) take CONDITION "$opt" "$2" ;;
    --seed) take SEED "$opt" "$2" ;;
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
[ -z "$SEED" ] || [ -n "$CONDITION" ] || die "--seed needs --condition"
[ -z "$CONDITION" ] || [ -n "$SEED" ] || die "--condition needs --seed"
[ -z "$CONDITION" ] || [ -z "$PACED" ] ||
  die "--condition with --paced is not built yet (bench-conditions Phase 5)"

REF="${REF:-WORKTREE}"
PIN="${PIN:-NativeBridge~/harness/expectations.json}"
OUT="${OUT:-.scratch/bench}"
PROFILE="${PROFILE:-Automatic}"
MODE="${MODE:-Continuous}"
[ -z "$NAMED" ] || { [[ $OUT == .scratch/?* ]] && [[ $OUT != *..* ]]; } ||
  die "--out must lie under .scratch/ for a named configuration (the harness's pin copy is written there): $OUT"
[ -z "$CONDITION" ] || { [[ $OUT == .scratch/?* ]] && [[ $OUT != *..* ]]; } ||
  die "--out must lie under .scratch/ for a conditioned run (the conditioned audio is written there): $OUT"
HARNESS="NativeBridge~/build-windows/bin/vosk-bridge-harness.exe"
CONFGEN="NativeBridge~/build-confgen/bin/confgen/debug/confgen.dll"
MODEL="NativeBridge~/vendor/vosk-model-small-en-us-0.15"
MANIFEST="Tests~/Fixtures/audio/manifest.json"
NEGATIVES="Tests~/Fixtures/audio/negatives/manifest.json"
FIXTURES="Tests~/Fixtures/audio/tts"
PACE=960

[ -f "$HARNESS" ] || die "no harness build at $HARNESS: build it with Command 3 (.claude/bindings/unity.md)"
[ -f "$CONFGEN" ] || die "no confgen build at $CONFGEN: build it with Command 3 (.claude/bindings/unity.md)"
[ -d "$MODEL" ] || die "no model at $MODEL: provision NativeBridge~/vendor (NativeBridge~/harness/README.md, Provisioning a branch tree)"
for f in "$PIN" "$MANIFEST"; do
  [ -f "$f" ] || die "not a file: $f"
done
[ -z "$CONDITION" ] || [ -f "$NEGATIVES" ] || die "not a file: $NEGATIVES"
[ -d "$FIXTURES" ] || die "not a directory: $FIXTURES"
[ -z "$NAMED" ] || command -v py >/dev/null 2>&1 ||
  die "no py on PATH: a named configuration writes the harness's pin copy with py"
[ -z "$PACED" ] || command -v py >/dev/null 2>&1 ||
  die "no py on PATH: --paced names a failing case with py"

mkdir -p "$OUT"
for d in "$OUT/model" "$OUT/stage" "$OUT/cond"; do
  if [ -e "$d" ]; then find "$d" -delete; fi
done
find "$OUT" -maxdepth 1 \( -name dump.jsonl -o -name harness.json -o -name harness.err -o -name report.md -o -name harness-pin.json \
  -o -name matched.jsonl -o -name matched.json -o -name matched.err -o -name paced.jsonl -o -name paced.json -o -name paced.err \
  -o -name cond.jsonl -o -name cond.json -o -name cond.err \) -delete

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
NEGATIVES_W="$(abspath "$NEGATIVES")"

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
HPIN_W="$HPIN"; [ -n "$NAMED" ] || HPIN_W="$(abspath "NativeBridge~/$HPIN")"

rc=0
(cd "NativeBridge~" && ./build-windows/bin/vosk-bridge-harness.exe --model "$OUT_W/model" \
  --fixtures "../Tests~/Fixtures/audio/tts" --manifest "$HPIN" \
  --dump "$OUT_W/dump.jsonl") >"$OUT/harness.json" 2>"$OUT/harness.err" || rc=$?
if [ "$rc" -ne 0 ] && [ "$rc" -ne 1 ]; then
  cat "$OUT/harness.err" >&2
  die "harness exit $rc: no report"
fi
HARNESS_EXIT="$rc"

pass() {
  rc=0
  (cd "NativeBridge~" && ./build-windows/bin/vosk-bridge-harness.exe --model "$OUT_W/model" \
    --fixtures "../Tests~/Fixtures/audio/tts" --manifest "$HPIN" \
    --paced "$1" --dump "$OUT_W/$2.jsonl") >"$OUT/$2.json" 2>"$OUT/$2.err" || rc=$?
  if [ "$rc" -ne 0 ] && [ "$rc" -ne 1 ]; then
    cat "$OUT/$2.err" >&2
    py -I -B -c "import json,sys;[print('bench.sh: case \"%s\": %s'%(c['file'],'; '.join(c['errors']))) for c in json.load(open(sys.argv[1],encoding='utf-8'))['cases'] if c['errors']]" "$OUT/$2.json" >&2 || true
    die "harness --paced $1 exit $rc: no report"
  fi
}

env -u MSYS_NO_PATHCONV bash "$HERE/stage.sh" "$REF" "$OUT/stage" recogniser \
  ${BUFFER:+--buffer "$BUFFER" --window "$WINDOW"} || die "$REF: stage failed"
(cd "$OUT/stage" && dotnet build -v q --nologo) || die "$REF: build failed"

if [ -n "$CONDITION" ]; then
  (cd "$OUT/stage" && dotnet run --no-build -- --bench condition --dump "$OUT_W/dump.jsonl" \
    --manifest "$MANIFEST_W" --pin "$PIN_W" --fixtures-root "$ROOT_W" --harness-pin "$HPIN_W" \
    --out "$OUT_W" --condition "$CONDITION" --seed "$SEED" --negatives "$NEGATIVES_W") || exit $?
  rc=0
  (cd "NativeBridge~" && ./build-windows/bin/vosk-bridge-harness.exe --model "$OUT_W/model" \
    --fixtures "$OUT_W/cond" --manifest "$OUT_W/cond/pin.json" \
    --dump "$OUT_W/cond.jsonl") >"$OUT/cond.json" 2>"$OUT/cond.err" || rc=$?
  if [ "$rc" -ne 0 ] && [ "$rc" -ne 1 ]; then
    cat "$OUT/cond.err" >&2
    die "harness on cond/ exit $rc: no report"
  fi
  COND_EXIT="$rc"
fi

if [ -n "$PACED" ]; then
  pass 16384 matched
  MATCHED_EXIT="$rc"
  (cd "$OUT/stage" && dotnet run --no-build -- --bench matched-check --dump "$OUT_W/dump.jsonl" \
    --manifest "$MANIFEST_W" --pin "$PIN_W" --fixtures-root "$ROOT_W" \
    --matched "$OUT_W/matched.jsonl" --pace 16384) || exit $?
  pass "$PACE" paced
  PACED_EXIT="$rc"
fi

(cd "$OUT/stage" && dotnet run --no-build -- --bench run --dump "$OUT_W/dump.jsonl" \
  --manifest "$MANIFEST_W" --pin "$PIN_W" --fixtures-root "$ROOT_W" \
  --harness-exit "$HARNESS_EXIT" --report "$OUT_W/report.md" \
  ${NAMED:+--profile "$PROFILE" --mode "$MODE"} \
  ${PACED:+--matched "$OUT_W/matched.jsonl" --matched-exit "$MATCHED_EXIT" --paced "$OUT_W/paced.jsonl" --paced-exit "$PACED_EXIT" --pace "$PACE"} \
  ${CONDITION:+--cond-dump "$OUT_W/cond.jsonl" --cond-manifest "$OUT_W/cond/manifest.json" --conditions "$OUT_W/cond/conditions.json" --cond-exit "$COND_EXIT" --condition "$CONDITION" --seed "$SEED"}) || exit $?

echo "$OUT_W/report.md"
exit 0
