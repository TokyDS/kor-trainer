using KorTrainer.Domain.Exercises;
using KorTrainer.Domain.Hangul;

namespace KorTrainer.Domain.Typing;

/// <param name="Jamo">Every key the lesson may use (cumulative with earlier lessons).</param>
/// <param name="NewJamo">Keys this lesson introduces.</param>
public sealed record TypingLesson(int Number, string Title, string Jamo, string NewJamo);

/// <summary>Row-by-row lessons of the 두벌식 layout, then words and sentences.</summary>
public static class TypingLessons
{
    public const int WordsLevel = 5;
    public const int SentencesLevel = 6;

    private const string Home = "ㅁㄴㅇㄹㅎㅗㅓㅏㅣ";
    private const string Top = "ㅂㅈㄷㄱㅅㅛㅕㅑㅐㅔ";
    private const string Bottom = "ㅋㅌㅊㅍㅠㅜㅡ";
    private const string Shifted = "ㅃㅉㄸㄲㅆㅒㅖ";

    public static IReadOnlyList<TypingLesson> Lessons { get; } =
    [
        new(1, "Основной ряд", Home, Home),
        new(2, "Верхний ряд", Home + Top, Top),
        new(3, "Нижний ряд", Home + Top + Bottom, Bottom),
        new(4, "Shift: двойные согласные, ㅒ ㅖ", Home + Top + Bottom + Shifted, Shifted),
    ];

    /// <summary>
    /// Practice items for a row lesson: real words that use only the lesson's keys (when given), topped up with
    /// generated syllables that stress the new keys.
    /// </summary>
    public static IReadOnlyList<string> Generate(int level, int count, Random random, IEnumerable<string>? words = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, Lessons.Count);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var lesson = Lessons[level - 1];
        var keys = lesson.Jamo.ToHashSet();

        var real = (words ?? [])
            .Where(w => w.Length is >= 1 and <= 4 && w.All(Syllable.IsSyllable) && Dubeolsik.Keystrokes(w).All(keys.Contains))
            .Distinct()
            .Shuffled(random)
            .Take(count / 2)
            .ToList();

        var consonants = lesson.Jamo.Where(HangulAlphabet.IsInitial).ToList();
        var vowels = lesson.Jamo.Where(HangulAlphabet.IsMedial).ToList();
        var newConsonants = lesson.NewJamo.Where(HangulAlphabet.IsInitial).ToList();
        var newVowels = lesson.NewJamo.Where(HangulAlphabet.IsMedial).ToList();
        var items = new List<string>(real);
        while (items.Count < count)
        {
            int length = 1 + random.Next(3);
            var syllables = Enumerable.Range(0, length).Select(_ =>
            {
                // Every generated syllable uses at least one key the lesson introduces.
                bool newConsonant = newConsonants.Count > 0 && (newVowels.Count == 0 || random.Next(2) == 0);
                char initial = newConsonant ? newConsonants.Pick(random) : consonants.Pick(random);
                char medial = !newConsonant && newVowels.Count > 0 ? newVowels.Pick(random) : vowels.Pick(random);
                char? final = random.Next(3) == 0 ? consonants.Where(HangulAlphabet.IsFinal).ToList() is { Count: > 0 } finals ? finals.Pick(random) : null : null;
                return new Syllable(initial, medial, final).Compose();
            });
            items.Add(new string(syllables.ToArray()));
        }

        return items.Shuffled(random).ToList();
    }
}
