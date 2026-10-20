# Vocabulary deck sources

The app seeds two embedded decks (`src/KorTrainer.Infrastructure/Seed`):

| File | Words | Source |
|---|---|---|
| `words.json` | ~350 | Hand-picked beginner words grouped by the lessons of plan days 4–9 and stage 1 topics. |
| `words-nikl.json` | ~5500 | Generated from the NIKL learner vocabulary list, levels A, B and C, ordered by frequency. |

## NIKL learner vocabulary list

`nikl-learner-vocabulary.tsv` is a plain-text export of **«한국어 학습용 어휘 목록»**
(Korean Learner's Vocabulary List, National Institute of Korean Language / 국립국어원, 2003):
5,965 headwords with part of speech, hanja, level (A — beginner, B — intermediate, C — advanced)
and corpus frequency rank. The list is published under
[KOGL Type 1](https://www.kogl.or.kr/info/licenseType1.do) (공공누리 제1유형): free use with attribution.

Columns: `key` (headword with homonym number), `word`, `pos`, `hanja`, `grade`, `rank`.

## Translations

`nikl.ru.tsv` holds Russian translations keyed by `key|pos`, plus an irregular-conjugation code for
verbs and adjectives (`b` ㅂ, `d` ㄷ, `reu` 르, `s` ㅅ, `h` ㅎ). The NIKL list has no translations;
they were **machine-generated** using the part of speech and hanja to pick the right homonym, and
should be reviewed. Fix a translation here and rebuild — progress is kept because words are matched
by their stable deck key.

## Rebuilding

```bash
dotnet run tools/deck/BuildNiklDeck.cs
```

Words already in `words.json` are skipped. Level A opens in stage 1, the first 1,000 level B words in
stage 2, the rest of B and all of C in stage 3 (TOPIK II); within a stage words are introduced by
frequency, in bands of 100. Rows without a translation are skipped.
