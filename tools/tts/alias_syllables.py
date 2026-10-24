"""Points each lone syllable that is pronounced as another syllable at that syllable's recording.

A lone 뽆 is said [뽄], 꽃 [꼳], 앉 [안]: the final becomes one of the seven representative sounds. MeloTTS
does not know the rare spellings (a speech recognizer heard 뽆 as "b ɛ l k ə"), but voices the plain
syllables well, so manifest.json maps such a syllable to the recording of its pronunciation. Run after
generate_melo.py and sharpen_stops.py: python3 tools/tts/alias_syllables.py
"""
from __future__ import annotations

import json
import pathlib

ROOT = pathlib.Path(__file__).resolve().parents[2]
SPEECH = ROOT / "artifacts" / "tts" / "ko-melo"

FIRST, LAST = 0xAC00, 0xD7A3
FINALS = " ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ"
# The sound a final has at the end of a lone syllable (표준 발음법 9–11항; 밟 [밥] is the one exception).
SOUND = {"ㄱ": "ㄱ", "ㄲ": "ㄱ", "ㄳ": "ㄱ", "ㄺ": "ㄱ", "ㅋ": "ㄱ", "ㄴ": "ㄴ", "ㄵ": "ㄴ", "ㄶ": "ㄴ",
         "ㄷ": "ㄷ", "ㅅ": "ㄷ", "ㅆ": "ㄷ", "ㅈ": "ㄷ", "ㅊ": "ㄷ", "ㅌ": "ㄷ", "ㅎ": "ㄷ",
         "ㄹ": "ㄹ", "ㄼ": "ㄹ", "ㄽ": "ㄹ", "ㄾ": "ㄹ", "ㅀ": "ㄹ", "ㅁ": "ㅁ", "ㄻ": "ㅁ",
         "ㅂ": "ㅂ", "ㅍ": "ㅂ", "ㅄ": "ㅂ", "ㄿ": "ㅂ", "ㅇ": "ㅇ"}


def pronounced(syllable: str) -> str:
    if syllable == "밟":
        return "밥"
    code = ord(syllable) - FIRST
    final = FINALS[code % 28]
    if final == " ":
        return syllable
    return chr(FIRST + code - code % 28 + FINALS.index(SOUND[final]))


def main() -> None:
    path = SPEECH / "manifest.json"
    manifest = json.loads(path.read_text(encoding="utf-8"))
    items: dict[str, str] = manifest["items"]
    aliased = 0
    for text in list(items):
        if len(text) != 1 or not FIRST <= ord(text) <= LAST:
            continue
        spoken = pronounced(text)
        if spoken != text and spoken in items and items[text] != items[spoken]:
            items[text] = items[spoken]
            aliased += 1
    path.write_text(json.dumps(manifest, ensure_ascii=False, indent=0), encoding="utf-8")
    print(f"{aliased} syllables now play the recording of their pronunciation")


if __name__ == "__main__":
    main()
