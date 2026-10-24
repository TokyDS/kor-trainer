using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Grammar;
using KorTrainer.Domain.Hangul;

namespace KorTrainer.Domain.Exercises;

public sealed record GrammarNoun(string Korean, string Russian);

public sealed record GrammarVerb(string Korean, string Russian, PartOfSpeech PartOfSpeech, Irregularity Irregularity);

/// <summary>How many questions come as gaps in real sentences.</summary>
/// <param name="Share">0–1; at 1 only topics with sentences are drilled.</param>
/// <param name="RevealEnding">Name the ending; otherwise the learner picks it by meaning among other endings.</param>
public sealed record ClozeSettings(IReadOnlyList<ClozeMatch> Matches, double Share, bool RevealEnding);

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

    // "Cannot like", "go in order to know" sound odd.
    private static readonly HashSet<string> NoAbility = ["알다", "모르다", "좋아하다", "싫어하다"];

    // Endings that join clauses; a gap mid-sentence is only confused with these.
    private static readonly HashSet<Ending> Connectives =
        [Ending.And, Ending.SoThat, Ending.Because, Ending.But, Ending.If, Ending.BecauseGi];

    private const int MeaningDistractors = 2;

    private static readonly HashSet<Ending> ActionEndings =
    [
        Ending.Want, Ending.Can, Ending.Must, Ending.Cannot, Ending.Please, Ending.ForMe,
        Ending.Progressive, Ending.Purpose, Ending.Intention, Ending.LetUs,
    ];

    public IReadOnlyList<ChoiceExercise> Generate(
        IReadOnlyList<GrammarTopic> topics,
        int count,
        IReadOnlyList<GrammarNoun> nouns,
        IReadOnlyList<GrammarVerb> verbs,
        ClozeSettings? cloze = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        if (topics.Count == 0)
        {
            throw new ArgumentException("At least one topic is required.", nameof(topics));
        }

        var usableNouns = nouns.Where(n => n.Korean.All(Syllable.IsSyllable)).ToList();
        var usableVerbs = verbs.Where(v => v.Korean.Length >= 2 && v.Korean.All(Syllable.IsSyllable) && v.Korean[^1] == '다').ToList();

        var sentences = (cloze?.Matches ?? [])
            .GroupBy(m => m.Topic.Type)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ClozeMatch>)g.ToList());
        bool sentencesOnly = cloze is { Share: >= 1 };

        // Topics without words to practise on are skipped instead of failing the whole set.
        var pool = topics;
        topics = topics
            .Where(t => sentencesOnly
                ? sentences.ContainsKey(t.Type)
                : t.Particle is not null ? usableNouns.Count > 0 : VerbPool(t, usableVerbs).Count > 0)
            .ToList();
        if (topics.Count == 0)
        {
            throw new InvalidOperationException("The deck has no words for the requested grammar topics.");
        }

        ChoiceExercise Next(GrammarTopic topic) =>
            sentences.TryGetValue(topic.Type, out var matches) && random.NextDouble() < cloze!.Share
                ? ClozeExercise(matches.Pick(random), cloze.RevealEnding, pool)
                : WordExercise(topic, usableNouns, usableVerbs);

        var result = new List<ChoiceExercise>(count);
        var seen = new HashSet<string>();
        for (int i = 0; i < count; i++)
        {
            var exercise = Next(topics.Pick(random));
            for (int attempt = 0; attempt < MaxUniqueAttempts && !seen.Add(exercise.ItemKey); attempt++)
            {
                exercise = Next(topics.Pick(random));
            }

            result.Add(exercise);
        }

        return result;
    }

    /// <summary>
    /// A gap in a real sentence. With the ending revealed it drills the form; otherwise the other options are
    /// the same verb with other endings of the same kind, and the translation decides.
    /// </summary>
    public ChoiceExercise ClozeExercise(ClozeMatch match, bool revealEnding, IReadOnlyList<GrammarTopic> pool)
    {
        ArgumentNullException.ThrowIfNull(match);
        var sentence = match.Sentence;
        string before = sentence.Korean[..match.Start];
        string after = sentence.Korean[(match.Start + match.Length)..];
        string translation = $"«{sentence.Russian}»{(sentence.MachineTranslated ? " (машинный перевод)" : string.Empty)}";
        string key = $"{match.Topic.Type}:s{sentence.Id}:{match.Start}";

        if (match.Noun is { } noun)
        {
            var particle = Particles.Attach(noun.Korean, match.Topic.Particle!.Value);
            var gap = ParticleGap(noun.Korean, particle);
            var choices = new[] { new ChoiceOption(gap.Right), new ChoiceOption(gap.Wrong, Why: particle.WrongWhy) }.Shuffled(random).ToList();
            return new ChoiceExercise(
                match.Topic.Type,
                key,
                $"{before}{gap.Lead}___{gap.Rest}{after}",
                $"{translation}{WordNote(noun.Korean, gap)} Выберите правильную форму.",
                choices,
                choices.FindIndex(o => o.Text == gap.Right),
                particle.Explanation,
                SpeakText: sentence.Korean);
        }

        var verb = match.Verb ?? throw new ArgumentException("A match carries a noun or a verb.", nameof(match));
        var ending = match.Topic.Ending!.Value;
        var conjugation = Conjugator.Conjugate(verb.Korean, ending, verb.Irregularity);
        string label = Conjugator.EndingLabel(ending);
        var mistakes = Conjugator.ExplainedMistakes(verb.Korean, ending, verb.Irregularity)
            .Shuffled(random)
            .Select(m => new ChoiceOption(m.Form, Why: m.Why));
        var distractors = revealEnding ? mistakes : OtherEndings(verb, ending, conjugation.Form, pool).Concat(mistakes);
        var (options, correct) = ChoiceBuilder.Shuffle(new ChoiceOption(conjugation.Form), distractors, OptionCount, random);

        return new ChoiceExercise(
            match.Topic.Type,
            key,
            $"{before}___ ({verb.Korean}){after}",
            revealEnding ? $"{translation} → {label}" : translation,
            options,
            correct,
            revealEnding ? conjugation.Explanation : $"По смыслу здесь {label}. {conjugation.Explanation}",
            SpeakText: sentence.Korean);
    }

    /// <summary>The same verb with other endings of the same kind: right forms, wrong meaning.</summary>
    private IEnumerable<ChoiceOption> OtherEndings(GrammarVerb verb, Ending ending, string correct, IReadOnlyList<GrammarTopic> pool)
    {
        bool connective = Connectives.Contains(ending);
        string wanted = Conjugator.EndingLabel(ending);
        return pool
            .Where(t => t.Ending is { } other && other != ending && Connectives.Contains(other) == connective)
            .Select(t => new ChoiceOption(
                Conjugator.Conjugate(verb.Korean, t.Ending!.Value, verb.Irregularity).Form,
                Why: $"Это {Conjugator.EndingLabel(t.Ending!.Value)}, а по смыслу здесь нужно {wanted}."))
            .Where(o => o.Text != correct)
            .DistinctBy(o => o.Text)
            .Shuffled(random)
            .Take(MeaningDistractors);
    }

    public ChoiceExercise ParticleExercise(GrammarTopic topic, GrammarNoun noun)
    {
        var kind = topic.Particle ?? throw new ArgumentException("Topic is not a particle topic.", nameof(topic));
        var particle = Particles.Attach(noun.Korean, kind);
        var (tail, translation) = ParticleFrame(kind, FirstSense(noun.Russian));
        var gap = ParticleGap(noun.Korean, particle);

        var options = new[] { new ChoiceOption(gap.Right), new ChoiceOption(gap.Wrong, Why: particle.WrongWhy) }
            .Shuffled(random)
            .ToList();

        return new ChoiceExercise(
            topic.Type,
            $"{topic.Type}:{noun.Korean}",
            $"{gap.Lead}___{gap.Rest}{tail}",
            $"{translation}{WordNote(noun.Korean, gap)} Выберите правильную форму.",
            options,
            options.FindIndex(o => o.Text == gap.Right),
            particle.Explanation,
            SpeakText: ParticleSpeakText(noun.Korean, kind));
    }

    /// <summary>
    /// What a particle gap asks for: only the particle after the noun, which stays in the prompt (책___ 아니에요 →
    /// 이 / 가). When the noun itself changes (저 + 가 = 제가), the gap covers the whole word.
    /// </summary>
    private static ParticleGapParts ParticleGap(string noun, ParticleResult particle)
    {
        int space = particle.Form.IndexOf(' ', StringComparison.Ordinal);
        string rest = space < 0 ? string.Empty : particle.Form[space..];
        string right = particle.Form[..^rest.Length];
        string wrong = particle.Wrong[..^rest.Length];
        return right.StartsWith(noun, StringComparison.Ordinal) && wrong.StartsWith(noun, StringComparison.Ordinal)
            ? new(noun, right[noun.Length..], wrong[noun.Length..], rest)
            : new(string.Empty, right, wrong, rest);
    }

    /// <summary>Names the word when the gap hides it: «(저)».</summary>
    private static string WordNote(string noun, ParticleGapParts gap) => gap.Lead.Length == 0 ? $" ({noun})" : string.Empty;

    private sealed record ParticleGapParts(string Lead, string Right, string Wrong, string Rest);

    /// <summary>What a particle question says aloud after the answer: 책을 좋아해요, 학생이에요.</summary>
    public static string ParticleSpeakText(string noun, ParticleKind kind) =>
        Particles.Attach(noun, kind).Form + ParticleFrame(kind, string.Empty).Tail.TrimEnd('.', '?');

    /// <summary>The rest of the phrase around a noun with a particle, and its Russian gloss.</summary>
    private static (string Tail, string Translation) ParticleFrame(ParticleKind kind, string meaning) => kind switch
    {
        ParticleKind.Copula => (".", $"«Это {meaning}.»"),
        ParticleKind.NegativeCopula => (".", $"«Это не {meaning}.»"),
        ParticleKind.Topic => (" 어디에 있어요?", $"«Где {meaning}?» (тема)"),
        ParticleKind.Subject => (" 있어요.", $"«Есть {meaning}.»"),
        ParticleKind.With => (" 빵을 샀어요.", $"«Купил(а): {meaning} и хлеб.»"),
        ParticleKind.Means => (" 만들어요.", $"«Делаю из этого: {meaning}.»"),
        _ => (" 좋아해요.", $"«Люблю: {meaning}.»"),
    };

    public ChoiceExercise VerbExercise(GrammarTopic topic, GrammarVerb verb)
    {
        var ending = topic.Ending ?? throw new ArgumentException("Topic is not a verb topic.", nameof(topic));
        var conjugation = Conjugator.Conjugate(verb.Korean, ending, verb.Irregularity);
        var mistakes = Conjugator.ExplainedMistakes(verb.Korean, ending, verb.Irregularity)
            .Shuffled(random)
            .Select(m => new ChoiceOption(m.Form, Why: m.Why));
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

    private ChoiceExercise WordExercise(GrammarTopic topic, IReadOnlyList<GrammarNoun> nouns, IReadOnlyList<GrammarVerb> verbs)
    {
        if (topic.Particle is not null)
        {
            return ParticleExercise(topic, nouns.Pick(random));
        }

        return VerbExercise(topic, VerbPool(topic, verbs).Pick(random));
    }

    private static List<GrammarVerb> VerbPool(GrammarTopic topic, IReadOnlyList<GrammarVerb> verbs) =>
        verbs.Where(v => Suits(topic, v)).ToList();

    /// <summary>Whether the drill asks for this verb in this topic's ending (some pairs sound odd or are ambiguous).</summary>
    public static bool Suits(GrammarTopic topic, GrammarVerb verb)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(verb);
        if (topic.Ending is not { } ending)
        {
            return false;
        }

        bool actionOnly = ActionEndings.Contains(ending);
        bool negation = ending is Ending.Not or Ending.NotJi;
        return (!actionOnly || (verb.PartOfSpeech == PartOfSpeech.Verb && !StateVerbs.Contains(verb.Korean)))
            && (ending != Ending.Purpose || !MotionVerbs.Contains(verb.Korean))
            && (!negation || !NoNegation.Contains(verb.Korean))
            && (ending is not (Ending.Cannot or Ending.Purpose) || !NoAbility.Contains(verb.Korean))

            // 피곤하다 is negated as 안 피곤해요, unlike the verb 공부 안 해요; keep the drill unambiguous.
            && (ending != Ending.Not || !IsHadaAdjective(verb));
    }
}
