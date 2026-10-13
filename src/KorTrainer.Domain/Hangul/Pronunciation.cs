using System.Text;

namespace KorTrainer.Domain.Hangul;

/// <summary>A sound change found while converting spelling into pronunciation.</summary>
public sealed record SoundChange(SoundRule Rule, string Description);

/// <summary>Pronounced form of a text plus the rules that produced it.</summary>
public sealed record PronunciationResult(string Text, IReadOnlyList<SoundChange> Changes)
{
    public bool IsChanged => Changes.Count > 0;
}

/// <summary>
/// Converts Hangul spelling into its standard pronunciation (표준 발음) for the rules a
/// beginner meets first: the 7 final sounds, 연음 and the common assimilations.
/// Rare exceptions (e.g. 밟다, ㄴ-insertion) are intentionally out of scope.
/// </summary>
public static class Pronunciation
{
    /// <summary>The 7 sounds a final consonant can actually make.</summary>
    public const string RepresentativeSounds = "ㄱㄴㄷㄹㅁㅂㅇ";

    public static char RepresentativeSound(char final) => final switch
    {
        'ㄱ' or 'ㄲ' or 'ㅋ' or 'ㄳ' or 'ㄺ' => 'ㄱ',
        'ㄴ' or 'ㄵ' or 'ㄶ' => 'ㄴ',
        'ㄷ' or 'ㅅ' or 'ㅆ' or 'ㅈ' or 'ㅊ' or 'ㅌ' or 'ㅎ' => 'ㄷ',
        'ㄹ' or 'ㄼ' or 'ㄽ' or 'ㄾ' or 'ㅀ' => 'ㄹ',
        'ㅁ' or 'ㄻ' => 'ㅁ',
        'ㅂ' or 'ㅍ' or 'ㅄ' or 'ㄿ' => 'ㅂ',
        'ㅇ' => 'ㅇ',
        _ => throw new ArgumentOutOfRangeException(nameof(final), final, "Not a final consonant."),
    };

    /// <summary>Splits a double final (겹받침) into its two consonants.</summary>
    public static bool TrySplitDoubleFinal(char final, out char first, out char second)
    {
        (first, second) = final switch
        {
            'ㄳ' => ('ㄱ', 'ㅅ'),
            'ㄵ' => ('ㄴ', 'ㅈ'),
            'ㄶ' => ('ㄴ', 'ㅎ'),
            'ㄺ' => ('ㄹ', 'ㄱ'),
            'ㄻ' => ('ㄹ', 'ㅁ'),
            'ㄼ' => ('ㄹ', 'ㅂ'),
            'ㄽ' => ('ㄹ', 'ㅅ'),
            'ㄾ' => ('ㄹ', 'ㅌ'),
            'ㄿ' => ('ㄹ', 'ㅍ'),
            'ㅀ' => ('ㄹ', 'ㅎ'),
            'ㅄ' => ('ㅂ', 'ㅅ'),
            _ => ('\0', '\0'),
        };
        return first != '\0';
    }

    /// <param name="tensify">
    /// Apply 경음화. Revised Romanization ignores tensification, so the romanizer turns it off.
    /// </param>
    public static PronunciationResult Pronounce(string text, bool tensify = true)
    {
        ArgumentNullException.ThrowIfNull(text);

        var output = new StringBuilder(text.Length);
        var changes = new List<SoundChange>();
        int index = 0;
        while (index < text.Length)
        {
            if (!Syllable.IsSyllable(text[index]))
            {
                output.Append(text[index]);
                index++;
                continue;
            }

            int start = index;
            while (index < text.Length && Syllable.IsSyllable(text[index]))
            {
                index++;
            }

            output.Append(PronounceRun(text[start..index], tensify, changes));
        }

        return new PronunciationResult(output.ToString(), changes);
    }

