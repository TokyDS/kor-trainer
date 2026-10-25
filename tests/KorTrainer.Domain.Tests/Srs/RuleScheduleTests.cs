using AwesomeAssertions;
using KorTrainer.Domain.Srs;

namespace KorTrainer.Domain.Tests.Srs;

public sealed class RuleScheduleTests
{
    private static readonly DateOnly Day = new(2026, 10, 25);

    [Fact]
    public void Learned_ComesBackTheNextDay() =>
        RuleSchedule.Learned(Day).Should().Be(new RuleState(0, Day.AddDays(1)));

    [Fact]
    public void PassedChecks_WalkOneThreeSevenFourteenDays_ThenStayLong()
    {
        var state = RuleSchedule.Learned(Day);
        var today = Day;
        int[] expected = [3, 7, 14, 30, 60, 60];

        foreach (int interval in expected)
        {
            today = state.Due;
            state = RuleSchedule.Checked(state, correct: 5, total: 5, today);
            state.Due.Should().Be(today.AddDays(interval));
        }

        state.Box.Should().Be(expected.Length);
    }

    [Theory]
    [InlineData(4, 5, true)]
    [InlineData(3, 5, false)]
    [InlineData(8, 10, true)]
    [InlineData(0, 0, false)]
    public void Passed_NeedsFourOutOfFive(int correct, int total, bool passed) =>
        RuleSchedule.Passed(correct, total).Should().Be(passed);

    [Fact]
    public void FailedCheck_StartsOverFromOneDay()
    {
        var state = new RuleState(3, Day);

        RuleSchedule.Checked(state, correct: 2, total: 5, Day).Should().Be(new RuleState(0, Day.AddDays(1)));
    }

    [Fact]
    public void CheckBeforeTheDueDay_ChangesNothing()
    {
        var state = RuleSchedule.Learned(Day);

        RuleSchedule.Checked(state, correct: 0, total: 5, Day).Should().Be(state);
        RuleSchedule.Checked(state, correct: 5, total: 5, Day).Should().Be(state);
    }
}
