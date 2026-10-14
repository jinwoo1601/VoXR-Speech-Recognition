#!/usr/bin/env bash
# =============================================================================
# Purpose:  Bootstrap the TTS toolchain and regenerate the fixture corpus
# Layer:    Tests.Fixtures
# Owns:     (script, no public types)
# Depends:  Python 3 via VOXR_PYTHON, py -3, python3 or python (everything else is venv-installed: piper-tts, soxr, numpy)
# =============================================================================
# Usage: ./generate.sh              # regenerate the whole corpus
#        ./generate.sh --only NAME  # regenerate entries whose file matches NAME
# Runs in bash on Linux or in Git Bash on Windows, with Python 3 and network
# on the first run (pip packages, the voice). In Git Bash the cache paths reach
# Python as C:/... (cygpath -m), so MSYS_NO_PATHCONV set or unset makes no
# difference. Generated WAVs are committed, so running this is only needed to
# add or change fixtures.
# VOXR_PYTHON is a command line split on spaces, as "py -3" is: it may name a
# launcher with arguments ("py -3.12") but not a path containing spaces.
set -euo pipefail
cd "$(dirname "$0")"

# Toolchain lives in the user cache, NOT inside the fixtures tree — Unity
# imports this folder during host-project test runs.
CACHE="${XDG_CACHE_HOME:-$HOME/.cache}/voxr-fixture-gen"
if command -v cygpath >/dev/null 2>&1; then CACHE="$(cygpath -m "$CACHE")"; fi
VENV="$CACHE/venv"
VOICE=en_US-lessac-medium
DATA_DIR="$CACHE/voices"

if [ -d "$VENV/Scripts" ]; then VBIN="$VENV/Scripts"; EXE=.exe; else VBIN="$VENV/bin"; EXE=; fi
if [ ! -x "$VBIN/piper$EXE" ]; then
    if [ -n "${VOXR_PYTHON:-}" ]; then
        PY="$VOXR_PYTHON"
        if ! $PY -c 'import sys; sys.exit(sys.version_info[0] != 3)' >/dev/null 2>&1; then
            echo "generate.sh: VOXR_PYTHON=$VOXR_PYTHON does not run as Python 3 (it is split on spaces, so a path containing spaces cannot be used)" >&2
            exit 1
        fi
    else
        PY=""
        for cand in "py -3" python3 python; do
            PY="$cand"
            if $PY -c 'import sys; sys.exit(sys.version_info[0] != 3)' >/dev/null 2>&1; then break; fi
            PY=""
        done
        if [ -z "${PY:-}" ]; then
            echo "generate.sh: no Python 3 found (tried: VOXR_PYTHON unset, py -3, python3, python)" >&2
            exit 1
        fi
    fi
    echo "Bootstrapping venv with $PY ($($PY --version 2>&1))..."
    $PY -m venv "$VENV"
    if [ -d "$VENV/Scripts" ]; then VBIN="$VENV/Scripts"; EXE=.exe; else VBIN="$VENV/bin"; EXE=; fi
    "$VBIN/pip$EXE" install --quiet "piper-tts==1.6.0" "soxr==1.1.0" numpy
fi

if [ ! -e "$DATA_DIR/$VOICE.onnx" ]; then
    echo "Downloading voice $VOICE..."
    mkdir -p "$DATA_DIR"
    "$VBIN/python$EXE" -m piper.download_voices "$VOICE" --data-dir "$DATA_DIR"
fi

"$VBIN/python$EXE" generate.py \
    --manifest audio/manifest.json \
    --fixtures-root . \
    --voice "$VOICE" \
    --data-dir "$DATA_DIR" \
    --piper-bin "$VBIN/piper$EXE" \
    "$@"
