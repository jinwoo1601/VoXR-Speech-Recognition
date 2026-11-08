#!/usr/bin/env python3
# =============================================================================
# Purpose:  Synthesize the TTS fixture corpus from the phrase manifest, and write its speech-boundary truth (--truth)
# Layer:    Tests.Fixtures
# Owns:     TruthStop (exception)
# Depends:  piper-tts, soxr, numpy (venv-installed by generate.sh) to regenerate; --truth the standard library only
# =============================================================================
# Pipeline per utterance entry (design §6.2): piper TTS -> resample to 48 kHz
# (soxr) -> peak-normalize into the Quest 3 mic range (0.04-0.4) -> pad lead/
# tail silence -> 16-bit mono WAV. Split-command entries synthesize segments
# and join them with a silence gap; silence entries emit digital silence.
# Piper synthesis is not bit-deterministic across runs (noise inputs), so
# regeneration is functionally reproducible, not byte-identical; the committed
# WAVs are the regression baseline.

import argparse
import json
import math
import struct
import subprocess
import sys
import tempfile
import wave
import zlib
from pathlib import Path

try:
    import numpy as np
    import soxr
except ImportError:
    np = soxr = None

TARGET_RATE = 48000
LEAD_SILENCE_S = 0.3
TAIL_SILENCE_S = 1.0
DEFAULT_PEAK = 0.2
DEFAULT_GAP_S = 0.7
DEFAULT_SILENCE_S = 3.0
MIN_PEAK, MAX_PEAK = 0.04, 0.4
TRIM_FRAME_S = 0.010
TRIM_DBFS = -40.0
TRUTH_FLOOR = math.ceil(32767 * 10 ** (TRIM_DBFS / 20))


class TruthStop(Exception):
    pass


def synth(piper_bin, voice, data_dir, text, tmp_path):
    subprocess.run(
        [piper_bin, "-m", voice, "--data-dir", str(data_dir), "-f", str(tmp_path)],
        input=text.encode(), check=True, capture_output=True)
    with wave.open(str(tmp_path), "rb") as w:
        if w.getnchannels() != 1 or w.getsampwidth() != 2:
            sys.exit(f"piper produced unexpected format for '{text}'")
        rate = w.getframerate()
        data = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16)
    samples = data.astype(np.float32) / 32768.0
    if rate != TARGET_RATE:
        samples = soxr.resample(samples, rate, TARGET_RATE)
    return samples


def scale_to_peak(samples, peak):
    if not (MIN_PEAK <= peak <= MAX_PEAK):
        sys.exit(f"peak {peak} outside the Quest mic range [{MIN_PEAK}, {MAX_PEAK}]")
    m = float(np.max(np.abs(samples)))
    if m <= 0.0:
        sys.exit("cannot peak-scale silence — check the TTS output")
    return samples * (peak / m)


def trim_edges(samples):
    frame = int(round(TRIM_FRAME_S * TARGET_RATE))
    n = len(samples) // frame
    frames = samples[: n * frame].reshape(n, frame)
    rms = np.sqrt(np.mean(frames * frames, axis=1))
    with np.errstate(divide="ignore"):
        dbfs = 20.0 * np.log10(rms)
    idx = np.flatnonzero(dbfs >= TRIM_DBFS)
    if idx.size == 0:
        sys.exit(f"no frame at or above {TRIM_DBFS} dBFS — check the TTS output")
    return samples[int(idx[0]) * frame:(int(idx[-1]) + 1) * frame]


def silence(seconds):
    return np.zeros(int(seconds * TARGET_RATE), dtype=np.float32)


def build_case(case, piper_bin, voice, data_dir, tmp_path):
    peak = case.get("peak", DEFAULT_PEAK)
    if "segments" in case and case["segments"]:
        gap = case.get("gapSeconds", DEFAULT_GAP_S)
        parts = []
        for i, seg in enumerate(case["segments"]):
            if i > 0:
                parts.append(silence(gap))
            seg_samples = scale_to_peak(
                synth(piper_bin, voice, data_dir, seg, tmp_path), peak)
            if case.get("category") == "ladder":
                seg_samples = trim_edges(seg_samples)
            parts.append(seg_samples)
        speech = np.concatenate(parts)
    elif case.get("phrase"):
        speech = scale_to_peak(
            synth(piper_bin, voice, data_dir, case["phrase"], tmp_path), peak)
    else:
        if "leadSilenceSeconds" in case or "noiseFloorDbfs" in case:
            sys.exit(f"{case['file']}: a silence entry takes neither "
                     "leadSilenceSeconds nor noiseFloorDbfs")
        return silence(case.get("silenceSeconds", DEFAULT_SILENCE_S))

    lead = case.get("leadSilenceSeconds", LEAD_SILENCE_S)
    x = np.concatenate([silence(lead), speech, silence(TAIL_SILENCE_S)])
    if "noiseFloorDbfs" not in case:
        return x
    seed = zlib.crc32(Path(case["file"]).name.encode("utf-8"))
    noise = np.random.default_rng(seed).standard_normal(len(x))
    noise *= 10 ** (case["noiseFloorDbfs"] / 20) / np.sqrt(np.mean(noise * noise))
    x = (x + noise).astype(np.float32)
    m = float(np.max(np.abs(x)))
    if not (MIN_PEAK <= m <= MAX_PEAK):
        sys.exit(f"{case['file']}: noisy peak {m} outside the Quest mic range "
                 f"[{MIN_PEAK}, {MAX_PEAK}]")
    return x


