namespace KorTrainer.Domain.Common;

/// <summary>How a drill topic went recently: answers given and how many were right.</summary>
public sealed record TopicTally(StudyModule Module, string Type, int Attempts, int Correct)
{
    public double Accuracy => Attempts == 0 ? 1 : (double)Correct / Attempts;
}

/// <summary>
/// Picks the topics that go worst, so the learner (and the 10-minute session) can return to them:
/// enough recent answers to judge, accuracy below the bar, the weakest first.
/// </summary>
public static class WeakTopics
{
    /// <summary>How far back answers count.</summary>
    public const int Days = 30;

    /// <summary>Fewer answers say more about luck than about the topic.</summary>
    public const int MinAttempts = 10;

    public const double Threshold = 0.85;

    public const int MaxShown = 3;

    /// <summary>Modules whose exercise types are topics worth drilling on their own.</summary>
    public static IReadOnlySet<StudyModule> Modules { get; } = new HashSet<StudyModule> { StudyModule.Hangul, StudyModule.Grammar, StudyModule.Numbers };

    public static IReadOnlyList<TopicTally> Rank(IEnumerable<TopicTally> tallies)
    {
        ArgumentNullException.ThrowIfNull(tallies);
        return tallies
            .Where(t => Modules.Contains(t.Module) && t.Type != ExerciseTypes.Mixed)
            .Where(t => t.Attempts >= MinAttempts && t.Accuracy < Threshold)
            .OrderBy(t => t.Accuracy)
            .ThenByDescending(t => t.Attempts)
            .Take(MaxShown)
            .ToList();
    }
}
