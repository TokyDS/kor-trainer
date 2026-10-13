using AwesomeAssertions;
using KorTrainer.Domain.Entities;
using KorTrainer.Domain.Grammar;

namespace KorTrainer.Domain.Tests.Grammar;

public sealed class ConjugatorTests
{
    [Theory]
    [InlineData("가다", Irregularity.None, "가요")]
    [InlineData("서다", Irregularity.None, "서요")]
    [InlineData("오다", Irregularity.None, "와요")]
    [InlineData("보다", Irregularity.None, "봐요")]
    [InlineData("먹다", Irregularity.None, "먹어요")]
    [InlineData("앉다", Irregularity.None, "앉아요")]
    [InlineData("읽다", Irregularity.None, "읽어요")]
    [InlineData("좋다", Irregularity.None, "좋아요")]
    [InlineData("있다", Irregularity.None, "있어요")]
    [InlineData("마시다", Irregularity.None, "마셔요")]
    [InlineData("기다리다", Irregularity.None, "기다려요")]
    [InlineData("배우다", Irregularity.None, "배워요")]
    [InlineData("주다", Irregularity.None, "줘요")]
    [InlineData("되다", Irregularity.None, "돼요")]
    [InlineData("쉬다", Irregularity.None, "쉬어요")]
    [InlineData("보내다", Irregularity.None, "보내요")]
    [InlineData("하다", Irregularity.None, "해요")]
    [InlineData("공부하다", Irregularity.None, "공부해요")]
    [InlineData("쓰다", Irregularity.None, "써요")]
    [InlineData("크다", Irregularity.None, "커요")]
    [InlineData("바쁘다", Irregularity.None, "바빠요")]
    [InlineData("아프다", Irregularity.None, "아파요")]
    [InlineData("예쁘다", Irregularity.None, "예뻐요")]
    [InlineData("살다", Irregularity.None, "살아요")]
    [InlineData("만들다", Irregularity.None, "만들어요")]
    [InlineData("받다", Irregularity.None, "받아요")]
    [InlineData("입다", Irregularity.None, "입어요")]
    [InlineData("듣다", Irregularity.Digeut, "들어요")]
    [InlineData("걷다", Irregularity.Digeut, "걸어요")]
    [InlineData("덥다", Irregularity.Bieup, "더워요")]
    [InlineData("어렵다", Irregularity.Bieup, "어려워요")]
    [InlineData("돕다", Irregularity.Bieup, "도와요")]
    [InlineData("모르다", Irregularity.Reu, "몰라요")]
    [InlineData("부르다", Irregularity.Reu, "불러요")]
    [InlineData("다르다", Irregularity.Reu, "달라요")]
    [InlineData("낫다", Irregularity.Siot, "나아요")]
    [InlineData("빨갛다", Irregularity.Hieut, "빨개요")]
    [InlineData("하얗다", Irregularity.Hieut, "하얘요")]
    public void PolitePresent_FollowsVowelHarmonyContractionAndIrregulars(string verb, Irregularity irregularity, string expected) =>
        Conjugator.Conjugate(verb, Ending.PolitePresent, irregularity).Form.Should().Be(expected);

    [Theory]
    [InlineData("가다", "갑니다")]
    [InlineData("먹다", "먹습니다")]
    [InlineData("하다", "합니다")]
    [InlineData("공부하다", "공부합니다")]
    [InlineData("만들다", "만듭니다")]
    [InlineData("살다", "삽니다")]
    [InlineData("듣다", "듣습니다")]
    [InlineData("덥다", "덥습니다")]
    [InlineData("마시다", "마십니다")]
    public void FormalPresent_UsesSeumnidaOrBieupNida(string verb, string expected) =>
        Conjugator.Conjugate(verb, Ending.FormalPresent).Form.Should().Be(expected);

