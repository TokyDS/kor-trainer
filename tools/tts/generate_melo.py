"""Voices the deck, example sentences and reading texts with MeloTTS (Korean) into Opus files.

Usage (from the repository root, inside a Python 3.11 venv with MeloTTS — see tools/tts/README.md):

    python tools/tts/generate_melo.py                      # everything, resumable
    python tools/tts/generate_melo.py --only words --limit 50 --workers 4

Output: artifacts/tts/ko-melo/<key>.opus and manifest.json mapping each text to its file. Existing files
are kept, so an interrupted run continues where it stopped.
"""
import argparse
import hashlib
import json
import multiprocessing as mp
import os
import pathlib
import subprocess
import tempfile
import time

ROOT = pathlib.Path(__file__).resolve().parents[2]
SEED = ROOT / "src" / "KorTrainer.Infrastructure" / "Seed"
OUT = ROOT / "artifacts" / "tts" / "ko-melo"

SPEED = 0.9          # the trainer speaks a little slower than normal; "slow" playback slows it further
BITRATE = "24k"      # mono speech in Opus stays clear at 24 kbit/s


def key_of(text: str) -> str:
    return hashlib.sha1(text.encode("utf-8")).hexdigest()[:16]


def collect(only: str | None) -> list[str]:
    texts: list[str] = []
    if only in (None, "words"):
        for name in ("words.json", "words-nikl.json"):
            deck = json.loads((SEED / name).read_text(encoding="utf-8"))
            texts += [w[0].strip() for s in deck["sections"] for w in s["words"]]
    if only in (None, "sentences"):
        texts += [s["ko"].strip() for s in json.loads((SEED / "sentences-tatoeba.json").read_text(encoding="utf-8"))["sentences"]]
        texts += [s["ko"].strip() for t in json.loads((SEED / "texts.json").read_text(encoding="utf-8"))["texts"] for s in t["sentences"]]
    if only in (None, "hangul"):
        # Every syllable (the Hangul drills build random ones) plus the module's own words, exported by
        # `dotnet run tools/tts/ExportHangulTexts.cs`.
        texts += [chr(code) for code in range(0xAC00, 0xD7A4)]
        extra = ROOT / "artifacts" / "tts" / "hangul-texts.txt"
        if extra.exists():
            texts += [line.strip() for line in extra.read_text(encoding="utf-8").splitlines()]
    if only in (None, "grammar"):
        # Verb forms the grammar drills speak, exported by `dotnet run tools/tts/ExportGrammarTexts.cs`.
        extra = ROOT / "artifacts" / "tts" / "grammar-texts.txt"
        if extra.exists():
            texts += [line.strip() for line in extra.read_text(encoding="utf-8").splitlines()]
    seen: set[str] = set()
    return [t for t in texts if t and not (t in seen or seen.add(t))]


_model = None
_speaker = None


def _init(threads: int) -> None:
    global _model, _speaker
    import torch
    torch.set_num_threads(threads)
    from melo.api import TTS
    _model = TTS(language="KR", device="cpu")
    _speaker = _model.hps.data.spk2id["KR"]


def _voice(text: str) -> tuple[str, bool]:
    target = OUT / f"{key_of(text)}.opus"
    if target.exists():
        return text, True
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp:
        wav = tmp.name
    try:
        _model.tts_to_file(text, _speaker, wav, speed=SPEED, quiet=True)
        partial = target.with_suffix(".part.opus")
        subprocess.run(
            ["ffmpeg", "-loglevel", "error", "-y", "-i", wav, "-ac", "1", "-ar", "24000", "-c:a", "libopus", "-b:a", BITRATE, str(partial)],
            check=True)
        partial.rename(target)
        return text, True
    except Exception as error:  # one bad text must not stop a multi-hour run
        print(f"failed: {text!r}: {error}", flush=True)
        return text, False
    finally:
        os.unlink(wav)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--only", choices=["words", "sentences", "hangul", "grammar"])
    parser.add_argument("--limit", type=int)
    parser.add_argument("--workers", type=int, default=max(1, (os.cpu_count() or 4) // 4))
    parser.add_argument("--threads", type=int, default=4, help="torch threads per worker")
    args = parser.parse_args()

    OUT.mkdir(parents=True, exist_ok=True)
    texts = collect(args.only)[: args.limit]
    todo = [t for t in texts if not (OUT / f"{key_of(t)}.opus").exists()]
    print(f"{len(texts)} texts, {len(todo)} to voice with {args.workers}×{args.threads} threads", flush=True)

    started, done = time.time(), 0
    # One text per task and recycled workers: a long run once stalled with a few chunks left.
    with mp.Pool(args.workers, initializer=_init, initargs=(args.threads,), maxtasksperchild=500) as pool:
        for _, ok in pool.imap_unordered(_voice, todo, chunksize=1):
            done += 1
            if done % 100 == 0 or done == len(todo):
                rate = done / (time.time() - started)
                print(f"{done}/{len(todo)} · {rate:.1f}/s · ~{(len(todo) - done) / rate / 60:.0f} min left", flush=True)

    manifest_path = OUT / "manifest.json"
    items = json.loads(manifest_path.read_text(encoding="utf-8"))["items"] if manifest_path.exists() else {}
    items.update({t: key_of(t) for t in collect(None) if (OUT / f"{key_of(t)}.opus").exists()})
    manifest_path.write_text(json.dumps(
        {"voice": "MeloTTS KR (MIT, https://github.com/myshell-ai/MeloTTS)", "speed": SPEED, "items": items},
        ensure_ascii=False, indent=0), encoding="utf-8")
    print(f"manifest: {len(items)} texts → {manifest_path}")


if __name__ == "__main__":
    main()
