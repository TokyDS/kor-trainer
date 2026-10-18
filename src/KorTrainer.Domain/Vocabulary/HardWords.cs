using KorTrainer.Domain.Srs;

namespace KorTrainer.Domain.Vocabulary;

/// <param name="Value">Higher is harder; used to order the list.</param>
/// <param name="Reason">Russian explanation shown next to the word.</param>
public sealed record HardWordScore(bool IsHard, int Value, string Reason);

/// <summary>
/// "Leeches": words the learner keeps forgetting. A word is hard after two lapses (forgotten once it was
/// learned) or when "Again" was pressed three times among its recent answers.
/// </summary>
public static class HardWords
{
    public const int LapseThreshold = 2;
    public const int AgainThreshold = 3;

    /// <summary>How many of the latest answers are considered.</summary>
    public const int RecentAnswers = 8;

    private const int LapseWeight = 3;

    /// <param name="recent">The word's latest grades, newest last (only the last <see cref="RecentAnswers"/> count).</param>
    public static HardWordScore Score(int lapses, IReadOnlyList<Grade> recent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(lapses);
        ArgumentNullException.ThrowIfNull(recent);
        var window = recent.TakeLast(RecentAnswers).ToList();
        int again = window.Count(g => g == Grade.Again);

        var reasons = new List<string>();
        if (lapses > 0)
        {
            reasons.Add($"забыто {lapses} {Times(lapses)}");
        }

        if (again > 0)
        {
            reasons.Add($"«Снова» {again} из {window.Count} последних");
        }

        return new HardWordScore(lapses >= LapseThreshold || again >= AgainThreshold, (lapses * LapseWeight) + again, string.Join(", ", reasons));
    }

    private static string Times(int n) => (n % 10, n % 100) switch
    {
        (2 or 3 or 4, not (12 or 13 or 14)) => "раза",
        _ => "раз",
    };
}
