namespace KorTrainer.Domain.Srs;

/// <summary>
/// SM-2 style spaced repetition scheduler modelled on Anki: short learning steps in minutes,
/// then day intervals multiplied by a per-card ease factor. Pure and deterministic.
/// </summary>
public sealed class SrsScheduler(SrsOptions options)
{
    public SrsScheduler()
        : this(SrsOptions.Default)
    {
    }

    public SrsOptions Options { get; } = options;

    public CardState Schedule(CardState card, Grade grade, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(card);
        return card.Phase switch
        {
            CardPhase.New or CardPhase.Learning => ScheduleLearning(card, grade, nowUtc),
            CardPhase.Review => ScheduleReview(card, grade, nowUtc),
            CardPhase.Relearning => ScheduleRelearning(card, grade, nowUtc),
            _ => throw new ArgumentOutOfRangeException(nameof(card), card.Phase, "Unknown card phase."),
        };
    }

    /// <summary>Time until the card would be due again for each possible answer.</summary>
    public IReadOnlyDictionary<Grade, TimeSpan> Preview(CardState card, DateTime nowUtc) =>
        Enum.GetValues<Grade>().ToDictionary(g => g, g => Schedule(card, g, nowUtc).DueUtc - nowUtc);

    private CardState ScheduleLearning(CardState card, Grade grade, DateTime now)
    {
        var steps = Options.LearningSteps;
        switch (grade)
        {
            case Grade.Again:
                return card with { Phase = CardPhase.Learning, Step = 0, DueUtc = now + steps[0] };
            case Grade.Hard:
                return card with { Phase = CardPhase.Learning, DueUtc = now + HardStepDelay(steps, card.Step) };
            case Grade.Good when card.Step + 1 < steps.Count:
                int next = card.Step + 1;
                return card with { Phase = CardPhase.Learning, Step = next, DueUtc = now + steps[next] };
            case Grade.Good:
                return Graduate(card, Options.GraduatingIntervalDays, now);
            case Grade.Easy:
                return Graduate(card, Options.EasyIntervalDays, now);
            default:
                throw new ArgumentOutOfRangeException(nameof(grade), grade, null);
        }
    }

    private CardState ScheduleReview(CardState card, Grade grade, DateTime now)
    {
        double interval = card.IntervalDays;
        switch (grade)
        {
            case Grade.Again:
                double lapseInterval = Math.Max(Options.MinimumLapseIntervalDays, interval * Options.LapseIntervalFactor);
                return card with
                {
                    Phase = CardPhase.Relearning,
                    Ease = Math.Max(Options.MinimumEase, card.Ease - Options.LapseEasePenalty),
                    IntervalDays = lapseInterval,
                    Lapses = card.Lapses + 1,
                    Step = 0,
                    DueUtc = now + Options.RelearningSteps[0],
                };
            case Grade.Hard:
                return ToReview(
                    card with { Ease = Math.Max(Options.MinimumEase, card.Ease - Options.EaseStepDown) },
                    Math.Max(interval + 1, interval * Options.HardMultiplier),
                    now);
            case Grade.Good:
                return ToReview(card, Math.Max(interval + 1, interval * card.Ease), now);
            case Grade.Easy:
                double good = Math.Max(interval + 1, interval * card.Ease);
                return ToReview(
                    card with { Ease = card.Ease + Options.EaseStepUp },
                    Math.Max(good + 1, interval * card.Ease * Options.EasyBonus),
                    now);
            default:
                throw new ArgumentOutOfRangeException(nameof(grade), grade, null);
        }
    }

    private CardState ScheduleRelearning(CardState card, Grade grade, DateTime now)
    {
        var steps = Options.RelearningSteps;
        return grade switch
        {
            Grade.Again => card with { Step = 0, DueUtc = now + steps[0] },
            Grade.Hard => card with { DueUtc = now + HardStepDelay(steps, card.Step) },
            Grade.Good when card.Step + 1 < steps.Count => card with { Step = card.Step + 1, DueUtc = now + steps[card.Step + 1] },
            Grade.Good => ToReview(card, card.IntervalDays, now),
            Grade.Easy => ToReview(card, card.IntervalDays + 1, now),
            _ => throw new ArgumentOutOfRangeException(nameof(grade), grade, null),
        };
    }

    private CardState Graduate(CardState card, double intervalDays, DateTime now) =>
        ToReview(card with { Step = 0 }, intervalDays, now);

    private CardState ToReview(CardState card, double intervalDays, DateTime now)
    {
        double interval = Math.Round(Math.Min(intervalDays, Options.MaximumIntervalDays), MidpointRounding.AwayFromZero);
        return card with
        {
            Phase = CardPhase.Review,
            IntervalDays = interval,
            Repetitions = card.Repetitions + 1,
            Step = 0,
            DueUtc = now.AddDays(interval),
        };
    }

    // Anki: "Hard" on the first step waits the average of the first two steps, otherwise repeats the step.
    private static TimeSpan HardStepDelay(IReadOnlyList<TimeSpan> steps, int step)
    {
        int current = Math.Clamp(step, 0, steps.Count - 1);
        return current == 0 && steps.Count > 1
            ? (steps[0] + steps[1]) / 2
            : steps[current];
    }
}
