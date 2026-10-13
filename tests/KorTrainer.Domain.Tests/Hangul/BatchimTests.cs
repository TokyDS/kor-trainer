using AwesomeAssertions;
using KorTrainer.Domain.Hangul;

namespace KorTrainer.Domain.Tests.Hangul;

public sealed class BatchimTests
{
    [Theory]
    [InlineData('책', true)]
    [InlineData('학', true)]
    [InlineData('생', true)]
    [InlineData('자', false)]
    [InlineData('구', false)]
    [InlineData('닭', true)]
    public void HasBatchim_UsesTheModulo28Rule(char syllable, bool expected) =>
        Batchim.HasBatchim(syllable).Should().Be(expected);

    [Fact]
    public void HasBatchim_AgreesWithDecompositionForEverySyllable()
    {
        for (char c = Syllable.First; c <= Syllable.Last; c++)
        {
            Batchim.HasBatchim(c).Should().Be(Syllable.Decompose(c).HasFinal);
        }
    }

    [Theory]
    [InlineData("학생", true)]
    [InlineData("의자", false)]
    [InlineData("선생님?", true)]
    [InlineData("커피 ", false)]
    public void EndsWithBatchim_LooksAtTheLastSyllable(string word, bool expected) =>
        Batchim.EndsWithBatchim(word).Should().Be(expected);

    [Theory]
    [InlineData("서울", true)]
    [InlineData("연필", true)]
    [InlineData("책", false)]
    public void EndsWithRieul_DetectsFinalRieul(string word, bool expected) =>
        Batchim.EndsWithRieul(word).Should().Be(expected);

    [Fact]
    public void EndsWithBatchim_RejectsTextWithoutHangul()
    {
        var act = () => Batchim.EndsWithBatchim("abc");

        act.Should().Throw<ArgumentException>();
    }
}
