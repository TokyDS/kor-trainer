namespace KorTrainer.Domain.Hangul;

/// <summary>
/// A precomposed Hangul syllable split into its jamo.
/// Decomposition and composition use the Unicode arithmetic
/// <c>code = 0xAC00 + (initial × 21 + medial) × 28 + final</c>, never lookup tables of syllables.
/// </summary>
public readonly record struct Syllable
{
    public const char First = '가';
    public const char Last = '힣';

    private const int MedialCount = 21;
    private const int FinalSlotCount = 28;
    private const int InitialBlockSize = MedialCount * FinalSlotCount;

    public Syllable(char initial, char medial, char? final = null)
    {
        if (!HangulAlphabet.IsInitial(initial))
        {
            throw new ArgumentException($"'{initial}' is not an initial consonant.", nameof(initial));
        }

        if (!HangulAlphabet.IsMedial(medial))
        {
            throw new ArgumentException($"'{medial}' is not a vowel.", nameof(medial));
        }

        if (final is { } f && !HangulAlphabet.IsFinal(f))
        {
            throw new ArgumentException($"'{f}' cannot be a final consonant.", nameof(final));
        }

        Initial = initial;
        Medial = medial;
        Final = final;
    }

    public char Initial { get; }

    public char Medial { get; }

    public char? Final { get; }

    public bool HasFinal => Final is not null;

    /// <summary>Jamo in writing order, e.g. 한 → ㅎ ㅏ ㄴ.</summary>
    public IReadOnlyList<char> Jamo => Final is { } f ? [Initial, Medial, f] : [Initial, Medial];

    public static bool IsSyllable(char c) => c is >= First and <= Last;

    public static Syllable Decompose(char syllable)
    {
        if (!IsSyllable(syllable))
        {
            throw new ArgumentOutOfRangeException(nameof(syllable), syllable, "Not a precomposed Hangul syllable.");
        }

        int index = syllable - First;
        int initial = index / InitialBlockSize;
        int medial = index % InitialBlockSize / FinalSlotCount;
        int final = index % FinalSlotCount;

        return new Syllable(
            HangulAlphabet.Initials[initial],
            HangulAlphabet.Medials[medial],
            final == 0 ? null : HangulAlphabet.Finals[final - 1]);
    }

    public char Compose()
    {
        int initial = HangulAlphabet.Initials.IndexOf(Initial);
        int medial = HangulAlphabet.Medials.IndexOf(Medial);
        int final = Final is { } f ? HangulAlphabet.Finals.IndexOf(f) + 1 : 0;
        return (char)(First + ((initial * MedialCount) + medial) * FinalSlotCount + final);
    }

    public Syllable WithInitial(char initial) => new(initial, Medial, Final);

    public Syllable WithMedial(char medial) => new(Initial, medial, Final);

    public Syllable WithFinal(char? final) => new(Initial, Medial, final);

    public override string ToString() => Compose().ToString();
}
