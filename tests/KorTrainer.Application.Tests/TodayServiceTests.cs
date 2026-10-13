using AwesomeAssertions;
using KorTrainer.Application.Common;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Plan;

namespace KorTrainer.Application.Tests;

public sealed class TodayServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetToday_OnStartDate_ReturnsDayOneHangulPlan()
    {
        await using var t = await TestDatabase.CreateAsync();

        var today = await t.Today.GetTodayAsync(Ct);

        today.DayNumber.Should().Be(1);
        today.Stage.Stage.Should().Be(PlanStage.Hangul);
        today.Lesson!.Day.Should().Be(1);
        today.Tasks.Should().NotBeEmpty().And.OnlyContain(task => !task.IsDone);
        today.StreakDays.Should().Be(0);
        today.StudiedToday.Should().BeFalse();
    }

    [Fact]
    public async Task GetToday_AnswersCountTowardsMatchingTaskOnly()
    {
        await using var t = await TestDatabase.CreateAsync();
        var answers = Enumerable.Range(0, 20)
            .Select(i => new AttemptDto(StudyModule.Hangul, ExerciseTypes.LetterToSound, $"letter:{i}", i % 2 == 0, 1500))
            .ToList();

        await t.Activity.RecordAttemptsAsync(answers, Ct);
        var today = await t.Today.GetTodayAsync(Ct);

        var letterTask = today.Tasks.Single(x => x.Key == ExerciseTypes.LetterToSound);
        letterTask.Progress.Should().Be(20);
        letterTask.IsDone.Should().BeTrue();
        today.Tasks.Single(x => x.Key == ExerciseTypes.SoundToLetter).Progress.Should().Be(0);
        today.StudiedToday.Should().BeTrue();
        today.StreakDays.Should().Be(1);
    }

    [Fact]
    public async Task GetToday_StreakCountsConsecutiveActiveDays()
    {
        await using var t = await TestDatabase.CreateAsync();
        var today = new DateOnly(2026, 10, 13);
        foreach (int daysAgo in new[] { 1, 2, 3, 5 })
        {
            t.Db.ReadingTrials.Add(new ReadingTrial
            {
                Text = "나무",
                SyllableCount = 2,
                ElapsedMs = 900,
                ReadCorrectly = true,
                CompletedUtc = DateTime.UtcNow,
                LocalDate = today.AddDays(-daysAgo),
            });
        }

        await t.Db.SaveChangesAsync();

        var result = await t.Today.GetTodayAsync(Ct);

        result.StreakDays.Should().Be(3);
        result.StudiedToday.Should().BeFalse();
    }

    [Fact]
    public async Task SetTaskDone_TicksAndUnticksManualTask()
    {
        await using var t = await TestDatabase.CreateAsync();
        var date = new DateOnly(2026, 10, 13);

        await t.Today.SetTaskDoneAsync("guide", new SetTaskDoneRequest(date, true), Ct);
        var ticked = await t.Today.GetTodayAsync(Ct);
        await t.Today.SetTaskDoneAsync("guide", new SetTaskDoneRequest(date, false), Ct);
        var unticked = await t.Today.GetTodayAsync(Ct);

        ticked.Tasks.Single(x => x.Key == "guide").IsDone.Should().BeTrue();
        unticked.Tasks.Single(x => x.Key == "guide").IsDone.Should().BeFalse();
    }

    [Fact]
    public async Task SetTaskDone_UnknownTask_ThrowsNotFound()
    {
        await using var t = await TestDatabase.CreateAsync();

        var act = () => t.Today.SetTaskDoneAsync("nope", new SetTaskDoneRequest(new DateOnly(2026, 10, 13), true), Ct);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task SetTaskDone_FutureDate_ThrowsValidation()
    {
        await using var t = await TestDatabase.CreateAsync();

        var act = () => t.Today.SetTaskDoneAsync("guide", new SetTaskDoneRequest(new DateOnly(2026, 10, 20), true), Ct);

        await act.Should().ThrowAsync<ValidationFailedException>();
    }

    [Fact]
    public async Task GetToday_UsesLearnerTimeZoneForDayBoundary()
    {
        // 22:30 UTC on Oct 13 is already Oct 14 in Moscow: plan day 2.
        await using var t = await TestDatabase.CreateAsync(new DateTimeOffset(2026, 10, 13, 22, 30, 0, TimeSpan.Zero));

        var today = await t.Today.GetTodayAsync(Ct);

        today.Date.Should().Be(new DateOnly(2026, 10, 14));
        today.DayNumber.Should().Be(2);
    }

    [Fact]
    public async Task GetPlan_ListsStagesAndLessons()
    {
        await using var t = await TestDatabase.CreateAsync();

        var plan = await t.Today.GetPlanAsync(Ct);

        plan.CurrentDay.Should().Be(1);
        plan.Stages.Should().HaveCount(6);
        plan.Lessons.Should().HaveCount(10);
    }
}
