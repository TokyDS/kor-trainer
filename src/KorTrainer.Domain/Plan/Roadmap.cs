using KorTrainer.Domain.Common;
using KorTrainer.Domain.Grammar;
using KorTrainer.Domain.Numbers;
using static KorTrainer.Domain.Common.ExerciseTypes;

namespace KorTrainer.Domain.Plan;

/// <summary>One step of the plan, done at the learner's own pace.</summary>
/// <param name="Index">Position in <see cref="Roadmap.Steps"/>, from 0.</param>
/// <param name="Key">Stable id stored with the learner's progress: <c>lesson-3</c>, <c>level1-2</c>, <c>mock-exam</c>, …</param>
/// <param name="Title">Name within the stage: "Урок 3: …" or "Блок 2 из 16".</param>
/// <param name="WordTarget">About how many words the deck holds at the end of the block (blocks only).</param>
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
/// The study plan towards TOPIK I as an ordered list of steps instead of calendar days: ten lessons
/// (Hangul and first phrases); blocks of fifty new words for levels 1 and 2, most of them with one new grammar
/// topic and the rest reviewing (time and money come in those); practice that rotates through texts, listening,
/// speaking, hanja and TOPIK I items; a mock TOPIK I with two steps to finish; then TOPIK II preparation and
/// maintenance. About 30–40 minutes a day; the learner moves on when ready. Keys never change meaning.
/// </summary>
public sealed class Roadmap
{
    public const int LessonCount = 10;
    public const int HangulLessons = 3;
    public const int WordsPerBlock = 50;
    public const int Level1Blocks = 16;
    public const int Level2Blocks = 14;
    public const int TopikPrepBlocks = 30;

    /// <summary>Lessons 4–9 bring new words, ten a day by default.</summary>
    public const int PhraseLessonsWithWords = 6;
    public const int LessonWords = PhraseLessonsWithWords * 10;

    /// <summary>About how many words the deck holds at the end of level 1, level 2 and TOPIK II preparation.</summary>
    public const int Level1Words = LessonWords + Level1Blocks * WordsPerBlock;
    public const int Level2Words = Level1Words + Level2Blocks * WordsPerBlock;
    public const int TopikWords = Level2Words + TopikPrepBlocks * WordsPerBlock;

    /// <summary>Hanja join once the deck has Sino-Korean words that share roots; TOPIK I items once texts are familiar.</summary>
    private const int HanjaFromBlock = 5;
    private const int TopikReadingFromBlock = 8;
    private const int PracticeCycle = 4;

    private const int WritingRotationLength = 3;
    private const int GrammarAnswers = 15;
    private const int NumberAnswers = 10;
    private const int BlockTextAnswers = 5;
    private const int BlockTopikItems = 5;
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

    /// <summary>A few words on what the step is about, for the map: «이에요/예요», «이/가, 을/를», «TOPIK II».</summary>
    public static string TopicOf(PlanStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (step.Lesson is { } lesson)
        {
            return PlanContent.LessonTopic(lesson.Day);
        }

        var grammar = step.Tasks
            .Where(t => t.Target.Module == StudyModule.Grammar && t.Target.ExerciseType is not null)
            .Select(t => GrammarCatalog.Find(t.Target.ExerciseType!)?.Title)
            .OfType<string>()
            .ToList();
        var numbers = step.Tasks
            .Where(t => t.Target.Module == StudyModule.Numbers && t.Target.Metric == TaskMetric.Answers && t.Target.ExerciseType is not null)
            .Select(t => NumberTheories.Find(t.Target.ExerciseType!)?.Title.ToLowerInvariant())
            .OfType<string>()
            .ToList();
        return step.Stage switch
        {
            PlanStage.MockExam => step.Key switch
            {
                "final-1" => "слабые места",
                "final-2" => "последняя неделя",
                _ => "пробный экзамен TOPIK I",
            },
            PlanStage.TopikPrep => "TOPIK II: письмо, чтение, аудирование",
            PlanStage.Maintenance => "поддержание формы",
            _ when grammar.Count > 0 => string.Join(", ", grammar),
            _ when numbers.Count > 0 => $"повторение · {string.Join(", ", numbers)}",
            _ => "повторение грамматики",
        };
    }

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

        int deck = PhraseLessonsWithWords * NewWordsPerDay;
        var level1Topics = TopicsOf(PlanStage.Level1);
        for (int k = 1; k <= Level1Blocks; k++)
        {
            int block = k;
            deck += WordsPerBlock;
            int words = deck;
            Add(index => LevelBlock(index, PlanStage.Level1, "level1", block, Level1Blocks, words, level1Topics));
        }

