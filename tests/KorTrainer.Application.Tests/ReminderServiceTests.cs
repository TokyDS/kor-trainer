using AwesomeAssertions;
using KorTrainer.Application.Today;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;

namespace KorTrainer.Application.Tests;

public sealed class ReminderServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reminder_WithoutActivityToday_AsksToStudy()
    {
        await using var t = await TestDatabase.CreateAsync();

        var reminder = await new ReminderService(t.Today).GetAsync(Ct);

        reminder.ShouldRemind.Should().BeTrue();
        reminder.Title.Should().Be("Сегодня ещё не было занятий");
        reminder.Message.Should().Contain("Осталось задач").And.Contain("Урок дня 1");
    }

    [Fact]
    public async Task Reminder_WarnsWhenTheStreakIsAtRisk()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.Activity.RecordAttemptsAsync([new AttemptDto(StudyModule.Hangul, ExerciseTypes.Compose, "c", true, 900)], Ct);
        t.Time.Advance(TimeSpan.FromDays(1));

        var reminder = await new ReminderService(t.Today).GetAsync(Ct);

        reminder.Title.Should().Be("Серия 1 дн. под угрозой");
        reminder.StreakDays.Should().Be(1);
        reminder.StudiedToday.Should().BeFalse();
    }

    [Fact]
    public async Task Reminder_AfterStudying_CountsTasksLeft()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.Activity.RecordAttemptsAsync([new AttemptDto(StudyModule.Hangul, ExerciseTypes.Compose, "c", true, 900)], Ct);

        var reminder = await new ReminderService(t.Today).GetAsync(Ct);

        reminder.StudiedToday.Should().BeTrue();
        reminder.Title.Should().StartWith("Задачи дня:");
        reminder.TasksLeft.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Reminder_BeforeTheStart_StaysQuiet()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanDayAsync(-3);

        var reminder = await new ReminderService(t.Today).GetAsync(Ct);

        reminder.ShouldRemind.Should().BeFalse();
    }
}
