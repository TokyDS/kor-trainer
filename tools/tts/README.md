# Neural speech (MeloTTS)

Deck words, Tatoeba example sentences and reading texts are voiced once with
[MeloTTS](https://github.com/myshell-ai/MeloTTS) (Korean, MIT licence) and played back instead of the
device's speech synthesis. Native-speaker recordings (Lingua Libre) still come first; the device's voice
is only used for text that was not generated.

## Generate

MeloTTS needs Python 3.11 (its dependencies do not build on newer versions), PyTorch for CPU and
`ffmpeg` with libopus:

```bash
uv python install 3.11 && uv venv -p 3.11 .venv-tts
VIRTUAL_ENV=.venv-tts uv pip install torch torchaudio --index-url https://download.pytorch.org/whl/cpu
VIRTUAL_ENV=.venv-tts uv pip install "melotts @ https://github.com/myshell-ai/MeloTTS/archive/refs/heads/main.zip" python-mecab-ko
.venv-tts/bin/python -m unidic download

.venv-tts/bin/python tools/tts/generate_melo.py --only words       # ~5,900 words, about an hour on 16 cores
.venv-tts/bin/python tools/tts/generate_melo.py --only sentences   # ~5,000 sentences, a few hours
```

Run from a directory without your own `coverage.py`/`*.py` that shadow library modules. The script is
resumable: it skips files that exist and rewrites `manifest.json` at the end.

Output goes to `artifacts/tts/ko-melo/` (git-ignored): `<sha1-prefix>.opus` (mono, 24 kHz, 24 kbit/s,
generated at speed 0.9) and `manifest.json` mapping each text to its file. About 5 KB per word and
12 KB per sentence.

## Use

- **Web**: `Audio:SynthesizedDirectory` points at the folder (by default `../../artifacts/tts/ko-melo`
  relative to `src/KorTrainer.Api`); `GET /api/audio/tts?text=` serves a file. In Docker mount it and set
  `Audio__SynthesizedDirectory`.
- **Android**: `tools/android/build-apk.sh` bundles the folder as app assets (`tts/…`) when it exists.

The page plays the audio at `rate / 0.9`, so the speech-rate setting and the slow listening mode apply
to it as well.
