using KorTrainer.Application.Common;
using KorTrainer.Application.Settings;
using KorTrainer.Application.Vocabulary;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Plan;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Today;

/// <summary>Builds the "Today" screen: plan tasks with live progress, lesson notes and streak.</summary>
public sealed class TodayService(
    IAppDbContext db,
    SettingsService settingsService,
    VocabularyService vocabularyService,
    StudyClock clock)
{
    private const int SecondsPerMinute = 60;

    public async Task<TodayDto> GetTodayAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetEntityAsync(cancellationToken);
        var today = clock.Today(settings);
        var plan = new StudyPlan(settings.StartDate, settings.NewWordsPerDay);
        var day = plan.GetDay(today);
        var vocabulary = await vocabularyService.GetSummaryAsync(cancellationToken);
        var activity = await LoadActivityAsync(today, cancellationToken);
        var activeDays = await LoadActiveDaysAsync(cancellationToken);

        var tasks = day.Tasks.Select(t => ToDto(t, activity, vocabulary)).ToList();
        return new TodayDto(
            today,
            day.DayNumber,
            ToDto(day.Stage),
            day.Lesson is { } l ? ToDto(l) : null,
            tasks,
            Streak.Calculate(activeDays, today),
            activeDays.Contains(today),
            day.QuickLinks.Select(q => new QuickLinkDto(q.Title, q.Route)).ToList(),
            vocabulary);
    }

    public async Task<PlanDto> GetPlanAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetEntityAsync(cancellationToken);
        var today = clock.Today(settings);
        var plan = new StudyPlan(settings.StartDate, settings.NewWordsPerDay);
        return new PlanDto(
            plan.StartDate,
            plan.DayNumber(today),
            plan.StageOn(today),
            plan.Stages.Select(ToDto).ToList(),
            plan.Lessons.Select(ToDto).ToList());
    }

    /// <summary>Ticks or unticks a task of the given day by hand.</summary>
    public async Task SetTaskDoneAsync(string taskKey, SetTaskDoneRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = await settingsService.GetEntityAsync(cancellationToken);
        var today = clock.Today(settings);
        new Validator()
            .Require(request.Date <= today && request.Date >= today.AddDays(-7), nameof(request.Date), "Отмечать можно только задачи за последнюю неделю.")
            .ThrowIfInvalid();

        var day = new StudyPlan(settings.StartDate, settings.NewWordsPerDay).GetDay(request.Date);
        if (day.Tasks.All(t => t.Key != taskKey))
        {
            throw new NotFoundException($"В плане на {request.Date:yyyy-MM-dd} нет задачи '{taskKey}'.");
        }

        var existing = await db.TaskCompletions.SingleOrDefaultAsync(c => c.Date == request.Date && c.TaskKey == taskKey, cancellationToken);
        if (request.Done && existing is null)
        {
            db.TaskCompletions.Add(new() { Date = request.Date, TaskKey = taskKey, CompletedUtc = clock.UtcNow });
        }
        else if (!request.Done && existing is not null)
        {
            db.TaskCompletions.Remove(existing);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<DayActivity> LoadActivityAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var answers = await db.ExerciseAttempts
            .Where(a => a.LocalDate == today)
            .GroupBy(a => new { a.Module, a.ExerciseType })
            .Select(g => new AnswerCount(g.Key.Module, g.Key.ExerciseType, g.Count()))
            .ToListAsync(cancellationToken);

        int reading = await db.ReadingTrials.CountAsync(r => r.LocalDate == today, cancellationToken);
        int writingSeconds = await db.WritingDrafts.Where(w => w.LocalDate == today).SumAsync(w => w.DurationSeconds, cancellationToken);
        var manual = await db.TaskCompletions.Where(c => c.Date == today).Select(c => c.TaskKey).ToListAsync(cancellationToken);

        return new DayActivity(answers, reading, writingSeconds, manual.ToHashSet());
    }

    private async Task<HashSet<DateOnly>> LoadActiveDaysAsync(CancellationToken cancellationToken)
    {
        var days = new HashSet<DateOnly>();
        days.UnionWith(await db.ReviewLogs.Select(r => r.LocalDate).Distinct().ToListAsync(cancellationToken));
        days.UnionWith(await db.ExerciseAttempts.Select(r => r.LocalDate).Distinct().ToListAsync(cancellationToken));
        days.UnionWith(await db.ReadingTrials.Select(r => r.LocalDate).Distinct().ToListAsync(cancellationToken));
        days.UnionWith(await db.WritingDrafts.Select(r => r.LocalDate).Distinct().ToListAsync(cancellationToken));
        days.UnionWith(await db.TaskCompletions.Select(r => r.Date).Distinct().ToListAsync(cancellationToken));
        return days;
    }

    private static DailyTaskDto ToDto(DailyTask task, DayActivity activity, VocabularySummaryDto vocabulary)
    {
        var target = task.Target;
        bool ticked = activity.ManualKeys.Contains(task.Key);
        (int progress, int goal, bool reached) = target.Metric switch
        {
            TaskMetric.Manual => (ticked ? 1 : 0, 1, false),
            TaskMetric.NewWords => Count(vocabulary.NewToday, target.Count),
            TaskMetric.VocabularyDueCleared => (vocabulary.ReviewsToday, vocabulary.ReviewsToday + vocabulary.DueNow, vocabulary.DueNow == 0),
            TaskMetric.Answers => Count(activity.Answers(target.Module, target.ExerciseType), target.Count),
            TaskMetric.ReadingTrials => Count(activity.ReadingTrials, target.Count),
            TaskMetric.WritingMinutes => Count(activity.WritingSeconds / SecondsPerMinute, target.Count),
            _ => (0, target.Count, false),
        };

        return new DailyTaskDto(task.Key, task.Title, task.Route, progress, goal, ticked || reached, target.Metric == TaskMetric.Manual);

        static (int, int, bool) Count(int value, int goal) => (Math.Min(value, goal), goal, value >= goal);
    }

    private static StageDto ToDto(StageInfo s) => new(s.Stage, s.Title, s.Goal, s.Start, s.End);

    private static LessonDto ToDto(LessonInfo l) => new(l.Day, l.Title, l.KeyPoints);

    private sealed record AnswerCount(StudyModule Module, string ExerciseType, int Count);

    private sealed record DayActivity(IReadOnlyList<AnswerCount> AnswerCounts, int ReadingTrials, int WritingSeconds, IReadOnlySet<string> ManualKeys)
    {
        public int Answers(StudyModule? module, string? exerciseType) => AnswerCounts
            .Where(a => (module is null || a.Module == module) && (exerciseType is null || a.ExerciseType == exerciseType))
            .Sum(a => a.Count);
    }
}
