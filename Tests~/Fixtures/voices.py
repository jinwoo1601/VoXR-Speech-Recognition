#!/usr/bin/env python3
# =============================================================================
# Purpose:  Rendering the bench's voiced set into the add-on cache with Kokoro or the Piper stand-in, and its known-value self-check (--self-check)
# Layer:    Tests.Fixtures
# Owns:     (script, no public types)
# Depends:  generate (build_case, write_wav, read_pcm, speech_truth, synth, silence, TruthStop); numpy, soxr; kokoro, misaki for --engine kokoro and --live (venv-installed by voices.sh)
# =============================================================================

import argparse
import hashlib
import importlib.metadata
import json
import shutil
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import generate
import numpy as np
import soxr

IDS = ["af_heart", "af_bella", "af_nicole", "af_sarah", "bf_emma", "bf_isabella",
       "am_michael", "am_fenrir", "am_puck", "bm_george", "bm_fable", "bm_lewis"]
PIPER_VOICE = "en_US-lessac-medium"
KOKORO_SOURCE = "huggingface.co/hexgrad/Kokoro-82M (packs by inference)"
KOKORO_RATE = 24000


def unspoken(tokens):
    return [text for text, phonemes in tokens
            if any(ch.isalnum() for ch in text) and (not phonemes or "❓" in phonemes)]


def gpl_dists(names):
    return [n for n in names if "espeak" in n.lower() or "phonemizer" in n.lower()]


def pick(spec):
    if spec == "all":
        return list(IDS)
    named = []
    for x in spec.split(","):
        if x not in IDS:
            raise ValueError(f'voices: unknown voice "{x}": valid: {", ".join(IDS)}')
        if x in named:
            raise ValueError(f'voices: voice "{x}" named twice')
        named.append(x)
    return [vid for vid in IDS if vid in named]


def voiced_set():
    corpus = json.loads((HERE / "audio" / "manifest.json").read_text(encoding="utf-8"))
    negatives = json.loads(
        (HERE / "audio" / "negatives" / "manifest.json").read_text(encoding="utf-8"))
    positives = [c for c in corpus["cases"]
                 if c.get("category") not in ("pause-lead", "ladder")
                 and (c.get("phrase") or c.get("segments"))]
    return positives, negatives["cases"]


def stem(case):
    return Path(case["file"]).name.removesuffix(".wav")


def texts(case):
    return case["segments"] if case.get("segments") else [case["phrase"]]


def lang(vid):
    return "a" if vid.startswith(("af_", "am_")) else "b"


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def stop(message):
    print(message, file=sys.stderr)
    sys.exit(2)


def g2p(code):
    from misaki import en
    return en.G2P(trf=False, british=code == "b", fallback=None)


def heard(convert, text):
    return unspoken([(t.text, t.phonemes) for t in convert(text)[1]])


def clip(case, path, spans):
    entry = {k: case[k] for k in ("file", "phrase", "segments", "gapSeconds", "peak") if k in case}
    entry["speech"] = [{"start": s, "end": e} for s, e in spans]
    entry["realizedPeak"] = round(max(abs(v) for v in generate.read_pcm(path)) / 32767, 6)
    entry["sha256"] = sha256(path)
    return entry


def render(vid, cases, speak, root, head):
    voices = root / "voices"
    partial = voices / f".{vid}.partial"
    shutil.rmtree(partial, ignore_errors=True)
    partial.mkdir(parents=True)
    try:
        clips = []
        for case in cases:
            name = stem(case) + ".wav"
            generate.write_wav(partial / name, generate.build_case(case, speak))
            try:
                spans = generate.speech_truth(dict(case, file=name), partial)
            except generate.TruthStop as refusal:
                sys.exit(f"voices: {vid}: {name}: {refusal}")
            clips.append(clip(case, partial / name, spans))
        text = json.dumps(dict(head, clips=clips), indent=2, ensure_ascii=False) + "\n"
        (partial / "voice.json").write_bytes(text.encode("utf-8"))
    except SystemExit:
        shutil.rmtree(partial, ignore_errors=True)
        raise
    target = voices / vid
    if target.exists():
        shutil.rmtree(target)
    partial.rename(target)
    digests = "\n".join(c["sha256"] for c in clips)
    print(f"voices: {vid}: {len(clips)} clips, "
          f"render {hashlib.sha256(digests.encode('utf-8')).hexdigest()[:12]}")