    [Theory]
    [InlineData("가다", Irregularity.None, "갔어요")]
    [InlineData("먹다", Irregularity.None, "먹었어요")]
    [InlineData("하다", Irregularity.None, "했어요")]
    [InlineData("보다", Irregularity.None, "봤어요")]
    [InlineData("마시다", Irregularity.None, "마셨어요")]
    [InlineData("쓰다", Irregularity.None, "썼어요")]
    [InlineData("되다", Irregularity.None, "됐어요")]
    [InlineData("듣다", Irregularity.Digeut, "들었어요")]
    [InlineData("덥다", Irregularity.Bieup, "더웠어요")]
    [InlineData("모르다", Irregularity.Reu, "몰랐어요")]
    public void PolitePast_AddsSsangSiotToTheAeoForm(string verb, Irregularity irregularity, string expected) =>
        Conjugator.Conjugate(verb, Ending.PolitePast, irregularity).Form.Should().Be(expected);

    [Theory]
    [InlineData("가다", Ending.Because, Irregularity.None, "가니까")]
    [InlineData("먹다", Ending.Because, Irregularity.None, "먹으니까")]
    [InlineData("살다", Ending.Because, Irregularity.None, "사니까")]
    [InlineData("듣다", Ending.Because, Irregularity.Digeut, "들으니까")]
    [InlineData("덥다", Ending.Because, Irregularity.Bieup, "더우니까")]
    [InlineData("가다", Ending.Can, Irregularity.None, "갈 수 있어요")]
    [InlineData("먹다", Ending.Can, Irregularity.None, "먹을 수 있어요")]
    [InlineData("만들다", Ending.Can, Irregularity.None, "만들 수 있어요")]
    [InlineData("듣다", Ending.Can, Irregularity.Digeut, "들을 수 있어요")]
    [InlineData("돕다", Ending.Can, Irregularity.Bieup, "도울 수 있어요")]
    [InlineData("가다", Ending.SoThat, Irregularity.None, "가서")]
    [InlineData("먹다", Ending.SoThat, Irregularity.None, "먹어서")]
    [InlineData("바쁘다", Ending.SoThat, Irregularity.None, "바빠서")]
    [InlineData("공부하다", Ending.Must, Irregularity.None, "공부해야 해요")]
    [InlineData("먹다", Ending.Must, Irregularity.None, "먹어야 해요")]
    [InlineData("먹다", Ending.And, Irregularity.None, "먹고")]
    [InlineData("가다", Ending.Want, Irregularity.None, "가고 싶어요")]
    public void LevelTwoEndings_FollowTheirRules(string verb, Ending ending, Irregularity irregularity, string expected) =>
        Conjugator.Conjugate(verb, ending, irregularity).Form.Should().Be(expected);

    [Theory]
    [InlineData("먹다", Ending.PolitePresent, Irregularity.None, "먹아요")]
    [InlineData("가다", Ending.PolitePresent, Irregularity.None, "가아요")]
    [InlineData("듣다", Ending.PolitePresent, Irregularity.Digeut, "듣어요")]
    [InlineData("입다", Ending.PolitePresent, Irregularity.None, "이워요")]
    [InlineData("먹다", Ending.FormalPresent, Irregularity.None, "먹읍니다")]
    [InlineData("먹다", Ending.Because, Irregularity.None, "먹니까")]
    public void CommonMistakes_ContainTypicalErrorsButNeverTheAnswer(string verb, Ending ending, Irregularity irregularity, string mistake)
    {
        var mistakes = Conjugator.CommonMistakes(verb, ending, irregularity);

        mistakes.Should().Contain(mistake);
        mistakes.Should().NotContain(Conjugator.Conjugate(verb, ending, irregularity).Form);
    }

    [Fact]
    public void Conjugate_ExplainsTheRule()
    {
        Conjugator.Conjugate("마시다", Ending.PolitePresent).Explanation.Should().Contain("ㅣ + ㅓ = ㅕ");
        Conjugator.Conjugate("만들다", Ending.FormalPresent).Explanation.Should().Contain("ㄹ выпадает");
    }

    [Theory]
    [InlineData("가")]
    [InlineData("먹다요")]
    [InlineData("go다")]
    public void Stem_RejectsNonDictionaryForms(string form) =>
        FluentActions.Invoking(() => Conjugator.Stem(form)).Should().Throw<ArgumentException>();
}
