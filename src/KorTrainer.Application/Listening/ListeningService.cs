using KorTrainer.Application.Common;
using KorTrainer.Application.Hangul;
using KorTrainer.Application.Settings;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Exercises;
using KorTrainer.Domain.Plan;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Listening;

/// <summary>Listening drills over the learner's own words, preferring native-speaker recordings.</summary>
public sealed class ListeningService(IAppDbContext db, SettingsService settingsService, StudyClock clock, Random random, IAudioLibrary audio)
{
    public const int MaxCount = 50;

    /// <summary>Fewest words for a varied drill; below this the pool widens.</summary>
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

        var settings = await settingsService.GetEntityAsync(cancellationToken);
        var stage = new StudyPlan(settings.StartDate, settings.NewWordsPerDay).StageOn(clock.Today(settings));
        var maxStage = StudyScope.MaxWordStage(stage);
        var words = await db.Words
            .Where(w => w.Stage <= maxStage)
            .OrderBy(w => w.Stage).ThenBy(w => w.Rank)
            .Select(w => new { w.Korean, w.Russian, Studied = w.Progress != null })
            .ToListAsync(cancellationToken);

        var pool = words.Select(w => new ListeningWord(w.Korean, w.Russian)).DistinctBy(w => w.Korean).ToList();
        var studied = words.Where(w => w.Studied).Select(w => new ListeningWord(w.Korean, w.Russian)).DistinctBy(w => w.Korean).ToList();
        var targets = Prefer(Prefer(pool, studied), studied.Concat(pool).Where(w => audio.Words.Contains(w.Korean)).DistinctBy(w => w.Korean).ToList());

        var items = new ListeningExerciseGenerator(random).Generate(type, count, targets, pool).Select(ExerciseMapper.ToDto).ToList();
        return new ExerciseSetDto(StudyModule.Listening, type, Titles[type], items);
    }

    private static IReadOnlyList<ListeningWord> Prefer(IReadOnlyList<ListeningWord> fallback, IReadOnlyList<ListeningWord> preferred) =>
        preferred.Count >= MinTargets ? preferred : fallback;
}
