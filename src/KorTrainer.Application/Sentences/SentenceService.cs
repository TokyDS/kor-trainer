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
    };

    public async Task<SentenceSetDto> GetPracticeAsync(string type, int count, CancellationToken cancellationToken)
    {
        new Validator()
            .Require(Titles.ContainsKey(type), nameof(type), "Неизвестный тип упражнения с предложениями.")
            .Require(count is >= 1 and <= MaxCount, nameof(count), $"Количество: от 1 до {MaxCount}.")
            .ThrowIfInvalid();

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
}
