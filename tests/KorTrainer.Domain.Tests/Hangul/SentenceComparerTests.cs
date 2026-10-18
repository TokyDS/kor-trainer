using AwesomeAssertions;
using KorTrainer.Domain.Hangul;

namespace KorTrainer.Domain.Tests.Hangul;

public sealed class SentenceComparerTests
{
    [Fact]
    public void SameSentence_IgnoringSpacesAndPunctuation_IsExact()
    {
        var result = SentenceComparer.Compare("학교에 가요.", "학교에가요");

        result.Score.Should().Be(1);
        result.IsExact.Should().BeTrue();
        result.Parts.Should().ContainSingle().Which.Should().Be(new DiffPart("학교에가요", DiffKind.Match));
    }

    [Fact]
    public void MissingSyllable_DoesNotShiftTheRest()
    {
        var result = SentenceComparer.Compare("학교에 가요", "학교 가요");

        result.Parts.Should().Equal(
            new DiffPart("학교", DiffKind.Match),
            new DiffPart("에", DiffKind.Missing),
            new DiffPart("가요", DiffKind.Match));
        result.Score.Should().BeApproximately(0.8, 0.001);
        result.IsExact.Should().BeFalse();
    }

    [Fact]
    public void WrongSyllable_IsMissingPlusExtra()
    {
        var result = SentenceComparer.Compare("바다", "파다");

        result.Parts.Should().Equal(
            new DiffPart("바", DiffKind.Missing),
            new DiffPart("파", DiffKind.Extra),
            new DiffPart("다", DiffKind.Match));
        result.Score.Should().Be(0.5);
    }

    [Fact]
    public void EmptyAnswer_ScoresZero()
    {
        var result = SentenceComparer.Compare("안녕하세요", "");

        result.Score.Should().Be(0);
        result.Parts.Should().ContainSingle().Which.Kind.Should().Be(DiffKind.Missing);
    }

    [Fact]
    public void ExtraWords_LowerTheScoreOnlyForPrecision()
    {
        var result = SentenceComparer.Compare("네", "네 네 네");

        result.Score.Should().BeApproximately(1 / 3.0, 0.001);
    }
}
