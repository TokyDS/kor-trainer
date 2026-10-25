using KorTrainer.Application.Common;
using KorTrainer.Application.Settings;
using KorTrainer.Contracts;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Plan;
using KorTrainer.Domain.Srs;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Progress;

/// <summary>Exports all learner data to a portable JSON model and restores it (full replace).</summary>
public sealed class ProgressTransferService(IAppDbContext db, SettingsService settingsService, StudyClock clock)
{
    public const int MaxRecordsPerSection = 500_000;

    private const double MinEase = 1.0;
    private const double MaxEase = 10.0;
    private const double MaxIntervalDays = 36500;
    private const int MaxStep = 20;
    private const int MaxKeyLength = 128;
    private const int MaxSessionSeconds = 24 * 60 * 60;
    private const int MaxDraftLength = 4000;
    private const int MaxResponseMs = 10 * 60 * 1000;

    public async Task<ProgressExport> ExportAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetEntityAsync(cancellationToken);
        var keys = await db.Words.ToDictionaryAsync(w => w.Id, w => w.DeckKey, cancellationToken);

        var words = await db.WordProgress.AsNoTracking().OrderBy(p => p.WordId).ToListAsync(cancellationToken);
        var reviews = await db.ReviewLogs.AsNoTracking().OrderBy(r => r.Id).ToListAsync(cancellationToken);
        var attempts = await db.ExerciseAttempts.AsNoTracking().OrderBy(a => a.Id).ToListAsync(cancellationToken);
        var reading = await db.ReadingTrials.AsNoTracking().OrderBy(r => r.Id).ToListAsync(cancellationToken);
        var sessions = await db.StudySessions.AsNoTracking().OrderBy(s => s.Id).ToListAsync(cancellationToken);
        var steps = await db.PlanSteps.AsNoTracking().OrderBy(s => s.StartedUtc).ToListAsync(cancellationToken);
        var notes = await db.WordNotes.AsNoTracking().OrderBy(n => n.WordId).ToListAsync(cancellationToken);
        var ticks = await db.StepTaskCompletions.AsNoTracking().OrderBy(c => c.StepKey).ThenBy(c => c.TaskKey).ToListAsync(cancellationToken);
        var drafts = await db.WritingDrafts.AsNoTracking().OrderBy(d => d.Id).ToListAsync(cancellationToken);
        var mistakes = await db.Mistakes.AsNoTracking().OrderBy(m => m.Id).ToListAsync(cancellationToken);
        var rules = await db.RuleReviews.AsNoTracking().OrderBy(r => r.ExerciseType).ToListAsync(cancellationToken);
        var flags = await db.TranslationFlags.AsNoTracking().OrderBy(f => f.WordId).ToListAsync(cancellationToken);

