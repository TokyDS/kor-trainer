namespace KorTrainer.Domain.Plan;

/// <summary>
/// Which deck words may be introduced: every word of an earlier stage, plus words of the
/// current stage whose lesson day has already been reached.
/// </summary>
public sealed record WordUnlock(PlanStage MaxStage, int MaxLessonDay)
{
    public static WordUnlock None { get; } = new(PlanStage.Hangul, 0);

    public bool IsUnlocked(PlanStage wordStage, int? lessonDay) =>
        wordStage < MaxStage || (wordStage == MaxStage && (lessonDay ?? 0) <= MaxLessonDay);
}
