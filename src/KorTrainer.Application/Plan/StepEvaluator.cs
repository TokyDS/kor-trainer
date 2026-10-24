using KorTrainer.Application.Common;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Plan;
using KorTrainer.Domain.Srs;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Plan;

/// <summary>The time a step was open; activity inside it counts towards the step's tasks.</summary>
/// <param name="DueNow">Cards due right now (only meaningful for the current step).</param>
public sealed record StepWindow(DateTime FromUtc, DateTime ToUtc, StepState State, int DueNow = 0);

/// <summary>Measures a step's tasks against the activity made while the step was open.</summary>
public sealed class StepEvaluator(IAppDbContext db)
{
    private const int SecondsPerMinute = 60;

    public async Task<IReadOnlyList<PlanTaskDto>> EvaluateAsync(PlanStep step, StepWindow window, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(window);

        var read = await db.StepTaskCompletions
            .Where(c => c.StepKey == step.Key)
            .Select(c => c.TaskKey)
            .ToListAsync(cancellationToken);
        var activity = window.State == StepState.Upcoming
            ? StepActivity.Empty
            : await LoadAsync(step, window, cancellationToken);

        var tasks = step.Tasks.Select(t => Evaluate(t, activity, window, read.Contains(t.Key))).ToList();
        if (step.Lesson is null || window.State == StepState.Done)
        {
            return tasks;
        }

        // A lesson is done in order; a finished step keeps whatever order it was done in. Any review counts as
        // progress of the due-words task, so it is never "started" ahead of its turn.
        var locked = TaskOrder.Locked(tasks
            .Select((t, i) => new TaskState(t.IsDone, t.Progress > 0 && step.Tasks[i].Target.Metric != TaskMetric.VocabularyDueCleared))
            .ToList());
        return tasks.Select((t, i) => t with { IsLocked = locked[i] }).ToList();
    }

    private async Task<StepActivity> LoadAsync(PlanStep step, StepWindow window, CancellationToken cancellationToken)
    {
        var (from, to) = (window.FromUtc, window.ToUtc);
        var answers = await db.ExerciseAttempts
            .Where(a => a.AnsweredUtc >= from && a.AnsweredUtc < to)
            .GroupBy(a => new { a.Module, a.ExerciseType })
            .Select(g => new AnswerCount(g.Key.Module, g.Key.ExerciseType, g.Count()))
            .ToListAsync(cancellationToken);
        var reviews = db.ReviewLogs.Where(r => r.ReviewedUtc >= from && r.ReviewedUtc < to);

        bool needsDeck = step.Tasks.Any(t => t.Target.Metric == TaskMetric.WordsStudied);
        return new StepActivity(
            answers,
            await db.ReadingTrials.CountAsync(r => r.CompletedUtc >= from && r.CompletedUtc < to, cancellationToken),
            await db.WritingDrafts.Where(w => w.StartedUtc >= from && w.StartedUtc < to).SumAsync(w => w.DurationSeconds, cancellationToken),
            await reviews.CountAsync(r => r.PhaseBefore == CardPhase.New, cancellationToken),
            await reviews.CountAsync(cancellationToken),
            needsDeck ? await db.WordProgress.CountAsync(cancellationToken) : 0);
    }

    private static PlanTaskDto Evaluate(PlanTask task, StepActivity activity, StepWindow window, bool read)
    {
        var target = task.Target;
        (int progress, int goal, bool reached) = target.Metric switch
        {
            TaskMetric.Read => (read ? 1 : 0, 1, read),
            TaskMetric.NewWords => Count(activity.NewWords, target.Count),
            TaskMetric.VocabularyDueCleared => window.State switch
            {
                StepState.Current => (activity.Reviews, activity.Reviews + window.DueNow, window.DueNow == 0),
                // The queue of a finished step cannot be reconstructed: done if reviews happened.
                StepState.Done => (activity.Reviews, activity.Reviews, activity.Reviews > 0),
                _ => (0, 0, false),
            },
            TaskMetric.Answers => Count(activity.Answers(target.Module, target.ExerciseType), target.Count),
            TaskMetric.ReadingTrials => Count(activity.ReadingTrials, target.Count),
            TaskMetric.WritingMinutes => Count(activity.WritingSeconds / SecondsPerMinute, target.Count),
            TaskMetric.WordsStudied => Count(activity.WordsStudied, target.Count),
            _ => (0, target.Count, false),
        };

        return new PlanTaskDto(task.Key, task.Title, task.Route, progress, goal, reached, target.Metric == TaskMetric.Read);

        static (int, int, bool) Count(int value, int goal) => (Math.Min(value, goal), goal, value >= goal);
    }

    private sealed record AnswerCount(StudyModule Module, string ExerciseType, int Count);

    private sealed record StepActivity(
        IReadOnlyList<AnswerCount> AnswerCounts, int ReadingTrials, int WritingSeconds, int NewWords, int Reviews, int WordsStudied)
    {
        public static StepActivity Empty { get; } = new([], 0, 0, 0, 0, 0);

        public int Answers(StudyModule? module, string? exerciseType) => AnswerCounts
            .Where(a => (module is null || a.Module == module) && (exerciseType is null || a.ExerciseType == exerciseType))
            .Sum(a => a.Count);
    }
}
