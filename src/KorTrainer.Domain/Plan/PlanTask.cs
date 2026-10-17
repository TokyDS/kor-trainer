using KorTrainer.Domain.Common;

namespace KorTrainer.Domain.Plan;

/// <summary>What has to happen while a step is open for its task to count as done automatically.</summary>
public enum TaskMetric
{
    /// <summary>Only a manual tick completes the task.</summary>
    Manual,

    /// <summary>New vocabulary cards seen for the first time since the step started.</summary>
    NewWords,

    /// <summary>No vocabulary card is due right now.</summary>
    VocabularyDueCleared,

    /// <summary>Answered exercises of a module, optionally of one exercise type.</summary>
    Answers,

    /// <summary>Timed reading trials.</summary>
    ReadingTrials,

    /// <summary>Minutes spent in TOPIK writing practice.</summary>
    WritingMinutes,

    /// <summary>Words in the learner's deck overall (cumulative, not only during the step).</summary>
    WordsStudied,
}

/// <param name="Count">Amount to reach; for <see cref="TaskMetric.Manual"/> and <see cref="TaskMetric.VocabularyDueCleared"/> unused.</param>
/// <param name="ExerciseType">Restricts <see cref="TaskMetric.Answers"/> to one exercise type.</param>
public sealed record TaskTarget(TaskMetric Metric, int Count = 1, StudyModule? Module = null, string? ExerciseType = null)
{
    public static TaskTarget Manual() => new(TaskMetric.Manual);

    public static TaskTarget Answers(StudyModule module, int count, string? exerciseType = null) =>
        new(TaskMetric.Answers, count, module, exerciseType);
}

/// <param name="Key">Stable key, unique within a step; used to store manual completion.</param>
/// <param name="Route">Client route that practices the task.</param>
public sealed record PlanTask(string Key, string Title, TaskTarget Target, string? Route = null);

public sealed record QuickLink(string Title, string Route);
