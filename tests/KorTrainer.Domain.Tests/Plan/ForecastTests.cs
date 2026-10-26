using AwesomeAssertions;
using KorTrainer.Domain.Plan;

namespace KorTrainer.Domain.Tests.Plan;

public sealed class ForecastTests
{
    private static readonly DateOnly Today = new(2026, 11, 1);

    [Theory]
    [InlineData(140, 30, 10)]
    [InlineData(30, 3, 10)]
    [InlineData(0, 0, 0)]
    public void Pace_AveragesOverTheWindowOrTheDaysSinceStart(int newWords, int daysSinceFirst, double expected) =>
        Forecast.Pace(newWords, daysSinceFirst).Should().Be(expected);

    [Fact]
    public void Project_DatesEachGoalAtTheCurrentPace()
    {
        var forecast = Forecast.Project(wordsStudied: 200, pacePerDay: 10, Today);

        forecast.Select(f => f.Goal.Words).Should().Equal(Roadmap.Level1Words, Roadmap.Level2Words, Roadmap.TopikWords);
        forecast[0].Reached.Should().BeFalse();
        forecast[0].ExpectedOn.Should().Be(Today.AddDays(66));
        forecast[1].ExpectedOn.Should().Be(Today.AddDays(136));
    }

    [Fact]
    public void Project_RoundsPartialDaysUp() =>
        Forecast.Project(wordsStudied: Roadmap.Level1Words - 5, pacePerDay: 2, Today)[0].ExpectedOn.Should().Be(Today.AddDays(3));

    [Fact]
    public void Project_ReachedGoalsHaveNoDate()
    {
        var forecast = Forecast.Project(wordsStudied: 900, pacePerDay: 5, Today);

        forecast[0].Reached.Should().BeTrue();
        forecast[0].ExpectedOn.Should().BeNull();
        forecast[1].Reached.Should().BeFalse();
    }

    [Fact]
    public void Project_WithoutPace_GivesNoDateRatherThanPressure() =>
        Forecast.Project(wordsStudied: 100, pacePerDay: 0, Today).Should().AllSatisfy(f => f.ExpectedOn.Should().BeNull());

    [Fact]
    public void WordsBy_ExtrapolatesAndIsCappedByTheDeck()
    {
        Forecast.WordsBy(new DateOnly(2026, 11, 11), wordsStudied: 200, pacePerDay: 10, Today, deckSize: 6000).Should().Be(300);
        Forecast.WordsBy(new DateOnly(2027, 4, 30), wordsStudied: 200, pacePerDay: 100, Today, deckSize: 1000).Should().Be(1000);
        Forecast.WordsBy(new DateOnly(2026, 10, 1), wordsStudied: 200, pacePerDay: 10, Today, deckSize: 6000).Should().Be(200);
    }
}
