using AwesomeAssertions;
using KorTrainer.Application.Common;
using KorTrainer.Application.Stats;
using KorTrainer.Application.Topik;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Plan;
using KorTrainer.Domain.Srs;

namespace KorTrainer.Application.Tests;

public sealed class TopikAndStatsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Guide_LinksOfficialPastPapersAndListsAllTasks()
    {
        var guide = TopikService.GetGuide();

        guide.PastPapersUrl.Should().Contain("topik.go.kr");
        guide.Tasks.Select(t => t.Number).Should().Equal(51, 52, 53, 54);
    }

    [Fact]
    public async Task SaveDraft_StoresTextAndCountsTowardsWritingTask()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanDayAsync(StudyPlan.TopikPrepStart.DayNumber - StudyPlan.DefaultStartDate.DayNumber + 1);
        var now = t.Time.GetUtcNow().UtcDateTime;

        var draft = await t.Topik.SaveDraftAsync(new SaveDraftRequest(53, now.AddMinutes(-16), 16 * 60, "최근 조사에 따르면 …"), Ct);
        var drafts = await t.Topik.ListDraftsAsync(53, Ct);

        draft.CharCount.Should().Be(12);
        drafts.Should().ContainSingle().Which.Id.Should().Be(draft.Id);
    }

    [Fact]
    public async Task SaveDraft_RejectsUnknownTaskAndOversizedText()
    {
        await using var t = await TestDatabase.CreateAsync();
        var now = t.Time.GetUtcNow().UtcDateTime;

        var act = () => t.Topik.SaveDraftAsync(new SaveDraftRequest(50, now, 60, new string('가', TopikService.MaxDraftLength + 1)), Ct);

        (await act.Should().ThrowAsync<ValidationFailedException>()).Which.Errors.Keys.Should().BeEquivalentTo("TaskNumber", "Text");
    }

    [Fact]
    public async Task Overview_ReportsAccuracyMinutesAndStreaks()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanDayAsync(4);
        await t.Activity.RecordAttemptsAsync(
        [
            new AttemptDto(StudyModule.Hangul, ExerciseTypes.Compose, "a", true, 1000),
            new AttemptDto(StudyModule.Hangul, ExerciseTypes.Compose, "b", false, 1000),
        ], Ct);
        var card = (await t.Vocabulary.GetQueueAsync(1, Ct)).Cards.Single();
        await t.Vocabulary.ReviewAsync(new ReviewRequest(card.WordId, Grade.Good, 1000), Ct);
        await t.Activity.RecordSessionAsync(new SessionDto(StudyModule.Hangul, t.Time.GetUtcNow().UtcDateTime.AddMinutes(-10), 600), Ct);

        var overview = await t.Stats.GetOverviewAsync(Ct);

        var hangul = overview.Accuracy.Single(a => a.Module == StudyModule.Hangul);
        hangul.Answers.Should().Be(2);
        hangul.Correct.Should().Be(1);
        overview.Accuracy.Single(a => a.Module == StudyModule.Vocabulary).Correct.Should().Be(1);
        overview.TotalStudyMinutes.Should().Be(10);
        overview.CurrentStreak.Should().Be(1);
        overview.Vocabulary.Learning.Should().Be(1);
    }

    [Fact]
    public async Task Daily_ReturnsOneRowPerDayEndingToday()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.Activity.RecordAttemptsAsync([new AttemptDto(StudyModule.Numbers, ExerciseTypes.Money, "m", true, 900)], Ct);

        var daily = await t.Stats.GetDailyAsync(7, Ct);

        daily.Should().HaveCount(7);
        daily[^1].Date.Should().Be(new DateOnly(2026, 10, 13));
        daily[^1].Answers.Should().Be(1);
        daily[0].Answers.Should().Be(0);
    }

    [Fact]
    public async Task Forecast_UsesRecentPaceAgainstPlanGoals()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanDayAsync(4);
        foreach (var card in (await t.Vocabulary.GetQueueAsync(3, Ct)).Cards)
        {
            await t.Vocabulary.ReviewAsync(new ReviewRequest(card.WordId, Grade.Good, 1000), Ct);
        }

        var forecast = await t.Stats.GetForecastAsync(Ct);

        forecast.WordsStudied.Should().Be(3);
        forecast.PacePerDay.Should().Be(3);
        forecast.Goals.Select(g => g.Goal).Should().Equal(800, 1500, 3000);
        forecast.Goals[0].Projected.Should().Be(3 + (3 * 80));
        forecast.Goals[0].OnTrack.Should().BeFalse();
    }

    [Fact]
    public void LongestStreak_FindsTheLongestRun()
    {
        var start = new DateOnly(2026, 10, 1);
        var days = new[] { 0, 1, 2, 5, 6, 7, 8, 12 }.Select(d => start.AddDays(d)).ToHashSet();

        StatsService.LongestStreak(days).Should().Be(4);
    }
}
