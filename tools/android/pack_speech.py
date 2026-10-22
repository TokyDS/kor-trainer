"""Prepares the speech bundled in the APK from artifacts/tts/ko-melo into artifacts/tts/ko-melo-apk.

Single syllables and words keep their quality (24 kbit/s): they carry the sounds a learner must tell apart
(달/탈/딸). Phrases and sentences (anything with a space) are re-encoded at 16 kbit/s, which keeps speech
clear and saves about a third of their size. The server keeps serving the originals.

Incremental: only new or changed files are processed; files no longer in the manifest are removed.
Run from anywhere: python3 tools/android/pack_speech.py
"""
import concurrent.futures
import json
import os
import pathlib
import shutil
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
SOURCE = ROOT / "artifacts" / "tts" / "ko-melo"
TARGET = ROOT / "artifacts" / "tts" / "ko-melo-apk"
PHRASE_BITRATE = "16k"


def is_phrase(text: str) -> bool:
    return " " in text.strip()


def pack(text: str, key: str) -> str:
    src = SOURCE / f"{key}.opus"
    dst = TARGET / f"{key}.opus"
    if not src.exists():
        return "missing"
    if dst.exists() and dst.stat().st_mtime >= src.stat().st_mtime:
        return "kept"
    if dst.exists():
        dst.unlink()
    if not is_phrase(text):
        try:
            os.link(src, dst)
        except OSError:
            shutil.copy2(src, dst)
        return "linked"
    partial = dst.with_suffix(".part.opus")
    subprocess.run(
        ["ffmpeg", "-loglevel", "error", "-y", "-i", str(src), "-map_metadata", "-1",
         "-c:a", "libopus", "-b:a", PHRASE_BITRATE, "-application", "voip", "-ac", "1", str(partial)],
        check=True)
    partial.replace(dst)
    return "encoded"


def main() -> None:
    manifest_path = SOURCE / "manifest.json"
    if not manifest_path.exists():
        print("no speech in artifacts/tts/ko-melo — nothing to pack", file=sys.stderr)
        return
    items = json.loads(manifest_path.read_text(encoding="utf-8"))["items"]
    TARGET.mkdir(parents=True, exist_ok=True)

    counts: dict[str, int] = {}
    with concurrent.futures.ThreadPoolExecutor(max_workers=os.cpu_count() or 4) as pool:
        for result in pool.map(lambda item: pack(*item), items.items()):
            counts[result] = counts.get(result, 0) + 1

    wanted = {f"{key}.opus" for key in items.values()}
    stale = [f for f in TARGET.glob("*.opus") if f.name not in wanted]
    for f in stale:
        f.unlink()
    shutil.copy2(manifest_path, TARGET / "manifest.json")

    size = sum(f.stat().st_size for f in TARGET.glob("*.opus"))
    print(f"packed {len(items)} texts ({', '.join(f'{k} {v}' for k, v in sorted(counts.items()))}; "
          f"removed {len(stale)}) → {size / 1048576:.1f} MB in {TARGET}")


if __name__ == "__main__":
    main()
