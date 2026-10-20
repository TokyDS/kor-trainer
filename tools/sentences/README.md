# Example sentences

`src/KorTrainer.Infrastructure/Seed/sentences-tatoeba.json` holds ~4,750 short Korean sentences from
[Tatoeba](https://tatoeba.org) (CC BY 2.0 FR; every sentence keeps its Tatoeba id and author), linked to the
deck words they contain. They appear as examples on word cards and feed dictation and shadowing.

| File | Purpose |
|---|---|
| `selected.tsv` | The selection: id, Korean, English, Russian (Tatoeba), author. |
| `sentences.ru.tsv` | Russian for sentences without a Tatoeba translation — **machine-translated**, flagged in the app. |
| `excluded.txt` | Ids and Korean/Russian fragments kept out of the app (profanity, violence, politics). |

Selection keeps sentences of up to 40 characters and 9 words in which `WordIndex` recognizes every word
(so they are built from deck vocabulary), up to three per word, preferring sentences made of earlier,
more frequent words.

```bash
dotnet run tools/sentences/BuildSentences.cs                    # rebuild the seed from the files above
dotnet run tools/sentences/BuildSentences.cs -- ~/tatoeba       # re-select from a fresh Tatoeba export
```

The export directory needs `kor_sentences_detailed.tsv`, `kor-eng_links.tsv`, `kor-rus_links.tsv`,
`eng_sentences.tsv` and `rus_sentences.tsv` from <https://downloads.tatoeba.org/exports/per_language/>.
New selections need Russian lines in `sentences.ru.tsv` before they appear.
