using KorTrainer.Domain.Plan;

namespace KorTrainer.Contracts;

public sealed record StageDto(PlanStage Stage, string Title, string Goal, DateOnly Start, DateOnly End);

public sealed record LessonDto(int Day, string Title, IReadOnlyList<string> KeyPoints);

public sealed record QuickLinkDto(string Title, string Route);

/// <param name="IsManualOnly">The task has no measurable target and is completed only by a tick.</param>
public sealed record DailyTaskDto(
    string Key,
    string Title,
    string? Route,
    int Progress,
    int Target,
    bool IsDone,
    bool IsManualOnly);

public sealed record TodayDto(
    DateOnly Date,
    int DayNumber,
    StageDto Stage,
    LessonDto? Lesson,
    IReadOnlyList<DailyTaskDto> Tasks,
    int StreakDays,
    bool StudiedToday,
    IReadOnlyList<QuickLinkDto> QuickLinks,
    VocabularySummaryDto Vocabulary);

public sealed record PlanDto(DateOnly StartDate, int CurrentDay, PlanStage CurrentStage, IReadOnlyList<StageDto> Stages, IReadOnlyList<LessonDto> Lessons);

public sealed record SetTaskDoneRequest(DateOnly Date, bool Done);
