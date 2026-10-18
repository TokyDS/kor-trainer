using KorTrainer.Application.Common;
using KorTrainer.Application.Hangul;
using KorTrainer.Application.Plan;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Exercises;
using KorTrainer.Domain.Vocabulary;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Hanja;

/// <summary>Sino-Korean roots of the deck: word families, root details and hanja drills.</summary>
public sealed class HanjaService(IAppDbContext db, IHanjaGlossary glossary, PlanCursor cursor, Random random)
{
    public const int MaxCount = 50;
    private const int ExampleCount = 4;

    /// <summary>Below this many studied words with hanja the drills also use words the plan has unlocked.</summary>
    private const int MinStudiedWords = 12;

    private static readonly Dictionary<string, string> Titles = new()
    {
        [ExerciseTypes.HanjaRoot] = "Ханча: общий корень",
        [ExerciseTypes.HanjaMeaning] = "Ханча: значение по корням",
    };

    /// <summary>Every character shared by at least two deck words, the learner's own roots first.</summary>
    public async Task<IReadOnlyList<HanjaRootDto>> GetRootsAsync(CancellationToken cancellationToken)
    {
        var words = await LoadAsync(cancellationToken);
        var studied = words.Where(w => w.Studied).Select(w => w.Word.Key).ToHashSet(StringComparer.Ordinal);
        return Domain.Vocabulary.Hanja.Roots(words.Select(w => w.Word), glossary.Glosses)
            .Where(r => r.Words.Count >= 2)
            .Select(r => ToDto(r, studied))
            .OrderByDescending(r => r.StudiedWords)
            .ThenByDescending(r => r.TotalWords)
            .ToList();
    }

    public async Task<HanjaRootDetailDto> GetRootAsync(string character, CancellationToken cancellationToken)
    {
        new Validator()
            .Require(character is { Length: 1 } && Domain.Vocabulary.Hanja.IsHanja(character[0]), nameof(character), "Укажите один иероглиф.")
            .ThrowIfInvalid();

        var words = await LoadAsync(cancellationToken);
        var root = Domain.Vocabulary.Hanja.Roots(words.Select(w => w.Word), glossary.Glosses).FirstOrDefault(r => r.Character == character[0])
            ?? throw new NotFoundException($"В колоде нет слов с иероглифом {character}.");
        var byKey = words.ToDictionary(w => w.Word.Key, StringComparer.Ordinal);
        var studied = words.Where(w => w.Studied).Select(w => w.Word.Key).ToHashSet(StringComparer.Ordinal);
        return new HanjaRootDetailDto(
            ToDto(root, studied),
            root.Words.Select(w => new HanjaWordDto(byKey[w.Key].Id, w.Korean, w.Russian, w.Hanja, studied.Contains(w.Key)))
                .OrderByDescending(w => w.Studied)
                .ToList());
    }

    public async Task<ExerciseSetDto> GetExercisesAsync(string type, int count, CancellationToken cancellationToken)
    {
        new Validator()
            .Require(Titles.ContainsKey(type), nameof(type), "Неизвестный тип упражнения с ханча.")
            .Require(count is >= 1 and <= MaxCount, nameof(count), $"Количество: от 1 до {MaxCount}.")
            .ThrowIfInvalid();

        var words = await LoadAsync(cancellationToken);
        var unlock = (await cursor.GetAsync(cancellationToken)).Step.WordUnlock;
        var studied = words.Where(w => w.Studied).Select(w => w.Word).ToList();
        var pool = studied.Count >= MinStudiedWords
            ? studied
            : words.Where(w => w.Studied || unlock.IsUnlocked(w.Stage, w.LessonDay)).Select(w => w.Word).ToList();

        var items = new HanjaExerciseGenerator(random).Generate(type, count, pool, glossary.Glosses);
        if (items.Count == 0)
        {
            throw ValidationFailedException.For(nameof(type), "Пока мало слов с ханча — выучите ещё несколько слов, и упражнения откроются.");
        }

        return new ExerciseSetDto(StudyModule.Hanja, type, Titles[type], items.Select(ExerciseMapper.ToDto).ToList());
    }

    private async Task<List<HanjaRow>> LoadAsync(CancellationToken cancellationToken)
    {
        var rows = await db.Words
            .Where(w => w.Hanja != null)
            .Select(w => new { w.Id, w.DeckKey, w.Korean, w.Russian, w.PartOfSpeech, w.Hanja, w.Rank, w.Stage, w.LessonDay, Studied = w.Progress != null })
            .ToListAsync(cancellationToken);
        return rows
            .Select(w => new HanjaRow(w.Id, new HanjaWord(w.DeckKey, w.Korean, w.Russian, w.PartOfSpeech, w.Hanja!, w.Rank), w.Stage, w.LessonDay, w.Studied))
            .ToList();
    }

    private static HanjaRootDto ToDto(HanjaRoot root, IReadOnlySet<string> studied) => new(
        root.Character.ToString(),
        root.Reading.ToString(),
        root.Gloss?.Hun,
        root.Gloss?.Russian,
        root.Words.Count(w => studied.Contains(w.Key)),
        root.Words.Count,
        root.Words.OrderByDescending(w => studied.Contains(w.Key)).Take(ExampleCount).Select(w => w.Korean).ToList());

    private sealed record HanjaRow(int Id, HanjaWord Word, Domain.Plan.PlanStage Stage, int? LessonDay, bool Studied);
}
