using AwesomeAssertions;
using KorTrainer.Application.Common;
using KorTrainer.Application.Sentences;
using KorTrainer.Application.Typing;
using KorTrainer.Domain.Typing;

namespace KorTrainer.Application.Tests;

public sealed class TypingServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RowLessons_UseOnlyTheirKeys_AndPreferRealWords()
    {
        await using var t = await TestDatabase.CreateAsync();

        var set = await Service(t).GetSetAsync(1, 20, Ct);

        set.Items.Should().HaveCount(20);
        var keys = TypingLessons.Lessons[0].Jamo.ToHashSet();
        set.Items.SelectMany(Dubeolsik.Keystrokes).Should().OnlyContain(c => keys.Contains(c));
    }

    [Fact]
    public async Task WordsLevel_StartsWithStudiedWords()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.StudyAsync(0, "학교", "학생", "친구");

        var set = await Service(t).GetSetAsync(TypingLessons.WordsLevel, 3, Ct);

        set.Items.Should().BeEquivalentTo(["학교", "학생", "친구"]);
    }

    [Fact]
    public async Task SentencesLevel_ReturnsSentences()
    {
        await using var t = await TestDatabase.CreateAsync();

        var set = await Service(t).GetSetAsync(TypingLessons.SentencesLevel, 5, Ct);

        set.Items.Should().HaveCount(5).And.OnlyContain(s => s.Length > 1);
    }

    [Fact]
    public async Task UnknownLevel_IsRejected()
    {
        await using var t = await TestDatabase.CreateAsync();

        await FluentActions.Awaiting(() => Service(t).GetSetAsync(7, 5, Ct)).Should().ThrowAsync<ValidationFailedException>();
    }

    private static TypingService Service(TestDatabase t) =>
        new(t.Db, t.Cursor, new SentenceService(t.Db, TestDatabase.Sentences, t.Cursor, new Random(1)), new Random(1));
}
