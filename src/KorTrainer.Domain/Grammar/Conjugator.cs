using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Hangul;

namespace KorTrainer.Domain.Grammar;

public enum Ending
{
    /// <summary>-아/어요: polite present.</summary>
    PolitePresent,

    /// <summary>-습니다/ㅂ니다: formal present.</summary>
    FormalPresent,

    /// <summary>-았/었어요: polite past.</summary>
    PolitePast,

    /// <summary>-고: "and".</summary>
    And,

    /// <summary>-아서/어서: "and so / because".</summary>
    SoThat,

    /// <summary>-(으)니까: "because".</summary>
    Because,

    /// <summary>-고 싶어요: "want to".</summary>
    Want,

    /// <summary>-(으)ㄹ 수 있어요: "can".</summary>
    Can,

    /// <summary>-아/어야 해요: "must".</summary>
    Must,
}

public sealed record Conjugation(string Form, string Explanation);

/// <summary>
/// Conjugates verbs and adjectives from their dictionary form (가다, 먹다, 듣다 …) by rule.
/// Irregular classes cannot be detected from spelling and are passed in from the deck.
/// </summary>
public static class Conjugator
{
    private const string BrightVowels = "ㅏㅑㅗ";

    public static string Stem(string dictionaryForm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dictionaryForm);
        if (dictionaryForm.Length < 2 || dictionaryForm[^1] != '다' || !dictionaryForm.All(Syllable.IsSyllable))
        {
            throw new ArgumentException($"'{dictionaryForm}' is not a dictionary form ending in 다.", nameof(dictionaryForm));
        }

