"""Gives voiced single syllables that end in a stop ([ㄱ], [ㄷ], [ㅂ]) an abrupt end.

MeloTTS lets the vowel of a lone syllable fade out, so 뻑 sounds like 뻐: an unreleased final stop is
heard only as the vowel stopping short. This cuts each such syllable where its level first falls 12 dB
below the peak, with a 6 ms fade, and drops the long silence after it.

Run after generate_melo.py: python3 tools/tts/sharpen_stops.py
Originals are kept in artifacts/tts/ko-melo-original/; processed keys are listed in sharpened.json, so a
second run only handles new files.
"""
from __future__ import annotations

import concurrent.futures
import json
import math
import os
import pathlib
import shutil
import struct
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[2]
SPEECH = ROOT / "artifacts" / "tts" / "ko-melo"
ORIGINALS = ROOT / "artifacts" / "tts" / "ko-melo-original"
DONE = SPEECH / "sharpened.json"

FIRST_SYLLABLE, LAST_SYLLABLE = 0xAC00, 0xD7A3
FINALS_PER_MEDIAL = 28
# Finals whose sound at the end of a syllable is a stop: ㄱ ㄲ ㄳ ㄷ ㄺ ㄿ ㅂ ㅄ ㅅ ㅆ ㅈ ㅊ ㅋ ㅌ ㅍ ㅎ.
STOP_FINALS = {1, 2, 3, 7, 9, 14, 17, 18, 19, 20, 22, 23, 24, 25, 26, 27}

RATE = 24000
FRAME = RATE // 200  # 5 ms
DROP_DB = 12
FADE_S = 0.006
ONSET_KEEP_FRAMES = 4  # 20 ms before the sound starts
PAD_S = 0.15
MIN_KEPT_S = 0.08  # anything shorter means the detection failed; leave the file alone


def is_stop_syllable(text: str) -> bool:
    if len(text) != 1 or not FIRST_SYLLABLE <= ord(text) <= LAST_SYLLABLE:
        return False
    return (ord(text) - FIRST_SYLLABLE) % FINALS_PER_MEDIAL in STOP_FINALS


def samples_of(path: pathlib.Path) -> list[int]:
    raw = subprocess.run(
        ["ffmpeg", "-loglevel", "error", "-i", str(path), "-f", "s16le", "-ac", "1", "-ar", str(RATE), "-"],
        capture_output=True, check=True).stdout
    return list(struct.unpack(f"<{len(raw) // 2}h", raw))


def sharpen(key: str) -> tuple[str, str]:
    path = SPEECH / f"{key}.opus"
    original = ORIGINALS / path.name
    if not original.exists():
        shutil.copy2(path, original)
    samples = samples_of(original)
    levels = [math.sqrt(sum(s * s for s in samples[i:i + FRAME]) / FRAME) for i in range(0, len(samples) - FRAME, FRAME)]
    if not levels or max(levels) == 0:
        return key, "silent"
    peak_at = max(range(len(levels)), key=levels.__getitem__)
    db = [20 * math.log10(max(level, 1e-6) / levels[peak_at]) for level in levels]
    onset = next(i for i, d in enumerate(db) if d > -30)
    cut = next((i for i in range(peak_at, len(db)) if db[i] < -DROP_DB), len(db) - 1)
    start_s = max(0, onset - ONSET_KEEP_FRAMES) * FRAME / RATE
    cut_s = cut * FRAME / RATE
    if cut_s - start_s < MIN_KEPT_S:
        return key, "skipped"
    partial = path.with_suffix(".part.opus")
    subprocess.run(
        ["ffmpeg", "-loglevel", "error", "-y", "-i", str(original), "-map_metadata", "-1", "-af",
         f"atrim={start_s:.3f}:{cut_s:.3f},asetpts=PTS-STARTPTS,"
         f"afade=t=out:st={cut_s - start_s - FADE_S:.3f}:d={FADE_S},apad=pad_dur={PAD_S}",
         "-c:a", "libopus", "-b:a", "24k", "-ac", "1", str(partial)],
        check=True)
    partial.replace(path)
    return key, "sharpened"


def main() -> None:
    items: dict[str, str] = json.loads((SPEECH / "manifest.json").read_text(encoding="utf-8"))["items"]
    done: set[str] = set(json.loads(DONE.read_text(encoding="utf-8"))) if DONE.exists() else set()
    todo = sorted({key for text, key in items.items()
                   if is_stop_syllable(text) and key not in done and (SPEECH / f"{key}.opus").exists()})
    ORIGINALS.mkdir(parents=True, exist_ok=True)
    print(f"{len(todo)} syllables to sharpen ({len(done)} done before)", flush=True)

    counts: dict[str, int] = {}
    with concurrent.futures.ProcessPoolExecutor(max_workers=os.cpu_count() or 4) as pool:
        for key, result in pool.map(sharpen, todo, chunksize=32):
            counts[result] = counts.get(result, 0) + 1
            if result == "sharpened":
                done.add(key)
    DONE.write_text(json.dumps(sorted(done)), encoding="utf-8")
    print(", ".join(f"{k} {v}" for k, v in sorted(counts.items())) or "nothing to do")


if __name__ == "__main__":
    main()
