using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Grammar;
using KorTrainer.Domain.Hangul;

namespace KorTrainer.Domain.Vocabulary;

/// <summary>A deck word as the index needs it.</summary>
public sealed record IndexedWord(string Key, string Korean, PartOfSpeech PartOfSpeech, Irregularity Irregularity, int Rank);

/// <param name="Text">The token as written, with punctuation.</param>
/// <param name="Hangul">The Hangul part that was looked up ("3시에" → "시에").</param>
/// <param name="Words">Deck words it can be, most frequent first; empty when unknown.</param>
public sealed record TextToken(string Text, string Hangul, IReadOnlyList<IndexedWord> Words)
{
    public bool IsKnown => Words.Count > 0;
}

/// <summary>
/// Recognizes inflected words in running Korean text (학교에서, 먹었어요, 더운) by their dictionary entry.
/// Instead of a morphological analyzer it generates the stems every deck verb and adjective takes
/// (via <see cref="Conjugator"/>) and finds the longest stem or noun a token starts with — particles
/// and endings simply follow. Good enough to link sentences and texts to the deck; not a parser.
/// </summary>
public sealed class WordIndex
{
    private const int MaxPhraseTokens = 4;

    private readonly Dictionary<string, List<IndexedWord>> _forms = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<IndexedWord>> _phrases = new(StringComparer.Ordinal);

    public WordIndex(IEnumerable<IndexedWord> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        foreach (var word in words)
        {
            if (word.Korean.Contains(' ', StringComparison.Ordinal))
            {
                Add(_phrases, string.Join(' ', word.Korean.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(HangulCore)), word);
                continue;
            }

            foreach (string form in Forms(word.Korean, word.PartOfSpeech, word.Irregularity))
            {
                Add(_forms, form, word);
            }
        }

        foreach (var list in _forms.Values.Concat(_phrases.Values))
        {
            list.Sort((a, b) => a.Rank.CompareTo(b.Rank));
        }
    }

    /// <summary>Deck words a single token can be (longest known prefix wins), most frequent first.</summary>
    public IReadOnlyList<IndexedWord> Lookup(string token)
    {
        string hangul = HangulCore(token);
        for (int length = hangul.Length; length > 0; length--)
        {
            if (_forms.TryGetValue(hangul[..length], out var words) && Plausible(hangul, length))
            {
                return words;
            }
        }

        return [];
    }

    /// <summary>
    /// A one-syllable match (가, 보, 시) only counts when what follows looks like a particle or ending —
    /// otherwise an unknown word such as 가수 would be read as 가다 + 수.
    /// </summary>
    private static bool Plausible(string token, int matched)
    {
        if (matched > 1 || matched == token.Length)
        {
            return true;
        }

        string rest = token[matched..];
        return Particles.Contains(rest) || EndingStarts.Contains(rest[0]);
    }

    private static readonly HashSet<string> Particles = new(StringComparer.Ordinal)
    {
        "이", "가", "은", "는", "을", "를", "에", "에서", "의", "도", "와", "과", "로", "으로", "하고", "랑", "이랑",
        "에게", "한테", "까지", "부터", "만", "보다", "처럼", "요", "께", "께서", "이에요", "예요", "이다", "입니다",
    };

    // First syllables of common verb endings after a bare or contracted stem.
    private static readonly HashSet<char> EndingStarts =
    [
        '어', '아', '여', '었', '았', '였', '고', '지', '게', '기', '는', '니', '면', '서', '세', '시', '습', '겠', '네', '다', '던', '도', '요', '죠', '자',
        '라', '러', '려', '며', '야', '냐', '나', '셨', '십', '실', '신', '를', '에', '은', '이', '가', '을', '의', '와', '과', '로', '만',
    ];

    /// <summary>Splits text at spaces and recognizes every token; multi-word deck phrases become one token.</summary>
    public IReadOnlyList<TextToken> Analyze(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var pieces = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var tokens = new List<TextToken>();
        for (int i = 0; i < pieces.Length;)
        {
            if (MatchPhrase(pieces, i) is { } phrase)
            {
                tokens.Add(phrase.Token);
                i += phrase.Length;
                continue;
            }

            tokens.Add(new TextToken(pieces[i], HangulCore(pieces[i]), Lookup(pieces[i])));
            i++;
        }

        return tokens;
    }

