using KorTrainer.Domain.Common;

namespace KorTrainer.Domain.Plan;

/// <summary>What has to happen while a step is open for its task to count as done automatically.</summary>
public enum TaskMetric
{
    /// <summary>The learner read a rule, lesson notes or a guide in the app to the end (<see cref="TaskTarget.Material"/>).</summary>
    Read,

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

/// <param name="Count">Amount to reach; for <see cref="TaskMetric.Read"/> and <see cref="TaskMetric.VocabularyDueCleared"/> unused.</param>
/// <param name="ExerciseType">Restricts <see cref="TaskMetric.Answers"/> to one exercise type.</param>
/// <param name="Material">What a <see cref="TaskMetric.Read"/> task asks to read, see <see cref="StudyMaterials"/>.</param>
public sealed record TaskTarget(TaskMetric Metric, int Count = 1, StudyModule? Module = null, string? ExerciseType = null, string? Material = null)
{
    public static TaskTarget Read(string material) => new(TaskMetric.Read, Material: material);

    public static TaskTarget Answers(StudyModule module, int count, string? exerciseType = null) =>
        new(TaskMetric.Answers, count, module, exerciseType);
}

/// <summary>Ids of what the learner reads in the app; the page marks it read when the learner gets to its end.</summary>
public static class StudyMaterials
{
    /// <summary>The Hangul guide of a lesson (<c>/hangul/guide?level=</c>).</summary>
    public static string HangulGuide(int level) => $"hangul-guide-{level}";

    /// <summary>The notes of a lesson (<c>/lesson/{day}</c>).</summary>
    public static string Lesson(int day) => $"lesson-{day}";

    /// <summary>The rule of a grammar topic, shown before its drill: the topic's exercise type itself.</summary>
    public static string GrammarRule(string topic) => topic;

    /// <summary>The rule of a number drill (<c>/numbers/{type}</c>), shown before it.</summary>
    public static string NumberRule(string type) => $"numbers-{type}";

    /// <summary>The scene that ends a lesson (<c>/lesson/{day}/practice</c>, <see cref="LessonPractices"/>).</summary>
    public static string Practice(int day) => $"practice-{day}";
}

/// <param name="Key">Stable key, unique within a step; used to store that a <see cref="TaskMetric.Read"/> task was read.</param>
/// <param name="Route">Client route that practices the task.</param>
public sealed record PlanTask(string Key, string Title, TaskTarget Target, string? Route = null);

public sealed record QuickLink(string Title, string Route);

/// <summary>Where a lesson task stands, for <see cref="TaskOrder"/>.</summary>
public readonly record struct TaskState(bool IsDone, bool IsStarted);

/// <summary>
/// A lesson is done in order: each task opens once every task before it is done. A task the learner has
/// already started (say, from the grammar hub) stays open, so progress is never hidden.
/// </summary>
public static class TaskOrder
{
    public static IReadOnlyList<bool> Locked(IReadOnlyList<TaskState> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        var locked = new bool[tasks.Count];
        bool earlierOpen = false;
        for (int i = 0; i < tasks.Count; i++)
        {
            locked[i] = earlierOpen && !tasks[i].IsDone && !tasks[i].IsStarted;
            earlierOpen |= !tasks[i].IsDone;
        }

        return locked;
    }
}
