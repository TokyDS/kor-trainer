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

/// <param name="Date">The day shown; may differ from <paramref name="Today"/> when browsing the plan.</param>
/// <param name="StudiedToday">There was activity on <paramref name="Date"/>.</param>
/// <param name="CanEdit">Manual ticks of this day can still be changed (today and the last week).</param>
public sealed record TodayDto(
    DateOnly Date,
    int DayNumber,
    StageDto Stage,
    LessonDto? Lesson,
    IReadOnlyList<DailyTaskDto> Tasks,
    int StreakDays,
    bool StudiedToday,
    IReadOnlyList<QuickLinkDto> QuickLinks,
    VocabularySummaryDto Vocabulary,
    DateOnly Today,
    bool CanEdit)
{
    public bool IsToday => Date == Today;

    public bool IsFuture => Date > Today;
}

public sealed record PlanDto(DateOnly StartDate, int CurrentDay, PlanStage CurrentStage, IReadOnlyList<StageDto> Stages, IReadOnlyList<LessonDto> Lessons);

public sealed record SetTaskDoneRequest(DateOnly Date, bool Done);

/// <param name="ShouldRemind">Something is left for today (used by the desktop reminder and the Today banner).</param>
public sealed record ReminderDto(bool ShouldRemind, string Title, string Message, int StreakDays, bool StudiedToday, int TasksLeft, int WordsDue);
