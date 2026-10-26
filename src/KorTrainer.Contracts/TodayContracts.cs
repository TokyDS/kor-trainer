using KorTrainer.Domain.Plan;

namespace KorTrainer.Contracts;

public sealed record LessonDto(int Day, string Title, IReadOnlyList<string> KeyPoints);

public sealed record QuickLinkDto(string Title, string Route);

/// <summary>Where a step stands for the learner.</summary>
public enum StepState
{
    Done,
    Current,
    Upcoming,
}

/// <param name="IsReading">The task is to read something in the app; it is done once read to the end.</param>
/// <param name="IsLocked">A lesson task that opens once the tasks before it are done.</param>
public sealed record PlanTaskDto(
    string Key,
    string Title,
    string? Route,
    int Progress,
    int Target,
    bool IsDone,
    bool IsReading,
    bool IsLocked = false);

/// <param name="Number">Position in the plan, from 1.</param>
/// <param name="Title">Name within the stage: "Урок 3: …" or "Блок 2 из 16".</param>
public sealed record PlanStepDto(
    string Key,
    int Number,
    int TotalSteps,
    PlanStage Stage,
    string StageTitle,
    string Title,
    string Goal,
    LessonDto? Lesson,
    IReadOnlyList<PlanTaskDto> Tasks,
    IReadOnlyList<QuickLinkDto> QuickLinks,
    StepState State,
    DateTime? StartedUtc,
    DateTime? CompletedUtc,
    string? PreviousKey,
    string? NextKey,
    string? NextTitle)
{
    public bool AllTasksDone => Tasks.All(t => t.IsDone);
}

/// <summary>The home screen: a step of the plan (the current one unless browsing) and today's context.</summary>
/// <param name="CurrentKey">The learner's current step, to return to while browsing.</param>
/// <param name="ActiveDaysThisWeek">Days with study among the last seven, today included.</param>
/// <param name="Week">The last seven days, today last: a quiet picture of the rhythm, no streak.</param>
public sealed record TodayDto(
    PlanStepDto Step,
    string CurrentKey,
    int ActiveDaysThisWeek,
    bool StudiedToday,
    VocabularySummaryDto Vocabulary,
    IReadOnlyList<StudyDayDto>? Week = null)
{
    public bool IsCurrent => Step.Key == CurrentKey;

    /// <summary>Every task of the current step is done: offer the next step.</summary>
    public bool ReadyForNext => IsCurrent && Step.AllTasksDone && Step.NextKey is not null;
}

/// <param name="Topic">What the step is about in a few words: «이에요/예요».</param>
/// <param name="Done">Tasks done so far; only for the current step.</param>
/// <param name="Total">Tasks of the step; only for the current step.</param>
public sealed record PlanStepSummaryDto(
    string Key, int Number, PlanStage Stage, string Title, StepState State, string? Topic = null, int? Done = null, int? Total = null);

public sealed record StageDto(PlanStage Stage, string Title, string Goal, int StepCount, int DoneCount);

public sealed record PlanDto(string CurrentKey, IReadOnlyList<StageDto> Stages, IReadOnlyList<PlanStepSummaryDto> Steps, IReadOnlyList<LessonDto> Lessons);

/// <param name="From">The step the learner is leaving; guards against advancing twice from two tabs.</param>
public sealed record AdvanceRequest(string From);

/// <param name="Material">What was read to the end: an id from <see cref="StudyMaterials"/>.</param>
public sealed record MarkReadRequest(string Material);

/// <param name="Label">Short Russian weekday: пн, вт …</param>
public sealed record StudyDayDto(string Label, bool Studied, bool IsToday);

/// <param name="ShouldRemind">A gentle nudge is due: no study yet today and reminders are on.</param>
public sealed record ReminderDto(bool ShouldRemind, string Title, string Message, bool StudiedToday, int ActiveDaysThisWeek, int WordsDue);
