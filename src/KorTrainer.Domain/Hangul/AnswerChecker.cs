namespace KorTrainer.Domain.Hangul;

/// <param name="Note">What is wrong with the syllable (Russian), null when it matches.</param>
public sealed record SyllableCheck(char Expected, char? Actual, bool IsMatch, string? Note);

/// <param name="IsCorrect">Right answer, ignoring spaces and punctuation.</param>
/// <param name="Extra">Characters typed beyond the expected word.</param>
public sealed record AnswerCheck(bool IsCorrect, IReadOnlyList<SyllableCheck> Syllables, string Extra);

/// <summary>
/// Checks a typed Korean answer syllable by syllable; mismatching syllables are compared jamo by jamo
/// so the learner sees exactly which letter is wrong (ㅓ вместо ㅗ, нет патчима ㄴ …).
/// </summary>
public static class AnswerChecker
{
    public static AnswerCheck Check(string expected, string actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        string want = Normalize(expected);
        string got = Normalize(actual);
        var syllables = new List<SyllableCheck>(want.Length);
        for (int i = 0; i < want.Length; i++)
        {
            char? typed = i < got.Length ? got[i] : null;
            bool match = typed == want[i];
            syllables.Add(new SyllableCheck(want[i], typed, match, match ? null : Describe(i + 1, want[i], typed)));
        }

        string extra = got.Length > want.Length ? got[want.Length..] : string.Empty;
        return new AnswerCheck(want == got, syllables, extra);
    }

    /// <summary>Drops spaces and punctuation: «안녕히 가세요» and «안녕히가세요» are both accepted.</summary>
    public static string Normalize(string text) =>
        new(text.Where(c => !char.IsWhiteSpace(c) && !char.IsPunctuation(c)).ToArray());

    private static string Describe(int position, char expected, char? actual)
    {
        string where = $"{position}-й слог";
        if (actual is not { } typed)
        {
            return $"{where}: не хватает «{expected}»";
        }

        if (!Syllable.IsSyllable(expected) || !Syllable.IsSyllable(typed))
        {
            return $"{where}: «{typed}» вместо «{expected}»";
        }

        var want = Syllable.Decompose(expected);
        var got = Syllable.Decompose(typed);
        var notes = new List<string>();
        if (want.Initial != got.Initial)
        {
            notes.Add($"{got.Initial} вместо {want.Initial}");
        }

        if (want.Medial != got.Medial)
        {
            notes.Add($"{got.Medial} вместо {want.Medial}");
        }

        if (want.Final != got.Final)
        {
            notes.Add((want.Final, got.Final) switch
            {
                (null, { } extraFinal) => $"лишний патчим {extraFinal}",
                ({ } missing, null) => $"нет патчима {missing}",
                var (w, g) => $"патчим {g} вместо {w}",
            });
        }

        return $"{where} «{typed}» → «{expected}»: {string.Join(", ", notes)}";
    }
}
