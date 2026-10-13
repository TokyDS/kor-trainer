namespace KorTrainer.Domain.Srs;

/// <summary>Scheduler tuning; defaults follow Anki's SM-2 based scheduler.</summary>
public sealed record SrsOptions
{
    public const double DefaultStartingEase = 2.5;

    public static SrsOptions Default { get; } = new();

    public IReadOnlyList<TimeSpan> LearningSteps { get; init; } = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10)];

    public IReadOnlyList<TimeSpan> RelearningSteps { get; init; } = [TimeSpan.FromMinutes(10)];

    public double GraduatingIntervalDays { get; init; } = 1;

    public double EasyIntervalDays { get; init; } = 4;

    public double StartingEase { get; init; } = DefaultStartingEase;

    public double MinimumEase { get; init; } = 1.3;

    public double EaseStepDown { get; init; } = 0.15;

    public double LapseEasePenalty { get; init; } = 0.2;

    public double EaseStepUp { get; init; } = 0.15;

    public double HardMultiplier { get; init; } = 1.2;

    public double EasyBonus { get; init; } = 1.3;

    /// <summary>Share of the old interval kept after a lapse (Anki "new interval", default 0 → 1 day).</summary>
    public double LapseIntervalFactor { get; init; }

    public double MinimumLapseIntervalDays { get; init; } = 1;

    public double MaximumIntervalDays { get; init; } = 36500;
}
