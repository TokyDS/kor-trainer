using AwesomeAssertions;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Grammar;
using KorTrainer.Domain.Plan;

namespace KorTrainer.Domain.Tests.Plan;

public sealed class RoadmapTests
{
    private readonly Roadmap _roadmap = new(newWordsPerDay: 10);

    [Fact]
    public void Steps_GoLessonsThenBlocksThenMockExamThenTopik()
    {
        var stages = _roadmap.Steps.Select(s => s.Stage).ToList();

        stages.Should().BeInAscendingOrder();
        _roadmap.Steps.Count(s => s.Stage is PlanStage.Hangul or PlanStage.FirstPhrases).Should().Be(Roadmap.LessonCount);
        _roadmap.Steps.Count(s => s.Stage == PlanStage.Level1).Should().Be(Roadmap.Level1Blocks);
        _roadmap.Steps.Count(s => s.Stage == PlanStage.Level2).Should().Be(Roadmap.Level2Blocks);
        _roadmap.Steps.Should().ContainSingle(s => s.Stage == PlanStage.MockExam);
        _roadmap.Steps[^1].Stage.Should().Be(PlanStage.Maintenance);
    }

    [Fact]
    public void Steps_HaveUniqueKeysAndConsecutiveIndexes()
    {
        _roadmap.Steps.Select(s => s.Key).Should().OnlyHaveUniqueItems();
        _roadmap.Steps.Select(s => s.Index).Should().Equal(Enumerable.Range(0, _roadmap.Steps.Count));
        _roadmap.Steps.Should().AllSatisfy(s => s.Tasks.Select(t => t.Key).Should().OnlyHaveUniqueItems());
    }

    [Fact]
    public void Lessons_KeepTheirNotesAndTasks()
    {
        var lesson = _roadmap.Find("lesson-2")!;

        lesson.Stage.Should().Be(PlanStage.Hangul);
        lesson.Lesson!.Day.Should().Be(2);
        lesson.Title.Should().StartWith("Урок 2");
        lesson.Tasks.Should().Contain(t => t.Target.ExerciseType == ExerciseTypes.ConsonantSeries);
        _roadmap.Find("lesson-4")!.Stage.Should().Be(PlanStage.FirstPhrases);
    }

    [Theory]
    [InlineData("level1-1", 100)]
    [InlineData("level1-8", 800)]
    [InlineData("level2-1", 900)]
    [InlineData("level2-7", 1500)]
    [InlineData("topik-15", 3000)]
    public void Blocks_AddAHundredWordsEach(string key, int words)
    {
        var block = _roadmap.Find(key)!;

        block.WordTarget.Should().Be(words);
        block.Tasks.Should().Contain(t => t.Target.Metric == TaskMetric.WordsStudied && t.Target.Count == words);
    }

    [Fact]
    public void Level1Blocks_IntroduceOneGrammarTopicEach_ThenMix()
    {
        var topics = GrammarCatalog.Topics.Where(t => t.Stage == PlanStage.Level1).Select(t => t.Type).ToList();

        var perBlock = Enumerable.Range(1, Roadmap.Level1Blocks)
            .Select(k => _roadmap.Find($"level1-{k}")!.Tasks.Single(t => t.Key == "grammar").Target.ExerciseType)
            .ToList();

        perBlock.Take(topics.Count).Should().Equal(topics);
        perBlock.Skip(topics.Count).Should().AllSatisfy(type => type.Should().BeNull("later blocks mix all topics"));
    }

    [Fact]
    public void GrammarUnlocked_GrowsWithTheSteps()
    {
        _roadmap.GrammarUnlocked(_roadmap.Find("lesson-4")!).Should().BeEmpty();
        _roadmap.GrammarUnlocked(_roadmap.Find("lesson-6")!).Select(t => t.Type)
            .Should().Equal(ExerciseTypes.Copula, ExerciseTypes.NegativeCopula);
        _roadmap.GrammarUnlocked(_roadmap.Find("level1-2")!).Select(t => t.Type)
            .Should().Contain([ExerciseTypes.TopicParticle, ExerciseTypes.SubjectParticle, ExerciseTypes.ObjectParticle])
            .And.NotContain(ExerciseTypes.PolitePresent);
        _roadmap.GrammarUnlocked(_roadmap.Steps[^1]).Should().HaveCount(GrammarCatalog.Topics.Count);
    }

    [Fact]
    public void WordUnlock_FollowsTheStage()
    {
        _roadmap.Find("lesson-2")!.WordUnlock.Should().Be(WordUnlock.None);
        _roadmap.Find("lesson-7")!.WordUnlock.Should().Be(new WordUnlock(PlanStage.FirstPhrases, 7));
        _roadmap.Find("level1-3")!.WordUnlock.MaxStage.Should().Be(PlanStage.Level1);
        _roadmap.Find("mock-exam")!.WordUnlock.MaxStage.Should().Be(PlanStage.Level2);
        _roadmap.Find("topik-1")!.WordUnlock.MaxStage.Should().Be(PlanStage.TopikPrep);
    }

    [Fact]
    public void NextAndPrevious_WalkTheList()
    {
        var first = _roadmap.First;

        first.Key.Should().Be("lesson-1");
        _roadmap.Previous(first).Should().BeNull();
        _roadmap.Next(_roadmap.Find("lesson-10")!)!.Key.Should().Be("level1-1");
        _roadmap.Next(_roadmap.Steps[^1]).Should().BeNull();
    }

    [Fact]
    public void Find_ReturnsNullForUnknownKeys() =>
        _roadmap.Find("day-42").Should().BeNull();

    [Fact]
    public void TopikBlocks_RotateTheWritingTasks()
    {
        var writing = Enumerable.Range(1, 3)
            .Select(k => _roadmap.Find($"topik-{k}")!.Tasks.Single(t => t.Key == "writing").Route)
            .ToList();

        writing.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void BlocksOfAStage_ShareItsTitle()
    {
        var block = _roadmap.Find("level2-3")!;

        block.Title.Should().Be("Блок 3 из 7");
        block.StageTitle.Should().Be("Этап 2: уровень 2");
        block.QuickLinks.Should().NotBeEmpty();
    }

    [Fact]
    public void NewWordsTask_UsesTheLearnersSetting() =>
        new Roadmap(newWordsPerDay: 15).Find("lesson-4")!.Tasks
            .Single(t => t.Target.Metric == TaskMetric.NewWords).Target.Count.Should().Be(15);

    [Fact]
    public void Maintenance_NeverRunsOut() =>
        _roadmap.Steps[^1].Tasks.Should().Contain(t => t.Target.Module == StudyModule.Grammar);
}
