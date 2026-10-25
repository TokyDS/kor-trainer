using KorTrainer.Application.Common;
using KorTrainer.Application.Settings;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Grammar;
using KorTrainer.Domain.Srs;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Grammar;

/// <summary>
/// Spaced checks of grammar rules (<see cref="RuleSchedule"/>). A rule joins once the learner has answered
/// <see cref="MinAnswers"/> of its exercises; its first check is due the day after the first practice.
/// The check itself is an ordinary drill of the topic; this service only keeps the schedule.
/// </summary>
public sealed class RuleReviewService(IAppDbContext db, SettingsService settingsService, StudyClock clock)
{
    /// <summary>Answers that make a topic a practised rule rather than a look ahead.</summary>
    public const int MinAnswers = 5;

    public async Task<RuleReviewSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var today = await TodayAsync(cancellationToken);
        var topics = (await StatesAsync(cancellationToken))
            .Select(x => ToDto(x.Topic, x.State, today))
            .OrderByDescending(x => x.IsDue)
            .ThenBy(x => x.Due)
            .ToList();
        return new RuleReviewSummaryDto(topics.Count(x => x.IsDue), topics);
    }

    /// <summary>Moves the rule on its schedule after a check; a check before its day changes nothing.</summary>
    public async Task<RuleReviewTopicDto> RecordCheckAsync(string type, RuleCheckRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var topic = GrammarCatalog.Find(type);
        new Validator()
            .Require(topic is not null, nameof(type), "Неизвестная тема грамматики.")
            .Require(request.Total is >= 1 and <= GrammarService.MaxCount, nameof(request.Total), $"Вопросов в проверке: от 1 до {GrammarService.MaxCount}.")
            .Require(request.Correct >= 0 && request.Correct <= request.Total, nameof(request.Correct), "Верных ответов не может быть больше, чем вопросов.")
            .ThrowIfInvalid();

        var today = await TodayAsync(cancellationToken);
        var row = await db.RuleReviews.SingleOrDefaultAsync(r => r.ExerciseType == type, cancellationToken);
        if (row is null && await db.ExerciseAttempts.CountAsync(a => a.Module == StudyModule.Grammar && a.ExerciseType == type, cancellationToken) < MinAnswers)
        {
            throw ValidationFailedException.For(nameof(type), "Эта тема ещё не изучалась: сначала упражнения по ней.");
        }

        var state = row is not null
            ? new RuleState(row.Box, row.DueDate)
            : RuleSchedule.Learned(await FirstPracticeAsync(type, cancellationToken) ?? today);
        var next = RuleSchedule.Checked(state, request.Correct, request.Total, today);
        if (next == state)
        {
            return ToDto(topic!, state, today);
        }

        if (row is null)
        {
            row = new RuleReview { ExerciseType = type };
            db.RuleReviews.Add(row);
        }

        row.Box = next.Box;
        row.DueDate = next.Due;
        row.LastCheckedDate = today;
        row.Lapses += RuleSchedule.Passed(request.Correct, request.Total) ? 0 : 1;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(topic!, next, today);
    }

    /// <summary>Every practised rule with its schedule; a rule never checked starts from its first practice.</summary>
    private async Task<List<(GrammarTopic Topic, RuleState State)>> StatesAsync(CancellationToken cancellationToken)
    {
        var rows = await db.RuleReviews.AsNoTracking().ToDictionaryAsync(r => r.ExerciseType, cancellationToken);
        var practice = await PracticeDaysAsync(cancellationToken);
        var states = new List<(GrammarTopic, RuleState)>();
        foreach (var topic in GrammarCatalog.Topics)
        {
            if (rows.TryGetValue(topic.Type, out var row))
            {
                states.Add((topic, new RuleState(row.Box, row.DueDate)));
            }
            else if (practice.TryGetValue(topic.Type, out var days) && days.Sum(d => d.Answers) >= MinAnswers)
            {
                states.Add((topic, RuleSchedule.Learned(days.Min(d => d.Date))));
            }
        }

        return states;
    }

    private async Task<Dictionary<string, List<(DateOnly Date, int Answers)>>> PracticeDaysAsync(CancellationToken cancellationToken) =>
        (await db.ExerciseAttempts
            .Where(a => a.Module == StudyModule.Grammar)
            .GroupBy(a => new { a.ExerciseType, a.LocalDate })
            .Select(g => new { g.Key.ExerciseType, g.Key.LocalDate, Answers = g.Count() })
            .ToListAsync(cancellationToken))
        .GroupBy(x => x.ExerciseType, StringComparer.Ordinal)
        .ToDictionary(g => g.Key, g => g.Select(x => (x.LocalDate, x.Answers)).ToList(), StringComparer.Ordinal);

    private Task<DateOnly?> FirstPracticeAsync(string type, CancellationToken cancellationToken) =>
        db.ExerciseAttempts
            .Where(a => a.Module == StudyModule.Grammar && a.ExerciseType == type)
            .OrderBy(a => a.LocalDate)
            .Select(a => (DateOnly?)a.LocalDate)
            .FirstOrDefaultAsync(cancellationToken);

    private static RuleReviewTopicDto ToDto(GrammarTopic topic, RuleState state, DateOnly today) =>
        new(topic.Type, topic.Title, state.Box, state.Due, state.Due <= today);

    private async Task<DateOnly> TodayAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetEntityAsync(cancellationToken);
        return StudyClock.LocalDate(settings, clock.UtcNow);
    }
}
