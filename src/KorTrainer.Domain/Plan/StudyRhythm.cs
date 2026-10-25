namespace KorTrainer.Domain.Plan;

public readonly record struct StudyDay(DateOnly Date, bool Studied);

/// <summary>
/// How regularly the learner studies, shown as "4 of the last 7 days" — a gap simply lowers the count
/// for a while instead of resetting a streak.
/// </summary>
public static class StudyRhythm
{
    public const int WeekDays = 7;

    /// <summary>Days with activity among the last <paramref name="windowDays"/> days, today included.</summary>
    public static int ActiveDays(IReadOnlySet<DateOnly> activeDays, DateOnly today, int windowDays = WeekDays)
    {
        ArgumentNullException.ThrowIfNull(activeDays);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowDays);

        var first = today.AddDays(1 - windowDays);
        return activeDays.Count(d => d >= first && d <= today);
    }

    /// <summary>The last seven days, oldest first and today last, each marked studied or not.</summary>
    public static IReadOnlyList<StudyDay> Week(IReadOnlySet<DateOnly> activeDays, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(activeDays);
        return Enumerable.Range(0, WeekDays)
            .Select(i => today.AddDays(i - WeekDays + 1))
            .Select(d => new StudyDay(d, activeDays.Contains(d)))
            .ToList();
    }
}
