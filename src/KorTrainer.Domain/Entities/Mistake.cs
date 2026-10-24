using KorTrainer.Domain.Common;

namespace KorTrainer.Domain.Entities;

public static class MistakeLimits
{
    /// <summary>A stored exercise is a few hundred bytes; anything far larger is not one of ours.</summary>
    public const int MaxPayloadLength = 8000;
}

/// <summary>
/// A multiple-choice exercise answered wrongly, kept as it was shown so it can come back on its schedule
/// (<see cref="Srs.MistakeSchedule"/>). One row per module and item; a new slip reopens a closed one.
/// </summary>
public sealed class Mistake
{
    public long Id { get; set; }

    public StudyModule Module { get; set; }

    public required string ExerciseType { get; set; }

    public required string ItemKey { get; set; }

    /// <summary>The exercise as JSON (<c>ChoiceExerciseDto</c>).</summary>
    public required string Payload { get; set; }

    /// <summary>Correct answers in a row since the last slip; the next interval depends on it.</summary>
    public int Box { get; set; }

    public int Lapses { get; set; }

    public DateOnly CreatedDate { get; set; }

    public DateOnly DueDate { get; set; }

    /// <summary>Set once the learner has got it right on every interval.</summary>
    public DateOnly? ResolvedDate { get; set; }
}
