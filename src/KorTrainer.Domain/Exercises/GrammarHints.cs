using KorTrainer.Domain.Hangul;

namespace KorTrainer.Domain.Exercises;

/// <summary>
/// The first step of the rule for this very question, offered before answering: where to look and what is there.
/// A particle gap points at the syllable before it, a verb at the last syllable of its stem.
/// </summary>
public static class GrammarHints
{
    private const string Gap = "___";

    public static string? For(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        int gap = prompt.IndexOf(Gap, StringComparison.Ordinal);
        if (gap > 0 && Syllable.IsSyllable(prompt[gap - 1]))
        {
            return Particle(prompt[gap - 1]);
        }

        string word = prompt.Trim();
        return gap < 0 && word.Length >= 2 && word[^1] == '다' && word.All(Syllable.IsSyllable) ? Verb(word[..^1]) : null;
    }

    private static string Particle(char last)
    {
        var parts = Syllable.Decompose(last);
        return parts.Final switch
        {
            null => $"Последний слог перед пропуском — {last}: внизу пусто, патчима нет.",
            'ㄹ' => $"Последний слог перед пропуском — {last}: внизу ㄹ. Для (으)로 ㄹ считается как гласная.",
            { } final => $"Последний слог перед пропуском — {last}: внизу {final}, есть патчим.",
        };
    }

    private static string Verb(string stem)
    {
        char last = stem[^1];
        if (last == '하')
        {
            return $"Основа {stem} оканчивается на 하: у глаголов на 하다 своя форма.";
        }

        var parts = Syllable.Decompose(last);
        string final = parts.Final is { } f ? $", внизу {f}" : ", патчима нет";
        return $"Основа {stem}: последний слог {last}, гласная {parts.Medial}{final}.";
    }
}
