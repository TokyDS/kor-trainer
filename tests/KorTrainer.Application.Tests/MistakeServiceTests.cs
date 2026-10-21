using AwesomeAssertions;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Tests;

public sealed class MistakeServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly ChoiceExerciseDto Exercise = new(
        ExerciseTypes.Past,
        "past:가다",
        "가다",
        "идти → прошедшее",
        null,
        [new ChoiceOptionDto("갔어요", null, null), new ChoiceOptionDto("가었어요", null, null), new ChoiceOptionDto("가았어요", null, null)],
        0,
        "Прошедшее время …",
        "갔어요",
        false);

    private static AttemptDto Wrong(ChoiceExerciseDto? exercise = null) =>
        new(StudyModule.Grammar, ExerciseTypes.Past, "past:가다", false, 900, exercise ?? Exercise);

    private static AttemptDto Right => new(StudyModule.Grammar, ExerciseTypes.Past, "past:가다", true, 900);

    [Fact]
    public async Task WrongAnswer_OpensAMistakeDueTomorrow()
    {
        await using var t = await TestDatabase.CreateAsync();

        await t.Activity.RecordAttemptsAsync([Wrong()], Ct);

        (await t.Mistakes.GetSummaryAsync(Ct)).Should().Be(new MistakeSummaryDto(0, 1, 0));
        t.Time.Advance(TimeSpan.FromDays(1));
        (await t.Mistakes.GetSummaryAsync(Ct)).Due.Should().Be(1);
    }

    [Fact]
    public async Task WrongAnswerWithoutTheExercise_IsOnlyAnAttempt()
    {
        await using var t = await TestDatabase.CreateAsync();

        await t.Activity.RecordAttemptsAsync([Wrong() with { Exercise = null }], Ct);

        (await t.Db.Mistakes.CountAsync(Ct)).Should().Be(0);
    }

    [Fact]
    public async Task GetDue_ReturnsTheStoredExercise_WithOptionsReshuffled()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.Activity.RecordAttemptsAsync([Wrong()], Ct);
        t.Time.Advance(TimeSpan.FromDays(1));

        var set = await t.Mistakes.GetDueAsync(10, Ct);

        var item = set.Items.Should().ContainSingle().Subject;
        set.Title.Should().Be("Работа над ошибками");
        item.ItemKey.Should().Be("past:가다");
        item.Module.Should().Be(StudyModule.Grammar);
        item.Options[item.CorrectIndex].Text.Should().Be("갔어요");
        item.Options.Select(o => o.Text).Should().BeEquivalentTo(Exercise.Options.Select(o => o.Text));
    }

    [Fact]
    public async Task ThreeCorrectReviewsOnSchedule_CloseTheMistake()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.Activity.RecordAttemptsAsync([Wrong()], Ct);

        // Right again in the same drill: not a review yet.
        await t.Activity.RecordAttemptsAsync([Right], Ct);
        foreach (int days in new[] { 1, 3, 7 })
        {
            t.Time.Advance(TimeSpan.FromDays(days));
            (await t.Mistakes.GetSummaryAsync(Ct)).Due.Should().Be(1, $"due after {days} more days");
            await t.Activity.RecordAttemptsAsync([Right], Ct);
        }

        (await t.Mistakes.GetSummaryAsync(Ct)).Should().Be(new MistakeSummaryDto(0, 0, 1));
    }

    [Fact]
    public async Task WrongAgain_ReopensAndStartsOver()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.Activity.RecordAttemptsAsync([Wrong()], Ct);
        t.Time.Advance(TimeSpan.FromDays(1));
        await t.Activity.RecordAttemptsAsync([Right], Ct);

        await t.Activity.RecordAttemptsAsync([Wrong()], Ct);

        var mistake = await t.Db.Mistakes.SingleAsync(Ct);
        mistake.Box.Should().Be(0);
        mistake.Lapses.Should().Be(2);
    }

    [Fact]
    public async Task Mistakes_SurviveExportAndImport()
    {
        await using var source = await TestDatabase.CreateAsync();
        await source.Activity.RecordAttemptsAsync([Wrong()], Ct);
        var snapshot = await source.Progress.ExportAsync(Ct);

        await using var target = await TestDatabase.CreateAsync();
        await target.Progress.ImportAsync(snapshot, Ct);

        snapshot.Mistakes.Should().ContainSingle();
        (await target.Mistakes.GetSummaryAsync(Ct)).Open.Should().Be(1);
    }
}
