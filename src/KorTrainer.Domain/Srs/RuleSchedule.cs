namespace KorTrainer.Domain.Srs;

/// <summary>Where a learned grammar rule stands: how many checks in a row it has passed and when it is due.</summary>
public readonly record struct RuleState(int Box, DateOnly Due);

/// <summary>
/// Spaced checks of grammar rules the learner has practised: a short check the next day, then after 3, 7, 14,
/// 30 days and every 60 days from then on. A check passes with four answers out of five; a failed one starts over.
/// Checks before the due day change nothing, so repeating a check the same day does not move the rule.
/// </summary>
public static class RuleSchedule
{
    public static IReadOnlyList<int> IntervalDays { get; } = [1, 3, 7, 14, 30, 60];

    /// <summary>Questions in one check of a rule.</summary>
    public const int CheckSize = 5;

    /// <summary>Passed checks are counted up to here; a backup with more is not one of ours.</summary>
    public const int MaxBox = 100;

    public static RuleState Learned(DateOnly firstDay) => new(0, firstDay.AddDays(IntervalDays[0]));

    public static bool Passed(int correct, int total) => total > 0 && correct * CheckSize >= total * (CheckSize - 1);

    public static RuleState Checked(RuleState state, int correct, int total, DateOnly today)
    {
        if (today < state.Due)
        {
            return state;
        }

        if (!Passed(correct, total))
        {
            return Learned(today);
        }

        int box = Math.Min(state.Box + 1, MaxBox);
        return new RuleState(box, today.AddDays(IntervalDays[Math.Min(box, IntervalDays.Count - 1)]));
    }
}
