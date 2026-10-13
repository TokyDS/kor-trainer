using AwesomeAssertions;
using KorTrainer.Application.Common;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Grammar;
using KorTrainer.Domain.Plan;

namespace KorTrainer.Application.Tests;

public sealed class GrammarServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetExercises_ForATopic_UsesDeckWords()
    {
        await using var t = await TestDatabase.CreateAsync();

        var set = await t.Grammar.GetExercisesAsync(ExerciseTypes.PolitePresent, 20, Ct);

        set.Module.Should().Be(StudyModule.Grammar);
        set.Items.Should().HaveCount(20).And.OnlyContain(i => i.Type == ExerciseTypes.PolitePresent);
        set.Items.Should().OnlyContain(i => i.Prompt.EndsWith('다'));
    }

    [Fact]
    public async Task GetExercises_Mixed_OnlyUsesTopicsOpenedByThePlan()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanDayAsync(6);

        var set = await t.Grammar.GetExercisesAsync(ExerciseTypes.Mixed, 50, Ct);

        var firstPhraseTopics = GrammarCatalog.Topics.Where(x => x.Stage == PlanStage.FirstPhrases).Select(x => x.Type);
        set.Items.Select(i => i.Type).Should().OnlyContain(type => firstPhraseTopics.Contains(type));
    }

    [Fact]
    public async Task GetTopics_MarksTopicsOfLaterStagesAsClosed()
    {
        await using var t = await TestDatabase.CreateAsync();
        await t.SetPlanDayAsync(15);

        var topics = await t.Grammar.GetTopicsAsync(Ct);

        topics.Single(x => x.Type == ExerciseTypes.PolitePresent).IsOpen.Should().BeTrue();
        topics.Single(x => x.Type == ExerciseTypes.Can).IsOpen.Should().BeFalse();
    }

    [Fact]
    public async Task GetExercises_RejectsUnknownTopicAndBadCount()
    {
        await using var t = await TestDatabase.CreateAsync();

        var act = () => t.Grammar.GetExercisesAsync("nope", 0, Ct);

        (await act.Should().ThrowAsync<ValidationFailedException>()).Which.Errors.Should().HaveCount(2);
    }
}
