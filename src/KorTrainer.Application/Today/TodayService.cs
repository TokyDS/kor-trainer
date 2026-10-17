using KorTrainer.Application.Common;
using KorTrainer.Application.Plan;
using KorTrainer.Application.Vocabulary;
using KorTrainer.Contracts;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Plan;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Today;

/// <summary>The home screen: the current plan step with live task progress, and browsing of other steps.</summary>
public sealed class TodayService(
    IAppDbContext db,
    PlanCursor cursor,
    StepEvaluator evaluator,
    VocabularyService vocabularyService,
    StudyClock clock)
{
    public Task<TodayDto> GetTodayAsync(CancellationToken cancellationToken) => GetStepAsync(null, cancellationToken);

    /// <summary>A step of the plan: the current one by default, a finished one with the progress made in it, or an upcoming one to look ahead.</summary>
    public async Task<TodayDto> GetStepAsync(string? stepKey, CancellationToken cancellationToken)
    {
        var current = await cursor.GetAsync(cancellationToken);
        var step = stepKey is null ? current.Step : current.Roadmap.Find(stepKey) ?? throw new NotFoundException("Такого шага в плане нет.");
        var history = await cursor.GetHistoryAsync(cancellationToken);
        var vocabulary = await vocabularyService.GetSummaryAsync(cancellationToken);

        var state = StateOf(step, current);
        var progress = history.GetValueOrDefault(step.Key);
        var window = state switch
        {
            StepState.Current => new StepWindow(current.Progress.StartedUtc, DateTime.MaxValue, state, vocabulary.DueNow),
            StepState.Done when progress is { CompletedUtc: { } completed } => new StepWindow(progress.StartedUtc, completed, state),
            _ => new StepWindow(DateTime.MinValue, DateTime.MinValue, StepState.Upcoming),
        };
        var tasks = await evaluator.EvaluateAsync(step, window, cancellationToken);

        var today = clock.Today(current.Settings);
        var activeDays = await db.ActiveDaysAsync(cancellationToken);
        return new TodayDto(
            ToDto(current.Roadmap, step, tasks, state, progress),
            current.Step.Key,
            StudyRhythm.ActiveDays(activeDays, today),
            activeDays.Contains(today),
            vocabulary);
    }

    /// <summary>Moves on to the next step (the learner decides; unfinished tasks do not block it).</summary>
    public async Task<TodayDto> AdvanceAsync(AdvanceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await cursor.AdvanceAsync(request.From, cancellationToken);
        return await GetTodayAsync(cancellationToken);
    }

    public async Task<PlanDto> GetPlanAsync(CancellationToken cancellationToken)
    {
        var current = await cursor.GetAsync(cancellationToken);
        var steps = current.Roadmap.Steps
            .Select(s => new PlanStepSummaryDto(s.Key, s.Index + 1, s.Stage, s.Title, StateOf(s, current)))
            .ToList();
        var stages = current.Roadmap.Steps
            .GroupBy(s => s.Stage)
            .Select(g => new StageDto(
                g.Key,
                g.First().StageTitle,
                Roadmap.StageGoal(g.Key),
                g.Count(),
                g.Count(s => StateOf(s, current) == StepState.Done)))
            .ToList();
        return new PlanDto(current.Step.Key, stages, steps, Roadmap.Lessons.Select(ToDto).ToList());
    }

    /// <summary>Ticks or unticks a task of the current step or of a finished one.</summary>
    public async Task SetTaskDoneAsync(string taskKey, SetTaskDoneRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var current = await cursor.GetAsync(cancellationToken);
        var step = current.Roadmap.Find(request.Step) ?? throw new NotFoundException("Такого шага в плане нет.");
        new Validator()
            .Require(StateOf(step, current) != StepState.Upcoming, nameof(request.Step), "Отмечать задачи можно, когда шаг уже начат.")
            .ThrowIfInvalid();
        if (step.Tasks.All(t => t.Key != taskKey))
        {
            throw new NotFoundException($"В шаге «{step.Title}» нет задачи '{taskKey}'.");
        }

        var existing = await db.StepTaskCompletions.SingleOrDefaultAsync(c => c.StepKey == step.Key && c.TaskKey == taskKey, cancellationToken);
        if (request.Done && existing is null)
        {
            db.StepTaskCompletions.Add(new StepTaskCompletion { StepKey = step.Key, TaskKey = taskKey, CompletedUtc = clock.UtcNow });
        }
        else if (!request.Done && existing is not null)
        {
            db.StepTaskCompletions.Remove(existing);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (request.Done)
        {
            // Ticked twice at once (double click): the task is ticked either way.
        }
    }

    private static StepState StateOf(PlanStep step, CurrentStep current) =>
        step.Index < current.Step.Index ? StepState.Done
        : step.Index == current.Step.Index ? StepState.Current
        : StepState.Upcoming;

    private static PlanStepDto ToDto(Roadmap roadmap, PlanStep step, IReadOnlyList<PlanTaskDto> tasks, StepState state, PlanStepProgress? progress)
    {
        var next = roadmap.Next(step);
        return new PlanStepDto(
            step.Key,
            step.Index + 1,
            roadmap.Steps.Count,
            step.Stage,
            step.StageTitle,
            step.Title,
            step.Goal,
            step.Lesson is { } l ? ToDto(l) : null,
            tasks,
            step.QuickLinks.Select(q => new QuickLinkDto(q.Title, q.Route)).ToList(),
            state,
            progress?.StartedUtc,
            progress?.CompletedUtc,
            roadmap.Previous(step)?.Key,
            next?.Key,
            next is null ? null : $"{next.StageTitle} · {next.Title}");
    }

    private static LessonDto ToDto(LessonInfo l) => new(l.Day, l.Title, l.KeyPoints);
}
