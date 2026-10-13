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
            if (nouns.Count == 0)
            {
                throw new InvalidOperationException("No nouns available for particle drills.");
            }

            return ParticleExercise(topic, nouns.Pick(random));
        }

        bool actionOnly = topic.Ending is Ending.Want or Ending.Can or Ending.Must;
        var pool = verbs
            .Where(v => !actionOnly || (v.PartOfSpeech == PartOfSpeech.Verb && !StateVerbs.Contains(v.Korean)))
            .ToList();
        if (pool.Count == 0)
        {
            throw new InvalidOperationException("No verbs available for conjugation drills.");
        }

        return VerbExercise(topic, pool.Pick(random));
    }
}
