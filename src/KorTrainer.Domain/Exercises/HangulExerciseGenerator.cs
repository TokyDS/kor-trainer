using KorTrainer.Domain.Common;
using KorTrainer.Domain.Hangul;

namespace KorTrainer.Domain.Exercises;

/// <summary>
/// Generates Hangul drills. Answers and distractors are derived from Unicode decomposition
/// and the pronunciation rules, so every item is computed rather than stored.
/// </summary>
public sealed class HangulExerciseGenerator(Random random)
{
    public const int OptionCount = 4;

    private const int MaxUniqueAttempts = 8;
    private const string CommonFinals = "ㄱㄴㄷㄹㅁㅂㅇㅅㅆㅈㅊㅋㅌㅍㅎㄲ";
    private const string DoubleFinals = "ㄳㄵㄶㄺㄻㄼㅀㅄ";
    private const int DoubleFinalOneIn = 6;
    private const int NoFinalOneIn = 4;
    private const int MinRealSyllables = 10;
    private const int RealSyllableShare = 3;
    private const int RealSyllableOutOf = 4;

    /// <summary>Classic examples used when the deck does not provide enough words with sound changes.</summary>
    private static readonly string[] SoundChangeExamples =
    [
        "한국어", "음악", "읽어요", "좋아요", "감사합니다", "학교", "꽃이", "옷을", "있어요", "없어요",
        "먹는", "같이", "신라", "국물", "축하해요", "입학", "많아요", "앉아요", "맛있어요", "닭",
        "부엌", "밖에", "작년", "십만", "설날", "좋다", "못해요", "합니다", "책상", "앞에",
    ];

    /// <param name="words">
    /// Real words (the deck): syllable drills mostly use their syllables, and the sound-change
    /// drill picks words whose pronunciation differs from the spelling.
    /// </param>
    /// <param name="recordedWords">Words with native-speaker audio; the listening drill then uses only those.</param>
    public IReadOnlyList<ChoiceExercise> Generate(
        string type,
        HangulLevel level,
        int count,
        IReadOnlyList<string>? words = null,
        IReadOnlySet<string>? recordedWords = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var syllables = RealSyllables(words ?? [], level);
        Func<ChoiceExercise> next = type switch
        {
            ExerciseTypes.LetterToSound => () => LetterToSound(level),
            ExerciseTypes.SoundToLetter => () => SoundToLetter(level),
            ExerciseTypes.Compose => () => Compose(level, syllables),
            ExerciseTypes.Decompose => () => Decompose(level, syllables),
            ExerciseTypes.ConsonantSeries => () => ConsonantSeries(recordedWords),
            ExerciseTypes.FinalSound => FinalSound,
            ExerciseTypes.SoundChange => () => SoundChange(words ?? []),
            _ => throw new ArgumentException($"Unknown Hangul exercise type '{type}'.", nameof(type)),
        };

        var result = new List<ChoiceExercise>(count);
        var seen = new HashSet<string>();
        for (int i = 0; i < count; i++)
        {
            var exercise = next();
            for (int attempt = 0; attempt < MaxUniqueAttempts && !seen.Add(exercise.ItemKey); attempt++)
            {
                exercise = next();
            }

            result.Add(exercise);
        }

        return result;
    }

    public ChoiceExercise LetterToSound(HangulLevel level)
    {
        var pool = JamoCatalog.ForLevel(level);
        var target = pool.Pick(random);
        var distractors = SimilarKind(pool, target).Select(j => new ChoiceOption(j.Sound, Romanization: j.Romanization));
        var (options, correct) = ChoiceBuilder.Shuffle(new ChoiceOption(target.Sound, Romanization: target.Romanization), distractors, OptionCount, random);

        return new ChoiceExercise(
            ExerciseTypes.LetterToSound,
            $"letter:{target.Jamo}",
            target.Jamo.ToString(),
            "Как звучит эта буква?",
            options,
            correct,
            ExplainLetter(target),
            SpeakText: target.SampleSyllable,
            PromptRomanization: target.Romanization);
    }

