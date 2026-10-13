using System.Text;

namespace KorTrainer.Domain.Hangul;

/// <summary>
/// Revised Romanization of Korean, applied to the pronounced form so that
/// 한국어 becomes "hangugeo" and 감사합니다 becomes "gamsahamnida".
/// Shown only when the learner enables romanization in settings.
/// </summary>
public static class Romanizer
{
    public static string Romanize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string pronounced = Pronunciation.Pronounce(text, tensify: false).Text;
        var output = new StringBuilder(pronounced.Length * 3);
        char? previousFinal = null;
        foreach (char c in pronounced)
        {
            if (!Syllable.IsSyllable(c))
            {
                output.Append(c);
                previousFinal = null;
                continue;
            }

            var syllable = Syllable.Decompose(c);
            output.Append(syllable.Initial == 'ㄹ' && previousFinal == 'ㄹ' ? "l" : Initial(syllable.Initial));
            output.Append(Medial(syllable.Medial));
            if (syllable.Final is { } final)
            {
                output.Append(Final(final));
            }

            previousFinal = syllable.Final;
        }

        return output.ToString();
    }

    public static string Initial(char jamo) => jamo switch
    {
        'ㄱ' => "g",
        'ㄲ' => "kk",
        'ㄴ' => "n",
        'ㄷ' => "d",
        'ㄸ' => "tt",
        'ㄹ' => "r",
        'ㅁ' => "m",
        'ㅂ' => "b",
        'ㅃ' => "pp",
        'ㅅ' => "s",
        'ㅆ' => "ss",
        'ㅇ' => string.Empty,
        'ㅈ' => "j",
        'ㅉ' => "jj",
        'ㅊ' => "ch",
        'ㅋ' => "k",
        'ㅌ' => "t",
        'ㅍ' => "p",
        'ㅎ' => "h",
        _ => throw new ArgumentOutOfRangeException(nameof(jamo), jamo, "Not an initial consonant."),
    };

    public static string Medial(char jamo) => jamo switch
    {
        'ㅏ' => "a",
        'ㅐ' => "ae",
        'ㅑ' => "ya",
        'ㅒ' => "yae",
        'ㅓ' => "eo",
        'ㅔ' => "e",
        'ㅕ' => "yeo",
        'ㅖ' => "ye",
        'ㅗ' => "o",
        'ㅘ' => "wa",
        'ㅙ' => "wae",
        'ㅚ' => "oe",
        'ㅛ' => "yo",
        'ㅜ' => "u",
        'ㅝ' => "wo",
        'ㅞ' => "we",
        'ㅟ' => "wi",
        'ㅠ' => "yu",
        'ㅡ' => "eu",
        'ㅢ' => "ui",
        'ㅣ' => "i",
        _ => throw new ArgumentOutOfRangeException(nameof(jamo), jamo, "Not a vowel."),
    };

    /// <summary>Romanizes a final consonant by the sound it actually makes.</summary>
    public static string Final(char jamo) => Pronunciation.RepresentativeSound(jamo) switch
    {
        'ㄱ' => "k",
        'ㄴ' => "n",
        'ㄷ' => "t",
        'ㄹ' => "l",
        'ㅁ' => "m",
        'ㅂ' => "p",
        _ => "ng",
    };
}
