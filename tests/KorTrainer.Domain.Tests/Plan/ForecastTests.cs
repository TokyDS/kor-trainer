using AwesomeAssertions;
using KorTrainer.Domain.Plan;

namespace KorTrainer.Domain.Tests.Plan;

public sealed class ForecastTests
{
    [Theory]
    [InlineData(140, 30, 10)]
    [InlineData(30, 3, 10)]
    [InlineData(0, 0, 0)]
    public void Pace_AveragesOverTheWindowOrTheDaysSinceStart(int newWords, int daysSinceFirst, double expected) =>
        Forecast.Pace(newWords, daysSinceFirst).Should().Be(expected);

    [Fact]
    public void Project_ExtrapolatesThePaceToEachRemainingGoal()
    {
        var today = new DateOnly(2026, 11, 1);

        var forecast = Forecast.Project(wordsStudied: 200, pacePerDay: 10, today, deckSize: 6000);

        forecast.Should().HaveCount(3);
        var level1 = forecast[0];
        level1.Goal.Words.Should().Be(800);
        level1.Projected.Should().Be(200 + (10 * 61));
        level1.OnTrack.Should().BeTrue();
        level1.RequiredPace.Should().Be(9.8);
    }

    [Fact]
    public void Project_IsCappedByDeckSizeAndSkipsPastGoals()
    {
        var forecast = Forecast.Project(wordsStudied: 100, pacePerDay: 100, new DateOnly(2027, 3, 1), deckSize: 1000);

        forecast.Should().ContainSingle().Which.Projected.Should().Be(1000);
    }

    [Fact]
    public void Project_ReachedGoalNeedsNoPace()
    {
        var forecast = Forecast.Project(wordsStudied: 900, pacePerDay: 0, new DateOnly(2026, 12, 1), deckSize: 6000);

        forecast[0].RequiredPace.Should().Be(0);
        forecast[0].OnTrack.Should().BeTrue();
        forecast[1].OnTrack.Should().BeFalse();
    }
}
