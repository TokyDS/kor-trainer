using AwesomeAssertions;
using KorTrainer.Domain.Exercises;

namespace KorTrainer.Domain.Tests.Exercises;

public sealed class GrammarHintTests
{
    [Theory]
    [InlineData("의자___.", "자", "патчима нет")]
    [InlineData("책___ 아니에요.", "책", "ㄱ")]
    [InlineData("이거는 누구 스키___?", "키", "патчима нет")]
    [InlineData("연필___ 써요.", "필", "ㄹ")]
    public void ParticleGap_PointsAtTheLastSyllable(string prompt, string syllable, string detail)
    {
        string? hint = GrammarHints.For(prompt);

        hint.Should().Contain(syllable).And.Contain(detail);
    }

    [Theory]
    [InlineData("먹다", "먹", "ㅓ")]
    [InlineData("가다", "가", "ㅏ")]
    [InlineData("공부하다", "하", "하다")]
    public void Verb_PointsAtTheStem(string prompt, string syllable, string detail) =>
        GrammarHints.For(prompt).Should().Contain(syllable).And.Contain(detail);

    [Theory]
    [InlineData("25")]
    [InlineData("___ (저)")]
    [InlineData("Сколько стоит?")]
    public void NothingToPointAt_GivesNoHint(string prompt) => GrammarHints.For(prompt).Should().BeNull();
}
