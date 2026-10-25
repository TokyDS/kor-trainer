using AwesomeAssertions;
using KorTrainer.Domain.Plan;

namespace KorTrainer.Domain.Tests.Plan;

public sealed class StudyRhythmTests
{
    private static readonly DateOnly Today = new(2026, 10, 18);

    [Fact]
    public void Week_ListsTheLastSevenDaysEndingToday()
    {
        var days = new HashSet<DateOnly> { Today, Today.AddDays(-2), Today.AddDays(-9) };

        var week = StudyRhythm.Week(days, Today);

        week.Should().HaveCount(7);
        week[^1].Should().Be(new StudyDay(Today, true));
        week[^3].Studied.Should().BeTrue();
        week[0].Date.Should().Be(Today.AddDays(-6));
        week.Count(d => d.Studied).Should().Be(2, "a day nine days back is outside the week");
    }

    [Fact]
    public void ActiveDays_CountsDaysInTheWindowIncludingToday()
    {
        var days = new HashSet<DateOnly> { Today, Today.AddDays(-2), Today.AddDays(-6), Today.AddDays(-7), Today.AddDays(1) };

        StudyRhythm.ActiveDays(days, Today, windowDays: 7).Should().Be(3);
    }

    [Fact]
    public void ActiveDays_GapsDoNotResetAnything()
    {
        var days = new HashSet<DateOnly> { Today.AddDays(-1), Today.AddDays(-5) };

        StudyRhythm.ActiveDays(days, Today, windowDays: 7).Should().Be(2);
    }

    [Fact]
    public void ActiveDays_RejectsAnEmptyWindow() =>
        FluentActions.Invoking(() => StudyRhythm.ActiveDays(new HashSet<DateOnly>(), Today, 0))
            .Should().Throw<ArgumentOutOfRangeException>();
}
