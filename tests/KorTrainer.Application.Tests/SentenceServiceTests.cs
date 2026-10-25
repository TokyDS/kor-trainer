using AwesomeAssertions;
using KorTrainer.Application.Common;
using KorTrainer.Application.Sentences;
using KorTrainer.Domain.Common;

namespace KorTrainer.Application.Tests;

public sealed class SentenceServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Practice_PrefersSentencesMadeOfStudiedWords()
    {
        await using var t = await TestDatabase.CreateAsync();
        var studiable = TestDatabase.Sentences.Sentences.Where(s => s.WordKeys.Count <= 2).Take(20).ToList();
        var korean = studiable.SelectMany(s => s.WordKeys).Select(k => k.Split('#')[0]).Distinct().ToArray();
        await t.StudyAsync(0, korean);

        var set = await Service(t).GetPracticeAsync(ExerciseTypes.Dictation, 5, Ct);

        set.Items.Should().HaveCount(5);
        set.FromStudiedWords.Should().BeTrue();
        set.Items.Should().OnlyContain(i => TestDatabase.Sentences.Sentences.Single(s => s.Id == i.Id).WordKeys.All(k => korean.Contains(k.Split('#')[0])));
    }

    [Fact]
    public async Task Practice_ForABeginner_FallsBackToTheSimplestSentences()
    {
        await using var t = await TestDatabase.CreateAsync();

        var set = await Service(t).GetPracticeAsync(ExerciseTypes.Shadowing, 10, Ct);

        set.Items.Should().HaveCount(10).And.OnlyHaveUniqueItems(i => i.Id);
        set.FromStudiedWords.Should().BeFalse();
        set.Items.Should().OnlyContain(i => i.Korean.Length > 0 && i.Russian.Length > 0);
    }

    [Fact]
    public async Task SayWord_OffersTheLearnersWords()
    {
        await using var t = await TestDatabase.CreateAsync();
        string[] studied = ["안녕하세요", "감사합니다", "괜찮아요", "미안해요", "고마워요", "아니요"];
        await t.StudyAsync(0, studied);

        var set = await Service(t).GetPracticeAsync(ExerciseTypes.SayWord, 5, Ct);

        set.Items.Should().HaveCount(5).And.OnlyHaveUniqueItems(i => i.Korean);
        set.FromStudiedWords.Should().BeTrue();
        set.Items.Select(i => i.Korean).Should().BeSubsetOf(studied);
        set.Items.Should().OnlyContain(i => i.Russian.Length > 0);
    }

    [Fact]
    public async Task SayWord_ForABeginner_TakesTheWordsOfTheCurrentLesson()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanStepAsync("lesson-4");

        var set = await Service(t).GetPracticeAsync(ExerciseTypes.SayWord, 5, Ct);

        set.Items.Should().HaveCount(5).And.OnlyHaveUniqueItems(i => i.Korean);
        set.FromStudiedWords.Should().BeFalse();
        set.Items.Select(i => i.Korean).Should().BeSubsetOf(
            ["안녕하세요", "안녕히 가세요", "안녕히 계세요", "감사합니다", "고마워요", "죄송합니다", "미안해요", "괜찮아요", "네", "아니요"]);
    }

    [Fact]
    public async Task Practice_RejectsUnknownTypes()
    {
        await using var t = await TestDatabase.CreateAsync();

        await FluentActions.Awaiting(() => Service(t).GetPracticeAsync("karaoke", 10, Ct)).Should().ThrowAsync<ValidationFailedException>();
        await FluentActions.Awaiting(() => Service(t).GetPracticeAsync(ExerciseTypes.Dictation, 0, Ct)).Should().ThrowAsync<ValidationFailedException>();
    }

    private static SentenceService Service(TestDatabase t) => new(t.Db, TestDatabase.Sentences, t.Cursor, new Random(5));
}
