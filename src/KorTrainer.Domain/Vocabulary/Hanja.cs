using System.Globalization;
using System.Text;
using KorTrainer.Domain.Entities;

namespace KorTrainer.Domain.Vocabulary;

/// <summary>The traditional native name (훈, e.g. 배울 for 學) and a Russian gloss of a character.</summary>
public sealed record HanjaGloss(string Hun, string Russian);

/// <summary>A deck word with its hanja spelling ("學校"; "始作-" when 하다 is native, "-番" when the start is).</summary>
public sealed record HanjaWord(string Key, string Korean, string Russian, PartOfSpeech PartOfSpeech, string Hanja, int Rank);

/// <param name="Index">Position of the syllable in the Korean word.</param>
public sealed record HanjaPair(int Index, char Syllable, char Character);

/// <summary>A character and the deck words that contain it, most frequent first.</summary>
/// <param name="Reading">The Sino-Korean syllable it is read as in these words (學 → 학).</param>
public sealed record HanjaRoot(char Character, char Reading, HanjaGloss? Gloss, IReadOnlyList<HanjaWord> Words);

/// <summary>Sino-Korean roots: which syllable of a word comes from which character, and word families.</summary>
public static class Hanja
{
    private const char NativePart = '-';

    /// <summary>NFKC: CJK compatibility ideographs (e.g. U+F981) become the unified ones.</summary>
    public static string Normalize(string hanja) => (hanja ?? string.Empty).Normalize(NormalizationForm.FormKC).Trim();

    public static bool IsHanja(char c) => c is >= '一' and <= '鿿';

    /// <summary>
    /// Pairs Korean syllables with characters. A '-' stands for the native part: "始作-" covers 시작 of 시작하다,
    /// "-番" covers 번 of 이번. Returns nothing when the spelling does not fit the word.
    /// </summary>
    public static IReadOnlyList<HanjaPair> Align(string korean, PartOfSpeech partOfSpeech, string? hanja)
    {
        ArgumentNullException.ThrowIfNull(korean);
        string spelling = Normalize(hanja ?? string.Empty);
        if (spelling.Length == 0 || !spelling.Any(IsHanja) || !spelling.All(c => IsHanja(c) || c == NativePart))
        {
            return [];
        }

        bool verb = partOfSpeech is PartOfSpeech.Verb or PartOfSpeech.Adjective && korean.EndsWith('다');
        string word = verb && spelling.EndsWith(NativePart) ? korean[..^1] : korean;
        var parts = spelling.Split(NativePart);
        return parts.Length switch
        {
            1 when spelling.Length == word.Length => Pairs(word, spelling, 0),
            2 when parts[0].Length + parts[1].Length < word.Length || (parts[1].Length == 0 && parts[0].Length <= word.Length) =>
                [.. Pairs(word, parts[0], 0), .. Pairs(word, parts[1], word.Length - parts[1].Length)],
            _ => [],
        };
    }

    /// <summary>Characters shared by the words, with their word families (largest first).</summary>
    public static IReadOnlyList<HanjaRoot> Roots(IEnumerable<HanjaWord> words, IReadOnlyDictionary<char, HanjaGloss> glosses)
    {
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(glosses);
        return words
            .SelectMany(w => Align(w.Korean, w.PartOfSpeech, w.Hanja).Select(p => (Word: w, Pair: p)))
            .GroupBy(x => x.Pair.Character)
            .Select(g => new HanjaRoot(
                g.Key,
                g.GroupBy(x => x.Pair.Syllable).OrderByDescending(r => r.Count()).First().Key,
                glosses.GetValueOrDefault(g.Key),
                g.Select(x => x.Word).DistinctBy(w => w.Key).OrderBy(w => w.Rank).ToList()))
            .OrderByDescending(r => r.Words.Count)
            .ThenBy(r => r.Words[0].Rank)
            .ToList();
    }

    /// <summary>"大 большой + 學 учёба" — the roots of a word as a reading aid.</summary>
    public static string Explain(IReadOnlyList<HanjaPair> pairs, IReadOnlyDictionary<char, HanjaGloss> glosses) =>
        string.Join(" + ", pairs.Select(p => glosses.TryGetValue(p.Character, out var g) ? $"{p.Character} {g.Russian}" : p.Character.ToString(CultureInfo.InvariantCulture)));

    private static List<HanjaPair> Pairs(string word, string characters, int offset) =>
        characters.Select((c, i) => new HanjaPair(offset + i, word[offset + i], c)).ToList();
}
