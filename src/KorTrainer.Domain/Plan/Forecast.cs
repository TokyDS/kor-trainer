namespace KorTrainer.Domain.Plan;

/// <summary>A vocabulary milestone of the plan.</summary>
public sealed record VocabularyGoal(string Title, int Words);

/// <param name="ExpectedOn">When the goal is reached if the recent pace holds; null when reached or without a pace.</param>
public sealed record GoalForecast(VocabularyGoal Goal, bool Reached, DateOnly? ExpectedOn);

/// <summary>Projects vocabulary growth: informational dates, never a schedule to keep up with.</summary>
public static class Forecast
{
    /// <summary>Days of history that define the current pace.</summary>
    public const int PaceWindowDays = 14;

    /// <summary>The spring 2027 TOPIK the plan was written for.</summary>
    public static readonly DateOnly ExamDate = new(2027, 4, 30);

    /// <summary>Projections further away than this are not worth a date.</summary>
    private const int MaxDaysAhead = 3650;

    public static IReadOnlyList<VocabularyGoal> Goals { get; } =
    [
        new("Уровень 1", Roadmap.Level1Words),
        new("Уровень 2", Roadmap.Level2Words),
        new("TOPIK II, уровень 3", Roadmap.TopikWords),
    ];

    /// <summary>
    /// Average new words per day over the last <see cref="PaceWindowDays"/> days, or over the days
    /// since the first new word if the learner started more recently.
    /// </summary>
    public static double Pace(int newWordsInWindow, int daysSinceFirstWord)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(newWordsInWindow);
        int days = Math.Clamp(daysSinceFirstWord, 1, PaceWindowDays);
        return (double)newWordsInWindow / days;
    }

    public static IReadOnlyList<GoalForecast> Project(int wordsStudied, double pacePerDay, DateOnly today)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(wordsStudied);
        ArgumentOutOfRangeException.ThrowIfNegative(pacePerDay);

        return Goals
            .Select(goal =>
            {
                if (wordsStudied >= goal.Words)
                {
                    return new GoalForecast(goal, true, null);
                }

                double days = pacePerDay > 0 ? Math.Ceiling((goal.Words - wordsStudied) / pacePerDay) : double.PositiveInfinity;
                return new GoalForecast(goal, false, days <= MaxDaysAhead ? today.AddDays((int)days) : null);
            })
            .ToList();
    }

    /// <summary>Words in the deck by <paramref name="date"/> at the current pace.</summary>
    public static int WordsBy(DateOnly date, int wordsStudied, double pacePerDay, DateOnly today, int deckSize)
    {
        int days = Math.Max(0, date.DayNumber - today.DayNumber);
        return (int)Math.Min(deckSize, wordsStudied + Math.Floor(pacePerDay * days));
    }
}
