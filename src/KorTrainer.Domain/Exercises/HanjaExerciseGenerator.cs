using KorTrainer.Domain.Common;
using KorTrainer.Domain.Vocabulary;

namespace KorTrainer.Domain.Exercises;

/// <summary>
/// Hanja drills: find the common root of a word family (학교, 학생, 대학 → 學 «учёба»), or guess a
/// word's meaning from its roots (대학 = 大 большой + 學 учёба → университет).
/// </summary>
public sealed class HanjaExerciseGenerator(Random random)
{
    public const int OptionCount = 4;
    private const int FamilyWords = 3;

    public static IReadOnlyList<string> Types { get; } = [ExerciseTypes.HanjaRoot, ExerciseTypes.HanjaMeaning];

    public IReadOnlyList<ChoiceExercise> Generate(string type, int count, IReadOnlyList<HanjaWord> words, IReadOnlyDictionary<char, HanjaGloss> glosses)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(glosses);
        return type switch
        {
            ExerciseTypes.HanjaRoot => RootItems(count, words, glosses),
            ExerciseTypes.HanjaMeaning => MeaningItems(count, words, glosses),
            _ => throw new ArgumentException($"Unknown hanja exercise type '{type}'.", nameof(type)),
        };
    }

    private List<ChoiceExercise> RootItems(int count, IReadOnlyList<HanjaWord> words, IReadOnlyDictionary<char, HanjaGloss> glosses)
    {
        var roots = Hanja.Roots(words, glosses).Where(r => r.Gloss is not null).ToList();
        var families = roots.Where(r => r.Words.Count >= 2).ToList();
        if (families.Count == 0 || roots.Count < OptionCount)
        {
            return [];
        }

        return families.Shuffled(random).Take(count).Select(root =>
        {
            var shown = root.Words.Shuffled(random).Take(FamilyWords).ToList();
            var (options, correct) = ChoiceBuilder.Shuffle(
                Option(root),
                roots.Where(r => r.Character != root.Character && r.Reading != root.Reading).Shuffled(random).Select(Option),
                OptionCount,
                random);
            string family = string.Join(", ", shown.Select(w => $"{w.Korean} ({w.Russian})"));
            return new ChoiceExercise(
                ExerciseTypes.HanjaRoot,
                $"hanja-root:{root.Character}",
                string.Join(", ", shown.Select(w => w.Korean)),
                "Какой общий корень у этих слов?",
                options,
                correct,
                $"{family} — во всех слог {root.Reading} = {root.Character} «{root.Gloss!.Russian}» ({root.Gloss.Hun} {root.Reading}).");
        }).ToList();
    }

    private List<ChoiceExercise> MeaningItems(int count, IReadOnlyList<HanjaWord> words, IReadOnlyDictionary<char, HanjaGloss> glosses)
    {
        var explained = words
            .Select(w => (Word: w, Pairs: Hanja.Align(w.Korean, w.PartOfSpeech, w.Hanja)))
            .Where(x => x.Pairs.Count >= 2 && x.Pairs.All(p => glosses.ContainsKey(p.Character)))
            .ToList();
        if (explained.Count == 0 || words.Select(w => w.Russian).Distinct().Count() < OptionCount)
        {
            return [];
        }

        return explained.Shuffled(random).Take(count).Select(x =>
        {
            string roots = Hanja.Explain(x.Pairs, glosses);
            var (options, correct) = ChoiceBuilder.Shuffle(
                new ChoiceOption(x.Word.Russian),
                words.Where(w => w.Key != x.Word.Key).Shuffled(random).Select(w => new ChoiceOption(w.Russian)),
                OptionCount,
                random);
            return new ChoiceExercise(
                ExerciseTypes.HanjaMeaning,
                $"hanja-meaning:{x.Word.Key}",
                x.Word.Korean,
                roots,
                options,
                correct,
                $"{x.Word.Korean} ({x.Word.Hanja}) = {roots} → «{x.Word.Russian}».",
                SpeakText: x.Word.Korean);
        }).ToList();
    }

    private static ChoiceOption Option(HanjaRoot root) =>
        new($"{root.Character} {root.Reading}", $"{root.Gloss?.Hun} {root.Reading} · {root.Gloss?.Russian}");
}
