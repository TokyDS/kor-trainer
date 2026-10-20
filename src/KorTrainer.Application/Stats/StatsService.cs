using KorTrainer.Application.Common;
using KorTrainer.Application.Settings;
using KorTrainer.Application.Vocabulary;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Plan;
using KorTrainer.Domain.Srs;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Stats;

/// <summary>Progress statistics; everything is grouped by the learner's local date stored on each record.</summary>
public sealed class StatsService(IAppDbContext db, SettingsService settingsService, VocabularyService vocabularyService, StudyClock clock)
{
    public const int MaxDays = 366;
    private const int RecentDays = 7;
    private const int MonthDays = 30;
    private const int SecondsPerMinute = 60;

    public async Task<StatsOverviewDto> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetEntityAsync(cancellationToken);
        var today = clock.Today(settings);
        var recentFrom = today.AddDays(1 - RecentDays);

        var accuracy = await db.ExerciseAttempts
            .GroupBy(a => a.Module)
            .Select(g => new ModuleAccuracyDto(
                g.Key,
                g.Count(),
                g.Count(a => a.IsCorrect),
                g.Count(a => a.LocalDate >= recentFrom),
                g.Count(a => a.LocalDate >= recentFrom && a.IsCorrect)))
            .ToListAsync(cancellationToken);

        // Flashcard answers count as vocabulary accuracy: anything but "Again" is a correct recall.
        var reviewLogs = db.ReviewLogs;
        int reviewCount = await reviewLogs.CountAsync(cancellationToken);
        if (reviewCount > 0)
        {
            accuracy.Add(new ModuleAccuracyDto(
                StudyModule.Vocabulary,
                reviewCount,
                await reviewLogs.CountAsync(r => r.Grade != Grade.Again, cancellationToken),
                await reviewLogs.CountAsync(r => r.LocalDate >= recentFrom, cancellationToken),
                await reviewLogs.CountAsync(r => r.LocalDate >= recentFrom && r.Grade != Grade.Again, cancellationToken)));
        }

        long sessionSeconds = await db.StudySessions.SumAsync(s => (long)s.DurationSeconds, cancellationToken);
        long writingSeconds = await db.WritingDrafts.SumAsync(d => (long)d.DurationSeconds, cancellationToken);
        var activeDays = await db.ActiveDaysAsync(cancellationToken);

        var correctReading = db.ReadingTrials.Where(r => r.ReadCorrectly);
        long readingSyllables = await correctReading.SumAsync(r => (long)r.SyllableCount, cancellationToken);
        long readingMs = await correctReading.SumAsync(r => (long)r.ElapsedMs, cancellationToken);

        return new StatsOverviewDto(
            await vocabularyService.GetSummaryAsync(cancellationToken),
            // Flashcards and hard-word drills both count as vocabulary: merge them into one row.
            accuracy.GroupBy(a => a.Module)
                .Select(g => new ModuleAccuracyDto(g.Key, g.Sum(a => a.Answers), g.Sum(a => a.Correct), g.Sum(a => a.AnswersLast7Days), g.Sum(a => a.CorrectLast7Days)))
                .OrderBy(a => a.Module)
                .ToList(),
            ToMinutesRoundedUp(sessionSeconds + writingSeconds),
            activeDays.Count,
            StudyRhythm.ActiveDays(activeDays, today),
            StudyRhythm.ActiveDays(activeDays, today, MonthDays),
            readingSyllables > 0 ? Math.Round((double)readingMs / readingSyllables) : null,
            await db.WritingDrafts.CountAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<DailyStatDto>> GetDailyAsync(int days, CancellationToken cancellationToken)
    {
        new Validator().Require(days is >= 1 and <= MaxDays, nameof(days), $"Период: от 1 до {MaxDays} дней.").ThrowIfInvalid();

        var settings = await settingsService.GetEntityAsync(cancellationToken);
        var today = clock.Today(settings);
        var from = today.AddDays(1 - days);

        var sessions = await db.StudySessions.Where(s => s.LocalDate >= from)
            .GroupBy(s => s.LocalDate).Select(g => new { Date = g.Key, Seconds = g.Sum(s => (long)s.DurationSeconds) })
            .ToDictionaryAsync(x => x.Date, x => x.Seconds, cancellationToken);
        var writing = await db.WritingDrafts.Where(d => d.LocalDate >= from)
            .GroupBy(d => d.LocalDate).Select(g => new { Date = g.Key, Seconds = g.Sum(d => (long)d.DurationSeconds) })
            .ToDictionaryAsync(x => x.Date, x => x.Seconds, cancellationToken);
        var answers = await db.ExerciseAttempts.Where(a => a.LocalDate >= from)
            .GroupBy(a => a.LocalDate).Select(g => new { Date = g.Key, Total = g.Count(), Correct = g.Count(a => a.IsCorrect) })
            .ToDictionaryAsync(x => x.Date, cancellationToken);
        var reviews = await db.ReviewLogs.Where(r => r.LocalDate >= from)
            .GroupBy(r => r.LocalDate).Select(g => new { Date = g.Key, Total = g.Count(), New = g.Count(r => r.PhaseBefore == CardPhase.New) })
            .ToDictionaryAsync(x => x.Date, cancellationToken);

        return Enumerable.Range(0, days)
            .Select(i => from.AddDays(i))
            .Select(date => new DailyStatDto(
                date,
                ToMinutesRoundedUp(sessions.GetValueOrDefault(date) + writing.GetValueOrDefault(date)),
                answers.TryGetValue(date, out var a) ? a.Total : 0,
                a?.Correct ?? 0,
                reviews.TryGetValue(date, out var r) ? r.Total : 0,
                r?.New ?? 0))
            .ToList();
    }

    /// <summary>When each vocabulary goal is reached at the recent pace of new words (informational, no deadlines).</summary>
    public async Task<ForecastDto> GetForecastAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetEntityAsync(cancellationToken);
        var today = clock.Today(settings);
        var windowStart = today.AddDays(1 - Forecast.PaceWindowDays);

        int studied = await db.WordProgress.CountAsync(cancellationToken);
        int deckSize = await db.Words.CountAsync(cancellationToken);
        int recentNew = await db.ReviewLogs.CountAsync(r => r.LocalDate >= windowStart && r.PhaseBefore == CardPhase.New, cancellationToken);
        var firstNew = await db.ReviewLogs
            .Where(r => r.PhaseBefore == CardPhase.New)
            .OrderBy(r => r.LocalDate)
            .Select(r => (DateOnly?)r.LocalDate)
            .FirstOrDefaultAsync(cancellationToken);
        int daysSinceFirst = firstNew is { } first ? today.DayNumber - first.DayNumber + 1 : 0;

        double pace = Forecast.Pace(recentNew, daysSinceFirst);
        var goals = Forecast.Project(studied, pace, today)
            .Select(g => new GoalForecastDto(g.Goal.Title, g.Goal.Words, g.Reached, g.ExpectedOn))
            .ToList();
        return new ForecastDto(
            studied,
            Math.Round(pace, 1),
            settings.NewWordsPerDay,
            goals,
            Forecast.ExamDate,
            Forecast.WordsBy(Forecast.ExamDate, studied, pace, today, deckSize));
    }

    // A day with 40 seconds of practice should still show up on the chart.
    private static int ToMinutesRoundedUp(long seconds) => (int)Math.Min(int.MaxValue, (seconds + SecondsPerMinute - 1) / SecondsPerMinute);
}
