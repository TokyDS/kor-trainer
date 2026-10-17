using KorTrainer.Domain.Common;

namespace KorTrainer.Domain.Plan;

/// <summary>
/// The study plan towards TOPIK in spring 2027. Days 1–10 are counted from the configurable
/// start date; later stages are bound to fixed calendar dates.
/// </summary>
public sealed class StudyPlan
{
    public const int HangulDays = 3;
    public const int FirstPhrasesLastDay = 10;

    public static readonly DateOnly DefaultStartDate = new(2026, 10, 13);
    public static readonly DateOnly Level1End = new(2026, 12, 31);
    public static readonly DateOnly Level2Start = new(2027, 1, 1);
    public static readonly DateOnly Level2End = new(2027, 2, 21);
    public static readonly DateOnly MockExamStart = new(2027, 2, 22);
    public static readonly DateOnly MockExamEnd = new(2027, 2, 28);
    public static readonly DateOnly TopikPrepStart = new(2027, 3, 1);
    public static readonly DateOnly TopikPrepEnd = new(2027, 4, 30);

    private const int WritingRotationLength = 3;

    public StudyPlan(DateOnly startDate, int newWordsPerDay = 10)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newWordsPerDay);
        StartDate = startDate;
        NewWordsPerDay = newWordsPerDay;
        Stages = BuildStages();
    }

    public DateOnly StartDate { get; }

    public int NewWordsPerDay { get; }

    /// <summary>Stages that actually take place for this start date (empty ones are dropped).</summary>
    public IReadOnlyList<StageInfo> Stages { get; }

    public IReadOnlyList<LessonInfo> Lessons => PlanContent.Lessons;

    /// <summary>1 on the start date; zero or negative before it.</summary>
    public int DayNumber(DateOnly date) => date.DayNumber - StartDate.DayNumber + 1;

    public PlanStage StageOn(DateOnly date)
    {
        int day = DayNumber(date);
        return day switch
        {
            < 1 => PlanStage.NotStarted,
            <= HangulDays => PlanStage.Hangul,
            <= FirstPhrasesLastDay => PlanStage.FirstPhrases,
            _ when date <= Level1End => PlanStage.Level1,
            _ when date <= Level2End => PlanStage.Level2,
            _ when date <= MockExamEnd => PlanStage.MockExam,
            _ when date <= TopikPrepEnd => PlanStage.TopikPrep,
            _ => PlanStage.Maintenance,
        };
    }

    public StageInfo StageInfoOn(DateOnly date)
    {
        var stage = StageOn(date);
        return Stages.FirstOrDefault(s => s.Stage == stage)
            ?? new StageInfo(stage, PlanContent.StageTitle(stage), PlanContent.StageGoal(stage), date, date);
    }

    public WordUnlock WordUnlockOn(DateOnly date) => StageOn(date) switch
    {
        PlanStage.NotStarted or PlanStage.Hangul => WordUnlock.None,
        PlanStage.FirstPhrases => new WordUnlock(PlanStage.FirstPhrases, DayNumber(date)),
        PlanStage.Level1 => new WordUnlock(PlanStage.Level1, int.MaxValue),
        PlanStage.Level2 or PlanStage.MockExam => new WordUnlock(PlanStage.Level2, int.MaxValue),
        _ => new WordUnlock(PlanStage.TopikPrep, int.MaxValue),
    };

    public PlanDay GetDay(DateOnly date)
    {
        int day = DayNumber(date);
        var stage = StageInfoOn(date);
        var lesson = PlanContent.Lessons.FirstOrDefault(l => l.Day == day);
        var tasks = stage.Stage switch
        {
            PlanStage.NotStarted => [],
            PlanStage.Hangul or PlanStage.FirstPhrases => PlanContent.LessonTasks(day, NewWordsPerDay),
            PlanStage.Level1 => Level1Tasks(),
            PlanStage.Level2 => Level2Tasks(),
            PlanStage.MockExam => MockExamTasks(date),
            PlanStage.TopikPrep => TopikPrepTasks(day),
            _ => MaintenanceTasks(),
        };

        return new PlanDay(day, date, stage, lesson, tasks, PlanContent.QuickLinks(stage.Stage, day));
    }

    private IReadOnlyList<StageInfo> BuildStages()
    {
        var candidates = new List<StageInfo>
        {
            Stage(PlanStage.Hangul, StartDate, StartDate.AddDays(HangulDays - 1)),
            Stage(PlanStage.FirstPhrases, StartDate.AddDays(HangulDays), StartDate.AddDays(FirstPhrasesLastDay - 1)),
            Stage(PlanStage.Level1, StartDate.AddDays(FirstPhrasesLastDay), Level1End),
            Stage(PlanStage.Level2, Max(Level2Start, StartDate.AddDays(FirstPhrasesLastDay)), Level2End),
            Stage(PlanStage.MockExam, Max(MockExamStart, StartDate.AddDays(FirstPhrasesLastDay)), MockExamEnd),
            Stage(PlanStage.TopikPrep, Max(TopikPrepStart, StartDate.AddDays(FirstPhrasesLastDay)), TopikPrepEnd),
        };

        return candidates.Where(s => s.Start <= s.End).ToList();

        static StageInfo Stage(PlanStage stage, DateOnly start, DateOnly end) =>
            new(stage, PlanContent.StageTitle(stage), PlanContent.StageGoal(stage), start, end);

        static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
    }

    private IReadOnlyList<DailyTask> Level1Tasks() =>
    [
        CommonTasks.ReviewDue,
        CommonTasks.NewWords(NewWordsPerDay),
        new("grammar", "Грамматика: 20 упражнений", TaskTarget.Answers(StudyModule.Grammar, 20), "/grammar/mixed"),
        new("numbers", "Числа: 10 упражнений", TaskTarget.Answers(StudyModule.Numbers, 10), "/numbers/mixed"),
        CommonTasks.Reading(10),
    ];

    private IReadOnlyList<DailyTask> Level2Tasks() =>
    [
        CommonTasks.ReviewDue,
        CommonTasks.NewWords(NewWordsPerDay),
        new("grammar", "Грамматика уровня 2: 25 упражнений", TaskTarget.Answers(StudyModule.Grammar, 25), "/grammar/mixed"),
        new("numbers", "Время и деньги: 10 упражнений", TaskTarget.Answers(StudyModule.Numbers, 10), "/numbers/mixed"),
        CommonTasks.Reading(10),
    ];

    private static IReadOnlyList<DailyTask> MockExamTasks(DateOnly date)
    {
        var tasks = new List<DailyTask>
        {
            CommonTasks.ReviewDue,
            new("mock-topik1", "Пробный TOPIK I: прошлый вариант с topik.go.kr (чтение 60 мин, аудирование 40 мин)", TaskTarget.Manual(), "/topik"),
            new("mock-writing", "Пробное письмо TOPIK II, задания 51–54 на таймере", new TaskTarget(TaskMetric.WritingMinutes, 50), "/topik/writing?task=54"),
        };

        if (date == MockExamEnd)
        {
            tasks.Add(new("decide", "Решение: сдаю TOPIK I или TOPIK II (сравни результаты пробников)", TaskTarget.Manual(), "/stats"));
        }

        return tasks;
    }

    private IReadOnlyList<DailyTask> TopikPrepTasks(int day)
    {
        var writing = (day % WritingRotationLength) switch
        {
            0 => new DailyTask("writing", "Письмо: задания 51–52 (15 минут)", new TaskTarget(TaskMetric.WritingMinutes, 15), "/topik/writing?task=51"),
            1 => new DailyTask("writing", "Письмо: задание 53, описание графика (15 минут)", new TaskTarget(TaskMetric.WritingMinutes, 15), "/topik/writing?task=53"),
            _ => new DailyTask("writing", "Письмо: эссе, задание 54 (30 минут)", new TaskTarget(TaskMetric.WritingMinutes, 30), "/topik/writing?task=54"),
        };

        return
        [
            CommonTasks.ReviewDue,
            CommonTasks.NewWords(NewWordsPerDay),
            writing,
            new("reading", "Чтение: один блок заданий TOPIK II из прошлых вариантов", TaskTarget.Manual(), "/topik"),
            new("listening", "Аудирование: 20 минут заданий TOPIK II или упражнений на слух", TaskTarget.Manual(), "/listening"),
        ];
    }

    private static IReadOnlyList<DailyTask> MaintenanceTasks() =>
    [
        CommonTasks.ReviewDue,
        new("grammar", "Грамматика: 10 упражнений", TaskTarget.Answers(StudyModule.Grammar, 10), "/grammar/mixed"),
    ];
}
