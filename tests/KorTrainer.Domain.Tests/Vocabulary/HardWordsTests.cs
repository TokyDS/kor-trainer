using AwesomeAssertions;
using KorTrainer.Domain.Srs;
using KorTrainer.Domain.Vocabulary;

namespace KorTrainer.Domain.Tests.Vocabulary;

public sealed class HardWordsTests
{
    [Theory]
    [InlineData(0, new Grade[] { Grade.Good, Grade.Good, Grade.Easy }, false)]
    [InlineData(2, new Grade[] { Grade.Good }, true)]
    [InlineData(1, new Grade[] { Grade.Again, Grade.Good, Grade.Again, Grade.Hard, Grade.Again }, true)]
    [InlineData(0, new Grade[] { Grade.Again, Grade.Good }, false)]
    [InlineData(0, new Grade[] { Grade.Again, Grade.Again, Grade.Again }, true)]
    public void IsHard_FlagsRepeatedFailures(int lapses, Grade[] recent, bool expected) =>
        HardWords.Score(lapses, recent).IsHard.Should().Be(expected);

    [Fact]
    public void Score_RanksLapsesAboveSingleSlips()
    {
        var many = HardWords.Score(4, [Grade.Good]);
        var few = HardWords.Score(2, [Grade.Again, Grade.Good]);

        many.Value.Should().BeGreaterThan(few.Value);
    }

    [Fact]
    public void Score_ExplainsWhyAWordIsHard() =>
        HardWords.Score(3, [Grade.Again, Grade.Again, Grade.Good]).Reason.Should().Be("забыто 3 раза, «Снова» 2 из 3 последних");
}
