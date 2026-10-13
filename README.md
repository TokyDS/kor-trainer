# KorTrainer

A self-study trainer for Korean from absolute zero to **TOPIK in spring 2027**, built as a
clean-architecture ASP.NET Core + Blazor WebAssembly application.

The interface is in Russian (the author's native language); code, comments and docs are in English.
Romanization is **off by default** — the point is to learn to read Hangul without crutches.

| Today (desktop) | Drill (phone, dark theme) | Flashcards |
|---|---|---|
| ![Today](docs/screenshots/today-desktop.png) | ![Drill](docs/screenshots/drill-mobile-dark.png) | ![Review](docs/screenshots/review.png) |

## Features

**Iteration 1 (MVP)**

- **Today** — the day of the study plan with lesson notes, tasks whose progress is tracked
  automatically from your answers, manual ticks, a day streak and quick links for the current stage.
- **Hangul**
  - letter → sound and sound → letter, with Russian sound approximations;
  - syllable composition and decomposition computed from Unicode arithmetic (no syllable tables);
  - the three consonant series (가/카/까, 다/타/따, …) by ear via the browser's Korean voice;
  - batchim: the 7 final sounds and pronunciation rules (연음, nasalization, tensification, …);
  - timed reading with milliseconds-per-syllable statistics;
  - a word analyzer that splits syllables into letters and explains how a word is pronounced.
- **Vocabulary with spaced repetition** — an Anki-style SM-2 scheduler (learning steps, ease,
  lapses), a ~350-word starter deck unlocked lesson by lesson according to the plan.
- **Keyboard first** — `1`–`4` to answer, `Enter` to continue, `Space` to hear the word.
- Responsive layout (phone tab bar / desktop sidebar), light and dark themes.
- Progress export/import as JSON; data stored in SQLite.

**Iteration 2 — grammar**

- Drills generated from deck words by rule: 이에요/예요, 이/가 아니에요, 은/는, 이/가, 을/를 by batchim;
  -아/어요 with vowel harmony and contraction (가요, 봐요, 마셔요, 돼요), ㅡ-drop (써요, 바빠요) and the
  ㅂ/ㄷ/르/ㅅ/ㅎ irregular classes (더워요, 들어요, 몰라요); -습니다/ㅂ니다 with ㄹ-drop (만듭니다); past -았/었어요;
  stage 2 endings -고, -아서/어서, -(으)니까, -고 싶다, -(으)ㄹ 수 있다, -아/어야 하다.
- Distractors are the typical learner mistakes (먹아요, 듣어요, 이워요, 먹읍니다), every answer
  explains the rule that decided it; the "mixed" drill only uses topics the plan has opened.

**Roadmap:** numbers (Sino-Korean/native, time, money), TOPIK II writing timer (tasks 51–54) and statistics.

> ⚠️ **The Russian translations in the starter deck were machine-generated and should be reviewed.**
> Deck entries can be corrected in `src/KorTrainer.Infrastructure/Seed/words.json`; the next start
> updates them in place without losing progress.

## Quick start

```bash
docker compose up --build          # or: podman compose up --build
```

Open <http://localhost:8080>. From a phone on the same network use `http://<computer-ip>:8080`.
Data lives in the `kortrainer-data` volume (`/data/kortrainer.db`).

> 🔒 KorTrainer is a single-user app **without authentication**: anyone who can reach the port can
> read and replace your progress (including the import endpoint). Run it on a trusted home network
> only, or bind it to localhost by changing the port mapping to `"127.0.0.1:8080:8080"`.

Without Docker (requires the .NET 10 SDK):

```bash
dotnet run --project src/KorTrainer.Api      # http://localhost:5116, DB in src/KorTrainer.Api/data/
```

### Korean voice

Listening drills use the [Web Speech API](https://developer.mozilla.org/docs/Web/API/SpeechSynthesis)
with a `ko-KR` voice — no paid services. If the browser has no Korean voice, the app shows
platform-specific installation instructions (Android: Google TTS → Korean; Windows: Settings → Speech;
macOS/iOS: Spoken Content → Yuna; Linux: Chrome has the online “Google 한국의” voice).

## Architecture

```
src/
  KorTrainer.Domain          pure C#: Hangul, pronunciation, SRS, study plan, exercise generators
  KorTrainer.Contracts       DTOs shared by the API and the Blazor client
  KorTrainer.Application     use cases (Today, Vocabulary, Hangul, Activity, Progress), validation
  KorTrainer.Infrastructure  EF Core + SQLite, migrations, deck seeding
  KorTrainer.Api             Minimal API, ProblemDetails, rate limiting; hosts the WASM client
  KorTrainer.Web             Blazor WebAssembly UI, JS interop for speech/keyboard/theme
tests/
  KorTrainer.Domain.Tests        unit tests of the language and scheduling logic
  KorTrainer.Application.Tests   services against a migrated in-memory SQLite database
  KorTrainer.Api.Tests           HTTP integration tests with WebApplicationFactory
```

Dependencies point inwards: `Api → Infrastructure → Application → Domain`. The Domain project has
no dependencies, so the same code runs on the server and inside the browser (the client uses it
for romanization).

### Interesting bits

- **Unicode syllable math** — `Syllable.Decompose` computes
  `initial = (code − 0xAC00) / 588`, `medial = (code − 0xAC00) % 588 / 28`, `final = (code − 0xAC00) % 28`;
  a syllable has batchim when `(code − 0xAC00) % 28 != 0`. A test round-trips all 11,172 syllables.
- **Pronunciation engine** — `Pronunciation.Pronounce("감사합니다")` → `[감사함니다]` and reports which
  rules fired; the explanations in the drills come from that list. Romanization follows the
  Revised Romanization rules on top of it (`학교` → `hakgyo`).
- **SM-2 scheduler** — a pure function `Schedule(CardState, Grade, now) → CardState` over an
  immutable record; the UI shows the next interval for every answer, as in Anki.
- **Plan-driven unlocking** — days 1–3 drill Hangul only; lesson words open on days 4–9; stage words
  open by calendar dates. Activity records store the learner's local date, so streaks and daily
  statistics are plain SQL `GROUP BY` without time zone math.
- **Deterministic generators** — every drill is generated from rules with an injected `Random`,
  so tests can assert properties over hundreds of generated items.

## API

| Method | Route | Purpose |
|---|---|---|
| GET/PUT | `/api/settings` | start date, time zone, romanization, theme, limits |
| GET | `/api/today` | plan day, tasks with progress, streak, vocabulary summary |
| GET | `/api/plan` | stages and lessons |
| PUT | `/api/today/tasks/{key}` | manual tick |
| GET | `/api/hangul/exercises/{type}?level=&count=` | generated multiple-choice drills |
| GET | `/api/hangul/reading?level=&count=` | words for timed reading |
| POST | `/api/hangul/reading-trials` | reading result |
| GET | `/api/hangul/analyze?text=` | syllable breakdown and pronunciation |
| GET | `/api/vocab/queue` · POST `/api/vocab/reviews` | SRS queue and answers |
| GET | `/api/grammar/topics` · `/api/grammar/exercises/{type\|mixed}?count=` | grammar topics and drills |
| GET | `/api/vocab/summary` · `/api/vocab/words` · `/api/vocab/topics` | vocabulary browser |
| POST | `/api/attempts` · `/api/sessions` | activity log |
| GET/POST | `/api/progress/export` · `/api/progress/import` | JSON backup |

Errors are RFC 7807 problem details (`400` with field errors, `404` for unknown routes).
In Development the OpenAPI document is at `/openapi/v1.json`.

## Development

```bash
dotnet build                                        # warnings are errors
dotnet test                                         # 348 tests (xUnit v3, Microsoft.Testing.Platform)
dotnet test -- --coverage --coverage-output-format cobertura
dotnet tool restore
dotnet ef migrations add <Name> -p src/KorTrainer.Infrastructure -s src/KorTrainer.Api -o Persistence/Migrations
```

Line coverage of the server-side code is about 92% (Domain 96%, Application 89%).

## Study plan

| Dates | Stage |
|---|---|
| days 1–3 | Hangul: basic letters → iotized/compound vowels and consonant series → batchim and 연음 |
| days 4–10 | first phrases: greetings, 이에요/예요, 이/가 아니에요, numbers, counters, self-introduction |
| until 2026-12-31 | stage 1: level 1, ~800 words |
| 2027-01-01 – 02-21 | stage 2: level 2 grammar, 1500–2000 words |
| 2027-02-22 – 02-28 | mock exam and the TOPIK I/II decision |
| 2027-03-01 – 04-30 | stage 3: TOPIK II writing 51–54, reading, listening |

The start date is configurable; days 1–10 move with it, later stages are bound to the calendar.
Official past papers are available at [topik.go.kr](https://www.topik.go.kr) — the app links to them
and does not copy exam content.
