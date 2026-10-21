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

    /// <summary>-(으)ㄹ 거예요: future / assumption.</summary>
    Future,

    /// <summary>-(으)ㄹ게요: "I will" (a promise to the listener).</summary>
    Intention,

    /// <summary>-(으)ㄹ까요?: "shall we?".</summary>
    LetUs,

    /// <summary>안 + -아/어요: short negation.</summary>
    Not,

    /// <summary>못 + -아/어요: "cannot".</summary>
    Cannot,

    /// <summary>-지 않아요: long negation.</summary>
    NotJi,

    /// <summary>-(으)세요: honorific present and polite request.</summary>
    Please,

    /// <summary>-아/어 주세요: "please do it for me".</summary>
    ForMe,

    /// <summary>-고 있어요: an action in progress.</summary>
    Progressive,

    /// <summary>-지만: "but".</summary>
    But,

    /// <summary>-(으)면: "if / when".</summary>
    If,

    /// <summary>-(으)러 가요: "go in order to".</summary>
    Purpose,

    /// <summary>-기 때문에: "because" (written style).</summary>
    BecauseGi,
}

public sealed record Conjugation(string Form, string Explanation);

/// <summary>
/// Conjugates verbs and adjectives from their dictionary form (가다, 먹다, 듣다 …) by rule.
/// Irregular classes cannot be detected from spelling and are passed in from the deck.
/// </summary>
public static class Conjugator
{
    private const string BrightVowels = "ㅏㅑㅗ";

    // Verbs whose honorific is a different word: 먹다 → 드시다, 자다 → 주무시다, 있다 → 계시다.
    private static readonly Dictionary<string, string> HonorificStems = new()
    {
        ["먹"] = "드",
        ["자"] = "주무",
        ["있"] = "계",
    };

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
            Ending.Can => Attributive(stem, irregularity, " 수 있어요", "-(으)ㄹ 수 있어요"),
            Ending.Future => Attributive(stem, irregularity, " 거예요", "-(으)ㄹ 거예요"),
            Ending.Intention => Attributive(stem, irregularity, "게요", "-(으)ㄹ게요 (пишется 게, читается [께])"),
            Ending.LetUs => Attributive(stem, irregularity, "까요", "-(으)ㄹ까요"),
            Ending.Not => Negate(stem, irregularity, "안"),
            Ending.Cannot => Negate(stem, irregularity, "못"),
            Ending.NotJi => new(stem + "지 않아요", $"-지 않다 присоединяется к основе {stem} без изменений, даже у неправильных глаголов: {stem}지 않아요."),
            Ending.Please => Honorific(stem, irregularity),
            Ending.ForMe => Aeo(stem, irregularity, " 주세요", "-아/어 주세요"),
            Ending.Progressive => new(stem + "고 있어요", $"-고 있다 присоединяется к основе {stem} без изменений: {stem}고 있어요."),
            Ending.But => new(stem + "지만", $"-지만 присоединяется к основе {stem} без изменений: {stem}지만."),
            Ending.If => EuForm(stem, irregularity, "면", "-(으)면"),
            Ending.Purpose => EuForm(stem, irregularity, "러 가요", "-(으)러 가다"),
            Ending.BecauseGi => new(stem + "기 때문에", $"-기 때문에 присоединяется к основе {stem} без изменений: {stem}기 때문에."),
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

