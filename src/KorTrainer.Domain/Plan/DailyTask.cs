using KorTrainer.Domain.Common;

namespace KorTrainer.Domain.Plan;

/// <summary>What has to happen during a day for a task to count as done automatically.</summary>
public enum TaskMetric
{
    /// <summary>Only a manual tick completes the task.</summary>
    Manual,

    /// <summary>New vocabulary cards seen for the first time today.</summary>
    NewWords,

    /// <summary>No vocabulary card is due right now.</summary>
    VocabularyDueCleared,

    /// <summary>Answered exercises of a module, optionally of one exercise type.</summary>
    Answers,

    /// <summary>Timed reading trials.</summary>
    ReadingTrials,

    /// <summary>Minutes spent in TOPIK writing practice.</summary>
    WritingMinutes,
}

public sealed record TaskTarget(TaskMetric Metric, int Count = 1, StudyModule? Module = null, string? ExerciseType = null)
{
    public static TaskTarget Manual() => new(TaskMetric.Manual);

    public static TaskTarget Answers(StudyModule module, int count, string? exerciseType = null) =>
        new(TaskMetric.Answers, count, module, exerciseType);
}

/// <param name="Key">Stable key, unique within a day; used to store manual completion.</param>
/// <param name="Route">Client route that opens the matching exercise.</param>
public sealed record DailyTask(string Key, string Title, TaskTarget Target, string? Route = null);

public sealed record PlanDay(
    int DayNumber,
    DateOnly Date,
    StageInfo Stage,
    LessonInfo? Lesson,
    IReadOnlyList<DailyTask> Tasks,
    IReadOnlyList<QuickLink> QuickLinks);

public sealed record QuickLink(string Title, string Route);
