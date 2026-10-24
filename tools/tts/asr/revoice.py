"""Voices each flagged text again in three variants (run in the MeloTTS venv; argv: flagged.json).

v1: the text with a final period (sentence-final intonation), v2 and v3: other random draws of the voice.
Variants land in artifacts/tts/ko-melo-variants/<key>-v<n>.opus; existing files are kept (resumable).
"""
import json
import os
import pathlib
import re
import subprocess
import sys
import tempfile

import torch

from thermal import cool_down

SPEECH = pathlib.Path(__file__).resolve().parents[3] / "artifacts" / "tts"
OUT = SPEECH / "ko-melo-variants"
SPEED = 0.9
VARIANTS = [(1, True, 0.6), (2, False, 0.6), (3, False, 0.4)]  # (seed, final period, noise scale)
# Whisper writes numbers as digits and loanwords in Latin (사월 → 4월, 티브이 → TV): nothing to fix there.
WHISPER_STYLE = re.compile(r"[0-9A-Za-z]")


def candidates(flagged: dict) -> list[dict]:
    return [r for rows in flagged.values() for r in rows if not WHISPER_STYLE.search(r["heard"] or "")]


def main() -> None:
    torch.set_num_threads(int(os.environ.get("ASR_THREADS", "2")))
    from melo.api import TTS
    model = TTS(language="KR", device="cpu")
    speaker = model.hps.data.spk2id["KR"]
    OUT.mkdir(exist_ok=True)
    todo = candidates(json.loads(pathlib.Path(sys.argv[1]).read_text(encoding="utf-8")))
    print(f"{len(todo)} texts to voice again", flush=True)
    for n, row in enumerate(todo, 1):
        cool_down()
        text = row["text"]
        for number, (seed, period, noise) in enumerate(VARIANTS, 1):
            cool_down()
            target = OUT / f"{row['key']}-v{number}.opus"
            if target.exists():
                continue
            spoken = text if not period or text[-1] in ".?!" else text + "."
            torch.manual_seed(seed)
            with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp:
                wav = tmp.name
            try:
                model.tts_to_file(spoken, speaker, wav, speed=SPEED, noise_scale=noise, quiet=True)
                subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-i", wav, "-ac", "1", "-ar", "24000",
                                "-c:a", "libopus", "-b:a", "24k", str(target)], check=True)
            except Exception as error:  # one bad text must not stop the run
                print(f"failed: {text!r}: {error}", flush=True)
            finally:
                os.unlink(wav)
        if n % 50 == 0:
            print(f"{n}/{len(todo)}", flush=True)


if __name__ == "__main__":
    main()
