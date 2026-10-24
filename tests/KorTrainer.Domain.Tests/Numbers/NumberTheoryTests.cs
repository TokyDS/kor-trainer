using AwesomeAssertions;
using KorTrainer.Domain.Common;
using KorTrainer.Domain.Numbers;

namespace KorTrainer.Domain.Tests.Numbers;

public sealed class NumberTheoryTests
{
    [Theory]
    [InlineData(ExerciseTypes.SinoNumbers)]
    [InlineData(ExerciseTypes.NativeNumbers)]
    [InlineData(ExerciseTypes.Counters)]
    [InlineData(ExerciseTypes.Time)]
    [InlineData(ExerciseTypes.Money)]
    public void EveryNumberDrill_HasARule(string type)
    {
        var theory = NumberTheories.Find(type);

        theory.Should().NotBeNull();
        theory!.Steps.Should().NotBeEmpty();
        theory.Table.Should().NotBeEmpty().And.OnlyContain(s => s.Korean.Length > 0 && s.Written.Length > 0);
        theory.Examples.Should().HaveCountGreaterThanOrEqualTo(3).And.OnlyContain(s => s.Korean.Length > 0);
        theory.Pitfall.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Samples_SayWhatTheDrillsCheck()
    {
        var sino = NumberTheories.Find(ExerciseTypes.SinoNumbers)!;
        var native = NumberTheories.Find(ExerciseTypes.NativeNumbers)!;

        sino.Examples.Should().Contain(new NumberSample("15 000", "만 오천", "10 000 + 5000: считают по 만"));
        sino.Examples.Should().Contain(s => s.Korean == "유월");
        native.Examples.Should().Contain(s => s.Korean == "스무 살");
        NumberTheories.Find(ExerciseTypes.Time)!.Examples.Should().Contain(new NumberSample("10:30", "열 시 삼십 분"));
    }
}
