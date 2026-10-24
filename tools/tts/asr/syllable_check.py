"""Checks every voiced single syllable with an espeak phoneme recognizer (argv: out.jsonl).

A syllable is flagged when the recognizer hears more vowel nuclei than the syllable has (an extra syllable,
as 뽆 → "b ɛ l k ə") or none at all. Unreleased finals are often not heard, so finals are not judged here.
"""
import json
import os
import pathlib
import subprocess
import sys

import numpy as np
import torch
from huggingface_hub import snapshot_download
from transformers import Wav2Vec2FeatureExtractor, Wav2Vec2ForCTC

SPEECH = pathlib.Path(__file__).resolve().parents[3] / "artifacts" / "tts" / "ko-melo"
VOWEL_CHARS = set("aeiouɑɛəɯʌɔæɪʊyøœɐɨʉɒɤ")
TWO_PART_MEDIALS = {19}  # ㅢ is heard as ɯ + i

MODEL = snapshot_download("facebook/wav2vec2-xlsr-53-espeak-cv-ft", allow_patterns=["*.json", "pytorch_model.bin"])
vocab = json.loads(pathlib.Path(MODEL, "vocab.json").read_text(encoding="utf-8"))
id_to_token = {i: t for t, i in vocab.items()}
extractor = Wav2Vec2FeatureExtractor.from_pretrained(MODEL)
model = Wav2Vec2ForCTC.from_pretrained(MODEL).eval()
torch.set_num_threads(int(os.environ.get("ASR_THREADS", "2")))


def phonemes(path: pathlib.Path) -> list[str]:
    raw = subprocess.run(["ffmpeg", "-loglevel", "error", "-i", str(path), "-f", "f32le", "-ac", "1", "-ar", "16000", "-"],
                         capture_output=True, check=True).stdout
    inputs = extractor(np.frombuffer(raw, dtype=np.float32), sampling_rate=16000, return_tensors="pt")
    with torch.no_grad():
        ids = torch.argmax(model(inputs.input_values).logits, dim=-1)[0].tolist()
    out, last = [], None
    for i in ids:
        token = id_to_token.get(i)
        if i != last and token not in ("<pad>", "<s>", "</s>", "<unk>"):
            out.append(token)
        last = i
    return out


def main() -> None:
    out = pathlib.Path(sys.argv[1])
    items = json.loads((SPEECH / "manifest.json").read_text(encoding="utf-8"))["items"]
    syllables = [t for t in items if len(t) == 1 and 0xAC00 <= ord(t) <= 0xD7A3]
    done = {json.loads(l)["text"] for l in out.read_text(encoding="utf-8").splitlines()} if out.exists() else set()
    todo = [t for t in syllables if t not in done]
    print(f"{len(todo)} syllables to check", flush=True)
    with out.open("a", encoding="utf-8") as log:
        for n, text in enumerate(todo, 1):
            heard = phonemes(SPEECH / f"{items[text]}.opus")
            vowels = sum(1 for p in heard if any(c in VOWEL_CHARS for c in p))
            medial = (ord(text) - 0xAC00) % 588 // 28
            allowed = 2 if medial in TWO_PART_MEDIALS else 1
            log.write(json.dumps({"text": text, "key": items[text], "heard": " ".join(heard), "vowels": vowels,
                                  "flagged": vowels == 0 or vowels > allowed}, ensure_ascii=False) + "\n")
            if n % 500 == 0:
                log.flush()
                print(f"{n}/{len(todo)}", flush=True)


if __name__ == "__main__":
    main()
