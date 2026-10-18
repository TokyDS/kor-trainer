namespace KorTrainer.Domain.Hangul;

public enum DiffKind
{
    Match,

    /// <summary>In the expected sentence but not in the answer.</summary>
    Missing,

    /// <summary>In the answer but not in the expected sentence.</summary>
    Extra,
}

public sealed record DiffPart(string Text, DiffKind Kind);

/// <param name="Score">Matched syllables over the longer of the two sentences (0–1).</param>
public sealed record SentenceComparison(double Score, IReadOnlyList<DiffPart> Parts)
{
    public bool IsExact => Parts.All(p => p.Kind == DiffKind.Match) && Parts.Count > 0;
}

/// <summary>
/// Compares a typed or recognized sentence with the expected one. Spaces and punctuation are ignored
/// (spacing is not graded); syllables are aligned by longest common subsequence, so a missing or extra
/// syllable does not shift the rest of the sentence as a position-by-position check would.
/// </summary>
public static class SentenceComparer
{
    public static SentenceComparison Compare(string expected, string actual)
    {
        string a = AnswerChecker.Normalize(expected ?? string.Empty);
        string b = AnswerChecker.Normalize(actual ?? string.Empty);

        var lcs = new int[a.Length + 1, b.Length + 1];
        for (int i = a.Length - 1; i >= 0; i--)
        {
            for (int j = b.Length - 1; j >= 0; j--)
            {
                lcs[i, j] = a[i] == b[j] ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            }
        }

        var parts = new List<DiffPart>();
        int x = 0, y = 0;
        while (x < a.Length || y < b.Length)
        {
            if (x < a.Length && y < b.Length && a[x] == b[y])
            {
                Append(parts, a[x], DiffKind.Match);
                x++;
                y++;
            }
            else if (y == b.Length || (x < a.Length && lcs[x + 1, y] >= lcs[x, y + 1]))
            {
                Append(parts, a[x], DiffKind.Missing);
                x++;
            }
            else
            {
                Append(parts, b[y], DiffKind.Extra);
                y++;
            }
        }

        int longest = Math.Max(a.Length, b.Length);
        return new SentenceComparison(longest == 0 ? 1 : (double)lcs[0, 0] / longest, parts);
    }

    private static void Append(List<DiffPart> parts, char c, DiffKind kind)
    {
        if (parts.Count > 0 && parts[^1].Kind == kind)
        {
            parts[^1] = parts[^1] with { Text = parts[^1].Text + c };
        }
        else
        {
            parts.Add(new DiffPart(c.ToString(), kind));
        }
    }
}