def write_wav(path, samples):
    samples = np.clip(samples, -1.0, 1.0)
    pcm = np.round(samples * 32767.0).astype(np.int16)
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(TARGET_RATE)
        w.writeframes(pcm.tobytes())


def read_pcm(path):
    with wave.open(str(path), "rb") as w:
        if (w.getnchannels() != 1 or w.getsampwidth() != 2
                or w.getframerate() != TARGET_RATE):
            raise TruthStop("not 16-bit mono 48000 Hz")
        n = w.getnframes()
        frames = w.readframes(n)
    return struct.unpack("<%dh" % n, frames)


def speech_truth(case, root):
    if case.get("segments"):
        n = len(case["segments"])
    elif case.get("phrase"):
        n = 1
    else:
        return []
    pcm = read_pcm(root / case["file"])
    lead = int(case.get("leadSilenceSeconds", LEAD_SILENCE_S) * TARGET_RATE)
    tail = int(TAIL_SILENCE_S * TARGET_RATE)
    gap = int(case.get("gapSeconds", DEFAULT_GAP_S) * TARGET_RATE)
    if lead >= len(pcm) - tail:
        raise TruthStop(f"lead {lead} + tail {tail} leaves no block in {len(pcm)} samples")
    for i in range(lead):
        if abs(pcm[i]) >= TRUTH_FLOOR:
            raise TruthStop(f"a sample at or above the floor {TRUTH_FLOOR} in the lead at {i}")
    above = [i for i in range(lead, len(pcm) - tail) if abs(pcm[i]) >= TRUTH_FLOOR]
    if not above:
        raise TruthStop(f"no sample at or above the floor {TRUTH_FLOOR} in the block")
    runs = []
    if n > 1:
        runs = [(p, q) for p, q in zip(above, above[1:]) if q - p - 1 >= gap]
        if len(runs) != n - 1:
            raise TruthStop(f"{n} segments need exactly {n - 1} sub-floor runs of "
                            f"at least {gap} samples; found {len(runs)}")
    spans = []
    start = above[0]
    for p, q in runs:
        spans.append((start, p + 1))
        start = q
    spans.append((start, above[-1] + 1))
    return spans


def write_truth(manifest_path, root):
    text = manifest_path.read_bytes().decode("utf-8")
    eol = "\r\n" if "\r\n" in text else "\n"
    manifest = json.loads(text)
    truths = []
    refused = 0
    for case in manifest["cases"]:
        try:
            truths.append((case["file"], speech_truth(case, root)))
        except TruthStop as stop:
            print(f"truth: {case['file']}: {stop}", file=sys.stderr)
            refused += 1
    if refused:
        sys.exit(f"truth: {refused} case(s) refused; {manifest_path} not written")

    file_key = " " * 12 + '"file": '
    speech_key = " " * 12 + '"speech": '
    mismatch = f"truth: {manifest_path}: file lines do not match the cases; not written"
    lines = []
    matched = 0
    for line in text.split(eol):
        if line.startswith(speech_key):
            continue
        lines.append(line)
        if line.startswith(file_key):
            if (matched >= len(truths)
                    or line != file_key + json.dumps(truths[matched][0]) + ","):
                sys.exit(mismatch)
            spans = ", ".join(f'{{ "start": {s}, "end": {e} }}'
                              for s, e in truths[matched][1])
            lines.append(speech_key + "[" + spans + "],")
            matched += 1
    if matched != len(truths):
        sys.exit(mismatch)
    manifest_path.write_bytes(eol.join(lines).encode("utf-8"))
    print(f"truth: {len(truths)} case(s) written to {manifest_path}")


def main():
    ap = argparse.ArgumentParser(description="Regenerate the TTS fixture corpus.")
    ap.add_argument("--manifest", required=True)
    ap.add_argument("--fixtures-root", required=True,
                    help="Directory the manifest 'file' paths are relative to")
    ap.add_argument("--voice", default="en_US-lessac-medium")
    ap.add_argument("--data-dir", help="piper voice data dir")
    ap.add_argument("--piper-bin", default="piper")
    ap.add_argument("--only", default=None,
                    help="generate only entries whose 'file' contains this substring")
    ap.add_argument("--truth", action="store_true")
    args = ap.parse_args()

    if args.truth:
        write_truth(Path(args.manifest), Path(args.fixtures_root))
        return
    if args.data_dir is None:
        ap.error("the following arguments are required: --data-dir")
    if np is None:
        sys.exit("regeneration needs numpy and soxr: run generate.sh")

    manifest = json.loads(Path(args.manifest).read_text())
    root = Path(args.fixtures_root)
    tmp_path = Path(tempfile.gettempdir()) / "voxr_tts_tmp.wav"

    generated = 0
    try:
        for case in manifest["cases"]:
            rel = case["file"]
            if args.only and args.only not in rel:
                continue
            samples = build_case(case, args.piper_bin, args.voice,
                                 Path(args.data_dir), tmp_path)
            write_wav(root / rel, samples)
            peak = float(np.max(np.abs(samples)))
            print(f"  {rel}  ({len(samples) / TARGET_RATE:.2f}s, peak {peak:.3f})")
            generated += 1
    finally:
        tmp_path.unlink(missing_ok=True)

    if generated == 0:
        sys.exit(f"no manifest entry matched --only {args.only}")
    print(f"{generated} fixture(s) written under {root}")
    write_truth(Path(args.manifest), root)


if __name__ == "__main__":
    main()
