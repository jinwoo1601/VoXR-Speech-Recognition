#!/usr/bin/env bash
# =============================================================================
# Purpose:  Bootstrap the Kokoro toolchain and render the bench's voiced set, or render the Piper stand-in (--stand-in) or self-check (--self-check)
# Layer:    Tests.Fixtures
# Owns:     (script, no public types)
# Depends:  generate.sh's Piper venv and voice (--stand-in, --self-check); Python >=3.10,<3.13 via VOXR_PYTHON, py -3, python3 or python for the Kokoro venv (venv-installed: kokoro, soxr, numpy)
# =============================================================================
set -euo pipefail

usage() {
    echo "usage: voices.sh [--stand-in] [--root DIR] [--voices all|ID,...] | --self-check [--live]" >&2
    exit 2
}

STAND_IN=
SELF_CHECK=
LIVE=
ROOT=
VOICES=
while [ $# -gt 0 ]; do
    case "$1" in
        --stand-in) STAND_IN=1 ;;
        --self-check) SELF_CHECK=1 ;;
        --live) LIVE=1 ;;
        --root) [ $# -ge 2 ] || usage; ROOT="$2"; shift ;;
        --voices) [ $# -ge 2 ] || usage; VOICES="$2"; shift ;;
        *) usage ;;
    esac
    shift
done
if [ -n "$LIVE" ] && [ -z "$SELF_CHECK" ]; then usage; fi
if [ -n "$SELF_CHECK" ] && { [ -n "$STAND_IN" ] || [ -n "$ROOT" ] || [ -n "$VOICES" ]; }; then usage; fi
if [ -n "$STAND_IN" ] && [ -z "$ROOT" ]; then
    echo "voices.sh: --stand-in needs --root" >&2
    exit 2
fi
if [ -z "$SELF_CHECK" ] && [ -z "$STAND_IN" ] && [ -z "$ROOT" ]; then
    ROOT="${XDG_CACHE_HOME:-$HOME/.cache}/voxr-bench"
fi
if [ -n "$ROOT" ]; then
    if command -v cygpath >/dev/null 2>&1; then ROOT="$(cygpath -am "$ROOT")"; else ROOT="$(realpath -m "$ROOT")"; fi
fi
cd "$(dirname "$0")"

CACHE="${XDG_CACHE_HOME:-$HOME/.cache}/voxr-fixture-gen"
if command -v cygpath >/dev/null 2>&1; then CACHE="$(cygpath -m "$CACHE")"; fi
VOICE=en_US-lessac-medium
DATA_DIR="$CACHE/voices"

if [ -z "$LIVE" ] && { [ -n "$SELF_CHECK" ] || [ -n "$STAND_IN" ]; }; then
    VENV="$CACHE/venv"
    if [ -d "$VENV/Scripts" ]; then VBIN="$VENV/Scripts"; EXE=.exe; else VBIN="$VENV/bin"; EXE=; fi
    if [ ! -x "$VBIN/piper$EXE" ] || [ ! -e "$DATA_DIR/$VOICE.onnx" ]; then
        echo "voices.sh: run generate.sh first" >&2
        exit 2
    fi
    if [ -n "$SELF_CHECK" ]; then
        "$VBIN/python$EXE" -B voices.py --self-check
    else
        "$VBIN/python$EXE" -B voices.py --engine piper --root "$ROOT" --voices "${VOICES:-all}" \
            --piper-bin "$VBIN/piper$EXE" --data-dir "$DATA_DIR"
    fi
    exit 0
fi

KVENV="$CACHE/kokoro/venv"
MODEL="$CACHE/kokoro/model"
PYOK='import sys; sys.exit(not ((3, 10) <= sys.version_info[:2] < (3, 13)))'
if [ -d "$KVENV/Scripts" ]; then KBIN="$KVENV/Scripts"; EXE=.exe; else KBIN="$KVENV/bin"; EXE=; fi
if [ ! -x "$KBIN/python$EXE" ]; then
    if [ -n "${VOXR_PYTHON:-}" ]; then
        PY="$VOXR_PYTHON"
        if ! $PY -c "$PYOK" >/dev/null 2>&1; then
            echo "voices.sh: VOXR_PYTHON=$VOXR_PYTHON does not run as Python >=3.10,<3.13 (it is split on spaces, so a path containing spaces cannot be used)" >&2
            exit 2
        fi
    else
        PY=""
        for cand in "py -3" python3 python; do
            PY="$cand"
            if $PY -c "$PYOK" >/dev/null 2>&1; then break; fi
            PY=""
        done
        if [ -z "${PY:-}" ]; then
            echo "voices.sh: no Python >=3.10,<3.13 found (tried: VOXR_PYTHON unset, py -3, python3, python)" >&2
            exit 2
        fi
    fi
    echo "Bootstrapping Kokoro venv with $PY ($($PY --version 2>&1))..."
    $PY -m venv "$KVENV"
    if [ -d "$KVENV/Scripts" ]; then KBIN="$KVENV/Scripts"; EXE=.exe; else KBIN="$KVENV/bin"; EXE=; fi
    "$KBIN/pip$EXE" install --quiet "kokoro==0.9.4" "soxr==1.1.0" numpy
fi

"$KBIN/python$EXE" -B -c '
import os, sys, voices
from huggingface_hub import hf_hub_download
for f in ["config.json", "kokoro-v1_0.pth"] + ["voices/%s.pt" % i for i in voices.IDS]:
    if not os.path.exists(os.path.join(sys.argv[1], f)):
        print("Downloading %s..." % f)
        hf_hub_download("hexgrad/Kokoro-82M", f, local_dir=sys.argv[1])
' "$MODEL"

if [ -n "$LIVE" ]; then
    HF_HUB_OFFLINE=1 "$KBIN/python$EXE" -B voices.py --self-check --live
else
    HF_HUB_OFFLINE=1 "$KBIN/python$EXE" -B voices.py --engine kokoro --root "$ROOT" \
        --voices "${VOICES:-all}" --model-dir "$MODEL"
fi
