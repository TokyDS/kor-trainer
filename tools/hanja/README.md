# Hanja

Sino-Korean spellings come from the hanja column of the NIKL learner vocabulary list
(`tools/deck/nikl-learner-vocabulary.tsv`) and are stored as the optional fifth field of deck entries:
`["학교", "школа", "noun", "", "學校"]`. A `-` marks the native part: `始作-` is 시작 of 시작하다, `-番` is 번 of
이번. `BuildNiklDeck.cs` writes them for the frequency deck; the curated deck got them once from the same list
(homonyms with different hanja were left out).

`src/KorTrainer.Infrastructure/Seed/hanja-glosses.tsv` lists the 1,333 characters used by the deck:
character, 훈 (native name, e.g. 배울 for 學) and a short Russian gloss chosen for the senses these words
use. The glosses were **machine-written** with the Unihan `kDefinition` field (Unicode, Inc., Unicode
License v3) as a hint and should be reviewed; fix a line in place.