    private static string PronounceRun(string run, bool tensify, List<SoundChange> changes)
    {
        // Working copy: the rules rewrite neighbouring syllables pairwise, left to right.
        var syllables = run.Select(Syllable.Decompose).ToArray();
        for (int i = 0; i < syllables.Length - 1; i++)
        {
            (syllables[i], syllables[i + 1]) = ApplyBoundary(syllables[i], syllables[i + 1], tensify, changes);
        }

        syllables[^1] = NeutralizeFinal(syllables[^1], changes);
        return new string(syllables.Select(s => s.Compose()).ToArray());
    }

    private static (Syllable Current, Syllable Next) ApplyBoundary(Syllable current, Syllable next, bool tensify, List<SoundChange> changes)
    {
        if (current.Final is not { } final)
        {
            return (current, next);
        }

        return next.Initial == 'ㅇ'
            ? ApplyBeforeVowel(current, next, final, tensify, changes)
            : ApplyBeforeConsonant(current, next, final, tensify, changes);
    }

    private static (Syllable, Syllable) ApplyBeforeVowel(Syllable current, Syllable next, char final, bool tensify, List<SoundChange> changes)
    {
        if (final == 'ㅇ')
        {
            return (current, next);
        }

        if (final == 'ㅎ')
        {
            changes.Add(new(SoundRule.HieutDrop, "ㅎ перед гласной не произносится"));
            return (current.WithFinal(null), next);
        }

        if (TrySplitDoubleFinal(final, out char first, out char second))
        {
            if (second == 'ㅎ')
            {
                changes.Add(new(SoundRule.HieutDrop, $"в {final} звук ㅎ выпадает, {first} переходит в следующий слог"));
                return (current.WithFinal(null), next.WithInitial(first));
            }

            char moved = tensify && IsStop(RepresentativeSound(first)) ? Tense(second) : second;
            changes.Add(new(SoundRule.Linking, $"двойной патчим {final}: {first} остаётся, {second} переходит в следующий слог"));
            if (moved != second)
            {
                changes.Add(new(SoundRule.Tensification, $"{second} после {first} становится напряжённой {moved}"));
            }

            return (current.WithFinal(first), next.WithInitial(moved));
        }

        if (next.Medial == 'ㅣ' && final is 'ㄷ' or 'ㅌ')
        {
            char palatal = final == 'ㄷ' ? 'ㅈ' : 'ㅊ';
            changes.Add(new(SoundRule.Palatalization, $"{final} перед 이 читается как {palatal}"));
            return (current.WithFinal(null), next.WithInitial(palatal));
        }

        changes.Add(new(SoundRule.Linking, $"патчим {final} переходит в следующий слог (연음)"));
        return (current.WithFinal(null), next.WithInitial(final));
    }