    public ChoiceExercise SoundToLetter(HangulLevel level)
    {
        var pool = JamoCatalog.ForLevel(level);
        var target = pool.Pick(random);
        var distractors = SimilarKind(pool, target).Select(j => new ChoiceOption(j.Jamo.ToString(), Romanization: j.Romanization));
        var (options, correct) = ChoiceBuilder.Shuffle(new ChoiceOption(target.Jamo.ToString(), Romanization: target.Romanization), distractors, OptionCount, random);

        return new ChoiceExercise(
            ExerciseTypes.SoundToLetter,
            $"sound:{target.Jamo}",
            $"«{target.Sound}»",
            target.IsVowel ? "Какая гласная так звучит? Пробел — послушать ещё раз." : "Какая согласная так звучит? Пробел — послушать ещё раз.",
            options,
            correct,
            ExplainLetter(target),
            SpeakText: target.SampleSyllable,
            AutoSpeak: true);
    }

    public ChoiceExercise Compose(HangulLevel level, IReadOnlyList<Syllable>? realSyllables = null)
    {
        var syllable = PickSyllable(level, realSyllables);
        var distractors = Mutations(syllable, level).Select(s => new ChoiceOption(s.ToString(), Romanization: Romanizer.Romanize(s.ToString())));
        var (options, correct) = ChoiceBuilder.Shuffle(new ChoiceOption(syllable.ToString(), Romanization: Romanizer.Romanize(syllable.ToString())), distractors, OptionCount, random);

        return new ChoiceExercise(
            ExerciseTypes.Compose,
            $"compose:{syllable}",
            JamoFormula(syllable),
            "Какой слог получится?",
            options,
            correct,
            ExplainLayout(syllable),
            SpeakText: syllable.ToString());
    }

    public ChoiceExercise Decompose(HangulLevel level, IReadOnlyList<Syllable>? realSyllables = null)
    {
        var syllable = PickSyllable(level, realSyllables);
        var distractors = Mutations(syllable, level).Select(s => new ChoiceOption(JamoFormula(s)));
        var (options, correct) = ChoiceBuilder.Shuffle(new ChoiceOption(JamoFormula(syllable)), distractors, OptionCount, random);

        return new ChoiceExercise(
            ExerciseTypes.Decompose,
            $"decompose:{syllable}",
            syllable.ToString(),
            "Из каких букв состоит слог?",
            options,
            correct,
            ExplainLayout(syllable),
            SpeakText: syllable.ToString(),
            PromptRomanization: Romanizer.Romanize(syllable.ToString()));
    }

    /// <summary>
    /// Listening drill on plain / aspirated / tense consonants using real minimal pairs (불 · 풀 · 뿔):
    /// synthesized isolated syllables (가/카/까) sound nearly identical, whole words do not.
    /// Options keep the plain → aspirated → tense order so keys 1–3 always mean the same series.
    /// </summary>
    public ChoiceExercise ConsonantSeries(IReadOnlySet<string>? recordedWords = null)
    {
        var set = MinimalPairs.Playable(recordedWords).Pick(random);
        int correct = random.Next(set.Count);
        var options = set
            .Select(w => new ChoiceOption(
                w.Korean,
                $"{JamoCatalog.SeriesName(JamoCatalog.Get(w.Consonant).Kind)} · {w.Russian}",
                Romanizer.Romanize(w.Korean)))
            .ToList();
        var word = set[correct];
        var target = JamoCatalog.Get(word.Consonant);

        return new ChoiceExercise(
            ExerciseTypes.ConsonantSeries,
            $"series:{word.Korean}",
            "Какое слово прозвучало?",
            "Пробел — послушать ещё раз. После ответа цифрами можно сравнить все варианты.",
            options,
            correct,
            $"Прозвучало {word.Korean} «{word.Russian}»: {JamoCatalog.SeriesName(target.Kind)} {target.Jamo}. {target.Hint}",
            SpeakText: word.Korean,
            AutoSpeak: true);
    }

    public ChoiceExercise FinalSound()
    {
        char final = random.Next(DoubleFinalOneIn) == 0
            ? DoubleFinals[random.Next(DoubleFinals.Length)]
            : CommonFinals[random.Next(CommonFinals.Length)];
        var syllable = new Syllable(HangulAlphabet.Initials[random.Next(HangulAlphabet.Initials.Length)], "ㅏㅓㅗㅜㅣㅡ"[random.Next(6)], final);
        char sound = Pronunciation.RepresentativeSound(final);
        string pronounced = Pronunciation.Pronounce(syllable.ToString()).Text;

        var distractors = Pronunciation.RepresentativeSounds
            .Where(s => s != sound)
            .Shuffled(random)
            .Select(s => new ChoiceOption(s.ToString(), Romanization: Romanizer.Final(s)));
        var (options, correct) = ChoiceBuilder.Shuffle(new ChoiceOption(sound.ToString(), Romanization: Romanizer.Final(sound)), distractors, OptionCount, random);

        string explanation = sound == final
            ? $"Патчим {final} звучит как есть: {syllable} → [{pronounced}]."
            : $"Патчим {final} в конце слога звучит как {sound}: {syllable} → [{pronounced}].";

        return new ChoiceExercise(
            ExerciseTypes.FinalSound,
            $"final:{syllable}",
            syllable.ToString(),
            "Как звучит патчим?",
            options,
            correct,
            explanation,
            SpeakText: syllable.ToString());
    }

