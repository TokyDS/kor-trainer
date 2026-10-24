using AwesomeAssertions;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Grammar;

namespace KorTrainer.Domain.Tests.Grammar;

public sealed class ConjugatorMistakeTests
{
    private static readonly (string Verb, Irregularity Irregularity)[] Verbs =
    [
        ("가다", Irregularity.None), ("먹다", Irregularity.None), ("오다", Irregularity.None), ("마시다", Irregularity.None),
        ("공부하다", Irregularity.None), ("살다", Irregularity.None), ("입다", Irregularity.None), ("바쁘다", Irregularity.None),
        ("듣다", Irregularity.Digeut), ("덥다", Irregularity.Bieup), ("모르다", Irregularity.Reu), ("짓다", Irregularity.Siot),
        ("빨갛다", Irregularity.Hieut),
    ];

    public static TheoryData<Ending> Endings => new(Enum.GetValues<Ending>());

    [Theory]
    [MemberData(nameof(Endings))]
    public void EveryMistake_SaysWhyItIsWrong(Ending ending)
    {
        foreach (var (verb, irregularity) in Verbs)
        {
            string correct = Conjugator.Conjugate(verb, ending, irregularity).Form;
            var mistakes = Conjugator.ExplainedMistakes(verb, ending, irregularity);

            mistakes.Should().NotBeEmpty($"{verb} {ending}");
            mistakes.Should().AllSatisfy(m =>
            {
                m.Form.Should().NotBe(correct);
                m.Why.Should().NotBeNullOrWhiteSpace($"{m.Form} for {verb} {ending}");
            });
            Conjugator.CommonMistakes(verb, ending, irregularity).Should().Equal(mistakes.Select(m => m.Form));
        }
    }

    [Theory]
    [InlineData("먹다", Ending.PolitePresent, Irregularity.None, "먹아요", "гласной ㅓ ставится 어")]
    [InlineData("가다", Ending.PolitePresent, Irregularity.None, "가아요", "сливаются")]
    [InlineData("입다", Ending.PolitePresent, Irregularity.None, "이워요", "правильный глагол")]
    [InlineData("듣다", Ending.PolitePresent, Irregularity.Digeut, "듣어요", "ㄷ → ㄹ")]
    [InlineData("공부하다", Ending.Not, Irregularity.None, "안 공부해요", "перед 하다")]
    [InlineData("가다", Ending.Not, Irregularity.None, "못 가요", "«не могу»")]
    [InlineData("가다", Ending.FormalPresent, Irregularity.None, "가습니다", "гласной")]
    [InlineData("먹다", Ending.If, Irregularity.None, "먹면", "нужен 으")]
    [InlineData("가다", Ending.Future, Irregularity.None, "갈 거에요", "예요")]
    [InlineData("가다", Ending.PolitePast, Irregularity.None, "가요", "настоящее время")]
    public void Mistake_IsExplainedForTheWord(string verb, Ending ending, Irregularity irregularity, string form, string reason)
    {
        var mistake = Conjugator.ExplainedMistakes(verb, ending, irregularity).Single(m => m.Form == form);

        mistake.Why.Should().Contain(reason);
    }
}
