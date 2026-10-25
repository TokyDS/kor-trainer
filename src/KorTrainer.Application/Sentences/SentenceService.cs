using KorTrainer.Application.Common;
using KorTrainer.Application.Plan;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Sentences;

/// <summary>
/// Sentences for dictation and shadowing, made of the learner's words: all words studied if possible,
/// otherwise unlocked by the current step, otherwise the simplest sentences of the collection.
/// </summary>
public sealed class SentenceService(IAppDbContext db, ISentenceLibrary library, PlanCursor cursor, Random random)
{
    public const int MaxCount = 30;

    private static readonly Dictionary<string, string> Titles = new()
    {
        [ExerciseTypes.Dictation] = "Диктант",
        [ExerciseTypes.Shadowing] = "Повторение вслух",
        [ExerciseTypes.SayWord] = "Произношение слов",
    };

    public async Task<SentenceSetDto> GetPracticeAsync(string type, int count, CancellationToken cancellationToken)
    {
        new Validator()
            .Require(Titles.ContainsKey(type), nameof(type), "Неизвестный тип упражнения с предложениями.")
            .Require(count is >= 1 and <= MaxCount, nameof(count), $"Количество: от 1 до {MaxCount}.")
            .ThrowIfInvalid();

        if (type == ExerciseTypes.SayWord)
        {
            return await WordsAsync(count, cancellationToken);
        }

        var unlock = (await cursor.GetAsync(cancellationToken)).Step.WordUnlock;
        var words = (await db.Words
                .Select(w => new { w.DeckKey, w.Stage, w.LessonDay, w.Rank, Studied = w.Progress != null })
                .ToListAsync(cancellationToken))
            .ToDictionary(w => w.DeckKey, w => (w.Rank, w.Studied, Unlocked: w.Studied || unlock.IsUnlocked(w.Stage, w.LessonDay)), StringComparer.Ordinal);

        var rated = library.Sentences
            .Where(s => s.WordKeys.Count > 0 && s.WordKeys.All(words.ContainsKey))
            .Select(s => (Sentence: s,
                Studied: s.WordKeys.All(k => words[k].Studied),
                Unlocked: s.WordKeys.All(k => words[k].Unlocked),
                Level: s.WordKeys.Max(k => words[k].Rank)))
            .ToList();

        var studied = rated.Where(r => r.Studied).OrderBy(_ => random.Next()).ToList();
        var unlocked = rated.Where(r => !r.Studied && r.Unlocked).OrderBy(r => r.Level).ThenBy(_ => random.Next()).ToList();
        var simplest = rated.Where(r => !r.Unlocked).OrderBy(r => r.Level).ToList();
        var picked = studied.Concat(unlocked).Concat(simplest).Take(count).Select(r => r.Sentence).ToList();

        return new SentenceSetDto(
            type,
            Titles[type],
            studied.Count >= count,
            picked.Select(s => new PracticeSentenceDto(s.Id, s.Korean, s.Russian, s.MachineTranslated, s.Author)).OrderBy(_ => random.Next()).ToList());
    }

    /// <summary>
    /// Single words to say aloud, before whole sentences: the learner's studied words, topped up with the words the
    /// current step opens (its own lesson first). The item id is the word's id.
    /// </summary>
    private async Task<SentenceSetDto> WordsAsync(int count, CancellationToken cancellationToken)
    {
        var unlock = (await cursor.GetAsync(cancellationToken)).Step.WordUnlock;
        var words = await db.Words
            .Select(w => new { w.Id, w.Korean, w.Russian, w.Stage, w.LessonDay, w.Rank, Studied = w.Progress != null })
            .ToListAsync(cancellationToken);

        var studied = words.Where(w => w.Studied).OrderBy(_ => random.Next()).ToList();
        var unlocked = words
            .Where(w => !w.Studied && unlock.IsUnlocked(w.Stage, w.LessonDay))
            .OrderByDescending(w => w.LessonDay ?? 0).ThenBy(w => w.Rank)
            .ToList();
        var picked = studied.Concat(unlocked).DistinctBy(w => w.Korean).Take(count).ToList();

        return new SentenceSetDto(
            ExerciseTypes.SayWord,
            Titles[ExerciseTypes.SayWord],
            studied.DistinctBy(w => w.Korean).Count() >= count,
            picked.Select(w => new PracticeSentenceDto(w.Id, w.Korean, w.Russian, false, string.Empty)).OrderBy(_ => random.Next()).ToList());
    }
}