    /// <summary>Share of Hangul tokens that were recognized (1 for text without Hangul).</summary>
    public static double Coverage(IReadOnlyList<TextToken> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        var hangul = tokens.Where(t => t.Hangul.Length > 0).ToList();
        return hangul.Count == 0 ? 1 : (double)hangul.Count(t => t.IsKnown) / hangul.Count;
    }

    /// <summary>The surface stems a word is recognized by.</summary>
    public static IReadOnlySet<string> Forms(string korean, PartOfSpeech partOfSpeech, Irregularity irregularity)
    {
        ArgumentNullException.ThrowIfNull(korean);
        var forms = new HashSet<string>(StringComparer.Ordinal) { korean };
        // Auxiliaries such as 싶다 are listed as phrases in the deck but conjugate like verbs.
        bool conjugates = partOfSpeech is PartOfSpeech.Verb or PartOfSpeech.Adjective or PartOfSpeech.Phrase
            && korean.Length >= 2 && korean[^1] == '다' && korean.All(Syllable.IsSyllable);
        if (!conjugates)
        {
            return forms;
        }

        try
        {
            string stem = Conjugator.Stem(korean);
            string aeo = Strip(Conjugator.Conjugate(korean, Ending.PolitePresent, irregularity).Form, "요");
            string past = Strip(Conjugator.Conjugate(korean, Ending.PolitePast, irregularity).Form, "어요");
            string eu = Strip(Conjugator.Conjugate(korean, Ending.Because, irregularity).Form, "니까");
            string future = Strip(Conjugator.Conjugate(korean, Ending.Can, irregularity).Form, " 수 있어요");
            forms.UnionWith(
            [
                stem,
                aeo,
                past,
                eu,
                future,
                Conjugator.Conjugate(korean, Ending.FormalPresent, irregularity).Form,
                WithFinalIfOpen(eu, 'ㄴ'),
                Present(stem),
            ]);
        }
        catch (ArgumentException)
        {
            // Not a regular dictionary form (e.g. a contraction): the word itself is enough.
        }

        forms.RemoveWhere(f => f.Length == 0);
        return forms;
    }

    private (TextToken Token, int Length)? MatchPhrase(string[] pieces, int start)
    {
        for (int length = Math.Min(MaxPhraseTokens, pieces.Length - start); length >= 2; length--)
        {
            var span = pieces.Skip(start).Take(length).ToList();
            string key = string.Join(' ', span.Select(HangulCore));
            if (_phrases.TryGetValue(key, out var words))
            {
                return (new TextToken(string.Join(' ', span), key, words), length);
            }
        }

        return null;
    }

    /// <summary>The Hangul run of a token: "3시에" → "시에", "가요!" → "가요".</summary>
    private static string HangulCore(string token)
    {
        int start = 0;
        while (start < token.Length && !Syllable.IsSyllable(token[start]))
        {
            start++;
        }

        int end = start;
        while (end < token.Length && Syllable.IsSyllable(token[end]))
        {
            end++;
        }

        return token[start..end];
    }

    private static void Add(Dictionary<string, List<IndexedWord>> map, string form, IndexedWord word)
    {
        if (form.Length == 0)
        {
            return;
        }

        if (!map.TryGetValue(form, out var list))
        {
            map[form] = list = [];
        }

        if (!list.Contains(word))
        {
            list.Add(word);
        }
    }

    private static string Strip(string form, string suffix) =>
        form.EndsWith(suffix, StringComparison.Ordinal) ? form[..^suffix.Length] : form;

    /// <summary>먹으 → 먹은, 가 → 간: the -(으)ㄴ attributive.</summary>
    private static string WithFinalIfOpen(string stem, char final)
    {
        var last = Syllable.Decompose(stem[^1]);
        return last.HasFinal ? stem : stem[..^1] + last.WithFinal(final).Compose();
    }

    /// <summary>The -는 attributive: 먹는, 만드는 (ㄹ drops).</summary>
    private static string Present(string stem)
    {
        var last = Syllable.Decompose(stem[^1]);
        return last.Final == 'ㄹ' ? stem[..^1] + last.WithFinal(null).Compose() + "는" : stem + "는";
    }
}
