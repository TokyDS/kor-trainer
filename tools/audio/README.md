# Native-speaker recordings

`src/KorTrainer.Infrastructure/Seed/audio-lingualibre.json` lists recordings of Korean words made by
native speakers for [Lingua Libre](https://lingualibre.org) and hosted on Wikimedia Commons
(category `Lingua_Libre_pronunciation-kor`): ~930 words, each with its speaker, license and source page.

- Only openly licensed files are included (currently CC0 and CC BY-SA 4.0), one recording per word,
  preferring the speakers with the most recordings so voices stay consistent.
- The audio itself is **not** stored in the repository. The API downloads a file from Wikimedia the
  first time it is played (only URLs from this manifest), caches it in `Audio:CacheDirectory`
  (`/data/audio` in Docker) and serves it from there; a background service prefetches the rest, one
  file per second, so recordings also work offline afterwards.
- Attribution for every recording is shown on the in-app `/credits` page.
- Wikimedia requires a descriptive User-Agent with a contact; requests without one are answered with
  HTTP 429. Set your own contact with `Audio__UserAgent` if you run the app publicly.

Rebuild the manifest (when new recordings appear on Lingua Libre):

```bash
dotnet run tools/audio/BuildAudioManifest.cs
```