        var mistakes = ending switch
        {
            Ending.PolitePresent => AeoMistakes(stem, irregularity, swappedIrregularity).Select(m => m + "요"),
            Ending.SoThat => AeoMistakes(stem, irregularity, swappedIrregularity).Select(m => m + "서"),
            Ending.Must => AeoMistakes(stem, irregularity, swappedIrregularity).Select(m => m + "야 해요"),
            Ending.ForMe => AeoMistakes(stem, irregularity, swappedIrregularity).Select(m => m + " 주세요"),
            Ending.PolitePast => AeoMistakes(stem, irregularity, swappedIrregularity).Select(PastOf).Append(SafeAeo(stem, irregularity) + "요"),
            Ending.FormalPresent =>
            [
                stem + "습니다",
                stem + "읍니다",
                WithFinal(stem, 'ㅂ') is { } b ? b + "니다" : stem + "ㅂ니다",
                stem + "니다",
            ],
            Ending.Not or Ending.Cannot => NegationMistakes(stem, ending, irregularity, swappedIrregularity),
            Ending.Because or Ending.Please or Ending.If or Ending.Purpose => EuMistakes(verb, stem, ending, irregularity, swappedIrregularity, correct),
            Ending.Can or Ending.Future or Ending.Intention or Ending.LetUs => AttributiveMistakes(verb, stem, ending, irregularity, swappedIrregularity, correct),
            _ => SuffixMistakes(stem, ending),
        };

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
        Ending.Future => "будущее -(으)ㄹ 거예요",
        Ending.Intention => "-(으)ㄹ게요 «я сделаю (обещаю)»",
        Ending.LetUs => "-(으)ㄹ까요? «давайте …?»",
        Ending.Not => "отрицание 안 «не»",
        Ending.Cannot => "отрицание 못 «не могу»",
        Ending.NotJi => "отрицание -지 않아요",
        Ending.Please => "уважительное -(으)세요",
        Ending.ForMe => "просьба -아/어 주세요",
        Ending.Progressive => "-고 있어요 «сейчас делаю»",
        Ending.But => "-지만 «но»",
        Ending.If => "-(으)면 «если»",
        Ending.Purpose => "-(으)러 가요 «иду, чтобы …»",
        Ending.BecauseGi => "-기 때문에 «из-за того, что»",
        _ => ending.ToString(),
    };

    private static IEnumerable<string> AeoMistakes(string stem, Irregularity irregularity, Irregularity swappedIrregularity)
    {
        yield return RawAeo(stem, bright: !IsBright(stem));
        yield return RawAeo(stem, IsBright(stem));
        yield return SafeAeo(stem, swappedIrregularity);

        // ㅐ/ㅔ sound the same, so 해 is often written 헤; other stems get a stray 으 or 여 (먹으요, 가여요).
        yield return stem[^1] == '하' ? stem[..^1] + "헤" : stem + (Syllable.Decompose(stem[^1]).HasFinal ? "으" : "여");

        // Spelling by ear: 먹어요 is heard [머거요].
        yield return Pronunciation.Pronounce(SafeAeo(stem, irregularity)).Text;
    }

    private static IEnumerable<string> NegationMistakes(
        string stem, Ending ending, Irregularity irregularity, Irregularity swappedIrregularity)
    {
        string adverb = ending == Ending.Not ? "안" : "못";
        string other = ending == Ending.Not ? "못" : "안";
        if (IsNounHada(stem))
        {
            string head = stem[..^1];
            return [$"{adverb} {head}해요", $"{head} {adverb} 하요", $"{head} {other} 해요"];
        }

        return AeoMistakes(stem, irregularity, swappedIrregularity)
            .Select(m => $"{adverb} {m}요")
            .Append($"{other} {SafeAeo(stem, irregularity)}요");
    }

    private static IEnumerable<string> EuMistakes(
        string verb, string stem, Ending ending, Irregularity irregularity, Irregularity swappedIrregularity, string correct)
    {
        string suffix = ending switch
        {
            Ending.Because => "니까",
            Ending.Please => "세요",
            Ending.If => "면",
            _ => "러 가요",
        };

        var mistakes = new List<string>
        {
            stem + "으" + suffix,
            stem + suffix,
            Conjugate(verb, ending, swappedIrregularity).Form,

            // Over-using the -아/어 form, the present attributive -는 or the future -ㄹ.
            SafeAeo(stem, irregularity) + (ending == Ending.Because ? "서니까" : suffix),
            stem + "는" + suffix,
            AttributiveOf(stem, irregularity).Form + suffix,
        };

        var last = Syllable.Decompose(stem[^1]);
        if (last.Final == 'ㄹ' && !DropsRieul(suffix))
        {
            mistakes.Add(stem[..^1] + last.WithFinal(null) + suffix);
        }

        if (ending == Ending.Please)
        {
            mistakes.Add(correct.Replace("세요", "새요", StringComparison.Ordinal));

            // 먹으세요 is only less respectful than 드세요, not wrong, so it is no distractor.
            mistakes.Remove(EuForm(stem, irregularity, suffix, string.Empty).Form);
        }

        return mistakes;
    }

    private static IEnumerable<string> AttributiveMistakes(
        string verb, string stem, Ending ending, Irregularity irregularity, Irregularity swappedIrregularity, string correct)
    {
        string tail = ending switch
        {
            Ending.Can => " 수 있어요",
            Ending.Future => " 거예요",
            Ending.Intention => "게요",
            _ => "까요",
        };

        var mistakes = new List<string>
        {
            stem + "을" + tail,
            (WithFinal(stem, 'ㄹ') ?? stem) + tail,
            Conjugate(verb, ending, swappedIrregularity).Form,
            SafeAeo(stem, irregularity) + tail,
        };

        // Spelling what is heard: 거예요 → *거에요, 갈게요 [갈께요] → *갈께요, 갈까요 → *갈가요.
        mistakes.Add(ending switch
        {
            Ending.Future => correct.Replace("거예요", "거에요", StringComparison.Ordinal),
            Ending.Intention => correct.Replace("게요", "께요", StringComparison.Ordinal),
            Ending.LetUs => correct.Replace("까요", "가요", StringComparison.Ordinal),
            _ => stem + "는" + tail,
        });

        if (ending is Ending.Intention or Ending.LetUs)
        {
            mistakes.Add(stem + "는" + tail);
        }

        return mistakes;
    }

    private static IEnumerable<string> SuffixMistakes(string stem, Ending ending)
    {
        string tail = ending switch
        {
            Ending.And => "고",
            Ending.Want => "고 싶어요",
            Ending.Progressive => "고 있어요",
            Ending.But => "지만",
            Ending.BecauseGi => "기 때문에",
            Ending.NotJi => "지 않아요",
            _ => throw new ArgumentOutOfRangeException(nameof(ending), ending, null),
        };

        yield return RawAeo(stem, IsBright(stem)) + tail;
        yield return stem + "으" + tail;
        switch (ending)
        {
            case Ending.And:
                yield return stem + "구";
                break;
            case Ending.Want:
                yield return stem + "고 싶다요";
                break;
            case Ending.Progressive:
                yield return stem + "고 있아요";
                break;
            case Ending.But:
                yield return stem + "는지만";
                break;
            case Ending.BecauseGi:
                yield return stem + "는 때문에";
                break;
            default:
                yield return stem + "지 안아요";
                yield return stem + "지 않어요";
                break;
        }
    }

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

    /// <summary>Endings with 으 after a consonant: -(으)니까, -(으)세요, -(으)면, -(으)러.</summary>
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
            (_, 'ㄹ') when DropsRieul(ending) => (head + last.WithFinal(null) + ending,
                $"основа на ㄹ: ㄹ выпадает перед {Syllable.Decompose(ending[0]).Initial}, 으 не нужен"),
            (_, 'ㄹ') => (stem + ending, "основа на ㄹ: 으 не нужен, ㄹ остаётся"),
            (_, null) => (stem + ending, "основа на гласную: без 으"),
            _ => (stem + "으" + ending, "основа на согласную: вставляется 으"),
        };
        return new(form, $"{label}: {rule} → {form}.");
    }

    // ㄹ drops before ㄴ, ㅂ and ㅅ (사니까, 사세요) but stays before other sounds (살면, 놀러).
    private static bool DropsRieul(string ending) => Syllable.Decompose(ending[0]).Initial is 'ㄴ' or 'ㅂ' or 'ㅅ';

    private static Conjugation Honorific(string stem, Irregularity irregularity)
    {
        if (HonorificStems.TryGetValue(stem, out string? special))
        {
            string form = special + "세요";
            return new(form, $"У глагола {stem}다 есть особая уважительная форма {special}시다 → {form}. Форма {EuForm(stem, irregularity, "세요", string.Empty).Form} менее вежлива.");
        }

        return EuForm(stem, irregularity, "세요", "-(으)세요 (уважительный суффикс -(으)시- + 어요)");
    }

    /// <summary>The future attributive -(으)ㄹ: 갈, 먹을, 들을, 도울, 만들.</summary>
    private static (string Form, string Rule) AttributiveOf(string stem, Irregularity irregularity)
    {
        var last = Syllable.Decompose(stem[^1]);
        string head = stem[..^1];
        return (irregularity, last.Final) switch
        {
            (Irregularity.Bieup, 'ㅂ') => (head + last.WithFinal(null) + "울", "ㅂ-неправильный: ㅂ → 우 + ㄹ"),
            (Irregularity.Digeut, 'ㄷ') => (head + last.WithFinal('ㄹ') + "을", "ㄷ-неправильный: ㄷ → ㄹ + 을"),
            (Irregularity.Siot, 'ㅅ') => (head + last.WithFinal(null) + "을", "ㅅ-неправильный: ㅅ выпадает + 을"),
            (Irregularity.Hieut, 'ㅎ') => (head + last.WithFinal('ㄹ'), "ㅎ-неправильный: ㅎ → ㄹ"),
            (_, 'ㄹ') => (stem, "основа уже оканчивается на ㄹ"),
            (_, null) => (head + last.WithFinal('ㄹ'), "основа на гласную: ㄹ становится патчимом"),
            _ => (stem + "을", "основа на согласную: + 을"),
        };
    }

    private static Conjugation Attributive(string stem, Irregularity irregularity, string tail, string label)
    {
        var (attributive, rule) = AttributiveOf(stem, irregularity);
        string form = attributive + tail;
        return new(form, $"{label}: {rule} → {form}.");
    }

    /// <summary>안/못 go before the verb, but between the noun and 하다 in 공부하다 → 공부 안 해요.</summary>
    private static Conjugation Negate(string stem, Irregularity irregularity, string adverb)
    {
        string meaning = adverb == "안" ? "«не»" : "«не могу»";
        if (IsNounHada(stem))
        {
            string form = $"{stem[..^1]} {adverb} 해요";
            return new(form, $"{adverb} {meaning}: в глаголах «существительное + 하다» {adverb} встаёт перед 하다 → {form}.");
        }

        var (aeo, rule) = AeoWithRule(stem, irregularity);
        string result = $"{adverb} {aeo}요";
        return new(result, $"{adverb} {meaning} ставится перед глаголом в форме -아/어요: {rule} → {result}.");
    }

    // 공부하다, 운동하다 split; 좋아하다, 싫어하다 (built from -아/어하다) and 하다 itself do not.
    private static bool IsNounHada(string stem)
    {
        if (stem.Length < 2 || stem[^1] != '하')
        {
            return false;
        }

        var before = Syllable.Decompose(stem[^2]);
        return !(before.Initial == 'ㅇ' && !before.HasFinal && before.Medial is 'ㅏ' or 'ㅓ' or 'ㅕ');
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
            case 'ㅜ' when stem == "푸":
                // The only ㅜ-irregular verb: ㅜ drops instead of merging (푸다 → 퍼요).
                return ("퍼", "푸다 — особый глагол: ㅜ выпадает → 퍼");
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
