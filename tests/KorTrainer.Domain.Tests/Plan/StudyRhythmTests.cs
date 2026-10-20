using AwesomeAssertions;
using KorTrainer.Domain.Plan;

namespace KorTrainer.Domain.Tests.Plan;

public sealed class StudyRhythmTests
{
    private static readonly DateOnly Today = new(2026, 10, 18);

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
