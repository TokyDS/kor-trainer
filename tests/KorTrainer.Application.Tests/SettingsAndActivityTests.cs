using AwesomeAssertions;
using KorTrainer.Application.Common;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Tests;

public sealed class SettingsAndActivityTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Settings_DefaultsMatchThePlan()
    {
        await using var t = await TestDatabase.CreateAsync();

        var settings = await t.Settings.GetAsync(Ct);

        settings.StartDate.Should().Be(new DateOnly(2026, 10, 13));
        settings.ShowRomanization.Should().BeFalse();
        settings.NativeRecordings.Should().BeFalse("one synthesized voice for everything unless the learner asks");
        settings.TimeZoneId.Should().Be("Europe/Moscow");
    }

    [Fact]
    public async Task Settings_UpdatePersistsValidValues()
    {
        await using var t = await TestDatabase.CreateAsync();
        var current = await t.Settings.GetAsync(Ct);

        await t.Settings.UpdateAsync(current with { ShowRomanization = true, Theme = ThemePreference.Dark, NewWordsPerDay = 15, NativeRecordings = true }, Ct);
        var reloaded = await t.Settings.GetAsync(Ct);

        reloaded.ShowRomanization.Should().BeTrue();
        reloaded.NativeRecordings.Should().BeTrue();
        reloaded.Theme.Should().Be(ThemePreference.Dark);
        reloaded.NewWordsPerDay.Should().Be(15);
    }

    [Fact]
    public async Task Settings_UpdateRejectsInvalidValuesWithFieldErrors()
    {
        await using var t = await TestDatabase.CreateAsync();
        var current = await t.Settings.GetAsync(Ct);

        var act = () => t.Settings.UpdateAsync(current with { TimeZoneId = "Mars/Olympus", NewWordsPerDay = 0, SpeechRate = 3 }, Ct);

        var error = (await act.Should().ThrowAsync<ValidationFailedException>()).Which;
        error.Errors.Keys.Should().BeEquivalentTo("TimeZoneId", "NewWordsPerDay", "SpeechRate");
    }

    [Fact]
    public async Task RecordAttempts_StoresLocalDateAndCapsResponseTime()
    {
        await using var t = await TestDatabase.CreateAsync();

        await t.Activity.RecordAttemptsAsync([new AttemptDto(StudyModule.Hangul, ExerciseTypes.Compose, "compose:한", true, int.MaxValue)], Ct);

        var attempt = await t.Db.ExerciseAttempts.SingleAsync();
        attempt.LocalDate.Should().Be(new DateOnly(2026, 10, 13));
        attempt.ResponseMs.Should().Be(10 * 60 * 1000);
    }

    [Fact]
    public async Task RecordAttempts_RejectsEmptyAndMalformedBatches()
    {
        await using var t = await TestDatabase.CreateAsync();

        var empty = () => t.Activity.RecordAttemptsAsync([], Ct);
        var malformed = () => t.Activity.RecordAttemptsAsync([new AttemptDto((StudyModule)42, "", "k", true, -1)], Ct);

        await empty.Should().ThrowAsync<ValidationFailedException>();
        (await malformed.Should().ThrowAsync<ValidationFailedException>()).Which.Errors.Should().HaveCount(3);
    }

    [Fact]
    public async Task RecordSession_RejectsFutureStartAndZeroDuration()
    {
        await using var t = await TestDatabase.CreateAsync();
        var now = TestDatabase.DayOneMorning.UtcDateTime;

        var act = () => t.Activity.RecordSessionAsync(new SessionDto(StudyModule.Vocabulary, now.AddHours(1), 0), Ct);

        (await act.Should().ThrowAsync<ValidationFailedException>()).Which.Errors.Keys.Should().BeEquivalentTo("DurationSeconds", "StartedUtc");
    }

    [Fact]
    public async Task ReadingStats_AverageMsPerSyllableOverCorrectTrials()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.Activity.RecordReadingTrialAsync(new ReadingTrialDto("나무", 2, 1000, true), Ct);
        await t.Activity.RecordReadingTrialAsync(new ReadingTrialDto("학교", 2, 3000, true), Ct);
        await t.Activity.RecordReadingTrialAsync(new ReadingTrialDto("어머니", 3, 9000, false), Ct);

        var stats = await t.Hangul.GetReadingStatsAsync(Ct);

        stats.Trials.Should().Be(3);
        stats.AverageMsPerSyllable.Should().Be(1000);
        stats.Accuracy.Should().BeApproximately(0.667, 0.001);
        stats.TodayMsPerSyllable.Should().Be(1000);
    }

    [Fact]
    public async Task HangulExercises_UseDeckAndValidateInput()
    {
        await using var t = await TestDatabase.CreateAsync();

        var set = await t.Hangul.GetExercisesAsync(ExerciseTypes.SoundChange, 3, 5, Ct);
        var invalid = () => t.Hangul.GetExercisesAsync("unknown", 7, 500, Ct);

        set.Items.Should().HaveCount(5);
        set.Title.Should().Contain("연음");
        (await invalid.Should().ThrowAsync<ValidationFailedException>()).Which.Errors.Should().HaveCount(3);
    }

    [Fact]
    public async Task Reading_AtBasicsLevel_UsesDeckWordsWithBasicLettersOnly()
    {
        await using var t = await TestDatabase.CreateAsync();

        var items = await t.Hangul.GetReadingAsync(1, 10, Ct);

        items.Should().HaveCount(10);
        items.Should().OnlyContain(i => i.Translation != null);
    }
}
