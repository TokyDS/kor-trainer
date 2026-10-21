using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Grammar;
using KorTrainer.Domain.Hangul;

namespace KorTrainer.Domain.Exercises;

public sealed record GrammarNoun(string Korean, string Russian);

public sealed record GrammarVerb(string Korean, string Russian, PartOfSpeech PartOfSpeech, Irregularity Irregularity);

/// <summary>
/// Builds grammar questions from deck words: the answer comes from <see cref="Particles"/> or
/// <see cref="Conjugator"/>, distractors from the typical mistakes, the explanation from the rule.
/// </summary>
public sealed class GrammarExerciseGenerator(Random random)
{
    public const int OptionCount = 4;

    private const int MaxUniqueAttempts = 8;

    // Verbs for which "want/can/must" sound odd.
    private static readonly HashSet<string> StateVerbs = ["있다", "없다", "되다"];

    // "Go in order to go" sounds odd.
    private static readonly HashSet<string> MotionVerbs = ["가다", "오다", "다니다", "나가다", "나오다", "들어가다", "들어오다", "돌아가다", "돌아오다"];

    // Their negation is a different word: 있다 ↔ 없다, 알다 ↔ 모르다.
    private static readonly HashSet<string> NoNegation = ["있다", "없다", "알다", "모르다"];

    private static readonly HashSet<Ending> ActionEndings =
    [
        Ending.Want, Ending.Can, Ending.Must, Ending.Cannot, Ending.Please, Ending.ForMe,
        Ending.Progressive, Ending.Purpose, Ending.Intention, Ending.LetUs,
    ];

    public IReadOnlyList<ChoiceExercise> Generate(
        IReadOnlyList<GrammarTopic> topics,
        int count,
        IReadOnlyList<GrammarNoun> nouns,
        IReadOnlyList<GrammarVerb> verbs)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        if (topics.Count == 0)
        {
            throw new ArgumentException("At least one topic is required.", nameof(topics));
        }

        var usableNouns = nouns.Where(n => n.Korean.All(Syllable.IsSyllable)).ToList();
        var usableVerbs = verbs.Where(v => v.Korean.Length >= 2 && v.Korean.All(Syllable.IsSyllable) && v.Korean[^1] == '다').ToList();

        // Topics without words to practise on are skipped instead of failing the whole set.
        topics = topics.Where(t => t.Particle is not null ? usableNouns.Count > 0 : VerbPool(t, usableVerbs).Count > 0).ToList();
        if (topics.Count == 0)
        {
            throw new InvalidOperationException("The deck has no words for the requested grammar topics.");
        }
        var result = new List<ChoiceExercise>(count);
        var seen = new HashSet<string>();
        for (int i = 0; i < count; i++)
        {
            var exercise = Next(topics.Pick(random), usableNouns, usableVerbs);
            for (int attempt = 0; attempt < MaxUniqueAttempts && !seen.Add(exercise.ItemKey); attempt++)
            {
                exercise = Next(topics.Pick(random), usableNouns, usableVerbs);
            }

            result.Add(exercise);
        }

        return result;
    }

    public ChoiceExercise ParticleExercise(GrammarTopic topic, GrammarNoun noun)
    {
        var kind = topic.Particle ?? throw new ArgumentException("Topic is not a particle topic.", nameof(topic));
        var particle = Particles.Attach(noun.Korean, kind);
        string meaning = FirstSense(noun.Russian);
        var (rest, translation) = kind switch
        {
            ParticleKind.Copula => (".", $"«Это {meaning}.»"),
            ParticleKind.NegativeCopula => (".", $"«Это не {meaning}.»"),
            ParticleKind.Topic => (" 어디에 있어요?", $"«Где {meaning}?» (тема)"),
            ParticleKind.Subject => (" 있어요.", $"«Есть {meaning}.»"),
            ParticleKind.With => (" 빵을 샀어요.", $"«Купил(а): {meaning} и хлеб.»"),
            ParticleKind.Means => (" 만들어요.", $"«Делаю из этого: {meaning}.»"),
            _ => (" 좋아해요.", $"«Люблю: {meaning}.»"),
        };

        var options = new[] { new ChoiceOption(particle.Form), new ChoiceOption(particle.Wrong) }
            .Shuffled(random)
            .ToList();

        return new ChoiceExercise(
            topic.Type,
            $"{topic.Type}:{noun.Korean}",
            $"{noun.Korean}___{rest}",
            $"{translation} Выберите правильную форму.",
            options,
            options.FindIndex(o => o.Text == particle.Form),
            particle.Explanation,
            SpeakText: particle.Form + rest.TrimEnd('.', '?'));
    }

    public ChoiceExercise VerbExercise(GrammarTopic topic, GrammarVerb verb)
    {
        var ending = topic.Ending ?? throw new ArgumentException("Topic is not a verb topic.", nameof(topic));
        var conjugation = Conjugator.Conjugate(verb.Korean, ending, verb.Irregularity);
        var mistakes = Conjugator.CommonMistakes(verb.Korean, ending, verb.Irregularity)
            .Shuffled(random)
            .Select(m => new ChoiceOption(m));
        var (options, correct) = ChoiceBuilder.Shuffle(new ChoiceOption(conjugation.Form), mistakes, OptionCount, random);

        return new ChoiceExercise(
            topic.Type,
            $"{topic.Type}:{verb.Korean}",
            verb.Korean,
            $"{FirstSense(verb.Russian)} → {Conjugator.EndingLabel(ending)}",
            options,
            correct,
            conjugation.Explanation,
            SpeakText: conjugation.Form);
    }

    private static bool IsHadaAdjective(GrammarVerb verb) =>
        verb.PartOfSpeech == PartOfSpeech.Adjective && verb.Korean.Length > 2 && verb.Korean.EndsWith("하다", StringComparison.Ordinal);

    /// <summary>First meaning of a Russian gloss: "студент, ученик" → "студент".</summary>
    public static string FirstSense(string russian)
    {
        int cut = russian.IndexOfAny([',', ';', '(']);
        return (cut > 0 ? russian[..cut] : russian).Trim();
    }

    private ChoiceExercise Next(GrammarTopic topic, IReadOnlyList<GrammarNoun> nouns, IReadOnlyList<GrammarVerb> verbs)
    {
        if (topic.Particle is not null)
        {
            return ParticleExercise(topic, nouns.Pick(random));
        }

        return VerbExercise(topic, VerbPool(topic, verbs).Pick(random));
    }

    private static List<GrammarVerb> VerbPool(GrammarTopic topic, IReadOnlyList<GrammarVerb> verbs)
    {
        bool actionOnly = topic.Ending is { } ending && ActionEndings.Contains(ending);
        bool negation = topic.Ending is Ending.Not or Ending.NotJi;
        return verbs
            .Where(v => !actionOnly || (v.PartOfSpeech == PartOfSpeech.Verb && !StateVerbs.Contains(v.Korean)))
            .Where(v => topic.Ending != Ending.Purpose || !MotionVerbs.Contains(v.Korean))
            .Where(v => !negation || !NoNegation.Contains(v.Korean))

            // 피곤하다 is negated as 안 피곤해요, unlike the verb 공부 안 해요; keep the drill unambiguous.
            .Where(v => topic.Ending != Ending.Not || !IsHadaAdjective(v))
            .ToList();
    }
}
