namespace KorTrainer.Domain.Plan;

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
}
