using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Hangul;

namespace KorTrainer.Domain.Grammar;

/// <summary>A wrong form a learner is likely to choose, and why it is wrong for this very word.</summary>
public sealed record Mistake(string Form, string Why);

public static partial class Conjugator
{
    private const string AttachesToStem = "Это окончание присоединяется прямо к основе, а не к форме на -아/어.";

    /// <summary>
    /// Plausible wrong forms a learner produces: wrong vowel harmony, no contraction,
    /// ignoring (or wrongly applying) an irregular class, wrong 으 insertion.
    /// </summary>
    public static IReadOnlyList<string> CommonMistakes(string verb, Ending ending, Irregularity irregularity = Irregularity.None) =>
        ExplainedMistakes(verb, ending, irregularity).Select(m => m.Form).ToList();

    /// <summary><see cref="CommonMistakes"/> with the reason each form is wrong, shown when a learner picks it.</summary>
    public static IReadOnlyList<Mistake> ExplainedMistakes(string verb, Ending ending, Irregularity irregularity = Irregularity.None)
    {
        string stem = Stem(verb);
        string correct = Conjugate(verb, ending, irregularity).Form;
        var swapped = irregularity == Irregularity.None ? GuessIrregularity(stem) : Irregularity.None;

        var mistakes = ending switch
        {
            Ending.PolitePresent => AeoMistakes(stem, irregularity, swapped).Select(m => m with { Form = m.Form + "요" }),
            Ending.SoThat => AeoMistakes(stem, irregularity, swapped).Select(m => m with { Form = m.Form + "서" }),
            Ending.Must => AeoMistakes(stem, irregularity, swapped).Select(m => m with { Form = m.Form + "야 해요" }),
            Ending.ForMe => AeoMistakes(stem, irregularity, swapped).Select(m => m with { Form = m.Form + " 주세요" }),
            Ending.PolitePast => AeoMistakes(stem, irregularity, swapped)
                .Select(m => m with { Form = PastOf(m.Form) })
                .Append(new Mistake(SafeAeo(stem, irregularity) + "요", "Это настоящее время. В прошедшем к последнему слогу добавляют ㅆ и -어요.")),
            Ending.FormalPresent => FormalMistakes(stem),
            Ending.Not or Ending.Cannot => NegationMistakes(stem, ending, irregularity, swapped),
            Ending.Because or Ending.Please or Ending.If or Ending.Purpose => EuMistakes(verb, stem, ending, irregularity, swapped, correct),
            Ending.Can or Ending.Future or Ending.Intention or Ending.LetUs => AttributiveMistakes(verb, stem, ending, irregularity, swapped, correct),
            _ => SuffixMistakes(stem, ending),
        };

        return mistakes.Where(m => m.Form != correct).DistinctBy(m => m.Form).ToList();
    }

    /// <summary>Mistakes in the -아/어 form (without what follows it): 먹아, 가아, 이워, 먹으, 머거.</summary>
    private static IEnumerable<Mistake> AeoMistakes(string stem, Irregularity irregularity, Irregularity swapped)
    {
        var last = Syllable.Decompose(stem[^1]);
        bool bright = IsBright(stem);
        bool eu = !last.HasFinal && last.Medial == 'ㅡ';
        const string EuDrops = "ㅡ выпадает перед -아/어, а 아 или 어 выбирают по гласной предыдущего слога.";
        string irregularWhy = irregularity == Irregularity.None ? string.Empty : " " + IrregularWhy(stem + "다", irregularity);

        string harmonyWhy = stem[^1] == '하' ? "У 하다 своя форма: 하 → 해."
            : eu ? EuDrops
            : bright ? $"После «светлой» гласной {last.Medial} ставится 아, а не 어."
            : $"После гласной {last.Medial} ставится 어: 아 бывает только после ㅏ и ㅗ.";
        yield return new(RawAeo(stem, !bright), harmonyWhy + irregularWhy);

        // 바꾸어요, 되어요, 보내어요 are correct uncontracted spellings, so they are no mistake.
        if (last.HasFinal || !OptionalContraction.Contains(last.Medial) || irregularity != Irregularity.None)
        {
            string why = irregularity != Irregularity.None ? IrregularWhy(stem + "다", irregularity)
                : stem[^1] == '하' ? "하 и окончание сливаются в 해."
                : eu ? EuDrops
                : $"Гласные {last.Medial} и {(bright ? "ㅏ" : "ㅓ")} сливаются в один слог: {SafeAeo(stem, irregularity)}.";
            yield return new(RawAeo(stem, bright), why);
        }

        yield return new(SafeAeo(stem, swapped), SwapWhy(stem, irregularity, swapped));

        // ㅐ/ㅔ sound the same, so 해 is often written 헤; other stems get a stray 으 or 여 (먹으요, 가여요).
        yield return stem[^1] == '하'
            ? new(stem[..^1] + "헤", "Пишется 해 (ㅐ), а не 헤 (ㅔ): звучат они одинаково.")
            : last.HasFinal
            ? new(stem + "으", "Перед -아/어 으 не вставляют: 으 нужен в других окончаниях, вроде -(으)면 и -(으)세요.")
            : new(stem + "여", "Окончание здесь -아/어; 여 бывает только в 하 + 여 → 해.");

        // Spelling by ear: 먹어요 is heard [머거요].
        yield return new(Pronunciation.Pronounce(SafeAeo(stem, irregularity)).Text,
            "Так слово звучит, но пишется по основе: на письме патчим не переносят в следующий слог.");
    }

