using AwesomeAssertions;
using KorTrainer.Application.Common;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Srs;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Tests;

public sealed class ProgressTransferServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExportThenImport_RestoresProgressIntoAFreshDatabase()
    {
        ProgressExport snapshot;
        await using (var source = await TestDatabase.CreateAsync())
        {
            await source.SetPlanDayAsync(4);
            var cards = (await source.Vocabulary.GetQueueAsync(3, Ct)).Cards;
            foreach (var card in cards)
            {
                await source.Vocabulary.ReviewAsync(new ReviewRequest(card.WordId, Grade.Easy, 2000), Ct);
            }

            await source.Activity.RecordAttemptsAsync([new AttemptDto(StudyModule.Hangul, ExerciseTypes.Compose, "compose:한", true, 900)], Ct);
            snapshot = await source.Progress.ExportAsync(Ct);
        }

        await using var target = await TestDatabase.CreateAsync();
        var result = await target.Progress.ImportAsync(snapshot, Ct);

        result.Words.Should().Be(3);
        result.Reviews.Should().Be(3);
        result.Attempts.Should().Be(1);
        (await target.Db.WordProgress.CountAsync()).Should().Be(3);
        (await target.Db.WordProgress.Include(p => p.Word).Select(p => p.Word!.Korean).ToListAsync())
            .Should().BeEquivalentTo(snapshot.Words.Select(w => w.DeckKey.Split('#')[0]));
        (await target.Settings.GetAsync(Ct)).StartDate.Should().Be(snapshot.Settings.StartDate);
    }

    [Fact]
    public async Task Import_ReplacesExistingProgress()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanDayAsync(4);
        var empty = await t.Progress.ExportAsync(Ct);
        var card = (await t.Vocabulary.GetQueueAsync(1, Ct)).Cards.Single();
        await t.Vocabulary.ReviewAsync(new ReviewRequest(card.WordId, Grade.Good, 1000), Ct);

        await t.Progress.ImportAsync(empty, Ct);

        (await t.Db.WordProgress.CountAsync()).Should().Be(0);
        (await t.Db.ReviewLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Import_SkipsWordsMissingFromTheDeck()
    {
        await using var t = await TestDatabase.CreateAsync();
        var snapshot = await t.Progress.ExportAsync(Ct);
        var unknown = new WordProgressExport("없는말#1", CardPhase.Review, 2.5, 3, 1, 0, 0, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow);

        var result = await t.Progress.ImportAsync(snapshot with { Words = [unknown] }, Ct);

        result.Words.Should().Be(0);
        result.SkippedWords.Should().Be(1);
    }

    [Fact]
    public async Task Import_RejectsCorruptRecordsWithoutTouchingExistingData()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanDayAsync(4);
        var card = (await t.Vocabulary.GetQueueAsync(1, Ct)).Cards.Single();
        await t.Vocabulary.ReviewAsync(new ReviewRequest(card.WordId, Grade.Good, 1000), Ct);
        var snapshot = await t.Progress.ExportAsync(Ct);
        var corrupt = snapshot.Words[0] with { Phase = (CardPhase)99, Ease = double.NaN };

        var act = () => t.Progress.ImportAsync(snapshot with { Words = [corrupt], Attempts = [null!] }, Ct);

        var error = (await act.Should().ThrowAsync<ValidationFailedException>()).Which;
        error.Errors.Keys.Should().BeEquivalentTo("Words", "Attempts");
        (await t.Db.WordProgress.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Import_RejectsOutOfRangeDurations()
    {
        await using var t = await TestDatabase.CreateAsync();
        var snapshot = await t.Progress.ExportAsync(Ct);
        var now = DateTime.UtcNow;

        var act = () => t.Progress.ImportAsync(snapshot with
        {
            WritingDrafts = [new WritingDraftExport(54, now, int.MaxValue, "글", new DateOnly(2026, 10, 13))],
            ReadingTrials = [new ReadingTrialExport("나무", 2, int.MaxValue, true, now, new DateOnly(2026, 10, 13))],
        }, Ct);

        (await act.Should().ThrowAsync<ValidationFailedException>()).Which.Errors.Keys.Should().BeEquivalentTo("WritingDrafts", "ReadingTrials");
    }

    [Fact]
    public async Task Import_RejectsUnsupportedSchemaVersion()
    {
        await using var t = await TestDatabase.CreateAsync();
        var snapshot = await t.Progress.ExportAsync(Ct);

        var act = () => t.Progress.ImportAsync(snapshot with { SchemaVersion = 99 }, Ct);

        await act.Should().ThrowAsync<ValidationFailedException>();
    }
}
