namespace KorTrainer.Domain.Plan;

/// <summary>A vocabulary goal of the plan: how many words should have been studied by a date.</summary>
public sealed record VocabularyGoal(DateOnly Date, string Title, int Words);

/// <param name="Projected">Words studied by the goal date if the current pace holds.</param>
/// <param name="RequiredPace">New words per day needed from today to reach the goal (0 when already reached).</param>
public sealed record GoalForecast(VocabularyGoal Goal, int Projected, double RequiredPace)
{
    public bool OnTrack => Projected >= Goal.Words;
}

/// <summary>Projects vocabulary growth against the plan's goals.</summary>
public static class Forecast
{
    /// <summary>Days of history that define the current pace.</summary>
    public const int PaceWindowDays = 14;

    public static IReadOnlyList<VocabularyGoal> Goals { get; } =
    [
        new(StudyPlan.Level1End, "Конец этапа 1 (уровень 1)", 800),
        new(StudyPlan.Level2End, "Конец этапа 2 (уровень 2)", 1500),
        new(StudyPlan.TopikPrepEnd, "TOPIK II, уровень 3", 3000),
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

    public static IReadOnlyList<GoalForecast> Project(int wordsStudied, double pacePerDay, DateOnly today, int deckSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(wordsStudied);
        ArgumentOutOfRangeException.ThrowIfNegative(pacePerDay);

        return Goals
            .Where(g => g.Date >= today)
            .Select(goal =>
            {
                int daysLeft = goal.Date.DayNumber - today.DayNumber + 1;
                int projected = (int)Math.Min(deckSize, wordsStudied + Math.Floor(pacePerDay * daysLeft));
                double required = Math.Max(0, (double)(goal.Words - wordsStudied) / daysLeft);
                return new GoalForecast(goal, projected, Math.Round(required, 1));
            })
            .ToList();
    }
}