    private static IEnumerable<Mistake> FormalMistakes(string stem)
    {
        var last = Syllable.Decompose(stem[^1]);
        string why = last.Final == 'ㄹ' ? $"У основы {stem} на ㄹ ㄹ выпадает, а ㅂ становится патчимом, затем 니다."
            : last.HasFinal ? $"Основа {stem} оканчивается согласной — после неё пишется -습니다."
            : $"Основа {stem} оканчивается гласной — ㅂ становится её патчимом, затем 니다.";
        return
        [
            new(stem + "습니다", why),
            new(stem + "읍니다", "읍니다 — старое написание; сейчас пишут -습니다 или -ㅂ니다."),
            new(WithFinal(stem, 'ㅂ') is { } b ? b + "니다" : stem + "ㅂ니다", why),
            new(stem + "니다", why),
        ];
    }

    private static IEnumerable<Mistake> NegationMistakes(string stem, Ending ending, Irregularity irregularity, Irregularity swapped)
    {
        string adverb = ending == Ending.Not ? "안" : "못";
        string other = ending == Ending.Not ? "못" : "안";
        string otherWhy = ending == Ending.Not
            ? "못 значит «не могу», а здесь нужно простое «не» — 안."
            : "안 значит просто «не», а здесь нужно «не могу» — 못.";
        if (IsNounHada(stem))
        {
            string head = stem[..^1];
            return
            [
                new($"{adverb} {head}해요", $"В глаголах «существительное + 하다» {adverb} ставится перед 하다, внутрь: {head} {adverb} 해요."),
                new($"{head} {adverb} 하요", "하 и окончание -아/어요 сливаются в 해요."),
                new($"{head} {other} 해요", otherWhy),
            ];
        }

        return AeoMistakes(stem, irregularity, swapped)
            .Select(m => m with { Form = $"{adverb} {m.Form}요" })
            .Append(new Mistake($"{other} {SafeAeo(stem, irregularity)}요", otherWhy));
    }

    private static IEnumerable<Mistake> EuMistakes(
        string verb, string stem, Ending ending, Irregularity irregularity, Irregularity swapped, string correct)
    {
        string suffix = ending switch
        {
            Ending.Because => "니까",
            Ending.Please => "세요",
            Ending.If => "면",
            _ => "러 가요",
        };

        var last = Syllable.Decompose(stem[^1]);
        string choiceWhy = irregularity != Irregularity.None ? IrregularWhy(verb, irregularity)
            : last.Final == 'ㄹ' && DropsRieul(suffix) ? $"Основа на ㄹ: перед {Syllable.Decompose(suffix[0]).Initial} ㄹ выпадает, 으 не нужен."
            : last.Final == 'ㄹ' ? "Основа на ㄹ: 으 не нужен, ㄹ остаётся."
            : last.HasFinal ? $"Основа {stem} оканчивается согласной — нужен 으."
            : $"Основа {stem} оканчивается гласной — 으 не нужен.";

        var mistakes = new List<Mistake>
        {
            new(stem + "으" + suffix, choiceWhy),
            new(stem + suffix, choiceWhy),
            new(Conjugate(verb, ending, swapped).Form, SwapWhy(stem, irregularity, swapped)),

            // Over-using the -아/어 form, the present attributive -는 or the future -ㄹ.
            new(SafeAeo(stem, irregularity) + (ending == Ending.Because ? "서니까" : suffix), AttachesToStem),
            new(stem + "는" + suffix, "-는 — окончание определения (먹는 사람), сюда оно не ставится."),
            new(AttributiveOf(stem, irregularity).Form + suffix, "-(으)ㄹ здесь лишнее: окончание присоединяется прямо к основе."),
        };

        if (last.Final == 'ㄹ' && !DropsRieul(suffix))
        {
            mistakes.Add(new(stem[..^1] + last.WithFinal(null) + suffix,
                $"Перед {Syllable.Decompose(suffix[0]).Initial} ㄹ не выпадает: она выпадает только перед ㄴ, ㅂ и ㅅ."));
        }

        if (ending == Ending.Please)
        {
            mistakes.Add(new(correct.Replace("세요", "새요", StringComparison.Ordinal), "Пишется 세 (ㅔ), а не 새 (ㅐ)."));

            // 먹으세요 is only less respectful than 드세요, not wrong, so it is no distractor.
            string lessPolite = EuForm(stem, irregularity, suffix, string.Empty).Form;
            mistakes.RemoveAll(m => m.Form == lessPolite);
        }

        return mistakes;
    }

