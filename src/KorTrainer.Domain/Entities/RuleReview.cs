namespace KorTrainer.Domain.Entities;

/// <summary>
/// The spaced check of one grammar rule (<see cref="Srs.RuleSchedule"/>). A rule the learner has practised but
/// never checked has no row: its first check is due the day after its first practice.
/// </summary>
public sealed class RuleReview
{
    /// <summary>The grammar topic, as in <c>ExerciseTypes</c>.</summary>
    public required string ExerciseType { get; set; }

    /// <summary>Checks passed in a row; the next interval depends on it.</summary>
    public int Box { get; set; }

    public DateOnly DueDate { get; set; }

    public DateOnly LastCheckedDate { get; set; }

    public int Lapses { get; set; }
}
