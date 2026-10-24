# Neural speech (MeloTTS)

Deck words, Tatoeba example sentences, reading texts, every Hangul syllable and the verb forms of the
grammar drills are voiced once with
[MeloTTS](https://github.com/myshell-ai/MeloTTS) (Korean, MIT licence) and played back instead of the
device's speech synthesis. Native-speaker recordings (Lingua Libre) are only played when the learner
turns them on in the settings; the device's voice is only used for text that was not generated.

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

dotnet run tools/tts/ExportHangulTexts.cs                          # letter samples, minimal pairs
.venv-tts/bin/python tools/tts/generate_melo.py --only hangul      # + all 11,172 syllables

dotnet run tools/tts/ExportGrammarTexts.cs                         # ~4,000 verb forms of the first stages
.venv-tts/bin/python tools/tts/generate_melo.py --only grammar
```

Run one generation at a time: each run merges its texts into `manifest.json` when it finishes.

After a run that adds syllables, `python3 tools/tts/sharpen_stops.py` gives every lone syllable that ends in a
stop ([ㄱ], [ㄷ], [ㅂ]: 각, 꽃, 밥, 닭 …) an abrupt end. MeloTTS lets such a vowel fade, so 뻑 sounded like 뻐;
the final of an unreleased stop is heard only as the vowel stopping short.

Then `python3 tools/tts/alias_syllables.py`: a lone syllable that is pronounced as another one (뽆 [뽄], 꽃
[꼳], 앉 [안], 밟 [밥]) plays that syllable's recording. MeloTTS garbles many rare spellings (a phoneme
recognizer heard 뽆 as "b ɛ l k ə") but voices the plain ones well. generate_melo.py rewrites these entries,
so run both steps again after every generation, and `apply_revoiced.py` after them (see below). Originals stay in
`artifacts/tts/ko-melo-original/`, processed keys in `sharpened.json`, and a second run only takes new files.

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

## Check with speech recognition

`tools/tts/asr/` checks the generated clips with speech recognizers and re-voices the ones that sound wrong.
It needs a second Python 3.11 venv with `torch` (CPU), `transformers`, `faster-whisper` and `numpy`; the models
are `facebook/wav2vec2-xlsr-53-espeak-cv-ft` (phonemes, for single syllables) and Whisper `large-v3-turbo`.

1. `syllable_check.py out.jsonl` flags syllables in which the phoneme recognizer hears no vowel or an extra one.
2. `asr_check.py words words.jsonl` lets Whisper hear every word and phrase one by one;
   `classify.py words.jsonl flagged.json` sorts the misses.
3. `revoice.py flagged.json` (MeloTTS venv) voices each miss again in three variants
   (`artifacts/tts/ko-melo-variants/`).
4. `batch_recheck.py flagged.json results.jsonl` hears the original and then the variants packed about ten to a
   Whisper window: one isolated word costs as much as a full 30-second window, and Whisper hears short words
   much better among others (one by one it wrote 학생 as "핫線"). The original stays when it passes.
5. `python3 tools/tts/apply_revoiced.py results.jsonl` plays the first variant that passed.
   `listen_data.py results.jsonl clips.json` collects the rest for listening by ear.

Of 18,000 words and phrases, Whisper heard 1,177 wrongly one by one; packed, 509 originals passed, 295 got
a better variant and 373 were left for listening.

All steps are resumable and slow on a laptop. Run them with limits and pause before it overheats (it powered
off at 103 °C under four busy threads): `thermal.py` pauses between clips above 80 °C, and `watchdog.sh <unit>`
freezes the systemd unit within a second, e.g.

```bash
systemd-run --user -d --unit kortrainer-asr -p CPUQuota=200% -p MemoryMax=3G -p Nice=19 \
  env ASR_THREADS=2 HF_HUB_OFFLINE=1 "$PWD/.venv-asr/bin/python" tools/tts/asr/batch_recheck.py flagged.json results.jsonl
systemd-run --user --unit kortrainer-heat-guard "$PWD/tools/tts/asr/watchdog.sh" kortrainer-asr.service
```
