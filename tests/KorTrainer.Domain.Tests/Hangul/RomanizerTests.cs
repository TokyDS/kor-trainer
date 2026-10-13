using AwesomeAssertions;
using KorTrainer.Domain.Hangul;

namespace KorTrainer.Domain.Tests.Hangul;

public sealed class RomanizerTests
{
    [Theory]
    [InlineData("안녕하세요", "annyeonghaseyo")]
    [InlineData("감사합니다", "gamsahamnida")]
    [InlineData("한국어", "hangugeo")]
    [InlineData("서울", "seoul")]
    [InlineData("설날", "seollal")]
    [InlineData("학교", "hakgyo")]
    [InlineData("없어요", "eopseoyo")]
    [InlineData("김치", "gimchi")]
    [InlineData("의자", "uija")]
    [InlineData("꽃", "kkot")]
    public void Romanize_UsesRevisedRomanizationOfThePronunciation(string text, string expected) =>
        Romanizer.Romanize(text).Should().Be(expected);

    [Fact]
    public void Romanize_KeepsSpacesAndPunctuation() =>
        Romanizer.Romanize("네, 아니요.").Should().Be("ne, aniyo.");
}
