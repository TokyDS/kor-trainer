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
        _roadmap.Steps.Where(s => s.Stage == PlanStage.MockExam).Select(s => s.Key)
            .Should().Equal("mock-exam", "final-1", "final-2");
        _roadmap.Steps.Count(s => s.Stage == PlanStage.TopikPrep).Should().Be(Roadmap.TopikPrepBlocks);
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

        rules.Select(t => t.Route).Should().OnlyHaveUniqueItems().And.HaveCount(KorTrainer.Domain.Numbers.NumberTheories.All.Count);
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
            .Should().AllSatisfy(s => Speaking(s).Should().NotContain(ExerciseTypes.SayWord, s.Key));
        _roadmap.Steps.Where(s => s.Stage == PlanStage.TopikPrep)
            .Should().AllSatisfy(s => Speaking(s).Should().Equal([ExerciseTypes.Shadowing], s.Key));
        Speaking(_roadmap.Find("level1-1")!).Should().Equal([ExerciseTypes.SayWord]);
        Speaking(_roadmap.Find("lesson-1")!).Should().BeEmpty();
    }

    [Theory]
    [InlineData("lesson-1", "ㅏ ㅓ ㅗ и слог")]
    [InlineData("lesson-5", "이에요/예요")]
    [InlineData("lesson-7", "числа 일 이 삼")]
    [InlineData("level1-1", "이/가")]
    [InlineData("mock-exam", "пробный экзамен")]
    public void TopicOf_NamesWhatTheStepIsAbout(string key, string topic) =>
        Roadmap.TopicOf(_roadmap.Find(key)!).Should().Contain(topic);

    [Fact]
    public void EveryStep_HasATopic() =>
        _roadmap.Steps.Should().AllSatisfy(s => Roadmap.TopicOf(s).Should().NotBeNullOrWhiteSpace(s.Key));

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
    [InlineData("level1-1", 110)]
    [InlineData("level1-16", 860)]
    [InlineData("level2-1", 910)]
    [InlineData("level2-14", 1560)]
    [InlineData("topik-30", 3060)]
    public void Blocks_AskForFiftyNewWords_OnTopOfTheLessons(string key, int deck)
    {
        var block = _roadmap.Find(key)!;

        block.WordTarget.Should().Be(deck);
        block.Tasks.Should().Contain(t => t.Target.Metric == TaskMetric.NewWords && t.Target.Count == Roadmap.WordsPerBlock);
        block.Tasks.Should().NotContain(t => t.Target.Metric == TaskMetric.WordsStudied, "a deck total is reached before the block even starts");
    }

    [Theory]
    [InlineData(PlanStage.Level1, "level1", Roadmap.Level1Blocks)]
    [InlineData(PlanStage.Level2, "level2", Roadmap.Level2Blocks)]
    public void LevelBlocks_IntroduceEveryStageTopicInOrder_AtMostOnePerBlock(PlanStage stage, string prefix, int blocks)
    {
        var topics = GrammarCatalog.Topics.Where(t => t.Stage == stage).Select(t => t.Type).ToList();

        var steps = Enumerable.Range(1, blocks).Select(k => _roadmap.Find($"{prefix}-{k}")!).ToList();
        var perBlock = steps.Select(s => s.Tasks.Single(t => t.Target.Module == StudyModule.Grammar)).ToList();

        perBlock.Should().AllSatisfy(task => task.Key.Should().Be("grammar"));
        perBlock.Select(t => t.Target.ExerciseType).OfType<string>().Should().Equal(topics);
        steps.Where(s => s.Tasks.Any(t => t.Key == "grammar" && t.Target.ExerciseType is not null))
            .Should().AllSatisfy(s => s.Tasks.Select(t => t.Key).Should().ContainInOrder(["rule", "grammar"], s.Key));
        perBlock[0].Target.ExerciseType.Should().Be(topics[0], "a stage opens with a new rule");
        perBlock[^1].Target.ExerciseType.Should().BeNull("the last block of a stage reviews all of its grammar");
    }

    [Fact]
    public void LevelBlocks_StayShort()
    {
        var blocks = _roadmap.Steps.Where(s => s.Stage is PlanStage.Level1 or PlanStage.Level2).ToList();

        blocks.Should().AllSatisfy(b => b.Tasks.Count.Should().BeLessThanOrEqualTo(7, b.Key));
        blocks.SelectMany(b => b.Tasks).Where(t => t.Target.Metric == TaskMetric.Answers)
            .Should().AllSatisfy(t => t.Target.Count.Should().BeLessThanOrEqualTo(15, t.Key));
    }

    [Fact]
    public void LevelBlocks_BringTextsHanjaAndTopikReading_Gradually()
    {
        var level1 = _roadmap.Steps.Where(s => s.Stage == PlanStage.Level1).ToList();
        var level2 = _roadmap.Steps.Where(s => s.Stage == PlanStage.Level2).ToList();
        static bool Has(PlanStep step, string key) => step.Tasks.Any(t => t.Key == key);

        level1.Count(s => Has(s, "text")).Should().BeGreaterThanOrEqualTo(level1.Count / 3);
        level1.Take(4).Should().NotContain(s => s.Tasks.Any(t => t.Key == "hanja"));
        level1.Skip(4).Should().Contain(s => s.Tasks.Any(t => t.Key == "hanja"));
        level1.Take(7).Should().NotContain(s => s.Tasks.Any(t => t.Key == "topik"));
        level1.Skip(7).Should().Contain(s => s.Tasks.Any(t => t.Key == "topik"));
        level2.Count(s => Has(s, "topik")).Should().BeGreaterThanOrEqualTo(level2.Count / 2);
        _roadmap.Find("lesson-10")!.Tasks.Should().Contain(t => t.Key == "text");
    }

    [Theory]
    [InlineData("level1-6", ExerciseTypes.Time)]
    [InlineData("level1-11", ExerciseTypes.Money)]
    public void ReviewBlocks_TeachTimeAndMoney(string key, string type)
    {
        var tasks = _roadmap.Find(key)!.Tasks;

        tasks.Should().Contain(t => t.Target.Material == StudyMaterials.NumberRule(type));
        tasks.Should().Contain(t => t.Target.Metric == TaskMetric.Answers && t.Target.ExerciseType == type);
    }

    [Fact]
    public void NumbersLeaveTheTopicBlocks()
    {
        var topicBlocks = _roadmap.Steps
            .Where(s => s.Stage is PlanStage.Level1 or PlanStage.Level2)
            .Where(s => s.Tasks.Any(t => t.Key == "grammar" && t.Target.ExerciseType is not null));

        topicBlocks.Should().AllSatisfy(s => s.Tasks.Should().NotContain(t => t.Target.Module == StudyModule.Numbers, s.Key));
    }

    [Fact]
    public void MockExam_IsTopikI_ThenWeakSpotsAndALastWeek()
    {
        var mock = _roadmap.Find("mock-exam")!;

        mock.Title.Should().Contain("TOPIK I");
        mock.Tasks.Should().NotContain(t => t.Target.Metric == TaskMetric.WritingMinutes, "TOPIK I has no writing");
        mock.Tasks.Should().Contain(t => t.Target.Module == StudyModule.Topik && t.Target.Count == 40);
        mock.Tasks.Should().Contain(t => t.Target.Module == StudyModule.Listening && t.Target.Count == 30);
        _roadmap.Next(mock)!.Key.Should().Be("final-1");
        _roadmap.Next(_roadmap.Find("final-2")!)!.Key.Should().Be("topik-1");
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
            .Should().Contain(ExerciseTypes.ObjectParticle)
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
    public void TopikBlocks_AlternateWritingAndReading_AndRotateTheWritingTasks()
    {
        var writing = new[] { 1, 3, 5 }
            .Select(k => _roadmap.Find($"topik-{k}")!.Tasks.Single(t => t.Key == "writing").Route)
            .ToList();

        writing.Should().OnlyHaveUniqueItems();
        _roadmap.Find("topik-2")!.Tasks.Should().Contain(t => t.Key == "reading").And.NotContain(t => t.Key == "writing");
    }

    [Fact]
    public void BlocksOfAStage_ShareItsTitle()
    {
        var block = _roadmap.Find("level2-3")!;

        block.Title.Should().Be("Блок 3 из 14");
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
