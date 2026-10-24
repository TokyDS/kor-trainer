"""Checks variants with Whisper about ten clips at a time (ASR venv).

Whisper always encodes a 30-second window, so a one-second word costs as much as a full window. Clips are
packed into one window with a second of silence between them, and each word Whisper hears is given back to
the clip its timestamp falls in. Rounds go v1, v2, v3: a text gets its next variant only if the previous
one failed; round v0 hears the original take, which stays when it passes. Resumable through batch-clips.jsonl; results are appended to the recheck results file.

usage: batch_recheck.py flagged.json results.jsonl          # finish the recheck
       batch_recheck.py flagged.json results.jsonl compare  # agreement with results already in the file
"""
import json
import os
import pathlib
import sys

import numpy as np

from asr_check import RATE, audio, ok
from revoice import OUT, SPEECH, candidates
from thermal import cool_down

WINDOW = 27.0   # seconds of speech and gaps per window (Whisper hears up to 30)
GAP = 1.0
CLIPS = pathlib.Path(__file__).resolve().parents[3] / "artifacts" / "tts" / "asr" / "batch-clips.jsonl"


def take(key: str, variant: int) -> pathlib.Path:
    return SPEECH / "ko-melo" / f"{key}.opus" if variant == 0 else OUT / f"{key}-v{variant}.opus"


def windows(clips: list[tuple[str, int, np.ndarray]]):
    batch, length = [], 0.0
    for clip in clips:
        seconds = len(clip[2]) / RATE + GAP
        if batch and length + seconds > WINDOW:
            yield batch
            batch, length = [], 0.0
        batch.append(clip)
        length += seconds
    if batch:
        yield batch


def transcribe(whisper, batch) -> list[str]:
    silence = np.zeros(int(GAP * RATE), dtype=np.float32)
    pieces, spans, at = [], [], 0.0
    for _, _, wave in batch:
        spans.append((at, at + len(wave) / RATE))
        pieces += [wave, silence]
        at += len(wave) / RATE + GAP
    segments, _ = whisper.transcribe(np.concatenate(pieces), language="ko", beam_size=1, temperature=0.0,
                                     condition_on_previous_text=False, word_timestamps=True, vad_filter=False)
    heard = [""] * len(batch)
    for segment in segments:
        for word in segment.words or []:
            middle = (word.start + word.end) / 2
            index = min(range(len(spans)), key=lambda i: 0 if spans[i][0] <= middle <= spans[i][1]
                        else min(abs(middle - spans[i][0]), abs(middle - spans[i][1])))
            heard[index] += word.word
    return [h.strip() for h in heard]


def main() -> None:
    flagged = json.loads(pathlib.Path(sys.argv[1]).read_text(encoding="utf-8"))
    results = pathlib.Path(sys.argv[2])
    compare = len(sys.argv) > 3
    rows = {r["text"]: r for r in candidates(flagged)}
    finished = [json.loads(l) for l in results.read_text(encoding="utf-8").splitlines()] if results.exists() else []
    checked = {(c["text"], c["variant"]): c for c in map(json.loads, CLIPS.read_text(encoding="utf-8").splitlines())} \
        if CLIPS.exists() and not compare else {}

    from faster_whisper import WhisperModel
    whisper = WhisperModel("large-v3-turbo", device="cpu", compute_type="int8",
                           cpu_threads=int(os.environ.get("ASR_THREADS", "2")))

    if compare:
        sample = [r for r in finished][:60]
        clips = [(r["text"], 1, audio(OUT / f"{r['key']}-v1.opus")) for r in sample]
        agree = 0
        for batch in windows(clips):
            cool_down()
            for (text, _, _), heard in zip(batch, transcribe(whisper, batch)):
                single = next(r for r in sample if r["text"] == text)["heard"][0]
                same = ok(text, heard) == ok(text, single)
                agree += same
                if not same:
                    print(f"differs: {text}: one by one {single!r}, packed {heard!r}", flush=True)
        print(f"agreement {agree}/{len(clips)}", flush=True)
        return

    done = {r["text"] for r in finished}
    pending = [t for t in rows if t not in done]
    print(f"{len(pending)} texts to recheck in windows", flush=True)
    CLIPS.parent.mkdir(parents=True, exist_ok=True)
    with CLIPS.open("a", encoding="utf-8") as log:
        for variant in (0, 1, 2, 3):
            passed = {t for (t, v), c in checked.items() if c["ok"]}
            todo = [t for t in pending if t not in passed and (t, variant) not in checked
                    and take(rows[t]["key"], variant).exists()]
            print(f"round v{variant}: {len(todo)} clips", flush=True)
            clips = [(t, variant, audio(take(rows[t]["key"], variant))) for t in todo]
            for n, batch in enumerate(windows(clips), 1):
                cool_down()
                for (text, v, _), heard in zip(batch, transcribe(whisper, batch)):
                    clip = {"text": text, "variant": v, "heard": heard, "ok": ok(text, heard)}
                    checked[(text, v)] = clip
                    log.write(json.dumps(clip, ensure_ascii=False) + "\n")
                log.flush()
                if n % 10 == 0:
                    print(f"v{variant}: {n} windows", flush=True)

    with results.open("a", encoding="utf-8") as out:
        for text in pending:
            heard, best = [], None
            for variant in (0, 1, 2, 3):
                clip = checked.get((text, variant))
                if clip is None:
                    continue
                heard.append(clip["heard"])
                if clip["ok"]:
                    best = variant
                    break
            out.write(json.dumps({"text": text, "key": rows[text]["key"], "was": rows[text]["heard"],
                                  "heard": heard, "best": best}, ensure_ascii=False) + "\n")
    print("all done", flush=True)


if __name__ == "__main__":
    main()