def kokoro_speaker(vid, pipeline, pack):
    def speak(text):
        results = list(pipeline(text, voice=str(pack)))
        if len(results) != 1:
            sys.exit(f'voices: {vid}: "{text}": split into {len(results)} chunks')
        return soxr.resample(results[0].audio.numpy(), KOKORO_RATE, generate.TARGET_RATE)
    return speak


def render_kokoro(ids, cases, root, model_dir):
    gpl = gpl_dists([d.metadata["Name"] or "" for d in importlib.metadata.distributions()])
    if gpl:
        stop(f"voices: GPL fallback installed: {', '.join(gpl)}")
    config = model_dir / "config.json"
    weights = model_dir / "kokoro-v1_0.pth"
    packs = {vid: model_dir / "voices" / f"{vid}.pt" for vid in ids}
    for path in [config, weights, *packs.values()]:
        if not path.is_file():
            stop(f"voices: missing file: {path}")
    langs = sorted({lang(vid) for vid in ids})
    refused = 0
    for code in langs:
        convert = g2p(code)
        for case in cases:
            for text in texts(case):
                words = heard(convert, text)
                if words:
                    print(f'voices: {code}: "{text}": no phonemes for: {", ".join(words)}',
                          file=sys.stderr)
                    refused += 1
    if refused:
        sys.exit(1)
    from kokoro import KModel, KPipeline
    model = KModel(config=str(config), model=str(weights))
    pipelines = {code: KPipeline(lang_code=code, model=model) for code in langs}
    head = {"licence": "Apache-2.0", "licenceSource": KOKORO_SOURCE, "engine": "kokoro",
            "kokoroVersion": importlib.metadata.version("kokoro"),
            "misakiVersion": importlib.metadata.version("misaki"),
            "modelSha256": sha256(weights)}
    for vid in ids:
        render(vid, cases, kokoro_speaker(vid, pipelines[lang(vid)], packs[vid]), root,
               {"id": vid, **head, "voicePackSha256": sha256(packs[vid])})


def render_piper(ids, cases, root, piper_bin, data_dir):
    model = data_dir / f"{PIPER_VOICE}.onnx"
    for path in (Path(piper_bin), model):
        if not path.is_file():
            stop(f"voices: missing file: {path}")
    head = {"licence": "stand-in, not a Kokoro voice", "licenceSource": f"{PIPER_VOICE} (Piper)",
            "engine": "stand-in", "kokoroVersion": None, "misakiVersion": None,
            "modelSha256": sha256(model), "voicePackSha256": None}
    with tempfile.TemporaryDirectory() as d:
        tmp_path = Path(d) / "voxr_tts_tmp.wav"
        speak = lambda text: generate.synth(piper_bin, PIPER_VOICE, data_dir, text, tmp_path)
        for vid in ids:
            render(vid, cases, speak, root, {"id": vid, **head})


def toy_truth(case):
    speak = lambda text: np.full(4800, 0.5, dtype=np.float32)
    with tempfile.TemporaryDirectory() as d:
        generate.write_wav(Path(d) / case["file"], generate.build_case(case, speak))
        return generate.speech_truth(case, Path(d))


def refusal_text(spec):
    try:
        return pick(spec)
    except ValueError as refusal:
        return str(refusal)


