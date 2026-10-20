using AwesomeAssertions;
using KorTrainer.Application.Common;
using KorTrainer.Application.Reading;
using KorTrainer.Application.Topik;
using KorTrainer.Contracts;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Plan;
using KorTrainer.Infrastructure.Content;
using Microsoft.EntityFrameworkCore;

namespace KorTrainer.Application.Tests;

public sealed class ReadingAndTopikTests
{
    private static readonly EmbeddedTextLibrary Texts = new();
    private static readonly EmbeddedTopikItemBank Topik = new();
    private static readonly BundledWordIndex Index = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void BundledTexts_AreWellFormed()
    {
        Texts.Texts.Should().HaveCountGreaterThanOrEqualTo(30);
        Texts.Texts.Select(t => t.Id).Should().OnlyHaveUniqueItems();
        Texts.Texts.Should().AllSatisfy(t =>
        {
            t.Sentences.Should().NotBeEmpty().And.OnlyContain(s => s.Korean.Length > 0 && s.Russian.Length > 0);
            t.Questions.Should().OnlyContain(q => q.Options.Count == 4 && q.Answer >= 0 && q.Answer < 4);
        });
    }

    [Fact]
    public void BundledTexts_AreAlmostFullyLinkedToTheDeck()
    {
        var tokens = Texts.Texts.SelectMany(t => t.Sentences).SelectMany(s => Index.Index.Analyze(s.Korean)).ToList();

        Domain.Vocabulary.WordIndex.Coverage(tokens).Should().BeGreaterThan(0.98);
    }

    [Fact]
    public void BundledTopikItems_AreWellFormed()
    {
        Topik.Items.Should().HaveCountGreaterThanOrEqualTo(100);
        Topik.Items.Select(i => i.Id).Should().OnlyHaveUniqueItems();
        Topik.Items.Should().AllSatisfy(i =>
        {
            i.Options.Should().HaveCount(4).And.OnlyHaveUniqueItems();
            i.Answer.Should().BeInRange(0, 3);
            Topik.Instructions.Should().ContainKey(i.Format);
        });
        Topik.Items.Where(i => i.Format == ExerciseTypes.TopikCloze).Should().OnlyContain(i => i.Prompt.Contains("(   )"));
    }

    [Fact]
    public async Task Texts_MarkTheOpenStagesAndProgress()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanStepAsync("level1-1");

        var texts = await Reading(t).GetTextsAsync(Ct);

        texts.Where(x => x.Stage <= PlanStage.Level1).Should().OnlyContain(x => x.IsOpen);
        texts.Where(x => x.Stage == PlanStage.Level2).Should().OnlyContain(x => !x.IsOpen);
        texts.Should().OnlyContain(x => !x.IsRead && x.KnownPercent == 0);
    }

    [Fact]
    public async Task Text_LinksInflectedWordsToTheirDictionaryForm()
    {
        await using var t = await TestDatabase.CreateAsync();
        string id = Texts.Texts.First(x => x.Stage == PlanStage.Level1).Id;

        var text = await Reading(t).GetTextAsync(id, Ct);

        var linked = text.Sentences.SelectMany(s => s.Tokens).Where(k => k.WordId is not null).ToList();
        linked.Should().NotBeEmpty();
        linked.Should().Contain(k => k.Lemma!.EndsWith('다') && k.Text != k.Lemma, "verbs appear conjugated in the text");
        text.Questions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Text_AfterAnsweringQuestions_IsRead()
    {
        await using var t = await TestDatabase.CreateAsync();
        string id = Texts.Texts[0].Id;
        await t.Activity.RecordAttemptsAsync([new AttemptDto(StudyModule.Reading, ExerciseTypes.TextReading, $"text:{id}:0", true, 0)], Ct);

        var texts = await Reading(t).GetTextsAsync(Ct);

        texts.Single(x => x.Id == id).IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task UnknownText_IsNotFound()
    {
        await using var t = await TestDatabase.CreateAsync();

        await FluentActions.Awaiting(() => Reading(t).GetTextAsync("nope", Ct)).Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task AddToStudy_PutsTheWordIntoTheQueueNow()
    {
        await using var t = await TestDatabase.CreateAsync();
        var word = await t.Db.Words.FirstAsync(w => w.Korean == "학교", Ct);

        bool added = await t.Vocabulary.AddToStudyAsync(word.Id, Ct);
        bool again = await t.Vocabulary.AddToStudyAsync(word.Id, Ct);
        var queue = await t.Vocabulary.GetQueueAsync(20, Ct);

        added.Should().BeTrue();
        again.Should().BeFalse();
        queue.Cards.Should().Contain(c => c.WordId == word.Id);
    }

    [Theory]
    [InlineData(ExerciseTypes.Mixed)]
    [InlineData(ExerciseTypes.TopikCloze)]
    [InlineData(ExerciseTypes.TopikNotice)]
    public async Task TopikReading_BeforeLevel2_UsesLevel1Items(string format)
    {
        await using var t = await TestDatabase.CreateAsync();

        var set = await TopikService(t).GetExercisesAsync(format, 10, Ct);

        set.Module.Should().Be(StudyModule.Topik);
        set.Items.Should().NotBeEmpty().And.OnlyContain(i => i.PromptNote != null && i.Options.Count == 4);
        var ids = set.Items.Select(i => i.ItemKey["topik:".Length..]).ToHashSet();
        Topik.Items.Where(i => ids.Contains(i.Id)).Should().OnlyContain(i => i.Stage == PlanStage.Level1);
    }

    [Fact]
    public async Task TopikReading_RejectsUnknownFormats()
    {
        await using var t = await TestDatabase.CreateAsync();

        await FluentActions.Awaiting(() => TopikService(t).GetExercisesAsync("listening", 10, Ct)).Should().ThrowAsync<ValidationFailedException>();
    }

    private static ReadingService Reading(TestDatabase t) => new(t.Db, Texts, Index, t.Cursor);

    private static TopikReadingService TopikService(TestDatabase t) => new(Topik, t.Cursor, new Random(3));
}
