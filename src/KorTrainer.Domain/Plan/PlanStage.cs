namespace KorTrainer.Domain.Plan;

/// <summary>Stages of the study plan in chronological order; the numeric order is relied upon.</summary>
public enum PlanStage
{
    NotStarted = 0,
    Hangul = 1,
    FirstPhrases = 2,
    Level1 = 3,
    Level2 = 4,
    MockExam = 5,
    TopikPrep = 6,
    Maintenance = 7,
}

public sealed record LessonInfo(int Day, string Title, IReadOnlyList<string> KeyPoints);
