"""Plays a better take for each clip a speech recognizer heard wrongly.

Clips that Whisper did not hear as their text were voiced again in a few variants
(artifacts/tts/ko-melo-variants/<key>-v<n>.opus); the first variant Whisper heard correctly is listed in
chosen.json there as {text: "<key>-v<n>"}. This script copies each chosen variant into ko-melo/ under its own
hash and points manifest.json at it, so cached copies of the old take are not reused.

    python3 tools/tts/apply_revoiced.py [recheck-results.jsonl ...]   # merges results into chosen.json first

Run after generate_melo.py, sharpen_stops.py and alias_syllables.py: generation rewrites these entries.
"""
from __future__ import annotations

import hashlib
import json
import pathlib
import shutil
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
SPEECH = ROOT / "artifacts" / "tts" / "ko-melo"
VARIANTS = ROOT / "artifacts" / "tts" / "ko-melo-variants"


def merge(results: list[pathlib.Path], chosen: dict[str, str]) -> None:
    for path in results:
        for line in path.read_text(encoding="utf-8").splitlines():
            row = json.loads(line)
            if row["best"]:
                chosen[row["text"]] = f"{row['key']}-v{row['best']}"


def main() -> None:
    chosen_path = VARIANTS / "chosen.json"
    chosen: dict[str, str] = json.loads(chosen_path.read_text(encoding="utf-8")) if chosen_path.exists() else {}
    merge([pathlib.Path(p) for p in sys.argv[1:]], chosen)
    chosen_path.write_text(json.dumps(chosen, ensure_ascii=False, indent=0), encoding="utf-8")

    manifest_path = SPEECH / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    items: dict[str, str] = manifest["items"]
    applied = 0
    for text, variant in chosen.items():
        source = VARIANTS / f"{variant}.opus"
        if text not in items or not source.exists():
            continue
        key = hashlib.sha1(f"{text}#{variant}".encode("utf-8")).hexdigest()[:16]
        target = SPEECH / f"{key}.opus"
        if not target.exists():
            shutil.copyfile(source, target)
        items[text] = key
        applied += 1
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=0), encoding="utf-8")
    print(f"{applied} texts now play a take the recognizer heard correctly")


if __name__ == "__main__":
    main()
