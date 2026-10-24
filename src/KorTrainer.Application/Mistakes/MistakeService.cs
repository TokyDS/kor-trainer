using System.Text.Json;
using KorTrainer.Application.Common;
using KorTrainer.Application.Settings;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Mistakes;

/// <summary>
/// The «Работа над ошибками» queue: exercises answered wrongly come back after 1, 3 and 7 days.
/// Answers are tracked by <see cref="Activity.ActivityService"/>; this service only reads the queue.
/// </summary>
public sealed class MistakeService(IAppDbContext db, SettingsService settingsService, StudyClock clock, Random random)
{
    public const int MaxCount = 50;

    public const string SetType = "mistakes";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Serialize(ChoiceExerciseDto exercise) => JsonSerializer.Serialize(exercise, Json);

    public async Task<MistakeSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var today = await TodayAsync(cancellationToken);
        var counts = await db.Mistakes
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Due = g.Count(m => m.ResolvedDate == null && m.DueDate <= today),
                Open = g.Count(m => m.ResolvedDate == null),
                Resolved = g.Count(m => m.ResolvedDate != null),
            })
            .FirstOrDefaultAsync(cancellationToken);
        return new MistakeSummaryDto(counts?.Due ?? 0, counts?.Open ?? 0, counts?.Resolved ?? 0);
    }

    /// <summary>Due mistakes, oldest first, with their options shuffled again so the position is not remembered.</summary>
    public async Task<ExerciseSetDto> GetDueAsync(int count, CancellationToken cancellationToken)
    {
        new Validator()
            .Require(count is >= 1 and <= MaxCount, nameof(count), $"Количество: от 1 до {MaxCount}.")
            .ThrowIfInvalid();

        var today = await TodayAsync(cancellationToken);
        var due = await db.Mistakes
            .Where(m => m.ResolvedDate == null && m.DueDate <= today)
            .OrderBy(m => m.DueDate).ThenBy(m => m.Id)
            .Take(count)
            .Select(m => new { m.Module, m.Payload })
            .ToListAsync(cancellationToken);

        var items = due
            .Select(m => Restore(m.Payload, m.Module))
            .OfType<ChoiceExerciseDto>()
            .ToList();
        var module = items.GroupBy(i => i.Module).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? StudyModule.Grammar;
        return new ExerciseSetDto(module, SetType, "Работа над ошибками", items);
    }

    private ChoiceExerciseDto? Restore(string payload, StudyModule module)
    {
        ChoiceExerciseDto? exercise;
        try
        {
            exercise = JsonSerializer.Deserialize<ChoiceExerciseDto>(payload, Json);
        }
        catch (JsonException)
        {
            return null;
        }

        if (exercise is not { Options.Count: > 1 } || exercise.CorrectIndex < 0 || exercise.CorrectIndex >= exercise.Options.Count)
        {
            return null;
        }

        var correct = exercise.Options[exercise.CorrectIndex];
        var options = exercise.Options.OrderBy(_ => random.Next()).ToList();
        return exercise with { Options = options, CorrectIndex = options.IndexOf(correct), Module = module };
    }

    private async Task<DateOnly> TodayAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetEntityAsync(cancellationToken);
        return StudyClock.LocalDate(settings, clock.UtcNow);
    }
}