    private static (Syllable, Syllable) ApplyBeforeConsonant(Syllable current, Syllable next, char final, bool tensify, List<SoundChange> changes)
    {
        bool hasDouble = TrySplitDoubleFinal(final, out char first, out char second);
        char hieutCarrier = hasDouble ? second : final;
        char? remaining = hasDouble ? first : null;

        // ㅎ + ㄱ/ㄷ/ㅈ → ㅋ/ㅌ/ㅊ, ㅎ + ㅅ → ㅆ, ㅎ + ㄴ → ㄴㄴ
        if (hieutCarrier == 'ㅎ')
        {
            if (next.Initial is 'ㄱ' or 'ㄷ' or 'ㅈ')
            {
                char aspirated = Aspirate(next.Initial);
                changes.Add(new(SoundRule.Aspiration, $"ㅎ + {next.Initial} → {aspirated}"));
                return (current.WithFinal(remaining), next.WithInitial(aspirated));
            }

            if (next.Initial == 'ㅅ' && tensify)
            {
                changes.Add(new(SoundRule.Tensification, "ㅎ + ㅅ → ㅆ"));
                return (current.WithFinal(remaining), next.WithInitial('ㅆ'));
            }

            if (next.Initial == 'ㄴ' && !hasDouble)
            {
                changes.Add(new(SoundRule.Nasalization, "ㅎ перед ㄴ читается как ㄴ"));
                return (current.WithFinal('ㄴ'), next);
            }
        }

        // ㄱ/ㄷ/ㅂ/ㅈ + ㅎ → ㅋ/ㅌ/ㅍ/ㅊ
        if (next.Initial == 'ㅎ')
        {
            char stop = hasDouble ? second : final;
            char? aspirated = stop is 'ㄱ' or 'ㄷ' or 'ㅂ' or 'ㅈ'
                ? Aspirate(stop)
                : !hasDouble && IsStop(RepresentativeSound(final)) ? Aspirate(RepresentativeSound(final)) : null;
            if (aspirated is { } a)
            {
                changes.Add(new(SoundRule.Aspiration, $"{final} + ㅎ → {a}"));
                return (current.WithFinal(remaining), next.WithInitial(a));
            }
        }

        char sound = RepresentativeSound(final);
        if (sound != final)
        {
            changes.Add(new(SoundRule.Neutralization, $"патчим {final} звучит как {sound}"));
        }

        char nextInitial = next.Initial;
        if (sound == 'ㄴ' && nextInitial == 'ㄹ')
        {
            changes.Add(new(SoundRule.Liquidization, "ㄴ перед ㄹ читается как ㄹ"));
            sound = 'ㄹ';
        }
        else if (sound == 'ㄹ' && nextInitial == 'ㄴ')
        {
            changes.Add(new(SoundRule.Liquidization, "ㄴ после ㄹ читается как ㄹ"));
            nextInitial = 'ㄹ';
        }
        else if (nextInitial == 'ㄹ' && sound is not 'ㄹ')
        {
            changes.Add(new(SoundRule.RieulNasalization, $"ㄹ после {sound} читается как ㄴ"));
            nextInitial = 'ㄴ';
        }

        if (IsStop(sound) && nextInitial is 'ㄴ' or 'ㅁ')
        {
            char nasal = Nasalize(sound);
            changes.Add(new(SoundRule.Nasalization, $"{sound} перед {nextInitial} → {nasal}"));
            sound = nasal;
        }
        else if (tensify && IsStop(sound) && Tense(nextInitial) != nextInitial)
        {
            char tense = Tense(nextInitial);
            changes.Add(new(SoundRule.Tensification, $"{nextInitial} после {sound} становится напряжённой {tense}"));
            nextInitial = tense;
        }

        return (current.WithFinal(sound), next.WithInitial(nextInitial));
    }

    private static Syllable NeutralizeFinal(Syllable syllable, List<SoundChange> changes)
    {
        if (syllable.Final is not { } final)
        {
            return syllable;
        }

        char sound = RepresentativeSound(final);
        if (sound != final)
        {
            changes.Add(new(SoundRule.Neutralization, $"патчим {final} в конце звучит как {sound}"));
        }

        return syllable.WithFinal(sound);
    }

    private static bool IsStop(char sound) => sound is 'ㄱ' or 'ㄷ' or 'ㅂ';

    private static char Nasalize(char stop) => stop switch
    {
        'ㄱ' => 'ㅇ',
        'ㄷ' => 'ㄴ',
        'ㅂ' => 'ㅁ',
        _ => stop,
    };

    private static char Tense(char consonant) => consonant switch
    {
        'ㄱ' => 'ㄲ',
        'ㄷ' => 'ㄸ',
        'ㅂ' => 'ㅃ',
        'ㅅ' => 'ㅆ',
        'ㅈ' => 'ㅉ',
        _ => consonant,
    };

    private static char Aspirate(char consonant) => consonant switch
    {
        'ㄱ' => 'ㅋ',
        'ㄷ' => 'ㅌ',
        'ㅂ' => 'ㅍ',
        'ㅈ' => 'ㅊ',
        _ => consonant,
    };
}
