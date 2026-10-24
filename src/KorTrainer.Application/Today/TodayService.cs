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
    private const int MaxMaterialLength = 64;

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

    /// <summary>
    /// The learner read a rule, lesson notes or a guide to the end: the current step's tasks asking to read it are done.
    /// Reading something the current step does not ask for changes nothing.
    /// </summary>
    public async Task MarkReadAsync(MarkReadRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        new Validator()
            .Require(!string.IsNullOrWhiteSpace(request.Material) && request.Material.Length <= MaxMaterialLength,
                nameof(request.Material), "Не указано, что прочитано.")
            .ThrowIfInvalid();

        var step = (await cursor.GetAsync(cancellationToken)).Step;
        var keys = step.Tasks
            .Where(t => t.Target.Metric == TaskMetric.Read && t.Target.Material == request.Material)
            .Select(t => t.Key)
            .ToList();
        var done = await db.StepTaskCompletions
            .Where(c => c.StepKey == step.Key && keys.Contains(c.TaskKey))
            .Select(c => c.TaskKey)
            .ToListAsync(cancellationToken);
        var added = keys.Except(done).ToList();
        if (added.Count == 0)
        {
            return;
        }

        var rows = added.Select(k => new StepTaskCompletion { StepKey = step.Key, TaskKey = k, CompletedUtc = clock.UtcNow }).ToList();
        db.StepTaskCompletions.AddRange(rows);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Read twice at once (two tabs): fine if the other request saved the rows; anything else is a real failure.
            foreach (var row in rows)
            {
                db.Entry(row).State = EntityState.Detached;
            }

            int saved = await db.StepTaskCompletions.CountAsync(c => c.StepKey == step.Key && added.Contains(c.TaskKey), cancellationToken);
            if (saved < added.Count)
            {
                throw;
            }
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
