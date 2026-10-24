using AwesomeAssertions;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Exercises;
using KorTrainer.Domain.Grammar;

namespace KorTrainer.Domain.Tests.Exercises;

public sealed class GrammarClozeTests
{
    private static readonly GrammarNoun[] Nouns = [new("사과", "яблоко"), new("영화", "фильм"), new("가", "край")];

    private static readonly GrammarVerb[] Verbs =
    [
        new("보다", "смотреть", PartOfSpeech.Verb, Irregularity.None),
        new("먹다", "есть", PartOfSpeech.Verb, Irregularity.None),
        new("가다", "идти", PartOfSpeech.Verb, Irregularity.None),
        new("사다", "покупать", PartOfSpeech.Verb, Irregularity.None),
        new("살다", "жить", PartOfSpeech.Verb, Irregularity.None),
    ];

    private static GrammarTopic Topic(string type) => GrammarCatalog.Find(type)!;

    private static IReadOnlyList<ClozeMatch> Find(string korean, params string[] types) =>
        GrammarClozeFinder.Find([new ClozeSentence(1, korean, "перевод", false)], types.Select(Topic), Nouns, Verbs);

    [Fact]
    public void Find_LocatesAConjugatedVerb()
    {
        var match = Find("어제 영화를 봤어요.", ExerciseTypes.Past).Should().ContainSingle().Subject;

        match.Form.Should().Be("봤어요");
        match.Verb!.Korean.Should().Be("보다");
        "어제 영화를 봤어요."[match.Start..(match.Start + match.Length)].Should().Be("봤어요");
    }

    [Fact]
    public void Find_PrefersTheLongestForm()
    {
        var match = Find("밥을 먹고 싶어요", ExerciseTypes.AndGo, ExerciseTypes.Want).Should().ContainSingle().Subject;

        match.Topic.Type.Should().Be(ExerciseTypes.Want);
        match.Form.Should().Be("먹고 싶어요");
    }

    [Fact]
    public void Find_SeesThroughPunctuationAndFindsParticles()
    {
        var matches = Find("\"사과를 먹어요?\"", ExerciseTypes.ObjectParticle, ExerciseTypes.PolitePresent);

        matches.Select(m => m.Form).Should().Equal("사과를", "먹어요");
    }

    [Fact]
    public void Find_SkipsOneSyllableNounsAndFormsSharedByTwoWords()
    {
        Find("가는 길", ExerciseTypes.TopicParticle).Should().BeEmpty();

        // 사요 belongs to 사다 alone (살다 gives 살아요), so it can be blanked.
        Find("빵을 사요", ExerciseTypes.PolitePresent).Should().ContainSingle();
    }

    [Fact]
    public void Find_TreatsASentenceInitialConnectiveAsAConjunction()
    {
        GrammarVerb[] verbs = [new("하다", "делать", PartOfSpeech.Verb, Irregularity.None)];

        var matches = GrammarClozeFinder.Find([new ClozeSentence(1, "하지만 공부를 하지만 …", "но", false)], [Topic(ExerciseTypes.But)], Nouns, verbs);

        matches.Should().ContainSingle().Which.Start.Should().Be(8);
    }

    [Fact]
    public void ClozeExercise_BlanksTheFormAndVoicesTheWholeSentence()
    {
        var generator = new GrammarExerciseGenerator(new Random(1));
        var match = Find("어제 영화를 봤어요.", ExerciseTypes.Past)[0];

        var exercise = generator.ClozeExercise(match, revealEnding: true, [Topic(ExerciseTypes.Past)]);

        exercise.Prompt.Should().Be("어제 영화를 ___ (보다).");
        exercise.Correct.Text.Should().Be("봤어요");
        exercise.PromptNote.Should().Contain("перевод").And.Contain("прошедшее");
        exercise.SpeakText.Should().Be("어제 영화를 봤어요.");
        exercise.Type.Should().Be(ExerciseTypes.Past);
    }

    [Fact]
    public void ClozeExercise_ByMeaning_OffersOtherEndingsOfTheSameVerb()
    {
        var generator = new GrammarExerciseGenerator(new Random(2));
        var match = Find("어제 영화를 봤어요.", ExerciseTypes.Past)[0];
        GrammarTopic[] pool = [Topic(ExerciseTypes.Past), Topic(ExerciseTypes.Future), Topic(ExerciseTypes.Want), Topic(ExerciseTypes.AndGo)];

        var exercise = generator.ClozeExercise(match, revealEnding: false, pool);

        exercise.Options.Select(o => o.Text).Should().Contain(["볼 거예요", "보고 싶어요"]).And.NotContain("보고");
        exercise.PromptNote.Should().NotContain("прошедшее");
        exercise.Explanation.Should().StartWith("По смыслу здесь прошедшее");
    }

    [Fact]
    public void ParticleGap_KeepsTheNounInTheSentence()
    {
        var generator = new GrammarExerciseGenerator(new Random(5));
        var match = Find("저는 사과를 좋아해요.", ExerciseTypes.ObjectParticle).Single();

        var exercise = generator.ClozeExercise(match, revealEnding: true, []);

        exercise.Prompt.Should().Be("저는 사과___ 좋아해요.");
        exercise.Options.Select(o => o.Text).Should().BeEquivalentTo(["를", "을"]);
        exercise.Correct.Text.Should().Be("를");
        exercise.SpeakText.Should().Be("저는 사과를 좋아해요.");
    }

    [Fact]
    public void Generate_MixesSentenceGapsIntoATopicDrill()
    {
        var generator = new GrammarExerciseGenerator(new Random(3));
        var matches = Find("어제 영화를 봤어요.", ExerciseTypes.Past);

        var exercises = generator.Generate([Topic(ExerciseTypes.Past)], 30, Nouns, Verbs, new ClozeSettings(matches, 0.5, RevealEnding: true));

        exercises.Should().Contain(e => e.Prompt.Contains("___", StringComparison.Ordinal))
            .And.Contain(e => !e.Prompt.Contains("___", StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_SentencesOnly_DropsTopicsWithoutSentences()
    {
        var generator = new GrammarExerciseGenerator(new Random(4));
        var matches = Find("어제 영화를 봤어요.", ExerciseTypes.Past);

        var exercises = generator.Generate(
            [Topic(ExerciseTypes.Past), Topic(ExerciseTypes.Want)], 5, Nouns, Verbs, new ClozeSettings(matches, 1, RevealEnding: false));

        exercises.Should().AllSatisfy(e => e.Type.Should().Be(ExerciseTypes.Past));
    }
}
