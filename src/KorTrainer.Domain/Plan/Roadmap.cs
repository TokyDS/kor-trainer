using KorTrainer.Domain.Common;
using KorTrainer.Domain.Grammar;
using static KorTrainer.Domain.Common.ExerciseTypes;

namespace KorTrainer.Domain.Plan;

/// <summary>One step of the plan, done at the learner's own pace.</summary>
/// <param name="Index">Position in <see cref="Roadmap.Steps"/>, from 0.</param>
/// <param name="Key">Stable id stored with the learner's progress: <c>lesson-3</c>, <c>level1-2</c>, <c>mock-exam</c>, …</param>
/// <param name="Title">Name within the stage: "Урок 3: …" or "Блок 2 из 8".</param>
/// <param name="WordTarget">Words in the deck the block works towards (blocks only).</param>
public sealed record PlanStep(
    int Index,
    string Key,
    PlanStage Stage,
    string StageTitle,
    string Title,
    string Goal,
    LessonInfo? Lesson,
    IReadOnlyList<PlanTask> Tasks,
    IReadOnlyList<QuickLink> QuickLinks,
    WordUnlock WordUnlock,
    int? WordTarget = null);

/// <summary>
/// The study plan towards TOPIK as an ordered list of steps instead of calendar days: ten lessons
/// (Hangul and first phrases), blocks of about a hundred words with one grammar topic each for
/// levels 1 and 2, a mock exam, TOPIK II preparation and maintenance. The learner moves on when ready.
/// </summary>
public sealed class Roadmap
{
    public const int LessonCount = 10;
    public const int HangulLessons = 3;
    public const int WordsPerBlock = 100;
    public const int Level1Blocks = 8;
    public const int Level2Blocks = 7;
    public const int TopikPrepBlocks = 15;

    /// <summary>Words studied at the end of level 1, level 2 and TOPIK II preparation.</summary>
    public const int Level1Words = Level1Blocks * WordsPerBlock;
    public const int Level2Words = Level1Words + Level2Blocks * WordsPerBlock;
    public const int TopikWords = Level2Words + TopikPrepBlocks * WordsPerBlock;

    private const int WritingRotationLength = 3;
    private const int MockReadingItems = 40;
    private const int MockListeningItems = 30;
    private const int TextAnswers = 10;

    private readonly Dictionary<string, PlanStep> _byKey;

