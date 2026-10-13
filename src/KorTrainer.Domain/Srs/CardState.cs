namespace KorTrainer.Domain.Srs;

public enum Grade
{
    Again = 1,
    Hard = 2,
    Good = 3,
    Easy = 4,
}

public enum CardPhase
{
    New = 0,
    Learning = 1,
    Review = 2,
    Relearning = 3,
}

/// <summary>Immutable scheduling state of one flashcard. All times are UTC.</summary>
public sealed record CardState(
    CardPhase Phase,
    double Ease,
    double IntervalDays,
    int Repetitions,
    int Lapses,
    int Step,
    DateTime DueUtc)
{
    /// <summary>Interval at which a card counts as "learned" (mature in Anki terms).</summary>
    public const double MatureIntervalDays = 21;

    public bool IsMature => Phase == CardPhase.Review && IntervalDays >= MatureIntervalDays;

    public static CardState New(DateTime nowUtc, double startingEase = SrsOptions.DefaultStartingEase) =>
        new(CardPhase.New, startingEase, 0, 0, 0, 0, nowUtc);
}
