using AwesomeAssertions;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Exercises;
using KorTrainer.Domain.Grammar;
using KorTrainer.Domain.Plan;

namespace KorTrainer.Domain.Tests.Exercises;

public sealed class GrammarExerciseGeneratorTests
{
    private const int SampleSize = 150;

    private static readonly GrammarNoun[] Nouns =
    [
        new("책", "книга"), new("의자", "стул"), new("학생", "студент, ученик"), new("커피", "кофе"),
        new("물", "вода"), new("사과", "яблоко"), new("선생님", "учитель"), new("가방", "сумка, портфель"),
    ];

    private static readonly GrammarVerb[] Verbs =
    [
        new("가다", "идти", PartOfSpeech.Verb, Irregularity.None),
        new("먹다", "есть", PartOfSpeech.Verb, Irregularity.None),
        new("마시다", "пить", PartOfSpeech.Verb, Irregularity.None),
        new("듣다", "слушать", PartOfSpeech.Verb, Irregularity.Digeut),
        new("만들다", "делать", PartOfSpeech.Verb, Irregularity.None),
        new("공부하다", "учиться", PartOfSpeech.Verb, Irregularity.None),
        new("덥다", "жаркий", PartOfSpeech.Adjective, Irregularity.Bieup),
        new("바쁘다", "занятой", PartOfSpeech.Adjective, Irregularity.None),
        new("있다", "быть", PartOfSpeech.Verb, Irregularity.None),
    ];

    public static TheoryData<string> AllTopics => new(GrammarCatalog.Topics.Select(t => t.Type));

    [Theory]
    [MemberData(nameof(AllTopics))]
    public void Generate_ProducesValidQuestionsForEveryTopic(string type)
    {
        var topic = GrammarCatalog.Find(type)!;
        var generator = new GrammarExerciseGenerator(new Random(5));

        var exercises = generator.Generate([topic], SampleSize, Nouns, Verbs);

        exercises.Should().HaveCount(SampleSize);
        foreach (var exercise in exercises)
        {
            exercise.Type.Should().Be(type);
            exercise.Options.Count.Should().BeGreaterThanOrEqualTo(2);
            exercise.Options.Select(o => o.Text).Should().OnlyHaveUniqueItems();
            exercise.Explanation.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void ParticleExercise_AnswerMatchesBatchimRule()
    {
        var generator = new GrammarExerciseGenerator(new Random(1));
        var topic = GrammarCatalog.Find(ExerciseTypes.Copula)!;

        foreach (var exercise in generator.Generate([topic], SampleSize, Nouns, Verbs))
        {
            string noun = exercise.Prompt.Split("___")[0];
            exercise.Correct.Text.Should().Be(Particles.Attach(noun, ParticleKind.Copula).Form);
            exercise.PromptNote.Should().StartWith("«Это ");
        }
    }

    [Fact]
    public void VerbExercise_AnswerMatchesConjugator()
    {
        var generator = new GrammarExerciseGenerator(new Random(2));
        var topic = GrammarCatalog.Find(ExerciseTypes.PolitePresent)!;

        foreach (var exercise in generator.Generate([topic], SampleSize, Nouns, Verbs))
        {
            var verb = Verbs.Single(v => v.Korean == exercise.Prompt);
            exercise.Correct.Text.Should().Be(Conjugator.Conjugate(verb.Korean, Ending.PolitePresent, verb.Irregularity).Form);
            exercise.SpeakText.Should().Be(exercise.Correct.Text);
        }
    }

    [Fact]
    public void WantCanMust_UseOnlyActionVerbs()
    {
        var generator = new GrammarExerciseGenerator(new Random(3));
        var topics = new[] { ExerciseTypes.Want, ExerciseTypes.Can, ExerciseTypes.Must }.Select(t => GrammarCatalog.Find(t)!).ToList();

        var prompts = generator.Generate(topics, SampleSize, Nouns, Verbs).Select(e => e.Prompt);

        prompts.Should().NotContain(["덥다", "바쁘다", "있다"]);
    }

    [Fact]
    public void Generate_SkipsMultiWordEntries()
    {
        var generator = new GrammarExerciseGenerator(new Random(4));
        GrammarNoun[] nouns = [new("안녕히 가세요", "до свидания"), new("책", "книга")];

        var exercises = generator.Generate([GrammarCatalog.Find(ExerciseTypes.TopicParticle)!], 10, nouns, Verbs);

        exercises.Should().OnlyContain(e => e.Prompt.StartsWith("책"));
    }

    [Fact]
    public void Generate_SkipsTopicsWithoutWordsAndFailsOnlyWhenNothingIsLeft()
    {
        var generator = new GrammarExerciseGenerator(new Random(6));
        var topics = new[] { ExerciseTypes.Copula, ExerciseTypes.PolitePresent }.Select(t => GrammarCatalog.Find(t)!).ToList();

        var exercises = generator.Generate(topics, 20, [], Verbs);
        var act = () => generator.Generate([GrammarCatalog.Find(ExerciseTypes.Copula)!], 5, [], Verbs);

        exercises.Should().OnlyContain(e => e.Type == ExerciseTypes.PolitePresent);
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("студент, ученик", "студент")]
    [InlineData("утро; завтрак", "утро")]
    [InlineData("до свидания (уходящему)", "до свидания")]
    [InlineData("вода", "вода")]
    public void FirstSense_TakesTheFirstMeaning(string russian, string expected) =>
        GrammarExerciseGenerator.FirstSense(russian).Should().Be(expected);
}