    public ChoiceExercise SoundChange(IReadOnlyList<string> words)
    {
        var candidates = words
            .Where(w => w.All(Syllable.IsSyllable) && Pronunciation.Pronounce(w).IsChanged)
            .Concat(SoundChangeExamples)
            .Distinct()
            .ToList();

        string word = candidates.Pick(random);
        var result = Pronunciation.Pronounce(word);
        var distractors = PronunciationDistractors(word, result.Text).Select(d => new ChoiceOption($"[{d}]"));
        var (options, correct) = ChoiceBuilder.Shuffle(new ChoiceOption($"[{result.Text}]"), distractors, OptionCount, random);
        string rules = string.Join("; ", result.Changes.Select(c => c.Description).Distinct());

        return new ChoiceExercise(
            ExerciseTypes.SoundChange,
            $"pron:{word}",
            word,
            "Как это читается?",
            options,
            correct,
            $"{word} → [{result.Text}]: {rules}.",
            SpeakText: word,
            PromptRomanization: Romanizer.Romanize(word));
    }

    /// <summary>Words (or pseudo-words) a learner of the given level can already read.</summary>
    public IReadOnlyList<ReadingItem> Reading(HangulLevel level, int count, IReadOnlyList<(string Korean, string? Translation)> words)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var items = words
            .Where(w => IsReadableAt(w.Korean, level))
            .DistinctBy(w => w.Korean)
            .Shuffled(random)
            .Take(count)
            .Select(w => ToReadingItem(w.Korean, w.Translation))
            .ToList();

        while (items.Count < count)
        {
            int length = 2 + random.Next(2);
            string pseudo = string.Concat(Enumerable.Range(0, length).Select(_ => RandomSyllable(level).ToString()));
            items.Add(ToReadingItem(pseudo, null));
        }

