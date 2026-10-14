using AwesomeAssertions;
using KorTrainer.Application.Common;
using KorTrainer.Contracts;
using KorTrainer.Domain.Plan;
using KorTrainer.Domain.Srs;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Tests;

public sealed class VocabularyServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetQueue_DuringHangulDays_OffersNoWords()
    {
        await using var t = await TestDatabase.CreateAsync();

        var queue = await t.Vocabulary.GetQueueAsync(50, Ct);

        queue.Cards.Should().BeEmpty();
        queue.NewRemainingToday.Should().Be(0);
    }

    [Fact]
    public async Task GetQueue_OnDayFour_OffersOnlyGreetingsLessonWords()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanDayAsync(4);

        var queue = await t.Vocabulary.GetQueueAsync(50, Ct);

        queue.Cards.Should().HaveCount(10);
        queue.Cards.Should().OnlyContain(c => c.Topic == "Приветствия" && c.Phase == CardPhase.New);
        queue.Cards[0].Korean.Should().Be("안녕하세요");
        queue.Cards[0].Previews.Select(p => p.Interval).Should().Equal("1 мин", "6 мин", "10 мин", "4 дн");
    }

    [Fact]
    public async Task GetQueue_RespectsNewWordsPerDayAcrossTheDay()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanDayAsync(12);
        var settings = await t.GetSettingsAsync();
        settings.NewWordsPerDay = 3;
        await t.Db.SaveChangesAsync();

        var first = await t.Vocabulary.GetQueueAsync(50, Ct);
        foreach (var card in first.Cards)
        {
            await t.Vocabulary.ReviewAsync(new ReviewRequest(card.WordId, Grade.Easy, 1000), Ct);
        }

        var second = await t.Vocabulary.GetQueueAsync(50, Ct);

        first.Cards.Should().HaveCount(3);
        second.Cards.Should().BeEmpty();
        second.NewRemainingToday.Should().Be(0);
    }

    [Fact]
    public async Task Review_NewCard_CreatesProgressAndLog()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanDayAsync(4);
        var card = (await t.Vocabulary.GetQueueAsync(1, Ct)).Cards.Single();

        var result = await t.Vocabulary.ReviewAsync(new ReviewRequest(card.WordId, Grade.Good, 4200), Ct);

        result.Phase.Should().Be(CardPhase.Learning);
        result.DueUtc.Should().Be(TestDatabase.DayOneMorning.UtcDateTime.AddMinutes(10));
        var log = await t.Db.ReviewLogs.SingleAsync();
        log.PhaseBefore.Should().Be(CardPhase.New);
        log.DurationMs.Should().Be(4200);
        log.LocalDate.Should().Be(new DateOnly(2026, 10, 13));
    }

    [Fact]
    public async Task GetQueue_ShowsLearningCardsDueSoonFirst()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanDayAsync(4);
        var first = (await t.Vocabulary.GetQueueAsync(1, Ct)).Cards.Single();
        await t.Vocabulary.ReviewAsync(new ReviewRequest(first.WordId, Grade.Again, 1000), Ct);
        t.Time.Advance(TimeSpan.FromMinutes(2));

        var queue = await t.Vocabulary.GetQueueAsync(50, Ct);

        queue.Cards[0].WordId.Should().Be(first.WordId);
        queue.Cards[0].Phase.Should().Be(CardPhase.Learning);
        queue.DueCount.Should().Be(1);
    }

    [Fact]
    public async Task GetQueue_IncludesReviewsDueLaterTodayInLearnerTimeZone()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanDayAsync(4);
        var card = (await t.Vocabulary.GetQueueAsync(1, Ct)).Cards.Single();
        await t.Vocabulary.ReviewAsync(new ReviewRequest(card.WordId, Grade.Easy, 1000), Ct);
        var progress = await t.Db.WordProgress.SingleAsync();
        // Due at 23:00 Moscow time (20:00 UTC) on the same local day.
        progress.DueUtc = new DateTime(2026, 10, 13, 20, 0, 0, DateTimeKind.Utc);
        await t.Db.SaveChangesAsync();

        var queue = await t.Vocabulary.GetQueueAsync(50, Ct);

        queue.Cards.Should().Contain(c => c.WordId == card.WordId && c.Phase == CardPhase.Review);
    }

    [Fact]
    public async Task Review_UnknownWord_ThrowsNotFound()
    {
        await using var t = await TestDatabase.CreateAsync();

        var act = () => t.Vocabulary.ReviewAsync(new ReviewRequest(999_999, Grade.Good, 0), Ct);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Review_InvalidGrade_ThrowsValidation()
    {
        await using var t = await TestDatabase.CreateAsync();

        var act = () => t.Vocabulary.ReviewAsync(new ReviewRequest(1, (Grade)9, 0), Ct);

        (await act.Should().ThrowAsync<ValidationFailedException>()).Which.Errors.Should().ContainKey("Grade");
    }

    [Fact]
    public async Task GetSummary_CountsUnlockedAndIntroducedWords()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanDayAsync(5);
        var card = (await t.Vocabulary.GetQueueAsync(1, Ct)).Cards.Single();
        await t.Vocabulary.ReviewAsync(new ReviewRequest(card.WordId, Grade.Good, 1000), Ct);

        var summary = await t.Vocabulary.GetSummaryAsync(Ct);

        summary.Unlocked.Should().Be(20);
        summary.Unseen.Should().Be(19);
        summary.Learning.Should().Be(1);
        summary.NewToday.Should().Be(1);
        summary.ReviewsToday.Should().Be(1);
        summary.Total.Should().BeGreaterThan(300);
    }

    [Fact]
    public async Task ListWords_FiltersByStatusAndSearch()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanDayAsync(4);

        var unlocked = await t.Vocabulary.ListWordsAsync(null, null, WordStatus.New, null, 1, 100, Ct);
        var locked = await t.Vocabulary.ListWordsAsync(null, PlanStage.Level1, WordStatus.Locked, null, 1, 5, Ct);
        var search = await t.Vocabulary.ListWordsAsync(null, null, null, "КАРАНДАШ", 1, 10, Ct);

        unlocked.Total.Should().Be(10);
        locked.Items.Should().HaveCount(5).And.OnlyContain(w => w.Status == WordStatus.Locked);
        search.Items.Should().Contain(w => w.Korean == "연필");
        search.Items.Should().OnlyContain(w => w.Russian.Contains("карандаш", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetTopics_ListsTopicsInPlanOrder()
    {
        await using var t = await TestDatabase.CreateAsync();

        var topics = await t.Vocabulary.GetTopicsAsync(Ct);

        topics[0].Topic.Should().Be("Приветствия");
        topics.Sum(x => x.Total).Should().BeGreaterThan(300);
    }
}
