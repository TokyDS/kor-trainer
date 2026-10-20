using KorTrainer.Application.Common;
using KorTrainer.Application.Plan;
using KorTrainer.Application.Sentences;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Hangul;
using KorTrainer.Domain.Typing;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Typing;

/// <summary>Practice text for the 두벌식 keyboard trainer: row lessons, the learner's words, then sentences.</summary>
public sealed class TypingService(IAppDbContext db, PlanCursor cursor, SentenceService sentences, Random random)
{
    public const int MaxCount = 40;

    public async Task<TypingSetDto> GetSetAsync(int level, int count, CancellationToken cancellationToken)
    {
        new Validator()
            .Require(level is >= 1 and <= TypingLessons.SentencesLevel, nameof(level), $"Уровень: от 1 до {TypingLessons.SentencesLevel}.")
            .Require(count is >= 1 and <= MaxCount, nameof(count), $"Количество: от 1 до {MaxCount}.")
            .ThrowIfInvalid();

        if (level == TypingLessons.SentencesLevel)
        {
            var set = await sentences.GetPracticeAsync(ExerciseTypes.Dictation, Math.Min(count, SentenceService.MaxCount), cancellationToken);
            return new TypingSetDto(level, "Предложения", set.Items.Select(i => i.Korean).ToList());
        }

        var words = await LearnerWordsAsync(cancellationToken);
        if (level == TypingLessons.WordsLevel)
        {
            var items = words.Where(w => w.All(c => Syllable.IsSyllable(c) || c == ' ')).Take(count).ToList();
            return new TypingSetDto(level, "Ваши слова", items);
        }

        return new TypingSetDto(level, TypingLessons.Lessons[level - 1].Title, TypingLessons.Generate(level, count, random, words));
    }

    /// <summary>Studied words first, then the ones the plan has unlocked, then the most frequent.</summary>
    private async Task<List<string>> LearnerWordsAsync(CancellationToken cancellationToken)
    {
        var unlock = (await cursor.GetAsync(cancellationToken)).Step.WordUnlock;
        var rows = await db.Words
            .OrderBy(w => w.Rank)
            .Select(w => new { w.Korean, w.Stage, w.LessonDay, Studied = w.Progress != null })
            .ToListAsync(cancellationToken);
        // Shuffled within each group, so sessions vary but studied words always come first.
        return rows
            .OrderByDescending(w => w.Studied)
            .ThenByDescending(w => unlock.IsUnlocked(w.Stage, w.LessonDay))
            .ThenBy(_ => random.Next())
            .Select(w => w.Korean)
            .Distinct()
            .ToList();
    }
}
