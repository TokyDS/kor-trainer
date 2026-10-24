"""Voices the same texts as generate_melo.py with Microsoft's SunHi voice (Edge read-aloud) into Opus files.

For personal use only: the voice comes from an online service whose terms do not allow sharing the audio, so
the output must never be committed or published. MeloTTS dropped many word-final consonants (한국 sounded like
한고); SunHi says them.

    python tools/tts/generate_edge.py              # resumable; needs edge-tts (pip install edge-tts) and ffmpeg

Output: artifacts/tts/ko-sunhi/<key>.opus and manifest.json, in the same format as generate_melo.py. A lone
syllable that is pronounced as another one (꽃 [꼳], 앉 [안]) plays that syllable's recording, as
alias_syllables.py does for MeloTTS, so only ~3,200 of the 11,172 syllables are requested.
"""
from __future__ import annotations

import argparse
import asyncio
import json
import pathlib
import subprocess
import tempfile
import time

import edge_tts

from alias_syllables import FIRST, LAST, pronounced
from generate_melo import BITRATE, ROOT, SPEED, collect, key_of

OUT = ROOT / "artifacts" / "tts" / "ko-sunhi"
VOICE = "ko-KR-SunHiNeural"
RATE = f"{round((SPEED - 1) * 100):+d}%"   # -10%: the same pace as the MeloTTS clips
ATTEMPTS = 5


def is_syllable(text: str) -> bool:
    return len(text) == 1 and FIRST <= ord(text) <= LAST


async def voice(text: str, gate: asyncio.Semaphore) -> bool:
    target = OUT / f"{key_of(text)}.opus"
    if target.exists():
        return True
    async with gate:
        for attempt in range(ATTEMPTS):
            with tempfile.TemporaryDirectory() as tmp:
                mp3 = pathlib.Path(tmp) / "speech.mp3"
                try:
                    await edge_tts.Communicate(text, VOICE, rate=RATE).save(str(mp3))
                    partial = target.with_suffix(".part.opus")
                    process = await asyncio.create_subprocess_exec(
                        "ffmpeg", "-loglevel", "error", "-y", "-i", str(mp3), "-ac", "1", "-ar", "24000",
                        "-c:a", "libopus", "-b:a", BITRATE, str(partial))
                    if await process.wait() != 0:
                        raise RuntimeError("ffmpeg failed")
                    partial.rename(target)
                    return True
                except Exception as error:  # network hiccups, throttling, an empty answer now and then: back off and retry
                    if attempt == ATTEMPTS - 1:
                        print(f"failed: {text!r}: {error}", flush=True)
                        return False
                    await asyncio.sleep(2 ** attempt * 3)
    return False


def write_manifest(texts: list[str]) -> int:
    items = {t: key_of(t) for t in texts if (OUT / f"{key_of(t)}.opus").exists()}
    for text in texts:
        if is_syllable(text) and text not in items and pronounced(text) in items:
            items[text] = items[pronounced(text)]
    (OUT / "manifest.json").write_text(json.dumps(
        {"voice": f"Microsoft {VOICE} (personal use only)", "speed": SPEED, "items": items},
        ensure_ascii=False, indent=0), encoding="utf-8")
    return len(items)


async def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--only", choices=["words", "sentences", "hangul", "grammar"])
    parser.add_argument("--limit", type=int)
    parser.add_argument("--concurrency", type=int, default=4)
    args = parser.parse_args()

    OUT.mkdir(parents=True, exist_ok=True)
    texts = collect(args.only)[: args.limit]
    wanted = [t for t in texts if not is_syllable(t) or pronounced(t) == t]
    todo = [t for t in wanted if not (OUT / f"{key_of(t)}.opus").exists()]
    print(f"{len(texts)} texts, {len(wanted)} to voice, {len(todo)} left", flush=True)

    gate = asyncio.Semaphore(args.concurrency)
    started, done, failed = time.time(), 0, 0
    for start in range(0, len(todo), 200):
        results = await asyncio.gather(*(voice(t, gate) for t in todo[start:start + 200]))
        done += len(results)
        failed += results.count(False)
        rate = done / (time.time() - started)
        print(f"{done}/{len(todo)} · {failed} failed · ~{(len(todo) - done) / rate / 60:.0f} min left", flush=True)
        if done % 2000 < 200:
            write_manifest(collect(None))
    print(f"manifest: {write_manifest(collect(None))} texts → {OUT / 'manifest.json'}")


if __name__ == "__main__":
    asyncio.run(main())
