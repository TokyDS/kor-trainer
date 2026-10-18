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
    public async Task Practice_RejectsUnknownTypes()
    {
        await using var t = await TestDatabase.CreateAsync();

        await FluentActions.Awaiting(() => Service(t).GetPracticeAsync("karaoke", 10, Ct)).Should().ThrowAsync<ValidationFailedException>();
        await FluentActions.Awaiting(() => Service(t).GetPracticeAsync(ExerciseTypes.Dictation, 0, Ct)).Should().ThrowAsync<ValidationFailedException>();
    }

    private static SentenceService Service(TestDatabase t) => new(t.Db, TestDatabase.Sentences, t.Cursor, new Random(5));
}
