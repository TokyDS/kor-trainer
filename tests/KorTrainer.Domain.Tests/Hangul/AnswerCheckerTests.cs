using AwesomeAssertions;
using KorTrainer.Domain.Hangul;

namespace KorTrainer.Domain.Tests.Hangul;

public sealed class AnswerCheckerTests
{
    [Theory]
    [InlineData("학교", "학교")]
    [InlineData("안녕히 가세요", "안녕히가세요")]
    [InlineData("얼마예요?", " 얼마예요 ")]
    public void Check_AcceptsTheWordIgnoringSpacesAndPunctuation(string expected, string actual) =>
        AnswerChecker.Check(expected, actual).IsCorrect.Should().BeTrue();

    [Fact]
    public void Check_PointsAtTheWrongVowel()
    {
        var result = AnswerChecker.Check("어머니", "오머니");

        result.IsCorrect.Should().BeFalse();
        result.Syllables[0].IsMatch.Should().BeFalse();
        result.Syllables[0].Note.Should().Contain("ㅗ вместо ㅓ");
        result.Syllables.Skip(1).Should().OnlyContain(s => s.IsMatch);
    }

    [Theory]
    [InlineData("한국", "하국", "нет патчима ㄴ")]
    [InlineData("바다", "반다", "лишний патчим ㄴ")]
    [InlineData("밖", "박", "патчим ㄱ вместо ㄲ")]
    [InlineData("까다", "가다", "ㄱ вместо ㄲ")]
    public void Check_DescribesFinalAndConsonantMistakes(string expected, string actual, string note) =>
        AnswerChecker.Check(expected, actual).Syllables.Single(s => !s.IsMatch).Note.Should().Contain(note);

    [Fact]
    public void Check_ReportsMissingAndExtraSyllables()
    {
        var shorter = AnswerChecker.Check("선생님", "선생");
        var longer = AnswerChecker.Check("책", "책상");

        shorter.Syllables[2].Note.Should().Contain("не хватает «님»");
        longer.IsCorrect.Should().BeFalse();
        longer.Extra.Should().Be("상");
    }
}
