using AwesomeAssertions;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;

namespace KorTrainer.Application.Tests;

public sealed class ReminderServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reminder_WithoutStudyToday_IsAGentleNudge()
    {
        await using var t = await TestDatabase.CreateAsync();

        var reminder = await t.Reminders.GetAsync(Ct);

        reminder.ShouldRemind.Should().BeTrue();
        reminder.Title.Should().Be("Немного корейского сегодня?");
        reminder.Message.Should().Contain("Урок 1");
        reminder.Title.Should().NotContainAny("угроз", "серия", "Серия");
    }

    [Fact]
    public async Task Reminder_MentionsWordsWaitingForReview()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanStepAsync("lesson-4");
        var card = (await t.Vocabulary.GetQueueAsync(1, Ct)).Cards[0];
        await t.Vocabulary.ReviewAsync(new ReviewRequest(card.WordId, Domain.Srs.Grade.Good, 2000), Ct);
        t.Time.Advance(TimeSpan.FromDays(3));

        var reminder = await t.Reminders.GetAsync(Ct);

        reminder.Title.Should().Be("Можно позаниматься 10 минут");
        reminder.WordsDue.Should().BeGreaterThan(0);
        reminder.Message.Should().StartWith("Слов к повторению:");
    }

    [Fact]
    public async Task Reminder_AfterStudyingToday_StaysQuiet()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.Activity.RecordAttemptsAsync([new AttemptDto(StudyModule.Hangul, ExerciseTypes.Compose, "c", true, 900)], Ct);

        var reminder = await t.Reminders.GetAsync(Ct);

        reminder.ShouldRemind.Should().BeFalse();
        reminder.StudiedToday.Should().BeTrue();
        reminder.ActiveDaysThisWeek.Should().Be(1);
    }

    [Fact]
    public async Task Reminder_TurnedOff_StaysQuiet()
    {
        await using var t = await TestDatabase.CreateAsync();
        var settings = await t.Settings.GetAsync(Ct);
        await t.Settings.UpdateAsync(settings with { RemindersEnabled = false }, Ct);

        var reminder = await t.Reminders.GetAsync(Ct);

        reminder.ShouldRemind.Should().BeFalse();
    }
}
