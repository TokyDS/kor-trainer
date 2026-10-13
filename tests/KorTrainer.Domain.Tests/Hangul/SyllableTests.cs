using AwesomeAssertions;
using KorTrainer.Domain.Hangul;

namespace KorTrainer.Domain.Tests.Hangul;

public sealed class SyllableTests
{
    [Theory]
    [InlineData('가', 'ㄱ', 'ㅏ', null)]
    [InlineData('한', 'ㅎ', 'ㅏ', 'ㄴ')]
    [InlineData('글', 'ㄱ', 'ㅡ', 'ㄹ')]
    [InlineData('닭', 'ㄷ', 'ㅏ', 'ㄺ')]
    [InlineData('뭐', 'ㅁ', 'ㅝ', null)]
    [InlineData('힣', 'ㅎ', 'ㅣ', 'ㅎ')]
    [InlineData('까', 'ㄲ', 'ㅏ', null)]
    public void Decompose_SplitsSyllableIntoJamo(char syllable, char initial, char medial, char? final)
    {
        var result = Syllable.Decompose(syllable);

        result.Initial.Should().Be(initial);
        result.Medial.Should().Be(medial);
        result.Final.Should().Be(final);
    }

    [Fact]
    public void ComposeAndDecompose_RoundTripEverySyllableInTheUnicodeBlock()
    {
        for (char c = Syllable.First; c <= Syllable.Last; c++)
        {
            Syllable.Decompose(c).Compose().Should().Be(c);
        }
    }

    [Fact]
    public void Compose_BuildsSyllableFromJamo()
    {
        new Syllable('ㅎ', 'ㅏ', 'ㄴ').Compose().Should().Be('한');
        new Syllable('ㄱ', 'ㅜ', 'ㄱ').Compose().Should().Be('국');
        new Syllable('ㅇ', 'ㅓ').Compose().Should().Be('어');
    }

    [Fact]
    public void Jamo_ListsLettersInWritingOrder()
    {
        Syllable.Decompose('한').Jamo.Should().Equal('ㅎ', 'ㅏ', 'ㄴ');
        Syllable.Decompose('가').Jamo.Should().Equal('ㄱ', 'ㅏ');
    }

    [Theory]
    [InlineData('a')]
    [InlineData('ㄱ')]
    [InlineData('1')]
    public void Decompose_RejectsNonSyllables(char c)
    {
        var act = () => Syllable.Decompose(c);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_RejectsJamoInTheWrongPosition()
    {
        FluentActions.Invoking(() => new Syllable('ㅏ', 'ㅏ')).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new Syllable('ㄱ', 'ㄱ')).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new Syllable('ㄱ', 'ㅏ', 'ㄸ')).Should().Throw<ArgumentException>();
    }
}
