namespace KorTrainer.Domain.Srs;

/// <summary>Where a mistake stands: how many reviews it has passed, when it is due, and whether it is closed.</summary>
public readonly record struct MistakeState(int Box, DateOnly Due, DateOnly? Resolved);

/// <summary>
/// A small Leitner schedule for exercises answered wrongly: back after 1 day, then 3, then 7; the third
/// correct review closes it. A wrong answer starts over. Answers before the due day change nothing, so
/// getting an item right again in the same drill does not count as remembering it.
/// </summary>
public static class MistakeSchedule
{
    public static IReadOnlyList<int> IntervalDays { get; } = [1, 3, 7];

    public static MistakeState Missed(DateOnly today) => new(0, today.AddDays(IntervalDays[0]), null);

    public static MistakeState Answered(MistakeState state, bool correct, DateOnly today)
    {
        if (state.Resolved is not null)
        {
            return correct ? state : Missed(today);
        }

        if (!correct)
        {
            return Missed(today);
        }

        if (today < state.Due)
        {
            return state;
        }

        int box = state.Box + 1;
        return box >= IntervalDays.Count
            ? state with { Box = box, Resolved = today }
            : new MistakeState(box, today.AddDays(IntervalDays[box]), null);
    }
}
