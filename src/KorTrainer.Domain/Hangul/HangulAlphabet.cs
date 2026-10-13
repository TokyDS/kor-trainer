namespace KorTrainer.Domain.Hangul;

/// <summary>
/// Ordered jamo inventories of the Unicode Hangul Syllables block (U+AC00–U+D7A3).
/// Only the alphabet itself is listed here; syllables are always computed arithmetically.
/// </summary>
public static class HangulAlphabet
{
    /// <summary>19 initial consonants in Unicode order.</summary>
    public const string Initials = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";

    /// <summary>21 vowels in Unicode order.</summary>
    public const string Medials = "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ";

    /// <summary>27 final consonants in Unicode order; final index 0 means "no final".</summary>
    public const string Finals = "ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ";

    /// <summary>Vowels written to the right of the initial consonant (vertical stroke).</summary>
    public const string VerticalVowels = "ㅏㅐㅑㅒㅓㅔㅕㅖㅣ";

    /// <summary>Vowels written below the initial consonant (horizontal stroke).</summary>
    public const string HorizontalVowels = "ㅗㅛㅜㅠㅡ";

    public static bool IsInitial(char jamo) => Initials.Contains(jamo);

    public static bool IsMedial(char jamo) => Medials.Contains(jamo);

    public static bool IsFinal(char jamo) => Finals.Contains(jamo);
}