    private static IEnumerable<Mistake> AttributiveMistakes(
        string verb, string stem, Ending ending, Irregularity irregularity, Irregularity swapped, string correct)
    {
        string tail = ending switch
        {
            Ending.Can => " 수 있어요",
            Ending.Future => " 거예요",
            Ending.Intention => "게요",
            _ => "까요",
        };

        var last = Syllable.Decompose(stem[^1]);
        string choiceWhy = irregularity != Irregularity.None ? IrregularWhy(verb, irregularity)
            : last.Final == 'ㄹ' ? $"Основа {stem} уже оканчивается на ㄹ — ничего добавлять не нужно."
            : last.HasFinal ? $"Основа {stem} оканчивается согласной — нужен 을."
            : $"Основа {stem} оканчивается гласной — ㄹ становится её патчимом, 을 не нужен.";
        const string PresentAttributive = "-는 — определение в настоящем времени, а здесь нужно будущее -(으)ㄹ.";

        var mistakes = new List<Mistake>
        {
            new(stem + "을" + tail, choiceWhy),
            new((WithFinal(stem, 'ㄹ') ?? stem) + tail, choiceWhy),
            new(Conjugate(verb, ending, swapped).Form, SwapWhy(stem, irregularity, swapped)),

            // 가 + 까요: the -아/어 form of 가다 is 가 itself, so the real slip is the missing ㄹ.
            new(SafeAeo(stem, irregularity) + tail, SafeAeo(stem, irregularity) == stem ? $"Не хватает ㄹ. {choiceWhy}" : AttachesToStem),
        };

        // Spelling what is heard: 거예요 → *거에요, 갈게요 [갈께요] → *갈께요, 갈까요 → *갈가요.
        mistakes.Add(ending switch
        {
            Ending.Future => new(correct.Replace("거예요", "거에요", StringComparison.Ordinal),
                "거 оканчивается гласной, поэтому пишется 거예요, хотя звучит [거에요]."),
            Ending.Intention => new(correct.Replace("게요", "께요", StringComparison.Ordinal), "Пишется 게요, хотя звучит [께요]."),
            Ending.LetUs => new(correct.Replace("까요", "가요", StringComparison.Ordinal), "Окончание пишется -(으)ㄹ까요, с ㄲ."),
            _ => new(stem + "는" + tail, PresentAttributive),
        });

        if (ending is Ending.Intention or Ending.LetUs)
        {
            mistakes.Add(new(stem + "는" + tail, PresentAttributive));
        }

        return mistakes;
    }

    private static IEnumerable<Mistake> SuffixMistakes(string stem, Ending ending)
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

        yield return new(RawAeo(stem, IsBright(stem)) + tail, AttachesToStem);
        yield return new(stem + "으" + tail, "으 здесь не нужен: окончание присоединяется прямо к основе.");
        yield return ending switch
        {
            Ending.And => new(stem + "구", "-구 — так говорят в разговоре, а пишется -고."),
            Ending.Want => new(stem + "고 싶다요", "싶다 тоже спрягается: 싶다 → 싶어요."),
            Ending.Progressive => new(stem + "고 있아요", "있다 → 있어요: после ㅣ ставится 어."),
            Ending.But => new(stem + "는지만", "-지만 присоединяется к основе без -는."),
            Ending.BecauseGi => new(stem + "는 때문에", "Перед 때문에 глагол становится существительным с -기."),
            _ => new(stem + "지 안아요", "Пишется 않아요 (патчим ㄶ), а не 안아요."),
        };

        if (ending == Ending.NotJi)
        {
            yield return new(stem + "지 않어요", "У 않다 гласная ㅏ, поэтому 않아요.");
        }
    }

    /// <summary>Why the regular or the irregular reading is wrong for this verb.</summary>
    private static string SwapWhy(string stem, Irregularity irregularity, Irregularity swapped) => irregularity != Irregularity.None
        ? IrregularWhy(stem + "다", irregularity)
        : $"{stem}다 — правильный глагол: {IrregularName(swapped)} изменение к нему не относится, основа {stem} не меняется.";

    private static string IrregularWhy(string verb, Irregularity irregularity) => irregularity switch
    {
        Irregularity.Bieup => $"{verb} — ㅂ-неправильный: перед гласной ㅂ превращается в 우 (오).",
        Irregularity.Digeut => $"{verb} — ㄷ-неправильный: перед гласной ㄷ → ㄹ.",
        Irregularity.Reu => $"{verb} — 르-неправильный: перед 아/어 ㅡ выпадает и ㄹ удваивается.",
        Irregularity.Siot => $"{verb} — ㅅ-неправильный: перед гласной ㅅ выпадает.",
        Irregularity.Hieut => $"{verb} — ㅎ-неправильный: ㅎ выпадает, гласная меняется.",
        _ => $"{verb} — правильный глагол.",
    };

    private static string IrregularName(Irregularity irregularity) => irregularity switch
    {
        Irregularity.Bieup => "ㅂ-неправильное",
        Irregularity.Digeut => "ㄷ-неправильное",
        Irregularity.Reu => "르-неправильное",
        Irregularity.Siot => "ㅅ-неправильное",
        Irregularity.Hieut => "ㅎ-неправильное",
        _ => "неправильное",
    };
}