def self_check(live):
    L = int(generate.LEAD_SILENCE_S * generate.TARGET_RATE)
    G = len(generate.silence(0.7))
    positives, negatives = voiced_set()
    toys = [
        ("unspoken open", lambda: unspoken([("open", "ˈOpən")]), []),
        ("unspoken None", lambda: unspoken([("zorblax", None)]), ["zorblax"]),
        ("unspoken empty", lambda: unspoken([("zorblax", "")]), ["zorblax"]),
        ("unspoken unknown mark", lambda: unspoken([("zorblax", "❓")]), ["zorblax"]),
        ("unspoken punctuation", lambda: unspoken([(",", None)]), []),
        ("gpl espeak and phonemizer",
         lambda: gpl_dists(["numpy", "espeakng-loader", "phonemizer-fork"]),
         ["espeakng-loader", "phonemizer-fork"]),
        ("gpl none", lambda: gpl_dists(["misaki"]), []),
        ("gpl case-blind", lambda: gpl_dists(["EspeakNG"]), ["EspeakNG"]),
        ("truth phrase",
         lambda: toy_truth({"file": "toy.wav", "phrase": "toy", "peak": 0.2}),
         [(L, L + 4800)]),
        ("truth split",
         lambda: toy_truth({"file": "toy.wav", "segments": ["a", "b"], "gapSeconds": 0.7,
                            "peak": 0.2}),
         [(L, L + 4800), (L + 4800 + G, L + 9600 + G)]),
        ("voiced set",
         lambda: (len(positives), len(negatives),
                  len({stem(c) for c in positives + negatives})),
         (15, 16, 31)),
        ("voices all", lambda: pick("all"), IDS),
        ("voices order", lambda: pick("bf_emma,af_heart"), ["af_heart", "bf_emma"]),
        ("voices repeat", lambda: refusal_text("af_heart,af_heart"),
         'voices: voice "af_heart" named twice'),
        ("voices unknown", lambda: refusal_text("xx"),
         f'voices: unknown voice "xx": valid: {", ".join(IDS)}'),
    ]
    if live:
        for code in ("a", "b"):
            toys.append((f"live G2P {code}",
                         lambda code=code: heard(g2p(code), "open zorblax fire"),
                         ["zorblax"]))
    passed = 0
    for n, (name, run, want) in enumerate(toys, 1):
        got = run()
        if got == want:
            passed += 1
            print(f"PASS {n} {name}")
        else:
            print(f"FAIL {n} {name}: got {got} want {want}")
    print(f"{passed}/{len(toys)} voices checks verified against known-value toys.")
    return 0 if passed == len(toys) else 1


def main():
    ap = argparse.ArgumentParser(description="Render the bench's voiced set into the add-on cache.")
    ap.add_argument("--self-check", action="store_true")
    ap.add_argument("--live", action="store_true")
    ap.add_argument("--engine", choices=["kokoro", "piper"])
    ap.add_argument("--root")
    ap.add_argument("--voices", default="all")
    ap.add_argument("--model-dir")
    ap.add_argument("--piper-bin")
    ap.add_argument("--data-dir")
    args = ap.parse_args()

    if args.self_check:
        if args.engine or args.root:
            ap.error("--self-check takes only --live")
        sys.exit(self_check(args.live))
    if args.live:
        ap.error("--live needs --self-check")
    if args.engine is None or args.root is None:
        ap.error("the following arguments are required: --engine, --root")
    if args.engine == "kokoro" and args.model_dir is None:
        ap.error("--engine kokoro needs --model-dir")
    if args.engine == "piper" and (args.piper_bin is None or args.data_dir is None):
        ap.error("--engine piper needs --piper-bin and --data-dir")
    try:
        ids = pick(args.voices)
    except ValueError as refusal:
        stop(str(refusal))
    positives, negatives = voiced_set()
    if args.engine == "kokoro":
        render_kokoro(ids, positives + negatives, Path(args.root), Path(args.model_dir))
    else:
        render_piper(ids, positives + negatives, Path(args.root), args.piper_bin,
                     Path(args.data_dir))


if __name__ == "__main__":
    main()
