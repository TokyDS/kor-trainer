using KorTrainer.Contracts;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Plan;
using KorTrainer.Domain.Srs;

namespace KorTrainer.Application.Vocabulary;

/// <summary>Reusable, SQL-translatable filters over words and their SRS progress.</summary>
internal static class WordQueries
{
    public static IQueryable<Word> Unlocked(this IQueryable<Word> words, WordUnlock unlock) =>
        words.Where(w => w.Stage < unlock.MaxStage || (w.Stage == unlock.MaxStage && (w.LessonDay ?? 0) <= unlock.MaxLessonDay));

    public static IQueryable<Word> Locked(this IQueryable<Word> words, WordUnlock unlock) =>
        words.Where(w => !(w.Stage < unlock.MaxStage || (w.Stage == unlock.MaxStage && (w.LessonDay ?? 0) <= unlock.MaxLessonDay)));

    public static IQueryable<Word> InIntroductionOrder(this IQueryable<Word> words) =>
        words.OrderBy(w => w.Stage).ThenBy(w => w.LessonDay ?? 0).ThenBy(w => w.Rank).ThenBy(w => w.Id);

    public static IQueryable<Word> WithStatus(this IQueryable<Word> words, WordStatus status, WordUnlock unlock) => status switch
    {
        WordStatus.Locked => words.Where(w => w.Progress == null).Locked(unlock),
        WordStatus.New => words.Where(w => w.Progress == null).Unlocked(unlock),
        WordStatus.Learning => words.Where(w => w.Progress != null && (w.Progress.Phase == CardPhase.Learning || w.Progress.Phase == CardPhase.Relearning)),
        WordStatus.Young => words.Where(w => w.Progress != null && w.Progress.Phase == CardPhase.Review && w.Progress.IntervalDays < CardState.MatureIntervalDays),
        WordStatus.Mature => words.Where(w => w.Progress != null && w.Progress.Phase == CardPhase.Review && w.Progress.IntervalDays >= CardState.MatureIntervalDays),
        _ => words,
    };

    public static WordStatus StatusOf(Word word, WordUnlock unlock) => word.Progress switch
    {
        null => unlock.IsUnlocked(word.Stage, word.LessonDay) ? WordStatus.New : WordStatus.Locked,
        { Phase: CardPhase.Learning or CardPhase.Relearning or CardPhase.New } => WordStatus.Learning,
        { IntervalDays: >= CardState.MatureIntervalDays } => WordStatus.Mature,
        _ => WordStatus.Young,
    };
}
