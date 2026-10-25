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

    [Fact]
    public void EveryTask_IsDoneInTheApp()
    {
        var tasks = _roadmap.Steps.SelectMany(s => s.Tasks).ToList();

        tasks.Should().AllSatisfy(t => t.Route.Should().NotBeNullOrWhiteSpace(t.Key));
        tasks.Where(t => t.Target.Metric == TaskMetric.Read)
            .Should().AllSatisfy(t => t.Target.Material.Should().NotBeNullOrWhiteSpace(t.Key));
    }

    [Theory]
    [InlineData("lesson-5", ExerciseTypes.Copula)]
    [InlineData("lesson-6", ExerciseTypes.NegativeCopula)]
    [InlineData("lesson-9", ExerciseTypes.TopicParticle)]
    public void GrammarLessons_TeachTheRuleBeforeTheDrill(string key, string topic)
    {
        var tasks = _roadmap.Find(key)!.Tasks.ToList();

        int rule = tasks.FindIndex(t => t.Target.Metric == TaskMetric.Read && t.Target.Material == topic);
        int drill = tasks.FindIndex(t => t.Target.Metric == TaskMetric.Answers && t.Target.ExerciseType == topic);

        rule.Should().BeGreaterThanOrEqualTo(0);
        drill.Should().BeGreaterThan(rule);
        tasks[rule].Route.Should().Be($"/grammar/{topic}");
        // Blazor reads a bool query parameter as true/false only.
        tasks[drill].Route.Should().Be($"/grammar/{topic}?drill=true");
    }

    [Theory]
    [InlineData("lesson-7", ExerciseTypes.SinoNumbers)]
    [InlineData("lesson-8", ExerciseTypes.NativeNumbers)]
    [InlineData("lesson-8", ExerciseTypes.Counters)]
    public void NumberLessons_TeachTheRuleBeforeTheDrill(string key, string type)
    {
        var tasks = _roadmap.Find(key)!.Tasks.ToList();

        int rule = tasks.FindIndex(t => t.Target.Material == StudyMaterials.NumberRule(type));
        int drill = tasks.FindIndex(t => t.Target.Metric == TaskMetric.Answers && t.Target.ExerciseType == type);

        rule.Should().BeGreaterThanOrEqualTo(0);
        drill.Should().Be(rule + 1);
        tasks[rule].Route.Should().Be($"/numbers/{type}");
        tasks[drill].Route.Should().Be($"/numbers/{type}?drill=true");
    }

    [Fact]
    public void NumberRules_ToRead_HaveATheory()
    {
        var rules = _roadmap.Steps.SelectMany(s => s.Tasks)
            .Where(t => t.Target.Metric == TaskMetric.Read && t.Route!.StartsWith("/numbers/", StringComparison.Ordinal))
            .ToList();

        rules.Should().HaveCount(3);
        rules.Should().AllSatisfy(t => KorTrainer.Domain.Numbers.NumberTheories.Find(t.Route!["/numbers/".Length..]).Should().NotBeNull());
    }

    [Fact]
    public void PhraseLessons_ListenAndSpeakBeforeTheReview()
    {
        foreach (var lesson in _roadmap.Steps.Where(s => s.Lesson is { Day: > Roadmap.HangulLessons }))
        {
            var keys = lesson.Tasks.Select(t => t.Key).ToList();

            keys[^3..].Should().Equal(["listening", "say-word", "vocab-due"], lesson.Key);
            lesson.Tasks[^3].Target.ExerciseType.Should().Be(ExerciseTypes.ListenMeaning);
            lesson.Tasks[^2].Target.ExerciseType.Should().Be(ExerciseTypes.SayWord);
        }
    }

    [Fact]
    public void Speaking_GrowsFromWordsToPhrases()
    {
        static IEnumerable<string?> Speaking(PlanStep step) => step.Tasks
            .Where(t => t.Target.ExerciseType is ExerciseTypes.SayWord or ExerciseTypes.Shadowing)
            .Select(t => t.Target.ExerciseType);

        _roadmap.Steps.Where(s => s.Stage is PlanStage.Level1 or PlanStage.Level2 or PlanStage.TopikPrep)
            .Should().AllSatisfy(s => s.Tasks.Should().Contain(t => t.Target.Module == StudyModule.Listening && t.Target.ExerciseType != ExerciseTypes.SayWord
                && t.Target.ExerciseType != ExerciseTypes.Shadowing, s.Key));
        _roadmap.Steps.Where(s => s.Stage is PlanStage.Level2 or PlanStage.TopikPrep)
            .Should().AllSatisfy(s => Speaking(s).Should().Equal([ExerciseTypes.Shadowing], s.Key));
        Speaking(_roadmap.Find("level1-1")!).Should().Equal([ExerciseTypes.SayWord]);
        Speaking(_roadmap.Find("lesson-1")!).Should().BeEmpty();
    }

    [Fact]
    public void GrammarRules_ToRead_HaveATheory()
    {
        var rules = _roadmap.Steps.SelectMany(s => s.Tasks)
            .Where(t => t.Target.Metric == TaskMetric.Read && t.Route!.StartsWith("/grammar/", StringComparison.Ordinal))
            .ToList();

        rules.Should().NotBeEmpty();
        rules.Should().AllSatisfy(t => GrammarTheories.Find(t.Target.Material!).Should().NotBeNull(t.Target.Material));
    }

    [Fact]
    public void Lessons_OpenWithTheirMaterialAndEndWithTheReview()
    {
        foreach (var lesson in _roadmap.Steps.Where(s => s.Lesson is not null))
        {
            var reading = lesson.Tasks.Where(t => t.Target.Metric == TaskMetric.Read).ToList();
            if (lesson.Lesson!.Day <= Roadmap.HangulLessons)
            {
                lesson.Tasks[0].Target.Metric.Should().Be(TaskMetric.Read, lesson.Key);
            }

            if (lesson.Tasks.Any(t => t.Key == "vocab-due"))
            {
                lesson.Tasks[^1].Key.Should().Be("vocab-due", lesson.Key);
            }

            reading.Select(t => t.Key).Should().OnlyHaveUniqueItems(lesson.Key);
        }
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

    [Theory]
    [InlineData(PlanStage.Level1, "level1", Roadmap.Level1Blocks)]
    [InlineData(PlanStage.Level2, "level2", Roadmap.Level2Blocks)]
    public void LevelBlocks_IntroduceEveryStageTopicInOrder_OneOrTwoPerBlock(PlanStage stage, string prefix, int blocks)
    {
        var topics = GrammarCatalog.Topics.Where(t => t.Stage == stage).Select(t => t.Type).ToList();

        var perBlock = Enumerable.Range(1, blocks)
            .Select(k => _roadmap.Find($"{prefix}-{k}")!.Tasks
                .Where(t => t.Target.Module == StudyModule.Grammar)
                .ToList())
            .ToList();

        perBlock.Should().AllSatisfy(tasks => tasks.Count.Should().BeInRange(1, 2));
        perBlock.Should().AllSatisfy(tasks => tasks[0].Key.Should().Be("grammar"));
        perBlock.SelectMany(tasks => tasks).Select(t => t.Target.ExerciseType).Should().Equal(topics);
    }

    [Fact]
    public void GrammarUnlocked_GrowsWithTheSteps()
    {
        _roadmap.GrammarUnlocked(_roadmap.Find("lesson-4")!).Should().BeEmpty();
        _roadmap.GrammarUnlocked(_roadmap.Find("lesson-6")!).Select(t => t.Type)
            .Should().Equal(ExerciseTypes.Copula, ExerciseTypes.NegativeCopula);
        _roadmap.GrammarUnlocked(_roadmap.Find("level1-1")!).Select(t => t.Type)
            .Should().Contain([ExerciseTypes.TopicParticle, ExerciseTypes.SubjectParticle])
            .And.NotContain(ExerciseTypes.ObjectParticle);
        _roadmap.GrammarUnlocked(_roadmap.Find("level1-2")!).Select(t => t.Type)
            .Should().Contain([ExerciseTypes.ObjectParticle, ExerciseTypes.PolitePresent])
            .And.NotContain(ExerciseTypes.Past);
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