    public Roadmap(int newWordsPerDay = 10)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newWordsPerDay);
        NewWordsPerDay = newWordsPerDay;
        Steps = Build();
        _byKey = Steps.ToDictionary(s => s.Key, StringComparer.Ordinal);
    }

    public int NewWordsPerDay { get; }

    public IReadOnlyList<PlanStep> Steps { get; }

    public PlanStep First => Steps[0];

    public static IReadOnlyList<LessonInfo> Lessons => PlanContent.Lessons;

    public static string StageGoal(PlanStage stage) => PlanContent.StageGoal(stage);

    public static string StageTitle(PlanStage stage) => PlanContent.StageTitle(stage);

    public PlanStep? Find(string key) => _byKey.GetValueOrDefault(key);

    public PlanStep? Next(PlanStep step) => step.Index + 1 < Steps.Count ? Steps[step.Index + 1] : null;

    public PlanStep? Previous(PlanStep step) => step.Index > 0 ? Steps[step.Index - 1] : null;

    /// <summary>Grammar topics practiced by this step or an earlier one, in catalog order.</summary>
    public IReadOnlyList<GrammarTopic> GrammarUnlocked(PlanStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        var introduced = Steps
            .Take(step.Index + 1)
            .SelectMany(s => s.Tasks)
            .Where(t => t.Target.Module == StudyModule.Grammar && t.Target.ExerciseType is not null)
            .Select(t => t.Target.ExerciseType!)
            .ToHashSet(StringComparer.Ordinal);

        // Every topic of a finished level counts as introduced, even if a block mixed it in.
        return GrammarCatalog.Topics
            .Where(t => introduced.Contains(t.Type) || (t.Stage < step.Stage && step.Stage > PlanStage.FirstPhrases))
            .ToList();
    }

    private List<PlanStep> Build()
    {
        var steps = new List<PlanStep>();
        void Add(Func<int, PlanStep> create) => steps.Add(create(steps.Count));

        foreach (var lesson in PlanContent.Lessons)
        {
            Add(index => LessonStep(index, lesson));
        }

        var level1Topics = TopicsOf(PlanStage.Level1);
        for (int k = 1; k <= Level1Blocks; k++)
        {
            int block = k;
            Add(index => LevelBlock(index, PlanStage.Level1, "level1", block, Level1Blocks, block * WordsPerBlock, level1Topics,
                new PlanTask("numbers", "Числа: 15 упражнений", TaskTarget.Answers(StudyModule.Numbers, 15), "/numbers/mixed")));
        }

        var level2Topics = TopicsOf(PlanStage.Level2);
        for (int k = 1; k <= Level2Blocks; k++)
        {
            int block = k;
            Add(index => LevelBlock(index, PlanStage.Level2, "level2", block, Level2Blocks, Level1Words + block * WordsPerBlock, level2Topics,
                new PlanTask("numbers", "Время и деньги: 15 упражнений", TaskTarget.Answers(StudyModule.Numbers, 15), "/numbers/mixed")));
        }

        Add(MockExamStep);
        for (int k = 1; k <= TopikPrepBlocks; k++)
        {
            int block = k;
            Add(index => TopikBlock(index, block));
        }

        Add(MaintenanceStep);
        return steps;
    }

    private PlanStep LessonStep(int index, LessonInfo lesson)
    {
        var stage = lesson.Day <= HangulLessons ? PlanStage.Hangul : PlanStage.FirstPhrases;
        var unlock = stage == PlanStage.Hangul ? WordUnlock.None : new WordUnlock(PlanStage.FirstPhrases, lesson.Day);
        return new PlanStep(
            index,
            $"lesson-{lesson.Day}",
            stage,
            PlanContent.StageTitle(stage),
            $"Урок {lesson.Day}: {lesson.Title}",
            PlanContent.StageGoal(stage),
            lesson,
            PlanContent.LessonTasks(lesson.Day, NewWordsPerDay),
            PlanContent.QuickLinks(stage, lesson.Day),
            unlock);
    }

    private static PlanStep LevelBlock(
        int index, PlanStage stage, string prefix, int block, int blocks, int words, IReadOnlyList<GrammarTopic> topics, PlanTask numbers)
    {
        var introduced = TopicsOfBlock(topics, block, blocks);
        List<PlanTask> grammar = introduced.Count == 0
            ? [new PlanTask("grammar", "Грамматика вперемешку: 30 упражнений", TaskTarget.Answers(StudyModule.Grammar, 30), "/grammar/mixed")]
            : introduced.Select((t, i) => GrammarTask(i == 0 ? "grammar" : $"grammar-{i + 1}", t, introduced.Count == 1 ? 20 : 15)).ToList();

        // Listening in every block; speaking grows from single words (level 1) to repeated phrases (level 2);
        // timed reading every other block keeps the blocks short.
        List<PlanTask> practice = (stage, block % 2 == 1) switch
        {
            (PlanStage.Level1, true) => [CommonTasks.Reading(10), CommonTasks.ListenWords(15), CommonTasks.SayWords(10)],
            (PlanStage.Level1, false) => [CommonTasks.Listening(20), CommonTasks.Shadowing(5)],
            (_, true) => [CommonTasks.Reading(10), CommonTasks.Listening(20), CommonTasks.Shadowing(8)],
            _ => [CommonTasks.Listening(20), CommonTasks.Shadowing(8)],
        };

        string topic = introduced.Count > 0 ? $" · грамматика: {string.Join(", ", introduced.Select(t => t.Title))}" : " · повторение грамматики";
        return new PlanStep(
            index,
            $"{prefix}-{block}",
            stage,
            PlanContent.StageTitle(stage),
            $"Блок {block} из {blocks}",
            $"До {words} слов в колоде{topic}.",
            null,
            [WordsTask(words), CommonTasks.ReviewDue, .. grammar, numbers, .. practice],
            PlanContent.QuickLinks(stage, 0),
            new WordUnlock(stage, int.MaxValue),
            words);
    }

    private static PlanStep MockExamStep(int index) => new(
        index,
        "mock-exam",
        PlanStage.MockExam,
        PlanContent.StageTitle(PlanStage.MockExam),
        "Пробный экзамен",
        PlanContent.StageGoal(PlanStage.MockExam),
        null,
        [
            CommonTasks.ReviewDue,
            new("mock-topik1", "Пробное чтение TOPIK I: 40 заданий", TaskTarget.Answers(StudyModule.Topik, MockReadingItems), "/topik/reading/mixed"),
            new("mock-listening", "Пробное аудирование: 30 упражнений на слух", TaskTarget.Answers(StudyModule.Listening, MockListeningItems), "/listening"),
            new("mock-writing", "Пробное письмо TOPIK II, задания 51–54 на таймере", new TaskTarget(TaskMetric.WritingMinutes, 50), "/topik/writing?task=54"),
        ],
        PlanContent.QuickLinks(PlanStage.MockExam, 0),
        new WordUnlock(PlanStage.Level2, int.MaxValue));

    private static PlanStep TopikBlock(int index, int block)
    {
        int words = Level2Words + block * WordsPerBlock;
        var writing = (block % WritingRotationLength) switch
        {
            1 => new PlanTask("writing", "Письмо: задания 51–52 (15 минут)", new TaskTarget(TaskMetric.WritingMinutes, 15), "/topik/writing?task=51"),
            2 => new PlanTask("writing", "Письмо: задание 53, описание графика (15 минут)", new TaskTarget(TaskMetric.WritingMinutes, 15), "/topik/writing?task=53"),
            _ => new PlanTask("writing", "Письмо: эссе, задание 54 (30 минут)", new TaskTarget(TaskMetric.WritingMinutes, 30), "/topik/writing?task=54"),
        };

        return new PlanStep(
            index,
            $"topik-{block}",
            PlanStage.TopikPrep,
            PlanContent.StageTitle(PlanStage.TopikPrep),
            $"Блок {block} из {TopikPrepBlocks}",
            $"До {words} слов в колоде · письмо, чтение и аудирование TOPIK II.",
            null,
            [
                WordsTask(words),
                CommonTasks.ReviewDue,
                writing,
                new("reading", "Чтение: 10 ответов к текстам", TaskTarget.Answers(StudyModule.Reading, TextAnswers, TextReading), "/read"),
                new("listening", "Аудирование: 20 упражнений на слух", TaskTarget.Answers(StudyModule.Listening, 20), "/listening"),
                CommonTasks.Shadowing(10),
            ],
            PlanContent.QuickLinks(PlanStage.TopikPrep, 0),
            new WordUnlock(PlanStage.TopikPrep, int.MaxValue),
            words);
    }

    private static PlanStep MaintenanceStep(int index) => new(
        index,
        "maintenance",
        PlanStage.Maintenance,
        PlanContent.StageTitle(PlanStage.Maintenance),
        "Поддержание формы",
        PlanContent.StageGoal(PlanStage.Maintenance),
        null,
        [
            CommonTasks.ReviewDue,
            new("grammar", "Грамматика: 10 упражнений", TaskTarget.Answers(StudyModule.Grammar, 10), "/grammar/mixed"),
        ],
        PlanContent.QuickLinks(PlanStage.Maintenance, 0),
        new WordUnlock(PlanStage.TopikPrep, int.MaxValue));

    private static PlanTask WordsTask(int words) =>
        new("words", $"Слова: довести колоду до {words}", new TaskTarget(TaskMetric.WordsStudied, words), "/vocab/review");

    /// <summary>Spreads a stage's topics evenly over its blocks, in catalog order.</summary>
    private static List<GrammarTopic> TopicsOfBlock(IReadOnlyList<GrammarTopic> topics, int block, int blocks)
    {
        int from = (block - 1) * topics.Count / blocks;
        int to = block * topics.Count / blocks;
        return topics.Skip(from).Take(to - from).ToList();
    }

    private static PlanTask GrammarTask(string key, GrammarTopic topic, int answers) => new(
        key,
        $"Грамматика: {topic.Title} — {answers} упражнений",
        TaskTarget.Answers(StudyModule.Grammar, answers, topic.Type),
        $"/grammar/{topic.Type}");

    private static IReadOnlyList<GrammarTopic> TopicsOf(PlanStage stage) =>
        GrammarCatalog.Topics.Where(t => t.Stage == stage).ToList();
}
