using AwesomeAssertions;
using KorTrainer.Domain.Srs;

namespace KorTrainer.Domain.Tests.Srs;

public sealed class SrsSchedulerTests
{
    private static readonly DateTime Now = new(2026, 10, 13, 9, 0, 0, DateTimeKind.Utc);
    private readonly SrsScheduler _scheduler = new();

    [Fact]
    public void NewCard_Again_StartsFirstLearningStep()
    {
        var result = _scheduler.Schedule(CardState.New(Now), Grade.Again, Now);

        result.Phase.Should().Be(CardPhase.Learning);
        result.Step.Should().Be(0);
        result.DueUtc.Should().Be(Now.AddMinutes(1));
    }

    [Fact]
    public void NewCard_Good_MovesToNextLearningStep()
    {
        var result = _scheduler.Schedule(CardState.New(Now), Grade.Good, Now);

        result.Phase.Should().Be(CardPhase.Learning);
        result.Step.Should().Be(1);
        result.DueUtc.Should().Be(Now.AddMinutes(10));
    }

    [Fact]
    public void NewCard_Hard_WaitsAverageOfFirstTwoSteps()
    {
        var result = _scheduler.Schedule(CardState.New(Now), Grade.Hard, Now);

        result.DueUtc.Should().Be(Now.AddMinutes(5.5));
    }

    [Fact]
    public void LearningCard_GoodOnLastStep_GraduatesWithOneDayInterval()
    {
        var learning = _scheduler.Schedule(CardState.New(Now), Grade.Good, Now);

        var result = _scheduler.Schedule(learning, Grade.Good, Now);

        result.Phase.Should().Be(CardPhase.Review);
        result.IntervalDays.Should().Be(1);
        result.Repetitions.Should().Be(1);
        result.DueUtc.Should().Be(Now.AddDays(1));
    }

    [Fact]
    public void NewCard_Easy_GraduatesImmediatelyWithFourDays()
    {
        var result = _scheduler.Schedule(CardState.New(Now), Grade.Easy, Now);

        result.Phase.Should().Be(CardPhase.Review);
        result.IntervalDays.Should().Be(4);
    }

    [Fact]
    public void ReviewCard_Good_MultipliesIntervalByEase()
    {
        var card = ReviewCard(intervalDays: 10, ease: 2.5);

        var result = _scheduler.Schedule(card, Grade.Good, Now);

        result.IntervalDays.Should().Be(25);
        result.Ease.Should().Be(2.5);
        result.DueUtc.Should().Be(Now.AddDays(25));
    }

    [Fact]
    public void ReviewCard_Hard_GrowsSlowlyAndLowersEase()
    {
        var card = ReviewCard(intervalDays: 10, ease: 2.5);

        var result = _scheduler.Schedule(card, Grade.Hard, Now);

        result.IntervalDays.Should().Be(12);
        result.Ease.Should().BeApproximately(2.35, 1e-9);
    }

    [Fact]
    public void ReviewCard_Easy_AppliesBonusAndRaisesEase()
    {
        var card = ReviewCard(intervalDays: 10, ease: 2.5);

        var result = _scheduler.Schedule(card, Grade.Easy, Now);

        result.IntervalDays.Should().Be(33);
        result.Ease.Should().BeApproximately(2.65, 1e-9);
    }

    [Fact]
    public void ReviewCard_GoodOnShortInterval_AlwaysGrowsByAtLeastOneDay()
    {
        var card = ReviewCard(intervalDays: 1, ease: 1.3);

        var result = _scheduler.Schedule(card, Grade.Good, Now);

        result.IntervalDays.Should().Be(2);
    }

    [Fact]
    public void ReviewCard_Again_LapsesIntoRelearning()
    {
        var card = ReviewCard(intervalDays: 30, ease: 2.5);

        var result = _scheduler.Schedule(card, Grade.Again, Now);

        result.Phase.Should().Be(CardPhase.Relearning);
        result.Lapses.Should().Be(1);
        result.Ease.Should().BeApproximately(2.3, 1e-9);
        result.IntervalDays.Should().Be(1);
        result.DueUtc.Should().Be(Now.AddMinutes(10));
    }

    [Fact]
    public void Ease_NeverDropsBelowMinimum()
    {
        var card = ReviewCard(intervalDays: 5, ease: 1.35);

        var result = _scheduler.Schedule(card, Grade.Again, Now);

        result.Ease.Should().Be(1.3);
    }

    [Fact]
    public void RelearningCard_Good_ReturnsToReviewWithLapseInterval()
    {
        var lapsed = _scheduler.Schedule(ReviewCard(intervalDays: 30, ease: 2.5), Grade.Again, Now);

        var result = _scheduler.Schedule(lapsed, Grade.Good, Now);

        result.Phase.Should().Be(CardPhase.Review);
        result.IntervalDays.Should().Be(1);
        result.DueUtc.Should().Be(Now.AddDays(1));
    }

    [Fact]
    public void Interval_IsCappedByMaximum()
    {
        var scheduler = new SrsScheduler(SrsOptions.Default with { MaximumIntervalDays = 100 });

        var result = scheduler.Schedule(ReviewCard(intervalDays: 90, ease: 2.5), Grade.Good, Now);

        result.IntervalDays.Should().Be(100);
    }

    [Fact]
    public void Schedule_DoesNotMutateTheInputCard()
    {
        var card = ReviewCard(intervalDays: 10, ease: 2.5);
        var copy = card with { };

        _ = _scheduler.Schedule(card, Grade.Easy, Now);

        card.Should().Be(copy);
    }

    [Fact]
    public void Preview_ReturnsDelayForEveryGrade()
    {
        var preview = _scheduler.Preview(ReviewCard(intervalDays: 10, ease: 2.5), Now);

        preview.Should().HaveCount(4);
        preview[Grade.Again].Should().Be(TimeSpan.FromMinutes(10));
        preview[Grade.Good].Should().Be(TimeSpan.FromDays(25));
    }

    [Fact]
    public void IsMature_AfterTwentyOneDays()
    {
        ReviewCard(intervalDays: 21, ease: 2.5).IsMature.Should().BeTrue();
        ReviewCard(intervalDays: 20, ease: 2.5).IsMature.Should().BeFalse();
    }

    private static CardState ReviewCard(double intervalDays, double ease) =>
        new(CardPhase.Review, ease, intervalDays, 3, 0, 0, Now);
}