        var level2Topics = TopicsOf(PlanStage.Level2);
        for (int k = 1; k <= Level2Blocks; k++)
        {
            int block = k;
            deck += WordsPerBlock;
            int words = deck;
            Add(index => LevelBlock(index, PlanStage.Level2, "level2", block, Level2Blocks, words, level2Topics));
        }

        Add(MockExamStep);
        Add(index => FinalStep(index, 1));
        Add(index => FinalStep(index, 2));
        for (int k = 1; k <= TopikPrepBlocks; k++)
        {
            int block = k;
            deck += WordsPerBlock;
            int words = deck;
            Add(index => TopikBlock(index, block, words));
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

    /// <summary>
    /// Fifty new words and either one new grammar topic (its rule, then the drill) or, in the blocks left over,
    /// a mixed review that brings the number topics of daily life. Practice rotates over four blocks.
    /// </summary>
    private static PlanStep LevelBlock(int index, PlanStage stage, string prefix, int block, int blocks, int deck, IReadOnlyList<GrammarTopic> topics)
    {
        var topic = TopicsOfBlock(topics, block, blocks).SingleOrDefault();
        var practice = Practice(stage, block);
        List<PlanTask> tasks = [CommonTasks.NewWords(WordsPerBlock), CommonTasks.ReviewDue];
        if (topic is not null)
        {
            tasks.Add(PlanContent.Rule(topic.Type, topic.Title));
            tasks.Add(PlanContent.Grammar(topic.Type, topic.Title, GrammarAnswers) with { Key = "grammar" });
            tasks.AddRange(practice);
        }
        else
        {
            int review = Enumerable.Range(1, block - 1).Count(k => TopicsOfBlock(topics, k, blocks).Count == 0);
            tasks.Add(CommonTasks.MixedGrammar(GrammarAnswers));
            tasks.AddRange(ReviewNumbers(stage, review));
            tasks.AddRange(practice.Take(2));
        }

        string about = topic is not null ? $"грамматика: {topic.Title}" : "повторение";
        return new PlanStep(
            index,
            $"{prefix}-{block}",
            stage,
            PlanContent.StageTitle(stage),
            $"Блок {block} из {blocks}",
            $"+{WordsPerBlock} новых слов, всего около {deck} · {about}.",
            null,
            tasks,
            PlanContent.QuickLinks(stage, 0),
            new WordUnlock(stage, int.MaxValue),
            deck);
    }

    /// <summary>Level 1's first two review blocks teach telling the time and paying; later ones mix all numbers.</summary>
    private static IReadOnlyList<PlanTask> ReviewNumbers(PlanStage stage, int review) => (stage, review) switch
    {
        (PlanStage.Level1, 0) => [PlanContent.NumberRule("number-rule", ExerciseTypes.Time, "о времени"), PlanContent.Numbers(ExerciseTypes.Time, "Время", NumberAnswers)],
        (PlanStage.Level1, 1) => [PlanContent.NumberRule("number-rule", ExerciseTypes.Money, "о деньгах"), PlanContent.Numbers(ExerciseTypes.Money, "Деньги", NumberAnswers)],
        _ => [CommonTasks.MixedNumbers(NumberAnswers)],
    };

    /// <summary>
    /// Three practice tasks, rotating over <see cref="PracticeCycle"/> blocks so each block stays short: level 1 reads
    /// texts and hears and says words, adding hanja and TOPIK I items as the deck grows; level 2 alternates texts with
    /// the exam's reading format, dictation and hanja. Listening is in every block.
    /// </summary>
    private static IReadOnlyList<PlanTask> Practice(PlanStage stage, int block)
    {
        bool hanja = block >= HanjaFromBlock;
        bool topik = block >= TopikReadingFromBlock;
        return (stage, (block - 1) % PracticeCycle) switch
        {
            (PlanStage.Level1, 0) => [CommonTasks.Text(BlockTextAnswers), CommonTasks.ListenWords(10), CommonTasks.SayWords(5)],
            (PlanStage.Level1, 1) => [CommonTasks.Listening(10), CommonTasks.Reading(5), CommonTasks.Shadowing(3)],
            (PlanStage.Level1, 2) => [CommonTasks.Text(BlockTextAnswers), CommonTasks.ListenWords(10), hanja ? CommonTasks.Hanja(10) : CommonTasks.SayWords(5)],
            (PlanStage.Level1, _) => [topik ? CommonTasks.TopikReading(BlockTopikItems) : CommonTasks.Text(BlockTextAnswers), CommonTasks.Listening(10), CommonTasks.Shadowing(3)],
            (_, 0 or 2) => [CommonTasks.Text(BlockTextAnswers), CommonTasks.Listening(10), CommonTasks.Shadowing(4)],
            (_, 1) => [CommonTasks.TopikReading(BlockTopikItems), CommonTasks.Dictation(3), CommonTasks.Hanja(10)],
            _ => [CommonTasks.TopikReading(BlockTopikItems), CommonTasks.Listening(10), CommonTasks.Hanja(10)],
        };
    }

    /// <summary>TOPIK I has reading (40 items, 60 minutes) and listening (30 items, 40 minutes); there is no writing.</summary>
    private static PlanStep MockExamStep(int index) => new(
        index,
        "mock-exam",
        PlanStage.MockExam,
        PlanContent.StageTitle(PlanStage.MockExam),
        "Пробный TOPIK I",
        "Чтение и аудирование в объёме экзамена: по итогам видно, что добрать.",
        null,
        [
            CommonTasks.ReviewDue,
            new("mock-topik1", $"Пробное чтение TOPIK I: {MockReadingItems} заданий (около 60 минут)", TaskTarget.Answers(StudyModule.Topik, MockReadingItems), "/topik/reading/mixed"),
            new("mock-listening", $"Пробное аудирование: {MockListeningItems} упражнений на слух", TaskTarget.Answers(StudyModule.Listening, MockListeningItems), "/listening"),
        ],
        PlanContent.QuickLinks(PlanStage.MockExam, 0),
        new WordUnlock(PlanStage.Level2, int.MaxValue));

    /// <summary>After the mock exam: the weak spots it showed, then a light last week in the exam's format.</summary>
    private static PlanStep FinalStep(int index, int number) => number == 1
        ? new(
            index,
            "final-1",
            PlanStage.MockExam,
            PlanContent.StageTitle(PlanStage.MockExam),
            "Слабые места",
            "Темы ниже 85 % верных ответов и накопленные ошибки — «10 минут» и «Работа над ошибками» приносят их сами.",
            null,
            [CommonTasks.ReviewDue, CommonTasks.MixedGrammar(20), CommonTasks.MixedNumbers(10), CommonTasks.TopikReading(15), CommonTasks.Listening(15)],
            PlanContent.QuickLinks(PlanStage.MockExam, 0),
            new WordUnlock(PlanStage.Level2, int.MaxValue))
        : new(
            index,
            "final-2",
            PlanStage.MockExam,
            PlanContent.StageTitle(PlanStage.MockExam),
            "Последняя неделя",
            "Без новых тем: каждый день немного заданий в формате экзамена и повторение слов.",
            null,
            [CommonTasks.ReviewDue, CommonTasks.TopikReading(20), CommonTasks.Listening(20), CommonTasks.MixedGrammar(10)],
            PlanContent.QuickLinks(PlanStage.MockExam, 0),
            new WordUnlock(PlanStage.Level2, int.MaxValue));

    /// <summary>Odd blocks write (51–52, 53, 54 in turn), even blocks read; listening and shadowing in every one.</summary>
    private static PlanStep TopikBlock(int index, int block, int words)
    {
        PlanTask focus = block % 2 == 0
            ? new("reading", "Чтение: 10 ответов к текстам", TaskTarget.Answers(StudyModule.Reading, TextAnswers, TextReading), "/read")
            : ((block + 1) / 2 % WritingRotationLength) switch
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
            $"+{WordsPerBlock} новых слов, всего около {words} · письмо, чтение и аудирование TOPIK II.",
            null,
            [
                CommonTasks.NewWords(WordsPerBlock),
                CommonTasks.ReviewDue,
                focus,
                new("listening", "Аудирование: 10 упражнений на слух", TaskTarget.Answers(StudyModule.Listening, 10), "/listening"),
                CommonTasks.Shadowing(5),
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

    /// <summary>
    /// Spreads a stage's topics evenly over its blocks, in catalog order, rounding up: the first block brings a new
    /// topic and the blocks left over (when there are more blocks than topics) review what came before.
    /// </summary>
    private static List<GrammarTopic> TopicsOfBlock(IReadOnlyList<GrammarTopic> topics, int block, int blocks)
    {
        int from = ((block - 1) * topics.Count + blocks - 1) / blocks;
        int to = (block * topics.Count + blocks - 1) / blocks;
        return topics.Skip(from).Take(to - from).ToList();
    }

    private static IReadOnlyList<GrammarTopic> TopicsOf(PlanStage stage) =>
        GrammarCatalog.Topics.Where(t => t.Stage == stage).ToList();
}