        return items;
    }

    public static bool IsReadableAt(string text, HangulLevel level)
    {
        string[] parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !parts.All(p => p.All(Syllable.IsSyllable)))
        {
            return false;
        }

        var basicLetters = JamoCatalog.ForLevel(HangulLevel.Basics).Select(j => j.Jamo).ToHashSet();
        return parts.SelectMany(p => p).Select(Syllable.Decompose).All(s => level switch
        {
            HangulLevel.Basics => !s.HasFinal && basicLetters.Contains(s.Initial) && basicLetters.Contains(s.Medial),
            HangulLevel.Full => !s.HasFinal,
            _ => true,
        });
    }

    private static ReadingItem ToReadingItem(string text, string? translation) =>
        new(text, text.Count(Syllable.IsSyllable), Pronunciation.Pronounce(text).Text, translation);

    private IEnumerable<JamoInfo> SimilarKind(IReadOnlyList<JamoInfo> pool, JamoInfo target) =>
        pool.Where(j => j.IsVowel == target.IsVowel && j.SoundGroup != target.SoundGroup)
            .Shuffled(random)
            .DistinctBy(j => j.SoundGroup);

    /// <summary>Syllables of real words that use only letters of the level.</summary>
    private static List<Syllable> RealSyllables(IReadOnlyList<string> words, HangulLevel level) =>
        words.SelectMany(w => w)
            .Where(Syllable.IsSyllable)
            .Distinct()
            .Where(c => IsReadableAt(c.ToString(), level))
            .Select(Syllable.Decompose)
            .ToList();

    // Mostly real syllables (familiar shapes), sometimes random ones so every letter gets drilled.
    private Syllable PickSyllable(HangulLevel level, IReadOnlyList<Syllable>? realSyllables) =>
        realSyllables is { Count: >= MinRealSyllables } && random.Next(RealSyllableOutOf) < RealSyllableShare
            ? realSyllables.Pick(random)
            : RandomSyllable(level);

    private Syllable RandomSyllable(HangulLevel level)
    {
        var pool = JamoCatalog.ForLevel(level);
        var consonants = pool.Where(j => !j.IsVowel).ToList();
        var vowels = pool.Where(j => j.IsVowel).ToList();
        char? final = level == HangulLevel.Batchim && random.Next(NoFinalOneIn) != 0 ? RandomFinal() : null;
        return new Syllable(consonants.Pick(random).Jamo, vowels.Pick(random).Jamo, final);
    }

    private char RandomFinal() => random.Next(DoubleFinalOneIn) == 0
        ? DoubleFinals[random.Next(DoubleFinals.Length)]
        : CommonFinals[random.Next(CommonFinals.Length)];

    /// <summary>Syllables differing from the original in exactly one letter.</summary>
    private IEnumerable<Syllable> Mutations(Syllable syllable, HangulLevel level)
    {
        var pool = JamoCatalog.ForLevel(level);
        var consonants = pool.Where(j => !j.IsVowel && j.Jamo != syllable.Initial).Select(j => j.Jamo).Shuffled(random).ToList();
        var vowels = pool.Where(j => j.IsVowel && j.Jamo != syllable.Medial).Select(j => j.Jamo).Shuffled(random).ToList();

        var candidates = new List<Syllable>
        {
            syllable.WithMedial(vowels[0]),
            syllable.WithInitial(consonants[0]),
            syllable.WithMedial(vowels[1]),
            syllable.WithInitial(consonants[1]),
        };

        if (level == HangulLevel.Batchim)
        {
            char other = RandomFinal();
            candidates.Insert(1, syllable.WithFinal(syllable.HasFinal && random.Next(2) == 0 ? null : other));
        }

        return candidates.Where(c => c != syllable);
    }

    // The spelling itself is always offered: "read it as written" is the typical beginner mistake.
    private IEnumerable<string> PronunciationDistractors(string spelling, string correct)
    {
        var neutralizedOnly = string.Concat(spelling.Select(c => Pronunciation.Pronounce(c.ToString()).Text));
        var perturbed = Enumerable.Range(0, correct.Length).Select(i => Perturb(correct, i)).Shuffled(random);
        return new[] { spelling, neutralizedOnly }
            .Concat(perturbed)
            .Where(c => c != correct)
            .Distinct();
    }

    // Flips one plausible feature of a syllable: tense↔plain initial or nasal↔stop final.
    private static string Perturb(string word, int index)
    {
        var syllable = Syllable.Decompose(word[index]);
        var changed = syllable.Initial switch
        {
            'ㄲ' => syllable.WithInitial('ㄱ'),
            'ㄸ' => syllable.WithInitial('ㄷ'),
            'ㅃ' => syllable.WithInitial('ㅂ'),
            'ㅆ' => syllable.WithInitial('ㅅ'),
            'ㅉ' => syllable.WithInitial('ㅈ'),
            'ㄱ' => syllable.WithInitial('ㄲ'),
            'ㄷ' => syllable.WithInitial('ㄸ'),
            'ㅂ' => syllable.WithInitial('ㅃ'),
            _ => syllable.Final switch
            {
                'ㅇ' => syllable.WithFinal('ㄱ'),
                'ㄴ' => syllable.WithFinal('ㄷ'),
                'ㅁ' => syllable.WithFinal('ㅂ'),
                null => syllable.WithFinal('ㄴ'),
                _ => syllable.WithFinal(null),
            },
        };

        return string.Concat(word[..index], changed.ToString(), word[(index + 1)..]);
    }

    private static string JamoFormula(Syllable syllable) => string.Join(" + ", syllable.Jamo);

    private static string ExplainLetter(JamoInfo jamo)
    {
        string name = jamo.Name is null ? string.Empty : $" (название: {jamo.Name})";
        return $"{jamo.Jamo} — «{jamo.Sound}»{name}. {jamo.Hint}";
    }

    private static string ExplainLayout(Syllable syllable)
    {
        string vowelPlace = HangulAlphabet.VerticalVowels.Contains(syllable.Medial)
            ? $"вертикальная гласная {syllable.Medial} пишется справа от согласной"
            : HangulAlphabet.HorizontalVowels.Contains(syllable.Medial)
                ? $"горизонтальная гласная {syllable.Medial} пишется под согласной"
                : $"составная гласная {syllable.Medial} огибает согласную снизу и справа";
        string final = syllable.Final is { } f ? $", патчим {f} — в самом низу" : string.Empty;
        return $"{JamoFormula(syllable)} = {syllable}: {vowelPlace}{final}.";
    }
}
