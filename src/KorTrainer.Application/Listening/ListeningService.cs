using KorTrainer.Application.Common;
using KorTrainer.Application.Plan;
using KorTrainer.Application.Hangul;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Exercises;
using KorTrainer.Domain.Plan;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Listening;

/// <summary>
/// Listening drills over the learner's own words. Only words already started are played; until there are a few,
/// the words the current step opens (its own lesson first) fill in. Wrong options may come from any opened word.
/// </summary>
public sealed class ListeningService(IAppDbContext db, PlanCursor cursor, Random random)
{
    public const int MaxCount = 50;

    /// <summary>Fewest words for a varied drill; below this the step's own words fill in.</summary>
    private const int MinTargets = 8;

    private static readonly Dictionary<string, string> Titles = new()
    {
        [ExerciseTypes.ListenMeaning] = "На слух: перевод",
        [ExerciseTypes.ListenSpelling] = "На слух: написание",
    };

    public async Task<ExerciseSetDto> GetExercisesAsync(string type, int count, CancellationToken cancellationToken)
    {
        new Validator()
            .Require(Titles.ContainsKey(type), nameof(type), "Неизвестный тип упражнения на слух.")
            .Require(count is >= 1 and <= MaxCount, nameof(count), $"Количество: от 1 до {MaxCount}.")
            .ThrowIfInvalid();

        var step = (await cursor.GetAsync(cancellationToken)).Step;
        var maxStage = StudyScope.MaxWordStage(step.Stage);
        var words = await db.Words
            .Where(w => w.Stage <= maxStage)
            .OrderBy(w => w.Stage).ThenBy(w => w.Rank)
            .Select(w => new { w.Korean, w.Russian, w.Stage, w.LessonDay, Studied = w.Progress != null })
            .ToListAsync(cancellationToken);

        var pool = words.Select(w => new ListeningWord(w.Korean, w.Russian)).DistinctBy(w => w.Korean).ToList();
        var studied = words.Where(w => w.Studied).Select(w => new ListeningWord(w.Korean, w.Russian)).DistinctBy(w => w.Korean).ToList();
        var targets = studied.Count >= MinTargets
            ? studied
            : studied
                .Concat(words
                    .Where(w => !w.Studied && step.WordUnlock.IsUnlocked(w.Stage, w.LessonDay))
                    .OrderByDescending(w => w.LessonDay ?? 0)
                    .Select(w => new ListeningWord(w.Korean, w.Russian)))
                .DistinctBy(w => w.Korean)
                .Take(Math.Max(MinTargets, studied.Count))
                .ToList();

        // At the very start (nothing studied, nothing opened) the earliest words of the stage are all there is.
        var items = new ListeningExerciseGenerator(random).Generate(type, count, targets.Count > 0 ? targets : pool, pool)
            .Select(ExerciseMapper.ToDto)
            .ToList();
        return new ExerciseSetDto(StudyModule.Listening, type, Titles[type], items);
    }
}
