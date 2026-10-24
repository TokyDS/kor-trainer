"""Checks MeloTTS clips with two speech recognizers and flags texts both of them hear wrongly.

usage: asr_check.py pilot|all out.jsonl [limit]
Each line of out.jsonl: text, key, kind, w2v (wav2vec2 transcript), whisper, ok_w2v, ok_whisper.
"""
from __future__ import annotations

import json
import os
import pathlib
import random
import subprocess
import sys

import numpy as np

REPO = pathlib.Path(__file__).resolve().parents[3]
SPEECH = REPO / "artifacts" / "tts" / "ko-melo"
SEED = REPO / "src" / "KorTrainer.Infrastructure" / "Seed"
RATE = 16000

FIRST = 0xAC00
FINALS = " ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ"
# Final as heard at the end of a lone syllable (the seven representative sounds).
SOUND = {"ㄱ": "ㄱ", "ㄲ": "ㄱ", "ㄳ": "ㄱ", "ㄺ": "ㄱ", "ㅋ": "ㄱ", "ㄴ": "ㄴ", "ㄵ": "ㄴ", "ㄶ": "ㄴ",
         "ㄷ": "ㄷ", "ㅅ": "ㄷ", "ㅆ": "ㄷ", "ㅈ": "ㄷ", "ㅊ": "ㄷ", "ㅌ": "ㄷ", "ㅎ": "ㄷ",
         "ㄹ": "ㄹ", "ㄼ": "ㄹ", "ㄽ": "ㄹ", "ㄾ": "ㄹ", "ㅀ": "ㄹ", "ㅁ": "ㅁ", "ㄻ": "ㅁ",
         "ㅂ": "ㅂ", "ㅍ": "ㅂ", "ㅄ": "ㅂ", "ㄿ": "ㅂ", "ㅇ": "ㅇ"}


def hangul_only(text: str) -> str:
    return "".join(c for c in text if FIRST <= ord(c) <= 0xD7A3)


def pronounced_syllable(s: str) -> str:
    code = ord(s) - FIRST
    final = FINALS[code % 28]
    if final == " ":
        return s
    return chr(FIRST + code - code % 28 + FINALS.index(SOUND[final]))


def accepted(text: str) -> set[str]:
    plain = hangul_only(text)
    return {plain, pronounced_syllable(plain)} if len(plain) == 1 else {plain}


def cer(a: str, b: str) -> float:
    if not b:
        return 1.0 if a else 0.0
    prev = list(range(len(b) + 1))
    for i, ca in enumerate(a, 1):
        cur = [i]
        for j, cb in enumerate(b, 1):
            cur.append(min(prev[j] + 1, cur[j - 1] + 1, prev[j - 1] + (ca != cb)))
        prev = cur
    return prev[-1] / len(b)


def ok(text: str, heard: str) -> bool:
    heard = hangul_only(heard)
    targets = accepted(text)
    if len(hangul_only(text)) == 1:
        return heard in targets
    return min(cer(heard, t) for t in targets) <= 0.25


def audio(path: pathlib.Path) -> np.ndarray:
    raw = subprocess.run(["ffmpeg", "-loglevel", "error", "-i", str(path), "-f", "f32le", "-ac", "1", "-ar", str(RATE), "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32)


def kinds() -> dict[str, str]:
    words = {w[0].strip() for n in ("words.json", "words-nikl.json")
             for s in json.loads((SEED / n).read_text(encoding="utf-8"))["sections"] for w in s["words"]}
    sentences = {s["ko"].strip() for s in json.loads((SEED / "sentences-tatoeba.json").read_text(encoding="utf-8"))["sentences"]}
    sentences |= {s["ko"].strip() for s in json.loads((SEED / "grammar-sentences.json").read_text(encoding="utf-8"))["sentences"]}

    def kind(t: str) -> str:
        if len(t) == 1:
            return "syllable"
        if t in words:
            return "word"
        if t in sentences:
            return "sentence"
        return "phrase"
    items = json.loads((SPEECH / "manifest.json").read_text(encoding="utf-8"))["items"]
    return {t: kind(t) for t in items}


def main() -> None:
    mode, out = sys.argv[1], pathlib.Path(sys.argv[2])
    items = json.loads((SPEECH / "manifest.json").read_text(encoding="utf-8"))["items"]
    by_kind = kinds()
    texts = list(items)
    if mode == "words":
        # Single syllables are judged by syllable_check.py; Whisper only hears words and phrases well.
        texts = [t for t in texts if by_kind[t] != "syllable"]
    if mode == "pilot":
        rng = random.Random(7)
        pick = lambda k, n: rng.sample([t for t in texts if by_kind[t] == k], n)
        texts = ["뽆", "뻖", "앉", "닭"] + pick("syllable", 150) + pick("word", 150) + pick("phrase", 80) + pick("sentence", 40)
    done = set()
    if out.exists():
        done = {json.loads(line)["text"] for line in out.read_text(encoding="utf-8").splitlines() if line.strip()}
    texts = [t for t in texts if t not in done]
    if len(sys.argv) > 3:
        texts = texts[: int(sys.argv[3])]
    print(f"{len(texts)} clips to check", flush=True)

    import torch
    from faster_whisper import WhisperModel
    from transformers import Wav2Vec2ForCTC, Wav2Vec2Processor

    torch.set_num_threads(int(os.environ.get("ASR_THREADS", "2")))
    # The Korean wav2vec2 proved unreliable on synthesized speech; it stays off unless asked for.
    use_w2v = os.environ.get("ASR_W2V", "0") == "1"
    processor = Wav2Vec2Processor.from_pretrained("kresnik/wav2vec2-large-xlsr-korean") if use_w2v else None
    w2v = Wav2Vec2ForCTC.from_pretrained("kresnik/wav2vec2-large-xlsr-korean").eval() if use_w2v else None
    # Whisper is optional (ASR_WHISPER=0): wav2vec2 alone already judges single syllables well.
    whisper = WhisperModel("large-v3-turbo", device="cpu", compute_type="int8", cpu_threads=int(os.environ.get("ASR_THREADS", "2"))) \
        if os.environ.get("ASR_WHISPER", "1") == "1" else None

    with out.open("a", encoding="utf-8") as log:
        for n, text in enumerate(texts, 1):
            wave = audio(SPEECH / f"{items[text]}.opus")
            heard_w2v = ""
            if use_w2v:
                inputs = processor(wave, sampling_rate=RATE, return_tensors="pt")
                with torch.no_grad():
                    ids = torch.argmax(w2v(inputs.input_values).logits, dim=-1)
                heard_w2v = processor.batch_decode(ids)[0]
            heard_whisper = None
            if whisper is not None:
                segments, _ = whisper.transcribe(wave, language="ko", beam_size=1, temperature=0.0,
                                                 condition_on_previous_text=False, without_timestamps=True,
                                                 vad_filter=False)
                heard_whisper = "".join(s.text for s in segments).strip()
            row = {"text": text, "key": items[text], "kind": by_kind[text], "w2v": heard_w2v, "whisper": heard_whisper,
                   "ok_w2v": ok(text, heard_w2v), "ok_whisper": None if heard_whisper is None else ok(text, heard_whisper)}
            log.write(json.dumps(row, ensure_ascii=False) + "\n")
            if n % 50 == 0:
                log.flush()
                print(f"{n}/{len(texts)}", flush=True)


if __name__ == "__main__":
    main()
