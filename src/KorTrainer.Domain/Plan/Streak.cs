namespace KorTrainer.Domain.Plan;

public static class Streak
{
    /// <summary>
    /// Consecutive study days ending today. A day without activity *yet* does not break the
    /// streak: if today is empty, counting starts from yesterday.
    /// </summary>
    public static int Calculate(IReadOnlySet<DateOnly> activeDays, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(activeDays);

        var day = activeDays.Contains(today) ? today : today.AddDays(-1);
        int streak = 0;
        while (activeDays.Contains(day))
        {
            streak++;
            day = day.AddDays(-1);
        }

        return streak;
    }
}
