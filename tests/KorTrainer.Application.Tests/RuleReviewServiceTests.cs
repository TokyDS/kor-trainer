using AwesomeAssertions;
using KorTrainer.Application.Common;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Tests;

public sealed class RuleReviewServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly DayOne = new(2026, 10, 13);

    private static AttemptDto[] Answers(string type, int count, StudyModule module = StudyModule.Grammar) =>
        Enumerable.Range(0, count).Select(i => new AttemptDto(module, type, $"{type}:{i}", true, 900)).ToArray();

    [Fact]
    public async Task PractisedRule_IsCheckedTheNextDay()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.Activity.RecordAttemptsAsync(Answers(ExerciseTypes.Copula, 5), Ct);

        var summary = await t.RuleReviews.GetSummaryAsync(Ct);

        summary.Due.Should().Be(0);
        var topic = summary.Topics.Should().ContainSingle().Subject;
        topic.Type.Should().Be(ExerciseTypes.Copula);
        topic.Due.Should().Be(DayOne.AddDays(1));
        topic.IsDue.Should().BeFalse();

        t.Time.Advance(TimeSpan.FromDays(1));
        (await t.RuleReviews.GetSummaryAsync(Ct)).Due.Should().Be(1);
    }

    [Fact]
    public async Task AFewAnswers_OrAnotherModule_DoNotMakeARule()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.Activity.RecordAttemptsAsync(Answers(ExerciseTypes.Copula, 4), Ct);
        // TOPIK reading has a "topic" format too; only grammar answers count.
        await t.Activity.RecordAttemptsAsync(Answers(ExerciseTypes.TopicParticle, 5, StudyModule.Topik), Ct);

        (await t.RuleReviews.GetSummaryAsync(Ct)).Topics.Should().BeEmpty();
    }

    [Fact]
    public async Task PassedCheck_MovesTheRuleThreeDaysOn()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.Activity.RecordAttemptsAsync(Answers(ExerciseTypes.Copula, 5), Ct);
        t.Time.Advance(TimeSpan.FromDays(1));

        var topic = await t.RuleReviews.RecordCheckAsync(ExerciseTypes.Copula, new RuleCheckRequest(4, 5), Ct);

        topic.Should().Be(new RuleReviewTopicDto(ExerciseTypes.Copula, topic.Title, 1, DayOne.AddDays(4), false));
        (await t.RuleReviews.GetSummaryAsync(Ct)).Due.Should().Be(0);
    }

    [Fact]
    public async Task FailedCheck_ComesBackTomorrow_AndCountsALapse()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.Activity.RecordAttemptsAsync(Answers(ExerciseTypes.Copula, 5), Ct);
        t.Time.Advance(TimeSpan.FromDays(3));

        var topic = await t.RuleReviews.RecordCheckAsync(ExerciseTypes.Copula, new RuleCheckRequest(3, 5), Ct);

        topic.Box.Should().Be(0);
        topic.Due.Should().Be(DayOne.AddDays(4));
        (await t.Db.RuleReviews.SingleAsync(Ct)).Lapses.Should().Be(1);
    }

    [Fact]
    public async Task CheckBeforeTheDueDay_StoresNothing()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.Activity.RecordAttemptsAsync(Answers(ExerciseTypes.Copula, 5), Ct);

        var topic = await t.RuleReviews.RecordCheckAsync(ExerciseTypes.Copula, new RuleCheckRequest(5, 5), Ct);

        topic.Due.Should().Be(DayOne.AddDays(1));
        (await t.Db.RuleReviews.CountAsync(Ct)).Should().Be(0);
    }

    [Fact]
    public async Task DueRules_ComeFirst()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.Activity.RecordAttemptsAsync(Answers(ExerciseTypes.Copula, 5), Ct);
        t.Time.Advance(TimeSpan.FromDays(1));
        await t.Activity.RecordAttemptsAsync(Answers(ExerciseTypes.TopicParticle, 5), Ct);

        var summary = await t.RuleReviews.GetSummaryAsync(Ct);

        summary.Topics.Select(x => x.Type).Should().Equal(ExerciseTypes.Copula, ExerciseTypes.TopicParticle);
        summary.Due.Should().Be(1);
    }

    [Fact]
    public async Task RecordCheck_OfATopicNeverPractised_IsRejected()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.Activity.RecordAttemptsAsync(Answers(ExerciseTypes.Copula, 2), Ct);

        var act = () => t.RuleReviews.RecordCheckAsync(ExerciseTypes.Copula, new RuleCheckRequest(5, 5), Ct);

        await act.Should().ThrowAsync<ValidationFailedException>();
        (await t.Db.RuleReviews.CountAsync(Ct)).Should().Be(0);
    }

    [Theory]
    [InlineData("nope", 4, 5)]
    [InlineData(ExerciseTypes.Copula, 6, 5)]
    [InlineData(ExerciseTypes.Copula, 0, 0)]
    public async Task RecordCheck_RejectsBadInput(string type, int correct, int total)
    {
        await using var t = await TestDatabase.CreateAsync();

        var act = () => t.RuleReviews.RecordCheckAsync(type, new RuleCheckRequest(correct, total), Ct);

        await act.Should().ThrowAsync<ValidationFailedException>();
    }
}