        return new ProgressExport(
            ProgressExport.CurrentSchemaVersion,
            clock.UtcNow,
            SettingsService.ToDto(settings),
            words.Where(p => keys.ContainsKey(p.WordId))
                .Select(p => new WordProgressExport(keys[p.WordId], p.Phase, p.Ease, p.IntervalDays, p.Repetitions, p.Lapses, p.Step, p.DueUtc, p.IntroducedUtc, p.LastReviewedUtc))
                .ToList(),
            reviews.Where(r => keys.ContainsKey(r.WordId))
                .Select(r => new ReviewLogExport(keys[r.WordId], r.ReviewedUtc, r.LocalDate, r.Grade, r.PhaseBefore, r.IntervalBefore, r.IntervalAfter, r.DurationMs))
                .ToList(),
            attempts.Select(a => new AttemptExport(a.Module, a.ExerciseType, a.ItemKey, a.IsCorrect, a.ResponseMs, a.AnsweredUtc, a.LocalDate)).ToList(),
            reading.Select(r => new ReadingTrialExport(r.Text, r.SyllableCount, r.ElapsedMs, r.ReadCorrectly, r.CompletedUtc, r.LocalDate)).ToList(),
            sessions.Select(s => new SessionExport(s.Module, s.StartedUtc, s.DurationSeconds, s.LocalDate)).ToList(),
            drafts.Select(d => new WritingDraftExport(d.TaskNumber, d.StartedUtc, d.DurationSeconds, d.Text, d.LocalDate)).ToList(),
            steps.Select(s => new PlanStepExport(s.StepKey, s.StartedUtc, s.CompletedUtc)).ToList(),
            ticks.Select(c => new StepTaskCompletionExport(c.StepKey, c.TaskKey, c.CompletedUtc)).ToList(),
            WordNotes: notes.Where(n => keys.ContainsKey(n.WordId)).Select(n => new WordNoteExport(keys[n.WordId], n.Text, n.UpdatedUtc)).ToList(),
            Mistakes: mistakes
                .Select(m => new MistakeExport(m.Module, m.ExerciseType, m.ItemKey, m.Payload, m.Box, m.Lapses, m.CreatedDate, m.DueDate, m.ResolvedDate))
                .ToList(),
            RuleReviews: rules.Select(r => new RuleReviewExport(r.ExerciseType, r.Box, r.DueDate, r.LastCheckedDate, r.Lapses)).ToList(),
            TranslationFlags: flags.Where(f => keys.ContainsKey(f.WordId))
                .Select(f => new TranslationFlagExport(keys[f.WordId], f.Russian, f.Note, f.CreatedUtc))
                .ToList());
    }

    /// <summary>
    /// Replaces all learner data with the snapshot inside one transaction. Progress for words
    /// that are not in the current deck is skipped and reported.
    /// </summary>
    public async Task<ImportResultDto> ImportAsync(ProgressExport snapshot, CancellationToken cancellationToken)
    {
        Validate(snapshot);

        var wordIds = await db.Words.ToDictionaryAsync(w => w.DeckKey, w => w.Id, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await db.ReviewLogs.ExecuteDeleteAsync(cancellationToken);
        await db.WordProgress.ExecuteDeleteAsync(cancellationToken);
        await db.ExerciseAttempts.ExecuteDeleteAsync(cancellationToken);
        await db.ReadingTrials.ExecuteDeleteAsync(cancellationToken);
        await db.StudySessions.ExecuteDeleteAsync(cancellationToken);
        await db.WordNotes.ExecuteDeleteAsync(cancellationToken);
        await db.PlanSteps.ExecuteDeleteAsync(cancellationToken);
        await db.StepTaskCompletions.ExecuteDeleteAsync(cancellationToken);
        await db.WritingDrafts.ExecuteDeleteAsync(cancellationToken);
        await db.Mistakes.ExecuteDeleteAsync(cancellationToken);
        await db.RuleReviews.ExecuteDeleteAsync(cancellationToken);
        await db.TranslationFlags.ExecuteDeleteAsync(cancellationToken);

        // Bulk deletes bypass the change tracker: forget entities loaded earlier in this scope.
        db.ChangeTracker.Clear();

        var settings = await settingsService.GetEntityAsync(cancellationToken);
        SettingsService.Apply(settings, snapshot.Settings);

        var words = snapshot.Words.Where(w => wordIds.ContainsKey(w.DeckKey)).DistinctBy(w => w.DeckKey).ToList();
        db.WordProgress.AddRange(words.Select(w => new WordProgress
        {
            WordId = wordIds[w.DeckKey],
            Phase = w.Phase,
            Ease = w.Ease,
            IntervalDays = w.IntervalDays,
            Repetitions = w.Repetitions,
            Lapses = w.Lapses,
            Step = w.Step,
            DueUtc = Utc(w.DueUtc),
            IntroducedUtc = Utc(w.IntroducedUtc),
            LastReviewedUtc = Utc(w.LastReviewedUtc),
        }));

        var reviews = snapshot.Reviews.Where(r => wordIds.ContainsKey(r.DeckKey)).ToList();
        db.ReviewLogs.AddRange(reviews.Select(r => new ReviewLog
        {
            WordId = wordIds[r.DeckKey],
            ReviewedUtc = Utc(r.ReviewedUtc),
            LocalDate = r.LocalDate,
            Grade = r.Grade,
            PhaseBefore = r.PhaseBefore,
            IntervalBefore = r.IntervalBefore,
            IntervalAfter = r.IntervalAfter,
            DurationMs = r.DurationMs,
        }));

        db.ExerciseAttempts.AddRange(snapshot.Attempts.Select(a => new ExerciseAttempt
        {
            Module = a.Module,
            ExerciseType = a.ExerciseType,
            ItemKey = a.ItemKey,
            IsCorrect = a.IsCorrect,
            ResponseMs = a.ResponseMs,
            AnsweredUtc = Utc(a.AnsweredUtc),
            LocalDate = a.LocalDate,
        }));
        db.ReadingTrials.AddRange(snapshot.ReadingTrials.Select(r => new ReadingTrial
        {
            Text = r.Text,
            SyllableCount = r.SyllableCount,
            ElapsedMs = r.ElapsedMs,
            ReadCorrectly = r.ReadCorrectly,
            CompletedUtc = Utc(r.CompletedUtc),
            LocalDate = r.LocalDate,
        }));
        db.StudySessions.AddRange(snapshot.Sessions.Select(s => new StudySession
        {
            Module = s.Module,
            StartedUtc = Utc(s.StartedUtc),
            DurationSeconds = s.DurationSeconds,
            LocalDate = s.LocalDate,
        }));
        var steps = ImportSteps(snapshot);
        db.PlanSteps.AddRange(steps);
        db.StepTaskCompletions.AddRange(ImportTicks(snapshot));
        db.WordNotes.AddRange((snapshot.WordNotes ?? [])
            .Where(n => wordIds.ContainsKey(n.DeckKey))
            .DistinctBy(n => n.DeckKey)
            .Select(n => new WordNote { WordId = wordIds[n.DeckKey], Text = n.Text, UpdatedUtc = Utc(n.UpdatedUtc) }));
        db.Mistakes.AddRange((snapshot.Mistakes ?? [])
            .DistinctBy(m => (m.Module, m.ItemKey))
            .Select(m => new Mistake
            {
                Module = m.Module,
                ExerciseType = m.ExerciseType,
                ItemKey = m.ItemKey,
                Payload = m.Payload,
                Box = m.Box,
                Lapses = m.Lapses,
                CreatedDate = m.CreatedDate,
                DueDate = m.DueDate,
                ResolvedDate = m.ResolvedDate,
            }));
        db.RuleReviews.AddRange((snapshot.RuleReviews ?? [])
            .DistinctBy(r => r.ExerciseType)
            .Select(r => new RuleReview { ExerciseType = r.ExerciseType, Box = r.Box, DueDate = r.DueDate, LastCheckedDate = r.LastCheckedDate, Lapses = r.Lapses }));
        db.TranslationFlags.AddRange((snapshot.TranslationFlags ?? [])
            .Where(f => wordIds.ContainsKey(f.DeckKey))
            .DistinctBy(f => f.DeckKey)
            .Select(f => new TranslationFlag { WordId = wordIds[f.DeckKey], Russian = f.Russian, Note = f.Note, CreatedUtc = Utc(f.CreatedUtc) }));
        db.WritingDrafts.AddRange(snapshot.WritingDrafts.Select(d => new WritingDraft
        {
            TaskNumber = d.TaskNumber,
            StartedUtc = Utc(d.StartedUtc),
            DurationSeconds = d.DurationSeconds,
            Text = d.Text,
            LocalDate = d.LocalDate,
        }));

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ImportResultDto(
            words.Count,
            snapshot.Words.Count - words.Count,
            reviews.Count,
            snapshot.Attempts.Count,
            snapshot.ReadingTrials.Count,
            snapshot.Sessions.Count,
            steps.Count,
            snapshot.WritingDrafts.Count);
    }

    /// <summary>
    /// Started steps of the plan, keeping exactly one open (the current step). A version 1 backup has none:
    /// the plan then reopens from its activity as for any learner of the calendar plan.
    /// </summary>
    private static List<PlanStepProgress> ImportSteps(ProgressExport snapshot)
    {
        var roadmap = new Roadmap(snapshot.Settings.NewWordsPerDay);
        var steps = (snapshot.PlanSteps ?? [])
            .Where(s => roadmap.Find(s.StepKey) is not null)
            .DistinctBy(s => s.StepKey)
            .Select(s => new PlanStepProgress
            {
                StepKey = s.StepKey,
                StartedUtc = Utc(s.StartedUtc),
                // A step cannot end before it began; an inverted window would hide its progress.
                CompletedUtc = s.CompletedUtc is { } c ? (Utc(c) < Utc(s.StartedUtc) ? Utc(s.StartedUtc) : Utc(c)) : null,
            })
            .ToList();
        if (steps.Count == 0)
        {
            return steps;
        }

        var furthest = steps.MaxBy(s => roadmap.Find(s.StepKey)!.Index)!;
        foreach (var step in steps.Where(s => s != furthest))
        {
            step.CompletedUtc ??= furthest.StartedUtc;
        }

        if (furthest.CompletedUtc is { } finished && roadmap.Next(roadmap.Find(furthest.StepKey)!) is { } next)
        {
            steps.Add(new PlanStepProgress { StepKey = next.Key, StartedUtc = finished });
        }
        else
        {
            furthest.CompletedUtc = null;
        }

        return steps;
    }

    /// <summary>Manual ticks; version 1 ticks were per calendar day and map to the lesson of that day.</summary>
    private static IEnumerable<StepTaskCompletion> ImportTicks(ProgressExport snapshot)
    {
        var ticks = (snapshot.StepTaskCompletions ?? []).Select(c => (c.StepKey, c.TaskKey, c.CompletedUtc));
        var legacy = (snapshot.TaskCompletions ?? [])
            .Select(c => (Day: c.Date.DayNumber - snapshot.Settings.StartDate.DayNumber + 1, c.TaskKey, c.CompletedUtc))
            .Where(c => c.Day is >= 1 and <= Roadmap.LessonCount)
            .Select(c => ($"lesson-{c.Day}", c.TaskKey, c.CompletedUtc));

        return ticks.Concat(legacy)
            .DistinctBy(c => (c.Item1, c.TaskKey))
            .Select(c => new StepTaskCompletion { StepKey = c.Item1, TaskKey = c.TaskKey, CompletedUtc = Utc(c.CompletedUtc) });
    }

    // A backup is untrusted input: every record is checked before the existing data is wiped.
    private static void Validate(ProgressExport? snapshot)
    {
        if (snapshot is null)
        {
            throw ValidationFailedException.For("file", "Файл пустой или не является экспортом KorTrainer.");
        }

        new Validator()
            .Require(snapshot.SchemaVersion is ProgressExport.CurrentSchemaVersion or ProgressExport.LegacySchemaVersion, nameof(snapshot.SchemaVersion), $"Поддерживаются версии формата {ProgressExport.LegacySchemaVersion} и {ProgressExport.CurrentSchemaVersion}.")
            .Require(snapshot.Settings is not null, nameof(snapshot.Settings), "В файле нет настроек.")
            .Require(snapshot.Words is not null && snapshot.Reviews is not null && snapshot.Attempts is not null
                && snapshot.ReadingTrials is not null && snapshot.Sessions is not null
                && snapshot.WritingDrafts is not null, "file", "В файле не хватает разделов.")
            .ThrowIfInvalid();
        SettingsService.Validate(snapshot.Settings!);

        var validator = new Validator();
        CheckSection(validator, snapshot.Words, nameof(snapshot.Words), w =>
            IsKey(w.DeckKey) && Enum.IsDefined(w.Phase) && w.Ease is >= MinEase and <= MaxEase
            && w.IntervalDays is >= 0 and <= MaxIntervalDays && w.Repetitions >= 0 && w.Lapses >= 0 && w.Step is >= 0 and <= MaxStep);
        CheckSection(validator, snapshot.Reviews, nameof(snapshot.Reviews), r =>
            IsKey(r.DeckKey) && Enum.IsDefined(r.Grade) && Enum.IsDefined(r.PhaseBefore)
            && r.IntervalBefore is >= 0 and <= MaxIntervalDays && r.IntervalAfter is >= 0 and <= MaxIntervalDays && r.DurationMs >= 0);
        CheckSection(validator, snapshot.Attempts, nameof(snapshot.Attempts), a =>
            Enum.IsDefined(a.Module) && IsKey(a.ExerciseType) && IsKey(a.ItemKey) && a.ResponseMs is >= 0 and <= MaxResponseMs);
        CheckSection(validator, snapshot.ReadingTrials, nameof(snapshot.ReadingTrials), r =>
            IsKey(r.Text) && r.SyllableCount is > 0 and <= MaxKeyLength && r.ElapsedMs is > 0 and <= MaxResponseMs);
        CheckSection(validator, snapshot.Sessions, nameof(snapshot.Sessions), s =>
            Enum.IsDefined(s.Module) && s.DurationSeconds is > 0 and <= MaxSessionSeconds);
        CheckSection(validator, snapshot.TaskCompletions, nameof(snapshot.TaskCompletions), c => IsKey(c.TaskKey));
        CheckSection(validator, snapshot.PlanSteps, nameof(snapshot.PlanSteps), s => IsKey(s.StepKey));
        CheckSection(validator, snapshot.WordNotes, nameof(snapshot.WordNotes), n => IsKey(n.DeckKey) && n.Text is { Length: > 0 and <= WordNoteLimits.MaxLength });
        CheckSection(validator, snapshot.Mistakes, nameof(snapshot.Mistakes), m =>
            Enum.IsDefined(m.Module) && IsKey(m.ExerciseType) && IsKey(m.ItemKey)
            && m.Payload is { Length: > 0 and <= MistakeLimits.MaxPayloadLength } && m.Box >= 0 && m.Lapses >= 0);
        CheckSection(validator, snapshot.RuleReviews, nameof(snapshot.RuleReviews), r =>
            Domain.Grammar.GrammarCatalog.Find(r.ExerciseType) is not null && r.Box is >= 0 and <= RuleSchedule.MaxBox && r.Lapses >= 0);
        CheckSection(validator, snapshot.TranslationFlags, nameof(snapshot.TranslationFlags), f =>
            IsKey(f.DeckKey) && f.Russian is { Length: <= TranslationFlagLimits.MaxTranslationLength }
            && (f.Note is null || f.Note.Length is > 0 and <= TranslationFlagLimits.MaxNoteLength));
        CheckSection(validator, snapshot.StepTaskCompletions, nameof(snapshot.StepTaskCompletions), c => IsKey(c.StepKey) && IsKey(c.TaskKey));
        CheckSection(validator, snapshot.WritingDrafts, nameof(snapshot.WritingDrafts), d =>
            d.TaskNumber is >= 51 and <= 54 && d.DurationSeconds is >= 0 and <= MaxSessionSeconds && d.Text is { Length: <= MaxDraftLength });
        validator.ThrowIfInvalid();
    }

    private static void CheckSection<T>(Validator validator, IReadOnlyList<T?>? items, string section, Func<T, bool> isValid)
        where T : class
    {
        if (items is null)
        {
            return;
        }

        validator.Require(items.Count <= MaxRecordsPerSection, section, $"Слишком много записей (больше {MaxRecordsPerSection}).");
        int invalid = items.Count(item => item is null || !isValid(item));
        validator.Require(invalid == 0, section, $"Некорректных записей: {invalid}.");
    }

    private static bool IsKey(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= MaxKeyLength;

    private static DateTime Utc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