        return dictionaryForm[..^1];
    }

    public static Conjugation Conjugate(string verb, Ending ending, Irregularity irregularity = Irregularity.None)
    {
        string stem = Stem(verb);
        return ending switch
        {
            Ending.PolitePresent => Aeo(stem, irregularity, "요", "-아/어요"),
            Ending.PolitePast => Past(stem, irregularity),
            Ending.FormalPresent => Formal(stem),
            Ending.And => new(stem + "고", $"-고 просто присоединяется к основе {stem}: {stem}고."),
            Ending.Want => new(stem + "고 싶어요", $"-고 싶다 присоединяется к основе {stem}: {stem}고 싶어요."),
            Ending.SoThat => Aeo(stem, irregularity, "서", "-아서/어서"),
            Ending.Must => Aeo(stem, irregularity, "야 해요", "-아/어야 하다"),
            Ending.Because => EuForm(stem, irregularity, "니까", "-(으)니까"),
            Ending.Can => Can(stem, irregularity),
            _ => throw new ArgumentOutOfRangeException(nameof(ending), ending, null),
        };
    }

    /// <summary>
    /// Plausible wrong forms a learner produces: wrong vowel harmony, no contraction,
    /// ignoring (or wrongly applying) an irregular class, wrong 으 insertion.
    /// </summary>
    public static IReadOnlyList<string> CommonMistakes(string verb, Ending ending, Irregularity irregularity = Irregularity.None)
    {
        string stem = Stem(verb);
        string correct = Conjugate(verb, ending, irregularity).Form;
        var swappedIrregularity = irregularity == Irregularity.None ? GuessIrregularity(stem) : Irregularity.None;
        var mistakes = new List<string>();

        string? suffix = ending switch
        {
            Ending.PolitePresent => "요",
            Ending.SoThat => "서",
            Ending.Must => "야 해요",
            _ => null,
        };

        if (suffix is not null)
        {
            mistakes.Add(RawAeo(stem, bright: !IsBright(stem)) + suffix);
            mistakes.Add(RawAeo(stem, IsBright(stem)) + suffix);
            mistakes.Add(SafeAeo(stem, swappedIrregularity) + suffix);
        }
        else if (ending == Ending.PolitePast)
        {
            mistakes.Add(PastOf(RawAeo(stem, !IsBright(stem))));
            mistakes.Add(PastOf(RawAeo(stem, IsBright(stem))));
            mistakes.Add(PastOf(SafeAeo(stem, swappedIrregularity)));
            mistakes.Add(SafeAeo(stem, irregularity) + "요");
        }
        else if (ending == Ending.FormalPresent)
        {
            mistakes.Add(stem + "습니다");
            mistakes.Add(stem + "읍니다");
            mistakes.Add(WithFinal(stem, 'ㅂ') is { } b ? b + "니다" : stem + "ㅂ니다");
        }
        else if (ending is Ending.Because or Ending.Can)
        {
            mistakes.Add(ending == Ending.Because ? stem + "으니까" : stem + "을 수 있어요");
            mistakes.Add(ending == Ending.Because ? stem + "니까" : (WithFinal(stem, 'ㄹ') ?? stem) + " 수 있어요");
            mistakes.Add(Conjugate(verb, ending, swappedIrregularity).Form);

            // Over-using the -아/어 form or the present attributive -는 is the other classic slip.
            string aeo = SafeAeo(stem, irregularity);
            mistakes.Add(ending == Ending.Because ? aeo + "서니까" : aeo + " 수 있어요");
            mistakes.Add(ending == Ending.Because ? stem + "는니까" : stem + "는 수 있어요");
        }
        else
        {
            mistakes.Add(RawAeo(stem, IsBright(stem)) + (ending == Ending.Want ? "고 싶어요" : "고"));
            mistakes.Add(stem + (ending == Ending.Want ? "으고 싶어요" : "으고"));
            mistakes.Add(stem + (ending == Ending.Want ? "고 싶다요" : "구"));
        }

        return mistakes.Where(m => m != correct).Distinct().ToList();
    }

    public static string EndingLabel(Ending ending) => ending switch
    {
        Ending.PolitePresent => "вежливое настоящее -아/어요",
        Ending.FormalPresent => "официальное -습니다/ㅂ니다",
        Ending.PolitePast => "прошедшее -았/었어요",
        Ending.And => "соединительное -고 «и»",
        Ending.SoThat => "-아서/어서 «и поэтому»",
        Ending.Because => "-(으)니까 «потому что»",
        Ending.Want => "-고 싶어요 «хочу»",
        Ending.Can => "-(으)ㄹ 수 있어요 «могу»",
        Ending.Must => "-아/어야 해요 «нужно»",
        _ => ending.ToString(),
    };

    private static Conjugation Aeo(string stem, Irregularity irregularity, string suffix, string label)
    {
        var (form, rule) = AeoWithRule(stem, irregularity);
        return new(form + suffix, $"{label}: {rule} → {form}{suffix}.");
    }

    private static Conjugation Past(string stem, Irregularity irregularity)
    {
        var (form, rule) = AeoWithRule(stem, irregularity);
        string past = PastOf(form);
        return new(past, $"Прошедшее время строится от формы на -아/어: {rule} → {form}; к последнему слогу добавляется ㅆ и -어요 → {past}.");
    }

    private static string PastOf(string aeoForm)
    {
        var last = Syllable.Decompose(aeoForm[^1]);
        return aeoForm[..^1] + last.WithFinal('ㅆ') + "어요";
    }

    private static Conjugation Formal(string stem)
    {
        var last = Syllable.Decompose(stem[^1]);
        if (last.Final == 'ㄹ')
        {
            string form = stem[..^1] + last.WithFinal('ㅂ') + "니다";
            return new(form, $"Основа {stem} оканчивается на ㄹ: ㄹ выпадает, добавляется ㅂ니다 → {form}.");
        }

        if (last.HasFinal)
        {
            return new(stem + "습니다", $"Основа {stem} оканчивается на согласную → -습니다: {stem}습니다.");
        }

        string vowelForm = stem[..^1] + last.WithFinal('ㅂ') + "니다";
        return new(vowelForm, $"Основа {stem} оканчивается на гласную → ㅂ становится патчимом последнего слога + 니다: {vowelForm}.");
    }

    private static Conjugation EuForm(string stem, Irregularity irregularity, string ending, string label)
    {
        var last = Syllable.Decompose(stem[^1]);
        string head = stem[..^1];
        var (form, rule) = (irregularity, last.Final) switch
        {
            (Irregularity.Bieup, 'ㅂ') => (head + last.WithFinal(null) + "우" + ending, "ㅂ-неправильный: ㅂ → 우"),
            (Irregularity.Digeut, 'ㄷ') => (head + last.WithFinal('ㄹ') + "으" + ending, "ㄷ-неправильный: ㄷ → ㄹ перед гласной"),
            (Irregularity.Siot, 'ㅅ') => (head + last.WithFinal(null) + "으" + ending, "ㅅ-неправильный: ㅅ выпадает"),
            (Irregularity.Hieut, 'ㅎ') => (head + last.WithFinal(null) + ending, "ㅎ-неправильный: ㅎ выпадает"),
            (_, 'ㄹ') => (head + last.WithFinal(null) + ending, "основа на ㄹ: ㄹ выпадает перед ㄴ, 으 не нужен"),
            (_, null) => (stem + ending, "основа на гласную: без 으"),
            _ => (stem + "으" + ending, "основа на согласную: вставляется 으"),
        };
        return new(form, $"{label}: {rule} → {form}.");
    }

    private static Conjugation Can(string stem, Irregularity irregularity)
    {
        var last = Syllable.Decompose(stem[^1]);
        string head = stem[..^1];
        var (attributive, rule) = (irregularity, last.Final) switch
        {
            (Irregularity.Bieup, 'ㅂ') => (head + last.WithFinal(null) + "울", "ㅂ-неправильный: ㅂ → 우 + ㄹ"),
            (Irregularity.Digeut, 'ㄷ') => (head + last.WithFinal('ㄹ') + "을", "ㄷ-неправильный: ㄷ → ㄹ + 을"),
            (Irregularity.Siot, 'ㅅ') => (head + last.WithFinal(null) + "을", "ㅅ-неправильный: ㅅ выпадает + 을"),
            (Irregularity.Hieut, 'ㅎ') => (head + last.WithFinal('ㄹ'), "ㅎ-неправильный: ㅎ → ㄹ"),
            (_, 'ㄹ') => (stem, "основа уже оканчивается на ㄹ"),
            (_, null) => (head + last.WithFinal('ㄹ'), "основа на гласную: ㄹ становится патчимом"),
            _ => (stem + "을", "основа на согласную: + 을"),
        };
        string form = attributive + " 수 있어요";
        return new(form, $"-(으)ㄹ 수 있어요: {rule} → {form}.");
    }

    /// <summary>The -아/어 form (가, 먹어, 해, 마셔 …) — the base of -아요, -아서, -았어요 and -아야.</summary>
    private static (string Form, string Rule) AeoWithRule(string stem, Irregularity irregularity)
    {
        var last = Syllable.Decompose(stem[^1]);
        string head = stem[..^1];

        if (stem[^1] == '하')
        {
            return (head + "해", "하 + 여 → 해");
        }

        switch (irregularity)
        {
            case Irregularity.Bieup when last.Final == 'ㅂ':
                bool wa = stem is "돕" or "곱";
                return (head + last.WithFinal(null) + (wa ? "와" : "워"), $"ㅂ-неправильный: ㅂ → {(wa ? "오 + 아 = 와" : "우 + 어 = 워")}");
            case Irregularity.Digeut when last.Final == 'ㄷ':
                string withRieul = head + last.WithFinal('ㄹ');
                return (withRieul + (IsBright(last.Medial) ? "아" : "어"), $"ㄷ-неправильный: ㄷ → ㄹ перед гласной ({withRieul})");
            case Irregularity.Reu when stem[^1] == '르' && stem.Length >= 2:
                var previous = Syllable.Decompose(stem[^2]).WithFinal('ㄹ');
                string la = IsBright(previous.Medial) ? "라" : "러";
                return (stem[..^2] + previous + la, $"르-неправильный: ㅡ выпадает, ㄹ удваивается → {previous}{la}");
            case Irregularity.Siot when last.Final == 'ㅅ':
                return (head + last.WithFinal(null) + (IsBright(last.Medial) ? "아" : "어"), "ㅅ-неправильный: ㅅ выпадает, гласные не сливаются");
            case Irregularity.Hieut when last.Final == 'ㅎ':
                char medial = last.Medial is 'ㅑ' or 'ㅕ' ? 'ㅒ' : 'ㅐ';
                return (head + last.WithFinal(null).WithMedial(medial), $"ㅎ-неправильный: ㅎ выпадает, гласная → {medial}");
        }

        bool bright = IsBright(last.Medial);
        string harmony = bright
            ? $"последняя гласная основы {last.Medial} «светлая» → 아"
            : $"последняя гласная основы {last.Medial} → 어";

        if (last.HasFinal)
        {
            return (stem + (bright ? "아" : "어"), harmony);
        }

        switch (last.Medial)
        {
            case 'ㅡ':
                bool prevBright = stem.Length > 1 && IsBright(Syllable.Decompose(stem[^2]).Medial);
                char replaced = prevBright ? 'ㅏ' : 'ㅓ';
                return (head + last.WithMedial(replaced), $"ㅡ выпадает перед -아/어 ({stem}{(prevBright ? "아" : "어")} → {head}{last.WithMedial(replaced)})");
            case 'ㅏ' or 'ㅓ' or 'ㅕ' or 'ㅐ' or 'ㅔ':
                return (stem, $"{harmony}; одинаковые гласные сливаются: {stem}");
            case 'ㅗ':
                return (head + last.WithMedial('ㅘ'), $"{harmony}; ㅗ + ㅏ = ㅘ");
            case 'ㅜ':
                return (head + last.WithMedial('ㅝ'), $"{harmony}; ㅜ + ㅓ = ㅝ");
            case 'ㅣ':
                return (head + last.WithMedial('ㅕ'), $"{harmony}; ㅣ + ㅓ = ㅕ");
            case 'ㅚ':
                return (head + last.WithMedial('ㅙ'), $"{harmony}; ㅚ + ㅓ = ㅙ");
            default:
                return (stem + (bright ? "아" : "어"), harmony);
        }
    }

    private static string SafeAeo(string stem, Irregularity irregularity) => AeoWithRule(stem, irregularity).Form;

    /// <summary>Stem + 아/어 without contraction or irregular changes: what a beginner writes first.</summary>
    private static string RawAeo(string stem, bool bright) => stem + (bright ? "아" : "어");

    private static bool IsBright(string stem) => IsBright(Syllable.Decompose(stem[^1]).Medial);

    private static bool IsBright(char medial) => BrightVowels.Contains(medial);

    private static string? WithFinal(string stem, char final)
    {
        var last = Syllable.Decompose(stem[^1]);
        return last.HasFinal ? null : stem[..^1] + last.WithFinal(final);
    }

    // For a regular verb, the "wrong" irregular reading a learner might over-apply (입다 → *이워요).
    private static Irregularity GuessIrregularity(string stem) => Syllable.Decompose(stem[^1]).Final switch
    {
        'ㅂ' => Irregularity.Bieup,
        'ㄷ' => Irregularity.Digeut,
        'ㅅ' => Irregularity.Siot,
        _ when stem[^1] == '르' => Irregularity.Reu,
        _ => Irregularity.None,
    };
}
