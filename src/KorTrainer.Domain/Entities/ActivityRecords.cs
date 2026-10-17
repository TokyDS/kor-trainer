using KorTrainer.Domain.Common;
using KorTrainer.Domain.Srs;

namespace KorTrainer.Domain.Entities;

/// <summary>
/// Activity records carry the learner's local date at the moment they were written,
/// so daily statistics and streaks can be grouped in SQL without time zone math.
/// </summary>
public interface IActivityRecord
{
    DateOnly LocalDate { get; set; }
}

public sealed class ReviewLog : IActivityRecord
{
    public long Id { get; set; }

    public int WordId { get; set; }

    public DateTime ReviewedUtc { get; set; }

    public DateOnly LocalDate { get; set; }

    public Grade Grade { get; set; }

    public CardPhase PhaseBefore { get; set; }

    public double IntervalBefore { get; set; }

    public double IntervalAfter { get; set; }

    public int DurationMs { get; set; }
}

public sealed class ExerciseAttempt : IActivityRecord
{
    public long Id { get; set; }

    public DateOnly LocalDate { get; set; }

    public StudyModule Module { get; set; }

    public required string ExerciseType { get; set; }

    public required string ItemKey { get; set; }

    public bool IsCorrect { get; set; }

    public int ResponseMs { get; set; }

    public DateTime AnsweredUtc { get; set; }
}

public sealed class ReadingTrial : IActivityRecord
{
    public long Id { get; set; }

    public DateOnly LocalDate { get; set; }

    public required string Text { get; set; }

    public int SyllableCount { get; set; }

    public int ElapsedMs { get; set; }

    public bool ReadCorrectly { get; set; }

    public DateTime CompletedUtc { get; set; }
}

public sealed class StudySession : IActivityRecord
{
    public long Id { get; set; }

    public DateOnly LocalDate { get; set; }

    public StudyModule Module { get; set; }

    public DateTime StartedUtc { get; set; }

    public int DurationSeconds { get; set; }
}

/// <summary>A roadmap step the learner has started; the one without <see cref="CompletedUtc"/> is the current step.</summary>
public sealed class PlanStepProgress
{
    public required string StepKey { get; set; }

    public DateTime StartedUtc { get; set; }

    public DateTime? CompletedUtc { get; set; }
}

/// <summary>A task of a step ticked by hand; automatic completion is derived from activity.</summary>
public sealed class StepTaskCompletion
{
    public required string StepKey { get; set; }

    public required string TaskKey { get; set; }

    public DateTime CompletedUtc { get; set; }
}

public sealed class WritingDraft : IActivityRecord
{
    public long Id { get; set; }

    public DateOnly LocalDate { get; set; }

    public int TaskNumber { get; set; }

    public DateTime StartedUtc { get; set; }

    public int DurationSeconds { get; set; }

    public string Text { get; set; } = string.Empty;
}
