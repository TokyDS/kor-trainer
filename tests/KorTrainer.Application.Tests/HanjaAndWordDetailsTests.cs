using AwesomeAssertions;
using KorTrainer.Application.Common;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Srs;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Tests;

public sealed class HanjaAndWordDetailsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void BundledGlossary_CoversTheCommonRoots()
    {
        TestDatabase.Glossary.Glosses.Should().HaveCountGreaterThan(1000);
        TestDatabase.Glossary.Glosses['學'].Hun.Should().Be("배울");
    }

    [Fact]
    public void BundledSentences_AreLinkedToDeckWords()
    {
        TestDatabase.Sentences.Sentences.Should().HaveCountGreaterThan(1000);
        TestDatabase.Sentences.ForWord("학교#1").Should().NotBeEmpty()
            .And.OnlyContain(s => s.Korean.Length > 0 && s.Russian.Length > 0);
    }

    [Fact]
    public async Task Roots_PutTheLearnersOwnRootsFirst()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.StudyAsync(0, "학생", "학교");

        var roots = await t.Hanja.GetRootsAsync(Ct);

        roots[0].Character.Should().Be("學");
        roots[0].StudiedWords.Should().Be(2);
        roots[0].TotalWords.Should().BeGreaterThan(10);
        roots[0].Examples.Take(2).Should().BeEquivalentTo(["학생", "학교"]);
    }

    [Fact]
    public async Task Root_ListsTheWholeFamily()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.StudyAsync(0, "학생");

        var root = await t.Hanja.GetRootAsync("學", Ct);

        root.Root.Russian.Should().NotBeNullOrEmpty();
        root.Words.Should().Contain(w => w.Korean == "대학교");
        root.Words[0].Studied.Should().BeTrue();
    }

    [Fact]
    public async Task Root_RejectsBadInput()
    {
        await using var t = await TestDatabase.CreateAsync();

        await FluentActions.Awaiting(() => t.Hanja.GetRootAsync("ab", Ct)).Should().ThrowAsync<ValidationFailedException>();
        await FluentActions.Awaiting(() => t.Hanja.GetRootAsync("龘", Ct)).Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData(ExerciseTypes.HanjaRoot)]
    [InlineData(ExerciseTypes.HanjaMeaning)]
    public async Task Exercises_UseUnlockedWordsWhileFewAreStudied(string type)
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanStepAsync("level1-2");

        var set = await t.Hanja.GetExercisesAsync(type, 10, Ct);

        set.Module.Should().Be(StudyModule.Hanja);
        set.Items.Should().HaveCount(10).And.OnlyContain(i => i.Options.Count == 4);
    }

    [Fact]
    public async Task Exercises_BeforeAnyWords_ExplainWhy()
    {
        await using var t = await TestDatabase.CreateAsync();

        var act = () => t.Hanja.GetExercisesAsync(ExerciseTypes.HanjaRoot, 10, Ct);

        (await act.Should().ThrowAsync<ValidationFailedException>()).Which.Errors["type"][0].Should().StartWith("Пока мало слов");
    }

    [Fact]
    public async Task Details_ShowRootsAndExamples()
    {
        await using var t = await TestDatabase.CreateAsync();
        var word = (await t.StudyAsync(0, "학교"))[0];

        var details = await t.WordDetails.GetAsync(word.Id, Ct);

        details.Hanja.Should().Be("學校");
        details.Roots.Select(r => r.Character).Should().Equal("學", "校");
        details.Roots[0].FamilySize.Should().BeGreaterThan(10);
        details.Examples.Should().NotBeEmpty();
        details.HardReason.Should().BeNull();
    }

    [Fact]
    public async Task Note_IsSavedChangedAndRemoved()
    {
        await using var t = await TestDatabase.CreateAsync();
        var word = (await t.StudyAsync(0, "학교"))[0];

        await t.WordDetails.SaveNoteAsync(word.Id, new SaveNoteRequest("  хак-кё — «хак» учёбы  "), Ct);
        var saved = await t.WordDetails.GetAsync(word.Id, Ct);
        await t.WordDetails.SaveNoteAsync(word.Id, new SaveNoteRequest(""), Ct);
        var removed = await t.WordDetails.GetAsync(word.Id, Ct);

        saved.Note.Should().Be("хак-кё — «хак» учёбы");
        removed.Note.Should().BeNull();
    }

    [Fact]
    public async Task Note_TooLongOrUnknownWord_IsRejected()
    {
        await using var t = await TestDatabase.CreateAsync();

        await FluentActions.Awaiting(() => t.WordDetails.SaveNoteAsync(1, new SaveNoteRequest(new string('a', 301)), Ct)).Should().ThrowAsync<ValidationFailedException>();
        await FluentActions.Awaiting(() => t.WordDetails.SaveNoteAsync(999_999, new SaveNoteRequest("x"), Ct)).Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HardWords_ListWordsWithLapsesOrRepeatedAgain()
    {
        await using var t = await TestDatabase.CreateAsync();
        var lapsed = (await t.StudyAsync(2, "학교"))[0];
        var again = (await t.StudyAsync(0, "친구"))[0];
        await t.StudyAsync(0, "의자");
        for (int i = 0; i < 3; i++)
        {
            t.Db.ReviewLogs.Add(new ReviewLog { WordId = again.Id, Grade = Grade.Again, ReviewedUtc = t.Clock.UtcNow.AddMinutes(i), LocalDate = new DateOnly(2026, 10, 13) });
        }

        await t.Db.SaveChangesAsync(Ct);
        await t.WordDetails.SaveNoteAsync(lapsed.Id, new SaveNoteRequest("школа"), Ct);

        var hard = await t.WordDetails.GetHardWordsAsync(Ct);
        var details = await t.WordDetails.GetAsync(lapsed.Id, Ct);

        hard.Select(h => h.Korean).Should().Equal("학교", "친구");
        hard[0].Note.Should().Be("школа");
        hard[1].Reason.Should().Contain("«Снова» 3");
        details.HardReason.Should().Be("забыто 2 раза");
    }

    [Fact]
    public async Task HardDrill_ComesBackTwicePerWord_AndKeepsTheSchedule()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.StudyAsync(0, "의자", "책", "친구", "학생");
        var word = (await t.StudyAsync(3, "학교"))[0];
        var dueBefore = (await t.Db.WordProgress.AsNoTracking().SingleAsync(p => p.WordId == word.Id, Ct)).DueUtc;

        var meaning = await t.WordDetails.GetHardExercisesAsync(ExerciseTypes.HardMeaning, 10, Ct);
        var spelling = await t.WordDetails.GetHardExercisesAsync(ExerciseTypes.HardSpelling, 10, Ct);

        meaning.Items.Should().HaveCount(2).And.OnlyContain(i => i.Prompt == "학교");
        spelling.Items[0].Options.Select(o => o.Text).Should().Contain("학교");
        (await t.Db.WordProgress.AsNoTracking().SingleAsync(p => p.WordId == word.Id, Ct)).DueUtc.Should().Be(dueBefore);
    }

    [Fact]
    public async Task HardDrill_WithoutHardWords_SaysSo()
    {
        await using var t = await TestDatabase.CreateAsync();

        var act = () => t.WordDetails.GetHardExercisesAsync(ExerciseTypes.HardMeaning, 10, Ct);

        await act.Should().ThrowAsync<ValidationFailedException>();
    }

    [Fact]
    public async Task Backup_KeepsNotes()
    {
        await using var source = await TestDatabase.CreateAsync();
        var word = (await source.StudyAsync(1, "학교"))[0];
        await source.WordDetails.SaveNoteAsync(word.Id, new SaveNoteRequest("школа"), Ct);
        var snapshot = await source.Progress.ExportAsync(Ct);

        await using var target = await TestDatabase.CreateAsync();
        await target.Progress.ImportAsync(snapshot, Ct);
        var restored = await target.Db.Words.SingleAsync(w => w.DeckKey == "학교#1", Ct);

        (await target.WordDetails.GetAsync(restored.Id, Ct)).Note.Should().Be("школа");
    }
}
