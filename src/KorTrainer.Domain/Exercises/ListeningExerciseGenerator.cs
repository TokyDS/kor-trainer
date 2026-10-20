using KorTrainer.Domain.Common;
using KorTrainer.Domain.Hangul;

namespace KorTrainer.Domain.Exercises;

public sealed record ListeningWord(string Korean, string Russian);

/// <summary>
/// Listening drills: a word is played (native recording when available) and the learner picks its
/// meaning or its spelling. Spelling distractors differ from the word by one audible sound.
/// </summary>
public sealed class ListeningExerciseGenerator(Random random)
{
    public const int OptionCount = 4;
    public const int MinPoolSize = OptionCount;

    private const int MaxUniqueAttempts = 8;

    // Vowel pairs a beginner confuses but that do sound different (ㅐ/ㅔ are left out: inaudible).
    private static readonly (char, char)[] VowelSwaps = [('ㅓ', 'ㅗ'), ('ㅜ', 'ㅡ'), ('ㅕ', 'ㅛ'), ('ㅏ', 'ㅓ'), ('ㅗ', 'ㅜ')];
    private static readonly string[] FinalSwaps = ["ㄴㅇ", "ㄴㅁ", "ㅁㅇ", "ㄱㅇ", "ㄹㄴ", "ㅂㅁ"];

    public static IReadOnlyList<string> Types { get; } = [ExerciseTypes.ListenMeaning, ExerciseTypes.ListenSpelling];

    /// <param name="targets">Words that may be played (e.g. those with native recordings).</param>
    /// <param name="pool">All words that may serve as meaning distractors.</param>
    public IReadOnlyList<ChoiceExercise> Generate(string type, int count, IReadOnlyList<ListeningWord> targets, IReadOnlyList<ListeningWord> pool)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        if (!Types.Contains(type))
        {
            throw new ArgumentException($"Unknown listening exercise type '{type}'.", nameof(type));
        }

        var playable = targets.Where(w => w.Korean.All(c => Syllable.IsSyllable(c) || c == ' ')).ToList();
        if (playable.Count == 0 || pool.Count < MinPoolSize)
        {
            throw new InvalidOperationException("Not enough words for a listening drill.");
        }

        var result = new List<ChoiceExercise>(count);
        var seen = new HashSet<string>();
        for (int i = 0; i < count; i++)
        {
            var exercise = Next(type, playable, pool);
            for (int attempt = 0; attempt < MaxUniqueAttempts && !seen.Add(exercise.ItemKey); attempt++)
            {
                exercise = Next(type, playable, pool);
            }

            result.Add(exercise);
        }

        return result;
    }

    public ChoiceExercise Meaning(ListeningWord word, IReadOnlyList<ListeningWord> pool)
    {
        string meaning = GrammarExerciseGenerator.FirstSense(word.Russian);
        var distractors = pool
            .Where(w => w.Korean != word.Korean)
            .Select(w => GrammarExerciseGenerator.FirstSense(w.Russian))
            .Where(m => !string.Equals(m, meaning, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .Shuffled(random)
            .Select(m => new ChoiceOption(m));
        var (options, correct) = ChoiceBuilder.Shuffle(new ChoiceOption(meaning), distractors, OptionCount, random);

        return new ChoiceExercise(
            ExerciseTypes.ListenMeaning,
            $"{ExerciseTypes.ListenMeaning}:{word.Korean}",
            "Что прозвучало?",
            "Выберите перевод. Пробел — послушать ещё раз.",
            options,
            correct,
            $"Прозвучало {word.Korean} — «{word.Russian}».",
            SpeakText: word.Korean,
            AutoSpeak: true);
    }

    public ChoiceExercise Spelling(ListeningWord word, IReadOnlyList<ListeningWord> pool)
    {
        var distractors = SoundAlikes(word.Korean)
            .Shuffled(random)
            .Concat(pool.Where(w => w.Korean != word.Korean && w.Korean.Length == word.Korean.Length).Select(w => w.Korean).Shuffled(random))
            .Distinct()
            .Select(k => new ChoiceOption(k));
        var (options, correct) = ChoiceBuilder.Shuffle(new ChoiceOption(word.Korean), distractors, OptionCount, random);

        return new ChoiceExercise(
            ExerciseTypes.ListenSpelling,
            $"{ExerciseTypes.ListenSpelling}:{word.Korean}",
            "Как пишется то, что прозвучало?",
            "Пробел — послушать ещё раз.",
            options,
            correct,
            $"Прозвучало {word.Korean} — «{word.Russian}».",
            SpeakText: word.Korean,
            AutoSpeak: true);
    }

    /// <summary>
    /// Spellings that differ from the word by one sound (consonant series, vowel, final consonant) and
    /// are actually pronounced differently.
    /// </summary>
    public static IReadOnlyList<string> SoundAlikes(string word)
    {
        string pronounced = Pronunciation.Pronounce(word).Text;
        var variants = new List<string>();
        for (int i = 0; i < word.Length; i++)
        {
            if (!Syllable.IsSyllable(word[i]))
            {
                continue;
            }

            var syllable = Syllable.Decompose(word[i]);
            var series = JamoCatalog.ConsonantSeries.FirstOrDefault(s => s.Contains(syllable.Initial));
            foreach (char other in series ?? [])
            {
                if (other != syllable.Initial)
                {
                    variants.Add(Replace(word, i, syllable.WithInitial(other)));
                }
            }

            foreach (var (a, b) in VowelSwaps)
            {
                if (syllable.Medial == a || syllable.Medial == b)
                {
                    variants.Add(Replace(word, i, syllable.WithMedial(syllable.Medial == a ? b : a)));
                }
            }

            if (syllable.Final is { } final)
            {
                foreach (var pair in FinalSwaps.Where(p => p.Contains(final)))
                {
                    variants.Add(Replace(word, i, syllable.WithFinal(pair[0] == final ? pair[1] : pair[0])));
                }
            }
        }

        return variants
            .Where(v => v != word && Pronunciation.Pronounce(v).Text != pronounced)
            .Distinct()
            .ToList();
    }

    private ChoiceExercise Next(string type, IReadOnlyList<ListeningWord> targets, IReadOnlyList<ListeningWord> pool)
    {
        var word = targets.Pick(random);
        return type == ExerciseTypes.ListenMeaning ? Meaning(word, pool) : Spelling(word, pool);
    }

    private static string Replace(string word, int index, Syllable syllable) =>
        string.Concat(word[..index], syllable.ToString(), word[(index + 1)..]);
}
