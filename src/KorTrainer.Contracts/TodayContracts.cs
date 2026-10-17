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

/// <param name="IsManualOnly">The task has no measurable target and is completed only by a tick.</param>
public sealed record PlanTaskDto(
    string Key,
    string Title,
    string? Route,
    int Progress,
    int Target,
    bool IsDone,
    bool IsManualOnly);

/// <param name="Number">Position in the plan, from 1.</param>
/// <param name="Title">Name within the stage: "Урок 3: …" or "Блок 2 из 8".</param>
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
public sealed record TodayDto(
    PlanStepDto Step,
    string CurrentKey,
    int ActiveDaysThisWeek,
    bool StudiedToday,
    VocabularySummaryDto Vocabulary)
{
    public bool IsCurrent => Step.Key == CurrentKey;

    /// <summary>Every task of the current step is done: offer the next step.</summary>
    public bool ReadyForNext => IsCurrent && Step.AllTasksDone && Step.NextKey is not null;
}

public sealed record PlanStepSummaryDto(string Key, int Number, PlanStage Stage, string Title, StepState State);

public sealed record StageDto(PlanStage Stage, string Title, string Goal, int StepCount, int DoneCount);

public sealed record PlanDto(string CurrentKey, IReadOnlyList<StageDto> Stages, IReadOnlyList<PlanStepSummaryDto> Steps, IReadOnlyList<LessonDto> Lessons);

/// <param name="From">The step the learner is leaving; guards against advancing twice from two tabs.</param>
public sealed record AdvanceRequest(string From);

public sealed record SetTaskDoneRequest(string Step, bool Done);

/// <param name="ShouldRemind">A gentle nudge is due: no study yet today and reminders are on.</param>
public sealed record ReminderDto(bool ShouldRemind, string Title, string Message, bool StudiedToday, int ActiveDaysThisWeek, int WordsDue);
