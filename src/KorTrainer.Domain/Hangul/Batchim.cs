namespace KorTrainer.Domain.Hangul;

/// <summary>Helpers for the final consonant (받침, "batchim") of a syllable.</summary>
public static class Batchim
{
    private const int FinalSlotCount = 28;

    /// <summary>True when the syllable has a final consonant: <c>(code − 0xAC00) % 28 != 0</c>.</summary>
    public static bool HasBatchim(char syllable)
    {
        if (!Syllable.IsSyllable(syllable))
        {
            throw new ArgumentOutOfRangeException(nameof(syllable), syllable, "Not a precomposed Hangul syllable.");
        }

        return (syllable - Syllable.First) % FinalSlotCount != 0;
    }

    /// <summary>Checks the last Hangul syllable of a word, ignoring trailing punctuation and spaces.</summary>
    public static bool EndsWithBatchim(string word) => HasBatchim(LastSyllable(word));

    /// <summary>True when the word ends in the final ㄹ (matters for (으)로, -(으)니까 and ㄹ-stems).</summary>
    public static bool EndsWithRieul(string word) => Syllable.Decompose(LastSyllable(word)).Final == 'ㄹ';

    public static char LastSyllable(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        for (int i = word.Length - 1; i >= 0; i--)
        {
            if (Syllable.IsSyllable(word[i]))
            {
                return word[i];
            }
        }

        throw new ArgumentException($"'{word}' contains no Hangul syllables.", nameof(word));
    }
}
