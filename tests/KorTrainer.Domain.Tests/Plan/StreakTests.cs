using AwesomeAssertions;
using KorTrainer.Domain.Plan;

namespace KorTrainer.Domain.Tests.Plan;

public sealed class StreakTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);

    [Fact]
    public void Calculate_CountsConsecutiveDaysEndingToday()
    {
        var days = Days(0, 1, 2, 4);

        Streak.Calculate(days, Today).Should().Be(3);
    }

    [Fact]
    public void Calculate_KeepsStreakWhenTodayHasNoActivityYet()
    {
        var days = Days(1, 2, 3);

        Streak.Calculate(days, Today).Should().Be(3);
    }

    [Fact]
    public void Calculate_IsZeroAfterAMissedDay()
    {
        var days = Days(2, 3);

        Streak.Calculate(days, Today).Should().Be(0);
    }

    [Fact]
    public void Calculate_IsZeroWithoutActivity() =>
        Streak.Calculate(new HashSet<DateOnly>(), Today).Should().Be(0);

    private static HashSet<DateOnly> Days(params int[] daysAgo) =>
        daysAgo.Select(d => Today.AddDays(-d)).ToHashSet();
}
