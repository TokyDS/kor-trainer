using KorTrainer.Domain.Hangul;

namespace KorTrainer.Domain.Typing;

/// <summary>
/// The syllable-building automaton of a Korean keyboard: jamo typed one by one become syllables.
/// A consonant after a vowel becomes the final (patchim) and jumps to the next syllable when a vowel
/// follows (달 + ㄱ + ㅣ → 달기); vowels and finals combine (ㅗ + ㅏ → ㅘ, ㄹ + ㄱ → ㄺ).
/// </summary>
public sealed class HangulComposer
{
    private static readonly Dictionary<(char, char), char> VowelPairs = new()
    {
        [('ㅗ', 'ㅏ')] = 'ㅘ', [('ㅗ', 'ㅐ')] = 'ㅙ', [('ㅗ', 'ㅣ')] = 'ㅚ',
        [('ㅜ', 'ㅓ')] = 'ㅝ', [('ㅜ', 'ㅔ')] = 'ㅞ', [('ㅜ', 'ㅣ')] = 'ㅟ', [('ㅡ', 'ㅣ')] = 'ㅢ',
    };

    private static readonly Dictionary<(char, char), char> FinalPairs = new()
    {
        [('ㄱ', 'ㅅ')] = 'ㄳ', [('ㄴ', 'ㅈ')] = 'ㄵ', [('ㄴ', 'ㅎ')] = 'ㄶ', [('ㄹ', 'ㄱ')] = 'ㄺ', [('ㄹ', 'ㅁ')] = 'ㄻ',
        [('ㄹ', 'ㅂ')] = 'ㄼ', [('ㄹ', 'ㅅ')] = 'ㄽ', [('ㄹ', 'ㅌ')] = 'ㄾ', [('ㄹ', 'ㅍ')] = 'ㄿ', [('ㄹ', 'ㅎ')] = 'ㅀ', [('ㅂ', 'ㅅ')] = 'ㅄ',
    };

    private static readonly Dictionary<char, (char First, char Second)> SplitVowels = VowelPairs.ToDictionary(p => p.Value, p => p.Key);
    private static readonly Dictionary<char, (char First, char Second)> SplitFinals = FinalPairs.ToDictionary(p => p.Value, p => p.Key);

    private string _committed = string.Empty;
    private char? _initial;
    private char? _medial;
    private char? _final;

    public string Text => _committed + Current;

    /// <summary>Starts over from text typed some other way (e.g. by the phone's own keyboard).</summary>
    public void Reset(string text = "")
    {
        _committed = text ?? string.Empty;
        _initial = _medial = _final = null;
    }

    public void Type(char jamo)
    {
        if (HangulAlphabet.IsMedial(jamo))
        {
            TypeVowel(jamo);
        }
        else if (HangulAlphabet.IsInitial(jamo))
        {
            TypeConsonant(jamo);
        }
        else
        {
            Commit();
            _committed += jamo;
        }
    }

    public void Backspace()
    {
        // Like phone keyboards, a finished syllable reopens and loses one jamo at a time (한 → 하 → ㅎ).
        if (_initial is null && _medial is null && _committed.Length > 0 && Syllable.IsSyllable(_committed[^1]))
        {
            var last = Syllable.Decompose(_committed[^1]);
            _committed = _committed[..^1];
            (_initial, _medial, _final) = (last.Initial, last.Medial, last.Final);
        }

        if (_final is { } final)
        {
            _final = SplitFinals.TryGetValue(final, out var pair) ? pair.First : null;
        }
        else if (_medial is { } medial)
        {
            _medial = SplitVowels.TryGetValue(medial, out var pair) ? pair.First : null;
        }
        else if (_initial is not null)
        {
            _initial = null;
        }
        else if (_committed.Length > 0)
        {
            _committed = _committed[..^1];
        }
    }

    /// <summary>Splits text into the keystrokes that produce it (compound vowels and finals take two keys).</summary>
    public static IReadOnlyList<char> Keystrokes(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var keys = new List<char>();
        foreach (char c in text)
        {
            if (!Syllable.IsSyllable(c))
            {
                keys.AddRange(SplitVowels.TryGetValue(c, out var v) ? [v.First, v.Second] : SplitFinals.TryGetValue(c, out var f) ? [f.First, f.Second] : [c]);
                continue;
            }

            var syllable = Syllable.Decompose(c);
            keys.Add(syllable.Initial);
            keys.AddRange(SplitVowels.TryGetValue(syllable.Medial, out var vowel) ? [vowel.First, vowel.Second] : [syllable.Medial]);
            if (syllable.Final is { } fin)
            {
                keys.AddRange(SplitFinals.TryGetValue(fin, out var pair) ? [pair.First, pair.Second] : [fin]);
            }
        }

        return keys;
    }

    private string Current => (_initial, _medial) switch
    {
        ({ } i, { } m) => new Syllable(i, m, _final).Compose().ToString(),
        ({ } i, null) => i.ToString(),
        (null, { } m) => m.ToString(),
        _ => string.Empty,
    };

    private void TypeConsonant(char c)
    {
        if (_initial is not null && _medial is not null && _final is null && HangulAlphabet.IsFinal(c))
        {
            _final = c;
        }
        else if (_final is { } final && FinalPairs.TryGetValue((final, c), out var pair))
        {
            _final = pair;
        }
        else
        {
            Commit();
            _initial = c;
        }
    }

    private void TypeVowel(char v)
    {
        if (_final is { } final)
        {
            // The final moves on as the initial of a new syllable (the second part of a compound final).
            char moved;
            if (SplitFinals.TryGetValue(final, out var pair))
            {
                _final = pair.First;
                moved = pair.Second;
            }
            else
            {
                _final = null;
                moved = final;
            }

            Commit();
            _initial = moved;
            _medial = v;
        }
        else if (_medial is { } medial && VowelPairs.TryGetValue((medial, v), out var combined))
        {
            _medial = combined;
        }
        else if (_initial is not null && _medial is null)
        {
            _medial = v;
        }
        else
        {
            Commit();
            _medial = v;
        }
    }

    private void Commit()
    {
        _committed += Current;
        _initial = _medial = _final = null;
    }
}
