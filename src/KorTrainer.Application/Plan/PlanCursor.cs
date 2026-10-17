using KorTrainer.Application.Common;
using KorTrainer.Application.Settings;
using KorTrainer.Contracts;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Plan;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Plan;

/// <summary>The learner's position in the roadmap.</summary>
public sealed record CurrentStep(Roadmap Roadmap, PlanStep Step, PlanStepProgress Progress, UserSettings Settings);

/// <summary>
/// Keeps track of the current step: exactly one started step is open. The learner moves on with
/// <see cref="AdvanceAsync"/>; nothing moves by itself when days pass.
/// </summary>
public sealed class PlanCursor(IAppDbContext db, SettingsService settingsService, StudyClock clock, StepEvaluator evaluator)
{
    public async Task<CurrentStep> GetAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetEntityAsync(cancellationToken);
        var roadmap = new Roadmap(settings.NewWordsPerDay);
        var current = await FindOpenAsync(roadmap, cancellationToken)
            ?? await ResumeAsync(roadmap, cancellationToken)
            ?? await StartAsync(roadmap, settings, cancellationToken);

        return new CurrentStep(roadmap, roadmap.Find(current.StepKey)!, current, settings);
    }

    private async Task<PlanStepProgress?> FindOpenAsync(Roadmap roadmap, CancellationToken cancellationToken)
    {
        var open = await db.PlanSteps.Where(p => p.CompletedUtc == null).ToListAsync(cancellationToken);
        return open
            .Where(p => roadmap.Find(p.StepKey) is not null)
            .MaxBy(p => roadmap.Find(p.StepKey)!.Index);
    }

    /// <summary>
    /// Started steps exist but none is open (e.g. the open row's key is no longer in the roadmap):
    /// continue after the furthest known step instead of starting over.
    /// </summary>
    private async Task<PlanStepProgress?> ResumeAsync(Roadmap roadmap, CancellationToken cancellationToken)
    {
        var known = (await db.PlanSteps.ToListAsync(cancellationToken)).Where(p => roadmap.Find(p.StepKey) is not null).ToList();
        if (known.Count == 0)
        {
            return null;
        }

        var furthest = known.MaxBy(p => roadmap.Find(p.StepKey)!.Index)!;
        if (roadmap.Next(roadmap.Find(furthest.StepKey)!) is { } next && furthest.CompletedUtc is { } finished)
        {
            furthest = new PlanStepProgress { StepKey = next.Key, StartedUtc = finished };
            db.PlanSteps.Add(furthest);
        }
        else
        {
            furthest.CompletedUtc = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return furthest;
    }

    /// <summary>All started steps by key (finished ones carry their completion time).</summary>
    public async Task<IReadOnlyDictionary<string, PlanStepProgress>> GetHistoryAsync(CancellationToken cancellationToken) =>
        await db.PlanSteps.AsNoTracking().ToDictionaryAsync(p => p.StepKey, StringComparer.Ordinal, cancellationToken);

    /// <summary>Finishes the current step and opens the next one, whether or not every task is done.</summary>
    /// <param name="fromKey">The step the learner sees; a different current step means another tab already moved on.</param>
    public async Task<CurrentStep> AdvanceAsync(string fromKey, CancellationToken cancellationToken)
    {
        var current = await GetAsync(cancellationToken);
        if (current.Step.Key != fromKey)
        {
            throw new ConflictException("Шаг уже сменился — обновите страницу.");
        }

        var next = current.Roadmap.Next(current.Step)
            ?? throw ValidationFailedException.For("step", "Это последний шаг плана.");
        var now = clock.UtcNow;
        current.Progress.CompletedUtc = now;

        var row = await db.PlanSteps.SingleOrDefaultAsync(p => p.StepKey == next.Key, cancellationToken);
        if (row is null)
        {
            row = new PlanStepProgress { StepKey = next.Key, StartedUtc = now };
            db.PlanSteps.Add(row);
        }
        else
        {
            row.StartedUtc = now;
            row.CompletedUtc = null;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another request (second tab, double click) opened the next step a moment earlier.
            throw new ConflictException("Шаг уже сменился — обновите страницу.");
        }

        return current with { Step = next, Progress = row };
    }

    /// <summary>
    /// Opens the plan. Learners who studied with the old calendar plan (lesson N on day N after the start date)
    /// keep their lessons: a lesson counts as done if its tasks were met on its day, and the first one that
    /// was not becomes current, with the activity since its day counting towards it.
    /// </summary>
    private async Task<PlanStepProgress> StartAsync(Roadmap roadmap, UserSettings settings, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var today = clock.Today(settings);
        var steps = new List<PlanStepProgress>();
        var step = roadmap.First;
        var started = now;

        if (await HasActivityAsync(cancellationToken))
        {
            while (step.Lesson is { } lesson)
            {
                var day = settings.StartDate.AddDays(lesson.Day - 1);
                if (day > today)
                {
                    break;
                }

                var from = StudyClock.StartOfLocalDayUtc(settings, day);
                var to = StudyClock.EndOfLocalDayUtc(settings, day);
                started = from;
                var tasks = await evaluator.EvaluateAsync(step, new StepWindow(from, to, StepState.Done), cancellationToken);
                if (!tasks.All(t => t.IsDone))
                {
                    break;
                }

                steps.Add(new PlanStepProgress { StepKey = step.Key, StartedUtc = from, CompletedUtc = to });
                step = roadmap.Next(step)!;
                started = to < now ? to : now;
            }
        }

        var current = new PlanStepProgress { StepKey = step.Key, StartedUtc = started };
        steps.Add(current);
        db.PlanSteps.AddRange(steps);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return current;
        }
        catch (DbUpdateException)
        {
            // A concurrent request may have opened the plan first: use its result if there is one.
            foreach (var entry in steps)
            {
                db.Entry(entry).State = EntityState.Detached;
            }

            var opened = await FindOpenAsync(roadmap, cancellationToken);
            if (opened is null)
            {
                throw;
            }

            return opened;
        }
    }

    private async Task<bool> HasActivityAsync(CancellationToken cancellationToken) =>
        await db.ExerciseAttempts.AnyAsync(cancellationToken)
        || await db.ReviewLogs.AnyAsync(cancellationToken)
        || await db.ReadingTrials.AnyAsync(cancellationToken)
        || await db.StepTaskCompletions.AnyAsync(cancellationToken);
}
