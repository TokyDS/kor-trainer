using KorTrainer.Domain.Common;

namespace KorTrainer.Domain.Exercises;

public sealed record HardWordItem(string Key, string Korean, string Russian, string? Note);

/// <summary>
/// Focused practice of words the learner keeps forgetting: meaning (Korean → Russian, distractors from the
/// other hard words) and spelling (Russian → Korean, distractors that sound almost the same).
/// </summary>
public sealed class HardWordExerciseGenerator(Random random)
{
    public const int OptionCount = 4;
    private const int Rounds = 2;

    public static IReadOnlyList<string> Types { get; } = [ExerciseTypes.HardMeaning, ExerciseTypes.HardSpelling];

    /// <param name="words">The hard words, hardest first.</param>
    /// <param name="pool">Other words for distractors.</param>
    public IReadOnlyList<ChoiceExercise> Generate(string type, int count, IReadOnlyList<HardWordItem> words, IReadOnlyList<HardWordItem> pool)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(pool);
        if (!Types.Contains(type))
        {
            throw new ArgumentException($"Unknown hard-word exercise type '{type}'.", nameof(type));
        }

        // Each hard word comes up twice in a session (at different places) for a second retrieval.
        var targets = Enumerable.Repeat(words, Rounds).SelectMany(w => w.Shuffled(random)).Take(count).ToList();
        var others = words.Concat(pool).DistinctBy(w => w.Key).ToList();
        return targets.Select(w => type == ExerciseTypes.HardMeaning ? Meaning(w, others) : Spelling(w, others)).ToList();
    }

    private ChoiceExercise Meaning(HardWordItem word, IReadOnlyList<HardWordItem> others)
    {
        var (options, correct) = ChoiceBuilder.Shuffle(
            new ChoiceOption(word.Russian),
            others.Where(o => o.Key != word.Key && o.Russian != word.Russian).Shuffled(random).Select(o => new ChoiceOption(o.Russian)),
            OptionCount,
            random);
        return new ChoiceExercise(
            ExerciseTypes.HardMeaning,
            $"hard:{word.Key}",
            word.Korean,
            "Что значит это слово?",
            options,
            correct,
            Explain(word),
            SpeakText: word.Korean);
    }

    private ChoiceExercise Spelling(HardWordItem word, IReadOnlyList<HardWordItem> others)
    {
        var alikes = ListeningExerciseGenerator.SoundAlikes(word.Korean).Shuffled(random).Select(k => new ChoiceOption(k));
        var fallback = others.Where(o => o.Korean != word.Korean).Shuffled(random).Select(o => new ChoiceOption(o.Korean));
        var (options, correct) = ChoiceBuilder.Shuffle(new ChoiceOption(word.Korean), alikes.Concat(fallback), OptionCount, random);
        return new ChoiceExercise(
            ExerciseTypes.HardSpelling,
            $"hard:{word.Key}",
            word.Russian,
            "Как это будет по-корейски?",
            options,
            correct,
            Explain(word),
            SpeakText: word.Korean);
    }

    private static string Explain(HardWordItem word) =>
        word.Note is { Length: > 0 } note ? $"{word.Korean} — {word.Russian}. Ваша подсказка: {note}" : $"{word.Korean} — {word.Russian}.";
}
